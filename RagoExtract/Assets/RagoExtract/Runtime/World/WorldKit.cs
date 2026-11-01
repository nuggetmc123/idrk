using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Batched level builder: every box/cylinder added is merged into one mesh per material (few draw calls on Quest)
    /// and gets a matching collider tagged with an impact surface. Includes a library of props.
    /// </summary>
    public class WorldBuilder
    {
        public readonly Transform Root;
        readonly Dictionary<Material, MB> _batches = new Dictionary<Material, MB>();
        readonly Dictionary<Synth.Surface, Transform> _colRoots = new Dictionary<Synth.Surface, Transform>();
        public Rng Rng;

        public WorldBuilder(Transform root, int seed) { Root = root; Rng = new Rng(seed); }

        MB Batch(Material m)
        {
            if (!_batches.TryGetValue(m, out var mb)) { mb = new MB(); _batches[m] = mb; }
            if (mb.VertexCount > 55000)
            {
                Flush(m, mb);
                mb = new MB();
                _batches[m] = mb;
            }
            return mb;
        }

        Transform ColRoot(Synth.Surface s)
        {
            if (!_colRoots.TryGetValue(s, out var t))
            {
                t = new GameObject("Colliders_" + s).transform;
                t.SetParent(Root, false);
                t.gameObject.AddComponent<SurfaceTag>().surface = s;
                t.gameObject.isStatic = true;
                _colRoots[s] = t;
            }
            return t;
        }

        public void Collider(Vector3 c, Vector3 size, Quaternion rot, Synth.Surface s)
        {
            var go = new GameObject("c");
            go.transform.SetParent(ColRoot(s), false);
            go.transform.SetPositionAndRotation(c, rot);
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        public void Box(Vector3 c, Vector3 size, Material m, Color tint, Quaternion? rot = null, bool col = true, Synth.Surface s = Synth.Surface.Concrete, float bevel = 0f)
        {
            var r = rot ?? Quaternion.identity;
            Batch(m).Box(c, size, tint, bevel, bevel > 0 ? 1 : 0, r);
            if (col) Collider(c, size, r, s);
        }

        public void Cyl(Vector3 a, Vector3 b, float r1, float r2, int seg, Material m, Color tint, bool col = true, Synth.Surface s = Synth.Surface.Concrete)
        {
            Batch(m).Cyl(a, b, r1, r2, seg, tint, true);
            if (col)
            {
                var go = new GameObject("c");
                go.transform.SetParent(ColRoot(s), false);
                go.transform.position = (a + b) * 0.5f;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = Mathf.Max(r1, r2); cc.height = (b - a).magnitude + cc.radius * 2; cc.direction = 1;
            }
        }

        public void Sphere(Vector3 c, Vector3 radii, int seg, Material m, Color tint, bool col = true, Synth.Surface s = Synth.Surface.Concrete)
        {
            Batch(m).Sphere(c, radii, seg, tint);
            if (col)
            {
                var go = new GameObject("c");
                go.transform.SetParent(ColRoot(s), false);
                go.transform.position = c;
                go.AddComponent<SphereCollider>().radius = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z)) * 0.95f;
            }
        }

        public MB Raw(Material m) => Batch(m);

        void Flush(Material m, MB mb)
        {
            if (mb.VertexCount == 0) return;
            var go = mb.Build("Mesh_" + m.name, Root, m);
            go.isStatic = true;
        }

        public void Finish()
        {
            foreach (var kv in _batches) Flush(kv.Key, kv.Value);
            _batches.Clear();
        }

        // =========================================================================== props
        public void Ground(Vector3 center, Vector2 size, Material m, Synth.Surface s, float y = 0)
        {
            Box(center + new Vector3(0, y - 0.5f, 0), new Vector3(size.x, 1f, size.y), m, Color.white, null, true, s);
        }

        public void Terrain(Vector3 origin, float size, int res, System.Func<float, float, float> h, Material m, System.Func<float, float, Color> col, Synth.Surface s)
        {
            var mb = new MB();
            mb.Grid(origin, size, size, res, h, col);
            var go = mb.Build("Terrain", Root, m);
            go.isStatic = true;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            go.AddComponent<SurfaceTag>().surface = s;
        }

        /// <summary>Wall from a to b (on the ground plane) with rectangular openings (centre distance along the wall, width, bottom, top).</summary>
        public void Wall(Vector3 a, Vector3 b, float height, float thick, Material m, Color tint, List<Vector4> openings = null, Synth.Surface s = Synth.Surface.Concrete)
        {
            Vector3 d = b - a; d.y = 0;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector3 dir = d / len;
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up) * Quaternion.Euler(0, 90, 0);
            rot = Quaternion.LookRotation(dir, Vector3.up);
            var ops = openings ?? new List<Vector4>();
            ops.Sort((x, y) => x.x.CompareTo(y.x));
            float cursor = 0;
            foreach (var o in ops)
            {
                float o0 = Mathf.Clamp(o.x - o.y * 0.5f, 0, len), o1 = Mathf.Clamp(o.x + o.y * 0.5f, 0, len);
                if (o0 > cursor) Seg(a, dir, rot, cursor, o0, 0, height, thick, m, tint, s);
                if (o.z > 0.01f) Seg(a, dir, rot, o0, o1, 0, o.z, thick, m, tint, s);           // below window
                if (o.w < height - 0.01f) Seg(a, dir, rot, o0, o1, o.w, height, thick, m, tint, s); // lintel
                cursor = Mathf.Max(cursor, o1);
            }
            if (cursor < len) Seg(a, dir, rot, cursor, len, 0, height, thick, m, tint, s);
        }

        void Seg(Vector3 a, Vector3 dir, Quaternion rot, float s0, float s1, float y0, float y1, float thick, Material m, Color tint, Synth.Surface s)
        {
            if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
            Vector3 c = a + dir * ((s0 + s1) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
            Box(c, new Vector3(thick, y1 - y0, s1 - s0), m, tint, rot, true, s);
        }

        /// <summary>Hollow building with doors, windows, floors, stairs and a flat roof. Returns interior floor points for props/loot.</summary>
        public List<Vector3> Building(Vector3 pos, float yaw, Vector2 size, int floors, Material wall, Color tint, Material floorMat, Material trim, int doorSide = 0, bool windows = true, bool roofAccess = false, float floorH = 3.2f)
        {
            var interior = new List<Vector3>();
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            float hx = size.x * 0.5f, hz = size.y * 0.5f, t = 0.25f;
            Vector3 P(float x, float y, float z) => pos + q * new Vector3(x, y, z);
            for (int f = 0; f < floors; f++)
            {
                float y = f * floorH;
                var corners = new[] { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz) };
                for (int side = 0; side < 4; side++)
                {
                    Vector3 a = corners[side], b = corners[(side + 1) % 4];
                    float len = (b - a).magnitude;
                    var ops = new List<Vector4>();
                    if (f == 0 && (side == doorSide || (size.x > 12 && side == (doorSide + 2) % 4))) ops.Add(new Vector4(len * 0.5f, 1.7f, 0, 2.4f));
                    if (windows)
                    {
                        int nw = Mathf.FloorToInt(len / 3.2f);
                        for (int w = 0; w < nw; w++)
                        {
                            float c = (w + 0.5f) * len / nw;
                            if (ops.Count > 0 && Mathf.Abs(c - ops[0].x) < 1.8f && f == 0) continue;
                            ops.Add(new Vector4(c, 1.2f, 1.0f, 2.1f));
                        }
                    }
                    Wall(P(a.x, y, a.z), P(b.x, y, b.z), floorH, t, wall, tint, ops);
                }
                // floor slab (with a stair hole on upper floors)
                if (f == 0) Box(P(0, 0.05f, 0), new Vector3(size.x, 0.1f, size.y), floorMat, Color.white, q, true, Synth.Surface.Wood);
                else
                {
                    float holeX = hx - 1.3f;
                    Box(P(-1.3f, y, 0), new Vector3(size.x - 2.6f, 0.2f, size.y), floorMat, Color.white, q, true, Synth.Surface.Wood);
                    Box(P(holeX, y, -hz * 0.5f - 1.2f), new Vector3(2.6f, 0.2f, hz - 2.4f + 0.001f), floorMat, Color.white, q, true, Synth.Surface.Wood);
                }
                // stairs up to next floor (ramp collider + visual steps) along the +x wall
                if (f < floors - 1 || roofAccess)
                {
                    float sx = hx - 1.3f;
                    float run = Mathf.Min(size.y - 2f, 5.2f);
                    Vector3 s0 = new Vector3(sx, y, hz - 0.6f), s1 = new Vector3(sx, y + floorH, hz - 0.6f - run);
                    Stairs(P(s0.x, s0.y, s0.z), P(s1.x, s1.y, s1.z), 1.3f, trim);
                }
                for (int k = 0; k < 3; k++) interior.Add(P(Rng.Range(-hx + 1f, hx - 2.5f), y + 0.12f, Rng.Range(-hz + 1f, hz - 1f)));
                // ceiling light
                var lamp = P(0, y + floorH - 0.15f, 0);
                Box(lamp, new Vector3(0.9f, 0.06f, 0.2f), Mats.Lit("lamp_on", new Color(1f, 0.95f, 0.8f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(2.2f, 2f, 1.6f)), Color.white, q, false);
            }
            // roof
            float top = floors * floorH;
            Box(P(0, top, 0), new Vector3(size.x + 0.3f, 0.25f, size.y + 0.3f), trim, Color.white, q, true);
            // parapet
            Wall(P(-hx, top, -hz), P(hx, top, -hz), 0.9f, 0.2f, wall, tint * 0.9f);
            Wall(P(hx, top, -hz), P(hx, top, hz), 0.9f, 0.2f, wall, tint * 0.9f);
            Wall(P(hx, top, hz), P(-hx, top, hz), 0.9f, 0.2f, wall, tint * 0.9f);
            Wall(P(-hx, top, hz), P(-hx, top, -hz), 0.9f, 0.2f, wall, tint * 0.9f);
            // base trim
            Box(P(0, 0.15f, 0), new Vector3(size.x + 0.08f, 0.3f, size.y + 0.08f), trim, Color.white * 0.8f, q, false);
            return interior;
        }

        public void Stairs(Vector3 bottom, Vector3 top, float width, Material m)
        {
            Vector3 d = top - bottom;
            Vector3 flat = new Vector3(d.x, 0, d.z);
            float run = flat.magnitude, rise = d.y;
            Quaternion yaw = Quaternion.LookRotation(flat.normalized, Vector3.up);
            int steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.2f));
            for (int i = 0; i < steps; i++)
            {
                float t0 = (float)i / steps;
                Vector3 c = bottom + flat * (t0 + 0.5f / steps) + Vector3.up * (rise * (i + 0.5f) / steps);
                Box(c, new Vector3(width, rise / steps, run / steps), m, Color.white, yaw, false);
            }
            // smooth ramp for walking + NavMesh
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Collider(bottom + d * 0.5f + Vector3.up * 0.05f, new Vector3(width, 0.1f, Mathf.Sqrt(run * run + rise * rise)), yaw * Quaternion.Euler(-ang, 0, 0), Synth.Surface.Metal);
        }

        public void ShippingContainer(Vector3 pos, float yaw, Color col, bool open = false)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            var m = Mats.Lit("container", Color.white, 0.4f, 0.35f, Mats.Corrugated, null, 0.5f, 0.4f, true, 1.5f);
            float L = 6.06f, W = 2.44f, H = 2.59f;
            Vector3 P(float x, float y, float z) => pos + q * new Vector3(x, y, z);
            if (!open) Box(P(0, H * 0.5f, 0), new Vector3(W, H, L), m, col, q, true, Synth.Surface.Metal);
            else
            {
                Box(P(-W * 0.5f + 0.05f, H * 0.5f, 0), new Vector3(0.1f, H, L), m, col, q, true, Synth.Surface.Metal);
                Box(P(W * 0.5f - 0.05f, H * 0.5f, 0), new Vector3(0.1f, H, L), m, col, q, true, Synth.Surface.Metal);
                Box(P(0, H - 0.05f, 0), new Vector3(W, 0.1f, L), m, col, q, true, Synth.Surface.Metal);
                Box(P(0, 0.05f, 0), new Vector3(W, 0.1f, L), m, col * 0.7f, q, true, Synth.Surface.Metal);
                Box(P(0, H * 0.5f, -L * 0.5f + 0.05f), new Vector3(W, H, 0.1f), m, col, q, true, Synth.Surface.Metal);
                // open doors swung out
                Box(P(-W * 0.5f - 0.6f, H * 0.5f, L * 0.5f + 0.55f), new Vector3(0.06f, H, 1.2f), m, col, q * Quaternion.Euler(0, -70, 0), true, Synth.Surface.Metal);
            }
            // corner castings + frame
            var frame = Mats.Lit("container_frame", Color.white, 0.6f, 0.3f, Mats.Metal, null, 0.5f, 0.5f, true, 1f);
            foreach (var sx in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    Box(P(sx * (W * 0.5f - 0.06f), H * 0.5f, sz * (L * 0.5f - 0.06f)), new Vector3(0.14f, H + 0.02f, 0.14f), frame, col * 0.8f, q, false);
            if (!open) for (int i = 0; i < 4; i++) Box(P(-0.6f + i * 0.4f, H * 0.5f, L * 0.5f + 0.02f), new Vector3(0.04f, H * 0.85f, 0.04f), frame, Color.gray, q, false);
        }

        public void Crate(Vector3 pos, float yaw, Vector3 size, bool collider = true)
        {
            var m = Mats.Lit("crate_wood", Color.white, 0f, 0.25f, Mats.Wood, null, 0.4f, 0.1f, true, 1.2f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Box(pos + Vector3.up * size.y * 0.5f, size, m, new Color(0.75f, 0.6f, 0.42f), q, collider, Synth.Surface.Wood, 0.01f);
            Box(pos + Vector3.up * size.y * 0.5f, size + new Vector3(0.02f, -size.y * 0.7f, 0.02f), m, new Color(0.5f, 0.38f, 0.25f), q, false);
        }

        public void Barrel(Vector3 pos, Color col)
        {
            var m = Mats.Lit("barrel", Color.white, 0.5f, 0.35f, Mats.Metal, null, 0.6f, 0.5f);
            Cyl(pos, pos + Vector3.up * 0.9f, 0.3f, 0.3f, 14, m, col, true, Synth.Surface.Metal);
            Cyl(pos + Vector3.up * 0.3f, pos + Vector3.up * 0.33f, 0.31f, 0.31f, 14, m, col * 0.7f, false);
            Cyl(pos + Vector3.up * 0.6f, pos + Vector3.up * 0.63f, 0.31f, 0.31f, 14, m, col * 0.7f, false);
        }

        public void Sandbags(Vector3 a, Vector3 b, int rows = 3)
        {
            var m = Mats.Lit("sandbag", Color.white, 0f, 0.1f, Mats.Fabric, Mats.FabricN, 0.5f, 0f, true, 2f);
            Vector3 d = b - a; float len = d.magnitude; Vector3 dir = d / len;
            Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.55f));
            for (int r = 0; r < rows; r++)
                for (int i = 0; i < n; i++)
                {
                    float off = (r % 2) * 0.27f;
                    float t = (i + 0.5f) / n * len + off;
                    if (t > len) continue;
                    Vector3 c = a + dir * t + Vector3.up * (0.12f + r * 0.2f);
                    Box(c, new Vector3(0.45f, 0.2f, 0.56f), m, new Color(0.62f, 0.56f, 0.42f) * Rng.Range(0.85f, 1.05f), q * Quaternion.Euler(0, Rng.Range(-4f, 4f), 0), false, Synth.Surface.Dirt, 0.07f);
                }
            Collider(a + d * 0.5f + Vector3.up * rows * 0.1f, new Vector3(0.5f, rows * 0.2f + 0.05f, len), q, Synth.Surface.Dirt);
        }

        public void JerseyBarrier(Vector3 pos, float yaw)
        {
            var m = Mats.Lit("concrete_rough", Color.white, 0, 0.2f, Mats.Concrete, null, 0.5f, 0.2f, true, 0.6f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Box(pos + Vector3.up * 0.2f, new Vector3(0.6f, 0.4f, 3f), m, new Color(0.75f, 0.74f, 0.7f), q, true);
            Box(pos + Vector3.up * 0.6f, new Vector3(0.25f, 0.45f, 3f), m, new Color(0.75f, 0.74f, 0.7f), q, true, Synth.Surface.Concrete, 0.05f);
            Box(pos + Vector3.up * 0.82f + q * new Vector3(0, 0, 0), new Vector3(0.27f, 0.1f, 3.02f), Mats.Lit("paint_stripe", new Color(0.9f, 0.7f, 0.1f), 0, 0.3f), Color.white, q, false);
        }

        public void Pine(Vector3 pos, float h)
        {
            var bark = Mats.Lit("bark", Color.white, 0, 0.1f, Mats.Bark, null, 0.3f, 0f, true, 1.5f);
            var leaves = Mats.Lit("pine", Color.white, 0, 0.15f, Mats.Leaves, null, 0.3f, 0f, true, 0.8f);
            Cyl(pos - Vector3.up * 0.3f, pos + Vector3.up * h * 0.95f, h * 0.022f, h * 0.008f, 8, bark, new Color(0.55f, 0.45f, 0.38f), true, Synth.Surface.Wood);
            int layers = 4;
            Color tint = Color.Lerp(new Color(0.75f, 0.85f, 0.7f), new Color(0.6f, 0.65f, 0.5f), Rng.Value);
            for (int i = 0; i < layers; i++)
            {
                float t = (float)i / layers;
                float y0 = h * (0.28f + t * 0.62f), y1 = y0 + h * 0.3f;
                float r = h * 0.22f * (1f - t * 0.75f);
                Raw(leaves).Cyl(pos + Vector3.up * y0, pos + Vector3.up * y1, r, 0.02f, 9, tint * (0.85f + t * 0.25f), true, Rng.Value * 6f);
            }
        }

        public void Rock(Vector3 pos, float size)
        {
            var m = Mats.Lit("rock", Color.white, 0, 0.2f, Mats.Concrete, null, 0.6f, 0.1f, true, 0.7f);
            Vector3 r = new Vector3(size * Rng.Range(0.8f, 1.4f), size * Rng.Range(0.5f, 0.9f), size * Rng.Range(0.8f, 1.3f));
            Sphere(pos, r, 9, m, new Color(0.55f, 0.53f, 0.5f) * Rng.Range(0.8f, 1.1f), true, Synth.Surface.Concrete);
        }

        public void CarWreck(Vector3 pos, float yaw, Color col)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            var paint = Mats.Lit("carpaint", Color.white, 0.5f, 0.45f, Mats.Metal, null, 0.7f, 0.6f, true, 1f);
            var dark = Mats.Lit("cardark", new Color(0.08f, 0.08f, 0.08f), 0.3f, 0.3f);
            var glass = Mats.Lit("carglass", new Color(0.1f, 0.13f, 0.15f), 0.8f, 0.9f);
            Vector3 P(float x, float y, float z) => pos + q * new Vector3(x, y, z);
            Box(P(0, 0.7f, 0), new Vector3(1.8f, 0.7f, 4.3f), paint, col, q, true, Synth.Surface.Metal, 0.12f);
            Box(P(0, 1.3f, -0.3f), new Vector3(1.6f, 0.55f, 2.2f), paint, col, q, true, Synth.Surface.Metal, 0.12f);
            Box(P(0, 1.3f, -0.3f), new Vector3(1.62f, 0.4f, 2.0f), glass, Color.white, q, false);
            foreach (var sx in new[] { -0.85f, 0.85f })
                foreach (var sz in new[] { -1.35f, 1.35f })
                    Raw(dark).Cyl(P(sx - 0.12f, 0.35f, sz), P(sx + 0.12f, 0.35f, sz), 0.35f, 0.35f, 12, Color.white, true);
            Box(P(0, 0.55f, 2.17f), new Vector3(1.7f, 0.25f, 0.08f), dark, Color.white, q, false);
        }

        public void LightPole(Vector3 pos, float h, Color light, bool realLight, float range = 14f)
        {
            var m = Mats.Lit("pole", new Color(0.25f, 0.26f, 0.27f), 0.6f, 0.4f, Mats.Metal, null, 0.5f, 0.3f);
            Cyl(pos, pos + Vector3.up * h, 0.08f, 0.06f, 8, m, Color.white, true, Synth.Surface.Metal);
            Box(pos + new Vector3(0, h, 0.4f), new Vector3(0.12f, 0.08f, 0.9f), m, Color.white, null, false);
            Box(pos + new Vector3(0, h - 0.06f, 0.75f), new Vector3(0.3f, 0.06f, 0.45f), Mats.Lit("lamp_" + light, light, 0, 0.5f, null, null, 0, 0, false, 0.5f, light * 2.5f), Color.white, null, false);
            if (realLight) AddLight(pos + new Vector3(0, h - 0.3f, 0.75f), light, range, 1.6f);
        }

        public Light AddLight(Vector3 pos, Color c, float range, float intensity, bool shadows = false)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            return l;
        }

        public void Fence(Vector3 a, Vector3 b, float h = 2.2f)
        {
            var m = Mats.Lit("fence", new Color(0.45f, 0.46f, 0.45f), 0.8f, 0.4f, Mats.Metal, null, 0.4f, 0.4f);
            Vector3 d = b - a; float len = d.magnitude; Vector3 dir = d / len;
            int posts = Mathf.Max(2, Mathf.RoundToInt(len / 2.5f) + 1);
            for (int i = 0; i < posts; i++) { Vector3 p = a + dir * (len * i / (posts - 1)); Cyl(p, p + Vector3.up * h, 0.04f, 0.04f, 6, m, Color.white, false); }
            Quaternion q = Quaternion.LookRotation(dir);
            Box(a + d * 0.5f + Vector3.up * h, new Vector3(0.04f, 0.04f, len), m, Color.white, q, false);
            Box(a + d * 0.5f + Vector3.up * 0.1f, new Vector3(0.04f, 0.04f, len), m, Color.white, q, false);
            // see-through chain-link mesh panel
            Raw(Mats.Unlit("chainlink", new Color(0.32f, 0.33f, 0.33f, 0.3f))).Box(a + d * 0.5f + Vector3.up * h * 0.5f, new Vector3(0.01f, h * 0.9f, len), new Color(1, 1, 1), 0, 0, q);
            Collider(a + d * 0.5f + Vector3.up * h * 0.5f, new Vector3(0.1f, h, len), q, Synth.Surface.Metal);
        }

        public void Shelf(Vector3 pos, float yaw, float w = 2f)
        {
            var m = Mats.Lit("shelf", new Color(0.3f, 0.32f, 0.35f), 0.7f, 0.4f, Mats.Metal, null, 0.4f, 0.5f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 4; i++) Box(pos + q * new Vector3(0, 0.1f + i * 0.6f, 0), new Vector3(w, 0.04f, 0.6f), m, Color.white, q, true, Synth.Surface.Metal);
            foreach (var sx in new[] { -w / 2, w / 2 }) foreach (var sz in new[] { -0.28f, 0.28f }) Box(pos + q * new Vector3(sx, 1f, sz), new Vector3(0.05f, 2f, 0.05f), m, Color.white, q, false);
            for (int i = 0; i < 6; i++) Box(pos + q * new Vector3(Rng.Range(-w / 2 + 0.3f, w / 2 - 0.3f), 0.25f + Rng.Range(0, 3) * 0.6f, 0), new Vector3(Rng.Range(0.2f, 0.5f), Rng.Range(0.15f, 0.35f), Rng.Range(0.2f, 0.45f)), Mats.Lit("cardboard", Color.white, 0, 0.2f, Mats.Noise, null, 0.4f, 0f), new Color(0.65f, 0.5f, 0.33f), q, false);
        }

        public void Table(Vector3 pos, float yaw, Vector2 size, float h = 0.78f)
        {
            var m = Mats.Lit("table", Color.white, 0f, 0.35f, Mats.Wood, Mats.WoodN, 0.4f, 0.2f, true, 1f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Box(pos + Vector3.up * h, new Vector3(size.x, 0.05f, size.y), m, new Color(0.55f, 0.42f, 0.3f), q, true, Synth.Surface.Wood, 0.01f);
            foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
                    Box(pos + q * new Vector3(sx * (size.x / 2 - 0.06f), h / 2, sz * (size.y / 2 - 0.06f)), new Vector3(0.05f, h, 0.05f), m, new Color(0.4f, 0.3f, 0.22f), q, false);
        }

        public void Tent(Vector3 pos, float yaw, Vector3 size, Color col)
        {
            var m = Mats.Lit("tent", Color.white, 0, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0, true, 1.5f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            float ang = Mathf.Atan2(size.y, size.x * 0.5f) * Mathf.Rad2Deg;
            float slant = Mathf.Sqrt(size.y * size.y + size.x * size.x * 0.25f);
            Box(pos + q * new Vector3(-size.x * 0.25f, size.y * 0.5f, 0), new Vector3(slant, 0.04f, size.z), m, col, q * Quaternion.Euler(0, 0, ang), true, Synth.Surface.Dirt);
            Box(pos + q * new Vector3(size.x * 0.25f, size.y * 0.5f, 0), new Vector3(slant, 0.04f, size.z), m, col, q * Quaternion.Euler(0, 0, -ang), true, Synth.Surface.Dirt);
        }

        public void Watchtower(Vector3 pos, float h)
        {
            var wood = Mats.Lit("tower_wood", Color.white, 0, 0.2f, Mats.Wood, Mats.WoodN, 0.5f, 0.1f, true, 1f);
            foreach (var sx in new[] { -1.2f, 1.2f }) foreach (var sz in new[] { -1.2f, 1.2f })
                    Box(pos + new Vector3(sx, h / 2, sz), new Vector3(0.2f, h, 0.2f), wood, new Color(0.6f, 0.48f, 0.35f), null, true, Synth.Surface.Wood);
            Box(pos + new Vector3(0, h, 0), new Vector3(3f, 0.15f, 3f), wood, new Color(0.6f, 0.48f, 0.35f), null, true, Synth.Surface.Wood);
            Box(pos + new Vector3(0, h + 0.5f, 1.45f), new Vector3(3f, 1f, 0.08f), wood, new Color(0.55f, 0.43f, 0.3f), null, true, Synth.Surface.Wood);
            Box(pos + new Vector3(0, h + 0.5f, -1.45f), new Vector3(3f, 1f, 0.08f), wood, new Color(0.55f, 0.43f, 0.3f), null, true, Synth.Surface.Wood);
            Box(pos + new Vector3(1.45f, h + 0.5f, 0), new Vector3(0.08f, 1f, 3f), wood, new Color(0.55f, 0.43f, 0.3f), null, true, Synth.Surface.Wood);
            Box(pos + new Vector3(0, h + 2.4f, 0), new Vector3(3.4f, 0.12f, 3.4f), wood, new Color(0.45f, 0.35f, 0.25f), null, true, Synth.Surface.Wood);
            foreach (var sx in new[] { -1.4f, 1.4f }) foreach (var sz in new[] { -1.4f, 1.4f }) Box(pos + new Vector3(sx, h + 1.2f, sz), new Vector3(0.1f, 2.4f, 0.1f), wood, new Color(0.6f, 0.48f, 0.35f), null, false);
            // ramp up (open side -x)
            Stairs(pos + new Vector3(-1.5f - h * 1.1f, 0, 0), pos + new Vector3(-1.5f, h, 0), 1.0f, wood);
        }

        /// <summary>Water surface at center.y: you can fall in (and drown); bullets splash on it.</summary>
        public void Water(Vector3 center, Vector2 size)
        {
            var m = Mats.Lit("water", new Color(0.06f, 0.14f, 0.18f), 0.3f, 0.96f, null, null, 0, 0, true, 0.2f);
            Box(center + Vector3.down * 0.05f, new Vector3(size.x, 0.1f, size.y), m, Color.white, null, false);
            var go = new GameObject("WaterVolume");
            go.transform.SetParent(Root, false);
            go.transform.position = center + Vector3.down * 1.5f;
            var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(size.x, 3f, size.y); bc.isTrigger = true;
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Water;
            go.AddComponent<KillVolume>().killY = center.y - 1.2f;
            Box(center + Vector3.down * 4.5f, new Vector3(size.x, 1f, size.y), Mats.Lit("seabed", new Color(0.1f, 0.12f, 0.1f), 0, 0.1f), Color.white, null, true, Synth.Surface.Dirt);
        }

        public void Sign(Vector3 pos, Quaternion rot, string text, Color col, float size = 0.25f)
        {
            var go = new GameObject("Sign");
            go.transform.SetParent(Root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            UI.Text(go.transform, text, Vector3.zero, size, col, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }

    /// <summary>Deep water / void: kills whoever falls in.</summary>
    public class KillVolume : MonoBehaviour
    {
        public float killY;
        void Update()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null || !p.Vitals.Alive) return;
            var b = GetComponent<BoxCollider>();
            Vector3 pp = p.transform.position;
            if (b != null && pp.y < killY && Mathf.Abs(pp.x - b.bounds.center.x) < b.bounds.extents.x && Mathf.Abs(pp.z - b.bounds.center.z) < b.bounds.extents.z) p.Vitals.Kill("Drowned");
        }
    }
}
