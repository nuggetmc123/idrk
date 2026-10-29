using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Runs Flopzone: boots everything, shows the welcome popup, connects
    /// (online or offline), runs the tutorial for new players, then hands
    /// over to the hub and matches.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public Session session;
        public World world;
        public PlayerRig rig;
        public MenuUI menu;
        public int MyHp { get; private set; } = 100;
        public bool MenuOpen => menu != null && menu.IsOpen;
        public event Action SessionChanged;

        readonly Dictionary<string, RemotePlayer> remotes = new Dictionary<string, RemotePlayer>();
        readonly Dictionary<string, BotController> bots = new Dictionary<string, BotController>();
        Transform playersRoot;
        Tutorial tutorial;
        CtfFlags ctf;
        float nextPose, nextPing;
        bool fellSent, pendingLoadout, banned, kicked;
        int reconnectTries;
        string lastKillerName = "";

        public IEnumerable<RemotePlayer> Remotes => remotes.Values;
        public RemotePlayer FindRemote(string id) => id != null && remotes.TryGetValue(id, out var r) ? r : null;
        IBotHost BotHost => session != null ? session.link.BotHost : null;
        Chopper chopper;
        /// <summary>Sitting in the helicopter lobby (picking a mode, or waiting for the match to fill).</summary>
        public bool InLobby => chopper != null && chopper.Occupied;

        /// <summary>
        /// The helicopter cabin is private: someone sitting in their own cabin (way up at
        /// Chopper.Origin) only shows up for people in the same party who are in there too.
        /// </summary>
        public bool HiddenInLobby(RemotePlayer r, Vector3 feet)
        {
            if (r.isBot || feet.y < Chopper.Origin.y - 50f) return false;
            if (!InLobby || session == null) return true;
            foreach (var kv in session.PartyMembers) if (kv.Key == r.id) return false;
            return true;
        }
        float nextBotCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            // the generated scene already has one; this covers pressing Play in any other scene
            if (FindFirstObjectByType<Game>() == null) new GameObject("Flopzone").AddComponent<Game>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Save.Load();
            Application.runInBackground = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Quest: no live shadows (they draw everything twice), and run at the headset's
            // refresh rate; Android otherwise caps games at 30 fps, which judders in VR
            QualitySettings.shadows = ShadowQuality.Disable;
            Application.targetFrameRate = 120;
#else
            QualitySettings.shadows = ShadowQuality.All;        // PC / editor: soft shadows
            QualitySettings.shadowDistance = 70f;
            QualitySettings.shadowCascades = 2;
#endif
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
            foreach (var w in FindObjectsByType<World>(FindObjectsSortMode.None)) Destroy(w.gameObject);   // the editor's hub preview

            surfacesReady = Surfaces.Prewarm();
            gameObject.AddComponent<VRInput>();
            gameObject.AddComponent<AudioManager>();
            gameObject.AddComponent<Fx>();
            gameObject.AddComponent<UIPointer>();
            gameObject.AddComponent<Hud>();
            menu = gameObject.AddComponent<MenuUI>();
            playersRoot = new GameObject("Players").transform;
        }

        void Start()
        {
            rig = PlayerRig.Create();
            UIPointer.I.hands[0] = rig.hands[0].transform;
            UIPointer.I.hands[1] = rig.hands[1].transform;
            Hud.I.Attach(rig);
            StartCoroutine(Boot());
        }

        /* ======================= boot ======================= */

        System.Threading.Tasks.Task surfacesReady;

        IEnumerator Boot()
        {
            float started = Time.time;
            rig.Teleport(new Vector3(0, 0.05f, 0), 0);
            var popup = Hud.I.Popup("Welcome to Flopzone", "Loading to hub...");
            // the textures are painted on other threads while the welcome popup shows
            while (surfacesReady != null && !surfacesReady.IsCompleted && Time.time - started < 20f) yield return null;
            // something nice to stand in while we connect, built a piece per frame so the
            // headset keeps getting frames (one long frame freezes and judders in VR)
            popup.PlaceInFront(rig.head, 0.9f, 0.05f);
            yield return MapBuilder.BuildHubOverFrames(w => world = w);
            Physics.gravity = new Vector3(0, -9.81f * world.gravity, 0);
            rig.GravityScale = world.gravity;
            world.Spawn(-1, 0, out var p, out var yaw);
            rig.Teleport(p, yaw);
            popup.PlaceInFront(rig.head, 0.9f, 0.05f);
            var body = popup.content.GetComponentsInChildren<TextMesh>()[1];
            UIPointer.I.Active = false;

            while (!AudioManager.I.Ready && Time.time - started < 4f) yield return null;
            AudioManager.I.PlayMusic("music_hub");

            yield return Connect();

            while (Time.time - started < 2.8f && !banned)
            {
                if (body != null) body.text = "Loading to hub" + new string('.', 1 + (int)(Time.time * 3) % 3);
                yield return null;
            }
            if (banned) yield break;
            Hud.I.ClosePopup();
            if (session.NeedsName || !Save.TutorialDone) StartTutorial();
            else GoHub();
        }

        IEnumerator Connect()
        {
            // the ban / moderator list (Resources/moderation.json, plus the newest copy online)
            yield return Moderation.Refresh();
            ILink link;
#if FLOPZONE_FUSION
            // Photon Fusion 2 is in the project: online play. (The account server URL is optional.)
            link = new PhotonLink(FlopzoneConfig.I.SocketUrl);
            PhotonLink.CurrentSession = () => session;
#else
            // No Photon yet: offline with bots (Flopzone > Set Up Multiplayer turns it on).
            link = new LocalServer();
#endif
            UseSession(new Session(link));
            session.Hello();
            float t1 = Time.time;
            while (!session.Welcomed && !banned && Time.time - t1 < 12f) { session.Tick(); yield return null; }
            if (!session.Welcomed && !banned)
            {
                link.Close();
                UseSession(new Session(new LocalServer()));
                session.Hello();
                session.Tick();
                Hud.I.Toast("Couldn't go online, so you're playing offline with bots.", 5f);
            }
        }

        void UseSession(Session s)
        {
            session = s;
            s.RoomEntered += OnRoom;
            s.PlayerJoined += p => AddRemote(p);
            s.PlayerLeft += RemoveRemote;
            s.Pose += (id, p) => { var r = FindRemote(id); if (r != null && p != null) r.ApplyPose(p); };
            s.Event += OnEvent;
            s.Hp += OnHp;
            s.Kill += OnKill;
            s.Respawn += OnRespawn;
            s.MatchState += OnMatchState;
            s.MatchEnd += OnMatchEnd;
            s.FlagsChanged += OnFlags;
            s.Chat += OnChat;
            s.Toast += msg => Hud.I.Toast(msg);
            s.Muted += (until, reason) => Hud.I.Toast(reason, 5f);
            s.Kicked += reason =>
            {
                kicked = true;
                Hud.I.Popup("Kicked", reason, ("OK", () => { kicked = false; Hud.I.ClosePopup(); GoHub(); }));
            };
            s.Banned += OnBanned;
            s.PartyInvite += (pid, from) => Hud.I.Popup("Party invite", from + " invited you to their party.",
                ("Join", () => { session.PartyAccept(pid); session.PendingInvitePartyId = null; Hud.I.ClosePopup(); }),
                ("No thanks", () => { session.PartyDecline(pid); session.PendingInvitePartyId = null; Hud.I.ClosePopup(); }));
            s.NameChanged += (id, name) => { var r = FindRemote(id); if (r != null) r.SetName(name); };
            s.NeedName += () => { if (session.Welcomed && tutorial == null && session.RoomId != null) Hud.I.Toast("A moderator reset your name. Pick a new one in Menu > Profile."); };
            s.Closed += OnClosed;
            s.Board += () => { if (session.InHub && !InLobby && tutorial == null && !banned) BoardChopper(false); };
            SessionChanged?.Invoke();
        }

        void OnClosed()
        {
            if (banned || kicked || !session.Online) return;
            Hud.I.Toast("Lost connection to the server. Reconnecting...", 4f);
            StartCoroutine(Reconnect());
        }

        IEnumerator Reconnect()
        {
            yield return new WaitForSeconds(2f + reconnectTries * 2f);
            reconnectTries++;
            string url = FlopzoneConfig.I.SocketUrl;
            if (reconnectTries > 3 || url.Length == 0)
            {
                Hud.I.Toast("Still can't reach the server. Playing offline for now.", 5f);
                UseSession(new Session(new LocalServer()));
                session.Hello();
                yield return null;
                GoHub();
                yield break;
            }
            var ws = new WsLink(url);
            float t0 = Time.time;
            while (!ws.Connected && !ws.Failed && Time.time - t0 < 9f) yield return null;
            if (!ws.Connected) { StartCoroutine(Reconnect()); yield break; }
            UseSession(new Session(ws));
            session.Hello();
            float t1 = Time.time;
            while (!session.Welcomed && Time.time - t1 < 6f) yield return null;
            reconnectTries = 0;
            Hud.I.Toast("Reconnected!");
            if (tutorial == null) GoHub();
        }

        void OnBanned(long until, string reason)
        {
            banned = true;
            string when = until <= 0 ? "permanently" : "until " + DateTimeOffset.FromUnixTimeMilliseconds(until).ToLocalTime().ToString("MMM d, h:mm tt");
            Hud.I.Popup("You are banned", "You are banned " + when + ".\nReason: " + (string.IsNullOrEmpty(reason) ? "breaking the rules" : reason));
        }

        public void StartTutorial()
        {
            if (tutorial != null) return;
            if (menu.IsOpen) menu.Close();
            ClearRemotes();
            BuildWorld(MapBuilder.TutorialIndex);
            world.Spawn(-1, 0, out var p, out var yaw);
            rig.Teleport(p, yaw);
            rig.SetTeam(-1);
            rig.GiveLoadout(Save.Settings.primaryGun, Save.Settings.sidearmGun);
            tutorial = Tutorial.Begin(session.NeedsName, () => { tutorial = null; GoHub(); });
        }

        /* ======================= every frame ======================= */

        void Update()
        {
            if (session == null) return;
            session.Tick();
            if (VRInput.I.MenuDown && (tutorial == null || tutorial.AllowMenu) && !banned && session.Welcomed) menu.Toggle();
            if (session.RoomId != null && Time.time >= nextPose && rig != null)
            {
                nextPose = Time.time + 1f / 12f;
                if (session.Online) session.SendPose(rig.BuildPose());
                else session.SendPose(new[] { rig.transform.position.x, rig.transform.position.y, rig.transform.position.z });
            }
            if (session.Online && Time.time >= nextPing) { nextPing = Time.time + 15f; session.Ping(); }
            if (Time.time >= nextBotCheck) { nextBotCheck = Time.time + 0.5f; SyncBots(); }
        }

        /* ======================= worlds and rooms ======================= */

        void BuildWorld(int index, bool expanded = false)
        {
            if (ctf) { Destroy(ctf.gameObject); ctf = null; }
            foreach (var h in rig.hands) if (h.held is Magazine) h.Release();
            foreach (var g in rig.LoadoutGuns()) { foreach (var h in rig.hands) if (h.held == g) h.ReleaseQuietly(); g.GoHome(); }
            foreach (var h in rig.hands) if (h.held != null && h.held is Gun gun && gun.displayOnly) { h.ReleaseQuietly(); Destroy(gun.gameObject); }
            foreach (var m in FindObjectsByType<Magazine>(FindObjectsSortMode.None)) Destroy(m.gameObject);
            foreach (var rd in FindObjectsByType<RagdollLife>(FindObjectsSortMode.None)) Destroy(rd.gameObject);
            if (world != null) Destroy(world.gameObject);
            world = MapBuilder.Build(index, expanded);
            Physics.gravity = new Vector3(0, -9.81f * world.gravity, 0);
            rig.GravityScale = world.gravity;
        }

        void OnRoom()
        {
            if (tutorial != null) { Destroy(tutorial.gameObject); tutorial = null; }
            ClearRemotes();
            int map = session.InHub ? MapBuilder.HubIndex : Mathf.Clamp(session.Map, 0, MapBuilder.Names.Length - 1);
            bool big = session.InMatch && MapBuilder.Expands(session.Mode);
            if (world == null || world.mapIndex != map || world.expanded != big) BuildWorld(map, big);
            foreach (var p in session.Players.Values) if (p.id != session.MyId) AddRemote(p);
            rig.SetTeam(session.MyTeam);
            rig.GiveLoadout(Save.Settings.primaryGun, Save.Settings.sidearmGun);
            bool waiting = session.InMatch && (session.State == "waiting" || session.State == "countdown");
            if (waiting || (session.InHub && InLobby))
            {
                // the match is still filling up (or we haven't picked one yet): wait in the chopper
                if (!InLobby) { EnsureChopper(); chopper.Enter(rig, SeatIndex()); }   // pulled in with the party: the door shuts behind you too
            }
            else
            {
                if (chopper != null) chopper.Exit();
                world.Spawn(session.MyTeam, session.SpawnIndex, out var pos, out var yaw);
                rig.Respawn(pos, yaw);
            }
            MyHp = 100;
            fellSent = false;
            if (session.InMatch && session.Mode == "ctf") ctf = CtfFlags.Create(world);
            AudioManager.I.PlayMusic(session.InHub ? "music_hub" : "music_match");
            if (session.InHub) { if (!InLobby) Hud.I.Big("THE HUB", 2f, Color.white); }
            else if (InLobby) Hud.I.Big("FINDING MATCH", 2.5f, UIKit.Accent);
            else
            {
                string modeName = LocalServer.Modes.TryGetValue(session.Mode, out var md) ? md.name : session.Mode;
                string team = session.MyTeam == 0 ? "You're on RED" : session.MyTeam == 1 ? "You're on BLUE" : "Everyone for themselves!";
                Hud.I.Big(modeName.ToUpperInvariant() + "\n<size=24>" + world.mapName + " · " + team + "</size>", 3f,
                    session.MyTeam >= 0 ? Color.Lerp(Avatar.TeamColor(session.MyTeam), Color.white, 0.3f) : UIKit.Accent);
            }
        }

        void AddRemote(PlayerInfo p)
        {
            if (p.id == session.MyId) return;
            RemoveRemote(p.id);
            var r = RemotePlayer.Create(playersRoot, p.id, p.name, p.team, p.bot);
            r.hp = p.hp;
            r.alive = p.alive;
            remotes[p.id] = r;
            if (p.bot && BotHost != null) DriveBot(r, true);
            if (!p.alive) r.avatar.SetVisible(false);
        }

        /// <summary>This headset runs the room: it moves and shoots for this bot.</summary>
        void DriveBot(RemotePlayer r, bool respawn)
        {
            var b = r.gameObject.AddComponent<BotController>();
            b.Init(r, BotHost);
            if (respawn) b.Respawn(UnityEngine.Random.Range(0, 1000));
            else b.TakeOver();
            bots[r.id] = b;
        }

        /// <summary>If the player running the room leaves, the next one takes over the bots (and lets go of them otherwise).</summary>
        void SyncBots()
        {
            var host = BotHost;
            foreach (var r in remotes.Values)
            {
                if (r == null || !r.isBot) continue;
                bool driving = bots.TryGetValue(r.id, out var b) && b != null;
                if (host != null && !driving) DriveBot(r, false);
                else if (host == null && driving) { Destroy(b); bots.Remove(r.id); }
                else if (driving && b.server != host) b.server = host;
            }
        }

        void RemoveRemote(string id)
        {
            if (remotes.TryGetValue(id, out var r) && r != null) Destroy(r.gameObject);
            remotes.Remove(id);
            bots.Remove(id);
        }

        void ClearRemotes()
        {
            foreach (var r in remotes.Values) if (r != null) Destroy(r.gameObject);
            remotes.Clear();
            bots.Clear();
        }

        /* ======================= requests from gameplay ======================= */

        public void GoHub()
        {
            if (session == null) return;
            if (!session.IsLeaderOrSolo && session.InParty) { Hud.I.Toast("Only the party leader can choose where the party goes. Leave the party first."); return; }
            session.Go("hub");
        }

        public void GoMode(string mode, bool privateMatch = false)
        {
            if (session == null || tutorial != null) return;
            if (!session.IsLeaderOrSolo) { Hud.I.Toast("Only your party leader can pick a mode."); return; }
            if (session.InHub && !InLobby) BoardChopper(true);
            session.Go(mode, privateMatch);
        }

        void EnsureChopper()
        {
            if (chopper == null) chopper = Chopper.Create();
        }

        /// <summary>Everyone in the party gets their own seat.</summary>
        int SeatIndex()
        {
            for (int i = 0; i < session.PartyMembers.Count; i++) if (session.PartyMembers[i].Key == session.MyId) return i;
            return 0;
        }

        /// <summary>Walk up the chopper's ramp: in you go, the door shuts, and (if you lead a party) everyone is pulled in.</summary>
        public void BoardChopper(bool pullParty)
        {
            if (session == null || tutorial != null || InLobby || !rig.Alive) return;
            if (menu.IsOpen) menu.Close();
            EnsureChopper();
            chopper.Enter(rig, SeatIndex());
            Hud.I.Big(session.IsLeaderOrSolo ? "PICK A GAME MODE" : "WAITING FOR YOUR\nPARTY LEADER", 2.5f, UIKit.Accent);
            if (pullParty && session.InParty && session.IsLeaderOrSolo) session.BoardParty();
        }

        /// <summary>Off the chopper. While searching for a match this cancels the search (party leader only).</summary>
        public void LeaveChopper()
        {
            if (session == null || !InLobby) return;
            if (session.InMatch) { GoHub(); return; }
            chopper.Exit();
            rig.Teleport(Chopper.HubExit, 180f);
        }

        public void LoadoutChanged()
        {
            if (session != null && session.InMatch && rig.Alive && !InLobby) { pendingLoadout = true; Hud.I.Toast("Your new loadout is ready next time you spawn."); return; }
            rig.GiveLoadout(Save.Settings.primaryGun, Save.Settings.sidearmGun);
        }

        public void ReportHit(RemotePlayer target, float dmg, bool head, GunDef gun, Vector3 dir)
        {
            if (session == null || !session.InMatch || target == null) return;
            bool teams = LocalServer.Modes.TryGetValue(session.Mode, out var md) && md.teams;
            if (teams && target.team == session.MyTeam) return;
            session.SendHit(target.id, dmg, head, gun.id, dir);
        }

        public void ReportShot(int hand, Vector3 from, Vector3 to, int gunIdx)
        {
            if (session == null || session.RoomId == null || !session.Online) return;
            session.SendEvent(Msg.Of("k", "f", "h", hand, "g", gunIdx, "a", new[] { from.x, from.y, from.z }, "b", new[] { to.x, to.y, to.z }));
        }

        public void LocalFell()
        {
            if (session != null && session.InMatch && session.State != "ended")
            {
                if (!fellSent) { fellSent = true; session.SendDie(); }
                return;
            }
            world.Spawn(-1, 0, out var p, out var yaw);
            rig.Teleport(p, yaw);
        }

        /// <summary>Somehow got past the barrier: back to a spawn point.</summary>
        public void OutOfBounds()
        {
            world.Spawn(session != null ? session.MyTeam : -1, UnityEngine.Random.Range(0, 1000), out var p, out var yaw);
            rig.Teleport(p, yaw);
            Hud.I.Toast("Out of bounds! Back you go.");
        }

        public void Invoke2(float delay, Action a) => StartCoroutine(After(delay, a));

        static IEnumerator After(float d, Action a)
        {
            yield return new WaitForSeconds(d);
            a?.Invoke();
        }

        /* ======================= server events ======================= */

        void OnEvent(string id, Dictionary<string, object> e)
        {
            var r = FindRemote(id);
            if (r == null || e == null) return;
            if (e.Str("k") == "f")
            {
                var a = e.FloatArr("a");
                var b = e.FloatArr("b");
                if (a == null || b == null || a.Length < 3 || b.Length < 3) return;
                r.PlayShot(e.Int("h"), new Vector3(a[0], a[1], a[2]), new Vector3(b[0], b[1], b[2]), e.Int("g"));
            }
        }

        void OnHp(string id, int hp, string by)
        {
            if (id == session.MyId)
            {
                if (hp < MyHp)
                {
                    Hud.I.Hurt((MyHp - hp) / 45f);
                    AudioManager.Ui("hurt", 0.8f);
                    VRInput.I.Haptic(0, 0.6f, 0.12f);
                    VRInput.I.Haptic(1, 0.6f, 0.12f);
                    Fx.I.Blood(rig.head.position + Vector3.down * 0.45f + rig.head.forward * 0.2f, rig.head.forward, 0.4f);
                }
                MyHp = hp;
                return;
            }
            var r = FindRemote(id);
            if (r != null) r.hp = hp;
        }

        string NameOf(string id)
        {
            if (id == null) return "";
            if (id == session.MyId) return "You";
            return session.Players.TryGetValue(id, out var p) ? p.name : "someone";
        }

        void OnKill(string victim, string killer, string gun, bool head, Vector3 dir)
        {
            var def = GunCatalog.ById(gun);
            string how = killer == null ? "fell" : (def != null ? def.name : "?") + (head ? " · HEADSHOT" : "");
            Color c = victim == session.MyId ? UIKit.Bad : killer == session.MyId ? UIKit.Good : Color.white;
            Hud.I.Feed(killer == null ? $"{NameOf(victim)} fell off" : $"{NameOf(killer)}  [{how}]  {NameOf(victim)}", c);
            if (victim == session.MyId)
            {
                MyHp = 0;
                rig.Die(dir, head);
                lastKillerName = killer == null ? "gravity" : NameOf(killer);
                Hud.I.Big("FLOPPED\n<size=24>by " + lastKillerName + " · back in 3...</size>", 3f, UIKit.Bad);
                AudioManager.Ui("lose", 0.4f);
            }
            else
            {
                var r = FindRemote(victim);
                if (r != null) r.Die(dir, head);
            }
            if (killer == session.MyId && victim != session.MyId)
            {
                Hud.I.Big(head ? "HEADSHOT!" : "FLOPPED " + NameOf(victim), 1.5f, UIKit.Good);
                AudioManager.Ui(head ? "hit_head" : "hitmarker", 1f, 0.8f);
            }
        }

        void OnRespawn(string id, int spawn)
        {
            if (id == session.MyId)
            {
                if (pendingLoadout) { pendingLoadout = false; rig.GiveLoadout(Save.Settings.primaryGun, Save.Settings.sidearmGun); }
                world.Spawn(session.MyTeam, spawn, out var p, out var yaw);
                rig.Respawn(p, yaw);
                MyHp = 100;
                fellSent = false;
                return;
            }
            var r = FindRemote(id);
            if (r == null) return;
            if (bots.TryGetValue(id, out var b)) b.Respawn(spawn);
            r.Revive();
        }

        void OnMatchState(string state, string note)
        {
            if (state == "countdown") { Hud.I.Big(InLobby ? "MATCH FOUND!" : "GET READY", 1.5f, InLobby ? UIKit.Good : UIKit.Accent); AudioManager.Ui("beep", 0.6f); }
            else if (state == "live")
            {
                if (InLobby)
                {
                    // deploy: out of the chopper and onto the map
                    chopper.Exit();
                    world.Spawn(session.MyTeam, session.SpawnIndex, out var p, out var yaw);
                    rig.Respawn(p, yaw);
                    MyHp = 100;
                }
                Hud.I.Big("FIGHT!", 1.5f, UIKit.Good);
                AudioManager.Ui("go", 0.8f);
            }
            else if (state == "waiting" && !string.IsNullOrEmpty(note)) Hud.I.Toast(note);
        }

        void OnMatchEnd(Dictionary<string, object> m)
        {
            int winnerTeam = m.Int("winnerTeam", -1);
            string winnerId = m.Has("winnerId") ? m.Str("winnerId") : null;
            bool teams = LocalServer.Modes.TryGetValue(session.Mode ?? "", out var md) && md.teams;
            bool won = teams ? winnerTeam == session.MyTeam : winnerId == session.MyId;
            bool draw = teams ? winnerTeam < 0 : winnerId == null;
            string title = draw ? "DRAW" : won ? "VICTORY!" : "DEFEAT";
            AudioManager.Ui(won ? "win" : "lose", 0.8f);
            var lines = new List<string>();
            var players = m.Arr("players");
            if (players != null)
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var o in players) if (o is Dictionary<string, object> d) list.Add(d);
                list.Sort((a, b) => b.Int("k").CompareTo(a.Int("k")));
                foreach (var d in list)
                {
                    string team = teams ? (d.Int("team") == 0 ? "<color=#ff6b5e>RED</color>  " : "<color=#6b9bff>BLUE</color> ") : "";
                    lines.Add($"{team}{d.Str("name")}   {d.Int("k")} / {d.Int("d")}");
                    if (lines.Count >= 8) break;
                }
            }
            string score = teams ? $"RED {session.Scores[0]} - {session.Scores[1]} BLUE\n" : "";
            Hud.I.Popup(title, (m.Str("note").Length > 0 ? m.Str("note") + "\n" : "") + score + string.Join("\n", lines) + "\n\nBack to the hub in 10 seconds...",
                ("OK", () => Hud.I.ClosePopup()));
            Invoke2(9.5f, () => Hud.I.ClosePopup());
        }

        void OnFlags(string note)
        {
            if (string.IsNullOrEmpty(note)) return;
            Hud.I.Feed(note, UIKit.Accent);
            if (note.Contains("captured")) AudioManager.Ui("flag_cap", 0.8f);
            else if (note.Contains("took")) AudioManager.Ui("flag_take", 0.7f);
        }

        void OnChat(string id, string name, string msg)
        {
            var r = FindRemote(id);
            if (r != null) r.ShowChat(msg);
            Hud.I.Feed(name + ": " + UIKit.Clip(msg, 50), new Color(0.85f, 0.9f, 1f));
            AudioManager.Ui("chat", 0.5f);
        }
    }
}
