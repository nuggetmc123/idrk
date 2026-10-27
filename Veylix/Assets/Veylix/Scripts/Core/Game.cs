using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Runs the whole game: boots into the garage showroom with the main menu, loads a map and starts a session, pauses,
    /// restarts, recovers the player's car, and returns to the menu. One per scene (the world builder puts it there).
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public enum State { Menu, Loading, Session, Paused, Results }
        public State state = State.Menu;
        public SessionConfig config = new SessionConfig();
        public Car playerCar;
        public Cockpit cockpit;
        public Dashboard dashboard;
        public Mirrors mirrors;
        public RaceSession session;
        public Showroom showroom;
        public Menus menus;
        public Hud hud;

        World world;
        Sky sky;
        Rig rig;
        float resetHold;
        bool crashHandled;
        float sessionStartTime;

        void Awake()
        {
            I = this;
            // VR: the headset sets the pace. Desktop: lock to the monitor so the GPU isn't drawing frames nobody sees.
            bool xr = UnityEngine.XR.XRSettings.isDeviceActive;
            Application.targetFrameRate = xr ? -1 : 90;
            QualitySettings.vSyncCount = xr ? 0 : 1;
            Time.fixedDeltaTime = 1f / 120f;   // stiff F1 suspension needs a fine physics step
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Layers.Setup();
            config = SaveData.D.lastSession ?? new SessionConfig();
        }

        IEnumerator Start()
        {
            ApplyRenderScale();
            var rigGo = new GameObject("Rig");
            rig = rigGo.AddComponent<Rig>();
            PlayerBody.Create(null); // your own body while you walk around the garage
            sky = new GameObject("Sky").AddComponent<Sky>();
            world = new GameObject("World").AddComponent<World>();
            menus = new GameObject("Menus").AddComponent<Menus>();
            hud = new GameObject("Hud").AddComponent<Hud>();
            if (Music.I == null) new GameObject("Music").AddComponent<Music>();
            Obj.Need<RaceFinder>(gameObject);
            yield return Boot();
            EnterMenu();
            if (Online.I != null) Online.I.Boot();
        }

        /// Splash screen in the dark while your garage and car are built, then a fade into the garage.
        IEnumerator Boot()
        {
            state = State.Loading;
            Music.Play(Music.Theme.Loading);
            rig.FadeInstant(1f);
            rig.SetVoid(true);
            rig.StandAt(new Vector3(0f, 5000f, 0f), Quaternion.identity);
            yield return null;
            menus.ShowSplash();
            rig.FadeTo(0f);
            float t0 = Time.realtimeSinceStartup;
            menus.SetSplashProgress(0.1f, "Starting up");
            yield return new WaitForSecondsRealtime(0.6f);
            menus.SetSplashProgress(0.35f, "Building your garage");
            yield return null;
            try
            {
                if (showroom == null) showroom = new GameObject("Showroom").AddComponent<Showroom>();
                showroom.gameObject.SetActive(false);
            }
            catch (System.Exception e) { Debug.LogException(e); }
            menus.SetSplashProgress(0.7f, "Rolling your car out");
            yield return null;
            menus.SetSplashProgress(0.9f, "Warming the tyres");
            while (Time.realtimeSinceStartup - t0 < 2.8f) yield return null;
            menus.SetSplashProgress(1f, "Ready");
            yield return new WaitForSecondsRealtime(0.3f);
            rig.FadeTo(1f);
            float t = 0f;
            while (!rig.Faded && t < 1.5f) { t += Time.unscaledDeltaTime; yield return null; }
            menus.Hide();
            // First launch: make your driver here in the dark, before the garage.
            if (!SaveData.Profile.created) yield return FirstRun();
            rig.SetVoid(false);
        }

        // ------------------------------------------------------------------ first launch: your driver

        Transform studio;
        GameObject studioFigure;

        /// A pool of light in the dark space with your driver standing in it, and the driver screen beside them.
        IEnumerator FirstRun()
        {
            studio = new GameObject("Driver Studio").transform;
            studio.position = new Vector3(0f, 5000f, 0f);
            try { BuildStudio(studio); PreviewDriver(SaveData.Profile); }
            catch (System.Exception e) { Debug.LogException(e); }
            rig.StandAt(studio.position, Quaternion.identity);
            yield return null;
            bool done = false;
            menus.ShowFirstRunProfile(() => done = true);
            Music.Play(Music.Theme.Garage);
            rig.FadeTo(0f);
            while (!done) yield return null;
            rig.FadeTo(1f);
            float t = 0f;
            while (!rig.Faded && t < 1.5f) { t += Time.unscaledDeltaTime; yield return null; }
            Destroy(studio.gameObject);
            studio = null;
            studioFigure = null;
        }

        static readonly Vector3 StudioFigureSpot = new Vector3(0.95f, 0f, 1.6f);

        static void BuildStudio(Transform root)
        {
            var A = VXAssets.I;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Obj.Kill(floor.GetComponent<Collider>());
            floor.name = "Floor";
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = StudioFigureSpot + new Vector3(0f, -0.005f, 0f);
            floor.transform.localScale = new Vector3(2.4f, 0.005f, 2.4f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = VXAssets.Or(A != null ? A.satinBlack : null, "Studio floor", new Color(0.07f, 0.07f, 0.08f), 0.55f);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Obj.Kill(ring.GetComponent<Collider>());
            ring.name = "Ring";
            ring.transform.SetParent(root, false);
            ring.transform.localPosition = StudioFigureSpot + new Vector3(0f, -0.007f, 0f);
            ring.transform.localScale = new Vector3(2.5f, 0.005f, 2.5f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = new Material(VXAssets.Or(null, "Studio ring", VUI.Accent, 0.3f)) { color = VUI.Accent };
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(root, false);
            key.transform.localPosition = StudioFigureSpot + new Vector3(-0.8f, 3.2f, -1.2f);
            key.transform.rotation = Quaternion.LookRotation(root.TransformPoint(StudioFigureSpot + Vector3.up * 1f) - key.transform.position);
            key.type = LightType.Spot; key.spotAngle = 50f; key.range = 8f; key.intensity = 2.6f; key.color = new Color(1f, 0.96f, 0.9f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.transform.localPosition = new Vector3(0.2f, 2.2f, 0.4f);
            fill.type = LightType.Point; fill.range = 6f; fill.intensity = 0.8f; fill.color = new Color(0.85f, 0.9f, 1f);
        }

        /// Shows a driver look on the figure you're customising: in the first-launch studio, or by your car in the garage.
        public void PreviewDriver(DriverLook look)
        {
            if (studio != null)
            {
                if (studioFigure != null) Destroy(studioFigure);
                studioFigure = DriverBody.BuildStanding(studio, look);
                studioFigure.transform.localPosition = StudioFigureSpot;
                Vector3 face = -StudioFigureSpot; face.y = 0f;
                studioFigure.transform.localRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            }
            else if (showroom != null) showroom.ShowFigure(look);
        }

        public static void ApplyRenderScale()
        {
            if (UnityEngine.XR.XRSettings.enabled) UnityEngine.XR.XRSettings.eyeTextureResolutionScale = Settings.RenderScale;
            QualitySettings.shadowDistance = Settings.Quality == 0 ? 60f : Settings.Quality == 1 ? 90f : 140f;
            QualitySettings.shadowCascades = Settings.Quality == 2 ? 2 : 1;
            QualitySettings.lodBias = Settings.Quality == 0 ? 0.8f : 1.2f;
            // The project has no quality preset of its own, so set everything that costs frames explicitly
            // (Unity's default "Ultra" level would use 4 shadow cascades, very high shadow maps and 4 pixel lights).
            int q = Settings.Quality;
            QualitySettings.pixelLightCount = q == 0 ? 1 : q == 1 ? 2 : 4;
            QualitySettings.shadowResolution = q == 0 ? ShadowResolution.Low : q == 1 ? ShadowResolution.Medium : ShadowResolution.High;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.antiAliasing = q == 0 ? 2 : 4;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = true;
            QualitySettings.anisotropicFiltering = q == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.maxQueuedFrames = 2;
        }

        // ------------------------------------------------------------------ menu

        public void EnterMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            state = State.Menu;
            TearDownSession();
            world.Clear();
            if (showroom == null) showroom = new GameObject("Showroom").AddComponent<Showroom>();
            showroom.gameObject.SetActive(true);
            showroom.Show(config.car);
            sky.Configure(MapCatalog.Get(0), 16.5f, WeatherKind.Clear, false, false);
            RenderSettings.fogDensity = 0f;
            var entrance = showroom.SpotPose(Showroom.Spot.Entrance);
            rig.StandAt(entrance.position, entrance.rotation);
            rig.walkArea = showroom.WalkArea;
            showroom.ShowFigure(SaveData.Profile);
            Music.Play(Music.Theme.Garage);
            if (Online.I != null) Online.I.ReturnToParty();
            // First time: make your driver before anything else.
            if (!SaveData.Profile.created) menus.ShowDriverProfile(true);
            else menus.ShowMain();
            hud.Hide();
            rig.FadeTo(0f);
        }

        // ------------------------------------------------------------------ sessions

        public void StartSession(SessionConfig cfg)
        {
            config = cfg.Clone();
            SaveData.D.lastSession = config.Clone();
            SaveData.Write();
            StopAllCoroutines();
            StartCoroutine(Load(config));
        }

        IEnumerator Load(SessionConfig cfg)
        {
            state = State.Loading;
            rig.walkArea = null;
            rig.FadeTo(1f);
            float t = 0f;
            while (!rig.Faded && t < 1.5f) { t += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = 1f;
            AudioListener.pause = false;
            TearDownSession();
            if (showroom != null) showroom.gameObject.SetActive(false);
            var map = MapCatalog.Get(cfg.map);
            // Loading room: a dark space with the progress panel in front of you.
            rig.StandAt(new Vector3(0f, 5000f, 0f), Quaternion.identity);
            rig.SetVoid(true);
            Music.Play(Music.Theme.Loading);
            menus.ShowLoading(map);
            rig.FadeTo(0f);
            System.Exception failed = null;
            yield return Safe(world.Build(map), e => failed = e);
            bool usable = world.track != null && world.grid != null && world.grid.Count > 0;
            if (failed != null && usable)
            {
                // Something in the scenery failed after the track and grid were built: race anyway, and log it.
                Debug.LogException(failed);
                failed = null;
            }
            if (failed == null && !usable) failed = new System.Exception("the track didn't build");
            yield return new WaitForSecondsRealtime(0.4f); // let 100% show for a moment
            rig.FadeTo(1f);
            t = 0f;
            while (!rig.Faded && t < 1.5f) { t += Time.unscaledDeltaTime; yield return null; }
            menus.Hide();
            rig.SetVoid(false);
            if (failed == null)
            {
                try { SpawnSession(cfg, map); }
                catch (System.Exception e) { failed = e; }
            }
            if (failed != null)
            {
                // Never leave you stuck on the loading screen: back to the garage, and say what happened.
                Debug.LogError("[Veylix] Couldn't load " + map.name + ": " + failed);
                Debug.LogException(failed);
                EnterMenu();
                menus.ShowNotice("COULDN'T LOAD " + map.name.ToUpperInvariant(), "Something went wrong building the track (" + failed.Message + "). The details are in Veylix-errors.txt next to the project's Assets folder.");
                yield break;
            }
            rig.FadeTo(0f);
        }

        /// <summary>
        /// Runs a builder coroutine, including the coroutines it yields, one step at a time. An exception stops it and
        /// is handed to onError instead of silently killing the loading screen.
        /// </summary>
        static IEnumerator Safe(IEnumerator routine, System.Action<System.Exception> onError)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                object current;
                try
                {
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    current = top.Current;
                }
                catch (System.Exception e) { onError(e); yield break; }
                if (current is IEnumerator inner) stack.Push(inner);
                else yield return current;
            }
        }

        void SpawnSession(SessionConfig cfg, MapDef map)
        {
            var w = world;
            sky.Configure(map, cfg.Hour(map), cfg.Weather(map), cfg.dynamicTime || cfg.mode == GameMode.FreeRoam, cfg.DynamicWeather);
            var go = new GameObject("Session");
            session = go.AddComponent<RaceSession>();
            var entrants = new List<Entrant>();
            int opponents = session != null && (cfg.mode == GameMode.TimeTrial || cfg.mode == GameMode.Practice || cfg.mode == GameMode.FreeRoam || cfg.mode == GameMode.Online) ? 0 : Mathf.Clamp(cfg.opponents, 0, RaceFinder.MaxRacers - 1); // every race is 12 cars at most
            int total = opponents + 1;
            int playerSlot = cfg.mode == GameMode.TimeTrial || cfg.mode == GameMode.Practice || cfg.mode == GameMode.FreeRoam ? 0 : Mathf.Clamp(cfg.gridPosition - 1, 0, total - 1);
            var grid = w.grid;
            int aiIndex = 0;
            float[] skills = { 0.8f, 0.86f, 0.91f, 0.955f, 0.985f };
            float baseSkill = skills[Mathf.Clamp(cfg.difficulty, 0, 4)];
            for (int slot = 0; slot < total; slot++)
            {
                var pose = grid[Mathf.Min(slot, grid.Count - 1)];
                if (cfg.mode == GameMode.FreeRoam) pose = new Pose(w.track.PointAt(w.startS) + Vector3.up * 0.3f, Quaternion.LookRotation(w.track.ForwardAt(w.startS)));
                if (slot == playerSlot)
                {
                    var setup = SaveData.Setup(cfg.car);
                    playerCar = CarFactory.Spawn(CarCatalog.Get(cfg.car), setup, true, Championship.PlayerName, 99, pose.position, pose.rotation);
                    entrants.Add(new Entrant { car = playerCar, isPlayer = true, name = Championship.PlayerName, team = "Veylix Academy", number = setup.Number(playerCar.spec) });
                }
                else
                {
                    int teamSlot = aiIndex++;
                    var t = Teams.TeamForSlot(teamSlot, out int d);
                    // The grid runs in team order: faster teams start nearer the front, with a little shuffle.
                    var spec = CarCatalog.Get(AiCarFor(cfg.car, t));
                    var ai = CarFactory.Spawn(spec, Teams.SetupFor(teamSlot), false, t.drivers[d], teamSlot, pose.position, pose.rotation);
                    var drv = ai.gameObject.AddComponent<AIDriver>();
                    float skill = Mathf.Clamp(baseSkill * t.pace + Random.Range(-0.012f, 0.012f), 0.7f, 1f);
                    drv.Init(ai, session, skill, Random.Range(0.3f, 0.9f), Env.Wetness);
                    entrants.Add(new Entrant { car = ai, ai = drv, name = t.drivers[d], team = t.name, number = t.numbers[d] });
                }
            }
            session.Setup(cfg, map, entrants, entrants.Find(e => e.isPlayer));
            AttachPlayer(playerCar);
            hud.Show(session, playerCar);
            state = State.Session;
            crashHandled = false;
            sessionStartTime = Time.time;
            session.OnResults += () => { state = State.Results; Music.Play(Music.Theme.Garage); menus.ShowResults(session); };
        }

        /// <summary>AI cars race the same class as the player's car, so the field is fair.</summary>
        static int AiCarFor(int playerCar, Teams.Team t)
        {
            var p = CarCatalog.Get(playerCar);
            if (p.tier == 1) return t.car; // modern field: the two current cars
            return playerCar;
        }

        void AttachPlayer(Car c)
        {
            cockpit = c.gameObject.AddComponent<Cockpit>();
            cockpit.Attach(c);
            var dashGo = new GameObject("Dashboard");
            dashboard = dashGo.AddComponent<Dashboard>();
            dashboard.Build(c);
            mirrors = c.gameObject.AddComponent<Mirrors>();
            mirrors.Build(c);
            rig.SitIn(c);
        }

        void TearDownSession()
        {
            if (session != null) Destroy(session.gameObject);
            session = null;
            CarFactory.DespawnAll();
            playerCar = null;
            cockpit = null;
            if (dashboard != null) Destroy(dashboard.gameObject);
            foreach (var g in GameObject.FindObjectsByType<GhostCar>(FindObjectsSortMode.None)) Destroy(g.gameObject);
            if (rig != null && rig.transform.parent != null) rig.StandAt(Vector3.zero, Quaternion.identity);
        }

        public void Restart()
        {
            if (session == null) { StartSession(config); return; }
            Time.timeScale = 1f;
            AudioListener.pause = false;
            menus.Hide();
            StartCoroutine(RestartRoutine());
        }

        IEnumerator RestartRoutine()
        {
            rig.FadeTo(1f);
            float t = 0f;
            while (!rig.Faded && t < 1.5f) { t += Time.unscaledDeltaTime; yield return null; }
            if (config.mode == GameMode.Championship && Championship.S.active && Championship.Finished) Championship.End();
            var cfg = config;
            if (session != null) Destroy(session.gameObject);
            session = null;
            CarFactory.DespawnAll();
            if (dashboard != null) Destroy(dashboard.gameObject);
            foreach (var g in GameObject.FindObjectsByType<GhostCar>(FindObjectsSortMode.None)) Destroy(g.gameObject);
            rig.StandAt(Vector3.zero, Quaternion.identity);
            yield return null;
            SpawnSession(cfg, MapCatalog.Get(cfg.map));
            rig.FadeTo(0f);
        }

        /// <summary>After the results of a championship round: on to the next round, or the final standings.</summary>
        public void NextChampionshipRound()
        {
            if (!Championship.S.active || Championship.Finished) { menus.ShowStandings(true); return; }
            var cfg = config.Clone();
            cfg.mode = GameMode.Championship;
            cfg.map = Championship.CurrentMap;
            cfg.laps = Championship.S.laps;
            cfg.difficulty = Championship.S.difficulty;
            cfg.opponents = Championship.S.opponents;
            StartSession(cfg);
        }

        // ------------------------------------------------------------------ pause

        public void Pause(bool on)
        {
            if (state != State.Session && state != State.Paused) return;
            state = on ? State.Paused : State.Session;
            // Online the race can't stop for one player: the watch opens but the world keeps going.
            bool freeze = on && config.mode != GameMode.Online;
            Time.timeScale = freeze ? 0f : 1f;
            AudioListener.pause = freeze;
            // VR: the settings screen rises from the watch on your left wrist. Desktop: the pause panel.
            if (on) { if (rig != null && rig.IsXR && rig.watch != null) menus.ShowWatch(); else menus.ShowPause(); }
            else menus.Hide();
            hud.SetVisible(!on);
        }

        void Update()
        {
            if (state == State.Session || state == State.Paused)
            {
                Music.Play(Settings.RaceMusic ? Music.Theme.Race : Music.Theme.None);
                if (InputHub.MenuPressed) Pause(state == State.Session);
            }
            else if (state == State.Results || state == State.Menu)
            {
                if (InputHub.MenuPressed && state == State.Results) menus.ShowResults(session);
                // In the garage the menu button puts the menu away (to walk around) or brings it back in front of you.
                else if (InputHub.MenuPressed && state == State.Menu && studio == null) { if (menus.Showing) menus.Hide(); else menus.ShowMain(); }
            }
            if (state != State.Session || playerCar == null) return;
            // P: pit this lap on the same tyres (the watch / pause menu lets you pick another compound).
            if (InputHub.KeyDown(UnityEngine.InputSystem.Key.P) && session != null)
            {
                if (session.PitRequested) session.CancelPit(); else session.RequestPit(playerCar.setup.compound);
            }

            // Hold B (or R) for a second to put the car back on track.
            bool hold = InputHub.Right.secondary || InputHub.Key(UnityEngine.InputSystem.Key.R) || (InputHub.Pad != null && InputHub.Pad.selectButton.isPressed);
            resetHold = hold ? resetHold + Time.deltaTime : 0f;
            if (resetHold > 1f) { resetHold = -999f; ResetCar(); }

            // A big crash: fade to the chase camera (comfort), then offer to carry on or restart.
            var dmg = playerCar.damage;
            bool upsideDown = playerCar.Upright < -0.2f;
            bool wrecked = dmg != null && dmg.Wrecked;
            bool tumbling = playerCar.Body != null && playerCar.Body.angularVelocity.magnitude > 4f && playerCar.Airborne;
            if ((wrecked || upsideDown || tumbling) && !crashHandled && Time.time - sessionStartTime > 3f)
            {
                crashHandled = true;
                StartCoroutine(Crash());
            }
            if (state == State.Session && cockpit != null && session != null)
                cockpit.inputEnabled = session.phase == RaceSession.Phase.Racing || session.phase == RaceSession.Phase.Countdown || session.phase == RaceSession.Phase.Grid || (session.phase == RaceSession.Phase.Finished && !session.player.finished);
        }

        IEnumerator Crash()
        {
            if (Settings.CrashCamera == CrashView.FadeToChase && rig.IsXR)
            {
                rig.FadeTo(1f);
                float t = 0f;
                while (!rig.Faded && t < 0.6f) { t += Time.deltaTime; yield return null; }
                rig.SetView(Rig.View.Chase);
                rig.FadeTo(0f);
            }
            // Wait for the car to come to rest (or the fire to do its thing).
            float wait = 0f;
            while (playerCar != null && playerCar.SpeedMs > 2f && wait < 6f) { wait += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(1.2f);
            if (playerCar == null) yield break;
            bool stillBad = playerCar.damage != null && playerCar.damage.Wrecked || playerCar.Upright < 0.3f;
            if (stillBad && state == State.Session) menus.ShowCrash();
            else
            {
                if (rig.view == Rig.View.Chase && Settings.CrashCamera == CrashView.FadeToChase && rig.IsXR) rig.SetView(Rig.View.Cockpit);
                crashHandled = false;
            }
        }

        public void ResetCar()
        {
            if (playerCar == null || session == null) return;
            StartCoroutine(ResetRoutine());
        }

        IEnumerator ResetRoutine()
        {
            menus.Hide();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            state = State.Session;
            rig.FadeTo(1f);
            float t = 0f;
            while (!rig.Faded && t < 0.8f) { t += Time.unscaledDeltaTime; yield return null; }
            session.Recover(playerCar);
            if (rig.view != Rig.View.Cockpit) rig.SetView(Rig.View.Cockpit);
            crashHandled = false;
            rig.FadeTo(0f);
            hud.SetVisible(true);
        }

        public void Quit()
        {
            SaveData.Write();
            Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) { SaveData.Write(); Settings.Save(); if (state == State.Session) Pause(true); }
        }
    }
}
