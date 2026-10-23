namespace Nullbound
{
    /// <summary>
    /// Asks Quest/Android for the microphone (for voice chat). The game asks once at startup, before going
    /// online: the permission popup pauses the game, and asking in the middle of a session froze you in place.
    /// On PC and in the editor there's nothing to ask.
    ///
    /// Android doesn't always answer (after "don't ask again" it can stay silent), so the question also counts as
    /// answered once the popup has closed and the game has had focus back for a moment.
    /// </summary>
    public static class MicPermission
    {
        public static bool Asked { get; private set; }
        /// <summary>True while the popup is up and hasn't been answered.</summary>
        public static bool Waiting
        {
            get
            {
                if (!waiting) return false;
                // The popup takes focus while it's open; focus back for a second means it closed.
                if (sawPopup && !AppFocus.Paused && UnityEngine.Time.realtimeSinceStartup - AppFocus.ChangedAt > 1f) waiting = false;
                return waiting;
            }
        }
        static volatile bool waiting;
        static bool sawPopup;

        public static bool Granted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
                return true;
#endif
            }
        }

        public static void Request()
        {
            if (Asked || Granted) return;
            Asked = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            waiting = true;
            sawPopup = false;
            AppFocus.Changed += OnFocus;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => waiting = false;
            callbacks.PermissionDenied += _ => waiting = false;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
#endif
        }

        static void OnFocus(bool paused)
        {
            if (paused) sawPopup = true;
            if (!waiting) AppFocus.Changed -= OnFocus;
        }
    }
}
