using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public static class Maps
    {
        public static readonly string[] RaidIds = { "dockyard", "pinewood", "oldtown", "underground" };
        public static string Name(string id) => id == "welcome" ? "Rago Extract" : id == "dockyard" ? "Dockyard" : id == "pinewood" ? "Pinewood Outpost" : id == "oldtown" ? "Old Town" : id == "underground" ? "Underground Sector" : id;
        public static string Blurb(string id) => id == "dockyard" ? "Container yard at dusk. Warehouses, cranes and the Harbor Office vault."
            : id == "pinewood" ? "Foggy forest and a military outpost with a bunker vault."
            : id == "underground" ? "A sealed underground research sector. Tight corridors, 6 operators max, squads of 2."
            : "Abandoned town at dawn. Streets, apartments and the old bank vault.";

        /// <summary>A seed that is the same for a map name on every machine and every run (string hashes aren't).</summary>
        public static int StableSeed(string id)
        {
            unchecked { uint h = 2166136261u; foreach (char c in id) { h ^= c; h *= 16777619u; } return (int)(h & 0x7fffffff); }
        }

        public static MapBase Create(string id, Transform parent, int seed)
        {
            var go = new GameObject("Map_" + id);
            go.transform.SetParent(parent, false);
            MapBase m;
            switch (id)
            {
                case "bunker": m = go.AddComponent<BunkerMap>(); break;
                case "market": m = go.AddComponent<MarketMap>(); break;
                case "welcome": m = go.AddComponent<WelcomeMap>(); break;
                case "pinewood": m = go.AddComponent<PinewoodMap>(); break;
                case "underground": m = go.AddComponent<UndergroundMap>(); break;
                case "oldtown": m = go.AddComponent<OldTownMap>(); break;
                default: m = go.AddComponent<DockyardMap>(); break;
            }
            m.Id = id;
            m.DisplayName = Name(id);
            // raid maps: the same layout every time (learn the map), but the loot is placed fresh from this raid's seed
            bool raid = System.Array.IndexOf(RaidIds, id) >= 0;
            m.Init(raid ? StableSeed(id) : seed);
            m.LootSeed = seed;
            m.Build();
            m.FinalizeLoot();
            m.AddSwingDoors();
            m.B.Finish();
            try { m.SettleCrates(); } catch (System.Exception e) { Debug.LogWarning("[Rago] crate settle skipped: " + e.Message); }
            return m;
        }

        // shared materials
        public static Material Concrete => Mats.Lit("w_concrete", Color.white, 0f, 0.25f, Mats.Concrete, null, 0.45f, 0.1f, true, 0.35f);
        public static Material ConcreteDark => Mats.Lit("w_concrete_dark", new Color(0.6f, 0.6f, 0.6f), 0f, 0.3f, Mats.Concrete, null, 0.55f, 0.1f, true, 0.25f);
        public static Material Asphalt => Mats.Lit("w_asphalt", Color.white, 0f, 0.35f, Mats.Asphalt, null, 0.3f, 0.05f, true, 0.3f);
        public static Material Brick => Mats.Lit("w_brick", Color.white, 0f, 0.2f, Mats.Brick, null, 0.45f, 0.05f, true, 0.4f);
        public static Material Plaster => Mats.Lit("w_plaster", Color.white, 0f, 0.15f, Mats.Plaster, null, 0.5f, 0.05f, true, 0.3f);
        public static Material WoodPlank => Mats.Lit("w_wood", Color.white, 0f, 0.25f, Mats.Wood, null, 0.4f, 0.1f, true, 0.6f);
        public static Material Metal => Mats.Lit("w_metal", Color.white, 0.7f, 0.4f, Mats.Metal, null, 0.45f, 0.4f, true, 0.6f);
        public static Material Corrugated => Mats.Lit("w_corr", Color.white, 0.5f, 0.35f, Mats.Corrugated, null, 0.5f, 0.3f, true, 0.6f);
        public static Material Tiles => Mats.Lit("w_tiles", Color.white, 0.1f, 0.6f, Mats.Tiles, null, 0.4f, 0.1f, true, 0.5f);
        public static Material GroundGrass => Mats.Lit("w_grass", Color.white, 0f, 0.12f, Mats.Grass, null, 0.3f, 0f, true, 0.25f);
        public static Material GroundDirt => Mats.Lit("w_dirt", Color.white, 0f, 0.15f, Mats.Ground, null, 0.3f, 0f, true, 0.3f);
    }

    // =============================================================================================== DOCKYARD
    public class DockyardMap : MapBase
    {
        public override void Build()
        {
            Ambience = Synth.Amb.Harbor;
            NavBounds = new Bounds(new Vector3(-45, 5, 0), new Vector3(290, 40, 190));
            Lighting(new Color(1f, 0.62f, 0.38f), 1.05f, new Vector3(12, 245, 0), new Color(0.45f, 0.45f, 0.55f), new Color(0.55f, 0.42f, 0.36f), new Color(0.2f, 0.17f, 0.15f),
                new Color(0.55f, 0.43f, 0.38f), 0.0065f, new Color(0.2f, 0.25f, 0.42f), new Color(0.95f, 0.55f, 0.32f), 0.45f);
            var b = B;
            // ground: concrete apron + asphalt roads
            b.Ground(new Vector3(-10, 0, 0), new Vector2(160, 180), Maps.ConcreteDark, Synth.Surface.Concrete);
            b.Box(new Vector3(-10, 0.01f, -20), new Vector3(160, 0.02f, 9), Maps.Asphalt, Color.white, null, false);
            b.Box(new Vector3(-30, 0.01f, 0), new Vector3(9, 0.02f, 180), Maps.Asphalt, Color.white, null, false);
            for (int i = -6; i < 7; i++) b.Box(new Vector3(-10 + i * 12, 0.025f, -20), new Vector3(4, 0.01f, 0.18f), Mats.Lit("roadpaint", new Color(0.9f, 0.85f, 0.7f), 0, 0.4f), Color.white, null, false);
            // sea + quay wall + piers
            b.Water(new Vector3(95, -1.6f, 0), new Vector2(60, 200));
            b.Box(new Vector3(70.5f, -1.5f, 0), new Vector3(1, 3, 180), Maps.Concrete, new Color(0.6f, 0.6f, 0.6f));
            for (int i = 0; i < 12; i++) b.Cyl(new Vector3(70, 0, -85 + i * 15), new Vector3(70, 0.6f, -85 + i * 15), 0.25f, 0.2f, 10, Maps.Metal, new Color(0.25f, 0.25f, 0.25f)); // bollards
            foreach (float z in new[] { -55f, 35f })
            {
                b.Box(new Vector3(85, -0.2f, z), new Vector3(30, 0.4f, 6), Maps.WoodPlank, new Color(0.55f, 0.45f, 0.35f), null, true, Synth.Surface.Wood);
                for (int i = 0; i < 6; i++) { b.Cyl(new Vector3(72 + i * 5.5f, -3, z - 3), new Vector3(72 + i * 5.5f, 0, z - 3), 0.2f, 0.2f, 8, Maps.WoodPlank, new Color(0.35f, 0.28f, 0.2f)); b.Cyl(new Vector3(72 + i * 5.5f, -3, z + 3), new Vector3(72 + i * 5.5f, 0, z + 3), 0.2f, 0.2f, 8, Maps.WoodPlank, new Color(0.35f, 0.28f, 0.2f)); }
            }
            // boat at the pier (extract)
            BoatHull(new Vector3(92, -1.2f, -62), 0);
            // container yard
            Color[] cc = { new Color(0.55f, 0.15f, 0.1f), new Color(0.15f, 0.3f, 0.55f), new Color(0.2f, 0.45f, 0.25f), new Color(0.7f, 0.5f, 0.15f), new Color(0.45f, 0.45f, 0.45f), new Color(0.6f, 0.3f, 0.12f), new Color(0.12f, 0.35f, 0.4f) };
            var interior = new List<Vector3>();
            for (int gx = 0; gx < 6; gx++)
                for (int gz = 0; gz < 7; gz++)
                {
                    float x = 8 + gx * 8.5f, z = -5 + gz * 11f;
                    if (b.Rng.Chance(0.18f)) continue;
                    int h = b.Rng.Range(1, 4);
                    for (int k = 0; k < h; k++)
                    {
                        bool open = k == 0 && b.Rng.Chance(0.25f);
                        b.ShippingContainer(new Vector3(x + b.Rng.Range(-0.3f, 0.3f), k * 2.6f, z), b.Rng.Range(-2f, 2f), b.Rng.Pick(cc) * b.Rng.Range(0.8f, 1.1f), open);
                        if (open) interior.Add(new Vector3(x, 0.12f, z + 1));
                    }
                }
            // gantry crane over the yard
            Crane(new Vector3(30, 0, 30));
            Crane(new Vector3(30, 0, -50));
            // warehouses
            var wh1 = Warehouse(new Vector3(-55, 0, 45), 0, new Vector2(30, 22));
            var wh2 = Warehouse(new Vector3(-55, 0, -60), 0, new Vector2(26, 20));
            interior.AddRange(wh1); interior.AddRange(wh2);
            // harbor office + vault
            var office = b.Building(new Vector3(-20, 0, -55), 0, new Vector2(14, 10), 3, Maps.Plaster, new Color(0.75f, 0.72f, 0.62f), Maps.Tiles, Maps.Concrete, 3, true, true, 3.2f, 2);
            interior.AddRange(office);
            b.Sign(new Vector3(-20, 9.2f, -60.2f), Quaternion.identity, "HARBOR OFFICE", new Color(0.9f, 0.85f, 0.7f), 0.5f);
            AddVault(new Vector3(-20, 0, -50f), 0, true, Maps.Plaster, new Color(0.75f, 0.72f, 0.62f), 3.2f, true, 6f, 0.25f);   // an annex in the office's own plaster; its door is the doorway in the office's back wall
            Boss(new Vector3(-20, 0.1f, -56.5f), "The Harbormaster");
            // fences + barriers + props
            // scattered props: only where they fit (not inside the warehouses, the office, the vault or the container stacks)
            for (int i = 0; i < 14; i++) { float yw = b.Rng.Range(0, 180f); if (b.Spot(() => new Vector3(b.Rng.Range(-75f, 60f), 0, b.Rng.Range(-80f, 80f)), new Vector3(0.8f, 0.9f, 3.1f), yw, out var sp)) b.JerseyBarrier(sp, yw); }
            var barrelCols = new[] { new Color(0.15f, 0.3f, 0.6f), new Color(0.6f, 0.15f, 0.1f), new Color(0.2f, 0.35f, 0.2f) };
            for (int i = 0; i < 30; i++) if (b.Spot(() => new Vector3(b.Rng.Range(-75f, 65f), 0, b.Rng.Range(-80f, 80f)), new Vector3(0.7f, 0.95f, 0.7f), 0, out var sp)) b.Barrel(sp, b.Rng.Pick(barrelCols));
            for (int i = 0; i < 22; i++) { float yw = b.Rng.Range(0, 90f); if (b.Spot(() => new Vector3(b.Rng.Range(-75f, 65f), 0, b.Rng.Range(-80f, 80f)), new Vector3(1.25f, 1.0f, 1.25f), yw, out var sp)) b.Crate(sp, yw, new Vector3(1.2f, 1.0f, 1.2f)); }
            for (int i = 0; i < 6; i++) { float yw = b.Rng.Range(0, 360f); if (b.Spot(() => new Vector3(b.Rng.Range(-70f, -10f), 0, b.Rng.Range(-80f, 80f)), new Vector3(2.1f, 1.4f, 4.5f), yw, out var sp)) b.CarWreck(sp, yw, b.Rng.Pick(cc)); }
            for (int i = -3; i <= 3; i++) b.LightPole(new Vector3(-34, 0, i * 25), 8, new Color(1f, 0.6f, 0.25f), i % 2 == 0);
            for (int i = 0; i < 4; i++) b.LightPole(new Vector3(66, 0, -70 + i * 45), 8, new Color(1f, 0.6f, 0.25f), true);
            // loot
            ScatterLoot(interior, 0.75f);
            AddCrate(new Vector3(88, 0.0f, -55), 0, LootCrate.Kind.WeaponCase);
            AddCrate(new Vector3(-68, 0.12f, 50), 90, LootCrate.Kind.Safe);
            // extracts
            AddExtract(new Vector3(95, 0, -55), "Boat Pier");
            AddExtract(new Vector3(-30, 0, 86), "North Gate");
            AddExtract(new Vector3(-82, 0, -30), "Rail Yard");
            // spawns
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                Spawn(new Vector3(Mathf.Clamp(Mathf.Cos(a) * 75f, -80, 62), 0.1f, Mathf.Sin(a) * 80f), Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg);
            }
            // squads
            Squad(new Vector3(25, 0, 20), 3); Squad(new Vector3(40, 0, -30), 2); Squad(new Vector3(-55, 0, 45), 3, true);
            Squad(new Vector3(-55, 0, -60), 2); Squad(new Vector3(-20, 0, -48), 3, true); Squad(new Vector3(60, 0, 60), 2); Squad(new Vector3(-40, 0, 0), 2);
            // ---- Workers' Village (west): a street of houses you can search room by room
            b.Ground(new Vector3(-137.5f, 0, 0), new Vector2(95, 180), Maps.GroundGrass, Synth.Surface.Grass, -0.01f);
            var village = Neighborhood(new Vector3(-88, 0, 0), 180, 6, 13.5f, false);
            ScatterLoot(village, 0.85f);
            for (int i = 0; i < 26; i++) { var tp = new Vector3(b.Rng.Range(-180f, -90f), 0, b.Rng.Chance(0.5f) ? b.Rng.Range(28f, 55f) : b.Rng.Range(-55f, -28f)); b.Pine(tp, b.Rng.Range(7f, 11f)); }
            AddExtract(new Vector3(-176, 0, 0), "Village Road");
            Squad(new Vector3(-120, 0, 8), 3); Squad(new Vector3(-150, 0, -10), 2); Squad(new Vector3(-105, 0, -12), 2, true);
            Spawn(new Vector3(-178, 0.1f, 22), 90);
            // ---- the open country around the docks: 1 km x 1 km
            Outskirts(new Rect(-185, -90, 255, 180), 500, new[] { "Lighthouse Road", "Old Quarry", "Ferry Landing" }, false, true, Maps.GroundGrass, 9);
            NavBounds = new Bounds(new Vector3(-57, 5, 0), new Vector3(260, 40, 190));
        }

        List<Vector3> Warehouse(Vector3 pos, float yaw, Vector2 size)
        {
            var b = B;
            var pts = new List<Vector3>();
            float hx = size.x / 2, hz = size.y / 2, h = 7f;
            var ops = new List<Vector4> { new Vector4(size.x * 0.5f, 5f, 0, 4.5f) };
            b.Wall(pos + new Vector3(-hx, 0, -hz), pos + new Vector3(hx, 0, -hz), h, 0.3f, Maps.Corrugated, new Color(0.55f, 0.58f, 0.6f), ops, Synth.Surface.Metal);
            b.Wall(pos + new Vector3(hx, 0, -hz), pos + new Vector3(hx, 0, hz), h, 0.3f, Maps.Corrugated, new Color(0.55f, 0.58f, 0.6f), new List<Vector4> { new Vector4(size.y * 0.5f, 1.8f, 0, 2.4f) }, Synth.Surface.Metal);
            b.Wall(pos + new Vector3(hx, 0, hz), pos + new Vector3(-hx, 0, hz), h, 0.3f, Maps.Corrugated, new Color(0.55f, 0.58f, 0.6f), ops, Synth.Surface.Metal);
            b.Wall(pos + new Vector3(-hx, 0, hz), pos + new Vector3(-hx, 0, -hz), h, 0.3f, Maps.Corrugated, new Color(0.55f, 0.58f, 0.6f), null, Synth.Surface.Metal);
            b.Box(pos + new Vector3(0, h, 0), new Vector3(size.x + 0.6f, 0.2f, size.y + 0.6f), Maps.Corrugated, new Color(0.4f, 0.42f, 0.44f), null, true, Synth.Surface.Metal);
            for (int i = 0; i < 5; i++) b.Box(pos + new Vector3(-hx + (i + 0.5f) * size.x / 5, h - 0.4f, 0), new Vector3(0.25f, 0.6f, size.y), Maps.Metal, new Color(0.3f, 0.3f, 0.3f), null, false);
            b.Box(pos + new Vector3(0, 0.02f, 0), new Vector3(size.x, 0.04f, size.y), Maps.Concrete, new Color(0.7f, 0.7f, 0.7f), null, false);
            for (int r = 0; r < 3; r++)
                for (int k = 0; k < 2; k++)
                {
                    var sp = pos + new Vector3(-hx + 5 + r * (size.x - 10) / 2, 0, -hz + 5 + k * (size.y - 10));
                    var at = b.Shelf(sp, 0, 4f);
                    if (at.HasValue) pts.Add(at.Value + new Vector3(0, 0.75f, 0));
                }
            for (int i = 0; i < 4; i++) pts.Add(pos + new Vector3(b.Rng.Range(-hx + 2, hx - 2), 0.05f, b.Rng.Range(-hz + 2, hz - 2)));
            b.AddLight(pos + new Vector3(0, h - 1.2f, 0), new Color(1f, 0.85f, 0.6f), 18f, 1.2f);
            b.Box(pos + new Vector3(0, h - 0.8f, 0), new Vector3(1.2f, 0.1f, 0.4f), Mats.Lit("lamp_on", new Color(1f, 0.95f, 0.8f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(2.2f, 2f, 1.6f)), Color.white, null, false);
            return pts;
        }

        void Crane(Vector3 pos)
        {
            var b = B;
            var m = Mats.Lit("crane", Color.white, 0.6f, 0.35f, Mats.Metal, null, 0.5f, 0.6f, true, 0.6f);
            Color c = new Color(0.8f, 0.55f, 0.12f);
            foreach (var sx in new[] { -22f, 22f })
                foreach (var sz in new[] { -4f, 4f })
                    b.Box(pos + new Vector3(sx, 9, sz), new Vector3(1, 18, 1), m, c, null, true, Synth.Surface.Metal);
            b.Box(pos + new Vector3(0, 18.5f, -4), new Vector3(46, 1.5f, 1.2f), m, c, null, true, Synth.Surface.Metal);
            b.Box(pos + new Vector3(0, 18.5f, 4), new Vector3(46, 1.5f, 1.2f), m, c, null, true, Synth.Surface.Metal);
            b.Box(pos + new Vector3(6, 17.5f, 0), new Vector3(4, 2, 9), m, c * 0.8f, null, false);
            for (int i = -2; i <= 2; i++) b.Box(pos + new Vector3(i * 9, 18.5f, 0), new Vector3(0.4f, 0.4f, 8), m, c, null, false);
            b.Cyl(pos + new Vector3(6, 6, 0), pos + new Vector3(6, 16.5f, 0), 0.04f, 0.04f, 4, m, new Color(0.15f, 0.15f, 0.15f), false);
        }

        void BoatHull(Vector3 pos, float yaw)
        {
            var b = B;
            var m = Mats.Lit("hull", Color.white, 0.4f, 0.4f, Mats.Metal, null, 0.6f, 0.5f, true, 0.6f);
            b.Box(pos + new Vector3(0, 0.5f, 0), new Vector3(5, 2, 14), m, new Color(0.15f, 0.2f, 0.3f), null, true, Synth.Surface.Metal, 0.3f);
            b.Box(pos + new Vector3(0, 1.6f, 0), new Vector3(4.6f, 0.2f, 13.4f), Maps.WoodPlank, new Color(0.6f, 0.5f, 0.38f), null, true, Synth.Surface.Wood);
            b.Box(pos + new Vector3(0, 2.8f, -3), new Vector3(3.5f, 2.2f, 4), m, new Color(0.85f, 0.85f, 0.82f), null, true, Synth.Surface.Metal, 0.1f);
        }
    }

    // =============================================================================================== PINEWOOD
    public class PinewoodMap : MapBase
    {
        float H(float x, float z)
        {
            float h = MathX.Fbm(x * 0.012f + 50, z * 0.012f + 50, 4, Seed % 97) * 16f - 5f;
            // flatten the outpost and cabin clearings to y = 0
            float outpost = Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), new Vector2(0, 10)) - 30f) / 16f);
            float cabins = Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), new Vector2(-48, -49)) - 18f) / 12f);
            float flat = Mathf.Min(outpost, cabins);
            // river valley
            float river = Mathf.Abs(z + 15 + Mathf.Sin(x * 0.04f) * 8f);
            float riverCut = Mathf.Clamp01(1 - river / 7f) * 3.2f;
            float north = Mathf.Clamp01((92f - z) / 14f);   // flattens into the hamlet's valley in the north
            float edge = Mathf.Clamp01((92f - Mathf.Abs(x)) / 14f) * Mathf.Clamp01((92f + z) / 14f); // and into the outskirts everywhere else
            return (h * north - riverCut) * flat * edge;   // the river valley stays out of the flat clearings too (it dipped under the outpost's fence and tents)
        }

        public override void Build()
        {
            Ambience = Synth.Amb.Forest;
            NavBounds = new Bounds(new Vector3(0, 5, 42), new Vector3(190, 50, 275));
            Lighting(new Color(0.9f, 0.92f, 0.95f), 0.85f, new Vector3(40, 150, 0), new Color(0.55f, 0.6f, 0.68f), new Color(0.45f, 0.48f, 0.45f), new Color(0.2f, 0.22f, 0.16f),
                new Color(0.62f, 0.66f, 0.68f), 0.012f, new Color(0.45f, 0.52f, 0.6f), new Color(0.75f, 0.78f, 0.8f), 0.75f, 0.6f);
            var b = B;
            b.Terrain(new Vector3(-95, 0, -95), 190, 120, H, Maps.GroundGrass, (x, z) =>
            {
                float n = MathX.Fbm(x * 0.05f, z * 0.05f, 3, 7);
                float dirt = Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), new Vector2(0, 10)) < 26 ? 1 : 0) + (n > 0.62f ? 0.6f : 0));
                return Color.Lerp(new Color(0.85f, 0.9f, 0.8f), new Color(0.7f, 0.58f, 0.45f), dirt);
            }, Synth.Surface.Grass);
            // river + bridge
            for (int i = -9; i <= 9; i++)
            {
                float x = i * 10f;
                b.Water(new Vector3(x, -1.6f, -15 - Mathf.Sin(x * 0.04f) * 8f), new Vector2(10.5f, 10f));
            }
            b.Box(new Vector3(30, 0.4f, -15 - Mathf.Sin(30 * 0.04f) * 8f), new Vector3(4, 0.3f, 16), Maps.WoodPlank, new Color(0.55f, 0.45f, 0.35f), null, true, Synth.Surface.Wood);
            // forest
            for (int i = 0; i < 340; i++)
            {
                float x = b.Rng.Range(-92f, 92f), z = b.Rng.Range(-92f, 92f);
                if (Vector2.Distance(new Vector2(x, z), new Vector2(0, 10)) < 30 || Vector2.Distance(new Vector2(x, z), new Vector2(-48, -49)) < 20) continue;
                if (Mathf.Abs(z + 15 + Mathf.Sin(x * 0.04f) * 8f) < 6) continue;
                b.Pine(new Vector3(x, H(x, z), z), b.Rng.Range(9f, 17f));
            }
            for (int i = 0; i < 60; i++) { float x = b.Rng.Range(-90f, 90f), z = b.Rng.Range(-90f, 90f); b.Rock(new Vector3(x, H(x, z), z), b.Rng.Range(0.5f, 2.2f)); }
            // outpost
            Vector3 o = new Vector3(0, 0, 10);
            b.Fence(o + new Vector3(-24, 0, -20), o + new Vector3(24, 0, -20));
            b.Fence(o + new Vector3(24, 0, -20), o + new Vector3(24, 0, 20));
            b.Fence(o + new Vector3(24, 0, 20), o + new Vector3(4, 0, 20));
            b.Fence(o + new Vector3(-4, 0, 20), o + new Vector3(-24, 0, 20));
            b.Fence(o + new Vector3(-24, 0, 20), o + new Vector3(-24, 0, -20));
            b.Watchtower(o + new Vector3(20, 0, 16), 5f);
            b.Watchtower(o + new Vector3(-20, 0, -16), 5f);
            var pts = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                var tp = o + new Vector3(-14 + i * 7.5f, 0, -8);
                b.Tent(tp, 0, new Vector3(4.5f, 2.6f, 6f), new Color(0.38f, 0.42f, 0.3f));
                pts.Add(tp + new Vector3(0, 0.05f, 0));
            }
            b.Sandbags(o + new Vector3(-14, 0, 17), o + new Vector3(-7, 0, 17));   // beside the vault, not through it
            b.Sandbags(o + new Vector3(10, 0, 4), o + new Vector3(16, 0, 4));
            b.Sandbags(o + new Vector3(-16, 0, 6), o + new Vector3(-10, 0, 6));
            // command bunker with vault
            var cb = b.Building(o + new Vector3(0, 0, 9), 0, new Vector2(12.5f, 8), 1, Maps.ConcreteDark, new Color(0.62f, 0.64f, 0.6f), Maps.Concrete, Maps.ConcreteDark, 0, false, false, 3.2f, 2);
            pts.AddRange(cb);
            AddVault(o + new Vector3(0, 0, 13f), 0, true, Maps.ConcreteDark, new Color(0.62f, 0.64f, 0.6f), 3.2f, true, 6f, 0.25f);   // same concrete as the bunker; its door is the doorway in the bunker's back wall
            Boss(o + new Vector3(0, 0.1f, 8.5f), "The Warden");
            // supply crates around the yard, only where they fit (not in the bunker, the vault, the tents or the towers)
            for (int i = 0; i < 8; i++) { float yw = b.Rng.Range(0, 90f); if (b.Spot(() => o + new Vector3(b.Rng.Range(-20f, 20f), 0, b.Rng.Range(-18f, 18f)), new Vector3(1.25f, 0.9f, 1.25f), yw, out var sp)) b.Crate(sp, yw, new Vector3(1.2f, 0.9f, 1.2f)); }
            // cabins
            for (int i = 0; i < 4; i++)
            {
                var cp = new Vector3(-55 + (i % 2) * 14, 0, -55 + (i / 2) * 12);
                pts.AddRange(b.Building(cp, b.Rng.Range(-10f, 10f), new Vector2(7, 6), 1, Maps.WoodPlank, new Color(0.62f, 0.5f, 0.38f), Maps.WoodPlank, Maps.WoodPlank, b.Rng.Range(0, 4), true));
            }
            // radio tower
            var rt = new Vector3(70, H(70, 70), 70);
            for (int i = 0; i < 4; i++) b.Cyl(rt + new Vector3(i % 2 * 3 - 1.5f, 0, i / 2 * 3 - 1.5f), rt + new Vector3(0, 30, 0), 0.12f, 0.06f, 6, Maps.Metal, new Color(0.7f, 0.25f, 0.2f));
            b.LightPole(o + new Vector3(0, 0, -18), 7, new Color(1f, 0.9f, 0.75f), true);
            ScatterLoot(pts, 0.8f);
            AddCrate(o + new Vector3(20, 5.1f, 16), 0, LootCrate.Kind.WeaponCase);
            AddCrate(o + new Vector3(-20, 5.1f, -16), 0, LootCrate.Kind.Optics);
            AddExtract(new Vector3(-80, H(-80, 70), 70), "Logging Road");
            AddExtract(new Vector3(30, 0.6f, -15 - Mathf.Sin(30 * 0.04f) * 8f + 12), "River Crossing");
            AddExtract(rt + new Vector3(4, 0, 4), "Radio Tower");
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2 + 0.3f;
                float x = Mathf.Cos(a) * 82f, z = Mathf.Sin(a) * 82f;
                Spawn(new Vector3(x, H(x, z) + 0.2f, z), Mathf.Atan2(-x, -z) * Mathf.Rad2Deg);
            }
            Squad(o, 3, true); Squad(o + new Vector3(15, 0, -10), 2); Squad(o + new Vector3(-15, 0, 10), 2);
            Squad(new Vector3(-55, 0, -50), 3); Squad(rt, 2); Squad(new Vector3(40, 0, -40), 2); Squad(new Vector3(-30, 0, 55), 2);
            // ---- North Hamlet: wooden houses in a valley past the forest
            b.Ground(new Vector3(0, 0, 137), new Vector2(190, 84), Maps.GroundGrass, Synth.Surface.Grass, -0.02f);
            var hamlet = Neighborhood(new Vector3(-62, 0, 132), 0, 9, 13.8f, true);
            ScatterLoot(hamlet, 0.85f);
            for (int i = 0; i < 70; i++)
            {
                var tp = new Vector3(b.Rng.Range(-94f, 94f), 0, b.Rng.Range(97f, 178f));
                if (Mathf.Abs(tp.z - 132f) < 22f && Mathf.Abs(tp.x) < 70f) continue;
                if (Vector2.Distance(new Vector2(tp.x, tp.z), new Vector2(0, 170)) < 9f) continue; // keep the extract clear
                b.Pine(tp, b.Rng.Range(8f, 13f));
            }
            AddExtract(new Vector3(0, 0, 170), "North Hamlet");
            Squad(new Vector3(-30, 0, 132), 3); Squad(new Vector3(40, 0, 135), 2); Squad(new Vector3(0, 0, 150), 2, true);
            Spawn(new Vector3(-85, 0.2f, 172), 135);
            // ---- endless forest around the valley: 1 km x 1 km
            Outskirts(new Rect(-95, -95, 190, 274), 500, new[] { "Ranger Station", "Dam Spillway", "Mountain Pass" }, true, false, Maps.GroundGrass, 9);
        }
    }

    // =============================================================================================== OLD TOWN
    public class OldTownMap : MapBase
    {
        public override void Build()
        {
            Ambience = Synth.Amb.Town;
            NavBounds = new Bounds(new Vector3(0, 8, 45), new Vector3(180, 40, 272));
            Lighting(new Color(1f, 0.8f, 0.65f), 0.95f, new Vector3(9, 80, 0), new Color(0.5f, 0.52f, 0.62f), new Color(0.5f, 0.46f, 0.44f), new Color(0.18f, 0.17f, 0.17f),
                new Color(0.66f, 0.62f, 0.6f), 0.011f, new Color(0.35f, 0.42f, 0.58f), new Color(0.95f, 0.72f, 0.55f), 0.55f);
            var b = B;
            b.Ground(Vector3.zero, new Vector2(180, 180), Maps.Concrete, Synth.Surface.Concrete);
            // street grid: roads every 34m, sidewalks
            var pts = new List<Vector3>();
            for (int i = -2; i <= 2; i++)
            {
                b.Box(new Vector3(i * 34, 0.01f, 0), new Vector3(9, 0.02f, 180), Maps.Asphalt, Color.white, null, false);
                b.Box(new Vector3(0, 0.012f, i * 34), new Vector3(180, 0.02f, 9), Maps.Asphalt, Color.white, null, false);
            }
            Color[] wallCols = { new Color(0.8f, 0.72f, 0.6f), new Color(0.7f, 0.65f, 0.6f), new Color(0.75f, 0.6f, 0.5f), new Color(0.62f, 0.66f, 0.7f), new Color(0.85f, 0.8f, 0.7f) };
            for (int bx = -2; bx < 2; bx++)
                for (int bz = -2; bz < 2; bz++)
                {
                    Vector3 c = new Vector3(bx * 34 + 17, 0, bz * 34 + 17);
                    // sidewalk block
                    b.Box(c + new Vector3(0, 0.08f, 0), new Vector3(25, 0.16f, 25), Maps.ConcreteDark, new Color(0.8f, 0.8f, 0.8f), null, true);
                    if (bx == 0 && bz == -1) { Bank(c); continue; }
                    if (bx == -1 && bz == 0) { Plaza(c); continue; }
                    // two buildings per block
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 bp = c + new Vector3(k == 0 ? -5.5f : 5.5f, 0.16f, b.Rng.Range(-3f, 3f));
                        bool brick = b.Rng.Chance(0.5f);
                        int floors = b.Rng.Range(2, 4);
                        pts.AddRange(b.Building(bp, b.Rng.Chance(0.5f) ? 0 : 180, new Vector2(9.5f, b.Rng.Range(12f, 17f)), floors, brick ? Maps.Brick : Maps.Plaster,
                            brick ? Color.white : b.Rng.Pick(wallCols), Maps.WoodPlank, Maps.ConcreteDark, k == 0 ? 3 : 1, true, floors == 2));
                    }
                }
            // gas station
            var gs = new Vector3(-68, 0, 68);
            b.Box(gs + new Vector3(0, 5, 0), new Vector3(14, 0.4f, 9), Maps.Metal, new Color(0.85f, 0.85f, 0.85f), null, true, Synth.Surface.Metal);
            foreach (var p in new[] { new Vector3(-6, 0, -3.5f), new Vector3(6, 0, -3.5f), new Vector3(-6, 0, 3.5f), new Vector3(6, 0, 3.5f) }) b.Box(gs + p + Vector3.up * 2.5f, new Vector3(0.4f, 5, 0.4f), Maps.Metal, new Color(0.8f, 0.2f, 0.15f), null, true, Synth.Surface.Metal);
            for (int i = 0; i < 2; i++) b.Box(gs + new Vector3(-3 + i * 6, 0.8f, 0), new Vector3(0.8f, 1.6f, 0.5f), Maps.Metal, new Color(0.85f, 0.2f, 0.15f), null, true, Synth.Surface.Metal);
            // street clutter
            Color[] carCols = { new Color(0.5f, 0.12f, 0.1f), new Color(0.2f, 0.25f, 0.35f), new Color(0.65f, 0.65f, 0.62f), new Color(0.15f, 0.15f, 0.15f), new Color(0.35f, 0.4f, 0.3f) };
            for (int i = 0; i < 22; i++)
            {
                int lane = b.Rng.Range(-2, 3);
                bool alongZ = b.Rng.Chance(0.5f);
                float t = b.Rng.Range(-80f, 80f);
                Vector3 p = alongZ ? new Vector3(lane * 34 + b.Rng.Range(-2.5f, 2.5f), 0, t) : new Vector3(t, 0, lane * 34 + b.Rng.Range(-2.5f, 2.5f));
                b.CarWreck(p, (alongZ ? 0 : 90) + b.Rng.Range(-25f, 25f), b.Rng.Pick(carCols));
            }
            for (int i = 0; i < 16; i++) { float yw = b.Rng.Range(60f, 120f); if (b.Spot(() => new Vector3(b.Rng.Range(-2, 3) * 34 + b.Rng.Range(-3f, 3f), 0, b.Rng.Range(-80f, 80f)), new Vector3(0.8f, 0.9f, 3.1f), yw, out var sp)) b.JerseyBarrier(sp, yw); }
            for (int i = 0; i < 18; i++) if (b.Spot(() => new Vector3(b.Rng.Range(-80f, 80f), 0, b.Rng.Range(-80f, 80f)), new Vector3(0.7f, 0.95f, 0.7f), 0, out var sp)) b.Barrel(sp, new Color(0.3f, 0.32f, 0.3f));   // on the street or the pavement, whichever it is
            for (int i = -2; i <= 2; i++) for (int k = -2; k <= 2; k += 2) b.LightPole(new Vector3(i * 34 + 5, 0.16f, k * 34 + 10), 6.5f, new Color(1f, 0.8f, 0.55f), (i + k) % 2 == 0, 12f);
            // checkpoint
            b.Sandbags(new Vector3(-6, 0, -85), new Vector3(6, 0, -85));
            b.Sandbags(new Vector3(-6, 0, -85), new Vector3(-6, 0, -79));
            b.Tent(new Vector3(10, 0, -82), 90, new Vector3(4, 2.4f, 5), new Color(0.35f, 0.38f, 0.28f));
            ScatterLoot(pts, 0.55f);
            AddExtract(new Vector3(0, 0, -86), "Checkpoint");
            AddExtract(new Vector3(86, 0, 34), "East Tunnel");
            AddExtract(new Vector3(-17, 0.2f, 17), "Church Square");
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2 + 0.15f;
                float x = Mathf.Cos(a) * 80f, z = Mathf.Sin(a) * 80f;
                // snap spawns onto the nearest road
                if (Mathf.Abs(x) > Mathf.Abs(z)) z = Mathf.Round(z / 34f) * 34f; else x = Mathf.Round(x / 34f) * 34f;
                Spawn(new Vector3(x, 0.1f, z), Mathf.Atan2(-x, -z) * Mathf.Rad2Deg);
            }
            Squad(new Vector3(17, 0, -17), 3, true); Squad(new Vector3(-17, 0, -51), 2); Squad(new Vector3(51, 0, 17), 3);
            Squad(new Vector3(-51, 0, 51), 2); Squad(new Vector3(17, 0, 51), 2); Squad(new Vector3(-68, 0, 68), 2); Squad(new Vector3(51, 0, -51), 2);
            // ---- Suburbs (north): main street continues into a residential street
            b.Ground(new Vector3(0, 0, 135), new Vector2(180, 92), Maps.GroundGrass, Synth.Surface.Grass, -0.01f);
            b.Box(new Vector3(0, 0.01f, 109), new Vector3(9, 0.02f, 40), Maps.Asphalt, Color.white, null, false);
            var suburb = Neighborhood(new Vector3(-67, 0, 128), 0, 10, 13.4f, false);
            ScatterLoot(suburb, 0.85f);
            for (int i = 0; i < 30; i++) { var tp = new Vector3(b.Rng.Range(-86f, 86f), 0, b.Rng.Range(150f, 177f)); if (Mathf.Abs(tp.x) < 9f || Vector2.Distance(new Vector2(tp.x, tp.z), new Vector2(60, 168)) < 5f) continue; b.Pine(tp, b.Rng.Range(7f, 10f)); }
            AddExtract(new Vector3(0, 0, 168), "Suburbs");
            Squad(new Vector3(-40, 0, 128), 3); Squad(new Vector3(30, 0, 132), 2); Squad(new Vector3(0, 0, 150), 2, true);
            Spawn(new Vector3(60, 0.1f, 168), 180);
            // ---- farmland and villages around the town: 1 km x 1 km
            Outskirts(new Rect(-90, -90, 180, 271), 500, new[] { "Highway Bridge", "Rail Depot", "County Road" }, false, false, Maps.GroundGrass, 9);
        }

        void Bank(Vector3 c)
        {
            var b = B;
            var pts = b.Building(c + new Vector3(0, 0.16f, -2), 0, new Vector2(20, 14), 2, Maps.Brick, new Color(0.85f, 0.8f, 0.75f), Maps.Tiles, Maps.Concrete, 0, true);
            b.Sign(c + new Vector3(0, 7.2f, -9.4f), Quaternion.identity, "FIRST NATIONAL BANK", new Color(0.95f, 0.85f, 0.55f), 0.45f);
            foreach (var x in new[] { -6f, -2f, 2f, 6f }) b.Cyl(c + new Vector3(x, 0.16f, -9.6f), c + new Vector3(x, 6.4f, -9.6f), 0.35f, 0.32f, 14, Maps.Plaster, new Color(0.9f, 0.88f, 0.82f));
            b.Table(c + new Vector3(-4, 0.16f, -4), 0, new Vector2(6, 0.8f), 1.1f);
            // the vault fills the back of the hall: its back wall sits just inside the bank's back wall (inner face z = +4.875)
            // brick like the bank, up to the floor above (3.1 m), which is its ceiling
            AddVault(c + new Vector3(0, 0.16f, 4.875f - 5.2f - 0.02f), 0, false, Maps.Brick, new Color(0.85f, 0.8f, 0.75f), 3.1f, false);
            pts.RemoveAll(p => Mathf.Abs(p.x - c.x) < 4.4f && p.z > c.z - 2.2f && p.y < c.y + 2.5f);   // not inside / in front of the vault
            Boss(c + new Vector3(0, 0.2f, -5.5f), "The Banker");
            ScatterLoot(pts, 0.9f);
        }

        void Plaza(Vector3 c)
        {
            var b = B;
            b.Box(c + new Vector3(0, 0.2f, 0), new Vector3(24, 0.08f, 24), Maps.Tiles, new Color(0.7f, 0.68f, 0.62f), null, true);
            b.Cyl(c + new Vector3(0, 0.2f, 0), c + new Vector3(0, 0.8f, 0), 3f, 3f, 24, Maps.Concrete, new Color(0.75f, 0.74f, 0.7f));
            b.Cyl(c + new Vector3(0, 0.8f, 0), c + new Vector3(0, 3.4f, 0), 0.4f, 0.25f, 12, Maps.Concrete, new Color(0.7f, 0.7f, 0.68f));
            // church
            var ch = c + new Vector3(0, 0.2f, 8);
            b.Building(ch, 180, new Vector2(10, 8), 2, Maps.Plaster, new Color(0.88f, 0.86f, 0.8f), Maps.WoodPlank, Maps.Concrete, 0, true);
            b.Box(ch + new Vector3(0, 9f, 0), new Vector3(3, 6, 3), Maps.Plaster, new Color(0.88f, 0.86f, 0.8f), null, true);
            b.Cyl(ch + new Vector3(0, 12f, 0), ch + new Vector3(0, 15f, 0), 2.1f, 0.05f, 4, Maps.Metal, new Color(0.3f, 0.35f, 0.38f));
            for (int i = 0; i < 6; i++) b.Pine(c + new Vector3(Mathf.Cos(i) * 9, 0.2f, Mathf.Sin(i) * 9 - 3), 6f);
        }
    }
}

namespace Rago
{
    /// <summary>
    /// Underground Sector: a sealed research complex under the islands. A 6 x 5 grid of rooms (offices, storage, labs,
    /// barracks, a server room, a generator hall, an armory and a vault lab) joined by 4 m service corridors with pipes,
    /// strip lights and hazard markings. Concrete everywhere, murky air, a moderate number of raiders (squads of 1-2),
    /// six player spawns round the edge and three extracts deeper in. 6 players per raid, squads of up to 2.
    /// </summary>
    /// <summary>
    /// Underground Sector: a sealed research complex three levels deep. Each level (B1 at the top, B2, B3) is a grid of
    /// rooms joined by service corridors, stacked exactly over each other; two stairwells link each level to the next
    /// (a railed opening in the floor above, a stair inside a walled shaft below). B1 is where you start, the vaults
    /// are on B2 and B3, and there are only two ways out: the Freight Elevator on B1 and the Drainage Tunnel on B3.
    /// </summary>
    public class UndergroundMap : MapBase
    {
        const int NX = 5, NZ = 4, Levels = 3;
        const float Pitch = 16f, Room = 12f, Ht = 3.4f, Slab = 0.5f, LevelH = Ht + Slab;
        static float LevelY(int l) => -l * LevelH;
        static Vector3 Cell(int i, int j, int l) => new Vector3((i - (NX - 1) / 2f) * Pitch, LevelY(l), (j - (NZ - 1) / 2f) * Pitch);
        // stairwells: (cell i, cell j, upper level) - the stair goes down from that level to the next
        static readonly (int i, int j, int l)[] Wells = { (1, 0, 0), (3, 3, 0), (3, 0, 1), (1, 3, 1) };

        public override void Build()
        {
            Ambience = Synth.Amb.Bunker;
            KeepFog = true; Indoor = true;
            NpcSafeRadius = 22f;
            MaxRaiders = 22;
            float ex = NX * Pitch / 2f + 2f, ez = NZ * Pitch / 2f + 2f;   // outer walls (84 x 68)
            float yBot = LevelY(Levels - 1), yTop = LevelY(0) + Ht;
            // every floor, not the top of the roof slab
            NavBounds = new Bounds(new Vector3(0, (yBot - 0.3f + 3.0f) / 2f, 0), new Vector3(ex * 2 + 2, 3.0f - (yBot - 0.3f), ez * 2 + 2));
            Lighting(new Color(0.9f, 0.95f, 1f), 0f, new Vector3(60, 30, 0), new Color(0.46f, 0.48f, 0.52f), new Color(0.38f, 0.38f, 0.39f), new Color(0.2f, 0.19f, 0.18f),
                new Color(0.05f, 0.055f, 0.06f), 0.03f, new Color(0.03f, 0.03f, 0.035f), new Color(0.06f, 0.06f, 0.065f), 0f, 0.6f);
            var b = B;
            b.NoSwingDoors = true;   // doors here come only from the rooms' own doorways (CustomDoors), one per doorway
            var conc = Maps.Concrete;
            var floor = Mats.Lit("ug_floor", new Color(0.46f, 0.47f, 0.47f), 0.1f, 0.45f, Mats.Concrete, null, 0.5f, 0.2f, true, 0.4f);
            var ceiling = Mats.Lit("ug_ceiling", Color.white, 0f, 0.2f, Mats.Concrete, null, 0.5f, 0.1f, true, 0.4f);
            var paint = Mats.Lit("ug_paint", new Color(0.26f, 0.32f, 0.3f), 0f, 0.35f, Mats.Plaster, null, 0.5f, 0.3f, true, 0.4f);
            var metal = Mats.Lit("ug_metal", Color.white, 0.7f, 0.45f, Mats.Metal, null, 0.35f, 0.4f);
            var lamp = Mats.Lit("ug_lamp", new Color(0.9f, 0.95f, 1f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(2.2f, 2.3f, 2.4f));
            var yellow = Mats.Lit("hazard_y", new Color(0.85f, 0.68f, 0.08f), 0f, 0.3f, Mats.Noise, null, 0.5f, 0.3f);
            Color wallCol = new Color(0.58f, 0.59f, 0.58f);

            // ---- shell: roof, outer walls (all three levels), and each level's floor slab with the stair openings cut out
            b.Box(new Vector3(0, yTop + 0.15f, 0), new Vector3(ex * 2, 0.3f, ez * 2), ceiling, new Color(0.45f, 0.45f, 0.46f), null, true);
            float wallH = yTop - yBot;
            b.Wall(new Vector3(-ex, yBot, -ez), new Vector3(ex, yBot, -ez), wallH, 0.5f, conc, wallCol);
            b.Wall(new Vector3(ex, yBot, -ez), new Vector3(ex, yBot, ez), wallH, 0.5f, conc, wallCol);
            b.Wall(new Vector3(ex, yBot, ez), new Vector3(-ex, yBot, ez), wallH, 0.5f, conc, wallCol);
            b.Wall(new Vector3(-ex, yBot, ez), new Vector3(-ex, yBot, -ez), wallH, 0.5f, conc, wallCol);
            for (int l = 0; l < Levels; l++)
            {
                var holes = new List<Rect>();
                foreach (var w in Wells) if (w.l == l) { var c = Cell(w.i, w.j, l); holes.Add(Rect.MinMaxRect(c.x - Room / 2f, c.z - 0.6f, c.x - Room / 2f + 2.45f, c.z + 3.6f)); }
                float th = l == Levels - 1 ? 0.3f : Slab;
                SlabWithHoles(LevelY(l), th, -ex, ex, -ez, ez, holes, floor);
            }

            // ---- per level: corridor lights, pipe runs, junction stripes
            for (int l = 0; l < Levels; l++)
            {
                float y = LevelY(l);
                for (int i = 0; i <= NX; i++)
                {
                    float x = (i - NX / 2f) * Pitch;
                    for (float z = -ez + 4f; z < ez - 2f; z += 8f) b.Box(new Vector3(x, y + Ht - 0.04f, z), new Vector3(0.25f, 0.06f, 1.4f), lamp, Color.white, null, false);
                    b.Cyl(new Vector3(x + 1.5f, y + Ht - 0.25f, -ez), new Vector3(x + 1.5f, y + Ht - 0.25f, ez), 0.07f, 0.07f, 8, metal, new Color(0.45f, 0.38f, 0.3f), false);
                    b.Cyl(new Vector3(x + 1.25f, y + Ht - 0.4f, -ez), new Vector3(x + 1.25f, y + Ht - 0.4f, ez), 0.05f, 0.05f, 8, metal, new Color(0.3f, 0.35f, 0.4f), false);
                }
                for (int j = 0; j <= NZ; j++)
                {
                    float z = (j - NZ / 2f) * Pitch;
                    for (float x = -ex + 4f; x < ex - 2f; x += 8f) b.Box(new Vector3(x, y + Ht - 0.04f, z), new Vector3(1.4f, 0.06f, 0.25f), lamp, Color.white, null, false);
                }
                for (int i = 0; i <= NX; i += 2)
                    for (int j = 0; j <= NZ; j += 2)
                    {
                        var c = new Vector3((i - NX / 2f) * Pitch, y + 0.005f, (j - NZ / 2f) * Pitch);
                        for (int s = 0; s < 4; s++) b.Box(c + Quaternion.Euler(0, s * 90, 0) * new Vector3(0, 0, 2.2f), new Vector3(3.6f, 0.008f, 0.18f), yellow, Color.white, Quaternion.Euler(0, s * 90, 0), false);
                    }
                // level markers painted on the corridor walls at the corners
                foreach (var cpos in new[] { new Vector3(-ex + 0.27f, y + 2.2f, -ez + 6f), new Vector3(ex - 0.27f, y + 2.2f, ez - 6f) })
                    b.Sign(cpos, Quaternion.Euler(0, cpos.x < 0 ? -90 : 90, 0), "LEVEL B" + (l + 1), new Color(0.85f, 0.7f, 0.15f), 0.4f);
            }

            // ---- rooms
            var pts = new List<Vector3>();
            for (int l = 0; l < Levels; l++)
            {
                var types = new List<string>();
                if (l == 0) types.AddRange(new[] { "generator", "office", "barracks", "storage" });
                else if (l == 1) types.AddRange(new[] { "vault", "armory", "server", "lab" });
                else types.AddRange(new[] { "vault", "armory", "server", "server", "generator" });
                var cells = new List<(int, int)>();
                for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++) if (WellAt(i, j, l) == null) cells.Add((i, j));
                string[] fill = l == 0 ? new[] { "office", "office", "storage", "storage", "barracks", "lab" } : new[] { "office", "storage", "storage", "lab", "barracks", "server" };
                while (types.Count < cells.Count) types.Add(b.Rng.Pick(fill));
                for (int k = types.Count - 1; k > 0; k--) { int r = b.Rng.Range(0, k + 1); var t = types[k]; types[k] = types[r]; types[r] = t; }
                int n = 0;
                for (int i = 0; i < NX; i++)
                    for (int j = 0; j < NZ; j++)
                    {
                        var c = Cell(i, j, l);
                        string well = WellAt(i, j, l);
                        string type = well ?? types[n++];
                        BuildRoom(c, type, i, j, l, conc, wallCol, paint, metal, lamp, pts);
                        if (type == "vault" || well != null) continue;
                        // raiders: the guarded rooms often hold a bigger group, elsewhere the odd pair
                        bool nest = type == "armory" || type == "server" || type == "generator" || type == "barracks";
                        if (nest ? b.Rng.Chance(0.6f) : b.Rng.Chance(0.3f))
                            Squad(c + new Vector3(b.Rng.Range(-1f, 1f), 0, -4.2f), nest ? b.Rng.Range(4, 6) : b.Rng.Range(2, 4), type == "armory" || type == "generator");
                    }
            }
            foreach (var w in Wells) BuildStairwell(Cell(w.i, w.j, w.l), metal, conc, wallCol, yellow);
            ScatterLoot(pts, 0.9f);

            // ---- player spawns round the edge of B1; only two ways out
            Spawn(new Vector3(-40, 0.05f, 32), 135); Spawn(new Vector3(40, 0.05f, -32), -45); Spawn(new Vector3(-8, 0.05f, -32), 0);
            Spawn(new Vector3(8, 0.05f, 32), 180); Spawn(new Vector3(-40, 0.05f, -32), 45); Spawn(new Vector3(40, 0.05f, 32), -135);
            AddExtract(new Vector3(8, 0, 0), "Freight Elevator (B1)");
            AddExtract(new Vector3(-40, LevelY(2), -32), "Drainage Tunnel (B3)");
        }

        /// <summary>"stairs" if a stair goes down from this cell on level l, "landing" if one arrives here from above.</summary>
        static string WellAt(int i, int j, int l)
        {
            foreach (var w in Wells)
            {
                if (w.i != i || w.j != j) continue;
                if (w.l == l) return "stairs";
                if (w.l + 1 == l) return "landing";
            }
            return null;
        }

        /// <summary>A floor slab (top at y) over the whole level, with rectangular openings left out.</summary>
        void SlabWithHoles(float y, float th, float x0, float x1, float z0, float z1, List<Rect> holes, Material m)
        {
            var xs = new List<float> { x0, x1 }; var zs = new List<float> { z0, z1 };
            foreach (var h in holes) { xs.Add(h.xMin); xs.Add(h.xMax); zs.Add(h.yMin); zs.Add(h.yMax); }
            xs.Sort(); zs.Sort();
            for (int zi = 0; zi < zs.Count - 1; zi++)
            {
                float za = zs[zi], zb = zs[zi + 1];
                if (zb - za < 0.01f) continue;
                float runStart = float.NaN;
                for (int xi = 0; xi < xs.Count - 1; xi++)
                {
                    float xa = xs[xi], xb = xs[xi + 1];
                    if (xb - xa < 0.01f) continue;
                    var mid = new Vector2((xa + xb) / 2f, (za + zb) / 2f);
                    bool hole = false; foreach (var h in holes) if (h.Contains(mid)) { hole = true; break; }
                    if (!hole && float.IsNaN(runStart)) runStart = xa;
                    bool last = xi == xs.Count - 2;
                    if (!float.IsNaN(runStart) && (hole || last))
                    {
                        float end = hole ? xa : xb;
                        B.Box(new Vector3((runStart + end) / 2f, y - th / 2f, (za + zb) / 2f), new Vector3(end - runStart, th, zb - za), m, Color.white, null, true, Synth.Surface.Concrete);
                        runStart = float.NaN;
                    }
                }
            }
        }

        /// <summary>A yellow safety railing from a to b: posts, a top rail and a mid rail, with a solid (invisible) barrier.</summary>
        void Rail(Vector3 a, Vector3 b2, Material metal)
        {
            var b = B;
            var d = b2 - a; float len = d.magnitude; var q = Quaternion.LookRotation(d / len, Vector3.up);
            var col = new Color(0.85f, 0.68f, 0.1f);
            foreach (float y in new[] { 1.0f, 0.5f }) b.Box(a + d * 0.5f + Vector3.up * y, new Vector3(0.05f, 0.05f, len), metal, col, q, false);
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 1.1f) + 1);
            for (int i = 0; i < posts; i++) b.Box(a + d * (i / (posts - 1f)) + Vector3.up * 0.5f, new Vector3(0.05f, 1.0f, 0.05f), metal, col, q, false);
            b.Collider(a + d * 0.5f + Vector3.up * 0.55f, new Vector3(0.08f, 1.1f, len), q, Synth.Surface.Metal);
        }

        /// <summary>
        /// The stair from the room at c (upper level) down to the same room on the level below: it runs along the west
        /// wall, coming up through a railed opening in the upper floor; below, a wall closes the stair off into a shaft
        /// you enter from the south.
        /// </summary>
        void BuildStairwell(Vector3 c, Material metal, Material conc, Color wallCol, Material yellow)
        {
            var b = B;
            float h = Room / 2f;
            float yU = c.y, yD = c.y - LevelH;
            float sx = c.x - h + 1.25f;   // 2.2 m wide: room for two people side by side
            b.Stairs(new Vector3(sx, yD, c.z - 2.8f), new Vector3(sx, yU, c.z + 3.6f), 2.2f, metal);
            // railings round the opening upstairs (east and south sides; the stair comes up at the north end)
            float rx = c.x - h + 2.48f;
            Rail(new Vector3(rx, yU, c.z - 0.6f), new Vector3(rx, yU, c.z + 3.6f), metal);
            Rail(new Vector3(c.x - h + 0.15f, yU, c.z - 0.6f), new Vector3(rx, yU, c.z - 0.6f), metal);
            b.Box(new Vector3(c.x - h + 1.25f, yU + 0.004f, c.z + 4.3f), new Vector3(2.3f, 0.008f, 1.0f), yellow, Color.white, null, false);   // painted landing at the top
            // the shaft below: a wall along the stair's open side, from the bottom step to the north wall
            b.Wall(new Vector3(rx, yD, c.z - 2.0f), new Vector3(rx, yD, c.z + h), Ht, 0.15f, conc, wallCol);
            int lvl = Mathf.RoundToInt(-yU / LevelH);
            b.Sign(new Vector3(c.x - h + 1.25f, yU + 2.4f, c.z + h - 0.18f), Quaternion.identity, "DOWN TO B" + (lvl + 2), new Color(0.9f, 0.85f, 0.6f), 0.16f);
            b.Sign(new Vector3(c.x - h + 0.17f, yD + 2.1f, c.z - 3.6f), Quaternion.Euler(0, -90, 0), "UP TO B" + (lvl + 1), new Color(0.9f, 0.85f, 0.6f), 0.16f);
        }

        void BuildRoom(Vector3 c, string type, int gi, int gj, int lvl, Material conc, Color wallCol, Material paint, Material metal, Material lamp, List<Vector3> pts)
        {
            var b = B;
            float h = Room / 2f;
            // doors: each side has a 1.6 m doorway with a fair chance, at least one per room
            var doors = new bool[4];
            for (int s = 0; s < 4; s++) doors[s] = b.Rng.Chance(0.55f);
            if (!doors[0] && !doors[1] && !doors[2] && !doors[3]) doors[b.Rng.Range(0, 4)] = true;
            if (type == "vault") { doors = new bool[4]; doors[0] = true; }
            bool shaft = type == "stairs" || type == "landing";
            if (shaft) { doors[3] = false; doors[0] = true; }   // the stair runs along the west wall; you reach it from the south
            var corners = new[] { c + new Vector3(-h, 0, -h), c + new Vector3(h, 0, -h), c + new Vector3(h, 0, h), c + new Vector3(-h, 0, h) };
            for (int s = 0; s < 4; s++)
            {
                var ops = doors[s] ? new List<Vector4> { new Vector4(Room * 0.5f, 1.6f, 0, 2.3f) } : null;
                b.Wall(corners[s], corners[(s + 1) % 4], Ht, 0.3f, conc, wallCol, ops);
                // one door in this doorway (not the vault room's: its round vault door goes there)
                if (doors[s] && type != "vault")
                {
                    var dir = (corners[(s + 1) % 4] - corners[s]).normalized;
                    CustomDoors.Add(new WorldBuilder.DoorGap { c = corners[s] + dir * (Room * 0.5f), along = dir, across = Vector3.Cross(dir, Vector3.up), halfW = 0.8f, top = c.y + 2.3f, swing = true });
                }
            }
            // paint band inside, a lamp, a door label
            // paint band along the bottom of each wall - left open at the doorways
            for (int s = 0; s < 4; s++)
            {
                var a = corners[s]; var d = (corners[(s + 1) % 4] - a).normalized; var inward = Vector3.Cross(d, Vector3.up);
                var segs = doors[s] ? new[] { (0.2f, Room * 0.5f - 0.9f), (Room * 0.5f + 0.9f, Room - 0.2f) } : new[] { (0.2f, Room - 0.2f) };
                foreach (var (t0, t1) in segs)
                    b.Box(a + d * ((t0 + t1) * 0.5f) + inward * 0.16f + Vector3.up * 0.6f, new Vector3(0.02f, 1.2f, t1 - t0), paint, Color.white, Quaternion.LookRotation(d, Vector3.up), false);
            }
            b.Box(c + new Vector3(0, Ht - 0.04f, 0), new Vector3(1.4f, 0.06f, 0.3f), lamp, Color.white, null, false);
            string label = "B" + (lvl + 1) + "-" + (char)('A' + gi) + (gj + 1).ToString() + "  " + (shaft ? "STAIRWELL" : type.ToUpper());
            for (int s = 0; s < 4; s++)
            {
                if (!doors[s]) continue;
                var mid = (corners[s] + corners[(s + 1) % 4]) / 2f;
                var outward = (mid - c).normalized;
                b.Sign(mid + outward * 0.18f + Vector3.up * 2.55f, Quaternion.LookRotation(-outward), label, new Color(0.85f, 0.85f, 0.8f), 0.12f);
            }
            // furniture and loot spots by type (loot: against the walls on the floor, or on desks/benches)
            Quaternion R(float y) => Quaternion.Euler(0, y, 0);
            void FloorSpot(Vector3 p, float yaw) { pts.Add(p + Vector3.up * 0.02f); b.SpotYaw[p + Vector3.up * 0.02f] = yaw; }
            void DeskSpot(Vector3 p, float yaw) { var q = p + Vector3.up * 0.82f; pts.Add(q); b.DeskSpots[q] = true; b.SpotYaw[q] = yaw; }
            // floor spots along the walls, away from the doorways
            for (int s = 0; s < 4; s++)
            {
                if (shaft && s != 1) continue;   // stairwells: loot only along the east wall
                if (type == "vault" && s != 0) continue;   // vault rooms: the vault fills the back of the room - loot only along the front
                var a = corners[s]; var d = corners[(s + 1) % 4] - a; var inward = Vector3.Cross(d.normalized, Vector3.up);   // corners run anticlockwise: this points into the room
                foreach (float t in new[] { 0.2f, 0.8f })
                {
                    var p = a + d * t + inward * 0.65f;
                    FloorSpot(p, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 90f);
                }
            }
            switch (type)
            {
                case "office":
                    for (int k = 0; k < 3; k++)
                    {
                        var dp = c + new Vector3(-3.5f + k * 3.5f, 0, -1f);
                        if (!b.Table(dp, 0, new Vector2(1.6f, 0.8f), 0.76f)) continue;
                        b.Box(dp + new Vector3(0, 0.25f, -0.7f), new Vector3(0.5f, 0.5f, 0.5f), metal, new Color(0.2f, 0.2f, 0.22f), null, true, Synth.Surface.Metal);   // chair
                        b.Box(dp + new Vector3(0.3f, 0.95f, 0.2f), new Vector3(0.5f, 0.35f, 0.05f), metal, new Color(0.1f, 0.1f, 0.11f), null, false);   // monitor
                        DeskSpot(dp + new Vector3(-0.35f, 0, -0.05f), 0);
                    }
                    for (int k = 0; k < 3; k++) b.Box(c + new Vector3(-h + 0.45f, 0.65f, -4.6f + k * 1.0f), new Vector3(0.6f, 1.3f, 0.9f), metal, new Color(0.38f, 0.4f, 0.38f), null, true, Synth.Surface.Metal);   // filing cabinets
                    break;
                case "storage":
                    b.Shelf(c + new Vector3(-3.4f, 0, -h + 0.5f), 0, 3.2f); b.Shelf(c + new Vector3(3.4f, 0, h - 0.5f), 180, 3.2f);
                    b.Shelf(c + new Vector3(-2.2f, 0, 1.0f), 90, 2.6f); b.Shelf(c + new Vector3(2.2f, 0, 1.0f), 90, 2.6f);
                    b.Crate(c + new Vector3(-4.2f, 0, 3.6f), 10, new Vector3(1.0f, 0.8f, 1.0f)); b.Crate(c + new Vector3(4.0f, 0, -3.4f), -8, new Vector3(1.2f, 0.9f, 1.0f));
                    break;
                case "lab":
                    foreach (float z in new[] { -2.2f, 1.6f })
                    {
                        if (!b.Table(c + new Vector3(0, 0, z), 0, new Vector2(5f, 0.9f), 0.9f)) continue;
                        DeskSpot(c + new Vector3(-1.4f, 0.12f, z), 0); DeskSpot(c + new Vector3(1.4f, 0.12f, z), 0);
                        for (int k = 0; k < 3; k++) b.Cyl(c + new Vector3(-1.8f + k * 1.8f, 0.93f, z + 0.25f), c + new Vector3(-1.8f + k * 1.8f, 1.13f, z + 0.25f), 0.05f, 0.04f, 10, Mats.Lit("lab_glass", new Color(0.6f, 0.8f, 0.85f), 0.1f, 0.9f), Color.white, false);
                    }
                    b.Box(c + new Vector3(h - 0.5f, 0.45f, 3.9f), new Vector3(0.7f, 0.9f, 2.4f), metal, new Color(0.75f, 0.76f, 0.78f), null, true, Synth.Surface.Metal);   // sink unit
                    break;
                case "barracks":
                    foreach (float bz in new[] { -4.3f, -2.1f, 2.1f, 4.3f })
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            var bp = c + new Vector3(sx * (h - 0.6f), 0, bz);
                            for (int bunk = 0; bunk < 2; bunk++) b.Box(bp + Vector3.up * (0.45f + bunk * 1.05f), new Vector3(0.9f, 0.12f, 2.0f), metal, new Color(0.32f, 0.35f, 0.3f), null, true, Synth.Surface.Metal);
                            foreach (float cz in new[] { -0.95f, 0.95f }) foreach (float cx in new[] { -0.42f, 0.42f }) b.Box(bp + new Vector3(cx, 0.85f, cz), new Vector3(0.05f, 1.7f, 0.05f), metal, new Color(0.25f, 0.25f, 0.25f), null, false);
                        }
                    if (b.Table(c, 0, new Vector2(1.6f, 0.9f), 0.76f)) DeskSpot(c, 0);
                    break;
                case "server":
                    for (int r = 0; r < 3; r++)
                        for (int k = 0; k < 4; k++)
                        {
                            var rp = c + new Vector3(-3f + k * 2f, 1.0f, -2.6f + r * 2.6f);
                            b.Box(rp, new Vector3(0.6f, 2.0f, 0.9f), metal, new Color(0.12f, 0.12f, 0.13f), null, true, Synth.Surface.Metal);
                            b.Box(rp + new Vector3(0, 0.3f, -0.46f), new Vector3(0.45f, 0.04f, 0.01f), Mats.Lit("server_led", new Color(0.1f, 0.8f, 0.3f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(0.2f, 1.6f, 0.5f)), Color.white, null, false);
                        }
                    break;
                case "generator":
                    b.Box(c + new Vector3(0, 1.1f, 0), new Vector3(4.5f, 2.2f, 2.4f), metal, new Color(0.35f, 0.4f, 0.32f), null, true, Synth.Surface.Metal);
                    b.Cyl(c + new Vector3(-1.5f, 2.2f, 0), c + new Vector3(-1.5f, Ht, 0), 0.3f, 0.3f, 12, metal, new Color(0.3f, 0.3f, 0.3f), false);   // exhaust
                    b.Box(c + new Vector3(3.6f, 0.75f, -3.5f), new Vector3(1.2f, 1.5f, 0.6f), metal, new Color(0.4f, 0.42f, 0.4f), null, true, Synth.Surface.Metal);   // control cabinet
                    if (b.Table(c + new Vector3(-3.8f, 0, 3.6f), 90, new Vector2(1.6f, 0.8f), 0.9f)) DeskSpot(c + new Vector3(-3.8f, 0.12f, 3.6f), 90);
                    break;
                case "armory":
                    foreach (float rx in new[] { -3.5f, 3.5f }) b.Box(c + new Vector3(rx, 1.0f, h - 0.4f), new Vector3(2.4f, 2.0f, 0.4f), metal, new Color(0.3f, 0.32f, 0.3f), null, true, Synth.Surface.Metal);   // weapon racks
                    b.Table(c + new Vector3(0, 0, -1f), 0, new Vector2(3f, 1f), 0.9f);
                    AddCrate(c + new Vector3(-0.8f, 0.94f, -1f), 0, LootCrate.Kind.WeaponCase, 0.85f, true);
                    AddCrate(c + new Vector3(0.9f, 0.94f, -1f), 0, LootCrate.Kind.Optics, 0.7f, true);
                    AddCrate(c + new Vector3(-h + 0.7f, 0, 3f), 90, LootCrate.Kind.ArmorCase, 0.7f, false);
                    break;
                case "vault":
                    // a vault lab: the vault room against the back wall, door facing the room's only doorway
                    // built across the back of the room: the room's own walls are its sides and back
                    AddVault(c + new Vector3(0, 0, h - 0.15f - 5.2f), 0, false, conc, wallCol, Ht, false, Room - 0.3f, 0.3f, false, false);
                    if (lvl == 2) Boss(c + new Vector3(0, 0.05f, -3.8f), "Doctor Kessler");
                    break;
            }
        }
    }
}
