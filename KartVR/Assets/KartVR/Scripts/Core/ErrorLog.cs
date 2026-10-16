using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Remembers the first errors the game hits, so the hub's info board can show them on the headset
    /// (where there's no Console to look at). Send a photo of the board and the problem can be found.
    /// </summary>
    public static class ErrorLog
    {
        public static string First { get; private set; } = "";
        static int count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (count++ >= 2) return; // the first ones are what matter
            string line = (message ?? "").Split('\n')[0];
            if (line.Length > 140) line = line.Substring(0, 140) + "...";
            if (type == LogType.Exception && !string.IsNullOrEmpty(stack)) line += " @ " + stack.Split('\n')[0];
            First = string.IsNullOrEmpty(First) ? line : First + "\n" + line;
        }
    }
}
