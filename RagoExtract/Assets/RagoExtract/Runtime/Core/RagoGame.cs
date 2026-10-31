using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public enum Area { None, Bunker, Market, Raid }

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
        bool _travelling;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            new GameObject("RagoGame").AddComponent<RagoGame>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            DisableSceneCameras();
            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 35f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.pixelLightCount = 3;
            QualitySettings.antiAliasing = 4;
            QualitySettings.lodBias = 1.5f;
            Time.fixedDeltaTime = 1f / 72f;
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
            if (NetHub.Backend != null) NetHub.Backend.Init(ok => { if (ok && NetHub.Voice != null) NetHub.Voice.Init(prof.name, null); });
            Vitals.OnLocalDeath = OnLocalDeath;
            GoTo(Area.Bunker);
        }

        void DisableSceneCameras()
        {
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c != null && c.gameObject.scene.IsValid() && c.transform.root.GetComponent<PlayerRig>() == null) c.gameObject.SetActive(false);
            foreach (var l in Resources.FindObjectsOfTypeAll<AudioListener>())
                if (l != null && l.gameObject.scene.IsValid() && l.transform.root.GetComponent<PlayerRig>() == null) l.enabled = false;
            foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                if (l != null && l.gameObject.scene.IsValid() && l.type == LightType.Directional && l.transform.root != transform) l.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------------------------------ travel
        public void GoTo(Area a) { if (!_travelling) StartCoroutine(Travel(a, null)); }
        public void StartRaid(RaidStart rs) { if (!_travelling) StartCoroutine(Travel(Area.Raid, rs)); }

        IEnumerator Travel(Area to, RaidStart rs)
        {
            _travelling = true;
            Player.Hud.FadeTo(1f);
            Player.Frozen = true;
            float t = 0;
            while (!Player.Hud.FadeDone && t < 1.5f) { t += Time.deltaTime; yield return null; }
            // leave the old area
            var from = Area;
            if (from == Area.Bunker || from == Area.Market) SaveHubState();
            if (from == Area.Market && to != Area.Market) { NetGame.I.End(); if (to != Area.Raid || rs == null || !rs.online) NetHub.Backend?.Leave(); }
            if (from == Area.Raid) { NetGame.I.End(); NetHub.Backend?.Leave(); }
            Kiosk.AnyQueued = false;
            if (MapRoot != null) { Fx.ClearMapEffects(); Destroy(MapRoot.gameObject); }
            yield return null;
            // build the new one
            MapRoot = new GameObject("World").transform;
            Area = to;
            string id = to == Area.Bunker ? "bunker" : to == Area.Market ? "market" : rs.map;
            int seed = to == Area.Raid ? rs.seed : 1234;
            Map = Maps.Create(id, MapRoot, seed);
            SafeZone = Map.Safe;
            Sfx.SetAmbience(Synth.Ambience(Map.Ambience), to == Area.Bunker ? 0.25f : 0.4f);
            yield return null;
            if (to == Area.Raid)
            {
                Map.BakeNavMesh();
                RaidManager.Begin(this, Map, rs);
            }
            else
            {
                Player.Vitals.ResetFull();
                Player.Hud.RaidTimer = "";
                Player.Gear.Ammo = Profile.Current.stashAmmo;
            }
            var spawn = Map.PlayerSpawns.Count > 0 ? Map.PlayerSpawns[(rs != null ? rs.spawnIndex : Random.Range(0, Map.PlayerSpawns.Count)) % Map.PlayerSpawns.Count] : new Pose(Vector3.up * 0.1f, Quaternion.identity);
            Player.Teleport(spawn.position, spawn.rotation.eulerAngles.y);
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
            Player.Frozen = false;
            Player.Hud.FadeTo(0f);
            _travelling = false;
        }

        /// <summary>Save stash + loadout while in the bunker / market.</summary>
        public void SaveHubState()
        {
            var prof = Profile.Current;
            var bunker = Map as BunkerMap;
            var market = Map as MarketMap;
            var loose = new List<ItemData>();
            if (bunker != null && bunker.Stash != null) prof.stash = bunker.Stash.Save();
            if (market != null) foreach (var s in market.Stalls) s.SweepToStash(loose);
            // anything lying around the hub goes back to the stash
            if (MapRoot != null)
                foreach (var it in new List<Item>(Item.All))
                {
                    if (it == null || it.Held || it.slot != null || it is Magazine || it is Backpack) continue;
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
        public float TimeLeft = 25 * 60f;
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
            var dir = NpcDirector.Create(map.transform);
            dir.SpawnSquads(map.NpcSpawns, rs.seed, online && !NetHub.Backend.IsHost);
            g.Player.Hud.Center(map.DisplayName.ToUpper() + "\n<size=40>" + (rs.players > 1 ? rs.players + " operators deployed" : "Solo deployment") + " - find an extract</size>", 6f);
        }

        static int Value(Gear gear) { int v = 0; foreach (var d in gear.AllCarried()) { var def = Db.Get(d.id); if (def != null) v += def.price; } return v; }

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
            var gear = _g.Player.Gear;
            var carried = gear.AllCarried();
            // drop everything you carried as lootable items around your body
            var pos = _g.Player.Body.Rig.chest.position;
            NetGame.I?.LocalDied(pos, carried);
            if (!NetGame.Live)
                foreach (var d in carried) ItemFactory.Spawn(d, pos + Vector3.up * 0.4f + Random.insideUnitSphere * 0.4f, Random.rotation);
            gear.ClearAll();
            // everything is lost; a basic backpack and, if you're broke, a free sidearm
            prof.loadout = new Loadout { hasPack = true };
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
