using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Helpers for building models, materials and world-space text from code.
    /// TextMeshPro is used (not the legacy TextMesh) because legacy text only
    /// renders in one eye on Quest's single-pass stereo rendering.
    /// </summary>
    public static class NBUI
    {
        /// <summary>
        /// Lit material that runtime-made parts copy (set by SessionManager). Using a
        /// material asset from the scene guarantees its shader is included in builds.
        /// </summary>
        public static Material BaseMaterial;

        /// <summary>Additive particle material for flames and sparks (set by SessionManager).</summary>
        public static Material ParticleMaterial;


        static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        // ---------------- Materials ----------------

        public static void SetColor(Material m, Color color)
        {
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        }

        static void SetSurface(Material m, float smoothness, float metallic)
        {
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness); // Standard shader
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness); // URP Lit
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        }

        static void SetEmission(Material m, Color emission)
        {
            if (emission.maxColorComponent <= 0f) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }

        /// <summary>A shared material; identical requests return the same instance (cheaper to draw).</summary>
        public static Material Mat(Color color, float smoothness = 0.3f, float metallic = 0f, Color emission = default(Color), Texture texture = null)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + smoothness + "|" + metallic + "|" +
                         ColorUtility.ToHtmlStringRGBA(emission) + "|" + (texture ? texture.GetInstanceID() : 0);
            if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;

            var m = BaseMaterial != null ? new Material(BaseMaterial) : new Material(Shader.Find("Standard"));
            SetColor(m, color);
            SetSurface(m, smoothness, metallic);
            SetEmission(m, emission);
            if (texture != null)
            {
                m.mainTexture = texture;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            }
            Materials[key] = m;
            return m;
        }

        public static Material Paint(Color c) => Mat(c, 0.75f, 0.1f);
        public static Material Chrome() => Mat(new Color(0.85f, 0.87f, 0.9f), 0.9f, 1f);
        public static Material Rubber() => Mat(new Color(0.08f, 0.08f, 0.09f), 0.15f);
        public static Material Carbon() => Mat(new Color(0.13f, 0.13f, 0.15f), 0.55f, 0.3f);
        public static Material Glow(Color c) => Mat(c, 0.5f, 0f, c * 1.6f);

        /// <summary>Recolor a renderer with its own material copy (used by poke buttons).</summary>
        public static void Paint(Renderer r, Color color)
        {
            var m = BaseMaterial != null ? new Material(BaseMaterial) : r.material;
            SetColor(m, color);
            r.material = m;
        }

        // ---------------- Model parts (no colliders) ----------------

        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (Meshes.TryGetValue(type, out var mesh) && mesh != null) return mesh;
            var temp = GameObject.CreatePrimitive(type);
            mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            Meshes[type] = mesh;
            return mesh;
        }

        /// <summary>A visual-only primitive: mesh + renderer, no collider (so it never affects physics).</summary>
        public static Transform Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat,
            Vector3 euler = default(Vector3), string name = null)
        {
            var go = new GameObject(name ?? type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(type);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>Stretch a capsule/cylinder part so it spans from a to b (world space).</summary>
        public static void Span(Transform part, Vector3 a, Vector3 b, float thickness, bool capsule = true)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            part.position = (a + b) * 0.5f;
            part.rotation = len > 0.0001f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            // A capsule's height is 2*y and includes its round caps, so add the thickness to reach both ends.
            float y = capsule ? (len + thickness) * 0.5f : len * 0.5f;
            part.localScale = new Vector3(thickness, y, thickness);
        }

        // ---------------- Particles ----------------

        /// <summary>A flame/spark emitter that fires along the parent's -Z (out of a nozzle at the back).</summary>
        public static ParticleSystem Flame(Transform parent, Vector3 localPos, float size, Color inner, Color outer)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.22f;
            main.startSpeed = 5f;
            main.startSize = size;
            main.startColor = new ParticleSystem.MinMaxGradient(inner, outer);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 70f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = size * 0.25f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (ParticleMaterial != null) r.sharedMaterial = ParticleMaterial;
            return ps;
        }

        // ---------------- Text ----------------

        static TMP_FontAsset uiFont, signFont;
        static bool fontsLoaded;

        /// <summary>Barlow SemiBold, for menus, panels and popups (null = TMP's default font).</summary>
        public static TMP_FontAsset UIFont { get { LoadFonts(); return uiFont; } }

        /// <summary>Rajdhani Bold, for signs and boards in the world (null = TMP's default font).</summary>
        public static TMP_FontAsset SignFont { get { LoadFonts(); return signFont; } }

        /// <summary>
        /// The fonts in Resources/Fonts (both SIL Open Font License, see the OFL files there) become dynamic TMP font
        /// assets the first time they're needed. They use the default font's shader (always in the build) and fall
        /// back to the default font for any character they lack. If anything fails, text keeps the default font.
        /// </summary>
        static void LoadFonts()
        {
            if (fontsLoaded) return;
            fontsLoaded = true;
            uiFont = MakeFont("Fonts/Barlow-SemiBold");
            signFont = MakeFont("Fonts/Rajdhani-Bold") ?? uiFont;
        }

        static TMP_FontAsset MakeFont(string path)
        {
            var source = Resources.Load<Font>(path);
            if (source == null) return null;
            try
            {
                var fa = TMP_FontAsset.CreateFontAsset(source);
                var fallback = TMP_Settings.defaultFontAsset;
                if (fa == null || fa.material == null) return null;
                if (fallback != null)
                {
                    if (fallback.material != null) fa.material.shader = fallback.material.shader;
                    if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    fa.fallbackFontAssetTable.Add(fallback);
                }
                return fa;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Nullbound: couldn't make a font from " + path + ", using the default font. " + e.Message);
                return null;
            }
        }

        /// <summary>Give a text the UI font (or the sign font).</summary>
        public static void UseFont(TMP_Text t, bool sign = false)
        {
            var f = sign ? SignFont : UIFont;
            if (f != null && t != null) t.font = f;
        }

        /// <summary>Signs, boards and labels built into the scene get the sign font (called when a scene loads).</summary>
        public static void ApplySceneFonts()
        {
            if (SignFont == null) return;
            foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                t.font = signFont;
        }

        /// <param name="fontSize">Roughly: 10 = one metre tall text.</param>
        public static TextMeshPro Label(Transform parent, string text, Vector3 localPos, float fontSize,
            Color color, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            UseFont(tmp);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }

        /// <summary>Keep a label on one line, shrinking it to fit its box instead of wrapping (titles, IDs).</summary>
        public static TextMeshPro Fit(TextMeshPro t, float minSize)
        {
            t.enableWordWrapping = false;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = minSize;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A visual box (no collider) with a shared material.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            return Part(parent, PrimitiveType.Cube, localPos, scale, Mat(color, 0.4f), default(Vector3), name).gameObject;
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return m + ":" + s.ToString("00.0");
        }

        public static string FormatCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 6) return code;
            return code.Substring(0, 3) + " " + code.Substring(3);
        }
    }
}
