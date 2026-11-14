using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Built-in proximity voice chat: your microphone is captured at 16 kHz, voice-activity gated, packed to 8 kHz u-law
    /// and sent over the game connection (Photon). Other players hear you from your head position in 3D.
    /// No extra SDK needed. Mute/unmute from the bunker Settings board.
    /// </summary>
    public class VoiceChat : MonoBehaviour, IVoiceBackend
    {
        const int MicRate = 16000, NetRate = 8000, FrameSamples = 960; // 60 ms at 16 kHz
        static VoiceChat _i;
        bool _muted, _joined, _started;
        string _device;
        AudioClip _mic;
        int _lastPos;
        readonly List<float> _pending = new List<float>(4096);
        float[] _read = new float[MicRate];
        float _hangover;
        string _status = "Voice ready";

        public bool Ready => true;
        public string Status => Microphone.devices.Length == 0 ? "No microphone found" : _status;
        public bool Muted => _muted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (_i != null) return;
            var go = new GameObject("RagoVoice");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<VoiceChat>();
            NetHub.Voice = _i;
        }

        public void Init(string displayName, System.Action<bool> done) { done?.Invoke(true); }
        public void Join(string channel, bool positional) { _joined = true; StartMic(); }
        public void Leave() { _joined = false; StopMic(); }
        public void SetMuted(bool muted) { _muted = muted; _status = muted ? "Voice muted" : "Voice on"; }
        public void UpdatePosition(Transform listener) { }
        public bool IsSpeaking(string participantName) => false;

        void StartMic()
        {
            if (_started) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                return; // try again next frame once allowed
            }
#endif
            if (Microphone.devices.Length == 0) return;
            _device = Microphone.devices[0];
            _mic = Microphone.Start(_device, true, 1, MicRate);
            _lastPos = 0;
            _started = _mic != null;
            _status = "Voice on";
        }

        void StopMic()
        {
            if (!_started) return;
            try { Microphone.End(_device); } catch { }
            _started = false;
            _pending.Clear();
        }

        void Update()
        {
            if (!_joined || !NetGame.Live) { if (_started) StopMic(); return; }
            if (!_started) { StartMic(); if (!_started) return; }
            int pos = Microphone.GetPosition(_device);
            if (pos < 0 || pos == _lastPos) return;
            int len = _mic.samples;
            int count = pos > _lastPos ? pos - _lastPos : len - _lastPos + pos;
            if (_read.Length < count) _read = new float[count];
            // read in up to two parts (ring buffer wrap)
            int first = Mathf.Min(count, len - _lastPos);
            var a = new float[first];
            _mic.GetData(a, _lastPos);
            _pending.AddRange(a);
            if (count > first) { var b = new float[count - first]; _mic.GetData(b, 0); _pending.AddRange(b); }
            _lastPos = pos;
            while (_pending.Count >= FrameSamples)
            {
                float rms = 0;
                for (int i = 0; i < FrameSamples; i++) rms += _pending[i] * _pending[i];
                rms = Mathf.Sqrt(rms / FrameSamples);
                if (rms > 0.02f) _hangover = 0.35f;   // talking (headset hiss and breathing don't open the mic)
                bool send = !_muted && _hangover > 0f;
                _hangover -= FrameSamples / (float)MicRate;
                if (send)
                {
                    var frame = new byte[FrameSamples / 2];
                    for (int i = 0; i < frame.Length; i++) frame[i] = MuLaw.Encode((_pending[i * 2] + _pending[i * 2 + 1]) * 0.5f * 1.6f);
                    NetGame.I.SendVoice(frame);
                }
                _pending.RemoveRange(0, FrameSamples);
            }
        }

        void OnDestroy() { StopMic(); }
    }

    /// <summary>Plays a remote player's voice from their head with 3D falloff.</summary>
    public class VoicePlayback : MonoBehaviour
    {
        const int Rate = 8000;
        readonly Queue<float> _buf = new Queue<float>(Rate);
        readonly object _lock = new object();
        AudioSource _src;

        public static VoicePlayback Create(Transform parent)
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VoicePlayback>();
            v._src = go.AddComponent<AudioSource>();
            v._src.spatialBlend = 1f;
            v._src.rolloffMode = AudioRolloffMode.Linear;
            v._src.minDistance = 2f;
            v._src.maxDistance = 30f;
            v._src.dopplerLevel = 0f;
            v._src.loop = true;
            v._src.clip = AudioClip.Create("voice", Rate * 2, 1, Rate, true, v.OnRead);
            v._src.Play();
            return v;
        }

        public void Push(byte[] frame)
        {
            lock (_lock)
            {
                if (_buf.Count > Rate / 2) _buf.Clear(); // more than 0.5 s behind: drop to stay in sync
                foreach (var b in frame) _buf.Enqueue(MuLaw.Decode(b));
            }
        }

        void OnRead(float[] data)
        {
            lock (_lock)
                for (int i = 0; i < data.Length; i++) data[i] = _buf.Count > 0 ? _buf.Dequeue() : 0f;
        }
    }

    /// <summary>G.711 u-law: 8-bit voice samples.</summary>
    public static class MuLaw
    {
        public static byte Encode(float f)
        {
            int s = (int)(Mathf.Clamp(f, -1f, 1f) * 32635f);
            int sign = (s >> 8) & 0x80;
            if (sign != 0) s = -s;
            s += 0x84;
            int exp = 7;
            for (int mask = 0x4000; (s & mask) == 0 && exp > 0; mask >>= 1) exp--;
            int mant = (s >> (exp + 3)) & 0x0F;
            return (byte)~(sign | (exp << 4) | mant);
        }

        public static float Decode(byte b)
        {
            int u = ~b;
            int sign = u & 0x80, exp = (u >> 4) & 0x07, mant = u & 0x0F;
            int s = ((mant << 3) + 0x84) << exp;
            s -= 0x84;
            return (sign != 0 ? -s : s) / 32768f;
        }
    }
}
