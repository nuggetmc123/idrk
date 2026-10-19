using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Nullbound.EditorTools
{
    /// One-click project setup: Quest/Android player settings, OpenXR + Meta Quest support,
    /// materials, the networked player prefab and the game scene. Also the APK build used by CI.
    public static class NBProjectSetup
    {
        const string Root = "Assets/Nullbound";
        const string Gen = Root + "/Generated";
        const string ScenePath = Root + "/Scenes/Nullbound.unity";
        const string PrefabPath = Gen + "/NBPlayer.prefab";
        const string MatSetPath = Root + "/Resources/NBMaterials.asset";
        const string AppId = "com.nuggetmc.nullbound";
        public const string DefaultApkPath = "Builds/Android/Nullbound.apk";

        [MenuItem("Nullbound/Setup Everything (VR + Quest + Scene)", priority = 1)]
        public static void SetupEverything()
        {
            ConfigurePlayer();
            ConfigureXR(BuildTargetGroup.Android, true);
            ConfigureXR(BuildTargetGroup.Standalone, false);   // lets you test over Quest Link from the editor
            BuildMaterials();
            BuildPrefab();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Nullbound] Setup complete. Press Play to test (keyboard+mouse or Quest Link), or Nullbound > Build Quest APK.");
        }

        [MenuItem("Nullbound/Build Quest APK", priority = 2)]
        public static void BuildQuestApkMenu()
        {
            var path = BuildQuestApk(DefaultApkPath);
            if (path != null) EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Nullbound/Open Game Scene", priority = 3)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) SetupEverything();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// Entry point for command-line / GitHub Actions builds:
        ///   Unity -batchmode -projectPath nullbound-vr -executeMethod Nullbound.EditorTools.NBProjectSetup.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            string output = DefaultApkPath;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-customBuildPath" || args[i] == "-apkPath") output = args[i + 1];
            if (Directory.Exists(output) || !output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                output = Path.Combine(output, "Nullbound.apk");
            var result = BuildQuestApk(output);
            if (Application.isBatchMode) EditorApplication.Exit(result != null ? 0 : 1);
        }

        public static string BuildQuestApk(string output)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;
            SetupEverything();

            var dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[Nullbound] APK built: " + output + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB)");
                return output;
            }
            Debug.LogError("[Nullbound] APK build failed: " + report.summary.result);
            return null;
        }

        // ------------------------------------------------------------------ player settings
        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "NuggetMC";
            PlayerSettings.productName = "Nullbound";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1")
                PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = true;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            // Quest wants the classic Activity entry point (Unity 6 defaults to GameActivity).
            var entry = typeof(PlayerSettings.Android).GetProperty("applicationEntry", BindingFlags.Static | BindingFlags.Public);
            if (entry != null && entry.PropertyType.IsEnum)
            {
                try { entry.SetValue(null, Enum.Parse(entry.PropertyType, "Activity")); }
                catch (Exception e) { Debug.LogWarning("[Nullbound] Couldn't set Android entry point: " + e.Message); }
            }

            // The game's materials use the Built-in Render Pipeline. If this was made as a URP/HDRP
            // project, switch rendering back to built-in so nothing shows up pink.
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                Debug.Log("[Nullbound] Switching the project to the Built-in Render Pipeline.");
                GraphicsSettings.defaultRenderPipeline = null;
            }
            int level = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null) QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(level, false);

            // Accept both input backends (the game reads XR devices directly and supports either).
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 2) { prop.intValue = 2; so.ApplyModifiedProperties(); }
            }
        }

        // ------------------------------------------------------------------ XR / OpenXR
        static void ConfigureXR(BuildTargetGroup group, bool quest)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(group))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            if (!general.Manager.activeLoaders.Any(l => l != null && l.GetType().Name == "OpenXRLoader"))
                XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();

            // make sure the OpenXR settings asset exists (the class is internal, so reflect)
            var pkgType = Type.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings, Unity.XR.OpenXR.Editor");
            pkgType?.GetMethod("GetOrCreateInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null);

            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);
            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (oxr == null) { Debug.LogWarning("[Nullbound] OpenXR settings not ready yet for " + group + " - run setup again."); return; }

            var wanted = quest
                ? new[] { "MetaQuestFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "MetaQuestTouchProControllerProfile" }
                : new[] { "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" };
            foreach (var f in oxr.GetFeatures())
            {
                if (f == null || !wanted.Contains(f.GetType().Name) || f.enabled) continue;
                f.enabled = true;
                EditorUtility.SetDirty(f);
            }
            EditorUtility.SetDirty(oxr);
        }

        // ------------------------------------------------------------------ assets
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void BuildMaterials()
        {
            EnsureFolder(Gen);
            EnsureFolder(Root + "/Resources");

            var lit = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/NB_Lit.mat");
            if (lit == null)
            {
                lit = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(lit, Gen + "/NB_Lit.mat");
            }
            // keep the emission variant in the build: the game turns it on per material
            lit.EnableKeyword("_EMISSION");
            lit.SetColor("_EmissionColor", Color.black);
            lit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            lit.enableInstancing = true;
            EditorUtility.SetDirty(lit);

            var glow = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/NB_Glow.mat");
            if (glow == null)
            {
                glow = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(glow, Gen + "/NB_Glow.mat");
            }

            var set = AssetDatabase.LoadAssetAtPath<NBMaterialSet>(MatSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<NBMaterialSet>();
                AssetDatabase.CreateAsset(set, MatSetPath);
            }
            set.lit = lit;
            set.glow = glow;
            set.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        static GameObject BuildPrefab()
        {
            EnsureFolder(Gen);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && existing.GetComponent<NBPlayer>() != null && existing.GetComponent<NetworkObject>() != null)
                return existing;
            var go = new GameObject("NBPlayer");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NBPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildScene()
        {
            EnsureFolder(Root + "/Scenes");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var netGo = new GameObject("NetworkManager");
            var nm = netGo.AddComponent<NetworkManager>();
            var utp = netGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = prefab;
            nm.NetworkConfig.EnableSceneManagement = false;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.TickRate = 30;
            netGo.AddComponent<NBNet>();

            new GameObject("NB_Game").AddComponent<NBGame>();

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.4f, 0.52f);
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    /// First time the project is opened: wait for Unity to finish importing, set everything up,
    /// open the game and say so. On later opens, open the game scene instead of an empty one.
    [InitializeOnLoad]
    static class NBAutoSetup
    {
        const string ScenePath = "Assets/Nullbound/Scenes/Nullbound.unity";
        static double s_Since;

        static NBAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Begin;
        }

        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath))
            {
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty) EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            s_Since = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup - s_Since < 2) return;
            EditorApplication.update -= Tick;
            try
            {
                EditorUtility.DisplayProgressBar("NULLBOUND", "Setting up VR, Quest and the game scene...", 0.6f);
                NBProjectSetup.SetupEverything();
                NBProjectSetup.OpenScene();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("NULLBOUND", "Setup didn't finish automatically. Click Nullbound > Setup Everything, and if it still fails send the red error from the Console.", "OK");
                return;
            }
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("NULLBOUND",
                "NULLBOUND is ready! Press the Play button at the top.\n\n" +
                "No headset? WASD to move, Q/E to turn, left/right click to punch, 1-4 moves, Shift dash, F block, G awaken, Esc menu.\n\n" +
                "With a Quest on Link/Air Link, Play runs in VR. Make the headset file with Nullbound > Build Quest APK.",
                "Let's fight");
        }
    }
}
