using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Flopzone.EditorTools
{
    /// <summary>
    /// The "Flopzone" tab: Import Multiplayer, Import VR Support, Make APK,
    /// plus setup status and moderator tools that talk to the live server.
    /// </summary>
    public class FlopzoneWindow : EditorWindow
    {
        Vector2 scroll;
        string serverUrl;
        string connectionResult = "";
        UnityWebRequest pending;
        Action<UnityWebRequest> pendingDone;
        // moderation
        string modKey, modTarget = "", modReason = "", modOutput = "";
        int modMinutes = 60;
        bool showMod;

        [MenuItem("Flopzone/Open Flopzone Tab", false, 0)]
        [MenuItem("Window/Flopzone")]
        public static void Open()
        {
            var w = GetWindow<FlopzoneWindow>("Flopzone");
            w.minSize = new Vector2(360, 480);
            w.Show();
        }

        [MenuItem("Flopzone/Import Multiplayer", false, 20)] static void MenuMultiplayer() { Open(); GetWindow<FlopzoneWindow>().ImportMultiplayer(); }
        [MenuItem("Flopzone/Import VR Support", false, 21)] static void MenuVR() => FlopzoneSetup.ImportVR(true);
        [MenuItem("Flopzone/Make APK", false, 22)] static void MenuApk() => FlopzoneBuild.BuildApk(false);
        [MenuItem("Flopzone/Make APK and Run on Quest", false, 23)] static void MenuApkRun() => FlopzoneBuild.BuildApk(true);
        [MenuItem("Flopzone/Run Setup Again", false, 40)] static void MenuSetup() => FlopzoneSetup.RunAll(true);

        void OnEnable()
        {
            serverUrl = FlopzoneSetup.Config != null ? FlopzoneSetup.Config.serverUrl : "";
            modKey = EditorPrefs.GetString("Flopzone.AdminKey", "");
        }

        void OnInspectorUpdate() => Repaint();

        static GUIStyle Title => new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        static GUIStyle Big => new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 40, fontStyle = FontStyle.Bold };

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);
            GUILayout.Label("FLOPZONE", Title);
            GUILayout.Label("VR ragdoll shooter · setup version " + FlopzoneSetup.Version, EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(10);

            // ---------------- the three main buttons ----------------
            GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            if (GUILayout.Button("Import Multiplayer", Big)) ImportMultiplayer();
            GUI.backgroundColor = new Color(0.7f, 1f, 0.6f);
            using (new EditorGUI.DisabledScope(FlopzoneSetup.Busy))
                if (GUILayout.Button(FlopzoneSetup.Busy ? "Importing VR packages..." : "Import VR Support", Big)) FlopzoneSetup.ImportVR(true);
            GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
            if (GUILayout.Button("Make APK", Big)) FlopzoneBuild.BuildApk(false);
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Make APK and run it on my Quest (USB)")) FlopzoneBuild.BuildApk(true);
            if (!string.IsNullOrEmpty(FlopzoneSetup.LastMessage)) EditorGUILayout.HelpBox(FlopzoneSetup.LastMessage, MessageType.Info);

            // ---------------- status ----------------
            GUILayout.Space(10);
            GUILayout.Label("Status", EditorStyles.boldLabel);
            Check("Game scene and assets", File.Exists(FlopzoneSetup.ScenePath) && File.Exists(FlopzoneSetup.AssetsPath));
            Check("VR packages (XR Management + OpenXR)", FlopzoneSetup.VRPackagesInstalled);
            Check("OpenXR on for Quest (Android)", FlopzoneSetup.XRLoaderAssigned(BuildTargetGroup.Android));
            Check("Android build support installed", BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android));
            Check("Online server set (otherwise offline with bots)", FlopzoneConfig.I != null && !string.IsNullOrEmpty(FlopzoneSetup.Config.serverUrl));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run setup again")) FlopzoneSetup.RunAll(true);
            if (GUILayout.Button("Open game scene")) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(FlopzoneSetup.ScenePath);
            if (GUILayout.Button(EditorApplication.isPlaying ? "Stop" : "Play")) EditorApplication.isPlaying = !EditorApplication.isPlaying;
            GUILayout.EndHorizontal();

            // ---------------- multiplayer ----------------
            GUILayout.Space(10);
            GUILayout.Label("Multiplayer server", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Friends, parties, unique names, matchmaking, the chat auto-mod, reports and bans all run on a small free Cloudflare Worker in the Server folder. Deploy it once (see Server/README.md), then paste its URL here. Leave it empty to play offline against bots.", MessageType.None);
            serverUrl = EditorGUILayout.TextField("Server URL", serverUrl);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save URL")) SaveUrl();
            if (GUILayout.Button("Test connection")) TestConnection();
            if (GUILayout.Button("Server instructions")) OpenServerReadme();
            GUILayout.EndHorizontal();
            if (connectionResult.Length > 0) EditorGUILayout.HelpBox(connectionResult, connectionResult.StartsWith("Connected") ? MessageType.Info : MessageType.Warning);

            // ---------------- moderation ----------------
            GUILayout.Space(10);
            showMod = EditorGUILayout.Foldout(showMod, "Moderation (kick / ban / mute by player ID)", true);
            if (showMod) ModGUI();

            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Testing without a headset: press Play. WASD move, click to look around, LMB shoot, E/Q grab, R reload, F drop mag, Space jump, Shift sprint, C crouch, Tab menu.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static void Check(string label, bool ok)
        {
            var c = GUI.color;
            GUI.color = ok ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.75f, 0.4f);
            GUILayout.Label((ok ? "✔  " : "•  ") + label);
            GUI.color = c;
        }

        void ImportMultiplayer()
        {
            // Multiplayer is part of the game code (no package to install); this
            // makes sure the config exists, saves the URL and checks the server.
            SaveUrl();
            if (string.IsNullOrEmpty(serverUrl))
            {
                connectionResult = "Multiplayer is built in. Deploy the server (Server instructions), paste its URL above, and press Import Multiplayer again. Until then the game runs offline with bots.";
                if (EditorUtility.DisplayDialog("Flopzone multiplayer",
                    "Multiplayer is already in the game. To play online with friends you deploy the free Flopzone server once (about 5 minutes), then paste its URL here.\n\nOpen the instructions?", "Open instructions", "Later"))
                    OpenServerReadme();
                return;
            }
            TestConnection();
        }

        void SaveUrl()
        {
            var cfg = FlopzoneSetup.Config;
            if (cfg == null) return;
            cfg.serverUrl = (serverUrl ?? "").Trim();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
        }

        static string HttpBase(string url)
        {
            url = (url ?? "").Trim().TrimEnd('/');
            if (url.EndsWith("/ws")) url = url.Substring(0, url.Length - 3);
            if (url.StartsWith("wss://")) url = "https://" + url.Substring(6);
            else if (url.StartsWith("ws://")) url = "http://" + url.Substring(5);
            else if (!url.StartsWith("http")) url = "https://" + url;
            return url;
        }

        void TestConnection()
        {
            if (string.IsNullOrEmpty(serverUrl)) { connectionResult = "No server URL yet."; return; }
            connectionResult = "Testing...";
            Request(UnityWebRequest.Get(HttpBase(serverUrl) + "/"), r =>
            {
                if (r.result == UnityWebRequest.Result.Success && r.downloadHandler.text.Contains("Flopzone"))
                    connectionResult = "Connected! The server is running. Players will go online automatically.";
                else connectionResult = "Couldn't reach a Flopzone server at that URL: " + (r.error ?? r.downloadHandler.text);
            });
        }

        void Request(UnityWebRequest r, Action<UnityWebRequest> done)
        {
            pending = r;
            pendingDone = done;
            r.SendWebRequest();
            EditorApplication.update += PollRequest;
        }

        void PollRequest()
        {
            if (pending == null || !pending.isDone) return;
            EditorApplication.update -= PollRequest;
            var r = pending;
            pending = null;
            try { pendingDone?.Invoke(r); }
            finally { r.Dispose(); }
            Repaint();
        }

        static void OpenServerReadme()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Server", "README.md"));
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else EditorUtility.DisplayDialog("Flopzone", "Server/README.md wasn't found next to the Assets folder.", "OK");
        }

        void ModGUI()
        {
            EditorGUILayout.HelpBox("In-game: put your player ID (Menu > Profile) in ADMIN_IDS in Server/wrangler.toml and redeploy; you then get a MOD tab in the menu.\nHere: set ADMIN_KEY on the server (npx wrangler secret put ADMIN_KEY) and type the same key below.", MessageType.None);
            string key = EditorGUILayout.PasswordField("Admin key", modKey);
            if (key != modKey) { modKey = key; EditorPrefs.SetString("Flopzone.AdminKey", modKey); }
            modTarget = EditorGUILayout.TextField("Player ID or name", modTarget);
            modMinutes = EditorGUILayout.IntField("Minutes (0 = forever)", modMinutes);
            modReason = EditorGUILayout.TextField("Reason", modReason);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Lookup")) Mod("lookup");
            if (GUILayout.Button("Kick")) Mod("kick");
            if (GUILayout.Button("Mute")) Mod("mute");
            if (GUILayout.Button("Unmute")) Mod("unmute");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ban")) Mod("ban");
            if (GUILayout.Button("Unban")) Mod("unban");
            if (GUILayout.Button("Reset name")) Mod("rename");
            if (GUILayout.Button("Make moderator")) Mod("promote");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Recent reports")) Mod("reports");
            if (GUILayout.Button("Who's online")) Mod("online");
            GUILayout.EndHorizontal();
            if (modOutput.Length > 0) EditorGUILayout.TextArea(modOutput, GUILayout.MinHeight(120));
        }

        void Mod(string action)
        {
            if (string.IsNullOrEmpty(serverUrl)) { modOutput = "Set the server URL first."; return; }
            if (string.IsNullOrEmpty(modKey)) { modOutput = "Type the admin key first."; return; }
            string url = HttpBase(serverUrl) + "/admin?key=" + UnityWebRequest.EscapeURL(modKey) + "&action=" + action +
                         "&target=" + UnityWebRequest.EscapeURL(modTarget ?? "") + "&minutes=" + modMinutes + "&reason=" + UnityWebRequest.EscapeURL(modReason ?? "");
            modOutput = "...";
            Request(UnityWebRequest.Get(url), r => modOutput = r.downloadHandler != null && !string.IsNullOrEmpty(r.downloadHandler.text) ? r.downloadHandler.text : r.error);
        }
    }

    public static class FlopzoneBuild
    {
        public static void BuildApk(bool run)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Flopzone: Android support needed",
                    "To make an APK, Unity needs Android Build Support.\n\nOpen Unity Hub > Installs > the gear next to your Unity version > Add modules, and tick 'Android Build Support' (with OpenJDK and Android SDK & NDK Tools). Then reopen this project.", "OK");
                return;
            }
            if (!File.Exists(FlopzoneSetup.ScenePath)) FlopzoneSetup.RunAll(true);
            if (!FlopzoneSetup.XRLoaderAssigned(BuildTargetGroup.Android) &&
                !EditorUtility.DisplayDialog("Flopzone", "VR isn't switched on for Android yet, so the APK would run flat. Build anyway?\n\n(Press 'Import VR Support' first to fix this.)", "Build anyway", "Cancel"))
                return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds");
            string path = Path.GetFullPath("Builds/Flopzone.apk");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { FlopzoneSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                FlopzoneSetup.LastMessage = "APK built: " + path;
                if (!run) EditorUtility.RevealInFinder(path);
                EditorUtility.DisplayDialog("Flopzone", "APK ready:\n" + path + "\n\nInstall it on your Quest with SideQuest or the Meta Quest Developer Hub (developer mode on), or use 'Make APK and run it on my Quest'.", "OK");
            }
            else
            {
                FlopzoneSetup.LastMessage = "APK build failed (" + report.summary.totalErrors + " errors). See the Console for details.";
                EditorUtility.DisplayDialog("Flopzone", FlopzoneSetup.LastMessage, "OK");
            }
        }
    }
}
