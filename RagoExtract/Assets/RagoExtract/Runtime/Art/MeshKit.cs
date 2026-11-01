using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rago
{
    /// <summary>
    /// Procedural mesh builder: bevelled boxes, cylinders, lathes, extruded profiles, spheres...
    /// Everything (guns, characters, props, buildings) is built from this so no model files are needed.
    /// Supports multiple sub-meshes (one per material) and a transform stack.
    /// </summary>
    public class MB
    {
        readonly List<Vector3> _v = new List<Vector3>(1024);
        readonly List<Vector3> _n = new List<Vector3>(1024);
        readonly List<Vector2> _uv = new List<Vector2>(1024);
        readonly List<Color> _c = new List<Color>(1024);
        readonly List<List<int>> _t = new List<List<int>>();
        int _sub;
        Matrix4x4 _m = Matrix4x4.identity;
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        public float UvScale = 1f;
        public int VertexCount => _v.Count;

        public MB(int submeshes = 1) { for (int i = 0; i < submeshes; i++) _t.Add(new List<int>()); }

        public MB Sub(int i) { while (_t.Count <= i) _t.Add(new List<int>()); _sub = i; return this; }
        public MB Push(Vector3 pos, Quaternion rot, Vector3? scale = null) { _stack.Push(_m); _m = _m * Matrix4x4.TRS(pos, rot, scale ?? Vector3.one); return this; }
        public MB Push(Matrix4x4 m) { _stack.Push(_m); _m = _m * m; return this; }
        public MB Pop() { _m = _stack.Pop(); return this; }

        int V(Vector3 p, Vector3 n, Vector2 uv, Color c)
        {
            _v.Add(_m.MultiplyPoint3x4(p));
            _n.Add(_m.MultiplyVector(n).normalized);
            _uv.Add(uv * UvScale);
            _c.Add(c);
            return _v.Count - 1;
        }
        void T(int a, int b, int c) { var l = _t[_sub]; l.Add(a); l.Add(b); l.Add(c); }

        public MB Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector2? uvSize = null)
        {
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            Vector2 s = uvSize ?? new Vector2((b - a).magnitude, (d - a).magnitude);
            int i0 = V(a, n, new Vector2(0, 0), col), i1 = V(b, n, new Vector2(s.x, 0), col), i2 = V(c, n, new Vector2(s.x, s.y), col), i3 = V(d, n, new Vector2(0, s.y), col);
            T(i0, i3, i2); T(i0, i2, i1);
            return this;
        }

        /// <summary>
        /// Smooth curved surface from a (nu x nv) grid of points p(u, v) with shared, smoothed normals.
        /// wrapU closes the surface around u (rings). flip makes it face the other way (insides of bags, tubes).
        /// </summary>
        public MB Patch(int nu, int nv, System.Func<int, int, Vector3> p, Color col, bool wrapU = false, bool flip = false, float uvU = 2f, float uvV = 2f)
        {
            int cols = nu + 1, start = _v.Count;
            for (int v = 0; v <= nv; v++)
                for (int u = 0; u <= nu; u++)
                {
                    int uu = wrapU ? u % nu : u;
                    Vector3 P = p(uu, v);
                    Vector3 du = p(wrapU ? (u + 1) % nu : Mathf.Min(u + 1, nu), v) - p(wrapU ? (uu - 1 + nu) % nu : Mathf.Max(u - 1, 0), v);
                    Vector3 dv = p(uu, Mathf.Min(v + 1, nv)) - p(uu, Mathf.Max(v - 1, 0));
                    Vector3 n = Vector3.Cross(dv, du);
                    if (n.sqrMagnitude < 1e-14f) n = Vector3.up;
                    if (flip) n = -n;
                    V(P, n.normalized, new Vector2(u / (float)nu * uvU, v / (float)nv * uvV), col);
                }
            for (int v = 0; v < nv; v++)
                for (int u = 0; u < nu; u++)
                {
                    int a = start + v * cols + u, b = a + 1, c = a + cols + 1, d = a + cols;
                    if (!flip) { T(a, d, c); T(a, c, b); } else { T(a, c, d); T(a, b, c); }
                }
            return this;
        }

        /// <summary>Box with optional chamfered/rounded edges (bevel in metres, segs 0=hard,1=chamfer,2=round).</summary>
        public MB Box(Vector3 center, Vector3 size, Color col, float bevel = 0f, int segs = 1, Quaternion? rot = null)
        {
            Quaternion r = rot ?? Quaternion.identity;
            Vector3 h = size * 0.5f;
            bevel = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            if (bevel <= 0.0001f || segs <= 0)
            {
                for (int f = 0; f < 6; f++) HardFace(center, h, r, f, col);
                return this;
            }
            Vector3 inner = h - Vector3.one * bevel;
            float[] st = segs >= 2 ? new[] { -1f, -0.7f, -0.0001f } : new[] { -1f, -0.0001f };
            // build axis sample positions along each axis
            for (int f = 0; f < 6; f++)
            {
                Vector3 nrm, ua, va;
                FaceAxes(f, out nrm, out ua, out va);
                float hu = Vector3.Dot(Abs(ua), h), hv = Vector3.Dot(Abs(va), h), hn = Vector3.Dot(Abs(nrm), h);
                float iu = Vector3.Dot(Abs(ua), inner), iv = Vector3.Dot(Abs(va), inner);
                var us = Samples(hu, iu, st); var vs = Samples(hv, iv, st);
                int nu = us.Count, nv = vs.Count;
                int baseIdx = _v.Count;
                for (int j = 0; j < nv; j++)
                    for (int i = 0; i < nu; i++)
                    {
                        Vector3 p = nrm * hn + ua * us[i] + va * vs[j];
                        Vector3 q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 d = p - q;
                        Vector3 n = d.sqrMagnitude > 1e-10f ? d.normalized : nrm;
                        Vector3 pos = q + n * bevel;
                        V(center + r * pos, r * n, new Vector2(us[i] + hu, vs[j] + hv), col);
                    }
                for (int j = 0; j < nv - 1; j++)
                    for (int i = 0; i < nu - 1; i++)
                    {
                        int a = baseIdx + j * nu + i, b = a + 1, c = a + nu, d2 = c + 1;
                        T(a, c, d2); T(a, d2, b);
                    }
            }
            return this;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static List<float> Samples(float h, float inner, float[] st)
        {
            var l = new List<float>();
            float b = h - inner;
            foreach (var s in st) l.Add(-inner + s * b);       // -h .. -inner
            for (int i = st.Length - 1; i >= 0; i--) l.Add(inner - st[i] * b); // inner .. h
            return l;
        }

        static void FaceAxes(int f, out Vector3 n, out Vector3 u, out Vector3 v)
        {
            switch (f)
            {
                case 0: n = Vector3.right; u = Vector3.forward; v = Vector3.up; break;
                case 1: n = Vector3.left; u = Vector3.back; v = Vector3.up; break;
                case 2: n = Vector3.up; u = Vector3.right; v = Vector3.forward; break;
                case 3: n = Vector3.down; u = Vector3.right; v = Vector3.back; break;
                case 4: n = Vector3.forward; u = Vector3.left; v = Vector3.up; break;
                default: n = Vector3.back; u = Vector3.right; v = Vector3.up; break;
            }
        }

        void HardFace(Vector3 c, Vector3 h, Quaternion r, int f, Color col)
        {
            FaceAxes(f, out var n, out var u, out var v);
            float hu = Vector3.Dot(Abs(u), h), hv = Vector3.Dot(Abs(v), h), hn = Vector3.Dot(Abs(n), h);
            Vector3 o = n * hn;
            int i0 = V(c + r * (o - u * hu - v * hv), r * n, new Vector2(0, 0), col);
            int i1 = V(c + r * (o + u * hu - v * hv), r * n, new Vector2(hu * 2, 0), col);
            int i2 = V(c + r * (o + u * hu + v * hv), r * n, new Vector2(hu * 2, hv * 2), col);
            int i3 = V(c + r * (o - u * hu + v * hv), r * n, new Vector2(0, hv * 2), col);
            T(i0, i3, i2); T(i0, i2, i1);
        }

        /// <summary>Cylinder / cone between two points.</summary>
        public MB Cyl(Vector3 a, Vector3 b, float r1, float r2, int seg, Color col, bool caps = true, float startAngle = 0f)
        {
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-5f) return this;
            Quaternion q = Quaternion.FromToRotation(Vector3.up, axis / len);
            int baseIdx = _v.Count;
            float slope = (r1 - r2) / len;
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg, ang = startAngle + t * Mathf.PI * 2;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                Vector3 n = q * new Vector3(dir.x, slope, dir.z).normalized;
                V(a + q * (dir * r1), n, new Vector2(t * Mathf.PI * 2 * r1, 0), col);
                V(a + q * (dir * r2 + Vector3.up * len), n, new Vector2(t * Mathf.PI * 2 * r1, len), col);
            }
            for (int i = 0; i < seg; i++)
            {
                int i0 = baseIdx + i * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                T(i0, i1, i3); T(i0, i3, i2);
            }
            if (caps)
            {
                Disc(a, -axis / len, r1, seg, col, q, startAngle);
                Disc(b, axis / len, r2, seg, col, q, startAngle);
            }
            return this;
        }

        void Disc(Vector3 c, Vector3 n, float r, int seg, Color col, Quaternion q, float startAngle)
        {
            if (r <= 0.0001f) return;
            int ci = V(c, n, Vector2.zero, col);
            int first = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float ang = startAngle + (float)i / seg * Mathf.PI * 2;
                Vector3 d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                V(c + q * (d * r), n, new Vector2(d.x * r, d.z * r), col);
            }
            bool up = Vector3.Dot(n, q * Vector3.up) > 0;
            for (int i = 0; i < seg; i++)
            {
                if (up) T(ci, first + i + 1, first + i); else T(ci, first + i, first + i + 1);
            }
        }

        /// <summary>Hollow tube (barrels, muzzle devices, scope tubes).</summary>
        public MB Tube(Vector3 a, Vector3 b, float rOut, float rIn, int seg, Color col)
        {
            Cyl(a, b, rOut, rOut, seg, col, false);
            // inner wall (flipped)
            Vector3 axis = (b - a); float len = axis.magnitude; Quaternion q = Quaternion.FromToRotation(Vector3.up, axis / len);
            int baseIdx = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float ang = (float)i / seg * Mathf.PI * 2; Vector3 d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                V(a + q * (d * rIn), q * -d, new Vector2(i, 0), col * 0.3f);
                V(b + q * (d * rIn), q * -d, new Vector2(i, 1), col * 0.3f);
            }
            for (int i = 0; i < seg; i++) { int i0 = baseIdx + i * 2; T(i0, i0 + 3, i0 + 1); T(i0, i0 + 2, i0 + 3); }
            Ring(a, -axis / len, rIn, rOut, seg, col, q);
            Ring(b, axis / len, rIn, rOut, seg, col, q);
            return this;
        }

        void Ring(Vector3 c, Vector3 n, float r0, float r1, int seg, Color col, Quaternion q)
        {
            int baseIdx = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float ang = (float)i / seg * Mathf.PI * 2; Vector3 d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                V(c + q * (d * r0), n, new Vector2(i, 0), col);
                V(c + q * (d * r1), n, new Vector2(i, 1), col);
            }
            bool up = Vector3.Dot(n, q * Vector3.up) > 0;
            for (int i = 0; i < seg; i++)
            {
                int i0 = baseIdx + i * 2;
                if (!up) { T(i0, i0 + 1, i0 + 3); T(i0, i0 + 3, i0 + 2); } else { T(i0, i0 + 3, i0 + 1); T(i0, i0 + 2, i0 + 3); }
            }
        }

        public MB Sphere(Vector3 c, Vector3 radii, int seg, Color col, float vMin = 0f, float vMax = 1f)
        {
            int rings = Mathf.Max(3, seg / 2);
            int baseIdx = _v.Count;
            for (int j = 0; j <= rings; j++)
            {
                float v = Mathf.Lerp(vMin, vMax, (float)j / rings);
                float phi = v * Mathf.PI;
                for (int i = 0; i <= seg; i++)
                {
                    float th = (float)i / seg * Mathf.PI * 2;
                    Vector3 d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    Vector3 p = Vector3.Scale(d, radii);
                    Vector3 n = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z).normalized;
                    V(c + p, n, new Vector2((float)i / seg, v), col);
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = baseIdx + j * (seg + 1) + i, b = a + seg + 1;
                    T(a, a + 1, b + 1); T(a, b + 1, b);
                }
            return this;
        }

        /// <summary>Flat double-sided disc sector in the plane facing rot*forward (crash-test markers, decals).</summary>
        public MB Sector(Vector3 c, Quaternion rot, float r, float a0, float a1, int seg, Color col)
        {
            Vector3 n = rot * Vector3.forward;
            for (int side = 0; side < 2; side++)
            {
                Vector3 nn = side == 0 ? n : -n;
                int ci = V(c, nn, new Vector2(0.5f, 0.5f), col);
                int first = _v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.Lerp(a0, a1, (float)i / seg) * Mathf.Deg2Rad;
                    V(c + rot * new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0), nn, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f), col);
                }
                for (int i = 0; i < seg; i++) { if (side == 0) T(ci, first + i + 1, first + i); else T(ci, first + i, first + i + 1); }
            }
            return this;
        }

        public MB Capsule(Vector3 a, Vector3 b, float r, int seg, Color col) => Capsule(a, b, r, r, seg, col);

        /// <summary>Tapered capsule (limbs). Hemispheres at both ends, cone between.</summary>
        public MB Capsule(Vector3 a, Vector3 b, float ra, float rb, int seg, Color col)
        {
            Vector3 axis = b - a; float len = axis.magnitude;
            if (len < 1e-4f) { Sphere(a, Vector3.one * ra, seg, col); return this; }
            Quaternion q = Quaternion.FromToRotation(Vector3.up, axis / len);
            int rings = Mathf.Max(2, seg / 4);
            int baseIdx = _v.Count;
            var rows = new List<(float y, float r, float ny)>();
            for (int j = 0; j <= rings; j++) { float ph = -Mathf.PI / 2 + (float)j / rings * Mathf.PI / 2; rows.Add((Mathf.Sin(ph) * ra, Mathf.Cos(ph) * ra, Mathf.Sin(ph))); }
            for (int j = 0; j <= rings; j++) { float ph = (float)j / rings * Mathf.PI / 2; rows.Add((len + Mathf.Sin(ph) * rb, Mathf.Cos(ph) * rb, Mathf.Sin(ph))); }
            foreach (var row in rows)
            {
                for (int i = 0; i <= seg; i++)
                {
                    float th = (float)i / seg * Mathf.PI * 2;
                    Vector3 d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                    float nh = Mathf.Sqrt(Mathf.Max(0, 1 - row.ny * row.ny));
                    Vector3 n = q * new Vector3(d.x * nh, row.ny, d.z * nh);
                    V(a + q * (d * row.r + Vector3.up * row.y), n, new Vector2((float)i / seg, row.y), col);
                }
            }
            for (int j = 0; j < rows.Count - 1; j++)
                for (int i = 0; i < seg; i++)
                {
                    int i0 = baseIdx + j * (seg + 1) + i, i1 = i0 + seg + 1;
                    T(i0, i1, i1 + 1); T(i0, i1 + 1, i0 + 1);
                }
            return this;
        }

        /// <summary>Revolve a (radius, height) profile around Y.</summary>
        public MB Lathe(Vector3 c, Quaternion rot, IList<Vector2> profile, int seg, Color col)
        {
            int baseIdx = _v.Count;
            for (int j = 0; j < profile.Count; j++)
            {
                Vector2 p = profile[j];
                Vector2 tan = j < profile.Count - 1 ? profile[j + 1] - p : p - profile[j - 1];
                if (j > 0 && j < profile.Count - 1) tan = profile[j + 1] - profile[j - 1];
                Vector2 n2 = new Vector2(tan.y, -tan.x).normalized;
                for (int i = 0; i <= seg; i++)
                {
                    float th = (float)i / seg * Mathf.PI * 2;
                    Vector3 d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                    V(c + rot * (d * p.x + Vector3.up * p.y), rot * (d * n2.x + Vector3.up * n2.y), new Vector2((float)i / seg, p.y), col);
                }
            }
            for (int j = 0; j < profile.Count - 1; j++)
                for (int i = 0; i < seg; i++)
                {
                    int i0 = baseIdx + j * (seg + 1) + i, i1 = i0 + seg + 1;
                    T(i0, i1, i1 + 1); T(i0, i1 + 1, i0 + 1);
                }
            return this;
        }

        /// <summary>
        /// Extrude a 2D side profile (points in Z/Y, counter-clockwise or clockwise) along X with chamfered edges.
        /// This is how gun receivers, stocks and grips get proper silhouettes.
        /// </summary>
        public MB Extrude(IList<Vector2> poly, float width, Color col, float bevel = 0.002f, Vector3? offset = null)
        {
            Vector3 off = offset ?? Vector3.zero;
            var pts = new List<Vector2>(poly);
            if (SignedArea(pts) < 0) pts.Reverse(); // make CCW
            int n = pts.Count;
            float hw = width * 0.5f;
            bevel = Mathf.Min(bevel, hw * 0.9f);
            // outward normals per vertex
            var vn = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n];
                Vector2 e0 = (p1 - p0).normalized, e1 = (p2 - p1).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x), n1 = new Vector2(e1.y, -e1.x);
                Vector2 m = (n0 + n1); m = m.sqrMagnitude < 1e-6f ? n1 : m.normalized;
                float dot = Mathf.Max(0.35f, Vector2.Dot(m, n1));
                vn[i] = m / dot;
            }
            var inset = new List<Vector2>(n);
            for (int i = 0; i < n; i++) inset.Add(pts[i] - vn[i] * bevel);
            var tris = Triangulate(inset);
            // caps
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? hw : -hw;
                Vector3 nrm = s == 0 ? Vector3.right : Vector3.left;
                int b0 = _v.Count;
                for (int i = 0; i < n; i++) V(off + new Vector3(x, inset[i].y, inset[i].x), nrm, new Vector2(inset[i].x, inset[i].y), col);
                for (int t = 0; t < tris.Count; t += 3)
                {
                    if (s == 0) T(b0 + tris[t], b0 + tris[t + 2], b0 + tris[t + 1]);
                    else T(b0 + tris[t], b0 + tris[t + 1], b0 + tris[t + 2]);
                }
            }
            // side band + chamfers: rings at -hw (inset), -hw+b (outline), hw-b (outline), hw (inset)
            float[] xs = { -hw, -hw + bevel, hw - bevel, hw };
            bool[] isInset = { true, false, false, true };
            int ringStart = _v.Count;
            float perim = 0;
            for (int r = 0; r < 4; r++)
            {
                perim = 0;
                for (int i = 0; i <= n; i++)
                {
                    int k = i % n;
                    if (i > 0) perim += (pts[k] - pts[i - 1]).magnitude;
                    Vector2 p = isInset[r] ? inset[k] : pts[k];
                    Vector2 on = vn[k].normalized;
                    Vector3 nrm = new Vector3(r == 0 ? -0.7f : r == 3 ? 0.7f : 0, on.y, on.x).normalized;
                    V(off + new Vector3(xs[r], p.y, p.x), nrm, new Vector2(perim, xs[r]), col);
                }
            }
            int rowLen = n + 1;
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < n; i++)
                {
                    int a = ringStart + r * rowLen + i, b = a + 1, c = a + rowLen, d = c + 1;
                    T(a, d, b); T(a, c, d);
                }
            return this;
        }

        static float SignedArea(List<Vector2> p)
        {
            float a = 0;
            for (int i = 0; i < p.Count; i++) { var q = p[i]; var r = p[(i + 1) % p.Count]; a += q.x * r.y - r.x * q.y; }
            return a * 0.5f;
        }

        /// <summary>Ear-clipping triangulation for simple (possibly concave) polygons, CCW.</summary>
        public static List<int> Triangulate(List<Vector2> pts)
        {
            var res = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < pts.Count; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i - 1 + idx.Count) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[i0], b = pts[i1], c = pts[i2];
                    if (Cross(b - a, c - b) <= 1e-9f) continue; // reflex
                    bool inside = false;
                    for (int j = 0; j < idx.Count; j++)
                    {
                        int k = idx[j];
                        if (k == i0 || k == i1 || k == i2) continue;
                        if (PointInTri(pts[k], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    res.Add(i0); res.Add(i1); res.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
            else if (idx.Count > 3) for (int i = 1; i < idx.Count - 1; i++) { res.Add(idx[0]); res.Add(idx[i]); res.Add(idx[i + 1]); }
            return res;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>Height-field grid (terrain).</summary>
        public MB Grid(Vector3 origin, float sizeX, float sizeZ, int res, System.Func<float, float, float> height, System.Func<float, float, Color> color)
        {
            int baseIdx = _v.Count;
            float dx = sizeX / res, dz = sizeZ / res;
            for (int j = 0; j <= res; j++)
                for (int i = 0; i <= res; i++)
                {
                    float x = origin.x + i * dx, z = origin.z + j * dz;
                    float y = height(x, z);
                    float hx = height(x + 0.5f, z) - height(x - 0.5f, z), hz = height(x, z + 0.5f) - height(x, z - 0.5f);
                    Vector3 n = new Vector3(-hx, 1f, -hz).normalized;
                    V(new Vector3(x, origin.y + y, z), n, new Vector2(x, z), color(x, z));
                }
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    int a = baseIdx + j * (res + 1) + i, b = a + 1, c = a + res + 1, d = c + 1;
                    T(a, c, d); T(a, d, b);
                }
            return this;
        }

        public Mesh ToMesh(string name = "mesh")
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetColors(_c);
            int subs = 0;
            for (int i = 0; i < _t.Count; i++) if (_t[i].Count > 0) subs = i + 1;
            m.subMeshCount = Mathf.Max(1, subs);
            for (int i = 0; i < m.subMeshCount; i++) m.SetTriangles(_t[i], i, false);
            m.RecalculateBounds();
            TryTangents(m);
            return m;
        }

        static void TryTangents(Mesh m) { try { m.RecalculateTangents(); } catch { } }

        /// <summary>Create a GameObject with this mesh and the given materials (one per sub-mesh).</summary>
        public GameObject Build(string name, Transform parent, params Material[] mats)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var mesh = ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var arr = new Material[mesh.subMeshCount];
            for (int i = 0; i < arr.Length; i++) arr[i] = mats.Length == 0 ? Mats.Polymer : mats[Mathf.Min(i, mats.Length - 1)];
            mr.sharedMaterials = arr;
            return go;
        }
    }
}
