using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Menu: KartVR > Set Up Multiplayer. Walks you through the two things only you
    /// can do (make a free Photon account to get an App ID, and import Photon
    /// Fusion 2), then finishes everything else automatically:
    /// multiplayer switches on, your App ID is filled in, and the world is rebuilt
    /// with real networking.
    /// </summary>
    public class MultiplayerSetup : EditorWindow
    {
        const string AppIdFile = "ProjectSettings/KartVRPhotonAppId.txt";
        const string DashboardUrl = "https://dashboard.photonengine.com";
        const string AssetStoreUrl = "https://assetstore.unity.com/?q=photon%20fusion%202";

        string appId;

        [MenuItem("KartVR/Set Up Multiplayer", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<MultiplayerSetup>(true, "Kart VR Multiplayer", true);
            w.minSize = new Vector2(460, 470);
        }

        void OnEnable() { appId = SavedAppId; }

        public static string SavedAppId
        {
            get { return File.Exists(AppIdFile) ? File.ReadAllText(AppIdFile).Trim() : ""; }
            set { File.WriteAllText(AppIdFile, (value ?? "").Trim()); }
        }

        static bool FusionInstalled => SessionManager.MultiplayerAvailable;

        void OnGUI()
        {
            var big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            var wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };

            GUILayout.Space(8);
            GUILayout.Label(FusionInstalled ? "Multiplayer is ON" : "Multiplayer is OFF (playing solo)", big);
            GUILayout.Label(FusionInstalled
                ? "Photon Fusion 2 is installed. Friends can join your private lobby with its code."
                : "Do these 3 steps once. Everything else happens by itself.", wrap);
            GUILayout.Space(12);

            // Step 1
            GUILayout.Label("1. Get your free Photon App ID", EditorStyles.boldLabel);
            GUILayout.Label("Make an account, click \"Create a new app\", choose Fusion, then Fusion 2. Copy the App ID (a long code).", wrap);
            if (GUILayout.Button("Open Photon website")) Application.OpenURL(DashboardUrl);
            GUILayout.Space(10);

            // Step 2
            GUILayout.Label("2. Paste your App ID here", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            appId = EditorGUILayout.TextField("App ID", appId);
            if (EditorGUI.EndChangeCheck()) SavedAppId = appId;
            if (!string.IsNullOrEmpty(appId) && appId.Trim().Length < 20)
                EditorGUILayout.HelpBox("That looks too short. A Photon App ID looks like 1a2b3c4d-1234-5678-9abc-def012345678.", MessageType.Warning);
            GUILayout.Space(10);

            // Step 3
            GUILayout.Label("3. Import Photon Fusion 2 (free)", EditorStyles.boldLabel);
            if (FusionInstalled)
            {
                EditorGUILayout.HelpBox("Done! Photon Fusion 2 is installed.", MessageType.Info);
            }
            else
            {
                GUILayout.Label("Click the button, find \"Photon Fusion 2\", click \"Add to My Assets\", then \"Open in Unity\" and \"Import\" " +
                                "(or: Window > Package Manager > My Assets > Photon Fusion 2 > Import).", wrap);
                if (GUILayout.Button("Find Photon Fusion 2 in the Asset Store")) Application.OpenURL(AssetStoreUrl);
            }
            GUILayout.Space(14);

            if (FusionInstalled)
            {
                if (GUILayout.Button("Finish setup now (apply App ID + rebuild world)", GUILayout.Height(30)))
                    FinishSetup(true);
            }
            else
            {
                EditorGUILayout.HelpBox("When the import finishes, Unity recompiles and this project switches multiplayer on, " +
                                        "fills in your App ID and rebuilds the world automatically. You'll get a message when it's ready.",
                    MessageType.None);
            }
        }

        /// <summary>Called automatically after Photon Fusion is detected (and by the button above).</summary>
        public static void FinishSetup(bool interactive)
        {
            string id = SavedAppId;
            bool idApplied = !string.IsNullOrEmpty(id) && TryApplyAppId(id);
            KartWorldBuilder.BuildWorld();
            TryRebuildFusionPrefabTable();

            string msg = "Multiplayer is ready!\n\n";
            if (idApplied) msg += "Your Photon App ID is set.\n";
            else if (string.IsNullOrEmpty(id)) msg += "You still need an App ID: open KartVR > Set Up Multiplayer and paste it in.\n";
            else msg += "Couldn't fill in the App ID by itself. Paste it in Tools > Fusion > Fusion Hub.\n";
            msg += "\nPress Play. Create a private lobby at a kiosk in the hub and give friends the code.";
            if (interactive || !idApplied) EditorUtility.DisplayDialog("Kart VR", msg, "OK");
            else Debug.Log("[KartVR] " + msg.Replace("\n", " "));
        }

        /// <summary>The App ID that actually goes into a build: the one in Fusion's settings asset ("" if none / no Fusion).</summary>
        public static string FusionAppId
        {
            get
            {
                try
                {
                    Type settingsType = FindType("Fusion.Photon.Realtime.PhotonAppSettings");
                    object global = settingsType == null ? null : GetMember(settingsType, null, "Global");
                    object appSettings = global == null ? null : GetMember(settingsType, global, "AppSettings");
                    return appSettings == null ? "" : (GetMember(appSettings.GetType(), appSettings, "AppIdFusion") as string ?? "").Trim();
                }
                catch (Exception) { return ""; }
            }
        }

        /// <summary>Writes the App ID into Fusion's settings asset (PhotonAppSettings.Global.AppSettings.AppIdFusion).</summary>
        public static bool TryApplyAppId(string id)
        {
            try
            {
                Type settingsType = FindType("Fusion.Photon.Realtime.PhotonAppSettings");
                if (settingsType == null) return false;
                object global = GetMember(settingsType, null, "Global");
                if (global == null) return false;
                object appSettings = GetMember(settingsType, global, "AppSettings");
                if (appSettings == null) return false;
                if (!SetMember(appSettings.GetType(), appSettings, "AppIdFusion", id)) return false;
                if (global is UnityEngine.Object asset)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[KartVR] Photon App ID applied.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KartVR] Couldn't set the Photon App ID automatically: " + e.Message);
                return false;
            }
        }

        static void TryRebuildFusionPrefabTable()
        {
            // Fusion normally picks up new prefabs by itself; this just makes sure.
            foreach (var typeName in new[] { "Fusion.Editor.NetworkProjectConfigUtilities", "Fusion.Editor.NetworkPrefabSourceFactory" })
            {
                var t = FindType(typeName);
                var m = t?.GetMethod("RebuildPrefabTable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (m != null && m.GetParameters().Length == 0)
                {
                    try { m.Invoke(null, null); } catch (Exception) { }
                    return;
                }
            }
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch (Exception) { }
            }
            return null;
        }

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        static object GetMember(Type t, object target, string name)
        {
            var p = t.GetProperty(name, Any);
            if (p != null) return p.GetValue(target);
            var f = t.GetField(name, Any);
            return f?.GetValue(target);
        }

        static bool SetMember(Type t, object target, string name, object value)
        {
            var f = t.GetField(name, Any);
            if (f != null) { f.SetValue(target, value); return true; }
            var p = t.GetProperty(name, Any);
            if (p != null && p.CanWrite) { p.SetValue(target, value); return true; }
            return false;
        }
    }
}
