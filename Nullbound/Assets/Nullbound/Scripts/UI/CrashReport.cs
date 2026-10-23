using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nullbound
{
    /// <summary>
    /// If the game closed by itself last time (ErrorLog.PreviousCrashed), a board in the briefing room shows the last
    /// things it was doing and any errors, so a photo from the headset says where it broke. Gone once you start playing.
    /// </summary>
    public class CrashReport : MonoBehaviour
    {
        static bool shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (shown || !ErrorLog.PreviousCrashed || FindAnyObjectByType<CrashReport>() != null) return;
            if (FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0) return; // only the world scene
            var go = new GameObject("Crash Report");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<CrashReport>().Build();
        }

        void Build()
        {
            // To the left of the main menu, turned to face where you stand.
            Vector3 you = HalloweenPlan.MenuOrigin, at = you + new Vector3(-2.6f, 1.55f, 4.3f);
            transform.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(at.x - you.x, 0f, at.z - you.z)));
            NBUI.Box(transform, "Back", new Vector3(0f, 0f, 0.03f), new Vector3(1.7f, 1.15f, 0.03f), new Color(0.12f, 0.05f, 0.06f));
            NBUI.Box(transform, "Edge", new Vector3(0f, 0.585f, 0.02f), new Vector3(1.7f, 0.03f, 0.03f), new Color(1f, 0.3f, 0.2f));
            NBUI.Fit(NBUI.Label(transform, "<b>THE GAME CLOSED BY ITSELF LAST TIME</b>", new Vector3(0f, 0.48f, 0f), 0.62f, new Color(1f, 0.45f, 0.35f), new Vector2(1.6f, 0.1f)), 0.3f);
            var body = NBUI.Label(transform, ErrorLog.PreviousTail, new Vector3(0f, 0.02f, 0f), 0.3f, new Color(0.92f, 0.92f, 0.95f), new Vector2(1.58f, 0.78f), TextAlignmentOptions.TopLeft);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Truncate;
            NBUI.Label(transform, "Take a photo of this (Meta button > Camera > Photo) and send it to the developer.\nIt goes away when you start playing.",
                new Vector3(0f, -0.48f, 0f), 0.3f, new Color(1f, 0.85f, 0.4f), new Vector2(1.6f, 0.14f));
            shown = true;
        }

        void Update()
        {
            var s = SessionManager.Instance;
            if (s != null && !s.InMenu) Destroy(gameObject);
        }
    }
}
