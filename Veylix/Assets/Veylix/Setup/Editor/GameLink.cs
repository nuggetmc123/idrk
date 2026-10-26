using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Veylix.EditorTools
{
    /// The setup tools (Import Quest, Make APK, Import Multiplayer) live in their own assembly so the Veylix menu is
    /// always there, even while the game scripts are still compiling or have an error. They reach the game's world
    /// builder through reflection instead of a direct reference.
    public static class GameLink
    {
        /// True when the real Photon Fusion is in the project (not the offline stand-in).
        public static bool FusionAvailable => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Fusion.Runtime");

        static Type Builder => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Veylix.EditorTools.GameBuilder", false)).FirstOrDefault(t => t != null);

        public static bool GameCompiled => Builder != null;

        public static bool WorldUpToDate
        {
            get
            {
                var p = Builder?.GetProperty("WorldUpToDate", BindingFlags.Public | BindingFlags.Static);
                return p != null && (bool)p.GetValue(null);
            }
        }

        public static void BuildWorld()
        {
            var m = Builder?.GetMethod("BuildWorld", BindingFlags.Public | BindingFlags.Static);
            if (m != null) { m.Invoke(null, null); return; }
            Debug.LogError("[Veylix] The game scripts haven't compiled, so the world can't be built yet. Check the Console for red errors.");
            EditorUtility.DisplayDialog("Veylix", "The game scripts haven't compiled yet, so the game world can't be built.\n\nOpen Window > General > Console and look for red errors.", "OK");
        }
    }
}
