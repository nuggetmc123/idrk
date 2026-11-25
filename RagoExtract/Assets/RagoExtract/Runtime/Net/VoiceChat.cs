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
        const int MicRate = 16000, NetRate = 8000, FrameSamples = 480; // 30 ms at 16 kHz: ~125-byte frames that fit the fast lane (60 ms frames were too big and fell back to slow per-player sends)
        static VoiceChat _i;
        bool _muted, _joined, _started;
        string _device;
        AudioClip _mic;
        int _lastPos;
        readonly List<float> _pending = new List<float>(4096);
        float[] _read = new float[MicRate];
        float _hangover;
        string _status = "Voice ready";
        static bool _permAsked;

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
                // ask ONCE per launch. It used to ask again every frame until allowed: each ask puts the system's permission
                // prompt over the game, so on a fresh store install the whole view kept flickering black in the Market and raids
                if (_permAsked) return;
                _permAsked = true;
                var cb = new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionDenied += _ => { _status = "Microphone not allowed - voice off (allow it in the Quest's Settings > Apps > Permissions)"; };
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, cb);
                return; // starts by itself on a later frame once allowed
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
            ReadMic(first, _lastPos);
            if (count > first) ReadMic(count - first, 0);
            _lastPos = pos;
            while (_pending.Count >= FrameSamples)
            {
                float rms = 0;
                for (int i = 0; i < FrameSamples; i++) rms += _pending[i] * _pending[i];
                rms = Mathf.Sqrt(rms / FrameSamples);
                if (rms > 0.011f) _hangover = 0.45f;   // talking (headset hiss and breathing don't open the mic; quieter Quest mics never got over the old 0.02)
                bool send = !_muted && _hangover > 0f;
                _hangover -= FrameSamples / (float)MicRate;
                if (send)
                {
                    // 8 kHz, 4-bit ADPCM: half the size of the old u-law frames (voice was by far the biggest thing sent,
                    // and with 3+ players it all goes through one player's connection)
                    var pcm = new float[FrameSamples / 2];
                    for (int i = 0; i < pcm.Length; i++) pcm[i] = (_pending[i * 2] + _pending[i * 2 + 1]) * 0.5f * 1.6f;
                    NetGame.I.SendVoice(Adpcm.Encode(pcm));
                }
                _pending.RemoveRange(0, FrameSamples);
            }
        }

        // the mic is read into reused buffers (it used to make two new arrays every frame: garbage = hitches on Quest)
        readonly Dictionary<int, float[]> _bufs = new Dictionary<int, float[]>();
        void ReadMic(int n, int at)
        {
            if (n <= 0) return;
            if (!_bufs.TryGetValue(n, out var buf)) { if (_bufs.Count > 16) _bufs.Clear(); _bufs[n] = buf = new float[n]; }
            _mic.GetData(buf, at);
            for (int i = 0; i < n; i++) _pending.Add(buf[i]);
        }

        void OnDestroy() { StopMic(); }
    }

    /// <summary>Plays a remote player's voice from their head with 3D falloff.</summary>
    public class VoicePlayback : MonoBehaviour
    {
        const int Rate = 8000;
        readonly Queue<float> _buf = new Queue<float>(Rate);
        readonly object _lock = new object();
        readonly System.Random _noise = new System.Random();
        AudioSource _src;
        AudioHighPassFilter _hp; AudioLowPassFilter _lp;
        bool _radio; float _radioOn;
        /// <summary>Squad radio: in your ears (not from their position), thin and band-limited with a bit of static,
        /// and a squelch click when they key up.</summary>
        public bool Radio
        {
            set
            {
                if (value == _radio) return;
                _radio = value;
                _src.spatialBlend = value ? 0f : 1f;
                _hp.enabled = value; _lp.enabled = value;
                _src.volume = (value ? 0.75f : 1f) * Sfx.Voices;
                if (value) Sfx.UI(Synth.UiClick(), 0.25f);
            }
        }

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
            v._hp = go.AddComponent<AudioHighPassFilter>(); v._hp.cutoffFrequency = 450f; v._hp.enabled = false;
            v._lp = go.AddComponent<AudioLowPassFilter>(); v._lp.cutoffFrequency = 2800f; v._lp.enabled = false;
            v._src.Play();
            return v;
        }

        public void Push(byte[] frame)
        {
            if (_src != null) _src.volume = (_radio ? 0.75f : 1f) * Sfx.Voices;   // voice chat volume (settings)
            lock (_lock)
            {
                if (_buf.Count > Rate / 2) _buf.Clear(); // more than 0.5 s behind: drop to stay in sync
                foreach (var f in Adpcm.Decode(frame)) _buf.Enqueue(f);
            }
        }

        void OnRead(float[] data)
        {
            lock (_lock)
                for (int i = 0; i < data.Length; i++)
                {
                    float s = _buf.Count > 0 ? _buf.Dequeue() : 0f;
                    if (_radio && s != 0f) s = Mathf.Clamp(s * 1.6f, -0.8f, 0.8f) + (float)(_noise.NextDouble() - 0.5) * 0.03f;   // a little crunch and hiss
                    data[i] = s;
                }
        }
    }

    /// <summary>IMA ADPCM: 4 bits per voice sample. Frame = [predictor s16][step index u8][samples, 2 per byte].</summary>
    public static class Adpcm
    {
        static readonly int[] Steps = { 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767 };
        static readonly int[] Index = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        static int _encPred, _encIdx;   // carried between frames (the decoder starts each frame from the header)

        public static byte[] Encode(float[] pcm)
        {
            var o = new byte[3 + (pcm.Length + 1) / 2];
            o[0] = (byte)(_encPred & 0xff); o[1] = (byte)((_encPred >> 8) & 0xff); o[2] = (byte)_encIdx;
            int pred = _encPred, idx = _encIdx;
            for (int i = 0; i < pcm.Length; i++)
            {
                int s = Mathf.Clamp((int)(pcm[i] * 32767f), -32768, 32767);
                int step = Steps[idx], diff = s - pred, code = 0;
                if (diff < 0) { code = 8; diff = -diff; }
                int delta = step >> 3;
                if (diff >= step) { code |= 4; diff -= step; delta += step; }
                step >>= 1; if (diff >= step) { code |= 2; diff -= step; delta += step; }
                step >>= 1; if (diff >= step) { code |= 1; delta += step; }
                pred = Mathf.Clamp((code & 8) != 0 ? pred - delta : pred + delta, -32768, 32767);
                idx = Mathf.Clamp(idx + Index[code], 0, 88);
                if ((i & 1) == 0) o[3 + i / 2] = (byte)code; else o[3 + i / 2] |= (byte)(code << 4);
            }
            _encPred = pred; _encIdx = idx;
            return o;
        }

        public static float[] Decode(byte[] f)
        {
            if (f == null || f.Length < 4) return new float[0];
            int pred = (short)(f[0] | (f[1] << 8)), idx = Mathf.Clamp(f[2], 0, 88);
            var o = new float[(f.Length - 3) * 2];
            for (int i = 0; i < o.Length; i++)
            {
                int code = (i & 1) == 0 ? f[3 + i / 2] & 0x0f : f[3 + i / 2] >> 4;
                int step = Steps[idx], delta = step >> 3;
                if ((code & 4) != 0) delta += step;
                if ((code & 2) != 0) delta += step >> 1;
                if ((code & 1) != 0) delta += step >> 2;
                pred = Mathf.Clamp((code & 8) != 0 ? pred - delta : pred + delta, -32768, 32767);
                idx = Mathf.Clamp(idx + Index[code], 0, 88);
                o[i] = pred / 32768f;
            }
            return o;
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
