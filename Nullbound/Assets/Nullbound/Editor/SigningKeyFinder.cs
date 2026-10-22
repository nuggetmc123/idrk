using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// Works out which key your first Meta upload was signed with ("The APK is signed with a different certificate"):
    /// finds the signing keys on this computer (the Android debug key, every Nullbound key, the one set in Player
    /// settings) and the APKs you've built (Downloads, Desktop, Documents, OneDrive and old project copies), reads
    /// which certificate signed each APK, and matches them up. Then it can switch builds to the key you need.
    /// </summary>
    public static class SigningKeyFinder
    {
        sealed class Key
        {
            public string name, path, alias, password, fingerprint;
        }

        sealed class Apk
        {
            public string path, fingerprint;
            public System.DateTime written;
        }

        [MenuItem("Nullbound/Signing/Find My First Upload's Key", priority = 39)]
        static void Find()
        {
            EditorUtility.DisplayProgressBar("Nullbound - Signing", "Looking for signing keys...", 0.1f);
            List<Key> keys;
            List<Apk> apks;
            try
            {
                keys = FindKeys();
                EditorUtility.DisplayProgressBar("Nullbound - Signing", "Looking for APKs you've built...", 0.4f);
                apks = FindApks();
            }
            finally { EditorUtility.ClearProgressBar(); }

            string KeyName(string fp) => fp == null ? "couldn't read" : keys.FirstOrDefault(k => k.fingerprint == fp)?.name ?? "an UNKNOWN key (" + Short(fp) + ")";
            var report = new StringBuilder();
            report.AppendLine("SIGNING KEYS ON THIS COMPUTER");
            foreach (var k in keys) report.AppendLine("- " + k.name + ": " + k.path + (k.fingerprint != null ? "  [" + Short(k.fingerprint) + "]" : "  (couldn't read: unknown password)"));
            report.AppendLine();
            report.AppendLine("APKs FOUND (oldest first) AND THE KEY THAT SIGNED EACH");
            foreach (var a in apks) report.AppendLine("- " + a.written.ToString("yyyy-MM-dd HH:mm") + "  " + a.path + "\n    signed with " + KeyName(a.fingerprint));
            if (apks.Count == 0) report.AppendLine("- none found");
            string reportFile = Path.Combine(QuestStoreReady.KeystoreFolder, "signing-report.txt");
            Directory.CreateDirectory(QuestStoreReady.KeystoreFolder);
            File.WriteAllText(reportFile, report.ToString());
            Debug.Log("[Nullbound] Signing report (also in " + reportFile + "):\n" + report);

            // The oldest APK signed by a key we have is most likely your first upload.
            var oldest = apks.FirstOrDefault(a => a.fingerprint != null);
            var match = oldest != null ? keys.FirstOrDefault(k => k.fingerprint == oldest.fingerprint && k.password != null) : null;
            string summary = "Found " + keys.Count + " key(s) and " + apks.Count + " APK(s). The full list is in the Console and in\n" + reportFile + "\n\n";
            if (oldest == null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Nullbound - Signing",
                    summary + "I couldn't find any APK you've built before. If you still have the APK you FIRST uploaded to Meta somewhere else " +
                    "(USB stick, another folder), pick it and I'll tell you which key signed it.", "Pick an APK...", "Close", "Open the report");
                if (choice == 0) PickApk(keys);
                if (choice == 2) EditorUtility.RevealInFinder(reportFile);
                return;
            }
            string what = "Your oldest APK (" + oldest.written.ToString("yyyy-MM-dd") + ", " + oldest.path + ") was signed with " + KeyName(oldest.fingerprint) + ".";
            if (match == null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Nullbound - Signing",
                    summary + what + "\n\nThat key isn't one I can use (it's not on this computer, or I don't know its password). If you made it yourself in " +
                    "Unity's Keystore Manager, set it in Player > Android > Publishing Settings and type its passwords. Otherwise see the README (Signing).",
                    "Pick a different APK...", "Close", "Open the report");
                if (choice == 0) PickApk(keys);
                if (choice == 2) EditorUtility.RevealInFinder(reportFile);
                return;
            }
            int c2 = EditorUtility.DisplayDialogComplex("Nullbound - Signing",
                summary + what + "\n\nIf that's the APK you first uploaded to Meta (or older than it), sign with that key and upload again.",
                "Sign with " + match.name, "Close", "Pick a different APK...");
            if (c2 == 0) Use(match);
            if (c2 == 2) PickApk(keys);
        }

        /// <summary>You point at the APK you first uploaded; this says which key signed it and offers to use it.</summary>
        static void PickApk(List<Key> keys)
        {
            string path = EditorUtility.OpenFilePanel("The APK you FIRST uploaded to Meta", System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "apk");
            if (string.IsNullOrEmpty(path)) return;
            string fp = ApkSigner(path);
            var key = fp != null ? keys.FirstOrDefault(k => k.fingerprint == fp) : null;
            if (fp == null)
                EditorUtility.DisplayDialog("Nullbound - Signing", "I couldn't read the signature of " + path + " (it may be signed the old v1 way only).", "OK");
            else if (key == null || key.password == null)
                EditorUtility.DisplayDialog("Nullbound - Signing", path + "\nwas signed with " + (key != null ? key.name + " (password unknown)" : "a key that isn't on this computer (" + Short(fp) + ")") +
                    ".\n\nIf you can't find that key, Meta won't accept updates from any other key: see the README (Signing) for making a new app.", "OK");
            else if (EditorUtility.DisplayDialog("Nullbound - Signing", path + "\nwas signed with " + key.name + ":\n" + key.path, "Sign builds with it", "Close"))
                Use(key);
        }

        static void Use(Key k)
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = k.path;
            PlayerSettings.Android.keyaliasName = k.alias;
            PlayerSettings.Android.keystorePass = k.password;
            PlayerSettings.Android.keyaliasPass = k.password;
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Nullbound - Signing", "Builds are now signed with " + k.name + ":\n" + k.path + "\n\nDo Nullbound > Build for Quest and upload the new APK. " +
                "Back this key up: every update has to use it.", "OK");
        }

        // ------------------------------------------------------------------ keys

        static List<Key> FindKeys()
        {
            var keys = new List<Key>();
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            void Add(string name, string path, string alias, string password)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                path = Path.GetFullPath(path);
                if (keys.Any(k => string.Equals(k.path, path, System.StringComparison.OrdinalIgnoreCase))) return;
                keys.Add(new Key { name = name, path = path, alias = alias, password = password, fingerprint = password != null ? KeyFingerprint(path, alias, password) : null });
            }
            Add("the Android debug key", Path.Combine(home, ".android", "debug.keystore"), "androiddebugkey", "android");
            // Every Nullbound key (the Documents folder can move, e.g. into OneDrive, which would have made a second one).
            foreach (var file in Search(SearchRoots(), f => Path.GetFileName(f).Equals("nullbound.keystore", System.StringComparison.OrdinalIgnoreCase) ||
                                                           Path.GetFileName(f).Equals("android-debug-key-BACKUP.keystore", System.StringComparison.OrdinalIgnoreCase)))
            {
                bool backup = Path.GetFileName(file).StartsWith("android-debug");
                string pass = backup ? "android" : PasswordNextTo(file);
                Add(backup ? "a backup of the Android debug key" : "the Nullbound key", file, backup ? "androiddebugkey" : "nullbound", pass);
            }
            // Whatever Player settings point at (a key you made yourself).
            if (PlayerSettings.Android.useCustomKeystore)
                Add("the key in Player settings", PlayerSettings.Android.keystoreName, PlayerSettings.Android.keyaliasName,
                    string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) ? null : PlayerSettings.Android.keystorePass);
            // Any other keystore files lying around (listed, but their passwords are unknown).
            foreach (var file in Search(SearchRoots(), f => f.EndsWith(".keystore", System.StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jks", System.StringComparison.OrdinalIgnoreCase)))
                Add("another keystore", file, null, null);
            // The same key can be in several files (backups): keep each file but they'll share a fingerprint.
            return keys;
        }

        static string PasswordNextTo(string keystore)
        {
            string file = Path.Combine(Path.GetDirectoryName(keystore) ?? "", "KEYSTORE-PASSWORD-keep-this-safe.txt");
            if (!File.Exists(file)) return null;
            foreach (var line in File.ReadAllLines(file))
                if (line.StartsWith("password")) return line.Substring(line.IndexOf(':') + 1).Trim();
            return null;
        }

        /// <summary>SHA-256 of a key's certificate, via keytool (exported as text, hashed here, so it works in any language).</summary>
        static string KeyFingerprint(string keystore, string alias, string password)
        {
            string keytool = QuestStoreReady.FindKeytool();
            if (keytool == null) return null;
            try
            {
                var p = Process.Start(new ProcessStartInfo(keytool, "-exportcert -rfc -keystore \"" + keystore + "\" -alias " + alias + " -storepass \"" + password + "\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
                string pem = p.StandardOutput.ReadToEnd();
                p.WaitForExit(20000);
                int a = pem.IndexOf("-----BEGIN CERTIFICATE-----"), b = pem.IndexOf("-----END CERTIFICATE-----");
                if (a < 0 || b < 0) return null;
                string body = pem.Substring(a + 27, b - a - 27).Replace("\r", "").Replace("\n", "").Trim();
                return Sha256(System.Convert.FromBase64String(body));
            }
            catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't read " + keystore + ": " + e.Message); return null; }
        }

        // ------------------------------------------------------------------ APKs

        static List<Apk> FindApks()
        {
            var list = new List<Apk>();
            foreach (var file in Search(SearchRoots(), f => f.EndsWith(".apk", System.StringComparison.OrdinalIgnoreCase)))
            {
                var info = new FileInfo(file);
                if (info.Length < 5 * 1024 * 1024) continue; // not a game build
                list.Add(new Apk { path = file, written = info.LastWriteTime, fingerprint = ApkSigner(file) });
            }
            return list.OrderBy(a => a.written).ToList();
        }

        /// <summary>
        /// SHA-256 of the first signer's certificate in an APK's v2/v3 signing block (what Meta compares). Null if the
        /// APK has no such block.
        /// </summary>
        static string ApkSigner(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var r = new BinaryReader(fs);
                // End of central directory: in the last 64 KB, signature 0x06054b50.
                long len = fs.Length;
                int tail = (int)System.Math.Min(len, 65557);
                fs.Position = len - tail;
                var end = r.ReadBytes(tail);
                int eocd = -1;
                for (int i = end.Length - 22; i >= 0; i--)
                    if (end[i] == 0x50 && end[i + 1] == 0x4b && end[i + 2] == 0x05 && end[i + 3] == 0x06) { eocd = i; break; }
                if (eocd < 0) return null;
                long cdOffset = System.BitConverter.ToUInt32(end, eocd + 16);
                // "APK Sig Block 42" sits right before the central directory, after the block's size.
                fs.Position = cdOffset - 24;
                long blockSize = r.ReadInt64();
                if (Encoding.ASCII.GetString(r.ReadBytes(16)) != "APK Sig Block 42") return null;
                fs.Position = cdOffset - blockSize - 8;
                var block = r.ReadBytes((int)(blockSize + 8));
                int pos = 8, stop = block.Length - 24;
                while (pos + 12 <= stop)
                {
                    long pairLen = System.BitConverter.ToInt64(block, pos);
                    uint id = System.BitConverter.ToUInt32(block, pos + 8);
                    int value = pos + 12;
                    if (id == 0x7109871a || id == 0xf05368c0 || id == 0x1b93ad61)
                    {
                        // signers -> first signer -> signed data -> (digests) -> certificates -> first certificate
                        int p = value;
                        var signers = Lp(block, ref p);
                        int q = signers.start;
                        var signer = Lp(block, ref q);
                        int s = signer.start;
                        var signedData = Lp(block, ref s);
                        int d = signedData.start;
                        Lp(block, ref d); // digests
                        var certs = Lp(block, ref d);
                        int c = certs.start;
                        var cert = Lp(block, ref c);
                        var der = new byte[cert.length];
                        System.Array.Copy(block, cert.start, der, 0, cert.length);
                        return Sha256(der);
                    }
                    pos += 8 + (int)pairLen;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Nullbound] Couldn't read the signature of " + path + ": " + e.Message); }
            return null;
        }

        /// <summary>A length-prefixed (uint32) piece of the signing block: returns where it starts and moves past it.</summary>
        static (int start, int length) Lp(byte[] b, ref int pos)
        {
            int n = (int)System.BitConverter.ToUInt32(b, pos);
            var piece = (pos + 4, n);
            pos += 4 + n;
            return piece;
        }

        // ------------------------------------------------------------------ helpers

        static IEnumerable<string> SearchRoots()
        {
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var roots = new List<string>
            {
                Path.Combine(home, "Downloads"), Path.Combine(home, "Desktop"), Path.Combine(home, "Documents"),
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), Path.Combine(home, "OneDrive"),
                Path.Combine(home, ".android"),
                // Old copies of the project usually sit next to this one.
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..")),
            };
            return roots.Where(Directory.Exists).Distinct(System.StringComparer.OrdinalIgnoreCase);
        }

        static readonly HashSet<string> SkipFolders = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "Library", "Temp", "Logs", "obj", "node_modules", ".git", "AppData", "PackageCache", "Packages", "Assets" };

        /// <summary>Files matching `want` under the roots, a few folders deep, skipping Unity caches and system folders.</summary>
        static IEnumerable<string> Search(IEnumerable<string> roots, System.Func<string, bool> want)
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<(string dir, int depth)>(roots.Select(r => (r, 0)));
            while (stack.Count > 0)
            {
                var (dir, depth) = stack.Pop();
                if (!seen.Add(dir)) continue;
                string[] files = System.Array.Empty<string>(), subs = System.Array.Empty<string>();
                try { files = Directory.GetFiles(dir); subs = depth < 6 ? Directory.GetDirectories(dir) : subs; } catch (System.Exception) { }
                foreach (var f in files) if (want(f)) yield return f;
                foreach (var s in subs) if (!SkipFolders.Contains(Path.GetFileName(s))) stack.Push((s, depth + 1));
            }
        }

        static string Sha256(byte[] data)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
        }

        static string Short(string fp) => fp.Length > 16 ? fp.Substring(0, 16).ToUpperInvariant() : fp;
    }
}
