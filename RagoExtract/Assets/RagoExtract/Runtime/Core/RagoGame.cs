using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public enum Area { None, Bunker, Market, Raid, Welcome }

    /// <summary>
    /// Rago Extract entry point. Created automatically when any scene starts, builds the player and the world,
    /// and runs the Bunker -> Market -> Raid loop.
    /// </summary>
    public class RagoGame : MonoBehaviour
    {
        public static RagoGame I;
        public PlayerRig Player;
        public Transform MapRoot;
        public MapBase Map;
        public Area Area;
        public bool SafeZone;
        public RaidResult LastResult;
        bool _travelling; float _travelStart;
        public bool ElevatorArrival;   // set by a lift: the next arrival starts inside the destination lift

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            new GameObject("RagoGame").AddComponent<RagoGame>();
        }

        /// <summary>Reset statics so Play works even with "Enter Play Mode Options" (no domain reload) turned on.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            I = null;
            Item.All.Clear(); Slot.Active.Clear(); Elevator.All.Clear(); Backpack.AllBags.Clear();
            Kiosk.AnyQueued = false;
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            try { Boot2(); }
            catch (System.Exception e) { Debug.LogError("[Rago Extract] startup failed - please send this error:\n" + e); }
        }

        /// <summary>
        /// In VR the headset paces the frames: don't force a frame rate (a mismatch with the display = judder), and run
        /// physics at the display's refresh rate so hands and held guns move in step with every rendered frame.
        /// Quest (Android) also gets lighter shadows so it holds its frame rate on the big maps.
        /// </summary>
        void ConfigureFramePacing()
        {
            if (!XRInputs.VR) return;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            float hz = 72f;
            try
            {
                var subs = new List<UnityEngine.XR.XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(subs);
                foreach (var d in subs) if (d.running && d.TryGetDisplayRefreshRate(out float r) && r > 1f) { hz = r; break; }
            }
            catch { }
            Time.fixedDeltaTime = 1f / Mathf.Clamp(hz, 60f, 120f);
            Time.maximumDeltaTime = 1f / 20f;   // after a slow frame, never run a pile of catch-up physics steps (that snowballs into stutter)
            if (Application.platform == RuntimePlatform.Android)
            {
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 25f;
                QualitySettings.pixelLightCount = 1;
            }
            Debug.Log("[Rago Extract] VR at " + hz + " Hz");
        }

        void Boot2()
        {
            DisableSceneCameras();
            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 35f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.pixelLightCount = 3;
            QualitySettings.antiAliasing = 4;
            QualitySettings.lodBias = 1.5f;
            ConfigureFramePacing();   // after the defaults, so the lighter Quest settings aren't overwritten
            if (!XRInputs.VR) Time.fixedDeltaTime = 1f / 72f;
            Physics.defaultSolverIterations = 10;
            Physics.defaultSolverVelocityIterations = 4;
            Layers.Init();
            Db.Init();
            Mats.Init();
            var prof = Profile.LoadOrCreate();
            Hand.GripPitch = prof.gripPitch;
            PlayerRig.SmoothTurn = prof.smoothTurn;
            PlayerRig.SnapAngle = prof.snapAngle;
            Sfx.MasterVolume = prof.volume; AudioListener.volume = prof.volume;
            Player = PlayerRig.Create();
            NetGame.Ensure();
            Player.Gear.LoadLoadout(prof.loadout);
            Player.Gear.Ammo = prof.stashAmmo; // in the hub you shoot from your stash ammo
            if (NetHub.Backend != null) NetHub.Backend.Init(ok => { if (ok && NetHub.Voice != null) NetHub.Voice.Init(prof.DisplayName, null); });
            Vitals.OnLocalDeath = OnLocalDeath;
            // first launch: the welcome screen, where you pick your (permanent) callsign
            GoTo(prof.nameSet ? Area.Bunker : Area.Welcome);
        }

        public const string PreviewName = "Rago World (editor preview)";

        /// <summary>
        /// Deletes the editor's preview copy of the world. It is marked "don't save", so after entering Play it no longer
        /// counts as part of the scene - it has to be found by name, otherwise it lingers (you'd see bunker signs in the Market).
        /// </summary>
        public static void RemoveEditorPreview()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.transform.parent == null && go.name == PreviewName) Destroy(go);
        }

        void DisableSceneCameras()
        {
            // the editor's imported world preview is replaced by the real, generated world when the game runs
            RemoveEditorPreview();
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c != null && c.gameObject.scene.IsValid() && c.transform.root.GetComponent<PlayerRig>() == null) c.gameObject.SetActive(false);
            foreach (var l in Resources.FindObjectsOfTypeAll<AudioListener>())
                if (l != null && l.gameObject.scene.IsValid() && l.transform.root.GetComponent<PlayerRig>() == null) l.enabled = false;
            foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                if (l != null && l.gameObject.scene.IsValid() && l.type == LightType.Directional && l.transform.root != transform) l.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------------------------------ travel
        public void GoTo(Area a) { if (!_travelling) StartCoroutine(Safe(Travel(a, null))); }
        public void StartRaid(RaidStart rs) { if (!_travelling) StartCoroutine(Safe(Travel(Area.Raid, rs))); }

        /// <summary>Runs a travel step by step; if any step throws, the screen fades back in instead of staying black.</summary>
        IEnumerator Safe(IEnumerator inner)
        {
            while (true)
            {
                object cur;
                try { if (!inner.MoveNext()) yield break; cur = inner.Current; }
                catch (System.Exception e)
                {
                    Debug.LogError("[Rago Extract] loading failed: " + e);
                    RecoverFromLoading("Loading hit an error: " + e.GetType().Name + " - " + e.Message);
                    yield break;
                }
                if (_travelling == false && _recovered) yield break;   // the watchdog already gave up on this load
                yield return cur;
            }
        }

        bool _recovered;

        /// <summary>Never leave the player on a black screen: unfreeze, fade back in and say what happened.</summary>
        void RecoverFromLoading(string why)
        {
            _travelling = false; _recovered = true;
            if (Player == null) return;
            Player.Frozen = false;
            Player.Hud.Center("", 0.01f);
            Player.Hud.Queue("");
            Player.Hud.FadeTo(0f);
            Player.Toast(why);
        }

        void Update()
        {
            // watchdog: no load may keep the screen black for more than a minute
            if (_travelling && Time.unscaledTime - _travelStart > 60f) { Debug.LogError("[Rago Extract] loading took over 60 s - fading back in"); RecoverFromLoading("Loading took too long"); }
        }

        IEnumerator Travel(Area to, RaidStart rs)
        {
            _travelling = true; _recovered = false; _travelStart = Time.unscaledTime;
            RemoveEditorPreview();
            ConfigureFramePacing();   // the headset may have come up after boot
            Player.Hud.FadeTo(1f);
            Player.Frozen = true;
            float t = 0;
            while (!Player.Hud.FadeDone && t < 1.5f) { t += Time.deltaTime; yield return null; }
            Player.Hud.Center("LOADING\n<size=40>" + (to == Area.Raid ? Maps.Name(rs.map) : to == Area.Welcome ? "Rago Extract" : to.ToString()) + "</size>", 60f);
            yield return null;
            // leave the old area
            var from = Area;
            if (from == Area.Bunker || from == Area.Market) SaveHubState();
            if (from == Area.Market && to != Area.Market) { NetGame.I.End(); if (to != Area.Raid || rs == null || !rs.online) NetHub.Backend?.Leave(); }
            if (from == Area.Raid) { NetGame.I.End(); NetHub.Backend?.Leave(); }
            Kiosk.AnyQueued = false;
            if (MapRoot != null) { Fx.ClearMapEffects(); Destroy(MapRoot.gameObject); }
            yield return null;
            System.GC.Collect();
            // build the new one
            MapRoot = new GameObject("World").transform;
            Area = to;
            string id = to == Area.Bunker ? "bunker" : to == Area.Market ? "market" : to == Area.Welcome ? "welcome" : rs.map;
            int seed = to == Area.Raid ? rs.seed : 1234;
            Map = Maps.Create(id, MapRoot, seed);
            SafeZone = Map.Safe;
            // raids: the world is merged into big 100 m chunks, so a per-pixel lamp would redraw a whole chunk. Lamps are
            // cheap vertex lights there, and on Quest the sun casts no shadows in raids (hubs keep both).
            if (to == Area.Raid) foreach (var l in MapRoot.GetComponentsInChildren<Light>(true)) if (l.type != LightType.Directional) l.renderMode = LightRenderMode.ForceVertex;
            if (Application.platform == RuntimePlatform.Android) QualitySettings.shadows = to == Area.Raid ? ShadowQuality.Disable : ShadowQuality.HardOnly;
            if (Player.Cam != null)
            {
                // raids: you can see the whole 1 km map - no draw-distance cut-off, and only a light haze instead of
                // thick fog (the far corner of the map is still about two-thirds visible)
                float far = 900f;
                if (to == Area.Raid)
                {
                    far = 1600f;
                    if (RenderSettings.fog) RenderSettings.fogDensity = Mathf.Min(RenderSettings.fogDensity, 0.00045f);
                }
                Player.Cam.farClipPlane = far;
                var cull = new float[32];
                if (Layers.Items > 0) cull[Layers.Items] = 70f;   // small loose items vanish past 70 m
                Player.Cam.layerCullDistances = cull;
                Player.Cam.layerCullSpherical = true;
            }
            Sfx.SetAmbience(Synth.Ambience(Map.Ambience), to == Area.Bunker || to == Area.Welcome ? 0.06f : 0.12f);   // quiet background air / wind
            yield return null;
            if (to == Area.Raid)
            {
                yield return Map.BakeNavMeshAsync();
                RaidManager.Begin(this, Map, rs);
            }
            else
            {
                Player.Vitals.ResetFull();
                Player.Hud.RaidTimer = "";
                Player.Gear.Ammo = Profile.Current.stashAmmo;
            }
            foreach (var sl in new[] { Player.Gear.Primary, Player.Gear.Secondary, Player.Gear.Pistol })
                if (sl.item is Gun pg) { Synth.Gunshot(pg.g); Synth.GunshotDistant(pg.g); }
            Synth.MagIn(); Synth.MagOut(); Synth.SlideForward(); Synth.SlideBack();
            // each player in a raid gets a different spawn spot; the raid seed shuffles which one
            var spawn = Map.PlayerSpawns.Count > 0 ? Map.PlayerSpawns[(rs != null ? rs.spawnIndex + Mathf.Abs(rs.seed) : Random.Range(0, Map.PlayerSpawns.Count)) % Map.PlayerSpawns.Count] : new Pose(Vector3.up * 0.1f, Quaternion.identity);
            // party members share a spawn spot: stand them side by side
            if (rs != null && rs.partySlot > 0)
            {
                int k = rs.partySlot;
                var side = spawn.rotation * Vector3.right * ((k % 2 == 1 ? 1 : -1) * ((k + 1) / 2) * 1.1f);
                spawn.position += side;
            }
            Player.Teleport(spawn.position, spawn.rotation.eulerAngles.y);
            if (ElevatorArrival)
            {
                ElevatorArrival = false;
                Elevator lift = null;
                foreach (var el in Elevator.All) if (el != null && el.transform.IsChildOf(MapRoot)) { lift = el; break; }
                if (lift != null && to != Area.Raid) { Player.Teleport(lift.ArrivePosition, lift.ArriveYaw); lift.Arrive(); }
            }
            // back in your bunker: rejoin your party (it pauses while you're in the Market or a raid)
            if (to == Area.Bunker && NetHub.Online && NetHub.Backend.PartyCode != null) NetHub.Backend.RejoinParty();
            if (to == Area.Market && NetHub.Online)
            {
                Player.Toast("Connecting to the Market...");
                NetHub.Backend.JoinMarket(ok =>
                {
                    if (ok) { NetGame.I.Begin(false); Player.Toast("Market online - " + (NetHub.Backend.Peers.Count + 1) + " operators here"); }
                    else Player.Toast("Market offline (could not connect)");
                });
            }
            if (to == Area.Bunker && LastResult != null) { ResultsBoard.Show(MapRoot, LastResult); LastResult = null; }
            // still black: look all the way round once so every shader the new area uses is compiled now, not as
            // stutters when you first turn your head; then wait for the frame rate to settle before fading in
            float spawnYaw = Player.LastTeleportYaw; Vector3 spawnPos = Player.LastTeleportPos;
            for (int k = 1; k <= 4; k++) { Player.Teleport(spawnPos, spawnYaw + k * 90f); yield return null; yield return null; }   // the last turn (360) puts you back facing the right way
            System.GC.Collect();
            yield return null;
            float budget = XRInputs.VR ? 1.6f / Mathf.Max(60f, 1f / Mathf.Max(0.001f, Time.fixedDeltaTime)) : 1f / 30f;
            int smooth = 0; float waited = 0;
            while (smooth < 20 && waited < 4f) { waited += Time.unscaledDeltaTime; smooth = Time.unscaledDeltaTime < budget ? smooth + 1 : 0; yield return null; }
            if (to != Area.Raid) Player.Hud.Center("", 0.01f);
            Player.Hud.Queue("");
            Player.Frozen = false;
            Player.Hud.FadeTo(0f);
            _travelling = false;
        }

        /// <summary>Save stash + loadout while in the bunker / market.</summary>
        public void SaveHubState()
        {
            var prof = Profile.Current;
            if (prof == null || Player == null || Player.Gear == null) return; // the game didn't finish starting: nothing to save
            var bunker = Map as BunkerMap;
            var market = Map as MarketMap;
            var loose = new List<ItemData>();
            if (bunker != null) prof.stash = bunker.SaveStash();
            if (market != null) { foreach (var s in market.Stalls) s.SweepToStash(loose); if (market.Foreman != null) market.Foreman.SweepToStash(loose); }
            // anything lying around the hub goes back to the stash
            if (MapRoot != null)
                foreach (var it in new List<Item>(Item.All))
                {
                    if (it == null || it.Held || it.slot != null || it is Magazine) continue;
                    if (!it.transform.IsChildOf(MapRoot) && it.transform.parent != null) continue;
                    loose.Add(it.Save());
                    Destroy(it.gameObject);
                }
            prof.stash.AddRange(loose);
            if (loose.Count > 0 && bunker == null) Player.Toast(loose.Count + " item(s) sent to your stash");
            prof.loadout = Player.Gear.SaveLoadout();
            prof.loadout.ammo = null;
            prof.Save();
        }

        void OnLocalDeath(int killer)
        {
            if (Area == Area.Raid && RaidManager.I != null) RaidManager.I.Died(killer);
            else StartCoroutine(HubRespawn());
        }

        IEnumerator HubRespawn()
        {
            yield return new WaitForSeconds(3f);
            Player.Revive();
            Player.Vitals.ResetFull();
            GoTo(Area.Bunker);
        }

        void OnApplicationPause(bool paused) { if (paused && (Area == Area.Bunker || Area == Area.Market)) SaveHubState(); }
        void OnApplicationQuit() { if (Area == Area.Bunker || Area == Area.Market) SaveHubState(); }
    }

    public class RaidResult
    {
        public bool extracted;
        public string map, exit, killer;
        public int value, kills;
        public float time;
    }

    /// <summary>Raid timer, spawning NPCs, extraction and death handling.</summary>
    public class RaidManager : MonoBehaviour
    {
        public static RaidManager I;
        public float TimeLeft = 40 * 60f;   // the maps are huge now
        public bool Done;
        RagoGame _g;
        MapBase _map;
        RaidStart _rs;
        float _start;
        int _startValue;

        public static void Begin(RagoGame g, MapBase map, RaidStart rs)
        {
            var go = new GameObject("RaidManager");
            go.transform.SetParent(map.transform, false);
            I = go.AddComponent<RaidManager>();
            I._g = g; I._map = map; I._rs = rs; I._start = Time.time;
            var prof = Profile.Current;
            prof.raids++;
            // pack ammo from the stash for the guns you brought
            prof.loadout = g.Player.Gear.SaveLoadout();
            prof.loadout.ammo = new int[prof.stashAmmo.Length];
            prof.PackAmmoForLoadout();
            g.Player.Gear.Ammo = (int[])prof.loadout.ammo.Clone();
            prof.Save();
            g.Player.Vitals.ResetFull();
            I._startValue = Value(g.Player.Gear);
            bool online = rs.online && NetGame.I != null && NetHub.Backend != null && NetHub.Backend.InSession;
            if (online) NetGame.I.Begin(true);
            FieldMap.Create(map, map.DisplayName);   // photographed now, while the screen is still black
            var dir = NpcDirector.Create(map.transform);
            dir.SpawnSquads(map.NpcSpawns, rs.seed, online && !NetHub.Backend.IsHost, map.PlayerSpawns);
            g.Player.Hud.Center(map.DisplayName.ToUpper() + "\n<size=40>" + (rs.players > 1 ? rs.players + " operators deployed" : "Solo deployment") + " - find an extract</size>", 6f);
        }

        static int Value(Gear gear) { int v = 0; foreach (var d in gear.AllCarried()) v += Backpack.DataValue(d); return v; }

        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            if (Done) return;
            TimeLeft -= Time.deltaTime;
            int m = Mathf.FloorToInt(Mathf.Max(0, TimeLeft) / 60f), s = Mathf.FloorToInt(Mathf.Max(0, TimeLeft) % 60f);
            _g.Player.Hud.RaidTimer = m.ToString("00") + ":" + s.ToString("00");
            if (TimeLeft <= 0) { _g.Player.Vitals.Kill("Missing in action - raid time expired"); }
        }

        public void Extract(string exit)
        {
            if (Done) return;
            Done = true;
            var prof = Profile.Current;
            prof.extracts++;
            BunkerJobs.RaidFinished(prof);
            var gear = _g.Player.Gear;
            prof.loadout = gear.SaveLoadout();
            // ammo you carried comes back to the stash
            for (int i = 0; i < prof.stashAmmo.Length; i++) prof.stashAmmo[i] += gear.Ammo[i];
            prof.loadout.ammo = null;
            prof.xp += 100 + NpcDirector.I.Kills * 50;
            prof.Save();
            Sfx.UI(Synth.Extracted(), 0.7f);
            _g.LastResult = new RaidResult { extracted = true, map = _map.DisplayName, exit = exit, value = Value(gear) - _startValue, kills = NpcDirector.I != null ? NpcDirector.I.Kills : 0, time = Time.time - _start };
            _g.GoTo(Area.Bunker);
        }

        public void Died(int killer)
        {
            if (Done) return;
            Done = true;
            var prof = Profile.Current;
            prof.deaths++;
            BunkerJobs.RaidFinished(prof);
            var gear = _g.Player.Gear;
            var carried = gear.AllCarried();
            // drop everything you carried as lootable items around your body
            var pos = _g.Player.Body.Rig.chest.position;
            NetGame.I?.LocalDied(pos, carried);
            if (!NetGame.Live)
                foreach (var d in carried) ItemFactory.Spawn(d, pos + Vector3.up * 0.4f + Random.insideUnitSphere * 0.4f, Random.rotation);
            gear.ClearAll();
            // everything is lost (armour too); a basic day pack and, if you're broke, a free sidearm
            prof.loadout = new Loadout { hasPack = true, packId = "pack_day" };
            prof.freeArmor = true;
            bool hasGun = prof.stash.Exists(d => Db.Get<GunDef>(d.id) != null);
            if (!hasGun) prof.loadout.pistol = new ItemData("p9");
            if (prof.stashAmmo[(int)Caliber.C9mm] < 34) prof.stashAmmo[(int)Caliber.C9mm] = 34;
            prof.Save();
            string by = killer >= 1000 ? "Raider" : killer > 0 ? "another operator" : killer == 0 ? "yourself" : "the environment";
            _g.LastResult = new RaidResult { extracted = false, map = _map.DisplayName, killer = by, kills = NpcDirector.I != null ? NpcDirector.I.Kills : 0, time = Time.time - _start };
            _g.Player.Hud.Center("KILLED IN ACTION", 5f);
            StartCoroutine(BackToBunker());
        }

        IEnumerator BackToBunker()
        {
            yield return new WaitForSeconds(6f);
            _g.Player.Revive();
            _g.Player.Gear.LoadLoadout(Profile.Current.loadout);
            _g.GoTo(Area.Bunker);
        }
    }

    /// <summary>Post-raid summary board in the bunker.</summary>
    public static class ResultsBoard
    {
        public static void Show(Transform parent, RaidResult r)
        {
            var root = new GameObject("Results").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0, 1.6f, 1.2f);
            var p = UI.Panel(root, "Panel", Vector3.zero, Quaternion.identity, new Vector2(1.3f, 0.75f));
            UI.Text(p, r.extracted ? "EXTRACTED" : "KILLED IN ACTION", new Vector3(0, 0.27f, 0), 0.07f, r.extracted ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.3f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
            string body = r.map + "\n" + (r.extracted ? "Exit: " + r.exit + "\nLoot gained: $" + r.value.ToString("N0") : "Killed by " + r.killer + "\nYour gear was lost") +
                          "\nKills: " + r.kills + "     Time: " + Mathf.FloorToInt(r.time / 60) + "m " + Mathf.FloorToInt(r.time % 60) + "s";
            UI.Text(p, body, new Vector3(0, -0.03f, 0), 0.032f, Color.white);
            UI.Button(p, "OK", new Vector3(0, -0.27f, 0), new Vector2(0.3f, 0.1f), () => Object.Destroy(root.gameObject));
        }
    }
}
