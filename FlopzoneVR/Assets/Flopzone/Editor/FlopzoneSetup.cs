using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Flopzone.EditorTools
{
    /// <summary>
    /// Sets the project up by itself whenever it is opened, and again after
    /// every update that bumps <see cref="Version"/>: makes the game scene and
    /// assets, sets the Quest/Android player settings, imports the VR packages
    /// and turns on OpenXR for Quest. The Flopzone menu has Set Up Multiplayer, Set Up VR (Quest) and Build for Quest (APK).
    /// </summary>
    [InitializeOnLoad]
    public static class FlopzoneSetup
    {
        /// <summary>Bump this when a change needs the setup to run again on everyone's machine.</summary>
        public const int Version = 3;

        public const string Root = "Assets/Flopzone";
        public const string ScenePath = Root + "/Scenes/Flopzone.unity";
        public const string AssetsPath = Root + "/Resources/FlopzoneAssets.asset";
        public const string ConfigPath = Root + "/Resources/FlopzoneConfig.asset";
        const string GeneratedDir = Root + "/Generated";
        const string StatePath = "ProjectSettings/FlopzoneSetup.json";

        public static readonly string[] VrPackages = { "com.unity.xr.management", "com.unity.xr.openxr" };

        [Serializable]
        class State
        {
            public int version;
            public bool packagesRequested;
            public bool xrConfigured;
            public bool askedRestart;
        }

        static State state;
        static readonly Queue<string> packageQueue = new Queue<string>();
        static AddRequest addRequest;
        static ListRequest listRequest;
        static readonly HashSet<string> installed = new HashSet<string>();
        public static string LastMessage = "";
        public static bool Busy => addRequest != null || packageQueue.Count > 0;

        static FlopzoneSetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        static State S
        {
            get
            {
                if (state == null)
                {
                    try { state = File.Exists(StatePath) ? JsonUtility.FromJson<State>(File.ReadAllText(StatePath)) : null; } catch { state = null; }
                    if (state == null) state = new State();
                }
                return state;
            }
        }

        static void SaveState() => File.WriteAllText(StatePath, JsonUtility.ToJson(S, true));

        static void AutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoRun; return; }
            bool firstTime = S.version == 0;
            if (S.version < Version || !File.Exists(AssetsPath) || !File.Exists(ScenePath))
            {
                RunAll(false);
                if (firstTime) EditorApplication.delayCall += () => EditorUtility.DisplayDialog("Flopzone",
                    "Flopzone is set up! Press Play to try it (keyboard and mouse work without a headset).\n\n" +
                    "VR support is importing in the background. Use the Flopzone menu at the top for:\n" +
                    "• Set Up Multiplayer\n• Set Up VR (Quest)\n• Build for Quest (APK)", "OK");
            }
            else
            {
                // Photon Fusion just arrived: put the saved App ID into it
                if (FindType("Fusion.NetworkRunner") != null && MultiplayerSetup.SavedAppId.Length > 0 && MultiplayerSetup.FusionAppId != MultiplayerSetup.SavedAppId)
                    MultiplayerSetup.FinishSetup(false);
                RefreshInstalled(() =>
                {
                    if (!VRPackagesInstalled)
                    {
                        // try once per editor session (e.g. it was offline last time)
                        if (!Busy && !SessionState.GetBool("Flopzone.TriedVR", false))
                        {
                            SessionState.SetBool("Flopzone.TriedVR", true);
                            ImportVR(false);
                        }
                    }
                    else if (!S.xrConfigured || !XRLoaderAssigned(BuildTargetGroup.Android)) ConfigureXR(false);
                });
            }
        }

        /// <summary>Do everything: assets, scene, settings, VR packages, XR config.</summary>
        public static void RunAll(bool interactive)
        {
            try
            {
                EnsureFolders();
                var assets = EnsureGameAssets();
                EnsureConfig();
                EnsureScene(assets);
                ApplyPlayerSettings();
                IncludeShaders();
                bool inputChanged = SetInputHandlingBoth();
                S.version = Version;
                SaveState();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                LastMessage = "Setup done.";
                Debug.Log("[Flopzone] Project setup done (version " + Version + ").");
                ImportVR(interactive);
                if (inputChanged && !S.askedRestart)
                {
                    S.askedRestart = true;
                    SaveState();
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorUtility.DisplayDialog("Flopzone",
                            "Flopzone switched 'Active Input Handling' to Both (needed for VR plus keyboard testing). Unity needs to restart for that to take effect.",
                            "Restart now", "Later"))
                            EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    };
                }
            }
            catch (Exception e)
            {
                LastMessage = "Setup failed: " + e.Message;
                Debug.LogException(e);
                if (interactive) EditorUtility.DisplayDialog("Flopzone", LastMessage, "OK");
            }
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { Root + "/Resources", Root + "/Scenes", GeneratedDir })
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }
        }

        static Material MakeMat(string name, string shaderName, Action<Material> tweak = null)
        {
            string path = GeneratedDir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("[Flopzone] shader missing: " + shaderName); return null; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            tweak?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameAssets EnsureGameAssets()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(AssetsPath);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(a, AssetsPath);
            }
            a.lit = MakeMat("Lit", "Standard", m => { m.enableInstancing = true; m.SetFloat("_Glossiness", 0.3f); });
            a.litEmissive = MakeMat("LitEmissive", "Standard", m =>
            {
                m.enableInstancing = true;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
            a.fade = MakeMat("Fade", "Standard", m => GameAssets.MakeFade(m));
            a.unlit = MakeMat("Unlit", "Unlit/Color");
            a.sprite = MakeMat("Sprite", "Sprites/Default");
            a.particleAdd = MakeMat("ParticleAdd", "Legacy Shaders/Particles/Additive");
            a.particleAlpha = MakeMat("ParticleAlpha", "Legacy Shaders/Particles/Alpha Blended");
            a.skybox = MakeMat("Sky", "Skybox/Procedural");
            a.font = GameAssets.BuiltinFont();
            EditorUtility.SetDirty(a);
            return a;
        }

        static void EnsureConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<FlopzoneConfig>(ConfigPath) != null) return;
            var c = ScriptableObject.CreateInstance<FlopzoneConfig>();
            AssetDatabase.CreateAsset(c, ConfigPath);
        }

        public static FlopzoneConfig Config
        {
            get
            {
                var c = AssetDatabase.LoadAssetAtPath<FlopzoneConfig>(ConfigPath);
                if (c == null) { EnsureFolders(); EnsureConfig(); c = AssetDatabase.LoadAssetAtPath<FlopzoneConfig>(ConfigPath); }
                return c;
            }
        }

        static void EnsureScene(GameAssets assets)
        {
            var active = SceneManager.GetActiveScene();
            bool activeIsOurs = active.path == ScenePath;
            bool untitled = string.IsNullOrEmpty(active.path);
            if (active.isDirty && !activeIsOurs && untitled && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene;
            if (activeIsOurs) scene = active;
            else if (untitled) scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // the scene only holds the Game object; everything else is built when the game runs
            foreach (var go in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(go);
            var prev = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("Flopzone");
            root.AddComponent<Game>();
            root.AddComponent<HubPreview>();   // shows the hub while editing
            // fog + skybox saved in the scene keep their shader variants in the build
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.skybox = assets.skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (prev.IsValid() && prev != scene) SceneManager.SetActiveScene(prev);
            if (!activeIsOurs && !untitled) EditorSceneManager.CloseScene(scene, true);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Flopzone";
            PlayerSettings.productName = "Flopzone";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
#if UNITY_2021_2_OR_NEWER
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.flopzone.game");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.flopzone.game");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
#endif
            EditorUserBuildSettings.buildAppBundle = false;
        }

        /// <summary>Small shaders the game creates materials from at runtime.</summary>
        static void IncludeShaders()
        {
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            foreach (var name in new[] { "Sprites/Default", "Unlit/Color", "Legacy Shaders/Particles/Additive", "Legacy Shaders/Particles/Alpha Blended", "Skybox/Procedural" })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                bool found = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) found = true;
                if (found) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>VR input + keyboard testing both need "Both". Returns true if it changed.</summary>
        static bool SetInputHandlingBoth()
        {
            try
            {
                var ps = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
                var so = new SerializedObject(ps);
                var p = so.FindProperty("activeInputHandler");
                if (p == null || p.intValue == 2) return false;
                p.intValue = 2;
                so.ApplyModifiedProperties();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Flopzone] Couldn't set Active Input Handling (set it to Both in Player Settings): " + e.Message);
                return false;
            }
        }

        /* ---------------- packages ---------------- */

        static void RefreshInstalled(Action then)
        {
            listRequest = Client.List(true, false);
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (listRequest == null || !listRequest.IsCompleted) return;
                EditorApplication.update -= poll;
                installed.Clear();
                if (listRequest.Status == StatusCode.Success)
                    foreach (var p in listRequest.Result) installed.Add(p.name);
                listRequest = null;
                then?.Invoke();
            };
            EditorApplication.update += poll;
        }

        public static bool HasPackage(string name) => installed.Contains(name);
        public static bool VRPackagesInstalled => FindType("UnityEngine.XR.OpenXR.OpenXRLoader") != null && FindType("UnityEngine.XR.Management.XRGeneralSettings") != null;

        /// <summary>Install XR Plug-in Management + OpenXR, then switch them on for Quest.</summary>
        public static void ImportVR(bool interactive)
        {
            RefreshInstalled(() =>
            {
                packageQueue.Clear();
                foreach (var p in VrPackages) if (!installed.Contains(p)) packageQueue.Enqueue(p);
                S.packagesRequested = true;
                SaveState();
                if (packageQueue.Count == 0)
                {
                    ConfigureXR(interactive);
                    return;
                }
                LastMessage = "Importing VR packages...";
                NextPackage();
            });
        }

        static void NextPackage()
        {
            if (packageQueue.Count == 0)
            {
                LastMessage = "VR packages imported. Configuring OpenXR for Quest...";
                // the packages compile first; the domain reload runs AutoRun again, which configures XR
                return;
            }
            string name = packageQueue.Dequeue();
            Debug.Log("[Flopzone] Adding package " + name);
            addRequest = Client.Add(name);
            EditorApplication.update += PollAdd;
        }

        static void PollAdd()
        {
            if (addRequest == null || !addRequest.IsCompleted) return;
            EditorApplication.update -= PollAdd;
            if (addRequest.Status == StatusCode.Success) installed.Add(addRequest.Result.name);
            else
            {
                LastMessage = "Couldn't add a package: " + (addRequest.Error != null ? addRequest.Error.message : "unknown error");
                Debug.LogWarning("[Flopzone] " + LastMessage);
            }
            addRequest = null;
            NextPackage();
        }

        /* ---------------- XR configuration (by reflection, so nothing breaks before the packages exist) ---------------- */

        public static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        const string XRSettingsKey = "com.unity.xr.management.loader_settings";

        /// <summary>Call a method that takes a BuildTargetGroup as its last argument (newer packages add overloads).</summary>
        static object CallGroup(object target, Type type, string name, BuildTargetGroup group, params object[] before)
        {
            foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
            {
                if (m.Name != name || m.IsGenericMethodDefinition) continue;
                var ps = m.GetParameters();
                if (ps.Length != before.Length + 1 || ps[ps.Length - 1].ParameterType != typeof(BuildTargetGroup)) continue;
                var args = new object[ps.Length];
                Array.Copy(before, args, before.Length);
                args[ps.Length - 1] = group;
                return m.Invoke(m.IsStatic ? null : target, args);
            }
            throw new MissingMethodException(type.Name + "." + name);
        }

        public static bool XRLoaderAssigned(BuildTargetGroup group)
        {
            try
            {
                var perTarget = GetPerBuildTarget(false);
                if (perTarget == null) return false;
                var settings = CallGroup(perTarget, perTarget.GetType(), "SettingsForBuildTarget", group);
                if (settings == null) return false;
                var manager = settings.GetType().GetProperty("AssignedSettings")?.GetValue(settings);
                if (manager == null) return false;
                var loaders = manager.GetType().GetProperty("activeLoaders")?.GetValue(manager) as System.Collections.IEnumerable;
                if (loaders == null) return false;
                foreach (var l in loaders) if (l != null && l.GetType().Name == "OpenXRLoader") return true;
            }
            catch (Exception) { }
            return false;
        }

        static ScriptableObject GetPerBuildTarget(bool create)
        {
            var perType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
            if (perType == null) return null;
            EditorBuildSettings.TryGetConfigObject(XRSettingsKey, out ScriptableObject obj);
            if (obj != null || !create) return obj;
            if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
            obj = ScriptableObject.CreateInstance(perType);
            AssetDatabase.CreateAsset(obj, AssetDatabase.GenerateUniqueAssetPath("Assets/XR/XRGeneralSettingsPerBuildTarget.asset"));
            EditorBuildSettings.AddConfigObject(XRSettingsKey, obj, true);
            return obj;
        }

        public static void ConfigureXR(bool interactive)
        {
            if (!VRPackagesInstalled)
            {
                if (interactive) EditorUtility.DisplayDialog("Flopzone", "The VR packages aren't installed yet. Use Flopzone > Set Up VR (Quest) and wait for Unity to finish importing.", "OK");
                return;
            }
            var problems = new List<string>();
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                if (XRLoaderAssigned(group)) continue;
                try
                {
                    var perTarget = GetPerBuildTarget(true);
                    var t = perTarget.GetType();
                    if (!(bool)CallGroup(perTarget, t, "HasSettingsForBuildTarget", group))
                        CallGroup(perTarget, t, "CreateDefaultSettingsForBuildTarget", group);
                    if (!(bool)CallGroup(perTarget, t, "HasManagerSettingsForBuildTarget", group))
                        CallGroup(perTarget, t, "CreateDefaultManagerSettingsForBuildTarget", group);
                    var manager = CallGroup(perTarget, t, "ManagerSettingsForBuildTarget", group);
                    var store = FindType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
                    bool ok = (bool)CallGroup(null, store, "AssignLoader", group, manager, "UnityEngine.XR.OpenXR.OpenXRLoader");
                    if (!ok) problems.Add("couldn't assign the OpenXR loader for " + group);
                    var general = CallGroup(perTarget, t, "SettingsForBuildTarget", group);
                    general?.GetType().GetProperty("InitManagerOnStart")?.SetValue(general, true);
                    EditorUtility.SetDirty(perTarget);
                }
                catch (Exception e)
                {
                    problems.Add(group + ": " + (e.InnerException ?? e).Message);
                }
            }
            EnableOpenXRFeatures(problems);
            AssetDatabase.SaveAssets();
            S.xrConfigured = problems.Count == 0;
            SaveState();
            if (problems.Count == 0)
            {
                LastMessage = "VR is ready: OpenXR is on for Quest (Android) and PC VR.";
                Debug.Log("[Flopzone] " + LastMessage);
                if (interactive) EditorUtility.DisplayDialog("Flopzone", LastMessage, "Nice");
            }
            else
            {
                LastMessage = "VR packages are in, but some settings need a click: " + string.Join("; ", problems);
                Debug.LogWarning("[Flopzone] " + LastMessage);
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Flopzone",
                        "Almost there. In the window that opens:\n\n1. Android tab: tick OpenXR, then under OpenXR tick 'Meta Quest Support'.\n2. Add the 'Oculus Touch Controller Profile' under Interaction Profiles.\n3. PC tab: tick OpenXR.\n\n(" + string.Join("; ", problems) + ")", "Open settings");
                    SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                }
            }
        }

        static void EnableOpenXRFeatures(List<string> problems)
        {
            try
            {
                var settingsType = FindType("UnityEngine.XR.OpenXR.OpenXRSettings");
                var helpers = FindType("UnityEditor.XR.OpenXR.Features.FeatureHelpers");
                foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
                {
                    if (helpers != null) { try { CallGroup(null, helpers, "RefreshFeatures", group); } catch (MissingMethodException) { } }
                    var settings = settingsType == null ? null : CallGroup(null, settingsType, "GetSettingsForBuildTargetGroup", group) as UnityEngine.Object;
                    if (settings == null) { problems.Add("OpenXR settings for " + group + " not found"); continue; }
                    Array features = null;
                    foreach (var m in settingsType.GetMethods())
                        if (m.Name == "GetFeatures" && !m.IsGenericMethodDefinition && m.GetParameters().Length == 0) features = m.Invoke(settings, null) as Array;
                    if (features == null) continue;
                    bool controller = false;
                    foreach (var f in features)
                    {
                        string n = f.GetType().Name;
                        bool want = n == "OculusTouchControllerProfile" || n == "MetaQuestTouchPlusControllerProfile" || n == "MetaQuestTouchProControllerProfile"
                                    || (group == BuildTargetGroup.Android && (n == "MetaQuestFeature" || n == "OculusQuestFeature"));
                        if (!want) continue;
                        f.GetType().GetProperty("enabled")?.SetValue(f, true);
                        EditorUtility.SetDirty((UnityEngine.Object)f);
                        if (n.Contains("Controller")) controller = true;
                    }
                    if (!controller) problems.Add("no Quest controller profile found for " + group);
                    EditorUtility.SetDirty(settings);
                }
            }
            catch (Exception e)
            {
                problems.Add("OpenXR features: " + (e.InnerException ?? e).Message);
            }
        }
    }
}
