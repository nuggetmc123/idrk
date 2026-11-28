using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Catches every error or exception the game logs, shows it as a message in front of you (so it can be read in the
    /// headset), keeps the last few for the menu's SETTINGS tab, and writes them to rago_errors.txt in the game's
    /// save folder - so they can be sent in and fixed.
    /// </summary>
    public static class ErrorLog
    {
        public static readonly List<string> Recent = new List<string>();
        static float _nextToast;
        static bool _hooked;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "rago_errors.txt");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            Recent.Clear();
            if (_hooked) return;
            _hooked = true;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string first = msg.Split('\n')[0];
            if (first.Length > 160) first = first.Substring(0, 160);
            string where = "";
            if (!string.IsNullOrEmpty(stack)) { var lines = stack.Split('\n'); if (lines.Length > 0) where = lines[0].Trim(); }
            string entry = first + (where.Length > 0 ? "  @ " + where : "");
            if (Recent.Count > 0 && Recent[Recent.Count - 1] == entry) return;
            Recent.Add(entry);
            while (Recent.Count > 6) Recent.RemoveAt(0);
            try { File.AppendAllText(FilePath, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + type + ": " + msg + "\n" + stack + "\n"); } catch { }
            if (Time.realtimeSinceStartup < _nextToast) return;
            _nextToast = Time.realtimeSinceStartup + 4f;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && pl.Hud != null) pl.Toast("Error: " + first + "  (menu > SETTINGS shows it)");
        }
    }
}
