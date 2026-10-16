using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Menu: KartVR > Build for Quest (APK). Sets every Android/Quest setting the
    /// Meta Horizon Store needs (64-bit ARM, IL2CPP, Vulkan, Android 10+ / target 12L,
    /// landscape, internet access, a proper package name, a new version number each
    /// time) and builds Builds/KartVR.apk. That APK can be installed on a headset with
    /// Meta Quest Developer Hub, or uploaded to the store for friends to download.
    /// </summary>
    public static class QuestBuild
    {
        const string OutputFolder = "Builds";
        const string ApkName = "KartVR.apk";

        [MenuItem("KartVR/Build for Quest (APK)", priority = 3)]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Kart VR - Build for Quest",
                    "Unity's Android Build Support isn't installed yet.\n\n" +
                    "Open Unity Hub > Installs > the gear next to your Unity version > Add modules, tick ANDROID BUILD SUPPORT " +
                    "(with OpenJDK and Android SDK & NDK Tools), install, restart Unity and try again.", "OK");
                return;
            }
            if (!VRSetup.EnsureQuestReady())
            {
                EditorUtility.DisplayDialog("Kart VR - Build for Quest", "VR support isn't installed yet. Click KartVR > Set Up VR (Quest) first, then build again.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(MultiplayerSetup.SavedAppId) &&
                !EditorUtility.DisplayDialog("Kart VR - Build for Quest",
                    "No Photon App ID is set, so this build can only be played OFFLINE (solo).\n\nSet it with KartVR > Set Up Multiplayer to play online.",
                    "Build anyway", "Cancel"))
                return;
            if (!KartWorldBuilder.WorldUpToDate &&
                EditorUtility.DisplayDialog("Kart VR - Build for Quest", "The game world is out of date. Rebuild it first? (Recommended)", "Rebuild world", "Build as is"))
                KartWorldBuilder.BuildWorld();

            ApplySettings();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayProgressBar("Kart VR", "Switching to Android (the first time this takes a few minutes)...", 0.3f);
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                EditorUtility.ClearProgressBar();
            }

            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, ApkName);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Kart VR - Build for Quest",
                    "The build didn't finish (" + report.summary.result + "). The red errors in the Console say why; send me a screenshot of them.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("Kart VR - Build for Quest",
                "Built " + path + " (version " + PlayerSettings.bundleVersion + ", code " + PlayerSettings.Android.bundleVersionCode + ").\n\n" +
                "Put it on YOUR headset: Meta Quest Developer Hub > Device Manager > drag the APK in.\n\n" +
                "Share it with friends: upload it to your app in the Meta Horizon developer dashboard and invite them to a release channel." +
                (PlayerSettings.Android.useCustomKeystore ? "" :
                    "\n\nNOTE: this APK is signed with Unity's debug key. That's fine on your own headset, but the store needs your own key: " +
                    "Edit > Project Settings > Player > Android > Publishing Settings > Keystore Manager > Create New. Keep the keystore and passwords safe; " +
                    "you need the same ones for every update."),
                "OK");
        }

        /// <summary>Everything Meta requires for a Quest app, plus sensible defaults.</summary>
        static void ApplySettings()
        {
            var android = NamedBuildTarget.Android;
            if (PlayerSettings.productName == "KartVR" || PlayerSettings.productName.StartsWith("New Unity Project") || string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "Kart VR";
            if (PlayerSettings.companyName == "DefaultCompany" || string.IsNullOrEmpty(PlayerSettings.companyName))
                PlayerSettings.companyName = "KartVRGames";

            // A unique package name (the store's ID for your app; it can't change after your first upload).
            string id = PlayerSettings.GetApplicationIdentifier(android);
            if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany") || id == "com.Company.ProductName")
            {
                string company = Regex.Replace(PlayerSettings.companyName.ToLowerInvariant(), "[^a-z0-9]", "");
                if (company.Length == 0 || char.IsDigit(company[0])) company = "kartvr" + company;
                PlayerSettings.SetApplicationIdentifier(android, "com." + company + ".kartvr");
            }

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP); // required for 64-bit
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;    // Android 10 (all Quest headsets)
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32; // what the Meta store asks for
            PlayerSettings.Android.forceInternetPermission = true;            // online play (Photon)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // The store refuses an upload whose version code isn't higher than the last one.
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, PlayerSettings.Android.bundleVersionCode + 1);
            AssetDatabase.SaveAssets();
        }
    }
}
