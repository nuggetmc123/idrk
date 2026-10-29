using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    public enum Surface
    {
        Auto, Generic, Concrete, Metal, PaintedMetal, Wood, Sand, Dirt, Grass, Snow, Asphalt, Tile, Brick,
        Camo, Fabric, Rock, Rubber, Plastic, Sandbag, Hesco, Plank, Ice
    }

    /// <summary>
    /// Makes the game's textures in code: a tileable detail map (multiplied
    /// with each material's colour) and a matching normal map for every kind of
    /// surface, plus box meshes whose UVs are in meters so textures keep the
    /// same size on a crate and on a 70 m wall.
    /// </summary>
    public static class Surfaces
    {
        const int N = 256;
        static readonly Dictionary<Surface, (Texture2D albedo, Texture2D normal)> cache = new Dictionary<Surface, (Texture2D, Texture2D)>();
        static readonly Dictionary<Vector3Int, Mesh> boxes = new Dictionary<Vector3Int, Mesh>();

        /// <summary>How many meters one copy of the texture covers.</summary>
        public static float TileMeters(Surface s)
        {
            switch (s)
            {
                case Surface.Metal: case Surface.Plastic: case Surface.Rubber: return 0.8f;
                case Surface.PaintedMetal: return 2f;
                case Surface.Wood: case Surface.Plank: return 1.5f;
                case Surface.Tile: return 2f;
                case Surface.Brick: return 1.6f;
                case Surface.Camo: return 3f;
                case Surface.Fabric: case Surface.Sandbag: return 0.8f;
                case Surface.Hesco: return 1f;
                case Surface.Grass: case Surface.Sand: case Surface.Dirt: case Surface.Snow: return 4f;
                case Surface.Asphalt: return 3f;
                case Surface.Rock: return 3f;
                default: return 2.5f;
            }
        }

        static readonly System.Collections.Concurrent.ConcurrentDictionary<Surface, (Color[] albedo, Color[] normal)> ready =
            new System.Collections.Concurrent.ConcurrentDictionary<Surface, (Color[], Color[])>();

        /// <summary>Paint every surface on worker threads (call once at start-up; the loading screen waits for it).</summary>
        public static System.Threading.Tasks.Task Prewarm()
        {
            return System.Threading.Tasks.Task.Run(() =>
                System.Threading.Tasks.Parallel.ForEach((Surface[])Enum.GetValues(typeof(Surface)), s =>
                {
                    if (s == Surface.Auto || ready.ContainsKey(s)) return;
                    ready[s] = Paint(s);
                }));
        }

        static (Color[] albedo, Color[] normal) Paint(Surface s)
        {
            var height = new float[N * N];
            var color = new Color[N * N];
            Generate(s, height, color);
            return (color, NormalsFromHeight(height, Strength(s)));
        }

        public static (Texture2D albedo, Texture2D normal) Get(Surface s)
        {
            if (cache.TryGetValue(s, out var t) && t.albedo != null) return t;
            if (!ready.TryGetValue(s, out var px)) px = Paint(s);
            var color = px.albedo;
            var albedo = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "fz_" + s, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            albedo.SetPixels(color);
            albedo.Apply(true, true);
            var normal = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "fz_" + s + "_n", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            normal.SetPixels(px.normal);
            normal.Apply(true, true);
            t = (albedo, normal);
            cache[s] = t;
            return t;
        }

        static float Strength(Surface s)
        {
            switch (s)
            {
                case Surface.Brick: case Surface.Tile: case Surface.Rock: case Surface.Hesco: case Surface.Plank: return 3.5f;
                case Surface.Concrete: case Surface.Asphalt: case Surface.Sandbag: case Surface.Wood: return 2.2f;
                case Surface.Metal: case Surface.Plastic: case Surface.Ice: return 0.8f;
                case Surface.Snow: return 1.2f;
                default: return 1.8f;
            }
        }

        /* ---------------- tileable noise ---------------- */

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        /// <summary>Value noise that wraps every px cells across and py cells down (so textures tile).</summary>
        static float Noise(float x, float y, int seed, int px, int py)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int x0 = ((xi % px) + px) % px, y0 = ((yi % py) + py) % py;
            int x1 = (x0 + 1) % px, y1 = (y0 + 1) % py;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float F(float u, float v, int fx, int fy, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u * fx, v * fy, seed + o * 31, fx, fy) * amp;
                norm += amp;
                amp *= 0.5f;
                fx *= 2;
                fy *= 2;
            }
            return sum / norm;
        }

        static Color Gray(float g) => new Color(g, g, g, 1f);

        /* ---------------- the surfaces ---------------- */

        static void Generate(Surface s, float[] h, Color[] c)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (float)x / N, v = (float)y / N;
                    int i = y * N + x;
                    float height = 0.5f;
                    Color col = Color.white;
                    switch (s)
                    {
                        case Surface.Concrete:
                        {
                            float n = F(u, v, 8, 8, 5, 11);
                            float pits = F(u, v, 64, 64, 2, 12) > 0.78f ? -0.25f : 0;
                            float stain = F(u, v, 3, 3, 3, 13);
                            height = n * 0.6f + pits;
                            col = Gray(0.78f + (n - 0.5f) * 0.25f - (stain > 0.62f ? (stain - 0.62f) * 0.8f : 0) + pits * 0.3f);
                            break;
                        }
                        case Surface.Metal:
                        {
                            float brushed = F(u, v, 2, 64, 3, 21);
                            float scratch = F(u, v, 16, 2, 2, 22) > 0.72f && F(u, v, 64, 64, 1, 23) > 0.5f ? 0.12f : 0;
                            height = brushed * 0.3f - scratch;
                            col = Gray(0.82f + (brushed - 0.5f) * 0.18f + scratch);
                            break;
                        }
                        case Surface.PaintedMetal:
                        {
                            float n = F(u, v, 6, 6, 4, 31);
                            float chip = F(u, v, 12, 12, 4, 32);
                            bool chipped = chip > 0.7f;
                            float seam = Seam(u, 4) * Seam(v, 1);
                            float rivet = (Frac(u * 8) - 0.5f) * (Frac(u * 8) - 0.5f) + (Frac(v * 8) - 0.06f) * (Frac(v * 8) - 0.06f) < 0.0015f ? 0.3f : 0;
                            height = 0.6f + (chipped ? -0.15f : 0) - (1 - seam) * 0.5f + rivet + n * 0.05f;
                            col = chipped ? new Color(0.55f, 0.5f, 0.45f) : Gray(0.92f + (n - 0.5f) * 0.12f - (1 - seam) * 0.35f);
                            float rust = F(u, v, 5, 5, 4, 33);
                            if (rust > 0.66f) col = Color.Lerp(col, new Color(0.6f, 0.38f, 0.22f), (rust - 0.66f) * 2f);
                            break;
                        }
                        case Surface.Wood:
                        case Surface.Plank:
                        {
                            float plank = s == Surface.Plank ? Frac(u * 4) : Frac(u * 2);
                            int idx = Mathf.FloorToInt(u * (s == Surface.Plank ? 4 : 2));
                            float grain = F(u + idx * 0.13f, v, 2, 32, 3, 41 + idx);
                            float rings = Mathf.Sin((grain * 12f + v * 6f) * Mathf.PI) * 0.5f + 0.5f;
                            float gap = plank < 0.03f || plank > 0.97f ? 1 : 0;
                            height = 0.6f + rings * 0.12f - gap * 0.5f;
                            float tone = 0.82f + Hash(idx, 7, 42) * 0.18f;
                            col = Gray(tone * (0.85f + rings * 0.15f) * (1 - gap * 0.5f));
                            break;
                        }
                        case Surface.Sand:
                        {
                            float n = F(u, v, 16, 16, 4, 51);
                            float ripple = Mathf.Sin((u * 10 + F(u, v, 4, 4, 2, 52) * 2.5f) * Mathf.PI * 2) * 0.5f + 0.5f;
                            height = ripple * 0.35f + n * 0.3f;
                            col = Gray(0.88f + (n - 0.5f) * 0.18f + ripple * 0.05f);
                            break;
                        }
                        case Surface.Dirt:
                        {
                            float n = F(u, v, 8, 8, 5, 61);
                            float pebbles = F(u, v, 48, 48, 1, 62) > 0.8f ? 0.3f : 0;
                            height = n * 0.6f + pebbles;
                            col = Gray(0.8f + (n - 0.5f) * 0.35f + pebbles * 0.4f);
                            break;
                        }
                        case Surface.Grass:
                        {
                            float n = F(u, v, 8, 8, 4, 71);
                            float blades = F(u, v, 128, 32, 2, 72);
                            float patch = F(u, v, 3, 3, 3, 73);
                            height = blades * 0.7f + n * 0.2f;
                            col = new Color(0.8f + (patch - 0.5f) * 0.3f, 0.9f + (n - 0.5f) * 0.2f + blades * 0.1f, 0.75f, 1) * (0.85f + blades * 0.2f);
                            break;
                        }
                        case Surface.Snow:
                        {
                            float n = F(u, v, 6, 6, 4, 81);
                            float sparkle = Hash(x, y, 82) > 0.996f ? 0.08f : 0;
                            height = n * 0.5f;
                            col = Gray(0.95f + (n - 0.5f) * 0.08f + sparkle);
                            break;
                        }
                        case Surface.Ice:
                        {
                            float n = F(u, v, 4, 4, 4, 85);
                            float crack = Mathf.Abs(F(u, v, 6, 6, 3, 86) - 0.5f) < 0.012f ? 1 : 0;
                            height = n * 0.2f - crack * 0.3f;
                            col = Gray(0.9f + (n - 0.5f) * 0.15f + crack * 0.1f);
                            break;
                        }
                        case Surface.Asphalt:
                        {
                            float n = F(u, v, 16, 16, 3, 91);
                            float speck = Hash(x, y, 92);
                            float crack = Mathf.Abs(F(u, v, 4, 4, 3, 93) - 0.5f) < 0.008f ? 1 : 0;
                            height = n * 0.4f + (speck > 0.85f ? 0.2f : 0) - crack * 0.4f;
                            col = Gray(0.75f + (n - 0.5f) * 0.15f + (speck > 0.9f ? 0.15f : speck < 0.08f ? -0.1f : 0) - crack * 0.2f);
                            break;
                        }
                        case Surface.Tile:
                        {
                            float gx = Frac(u * 4), gy = Frac(v * 4);
                            bool grout = gx < 0.035f || gy < 0.035f;
                            int tx = Mathf.FloorToInt(u * 4), ty = Mathf.FloorToInt(v * 4);
                            float n = F(u, v, 16, 16, 3, 101);
                            height = grout ? 0.1f : 0.7f + n * 0.05f;
                            col = grout ? Gray(0.5f) : Gray(0.88f + (Hash(tx, ty, 102) - 0.5f) * 0.12f + (n - 0.5f) * 0.06f);
                            break;
                        }
                        case Surface.Brick:
                        {
                            int row = Mathf.FloorToInt(v * 8);
                            float bx = Frac(u * 4 + (row % 2) * 0.5f), by = Frac(v * 8);
                            bool mortar = bx < 0.05f || by < 0.1f;
                            int id = Mathf.FloorToInt(u * 4 + (row % 2) * 0.5f) * 13 + row;
                            float n = F(u, v, 16, 16, 3, 111);
                            height = mortar ? 0.15f : 0.7f + n * 0.1f;
                            float tone = 0.78f + Hash(id, 1, 112) * 0.22f;
                            col = mortar ? new Color(0.75f, 0.73f, 0.7f) : new Color(tone, tone * 0.92f, tone * 0.88f) * (0.9f + n * 0.15f);
                            break;
                        }
                        case Surface.Camo:
                        {
                            float a = F(u, v, 4, 4, 4, 121), b = F(u, v, 5, 5, 4, 122), d = F(u, v, 6, 6, 3, 123);
                            col = a > 0.56f ? new Color(0.36f, 0.4f, 0.24f) : b > 0.55f ? new Color(0.48f, 0.4f, 0.27f) : d > 0.6f ? new Color(0.16f, 0.17f, 0.12f) : new Color(0.55f, 0.58f, 0.4f);
                            float weave = (Frac(u * 96) < 0.5f ^ Frac(v * 96) < 0.5f) ? 0.04f : 0;
                            height = 0.5f + weave;
                            col *= 1.6f - weave;
                            break;
                        }
                        case Surface.Fabric:
                        {
                            float weave = (Mathf.Sin(u * N * 0.8f) * Mathf.Sin(v * N * 0.8f)) * 0.5f + 0.5f;
                            float n = F(u, v, 8, 8, 3, 131);
                            height = weave * 0.4f;
                            col = Gray(0.85f + (n - 0.5f) * 0.12f + weave * 0.06f);
                            break;
                        }
                        case Surface.Sandbag:
                        {
                            float weave = (Mathf.Sin(u * N * 1.2f) + Mathf.Sin(v * N * 1.2f)) * 0.25f + 0.5f;
                            float n = F(u, v, 6, 6, 3, 141);
                            height = weave * 0.3f + n * 0.4f;
                            col = Gray(0.85f + (n - 0.5f) * 0.2f) * new Color(1, 0.95f, 0.85f);
                            break;
                        }
                        case Surface.Hesco:
                        {
                            bool wire = Frac(u * 8) < 0.06f || Frac(v * 8) < 0.06f;
                            float n = F(u, v, 8, 8, 4, 151);
                            height = wire ? 0.9f : 0.3f + n * 0.3f;
                            col = wire ? Gray(0.6f) : new Color(0.82f, 0.76f, 0.6f) * (0.85f + n * 0.25f);
                            break;
                        }
                        case Surface.Rock:
                        {
                            float n = F(u, v, 4, 4, 6, 161);
                            float crack = Mathf.Abs(F(u, v, 3, 3, 4, 162) - 0.5f) < 0.015f ? 1 : 0;
                            height = n - crack * 0.4f;
                            col = Gray(0.72f + (n - 0.5f) * 0.4f - crack * 0.3f);
                            break;
                        }
                        case Surface.Rubber:
                        case Surface.Plastic:
                        {
                            float n = F(u, v, 32, 32, 2, 171);
                            height = n * 0.3f;
                            col = Gray(0.9f + (n - 0.5f) * 0.08f);
                            break;
                        }
                        default: // Generic: a gentle grain so flat colours don't look like plastic
                        {
                            float n = F(u, v, 8, 8, 4, 181);
                            float grime = F(u, v, 3, 3, 3, 182);
                            height = n * 0.4f;
                            col = Gray(0.9f + (n - 0.5f) * 0.12f - (grime > 0.65f ? (grime - 0.65f) * 0.5f : 0));
                            break;
                        }
                    }
                    h[i] = height;
                    col.a = 1;
                    c[i] = col;
                }
        }

        static float Frac(float x) => x - Mathf.Floor(x);
        static float Seam(float t, int count) { float f = Frac(t * count); return f < 0.012f || f > 0.988f ? 0 : 1; }

        static Color[] NormalsFromHeight(float[] h, float strength)
        {
            var o = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float l = h[y * N + (x + N - 1) % N], r = h[y * N + (x + 1) % N];
                    float d = h[((y + N - 1) % N) * N + x], u = h[((y + 1) % N) * N + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    float nx = n.x * 0.5f + 0.5f, ny = n.y * 0.5f + 0.5f, nz = n.z * 0.5f + 0.5f;
                    // RGB for mobile, and X in alpha too so desktop (DXT5nm-style unpacking) reads it right
                    o[y * N + x] = new Color(nx, ny, nz, nx);
                }
            return o;
        }

        /* ---------------- boxes with meter UVs ---------------- */

        /// <summary>A cube mesh whose UVs are scaled so a texture is TileMeters wide on every face.</summary>
        public static Mesh Box(Vector3 size)
        {
            var key = new Vector3Int(Mathf.RoundToInt(size.x * 20), Mathf.RoundToInt(size.y * 20), Mathf.RoundToInt(size.z * 20));
            if (boxes.TryGetValue(key, out var m) && m != null) return m;
            float sx = Mathf.Max(0.01f, key.x / 20f), sy = Mathf.Max(0.01f, key.y / 20f), sz = Mathf.Max(0.01f, key.z / 20f);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 n, Vector3 a, Vector3 b, float ua, float ub)
            {
                // a, b: the face's two edge axes (unit), ua/ub: their real lengths in meters
                Vector3 c = n * 0.5f;
                int i = verts.Count;
                verts.Add(c - a * 0.5f - b * 0.5f); verts.Add(c + a * 0.5f - b * 0.5f); verts.Add(c + a * 0.5f + b * 0.5f); verts.Add(c - a * 0.5f + b * 0.5f);
                for (int k = 0; k < 4; k++) norms.Add(n);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(ua, 0)); uvs.Add(new Vector2(ua, ub)); uvs.Add(new Vector2(0, ub));
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, sx, sy);
            Face(Vector3.back, Vector3.right, Vector3.up, sx, sy);
            Face(Vector3.right, Vector3.forward, Vector3.up, sz, sy);
            Face(Vector3.left, Vector3.back, Vector3.up, sz, sy);
            Face(Vector3.up, Vector3.right, Vector3.forward, sx, sz);
            Face(Vector3.down, Vector3.left, Vector3.forward, sx, sz);
            m = new Mesh { name = "fz_box" };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            boxes[key] = m;
            return m;
        }
    }
}
