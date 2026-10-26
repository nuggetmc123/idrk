using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Online races with Photon Fusion 2 (Shared Mode, up to 8 players a room). QUICK RACE ONLINE puts you in the
    /// fullest open room (or makes one on your current map); CREATE PRIVATE ROOM gives you a 6-digit code to share;
    /// JOIN WITH CODE joins a friend. Each player simulates their own car; the room's race state starts everyone
    /// together. Public codes start with 1-4, private with 5-9. Without Photon Fusion installed this is a stand-in
    /// and the menu says how to switch it on.
    /// </summary>
    public class Online : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static Online I { get; private set; }
        public static readonly List<NetCar> Cars = new List<NetCar>();
        public const int MaxPlayers = 8;
        const string PublicPrefix = "VX_PUB_", PrivatePrefix = "VX_PRIV_", MapProperty = "map", LapsProperty = "laps";

        [Tooltip("NetCar prefab (NetworkObject + NetCar). The world builder fills this in.")]
        public NetworkObject netCarPrefab;
        [Tooltip("NetRace prefab (NetworkObject + NetRace).")]
        public NetworkObject netRacePrefab;

        public NetworkRunner Runner { get; private set; }
        public bool Connected => Runner != null && Runner.IsRunning;
        public bool Busy { get; private set; }
        public string RoomCode { get; private set; } = "";
        public string Status { get; private set; } = "";
        public int roomMap, roomLaps = 5;
        bool leaving;
        string typedCode = "";

        public static bool Available => typeof(NetworkRunner).Assembly.GetName().Name == "Fusion.Runtime";

        void Awake() { I = this; }

        public void Boot() { }

        // ------------------------------------------------------------------ menu

        public void BuildMenu(Transform panel, Menus menus)
        {
            float x = -0.3f, y = 0.17f;
            VButton.Make(panel, "QUICK RACE ONLINE", new Vector2(x, y), new Vector2(0.44f, 0.08f), () => _ = QuickRace(), true, "Join the busiest room, or open one on your current map");
            VButton.Make(panel, "CREATE PRIVATE ROOM", new Vector2(x, y - 0.1f), new Vector2(0.44f, 0.08f), () => _ = CreatePrivate(), false, "Get a code to give your friends");
            VUI.Label(panel, "JOIN WITH CODE", new Vector2(0.06f, y + 0.02f), 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.4f);
            var codeLbl = VUI.Label(panel, Format(typedCode), new Vector2(0.06f, y - 0.03f), 0.5f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.4f);
            // Number pad.
            for (int k = 0; k < 12; k++)
            {
                int col = k % 3, row = k / 3;
                string label = k < 9 ? (k + 1).ToString() : k == 9 ? "DEL" : k == 10 ? "0" : "JOIN";
                int kk = k;
                VButton.Make(panel, label, new Vector2(0.1f + col * 0.09f, y - 0.1f - row * 0.06f), new Vector2(0.08f, 0.05f), () =>
                {
                    if (kk < 9 && typedCode.Length < 6) typedCode += (kk + 1).ToString();
                    else if (kk == 10 && typedCode.Length < 6) typedCode += "0";
                    else if (kk == 9 && typedCode.Length > 0) typedCode = typedCode.Substring(0, typedCode.Length - 1);
                    else if (kk == 11 && typedCode.Length == 6) _ = JoinCode(typedCode);
                    codeLbl.text = Format(typedCode);
                }, kk == 11);
            }
            string status = !Available ? "Photon Fusion 2 isn't installed in this build, so online play is off. Every other mode works offline." :
                Connected ? "In room " + Format(RoomCode) + "  -  " + Cars.Count + " / " + MaxPlayers + " drivers" : Status;
            VUI.Label(panel, status, new Vector2(-0.53f, -0.2f), 0.22f, Available ? VUI.Text : VUI.Warn, TextAlignmentOptions.MidlineLeft, false, 1.0f);
        }

        public static string Format(string code) => string.IsNullOrEmpty(code) ? "------" : code.Length > 3 ? code.Substring(0, 3) + " " + code.Substring(3) : code;

        void SetStatus(string s)
        {
            Status = s;
            Debug.Log("[Veylix] Online: " + s);
            if (Game.I != null && Game.I.menus != null && Game.I.state == Game.State.Menu) Game.I.menus.ShowOnlineStatus(s);
        }

        // ------------------------------------------------------------------ connecting

        static string NewCode(bool isPrivate)
        {
            int first = isPrivate ? UnityEngine.Random.Range(5, 10) : UnityEngine.Random.Range(1, 5);
            return first + UnityEngine.Random.Range(0, 100000).ToString("00000");
        }

        StartGameArgs Args(string name, bool isPrivate)
        {
            var cfg = Game.I.config;
            return new StartGameArgs
            {
                GameMode = Fusion.GameMode.Shared,
                SessionName = name,
                SessionNameGenerator = () => PublicPrefix + NewCode(false),
                SessionProperties = new Dictionary<string, SessionProperty> { [MapProperty] = cfg.map, [LapsProperty] = Mathf.Clamp(cfg.laps, 1, 30) },
                IsVisible = !isPrivate,
                PlayerCount = MaxPlayers,
            };
        }

        async Task QuickRace()
        {
            if (!Available) { SetStatus("Online play is off: Photon Fusion 2 isn't installed."); return; }
            if (Busy) return;
            SetStatus("Finding a race...");
            var r = await Start(Args(null, false));
            if (r != null && r.Ok) AfterJoin();
        }

        async Task CreatePrivate()
        {
            if (!Available) { SetStatus("Online play is off: Photon Fusion 2 isn't installed."); return; }
            if (Busy) return;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string code = NewCode(true);
                SetStatus("Creating a private room...");
                var r = await Start(Args(PrivatePrefix + code, true));
                if (r == null || !r.Ok) return;
                if (Runner.ActivePlayers.Count() <= 1) { AfterJoin(); return; }
                await Shutdown(); // code already in use
            }
            SetStatus("Couldn't find a free code. Try again.");
        }

        async Task JoinCode(string code)
        {
            if (!Available) { SetStatus("Online play is off: Photon Fusion 2 isn't installed."); return; }
            if (Busy) return;
            bool isPrivate = code[0] >= '5';
            SetStatus("Joining " + Format(code) + "...");
            var r = await Start(Args((isPrivate ? PrivatePrefix : PublicPrefix) + code, isPrivate));
            if (r == null || !r.Ok) return;
            if (Runner.ActivePlayers.Count() <= 1)
            {
                await Shutdown();
                SetStatus("Nobody is in room " + Format(code) + ". Check the code?");
                return;
            }
            AfterJoin();
        }

        async Task<StartGameResult> Start(StartGameArgs args)
        {
            Busy = true;
            try
            {
                await Shutdown();
                var go = new GameObject("NetworkRunner");
                DontDestroyOnLoad(go);
                Runner = go.AddComponent<NetworkRunner>();
                Runner.AddCallbacks(this);
                var runner = Runner;
                var start = runner.StartGame(args);
                if (await Task.WhenAny(start, Task.Delay(30000)) != start)
                {
                    Runner = null;
                    try { await Task.WhenAny(runner.Shutdown(), Task.Delay(8000)); } catch (Exception e) { Debug.LogException(e); }
                    if (runner != null) Destroy(runner.gameObject);
                    SetStatus("The game servers didn't answer. Check your Wi-Fi.");
                    return null;
                }
                var result = await start;
                if (!result.Ok) { Runner = null; SetStatus("Couldn't connect: " + Explain(result)); return result; }
                var info = Runner.SessionInfo;
                string name = info != null && info.IsValid ? info.Name : args.SessionName ?? "";
                RoomCode = name.StartsWith(PublicPrefix) ? name.Substring(PublicPrefix.Length) : name.StartsWith(PrivatePrefix) ? name.Substring(PrivatePrefix.Length) : "";
                roomMap = Game.I.config.map;
                roomLaps = Mathf.Clamp(Game.I.config.laps, 1, 30);
                if (info != null && info.Properties != null)
                {
                    if (info.Properties.TryGetValue(MapProperty, out var m)) roomMap = (int)m;
                    if (info.Properties.TryGetValue(LapsProperty, out var l)) roomLaps = Mathf.Max(1, (int)l);
                }
                return result;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Couldn't connect: " + e.Message);
                return null;
            }
            finally { Busy = false; }
        }

        static string Explain(StartGameResult r)
        {
            switch (r.ShutdownReason)
            {
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed: return "the Photon App ID is missing or wrong (Veylix > Import Multiplayer in the editor, then rebuild)";
                case ShutdownReason.GameIsFull: return "that room is full";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                case ShutdownReason.OperationTimeout: return "no internet connection";
                default: return r.ShutdownReason + (string.IsNullOrEmpty(r.ErrorMessage) ? "" : " - " + r.ErrorMessage);
            }
        }

        /// <summary>Connected: load the room's map as an online race, then put our car on the network.</summary>
        void AfterJoin()
        {
            SetStatus("Joined room " + Format(RoomCode) + ". Loading the track...");
            var cfg = Game.I.config.Clone();
            cfg.mode = GameMode.Online;
            cfg.map = roomMap;
            cfg.laps = roomLaps;
            Game.I.StartSession(cfg);
            StartCoroutine(SpawnWhenReady());
        }

        System.Collections.IEnumerator SpawnWhenReady()
        {
            while (Connected && (Game.I.state != Game.State.Session || RaceSession.I == null)) yield return null;
            if (!Connected) yield break;
            RaceSession.I.online = true;
            RaceSession.I.totalLaps = roomLaps;
            if (netCarPrefab != null)
            {
                try { Runner.Spawn(netCarPrefab, Game.I.playerCar.transform.position, Game.I.playerCar.transform.rotation, Runner.LocalPlayer); }
                catch (Exception e) { Debug.LogError("[Veylix] Couldn't spawn your network car: " + e.Message); }
            }
            if (Runner.IsSharedModeMasterClient && NetRace.I == null && netRacePrefab != null)
            {
                try { Runner.Spawn(netRacePrefab, Vector3.zero, Quaternion.identity, Runner.LocalPlayer); }
                catch (Exception e) { Debug.LogError("[Veylix] Couldn't start the race state: " + e.Message); }
            }
            // Space the grid: each driver takes the slot matching their player number.
            int slot = Mathf.Max(0, (Runner.LocalPlayer.PlayerId - 1) % 20);
            var pose = World.Current.grid[Mathf.Min(slot, World.Current.grid.Count - 1)];
            Game.I.playerCar.Teleport(pose.position, pose.rotation);
            RaceSession.I.Message("ONLINE  -  ROOM " + Format(RoomCode) + "  -  race starts when the room is ready", VUI.Text, 6f);
        }

        public void Leave() { _ = Shutdown(); }

        async Task Shutdown()
        {
            if (Runner == null) return;
            leaving = true;
            var r = Runner;
            Runner = null;
            try { if (r.IsRunning) await Task.WhenAny(r.Shutdown(), Task.Delay(8000)); if (r != null) Destroy(r.gameObject); }
            finally { leaving = false; Cars.Clear(); RoomCode = ""; }
        }

        void OnApplicationQuit() { if (Runner != null) { leaving = true; Runner.Shutdown(); } }

        void Update()
        {
            // Host: A starts the race from the grid. Everyone: see the lobby countdown.
            var nr = NetRace.I;
            var s = RaceSession.I;
            if (nr == null || s == null || !s.online) return;
            if (nr.Phase == NetRace.Lobby && s.phase == RaceSession.Phase.Grid)
            {
                if (Time.frameCount % 30 == 0)
                    s.Message(Cars.Count < 2 ? "WAITING FOR DRIVERS  (" + Cars.Count + "/" + MaxPlayers + ")  -  ROOM " + Format(RoomCode) : nr.IsMaster ? "PRESS A TO START  -  auto start in " + Mathf.CeilToInt(nr.SecondsLeft) + "s" : "RACE STARTS IN " + Mathf.CeilToInt(nr.SecondsLeft) + "s", VUI.Text, 1f);
                if (nr.IsMaster && (InputHub.Right.primaryDown || InputHub.KeyDown(UnityEngine.InputSystem.Key.Enter))) nr.StartRace();
            }
        }

        // ------------------------------------------------------------------ Fusion callbacks

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (leaving) return;
            Runner = null;
            Cars.Clear();
            Debug.Log("[Veylix] Disconnected: " + shutdownReason);
            if (Game.I != null && Game.I.config.mode == GameMode.Online && Game.I.state != Game.State.Menu)
            {
                if (RaceSession.I != null) RaceSession.I.Message("DISCONNECTED (" + shutdownReason + ")", VUI.Accent, 6f);
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
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
        // Fusion versions differ on this one's last parameter: implement both.
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
