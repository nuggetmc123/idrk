using UnityEngine;

namespace Flopzone
{
    /// <summary>Military base set pieces: sandbags, HESCO walls, fences, towers, huts, vehicles, a helicopter.</summary>
    public static class MilitaryProps
    {
        public static Material Olive => MapKit.M("4b5320", 0.35f, 0.25f, Surface.PaintedMetal);
        public static Material OliveDark => MapKit.M("343a1a", 0.35f, 0.25f, Surface.PaintedMetal);
        public static Material Steel => MapKit.M("5c6066", 0.5f, 0.7f, Surface.Metal);
        public static Material DarkSteel => MapKit.M("2c2f33", 0.45f, 0.7f, Surface.Metal);
        public static Material Tire => MapKit.M("1b1b1b", 0.25f, 0f, Surface.Rubber);
        public static Material Glass => Prims.Lit(Prims.Hex("1d2a36"), 0.95f, 0.3f, Surface.Plastic);
        public static Material Canvas => MapKit.M("6b6b4a", 0.1f, 0f, Surface.Fabric);
        public static Material CamoMat => MapKit.M("ffffff", 0.1f, 0f, Surface.Camo);
        public static Material Sandbag => MapKit.M("b9a57a", 0.1f, 0f, Surface.Sandbag);
        public static Material HescoMat => MapKit.M("c9b98f", 0.1f, 0f, Surface.Hesco);
        public static Material ConcreteMat => MapKit.M("9a9a94", 0.2f, 0f, Surface.Concrete);
        public static Material Hazard => MapKit.M("e0b21a", 0.4f, 0.2f, Surface.PaintedMetal);

        /// <summary>A wall of sandbags from a to b (on the ground), `rows` high.</summary>
        public static void Sandbags(MapKit k, Vector3 a, Vector3 b, int rows = 3)
        {
            var d = b - a;
            d.y = 0;
            float len = d.magnitude;
            var dir = d / Mathf.Max(0.01f, len);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            int count = Mathf.Max(1, Mathf.RoundToInt(len / 0.6f));
            var m = Sandbag;
            for (int r = 0; r < rows; r++)
                for (int i = 0; i < count - (r % 2); i++)
                {
                    float t = (i + 0.5f + (r % 2) * 0.5f) / count;
                    var p = a + d * t + Vector3.up * (0.12f + r * 0.22f);
                    Prims.LowBall(k.S, p, new Vector3(0.36f, 0.24f, 0.64f), m).transform.rotation = Quaternion.Euler(k.R(-4, 4), yaw + k.R(-5, 5), k.R(-3, 3));
                }
            // one simple collider for the whole wall
            var c = new GameObject("sandbagWall");
            c.transform.SetParent(k.S, false);
            c.transform.position = (a + b) * 0.5f + Vector3.up * rows * 0.11f;
            c.transform.rotation = Quaternion.Euler(0, yaw, 0);
            c.AddComponent<BoxCollider>().size = new Vector3(0.45f, rows * 0.22f + 0.05f, len);
        }

        /// <summary>A line of HESCO barriers (wire baskets full of sand).</summary>
        public static void Hesco(MapKit k, Vector3 a, Vector3 b, float h = 1.4f)
        {
            var d = b - a;
            d.y = 0;
            int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 1.1f));
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                var p = a + d * ((i + 0.5f) / n);
                var box = k.Block(p, new Vector3(1.08f, h * k.R(0.96f, 1.04f), 1.08f), HescoMat, yaw);
                Prims.Box(box.transform, new Vector3(0, 0.5f, 0), new Vector3(0.96f, 0.02f, 0.96f), MapKit.M("a8946a", 0.05f, 0f, Surface.Dirt), false);
            }
        }

        public static void Jersey(MapKit k, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            k.BoxRot(p + Vector3.up * 0.18f, new Vector3(0.6f, 0.36f, 3f), ConcreteMat, rot);
            k.BoxRot(p + Vector3.up * 0.6f, new Vector3(0.32f, 0.5f, 3f), ConcreteMat, rot);
            k.BoxRot(p + Vector3.up * 0.86f, new Vector3(0.26f, 0.04f, 3f), Hazard, rot, false);
        }

        static Texture2D chainTex;

        static Material ChainLink()
        {
            if (chainTex == null)
            {
                const int n = 64;
                chainTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "fz_chain", wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + y) % 32, v = (x - y + 64) % 32;
                        bool wire = u < 2.2f || v < 2.2f;
                        chainTex.SetPixel(x, y, wire ? new Color(0.75f, 0.77f, 0.8f, 1) : new Color(0, 0, 0, 0));
                    }
                chainTex.Apply(true, true);
            }
            var m = Prims.Fade(new Color(1, 1, 1, 0.9f));
            m.mainTexture = chainTex;
            m.SetFloat("_Metallic", 0.6f);
            m.SetFloat("_Glossiness", 0.5f);
            return m;
        }

        /// <summary>Chain-link fence with barbed wire on top.</summary>
        public static void Fence(MapKit k, Vector3 a, Vector3 b, float h = 3f)
        {
            var d = b - a;
            d.y = 0;
            float len = d.magnitude;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 3f) + 1);
            for (int i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)(posts - 1));
                Prims.Cyl(k.S, p + Vector3.up * h * 0.5f, 0.08f, h, Steel, Vector3.up, false);
                Prims.Cyl(k.S, p + Vector3.up * (h + 0.25f) + Quaternion.Euler(0, yaw, 0) * Vector3.right * 0.12f, 0.04f, 0.6f, Steel, Quaternion.Euler(0, yaw, 0) * new Vector3(0.6f, 1, 0), false);
            }
            var mesh = Prims.Make(PrimitiveType.Quad, k.D, (a + b) * 0.5f + Vector3.up * h * 0.5f, new Vector3(len, h, 1), ChainLink(), false, Quaternion.Euler(0, yaw - 90, 0), "chainlink");
            var mf = mesh.GetComponent<MeshFilter>();
            var q = Object.Instantiate(mf.sharedMesh);
            var uv = q.uv;
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(uv[i].x * len / 0.5f, uv[i].y * h / 0.5f);
            q.uv = uv;
            mf.sharedMesh = q;
            var back = Object.Instantiate(mesh, mesh.transform.parent);
            back.transform.rotation = Quaternion.Euler(0, yaw + 90, 0);
            // barbed wire: three strands
            for (int s = 0; s < 3; s++)
            {
                var off = Quaternion.Euler(0, yaw, 0) * Vector3.right * (0.05f + s * 0.1f) + Vector3.up * (h + 0.1f + s * 0.17f);
                Prims.Cyl(k.S, (a + b) * 0.5f + off, 0.015f, len, DarkSteel, d, false);
            }
            var c = new GameObject("fenceCollider");
            c.transform.SetParent(k.S, false);
            c.transform.position = (a + b) * 0.5f + Vector3.up * h * 0.5f;
            c.transform.rotation = Quaternion.Euler(0, yaw, 0);
            c.AddComponent<BoxCollider>().size = new Vector3(0.1f, h, len);
        }

        public static void Watchtower(MapKit k, Vector3 p, float yaw)
        {
            var wood = MapKit.M("6b5536", 0.15f, 0f, Surface.Wood);
            float h = 5.5f;
            foreach (var o in new[] { new Vector3(-1.4f, 0, -1.4f), new Vector3(1.4f, 0, -1.4f), new Vector3(-1.4f, 0, 1.4f), new Vector3(1.4f, 0, 1.4f) })
            {
                k.Box(p + o + Vector3.up * h * 0.5f, new Vector3(0.25f, h, 0.25f), wood);
                k.BoxRot(p + o * 0.5f + Vector3.up * h * 0.35f, new Vector3(0.1f, 0.1f, 2.6f), wood, Quaternion.Euler(0, Mathf.Atan2(o.x, o.z) * Mathf.Rad2Deg, 0) * Quaternion.Euler(45, 0, 0), false);
            }
            k.Box(p + Vector3.up * h, new Vector3(3.6f, 0.2f, 3.6f), MapKit.M("7a6242", 0.15f, 0f, Surface.Plank));
            var rot = Quaternion.Euler(0, yaw, 0);
            Sandbags(k, p + Vector3.up * (h + 0.1f) + rot * new Vector3(-1.6f, 0, 1.65f), p + Vector3.up * (h + 0.1f) + rot * new Vector3(1.6f, 0, 1.65f), 3);
            k.Box(p + Vector3.up * (h + 2.6f), new Vector3(4f, 0.15f, 4f), OliveDark);
            foreach (var o in new[] { new Vector3(-1.7f, 0, -1.7f), new Vector3(1.7f, 0, -1.7f), new Vector3(-1.7f, 0, 1.7f), new Vector3(1.7f, 0, 1.7f) })
                k.Box(p + o + Vector3.up * (h + 1.3f), new Vector3(0.12f, 2.6f, 0.12f), wood, 0, false);
            // searchlight
            var lamp = Prims.Cyl(k.S, p + Vector3.up * (h + 1.4f) + rot * new Vector3(1.4f, 0, 1.4f), 0.45f, 0.5f, DarkSteel, rot * new Vector3(0, -0.3f, 1), false);
            Prims.Cyl(lamp.transform, new Vector3(0, 0.51f, 0), 0.9f, 0.02f, Prims.Emissive(Prims.Hex("fff4d6"), 3f), Vector3.up);
            // ladder
            for (int i = 0; i < 12; i++) k.Box(p + rot * new Vector3(0, 0.3f + i * 0.45f, -1.9f), new Vector3(0.6f, 0.05f, 0.05f), wood, yaw, false);
        }

        /// <summary>A half-round Quonset hut (barracks).</summary>
        public static void Quonset(MapKit k, Vector3 p, float yaw, float length = 12f)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var hut = Prims.Cyl(k.S, p, 7.5f, length, MapKit.M("5f6648", 0.35f, 0.3f, Surface.PaintedMetal), rot * Vector3.forward, true);
            hut.name = "quonset";
            for (int i = 0; i < 6; i++)
                Prims.Cyl(k.S, p + rot * new Vector3(0, 0, -length / 2 + 1 + i * (length - 2) / 5f), 7.6f, 0.15f, OliveDark, rot * Vector3.forward, false);
            k.BoxRot(p + rot * new Vector3(0, 1.1f, length / 2 + 0.02f), new Vector3(1.4f, 2.2f, 0.08f), MapKit.M("3a3020", 0.3f, 0f, Surface.Wood), rot, false);
            k.BoxRot(p + rot * new Vector3(-2f, 1.8f, length / 2 + 0.02f), new Vector3(0.9f, 0.6f, 0.05f), Prims.Emissive(Prims.Hex("ffd58a"), 1.2f), rot, false);
            k.BoxRot(p + rot * new Vector3(2f, 1.8f, length / 2 + 0.02f), new Vector3(0.9f, 0.6f, 0.05f), Prims.Emissive(Prims.Hex("ffd58a"), 1.2f), rot, false);
            k.BoxRot(p + rot * new Vector3(0, 2.5f, length / 2 + 0.3f), new Vector3(2f, 0.08f, 0.7f), OliveDark, rot * Quaternion.Euler(-10, 0, 0), false);
        }

        public static void Tent(MapKit k, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            k.BoxRot(p + rot * new Vector3(-1.1f, 1.1f, 0), new Vector3(0.06f, 2.6f, 4f), CamoMat, rot * Quaternion.Euler(0, 0, -38));
            k.BoxRot(p + rot * new Vector3(1.1f, 1.1f, 0), new Vector3(0.06f, 2.6f, 4f), CamoMat, rot * Quaternion.Euler(0, 0, 38));
            k.BoxRot(p + rot * new Vector3(0, 1.05f, -2f), new Vector3(2.9f, 2.1f, 0.04f), Canvas, rot, false);
        }

        public static void Jeep(MapKit k, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var body = Olive;
            k.BoxRot(p + rot * new Vector3(0, 0.75f, 0), new Vector3(1.7f, 0.55f, 3.6f), body, rot);
            k.BoxRot(p + rot * new Vector3(0, 1.12f, 1.05f), new Vector3(1.6f, 0.2f, 1.3f), body, rot, false);
            k.BoxRot(p + rot * new Vector3(0, 1.45f, 0.35f), new Vector3(1.6f, 0.6f, 0.06f), DarkSteel, rot * Quaternion.Euler(-15, 0, 0), false);
            k.BoxRot(p + rot * new Vector3(0, 1.45f, 0.36f), new Vector3(1.4f, 0.5f, 0.02f), Glass, rot * Quaternion.Euler(-15, 0, 0), false);
            k.BoxRot(p + rot * new Vector3(-0.4f, 1.15f, -0.4f), new Vector3(0.55f, 0.6f, 0.55f), Canvas, rot, false);
            k.BoxRot(p + rot * new Vector3(0.4f, 1.15f, -0.4f), new Vector3(0.55f, 0.6f, 0.55f), Canvas, rot, false);
            k.BoxRot(p + rot * new Vector3(0, 0.9f, 1.82f), new Vector3(1.2f, 0.4f, 0.04f), DarkSteel, rot, false);
            foreach (var o in new[] { new Vector3(-0.85f, 0.42f, 1.2f), new Vector3(0.85f, 0.42f, 1.2f), new Vector3(-0.85f, 0.42f, -1.2f), new Vector3(0.85f, 0.42f, -1.2f) })
            {
                Prims.Cyl(k.S, p + rot * o, 0.84f, 0.32f, Tire, rot * Vector3.right, false);
                Prims.Cyl(k.S, p + rot * (o + new Vector3(Mathf.Sign(o.x) * 0.17f, 0, 0)), 0.4f, 0.02f, Steel, rot * Vector3.right, false);
            }
            Prims.Cyl(k.S, p + rot * new Vector3(0, 1.05f, -1.86f), 0.8f, 0.3f, Tire, rot * Vector3.forward, false);
            k.BoxRot(p + rot * new Vector3(-0.6f, 0.85f, 1.81f), new Vector3(0.2f, 0.2f, 0.03f), Prims.Emissive(Prims.Hex("fff4c9"), 1.5f), rot, false);
            k.BoxRot(p + rot * new Vector3(0.6f, 0.85f, 1.81f), new Vector3(0.2f, 0.2f, 0.03f), Prims.Emissive(Prims.Hex("fff4c9"), 1.5f), rot, false);
            k.BoxRot(p + rot * new Vector3(0.75f, 0.85f, 0.2f), new Vector3(0.05f, 0.25f, 0.35f), MapKit.M("e8e8e8", 0.3f), rot, false);
        }

        public static void Truck(MapKit k, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            k.BoxRot(p + rot * new Vector3(0, 1.4f, 2.6f), new Vector3(2.3f, 1.8f, 1.9f), Olive, rot);
            k.BoxRot(p + rot * new Vector3(0, 1.75f, 3.56f), new Vector3(2f, 0.7f, 0.04f), Glass, rot, false);
            k.BoxRot(p + rot * new Vector3(0, 0.85f, 3.62f), new Vector3(2.2f, 0.5f, 0.12f), DarkSteel, rot, false);
            k.BoxRot(p + rot * new Vector3(0, 1.0f, -1.2f), new Vector3(2.4f, 0.4f, 5.4f), OliveDark, rot);
            k.BoxRot(p + rot * new Vector3(-1.18f, 1.6f, -1.2f), new Vector3(0.06f, 0.9f, 5.4f), Olive, rot);
            k.BoxRot(p + rot * new Vector3(1.18f, 1.6f, -1.2f), new Vector3(0.06f, 0.9f, 5.4f), Olive, rot);
            Prims.Cyl(k.S, p + rot * new Vector3(0, 2.05f, -1.2f), 2.4f, 5.4f, Canvas, rot * Vector3.forward, false);
            foreach (var z in new[] { 2.6f, -0.4f, -2.6f })
                foreach (var x in new[] { -1.05f, 1.05f })
                    Prims.Cyl(k.S, p + rot * new Vector3(x, 0.55f, z), 1.1f, 0.4f, Tire, rot * Vector3.right, false);
        }

        public static GameObject Helicopter(MapKit k, Vector3 p, float yaw)
        {
            var root = Prims.Empty("helicopter", k.D, p);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var t = root.transform;
            var body = OliveDark;
            Prims.Make(PrimitiveType.Capsule, t, new Vector3(0, 1.6f, 0), new Vector3(2.4f, 2.6f, 2.6f), body, true, Quaternion.Euler(90, 0, 0));
            Prims.Ball(t, new Vector3(0, 1.75f, 2.1f), new Vector3(2f, 1.7f, 1.9f), Glass);
            Prims.Cyl(t, new Vector3(0, 2.0f, -4.5f), 0.6f, 5.5f, body, Vector3.forward);
            Prims.Box(t, new Vector3(0, 2.8f, -7f), new Vector3(0.12f, 1.8f, 1.2f), body, false, Quaternion.Euler(-15, 0, 0));
            Prims.Box(t, new Vector3(0, 2.1f, -6.6f), new Vector3(2f, 0.08f, 0.6f), body, false);
            foreach (var x in new[] { -1.1f, 1.1f })
            {
                Prims.Cyl(t, new Vector3(x, 0.1f, 0), 0.12f, 4.4f, DarkSteel, Vector3.forward);
                Prims.Box(t, new Vector3(x * 0.85f, 0.5f, 1f), new Vector3(0.08f, 0.8f, 0.08f), DarkSteel, false, Quaternion.Euler(0, 0, x * 15));
                Prims.Box(t, new Vector3(x * 0.85f, 0.5f, -1f), new Vector3(0.08f, 0.8f, 0.08f), DarkSteel, false, Quaternion.Euler(0, 0, x * 15));
            }
            Prims.Cyl(t, new Vector3(0, 3.1f, 0), 0.5f, 0.5f, DarkSteel, Vector3.up);
            var rotor = Prims.Empty("rotor", t, new Vector3(0, 3.4f, 0));
            for (int i = 0; i < 4; i++)
                Prims.Box(rotor.transform, Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 0, 3.6f), new Vector3(0.35f, 0.05f, 7.2f), DarkSteel, false, Quaternion.Euler(0, i * 90, 2));
            rotor.AddComponent<Spinner>().speed = 25f;
            var tail = Prims.Empty("tailRotor", t, new Vector3(0.2f, 2.9f, -7.2f));
            Prims.Box(tail.transform, Vector3.zero, new Vector3(0.04f, 1.6f, 0.15f), DarkSteel, false);
            Prims.Box(tail.transform, Vector3.zero, new Vector3(0.04f, 0.15f, 1.6f), DarkSteel, false);
            tail.AddComponent<Spinner>().axis = Vector3.right;
            tail.GetComponent<Spinner>().speed = 160f;
            Prims.Box(t, new Vector3(-1.21f, 1.5f, 0.3f), new Vector3(0.02f, 0.7f, 0.9f), Prims.Emissive(Prims.Hex("ffffff"), 0.4f), false);
            UIKit.Text(t, "FZ-01", new Vector3(1.23f, 1.6f, -1f), 0.35f, new Color(0.85f, 0.85f, 0.75f)).transform.localRotation = Quaternion.Euler(0, -90, 0);
            return root;
        }

        public static void AmmoCrate(MapKit k, Vector3 p, float yaw)
        {
            var c = k.Block(p, new Vector3(0.9f, 0.45f, 0.5f), MapKit.M("4a5a2a", 0.3f, 0.1f, Surface.Wood), yaw);
            Prims.Box(c.transform, new Vector3(0, 0.51f, 0), new Vector3(1.02f, 0.06f, 1.02f), OliveDark, false);
            Prims.Box(c.transform, new Vector3(0.45f, 0.1f, 0.51f), new Vector3(0.12f, 0.2f, 0.02f), Steel, false);
            Prims.Box(c.transform, new Vector3(-0.45f, 0.1f, 0.51f), new Vector3(0.12f, 0.2f, 0.02f), Steel, false);
            var rot = Quaternion.Euler(0, yaw, 0);
            var label = UIKit.Text(k.S, "AMMO", p + rot * new Vector3(0, 0.22f, 0.262f), 0.11f, new Color(0.95f, 0.85f, 0.4f));
            label.transform.rotation = rot * Quaternion.Euler(0, 180, 0);
        }

        public static void Pallet(MapKit k, Vector3 p, float yaw)
        {
            var w = MapKit.M("9c7f55", 0.1f, 0f, Surface.Plank);
            k.Block(p, new Vector3(1.2f, 0.14f, 1.0f), w, yaw);
            for (int i = 0; i < 4; i++) AmmoCrate(k, p + Vector3.up * (0.14f + (i / 2) * 0.46f) + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, (i % 2 - 0.5f) * 0.5f), yaw + 90);
        }

        public static void Drum(MapKit k, Vector3 p, string hex)
        {
            var m = MapKit.M(hex, 0.45f, 0.35f, Surface.PaintedMetal);
            var b = Prims.Cyl(k.S, p + Vector3.up * 0.45f, 0.6f, 0.9f, m, Vector3.up, true);
            Prims.Cyl(b.transform, new Vector3(0, 0.25f, 0), 1.03f, 0.03f, DarkSteel, Vector3.up);
            Prims.Cyl(b.transform, new Vector3(0, -0.25f, 0), 1.03f, 0.03f, DarkSteel, Vector3.up);
        }

        public static void CamoNet(MapKit k, Vector3 center, Vector2 size, float h)
        {
            k.Box(center + Vector3.up * h, new Vector3(size.x, 0.05f, size.y), CamoMat, 0, false);
            for (int i = 0; i < 9; i++)
                Prims.Ball(k.S, center + new Vector3(k.R(-size.x, size.x) * 0.45f, h - 0.1f, k.R(-size.y, size.y) * 0.45f), new Vector3(k.R(1, 2.5f), 0.25f, k.R(1, 2.5f)), CamoMat);
            foreach (var o in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                k.Box(center + new Vector3(o.x * size.x * 0.45f, h * 0.5f, o.z * size.y * 0.45f), new Vector3(0.12f, h, 0.12f), Steel);
        }

        public static void FloodLight(MapKit k, Vector3 p, float yaw)
        {
            Prims.Cyl(k.S, p + Vector3.up * 4.5f, 0.2f, 9f, Steel, Vector3.up, true);
            var rot = Quaternion.Euler(0, yaw, 0);
            k.BoxRot(p + Vector3.up * 9.1f, new Vector3(2.4f, 0.12f, 0.12f), Steel, rot, false);
            for (int i = 0; i < 3; i++)
            {
                var head = k.BoxRot(p + Vector3.up * 9.35f + rot * new Vector3(-0.8f + i * 0.8f, 0, 0.1f), new Vector3(0.6f, 0.45f, 0.3f), DarkSteel, rot * Quaternion.Euler(25, 0, 0), false);
                Prims.Box(head.transform, new Vector3(0, 0, 0.51f), new Vector3(0.85f, 0.8f, 0.02f), Prims.Emissive(Prims.Hex("fff6dc"), 3.5f), false);
            }
        }

        public static void Radar(MapKit k, Vector3 p)
        {
            Prims.Cyl(k.S, p + Vector3.up * 0.6f, 1.2f, 1.2f, Steel, Vector3.up, true);
            var arm = Prims.Empty("radar", k.D, p + Vector3.up * 1.3f);
            Prims.Box(arm.transform, new Vector3(0, 0.6f, 0), new Vector3(0.2f, 1.2f, 0.2f), DarkSteel, false);
            Prims.Ball(arm.transform, new Vector3(0, 1.5f, 0.3f), new Vector3(3f, 1.6f, 0.4f), MapKit.M("d8d8d0", 0.4f, 0.3f, Surface.PaintedMetal));
            Prims.Box(arm.transform, new Vector3(0, 1.5f, 0.9f), new Vector3(0.1f, 0.1f, 1f), DarkSteel, false);
            arm.AddComponent<Spinner>().speed = 30f;
        }

        public static void Flagpole(MapKit k, Vector3 p)
        {
            Prims.Cyl(k.S, p + Vector3.up * 0.25f, 2f, 0.5f, ConcreteMat, Vector3.up, true);
            Prims.Cyl(k.S, p + Vector3.up * 6f, 0.14f, 11.5f, MapKit.M("cfcfcf", 0.7f, 0.8f), Vector3.up, true);
            Prims.Sphere(k.S, p + Vector3.up * 11.8f, 0.3f, MapKit.M("d4af37", 0.8f, 0.9f));
            var flag = Prims.Empty("flag", k.D, p + new Vector3(0, 10.4f, 0));
            var wave = flag.AddComponent<FlagWave>();
            Transform prev = flag.transform;
            for (int i = 0; i < 6; i++)
            {
                var seg = Prims.Empty("seg" + i, prev, new Vector3(i == 0 ? 0.07f : 0.42f, 0, 0));
                wave.segments.Add(seg.transform);
                prev = seg.transform;
                Prims.Box(seg.transform, new Vector3(0.21f, 0, 0), new Vector3(0.42f, 1.6f, 0.03f), MapKit.M(i % 2 == 0 ? "c0392b" : "b03424", 0.2f, 0f, Surface.Fabric), false);
                Prims.Box(seg.transform, new Vector3(0.21f, 0, -0.02f), new Vector3(0.42f, 0.3f, 0.01f), MapKit.M("f2f2f2", 0.2f, 0f, Surface.Fabric), false);
            }
        }
    }

    /// <summary>Makes a segmented flag ripple.</summary>
    public class FlagWave : MonoBehaviour
    {
        public readonly System.Collections.Generic.List<Transform> segments = new System.Collections.Generic.List<Transform>();

        void Update()
        {
            for (int i = 0; i < segments.Count; i++)
                segments[i].localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 3.2f - i * 0.9f) * 14f, 0);
        }
    }
}
