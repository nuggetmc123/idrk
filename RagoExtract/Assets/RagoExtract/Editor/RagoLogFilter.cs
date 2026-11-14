using System;
using UnityEditor;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Editor only: hides Unity's own harmless Package Manager error "ScriptableSingleton already exists. Did you query
    /// the singleton in a constructor?" (a known Unity 6 bug in its ServicesContainer / PackageManagerProjectSettings).
    /// Only that exact message is dropped - every other log, warning and error still shows. Not in builds.
    /// </summary>
    [InitializeOnLoad]
    static class RagoLogFilter
    {
        static RagoLogFilter()
        {
            var cur = Debug.unityLogger.logHandler;
            if (cur is Filter) return;
            Debug.unityLogger.logHandler = new Filter(cur);
        }

        class Filter : ILogHandler
        {
            readonly ILogHandler _inner;
            public Filter(ILogHandler inner) { _inner = inner; }

            static bool Noise(string s) => s != null && s.Contains("ScriptableSingleton already exists");

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                if (logType == LogType.Error || logType == LogType.Assert)
                {
                    string msg = format;
                    try { if (args != null && args.Length > 0) msg = string.Format(format, args); } catch { }
                    if (Noise(msg)) return;
                }
                _inner.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                if (exception != null && Noise(exception.Message)) return;
                _inner.LogException(exception, context);
            }
        }
    }
}
