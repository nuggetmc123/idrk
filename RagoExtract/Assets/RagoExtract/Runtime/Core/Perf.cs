using System.Diagnostics;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Frame-rate numbers for tracking down slowdowns (shown on the watch next to NET, and in menu > RECENT):
    /// FPS, the worst frame in the last 5 s, how often the garbage collector ran (each run is a hitch on Quest), and how
    /// much of each frame went on other players (network messages, drawing their bodies).
    /// </summary>
    public class Perf : MonoBehaviour
    {
        static Perf _i;
        public static float Fps { get; private set; } = 72f;
        public static float WorstMs { get; private set; }
        public static int GcPerMin { get; private set; }
        public static float NetMs { get; private set; }      // per frame, handling other players' messages
        public static float RemoteMs { get; private set; }   // per frame, moving / posing other players' bodies
        public static int MsgsPerSec { get; private set; }

        static readonly Stopwatch _net = new Stopwatch(), _remote = new Stopwatch();
        static int _msgs;
        float _worst, _worstWindow, _gcT, _secT, _netAcc, _remAcc; int _gc0, _frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_i != null) return;
            var go = new GameObject("RagoPerf");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<Perf>();
        }

        public static void NetBegin() { _net.Start(); _msgs++; }
        public static void NetEnd() { _net.Stop(); }
        public static void RemoteBegin() { _remote.Start(); }
        public static void RemoteEnd() { _remote.Stop(); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) Fps = Mathf.Lerp(Fps, 1f / dt, 0.05f);
            _worst = Mathf.Max(_worst, dt * 1000f);
            _worstWindow += dt;
            if (_worstWindow >= 5f) { WorstMs = _worst; _worst = 0f; _worstWindow = 0f; }
            _frames++;
            _netAcc += (float)_net.Elapsed.TotalMilliseconds; _net.Reset();
            _remAcc += (float)_remote.Elapsed.TotalMilliseconds; _remote.Reset();
            _secT += dt;
            if (_secT >= 1f)
            {
                NetMs = _netAcc / Mathf.Max(1, _frames); RemoteMs = _remAcc / Mathf.Max(1, _frames);
                MsgsPerSec = Mathf.RoundToInt(_msgs / _secT);
                _netAcc = _remAcc = 0f; _frames = 0; _msgs = 0; _secT = 0f;
            }
            _gcT += dt;
            if (_gcT >= 10f)
            {
                int gc = System.GC.CollectionCount(0);
                GcPerMin = Mathf.RoundToInt((gc - _gc0) * 60f / _gcT);
                _gc0 = gc; _gcT = 0f;
            }
        }

        public static string Report =>
            "PERF  " + Mathf.RoundToInt(Fps) + " fps  worst " + Mathf.RoundToInt(WorstMs) + " ms (5 s)\n" +
            "GC " + GcPerMin + "/min   net " + NetMs.ToString("0.00") + " ms/frame (" + MsgsPerSec + " msgs/s)   players " + RemoteMs.ToString("0.00") + " ms/frame\n";
    }
}
