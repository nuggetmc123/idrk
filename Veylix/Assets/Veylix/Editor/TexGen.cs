using System.IO;
using UnityEditor;
using UnityEngine;

namespace Veylix.EditorTools
{
    /// <summary>
    /// Generates every texture the game uses as real PNG assets (so Unity mip-maps and compresses them for Quest):
    /// tileable PBR surfaces (albedo + normal) for tarmac, kerbs, run-off, barriers, terrain and buildings; alpha-cut
    /// foliage; facades with lit-window emission maps; crowds, advertising, signs; car materials (carbon weave, tyre
    /// sidewalls per compound, brake discs); particles; and an outline drawing of each of the 25 maps.
    /// Every pattern is built from tileable noise at several scales so nothing looks stamped or repeated.
    /// </summary>
    public static class TexGen
    {
        public const string Folder = "Assets/Veylix/Generated/Textures";

        public delegate void Px(float u, float v, out Color albedo, out float height);

        public struct Pair { public Texture2D albedo, normal, emission; }

        static float N(float u, float v, int f, int oct = 4, int seed = 0) => Noise.Fbm(u * f, v * f, oct, f, seed) * 0.5f + 0.5f;
        static float R(float u, float v, int f, int oct = 4, int seed = 0) => Noise.Ridged(u * f, v * f, oct, f, seed);
        static Vector2 Wo(float u, float v, int f, int seed = 0) => Noise.Worley(u * f, v * f, f, seed);
        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        /// <summary>Make albedo (+ optional normal from the height) and save them.</summary>
        public static Pair Make(string name, int size, Px fn, float normalStrength = 2f, bool alpha = false, bool repeat = true, bool normal = true)
        {
            Directory.CreateDirectory(Folder);
            var albedo = new Color[size * size];
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                fn((x + 0.5f) / size, (y + 0.5f) / size, out Color c, out float h);
                albedo[y * size + x] = c;
                height[y * size + x] = h;
            }
            var pair = new Pair { albedo = Save(name, size, size, albedo, false, alpha, repeat) };
            if (normal && normalStrength > 0f) pair.normal = Save(name + "_N", size, size, NormalFromHeight(height, size, normalStrength, repeat), true, false, repeat);
            return pair;
        }

        public static Color[] NormalFromHeight(float[] h, int size, float strength, bool repeat)
        {
            var n = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int xl = repeat ? (x - 1 + size) % size : Mathf.Max(0, x - 1), xr = repeat ? (x + 1) % size : Mathf.Min(size - 1, x + 1);
                int yd = repeat ? (y - 1 + size) % size : Mathf.Max(0, y - 1), yu = repeat ? (y + 1) % size : Mathf.Min(size - 1, y + 1);
                float dx = (h[y * size + xr] - h[y * size + xl]) * strength * size / 256f;
                float dy = (h[yu * size + x] - h[yd * size + x]) * strength * size / 256f;
                Vector3 v = new Vector3(-dx, -dy, 1f).normalized;
                n[y * size + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
            }
            return n;
        }

        public static Texture2D Save(string name, int w, int h, Color[] px, bool isNormal, bool alpha, bool repeat, int maxSize = 0, bool linear = false)
        {
            string path = Folder + "/" + name + ".png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.anisoLevel = repeat ? 8 : 2;
            imp.alphaIsTransparency = alpha;
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.sRGBTexture = !isNormal && !linear;
            if (alpha) imp.mipMapsPreserveCoverage = true;
            if (maxSize > 0) imp.maxTextureSize = maxSize;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.maxTextureSize = Mathf.Min(maxSize > 0 ? maxSize : 2048, Mathf.Max(w, h));
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Text height (px) that fits a box: the stroke font is about 0.85 em wide per character.</summary>
        static float Fit(string text, float boxW, float boxH) => Mathf.Min(boxH, boxW / Mathf.Max(1, text.Length) / 0.88f);

        static Color Grain(Color c, float u, float v, float amount, int f = 64, int seed = 3)
        {
            float g = (N(u, v, f, 2, seed) - 0.5f) * amount;
            return new Color(c.r + g, c.g + g, c.b + g, c.a);
        }

        // ================================================================== track surfaces

        public static Pair Asphalt(bool track)
        {
            return Make(track ? "Asphalt" : "AsphaltRoad", 1024, (float u, float v, out Color c, out float h) =>
            {
                // Aggregate: small stones of varied tone set in darker binder, plus wear patches and tar seams.
                Vector2 w1 = Wo(u, v, 96, 1), w2 = Wo(u, v, 190, 2);
                float stone = Mathf.Clamp01(1f - w1.x * 2.4f);
                float fine = Mathf.Clamp01(1f - w2.x * 2.2f);
                float tone = Noise.Cell(Mathf.FloorToInt(u * 96f), Mathf.FloorToInt(v * 96f), 7);
                float patch = N(u, v, 3, 4, 5);
                float stain = N(u, v, 6, 3, 8);
                float baseV = track ? 0.13f : 0.16f;
                float g = baseV + stone * (0.05f + tone * 0.08f) + fine * 0.025f + (patch - 0.5f) * (track ? 0.035f : 0.06f) - Mathf.Clamp01(stain - 0.62f) * 0.08f;
                // Repaired strips and cracks on road surfaces.
                if (!track)
                {
                    float crack = Mathf.Clamp01(1f - Mathf.Abs(Noise.Fbm(u * 5f, v * 5f, 4, 5, 9)) * 60f) * Mathf.Clamp01((N(u, v, 2, 2, 11) - 0.55f) * 6f);
                    g -= crack * 0.06f;
                    float seam = Mathf.Abs(Mathf.Repeat(u * 2f + N(u, v, 4, 2, 13) * 0.05f, 1f) - 0.5f) < 0.006f ? 0.04f : 0f;
                    g -= seam;
                }
                c = new Color(g * 1.0f, g * 1.0f, g * 1.04f);
                h = stone * 0.6f + fine * 0.3f + patch * 0.1f;
            }, 3f);
        }

        /// <summary>Rubbered-in racing line, used as a detail map on uv2 (0.5 grey = no change).</summary>
        public static Texture2D RubberLine()
        {
            int w = 256, hgt = 256;
            var px = new Color[w * hgt];
            for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / hgt;
                float d = Mathf.Abs(u - 0.5f) * 2f;
                float band = Mathf.Exp(-d * d * 9f);
                float streak = N(u * 0.25f, v, 32, 3, 4) * 0.6f + N(u, v, 8, 3, 5) * 0.4f;
                float m = 0.5f - band * (0.12f + streak * 0.08f) - (1f - d) * 0.02f;
                px[y * w + x] = new Color(m, m, m, 1f);
            }
            return Save("RubberLine", w, hgt, px, false, false, true, 0, true);
        }

        public static Pair Kerb(Color a, Color b, string name)
        {
            return Make(name, 512, (float u, float v, out Color c, out float h) =>
            {
                // u across the kerb, v along it: two stripes per tile, with rubber and dirt.
                bool first = Mathf.Repeat(v * 2f, 1f) < 0.5f;
                float edge = Mathf.Abs(Mathf.Repeat(v * 2f, 0.5f) - 0.25f) / 0.25f;
                Color col = first ? a : b;
                float rubber = Mathf.Clamp01(N(u, v, 8, 3, 21) - 0.55f) * 0.8f * (1f - u);
                float dirt = Mathf.Clamp01(N(u, v, 16, 3, 22) - 0.6f) * 0.5f;
                col = Color.Lerp(col, new Color(0.06f, 0.06f, 0.06f), rubber);
                col = Color.Lerp(col, new Color(0.35f, 0.3f, 0.25f), dirt);
                col = Grain(col, u, v, 0.05f, 128, 23);
                // Ridges across the kerb surface.
                float ridge = Mathf.Repeat(v * 16f, 1f) < 0.12f ? 0.5f : 0f;
                c = col;
                h = ridge + N(u, v, 64, 2, 24) * 0.3f + (edge > 0.97f ? -0.3f : 0f);
            }, 2.5f);
        }

        public static Pair Runoff()
        {
            return Make("Runoff", 512, (float u, float v, out Color c, out float h) =>
            {
                // Painted run-off: wide green and blue bands over tarmac, paint worn at random.
                float stripe = Mathf.Repeat(u * 3f, 1f);
                Color paint = stripe < 0.5f ? new Color(0.12f, 0.33f, 0.2f) : new Color(0.1f, 0.24f, 0.42f);
                float wear = Mathf.Clamp01((N(u, v, 6, 4, 31) - 0.62f) * 3f);
                Vector2 wo = Wo(u, v, 120, 32);
                Color tarmac = new Color(0.16f, 0.16f, 0.17f) * (0.9f + Mathf.Clamp01(1f - wo.x * 2.5f) * 0.25f);
                c = Grain(Color.Lerp(paint, tarmac, wear), u, v, 0.04f, 128, 33);
                h = Mathf.Clamp01(1f - wo.x * 2.5f) * 0.5f;
            }, 1.5f);
        }

        public static Pair LinePaint()
        {
            return Make("LinePaint", 256, (float u, float v, out Color c, out float h) =>
            {
                float wear = Mathf.Clamp01((N(u, v, 8, 3, 41) - 0.55f) * 2f);
                c = Color.Lerp(new Color(0.92f, 0.92f, 0.9f), new Color(0.35f, 0.35f, 0.35f), wear * 0.5f);
                h = 0.5f;
            }, 0.5f);
        }

        public static Pair Gravel(bool sand)
        {
            return Make(sand ? "SandTrap" : "GravelTrap", 512, (float u, float v, out Color c, out float h) =>
            {
                Vector2 w = Wo(u, v, sand ? 160 : 48, sand ? 51 : 52);
                float stone = Mathf.Clamp01(1f - w.x * 1.9f);
                float tone = Noise.Cell(Mathf.FloorToInt(u * 48f), Mathf.FloorToInt(v * 48f), 53);
                Color baseC = sand ? new Color(0.76f, 0.66f, 0.5f) : new Color(0.62f, 0.58f, 0.52f);
                c = Grain(baseC * (0.75f + stone * 0.3f + tone * 0.1f), u, v, 0.05f);
                c.a = 1f;
                h = stone + (w.y - w.x) * 0.5f;
            }, sand ? 1.5f : 4f);
        }

        public static Pair Concrete(string name, Color tint, bool stripes)
        {
            return Make(name, 512, (float u, float v, out Color c, out float h) =>
            {
                float n = N(u, v, 8, 5, 61), spots = Mathf.Clamp01(1f - Wo(u, v, 64, 62).x * 4f);
                Color col = tint * (0.85f + n * 0.25f - spots * 0.08f);
                // Rain streaks running down.
                float streak = Mathf.Clamp01(N(u * 4f, v * 0.3f, 8, 3, 63) - 0.55f) * (1f - v) * 0.4f;
                col = Color.Lerp(col, col * 0.6f, streak);
                if (stripes)
                {
                    // Barrier wall with a red/white band at the top and rubber scuffs low down.
                    if (v > 0.82f) col = Mathf.Repeat(u * 2f, 1f) < 0.5f ? new Color(0.82f, 0.1f, 0.08f) : new Color(0.92f, 0.92f, 0.9f);
                    float scuff = Mathf.Clamp01(N(u, v, 12, 3, 64) - 0.6f) * Mathf.Clamp01(0.45f - v) * 2f;
                    col = Color.Lerp(col, new Color(0.08f, 0.08f, 0.08f), scuff);
                }
                c = col; c.a = 1f;
                h = n * 0.4f + spots * 0.2f;
            }, 1.5f);
        }

        public static Pair Armco()
        {
            return Make("Armco", 512, (float u, float v, out Color c, out float h) =>
            {
                // Galvanised W-beam: two corrugations; posts every tile; rust and dirt low down.
                float wave = Mathf.Sin(v * Mathf.PI * 2f * 2f);
                float post = Mathf.Abs(u - 0.5f) < 0.02f ? 1f : 0f;
                float rust = Mathf.Clamp01(N(u, v, 10, 4, 71) - 0.66f) * 1.5f;
                Color metal = new Color(0.62f, 0.64f, 0.66f) * (0.85f + wave * 0.12f + N(u, v, 32, 2, 72) * 0.1f);
                metal = Color.Lerp(metal, new Color(0.45f, 0.28f, 0.16f), rust);
                if (post > 0f) metal *= 0.6f;
                c = metal; c.a = 1f;
                h = wave * 0.5f + 0.5f;
            }, 3f);
        }

        public static Pair TyreWall()
        {
            return Make("TyreWall", 512, (float u, float v, out Color c, out float h) =>
            {
                // Stacked tyres behind a conveyor-belt cover with coloured bands.
                float band = Mathf.Repeat(v * 2f, 1f);
                Color belt = v > 0.5f ? new Color(0.85f, 0.1f, 0.08f) : new Color(0.92f, 0.92f, 0.9f);
                float bolt = Mathf.Repeat(u * 4f, 1f) < 0.03f && band > 0.45f && band < 0.55f ? 1f : 0f;
                float wear = N(u, v, 12, 3, 81);
                c = Grain(Color.Lerp(belt, belt * 0.55f, Mathf.Clamp01(wear - 0.6f) * 1.6f), u, v, 0.05f);
                if (bolt > 0f) c = new Color(0.3f, 0.3f, 0.3f);
                c.a = 1f;
                h = Mathf.Sin(u * Mathf.PI * 8f) * 0.3f + 0.5f + bolt * 0.3f;
            }, 2f);
        }

        public static Texture2D Fence()
        {
            int s = 256;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                // Diamond chain-link mesh.
                float a = Mathf.Abs(Mathf.Repeat(u * 8f + v * 8f, 1f) - 0.5f), b = Mathf.Abs(Mathf.Repeat(u * 8f - v * 8f, 1f) - 0.5f);
                float wire = Mathf.Max(Mathf.Clamp01(1f - a * 22f), Mathf.Clamp01(1f - b * 22f));
                float rail = Mathf.Abs(v - 0.02f) < 0.015f || Mathf.Abs(v - 0.98f) < 0.015f ? 1f : 0f;
                float alpha = Mathf.Max(wire, rail);
                px[y * s + x] = new Color(0.45f, 0.46f, 0.47f, alpha);
            }
            return Save("Fence", s, s, px, false, true, true);
        }

        public static Texture2D Chequer()
        {
            int s = 128;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool w = ((x / 16) + (y / 16)) % 2 == 0;
                px[y * s + x] = w ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.05f, 0.05f, 0.05f);
            }
            return Save("Chequer", s, s, px, false, false, true);
        }

        public static Pair Grass(string name, Color a, Color b, int seed)
        {
            return Make(name, 512, (float u, float v, out Color c, out float h) =>
            {
                float blades = N(u, v, 128, 2, seed) * 0.6f + N(u, v, 256, 1, seed + 1) * 0.4f;
                float patches = N(u, v, 4, 4, seed + 2);
                float clumps = Mathf.Clamp01(1f - Wo(u, v, 24, seed + 3).x * 2f);
                Color col = Color.Lerp(a, b, Mathf.Clamp01(patches * 1.3f - 0.15f));
                col *= 0.78f + blades * 0.35f + clumps * 0.08f;
                c = col; c.a = 1f;
                h = blades * 0.7f + clumps * 0.3f;
            }, 2f);
        }

        // ================================================================== terrain layers

        public static Pair Rock(string name, Color tint, int seed)
        {
            return Make(name, 512, (float u, float v, out Color c, out float h) =>
            {
                float r = R(u, v, 4, 6, seed);
                float strata = Mathf.Sin((v + N(u, v, 3, 3, seed + 1) * 0.25f) * Mathf.PI * 18f) * 0.5f + 0.5f;
                float cracks = Mathf.Clamp01(1f - Wo(u, v, 10, seed + 2).x * 6f);
                float lichen = Mathf.Clamp01(N(u, v, 12, 3, seed + 3) - 0.62f) * 2f;
                Color col = tint * (0.7f + r * 0.35f + strata * 0.08f - cracks * 0.15f);
                col = Color.Lerp(col, new Color(0.5f, 0.52f, 0.35f), lichen * 0.35f);
                c = col; c.a = 1f;
                h = r * 0.7f + strata * 0.1f - cracks * 0.3f;
            }, 5f);
        }

        public static Pair Dirt(string name, Color tint, int seed, float pebbles)
        {
            return Make(name, 512, (float u, float v, out Color c, out float h) =>
            {
                float n = N(u, v, 6, 5, seed);
                Vector2 w = Wo(u, v, 60, seed + 1);
                float peb = Mathf.Clamp01(1f - w.x * 2.6f) * pebbles;
                float dark = Mathf.Clamp01(N(u, v, 20, 3, seed + 2) - 0.55f);
                Color col = tint * (0.8f + n * 0.3f - dark * 0.2f) + new Color(0.06f, 0.06f, 0.06f) * peb;
                c = col; c.a = 1f;
                h = n * 0.5f + peb * 0.6f;
            }, 2.5f);
        }

        public static Pair Snow()
        {
            return Make("Snow", 512, (float u, float v, out Color c, out float h) =>
            {
                float n = N(u, v, 8, 5, 91), sparkle = Mathf.Clamp01(N(u, v, 256, 1, 92) - 0.82f) * 3f;
                float drift = N(u, v, 3, 3, 93);
                c = new Color(0.9f, 0.92f, 0.96f) * (0.9f + n * 0.12f - drift * 0.05f) + Color.white * sparkle * 0.08f;
                c.a = 1f;
                h = n * 0.6f + drift * 0.4f;
            }, 1.2f);
        }

        public static Pair ForestFloor()
        {
            return Make("ForestFloor", 512, (float u, float v, out Color c, out float h) =>
            {
                // Needles, leaf litter and moss.
                float needles = Mathf.Abs(Noise.Perlin(u * 200f, v * 40f, 40, 101)) * 2f;
                float leaves = Mathf.Clamp01(1f - Wo(u, v, 40, 102).x * 3f);
                float moss = Mathf.Clamp01(N(u, v, 6, 4, 103) - 0.5f) * 2f;
                Color col = Color.Lerp(new Color(0.28f, 0.2f, 0.12f), new Color(0.42f, 0.3f, 0.16f), leaves);
                col = Color.Lerp(col, new Color(0.22f, 0.32f, 0.12f), moss * 0.6f) * (0.8f + needles * 0.2f);
                c = col; c.a = 1f;
                h = leaves * 0.5f + needles * 0.3f;
            }, 2.5f);
        }

        // ================================================================== vegetation

        /// <summary>A spray of leaves or needles on transparent background.</summary>
        public static Texture2D Foliage(string name, int kind, Color baseCol, int seed)
        {
            int s = 512;
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
            var rng = new System.Random(seed);
            float Rf() => (float)rng.NextDouble();
            void Stamp(float cx, float cy, float rx, float ry, float ang, Color col)
            {
                int x0 = Mathf.Max(0, (int)((cx - Mathf.Max(rx, ry)) * s) - 1), x1 = Mathf.Min(s - 1, (int)((cx + Mathf.Max(rx, ry)) * s) + 1);
                int y0 = Mathf.Max(0, (int)((cy - Mathf.Max(rx, ry)) * s) - 1), y1 = Mathf.Min(s - 1, (int)((cy + Mathf.Max(rx, ry)) * s) + 1);
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f) / s - cx, dy = (y + 0.5f) / s - cy;
                    float lx = (dx * ca + dy * sa) / rx, ly = (-dx * sa + dy * ca) / ry;
                    float d = lx * lx + ly * ly;
                    if (d > 1f) continue;
                    float shade = 0.75f + 0.25f * (1f - d) + ly * 0.1f;
                    var o = px[y * s + x];
                    px[y * s + x] = new Color(col.r * shade, col.g * shade, col.b * shade, 1f);
                }
            }
            void Line(float x0, float y0, float x1, float y1, float w, Color col)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * s);
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)Mathf.Max(1, steps);
                    float x = Mathf.Lerp(x0, x1, t), y = Mathf.Lerp(y0, y1, t);
                    Stamp(x, y, w, w, 0f, col);
                }
            }
            Color Var(float amt) { float k = 1f + (Rf() - 0.5f) * amt; return new Color(baseCol.r * k * (1f + (Rf() - 0.5f) * 0.1f), baseCol.g * k, baseCol.b * k); }
            switch (kind)
            {
                case 0: // conifer needles: twigs with dense short needles
                    for (int b = 0; b < 16; b++)
                    {
                        float bx = 0.5f + (Rf() - 0.5f) * 0.2f, by = 0.04f + b * 0.058f;
                        float ex = 0.5f + (Rf() - 0.5f) * 0.9f, ey = by + 0.12f + Rf() * 0.12f;
                        Line(bx, by, ex, ey, 0.004f, baseCol * 0.5f);
                        for (int n = 0; n < 220; n++)
                        {
                            float t = Rf();
                            float px0 = Mathf.Lerp(bx, ex, t), py0 = Mathf.Lerp(by, ey, t);
                            float ang = Mathf.Atan2(ey - by, ex - bx) + (Rf() < 0.5f ? 1f : -1f) * (0.6f + Rf() * 0.6f);
                            float len = 0.025f + Rf() * 0.02f;
                            Line(px0, py0, px0 + Mathf.Cos(ang) * len, py0 + Mathf.Sin(ang) * len, 0.0035f, Var(0.4f));
                        }
                    }
                    break;
                case 1: // broadleaf cluster
                case 3: // birch (smaller, brighter)
                    for (int l = 0; l < (kind == 1 ? 420 : 520); l++)
                    {
                        float r = Mathf.Sqrt(Rf()) * 0.46f, a = Rf() * Mathf.PI * 2f;
                        float cx = 0.5f + Mathf.Cos(a) * r, cy = 0.5f + Mathf.Sin(a) * r;
                        float size = kind == 1 ? 0.03f + Rf() * 0.025f : 0.02f + Rf() * 0.015f;
                        Stamp(cx, cy, size, size * 0.55f, Rf() * Mathf.PI, Var(0.5f) * (0.75f + (1f - r / 0.46f) * 0.1f + (cy) * 0.25f));
                    }
                    break;
                case 2: // palm frond: rib down the middle, leaflets either side (frond runs along v)
                    Line(0.5f, 0.02f, 0.5f, 0.98f, 0.006f, baseCol * 0.6f);
                    for (int l = 0; l < 70; l++)
                    {
                        float t = l / 70f;
                        float y = 0.04f + t * 0.92f;
                        float len = Mathf.Sin(t * Mathf.PI) * 0.45f + 0.04f;
                        foreach (var side in new[] { -1f, 1f })
                            Line(0.5f, y, 0.5f + side * len, y + 0.05f, 0.007f, Var(0.35f));
                    }
                    break;
                default: // bush: dense small leaves
                    for (int l = 0; l < 650; l++)
                    {
                        float r = Mathf.Sqrt(Rf()) * 0.47f, a = Rf() * Mathf.PI * 2f;
                        Stamp(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r * 0.9f, 0.018f, 0.012f, Rf() * Mathf.PI, Var(0.5f));
                    }
                    break;
            }
            // Bleed colour into transparent pixels so mip-maps don't get dark halos.
            for (int pass = 0; pass < 4; pass++)
                for (int y = 1; y < s - 1; y++)
                for (int x = 1; x < s - 1; x++)
                {
                    int i = y * s + x;
                    if (px[i].a > 0.5f) continue;
                    var n = px[i + 1].a > 0.5f ? px[i + 1] : px[i - 1].a > 0.5f ? px[i - 1] : px[i + s].a > 0.5f ? px[i + s] : px[i - s].a > 0.5f ? px[i - s] : px[i];
                    px[i] = new Color(n.r, n.g, n.b, px[i].a);
                }
            return Save(name, s, s, px, false, true, false);
        }

        public static Pair Bark(string name, Color tint, bool palm, bool birch)
        {
            return Make(name, 256, (float u, float v, out Color c, out float h) =>
            {
                float grooves = palm ? Mathf.Abs(Mathf.Sin(v * Mathf.PI * 14f)) : Mathf.Abs(Noise.Perlin(u * 12f, v * 2f, 12, 111)) * 1.6f;
                float n = N(u, v, 8, 4, 112);
                Color col = tint * (0.7f + n * 0.3f + grooves * 0.15f);
                if (birch && Mathf.Clamp01(N(u, v * 0.3f, 16, 2, 113) - 0.62f) > 0f) col = new Color(0.12f, 0.11f, 0.1f);
                c = col; c.a = 1f;
                h = grooves * 0.7f + n * 0.3f;
            }, 3f);
        }

        // ================================================================== buildings

        /// <summary>A facade tile: one bay wide, one floor high. Window panes are glossy dark; lit windows go in the emission map.</summary>
        public static Pair Facade(string name, int style, int seed)
        {
            int s = 256;
            var alb = new Color[s * s];
            var hgt = new float[s * s];
            var emi = new Color[s * s];
            // A 2x2 bay tile so neighbouring windows differ.
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                int bx = u < 0.5f ? 0 : 1, by = v < 0.5f ? 0 : 1;
                float lu = Mathf.Repeat(u * 2f, 1f), lv = Mathf.Repeat(v * 2f, 1f);
                float lit = Noise.Cell(bx, by, seed) < 0.45f ? 1f : 0f;
                Color wall, frame, glass;
                float wx0, wx1, wy0, wy1;
                switch (style)
                {
                    case 0: // Mediterranean stucco: tall windows with green shutters
                        wall = new Color(0.92f, 0.86f, 0.75f) * (0.88f + N(u, v, 8, 4, seed) * 0.2f);
                        frame = new Color(0.2f, 0.36f, 0.26f); glass = new Color(0.08f, 0.09f, 0.1f);
                        wx0 = 0.3f; wx1 = 0.7f; wy0 = 0.18f; wy1 = 0.82f; break;
                    case 1: // office: curtain wall glass with mullions
                        wall = new Color(0.55f, 0.58f, 0.62f); frame = new Color(0.25f, 0.27f, 0.3f); glass = new Color(0.16f, 0.22f, 0.28f);
                        wx0 = 0.04f; wx1 = 0.96f; wy0 = 0.12f; wy1 = 0.96f; break;
                    case 2: // brick terrace
                        wall = Brick(u, v, seed); frame = new Color(0.88f, 0.86f, 0.82f); glass = new Color(0.08f, 0.09f, 0.11f);
                        wx0 = 0.28f; wx1 = 0.72f; wy0 = 0.22f; wy1 = 0.8f; break;
                    case 3: // industrial: corrugated cladding, small high windows
                        wall = new Color(0.56f, 0.6f, 0.62f) * (0.85f + Mathf.Sin(u * Mathf.PI * 40f) * 0.06f + N(u, v, 6, 3, seed) * 0.12f);
                        frame = new Color(0.3f, 0.32f, 0.34f); glass = new Color(0.12f, 0.14f, 0.16f);
                        wx0 = 0.2f; wx1 = 0.8f; wy0 = 0.7f; wy1 = 0.9f; break;
                    default: // village: cream render, small windows with wooden frames
                        wall = new Color(0.9f, 0.86f, 0.78f) * (0.86f + N(u, v, 6, 4, seed) * 0.2f);
                        frame = new Color(0.36f, 0.25f, 0.16f); glass = new Color(0.07f, 0.08f, 0.09f);
                        wx0 = 0.32f; wx1 = 0.68f; wy0 = 0.3f; wy1 = 0.75f; break;
                }
                bool inWin = lu > wx0 && lu < wx1 && lv > wy0 && lv < wy1;
                bool inFrame = lu > wx0 - 0.04f && lu < wx1 + 0.04f && lv > wy0 - 0.04f && lv < wy1 + 0.04f;
                Color col = wall;
                float h = 0.6f;
                Color e = Color.black;
                if (style == 1 && inFrame && !inWin) col = frame;
                if (inFrame && !inWin) { col = frame; h = 0.8f; }
                if (inWin)
                {
                    // Mullion cross in the window.
                    bool mull = Mathf.Abs(lu - (wx0 + wx1) * 0.5f) < 0.012f || Mathf.Abs(lv - (wy0 + wy1) * 0.55f) < 0.012f;
                    col = mull ? frame : glass * (0.8f + lv * 0.4f);
                    h = mull ? 0.7f : 0.2f;
                    if (!mull && lit > 0f) e = new Color(1f, 0.85f, 0.6f) * (0.6f + Noise.Cell(bx + 7, by + 3, seed) * 0.4f);
                    if (style == 0 && (lu < wx0 + 0.09f || lu > wx1 - 0.09f) && !mull) { col = frame * 0.9f; e = Color.black; h = 0.7f; }
                }
                // Floor line / ledge.
                if (lv < 0.05f) { col *= 0.82f; h = 0.9f; }
                col = Grain(col, u, v, 0.03f, 64, seed + 9);
                alb[y * s + x] = new Color(col.r, col.g, col.b, inWin ? 0.9f : 0.15f);
                hgt[y * s + x] = h;
                emi[y * s + x] = e;
            }
            var pair = new Pair
            {
                albedo = Save(name, s, s, alb, false, false, true),
                normal = Save(name + "_N", s, s, NormalFromHeight(hgt, s, 4f, true), true, false, true),
                emission = Save(name + "_E", s, s, emi, false, false, true),
            };
            return pair;
        }

        static Color Brick(float u, float v, int seed)
        {
            float rows = 28f;
            float row = Mathf.Floor(v * rows);
            float off = row % 2 == 0 ? 0f : 0.5f;
            float col = Mathf.Floor(u * 14f + off);
            float fu = Mathf.Repeat(u * 14f + off, 1f), fv = Mathf.Repeat(v * rows, 1f);
            bool mortar = fu < 0.06f || fv < 0.1f;
            float tone = Noise.Cell((int)col, (int)row, seed);
            return mortar ? new Color(0.7f, 0.68f, 0.64f) : new Color(0.55f, 0.27f, 0.2f) * (0.8f + tone * 0.35f);
        }

        public static Pair Roof(bool tiles)
        {
            return Make(tiles ? "RoofTiles" : "RoofFlat", 256, (float u, float v, out Color c, out float h) =>
            {
                if (tiles)
                {
                    float row = Mathf.Repeat(v * 8f, 1f), coln = Mathf.Repeat(u * 8f + (Mathf.Floor(v * 8f) % 2) * 0.5f, 1f);
                    float curve = Mathf.Sin(coln * Mathf.PI);
                    float tone = Noise.Cell(Mathf.FloorToInt(u * 8f), Mathf.FloorToInt(v * 8f), 121);
                    c = new Color(0.62f, 0.3f, 0.2f) * (0.7f + curve * 0.3f + tone * 0.15f) * (row < 0.12f ? 0.7f : 1f);
                    h = curve * 0.6f + (row < 0.12f ? 0f : 0.3f);
                }
                else
                {
                    float n = N(u, v, 16, 4, 122);
                    c = new Color(0.36f, 0.35f, 0.34f) * (0.8f + n * 0.3f);
                    h = n;
                }
                c.a = 1f;
            }, 2f);
        }

        public static Texture2D Crowd()
        {
            // One row of spectators, in pixels: shoulders, head, some with arms up. Tiled along each grandstand tier.
            int w = 512, hgt = 128;
            var px = new Color[w * hgt];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.1f, 0.1f, 0.1f, 0f);
            var rng = new System.Random(131);
            Color[] shirts = { Hex("C8102E"), Hex("F4F4F2"), Hex("1C4F9C"), Hex("F2A900"), Hex("101114"), Hex("0B5D3B"), Hex("E8501C"), Hex("63C5DA"), Hex("8A8F96"), Hex("5B2A86") };
            Color[] skins = { Hex("E8C4A0"), Hex("C99A6E"), Hex("8D5B3E"), Hex("F1D2B6"), Hex("5C3A28") };
            int people = 22;
            for (int p = 0; p < people; p++)
            {
                float cx = (p + 0.5f) * w / people + ((float)rng.NextDouble() - 0.5f) * 8f;
                float shoulder = 62f + (float)rng.NextDouble() * 18f;   // px from the bottom
                float half = 8.5f + (float)rng.NextDouble() * 2.5f;
                Color shirt = shirts[rng.Next(shirts.Length)] * (0.85f + (float)rng.NextDouble() * 0.2f);
                Color skin = skins[rng.Next(skins.Length)];
                bool armsUp = rng.NextDouble() < 0.18, cap = rng.NextDouble() < 0.3;
                Color capCol = shirts[rng.Next(shirts.Length)];
                for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f;
                    // Torso with rounded shoulders.
                    bool torso = dy < shoulder && Mathf.Abs(dx) < half - Mathf.Max(0f, dy - (shoulder - 5f)) * 0.9f;
                    float hx = dx, hy = dy - (shoulder + 8f);
                    bool head = hx * hx / 30f + hy * hy / 42f < 1f;
                    bool capTop = cap && head && hy > 2.5f;
                    bool arm = armsUp && (Mathf.Abs(dx - half) < 2f || Mathf.Abs(dx + half) < 2f) && dy > shoulder - 6f && dy < shoulder + 26f;
                    int i = y * w + x;
                    if (capTop) px[i] = capCol;
                    else if (head) px[i] = skin * (0.85f + 0.15f * Mathf.Clamp01(hy / 6f + 0.5f));
                    else if (arm) px[i] = skin * 0.9f;
                    else if (torso) px[i] = shirt * (0.75f + 0.25f * dy / shoulder);
                }
            }
            return Save("Crowd", w, hgt, px, false, true, true);
        }

        /// <summary>Ad boards (2 x 3 panels) with the wide bridge banner across the top row.</summary>
        public static Texture2D Ads()
        {
            int w = 1024, h = 512;
            var px = new Color32[w * h];
            string[] brands = { "VEYLIX", "KESTREL TYRES", "NOVACOM", "ARDENT OIL", "HALCYON", "ORBIS" };
            Color[] bg = { Hex("101114"), Hex("F2A900"), Hex("1C4F9C"), Hex("C8102E"), Hex("0B5D3B"), Hex("F4F4F2") };
            Color[] fg = { Hex("63C5DA"), Hex("101114"), Hex("F4F4F2"), Hex("F4F4F2"), Hex("D6B98C"), Hex("C8102E") };
            for (int p = 0; p < 6; p++)
            {
                int col = p % 2, row = p / 2;
                int x0 = col * 512, y0 = row * 128;
                for (int y = y0; y < y0 + 128; y++) for (int x = x0; x < x0 + 512; x++) px[y * w + x] = bg[p];
                LiveryPainter.DrawText(px, w, h, x0 + 256, y0 + 64, Fit(brands[p], 440f, 62f), brands[p], fg[p], 1.1f);
            }
            for (int y = 384; y < 512; y++) for (int x = 0; x < w; x++) px[y * w + x] = Hex("101114");
            LiveryPainter.DrawText(px, w, h, 512, 448, Fit("VEYLIX GRAND PRIX", 900f, 72f), "VEYLIX GRAND PRIX", Hex("F4F4F2"), 1.1f);
            for (int x = 0; x < w; x++) for (int y = 386; y < 392; y++) px[y * w + x] = Hex("E8501C");
            var c = new Color[px.Length];
            for (int i = 0; i < px.Length; i++) c[i] = px[i];
            return Save("Ads", w, h, c, false, false, false);
        }

        /// <summary>Brake marker boards (150/100/50) in the top row, other track signs below.</summary>
        public static Texture2D Signs()
        {
            int w = 512, h = 512;
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(240, 240, 238, 255);
            string[] n = { "150", "100", "50", "" };
            for (int b = 0; b < 4; b++)
            {
                int x0 = b * 128;
                for (int y = 384; y < 512; y++) for (int x = x0; x < x0 + 128; x++)
                {
                    bool border = x - x0 < 6 || x - x0 > 121 || y < 390 || y > 505;
                    px[y * w + x] = border ? new Color32(200, 30, 20, 255) : new Color32(245, 245, 243, 255);
                }
                LiveryPainter.DrawText(px, w, h, x0 + 64, 448, Fit(n[b], 100f, 60f), n[b], new Color(0.05f, 0.05f, 0.06f), 1.2f);
            }
            var c = new Color[px.Length];
            for (int i = 0; i < px.Length; i++) c[i] = px[i];
            return Save("Signs", w, h, c, false, false, false);
        }

        public static Texture2D Neon(int seed)
        {
            int w = 512, h = 512;
            var px = new Color32[w * h];
            string[] words = { "CASINO", "HOTEL", "BAR", "OPEN", "LUCKY 7", "DINER", "VEYLIX", "MOTEL", "JACKPOT", "CLUB", "ROYALE", "NOODLES", "24H", "GRAND", "STAR", "LOUNGE" };
            Color[] cols = { Hex("FF3C8E"), Hex("38E1FF"), Hex("FFD23A"), Hex("7CFF5A"), Hex("FF6A2A"), Hex("B76CFF") };
            var rng = new System.Random(seed);
            for (int k = 0; k < 16; k++)
            {
                int cx = (k % 4) * 128 + 64, cy = (k / 4) * 128 + 64;
                Color c = cols[rng.Next(cols.Length)];
                // Border tube then lettering.
                for (int y = cy - 56; y < cy + 56; y++) for (int x = cx - 60; x < cx + 60; x++)
                {
                    bool border = (Mathf.Abs(x - cx) > 54 || Mathf.Abs(y - cy) > 50) && Mathf.Abs(x - cx) < 58 && Mathf.Abs(y - cy) < 54;
                    px[y * w + x] = border ? (Color32)(c * 0.9f) : new Color32(8, 8, 10, 255);
                }
                string word = words[k];
                float size = Fit(word, 100f, 40f);
                LiveryPainter.DrawText(px, w, h, cx, cy, size, word, c, 1f);
            }
            var cc = new Color[px.Length];
            for (int i = 0; i < px.Length; i++) cc[i] = px[i];
            return Save("Neon" + seed, w, h, cc, false, false, false);
        }

        public static Pair Containers()
        {
            Color[] cols = { Hex("A33B20"), Hex("1C4F9C"), Hex("2E6B3F"), Hex("C2A23A"), Hex("6E6F72"), Hex("8E1B3A"), Hex("E8501C"), Hex("1E7B7B") };
            return Make("Containers", 512, (float u, float v, out Color c, out float h) =>
            {
                // v picks the colour band (8 bands); corrugation along u.
                int band = Mathf.Clamp(Mathf.FloorToInt(v * 8f), 0, 7);
                float corr = Mathf.Sin(u * Mathf.PI * 2f * 48f);
                float rust = Mathf.Clamp01(N(u, v, 12, 3, 141) - 0.68f) * 2f;
                c = Color.Lerp(cols[band] * (0.85f + corr * 0.08f), new Color(0.4f, 0.24f, 0.14f), rust * 0.5f);
                c.a = 1f;
                h = corr * 0.5f + 0.5f;
            }, 2.5f);
        }

        public static Pair Runway()
        {
            return Make("Runway", 512, (float u, float v, out Color c, out float h) =>
            {
                Vector2 w = Wo(u, v, 120, 151);
                float g = 0.2f + Mathf.Clamp01(1f - w.x * 2.4f) * 0.06f + (N(u, v, 4, 3, 152) - 0.5f) * 0.05f;
                Color col = new Color(g, g, g * 1.03f);
                // Centre-line dashes and edge lines.
                if (Mathf.Abs(u - 0.5f) < 0.006f && Mathf.Repeat(v, 1f) < 0.5f) col = new Color(0.88f, 0.88f, 0.86f);
                if (u < 0.02f || u > 0.98f) col = new Color(0.88f, 0.88f, 0.86f);
                if (Mathf.Repeat(v, 1f) > 0.92f && (Mathf.Repeat(u * 12f, 1f) < 0.5f) && u > 0.1f && u < 0.9f) col = new Color(0.85f, 0.85f, 0.83f);
                c = col; c.a = 1f;
                h = Mathf.Clamp01(1f - w.x * 2.4f);
            }, 2f);
        }

        public static Pair Pavers()
        {
            return Make("Pavers", 512, (float u, float v, out Color c, out float h) =>
            {
                float pu = Mathf.Repeat(u * 6f, 1f), pv = Mathf.Repeat(v * 6f, 1f);
                bool gap = pu < 0.03f || pv < 0.03f;
                float tone = Noise.Cell(Mathf.FloorToInt(u * 6f), Mathf.FloorToInt(v * 6f), 161);
                c = gap ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.58f, 0.55f) * (0.85f + tone * 0.2f + N(u, v, 32, 2, 162) * 0.1f);
                c.a = 1f;
                h = gap ? 0f : 0.7f;
            }, 3f);
        }

        public static Pair Water()
        {
            return Make("WaterWaves", 512, (float u, float v, out Color c, out float h) =>
            {
                c = new Color(0.1f, 0.25f, 0.3f);
                h = N(u, v, 6, 5, 171) * 0.6f + N(u, v, 20, 3, 172) * 0.4f;
            }, 6f);
        }

        // ================================================================== car

        public static Pair Carbon()
        {
            return Make("Carbon", 256, (float u, float v, out Color c, out float h) =>
            {
                // 2x2 twill weave: tows alternate direction; the clear coat gets a soft sheen per tow.
                int cu = Mathf.FloorToInt(u * 16f), cv = Mathf.FloorToInt(v * 16f);
                bool warp = ((cu + cv / 2 * 0 + (cv)) / 2 % 2) == 0 ^ ((cu / 2) % 2 == 0);
                warp = ((cu + cv) / 2) % 2 == 0;
                float fu = Mathf.Repeat(u * 16f, 1f), fv = Mathf.Repeat(v * 16f, 1f);
                float across = warp ? Mathf.Sin(fv * Mathf.PI) : Mathf.Sin(fu * Mathf.PI);
                float fibres = warp ? Mathf.Abs(Noise.Perlin(u * 400f, v * 20f, 0, 181)) : Mathf.Abs(Noise.Perlin(u * 20f, v * 400f, 0, 182));
                float g = 0.05f + across * 0.06f + fibres * 0.02f;
                c = new Color(g, g, g * 1.08f);
                h = across;
            }, 1.5f);
        }

        public static Texture2D TyreSidewall(int compound, Color band)
        {
            int w = 1024, h = 128;
            var px = new Color32[w * h];
            // v along the tyre profile (inner bead -> tread -> outer bead), u around the circumference (x2).
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float g = 0.055f + N(u, v, 64, 2, 191) * 0.02f;
                bool tread = v > 0.33f && v < 0.67f;
                if (tread)
                {
                    g = 0.06f + N(u, v, 128, 2, 192) * 0.025f;
                    if (compound >= 3)
                    {
                        // Grooved wet / inter tread.
                        float grooveA = Mathf.Abs(Mathf.Repeat(u * 64f + (v - 0.5f) * 6f, 1f) - 0.5f);
                        float grooveB = Mathf.Abs(v - 0.5f) < 0.015f ? 0f : 1f;
                        if (grooveA < (compound == 4 ? 0.12f : 0.07f) || grooveB == 0f) g *= 0.4f;
                    }
                }
                Color c = new Color(g, g, g);
                // Outer sidewall: the compound colour band.
                if (v > 0.93f && v < 0.96f) c = band * 0.95f;
                px[y * w + x] = c;
            }
            // Brand lettering on the outer sidewall, twice round.
            for (int k = 0; k < 2; k++) LiveryPainter.DrawText(px, w, h, w * (0.25f + 0.5f * k), h * 0.82f, 14f, "KESTREL  " + CarCatalog.CompoundNames[compound].ToUpperInvariant(), new Color(0.88f, 0.88f, 0.88f), 0.9f);
            var c2 = new Color[px.Length];
            for (int i = 0; i < px.Length; i++) c2[i] = px[i];
            return Save("Tyre" + compound, w, h, c2, false, false, true);
        }

        public static Pair BrakeDisc()
        {
            return Make("BrakeDisc", 256, (float u, float v, out Color c, out float h) =>
            {
                // u around, v across the disc face: drilled holes in a spiral, carbon-ceramic grey.
                float holes = Mathf.Clamp01(1f - Wo(u, v, 24, 201).x * 7f);
                float n = N(u, v, 32, 3, 202);
                float g = 0.22f + n * 0.06f - holes * 0.18f;
                c = new Color(g, g * 0.98f, g * 0.96f); c.a = 1f;
                h = 1f - holes;
            }, 3f);
        }

        public static Pair Plank()
        {
            return Make("Plank", 256, (float u, float v, out Color c, out float h) =>
            {
                float grain = Mathf.Abs(Noise.Perlin(u * 3f, v * 60f, 3, 211)) * 1.5f;
                c = new Color(0.58f, 0.47f, 0.33f) * (0.8f + grain * 0.25f); c.a = 1f;
                h = grain;
            }, 1f);
        }

        // ================================================================== particles

        public static Texture2D Particle(string name, int kind)
        {
            int s = 128;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s - 0.5f, v = (y + 0.5f) / s - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v) * 2f;
                float a;
                switch (kind)
                {
                    case 0: // smoke: soft lumpy puff
                        a = Mathf.Clamp01(1f - d) * (0.55f + Noise.Fbm(u * 6f + 3f, v * 6f, 4) * 0.6f);
                        a = Mathf.Clamp01(a * 1.3f);
                        break;
                    case 1: // fire: flickering blob
                        a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.5f) * (0.7f + Noise.Fbm(u * 8f, v * 8f + 5f, 4) * 0.6f);
                        break;
                    case 2: // spark / glow
                        a = Mathf.Pow(Mathf.Clamp01(1f - d), 3f);
                        break;
                    case 3: // rain streak (stretched by the renderer)
                        a = Mathf.Clamp01(1f - Mathf.Abs(u) * 6f) * Mathf.Clamp01(1f - Mathf.Abs(v) * 2f) * 0.7f;
                        break;
                    case 4: // light pool on the ground
                        a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f) * 0.6f;
                        break;
                    default: // debris chunk
                        a = Mathf.Abs(u) < 0.3f && Mathf.Abs(v) < 0.2f + u * 0.3f ? 1f : 0f;
                        break;
                }
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            return Save(name, s, s, px, false, true, false);
        }

        // ================================================================== map previews

        /// <summary>A clean line drawing of the map's layout with a start marker, for the map cards.</summary>
        public static Texture2D MapPreview(MapDef m)
        {
            int w = 512, h = 320;
            var px = new Color[w * h];
            Color bg = new Color(0.075f, 0.08f, 0.09f);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float grid = (x % 32 == 0 || y % 32 == 0) ? 0.012f : 0f;
                px[y * w + x] = bg + new Color(grid, grid, grid, 0f);
            }
            var cps = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i + 2 < m.points.Length; i += 3) cps.Add(new Vector3(m.points[i], m.points[i + 2], m.points[i + 1]));
            var dense = TrackSpline.CatmullRom(cps, true);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (var p in dense) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            float scale = Mathf.Min((w - 60) / (max.x - min.x), (h - 60) / (max.y - min.y));
            Vector2 off = new Vector2((w - (max.x - min.x) * scale) * 0.5f, (h - (max.y - min.y) * scale) * 0.5f);
            System.Func<Vector3, Vector2> P = p => new Vector2((p.x - min.x) * scale + off.x, (p.z - min.y) * scale + off.y);
            // Shadow, then the track in white, start marker in signal orange.
            for (int pass = 0; pass < 2; pass++)
            {
                float width = pass == 0 ? 9f : 5f;
                Color col = pass == 0 ? new Color(0f, 0f, 0f, 1f) : (m.freeRoam ? new Color(0.55f, 0.85f, 0.6f) : new Color(0.95f, 0.95f, 0.94f));
                for (int i = 0; i < dense.Count; i++)
                {
                    Vector2 a = P(dense[i]), b = P(dense[(i + 1) % dense.Count]);
                    if (pass == 0) { a += new Vector2(3, -3); b += new Vector2(3, -3); }
                    Segment(px, w, h, a, b, width, col);
                }
            }
            Vector2 s0 = P(dense[0]);
            Disc(px, w, h, s0, 9f, new Color(1f, 0.32f, 0.16f));
            return Save("Map_" + m.id, w, h, px, false, false, false, 512);
        }

        static void Segment(Color[] px, int w, int h, Vector2 a, Vector2 b, float width, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width));
            Vector2 d = b - a;
            float l2 = Mathf.Max(1e-4f, d.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / l2);
                float dist = Vector2.Distance(p, a + d * t);
                float cov = Mathf.Clamp01(width * 0.5f + 0.5f - dist);
                if (cov > 0f) px[y * w + x] = Color.Lerp(px[y * w + x], col, cov * col.a);
            }
        }

        static void Disc(Color[] px, int w, int h, Vector2 c, float r, Color col)
        {
            for (int y = Mathf.Max(0, (int)(c.y - r - 1)); y < Mathf.Min(h, (int)(c.y + r + 2)); y++)
            for (int x = Mathf.Max(0, (int)(c.x - r - 1)); x < Mathf.Min(w, (int)(c.x + r + 2)); x++)
            {
                float cov = Mathf.Clamp01(r + 0.5f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c));
                if (cov > 0f) px[y * w + x] = Color.Lerp(px[y * w + x], col, cov);
            }
        }
    }
}
