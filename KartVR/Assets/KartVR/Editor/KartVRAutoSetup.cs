using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// First-time setup, with no clicks needed: when the project is opened and the
    /// game world hasn't been built yet, this adds the text resources, builds the
    /// world (same as KartVR > Build Prototype World) and opens it, so you can just
    /// press Play. On later opens it simply opens the game scene if nothing else is open.
    /// </summary>
    [InitializeOnLoad]
    static class KartVRAutoSetup
    {
        static double waitingSince;
        static bool askedForText;

        static KartVRAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Begin;
        }

        static string PendingKey => "KartVR.PendingFusionSetup." + Application.dataPath;

        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Photon Fusion was just imported: finish multiplayer setup once Unity is idle.
            if (SessionManager.MultiplayerAvailable && EditorPrefs.GetBool(PendingKey, false) && KartWorldBuilder.TextResourcesReady)
            {
                EditorApplication.update -= FinishMultiplayer;
                EditorApplication.update += FinishMultiplayer;
                return;
            }

            if (File.Exists(KartWorldBuilder.WorldScenePath) && !KartWorldBuilder.WorldUpToDate)
                Debug.Log("[KartVR] This version changes the world (new garage hub, smoother ramps); rebuilding it...");
            else if (File.Exists(KartWorldBuilder.WorldScenePath))
            {
                // Open straight into the game instead of an empty "Untitled" scene.
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(KartWorldBuilder.WorldScenePath);
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

            if (!KartWorldBuilder.TextResourcesReady)
            {
                if (!askedForText)
                {
                    askedForText = true;
                    waitingSince = EditorApplication.timeSinceStartup;
                    if (!KartWorldBuilder.ImportTextMeshProEssentials())
                    {
                        Stop("Almost ready! Please click Window > TextMeshPro > Import TMP Essential Resources, then KartVR > Build Prototype World.");
                        return;
                    }
                    Debug.Log("[KartVR] First-time setup: adding text resources...");
                }
                else if (EditorApplication.timeSinceStartup - waitingSince > 180)
                {
                    Stop("Almost ready! Please click KartVR > Build Prototype World (if it asks to import text resources, click Import).");
                }
                return;
            }

            EditorApplication.update -= Tick;
            try
            {
                EditorUtility.DisplayProgressBar("Kart VR", "Building the game world...", 0.6f);
                KartWorldBuilder.BuildWorld();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Stop("Couldn't finish building the world automatically. Try KartVR > Build Prototype World, and if it still fails send the red error from the Console.");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog("Kart VR",
                "Kart VR is ready! Press the Play button at the top.\n\n" +
                "No headset? W/A/S/D to walk or drive, Q/E to turn, hold right mouse to look around, left click to press buttons.\n\n" +
                (SessionManager.MultiplayerAvailable
                    ? "Multiplayer is ON (set your Photon App ID in Tools > Fusion > Fusion Hub)."
                    : "You're playing OFFLINE (solo). Import Photon Fusion 2 later to play online with friends."),
                "Let's go");
        }

        static void FinishMultiplayer()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= FinishMultiplayer;
            EditorPrefs.DeleteKey(PendingKey);
            try
            {
                EditorUtility.DisplayProgressBar("Kart VR", "Turning multiplayer on and rebuilding the world...", 0.6f);
                MultiplayerSetup.FinishSetup(false);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Stop("Multiplayer is installed, but setup didn't finish. Open KartVR > Set Up Multiplayer and click Finish setup.");
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
            Debug.LogWarning("[KartVR] " + message);
            EditorUtility.DisplayDialog("Kart VR", message, "OK");
        }
    }
}
