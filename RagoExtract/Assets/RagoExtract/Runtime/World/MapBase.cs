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
        public float NpcSafeRadius = 110f;   // no raiders this close to a player spawn
        public bool KeepFog;                 // indoor maps keep their own fog (raids otherwise get a light haze and see the whole island)
        public WorldBuilder B;
        public readonly List<Pose> PlayerSpawns = new List<Pose>();
        /// <summary>Maps with several floors (Underground): each floor's floor height, top first, its name and the
        /// room height - the field map gets a page per floor and shows the one you're on.</summary>
        public float[] MapFloors; public string[] MapFloorNames; public float MapFloorHeight = 3f;
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
        public bool Indoor;   // every walkable spot has a roof over it (bunker, Market, Underground): open sky = you got out
        NavMeshDataInstance _nav;
        Light _sun;
        public Light Sun => _sun;

        public struct NpcSpawn { public Vector3 pos; public int count; public bool heavy; public string boss; public float chance; public bool guard; }   // guard: always spawns (vault guards)

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
                    var markups = new List<NavMeshBuildMarkup>();
                    foreach (var t in NavIgnore) if (t != null) markups.Add(new NavMeshBuildMarkup { root = t, ignoreFromBuild = true });   // doors: raiders path through doorways
                    NavMeshBuilder.CollectSources(nb, mask, NavMeshCollectGeometry.PhysicsColliders, 0, markups, src);
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

        public LootCrate AddCrate(Vector3 pos, float yaw, LootCrate.Kind kind) => AddCrate(pos, yaw, (LootCrate.Kind?)kind, 0.6f, false);

        /// <summary>
        /// In a raid this only registers a possible crate spot: the map layout is the same every raid, but which spots
        /// get a crate - and what kind - is picked fresh from the raid's seed (FinalizeLoot). In the hubs it's immediate.
        /// </summary>
        public LootCrate AddCrate(Vector3 pos, float yaw, LootCrate.Kind? kind, float chance, bool desk)
        {
            if (IsRaid && !_lootPlaced) { _spots.Add(new LootSpot { pos = pos, yaw = yaw, kind = kind, chance = chance, desk = desk }); return null; }
            var c = LootCrate.Create(this, Crates.Count, pos, yaw, kind ?? LootCrate.Kind.Crate);
            Crates.Add(c);
            return c;
        }

        struct LootSpot { public Vector3 pos; public float yaw; public LootCrate.Kind? kind; public float chance; public bool desk; }
        readonly List<LootSpot> _spots = new List<LootSpot>();
        bool _lootPlaced;
        public readonly List<Transform> NavIgnore = new List<Transform>();

        /// <summary>Raids: a pushable door in every ordinary doorway (not vault entrances, gates or wide openings).</summary>
        /// <summary>Doorways that get a door, listed by the map itself (instead of every wall's openings).</summary>
        public readonly List<WorldBuilder.DoorGap> CustomDoors = new List<WorldBuilder.DoorGap>();

        public void AddSwingDoors()
        {
            if (!IsRaid || B == null) return;
            var root = new GameObject("Doors").transform; root.SetParent(transform, false);
            NavIgnore.Add(root);
            int k = 0;
            var placed = new List<WorldBuilder.DoorGap>();
            var all = new List<WorldBuilder.DoorGap>(B.Doors);
            all.AddRange(CustomDoors);   // maps that place their own doors (the Underground's rooms)
            foreach (var d in all)
            {
                if (!d.swing) continue;
                // two doorways overlapping (walls meeting at a doorway): only one door
                bool clash = false;
                foreach (var p in placed) if (Mathf.Abs(p.c.y - d.c.y) < 1f && (p.c - d.c).magnitude < p.halfW + d.halfW + 0.3f) { clash = true; break; }
                if (clash) continue;
                placed.Add(d);
                int seed = Mathf.RoundToInt(d.c.x * 13.1f + d.c.z * 7.7f + d.c.y * 3.3f) ^ LootSeed;
                SwingDoor.Create(root, d.c - d.along * (d.halfW - 0.02f), d.along, d.halfW * 2f - 0.04f, d.top - d.c.y - 0.03f, seed + k++);
            }
        }

        public int MaxRaiders = NpcDirector.MaxNpcs;   // raiders alive at the start of a raid on this map
        public int LootSeed;

        int _fp; float _bornAt = -1f;
        void OnEnable() { if (_bornAt < 0f) _bornAt = Time.realtimeSinceStartup; }
        /// <summary>A fingerprint of the world geometry (terrain heights and the number of static pieces), the same on
        /// every machine that built the same map - compared with other players to catch a mismatch.</summary>
        public int Fingerprint()
        {
            if (_fp != 0) return _fp;
            if (Time.timeSinceLevelLoad < 0f) return 0;
            unchecked
            {
                int h = 17;
                if (B != null)
                    foreach (var t in B.Terrains)
                    {
                        if (t == null || t.sharedMesh == null) continue;
                        var v = t.sharedMesh.vertices;
                        h = h * 31 + v.Length;
                        for (int i = 0; i < v.Length; i += 97) h = h * 31 + Mathf.RoundToInt(v[i].y * 10f);
                    }
                int statics = 0;
                foreach (var c in GetComponentsInChildren<Collider>(true)) if (c.attachedRigidbody == null && c.gameObject.isStatic) statics++;
                h = h * 31 + statics;
                int fp = h == 0 ? 1 : h;
                // only keep it once the world is fully built (a Hello sent mid-load used to cache a half-built count and
                // made everyone see a false "map is different" warning)
                if (B != null && B.Terrains.Count > 0 || Time.realtimeSinceStartup - _bornAt > 10f) _fp = fp;
                return fp;
            }
            return _fp;
        }

        /// <summary>After the map is built: pick this raid's crates from all the possible spots.</summary>
        public void FinalizeLoot()
        {
            if (_lootPlaced) return;
            _lootPlaced = true;
            var rng = new Rng(LootSeed * 92821 + 17);
            Physics.SyncTransforms();
            // the map's own keycard spots (4, every map) and military radio spots (3, not the Underground): the same places
            // every raid so you can learn them, picked once from this map's desk / floor spots and kept free of crates
            var fixedRng = new Rng(Seed * 7027 + 911);
            var cand = _spots.FindAll(s => s.desk && !s.kind.HasValue);
            if (cand.Count < 7) cand = _spots.FindAll(s => !s.kind.HasValue);
            cand.Sort((a, b) => a.pos.x != b.pos.x ? a.pos.x.CompareTo(b.pos.x) : a.pos.z.CompareTo(b.pos.z));
            var cardSpots = new List<Vector3>(); var radioSpots = new List<Vector3>();
            bool radios = Id != "underground";
            for (int i = 0; i < 7 && cand.Count > 0; i++)
            {
                if (i >= 4 && !radios) break;
                int k = fixedRng.Range(0, cand.Count);
                var sp = cand[k]; cand.RemoveAt(k);
                // keep them spread out: skip one too close to a spot already taken
                bool close = false;
                foreach (var q in cardSpots) if ((q - sp.pos).sqrMagnitude < 15f * 15f) close = true;
                foreach (var q in radioSpots) if ((q - sp.pos).sqrMagnitude < 15f * 15f) close = true;
                if (close && cand.Count > 7 - i) { i--; continue; }
                _spots.RemoveAll(s => s.pos == sp.pos);
                if (i < 4) cardSpots.Add(sp.pos); else radioSpots.Add(sp.pos);
            }
            var free = new List<LootSpot>();
            foreach (var s in _spots)
            {
                // never under a table or a shelf (the overlap check only saw the table top / shelf boards, so a crate
                // could sit underneath one - it looked like it was in the floor)
                if (!s.desk && B != null && B.InFurniture(s.pos, 0.45f, 0.28f)) continue;
                if (!rng.Chance(s.chance)) { free.Add(s); continue; }
                LootCrate.Kind k;
                if (s.kind.HasValue) k = s.kind.Value;
                else if (s.desk)
                {
                    // on a desk: the small cases
                    float r = rng.Value;
                    k = r < 0.35f ? LootCrate.Kind.Medical : r < 0.65f ? LootCrate.Kind.Optics : r < 0.85f ? LootCrate.Kind.WeaponCase : LootCrate.Kind.Duffel;
                }
                else
                {
                    // on the floor against a wall: crates, armour cases, the odd safe
                    float r = rng.Value;
                    k = r < 0.55f ? LootCrate.Kind.Crate : r < 0.72f ? LootCrate.Kind.ArmorCase : r < 0.86f ? LootCrate.Kind.Duffel : r < 0.95f ? LootCrate.Kind.WeaponCase : LootCrate.Kind.Safe;
                }
                // only where it really fits: the box at its true size and angle must not touch a table, shelf, bed,
                // wall or another box (furniture used to be checked with a rough circle, and only tables and shelves).
                // Too big here: a smaller case instead, or nothing.
                float yaw = FaceRoom(s.pos, s.yaw, s.desk);
                if (CrateFits(s.pos, yaw, k)) AddCrate(s.pos, yaw, k);
                else if (!s.desk && CrateFits(s.pos, yaw, LootCrate.Kind.Duffel)) AddCrate(s.pos, yaw, LootCrate.Kind.Duffel);
                else if (CrateFits(s.pos, yaw, LootCrate.Kind.Optics)) AddCrate(s.pos, yaw, LootCrate.Kind.Optics);
                else free.Add(s);
            }
            // this raid: each card spot has a card half the time (at least one somewhere), each radio spot likewise
            var at = new List<Vector3>(); var ids = new List<string>();
            void Fill(List<Vector3> spots, string id)
            {
                int before = at.Count;
                foreach (var p in spots) if (rng.Chance(0.5f)) { at.Add(p); ids.Add(id); }
                if (at.Count == before && spots.Count > 0) { at.Add(spots[rng.Range(0, spots.Count)]); ids.Add(id); }
            }
            Fill(cardSpots, "key_vault");
            Fill(radioSpots, "val_radio");
            if (at.Count > 0) { var ks = gameObject.AddComponent<KeycardSpawner>(); ks.At = at; ks.Ids = ids; }
            // melee weapons lying around: bats, crowbars, pans, the odd machete, axe or sledgehammer, on spare spots
            {
                var mrng = new Rng(LootSeed * 5113 + 29);
                var mAt = new List<Vector3>(); var mIds = new List<string>();
                var pool = free.FindAll(f => { foreach (var c in Crates) if (c != null && (c.transform.position - f.pos).sqrMagnitude < 1.5f) return false; foreach (var q in at) if ((q - f.pos).sqrMagnitude < 1.5f) return false; return true; });
                int want = Id == "underground" ? 4 : 6;
                for (int guard = 0; guard < 200 && mAt.Count < want && pool.Count > 0; guard++)
                {
                    int k = mrng.Range(0, pool.Count); var sp = pool[k]; pool.RemoveAt(k);
                    bool close = false; foreach (var q in mAt) if ((q - sp.pos).sqrMagnitude < 12f * 12f) { close = true; break; }
                    if (close) continue;
                    float r = mrng.Value;
                    mAt.Add(sp.pos); mIds.Add(r < 0.25f ? "bat" : r < 0.48f ? "crowbar" : r < 0.66f ? "pan" : r < 0.82f ? "machete" : r < 0.93f ? "fireaxe" : "sledge");
                }
                if (mAt.Count > 0) { var ms = gameObject.AddComponent<KeycardSpawner>(); ms.At = mAt; ms.Ids = mIds; ms.BaseId = 960000; }
            }
            _spots.Clear();
        }

        /// <summary>Christmas: wrapped presents on spare loot spots round the map, spread out (fewer underground).</summary>
        void PlacePresents(List<LootSpot> free)
        {
            var prng = new Rng(LootSeed * 3119 + 47);
            var pool = new List<LootSpot>(free);
            var taken = new List<Vector3>();
            int want = Id == "northpole" ? 32 : Id == "underground" ? 12 : 20;   // presents are everywhere (the North Pole most of all)
            for (int guard = 0; guard < 400 && taken.Count < want && pool.Count > 0; guard++)
            {
                int k = prng.Range(0, pool.Count);
                var s = pool[k]; pool.RemoveAt(k);
                bool close = false;
                foreach (var q in taken) if ((q - s.pos).sqrMagnitude < 9f * 9f) { close = true; break; }
                if (close) continue;
                float yaw = FaceRoom(s.pos, s.yaw, s.desk);
                if (!CrateFits(s.pos, yaw, LootCrate.Kind.Present)) continue;
                AddCrate(s.pos, yaw, LootCrate.Kind.Present);
                taken.Add(s.pos);
            }
        }

        /// <summary>
        /// Which way a crate at p should face: its lid is hinged at the back, so the back goes against the nearest wall
        /// (squared up to it) and the opening faces the room. A desk out in the open: the opening faces the chair side.
        /// </summary>
        float FaceRoom(Vector3 p, float yaw, bool desk)
        {
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            float best = 1.3f; Vector3 n = Vector3.zero;
            Vector3 o = p + Vector3.up * 0.3f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                if (!Physics.Raycast(o, dir, out var h, best, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(h.normal.y) > 0.3f || h.collider.GetComponentInParent<LootCrate>() != null) continue;
                best = h.distance; n = h.normal;
            }
            if (n != Vector3.zero) { n.y = 0; return Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg; }   // local +z (the opening side) along the wall's normal
            return desk ? yaw + 180f : yaw;
        }

        public Extraction AddExtract(Vector3 pos, string name)
        {
            var e = Extraction.Create(transform, pos, name);
            Extracts.Add(e);
            return e;
        }

        /// <param name="attached">the vault is built onto the back of a building whose wall (with the doorway cut in it) is the vault front</param>
        /// <param name="wall">inside a building: build the vault walls from the building's own wall material, up to its ceiling</param>
        public Vault AddVault(Vector3 pos, float yaw, bool attached = false, Material wall = null, Color? tint = null, float height = 2.9f, bool ceiling = true,
                              float width = 6f, float thick = 0.4f, bool sides = true, bool back = true)
        {
            var v = Vault.Create(this, Vaults.Count, pos, yaw, attached, wall, tint, height, ceiling, width, thick, sides, back);
            Vaults.Add(v);
            // three heavy guards in front of the vault door, every raid
            var q = Quaternion.Euler(0, yaw, 0);
            NpcSpawns.Add(new NpcSpawn { pos = pos + q * new Vector3(0, 0.05f, -2.6f), count = 3, heavy = true, guard = true, chance = 1f });
            // and three crates of vault-grade loot inside (behind the locked door)
            if (IsRaid)
                foreach (var lp in new[] { new Vector3(-1.1f, 0.13f, 1.3f), new Vector3(1.1f, 0.13f, 1.3f), new Vector3(0f, 0.13f, 3.7f) })
                {
                    var c = LootCrate.Create(this, Crates.Count, pos + q * lp, yaw + 180f, lp.z > 3f ? LootCrate.Kind.Safe : LootCrate.Kind.Crate);
                    c.vaultGrade = true;
                    Crates.Add(c);
                }
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
                try {   // each crate on its own: one going wrong must never leave all the others where they were
                float spotY = c.transform.position.y;
                // under the terrain (a slope higher than the spot): start from the terrain's surface, or the probe below
                // starts inside the hill, sees nothing and leaves the crate buried
                float tt = B != null ? B.TerrainTop(c.transform.position) : float.NegativeInfinity;
                if (tt > c.transform.position.y + 0.01f && tt < c.transform.position.y + 12f) { var q0 = c.transform.position; q0.y = tt; c.transform.position = q0; Physics.SyncTransforms(); }
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
                        if (hits[i].normal.y < 0.85f || hits[i].point.y > p.y + 1.4f) continue;   // flat surfaces only (not walls, tent roofs or slopes), not a shelf or ceiling far above
                        if (hits[i].point.y > top) top = hits[i].point.y;
                    }
                    if (top > best) best = top;
                }
                if (best > float.NegativeInfinity) p.y = best + 0.004f;
                // never below the spot it was made for (every spot is written at its floor's top surface), and a touch
                // above whatever it's on - a crate a few mm high looks right, one sunk into the floor never does
                p.y = Mathf.Max(p.y, spotY) + 0.015f;
                c.transform.position = p;
                } catch (System.Exception e) { Debug.LogWarning("[Rago] settling crate " + c.name + ": " + e.Message); var q = c.transform.position; q.y += 0.12f; c.transform.position = q; }
            }
            Physics.SyncTransforms();
            // no crate inside a wall, furniture, stairs or another crate: move it to the nearest clear spot, or drop it
            var cols = new Collider[16];
            for (int ci = Crates.Count - 1; ci >= 0; ci--)
            {
                var c = Crates[ci];
                if (c == null) continue;
                if (!Blocked(c, c.transform.position, cols, mask)) continue;
                Vector3 start = c.transform.position;
                bool placed = false;
                for (float r = 0.4f; r <= 1.6f && !placed; r += 0.4f)
                    for (int a = 0; a < 8 && !placed; a++)
                    {
                        float ang = a * 45f * Mathf.Deg2Rad;
                        Vector3 cand = start + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * r;
                        if (!Physics.Raycast(cand + Vector3.up * 1.5f, Vector3.down, out var gh, 3f, mask, QueryTriggerInteraction.Ignore) || gh.normal.y < 0.7f) continue;
                        if (Mathf.Abs(gh.point.y - start.y) > 0.8f) continue;   // stay on the same floor
                        cand.y = gh.point.y + 0.004f;
                        if (Blocked(c, cand, cols, mask)) continue;
                        c.transform.position = cand;
                        Physics.SyncTransforms();
                        placed = true;
                    }
                if (!placed) { Crates.RemoveAt(ci); Destroy(c.gameObject); Physics.SyncTransforms(); }
            }
            // last check: no crate may end up with a floor just above it (sunk under a house's floor boards): if one
            // is, put it on top of that floor
            foreach (var c in Crates)
            {
                if (c == null) continue;
                Vector3 sz = c.Size.sqrMagnitude > 0 ? c.Size : new Vector3(0.6f, 0.4f, 0.5f);
                Vector3 mid = c.transform.position + Vector3.up * (sz.y * 0.5f);
                if (!Physics.Raycast(mid, Vector3.up, out var up, sz.y * 0.5f + 0.5f, mask, QueryTriggerInteraction.Ignore) || up.collider.transform.IsChildOf(c.transform)) continue;
                if (Physics.Raycast(up.point + Vector3.up * 0.8f, Vector3.down, out var top, 1.0f, mask, QueryTriggerInteraction.Ignore) && top.normal.y > 0.7f && top.point.y - c.transform.position.y < 0.7f)
                { c.transform.position = new Vector3(c.transform.position.x, top.point.y + 0.004f, c.transform.position.z); Physics.SyncTransforms(); }
            }
        }

        /// <summary>Would crate c overlap anything solid (except the floor it stands on) at position p?</summary>
        bool Blocked(LootCrate c, Vector3 p, Collider[] cols, int mask)
        {
            Vector3 size = c.Size.sqrMagnitude > 0 ? c.Size : new Vector3(0.6f, 0.4f, 0.5f);
            if (B != null && B.InDoorway(p, c.transform.rotation, size.x * 0.5f, size.z * 0.5f, 0.7f)) return true;   // keep doorways walkable
            if (B != null && c.transform.position.y - p.y < 0.5f && B.InFurniture(p + Vector3.up * 0.05f, size.x * 0.5f, size.z * 0.5f)) return true;   // not under a table / shelf
            Vector3 he = new Vector3(size.x * 0.47f, size.y * 0.45f, size.z * 0.47f);
            Vector3 center = p + Vector3.up * (size.y * 0.5f + 0.03f);
            int n = Physics.OverlapBoxNonAlloc(center, he, cols, c.transform.rotation, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!cols[i].transform.IsChildOf(c.transform)) return true;
            return false;
        }

        public void Spawn(Vector3 p, float yaw) => PlayerSpawns.Add(new Pose(p, Quaternion.Euler(0, yaw, 0)));
        public void Squad(Vector3 p, int n, bool heavy = false) => NpcSpawns.Add(new NpcSpawn { pos = p, count = n, heavy = heavy });
        /// <summary>A named boss with heavy guards in their lair - only turns up in some raids (chance per raid).</summary>
        public void Boss(Vector3 p, string name, float chance = 0.25f) => NpcSpawns.Add(new NpcSpawn { pos = p, count = 3, heavy = true, boss = name, chance = chance });

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
                    pts.AddRange(b.Building(hp, hy, new Vector2(w, d), floors, wall, tint, Maps.WoodPlank, wooden ? Maps.WoodPlank : Maps.ConcreteDark, 0, true, desk: false));   // houses have their own furniture
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
                        if (b.Table(H(-hx * 0.15f, y, hz - 0.75f), hy, new Vector2(1.4f, 0.8f), 0.78f))
                        { var dp = H(-hx * 0.15f, y + 0.84f, hz - 0.75f); pts.Add(dp); b.DeskSpots[dp] = true; b.SpotYaw[dp] = hy; }   // loot can sit on the table
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
            // it's an island: flat land out to `land`, then an irregular coastline, beaches and open sea (see Coast)
            float land = half - 70f, slab = land - 40f;   // flat ground slabs out to `slab`; the shore mesh carries the grass on to the coast
            // ground around the core
            b.Ground(new Vector3((-slab + core.xMin) / 2, 0, 0), new Vector2(core.xMin + slab, slab * 2), ground, Synth.Surface.Grass, y);
            if (seaEast) b.Water(new Vector3((core.xMax + half) / 2 + 30, -1.6f, 0), new Vector2(half - core.xMax + 60, land * 2));
            else b.Ground(new Vector3((core.xMax + slab) / 2, 0, 0), new Vector2(slab - core.xMax, slab * 2), ground, Synth.Surface.Grass, y);
            b.Ground(new Vector3(core.center.x, 0, (-slab + core.yMin) / 2), new Vector2(core.width, core.yMin + slab), ground, Synth.Surface.Grass, y);
            b.Ground(new Vector3(core.center.x, 0, (core.yMax + slab) / 2), new Vector2(core.width, slab - core.yMax), ground, Synth.Surface.Grass, y);
            Coast(core, half, land, seaEast, ground);
            var roadMat = wooden ? Maps.GroundDirt : Maps.Asphalt;
            // pick locations on a ring around the core
            var spots = new List<Vector3>();
            int tries = 0;
            while (spots.Count < pois && tries++ < 900)
            {
                float ang = b.Rng.Range(0f, Mathf.PI * 2f);   // spacing below keeps them spread around the core
                float r = b.Rng.Range(0.3f, 0.86f) * half;
                var p = new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
                if (Mathf.Abs(p.x) > land - 35 || Mathf.Abs(p.z) > land - 35 || !OnLand(p, 45f)) continue;
                if (p.x > core.xMin - 45 && p.x < core.xMax + 45 && p.z > core.yMin - 45 && p.z < core.yMax + 45) continue;
                if (seaEast && p.x > core.xMax - 20) continue;
                bool clash = false; foreach (var o in spots) if ((o - p).sqrMagnitude < (tries > 250 ? 90 * 90 : 130 * 130)) clash = true;
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
                switch (kind++ % 8)
                {
                    case 6: case 7: pts = Hamlet(p, yaw, wooden); break;   // villages: lots more houses
                    case 0: pts = Farm(p, yaw); break;
                    case 1: pts = ArmyCamp(p, yaw); break;
                    case 2: pts = GasStation(p, yaw); break;
                    case 3: pts = Warehouse(p, yaw); break;
                    case 4: pts = Cabins(p, yaw, wooden); break;
                    default: pts = Neighborhood(p + Quaternion.Euler(0, yaw + 90, 0) * new Vector3(-27, 0, 0), yaw + 90, 4, 13.5f, wooden); break;
                }
                ScatterLoot(pts, 0.85f);
                Squad(p + new Vector3(b.Rng.Range(-8f, 8f), 0, b.Rng.Range(-8f, 8f)), b.Rng.Range(2, 4), kind % 8 == 2);
                ExtraNav.Add(new Bounds(p + Vector3.up * 5, new Vector3(80, 30, 80)));
            }
            // only 3 extracts per map: the first one in town, plus two far out on opposite sides
            for (int i = Extracts.Count - 1; i >= 1; i--) { if (Extracts[i] != null) Compat.Kill(Extracts[i].gameObject); Extracts.RemoveAt(i); }
            var far = new List<Vector3>(spots);
            far.Sort((a1, a2) => a2.sqrMagnitude.CompareTo(a1.sqrMagnitude));
            var picks = new List<Vector3>();
            if (far.Count > 0) picks.Add(far[0]);
            if (far.Count > 1) { var best = far[1]; foreach (var f in far) if ((f - far[0]).sqrMagnitude > (best - far[0]).sqrMagnitude) best = f; picks.Add(best); }
            if (far.Count > 2)
            {
                // a third one out to the side of the other two
                Vector3 best = far[2]; float bs = -1f;
                foreach (var f in far) { float sc = Mathf.Min((f - picks[0]).sqrMagnitude, (f - picks[1]).sqrMagnitude); if (sc > bs && f.sqrMagnitude > half * half * 0.25f) { bs = sc; best = f; } }
                picks.Add(best);
            }
            for (int i = 0; i < picks.Count && i < extractNames.Length; i++)
            {
                var dir = new Vector3(picks[i].x, 0, picks[i].z).normalized;
                var e = picks[i] + dir * 32f;
                e.x = Mathf.Clamp(e.x, -land + 12, land - 12); e.z = Mathf.Clamp(e.z, -land + 12, land - 12);
                for (int k = 0; k < 40 && !OnLand(e, 12f); k++) e -= dir * 4f;   // walk it back from the shore onto land
                Road(picks[i], e, roadMat); roads.Add((picks[i], e));
                AddExtract(e, extractNames[i]);
            }
            // player spawn sites are picked now (built at the end), so the hills can leave flat ground for them
            var sitePos = new List<Vector3>();
            for (int i = 0; i < 160 && sitePos.Count < 20; i++)
            {
                float ang = (sitePos.Count + i * 0.37f) / 20f * Mathf.PI * 2f;
                var hp = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (land * b.Rng.Range(0.84f, 0.93f));
                hp.x = Mathf.Clamp(hp.x, -land + 20, land - 20); hp.z = Mathf.Clamp(hp.z, -land + 20, land - 20);
                if (seaEast && hp.x > core.xMax - 25) continue;
                if (Near(hp, spots, 75f) || NearRoad(hp, roads, 10f) || !OnLand(hp, 25f)) continue;
                bool crowded = false; foreach (var sp in sitePos) if ((sp - hp).sqrMagnitude < 110 * 110) crowded = true;
                if (crowded) continue;
                sitePos.Add(hp);
            }
            BuildHills(core, slab, spots, roads, sitePos, seaEast, ground);
            // forest + rocks everywhere else
            float area = Mathf.Clamp((half * half) / (500f * 500f), 1f, 4f);   // the bigger the map, the more of everything
            CliffsAndCover(core, land, spots, roads, seaEast, wooden);
            int trees = Mathf.RoundToInt((wooden ? 700 : 420) * area * 0.85f);
            for (int i = 0; i < trees; i++)
            {
                var t = new Vector3(b.Rng.Range(-land + 3, land - 3), 0, b.Rng.Range(-land + 3, land - 3));
                if (t.x > core.xMin - 6 && t.x < core.xMax + 6 && t.z > core.yMin - 6 && t.z < core.yMax + 6) continue;
                if (seaEast && t.x > core.xMax - 4) continue;
                if (Near(t, spots, 42f) || NearRoad(t, roads, 6f) || !OnLand(t)) continue;
                // mixed woods: mostly pines in pine country, mostly leafy trees (some birches) elsewhere
                t.y = HillY(t);
                float pineShare = wooden ? 0.65f : 0.3f;
                if (b.Rng.Value < pineShare) b.Pine(t, b.Rng.Range(8f, 15f));
                else b.LeafyTree(t, b.Rng.Range(7f, 13f), b.Rng.Value < 0.25f);
                // undergrowth at the foot of some trees
                if (b.Rng.Value < 0.35f) { var o = b.Rng.InCircle() * 3f; b.Fern(t + new Vector3(o.x, y, o.y), b.Rng.Range(0.6f, 1.0f)); }
            }
            // bushes you can walk through (and hide in)
            for (int i = 0; i < Mathf.RoundToInt(520 * area * 0.85f); i++)
            {
                var t = new Vector3(b.Rng.Range(-land + 3, land - 3), y, b.Rng.Range(-land + 3, land - 3));
                if (t.x > core.xMin - 4 && t.x < core.xMax + 4 && t.z > core.yMin - 4 && t.z < core.yMax + 4) continue;
                if (seaEast && t.x > core.xMax - 4) continue;
                if (Near(t, spots, 30f) || NearRoad(t, roads, 4f) || !OnLand(t)) continue;
                t.y = y + HillY(t);
                b.Bush(t, b.Rng.Range(1.1f, 2.2f));
            }
            // patches of tall grass and ferns on the ground
            for (int i = 0; i < Mathf.RoundToInt(260 * area * 0.7f); i++)
            {
                var c = new Vector3(b.Rng.Range(-land + 3, land - 3), y, b.Rng.Range(-land + 3, land - 3));
                if (c.x > core.xMin - 4 && c.x < core.xMax + 4 && c.z > core.yMin - 4 && c.z < core.yMax + 4) continue;
                if (seaEast && c.x > core.xMax - 4) continue;
                if (Near(c, spots, 25f) || NearRoad(c, roads, 3f) || !OnLand(c, 10f)) continue;
                int n = b.Rng.Range(8, 16);
                for (int k = 0; k < n; k++)
                {
                    var o = b.Rng.InCircle() * 5f;
                    var p = c + new Vector3(o.x, 0, o.y);
                    p.y = y + HillY(p);
                    if (b.Rng.Value < 0.82f) b.GrassTuft(p, b.Rng.Range(0.45f, 0.85f)); else b.Fern(p, b.Rng.Range(0.5f, 0.9f));
                }
            }
            for (int i = 0; i < Mathf.RoundToInt(90 * area); i++)
            {
                var t = new Vector3(b.Rng.Range(-land + 3, land - 3), 0, b.Rng.Range(-land + 3, land - 3));
                if (t.x > core.xMin - 6 && t.x < core.xMax + 6 && t.z > core.yMin - 6 && t.z < core.yMax + 6) continue;
                if ((seaEast && t.x > core.xMax - 4) || Near(t, spots, 42f) || NearRoad(t, roads, 6f) || !OnLand(t)) continue;
                t.y = HillY(t);
                b.Rock(t, b.Rng.Range(1f, 3.5f));
            }
            // the edge of the world is out in the shallows: invisible walls (no fence - it's an island)
            var corners = new[] { new Vector3(-half, 0, -half), new Vector3(half, 0, -half), new Vector3(half, 0, half), new Vector3(-half, 0, half) };
            for (int i = 0; i < 4; i++)
            {
                var a1 = corners[i]; var a2 = corners[(i + 1) % 4];
                var mid = (a1 + a2) / 2; var d = a2 - a1;
                b.Collider(mid + Vector3.up * 10, new Vector3(1, 20, d.magnitude), Quaternion.LookRotation(d.normalized), Synth.Surface.Concrete);
            }
            // spawn spots out towards the border, well away from the raiders. Every player in a raid gets a different one:
            // about half are small lootable places (a shed, a cabin, a supply tent, an open container) with a few crates
            // to search before you move out, and the rest are out in the open (a wrecked car, a rock pile, a sandbag nest).
            PlayerSpawns.Clear();
            int sites = 0;
            foreach (var hp in sitePos)
            {
                if (sites % 2 == 0) LootSpawn(hp, sites / 2 % 4, wooden); else OpenSpawn(hp, sites / 2 % 3);
                sites++;
            }
        }

        // ================================================================================== the coast
        float _coastA, _coastB;

        /// <summary>Is (p.x, p.z) on dry land, at least `margin` metres in from the shoreline?</summary>
        protected bool OnLand(Vector3 p, float margin = 6f) => Half <= 0 || IslandD(p.x, p.z) < ShoreAt(p.x, p.z, Half - 70f) - margin;

        /// <summary>Rounded-square distance from the middle (a superellipse): the island's shape.</summary>
        static float IslandD(float x, float z) => Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 6f) + Mathf.Pow(Mathf.Abs(z), 6f), 1f / 6f);

        /// <summary>Where the grass ends and the beach starts, in that direction: an irregular coastline.</summary>
        float ShoreAt(float x, float z, float land)
        {
            float ang = Mathf.Atan2(z, x);
            float n = Mathf.PerlinNoise(Mathf.Cos(ang) * 1.7f + _coastA, Mathf.Sin(ang) * 1.7f + _coastB);
            float n2 = Mathf.PerlinNoise(Mathf.Cos(ang) * 5f + _coastB, Mathf.Sin(ang) * 5f + _coastA);
            return land + 10f + n * 20f + n2 * 6f;
        }

        /// <summary>
        /// Beaches and open sea all round: past the flat land, grass runs out to an irregular shoreline, then sand slopes
        /// gently into the sea (dry sand, wet sand at the waterline). The sea runs to the horizon; you can wade into the
        /// shallows up to the invisible edge but the water there is never over your head. Rocks, driftwood and a few old
        /// wooden jetties line the shore.
        /// </summary>
        void Coast(Rect core, float half, float land, bool seaEast, Material grass)
        {
            var b = B;
            _coastA = b.Rng.Range(0f, 100f); _coastB = b.Rng.Range(0f, 100f);
            const float Water = -1.6f;
            float ext = half + 60f;
            bool InCore(float x, float z) => x > core.xMin - 1 && x < core.xMax + 1 && z > core.yMin - 1 && z < core.yMax + 1;
            bool EastSea(float x) => seaEast && x > core.xMax - 1;
            float Sand(float x, float z)
            {
                if (InCore(x, z) || EastSea(x)) return -8f;
                float d = IslandD(x, z), e = ShoreAt(x, z, land);
                if (d < e) return Mathf.Max(-8f, -0.06f - (e - d) * 0.05f);   // tucked under the grass
                float t = Mathf.Clamp01((d - e) / 70f);
                return -0.06f - 2.84f * t * t * (3f - 2f * t);
            }
            float Grass(float x, float z)
            {
                if (InCore(x, z) || EastSea(x)) return -8f;
                if (Mathf.Abs(x) < land - 41f && Mathf.Abs(z) < land - 41f) return -0.4f;   // under the flat ground slabs
                float d = IslandD(x, z), e = ShoreAt(x, z, land);
                return d < e ? -0.06f : -0.06f - (d - e) * 0.3f;   // dives under the sand at the shoreline (and stays clear of the flat ground's edge)
            }
            Color SandCol(float x, float z)
            {
                float h = Sand(x, z);
                Color dry = new Color(0.9f, 0.82f, 0.64f), wet = new Color(0.6f, 0.53f, 0.41f), bed = new Color(0.4f, 0.38f, 0.32f);
                float jitter = 0.94f + 0.08f * Mathf.PerlinNoise(x * 0.11f, z * 0.11f);
                Color c = h > Water + 0.25f ? dry : h > Water - 0.2f ? Color.Lerp(wet, dry, Mathf.InverseLerp(Water - 0.2f, Water + 0.25f, h)) : bed;
                c *= jitter; c.a = 1f;
                return c;
            }
            var sandMat = Mats.Lit("beach_sand", Color.white, 0f, 0.2f, Mats.Noise, null, 0.15f, 0f, true, 2.2f);
            int res = Mathf.RoundToInt(ext * 2f / 5f);
            b.Terrain(new Vector3(-ext, 0, -ext), ext * 2f, res, Grass, grass, (x, z) => new Color(0.97f, 0.97f, 0.97f, 1f), Synth.Surface.Grass);
            b.Terrain(new Vector3(-ext, 0, -ext), ext * 2f, res, Sand, sandMat, SandCol, Synth.Surface.Dirt);
            // the open sea: a ring around the island out to the horizon (the Dockyard keeps its own harbour water to the east)
            float far = 1800f, inner = land, eastStart = seaEast ? half + 60f : land;
            b.Water(new Vector3((-far - inner) / 2f, Water, 0), new Vector2(far - inner, far * 2f), 3.2f);
            b.Water(new Vector3((eastStart + far) / 2f, Water, 0), new Vector2(far - eastStart, far * 2f), 3.2f);
            b.Water(new Vector3((-inner + eastStart) / 2f, Water, (inner + far) / 2f), new Vector2(eastStart + inner, far - inner), 3.2f);
            b.Water(new Vector3((-inner + eastStart) / 2f, Water, (-inner - far) / 2f), new Vector2(eastStart + inner, far - inner), 3.2f);
            // along the shore: rocks, driftwood and a few old jetties
            Vector3 OnShore(float ang, float offset)
            {
                var u = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                float e = ShoreAt(u.x * 100f, u.z * 100f, land);
                float r = (e + offset) / Mathf.Max(0.001f, IslandD(u.x, u.z));
                var p = u * r;
                p.y = Mathf.Max(Sand(p.x, p.z), offset < 0 ? -0.02f : -8f);
                return p;
            }
            bool Blocked(Vector3 p) => EastSea(p.x + 15f) || Mathf.Abs(p.x) > half - 2f || Mathf.Abs(p.z) > half - 2f;
            for (int i = 0; i < 150; i++)
            {
                var p = OnShore(b.Rng.Range(0f, Mathf.PI * 2f), b.Rng.Range(-4f, 30f));
                if (Blocked(p)) continue;
                b.Rock(p, b.Rng.Range(0.6f, 2.8f) * (b.Rng.Chance(0.15f) ? 1.8f : 1f));
            }
            var drift = Mats.Lit("driftwood", Color.white, 0f, 0.15f, Mats.Bark, null, 0.3f, 0f, true, 1.5f);
            for (int i = 0; i < 45; i++)
            {
                var p = OnShore(b.Rng.Range(0f, Mathf.PI * 2f), b.Rng.Range(3f, 16f));
                if (Blocked(p) || p.y < Water + 0.15f) continue;
                float yaw = b.Rng.Range(0f, Mathf.PI * 2f), len = b.Rng.Range(1.5f, 4.5f), r = b.Rng.Range(0.08f, 0.2f);
                var d = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)) * len * 0.5f;
                b.Cyl(p - d + Vector3.up * r * 0.6f, p + d + Vector3.up * r * 0.6f, r, r * 0.8f, 7, drift, new Color(0.75f, 0.7f, 0.62f), true, Synth.Surface.Wood);
            }
            var plank = Maps.WoodPlank;
            for (int j = 0; j < 3; j++)
            {
                float ang = (j / 3f + b.Rng.Range(0.05f, 0.25f)) * Mathf.PI * 2f;
                var start = OnShore(ang, 2f);
                if (Blocked(start + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 40f)) continue;
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                float deckY = 0.35f, len = 34f;
                var q = Quaternion.LookRotation(dir, Vector3.up);
                var mid = start + dir * (len * 0.5f); mid.y = deckY;
                b.Box(mid, new Vector3(2.6f, 0.14f, len), plank, new Color(0.55f, 0.47f, 0.38f), q, true, Synth.Surface.Wood);
                for (float t = 1f; t < len; t += 3.4f)
                    foreach (float sx in new[] { -1.2f, 1.2f })
                    {
                        var pp = start + dir * t + Vector3.Cross(Vector3.up, dir) * sx;
                        float bottom = Mathf.Min(Sand(pp.x, pp.z), -0.5f) - 0.6f;
                        b.Cyl(new Vector3(pp.x, bottom, pp.z), new Vector3(pp.x, deckY + 0.6f, pp.z), 0.11f, 0.1f, 8, plank, new Color(0.4f, 0.33f, 0.27f), false);
                    }
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

        int _roadN;
        void Road(Vector3 a, Vector3 c, Material m)
        {
            var d = c - a; d.y = 0; float len = d.magnitude;
            if (len < 1) return;
            // split long roads so each piece lands in its own world chunk
            int pieces = Mathf.CeilToInt(len / 60f);
            _roadN++;
            for (int i = 0; i < pieces; i++)
            {
                var p0 = a + d * (i / (float)pieces); var p1 = a + d * ((i + 1) / (float)pieces);
                // every road (and every other piece of one) a few mm higher than the last: roads crossing or overlapping
                // used to lie at exactly the same height and flicker between each other
                float y = 0.02f + (_roadN % 6) * 0.006f + (i % 2) * 0.003f;
                B.Box((p0 + p1) / 2 + Vector3.up * y, new Vector3(7, 0.02f, len / pieces + 0.2f), m, Color.white, Quaternion.LookRotation(d.normalized), false);
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

        /// <summary>
        /// A village: 5-8 houses (one or two storeys, brick / plaster / timber) round a crossroads, with a well,
        /// garden walls and parked wrecks. Every house has loot spots.
        /// </summary>
        List<Vector3> Hamlet(Vector3 p, float yaw, bool wooden)
        {
            var b = B; var pts = new List<Vector3>();
            var q = Quaternion.Euler(0, yaw, 0);
            var roadMat = wooden ? Maps.GroundDirt : Maps.Asphalt;
            // crossroads
            b.Box(p + Vector3.up * 0.01f, new Vector3(60f, 0.02f, 7f), roadMat, Color.white, q, false);
            b.Box(p + Vector3.up * 0.03f, new Vector3(7f, 0.02f, 60f), roadMat, Color.white, q, false);   // clearly above the other road (they flickered where they cross)
            Material[] walls = wooden ? new[] { Maps.WoodPlank, Maps.WoodPlank, Maps.Plaster } : new[] { Maps.Brick, Maps.Plaster, Maps.WoodPlank, Maps.Brick };
            Color[] tints = { new Color(0.85f, 0.8f, 0.72f), new Color(0.75f, 0.68f, 0.6f), new Color(0.9f, 0.88f, 0.82f), new Color(0.65f, 0.55f, 0.45f), new Color(0.8f, 0.82f, 0.75f) };
            int houses = b.Rng.Range(5, 9);
            for (int i = 0; i < houses; i++)
            {
                // one per quarter of the crossroads, then a second row behind
                int quad = i % 4, row = i / 4;
                float sx = (quad % 2 == 0 ? 1 : -1), sz = (quad < 2 ? 1 : -1);
                var size = new Vector2(b.Rng.Range(7f, 10f), b.Rng.Range(6f, 8.5f));
                var lp = new Vector3(sx * (10f + size.x * 0.5f + row * 14f), 0, sz * (9f + size.y * 0.5f + b.Rng.Range(0f, 4f)));
                var hp = p + q * lp;
                float hy = yaw + (sz > 0 ? 180f : 0f);   // the front door faces the road
                int floors = b.Rng.Chance(0.4f) ? 2 : 1;
                var wall = b.Rng.Pick(walls);
                pts.AddRange(b.Building(hp, hy, size, floors, wall, b.Rng.Pick(tints), Maps.WoodPlank, Maps.ConcreteDark, 0, true, false));
                // garden wall at the back (low cover)
                if (b.Rng.Chance(0.6f)) b.Box(hp + Quaternion.Euler(0, hy, 0) * new Vector3(0, 0.5f, size.y * 0.5f + 2.5f), new Vector3(size.x + 3f, 1.0f, 0.35f), Maps.Brick, new Color(0.7f, 0.6f, 0.5f), Quaternion.Euler(0, hy, 0), true, Synth.Surface.Concrete);
            }
            // a well in the square and a couple of wrecks
            b.Cyl(p + q * new Vector3(5.5f, 0, 5.5f), p + q * new Vector3(5.5f, 0.9f, 5.5f), 0.9f, 0.9f, 14, Maps.ConcreteDark, new Color(0.6f, 0.6f, 0.58f), true);
            if (b.Rng.Chance(0.7f)) b.CarWreck(p + q * new Vector3(-2f, 0, 14f), yaw + b.Rng.Range(-15f, 15f), new Color(0.4f, 0.3f, 0.25f));
            if (b.Rng.Chance(0.5f)) b.CarWreck(p + q * new Vector3(15f, 0, 2f), yaw + 90 + b.Rng.Range(-15f, 15f), new Color(0.25f, 0.3f, 0.35f));
            return pts;
        }

        /// <summary>
        /// Relief and cover across the open country: rocky cliff ridges (5-16 m high, steep, climbable at the low ends)
        /// that break the island up into valleys, and lots of small cover in the fields - stone walls, rock clusters,
        /// hay bales, log piles, sandbag nests and wrecks - so crossing open ground is never a sprint with nowhere to hide.
        /// </summary>
        void CliffsAndCover(Rect core, float land, List<Vector3> spots, List<(Vector3, Vector3)> roads, bool seaEast, bool wooden)
        {
            var b = B;
            var rockMat = Mats.Lit("cliff", Color.white, 0f, 0.18f, Mats.Concrete, null, 0.7f, 0.1f, true, 0.35f);
            bool Free(Vector3 t, float poi, float road)
            {
                if (t.x > core.xMin - 25 && t.x < core.xMax + 25 && t.z > core.yMin - 25 && t.z < core.yMax + 25) return false;
                if (seaEast && t.x > core.xMax - 10) return false;
                return !Near(t, spots, poi) && !NearRoad(t, roads, road) && OnLand(t, 20f);
            }
            // cliff ridges: a long wall of overlapping, jagged rock - tallest in the middle (up to ~22 m), tapering to
            // scrambling height at the ends, with a broken top line and buttresses at the foot
            int ridges = Mathf.RoundToInt(land / 75f);
            for (int r = 0; r < ridges; r++)
            {
                var start = new Vector3(b.Rng.Range(-land + 60, land - 60), 0, b.Rng.Range(-land + 60, land - 60));
                float heading = b.Rng.Range(0f, 360f);
                int segs = b.Rng.Range(12, 26);
                float peak = b.Rng.Range(10f, 22f);
                var pos = start;
                for (int k = 0; k < segs; k++)
                {
                    heading += b.Rng.Range(-12f, 12f);
                    var dir = Quaternion.Euler(0, heading, 0) * Vector3.forward;
                    var side = Quaternion.Euler(0, heading + 90, 0) * Vector3.forward;
                    pos += dir * b.Rng.Range(6.5f, 8.5f);
                    float t = (float)k / Mathf.Max(1, segs - 1);
                    float h = Mathf.Lerp(2.2f, peak, Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.6f)) * b.Rng.Range(0.85f, 1.12f);
                    // the draws happen whether or not this piece is built, so the ridge's shape doesn't depend on gaps
                    float len = b.Rng.Range(11f, 15f), dep = b.Rng.Range(7f, 10f), lean = b.Rng.Range(-6f, 6f), tw = b.Rng.Range(-14f, 14f);
                    float topH = h * b.Rng.Range(0.25f, 0.45f), off = b.Rng.Range(-2f, 2f), tt = b.Rng.Range(-20f, 20f);
                    float bh = h * b.Rng.Range(0.3f, 0.55f), bs = b.Rng.Chance(0.5f) ? 1f : -1f; bool tree = b.Rng.Chance(0.3f), boulder = b.Rng.Chance(0.6f);
                    if (!Free(pos, 55f, 14f)) continue;   // gaps where a road or a place is: you can always get through
                    pos.y = HillY(pos);
                    Color tone = new Color(0.5f, 0.48f, 0.45f) * b.Rng.Range(0.82f, 1.08f);
                    // main body
                    b.Box(pos + Vector3.up * (h * 0.5f - 1f), new Vector3(len, h + 1f, dep), rockMat, tone, Quaternion.Euler(lean, heading + tw, lean * 0.5f), true, Synth.Surface.Concrete, 0.8f);
                    // a jagged crown on top
                    b.Box(pos + side * off + Vector3.up * (h + topH * 0.35f - 0.5f), new Vector3(len * 0.55f, topH, dep * 0.6f), rockMat, tone * 0.94f, Quaternion.Euler(lean + 8f, heading + tt, -lean), true, Synth.Surface.Concrete, 0.6f);
                    // a buttress at the foot on one side (makes the face uneven - and somewhere to climb)
                    b.Box(pos + side * (bs * dep * 0.6f) + Vector3.up * (bh * 0.5f - 0.5f), new Vector3(len * 0.6f, bh, dep * 0.55f), rockMat, tone * 0.9f, Quaternion.Euler(b.Rng.Range(-12f, 12f), heading + b.Rng.Range(-35f, 35f), b.Rng.Range(-12f, 12f)), true, Synth.Surface.Concrete, 0.6f);
                    if (boulder) b.Rock(pos + side * (-bs * dep * 0.85f), b.Rng.Range(1.2f, 2.8f));
                    if (tree) b.Pine(pos + Vector3.up * (h - 0.6f), b.Rng.Range(5f, 9f));   // a tree clinging to the top
                }
            }
            // field cover
            int cover = Mathf.RoundToInt(land * 0.55f);
            for (int i = 0; i < cover; i++)
            {
                var t = new Vector3(b.Rng.Range(-land + 10, land - 10), 0, b.Rng.Range(-land + 10, land - 10));
                if (!Free(t, 35f, 5f)) continue;
                t.y = HillY(t);
                float yaw = b.Rng.Range(0f, 360f);
                var q = Quaternion.Euler(0, yaw, 0);
                switch (b.Rng.Range(0, 6))
                {
                    case 0:   // dry-stone wall, waist to chest high
                        float len = b.Rng.Range(6f, 16f), wh = b.Rng.Range(0.9f, 1.4f);
                        b.Box(t + Vector3.up * (wh * 0.5f), new Vector3(len, wh, 0.6f), rockMat, new Color(0.6f, 0.58f, 0.52f), q, true, Synth.Surface.Concrete, 0.15f);
                        break;
                    case 1:   // rock cluster
                        for (int k = 0; k < 3; k++) { var o = b.Rng.InCircle() * 2.5f; b.Rock(t + new Vector3(o.x, 0, o.y), b.Rng.Range(1f, 2.4f)); }
                        break;
                    case 2:   // hay bales
                        for (int k = 0; k < b.Rng.Range(2, 5); k++) b.Cyl(t + q * new Vector3(k * 1.7f, 0.75f, -0.8f), t + q * new Vector3(k * 1.7f, 0.75f, 0.8f), 0.75f, 0.75f, 14, Mats.Lit("hay", new Color(0.78f, 0.66f, 0.35f), 0f, 0.1f, Mats.Fabric, null, 0.5f, 0f), Color.white, true, Synth.Surface.Dirt);
                        break;
                    case 3:   // log pile
                        for (int k = 0; k < 5; k++) { int row = k < 3 ? 0 : 1; float x = (k < 3 ? k - 1 : k - 3.5f) * 0.55f; b.Cyl(t + q * new Vector3(x, 0.28f + row * 0.48f, -2.2f), t + q * new Vector3(x, 0.28f + row * 0.48f, 2.2f), 0.28f, 0.28f, 10, Maps.WoodPlank, new Color(0.45f, 0.33f, 0.22f), true, Synth.Surface.Wood); }
                        break;
                    case 4:   // sandbag nest
                        b.Sandbags(t + q * new Vector3(-2f, 0, 0), t + q * new Vector3(2f, 0, 0));
                        b.Sandbags(t + q * new Vector3(2f, 0, 0), t + q * new Vector3(2.8f, 0, 2f));
                        break;
                    default:  // a wreck
                        b.CarWreck(t, yaw, new Color(b.Rng.Range(0.2f, 0.5f), b.Rng.Range(0.2f, 0.4f), b.Rng.Range(0.2f, 0.4f)));
                        break;
                }
            }
        }

        // ---- hills: real rolling hills over the open country (up to ~35 m), flat where the towns, places, roads,
        // player spawns and the shore are, so everything built there still stands on level ground
        bool _hills; float _hox, _hoz, _hSlab; Rect _hCore; bool _hSea;
        readonly List<Vector3> _hFlat = new List<Vector3>();
        readonly List<float> _hFlatR = new List<float>();
        List<(Vector3, Vector3)> _hRoads;

        /// <summary>Height of the hills at p (0 where it's flat, or on maps / places without hills).</summary>
        public float HillY(Vector3 p)
        {
            if (!_hills) return 0f;
            float x = p.x, z = p.z;
            float edge = _hSlab - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (edge <= 0f) return 0f;
            float f = Mathf.SmoothStep(0f, 1f, edge / 90f);
            // the town in the middle
            float dx = Mathf.Max(Mathf.Max(_hCore.xMin - 20 - x, 0f), x - _hCore.xMax - 20), dz = Mathf.Max(Mathf.Max(_hCore.yMin - 20 - z, 0f), z - _hCore.yMax - 20);
            f = Mathf.Min(f, Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / 70f));
            if (_hSea && x > _hCore.xMax - 40) f = Mathf.Min(f, Mathf.SmoothStep(0f, 1f, (_hCore.xMax - 20 - x) / 40f));
            if (f <= 0f) return 0f;
            for (int i = 0; i < _hFlat.Count; i++)
            {
                float d = new Vector2(x - _hFlat[i].x, z - _hFlat[i].z).magnitude - _hFlatR[i];
                if (d < 50f) f = Mathf.Min(f, Mathf.SmoothStep(0f, 1f, d / 50f));
                if (f <= 0f) return 0f;
            }
            if (_hRoads != null)
                foreach (var (ra, rb) in _hRoads)
                {
                    Vector3 rd = rb - ra; float rl2 = rd.sqrMagnitude; if (rl2 < 1f) continue;
                    float t = Mathf.Clamp01(((x - ra.x) * rd.x + (z - ra.z) * rd.z) / rl2);
                    float d = new Vector2(x - (ra.x + rd.x * t), z - (ra.z + rd.z * t)).magnitude - 8f;
                    if (d < 32f) f = Mathf.Min(f, Mathf.SmoothStep(0f, 1f, d / 32f));
                    if (f <= 0f) return 0f;
                }
            float n = Mathf.PerlinNoise(x * 0.0032f + _hox, z * 0.0032f + _hoz);
            float n2 = Mathf.PerlinNoise(x * 0.011f + _hoz, z * 0.011f + _hox);
            float h = Mathf.Pow(Mathf.Clamp01((n - 0.22f) / 0.78f), 1.5f) * 36f + n2 * 4f;
            return h * f;
        }

        void BuildHills(Rect core, float slab, List<Vector3> spots, List<(Vector3, Vector3)> roads, List<Vector3> sites, bool seaEast, Material ground)
        {
            var b = B;
            _hox = b.Rng.Range(0f, 100f); _hoz = b.Rng.Range(0f, 100f);
            _hSlab = slab; _hCore = core; _hSea = seaEast; _hRoads = roads;
            _hFlat.Clear(); _hFlatR.Clear();
            foreach (var s in spots) { _hFlat.Add(s); _hFlatR.Add(55f); }
            foreach (var s in sites) { _hFlat.Add(s); _hFlatR.Add(24f); }
            foreach (var e in Extracts) if (e != null) { _hFlat.Add(e.transform.position); _hFlatR.Add(22f); }
            _hills = true;
            float size = slab * 2f;
            int res = Mathf.Clamp(Mathf.RoundToInt(size / 6.5f), 60, 240);
            // where it's flat the hills mesh sits a few cm under the flat ground slabs (it used to lie exactly on them: the two
            // surfaces fought over every pixel and the ground flickered); it blends back up to the real height on slopes
            b.Terrain(new Vector3(-slab, 0, -slab), size, res, (x, z) => { float h = HillY(new Vector3(x, 0, z)); return h - 0.07f * (1f - Mathf.Clamp01(h / 0.4f)); }, ground, (x, z) => Color.white, Synth.Surface.Grass);
        }

        /// <summary>Scatter random loot crates around interior points.</summary>
        /// <summary>
        /// Register a building's loot spots: on the floor against a wall, or on a desk. Each raid picks a different
        /// selection of them (about one in three floor spots, half the desks).
        /// </summary>
        /// <summary>Is there room for this box here - nothing solid inside its footprint (a few cm of slack for walls it backs onto)?</summary>
        bool CrateFits(Vector3 pos, float yaw, LootCrate.Kind kind)
        {
            Vector3 size = LootCrate.SizeOf(kind);
            float tt = B != null ? B.TerrainTop(pos) : float.NegativeInfinity;   // on a hillside: measure from the ground (the crate is settled onto it later)
            if (tt > pos.y && tt < pos.y + 3f) pos.y = tt;
            Vector3 half = new Vector3(Mathf.Max(0.05f, size.x * 0.5f - 0.05f), Mathf.Max(0.04f, size.y * 0.5f - 0.03f), Mathf.Max(0.05f, size.z * 0.5f - 0.05f));
            Vector3 center = pos + Vector3.up * (size.y * 0.5f + 0.08f);
            return !Physics.CheckBox(center, half, Quaternion.Euler(0, yaw, 0), ~0, QueryTriggerInteraction.Ignore);
        }

        protected void ScatterLoot(List<Vector3> pts, float chance)
        {
            foreach (var p in pts)
            {
                bool desk = B.DeskSpots.ContainsKey(p);
                float yaw = B.SpotYaw.TryGetValue(p, out var y) ? y : B.Rng.Range(0f, 360f);
                AddCrate(p, yaw, null, desk ? chance * 0.6f : chance * 0.4f, desk);
            }
        }
    }

    // ===================================================================================== loot crates
    public class LootCrate : MonoBehaviour
    {
        /// <summary>How big each kind of box is (width, height, depth).</summary>
        public static Vector3 SizeOf(Kind k)
        {
            switch (k)
            {
                case Kind.WeaponCase: return new Vector3(1.15f, 0.16f, 0.42f);
                case Kind.Optics: return new Vector3(0.4f, 0.2f, 0.32f);
                case Kind.Medical: return new Vector3(0.5f, 0.28f, 0.34f);
                case Kind.Safe: return new Vector3(0.5f, 0.5f, 0.45f);
                case Kind.ArmorCase: return new Vector3(0.72f, 0.4f, 0.5f);
                case Kind.Duffel: return new Vector3(0.6f, 0.28f, 0.36f);
                case Kind.AirDrop: return new Vector3(1.8f, 0.95f, 1.2f);
                case Kind.Present: return new Vector3(0.42f, 0.3f, 0.42f);
                default: return new Vector3(0.9f, 0.5f, 0.55f);
            }
        }
        public enum Kind { Crate, WeaponCase, Optics, Medical, Safe, Duffel, ArmorCase, AirDrop, Present }
        public Kind kind;
        public int index;
        public bool opened;
        public bool vaultGrade;   // a crate inside a vault: vault loot, and more of it
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
            ByIndex[index] = c;
            Vector3 size = SizeOf(kind); Material mat; Color col;
            switch (kind)
            {
                case Kind.WeaponCase: mat = Mats.Lit("case_hard", new Color(0.12f, 0.13f, 0.12f), 0.1f, 0.45f, Mats.Noise, null, 0.3f, 0.3f); col = Color.white; break;
                case Kind.Optics: mat = Mats.Lit("case_optics", new Color(0.55f, 0.48f, 0.33f), 0.1f, 0.35f, Mats.Noise, null, 0.4f, 0.3f); col = Color.white; break;
                case Kind.Medical: mat = Mats.Lit("case_med", new Color(0.85f, 0.85f, 0.82f), 0.1f, 0.4f, Mats.Noise, null, 0.4f, 0.3f); col = Color.white; break;
                case Kind.Safe: mat = Mats.Lit("safe", new Color(0.2f, 0.22f, 0.24f), 0.8f, 0.4f, Mats.Metal, Mats.MetalN, 0.4f, 0.5f); col = Color.white; break;
                case Kind.ArmorCase: mat = Mats.Lit("case_armor", new Color(0.24f, 0.27f, 0.2f), 0.15f, 0.35f, Mats.Noise, null, 0.45f, 0.35f); col = Color.white; break;
                case Kind.Duffel: mat = Mats.Lit("duffel", new Color(0.15f, 0.15f, 0.14f), 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.4f, 0f); col = Color.white; break;
                case Kind.AirDrop: mat = Mats.Lit("crate_airdrop", new Color(0.3f, 0.34f, 0.24f), 0.1f, 0.35f, Mats.Wood, Mats.WoodN, 0.5f, 0.3f); col = Color.white; break;
                case Kind.Present: mat = Mats.Lit("present_wrap", Color.white, 0.1f, 0.5f, Mats.Noise, null, 0.08f, 0f); col = PresentWrap[index % PresentWrap.Length]; break;
                default: mat = Mats.Lit("crate_mil", new Color(0.33f, 0.36f, 0.26f), 0f, 0.3f, Mats.Wood, Mats.WoodN, 0.5f, 0.3f); col = Color.white; break;
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
            Color ribbon = kind == Kind.Present ? PresentRibbon[index % PresentRibbon.Length] : Color.white;
            if (kind == Kind.Present)
            {
                // ribbon round all four sides
                mb.Sub(0).Box(new Vector3(0, size.y * 0.5f, size.z * 0.5f + 0.002f), new Vector3(0.05f, size.y, 0.004f), ribbon, 0f);
                mb.Sub(0).Box(new Vector3(0, size.y * 0.5f, -size.z * 0.5f - 0.002f), new Vector3(0.05f, size.y, 0.004f), ribbon, 0f);
                mb.Sub(0).Box(new Vector3(size.x * 0.5f + 0.002f, size.y * 0.5f, 0), new Vector3(0.004f, size.y, 0.05f), ribbon, 0f);
                mb.Sub(0).Box(new Vector3(-size.x * 0.5f - 0.002f, size.y * 0.5f, 0), new Vector3(0.004f, size.y, 0.05f), ribbon, 0f);
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
            if (kind == Kind.Present)
            {
                // ribbon cross over the lid and a bow on top
                lid.Box(new Vector3(0, 0.032f, size.z * 0.5f), new Vector3(0.05f, 0.006f, size.z + 0.014f), ribbon, 0f);
                lid.Box(new Vector3(0, 0.032f, size.z * 0.5f), new Vector3(size.x + 0.014f, 0.006f, 0.05f), ribbon, 0f);
                lid.Sphere(new Vector3(-0.045f, 0.06f, size.z * 0.5f), new Vector3(0.05f, 0.03f, 0.025f), 8, ribbon);
                lid.Sphere(new Vector3(0.045f, 0.06f, size.z * 0.5f), new Vector3(0.05f, 0.03f, 0.025f), 8, ribbon);
                lid.Sphere(new Vector3(0, 0.055f, size.z * 0.5f), Vector3.one * 0.022f, 8, ribbon * 0.85f);
            }
            if (kind == Kind.WeaponCase || kind == Kind.Optics) for (int i = 0; i < 2; i++) lid.Box(new Vector3(-size.x * 0.3f + i * size.x * 0.6f, 0.0f, size.z + 0.008f), new Vector3(0.05f, 0.03f, 0.02f), new Color(0.4f, 0.4f, 0.4f), 0.003f);
            lid.Build("Lid", c._lid, mat);
            var lc = c._lid.gameObject.AddComponent<BoxCollider>();
            lc.center = new Vector3(0, 0.015f, size.z * 0.5f); lc.size = new Vector3(size.x, 0.03f, size.z);
            // item cells
            int cols = kind == Kind.WeaponCase ? 1 : kind == Kind.Optics || kind == Kind.ArmorCase || kind == Kind.Present ? 2 : kind == Kind.AirDrop ? 4 : 3;
            int rows = kind == Kind.WeaponCase || kind == Kind.ArmorCase ? 1 : kind == Kind.AirDrop ? 3 : 2;
            float fit = kind == Kind.WeaponCase ? 1.1f : Mathf.Min(size.x / cols, size.z / rows) * 0.95f;
            c._box = ItemContainer.Grid(go.transform, new Vector3(0, 0.06f, 0), Quaternion.identity, cols, rows, new Vector2(size.x / cols, size.z / rows), fit,
                null, kind == Kind.WeaponCase ? Quaternion.Euler(0, 90, 0) * Quaternion.Euler(0, 0, 90) : Quaternion.identity);   // the gun lies along the case
            foreach (var s in c._box.slots) s.enabled = false;
            return c;
        }

        static readonly Color[] PresentWrap = { new Color(0.75f, 0.1f, 0.1f), new Color(0.1f, 0.45f, 0.2f), new Color(0.2f, 0.3f, 0.75f), new Color(0.85f, 0.7f, 0.2f), new Color(0.82f, 0.82f, 0.85f), new Color(0.5f, 0.15f, 0.55f) };
        static readonly Color[] PresentRibbon = { new Color(0.9f, 0.75f, 0.2f), new Color(0.85f, 0.1f, 0.1f), new Color(0.92f, 0.92f, 0.92f), new Color(0.75f, 0.1f, 0.1f), new Color(0.1f, 0.45f, 0.2f), new Color(0.9f, 0.75f, 0.2f) };

        /// <summary>Tester menu: a present on the floor here, with a fresh random roll inside.</summary>
        public static LootCrate SpawnPresent(Vector3 pos, float yaw)
        {
            var map = RagoGame.I != null ? RagoGame.I.Map : null;
            if (map == null) return null;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var h, 6f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) pos = h.point;
            int index = 0;
            for (int i = 0; i < 50; i++) { index = 6000 + Random.Range(0, 3000); if (!ByIndex.ContainsKey(index)) break; }
            var c = Create(map, index, pos, yaw, Kind.Present);
            c.local = true;
            return c;
        }
        public bool presentGrade;   // inside the North Pole's giant present: full of vault loot
        public bool santaGift;   // dropped by Evil Santa: always has a North Pole Keycard
        public bool local;   // spawned from the tester menu: only on this machine, so opening it isn't sent to anyone

        /// <summary>Every crate by its index, so another player opening one opens it here too.</summary>
        public static readonly System.Collections.Generic.Dictionary<int, LootCrate> ByIndex = new System.Collections.Generic.Dictionary<int, LootCrate>();
        void OnDestroy() { if (ByIndex.TryGetValue(index, out var c) && c == this) ByIndex.Remove(index); }

        static void AddBox(Transform t, Vector3 c, Vector3 s) { var b = t.gameObject.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

        void Update()
        {
            if (!opened)
            {
                var p = RagoGame.I != null ? RagoGame.I.Player : null;
                if (p == null) return;
                Vector3 lp = _lid.position;
                // an EMPTY hand opens it (a hand holding something used to set it off - and the item came loose)
                bool near = Vector3.Distance(p.HandL.Anchor.position, lp) < 0.7f && p.HandL.Input.GripHeld && p.HandL.Held == null
                         || Vector3.Distance(p.HandR.Anchor.position, lp) < 0.7f && p.HandR.Input.GripHeld && p.HandR.Held == null;
                if (!XRInputs.VR && Desktop.Interact && Physics.Raycast(p.Head.position, p.Head.forward, out var h, 3f) && h.collider.transform.IsChildOf(transform)) near = true;
                if (near) Open();
            }
            if (opened && _openAnim < 1f)
            {
                _openAnim = Mathf.MoveTowards(_openAnim, 1f, Time.deltaTime * 2.5f);
                _lid.localRotation = Quaternion.Euler(-105f * Mathf.SmoothStep(0, 1, _openAnim), 0, 0);
            }
        }

        public void Open() => Open(true);

        /// <param name="tell">we opened it: tell the other players, so it's open (with the same loot inside) for them too</param>
        public void Open(bool tell)
        {
            if (opened) return;
            opened = true;
            if (tell && !local && NetGame.Live) NetGame.I.CrateOpened(index);
            Sfx.Play(kind == Kind.Safe ? Synth.SlideBack() : Synth.Grab(1), transform.position, 0.6f, kind == Kind.Safe ? 0.6f : 0.8f, 1f, 10f);
            foreach (var s in _box.slots) s.enabled = true;
            var rng = new Rng(_map.Seed * 7349 + index * 131);
            if (kind == Kind.Present) { OpenPresent(rng, tell); return; }
            var table = vaultGrade ? Db.LootTable.Vault : kind == Kind.WeaponCase ? Db.LootTable.WeaponCase : kind == Kind.Optics ? Db.LootTable.OpticsBox : kind == Kind.Medical ? Db.LootTable.Medical : kind == Kind.Safe ? Db.LootTable.Safe : kind == Kind.ArmorCase ? Db.LootTable.Armor : Db.LootTable.Crate;
            int n = presentGrade ? _box.slots.Count : kind == Kind.AirDrop ? Mathf.Min(_box.slots.Count, rng.Range(5, 9)) : vaultGrade ? Mathf.Min(_box.slots.Count, rng.Range(2, 5)) : kind == Kind.WeaponCase ? 1 : kind == Kind.Optics || kind == Kind.ArmorCase ? rng.Range(1, 3) : rng.Range(1, 5);
            for (int i = 0; i < n && i < _box.slots.Count; i++)
            {
                var def = Db.Roll(table, rng);
                if (def == null) continue;
                var d = new ItemData(def.id);
                if (def is GunDef gd) { d.rounds = rng.Range(0, gd.magSize + 1); d.chambered = false; }
                if (NetGame.Taken(10000 + index * 16 + i)) continue;   // another player already took it
                var it = ItemFactory.Spawn(d, _box.slots[i].transform.position, transform.rotation);
                if (it == null) continue;
                it.netId = 10000 + index * 16 + i;
                NetGame.I?.Register(it);
                _box.slots[i].Store(it, false);
            }
            // Christmas (and always on the North Pole): crates often hold a small present - shake it open
            if (_map != null && (Festive.On || _map.Id == "northpole" || LiveEvent.Is(LiveEvent.Presents)) && kind != Kind.Present && rng.Chance(LiveEvent.Is(LiveEvent.Presents) ? 0.75f : _map.Id == "northpole" ? 0.6f : 0.35f))
            {
                int gid = 10000 + index * 16 + 12;
                Slot fs = null;
                foreach (var sl in _box.slots) if (sl.item == null) { fs = sl; break; }
                if (fs != null && !NetGame.Taken(gid))
                {
                    var gi = ItemFactory.Spawn(new ItemData("gift"), fs.transform.position, transform.rotation);
                    if (gi != null) { gi.netId = gid; NetGame.I?.Register(gi); fs.Store(gi, false); }
                }
            }
            // vaults open with a keycard only: safes often have one
            // vaults (and air drops): a small chance of a Vault Keycard or a Military Radio - the only crates that ever have one
            if (vaultGrade)
            {
                string[] rare = { "key_vault", "val_radio" };
                for (int r = 0; r < rare.Length; r++)
                {
                    int rid = 10000 + index * 16 + 13 + r;
                    if (!rng.Chance(presentGrade ? 0.5f : 0.06f) || NetGame.Taken(rid)) continue;
                    if (rare[r] == "val_radio" && _map != null && _map.Id == "underground" && !rng.Chance(0.5f)) continue;
                    Slot fs = null;
                    foreach (var sl in _box.slots) if (sl.item == null) { fs = sl; break; }
                    if (fs == null) break;
                    var ri = ItemFactory.Spawn(new ItemData(rare[r]), fs.transform.position, transform.rotation);
                    if (ri == null) continue;
                    ri.netId = rid; NetGame.I?.Register(ri); fs.Store(ri, false);
                }
            }
        }

        /// <summary>
        /// A Christmas present: one roll decides what's inside, and the better it is the rarer it is - mostly ammo,
        /// meds and snacks, sometimes a gun or armour, now and then a LIVE GRENADE that pops out and goes off, and very
        /// rarely a top-tier gun, a Vault Keycard, a Military Radio or a whole vault's worth of loot. Every machine
        /// rolls the same (map seed + present), so everyone agrees what was in it.
        /// </summary>
        void OpenPresent(Rng rng, bool opener)
        {
            Sfx.Play(Synth.UiClick(), transform.position, 0.6f, 1.3f, 1f, 8f);
            bool pole = _map != null && _map.Id == "northpole";
            var items = PresentItem.Roll(rng, pole, santaGift, out bool grenade);
            // the prank: a live grenade on a short fuse jumps out (only on the machine that opened it: the blast is shared)
            if (grenade && opener && ItemFactory.Spawn(new ItemData("frag"), transform.position + Vector3.up * (Size.y + 0.15f), Quaternion.identity) is Grenade gr)
            {
                gr.ArmNow(2.5f);
                if (gr.rb != null) { gr.rb.SetVel(Vector3.up * 3.2f + Random.insideUnitSphere * 0.6f); gr.rb.angularVelocity = Random.insideUnitSphere * 6f; }
            }
            for (int i = 0; i < items.Count && i < _box.slots.Count; i++)
            {
                int nid = 10000 + index * 16 + i;
                if (!local && NetGame.Taken(nid)) continue;
                var it = ItemFactory.Spawn(items[i], _box.slots[i].transform.position, transform.rotation);
                if (it == null) continue;
                if (!local) { it.netId = nid; NetGame.I?.Register(it); }
                _box.slots[i].Store(it, false);
            }
        }
    }


    /// <summary>Puts this raid's loose keycards down once the raid is running (so they get network ids like crate loot).</summary>
    public class KeycardSpawner : MonoBehaviour
    {
        public List<Vector3> At;
        public List<string> Ids;
        public int BaseId = 950000;   // network ids: 950000+ keycards / radios, 960000+ loose melee weapons
        System.Collections.IEnumerator Start()
        {
            yield return new WaitForSeconds(1f);
            for (int i = 0; At != null && i < At.Count; i++)
            {
                if (NetGame.Taken(BaseId + i)) continue;
                string id = Ids != null && i < Ids.Count ? Ids[i] : "key_vault";
                var it = ItemFactory.Spawn(new ItemData(id), At[i] + Vector3.up * 0.04f, Quaternion.Euler(0, i * 67f + 20f, 0));
                if (it == null) continue;
                it.netId = BaseId + i;
                NetGame.I?.Register(it);
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
            _screenSub = UI.Text(scr, "TOUCH CALL TO EXFIL", new Vector3(0, -0.055f, 0), 0.016f, UI.CrtDim, TextAnchor.MiddleCenter);
            _touchA = UI.Button(scr, "CALL EXFIL", new Vector3(0.0f, -0.055f, -0.004f), new Vector2(0.2f, 0.04f), Call, null, BtnStyle.Crt).transform; _touchA.GetComponent<WorldButton>().Label.text = "";
            var deck = new GameObject("Deck").transform; deck.SetParent(_laptop, false);
            deck.localPosition = new Vector3(0, 0.474f, -0.04f); deck.localRotation = Quaternion.Euler(90, 0, 0);
            _touchB = UI.Button(deck, "CALL", new Vector3(0, 0, 0), new Vector2(0.12f, 0.06f), Call, new Color(0.2f, 0.42f, 0.2f), BtnStyle.Key).transform;
            var col = _laptop.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.3f, 0); col.size = new Vector3(0.62f, 0.62f, 0.46f);
            _laptop.gameObject.AddComponent<ExfilLaptop>().zone = this;
        }

        /// <summary>
        /// Put the laptop crate somewhere clear at the edge of the zone - not inside a sandbag wall, a wreck or a crate -
        /// standing on the ground and facing the middle of the zone.
        /// </summary>
        void PlaceLaptop()
        {
            if (_laptop == null) return;
            Physics.SyncTransforms();
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            var hits = new Collider[12];
            Vector3 c = transform.position;
            foreach (float r in new[] { 1.1f, 1.6f, 0.7f, 2.2f })
                for (int k = 0; k < 12; k++)
                {
                    float a = k * 30f * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    Vector3 p = c + dir * r;
                    if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var g, 3f, mask, QueryTriggerInteraction.Ignore) || g.normal.y < 0.8f || g.collider.transform.IsChildOf(_laptop)) continue;
                    if (Mathf.Abs(g.point.y - c.y) > 0.6f) continue;
                    var rot = Quaternion.LookRotation(dir, Vector3.up);   // the screen side (local -Z) faces the middle
                    int n = Physics.OverlapBoxNonAlloc(g.point + Vector3.up * 0.34f, new Vector3(0.33f, 0.3f, 0.26f), hits, rot, mask, QueryTriggerInteraction.Ignore);
                    bool blocked = false;
                    for (int i = 0; i < n; i++) if (!hits[i].transform.IsChildOf(_laptop) && !hits[i].transform.IsChildOf(transform)) { blocked = true; break; }
                    if (blocked) continue;
                    // room to stand at it, on the zone side
                    n = Physics.OverlapBoxNonAlloc(g.point - dir * 0.7f + Vector3.up * 1.0f, new Vector3(0.25f, 0.8f, 0.25f), hits, rot, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++) if (!hits[i].transform.IsChildOf(_laptop) && !hits[i].transform.IsChildOf(transform)) { blocked = true; break; }
                    if (blocked) continue;
                    _laptop.SetPositionAndRotation(g.point, rot);
                    return;
                }
            // nowhere perfect (a steep or cluttered spot): still put it down beside the zone, on the ground, so every
            // extract always has its laptop
            foreach (float r in new[] { 1.2f, 1.8f, 0.8f })
                for (int k = 0; k < 12; k++)
                {
                    float a = k * 30f * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    Vector3 p = c + dir * r;
                    if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var g, 8f, mask, QueryTriggerInteraction.Ignore) || g.normal.y < 0.5f || g.collider.transform.IsChildOf(_laptop)) continue;
                    _laptop.SetPositionAndRotation(g.point, Quaternion.LookRotation(dir, Vector3.up));
                    return;
                }
            _laptop.position = c + _laptop.forward * 1.1f;   // last resort: right next to the flare
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

        void Start() { SnapToGround(); PlaceLaptop(); }

        /// <summary>Sit the zone on the real ground (on a slope its spot could be under the hill or in the air, and then
        /// the laptop had nowhere to go - that's how Logging Road ended up with no exfil).</summary>
        void SnapToGround()
        {
            Physics.SyncTransforms();
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            var p = transform.position;
            var hits = Physics.RaycastAll(p + Vector3.up * 40f, Vector3.down, 120f, mask, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform) || h.normal.y < 0.5f) continue;
                // the ground surface nearest to where the zone was meant to be
                if (float.IsNaN(best) || Mathf.Abs(h.point.y - p.y) < Mathf.Abs(best - p.y)) best = h.point.y;
            }
            if (!float.IsNaN(best) && Mathf.Abs(best - p.y) > 0.05f) transform.position = new Vector3(p.x, best + 0.02f, p.z);
        }

        Transform _touchA, _touchB;   // the CALL buttons: touching one calls the exfil
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
            // VR: just touch the CALL button (or the screen's button) with either hand - no trigger needed
            if (!_calling && XRInputs.VR && inside)
                foreach (var h in new[] { p.HandL, p.HandR })
                    if (h != null && ((_touchA != null && Vector3.Distance(h.TrackedPosition, _touchA.position) < 0.07f) || (_touchB != null && Vector3.Distance(h.TrackedPosition, _touchB.position) < 0.07f)))
                    { h.Haptic(0.5f, 0.08f); Call(); break; }
            // desktop: look at the laptop and press F
            if (!_calling && !XRInputs.VR && Desktop.Interact && Physics.Raycast(p.Head.position, p.Head.forward, out var hit, 3f, ~0, QueryTriggerInteraction.Collide) && hit.collider.transform.IsChildOf(_laptop)) Call();
            if (_calling)
            {
                if (!inside)
                {
                    _calling = false; _t = 0;
                    Sfx.UI(Synth.UiError(), 0.4f);
                    p.Hud.Center("EXFIL ABORTED\n<size=40>you left the zone</size>", 2f);
                    SetScreen("SIGNAL LOST", "TOUCH CALL AGAIN");
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
                SetScreen(inside ? "READY" : "STANDBY", inside ? (XRInputs.VR ? "TOUCH CALL TO EXFIL" : "PRESS F TO CALL EXFIL") : "ENTER THE ZONE");
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
    /// Vault: open it with a Vault Keycard (bring the card to the reader). Keycards turn up in safes and loot.
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

        /// <summary>
        /// The vault alarm on its own speaker (the shared sound pool used to hand its speaker to the next gunshot, cutting
        /// it off). In the Underground it's heard everywhere: loud, long range and partly "in your head" through the
        /// tunnels; elsewhere it carries a long way round the vault.
        /// </summary>
        void StartAlarm()
        {
            bool under = _map != null && _map.Id == "underground";
            if (_alarm == null)
            {
                var go = new GameObject("VaultAlarm");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 2.5f;
                _alarm = go.AddComponent<AudioSource>();
                _alarm.playOnAwake = false;
            }
            _alarm.clip = Synth.Alarm(); _alarm.loop = true;
            _alarm.volume = (under ? 1f : 0.8f) * Sfx.Effects;
            _alarm.spatialBlend = under ? 0.55f : 1f;   // underground: you hear it wherever you are, louder the closer you get
            _alarm.rolloffMode = AudioRolloffMode.Linear;
            _alarm.minDistance = under ? 20f : 8f; _alarm.maxDistance = under ? 400f : 160f;
            _alarm.Play();
        }

        /// <summary>Builds a 6x5m vault room. pos = centre of the doorway, the room extends along +Z (local).</summary>
        /// <param name="width">inside width; a vault built across a room uses the room's width</param>
        /// <param name="sides">false: the room's own side walls are the vault's sides</param>
        /// <param name="back">false: the room's own back wall is the vault's back</param>
        public static Vault Create(MapBase map, int id, Vector3 pos, float yaw, bool attached = false, Material wallMat = null, Color? wallTint = null, float height = 2.9f, bool ceiling = true,
                                   float width = 6f, float thick = 0.4f, bool sides = true, bool back = true)
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
            // room walls: concrete on their own, or the host building's own wall material so the vault matches the room
            var wm = wallMat != null ? wallMat : conc;
            Color wt = wallTint ?? new Color(0.7f, 0.7f, 0.72f);
            float z0 = attached ? 0.13f : 0f;   // attached: start behind the building's wall (its outer face)
            float hw = width * 0.5f;
            if (sides) { B.Wall(P(-hw, 0, z0), P(-hw, 0, 5), height, thick, wm, wt); B.Wall(P(hw, 0, 5), P(hw, 0, z0), height, thick, wm, wt); }
            if (back) B.Wall(P(-hw, 0, 5), P(hw, 0, 5), height, thick, wm, wt);
            float face;   // where the front of the vault is (local z)
            if (attached) face = -0.125f;   // the building's wall, with the doorway already in it, is the front
            else
            {
                // front wall in the same material as the room, the full height, with the doorway
                bool was = B.NoSwingDoors;
                B.NoSwingDoors = true;
                B.Wall(P(-hw, 0, 0), P(hw, 0, 0), height, thick, wm, wt, new List<Vector4> { new Vector4(hw, 1.8f, 0f, 2.3f) });
                B.NoSwingDoors = was;   // (it used to switch door-registration back on for everything built after a vault)
                face = -thick * 0.5f;
            }
            // steel frame around the round door
            var frameCol = new Color(0.36f, 0.38f, 0.4f);
            B.Box(P(-0.96f, 1.21f, face - 0.03f), new Vector3(0.12f, 2.42f, 0.06f), steel, frameCol, q, false);
            B.Box(P(0.96f, 1.21f, face - 0.03f), new Vector3(0.12f, 2.42f, 0.06f), steel, frameCol, q, false);
            B.Box(P(0, 2.36f, face - 0.03f), new Vector3(2.04f, 0.12f, 0.06f), steel, frameCol, q, false);
            {
                var haz = Mats.Lit("vault_hazard", Color.white, 0.1f, 0.4f, Mats.Noise, null, 0.35f, 0.3f);
                foreach (float sx in new[] { -1f, 1f })   // hazard-striped posts on the wall either side of the frame (the door covers the frame itself)
                    for (int k = 0; k < 8; k++)
                        B.Box(P(sx * 1.14f, 0.15f + k * 0.29f, face - 0.012f), new Vector3(0.16f, 0.145f, 0.02f), haz, k % 2 == 0 ? new Color(0.9f, 0.7f, 0.1f) : new Color(0.08f, 0.08f, 0.08f), q, false);
                B.Cyl(P(0, 2.46f, face - 0.08f), P(0, 2.62f, face - 0.08f), 0.075f, 0.065f, 12, Mats.Lit("vault_beacon", new Color(0.9f, 0.1f, 0.06f), 0f, 0.8f, null, null, 0f, 0f, false, 0.5f, new Color(0.8f, 0.05f, 0.02f)), Color.white, false);
                B.Cyl(P(0, 2.42f, face - 0.08f), P(0, 2.46f, face - 0.08f), 0.09f, 0.09f, 12, steel, new Color(0.25f, 0.25f, 0.26f), false);
            }
            if (ceiling) B.Box(P(0, height + 0.07f, 2.5f + z0 * 0.5f), new Vector3(width + 0.4f, 0.14f, 5.4f - z0), conc, new Color(0.6f, 0.6f, 0.62f), q);
            B.Box(P(0, 0.06f, 2.5f + z0 * 0.5f), new Vector3(width - 0.4f, 0.12f, 4.6f - z0), Mats.Lit("vault_floor2", Color.white, 0.75f, 0.55f, Mats.Metal, Mats.MetalN, 0.3f, 0.35f, true, 1.5f), new Color(0.4f, 0.42f, 0.45f), q);   // steel deck
            B.AddLight(P(0, 2.6f, 2.5f), new Color(0.85f, 0.92f, 1f), 7f, 1.35f);   // cold strip light
            B.Box(P(0, height - 0.04f, 2.5f), new Vector3(0.25f, 0.05f, 2.2f), Mats.Lit("vault_strip", new Color(0.9f, 0.95f, 1f), 0f, 0.6f, null, null, 0f, 0f, false, 0.5f, new Color(1.6f, 1.75f, 1.9f)), Color.white, q, false);
            B.Box(P(hw - 0.35f, height - 0.3f, 4.6f), new Vector3(0.14f, 0.1f, 0.08f), Mats.Lit("vault_redlamp", new Color(0.9f, 0.12f, 0.08f), 0f, 0.7f, null, null, 0f, 0f, false, 0.5f, new Color(0.9f, 0.08f, 0.04f)), Color.white, q, false);
            // safe-deposit boxes lining both side walls, above the tables: rows of little steel doors with keyholes and number plates
            {
                var dep = Mats.Lit("vault_deposit", Color.white, 0.45f, 0.55f, Mats.Metal, null, 0.2f, 0.3f, true, 2f);
                float yTop = Mathf.Min(height - 0.25f, 2.55f), bw = 0.3f, bh = 0.26f;
                foreach (float sx in new[] { -1f, 1f })
                {
                    float xf = sx * (hw - thick * 0.5f - 0.06f);
                    B.Box(P(sx * (hw - thick * 0.5f - 0.03f), (1.05f + yTop) * 0.5f, 2.3f + z0 * 0.5f), new Vector3(0.06f, yTop - 1.05f, 3.6f - z0), dep, new Color(0.28f, 0.29f, 0.31f), q, false);   // the bank's back plate
                    int k = 0;
                    for (float y = 1.05f + bh * 0.5f; y < yTop - bh * 0.45f; y += bh)
                        for (float z = 0.55f + z0 + bw * 0.5f; z < 4.1f; z += bw, k++)
                        {
                            B.Box(P(xf, y, z), new Vector3(0.02f, bh - 0.018f, bw - 0.018f), dep, k % 3 == 0 ? new Color(0.55f, 0.56f, 0.58f) : new Color(0.62f, 0.63f, 0.65f), q, false);   // a box door
                            B.Box(P(xf - sx * 0.012f, y + 0.05f, z), new Vector3(0.004f, 0.03f, 0.08f), dep, new Color(0.8f, 0.68f, 0.35f), q, false);   // brass number plate
                            B.Cyl(P(xf - sx * 0.01f, y - 0.04f, z), P(xf - sx * 0.02f, y - 0.04f, z), 0.012f, 0.012f, 8, dep, new Color(0.2f, 0.2f, 0.2f), false);   // keyhole
                        }
                }
            }
            // door: a thick square steel slab that fills the frame exactly, with a locking wheel; swings open on the left
            v._door = new GameObject("DoorHinge").transform;
            v._door.SetParent(go.transform, false);
            v._door.localPosition = new Vector3(-0.95f, 1.15f, face - 0.06f);
            var dm = new MB(2);
            dm.Sub(0).Box(new Vector3(0.95f, 0.003f, -0.175f), new Vector3(1.86f, 2.3f, 0.35f), Color.white, 0.02f, 2);
            dm.Sub(0).Box(new Vector3(0.95f, 0.003f, -0.355f), new Vector3(1.5f, 1.95f, 0.02f), new Color(0.85f, 0.85f, 0.85f), 0.01f);   // raised inner panel
            foreach (float bx in new[] { 0.16f, 1.74f }) for (int k = 0; k < 4; k++)                                                          // bolt heads round the edge
                dm.Sub(1).Cyl(new Vector3(bx, -0.9f + k * 0.6f, -0.35f), new Vector3(bx, -0.9f + k * 0.6f, -0.375f), 0.03f, 0.03f, 10, Color.white);
            for (int h = 0; h < 3; h++) dm.Sub(1).Box(new Vector3(1.86f, -0.7f + h * 0.7f, -0.17f), new Vector3(0.06f, 0.18f, 0.3f), Color.white, 0.01f);   // locking bars on the opening edge
            // the classic bank-vault face (3.42): a thick round door embossed on the slab, ringed with radial locking bolts,
            // a five-spoke handwheel with grips on a big hub, and a time-lock box with three dials
            dm.Sub(0).Cyl(new Vector3(0.95f, 0.02f, -0.355f), new Vector3(0.95f, 0.02f, -0.4f), 0.86f, 0.84f, 40, new Color(0.92f, 0.92f, 0.92f));   // the round door
            dm.Sub(1).Tube(new Vector3(0.95f, 0.02f, -0.4f), new Vector3(0.95f, 0.02f, -0.415f), 0.86f, 0.78f, 40, Color.white);                     // its polished rim
            dm.Sub(0).Cyl(new Vector3(0.95f, 0.02f, -0.4f), new Vector3(0.95f, 0.02f, -0.41f), 0.62f, 0.62f, 32, new Color(0.8f, 0.8f, 0.8f));       // inner step
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f; var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var c0 = new Vector3(0.95f, 0.02f, -0.385f) + dir * 0.7f;
                dm.Sub(1).Cyl(c0, c0 + dir * 0.13f, 0.035f, 0.035f, 10, Color.white);                                                           // locking bolt
            }
            dm.Sub(1).Cyl(new Vector3(0.95f, 0.02f, -0.41f), new Vector3(0.95f, 0.02f, -0.5f), 0.12f, 0.1f, 20, Color.white);                       // hub
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.3f; var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var h0 = new Vector3(0.95f, 0.02f, -0.47f);
                dm.Sub(1).Cyl(h0 + dir * 0.08f, h0 + dir * 0.42f, 0.022f, 0.02f, 8, Color.white);                                               // spoke
                dm.Sub(0).Capsule(h0 + dir * 0.42f, h0 + dir * 0.42f + new Vector3(0, 0, -0.1f), 0.03f, 8, new Color(0.12f, 0.12f, 0.12f));       // grip
            }
            dm.Sub(1).Tube(new Vector3(0.95f, 0.02f, -0.46f), new Vector3(0.95f, 0.02f, -0.48f), 0.44f, 0.4f, 32, Color.white);                     // wheel rim
            dm.Sub(0).Box(new Vector3(0.95f, 0.88f, -0.4f), new Vector3(0.5f, 0.2f, 0.08f), new Color(0.2f, 0.2f, 0.22f), 0.015f, 1);              // time lock
            for (int i = -1; i <= 1; i++)
            {
                dm.Sub(1).Cyl(new Vector3(0.95f + i * 0.14f, 0.88f, -0.44f), new Vector3(0.95f + i * 0.14f, 0.88f, -0.45f), 0.05f, 0.05f, 14, Color.white);
                dm.Sub(0).Box(new Vector3(0.95f + i * 0.14f, 0.9f, -0.452f), new Vector3(0.006f, 0.04f, 0.002f), new Color(0.1f, 0.1f, 0.1f), 0f);   // dial needle
            }
            var door = dm.Build("Door", v._door, Mats.Lit("vault_door2", new Color(0.66f, 0.68f, 0.72f), 0.55f, 0.55f, Mats.Metal, Mats.MetalN, 0.2f, 0.4f), Mats.Lit("vault_brass", new Color(0.85f, 0.8f, 0.7f), 0.7f, 0.7f, Mats.Metal, null, 0.1f, 0.25f));
            var dc = door.AddComponent<BoxCollider>(); dc.center = new Vector3(0.95f, 0, -0.175f); dc.size = new Vector3(1.86f, 2.3f, 0.36f);
            door.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            // card reader outside, right of the door (keycard only - no hacking)
            var reader = new GameObject("Reader").AddComponent<KeycardReader>();
            reader.transform.SetParent(go.transform, false);
            reader.transform.localPosition = new Vector3(1.6f, 1.3f, face - 0.1f);
            reader.vault = v; reader.Build();
            v._status = UI.Text(go.transform, "VAULT LOCKED", new Vector3(0, 2.6f, face - 0.1f), 0.07f, new Color(1f, 0.3f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
            v._status.transform.localRotation = Quaternion.Euler(0, 0, 0);
            // racks inside
            var rackMat = Mats.Lit("vault_rack", Color.white, 0.4f, 0.45f, Mats.Metal, null, 0.3f, 0.35f, true, 1f);
            B.Box(P(0, 1.0f, 4.6f), new Vector3(4.5f, 2f, 0.3f), rackMat, new Color(0.42f, 0.44f, 0.47f), q, true, Synth.Surface.Metal);
            for (int k = 0; k < 4; k++) B.Box(P(0, 0.35f + k * 0.45f, 4.43f), new Vector3(4.4f, 0.05f, 0.05f), rackMat, new Color(0.7f, 0.72f, 0.75f), q, false);   // rack bars
            for (int k = 0; k < 6; k++) B.Box(P(-1.9f + k * 0.76f, 1.0f, 4.43f), new Vector3(0.05f, 1.9f, 0.05f), rackMat, new Color(0.62f, 0.64f, 0.67f), q, false);
            v._rack = ItemContainer.Grid(go.transform, new Vector3(0, 1.0f, 4.35f), Quaternion.Euler(-90, 0, 0), 3, 1, new Vector2(1.3f, 0.5f), 1.15f, null, Quaternion.Euler(0, 0, 90));
            B.Table(P(-2.2f, 0, 2.5f), 90, new Vector2(1.6f, 0.8f), 0.9f);
            B.Table(P(2.2f, 0, 2.5f), 90, new Vector2(1.6f, 0.8f), 0.9f);
            v._shelf = ItemContainer.Grid(go.transform, new Vector3(0, 0.96f, 2.5f), Quaternion.identity, 2, 3, new Vector2(4.4f, 0.45f), 0.3f);
            foreach (var s in v._rack.slots) s.enabled = false;
            foreach (var s in v._shelf.slots) s.enabled = false;
            return v;
        }

        /// <summary>The card that opens it: the Vault Keycard, or the North Pole Keycard for the giant present.</summary>
        public string CardId = "key_vault";
        public bool Present => CardId == "key_xmas";

        public void TryCard(Item card)
        {
            if (IsOpen || card == null || card.def == null || card.def.id != CardId) return;
            // used up: gone for everyone (it used to drop to the floor for the other players first - a free copy)
            card.Consumed = true;
            if (card.netId != 0) NetGame.I?.OnItemTaken(card);
            card.primaryHand?.ForceRelease();
            if (card.slot != null) card.slot.Release(card);
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
            bool under = _map != null && _map.Id == "underground";
            Sfx.Play(Synth.VaultDoor(), transform.position, 1f, 1f, under ? 15f : 3f, under ? 300f : 60f);
            StartAlarm();
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
            if (Present)
            {
                // the giant present: every rack slot gets a vault gun (fully loaded), and the shelves fill with the best there is
                int extra = 0;
                while (r < _rack.slots.Count)
                {
                    var g2 = rng.Pick(vaultGuns);
                    var slot = _rack.slots[r++];
                    var it = ItemFactory.Spawn(new ItemData(g2.id) { rounds = g2.magSize }, slot.transform.position, slot.transform.rotation);
                    if (it != null) { it.netId = 47000 + Id * 16 + extra; NetGame.I?.Register(it); if (slot.Accepts(it)) slot.Store(it, false); }   // own id range: never clashes with the normal vault ids
                    extra++;
                }
                gameObject.AddComponent<PresentVaultUnlock>().vault = this;
            }
            for (int i = 0; i < (Present ? 6 : 3) && sh < _shelf.slots.Count; i++)
            {
                var d = Db.Roll(Db.LootTable.Vault, rng);
                if (d == null || d is GunDef g3 && g3.TwoHanded) continue;
                var slot = _shelf.slots[sh++];
                var it = ItemFactory.Spawn(new ItemData(d.id), slot.transform.position, slot.transform.rotation);
                if (it == null) continue;
                it.netId = Present ? 48000 + Id * 16 + i : 40000 + Id * 32 + 20 + i;
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
            StartAlarm();
            _alarmT = 40f;
            NpcDirector.I?.AlertAll(transform.position, 90f);
        }
    }

    /// <summary>
    /// The giant present vault on the North Pole: the first time you step inside it (once it's open), its four skins
    /// (Aurora, Gift Wrap, Gingerbread, North Star) unlock at your paint booth, with a big NEW SKINS UNLOCKED in front of you.
    /// </summary>
    public class PresentVaultUnlock : MonoBehaviour
    {
        public Vault vault;
        bool _done;
        void Update()
        {
            if (_done || vault == null || !vault.IsOpen) return;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null || pl.Dead) return;
            var lp = vault.transform.InverseTransformPoint(pl.Head.position);
            if (Mathf.Abs(lp.x) > 3.2f || lp.z < 0.2f || lp.z > 5.4f || lp.y < 0f || lp.y > 4f) return;
            _done = true;
            var prof = Profile.Current;
            if (prof == null) return;
            prof.dlc ??= new List<string>();
            bool fresh = !prof.dlc.Contains("northpole");
            if (fresh) { prof.dlc.Add("northpole"); prof.Save(); }
            Sfx.UI(Synth.QueueFound(), 0.8f);
            pl.Hud.Center(fresh ? "NEW SKINS UNLOCKED\n<size=34>Aurora - Gift Wrap - Gingerbread - North Star\nat your paint booth</size>" : "THE GIANT PRESENT\n<size=34>(you already have its skins)</size>", 5f);
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
                if (it == null || !it.Held || it.def == null || it.def.id != vault.CardId) continue;
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
