using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KingdomChaos
{
    // A tiny immediate-mode vector painter, modelled on the HTML canvas API the browser
    // version was drawn with: a transform stack, filled shapes with optional outlines, lines
    // and arcs. Everything a frame draws is appended to one mesh in canvas-style pixel
    // coordinates (960 x 540, y pointing down), and the camera is set up to frame that area.
    public class KCPainter
    {
        struct Mat
        {
            public float a, b, c, d, e, f;   // x' = a*x + c*y + e, y' = b*x + d*y + f
        }

        // declared before Outline: static fields initialise in order, and Outline needs the cache
        static readonly Dictionary<string, Color> hexCache = new Dictionary<string, Color>();
        static readonly Mat Identity = new Mat { a = 1, d = 1 };
        public static readonly Color Outline = Hex("#1a1024");

        Mat m = Identity;
        readonly Stack<Mat> stack = new Stack<Mat>();
        readonly Stack<float> alphaStack = new Stack<float>();
        readonly List<Vector3> verts = new List<Vector3>(32768);
        readonly List<Color32> cols = new List<Color32>(32768);
        readonly List<int> tris = new List<int>(65536);
        readonly List<Vector2> pts = new List<Vector2>(256);
        readonly List<Vector2> pts2 = new List<Vector2>(256);
        readonly List<int> ear = new List<int>(64);

        public float Alpha = 1f;
        public bool Flash;           // draw fills white (hit flash), like the canvas version's C()
        public float LineWidth = 3f;

        public readonly Mesh Mesh;

        public KCPainter()
        {
            Mesh = new Mesh { name = "Kingdom Chaos Frame" };
            Mesh.indexFormat = IndexFormat.UInt32;
            Mesh.MarkDynamic();
        }

        public void Begin()
        {
            verts.Clear(); cols.Clear(); tris.Clear();
            stack.Clear(); alphaStack.Clear();
            m = Identity; Alpha = 1f; Flash = false;
        }

        public void End()
        {
            Mesh.Clear();
            Mesh.SetVertices(verts);
            Mesh.SetColors(cols);
            Mesh.SetTriangles(tris, 0, false);
            Mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 10f));
        }

        /* ---------------- transform stack ---------------- */

        public void Save() { stack.Push(m); alphaStack.Push(Alpha); }
        public void Restore()
        {
            if (stack.Count > 0) m = stack.Pop();
            if (alphaStack.Count > 0) Alpha = alphaStack.Pop();
        }
        public void Translate(float x, float y) { m.e += m.a * x + m.c * y; m.f += m.b * x + m.d * y; }
        public void Scale(float sx, float sy) { m.a *= sx; m.b *= sx; m.c *= sy; m.d *= sy; }
        public void Rotate(float r)
        {
            float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            float na = m.a * cs + m.c * sn, nb = m.b * cs + m.d * sn;
            float nc = -m.a * sn + m.c * cs, nd = -m.b * sn + m.d * cs;
            m.a = na; m.b = nb; m.c = nc; m.d = nd;
        }
        float ScaleFactor => Mathf.Sqrt(Mathf.Abs(m.a * m.d - m.b * m.c));

        /* ---------------- low level ---------------- */

        Color32 Col(Color c, bool fill)
        {
            if (fill && Flash) c = Color.white;
            c.a *= Alpha;
            return c;
        }

        int V(float x, float y, Color32 c)
        {
            verts.Add(new Vector3(m.a * x + m.c * y + m.e, -(m.b * x + m.d * y + m.f), 0));
            cols.Add(c);
            return verts.Count - 1;
        }

        void FillFan(List<Vector2> p, Color color)
        {
            if (p.Count < 3) return;
            var c = Col(color, true);
            int b = verts.Count;
            foreach (var q in p) V(q.x, q.y, c);
            for (int i = 1; i < p.Count - 1; i++) { tris.Add(b); tris.Add(b + i); tris.Add(b + i + 1); }
        }

        // Ear clipping, for the handful of small concave polygons (crowns, ears, thorns).
        void FillConcave(List<Vector2> p, Color color)
        {
            int n = p.Count;
            if (n < 3) return;
            var c = Col(color, true);
            int b = verts.Count;
            foreach (var q in p) V(q.x, q.y, c);
            float area = 0;
            for (int i = 0; i < n; i++) { var p0 = p[i]; var p1 = p[(i + 1) % n]; area += p0.x * p1.y - p1.x * p0.y; }
            bool ccw = area > 0;
            ear.Clear();
            for (int i = 0; i < n; i++) ear.Add(i);
            int guard = 0;
            while (ear.Count > 3 && guard++ < 512)
            {
                bool clipped = false;
                for (int i = 0; i < ear.Count; i++)
                {
                    int i0 = ear[(i + ear.Count - 1) % ear.Count], i1 = ear[i], i2 = ear[(i + 1) % ear.Count];
                    Vector2 a = p[i0], bb = p[i1], cc = p[i2];
                    float cross = (bb.x - a.x) * (cc.y - a.y) - (bb.y - a.y) * (cc.x - a.x);
                    if (ccw ? cross <= 0 : cross >= 0) continue;
                    bool inside = false;
                    for (int k = 0; k < ear.Count; k++)
                    {
                        int q = ear[k];
                        if (q == i0 || q == i1 || q == i2) continue;
                        if (InTri(p[q], a, bb, cc)) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add(b + i0); tris.Add(b + i1); tris.Add(b + i2);
                    ear.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            for (int i = 1; i < ear.Count - 1; i++) { tris.Add(b + ear[0]); tris.Add(b + ear[i]); tris.Add(b + ear[i + 1]); }
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // Thick polyline as one quad per segment, each pushed out by half the width at both ends
        // so corners are covered.
        public void Stroke(List<Vector2> p, bool closed, float width, Color color)
        {
            var c = Col(color, false);
            int n = p.Count, segs = closed ? n : n - 1;
            float hw = width / 2;
            for (int i = 0; i < segs; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % n];
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;
                dir /= len;
                Vector2 nrm = new Vector2(-dir.y, dir.x) * hw;
                Vector2 ext = dir * hw * .9f;
                Vector2 a2 = a - ext, b2 = b + ext;
                int k = verts.Count;
                V(a2.x + nrm.x, a2.y + nrm.y, c);
                V(b2.x + nrm.x, b2.y + nrm.y, c);
                V(b2.x - nrm.x, b2.y - nrm.y, c);
                V(a2.x - nrm.x, a2.y - nrm.y, c);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
            }
        }

        int Segs(float r) => Mathf.Clamp((int)(r * ScaleFactor * .5f), 10, 48);

        void EllipsePts(List<Vector2> p, float x, float y, float rx, float ry, float rot, int n)
        {
            p.Clear();
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2;
                float ex = Mathf.Cos(t) * rx, ey = Mathf.Sin(t) * ry;
                p.Add(new Vector2(x + ex * cr - ey * sr, y + ex * sr + ey * cr));
            }
        }

        /* ---------------- shapes ---------------- */

        public void Circle(float x, float y, float r, Color fill, bool stroke = true) => Ellipse(x, y, r, r, fill, stroke);

        public void Ellipse(float x, float y, float rx, float ry, Color fill, bool stroke = true, float rot = 0)
        {
            rx = Mathf.Max(.1f, rx); ry = Mathf.Max(.1f, ry);
            EllipsePts(pts, x, y, rx, ry, rot, Segs(Mathf.Max(rx, ry)));
            FillFan(pts, fill);
            if (stroke) Stroke(pts, true, LineWidth, Outline);
        }

        public void EllipseStroke(float x, float y, float rx, float ry, float width, Color color)
        {
            EllipsePts(pts, x, y, rx, ry, 0, Segs(Mathf.Max(rx, ry)));
            Stroke(pts, true, width, color);
        }

        public void RRect(float x, float y, float w, float h, float r, Color fill, bool stroke = true)
        {
            r = Mathf.Min(r, Mathf.Min(Mathf.Abs(w), Mathf.Abs(h)) / 2);
            pts.Clear();
            const int k = 4;
            void Corner(float cx, float cy, float a0)
            {
                for (int i = 0; i <= k; i++)
                {
                    float a = a0 + i / (float)k * Mathf.PI / 2;
                    pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            Corner(x + w - r, y + r, -Mathf.PI / 2);
            Corner(x + w - r, y + h - r, 0);
            Corner(x + r, y + h - r, Mathf.PI / 2);
            Corner(x + r, y + r, Mathf.PI);
            FillFan(pts, fill);
            if (stroke) Stroke(pts, true, LineWidth, Outline);
        }

        public void Rect(float x, float y, float w, float h, Color fill)
        {
            var c = Col(fill, true);
            int k = verts.Count;
            V(x, y, c); V(x + w, y, c); V(x + w, y + h, c); V(x, y + h, c);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
        }

        public void GradientRect(float x, float y, float w, float h, Color top, Color bottom)
        {
            var ct = Col(top, true); var cb = Col(bottom, true);
            int k = verts.Count;
            V(x, y, ct); V(x + w, y, ct); V(x + w, y + h, cb); V(x, y + h, cb);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
        }

        public void Quad(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy, Color fill)
        {
            var c = Col(fill, true);
            int k = verts.Count;
            V(ax, ay, c); V(bx, by, c); V(cx, cy, c); V(dx, dy, c);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
        }

        public void Poly(float[] p, Color fill, bool stroke = true)
        {
            pts.Clear();
            for (int i = 0; i + 1 < p.Length; i += 2) pts.Add(new Vector2(p[i], p[i + 1]));
            if (pts.Count == 3) FillFan(pts, fill); else FillConcave(pts, fill);
            if (stroke) Stroke(pts, true, LineWidth, Outline);
        }

        // A convex closed path built by the caller (e.g. bezier blobs).
        public void Shape(List<Vector2> p, Color fill, bool stroke = true)
        {
            FillFan(p, fill);
            if (stroke) Stroke(p, true, LineWidth, Outline);
        }

        public void Line(float x1, float y1, float x2, float y2, float width, Color color)
        {
            pts2.Clear();
            pts2.Add(new Vector2(x1, y1)); pts2.Add(new Vector2(x2, y2));
            Stroke(pts2, false, width, color);
        }

        public void Polyline(float[] p, float width, Color color)
        {
            pts2.Clear();
            for (int i = 0; i + 1 < p.Length; i += 2) pts2.Add(new Vector2(p[i], p[i + 1]));
            Stroke(pts2, false, width, color);
        }

        public void Arc(float cx, float cy, float r, float a0, float a1, float width, Color color)
        {
            pts2.Clear();
            int n = Mathf.Max(4, (int)(Mathf.Abs(a1 - a0) / (Mathf.PI * 2) * Segs(r)) + 2);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n);
                pts2.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            Stroke(pts2, false, width, color);
        }

        public void Pie(float cx, float cy, float r, float a0, float a1, Color fill)
        {
            pts.Clear();
            pts.Add(new Vector2(cx, cy));
            int n = Mathf.Max(3, (int)(Mathf.Abs(a1 - a0) / (Mathf.PI * 2) * 40) + 2);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n);
                pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            FillFan(pts, fill);
        }

        /* ---------------- path helpers ---------------- */

        public static void AddBezier(List<Vector2> p, Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                p.Add(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p1);
            }
        }

        public static void AddQuadratic(List<Vector2> p, Vector2 p0, Vector2 c, Vector2 p1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                p.Add(u * u * p0 + 2 * u * t * c + t * t * p1);
            }
        }

        /* ---------------- colors ---------------- */

        public static Color Hex(string s)
        {
            if (hexCache.TryGetValue(s, out var c)) return c;
            if (!ColorUtility.TryParseHtmlString(s, out c)) c = Color.magenta;
            hexCache[s] = c;
            return c;
        }
    }
}
