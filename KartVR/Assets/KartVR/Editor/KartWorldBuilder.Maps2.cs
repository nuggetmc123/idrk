using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// More maps: Night City Race, Rooftop Bumper, Graveyard Tag, The Backrooms (tag + build) and Island Free Roam.
    /// </summary>
    public static partial class KartWorldBuilder
    {
        // ================================================================== Night City Race

        static readonly Circuit CityCircuit = new Circuit
        {
            name = "CityTrack", title = "CITY RACE", mode = KartMode.CityRace, origin = KartMaps.Get(KartMode.CityRace).center, seed = 33,
            // A wide, squared-off loop like city blocks, with a dip on the back straight (corners stay 40 m+ in radius).
            curve = t =>
            {
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                return new Vector3(150f * c - 25f * c * c * c + 10f * Mathf.Sin(2f * t), 0f, 105f * s - 25f * s * s * s - 14f * Mathf.Cos(2f * t) * s * s);
            },
            groundSize = new Vector2(380f, 300f),
            ground = () => Mat("CityGround", new Color(0.2f, 0.2f, 0.23f), 0.4f), wallA = () => rampBlue, wallB = () => wallWhite, boardGlow = () => neonPink,
            scenery = CityScenery,
        };

        /// <summary>Tall dark towers with glowing windows and neon roof lines, street lights along the track.</summary>
        static void CityScenery(Transform race, Circuit c, System.Random rng)
        {
            float start = -Mathf.PI / 2f;
            Grandstand(race, TrackPoint(start) - c.origin + new Vector3(0f, 0f, -17f), rng);
            Material[] towers =
            {
                Mat("TowerA", new Color(0.16f, 0.17f, 0.22f), 0.7f, 0.3f), Mat("TowerB", new Color(0.24f, 0.25f, 0.3f), 0.6f, 0.2f),
                Mat("TowerC", new Color(0.12f, 0.14f, 0.2f), 0.8f, 0.4f),
            };
            var window = Mat("Window", new Color(1f, 0.85f, 0.55f), 0.5f, 0f, null, null, new Color(1f, 0.8f, 0.45f) * 1.6f);
            Material[] neons = { neonPink, neonCyan, glowRace };
            for (int i = 0; i < 200; i++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 360f, 0f, ((float)rng.NextDouble() - 0.5f) * 280f);
                float d = DistanceToTrack(p + c.origin);
                float w = 10f + (float)rng.NextDouble() * 12f, depth = 10f + (float)rng.NextDouble() * 12f;
                if (d < Mathf.Max(w, depth) * 0.75f + 12f) continue;
                if (Mathf.Abs(p.x) < 22f && p.z < -70f && p.z > -105f) continue; // grandstand
                if (Mathf.Abs(p.x) < 9f && Mathf.Abs(p.z) < 7f) continue; // info board
                float h = 14f + (float)rng.NextDouble() * (d > 50f ? 70f : 35f);
                var t = Group("Tower", race, p, Quaternion.Euler(0f, (float)rng.Next(4) * 90f, 0f));
                Prim(PrimitiveType.Cube, "Body", t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, depth), towers[rng.Next(towers.Length)]);
                Prim(PrimitiveType.Cube, "RoofNeon", t, new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 0.3f, 0.25f, depth + 0.3f), neons[rng.Next(neons.Length)], false);
                // A few rows of lit windows on two sides.
                for (int f = 1; f < h / 4f; f++)
                    for (int k = 0; k < 3; k++)
                    {
                        if (rng.NextDouble() < 0.45) continue;
                        float x = (k - 1) * w * 0.3f;
                        Prim(PrimitiveType.Cube, "Win", t, new Vector3(x, f * 4f, -depth * 0.5f - 0.05f), new Vector3(w * 0.18f, 1.6f, 0.1f), window, false);
                        Prim(PrimitiveType.Cube, "Win", t, new Vector3(-w * 0.5f - 0.05f, f * 4f, x * depth / w), new Vector3(0.1f, 1.6f, depth * 0.18f), window, false);
                    }
            }
            // Street lights along the track.
            for (int i = 0; i < 40; i++)
            {
                float t = i * Mathf.PI * 2f / 40f;
                Vector3 side = Vector3.Cross(Vector3.up, TrackTangent(t)) * (i % 2 == 0 ? 9.5f : -9.5f);
                Lamp(race, TrackPoint(t) - c.origin + side);
            }
        }

        // ================================================================== Rooftop Bumper

        static Transform BuildRooftop()
        {
            var r = Group("Rooftop", null, KartMaps.Get(KartMode.RooftopBumper).center);
            var roof = Mat("RoofTop", new Color(0.42f, 0.43f, 0.46f), 0.3f);
            var concrete = Mat("Concrete", new Color(0.6f, 0.6f, 0.62f), 0.2f);
            var glass = Mat("TowerGlass", new Color(0.35f, 0.5f, 0.7f), 0.9f, 0.4f);
            // The roof (top at y = 0) on top of a tall glass tower.
            Prim(PrimitiveType.Cube, "Roof", r, new Vector3(0f, -0.5f, 0f), new Vector3(52f, 1f, 52f), roof);
            Prim(PrimitiveType.Cube, "Tower", r, new Vector3(0f, -41f, 0f), new Vector3(50f, 80f, 50f), glass, false);
            for (int i = -2; i <= 2; i++)
            {
                Prim(PrimitiveType.Cube, "EdgeLine", r, new Vector3(i * 12f, 0.01f, 0f), new Vector3(0.2f, 0.01f, 50f), lineWhite, false);
            }
            // Helipad in the middle.
            Prim(PrimitiveType.Cylinder, "Helipad", r, new Vector3(0f, 0.02f, 0f), new Vector3(16f, 0.02f, 16f), concrete, false);
            Prim(PrimitiveType.Cylinder, "HelipadRing", r, new Vector3(0f, 0.025f, 0f), new Vector3(14f, 0.02f, 14f), hazard, false);
            Prim(PrimitiveType.Cylinder, "HelipadInner", r, new Vector3(0f, 0.03f, 0f), new Vector3(13f, 0.02f, 13f), concrete, false);
            foreach (var p in new[] { new Vector3(-2f, 0.035f, 0f), new Vector3(2f, 0.035f, 0f) })
                Prim(PrimitiveType.Cube, "H", r, p, new Vector3(0.9f, 0.02f, 6f), lineWhite, false);
            Prim(PrimitiveType.Cube, "Hbar", r, new Vector3(0f, 0.035f, 0f), new Vector3(4f, 0.02f, 0.9f), lineWhite, false);
            // A crane arm sweeping round slowly (it shoves you).
            Prim(PrimitiveType.Cylinder, "CraneBase", r, new Vector3(0f, 0.8f, 0f), new Vector3(2.4f, 0.8f, 2.4f), metal);
            var crane = new GameObject("Crane");
            crane.transform.SetParent(r, false);
            var crb = crane.AddComponent<Rigidbody>();
            crb.isKinematic = true;
            crb.interpolation = RigidbodyInterpolation.Interpolate;
            crane.AddComponent<Rotator>().degreesPerSecond = 18f;
            Prim(PrimitiveType.Cube, "Arm", crane.transform, new Vector3(0f, 0.6f, 11f), new Vector3(0.7f, 0.6f, 20f), hazard);
            // AC units and vents as obstacles; two bouncy vents.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 18f, 0f, Mathf.Cos(a) * 18f);
                Prim(PrimitiveType.Cube, "ACUnit", r, p + Vector3.up * 0.8f, new Vector3(3f, 1.6f, 2.2f), metal, true, Quaternion.Euler(0f, i * 60f, 0f));
                Prim(PrimitiveType.Cylinder, "Fan", r, p + Vector3.up * 1.62f, new Vector3(1.6f, 0.02f, 1.6f), dark, false);
            }
            foreach (var p in new[] { new Vector3(-13f, 0f, 13f), new Vector3(13f, 0f, -13f) })
            {
                var b = Group("Vent", r, p);
                b.gameObject.AddComponent<Bouncer>();
                Prim(PrimitiveType.Cylinder, "VentBody", b, new Vector3(0f, 0.6f, 0f), new Vector3(2.6f, 0.6f, 2.6f), hazard);
                Prim(PrimitiveType.Cylinder, "VentGlow", b, new Vector3(0f, 1.25f, 0f), new Vector3(2.7f, 0.05f, 2.7f), neonCyan, false);
            }
            // Water tower in a corner (just to look at; sits on the corner, off the playing area).
            var wt = Group("WaterTower", r, new Vector3(24f, 0f, 24f));
            foreach (var x in new[] { -1.2f, 1.2f }) foreach (var z in new[] { -1.2f, 1.2f })
                Prim(PrimitiveType.Cylinder, "Leg", wt, new Vector3(x, 2f, z), new Vector3(0.25f, 2f, 0.25f), metal);
            Prim(PrimitiveType.Cylinder, "Tank", wt, new Vector3(0f, 5.5f, 0f), new Vector3(3.6f, 1.6f, 3.6f), wood);
            Prim(PrimitiveType.Cylinder, "Cap", wt, new Vector3(0f, 7.4f, 0f), new Vector3(3.8f, 0.3f, 3.8f), dark);
            // The city far below.
            var rng = new System.Random(19);
            var below = new[] { Mat("CityTowerA", new Color(0.55f, 0.6f, 0.68f), 0.6f, 0.3f), Mat("CityTowerB", new Color(0.7f, 0.68f, 0.62f), 0.3f), glass };
            Prim(PrimitiveType.Cube, "Street", r, new Vector3(0f, -82f, 0f), new Vector3(300f, 1f, 300f), Mat("CityStreet", new Color(0.3f, 0.3f, 0.32f), 0.3f), false);
            for (int i = 0; i < 70; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = 40f + (float)rng.NextDouble() * 100f;
                float h = 20f + (float)rng.NextDouble() * 70f, w = 12f + (float)rng.NextDouble() * 14f;
                Prim(PrimitiveType.Cube, "CityTower", r, new Vector3(Mathf.Sin(a) * d, -82f + h * 0.5f, Mathf.Cos(a) * d), new Vector3(w, h, w), below[rng.Next(below.Length)], false);
            }

            var spawn = new GameObject("RooftopSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.RooftopBumper;
            spawn.killY = -6f;
            spawn.transform.SetParent(r, false);
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f + 15f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 11f, 0f, Mathf.Cos(a) * 11f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = pos;
                slot.localRotation = Quaternion.LookRotation(-pos, Vector3.up);
            }
            var board = Board(r, "RooftopBoard", new Vector3(0f, 9f, 27f), Quaternion.identity, new Vector2(14f, 8f), 4f, "ROOFTOP BUMPER", neonCyan);
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Bumper;
            info.text = board;
            info.title = "ROOFTOP BUMPER";
            return r;
        }

        // ================================================================== Graveyard Tag

        static Transform BuildGraveyard()
        {
            var g = Group("Graveyard", null, KartMaps.Get(KartMode.GraveyardTag).center);
            var rng = new System.Random(31);
            var deadGrass = Mat("DeadGrass", new Color(0.18f, 0.24f, 0.14f), 0.1f);
            var tomb = Mat("Tombstone", new Color(0.5f, 0.5f, 0.53f), 0.2f, 0f, null);
            var iron = Mat("Iron", new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.6f);
            var deadWood = Mat("DeadWood", new Color(0.18f, 0.13f, 0.1f), 0.1f);
            var pumpkin = Mat("Pumpkin", new Color(1f, 0.45f, 0.05f), 0.5f);
            var candle = Mat("Candle", new Color(1f, 0.75f, 0.3f), 0.5f, 0f, null, null, new Color(1f, 0.6f, 0.2f) * 2.5f);

            Prim(PrimitiveType.Cube, "Ground", g, new Vector3(0f, -0.5f, 0f), new Vector3(300f, 1f, 300f), deadGrass);
            // Winding dirt paths.
            var dirt = Mat("Dirt", new Color(0.3f, 0.22f, 0.15f), 0.1f);
            Prim(PrimitiveType.Cube, "Path", g, new Vector3(0f, 0.01f, 0f), new Vector3(8f, 0.02f, 220f), dirt, false);
            Prim(PrimitiveType.Cube, "Path", g, new Vector3(0f, 0.012f, 0f), new Vector3(220f, 0.02f, 8f), dirt, false);
            // Iron fence round the edge.
            for (int i = 0; i < 180; i++)
            {
                float a = i * 2f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cube, "FenceBar", g, new Vector3(Mathf.Sin(a) * 112f, 1.3f, Mathf.Cos(a) * 112f), new Vector3(0.12f, 2.6f, 0.12f), iron);
            }
            // Rows of tombstones (solid, good for dodging round), with a few crosses.
            for (int i = 0; i < 160; i++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 190f, 0f, ((float)rng.NextDouble() - 0.5f) * 190f);
                if (p.magnitude > 100f || p.magnitude < 22f || Mathf.Abs(p.x) < 6f || Mathf.Abs(p.z) < 6f) continue;
                if (Mathf.Abs(p.magnitude - 80f) < 6f) continue; // keep the spawn ring clear
                var t = Group("Grave", g, p, Quaternion.Euler(0f, (float)rng.NextDouble() * 20f - 10f, (float)rng.NextDouble() * 8f - 4f));
                if (rng.NextDouble() < 0.2)
                {
                    Prim(PrimitiveType.Cube, "CrossV", t, new Vector3(0f, 1.1f, 0f), new Vector3(0.3f, 2.2f, 0.3f), tomb);
                    Prim(PrimitiveType.Cube, "CrossH", t, new Vector3(0f, 1.5f, 0f), new Vector3(1.2f, 0.3f, 0.3f), tomb, false);
                }
                else
                {
                    Prim(PrimitiveType.Cube, "Stone", t, new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 1.4f, 0.35f), tomb);
                    Prim(PrimitiveType.Cylinder, "StoneTop", t, new Vector3(0f, 1.4f, 0f), new Vector3(1.4f, 0.175f, 1.4f), tomb, false, Quaternion.Euler(90f, 0f, 0f));
                }
            }
            // A crypt in the middle with ramps up to its roof.
            var crypt = Group("Crypt", g, Vector3.zero);
            Prim(PrimitiveType.Cube, "CryptBody", crypt, new Vector3(0f, 2f, 0f), new Vector3(16f, 4f, 16f), tomb);
            Prim(PrimitiveType.Cube, "CryptRoof", crypt, new Vector3(0f, 4.15f, 0f), new Vector3(17f, 0.3f, 17f), dark);
            foreach (float x in new[] { -6f, -2f, 2f, 6f })
                Prim(PrimitiveType.Cylinder, "Column", crypt, new Vector3(x, 2f, -8.6f), new Vector3(0.8f, 2f, 0.8f), tomb);
            Prim(PrimitiveType.Cube, "Door", crypt, new Vector3(0f, 1.4f, -8.05f), new Vector3(3f, 2.8f, 0.2f), dark, false);
            Ramp(crypt, "CryptRampE", new Vector3(17.5f, 0f, 0f), -90f, 16f, 6f, 18f, Mat("Stone2", new Color(0.4f, 0.4f, 0.42f), 0.2f));
            Ramp(crypt, "CryptRampW", new Vector3(-17.5f, 0f, 0f), 90f, 16f, 6f, 18f, Mat("Stone2", new Color(0.4f, 0.4f, 0.42f), 0.2f));
            // Dead trees and glowing jack-o'-lanterns.
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = 30f + (float)rng.NextDouble() * 70f;
                var p = new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d);
                if (Mathf.Abs(d - 80f) < 6f) continue;
                var tree = Group("DeadTree", g, p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                float h = 5f + (float)rng.NextDouble() * 4f;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.6f, h * 0.5f, 0.6f), deadWood);
                for (int b = 0; b < 4; b++)
                    Prim(PrimitiveType.Cylinder, "Branch", tree, new Vector3(0f, h * (0.55f + b * 0.12f), 0f), new Vector3(0.18f, 1.4f, 0.18f), deadWood, false,
                        Quaternion.Euler(50f + b * 8f, b * 95f, 0f));
            }
            for (int i = 0; i < 30; i++)
            {
                float a = i * 12f * Mathf.Deg2Rad;
                float d = i % 2 == 0 ? 26f : 60f;
                var p = new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d);
                var pk = Group("JackOLantern", g, p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 180f, 0f));
                Prim(PrimitiveType.Sphere, "Pumpkin", pk, new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.85f, 1.1f), pumpkin, false);
                Prim(PrimitiveType.Cylinder, "Stem", pk, new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 0.12f, 0.12f), deadWood, false);
                foreach (float x in new[] { -0.2f, 0.2f })
                    Prim(PrimitiveType.Cube, "Eye", pk, new Vector3(x, 0.55f, -0.53f), new Vector3(0.16f, 0.14f, 0.05f), candle, false, Quaternion.Euler(0f, 0f, 45f));
                Prim(PrimitiveType.Cube, "Mouth", pk, new Vector3(0f, 0.3f, -0.52f), new Vector3(0.5f, 0.1f, 0.05f), candle, false);
            }
            // A few lantern posts that really light the ground.
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 45f, 0f, Mathf.Cos(a) * 45f);
                Prim(PrimitiveType.Cylinder, "LanternPost", g, p + Vector3.up * 1.6f, new Vector3(0.15f, 1.6f, 0.15f), iron);
                Prim(PrimitiveType.Cube, "Lantern", g, p + Vector3.up * 3.35f, new Vector3(0.45f, 0.55f, 0.45f), candle, false);
                var light = new GameObject("LanternLight").AddComponent<Light>();
                light.transform.SetParent(g, false);
                light.transform.localPosition = p + Vector3.up * 3.2f;
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.25f);
                light.range = 14f;
                light.intensity = 1.6f;
            }

            var spawn = new GameObject("GraveyardSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.GraveyardTag;
            spawn.killY = -8f;
            spawn.transform.SetParent(g, false);
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f + 15f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 80f, 0f, Mathf.Cos(a) * 80f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = pos;
                slot.localRotation = Quaternion.LookRotation(-pos, Vector3.up);
            }
            var board = Board(g, "GraveBoard", new Vector3(0f, 12f, 104f), Quaternion.identity, new Vector2(16f, 10f), 4f, "GRAVEYARD TAG", neonPink);
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Tag;
            info.text = board;
            info.title = "GRAVEYARD TAG";
            return g;
        }

        // ================================================================== The Backrooms

        /// <summary>
        /// Endless-looking yellow rooms: damp carpet, buzzing ceiling lights and wallpaper walls in a maze with
        /// doorways everywhere, under a low ceiling. Played as Tag, and you can build in it.
        /// </summary>
        static Transform BuildBackrooms()
        {
            var b = Group("Backrooms", null, KartMaps.Get(KartMode.Backrooms).center);
            var rng = new System.Random(13);
            var carpet = Mat("BackroomsCarpet", new Color(0.62f, 0.55f, 0.33f), 0.05f);
            var wallpaper = Mat("BackroomsWall", new Color(0.86f, 0.79f, 0.47f), 0.15f);
            var ceiling = Mat("BackroomsCeiling", new Color(0.84f, 0.81f, 0.68f), 0.1f);
            var panel = Mat("BackroomsLight", new Color(1f, 0.98f, 0.88f), 0.5f, 0f, null, null, new Color(1f, 0.97f, 0.85f) * 1.8f);
            const float cell = 12f, height = 5f;
            const int n = 13; // 13 x 13 rooms = 156 m across (corners stay inside the map's barrier)
            float half = n * cell * 0.5f;

            Prim(PrimitiveType.Cube, "Carpet", b, new Vector3(0f, -0.5f, 0f), new Vector3(n * cell + 2f, 1f, n * cell + 2f), carpet);
            var roof = Prim(PrimitiveType.Cube, "Ceiling", b, new Vector3(0f, height + 0.25f, 0f), new Vector3(n * cell + 2f, 0.5f, n * cell + 2f), ceiling);
            roof.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Outer walls.
            foreach (var w in new[] { (new Vector3(0f, height * 0.5f, half), new Vector3(n * cell + 1f, height, 0.6f)), (new Vector3(0f, height * 0.5f, -half), new Vector3(n * cell + 1f, height, 0.6f)),
                                      (new Vector3(half, height * 0.5f, 0f), new Vector3(0.6f, height, n * cell + 1f)), (new Vector3(-half, height * 0.5f, 0f), new Vector3(0.6f, height, n * cell + 1f)) })
                Prim(PrimitiveType.Cube, "OuterWall", b, w.Item1, w.Item2, wallpaper);
            // The maze: a wall on some room edges, each with a wide doorway gap, and the middle 3x3 rooms left open to spawn in.
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                {
                    bool middle = Mathf.Abs(ix - n / 2) <= 1 && Mathf.Abs(iz - n / 2) <= 1;
                    float x0 = -half + ix * cell, z0 = -half + iz * cell;
                    if (!middle && ix < n - 1 && rng.NextDouble() < 0.42) MazeWall(b, new Vector3(x0 + cell, 0f, z0 + cell * 0.5f), true, cell, height, wallpaper, rng);
                    if (!middle && iz < n - 1 && rng.NextDouble() < 0.42) MazeWall(b, new Vector3(x0 + cell * 0.5f, 0f, z0 + cell), false, cell, height, wallpaper, rng);
                    // Every room has a ceiling light; some are out (it's the Backrooms).
                    if (rng.NextDouble() < 0.85)
                        Prim(PrimitiveType.Cube, "CeilingLight", b, new Vector3(x0 + cell * 0.5f, height - 0.03f, z0 + cell * 0.5f), new Vector3(2.4f, 0.06f, 1.2f), panel, false);
                    if (!middle && rng.NextDouble() < 0.06)
                        Prim(PrimitiveType.Cube, "Pillar", b, new Vector3(x0 + cell * 0.5f, height * 0.5f, z0 + cell * 0.5f), new Vector3(1.4f, height, 1.4f), wallpaper);
                }
            // A handful of real lights so the rooms glow (most of the brightness is the ambient light, see MapAtmosphere).
            for (int i = 0; i < 9; i++)
            {
                var light = new GameObject("RoomLight").AddComponent<Light>();
                light.transform.SetParent(b, false);
                light.transform.localPosition = new Vector3((i % 3 - 1) * 55f, height - 0.6f, (i / 3 - 1) * 55f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.95f, 0.75f);
                light.range = 30f;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
            }

            var spawn = new GameObject("BackroomsSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.Backrooms;
            spawn.killY = -6f;
            spawn.transform.SetParent(b, false);
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f + 15f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 12f, 0f, Mathf.Cos(a) * 12f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = pos;
                slot.localRotation = Quaternion.LookRotation(pos, Vector3.up); // facing out into the maze
            }
            // Tag scores on a wall in the middle room (a plain sign: it's the Backrooms).
            var board = Text(b, "BackroomsBoard", "THE BACKROOMS", new Vector3(0f, 2.6f, 17.6f), Quaternion.identity, 3f, new Color(0.25f, 0.2f, 0.1f), new Vector2(9f, 4f));
            Prim(PrimitiveType.Cube, "BoardWall", b, new Vector3(0f, height * 0.5f, 18f), new Vector3(10f, height, 0.6f), wallpaper);
            var info = board.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Tag;
            info.text = board;
            info.title = "THE BACKROOMS";
            return b;
        }

        /// <summary>A wall along one room edge with a doorway gap somewhere in it.</summary>
        static void MazeWall(Transform parent, Vector3 center, bool alongZ, float length, float height, Material mat, System.Random rng)
        {
            float gap = 5f, gapAt = ((float)rng.NextDouble() - 0.5f) * (length - gap - 1f);
            float a = (gapAt - gap * 0.5f) - (-length * 0.5f), bLen = length * 0.5f - (gapAt + gap * 0.5f);
            foreach (var (mid, len) in new[] { (-length * 0.5f + a * 0.5f, a), (gapAt + gap * 0.5f + bLen * 0.5f, bLen) })
            {
                if (len < 0.5f) continue;
                Vector3 offset = alongZ ? new Vector3(0f, 0f, mid) : new Vector3(mid, 0f, 0f);
                Vector3 size = alongZ ? new Vector3(0.5f, height, len) : new Vector3(len, height, 0.5f);
                Prim(PrimitiveType.Cube, "Wall", parent, center + offset + Vector3.up * height * 0.5f, size, mat);
            }
        }

        // ================================================================== Island Free Roam

        static Transform BuildIsland()
        {
            var isl = Group("Island", null, KartMaps.Get(KartMode.IslandFreeRoam).center);
            var rng = new System.Random(41);
            var sea = Mat("Sea", new Color(0.1f, 0.5f, 0.7f), 0.95f, 0.1f);
            var shallow = Mat("Shallow", new Color(0.3f, 0.8f, 0.8f), 0.9f);
            var palmLeaf = Mat("PalmLeaf", new Color(0.2f, 0.65f, 0.25f), 0.2f);
            var palmTrunk = Mat("PalmTrunk", new Color(0.55f, 0.4f, 0.25f), 0.1f);
            var hut = Mat("Hut", new Color(0.75f, 0.6f, 0.38f), 0.1f);
            var thatch = Mat("Thatch", new Color(0.85f, 0.75f, 0.4f), 0.1f);

            Prim(PrimitiveType.Cube, "Sea", isl, new Vector3(0f, -1.2f, 0f), new Vector3(900f, 1f, 900f), sea, false);
            Prim(PrimitiveType.Cylinder, "Shallows", isl, new Vector3(0f, -0.95f, 0f), new Vector3(420f, 0.2f, 420f), shallow, false);
            // Sea floor below the water so karts that drive in sink a bit and get put back.
            Prim(PrimitiveType.Cube, "SeaFloor", isl, new Vector3(0f, -9f, 0f), new Vector3(900f, 1f, 900f), sand);
            // Beach, then grass, then a hill in the middle.
            var beach = Prim(PrimitiveType.Cylinder, "Beach", isl, new Vector3(0f, -0.6f, 0f), new Vector3(360f, 0.6f, 360f), sand);
            Object.DestroyImmediate(beach.GetComponent<Collider>());
            beach.AddComponent<MeshCollider>().sharedMesh = beach.GetComponent<MeshFilter>().sharedMesh;
            var grass = Prim(PrimitiveType.Cylinder, "Grass", isl, new Vector3(0f, -0.5f, 0f), new Vector3(290f, 0.52f, 290f), freeGrass);
            Object.DestroyImmediate(grass.GetComponent<Collider>());
            grass.AddComponent<MeshCollider>().sharedMesh = grass.GetComponent<MeshFilter>().sharedMesh;
            Prim(PrimitiveType.Sphere, "Hill", isl, new Vector3(60f, -14f, 50f), new Vector3(80f, 50f, 80f), freeGrass);
            Ramp(isl, "HillRamp", new Vector3(60f, 0f, 4f), 0f, 22f, 10f, 14f, rampMat);

            // Ring road round the island, and a road across it.
            RingRoad(isl, 120f, 80);
            Prim(PrimitiveType.Cube, "CrossRoad", isl, new Vector3(0f, 0.012f, 0f), new Vector3(12f, 0.02f, 230f), asphalt, false);
            Prim(PrimitiveType.Cylinder, "Plaza", isl, new Vector3(0f, 0.015f, 0f), new Vector3(36f, 0.01f, 36f), plaza, false);

            // Beach jumps that launch you into the sea, a pier and boost hoops.
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 160f, 0f, Mathf.Cos(a) * 160f);
                Ramp(isl, "BeachJump", p, i * 90f + 45f, 12f, 9f, 14f, i % 2 == 0 ? rampBlue : rampRed);
            }
            Prim(PrimitiveType.Cube, "Pier", isl, new Vector3(0f, 0.2f, -205f), new Vector3(8f, 0.4f, 50f), wood);
            for (int i = 0; i < 3; i++) BoostHoop(isl, new Vector3(-70f + i * 14f, 3.5f, -60f));

            // Palm trees round the beach and dotted inland, beach huts.
            for (int i = 0; i < 70; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = i < 45 ? 135f + (float)rng.NextDouble() * 30f : 25f + (float)rng.NextDouble() * 90f;
                var p = new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d);
                if (Mathf.Abs(d - 120f) < 9f || Mathf.Abs(p.x) < 9f && d < 120f) continue; // roads
                Palm(isl, p, 0.8f + (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() * 360f, palmTrunk, palmLeaf);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 20f) * Mathf.Deg2Rad;
                var h = Group("BeachHut", isl, new Vector3(Mathf.Sin(a) * 100f, 0f, Mathf.Cos(a) * 100f), Quaternion.Euler(0f, i * 60f + 200f, 0f));
                Prim(PrimitiveType.Cube, "Walls", h, new Vector3(0f, 1.6f, 0f), new Vector3(5f, 3.2f, 5f), hut);
                Prim(PrimitiveType.Cube, "Roof", h, new Vector3(0f, 3.6f, 0f), new Vector3(6.4f, 0.9f, 6.4f), thatch, false, Quaternion.Euler(0f, 45f, 0f));
            }

            var spawn = new GameObject("IslandSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.IslandFreeRoam;
            spawn.killY = -5f;
            spawn.transform.SetParent(isl, false);
            for (int i = 0; i < 12; i++)
            {
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = new Vector3((i % 4 - 1.5f) * 3.5f, 0f, -(i / 4) * 4f);
            }
            Board(isl, "IslandSign", new Vector3(0f, 4.5f, 16f), Quaternion.identity, new Vector2(13f, 4f), 2.4f,
                "<b>ISLAND FREE ROAM</b>\nDrive anywhere! Beach jumps, a hill ramp and boost hoops.\nClick the right stick to BUILD.", neonCyan);
            return isl;
        }

        static void Palm(Transform parent, Vector3 pos, float s, float yaw, Material trunk, Material leaf)
        {
            var p = Group("Palm", parent, pos, Quaternion.Euler(0f, yaw, 0f));
            Vector3 at = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                var next = at + new Vector3(0f, 1.2f * s, 0.25f * s * i * 0.3f);
                Prim(PrimitiveType.Cylinder, "Trunk", p, (at + next) * 0.5f, new Vector3(0.45f * s, 0.62f * s, 0.45f * s), trunk, i < 2,
                    Quaternion.FromToRotation(Vector3.up, next - at));
                at = next;
            }
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Cube, "Leaf", p, at + Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0f, -0.3f * s, 1.4f * s),
                    new Vector3(0.8f * s, 0.08f * s, 3f * s), leaf, false, Quaternion.Euler(18f, i * 60f, 0f));
        }
    }
}
