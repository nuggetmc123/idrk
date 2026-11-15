#if RAGO_FUSION
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Rago.Net
{
    /// <summary>
    /// Online play over Photon Fusion 2 (Shared mode, Photon cloud - no servers to run).
    /// Market: everyone joins the session "RagoMarket". Raids: you join a random open session for the chosen map;
    /// the session's master client runs the queue rules (4-10 players, aims for 8, launches at 1:00), closes the
    /// session and tells everyone their spawn. Game data is sent as small binary packets (NetGame) and voice too.
    /// The App ID comes from Resources/Rago/photon_appid.txt (written by the Multiplayer window).
    /// </summary>
    public class FusionBackend : MonoBehaviour, INetBackend, INetworkRunnerCallbacks
    {
        NetworkRunner _runner;
        string _status = "Offline";
        bool _ready, _queueing, _cancel, _leaving;
        string _sessionId = "";
        readonly List<ulong> _peers = new List<ulong>();
        readonly Dictionary<ulong, PlayerRef> _refs = new Dictionary<ulong, PlayerRef>();
        RaidStart _pendingStart;
        string _raidMap;
        int _nextSpawn = 1;

        public string Status => _status;
        public bool Ready => _ready;
        public bool InSession => _runner != null && _runner.IsRunning && !_leaving;
        public bool IsHost => _runner != null && _runner.IsRunning && _runner.IsSharedModeMasterClient;
        public ulong LocalId => _runner != null ? Id(_runner.LocalPlayer) : 0;
        public ulong HostId => IsHost ? LocalId : _hostId; // the master client announces itself every 2 s
        ulong _hostId;
        float _announce;
        public string SessionId => _sessionId;
        public IReadOnlyList<ulong> Peers => _peers;
        public event Action<ulong, byte[]> OnData;
        public event Action<ulong> OnPeerJoined;
        public event Action<ulong> OnPeerLeft;
        public event Action OnDisconnected;

        // Who a player is, as a number EVERY client agrees on: Photon's actor number for them in the room. (Fusion's
        // own player index isn't guaranteed to be the same on every machine - and when it wasn't, a player relayed
        // through the host showed up as a second, frozen body with someone else's name.)
        readonly Dictionary<PlayerRef, ulong> _idOf = new Dictionary<PlayerRef, ulong>();
        static MethodInfo _actorOf;
        ulong Id(PlayerRef p) => (ulong)p.PlayerId;

        // ------------------------------------------------------------------------------------ party
        // A party is its own small Photon session ("rago_party_<leader>") that members sit in while they're in
        // their bunkers. Invites:
        //  - in the bunker, players who aren't in a party sit in Photon's session lobby, which lists every party with
        //    an "inv" property naming who's invited - so an invite reaches them without being in the same session.
        //  - in a raid, an invite is sent straight to that player (you're in the same session); it takes effect when
        //    you're both back in your bunkers.
        // When the leader queues, everyone is told the name of a fresh raid session to join, so the party lands in
        // the same raid (random players can still fill the free spots) and shares one spawn spot.
        string _partyCode;
        bool _inPartySession, _wasLeader;
        readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, string> _peerParty = new Dictionary<ulong, string>();
        readonly List<string> _members = new List<string>();
        readonly List<string> _roster = new List<string>();   // the other members, as last seen together
        readonly List<string> _sessionPlayers = new List<string>();
        readonly List<PartyInvite> _invites = new List<PartyInvite>();
        readonly List<string> _invited = new List<string>();     // leader: callsigns invited to this party
        string _pendingRaidSession; int _pendingPartySize = 1; int _queuePartySize = 1;
        string _sessionKind = "";
        readonly HashSet<ulong> _acks = new HashSet<ulong>();           // members who answered the leader's "follow me"
        readonly Dictionary<ulong, float> _nameAsk = new Dictionary<ulong, float>();   // when we last asked a player who they are
        float _pingT, _lastSendWarn;
        // ---- reconnecting to a raid you dropped out of
        string _lastRaidSession, _lastRaidMap; bool _lastRaidNight; float _lastRaidAt = -999f;
        public bool CanReconnect => _ready && _lastRaidSession != null && Time.realtimeSinceStartup - _lastRaidAt < 300f && !_queueing;
        public async void Reconnect(Action<bool> done)
        {
            if (!CanReconnect) { done?.Invoke(false); return; }
            _status = "Reconnecting...";
            bool ok = false;
            // a dropped connection can take a few seconds to clear on Photon's side: try a few times
            for (int tryN = 0; tryN < 3 && !ok; tryN++)
            {
                if (tryN > 0) await Task.Delay(2500);
                ok = await StartSession(_lastRaidSession, new Dictionary<string, SessionProperty> { { "kind", "raid_started_" + _lastRaidMap } }, QueueRules.MaxFor(_lastRaidMap), false);
            }
            if (ok)
            {
                // the other players can take a moment to show up after joining
                float until = Time.realtimeSinceStartup + 4f;
                while (_peers.Count == 0 && Time.realtimeSinceStartup < until && InSession) { await Task.Delay(200); RefreshPeers(); }
                if (_peers.Count == 0) { ok = false; await Shutdown(); }   // nobody left in it: the raid is over
            }
            if (ok) foreach (var p in _peers) { SendSys(p, 3, MyName()); if (_partyCode != null) SendSys(p, 4, _partyCode); }
            _status = ok ? "Raid (reconnected)" : (_ready ? "Online (Photon)" : _status);
            if (!ok) _lastRaidSession = null;
            done?.Invoke(ok);
            if (!ok) RejoinParty();
        }
        public void ForgetRaid() { _lastRaidSession = null; }

        public string DebugInfo
        {
            get
            {
                if (_runner == null || !InSession) return "NET: not in a session  (" + _status + ")";
                var sb = new System.Text.StringBuilder();
                float now = Time.realtimeSinceStartup;
                sb.Append("NET ").Append(_sessionKind).Append("  me ").Append(LocalId).Append(" (f").Append(_runner.LocalPlayer.PlayerId).Append(")")
                  .Append(IsHost ? "  MASTER" : "  master " + _hostId).Append("  rtt ").Append((_rtt * 1000f).ToString("0")).Append("ms\n");
                foreach (var p in _peers)
                {
                    _refs.TryGetValue(p, out var pr);
                    sb.Append(p).Append(" (f").Append(pr.PlayerId).Append(") ").Append(_names.TryGetValue(p, out var n) ? n : "?")
                      .Append("  heard ").Append(_lastHeard.TryGetValue(p, out var h) ? (now - h).ToString("0.0") + "s" : "never").Append("\n");
                }
                return sb.ToString();
            }
        }
        string _lastRaid;
        bool _pendingNight, _queueNight;
        NetworkRunner _lobby;
        public string PartyCode => _partyCode;
        // the leader is whoever the party is named after - not whoever got back to their bunker first (that player opened
        // the party session and became its host, which made them "leader" and left the real leader unable to queue).
        // If the leader has left the party, the session's host takes over.
        public bool PartyLeader
        {
            get
            {
                if (_partyCode == null) return false;
                if (Key(_partyCode) == Key(MyName())) return true;
                if (!_inPartySession) return _wasLeader;
                foreach (var p in _peers) if (_names.TryGetValue(p, out var n) && Key(n) == Key(_partyCode)) return false;
                return IsHost && _names.Count >= _peers.Count;   // leader not here (and everyone's name is known): the host leads
            }
        }
        public IReadOnlyList<PartyInvite> Invites { get { _invites.RemoveAll(i => Time.realtimeSinceStartup - i.time > 300f); return _invites; } }
        public IReadOnlyList<string> PartyMembers
        {
            get
            {
                _members.Clear();
                if (_partyCode == null) return _members;
                _members.Add(MyName() + (PartyLeader ? "  (leader)" : ""));
                if (_inPartySession)
                {
                    foreach (var p in _peers) _members.Add(_names.TryGetValue(p, out var n) ? n : "joining...");
                    // remember who's in the party: while everyone moves from the party lobby into the raid (and back)
                    // people leave and arrive one by one - the list kept showing them kicked and rejoining
                    if (_queueing || _pendingRaidSession != null)
                    {
                        // heading into the raid: members who already went on ahead are still in the party
                        foreach (var n in _roster) if (!_members.Exists(x => Key(x) == Key(n))) _members.Add(n);
                    }
                    else if (_peers.Count > 0 && _names.Count >= _peers.Count) { _roster.Clear(); foreach (var p in _peers) if (_names.TryGetValue(p, out var n2)) _roster.Add(n2); }
                }
                else
                {
                    foreach (var n in _roster) _members.Add(n);
                    if (InSession)   // in a raid together: anyone else from the party who's in here
                        foreach (var p in _peers) if (IsTeammate(p) && _names.TryGetValue(p, out var n) && !_roster.Exists(x => Key(x) == Key(n))) { _roster.Add(n); _members.Add(n); }
                }
                return _members;
            }
        }
        public IReadOnlyList<string> SessionPlayers
        {
            get { _sessionPlayers.Clear(); if (InSession) foreach (var p in _peers) if (_names.TryGetValue(p, out var n)) _sessionPlayers.Add(n); return _sessionPlayers; }
        }
        public bool IsTeammate(ulong peer) => _partyCode != null && _peerParty.TryGetValue(peer, out var pc) && pc == _partyCode;
        public event Action OnPartyChanged;
        public event Action<string> OnPartyQueue;
        public event Action OnPartyQueueCancelled;
        public event Action<string> OnMetPlayer;

        /// <summary>A session property's text, whichever way this Fusion version stores it.</summary>
        static string Str(object prop)
        {
            if (prop == null) return "";
            var t = prop.GetType();
            var pv = t.GetProperty("PropertyValue") ?? t.GetProperty("Value");
            if (pv != null) { var v = pv.GetValue(prop); return v != null ? v.ToString() : ""; }
            var f = t.GetField("PropertyValue") ?? t.GetField("Value");
            if (f != null) { var v = f.GetValue(prop); return v != null ? v.ToString() : ""; }
            return prop.ToString();
        }

        static string MyName() => Profile.Current != null ? Profile.Current.DisplayName : "Operator";
        static string Key(string callsign) => (callsign ?? "").Trim().ToLowerInvariant();
        static string PartySession(string id)
        {
            var sb = new System.Text.StringBuilder("rago_party_");
            foreach (char c in Key(id)) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        /// <summary>Invite someone by callsign. Not in a party yet? You become the leader of a new one.</summary>
        public async void InviteToParty(string callsign)
        {
            if (!_ready || string.IsNullOrWhiteSpace(callsign)) return;
            if (Key(callsign) == Key(MyName())) return;
            if (_partyCode != null && PartyMembers.Count >= QueueRules.PartyMax) { Debug.Log("[Rago] party full"); return; }
            // in a raid (or the Market) with them: tell them directly
            if (InSession && !_inPartySession)
            {
                ulong to = 0; bool found = false;
                foreach (var kv in _names) if (Key(kv.Value) == Key(callsign) && _peers.Contains(kv.Key)) { to = kv.Key; found = true; }
                if (_partyCode == null) { _partyCode = MyName(); _wasLeader = true; OnPartyChanged?.Invoke(); }
                if (found) SendSys(to, 8, MyName() + "|" + _partyCode);
                else Remember(callsign);   // not here: send it from the bunker later
                return;
            }
            if (_partyCode == null) { if (!await EnterParty(MyName(), true)) return; }
            else if (!_inPartySession) { if (!await EnterParty(_partyCode, true)) return; }
            if (IsHost) { Remember(callsign); PublishInvites(); }
            else foreach (var p in _peers) SendSys(p, 7, callsign);   // members ask the leader to invite
        }

        void Remember(string callsign)
        {
            _invited.RemoveAll(x => Key(x) == Key(callsign));
            _invited.Add(callsign.Trim());
            while (_invited.Count > 12) _invited.RemoveAt(0);
        }

        /// <summary>
        /// The host lists everyone in the session (callsigns) in its properties, so players in their bunkers can see who
        /// is online in parties, raids and the Market - that's how a typed-in friend is found.
        /// </summary>
        void PublishNames()
        {
            if (_runner == null || !IsHost || !InSession) return;
            try
            {
                var props = new Dictionary<string, SessionProperty>();
                var cur = _runner.SessionInfo.Properties;
                if (cur != null) foreach (var kv in cur) if (kv.Key != "names") props[kv.Key] = kv.Value;
                props["names"] = NamesHere();
                if (_inPartySession) { props["kind"] = "party"; props["lead"] = MyName(); props["inv"] = string.Join(",", _invited.ConvertAll(Key)); }
                _runner.SessionInfo.UpdateCustomProperties(props);
            }
            catch (Exception e) { Debug.Log("[Rago] couldn't publish player names: " + e.Message); }
        }

        string NamesHere()
        {
            var names = new List<string> { MyName() };
            foreach (var p in _peers) if (_names.TryGetValue(p, out var n)) names.Add(n);
            return string.Join(",", names);
        }

        void PublishInvites()
        {
            if (!_inPartySession || !IsHost || _runner == null) return;
            try
            {
                _runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
                    { { "kind", "party" }, { "lead", MyName() }, { "inv", string.Join(",", _invited.ConvertAll(Key)) }, { "names", NamesHere() } });
            }
            catch (Exception e) { Debug.Log("[Rago] couldn't publish party invites: " + e.Message); }
        }

        public async void AcceptInvite(PartyInvite inv)
        {
            if (inv == null || !_ready) return;
            _invites.Remove(inv);
            if (_queueing) return;
            if (InSession && !_inPartySession)
            {
                // accepted inside a raid: you'll join them when you're both back in your bunkers
                _partyCode = inv.party; _wasLeader = false;
                OnPartyChanged?.Invoke();
                return;
            }
            _partyCode = null;
            bool ok = await EnterParty(inv.party, false);
            if (!ok) ok = await EnterParty(inv.party, true);   // the leader isn't back yet: open it for them
            OnPartyChanged?.Invoke();
        }

        public void DeclineInvite(PartyInvite inv) { _invites.Remove(inv); OnPartyChanged?.Invoke(); }

        public async void LeaveParty()
        {
            _partyCode = null; _wasLeader = false; _invited.Clear(); _roster.Clear();
            if (_inPartySession) { _inPartySession = false; await Shutdown(); }
            _status = _ready ? "Online (Photon)" : _status;
            OnPartyChanged?.Invoke();
            RejoinParty();   // back to listening for invites
        }

        public async void RejoinParty()
        {
            if (!_ready || _queueing) return;
            if (InSession && !_inPartySession) return;   // in the Market / a raid: wait for the bunker
            if (_partyCode != null)
            {
                if (_inPartySession) return;
                await EnterParty(_partyCode, true);
                if (_wasLeader && _invited.Count > 0) PublishInvites();
            }
            else await EnterLobby();
        }

        async Task<bool> EnterParty(string id, bool create)
        {
            _status = (create ? "Starting" : "Joining") + " party...";
            _names.Clear();
            // "inv" exists from the start (empty): properties added later may not be shown to other players' lobbies
            bool ok = await StartSession(PartySession(id), new Dictionary<string, SessionProperty> { { "kind", "party" }, { "lead", id }, { "inv", create ? string.Join(",", _invited.ConvertAll(Key)) : "" } }, QueueRules.PartyMax, create);
            if (ok && !create && _peers.Count == 0) { ok = false; await Shutdown(); }   // nobody there
            if (!ok) { OnPartyChanged?.Invoke(); return false; }
            _partyCode = id; _inPartySession = true;
            _wasLeader = IsHost;
            _status = "Party (" + (_peers.Count + 1) + ")";
            foreach (var p in _peers) SendSys(p, 3, MyName());
            OnPartyChanged?.Invoke();
            _ = EnterLobby();   // keep receiving invites from other parties while you're in this one
            return true;
        }

        // ---- the session lobby: lets you receive bunker invites while you're not in any session
        async Task EnterLobby()
        {
            if (_lobby != null || (InSession && !_inPartySession)) return;   // in a party (even just you): keep listening
            try
            {
                var go = new GameObject("FusionLobby");
                DontDestroyOnLoad(go);
                var r = go.AddComponent<NetworkRunner>();
                r.AddCallbacks(this);
                _lobby = r;
                var m = typeof(NetworkRunner).GetMethod("JoinSessionLobby", BindingFlags.Public | BindingFlags.Instance);
                if (m == null) { Debug.LogWarning("[Rago] this Fusion version has no session lobby: bunker invites need a raid together first"); return; }
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                var app = MakeAppSettings(AppId());
                for (int i = 0; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    if (pt.IsEnum && pt.Name == "SessionLobby") args[i] = Enum.Parse(pt, "Shared");
                    else if (app != null && pt.IsInstanceOfType(app)) args[i] = app;
                    else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (pt.IsValueType ? Activator.CreateInstance(pt) : null);
                }
                if (m.Invoke(r, args) is Task t) await t;
                if (!_inPartySession && (_status.StartsWith("Starting") || _status.StartsWith("Joining") || _status.StartsWith("Party"))) _status = "Online (Photon)";
            }
            catch (Exception e) { Debug.Log("[Rago] couldn't join the session lobby: " + e.Message); }
        }

        async Task LeaveLobby()
        {
            var r = _lobby; _lobby = null;
            if (r == null) return;
            try { await r.Shutdown(); } catch { }
            if (r != null) Destroy(r.gameObject);
        }

        // ---------------------------------------------------------------- players online
        int _listedPlayers = -1, _partyListed;
        float _listedAt = -999f;
        readonly Dictionary<string, (int players, bool started)> _listed = new Dictionary<string, (int, bool)>();
        object _statsSrc; PropertyInfo _onMaster, _inRooms; float _statsSearchT;

        /// <summary>
        /// How many people are playing right now. Photon's own app statistics (players on the master server - in their
        /// bunkers - plus players in sessions: the Market, parties and raids) when the SDK exposes them; otherwise
        /// everyone in a listed session plus you.
        /// </summary>
        int _statsCache = -1; float _statsCacheT = -999f;
        public int OnlineCount
        {
            get
            {
                // Photon's own count (everyone connected, bunkers included) is only sent while you're in the lobby: keep
                // the last one for a while so it doesn't drop to "just the people in my session" in the Market or a party
                // Photon's count of connections, minus party members' second (lobby) connection; it's only updated
                // while you're in the lobby, so an old number is only trusted for 30 s (it used to stick for 2 min)
                int s = StatsCount();
                if (s > 0) { _statsCache = Mathf.Max(1, s - _partyListed); _statsCacheT = Time.realtimeSinceStartup; }
                int here = InSession ? _peers.Count + 1 : 1;
                bool fresh = _statsCache > 0 && Time.realtimeSinceStartup - _statsCacheT < 30f;
                bool listFresh = _listedPlayers > 0 && Time.realtimeSinceStartup - _listedAt < 30f;
                if (fresh) return Mathf.Max(here, _statsCache);
                if (listFresh) return Mathf.Max(here, _listedPlayers);
                return here;
            }
        }

        int StatsCount()
        {
            if (_lobby == null) return -1;
            if (_statsSrc == null && Time.realtimeSinceStartup > _statsSearchT)
            {
                _statsSearchT = Time.realtimeSinceStartup + 5f;
                try { FindStats(_lobby, 0, new HashSet<object>(RefEq.I)); } catch { }
                if (_statsSrc != null) Debug.Log("[Rago] players-online stats found on " + _statsSrc.GetType().Name);
            }
            if (_statsSrc == null) return -1;
            try { return (int)_onMaster.GetValue(_statsSrc) + (int)_inRooms.GetValue(_statsSrc); }
            catch { _statsSrc = null; return -1; }
        }

        bool FindStats(object o, int depth, HashSet<object> seen)
        {
            if (o == null || depth > 6 || !seen.Add(o)) return false;
            var t = o.GetType();
            var pm = t.GetProperty("PlayersOnMasterCount", Any); var pr = t.GetProperty("PlayersInRoomsCount", Any);
            if (pm != null && pr != null && pm.PropertyType == typeof(int) && pr.PropertyType == typeof(int)) { _statsSrc = o; _onMaster = pm; _inRooms = pr; return true; }
            if (o is UnityEngine.Object && depth > 0) return false;
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var ft = f.FieldType;
                if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft.IsValueType || typeof(System.Collections.IEnumerable).IsAssignableFrom(ft) || typeof(Delegate).IsAssignableFrom(ft)) continue;
                object v; try { v = f.GetValue(o); } catch { continue; }
                if (FindStats(v, depth + 1, seen)) return true;
            }
            return false;
        }

        sealed class RefEq : IEqualityComparer<object>
        {
            public static readonly RefEq I = new RefEq();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner != _lobby || sessionList == null) return;
            int sum = 0;
            foreach (var si in sessionList)
            {
                var pc = si.GetType().GetProperty("PlayerCount", Any);   // read by name so a Fusion version difference can't break the build
                if (pc != null && pc.GetValue(si) is int c) sum += c;
            }
            // the online count: each party member is ALSO connected to the lobby (to hear invites), so Photon's own
            // count has them twice - remember how many are in parties to take them off again
            int inParties = 0; bool meListed = false;
            _listed.Clear();
            foreach (var si in sessionList)
            {
                int c = si.GetType().GetProperty("PlayerCount", Any)?.GetValue(si) is int pc2 ? pc2 : 0;
                var pr = si.Properties;
                string kind = pr != null && pr.TryGetValue("kind", out var kk2) ? Str(kk2) : "";
                bool started = pr != null && (pr.ContainsKey("go") || kind.StartsWith("raid_started"));
                if (kind == "party") inParties += c;
                if (si.Name == _sessionId && InSession) meListed = true;
                _listed[si.Name ?? ""] = (c, started);
            }
            _partyListed = inParties;
            _listedAt = Time.realtimeSinceStartup;
            _listedPlayers = sum + (meListed ? 0 : 1);   // + you, if you're not already counted in a listed session
            string me = Key(MyName());
            bool changed = false;
            foreach (var si in sessionList)   // everyone online in a party, raid or the Market: real players a friend can be added from
            {
                var pr = si.Properties;
                if (pr == null) continue;
                if (pr.TryGetValue("names", out var nm)) foreach (var n in Str(nm).Split(',')) KnownPlayers.Add(n);
                if (pr.TryGetValue("lead", out var ld)) KnownPlayers.Add(Str(ld));
            }
            foreach (var si in sessionList)
            {
                var props = si.Properties;
                if (props == null || !props.TryGetValue("kind", out var k) || Str(k) != "party") continue;
                if (!props.TryGetValue("inv", out var inv) || !props.TryGetValue("lead", out var lead)) continue;
                string invited = Str(inv), leader = Str(lead);
                KnownPlayers.Add(leader);   // a real player, online now
                if (string.IsNullOrEmpty(invited) || Array.IndexOf(invited.Split(','), me) < 0) continue;
                if (_invites.Exists(x => Key(x.party) == Key(leader))) continue;
                _invites.Add(new PartyInvite { from = leader, party = leader, time = Time.realtimeSinceStartup });
                changed = true;
                RagoGame.I?.Player?.Toast(leader + " invited you to their party - press the menu button to accept");
            }
            if (changed) OnPartyChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var go = new GameObject("RagoFusion");
            DontDestroyOnLoad(go);
            NetHub.Backend = go.AddComponent<FusionBackend>();
        }

        /// <summary>The Photon region everyone plays on: Resources/Rago/photon_region.txt if present (e.g. "eu", "asia"), else "us".</summary>
        static string Region()
        {
            var t = Resources.Load<TextAsset>("Rago/photon_region");
            var r = t != null ? t.text.Trim().ToLowerInvariant() : "";
            return string.IsNullOrEmpty(r) ? "us" : r;
        }

        static string AppId()
        {
            var t = Resources.Load<TextAsset>("Rago/photon_appid");
            return t != null ? t.text.Trim() : "";
        }

        public void Init(Action<bool> done)
        {
            _ready = !string.IsNullOrEmpty(AppId());
            _status = _ready ? "Online (Photon)" : "Offline - paste your Photon App ID in Rago Extract > Multiplayer";
            done?.Invoke(_ready);
        }

        // ------------------------------------------------------------------------------------ app id
        // Fusion's app-settings type has moved between versions (Fusion.Realtime vs a separate Photon.Realtime
        // assembly), so it's reached by name here: this compiles against every Fusion 2 version.
        static void UseAppId(ref StartGameArgs args, string appId)
        {
            var copy = MakeAppSettings(appId);
            if (copy == null) return;
            object boxed = args;
            if (Set(boxed, "CustomPhotonAppSettings", copy)) args = (StartGameArgs)boxed;
        }

        /// <summary>A copy of the project's Photon app settings with our App ID in it (null = use the project's own).</summary>
        static object MakeAppSettings(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return null;   // fall back to whatever is in PhotonAppSettings
            try
            {
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("Fusion.Photon.Realtime.PhotonAppSettings", false); if (t != null) break; }
                if (t == null) { Debug.LogWarning("[Rago] PhotonAppSettings not found; using the project's Photon settings"); return null; }
                var global = Get(null, t, "Global");
                var settings = Get(global, t, "AppSettings");
                if (settings == null) return null;
                var copy = settings.GetType().GetMethod("GetCopy", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)?.Invoke(settings, null) ?? settings;
                if (!Set(copy, "AppIdFusion", appId)) { Debug.LogWarning("[Rago] couldn't set AppIdFusion; using the project's Photon settings"); return null; }
                // everyone on ONE region: with "best region" two friends in different areas land on different servers
                // and can never see each other's parties, invites or raids
                Set(copy, "FixedRegion", Region());
                return copy;
            }
            catch (Exception e) { Debug.LogWarning("[Rago] couldn't apply the Photon App ID (using the project's Photon settings): " + e.Message); return null; }
        }

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // ---- who's who without any messages: every player joins with a Photon user id of "CALLSIGN#0000@party~1234",
        // which every other player in the session can read straight away. Names and parties sent as messages could get
        // lost right after joining - the party list said "joining..." forever and parties were split up in raids.
        static void UseUserId(ref StartGameArgs args, string uid)
        {
            try
            {
                var f = typeof(StartGameArgs).GetField("AuthValues", Any); var pr = f == null ? typeof(StartGameArgs).GetProperty("AuthValues", Any) : null;
                var t = f != null ? f.FieldType : pr != null ? pr.PropertyType : null;
                if (t == null) return;
                object av = null;
                try { av = Activator.CreateInstance(t, uid); } catch { }
                if (av == null) { av = Activator.CreateInstance(t); Set(av, "UserId", uid); }
                object boxed = args;
                if (Set(boxed, "AuthValues", av)) args = (StartGameArgs)boxed;
            }
            catch (Exception e) { Debug.LogWarning("[Rago] couldn't set the player id: " + e.Message); }
        }

        static MethodInfo _getUid, _setMaster;
        string UserIdOf(PlayerRef p)
        {
            if (_runner == null) return null;
            try
            {
                if (_getUid == null) _getUid = typeof(NetworkRunner).GetMethod("GetPlayerUserId", Any, null, new[] { typeof(PlayerRef) }, null);
                return _getUid?.Invoke(_runner, new object[] { p }) as string;
            }
            catch { return null; }
        }

        /// <summary>Read a player's callsign and party from their user id.</summary>
        string _myUid; bool _uidBad;
        void LearnFromUserId(ulong id, PlayerRef p)
        {
            if (_uidBad) return;
            var uid = UserIdOf(p);
            if (string.IsNullOrEmpty(uid)) return;
            // some Photon versions hand back YOUR id for every player: then everyone got your name and your party
            // (same names, parties spawning apart). If that happens, stop trusting ids and use the name messages.
            if (uid == _myUid || (!string.IsNullOrEmpty(_myUid) && uid.StartsWith(MyName() + "@")))
            {
                _uidBad = true;
                foreach (var k in new List<ulong>(_names.Keys)) if (Key(_names[k]) == Key(MyName())) _names.Remove(k);
                foreach (var k in new List<ulong>(_peerParty.Keys)) _peerParty.Remove(k);
                _nameAsk.Clear();   // ask everyone again
                return;
            }
            int at = uid.IndexOf('@'), tl = uid.LastIndexOf('~');
            string name = at >= 0 ? uid.Substring(0, at) : tl >= 0 ? uid.Substring(0, tl) : uid;
            string party = at >= 0 ? uid.Substring(at + 1, (tl > at ? tl : uid.Length) - at - 1) : "";
            bool changed = false;
            if (!_names.ContainsKey(id) && name.Contains("#"))
            {
                _names[id] = name; KnownPlayers.Add(name); changed = true;
                OnMetPlayer?.Invoke(name);
            }
            if (!string.IsNullOrEmpty(party) && !_peerParty.ContainsKey(id)) _peerParty[id] = party;
            if (changed) { PublishNames(); OnPartyChanged?.Invoke(); }
        }

        /// <summary>Shared mode: hand the session's "host" role to another player (the party leader).</summary>
        void MakeMaster(PlayerRef p)
        {
            try
            {
                if (_setMaster == null) _setMaster = typeof(NetworkRunner).GetMethod("SetMasterClient", Any, null, new[] { typeof(PlayerRef) }, null);
                _setMaster?.Invoke(_runner, new object[] { p });
            }
            catch (Exception e) { Debug.Log("[Rago] couldn't hand over the party: " + e.Message); }
        }

        static int Fnv(string s) { unchecked { int h = (int)2166136261; foreach (char c in s ?? "") h = (h ^ c) * 16777619; return h & 0x7fffffff; } }

        /// <summary>
        /// Everyone's spawn worked out the same way on every machine from the players in the session: each party shares
        /// one spot (members side by side, in callsign order), solo players get their own. So even if the host's
        /// "go" message never reaches you, you still land next to your party.
        /// </summary>
        void SpawnFor(string myName, out int spot, out int slot)
        {
            var groups = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            void Add(string name, string party)
            {
                string g = !string.IsNullOrEmpty(party) ? "p:" + Key(party) : "s:" + Key(name);
                if (!groups.TryGetValue(g, out var l)) groups[g] = l = new List<string>();
                if (!l.Contains(Key(name))) l.Add(Key(name));
            }
            Add(myName, _partyCode);
            foreach (var p in _peers) if (_names.TryGetValue(p, out var n)) Add(n, _peerParty.TryGetValue(p, out var pc) ? pc : null);
            var used = new HashSet<int>();
            spot = 0; slot = 0;
            foreach (var kv in groups)
            {
                int s0 = Fnv(kv.Key) % 20, s1 = s0;   // 20 spawn spots on the big maps
                for (int i = 0; i < 20 && used.Contains(s1); i++) s1 = (s1 + 1) % 20;
                used.Add(s1);
                kv.Value.Sort(StringComparer.Ordinal);
                int k = kv.Value.IndexOf(Key(myName));
                if (k >= 0) { spot = s1; slot = k; }
            }
        }

        static object Get(object o, Type t, string name)
        {
            var p = t.GetProperty(name, Any); if (p != null) return p.GetValue(o);
            var f = t.GetField(name, Any); return f?.GetValue(o);
        }

        static bool _timeoutDone;
        /// <summary>Fusion drops a player it hasn't heard from for ~10 s. Building a big raid map on a Quest can freeze
        /// the game for that long, so give it 40 s (if this Fusion version lets us).</summary>
        static void LongerTimeout()
        {
            if (_timeoutDone) return;
            _timeoutDone = true;
            try
            {
                var cfgType = Type.GetType("Fusion.NetworkProjectConfig, Fusion.Runtime");
                var global = cfgType?.GetProperty("Global", Any)?.GetValue(null);
                if (global == null) { Debug.Log("[Rago] no NetworkProjectConfig - default connection timeout"); return; }
                var nf = cfgType.GetField("Network", Any); var np = nf == null ? cfgType.GetProperty("Network", Any) : null;
                object net = nf != null ? nf.GetValue(global) : np?.GetValue(global);
                if (net == null) { Debug.Log("[Rago] no network config - default connection timeout"); return; }
                bool any = false;
                foreach (var name in new[] { "ConnectionTimeout", "ConnectionTimeoutSeconds" })
                {
                    var f = net.GetType().GetField(name, Any);
                    if (f != null) { f.SetValue(net, Convert.ChangeType(40, f.FieldType)); any = true; continue; }
                    var p = net.GetType().GetProperty(name, Any);
                    if (p != null && p.CanWrite) { p.SetValue(net, Convert.ChangeType(40, p.PropertyType)); any = true; }
                }
                if (net.GetType().IsValueType) { if (nf != null) nf.SetValue(global, net); else np?.SetValue(global, net); }
                Debug.Log(any ? "[Rago] connection timeout set to 40 s" : "[Rago] this Fusion has no ConnectionTimeout setting");
            }
            catch (Exception e) { Debug.Log("[Rago] couldn't change the connection timeout: " + e.Message); }
        }

        static bool Set(object o, string name, object value)
        {
            if (o == null) return false;
            var t = o.GetType();
            var f = t.GetField(name, Any);
            if (f != null && f.FieldType.IsInstanceOfType(value)) { f.SetValue(o, value); return true; }
            var p = t.GetProperty(name, Any);
            if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(value)) { p.SetValue(o, value); return true; }
            return false;
        }

        // ------------------------------------------------------------------------------------ sessions
        async Task<bool> StartSession(string name, Dictionary<string, SessionProperty> props, int maxPlayers, bool allowCreate = true)
        {
            await Shutdown();
            // a party lobby keeps the invite listener running (invites to you only arrive through it); a raid or the
            // Market doesn't need it
            bool party = props != null && props.TryGetValue("kind", out var kk) && Str(kk) == "party";
            if (!party) await LeaveLobby();
            _inPartySession = false;
            _names.Clear(); _peerParty.Clear(); _nameAsk.Clear(); _acks.Clear(); _out.Clear(); _rtt = 0f; _pingOut = -1f;
            _lastHeard.Clear(); _peerSince.Clear(); _relayTo.Clear(); _hostId = 0; _idOf.Clear();
            _sessionKind = props != null && props.TryGetValue("kind", out var kp) ? Str(kp) : "";
            var go = new GameObject("FusionRunner");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<NetworkRunner>();
            _runner.ProvideInput = false;
            _runner.AddCallbacks(this);
            var smType = Type.GetType("Fusion.NetworkSceneManagerDefault, Fusion.Unity");
            var sceneManager = smType != null ? go.AddComponent(smType) as INetworkSceneManager : null;
            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = name,
                PlayerCount = maxPlayers,
                SessionProperties = props,
                SceneManager = sceneManager,
            };
            UseAppId(ref args, AppId());
            LongerTimeout();
            _myUid = MyName() + "@" + (_partyCode ?? "") + "~" + UnityEngine.Random.Range(1000, 9999);
            _uidBad = false;
            UseUserId(ref args, _myUid);
            if (!allowCreate) { object boxed = args; if (Set(boxed, "EnableClientSessionCreation", false)) args = (StartGameArgs)boxed; }   // joining a party: never create an empty one
            var res = await _runner.StartGame(args);
            if (!res.Ok)
            {
                _status = "Photon: " + res.ShutdownReason;
                Debug.LogWarning("[Rago] Photon StartGame failed: " + res.ShutdownReason + " " + res.ErrorMessage);
                await Shutdown();
                return false;
            }
            _leaving = false;
            _sessionId = _runner.SessionInfo.Name;
            RefreshPeers();
            return true;
        }

        void RefreshPeers()
        {
            _peers.Clear(); _refs.Clear();
            if (_runner == null) return;
            foreach (var p in _runner.ActivePlayers) NotePeer(p);
        }

        async Task Shutdown()
        {
            if (_runner == null) return;
            _leaving = true;
            var r = _runner;
            _runner = null;
            try { await r.Shutdown(); } catch { }
            if (r != null) Destroy(r.gameObject);
            _peers.Clear(); _refs.Clear();
            _leaving = false;
        }

        public async void JoinMarket(Action<bool> done)
        {
            if (!_ready) { done?.Invoke(false); return; }
            _status = "Joining the Market...";
            bool ok = await StartSession("RagoMarket", new Dictionary<string, SessionProperty> { { "kind", "market" } }, 20);
            if (ok) foreach (var p in _peers) SendSys(p, 3, MyName());
            _status = ok ? "Market (" + (_peers.Count + 1) + " players)" : _status;
            done?.Invoke(ok);
        }

        public async void QueueRaid(string map, Action<QueueStatus> status, Action<RaidStart> start, Action<string> failed)
        {
            if (!_ready) { failed?.Invoke("offline"); return; }
            if (_queueing) return;
            _queueing = true; _cancel = false; _pendingStart = null; _raidMap = map;
            float pressedAt = Time.realtimeSinceStartup;   // the 1:00 launch counts from when you pressed QUEUE, like your HUD timer
            try
            {
                status?.Invoke(new QueueStatus { players = 1, max = QueueRules.MaxFor(map), text = "Finding a raid..." });
                string sessionName = null; _queuePartySize = 1;
                bool night = RaidChoice.Night;   // night raids are matched with other night players only
                if (_pendingRaidSession != null)
                {
                    // following the party leader into their raid session (at their time of day)
                    sessionName = _pendingRaidSession; _queuePartySize = _pendingPartySize; _pendingRaidSession = null;
                    night = _pendingNight; RaidChoice.Night = night;
                }
                else if (_partyCode != null && _inPartySession)
                {
                    if (!PartyLeader) { failed?.Invoke("Only the party leader can start the queue"); _queueing = false; return; }
                    _wasLeader = true;
                    _queuePartySize = _peers.Count + 1;
                    sessionName = ChooseSlot(map, night, _queuePartySize, 1);   // the same public queue as everyone else
                    // tell every member, and keep telling them until each one answers (up to 5 s): leaving the party session
                    // before a member heard it left them behind on their own - it looked like they'd been kicked
                    _acks.Clear();
                    var members = new List<ulong>(_peers);
                    string go6 = map + "|" + sessionName + "|" + _queuePartySize + "|" + (night ? 1 : 0);
                    if (IsHost)
                        try
                        {
                            var pp = new Dictionary<string, SessionProperty>();
                            var cur = _runner.SessionInfo.Properties;
                            if (cur != null) foreach (var kv in cur) pp[kv.Key] = kv.Value;
                            pp["raid"] = go6 + "|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            _runner.SessionInfo.UpdateCustomProperties(pp);
                        }
                        catch (Exception e) { Debug.Log("[Rago] couldn't post the raid to the party: " + e.Message); }
                    float until = Time.realtimeSinceStartup + 5f;
                    while (Time.realtimeSinceStartup < until && InSession)
                    {
                        bool all = true;
                        foreach (var p in members) if (!_acks.Contains(p) && _peers.Contains(p)) { all = false; SendSys(p, 6, go6); }
                        if (all) break;
                        await Task.Delay(500);
                    }
                    await Task.Delay(300);   // let the last messages go out before leaving the party session
                }
                _queueNight = night;
                // Everyone queueing for this map (and time of day) goes into the same numbered queue sessions -
                // rago_q_<map>[_n]_1, _2, ... - the first one that hasn't launched and has room. Parties used to make
                // their own session and solo players a random one, so a party of 2 and a 3rd player (or two solo
                // players) each launched alone. A party follows its leader's pick; if that one launched meanwhile,
                // everyone in the party steps to the next number by the same rule, so they stay together.
                bool ok = false;
                int startSlot = sessionName != null ? SlotOf(sessionName) : 1;
                if (sessionName == null) sessionName = ChooseSlot(map, night, _queuePartySize, startSlot);
                for (int attempt = 0; attempt < 12 && !_cancel; attempt++)
                {
                    ok = await StartSession(sessionName, new Dictionary<string, SessionProperty> { { "kind", "raid_" + map + (night ? "_n" : "") } }, QueueRules.MaxFor(map));
                    var jp = ok ? _runner.SessionInfo.Properties : null;
                    bool launched = jp != null && (jp.ContainsKey("go") || (jp.TryGetValue("kind", out var jk) && Str(jk).StartsWith("raid_started")));
                    bool full = ok && _peers.Count + _queuePartySize > QueueRules.MaxFor(map) && _queuePartySize > 1 && _partyCode != null && _wasLeader;
                    if (ok && !launched && !full) break;
                    if (ok) { Debug.Log("[Rago] " + sessionName + (launched ? " already launched" : " too full for the party") + " - next one"); await Shutdown(); ok = false; }
                    else Debug.Log("[Rago] couldn't get into " + sessionName + " - next one");
                    sessionName = SlotName(map, night, SlotOf(sessionName) + 1);
                }
                Debug.Log("[Rago] queueing in " + sessionName + " with " + _peers.Count + " others");
                if (ok) foreach (var p in _peers) { SendSys(p, 3, MyName()); if (_partyCode != null) SendSys(p, 4, _partyCode); }
                if (!ok) { failed?.Invoke(_status); _queueing = false; return; }
                float atTarget = -1f;
                float queuedAt = Time.realtimeSinceStartup;
                while (!_cancel && InSession)
                {
                    var props = _runner.SessionInfo.Properties;
                    // the host said go (message) or closed the session with a "go" on it (property - can't get lost)
                    if (_pendingStart == null && !IsHost && props != null && props.ContainsKey("go") && props.TryGetValue("seed", out var gs))
                        _pendingStart = new RaidStart { map = map, seed = (int)gs, spawnIndex = -1, players = _peers.Count + 1, online = true, night = night };
                    if (_pendingStart != null)
                    {
                        var rs = _pendingStart; _pendingStart = null; _queueing = false;
                        _lastRaidSession = _sessionId; _lastRaidMap = map; _lastRaidNight = night; _lastRaidAt = Time.realtimeSinceStartup;
                        if (rs.spawnIndex < 0) { SpawnFor(MyName(), out int sp0, out int sl0); rs.spawnIndex = sp0; rs.partySlot = sl0; }
                        start?.Invoke(rs);
                        return;
                    }
                    if (IsHost && (props == null || !props.ContainsKey("t0")))
                        try { _runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty> { { "kind", "raid_" + map + (night ? "_n" : "") }, { "t0", (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000) }, { "seed", UnityEngine.Random.Range(1, 999999) } }); }
                        catch (Exception e) { Debug.Log("[Rago] couldn't set the queue start time: " + e.Message); }
                    int t0 = props != null && props.TryGetValue("t0", out var tp) ? (int)tp : (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000);
                    float elapsed = (float)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000 - t0);
                    // the session's start time may never sync (or the host changed): never wait longer than our own timer says
                    elapsed = Mathf.Max(elapsed, Time.realtimeSinceStartup - queuedAt, Time.realtimeSinceStartup - pressedAt);
                    int players = _peers.Count + 1;
                    if (players >= QueueRules.TargetFor(map)) { if (atTarget < 0) atTarget = Time.realtimeSinceStartup; } else atTarget = -1;
                    status?.Invoke(new QueueStatus { players = players, max = QueueRules.MaxFor(map), target = QueueRules.TargetFor(map), elapsed = elapsed, maxWait = QueueRules.MaxWait, text = QueueRules.Describe(players, elapsed, map) });
                    if (IsHost && QueueRules.ShouldStart(players, elapsed, atTarget < 0 ? 0 : Time.realtimeSinceStartup - atTarget, _queuePartySize, map))
                    {
                        int seed = props != null && props.TryGetValue("seed", out var sp) ? (int)sp : UnityEngine.Random.Range(1, 999999);
                        try
                        {
                            var gp = new Dictionary<string, SessionProperty>();
                            if (props != null) foreach (var kv in props) gp[kv.Key] = kv.Value;
                            gp["seed"] = seed; gp["go"] = players;
                            // started: the matchmaking tag changes so nobody new joins by chance, but it stays open so a
                            // player who drops out can reconnect to it by name
                            gp["kind"] = "raid_started_" + map;
                            gp["map"] = map; gp["night"] = night ? 1 : 0;
                            _runner.SessionInfo.UpdateCustomProperties(gp);
                        }
                        catch (Exception e) { Debug.Log("[Rago] couldn't close the raid session: " + e.Message); }
                        // each party shares one spawn spot (members stand side by side); everyone else gets their own
                        foreach (var p in _peers) SendSys(p, 1, map + "|" + seed + "|-1|" + players + "|0");   // everyone works out their own spot
                        SpawnFor(MyName(), out int mySpot, out int mySlot);
                        _queueing = false;
                        _status = "Raid (" + players + " players)";
                        _lastRaidSession = _sessionId; _lastRaidMap = map; _lastRaidNight = night; _lastRaidAt = Time.realtimeSinceStartup;
                        start?.Invoke(new RaidStart { map = map, seed = seed, spawnIndex = mySpot, players = players, online = true, partySlot = mySlot, night = night });
                        return;
                    }
                    // not the host and the host's go never came (left, lagging, message lost): don't sit past 1:00 - go in
                    // anyway with this session's seed, so we still land in the same raid as everyone else in it
                    if (!IsHost && elapsed >= QueueRules.MaxWait + 4f)
                    {
                        int seed = props != null && props.TryGetValue("seed", out var sp2) ? (int)sp2 : (_runner.SessionInfo.Name ?? map).GetHashCode() & 0x7fffff;
                        _queueing = false;
                        _status = "Raid (" + players + " players)";
                        SpawnFor(MyName(), out int sp1, out int sl1);
                        _lastRaidSession = _sessionId; _lastRaidMap = map; _lastRaidNight = night; _lastRaidAt = Time.realtimeSinceStartup;
                        start?.Invoke(new RaidStart { map = map, seed = seed, spawnIndex = sp1, players = players, online = true, partySlot = sl1, night = night });
                        return;
                    }
                    await Task.Delay(500);
                }
                if (_cancel) { await Shutdown(); _queueing = false; RejoinParty(); return; }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rago] queue failed: " + e);
                failed?.Invoke(e.Message);
                await Shutdown();
            }
            _queueing = false;
            RejoinParty();
        }

        static string SlotName(string map, bool night, int slot) => "rago_q_" + map + (night ? "_n" : "") + "_" + slot;
        static int SlotOf(string name) { int u = name != null ? name.LastIndexOf('_') : -1; return u >= 0 && int.TryParse(name.Substring(u + 1), out int n) ? n : 1; }

        /// <summary>The first queue session (from this number on) that, as far as the lobby list shows, hasn't launched
        /// and has room for this many players. With no list: just this number (joining checks it anyway).</summary>
        string ChooseSlot(string map, bool night, int partySize, int from)
        {
            bool fresh = Time.realtimeSinceStartup - _listedAt < 20f;
            for (int i = from; i < from + 40; i++)
            {
                string n = SlotName(map, night, i);
                if (!fresh || !_listed.TryGetValue(n, out var st)) return n;   // not listed: empty (it'll be made) or unknown
                if (!st.started && st.players + partySize <= QueueRules.MaxFor(map)) return n;
            }
            return SlotName(map, night, from);
        }

        public void CancelQueue()
        {
            // the leader cancelling takes the whole party out of the queue
            if (_partyCode != null && _wasLeader && InSession)
                foreach (var p in _peers) if (_peerParty.TryGetValue(p, out var pc) && pc == _partyCode) SendSys(p, 5, _partyCode);
            _cancel = true;
        }
        public async void Leave() { await Shutdown(); _status = _ready ? "Online (Photon)" : _status; }

        // ------------------------------------------------------------------------------------ data
        static readonly ReliableKey GameKey = ReliableKey.FromInts(7, 1, 0, 0);

        // Game messages are queued and sent as ONE packet per player per frame (channel 2: [len u16][msg]...).
        // Every reliable send has its own overhead and Photon only moves so many a second - lots of small ones
        // (poses, raider updates, shots, voice) backed up in raids and everything arrived seconds late.
        readonly Dictionary<ulong, System.IO.MemoryStream> _out = new Dictionary<ulong, System.IO.MemoryStream>();
        float _rtt;
        float _pingOut = -1f;   // when the oldest still-unanswered ping went out
        /// <summary>Round trip to the other players, seconds. Grows while pings go unanswered (a backed-up line).</summary>
        public float Rtt => !InSession || _peers.Count == 0 ? 0f : Mathf.Max(_rtt, _pingOut >= 0f ? Time.realtimeSinceStartup - _pingOut : 0f);

        public void Send(byte[] data, bool reliable, ulong target = ulong.MaxValue)
        {
            if (!InSession || data == null || data.Length == 0 || data.Length > 60000) return;
            if (target == ulong.MaxValue)
            {
                // Photon's shared mode only reliably carries this data between its master client and each player -
                // straight between two other players it went missing (the "can't see the 3rd player" bug). So: one
                // copy to the master client, which hands it to everyone. It's only a postman - it runs nothing for
                // anyone, and when it leaves Photon picks another one at once.
                if (!IsHost && _hostId != 0 && _refs.ContainsKey(_hostId)) Queue(Bcast, data);
                else foreach (var p in _peers) Queue(p, data);
            }
            else if (target != LocalId) Queue(target, data);
        }

        void Queue(ulong id, byte[] data)
        {
            if (!_out.TryGetValue(id, out var ms)) _out[id] = ms = new System.IO.MemoryStream(512);
            if (ms.Length == 0) ms.WriteByte(2);
            ms.WriteByte((byte)(data.Length & 0xff)); ms.WriteByte((byte)(data.Length >> 8));
            ms.Write(data, 0, data.Length);
            if (ms.Length > 16000) Flush(id, ms);
        }

        const ulong Bcast = ulong.MaxValue - 1;
        void Flush(ulong id, System.IO.MemoryStream ms)
        {
            if (ms.Length == 0) return;
            if (id == Bcast)
            {
                var data = ms.ToArray();
                if (!IsHost && _hostId != 0 && _refs.TryGetValue(_hostId, out var hp))
                {
                    var w = new byte[data.Length + 1]; w[0] = 5; Buffer.BlockCopy(data, 0, w, 1, data.Length);
                    Raw(hp, w);
                }
                else foreach (var p in _peers) SendTo(p, data);   // we became the host meanwhile
            }
            else if (_peers.Contains(id)) SendTo(id, ms.ToArray());
            ms.SetLength(0);
        }

        // Sent at a steady 15 packets a second per player (10 when the line is backing up) instead of every frame: at
        // 72-90 fps with 3+ players that was hundreds of separate reliable sends a second, the line backed up and
        // everyone lagged and teleported. 15 a second matches the pose rate, so nothing goes out later than it did.
        float _flushT;
        void LateUpdate()
        {
            if (_out.Count == 0) return;
            if (!InSession) { _out.Clear(); return; }
            _flushT -= Time.unscaledDeltaTime;
            if (_flushT > 0f) return;
            _flushT = Rtt > 0.3f ? 0.1f : 1f / 15f;
            foreach (var kv in _out) Flush(kv.Key, kv.Value);
        }

        void SendSys(ulong to, byte code, string text)
        {
            if (code == 3 && Spectator.Active) return;   // spectating: don't introduce yourself (you stay out of their lists)
            var t = System.Text.Encoding.UTF8.GetBytes(text);
            var packet = new byte[t.Length + 2];
            packet[0] = 1; packet[1] = code;
            Buffer.BlockCopy(t, 0, packet, 2, t.Length);
            SendTo(to, packet);
        }

        // Every packet starts with WHO sent it ([0xA5][sender id]). Photon's own "this came from player X" can't be
        // trusted in shared mode: with 3+ players it labelled several players' data as the same one - everyone saw a
        // single frozen body with one name that kept changing. Now each packet says who it's from.
        const byte Stamp = 0xA5;
        void Raw(PlayerRef to, byte[] packet)
        {
            if (_runner == null) return;
            var w = new byte[packet.Length + 9];
            w[0] = Stamp; Buffer.BlockCopy(BitConverter.GetBytes(LocalId), 0, w, 1, 8);
            Buffer.BlockCopy(packet, 0, w, 9, packet.Length);
            try { _runner.SendReliableDataToPlayer(to, GameKey, w); }
            catch (Exception e) { if (Time.realtimeSinceStartup - _lastSendWarn > 10f) { _lastSendWarn = Time.realtimeSinceStartup; Debug.Log("[Rago] send skipped: " + e.Message); } }
        }
        bool _mislabelLogged;

        void SendTo(ulong id, byte[] packet)
        {
            if (_runner == null || !_refs.TryGetValue(id, out var p)) return;
            // to one other player: through the master client too (see Send)
            if (!IsHost && _hostId != 0 && _hostId != id && _refs.TryGetValue(_hostId, out var hp))
            {
                var w = new byte[packet.Length + 9];
                w[0] = 3; Buffer.BlockCopy(BitConverter.GetBytes(id), 0, w, 1, 8);
                Buffer.BlockCopy(packet, 0, w, 9, packet.Length);
                Raw(hp, w);
                return;
            }
            Raw(p, packet);
        }

        readonly Dictionary<ulong, float> _lastHeard = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, float> _peerSince = new Dictionary<ulong, float>();
        readonly HashSet<ulong> _relayTo = new HashSet<ulong>();

        /// <summary>Every player pings every second: a player we haven't heard from directly for 4 s gets relayed.</summary>
        void CheckLinks()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var p in _peers)
            {
                if (!_peerSince.ContainsKey(p)) _peerSince[p] = now;
                if (p == _hostId || IsHost) continue;
                float heard = _lastHeard.TryGetValue(p, out var h) ? h : _peerSince[p];
                if (now - heard > 4f && _relayTo.Add(p)) Debug.Log("[Rago] no direct link to player " + p + " - relaying through the host");
            }
        }

        // Fusion 2.0.x (current) passes the bytes as ReadOnlySpan<byte>; older 2.0 builds used ArraySegment<byte>.
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> span)
        {
            var arr = span.ToArray();
            HandleReliable(player, new ArraySegment<byte>(arr));
        }

        void HandleReliable(PlayerRef player, ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count < 1) return;
            NotePeer(player);   // they can reach us, so we can reach them: never ignore a player we hear from
            ulong direct = Id(player);
            if (data.Count >= 10 && data.Array[data.Offset] == Stamp)
            {
                ulong stamped = BitConverter.ToUInt64(data.Array, data.Offset + 1);
                if (stamped != direct && !_mislabelLogged) { _mislabelLogged = true; Debug.Log("[Rago] Photon said a packet came from " + direct + " but it's from " + stamped + " - using the packet's own label"); }
                direct = stamped;
                data = new ArraySegment<byte>(data.Array, data.Offset + 9, data.Count - 9);
                if (direct == LocalId) return;   // our own packet bounced back
                if (!_peers.Contains(direct)) foreach (var ap in _runner.ActivePlayers) if (Id(ap) == direct) NotePeer(ap);
            }
            _lastHeard[direct] = Time.realtimeSinceStartup;
            if (_relayTo.Remove(direct)) Debug.Log("[Rago] direct link to player " + direct + " works again");
            byte ch = data.Array[data.Offset];
            if (ch == 5)
            {
                // a player's broadcast sent to us as the hub: use it ourselves and hand it to every other player
                // (even if we're not the master any more - the sender picked us, so nobody else will pass it on)
                var inner = new ArraySegment<byte>(data.Array, data.Offset + 1, data.Count - 1);
                {
                    var fwd = new byte[inner.Count + 9];
                    fwd[0] = 4; Buffer.BlockCopy(BitConverter.GetBytes(direct), 0, fwd, 1, 8);
                    Buffer.BlockCopy(inner.Array, inner.Offset, fwd, 9, inner.Count);
                    foreach (var p in _peers) if (p != direct && _refs.TryGetValue(p, out var pr)) Raw(pr, fwd);
                }
                Process(direct, inner);
                return;
            }
            if (ch == 3)
            {
                // host: a player who can't reach another one directly - pass it on, marked with who it's from
                if (data.Count < 9) return;
                ulong dest = BitConverter.ToUInt64(data.Array, data.Offset + 1);
                var inner = new ArraySegment<byte>(data.Array, data.Offset + 9, data.Count - 9);
                if (dest == LocalId) { Process(direct, inner); return; }
                var fwd = new byte[inner.Count + 9];
                fwd[0] = 4; Buffer.BlockCopy(BitConverter.GetBytes(direct), 0, fwd, 1, 8);
                Buffer.BlockCopy(inner.Array, inner.Offset, fwd, 9, inner.Count);
                if (_refs.TryGetValue(dest, out var dp)) Raw(dp, fwd);
                return;
            }
            if (ch == 4)
            {
                // relayed by the host from another player
                if (data.Count < 9) return;
                ulong src = BitConverter.ToUInt64(data.Array, data.Offset + 1);
                _lastHeard[src] = Time.realtimeSinceStartup;
                // they had to send it the long way round: our direct packets probably aren't reaching them either
                if (_relayTo.Add(src)) Debug.Log("[Rago] player " + src + " is relaying to us - relaying back");
                Process(src, new ArraySegment<byte>(data.Array, data.Offset + 9, data.Count - 9));
                return;
            }
            Process(direct, data);
        }

        void Process(ulong fromId, ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count < 1) return;
            byte channel = data.Array[data.Offset];
            if (channel == 1)
            {
                if (data.Count < 2) return;
                byte code = data.Array[data.Offset + 1];
                string text = System.Text.Encoding.UTF8.GetString(data.Array, data.Offset + 2, data.Count - 2);
                if (code == 2) { _hostId = fromId; return; }
                if (code == 13) { if (IsHost) SendSys(fromId, 2, "host"); return; }   // "who's the host?" 
                if (code == 1)
                {
                    var p = text.Split('|');
                    if (p.Length >= 4) _pendingStart = new RaidStart { map = p[0], seed = int.Parse(p[1]), spawnIndex = int.Parse(p[2]), players = int.Parse(p[3]), online = true, partySlot = p.Length >= 5 ? int.Parse(p[4]) : 0, night = _queueNight };
                }
                else if (code == 3)
                {
                    // someone's callsign (sent to everyone in a session)
                    _names[fromId] = text;
                    KnownPlayers.Add(text);
                    PublishNames();
                    OnMetPlayer?.Invoke(text);   // raid, Market or party: a real player you've met (RECENT, and you can add them as a friend)
                    OnPartyChanged?.Invoke();
                }
                else if (code == 7 && _inPartySession && IsHost) { Remember(text); PublishInvites(); }      // a member asks the leader to invite someone
                else if (code == 8)
                {
                    // invited in a raid: "from|party"
                    var p = text.Split('|');
                    if (p.Length >= 2 && !_invites.Exists(x => Key(x.party) == Key(p[1])) && Key(p[1]) != Key(_partyCode))
                    {
                        _invites.Add(new PartyInvite { from = p[0], party = p[1], time = Time.realtimeSinceStartup });
                        RagoGame.I?.Player?.Toast(p[0] + " invited you to their party - press the menu button to accept");
                        OnPartyChanged?.Invoke();
                    }
                }
                else if (code == 4) { _peerParty[fromId] = text; }                                         // which party a raid peer is in
                else if (code == 5 && _queueing && text == _partyCode) { _cancel = true; OnPartyQueueCancelled?.Invoke(); }   // leader cancelled
                else if (code == 6 && _inPartySession && !_queueing)
                {
                    // the leader queued: follow them into their raid session
                    var p = text.Split('|');
                    SendSys(fromId, 10, "ok");
                    if (p.Length >= 3 && _pendingRaidSession != p[1] && _lastRaid != p[1]) { _lastRaid = p[1]; _pendingRaidSession = p[1]; _pendingPartySize = int.Parse(p[2]); _pendingNight = p.Length >= 4 && p[3] == "1"; _wasLeader = false; OnPartyQueue?.Invoke(p[0]); }
                }
                else if (code == 6) SendSys(fromId, 10, "ok");     // already on our way: still answer so the leader stops asking
                else if (code == 10) _acks.Add(fromId);
                else if (code == 9) { SendSys(fromId, 3, MyName()); if (_partyCode != null && !_inPartySession) SendSys(fromId, 4, _partyCode); }   // "who are you?"
                else if (code == 11) SendSys(fromId, 12, text);    // ping: echo it back
                else if (code == 12 && float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sentAt))
                {
                    float rtt = Time.realtimeSinceStartup - sentAt;
                    _rtt = rtt > _rtt ? rtt : Mathf.Lerp(_rtt, rtt, 0.3f);   // rises at once, settles slowly
                    _pingOut = -1f;
                }
                return;
            }
            if (channel == 2)
            {
                int i = data.Offset + 1, end = data.Offset + data.Count;
                while (i + 2 <= end)
                {
                    int len = data.Array[i] | (data.Array[i + 1] << 8); i += 2;
                    if (len <= 0 || i + len > end) break;
                    var msg = new byte[len];
                    Buffer.BlockCopy(data.Array, i, msg, 0, len); i += len;
                    try { OnData?.Invoke(fromId, msg); } catch (Exception e) { Debug.LogWarning("[Rago] packet handler: " + e.Message); }
                }
                return;
            }
            var payload = new byte[data.Count - 1];
            Buffer.BlockCopy(data.Array, data.Offset + 1, payload, 0, payload.Length);
            OnData?.Invoke(fromId, payload);
        }

        // ------------------------------------------------------------------------------------ callbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner != _runner) return;
            NotePeer(player);
            foreach (var p in runner.ActivePlayers) NotePeer(p);
            if (_inPartySession) OnPartyChanged?.Invoke();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner != _runner) return;
            _peers.Remove(Id(player)); _refs.Remove(Id(player));
            _names.Remove(Id(player));
            // the host left: someone else is host now - find out who straight away (relaying and the raiders depend on it)
            if (Id(player) == _hostId)
            {
                _hostId = 0;
                if (IsHost) { _hostId = LocalId; foreach (var p in _peers) SendSys(p, 2, "host"); }
                else foreach (var p in _peers) SendSys(p, 13, "?");
            }
            PublishNames();
            OnPeerLeft?.Invoke(Id(player));
            if (_inPartySession) OnPartyChanged?.Invoke();
            _idOf.Remove(player);   // the slot can be reused by someone else
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (_leaving || runner != _runner) return;
            _status = "Disconnected (" + shutdownReason + ")";
            Debug.LogWarning("[Rago] Photon session ended: " + shutdownReason + " (players here: " + _peers.Count + ", rtt " + _rtt.ToString("0.00") + ")");
            _peers.Clear();
            if (_inPartySession) { _inPartySession = false; OnPartyChanged?.Invoke(); }
            OnDisconnected?.Invoke();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (_leaving || runner != _runner) return;
            _status = "Disconnected (" + reason + ")";
            Debug.LogWarning("[Rago] disconnected from Photon: " + reason + " (rtt " + _rtt.ToString("0.00") + ")");
            OnDisconnected?.Invoke();
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { _status = "Connect failed: " + reason; }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }

        float _peerScan;

        /// <summary>
        /// Make sure a player in our session is on the peer list (and introduced) - whether we learned about them from the
        /// session's player list or from a message they sent. In Shared mode the players already in a session can reach
        /// a newcomer's client a moment after it joins, so the list taken at join time can be empty; nothing is ever sent
        /// to a player who isn't on it, which made other players invisible.
        /// </summary>
        void NotePeer(PlayerRef p)
        {
            if (_runner == null || p == _runner.LocalPlayer || p.PlayerId < 0) return;
            ulong id = Id(p);
            _refs[id] = p;
            LearnFromUserId(id, p);
            if (_peers.Contains(id)) return;
            _peers.Add(id);
            Debug.Log("[Rago] player joined: id " + id + " (fusion #" + p.PlayerId + ", uid " + (UserIdOf(p) ?? "?") + "), me = " + LocalId + (IsHost ? " (host)" : ", host = " + _hostId));
            if (IsHost) SendSys(id, 2, "host"); else if (_hostId == 0) SendSys(id, 13, "?");
            SendSys(id, 3, MyName());
            if (!_inPartySession && _queueing && _partyCode != null) SendSys(id, 4, _partyCode);
            OnPeerJoined?.Invoke(id);
            if (_inPartySession) OnPartyChanged?.Invoke();
        }

        void Update()
        {
            // keep the peer list in step with the session (players can show up after we joined)
            if (InSession)
            {
                _peerScan -= Time.unscaledDeltaTime;
                if (_peerScan <= 0f)
                {
                    _peerScan = 0.5f;
                    try { foreach (var p in _runner.ActivePlayers) NotePeer(p); } catch { }
                    // the party session's host should be the leader (whoever got back first opened it and became host)
                    if (_inPartySession && IsHost && _partyCode != null && Key(_partyCode) != Key(MyName()))
                        foreach (var kv in _names) if (Key(kv.Value) == Key(_partyCode) && _refs.TryGetValue(kv.Key, out var lr)) { MakeMaster(lr); break; }
                    // the leader queued: follow - read from the party session itself too, not only from a message
                    if (_inPartySession && !_queueing && _pendingRaidSession == null && _runner.SessionInfo.Properties != null
                        && _runner.SessionInfo.Properties.TryGetValue("raid", out var rv))
                    {
                        var rp = Str(rv).Split('|');
                        if (rp.Length >= 5 && rp[1] != _lastRaid && long.TryParse(rp[4], out var when) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - when < 25)
                        {
                            _lastRaid = rp[1]; _pendingRaidSession = rp[1]; int.TryParse(rp[2], out _pendingPartySize); _pendingNight = rp[3] == "1"; _wasLeader = false;
                            OnPartyQueue?.Invoke(rp[0]);
                        }
                    }
                    // a callsign (or party tag) sent the moment someone joined can be lost: keep introducing ourselves to,
                    // and asking, anyone we still don't have a name for - the party list said "joining..." forever
                    float now = Time.realtimeSinceStartup;
                    foreach (var p in _peers)
                    {
                        if (_names.ContainsKey(p) && (_partyCode == null || _inPartySession || _peerParty.ContainsKey(p))) continue;
                        if (_nameAsk.TryGetValue(p, out var t) && now - t < 1.5f) continue;
                        _nameAsk[p] = now;
                        SendSys(p, 3, MyName());
                        if (_partyCode != null && !_inPartySession) SendSys(p, 4, _partyCode);
                        SendSys(p, 9, "?");
                    }
                }
                _pingT -= Time.unscaledDeltaTime;
                if (_pingT <= 0f && _peers.Count > 0)
                {
                    _pingT = 2f;
                    string ts = Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    foreach (var p in _peers) SendSys(p, 11, ts);
                    CheckLinks();
                    if (_pingOut < 0f) _pingOut = Time.realtimeSinceStartup;
                }
            }
            // the shared-mode master client runs the NPCs; tell everyone who that is
            if (!InSession || !IsHost) return;
            _announce -= Time.unscaledDeltaTime;
            if (_announce > 0) return;
            _announce = 1f;
            _hostId = LocalId;
            foreach (var p in _peers) SendSys(p, 2, "host");
        }

        void OnApplicationQuit() { _ = Shutdown(); }
    }
}
#endif
