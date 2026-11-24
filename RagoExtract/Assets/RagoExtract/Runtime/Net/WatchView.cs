using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The owner is watching this player (owner panel > Watch): a small first-person view - the player's head camera,
    /// 480x270 - goes to the server as a JPEG about three times a second. It runs only while the server keeps asking (it renews
    /// every few seconds while the watch page is open) and stops by itself 15 s after the last request. The player's
    /// watch shows SPECTATED meanwhile.
    /// </summary>
    public class WatchView : MonoBehaviour
    {
        public static bool Active => _i != null && Time.unscaledTime < _i._until;
        static WatchView _i;
        const int W = 480, H = 270;
        float _until, _next;
        Camera _cam; RenderTexture _rt; Texture2D _cpu;
        bool _busy;

        public static void On()
        {
            if (_i == null) { var go = new GameObject("WatchView"); DontDestroyOnLoad(go); _i = go.AddComponent<WatchView>(); }
            if (!Active) RagoBackendLog("the owner is watching (SPECTATED)");
            _i._until = Time.unscaledTime + 15f;
        }

        public static void Off() { if (_i != null) _i._until = 0f; }

        static void RagoBackendLog(string s) { Debug.Log("[Rago] " + s); }

        void LateUpdate()
        {
            if (!Active) { if (_cam != null && _cam.enabled) _cam.enabled = false; return; }
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null || pl.Head == null || Time.unscaledTime < _next || _busy) return;
            _next = Time.unscaledTime + 0.33f;   // ~3 views a second
            if (_cam == null)
            {
                var go = new GameObject("WatchCam"); go.transform.SetParent(transform, false);
                _cam = go.AddComponent<Camera>();
                _cam.stereoTargetEye = StereoTargetEyeMask.None;
                _cam.fieldOfView = 80f; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 500f;
                _rt = new RenderTexture(W, H, 16, RenderTextureFormat.ARGB32); _rt.Create();
                _cam.targetTexture = _rt;
            }
            _cam.enabled = false;
            _cam.transform.SetPositionAndRotation(pl.Head.position, pl.Head.rotation);
            try { _cam.Render(); } catch { return; }
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                _busy = true;
                UnityEngine.Rendering.AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, req =>
                {
                    _busy = false;
                    if (!req.hasError) SendFrame(req.GetData<byte>().ToArray());
                });
            }
            else
            {
                if (_cpu == null) _cpu = new Texture2D(W, H, TextureFormat.RGBA32, false);
                var prev = RenderTexture.active; RenderTexture.active = _rt;
                _cpu.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
                RenderTexture.active = prev;
                SendFrame(_cpu.GetRawTextureData<byte>().ToArray());
            }
        }

        static void SendFrame(byte[] px)
        {
            try
            {
                // read back in Unity's own row order (bottom row first) - exactly what the JPEG encoder expects. It used to
                // flip the rows "to fix" that, which turned the view upside down on the headset.
                var jpg = ImageConversion.EncodeArrayToJPG(px, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, W, H, 0, 60);
                RelayBackend.Instance?.SendWatchFrame(jpg);
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] watch frame: " + e.Message); }
        }

        void OnDestroy() { if (_rt != null) { _rt.Release(); Destroy(_rt); } if (_cpu != null) Destroy(_cpu); }
    }
}
