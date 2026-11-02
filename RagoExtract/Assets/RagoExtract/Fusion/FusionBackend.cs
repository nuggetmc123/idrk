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
        // A party is its own small Photon session ("rago_party_CODE") that members sit in while they're in their
        // bunkers. When the leader queues, everyone is told the name of a fresh raid session to join, so the party
        // lands in the same raid (random players can still fill the free spots). Back in the bunker, everyone
        // rejoins the party session automatically.
        string _partyCode;
        bool _inPartySession, _wasLeader;
        readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, string> _peerParty = new Dictionary<ulong, string>();
        readonly List<string> _members = new List<string>();
        string _pendingRaidSession; int _pendingPartySize = 1; int _queuePartySize = 1;
        public string PartyCode => _partyCode;
        public bool PartyLeader => _partyCode != null && (_inPartySession ? IsHost : _wasLeader);
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
        public event Action OnPartyChanged;
        public event Action<string> OnPartyQueue;
        public event Action OnPartyQueueCancelled;

        static string MyName() => Profile.Current != null ? Profile.Current.DisplayName : "Operator";
        static string PartySession(string code) => "rago_party_" + code;
        const string CodeChars = "ACDEFHJKMNPRTUVWXY3479";

        public async void CreateParty(Action<bool> done)
        {
            if (!_ready || _queueing) { done?.Invoke(false); return; }
            var r = new System.Random();
            string code = "";
            for (int i = 0; i < 4; i++) code += CodeChars[r.Next(CodeChars.Length)];
            bool ok = await EnterParty(code, true);
            done?.Invoke(ok);
        }

        public async void JoinParty(string code, Action<bool> done)
        {
            if (!_ready || _queueing || string.IsNullOrEmpty(code)) { done?.Invoke(false); return; }
            bool ok = await EnterParty(code.ToUpperInvariant(), false);
            done?.Invoke(ok);
        }

        public async void LeaveParty()
        {
            _partyCode = null; _wasLeader = false;
            if (_inPartySession) { _inPartySession = false; await Shutdown(); }
            _status = _ready ? "Online (Photon)" : _status;
            OnPartyChanged?.Invoke();
        }

        public async void RejoinParty()
        {
            if (_partyCode == null || !_ready || _queueing || InSession) return;
            await EnterParty(_partyCode, true);
        }

        async Task<bool> EnterParty(string code, bool create)
        {
            _status = (create ? "Starting" : "Joining") + " party " + code + "...";
            _names.Clear();
            bool ok = await StartSession(PartySession(code), new Dictionary<string, SessionProperty> { { "kind", "party" } }, QueueRules.Max, create);
            if (ok && !create && _peers.Count == 0)
            {
                // nobody there: the code is wrong (or the party broke up)
                ok = false;
                await Shutdown();
                _status = "No party with code " + code;
            }
            if (!ok) { OnPartyChanged?.Invoke(); return false; }   // keeps the code you had (a rejoin can be retried)
            _partyCode = code; _inPartySession = true;
            _wasLeader = IsHost;
            _status = "Party " + code;
            foreach (var p in _peers) SendSys(p, 3, MyName());
            OnPartyChanged?.Invoke();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var go = new GameObject("RagoFusion");
            DontDestroyOnLoad(go);
            NetHub.Backend = go.AddComponent<FusionBackend>();
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
            if (string.IsNullOrEmpty(appId)) return;   // fall back to whatever is in PhotonAppSettings
            try
            {
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("Fusion.Photon.Realtime.PhotonAppSettings", false); if (t != null) break; }
                if (t == null) { Debug.LogWarning("[Rago] PhotonAppSettings not found; using the project's Photon settings"); return; }
                var global = Get(null, t, "Global");
                var settings = Get(global, t, "AppSettings");
                if (settings == null) return;
                var copy = settings.GetType().GetMethod("GetCopy", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)?.Invoke(settings, null) ?? settings;
                if (!Set(copy, "AppIdFusion", appId)) { Debug.LogWarning("[Rago] couldn't set AppIdFusion; using the project's Photon settings"); return; }
                object boxed = args;
                if (Set(boxed, "CustomPhotonAppSettings", copy)) args = (StartGameArgs)boxed;
            }
            catch (Exception e) { Debug.LogWarning("[Rago] couldn't apply the Photon App ID (using the project's Photon settings): " + e.Message); }
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
            _inPartySession = false;
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
            foreach (var p in _runner.ActivePlayers)
            {
                _refs[Id(p)] = p;
                if (p != _runner.LocalPlayer) _peers.Add(Id(p));
            }
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
            _status = ok ? "Market (" + (_peers.Count + 1) + " players)" : _status;
            done?.Invoke(ok);
        }

        public async void QueueRaid(string map, Action<QueueStatus> status, Action<RaidStart> start, Action<string> failed)
        {
            if (!_ready) { failed?.Invoke("offline"); return; }
            if (_queueing) return;
            _queueing = true; _cancel = false; _pendingStart = null; _raidMap = map;
            try
            {
                status?.Invoke(new QueueStatus { players = 1, max = QueueRules.Max, text = "Finding a raid..." });
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
                bool ok = await StartSession(sessionName, new Dictionary<string, SessionProperty> { { "kind", "raid_" + map } }, QueueRules.Max);
                _peerParty.Clear();
                if (ok && _partyCode != null) foreach (var p in _peers) SendSys(p, 4, _partyCode);
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
                    elapsed = Mathf.Max(elapsed, Time.realtimeSinceStartup - queuedAt);
                    int players = _peers.Count + 1;
                    if (players >= QueueRules.Target) { if (atTarget < 0) atTarget = Time.realtimeSinceStartup; } else atTarget = -1;
                    status?.Invoke(new QueueStatus { players = players, max = QueueRules.Max, target = QueueRules.Target, elapsed = elapsed, maxWait = QueueRules.MaxWait, text = QueueRules.Describe(players, elapsed) });
                    if (IsHost && QueueRules.ShouldStart(players, elapsed, atTarget < 0 ? 0 : Time.realtimeSinceStartup - atTarget, _queuePartySize))
                    {
                        try { _runner.SessionInfo.IsOpen = false; _runner.SessionInfo.IsVisible = false; }
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
                    await Task.Delay(1000);
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
                else if (code == 3) { _names[Id(player)] = text; OnPartyChanged?.Invoke(); }                 // party member's callsign
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
            RefreshPeers();
            if (player != runner.LocalPlayer)
            {
                if (_inPartySession) SendSys(Id(player), 3, MyName());
                else if (_queueing && _partyCode != null) SendSys(Id(player), 4, _partyCode);
                OnPeerJoined?.Invoke(Id(player));
            }
            if (_inPartySession) OnPartyChanged?.Invoke();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            RefreshPeers();
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
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }

        void Update()
        {
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
