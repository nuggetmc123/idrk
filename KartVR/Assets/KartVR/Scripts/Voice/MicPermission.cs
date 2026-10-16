namespace KartVR
{
    /// <summary>
    /// Asks Quest/Android for the microphone (for voice chat). The game asks once at startup, before going
    /// online: the permission popup pauses the game, and asking in the middle of a session froze you in place.
    /// On PC and in the editor there's nothing to ask.
    /// </summary>
    public static class MicPermission
    {
        public static bool Asked { get; private set; }
        /// <summary>True while the popup is up and hasn't been answered.</summary>
        public static bool Waiting => waiting;
        static volatile bool waiting;

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
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => waiting = false;
            callbacks.PermissionDenied += _ => waiting = false;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
#endif
        }
    }
}
