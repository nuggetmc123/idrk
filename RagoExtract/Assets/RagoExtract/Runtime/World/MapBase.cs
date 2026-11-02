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
        public readonly List<Bounds> ExtraNav = new List<Bounds>();   // outskirts locations get their own small navmesh
        public float Half;                                              // map half-size (for the field map)
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

        /// <summary>
        /// Bakes the raiders' navmesh over many frames (the main area plus each outskirts location), so the game - and
        /// the headset - keeps running while it builds instead of freezing for seconds.
        /// </summary>
        public System.Collections.IEnumerator BakeNavMeshAsync()
        {
            var jobs = new List<(Bounds, List<NavMeshBuildSource>)>();
            NavMeshBuildSettings settings;
            try
            {
                int mask = ~0;
                foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
                settings = NavMesh.GetSettingsByID(0);
                settings.agentRadius = 0.35f;
                settings.agentClimb = 0.4f;
                settings.agentSlope = 42f;
                settings.overrideVoxelSize = true; settings.voxelSize = 0.15f;   // a bit coarser: big maps bake much faster
                var all = new List<Bounds> { NavBounds }; all.AddRange(ExtraNav);
                foreach (var nb in all)
                {
                    var src = new List<NavMeshBuildSource>();
                    NavMeshBuilder.CollectSources(nb, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), src);
                    jobs.Add((nb, src));
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] NavMesh build failed (NPCs will hold position): " + e.Message); yield break; }
            for (int i = 0; i < jobs.Count; i++)
            {
                var (bounds, src) = jobs[i];
                var data = new NavMeshData();
                AsyncOperation op = null;
                try { op = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, src, bounds); }
                catch (System.Exception e) { Debug.LogWarning("[Rago] NavMesh part failed: " + e.Message); continue; }
                float waited = 0;
                while (op != null && !op.isDone && waited < 15f) { waited += Time.unscaledDeltaTime; yield return null; }   // never hold the loading screen forever
                if (this == null) yield break;   // left the map while baking
                var inst = NavMesh.AddNavMeshData(data);
                if (i == 0) _nav = inst; else _extraNav.Add(inst);
            }
        }

        readonly List<NavMeshDataInstance> _extraNav = new List<NavMeshDataInstance>();

        protected virtual void OnDestroy()
        {
            foreach (var n in _extraNav) if (n.valid) NavMesh.RemoveNavMeshData(n);
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

        /// <summary>
        /// Drop every loot crate onto whatever is really under it (floor, table, terrain), so none start sunk into a floor:
        /// crate spots are written as "floor level" but floors have thickness and terrain has bumps.
        /// </summary>
        public void SettleCrates()
        {
            Physics.SyncTransforms();
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            var hits = new RaycastHit[24];
            foreach (var c in Crates)
            {
                if (c == null) continue;
                var p = c.transform.position;
                // probe the centre and the four corners of the crate's footprint: it rests on the highest support, so a
                // crate half over a stair hole or a floor edge can't end up sunk into the floor
                Vector3 hs = (c.Size.sqrMagnitude > 0 ? c.Size : new Vector3(0.6f, 0.4f, 0.5f)) * 0.45f;
                var r = c.transform.rotation;
                float best = float.NegativeInfinity;
                foreach (var o in new[] { Vector3.zero, new Vector3(hs.x, 0, hs.z), new Vector3(-hs.x, 0, hs.z), new Vector3(hs.x, 0, -hs.z), new Vector3(-hs.x, 0, -hs.z) })
                {
                    int n = Physics.RaycastNonAlloc(p + r * o + Vector3.up * 1.5f, Vector3.down, hits, 3.0f, mask, QueryTriggerInteraction.Ignore);
                    float top = float.NegativeInfinity;
                    for (int i = 0; i < n; i++)
                    {
                        var t = hits[i].collider.transform;
                        if (t.IsChildOf(c.transform)) continue;
                        var other = t.GetComponentInParent<LootCrate>();
                        if (other != null && other != c) continue;   // stacked spots: ignore other crates, settle on the floor
                        if (hits[i].normal.y < 0.5f || hits[i].point.y > p.y + 1.4f) continue;   // not walls, not a shelf or ceiling far above
                        if (hits[i].point.y > top) top = hits[i].point.y;
                    }
                    if (top > best) best = top;
                }
                if (best > float.NegativeInfinity) { p.y = best + 0.004f; c.transform.position = p; }
            }
            Physics.SyncTransforms();
        }

        public void Spawn(Vector3 p, float yaw) => PlayerSpawns.Add(new Pose(p, Quaternion.Euler(0, yaw, 0)));
        public void Squad(Vector3 p, int n, bool heavy = false) => NpcSpawns.Add(new NpcSpawn { pos = p, count = n, heavy = heavy });

        /// <summary>
        /// A street of houses you can walk into: a road along local +x from origin with lots on both sides.
        /// Every house has a doorway facing the road, stairs, windows, furniture along the walls and loot points inside.
        /// Returns the interior loot points (feed them to ScatterLoot).
        /// </summary>
        protected List<Vector3> Neighborhood(Vector3 origin, float yaw, int lots, float lotW, bool wooden)
        {
            var b = B;
            var pts = new List<Vector3>();
            var q = Quaternion.Euler(0, yaw, 0);
            float len = lots * lotW;
            Vector3 P(float x, float y, float z) => origin + q * new Vector3(x, y, z);
            var road = wooden ? Maps.GroundDirt : Maps.Asphalt;
            b.Box(P(len / 2, 0.015f, 0), new Vector3(len + 12, 0.02f, 7), road, Color.white, q, false);
            Color[] paint = { new Color(0.85f, 0.8f, 0.7f), new Color(0.72f, 0.78f, 0.82f), new Color(0.82f, 0.72f, 0.6f), new Color(0.7f, 0.75f, 0.62f), new Color(0.9f, 0.88f, 0.82f), new Color(0.78f, 0.62f, 0.55f) };
            Color[] fabric = { new Color(0.45f, 0.25f, 0.2f), new Color(0.25f, 0.3f, 0.4f), new Color(0.35f, 0.38f, 0.28f), new Color(0.5f, 0.45f, 0.38f) };
            var fabricMat = Mats.Lit("furn_fabric", Color.white, 0f, 0.12f, Mats.Fabric, Mats.FabricN, 0.45f, 0.05f);
            var woodMat = Mats.Lit("furn_wood", Color.white, 0f, 0.35f, Mats.Wood, Mats.WoodN, 0.4f, 0.2f, true, 1f);
            foreach (int side in new[] { 1, -1 })
                for (int i = 0; i < lots; i++)
                {
                    if (b.Rng.Chance(0.12f)) continue; // an empty lot now and then
                    float w = b.Rng.Range(8f, 10.5f), d = b.Rng.Range(7f, 9f);
                    int floors = b.Rng.Chance(0.45f) ? 2 : 1;
                    float x = (i + 0.5f) * lotW + b.Rng.Range(-1f, 1f);
                    float z = side * (4.5f + d / 2 + b.Rng.Range(1.5f, 3.5f));
                    float hy = yaw + (side > 0 ? 0 : 180);   // doorway (local -z) faces the road
                    var hq = Quaternion.Euler(0, hy, 0);
                    Vector3 hp = P(x, 0, z);
                    bool brick = !wooden && b.Rng.Chance(0.4f);
                    var wall = wooden ? Maps.WoodPlank : brick ? Maps.Brick : Maps.Plaster;
                    Color tint = wooden ? new Color(0.62f, 0.5f, 0.38f) * b.Rng.Range(0.85f, 1.1f) : brick ? Color.white : b.Rng.Pick(paint);
                    pts.AddRange(b.Building(hp, hy, new Vector2(w, d), floors, wall, tint, Maps.WoodPlank, wooden ? Maps.WoodPlank : Maps.ConcreteDark, 0, true));
                    // furniture against the walls (loot points stay in the middle of the rooms)
                    float hx = w / 2, hz = d / 2;
                    Vector3 H(float lx, float ly, float lz) => hp + hq * new Vector3(lx, ly, lz);
                    for (int f = 0; f < floors; f++)
                    {
                        float y = f * 3.2f + 0.1f;
                        bool bedroom = floors > 1 ? f == floors - 1 : b.Rng.Chance(0.5f);
                        var fc = b.Rng.Pick(fabric);
                        if (bedroom)
                        {
                            b.Box(H(-hx + 1.15f, y + 0.25f, hz * 0.2f), new Vector3(1.9f, 0.5f, 1.5f), woodMat, new Color(0.5f, 0.38f, 0.28f), hq, true, Synth.Surface.Wood);
                            b.Box(H(-hx + 1.15f, y + 0.55f, hz * 0.2f), new Vector3(1.8f, 0.14f, 1.4f), fabricMat, fc, hq, false, Synth.Surface.Dirt, 0.04f);
                            b.Box(H(-hx + 0.4f, y + 0.72f, hz * 0.2f), new Vector3(0.3f, 0.14f, 1.2f), fabricMat, new Color(0.9f, 0.88f, 0.84f), hq, false, Synth.Surface.Dirt, 0.05f);
                        }
                        else
                        {
                            // couch + coffee table
                            b.Box(H(-hx + 0.55f, y + 0.25f, 0), new Vector3(0.9f, 0.5f, 2.1f), fabricMat, fc, hq, true, Synth.Surface.Dirt, 0.06f);
                            b.Box(H(-hx + 0.25f, y + 0.6f, 0), new Vector3(0.3f, 0.5f, 2.1f), fabricMat, fc * 0.9f, hq, false, Synth.Surface.Dirt, 0.06f);
                        }
                        b.Table(H(-hx * 0.15f, y, hz - 0.75f), hy, new Vector2(1.4f, 0.8f), 0.78f);
                        if (w > 8.5f) b.Shelf(H(-hx + 1.7f, y, -hz + 0.4f), hy, 1.4f);
                        b.Box(H(-0.5f, y + 0.012f, 0), new Vector3(2.4f, 0.01f, 1.7f), fabricMat, b.Rng.Pick(fabric) * 0.8f, hq, false); // rug
                    }
                    // yard: low picket fence on the road side with a gap at the path, a mailbox
                    float fz = -hz - b.Rng.Range(1.5f, 2.2f);
                    foreach (var sx in new[] { -1, 1 })
                        b.Box(H(sx * (hx * 0.5f + 1.1f), 0.45f, fz), new Vector3(hx - 1.3f, 0.9f, 0.06f), woodMat, new Color(0.88f, 0.86f, 0.8f), hq, true, Synth.Surface.Wood);
                    b.Box(H(1.6f, 0.6f, fz - 0.3f), new Vector3(0.25f, 0.2f, 0.4f), Maps.Metal, new Color(0.25f, 0.3f, 0.35f), hq, false, Synth.Surface.Metal);
                    b.Cyl(H(1.6f, 0f, fz - 0.3f), H(1.6f, 0.5f, fz - 0.3f), 0.03f, 0.03f, 6, woodMat, new Color(0.4f, 0.3f, 0.2f), false);
                    if (b.Rng.Chance(0.3f)) b.CarWreck(H(hx + 2.4f, 0, -hz + 1f), hy + 90 + b.Rng.Range(-10f, 10f), b.Rng.Pick(paint) * 0.6f);
                }
            // street lights + a few wrecks on the road
            for (int i = 0; i <= lots; i += 2) b.LightPole(P(i * lotW, 0, 4.2f), 6f, new Color(1f, 0.82f, 0.6f), false);
            for (int i = 0; i < lots / 2; i++) if (b.Rng.Chance(0.5f)) b.CarWreck(P(b.Rng.Range(4f, len - 4f), 0, b.Rng.Range(-2f, 2f)), yaw + b.Rng.Range(-20f, 20f), b.Rng.Pick(paint) * 0.55f);
            return pts;
        }

        // ================================================================================== outskirts
        /// <summary>
        /// Turns a map into a huge one: open country out to +-half around the existing core, with roads out to
        /// far-flung locations (farms, army camps, gas stations, warehouses, cabins, little streets), each with loot,
        /// raiders and its own navmesh, extracts at the far ones, forest everywhere and a fenced border.
        /// </summary>
        protected void Outskirts(Rect core, float half, string[] extractNames, bool wooden, bool seaEast, Material ground, int pois = 8)
        {
            var b = B;
            Half = half;
            float y = -0.02f;
            // ground around the core
            b.Ground(new Vector3((-half + core.xMin) / 2, 0, 0), new Vector2(core.xMin + half, half * 2), ground, Synth.Surface.Grass, y);
            if (seaEast) b.Water(new Vector3((core.xMax + half) / 2 + 30, -1.6f, 0), new Vector2(half - core.xMax + 60, half * 2));
            else b.Ground(new Vector3((core.xMax + half) / 2, 0, 0), new Vector2(half - core.xMax, half * 2), ground, Synth.Surface.Grass, y);
            b.Ground(new Vector3(core.center.x, 0, (-half + core.yMin) / 2), new Vector2(core.width, core.yMin + half), ground, Synth.Surface.Grass, y);
            b.Ground(new Vector3(core.center.x, 0, (core.yMax + half) / 2), new Vector2(core.width, half - core.yMax), ground, Synth.Surface.Grass, y);
            var roadMat = wooden ? Maps.GroundDirt : Maps.Asphalt;
            // pick locations on a ring around the core
            var spots = new List<Vector3>();
            int tries = 0;
            while (spots.Count < pois && tries++ < 400)
            {
                float ang = b.Rng.Range(0f, Mathf.PI * 2f);   // spacing below keeps them spread around the core
                float r = b.Rng.Range(0.5f, 0.82f) * half;
                var p = new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
                if (Mathf.Abs(p.x) > half - 45 || Mathf.Abs(p.z) > half - 45) continue;
                if (p.x > core.xMin - 45 && p.x < core.xMax + 45 && p.z > core.yMin - 45 && p.z < core.yMax + 45) continue;
                if (seaEast && p.x > core.xMax - 20) continue;
                bool clash = false; foreach (var o in spots) if ((o - p).sqrMagnitude < (tries > 250 ? 80 * 80 : 120 * 120)) clash = true;
                if (clash) continue;
                spots.Add(p);
            }
            var roads = new List<(Vector3, Vector3)>();
            int kind = b.Rng.Range(0, 6);
            foreach (var p in spots)
            {
                // road from the core edge out to the location
                var edge = new Vector3(Mathf.Clamp(p.x, core.xMin + 5, core.xMax - 5), 0, Mathf.Clamp(p.z, core.yMin + 5, core.yMax - 5));
                Road(edge, p, roadMat); roads.Add((edge, p));
                float yaw = Mathf.Atan2(edge.x - p.x, edge.z - p.z) * Mathf.Rad2Deg; // locations face their road
                var pts = new List<Vector3>();
                switch (kind++ % 6)
                {
                    case 0: pts = Farm(p, yaw); break;
                    case 1: pts = ArmyCamp(p, yaw); break;
                    case 2: pts = GasStation(p, yaw); break;
                    case 3: pts = Warehouse(p, yaw); break;
                    case 4: pts = Cabins(p, yaw, wooden); break;
                    default: pts = Neighborhood(p + Quaternion.Euler(0, yaw + 90, 0) * new Vector3(-27, 0, 0), yaw + 90, 4, 13.5f, wooden); break;
                }
                ScatterLoot(pts, 0.85f);
                Squad(p + new Vector3(b.Rng.Range(-8f, 8f), 0, b.Rng.Range(-8f, 8f)), b.Rng.Range(2, 4), kind % 6 == 2);
                ExtraNav.Add(new Bounds(p + Vector3.up * 5, new Vector3(80, 30, 80)));
            }
            // only 3 extracts per map: the first one in town, plus two far out on opposite sides
            for (int i = Extracts.Count - 1; i >= 1; i--) { if (Extracts[i] != null) Compat.Kill(Extracts[i].gameObject); Extracts.RemoveAt(i); }
            var far = new List<Vector3>(spots);
            far.Sort((a1, a2) => a2.sqrMagnitude.CompareTo(a1.sqrMagnitude));
            var picks = new List<Vector3>();
            if (far.Count > 0) picks.Add(far[0]);
            if (far.Count > 1) { var best = far[1]; foreach (var f in far) if ((f - far[0]).sqrMagnitude > (best - far[0]).sqrMagnitude) best = f; picks.Add(best); }
            for (int i = 0; i < picks.Count && i < extractNames.Length; i++)
            {
                var dir = new Vector3(picks[i].x, 0, picks[i].z).normalized;
                var e = picks[i] + dir * 32f;
                e.x = Mathf.Clamp(e.x, -half + 15, half - 15); e.z = Mathf.Clamp(e.z, -half + 15, half - 15);
                Road(picks[i], e, roadMat); roads.Add((picks[i], e));
                AddExtract(e, extractNames[i]);
            }
            // forest + rocks everywhere else
            int trees = wooden ? 700 : 420;
            for (int i = 0; i < trees; i++)
            {
                var t = new Vector3(b.Rng.Range(-half + 6, half - 6), 0, b.Rng.Range(-half + 6, half - 6));
                if (t.x > core.xMin - 6 && t.x < core.xMax + 6 && t.z > core.yMin - 6 && t.z < core.yMax + 6) continue;
                if (seaEast && t.x > core.xMax - 4) continue;
                if (Near(t, spots, 42f) || NearRoad(t, roads, 6f)) continue;
                // mixed woods: mostly pines in pine country, mostly leafy trees (some birches) elsewhere
                float pineShare = wooden ? 0.65f : 0.3f;
                if (b.Rng.Value < pineShare) b.Pine(t, b.Rng.Range(8f, 15f));
                else b.LeafyTree(t, b.Rng.Range(7f, 13f), b.Rng.Value < 0.25f);
                // undergrowth at the foot of some trees
                if (b.Rng.Value < 0.35f) { var o = b.Rng.InCircle() * 3f; b.Fern(t + new Vector3(o.x, y, o.y), b.Rng.Range(0.6f, 1.0f)); }
            }
            // bushes you can walk through (and hide in)
            for (int i = 0; i < 520; i++)
            {
                var t = new Vector3(b.Rng.Range(-half + 6, half - 6), y, b.Rng.Range(-half + 6, half - 6));
                if (t.x > core.xMin - 4 && t.x < core.xMax + 4 && t.z > core.yMin - 4 && t.z < core.yMax + 4) continue;
                if (seaEast && t.x > core.xMax - 4) continue;
                if (Near(t, spots, 30f) || NearRoad(t, roads, 4f)) continue;
                b.Bush(t, b.Rng.Range(1.1f, 2.2f));
            }
            // patches of tall grass and ferns on the ground
            for (int i = 0; i < 260; i++)
            {
                var c = new Vector3(b.Rng.Range(-half + 6, half - 6), y, b.Rng.Range(-half + 6, half - 6));
                if (c.x > core.xMin - 4 && c.x < core.xMax + 4 && c.z > core.yMin - 4 && c.z < core.yMax + 4) continue;
                if (seaEast && c.x > core.xMax - 4) continue;
                if (Near(c, spots, 25f) || NearRoad(c, roads, 3f)) continue;
                int n = b.Rng.Range(8, 16);
                for (int k = 0; k < n; k++)
                {
                    var o = b.Rng.InCircle() * 5f;
                    var p = c + new Vector3(o.x, 0, o.y);
                    if (b.Rng.Value < 0.82f) b.GrassTuft(p, b.Rng.Range(0.45f, 0.85f)); else b.Fern(p, b.Rng.Range(0.5f, 0.9f));
                }
            }
            for (int i = 0; i < 90; i++)
            {
                var t = new Vector3(b.Rng.Range(-half + 6, half - 6), 0, b.Rng.Range(-half + 6, half - 6));
                if (t.x > core.xMin - 6 && t.x < core.xMax + 6 && t.z > core.yMin - 6 && t.z < core.yMax + 6) continue;
                if ((seaEast && t.x > core.xMax - 4) || Near(t, spots, 42f) || NearRoad(t, roads, 6f)) continue;
                b.Rock(t, b.Rng.Range(1f, 3.5f));
            }
            // border: fence, plus invisible walls so nobody walks off the edge of the world
            var corners = new[] { new Vector3(-half, 0, -half), new Vector3(half, 0, -half), new Vector3(half, 0, half), new Vector3(-half, 0, half) };
            for (int i = 0; i < 4; i++)
            {
                var a1 = corners[i]; var a2 = corners[(i + 1) % 4];
                if (!(seaEast && i == 1)) b.Fence(a1, a2, 3f);
                var mid = (a1 + a2) / 2; var d = a2 - a1;
                b.Collider(mid + Vector3.up * 10, new Vector3(1, 20, d.magnitude), Quaternion.LookRotation(d.normalized), Synth.Surface.Concrete);
            }
            // spawn spots out towards the border, well away from the raiders. Every player in a raid gets a different one:
            // about half are small lootable places (a shed, a cabin, a supply tent, an open container) with a few crates
            // to search before you move out, and the rest are out in the open (a wrecked car, a rock pile, a sandbag nest).
            PlayerSpawns.Clear();
            int sites = 0;
            for (int i = 0; i < 70 && sites < 10; i++)
            {
                float ang = (sites + i * 0.37f) / 10f * Mathf.PI * 2f;
                var hp = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (half * b.Rng.Range(0.8f, 0.9f));
                hp.x = Mathf.Clamp(hp.x, -half + 30, half - 30); hp.z = Mathf.Clamp(hp.z, -half + 30, half - 30);
                if (seaEast && hp.x > core.xMax - 25) continue;
                if (Near(hp, spots, 75f) || NearRoad(hp, roads, 10f)) continue;
                bool crowded = false; foreach (var sp in PlayerSpawns) if ((sp.position - hp).sqrMagnitude < 120 * 120) crowded = true;
                if (crowded) continue;
                if (sites % 2 == 0) LootSpawn(hp, sites / 2 % 4, wooden); else OpenSpawn(hp, sites / 2 % 3);
                sites++;
            }
        }

        /// <summary>A small lootable place you can start the raid in: you stand inside, facing out towards the middle of the map.</summary>
        void LootSpawn(Vector3 p, int kind, bool wooden)
        {
            var b = B;
            float yaw = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;   // local -z (doorway / open end) faces the map centre
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float z) => p + q * new Vector3(x, 0, z);
            float face = yaw + 180f;   // what the player looks at: out of the door
            LootCrate.Kind Pick() { float r = b.Rng.Value; return r < 0.5f ? LootCrate.Kind.Crate : r < 0.7f ? LootCrate.Kind.Medical : r < 0.82f ? LootCrate.Kind.Optics : r < 0.92f ? LootCrate.Kind.Duffel : LootCrate.Kind.WeaponCase; }
            switch (kind)
            {
                case 0: // tool shed: shelves and a workbench
                    b.Building(p, yaw, new Vector2(4.6f, 4.2f), 1, wooden ? Maps.WoodPlank : Maps.ConcreteDark, wooden ? new Color(0.55f, 0.43f, 0.32f) : new Color(0.6f, 0.62f, 0.58f), Maps.WoodPlank, wooden ? Maps.WoodPlank : Maps.Concrete, 0, true);
                    b.Shelf(P(0, 1.65f), yaw, 3f);
                    b.Table(P(-1.55f, 0.2f), yaw + 90, new Vector2(1.6f, 0.7f));
                    AddCrate(P(1.4f, 0.7f), yaw, Pick()); AddCrate(P(-1.5f, 0.2f) + Vector3.up * 0.82f, yaw + 90, LootCrate.Kind.Duffel);
                    b.AddLight(p + Vector3.up * 2.6f, new Color(1f, 0.85f, 0.6f), 5f, 0.8f);
                    break;
                case 1: // hunter's cabin: cot, table, footlocker
                    b.Building(p, yaw, new Vector2(6f, 5f), 1, Maps.WoodPlank, new Color(0.5f, 0.38f, 0.27f), Maps.WoodPlank, Maps.WoodPlank, 0, true);
                    b.Box(P(-2.2f, 1.2f) + Vector3.up * 0.3f, new Vector3(0.9f, 0.45f, 2f), Maps.WoodPlank, new Color(0.45f, 0.36f, 0.28f), q, true, Synth.Surface.Wood); // cot
                    b.Table(P(1.6f, 1.2f), yaw, new Vector2(1.4f, 0.9f));
                    AddCrate(P(1.6f, 1.2f) + Vector3.up * 0.8f, yaw, Pick()); AddCrate(P(-2.2f, -1.5f), yaw + 90, LootCrate.Kind.Crate); AddCrate(P(2.3f, -1.4f), yaw, Pick());
                    b.AddLight(p + Vector3.up * 2.6f, new Color(1f, 0.8f, 0.55f), 6f, 0.8f);
                    break;
                case 2: // supply tent behind sandbags
                    b.Tent(P(0, 0.6f), yaw, new Vector3(4.2f, 2.4f, 4.6f), new Color(0.32f, 0.36f, 0.24f));
                    b.Sandbags(P(-3f, 3.4f), P(3f, 3.4f), 2);
                    b.Crate(P(-1.3f, 2f), yaw, new Vector3(0.9f, 0.7f, 0.9f));
                    AddCrate(P(1.1f, 2f), yaw, LootCrate.Kind.Medical); AddCrate(P(-1.2f, 0.3f), yaw + 90, Pick()); AddCrate(P(1.2f, 0.4f), yaw, LootCrate.Kind.Crate);
                    b.Barrel(P(2.6f, -1.6f), new Color(0.3f, 0.35f, 0.25f));
                    break;
                default: // an open shipping container someone was living out of
                    b.ShippingContainer(P(0, 0.8f), yaw + 180f, new Color(0.42f, 0.2f, 0.14f), true);   // open end faces the centre
                    AddCrate(P(-0.6f, 2.6f), yaw, LootCrate.Kind.Crate); AddCrate(P(0.6f, 1.6f), yaw, Pick()); AddCrate(P(-0.6f, 0.4f), yaw + 90, LootCrate.Kind.Duffel);
                    b.Barrel(P(-1.9f, -3.4f), new Color(0.2f, 0.3f, 0.45f)); b.Crate(P(-2.8f, -1.6f), yaw + 20, new Vector3(1f, 0.8f, 1f));
                    b.AddLight(P(0, 1.5f) + Vector3.up * 2.2f, new Color(1f, 0.85f, 0.6f), 5f, 0.7f);
                    p = P(0, -0.6f);
                    break;
            }
            Spawn(p + Vector3.up * 0.15f, face);
        }

        /// <summary>A spawn out in the open, behind a bit of cover, with nothing to loot.</summary>
        void OpenSpawn(Vector3 p, int kind)
        {
            var b = B;
            float yaw = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;   // local -z faces the map centre: the cover is on that side
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float z) => p + q * new Vector3(x, 0, z);
            switch (kind)
            {
                case 0: b.CarWreck(P(0, -2.6f), yaw + 80f, new Color(0.35f, 0.38f, 0.42f)); b.Rock(P(3.5f, 1.5f), 1.6f); break;            // wrecked car
                case 1: b.Rock(P(-1.8f, -2f), 3f); b.Rock(P(1.9f, -2.4f), 2.2f); b.Rock(P(0.3f, 2.4f), 1.4f); b.Pine(P(-4f, 2f), 11f); break;  // rock pile in the trees
                default: b.Sandbags(P(-2.5f, -2f), P(2.5f, -2f), 3); b.Sandbags(P(-2.5f, -2f), P(-2.5f, 1.5f), 2); b.JerseyBarrier(P(3.5f, 0.5f), yaw + 90); break; // old sandbag nest
            }
            Spawn(p + Vector3.up * 0.15f, yaw + 180f);
        }

        static bool Near(Vector3 p, List<Vector3> spots, float r) { foreach (var s in spots) if ((s - p).sqrMagnitude < r * r) return true; return false; }

        static bool NearRoad(Vector3 p, List<(Vector3, Vector3)> roads, float r)
        {
            foreach (var (a, c) in roads)
            {
                var ab = c - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                if ((a + ab * t - p).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        void Road(Vector3 a, Vector3 c, Material m)
        {
            var d = c - a; d.y = 0; float len = d.magnitude;
            if (len < 1) return;
            // split long roads so each piece lands in its own world chunk
            int pieces = Mathf.CeilToInt(len / 60f);
            for (int i = 0; i < pieces; i++)
            {
                var p0 = a + d * (i / (float)pieces); var p1 = a + d * ((i + 1) / (float)pieces);
                B.Box((p0 + p1) / 2 + Vector3.up * 0.015f, new Vector3(7, 0.02f, len / pieces + 0.2f), m, Color.white, Quaternion.LookRotation(d.normalized), false);
            }
        }

        List<Vector3> Farm(Vector3 p, float yaw)
        {
            var b = B; var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float z) => p + q * new Vector3(x, 0, z);
            var pts = b.Building(P(-6, 4), yaw, new Vector2(16, 11), 1, Maps.WoodPlank, new Color(0.55f, 0.2f, 0.15f), Maps.WoodPlank, Maps.WoodPlank, 0, true, false, 5f);
            pts.AddRange(b.Building(P(14, 2), yaw, new Vector2(9, 8), 2, Maps.Plaster, new Color(0.88f, 0.84f, 0.74f), Maps.WoodPlank, Maps.ConcreteDark, 0, true));
            foreach (var x in new[] { -20f, -26f }) { b.Cyl(P(x, 6), P(x, 6) + Vector3.up * 11, 2.3f, 2.3f, 16, Maps.Metal, new Color(0.75f, 0.76f, 0.78f), true, Synth.Surface.Metal); b.Sphere(P(x, 6) + Vector3.up * 11, new Vector3(2.3f, 1.4f, 2.3f), 14, Maps.Metal, new Color(0.7f, 0.7f, 0.72f), false); }
            for (int i = 0; i < 8; i++) b.Cyl(P(b.Rng.Range(-14f, 6f), b.Rng.Range(-14f, -8f)) + Vector3.up * 0.6f, P(b.Rng.Range(-14f, 6f), b.Rng.Range(-14f, -8f)) + Vector3.up * 0.6f + q * Vector3.right * 1.4f, 0.6f, 0.6f, 12, Maps.GroundDirt, new Color(0.85f, 0.72f, 0.35f), true, Synth.Surface.Dirt); // hay
            var wood = Maps.WoodPlank;
            foreach (var (a, c) in new[] { (P(-30, -20), P(26, -20)), (P(26, -20), P(26, 18)), (P(26, 18), P(-30, 18)) })
            { var d = c - a; b.Box((a + c) / 2 + Vector3.up * 0.6f, new Vector3(0.12f, 0.12f, d.magnitude), wood, new Color(0.5f, 0.4f, 0.3f), Quaternion.LookRotation(d.normalized), false); }
            b.CarWreck(P(4, -12), yaw + 30, new Color(0.25f, 0.4f, 0.25f));
            AddCrate(P(-10, -4), b.Rng.Range(0f, 360f), LootCrate.Kind.Crate);
            return pts;
        }

        List<Vector3> ArmyCamp(Vector3 p, float yaw)
        {
            var b = B; var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float z) => p + q * new Vector3(x, 0, z);
            var pts = new List<Vector3>();
            b.Sandbags(P(-16, -16), P(-3, -16)); b.Sandbags(P(3, -16), P(16, -16)); b.Sandbags(P(16, -16), P(16, 16)); b.Sandbags(P(16, 16), P(-16, 16)); b.Sandbags(P(-16, 16), P(-16, -16));
            var olive = new Color(0.33f, 0.36f, 0.26f);
            b.Tent(P(-8, 6), yaw, new Vector3(5, 2.6f, 7), olive); b.Tent(P(0, 8), yaw, new Vector3(5, 2.6f, 7), olive); b.Tent(P(8, 6), yaw, new Vector3(5, 2.6f, 7), olive);
            b.ShippingContainer(P(-10, -8), yaw + 90, new Color(0.3f, 0.35f, 0.25f), true); b.ShippingContainer(P(10, -6), yaw, new Color(0.35f, 0.3f, 0.22f));
            b.Watchtower(P(14, 14), 5f);
            b.JerseyBarrier(P(-1.5f, -19), yaw); b.JerseyBarrier(P(1.5f, -21), yaw + 20);
            AddCrate(P(-8, 6), 0, LootCrate.Kind.WeaponCase); AddCrate(P(0, 8), 0, LootCrate.Kind.ArmorCase); AddCrate(P(8, 6), 0, LootCrate.Kind.Medical);
            AddCrate(P(-10, -8), yaw, LootCrate.Kind.Crate); AddCrate(P(4, -4), 30, LootCrate.Kind.Optics);
            return pts;
        }

        List<Vector3> GasStation(Vector3 p, float yaw)
        {
            var b = B; var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float z) => p + q * new Vector3(x, 0, z);
            b.Box(P(0, -6) + Vector3.up * 0.02f, new Vector3(30, 0.03f, 20), Maps.Asphalt, Color.white, q, false);
            b.Box(P(-4, -8) + Vector3.up * 5, new Vector3(14, 0.4f, 9), Maps.Metal, new Color(0.85f, 0.85f, 0.85f), q, true, Synth.Surface.Metal);
            foreach (var o in new[] { new Vector3(-10, 0, -11.5f), new Vector3(2, 0, -11.5f), new Vector3(-10, 0, -4.5f), new Vector3(2, 0, -4.5f) }) b.Box(p + q * o + Vector3.up * 2.5f, new Vector3(0.4f, 5, 0.4f), Maps.Metal, new Color(0.8f, 0.2f, 0.15f), q, true, Synth.Surface.Metal);
            for (int i = 0; i < 2; i++) b.Box(P(-7 + i * 6, -8) + Vector3.up * 0.8f, new Vector3(0.8f, 1.6f, 0.5f), Maps.Metal, new Color(0.85f, 0.2f, 0.15f), q, true, Synth.Surface.Metal);
            var pts = b.Building(P(6, 6), yaw, new Vector2(12, 8), 1, Maps.Plaster, new Color(0.9f, 0.88f, 0.8f), Maps.Tiles, Maps.ConcreteDark, 0, true);
            b.CarWreck(P(-4, -14), yaw + 80, new Color(0.5f, 0.12f, 0.1f));
            AddCrate(P(9, 6), 0, LootCrate.Kind.Medical);
            return pts;
        }

        List<Vector3> Warehouse(Vector3 p, float yaw)
        {
            var b = B; var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float z) => p + q * new Vector3(x, 0, z);
            var pts = b.Building(P(0, 4), yaw, new Vector2(24, 16), 1, Maps.Corrugated, new Color(0.55f, 0.6f, 0.62f), Maps.ConcreteDark, Maps.Metal, 0, false, false, 6f);
            for (int i = 0; i < 3; i++) b.Shelf(P(-8 + i * 8, 8), yaw, 4f);
            b.ShippingContainer(P(-16, -10), yaw + 90, new Color(0.5f, 0.15f, 0.12f)); b.ShippingContainer(P(16, -8), yaw, new Color(0.15f, 0.3f, 0.5f), true);
            b.ShippingContainer(P(16, -8) + Vector3.up * 2.6f, yaw + 3, new Color(0.2f, 0.4f, 0.25f));
            AddCrate(P(-6, 0), 0, LootCrate.Kind.WeaponCase); AddCrate(P(16, -8), yaw, LootCrate.Kind.Crate);
            return pts;
        }

        List<Vector3> Cabins(Vector3 p, float yaw, bool wooden)
        {
            var b = B; var pts = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                var o = p + Quaternion.Euler(0, yaw + i * 90 + b.Rng.Range(-20f, 20f), 0) * new Vector3(0, 0, 12);
                pts.AddRange(b.Building(o, yaw + i * 90 + 180, new Vector2(7, 6), 1, Maps.WoodPlank, new Color(0.6f, 0.47f, 0.35f) * b.Rng.Range(0.85f, 1.1f), Maps.WoodPlank, Maps.WoodPlank, 0, true));
            }
            b.Cyl(p, p + Vector3.up * 0.4f, 1.2f, 1.2f, 14, Maps.Concrete, new Color(0.5f, 0.5f, 0.5f), true); // fire pit
            AddCrate(p + new Vector3(2, 0, 2), 0, LootCrate.Kind.Duffel);
            return pts;
        }

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
        public Vector3 Size;
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
            c.Size = size;
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
        public float OpensAt;   // seconds into the raid when this extract unlocks (5-10 min, the same for everyone)
        float _t; float _beep;
        bool _announced;
        Light _light;
        public bool IsOpen => RaidManager.I == null || RaidManager.I.Elapsed >= OpensAt;
        public float OpensIn => RaidManager.I == null ? 0f : Mathf.Max(0f, OpensAt - RaidManager.I.Elapsed);
        static string Clock(float s) { int t = Mathf.CeilToInt(s); return (t / 60) + ":" + (t % 60).ToString("00"); }
        public const float Time_ = 10f;
        ParticleSystem _smoke;
        bool _calling;
        TextMesh _screen, _screenSub;
        Transform _laptop;

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
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.4f, 1f, 0.5f); l.range = 8f; l.intensity = 1.5f; e._light = l;
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
            e.BuildLaptop();
            return e;
        }

        /// <summary>Rugged laptop on a crate at the edge of the zone: use it to call the exfil (10 s countdown).</summary>
        void BuildLaptop()
        {
            _laptop = new GameObject("ExfilLaptop").transform;
            _laptop.SetParent(transform, false);
            _laptop.localPosition = new Vector3(0, 0, 1.1f);
            Color olive = UI.Lin(new Color(0.3f, 0.33f, 0.24f)), dark = UI.Lin(new Color(0.09f, 0.09f, 0.1f)), keys = UI.Lin(new Color(0.16f, 0.16f, 0.17f)), bump = UI.Lin(new Color(0.55f, 0.45f, 0.12f));
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.22f, 0), new Vector3(0.62f, 0.44f, 0.46f), olive, 0.02f, 1);                 // supply crate
            foreach (var x in new[] { -0.32f, 0.32f }) mb.Sub(0).Box(new Vector3(x, 0.3f, 0), new Vector3(0.025f, 0.05f, 0.16f), olive * 0.7f, 0.006f); // handles
            mb.Sub(1).Box(new Vector3(0, 0.455f, 0), new Vector3(0.38f, 0.03f, 0.27f), dark, 0.008f, 1);                // laptop base
            mb.Sub(1).Box(new Vector3(0, 0.471f, -0.03f), new Vector3(0.32f, 0.004f, 0.14f), keys, 0.002f);             // keyboard
            foreach (var x in new[] { -0.19f, 0.19f }) foreach (var z in new[] { -0.135f, 0.135f })
                    mb.Sub(1).Box(new Vector3(x, 0.455f, z), new Vector3(0.03f, 0.036f, 0.03f), bump, 0.01f);           // rubber corner bumpers
            mb.Build("Crate", _laptop, Mats.Lit("exfil_crate", Color.white, 0.2f, 0.3f, Mats.Metal, null, 0.5f, 0.4f), Mats.Lit("exfil_laptop", Color.white, 0.3f, 0.45f, Mats.Noise, null, 0.3f, 0.3f));
            var lid = new GameObject("Lid").transform; lid.SetParent(_laptop, false);
            lid.localPosition = new Vector3(0, 0.47f, 0.13f); lid.localRotation = Quaternion.Euler(-12, 0, 0);
            var lm = new MB();
            lm.Box(new Vector3(0, 0.125f, 0.008f), new Vector3(0.38f, 0.25f, 0.016f), dark, 0.008f, 1);
            lm.Build("LidShell", lid, Mats.Lit("exfil_laptop", Color.white, 0.3f, 0.45f, Mats.Noise, null, 0.3f, 0.3f));
            var scr = UI.Panel(lid, "Screen", new Vector3(0, 0.125f, -0.004f), Quaternion.identity, new Vector2(0.31f, 0.19f), null, false, Skin.Crt);
            UI.Text(scr, "EXFIL UPLINK", new Vector3(-0.14f, 0.075f, 0), 0.016f, UI.CrtDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            _screen = UI.Text(scr, "READY", new Vector3(0, 0.01f, 0), 0.05f, UI.CrtGreen, TextAnchor.MiddleCenter, FontStyle.Bold);
            _screenSub = UI.Text(scr, "PRESS TO CALL EXFIL", new Vector3(0, -0.055f, 0), 0.016f, UI.CrtDim, TextAnchor.MiddleCenter);
            UI.Button(scr, "CALL EXFIL", new Vector3(0.0f, -0.055f, -0.004f), new Vector2(0.2f, 0.04f), Call, null, BtnStyle.Crt).Label.text = "";
            var deck = new GameObject("Deck").transform; deck.SetParent(_laptop, false);
            deck.localPosition = new Vector3(0, 0.474f, -0.04f); deck.localRotation = Quaternion.Euler(90, 0, 0);
            UI.Button(deck, "CALL", new Vector3(0, 0, 0), new Vector2(0.12f, 0.06f), Call, new Color(0.2f, 0.42f, 0.2f), BtnStyle.Key);
            var col = _laptop.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.3f, 0); col.size = new Vector3(0.62f, 0.62f, 0.46f);
            _laptop.gameObject.AddComponent<ExfilLaptop>().zone = this;
        }

        bool PlayerInZone(PlayerRig p)
        {
            Vector3 d = p.transform.position - transform.position; d.y = 0;
            return d.magnitude < Radius + 0.4f;
        }

        void SetScreen(string big, string small)
        {
            if (_screen != null && _screen.text != big) _screen.text = big;
            if (_screenSub != null && _screenSub.text != small) _screenSub.text = small;
        }

        public void Call()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            var raid = RaidManager.I;
            if (p == null || raid == null || raid.Done || !p.Vitals.Alive) return;
            if (_calling) return;
            if (!IsOpen) { p.Toast(Name + " is locked - it opens in " + Clock(OpensIn)); Sfx.UI(Synth.UiError(), 0.3f); return; }
            if (!PlayerInZone(p)) { p.Toast("Get inside the green zone first"); Sfx.UI(Synth.UiError(), 0.3f); return; }
            _calling = true; _t = 0; _beep = 0;
            Sfx.Play(Synth.QueueFound(), _laptop.position, 0.5f, 1f, 0.6f, 15f);
            p.Toast("Exfil called - stay in the zone for " + (int)Time_ + " seconds");
        }

        void Update()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            var raid = RaidManager.I;
            if (p == null || raid == null || !p.Vitals.Alive || raid.Done) return;
            bool inside = PlayerInZone(p);
            // locked until its timer runs out: red light, no smoke, the laptop counts down
            bool open = IsOpen;
            if (_smoke != null) { var em = _smoke.emission; em.enabled = open; }
            if (_light != null) _light.color = open ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.25f, 0.15f);
            if (!open)
            {
                SetScreen("LOCKED", "OPENS IN " + Clock(OpensIn));
                if (inside) p.Hud.Center(Name + " opens in " + Clock(OpensIn), 0.2f);
                return;
            }
            if (!_announced) { _announced = true; if (RaidManager.I.Elapsed > 2f) p.Toast("Extract open: " + Name); }
            // desktop: look at the laptop and press F
            if (!_calling && !XRInputs.VR && Desktop.Interact && Physics.Raycast(p.Head.position, p.Head.forward, out var hit, 3f, ~0, QueryTriggerInteraction.Collide) && hit.collider.transform.IsChildOf(_laptop)) Call();
            if (_calling)
            {
                if (!inside)
                {
                    _calling = false; _t = 0;
                    Sfx.UI(Synth.UiError(), 0.4f);
                    p.Hud.Center("EXFIL ABORTED\n<size=40>you left the zone</size>", 2f);
                    SetScreen("SIGNAL LOST", "PRESS TO CALL AGAIN");
                    return;
                }
                _t += Time.deltaTime;
                _beep -= Time.deltaTime;
                if (_beep <= 0) { _beep = 1f; Sfx.Play(Synth.Beep(), _laptop.position, 0.4f, 1.2f, 0.5f, 12f); }
                int left = Mathf.CeilToInt(Time_ - _t);
                SetScreen("00:" + left.ToString("00"), "EXFIL INBOUND - HOLD POSITION");
                p.Hud.Center("EXTRACTING  " + left, 0.2f);
                if (_t >= Time_) { _calling = false; raid.Extract(Name); }
            }
            else
            {
                _t = 0;
                SetScreen(inside ? "READY" : "STANDBY", inside ? "PRESS TO CALL EXFIL" : "ENTER THE ZONE");
                if (inside) p.Hud.Center("Use the laptop to call the exfil", 0.2f);
            }
        }
    }

    /// <summary>Grab the exfil laptop (VR) to call the exfil.</summary>
    public class ExfilLaptop : Interactable
    {
        public Extraction zone;
        public override void OnGrab(Hand h) { if (zone != null) zone.Call(); h.Haptic(0.3f, 0.05f); }
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
            Noise.Emit(transform.position, 80f, 0, true);   // the vault alarm really does pull everyone nearby
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
