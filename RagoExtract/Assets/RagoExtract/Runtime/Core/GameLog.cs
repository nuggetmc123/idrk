using System.IO;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A log file on the device, for "it crashed" reports: every warning, error and exception (and the game's own
    /// "[Rago]" lines) goes to rago_log.txt; the one before is kept as rago_log_prev.txt. On the Quest it's at
    /// Android/data/com.rago.extract/files/ (Meta Quest Developer Hub > Device Manager > files, or SideQuest).
    /// </summary>
    public static class GameLog
    {
        static StreamWriter _w;
        static readonly object _lock = new object();
        public static string FilePath => Path.Combine(Application.persistentDataPath, "rago_log.txt");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Start()
        {
            try
            {
                string p = FilePath, prev = Path.Combine(Application.persistentDataPath, "rago_log_prev.txt");
                if (File.Exists(p)) File.Copy(p, prev, true);
                _w = new StreamWriter(p, false) { AutoFlush = true };
                _w.WriteLine("Rago Extract " + Application.version + " - " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + SystemInfo.deviceModel + " - " + SystemInfo.operatingSystem);
                Application.logMessageReceivedThreaded += OnLog;
            }
            catch { }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Log && (msg == null || !msg.StartsWith("[Rago"))) return;   // only our own info lines
            lock (_lock)
            {
                try
                {
                    if (_w == null || _w.BaseStream.Length > 2_000_000) return;   // never let it grow past 2 MB
                    _w.WriteLine(System.DateTime.Now.ToString("HH:mm:ss") + " " + type + ": " + msg);
                    if (type == LogType.Exception || type == LogType.Error) _w.WriteLine(stack);
                }
                catch { }
            }
        }
    }
}
