using System;
using UnityEditor;
using UnityEngine;

namespace Rago.EditorTools
{
    /// <summary>The "Rago Extract" tab: one-click VR import, multiplayer import, project setup and APK builds.</summary>
    public class RagoWindow : EditorWindow
    {
        Vector2 _scroll;
        double _nextRefresh;

        [MenuItem("Rago Extract/Open Rago Extract Tab", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<RagoWindow>("Rago Extract");
            w.minSize = new Vector2(420, 560);
            w.Show();
        }

        [MenuItem("Rago Extract/1. Import VR (OpenXR + Quest)", priority = 20)] static void MenuVr() => RagoSetup.Queue(RagoSetup.VrPackages);
        [MenuItem("Rago Extract/2. Import Multiplayer + Voice", priority = 21)] static void MenuNet() { RagoSetup.Queue(RagoSetup.NetPackages); RagoSetup.Queue(RagoSetup.VoicePackages); }
        [MenuItem("Rago Extract/3. Configure Project", priority = 22)] static void MenuCfg() { RagoSetup.ConfigureProject(); RagoSetup.CreateScene(true); RagoSetup.ConfigureXR(); }
        [MenuItem("Rago Extract/4. Build APK (Quest)", priority = 40)] static void MenuApk() => RagoSetup.BuildApk(false);
        [MenuItem("Rago Extract/5. Build And Run On Quest", priority = 41)] static void MenuRun() => RagoSetup.BuildApk(true);
        [MenuItem("Rago Extract/Build Windows (PC VR)", priority = 42)] static void MenuWin() => RagoSetup.BuildWindows();
        [MenuItem("Rago Extract/Delete Save (reset profile)", priority = 60)]
        static void MenuReset()
        {
            var p = System.IO.Path.Combine(Application.persistentDataPath, "rago_profile.json");
            if (System.IO.File.Exists(p) && EditorUtility.DisplayDialog("Rago Extract", "Delete your local profile (money, stash, loadout)?", "Delete", "Cancel")) System.IO.File.Delete(p);
        }

        void OnEnable() { RagoSetup.RefreshInstalled(); }

        void Update()
        {
            if (EditorApplication.timeSinceStartup > _nextRefresh) { _nextRefresh = EditorApplication.timeSinceStartup + 1.0; Repaint(); }
        }

        static GUIStyle _h1, _h2, _wrap;
        static void Styles()
        {
            if (_h1 != null) return;
            _h1 = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
            _h2 = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
        }

        void OnGUI()
        {
            Styles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(6);
            GUILayout.Label("RAGO EXTRACT", _h1);
            GUILayout.Label("VR ragdoll extraction shooter - setup & build", EditorStyles.miniLabel);
            GUILayout.Space(8);

            // ---------------------------------------------------------------- status
            GUILayout.Label("Status", _h2);
            Row("VR: XR Plug-in Management", RagoSetup.IsInstalled("com.unity.xr.management"));
            Row("VR: OpenXR (Quest / PC VR)", RagoSetup.IsInstalled("com.unity.xr.openxr"));
            Row("VR: OpenXR loader enabled for Android", RagoSetup.XRConfigured());
            Row("Multiplayer: Netcode for GameObjects", RagoSetup.IsInstalled("com.unity.netcode.gameobjects"));
            Row("Multiplayer: Lobby + Relay (matchmaking)", (RagoSetup.IsInstalled("com.unity.services.lobby") && RagoSetup.IsInstalled("com.unity.services.relay")) || RagoSetup.IsInstalled("com.unity.services.multiplayer"));
            Row("Voice chat: Vivox", RagoSetup.IsInstalled("com.unity.services.vivox"));
            Row("Multiplayer code compiled", Type.GetType("Rago.Net.NetBackendNgo, RagoExtract.Net") != null);
            Row("Voice code compiled", Type.GetType("Rago.Voice.VivoxVoice, RagoExtract.Voice") != null);
            Row("Linked to a Unity Cloud project", !string.IsNullOrEmpty(CloudProjectSettings.projectId));
            Row("Build target is Android", EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android);
            if (RagoSetup.Busy) EditorGUILayout.HelpBox("Importing packages... Unity will recompile a few times. Leave the editor open.", MessageType.Info);
            if (!string.IsNullOrEmpty(RagoSetup.LastMessage)) EditorGUILayout.HelpBox(RagoSetup.LastMessage, MessageType.None);
            GUILayout.Space(10);

            // ---------------------------------------------------------------- import
            GUILayout.Label("Import", _h2);
            using (new EditorGUI.DisabledScope(RagoSetup.Busy))
            {
                if (GUILayout.Button("Import VR  (OpenXR + Meta Quest support)", GUILayout.Height(32))) RagoSetup.Queue(RagoSetup.VrPackages);
                if (GUILayout.Button("Import Multiplayer  (Netcode + Lobby + Relay + Vivox voice)", GUILayout.Height(32))) { RagoSetup.Queue(RagoSetup.NetPackages); RagoSetup.Queue(RagoSetup.VoicePackages); }
            }
            if (GUILayout.Button("Configure VR + Project (player settings, layers, scene)", GUILayout.Height(26))) { RagoSetup.ConfigureProject(); RagoSetup.CreateScene(true); RagoSetup.ConfigureXR(); }
            if (RagoSetup.HasDefine("RAGO_DISABLE_NET") || RagoSetup.HasDefine("RAGO_DISABLE_VOICE"))
            {
                EditorGUILayout.HelpBox("A multiplayer/voice module was switched off because it didn't compile with your package versions (the game still runs offline). Check the Console, update packages, then retry.", MessageType.Warning);
                if (GUILayout.Button("Retry multiplayer + voice modules")) { RagoSetup.RemoveDefine("RAGO_DISABLE_NET"); RagoSetup.RemoveDefine("RAGO_DISABLE_VOICE"); }
            }
            GUILayout.Space(10);

            // ---------------------------------------------------------------- build
            GUILayout.Label("Build", _h2);
            if (GUILayout.Button("Build APK  (Quest)", GUILayout.Height(36))) RagoSetup.BuildApk(false);
            if (GUILayout.Button("Build And Run On Quest  (USB, developer mode)", GUILayout.Height(28))) RagoSetup.BuildApk(true);
            if (GUILayout.Button("Build Windows  (PC VR / desktop)")) RagoSetup.BuildWindows();
            if (GUILayout.Button("Open game scene")) RagoSetup.CreateScene(true);
            GUILayout.Space(10);

            // ---------------------------------------------------------------- help
            GUILayout.Label("Online multiplayer setup (one time)", _h2);
            GUILayout.Label(
                "1. Edit > Project Settings > Services: sign in and link (or create) a Unity Cloud project.\n" +
                "2. In the Unity Cloud dashboard enable Lobby, Relay and Vivox for that project.\n" +
                "3. Press Play / build the APK. Without this the game runs in offline mode (solo raids with AI).", _wrap);
            GUILayout.Space(6);
            GUILayout.Label("Playing", _h2);
            GUILayout.Label(
                "Press Play to test in the editor. No headset: WASD move, mouse look, Shift sprint, C crouch, E grab what you look at, " +
                "LMB fire / click buttons, RMB aim, R reload, F rack/interact, X drop mag, 1/2/3 weapons, Tab backpack, G drop.\n\n" +
                "VR: left stick move (click = sprint), right stick turn (click = crouch), grip = grab, trigger = fire, " +
                "A/X = drop mag, B/Y = rack / fire mode. Grab mags from your chest pouch, pistol from your right hip, rifle over your right shoulder, " +
                "backpack over your left shoulder, second rifle behind your lower back.", _wrap);
            EditorGUILayout.EndScrollView();
        }

        static void Row(string label, bool ok)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var c = GUI.color;
                GUI.color = ok ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.6f, 0.4f);
                GUILayout.Label(ok ? "OK" : "--", EditorStyles.boldLabel, GUILayout.Width(26));
                GUI.color = c;
                GUILayout.Label(label);
            }
        }
    }
}
