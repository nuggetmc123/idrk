using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rago.EditorTools
{
    /// <summary>
    /// Imports the generated world (bunker, market or a raid map) into the open scene so you can see and fly
    /// around it in the Scene view, Boosted-VR style. The game itself still generates the world when you press
    /// Play (so loot, NPCs and multiplayer stay in sync) - the imported copy is replaced automatically at runtime.
    /// </summary>
    [InitializeOnLoad]
    public static class RagoWorldImport
    {
        const string MapKey = "RagoExtract.ImportMap", AutoKey = "RagoExtract.AutoImport";
        public static readonly string[] MapIds = { "bunker", "market", "dockyard", "pinewood", "oldtown" };
        public static readonly string[] MapNames = { "Bunker", "Market", "Dockyard", "Pinewood Outpost", "Old Town" };
        public static string LastMessage = "";

        public static string CurrentMap { get => EditorPrefs.GetString(MapKey, "bunker"); set => EditorPrefs.SetString(MapKey, value); }
        public static bool AutoImport { get => EditorPrefs.GetBool(AutoKey, true); set => EditorPrefs.SetBool(AutoKey, value); }

        static RagoWorldImport()
        {
            EditorSceneManager.sceneOpened += (s, m) => Schedule();
            EditorApplication.playModeStateChanged += st =>
            {
                // the preview is only for looking at in the editor: remove it before Play (the game builds the real world), put it back after
                if (st == PlayModeStateChange.ExitingEditMode) Clear();
                if (st == PlayModeStateChange.EnteredEditMode) Schedule();
            };
            Schedule();
        }

        static void Schedule()
        {
            EditorApplication.delayCall += () =>
            {
                if (!AutoImport || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.path != RagoSetup.ScenePath) return;
                if (Find() != null) return;
                Import(CurrentMap, false);
            };
        }

        public static GameObject Find()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            foreach (var go in scene.GetRootGameObjects()) if (go != null && go.name == RagoGame.PreviewName) return go;
            return null;
        }

        public static void Clear()
        {
            GameObject g;
            int guard = 0;
            while ((g = Find()) != null && guard++ < 10) Object.DestroyImmediate(g);
        }

        public static bool Import(string mapId, bool frameView = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            try
            {
                EditorUtility.DisplayProgressBar("Rago Extract", "Importing " + Name(mapId) + " into the scene...", 0.3f);
                Clear();
                CurrentMap = mapId;
                if (Profile.Current == null) Profile.LoadOrCreate();
                Db.Init();
                Mats.Init();
                var root = new GameObject(RagoGame.PreviewName);
                var map = Maps.Create(mapId, root.transform, 4242);
                EditorUtility.DisplayProgressBar("Rago Extract", "Placing dummies...", 0.8f);
                // your player dummy at the first spawn
                var sp = map.PlayerSpawns.Count > 0 ? map.PlayerSpawns[0] : new Pose(Vector3.zero, Quaternion.identity);
                float yaw = sp.rotation.eulerAngles.y;
                PoseDummy(root.transform, Outfit.Operator, Profile.Current.outfitSeed, sp.position + sp.rotation * Vector3.forward * 2.5f, yaw + 180f, "Player");
                // raider squads on raid maps
                if (map.IsRaid)
                {
                    Physics.SyncTransforms();
                    var rng = new Rng(9);
                    int seed = 1;
                    foreach (var s in map.NpcSpawns)
                        for (int i = 0; i < s.count; i++)
                        {
                            var off = rng.InCircle() * 2.5f;
                            var p = s.pos + new Vector3(off.x, 0, off.y);
                            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f)) p = hit.point;
                            PoseDummy(root.transform, s.heavy ? Outfit.RaiderHeavy : Outfit.Raider, seed++, p, rng.Range(0f, 360f), "Raider");
                        }
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
                if (frameView && SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.LookAt(sp.position + Vector3.up * 1.6f, sp.rotation * Quaternion.Euler(12, 0, 0), map.IsRaid ? 18f : 7f);
                LastMessage = Name(mapId) + " imported into the scene. Press Play to play it.";
                Debug.Log("[Rago Extract] " + LastMessage);
                return true;
            }
            catch (System.Exception e)
            {
                LastMessage = "World import failed: " + e.Message;
                Debug.LogError("[Rago Extract] " + LastMessage + "\n" + e);
                return false;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void PoseDummy(Transform parent, Outfit o, int seed, Vector3 pos, float yaw, string name)
        {
            var rig = HumanRig.Build(o, seed, parent);
            rig.root.name = name + " (dummy)";
            if (o == Outfit.RaiderHeavy) ArmorBuilder.Dress(rig, Db.Get<ArmorDef>("helm_ballistic"), Db.Get<ArmorDef>("vest_carrier"), null, false);
            if (o == Outfit.Operator) ArmorBuilder.Dress(rig, null, null, Db.Get<BackpackDef>(string.IsNullOrEmpty(Profile.Current.loadout.packId) ? "pack_day" : Profile.Current.loadout.packId), false);
            var head = pos + Vector3.up * 1.62f;
            for (int i = 0; i < 3; i++) rig.Pose(pos, yaw, head, Quaternion.Euler(4, yaw, 0), Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.identity, Vector3.zero, 0.016f, false);
        }

        public static string Name(string id) { int i = System.Array.IndexOf(MapIds, id); return i >= 0 ? MapNames[i] : id; }
    }
}
