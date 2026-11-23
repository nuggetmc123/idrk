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
    /// Online play over Photon Fusion 2 (Shared mode, Photon cloud). Rebuilt from scratch in 2.28 to be small and
    /// predictable:
    ///
    ///  * ONE Photon connection per player at any time: the session lobby while you idle in your bunker (that's how
    ///    invites reach you), OR one session - your party, the Market or a raid. Never two or three at once.
    ///  * Sessions: "rago_party_&lt;leader&gt;", "RagoMarket", raid queues "rago_q_&lt;map&gt;[_n]_&lt;slot&gt;"; all end in the
    ///    game version, so only the same version plays together.
    ///  * Who's who: every player joins with the Photon user id "CALLSIGN@party~1234", readable by everyone at once,
    ///    and also says its callsign in a message (in case a Photon version doesn't pass user ids on).
    ///  * Data: every packet is stamped with its sender. Events (shots, hits, items, trades) go reliably to each player,
    ///    batched 15 times a second. Movement, voice and raider positions go ONCE to everyone as an unreliable Photon
    ///    RPC (RagoRpc); a heartbeat checks that lane works and falls back to reliable sends if it doesn't.
    ///  * Everything that happens (connects, failures with Photon's reason, players joining / leaving) is kept in a log
    ///    shown in the menu (RECENT tab) - screenshot it when something goes wrong.
    /// </summary>
    public class FusionBackend : MonoBehaviour, INetBackend, INetworkRunnerCallbacks
    {
        // =========================================================================================== state
        NetworkRunner _runner;          // the session we're in (party, Market or raid), or null
        NetworkRunner _lobby;           // the session lobby (only while not in a session)
        string _status = "Offline";
        bool _ready, _leaving;
        string _sessionId = "", _sessionKind = "";
        readonly List<ulong> _peers = new List<ulong>();
        readonly Dictionary<ulong, PlayerRef> _refs = new Dictionary<ulong, PlayerRef>();
        readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, string> _peerParty = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, float> _lastHeard = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, float> _peerSince = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, float> _nameAsk = new Dictionary<ulong, float>();
        readonly HashSet<ulong> _relayTo = new HashSet<ulong>();
        ulong _hostId;

        public string Status => _status;
        public bool Ready => _ready;
        public bool InSession => _runner != null && _runner.IsRunning && !_leaving;
        public bool IsHost => InSession && _runner.IsSharedModeMasterClient;
        public ulong LocalId => _runner != null ? Id(_runner.LocalPlayer) : 0;
        public ulong HostId => IsHost ? LocalId : _hostId;
        public string SessionId => _sessionId;
        public IReadOnlyList<ulong> Peers => _peers;
        public event Action<ulong, byte[]> OnData;
        public event Action<ulong> OnPeerJoined;
        public event Action<ulong> OnPeerLeft;
        public event Action OnDisconnected;
        public event Action OnPartyChanged;
        public event Action<string> OnPartyQueue;
        public event Action OnPartyQueueCancelled;
        public event Action<string> OnMetPlayer;

        static ulong Id(PlayerRef p) => (ulong)p.PlayerId;
        static string MyName() => Profile.Current != null ? Profile.Current.DisplayName : "Operator";
        static string Key(string callsign) => (callsign ?? "").Trim().ToLowerInvariant();
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // =========================================================================================== log
        // what happened, newest last - shown in the menu so a screenshot tells exactly what went wrong
        static readonly List<string> _log = new List<string>();
        static void Log(string s, bool warn = false)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " " + s;
            _log.Add(line); while (_log.Count > 12) _log.RemoveAt(0);
            if (warn) Debug.LogWarning("[Rago] " + s); else Debug.Log("[Rago] " + s);
        }

        public string DebugInfo
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                float now = Time.realtimeSinceStartup;
                sb.Append("NETWORK  v").Append(Ver.Replace("_v", "").Replace('_', '.')).Append("  region ").Append(Region()).Append('\n');
                sb.Append(_status).Append('\n');
                if (InSession)
                {
                    sb.Append("session ").Append(_sessionId).Append(" (").Append(_sessionKind).Append(")\n");
                    sb.Append("me #").Append(LocalId).Append(IsHost ? "  HOST" : "  host #" + _hostId).Append("  rtt ").Append((_rtt * 1000f).ToString("0")).Append("ms  ")
                      .Append(FastBroadcast ? "FAST" : _fastBroken ? "SLOW (fast lane off)" : "SLOW").Append(RelRpc ? " +RPC" : _relBroken ? " (RPC off)" : "").Append(_oversize > 0 ? "  too big: " + _oversize : "").Append('\n');
                    foreach (var p in _peers)
                    {
                        sb.Append(" #").Append(p).Append(' ').Append(_names.TryGetValue(p, out var n) ? n : "(no name yet)")
                          .Append("  heard ").Append(_lastHeard.TryGetValue(p, out var h) ? (now - h).ToString("0.0") + "s" : "never")
                          .Append(_fastAt.TryGetValue(p, out var f) && now - f < 5f ? " fast" : "")
                          .Append(_relAt.TryGetValue(p, out var rl) && now - rl < 5f ? " rpc" : "")
                          .Append(_relayTo.Contains(p) ? " RELAY" : "").Append('\n');
                    }
                }
                else sb.Append(_lobby != null ? "in the lobby (listening for invites)\n" : "not connected\n");
                if (_partyCode != null) sb.Append("party ").Append(_partyCode).Append(_inPartySession ? " (together)" : " (apart)").Append('\n');
                sb.Append("-- log --\n");
                foreach (var l in _log) sb.Append(l).Append('\n');
                return sb.ToString();
            }
        }

        // =========================================================================================== setup
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (RelayBackend.Configured) return;   // our own game server is set up (Resources/Rago/server.txt): no Photon
            var go = new GameObject("RagoFusion");
            DontDestroyOnLoad(go);
            NetHub.Backend = go.AddComponent<FusionBackend>();
        }

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
            Log(_ready ? "ready, app id set, region " + Region() : "no Photon App ID - offline");
            done?.Invoke(_ready);
        }

        /// <summary>Only the same game version plays together (different versions build different maps).</summary>
        static string Ver { get { try { return "_v" + PatchNotes.All[0].version.Replace('.', '_'); } catch { return ""; } } }

        // ---- Photon settings, reached by name: the app-settings type moved between Fusion 2 versions
        static object MakeAppSettings(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return null;
            try
            {
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("Fusion.Photon.Realtime.PhotonAppSettings", false); if (t != null) break; }
                if (t == null) return null;
                var settings = Get(Get(null, t, "Global"), t, "AppSettings");
                if (settings == null) return null;
                var copy = settings.GetType().GetMethod("GetCopy", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)?.Invoke(settings, null) ?? settings;
                if (!Set(copy, "AppIdFusion", appId)) return null;
                Set(copy, "FixedRegion", Region());   // everyone on ONE region, or friends land on different servers
                return copy;
            }
            catch (Exception e) { Log("couldn't apply the App ID: " + e.Message, true); return null; }
        }

        static void UseAppId(ref StartGameArgs args)
        {
            var s = MakeAppSettings(AppId());
            if (s == null) return;
            object boxed = args;
            if (Set(boxed, "CustomPhotonAppSettings", s)) args = (StartGameArgs)boxed;
        }

        static void UseUserId(ref StartGameArgs args, string uid)
        {
            try
            {
                var f = typeof(StartGameArgs).GetField("AuthValues", Any); var pr = f == null ? typeof(StartGameArgs).GetProperty("AuthValues", Any) : null;
                var t = f != null ? f.FieldType : pr?.PropertyType;
                if (t == null) return;
                object av = null;
                try { av = Activator.CreateInstance(t, uid); } catch { }
                if (av == null) { av = Activator.CreateInstance(t); Set(av, "UserId", uid); }
                object boxed = args;
                if (Set(boxed, "AuthValues", av)) args = (StartGameArgs)boxed;
            }
            catch { }
        }

        static bool _timeoutDone;
        /// <summary>Building a big raid map on a Quest can freeze the game for a while: give Photon 40 s before it drops us.</summary>
        static void LongerTimeout()
        {
            if (_timeoutDone) return;
            _timeoutDone = true;
            try
            {
                var cfgType = Type.GetType("Fusion.NetworkProjectConfig, Fusion.Runtime");
                var global = cfgType?.GetProperty("Global", Any)?.GetValue(null);
                if (global == null) return;
                var nf = cfgType.GetField("Network", Any); var np = nf == null ? cfgType.GetProperty("Network", Any) : null;
                object net = nf != null ? nf.GetValue(global) : np?.GetValue(global);
                if (net == null) return;
                foreach (var name in new[] { "ConnectionTimeout", "ConnectionTimeoutSeconds" })
                {
                    var f = net.GetType().GetField(name, Any);
                    if (f != null) { f.SetValue(net, Convert.ChangeType(40, f.FieldType)); continue; }
                    var p = net.GetType().GetProperty(name, Any);
                    if (p != null && p.CanWrite) p.SetValue(net, Convert.ChangeType(40, p.PropertyType));
                }
                if (net.GetType().IsValueType) { if (nf != null) nf.SetValue(global, net); else np?.SetValue(global, net); }
            }
            catch { }
        }

        static object Get(object o, Type t, string name)
        {
            var p = t.GetProperty(name, Any); if (p != null) return p.GetValue(o);
            return t.GetField(name, Any)?.GetValue(o);
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

        /// <summary>A session property's text, whichever way this Fusion version stores it.</summary>
        static string Str(object prop)
        {
            if (prop == null) return "";
            var t = prop.GetType();
            var pv = t.GetProperty("PropertyValue") ?? t.GetProperty("Value");
            if (pv != null) return pv.GetValue(prop)?.ToString() ?? "";
            var f = t.GetField("PropertyValue") ?? t.GetField("Value");
            if (f != null) return f.GetValue(prop)?.ToString() ?? "";
            return prop.ToString();
        }

        static int Fnv(string s) { unchecked { int h = (int)2166136261; foreach (char c in s ?? "") h = (h ^ c) * 16777619; return h & 0x7fffffff; } }

        // =========================================================================================== sessions
        /// <summary>Leave whatever we're connected to (lobby included) and join / create one session.</summary>
        bool _starting;
        async Task<bool> StartSession(string name, Dictionary<string, SessionProperty> props, int maxPlayers, bool allowCreate = true)
        {
            _starting = true;
            try { return await StartSessionInner(name, props, maxPlayers, allowCreate); }
            finally { _starting = false; }
        }

        async Task<bool> StartSessionInner(string name, Dictionary<string, SessionProperty> props, int maxPlayers, bool allowCreate)
        {
            await Shutdown();
            await LeaveLobby();   // one connection at a time
            _inPartySession = false;
            _names.Clear(); _peerParty.Clear(); _nameAsk.Clear(); _acks.Clear(); _out.Clear();
            _lastHeard.Clear(); _peerSince.Clear(); _fastAt.Clear(); _relayTo.Clear(); _uidRead.Clear(); _relOut.Clear(); _relAt.Clear();
            _hostId = 0; _rtt = 0f; _pingOut = -1f;
            _sessionKind = props != null && props.TryGetValue("kind", out var kp) ? Str(kp) : "";
            var go = new GameObject("FusionRunner");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<NetworkRunner>();
            r.ProvideInput = false;
            r.AddCallbacks(this);
            var smType = Type.GetType("Fusion.NetworkSceneManagerDefault, Fusion.Unity");
            var sceneManager = smType != null ? go.AddComponent(smType) as INetworkSceneManager : null;
            var args = new StartGameArgs { GameMode = GameMode.Shared, SessionName = name, PlayerCount = maxPlayers, SessionProperties = props, SceneManager = sceneManager };
            UseAppId(ref args);
            LongerTimeout();
            _myUid = MyName() + "@" + (_partyCode ?? "") + "~" + UnityEngine.Random.Range(1000, 9999);
            _uidBad = false;
            UseUserId(ref args, _myUid);
            if (!allowCreate) { object boxed = args; if (Set(boxed, "EnableClientSessionCreation", false)) args = (StartGameArgs)boxed; }
            _runner = r;
            _byRunner[r] = this;
            StartGameResult res = null;
            try { res = await r.StartGame(args); }
            catch (Exception e) { Log("join " + name + " crashed: " + e.Message, true); }
            if (res == null || !res.Ok)
            {
                string why = res != null ? res.ShutdownReason + (string.IsNullOrEmpty(res.ErrorMessage) ? "" : " - " + res.ErrorMessage) : "error";
                _status = "Photon: " + (res != null ? Explain(res.ShutdownReason.ToString()) : "error");
                Log((allowCreate ? "couldn't join/create " : "couldn't join ") + name + ": " + why, true);
                await Shutdown();
                return false;
            }
            _leaving = false;
            _sessionId = _runner.SessionInfo.Name;
            RefreshPeers();
            Log("joined " + _sessionId + " (" + (_peers.Count + 1) + " here" + (IsHost ? ", host" : "") + ")");
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
            _byRunner.Remove(r);
            _fastOut.SetLength(0); _out.Clear(); _relOut.Clear();
            try { await r.Shutdown(); } catch { }
            if (r != null) Destroy(r.gameObject);
            _peers.Clear(); _refs.Clear();
            _leaving = false;
        }

        // ---- the session lobby: while you're not in any session, so party invites can reach you
        async Task EnterLobby()
        {
            if (_lobby != null || _runner != null || _starting || !_ready) return;
            var go = new GameObject("FusionLobby");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<NetworkRunner>();
            r.AddCallbacks(this);
            _lobby = r;
            try
            {
                var m = typeof(NetworkRunner).GetMethod("JoinSessionLobby", BindingFlags.Public | BindingFlags.Instance);
                if (m == null) { Log("this Fusion version has no session lobby: invites only work in a raid together", true); return; }
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
                object result = null;
                if (m.Invoke(r, args) is Task t) { await t; result = t.GetType().GetProperty("Result")?.GetValue(t); }
                if (result is StartGameResult sr && !sr.Ok) { Log("lobby: " + sr.ShutdownReason, true); await LeaveLobby(); return; }
                if (_lobby == r) Log("in the lobby (invites can reach you)");
            }
            catch (Exception e) { Log("couldn't join the lobby: " + (e.InnerException ?? e).Message, true); await LeaveLobby(); }
        }

        async Task LeaveLobby()
        {
            var r = _lobby; _lobby = null;
            if (r == null) return;
            try { await r.Shutdown(); } catch { }
            if (r != null) Destroy(r.gameObject);
        }

        public async void JoinMarket(Action<bool> done)
        {
            if (!_ready) { done?.Invoke(false); return; }
            _status = "Joining the Market...";
            bool ok = await StartSession("RagoMarket" + Ver, new Dictionary<string, SessionProperty> { { "kind", "market" } }, 20);
            _status = ok ? "Market (" + (_peers.Count + 1) + " players)" : _status;
            done?.Invoke(ok);
            if (!ok) _ = EnterLobby();
        }

        public async void Leave()
        {
            if (InSession) Log("left " + _sessionId);
            await Shutdown();
            _inPartySession = false;
            _status = _ready ? "Online (Photon)" : _status;
        }

        // =========================================================================================== raids
        RaidStart _pendingStart;
        bool _queueing, _cancel, _queueNight;
        string _raidMap;
        int _queuePartySize = 1;
        string _lastRaidSession, _lastRaidMap; float _lastRaidAt = -999f;

        static string SlotName(string map, bool night, int slot) => "rago_q_" + map + (night ? "_n" : "") + "_" + slot + Ver;
        static int SlotOf(string name)
        {
            if (name == null) return 1;
            string core = name.EndsWith(Ver) ? name.Substring(0, name.Length - Ver.Length) : name;
            int u = core.LastIndexOf('_');
            return u >= 0 && int.TryParse(core.Substring(u + 1), out int n) ? n : 1;
        }

        /// <summary>The first queue session (from this number on) that the lobby shows hasn't launched and has room.</summary>
        string ChooseSlot(string map, bool night, int partySize, int from)
        {
            bool fresh = Time.realtimeSinceStartup - _listedAt < 20f;
            for (int i = from; i < from + 40; i++)
            {
                string n = SlotName(map, night, i);
                if (!fresh || !_listed.TryGetValue(n, out var st)) return n;
                if (!st.started && st.players + partySize <= QueueRules.MaxFor(map)) return n;
            }
            return SlotName(map, night, from);
        }

        public async void QueueRaid(string map, Action<QueueStatus> status, Action<RaidStart> start, Action<string> failed)
        {
            if (!_ready) { failed?.Invoke("offline"); return; }
            if (_queueing) return;
            _queueing = true; _cancel = false; _pendingStart = null; _raidMap = map;
            float pressedAt = Time.realtimeSinceStartup;
            try
            {
                status?.Invoke(new QueueStatus { players = 1, max = QueueRules.MaxFor(map), text = "Finding a raid..." });
                string sessionName = null; _queuePartySize = 1;
                bool night = RaidChoice.Night;
                if (_pendingRaidSession != null)
                {
                    // following the party leader into the raid session they picked
                    sessionName = _pendingRaidSession; _queuePartySize = _pendingPartySize; _pendingRaidSession = null;
                    night = _pendingNight; RaidChoice.Night = night;
                    Log("following the leader into " + sessionName);
                }
                else if (_partyCode != null && _inPartySession)
                {
                    if (!PartyLeader) { failed?.Invoke("Only the party leader can start the queue"); _queueing = false; return; }
                    _wasLeader = true;
                    _queuePartySize = _peers.Count + 1;
                    sessionName = ChooseSlot(map, night, _queuePartySize, 1);
                    string go6 = map + "|" + sessionName + "|" + _queuePartySize + "|" + (night ? 1 : 0);
                    // tell the party: on the party session itself (can't get lost) and by message, until each one answers
                    try
                    {
                        var pp = CurrentProps();
                        pp["raid"] = go6 + "|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        _runner.SessionInfo.UpdateCustomProperties(pp);
                    }
                    catch (Exception e) { Log("couldn't post the raid to the party: " + e.Message, true); }
                    _acks.Clear();
                    var members = new List<ulong>(_peers);
                    float until = Time.realtimeSinceStartup + 5f;
                    while (Time.realtimeSinceStartup < until && InSession)
                    {
                        bool all = true;
                        foreach (var p in members) if (!_acks.Contains(p) && _peers.Contains(p)) { all = false; SendSys(p, 6, go6); }
                        FlushAll();
                        if (all) break;
                        await Task.Delay(500);
                    }
                    await Task.Delay(300);
                    Log("party of " + _queuePartySize + " heading for " + sessionName);
                }
                _queueNight = night;
                bool ok = false;
                int startSlot = sessionName != null ? SlotOf(sessionName) : 1;
                if (sessionName == null) sessionName = ChooseSlot(map, night, _queuePartySize, startSlot);
                for (int attempt = 0; attempt < 12 && !_cancel; attempt++)
                {
                    ok = await StartSession(sessionName, new Dictionary<string, SessionProperty> { { "kind", "raid_" + map + (night ? "_n" : "") } }, QueueRules.MaxFor(map));
                    var jp = ok ? _runner.SessionInfo.Properties : null;
                    bool launched = jp != null && (jp.ContainsKey("go") || (jp.TryGetValue("kind", out var jk) && Str(jk).StartsWith("raid_started")));
                    bool full = ok && _queuePartySize > 1 && _wasLeader && _peers.Count + _queuePartySize > QueueRules.MaxFor(map);
                    if (ok && !launched && !full) break;
                    if (ok) { Log(sessionName + (launched ? " already launched" : " too full for the party") + " - trying the next"); await Shutdown(); ok = false; }
                    sessionName = SlotName(map, night, SlotOf(sessionName) + 1);
                }
                if (!ok) { failed?.Invoke(_status); _queueing = false; RejoinParty(); return; }
                if (_partyCode != null) foreach (var p in _peers) SendSys(p, 4, _partyCode);
                float atTarget = -1f, queuedAt = Time.realtimeSinceStartup;
                while (!_cancel && InSession)
                {
                    var props = _runner.SessionInfo.Properties;
                    // the host said go (message) or put "go" on the session (property - can't get lost)
                    if (_pendingStart == null && !IsHost && props != null && props.ContainsKey("go") && props.TryGetValue("seed", out var gs))
                        _pendingStart = new RaidStart { map = map, seed = (int)gs, spawnIndex = -1, players = _peers.Count + 1, online = true, night = night };
                    if (_pendingStart != null)
                    {
                        var rs = _pendingStart; _pendingStart = null;
                        if (rs.spawnIndex < 0) { SpawnFor(MyName(), out int sp0, out int sl0); rs.spawnIndex = sp0; rs.partySlot = sl0; }
                        Launch(map, rs, start);
                        return;
                    }
                    if (IsHost && (props == null || !props.ContainsKey("t0")))
                        try { var np = CurrentProps(); np["kind"] = "raid_" + map + (night ? "_n" : ""); np["t0"] = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000); np["seed"] = UnityEngine.Random.Range(1, 999999); _runner.SessionInfo.UpdateCustomProperties(np); }
                        catch (Exception e) { Log("couldn't set the queue clock: " + e.Message, true); }
                    int t0 = props != null && props.TryGetValue("t0", out var tp) ? (int)tp : (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000);
                    float elapsed = (float)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000 - t0);
                    elapsed = Mathf.Max(elapsed, Time.realtimeSinceStartup - queuedAt, Time.realtimeSinceStartup - pressedAt);
                    int players = _peers.Count + 1;
                    if (players >= QueueRules.TargetFor(map)) { if (atTarget < 0) atTarget = Time.realtimeSinceStartup; } else atTarget = -1;
                    status?.Invoke(new QueueStatus { players = players, max = QueueRules.MaxFor(map), target = QueueRules.TargetFor(map), elapsed = elapsed, maxWait = QueueRules.MaxWait, text = QueueRules.Describe(players, elapsed, map) });
                    if (IsHost && QueueRules.ShouldStart(players, elapsed, atTarget < 0 ? 0 : Time.realtimeSinceStartup - atTarget, _queuePartySize, map))
                    {
                        int seed = props != null && props.TryGetValue("seed", out var sp) ? (int)sp : UnityEngine.Random.Range(1, 999999);
                        try
                        {
                            var gp = CurrentProps();
                            gp["seed"] = seed; gp["go"] = players; gp["kind"] = "raid_started_" + map; gp["map"] = map; gp["night"] = night ? 1 : 0;
                            _runner.SessionInfo.UpdateCustomProperties(gp);
                        }
                        catch (Exception e) { Log("couldn't mark the raid started: " + e.Message, true); }
                        foreach (var p in _peers) SendSys(p, 1, map + "|" + seed + "|-1|" + players + "|0");
                        FlushAll();
                        SpawnFor(MyName(), out int mySpot, out int mySlot);
                        Launch(map, new RaidStart { map = map, seed = seed, spawnIndex = mySpot, players = players, online = true, partySlot = mySlot, night = night }, start);
                        return;
                    }
                    // the host's go never came (left, lagging): don't sit past 1:00 - go in with this session's seed
                    if (!IsHost && elapsed >= QueueRules.MaxWait + 4f)
                    {
                        int seed = props != null && props.TryGetValue("seed", out var sp2) ? (int)sp2 : Fnv(_sessionId) & 0x7fffff;
                        SpawnFor(MyName(), out int sp1, out int sl1);
                        Log("no go from the host - starting anyway");
                        Launch(map, new RaidStart { map = map, seed = seed, spawnIndex = sp1, players = players, online = true, partySlot = sl1, night = night }, start);
                        return;
                    }
                    await Task.Delay(500);
                }
                if (_cancel) { Log("queue cancelled"); await Shutdown(); }
                else Log("lost the queue session", true);
            }
            catch (Exception e)
            {
                Log("queue failed: " + e.Message, true);
                failed?.Invoke(e.Message);
                await Shutdown();
            }
            _queueing = false;
            RejoinParty();
        }

        void Launch(string map, RaidStart rs, Action<RaidStart> start)
        {
            _queueing = false;
            _status = "Raid (" + rs.players + " players)";
            _lastRaidSession = _sessionId; _lastRaidMap = map; _lastRaidAt = Time.realtimeSinceStartup;
            Log("raid starting: " + rs.players + " players, spawn " + rs.spawnIndex % 1000 + "/" + rs.partySlot);
            start?.Invoke(rs);
        }

        Dictionary<string, SessionProperty> CurrentProps()
        {
            var d = new Dictionary<string, SessionProperty>();
            var cur = _runner != null ? _runner.SessionInfo.Properties : null;
            if (cur != null) foreach (var kv in cur) d[kv.Key] = kv.Value;
            return d;
        }

        public void CancelQueue()
        {
            if (_partyCode != null && _wasLeader && InSession)
                foreach (var p in _peers) if (_peerParty.TryGetValue(p, out var pc) && pc == _partyCode) SendSys(p, 5, _partyCode);
            FlushAll();
            _cancel = true;
        }

        /// <summary>Everyone's spawn worked out the same way on every machine: each party shares one spot (members side by
        /// side, in callsign order), solo players get their own. A group's spot comes only from its own key.</summary>
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
            spot = 0; slot = 0;
            foreach (var kv in groups)
            {
                kv.Value.Sort(StringComparer.Ordinal);
                int k = kv.Value.IndexOf(Key(myName));
                if (k >= 0) { spot = Fnv(kv.Key); slot = k; }
            }
        }

        // ---- getting back into a raid you dropped out of
        public bool CanReconnect => _ready && _lastRaidSession != null && Time.realtimeSinceStartup - _lastRaidAt < 300f && !_queueing;
        public void ForgetRaid() { _lastRaidSession = null; }
        public async void Reconnect(Action<bool> done)
        {
            if (!CanReconnect) { done?.Invoke(false); return; }
            _status = "Reconnecting...";
            bool ok = false;
            for (int tryN = 0; tryN < 3 && !ok; tryN++)
            {
                if (tryN > 0) await Task.Delay(2500);
                ok = await StartSession(_lastRaidSession, new Dictionary<string, SessionProperty> { { "kind", "raid_started_" + _lastRaidMap } }, QueueRules.MaxFor(_lastRaidMap), false);
            }
            if (ok)
            {
                float until = Time.realtimeSinceStartup + 4f;
                while (_peers.Count == 0 && Time.realtimeSinceStartup < until && InSession) { await Task.Delay(200); RefreshPeers(); }
                if (_peers.Count == 0) { ok = false; Log("nobody left in that raid"); await Shutdown(); }
            }
            if (ok && _partyCode != null) foreach (var p in _peers) SendSys(p, 4, _partyCode);
            _status = ok ? "Raid (reconnected)" : (_ready ? "Online (Photon)" : _status);
            if (!ok) _lastRaidSession = null;
            done?.Invoke(ok);
            if (!ok) RejoinParty();
        }

        // =========================================================================================== party
        // A party is a small session "rago_party_<leader>" the members sit in while in their bunkers. Invites reach
        // players idling in their bunker through the lobby: the party session lists who's invited ("inv"). Inside a raid
        // or the Market you can invite people who are in it with you (sent straight to them).
        string _partyCode;
        bool _inPartySession, _wasLeader;
        readonly List<string> _members = new List<string>();
        readonly List<string> _roster = new List<string>();
        readonly List<string> _sessionPlayers = new List<string>();
        readonly List<PartyInvite> _invites = new List<PartyInvite>();
        readonly List<string> _invited = new List<string>();
        readonly HashSet<ulong> _acks = new HashSet<ulong>();
        string _pendingRaidSession, _lastRaid; int _pendingPartySize = 1; bool _pendingNight;

        public string PartyCode => _partyCode;
        public bool PartyLeader
        {
            get
            {
                if (_partyCode == null) return false;
                if (Key(_partyCode) == Key(MyName())) return true;
                if (!_inPartySession) return _wasLeader;
                foreach (var p in _peers) if (_names.TryGetValue(p, out var n) && Key(n) == Key(_partyCode)) return false;
                return IsHost && _names.Count >= _peers.Count;   // the leader isn't here: the session's host leads
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
                    if (_queueing || _pendingRaidSession != null) { foreach (var n in _roster) if (!_members.Exists(x => Key(x) == Key(n))) _members.Add(n); }
                    else if (_peers.Count > 0 && _names.Count >= _peers.Count) { _roster.Clear(); foreach (var p in _peers) if (_names.TryGetValue(p, out var n2)) _roster.Add(n2); }
                }
                else
                {
                    foreach (var n in _roster) _members.Add(n);
                    if (InSession) foreach (var p in _peers) if (IsTeammate(p) && _names.TryGetValue(p, out var n) && !_roster.Exists(x => Key(x) == Key(n))) { _roster.Add(n); _members.Add(n); }
                }
                return _members;
            }
        }
        public IReadOnlyList<string> SessionPlayers
        {
            get { _sessionPlayers.Clear(); if (InSession) foreach (var p in _peers) if (_names.TryGetValue(p, out var n)) _sessionPlayers.Add(n); return _sessionPlayers; }
        }
        public bool IsTeammate(ulong peer) => _partyCode != null && _peerParty.TryGetValue(peer, out var pc) && pc == _partyCode;

        static string PartySession(string id)
        {
            var sb = new System.Text.StringBuilder("rago_party_");
            foreach (char c in Key(id)) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.Append(Ver).ToString();
        }

        void Remember(string callsign)
        {
            _invited.RemoveAll(x => Key(x) == Key(callsign));
            _invited.Add(callsign.Trim());
            while (_invited.Count > 12) _invited.RemoveAt(0);
        }

        public async void InviteToParty(string callsign)
        {
            if (!_ready || string.IsNullOrWhiteSpace(callsign) || Key(callsign) == Key(MyName())) return;
            if (_partyCode != null && PartyMembers.Count >= QueueRules.PartyMax) { RagoGame.I?.Player?.Toast("Your party is full"); return; }
            if (InSession && !_inPartySession)
            {
                // in a raid / the Market: straight to them if they're here; otherwise it's posted when you're back home
                if (_partyCode == null) { _partyCode = MyName(); _wasLeader = true; OnPartyChanged?.Invoke(); }
                ulong to = 0; bool found = false;
                foreach (var kv in _names) if (Key(kv.Value) == Key(callsign) && _peers.Contains(kv.Key)) { to = kv.Key; found = true; }
                if (found) { SendSys(to, 8, MyName() + "|" + _partyCode); Log("invited " + callsign + " (here with you)"); }
                else { Remember(callsign); Log("invite to " + callsign + " goes out when you're back in your bunker"); RagoGame.I?.Player?.Toast("Invite sent when you're back in your bunker"); }
                return;
            }
            Remember(callsign);
            if (_partyCode == null) { if (!await EnterParty(MyName(), true)) return; }
            else if (!_inPartySession) { if (!await EnterParty(_partyCode, true)) return; }
            if (IsHost) PublishInvites(); else foreach (var p in _peers) SendSys(p, 7, callsign);
            Log("invited " + callsign);
        }

        public async void AcceptInvite(PartyInvite inv)
        {
            if (inv == null || !_ready) return;
            _invites.Remove(inv);
            if (_queueing) return;
            if (InSession && !_inPartySession)
            {
                // accepted inside a raid: you join them when you're both back in your bunkers
                _partyCode = inv.party; _wasLeader = false;
                OnPartyChanged?.Invoke();
                return;
            }
            _partyCode = null;
            bool ok = await EnterParty(inv.party, false);
            if (!ok) ok = await EnterParty(inv.party, true);   // the leader isn't there right now: open it for them
            if (!ok) _ = EnterLobby();
            OnPartyChanged?.Invoke();
        }

        public void DeclineInvite(PartyInvite inv) { _invites.Remove(inv); OnPartyChanged?.Invoke(); }

        public async void LeaveParty()
        {
            Log("left the party");
            _partyCode = null; _wasLeader = false; _invited.Clear(); _roster.Clear();
            if (_inPartySession) { _inPartySession = false; await Shutdown(); }
            _status = _ready ? "Online (Photon)" : _status;
            OnPartyChanged?.Invoke();
            RejoinParty();
        }

        public async void RejoinParty()
        {
            if (!_ready || _queueing) return;
            if (InSession && !_inPartySession) return;   // in the Market / a raid: wait until you're back home
            if (_partyCode != null)
            {
                if (_inPartySession) return;
                if (await EnterParty(_partyCode, true)) { if (PartyLeader && _invited.Count > 0) PublishInvites(); }
                else _ = EnterLobby();
            }
            else await EnterLobby();
        }

        async Task<bool> EnterParty(string id, bool create)
        {
            _status = (create ? "Starting" : "Joining") + " party...";
            bool ok = await StartSession(PartySession(id), new Dictionary<string, SessionProperty> { { "kind", "party" }, { "lead", id }, { "inv", create ? string.Join(",", _invited.ConvertAll(Key)) : "" }, { "names", MyName() } }, QueueRules.PartyMax, create);
            if (ok && !create && _peers.Count == 0) { ok = false; Log("that party is empty"); await Shutdown(); }
            if (!ok) { OnPartyChanged?.Invoke(); return false; }
            _partyCode = id; _inPartySession = true;
            _wasLeader = Key(id) == Key(MyName()) || IsHost;
            _status = "Party (" + (_peers.Count + 1) + ")";
            OnPartyChanged?.Invoke();
            return true;
        }

        string NamesHere()
        {
            var names = new List<string> { MyName() };
            foreach (var p in _peers) if (_names.TryGetValue(p, out var n)) names.Add(n);
            return string.Join(",", names);
        }

        /// <summary>The host lists everyone's callsigns on the session (and, in a party, who's invited), for the lobby.</summary>
        void PublishNames()
        {
            if (!IsHost) return;
            try
            {
                var props = CurrentProps();
                props["names"] = NamesHere();
                if (_inPartySession) { props["kind"] = "party"; props["lead"] = _partyCode ?? MyName(); props["inv"] = string.Join(",", _invited.ConvertAll(Key)); }
                _runner.SessionInfo.UpdateCustomProperties(props);
            }
            catch (Exception e) { Log("couldn't update the session's player list: " + e.Message); }
        }
        void PublishInvites() => PublishNames();

        // ---- the lobby's list of sessions: invites, who's online, which raid queues are open
        int _listedPlayers = -1;
        float _listedAt = -999f;
        readonly Dictionary<string, (int players, bool started)> _listed = new Dictionary<string, (int, bool)>();

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner != _lobby || sessionList == null) return;
            int sum = 0; string me = Key(MyName()); bool changed = false;
            _listed.Clear();
            foreach (var si in sessionList)
            {
                int c = si.GetType().GetProperty("PlayerCount", Any)?.GetValue(si) is int pc ? pc : 0;
                var pr = si.Properties;
                string kind = pr != null && pr.TryGetValue("kind", out var kk) ? Str(kk) : "";
                bool started = pr != null && (pr.ContainsKey("go") || kind.StartsWith("raid_started"));
                _listed[si.Name ?? ""] = (c, started);
                if (si.Name == null || !si.Name.EndsWith(Ver)) continue;   // another game version
                sum += c;
                if (pr == null) continue;
                if (pr.TryGetValue("names", out var nm)) foreach (var n in Str(nm).Split(',')) KnownPlayers.Add(n);
                if (kind != "party" || !pr.TryGetValue("inv", out var inv) || !pr.TryGetValue("lead", out var lead)) continue;
                string leader = Str(lead);
                KnownPlayers.Add(leader);
                if (Array.IndexOf(Str(inv).Split(','), me) < 0) continue;
                if (_invites.Exists(x => Key(x.party) == Key(leader))) continue;
                _invites.Add(new PartyInvite { from = leader, party = leader, time = Time.realtimeSinceStartup });
                Log("invite from " + leader);
                changed = true;
                RagoGame.I?.Player?.Toast(leader + " invited you to their party - press the menu button to accept");
            }
            _listedAt = Time.realtimeSinceStartup;
            _listedPlayers = sum + 1;   // + you (in the lobby, not in any session)
            if (changed) OnPartyChanged?.Invoke();
        }

        /// <summary>Players online: everyone in a session of this version the lobby listed (+ you), or who's here with you.</summary>
        public int OnlineCount
        {
            get
            {
                int here = InSession ? _peers.Count + 1 : 1;
                bool fresh = _listedPlayers > 0 && Time.realtimeSinceStartup - _listedAt < (InSession ? 900f : 30f);
                return fresh ? Mathf.Max(here, _listedPlayers) : here;
            }
        }

        // =========================================================================================== who's who
        string _myUid; bool _uidBad;
        readonly HashSet<ulong> _uidRead = new HashSet<ulong>();
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

        void LearnFromUserId(ulong id, PlayerRef p)
        {
            if (_uidBad) return;
            var uid = UserIdOf(p);
            if (string.IsNullOrEmpty(uid)) return;
            if (uid == _myUid || uid.StartsWith(MyName() + "@"))
            {
                // this Photon hands back OUR id for everyone: stop trusting ids, use the name messages
                _uidBad = true;
                foreach (var k in new List<ulong>(_names.Keys)) if (Key(_names[k]) == Key(MyName())) _names.Remove(k);
                _peerParty.Clear(); _nameAsk.Clear();
                Log("Photon user ids unreliable here - using name messages");
                return;
            }
            _uidRead.Add(id);
            int at = uid.IndexOf('@'), tl = uid.LastIndexOf('~');
            string name = at >= 0 ? uid.Substring(0, at) : tl >= 0 ? uid.Substring(0, tl) : uid;
            string party = at >= 0 ? uid.Substring(at + 1, (tl > at ? tl : uid.Length) - at - 1) : "";
            if (!string.IsNullOrEmpty(party) && !_peerParty.ContainsKey(id)) _peerParty[id] = party;
            if (!_names.ContainsKey(id) && name.Contains("#")) SetName(id, name);
        }

        void SetName(ulong id, string name)
        {
            bool isNew = !_names.TryGetValue(id, out var old) || old != name;
            _names[id] = name;
            if (!isNew) return;
            KnownPlayers.Add(name);
            OnMetPlayer?.Invoke(name);
            PublishNames();
            OnPartyChanged?.Invoke();
        }

        void MakeMaster(PlayerRef p)
        {
            try
            {
                if (_setMaster == null) _setMaster = typeof(NetworkRunner).GetMethod("SetMasterClient", Any, null, new[] { typeof(PlayerRef) }, null);
                _setMaster?.Invoke(_runner, new object[] { p });
            }
            catch { }
        }

        /// <summary>Put a player in the session on our list (from the session's player list or from a packet they sent).</summary>
        void NotePeer(PlayerRef p)
        {
            if (_runner == null || p == _runner.LocalPlayer || p.PlayerId < 0) return;
            ulong id = Id(p);
            _refs[id] = p;
            if (!_uidRead.Contains(id)) LearnFromUserId(id, p);   // once per player, not on every packet (it's a slow lookup)
            if (_peers.Contains(id)) return;
            _peers.Add(id);
            _peerSince[id] = Time.realtimeSinceStartup;
            Log("player #" + id + " here" + (_names.TryGetValue(id, out var n) ? " (" + n + ")" : ""));
            if (IsHost) SendSys(id, 2, "host"); else if (_hostId == 0) SendSys(id, 13, "?");
            SendSys(id, 3, MyName());
            if (_partyCode != null && !_inPartySession) SendSys(id, 4, _partyCode);
            OnPeerJoined?.Invoke(id);
            if (_inPartySession) { _status = "Party (" + (_peers.Count + 1) + ")"; OnPartyChanged?.Invoke(); }
        }

        // =========================================================================================== data
        // Packets: [0xA5][sender u64][channel]...  channel 1 = system [code][utf8 text], channel 2 = game messages
        // [len u16][msg]..., 3 = "please pass this to [dest u64]" (to the host), 4 = "passed on from [src u64]".
        static readonly ReliableKey GameKey = ReliableKey.FromInts(7, 1, 0, 0);
        const byte Stamp = 0xA5;
        readonly Dictionary<ulong, System.IO.MemoryStream> _out = new Dictionary<ulong, System.IO.MemoryStream>();
        float _rtt, _pingOut = -1f, _flushT, _lastSendWarn;
        public float Rtt => !InSession || _peers.Count == 0 ? 0f : Mathf.Max(_rtt, _pingOut >= 0f ? Time.realtimeSinceStartup - _pingOut : 0f);
        public void FlushNow() { _flushT = 0f; }
        int _oversize; bool _oversizeLogged;

        public void Send(byte[] data, bool reliable, ulong target = ulong.MaxValue)
        {
            if (!InSession || data == null || data.Length == 0 || data.Length > 60000) return;
            if (!reliable && target == ulong.MaxValue && FastBroadcast && data.Length <= FastChunk - 3)
            {
                if (_fastOut.Length + data.Length + 2 > FastChunk) FlushFast();
                if (_fastOut.Length == 0) _fastOut.WriteByte(2);
                _fastOut.WriteByte((byte)(data.Length & 0xff)); _fastOut.WriteByte((byte)(data.Length >> 8));
                _fastOut.Write(data, 0, data.Length);
                return;
            }
            if (!reliable && target == ulong.MaxValue && FastBroadcast)
            {
                // too big for the fast lane: it goes the slow way, a copy per player - count it so the report shows it
                _oversize++;
                if (!_oversizeLogged) { _oversizeLogged = true; Log("a " + data.Length + "-byte update (type " + data[0] + ") was too big for the fast lane", true); }
            }
            if (target != ulong.MaxValue && target == LocalId) return;
            // small and important: a reliable Photon RPC, batched (one copy for everyone, or marked for one player)
            if (RelRpc && data.Length <= RelChunk - 3) { RelAppend(target == ulong.MaxValue ? 0UL : target, data); return; }
            if (target == ulong.MaxValue) { foreach (var p in _peers) Queue(p, data); }
            else Queue(target, data);
        }

        // =========================================================================================== reliable RPC lane
        // The small important messages (shots, hits, names, pings, items) go as reliable Photon RPCs. Photon's "reliable
        // data" (SendReliableDataToPlayer) is made for big blobs and rations its sends: used for every message, with 3
        // players they queued for over a second. Only messages too big for an RPC still use it. Payload:
        // [0xA5][sender u64][for u64, 0 = everyone][channel...]. A heartbeat (the pings) proves the lane works.
        const int RelChunk = 220;
        static MethodInfo _relSend; static bool _relLooked, _relBroken;
        readonly Dictionary<ulong, System.IO.MemoryStream> _relOut = new Dictionary<ulong, System.IO.MemoryStream>();
        readonly Dictionary<ulong, float> _relAt = new Dictionary<ulong, float>();
        float _relHealthT;

        bool RelRpc
        {
            get
            {
                if (_relBroken || !InSession) return false;
                if (!_relLooked)
                {
                    _relLooked = true;
                    var t = Type.GetType("Rago.Net.RagoRpc, RagoExtract.Fusion") ?? typeof(FusionBackend).Assembly.GetType("Rago.Net.RagoRpc");
                    _relSend = t?.GetMethod("SendRel", BindingFlags.Public | BindingFlags.Static);
                    Log(_relSend != null ? "reliable RPC lane available" : "reliable RPC lane missing - using reliable data");
                }
                return _relSend != null;
            }
        }

        void RelAppend(ulong dest, byte[] data)
        {
            if (!_relOut.TryGetValue(dest, out var ms)) _relOut[dest] = ms = new System.IO.MemoryStream(256);
            if (ms.Length + data.Length + 2 > RelChunk) RelFlush(dest, ms);
            if (ms.Length == 0) ms.WriteByte(2);
            ms.WriteByte((byte)(data.Length & 0xff)); ms.WriteByte((byte)(data.Length >> 8));
            ms.Write(data, 0, data.Length);
        }

        void RelFlush(ulong dest, System.IO.MemoryStream ms)
        {
            if (ms.Length == 0) return;
            var body = ms.ToArray(); ms.SetLength(0);
            if (dest != 0 && !_peers.Contains(dest)) return;
            RelRaw(dest, body);
        }

        /// <summary>One reliable RPC: [stamp][me][for][packet]. False if the lane failed (then it's switched off).</summary>
        bool RelRaw(ulong dest, byte[] packet)
        {
            if (_runner == null || _relSend == null) return false;
            var w = new byte[packet.Length + 17];
            w[0] = Stamp; Buffer.BlockCopy(BitConverter.GetBytes(LocalId), 0, w, 1, 8); Buffer.BlockCopy(BitConverter.GetBytes(dest), 0, w, 9, 8);
            Buffer.BlockCopy(packet, 0, w, 17, packet.Length);
            try { _relSend.Invoke(null, new object[] { _runner, Convert.ToBase64String(w) }); return true; }
            catch (Exception e)
            {
                _relBroken = true;
                Log("reliable RPC failed (" + (e.InnerException ?? e).Message + ") - using reliable data", true);
                return false;
            }
        }

        public static void OnRelData(NetworkRunner runner, string payload)
        {
            if (runner == null || !_byRunner.TryGetValue(runner, out var b) || b == null || b._runner != runner || string.IsNullOrEmpty(payload)) return;
            byte[] data;
            try { data = Convert.FromBase64String(payload); } catch { return; }
            if (data.Length < 18 || data[0] != Stamp) return;
            ulong from = BitConverter.ToUInt64(data, 1), dest = BitConverter.ToUInt64(data, 9);
            if (from == b.LocalId) return;
            if (!b._peers.Contains(from)) foreach (var ap in runner.ActivePlayers) if (Id(ap) == from) b.NotePeer(ap);
            if (!b._peers.Contains(from)) return;
            float now = Time.realtimeSinceStartup;
            b._relAt[from] = now; b._lastHeard[from] = now;
            if (dest != 0 && dest != b.LocalId) return;   // for someone else
            if (b._relayTo.Remove(from)) Log("player #" + from + " reachable again");
            b.Process(from, new ArraySegment<byte>(data, 17, data.Length - 17));
        }

        /// <summary>Everyone pings everyone every 2 s on this lane: players we hear (any way) whose pings never come on it
        /// for 10 s mean it doesn't work here - switch it off and use reliable data.</summary>
        void RelHealth()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _relHealthT) return;
            _relHealthT = now + 1f;
            int checkedPeers = 0, silent = 0;
            foreach (var p in _peers)
            {
                if (!_peerSince.TryGetValue(p, out var since) || now - since < 12f) continue;
                if (!_lastHeard.TryGetValue(p, out var h) || now - h > 3f) continue;
                checkedPeers++;
                if (!_relAt.TryGetValue(p, out var f) || now - f > 10f) silent++;
            }
            if (checkedPeers > 0 && silent == checkedPeers)
            {
                _relBroken = true; _relOut.Clear();
                Log("reliable RPCs: nothing arriving from " + silent + " player(s) - using reliable data", true);
            }
        }

        void Queue(ulong id, byte[] data)
        {
            if (!_out.TryGetValue(id, out var ms)) _out[id] = ms = new System.IO.MemoryStream(512);
            if (ms.Length == 0) ms.WriteByte(2);
            ms.WriteByte((byte)(data.Length & 0xff)); ms.WriteByte((byte)(data.Length >> 8));
            ms.Write(data, 0, data.Length);
            if (ms.Length > 16000) { SendTo(id, ms.ToArray()); ms.SetLength(0); }
        }

        void FlushAll()
        {
            foreach (var kv in _relOut) if (kv.Value.Length > 0) RelFlush(kv.Key, kv.Value);
            foreach (var kv in _out) if (kv.Value.Length > 0) { if (_peers.Contains(kv.Key)) SendTo(kv.Key, kv.Value.ToArray()); kv.Value.SetLength(0); }
        }

        void SendSys(ulong to, byte code, string text)
        {
            if (code == 3 && Spectator.Active) return;   // spectating: stay out of their lists
            var t = System.Text.Encoding.UTF8.GetBytes(text ?? "");
            var packet = new byte[t.Length + 2];
            packet[0] = 1; packet[1] = code;
            Buffer.BlockCopy(t, 0, packet, 2, t.Length);
            SendTo(to, packet);
        }

        void SendTo(ulong id, byte[] packet)
        {
            if (_runner == null || !_refs.TryGetValue(id, out var p)) return;
            if (packet.Length <= RelChunk && RelRpc && RelRaw(id, packet)) return;   // small: straight out as a reliable RPC
            // nothing heard from them for a while: send it through the host instead (it passes it on). Never both - a
            // shot or a hit arriving twice would count twice. Pings still go direct, so a working link is noticed.
            bool ping = packet.Length >= 2 && packet[0] == 1 && (packet[1] == 11 || packet[1] == 12);
            if (!ping && _relayTo.Contains(id) && !IsHost && _hostId != 0 && _hostId != id && _refs.TryGetValue(_hostId, out var hp))
            {
                var w = new byte[packet.Length + 9];
                w[0] = 3; Buffer.BlockCopy(BitConverter.GetBytes(id), 0, w, 1, 8);
                Buffer.BlockCopy(packet, 0, w, 9, packet.Length);
                Raw(hp, w);
                return;
            }
            Raw(p, packet);
        }

        void Raw(PlayerRef to, byte[] packet)
        {
            if (_runner == null) return;
            var w = new byte[packet.Length + 9];
            w[0] = Stamp; Buffer.BlockCopy(BitConverter.GetBytes(LocalId), 0, w, 1, 8);
            Buffer.BlockCopy(packet, 0, w, 9, packet.Length);
            try { _runner.SendReliableDataToPlayer(to, GameKey, w); }
            catch (Exception e) { if (Time.realtimeSinceStartup - _lastSendWarn > 10f) { _lastSendWarn = Time.realtimeSinceStartup; Log("send failed: " + e.Message, true); } }
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> span)
        {
            if (runner != _runner) return;
            var data = span.ToArray();
            if (data.Length < 10 || data[0] != Stamp) return;
            NotePeer(player);
            ulong from = BitConverter.ToUInt64(data, 1);   // the packet says who sent it (Photon's own label can be wrong)
            if (from == LocalId) return;
            if (!_peers.Contains(from)) foreach (var ap in _runner.ActivePlayers) if (Id(ap) == from) NotePeer(ap);
            _lastHeard[from] = Time.realtimeSinceStartup;
            if (_relayTo.Remove(from)) Log("player #" + from + " reachable again");
            var body = new ArraySegment<byte>(data, 9, data.Length - 9);
            byte ch = data[9];
            if (ch == 3 && body.Count >= 9)
            {
                // host: pass it on to the player it's for
                ulong dest = BitConverter.ToUInt64(data, 10);
                var inner = new ArraySegment<byte>(data, 18, data.Length - 18);
                if (dest == LocalId) { Process(from, inner); return; }
                if (_refs.TryGetValue(dest, out var dp))
                {
                    var w = new byte[inner.Count + 9];
                    w[0] = 4; Buffer.BlockCopy(BitConverter.GetBytes(from), 0, w, 1, 8);
                    Buffer.BlockCopy(data, 18, w, 9, inner.Count);
                    Raw(dp, w);
                }
                return;
            }
            if (ch == 4 && body.Count >= 9)
            {
                ulong src = BitConverter.ToUInt64(data, 10);
                if (src == LocalId) return;
                _lastHeard[src] = Time.realtimeSinceStartup;
                Process(src, new ArraySegment<byte>(data, 18, data.Length - 18));
                return;
            }
            Process(from, body);
        }

        void Process(ulong fromId, ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count < 1) return;
            byte channel = data.Array[data.Offset];
            if (channel == 1) { if (data.Count >= 2) OnSys(fromId, data.Array[data.Offset + 1], System.Text.Encoding.UTF8.GetString(data.Array, data.Offset + 2, data.Count - 2)); return; }
            if (channel != 2) return;
            int i = data.Offset + 1, end = data.Offset + data.Count;
            while (i + 2 <= end)
            {
                int len = data.Array[i] | (data.Array[i + 1] << 8); i += 2;
                if (len <= 0 || i + len > end) break;
                var msg = new byte[len];
                Buffer.BlockCopy(data.Array, i, msg, 0, len); i += len;
                try { OnData?.Invoke(fromId, msg); } catch (Exception e) { Debug.LogWarning("[Rago] packet handler: " + e.Message); }
            }
        }

        // system codes: 1 go | 2 I'm the host | 3 my callsign | 4 my party | 5 leader cancelled | 6 follow me into a raid
        // | 7 please invite | 8 invite | 9 who are you? | 10 ok | 11 ping | 12 pong | 13 who's the host?
        void OnSys(ulong from, byte code, string text)
        {
            switch (code)
            {
                case 1:
                {
                    var p = text.Split('|');
                    if (p.Length >= 4 && int.TryParse(p[1], out int seed) && int.TryParse(p[2], out int sp) && int.TryParse(p[3], out int n))
                        _pendingStart = new RaidStart { map = p[0], seed = seed, spawnIndex = sp, players = n, online = true, partySlot = p.Length >= 5 && int.TryParse(p[4], out int sl) ? sl : 0, night = _queueNight };
                    break;
                }
                case 2: _hostId = from; break;
                case 3: SetName(from, text); break;
                case 4: _peerParty[from] = text; break;
                case 5: if (_queueing && text == _partyCode) { _cancel = true; OnPartyQueueCancelled?.Invoke(); } break;
                case 6:
                {
                    SendSys(from, 10, "ok");
                    if (!_inPartySession || _queueing) break;
                    var p = text.Split('|');
                    if (p.Length >= 3 && p[1] != _pendingRaidSession && p[1] != _lastRaid) FollowLeader(p[0], p[1], p[2], p.Length >= 4 && p[3] == "1");
                    break;
                }
                case 7: if (_inPartySession && IsHost) { Remember(text); PublishInvites(); } break;
                case 8:
                {
                    var p = text.Split('|');
                    if (p.Length >= 2 && !_invites.Exists(x => Key(x.party) == Key(p[1])) && Key(p[1]) != Key(_partyCode))
                    {
                        _invites.Add(new PartyInvite { from = p[0], party = p[1], time = Time.realtimeSinceStartup });
                        Log("invite from " + p[0]);
                        RagoGame.I?.Player?.Toast(p[0] + " invited you to their party - press the menu button to accept");
                        OnPartyChanged?.Invoke();
                    }
                    break;
                }
                case 9: SendSys(from, 3, MyName()); if (_partyCode != null && !_inPartySession) SendSys(from, 4, _partyCode); break;
                case 10: _acks.Add(from); break;
                case 11: SendSys(from, 12, text); break;
                case 12:
                    if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sentAt))
                    {
                        float rtt = Time.realtimeSinceStartup - sentAt;
                        _rtt = rtt > _rtt ? rtt : Mathf.Lerp(_rtt, rtt, 0.3f);
                        _pingOut = -1f;
                    }
                    break;
                case 13: if (IsHost) SendSys(from, 2, "host"); break;
            }
        }

        void FollowLeader(string map, string session, string size, bool night)
        {
            _lastRaid = session; _pendingRaidSession = session; int.TryParse(size, out _pendingPartySize); _pendingNight = night; _wasLeader = false;
            Log("leader queued for " + map);
            OnPartyQueue?.Invoke(map);
        }

        // =========================================================================================== fast lane
        // Frequent, replaceable updates (movement, voice, raiders): ONE unreliable Photon RPC to everyone (RagoRpc, found
        // by name so deleting that file just turns this off). A heartbeat every 2 s proves it works; if players we hear
        // fine on the reliable channel send nothing on it for 10 s, it's switched off and everything goes reliable.
        const int FastChunk = 220;
        readonly System.IO.MemoryStream _fastOut = new System.IO.MemoryStream(512);
        static MethodInfo _fastSend; static bool _fastLooked, _fastBroken;
        static readonly Dictionary<NetworkRunner, FusionBackend> _byRunner = new Dictionary<NetworkRunner, FusionBackend>();
        readonly Dictionary<ulong, float> _fastAt = new Dictionary<ulong, float>();
        float _lastFast, _beatT, _healthT;

        public bool FastBroadcast
        {
            get
            {
                if (_fastBroken || !InSession) return false;
                if (!_fastLooked)
                {
                    _fastLooked = true;
                    var t = Type.GetType("Rago.Net.RagoRpc, RagoExtract.Fusion") ?? typeof(FusionBackend).Assembly.GetType("Rago.Net.RagoRpc");
                    _fastSend = t?.GetMethod("Send", BindingFlags.Public | BindingFlags.Static);
                    Log(_fastSend != null ? "fast lane available" : "fast lane missing (RagoRpc) - all reliable");
                }
                return _fastSend != null;
            }
        }

        bool FastInvoke(byte[] w)
        {
            try { _fastSend.Invoke(null, new object[] { _runner, Convert.ToBase64String(w) }); return true; }
            catch (Exception e) { _fastBroken = true; _fastOut.SetLength(0); Log("fast lane failed (" + (e.InnerException ?? e).Message + ") - all reliable now", true); return false; }
        }

        void FlushFast()
        {
            if (_fastOut.Length == 0) return;
            var body = _fastOut.ToArray(); _fastOut.SetLength(0);
            if (_runner == null || _fastSend == null || _fastBroken) return;
            var w = new byte[body.Length + 9];
            w[0] = Stamp; Buffer.BlockCopy(BitConverter.GetBytes(LocalId), 0, w, 1, 8);
            Buffer.BlockCopy(body, 0, w, 9, body.Length);
            FastInvoke(w);
        }

        public static void OnFastData(NetworkRunner runner, string payload)
        {
            if (runner == null || !_byRunner.TryGetValue(runner, out var b) || b == null || b._runner != runner || string.IsNullOrEmpty(payload)) return;
            byte[] data;
            try { data = Convert.FromBase64String(payload); } catch { return; }
            if (data.Length < 10 || data[0] != Stamp) return;
            ulong from = BitConverter.ToUInt64(data, 1);
            if (from == b.LocalId) return;
            if (!b._peers.Contains(from)) foreach (var ap in runner.ActivePlayers) if (Id(ap) == from) b.NotePeer(ap);
            if (!b._peers.Contains(from)) return;
            float now = Time.realtimeSinceStartup;
            b._fastAt[from] = now; b._lastHeard[from] = now;
            if (data.Length == 10) return;   // heartbeat
            b.Process(from, new ArraySegment<byte>(data, 9, data.Length - 9));
        }

        void FastHealth()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= _beatT)
            {
                _beatT = now + 2f;
                var w = new byte[10]; w[0] = Stamp; Buffer.BlockCopy(BitConverter.GetBytes(LocalId), 0, w, 1, 8); w[9] = 0xBE;
                if (!FastInvoke(w)) return;
            }
            if (now < _healthT) return;
            _healthT = now + 1f;
            int checkedPeers = 0, silent = 0;
            foreach (var p in _peers)
            {
                if (!_peerSince.TryGetValue(p, out var since) || now - since < 12f) continue;
                if (!_lastHeard.TryGetValue(p, out var h) || now - h > 3f) continue;
                checkedPeers++;
                if (!_fastAt.TryGetValue(p, out var f) || now - f > 10f) silent++;
            }
            if (checkedPeers > 0 && silent == checkedPeers)
            {
                _fastBroken = true; _fastOut.SetLength(0);
                Log("fast lane: nothing arriving from " + silent + " player(s) - all reliable now", true);
            }
        }

        // =========================================================================================== frame
        float _peerScan, _pingT, _announce, _lobbyT;

        void Update()
        {
            // idle (not in any session): sit in the lobby so invites can reach you
            _lobbyT -= Time.unscaledDeltaTime;
            if (_lobbyT <= 0f && _ready)
            {
                _lobbyT = 3f;
                if (_runner == null && !_queueing && !_starting && _lobby == null && !_pausedAway) _ = EnterLobby();
            }
            if (!InSession) return;
            float now = Time.realtimeSinceStartup;
            _peerScan -= Time.unscaledDeltaTime;
            if (_peerScan <= 0f)
            {
                _peerScan = 0.5f;
                try { foreach (var p in _runner.ActivePlayers) NotePeer(p); } catch { }
                // the party session's host should be the leader
                if (_inPartySession && IsHost && _partyCode != null && Key(_partyCode) != Key(MyName()))
                    foreach (var kv in _names) if (Key(kv.Value) == Key(_partyCode) && _refs.TryGetValue(kv.Key, out var lr)) { MakeMaster(lr); break; }
                // the leader queued: follow (read from the party session too, not only the message)
                var props = _runner.SessionInfo.Properties;
                if (_inPartySession && !_queueing && _pendingRaidSession == null && props != null && props.TryGetValue("raid", out var rv))
                {
                    var rp = Str(rv).Split('|');
                    if (rp.Length >= 5 && rp[1] != _lastRaid && long.TryParse(rp[4], out var when) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - when < 25)
                        FollowLeader(rp[0], rp[1], rp[2], rp[3] == "1");
                }
                // keep asking anyone we still don't have a name (or party) for - a message sent on join can be lost
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
                string ts = now.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var p in _peers) SendSys(p, 11, ts);
                if (_pingOut < 0f) _pingOut = now;
                // a player we haven't heard from for 4 s: also send to them through the host
                foreach (var p in _peers)
                {
                    if (p == _hostId || IsHost) continue;
                    float heard = _lastHeard.TryGetValue(p, out var h) ? h : (_peerSince.TryGetValue(p, out var s) ? s : now);
                    if (now - heard > 4f && _relayTo.Add(p)) Log("nothing from #" + p + " for 4 s - sending through the host");
                }
            }
            // the host runs the raiders: tell everyone who that is
            if (IsHost)
            {
                _announce -= Time.unscaledDeltaTime;
                if (_announce <= 0f) { _announce = 1f; _hostId = LocalId; foreach (var p in _peers) SendSys(p, 2, "host"); }
            }
        }

        void LateUpdate()
        {
            if (!InSession) { _fastOut.SetLength(0); _out.Clear(); _relOut.Clear(); return; }
            if (_peers.Count > 0 && FastBroadcast) FastHealth();
            if (_peers.Count > 0 && RelRpc) RelHealth();
            // the fast lane goes out the frame it's written (up to 30 a second)
            if (_fastOut.Length > 0 && Time.unscaledTime - _lastFast >= 1f / 30f) { _lastFast = Time.unscaledTime; FlushFast(); }
            // reliable batches: 15 a second (10 when the line is backing up)
            _flushT -= Time.unscaledDeltaTime;
            if (_flushT > 0f) return;
            _flushT = Rtt > 0.3f ? 0.1f : 1f / 15f;
            FlushAll();
        }

        // =========================================================================================== callbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner != _runner) return;
            NotePeer(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner != _runner) return;
            ulong id = Id(player);
            if (!_peers.Remove(id)) return;
            Log("player #" + id + " left" + (_names.TryGetValue(id, out var n) ? " (" + n + ")" : ""));
            _refs.Remove(id); _names.Remove(id); _relayTo.Remove(id); _out.Remove(id); _uidRead.Remove(id);
            if (id == _hostId)
            {
                _hostId = 0;
                if (IsHost) { _hostId = LocalId; foreach (var p in _peers) SendSys(p, 2, "host"); }
                else foreach (var p in _peers) SendSys(p, 13, "?");
            }
            PublishNames();
            OnPeerLeft?.Invoke(id);
            if (_inPartySession) { _status = "Party (" + (_peers.Count + 1) + ")"; OnPartyChanged?.Invoke(); }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (runner == _lobby) { _lobby = null; Log("lobby closed: " + reason); return; }
            if (_leaving || runner != _runner) return;
            _status = "Disconnected (" + Explain(reason.ToString()) + ")";
            Log("session ended: " + reason + " (" + _peers.Count + " others, rtt " + (_rtt * 1000f).ToString("0") + "ms)", true);
            _peers.Clear();
            if (_inPartySession) { _inPartySession = false; OnPartyChanged?.Invoke(); }
            OnDisconnected?.Invoke();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (_leaving || runner != _runner) return;
            _status = "Disconnected (" + reason + ")";
            Log("disconnected from Photon: " + reason, true);
            OnDisconnected?.Invoke();
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { _status = "Connect failed: " + Explain(reason.ToString()); Log("connect failed: " + reason, true); }

        /// <summary>Photon's reasons in plain words (the raid terminal shows the status).</summary>
        static string Explain(string reason)
        {
            if (reason == null) return "error";
            if (reason.Contains("MaxCcu") || reason.Contains("CCU")) return "PLAYER LIMIT REACHED - your Photon plan's max players online are all connected (check the Photon dashboard; close other copies of the game / the Unity editor)";
            if (reason.Contains("GameIsFull")) return "that session is full";
            if (reason.Contains("GameNotFound")) return "that session doesn't exist any more";
            if (reason.Contains("InvalidAuthentication") || reason.Contains("AppId")) return "the Photon App ID is wrong";
            if (reason.Contains("Timeout") || reason.Contains("TimedOut")) return "Photon didn't answer (internet connection?)";
            return reason;
        }
        public void OnConnectedToServer(NetworkRunner runner) { }
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

        void OnApplicationQuit() { _ = Shutdown(); _ = LeaveLobby(); }

        // Headset off / asleep: let go of Photon while we're only idling (the lobby, or a party in the bunker) - every
        // connection counts towards the Photon plan's player limit, and a sleeping headset kept its slot. A raid or the
        // Market is kept (you'd lose your place). Back on: the lobby / party come back by themselves.
        bool _pausedAway;
        async void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                if (_queueing || (InSession && !_inPartySession)) return;
                _pausedAway = true;
                Log("headset asleep - disconnected from Photon to free the slot");
                await LeaveLobby();
                if (_inPartySession) { _inPartySession = false; await Shutdown(); }
            }
            else if (_pausedAway)
            {
                _pausedAway = false;
                Log("headset back - reconnecting");
                RejoinParty();
            }
        }
    }
}
#endif
