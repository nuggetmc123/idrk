using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Menu: KartVR > Set Up VR (Quest). Does the whole "turn on VR" setup for you:
    /// 1. installs Unity's VR packages (XR Plugin Management + OpenXR),
    /// 2. ticks OpenXR for PC (Quest Link / SteamVR) and Android (Quest headset),
    /// 3. turns on the Quest controller profiles and Meta Quest support.
    /// Unity reloads after step 1; setup carries on by itself afterwards.
    ///
    /// Talks to the XR packages through reflection, so this script compiles even
    /// before they're installed. Anything it can't do is listed with the manual click.
    /// </summary>
    [InitializeOnLoad]
    public static class VRSetup
    {
        static string PendingKey => "KartVR.PendingVRSetup." + Application.dataPath;
        static AddAndRemoveRequest request;

        static VRSetup()
        {
            if (Application.isBatchMode) return;
            if (EditorPrefs.GetBool(PendingKey, false)) EditorApplication.delayCall += Resume;
            EditorApplication.playModeStateChanged -= BeforePlay;
            EditorApplication.playModeStateChanged += BeforePlay;
        }

        /// <summary>
        /// Every time you press Play: make sure the Quest controller profiles are switched on, otherwise OpenXR
        /// never reports the controllers and your hands stay frozen.
        /// </summary>
        static void BeforePlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || !PackagesInstalled()) return;
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                int on = EnableFeatures(group, ControllerProfiles);
                if (on == 0)
                    Debug.LogWarning("[KartVR] Couldn't switch on the Quest controller profile for " + group +
                                     ". Your hands won't move in VR until it's on: KartVR > Set Up VR (Quest).");
            }
            AssetDatabase.SaveAssets();
        }

        static readonly string[] ControllerProfiles =
        {
            "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "MetaQuestTouchProControllerProfile",
        };

        [MenuItem("KartVR/Set Up VR (Quest)", priority = 1)]
        public static void Run()
        {
            if (PackagesInstalled())
            {
                Configure(true);
                return;
            }
            if (!EditorUtility.DisplayDialog("Kart VR - Set Up VR",
                    "This installs Unity's VR support (OpenXR) and switches it on for Meta Quest.\n\n" +
                    "It takes a minute or two and Unity will reload. If Unity asks to restart or to enable the new input system, say Yes.",
                    "Install VR", "Cancel"))
                return;
            EditorPrefs.SetBool(PendingKey, true);
            request = Client.AddAndRemove(new[] { "com.unity.xr.management", "com.unity.xr.openxr" }, null);
            EditorApplication.update -= WaitForInstall;
            EditorApplication.update += WaitForInstall;
            EditorUtility.DisplayProgressBar("Kart VR", "Installing VR support (OpenXR)...", 0.3f);
        }

        static void WaitForInstall()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= WaitForInstall;
            EditorUtility.ClearProgressBar();
            if (request.Status == StatusCode.Failure)
            {
                EditorPrefs.DeleteKey(PendingKey);
                EditorUtility.DisplayDialog("Kart VR", "Couldn't install the VR packages:\n" + request.Error?.message +
                    "\n\nYou can install them by hand: Window > Package Manager > Unity Registry > OpenXR Plugin > Install.", "OK");
                return;
            }
            // Unity recompiles and reloads now; Resume() continues after that.
            if (PackagesInstalled()) EditorApplication.delayCall += Resume;
        }

        static void Resume()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Resume;
                return;
            }
            if (!PackagesInstalled()) return; // still installing; we'll be called again after the reload
            EditorPrefs.DeleteKey(PendingKey);
            Configure(true);
        }

        /// <summary>Used by the Quest build: make sure OpenXR + the Quest settings are on. False if VR isn't installed yet.</summary>
        public static bool EnsureQuestReady()
        {
            if (!PackagesInstalled()) return false;
            Configure(false);
            return true;
        }

        /// <summary>
        /// What's still missing for the Quest (Android) build to start in VR with working controllers.
        /// Empty = all good. Without these the APK opens as a flat window and the controllers do nothing.
        /// </summary>
        public static List<string> QuestProblems()
        {
            var problems = new List<string>();
            var manager = GetManagerSettings(BuildTargetGroup.Android);
            if (manager == null || !HasOpenXRLoader(manager))
                problems.Add("OpenXR isn't ticked on the Android tab of XR Plug-in Management");
            if (CountFeatures(BuildTargetGroup.Android, true, ControllerProfiles) == 0)
                problems.Add("No Quest controller profile on Android (OpenXR > Android tab > Interaction Profiles > + > Oculus Touch Controller Profile)");
            if (CountFeatures(BuildTargetGroup.Android, true, "MetaQuestFeature") == 0)
                problems.Add("Meta Quest Support isn't ticked (OpenXR > Android tab > OpenXR Feature Groups)");
            return problems;
        }

        static bool PackagesInstalled() =>
            FindType("UnityEngine.XR.OpenXR.OpenXRLoader") != null &&
            FindType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore") != null;

        // ------------------------------------------------------------------ configuration

        static void Configure(bool interactive)
        {
            var done = new List<string>();
            var todo = new List<string>();

            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                string where = group == BuildTargetGroup.Standalone ? "PC (monitor icon tab)" : "Quest/Android (Android robot icon tab)";
                if (EnableOpenXR(group)) done.Add("OpenXR switched on for " + where);
                else todo.Add("Tick OpenXR on the " + where + " in Edit > Project Settings > XR Plug-in Management");

                int profiles = EnableFeatures(group, ControllerProfiles);
                if (profiles > 0) done.Add("Quest controllers added for " + where);
                else todo.Add("Under XR Plug-in Management > OpenXR (" + where + "), add Oculus Touch Controller Profile with the + button");

                if (group == BuildTargetGroup.Android)
                {
                    if (EnableFeatures(group, "MetaQuestFeature") > 0) done.Add("Meta Quest support switched on");
                    else todo.Add("Under XR Plug-in Management > OpenXR (Android tab), tick Meta Quest Support");
                }
            }
            AssetDatabase.SaveAssets();

            string msg = done.Count > 0 ? "Done:\n- " + string.Join("\n- ", done) : "";
            if (todo.Count > 0)
                msg += (msg.Length > 0 ? "\n\n" : "") + "Still to do by hand (Unity didn't let me):\n- " + string.Join("\n- ", todo) +
                       "\n\nThe XR Plug-in Management page is open now.";
            msg += "\n\nTo play on PC with your Quest: connect Quest Link, then press Play.";
            if (todo.Count > 0 && interactive) SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
            Debug.Log("[KartVR] VR setup:\n" + msg);
            if (interactive) EditorUtility.DisplayDialog("Kart VR - VR setup", msg, "OK");
        }

        /// <summary>Tick OpenXR as the VR plug-in for a platform.</summary>
        static bool EnableOpenXR(BuildTargetGroup group)
        {
            try
            {
                var manager = GetManagerSettings(group);
                if (manager == null) return false;
                var store = FindType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
                var assign = store?.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
                if (assign == null) return false;
                object result = assign.Invoke(null, new object[] { manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group });
                if (manager is UnityEngine.Object o) EditorUtility.SetDirty(o);
                // AssignLoader returns false if it was already assigned; check the loader list either way.
                return result is bool ok && ok || HasOpenXRLoader(manager);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KartVR] Couldn't switch on OpenXR for " + group + ": " + e.Message);
                return false;
            }
        }

        static bool HasOpenXRLoader(object manager)
        {
            var loaders = GetMember(manager, "activeLoaders") as System.Collections.IEnumerable ?? GetMember(manager, "loaders") as System.Collections.IEnumerable;
            if (loaders == null) return false;
            foreach (var l in loaders)
                if (l != null && l.GetType().Name == "OpenXRLoader") return true;
            return false;
        }

        /// <summary>Find (or create) XR Management's settings for a platform and return its XRManagerSettings.</summary>
        static object GetManagerSettings(BuildTargetGroup group)
        {
            var perTargetType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
            var generalType = FindType("UnityEngine.XR.Management.XRGeneralSettings");
            if (perTargetType == null || generalType == null) return null;

            // The settings asset is registered under this key.
            string key = (generalType.GetField("settingsKey", BindingFlags.Public | BindingFlags.Static) ?? generalType.GetField("k_SettingsKey", BindingFlags.Public | BindingFlags.Static))?.GetValue(null) as string
                         ?? "com.unity.xr.management.loader_settings";
            EditorBuildSettings.TryGetConfigObject(key, out UnityEngine.Object perTarget);
            if (perTarget == null)
            {
                // Make the settings asset the same way the XR Plug-in Management page does.
                if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
                perTarget = ScriptableObject.CreateInstance(perTargetType);
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(key, perTarget, true);
            }

            const BindingFlags inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Call(perTarget, "CreateDefaultSettingsForBuildTarget", group, onlyIf: "HasSettingsForBuildTarget");
            Call(perTarget, "CreateDefaultManagerSettingsForBuildTarget", group, onlyIf: "HasManagerSettingsForBuildTarget");

            var getSettings = perTargetType.GetMethod("SettingsForBuildTarget", inst, null, new[] { typeof(BuildTargetGroup) }, null);
            object general = getSettings?.Invoke(perTarget, new object[] { group });
            if (general == null)
            {
                // Older/newer XR Management without the helper methods: build the settings by hand.
                var managerType = FindType("UnityEngine.XR.Management.XRManagerSettings");
                var setSettings = perTargetType.GetMethod("SetSettingsForBuildTarget", inst, null, new[] { typeof(BuildTargetGroup), generalType }, null);
                if (managerType == null || setSettings == null) return null;
                var gs = ScriptableObject.CreateInstance(generalType);
                var mgr = ScriptableObject.CreateInstance(managerType);
                gs.name = group + " Settings";
                mgr.name = group + " Providers";
                generalType.GetProperty("Manager", inst)?.SetValue(gs, mgr);
                AssetDatabase.AddObjectToAsset(gs, perTarget);
                AssetDatabase.AddObjectToAsset(mgr, perTarget);
                setSettings.Invoke(perTarget, new object[] { group, gs });
                EditorUtility.SetDirty(perTarget);
                general = gs;
            }
            // "Initialize XR on Startup": without it the APK starts with VR (and the controllers) switched off.
            var initOnStart = general.GetType().GetProperty("InitManagerOnStart", inst);
            if (initOnStart != null && initOnStart.CanWrite && !(initOnStart.GetValue(general) is bool on && on))
            {
                initOnStart.SetValue(general, true);
                if (general is UnityEngine.Object g) EditorUtility.SetDirty(g);
            }
            return GetMember(general, "Manager") ?? GetMember(general, "AssignedSettings");
        }

        /// <summary>Calls perTarget.method(group) unless perTarget.onlyIf(group) is already true.</summary>
        static void Call(object target, string method, BuildTargetGroup group, string onlyIf)
        {
            const BindingFlags inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var check = target.GetType().GetMethod(onlyIf, inst, null, new[] { typeof(BuildTargetGroup) }, null);
            if (check != null && check.Invoke(target, new object[] { group }) is bool has && has) return;
            var m = target.GetType().GetMethod(method, inst, null, new[] { typeof(BuildTargetGroup) }, null);
            m?.Invoke(target, new object[] { group });
        }

        /// <summary>Turn on OpenXR features (controller profiles, Meta Quest support) by class name. Returns how many are on.</summary>
        static int EnableFeatures(BuildTargetGroup group, params string[] typeNames) => CountFeatures(group, false, typeNames);

        /// <summary>Counts the named OpenXR features; switches them on first unless onlyCountEnabled.</summary>
        static int CountFeatures(BuildTargetGroup group, bool onlyCountEnabled, params string[] typeNames)
        {
            try
            {
                var settingsType = FindType("UnityEngine.XR.OpenXR.OpenXRSettings");
                var get = settingsType?.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static);
                var settings = get?.Invoke(null, new object[] { group });
                if (settings == null) return 0;

                // Make OpenXR discover all its features first; a freshly made settings asset has an empty list.
                var helpers = FindType("UnityEditor.XR.OpenXR.Features.FeatureHelpers");
                helpers?.GetMethod("RefreshFeatures", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BuildTargetGroup) }, null)
                    ?.Invoke(null, new object[] { group });

                // OpenXRSettings has both GetFeatures() and GetFeatures<T>(); pick the plain one
                // (asking for "GetFeatures with no arguments" directly throws "ambiguous match").
                var getFeatures = settingsType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetFeatures" && !m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                var features = getFeatures?.Invoke(settings, null) as Array;
                if (features == null) return 0;
                int on = 0;
                foreach (var f in features)
                {
                    if (f == null || !typeNames.Contains(f.GetType().Name)) continue;
                    var enabled = f.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
                    if (enabled == null) continue;
                    if (onlyCountEnabled)
                    {
                        if (enabled.GetValue(f) is bool isOn && isOn) on++;
                        continue;
                    }
                    enabled.SetValue(f, true);
                    if (f is UnityEngine.Object o) EditorUtility.SetDirty(o);
                    on++;
                }
                if (settings is UnityEngine.Object so) EditorUtility.SetDirty(so);
                return on;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KartVR] Couldn't switch on OpenXR features for " + group + ": " + e.Message);
                return 0;
            }
        }

        // ------------------------------------------------------------------ reflection helpers

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch (Exception) { }
            }
            return null;
        }

        static object GetMember(object target, string name)
        {
            if (target == null) return null;
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var p = target.GetType().GetProperty(name, any);
            if (p != null) return p.GetValue(target);
            var f = target.GetType().GetField(name, any) ?? target.GetType().GetField("m_" + name, any);
            return f?.GetValue(target);
        }
    }
}
