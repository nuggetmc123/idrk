using System;
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
    /// Without Photon Fusion 2 the game uses a built-in offline stand-in, so the
    /// project still compiles, the KartVR menu works and you can play solo.
    /// When this sees Photon Fusion 2 in the project it adds the KARTVR_FUSION
    /// define, which switches the stand-in off and real multiplayer on (and the
    /// other way round if Fusion is removed).
    ///
    /// This lives in its own assembly on purpose: it keeps working even while
    /// the main game scripts have compile errors (e.g. right after importing Fusion).
    /// </summary>
    [InitializeOnLoad]
    static class FusionSwitch
    {
        const string Define = "KARTVR_FUSION";

        /// <summary>Set when Fusion first appears; the game's editor scripts finish setup after recompiling.</summary>
        public static string PendingKey => "KartVR.PendingFusionSetup." + Application.dataPath;

        static FusionSwitch()
        {
            EditorApplication.delayCall += Check;
            // If importing Fusion still ends in "exists in both Assembly-CSharp and Fusion.Runtime" errors,
            // flip the switch from here: this runs even when the compile fails.
            CompilationPipeline.assemblyCompilationFinished -= OnCompiled;
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        }

        static void OnCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var m in messages)
            {
                if (m.type != CompilerMessageType.Error) continue;
                if (m.message.Contains("Fusion.Runtime") && m.message.Contains("Assembly-CSharp"))
                {
                    Debug.Log("[KartVR] Real Photon Fusion clashes with the offline stand-in; switching multiplayer ON.");
                    Apply(true);
                    return;
                }
            }
        }

        /// <summary>
        /// Watches imports: the moment Photon Fusion's files arrive (before Unity compiles them), switch the
        /// offline stand-in off so the two never clash.
        /// </summary>
        class ImportWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var path in imported)
                {
                    var p = path.Replace('\\', '/');
                    if (p.StartsWith("Assets/Photon/Fusion", StringComparison.OrdinalIgnoreCase) &&
                        (p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                    {
                        Apply(true);
                        return;
                    }
                }
            }
        }

        [MenuItem("KartVR/Check Multiplayer (Photon Fusion)")]
        static void CheckFromMenu()
        {
            Check();
            EditorUtility.DisplayDialog("KartVR",
                HasFusion()
                    ? "Photon Fusion 2 is installed: multiplayer is ON.\n\nMake sure your App ID is set in Tools > Fusion > Fusion Hub."
                    : "Photon Fusion 2 is not installed: the game runs OFFLINE (solo).\n\nImport Photon Fusion 2 from the Asset Store to turn multiplayer on.",
                "OK");
        }

        /// <summary>Real Fusion = a Fusion.NetworkRunner type in some assembly other than our own game scripts.</summary>
        static bool HasFusion()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name;
                if (name.StartsWith("Assembly-CSharp", StringComparison.Ordinal)) continue;
                try
                {
                    if (asm.GetType("Fusion.NetworkRunner", false) != null) return true;
                }
                catch (Exception)
                {
                    // Some assemblies can't be inspected; skip them.
                }
            }
            return false;
        }

        static void Check()
        {
            // Loaded Fusion assembly, or its files sitting in the project (in case they haven't compiled yet).
            Apply(HasFusion() || System.IO.Directory.Exists("Assets/Photon/Fusion"));
        }

        static void Apply(bool fusion)
        {
            bool changed = false;
            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android })
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToList();
                bool has = defines.Contains(Define);
                if (fusion && !has) { defines.Add(Define); changed = true; }
                else if (!fusion && has) { defines.Remove(Define); changed = true; }
                else continue;
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
            if (!changed) return;

            // After Unity recompiles, KartVRAutoSetup sees this and applies the App ID + rebuilds the world.
            if (fusion) EditorPrefs.SetBool(PendingKey, true);
            else EditorPrefs.DeleteKey(PendingKey);
            Debug.Log(fusion
                ? "[KartVR] Photon Fusion 2 found: switching multiplayer ON (finishing setup after recompiling)..."
                : "[KartVR] Photon Fusion 2 not found: running OFFLINE (solo). Use KartVR > Set Up Multiplayer to play online.");
        }
    }
}
