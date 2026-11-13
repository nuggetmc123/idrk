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

        static ulong Id(PlayerRef p) => (ulong)p.PlayerId;

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
        readonly List<string> _sessionPlayers = new List<string>();
        readonly List<PartyInvite> _invites = new List<PartyInvite>();
        readonly List<string> _invited = new List<string>();     // leader: callsigns invited to this party
        string _pendingRaidSession; int _pendingPartySize = 1; int _queuePartySize = 1;
        string _sessionKind = "";
        NetworkRunner _lobby;
        public string PartyCode => _partyCode;
        public bool PartyLeader => _partyCode != null && (_inPartySession ? IsHost : _wasLeader);
        public IReadOnlyList<PartyInvite> Invites { get { _invites.RemoveAll(i => Time.realtimeSinceStartup - i.time > 300f); return _invites; } }
        public IReadOnlyList<string> PartyMembers
        {
            get
            {
                _members.Clear();
                if (_partyCode == null) return _members;
                _members.Add(MyName() + (PartyLeader ? "  (leader)" : ""));
                if (_inPartySession) foreach (var p in _peers) _members.Add(_names.TryGetValue(p, out var n) ? n : "joining...");
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

        void PublishInvites()
        {
            if (!_inPartySession || !IsHost || _runner == null) return;
            try
            {
                _runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
                    { { "kind", "party" }, { "lead", MyName() }, { "inv", string.Join(",", _invited.ConvertAll(Key)) } });
            }
            catch (Exception e) { Debug.LogWarning("[Rago] couldn't publish party invites: " + e.Message); }
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
            _partyCode = null; _wasLeader = false; _invited.Clear();
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
            bool ok = await StartSession(PartySession(id), new Dictionary<string, SessionProperty> { { "kind", "party" }, { "lead", id }, { "inv", create ? string.Join(",", _invited.ConvertAll(Key)) : "" } }, QueueRules.Max, create);
            if (ok && !create && _peers.Count == 0) { ok = false; await Shutdown(); }   // nobody there
            if (!ok) { OnPartyChanged?.Invoke(); return false; }
            _partyCode = id; _inPartySession = true;
            _wasLeader = IsHost;
            _status = "Party (" + (_peers.Count + 1) + ")";
            foreach (var p in _peers) SendSys(p, 3, MyName());
            OnPartyChanged?.Invoke();
            return true;
        }

        // ---- the session lobby: lets you receive bunker invites while you're not in any session
        async Task EnterLobby()
        {
            if (_lobby != null || InSession) return;
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
                if (_status.StartsWith("Starting") || _status.StartsWith("Joining") || _status.StartsWith("Party")) _status = "Online (Photon)";
            }
            catch (Exception e) { Debug.LogWarning("[Rago] couldn't join the session lobby: " + e.Message); }
        }

        async Task LeaveLobby()
        {
            var r = _lobby; _lobby = null;
            if (r == null) return;
            try { await r.Shutdown(); } catch { }
            if (r != null) Destroy(r.gameObject);
        }

        // ---------------------------------------------------------------- players online
        int _listedPlayers = -1;
        object _statsSrc; PropertyInfo _onMaster, _inRooms; float _statsSearchT;

        /// <summary>
        /// How many people are playing right now. Photon's own app statistics (players on the master server - in their
        /// bunkers - plus players in sessions: the Market, parties and raids) when the SDK exposes them; otherwise
        /// everyone in a listed session plus you.
        /// </summary>
        public int OnlineCount
        {
            get
            {
                int s = StatsCount();
                if (s > 0) return s;
                return _listedPlayers;
            }
        }

        int StatsCount()
        {
            if (_lobby == null) return -1;
            if (_statsSrc == null && Time.realtimeSinceStartup > _statsSearchT)
            {
                _statsSearchT = Time.realtimeSinceStartup + 10f;
                try { FindStats(_lobby, 0, new HashSet<object>(RefEq.I)); } catch { }
            }
            if (_statsSrc == null) return -1;
            try { return (int)_onMaster.GetValue(_statsSrc) + (int)_inRooms.GetValue(_statsSrc); }
            catch { _statsSrc = null; return -1; }
        }

        bool FindStats(object o, int depth, HashSet<object> seen)
        {
            if (o == null || depth > 4 || !seen.Add(o)) return false;
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
            _listedPlayers = sum + 1;   // + you (in the lobby, not in a session)
            string me = Key(MyName());
            bool changed = false;
            foreach (var si in sessionList)
            {
                var props = si.Properties;
                if (props == null || !props.TryGetValue("kind", out var k) || Str(k) != "party") continue;
                if (!props.TryGetValue("inv", out var inv) || !props.TryGetValue("lead", out var lead)) continue;
                string invited = Str(inv), leader = Str(lead);
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

        static object Get(object o, Type t, string name)
        {
            var p = t.GetProperty(name, Any); if (p != null) return p.GetValue(o);
            var f = t.GetField(name, Any); return f?.GetValue(o);
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
            await LeaveLobby();
            _inPartySession = false;
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
            _names.Clear();
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
                if (_pendingRaidSession != null)
                {
                    // following the party leader into their raid session
                    sessionName = _pendingRaidSession; _queuePartySize = _pendingPartySize; _pendingRaidSession = null;
                }
                else if (_partyCode != null && _inPartySession)
                {
                    if (!IsHost) { failed?.Invoke("Only the party leader can start the queue"); _queueing = false; return; }
                    _wasLeader = true;
                    _queuePartySize = _peers.Count + 1;
                    sessionName = "rago_raid_" + map + "_" + _partyCode + "_" + UnityEngine.Random.Range(1000, 9999);
                    foreach (var p in _peers) SendSys(p, 6, map + "|" + sessionName + "|" + _queuePartySize);
                    await Task.Delay(400);   // let the message go out before leaving the party session
                }
                // random join of an open queue for this map, or create one (a party gets its own session that randoms can still fill)
                bool ok = await StartSession(sessionName, new Dictionary<string, SessionProperty> { { "kind", "raid_" + map } }, QueueRules.MaxFor(map));
                _peerParty.Clear(); _names.Clear();
                if (ok) foreach (var p in _peers) { SendSys(p, 3, MyName()); if (_partyCode != null) SendSys(p, 4, _partyCode); }
                if (!ok) { failed?.Invoke(_status); _queueing = false; return; }
                float atTarget = -1f;
                float queuedAt = Time.realtimeSinceStartup;
                while (!_cancel && InSession)
                {
                    if (_pendingStart != null) { var rs = _pendingStart; _pendingStart = null; _queueing = false; start?.Invoke(rs); return; }
                    var props = _runner.SessionInfo.Properties;
                    if (IsHost && (props == null || !props.ContainsKey("t0")))
                        try { _runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty> { { "kind", "raid_" + map }, { "t0", (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000) }, { "seed", UnityEngine.Random.Range(1, 999999) } }); }
                        catch (Exception e) { Debug.LogWarning("[Rago] couldn't set the queue start time: " + e.Message); }
                    int t0 = props != null && props.TryGetValue("t0", out var tp) ? (int)tp : (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000);
                    float elapsed = (float)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000 - t0);
                    // the session's start time may never sync (or the host changed): never wait longer than our own timer says
                    elapsed = Mathf.Max(elapsed, Time.realtimeSinceStartup - queuedAt, Time.realtimeSinceStartup - pressedAt);
                    int players = _peers.Count + 1;
                    if (players >= QueueRules.TargetFor(map)) { if (atTarget < 0) atTarget = Time.realtimeSinceStartup; } else atTarget = -1;
                    status?.Invoke(new QueueStatus { players = players, max = QueueRules.MaxFor(map), target = QueueRules.TargetFor(map), elapsed = elapsed, maxWait = QueueRules.MaxWait, text = QueueRules.Describe(players, elapsed, map) });
                    if (IsHost && QueueRules.ShouldStart(players, elapsed, atTarget < 0 ? 0 : Time.realtimeSinceStartup - atTarget, _queuePartySize, map))
                    {
                        try { _runner.SessionInfo.IsOpen = false; }   // closed (nobody can join) but still listed, so its players count as online
                        catch (Exception e) { Debug.LogWarning("[Rago] couldn't close the raid session: " + e.Message); }
                        int seed = props != null && props.TryGetValue("seed", out var sp) ? (int)sp : UnityEngine.Random.Range(1, 999999);
                        // each party shares one spawn spot (members stand side by side); everyone else gets their own
                        var spotOf = new Dictionary<string, int>(); var slotOf = new Dictionary<string, int>();
                        string mine = _partyCode ?? "solo_host";
                        spotOf[mine] = 0; slotOf[mine] = 1;
                        _nextSpawn = 1;
                        foreach (var p in _peers)
                        {
                            string key = _peerParty.TryGetValue(p, out var pc) && !string.IsNullOrEmpty(pc) ? pc : "solo_" + p;
                            if (!spotOf.ContainsKey(key)) { spotOf[key] = _nextSpawn++; slotOf[key] = 0; }
                            int slot = slotOf[key]++;
                            SendSys(p, 1, map + "|" + seed + "|" + (spotOf[key] % 10) + "|" + players + "|" + slot);
                        }
                        _queueing = false;
                        _status = "Raid (" + players + " players)";
                        start?.Invoke(new RaidStart { map = map, seed = seed, spawnIndex = 0, players = players, online = true, partySlot = 0 });
                        return;
                    }
                    // not the host and the host's go never came (left, lagging, message lost): don't sit past 1:00 - go in
                    // anyway with this session's seed, so we still land in the same raid as everyone else in it
                    if (!IsHost && elapsed >= QueueRules.MaxWait + 4f)
                    {
                        int seed = props != null && props.TryGetValue("seed", out var sp2) ? (int)sp2 : (_runner.SessionInfo.Name ?? map).GetHashCode() & 0x7fffff;
                        _queueing = false;
                        _status = "Raid (" + players + " players)";
                        start?.Invoke(new RaidStart { map = map, seed = seed, spawnIndex = (int)(LocalId % 10), players = players, online = true, partySlot = 0 });
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

        public void Send(byte[] data, bool reliable, ulong target = ulong.MaxValue)
        {
            if (!InSession || data == null) return;
            var packet = new byte[data.Length + 1];
            packet[0] = 0; // game channel
            Buffer.BlockCopy(data, 0, packet, 1, data.Length);
            if (target == ulong.MaxValue) { foreach (var p in _peers) SendTo(p, packet); }
            else if (target != LocalId) SendTo(target, packet);
        }

        void SendSys(ulong to, byte code, string text)
        {
            var t = System.Text.Encoding.UTF8.GetBytes(text);
            var packet = new byte[t.Length + 2];
            packet[0] = 1; packet[1] = code;
            Buffer.BlockCopy(t, 0, packet, 2, t.Length);
            SendTo(to, packet);
        }

        void SendTo(ulong id, byte[] packet)
        {
            if (_runner == null || !_refs.TryGetValue(id, out var p)) return;
            try { _runner.SendReliableDataToPlayer(p, GameKey, packet); } catch (Exception e) { Debug.LogWarning("[Rago] send failed: " + e.Message); }
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
            byte channel = data.Array[data.Offset];
            if (channel == 1)
            {
                if (data.Count < 2) return;
                byte code = data.Array[data.Offset + 1];
                string text = System.Text.Encoding.UTF8.GetString(data.Array, data.Offset + 2, data.Count - 2);
                if (code == 2) { _hostId = Id(player); return; }
                if (code == 1)
                {
                    var p = text.Split('|');
                    if (p.Length >= 4) _pendingStart = new RaidStart { map = p[0], seed = int.Parse(p[1]), spawnIndex = int.Parse(p[2]), players = int.Parse(p[3]), online = true, partySlot = p.Length >= 5 ? int.Parse(p[4]) : 0 };
                }
                else if (code == 3)
                {
                    // someone's callsign (sent to everyone in a session)
                    _names[Id(player)] = text;
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
                else if (code == 4) { _peerParty[Id(player)] = text; }                                         // which party a raid peer is in
                else if (code == 5 && _queueing && text == _partyCode) { _cancel = true; OnPartyQueueCancelled?.Invoke(); }   // leader cancelled
                else if (code == 6 && _inPartySession && !_queueing)
                {
                    // the leader queued: follow them into their raid session
                    var p = text.Split('|');
                    if (p.Length >= 3) { _pendingRaidSession = p[1]; _pendingPartySize = int.Parse(p[2]); _wasLeader = false; OnPartyQueue?.Invoke(p[0]); }
                }
                return;
            }
            var payload = new byte[data.Count - 1];
            Buffer.BlockCopy(data.Array, data.Offset + 1, payload, 0, payload.Length);
            OnData?.Invoke(Id(player), payload);
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
            OnPeerLeft?.Invoke(Id(player));
            if (_inPartySession) OnPartyChanged?.Invoke();
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (_leaving || runner != _runner) return;
            _status = "Disconnected (" + shutdownReason + ")";
            _peers.Clear();
            if (_inPartySession) { _inPartySession = false; OnPartyChanged?.Invoke(); }
            OnDisconnected?.Invoke();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (_leaving || runner != _runner) return;
            _status = "Disconnected (" + reason + ")";
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
            if (_peers.Contains(id)) return;
            _peers.Add(id);
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
                }
            }
            // the shared-mode master client runs the NPCs; tell everyone who that is
            if (!InSession || !IsHost) return;
            _announce -= Time.unscaledDeltaTime;
            if (_announce > 0) return;
            _announce = 2f;
            _hostId = LocalId;
            foreach (var p in _peers) SendSys(p, 2, "host");
        }

        void OnApplicationQuit() { _ = Shutdown(); }
    }
}
#endif
