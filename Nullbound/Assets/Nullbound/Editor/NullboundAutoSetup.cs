using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// First-time setup, with no clicks needed: when the project is opened and the
    /// game world hasn't been built yet, this adds the text resources, builds the
    /// world (same as Nullbound > Build Prototype World) and opens it, so you can just
    /// press Play. On later opens it simply opens the game scene if nothing else is open.
    /// </summary>
    [InitializeOnLoad]
    static class NullboundAutoSetup
    {
        static double waitingSince;
        static bool askedForText;

        static NullboundAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Begin;
        }

        static string PendingKey => "Nullbound.PendingFusionSetup." + Application.dataPath;

        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Photon Fusion was just imported: finish multiplayer setup once Unity is idle.
            if (SessionManager.MultiplayerAvailable && EditorPrefs.GetBool(PendingKey, false) && ArenaBuilder.TextResourcesReady)
            {
                EditorApplication.update -= FinishMultiplayer;
                EditorApplication.update += FinishMultiplayer;
                return;
            }

            if (File.Exists(ArenaBuilder.WorldScenePath) && !ArenaBuilder.WorldUpToDate)
                Debug.Log("[Nullbound] This version changes the arena; rebuilding it...");
            else if (File.Exists(ArenaBuilder.WorldScenePath))
            {
                // Open straight into the game instead of an empty "Untitled" scene.
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(ArenaBuilder.WorldScenePath);
                return;
            }

            waitingSince = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            // Wait until Unity has finished importing and compiling.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!ArenaBuilder.TextResourcesReady)
            {
                if (!askedForText)
                {
                    askedForText = true;
                    waitingSince = EditorApplication.timeSinceStartup;
                    if (!ArenaBuilder.ImportTextMeshProEssentials())
                    {
                        Stop("Almost ready! Please click Window > TextMeshPro > Import TMP Essential Resources, then Nullbound > Build Prototype World.");
                        return;
                    }
                    Debug.Log("[Nullbound] First-time setup: adding text resources...");
                }
                else if (EditorApplication.timeSinceStartup - waitingSince > 180)
                {
                    Stop("Almost ready! Please click Nullbound > Build Prototype World (if it asks to import text resources, click Import).");
                }
                return;
            }

            EditorApplication.update -= Tick;
            try
            {
                EditorUtility.DisplayProgressBar("Nullbound", "Building the arena...", 0.6f);
                ArenaBuilder.BuildWorld();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Stop("Couldn't finish building the world automatically. Try Nullbound > Build Prototype World, and if it still fails send the red error from the Console.");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog("Nullbound",
                "Nullbound is ready! Press the Play button at the top.\n\n" +
                "No headset? W/A/S/D move, Q/E turn, hold right mouse to look, LEFT CLICK punch, G left punch, hold H block, Space jump, Shift dash, F special, Tab menu.\n\n" +
                (SessionManager.MultiplayerAvailable
                    ? "Multiplayer is ON (set your Photon App ID with Nullbound > Set Up Multiplayer)."
                    : "Online play is OFF until you import Photon Fusion 2 (Nullbound > Set Up Multiplayer). Training vs bots works now."),
                "Fight!");
        }

        static void FinishMultiplayer()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= FinishMultiplayer;
            EditorPrefs.DeleteKey(PendingKey);
            try
            {
                EditorUtility.DisplayProgressBar("Nullbound", "Turning multiplayer on and rebuilding the world...", 0.6f);
                MultiplayerSetup.FinishSetup(false);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Stop("Multiplayer is installed, but setup didn't finish. Open Nullbound > Set Up Multiplayer and click Finish setup.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Stop(string message)
        {
            EditorApplication.update -= Tick;
            EditorUtility.ClearProgressBar();
            Debug.LogWarning("[Nullbound] " + message);
            EditorUtility.DisplayDialog("Nullbound", message, "OK");
        }
    }
}
