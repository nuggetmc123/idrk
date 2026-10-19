using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// Menu: Nullbound > Build for Quest (APK). Sets every Android/Quest setting the
    /// Meta Horizon Store needs (64-bit ARM, IL2CPP, Vulkan, Android 10+ / target 12L,
    /// landscape, internet access, a proper package name, a new version number each
    /// time) and builds Builds/Nullbound.apk. That APK can be installed on a headset with
    /// Meta Quest Developer Hub, or uploaded to the store for friends to download.
    /// </summary>
    public static class QuestBuild
    {
        const string OutputFolder = "Builds";
        const string ApkName = "Nullbound.apk";

        [MenuItem("Nullbound/Build for Quest (APK)", priority = 3)]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Nullbound - Build for Quest",
                    "Unity's Android Build Support isn't installed yet.\n\n" +
                    "Open Unity Hub > Installs > the gear next to your Unity version > Add modules, tick ANDROID BUILD SUPPORT " +
                    "(with OpenJDK and Android SDK & NDK Tools), install, restart Unity and try again.", "OK");
                return;
            }
            if (!VRSetup.EnsureQuestReady())
            {
                EditorUtility.DisplayDialog("Nullbound - Build for Quest", "VR support isn't installed yet. Click Nullbound > Set Up VR (Quest) first, then build again.", "OK");
                return;
            }
            var vrProblems = VRSetup.QuestProblems();
            if (vrProblems.Count > 0)
            {
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                EditorUtility.DisplayDialog("Nullbound - Build for Quest",
                    "The APK would open as a flat window with no controllers (you couldn't move). Unity didn't let me switch these on, " +
                    "so please tick them on the ANDROID tab (robot icon) of the page that just opened, then build again:\n\n- " +
                    string.Join("\n- ", vrProblems), "OK");
                return;
            }
            if (!CheckMultiplayer()) return;
            if (!ArenaBuilder.WorldUpToDate &&
                EditorUtility.DisplayDialog("Nullbound - Build for Quest", "The game world is out of date. Rebuild it first? (Recommended)", "Rebuild world", "Build as is"))
                ArenaBuilder.BuildWorld();

            ApplySettings();
            QuestStoreReady.PlainApk();
            if (!QuestStoreReady.EnsureSigning()) return;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayProgressBar("Nullbound", "Switching to Android (the first time this takes a few minutes)...", 0.3f);
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                EditorUtility.ClearProgressBar();
            }

            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, ApkName);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            // Bake Photon's scene objects (the GameState) into the saved scene; without it the game state never starts on the headset.
            if (SessionManager.MultiplayerAvailable)
                foreach (var scenePath in scenes) FusionBake.BakeAndSave(scenePath);
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
                EditorUtility.DisplayDialog("Nullbound - Build for Quest",
                    "The build didn't finish (" + report.summary.result + "). The red errors in the Console say why; send me a screenshot of them.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("Nullbound - Build for Quest",
                "Built " + path + " (version " + PlayerSettings.bundleVersion + ", code " + PlayerSettings.Android.bundleVersionCode + ").\n\n" +
                "Put it on YOUR headset: Meta Quest Developer Hub > Device Manager > drag the APK in.\n\n" +
                "Share it with friends: upload THIS file to your app in the Meta Horizon developer dashboard (a release channel), and " +
                "invite them. It's signed with your store key (" + PlayerSettings.Android.keystoreName + "): back that up, every update needs it.\n\n" +
                "If you put an earlier debug-signed copy on your headset by hand, uninstall it first, or the store version can't install over it.",
                "OK");
        }

        /// <summary>
        /// Online play in the APK needs Photon Fusion installed AND the App ID inside Fusion's settings asset
        /// (that asset is what the headset reads). Copies the App ID from Nullbound > Set Up Multiplayer into it.
        /// </summary>
        static bool CheckMultiplayer()
        {
            string problem = null;
            if (!SessionManager.MultiplayerAvailable)
                problem = "Photon Fusion 2 isn't installed in this project, so the APK can't go online.";
            else
            {
                string saved = MultiplayerSetup.SavedAppId;
                if (!string.IsNullOrEmpty(saved) && MultiplayerSetup.FusionAppId != saved) MultiplayerSetup.TryApplyAppId(saved);
                if (string.IsNullOrEmpty(MultiplayerSetup.FusionAppId))
                    problem = "No Photon App ID is set, so the APK can't go online (you'd play solo).\n\n" +
                              "Paste it in Nullbound > Set Up Multiplayer (or Tools > Fusion > Fusion Hub), then build again.";
            }
            if (problem == null) return true;
            Debug.LogWarning("[Nullbound] " + problem);
            return EditorUtility.DisplayDialog("Nullbound - Build for Quest", problem, "Build anyway (solo only)", "Cancel");
        }

        /// <summary>Everything Meta requires for a Quest app, plus sensible defaults.</summary>
        static void ApplySettings()
        {
            var android = NamedBuildTarget.Android;
            if (PlayerSettings.productName == "Nullbound" || PlayerSettings.productName.StartsWith("New Unity Project") || string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "Nullbound";
            if (PlayerSettings.companyName == "DefaultCompany" || string.IsNullOrEmpty(PlayerSettings.companyName))
                PlayerSettings.companyName = "NullboundGames";

            // A unique package name (the store's ID for your app; it can't change after your first upload).
            string id = PlayerSettings.GetApplicationIdentifier(android);
            if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany") || id == "com.Company.ProductName")
            {
                string company = Regex.Replace(PlayerSettings.companyName.ToLowerInvariant(), "[^a-z0-9]", "");
                if (company.Length == 0 || char.IsDigit(company[0])) company = "nullbound" + company;
                PlayerSettings.SetApplicationIdentifier(android, "com." + company + ".nullbound");
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

            // The Meta store rejects any APK whose install location isn't "auto".
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            DropQuest1();

            // Quest's OpenXR runs on the classic Android "Activity" (Unity 6 defaults to GameActivity).
            SetEnum(typeof(PlayerSettings.Android), "applicationEntry", "Activity");
            // Don't let the build strip code Photon finds by reflection (that broke joining online).
            if (System.Enum.IsDefined(typeof(ManagedStrippingLevel), "Minimal"))
                PlayerSettings.SetManagedStrippingLevel(android, (ManagedStrippingLevel)System.Enum.Parse(typeof(ManagedStrippingLevel), "Minimal"));
            WriteLinkXml();

            // The store refuses an upload whose version code isn't higher than the last one.
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "1.0";
            // Counting up from the project's saved number broke whenever a fresh copy of the project was opened (the
            // count started again from an old number). So the code is the number of minutes since 2025: every build
            // is higher than every build before it, on any computer or project copy.
            int byClock = (int)((System.DateTime.UtcNow - new System.DateTime(2025, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalMinutes);
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(PlayerSettings.Android.bundleVersionCode + 1, byClock);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Meta no longer supports the original Quest 1. Unticks it in OpenXR's Meta Quest Support target devices so
        /// the store doesn't warn about it. Uses reflection because the OpenXR package might not be there.
        /// </summary>
        static void DropQuest1()
        {
            try
            {
                const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                System.Type settingsType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    settingsType = settingsType ?? asm.GetType("UnityEngine.XR.OpenXR.OpenXRSettings", false);
                var settings = settingsType?.GetMethod("GetSettingsForBuildTargetGroup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { BuildTargetGroup.Android });
                var getFeatures = settingsType?.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetFeatures" && !m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                if (!(getFeatures?.Invoke(settings, null) is System.Array features)) return;
                foreach (var feature in features)
                {
                    if (feature == null || feature.GetType().Name != "MetaQuestFeature") continue;
                    if (!(feature.GetType().GetField("targetDevices", any)?.GetValue(feature) is System.Collections.IList devices)) return;
                    for (int i = 0; i < devices.Count; i++)
                    {
                        object device = devices[i]; // a struct: change the copy, then put it back
                        var name = device.GetType().GetField("manifestName", any)?.GetValue(device) as string;
                        var enabled = device.GetType().GetField("enabled", any);
                        if (name != "quest" || enabled == null) continue;
                        enabled.SetValue(device, false);
                        devices[i] = device;
                        if (feature is Object o) EditorUtility.SetDirty(o);
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't untick Quest 1: " + e.Message); }
        }

        const string LinkXmlPath = "Assets/Nullbound/link.xml";

        /// <summary>Keeps the game's and Photon's code whole in the build. Lists only assemblies this project has.</summary>
        static void WriteLinkXml()
        {
            var keep = new[] { "Assembly-CSharp", "Fusion.Runtime", "Fusion.Common", "Fusion.Sockets", "Fusion.Realtime", "Fusion.Log", "Fusion.Unity" };
            var present = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name);
            var lines = keep.Where(present.Contains).Select(n => "  <assembly fullname=\"" + n + "\" preserve=\"all\"/>");
            string xml = "<linker>\n" + string.Join("\n", lines) + "\n</linker>\n";
            if (File.Exists(LinkXmlPath) && File.ReadAllText(LinkXmlPath) == xml) return;
            File.WriteAllText(LinkXmlPath, xml);
            AssetDatabase.ImportAsset(LinkXmlPath);
        }

        /// <summary>Sets a static enum property by name, if this Unity version has it.</summary>
        static void SetEnum(System.Type type, string property, string value)
        {
            var p = type.GetProperty(property, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (p == null || !p.CanWrite || !p.PropertyType.IsEnum) return;
            try { p.SetValue(null, System.Enum.Parse(p.PropertyType, value)); }
            catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't set " + property + ": " + e.Message); }
        }
    }
}
