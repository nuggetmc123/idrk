using UnityEngine;

namespace Nullbound.Art
{
    /// <summary>
    /// More building blocks for texture recipes: stretched (one-direction) noise for grain and brushing, shape
    /// distances for panels, bolts and strokes, scattering helpers, colour helpers, and a 3D noise for meshes.
    /// Same rules as the rest of ProcTex: pure maths, and every 2D noise is tileable.
    /// </summary>
    public static partial class ProcTex
    {
        // ------------------------------------------------------------------ stretched (anisotropic) noise

        /// <summary>Gradient noise 0..1 with a different number of cells across and up (grain, brushing, strata).</summary>
        public static float Perlin(float u, float v, int cellsU, int cellsV, int seed)
        {
            float x = Frac(u) * cellsU, y = Frac(v) * cellsV;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float dx = x - x0, dy = y - y0;
            int x1 = Wrap(x0 + 1, cellsU), y1 = Wrap(y0 + 1, cellsV);
            x0 = Wrap(x0, cellsU); y0 = Wrap(y0, cellsV);
            float fx = Fade(dx), fy = Fade(dy);
            float n00 = Grad(x0, y0, seed, dx, dy), n10 = Grad(x1, y0, seed, dx - 1f, dy);
            float n01 = Grad(x0, y1, seed, dx, dy - 1f), n11 = Grad(x1, y1, seed, dx - 1f, dy - 1f);
            float n = Mathf.Lerp(Mathf.Lerp(n00, n10, fx), Mathf.Lerp(n01, n11, fx), fy);
            return Mathf.Clamp01(n * 0.70710678f + 0.5f);
        }

        /// <summary>Fractal version of the stretched noise (cells double every octave on both axes).</summary>
        public static float Fbm(float u, float v, int cellsU, int cellsV, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int cu = Mathf.Max(1, cellsU), cv = Mathf.Max(1, cellsV);
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(u, v, cu, cv, seed + i * 1013) * amp;
                norm += amp;
                amp *= gain;
                cu *= 2;
                cv *= 2;
            }
            return sum / norm;
        }

        /// <summary>Cellular noise with different cell counts across and up (stretched cells: bark plates, stones in courses).</summary>
        public static float Worley(float u, float v, int cellsU, int cellsV, int seed, out float edge, out float id)
        {
            float x = Frac(u) * cellsU, y = Frac(v) * cellsV;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 9f, f2 = 9f;
            id = 0f;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    int wx = Wrap(gx, cellsU), wy = Wrap(gy, cellsV);
                    float px = gx + Hash01(wx, wy, seed), py = gy + Hash01(wx, wy, seed + 17);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; id = Hash01(wx, wy, seed + 31); }
                    else if (d < f2) f2 = d;
                }
            edge = f2 - f1;
            return f1;
        }

        // ------------------------------------------------------------------ shapes (all distances in the caller's units)

        /// <summary>Signed distance to an axis-aligned box centred on 0 (negative inside).</summary>
        public static float Box(float x, float y, float halfW, float halfH)
        {
            float dx = Mathf.Abs(x) - halfW, dy = Mathf.Abs(y) - halfH;
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
        }

        /// <summary>Signed distance to a box with rounded corners (radius r).</summary>
        public static float RoundBox(float x, float y, float halfW, float halfH, float r) => Box(x, y, halfW - r, halfH - r) - r;

        /// <summary>Distance from (px,py) to the segment a-b. t = where along it (0 at a, 1 at b).</summary>
        public static float Segment(float px, float py, float ax, float ay, float bx, float by, out float t)
        {
            float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
            float len2 = vx * vx + vy * vy;
            t = len2 > 1e-12f ? Saturate((wx * vx + wy * vy) / len2) : 0f;
            float dx = wx - vx * t, dy = wy - vy * t;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Anti-aliased coverage of a shape from its signed distance (aa = half the soft edge width).</summary>
        public static float Fill(float signedDistance, float aa) => Saturate(0.5f - signedDistance / Mathf.Max(aa * 2f, 1e-6f));

        /// <summary>Distance to the nearest line of a regular grid of lines (period apart), in the caller's units.</summary>
        public static float LineDistance(float x, float period)
        {
            float f = x / period;
            f -= Mathf.Floor(f);
            return Mathf.Min(f, 1f - f) * period;
        }

        /// <summary>
        /// Splits 0..1 into n pieces of random width (each between 1-jitter and 1+jitter of the average), tileable:
        /// returns the piece index and where the point is inside it (0..1) and the piece's width.
        /// Used for random brick, block and plank lengths along a row.
        /// </summary>
        public static int Pieces(float t, int n, float jitter, int seed, out float local, out float width)
        {
            t = Frac(t);
            // Cumulative random widths, normalised so they add up to exactly 1 (no seam at the wrap).
            float total = 0f;
            for (int i = 0; i < n; i++) total += 1f + (Hash01(i, 0, seed) * 2f - 1f) * jitter;
            float start = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (1f + (Hash01(i, 0, seed) * 2f - 1f) * jitter) / total;
                if (t < start + w || i == n - 1)
                {
                    width = w;
                    local = Saturate((t - start) / w);
                    return i;
                }
                start += w;
            }
            width = 1f / n;
            local = 0f;
            return 0;
        }

        // ------------------------------------------------------------------ values and colours

        public static float Remap(float x, float a, float b, float c, float d) => c + (d - c) * Saturate((x - a) / (b - a));
        public static float Pow2(float x) => x * x;
        public static float Signed(float n01) => n01 * 2f - 1f;

        /// <summary>Linear mix of three colours over t 0..1 (a at 0, b at 0.5, c at 1).</summary>
        public static Color Lerp3(Color a, Color b, Color c, float t) => t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, t * 2f - 1f);

        /// <summary>Multiply a colour's brightness by k without touching alpha.</summary>
        public static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Tints toward warm (k &gt; 0) or cool (k &lt; 0) by a small amount, keeping brightness about the same.</summary>
        public static Color Warm(Color c, float k) => new Color(c.r * (1f + k), c.g * (1f + k * 0.2f), c.b * (1f - k), c.a);

        /// <summary>A grey (or tinted) value as a colour.</summary>
        public static Color Grey(float g) => new Color(g, g, g, 1f);

        // ------------------------------------------------------------------ 3D noise (for shaping meshes; not tileable)

        static float Hash3(int x, int y, int z, int seed) => Hash01(x * 7919 + z * 104729, y, seed);

        /// <summary>Smooth 3D value noise 0..1 (rocks and other generated meshes).</summary>
        public static float Value3(float x, float y, float z, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
            float fx = Fade(x - x0), fy = Fade(y - y0), fz = Fade(z - z0);
            float c000 = Hash3(x0, y0, z0, seed), c100 = Hash3(x0 + 1, y0, z0, seed);
            float c010 = Hash3(x0, y0 + 1, z0, seed), c110 = Hash3(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash3(x0, y0, z0 + 1, seed), c101 = Hash3(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash3(x0, y0 + 1, z0 + 1, seed), c111 = Hash3(x0 + 1, y0 + 1, z0 + 1, seed);
            float a = Mathf.Lerp(Mathf.Lerp(c000, c100, fx), Mathf.Lerp(c010, c110, fx), fy);
            float b = Mathf.Lerp(Mathf.Lerp(c001, c101, fx), Mathf.Lerp(c011, c111, fx), fy);
            return Mathf.Lerp(a, b, fz);
        }

        /// <summary>Fractal 3D value noise 0..1.</summary>
        public static float Fbm3(float x, float y, float z, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value3(x * f, y * f, z * f, seed + i * 977) * amp;
                norm += amp;
                amp *= gain;
                f *= 2f;
            }
            return sum / norm;
        }
    }
}
