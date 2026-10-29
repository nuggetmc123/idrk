using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Flopzone.Setup
{
    /// <summary>
    /// Turns online play on or off by itself: when Photon Fusion 2 is in the
    /// project it adds the FLOPZONE_FUSION define (which switches on
    /// Scripts/Net/PhotonLink.cs); when Fusion isn't there it takes the define
    /// away again, so the game always compiles (offline with bots).
    ///
    /// It also finds second copies of Flopzone's scripts (an old copy left
    /// next to a new one) and offers to delete them.
    ///
    /// It lives in its own assembly on purpose, so it keeps working even while
    /// the game's own scripts have compile errors.
    /// </summary>
    [InitializeOnLoad]
    static class FusionSwitch
    {
        public const string Define = "FLOPZONE_FUSION";
        static string DuplicateAskedKey => "Flopzone.DuplicateAsked." + Application.dataPath;

        static FusionSwitch()
        {
            EditorApplication.delayCall += Check;
            CompilationPipeline.assemblyCompilationFinished -= OnCompiled;
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        }

        static void OnCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var m in messages)
                if (m.type == CompilerMessageType.Error && (m.message.Contains("Fusion") || m.message.Contains("already contains a definition") || m.message.Contains("already defines")))
                {
                    EditorApplication.delayCall -= Check;
                    EditorApplication.delayCall += Check;
                    return;
                }
        }

        class ImportWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var path in imported.Concat(deleted))
                {
                    var p = path.Replace('\\', '/');
                    if (p.IndexOf("Photon/Fusion", StringComparison.OrdinalIgnoreCase) >= 0 || p.EndsWith("Fusion.Runtime.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        EditorApplication.delayCall -= Check;
                        EditorApplication.delayCall += Check;
                        return;
                    }
                }
            }
        }

        public static bool HasFusion()
        {
            foreach (var root in new[] { "Assets", "Packages" })
            {
                try
                {
                    if (Directory.Exists(root) && Directory.GetFiles(root, "Fusion.Runtime.dll", SearchOption.AllDirectories).Length > 0) return true;
                }
                catch (Exception) { }
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name == "Fusion.Runtime") return true;
            return false;
        }

        static void Check()
        {
            CheckDuplicates();
            bool fusion = HasFusion();
            bool changed = false;
            foreach (var target in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var defines = Get(target).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToList();
                bool has = defines.Contains(Define);
                if (fusion && !has) defines.Add(Define);
                else if (!fusion && has) defines.Remove(Define);
                else continue;
                Set(target, string.Join(";", defines));
                changed = true;
            }
            if (changed)
                Debug.Log(fusion ? "[Flopzone] Photon Fusion 2 found: online play is ON." : "[Flopzone] Photon Fusion 2 not found: the game plays offline with bots.");
        }

        static string Get(BuildTargetGroup g)
        {
#if UNITY_2021_2_OR_NEWER
            return PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(g));
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(g);
#endif
        }

        static void Set(BuildTargetGroup g, string defines)
        {
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(g), defines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(g, defines);
#endif
        }

        /// <summary>Scripts from an old copy of Flopzone elsewhere in Assets cause "already defined" errors.</summary>
        static void CheckDuplicates()
        {
            const string Root = "Assets/Flopzone/";
            string[] extras;
            try
            {
                var ours = new System.Collections.Generic.HashSet<string>(Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories).Select(Path.GetFileName));
                extras = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                    .Select(p => p.Replace('\\', '/'))
                    .Where(p => !p.StartsWith(Root) && ours.Contains(Path.GetFileName(p)))
                    .Where(p => File.ReadAllText(p).Contains("namespace Flopzone"))
                    .ToArray();
            }
            catch (Exception) { return; }
            if (extras.Length == 0) return;
            Debug.LogWarning("[Flopzone] Extra copies of Flopzone scripts outside Assets/Flopzone:\n" + string.Join("\n", extras));
            if (EditorPrefs.GetBool(DuplicateAskedKey, false)) return;
            EditorPrefs.SetBool(DuplicateAskedKey, true);
            string list = string.Join("\n", extras.Take(12)) + (extras.Length > 12 ? "\n... and " + (extras.Length - 12) + " more" : "");
            if (EditorUtility.DisplayDialog("Flopzone: extra copies found",
                    "These Flopzone scripts are in your project twice:\n\n" + list + "\n\nTwo copies cause lots of errors. Delete the extra copies?\n\n(The real ones in Assets/Flopzone are kept.)",
                    "Delete extra copies", "Keep"))
            {
                foreach (var file in extras) if (!AssetDatabase.DeleteAsset(file)) File.Delete(file);
                AssetDatabase.Refresh();
            }
        }
    }
}
