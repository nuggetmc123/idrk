using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rago.EditorTools
{
    /// <summary>Photon Fusion 2 setup: App ID, importing the Fusion SDK, switching the multiplayer module on.</summary>
    public static class RagoMultiplayer
    {
        public const string AppIdPath = "Assets/RagoExtract/Resources/Rago/photon_appid.txt";
        public const string Define = "RAGO_FUSION";
        public const string SdkPage = "https://doc.photonengine.com/fusion/current/getting-started/sdk-download";
        public const string DashboardPage = "https://dashboard.photonengine.com";
        public static string ImportStatus = "";
        static double _watchUntil, _nextPoll;
        static string _seenPath; static long _seenSize = -1;

        public static bool FusionInstalled => FindType("Fusion.NetworkRunner") != null;
        public static bool ModuleCompiled => Type.GetType("Rago.Net.FusionBackend, RagoExtract.Fusion") != null;
        public static string AppId => File.Exists(AppIdPath) ? File.ReadAllText(AppIdPath).Trim() : "";
        public static bool IsOn => FusionInstalled && !string.IsNullOrEmpty(AppId) && DefineEverywhere && !RagoSetup.HasDefine("RAGO_DISABLE_NET") && ModuleCompiled;
        /// <summary>The on-switch is set for both Quest (Android) and PC builds.</summary>
        public static bool DefineEverywhere => RagoSetup.HasDefineIn(BuildTargetGroup.Android, Define) && RagoSetup.HasDefineIn(BuildTargetGroup.Standalone, Define);

        static Type FindType(string full)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(full); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Runs on every editor load: once Fusion is in the project and an App ID is saved, switch the module on.</summary>
        public static void AutoEnable()
        {
            if (FusionInstalled && !string.IsNullOrEmpty(AppId) && !RagoSetup.HasDefine(Define)) { RagoSetup.AddDefine(Define); ImportStatus = "Done! Photon Fusion 2 is installed."; }
            if (FusionInstalled && string.IsNullOrEmpty(ImportStatus)) ImportStatus = "Done! Photon Fusion 2 is installed.";
        }

        // ------------------------------------------------------------------------------------ App ID
        public static void SaveAppId(string id)
        {
            id = (id ?? "").Trim();
            Directory.CreateDirectory(Path.GetDirectoryName(AppIdPath));
            File.WriteAllText(AppIdPath, id);
            AssetDatabase.ImportAsset(AppIdPath);
            // also write it into Photon's own settings asset so Fusion's tools see it
            try
            {
                var t = FindType("Fusion.Photon.Realtime.PhotonAppSettings");
                var global = t?.GetProperty("Global", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as UnityEngine.Object;
                if (global != null)
                {
                    var appSettings = t.GetField("AppSettings")?.GetValue(global);
                    var f = appSettings?.GetType().GetField("AppIdFusion");
                    if (f != null) { f.SetValue(appSettings, id); EditorUtility.SetDirty(global); AssetDatabase.SaveAssets(); }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Rago Extract] couldn't write PhotonAppSettings (the game still uses your App ID): " + e.Message); }
        }

        // ------------------------------------------------------------------------------------ Fusion SDK import
        static string[] SearchDirs()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[] { Path.Combine(home, "Downloads"), Path.Combine(home, "Desktop"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }.Where(Directory.Exists).Distinct().ToArray();
        }

        static string FindSdkPackage()
        {
            string best = null; DateTime bestTime = DateTime.MinValue;
            foreach (var d in SearchDirs())
                foreach (var f in Directory.GetFiles(d, "*.unitypackage"))
                {
                    var n = Path.GetFileName(f).ToLowerInvariant();
                    if (!n.Contains("fusion")) continue;
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t > bestTime) { bestTime = t; best = f; }
                }
            return best;
        }

        /// <summary>Imports the Fusion 2 SDK. Uses a downloaded .unitypackage if there is one, otherwise opens the download page and imports it automatically as soon as it lands in Downloads.</summary>
        public static void ImportFusion()
        {
            if (FusionInstalled) { ImportStatus = "Done! Photon Fusion 2 is installed."; return; }
            var pkg = FindSdkPackage();
            if (pkg != null) { ImportFile(pkg); return; }
            Application.OpenURL(SdkPage);
            ImportStatus = "Download \"Fusion 2 SDK\" from the page that just opened. It will be imported automatically as soon as it is in your Downloads folder.";
            _watchUntil = EditorApplication.timeSinceStartup + 30 * 60;
            _seenPath = null; _seenSize = -1;
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        public static void ImportFile(string path)
        {
            ImportStatus = "Importing " + Path.GetFileName(path) + " ...";
            Debug.Log("[Rago Extract] " + ImportStatus);
            AssetDatabase.ImportPackage(path, false);
        }

        static void Watch()
        {
            if (EditorApplication.timeSinceStartup > _watchUntil || FusionInstalled) { EditorApplication.update -= Watch; return; }
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 2;
            var pkg = FindSdkPackage();
            if (pkg == null) return;
            long size = new FileInfo(pkg).Length;
            // wait until the browser has finished writing the file
            if (pkg != _seenPath || size != _seenSize || size == 0) { _seenPath = pkg; _seenSize = size; return; }
            EditorApplication.update -= Watch;
            ImportFile(pkg);
        }

        /// <summary>"Finish setup now": save the App ID, switch the multiplayer module on and rebuild the world.</summary>
        public static void Finish(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) { EditorUtility.DisplayDialog("Rago Extract", "Paste your Photon App ID first (step 2).", "OK"); return; }
            SaveAppId(appId);
            if (!FusionInstalled) { EditorUtility.DisplayDialog("Rago Extract", "App ID saved. Now import Photon Fusion 2 (step 3) - multiplayer switches on automatically when it's done.", "OK"); return; }
            RagoSetup.RemoveDefine("RAGO_DISABLE_NET");
            RagoSetup.AddDefine(Define);
            ImportStatus = "Done! Photon Fusion 2 is installed.";
            RagoSetup.LastMessage = "Multiplayer is ON. Unity is recompiling once.";
            if (RagoWorldImport.Find() != null || UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path == RagoSetup.ScenePath) RagoWorldImport.Import(RagoWorldImport.CurrentMap, false);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>The Multiplayer window (Photon Fusion 2 setup).</summary>
    public class RagoMultiplayerWindow : EditorWindow
    {
        string _appId;
        double _nextRefresh;

        public static void Open()
        {
            var w = GetWindow<RagoMultiplayerWindow>(true, "Rago Extract Multiplayer");
            w.minSize = new Vector2(460, 420);
            w._appId = RagoMultiplayer.AppId;
            w.Show();
        }

        void OnEnable() { if (_appId == null) _appId = RagoMultiplayer.AppId; }

        static string Tail(string s) => s.Length <= 4 ? s : s.Substring(s.Length - 4);

        static void Check(string label, bool ok)
        {
            var st = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            GUILayout.Label((ok ? "<color=#3fbf5a><b>[done]</b></color>  " : "<color=#d9822b><b>[ todo ]</b></color>  ") + label, st);
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup > _nextRefresh) { _nextRefresh = EditorApplication.timeSinceStartup + 1.0; Repaint(); }
        }

        void OnGUI()
        {
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            var head = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            var wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
            bool on = RagoMultiplayer.IsOn;
            bool fusion = RagoMultiplayer.FusionInstalled;

            GUILayout.Space(6);
            GUILayout.Label(on ? "Multiplayer is ON" : "Multiplayer is OFF", title);
            GUILayout.Label(on ? "Everything is set up. Press Play: the raid terminal in your bunker should say ONLINE. The Market, raid matchmaking (4-10 players) and voice chat are on."
                               : "Do the 3 steps below, then press Finish. Until then the game plays offline (solo raids with AI).", wrap);
            // checklist: exactly what is and isn't done
            GUILayout.Space(4);
            bool disabled = RagoSetup.HasDefine("RAGO_DISABLE_NET");
            Check("Photon Fusion 2 SDK imported", fusion);
            Check("App ID saved" + (string.IsNullOrEmpty(RagoMultiplayer.AppId) ? "" : "  (..." + Tail(RagoMultiplayer.AppId) + ")"), !string.IsNullOrEmpty(RagoMultiplayer.AppId));
            Check("Multiplayer switched on for Quest + PC (press Finish)", RagoMultiplayer.DefineEverywhere);
            Check(disabled ? "Multiplayer code: switched off after a compile error (press Finish to retry)" : "Multiplayer code compiled", !disabled && RagoMultiplayer.ModuleCompiled);
            if (EditorApplication.isCompiling) EditorGUILayout.HelpBox("Unity is compiling... the checklist updates when it's done.", MessageType.Info);
            GUILayout.Space(10);

            GUILayout.Label("1. Get your free Photon App ID", head);
            GUILayout.Label("Make an account, click \"Create a new app\", choose Fusion, then Fusion 2. Copy the App ID (a long code).", wrap);
            if (GUILayout.Button("Open Photon website")) Application.OpenURL(RagoMultiplayer.DashboardPage);
            GUILayout.Space(8);

            GUILayout.Label("2. Paste your App ID here", head);
            _appId = EditorGUILayout.TextField("App ID", _appId ?? "");
            GUILayout.Space(8);

            GUILayout.Label("3. Import Photon Fusion 2 (free)", head);
            string status = fusion ? "Done! Photon Fusion 2 is installed." : string.IsNullOrEmpty(RagoMultiplayer.ImportStatus) ? "Not imported yet." : RagoMultiplayer.ImportStatus;
            EditorGUILayout.HelpBox(status, fusion ? MessageType.Info : MessageType.None);
            if (!fusion)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import Photon Fusion 2")) RagoMultiplayer.ImportFusion();
                    if (GUILayout.Button("Pick .unitypackage...", GUILayout.Width(150)))
                    {
                        var p = EditorUtility.OpenFilePanel("Photon Fusion 2 SDK", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "unitypackage");
                        if (!string.IsNullOrEmpty(p)) RagoMultiplayer.ImportFile(p);
                    }
                }
            }
            if (RagoSetup.HasDefine("RAGO_DISABLE_NET"))
                EditorGUILayout.HelpBox("The multiplayer module didn't compile with this Fusion version and was switched off (the game still runs offline). Press Finish to try again; if it fails again, send me the red Console errors.", MessageType.Warning);
            GUILayout.Space(16);

            if (GUILayout.Button("Finish setup now (apply App ID + rebuild world)", GUILayout.Height(30))) RagoMultiplayer.Finish(_appId);
        }
    }
}
