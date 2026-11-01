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
        async Task<bool> StartSession(string name, Dictionary<string, SessionProperty> props, int maxPlayers)
        {
            await Shutdown();
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
                // random join of an open queue for this map, or create one
                bool ok = await StartSession(null, new Dictionary<string, SessionProperty> { { "kind", "raid_" + map } }, QueueRules.Max);
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
                    if (IsHost && QueueRules.ShouldStart(players, elapsed, atTarget < 0 ? 0 : Time.realtimeSinceStartup - atTarget))
                    {
                        try { _runner.SessionInfo.IsOpen = false; _runner.SessionInfo.IsVisible = false; }
                        catch (Exception e) { Debug.LogWarning("[Rago] couldn't close the raid session: " + e.Message); }
                        int seed = props != null && props.TryGetValue("seed", out var sp) ? (int)sp : UnityEngine.Random.Range(1, 999999);
                        _nextSpawn = 1;
                        foreach (var p in _peers) SendSys(p, 1, map + "|" + seed + "|" + (_nextSpawn++ % 10) + "|" + players);
                        _queueing = false;
                        _status = "Raid (" + players + " players)";
                        start?.Invoke(new RaidStart { map = map, seed = seed, spawnIndex = 0, players = players, online = true });
                        return;
                    }
                    await Task.Delay(1000);
                }
                if (_cancel) await Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rago] queue failed: " + e);
                failed?.Invoke(e.Message);
                await Shutdown();
            }
            _queueing = false;
        }

        public void CancelQueue() { _cancel = true; }
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
                    if (p.Length >= 4) _pendingStart = new RaidStart { map = p[0], seed = int.Parse(p[1]), spawnIndex = int.Parse(p[2]), players = int.Parse(p[3]), online = true };
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
            if (player != runner.LocalPlayer) OnPeerJoined?.Invoke(Id(player));
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            RefreshPeers();
            OnPeerLeft?.Invoke(Id(player));
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (_leaving || runner != _runner) return;
            _status = "Disconnected (" + shutdownReason + ")";
            _peers.Clear();
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
