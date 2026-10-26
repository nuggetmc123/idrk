using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    public enum Species { Fir, Pine, Broadleaf, Birch, Palm, Bush, Cactus, Scrub, Cypress, SnowFir, Rock }

    /// <summary>
    /// Procedural trees, bushes and rocks, drawn with GPU instancing in culled cells. Foliage is built from many
    /// alpha-cut leaf cards whose normals point out from the crown, which lights them like a soft volume instead of
    /// flat planes. Two detail levels per species. Trunks near roads get colliders.
    /// </summary>
    [ExecuteAlways]
    public class Vegetation : MonoBehaviour
    {
        class Kind
        {
            public Mesh near, far;
            public Material bark, leaves;
            public float radius; // trunk collider radius at scale 1
            public float height;
        }

        class Cell
        {
            public Bounds bounds;
            public readonly Dictionary<Species, List<Matrix4x4>> items = new Dictionary<Species, List<Matrix4x4>>();
        }

        readonly Dictionary<Species, Kind> kinds = new Dictionary<Species, Kind>();
        readonly Dictionary<Vector2Int, Cell> cells = new Dictionary<Vector2Int, Cell>();
        const float CellSize = 160f;
        public float drawDistance = 700f, nearDistance = 160f;
        readonly Plane[] planes = new Plane[6];
        readonly Matrix4x4[] batch = new Matrix4x4[1023];
        Transform colliderRoot;
        public int Count { get; private set; }

        public void Init()
        {
            drawDistance = Settings.Quality == 0 ? 450f : Settings.Quality == 1 ? 700f : 1000f;
            nearDistance = Settings.Quality == 0 ? 110f : 180f;
            colliderRoot = new GameObject("TreeColliders").transform;
            colliderRoot.SetParent(transform, false);
        }

        Kind Get(Species s)
        {
            if (kinds.TryGetValue(s, out var k)) return k;
            var A = VXAssets.I;
            k = new Kind();
            switch (s)
            {
                case Species.Palm: k.bark = VXAssets.Or(A?.palmBark, "Palm bark", new Color(0.45f, 0.38f, 0.3f)); k.leaves = VXAssets.Or(A?.leavesPalm, "Palm", new Color(0.3f, 0.45f, 0.18f)); break;
                case Species.Birch: k.bark = VXAssets.Or(A?.palmBark, "Birch bark", new Color(0.85f, 0.85f, 0.8f)); k.leaves = VXAssets.Or(A?.leavesBirch, "Birch", new Color(0.45f, 0.55f, 0.2f)); break;
                case Species.Broadleaf: case Species.Bush: case Species.Scrub: k.bark = VXAssets.Or(A?.bark, "Bark", new Color(0.3f, 0.24f, 0.18f)); k.leaves = VXAssets.Or(s == Species.Broadleaf ? A?.leavesBroad : A?.bush, "Leaves", new Color(0.22f, 0.35f, 0.14f)); break;
                case Species.Cactus: k.bark = VXAssets.Or(A?.cactus, "Cactus", new Color(0.3f, 0.42f, 0.25f)); k.leaves = k.bark; break;
                case Species.Rock: k.bark = VXAssets.Or(A?.rock, "Rock", new Color(0.5f, 0.48f, 0.45f)); k.leaves = k.bark; break;
                case Species.SnowFir: k.bark = VXAssets.Or(A?.bark, "Bark", new Color(0.3f, 0.24f, 0.18f)); k.leaves = VXAssets.Or(A?.snowCap, "Snowy needles", new Color(0.75f, 0.8f, 0.82f)); break;
                default: k.bark = VXAssets.Or(A?.bark, "Bark", new Color(0.3f, 0.24f, 0.18f)); k.leaves = VXAssets.Or(A?.leavesConifer, "Needles", new Color(0.15f, 0.25f, 0.14f)); break;
            }
            if (k.bark != null) k.bark.enableInstancing = true;
            if (k.leaves != null) k.leaves.enableInstancing = true;
            k.near = TreeKit.Build(s, true, out k.radius, out k.height);
            k.far = TreeKit.Build(s, false, out _, out _);
            kinds[s] = k;
            return k;
        }

        public void Add(Species s, Vector3 pos, float scale, float yaw, bool collider)
        {
            var k = Get(s);
            var key = new Vector2Int(Mathf.FloorToInt(pos.x / CellSize), Mathf.FloorToInt(pos.z / CellSize));
            if (!cells.TryGetValue(key, out var cell))
            {
                cell = new Cell { bounds = new Bounds(new Vector3((key.x + 0.5f) * CellSize, pos.y, (key.y + 0.5f) * CellSize), new Vector3(CellSize, 1f, CellSize)) };
                cells[key] = cell;
            }
            cell.bounds.Encapsulate(pos + Vector3.up * k.height * scale);
            cell.bounds.Encapsulate(pos - Vector3.up * 2f);
            if (!cell.items.TryGetValue(s, out var list)) { list = new List<Matrix4x4>(); cell.items[s] = list; }
            list.Add(Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale));
            Count++;
            if (collider && k.radius > 0f)
            {
                var go = new GameObject("t");
                go.layer = Layers.Barrier;
                go.transform.SetParent(colliderRoot, false);
                go.transform.position = pos;
                if (s == Species.Rock)
                {
                    var sc = go.AddComponent<SphereCollider>();
                    sc.radius = k.radius * scale;
                    sc.center = Vector3.up * k.radius * scale * 0.3f;
                }
                else
                {
                    var c = go.AddComponent<CapsuleCollider>();
                    c.radius = k.radius * scale;
                    c.height = 6f * scale;
                    c.center = Vector3.up * 3f * scale;
                }
            }
        }

        void LateUpdate()
        {
            if (Application.isPlaying) DrawFor(Cameras.Main, null);
        }

        // Outside Play mode (the editor's track preview) draw for each camera as it renders, scene view included.
        void OnEnable() { if (!Application.isPlaying) Camera.onPreCull += EditorDraw; }
        void OnDisable() { Camera.onPreCull -= EditorDraw; }
        void EditorDraw(Camera cam) { if (this != null && !Application.isPlaying) DrawFor(cam, cam); }

        void DrawFor(Camera cam, Camera target)
        {
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            Vector3 cp = cam.transform.position;
            float far2 = drawDistance * drawDistance, near2 = nearDistance * nearDistance;
            bool vr = UnityEngine.XR.XRSettings.isDeviceActive;
            foreach (var cell in cells.Values)
            {
                float d2 = cell.bounds.SqrDistance(cp);
                if (d2 > far2) continue;
                // In VR the culling camera is the centre eye; widen the test by using a slightly enlarged box.
                var b = cell.bounds;
                if (vr) b.Expand(30f);
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                bool near = d2 < near2;
                foreach (var kv in cell.items)
                {
                    var k = kinds[kv.Key];
                    Draw(near ? k.near : k.far, k, kv.Value, d2 < 140f * 140f, target);
                }
            }
        }

        void Draw(Mesh mesh, Kind k, List<Matrix4x4> list, bool shadows, Camera target)
        {
            var castMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < list.Count; i += 1023)
            {
                int n = Mathf.Min(1023, list.Count - i);
                list.CopyTo(i, batch, 0, n);
                Graphics.DrawMeshInstanced(mesh, 0, k.bark, batch, n, null, castMode, true, Layers.Scenery, target);
                if (mesh.subMeshCount > 1) Graphics.DrawMeshInstanced(mesh, 1, k.leaves, batch, n, null, castMode, true, Layers.Scenery, target);
            }
        }
    }

    /// <summary>Builds tree, bush and rock meshes (sub-mesh 0 bark, 1 leaves).</summary>
    public static class TreeKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Build(Species s, bool near, out float trunkRadius, out float height)
        {
            trunkRadius = 0.25f; height = 10f;
            string key = s + (near ? "N" : "F");
            var bark = new MeshAcc();
            var leaves = new MeshAcc();
            var rng = new Rng((int)s * 31 + 7);
            switch (s)
            {
                case Species.Fir: case Species.SnowFir: case Species.Pine: case Species.Cypress:
                {
                    bool pine = s == Species.Pine, cypress = s == Species.Cypress;
                    height = pine ? 14f : cypress ? 11f : 16f;
                    trunkRadius = pine ? 0.22f : 0.3f;
                    Trunk(bark, Vector3.zero, Vector3.up * height, trunkRadius, 0.05f, near ? 8 : 5);
                    int tiers = near ? (cypress ? 9 : 12) : 5;
                    for (int t = 0; t < tiers; t++)
                    {
                        float f = t / (float)(tiers - 1);
                        float y = Mathf.Lerp(pine ? height * 0.55f : height * 0.18f, height * 0.98f, f);
                        float radius = cypress ? Mathf.Lerp(1.3f, 0.3f, f * f) : pine ? Mathf.Lerp(3.2f, 1.2f, f) : Mathf.Lerp(3.6f, 0.5f, f);
                        int cards = near ? (pine ? 7 : 6) : 3;
                        for (int c = 0; c < cards; c++)
                        {
                            float a = (c / (float)cards + rng.Range(0f, 0.15f) + t * 0.37f) * Mathf.PI * 2f;
                            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                            Branch(leaves, new Vector3(0f, y, 0f), dir, radius, radius * (cypress ? 0.9f : 0.55f), pine ? 0.0f : -0.35f, height);
                        }
                    }
                    break;
                }
                case Species.Broadleaf: case Species.Birch:
                {
                    bool birch = s == Species.Birch;
                    height = birch ? 13f : 12f;
                    trunkRadius = birch ? 0.18f : 0.32f;
                    float crownY = height * 0.62f;
                    Vector3 crownSize = birch ? new Vector3(2.6f, 4.2f, 2.6f) : new Vector3(4.6f, 4f, 4.6f);
                    Trunk(bark, Vector3.zero, new Vector3(0.2f, crownY, 0f), trunkRadius, trunkRadius * 0.6f, near ? 8 : 5);
                    int branches = near ? 5 : 3;
                    for (int b = 0; b < branches; b++)
                    {
                        float a = b / (float)branches * Mathf.PI * 2f + rng.Range(0f, 0.6f);
                        Vector3 from = new Vector3(0.15f, crownY * rng.Range(0.7f, 0.95f), 0f);
                        Vector3 to = from + new Vector3(Mathf.Cos(a) * crownSize.x * 0.6f, rng.Range(1.5f, 3f), Mathf.Sin(a) * crownSize.z * 0.6f);
                        Trunk(bark, from, to, trunkRadius * 0.45f, 0.04f, 5);
                    }
                    int cards = near ? (birch ? 22 : 30) : 8;
                    Vector3 centre = new Vector3(0f, crownY + crownSize.y * 0.35f, 0f);
                    for (int c = 0; c < cards; c++)
                    {
                        Vector3 d = Random3(rng);
                        Vector3 p = centre + Vector3.Scale(d, crownSize) * rng.Range(0.45f, 0.95f);
                        float size = near ? rng.Range(2.2f, 3.4f) : 4.5f;
                        Card(leaves, p, (p - centre).normalized, size, centre, rng.Range(0f, 360f));
                    }
                    break;
                }
                case Species.Palm:
                {
                    height = 11f;
                    trunkRadius = 0.22f;
                    var path = new List<Vector3>();
                    float lean = rng.Range(0.8f, 1.8f);
                    for (int i = 0; i <= 10; i++)
                    {
                        float f = i / 10f;
                        path.Add(new Vector3(lean * f * f * 1.5f, f * height, 0f));
                    }
                    for (int i = 0; i < path.Count - 1; i++) Trunk(bark, path[i], path[i + 1], Mathf.Lerp(0.26f, 0.17f, i / 10f), Mathf.Lerp(0.26f, 0.17f, (i + 1) / 10f), near ? 7 : 5, i / 10f);
                    Vector3 top = path[path.Count - 1];
                    int fronds = near ? 11 : 6;
                    for (int f = 0; f < fronds; f++)
                    {
                        float a = f / (float)fronds * Mathf.PI * 2f + rng.Range(0f, 0.3f);
                        Frond(leaves, top, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), rng.Range(4f, 5.2f), rng.Range(0.2f, 0.9f), near ? 7 : 4);
                    }
                    break;
                }
                case Species.Bush: case Species.Scrub:
                {
                    height = s == Species.Bush ? 1.6f : 1.0f;
                    trunkRadius = 0f;
                    Vector3 centre = new Vector3(0f, height * 0.5f, 0f);
                    int cards = near ? 10 : 4;
                    for (int c = 0; c < cards; c++)
                    {
                        Vector3 d = Random3(rng);
                        d.y = Mathf.Abs(d.y) * 0.6f;
                        Vector3 p = centre + Vector3.Scale(d, new Vector3(1.2f, height * 0.6f, 1.2f));
                        Card(leaves, p, (p - centre + Vector3.up * 0.3f).normalized, height * rng.Range(1.1f, 1.6f), centre, rng.Range(0f, 360f));
                    }
                    Trunk(bark, Vector3.zero, Vector3.up * 0.3f, 0.05f, 0.03f, 4);
                    break;
                }
                case Species.Cactus:
                {
                    height = 6f;
                    trunkRadius = 0.3f;
                    Trunk(bark, Vector3.zero, Vector3.up * height, 0.32f, 0.28f, near ? 10 : 6);
                    for (int arm = 0; arm < 2; arm++)
                    {
                        float side = arm == 0 ? 1f : -1f;
                        float y = rng.Range(2f, 3.5f);
                        Trunk(bark, new Vector3(0f, y, 0f), new Vector3(side * 1.1f, y + 0.3f, 0f), 0.2f, 0.2f, 6);
                        Trunk(bark, new Vector3(side * 1.1f, y + 0.2f, 0f), new Vector3(side * 1.1f, y + 2.2f, 0f), 0.2f, 0.18f, 6);
                    }
                    break;
                }
                case Species.Rock:
                {
                    height = 2f;
                    trunkRadius = 1.4f;
                    RockMesh(bark, near ? 3 : 2, rng);
                    break;
                }
            }
            var mesh = new Mesh { name = key };
            var verts = new List<Vector3>(bark.v); verts.AddRange(leaves.v);
            var norms = new List<Vector3>(bark.n); norms.AddRange(leaves.n);
            var uvs = new List<Vector2>(bark.uv); uvs.AddRange(leaves.uv);
            var cols = new List<Color32>(bark.col); cols.AddRange(leaves.col);
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetColors(cols);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bark.t, 0);
            var lt = new List<int>(leaves.t.Count);
            foreach (int i in leaves.t) lt.Add(i + bark.Count);
            mesh.SetTriangles(lt, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            cache[key] = mesh;
            return mesh;
        }

        static Vector3 Random3(Rng r)
        {
            Vector3 v;
            do v = new Vector3(r.Range(-1f, 1f), r.Range(-1f, 1f), r.Range(-1f, 1f)); while (v.sqrMagnitude > 1f || v.sqrMagnitude < 0.05f);
            return v;
        }

        static void Trunk(MeshAcc m, Vector3 a, Vector3 b, float ra, float rb, int sides, float v0 = 0f)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 t1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 t2 = Vector3.Cross(axis, t1);
            int baseIdx = m.Count;
            float len = Vector3.Distance(a, b);
            for (int ring = 0; ring < 2; ring++)
            {
                Vector3 c = ring == 0 ? a : b;
                float r = ring == 0 ? ra : rb;
                for (int i = 0; i <= sides; i++)
                {
                    float ang = i / (float)sides * Mathf.PI * 2f;
                    Vector3 d = t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang);
                    m.Add(c + d * r, d, new Vector2(i / (float)sides * 2f, v0 + ring * len / 2f), Vector2.zero, new Color32(255, 255, 255, 255));
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int p = baseIdx + i, q = baseIdx + sides + 1 + i;
                Vector3 outDir = (m.v[p] - a);
                m.Quad(p, p + 1, q + 1, q, outDir);
            }
        }

        /// <summary>A drooping fir bough: a card from the trunk outwards, normals tilted up and out.</summary>
        static void Branch(MeshAcc m, Vector3 root, Vector3 dir, float length, float width, float droop, float treeH)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 tip = root + dir * length + Vector3.up * droop * length;
            Vector3 mid = root + dir * length * 0.5f + Vector3.up * droop * length * 0.3f;
            Vector3 nUp = (Vector3.up * 0.8f + dir * 0.6f).normalized;
            Color32 shade = new Color32((byte)(200 + 55 * root.y / treeH), 255, 255, 255);
            int a = m.Add(root - side * width * 0.2f, nUp, new Vector2(0.4f, 0f), Vector2.zero, shade);
            int b = m.Add(root + side * width * 0.2f, nUp, new Vector2(0.6f, 0f), Vector2.zero, shade);
            int c = m.Add(mid + side * width, nUp, new Vector2(1f, 0.5f), Vector2.zero, shade);
            int d = m.Add(tip + side * width * 0.15f, nUp, new Vector2(0.55f, 1f), Vector2.zero, shade);
            int e = m.Add(tip - side * width * 0.15f, nUp, new Vector2(0.45f, 1f), Vector2.zero, shade);
            int f = m.Add(mid - side * width, nUp, new Vector2(0f, 0.5f), Vector2.zero, shade);
            // Double-sided cards: leaves shader culls nothing; one winding is enough.
            m.t.Add(a); m.t.Add(b); m.t.Add(c);
            m.t.Add(a); m.t.Add(c); m.t.Add(d);
            m.t.Add(a); m.t.Add(d); m.t.Add(e);
            m.t.Add(a); m.t.Add(e); m.t.Add(f);
        }

        /// <summary>A leaf-cluster card facing outward, lit with crown-radial normals.</summary>
        static void Card(MeshAcc m, Vector3 p, Vector3 outward, float size, Vector3 centre, float roll)
        {
            Quaternion q = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            Vector3 r = q * Vector3.right * size * 0.5f, u = q * Vector3.up * size * 0.5f;
            Vector3[] corners = { p - r - u, p + r - u, p + r + u, p - r + u };
            Vector2[] uv = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            int baseIdx = m.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3 n = (corners[i] - centre).normalized;
                byte ao = (byte)Mathf.Clamp(150 + 105 * Mathf.Clamp01((corners[i].y - centre.y) / 3f + 0.5f), 0, 255);
                m.Add(corners[i], n, uv[i], Vector2.zero, new Color32(ao, ao, ao, 255));
            }
            m.t.Add(baseIdx); m.t.Add(baseIdx + 2); m.t.Add(baseIdx + 1);
            m.t.Add(baseIdx); m.t.Add(baseIdx + 3); m.t.Add(baseIdx + 2);
        }

        static void Frond(MeshAcc m, Vector3 top, Vector3 dir, float length, float lift, int segs)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            int baseIdx = m.Count;
            for (int i = 0; i <= segs; i++)
            {
                float f = i / (float)segs;
                Vector3 p = top + dir * length * f + Vector3.up * (lift * f * 2f - f * f * 2.6f);
                float w = Mathf.Sin(f * Mathf.PI) * 0.75f + 0.05f;
                Vector3 n = (Vector3.up + dir * 0.3f).normalized;
                m.Add(p - side * w + Vector3.down * w * 0.25f, n, new Vector2(0f, f), Vector2.zero, new Color32(255, 255, 255, 255));
                m.Add(p + side * w + Vector3.down * w * 0.25f, n, new Vector2(1f, f), Vector2.zero, new Color32(255, 255, 255, 255));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = baseIdx + i * 2;
                m.t.Add(a); m.t.Add(a + 2); m.t.Add(a + 1);
                m.t.Add(a + 1); m.t.Add(a + 2); m.t.Add(a + 3);
            }
        }

        static void RockMesh(MeshAcc m, int subdiv, Rng rng)
        {
            // Displaced icosphere, flattened a little and sunk into the ground.
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3> { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            var f = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            for (int s = 0; s < subdiv; s++)
            {
                var nf = new List<int>();
                var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(k, out int idx)) return idx;
                    v.Add((v[a] + v[b]) * 0.5f);
                    mid[k] = v.Count - 1;
                    return v.Count - 1;
                }
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            int baseIdx = m.Count;
            for (int i = 0; i < v.Count; i++)
            {
                Vector3 d = v[i].normalized;
                float n = 1f + Noise.Fbm(d.x * 1.7f + 3f, d.z * 1.7f + d.y * 1.3f, 3) * 0.45f;
                Vector3 p = Vector3.Scale(d * n, new Vector3(1.6f, 0.9f, 1.3f));
                p.y -= 0.3f;
                m.Add(p, d, new Vector2(p.x * 0.5f + p.y * 0.3f, p.z * 0.5f + p.y * 0.3f));
            }
            for (int i = 0; i < f.Count; i += 3) m.Tri(baseIdx + f[i], baseIdx + f[i + 1], baseIdx + f[i + 2], v[f[i]]);
            // Smooth normals from faces.
            var acc = new Vector3[v.Count];
            for (int i = 0; i < f.Count; i += 3)
            {
                Vector3 a = m.v[baseIdx + f[i]], b = m.v[baseIdx + f[i + 1]], c = m.v[baseIdx + f[i + 2]];
                Vector3 nn = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(nn, a) < 0f) nn = -nn;
                acc[f[i]] += nn; acc[f[i + 1]] += nn; acc[f[i + 2]] += nn;
            }
            for (int i = 0; i < v.Count; i++) m.n[baseIdx + i] = acc[i].normalized;
        }
    }

    /// <summary>The camera everything culls against (the VR head camera, or the desktop camera).</summary>
    public static class Cameras
    {
        public static Camera Main;
    }
}
