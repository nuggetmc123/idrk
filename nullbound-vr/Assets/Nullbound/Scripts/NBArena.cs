using UnityEngine;

namespace Nullbound
{
    /// Builds the world out of primitives at startup (same layout on every headset):
    /// the fighting arena "THE NULL YARD" and the lobby platform where the menu lives.
    public static class NBArena
    {
        public const float Half = 42f;
        public const float KillY = -25f;
        public static readonly Vector3 LobbyCenter = new Vector3(0, 0, -400f);
        public static Vector3 LobbySpawn => LobbyCenter + new Vector3(0, 0, -1.2f);
        public static Vector3 MenuPos => LobbyCenter + new Vector3(0, 1.45f, 1.1f);
        public static Vector3 BagPos => LobbyCenter + new Vector3(1.6f, 0, -0.6f);

        static readonly Vector3[] Spawns = BuildSpawns();
        static Transform s_Root;

        static Vector3[] BuildSpawns()
        {
            var s = new Vector3[12];
            for (int i = 0; i < s.Length; i++)
            {
                float a = i * Mathf.PI * 2f / s.Length;
                s[i] = new Vector3(Mathf.Cos(a) * 24f, 0.1f, Mathf.Sin(a) * 24f);
            }
            return s;
        }

        /// The spawn point furthest from everyone else.
        public static Vector3 PickSpawn(NBPlayer self)
        {
            Vector3 best = Spawns[Random.Range(0, Spawns.Length)];
            float bestScore = -1f;
            foreach (var s in Spawns)
            {
                float closest = 999f;
                foreach (var p in NBPlayer.All)
                    if (p != null && p != self && p.Alive) closest = Mathf.Min(closest, Vector3.Distance(p.Foot, s));
                closest += Random.value * 3f;
                if (closest > bestScore) { bestScore = closest; best = s; }
            }
            return best;
        }

        public static void Build()
        {
            if (s_Root != null) return;
            s_Root = new GameObject("NB_World").transform;
            var rng = new System.Random(1337);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.4f, 0.52f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.8f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            sun.transform.SetParent(s_Root);

            BuildArena(rng);
            BuildLobby();
            BuildSky(rng);
        }

        static GameObject Block(Vector3 center, Vector3 size, Color c, bool collide = true, float emission = 0f)
        {
            return NBMat.Prim(PrimitiveType.Cube, s_Root, center, size, NBMat.Lit(c, 0.15f, emission), collide);
        }

        static void BuildArena(System.Random rng)
        {
            float size = Half * 2f;
            // one big collider for the ground, coloured tiles on top for depth perception
            var ground = Block(new Vector3(0, -1f, 0), new Vector3(size, 2f, size), new Color(0.2f, 0.2f, 0.25f));
            ground.name = "Ground";
            int tiles = 12;
            float t = size / tiles;
            for (int x = 0; x < tiles; x++)
                for (int z = 0; z < tiles; z++)
                {
                    bool dark = (x + z) % 2 == 0;
                    var c = dark ? new Color(0.24f, 0.25f, 0.31f) : new Color(0.3f, 0.31f, 0.38f);
                    Block(new Vector3(-Half + t * (x + 0.5f), 0.005f, -Half + t * (z + 0.5f)), new Vector3(t - 0.08f, 0.01f, t - 0.08f), c, false);
                }
            // glowing centre circle
            NBMat.Prim(PrimitiveType.Cylinder, s_Root, new Vector3(0, 0.02f, 0), new Vector3(10f, 0.01f, 10f), NBMat.Lit(new Color(0.5f, 0.2f, 0.9f), 0.2f, 0.6f));
            NBMat.Prim(PrimitiveType.Cylinder, s_Root, new Vector3(0, 0.03f, 0), new Vector3(8.6f, 0.01f, 8.6f), NBMat.Lit(new Color(0.22f, 0.22f, 0.28f)));

            // edge glow strips so you can see where the void starts
            var edge = new Color(0.6f, 0.2f, 1f);
            Block(new Vector3(0, 0.06f, Half), new Vector3(size, 0.12f, 0.25f), edge, false, 1.5f);
            Block(new Vector3(0, 0.06f, -Half), new Vector3(size, 0.12f, 0.25f), edge, false, 1.5f);
            Block(new Vector3(Half, 0.06f, 0), new Vector3(0.25f, 0.12f, size), edge, false, 1.5f);
            Block(new Vector3(-Half, 0.06f, 0), new Vector3(0.25f, 0.12f, size), edge, false, 1.5f);

            // city blocks (cover), kept away from the centre and the spawn ring
            var palette = new[] { new Color(0.45f, 0.42f, 0.5f), new Color(0.35f, 0.38f, 0.48f), new Color(0.52f, 0.45f, 0.4f), new Color(0.3f, 0.32f, 0.36f) };
            int placed = 0, guard = 0;
            while (placed < 16 && guard++ < 400)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * (Half - 6f);
                float z = (float)(rng.NextDouble() * 2 - 1) * (Half - 6f);
                float r = Mathf.Sqrt(x * x + z * z);
                if (r < 11f || Mathf.Abs(r - 24f) < 4f) continue;
                float w = 3f + (float)rng.NextDouble() * 5f, d = 3f + (float)rng.NextDouble() * 5f, h = 3f + (float)rng.NextDouble() * 10f;
                var c = palette[rng.Next(palette.Length)];
                Block(new Vector3(x, h * 0.5f, z), new Vector3(w, h, d), c);
                // windows
                for (float wy = 1.5f; wy < h - 0.8f; wy += 2f)
                    Block(new Vector3(x, wy, z), new Vector3(w + 0.02f, 0.5f, d + 0.02f), new Color(1f, 0.85f, 0.5f), false, 0.8f);
                placed++;
            }
            // low crates you can jump on
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad + 0.3f;
                var p = new Vector3(Mathf.Cos(a) * 15f, 0.6f, Mathf.Sin(a) * 15f);
                Block(p, new Vector3(1.6f, 1.2f, 1.6f), new Color(0.55f, 0.4f, 0.25f));
            }
            // broken pillars
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 34f, 2.5f, Mathf.Sin(a) * 34f);
                NBMat.Prim(PrimitiveType.Cylinder, s_Root, p, new Vector3(1.4f, 2.5f, 1.4f), NBMat.Lit(new Color(0.6f, 0.58f, 0.62f)), true);
            }
        }

        static void BuildLobby()
        {
            var c = LobbyCenter;
            Block(c + new Vector3(0, -0.5f, 0), new Vector3(12f, 1f, 12f), new Color(0.18f, 0.17f, 0.24f)).name = "LobbyFloor";
            NBMat.Prim(PrimitiveType.Cylinder, s_Root, c + new Vector3(0, 0.01f, 0), new Vector3(5f, 0.01f, 5f), NBMat.Lit(new Color(0.45f, 0.2f, 0.85f), 0.2f, 0.5f));
            NBMat.Prim(PrimitiveType.Cylinder, s_Root, c + new Vector3(0, 0.02f, 0), new Vector3(4.4f, 0.01f, 4.4f), NBMat.Lit(new Color(0.2f, 0.19f, 0.27f)));
            // invisible walls so you can't walk off the lobby
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var wall = new GameObject("LobbyWall");
                wall.transform.SetParent(s_Root);
                wall.transform.position = c + d * 6f + Vector3.up * 1.5f;
                var bc = wall.AddComponent<BoxCollider>();
                bc.size = new Vector3(d.x != 0 ? 0.2f : 12f, 3f, d.z != 0 ? 0.2f : 12f);
                Block(c + d * 6f + Vector3.up * 0.05f, new Vector3(d.x != 0 ? 0.15f : 12f, 0.1f, d.z != 0 ? 0.15f : 12f), new Color(0.6f, 0.2f, 1f), false, 1.5f);
            }

            var title = NBMat.Text(s_Root, "NULLBOUND", c + new Vector3(0, 4.2f, 6f), 1.4f, new Color(0.8f, 0.6f, 1f));
            title.transform.rotation = Quaternion.identity;
            var sub = NBMat.Text(s_Root, "VR BATTLEGROUNDS", c + new Vector3(0, 3.25f, 6f), 0.4f, Color.white);
            sub.transform.rotation = Quaternion.identity;

            // fighter showcase: mannequins on pedestals in a semicircle behind you
            for (int i = 0; i < NBFighters.All.Length; i++)
            {
                var f = NBFighters.All[i];
                float a = Mathf.Lerp(-70f, 70f, i / (float)(NBFighters.All.Length - 1)) * Mathf.Deg2Rad;
                var p = c + new Vector3(Mathf.Sin(a) * 4.6f, 0, -Mathf.Cos(a) * 4.6f);
                var face = Quaternion.LookRotation(new Vector3(c.x - p.x, 0, c.z - p.z));
                var root = new GameObject("Showcase_" + f.name).transform;
                root.SetParent(s_Root);
                root.position = p; root.rotation = face;
                NBMat.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.15f, 0), new Vector3(0.9f, 0.15f, 0.9f), NBMat.Lit(new Color(0.3f, 0.3f, 0.36f)), true);
                NBMat.Prim(PrimitiveType.Capsule, root, new Vector3(0, 1.25f, 0), new Vector3(0.42f, 0.42f, 0.28f), NBMat.Lit(Color.Lerp(f.main, Color.black, 0.25f)));
                NBMat.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.8f, 0), Vector3.one * 0.28f, NBMat.Lit(f.main));
                NBMat.Prim(PrimitiveType.Cube, root, new Vector3(0, 1.82f, 0.1f), new Vector3(0.24f, 0.08f, 0.1f), NBMat.Lit(new Color(0.05f, 0.05f, 0.08f), 0.9f));
                NBMat.Prim(PrimitiveType.Sphere, root, new Vector3(-0.3f, 1.35f, 0.25f), new Vector3(0.12f, 0.11f, 0.14f), NBMat.Lit(f.accent));
                NBMat.Prim(PrimitiveType.Sphere, root, new Vector3(0.3f, 1.35f, 0.25f), new Vector3(0.12f, 0.11f, 0.14f), NBMat.Lit(f.accent));
                var label = NBMat.Text(root, f.name, new Vector3(0, 2.25f, 0), 0.16f, f.main);
                label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }

            // punching bag for the VR setup test
            var bag = new GameObject("PunchBag");
            bag.transform.SetParent(s_Root);
            bag.transform.position = BagPos;
            bag.AddComponent<NBPunchBag>();
        }

        static void BuildSky(System.Random rng)
        {
            // floating rocks and a giant ring so the void has something to look at
            for (int i = 0; i < 40; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 80f + (float)rng.NextDouble() * 120f;
                var p = new Vector3(Mathf.Cos(a) * r, -30f + (float)rng.NextDouble() * 70f, Mathf.Sin(a) * r);
                float s = 3f + (float)rng.NextDouble() * 12f;
                var g = NBMat.Prim(PrimitiveType.Cube, s_Root, p, new Vector3(s, s * 0.6f, s * 0.8f), NBMat.Lit(new Color(0.25f, 0.22f, 0.32f)));
                g.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f);
            }
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 140f, 60f + Mathf.Sin(a) * 140f * 0.15f, 220f + Mathf.Sin(a) * 140f);
                var g = NBMat.Prim(PrimitiveType.Cube, s_Root, p, new Vector3(6, 6, 22), NBMat.Lit(new Color(0.6f, 0.3f, 1f), 0.2f, 2f));
                g.transform.rotation = Quaternion.Euler(0, -i * 10f, 0);
            }
            // lobby sky ring
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                var p = LobbyCenter + new Vector3(Mathf.Cos(a) * 30f, 8f + Mathf.Sin(a * 2f) * 2f, Mathf.Sin(a) * 30f);
                NBMat.Prim(PrimitiveType.Sphere, s_Root, p, Vector3.one * 1.5f, NBMat.Lit(new Color(0.7f, 0.5f, 1f), 0.2f, 2f));
            }
        }
    }
}
