using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Veylix.EditorTools
{
    /// <summary>
    /// First-time setup, no clicks needed: when the project opens and the game world hasn't been built yet, this adds
    /// the text resources, builds it (same as Veylix > Build Game World) and opens the scene, so you can just press
    /// Play. On later opens it simply opens the game scene if nothing else is open.
    /// </summary>
    [InitializeOnLoad]
    static class VeylixAutoSetup
    {
        static double waitingSince;

        static VeylixAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Begin;
        }

        static string PendingKey => "Veylix.PendingFusionSetup." + Application.dataPath;

        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            RenderSetup.Apply();
            InputCheck();

            // Photon Fusion was just imported: finish multiplayer setup once Unity is idle.
            if (Online.Available && EditorPrefs.GetBool(PendingKey, false) && GameBuilder.TextResourcesReady)
            {
                EditorApplication.update -= FinishMultiplayer;
                EditorApplication.update += FinishMultiplayer;
                return;
            }

            if (File.Exists(GameBuilder.ScenePath) && !GameBuilder.WorldUpToDate)
                Debug.Log("[Veylix] This version changes the generated game world; rebuilding it...");
            else if (File.Exists(GameBuilder.ScenePath))
            {
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(GameBuilder.ScenePath);
                return;
            }

            waitingSince = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// Desktop controls use the Input System package; its native backend must be switched on (Unity asks once and
        /// restarts). VR controllers work either way.
        /// </summary>
        static void InputCheck()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p == null || p.intValue != 0) return;
            const string Asked = "Veylix.AskedInputBackend";
            if (EditorPrefs.GetBool(Asked + Application.dataPath, false)) return;
            EditorPrefs.SetBool(Asked + Application.dataPath, true);
            if (!EditorUtility.DisplayDialog("Veylix",
                    "Keyboard, mouse and gamepad controls need Unity's Input System switched on (VR controllers work without it).\n\nSwitch it on now? Unity will restart.",
                    "Switch on and restart", "Later"))
                return;
            p.intValue = 2; // Both: old and new input
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }

        // ------------------------------------------------------------------ first-time build (same as the menu)

        const string ImportAskedKey = "Veylix.Setup.TextImportAsked";
        const string AttemptsKey = "Veylix.Setup.Attempts";
        static double lastCheck;
        static bool building;

        /// <summary>
        /// Waits until Unity is idle and the text resources are in, then runs exactly what Veylix > Build Game World
        /// runs. It doesn't give up: it keeps waiting through imports and script reloads (state is kept in SessionState),
        /// and retries the build once if the first try fails.
        /// </summary>
        /// Start (or restart) the automatic build: waits for text resources, then builds.
        public static void StartSetup()
        {
            waitingSince = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (building || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup - lastCheck < 0.5) return;
            lastCheck = EditorApplication.timeSinceStartup;

            if (!TextReady())
            {
                if (!SessionState.GetBool(ImportAskedKey, false))
                {
                    SessionState.SetBool(ImportAskedKey, true);
                    waitingSince = EditorApplication.timeSinceStartup;
                    Debug.Log("[Veylix] First-time setup: adding TextMeshPro resources...");
                    AssetDatabase.importPackageCompleted -= OnPackageImported;
                    AssetDatabase.importPackageCompleted += OnPackageImported;
                    if (!GameBuilder.ImportTextMeshProEssentials())
                    {
                        Stop("Almost ready! Please click Window > TextMeshPro > Import TMP Essential Resources. Veylix will then build itself.");
                        SessionState.SetBool(ImportAskedKey, false);
                        return;
                    }
                }
                EditorUtility.DisplayProgressBar("Veylix", "First-time setup: adding text resources...", 0.05f);
                if (EditorApplication.timeSinceStartup - waitingSince > 600)
                    Stop("The text resources are taking very long. Click Window > TextMeshPro > Import TMP Essential Resources, then Veylix > Build Game World.");
                return;
            }

            // Ready: build on the next editor tick, outside this update loop (just like clicking the menu).
            EditorApplication.update -= Tick;
            EditorUtility.ClearProgressBar();
            building = true;
            EditorApplication.delayCall += BuildNow;
        }

        static void OnPackageImported(string name)
        {
            AssetDatabase.importPackageCompleted -= OnPackageImported;
            AssetDatabase.Refresh();
            lastCheck = 0;
        }

        static bool TextReady()
        {
            if (GameBuilder.TextResourcesReady) return true;
            // Imported but not loadable yet: refresh once in a while so Resources picks it up.
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                AssetDatabase.Refresh();
                return GameBuilder.TextResourcesReady;
            }
            return false;
        }

        static void BuildNow()
        {
            try
            {
                Debug.Log("[Veylix] Building the game world (same as Veylix > Build Game World)...");
                GameBuilder.BuildWorld();
                SessionState.EraseInt(AttemptsKey);
                SessionState.EraseBool(ImportAskedKey);
                building = false;
                EditorSceneManager.OpenScene(GameBuilder.ScenePath);
                EditorUtility.DisplayDialog("Veylix", GameBuilder.ReadyMessage(), "Let's race");
            }
            catch (System.Exception e)
            {
                building = false;
                Debug.LogException(e);
                int attempts = SessionState.GetInt(AttemptsKey, 0) + 1;
                SessionState.SetInt(AttemptsKey, attempts);
                if (attempts < 2)
                {
                    Debug.Log("[Veylix] First build attempt failed; trying again in a moment...");
                    AssetDatabase.Refresh();
                    waitingSince = EditorApplication.timeSinceStartup;
                    EditorApplication.update -= Tick;
                    EditorApplication.update += Tick;
                    return;
                }
                var inner = e is System.Reflection.TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                Stop("Couldn't finish building the game automatically:\n\n" + inner.GetType().Name + ": " + inner.Message + "\n\nTry Veylix > Build Game World. If it fails again, send a screenshot of this message and the first red line in the Console.");
            }
        }

        static void FinishMultiplayer()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= FinishMultiplayer;
            EditorPrefs.DeleteKey(PendingKey);
            try
            {
                EditorUtility.DisplayProgressBar("Veylix", "Turning multiplayer on and rebuilding the game scene...", 0.6f);
                MultiplayerSetup.FinishSetup(false);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Stop("Multiplayer is installed, but setup didn't finish. Open Veylix > Import Multiplayer and click Finish setup.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void Stop(string message)
        {
            EditorApplication.update -= Tick;
            EditorUtility.ClearProgressBar();
            Debug.LogWarning("[Veylix] " + message);
            EditorUtility.DisplayDialog("Veylix", message, "OK");
        }
    }
}
