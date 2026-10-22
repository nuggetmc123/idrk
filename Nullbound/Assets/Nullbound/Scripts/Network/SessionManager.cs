using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nullbound
{
    /// <summary>
    /// Owns the Photon Fusion connection (Shared Mode) and moves you between rooms.
    ///
    /// QUICK MATCH: free-for-all rooms of up to <see cref="MaxPlayers"/>. You're put in the fullest room that still
    /// has space; a new room (with a random 6-digit code) is only made when they're all full.
    /// PRIVATE LOBBY: a room only people with its code can join (no host: it lives as long as anyone's in it).
    /// Friends type your code (public or private) in Menu > Play > Join a Code.
    /// TRAINING: just you and AI bots on this headset (the Training Grounds). No internet needed.
    /// MAIN MENU: where the game starts and where you go back to (no session at all).
    ///
    /// Public codes start with 1-4, private codes with 5-9.
    /// </summary>
    public class SessionManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static SessionManager Instance { get; private set; }

        const string PublicPrefix = "NB_PUB_", PrivatePrefix = "NB_PRIV_";
        const string ModeProperty = "mode", KindProperty = "kind";
        /// <summary>Fighters per room.</summary>
        public const int MaxPlayers = 8;
        public const int MaxBots = 5;

        [Tooltip("The Fighter prefab (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject fighterPrefab;
        [Tooltip("Plain lit material that runtime-made parts copy.")]
        public Material baseMaterial;
        [Tooltip("Additive particle material for sparks.")]
        public Material particleMaterial;

        public NetworkRunner Runner { get; private set; }
        public bool IsConnected => Runner != null && Runner.IsRunning;
        public bool IsPrivate { get; private set; }
        /// <summary>Training vs bots on this headset only.</summary>
        public bool IsTraining { get; private set; }
        /// <summary>Playing on your own because going online failed (training rules).</summary>
        public bool IsSolo { get; private set; }
        /// <summary>Which map this session plays on: the Battleground online, the Training Grounds in training.</summary>
        public ArenaMap CurrentMap { get; private set; } = ArenaMap.TrainingGrounds;
        /// <summary>In the main menu room (not in any match).</summary>
        public bool InMenu { get; private set; } = true;
        public bool Busy { get; private set; }
        public string OnlineError { get; private set; } = "";
        /// <summary>The code friends type to join you.</summary>
        public string RoomCode { get; private set; } = "";
        public string SessionName { get; private set; } = "";
        public int PlayerCount => IsConnected ? Runner.ActivePlayers.Count() : 0;
        /// <summary>Bots can be added in training and private lobbies (never in quick match).</summary>
        public bool CanUseBots => IsConnected && (IsTraining || IsSolo || IsPrivate);
        /// <summary>Bots only go after people (not each other).</summary>
        public bool BotsFightEachOther => false;

        public string Status { get; private set; } = "Starting...";
        float statusUntil;
        public string TransientStatus => Time.time < statusUntil ? Status : null;

        int worldSceneIndex;
        bool hasConnectedOnce, shuttingDownOnPurpose;
        int botsToSpawn;
        // Where we were, to go back if a move fails.
        string lastRoomName;
        bool lastWasPrivate;

        public static bool MultiplayerAvailable => typeof(NetworkRunner).Assembly.GetName().Name == "Fusion.Runtime";
        const string OfflineMessage = "Online play needs Photon Fusion 2 (Nullbound > Set Up Multiplayer). Training works offline.";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            NBUI.BaseMaterial = baseMaterial;
            NBUI.ParticleMaterial = particleMaterial;
            worldSceneIndex = SceneManager.GetActiveScene().buildIndex;
            Time.fixedDeltaTime = 1f / 72f;
        }

        System.Collections.IEnumerator Start()
        {
            if (Instance != this) yield break;
            // Ask for the microphone before going anywhere (the popup pauses the game). Never wait more than 15 s.
            MicPermission.Request();
            float until = Time.realtimeSinceStartup + 15f;
            while (MicPermission.Waiting && Time.realtimeSinceStartup < until) yield return null;
            InMenu = true;
            SetStatus("Welcome to Nullbound!", 3f);
        }

        /// <summary>Leave the match (or training) and go back to the main menu.</summary>
        public void GoToMainMenu() => _ = RunMainMenu();

        async Task RunMainMenu()
        {
            if (Busy) return;
            Busy = true;
            try
            {
                await ShutdownRunner();
                VRRig.Instance?.Detach();
                var op = SceneManager.LoadSceneAsync(worldSceneIndex, LoadSceneMode.Single);
                while (op != null && !op.isDone) await Task.Yield();
                hasConnectedOnce = false; // the next session doesn't need another reload
                IsPrivate = IsTraining = IsSolo = false;
                RoomCode = SessionName = "";
                InMenu = true;
            }
            catch (Exception e) { Debug.LogException(e); }
            Busy = false;
        }

        /// <summary>Kicked or banned by a moderator: back to the main menu, with the reason.</summary>
        public void Removed(string why)
        {
            SetStatus(why, 20f);
            ScreenFx.Message("REMOVED", why, 8f);
            if (IsConnected && !IsTraining) GoToMainMenu();
        }

        /// <summary>Banned (or kicked a few minutes ago): no online play. Training still works.</summary>
        bool OnlineBlocked()
        {
            if (!Moderation.IsBanned(out string why)) return false;
            SetStatus(why + ". Training still works.", 8f);
            AudioManager.Play(AudioManager.Sfx.Error);
            return true;
        }

        void Update()
        {
            int others = 0;
            foreach (var f in Fighter.All) if (f.IsReady && f != Fighter.Local && !f.Down) others++;
            AudioManager.InBattle = others > 0;
            MapSky.Update(InMenu, CurrentMap);
            FillWithBots();
        }

        /// <summary>Quick Match keeps the Battleground busy: bots fill up to this many fighters, and leave as people join.</summary>
        public const int BattlegroundFighters = 8;
        /// <summary>At most this many bots roam the Battleground at once.</summary>
        public const int BattlegroundBots = 5;
        float nextBotCheck;

        /// <summary>
        /// One game (the room's master client) tops Quick Match up with bots so there's always someone to fight, and takes
        /// them out again as real players join. Bots don't pay coins. (Private lobbies and Training add bots by hand.)
        /// </summary>
        void FillWithBots()
        {
            if (!IsConnected || IsTraining || IsSolo || IsPrivate || InMenu || CurrentMap != ArenaMap.Battleground) return;
            if (Time.time < nextBotCheck) return;
            nextBotCheck = Time.time + 2.5f;
            if (!Runner.IsSharedModeMasterClient || Fighter.Local == null) return;
            int people = Runner.ActivePlayers.Count();
            int bots = Fighter.All.Count(f => f.IsBot);
            int want = Mathf.Clamp(BattlegroundFighters - people, 0, BattlegroundBots);
            if (bots < want) SpawnBot();
            else if (bots > want)
            {
                var mine = Fighter.All.FirstOrDefault(f => f.IsBot && f.IsMine);
                if (mine != null) Runner.Despawn(mine.Object);
            }
        }

        // ---------------- public API (menu)

        public void JoinPublic()
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
            if (OnlineBlocked()) return;
            _ = RunConnect(ConnectPublic);
        }

        public void CreatePrivateLobby()
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
            if (OnlineBlocked()) return;
            _ = RunConnect(CreatePrivate);
        }

        public void JoinCode(string code)
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
            code = (code ?? "").Trim();
            if (code.Length != 6 || !code.All(char.IsDigit) || code[0] == '0') { SetStatus("Codes are 6 digits", 3f); return; }
            if (OnlineBlocked()) return;
            if (IsConnected && code == RoomCode) { SetStatus("You're already in " + NBUI.FormatCode(code), 3f); return; }
            _ = RunConnect(() => ConnectCode(code));
        }

        public void StartTraining()
        {
            _ = RunTraining(false);
        }

        public void AddBot()
        {
            if (!CanUseBots) { SetStatus("Bots are for Training and private lobbies", 3f); return; }
            int bots = Fighter.All.Count(f => f.IsBot && f.IsMine);
            if (bots >= MaxBots) { SetStatus("That's the most bots you can add (" + MaxBots + ")", 3f); return; }
            SpawnBot();
        }

        public void RemoveBots()
        {
            if (!IsConnected) return;
            foreach (var f in Fighter.All.ToArray())
                if (f.IsBot && f.IsMine) Runner.Despawn(f.Object);
        }

        public string DescribeRoom()
        {
            if (Busy) return string.IsNullOrEmpty(Status) ? "Connecting..." : Status;
            if (!IsConnected) return InMenu ? "MAIN MENU" : "Offline";
            int bots = Fighter.All.Count(f => f.IsBot);
            string botText = bots > 0 ? "  +" + bots + " bot" + (bots > 1 ? "s" : "") : "";
            if (IsTraining) return "TRAINING (just you)" + botText;
            if (IsSolo) return "SOLO - couldn't go online: " + OnlineError + botText;
            if (IsPrivate) return "PRIVATE LOBBY " + NBUI.FormatCode(RoomCode) + "  " + PlayerCount + " player(s)" + botText;
            return "QUICK MATCH " + NBUI.FormatCode(RoomCode) + "  " + PlayerCount + "/" + MaxPlayers + " players";
        }

        public void SetStatus(string text, float seconds = 4f)
        {
            Status = text;
            statusUntil = Time.time + seconds;
            Debug.Log("[Nullbound] " + text);
        }

        // ---------------- connecting

        async Task RunConnect(Func<Task<bool>> attempt)
        {
            if (Busy) return;
            Busy = true;
            bool ok;
            try { ok = await attempt(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Connection error: " + e.Message);
                ok = false;
            }
            Busy = false;
            if (ok || IsConnected) return;

            // Didn't work: back to the room you were in, or training if there wasn't one.
            await Task.Delay(1500);
            if (Busy || IsConnected) return;
            if (!string.IsNullOrEmpty(lastRoomName) && MultiplayerAvailable)
            {
                string name = lastRoomName;
                bool priv = lastWasPrivate;
                lastRoomName = null;
                Busy = true;
                StartGameResult r = null;
                try { r = await StartSession(Args(name, priv), priv, false); }
                catch (Exception e) { Debug.LogException(e); }
                Busy = false;
                if (r != null && r.Ok) return;
            }
            OnlineError = Status.Replace("Couldn't connect: ", "");
            await RunMainMenu();
            SetStatus("Couldn't go online: " + OnlineError + ". Training works offline.", 10f);
        }

        StartGameArgs Args(string name, bool isPrivate)
        {
            return new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = name,
                SessionNameGenerator = () => PublicPrefix + NewCode(false),
                SessionProperties = new Dictionary<string, SessionProperty> { [ModeProperty] = 0, [KindProperty] = isPrivate ? 1 : 0 },
                IsVisible = !isPrivate,
                PlayerCount = MaxPlayers,
            };
        }

        static string NewCode(bool isPrivate)
        {
            int first = isPrivate ? UnityEngine.Random.Range(5, 10) : UnityEngine.Random.Range(1, 5);
            return first + UnityEngine.Random.Range(0, 100000).ToString("00000");
        }

        async Task<bool> ConnectPublic()
        {
            SetStatus("Finding a match...", 30f);
            // No name: Photon puts us in the fullest open public room, or makes a new one.
            var result = await StartSession(Args(null, false), false, false);
            if (!result.Ok) { SetStatus("Couldn't connect: " + Explain(result), 8f); return false; }
            SetStatus("Joined match " + NBUI.FormatCode(RoomCode) + ". FIGHT!", 5f);
            return true;
        }

        async Task<bool> CreatePrivate()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string code = NewCode(true);
                SetStatus("Creating private lobby...", 30f);
                var result = await StartSession(Args(PrivatePrefix + code, true), true, false);
                if (!result.Ok) { SetStatus("Couldn't create lobby: " + Explain(result), 8f); return false; }
                if (Runner.ActivePlayers.Count() <= 1)
                {
                    SetStatus("Private lobby made! Code: " + NBUI.FormatCode(code), 12f);
                    return true;
                }
                await ShutdownRunner(); // someone already uses that code
            }
            SetStatus("Couldn't find a free code, try again");
            return false;
        }

        async Task<bool> ConnectCode(string code)
        {
            bool isPrivate = code[0] >= '5';
            SetStatus("Joining " + NBUI.FormatCode(code) + "...", 30f);
            var result = await StartSession(Args((isPrivate ? PrivatePrefix : PublicPrefix) + code, isPrivate), isPrivate, false);
            if (!result.Ok)
            {
                SetStatus(result.ShutdownReason == ShutdownReason.GameIsFull ? NBUI.FormatCode(code) + " is full" : "Couldn't join: " + Explain(result), 6f);
                return false;
            }
            // Shared Mode makes the room if it doesn't exist: alone in it means nobody's using that code.
            if (Runner.ActivePlayers.Count() <= 1)
            {
                await ShutdownRunner();
                SetStatus("Nobody is in " + NBUI.FormatCode(code) + ". Check the code?", 6f);
                return false;
            }
            SetStatus("Joined " + NBUI.FormatCode(code), 5f);
            return true;
        }

        /// <summary>Training (or solo when online failed): a one-player session with a bot to fight.</summary>
        async Task RunTraining(bool becauseOffline)
        {
            if (Busy) return;
            Busy = true;
            try
            {
                SetStatus(becauseOffline ? "Couldn't go online, playing Training" : "Starting training...", 6f);
                botsToSpawn = 1;
                var result = await StartSession(new StartGameArgs
                {
                    GameMode = GameMode.Single,
                    SessionName = "NB_TRAINING",
                    PlayerCount = 1,
                }, false, true);
                IsSolo = becauseOffline && result.Ok;
                IsTraining = !becauseOffline && result.Ok;
                if (!result.Ok) SetStatus("Couldn't start the game: " + result.ShutdownReason + " " + result.ErrorMessage, 30f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Couldn't start the game: " + e.Message, 30f);
            }
            Busy = false;
        }

        async Task<StartGameResult> StartSession(StartGameArgs args, bool isPrivate, bool training)
        {
            if (IsConnected && !IsTraining && !IsSolo && !string.IsNullOrEmpty(SessionName))
            {
                lastRoomName = SessionName;
                lastWasPrivate = IsPrivate;
            }
            await ShutdownRunner();
            if (hasConnectedOnce)
            {
                VRRig.Instance?.Detach();
                var op = SceneManager.LoadSceneAsync(worldSceneIndex, LoadSceneMode.Single);
                while (op != null && !op.isDone) await Task.Yield();
            }
            hasConnectedOnce = true;

            InMenu = false;
            CurrentMap = training ? ArenaMap.TrainingGrounds : ArenaMap.Battleground;
            IsPrivate = isPrivate;
            IsTraining = false;
            IsSolo = false;
            RoomCode = "";
            SessionName = args.SessionName ?? "";
            if (!training) botsToSpawn = 0;

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
            if (!result.Ok) { Runner = null; return result; }
            NoteRoomName();
            return result;
        }

        void NoteRoomName()
        {
            var info = Runner != null ? Runner.SessionInfo : null;
            if (info != null && info.IsValid && !string.IsNullOrEmpty(info.Name)) SessionName = info.Name;
            if (SessionName.StartsWith(PublicPrefix)) RoomCode = SessionName.Substring(PublicPrefix.Length);
            else if (SessionName.StartsWith(PrivatePrefix)) RoomCode = SessionName.Substring(PrivatePrefix.Length);
        }

        async Task ShutdownRunner()
        {
            if (Runner == null) return;
            VRRig.Instance?.Detach();
            shuttingDownOnPurpose = true;
            var runner = Runner;
            Runner = null;
            if (runner.IsRunning) await runner.Shutdown();
            else if (runner != null) Destroy(runner.gameObject);
            shuttingDownOnPurpose = false;
        }

        static string Explain(StartGameResult result)
        {
            switch (result.ShutdownReason)
            {
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                    return "the Photon App ID is missing or wrong (Nullbound > Set Up Multiplayer, then build again)";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                case ShutdownReason.OperationTimeout:
                    return "no internet connection (check the headset's Wi-Fi)";
                default:
                    return result.ShutdownReason + (string.IsNullOrEmpty(result.ErrorMessage) ? "" : " - " + result.ErrorMessage);
            }
        }

        void SpawnBot()
        {
            if (!IsConnected || fighterPrefab == null) return;
            Fighter.SpawningBot = true;
            Fighter.SpawningBotKind = (FighterKind)UnityEngine.Random.Range(0, FighterInfo.Count);
            try { Runner.Spawn(fighterPrefab, SpawnPoint.PickForBot(Vector3.zero), Quaternion.identity, Runner.LocalPlayer); }
            catch (Exception e) { Debug.LogError("[Nullbound] Couldn't spawn a bot: " + e.Message); }
            finally { Fighter.SpawningBot = false; }
        }

        void OnApplicationQuit()
        {
            if (Runner != null) { shuttingDownOnPurpose = true; Runner.Shutdown(); }
        }

        // ---------------- Fusion callbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (player != runner.LocalPlayer) return;
            if (runner == Runner) NoteRoomName();
            if (fighterPrefab == null)
            {
                Debug.LogError("[Nullbound] SessionManager is missing the Fighter prefab. Run Nullbound > Build Prototype World.");
                return;
            }
            try { runner.Spawn(fighterPrefab, SpawnPoint.Pick(Vector3.up), Quaternion.identity, player); }
            catch (Exception e) { Debug.LogError("[Nullbound] Couldn't spawn your fighter: " + e.Message); }
            for (int i = 0; i < botsToSpawn; i++) SpawnBot();
            botsToSpawn = 0;
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (shuttingDownOnPurpose) return;
            if (Runner != null && runner != Runner) return;
            Runner = null;
            VRRig.Instance?.Detach();
            SetStatus("Disconnected (" + shutdownReason + ")", 8f);
            Invoke(nameof(AfterDrop), 1f);
        }

        void AfterDrop()
        {
            if (!IsConnected && !Busy) GoToMainMenu();
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
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
