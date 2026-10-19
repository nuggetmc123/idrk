using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Nullbound.Installer
{
    /// Lives in its own assembly so it still runs while the game scripts can't compile yet:
    /// when NULLBOUND is imported into a fresh Unity 6 project, this installs the packages the
    /// game needs (Netcode, Relay, OpenXR, XR Management, Input System). Unity then recompiles
    /// and the game's own setup takes over.
    [InitializeOnLoad]
    static class NBInstaller
    {
        static readonly string[] Needed =
        {
            "com.unity.netcode.gameobjects@2.9.2",
            "com.unity.services.relay@1.2.0",
            "com.unity.xr.management@4.5.1",
            "com.unity.xr.openxr@1.14.3",
            "com.unity.inputsystem@1.14.2",
        };

        static AddAndRemoveRequest s_Request;

        static NBInstaller()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Check;
        }

        [MenuItem("Nullbound/Install Required Packages", priority = 0)]
        static void InstallMenu()
        {
            SessionState.SetBool("nb_install_started", false);
            Check();
        }

        static void Check()
        {
            if (s_Request != null) return;
            var manifest = File.Exists("Packages/manifest.json") ? File.ReadAllText("Packages/manifest.json") : "";
            var missing = new List<string>();
            foreach (var p in Needed)
            {
                var name = p.Substring(0, p.IndexOf('@'));
                if (!manifest.Contains("\"" + name + "\"")) missing.Add(p);
            }
            if (missing.Count == 0) return;
            if (SessionState.GetBool("nb_install_started", false)) return;
            SessionState.SetBool("nb_install_started", true);

            Debug.Log("[Nullbound] Installing packages the game needs: " + string.Join(", ", missing) + " (this takes a few minutes)...");
            s_Request = Client.AddAndRemove(missing.ToArray(), null);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (s_Request == null || !s_Request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (s_Request.Status == StatusCode.Success)
            {
                Debug.Log("[Nullbound] Packages installed. Unity will recompile, then NULLBOUND sets itself up.");
                EditorUtility.DisplayDialog("NULLBOUND",
                    "The packages NULLBOUND needs are installed.\n\nIf Unity asks to enable the new Input System and restart, click YES.\n\nAfter that, use the 'Nullbound' menu at the top: Setup Everything, then press Play.",
                    "OK");
            }
            else
            {
                Debug.LogError("[Nullbound] Package install failed: " + (s_Request.Error != null ? s_Request.Error.message : "unknown error") +
                               "\nMake sure you're using Unity 6 (6000.x) and are online, then use Nullbound > Install Required Packages.");
            }
            s_Request = null;
        }
    }
}
