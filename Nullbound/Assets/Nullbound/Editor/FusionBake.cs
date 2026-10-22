using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// Photon Fusion has to "bake" the networked objects in a scene (like the GameState) before the scene is saved,
    /// or they never start in a build ("the game state didn't start"). Fusion only does it on save and on Play, and
    /// it can miss a scene saved by a script, so the world builder and the Quest build do it themselves.
    /// Uses reflection so it compiles without Fusion.
    /// </summary>
    public static class FusionBake
    {
        /// <summary>Bakes the scene and saves it. False if Fusion isn't installed or baking failed.</summary>
        public static bool BakeAndSave(Scene scene)
        {
            Type post = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { post = post ?? asm.GetType("Fusion.Editor.NetworkObjectPostprocessor", false); }
                catch (Exception) { }
            }
            var bake = post?.GetMethod("BakeScene", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Scene) }, null);
            if (bake == null) return false;
            if (string.IsNullOrEmpty(scene.path))
            {
                // Networked objects get their IDs from the scene file, so a never-saved scene can't be baked.
                Debug.LogError("[Nullbound] Save the scene to a file before baking its network objects (it has never been saved).");
                return false;
            }
            try
            {
                bake.Invoke(null, new object[] { scene });
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Nullbound] Couldn't bake the scene's network objects: " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>Opens (if needed), bakes and saves the scene at this path.</summary>
        public static bool BakeAndSave(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            return BakeAndSave(scene);
        }
    }
}
