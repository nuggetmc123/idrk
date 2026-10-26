using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Deterministic gradient noise used by the texture generator (editor) and the terrain builder (runtime).
    /// Everything can tile: pass a period and the noise wraps exactly, so textures repeat without seams.
    /// </summary>
    public static class Noise
    {
        static readonly int[] perm = BuildPerm(1337);

        static int[] BuildPerm(int seed)
        {
            var p = new int[512];
            var r = new System.Random(seed);
            var src = new int[256];
            for (int i = 0; i < 256; i++) src[i] = i;
            for (int i = 255; i > 0; i--) { int j = r.Next(i + 1); int t = src[i]; src[i] = src[j]; src[j] = t; }
            for (int i = 0; i < 512; i++) p[i] = src[i & 255];
            return p;
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        static int Wrap(int v, int period) => period > 0 ? ((v % period) + period) % period : v;

        /// <summary>Perlin noise in roughly -1..1. period (in noise cells) > 0 makes it tile.</summary>
        public static float Perlin(float x, float y, int period = 0, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            int x0 = Wrap(xi, period) + seed * 17, x1 = Wrap(xi + 1, period) + seed * 17;
            int y0 = Wrap(yi, period) + seed * 31, y1 = Wrap(yi + 1, period) + seed * 31;
            int aa = perm[(perm[x0 & 255] + y0) & 255], ab = perm[(perm[x0 & 255] + y1) & 255];
            int ba = perm[(perm[x1 & 255] + y0) & 255], bb = perm[(perm[x1 & 255] + y1) & 255];
            float u = Fade(xf), v = Fade(yf);
            float l0 = Mathf.Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float l1 = Mathf.Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Mathf.Lerp(l0, l1, v) * 0.7071f;
        }

        /// <summary>Fractal sum of octaves, roughly -1..1.</summary>
        public static float Fbm(float x, float y, int octaves, int period = 0, int seed = 0, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int p = period;
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(x, y, p, seed + i) * amp;
                norm += amp;
                amp *= gain;
                x *= lacunarity; y *= lacunarity;
                if (p > 0) p = Mathf.RoundToInt(p * lacunarity);
            }
            return sum / norm;
        }

        /// <summary>Ridged multifractal (sharp crests, for mountains and rock), 0..1.</summary>
        public static float Ridged(float x, float y, int octaves, int period = 0, int seed = 0)
        {
            float sum = 0f, amp = 0.5f, weight = 1f;
            int p = period;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin(x, y, p, seed + i) * 1.41f);
                n *= n;
                n *= weight;
                weight = Mathf.Clamp01(n * 2f);
                sum += n * amp;
                amp *= 0.5f;
                x *= 2f; y *= 2f;
                if (p > 0) p *= 2;
            }
            return Mathf.Clamp01(sum * 1.1f);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Cellular noise: distance to the nearest and second-nearest feature point (cells tile with period).</summary>
        public static Vector2 Worley(float x, float y, int period = 0, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float d1 = 9f, d2 = 9f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int cx = xi + ox, cy = yi + oy;
                int hx = Wrap(cx, period), hy = Wrap(cy, period);
                float px = cx + Hash(hx, hy, seed), py = cy + Hash(hx, hy, seed + 7);
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < d1) { d2 = d1; d1 = d; }
                else if (d < d2) d2 = d;
            }
            return new Vector2(Mathf.Sqrt(d1), Mathf.Sqrt(d2));
        }

        /// <summary>Random value 0..1 for an integer cell (stable).</summary>
        public static float Cell(int x, int y, int seed = 0) => Hash(x, y, seed);
    }

    /// <summary>A small seeded random source that doesn't touch UnityEngine.Random's global state.</summary>
    public class Rng
    {
        System.Random r;
        public Rng(int seed) { r = new System.Random(seed); }
        public float Value => (float)r.NextDouble();
        public float Range(float a, float b) => a + (b - a) * (float)r.NextDouble();
        public int Range(int a, int bExclusive) => r.Next(a, bExclusive);
        public bool Chance(float p) => r.NextDouble() < p;
        public Vector2 InCircle() { float a = Range(0f, Mathf.PI * 2f), d = Mathf.Sqrt(Value); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d; }
        public T Pick<T>(T[] items) => items[r.Next(items.Length)];
    }
}
