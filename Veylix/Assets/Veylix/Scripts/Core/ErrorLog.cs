using System.IO;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Writes every error and exception the game hits to a text file, so a problem seen on the headset (or in a long
    /// Console) can be sent over in one go: Veylix-errors.txt next to the project's Assets folder in the editor, and
    /// in the app's data folder on a device. Repeats of the same message are counted, not written again, so an error
    /// thrown every frame doesn't flood the disk.
    /// </summary>
    public static class ErrorLog
    {
        static string path;
        static readonly System.Collections.Generic.Dictionary<string, int> seen = new System.Collections.Generic.Dictionary<string, int>();
        public static int Count { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            seen.Clear();
            Count = 0;
            path = Application.isEditor ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Veylix-errors.txt")
                                        : Path.Combine(Application.persistentDataPath, "Veylix-errors.txt");
            try { File.WriteAllText(path, "Veylix " + Application.version + "  -  Unity " + Application.unityVersion + "  -  " + System.DateTime.Now + "\n\n"); }
            catch { path = null; }
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Count++;
            string key = message.Length > 300 ? message.Substring(0, 300) : message;
            seen.TryGetValue(key, out int n);
            seen[key] = n + 1;
            if (n > 0 || path == null) return;
            try { File.AppendAllText(path, "[" + type + "] " + message + "\n" + stack + "\n"); }
            catch { path = null; }
        }
    }
}
