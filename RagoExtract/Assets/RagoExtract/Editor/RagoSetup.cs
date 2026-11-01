using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rago.EditorTools
{
    /// <summary>
    /// First-open automation + the tools behind the "Rago Extract" tab:
    /// project configuration, package import (VR / multiplayer / voice), XR setup, scene creation and APK builds.
    /// Everything here is reflection-safe so it compiles before any optional package is installed.
    /// </summary>
    [InitializeOnLoad]
    public static class RagoSetup
    {
        public const string ScenePath = "Assets/RagoExtract/Scenes/RagoExtract.unity";
        const string StateFile = "ProjectSettings/RagoExtractSetup.json";
        const string QueueFile = "Library/RagoPackageQueue.txt";
        public static readonly string[] VrPackages = { "com.unity.xr.management", "com.unity.xr.openxr" };
        public static string LastMessage = "";
        static AddRequest _add;
        static ListRequest _list;
        static string _current;
        static readonly HashSet<string> _installed = new HashSet<string>();
        public static bool Busy => _add != null || _batch != null || File.Exists(QueueFile);

        [Serializable] class State { public bool configured; public bool sceneCreated; public bool autoPackagesQueued; public bool xrConfigured; public int version = 1; }

        static State Load() { try { return File.Exists(StateFile) ? JsonUtility.FromJson<State>(File.ReadAllText(StateFile)) ?? new State() : new State(); } catch { return new State(); } }
        static void Save(State s) { try { File.WriteAllText(StateFile, JsonUtility.ToJson(s, true)); } catch { } }

        static double _runAt;
        static readonly Dictionary<string, int> _attempts = new Dictionary<string, int>();
        static double _retryAt;
        static Request _batch;
        static string[] _batchPkgs;

        static RagoSetup()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            // wait until Unity has finished importing/compiling before touching the project
            _runAt = EditorApplication.timeSinceStartup + 1.5;
            EditorApplication.update -= WaitThenRun;
            EditorApplication.update += WaitThenRun;
        }

        static void WaitThenRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < _runAt) return;
            EditorApplication.update -= WaitThenRun;
            OnEditorReady();
        }

        static bool Step(string what, Action a)
        {
            try { a(); return true; }
            catch (Exception e) { LastMessage = what + " failed: " + e.Message; Debug.LogWarning("[Rago Extract] " + LastMessage + "\n" + e); return false; }
        }

        /// <summary>Runs every time the editor loads; each step only runs until it has succeeded once.</summary>
        static void OnEditorReady()
        {
            var s = Load();
            bool first = !s.autoPackagesQueued;
            if (!s.configured && Step("Project setup", ConfigureProject)) { s.configured = true; Save(s); }
            if ((!s.sceneCreated || !File.Exists(ScenePath)) && Step("Scene setup", () => CreateScene(true))) { s.sceneCreated = true; Save(s); }
            if (first)
            {
                s.autoPackagesQueued = true; Save(s);
                RagoWindow.Open();
            }
            Step("Multiplayer check", RagoMultiplayer.AutoEnable);
            Step("Package list", RefreshInstalled);
            Step("Package import", ResumeQueue);
            if (IsInstalled("com.unity.xr.openxr") && !s.xrConfigured && ConfigureXR()) { s.xrConfigured = true; Save(s); }
        }

        // ------------------------------------------------------------------------------------------ packages
        public static void RefreshInstalled()
        {
            _list = Client.List(true, false);
            EditorApplication.update -= PollList;
            EditorApplication.update += PollList;
        }

        static void PollList()
        {
            if (_list == null || !_list.IsCompleted) return;
            EditorApplication.update -= PollList;
            if (_list.Status == StatusCode.Success) { _installed.Clear(); foreach (var p in _list.Result) _installed.Add(p.name); }
            _list = null;
        }

        public static bool IsInstalled(string pkg)
        {
            if (_installed.Contains(pkg)) return true;
            try { return File.ReadAllText("Packages/manifest.json").Contains("\"" + pkg + "\""); } catch { return false; }
        }

        /// <summary>Adds packages (latest version compatible with this editor). Tries one batch first, then one by one with retries.</summary>
        public static void Queue(IEnumerable<string> pkgs)
        {
            var existing = File.Exists(QueueFile) ? File.ReadAllLines(QueueFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() : new List<string>();
            foreach (var p in pkgs) if (!existing.Contains(p) && !IsInstalled(p)) existing.Add(p);
            Directory.CreateDirectory("Library");
            if (existing.Count == 0) { LastMessage = "Already installed."; if (File.Exists(QueueFile)) File.Delete(QueueFile); OnPackagesDone(); return; }
            File.WriteAllLines(QueueFile, existing);
            if (_add == null && _batch == null && TryBatch(existing.ToArray())) return;
            ResumeQueue();
        }

        static bool TryBatch(string[] pkgs)
        {
            // Client.AddAndRemove (Unity 2021.2+) installs everything in one resolve = one recompile
            var mi = typeof(Client).GetMethod("AddAndRemove", new[] { typeof(string[]), typeof(string[]) });
            if (mi == null || pkgs.Length < 2) return false;
            try
            {
                _batch = mi.Invoke(null, new object[] { pkgs, null }) as Request;
                if (_batch == null) return false;
                _batchPkgs = pkgs;
                LastMessage = "Importing " + pkgs.Length + " packages (" + string.Join(", ", pkgs.Select(p => p.Replace("com.unity.", ""))) + ")...";
                Debug.Log("[Rago Extract] " + LastMessage);
                EditorApplication.update -= PollBatch;
                EditorApplication.update += PollBatch;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Rago Extract] batch import unavailable: " + e.Message); _batch = null; return false; }
        }

        static void PollBatch()
        {
            if (_batch == null) { EditorApplication.update -= PollBatch; return; }
            if (!_batch.IsCompleted) return;
            EditorApplication.update -= PollBatch;
            bool ok = _batch.Status == StatusCode.Success;
            if (ok)
            {
                foreach (var p in _batchPkgs) _installed.Add(p);
                LastMessage = "Imported: " + string.Join(", ", _batchPkgs.Select(p => p.Replace("com.unity.", "")));
                Debug.Log("[Rago Extract] " + LastMessage);
            }
            else Debug.LogWarning("[Rago Extract] Batch import failed (" + (_batch.Error != null ? _batch.Error.message : "?") + ") - installing packages one by one instead.");
            _batch = null;
            ResumeQueue();
        }

        static void ResumeQueue()
        {
            if (_add != null || _batch != null || !File.Exists(QueueFile)) return;
            var q = File.ReadAllLines(QueueFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            while (q.Count > 0 && IsInstalled(q[0])) q.RemoveAt(0);
            if (q.Count == 0) { File.Delete(QueueFile); LastMessage = "All packages installed."; OnPackagesDone(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < _retryAt)
            {
                // try again shortly
                EditorApplication.update -= RetryTick;
                EditorApplication.update += RetryTick;
                return;
            }
            _current = q[0];
            File.WriteAllLines(QueueFile, q); // keep it in the queue until it's actually installed
            LastMessage = "Installing " + _current + " ...";
            Debug.Log("[Rago Extract] " + LastMessage);
            _add = Client.Add(_current);
            EditorApplication.update -= PollAdd;
            EditorApplication.update += PollAdd;
        }

        static void RetryTick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < _retryAt) return;
            EditorApplication.update -= RetryTick;
            ResumeQueue();
        }

        static void DropFromQueue(string pkg)
        {
            if (!File.Exists(QueueFile)) return;
            var q = File.ReadAllLines(QueueFile).Where(l => !string.IsNullOrWhiteSpace(l) && l != pkg).ToList();
            if (q.Count > 0) File.WriteAllLines(QueueFile, q); else File.Delete(QueueFile);
        }

        static void PollAdd()
        {
            if (_add == null || !_add.IsCompleted) return;
            EditorApplication.update -= PollAdd;
            if (_add.Status == StatusCode.Success)
            {
                LastMessage = "Installed " + _add.Result.name + " " + _add.Result.version;
                _installed.Add(_add.Result.name);
                DropFromQueue(_current);
            }
            else
            {
                _attempts.TryGetValue(_current, out int n);
                _attempts[_current] = ++n;
                string err = _add.Error != null ? _add.Error.message : "unknown error";
                if (n < 3)
                {
                    LastMessage = "Retrying " + _current + " (" + err + ")";
                    _retryAt = EditorApplication.timeSinceStartup + 4;
                }
                else
                {
                    LastMessage = "Could not install " + _current + ": " + err;
                    DropFromQueue(_current);
                }
                Debug.LogWarning("[Rago Extract] " + LastMessage);
            }
            _add = null;
            ResumeQueue(); // after a domain reload the static constructor resumes the queue anyway
        }

        static void OnPackagesDone()
        {
            var s = Load();
            if (IsInstalled("com.unity.xr.openxr") && !s.xrConfigured && ConfigureXR()) { s.xrConfigured = true; Save(s); }
        }

        // ------------------------------------------------------------------------------------------ project
        public static void ConfigureProject()
        {
            // each setting is applied on its own so one failure (e.g. Android module not installed yet) can't stop the rest
            Try(() => { PlayerSettings.companyName = "Rago"; PlayerSettings.productName = "Rago Extract"; });
            Try(() => PlayerSettings.colorSpace = ColorSpace.Linear);
            Try(() => PlayerSettings.runInBackground = true);
            Try(() => PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft);
            Try(() => PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29);
            Try(() => PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto);
            Try(() => PlayerSettings.Android.forceInternetPermission = true);
            Try(() => PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64);
            Try(() => { PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false); PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 }); });
#if UNITY_2021_2_OR_NEWER
            Try(() => PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.rago.extract"));
            Try(() => PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP));
#else
            Try(() => PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.rago.extract"));
            Try(() => PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP));
#endif
            Try(() => EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC);
            Try(SetInputHandlingBoth);
            Try(SetupLayers);
            Try(AssetDatabase.SaveAssets);
            LastMessage = "Project configured (Quest player settings, layers, input).";
        }

        static void Try(Action a) { try { a(); } catch (Exception e) { Debug.LogWarning("[Rago Extract] setting skipped: " + e.Message); } }

        /// <summary>Old + new input both enabled, so desktop testing (legacy Input) and OpenXR (Input System) work together.</summary>
        static void SetInputHandlingBoth()
        {
            try
            {
                var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
                if (ps == null) return;
                var so = new SerializedObject(ps);
                var p = so.FindProperty("activeInputHandler");
                if (p != null && p.intValue != 2) { p.intValue = 2; so.ApplyModifiedProperties(); }
            }
            catch (Exception e) { Debug.LogWarning("[Rago Extract] could not set input handling: " + e.Message); }
        }

        static void SetupLayers()
        {
            string[] want = { "RagoPlayer", "RagoHands", "RagoItems", "RagoNpc", "RagoRagdoll", "RagoLocalBody" };
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (tm == null) return;
            var so = new SerializedObject(tm);
            var layers = so.FindProperty("layers");
            if (layers == null) return;
            foreach (var name in want)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++) if (layers.GetArrayElementAtIndex(i).stringValue == name) exists = true;
                if (exists) continue;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var e = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(e.stringValue)) { e.stringValue = name; break; }
                }
            }
            so.ApplyModifiedProperties();
        }

        public static void CreateScene(bool open)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var note = new GameObject("Rago Extract (the world is built at runtime - press Play)");
                note.transform.position = Vector3.zero;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (open && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
                if (RagoWorldImport.Find() == null) RagoWorldImport.Import(RagoWorldImport.CurrentMap);
            }
        }

        // ------------------------------------------------------------------------------------------ XR (reflection)
        static Type T(string full)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(full); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>Enables the OpenXR loader for Android (Quest) and PC, plus Meta Quest support and controller profiles.</summary>
        public static bool ConfigureXR()
        {
            try
            {
                var perBT = T("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
                var gen = T("UnityEngine.XR.Management.XRGeneralSettings");
                var store = T("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
                if (perBT == null || gen == null || store == null) { LastMessage = "XR Plug-in Management is not loaded yet."; return false; }
                string key = gen.GetField("k_SettingsKey", Any)?.GetValue(null) as string ?? "com.unity.xr.management.loader_settings";
                EditorBuildSettings.TryGetConfigObject<UnityEngine.Object>(key, out var obj);
                if (obj == null)
                {
                    Directory.CreateDirectory("Assets/XR");
                    obj = ScriptableObject.CreateInstance(perBT);
                    AssetDatabase.CreateAsset(obj, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                    EditorBuildSettings.AddConfigObject(key, obj, true);
                }
                foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
                {
                    var has = perBT.GetMethod("HasManagerSettingsForBuildTarget", Any);
                    var create = perBT.GetMethod("CreateDefaultManagerSettingsForBuildTarget", Any);
                    if (has != null && create != null && !(bool)has.Invoke(obj, new object[] { group })) create.Invoke(obj, new object[] { group });
                    var gs = perBT.GetMethod("SettingsForBuildTarget", Any)?.Invoke(obj, new object[] { group });
                    if (gs != null) gen.GetProperty("InitManagerOnStart", Any)?.SetValue(gs, true);
                    var mgr = perBT.GetMethod("ManagerSettingsForBuildTarget", Any)?.Invoke(obj, new object[] { group });
                    if (mgr == null && gs != null) mgr = gen.GetProperty("Manager", Any)?.GetValue(gs);
                    if (mgr == null) { Debug.LogWarning("[Rago Extract] No XR manager settings for " + group); continue; }
                    var assign = store.GetMethods(Any).FirstOrDefault(m => m.Name == "AssignLoader" && m.GetParameters().Length == 3);
                    assign?.Invoke(null, new object[] { mgr, "UnityEngine.XR.OpenXR.OpenXRLoader", group });
                    EnableOpenXRFeatures(group);
                }
                AssetDatabase.SaveAssets();
                LastMessage = "VR configured: OpenXR enabled for Quest (Android) and PC VR.";
                Debug.Log("[Rago Extract] " + LastMessage);
                return true;
            }
            catch (Exception e)
            {
                LastMessage = "Automatic XR setup failed - open Project Settings > XR Plug-in Management and tick OpenXR (+ Meta Quest feature). " + e.Message;
                Debug.LogWarning("[Rago Extract] " + LastMessage);
                return false;
            }
        }

        static void EnableOpenXRFeatures(BuildTargetGroup group)
        {
            var helpers = T("UnityEditor.XR.OpenXR.Features.FeatureHelpers");
            helpers?.GetMethod("RefreshFeatures", Any, null, new[] { typeof(BuildTargetGroup) }, null)?.Invoke(null, new object[] { group });
            var oxs = T("UnityEngine.XR.OpenXR.OpenXRSettings");
            if (oxs == null) return;
            var settings = oxs.GetMethod("GetSettingsForBuildTargetGroup", Any)?.Invoke(null, new object[] { group });
            if (settings == null) return;
            var getFeatures = oxs.GetMethods(Any).FirstOrDefault(m => m.Name == "GetFeatures" && !m.IsGenericMethod && m.GetParameters().Length == 0);
            var feats = getFeatures?.Invoke(settings, null) as Array;
            string[] android = { "MetaQuestFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "MetaQuestTouchProControllerProfile" };
            string[] pc = { "OculusTouchControllerProfile", "ValveIndexControllerProfile", "HTCViveControllerProfile", "MetaQuestTouchPlusControllerProfile" };
            var want = group == BuildTargetGroup.Android ? android : pc;
            if (feats != null)
                foreach (var f in feats)
                {
                    if (f == null) continue;
                    if (want.Contains(f.GetType().Name)) f.GetType().GetProperty("enabled", Any)?.SetValue(f, true);
                    EditorUtility.SetDirty(f as UnityEngine.Object);
                }
            var rm = oxs.GetProperty("renderMode", Any);
            if (rm != null && rm.PropertyType.IsEnum) { try { rm.SetValue(settings, Enum.Parse(rm.PropertyType, "SinglePassInstanced")); } catch { } }
            EditorUtility.SetDirty(settings as UnityEngine.Object);
        }

        public static bool XRConfigured()
        {
            try
            {
                var gen = T("UnityEngine.XR.Management.XRGeneralSettings");
                if (gen == null) return false;
                string key = gen.GetField("k_SettingsKey", Any)?.GetValue(null) as string ?? "com.unity.xr.management.loader_settings";
                EditorBuildSettings.TryGetConfigObject<UnityEngine.Object>(key, out var obj);
                if (obj == null) return false;
                var gs = obj.GetType().GetMethod("SettingsForBuildTarget", Any)?.Invoke(obj, new object[] { BuildTargetGroup.Android });
                var mgr = gs != null ? gen.GetProperty("Manager", Any)?.GetValue(gs) : null;
                var loaders = mgr?.GetType().GetProperty("activeLoaders", Any)?.GetValue(mgr) as System.Collections.IList;
                return loaders != null && loaders.Count > 0;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------------------------------ compile guard
        /// <summary>
        /// If an optional assembly fails to compile (e.g. a future package changed its API), switch that module off
        /// with a define so the rest of the game keeps compiling and playing (offline). The tab can switch it back on.
        /// </summary>
        static void OnAssemblyCompiled(string path, CompilerMessage[] messages)
        {
            bool err = messages.Any(m => m.type == CompilerMessageType.Error);
            if (!err) return;
            string asm = Path.GetFileNameWithoutExtension(path);
            if (asm == "RagoExtract.Fusion") { Debug.LogWarning("[Rago Extract] The Photon Fusion module didn't compile with your Fusion version - switching it off so the game still runs offline. Errors:\n" + string.Join("\n", messages.Where(m => m.type == CompilerMessageType.Error).Select(m => m.message))); AddDefine("RAGO_DISABLE_NET"); }
        }

        static readonly BuildTargetGroup[] Groups = { BuildTargetGroup.Android, BuildTargetGroup.Standalone };

        public static bool HasDefine(string d) => GetDefines(BuildTargetGroup.Android).Contains(d) || GetDefines(BuildTargetGroup.Standalone).Contains(d);

        public static void AddDefine(string d)
        {
            foreach (var g in Groups) { var l = GetDefines(g); if (!l.Contains(d)) { l.Add(d); SetDefines(g, l); } }
        }

        public static void RemoveDefine(string d)
        {
            foreach (var g in Groups) { var l = GetDefines(g); if (l.Remove(d)) SetDefines(g, l); }
        }

        static List<string> GetDefines(BuildTargetGroup g)
        {
#if UNITY_2021_2_OR_NEWER
            string s = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(g));
#else
            string s = PlayerSettings.GetScriptingDefineSymbolsForGroup(g);
#endif
            return s.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        static void SetDefines(BuildTargetGroup g, List<string> l)
        {
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(g), string.Join(";", l));
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(g, string.Join(";", l));
#endif
        }

        // ------------------------------------------------------------------------------------------ builds
        public static void BuildApk(bool run)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Rago Extract", "Android Build Support is not installed.\n\nOpen Unity Hub > Installs > (your Unity version) > Add modules and tick:\n  - Android Build Support\n  - OpenJDK\n  - Android SDK & NDK Tools", "OK");
                return;
            }
            if (EditorUtility.scriptCompilationFailed)
            {
                EditorUtility.DisplayDialog("Rago Extract", "There are script errors (red messages in the Console). Fix or send them to me first - an APK can't be built until they're gone.", "OK");
                return;
            }
            CreateScene(false);
            ConfigureProject();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            if (!XRConfigured())
            {
                if (!IsInstalled("com.unity.xr.openxr"))
                {
                    if (EditorUtility.DisplayDialog("Rago Extract", "VR isn't set up yet, so the APK would run flat on Quest.\n\nSet up VR first? (then press Build APK again when it's done)", "Set Up VR", "Build anyway")) { Queue(VrPackages); return; }
                }
                else ConfigureXR();
            }
            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/RagoExtract.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var res = report.summary.result;
            LastMessage = res == UnityEditor.Build.Reporting.BuildResult.Succeeded
                ? "APK built: " + Path.GetFullPath(opts.locationPathName) + "  (" + (report.summary.totalSize / (1024 * 1024)) + " MB)"
                : "APK build " + res + " - see the Console for details.";
            Debug.Log("[Rago Extract] " + LastMessage);
            if (res == UnityEditor.Build.Reporting.BuildResult.Succeeded && !run) EditorUtility.RevealInFinder(opts.locationPathName);
        }

        public static void BuildWindows()
        {
            CreateScene(false);
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/RagoExtract.exe", target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone });
            LastMessage = "Windows (PC VR) build: " + report.summary.result;
        }
    }
}
