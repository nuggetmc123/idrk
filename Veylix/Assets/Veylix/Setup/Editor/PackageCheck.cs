using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Veylix.Setup
{
    /// When Veylix is imported into a fresh project (Assets > Import Package), the packages it needs may be
    /// missing and the game scripts can't compile yet. This lives in its own assembly so it still runs then,
    /// and adds the Input System package (which also brings the rest of the game back to life).
    [InitializeOnLoad]
    static class PackageCheck
    {
        const string InputSystem = "com.unity.inputsystem";
        static AddRequest request;

        static PackageCheck()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            string manifest = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json");
            if (!File.Exists(manifest) || File.ReadAllText(manifest).Contains("\"" + InputSystem + "\"")) return;
            Debug.Log("Veylix: adding the Input System package...");
            request = Client.Add(InputSystem + "@1.11.2");
            EditorApplication.update += Wait;
        }

        static void Wait()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= Wait;
            if (request.Status == StatusCode.Success) Debug.Log("Veylix: Input System added.");
            else Debug.LogError("Veylix: couldn't add the Input System package (" + request.Error?.message + "). Add it in Window > Package Manager.");
            request = null;
        }
    }
}
