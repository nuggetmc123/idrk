using System.Collections.Generic;
using UnityEngine;

namespace Nullbound.Art
{
    /// <summary>Raw mesh data (pure lists, no Unity objects), turned into a Mesh by whoever needs one.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector4> tangents = new List<Vector4>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();

        public int Add(Vector3 v, Vector3 n, Vector4 t, Vector2 uv)
        {
            vertices.Add(v);
            normals.Add(n);
            tangents.Add(t);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        /// <summary>A front face as seen from outside: a, b, c clockwise (Unity's winding).</summary>
        public void Tri(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }

    /// <summary>
    /// Generated meshes for world building, all with UVs in METRES (1 UV unit = 1 m, so a surface's tiling is
    /// 1 / TexSet.metres) and proper tangents for normal maps:
    /// - Box: a unit cube (like Unity's) with small rounded bevels, so edges catch the light. The object's scale stays
    ///   its real size; the bevel and UVs are worked out for that size.
    /// - Cylinder: like Unity's (diameter 1, height 2) with smoother sides and rounded rims.
    /// - Quad: like Unity's (1x1, facing -Z).
    /// - Rock: a chunky, noise-shaped, faceted boulder that exactly fills a unit box.
    /// Everything is built in metres and then converted to the unit shape, with normals corrected for the scale the
    /// object will have, so bevels stay round and lighting stays right on stretched boxes.
    /// Pure maths (only Vector/Mathf), so it also runs at runtime and outside Unity.
    /// </summary>
    public static class MeshKit
    {
        static Vector3 Mul(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        static float Comp(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;

        /// <summary>
        /// Adds a vertex given in metre space. unitScale = how the unit mesh is scaled to metres (object scale).
        /// Normal and tangent are converted so they come out right after that scale is applied.
        /// </summary>
        static int Vert(MeshData d, Vector3 posMetres, Vector3 normal, Vector3 tangentDir, Vector3 bitangentDir, Vector2 uv, Vector3 unitScale)
        {
            Vector3 n = normal.normalized;
            Vector3 t = (tangentDir - n * Vector3.Dot(tangentDir, n)).normalized;
            if (t.sqrMagnitude < 0.5f) t = Perpendicular(n);
            float w = Vector3.Dot(Vector3.Cross(n, t), bitangentDir) >= 0f ? 1f : -1f;
            Vector3 nm = Mul(n, unitScale).normalized;
            Vector3 tm = Div(t, unitScale).normalized;
            return d.Add(Div(posMetres, unitScale), nm, new Vector4(tm.x, tm.y, tm.z, w), uv);
        }

        static Vector3 Perpendicular(Vector3 n)
        {
            Vector3 a = Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.Cross(a, n).normalized;
        }

        // ------------------------------------------------------------------ box

        // The six faces as (outward normal, right, up) seen from outside, so textures are never mirrored. Walls have
        // "up" = +Y; the top is read with +Z up, the bottom from below.
        static readonly Vector3[][] boxFaces =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0) },
            new[] { new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0) },
            new[] { new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0) },
            new[] { new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0) },
            new[] { new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) },
            new[] { new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1) },
        };

        static int Axis(Vector3 v) => Mathf.Abs(v.x) > 0.5f ? 0 : Mathf.Abs(v.y) > 0.5f ? 1 : 2;

        /// <summary>Largest bevel that suits a box: at most 20% of its smallest side.</summary>
        public static float ClampBevel(Vector3 size, float bevel)
        {
            float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            bevel = Mathf.Clamp(bevel, 0f, 0.2f * min);
            return bevel < 0.0008f ? 0f : bevel;
        }

        /// <summary>
        /// A unit cube (-0.5..0.5) meant to be scaled to `size` metres, with rounded edges of `bevel` metres and
        /// UVs in metres starting at each face's bottom-left corner. 108 triangles (12 with no bevel).
        /// </summary>
        public static MeshData Box(Vector3 size, float bevel)
        {
            size = new Vector3(Mathf.Max(Mathf.Abs(size.x), 1e-4f), Mathf.Max(Mathf.Abs(size.y), 1e-4f), Mathf.Max(Mathf.Abs(size.z), 1e-4f));
            float b = ClampBevel(size, bevel);
            var d = new MeshData();
            Vector3 h = size * 0.5f;
            Vector3 inner = h - Vector3.one * b;
            foreach (var f in boxFaces)
            {
                Vector3 n = f[0], right = f[1], up = f[2];
                float hn = Comp(h, Axis(n)), hu = Comp(h, Axis(right)), hv = Comp(h, Axis(up));
                float[] us = b > 0f ? new[] { -hu, -hu + b, hu - b, hu } : new[] { -hu, hu };
                float[] vs = b > 0f ? new[] { -hv, -hv + b, hv - b, hv } : new[] { -hv, hv };
                int start = d.vertices.Count;
                for (int j = 0; j < vs.Length; j++)
                    for (int i = 0; i < us.Length; i++)
                    {
                        Vector3 flat = n * hn + right * us[i] + up * vs[j];
                        Vector3 nrm = n, pos = flat;
                        if (b > 0f)
                        {
                            // Round the edges: push the point in to the inner box, then out again along the rounded normal.
                            var q = new Vector3(Mathf.Clamp(flat.x, -inner.x, inner.x), Mathf.Clamp(flat.y, -inner.y, inner.y), Mathf.Clamp(flat.z, -inner.z, inner.z));
                            nrm = (flat - q).normalized;
                            pos = q + nrm * b;
                        }
                        Vert(d, pos, nrm, right, up, new Vector2(us[i] + hu, vs[j] + hv), size);
                    }
                int w = us.Length;
                for (int j = 0; j < vs.Length - 1; j++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int bl = start + j * w + i, br = bl + 1, tl = bl + w, tr = tl + 1;
                        d.Tri(bl, tl, tr);
                        d.Tri(bl, tr, br);
                    }
            }
            return d;
        }

        // ------------------------------------------------------------------ cylinder

        /// <summary>Sides that look round at this size: 24 for small, up to 40 for big discs and columns.</summary>
        public static int CylinderSegments(Vector3 size)
        {
            float dia = Mathf.Max(size.x, size.z);
            return dia < 1f ? 24 : dia < 4f ? 32 : 40;
        }

        /// <summary>
        /// A cylinder like Unity's (unit diameter, height 2: y -1..1) meant to be scaled to `size` = (diameter x, height,
        /// diameter z) metres, i.e. object scale (x, height/2, z). Rounded rims of `bevel` metres; UVs in metres
        /// (sides: round the circumference by height, caps: x by z from the centre).
        /// </summary>
        public static MeshData Cylinder(Vector3 size, int segments, float bevel)
        {
            size = new Vector3(Mathf.Max(Mathf.Abs(size.x), 1e-4f), Mathf.Max(Mathf.Abs(size.y), 1e-4f), Mathf.Max(Mathf.Abs(size.z), 1e-4f));
            if (segments < 3) segments = CylinderSegments(size);
            float rx = size.x * 0.5f, rz = size.z * 0.5f, hy = size.y * 0.5f;
            float b = Mathf.Clamp(bevel, 0f, 0.2f * Mathf.Min(Mathf.Min(size.x, size.z), size.y));
            if (b < 0.0008f) b = 0f;
            var unit = new Vector3(size.x, hy, size.z);
            var d = new MeshData();

            // Profile from the bottom rim up to the top rim: (inset from the full radius, height, normal radial, normal up).
            var profile = new List<Vector4>();
            const float s45 = 0.70710678f;
            if (b > 0f)
            {
                profile.Add(new Vector4(b, -hy, 0f, -1f));
                profile.Add(new Vector4(b - b * s45, -hy + b - b * s45, s45, -s45));
                profile.Add(new Vector4(0f, -hy + b, 1f, 0f));
                profile.Add(new Vector4(0f, hy - b, 1f, 0f));
                profile.Add(new Vector4(b - b * s45, hy - b + b * s45, s45, s45));
                profile.Add(new Vector4(b, hy, 0f, 1f));
            }
            else
            {
                profile.Add(new Vector4(0f, -hy, 1f, 0f));
                profile.Add(new Vector4(0f, hy, 1f, 0f));
            }

            float avgR = (rx + rz) * 0.5f;
            int cols = segments + 1;
            int sideStart = d.vertices.Count;
            foreach (var p in profile)
                for (int i = 0; i < cols; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    var pos = new Vector3(c * (rx - p.x), p.y, s * (rz - p.x));
                    var radial = new Vector3(c * rz, 0f, s * rx).normalized; // ellipse normal direction
                    var nrm = radial * p.z + Vector3.up * p.w;
                    var tan = new Vector3(-s * rx, 0f, c * rz);
                    var along = radial * -p.w + Vector3.up * p.z; // up the profile (the V direction)
                    Vert(d, pos, nrm, tan, along, new Vector2(a * avgR, p.y + hy), unit);
                }
            for (int k = 0; k < profile.Count - 1; k++)
                for (int i = 0; i < segments; i++)
                {
                    int bl = sideStart + k * cols + i, br = bl + 1, tl = bl + cols, tr = tl + 1;
                    d.Tri(bl, tl, tr);
                    d.Tri(bl, tr, br);
                }

            // Caps: a fan from the centre (seen from above for the top, from below for the bottom).
            for (int cap = 0; cap < 2; cap++)
            {
                bool top = cap == 1;
                float y = top ? hy : -hy;
                var n = top ? Vector3.up : Vector3.down;
                var right = top ? Vector3.right : new Vector3(-1f, 0f, 0f);
                int centre = Vert(d, new Vector3(0f, y, 0f), n, right, Vector3.forward, Vector2.zero, unit);
                int ring = d.vertices.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    var pos = new Vector3(Mathf.Cos(a) * (rx - b), y, Mathf.Sin(a) * (rz - b));
                    Vert(d, pos, n, right, Vector3.forward, new Vector2(top ? pos.x : -pos.x, pos.z), unit);
                }
                for (int i = 0; i < segments; i++)
                {
                    int p0 = ring + i, p1 = ring + (i + 1) % segments;
                    if (top) d.Tri(centre, p1, p0);
                    else d.Tri(centre, p0, p1);
                }
            }
            return d;
        }

        // ------------------------------------------------------------------ quad

        /// <summary>A quad like Unity's (unit square in X/Y, visible from -Z) meant to be scaled to `size` metres.</summary>
        public static MeshData Quad(Vector2 size)
        {
            var s = new Vector3(Mathf.Max(Mathf.Abs(size.x), 1e-4f), Mathf.Max(Mathf.Abs(size.y), 1e-4f), 1f);
            var d = new MeshData();
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++)
                {
                    var pos = new Vector3((i - 0.5f) * s.x, (j - 0.5f) * s.y, 0f);
                    Vert(d, pos, new Vector3(0f, 0f, -1f), Vector3.right, Vector3.up, new Vector2(i * s.x, j * s.y), s);
                }
            d.Tri(0, 2, 3);
            d.Tri(0, 3, 1);
            return d;
        }

        // ------------------------------------------------------------------ rock

        /// <summary>
        /// A chunky boulder: an icosphere (detail 2 = 320 triangles) pushed about by 3D noise and sliced by a few flat
        /// planes (rocks have faces), with a flattened bottom. It exactly fills the unit box (-0.5..0.5), so the
        /// object's scale is its size. Mostly faceted shading; UVs projected along each face's main axis, in metres
        /// of `size`.
        /// </summary>
        public static MeshData Rock(int seed, int detail, Vector3 size)
        {
            size = new Vector3(Mathf.Max(Mathf.Abs(size.x), 0.01f), Mathf.Max(Mathf.Abs(size.y), 0.01f), Mathf.Max(Mathf.Abs(size.z), 0.01f));
            List<Vector3> pts;
            List<int> tris;
            Icosphere(Mathf.Clamp(detail, 0, 3), out pts, out tris);

            // Random cutting planes (seeded), plus a flat-ish base.
            var planes = new List<Vector4>();
            int cuts = 5 + (int)(ProcTex.Hash01(seed, 1, 77) * 3f);
            for (int k = 0; k < cuts; k++)
            {
                float a = ProcTex.Hash01(seed, k, 11) * Mathf.PI * 2f, z = ProcTex.Hash01(seed, k, 12) * 1.6f - 0.6f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                var nrm = new Vector3(Mathf.Cos(a) * r, z, Mathf.Sin(a) * r);
                planes.Add(new Vector4(nrm.x, nrm.y, nrm.z, 0.36f + ProcTex.Hash01(seed, k, 13) * 0.1f));
            }
            planes.Add(new Vector4(0f, -1f, 0f, 0.3f));

            var shaped = new Vector3[pts.Count];
            Vector3 lo = Vector3.one * 9f, hi = -Vector3.one * 9f;
            float ox = ProcTex.Hash01(seed, 0, 3) * 50f, oz = ProcTex.Hash01(seed, 0, 4) * 50f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 dir = pts[i];
                float n = ProcTex.Fbm3(dir.x * 1.7f + ox, dir.y * 1.7f, dir.z * 1.7f + oz, 4, seed);
                float r = 0.5f * (0.8f + 0.45f * n);
                foreach (var p in planes)
                {
                    float dn = dir.x * p.x + dir.y * p.y + dir.z * p.z;
                    if (dn > 0.05f) r = Mathf.Min(r, p.w / dn);
                }
                var v = dir * r;
                shaped[i] = v;
                lo = new Vector3(Mathf.Min(lo.x, v.x), Mathf.Min(lo.y, v.y), Mathf.Min(lo.z, v.z));
                hi = new Vector3(Mathf.Max(hi.x, v.x), Mathf.Max(hi.y, v.y), Mathf.Max(hi.z, v.z));
            }
            // Stretch to fill the unit box exactly, then to metres.
            Vector3 span = hi - lo, mid = (hi + lo) * 0.5f;
            for (int i = 0; i < shaped.Length; i++)
                shaped[i] = Mul(Div(shaped[i] - mid, span), size);

            // Smooth normals (to soften the facets a little).
            var smooth = new Vector3[shaped.Length];
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = shaped[tris[t]], b = shaped[tris[t + 1]], c = shaped[tris[t + 2]];
                var fn = Vector3.Cross(b - a, c - a);
                smooth[tris[t]] += fn;
                smooth[tris[t + 1]] += fn;
                smooth[tris[t + 2]] += fn;
            }

            var d = new MeshData();
            for (int t = 0; t < tris.Count; t += 3)
            {
                int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                Vector3 a = shaped[ia], b = shaped[ib], c = shaped[ic];
                var fn = Vector3.Cross(b - a, c - a).normalized;
                // Project the UVs along the face's main axis.
                int axis = Mathf.Abs(fn.x) > Mathf.Abs(fn.y) ? (Mathf.Abs(fn.x) > Mathf.Abs(fn.z) ? 0 : 2) : (Mathf.Abs(fn.y) > Mathf.Abs(fn.z) ? 1 : 2);
                Vector3 right = axis == 0 ? new Vector3(0f, 0f, Mathf.Sign(fn.x)) : axis == 1 ? Vector3.right : new Vector3(-Mathf.Sign(fn.z), 0f, 0f);
                Vector3 up = axis == 1 ? new Vector3(0f, 0f, Mathf.Sign(fn.y)) : Vector3.up;
                int first = d.vertices.Count;
                foreach (int idx in new[] { ia, ib, ic })
                {
                    Vector3 p = shaped[idx];
                    var nrm = (fn * 0.7f + smooth[idx].normalized * 0.3f).normalized;
                    Vert(d, p, nrm, right, up, new Vector2(Vector3.Dot(p, right), Vector3.Dot(p, up)), size);
                }
                d.Tri(first, first + 1, first + 2);
            }
            return d;
        }

        /// <summary>Unit icosphere (points on the unit sphere) with outward-facing clockwise triangles.</summary>
        static void Icosphere(int detail, out List<Vector3> pts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            pts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < pts.Count; i++) pts[i] = pts[i].normalized;
            tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int level = 0; level < detail; level++)
            {
                var mids = new Dictionary<long, int>();
                var next = new List<int>(tris.Count * 4);
                var p = pts;
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mids.TryGetValue(key, out int m)) return m;
                    p.Add(((p[a] + p[b]) * 0.5f).normalized);
                    mids[key] = p.Count - 1;
                    return p.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            // Make every triangle clockwise seen from outside (Unity's front face).
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = pts[tris[i]], b = pts[tris[i + 1]], c = pts[tris[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                {
                    int tmp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = tmp;
                }
            }
        }
    }
}
