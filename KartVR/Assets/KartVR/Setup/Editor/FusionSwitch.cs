using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
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

        static FusionSwitch()
        {
            EditorApplication.delayCall += Check;
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
            bool fusion = HasFusion();
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

            Debug.Log(fusion
                ? "[KartVR] Photon Fusion 2 found: multiplayer ON. Run KartVR > Build Prototype World again so the karts and players use real networking."
                : "[KartVR] Photon Fusion 2 not found: running OFFLINE (solo). Import Photon Fusion 2 to play online.");
            if (fusion)
                EditorUtility.DisplayDialog("KartVR",
                    "Photon Fusion 2 found: multiplayer is now ON.\n\n" +
                    "1. Paste your App ID in Tools > Fusion > Fusion Hub.\n" +
                    "2. Run KartVR > Build Prototype World again (so the world uses real networking).",
                    "OK");
        }
    }
}
