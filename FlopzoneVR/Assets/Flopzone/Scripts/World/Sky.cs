using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Flopzone
{
    public enum SkyStyle { Day, Sunset, Night, Overcast, Haze, Space }

    /// <summary>
    /// Paints a 360° sky (clouds, sun glow, stars, the Milky Way) into a
    /// panorama texture on a worker thread, for the Skybox/Panoramic shader.
    /// </summary>
    public static class Sky
    {
        public const int W = 1024, H = 512;

        public class Spec
        {
            public SkyStyle style;
            public Color zenith, horizon, ground, sun, cloud;
            public Vector3 sunDir;   // towards the sun
            public float clouds;     // 0..1 cover
            public int seed;
        }

        public static Spec Make(SkyStyle style, Vector3 sunDir, int seed)
        {
            var s = new Spec { style = style, sunDir = sunDir.normalized, seed = seed };
            switch (style)
            {
                case SkyStyle.Sunset:
                    s.zenith = new Color(0.16f, 0.2f, 0.42f); s.horizon = new Color(1f, 0.55f, 0.3f); s.ground = new Color(0.25f, 0.18f, 0.2f);
                    s.sun = new Color(1f, 0.7f, 0.4f); s.cloud = new Color(1f, 0.62f, 0.5f); s.clouds = 0.45f; break;
                case SkyStyle.Night:
                    s.zenith = new Color(0.02f, 0.02f, 0.07f); s.horizon = new Color(0.16f, 0.08f, 0.25f); s.ground = new Color(0.03f, 0.03f, 0.05f);
                    s.sun = new Color(0.8f, 0.85f, 1f); s.cloud = new Color(0.18f, 0.12f, 0.28f); s.clouds = 0.3f; break;
                case SkyStyle.Overcast:
                    s.zenith = new Color(0.5f, 0.55f, 0.62f); s.horizon = new Color(0.75f, 0.78f, 0.82f); s.ground = new Color(0.4f, 0.4f, 0.42f);
                    s.sun = new Color(1f, 0.97f, 0.9f); s.cloud = new Color(0.82f, 0.84f, 0.88f); s.clouds = 0.85f; break;
                case SkyStyle.Haze:
                    s.zenith = new Color(0.35f, 0.55f, 0.85f); s.horizon = new Color(0.95f, 0.85f, 0.68f); s.ground = new Color(0.75f, 0.62f, 0.45f);
                    s.sun = new Color(1f, 0.95f, 0.8f); s.cloud = new Color(1f, 0.96f, 0.9f); s.clouds = 0.15f; break;
                case SkyStyle.Space:
                    s.zenith = Color.black; s.horizon = new Color(0.02f, 0.02f, 0.04f); s.ground = Color.black;
                    s.sun = Color.white; s.cloud = Color.black; s.clouds = 0f; break;
                default:
                    s.zenith = new Color(0.2f, 0.42f, 0.85f); s.horizon = new Color(0.72f, 0.85f, 0.98f); s.ground = new Color(0.45f, 0.48f, 0.45f);
                    s.sun = new Color(1f, 0.97f, 0.88f); s.cloud = Color.white; s.clouds = 0.4f; break;
            }
            return s;
        }

        public static Task<Color32[]> Paint(Spec s) => Task.Run(() => PaintNow(s));

        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        static float Noise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        static float Fbm(float x, float y, int seed, int oct)
        {
            float sum = 0, amp = 0.5f, f = 1, norm = 0;
            for (int i = 0; i < oct; i++) { sum += Noise(x * f, y * f, seed + i * 17) * amp; norm += amp; amp *= 0.5f; f *= 2.03f; }
            return sum / norm;
        }

        static Color32[] PaintNow(Spec s)
        {
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
                float sinLat = (float)Math.Sin(lat), cosLat = (float)Math.Cos(lat);
                for (int x = 0; x < W; x++)
                {
                    float lon = ((x + 0.5f) / W - 0.5f) * Mathf.PI * 2;
                    var dir = new Vector3(cosLat * (float)Math.Sin(lon), sinLat, cosLat * (float)Math.Cos(lon));
                    px[y * W + x] = Shade(s, dir, x, y);
                }
            }
            return px;
        }

        static Color32 Shade(Spec s, Vector3 d, int x, int y)
        {
            float up = d.y;
            Color c;
            if (up >= 0)
            {
                float t = (float)Math.Pow(up, 0.45);
                c = Color.Lerp(s.horizon, s.zenith, t);
            }
            else c = Color.Lerp(s.horizon, s.ground, Mathf.Clamp01(-up * 6f));

            float sd = Mathf.Max(0, Vector3.Dot(d, s.sunDir));
            // sky glow around the sun, brighter near the horizon
            c += s.sun * ((float)Math.Pow(sd, 8) * (s.style == SkyStyle.Space ? 0.05f : 0.35f) + (float)Math.Pow(sd, 64) * 0.4f);
            if (s.style == SkyStyle.Sunset) c += s.sun * (float)Math.Pow(sd, 3) * 0.25f * (1 - Mathf.Clamp01(up * 3));

            // stars (night, space) with a soft band of Milky Way
            if ((s.style == SkyStyle.Night || s.style == SkyStyle.Space) && up > -0.05f)
            {
                float h = Hash(x, y, s.seed);
                if (h > 0.9965f) { float b = (h - 0.9965f) / 0.0035f; c += Color.white * (0.4f + b * 0.8f); }
                float band = Mathf.Exp(-Mathf.Pow(Vector3.Dot(d, new Vector3(0.3f, 0.5f, 0.81f).normalized) * 3.2f, 2));
                float neb = Fbm(d.x * 4 + 10, d.z * 4 + d.y * 3, s.seed + 5, 4);
                c += new Color(0.35f, 0.3f, 0.5f) * band * neb * (s.style == SkyStyle.Space ? 0.55f : 0.25f);
                if (Hash(x / 2, y / 2, s.seed + 9) > 0.999f && band > 0.3f) c += Color.white * 0.5f;
            }

            // the sun's disc (or the moon at night)
            if (s.style != SkyStyle.Overcast)
            {
                float disc = Mathf.Clamp01((sd - 0.9993f) / 0.0004f);
                if (s.style == SkyStyle.Night)
                {
                    var moonDir = new Vector3(-s.sunDir.x, Mathf.Abs(s.sunDir.y) + 0.25f, -s.sunDir.z).normalized;
                    float md = Vector3.Dot(d, moonDir);
                    float moon = Mathf.Clamp01((md - 0.9985f) / 0.0006f);
                    float crater = Noise(d.x * 300, d.y * 300, 3) * 0.25f;
                    c = Color.Lerp(c, new Color(0.9f, 0.92f, 1f) * (1 - crater), moon);
                    c += new Color(0.5f, 0.55f, 0.7f) * (float)Math.Pow(Mathf.Max(0, md), 200) * 0.3f;
                }
                else c = Color.Lerp(c, s.sun * 1.5f, disc);
            }

            // clouds, painted onto a flat layer overhead
            if (s.clouds > 0 && up > 0.015f)
            {
                float k = 1f / (up + 0.12f);
                float cx = d.x * k * 1.6f + 7, cz = d.z * k * 1.6f + 3;
                float n = Fbm(cx, cz, s.seed, 5);
                float cover = Mathf.Clamp01((n - (1 - s.clouds) * 0.75f - 0.2f) * 3.2f);
                if (cover > 0)
                {
                    float shade = Fbm(cx + 0.08f, cz + 0.08f, s.seed, 4);
                    float lit = Mathf.Clamp01(0.75f + (n - shade) * 4f + sd * 0.3f);
                    var cc = Color.Lerp(s.cloud * 0.55f + s.zenith * 0.15f, s.cloud, lit);
                    cc += s.sun * (float)Math.Pow(sd, 6) * 0.35f;
                    float fade = Mathf.Clamp01((up - 0.015f) * 6f);
                    c = Color.Lerp(c, cc, cover * fade * (s.style == SkyStyle.Night ? 0.6f : 0.95f));
                }
            }

            // distant haze band on the horizon
            float haze = Mathf.Exp(-Mathf.Abs(up) * 18f) * (s.style == SkyStyle.Haze ? 0.5f : 0.25f);
            c = Color.Lerp(c, s.horizon, haze);
            return new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 255);
        }
    }
}
