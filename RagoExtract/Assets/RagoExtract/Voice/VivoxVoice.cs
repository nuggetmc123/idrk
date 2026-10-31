#if RAGO_VIVOX
using System;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rago.Voice
{
    /// <summary>
    /// Proximity voice chat through Vivox (Unity Gaming Services). Everyone in the Market or in your raid
    /// shares a positional channel, so you hear people near you, fading with distance.
    /// Mute/unmute from the bunker Settings board.
    /// </summary>
    public class VivoxVoice : MonoBehaviour, IVoiceBackend
    {
        bool _ready, _muted;
        string _status = "Voice offline";
        string _channel;
        bool _positional;
        GameObject _anchor;

        public bool Ready => _ready;
        public string Status => _status;
        public bool Muted => _muted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var go = new GameObject("RagoVoice");
            DontDestroyOnLoad(go);
            NetHub.Voice = go.AddComponent<VivoxVoice>();
        }

        public async void Init(string displayName, Action<bool> done)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                    UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
                _status = "Voice connecting...";
                await VivoxService.Instance.InitializeAsync();
                await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = Clean(displayName) });
                _ready = true;
                _status = "Voice ready";
                done?.Invoke(true);
            }
            catch (Exception e)
            {
                _ready = false;
                _status = "Voice offline";
                Debug.LogWarning("[Rago] Vivox voice unavailable: " + e.Message);
                done?.Invoke(false);
            }
        }

        static string Clean(string n)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in n ?? "Operator") if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
            return sb.Length == 0 ? "Operator" : sb.ToString();
        }

        public async void Join(string channel, bool positional)
        {
            if (!_ready) return;
            try
            {
                await LeaveAsync();
                _channel = Clean(channel);
                _positional = positional;
                if (positional)
                    await VivoxService.Instance.JoinPositionalChannelAsync(_channel, ChatCapability.AudioOnly, new Channel3DProperties(40, 3, 1.0f, AudioFadeModel.InverseByDistance));
                else
                    await VivoxService.Instance.JoinGroupChannelAsync(_channel, ChatCapability.AudioOnly);
                _status = "Voice: in channel";
                if (_muted) VivoxService.Instance.MuteInputDevice();
            }
            catch (Exception e) { _status = "Voice join failed"; Debug.LogWarning("[Rago] Vivox join failed: " + e.Message); }
        }

        public async void Leave() { await LeaveAsync(); }

        async Task LeaveAsync()
        {
            if (string.IsNullOrEmpty(_channel)) return;
            var c = _channel; _channel = null;
            try { await VivoxService.Instance.LeaveChannelAsync(c); } catch { }
            _status = _ready ? "Voice ready" : _status;
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            if (!_ready) return;
            try { if (muted) VivoxService.Instance.MuteInputDevice(); else VivoxService.Instance.UnmuteInputDevice(); } catch { }
        }

        public void UpdatePosition(Transform listener)
        {
            if (!_ready || !_positional || string.IsNullOrEmpty(_channel) || listener == null) return;
            if (_anchor == null) { _anchor = new GameObject("VoiceAnchor"); DontDestroyOnLoad(_anchor); }
            _anchor.transform.SetPositionAndRotation(listener.position, listener.rotation);
            try { VivoxService.Instance.Set3DPosition(_anchor, _channel); } catch { }
        }

        public bool IsSpeaking(string participant)
        {
            if (!_ready || string.IsNullOrEmpty(_channel)) return false;
            try
            {
                if (VivoxService.Instance.ActiveChannels.TryGetValue(_channel, out var list))
                {
                    string n = Clean(participant);
                    foreach (var p in list) if (p.DisplayName == n) return p.SpeechDetected;
                }
            }
            catch { }
            return false;
        }
    }
}
#endif
