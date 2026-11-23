using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rago.Relay;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Online play through our OWN game server (Server: RagoServer, on idrk.dev). Used instead of Photon whenever
    /// Resources/Rago/server.txt holds an address ("idrk.dev" or "idrk.dev:27015"); empty or missing = Photon as before.
    ///
    ///  * One connection to the server for the whole time the game runs. Being connected is the "lobby": invites reach you
    ///    straight away, and the server says how many players are online. No player limit.
    ///  * Rooms are what Photon called sessions: "rago_party_&lt;leader&gt;", "RagoMarket", raid queues
    ///    "rago_q_&lt;map&gt;[_n]_&lt;slot&gt;", all ending in the game version. The server picks each room's host (first in,
    ///    handed on when they leave), keeps the room's properties, and passes messages between the players in it.
    ///  * Events (shots, hits, items, trades) go over TCP (reliable, in order); movement, voice and raiders go over UDP
    ///    (the server copies each packet to everyone in the room). If UDP doesn't get through, they go over TCP too.
    ///  * The game's own messages and the system codes (go, host, callsign, party, follow-the-leader...) are the same as
    ///    with Photon, so everything above this file works unchanged.
    /// </summary>
    public class RelayBackend : MonoBehaviour, INetBackend
    {
        // =========================================================================================== config
        static string _addr; static bool _addrRead;
        public static string Address
        {
            get
            {
                if (_addrRead) return _addr;
                _addrRead = true;
                try
                {
                    var t = Resources.Load<TextAsset>("Rago/server");
                    var s = t != null ? t.text.Trim() : "";
                    foreach (var line in s.Split('\n')) { var l = line.Trim(); if (l.Length > 0 && !l.StartsWith("#")) { _addr = l; break; } }
                }
                catch { _addr = null; }
                return _addr;
            }
        }
        /// <summary>Our own server is set up (Resources/Rago/server.txt): Photon stays out of the way.</summary>
        public static bool Configured => !string.IsNullOrEmpty(Address);

        public static RelayBackend Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (!Configured) return;
            var go = new GameObject("RagoRelay");
            DontDestroyOnLoad(go);
            NetHub.Backend = Instance = go.AddComponent<RelayBackend>();
        }

        // ---- identity: a random key made once per install, and a hash of the device id (bans match either)
        static string PlayerKey
        {
            get
            {
                var k = PlayerPrefs.GetString("rago_player_key", "");
                if (k.Length < 16) { k = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("rago_player_key", k); PlayerPrefs.Save(); }
                return k;
            }
        }
        static string DeviceId
        {
            get
            {
                try
                {
                    var id = SystemInfo.deviceUniqueIdentifier;
                    if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier) return "";
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("rago:" + id))).Replace("-", "").Substring(0, 32);
                }
                catch { return ""; }
            }
        }

        // ---- moderation (MOD tab)
        public static bool IsMod { get; private set; }
        public static bool Banned { get; private set; }
        public static string LastModReply = "";
        Action<List<string>> _searchCb; int _searchId;
        /// <summary>Callsigns matching what's typed (online players first, then anyone who's played here).</summary>
        public void Search(string q, Action<List<string>> cb)
        {
            if (_link == null || !_link.Connected || string.IsNullOrWhiteSpace(q)) { cb?.Invoke(new List<string>()); return; }
            _searchCb = cb;
            _link.Send(new W(P.Search).Str(q.Trim()).I32(++_searchId).Frame());
        }
        /// <summary>A moderator's ban / unban / wipe / Market kick: goes to the owner for approval.</summary>
        public void ModRequest(byte act, string target, string reason, int hours)
        {
            if (_link == null || !_link.Connected) { LastModReply = "Not connected to the server."; return; }
            _link.Send(new W(P.ModReq).U8(act).Str(target ?? "").Str(reason ?? "").I32(hours).Frame());
            LastModReply = "Sending...";
        }

        static void HostPort(out string host, out int port)
        {
            host = Address.Trim(); port = P.Port;
            int c = host.LastIndexOf(':');
            if (c > 0 && host.IndexOf(':') == c && int.TryParse(host.Substring(c + 1), out var p)) { port = p; host = host.Substring(0, c); }
        }

        // =========================================================================================== state
        RelayLink _link;
        string _status = "Offline";
        bool _ready, _wantOnline;
        float _retryT;
        string _room;                       // the room we're in, or null
        string _sessionKind = "";
        readonly Dictionary<string, string> _props = new Dictionary<string, string>();
        readonly List<ulong> _peers = new List<ulong>();
        readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, string> _peerParty = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, float> _lastHeard = new Dictionary<ulong, float>();
        ulong _hostId;
        int _online = -1;

        public string Status => _status;
        public bool Ready => _ready;
        public bool InSession => _room != null && _link != null && _link.Connected;
        public bool IsHost => InSession && _hostId == LocalId;
        public ulong LocalId => _link != null ? _link.Id : 0;
        public ulong HostId => _hostId;
        public string SessionId => _room ?? "";
        public IReadOnlyList<ulong> Peers => _peers;
        public bool FastBroadcast => InSession;   // one copy to the server, it copies to everyone
        public event Action<ulong, byte[]> OnData;
        public event Action<ulong> OnPeerJoined;
        public event Action<ulong> OnPeerLeft;
        public event Action OnDisconnected;
        public event Action OnPartyChanged;
        public event Action<string> OnPartyQueue;
        public event Action OnPartyQueueCancelled;
        public event Action<string> OnMetPlayer;

        static string MyName() => Profile.Current != null ? Profile.Current.DisplayName : "Operator";
        static string Key(string callsign) => (callsign ?? "").Trim().ToLowerInvariant();
        static string Ver { get { try { return "_v" + PatchNotes.All[0].version.Replace('.', '_'); } catch { return ""; } } }
        static int Fnv(string s) { unchecked { int h = (int)2166136261; foreach (char c in s ?? "") h = (h ^ c) * 16777619; return h & 0x7fffffff; } }

        // =========================================================================================== log
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
                sb.Append("NETWORK  v").Append(Ver.Replace("_v", "").Replace('_', '.')).Append("  server ").Append(Address).Append('\n');
                if (AntiCheat.Fingerprint.Length > 0) sb.Append("build signature ").Append(AntiCheat.Fingerprint.Substring(0, 16)).Append("...").Append(AntiCheat.Tampered ? "  MODIFIED" : "").Append('\n');
                sb.Append(_status).Append(_link != null && _link.Connected ? (_link.UdpOk ? "  UDP ok" : "  UDP blocked (all TCP)") : "").Append('\n');
                if (InSession)
                {
                    sb.Append("room ").Append(_room).Append(" (").Append(_sessionKind).Append(")\n");
                    sb.Append("me #").Append(LocalId).Append(IsHost ? "  HOST" : "  host #" + _hostId).Append("  rtt ").Append((_rtt * 1000f).ToString("0")).Append("ms  server ").Append((_serverRtt * 1000f).ToString("0")).Append("ms\n");
                    foreach (var p in _peers)
                        sb.Append(" #").Append(p).Append(' ').Append(_names.TryGetValue(p, out var n) ? n : "?")
                          .Append("  heard ").Append(_lastHeard.TryGetValue(p, out var h) ? (now - h).ToString("0.0") + "s" : "never").Append('\n');
                }
                else sb.Append(_link != null && _link.Connected ? "connected (invites can reach you)\n" : "not connected\n");
                if (_partyCode != null) sb.Append("party ").Append(_partyCode).Append(_inPartySession ? " (together)" : " (apart)").Append('\n');
                if (_link != null) sb.Append("sent ").Append(_link.SentBytes / 1024).Append(" KB  got ").Append(_link.ReceivedBytes / 1024).Append(" KB\n");
                sb.Append("-- log --\n");
                foreach (var l in _log) sb.Append(l).Append('\n');
                return sb.ToString();
            }
        }

        // =========================================================================================== connection
        public void Init(Action<bool> done)
        {
            if (AntiCheat.Tampered) { _status = "Online play is off: this copy of the game has been modified (" + AntiCheat.Why + ")"; _ready = false; Log(_status, true); done?.Invoke(false); return; }
            _wantOnline = true;
            Connect();
            done?.Invoke(true);   // "online" from the start: the connection comes up in the background
        }

        void Connect()
        {
            if (_link != null) { _link.Close(); _link = null; }
            HostPort(out var host, out var port);
            _status = "Connecting to " + host + "...";
            _ready = true;
            _link = new RelayLink();
            _helloName = MyName();
            _link.Connect(host, port, _helloName, Ver, PlayerKey, DeviceId);
            Log("connecting to " + host + ":" + port);
        }

        void Update()
        {
            if (_link == null) return;
            // what arrived from the server
            int n = 0;
            while (n++ < 2000 && _link.Inbox.TryDequeue(out var m))
            {
                try { Handle(m); } catch (Exception e) { Debug.LogWarning("[Rago] server message " + m.Type + ": " + e.Message); }
            }
            float now = Time.realtimeSinceStartup;
            // lost the server (or never reached it): try again every 5 s
            if (!_link.Connected)
            {
                if (_link.Failed && _wantOnline && !Banned && now > _retryT)
                {
                    if (_retryT > 0f) Log("server: " + _link.Error, true);
                    _status = "Can't reach the game server (" + _link.Error + ") - retrying";
                    _retryT = now + 5f;
                    Connect();
                }
                return;
            }
            _keepT -= Time.unscaledDeltaTime;
            if (_keepT <= 0f)
            {
                _keepT = 1f; _link.KeepAlive(); _link.Send(new W(P.Ping).F32(now).Frame());
                // the profile (callsign) was picked after we connected: tell the server, so invites by callsign find us
                if (MyName() != _helloName) { _helloName = MyName(); _link.Send(new W(P.Hello).Str(_helloName).Str(Ver).Str(PlayerKey).Str(DeviceId).Frame()); }
            }
            if (!InSession) return;
            Tick(now);
        }
        float _keepT, _serverRtt;
        string _helloName;

        void Handle(RelayLink.Msg m)
        {
            var r = new R(m.Body);
            switch (m.Type)
            {
                case P.Welcome:
                    r.U64(); r.U32(); _online = r.I32();
                    _status = "Online (Rago server)";
                    _retryT = 0f;
                    Log("connected to the game server as #" + _link.Id + " (" + _online + " online)");
                    if (_partyCode != null && _room == null) RejoinParty();
                    break;
                case P.Online: _online = r.I32(); break;
                case P.Role:
                    IsMod = r.U8() == 1;
                    Log(IsMod ? "you're a moderator (MOD tab in the menu)" : "moderator role removed");
                    if (IsMod) RagoGame.I?.Player?.Toast("You're a moderator - the MOD tab is in your menu");
                    break;
                case P.Banned:
                {
                    string why = r.Str();
                    Banned = true; _wantOnline = false;
                    _status = "BANNED from online play: " + why;
                    Log(_status, true);
                    RagoGame.I?.Player?.Hud?.Center("BANNED FROM ONLINE PLAY\n<size=34>" + why + "</size>", 8f);
                    break;
                }
                case P.Wipe:
                {
                    Log("your progress was wiped by the server owner", true);
                    Profile.Current?.Wipe();
                    RagoGame.I?.Player?.Hud?.Center("YOUR PROGRESS WAS WIPED\n<size=34>by the server owner</size>", 6f);
                    if (RagoGame.I != null && (RagoGame.I.Area == Area.Bunker || RagoGame.I.Area == Area.Market)) { RagoGame.SkipHubSave = true; RagoGame.I.GoTo(Area.Bunker); }   // reload the (now empty) bunker without saving the old one back
                    break;
                }
                case P.Kicked:
                {
                    string why = r.Str();
                    Log("kicked: " + why, true);
                    ClearRoom();
                    RagoGame.I?.Player?.Toast("Kicked from the Market: " + why);
                    if (RagoGame.I != null && RagoGame.I.Area == Area.Market) RagoGame.I.GoTo(Area.Bunker);
                    break;
                }
                case P.ModReply: LastModReply = r.Str(); RagoGame.I?.Player?.Toast(LastModReply); break;
                case P.Found:
                {
                    int id = r.I32(); int cnt = r.U16(); var names = new List<string>();
                    for (int i = 0; i < cnt; i++) names.Add(r.Str());
                    if (id == _searchId) { var cb = _searchCb; _searchCb = null; cb?.Invoke(names); }
                    break;
                }
                case P.Pong: { float sent = r.F32(); _serverRtt = Mathf.Max(0f, Time.realtimeSinceStartup - sent); break; }
                case P.Joined:
                {
                    string room = r.Str(); ulong host = r.U64(); var props = r.Dict(); int cnt = r.U16();
                    _room = room; _hostId = host;
                    _props.Clear(); foreach (var kv in props) _props[kv.Key] = kv.Value;
                    _sessionKind = _props.TryGetValue("kind", out var k) ? k : "";
                    for (int i = 0; i < cnt; i++) { ulong id = r.U64(); string nm = r.Str(); AddPeer(id, nm); }
                    Log("joined " + room + " (" + (_peers.Count + 1) + " here" + (IsHost ? ", host" : "") + ")");
                    _join?.TrySetResult(true); _join = null;
                    break;
                }
                case P.JoinFail:
                {
                    string room = r.Str(), why = r.Str();
                    Log("couldn't join " + room + ": " + why, why != "not found");
                    _join?.TrySetResult(false); _join = null;
                    break;
                }
                case P.PeerJoin: { ulong id = r.U64(); string nm = r.Str(); if (_room != null) AddPeer(id, nm); break; }
                case P.PeerLeft: RemovePeer(r.U64()); break;
                case P.Host:
                    _hostId = r.U64();
                    if (IsHost) foreach (var p in _peers) SendSys(p, 2, "host");
                    break;
                case P.Props:
                {
                    string room = r.Str(); var d = r.Dict();
                    if (room != _room) break;
                    _props.Clear(); foreach (var kv in d) _props[kv.Key] = kv.Value;
                    break;
                }
                case P.DataIn: { ulong from = r.U64(); var data = r.Bytes(); Got(from, data); break; }
                case P.FastIn:
                {
                    if (m.From != 0) { Got(m.From, m.Body); break; }   // over UDP: the body is the data
                    ulong from = r.U64(); var data = r.Bytes(); Got(from, data); break;
                }
                case P.Rooms:
                {
                    int id = r.I32(); int cnt = r.U16();
                    var list = new List<RoomInfo>();
                    for (int i = 0; i < cnt; i++) list.Add(new RoomInfo { name = r.Str(), players = r.U8(), max = r.U8(), props = r.Dict() });
                    if (_lists.TryGetValue(id, out var tcs)) { _lists.Remove(id); tcs.TrySetResult(list); }
                    break;
                }
                case P.WhisperIn:
                {
                    string fromName = r.Str(); r.U64(); var data = r.Bytes();
                    OnWhisper(fromName, System.Text.Encoding.UTF8.GetString(data));
                    break;
                }
                case P.NoSuch:
                {
                    string who = r.Str();
                    RagoGame.I?.Player?.Toast(who + " isn't online right now - try again when they are");
                    Log(who + " isn't online");
                    break;
                }
                case P.Bye:
                {
                    Log("lost the game server: " + _link.Error, true);
                    _status = "Disconnected from the game server";
                    bool was = _room != null;
                    ClearRoom();
                    _join?.TrySetResult(false); _join = null;
                    foreach (var kv in _lists) kv.Value.TrySetResult(new List<RoomInfo>());
                    _lists.Clear();
                    if (_inPartySession) { _inPartySession = false; OnPartyChanged?.Invoke(); }
                    if (was) OnDisconnected?.Invoke();
                    _retryT = Time.realtimeSinceStartup + 2f;
                    break;
                }
            }
        }

        void AddPeer(ulong id, string name)
        {
            if (id == LocalId || _peers.Contains(id)) return;
            _peers.Add(id);
            _lastHeard[id] = Time.realtimeSinceStartup;
            if (!string.IsNullOrEmpty(name) && name != "?") SetName(id, name);
            Log("player #" + id + " here (" + name + ")");
            if (IsHost) SendSys(id, 2, "host");
            if (_partyCode != null && !_inPartySession) SendSys(id, 4, _partyCode);
            OnPeerJoined?.Invoke(id);
            if (_inPartySession) { _status = "Party (" + (_peers.Count + 1) + ")"; OnPartyChanged?.Invoke(); }
        }

        void RemovePeer(ulong id)
        {
            if (!_peers.Remove(id)) return;
            Log("player #" + id + " left" + (_names.TryGetValue(id, out var n) ? " (" + n + ")" : ""));
            _names.Remove(id); _peerParty.Remove(id); _lastHeard.Remove(id); _out.Remove(id);
            OnPeerLeft?.Invoke(id);
            if (_inPartySession) { _status = "Party (" + (_peers.Count + 1) + ")"; OnPartyChanged?.Invoke(); }
        }

        void SetName(ulong id, string name)
        {
            bool isNew = !_names.TryGetValue(id, out var old) || old != name;
            _names[id] = name;
            if (!isNew) return;
            KnownPlayers.Add(name);
            OnMetPlayer?.Invoke(name);
            OnPartyChanged?.Invoke();
        }

        void ClearRoom()
        {
            _room = null; _props.Clear(); _peers.Clear(); _names.Clear(); _peerParty.Clear(); _lastHeard.Clear();
            _out.Clear(); _fastOut.SetLength(0); _hostId = 0; _rtt = 0f; _pingOut = -1f; _acks.Clear();
        }

        // =========================================================================================== rooms
        TaskCompletionSource<bool> _join;
        bool _starting;
        public class RoomInfo { public string name; public int players, max; public Dictionary<string, string> props; }
        readonly Dictionary<int, TaskCompletionSource<List<RoomInfo>>> _lists = new Dictionary<int, TaskCompletionSource<List<RoomInfo>>>();
        int _listN;

        async Task<bool> WaitConnected(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while ((_link == null || !_link.Connected) && Time.realtimeSinceStartup < until) await Task.Delay(100);
            return _link != null && _link.Connected;
        }

        /// <summary>Leave the room we're in (if any) and join / create this one.</summary>
        async Task<bool> StartSession(string name, Dictionary<string, string> props, int max, bool allowCreate = true)
        {
            _starting = true;
            try
            {
                if (!await WaitConnected(8f)) { _status = "Can't reach the game server"; return false; }
                Shutdown();
                _inPartySession = false;
                _join = new TaskCompletionSource<bool>();
                var tcs = _join;
                _link.Send(new W(P.Join).Str(name).U8((byte)Mathf.Clamp(max, 1, 255)).Bool(allowCreate).Dict(props).Frame());
                var done = await Task.WhenAny(tcs.Task, Task.Delay(8000));
                if (done != tcs.Task) { _join = null; Log("no answer joining " + name, true); return false; }
                return tcs.Task.Result && _room == name;
            }
            finally { _starting = false; }
        }

        void Shutdown()
        {
            if (_room == null) return;
            FlushAll();
            if (_link != null && _link.Connected) _link.Send(new W(P.Leave).Frame());
            ClearRoom();
        }

        async Task<List<RoomInfo>> ListRooms(string prefix)
        {
            if (_link == null || !_link.Connected) return new List<RoomInfo>();
            int id = ++_listN;
            var tcs = new TaskCompletionSource<List<RoomInfo>>();
            _lists[id] = tcs;
            _link.Send(new W(P.List).Str(prefix).I32(id).Frame());
            var done = await Task.WhenAny(tcs.Task, Task.Delay(4000));
            if (done != tcs.Task) { _lists.Remove(id); return new List<RoomInfo>(); }
            return tcs.Task.Result;
        }

        void SetProps(Dictionary<string, string> d)
        {
            if (!InSession) return;
            foreach (var kv in d) { if (string.IsNullOrEmpty(kv.Value)) _props.Remove(kv.Key); else _props[kv.Key] = kv.Value; }
            _link.Send(new W(P.SetProps).Dict(d).Frame());
        }

        string Prop(string k) => _props.TryGetValue(k, out var v) ? v : null;

        public async void JoinMarket(Action<bool> done)
        {
            _status = "Joining the Market...";
            bool ok = await StartSession("RagoMarket" + Ver, new Dictionary<string, string> { { "kind", "market" } }, 32);
            _status = ok ? "Market (" + (_peers.Count + 1) + " players)" : "Couldn't join the Market";
            done?.Invoke(ok);
        }

        public void Leave()
        {
            if (InSession) Log("left " + _room);
            Shutdown();
            _inPartySession = false;
            if (_link != null && _link.Connected) _status = "Online (Rago server)";
        }

        public int OnlineCount => _online > 0 ? _online : (InSession ? _peers.Count + 1 : -1);

        // =========================================================================================== raids
        RaidStart _pendingStart;
        bool _queueing, _cancel, _queueNight;
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

        /// <summary>The first queue room (from this number on) that hasn't launched and has room for us.</summary>
        async Task<string> ChooseSlot(string map, bool night, int partySize, int from)
        {
            var rooms = await ListRooms("rago_q_" + map + (night ? "_n" : "") + "_");
            var byName = new Dictionary<string, RoomInfo>();
            foreach (var ri in rooms) byName[ri.name] = ri;
            for (int i = from; i < from + 60; i++)
            {
                string n = SlotName(map, night, i);
                if (!byName.TryGetValue(n, out var ri)) return n;
                bool started = ri.props != null && (ri.props.ContainsKey("go") || (ri.props.TryGetValue("kind", out var k) && k.StartsWith("raid_started")));
                if (!started && ri.players + partySize <= QueueRules.MaxFor(map)) return n;
            }
            return SlotName(map, night, from);
        }

        static int Now32 => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000);

        public async void QueueRaid(string map, Action<QueueStatus> status, Action<RaidStart> start, Action<string> failed)
        {
            if (_queueing) return;
            if (_link == null || !_link.Connected) { failed?.Invoke("Can't reach the game server"); return; }
            _queueing = true; _cancel = false; _pendingStart = null;
            float pressedAt = Time.realtimeSinceStartup;
            try
            {
                status?.Invoke(new QueueStatus { players = 1, max = QueueRules.MaxFor(map), text = "Finding a raid..." });
                string sessionName = null; _queuePartySize = 1;
                bool night = RaidChoice.Night;
                if (_pendingRaidSession != null)
                {
                    sessionName = _pendingRaidSession; _queuePartySize = _pendingPartySize; _pendingRaidSession = null;
                    night = _pendingNight; RaidChoice.Night = night;
                    Log("following the leader into " + sessionName);
                }
                else if (_partyCode != null && _inPartySession)
                {
                    if (!PartyLeader) { failed?.Invoke("Only the party leader can start the queue"); _queueing = false; return; }
                    _wasLeader = true;
                    _queuePartySize = _peers.Count + 1;
                    sessionName = await ChooseSlot(map, night, _queuePartySize, 1);
                    string go6 = map + "|" + sessionName + "|" + _queuePartySize + "|" + (night ? 1 : 0);
                    // tell the party: on the party room itself (can't get lost) and by message, until each one answers
                    SetProps(new Dictionary<string, string> { { "raid", go6 + "|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() } });
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
                if (sessionName == null) sessionName = await ChooseSlot(map, night, _queuePartySize, 1);
                for (int attempt = 0; attempt < 12 && !_cancel; attempt++)
                {
                    ok = await StartSession(sessionName, new Dictionary<string, string> { { "kind", "raid_" + map + (night ? "_n" : "") } }, QueueRules.MaxFor(map));
                    bool launched = ok && (Prop("go") != null || (Prop("kind") ?? "").StartsWith("raid_started"));
                    bool full = ok && _queuePartySize > 1 && _wasLeader && _peers.Count + _queuePartySize > QueueRules.MaxFor(map);
                    if (ok && !launched && !full) break;
                    if (ok) { Log(sessionName + (launched ? " already launched" : " too full for the party") + " - trying the next"); Shutdown(); ok = false; }
                    sessionName = SlotName(map, night, SlotOf(sessionName) + 1);
                }
                if (!ok) { failed?.Invoke(_status); _queueing = false; RejoinParty(); return; }
                if (_partyCode != null) foreach (var p in _peers) SendSys(p, 4, _partyCode);
                float atTarget = -1f, queuedAt = Time.realtimeSinceStartup;
                while (!_cancel && InSession)
                {
                    // the host said go (message) or put "go" on the room (property - can't get lost)
                    if (_pendingStart == null && !IsHost && Prop("go") != null && int.TryParse(Prop("seed"), out var gs))
                        _pendingStart = new RaidStart { map = map, seed = gs, spawnIndex = -1, players = _peers.Count + 1, online = true, night = night };
                    if (_pendingStart != null)
                    {
                        var rs = _pendingStart; _pendingStart = null;
                        if (rs.spawnIndex < 0) { SpawnFor(MyName(), out int sp0, out int sl0); rs.spawnIndex = sp0; rs.partySlot = sl0; }
                        Launch(map, rs, start);
                        return;
                    }
                    if (IsHost && Prop("t0") == null)
                        SetProps(new Dictionary<string, string> { { "kind", "raid_" + map + (night ? "_n" : "") }, { "t0", Now32.ToString() }, { "seed", UnityEngine.Random.Range(1, 999999).ToString() } });
                    int t0 = int.TryParse(Prop("t0"), out var tp) ? tp : Now32;
                    float elapsed = Now32 - t0;
                    elapsed = Mathf.Max(elapsed, Time.realtimeSinceStartup - queuedAt, Time.realtimeSinceStartup - pressedAt);
                    int players = _peers.Count + 1;
                    if (players >= QueueRules.TargetFor(map)) { if (atTarget < 0) atTarget = Time.realtimeSinceStartup; } else atTarget = -1;
                    status?.Invoke(new QueueStatus { players = players, max = QueueRules.MaxFor(map), target = QueueRules.TargetFor(map), elapsed = elapsed, maxWait = QueueRules.MaxWait, text = QueueRules.Describe(players, elapsed, map) });
                    if (IsHost && QueueRules.ShouldStart(players, elapsed, atTarget < 0 ? 0 : Time.realtimeSinceStartup - atTarget, _queuePartySize, map))
                    {
                        int seed = int.TryParse(Prop("seed"), out var sp) ? sp : UnityEngine.Random.Range(1, 999999);
                        SetProps(new Dictionary<string, string> { { "seed", seed.ToString() }, { "go", players.ToString() }, { "kind", "raid_started_" + map }, { "map", map }, { "night", night ? "1" : "0" } });
                        foreach (var p in _peers) SendSys(p, 1, map + "|" + seed + "|-1|" + players + "|0");
                        FlushAll();
                        SpawnFor(MyName(), out int mySpot, out int mySlot);
                        Launch(map, new RaidStart { map = map, seed = seed, spawnIndex = mySpot, players = players, online = true, partySlot = mySlot, night = night }, start);
                        return;
                    }
                    // the host's go never came: don't sit past 1:00 - go in with this room's seed
                    if (!IsHost && elapsed >= QueueRules.MaxWait + 4f)
                    {
                        int seed = int.TryParse(Prop("seed"), out var sp2) ? sp2 : Fnv(_room) & 0x7fffff;
                        SpawnFor(MyName(), out int sp1, out int sl1);
                        Log("no go from the host - starting anyway");
                        Launch(map, new RaidStart { map = map, seed = seed, spawnIndex = sp1, players = players, online = true, partySlot = sl1, night = night }, start);
                        return;
                    }
                    await Task.Delay(500);
                }
                if (_cancel) { Log("queue cancelled"); Shutdown(); }
                else Log("lost the queue room", true);
            }
            catch (Exception e)
            {
                Log("queue failed: " + e.Message, true);
                failed?.Invoke(e.Message);
                Shutdown();
            }
            _queueing = false;
            RejoinParty();
        }

        void Launch(string map, RaidStart rs, Action<RaidStart> start)
        {
            _queueing = false;
            _status = "Raid (" + rs.players + " players)";
            _lastRaidSession = _room; _lastRaidMap = map; _lastRaidAt = Time.realtimeSinceStartup;
            Log("raid starting: " + rs.players + " players, spawn " + rs.spawnIndex % 1000 + "/" + rs.partySlot);
            start?.Invoke(rs);
        }

        public void CancelQueue()
        {
            if (_partyCode != null && _wasLeader && InSession)
                foreach (var p in _peers) if (_peerParty.TryGetValue(p, out var pc) && pc == _partyCode) SendSys(p, 5, _partyCode);
            FlushAll();
            _cancel = true;
        }

        /// <summary>Everyone's spawn worked out the same way on every machine (same as with Photon).</summary>
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
                ok = await StartSession(_lastRaidSession, new Dictionary<string, string>(), QueueRules.MaxFor(_lastRaidMap), false);
            }
            if (ok && _peers.Count == 0) { ok = false; Log("nobody left in that raid"); Shutdown(); }
            if (ok && _partyCode != null) foreach (var p in _peers) SendSys(p, 4, _partyCode);
            _status = ok ? "Raid (reconnected)" : "Online (Rago server)";
            if (!ok) _lastRaidSession = null;
            done?.Invoke(ok);
            if (!ok) RejoinParty();
        }

        // =========================================================================================== party
        // A party is a small room "rago_party_<leader>" the members sit in while in their bunkers. Invites go straight to
        // the player through the server (by callsign) wherever they are; inside a raid or the Market they're also told
        // which party the people around them are in.
        string _partyCode;
        bool _inPartySession, _wasLeader;
        readonly List<string> _members = new List<string>();
        readonly List<string> _roster = new List<string>();
        readonly List<string> _sessionPlayers = new List<string>();
        readonly List<PartyInvite> _invites = new List<PartyInvite>();
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
                return IsHost;   // the leader isn't here: the room's host leads
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
                    else { _roster.Clear(); foreach (var p in _peers) if (_names.TryGetValue(p, out var n2)) _roster.Add(n2); }
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

        void Whisper(string to, string text) { if (_link != null && _link.Connected) _link.Send(new W(P.Whisper).Str(to).Bytes(System.Text.Encoding.UTF8.GetBytes(text)).Frame()); }

        void OnWhisper(string from, string text)
        {
            var p = text.Split('|');
            if (p.Length >= 3 && p[0] == "inv")
            {
                string party = p[2];
                if (_invites.Exists(x => Key(x.party) == Key(party)) || Key(party) == Key(_partyCode)) return;
                _invites.Add(new PartyInvite { from = from, party = party, time = Time.realtimeSinceStartup });
                KnownPlayers.Add(from);
                Log("invite from " + from);
                RagoGame.I?.Player?.Toast(from + " invited you to their party - press the menu button to accept");
                OnPartyChanged?.Invoke();
            }
        }

        public async void InviteToParty(string callsign)
        {
            if (!_ready || string.IsNullOrWhiteSpace(callsign) || Key(callsign) == Key(MyName())) return;
            if (_partyCode != null && PartyMembers.Count >= QueueRules.PartyMax) { RagoGame.I?.Player?.Toast("Your party is full"); return; }
            if (_partyCode == null)
            {
                if (InSession && !_inPartySession) { _partyCode = MyName(); _wasLeader = true; OnPartyChanged?.Invoke(); }   // in a raid / the Market: the party room opens back home
                else if (!await EnterParty(MyName(), true)) return;
            }
            else if (!_inPartySession && !InSession) { if (!await EnterParty(_partyCode, true)) return; }
            Whisper(callsign.Trim(), "inv|" + MyName() + "|" + _partyCode);
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
            OnPartyChanged?.Invoke();
        }

        public void DeclineInvite(PartyInvite inv) { _invites.Remove(inv); OnPartyChanged?.Invoke(); }

        public void LeaveParty()
        {
            Log("left the party");
            _partyCode = null; _wasLeader = false; _roster.Clear();
            if (_inPartySession) { _inPartySession = false; Shutdown(); }
            if (_link != null && _link.Connected) _status = "Online (Rago server)";
            OnPartyChanged?.Invoke();
        }

        public async void RejoinParty()
        {
            if (!_ready || _queueing || _starting) return;
            if (InSession && !_inPartySession) return;   // in the Market / a raid: wait until you're back home
            if (_partyCode != null && !_inPartySession) await EnterParty(_partyCode, true);
        }

        async Task<bool> EnterParty(string id, bool create)
        {
            _status = (create ? "Starting" : "Joining") + " party...";
            bool ok = await StartSession(PartySession(id), new Dictionary<string, string> { { "kind", "party" }, { "lead", id } }, QueueRules.PartyMax, create);
            if (ok && !create && _peers.Count == 0) { ok = false; Log("that party is empty"); Shutdown(); }
            if (!ok) { if (_link != null && _link.Connected) _status = "Online (Rago server)"; OnPartyChanged?.Invoke(); return false; }
            _partyCode = id; _inPartySession = true;
            _wasLeader = Key(id) == Key(MyName()) || IsHost;
            _status = "Party (" + (_peers.Count + 1) + ")";
            OnPartyChanged?.Invoke();
            return true;
        }

        void FollowLeader(string map, string session, string size, bool night)
        {
            _lastRaid = session; _pendingRaidSession = session; int.TryParse(size, out _pendingPartySize); _pendingNight = night; _wasLeader = false;
            Log("leader queued for " + map);
            OnPartyQueue?.Invoke(map);
        }

        // =========================================================================================== data
        // Packets: [channel]...  1 = system [code][utf8 text], 2 = game messages [len u16][msg]... The server stamps
        // who sent each one.
        readonly Dictionary<ulong, System.IO.MemoryStream> _out = new Dictionary<ulong, System.IO.MemoryStream>();   // 0 = everyone
        readonly System.IO.MemoryStream _fastOut = new System.IO.MemoryStream(1200);
        float _rtt, _pingOut = -1f, _flushT, _lastFast;
        public float Rtt => !InSession || _peers.Count == 0 ? 0f : Mathf.Max(_rtt, _pingOut >= 0f ? Time.realtimeSinceStartup - _pingOut : 0f);
        public void FlushNow() { _flushT = 0f; }

        public void Send(byte[] data, bool reliable, ulong target = ulong.MaxValue)
        {
            if (!InSession || data == null || data.Length == 0 || data.Length > 60000) return;
            if (target != ulong.MaxValue && target == LocalId) return;
            if (!reliable && target == ulong.MaxValue && data.Length <= P.MaxUdp - 3)
            {
                if (_fastOut.Length + data.Length + 2 > P.MaxUdp) FlushFast();
                if (_fastOut.Length == 0) _fastOut.WriteByte(2);
                _fastOut.WriteByte((byte)(data.Length & 0xff)); _fastOut.WriteByte((byte)(data.Length >> 8));
                _fastOut.Write(data, 0, data.Length);
                return;
            }
            Queue(target == ulong.MaxValue ? 0UL : target, data);
        }

        void Queue(ulong dest, byte[] data)
        {
            if (!_out.TryGetValue(dest, out var ms)) _out[dest] = ms = new System.IO.MemoryStream(512);
            if (ms.Length == 0) ms.WriteByte(2);
            ms.WriteByte((byte)(data.Length & 0xff)); ms.WriteByte((byte)(data.Length >> 8));
            ms.Write(data, 0, data.Length);
            if (ms.Length > 16000) { SendData(dest, ms.ToArray()); ms.SetLength(0); }
        }

        void FlushFast()
        {
            if (_fastOut.Length == 0 || _link == null) return;
            var body = _fastOut.ToArray(); _fastOut.SetLength(0);
            _link.SendFast(body, body.Length);
        }

        void FlushAll()
        {
            FlushFast();
            foreach (var kv in _out) if (kv.Value.Length > 0) { if (kv.Key == 0 || _peers.Contains(kv.Key)) SendData(kv.Key, kv.Value.ToArray()); kv.Value.SetLength(0); }
        }

        void SendData(ulong dest, byte[] packet)
        {
            if (_link == null || !_link.Connected || _room == null) return;
            _link.Send(new W(P.Data).U64(dest).Bytes(packet).Frame());
        }

        void SendSys(ulong to, byte code, string text)
        {
            if (code == 3 && Spectator.Active) return;
            var t = System.Text.Encoding.UTF8.GetBytes(text ?? "");
            var packet = new byte[t.Length + 2];
            packet[0] = 1; packet[1] = code;
            Buffer.BlockCopy(t, 0, packet, 2, t.Length);
            SendData(to, packet);
        }

        void Got(ulong from, byte[] data)
        {
            if (from == LocalId || data == null || data.Length < 1 || !_peers.Contains(from)) return;
            _lastHeard[from] = Time.realtimeSinceStartup;
            byte channel = data[0];
            if (channel == 1) { if (data.Length >= 2) OnSys(from, data[1], System.Text.Encoding.UTF8.GetString(data, 2, data.Length - 2)); return; }
            if (channel != 2) return;
            int i = 1, end = data.Length;
            while (i + 2 <= end)
            {
                int len = data[i] | (data[i + 1] << 8); i += 2;
                if (len <= 0 || i + len > end) break;
                var msg = new byte[len];
                Buffer.BlockCopy(data, i, msg, 0, len); i += len;
                try { OnData?.Invoke(from, msg); } catch (Exception e) { Debug.LogWarning("[Rago] packet handler: " + e.Message); }
            }
        }

        // system codes (as with Photon): 1 go | 2 I'm the host | 3 my callsign | 4 my party | 5 leader cancelled
        // | 6 follow me into a raid | 9 who are you? | 10 ok | 11 ping | 12 pong
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
                case 2: break;   // the server says who the host is
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
            }
        }

        // =========================================================================================== frame
        float _scanT, _pingT, _hostAskT;

        void Tick(float now)
        {
            _scanT -= Time.unscaledDeltaTime;
            if (_scanT <= 0f)
            {
                _scanT = 0.5f;
                // the party room's host should be the leader
                if (_inPartySession && _partyCode != null && Key(_partyCode) == Key(MyName()) && !IsHost && now > _hostAskT)
                { _hostAskT = now + 3f; _link.Send(new W(P.SetHost).U64(LocalId).Frame()); }
                // the leader queued: follow (read from the party room too, not only the message)
                var raid = Prop("raid");
                if (_inPartySession && !_queueing && _pendingRaidSession == null && raid != null)
                {
                    var rp = raid.Split('|');
                    if (rp.Length >= 5 && rp[1] != _lastRaid && long.TryParse(rp[4], out var when) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - when < 25)
                        FollowLeader(rp[0], rp[1], rp[2], rp[3] == "1");
                }
                // in a raid / the Market, tell everyone which party we're in (a message can arrive before they know us)
                if (_partyCode != null && !_inPartySession)
                    foreach (var p in _peers) if (!_peerParty.ContainsKey(p) || !_names.ContainsKey(p)) { SendSys(p, 4, _partyCode); SendSys(p, 9, "?"); }
            }
            _pingT -= Time.unscaledDeltaTime;
            if (_pingT <= 0f && _peers.Count > 0)
            {
                _pingT = 2f;
                string ts = now.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                SendSys(0, 11, ts);
                if (_pingOut < 0f) _pingOut = now;
            }
        }

        void LateUpdate()
        {
            if (!InSession) { _fastOut.SetLength(0); _out.Clear(); return; }
            // the fast lane goes out up to 30 times a second
            if (_fastOut.Length > 0 && Time.unscaledTime - _lastFast >= 1f / 30f) { _lastFast = Time.unscaledTime; FlushFast(); }
            // reliable batches: 30 a second (TCP keeps them in order and never loses them)
            _flushT -= Time.unscaledDeltaTime;
            if (_flushT > 0f) return;
            _flushT = 1f / 30f;
            FlushAll();
        }

        void OnApplicationQuit() { _wantOnline = false; _link?.Close(); }
    }
}
