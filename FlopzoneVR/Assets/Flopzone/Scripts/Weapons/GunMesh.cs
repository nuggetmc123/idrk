using System;
using System.Collections.Generic;

namespace Flopzone
{
    /// <summary>The materials a gun is made of (GunModel turns these into real materials).</summary>
    public enum GM { Metal, Dark, Polymer, Wood, DarkWood, Rubber, Tan, Olive, Green, Chrome, Brass, Glass, Port, Accent, Red, Glow, Bronze }

    /// <summary>A small vector, so gun shapes can be built (and previewed) without Unity.</summary>
    public struct P3
    {
        public float x, y, z;
        public P3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static P3 operator +(P3 a, P3 b) => new P3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static P3 operator -(P3 a, P3 b) => new P3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static P3 operator *(P3 a, float k) => new P3(a.x * k, a.y * k, a.z * k);
        public static P3 operator -(P3 a) => new P3(-a.x, -a.y, -a.z);
        public float Len => (float)Math.Sqrt(x * x + y * y + z * z);
        public P3 Norm { get { float l = Len; return l < 1e-9f ? new P3(0, 0, 1) : this * (1f / l); } }
        public static float Dot(P3 a, P3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static P3 Cross(P3 a, P3 b) => new P3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public override string ToString() => $"({x:0.###},{y:0.###},{z:0.###})";
    }

    /// <summary>
    /// Builds gun meshes from real shapes instead of stacked boxes: side profiles
    /// (the silhouette of a receiver, stock or grip) extruded to their width with
    /// bevelled edges, round barrels and tubes, and smooth balls. Pure math, so the
    /// same code makes the in-game guns and the offline preview pictures.
    /// </summary>
    public class GunMesh
    {
        public class Part
        {
            public readonly List<P3> v = new List<P3>(), n = new List<P3>();
            public readonly List<float> u = new List<float>();   // u, v pairs (metres, for tiling textures)
            public readonly List<int> t = new List<int>();
        }

        /// <summary>Meshes by (group, material). Group "" is the gun itself; others ("pump") move on their own.</summary>
        public readonly Dictionary<(string, GM), Part> parts = new Dictionary<(string, GM), Part>();
        /// <summary>Named points: muzzle, magwell, scopeEye, scopeFront, foregrip, eject, ammo, pump. (Angle = tilt about X, degrees.)</summary>
        public readonly Dictionary<string, (P3 pos, float angle)> anchors = new Dictionary<string, (P3, float)>();
        public string group = "";

        // an optional transform for what's added next: rotate about X (pitch) around a pivot, then move
        float rotX, rotY, rotZ;
        P3 pivot, offset;

        public void Anchor(string name, float y, float z, float angle = 0f, float x = 0f) => anchors[name] = (new P3(x, y, z), angle);

        /// <summary>Everything added inside <paramref name="build"/> is turned (degrees) about a pivot and moved.</summary>
        public void With(P3 pivot, float rx, float ry, float rz, P3 move, Action build)
        {
            var (a, b, c, d, e) = (rotX, rotY, rotZ, this.pivot, this.offset);
            rotX = rx; rotY = ry; rotZ = rz; this.pivot = pivot; this.offset = move;
            build();
            (rotX, rotY, rotZ, this.pivot, this.offset) = (a, b, c, d, e);
        }

        P3 Tf(P3 p)
        {
            if (rotX == 0 && rotY == 0 && rotZ == 0) return p + offset;
            return Rot(p - pivot) + pivot + offset;
        }

        P3 Rot(P3 p)
        {
            if (rotZ != 0) { float c = Cos(rotZ), s = Sin(rotZ); p = new P3(p.x * c - p.y * s, p.x * s + p.y * c, p.z); }
            if (rotX != 0) { float c = Cos(rotX), s = Sin(rotX); p = new P3(p.x, p.y * c - p.z * s, p.y * s + p.z * c); }
            if (rotY != 0) { float c = Cos(rotY), s = Sin(rotY); p = new P3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c); }
            return p;
        }

        static float Cos(float deg) => (float)Math.Cos(deg * Math.PI / 180);
        static float Sin(float deg) => (float)Math.Sin(deg * Math.PI / 180);

        Part Get(GM m)
        {
            if (!parts.TryGetValue((group, m), out var p)) parts[(group, m)] = p = new Part();
            return p;
        }

        /// <summary>A flat polygon (already in place), facing <paramref name="normal"/>.</summary>
        void Poly(GM m, List<P3> pts, P3 normal, List<int> tris = null)
        {
            var p = Get(m);
            int b = p.v.Count;
            var nn = Rot(normal).Norm;
            foreach (var q in pts)
            {
                var w = Tf(q);
                p.v.Add(w); p.n.Add(nn);
                AddUV(p, w, nn);
            }
            if (tris != null) foreach (int i in tris) p.t.Add(b + i);
            else for (int i = 1; i + 1 < pts.Count; i++) { p.t.Add(b); p.t.Add(b + i); p.t.Add(b + i + 1); }
        }

        static void AddUV(Part p, P3 w, P3 n)
        {
            float ax = Math.Abs(n.x), ay = Math.Abs(n.y), az = Math.Abs(n.z);
            if (ax >= ay && ax >= az) { p.u.Add(w.z); p.u.Add(w.y); }
            else if (ay >= az) { p.u.Add(w.x); p.u.Add(w.z); }
            else { p.u.Add(w.x); p.u.Add(w.y); }
        }

        /// <summary>A quad a-b-c-d (counter-clockwise seen from outside), normal from its corners.</summary>
        void Quad(GM m, P3 a, P3 b, P3 c, P3 d)
        {
            var n = P3.Cross(b - a, d - a);
            if (n.Len < 1e-12f) n = P3.Cross(c - b, a - b);
            var p = Get(m);
            int i = p.v.Count;
            var nn = Rot(n).Norm;
            foreach (var q in new[] { a, b, c, d }) { var w = Tf(q); p.v.Add(w); p.n.Add(nn); AddUV(p, w, nn); }
            p.t.Add(i); p.t.Add(i + 1); p.t.Add(i + 2);
            p.t.Add(i); p.t.Add(i + 2); p.t.Add(i + 3);
        }

        /* ---------------- extruded side profiles ---------------- */

        /// <summary>
        /// A side profile (pairs of z, y: the outline seen from the gun's right side) given
        /// thickness <paramref name="w"/> centred on x, with chamfered edges of size <paramref name="bevel"/>.
        /// </summary>
        public void Profile(GM m, float x, float w, float bevel, params float[] zy)
        {
            var pts = new List<(float z, float y)>();
            for (int i = 0; i + 1 < zy.Length; i += 2) pts.Add((zy[i], zy[i + 1]));
            if (pts.Count < 3) return;
            // make it counter-clockwise in (z, y)
            float area = 0;
            for (int i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; area += a.z * b.y - b.z * a.y; }
            if (area < 0) pts.Reverse();
            bevel = Math.Min(bevel, w * 0.45f);
            var inner = Inset(pts, bevel);
            float xo = w * 0.5f - bevel, xi = w * 0.5f;
            int nPts = pts.Count;
            var tris = Triangulate(inner);
            // caps
            var right = new List<P3>(); var left = new List<P3>();
            foreach (var p in inner) { right.Add(new P3(x + xi, p.y, p.z)); left.Add(new P3(x - xi, p.y, p.z)); }
            Poly(m, right, new P3(1, 0, 0), Flip(tris));
            Poly(m, left, new P3(-1, 0, 0), tris);
            for (int i = 0; i < nPts; i++)
            {
                int j = (i + 1) % nPts;
                var a = pts[i]; var b = pts[j]; var ia = inner[i]; var ib = inner[j];
                // the wall all round, between the two chamfers
                // (corner order chosen so each face points outward for a counter-clockwise outline)
                Quad(m, new P3(x + xo, a.y, a.z), new P3(x + xo, b.y, b.z), new P3(x - xo, b.y, b.z), new P3(x - xo, a.y, a.z));
                if (bevel > 0)
                {
                    Quad(m, new P3(x + xi, ia.y, ia.z), new P3(x + xi, ib.y, ib.z), new P3(x + xo, b.y, b.z), new P3(x + xo, a.y, a.z));
                    Quad(m, new P3(x - xo, a.y, a.z), new P3(x - xo, b.y, b.z), new P3(x - xi, ib.y, ib.z), new P3(x - xi, ia.y, ia.z));
                }
            }
        }

        static List<int> Flip(List<int> t)
        {
            var r = new List<int>(t.Count);
            for (int i = 0; i < t.Count; i += 3) { r.Add(t[i]); r.Add(t[i + 2]); r.Add(t[i + 1]); }
            return r;
        }

        /// <summary>Move every edge of a counter-clockwise polygon inward by d (mitred, clamped at sharp corners).</summary>
        static List<(float z, float y)> Inset(List<(float z, float y)> p, float d)
        {
            var r = new List<(float z, float y)>(p.Count);
            if (d <= 0) { r.AddRange(p); return r; }
            int n = p.Count;
            for (int i = 0; i < n; i++)
            {
                var a = p[(i + n - 1) % n]; var b = p[i]; var c = p[(i + 1) % n];
                // inward normals of the two edges (left of the direction of travel for CCW)
                var (n1z, n1y) = LeftNormal(a, b);
                var (n2z, n2y) = LeftNormal(b, c);
                float mz = n1z + n2z, my = n1y + n2y;
                float ml = (float)Math.Sqrt(mz * mz + my * my);
                if (ml < 1e-6f) { mz = n1z; my = n1y; ml = 1; }
                mz /= ml; my /= ml;
                float cos = mz * n1z + my * n1y;
                float k = d / Math.Max(0.35f, cos);
                r.Add((b.z + mz * k, b.y + my * k));
            }
            return r;
        }

        static (float, float) LeftNormal((float z, float y) a, (float z, float y) b)
        {
            float dz = b.z - a.z, dy = b.y - a.y;
            float l = (float)Math.Sqrt(dz * dz + dy * dy);
            if (l < 1e-9f) return (0, 0);
            return (-dy / l, dz / l);
        }

        /// <summary>Ear-clipping triangulation of a simple counter-clockwise polygon.</summary>
        static List<int> Triangulate(List<(float z, float y)> p)
        {
            var idx = new List<int>();
            for (int i = 0; i < p.Count; i++) idx.Add(i);
            var tris = new List<int>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int i0 = idx[(k + idx.Count - 1) % idx.Count], i1 = idx[k], i2 = idx[(k + 1) % idx.Count];
                    var a = p[i0]; var b = p[i1]; var c = p[i2];
                    float cross = (b.z - a.z) * (c.y - a.y) - (b.y - a.y) * (c.z - a.z);
                    if (cross <= 1e-10f) continue;   // reflex corner
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (InTri(p[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped) break;   // a degenerate outline: fan the rest
            }
            for (int i = 1; i + 1 < idx.Count; i++) { tris.Add(idx[0]); tris.Add(idx[i]); tris.Add(idx[i + 1]); }
            return tris;
        }

        static bool InTri((float z, float y) p, (float z, float y) a, (float z, float y) b, (float z, float y) c)
        {
            float d1 = (p.z - b.z) * (a.y - b.y) - (a.z - b.z) * (p.y - b.y);
            float d2 = (p.z - c.z) * (b.y - c.y) - (b.z - c.z) * (p.y - c.y);
            float d3 = (p.z - a.z) * (c.y - a.y) - (c.z - a.z) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>A bevelled box (centre, size).</summary>
        public void Box(GM m, float x, float y, float z, float sx, float sy, float sz, float bevel = 0.0015f)
        {
            float hz = sz / 2, hy = sy / 2;
            Profile(m, x, sx, bevel, z - hz, y - hy, z + hz, y - hy, z + hz, y + hy, z - hz, y + hy);
        }

        /* ---------------- round things ---------------- */

        /// <summary>A tube from a to b, radius r0 at a and r1 at b, smooth sides, optional end caps.</summary>
        public void Tube(GM m, P3 a, P3 b, float r0, float r1, int seg = 16, bool capA = true, bool capB = true)
        {
            var axis = (b - a).Norm;
            var side = Math.Abs(axis.y) < 0.9f ? P3.Cross(axis, new P3(0, 1, 0)).Norm : P3.Cross(axis, new P3(1, 0, 0)).Norm;
            var up = P3.Cross(side, axis).Norm;
            var p = Get(m);
            int b0 = p.v.Count;
            float slope = (r0 - r1) / Math.Max(1e-5f, (b - a).Len);
            for (int i = 0; i <= seg; i++)
            {
                float ang = (float)(i * 2 * Math.PI / seg);
                var dir = side * (float)Math.Cos(ang) + up * (float)Math.Sin(ang);
                var nrm = Rot((dir + axis * slope).Norm).Norm;
                var va = Tf(a + dir * r0); var vb = Tf(b + dir * r1);
                p.v.Add(va); p.n.Add(nrm); p.u.Add(i / (float)seg * 6.283f * Math.Max(r0, r1)); p.u.Add(0);
                p.v.Add(vb); p.n.Add(nrm); p.u.Add(i / (float)seg * 6.283f * Math.Max(r0, r1)); p.u.Add((b - a).Len);
            }
            for (int i = 0; i < seg; i++)
            {
                int k = b0 + i * 2;
                p.t.Add(k); p.t.Add(k + 1); p.t.Add(k + 2);
                p.t.Add(k + 1); p.t.Add(k + 3); p.t.Add(k + 2);
            }
            if (capA && r0 > 0) Disc(m, a, -axis, r0, seg, side, up);
            if (capB && r1 > 0) Disc(m, b, axis, r1, seg, side, up);
        }

        void Disc(GM m, P3 c, P3 normal, float r, int seg, P3 side, P3 up)
        {
            var pts = new List<P3>();
            bool flip = P3.Dot(P3.Cross(side, up), normal) < 0;
            for (int i = 0; i < seg; i++)
            {
                int k = flip ? seg - i : i;
                float ang = (float)(k * 2 * Math.PI / seg);
                pts.Add(c + side * (r * (float)Math.Cos(ang)) + up * (r * (float)Math.Sin(ang)));
            }
            Poly(m, pts, normal);
        }

        /// <summary>A tube along Z (the barrel direction).</summary>
        public void TubeZ(GM m, float x, float y, float z0, float z1, float r0, float r1 = -1, int seg = 16, bool caps = true)
            => Tube(m, new P3(x, y, z0), new P3(x, y, z1), r0, r1 < 0 ? r0 : r1, seg, caps, caps);

        /// <summary>A hollow muzzle end: a dark disc set into the end of a tube.</summary>
        public void Bore(float y, float z, float r, float x = 0)
        {
            Tube(GM.Port, new P3(x, y, z - 0.004f), new P3(x, y, z + 0.0004f), r, r, 12, false, true);
        }

        /// <summary>An ellipsoid.</summary>
        public void Ball(GM m, float x, float y, float z, float rx, float ry, float rz, int seg = 12)
        {
            var p = Get(m);
            int b0 = p.v.Count;
            int rings = Math.Max(4, seg / 2);
            for (int r = 0; r <= rings; r++)
            {
                float lat = (float)(Math.PI * r / rings - Math.PI / 2);
                for (int s = 0; s <= seg; s++)
                {
                    float lon = (float)(2 * Math.PI * s / seg);
                    var d = new P3((float)(Math.Cos(lat) * Math.Cos(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Sin(lon)));
                    var w = Tf(new P3(x + d.x * rx, y + d.y * ry, z + d.z * rz));
                    p.v.Add(w);
                    p.n.Add(Rot(new P3(d.x / rx, d.y / ry, d.z / rz)).Norm);
                    p.u.Add(s / (float)seg * 6.283f * rx); p.u.Add(r / (float)rings * 3.14f * ry);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = b0 + r * (seg + 1) + s, b = a + seg + 1;
                    p.t.Add(a); p.t.Add(b); p.t.Add(a + 1);
                    p.t.Add(a + 1); p.t.Add(b); p.t.Add(b + 1);
                }
        }

        /// <summary>Total triangles (for keeping guns cheap).</summary>
        public int Triangles { get { int n = 0; foreach (var p in parts.Values) n += p.t.Count / 3; return n; } }

        /// <summary>Wavefront OBJ, one object per material (for the offline preview).</summary>
        public string ToObj()
        {
            var sb = new System.Text.StringBuilder();
            int baseIdx = 1;
            foreach (var kv in parts)
            {
                sb.Append("o ").Append(kv.Key.Item1.Length > 0 ? kv.Key.Item1 + "_" : "").Append(kv.Key.Item2).Append('\n');
                sb.Append("usemtl ").Append(kv.Key.Item2).Append('\n');
                var p = kv.Value;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                foreach (var q in p.v) sb.Append(string.Format(ci, "v {0:0.#####} {1:0.#####} {2:0.#####}\n", q.x, q.y, q.z));
                foreach (var q in p.n) sb.Append(string.Format(ci, "vn {0:0.####} {1:0.####} {2:0.####}\n", q.x, q.y, q.z));
                for (int i = 0; i < p.t.Count; i += 3)
                {
                    int a = p.t[i] + baseIdx, b = p.t[i + 1] + baseIdx, c = p.t[i + 2] + baseIdx;
                    sb.Append($"f {a}//{a} {b}//{b} {c}//{c}\n");
                }
                baseIdx += p.v.Count;
            }
            return sb.ToString();
        }
    }
}
