using UnityEditor;
using UnityEngine;

namespace Flopzone.EditorTools
{
    /// <summary>
    /// Sloppy > Set Up VR (Quest). One big Import VR button (like BoostedVR):
    /// it installs XR Plug-in Management + OpenXR, waits for Unity to reload,
    /// then switches on OpenXR with Meta Quest support and the Touch controllers.
    /// </summary>
    public class VRSetup : EditorWindow
    {
        public static void Open()
        {
            var w = GetWindow<VRSetup>(true, "Sloppy VR", true);
            w.minSize = new Vector2(420, 300);
        }

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            var big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            var wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            bool installed = FlopzoneSetup.VRPackagesInstalled;
            bool ready = installed && FlopzoneSetup.VRReady;
            bool busy = FlopzoneSetup.Busy || EditorApplication.isCompiling || EditorApplication.isUpdating;

            GUILayout.Space(8);
            GUILayout.Label(ready ? "VR is ON" : installed ? "VR is installed, not switched on yet" : "VR is OFF", big);
            GUILayout.Label(ready
                ? "OpenXR is on for Quest (Android, for the APK) and PC VR (Quest Link in Play mode), with the Touch controllers."
                : "Click Import VR. It installs the XR Plug-in Management and OpenXR packages and sets them up for Meta Quest. " +
                  "Unity reloads once or twice while it works; just wait for the \"VR is ready\" message.", wrap);
            GUILayout.Space(14);

            using (new EditorGUI.DisabledScope(busy))
            {
                if (!installed)
                {
                    if (GUILayout.Button(busy ? "Importing VR..." : "Import VR", button, GUILayout.Height(44))) FlopzoneSetup.ImportVR(true);
                }
                else if (!ready)
                {
                    if (GUILayout.Button("Switch VR on", button, GUILayout.Height(44))) FlopzoneSetup.ConfigureXR(true);
                }
                else if (GUILayout.Button("Check VR settings again", GUILayout.Height(28))) FlopzoneSetup.ConfigureXR(true);
            }

            GUILayout.Space(10);
            GUILayout.Label("1. XR Plug-in Management + OpenXR: " + (installed ? "installed" : busy ? "installing..." : "not installed"), wrap);
            GUILayout.Label("2. OpenXR switched on for Quest: " + (ready ? "yes" : "no"), wrap);
            if (!string.IsNullOrEmpty(FlopzoneSetup.LastMessage))
            {
                GUILayout.Space(8);
                EditorGUILayout.HelpBox(FlopzoneSetup.LastMessage, MessageType.None);
            }
            GUILayout.FlexibleSpace();
            if (installed && GUILayout.Button("Open XR Plug-in Management settings")) SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
            GUILayout.Space(6);
        }
    }
}
