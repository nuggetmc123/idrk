using System.Diagnostics;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using Debug = UnityEngine.Debug;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// Makes Build for Quest (APK) produce an APK the Meta Horizon Store accepts AND that runs after downloading it:
    /// 1. SIGNING: the store refuses Unity's debug key, and every update must be signed with the same key. The first
    ///    build makes your own release key (Documents/Nullbound Keystore/nullbound.keystore, password saved next to it)
    ///    and every build after that, from any copy of the project, uses it. BACK UP THAT FOLDER: without it you can
    ///    never update the app on the store.
    /// 2. MANIFEST: makes sure the launcher activity is marked as a VR app (com.oculus.intent.category.VR, otherwise
    ///    the store copy opens as a flat window or not at all), that head tracking is declared, that the supported
    ///    headsets are listed, and that the install location is "auto".
    /// </summary>
    public static class QuestStoreReady
    {
        // In your Documents folder, NOT in the project: every new copy of the project (each zip I send) then reuses the
        // same key. A different key would stop the store from updating the app.
        static string KeystoreFolder => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Nullbound Keystore");
        static string KeystoreFile => Path.Combine(KeystoreFolder, "nullbound.keystore");
        static string PasswordFile => Path.Combine(KeystoreFolder, "KEYSTORE-PASSWORD-keep-this-safe.txt");
        const string Alias = "nullbound";

        /// <summary>Sets up release signing. Returns false (after telling you why) if the build should stop.</summary>
        public static bool EnsureSigning()
        {
            string ours = Path.GetFullPath(KeystoreFile);
            bool usingOurs = PlayerSettings.Android.useCustomKeystore && SamePath(PlayerSettings.Android.keystoreName, ours);

            // Someone set up their own keystore: keep it, but Unity forgets its passwords whenever it restarts.
            if (PlayerSettings.Android.useCustomKeystore && !usingOurs && File.Exists(PlayerSettings.Android.keystoreName))
            {
                if (!string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) && !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass)) return true;
                SettingsService.OpenProjectSettings("Project/Player");
                EditorUtility.DisplayDialog("Nullbound - Build for Quest",
                    "Your project is signed with your own keystore, but Unity has forgotten its passwords (it does that every restart).\n\n" +
                    "Player > Android (robot icon) > Publishing Settings: type the keystore password and the key password, then build again.", "OK");
                return false;
            }

            if (!File.Exists(ours))
            {
                string keytool = FindKeytool();
                if (keytool == null)
                {
                    EditorUtility.DisplayDialog("Nullbound - Build for Quest",
                        "I couldn't find Java's keytool (it comes with Unity's Android Build Support > OpenJDK module), so I can't make a " +
                        "store key automatically.\n\nMake one by hand: Project Settings > Player > Android > Publishing Settings > Keystore Manager > " +
                        "Create New, then build again.", "OK");
                    return false;
                }
                Directory.CreateDirectory(KeystoreFolder);
                string pass = NewPassword();
                string args = "-genkeypair -keystore \"" + ours + "\" -alias " + Alias + " -keyalg RSA -keysize 2048 -validity 10000 " +
                              "-storepass " + pass + " -keypass " + pass + " -dname \"CN=Nullbound, O=" + Clean(PlayerSettings.companyName) + ", C=US\"";
                var p = Process.Start(new ProcessStartInfo(keytool, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true });
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                if (!File.Exists(ours))
                {
                    EditorUtility.DisplayDialog("Nullbound - Build for Quest", "Making the store key failed:\n\n" + err, "OK");
                    return false;
                }
                File.WriteAllText(PasswordFile,
                    "Nullbound release keystore\n\nkeystore: " + ours + "\nalias: " + Alias + "\npassword (keystore and key): " + pass + "\n\n" +
                    "KEEP THIS FOLDER SAFE AND BACKED UP. Every update you upload to the Meta store must be signed with this exact key.\n" +
                    "If you lose it you can't update the app any more (you'd have to make a new app).\n");
                EditorUtility.DisplayDialog("Nullbound - Build for Quest",
                    "I made your app's store key:\n" + ours + "\n\nIts password is in " + Path.GetFullPath(PasswordFile) + ".\n\n" +
                    "BACK UP THAT KEYSTORE FOLDER (USB stick, cloud drive...). Every store update must use the same key.", "Got it");
            }

            string saved = ReadPassword();
            if (saved == null)
            {
                EditorUtility.DisplayDialog("Nullbound - Build for Quest", "The keystore password file is missing: " + Path.GetFullPath(PasswordFile), "OK");
                return false;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ours;
            PlayerSettings.Android.keystorePass = saved;
            PlayerSettings.Android.keyaliasName = Alias;
            PlayerSettings.Android.keyaliasPass = saved;
            return true;
        }

        /// <summary>APK (not an app bundle), one APK (not one per CPU), not an exported Android Studio project.</summary>
        public static void PlainApk()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            var split = typeof(PlayerSettings.Android).GetProperty("buildApkPerCpuArchitecture");
            if (split != null && split.CanWrite) split.SetValue(null, false);
            EditorUserBuildSettings.development = false;
        }

        static string ReadPassword()
        {
            if (!File.Exists(PasswordFile)) return null;
            foreach (var line in File.ReadAllLines(PasswordFile))
                if (line.StartsWith("password")) return line.Substring(line.IndexOf(':') + 1).Trim();
            return null;
        }

        static string FindKeytool()
        {
            string exe = System.Environment.OSVersion.Platform == System.PlatformID.Win32NT ? "keytool.exe" : "keytool";
            var candidates = new System.Collections.Generic.List<string>();
            // Unity's Android tools settings (if the Android module is installed).
            var type = System.Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            var jdk = type?.GetProperty("jdkRootPath")?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(jdk)) candidates.Add(Path.Combine(jdk, "bin", exe));
            candidates.Add(Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", exe));
            candidates.Add(Path.Combine(Path.GetDirectoryName(EditorApplication.applicationPath) ?? "", "Data", "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", exe));
            string home = System.Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(home)) candidates.Add(Path.Combine(home, "bin", exe));
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return null;
        }

        static string NewPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var bytes = new byte[20];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new System.Text.StringBuilder();
            foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }

        static string Clean(string s) => string.IsNullOrEmpty(s) ? "Nullbound" : System.Text.RegularExpressions.Regex.Replace(s, "[^A-Za-z0-9 ]", "");
        static bool SamePath(string a, string b) => !string.IsNullOrEmpty(a) && Path.GetFullPath(a).Replace('\\', '/').ToLowerInvariant() == b.Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>Fixes up the generated Android manifests so the Meta store treats (and launches) the APK as a VR app.</summary>
    public class QuestManifestFix : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 9999; // after OpenXR's own manifest changes

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // path is the unityLibrary module; the launcher module sits beside it.
            Fix(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
            Fix(Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"));
        }

        static void Fix(string file)
        {
            if (!File.Exists(file)) return;
            try
            {
                var doc = new XmlDocument();
                doc.Load(file);
                var manifest = doc.DocumentElement;
                if (manifest == null) return;
                bool changed = false;
                if (manifest.GetAttribute("installLocation", AndroidNs) != "auto") { manifest.SetAttribute("installLocation", AndroidNs, "auto"); changed = true; }

                var app = manifest["application"];
                bool hasLauncher = false;
                if (app != null)
                {
                    foreach (XmlNode activity in app.SelectNodes("activity"))
                        foreach (XmlNode filter in activity.SelectNodes("intent-filter"))
                        {
                            bool main = false, vr = false;
                            foreach (XmlNode n in filter.ChildNodes)
                            {
                                var name = (n as XmlElement)?.GetAttribute("name", AndroidNs);
                                if (n.Name == "action" && name == "android.intent.action.MAIN") main = true;
                                if (n.Name == "category" && name == "com.oculus.intent.category.VR") vr = true;
                            }
                            if (!main) continue;
                            hasLauncher = true;
                            if (!vr)
                            {
                                var cat = doc.CreateElement("category");
                                cat.SetAttribute("name", AndroidNs, "com.oculus.intent.category.VR");
                                filter.AppendChild(cat);
                                changed = true;
                            }
                        }
                    if (hasLauncher && !HasChild(app, "meta-data", "com.oculus.supportedDevices"))
                    {
                        var md = doc.CreateElement("meta-data");
                        md.SetAttribute("name", AndroidNs, "com.oculus.supportedDevices");
                        md.SetAttribute("value", AndroidNs, "quest2|questpro|quest3|quest3s");
                        app.AppendChild(md);
                        changed = true;
                    }
                    if (app.HasAttribute("debuggable", AndroidNs)) { app.RemoveAttribute("debuggable", AndroidNs); changed = true; }
                }
                if (hasLauncher && !HasChild(manifest, "uses-feature", "android.hardware.vr.headtracking"))
                {
                    var f = doc.CreateElement("uses-feature");
                    f.SetAttribute("name", AndroidNs, "android.hardware.vr.headtracking");
                    f.SetAttribute("version", AndroidNs, "1");
                    f.SetAttribute("required", AndroidNs, "true");
                    manifest.InsertBefore(f, app);
                    changed = true;
                }
                if (changed) { doc.Save(file); Debug.Log("[Nullbound] Made the Android manifest Quest-store ready: " + file); }
            }
            catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't check the Android manifest " + file + ": " + e.Message); }
        }

        static bool HasChild(XmlNode parent, string tag, string name)
        {
            foreach (XmlNode n in parent.ChildNodes)
                if (n.Name == tag && (n as XmlElement)?.GetAttribute("name", AndroidNs) == name) return true;
            return false;
        }
    }
}
