#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KingdomChaos.EditorTools
{
    // Tools > Kingdom Chaos > Create Game Scene: builds a ready-to-play scene so there is
    // nothing to wire up by hand. It makes a vertex-colour material (saved as an asset so it
    // is included in builds), a scene with a camera and the game object, saves the scene next
    // to this folder, and adds it to the top of Build Settings.
    public static class KingdomChaosMenu
    {
        [MenuItem("Tools/Kingdom Chaos/Create Game Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string dir = FolderOfThisPackage();
            string matPath = dir + "/KingdomChaosVertexColor.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                mat = new Material(shader) { name = "KingdomChaosVertexColor" };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("Kingdom Chaos");
            var game = go.AddComponent<KingdomChaosGame>();
            game.material = mat;

            string scenePath = dir + "/KingdomChaos.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = go;
            EditorUtility.DisplayDialog("Kingdom Chaos",
                "Scene created at " + scenePath + ".\n\nPress Play to start the game.", "OK");
        }

        // The folder the KingdomChaos package was imported into (Assets/KingdomChaos by default).
        static string FolderOfThisPackage()
        {
            foreach (var guid in AssetDatabase.FindAssets("KingdomChaosMenu t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);          // .../KingdomChaos/Editor/KingdomChaosMenu.cs
                string editorDir = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                string root = System.IO.Path.GetDirectoryName(editorDir)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root)) return root;
            }
            return "Assets";
        }
    }
}
#endif
