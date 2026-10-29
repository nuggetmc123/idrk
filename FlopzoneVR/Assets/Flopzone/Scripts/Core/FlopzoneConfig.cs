using UnityEngine;

namespace Flopzone
{
    /// <summary>Online settings, edited in the Flopzone editor tab (Resources/FlopzoneConfig).</summary>
    public class FlopzoneConfig : ScriptableObject
    {
        [Tooltip("wss://flopzone-server.<you>.workers.dev — leave blank to play offline with bots.")]
        public string serverUrl = "";

        static FlopzoneConfig instance;

        public static FlopzoneConfig I
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<FlopzoneConfig>("FlopzoneConfig");
                    if (instance == null) instance = CreateInstance<FlopzoneConfig>();
                }
                return instance;
            }
        }

        /// <summary>The WebSocket URL to connect to, or "" for offline.</summary>
        public string SocketUrl
        {
            get
            {
                string u = (serverUrl ?? "").Trim().TrimEnd('/');
                if (u.Length == 0) return "";
                if (u.StartsWith("https://")) u = "wss://" + u.Substring(8);
                else if (u.StartsWith("http://")) u = "ws://" + u.Substring(7);
                else if (!u.StartsWith("ws")) u = "wss://" + u;
                if (!u.EndsWith("/ws")) u += "/ws";
                return u;
            }
        }
    }
}
