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
        static bool askedForText;

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

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!GameBuilder.TextResourcesReady)
            {
                if (!askedForText)
                {
                    askedForText = true;
                    waitingSince = EditorApplication.timeSinceStartup;
                    if (!GameBuilder.ImportTextMeshProEssentials())
                    {
                        Stop("Almost ready! Please click Window > TextMeshPro > Import TMP Essential Resources, then Veylix > Build Game World.");
                        return;
                    }
                    Debug.Log("[Veylix] First-time setup: adding text resources...");
                }
                else if (EditorApplication.timeSinceStartup - waitingSince > 180)
                    Stop("Almost ready! Please click Veylix > Build Game World (if it asks to import text resources, click Import).");
                return;
            }

            EditorApplication.update -= Tick;
            try { GameBuilder.BuildWorld(); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Stop("Couldn't finish building the game automatically. Try Veylix > Build Game World, and if it still fails send the red error from the Console.");
                return;
            }
            EditorSceneManager.OpenScene(GameBuilder.ScenePath);
            EditorUtility.DisplayDialog("Veylix", GameBuilder.ReadyMessage(), "Let's race");
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
