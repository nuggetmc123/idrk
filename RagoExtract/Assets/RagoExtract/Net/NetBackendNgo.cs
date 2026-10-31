#if RAGO_NGO
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Rago.Net
{
    /// <summary>
    /// Online backend: Unity Gaming Services (Authentication + Lobby + Relay) for matchmaking and NAT-free
    /// connections, Netcode for GameObjects (named messages) as the transport. Registers itself into
    /// NetHub when the packages are installed (see the "Rago Extract" editor tab).
    ///
    /// Market: a public lobby ("market") everybody quick-joins; the first player hosts it over Relay.
    /// Raids: queue lobbies per map. The lobby owner runs QueueRules (4-10 players, aim for 8, launch at 1:00),
    /// then hosts the raid over Relay and publishes the join code; queued players connect automatically.
    /// </summary>
    public class NetBackendNgo : MonoBehaviour, INetBackend
    {
        const string MsgGame = "rago_g", MsgSys = "rago_s";
        NetworkManager _nm;
        UnityTransport _ut;
        Lobby _lobby;
        string _status = "Offline";
        bool _ready, _inSession, _queueing, _cancel;
        float _heartbeat;
        string _sessionId = "";
        readonly List<ulong> _peers = new List<ulong>();
        int _nextSpawn = 1;
        RaidStart _pendingStart;
        Action<RaidStart> _onRaidStart;

        public string Status => _status;
        public bool Ready => _ready;
        public bool InSession => _inSession && _nm != null && _nm.IsListening;
        public bool IsHost => _nm != null && _nm.IsServer;
        public ulong LocalId => _nm != null ? _nm.LocalClientId : 0;
        public string SessionId => _sessionId;
        public IReadOnlyList<ulong> Peers => _peers;
        public event Action<ulong, byte[]> OnData;
        public event Action<ulong> OnPeerJoined;
        public event Action<ulong> OnPeerLeft;
        public event Action OnDisconnected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var go = new GameObject("RagoNet");
            DontDestroyOnLoad(go);
            NetHub.Backend = go.AddComponent<NetBackendNgo>();
        }

        // ------------------------------------------------------------------------------------ init
        public async void Init(Action<bool> done)
        {
            try
            {
                _status = "Connecting to Unity Services...";
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                CreateNetworkManager();
                _ready = true;
                _status = "Online";
                done?.Invoke(true);
            }
            catch (Exception e)
            {
                _ready = false;
                _status = "Offline (" + Short(e) + ")";
                Debug.LogWarning("[Rago] Unity Services unavailable - playing offline. Link the project to a Unity Cloud project and enable Lobby + Relay. " + e.Message);
                done?.Invoke(false);
            }
        }

        static string Short(Exception e) { var m = e.Message ?? e.GetType().Name; return m.Length > 60 ? m.Substring(0, 60) + "..." : m; }

        void CreateNetworkManager()
        {
            if (_nm != null) return;
            var go = new GameObject("NetworkManager");
            DontDestroyOnLoad(go);
            _ut = go.AddComponent<UnityTransport>();
            _nm = go.AddComponent<NetworkManager>();
            if (_nm.NetworkConfig == null) _nm.NetworkConfig = new NetworkConfig();
            _nm.NetworkConfig.NetworkTransport = _ut;
            _nm.NetworkConfig.EnableSceneManagement = false;
            _nm.NetworkConfig.ConnectionApproval = false;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // ------------------------------------------------------------------------------------ market
        public async void JoinMarket(Action<bool> done)
        {
            if (!_ready) { done?.Invoke(false); return; }
            try
            {
                await LeaveInternal();
                _status = "Finding the Market...";
                Lobby lobby = null;
                try
                {
                    lobby = await LobbyService.Instance.QuickJoinLobbyAsync(new QuickJoinLobbyOptions
                    {
                        Filter = new List<QueryFilter> { new QueryFilter(QueryFilter.FieldOptions.S1, "market", QueryFilter.OpOptions.EQ) }
                    });
                }
                catch (LobbyServiceException) { lobby = null; }

                if (lobby != null && lobby.Data != null && lobby.Data.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code.Value))
                {
                    _lobby = lobby;
                    bool ok = await ConnectClient(code.Value);
                    if (!ok) { await LeaveLobby(); done?.Invoke(false); return; }
                }
                else
                {
                    if (lobby != null) { _lobby = lobby; await LeaveLobby(); }
                    // nobody is hosting: host the market ourselves
                    var alloc = await RelayService.Instance.CreateAllocationAsync(19);
                    string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                    _ut.SetHostRelayData(alloc.RelayServer.IpV4, (ushort)alloc.RelayServer.Port, alloc.AllocationIdBytes, alloc.Key, alloc.ConnectionData);
                    if (!_nm.StartHost()) throw new Exception("StartHost failed");
                    RegisterHandlers();
                    _lobby = await LobbyService.Instance.CreateLobbyAsync("Rago Market", 20, new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Data = new Dictionary<string, DataObject>
                        {
                            { "kind", new DataObject(DataObject.VisibilityOptions.Public, "market", DataObject.IndexOptions.S1) },
                            { "code", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                        }
                    });
                }
                _sessionId = _lobby.Id;
                _inSession = true;
                _status = "Market (" + (_lobby.Players != null ? _lobby.Players.Count : 1) + " players)";
                done?.Invoke(true);
            }
            catch (Exception e)
            {
                _status = "Market offline (" + Short(e) + ")";
                Debug.LogWarning("[Rago] JoinMarket failed: " + e);
                await LeaveInternal();
                done?.Invoke(false);
            }
        }

        // ------------------------------------------------------------------------------------ raid queue
        public async void QueueRaid(string map, Action<QueueStatus> status, Action<RaidStart> start, Action<string> failed)
        {
            if (!_ready) { failed?.Invoke("offline"); return; }
            if (_queueing) return;
            _queueing = true; _cancel = false;
            _onRaidStart = start;
            string kind = "raid_" + map;
            Lobby queue = null;
            try
            {
                try
                {
                    queue = await LobbyService.Instance.QuickJoinLobbyAsync(new QuickJoinLobbyOptions
                    {
                        Filter = new List<QueryFilter>
                        {
                            new QueryFilter(QueryFilter.FieldOptions.S1, kind, QueryFilter.OpOptions.EQ),
                            new QueryFilter(QueryFilter.FieldOptions.S2, "queue", QueryFilter.OpOptions.EQ),
                            new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                        }
                    });
                }
                catch (LobbyServiceException) { queue = null; }

                if (queue == null)
                {
                    long t0 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    queue = await LobbyService.Instance.CreateLobbyAsync("Rago " + map, QueueRules.Max, new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Data = new Dictionary<string, DataObject>
                        {
                            { "kind", new DataObject(DataObject.VisibilityOptions.Public, kind, DataObject.IndexOptions.S1) },
                            { "state", new DataObject(DataObject.VisibilityOptions.Public, "queue", DataObject.IndexOptions.S2) },
                            { "t0", new DataObject(DataObject.VisibilityOptions.Public, t0.ToString()) },
                            { "code", new DataObject(DataObject.VisibilityOptions.Public, "") },
                            { "seed", new DataObject(DataObject.VisibilityOptions.Public, UnityEngine.Random.Range(1, 999999).ToString()) }
                        }
                    });
                }
                string myId = AuthenticationService.Instance.PlayerId;
                float atTarget = -1f;
                float lastPoll = 0, lastBeat = Time.realtimeSinceStartup;
                while (!_cancel)
                {
                    await Task.Delay(1500);
                    if (_cancel) break;
                    queue = await LobbyService.Instance.GetLobbyAsync(queue.Id);
                    long t0 = long.TryParse(Val(queue, "t0"), out var tt) ? tt : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    float elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - t0;
                    int players = queue.Players.Count;
                    if (players >= QueueRules.Target) { if (atTarget < 0) atTarget = Time.realtimeSinceStartup; } else atTarget = -1;
                    status?.Invoke(new QueueStatus { players = players, max = QueueRules.Max, target = QueueRules.Target, elapsed = elapsed, maxWait = QueueRules.MaxWait, text = QueueRules.Describe(players, elapsed) });
                    bool iAmHost = queue.HostId == myId;
                    if (iAmHost && Time.realtimeSinceStartup - lastBeat > 12f) { lastBeat = Time.realtimeSinceStartup; await LobbyService.Instance.SendHeartbeatPingAsync(queue.Id); }
                    string state = Val(queue, "state");
                    int seed = int.TryParse(Val(queue, "seed"), out var sd) ? sd : 1;
                    if (iAmHost && state == "queue" && QueueRules.ShouldStart(players, elapsed, atTarget < 0 ? 0 : Time.realtimeSinceStartup - atTarget))
                    {
                        await HostRaid(queue, map, seed, players, status);
                        _queueing = false;
                        return;
                    }
                    if (!iAmHost && state == "started" && !string.IsNullOrEmpty(Val(queue, "code")))
                    {
                        status?.Invoke(new QueueStatus { players = players, max = QueueRules.Max, text = "Raid found! Connecting to host..." });
                        await JoinRaid(queue, map, seed, failed);
                        _queueing = false;
                        return;
                    }
                    lastPoll = Time.realtimeSinceStartup;
                }
                // cancelled
                try { await LobbyService.Instance.RemovePlayerAsync(queue.Id, myId); } catch { }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rago] queue failed: " + e);
                if (queue != null) { try { await LobbyService.Instance.RemovePlayerAsync(queue.Id, AuthenticationService.Instance.PlayerId); } catch { } }
                failed?.Invoke(Short(e));
            }
            _queueing = false;
        }

        static string Val(Lobby l, string k) => l.Data != null && l.Data.TryGetValue(k, out var d) ? d.Value : "";

        async Task HostRaid(Lobby queue, string map, int seed, int players, Action<QueueStatus> status)
        {
            status?.Invoke(new QueueStatus { players = players, max = QueueRules.Max, text = "Launching raid with " + players + " operator(s)..." });
            await LeaveInternal(keepQueueLobby: true);
            var alloc = await RelayService.Instance.CreateAllocationAsync(QueueRules.Max - 1);
            string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            _ut.SetHostRelayData(alloc.RelayServer.IpV4, (ushort)alloc.RelayServer.Port, alloc.AllocationIdBytes, alloc.Key, alloc.ConnectionData);
            if (!_nm.StartHost()) throw new Exception("StartHost failed");
            RegisterHandlers();
            _nextSpawn = 1;
            _pendingStart = new RaidStart { map = map, seed = seed, players = players, online = true, spawnIndex = 0 };
            await LobbyService.Instance.UpdateLobbyAsync(queue.Id, new UpdateLobbyOptions
            {
                IsLocked = true,
                Data = new Dictionary<string, DataObject>
                {
                    { "state", new DataObject(DataObject.VisibilityOptions.Public, "started", DataObject.IndexOptions.S2) },
                    { "code", new DataObject(DataObject.VisibilityOptions.Public, code) }
                }
            });
            _lobby = queue;
            _sessionId = queue.Id;
            _inSession = true;
            _status = "Raid host";
            _onRaidStart?.Invoke(_pendingStart);
            // the queue lobby is no longer needed once everyone had time to read the join code
            DeleteLobbyLater(queue.Id, 25000);
        }

        async void DeleteLobbyLater(string id, int ms)
        {
            await Task.Delay(ms);
            try { await LobbyService.Instance.DeleteLobbyAsync(id); } catch { }
            if (_lobby != null && _lobby.Id == id) _lobby = null;
        }

        async Task JoinRaid(Lobby queue, string map, int seed, Action<string> failed)
        {
            await LeaveInternal(keepQueueLobby: true);
            _lobby = queue;
            bool ok = await ConnectClient(Val(queue, "code"));
            try { await LobbyService.Instance.RemovePlayerAsync(queue.Id, AuthenticationService.Instance.PlayerId); } catch { }
            _lobby = null;
            if (!ok) { failed?.Invoke("could not connect to the raid host"); return; }
            _sessionId = queue.Id;
            _inSession = true;
            _status = "Raid client";
            // wait for the host's start message (carries our spawn slot)
            float t = 0;
            while (_pendingStart == null && t < 15f && !_cancel) { await Task.Delay(100); t += 0.1f; }
            if (_pendingStart == null) { await LeaveInternal(); failed?.Invoke("host never started the raid"); return; }
            var rs = _pendingStart; _pendingStart = null;
            _onRaidStart?.Invoke(rs);
        }

        async Task<bool> ConnectClient(string joinCode)
        {
            var ja = await RelayService.Instance.JoinAllocationAsync(joinCode);
            _ut.SetClientRelayData(ja.RelayServer.IpV4, (ushort)ja.RelayServer.Port, ja.AllocationIdBytes, ja.Key, ja.ConnectionData, ja.HostConnectionData);
            _pendingStart = null;
            if (!_nm.StartClient()) return false;
            RegisterHandlers();
            float t = 0;
            while (!_nm.IsConnectedClient && t < 12f) { await Task.Delay(100); t += 0.1f; if (!_nm.IsListening) break; }
            return _nm.IsConnectedClient;
        }

        public void CancelQueue() { _cancel = true; }

        public async void Leave() { await LeaveInternal(); }

        async Task LeaveInternal(bool keepQueueLobby = false)
        {
            _inSession = false;
            _peers.Clear();
            if (_nm != null && (_nm.IsListening || _nm.ShutdownInProgress))
            {
                try { _nm.CustomMessagingManager?.UnregisterNamedMessageHandler(MsgGame); _nm.CustomMessagingManager?.UnregisterNamedMessageHandler(MsgSys); } catch { }
                _nm.Shutdown();
                float t = 0;
                while (_nm.ShutdownInProgress && t < 3f) { await Task.Delay(50); t += 0.05f; }
            }
            await LeaveLobby(); // the queue lobby is never stored in _lobby while queueing, so this only leaves the market
            _status = _ready ? "Online" : _status;
        }

        async Task LeaveLobby()
        {
            if (_lobby == null) return;
            var l = _lobby; _lobby = null;
            try
            {
                string me = AuthenticationService.Instance.PlayerId;
                if (l.HostId == me) await LobbyService.Instance.DeleteLobbyAsync(l.Id);
                else await LobbyService.Instance.RemovePlayerAsync(l.Id, me);
            }
            catch { }
        }

        void Update()
        {
            // keep the market lobby alive while we host it
            if (_lobby != null && _inSession && IsHost)
            {
                _heartbeat -= Time.unscaledDeltaTime;
                if (_heartbeat <= 0) { _heartbeat = 14f; Beat(_lobby.Id); }
            }
        }

        async void Beat(string id) { try { await LobbyService.Instance.SendHeartbeatPingAsync(id); } catch { } }

        void OnDestroy() { try { if (_nm != null && _nm.IsListening) _nm.Shutdown(); } catch { } }
        void OnApplicationQuit() { _ = LeaveLobby(); }

        // ------------------------------------------------------------------------------------ messaging
        void RegisterHandlers()
        {
            var cmm = _nm.CustomMessagingManager;
            if (cmm == null) return;
            cmm.UnregisterNamedMessageHandler(MsgGame);
            cmm.UnregisterNamedMessageHandler(MsgSys);
            cmm.RegisterNamedMessageHandler(MsgGame, OnGameMessage);
            cmm.RegisterNamedMessageHandler(MsgSys, OnSysMessage);
        }

        void OnClientConnected(ulong id)
        {
            if (!_nm.IsServer || id == _nm.LocalClientId) return;
            if (!_peers.Contains(id)) _peers.Add(id);
            OnPeerJoined?.Invoke(id);
            // raid host: hand the new client its start info
            if (_pendingStart != null)
            {
                var rs = _pendingStart;
                SendSys(id, 1, rs.map + "|" + rs.seed + "|" + (_nextSpawn++ % 10) + "|" + rs.players);
            }
            // tell everyone else about the new player
            foreach (var p in _peers) if (p != id) SendSys(p, 2, id.ToString());
        }

        void OnClientDisconnected(ulong id)
        {
            if (_nm == null) return;
            if (!_nm.IsServer && (id == _nm.LocalClientId || id == NetworkManager.ServerClientId))
            {
                _inSession = false;
                _peers.Clear();
                _status = "Disconnected";
                OnDisconnected?.Invoke();
                return;
            }
            if (_nm.IsServer)
            {
                _peers.Remove(id);
                OnPeerLeft?.Invoke(id);
                foreach (var p in _peers) SendSys(p, 3, id.ToString());
            }
        }

        public void Send(byte[] data, bool reliable, ulong target = ulong.MaxValue)
        {
            if (!InSession || data == null) return;
            var delivery = reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;
            if (!reliable && data.Length > 1100) delivery = NetworkDelivery.ReliableFragmentedSequenced;
            ulong me = _nm.LocalClientId;
            using (var w = new FastBufferWriter(data.Length + 32, Allocator.Temp))
            {
                w.WriteValueSafe(me);
                w.WriteValueSafe(target);
                w.WriteValueSafe(data);
                var cmm = _nm.CustomMessagingManager;
                if (_nm.IsServer)
                {
                    if (target == ulong.MaxValue) { foreach (var p in _peers) cmm.SendNamedMessage(MsgGame, p, w, delivery); }
                    else if (target != me) cmm.SendNamedMessage(MsgGame, target, w, delivery);
                }
                else cmm.SendNamedMessage(MsgGame, NetworkManager.ServerClientId, w, delivery);
            }
        }

        void OnGameMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong origin);
            reader.ReadValueSafe(out ulong target);
            reader.ReadValueSafe(out byte[] payload);
            if (_nm.IsServer)
            {
                origin = sender; // never trust the claimed origin
                // relay to others
                if (target == ulong.MaxValue || (target != _nm.LocalClientId && target != NetworkManager.ServerClientId))
                {
                    using (var w = new FastBufferWriter(payload.Length + 32, Allocator.Temp))
                    {
                        w.WriteValueSafe(origin); w.WriteValueSafe(target); w.WriteValueSafe(payload);
                        if (target == ulong.MaxValue) { foreach (var p in _peers) if (p != sender) _nm.CustomMessagingManager.SendNamedMessage(MsgGame, p, w, NetworkDelivery.ReliableFragmentedSequenced); }
                        else _nm.CustomMessagingManager.SendNamedMessage(MsgGame, target, w, NetworkDelivery.ReliableFragmentedSequenced);
                    }
                    if (target != ulong.MaxValue) return;
                }
            }
            else if (!_peers.Contains(origin) && origin != _nm.LocalClientId) _peers.Add(origin);
            OnData?.Invoke(origin, payload);
        }

        void SendSys(ulong to, byte code, string text)
        {
            using (var w = new FastBufferWriter(256 + text.Length * 2, Allocator.Temp))
            {
                w.WriteValueSafe(code);
                w.WriteValueSafe(text);
                _nm.CustomMessagingManager.SendNamedMessage(MsgSys, to, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnSysMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte code);
            reader.ReadValueSafe(out string text);
            switch (code)
            {
                case 1: // raid start
                    var p = text.Split('|');
                    if (p.Length >= 4) _pendingStart = new RaidStart { map = p[0], seed = int.Parse(p[1]), spawnIndex = int.Parse(p[2]), players = int.Parse(p[3]), online = true };
                    break;
                case 2: if (ulong.TryParse(text, out var j)) { if (!_peers.Contains(j)) _peers.Add(j); OnPeerJoined?.Invoke(j); } break;
                case 3: if (ulong.TryParse(text, out var l)) { _peers.Remove(l); OnPeerLeft?.Invoke(l); } break;
            }
        }
    }
}
#endif
