using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Shows the hub in the Scene view while you're editing, so the project
    /// doesn't look empty. It's rebuilt each time the scene opens and never
    /// saved; when you press Play the game builds the real one.
    /// </summary>
    [ExecuteAlways]
    public class HubPreview : MonoBehaviour
    {
        GameObject preview;

        void OnEnable()
        {
            if (Application.isPlaying) return;
            Clear();
            try
            {
                var w = MapBuilder.Build(MapBuilder.HubIndex);
                preview = w.gameObject;
                preview.name = "Hub (editor preview)";
                preview.transform.SetParent(transform, false);
                foreach (var t in preview.GetComponentsInChildren<Transform>(true))
                    t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Sloppy] Couldn't show the hub preview: " + e.Message);
                Clear();
            }
        }

        void OnDisable() => Clear();

#if UNITY_EDITOR
        // Take the preview away before Play starts (the game builds its own hub), put it back after.
        [UnityEditor.InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                foreach (var p in Resources.FindObjectsOfTypeAll<HubPreview>())
                {
                    if (p == null || UnityEditor.EditorUtility.IsPersistent(p)) continue;
                    if (change == UnityEditor.PlayModeStateChange.ExitingEditMode) p.Clear();
                    else if (change == UnityEditor.PlayModeStateChange.EnteredEditMode && p.isActiveAndEnabled) p.OnEnable();
                }
            };
        }
#endif

        void Clear()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = null;
            // leftovers from an earlier load
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (c.name == "Hub (editor preview)") DestroyImmediate(c);
            }
        }
    }
}
