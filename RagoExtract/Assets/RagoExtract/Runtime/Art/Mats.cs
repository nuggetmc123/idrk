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
        static Material _lit, _unlit, _holo, _sky;
        static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        public static Texture2D Grime, Noise, Concrete, ConcreteN, Metal, MetalN, Wood, WoodN, Ground, Grass, Dirt, Asphalt, AsphaltN,
            Corrugated, CorrugatedN, Fabric, FabricN, Camo, Brick, BrickN, Tiles, TilesN, Bark, Leaves, Plaster;
        public static Texture2D Soft, Flash, Smoke, BloodSplat, BloodDrop, BulletHole, Spark, Vignette, Glow, Panel;
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
            if (_sky == null) _sky = new Material(FindShader("Rago/Sky", "Skybox/Procedural"));
            BuildTextures();
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
            m.SetFloat("_Size", size);
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
        public static Material GunMetal => Lit("gunmetal", new Color(0.13f, 0.135f, 0.14f), 0.85f, 0.55f, Metal, MetalN, 0.25f, 0.55f);
        public static Material GunBlued => Lit("gunblued", new Color(0.09f, 0.1f, 0.12f), 0.9f, 0.7f, Metal, MetalN, 0.15f, 0.35f);
        public static Material Polymer => Lit("polymer", new Color(0.075f, 0.075f, 0.08f), 0.0f, 0.28f, Noise, null, 0.3f, 0.12f);
        public static Material PolymerFde => Lit("polyfde", new Color(0.55f, 0.46f, 0.33f), 0.0f, 0.25f, Noise, null, 0.45f, 0.2f);
        public static Material PolymerOd => Lit("polyod", new Color(0.26f, 0.29f, 0.2f), 0.0f, 0.25f, Noise, null, 0.45f, 0.2f);
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
            BloodSplat = Tex(128, false, (x, y, u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2;
                float r = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
                float edge = 0.45f + MathX.Fbm(Mathf.Cos(ang) * 2 + 4, Mathf.Sin(ang) * 2 + 4, 4, 170) * 0.5f;
                float body = Mathf.SmoothStep(edge, edge - 0.08f, r);
                float drops = 0;
                for (int i = 0; i < 9; i++)
                {
                    float a2 = MathX.Hash(i * 3.1f) * 6.283f, dr = 0.55f + MathX.Hash(i * 7.7f) * 0.35f;
                    Vector2 c = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * dr;
                    drops = Mathf.Max(drops, Mathf.SmoothStep(0.07f, 0.04f, Vector2.Distance(p, c) / (0.6f + MathX.Hash(i * 1.3f))));
                }
                float a = Mathf.Max(body, drops);
                float dark = 0.75f + MathX.TFbm(u, v, 8, 8, 3, 171) * 0.35f;
                return new Color(0.38f * dark, 0.02f, 0.02f, a * 0.95f);
            });
            BloodDrop = Tex(32, false, (x, y, u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2; return new Color(0.55f, 0.03f, 0.03f, Mathf.SmoothStep(1f, 0.7f, d)); });
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
