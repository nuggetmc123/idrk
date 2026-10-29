using System;
using UnityEngine;

namespace Flopzone
{
    /// <summary>The 10 battle maps and the hub, built in code.</summary>
    public static class MapBuilder
    {
        public static readonly string[] Names =
        {
            "Warehouse", "Rooftops", "Dune Outpost", "Frostbite", "Jungle Ruins",
            "Neon City", "Shipyard", "Foundry", "Castle Keep", "Moon Base"
        };

        public const int HubIndex = -1;
        public const int TutorialIndex = -2;

        /// <summary>How much bigger a map gets for the larger modes (everything but 1v1 and 2v2).</summary>
        public const float ExpandScale = 1.35f;

        /// <summary>1v1 and 2v2 keep the tight maps; every other mode plays on the bigger versions.</summary>
        public static bool Expands(string mode) => mode != null && mode != "1v1" && mode != "2v2";

        public static World Build(int index, bool expanded = false)
        {
            var go = new GameObject(index == HubIndex ? "Hub" : index == TutorialIndex ? "Training" : "Map " + Names[index]);
            var w = go.AddComponent<World>();
            w.mapIndex = index;
            w.mapName = index >= 0 ? Names[index] : index == HubIndex ? "Hub" : "Training Range";
            var k = new MapKit(w, 1000 + index);
            switch (index)
            {
                case 0: Warehouse(k); break;
                case 1: Rooftops(k); break;
                case 2: Dunes(k); break;
                case 3: Frostbite(k); break;
                case 4: Jungle(k); break;
                case 5: Neon(k); break;
                case 6: Shipyard(k); break;
                case 7: Foundry(k); break;
                case 8: Castle(k); break;
                case 9: Moon(k); break;
                case TutorialIndex: Training(k); break;
                default: Hub.Build(k); break;
            }
            // the edge of the play area: a glowing wall nobody gets past
            w.bounds = Bounds(index);
            if (index >= 0) Dress(k, w.bounds, index);
            if (index >= 0 && expanded) Expand(k, w, index);
            BarrierField.Create(k.D, w.bounds);
            Physics.SyncTransforms();
            if (Application.isPlaying)
            {
                try { StaticBatchingUtility.Combine(k.S.gameObject); }
                catch (Exception e) { Debug.LogWarning("[Flopzone] static batching skipped: " + e.Message); }
            }
            return w;
        }

        /* ======================= 0 · Warehouse ======================= */
        /// <summary>Make a map look lived-in: stains and scorch marks on the ground, rubble, tyres, cones.</summary>
        static void Dress(MapKit k, Vector2 b, int index)
        {
            Physics.SyncTransforms();
            var stain = Prims.Fade(new Color(0.05f, 0.04f, 0.03f, 0.45f));
            stain.mainTexture = Prims.SoftDot;
            stain.renderQueue = 2460;
            var scorch = Prims.Fade(new Color(0.02f, 0.02f, 0.02f, 0.7f));
            scorch.mainTexture = Prims.SoftDot;
            scorch.renderQueue = 2461;
            bool snowy = index == 3, space = index == 9;
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(k.R(-b.x + 2, b.x - 2), 0, k.R(-b.y + 2, b.y - 2));
                if (!Physics.Raycast(p + Vector3.up * 30, Vector3.down, out var hit, 60f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) continue;
                var q = Prims.Make(PrimitiveType.Quad, k.D, hit.point + hit.normal * 0.012f, new Vector3(k.R(1f, 3.5f), k.R(1f, 3.5f), 1),
                    i % 4 == 0 ? scorch : stain, false, Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, k.R(0, 360)));
                q.name = "stain";
            }
            var rubble = MapKit.M(snowy ? "dfe6ee" : space ? "8a8a8e" : "8c8478", 0.15f, 0f, Surface.Concrete);
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector3(k.R(-b.x + 2, b.x - 2), 0, k.R(-b.y + 2, b.y - 2));
                if (!Physics.Raycast(p + Vector3.up * 30, Vector3.down, out var hit, 60f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) continue;
                for (int j = 0; j < 3; j++)
                    Prims.Box(k.S, hit.point + new Vector3(k.R(-0.4f, 0.4f), 0.05f, k.R(-0.4f, 0.4f)), Vector3.one * k.R(0.08f, 0.25f), rubble, false, Quaternion.Euler(k.R(0, 90), k.R(0, 360), k.R(0, 90)));
            }
            if (space || index == 4 || index == 8) return;
            var tire = MapKit.M("1b1b1b", 0.25f, 0f, Surface.Rubber);
            var cone = MapKit.M("ff6a13", 0.4f, 0f, Surface.Plastic);
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(k.R(-b.x + 4, b.x - 4), 0, k.R(-b.y + 6, b.y - 6));
                if (!Physics.Raycast(p + Vector3.up * 30, Vector3.down, out var hit, 60f, Layers.ShootMask, QueryTriggerInteraction.Ignore) || hit.point.y > 0.5f) continue;
                if (i % 2 == 0)
                    for (int t = 0; t < 3; t++) Prims.Cyl(k.S, hit.point + Vector3.up * (0.15f + t * 0.3f), 0.95f, 0.3f, tire, Vector3.up, t == 0);
                else
                    for (int c = 0; c < 3; c++)
                    {
                        var cp = hit.point + new Vector3(c * 0.8f, 0, k.R(-0.3f, 0.3f));
                        Prims.Box(k.S, cp + Vector3.up * 0.02f, new Vector3(0.4f, 0.04f, 0.4f), cone, false);
                        Prims.Cyl(k.S, cp + Vector3.up * 0.32f, 0.16f, 0.6f, cone, Vector3.up);
                        Prims.Cyl(k.S, cp + Vector3.up * 0.35f, 0.18f, 0.08f, MapKit.M("f2f2f2", 0.4f, 0f, Surface.Plastic), Vector3.up);
                    }
            }
        }

        /// <summary>
        /// The big version of a map: a few more pieces of cover, then the whole layout is
        /// spread out sideways (heights stay the same, so every jump and ramp still works),
        /// along with the spawns, flags and the play area.
        /// </summary>
        static void Expand(MapKit k, World w, int index)
        {
            Physics.SyncTransforms();
            var b = w.bounds;
            int placed = 0;
            for (int i = 0; i < 60 && placed < 10; i++)
            {
                var p = new Vector3(k.R(-b.x + 4, b.x - 4), 0, k.R(-b.y + 4, b.y - 4));
                if (!Physics.Raycast(p + Vector3.up * 30, Vector3.down, out var hit, 60f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 1.4f, 1.2f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) continue;
                k.CrateStack(hit.point, k.R(0, 360));
                placed++;
            }
            float s = ExpandScale;
            k.S.localScale = new Vector3(s, 1, s);
            foreach (Transform c in k.D) c.localPosition = new Vector3(c.localPosition.x * s, c.localPosition.y, c.localPosition.z * s);
            Vector3 Spread(Vector3 v) => new Vector3(v.x * s, v.y, v.z * s);
            for (int t = 0; t < 2; t++)
            {
                for (int i = 0; i < w.teamSpawns[t].Count; i++) w.teamSpawns[t][i] = Spread(w.teamSpawns[t][i]);
                w.flagBase[t] = Spread(w.flagBase[t]);
            }
            for (int i = 0; i < w.ffaSpawns.Count; i++) w.ffaSpawns[i] = Spread(w.ffaSpawns[i]);
            w.bounds = b * s;
            w.expanded = true;
            Physics.SyncTransforms();
        }

        static Vector2 Bounds(int index)
        {
            switch (index)
            {
                case 1: return new Vector2(21, 33);
                case 6: return new Vector2(40, 34.5f);
                case HubIndex: return new Vector2(44, 46);
                case TutorialIndex: return new Vector2(16, 16);
                default: return new Vector2(35, 35);
            }
        }

        static void Warehouse(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Overcast,
                skyTint = Prims.Hex("8aa0b8"), sunEuler = new Vector3(62, 35, 0), sunIntensity = 1.0f,
                ambSky = Prims.Hex("7d8a99"), ambEquator = Prims.Hex("5b5f66"), ambGround = Prims.Hex("3a3632"),
                fog = Prims.Hex("8c96a3"), fogDensity = 0.008f
            });
            var concrete = MapKit.M("7c7f84", 0.25f, 0f, Surface.Concrete);
            var wall = MapKit.M("3e5a63", 0.3f, 0.3f);
            var beam = MapKit.M("c9a227", 0.35f, 0.5f);
            k.Ground(36, 36, concrete);
            k.Perimeter(34, 34, 8f, wall);
            for (int i = -32; i <= 32; i += 4)
            {
                k.Box(new Vector3(i, 4, 34.45f), new Vector3(0.25f, 8, 0.3f), wall);
                k.Box(new Vector3(i, 4, -34.45f), new Vector3(0.25f, 8, 0.3f), wall);
                k.Box(new Vector3(34.45f, 4, i), new Vector3(0.3f, 8, 0.25f), wall);
                k.Box(new Vector3(-34.45f, 4, i), new Vector3(0.3f, 8, 0.25f), wall);
            }
            for (int z = -30; z <= 30; z += 10)
            {
                k.Box(new Vector3(0, 8.2f, z), new Vector3(70, 0.5f, 0.4f), beam, 0, false);
                k.Box(new Vector3(-15, 7.6f, z), new Vector3(0.6f, 0.6f, 0.6f), MapKit.Glow("fff1c9", 2.5f), 0, false);
                k.Box(new Vector3(15, 7.6f, z), new Vector3(0.6f, 0.6f, 0.6f), MapKit.Glow("fff1c9", 2.5f), 0, false);
            }
            // painted lanes
            var paint = MapKit.M("e8c33a", 0.3f);
            k.Box(new Vector3(-6, 0.005f, 0), new Vector3(0.2f, 0.01f, 60), paint, 0, false);
            k.Box(new Vector3(6, 0.005f, 0), new Vector3(0.2f, 0.01f, 60), paint, 0, false);
            // container rows (some stacked, with ramps up)
            string[] colors = { "b33a2e", "2e6db3", "2e9e57", "d9822b", "6b6f76" };
            k.Sym((p, y) => k.Container(p, y, colors[k.Ri(0, colors.Length)]), new Vector3(-14, 0, -12), 0);
            k.Sym((p, y) => k.Container(p, y, colors[k.Ri(0, colors.Length)]), new Vector3(-14, 0, -4), 0);
            k.Sym((p, y) => k.Container(p, y, colors[k.Ri(0, colors.Length)]), new Vector3(-14, 2.6f, -8), 0);
            k.Sym((p, y) => k.Ramp(p, y, 6.9f, 2.2f, 2.6f, MapKit.M("5b5e63", 0.4f, 0.6f)), new Vector3(-14, 0, -22), 0);
            k.Sym((p, y) => k.Container(p, y + 90, colors[k.Ri(0, colors.Length)]), new Vector3(-26, 0, 10), 0);
            k.Sym((p, y) => k.Container(p, y + 90, colors[k.Ri(0, colors.Length)]), new Vector3(20, 0, -22), 0);
            // center platform
            var plat = MapKit.M("5b5e63", 0.4f, 0.6f);
            k.Box(new Vector3(0, 2.1f, 0), new Vector3(10, 0.3f, 10), plat);
            foreach (var p in new[] { new Vector3(-4.6f, 0, -4.6f), new Vector3(4.6f, 0, -4.6f), new Vector3(-4.6f, 0, 4.6f), new Vector3(4.6f, 0, 4.6f) })
                k.Box(p + Vector3.up, new Vector3(0.4f, 2f, 0.4f), beam);
            k.Stairs(new Vector3(0, 0, -11.2f), 0, 7, 0.32f, 0.9f, 3f, plat);
            k.Stairs(new Vector3(0, 0, 11.2f), 180, 7, 0.32f, 0.9f, 3f, plat);
            k.Crate(new Vector3(-3, 2.25f, 2), 1.2f, 20);
            k.Crate(new Vector3(3, 2.25f, -2), 1.2f, -10);
            k.Railing(new Vector3(-5, 2.25f, 5), new Vector3(-5, 2.25f, -5), beam);
            k.Railing(new Vector3(5, 2.25f, 5), new Vector3(5, 2.25f, -5), beam);
            // crates and barrels
            for (int i = 0; i < 7; i++) k.Sym((p, y) => k.CrateStack(p, y), new Vector3(k.R(-28, 28), 0, k.R(-26, -6)), k.R(0, 90));
            for (int i = 0; i < 5; i++) k.Sym((p, y) => k.Barrel(p, k.Pick("c0392b", "2c7fb8", "e1b12c")), new Vector3(k.R(-30, 30), 0, k.R(-28, -4)), 0);
            // forklifts
            k.Sym((p, y) => Forklift(k, p, y), new Vector3(9, 0, -20), 30);
            k.DefaultSpawns(28, 22);
            k.BasePads();
            k.world.killY = -10;
        }

        static void Forklift(MapKit k, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var yel = MapKit.M("e5b221", 0.45f, 0.2f);
            var blk = MapKit.M("1f1f22", 0.3f);
            k.BoxRot(p + rot * new Vector3(0, 0.75f, 0), new Vector3(1.3f, 1.1f, 2.0f), yel, rot);
            k.BoxRot(p + rot * new Vector3(0, 1.9f, -0.2f), new Vector3(1.2f, 0.1f, 1.3f), blk, rot);
            k.BoxRot(p + rot * new Vector3(0, 1.4f, 1.1f), new Vector3(1.1f, 2.8f, 0.12f), blk, rot);
            k.BoxRot(p + rot * new Vector3(0.35f, 0.15f, 1.8f), new Vector3(0.15f, 0.08f, 1.3f), blk, rot);
            k.BoxRot(p + rot * new Vector3(-0.35f, 0.15f, 1.8f), new Vector3(0.15f, 0.08f, 1.3f), blk, rot);
        }

        /* ======================= 1 · Rooftops ======================= */
        static void Rooftops(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Sunset,
                skyTint = Prims.Hex("ff9a5c"), groundTint = Prims.Hex("4a3a4f"), atmosphere = 1.6f, exposure = 1.3f,
                sun = Prims.Hex("ffb27a"), sunIntensity = 1.2f, sunEuler = new Vector3(14, -60, 0),
                ambSky = Prims.Hex("c9877a"), ambEquator = Prims.Hex("8a6670"), ambGround = Prims.Hex("3c3040"),
                fog = Prims.Hex("d99a86"), fogDensity = 0.007f
            });
            var roof = MapKit.M("8d8f94", 0.2f, 0f, Surface.Concrete);
            var tar = MapKit.M("4a4b50", 0.15f, 0f, Surface.Asphalt);
            var rail = MapKit.M("3a3d44", 0.5f, 0.6f);
            var plank = MapKit.M("9c7448", 0.15f, 0f, Surface.Plank);
            // (center, size, roof height, wall color)
            var roofs = new[]
            {
                (new Vector3(0, 0, -24), new Vector2(26, 16), 0f, "b5654a"),
                (new Vector3(0, 0, 24), new Vector2(26, 16), 0f, "4a6fb5"),
                (new Vector3(-13, 0, 0), new Vector2(14, 16), 1.2f, "c2a27a"),
                (new Vector3(13, 0, 0), new Vector2(14, 16), 1.2f, "7a8c6a"),
                (new Vector3(0, 0, 0), new Vector2(6, 6), 3.6f, "9a9a9a"),
            };
            foreach (var (c, s, h, wallHex) in roofs)
            {
                k.Building(new Vector3(c.x, -45, c.z), new Vector3(s.x, 45 + h, s.y), wallHex, "ffe2a8", true);
                k.Box(new Vector3(c.x, h - 0.04f, c.z), new Vector3(s.x - 0.6f, 0.1f, s.y - 0.6f), tar, 0, false);
            }
            // parapets on the outer edges of the two base roofs
            k.Box(new Vector3(0, 0.35f, -32), new Vector3(26, 0.7f, 0.3f), roof);
            k.Box(new Vector3(0, 0.35f, 32), new Vector3(26, 0.7f, 0.3f), roof);
            // walkways: each base roof to both side roofs, side roofs up to the tower
            k.Sym((p, y) => k.Ramp(p, y, 8f, 2.4f, 1.2f, plank), new Vector3(-11, 0, -16), 0);
            k.Sym((p, y) => k.Ramp(p, y, 8f, 2.4f, 1.2f, plank), new Vector3(11, 0, -16), 0);
            k.Sym((p, y) => k.Ramp(p, y, 3f, 2f, 2.4f, plank), new Vector3(-6, 1.2f, 0), 90);
            // roof props
            k.Sym((p, y) => AcUnit(k, p, y), new Vector3(-8, 0, -27), 0);
            k.Sym((p, y) => AcUnit(k, p, y), new Vector3(7, 0, -21), 90);
            k.Sym((p, y) => WaterTower(k, p), new Vector3(9, 0, -28), 0);
            k.Sym((p, y) => k.CrateStack(p, y), new Vector3(-3, 0, -18.5f), 15);
            k.Sym((p, y) => k.CrateStack(p, y), new Vector3(-14, 1.2f, -4), 0);
            k.Sym((p, y) => AcUnit(k, p, y), new Vector3(-15, 1.2f, 3), 0);
            k.Box(new Vector3(0, 3.6f + 0.5f, 0), new Vector3(1.2f, 1f, 1.2f), rail);
            k.Sym((p, y) => k.Railing(p, p + new Vector3(0, 0, 5f), rail), new Vector3(-20, 1.2f, -5), 0);
            // the city far below
            k.Backdrop(70, 45, 160, p =>
                k.Building(new Vector3(p.x, -60, p.z), new Vector3(k.R(8, 18), k.R(30, 75), k.R(8, 18)), k.Pick("8a6f6a", "6f7a8a", "a08870", "5a6070"), "ffd99a", k.R(0, 1) < 0.6f, false));
            k.Box(new Vector3(0, -60, 0), new Vector3(400, 1, 400), MapKit.M("3a3a40"), 0, false);
            // spawns on the two base roofs; ffa across all
            for (int t = 0; t < 2; t++)
            {
                float z = t == 0 ? -24 : 24;
                for (int i = 0; i < 8; i++) k.world.teamSpawns[t].Add(new Vector3((i % 4 - 1.5f) * 4f, 0.5f, z + (i / 4 - 0.5f) * 4f * (t == 0 ? -1 : 1)));
                k.world.flagBase[t] = new Vector3(0, 0, t == 0 ? -29 : 29);
            }
            foreach (var (c, s, h, _) in roofs)
                for (int i = 0; i < 3; i++) k.world.ffaSpawns.Add(new Vector3(c.x + k.R(-s.x * 0.3f, s.x * 0.3f), h + 0.5f, c.z + k.R(-s.y * 0.3f, s.y * 0.3f)));
            k.BasePads();
            k.world.killY = -8;
        }

        static void AcUnit(MapKit k, Vector3 p, float yaw)
        {
            var m = MapKit.M("b9bdc4", 0.4f, 0.5f);
            var u = k.Block(p, new Vector3(2.2f, 1.3f, 1.4f), m, yaw);
            Prims.Cyl(u.transform, new Vector3(0.2f, 0.505f, 0), 0.5f, 0.02f, MapKit.M("3a3a3a", 0.5f, 0.6f), Vector3.up);
        }

        static void WaterTower(MapKit k, Vector3 p)
        {
            var wood = MapKit.M("7a5638", 0.15f, 0f, Surface.Plank);
            var steel = MapKit.M("444", 0.4f, 0.7f);
            foreach (var o in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                k.Box(p + o + Vector3.up * 1.5f, new Vector3(0.2f, 3f, 0.2f), steel);
            Prims.Cyl(k.S, p + Vector3.up * 4.2f, 3f, 2.6f, wood, Vector3.up, true);
            Prims.Cyl(k.S, p + Vector3.up * 5.7f, 3.2f, 0.4f, steel, Vector3.up, false);
        }

        /* ======================= 2 · Dune Outpost ======================= */
        static void Dunes(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Haze,
                skyTint = Prims.Hex("9cc3e6"), groundTint = Prims.Hex("c9a46b"), exposure = 1.4f, sunIntensity = 1.35f,
                sun = Prims.Hex("fff0d0"), sunEuler = new Vector3(58, 120, 0),
                ambSky = Prims.Hex("a9c2dd"), ambEquator = Prims.Hex("c9ad85"), ambGround = Prims.Hex("8a6a45"),
                fog = Prims.Hex("e3cfa8"), fogDensity = 0.007f
            });
            var sand = MapKit.M("dcc08a", 0.08f, 0f, Surface.Sand);
            var stone = MapKit.M("c99a63", 0.1f, 0f, Surface.Brick);
            var stoneDark = MapKit.M("a77a4b", 0.1f, 0f, Surface.Brick);
            k.Ground(60, 60, sand);
            float H = 34;
            k.Battlement(new Vector3(-H, 0, -H), new Vector3(H, 0, -H), 4.5f, 1.2f, stone);
            k.Battlement(new Vector3(-H, 0, H), new Vector3(H, 0, H), 4.5f, 1.2f, stone);
            k.Battlement(new Vector3(-H, 0, -H), new Vector3(-H, 0, H), 4.5f, 1.2f, stone);
            k.Battlement(new Vector3(H, 0, -H), new Vector3(H, 0, H), 4.5f, 1.2f, stone);
            foreach (var c in new[] { new Vector3(-H, 0, -H), new Vector3(H, 0, -H), new Vector3(-H, 0, H), new Vector3(H, 0, H) })
            {
                Prims.Cyl(k.S, c + Vector3.up * 3.5f, 4f, 7f, stoneDark, Vector3.up, true);
                Prims.Cyl(k.S, c + Vector3.up * 7.2f, 4.6f, 0.5f, stone, Vector3.up, false);
            }
            // mud-brick houses with roofs you can climb to
            k.Sym((p, y) => House(k, p, y, new Vector3(7, 3.4f, 6), stone, stoneDark), new Vector3(-14, 0, -14), 0);
            k.Sym((p, y) => House(k, p, y, new Vector3(6, 2.8f, 8), stone, stoneDark), new Vector3(16, 0, -10), 90);
            k.Sym((p, y) => House(k, p, y, new Vector3(5, 3f, 5), stone, stoneDark), new Vector3(-24, 0, 4), 0);
            // market stalls
            string[] cloth = { "c0392b", "2980b9", "f39c12", "8e44ad", "16a085" };
            for (int i = 0; i < 4; i++) k.Sym((p, y) => Stall(k, p, y, cloth[k.Ri(0, cloth.Length)]), new Vector3(-6 + i * 4.2f, 0, -6), 0);
            // well
            Prims.Cyl(k.S, new Vector3(0, 0.5f, 0), 3f, 1f, stoneDark, Vector3.up, true);
            Prims.Cyl(k.S, new Vector3(0, 1.02f, 0), 2.4f, 0.05f, MapKit.M("2a5470", 0.9f), Vector3.up, false);
            k.Box(new Vector3(-1.3f, 1.8f, 0), new Vector3(0.2f, 1.6f, 0.2f), MapKit.M("6b4a2e"));
            k.Box(new Vector3(1.3f, 1.8f, 0), new Vector3(0.2f, 1.6f, 0.2f), MapKit.M("6b4a2e"));
            k.Box(new Vector3(0, 2.6f, 0), new Vector3(2.8f, 0.2f, 0.2f), MapKit.M("6b4a2e"), 0, false);
            for (int i = 0; i < 8; i++) k.Sym((p, y) => k.Palm(p, k.R(5, 8)), new Vector3(k.R(-30, 30), 0, k.R(-30, -3)), 0);
            for (int i = 0; i < 6; i++) k.Sym((p, y) => k.CrateStack(p, y), new Vector3(k.R(-28, 28), 0, k.R(-26, -4)), k.R(0, 90));
            for (int i = 0; i < 5; i++) k.Sym((p, y) => k.Rock(p, k.R(1, 2.2f), "b48a5a"), new Vector3(k.R(-30, 30), 0, k.R(-28, -4)), 0);
            k.Backdrop(30, 45, 110, p => { Prims.Ball(k.S, p, new Vector3(k.R(20, 40), k.R(4, 10), k.R(20, 40)), sand); return null; });
            k.DefaultSpawns(27, 21);
            k.BasePads();
            k.world.killY = -10;
        }

        static void House(MapKit k, Vector3 p, float yaw, Vector3 size, Material wall, Material trim)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            float t = 0.35f;
            // four walls with a door gap on the front
            k.BoxRot(p + rot * new Vector3(0, size.y / 2, size.z / 2), new Vector3(size.x, size.y, t), wall, rot);
            k.BoxRot(p + rot * new Vector3(-size.x / 2, size.y / 2, 0), new Vector3(t, size.y, size.z), wall, rot);
            k.BoxRot(p + rot * new Vector3(size.x / 2, size.y / 2, 0), new Vector3(t, size.y, size.z), wall, rot);
            float side = (size.x - 1.6f) / 2;
            k.BoxRot(p + rot * new Vector3(-(1.6f / 2 + side / 2), size.y / 2, -size.z / 2), new Vector3(side, size.y, t), wall, rot);
            k.BoxRot(p + rot * new Vector3(1.6f / 2 + side / 2, size.y / 2, -size.z / 2), new Vector3(side, size.y, t), wall, rot);
            k.BoxRot(p + rot * new Vector3(0, size.y - 0.4f, -size.z / 2), new Vector3(1.7f, 0.8f, t), wall, rot);
            // roof + trim
            k.BoxRot(p + rot * new Vector3(0, size.y + 0.1f, 0), new Vector3(size.x + 0.3f, 0.25f, size.z + 0.3f), trim, rot);
            // outside stairs to the roof
            k.Stairs(p + rot * new Vector3(size.x / 2 + 0.7f, 0, -size.z / 2), yaw, Mathf.CeilToInt(size.y / 0.33f), 0.33f, 0.45f, 1.2f, trim);
            // window holes (dark)
            k.BoxRot(p + rot * new Vector3(size.x / 2 + 0.01f, size.y * 0.6f, 0), new Vector3(0.05f, 0.7f, 0.9f), MapKit.M("2a2016"), rot, false);
        }

        static void Stall(MapKit k, Vector3 p, float yaw, string clothHex)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var wood = MapKit.M("7a5638", 0.15f, 0f, Surface.Plank);
            foreach (var o in new[] { new Vector3(-1.3f, 0, -1), new Vector3(1.3f, 0, -1), new Vector3(-1.3f, 0, 1), new Vector3(1.3f, 0, 1) })
                k.BoxRot(p + rot * (o + Vector3.up * 1.2f), new Vector3(0.12f, 2.4f, 0.12f), wood, rot);
            k.BoxRot(p + rot * new Vector3(0, 2.45f, 0), new Vector3(3f, 0.08f, 2.4f), MapKit.M(clothHex, 0.1f), rot * Quaternion.Euler(8, 0, 0), false);
            k.BoxRot(p + rot * new Vector3(0, 0.5f, -0.6f), new Vector3(2.6f, 1f, 0.8f), wood, rot);
            for (int i = 0; i < 4; i++)
                Prims.Ball(k.S, p + rot * new Vector3(-0.9f + i * 0.6f, 1.12f, -0.6f), Vector3.one * 0.3f, MapKit.M(k.Pick("e67e22", "f1c40f", "27ae60", "c0392b")));
        }

        /* ======================= 3 · Frostbite ======================= */
        static void Frostbite(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Overcast,
                skyTint = Prims.Hex("b8c8de"), groundTint = Prims.Hex("e8eef7"), exposure = 1.1f, sunIntensity = 0.95f,
                sun = Prims.Hex("e6efff"), sunEuler = new Vector3(30, 200, 0),
                ambSky = Prims.Hex("c7d6ea"), ambEquator = Prims.Hex("a9b7c9"), ambGround = Prims.Hex("8995a6"),
                fog = Prims.Hex("dbe5f2"), fogDensity = 0.016f
            });
            var snow = MapKit.M("eef3fb", 0.35f, 0f, Surface.Snow);
            var ice = Prims.Lit(Prims.Hex("a9dcf5"), 0.95f, 0.1f, Surface.Ice);
            var logs = MapKit.M("6e4b2f", 0.1f, 0f, Surface.Wood);
            var roofM = MapKit.M("4b3a33", 0.2f, 0f, Surface.Plank);
            k.Ground(60, 60, snow);
            k.InvisibleFence(34, 34);
            // frozen lake in the middle
            k.Box(new Vector3(0, 0.02f, 0), new Vector3(16, 0.06f, 10), ice, 0, false);
            for (int i = 0; i < 6; i++) k.Block(new Vector3(k.R(-7, 7), 0, k.R(-4, 4)), new Vector3(k.R(1, 2), k.R(0.8f, 1.6f), k.R(1, 2)), ice, k.R(0, 90));
            // cabins
            k.Sym((p, y) => Cabin(k, p, y, logs, roofM, snow), new Vector3(-15, 0, -18), 0);
            k.Sym((p, y) => Cabin(k, p, y, logs, roofM, snow), new Vector3(17, 0, -8), 90);
            // radio tower
            var steel = MapKit.M("b33a2e", 0.4f, 0.6f);
            foreach (var o in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                k.Box(new Vector3(24, 6, 22) + o, new Vector3(0.18f, 12, 0.18f), steel);
            k.Box(new Vector3(24, 12.2f, 22), new Vector3(0.5f, 0.5f, 0.5f), MapKit.Glow("ff3b30", 3f), 0, false);
            k.Box(new Vector3(-24, 6, -22), new Vector3(0.3f, 12, 0.3f), steel);
            // trees, snowbanks, rocks, fences
            for (int i = 0; i < 18; i++) k.Sym((p, y) => k.Pine(p, k.R(5, 9)), new Vector3(k.R(-31, 31), 0, k.R(-31, -6)), 0);
            for (int i = 0; i < 6; i++) k.Sym((p, y) => k.Rock(p, k.R(1.2f, 2.5f), "8892a0"), new Vector3(k.R(-30, 30), 0, k.R(-28, -5)), 0);
            for (int i = 0; i < 5; i++) k.Sym((p, y) => k.Block(p, new Vector3(3f, 1.1f, 0.5f), logs, y), new Vector3(k.R(-26, 26), 0, k.R(-24, -6)), k.R(0, 180));
            k.Backdrop(50, 40, 90, p => { k.Pine(p, k.R(8, 14)); return null; });
            k.Backdrop(16, 90, 160, p => { Prims.Ball(k.S, p, new Vector3(k.R(40, 70), k.R(25, 50), k.R(40, 70)), snow); return null; });
            k.DefaultSpawns(27, 21);
            k.BasePads();
            k.world.killY = -10;
        }

        static void Cabin(MapKit k, Vector3 p, float yaw, Material logs, Material roof, Material snow)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var size = new Vector3(6, 3, 5);
            k.BoxRot(p + rot * new Vector3(0, 1.5f, 2.5f), new Vector3(6, 3, 0.4f), logs, rot);
            k.BoxRot(p + rot * new Vector3(-3, 1.5f, 0), new Vector3(0.4f, 3, 5), logs, rot);
            k.BoxRot(p + rot * new Vector3(3, 1.5f, 0), new Vector3(0.4f, 3, 5), logs, rot);
            k.BoxRot(p + rot * new Vector3(-2, 1.5f, -2.5f), new Vector3(2, 3, 0.4f), logs, rot);
            k.BoxRot(p + rot * new Vector3(2, 1.5f, -2.5f), new Vector3(2, 3, 0.4f), logs, rot);
            for (int i = 0; i < 5; i++)
                k.BoxRot(p + rot * new Vector3(0, 0.3f + i * 0.6f, -2.72f), new Vector3(6.2f, 0.08f, 0.05f), MapKit.M("4f351f"), rot, false);
            // pitched roof from two tilted slabs, snow on top
            k.BoxRot(p + rot * new Vector3(-1.6f, 3.9f, 0), new Vector3(3.6f, 0.25f, 5.8f), roof, rot * Quaternion.Euler(0, 0, 30));
            k.BoxRot(p + rot * new Vector3(1.6f, 3.9f, 0), new Vector3(3.6f, 0.25f, 5.8f), roof, rot * Quaternion.Euler(0, 0, -30));
            k.BoxRot(p + rot * new Vector3(-1.6f, 4.05f, 0), new Vector3(3.5f, 0.08f, 5.6f), snow, rot * Quaternion.Euler(0, 0, 30), false);
            k.BoxRot(p + rot * new Vector3(1.6f, 4.05f, 0), new Vector3(3.5f, 0.08f, 5.6f), snow, rot * Quaternion.Euler(0, 0, -30), false);
            k.BoxRot(p + rot * new Vector3(2, 4.2f, 1.5f), new Vector3(0.6f, 1.6f, 0.6f), MapKit.M("7d6e66"), rot);
            k.BoxRot(p + rot * new Vector3(3.22f, 1.8f, 0), new Vector3(0.05f, 0.9f, 1.2f), MapKit.Glow("ffcf7a", 1.4f), rot, false);
        }

        /* ======================= 4 · Jungle Ruins ======================= */
        static void Jungle(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Day,
                skyTint = Prims.Hex("8fc9a8"), groundTint = Prims.Hex("4a6a3a"), exposure = 1.15f, sunIntensity = 1.1f,
                sun = Prims.Hex("fff6d6"), sunEuler = new Vector3(48, 70, 0),
                ambSky = Prims.Hex("8db59a"), ambEquator = Prims.Hex("5c7a4f"), ambGround = Prims.Hex("2f3d24"),
                fog = Prims.Hex("9fbf9a"), fogDensity = 0.014f
            });
            var grass = MapKit.M("4f7f35", 0.1f, 0f, Surface.Grass);
            var stone = MapKit.M("8f8c7a", 0.12f, 0f, Surface.Rock);
            var moss = MapKit.M("6b7f4a", 0.1f, 0f, Surface.Rock);
            k.Ground(60, 60, grass);
            k.InvisibleFence(34, 34);
            // stepped temple in the middle
            for (int i = 0; i < 3; i++)
            {
                float s = 12 - i * 3.5f;
                k.Block(new Vector3(0, i * 1.4f, 0), new Vector3(s, 1.4f, s), i == 0 ? moss : stone);
            }
            k.Stairs(new Vector3(0, 0, -8.6f), 0, 13, 0.32f, 0.5f, 2.6f, stone);
            k.Stairs(new Vector3(0, 0, 8.6f), 180, 13, 0.32f, 0.5f, 2.6f, stone);
            k.Box(new Vector3(0, 4.2f + 1.1f, 0), new Vector3(1.6f, 2.2f, 1.6f), MapKit.M("a39d84"));
            k.Box(new Vector3(0, 5.6f, 0), new Vector3(0.6f, 0.6f, 0.6f), MapKit.Glow("4cffb5", 2f), 0, false);
            // broken pillars and walls
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2 / 8;
                var p = new Vector3(Mathf.Cos(a) * 15, 0, Mathf.Sin(a) * 15);
                float h = k.R(1.5f, 5.5f);
                Prims.Cyl(k.S, p + Vector3.up * h / 2, 1.1f, h, stone, Vector3.up, true);
                Prims.Cyl(k.S, p + Vector3.up * (h + 0.15f), 1.4f, 0.3f, moss, Vector3.up, false);
            }
            k.Sym((p, y) => k.Block(p, new Vector3(6, 2.2f, 0.8f), stone, y), new Vector3(-10, 0, -22), 0);
            k.Sym((p, y) => k.Block(p, new Vector3(0.8f, 1.6f, 7), stone, y), new Vector3(-24, 0, -10), 0);
            k.Sym((p, y) => k.Block(p, new Vector3(4, 1.2f, 0.8f), moss, y), new Vector3(14, 0, -26), 20);
            // pond
            k.Box(new Vector3(-22, 0.01f, 18), new Vector3(9, 0.04f, 6), Prims.Lit(Prims.Hex("2f7d8a"), 0.95f), 0, false);
            k.Box(new Vector3(22, 0.01f, -18), new Vector3(9, 0.04f, 6), Prims.Lit(Prims.Hex("2f7d8a"), 0.95f), 0, false);
            for (int i = 0; i < 14; i++) k.Sym((p, y) => k.Tree(p, k.R(6, 11), k.Pick("2f7a35", "3d8b3a", "2a6b30")), new Vector3(k.R(-31, 31), 0, k.R(-31, -6)), 0);
            for (int i = 0; i < 14; i++) k.Sym((p, y) => k.Bush(p, k.R(0.8f, 1.6f), k.Pick("3b7a2e", "4a8f35")), new Vector3(k.R(-30, 30), 0, k.R(-30, -4)), 0);
            k.Backdrop(40, 40, 90, p => { k.Tree(p, k.R(12, 20), "2a6b30"); return null; });
            k.DefaultSpawns(27, 22);
            k.BasePads();
            k.world.killY = -10;
        }

        /* ======================= 5 · Neon City ======================= */
        static void Neon(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Night,
                skyTint = Prims.Hex("1a1440"), groundTint = Prims.Hex("0a0a14"), exposure = 0.35f, atmosphere = 0.6f, sunSize = 0.0f,
                sun = Prims.Hex("8f7dff"), sunIntensity = 0.45f, sunEuler = new Vector3(60, 30, 0),
                ambSky = Prims.Hex("3a2f7a"), ambEquator = Prims.Hex("2a2245"), ambGround = Prims.Hex("120f1c"),
                fog = Prims.Hex("2a1d4a"), fogDensity = 0.018f
            });
            var asphalt = MapKit.M("26262c", 0.55f, 0f, Surface.Asphalt);
            var walk = MapKit.M("4a4a55", 0.3f, 0f, Surface.Tile);
            k.Ground(60, 60, asphalt);
            k.InvisibleFence(34, 34);
            string[] neon = { "ff2bd6", "2bf5ff", "ffe12b", "7c2bff", "2bff8a" };
            // sidewalks and the city block edges
            k.Box(new Vector3(-17, 0.1f, 0), new Vector3(10, 0.2f, 68), walk);
            k.Box(new Vector3(17, 0.1f, 0), new Vector3(10, 0.2f, 68), walk);
            for (int i = 0; i < 4; i++)
            {
                float z = -27 + i * 18;
                k.Building(new Vector3(-29, 0, z), new Vector3(10, k.R(14, 30), 12), k.Pick("2a2a3a", "352a40", "2a3540"), neon[k.Ri(0, neon.Length)], true);
                k.Building(new Vector3(29, 0, -z), new Vector3(10, k.R(14, 30), 12), k.Pick("2a2a3a", "352a40", "2a3540"), neon[k.Ri(0, neon.Length)], true);
            }
            // lane markings
            for (int z = -30; z <= 30; z += 5) k.Box(new Vector3(0, 0.005f, z), new Vector3(0.25f, 0.01f, 2.5f), MapKit.Glow("ffe9a8", 0.6f), 0, false);
            // neon signs
            for (int i = 0; i < 6; i++)
                k.Sym((p, y) => k.Box(p, new Vector3(0.2f, 0.6f, k.R(3, 6)), MapKit.Glow(neon[k.Ri(0, neon.Length)], 3f), y, false), new Vector3(-23.9f, k.R(4, 10), -28 + i * 9), 0);
            k.Sign(new Vector3(-23.6f, 7, -10), 90, "FLOP COLA", 1.2f, Prims.Hex("ff59e3"));
            k.Sign(new Vector3(23.6f, 7, 10), -90, "RAGDOLL BAR", 1.2f, Prims.Hex("59f3ff"));
            // cars
            for (int i = 0; i < 5; i++) k.Sym((p, y) => Car(k, p, y, k.Pick("c0392b", "2980b9", "f1c40f", "ecf0f1", "8e44ad")), new Vector3(k.R(-9, 9), 0, k.R(-26, -6)), k.Pick(0f, 90f, 15f));
            // central stage
            k.Block(new Vector3(0, 0, 0), new Vector3(8, 1.2f, 6), MapKit.M("3a3346", 0.4f));
            k.Stairs(new Vector3(0, 0, -5.4f), 0, 4, 0.3f, 0.6f, 3f, walk);
            k.Stairs(new Vector3(0, 0, 5.4f), 180, 4, 0.3f, 0.6f, 3f, walk);
            k.Box(new Vector3(0, 1.25f, 0), new Vector3(7.6f, 0.05f, 5.6f), MapKit.Glow("7c2bff", 0.8f), 0, false);
            for (int i = 0; i < 6; i++) k.Sym((p, y) => k.Lamp(p, "c9a8ff", 4.5f), new Vector3(-12.2f, 0.2f, -28 + i * 10), 0);
            for (int i = 0; i < 5; i++) k.Sym((p, y) => k.Barrel(p, k.Pick("333", "2b2b55")), new Vector3(k.R(-20, 20), 0, k.R(-28, -8)), 0);
            k.Backdrop(50, 50, 140, p =>
                k.Building(new Vector3(p.x, 0, p.z), new Vector3(k.R(10, 20), k.R(25, 80), k.R(10, 20)), "1f1f2e", neon[k.Ri(0, neon.Length)], true, false));
            k.DefaultSpawns(27, 20);
            k.BasePads();
            k.world.killY = -10;
        }

        static void Car(MapKit k, Vector3 p, float yaw, string hex)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var paint = MapKit.M(hex, 0.75f, 0.4f);
            var glass = Prims.Lit(Prims.Hex("1a2433"), 0.95f, 0.2f);
            var tire = MapKit.M("151515", 0.3f);
            k.BoxRot(p + rot * new Vector3(0, 0.6f, 0), new Vector3(1.9f, 0.7f, 4.2f), paint, rot);
            k.BoxRot(p + rot * new Vector3(0, 1.2f, -0.2f), new Vector3(1.7f, 0.6f, 2.2f), glass, rot);
            foreach (var o in new[] { new Vector3(-0.9f, 0.35f, 1.3f), new Vector3(0.9f, 0.35f, 1.3f), new Vector3(-0.9f, 0.35f, -1.3f), new Vector3(0.9f, 0.35f, -1.3f) })
                Prims.Cyl(k.S, p + rot * o, 0.7f, 0.3f, tire, rot * Vector3.right, false);
            k.BoxRot(p + rot * new Vector3(0, 0.65f, 2.11f), new Vector3(1.5f, 0.15f, 0.02f), MapKit.Glow("fff6c9", 2f), rot, false);
            k.BoxRot(p + rot * new Vector3(0, 0.7f, -2.11f), new Vector3(1.5f, 0.12f, 0.02f), MapKit.Glow("ff2a2a", 2f), rot, false);
        }

        /* ======================= 6 · Shipyard ======================= */
        static void Shipyard(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Overcast,
                skyTint = Prims.Hex("7f98b0"), groundTint = Prims.Hex("3a4a55"), exposure = 1.0f, sunIntensity = 0.9f,
                sun = Prims.Hex("e8eef5"), sunEuler = new Vector3(35, -120, 0),
                ambSky = Prims.Hex("8a9cad"), ambEquator = Prims.Hex("66707a"), ambGround = Prims.Hex("343c44"),
                fog = Prims.Hex("9aa8b5"), fogDensity = 0.012f
            });
            var concrete = MapKit.M("8a8d90", 0.2f, 0f, Surface.Concrete);
            var water = Prims.Lit(Prims.Hex("1f4f6b"), 0.92f, 0.1f);
            k.Box(new Vector3(0, -3, 0), new Vector3(400, 0.1f, 400), water, 0, false);
            k.Box(new Vector3(0, -2, 0), new Vector3(52, 4, 70), concrete);
            k.Box(new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 70), MapKit.M("e8c33a"), 0, false);
            k.InvisibleFence(40, 34);
            string[] colors = { "b33a2e", "2e6db3", "2e9e57", "d9822b", "6d4c9a", "c7c7c7" };
            // container stacks
            k.Sym((p, y) => k.Container(p, y, colors[k.Ri(0, colors.Length)]), new Vector3(-12, 0, -14), 0);
            k.Sym((p, y) => k.Container(p, y, colors[k.Ri(0, colors.Length)]), new Vector3(-9.4f, 0, -14), 0);
            k.Sym((p, y) => k.Container(p, y, colors[k.Ri(0, colors.Length)]), new Vector3(-10.7f, 2.6f, -14), 0);
            k.Sym((p, y) => k.Container(p, y + 90, colors[k.Ri(0, colors.Length)]), new Vector3(8, 0, -5), 0);
            k.Sym((p, y) => k.Container(p, y + 90, colors[k.Ri(0, colors.Length)]), new Vector3(18, 0, -22), 0);
            k.Sym((p, y) => k.Ramp(p, y, 5f, 2f, 2.6f, MapKit.M("5b5e63", 0.4f, 0.6f)), new Vector3(-10.7f, 0, -22.2f), 0);
            // a crane
            var crane = MapKit.M("e5a21f", 0.4f, 0.4f);
            foreach (var x in new[] { -8f, 8f })
                foreach (var z in new[] { -2f, 2f })
                    k.Box(new Vector3(x, 7, z), new Vector3(0.6f, 14, 0.6f), crane);
            k.Box(new Vector3(0, 14.2f, 0), new Vector3(17, 0.8f, 5), crane);
            k.Box(new Vector3(-20, 14.2f, 0), new Vector3(24, 0.6f, 1.2f), crane, 0, false);
            k.Box(new Vector3(-24, 8, 0), new Vector3(0.1f, 12, 0.1f), MapKit.M("333"), 0, false);
            k.Block(new Vector3(-24, 0.8f, 0), new Vector3(2.5f, 2.6f, 6.1f), MapKit.M("2e6db3", 0.3f, 0.35f), 90);
            // the ship along one side
            var hull = MapKit.M("8b1e1e", 0.35f, 0.2f);
            var deck = MapKit.M("7a6a55", 0.2f);
            k.Box(new Vector3(31, -1.5f, 0), new Vector3(10, 6, 44), hull, 0, false);
            k.Box(new Vector3(31, 1.6f, 0), new Vector3(9.5f, 0.3f, 43), deck);
            k.Box(new Vector3(31, 4, 14), new Vector3(7, 4.5f, 7), MapKit.M("e6e6e6", 0.3f));
            k.Box(new Vector3(31, 4.3f, 10.45f), new Vector3(6, 1, 0.1f), Prims.Lit(Prims.Hex("1a2433"), 0.9f), 0, false);
            k.Ramp(new Vector3(24.5f, 0, -6), 90, 2.5f, 2.2f, 1.6f, MapKit.M("5b5e63", 0.4f, 0.6f));
            k.Ramp(new Vector3(24.5f, 0, 6), 90, 2.5f, 2.2f, 1.6f, MapKit.M("5b5e63", 0.4f, 0.6f));
            k.Railing(new Vector3(35.7f, 1.75f, -21), new Vector3(35.7f, 1.75f, 21), MapKit.M("ddd"));
            // bollards, crates, barrels
            for (int z = -32; z <= 32; z += 8) { Prims.Cyl(k.S, new Vector3(-25.5f, 0.4f, z), 0.5f, 0.8f, MapKit.M("222", 0.4f, 0.5f), Vector3.up, true); }
            for (int i = 0; i < 6; i++) k.Sym((p, y) => k.CrateStack(p, y), new Vector3(k.R(-22, 22), 0, k.R(-30, -3)), k.R(0, 90));
            for (int i = 0; i < 6; i++) k.Sym((p, y) => k.Barrel(p, k.Pick("c0392b", "2c7fb8", "e1b12c", "27ae60")), new Vector3(k.R(-22, 22), 0, k.R(-30, -3)), 0);
            k.Backdrop(12, 80, 160, p => k.Building(p + Vector3.down * 3, new Vector3(k.R(10, 30), k.R(8, 25), k.R(10, 30)), "6f7a85", "aac4d9", false, false));
            k.DefaultSpawns(28, 18);
            k.BasePads();
            k.world.killY = -1.5f;
        }

        /* ======================= 7 · Foundry ======================= */
        static void Foundry(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Sunset,
                skyTint = Prims.Hex("5a4a40"), groundTint = Prims.Hex("2a2220"), exposure = 0.75f, sunIntensity = 0.7f,
                sun = Prims.Hex("ffc79a"), sunEuler = new Vector3(55, 10, 0),
                ambSky = Prims.Hex("6a5548"), ambEquator = Prims.Hex("4a3d36"), ambGround = Prims.Hex("2a1f1a"),
                fog = Prims.Hex("4a382c"), fogDensity = 0.02f
            });
            var floor = MapKit.M("4a4c50", 0.45f, 0.6f);
            var steel = MapKit.M("6a6d72", 0.5f, 0.7f);
            var rust = MapKit.M("8a4a2a", 0.3f, 0.4f);
            var lava = MapKit.Glow("ff6a00", 3f);
            k.Box(new Vector3(-19, -0.5f, 0), new Vector3(32, 1, 72), floor);
            k.Box(new Vector3(19, -0.5f, 0), new Vector3(32, 1, 72), floor);
            k.Box(new Vector3(0, -6.5f, 0), new Vector3(6, 1, 72), lava, 0, false);
            k.Box(new Vector3(0, -5.9f, 0), new Vector3(6, 0.2f, 72), lava, 0, false);
            k.Perimeter(35, 35, 9f, MapKit.M("3a3c40", 0.3f, 0.4f));
            // each team's base sits on a deck over the molten channel
            k.Box(new Vector3(0, -0.15f, -30), new Vector3(6.4f, 0.3f, 10), steel);
            k.Box(new Vector3(0, -0.15f, 30), new Vector3(6.4f, 0.3f, 10), steel);
            // bridges over the molten channel
            foreach (var z in new[] { -18f, 0f, 18f })
            {
                k.Box(new Vector3(0, -0.15f, z), new Vector3(6.4f, 0.3f, 3f), steel);
                k.Railing(new Vector3(-3.2f, 0, z - 1.5f), new Vector3(3.2f, 0, z - 1.5f), rust);
                k.Railing(new Vector3(-3.2f, 0, z + 1.5f), new Vector3(3.2f, 0, z + 1.5f), rust);
            }
            // tanks
            k.Sym((p, y) => { Prims.Cyl(k.S, p + Vector3.up * 4, 5f, 8f, rust, Vector3.up, true); Prims.Cyl(k.S, p + Vector3.up * 8.2f, 5.3f, 0.4f, steel, Vector3.up, false); }, new Vector3(-24, 0, -22), 0);
            k.Sym((p, y) => { Prims.Cyl(k.S, p + Vector3.up * 3, 4f, 6f, steel, Vector3.up, true); }, new Vector3(24, 0, -8), 0);
            // catwalks with stairs
            k.Sym((p, y) =>
            {
                k.Box(p + Vector3.up * 3, new Vector3(3, 0.25f, 16), steel, y);
                k.Railing(p + new Vector3(-1.5f, 3.1f, -8), p + new Vector3(-1.5f, 3.1f, 8), rust);
                foreach (var dz in new[] { -7f, 0f, 7f }) k.Box(p + new Vector3(1.2f, 1.5f, dz), new Vector3(0.25f, 3f, 0.25f), rust);
            }, new Vector3(-12, 0, -14), 0);
            k.Sym((p, y) => k.Stairs(p, y, 9, 0.33f, 0.5f, 1.6f, steel), new Vector3(-12, 0, -26.6f), 0);
            // pipes along the walls and overhead
            for (int i = 0; i < 4; i++)
            {
                Prims.Cyl(k.S, new Vector3(-34.2f, 1.5f + i * 1.6f, 0), 0.6f, 70, i % 2 == 0 ? rust : steel, Vector3.forward, false);
                Prims.Cyl(k.S, new Vector3(34.2f, 1.5f + i * 1.6f, 0), 0.6f, 70, i % 2 == 0 ? steel : rust, Vector3.forward, false);
            }
            for (int z = -30; z <= 30; z += 12) Prims.Cyl(k.S, new Vector3(0, 7.5f, z), 0.8f, 70, steel, Vector3.right, false);
            // machines
            k.Sym((p, y) => Machine(k, p, y, steel, rust), new Vector3(-20, 0, 2), 0);
            k.Sym((p, y) => Machine(k, p, y, steel, rust), new Vector3(-8, 0, -30), 90);
            for (int i = 0; i < 5; i++) k.Sym((p, y) => k.CrateStack(p, y), new Vector3(k.R(-30, -6), 0, k.R(-28, 28)), k.R(0, 90));
            for (int i = 0; i < 5; i++) k.Sym((p, y) => k.Barrel(p, k.Pick("c0392b", "e1b12c", "555")), new Vector3(k.R(-30, -6), 0, k.R(-28, 28)), 0);
            for (int z = -30; z <= 30; z += 10) { k.Box(new Vector3(-15, 8.6f, z), new Vector3(0.6f, 0.4f, 0.6f), MapKit.Glow("ffd28a", 2.2f), 0, false); k.Box(new Vector3(15, 8.6f, z), new Vector3(0.6f, 0.4f, 0.6f), MapKit.Glow("ffd28a", 2.2f), 0, false); }
            k.DefaultSpawns(27, 0);
            k.world.ffaSpawns.Clear();
            for (int i = 0; i < 12; i++) k.world.ffaSpawns.Add(new Vector3((i % 2 == 0 ? -1 : 1) * k.R(8, 28), 0, k.R(-28, 28)));
            k.BasePads();
            k.world.killY = -3f;
        }

        static void Machine(MapKit k, Vector3 p, float yaw, Material steel, Material rust)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            k.BoxRot(p + rot * new Vector3(0, 1.2f, 0), new Vector3(4, 2.4f, 3), steel, rot);
            k.BoxRot(p + rot * new Vector3(0, 2.8f, 0.4f), new Vector3(2.4f, 0.8f, 1.6f), rust, rot);
            k.BoxRot(p + rot * new Vector3(1.4f, 1.6f, -1.51f), new Vector3(0.6f, 0.4f, 0.02f), MapKit.Glow("32ff6a", 2f), rot, false);
            Prims.Cyl(k.S, p + rot * new Vector3(-1.2f, 4.2f, 0), 0.5f, 3f, steel, Vector3.up, false);
        }

        /* ======================= 8 · Castle Keep ======================= */
        static void Castle(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Day,
                skyTint = Prims.Hex("6f9ad1"), groundTint = Prims.Hex("4a5a3a"), exposure = 1.3f, sunIntensity = 1.2f,
                sun = Prims.Hex("fff4dc"), sunEuler = new Vector3(45, -150, 0),
                ambSky = Prims.Hex("9ab4d6"), ambEquator = Prims.Hex("7a8a6a"), ambGround = Prims.Hex("3a3f2a"),
                fog = Prims.Hex("b9cbe0"), fogDensity = 0.006f
            });
            var grass = MapKit.M("5e8c3a", 0.1f, 0f, Surface.Grass);
            var stone = MapKit.M("9a9a96", 0.15f, 0f, Surface.Brick);
            var stoneDark = MapKit.M("7a7a76", 0.15f, 0f, Surface.Brick);
            var wood = MapKit.M("7a5638", 0.15f, 0f, Surface.Plank);
            k.Ground(80, 80, grass);
            float H = 33;
            k.Battlement(new Vector3(-H, 0, -H), new Vector3(H, 0, -H), 5f, 1.6f, stone);
            k.Battlement(new Vector3(-H, 0, H), new Vector3(H, 0, H), 5f, 1.6f, stone);
            k.Battlement(new Vector3(-H, 0, -H), new Vector3(-H, 0, H), 5f, 1.6f, stone);
            k.Battlement(new Vector3(H, 0, -H), new Vector3(H, 0, H), 5f, 1.6f, stone);
            foreach (var c in new[] { new Vector3(-H, 0, -H), new Vector3(H, 0, -H), new Vector3(-H, 0, H), new Vector3(H, 0, H) })
            {
                Prims.Cyl(k.S, c + Vector3.up * 4.5f, 5f, 9f, stoneDark, Vector3.up, true);
                Prims.Cyl(k.S, c + Vector3.up * 9.4f, 5.6f, 0.8f, stone, Vector3.up, false);
                Prims.Cyl(k.S, c + Vector3.up * 11f, 3f, 2.4f, MapKit.M("8a2f2f", 0.2f), Vector3.up, false);
            }
            // central keep you can walk through and climb
            var keep = new Vector3(0, 0, 0);
            k.Block(keep + new Vector3(-4, 0, 0), new Vector3(2, 6, 10), stone);
            k.Block(keep + new Vector3(4, 0, 0), new Vector3(2, 6, 10), stone);
            k.Box(keep + new Vector3(0, 6.15f, 0), new Vector3(10, 0.3f, 10), stoneDark);
            for (int i = -2; i <= 2; i++) { k.Box(new Vector3(i * 2, 6.7f, 4.8f), new Vector3(1, 0.8f, 0.5f), stone); k.Box(new Vector3(i * 2, 6.7f, -4.8f), new Vector3(1, 0.8f, 0.5f), stone); }
            k.Ramp(new Vector3(-6.2f, 0, -9), 0, 11f, 2f, 6.15f, wood);
            k.Ramp(new Vector3(6.2f, 0, 9), 180, 11f, 2f, 6.15f, wood);
            // team banners at each gate
            for (int t = 0; t < 2; t++)
            {
                float z = t == 0 ? -H + 1.2f : H - 1.2f;
                var m = Prims.Lit(Avatar.TeamColor(t), 0.2f);
                k.Box(new Vector3(-4, 3, z), new Vector3(1.6f, 3.5f, 0.1f), m, 0, false);
                k.Box(new Vector3(4, 3, z), new Vector3(1.6f, 3.5f, 0.1f), m, 0, false);
            }
            // hay, carts, training dummies, wells
            k.Sym((p, y) => { Prims.Cyl(k.S, p + Vector3.up * 0.6f, 1.2f, 1.6f, MapKit.M("d9b55a", 0.05f), Vector3.right, true); }, new Vector3(-12, 0, -18), 0);
            k.Sym((p, y) => { Prims.Cyl(k.S, p + Vector3.up * 0.6f, 1.2f, 1.6f, MapKit.M("d9b55a", 0.05f), Vector3.forward, true); }, new Vector3(16, 0, -14), 0);
            k.Sym((p, y) => Cart(k, p, y, wood), new Vector3(-20, 0, -8), 30);
            k.Sym((p, y) => Cart(k, p, y, wood), new Vector3(10, 0, -24), -20);
            k.Sym((p, y) => { k.Box(p + Vector3.up * 0.9f, new Vector3(0.15f, 1.8f, 0.15f), wood); k.Box(p + Vector3.up * 1.4f, new Vector3(1.2f, 0.15f, 0.15f), wood, y); Prims.Ball(k.S, p + Vector3.up * 1.95f, Vector3.one * 0.45f, MapKit.M("cdb27a")); }, new Vector3(20, 0, -26), 0);
            k.Sym((p, y) => k.Block(p, new Vector3(5, 1.4f, 0.8f), stoneDark, y), new Vector3(-20, 0, -22), 0);
            k.Sym((p, y) => k.Block(p, new Vector3(0.8f, 1.4f, 5), stoneDark, y), new Vector3(-24, 0, 6), 0);
            for (int i = 0; i < 6; i++) k.Sym((p, y) => k.CrateStack(p, y), new Vector3(k.R(-28, 28), 0, k.R(-28, -6)), k.R(0, 90));
            k.Backdrop(40, 45, 120, p => { k.Tree(p, k.R(8, 14), "3d7a35"); return null; });
            k.DefaultSpawns(27, 20);
            k.BasePads();
            k.world.killY = -10;
        }

        static void Cart(MapKit k, Vector3 p, float yaw, Material wood)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            k.BoxRot(p + rot * new Vector3(0, 0.9f, 0), new Vector3(1.8f, 0.6f, 3f), wood, rot);
            Prims.Cyl(k.S, p + rot * new Vector3(-1f, 0.6f, 0.8f), 1.2f, 0.15f, MapKit.M("4f351f"), rot * Vector3.right, false);
            Prims.Cyl(k.S, p + rot * new Vector3(1f, 0.6f, 0.8f), 1.2f, 0.15f, MapKit.M("4f351f"), rot * Vector3.right, false);
            Prims.Cyl(k.S, p + rot * new Vector3(-1f, 0.6f, -0.8f), 1.2f, 0.15f, MapKit.M("4f351f"), rot * Vector3.right, false);
            Prims.Cyl(k.S, p + rot * new Vector3(1f, 0.6f, -0.8f), 1.2f, 0.15f, MapKit.M("4f351f"), rot * Vector3.right, false);
        }

        /* ======================= 9 · Moon Base ======================= */
        static void Moon(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Space,
                skyTint = Color.black, groundTint = Color.black, atmosphere = 0f, exposure = 0.6f, sunSize = 0.03f,
                sun = Prims.Hex("ffffff"), sunIntensity = 1.4f, sunEuler = new Vector3(25, 60, 0),
                ambSky = Prims.Hex("4a4f66"), ambEquator = Prims.Hex("3a3d48"), ambGround = Prims.Hex("1f2026"),
                fog = Color.black, fogDensity = 0f
            });
            k.world.gravity = 0.35f;
            var dust = MapKit.M("9a9a9e", 0.08f, 0f, Surface.Sand);
            var white = MapKit.M("e8ecf2", 0.5f, 0.2f);
            var panel = Prims.Lit(Prims.Hex("1f3f8a"), 0.9f, 0.5f);
            k.Ground(80, 80, dust);
            k.InvisibleFence(34, 34, 20f);
            // craters
            for (int i = 0; i < 6; i++)
                k.Sym((p, y) =>
                {
                    float s = k.R(4, 8);
                    for (int j = 0; j < 10; j++)
                    {
                        float a = j * Mathf.PI * 2 / 10;
                        k.BoxRot(p + new Vector3(Mathf.Cos(a) * s * 0.5f, 0.2f, Mathf.Sin(a) * s * 0.5f), new Vector3(s * 0.4f, 0.6f, 0.8f), MapKit.M("8a8a8e", 0.08f), Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, k.R(-10, 10)));
                    }
                    k.Box(p + Vector3.up * 0.01f, new Vector3(s * 0.7f, 0.02f, s * 0.7f), MapKit.M("7a7a80", 0.05f), 0, false);
                }, new Vector3(k.R(-28, 28), 0, k.R(-28, -6)), 0);
            // habitat domes and modules
            k.Sym((p, y) => { Prims.Sphere(k.S, p, 9f, white, true); }, new Vector3(-16, 0, -18), 0);
            k.Sym((p, y) => { Prims.Capsule(k.S, p + Vector3.up * 1.4f, 2.8f, 10f, white, Vector3.right, true); }, new Vector3(-6, 0, -22), 0);
            k.Sym((p, y) => { Prims.Capsule(k.S, p + Vector3.up * 1.4f, 2.8f, 8f, white, Vector3.forward, true); }, new Vector3(18, 0, -6), 0);
            k.Sym((p, y) => k.Box(p + Vector3.up * 1.4f, new Vector3(1.4f, 0.4f, 0.05f), MapKit.Glow("4cc9ff", 2f), y, false), new Vector3(-6, 0, -20.6f), 0);
            // solar arrays
            for (int i = 0; i < 4; i++)
                k.Sym((p, y) =>
                {
                    k.Box(p + Vector3.up * 0.7f, new Vector3(0.15f, 1.4f, 0.15f), MapKit.M("555", 0.5f, 0.7f));
                    k.BoxRot(p + Vector3.up * 1.5f, new Vector3(3f, 0.06f, 1.8f), panel, Quaternion.Euler(25, y, 0));
                }, new Vector3(-26 + i * 4, 0, -28), 0);
            // landing pad in the middle with a rover
            Prims.Cyl(k.S, new Vector3(0, 0.15f, 0), 12f, 0.3f, MapKit.M("5a5a60", 0.3f, 0.4f), Vector3.up, true);
            Prims.Cyl(k.S, new Vector3(0, 0.31f, 0), 9f, 0.02f, MapKit.Glow("ffb02e", 0.8f), Vector3.up, false);
            Rover(k, new Vector3(3, 0.3f, 2), 30);
            Rover(k, new Vector3(-3, 0.3f, -2), 210);
            // antenna dish
            k.Box(new Vector3(26, 3, 24), new Vector3(0.3f, 6, 0.3f), MapKit.M("bbb", 0.5f, 0.6f));
            Prims.Cyl(k.S, new Vector3(26, 6.2f, 24), 4f, 0.3f, white, new Vector3(0.3f, 1, 0.2f), false);
            for (int i = 0; i < 5; i++) k.Sym((p, y) => k.Rock(p, k.R(1.2f, 2.6f), "77777c"), new Vector3(k.R(-30, 30), 0, k.R(-28, -4)), 0);
            // stars and the Earth
            var star = Prims.Unlit(Color.white);
            for (int i = 0; i < 220; i++)
            {
                var d = UnityEngine.Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.9f + 0.1f;
                Prims.Sphere(k.S, d.normalized * 350f, k.R(0.6f, 1.6f), star);
            }
            var earth = Prims.Sphere(k.S, new Vector3(-120, 140, 260), 70f, Prims.Lit(Prims.Hex("2a6bd1"), 0.6f));
            Prims.Ball(earth.transform, new Vector3(0.2f, 0.1f, -0.2f), new Vector3(0.5f, 0.4f, 0.7f), Prims.Lit(Prims.Hex("3f9f4a"), 0.3f));
            Prims.Ball(earth.transform, new Vector3(-0.2f, -0.2f, -0.25f), new Vector3(0.4f, 0.3f, 0.5f), Prims.Lit(Prims.Hex("3f9f4a"), 0.3f));
            k.DefaultSpawns(27, 20);
            k.BasePads();
            k.world.killY = -10;
        }

        static void Rover(MapKit k, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            k.BoxRot(p + rot * new Vector3(0, 0.9f, 0), new Vector3(2f, 0.5f, 3.2f), MapKit.M("d0d4da", 0.4f, 0.3f), rot);
            k.BoxRot(p + rot * new Vector3(0, 1.5f, -0.5f), new Vector3(1.6f, 0.7f, 1.4f), Prims.Lit(Prims.Hex("1a2433"), 0.9f), rot);
            foreach (var o in new[] { new Vector3(-1.1f, 0.45f, 1.1f), new Vector3(1.1f, 0.45f, 1.1f), new Vector3(-1.1f, 0.45f, -1.1f), new Vector3(1.1f, 0.45f, -1.1f) })
                Prims.Cyl(k.S, p + rot * o, 0.9f, 0.35f, MapKit.M("333", 0.2f), rot * Vector3.right, false);
        }

        /* ======================= Training range (tutorial) ======================= */
        static void Training(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme { fogDensity = 0.004f, sky = SkyStyle.Day, sunEuler = new Vector3(40, -30, 0) });
            var floor = MapKit.M("8a7a5e", 0.1f, 0f, Surface.Dirt);
            k.Ground(30, 30, floor);
            k.Box(new Vector3(0, 0.02f, 2), new Vector3(12, 0.05f, 12), MapKit.M("a3a199", 0.25f, 0f, Surface.Concrete), 0, false);
            k.InvisibleFence(16, 16);
            var wood = MapKit.M("6b5536", 0.2f, 0f, Surface.Plank);
            k.Block(new Vector3(0, 0, 3), new Vector3(6, 1f, 0.8f), wood);
            MilitaryProps.Sandbags(k, new Vector3(-5, 0, 4.2f), new Vector3(5, 0, 4.2f), 2);
            MilitaryProps.Hesco(k, new Vector3(-15, 0, -15), new Vector3(-15, 0, 15), 1.6f);
            MilitaryProps.Hesco(k, new Vector3(15, 0, -15), new Vector3(15, 0, 15), 1.6f);
            MilitaryProps.Hesco(k, new Vector3(-15, 0, 15), new Vector3(15, 0, 15), 1.6f);
            MilitaryProps.Watchtower(k, new Vector3(-12, 0, 12), 135);
            MilitaryProps.AmmoCrate(k, new Vector3(3.5f, 1f, 3), 0);
            MilitaryProps.FloodLight(k, new Vector3(10, 0, -6), -40);
            k.Sign(new Vector3(0, 3.2f, 14.4f), 180, "BASIC TRAINING", 0.6f, new Color(0.95f, 0.85f, 0.45f));
            for (int i = 0; i < 3; i++)
            {
                var t = ShootingTarget.Create(k.D, new Vector3(-4 + i * 4, 0, 10 + i * 3));
                t.name = "target" + i;
            }
            for (int i = 0; i < 6; i++) k.Tree(new Vector3(k.R(-15, 15), 0, k.R(14, 18)), k.R(5, 8), "3d8b3a");
            k.Block(new Vector3(-6, 0, 6), new Vector3(1.5f, 0.6f, 1.5f), MapKit.M("b07a45"));
            k.Block(new Vector3(6, 0, 6), new Vector3(1.5f, 1.0f, 1.5f), MapKit.M("b07a45"));
            k.world.ffaSpawns.Add(new Vector3(0, 0, 0));
            k.world.killY = -10;
        }
    }
}
