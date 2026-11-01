using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Rago
{
    /// <summary>Base for every playable area: lighting, spawns, loot, extraction, vaults, runtime NavMesh.</summary>
    public abstract class MapBase : MonoBehaviour
    {
        public string Id, DisplayName;
        public int Seed;
        public WorldBuilder B;
        public readonly List<Pose> PlayerSpawns = new List<Pose>();
        public readonly List<NpcSpawn> NpcSpawns = new List<NpcSpawn>();
        public readonly List<LootCrate> Crates = new List<LootCrate>();
        public readonly List<Extraction> Extracts = new List<Extraction>();
        public readonly List<Vault> Vaults = new List<Vault>();
        public Bounds NavBounds = new Bounds(Vector3.zero, new Vector3(200, 60, 200));
        public Synth.Amb Ambience = Synth.Amb.Wind;
        public bool IsRaid = true;
        public bool Safe;
        NavMeshDataInstance _nav;
        Light _sun;

        public struct NpcSpawn { public Vector3 pos; public int count; public bool heavy; }

        public void Init(int seed)
        {
            Seed = seed;
            B = new WorldBuilder(transform, seed);
        }

        public abstract void Build();

        protected void Lighting(Color sun, float intensity, Vector3 sunEuler, Color sky, Color equator, Color ground, Color fog, float fogDensity, Color skyTop, Color skyHorizon, float clouds, float shadowStrength = 0.8f)
        {
            var lgo = new GameObject("Sun");
            lgo.transform.SetParent(transform, false);
            lgo.transform.rotation = Quaternion.Euler(sunEuler);
            _sun = lgo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = sun; _sun.intensity = intensity;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = shadowStrength;
            _sun.shadowBias = 0.04f; _sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = _sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = fogDensity > 0;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.skybox = Mats.Sky(skyTop, skyHorizon, ground * 0.8f, sun, lgo.transform.forward, clouds, Color.Lerp(skyHorizon, Color.white, 0.5f));
            RenderSettings.reflectionIntensity = 0.6f;
            DynamicGI.UpdateEnvironment();
        }

        public void BakeNavMesh()
        {
            try
            {
                var sources = new List<NavMeshBuildSource>();
                int mask = ~0;
                foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
                NavMeshBuilder.CollectSources(NavBounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
                var settings = NavMesh.GetSettingsByID(0);
                settings.agentRadius = 0.35f;
                settings.agentClimb = 0.4f;
                settings.agentSlope = 42f;
                var data = NavMeshBuilder.BuildNavMeshData(settings, sources, NavBounds, Vector3.zero, Quaternion.identity);
                if (data != null) _nav = NavMesh.AddNavMeshData(data);
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] NavMesh build failed (NPCs will hold position): " + e.Message); }
        }

        protected virtual void OnDestroy()
        {
            if (_nav.valid) NavMesh.RemoveNavMeshData(_nav);
        }

        public LootCrate AddCrate(Vector3 pos, float yaw, LootCrate.Kind kind)
        {
            var c = LootCrate.Create(this, Crates.Count, pos, yaw, kind);
            Crates.Add(c);
            return c;
        }

        public Extraction AddExtract(Vector3 pos, string name)
        {
            var e = Extraction.Create(transform, pos, name);
            Extracts.Add(e);
            return e;
        }

        public Vault AddVault(Vector3 pos, float yaw)
        {
            var v = Vault.Create(this, Vaults.Count, pos, yaw);
            Vaults.Add(v);
            return v;
        }

        public void Spawn(Vector3 p, float yaw) => PlayerSpawns.Add(new Pose(p, Quaternion.Euler(0, yaw, 0)));
        public void Squad(Vector3 p, int n, bool heavy = false) => NpcSpawns.Add(new NpcSpawn { pos = p, count = n, heavy = heavy });

        /// <summary>Scatter random loot crates around interior points.</summary>
        protected void ScatterLoot(List<Vector3> pts, float chance)
        {
            foreach (var p in pts)
            {
                if (!B.Rng.Chance(chance)) continue;
                float r = B.Rng.Value;
                var k = r < 0.46f ? LootCrate.Kind.Crate : r < 0.62f ? LootCrate.Kind.Medical : r < 0.75f ? LootCrate.Kind.Optics : r < 0.86f ? LootCrate.Kind.ArmorCase : r < 0.96f ? LootCrate.Kind.WeaponCase : LootCrate.Kind.Safe;
                AddCrate(p, B.Rng.Range(0f, 360f), k);
            }
        }
    }

    // ===================================================================================== loot crates
    public class LootCrate : MonoBehaviour
    {
        public enum Kind { Crate, WeaponCase, Optics, Medical, Safe, Duffel, ArmorCase }
        public Kind kind;
        public int index;
        public bool opened;
        Transform _lid;
        ItemContainer _box;
        MapBase _map;
        float _openAnim;

        public static LootCrate Create(MapBase map, int index, Vector3 pos, float yaw, Kind kind)
        {
            var go = new GameObject("Loot_" + kind + "_" + index);
            go.transform.SetParent(map.transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var c = go.AddComponent<LootCrate>();
            c.kind = kind; c.index = index; c._map = map;
            Vector3 size; Material mat; Color col;
            switch (kind)
            {
                case Kind.WeaponCase: size = new Vector3(0.42f, 0.16f, 1.15f); mat = Mats.Lit("case_hard", new Color(0.12f, 0.13f, 0.12f), 0.1f, 0.45f, Mats.Noise, null, 0.3f, 0.3f); col = Color.white; break;
                case Kind.Optics: size = new Vector3(0.4f, 0.2f, 0.32f); mat = Mats.Lit("case_optics", new Color(0.55f, 0.48f, 0.33f), 0.1f, 0.35f, Mats.Noise, null, 0.4f, 0.3f); col = Color.white; break;
                case Kind.Medical: size = new Vector3(0.5f, 0.28f, 0.34f); mat = Mats.Lit("case_med", new Color(0.85f, 0.85f, 0.82f), 0.1f, 0.4f, Mats.Noise, null, 0.4f, 0.3f); col = Color.white; break;
                case Kind.Safe: size = new Vector3(0.5f, 0.5f, 0.45f); mat = Mats.Lit("safe", new Color(0.2f, 0.22f, 0.24f), 0.8f, 0.4f, Mats.Metal, Mats.MetalN, 0.4f, 0.5f); col = Color.white; break;
                case Kind.ArmorCase: size = new Vector3(0.72f, 0.4f, 0.5f); mat = Mats.Lit("case_armor", new Color(0.24f, 0.27f, 0.2f), 0.15f, 0.35f, Mats.Noise, null, 0.45f, 0.35f); col = Color.white; break;
                case Kind.Duffel: size = new Vector3(0.36f, 0.28f, 0.6f); mat = Mats.Lit("duffel", new Color(0.15f, 0.15f, 0.14f), 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.4f, 0f); col = Color.white; break;
                default: size = new Vector3(0.9f, 0.5f, 0.55f); mat = Mats.Lit("crate_mil", new Color(0.33f, 0.36f, 0.26f), 0f, 0.3f, Mats.Wood, Mats.WoodN, 0.5f, 0.3f); col = Color.white; break;
            }
            // body (open top)
            var mb = new MB(2);
            float t = 0.025f;
            mb.Sub(0).Box(new Vector3(0, t * 0.5f, 0), new Vector3(size.x, t, size.z), col, 0.004f);
            mb.Sub(0).Box(new Vector3(-size.x * 0.5f + t * 0.5f, size.y * 0.5f, 0), new Vector3(t, size.y, size.z), col, 0.004f);
            mb.Sub(0).Box(new Vector3(size.x * 0.5f - t * 0.5f, size.y * 0.5f, 0), new Vector3(t, size.y, size.z), col, 0.004f);
            mb.Sub(0).Box(new Vector3(0, size.y * 0.5f, -size.z * 0.5f + t * 0.5f), new Vector3(size.x, size.y, t), col, 0.004f);
            mb.Sub(0).Box(new Vector3(0, size.y * 0.5f, size.z * 0.5f - t * 0.5f), new Vector3(size.x, size.y, t), col, 0.004f);
            mb.Sub(1).Box(new Vector3(0, t + 0.004f, 0), new Vector3(size.x - t * 2, 0.008f, size.z - t * 2), new Color(0.08f, 0.08f, 0.08f), 0); // foam / liner
            if (kind == Kind.Crate)
            {
                mb.Sub(1).Box(new Vector3(0, size.y * 0.5f, size.z * 0.5f + 0.003f), new Vector3(size.x * 0.6f, 0.08f, 0.004f), new Color(0.9f, 0.85f, 0.6f), 0);
                foreach (var sx in new[] { -1, 1 }) mb.Sub(0).Box(new Vector3(sx * (size.x * 0.5f + 0.012f), size.y * 0.5f, 0), new Vector3(0.025f, 0.05f, 0.18f), col * 0.6f, 0.005f);
            }
            if (kind == Kind.Medical) { mb.Sub(1).Box(new Vector3(0, size.y * 0.5f, size.z * 0.5f + 0.003f), new Vector3(0.1f, 0.03f, 0.004f), new Color(0.8f, 0.1f, 0.1f)); mb.Sub(1).Box(new Vector3(0, size.y * 0.5f, size.z * 0.5f + 0.003f), new Vector3(0.03f, 0.1f, 0.004f), new Color(0.8f, 0.1f, 0.1f)); }
            if (kind == Kind.ArmorCase)
            {
                // yellow stencil band + shield mark so you can spot armour cases
                mb.Sub(1).Box(new Vector3(0, size.y * 0.62f, size.z * 0.5f + 0.003f), new Vector3(size.x * 0.9f, 0.05f, 0.004f), new Color(0.85f, 0.65f, 0.12f), 0);
                mb.Sub(1).Box(new Vector3(0, size.y * 0.32f, size.z * 0.5f + 0.003f), new Vector3(0.09f, 0.1f, 0.004f), new Color(0.85f, 0.65f, 0.12f), 0.02f);
                foreach (var sx in new[] { -1, 1 }) mb.Sub(0).Box(new Vector3(sx * (size.x * 0.5f + 0.012f), size.y * 0.6f, 0), new Vector3(0.025f, 0.04f, 0.16f), col * 0.5f, 0.005f);
            }
            mb.Build("Body", go.transform, mat, Mats.Lit("liner", Color.white, 0f, 0.1f, Mats.Fabric, null, 0.2f, 0f));
            // colliders for the walls
            AddBox(go.transform, new Vector3(0, t * 0.5f, 0), new Vector3(size.x, t, size.z));
            AddBox(go.transform, new Vector3(-size.x * 0.5f + t * 0.5f, size.y * 0.5f, 0), new Vector3(t, size.y, size.z));
            AddBox(go.transform, new Vector3(size.x * 0.5f - t * 0.5f, size.y * 0.5f, 0), new Vector3(t, size.y, size.z));
            AddBox(go.transform, new Vector3(0, size.y * 0.5f, -size.z * 0.5f + t * 0.5f), new Vector3(size.x, size.y, t));
            AddBox(go.transform, new Vector3(0, size.y * 0.5f, size.z * 0.5f - t * 0.5f), new Vector3(size.x, size.y, t));
            go.AddComponent<SurfaceTag>().surface = kind == Kind.Safe ? Synth.Surface.Metal : kind == Kind.Crate ? Synth.Surface.Wood : Synth.Surface.Metal;
            // lid hinged at the back
            c._lid = new GameObject("LidHinge").transform;
            c._lid.SetParent(go.transform, false);
            c._lid.localPosition = new Vector3(0, size.y, -size.z * 0.5f);
            var lid = new MB();
            lid.Box(new Vector3(0, 0.015f, size.z * 0.5f), new Vector3(size.x + 0.01f, 0.03f, size.z + 0.01f), col, 0.006f);
            if (kind == Kind.WeaponCase || kind == Kind.Optics) for (int i = 0; i < 2; i++) lid.Box(new Vector3(-size.x * 0.3f + i * size.x * 0.6f, 0.0f, size.z + 0.008f), new Vector3(0.05f, 0.03f, 0.02f), new Color(0.4f, 0.4f, 0.4f), 0.003f);
            lid.Build("Lid", c._lid, mat);
            var lc = c._lid.gameObject.AddComponent<BoxCollider>();
            lc.center = new Vector3(0, 0.015f, size.z * 0.5f); lc.size = new Vector3(size.x, 0.03f, size.z);
            // item cells
            int cols = kind == Kind.WeaponCase ? 1 : kind == Kind.Optics || kind == Kind.ArmorCase ? 2 : 3;
            int rows = kind == Kind.WeaponCase || kind == Kind.ArmorCase ? 1 : 2;
            float fit = kind == Kind.WeaponCase ? 1.1f : Mathf.Min(size.x / cols, size.z / rows) * 0.95f;
            c._box = ItemContainer.Grid(go.transform, new Vector3(0, 0.06f, 0), Quaternion.identity, cols, rows, new Vector2(size.x / cols, size.z / rows), fit,
                null, kind == Kind.WeaponCase ? Quaternion.Euler(0, 0, 90) : Quaternion.identity);
            foreach (var s in c._box.slots) s.enabled = false;
            return c;
        }

        static void AddBox(Transform t, Vector3 c, Vector3 s) { var b = t.gameObject.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

        void Update()
        {
            if (!opened)
            {
                var p = RagoGame.I != null ? RagoGame.I.Player : null;
                if (p == null) return;
                Vector3 lp = _lid.position;
                bool near = Vector3.Distance(p.HandL.Anchor.position, lp) < 0.5f && p.HandL.Input.GripHeld
                         || Vector3.Distance(p.HandR.Anchor.position, lp) < 0.5f && p.HandR.Input.GripHeld;
                if (!XRInputs.VR && Desktop.Interact && Physics.Raycast(p.Head.position, p.Head.forward, out var h, 3f) && h.collider.transform.IsChildOf(transform)) near = true;
                if (near) Open();
            }
            if (opened && _openAnim < 1f)
            {
                _openAnim = Mathf.MoveTowards(_openAnim, 1f, Time.deltaTime * 2.5f);
                _lid.localRotation = Quaternion.Euler(-105f * Mathf.SmoothStep(0, 1, _openAnim), 0, 0);
            }
        }

        public void Open()
        {
            if (opened) return;
            opened = true;
            Sfx.Play(kind == Kind.Safe ? Synth.SlideBack() : Synth.Grab(1), transform.position, 0.6f, kind == Kind.Safe ? 0.6f : 0.8f, 1f, 10f);
            foreach (var s in _box.slots) s.enabled = true;
            var rng = new Rng(_map.Seed * 7349 + index * 131);
            var table = kind == Kind.WeaponCase ? Db.LootTable.WeaponCase : kind == Kind.Optics ? Db.LootTable.OpticsBox : kind == Kind.Medical ? Db.LootTable.Medical : kind == Kind.Safe ? Db.LootTable.Safe : kind == Kind.ArmorCase ? Db.LootTable.Armor : Db.LootTable.Crate;
            int n = kind == Kind.WeaponCase ? 1 : kind == Kind.Optics || kind == Kind.ArmorCase ? rng.Range(1, 3) : rng.Range(1, 5);
            for (int i = 0; i < n && i < _box.slots.Count; i++)
            {
                var def = Db.Roll(table, rng);
                if (def == null) continue;
                var d = new ItemData(def.id);
                if (def is GunDef gd) { d.rounds = rng.Range(0, gd.magSize + 1); d.chambered = false; }
                var it = ItemFactory.Spawn(d, _box.slots[i].transform.position, transform.rotation);
                if (it == null) continue;
                it.netId = 10000 + index * 16 + i;
                NetGame.I?.Register(it);
                _box.slots[i].Store(it, false);
            }
        }
    }

    // ===================================================================================== extraction
    public class Extraction : MonoBehaviour
    {
        public string Name;
        public float Radius = 2.6f;
        float _t; float _beep;
        public const float Time_ = 8f;
        ParticleSystem _smoke;

        public static Extraction Create(Transform parent, Vector3 pos, string name)
        {
            var go = new GameObject("Extract_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var e = go.AddComponent<Extraction>();
            e.Name = name;
            // flare + marker
            var mb = new MB(2);
            mb.Sub(0).Cyl(Vector3.zero, Vector3.up * 0.06f, e.Radius, e.Radius, 32, new Color(0.2f, 1f, 0.35f), false);
            mb.Sub(1).Cyl(new Vector3(0, 0, 0), new Vector3(0, 0.3f, 0), 0.03f, 0.03f, 8, Color.white);
            mb.Build("Marker", go.transform, Mats.Unlit("extract_ring", new Color(0.2f, 1f, 0.35f, 0.15f), Mats.Glow, Mats.BlendKind.Additive), Mats.Lit("flare", new Color(1f, 0.2f, 0.2f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(4f, 0.6f, 0.4f)));
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.4f, 1f, 0.5f); l.range = 8f; l.intensity = 1.5f;
            var sgo = new GameObject("Smoke");
            sgo.transform.SetParent(go.transform, false);
            e._smoke = sgo.AddComponent<ParticleSystem>();
            var main = e._smoke.main; main.startLifetime = 6f; main.startSpeed = 0.9f; main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startColor = new Color(0.4f, 0.95f, 0.5f, 0.35f); main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.02f;
            var em = e._smoke.emission; em.rateOverTime = 8f;
            var sh = e._smoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.1f;
            sh.rotation = new Vector3(-90, 0, 0);
            var col = e._smoke.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) }); col.color = g;
            var sol = e._smoke.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 3f)));
            var r = sgo.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Mats.Unlit("fx_extract_smoke", Color.white, Mats.Smoke);
            var sign = new GameObject("Sign"); sign.transform.SetParent(go.transform, false); sign.transform.localPosition = Vector3.up * 2.6f;
            UI.Text(sign.transform, "EXTRACT\n" + name.ToUpper(), Vector3.zero, 0.22f, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
            sign.AddComponent<Billboard>();
            return e;
        }

        void Update()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            var raid = RaidManager.I;
            if (p == null || raid == null || !p.Vitals.Alive || raid.Done) return;
            Vector3 d = p.transform.position - transform.position; d.y = 0;
            if (d.magnitude < Radius)
            {
                _t += Time.deltaTime;
                _beep -= Time.deltaTime;
                if (_beep <= 0) { _beep = 1f; Sfx.UI(Synth.Beep(), 0.4f); }
                p.Hud.Center("EXTRACTING  " + Mathf.CeilToInt(Time_ - _t), 0.2f);
                if (_t >= Time_) raid.Extract(Name);
            }
            else _t = 0;
        }
    }

    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return;
            Vector3 d = transform.position - p.Head.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }
    }

    // ===================================================================================== vault
    /// <summary>
    /// Vault: open it instantly with a Vault Keycard (bring the card to the reader), or grab the hack panel
    /// and hold on for 30 seconds while the alarm pulls every NPC nearby. Holds the vault-only guns.
    /// </summary>
    public class Vault : MonoBehaviour
    {
        public int Id;
        public bool IsOpen;
        Transform _door;
        float _anim, _alarmT;
        ItemContainer _rack, _shelf;
        MapBase _map;
        AudioSource _alarm;
        TextMesh _status;
        public float HackProgress;
        static readonly List<Vault> _all = new List<Vault>();

        public static Vault Find(int id) { foreach (var v in _all) if (v != null && v.Id == id) return v; return null; }
        void OnDestroy() { _all.Remove(this); if (_alarm != null) _alarm.Stop(); }

        /// <summary>Builds a 6x5m vault room. pos = centre of the doorway, the room extends along +Z (local).</summary>
        public static Vault Create(MapBase map, int id, Vector3 pos, float yaw)
        {
            var go = new GameObject("Vault_" + id);
            go.transform.SetParent(map.transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var v = go.AddComponent<Vault>();
            v.Id = id; v._map = map;
            _all.Add(v);
            var B = map.B;
            Quaternion q = go.transform.rotation;
            Vector3 P(float x, float y, float z) => pos + q * new Vector3(x, y, z);
            var steel = Mats.Lit("vault_steel", new Color(0.42f, 0.44f, 0.46f), 0.85f, 0.55f, Mats.Metal, Mats.MetalN, 0.25f, 0.4f, true, 1f);
            var conc = Mats.Lit("vault_conc", Color.white, 0f, 0.3f, Mats.Concrete, null, 0.4f, 0.1f, true, 0.6f);
            // room walls
            B.Wall(P(-3, 0, 0), P(-3, 0, 5), 3f, 0.4f, conc, new Color(0.7f, 0.7f, 0.72f));
            B.Wall(P(3, 0, 5), P(3, 0, 0), 3f, 0.4f, conc, new Color(0.7f, 0.7f, 0.72f));
            B.Wall(P(-3, 0, 5), P(3, 0, 5), 3f, 0.4f, conc, new Color(0.7f, 0.7f, 0.72f));
            B.Wall(P(-3, 0, 0), P(3, 0, 0), 3f, 0.5f, steel, Color.white, new List<Vector4> { new Vector4(3f, 1.8f, 0f, 2.3f) }, Synth.Surface.Metal);
            B.Box(P(0, 3f, 2.5f), new Vector3(6.4f, 0.3f, 5.4f), conc, new Color(0.6f, 0.6f, 0.62f), q);
            B.Box(P(0, 0.03f, 2.5f), new Vector3(6f, 0.06f, 5f), Mats.Lit("vault_floor", Color.white, 0.3f, 0.5f, Mats.Tiles, Mats.TilesN, 0.3f, 0.2f, true, 0.5f), new Color(0.5f, 0.52f, 0.55f), q);
            B.AddLight(P(0, 2.6f, 2.5f), new Color(1f, 0.95f, 0.85f), 7f, 1.3f);
            // door (round, swings open)
            v._door = new GameObject("DoorHinge").transform;
            v._door.SetParent(go.transform, false);
            v._door.localPosition = new Vector3(-0.95f, 1.15f, -0.2f);
            var dm = new MB(2);
            dm.Sub(0).Cyl(new Vector3(0.95f, 0, 0.0f), new Vector3(0.95f, 0, -0.35f), 1.15f, 1.15f, 28, Color.white);
            dm.Sub(1).Cyl(new Vector3(0.95f, 0, -0.36f), new Vector3(0.95f, 0, -0.42f), 0.35f, 0.3f, 18, Color.white);
            for (int i = 0; i < 3; i++) dm.Sub(1).Box(new Vector3(0.95f, 0, -0.43f), new Vector3(0.9f, 0.06f, 0.05f), Color.white, 0.01f, 1, Quaternion.Euler(0, 0, i * 60f));
            var door = dm.Build("Door", v._door, Mats.Lit("vault_door", new Color(0.55f, 0.57f, 0.6f), 0.9f, 0.6f, Mats.Metal, Mats.MetalN, 0.15f, 0.35f), Mats.Chrome);
            var dc = door.AddComponent<BoxCollider>(); dc.center = new Vector3(0.95f, 0, -0.17f); dc.size = new Vector3(2f, 2.3f, 0.36f);
            door.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            // card reader + hack panel outside, right of the door
            var reader = new GameObject("Reader").AddComponent<KeycardReader>();
            reader.transform.SetParent(go.transform, false);
            reader.transform.localPosition = new Vector3(1.6f, 1.3f, -0.3f);
            reader.vault = v; reader.Build();
            var hack = new GameObject("HackPanel").AddComponent<HackPanel>();
            hack.transform.SetParent(go.transform, false);
            hack.transform.localPosition = new Vector3(1.6f, 0.95f, -0.3f);
            hack.vault = v; hack.Build();
            v._status = UI.Text(go.transform, "VAULT LOCKED", new Vector3(0, 2.6f, -0.3f), 0.07f, new Color(1f, 0.3f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
            v._status.transform.localRotation = Quaternion.Euler(0, 0, 0);
            // racks inside
            B.Box(P(0, 1.0f, 4.6f), new Vector3(4.5f, 2f, 0.3f), steel, new Color(0.3f, 0.32f, 0.34f), q, true, Synth.Surface.Metal);
            v._rack = ItemContainer.Grid(go.transform, new Vector3(0, 1.0f, 4.35f), Quaternion.Euler(-90, 0, 0), 3, 1, new Vector2(1.3f, 0.5f), 1.15f, null, Quaternion.Euler(0, 0, 90));
            B.Table(P(-2.2f, 0, 2.5f), 90, new Vector2(1.6f, 0.8f), 0.9f);
            B.Table(P(2.2f, 0, 2.5f), 90, new Vector2(1.6f, 0.8f), 0.9f);
            v._shelf = ItemContainer.Grid(go.transform, new Vector3(0, 0.96f, 2.5f), Quaternion.identity, 2, 3, new Vector2(4.4f, 0.45f), 0.3f);
            foreach (var s in v._rack.slots) s.enabled = false;
            foreach (var s in v._shelf.slots) s.enabled = false;
            return v;
        }

        public void TryCard(Item card)
        {
            if (IsOpen || card == null || card.def == null || card.def.id != "key_vault") return;
            card.primaryHand?.ForceRelease();
            Destroy(card.gameObject);
            Sfx.Play(Synth.QueueFound(), transform.position, 0.6f, 1.2f, 1f, 15f);
            Open(true);
        }

        public void Open(bool broadcast)
        {
            if (IsOpen) return;
            IsOpen = true;
            _status.text = "VAULT OPEN";
            _status.color = new Color(0.4f, 1f, 0.5f);
            Sfx.Play(Synth.VaultDoor(), transform.position, 1f, 1f, 3f, 60f);
            _alarm = Sfx.Play(Synth.Alarm(), transform.position + Vector3.up * 2.5f, 0.7f, 1f, 5f, 120f);
            if (_alarm != null) _alarm.loop = true;
            _alarmT = 25f;
            NpcDirector.I?.AlertAll(transform.position, 80f);
            Noise.Emit(transform.position, 80f, 0);
            if (broadcast) NetGame.I?.VaultOpened(Id);
            foreach (var s in _rack.slots) s.enabled = true;
            foreach (var s in _shelf.slots) s.enabled = true;
            var rng = new Rng(_map.Seed * 31 + Id * 977 + 3);
            // guaranteed vault-only gun + 1-2 more
            var vaultGuns = Db.Guns.FindAll(g => g.rarity == Rarity.Vault);
            var picks = new List<ItemDef> { rng.Pick(vaultGuns) };
            for (int i = 0; i < 2; i++) picks.Add(Db.Roll(Db.LootTable.Vault, rng));
            int r = 0, sh = 0;
            foreach (var d in picks)
            {
                if (d == null) continue;
                bool big = d is GunDef g2 && g2.TwoHanded;
                var slot = big && r < _rack.slots.Count ? _rack.slots[r++] : sh < _shelf.slots.Count ? _shelf.slots[sh++] : null;
                if (slot == null) continue;
                var it = ItemFactory.Spawn(new ItemData(d.id), slot.transform.position, slot.transform.rotation);
                if (it == null) continue;
                it.netId = 40000 + Id * 32 + r * 8 + sh;
                NetGame.I?.Register(it);
                if (slot.Accepts(it)) slot.Store(it, false);
            }
            for (int i = 0; i < 3 && sh < _shelf.slots.Count; i++)
            {
                var d = Db.Roll(Db.LootTable.Vault, rng);
                if (d == null || d is GunDef g3 && g3.TwoHanded) continue;
                var slot = _shelf.slots[sh++];
                var it = ItemFactory.Spawn(new ItemData(d.id), slot.transform.position, slot.transform.rotation);
                if (it == null) continue;
                it.netId = 40000 + Id * 32 + 20 + i;
                NetGame.I?.Register(it);
                slot.Store(it, false);
            }
        }

        void Update()
        {
            if (IsOpen && _anim < 1f)
            {
                _anim = Mathf.MoveTowards(_anim, 1f, Time.deltaTime / 3f);
                _door.localRotation = Quaternion.Euler(0, 105f * Mathf.SmoothStep(0, 1, _anim), 0);
            }
            if (_alarm != null && _alarmT > 0) { _alarmT -= Time.deltaTime; if (_alarmT <= 0) _alarm.Stop(); }
            if (!IsOpen && HackProgress > 0) _status.text = "HACKING  " + Mathf.FloorToInt(HackProgress * 100) + "%";
        }

        public void Alarm()
        {
            if (_alarm != null && _alarm.isPlaying) return;
            _alarm = Sfx.Play(Synth.Alarm(), transform.position + Vector3.up * 2.5f, 0.7f, 1f, 5f, 120f);
            if (_alarm != null) _alarm.loop = true;
            _alarmT = 40f;
            NpcDirector.I?.AlertAll(transform.position, 90f);
        }
    }

    public class KeycardReader : MonoBehaviour
    {
        public Vault vault;
        public void Build()
        {
            var mb = new MB(2);
            mb.Sub(0).Box(Vector3.zero, new Vector3(0.14f, 0.2f, 0.05f), Color.white, 0.01f, 2);
            mb.Sub(1).Box(new Vector3(0, 0.05f, -0.026f), new Vector3(0.09f, 0.03f, 0.004f), Color.white);
            mb.Build("Reader", transform, Mats.Polymer, Mats.Lit("reader_led", new Color(1f, 0.1f, 0.1f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(3f, 0.2f, 0.2f)));
            UI.Text(transform, "KEYCARD", new Vector3(0, -0.04f, -0.03f), 0.012f, UI.Dim);
        }
        void Update()
        {
            if (vault == null || vault.IsOpen) return;
            foreach (var it in Item.All)
            {
                if (it == null || !it.Held || it.def == null || it.def.id != "key_vault") continue;
                if (Vector3.Distance(it.transform.position, transform.position) < 0.15f) { vault.TryCard(it); break; }
            }
        }
    }

    public class HackPanel : Interactable
    {
        public Vault vault;
        const float HackTime = 30f;
        float _tick;
        public void Build()
        {
            var mb = new MB(2);
            mb.Sub(0).Box(Vector3.zero, new Vector3(0.3f, 0.22f, 0.06f), Color.white, 0.01f, 2);
            mb.Sub(1).Box(new Vector3(0, 0.02f, -0.031f), new Vector3(0.22f, 0.12f, 0.004f), Color.white);
            mb.Build("Panel", transform, Mats.GunMetal, Mats.Lit("hack_screen", new Color(0.05f, 0.25f, 0.1f), 0, 0.8f, null, null, 0, 0, false, 0.5f, new Color(0.1f, 0.8f, 0.3f)));
            var c = gameObject.AddComponent<BoxCollider>(); c.size = new Vector3(0.3f, 0.22f, 0.1f);
            UI.Text(transform, "HOLD TO HACK", new Vector3(0, -0.085f, -0.035f), 0.012f, new Color(0.5f, 1f, 0.6f));
        }
        public override bool CanGrab(Hand h) => vault != null && !vault.IsOpen;
        public override void OnGrab(Hand h) { vault.Alarm(); RagoGame.I.Player.Toast("Hacking... hold on!"); }
        public override void HoldUpdate(Hand h)
        {
            if (vault == null || vault.IsOpen) return;
            if (Vector3.Distance(h.Anchor.position, transform.position) > 0.6f) return;
            vault.HackProgress += Time.deltaTime / HackTime;
            _tick -= Time.deltaTime;
            if (_tick <= 0) { _tick = 0.5f; Sfx.Play(Synth.HackTick(), transform.position, 0.4f, 1f, 1f, 10f); h.Haptic(0.1f, 0.02f); }
            if (vault.HackProgress >= 1f) vault.Open(true);
        }
        void Update()
        {
            // desktop: F starts it, keep looking at it to continue
            if (XRInputs.VR || vault == null || vault.IsOpen) return;
            var p = RagoGame.I.Player;
            if (Physics.Raycast(p.Head.position, p.Head.forward, out var hit, 2.5f, ~0, QueryTriggerInteraction.Collide) && hit.collider.gameObject == gameObject && DesktopHold())
            {
                if (vault.HackProgress <= 0) vault.Alarm();
                vault.HackProgress += Time.deltaTime / HackTime;
                if (vault.HackProgress >= 1f) vault.Open(true);
            }
        }
        static bool DesktopHold() { try { return Input.GetKey(KeyCode.F); } catch { return false; } }
    }
}
