using UnityEditor;
using UnityEngine;

namespace Rago.EditorTools
{
    /// <summary>The Rago Extract tab: three buttons - Set Up VR, Multiplayer, Build APK.</summary>
    public class RagoWindow : EditorWindow
    {
        double _nextRefresh;

        [MenuItem("Rago Extract/Open Rago Extract Tab", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<RagoWindow>("Rago Extract");
            w.minSize = new Vector2(360, 300);
            w.Show();
        }

        [MenuItem("Rago Extract/Set Up VR", priority = 20)] static void MenuVr() => SetUpVr();
        [MenuItem("Rago Extract/Multiplayer", priority = 21)] static void MenuNet() => RagoMultiplayerWindow.Open();
        [MenuItem("Rago Extract/Build APK", priority = 22)] static void MenuApk() => RagoSetup.BuildApk(false);

        static void SetUpVr()
        {
            bool installed = RagoSetup.IsInstalled("com.unity.xr.management") && RagoSetup.IsInstalled("com.unity.xr.openxr");
            if (installed) RagoSetup.ConfigureXR();
            else RagoSetup.Queue(RagoSetup.VrPackages); // OpenXR is switched on automatically once the import finishes
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup > _nextRefresh) { _nextRefresh = EditorApplication.timeSinceStartup + 1.0; Repaint(); }
        }

        void OnGUI()
        {
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            var big = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 44 };
            GUILayout.Space(8);
            GUILayout.Label("Rago Extract", title);
            GUILayout.Space(8);

            bool vr = RagoSetup.XRConfigured();
            if (GUILayout.Button(vr ? "Set Up VR   (done)" : "Set Up VR", big)) SetUpVr();
            GUILayout.Space(4);
            if (GUILayout.Button(RagoMultiplayer.IsOn ? "Multiplayer   (on)" : "Multiplayer", big)) RagoMultiplayerWindow.Open();
            GUILayout.Space(4);
            if (GUILayout.Button("Build APK", big)) RagoSetup.BuildApk(false);

            GUILayout.Space(8);
            if (RagoSetup.Busy) EditorGUILayout.HelpBox("Importing... Unity will recompile a few times. Leave the editor open.", MessageType.Info);
            else if (!string.IsNullOrEmpty(RagoSetup.LastMessage)) EditorGUILayout.HelpBox(RagoSetup.LastMessage, MessageType.None);
        }
    }
}
