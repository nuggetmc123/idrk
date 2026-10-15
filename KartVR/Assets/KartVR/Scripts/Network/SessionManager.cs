using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartVR
{
    /// <summary>
    /// Owns the Photon Fusion connection (Shared Mode) and moves you between sessions.
    ///
    /// Public play: on launch you're matched into a public Hub session with
    /// whoever is online. Driving into a mode portal in the public hub matches
    /// you into a public session of that mode (Race / Bumper / Free Roam).
    ///
    /// Private play: "Create" makes an invisible session named after a random
    /// 6-digit code. Friends type that code on the hub keypad to join. Inside a
    /// private lobby the portals move the WHOLE lobby together instead of
    /// matchmaking, because everyone shares one GameState.
    ///
    /// Shared Mode keeps a session alive as long as anyone is in it, so the hub
    /// survives whoever opened it leaving.
    /// </summary>
    public class SessionManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static SessionManager Instance { get; private set; }

        const string PrivatePrefix = "KARTVR_PRIV_";
        const string ModeProperty = "mode";

        [Tooltip("The Kart prefab (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject kartPrefab;

        [Tooltip("The on-foot Player prefab used in the hub (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject playerPrefab;

        [Range(2, 20)] public int maxPlayersPerSession = 12;

        [Tooltip("Plain lit material that buttons, panels, karts and avatars built at runtime copy.")]
        public Material baseMaterial;

        [Tooltip("Additive particle material for booster flames.")]
        public Material particleMaterial;

        [Tooltip("Player body model (Models/Racer.fbx). Leave empty to use the simple shape body.")]
        public GameObject racerModel;

        public NetworkRunner Runner { get; private set; }
        public bool IsConnected => Runner != null && Runner.IsRunning;
        public bool IsPrivate { get; private set; }
        public string LobbyCode { get; private set; }
        public bool Busy { get; private set; }

        /// <summary>The mode we asked for when we connected. The first player in a session uses it to set GameState.Mode.</summary>
        public KartMode RequestedMode { get; private set; }

        public string Status { get; private set; } = "Starting...";
        float statusUntil;
        public string TransientStatus => Time.time < statusUntil ? Status : null;

        public int PlayerCount => IsConnected ? Runner.ActivePlayers.Count() : 0;

        int worldSceneIndex;
        bool hasConnectedOnce;
        bool shuttingDownOnPurpose;

        void Awake()
        {
            // The world scene is reloaded every time we switch sessions; keep the first manager.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            KartUI.BaseMaterial = baseMaterial;
            KartUI.ParticleMaterial = particleMaterial;
            KartUI.RacerModel = racerModel;
            worldSceneIndex = SceneManager.GetActiveScene().buildIndex;

            // Smoother physics for a camera that rides on a rigidbody.
            Time.fixedDeltaTime = 1f / 72f;
        }

        void Start()
        {
            if (Instance == this) JoinPublic(KartMode.Hub);
        }

        // ---------- Public API (called by portals, keypad, wrist menu) ----------

        public void JoinPublic(KartMode mode)
        {
            _ = RunConnect(() => ConnectPublic(mode));
        }

        public void CreatePrivateLobby()
        {
            _ = RunConnect(CreatePrivate);
        }

        public void JoinPrivateLobby(string code)
        {
            code = (code ?? "").Trim();
            if (code.Length != 6 || !code.All(char.IsDigit))
            {
                SetStatus("Codes are 6 digits", 3f);
                return;
            }
            _ = RunConnect(() => JoinPrivate(code));
        }

        public void LeaveToPublicHub()
        {
            JoinPublic(KartMode.Hub);
        }

        /// <summary>"Go to the hub" button: in a private lobby take the group back, otherwise rejoin the public hub.</summary>
        public void GoToHub()
        {
            if (IsPrivate && GameState.Instance != null && GameState.Instance.IsReady)
                GameState.Instance.RequestMode(KartMode.Hub);
            else
                JoinPublic(KartMode.Hub);
        }

        public string DescribeLobby()
        {
            if (!IsConnected) return Busy ? "Connecting..." : "Offline";
            string mode = GameState.Instance != null && GameState.Instance.IsReady
                ? KartModeInfo.DisplayName(GameState.Instance.Mode)
                : KartModeInfo.DisplayName(RequestedMode);
            if (IsPrivate)
                return "PRIVATE LOBBY  CODE " + KartUI.FormatCode(LobbyCode) + "\n" + mode + " - " + PlayerCount + " player(s)";
            return "PUBLIC " + mode.ToUpper() + "\n" + PlayerCount + " player(s) online here";
        }

        public void SetStatus(string text, float seconds = 4f)
        {
            Status = text;
            statusUntil = Time.time + seconds;
            Debug.Log("[KartVR] " + text);
        }

        // ---------- Connection flow ----------

        async Task RunConnect(Func<Task<bool>> attempt)
        {
            if (Busy) return;
            Busy = true;
            bool ok;
            try
            {
                ok = await attempt();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Connection error: " + e.Message);
                ok = false;
            }
            Busy = false;

            // Anything that leaves us offline (bad code, network hiccup) falls back to the public hub.
            if (!ok && !IsConnected)
            {
                await Task.Delay(3000);
                if (!Busy && !IsConnected) JoinPublic(KartMode.Hub);
            }
        }

        async Task<bool> ConnectPublic(KartMode mode)
        {
            SetStatus("Finding a public " + KartModeInfo.DisplayName(mode) + " session...", 30f);
            var result = await StartSession(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = null, // null = join any matching public session, or create one
                SessionProperties = new Dictionary<string, SessionProperty> { [ModeProperty] = (int)mode },
                IsVisible = true,
                PlayerCount = maxPlayersPerSession,
            }, mode, false, null);

            if (!result.Ok)
            {
                SetStatus("Couldn't connect: " + result.ShutdownReason);
                return false;
            }
            SetStatus("Joined public " + KartModeInfo.DisplayName(mode), 3f);
            return true;
        }

        async Task<bool> CreatePrivate()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string code = UnityEngine.Random.Range(0, 1000000).ToString("000000");
                SetStatus("Creating private lobby...", 30f);
                var result = await StartSession(PrivateArgs(code), KartMode.Hub, true, code);
                if (!result.Ok)
                {
                    SetStatus("Couldn't create lobby: " + result.ShutdownReason);
                    return false;
                }
                // If we aren't the master client, someone already had this code. Try another.
                if (Runner.IsSharedModeMasterClient)
                {
                    SetStatus("Private lobby created! Code: " + KartUI.FormatCode(code), 10f);
                    return true;
                }
                await ShutdownRunner();
            }
            SetStatus("Couldn't find a free code, try again");
            return false;
        }

        async Task<bool> JoinPrivate(string code)
        {
            SetStatus("Joining lobby " + KartUI.FormatCode(code) + "...", 30f);
            var result = await StartSession(PrivateArgs(code), KartMode.Hub, true, code);
            if (!result.Ok)
            {
                SetStatus("Couldn't join: " + result.ShutdownReason);
                return false;
            }
            // Shared Mode creates the session if it doesn't exist. If we're the
            // master client we're alone in it: nobody is using that code.
            if (Runner.IsSharedModeMasterClient && Runner.ActivePlayers.Count() <= 1)
            {
                await ShutdownRunner();
                SetStatus("No lobby found with code " + KartUI.FormatCode(code), 5f);
                return false;
            }
            SetStatus("Joined lobby " + KartUI.FormatCode(code), 5f);
            return true;
        }

        StartGameArgs PrivateArgs(string code)
        {
            return new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = PrivatePrefix + code,
                IsVisible = false, // never shows up in public matchmaking
                PlayerCount = maxPlayersPerSession,
            };
        }

        async Task<StartGameResult> StartSession(StartGameArgs args, KartMode mode, bool isPrivate, string code)
        {
            await ShutdownRunner();

            // Reload the world so scene network objects (GameState etc.) start fresh.
            if (hasConnectedOnce)
            {
                KartRig.Instance?.Detach();
                var op = SceneManager.LoadSceneAsync(worldSceneIndex, LoadSceneMode.Single);
                while (op != null && !op.isDone) await Task.Yield();
            }
            hasConnectedOnce = true;

            RequestedMode = mode;
            IsPrivate = isPrivate;
            LobbyCode = code;

            var go = new GameObject("NetworkRunner");
            DontDestroyOnLoad(go);
            Runner = go.AddComponent<NetworkRunner>();
            var sceneManager = go.AddComponent<NetworkSceneManagerDefault>();
            Runner.AddCallbacks(this);

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(worldSceneIndex), LoadSceneMode.Additive);
            args.Scene = sceneInfo;
            args.SceneManager = sceneManager;

            var result = await Runner.StartGame(args);
            if (!result.Ok)
            {
                Runner = null;
            }
            return result;
        }

        async Task ShutdownRunner()
        {
            if (Runner == null) return;
            KartRig.Instance?.Detach();
            shuttingDownOnPurpose = true;
            var runner = Runner;
            Runner = null;
            if (runner.IsRunning) await runner.Shutdown();
            else if (runner != null) Destroy(runner.gameObject);
            shuttingDownOnPurpose = false;
        }

        void OnApplicationQuit()
        {
            if (Runner != null)
            {
                shuttingDownOnPurpose = true;
                Runner.Shutdown();
            }
        }

        // ---------- Fusion callbacks ----------

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // In Shared Mode every client spawns its own kart and its own walking avatar.
            if (player != runner.LocalPlayer) return;
            if (kartPrefab == null || playerPrefab == null)
            {
                Debug.LogError("[KartVR] SessionManager is missing the Kart or Player prefab. Run KartVR > Build Prototype World.");
                return;
            }
            var area = SpawnArea.Get(KartMode.Hub);
            Pose pose = area != null ? area.GetSlot(0) : new Pose(Vector3.up, Quaternion.identity);
            // The kart starts out of sight; it parks itself in your garage (or drives to the grid) right away.
            runner.Spawn(kartPrefab, new Vector3(0f, -50f, 0f), Quaternion.identity, player);
            runner.Spawn(playerPrefab, pose.position, pose.rotation, player);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (shuttingDownOnPurpose) return;
            if (Runner != null && runner != Runner) return;
            Runner = null;
            KartRig.Instance?.Detach();
            SetStatus("Disconnected (" + shutdownReason + "), reconnecting...", 5f);
            Invoke(nameof(ReconnectAfterDrop), 2f);
        }

        void ReconnectAfterDrop()
        {
            if (!IsConnected && !Busy) JoinPublic(KartMode.Hub);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
