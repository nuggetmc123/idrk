using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The few real asset references the game needs, made by the editor setup
    /// (runs by itself when the project opens) into Resources/FlopzoneAssets. Everything else —
    /// models, maps, sounds, music — is generated in code at runtime.
    ///
    /// Template materials matter for builds: Unity only ships the shader
    /// variants that some material in the build uses, so each look the game
    /// creates at runtime (emissive, see-through, particles) is cloned from
    /// one of these.
    /// </summary>
    public class GameAssets : ScriptableObject
    {
        public Material lit;
        public Material litEmissive;
        public Material fade;
        public Material unlit;
        public Material sprite;
        public Material particleAdd;
        public Material particleAlpha;
        public Material skybox;
        public Font font;

        static GameAssets instance;

        public static GameAssets I
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameAssets>("FlopzoneAssets");
                    if (instance == null)
                    {
                        Debug.LogWarning("[Flopzone] Resources/FlopzoneAssets is missing; reopen the project to rebuild it. Using fallbacks.");
                        instance = CreateInstance<GameAssets>();
                    }
                    instance.FillFallbacks();
                }
                return instance;
            }
        }

        void FillFallbacks()
        {
            if (lit == null) lit = new Material(Shader.Find("Standard"));
            if (litEmissive == null) { litEmissive = new Material(lit); litEmissive.EnableKeyword("_EMISSION"); }
            if (fade == null) fade = MakeFade(new Material(lit));
            if (unlit == null) unlit = new Material(Shader.Find("Unlit/Color"));
            if (sprite == null) sprite = new Material(Shader.Find("Sprites/Default"));
            if (particleAdd == null) particleAdd = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            if (particleAlpha == null) particleAlpha = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            if (skybox == null) skybox = new Material(Shader.Find("Skybox/Procedural"));
            if (font == null) font = BuiltinFont();
        }

        /// <summary>Standard shader in "Fade" mode (what the material inspector does).</summary>
        public static Material MakeFade(Material m)
        {
            m.SetFloat("_Mode", 2);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }

        public static Font BuiltinFont()
        {
            // Unity 2022.2+ renamed the built-in font; older versions only know Arial.
            Font f = null;
#if UNITY_2022_2_OR_NEWER
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
#else
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
#endif
            if (f == null) f = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "Helvetica", "Liberation Sans" }, 32);
            return f;
        }
    }
}
