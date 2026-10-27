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
    /// Online races with Photon Fusion 2 (Shared Mode, up to 6 players a room).
    /// FIND RACE (the garage door, see RaceFinder): everyone searching waits in a matchmaking room until its host sends
    /// the room to a race.
    /// FIND A RACE: matchmaking on the track you've picked (joins a race filling up there, or opens one and waits).
    /// ANY TRACK: joins whichever race is filling up. CREATE PRIVATE ROOM / JOIN WITH CODE: race friends by code.
    /// PARTY: a lobby you sit in from the garage with friends (code starts with 0). The leader picks the track and the
    /// whole party goes: PARTY ONLY (a private race) or PARTY VS OTHERS (a public race strangers matchmake into).
    /// After the race everyone drops back into the party. Codes: party 0, public 1-4, private 5-9.
    /// Each player simulates their own car; the room's race state starts everyone together. Without Photon Fusion
    /// installed this is a stand-in and the menu says how to switch it on.
    /// </summary>
    public class Online : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static Online I { get; private set; }
        public static readonly List<NetCar> Cars = new List<NetCar>();
        public const int MaxPlayers = RaceFinder.MaxRacers;
        const string PublicPrefix = "VX_PUB_", PrivatePrefix = "VX_PRIV_", PartyPrefix = "VX_PARTY_", LobbyPrefix = "VX_MM_", MatchProperty = "mm", MapProperty = "map", LapsProperty = "laps";
        const string GoProperty = "go", GoMapProperty = "gmap", GoLapsProperty = "glaps";

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
            var cfg = Game.I.config;
            var map = MapCatalog.Get(cfg.map);
            float x = -0.38f, y = 0.19f;
            VUI.Label(panel, "RACE ONLINE", new Vector2(x - 0.17f, y + 0.055f), 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.4f).characterSpacing = 4f;
            VButton.Make(panel, "FIND A RACE", new Vector2(x, y), new Vector2(0.34f, 0.075f), () => _ = FindRace(true), true, map.name + ": join drivers there, or wait for them");
            VButton.Make(panel, "ANY TRACK", new Vector2(x, y - 0.085f), new Vector2(0.34f, 0.075f), () => _ = FindRace(false), false, "Join whichever race is filling up");
            VButton.Make(panel, "PRIVATE ROOM", new Vector2(x, y - 0.17f), new Vector2(0.34f, 0.075f), () => _ = CreatePrivate(), false, "A code for friends, no strangers");

            // Party.
            float px = 0.0f;
            VUI.Label(panel, "PARTY", new Vector2(px - 0.17f, y + 0.055f), 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.4f).characterSpacing = 4f;
            if (!InParty)
            {
                VButton.Make(panel, "CREATE PARTY", new Vector2(px, y), new Vector2(0.34f, 0.075f), () => _ = CreateParty(), true, "Friends join with your code");
                VUI.Label(panel, "In a party the leader picks the track and everyone goes together: just the party, or the party against other players.", new Vector2(px - 0.17f, y - 0.09f), 0.18f, VUI.TextDim, TextAlignmentOptions.TopLeft, false, 0.34f);
            }
            else
            {
                VUI.Label(panel, "CODE  " + Format(PartyCode), new Vector2(px - 0.17f, y + 0.01f), 0.36f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.36f);
                VUI.Label(panel, PartySize + (PartySize == 1 ? " driver" : " drivers") + "  -  " + (IsPartyLeader ? "you lead" : "following the leader"), new Vector2(px - 0.17f, y - 0.035f), 0.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.36f);
                if (IsPartyLeader)
                {
                    VButton.Make(panel, "PARTY ONLY", new Vector2(px, y - 0.09f), new Vector2(0.34f, 0.07f), () => _ = PartyGo(cfg, false), true, map.name + ", just your party");
                    VButton.Make(panel, "PARTY VS OTHERS", new Vector2(px, y - 0.17f), new Vector2(0.34f, 0.07f), () => _ = PartyGo(cfg, true), false, map.name + ", open to other players");
                }
                else VUI.Label(panel, "Waiting for the leader to pick a race...", new Vector2(px - 0.17f, y - 0.1f), 0.2f, VUI.Warn, TextAlignmentOptions.MidlineLeft, false, 0.36f);
                VButton.Make(panel, "LEAVE PARTY", new Vector2(px, y - 0.25f), new Vector2(0.34f, 0.05f), () => { PartyCode = ""; _ = Shutdown(); SetStatus("Left the party."); });
            }

            // Join with a code (rooms and parties).
            float cx = 0.27f;
            VUI.Label(panel, "JOIN WITH CODE", new Vector2(cx, y + 0.055f), 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f).characterSpacing = 4f;
            var codeLbl = VUI.Label(panel, Format(typedCode), new Vector2(cx, y + 0.005f), 0.42f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.3f);
            for (int k = 0; k < 12; k++)
            {
                int col = k % 3, row = k / 3;
                string label = k < 9 ? (k + 1).ToString() : k == 9 ? "DEL" : k == 10 ? "0" : "JOIN";
                int kk = k;
                VButton.Make(panel, label, new Vector2(cx + 0.04f + col * 0.085f, y - 0.065f - row * 0.058f), new Vector2(0.078f, 0.05f), () =>
                {
                    if (kk < 9 && typedCode.Length < 6) typedCode += (kk + 1).ToString();
                    else if (kk == 10 && typedCode.Length < 6) typedCode += "0";
                    else if (kk == 9 && typedCode.Length > 0) typedCode = typedCode.Substring(0, typedCode.Length - 1);
                    else if (kk == 11 && typedCode.Length == 6) _ = JoinCode(typedCode);
                    codeLbl.text = Format(typedCode);
                }, kk == 11);
            }
            string status = !Available ? "Photon Fusion 2 isn't installed in this build, so online play is off. Every other mode works offline." :
                Connected && !InParty ? "In room " + Format(RoomCode) + "  -  " + Cars.Count + " / " + MaxPlayers + " drivers" : Status;
            VUI.Label(panel, status, new Vector2(-0.53f, -0.24f), 0.2f, Available ? VUI.Text : VUI.Warn, TextAlignmentOptions.MidlineLeft, false, 1.0f);
        }

        public static string Format(string code) => string.IsNullOrEmpty(code) ? "------" : code.Length > 3 ? code.Substring(0, 3) + " " + code.Substring(3) : code;

        void SetStatus(string s)
        {
            Status = s;
            Debug.Log("[Veylix] Online: " + s);
            if (Game.I != null && Game.I.menus != null && Game.I.state == Game.State.Menu && !Game.I.menus.FindingRace) Game.I.menus.ShowOnlineStatus(s);
        }

        // ------------------------------------------------------------------ connecting

        static string NewPartyCode() => "0" + UnityEngine.Random.Range(0, 100000).ToString("00000");

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

        /// Matchmaking: on your track (session property filter) or any race that's filling up.
        async Task FindRace(bool onMyTrack)
        {
            if (!Available) { SetStatus("Online play is off: Photon Fusion 2 isn't installed."); return; }
            if (Busy) return;
            PartyCode = "";
            SetStatus(onMyTrack ? "Searching for drivers on " + MapCatalog.Get(Game.I.config.map).name + "..." : "Searching for a race...");
            var args = Args(null, false);
            if (!onMyTrack) args.SessionProperties = null; // no filter: any open race (we create one on our track if there's none)
            var r = await Connect(args);
            if (r != null && r.Ok) AfterJoin();
        }

        // ------------------------------------------------------------------ FIND RACE matchmaking room

        bool lobby;
        public bool InLobby => Connected && lobby;
        public int LobbyPlayers => InLobby ? Runner.ActivePlayers.Count() : 1;
        public bool IsLobbyMaster => InLobby && Runner.IsSharedModeMasterClient;

        /// Join an open matchmaking room (or open one) and wait there; RaceFinder decides when to go.
        public async Task StartMatchmaking()
        {
            if (!Available || Busy) return;
            PartyCode = "";
            var r = await Connect(new StartGameArgs
            {
                GameMode = Fusion.GameMode.Shared,
                SessionNameGenerator = () => LobbyPrefix + NewCode(false),
                SessionProperties = new Dictionary<string, SessionProperty> { [MatchProperty] = 1 },
                IsVisible = true,
                PlayerCount = MaxPlayers,
            });
            if (r == null || !r.Ok) return;
            if (RaceFinder.I == null || !RaceFinder.I.Searching) { await Shutdown(); return; } // cancelled while connecting
            lobby = true;
            handledGo = CurrentGo();
        }

        public void StopMatchmaking() { if (lobby) { lobby = false; _ = Shutdown(); } } // still connecting: StartMatchmaking leaves once it lands

        /// Host of the matchmaking room: close it to newcomers and send everyone in it to a private race.
        public async Task LaunchLobbyRace(int map, int laps)
        {
            if (!IsLobbyMaster || Busy) return;
            string name = PrivatePrefix + NewCode(true);
            Runner.SessionInfo.IsOpen = false;
            Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
            {
                [GoProperty] = name + "|0", [GoMapProperty] = map, [GoLapsProperty] = Mathf.Clamp(laps, 1, 30),
            });
            handledGo = name + "|0";
            await Task.Delay(1500); // let the others see it
            lobby = false;
            await GoTo(name, false, map, laps);
        }

        /// From the race setup screen: matchmaking on the track you just set up.
        public void FindPlayers() => _ = FindRace(true);

        // ------------------------------------------------------------------ party

        public string PartyCode { get; private set; } = "";
        public bool InParty => Connected && !string.IsNullOrEmpty(PartyCode) && RoomCode == PartyCode;
        public bool IsPartyLeader => InParty && Runner.IsSharedModeMasterClient;
        public int PartySize => InParty ? Runner.ActivePlayers.Count() : 0;
        string handledGo = "";

        async Task CreateParty()
        {
            if (!Available) { SetStatus("Online play is off: Photon Fusion 2 isn't installed."); return; }
            if (Busy) return;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string code = NewPartyCode();
                SetStatus("Creating a party...");
                var r = await Connect(PartyArgs(code));
                if (r == null || !r.Ok) return;
                if (Runner.ActivePlayers.Count() <= 1) { PartyCode = code; handledGo = ""; SetStatus("Party " + Format(code) + " is open. Give friends the code."); return; }
                await Shutdown();
            }
            SetStatus("Couldn't find a free party code. Try again.");
        }

        StartGameArgs PartyArgs(string code) => new StartGameArgs
        {
            GameMode = Fusion.GameMode.Shared,
            SessionName = PartyPrefix + code,
            SessionProperties = new Dictionary<string, SessionProperty> { [GoProperty] = "" },
            IsVisible = false,
            PlayerCount = MaxPlayers,
        };

        async Task JoinParty(string code)
        {
            SetStatus("Joining party " + Format(code) + "...");
            var r = await Connect(PartyArgs(code));
            if (r == null || !r.Ok) return;
            PartyCode = code;
            // Don't follow a race the party already left for.
            handledGo = CurrentGo();
            SetStatus(Runner.ActivePlayers.Count() <= 1 ? "Party " + Format(code) + " is empty: you're the leader now." : "Joined party " + Format(code) + ".");
        }

        string CurrentGo()
        {
            var info = Runner != null ? Runner.SessionInfo : null;
            if (info == null || info.Properties == null || !info.Properties.TryGetValue(GoProperty, out var p) || p == null) return "";
            return p.PropertyValue as string ?? "";
        }

        /// <summary>
        /// Leader: send the party to a race on this config's track. Party only = a fresh private room; party vs others
        /// = a fresh public room on that track, which strangers' matchmaking fills. Everyone follows the "go" property.
        /// </summary>
        public async Task PartyGo(SessionConfig cfg, bool vsOthers)
        {
            if (!IsPartyLeader || Busy) return;
            string name = (vsOthers ? PublicPrefix + NewCode(false) : PrivatePrefix + NewCode(true));
            Game.I.config = cfg.Clone();
            Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
            {
                [GoProperty] = name + "|" + (vsOthers ? 1 : 0), [GoMapProperty] = cfg.map, [GoLapsProperty] = Mathf.Clamp(cfg.laps, 1, 30),
            });
            handledGo = name + "|" + (vsOthers ? 1 : 0);
            SetStatus("Taking the party to " + MapCatalog.Get(cfg.map).name + "...");
            await Task.Delay(1500); // let the others see it
            await GoTo(name, vsOthers, cfg.map, cfg.laps);
        }

        async Task GoTo(string sessionName, bool visible, int map, int laps)
        {
            string party = PartyCode;
            await Shutdown();
            PartyCode = party; // remembered: back to the party after the race
            Game.I.config.map = map;
            Game.I.config.laps = laps;
            var r = await Connect(Args(sessionName, !visible));
            if (r != null && r.Ok) AfterJoin();
        }

        /// Back in the garage after an online race: rejoin your party.
        public void ReturnToParty()
        {
            if (!Available || string.IsNullOrEmpty(PartyCode) || Busy) return;
            if (Connected && RoomCode == PartyCode) return;
            string code = PartyCode;
            _ = ReturnAsync(code);
        }

        async Task ReturnAsync(string code)
        {
            await Shutdown();
            await JoinParty(code);
        }

        async Task CreatePrivate()
        {
            if (!Available) { SetStatus("Online play is off: Photon Fusion 2 isn't installed."); return; }
            if (Busy) return;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string code = NewCode(true);
                SetStatus("Creating a private room...");
                var r = await Connect(Args(PrivatePrefix + code, true));
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
            if (code[0] == '0') { await JoinParty(code); return; }
            PartyCode = "";
            bool isPrivate = code[0] >= '5';
            SetStatus("Joining " + Format(code) + "...");
            var r = await Connect(Args((isPrivate ? PrivatePrefix : PublicPrefix) + code, isPrivate));
            if (r == null || !r.Ok) return;
            if (Runner.ActivePlayers.Count() <= 1)
            {
                await Shutdown();
                SetStatus("Nobody is in room " + Format(code) + ". Check the code?");
                return;
            }
            AfterJoin();
        }

        async Task<StartGameResult> Connect(StartGameArgs args) // not "Start": Unity would treat that as its Start message
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
                RoomCode = name.StartsWith(PublicPrefix) ? name.Substring(PublicPrefix.Length) : name.StartsWith(PrivatePrefix) ? name.Substring(PrivatePrefix.Length) : name.StartsWith(PartyPrefix) ? name.Substring(PartyPrefix.Length) : name.StartsWith(LobbyPrefix) ? name.Substring(LobbyPrefix.Length) : "";
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
            int slot = Mathf.Max(0, (Runner.LocalPlayer.PlayerId - 1) % MaxPlayers);
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
            finally { leaving = false; lobby = false; Cars.Clear(); RoomCode = ""; }
        }

        void OnApplicationQuit() { if (Runner != null) { leaving = true; Runner.Shutdown(); } }

        void Update()
        {
            // Party members follow the leader to the race they picked.
            if (((InParty && !IsPartyLeader) || (InLobby && !IsLobbyMaster)) && !Busy && Time.frameCount % 15 == 0)
            {
                string go = CurrentGo();
                if (!string.IsNullOrEmpty(go) && go != handledGo)
                {
                    handledGo = go;
                    var parts = go.Split('|');
                    int map = Game.I.config.map, laps = Game.I.config.laps;
                    var props = Runner.SessionInfo.Properties;
                    if (props.TryGetValue(GoMapProperty, out var m)) map = (int)m;
                    if (props.TryGetValue(GoLapsProperty, out var l)) laps = Mathf.Max(1, (int)l);
                    if (InLobby) { lobby = false; RaceFinder.I?.Stop("Race found! Heading to " + MapCatalog.Get(map).name + "..."); }
                    else SetStatus("The party is off to " + MapCatalog.Get(map).name + "!");
                    _ = GoTo(parts[0], parts.Length > 1 && parts[1] == "1", map, laps);
                }
            }

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
