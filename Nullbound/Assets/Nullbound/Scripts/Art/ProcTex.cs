using UnityEngine;

namespace Nullbound.Art
{
    /// <summary>
    /// One generated PBR texture set, laid out like Unity's Texture2D.SetPixels (row 0 = bottom).
    /// albedo: sRGB colour (alpha = 1 unless the surface is cut out).
    /// normal: tangent-space normal packed for BOTH Unity decoders: R = x, G = y, B = z, A = x
    ///   (mobile reads RGB, desktop reads the DXT5nm layout AG), so runtime-made normal maps work everywhere.
    /// mask: Standard shader _MetallicGlossMap layout: R = metallic, A = smoothness. G = ambient occlusion
    ///   (bound as _OcclusionMap, which the Standard shader reads from G).
    /// emission: optional, null when the surface doesn't glow.
    /// </summary>
    public sealed class TexSet
    {
        public string name;
        public int size;
        public Color[] albedo, normal, mask, emission;
        /// <summary>How many metres one repeat of this texture should cover on a surface.</summary>
        public float metres = 1f;
    }

    /// <summary>
    /// Procedural texture toolkit: pure maths with no Unity objects, so the same code runs in the editor (to
    /// write texture assets), at runtime (fighter detail maps) and outside Unity (preview pictures).
    /// Everything is TILEABLE: u and v run 0..1 over the texture, and every noise takes a whole number of cells
    /// across it, so the left edge matches the right edge and the bottom matches the top (no seams when repeated).
    /// </summary>
    public static class ProcTex
    {
        // ------------------------------------------------------------------ hashing

        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>A repeatable random number 0..1 for a grid cell.</summary>
        public static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777216f;

        /// <summary>Stable seed from a name (string.GetHashCode differs between runtimes).</summary>
        public static int Seed(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in s) h = (h ^ c) * 16777619u;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        static int Wrap(int i, int n) { i %= n; return i < 0 ? i + n : i; }
        public static float Frac(float x) => x - Mathf.Floor(x);
        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        // ------------------------------------------------------------------ noise (all tileable)

        /// <summary>Value noise 0..1 with `cells` cells across the texture.</summary>
        public static float Value(float u, float v, int cells, int seed)
        {
            float x = Frac(u) * cells, y = Frac(v) * cells;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Fade(x - x0), fy = Fade(y - y0);
            int x1 = Wrap(x0 + 1, cells), y1 = Wrap(y0 + 1, cells);
            x0 = Wrap(x0, cells); y0 = Wrap(y0, cells);
            float a = Hash01(x0, y0, seed), b = Hash01(x1, y0, seed), c = Hash01(x0, y1, seed), d = Hash01(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Grad(int ix, int iy, int seed, float dx, float dy)
        {
            float a = Hash01(ix, iy, seed) * Mathf.PI * 2f;
            return Mathf.Cos(a) * dx + Mathf.Sin(a) * dy;
        }

        /// <summary>Gradient (Perlin-style) noise 0..1, smoother and less blocky than value noise.</summary>
        public static float Perlin(float u, float v, int cells, int seed)
        {
            float x = Frac(u) * cells, y = Frac(v) * cells;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float dx = x - x0, dy = y - y0;
            int x1 = Wrap(x0 + 1, cells), y1 = Wrap(y0 + 1, cells);
            x0 = Wrap(x0, cells); y0 = Wrap(y0, cells);
            float fx = Fade(dx), fy = Fade(dy);
            float n00 = Grad(x0, y0, seed, dx, dy), n10 = Grad(x1, y0, seed, dx - 1f, dy);
            float n01 = Grad(x0, y1, seed, dx, dy - 1f), n11 = Grad(x1, y1, seed, dx - 1f, dy - 1f);
            float n = Mathf.Lerp(Mathf.Lerp(n00, n10, fx), Mathf.Lerp(n01, n11, fx), fy);
            return Mathf.Clamp01(n * 0.70710678f + 0.5f);
        }

        /// <summary>Fractal (layered) gradient noise 0..1. Each octave doubles the cells, so it stays tileable.</summary>
        public static float Fbm(float u, float v, int cells, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int c = Mathf.Max(1, cells);
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(u, v, c, seed + i * 1013) * amp;
                norm += amp;
                amp *= gain;
                c *= 2;
            }
            return sum / norm;
        }

        /// <summary>Ridged fractal noise 0..1: sharp creases (rock, cracks, veins).</summary>
        public static float Ridged(float u, float v, int cells, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int c = Mathf.Max(1, cells);
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin(u, v, c, seed + i * 733) * 2f - 1f);
                sum += n * n * amp;
                norm += amp;
                amp *= gain;
                c *= 2;
            }
            return sum / norm;
        }

        /// <summary>
        /// Cellular (Worley) noise: f1 = distance to the nearest random point (0..~1 in cell units), edge = f2 - f1
        /// (0 on the borders between cells: cobbles, cracks, scales). Returns f1. id = a random 0..1 per cell.
        /// </summary>
        public static float Worley(float u, float v, int cells, int seed, out float edge, out float id)
        {
            float x = Frac(u) * cells, y = Frac(v) * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 9f, f2 = 9f;
            id = 0f;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float px = gx + Hash01(wx, wy, seed), py = gy + Hash01(wx, wy, seed + 17);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; id = Hash01(wx, wy, seed + 31); }
                    else if (d < f2) f2 = d;
                }
            edge = f2 - f1;
            return f1;
        }

        public static float Worley(float u, float v, int cells, int seed) => Worley(u, v, cells, seed, out _, out _);

        /// <summary>Warp the coordinates by noise (organic, streaky looks). Still tileable.</summary>
        public static void Warp(ref float u, ref float v, int cells, float amount, int seed)
        {
            float wu = Fbm(u, v, cells, 3, seed) - 0.5f, wv = Fbm(u, v, cells, 3, seed + 99) - 0.5f;
            u += wu * amount;
            v += wv * amount;
        }

        // ------------------------------------------------------------------ shape helpers

        /// <summary>1 on a line every 1/count of the way across, falling to 0 at halfWidth (in 0..1 texture units).</summary>
        public static float Lines(float t, int count, float halfWidth)
        {
            float f = Frac(t * count);
            float d = Mathf.Min(f, 1f - f) / count; // distance to the nearest line, texture units
            return 1f - Mathf.Clamp01(d / Mathf.Max(halfWidth, 1e-5f));
        }

        /// <summary>A groove profile (0 = flat, 1 = bottom of the groove) with soft bevelled shoulders.</summary>
        public static float Groove(float t, int count, float halfWidth, float bevel)
        {
            float f = Frac(t * count);
            float d = Mathf.Min(f, 1f - f) / count;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth, halfWidth + bevel, d));
        }

        /// <summary>Distance (texture units) from (u,v) to the nearest point of a tiled grid of dots.</summary>
        public static float DotDistance(float u, float v, int countU, int countV, float offU = 0.5f, float offV = 0.5f)
        {
            float fu = Frac(u * countU - offU + 0.5f) - 0.5f, fv = Frac(v * countV - offV + 0.5f) - 0.5f;
            return Mathf.Sqrt(fu * fu / (countU * countU) + fv * fv / (countV * countV));
        }

        public static float Saturate(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        public static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // ------------------------------------------------------------------ building texture sets

        public delegate void Sample(float u, float v, out Color albedo, out float height, out float metallic, out float smoothness, out float ao);

        /// <summary>
        /// Evaluates a surface function over the texture and builds albedo, normal (from the height), and mask.
        /// strength = how bumpy the normal map is (height units per texture width; 2-8 is typical).
        /// </summary>
        public static TexSet Build(string name, int size, float metres, float strength, Sample sample, System.Func<float, float, Color> emission = null)
        {
            var set = new TexSet { name = name, size = size, metres = metres };
            int n = size * size;
            set.albedo = new Color[n];
            set.mask = new Color[n];
            var height = new float[n];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    sample(u, v, out var c, out float h, out float m, out float s, out float ao);
                    int i = y * size + x;
                    set.albedo[i] = new Color(Saturate(c.r), Saturate(c.g), Saturate(c.b), 1f);
                    height[i] = h;
                    set.mask[i] = new Color(Saturate(m), Saturate(ao), 0f, Saturate(s));
                }
            }
            set.normal = NormalMap(height, size, strength);
            if (emission != null)
            {
                set.emission = new Color[n];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        set.emission[y * size + x] = emission((x + 0.5f) / size, (y + 0.5f) / size);
            }
            return set;
        }

        /// <summary>Normal map from a tileable height field (wraps at the edges). Packed as described on TexSet.</summary>
        public static Color[] NormalMap(float[] height, int size, float strength)
        {
            var result = new Color[size * size];
            float k = strength * size / 256f; // keep the same look at any resolution
            for (int y = 0; y < size; y++)
            {
                int yu = (y + 1) % size, yd = (y - 1 + size) % size;
                for (int x = 0; x < size; x++)
                {
                    int xr = (x + 1) % size, xl = (x - 1 + size) % size;
                    float dx = (height[y * size + xr] - height[y * size + xl]) * 0.5f;
                    float dy = (height[yu * size + x] - height[yd * size + x]) * 0.5f;
                    result[y * size + x] = PackNormal(new Vector3(-dx * k, -dy * k, 1f).normalized);
                }
            }
            return result;
        }

        public static Color PackNormal(Vector3 n) => new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, n.x * 0.5f + 0.5f);

        /// <summary>A flat normal map (for surfaces that only need colour).</summary>
        public static Color FlatNormal => new Color(0.5f, 0.5f, 1f, 0.5f);

        /// <summary>Colour helpers: hex to colour, and a random-but-repeatable tint variation.</summary>
        public static Color Hex(string hex)
        {
            hex = hex.TrimStart('#');
            int v = System.Convert.ToInt32(hex.Substring(0, 6), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
