using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The set pieces that give each map its identity: the sea and moored yachts, a lighthouse, a pagoda, a Ferris
    /// wheel, a hilltop castle, turning windmills, harbour cranes and container stacks, neon signs, a runway, a stadium
    /// bowl, and a ring of distant mountains so the horizon is never empty.
    /// </summary>
    public static class Landmarks
    {
        static Material M(System.Func<VXAssets, Material> f, string n, Color c, float s = 0.3f) { var A = VXAssets.I; return VXAssets.Or(A != null ? f(A) : null, n, c, s); }

        public static GameObject Sea(Transform parent, float level, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Obj.Kill(go.GetComponent<Collider>());
            go.name = "Sea";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, level, 0f);
            go.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            go.layer = Layers.Scenery;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = M(a => a.water, "Water", new Color(0.05f, 0.2f, 0.28f), 0.95f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static void Yacht(Transform parent, Vector3 pos, float yaw, float length, Rng rng)
        {
            var k = new MeshKit();
            float w = length * 0.24f;
            k.Loft(0, new[]
            {
                new MeshKit.Station(length * 0.5f, 0.9f, 0.05f, 0.5f, 2.2f, 0, 0.6f),
                new MeshKit.Station(length * 0.3f, 0.8f, w * 0.38f, 0.75f, 2.4f, 0, 0.6f),
                new MeshKit.Station(0f, 0.75f, w * 0.5f, 0.85f, 2.6f, 0, 0.7f),
                new MeshKit.Station(-length * 0.45f, 0.8f, w * 0.46f, 0.8f, 3.2f, 0, 0.7f),
            }, 18, 14, null, 0, 0);
            // Superstructure decks.
            k.Box(1, new Vector3(0f, 2.0f, -length * 0.08f), new Vector3(w * 0.7f, 1.1f, length * 0.45f), 0.5f);
            k.Box(1, new Vector3(0f, 2.9f, -length * 0.12f), new Vector3(w * 0.55f, 0.8f, length * 0.28f), 0.5f);
            k.Box(2, new Vector3(0f, 2.05f, -length * 0.08f + length * 0.226f), new Vector3(w * 0.68f, 0.5f, 0.05f), 0.5f);
            var go = new GameObject("Yacht");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.layer = Layers.Scenery;
            go.AddComponent<MeshFilter>().sharedMesh = k.Build("yacht");
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { M(a => a.boat, "Hull", new Color(0.95f, 0.95f, 0.95f), 0.8f), M(a => a.boat, "Deck", new Color(0.9f, 0.9f, 0.88f), 0.6f), M(a => a.glass, "Glass", new Color(0.05f, 0.08f, 0.1f), 0.95f) };
            go.AddComponent<Bob>().phase = rng.Range(0f, 6f);
        }

        public static void Lighthouse(Transform parent, Vector3 pos)
        {
            var k = new MeshKit();
            k.matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            k.Lathe(0, new[] { new Vector2(3.2f, 0f), new Vector2(2.4f, 22f), new Vector2(2.6f, 22.2f), new Vector2(2.6f, 23f) }, 20, 1f, true, 3f);
            k.Lathe(1, new[] { new Vector2(1.8f, 23f), new Vector2(1.8f, 26f), new Vector2(0.4f, 27.5f), new Vector2(0.01f, 28f) }, 16);
            k.matrix = Matrix4x4.identity;
            var go = new GameObject("Lighthouse");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = k.Build("lighthouse");
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { M(a => a.buildingStucco, "Tower", new Color(0.95f, 0.95f, 0.93f), 0.3f), M(a => a.lampHead, "Lantern", new Color(1f, 0.9f, 0.6f), 0.5f) };
            var c = go.AddComponent<CapsuleCollider>(); c.center = pos + Vector3.up * 12f; c.radius = 3f; c.height = 26f;
            go.layer = Layers.Barrier;
        }

        public static void Pagoda(Transform parent, Vector3 pos, float yaw)
        {
            var body = new MeshAcc();
            var roofs = new MeshAcc();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            float y = 0f;
            for (int t = 0; t < 5; t++)
            {
                float w = 9f - t * 1.4f;
                body.Box(pos + Vector3.up * (y + 1.6f), new Vector3(w, 3.2f, w), rot, 0.3f);
                y += 3.2f;
                // Upswept eave roof: a flattened pyramid frustum.
                float ow = w + 3f;
                Vector3 c = pos + Vector3.up * y;
                Vector3[] lo = { c + rot * new Vector3(ow * 0.5f, -0.3f, ow * 0.5f), c + rot * new Vector3(-ow * 0.5f, -0.3f, ow * 0.5f), c + rot * new Vector3(-ow * 0.5f, -0.3f, -ow * 0.5f), c + rot * new Vector3(ow * 0.5f, -0.3f, -ow * 0.5f) };
                Vector3[] hi = { c + rot * new Vector3(w * 0.3f, 0.9f, w * 0.3f), c + rot * new Vector3(-w * 0.3f, 0.9f, w * 0.3f), c + rot * new Vector3(-w * 0.3f, 0.9f, -w * 0.3f), c + rot * new Vector3(w * 0.3f, 0.9f, -w * 0.3f) };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 n = Vector3.Cross(lo[(i + 1) % 4] - lo[i], hi[i] - lo[i]).normalized;
                    if (n.y < 0) n = -n;
                    int a = roofs.Add(lo[i], n, new Vector2(0, 0)), b = roofs.Add(lo[(i + 1) % 4], n, new Vector2(ow / 2f, 0)), cc = roofs.Add(hi[(i + 1) % 4], n, new Vector2(ow / 2f, 1)), d = roofs.Add(hi[i], n, new Vector2(0, 1));
                    roofs.Quad(a, b, cc, d, n);
                }
                y += 0.9f;
            }
            body.Box(pos + Vector3.up * (y + 2.5f), new Vector3(0.3f, 5f, 0.3f), rot, 1f);
            body.Emit(parent, "Pagoda", M(a => a.buildingWarm, "Wood", new Color(0.55f, 0.18f, 0.12f), 0.3f), Layers.Barrier, true);
            roofs.Emit(parent, "PagodaRoofs", M(a => a.roof, "Roof", new Color(0.2f, 0.22f, 0.24f), 0.4f), Layers.Scenery, false);
        }

        public static void FerrisWheel(Transform parent, Vector3 pos, float yaw)
        {
            var rootGo = new GameObject("FerrisWheel");
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var legs = new MeshKit();
            float R = 32f, hub = R + 6f;
            foreach (float z in new[] { -2.5f, 2.5f })
            {
                legs.Tube(0, new[] { new Vector3(-14f, 0f, z), new Vector3(0f, hub, z) }, 0.5f, 8);
                legs.Tube(0, new[] { new Vector3(14f, 0f, z), new Vector3(0f, hub, z) }, 0.5f, 8);
            }
            var lg = new GameObject("Legs");
            lg.transform.SetParent(rootGo.transform, false);
            lg.AddComponent<MeshFilter>().sharedMesh = legs.Build("legs");
            lg.AddComponent<MeshRenderer>().sharedMaterial = M(a => a.metal, "Steel", new Color(0.85f, 0.85f, 0.88f), 0.5f);
            var wheel = new MeshKit();
            var rim = new Vector3[49];
            for (int i = 0; i <= 48; i++) { float a = i / 48f * Mathf.PI * 2f; rim[i] = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f); }
            wheel.Tube(0, rim, 0.45f, 8, false);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                wheel.Tube(0, new[] { Vector3.zero, new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f) }, 0.12f, 5, false);
                wheel.Box(1, new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R - 2f, 0f), new Vector3(2.2f, 2.4f, 2.2f));
            }
            var wg = new GameObject("Wheel");
            wg.transform.SetParent(rootGo.transform, false);
            wg.transform.localPosition = new Vector3(0f, hub, 0f);
            wg.AddComponent<MeshFilter>().sharedMesh = wheel.Build("ferris");
            wg.AddComponent<MeshRenderer>().sharedMaterials = new[] { M(a => a.metal, "Steel", new Color(0.85f, 0.85f, 0.88f), 0.5f), M(a => a.neonA, "Cabins", new Color(0.9f, 0.4f, 0.3f), 0.5f) };
            wg.AddComponent<Spin>().degreesPerSecond = new Vector3(0f, 0f, 2f);
        }

        public static void Castle(Transform parent, Vector3 pos, float yaw)
        {
            var acc = new MeshAcc();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            float s = 40f;
            Vector3[] corners = { new Vector3(s, 0, s), new Vector3(-s, 0, s), new Vector3(-s, 0, -s), new Vector3(s, 0, -s) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = pos + rot * corners[i], b = pos + rot * corners[(i + 1) % 4];
                acc.Box((a + b) * 0.5f + Vector3.up * 5f, new Vector3(Vector3.Distance(a, b), 12f, 3f), Quaternion.LookRotation(b - a) * Quaternion.Euler(0f, 90f, 0f), 0.25f);
                acc.Box(a + Vector3.up * 8f, new Vector3(9f, 20f, 9f), rot, 0.25f);
                // Crenellations.
                for (int m = 1; m < 10; m += 2)
                {
                    Vector3 p = Vector3.Lerp(a, b, m / 10f) + Vector3.up * 11.6f;
                    acc.Box(p, new Vector3(2f, 1.2f, 3.2f), Quaternion.LookRotation(b - a), 0.25f);
                }
            }
            acc.Box(pos + Vector3.up * 14f, new Vector3(16f, 32f, 16f), rot, 0.25f);
            acc.Emit(parent, "Castle", M(a => a.rock, "Stone", new Color(0.62f, 0.58f, 0.52f), 0.15f), Layers.Barrier, true);
        }

        public static void Windmill(Transform parent, Vector3 pos, float yaw, bool modern)
        {
            var go = new GameObject("Windmill");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var k = new MeshKit();
            float hubY;
            if (modern)
            {
                hubY = 70f;
                k.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                k.Lathe(0, new[] { new Vector2(2.2f, 0f), new Vector2(1.3f, hubY) }, 14);
                k.matrix = Matrix4x4.identity;
                k.Box(0, new Vector3(0f, hubY, -1f), new Vector3(2.6f, 2.6f, 7f));
            }
            else
            {
                hubY = 12f;
                k.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                k.Lathe(0, new[] { new Vector2(4f, 0f), new Vector2(3f, 11f), new Vector2(0.01f, 15f) }, 10);
                k.matrix = Matrix4x4.identity;
            }
            go.AddComponent<MeshFilter>().sharedMesh = k.Build("mill");
            go.AddComponent<MeshRenderer>().sharedMaterial = modern ? M(a => a.concrete, "White", new Color(0.92f, 0.92f, 0.92f), 0.4f) : M(a => a.buildingWarm, "Mill", new Color(0.85f, 0.8f, 0.72f), 0.2f);
            var rotor = new GameObject("Rotor");
            rotor.transform.SetParent(go.transform, false);
            rotor.transform.localPosition = new Vector3(0f, hubY, modern ? 2.6f : 3.4f);
            var rk = new MeshKit();
            int blades = modern ? 3 : 4;
            float len = modern ? 38f : 11f;
            for (int b = 0; b < blades; b++)
            {
                rk.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, b * 360f / blades), Vector3.one);
                if (modern) rk.Wing(0, 0.5f, len, 2.2f, 2f, 0f, 0f, 0.14f, 0.05f, null, null, 6);
                else rk.Box(0, new Vector3(len * 0.5f, 0f, 0f), new Vector3(len, 1.6f, 0.15f));
            }
            rotor.AddComponent<MeshFilter>().sharedMesh = rk.Build("rotor");
            rotor.AddComponent<MeshRenderer>().sharedMaterial = modern ? M(a => a.concrete, "White", new Color(0.92f, 0.92f, 0.92f), 0.4f) : M(a => a.bark, "Sails", new Color(0.4f, 0.32f, 0.25f), 0.2f);
            rotor.AddComponent<Spin>().degreesPerSecond = new Vector3(0f, 0f, modern ? 14f : 20f);
        }

        public static void Crane(Transform parent, Vector3 pos, float yaw, Rng rng)
        {
            var acc = new MeshAcc();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            float h = 42f;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                acc.Box(pos + r * sx * 9f + f * sz * 7f + Vector3.up * h * 0.5f, new Vector3(1.2f, h, 1.2f), rot);
            acc.Box(pos + Vector3.up * h, new Vector3(20f, 3f, 16f), rot);
            acc.Box(pos + Vector3.up * (h + 1f) + f * 25f, new Vector3(4f, 3f, 70f), rot);
            acc.Box(pos + Vector3.up * (h + 9f), new Vector3(2f, 16f, 2f), rot);
            acc.Emit(parent, "Crane", M(a => a.kerbRed, "Crane", new Color(0.8f, 0.25f, 0.1f), 0.4f), Layers.Barrier, true);
        }

        public static void Containers(Transform parent, Vector3 pos, float yaw, int rows, int cols, int maxStack, Rng rng)
        {
            var acc = new MeshAcc();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            for (int x = 0; x < cols; x++)
            for (int z = 0; z < rows; z++)
            {
                int stack = rng.Range(1, maxStack + 1);
                for (int s = 0; s < stack; s++)
                {
                    Vector3 c = pos + r * x * 2.6f + f * z * 12.6f + Vector3.up * (s * 2.6f + 1.3f);
                    acc.Box(c, new Vector3(2.45f, 2.55f, 12.1f), rot, 0.4f);
                    // Each container gets its own colour from the texture strip (v offset by uv2 is not available: use uv shift).
                    int baseIdx = acc.Count - 24;
                    float shift = rng.Range(0, 8) / 8f;
                    for (int v = baseIdx; v < acc.Count; v++) acc.uv[v] = new Vector2(acc.uv[v].x * 0.08f, shift + acc.uv[v].y * 0.02f);
                }
            }
            acc.Emit(parent, "Containers", M(a => a.container, "Containers", new Color(0.6f, 0.3f, 0.2f), 0.4f), Layers.Barrier, true);
        }

        public static void NeonSign(Transform parent, Vector3 pos, Vector3 facing, float w, float h, int variant)
        {
            var acc = new MeshAcc();
            facing.y = 0f; facing.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, facing);
            int a = acc.Add(pos - r * w * 0.5f, facing, new Vector2((variant % 4) * 0.25f, (variant / 4 % 4) * 0.25f));
            int b = acc.Add(pos + r * w * 0.5f, facing, new Vector2((variant % 4) * 0.25f + 0.25f, (variant / 4 % 4) * 0.25f));
            int c = acc.Add(pos + r * w * 0.5f + Vector3.up * h, facing, new Vector2((variant % 4) * 0.25f + 0.25f, (variant / 4 % 4) * 0.25f + 0.25f));
            int d = acc.Add(pos - r * w * 0.5f + Vector3.up * h, facing, new Vector2((variant % 4) * 0.25f, (variant / 4 % 4) * 0.25f + 0.25f));
            acc.Quad(a, b, c, d, facing);
            var mats = new[] { VXAssets.I?.neonA, VXAssets.I?.neonB, VXAssets.I?.neonC };
            acc.Emit(parent, "Neon", VXAssets.Or(mats[variant % 3], "Neon", new Color(1f, 0.2f, 0.6f), 0.5f), Layers.Scenery, false, null, false);
        }

        public static void Runway(Transform parent, Vector3 centre, float yaw, float length, float width)
        {
            var acc = new MeshAcc();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            int a = acc.Add(centre - f * length * 0.5f - r * width * 0.5f, Vector3.up, new Vector2(0, 0));
            int b = acc.Add(centre - f * length * 0.5f + r * width * 0.5f, Vector3.up, new Vector2(1, 0));
            int c = acc.Add(centre + f * length * 0.5f + r * width * 0.5f, Vector3.up, new Vector2(1, length / width));
            int d = acc.Add(centre + f * length * 0.5f - r * width * 0.5f, Vector3.up, new Vector2(0, length / width));
            acc.Quad(a, b, c, d, Vector3.up);
            acc.Emit(parent, "Runway", M(a => a.runway, "Runway", new Color(0.3f, 0.3f, 0.31f), 0.2f), Layers.Road, true, SurfaceType.Asphalt);
        }

        public static void StadiumBowl(Transform parent, Vector3 centre, float radiusX, float radiusZ)
        {
            var acc = new MeshAcc();
            int seg = 64;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                for (int ring = 0; ring < 2; ring++)
                {
                    float ri = ring == 0 ? 1f : 1.12f, h0 = ring == 0 ? 2f : 22f, ro = ring == 0 ? 1.12f : 1.16f, h1 = ring == 0 ? 22f : 26f;
                    Vector3 p0 = centre + new Vector3(Mathf.Cos(a0) * radiusX * ri, h0, Mathf.Sin(a0) * radiusZ * ri);
                    Vector3 p1 = centre + new Vector3(Mathf.Cos(a1) * radiusX * ri, h0, Mathf.Sin(a1) * radiusZ * ri);
                    Vector3 p2 = centre + new Vector3(Mathf.Cos(a1) * radiusX * ro, h1, Mathf.Sin(a1) * radiusZ * ro);
                    Vector3 p3 = centre + new Vector3(Mathf.Cos(a0) * radiusX * ro, h1, Mathf.Sin(a0) * radiusZ * ro);
                    Vector3 inward = (centre - (p0 + p1) * 0.5f); inward.y = 0f;
                    Vector3 n = (inward.normalized + Vector3.up * 1.2f).normalized;
                    int a = acc.Add(p0, n, new Vector2(i * 2f, 0)), b = acc.Add(p1, n, new Vector2(i * 2f + 2f, 0)), c = acc.Add(p2, n, new Vector2(i * 2f + 2f, 6)), d = acc.Add(p3, n, new Vector2(i * 2f, 6));
                    acc.Quad(a, b, c, d, n);
                }
            }
            acc.Emit(parent, "StadiumBowl", M(a => a.crowd, "Crowd", new Color(0.5f, 0.45f, 0.42f), 0.2f), Layers.Scenery, false);
        }

        /// <summary>Distant mountain silhouettes around the map so the horizon reads as landscape, not an edge.</summary>
        public static void Horizon(Transform parent, MapDef map, float radius, float baseY, float height, int seed)
        {
            if (height <= 1f) return;
            var acc = new MeshAcc();
            int seg = 160;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float n = Noise.Ridged(Mathf.Cos(a) * 3f + seed, Mathf.Sin(a) * 3f, 5);
                float h = height * (0.25f + n * 0.95f);
                // Skip the sea side for coastal maps (open water horizon).
                if (map.Has(Feature.Sea) && map.terrain != TerrainStyle.Island)
                {
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    if (Vector2.Dot(d, map.seaDir.normalized) > 0.2f) h = 0f;
                }
                if (map.terrain == TerrainStyle.Island) h *= 0.15f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, baseY - 40f, Mathf.Sin(a) * radius);
                Vector3 inward = -new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                acc.Add(p, inward, new Vector2(i / 8f, 0f));
                acc.Add(p + Vector3.up * (h + 40f) + inward * h * 0.4f, (inward + Vector3.up).normalized, new Vector2(i / 8f, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                acc.Quad(a, a + 2, a + 3, a + 1, -new Vector3(Mathf.Cos(i / (float)seg * Mathf.PI * 2f), 0f, Mathf.Sin(i / (float)seg * Mathf.PI * 2f)));
            }
            var go = acc.Emit(parent, "Horizon", M(a => a.mountainFar, "Far hills", new Color(0.45f, 0.5f, 0.55f), 0.1f), Layers.Scenery, false, null, false);
        }
    }

    public class Spin : MonoBehaviour
    {
        public Vector3 degreesPerSecond;
        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }

    public class Bob : MonoBehaviour
    {
        public float phase;
        Vector3 basePos; Quaternion baseRot;
        void Start() { basePos = transform.position; baseRot = transform.rotation; }
        void Update()
        {
            float t = Time.time * 0.6f + phase;
            transform.position = basePos + Vector3.up * Mathf.Sin(t) * 0.12f;
            transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.8f) * 1.2f, 0f, Mathf.Sin(t * 1.1f) * 1.8f);
        }
    }
}
