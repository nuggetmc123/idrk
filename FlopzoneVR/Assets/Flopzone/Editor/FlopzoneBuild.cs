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
                EditorUtility.DisplayDialog("Sloppy: Android support needed",
                    "To make an APK, Unity needs Android Build Support.\n\nOpen Unity Hub > Installs > the gear next to your Unity version > Add modules, and tick 'Android Build Support' (with OpenJDK and Android SDK & NDK Tools). Then reopen this project.", "OK");
                return;
            }
            if (!File.Exists(FlopzoneSetup.ScenePath)) FlopzoneSetup.RunAll(true);
            if (!FlopzoneSetup.XRLoaderAssigned(BuildTargetGroup.Android) &&
                !EditorUtility.DisplayDialog("Sloppy", "VR isn't switched on for Android yet, so the APK would run flat. Build anyway?\n\n(Use Sloppy > Set Up VR (Quest) first to fix this.)", "Build anyway", "Cancel"))
                return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(RunKey, run);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                // switching recompiles every script; finish the build once Unity is done (even across its reload)
                SessionState.SetBool(PendingKey, true);
                FlopzoneSetup.LastMessage = "Switching to Android, then building the APK...";
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                WaitThenBuild();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SessionState.SetBool(PendingKey, true);
                WaitThenBuild();
                return;
            }
            Build(run);
        }

        const string PendingKey = "Flopzone.BuildPending", RunKey = "Flopzone.BuildRun";
        static double quietSince;

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (SessionState.GetBool(PendingKey, false)) WaitThenBuild();
        }

        static void WaitThenBuild()
        {
            quietSince = -1;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { quietSince = -1; return; }
            // wait until Unity has been idle for a moment (setting changes can start another compile)
            if (quietSince < 0) { quietSince = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - quietSince < 2.0) return;
            EditorApplication.update -= Tick;
            if (!SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            Build(SessionState.GetBool(RunKey, false));
        }

        static void Build(bool run)
        {
            EditorUserBuildSettings.buildAppBundle = false;
            FlopzoneSetup.ApplyPlayerSettings();   // Android 12L (API 32) minimum: OpenXR needs at least 24, Quest wants 32
            Directory.CreateDirectory("Builds");
            string path = Path.GetFullPath("Builds/Sloppy.apk");
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
                EditorUtility.DisplayDialog("Sloppy", "APK ready:\n" + path + "\n\nInstall it on your Quest with SideQuest or the Meta Quest Developer Hub (developer mode on).", "OK");
            }
            else
            {
                FlopzoneSetup.LastMessage = "APK build failed (" + report.summary.totalErrors + " errors). See the Console for details.";
                EditorUtility.DisplayDialog("Sloppy", FlopzoneSetup.LastMessage, "OK");
            }
        }
    }
}
