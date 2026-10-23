using System;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Opening the headset's system menu (the Meta button), taking the headset off and switching apps all take the
    /// game out of focus. While it is out of focus: all sound is paused, the mic stops sending, the controls read as
    /// untouched and your own bots wait. Online matches can't stop for one player, so your fighter just stands still.
    /// Everything picks up again when the game has focus. (Meta's store checks require this.)
    /// </summary>
    public static class AppFocus
    {
        /// <summary>True while the game is out of focus (system menu open, headset off, another app in front).</summary>
        public static bool Paused { get; private set; }
        /// <summary>Time (realtime) the game last lost or regained focus.</summary>
        public static float ChangedAt { get; private set; }
        /// <summary>Called with true when focus is lost and false when it comes back.</summary>
        public static event Action<bool> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (watcher != null) return;
            var go = new GameObject("Nullbound Focus");
            UnityEngine.Object.DontDestroyOnLoad(go);
            watcher = go.AddComponent<Watcher>();
        }

        static Watcher watcher;

        class Watcher : MonoBehaviour
        {
            void OnApplicationFocus(bool focus) => Set(!focus);
            void OnApplicationPause(bool pause) => Set(pause || !Application.isFocused);
        }

        static void Set(bool paused)
        {
            // In the editor, clicking another window shouldn't freeze the game you are testing.
            if (Application.isEditor) return;
            if (paused == Paused) return;
            Paused = paused;
            ChangedAt = Time.realtimeSinceStartup;
            AudioListener.pause = paused;
            ErrorLog.Note(paused ? "Lost focus (system menu / headset off)" : "Focus back");
            try { Changed?.Invoke(paused); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
