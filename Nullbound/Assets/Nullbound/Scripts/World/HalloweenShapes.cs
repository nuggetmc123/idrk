using System.Collections.Generic;
using Nullbound.Art;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// The meshes for the Halloween decorations, made in code (pure maths, so they also build outside Unity for
    /// previews). Sizes are in metres; most shapes sit on y = 0 and face +Z.
    /// </summary>
    public enum HShape { Pumpkin, PumpkinFace, Stem, Gravestone, CrossUpright, CrossBar, Ghost, Sphere, BatWing, Web, WebRound, Box, Cylinder, Cone, Bowl, Flag }

    public static class HalloweenShapes
    {

        public static MeshData Build(HShape s)
        {
            switch (s)
            {
                case HShape.Pumpkin: return Pumpkin();
                case HShape.PumpkinFace: return PumpkinFace();
                case HShape.Stem: return Stem();
                case HShape.Gravestone: return Gravestone();
                case HShape.CrossUpright: return MeshKit.Box(new Vector3(0.12f, 0.95f, 0.12f), 0.015f);
                case HShape.CrossBar: return MeshKit.Box(new Vector3(0.55f, 0.12f, 0.12f), 0.015f);
                case HShape.Ghost: return Ghost();
                case HShape.Sphere: return Sphere(10, 16);
                case HShape.BatWing: return BatWing();
                case HShape.Web: return Web(false);
                case HShape.WebRound: return Web(true);
                case HShape.Box: return MeshKit.Box(Vector3.one, 0f);
                case HShape.Cylinder: return Lathe(new[] { new Vector2(0.5f, 0f), new Vector2(0.5f, 1f) }, 10, true, true);
                case HShape.Cone: return Lathe(new[] { new Vector2(0.5f, 0f), new Vector2(0.001f, 1f) }, 12, true, false);
                case HShape.Bowl: return Bowl();
                default: return Flag();
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A triangle that faces the way the vertex normals point (works out the winding itself).</summary>
        static void Tri(MeshData d, int a, int b, int c)
        {
            Vector3 pa = d.vertices[a], pb = d.vertices[b], pc = d.vertices[c];
            Vector3 n = d.normals[a] + d.normals[b] + d.normals[c];
            if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), n) < 0f) d.Tri(a, c, b); else d.Tri(a, b, c);
        }

        static int V(MeshData d, Vector3 p, Vector3 n, Vector2 uv)
        {
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            Vector3 t = Vector3.Cross(Mathf.Abs(n.y) < 0.95f ? Vector3.up : Vector3.right, n).normalized;
            return d.Add(p, n, new Vector4(t.x, t.y, t.z, 1f), uv);
        }

        /// <summary>A grid of points P(u, v), u around (wrapping), v along; normals from the surface itself.</summary>
        static MeshData Surface(int nu, int nv, System.Func<float, float, Vector3> P, Vector3 centre, bool flipOut = false)
        {
            var d = new MeshData();
            const float e = 1e-3f;
            int[,] idx = new int[nu + 1, nv + 1];
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    float u = i / (float)nu, v = j / (float)nv;
                    Vector3 p = P(u, v);
                    Vector3 du = P(u + e, v) - P(u - e, v), dv = P(u, Mathf.Min(1f, v + e)) - P(u, Mathf.Max(0f, v - e));
                    Vector3 n = Vector3.Cross(du, dv);
                    if (n.sqrMagnitude < 1e-14f) n = p - centre;
                    if (Vector3.Dot(n, p - centre) < 0f) n = -n;
                    if (flipOut) n = -n;
                    idx[i, j] = V(d, p, n, new Vector2(u * 4f, v * 2f));
                }
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    Tri(d, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1]);
                    Tri(d, idx[i, j], idx[i + 1, j + 1], idx[i, j + 1]);
                }
            return d;
        }

        /// <summary>A shape turned on a lathe: profile (radius, height) from bottom to top, with optional end caps.</summary>
        static MeshData Lathe(Vector2[] profile, int sides, bool capBottom, bool capTop)
        {
            var d = new MeshData();
            int n = profile.Length;
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j < n; j++)
                {
                    Vector2 prev = profile[Mathf.Max(0, j - 1)], next = profile[Mathf.Min(n - 1, j + 1)];
                    Vector2 along = next - prev;
                    Vector3 normal = dir * along.y - Vector3.up * along.x;
                    V(d, dir * profile[j].x + Vector3.up * profile[j].y, normal, new Vector2(i / (float)sides, profile[j].y));
                }
            }
            for (int i = 0; i < sides; i++)
                for (int j = 0; j < n - 1; j++)
                {
                    int a = i * n + j, b = (i + 1) * n + j;
                    Tri(d, a, b, b + 1);
                    Tri(d, a, b + 1, a + 1);
                }
            void Cap(Vector2 p, float ny)
            {
                if (p.x < 0.002f) return;
                int c = V(d, Vector3.up * p.y, Vector3.up * ny, new Vector2(0.5f, 0.5f));
                int first = d.vertices.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    V(d, new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x), Vector3.up * ny, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }
                for (int i = 0; i < sides; i++) Tri(d, c, first + i, first + i + 1);
            }
            if (capBottom) Cap(profile[0], -1f);
            if (capTop) Cap(profile[n - 1], 1f);
            return d;
        }

        /// <summary>Both sides of a flat or thin shape: every triangle again, facing the other way.</summary>
        static void DoubleSide(MeshData d)
        {
            int count = d.vertices.Count, tris = d.triangles.Count;
            for (int i = 0; i < count; i++)
            {
                Vector4 t = d.tangents[i];
                d.Add(d.vertices[i], -d.normals[i], new Vector4(t.x, t.y, t.z, -t.w), d.uvs[i]);
            }
            for (int i = 0; i < tris; i += 3) d.Tri(d.triangles[i] + count, d.triangles[i + 2] + count, d.triangles[i + 1] + count);
        }

        public static MeshData Sphere(int lat, int lon) => Surface(lon, lat, (u, v) =>
        {
            float th = Mathf.Clamp01(v) * Mathf.PI, ph = u * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f;
        }, Vector3.zero);

        // ------------------------------------------------------------------ pumpkin (radius 1 m, sits on y = 0)

        public const float PumpkinHeight = 0.78f, PumpkinPole = 0.82f;

        /// <summary>A point on the pumpkin: around = 0..1 (0.25 faces +Z), up = 0 (bottom) .. 1 (top).</summary>
        public static Vector3 PumpkinPoint(float around, float up)
        {
            float th = Mathf.Clamp01(up) * Mathf.PI, ph = around * Mathf.PI * 2f;
            float rib = 0.9f + 0.1f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ph * 5f)), 0.7f); // ten ridges
            float s = Mathf.Sin(th);
            float y = -Mathf.Cos(th) * PumpkinHeight * (PumpkinPole + (1f - PumpkinPole) * s); // dimpled top and bottom
            return new Vector3(s * rib * Mathf.Cos(ph), y + PumpkinHeight * PumpkinPole, s * rib * Mathf.Sin(ph));
        }

        public static float PumpkinTop => PumpkinHeight * PumpkinPole * 2f;

        static MeshData Pumpkin() => Surface(40, 14, PumpkinPoint, new Vector3(0f, PumpkinHeight * PumpkinPole, 0f));

        /// <summary>The glowing carved face: triangle eyes, a nose and a toothy grin, on the pumpkin's +Z side.</summary>
        static MeshData PumpkinFace()
        {
            var d = new MeshData();
            Vector3 centre = new Vector3(0f, PumpkinHeight * PumpkinPole, 0f);
            // face (x, y) in -1..1 -> a point just outside the skin
            Vector3 On(float x, float y)
            {
                Vector3 p = PumpkinPoint(0.25f - x * 0.085f, 0.5f + y * 0.24f);
                return centre + (p - centre) * 1.018f;
            }
            void Poly(params Vector2[] pts)
            {
                // fan over a convex outline, subdivided so it follows the curve
                Vector2 c = Vector2.zero;
                foreach (var p in pts) c += p;
                c /= pts.Length;
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                    const int steps = 3;
                    for (int s = 0; s < steps; s++)
                    {
                        Vector2 a0 = Vector2.Lerp(a, b, s / (float)steps), a1 = Vector2.Lerp(a, b, (s + 1) / (float)steps);
                        Vector3 pc = On(c.x, c.y), p0 = On(a0.x, a0.y), p1 = On(a1.x, a1.y);
                        Tri(d, V(d, pc, pc - centre, c), V(d, p0, p0 - centre, a0), V(d, p1, p1 - centre, a1));
                    }
                }
            }
            Poly(new Vector2(-0.62f, 0.25f), new Vector2(-0.18f, 0.25f), new Vector2(-0.4f, 0.75f)); // eyes
            Poly(new Vector2(0.18f, 0.25f), new Vector2(0.62f, 0.25f), new Vector2(0.4f, 0.75f));
            Poly(new Vector2(-0.11f, -0.05f), new Vector2(0.11f, -0.05f), new Vector2(0f, 0.17f));  // nose
            // the grin: a curved band in pieces, with two teeth hanging down and one coming up
            const int parts = 7;
            for (int i = 0; i < parts; i++)
            {
                float x0 = -0.75f + 1.5f * i / parts, x1 = -0.75f + 1.5f * (i + 1) / parts;
                float Top(float x) => -0.22f - 0.25f * (1f - x * x);
                float Bottom(float x) => -0.42f - 0.38f * (1f - x * x) + 0.18f * (Mathf.Abs(x) > 0.6f ? 1f : 0f);
                float tooth = i == 1 || i == 5 ? 0.12f : 0f, under = i == 3 ? 0.1f : 0f;
                Poly(new Vector2(x0, Top(x0) - tooth), new Vector2(x1, Top(x1) - tooth), new Vector2(x1, Bottom(x1) + under), new Vector2(x0, Bottom(x0) + under));
            }
            return d;
        }

        /// <summary>A curly stem, 0.3 m tall from the pumpkin's top (y = 0 here).</summary>
        static MeshData Stem()
        {
            return Surface(8, 6, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, r = Mathf.Lerp(0.1f, 0.065f, v) * (0.9f + 0.1f * Mathf.Cos(a * 4f));
                Vector3 spine = new Vector3(0.08f * v * v, 0.3f * v, 0.02f * v);
                return spine + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }, new Vector3(0f, 0.15f, 0f));
        }

        // ------------------------------------------------------------------ graveyard

        /// <summary>A rounded headstone 0.62 m wide, 0.95 m tall, 0.14 m thick, with a slightly chipped top.</summary>
        static MeshData Gravestone()
        {
            var outline = new List<Vector2> { new Vector2(-0.31f, 0f), new Vector2(0.31f, 0f), new Vector2(0.31f, 0.64f) };
            for (int i = 1; i < 10; i++)
            {
                float a = i * Mathf.PI / 10f;
                float chip = i == 3 ? 0.03f : 0f;
                outline.Add(new Vector2(Mathf.Cos(a) * (0.31f - chip), 0.64f + Mathf.Sin(a) * (0.31f - chip)));
            }
            outline.Add(new Vector2(-0.31f, 0.64f));
            return Extrude(outline, 0.14f);
        }

        /// <summary>A convex outline (counter-clockwise in X/Y) pushed out to a slab centred on z = 0.</summary>
        static MeshData Extrude(List<Vector2> outline, float thickness)
        {
            var d = new MeshData();
            float hz = thickness * 0.5f;
            Vector2 c = Vector2.zero;
            foreach (var p in outline) c += p;
            c /= outline.Count;
            foreach (float z in new[] { -hz, hz })
            {
                Vector3 n = new Vector3(0f, 0f, Mathf.Sign(z));
                int ci = V(d, new Vector3(c.x, c.y, z), n, c);
                int first = d.vertices.Count;
                foreach (var p in outline) V(d, new Vector3(p.x, p.y, z), n, p);
                for (int i = 0; i < outline.Count; i++) Tri(d, ci, first + i, first + (i + 1) % outline.Count);
            }
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector2 e = b - a;
                Vector3 n = new Vector3(e.y, -e.x, 0f);
                if (Vector2.Dot(new Vector2(n.x, n.y), (a + b) * 0.5f - c) < 0f) n = -n;
                int v0 = V(d, new Vector3(a.x, a.y, -hz), n, new Vector2(0f, 0f)), v1 = V(d, new Vector3(b.x, b.y, -hz), n, new Vector2(1f, 0f));
                int v2 = V(d, new Vector3(b.x, b.y, hz), n, new Vector2(1f, 1f)), v3 = V(d, new Vector3(a.x, a.y, hz), n, new Vector2(0f, 1f));
                Tri(d, v0, v1, v2);
                Tri(d, v0, v2, v3);
            }
            return d;
        }

        // ------------------------------------------------------------------ ghost (1.35 m tall, hem at y = 0)

        static float GhostRadius(float v) // v = 0 at the hem .. 1 at the top of the head
        {
            float y = v * 1.35f;
            if (y > 0.98f) { float k = (y - 0.98f) / 0.37f; return 0.33f * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)); }
            return Mathf.Lerp(0.44f, 0.33f, Mathf.SmoothStep(0f, 1f, y / 0.98f));
        }

        static MeshData Ghost()
        {
            System.Func<float, float, Vector3> P = (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float hem = 0.07f * Mathf.Sin(a * 7f) * (1f - Mathf.Clamp01(v * 6f));
                float r = GhostRadius(v);
                return new Vector3(Mathf.Cos(a) * r, v * 1.35f + hem, Mathf.Sin(a) * r);
            };
            var d = Surface(28, 16, P, new Vector3(0f, 0.75f, 0f));
            DoubleSide(d); // you can see up into the sheet
            return d;
        }

        // ------------------------------------------------------------------ bat wing (0.34 m, root at x = 0, flat in X/Z)

        static MeshData BatWing()
        {
            var d = new MeshData();
            Vector2[] edge =
            {
                new Vector2(0f, 0.05f), new Vector2(0.12f, 0.07f), new Vector2(0.24f, 0.05f), new Vector2(0.34f, 0.0f),   // leading edge
                new Vector2(0.27f, -0.05f), new Vector2(0.23f, -0.12f), new Vector2(0.17f, -0.07f), new Vector2(0.11f, -0.14f),
                new Vector2(0.06f, -0.08f), new Vector2(0f, -0.1f),                                                        // scalloped trailing edge
            };
            int root = V(d, new Vector3(0.02f, 0f, -0.02f), Vector3.up, Vector2.zero);
            int first = d.vertices.Count;
            foreach (var p in edge) V(d, new Vector3(p.x, 0.012f * Mathf.Sin(p.x * 12f), p.y), Vector3.up, p);
            for (int i = 0; i < edge.Length - 1; i++) Tri(d, root, first + i, first + i + 1);
            DoubleSide(d);
            return d;
        }

        // ------------------------------------------------------------------ cobweb

        /// <summary>
        /// A cobweb of thin threads in the X/Y plane: a corner web fans out over a quarter circle from the origin
        /// (+X to +Y, 1 m), a round web is a full circle of radius 0.5 m centred on the origin.
        /// </summary>
        static MeshData Web(bool round)
        {
            var d = new MeshData();
            const float w = 0.006f;
            void Thread(Vector2 a, Vector2 b)
            {
                Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x) * w;
                Vector3 P(Vector2 q) => new Vector3(q.x, q.y, 0f);
                int i0 = V(d, P(a - side), Vector3.back, Vector2.zero), i1 = V(d, P(a + side), Vector3.back, Vector2.right);
                int i2 = V(d, P(b + side), Vector3.back, Vector2.one), i3 = V(d, P(b - side), Vector3.back, Vector2.up);
                Tri(d, i0, i1, i2);
                Tri(d, i0, i2, i3);
            }
            int spokes = round ? 12 : 7;
            float span = round ? Mathf.PI * 2f : Mathf.PI * 0.5f, length = round ? 0.5f : 1f;
            Vector2 Spoke(int i, float r)
            {
                float a = round ? i * span / spokes : i * span / (spokes - 1);
                float wobble = 1f + 0.06f * Mathf.Sin(i * 2.3f);
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * wobble;
            }
            for (int i = 0; i < spokes; i++) Thread(round ? Vector2.zero : Spoke(i, 0.02f), Spoke(i, length));
            int rings = round ? 6 : 6;
            for (int k = 1; k <= rings; k++)
            {
                int segments = round ? spokes : spokes - 1;
                for (int i = 0; i < segments; i++)
                {
                    float r0 = length * (k + i * 0.02f) / (rings + 0.6f), r1 = length * (k + (i + 1) * 0.02f) / (rings + 0.6f);
                    // threads sag a little between spokes
                    Vector2 a = Spoke(i, r0), b = Spoke((i + 1) % spokes, r1), mid = (a + b) * 0.5f * 0.92f;
                    Thread(a, mid);
                    Thread(mid, b);
                }
            }
            DoubleSide(d);
            return d;
        }

        // ------------------------------------------------------------------ cauldron and bunting

        /// <summary>A cauldron bowl 0.9 m across, 0.6 m tall (open at the top: the brew goes in it).</summary>
        static MeshData Bowl()
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = (-90f + i * 13f) * Mathf.Deg2Rad; // round belly, narrowing towards the neck
                profile.Add(new Vector2(Mathf.Cos(a) * 0.45f + 0.001f, 0.32f + Mathf.Sin(a) * 0.32f));
            }
            profile.Add(new Vector2(0.33f, 0.56f));
            profile.Add(new Vector2(0.38f, 0.6f)); // the rim
            var d = Lathe(profile.ToArray(), 16, false, false);
            DoubleSide(d); // you look into it
            return d;
        }

        /// <summary>A pennant for bunting: a triangle 0.24 m wide hanging 0.3 m down from y = 0.</summary>
        static MeshData Flag()
        {
            var d = new MeshData();
            int a = V(d, new Vector3(-0.12f, 0f, 0f), Vector3.back, Vector2.zero), b = V(d, new Vector3(0.12f, 0f, 0f), Vector3.back, Vector2.right);
            int c = V(d, new Vector3(0f, -0.3f, 0f), Vector3.back, new Vector2(0.5f, 1f));
            Tri(d, a, b, c);
            DoubleSide(d);
            return d;
        }
    }
}
