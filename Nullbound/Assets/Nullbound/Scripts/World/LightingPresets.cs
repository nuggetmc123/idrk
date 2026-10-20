using UnityEngine;
using UnityEngine.Rendering;

namespace Nullbound
{
    /// <summary>The lighting of one place: sun, ambient, fog and sky. Colours are sRGB (Unity converts them for Linear).</summary>
    public sealed class LightingPreset
    {
        public string name;
        // The key light (the scene's one directional light).
        public Vector3 sunEuler;
        public Color sunColor;
        public float sunIntensity;
        public float shadowStrength;
        // Ambient light (Trilight: from above, from the sides, from below).
        public Color ambientSky, ambientEquator, ambientGround;
        // Distance fog (linear). Its colour should match the sky's horizon so there's no band.
        public Color fogColor;
        public float fogStart, fogEnd;
        // Skybox/Procedural parameters.
        public Color skyTint, skyGround;
        public float skyThickness, skyExposure, sunSize;
        // Reflections where no reflection probe covers (a soft gradient cubemap: top, horizon, bottom).
        public Color reflectTop, reflectHorizon, reflectBottom;
        public float reflectionIntensity = 1f;
    }

    /// <summary>
    /// One lighting preset per place, shared by the world builder (editor) and MapSky (runtime), so what you see
    /// while building is what you get in play. Training and the main menu are indoor test-lab looks (a neutral
    /// cool-white key light from high above, bright neutral fill, light haze); the Battleground is a field test
    /// site under a warm late-afternoon sun.
    /// </summary>
    public static class LightingPresets
    {
        public static readonly LightingPreset Menu = new LightingPreset
        {
            name = "Menu",
            sunEuler = new Vector3(64f, 150f, 0f),
            sunColor = new Color(0.93f, 0.96f, 1f),
            sunIntensity = 0.9f,
            shadowStrength = 0.7f,
            ambientSky = new Color(0.52f, 0.55f, 0.6f),
            ambientEquator = new Color(0.42f, 0.43f, 0.45f),
            ambientGround = new Color(0.2f, 0.2f, 0.21f),
            fogColor = new Color(0.13f, 0.14f, 0.16f),
            fogStart = 25f, fogEnd = 140f,
            skyTint = new Color(0.32f, 0.34f, 0.4f), skyGround = new Color(0.07f, 0.07f, 0.08f),
            skyThickness = 0.6f, skyExposure = 0.45f, sunSize = 0.02f,
            reflectTop = new Color(0.62f, 0.64f, 0.68f), reflectHorizon = new Color(0.38f, 0.39f, 0.41f), reflectBottom = new Color(0.12f, 0.12f, 0.13f),
            reflectionIntensity = 0.9f,
        };

        public static readonly LightingPreset Training = new LightingPreset
        {
            name = "Training",
            sunEuler = new Vector3(68f, -32f, 0f),
            sunColor = new Color(0.95f, 0.97f, 1f),
            sunIntensity = 1.05f,
            shadowStrength = 0.72f,
            ambientSky = new Color(0.62f, 0.64f, 0.68f),
            ambientEquator = new Color(0.5f, 0.51f, 0.52f),
            ambientGround = new Color(0.3f, 0.3f, 0.31f),
            fogColor = new Color(0.68f, 0.71f, 0.75f),
            fogStart = 35f, fogEnd = 260f,
            skyTint = new Color(0.46f, 0.5f, 0.58f), skyGround = new Color(0.42f, 0.42f, 0.43f),
            skyThickness = 0.75f, skyExposure = 1.05f, sunSize = 0.03f,
            reflectTop = new Color(0.86f, 0.88f, 0.9f), reflectHorizon = new Color(0.62f, 0.64f, 0.66f), reflectBottom = new Color(0.3f, 0.3f, 0.31f),
            reflectionIntensity = 1f,
        };

        public static readonly LightingPreset Battleground = new LightingPreset
        {
            name = "Battleground",
            sunEuler = new Vector3(27f, -58f, 0f),
            sunColor = new Color(1f, 0.86f, 0.68f),
            sunIntensity = 1.25f,
            shadowStrength = 0.8f,
            ambientSky = new Color(0.5f, 0.58f, 0.72f),
            ambientEquator = new Color(0.62f, 0.56f, 0.48f),
            ambientGround = new Color(0.27f, 0.24f, 0.2f),
            fogColor = new Color(0.8f, 0.72f, 0.62f),
            fogStart = 70f, fogEnd = 380f,
            skyTint = new Color(0.5f, 0.53f, 0.62f), skyGround = new Color(0.38f, 0.34f, 0.3f),
            skyThickness = 1.2f, skyExposure = 1.15f, sunSize = 0.04f,
            reflectTop = new Color(0.55f, 0.65f, 0.82f), reflectHorizon = new Color(0.85f, 0.75f, 0.62f), reflectBottom = new Color(0.3f, 0.27f, 0.22f),
            reflectionIntensity = 1f,
        };

        public static LightingPreset For(bool inMenu, ArenaMap map) => inMenu ? Menu : map == ArenaMap.Battleground ? Battleground : Training;

        /// <summary>Sets up a Skybox/Procedural material for the preset (the material can be an asset or made at runtime).</summary>
        public static void SetUpSky(Material sky, LightingPreset p)
        {
            if (sky == null) return;
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", p.skyTint);
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", p.skyGround);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", p.skyThickness);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", p.skyExposure);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", p.sunSize);
            if (sky.HasProperty("_SunSizeConvergence")) sky.SetFloat("_SunSizeConvergence", 6f);
            // A simple sun disc (the keyword has to be set from code; the scene's Sky material keeps the variant in builds).
            if (sky.HasProperty("_SunDisk")) sky.SetFloat("_SunDisk", 1f);
            sky.DisableKeyword("_SUNDISK_NONE");
            sky.DisableKeyword("_SUNDISK_HIGH_QUALITY");
            sky.EnableKeyword("_SUNDISK_SIMPLE");
        }

        /// <summary>The scene's key light: RenderSettings.sun, or else the first directional light.</summary>
        public static Light FindSun()
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional) return sun;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        /// <summary>
        /// Applies everything in the preset: sky, sun (direction, colour, strength, shadows), Trilight ambient, linear fog
        /// and the fallback reflection cubemap (pass null to keep the current one).
        /// </summary>
        public static void Apply(LightingPreset p, Material sky, Cubemap reflection)
        {
            if (sky != null)
            {
                SetUpSky(sky, p);
                RenderSettings.skybox = sky;
            }
            var sun = FindSun();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = p.shadowStrength;
                sun.shadowBias = 0.04f;
                sun.shadowNormalBias = 0.3f;
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            RenderSettings.reflectionIntensity = p.reflectionIntensity;
            if (reflection != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
#if UNITY_2022_1_OR_NEWER
                RenderSettings.customReflectionTexture = reflection;
#else
                RenderSettings.customReflection = reflection;
#endif
            }
        }

        /// <summary>
        /// A small soft gradient cubemap (top colour above, horizon colour around, bottom colour below) for reflections
        /// outside the baked reflection probes. Cheap enough to make at runtime.
        /// </summary>
        public static Cubemap MakeReflectionCubemap(LightingPreset p, int size = 32)
        {
            var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = "Reflection_" + p.name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Vector3 d = CubeDirection((CubemapFace)face, (x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                        px[y * size + x] = ReflectionColour(p, d.normalized.y);
                    }
                cube.SetPixels(px, (CubemapFace)face);
            }
            cube.Apply(true, false);
            return cube;
        }

        /// <summary>The gradient colour for a direction with this up component (-1 down .. 1 up).</summary>
        public static Color ReflectionColour(LightingPreset p, float up)
        {
            if (up >= 0f) return Color.Lerp(p.reflectHorizon, p.reflectTop, Mathf.SmoothStep(0f, 1f, Mathf.Pow(up, 0.6f)));
            return Color.Lerp(p.reflectHorizon, p.reflectBottom, Mathf.SmoothStep(0f, 1f, Mathf.Pow(-up, 0.5f)));
        }

        /// <summary>World direction of a texel on a cubemap face (s, t in -1..1, t up on the stored image).</summary>
        public static Vector3 CubeDirection(CubemapFace face, float s, float t)
        {
            // Cubemap texel rows run top to bottom, so the image's "up" is -t in face space.
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -t, -s);
                case CubemapFace.NegativeX: return new Vector3(-1f, -t, s);
                case CubemapFace.PositiveY: return new Vector3(s, 1f, t);
                case CubemapFace.NegativeY: return new Vector3(s, -1f, -t);
                case CubemapFace.PositiveZ: return new Vector3(s, -t, 1f);
                default: return new Vector3(-s, -t, -1f);
            }
        }
    }
}
