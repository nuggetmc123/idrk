using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rago.EditorTools
{
    /// <summary>
    /// Meta Horizon Store build rules, checked every time the editor loads and again right before every Android build
    /// (so an old project gets fixed too, not only a new one):
    ///   - target Android API 34 (the store rejects 35 for now: "targetSdkVersion ... should be 34 or lower")
    ///   - install location "auto" (android:installLocation; leaving it out means internalOnly, which is also rejected)
    ///   - version code from the game version (3.06 -> 306): every upload needs a higher one
    ///   - no engine code stripping and the least managed stripping (see link.xml): the game builds its world in code,
    ///     so stripping removed parts it needs and the build crashed on boot
    /// </summary>
    [InitializeOnLoad]
    public class RagoStoreBuild : IPreprocessBuildWithReport
    {
        public const int TargetSdk = 34;
        public int callbackOrder => -100;

        static RagoStoreBuild() { EditorApplication.delayCall += () => Apply(false); }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Apply(true);
        }

        public static void Apply(bool building)
        {
            bool changed = false;
            try
            {
                if ((int)PlayerSettings.Android.targetSdkVersion != TargetSdk)
                {
                    PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
                    changed = true;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago Extract] couldn't set the Android target SDK: " + e.Message); }
            try
            {
                if (PlayerSettings.Android.preferredInstallLocation != AndroidPreferredInstallLocation.Auto)
                {
                    PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
                    changed = true;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago Extract] couldn't set the install location: " + e.Message); }
            // the world is built entirely in code at runtime (nothing sits in a scene), so code stripping can't tell what's
            // used: a stripped build crashed on boot. No engine stripping, the least managed stripping (and link.xml)
            // version: the store refuses an APK whose version code isn't higher than the last one uploaded, so it comes
            // from the game version (menu > NOTES): 3.06 -> code 306, name "3.06". Never lowered.
            try
            {
                string ver = Rago.PatchNotes.All[0].version;
                if (decimal.TryParse(ver, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v))
                {
                    int code = (int)System.Math.Round(v * 100m);
                    if (PlayerSettings.Android.bundleVersionCode < code) { PlayerSettings.Android.bundleVersionCode = code; changed = true; }
                    if (PlayerSettings.bundleVersion != ver) { PlayerSettings.bundleVersion = ver; changed = true; }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago Extract] couldn't set the version code: " + e.Message); }
            try { if (PlayerSettings.stripEngineCode) { PlayerSettings.stripEngineCode = false; changed = true; } }
            catch (System.Exception e) { Debug.LogWarning("[Rago Extract] couldn't turn off engine stripping: " + e.Message); }
            try
            {
#if UNITY_2021_2_OR_NEWER
                var nt = UnityEditor.Build.NamedBuildTarget.Android;
                if (PlayerSettings.GetManagedStrippingLevel(nt) != ManagedStrippingLevel.Minimal) { PlayerSettings.SetManagedStrippingLevel(nt, ManagedStrippingLevel.Minimal); changed = true; }
#else
                if (PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Android) != ManagedStrippingLevel.Low) { PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low); changed = true; }
#endif
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago Extract] couldn't set code stripping: " + e.Message); }
            if (changed)
            {
                Debug.Log("[Rago Extract] build settings applied: Android target SDK " + TargetSdk + ", install location auto, no engine code stripping, minimal managed stripping" + (building ? " (before this build)" : ""));
                if (!building) AssetDatabase.SaveAssets();
            }
        }
    }
}
