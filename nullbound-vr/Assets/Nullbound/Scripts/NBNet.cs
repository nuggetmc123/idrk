using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

namespace Nullbound
{
    /// Starting and joining matches.
    ///  - SOLO: you host on your own headset, with bots.
    ///  - ONLINE: Unity Relay gives the host a 6-letter room code; friends type it in to join from anywhere.
    ///  - LAN: same Wi-Fi, join by the host's IP address (no Unity account needed).
    public class NBNet : MonoBehaviour
    {
        public const int MaxPlayers = 10;
        public const ushort LanPort = 7777;
        const string Version = "nullbound-1";

        public static NBNet Instance { get; private set; }
        public static string Status = "Pick a mode to start fighting!";

        public bool Busy { get; private set; }
        public string Mode { get; private set; } = "";
        public string RoomCode { get; private set; } = "";
        public bool IsHost => m_Nm != null && m_Nm.IsHost;

        public string RoomLabel =>
            Mode == "online" ? (IsHost ? "ROOM CODE: " + RoomCode : "ONLINE ROOM " + RoomCode)
            : Mode == "lan" ? (IsHost ? "LAN HOST  " + LocalIp() : "LAN MATCH")
            : Mode == "solo" ? "SOLO MATCH" : "";

        NetworkManager m_Nm;
        UnityTransport m_Utp;
        bool m_Hooked;
        bool m_LeavingOnPurpose;
        int m_BotNames;

        static readonly string[] BotNames = { "BOT ECHO", "BOT RAZOR", "BOT PIXEL", "BOT TURBO", "BOT GHOST", "BOT NOVA", "BOT BRICK", "BOT VIPER", "BOT ORBIT", "BOT FANG" };

        void Awake()
        {
            Instance = this;
            m_Nm = GetComponent<NetworkManager>();
            if (m_Nm == null) m_Nm = FindFirstObjectByType<NetworkManager>();
            m_Utp = m_Nm != null ? m_Nm.GetComponent<UnityTransport>() : null;
            if (m_Nm == null || m_Utp == null)
            {
                Status = "Network setup missing - run Nullbound > Setup Everything in the Unity editor.";
                return;
            }
            m_Nm.NetworkConfig.NetworkTransport = m_Utp;
            m_Nm.NetworkConfig.ConnectionApproval = true;
            m_Nm.NetworkConfig.EnableSceneManagement = false;
            m_Nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Version);
        }

        void Hook()
        {
            if (m_Hooked) return;
            m_Hooked = true;
            m_Nm.ConnectionApprovalCallback = Approve;
            m_Nm.OnClientDisconnectCallback += OnClientDisconnect;
            m_Nm.OnClientStopped += OnStopped;
            m_Nm.OnServerStopped += OnStopped;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            string v = req.Payload != null ? Encoding.UTF8.GetString(req.Payload) : "";
            int humans = m_Nm.ConnectedClientsIds.Count;
            if (v != Version) { res.Approved = false; res.Reason = "Different game version - update both headsets."; return; }
            if (humans >= MaxPlayers) { res.Approved = false; res.Reason = "That room is full."; return; }
            res.Approved = true;
            res.CreatePlayerObject = true;
            res.Position = new Vector3(0, 0.1f, 0);
            // make room by kicking a bot if needed
            int total = NBPlayer.All.Count(p => p != null && p.IsSpawned);
            if (total >= MaxPlayers) RemoveBot();
        }

        void OnClientDisconnect(ulong id)
        {
            if (m_Nm.IsServer) return;
            if (id == m_Nm.LocalClientId && !m_LeavingOnPurpose)
            {
                var reason = m_Nm.DisconnectReason;
                Status = string.IsNullOrEmpty(reason) ? "Disconnected from the match." : reason;
            }
        }

        void OnStopped(bool wasHost)
        {
            Mode = ""; RoomCode = "";
            Busy = false;
            if (!m_LeavingOnPurpose && string.IsNullOrEmpty(Status)) Status = "Match ended.";
            m_LeavingOnPurpose = false;
            NBGame.Instance?.ReturnToLobby();
        }

        bool Ready()
        {
            if (m_Nm == null) return false;
            if (m_Nm.IsListening || m_Nm.ShutdownInProgress) { Status = "Already in a match - leave it first."; return false; }
            Hook();
            return true;
        }

        // -------------------------------------------------------------- modes
        public void StartSolo()
        {
            if (!Ready()) return;
            m_Utp.SetConnectionData("127.0.0.1", LanPort, "127.0.0.1");
            if (m_Nm.StartHost())
            {
                Mode = "solo"; Status = "";
                SpawnBots(Mathf.Max(1, NBSettings.Bots));
            }
            else Status = "Couldn't start the match.";
        }

        public void HostLan()
        {
            if (!Ready()) return;
            m_Utp.SetConnectionData("0.0.0.0", LanPort, "0.0.0.0");
            if (m_Nm.StartHost())
            {
                Mode = "lan"; Status = "";
                SpawnBots(NBSettings.Bots);
            }
            else Status = "Couldn't host on LAN (is port " + LanPort + " in use?).";
        }

        public void JoinLan(string ip)
        {
            if (!Ready()) return;
            ip = (ip ?? "").Trim();
            if (ip.Count(c => c == '.') != 3) { Status = "That doesn't look like an IP address (like 192.168.1.20)."; return; }
            m_Utp.SetConnectionData(ip, LanPort);
            Mode = "lan";
            Status = "Connecting to " + ip + "...";
            if (!m_Nm.StartClient()) { Status = "Couldn't connect."; Mode = ""; }
        }

        public async void HostOnline()
        {
            if (!Ready() || Busy) return;
            Busy = true;
            try
            {
                Status = "Signing in to Unity Relay...";
                await SignIn();
                Status = "Creating room...";
                var alloc = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
                var code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                var ep = alloc.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls");
                if (ep == null) throw new Exception("Relay gave no secure endpoint.");
                m_Utp.SetHostRelayData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.Key, alloc.ConnectionData, true);
                if (!m_Nm.StartHost()) throw new Exception("Couldn't start hosting.");
                Mode = "online"; RoomCode = code.ToUpperInvariant();
                Status = "";
                SpawnBots(NBSettings.Bots);
            }
            catch (Exception e)
            {
                Status = "Online hosting failed: " + Friendly(e);
                Debug.LogException(e);
            }
            Busy = false;
        }

        public async void JoinOnline(string code)
        {
            if (!Ready() || Busy) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 6) { Status = "Room codes are 6 letters/numbers."; return; }
            Busy = true;
            try
            {
                Status = "Signing in to Unity Relay...";
                await SignIn();
                Status = "Joining room " + code + "...";
                var join = await RelayService.Instance.JoinAllocationAsync(code);
                var ep = join.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls");
                if (ep == null) throw new Exception("Relay gave no secure endpoint.");
                m_Utp.SetClientRelayData(ep.Host, (ushort)ep.Port, join.AllocationIdBytes, join.Key, join.ConnectionData, join.HostConnectionData, true);
                Mode = "online"; RoomCode = code;
                if (!m_Nm.StartClient()) throw new Exception("Couldn't start the connection.");
                Status = "Connecting...";
            }
            catch (Exception e)
            {
                Mode = "";
                Status = "Couldn't join: " + Friendly(e);
                Debug.LogException(e);
            }
            Busy = false;
        }

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string Friendly(Exception e)
        {
            var m = e.Message ?? "";
            if (Has(m, "project") || Has(m, "cloud"))
                return "this build isn't linked to a Unity Cloud project (see README: Online multiplayer).";
            if (Has(m, "join code") || Has(m, "not found"))
                return "no room with that code.";
            return m.Length > 120 ? m.Substring(0, 120) : m;
        }

        static bool Has(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        public void Leave()
        {
            if (m_Nm == null) return;
            m_LeavingOnPurpose = true;
            Status = "Left the match.";
            m_Nm.Shutdown();
        }

        // -------------------------------------------------------------- bots
        public void SpawnBots(int count)
        {
            for (int i = 0; i < count; i++) AddBot();
        }

        public void AddBot()
        {
            if (m_Nm == null || !m_Nm.IsServer) return;
            if (NBPlayer.All.Count(p => p != null && p.IsSpawned) >= MaxPlayers) return;
            var prefab = m_Nm.NetworkConfig.PlayerPrefab;
            if (prefab == null) return;
            var go = Instantiate(prefab, NBArena.PickSpawn(null), Quaternion.identity);
            var p = go.GetComponent<NBPlayer>();
            p.pendingBot = true;
            p.pendingFighter = UnityEngine.Random.Range(0, NBFighters.All.Length);
            p.pendingName = BotNames[m_BotNames++ % BotNames.Length];
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        public void RemoveBot()
        {
            if (m_Nm == null || !m_Nm.IsServer) return;
            var bot = NBPlayer.All.LastOrDefault(p => p != null && p.IsSpawned && p.IsBot.Value);
            if (bot != null) bot.NetworkObject.Despawn(true);
        }

        public static string LocalIp()
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    foreach (var a in ni.GetIPProperties().UnicastAddresses)
                        if (a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a.Address))
                            return a.Address.ToString();
                }
            }
            catch { }
            return "(check headset Wi-Fi settings)";
        }
    }
}
