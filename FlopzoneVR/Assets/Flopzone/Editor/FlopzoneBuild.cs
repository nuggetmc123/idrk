using System.IO;
using UnityEditor;

namespace Flopzone.EditorTools
{
    public static class FlopzoneBuild
    {
        public static void BuildApk(bool run)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Flopzone: Android support needed",
                    "To make an APK, Unity needs Android Build Support.\n\nOpen Unity Hub > Installs > the gear next to your Unity version > Add modules, and tick 'Android Build Support' (with OpenJDK and Android SDK & NDK Tools). Then reopen this project.", "OK");
                return;
            }
            if (!File.Exists(FlopzoneSetup.ScenePath)) FlopzoneSetup.RunAll(true);
            if (!FlopzoneSetup.XRLoaderAssigned(BuildTargetGroup.Android) &&
                !EditorUtility.DisplayDialog("Flopzone", "VR isn't switched on for Android yet, so the APK would run flat. Build anyway?\n\n(Use Flopzone > Set Up VR (Quest) first to fix this.)", "Build anyway", "Cancel"))
                return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds");
            string path = Path.GetFullPath("Builds/Flopzone.apk");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { FlopzoneSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                FlopzoneSetup.LastMessage = "APK built: " + path;
                if (!run) EditorUtility.RevealInFinder(path);
                EditorUtility.DisplayDialog("Flopzone", "APK ready:\n" + path + "\n\nInstall it on your Quest with SideQuest or the Meta Quest Developer Hub (developer mode on).", "OK");
            }
            else
            {
                FlopzoneSetup.LastMessage = "APK build failed (" + report.summary.totalErrors + " errors). See the Console for details.";
                EditorUtility.DisplayDialog("Flopzone", FlopzoneSetup.LastMessage, "OK");
            }
        }
    }
}
