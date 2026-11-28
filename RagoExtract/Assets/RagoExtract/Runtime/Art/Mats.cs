using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Material library. All textures are generated procedurally at startup (noise, scratches,
    /// panel lines, wood grain...) so the project needs no imported art and still gets
    /// worn, hand-crafted looking surfaces.
    /// </summary>
    public static class Mats
    {
        static Material _lit, _unlit, _holo, _sky, _foliage, _scope;
        static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        public static Texture2D Grime, Noise, Concrete, ConcreteN, Metal, MetalN, Wood, WoodN, Planks, Ground, Grass, Dirt, Asphalt, AsphaltN,
            Corrugated, CorrugatedN, Fabric, FabricN, Camo, Brick, BrickN, Tiles, TilesN, Bark, Leaves, Plaster, SteelPanel, SteelPanelN, Epoxy, EpoxyN, WornPanel, WornPanelN, WornConcrete, LabTile, LabTileN, Grunge, Cerakote, Stipple;
        public static Texture2D Soft, Flash, Smoke, OilSplat, OilDrop, Drop, BulletHole, Spark, Vignette, Glow, Panel, Eye;
        static bool _init;

        public static void Init()
        {
            if (_init) return;
            _init = true;
            _lit = Resources.Load<Material>("Rago/M_Lit");
            _unlit = Resources.Load<Material>("Rago/M_Unlit");
            _holo = Resources.Load<Material>("Rago/M_Holo");
            _sky = Resources.Load<Material>("Rago/M_Sky");
            if (_lit == null) _lit = new Material(FindShader("Rago/Lit", "Standard"));
            if (_unlit == null) _unlit = new Material(FindShader("Rago/Unlit", "Sprites/Default"));
            if (_holo == null) _holo = new Material(FindShader("Rago/HoloReticle", "Sprites/Default"));
            _scope = Resources.Load<Material>("Rago/M_Scope");
            if (_scope == null) _scope = new Material(FindShader("Rago/ScopeLens", "Unlit/Texture"));
            if (_sky == null) _sky = new Material(FindShader("Rago/Sky", "Skybox/Procedural"));
            BuildTextures();
        }

        // ---------------------------------------------------------------- foliage
        public static Texture2D LeafCard, NeedleCard, GrassCard, FernCard;
        static bool _foliageInit;

        /// <summary>Alpha-cutout, two-sided, wind-swayed material for leaf / needle / grass cards.</summary>
        public static Material Foliage(string key, Texture2D tex, Color tint, float wind = 1f, float cutoff = 0.45f)
        {
            Init(); InitFoliage();
            if (_cache.TryGetValue(key, out var m) && m != null) return m;
            if (_foliage == null) _foliage = Resources.Load<Material>("Rago/M_Foliage");
            if (_foliage == null) { var sh = Shader.Find("Rago/Foliage") ?? Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse"); _foliage = new Material(sh); }
            m = new Material(_foliage);
            m.name = key;
            m.SetColor("_Color", tint);
            m.SetTexture("_MainTex", tex);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_Wind", wind);
            m.renderQueue = 2450;
            m.enableInstancing = true;
            _cache[key] = m;
            return m;
        }

        static void InitFoliage()
        {
            if (_foliageInit) return;
            _foliageInit = true;
            var r = new System.Random(77);
            float R() => (float)r.NextDouble();
            // broad-leaf cluster: ~46 pointed leaves fanning out from the middle, each with a midrib, on a few twigs
            int nl = 46;
            var lc = new Vector2[nl]; var la = new float[nl]; var ll = new float[nl]; var lw = new float[nl]; var lcol = new Color[nl];
            for (int i = 0; i < nl; i++)
            {
                float ang = R() * Mathf.PI * 2, rad = Mathf.Sqrt(R()) * 0.36f;
                lc[i] = new Vector2(0.5f + Mathf.Cos(ang) * rad, 0.5f + Mathf.Sin(ang) * rad);
                la[i] = ang + (R() - 0.5f) * 1.2f;
                ll[i] = 0.075f + R() * 0.05f; lw[i] = ll[i] * (0.38f + R() * 0.12f);
                float g = R();
                lcol[i] = new Color(0.17f + g * 0.12f, 0.33f + g * 0.17f, 0.09f + g * 0.06f);
            }
            LeafCard = Tex(256, true, (x, y, u, v) =>
            {
                var p = new Vector2(u, v);
                Color c = new Color(0.17f, 0.29f, 0.12f, 0);
                // twigs: thin lines from the middle out
                for (int i = 0; i < nl; i += 3)
                {
                    Vector2 a = new Vector2(0.5f, 0.5f), b = lc[i], ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                    if ((a + ab * t - p).magnitude < 0.0045f) c = new Color(0.22f, 0.16f, 0.1f, 1);
                }
                for (int i = 0; i < nl; i++)
                {
                    Vector2 d = p - lc[i];
                    float ca = Mathf.Cos(-la[i]), sa = Mathf.Sin(-la[i]);
                    float lx = d.x * ca - d.y * sa, ly = d.x * sa + d.y * ca;
                    float k = lx / ll[i];
                    if (k < -1 || k > 1) continue;
                    float half = lw[i] * Mathf.Sqrt(1 - k * k) * (1 - 0.35f * Mathf.Max(0, k));   // pointed tip
                    if (Mathf.Abs(ly) > half) continue;
                    float shade = 0.85f + 0.3f * (ly / Mathf.Max(1e-4f, half)) * 0.5f + (1 - Mathf.Abs(k)) * 0.1f;
                    Color lcI = lcol[i] * shade;
                    if (Mathf.Abs(ly) < 0.0035f) lcI *= 0.78f;   // midrib
                    c = new Color(lcI.r, lcI.g, lcI.b, 1);
                }
                return c;
            });
            // pine branch: a twig up the middle with rows of needles slanting up and out
            NeedleCard = Tex(256, true, (x, y, u, v) =>
            {
                float side = Mathf.Abs(u - 0.5f);
                float taper = Mathf.Clamp01(1 - v * 0.85f);
                if (side < 0.012f * taper + 0.003f && v > 0.02f && v < 0.97f) return new Color(0.27f, 0.2f, 0.13f, 1);
                float v0 = v - side * 0.85f;
                if (v0 < 0.02f) return new Color(0.17f, 0.29f, 0.12f, 0);
                float row = v0 / 0.014f;
                float fr = row - Mathf.Floor(row);
                float len = 0.47f * (1 - v0 * 0.55f) * (0.85f + 0.3f * MathX.Noise(Mathf.Floor(row) * 3.1f, u > 0.5f ? 1 : 2, 7));
                if (fr < 0.62f && side < len)
                {
                    float g = 0.75f + 0.25f * Mathf.Sin(row * 2.3f);
                    return new Color(0.11f * g, 0.25f * g, 0.13f * g, 1);
                }
                return new Color(0.17f, 0.29f, 0.12f, 0);
            });
            // grass tuft: 34 tapered, curving blades from the bottom
            int nb = 34;
            var bx = new float[nb]; var bh = new float[nb]; var bl = new float[nb]; var bw = new float[nb]; var bc = new Color[nb];
            for (int i = 0; i < nb; i++)
            {
                bx[i] = 0.2f + R() * 0.6f; bh[i] = 0.45f + R() * 0.52f; bl[i] = (R() - 0.5f) * 0.5f; bw[i] = 0.012f + R() * 0.012f;
                float g = R(); bc[i] = new Color(0.26f + g * 0.14f, 0.38f + g * 0.16f, 0.12f + g * 0.05f);
            }
            GrassCard = Tex(128, true, (x, y, u, v) =>
            {
                for (int i = 0; i < nb; i++)
                {
                    if (v > bh[i]) continue;
                    float t = v / bh[i];
                    float cx = bx[i] + bl[i] * t * t;
                    if (Mathf.Abs(u - cx) < bw[i] * (1 - t)) { var c = bc[i] * (0.6f + 0.5f * t); return new Color(c.r, c.g, c.b, 1); }
                }
                return new Color(0.17f, 0.29f, 0.12f, 0);
            });
            // fern frond: a curved stem with paired leaflets getting smaller towards the tip
            FernCard = Tex(128, true, (x, y, u, v) =>
            {
                float stemX = 0.5f + (v - 0.1f) * (v - 0.1f) * 0.25f;
                float side = Mathf.Abs(u - stemX);
                if (v < 0.06f || v > 0.96f) return new Color(0.17f, 0.29f, 0.12f, 0);
                if (side < 0.008f) return new Color(0.2f, 0.32f, 0.12f, 1);
                float reach = 0.4f * Mathf.Sin(Mathf.Clamp01((v - 0.06f) / 0.9f) * Mathf.PI) + 0.03f;
                float v0 = v - side * 0.35f;
                float row = v0 / 0.05f, fr = row - Mathf.Floor(row);
                float w = 0.32f * (1 - side / Mathf.Max(0.01f, reach));
                if (side < reach && Mathf.Abs(fr - 0.5f) < w) { float g = 0.8f + 0.2f * Mathf.Sin(row); return new Color(0.16f * g, 0.36f * g, 0.13f * g, 1); }
                return new Color(0.17f, 0.29f, 0.12f, 0);
            });
        }

        static Material _mirrorBase;
        /// <summary>(3.55) A material for a Mirror's glass (Rago/Mirror) - a plain silvery one if that shader isn't there.</summary>
        public static Material Mirror()
        {
            Init();
            if (_mirrorBase == null) _mirrorBase = Resources.Load<Material>("Rago/M_Mirror");
            if (_mirrorBase != null && _mirrorBase.shader != null && _mirrorBase.shader.isSupported) return new Material(_mirrorBase);
            return Lit("mirror_fallback", new Color(0.55f, 0.6f, 0.62f), 0.9f, 0.95f, null, null, 0f, 0f);
        }

        static Shader FindShader(string a, string b)
        {
            var s = Shader.Find(a);
            if (s == null) s = Shader.Find(b);
            return s;
        }

        // ---------------------------------------------------------------- materials
        public static Material Lit(string key, Color c, float metal = 0f, float smooth = 0.35f, Texture2D tex = null, Texture2D normal = null,
            float grime = 0.35f, float scratch = 0.2f, bool tri = false, float triScale = 0.5f, Color? emission = null)
        {
            Init();
            if (key != null && _cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(_lit);
            m.name = key ?? "lit";
            m.SetColor("_Color", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", smooth);
            m.SetTexture("_MainTex", tex != null ? tex : Texture2D.whiteTexture);
            if (normal != null) m.SetTexture("_BumpMap", normal);
            m.SetTexture("_DetailTex", Grime);
            m.SetFloat("_Grime", grime);
            m.SetFloat("_Scratch", scratch);
            m.SetFloat("_Triplanar", tri ? 1 : 0);
            m.SetFloat("_TriScale", triScale);
            m.SetFloat("_VColor", 1);
            if (emission.HasValue) m.SetColor("_EmissionColor", emission.Value);
            m.enableInstancing = true;
            if (key != null) _cache[key] = m;
            return m;
        }

        public enum BlendKind { Alpha, Additive, Premultiplied, Multiply }
        public static Material Unlit(string key, Color c, Texture2D tex = null, BlendKind blend = BlendKind.Alpha, bool zwrite = false, int queueOffset = 0, bool zTestAlways = false)
        {
            Init();
            if (key != null && _cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(_unlit);
            m.name = key ?? "unlit";
            m.SetColor("_Color", c);
            m.SetTexture("_MainTex", tex != null ? tex : Texture2D.whiteTexture);
            switch (blend)
            {
                case BlendKind.Additive: m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); break;
                case BlendKind.Premultiplied: m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); break;
                case BlendKind.Multiply: m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero); break;
                default: m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); break;
            }
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
            m.SetFloat("_ZTest", zTestAlways ? (float)UnityEngine.Rendering.CompareFunction.Always : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            m.renderQueue = (zwrite ? 2450 : 3000) + queueOffset;
            m.enableInstancing = true;
            if (key != null) _cache[key] = m;
            return m;
        }

        public static Material Holo(Color reticle, int style, float size = 0.012f)
        {
            Init();
            var m = new Material(_holo);
            m.SetColor("_ReticleColor", reticle);
            m.SetFloat("_Style", style);
            m.SetFloat("_Size", size * 1.35f);       // big enough to read in a headset
            m.SetFloat("_Thickness", 0.0019f);
            return m;
        }

        /// <summary>A plain textured, unlit material (any texture, e.g. a render texture). Always in the build.</summary>
        public static Material UnlitTexture(Texture t)
        {
            Init();
            var m = new Material(_unlit);
            m.SetColor("_Color", Color.white);
            m.SetTexture("_MainTex", t);
            return m;
        }

        /// <summary>A magnified scope's eyepiece (one per scope: it shows that scope's own camera).</summary>
        public static Material Scope(int style, Color dot, float lensR)
        {
            Init();
            var m = new Material(_scope);
            m.SetFloat("_Style", style);
            m.SetColor("_DotColor", dot);
            m.SetFloat("_LensR", lensR);
            m.SetFloat("_Active", 0f);
            return m;
        }

        public static Material Sky(Color top, Color horizon, Color bottom, Color sun, Vector3 sunDir, float clouds, Color cloudCol)
        {
            Init();
            var m = new Material(_sky);
            m.SetColor("_Top", top); m.SetColor("_Horizon", horizon); m.SetColor("_Bottom", bottom);
            m.SetColor("_SunColor", sun); m.SetVector("_SunDir", -sunDir.normalized);
            m.SetFloat("_Clouds", clouds); m.SetColor("_CloudColor", cloudCol);
            return m;
        }

        // Shared palette ------------------------------------------------------------
        // gun finishes (3.46): cerakote and stippled polymer mapped in the gun's own space, a little less mirror-metal so
        // they read in any light (very metallic dark steel just looked black away from bright reflections)
        public static Material GunObj(Material m, float scale) { m.SetFloat("_Triplanar", 2f); m.SetFloat("_TriScale", scale); return m; }   // mapped in the object's own space (moves with it)
        public static Material GunMetal => GunObj(Lit("gunmetal2", new Color(0.2f, 0.205f, 0.215f), 0.6f, 0.5f, Cerakote, null, 0.25f, 0.6f, true, 6f), 6f);
        public static Material GunBlued => GunObj(Lit("gunblued2", new Color(0.13f, 0.14f, 0.17f), 0.75f, 0.68f, Cerakote, null, 0.15f, 0.45f, true, 8f), 8f);
        public static Material Polymer => GunObj(Lit("polymer2", new Color(0.11f, 0.11f, 0.115f), 0.0f, 0.3f, Stipple, null, 0.3f, 0.15f, true, 9f), 9f);
        public static Material PolymerFde => GunObj(Lit("polyfde2", new Color(0.62f, 0.52f, 0.38f), 0.0f, 0.28f, Stipple, null, 0.45f, 0.2f, true, 9f), 9f);
        public static Material PolymerOd => GunObj(Lit("polyod2", new Color(0.3f, 0.33f, 0.23f), 0.0f, 0.28f, Stipple, null, 0.45f, 0.2f, true, 9f), 9f);
        public static Material GunWood => Lit("gunwood", new Color(0.55f, 0.3f, 0.15f), 0.0f, 0.5f, Wood, WoodN, 0.3f, 0.15f);
        public static Material Brass => Lit("brass", new Color(0.85f, 0.65f, 0.3f), 1f, 0.75f, null, null, 0.1f, 0.1f);
        public static Material Chrome => Lit("chrome", new Color(0.8f, 0.8f, 0.82f), 1f, 0.9f, Metal, null, 0.05f, 0.1f);
        public static Material Gold => Lit("gold", new Color(1f, 0.75f, 0.32f), 1f, 0.8f, Metal, null, 0.05f, 0.15f);
        public static Material Rubber => Lit("rubber", new Color(0.05f, 0.05f, 0.05f), 0f, 0.15f, Noise, null, 0.2f, 0f);
        public static Material Glass => Unlit("glass", new Color(0.4f, 0.55f, 0.65f, 0.25f));
        public static Material Skin => Lit("skin", new Color(0.78f, 0.6f, 0.5f), 0f, 0.35f, Noise, null, 0.15f, 0f);

        // ---------------------------------------------------------------- textures
        static void BuildTextures()
        {
            Grime = Tex(256, true, (x, y, u, v) =>
            {
                float g = MathX.TFbm(u, v, 6, 6, 5, 3);
                float s = Mathf.Max(Scratch(u, v, 11), Scratch(u * 1.3f, v * 0.8f, 23) * 0.8f);
                float ao = 0.55f + MathX.TFbm(u, v, 3, 3, 3, 9) * 0.5f;
                return new Color(g, s, ao, 1);
            });
            Noise = Tex(128, true, (x, y, u, v) => Gray(0.8f + MathX.TFbm(u, v, 8, 8, 4, 1) * 0.35f));
            Concrete = Tex(256, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 8, 8, 5, 5);
                float pores = MathX.TNoise(u, v, 90, 90, 7) > 0.86f ? 0.75f : 1f;
                float stain = Mathf.SmoothStep(0.4f, 0.8f, MathX.TFbm(u, v, 2, 2, 3, 12));
                float c = (0.62f + n * 0.25f) * pores * (1 - stain * 0.25f);
                return new Color(c, c * 0.99f, c * 0.96f, 0.25f + n * 0.2f);
            });
            ConcreteN = NormalFrom(Concrete, 1.5f);
            Metal = Tex(256, true, (x, y, u, v) =>
            {
                float brushed = MathX.TNoise(u, v, 3, 220, 4) * 0.08f;
                float n = MathX.TFbm(u, v, 10, 10, 4, 2) * 0.15f;
                float c = 0.82f + brushed + n;
                return new Color(c, c, c, 0.75f + n);
            });
            MetalN = NormalFrom(Metal, 0.6f);
            Wood = Tex(256, true, (x, y, u, v) =>
            {
                float warp = MathX.TFbm(u, v, 3, 1, 3, 8) * 3f;
                float ring = Mathf.Sin((u * 26f + warp) * Mathf.PI) * 0.5f + 0.5f;
                ring = Mathf.Pow(ring, 3f);
                float fine = MathX.TNoise(u, v, 160, 4, 3) * 0.12f;
                float c = 0.6f + ring * 0.3f + fine;
                return new Color(c, c * 0.82f, c * 0.62f, 0.6f);
            });
            WoodN = NormalFrom(Wood, 0.8f);
            // floor / wall boards: 8 planks across, each its own shade, staggered end joints, soft grain (the old ring
            // texture tiled into hard stripes that shimmered across floors)
            Planks = Tex(256, true, (x, y, u, v) =>
            {
                int board = Mathf.FloorToInt(u * 8f);
                float bu = u * 8f - board;
                float shade = 0.82f + MathX.Hash(board, 3, 11) * 0.3f;
                float off = MathX.Hash(board, 7, 5);
                float jv = Mathf.Repeat(v + off, 0.5f) / 0.5f;   // two board lengths down the texture, staggered per row
                float grain = MathX.TFbm(u, v, 32, 2, 3, board + 40) * 0.12f + MathX.TNoise(u, v, 4, 90, 2) * 0.05f;
                float edge = Mathf.Min(bu, 1f - bu) < 0.03f || jv < 0.012f ? 0.55f : 1f;
                float c = (0.62f + grain) * shade * edge;
                return new Color(c, c * 0.8f, c * 0.6f, 0.6f);
            });
            Asphalt = Tex(256, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 12, 12, 4, 31);
                float grit = MathX.Hash(x, y, 77) * 0.12f;
                float crack = Mathf.Abs(MathX.TFbm(u, v, 3, 3, 4, 40) - 0.5f) < 0.012f ? 0.55f : 1f;
                float c = (0.32f + n * 0.18f + grit) * crack;
                return new Color(c, c, c * 1.02f, 0.3f);
            });
            AsphaltN = NormalFrom(Asphalt, 1.2f);
            Ground = Tex(256, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 6, 6, 5, 50);
                float pebble = MathX.TNoise(u, v, 64, 64, 51) > 0.8f ? 1.15f : 1f;
                return new Color((0.42f + n * 0.2f) * pebble, (0.35f + n * 0.17f) * pebble, (0.26f + n * 0.12f) * pebble, 0.15f);
            });
            Dirt = Ground;
            Grass = Tex(256, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 5, 5, 5, 60);
                float blades = MathX.TNoise(u, v, 140, 40, 61) * 0.25f;
                float dry = Mathf.SmoothStep(0.45f, 0.75f, MathX.TFbm(u, v, 2, 2, 3, 62));
                Color g = Color.Lerp(new Color(0.25f, 0.36f, 0.14f), new Color(0.45f, 0.42f, 0.22f), dry);
                return g * (0.75f + n * 0.4f + blades);
            });
            Corrugated = Tex(256, true, (x, y, u, v) =>
            {
                float wave = Mathf.Sin(u * Mathf.PI * 2 * 16) * 0.5f + 0.5f;
                float rust = Mathf.SmoothStep(0.55f, 0.8f, MathX.TFbm(u, v, 4, 4, 4, 70));
                float c = 0.8f + wave * 0.2f;
                Color baseC = new Color(c, c, c, 0.4f);
                return Color.Lerp(baseC, new Color(0.55f, 0.3f, 0.15f, 0.1f), rust * 0.7f);
            });
            CorrugatedN = Tex(128, false, (x, y, u, v) =>
            {
                float dx = Mathf.Cos(u * Mathf.PI * 2 * 16) * 0.6f;
                Vector3 n = new Vector3(-dx, 0, 1).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }, true);
            Fabric = Tex(128, true, (x, y, u, v) =>
            {
                float weave = ((x + y) % 4 < 2 ? 0.92f : 1f) * ((x - y + 256) % 4 < 2 ? 0.95f : 1f);
                float n = 0.85f + MathX.TFbm(u, v, 6, 6, 3, 80) * 0.25f;
                return Gray(weave * n);
            });
            FabricN = NormalFrom(Fabric, 0.7f);
            Camo = Tex(256, true, (x, y, u, v) =>
            {
                float a = MathX.TFbm(u, v, 5, 5, 4, 90);
                float b = MathX.TFbm(u, v, 7, 7, 4, 91);
                Color c = new Color(0.45f, 0.42f, 0.32f);
                if (a > 0.55f) c = new Color(0.3f, 0.32f, 0.22f);
                if (b > 0.6f) c = new Color(0.2f, 0.18f, 0.14f);
                if (a < 0.35f) c = new Color(0.55f, 0.5f, 0.38f);
                float weave = (x + y) % 3 == 0 ? 0.93f : 1f;
                return c * weave * 1.6f;
            });
            Brick = Tex(256, true, (x, y, u, v) =>
            {
                float rows = 8, cols = 4;
                float ry = v * rows; int r = Mathf.FloorToInt(ry);
                float rx = u * cols + (r % 2) * 0.5f;
                float fx = MathX.Frac(rx), fy = MathX.Frac(ry);
                bool mortar = fx < 0.04f || fy < 0.08f;
                float id = MathX.Hash(Mathf.FloorToInt(rx), r, 5);
                float n = MathX.TFbm(u, v, 20, 20, 3, 100) * 0.2f;
                if (mortar) return new Color(0.62f, 0.6f, 0.56f, 0.1f);
                return new Color(0.55f + id * 0.15f + n, 0.28f + id * 0.06f + n * 0.5f, 0.2f + n * 0.4f, 0.2f);
            });
            BrickN = NormalFrom(Brick, 2f);
            Tiles = Tex(256, true, (x, y, u, v) =>
            {
                float fx = MathX.Frac(u * 4), fy = MathX.Frac(v * 4);
                bool grout = fx < 0.03f || fy < 0.03f;
                float n = MathX.TFbm(u, v, 10, 10, 3, 110) * 0.1f;
                return grout ? new Color(0.4f, 0.4f, 0.38f, 0.1f) : new Color(0.78f + n, 0.78f + n, 0.75f + n, 0.7f);
            });
            TilesN = NormalFrom(Tiles, 1.5f);
            // bunker wall cladding: brushed steel panels (2 x 2 per tile) with dark seams, a bevel catching the light and corner rivets
            SteelPanel = Tex(256, true, (x, y, u, v) =>
            {
                float fx = MathX.Frac(u * 2), fy = MathX.Frac(v * 2);
                float e = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
                int px = Mathf.FloorToInt(u * 2), py = Mathf.FloorToInt(v * 2);
                float id = MathX.Hash(px, py, 21);
                float brush = MathX.TFbm(u, v, 64, 2, 2, 160) * 0.08f;                      // horizontal brushing
                float c = 0.62f + id * 0.08f + brush + MathX.TFbm(u, v, 6, 6, 3, 161) * 0.06f;
                if (e < 0.012f) return new Color(0.16f, 0.16f, 0.17f, 0.2f);               // seam
                if (e < 0.03f) c += 0.12f;                                                  // bevel highlight
                foreach (var rx in new[] { 0.06f, 0.94f }) foreach (var ry in new[] { 0.06f, 0.94f })
                    { float d = Mathf.Sqrt((fx - rx) * (fx - rx) + (fy - ry) * (fy - ry)); if (d < 0.018f) return Gray(0.85f); if (d < 0.026f) return Gray(0.3f); }   // rivets
                return new Color(c, c, c * 1.02f, 0.55f);
            });
            SteelPanelN = NormalFrom(SteelPanel, 2f);
            // bunker floor: dark epoxy-coated tiles, 4 x 4, with thin grout and faint scuffs
            Epoxy = Tex(256, true, (x, y, u, v) =>
            {
                float fx = MathX.Frac(u * 4), fy = MathX.Frac(v * 4);
                if (fx < 0.015f || fy < 0.015f) return new Color(0.18f, 0.18f, 0.19f, 0.2f);
                float id = MathX.Hash(Mathf.FloorToInt(u * 4), Mathf.FloorToInt(v * 4), 33);
                float scuff = Mathf.Max(0f, MathX.TFbm(u, v, 12, 12, 3, 170) - 0.62f) * 0.6f;
                float c = 0.5f + id * 0.06f + MathX.TFbm(u, v, 20, 20, 2, 171) * 0.05f - scuff;
                return new Color(c, c, c, 0.75f);
            });
            EpoxyN = NormalFrom(Epoxy, 1.2f);
            // the worn bunker (3.39): old olive-drab painted steel plates - chipped paint with rust showing through, rust and
            // water streaks running down from the seams and rivets, grime in the corners
            WornPanel = Tex(256, true, (x, y, u, v) =>
            {
                float fx = MathX.Frac(u * 2), fy = MathX.Frac(v * 2);
                float e = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
                int px = Mathf.FloorToInt(u * 2), py = Mathf.FloorToInt(v * 2);
                float id = MathX.Hash(px, py, 41);
                Color paint = new Color(0.36f + id * 0.05f, 0.4f + id * 0.04f, 0.3f, 0.3f);                 // faded olive drab
                Color rust = Color.Lerp(new Color(0.36f, 0.18f, 0.09f, 0.15f), new Color(0.55f, 0.3f, 0.13f, 0.15f), MathX.TFbm(u, v, 24, 24, 3, 181));
                Color bare = new Color(0.45f, 0.44f, 0.42f, 0.4f);
                float n = MathX.TFbm(u, v, 8, 8, 4, 182), chip = MathX.TFbm(u, v, 16, 16, 3, 183);
                Color c = paint * (0.9f + n * 0.2f);
                float edgeWear = 1f - MathX.Step(0.02f, 0.09f, e);                                           // paint wears off at the plate edges
                if (chip > 0.62f - edgeWear * 0.25f) c = chip > 0.68f - edgeWear * 0.2f ? rust : bare;        // chips: bare steel, then rust
                // rust streaks running down (the texture's -v) under each rivet and along the bottom seam
                float streak = 0f;
                foreach (var rx in new[] { 0.06f, 0.5f, 0.94f })
                {
                    float dx = Mathf.Abs(fx - rx); float len = 0.35f + MathX.Hash(px * 3 + (int)(rx * 10), py, 7) * 0.4f;
                    if (dx < 0.035f && fy < 0.94f && fy > 0.94f - len) streak = Mathf.Max(streak, (1f - dx / 0.035f) * (1f - (0.94f - fy) / len));
                }
                c = Color.Lerp(c, new Color(0.38f, 0.2f, 0.1f, 0.15f), streak * 0.75f);
                c = Color.Lerp(c, new Color(0.12f, 0.11f, 0.09f, 0.1f), MathX.Step(0.55f, 0.85f, MathX.TFbm(u, v, 4, 4, 3, 184)) * 0.35f);   // grime blotches
                if (e < 0.012f) return new Color(0.08f, 0.07f, 0.06f, 0.1f);                                 // seam
                foreach (var rx in new[] { 0.06f, 0.5f, 0.94f }) foreach (var ry in new[] { 0.06f, 0.94f })
                    { float d = Mathf.Sqrt((fx - rx) * (fx - rx) + (fy - ry) * (fy - ry)); if (d < 0.016f) return new Color(0.42f, 0.25f, 0.13f, 0.2f); if (d < 0.024f) return new Color(0.1f, 0.08f, 0.06f, 0.1f); }   // rusty rivets
                return c;
            });
            WornPanelN = NormalFrom(WornPanel, 2.2f);
            // the abandoned research sector (3.43): off-white lab tiles - dirty grout, cracked and missing tiles, drip stains
            LabTile = Tex(256, true, (x, y, u, v) =>
            {
                int tx = Mathf.FloorToInt(u * 8), ty = Mathf.FloorToInt(v * 8);
                float fx = MathX.Frac(u * 8), fy = MathX.Frac(v * 8), id = MathX.Hash(tx, ty, 51);
                if (id > 0.978f) { float n0 = MathX.TFbm(u, v, 16, 16, 3, 52); return new Color(0.48f + n0 * 0.1f, 0.47f + n0 * 0.1f, 0.45f + n0 * 0.08f, 0.1f); }   // a tile fallen off: bare concrete
                if (fx < 0.035f || fy < 0.035f) return new Color(0.42f, 0.41f, 0.37f, 0.1f);                                         // dirty grout
                float n = MathX.TFbm(u, v, 12, 12, 3, 53);
                Color c = new Color(0.8f + id * 0.06f, 0.82f + id * 0.05f, 0.78f + id * 0.04f, 0.6f) * (0.94f + n * 0.1f);
                float crack = Mathf.Abs(MathX.TFbm(u, v, 6, 6, 4, 54) - 0.5f);
                if (crack < 0.004f && MathX.Hash(tx, ty, 55) > 0.6f) c *= 0.45f;                                                   // cracked tiles
                float drip = MathX.Step(0.5f, 0.85f, MathX.TFbm(u * 1f, v, 24, 2, 3, 56)) * MathX.Step(0.4f, 0.75f, MathX.TFbm(u, v, 3, 3, 3, 57));
                c = Color.Lerp(c, new Color(0.42f, 0.33f, 0.22f, 0.3f), drip * 0.55f);                                               // rust / water drips
                c = Color.Lerp(c, new Color(0.25f, 0.24f, 0.2f, 0.2f), MathX.Step(0.58f, 0.85f, MathX.TFbm(u, v, 4, 4, 3, 58)) * 0.45f);   // grime
                return c;
            });
            LabTileN = NormalFrom(LabTile, 1.8f);
            // guns (3.46): a fine speckled cerakote coating with soft mottling, and stippled polymer grip texture
            Cerakote = Tex(256, true, (x, y, u, v) =>
            {
                float speck = MathX.Hash(x, y, 70) * 0.08f, mott = MathX.TFbm(u, v, 6, 6, 4, 71) * 0.12f, fine = MathX.TFbm(u, v, 48, 48, 2, 72) * 0.06f;
                float c = 0.62f + speck + mott + fine;
                return new Color(c, c, c * 1.01f, 0.55f);
            });
            Stipple = Tex(256, true, (x, y, u, v) =>
            {
                const int N = 48; float gx = u * N, gy = v * N; int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
                float best = 9f;
                for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++)
                {
                    int wx = ((cx + i) % N + N) % N, wy = ((cy + j) % N + N) % N;
                    float px = cx + i + 0.5f + (MathX.Hash(wx, wy, 73) - 0.5f) * 0.6f, py = cy + j + 0.5f + (MathX.Hash(wx, wy, 74) - 0.5f) * 0.6f;
                    best = Mathf.Min(best, (gx - px) * (gx - px) + (gy - py) * (gy - py));
                }
                float bump = 1f - MathX.Step(0.02f, 0.16f, best);                                   // little raised dots
                float c = 0.6f + bump * 0.22f + MathX.TFbm(u, v, 8, 8, 3, 75) * 0.06f;
                return new Color(c, c, c, 0.35f + bump * 0.2f);
            });
            // general grunge: dirty, corroded steel for pipes and fittings
            Grunge = Tex(128, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 8, 8, 4, 60), r = MathX.Step(0.55f, 0.75f, MathX.TFbm(u, v, 6, 6, 3, 61));
                Color c = new Color(0.55f + n * 0.15f, 0.55f + n * 0.14f, 0.54f + n * 0.12f, 0.4f);
                return Color.Lerp(c, new Color(0.42f, 0.26f, 0.14f, 0.15f), r * 0.7f);
            });
            // stained concrete floor: the plain concrete with oil and water stains, cracks and dust
            WornConcrete = Tex(256, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 10, 10, 5, 190);
                Color c = new Color(0.5f + n * 0.12f, 0.49f + n * 0.12f, 0.46f + n * 0.1f, 0.25f);
                float stain = MathX.Step(0.58f, 0.72f, MathX.TFbm(u, v, 3, 3, 4, 191));
                c = Color.Lerp(c, new Color(0.16f, 0.15f, 0.13f, 0.6f), stain * 0.7f);                    // oil / water stains (a bit glossy)
                float crack = Mathf.Abs(MathX.TFbm(u, v, 5, 5, 4, 192) - 0.5f);
                if (crack < 0.0035f && MathX.TFbm(u, v, 3, 3, 3, 194) > 0.58f) c *= 0.55f;                   // a few hairline cracks
                if (MathX.Hash(x, y, 193) > 0.985f) c *= 0.75f;                                              // grit
                return c;
            });
            Plaster = Tex(256, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 10, 10, 5, 120);
                float peel = Mathf.SmoothStep(0.62f, 0.66f, MathX.TFbm(u, v, 3, 3, 4, 121));
                Color c = new Color(0.82f + n * 0.12f, 0.8f + n * 0.12f, 0.74f + n * 0.1f, 0.15f);
                return Color.Lerp(c, new Color(0.5f, 0.42f, 0.36f, 0.1f), peel);
            });
            Bark = Tex(128, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 18, 3, 4, 130);
                float c = 0.35f + n * 0.4f;
                return new Color(c, c * 0.8f, c * 0.65f, 0.1f);
            });
            Leaves = Tex(128, true, (x, y, u, v) =>
            {
                float n = MathX.TFbm(u, v, 12, 12, 4, 140);
                return new Color(0.16f + n * 0.12f, 0.27f + n * 0.16f, 0.12f + n * 0.06f, 0.2f);
            });
            Panel = Tex(128, true, (x, y, u, v) =>
            {
                float fx = MathX.Frac(u * 2), fy = MathX.Frac(v * 2);
                bool seam = fx < 0.02f || fy < 0.02f;
                float c = 0.9f + MathX.TFbm(u, v, 6, 6, 3, 150) * 0.15f;
                return Gray(seam ? 0.5f : c);
            });

            // Effects
            Soft = Tex(64, false, (x, y, u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2; float a = Mathf.Clamp01(1 - d); return new Color(1, 1, 1, a * a); });
            Glow = Tex(64, false, (x, y, u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2; float a = Mathf.Exp(-d * d * 6f); return new Color(1, 1, 1, a); });
            Flash = Tex(128, false, (x, y, u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2;
                float r = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 18f) * 0.8f + Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2.5f + 0.6f)), 30f) * 0.5f;
                float core = Mathf.Exp(-r * r * 22f);
                float a = Mathf.Clamp01(core + spikes * Mathf.Clamp01(1 - r) * (0.6f + MathX.Noise(ang * 4 + 9, r * 6, 3) * 0.6f));
                return new Color(1, 0.85f + core * 0.15f, 0.55f + core * 0.45f, a);
            });
            Smoke = Tex(128, false, (x, y, u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2;
                float n = MathX.TFbm(u, v, 5, 5, 5, 160);
                float a = Mathf.Clamp01((1 - d) * 1.3f) * Mathf.Clamp01(n * 1.6f - 0.25f);
                return new Color(1, 1, 1, a);
            });
            // 3.47: the people leak OIL, not blood. A splat of used motor oil: black where it's thick, amber-brown at the thin
            // edges (where you can see through it), a faint rainbow film swirling on top and a glossy lip just inside the
            // edge. (The old blood splat used Mathf.SmoothStep - a lerp in Unity - so every splat was a soft square.)
            OilSplat = Tex(128, false, (x, y, u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2;
                float r = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
                float edge = 0.42f + MathX.Fbm(Mathf.Cos(ang) * 2 + 4, Mathf.Sin(ang) * 2 + 4, 4, 170) * 0.46f;
                float inside = edge - r;                                   // > 0 inside the blob
                float body = MathX.Step(-0.012f, 0.02f, inside);
                float drops = 0f;
                for (int i = 0; i < 9; i++)
                {
                    float a2 = MathX.Hash(i * 3.1f) * 6.283f, dr = 0.55f + MathX.Hash(i * 7.7f) * 0.3f;
                    Vector2 c = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * dr;
                    float rad = 0.022f + MathX.Hash(i * 1.3f) * 0.05f;
                    drops = Mathf.Max(drops, MathX.Step(rad, rad * 0.5f, Vector2.Distance(p, c)));
                }
                float a = Mathf.Max(body, drops);
                float th = Mathf.Max(MathX.Step(0f, 0.16f, inside), drops * 0.7f);   // how thick the oil is
                Color col = Color.Lerp(new Color(0.3f, 0.17f, 0.055f), new Color(0.028f, 0.024f, 0.018f), MathX.Step(0f, 0.5f, th));
                // the rainbow film: thin-film colours drifting over the thick part
                float hue = Mathf.Repeat(MathX.TFbm(u, v, 3, 3, 4, 172) * 2.6f + r * 0.8f, 1f);
                var rain = new Color(0.5f + 0.5f * Mathf.Cos(6.283f * hue), 0.5f + 0.5f * Mathf.Cos(6.283f * (hue - 0.33f)), 0.5f + 0.5f * Mathf.Cos(6.283f * (hue - 0.67f)));
                float film = MathX.Step(0.45f, 0.75f, MathX.TFbm(u, v, 2, 2, 3, 173)) * th;
                col += rain * film * 0.13f;
                // glossy lip just inside the edge
                float lip = MathX.Step(0f, 0.025f, inside) * (1f - MathX.Step(0.03f, 0.09f, inside));
                col += new Color(0.11f, 0.1f, 0.095f) * lip;
                return new Color(col.r, col.g, col.b, a * Mathf.Lerp(0.72f, 0.97f, th));
            });
            // a flying drop of oil: dark (tinted by the particle) with a glint on one side
            // (3.54) a bloodshot eyeball, mapped round a sphere whose pole faces forward (v = 0 at the pupil, 1 at the back):
            // yellowed whites going pink, red veins creeping in toward a pale washed-out iris, a dark ring and a black pupil
            Eye = Tex(128, true, (x, y, u, v) =>
            {
                Color sclera = Color.Lerp(new Color(0.92f, 0.84f, 0.74f), new Color(0.88f, 0.5f, 0.46f), MathX.Step(0.12f, 0.45f, v) * 0.8f);   // yellowed, going raw pink
                float w1 = MathX.TFbm(u, v, 6, 3, 3, 301), w2 = MathX.TFbm(u, v, 13, 5, 3, 302), w3 = MathX.TFbm(u, v, 24, 7, 2, 304);
                float veins = Mathf.Max(1f - MathX.Step(0f, 0.05f, Mathf.Abs(w1 - 0.5f)), Mathf.Max(0.85f * (1f - MathX.Step(0f, 0.032f, Mathf.Abs(w2 - 0.5f))), 0.6f * (1f - MathX.Step(0f, 0.025f, Mathf.Abs(w3 - 0.5f)))))
                              * MathX.Step(0.115f, 0.2f, v);   // thick and thin red veins right up to the iris
                Color c = Color.Lerp(sclera, new Color(0.6f, 0.02f, 0.02f), veins * 0.95f);
                if (v < 0.12f)
                {
                    float streak = MathX.TNoise(u, 0.5f, 48, 1, 303);
                    Color iris = Color.Lerp(new Color(0.72f, 0.74f, 0.62f), new Color(0.4f, 0.42f, 0.3f), streak * 0.6f + MathX.Step(0.04f, 0.1f, v) * 0.4f);   // small, pale, washed out
                    c = Color.Lerp(iris, new Color(0.07f, 0.05f, 0.04f), MathX.Step(0.1f, 0.12f, v));
                }
                if (v < 0.04f) c = Color.Lerp(new Color(0.01f, 0.01f, 0.01f), c, MathX.Step(0.026f, 0.04f, v));   // a pinprick pupil
                return c;
            });
            OilDrop = Tex(32, false, (x, y, u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2;
                float hl = MathX.Step(0.42f, 0.08f, Vector2.Distance(p, new Vector2(-0.28f, 0.3f)));
                float c = Mathf.Lerp(0.35f, 1f, hl);
                return new Color(c, c, c, MathX.Step(1f, 0.62f, p.magnitude));
            });
            // a plain soft round drop, tinted by whoever uses it (impact debris, oil running down a wall) - the old drop was
            // red, so chips knocked off concrete flew off dark red
            Drop = Tex(32, false, (x, y, u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2; return new Color(1, 1, 1, MathX.Step(1f, 0.62f, d)); });
            BulletHole = Tex(64, false, (x, y, u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2;
                float r = p.magnitude;
                float hole = Mathf.SmoothStep(0.22f, 0.16f, r);
                float ring = Mathf.SmoothStep(0.85f, 0.3f, r) * (0.4f + MathX.Noise(Mathf.Atan2(p.y, p.x) * 3 + 5, r * 4, 2) * 0.5f);
                float a = Mathf.Max(hole, ring * 0.7f);
                float c = Mathf.Lerp(0.25f, 0.02f, hole);
                return new Color(c, c * 0.95f, c * 0.9f, a);
            });
            Spark = Tex(32, false, (x, y, u, v) => { float dx = Mathf.Abs(u - 0.5f) * 2, dy = Mathf.Abs(v - 0.5f) * 2; float a = Mathf.Clamp01(1 - dx * 4) * Mathf.Clamp01(1 - dy); return new Color(1, 0.8f, 0.4f, a); });
            Vignette = Tex(128, false, (x, y, u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2; return new Color(1, 1, 1, Mathf.SmoothStep(0.45f, 1.05f, d)); });
        }

        static Color Gray(float v) => new Color(v, v, v, 1);

        static float Scratch(float u, float v, int seed)
        {
            float best = 0;
            for (int i = 0; i < 26; i++)
            {
                float x0 = MathX.Hash(i * 1.7f + seed), y0 = MathX.Hash(i * 2.9f + seed * 3);
                float ang = MathX.Hash(i * 5.3f + seed) * Mathf.PI;
                float len = 0.05f + MathX.Hash(i * 9.1f + seed) * 0.25f;
                Vector2 a = new Vector2(x0, y0), dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 p = new Vector2(u, v) - a;
                float t = Mathf.Clamp(Vector2.Dot(p, dir), 0, len);
                float d = (p - dir * t).magnitude;
                best = Mathf.Max(best, Mathf.Clamp01(1 - d * 600f));
            }
            return best;
        }

        delegate Color PixelFn(int x, int y, float u, float v);

        static Color32[] _lastPx; static int _lastSize;

        static Texture2D Tex(int size, bool mips, PixelFn fn, bool linear = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, mips, linear);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            var px = new Color32[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = fn(x, y, x * inv, y * inv);
            _lastPx = px; _lastSize = size;
            t.SetPixels32(px);
            t.Apply(mips, true);
            return t;
        }

        static Texture2D NormalFrom(Texture2D src, float strength)
        {
            // Builds a tangent-space normal map from the pixels of the texture generated just before.
            int size = _lastSize;
            var h = _lastPx;
            var n = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float l = Lum(h[y * size + ((x - 1 + size) % size)]), r = Lum(h[y * size + ((x + 1) % size)]);
                    float d = Lum(h[((y - 1 + size) % size) * size + x]), u = Lum(h[((y + 1) % size) * size + x]);
                    Vector3 nv = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * size + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1);
                }
            n.SetPixels32(px);
            n.wrapMode = TextureWrapMode.Repeat;
            n.Apply(true, true);
            return n;
        }

        static float Lum(Color32 c) => (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
    }
}
