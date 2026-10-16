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
        const string PublicPrefix = "KARTVR_PUB_";

        /// <summary>Players per session: one for each garage in the hub.</summary>
        public const int MaxPlayers = 6;
        /// <summary>How many numbered public servers we look through for a free spot.</summary>
        public const int ServerCount = 30;
        const string ModeProperty = "mode";

        [Tooltip("The Kart prefab (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject kartPrefab;

        [Tooltip("The on-foot Player prefab used in the hub (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject playerPrefab;


        [Tooltip("Plain lit material that buttons, panels, karts and avatars built at runtime copy.")]
        public Material baseMaterial;

        [Tooltip("Additive particle material for booster flames.")]
        public Material particleMaterial;

        [Tooltip("Player body model (Models/Racer.fbx). Leave empty to use the simple shape body.")]
        public GameObject racerModel;

        public NetworkRunner Runner { get; private set; }
        public bool IsConnected => Runner != null && Runner.IsRunning;
        public bool IsPrivate { get; private set; }
        /// <summary>
        /// Playing on your own because going online failed (no internet, no Photon App ID...). You can still walk,
        /// drive and use every map; the server board / keypad try online again.
        /// </summary>
        public bool IsSolo { get; private set; }
        /// <summary>In the first-time tutorial (a solo Free Roam game on this headset only).</summary>
        public bool IsTutorial { get; private set; }
        /// <summary>Why the last online attempt failed (shown on the hub boards and the menu).</summary>
        public string OnlineError { get; private set; } = "";
        public string LobbyCode { get; private set; }
        public bool Busy { get; private set; }
        /// <summary>Which numbered public server you're on (1, 2, 3...). People on the same hub server go through portals together.</summary>
        public int ServerNumber { get; private set; } = 1;

        /// <summary>The mode we asked for when we connected. The first player in a session uses it to set GameState.Mode.</summary>
        public KartMode RequestedMode { get; private set; }

        public string Status { get; private set; } = "Starting...";
        float statusUntil;
        public string TransientStatus => Time.time < statusUntil ? Status : null;

        public int PlayerCount => IsConnected ? Runner.ActivePlayers.Count() : 0;

        int worldSceneIndex;
        float joinedAt = -1f;
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

        System.Collections.IEnumerator Start()
        {
            if (Instance != this) yield break;
            // Ask for the microphone BEFORE going online. On Quest the permission popup pauses the game; asking
            // mid-session froze you in place and could drop the connection. Never wait more than 15 s for it.
            SetStatus("Starting...", 30f);
            MicPermission.Request();
            float until = Time.realtimeSinceStartup + 15f;
            while (MicPermission.Waiting && Time.realtimeSinceStartup < until) yield return null;
            // First time on this device: the tutorial comes first, then the hub.
            if (Tutorial.Done) JoinPublic(KartMode.Hub);
            else StartTutorial();
        }

        // ---------- Public API (called by portals, keypad, wrist menu) ----------

        public void JoinPublic(KartMode mode)
        {
            _ = RunConnect(() => ConnectPublic(mode, ServerNumber), mode);
        }

        /// <summary>Move to a numbered public hub server (from the server board). If it's full you get the next free one.</summary>
        public void JoinServer(int number)
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
            number = Mathf.Clamp(number, 1, ServerCount);
            if (!IsPrivate && !IsSolo && number == ServerNumber && IsConnected && GameState.CurrentMode == KartMode.Hub)
            {
                SetStatus("You're already on server " + number, 3f);
                return;
            }
            _ = RunConnect(() => ConnectPublic(KartMode.Hub, number), KartMode.Hub);
        }

        /// <summary>False until Photon Fusion 2 is imported (the game then runs solo, offline).</summary>
        public static bool MultiplayerAvailable => typeof(NetworkRunner).Assembly.GetName().Name == "Fusion.Runtime";

        const string OfflineMessage = "Playing OFFLINE (solo). Import Photon Fusion 2 to play online with friends.";

        public void CreatePrivateLobby()
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
            _ = RunConnect(CreatePrivate);
        }

        public void JoinPrivateLobby(string code)
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
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
            if (!IsConnected) return Busy ? (string.IsNullOrEmpty(Status) ? "Connecting..." : Status) : "Offline";
            if (!MultiplayerAvailable)
                return "OFFLINE - SOLO PLAY\n" + (GameState.Instance != null && GameState.Instance.IsReady
                    ? KartModeInfo.DisplayName(GameState.Instance.Mode) : KartModeInfo.DisplayName(RequestedMode));
            string mode = GameState.Instance != null && GameState.Instance.IsReady
                ? KartModeInfo.DisplayName(GameState.Instance.Mode)
                : KartModeInfo.DisplayName(RequestedMode);
            string problem = Diagnose();
            if (problem != null) return "<color=#ff9f43>PROBLEM: " + problem + "</color>";
            if (IsTutorial)
                return "TUTORIAL\nFollow the steps on the panel in front of you.";
            if (IsSolo)
                return "PLAYING SOLO - " + mode.ToUpper() + "\nCouldn't go online: " + OnlineError + "\nPress a SERVER button to try again.";
            if (IsPrivate)
                return "PRIVATE LOBBY  CODE " + KartUI.FormatCode(LobbyCode) + "\n" + mode + " - " + PlayerCount + " player(s)";
            return "SERVER " + ServerNumber + "  -  " + mode.ToUpper() + "\n" + PlayerCount + "/" + MaxPlayers + " players";
        }

        /// <summary>Connected but something's missing a few seconds later: say what, plus the first error the game hit.</summary>
        string Diagnose()
        {
            if (joinedAt < 0f || Time.time - joinedAt < 6f) return null;
            string what = null;
            if (PlayerAvatar.Local == null) what = "your player didn't appear";
            else if (GameState.Instance == null || !GameState.Instance.IsReady) what = "the game state didn't start";
            if (what == null) return null;
            return what + (string.IsNullOrEmpty(ErrorLog.First) ? "" : "\n<size=60%>" + ErrorLog.First + "</size>") +
                   "\n<size=60%>(Send a photo of this board.)</size>";
        }

        public void SetStatus(string text, float seconds = 4f)
        {
            Status = text;
            statusUntil = Time.time + seconds;
            Debug.Log("[KartVR] " + text);
        }

        // ---------- Connection flow ----------

        /// <param name="publicMode">Set for public joins: if going online fails, you play solo in this mode.</param>
        async Task RunConnect(Func<Task<bool>> attempt, KartMode? publicMode = null)
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
            if (ok || IsConnected) return;

            if (publicMode.HasValue && MultiplayerAvailable)
            {
                // Couldn't get online: play solo instead of standing frozen with no player.
                OnlineError = string.IsNullOrEmpty(Status) ? "unknown error" : Status.Replace("Couldn't connect: ", "");
                await StartSolo(publicMode.Value);
                return;
            }
            // A bad lobby code etc.: back to the public hub.
            await Task.Delay(3000);
            if (!Busy && !IsConnected) JoinPublic(KartMode.Hub);
        }

        /// <summary>Start (or replay) the tutorial: a solo Free Roam game with a step-by-step guide.</summary>
        public void StartTutorial()
        {
            _ = RunTutorial();
        }

        async Task RunTutorial()
        {
            if (Busy) return;
            Busy = true;
            bool ok = false;
            try
            {
                SetStatus("Starting the tutorial...", 10f);
                var result = await StartSession(new StartGameArgs
                {
                    GameMode = GameMode.Single,
                    SessionName = "KARTVR_TUTORIAL",
                    PlayerCount = 1,
                }, KartMode.Tutorial, false, null);
                ok = result.Ok;
                if (!ok) SetStatus("Couldn't start the tutorial: " + result.ShutdownReason, 8f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Busy = false;
            IsTutorial = ok;
            if (ok) Tutorial.Begin();
            else JoinPublic(KartMode.Hub); // never get stuck: skip straight to the game
        }

        /// <summary>Called when the last tutorial step is done: off to the hub with everyone.</summary>
        public void FinishTutorial()
        {
            IsTutorial = false;
            SetStatus("Tutorial complete! Joining the hub...", 6f);
            JoinPublic(KartMode.Hub);
        }

        /// <summary>A one-player game on this headset only. Needs no internet or App ID, so it always works.</summary>
        async Task StartSolo(KartMode mode)
        {
            if (Busy) return;
            Busy = true;
            try
            {
                var result = await StartSession(new StartGameArgs
                {
                    GameMode = GameMode.Single,
                    SessionName = "KARTVR_SOLO",
                    PlayerCount = 1,
                }, mode, false, null);
                IsSolo = result.Ok;
                if (result.Ok) SetStatus("Couldn't go online (" + OnlineError + "). Playing solo.", 10f);
                else SetStatus("Couldn't start the game: " + result.ShutdownReason + " " + result.ErrorMessage, 30f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Couldn't start the game: " + e.Message, 30f);
            }
            Busy = false;
        }

        /// <summary>
        /// Join public server number `first` for this mode; if it's full, try the next number, and so on.
        /// Each server holds <see cref="MaxPlayers"/> players.
        /// </summary>
        async Task<bool> ConnectPublic(KartMode mode, int first)
        {
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
            first = Mathf.Clamp(first, 1, ServerCount);
            for (int i = 0; i < ServerCount; i++)
            {
                int number = (first - 1 + i) % ServerCount + 1;
                SetStatus("Joining server " + number + " (" + KartModeInfo.DisplayName(mode) + ")...", 30f);
                var result = await StartSession(new StartGameArgs
                {
                    GameMode = GameMode.Shared,
                    SessionName = PublicPrefix + mode + "_" + number,
                    SessionProperties = new Dictionary<string, SessionProperty> { [ModeProperty] = (int)mode },
                    IsVisible = true,
                    PlayerCount = MaxPlayers,
                }, mode, false, null);

                if (result.Ok)
                {
                    ServerNumber = number;
                    SetStatus(number == first
                        ? "Joined server " + number + " (" + KartModeInfo.DisplayName(mode) + ")"
                        : "Server " + first + " is full, so you joined server " + number, 5f);
                    return true;
                }
                if (result.ShutdownReason != ShutdownReason.GameIsFull)
                {
                    SetStatus("Couldn't connect: " + Explain(result), 8f);
                    return false;
                }
            }
            SetStatus("Every server is full! Try again in a bit.");
            return false;
        }

        async Task<bool> CreatePrivate()
        {
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
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
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
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

        /// <summary>Banned, or a modified copy of the game: no online play (solo still works).</summary>
        static bool OnlineBlocked(out string why)
        {
            if (AntiCheat.Tampered) { why = "this copy of the game was modified"; return true; }
            if (Moderation.IsBanned(out why)) return true;
            return false;
        }

        /// <summary>Kicked or banned by a moderator: leave the online game and play solo.</summary>
        public void Removed(string why)
        {
            SetStatus(why, 20f);
            if (!IsConnected || IsSolo || IsTutorial || !MultiplayerAvailable) return;
            OnlineError = why;
            _ = StartSolo(KartMode.Hub);
        }

        /// <summary>Turns Fusion's failure reason into something you can act on.</summary>
        static string Explain(StartGameResult result)
        {
            switch (result.ShutdownReason)
            {
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                    return "the Photon App ID is missing or wrong (KartVR > Set Up Multiplayer, then build again)";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                case ShutdownReason.OperationTimeout:
                    return "no internet connection (check the headset's Wi-Fi)";
                default:
                    return result.ShutdownReason + (string.IsNullOrEmpty(result.ErrorMessage) ? "" : " - " + result.ErrorMessage);
            }
        }

        StartGameArgs PrivateArgs(string code)
        {
            return new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = PrivatePrefix + code,
                IsVisible = false, // never shows up in public matchmaking
                PlayerCount = MaxPlayers,
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

            joinedAt = -1f;
            RequestedMode = mode;
            IsPrivate = isPrivate;
            IsSolo = false;
            if (IsTutorial) { IsTutorial = false; Tutorial.Stop(); } // left the tutorial early: you'll get it again next launch
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
            // Your walking player first (without it you can't move), then the kart, which starts out of sight and
            // parks itself in your garage (or drives to the grid) right away. One failing never stops the other.
            try { runner.Spawn(playerPrefab, pose.position, pose.rotation, player); }
            catch (Exception e) { Debug.LogError("[KartVR] Couldn't spawn your player: " + e.Message); }
            try { runner.Spawn(kartPrefab, new Vector3(0f, -50f, 0f), Quaternion.identity, player); }
            catch (Exception e) { Debug.LogError("[KartVR] Couldn't spawn your kart: " + e.Message); }
            joinedAt = Time.time;
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
        // Fusion 2.0 passes ArraySegment, Fusion 2.1+ passes ReadOnlySpan. Having both means any version compiles.
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, Fusion.Sockets.ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, Fusion.Sockets.ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, Fusion.Sockets.ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
