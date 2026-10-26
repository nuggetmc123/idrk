using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Paints a car's livery texture at runtime: base colour, a pattern, pinstripes, the race number and the team's
    /// (fictional) sponsor names, laid out on the UV regions the car model uses. Text uses a stroked sign-writer
    /// font, so numbers come out crisp at any size.
    /// </summary>
    public static class LiveryPainter
    {
        public const int W = 1024, H = 512;
        // UV regions (x, y, w, h in 0..1)
        public static readonly Rect Chassis = new Rect(0f, 0.5f, 0.5f, 0.5f);
        public static readonly Rect Cover = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
        public static readonly Rect Pods = new Rect(0f, 0f, 0.5f, 0.5f);
        public static readonly Rect Wings = new Rect(0.5f, 0f, 0.25f, 0.25f);
        public static readonly Rect Plates = new Rect(0.75f, 0f, 0.25f, 0.25f);
        public static readonly Rect Helmet = new Rect(0.5f, 0.25f, 0.25f, 0.25f);
        public static readonly Rect Fin = new Rect(0.75f, 0.25f, 0.25f, 0.25f);

        static Color32[] px;

        public static Texture2D Paint(CarSpec spec, CarSetup setup, string sponsor, string sponsor2, Texture2D reuse = null)
        {
            Color p = setup.Primary(spec), s = setup.Secondary(spec), a = setup.Accent(spec);
            int pattern = setup.Livery(spec);
            int number = setup.Number(spec);
            px = new Color32[W * H];

            // Region by region: u along the car (0 = nose / front), angle around (0.5 = top).
            PaintRegion(Chassis, (u, v) => Pattern(pattern, u, v, p, s, a, 0));
            PaintRegion(Cover, (u, v) => Pattern(pattern, Mathf.Lerp(0.45f, 1f, u), v, p, s, a, 1));
            PaintRegion(Pods, (u, v) => Pattern(pattern, Mathf.Lerp(0.35f, 0.8f, u), v, p, s, a, 2));
            PaintRegion(Wings, (u, v) => WingColor(pattern, u, v, p, s, a));
            PaintRegion(Plates, (u, v) => v > 0.82f ? a : v > 0.74f ? s : p);
            PaintRegion(Helmet, (u, v) => HelmetColor(u, v, p, s, a));
            PaintRegion(Fin, (u, v) => v < 0.3f ? s : p);

            Color ink = Contrast(p, s);
            // Race number: top of the nose and both sides of the engine-cover fin.
            string num = number.ToString();
            Text(Chassis, 0.17f, 0.5f, 0.2f, num, ink, rotate: true, flipU: false);
            Text(Fin, 0.5f, 0.6f, 0.45f, num, Contrast(p, a), rotate: false, flipU: false);
            // Sponsor on the sidepods (both sides read left to right from the outside).
            Color sponsorInk = Contrast(PodColorAt(pattern, p, s, a), a);
            Text(Pods, 0.5f, 0.29f, 0.11f, sponsor, sponsorInk, rotate: false, flipU: true);
            Text(Pods, 0.5f, 0.71f, 0.11f, sponsor, sponsorInk, rotate: false, flipU: false, flipV: true);
            // Secondary sponsor along the engine cover sides, and on the endplates.
            if (!string.IsNullOrEmpty(sponsor2))
            {
                Text(Cover, 0.42f, 0.32f, 0.085f, sponsor2, ink, rotate: false, flipU: true);
                Text(Cover, 0.42f, 0.68f, 0.085f, sponsor2, ink, rotate: false, flipU: false, flipV: true);
                Text(Plates, 0.5f, 0.4f, 0.22f, sponsor2, Contrast(p, s), rotate: false, flipU: false);
            }
            Text(Wings, 0.5f, 0.5f, 0.3f, sponsor, Contrast(WingColor(pattern, 0.5f, 0.5f, p, s, a), a), rotate: false, flipU: false);

            var tex = reuse;
            if (tex == null || tex.width != W || tex.height != H)
            {
                tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Livery", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            px = null;
            return tex;
        }

        static Color PodColorAt(int pattern, Color p, Color s, Color a)
        {
            switch (pattern)
            {
                case 2: return p;
                case 3: return Color.Lerp(p, s, 0.55f);
                default: return p;
            }
        }

        delegate Color Shade(float u, float v);

        static void PaintRegion(Rect r, Shade f)
        {
            int x0 = Mathf.RoundToInt(r.x * W), y0 = Mathf.RoundToInt(r.y * H);
            int w = Mathf.RoundToInt(r.width * W), h = Mathf.RoundToInt(r.height * H);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    px[(y0 + y) * W + x0 + x] = f(u, v);
                }
            }
        }

        /// <summary>u: 0 nose .. 1 tail. v: 0 bottom, 0.25 right side, 0.5 top, 0.75 left side, 1 bottom.</summary>
        static Color Pattern(int pattern, float u, float v, Color p, Color s, Color a, int region)
        {
            float top = 1f - Mathf.Abs(v - 0.5f) * 2f;          // 1 on top, 0 underneath
            float side = 1f - Mathf.Abs(Mathf.Abs(v - 0.5f) - 0.25f) * 4f; // 1 on the flanks
            bool under = top < 0.32f;
            if (under) return Color.Lerp(p, Color.black, 0.55f); // floor-facing surfaces darker, like real paint on carbon
            switch (pattern)
            {
                case 0: // Classic stripe: wide stripe down the spine with pinstripes either side
                {
                    float d = Mathf.Abs(v - 0.5f);
                    if (d < 0.045f) return s;
                    if (d < 0.055f) return a;
                    return p;
                }
                case 1: // Arrowhead: secondary nose flowing back in a V along the flanks
                {
                    float edge = 0.25f + Mathf.Abs(v - 0.5f) * 1.1f;
                    if (u < edge - 0.02f) return s;
                    if (u < edge) return a;
                    return p;
                }
                case 2: // Split tone: upper body primary, lower flanks secondary, accent line between
                {
                    float d = Mathf.Abs(v - 0.5f);
                    if (d > 0.21f) return s;
                    if (d > 0.195f) return a;
                    return p;
                }
                case 3: // Fade: primary to secondary along the car with a fine dither band
                {
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, u + (v - 0.5f) * 0.15f));
                    float dither = Mathf.Repeat(u * 140f + v * 60f, 1f) < t ? 1f : 0f;
                    return Color.Lerp(p, s, Mathf.Abs(t - 0.5f) < 0.12f ? dither : t);
                }
                case 4: // Twin stripes
                {
                    float d = Mathf.Abs(v - 0.5f);
                    if ((d > 0.02f && d < 0.05f) || (d > 0.07f && d < 0.085f)) return s;
                    return p;
                }
                case 5: // Chevron on the flanks
                {
                    float c = Mathf.Repeat(u * 4f + Mathf.Abs(v - 0.5f) * 3f, 1f);
                    if (side > 0.25f && c < 0.18f && u > 0.3f) return s;
                    if (side > 0.25f && c < 0.21f && u > 0.3f) return a;
                    return p;
                }
                case 6: // Heritage band: a band right around the car, coloured nose tip
                {
                    if (u < 0.12f) return s;
                    if (u > 0.47f && u < 0.6f) return s;
                    if ((u > 0.45f && u < 0.47f) || (u > 0.6f && u < 0.62f)) return a;
                    return p;
                }
                default: // Shard: angled facets
                {
                    float k = u * 3.2f - v * 1.4f;
                    float f = Mathf.Repeat(k, 1f);
                    if (f < 0.22f && u > 0.25f) return s;
                    if (f < 0.25f && u > 0.25f) return a;
                    return p;
                }
            }
        }

        static Color WingColor(int pattern, float u, float v, Color p, Color s, Color a)
        {
            if (v > 0.85f) return a;
            return pattern == 2 || pattern == 6 ? s : p;
        }

        static Color HelmetColor(float u, float v, Color p, Color s, Color a)
        {
            float band = Mathf.Abs(v - 0.55f);
            if (band < 0.04f) return a;
            if (v > 0.55f) return Color.Lerp(s, p, 0.15f);
            return Color.white * 0.92f;
        }

        static Color Contrast(Color bg, Color preferred)
        {
            float lb = Lum(bg), lp = Lum(preferred);
            if (Mathf.Abs(lb - lp) > 0.35f) return preferred;
            return lb > 0.5f ? new Color(0.06f, 0.06f, 0.07f) : new Color(0.96f, 0.96f, 0.95f);
        }

        static float Lum(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        // ------------------------------------------------------------------ stroked text

        /// <summary>Writes text centred at (uc, vc) of a region; height is a fraction of the region's height.</summary>
        static void Text(Rect r, float uc, float vc, float height, string text, Color col, bool rotate, bool flipU, bool flipV = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.ToUpperInvariant();
            int rx = Mathf.RoundToInt(r.x * W), ry = Mathf.RoundToInt(r.y * H), rw = Mathf.RoundToInt(r.width * W), rh = Mathf.RoundToInt(r.height * H);
            float hPx = height * (rotate ? rw : rh);
            float scale = hPx / 6f;          // glyph grid is 4 wide x 6 tall
            float advance = 5.1f * scale;
            float stroke = Mathf.Max(1.2f, scale * 0.95f);
            float totalW = text.Length * advance - scale * 1.1f;
            var segs = new List<Vector4>();
            float cursor = -totalW * 0.5f;
            foreach (char ch in text)
            {
                if (Glyphs.TryGetValue(ch, out var strokes))
                    foreach (var st in strokes)
                        for (int i = 0; i < st.Length - 1; i++)
                            segs.Add(new Vector4(cursor + st[i].x * scale, (st[i].y - 3f) * scale, cursor + st[i + 1].x * scale, (st[i + 1].y - 3f) * scale));
                cursor += advance;
            }
            float cx = rx + uc * rw, cy = ry + vc * rh;
            float halfW = totalW * 0.5f + stroke * 2f, halfH = hPx * 0.5f + stroke * 2f;
            float bw = rotate ? halfH : halfW, bh = rotate ? halfW : halfH;
            int x0 = Mathf.Max(rx, Mathf.FloorToInt(cx - bw)), x1 = Mathf.Min(rx + rw - 1, Mathf.CeilToInt(cx + bw));
            int y0 = Mathf.Max(ry, Mathf.FloorToInt(cy - bh)), y1 = Mathf.Min(ry + rh - 1, Mathf.CeilToInt(cy + bh));
            Color32 c32 = col;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float lx = x + 0.5f - cx, ly = y + 0.5f - cy;
                float tx, ty;
                if (rotate) { tx = -ly; ty = lx; } else { tx = lx; ty = ly; }
                if (flipU) tx = -tx;
                if (flipV) ty = -ty;
                float d = 1e9f;
                foreach (var sgm in segs)
                {
                    float dd = SegDist(tx, ty, sgm);
                    if (dd < d) d = dd;
                }
                float cov = Mathf.Clamp01(stroke * 0.5f + 0.5f - d);
                if (cov <= 0f) continue;
                int i = y * W + x;
                px[i] = Color32.Lerp(px[i], c32, cov);
            }
        }

        /// <summary>Stroked text into any pixel buffer (used by the editor's texture generator for signs and boards).</summary>
        public static void DrawText(Color32[] pixels, int width, int height, float cx, float cy, float heightPx, string text, Color col, float strokeScale = 0.95f)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.ToUpperInvariant();
            float scale = heightPx / 6f;
            float advance = 5.1f * scale;
            float stroke = Mathf.Max(1.2f, scale * strokeScale);
            float totalW = text.Length * advance - scale * 1.1f;
            var segs = new List<Vector4>();
            float cursor = -totalW * 0.5f;
            foreach (char ch in text)
            {
                if (Glyphs.TryGetValue(ch, out var strokes))
                    foreach (var st in strokes)
                        for (int i = 0; i < st.Length - 1; i++)
                            segs.Add(new Vector4(cursor + st[i].x * scale, (st[i].y - 3f) * scale, cursor + st[i + 1].x * scale, (st[i + 1].y - 3f) * scale));
                cursor += advance;
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - totalW * 0.5f - stroke * 2f)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(cx + totalW * 0.5f + stroke * 2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - heightPx * 0.5f - stroke * 2f)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(cy + heightPx * 0.5f + stroke * 2f));
            Color32 c32 = col;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float tx = x + 0.5f - cx, ty = y + 0.5f - cy;
                float d = 1e9f;
                foreach (var sgm in segs) { float dd = SegDist(tx, ty, sgm); if (dd < d) d = dd; }
                float cov = Mathf.Clamp01(stroke * 0.5f + 0.5f - d);
                if (cov <= 0f) continue;
                int i = y * width + x;
                var o = pixels[i];
                pixels[i] = new Color32((byte)Mathf.Lerp(o.r, c32.r, cov), (byte)Mathf.Lerp(o.g, c32.g, cov), (byte)Mathf.Lerp(o.b, c32.b, cov), (byte)Mathf.Max(o.a, c32.a * cov));
            }
        }

        static float SegDist(float x, float y, Vector4 s)
        {
            float ax = s.x, ay = s.y, bx = s.z, by = s.w;
            float dx = bx - ax, dy = by - ay;
            float l2 = dx * dx + dy * dy;
            float t = l2 > 1e-6f ? Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / l2) : 0f;
            float px_ = ax + t * dx - x, py = ay + t * dy - y;
            return Mathf.Sqrt(px_ * px_ + py * py);
        }

        static Vector2 P(float x, float y) => new Vector2(x, y);

        // A clean sign-writer alphabet on a 4 x 6 grid (y up).
        static readonly Dictionary<char, Vector2[][]> Glyphs = new Dictionary<char, Vector2[][]>
        {
            ['0'] = new[] { new[] { P(1, 0), P(3, 0), P(4, 1), P(4, 5), P(3, 6), P(1, 6), P(0, 5), P(0, 1), P(1, 0) } },
            ['1'] = new[] { new[] { P(0.8f, 4.8f), P(2.4f, 6), P(2.4f, 0) }, new[] { P(1, 0), P(3.6f, 0) } },
            ['2'] = new[] { new[] { P(0, 5), P(1, 6), P(3, 6), P(4, 5), P(4, 3.8f), P(0, 0.6f), P(0, 0), P(4, 0) } },
            ['3'] = new[] { new[] { P(0, 5.2f), P(1, 6), P(3, 6), P(4, 5), P(4, 4), P(3, 3.1f), P(1.6f, 3.1f) }, new[] { P(3, 3.1f), P(4, 2.2f), P(4, 1), P(3, 0), P(1, 0), P(0, 0.8f) } },
            ['4'] = new[] { new[] { P(3, 0), P(3, 6), P(0, 1.8f), P(0, 1.5f), P(4, 1.5f) } },
            ['5'] = new[] { new[] { P(4, 6), P(0.3f, 6), P(0, 3.4f), P(2.8f, 3.6f), P(4, 2.6f), P(4, 1), P(3, 0), P(1, 0), P(0, 0.8f) } },
            ['6'] = new[] { new[] { P(3.6f, 5.6f), P(2.6f, 6), P(1, 6), P(0, 5), P(0, 1), P(1, 0), P(3, 0), P(4, 1), P(4, 2.5f), P(3, 3.5f), P(1, 3.5f), P(0, 2.6f) } },
            ['7'] = new[] { new[] { P(0, 6), P(4, 6), P(4, 5.4f), P(1.5f, 0) } },
            ['8'] = new[] { new[] { P(1, 3.2f), P(0.2f, 4.1f), P(0.2f, 5.1f), P(1, 6), P(3, 6), P(3.8f, 5.1f), P(3.8f, 4.1f), P(3, 3.2f), P(1, 3.2f), P(0, 2.2f), P(0, 1), P(1, 0), P(3, 0), P(4, 1), P(4, 2.2f), P(3, 3.2f) } },
            ['9'] = new[] { new[] { P(0.4f, 0.4f), P(1.4f, 0), P(3, 0), P(4, 1), P(4, 5), P(3, 6), P(1, 6), P(0, 5), P(0, 3.5f), P(1, 2.5f), P(3, 2.5f), P(4, 3.4f) } },
            ['A'] = new[] { new[] { P(0, 0), P(0, 4), P(2, 6), P(4, 4), P(4, 0) }, new[] { P(0, 2.4f), P(4, 2.4f) } },
            ['B'] = new[] { new[] { P(0, 0), P(0, 6), P(3, 6), P(4, 5), P(4, 4), P(3, 3.2f), P(0, 3.2f) }, new[] { P(3, 3.2f), P(4, 2.3f), P(4, 1), P(3, 0), P(0, 0) } },
            ['C'] = new[] { new[] { P(4, 5), P(3, 6), P(1, 6), P(0, 5), P(0, 1), P(1, 0), P(3, 0), P(4, 1) } },
            ['D'] = new[] { new[] { P(0, 0), P(0, 6), P(2.6f, 6), P(4, 4.6f), P(4, 1.4f), P(2.6f, 0), P(0, 0) } },
            ['E'] = new[] { new[] { P(4, 6), P(0, 6), P(0, 0), P(4, 0) }, new[] { P(0, 3.1f), P(3, 3.1f) } },
            ['F'] = new[] { new[] { P(4, 6), P(0, 6), P(0, 0) }, new[] { P(0, 3.1f), P(3, 3.1f) } },
            ['G'] = new[] { new[] { P(4, 5), P(3, 6), P(1, 6), P(0, 5), P(0, 1), P(1, 0), P(3, 0), P(4, 1), P(4, 2.8f), P(2.2f, 2.8f) } },
            ['H'] = new[] { new[] { P(0, 0), P(0, 6) }, new[] { P(4, 0), P(4, 6) }, new[] { P(0, 3.1f), P(4, 3.1f) } },
            ['I'] = new[] { new[] { P(2, 0), P(2, 6) }, new[] { P(0.8f, 6), P(3.2f, 6) }, new[] { P(0.8f, 0), P(3.2f, 0) } },
            ['J'] = new[] { new[] { P(4, 6), P(4, 1), P(3, 0), P(1, 0), P(0, 1) } },
            ['K'] = new[] { new[] { P(0, 0), P(0, 6) }, new[] { P(4, 6), P(0.2f, 2.6f) }, new[] { P(1.5f, 3.6f), P(4, 0) } },
            ['L'] = new[] { new[] { P(0, 6), P(0, 0), P(4, 0) } },
            ['M'] = new[] { new[] { P(0, 0), P(0, 6), P(2, 3.2f), P(4, 6), P(4, 0) } },
            ['N'] = new[] { new[] { P(0, 0), P(0, 6), P(4, 0), P(4, 6) } },
            ['O'] = new[] { new[] { P(1, 0), P(3, 0), P(4, 1), P(4, 5), P(3, 6), P(1, 6), P(0, 5), P(0, 1), P(1, 0) } },
            ['P'] = new[] { new[] { P(0, 0), P(0, 6), P(3, 6), P(4, 5), P(4, 3.8f), P(3, 2.8f), P(0, 2.8f) } },
            ['Q'] = new[] { new[] { P(1, 0), P(3, 0), P(4, 1), P(4, 5), P(3, 6), P(1, 6), P(0, 5), P(0, 1), P(1, 0) }, new[] { P(2.6f, 1.4f), P(4.2f, -0.4f) } },
            ['R'] = new[] { new[] { P(0, 0), P(0, 6), P(3, 6), P(4, 5), P(4, 3.9f), P(3, 3f), P(0, 3f) }, new[] { P(2.2f, 3f), P(4, 0) } },
            ['S'] = new[] { new[] { P(4, 5), P(3, 6), P(1, 6), P(0, 5), P(0, 4), P(1, 3.1f), P(3, 3.1f), P(4, 2.2f), P(4, 1), P(3, 0), P(1, 0), P(0, 1) } },
            ['T'] = new[] { new[] { P(0, 6), P(4, 6) }, new[] { P(2, 6), P(2, 0) } },
            ['U'] = new[] { new[] { P(0, 6), P(0, 1), P(1, 0), P(3, 0), P(4, 1), P(4, 6) } },
            ['V'] = new[] { new[] { P(0, 6), P(2, 0), P(4, 6) } },
            ['W'] = new[] { new[] { P(0, 6), P(0.8f, 0), P(2, 3.6f), P(3.2f, 0), P(4, 6) } },
            ['X'] = new[] { new[] { P(0, 6), P(4, 0) }, new[] { P(4, 6), P(0, 0) } },
            ['Y'] = new[] { new[] { P(0, 6), P(2, 3) , P(4, 6) }, new[] { P(2, 3), P(2, 0) } },
            ['Z'] = new[] { new[] { P(0, 6), P(4, 6), P(0, 0), P(4, 0) } },
            ['-'] = new[] { new[] { P(0.8f, 3), P(3.2f, 3) } },
            ['.'] = new[] { new[] { P(1.8f, 0.1f), P(2.2f, 0.1f) } },
            ['&'] = new[] { new[] { P(4, 0), P(1, 4), P(1, 5.2f), P(1.8f, 6), P(2.6f, 5.2f), P(2.6f, 4.4f), P(0, 2), P(0, 1), P(1, 0), P(2.4f, 0), P(4, 2) } },
        };
    }
}
