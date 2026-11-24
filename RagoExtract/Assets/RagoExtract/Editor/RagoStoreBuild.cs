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
            if (changed)
            {
                Debug.Log("[Rago Extract] Meta store settings applied: Android target SDK " + TargetSdk + ", install location auto" + (building ? " (before this build)" : ""));
                if (!building) AssetDatabase.SaveAssets();
            }
        }
    }
}
