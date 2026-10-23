using System;
using System.IO;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Remembers the first errors the game hits, so they can be shown on the headset
    /// (where there's no Console to look at). Send a photo of the board and the problem can be found.
    ///
    /// Also writes a small log file on the headset: errors with their stack traces, scene loads, focus changes and
    /// low-memory warnings. The previous run's log is kept next to it, so after a crash it is still there.
    /// Find them in Meta Quest Developer Hub (Device Manager > Files) under
    /// Android/data/(the app's package name)/files/, or with
    /// adb pull /sdcard/Android/data/(package)/files/nullbound-log-previous.txt
    /// </summary>
    public static class ErrorLog
    {
        public static string First { get; private set; } = "";
        static int count;

        const string FileName = "nullbound-log.txt", PreviousName = "nullbound-log-previous.txt";
        const int MaxFileLines = 400, MaxErrorsWritten = 60;
        static string path;
        static int linesWritten, errorsWritten;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            Application.lowMemory -= OnLowMemory;
            Application.lowMemory += OnLowMemory;
            OpenFile();
        }

        static void OpenFile()
        {
            try
            {
                string dir = Application.persistentDataPath;
                path = Path.Combine(dir, FileName);
                string previous = Path.Combine(dir, PreviousName);
                if (File.Exists(path)) File.Copy(path, previous, true);
                File.WriteAllText(path, "Nullbound " + Application.version + " started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    " on " + SystemInfo.deviceModel + " (" + SystemInfo.operatingSystem + ", " + SystemInfo.systemMemorySize + " MB)\n");
            }
            catch (Exception) { path = null; } // no log file is no reason to stop the game
        }

        /// <summary>A line in the log file (what the game was doing when something went wrong).</summary>
        public static void Note(string line) => Write(line);

        static void Write(string line)
        {
            if (path == null || linesWritten >= MaxFileLines) return;
            linesWritten++;
            try { File.AppendAllText(path, Time.realtimeSinceStartup.ToString("F1") + "s  " + line + "\n"); }
            catch (Exception) { path = null; }
        }

        static void OnLowMemory()
        {
            Write("LOW MEMORY: freeing unused assets");
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errorsWritten++ < MaxErrorsWritten)
                Write(type + ": " + message + (string.IsNullOrEmpty(stack) ? "" : "\n    " + stack.Trim().Replace("\n", "\n    ")));
            if (count++ >= 2) return; // the first ones are what matter
            string line = (message ?? "").Split('\n')[0];
            if (line.Length > 140) line = line.Substring(0, 140) + "...";
            if (type == LogType.Exception && !string.IsNullOrEmpty(stack)) line += " @ " + stack.Split('\n')[0];
            First = string.IsNullOrEmpty(First) ? line : First + "\n" + line;
        }
    }
}
