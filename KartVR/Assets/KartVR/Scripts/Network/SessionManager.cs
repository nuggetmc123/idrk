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
    /// Owns the Photon Fusion connection (Shared Mode) and moves you between rooms.
    ///
    /// Public play: every room has a random 6-digit code. Going to a map (or the hub) puts you in the fullest room
    /// on that map that still has space; only when they're all full is a new room with a new random code made.
    /// Friends can join you anywhere by typing your room code on the hub keypad.
    ///
    /// Private lobbies: a code of your own that nobody is matched into. There's no host: everyone in the lobby can
    /// go to any map on their own and stays in the lobby (each map of a lobby is its own room, named after the
    /// lobby code). Typing a lobby code takes you to wherever most of that lobby is.
    ///
    /// Public codes start with 1-4, private lobby codes with 5-9, so one keypad joins either.
    /// Shared Mode keeps a room alive as long as anyone is in it.
    /// </summary>
    public class SessionManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static SessionManager Instance { get; private set; }

        const string PrivatePrefix = "KARTVR_PRIV_";
        const string PublicPrefix = "KARTVR_PUB_";

        /// <summary>Players per room: one for each garage in the hub.</summary>
        public const int MaxPlayers = 6;
        const string ModeProperty = "mode", KindProperty = "kind", CodeProperty = "code";

        [Tooltip("The Kart prefab (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject kartPrefab;

        [Tooltip("The on-foot Player prefab used in the hub (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject playerPrefab;


        [Tooltip("Things moderators spawn from the Spawn Menu (must have a NetworkObject). The builder fills this in.")]
        public NetworkObject propPrefab;

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
        /// <summary>The private lobby's code (only while <see cref="IsPrivate"/>).</summary>
        public string LobbyCode { get; private set; }
        /// <summary>The code friends type to join you: your public room's code, or your private lobby's.</summary>
        public string RoomCode => IsPrivate ? LobbyCode : publicCode;
        string publicCode = "";
        /// <summary>The Photon session we're in (part of what each player signs to prove their ID).</summary>
        public string SessionName { get; private set; } = "";
        public bool Busy { get; private set; }
        /// <summary>The mode we asked for when we connected. The first player in a session uses it to set GameState.Mode.</summary>
        public KartMode RequestedMode { get; private set; }

        public string Status { get; private set; } = "Starting...";
        float statusUntil;
        public string TransientStatus => Time.time < statusUntil ? Status : null;

        public int PlayerCount => IsConnected ? Runner.ActivePlayers.Count() : 0;

        int worldSceneIndex;
        // Where we were before the current move, to go back if the move fails (a wrong code, a full map...).
        bool lastPrivate;
        string lastCode = "";
        KartMode lastMode = KartMode.Hub;
        bool lastValid;
        List<SessionInfo> sessionList;
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

        // ---------- Public API (called by the map board, keypad, wrist menu) ----------

        /// <summary>Go to a map in a public room: the fullest one with space, or a new one if they're all full.</summary>
        public void JoinPublic(KartMode mode)
        {
            _ = RunConnect(() => ConnectPublic(mode), mode);
        }

        /// <summary>False until Photon Fusion 2 is imported (the game then runs solo, offline).</summary>
        public static bool MultiplayerAvailable => typeof(NetworkRunner).Assembly.GetName().Name == "Fusion.Runtime";

        const string OfflineMessage = "Playing OFFLINE (solo). Import Photon Fusion 2 to play online with friends.";

        public void CreatePrivateLobby()
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
            _ = RunConnect(CreatePrivate);
        }

        /// <summary>Join a friend by their room code (public room) or lobby code (private lobby), on whatever map they're on.</summary>
        public void JoinCode(string code)
        {
            if (!MultiplayerAvailable) { SetStatus(OfflineMessage, 6f); return; }
            code = (code ?? "").Trim();
            if (code.Length != 6 || !code.All(char.IsDigit) || code[0] == '0')
            {
                SetStatus("Codes are 6 digits", 3f);
                return;
            }
            if (IsConnected && !IsSolo && code == RoomCode)
            {
                SetStatus("You're already in " + KartUI.FormatCode(code), 3f);
                return;
            }
            _ = RunConnect(() => ConnectToCode(code));
        }

        public void LeaveToPublicHub()
        {
            JoinPublic(KartMode.Hub);
        }

        /// <summary>
        /// Go to a map. Public: a public room on that map. Private lobby: that map in your lobby (just you; the rest
        /// of the lobby stays where they are). Solo / tutorial: the map changes on this headset.
        /// </summary>
        public void Travel(KartMode mode)
        {
            if (IsPrivate)
            {
                string code = LobbyCode;
                _ = RunConnect(() => ConnectPrivate(code, mode));
            }
            else JoinPublic(mode);
        }

        /// <summary>"Go to the hub" button: your lobby's hub in a private lobby, otherwise a public hub.</summary>
        public void GoToHub()
        {
            if (IsSolo && GameState.Instance != null && GameState.Instance.IsReady)
                GameState.Instance.RequestMode(KartMode.Hub);
            else
                Travel(KartMode.Hub);
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
                return "PLAYING SOLO - " + mode.ToUpper() + "\nCouldn't go online: " + OnlineError + "\nTouch a map or FIND A ROOM to try again.";
            if (IsPrivate)
                return "PRIVATE LOBBY  " + KartUI.FormatCode(LobbyCode) + "\n" + mode + " - " + PlayerCount + " here (anyone can go to any map)";
            return "ROOM " + KartUI.FormatCode(RoomCode) + "  -  " + mode.ToUpper() + "\n" + PlayerCount + "/" + MaxPlayers + " players";
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
            // A wrong code, a full map etc.: back to where you were.
            await Task.Delay(2500);
            if (Busy || IsConnected) return;
            if (lastValid && lastPrivate)
            {
                string code = lastCode;
                KartMode mode = lastMode;
                _ = RunConnect(() => ConnectPrivate(code, mode), KartMode.Hub);
            }
            else if (lastValid && !string.IsNullOrEmpty(lastCode))
            {
                string code = lastCode;
                KartMode mode = lastMode;
                _ = RunConnect(() => ConnectPublicRoom(code, mode), mode);
            }
            else JoinPublic(KartMode.Hub);
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

        static string NewCode(bool isPrivate)
        {
            int first = isPrivate ? UnityEngine.Random.Range(5, 10) : UnityEngine.Random.Range(1, 5);
            return first + UnityEngine.Random.Range(0, 100000).ToString("00000");
        }

        static bool IsPrivateCode(string code) => code.Length > 0 && code[0] >= '5';

        StartGameArgs PublicArgs(KartMode mode, string code)
        {
            return new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = code == null ? null : PublicPrefix + code,
                // With no name, Photon puts you in the fullest open room with these properties, and only makes a
                // new one (named by the generator, so with a fresh random code) when they're all full.
                SessionNameGenerator = () => PublicPrefix + NewCode(false),
                SessionProperties = new Dictionary<string, SessionProperty> { [ModeProperty] = (int)mode, [KindProperty] = 0 },
                IsVisible = true,
                PlayerCount = MaxPlayers,
            };
        }

        StartGameArgs PrivateArgs(string code, KartMode mode)
        {
            return new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = PrivatePrefix + code + "_" + (int)mode,
                // Listed (so a lobby code can be found on whichever map its players are), but never matched into:
                // public matchmaking only looks for kind 0.
                SessionProperties = new Dictionary<string, SessionProperty> { [ModeProperty] = (int)mode, [KindProperty] = 1, [CodeProperty] = code },
                IsVisible = true,
                PlayerCount = MaxPlayers,
            };
        }

        async Task<bool> ConnectPublic(KartMode mode)
        {
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
            SetStatus("Finding a room (" + KartModeInfo.DisplayName(mode) + ")...", 30f);
            var result = await StartSession(PublicArgs(mode, null), mode, false, null);
            if (!result.Ok)
            {
                SetStatus("Couldn't connect: " + Explain(result), 8f);
                return false;
            }
            SetStatus("Joined room " + KartUI.FormatCode(publicCode) + " (" + KartModeInfo.DisplayName(mode) + ")", 5f);
            return true;
        }

        /// <summary>A public room by its code (going back to it, or a friend's room).</summary>
        async Task<bool> ConnectPublicRoom(string code, KartMode mode)
        {
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
            SetStatus("Joining room " + KartUI.FormatCode(code) + "...", 30f);
            var result = await StartSession(PublicArgs(mode, code), mode, false, null);
            if (!result.Ok)
            {
                SetStatus(result.ShutdownReason == ShutdownReason.GameIsFull ? "Room " + KartUI.FormatCode(code) + " is full (6 players)"
                    : "Couldn't join: " + Explain(result), 6f);
                return false;
            }
            SetStatus("Joined room " + KartUI.FormatCode(code), 5f);
            return true;
        }

        /// <summary>A map in a private lobby (made if nobody in the lobby is on it yet).</summary>
        async Task<bool> ConnectPrivate(string code, KartMode mode)
        {
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
            SetStatus("Going to " + KartModeInfo.DisplayName(mode) + " in your lobby...", 30f);
            var result = await StartSession(PrivateArgs(code, mode), mode, true, code);
            if (!result.Ok)
            {
                SetStatus(result.ShutdownReason == ShutdownReason.GameIsFull ? KartModeInfo.DisplayName(mode) + " is full in your lobby (6 players)"
                    : "Couldn't connect: " + Explain(result), 6f);
                return false;
            }
            SetStatus("Lobby " + KartUI.FormatCode(code) + ": " + KartModeInfo.DisplayName(mode), 5f);
            return true;
        }

        async Task<bool> CreatePrivate()
        {
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string code = NewCode(true);
                SetStatus("Creating private lobby...", 30f);
                var result = await StartSession(PrivateArgs(code, KartMode.Hub), KartMode.Hub, true, code);
                if (!result.Ok)
                {
                    SetStatus("Couldn't create lobby: " + Explain(result));
                    return false;
                }
                // Somebody's already in a room with this code: pick another.
                if (Runner.ActivePlayers.Count() <= 1)
                {
                    SetStatus("Private lobby created! Code: " + KartUI.FormatCode(code), 10f);
                    return true;
                }
                await ShutdownRunner();
            }
            SetStatus("Couldn't find a free code, try again");
            return false;
        }

        /// <summary>
        /// Find a code's room in Photon's room list and join it. A lobby code goes to the map most of the lobby is on.
        /// </summary>
        async Task<bool> ConnectToCode(string code)
        {
            if (OnlineBlocked(out string blocked)) { SetStatus("Couldn't connect: " + blocked, 15f); return false; }
            SetStatus("Looking for " + KartUI.FormatCode(code) + "...", 30f);
            var rooms = await ListRooms();
            bool isPrivate = IsPrivateCode(code);
            if (rooms == null)
            {
                // Couldn't get the list: try the room directly (a lobby's hub for lobby codes).
                return isPrivate ? await ConnectPrivate(code, KartMode.Hub) : await ConnectPublicRoom(code, KartMode.Hub);
            }
            string publicName = PublicPrefix + code, privatePrefix = PrivatePrefix + code + "_";
            var matches = rooms.Where(r => r != null && r.IsValid && (r.Name == publicName || r.Name.StartsWith(privatePrefix))).ToList();
            if (matches.Count == 0)
            {
                SetStatus("Nobody is in " + KartUI.FormatCode(code) + " right now. Check the code?", 6f);
                return false;
            }
            var open = matches.Where(r => r.IsOpen && r.PlayerCount < MaxPlayers).ToList();
            if (open.Count == 0)
            {
                SetStatus(KartUI.FormatCode(code) + " is full (6 players)", 6f);
                return false;
            }
            // Where most of them are (the hub if it's a tie).
            var best = open.OrderByDescending(r => r.PlayerCount).ThenBy(r => ModeOf(r) == KartMode.Hub ? 0 : 1).First();
            var mode = ModeOf(best);
            return best.Name.StartsWith(PrivatePrefix) ? await ConnectPrivate(code, mode) : await ConnectPublicRoom(code, mode);
        }

        static KartMode ModeOf(SessionInfo room)
        {
            if (room.Properties != null && room.Properties.TryGetValue(ModeProperty, out var p) && p != null)
            {
                int m = p;
                if (Enum.IsDefined(typeof(KartMode), (KartMode)m)) return (KartMode)m;
            }
            return KartMode.Hub;
        }

        /// <summary>Photon's list of open rooms (we have to leave the current room to see it). Null if it can't be had.</summary>
        async Task<List<SessionInfo>> ListRooms()
        {
            RememberRoom();
            await ShutdownRunner();
            var go = new GameObject("LobbyRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<NetworkRunner>();
            runner.AddCallbacks(this);
            sessionList = null;
            try
            {
                var result = await runner.JoinSessionLobby(SessionLobby.Shared);
                if (!result.Ok) return null;
                // The list arrives a moment after joining the lobby.
                float until = Time.realtimeSinceStartup + 6f;
                while (sessionList == null && Time.realtimeSinceStartup < until) await Task.Yield();
                return sessionList ?? new List<SessionInfo>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KartVR] Couldn't list rooms: " + e.Message);
                return null;
            }
            finally
            {
                shuttingDownOnPurpose = true;
                try { if (runner != null && runner.IsRunning) await runner.Shutdown(); else if (runner != null) Destroy(runner.gameObject); }
                catch (Exception) { }
                shuttingDownOnPurpose = false;
            }
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

        async Task<StartGameResult> StartSession(StartGameArgs args, KartMode mode, bool isPrivate, string code)
        {
            RememberRoom();
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
            SessionName = args.SessionName ?? "";
            RequestedMode = mode;
            IsPrivate = isPrivate;
            IsSolo = false;
            if (IsTutorial) { IsTutorial = false; Tutorial.Stop(); } // left the tutorial early: you'll get it again next launch
            LobbyCode = code;
            publicCode = "";

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
                return result;
            }
            NoteRoomName();
            return result;
        }

        /// <summary>Note the room we're leaving, to come back to if the move fails.</summary>
        void RememberRoom()
        {
            if (!IsConnected || IsSolo || IsTutorial) return;
            lastValid = true;
            lastPrivate = IsPrivate;
            lastCode = RoomCode ?? "";
            lastMode = GameState.CurrentMode;
        }

        /// <summary>The real room name (a random public room gets its name from Photon) and the code in it.</summary>
        void NoteRoomName()
        {
            var info = Runner != null ? Runner.SessionInfo : null;
            string name = info != null && info.IsValid && !string.IsNullOrEmpty(info.Name) ? info.Name : SessionName;
            SessionName = name ?? "";
            if (!IsPrivate && SessionName.StartsWith(PublicPrefix)) publicCode = SessionName.Substring(PublicPrefix.Length);
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
            if (runner == Runner) NoteRoomName();
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
            if (IsConnected || Busy) return;
            // Back into your private lobby if you were in one, otherwise a public hub.
            if (!string.IsNullOrEmpty(LobbyCode) && IsPrivate)
            {
                string code = LobbyCode;
                _ = RunConnect(() => ConnectPrivate(code, KartMode.Hub), KartMode.Hub);
            }
            else JoinPublic(KartMode.Hub);
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
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            this.sessionList = sessionList != null ? new List<SessionInfo>(sessionList) : new List<SessionInfo>();
        }
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
