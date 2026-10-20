using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// The project's graphics settings, applied from code because a fresh copy of the project starts with Unity's
    /// defaults (which on Quest means Gamma colour, no anti-aliasing and blocky short shadows):
    /// Linear colour, 4x MSAA on every quality level, real shadows, and Quest-friendly Android tier settings.
    /// Called when the project opens, when the world is built and before a Quest build. Safe to call any time:
    /// it only writes settings that are different, and anything Unity might not have is reached by reflection.
    /// </summary>
    public static class RenderSetup
    {
        public const int MsaaSamples = 4;
        public const float ShadowDistance = 30f;

        public static void Apply()
        {
            try { ApplyColourSpace(); } catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't set Linear colour: " + e.Message); }
            try { ApplyQuality(); } catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't set quality levels: " + e.Message); }
            try { ApplyAndroidTiers(); } catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't set Android graphics tiers: " + e.Message); }
        }

        static void ApplyColourSpace()
        {
            // Physically based shading (and every colour in the game) is tuned for Linear. Switching reimports textures once.
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        /// <summary>The quality level Android (Quest) uses: "High" if the project has one, otherwise the middle level.</summary>
        static int AndroidLevel(string[] names)
        {
            for (int i = 0; i < names.Length; i++) if (names[i] == "High") return i;
            return names.Length / 2;
        }

        static void ApplyQuality()
        {
            var names = QualitySettings.names;
            if (names == null || names.Length == 0) return;
            int questLevel = AndroidLevel(names);
            int active = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                bool quest = i == questLevel;
                if (QualitySettings.antiAliasing != MsaaSamples) QualitySettings.antiAliasing = MsaaSamples;
                if (QualitySettings.shadows != ShadowQuality.All) QualitySettings.shadows = ShadowQuality.All;
                var res = quest ? ShadowResolution.Medium : ShadowResolution.High;
                if (QualitySettings.shadowResolution != res) QualitySettings.shadowResolution = res;
                if (QualitySettings.shadowProjection != ShadowProjection.StableFit) QualitySettings.shadowProjection = ShadowProjection.StableFit; // no shimmer when the head moves
                if (!Mathf.Approximately(QualitySettings.shadowDistance, ShadowDistance)) QualitySettings.shadowDistance = ShadowDistance;
                int cascades = quest ? 1 : 2;
                if (QualitySettings.shadowCascades != cascades) QualitySettings.shadowCascades = cascades;
                if (QualitySettings.pixelLightCount != 2) QualitySettings.pixelLightCount = 2;
                if (QualitySettings.anisotropicFiltering != AnisotropicFiltering.Enable) QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                if (QualitySettings.softParticles) QualitySettings.softParticles = false;
                if (QualitySettings.realtimeReflectionProbes) QualitySettings.realtimeReflectionProbes = false;
#if UNITY_2022_2_OR_NEWER
                if (QualitySettings.globalTextureMipmapLimit != 0) QualitySettings.globalTextureMipmapLimit = 0;
#else
                if (QualitySettings.masterTextureLimit != 0) QualitySettings.masterTextureLimit = 0;
#endif
            }
            QualitySettings.SetQualityLevel(Mathf.Clamp(active, 0, names.Length - 1), false);
            SetAndroidDefaultLevel(questLevel);
        }

        /// <summary>
        /// Makes Android use the given quality level by default and makes sure that level isn't switched off for
        /// Android. There's no public API for this in every Unity version, so it edits QualitySettings.asset.
        /// </summary>
        static void SetAndroidDefaultLevel(int level)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null) return;
            var so = new SerializedObject(assets[0]);
            bool changed = false;

            var defaults = so.FindProperty("m_PerPlatformDefaultQuality");
            if (defaults != null && defaults.isArray)
            {
                bool found = false;
                for (int i = 0; i < defaults.arraySize; i++)
                {
                    var pair = defaults.GetArrayElementAtIndex(i);
                    var key = pair.FindPropertyRelative("first");
                    var value = pair.FindPropertyRelative("second");
                    if (key == null || value == null || key.stringValue != "Android") continue;
                    found = true;
                    if (value.intValue != level) { value.intValue = level; changed = true; }
                }
                if (!found)
                {
                    defaults.InsertArrayElementAtIndex(defaults.arraySize);
                    var pair = defaults.GetArrayElementAtIndex(defaults.arraySize - 1);
                    var key = pair.FindPropertyRelative("first");
                    var value = pair.FindPropertyRelative("second");
                    if (key != null && value != null) { key.stringValue = "Android"; value.intValue = level; changed = true; }
                }
            }

            var levels = so.FindProperty("m_QualitySettings");
            if (levels != null && levels.isArray && level < levels.arraySize)
            {
                var excluded = levels.GetArrayElementAtIndex(level).FindPropertyRelative("excludedTargetPlatforms");
                if (excluded != null && excluded.isArray)
                    for (int i = excluded.arraySize - 1; i >= 0; i--)
                        if (excluded.GetArrayElementAtIndex(i).stringValue == "Android") { excluded.DeleteArrayElementAtIndex(i); changed = true; }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Android graphics tiers: full-quality Standard shader, box-projected reflection probes (no blending, which is
        /// cheaper), no HDR (there's no post-processing to use it), forward rendering. Done by reflection because
        /// these settings have moved between Unity versions.
        /// </summary>
        static void ApplyAndroidTiers()
        {
            var settingsType = typeof(Editor).Assembly.GetType("UnityEditor.Rendering.EditorGraphicsSettings");
            var tierType = typeof(Editor).Assembly.GetType("UnityEditor.Rendering.TierSettings");
            var graphicsTier = typeof(GraphicsSettings).Assembly.GetType("UnityEngine.Rendering.GraphicsTier");
            if (settingsType == null || tierType == null || graphicsTier == null) return;
            const BindingFlags pub = BindingFlags.Public | BindingFlags.Static;
            var get = settingsType.GetMethod("GetTierSettings", pub, null, new[] { typeof(BuildTargetGroup), graphicsTier }, null);
            var set = settingsType.GetMethod("SetTierSettings", pub, null, new[] { typeof(BuildTargetGroup), graphicsTier, tierType }, null);
            if (get == null || set == null) return;
            foreach (var tier in System.Enum.GetValues(graphicsTier))
            {
                object ts = get.Invoke(null, new object[] { BuildTargetGroup.Android, tier });
                if (ts == null) continue;
                bool changed = false;
                void Field(string name, object value)
                {
                    var f = tierType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (f == null) return;
                    object v = value;
                    if (f.FieldType.IsEnum)
                    {
                        if (!System.Enum.IsDefined(f.FieldType, value.ToString())) return;
                        v = System.Enum.Parse(f.FieldType, value.ToString());
                    }
                    else if (f.FieldType != value.GetType()) return;
                    if (Equals(f.GetValue(ts), v)) return;
                    f.SetValue(ts, v);
                    changed = true;
                }
                Field("standardShaderQuality", "High");
                Field("reflectionProbeBoxProjection", true);
                Field("reflectionProbeBlending", false);
                Field("hdr", false);
                Field("renderingPath", "Forward");
                Field("cascadedShadowMaps", true);
                if (changed) set.Invoke(null, new object[] { BuildTargetGroup.Android, tier, ts });
            }
        }
    }
}
