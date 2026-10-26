using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Veylix.EditorTools
{
    /// <summary>
    /// Veylix > Preview a Track. The game builds each map from code when you pick it, so the tracks aren't sitting in
    /// the project as scenes. This window builds any of the 25 maps right in the editor (same code the game runs),
    /// into a separate preview scene, so you can fly around it in the Scene view. Press Play from the main Veylix
    /// scene to actually drive it.
    /// </summary>
    public class TrackPreview : EditorWindow
    {
        Vector2 scroll;

        [MenuItem("Veylix/Preview a Track", priority = 21)]
        static void Open()
        {
            var w = GetWindow<TrackPreview>(false, "Veylix Tracks");
            w.minSize = new Vector2(360f, 420f);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Pick a map to build it in the Scene view (takes 10-60 seconds). This opens a separate preview scene; " +
                                    "to drive, open Assets/Veylix/Scenes/Veylix.unity (or click the button below) and press Play.", MessageType.Info);
            if (GUILayout.Button("Back to the game scene", GUILayout.Height(24))) BackToGame();
            EditorGUILayout.Space(6);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            bool header = false, headerFree = false;
            for (int i = 0; i < MapCatalog.Count; i++)
            {
                var m = MapCatalog.Get(i);
                if (!m.freeRoam && !header) { header = true; EditorGUILayout.LabelField("CIRCUITS", EditorStyles.boldLabel); }
                if (m.freeRoam && !headerFree) { headerFree = true; EditorGUILayout.Space(6); EditorGUILayout.LabelField("FREE ROAM", EditorStyles.boldLabel); }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button((i + 1) + ".  " + m.name, GUILayout.Height(22))) { int idx = i; EditorApplication.delayCall += () => Build(idx); }
                GUILayout.Label(m.location, EditorStyles.miniLabel, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        static void BackToGame()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(GameBuilder.ScenePath)) EditorSceneManager.OpenScene(GameBuilder.ScenePath);
        }

        public static void Build(int index)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Veylix", "Stop Play mode first.", "OK"); return; }
            if (!File.Exists(GameBuilder.ScenePath)) { EditorUtility.DisplayDialog("Veylix", "Run Veylix > Build Game World first.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var map = MapCatalog.Get(index);
            try
            {
                EditorUtility.DisplayProgressBar("Veylix", "Opening " + map.name + "...", 0f);
                // Borrow the game's materials and textures from the main scene.
                var game = EditorSceneManager.OpenScene(GameBuilder.ScenePath, OpenSceneMode.Single);
                VXAssets source = null;
                foreach (var root in game.GetRootGameObjects()) { source = root.GetComponentInChildren<VXAssets>(true); if (source != null) break; }
                var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager_SetActive(preview);
                var assetsGo = new GameObject("Assets (preview)");
                var assets = assetsGo.AddComponent<VXAssets>();
                if (source != null) EditorUtility.CopySerialized(source, assets);
                EditorSceneManager.CloseScene(game, true);
                VXAssets.UseInEditor(assets);

                // Daylight.
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
                sun.intensity = 1.2f;
                sun.color = new Color(1f, 0.95f, 0.88f);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
                if (assets.skybox != null) RenderSettings.skybox = assets.skybox;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.fog = false;

                var world = new GameObject("World").AddComponent<World>();
                world.UseAsCurrent();
                Run(world.Build(map), world, map.name);

                var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
                if (world.grid.Count > 0)
                {
                    var g = world.grid[0];
                    view.LookAt(g.position + Vector3.up * 2f, Quaternion.LookRotation(g.rotation * Vector3.forward + Vector3.down * 0.25f), 45f);
                }
                else view.Frame(world.trackBounds, false);
                view.cameraSettings.farClip = 20000f;
                view.Repaint();
                Debug.Log("[Veylix] Built " + map.name + " for preview. Fly around in the Scene view (right mouse + WASD). This preview scene isn't saved.");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Veylix", "Couldn't build that map in the editor. The red error in the Console says why; send it over.", "OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void SceneManager_SetActive(UnityEngine.SceneManagement.Scene s) => UnityEngine.SceneManagement.SceneManager.SetActiveScene(s);

        /// Runs a game coroutine (and the coroutines it yields) to the end, outside Play mode.
        static void Run(IEnumerator routine, World world, string name)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            int steps = 0;
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) stack.Push(inner);
                else System.Threading.Thread.Sleep(1);
                if (++steps % 10 == 0) EditorUtility.DisplayProgressBar("Veylix", name + ": " + world.Stage, world.Progress);
            }
        }
    }
}
