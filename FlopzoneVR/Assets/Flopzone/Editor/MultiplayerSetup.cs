using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Flopzone.EditorTools
{
    /// <summary>The top "Flopzone" menu.</summary>
    static class FlopzoneMenu
    {
        [MenuItem("Flopzone/Set Up Multiplayer", priority = 0)]
        static void Multiplayer() => MultiplayerSetup.Open();

        [MenuItem("Flopzone/Set Up VR (Quest)", priority = 1)]
        static void VR()
        {
            // also repairs the scene/assets if they went missing
            if (!File.Exists(FlopzoneSetup.ScenePath) || !File.Exists(FlopzoneSetup.AssetsPath)) FlopzoneSetup.RunAll(true);
            else FlopzoneSetup.ImportVR(true);
        }

        [MenuItem("Flopzone/Build for Quest (APK)", priority = 2)]
        static void Apk() => FlopzoneBuild.BuildApk(false);
    }

    /// <summary>
    /// Flopzone > Set Up Multiplayer. Three steps, each one button: install
    /// Node.js, sign in to Cloudflare (free), turn on the server. The server's
    /// address is filled in for you. Until then the game plays offline with bots.
    /// </summary>
    public class MultiplayerSetup : EditorWindow
    {
        string url;
        string status = "";
        bool statusOk;
        readonly StringBuilder log = new StringBuilder();
        Process proc;
        string running = "";
        Action<int, string> onExit;
        Vector2 scroll, logScroll;
        UnityWebRequest pending;
        Action<UnityWebRequest> pendingDone;
        bool? nodeInstalled;
        // moderation
        bool showMod;
        string modKey, modTarget = "", modReason = "", modOutput = "";
        int modMinutes = 60;

        static string ServerDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Server"));

        public static void Open()
        {
            var w = GetWindow<MultiplayerSetup>(true, "Flopzone Multiplayer", true);
            w.minSize = new Vector2(480, 560);
        }

        void OnEnable()
        {
            url = FlopzoneSetup.Config != null ? FlopzoneSetup.Config.serverUrl : "";
            modKey = EditorPrefs.GetString("Flopzone.AdminKey", "");
            CheckNode();
        }

        void OnDisable() => KillProcess();

        void OnInspectorUpdate() => Repaint();

        bool Online => !string.IsNullOrEmpty(url);

        void OnGUI()
        {
            var big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            var wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);
            GUILayout.Label(Online ? "Multiplayer is ON" : "Multiplayer is OFF (playing offline with bots)", big);
            GUILayout.Label(Online
                ? "Friends, parties, matchmaking, unique names, the chat auto-mod, reports and bans all work. Build a new APK so the headset uses this server."
                : "Do these 3 steps once. Each one is a button. It's free.", wrap);
            GUILayout.Space(12);

            using (new EditorGUI.DisabledScope(proc != null))
            {
                // Step 1
                GUILayout.Label("1. Install Node.js" + (nodeInstalled == true ? "   ✔" : ""), EditorStyles.boldLabel);
                if (nodeInstalled == true) GUILayout.Label("Node.js is installed.", wrap);
                else
                {
                    GUILayout.Label("Download the LTS version, install it, then restart Unity.", wrap);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Open nodejs.org")) Application.OpenURL("https://nodejs.org");
                    if (GUILayout.Button("Check again", GUILayout.Width(100))) CheckNode();
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(10);

                // Step 2
                GUILayout.Label("2. Sign in to Cloudflare (free)", EditorStyles.boldLabel);
                GUILayout.Label("A browser opens. Make a free account (or log in) and click Allow.", wrap);
                if (GUILayout.Button("Sign in to Cloudflare")) Run("Signing in", "npx --yes wrangler login", (code, outp) =>
                {
                    SetStatus(code == 0 ? "Signed in. Now press Turn On Server." : "Sign-in didn't finish. Try again.", code == 0);
                });
                GUILayout.Space(10);

                // Step 3
                GUILayout.Label("3. Turn on the server", EditorStyles.boldLabel);
                GUILayout.Label("Puts the Flopzone server online and fills in its address below.", wrap);
                if (GUILayout.Button("Turn On Server", GUILayout.Height(30))) Deploy();
            }

            GUILayout.Space(10);
            GUILayout.Label("Server address", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            url = EditorGUILayout.TextField(url);
            if (EditorGUI.EndChangeCheck()) SaveUrl();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test connection")) TestConnection();
            if (Online && GUILayout.Button("Play offline instead")) { url = ""; SaveUrl(); SetStatus("Offline: the game plays against bots.", true); }
            GUILayout.EndHorizontal();

            if (status.Length > 0) EditorGUILayout.HelpBox(status, statusOk ? MessageType.Info : MessageType.Warning);
            if (proc != null) EditorGUILayout.HelpBox(running + "... (this can take a minute)", MessageType.None);
            if (log.Length > 0)
            {
                logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(110));
                GUILayout.Label(log.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            }

            GUILayout.Space(10);
            showMod = EditorGUILayout.Foldout(showMod, "Moderation (kick / ban / mute by player ID)", true);
            if (showMod) ModGUI();
            EditorGUILayout.EndScrollView();
        }

        void SetStatus(string s, bool ok) { status = s; statusOk = ok; Repaint(); }

        void SaveUrl()
        {
            var cfg = FlopzoneSetup.Config;
            if (cfg == null) return;
            cfg.serverUrl = (url ?? "").Trim();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
        }

        /* ---------------- running Node / wrangler ---------------- */

        void CheckNode()
        {
            nodeInstalled = null;
            Run(null, "node --version", (code, outp) => { nodeInstalled = code == 0 && outp.Contains("v"); Repaint(); }, quiet: true);
        }

        void Deploy()
        {
            if (nodeInstalled != true) { SetStatus("Install Node.js first (step 1), then restart Unity.", false); return; }
            log.Clear();
            Run("Installing server tools", "npm install --no-audit --no-fund", (c1, o1) =>
            {
                if (c1 != 0) { SetStatus("Couldn't install the server tools. See the log below.", false); return; }
                Run("Turning on the server", "npx wrangler deploy", (c2, o2) =>
                {
                    var m = Regex.Match(o2, @"https://[A-Za-z0-9.\-]+\.workers\.dev");
                    if (c2 == 0 && m.Success)
                    {
                        url = m.Value;
                        SaveUrl();
                        SetStatus("Server is on! Testing the connection...", true);
                        TestConnection();
                    }
                    else if (o2.Contains("login") || o2.Contains("authenticate") || o2.Contains("Not logged in"))
                        SetStatus("You need to sign in first (step 2).", false);
                    else if (o2.Contains("workers.dev subdomain") || o2.Contains("subdomain"))
                    {
                        SetStatus("Cloudflare wants you to pick a free workers.dev name once. The dashboard is opening: go to Workers & Pages, pick a name, then press Turn On Server again.", false);
                        Application.OpenURL("https://dash.cloudflare.com/?to=/:account/workers-and-pages");
                    }
                    else SetStatus("The server didn't turn on. See the log below.", false);
                });
            });
        }

        void Run(string label, string command, Action<int, string> done, bool quiet = false)
        {
            KillProcess();
            var psi = new ProcessStartInfo
            {
                WorkingDirectory = ServerDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (Application.platform == RuntimePlatform.WindowsEditor) { psi.FileName = "cmd.exe"; psi.Arguments = "/c " + command; }
            else { psi.FileName = "/bin/bash"; psi.Arguments = "-lc \"" + command.Replace("\"", "\\\"") + "\""; }
            var output = new StringBuilder();
            try
            {
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                DataReceivedEventHandler h = (s, e) =>
                {
                    if (e.Data == null) return;
                    string line = Regex.Replace(e.Data, @"\x1B\[[0-9;]*[A-Za-z]", "");
                    lock (output) output.AppendLine(line);
                    if (!quiet) lock (log) log.AppendLine(line);
                };
                p.OutputDataReceived += h;
                p.ErrorDataReceived += h;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (quiet)
                {
                    // tiny commands: wait right here
                    p.WaitForExit(5000);
                    int code = p.HasExited ? p.ExitCode : -1;
                    done(code, output.ToString());
                    return;
                }
                proc = p;
                running = label;
                onExit = done;
                EditorApplication.update += WatchProcess;
                outputBuffer = output;
            }
            catch (Exception e)
            {
                if (!quiet) log.AppendLine(e.Message);
                done(-1, e.Message);
            }
        }

        StringBuilder outputBuffer;

        void WatchProcess()
        {
            if (proc == null) { EditorApplication.update -= WatchProcess; return; }
            if (!proc.HasExited) return;
            EditorApplication.update -= WatchProcess;
            proc.WaitForExit();
            int code = proc.ExitCode;
            string outp;
            lock (outputBuffer) outp = outputBuffer.ToString();
            proc.Dispose();
            proc = null;
            var cb = onExit;
            onExit = null;
            cb?.Invoke(code, outp);
            Repaint();
        }

        void KillProcess()
        {
            if (proc == null) return;
            try { if (!proc.HasExited) proc.Kill(); } catch { }
            proc = null;
            EditorApplication.update -= WatchProcess;
        }

        /* ---------------- connection test + moderation (HTTP) ---------------- */

        static string HttpBase(string u)
        {
            u = (u ?? "").Trim().TrimEnd('/');
            if (u.EndsWith("/ws")) u = u.Substring(0, u.Length - 3);
            if (u.StartsWith("wss://")) u = "https://" + u.Substring(6);
            else if (u.StartsWith("ws://")) u = "http://" + u.Substring(5);
            else if (!u.StartsWith("http")) u = "https://" + u;
            return u;
        }

        void TestConnection()
        {
            if (!Online) { SetStatus("No server address yet.", false); return; }
            Request(UnityWebRequest.Get(HttpBase(url) + "/"), r =>
            {
                if (r.result == UnityWebRequest.Result.Success && r.downloadHandler.text.Contains("Flopzone"))
                    SetStatus("Connected! Multiplayer is on. Build for Quest (APK) again so the headset gets it.", true);
                else SetStatus("Can't reach the server yet (" + (r.error ?? "no reply") + "). A brand-new server can take a minute; press Test connection again.", false);
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

        void ModGUI()
        {
            EditorGUILayout.HelpBox("In the game: add your player ID (Menu > Profile) to ADMIN_IDS in Server/wrangler.toml and press Turn On Server again. You'll get a MOD tab in the game menu.\n\nFrom here: run 'npx wrangler secret put ADMIN_KEY' in the Server folder once, then type the same key below.", MessageType.None);
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
            if (!Online) { modOutput = "Turn on the server first."; return; }
            if (string.IsNullOrEmpty(modKey)) { modOutput = "Type the admin key first."; return; }
            string u = HttpBase(url) + "/admin?key=" + UnityWebRequest.EscapeURL(modKey) + "&action=" + action +
                       "&target=" + UnityWebRequest.EscapeURL(modTarget ?? "") + "&minutes=" + modMinutes + "&reason=" + UnityWebRequest.EscapeURL(modReason ?? "");
            modOutput = "...";
            Request(UnityWebRequest.Get(u), r => modOutput = r.downloadHandler != null && !string.IsNullOrEmpty(r.downloadHandler.text) ? r.downloadHandler.text : r.error);
        }
    }
}
