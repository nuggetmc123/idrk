using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace KartVR.Setup
{
    /// <summary>
    /// Turns multiplayer on or off automatically.
    ///
    /// Without Photon Fusion 2 the game uses a built-in offline stand-in
    /// (Scripts/Network/OfflineFusion), so the project still compiles, the KartVR
    /// menu works and you can play solo. The moment Photon Fusion 2 shows up in
    /// the project this DELETES the stand-in, so the two can never clash. If Fusion
    /// is removed again, the stand-in is restored from a hidden backup
    /// (Setup/Editor/OfflineFusion~, which Unity ignores).
    ///
    /// It also spots a second copy of the game's scripts (e.g. "KartVR 1"), which
    /// would otherwise cause hundreds of "already defined" errors.
    ///
    /// This lives in its own assembly on purpose: it keeps working even while
    /// the main game scripts have compile errors.
    /// </summary>
    [InitializeOnLoad]
    static class FusionSwitch
    {
        const string ShimFolder = "Assets/KartVR/Scripts/Network/OfflineFusion";
        const string BackupFolder = "Assets/KartVR/Setup/Editor/OfflineFusion~";
        const string OldDefine = "KARTVR_FUSION";

        /// <summary>Set when Fusion first appears; the game's editor scripts finish setup after recompiling.</summary>
        public static string PendingKey => "KartVR.PendingFusionSetup." + Application.dataPath;
        static string DuplicateAskedKey => "KartVR.DuplicateAsked." + Application.dataPath;

        static FusionSwitch()
        {
            EditorApplication.delayCall += Check;
            // Belt and braces: if a compile still fails because the stand-in and real Fusion clash, fix it from here.
            CompilationPipeline.assemblyCompilationFinished -= OnCompiled;
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        }

        static void OnCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var m in messages)
            {
                if (m.type != CompilerMessageType.Error) continue;
                if (m.message.Contains("Fusion.Runtime") || m.message.Contains("already contains a definition"))
                {
                    EditorApplication.delayCall -= Check;
                    EditorApplication.delayCall += Check;
                    return;
                }
            }
        }

        /// <summary>The moment Photon Fusion's files arrive, remove the stand-in.</summary>
        class ImportWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var path in imported.Concat(deleted))
                {
                    var p = path.Replace('\\', '/');
                    if (p.IndexOf("Photon/Fusion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        p.EndsWith("Fusion.Runtime.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        EditorApplication.delayCall -= Check;
                        EditorApplication.delayCall += Check;
                        return;
                    }
                }
            }
        }

        [MenuItem("KartVR/Check Multiplayer (Photon Fusion)")]
        static void CheckFromMenu()
        {
            EditorPrefs.DeleteKey(DuplicateAskedKey);
            Check();
            EditorUtility.DisplayDialog("KartVR",
                HasFusion()
                    ? "Photon Fusion 2 is installed: multiplayer is ON.\n\nSet your App ID with KartVR > Set Up Multiplayer."
                    : "Photon Fusion 2 is not installed: the game runs OFFLINE (solo).\n\nImport Photon Fusion 2 from the Asset Store to turn multiplayer on.",
                "OK");
        }

        /// <summary>Photon Fusion 2 is in the project if its runtime DLL is anywhere under Assets (or Packages).</summary>
        static bool HasFusion()
        {
            foreach (var root in new[] { "Assets", "Packages" })
            {
                try
                {
                    if (Directory.Exists(root) &&
                        Directory.GetFiles(root, "Fusion.Runtime.dll", SearchOption.AllDirectories).Length > 0)
                        return true;
                }
                catch (Exception)
                {
                    // Unreadable folder; keep looking.
                }
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name == "Fusion.Runtime") return true;
            return false;
        }

        static void Check()
        {
            CheckDuplicates();
            bool fusion = HasFusion();
            bool shimThere = Directory.Exists(ShimFolder);

            if (fusion && shimThere)
            {
                Debug.Log("[KartVR] Photon Fusion 2 found: removing the offline stand-in and switching multiplayer ON...");
                EditorPrefs.SetBool(PendingKey, true); // KartVRAutoSetup applies the App ID + rebuilds the world after recompiling
                if (!AssetDatabase.DeleteAsset(ShimFolder))
                {
                    Directory.Delete(ShimFolder, true);
                    if (File.Exists(ShimFolder + ".meta")) File.Delete(ShimFolder + ".meta");
                    AssetDatabase.Refresh();
                }
            }
            else if (!fusion && !shimThere && Directory.Exists(BackupFolder))
            {
                Debug.Log("[KartVR] Photon Fusion 2 not found: restoring the offline stand-in (solo play).");
                Directory.CreateDirectory(ShimFolder);
                foreach (var file in Directory.GetFiles(BackupFolder, "*.cs"))
                    File.Copy(file, Path.Combine(ShimFolder, Path.GetFileName(file)), true);
                EditorPrefs.DeleteKey(PendingKey);
                AssetDatabase.Refresh();
            }

            RemoveOldDefine();
        }

        /// <summary>Older versions used a KARTVR_FUSION define; it isn't needed any more.</summary>
        static void RemoveOldDefine()
        {
            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android })
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToList();
                if (defines.Remove(OldDefine))
                    PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
        }

        /// <summary>
        /// A second copy of any game script (e.g. the project folder unzipped into itself, "KartVR 1", or a
        /// replacement script dropped somewhere else) defines those classes twice. Offer to delete the extras.
        /// </summary>
        static void CheckDuplicates()
        {
            const string Root = "Assets/KartVR/";
            string[] extras;
            try
            {
                var ours = new System.Collections.Generic.HashSet<string>(
                    Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories).Select(Path.GetFileName));
                extras = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                    .Select(p => p.Replace('\\', '/'))
                    .Where(p => !p.StartsWith(Root) && ours.Contains(Path.GetFileName(p)))
                    .Where(p => File.ReadAllText(p).Contains("namespace KartVR"))
                    .ToArray();
            }
            catch (Exception)
            {
                return;
            }
            if (extras.Length == 0) return;

            Debug.LogWarning("[KartVR] Found extra copies of KartVR scripts outside Assets/KartVR (they cause " +
                             "'already contains a definition' errors):\n" + string.Join("\n", extras));
            if (EditorPrefs.GetBool(DuplicateAskedKey, false)) return;
            EditorPrefs.SetBool(DuplicateAskedKey, true);

            string list = string.Join("\n", extras.Take(12)) + (extras.Length > 12 ? "\n... and " + (extras.Length - 12) + " more" : "");
            if (EditorUtility.DisplayDialog("KartVR: extra copies found",
                    "These KartVR scripts are in your project twice:\n\n" + list +
                    "\n\nTwo copies cause hundreds of errors. Delete the extra copies?\n\n(The real ones in Assets/KartVR are kept.)",
                    "Delete extra copies", "Keep"))
            {
                foreach (var file in extras)
                    if (!AssetDatabase.DeleteAsset(file)) File.Delete(file);
                AssetDatabase.Refresh();
            }
        }
    }
}
