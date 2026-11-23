using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The CREATOR CAM (for content creators): a little action camera that clips onto the side of your helmet. It only
    /// has one button - RECORD. Reach to it with an empty hand and pull the trigger to start / stop (desktop: C); grab it
    /// to take it off. While it records, its red light blinks for everyone to see, your watch shows REC and the time, and
    /// the video is saved as an MP4 (Quest: Movies/Rago Extract; PC: Videos\Rago Extract). Bought from Mara, or found in raids.
    /// </summary>
    public class CamItem : Item
    {
        public static CamItem Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var it = go.AddComponent<CamItem>();
            it.def = d;
            Model(go.transform, false);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0, 0.005f); col.size = new Vector3(0.045f, 0.05f, 0.075f);
            it.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            it.SetupPhysics(0.25f);
            return it;
        }

        /// <summary>The camera body: a small black box, lens to the front (+Z), a record button on top, the red light.</summary>
        public static GameObject Model(Transform parent, bool rec)
        {
            var mb = new MB(2);
            Color body = new Color(0.08f, 0.08f, 0.09f), rim = new Color(0.35f, 0.36f, 0.38f), red = rec ? new Color(1f, 0.05f, 0.05f) : new Color(0.35f, 0.04f, 0.04f);
            mb.Sub(0).Box(Vector3.zero, new Vector3(0.04f, 0.045f, 0.06f), body, 0.006f);
            mb.Sub(1).Cyl(new Vector3(0, -0.004f, 0.03f), new Vector3(0, -0.004f, 0.04f), 0.014f, 0.013f, 14, rim);
            mb.Sub(1).Cyl(new Vector3(0, -0.004f, 0.04f), new Vector3(0, -0.004f, 0.0415f), 0.01f, 0.01f, 14, new Color(0.05f, 0.08f, 0.15f));
            mb.Sub(1).Cyl(new Vector3(0, 0.0225f, -0.01f), new Vector3(0, 0.027f, -0.01f), 0.007f, 0.007f, 10, red);   // the RECORD button
            mb.Sub(1).Sphere(new Vector3(0.012f, 0.016f, 0.031f), Vector3.one * 0.004f, 6, red);                        // the light
            return mb.Build("CamModel", parent, Mats.Lit("cam_body", Color.white, 0.2f, 0.4f, Mats.Noise, null, 0.2f, 0.1f),
                Mats.Lit("cam_parts", Color.white, 0.6f, 0.8f, null, null, 0f, 0f, false, 0.5f, rec ? new Color(1.5f, 0.1f, 0.1f) : (Color?)null));
        }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead && pl.Gear.TryWearCam(this, h)) return true;
            return base.TrySlot(h);
        }
    }

    /// <summary>
    /// The recording side. The helmet camera (smoothed, first person) is rendered into a 1024x576 picture 30 times a second
    /// and saved as a real video file:
    ///   Quest / Android: H.264 MP4 via the headset's own video encoder, saved to Movies/Rago Extract (shows in the Files /
    ///                    Gallery app, and the Quest's own media).
    ///   PC (Steam):      piped to ffmpeg (ffmpeg.exe next to the game, or on PATH), saved to your Videos\Rago Extract folder.
    /// Video only (no sound). On PC the game window also shows what the camera sees while it records.
    /// </summary>
    public class CreatorCamRecorder : MonoBehaviour
    {
        public static float Since = -1f;
        public const int W = 1024, H = 576, Fps = 30;
        Camera _cam; Transform _head; PlayerRig _p;
        Quaternion _rot; Vector3 _pos;
        RenderTexture _rt;
        float _nextFrame; long _frames; bool _busy;
        IVideoSink _sink;

        public static void Set(PlayerRig p, bool on)
        {
            var r = p.GetComponent<CreatorCamRecorder>() ?? p.gameObject.AddComponent<CreatorCamRecorder>();
            r._head = p.Head; r._p = p;
            if (on) r.Begin(); else r.End();
        }

        void Begin()
        {
            End();
            Since = Time.unscaledTime;
            if (_cam == null)
            {
                var go = new GameObject("CreatorCamView");
                go.transform.SetParent(transform, false);
                _cam = go.AddComponent<Camera>();
                _cam.stereoTargetEye = StereoTargetEyeMask.None;
                _cam.fieldOfView = 75f; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 600f;
                _cam.enabled = false;   // rendered by hand, 30 times a second
            }
            if (_rt == null) { _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); _rt.Create(); }
            _cam.targetTexture = _rt;
            _pos = _head.position; _rot = _head.rotation;
            _frames = 0; _nextFrame = Time.unscaledTime;
            string where;
            try
            {
                if (Application.platform == RuntimePlatform.Android) _sink = new AndroidSink(); else _sink = new FfmpegSink();
                where = _sink.Open(W, H, Fps);
            }
            catch (System.Exception e) { _sink = null; where = null; Debug.LogWarning("[Rago] creator cam: " + e.Message); }
            if (_sink == null || where == null)
            {
                _sink = null;
                Toast(Application.platform == RuntimePlatform.Android ? "Can't save video on this device" : "To save videos put ffmpeg.exe next to the game (ffmpeg.org)");
            }
            else Toast("Recording - saving to " + where);
        }

        void End()
        {
            if (_sink != null)
            {
                var s = _sink; _sink = null;
                string file = null;
                try { file = s.Close(); } catch (System.Exception e) { Debug.LogWarning("[Rago] creator cam close: " + e.Message); }
                if (file != null) Toast("Video saved: " + file);
            }
            Since = -1f;
        }

        void Toast(string m) { try { if (_p != null) _p.Toast(m); } catch { } }

        void OnDestroy() { End(); if (_rt != null) { _rt.Release(); Destroy(_rt); } }
        void OnApplicationQuit() { End(); }

        void LateUpdate()
        {
            if (Since < 0f || _cam == null || _head == null) return;
            // first person, from the helmet: follows your head but smoothed, so the footage isn't shaky
            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime), kr = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
            _pos = Vector3.Lerp(_pos, _head.position + _head.up * 0.06f, k);
            _rot = Quaternion.Slerp(_rot, _head.rotation, kr);
            _cam.transform.SetPositionAndRotation(_pos, _rot);
            if (Time.unscaledTime < _nextFrame) return;
            _nextFrame = Mathf.Max(_nextFrame + 1f / Fps, Time.unscaledTime - 0.1f);
            _cam.Render();
            if (_sink == null || _busy) return;
            if (!SystemInfo.supportsAsyncGPUReadback) return;
            _busy = true;
            long pts = (long)((Time.unscaledTime - Since) * 1000000.0);
            UnityEngine.Rendering.AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, req =>
            {
                _busy = false;
                if (req.hasError || _sink == null) return;
                try { _sink.Frame(req.GetData<byte>(), pts); _frames++; }
                catch (System.Exception e) { Debug.LogWarning("[Rago] creator cam frame: " + e.Message); }
            });
        }

        // PC: the window shows the camera while it records
        void OnGUI()
        {
            if (Since < 0f || _rt == null || Application.platform == RuntimePlatform.Android || Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _rt, ScaleMode.ScaleAndCrop, false);
        }

        public static string Clock() { if (Since < 0f) return ""; int s = Mathf.FloorToInt(Time.unscaledTime - Since); return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }

        static string Stamp() => "Rago_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".mp4";

        interface IVideoSink
        {
            string Open(int w, int h, int fps);   // where it saves (null = can't)
            void Frame(Unity.Collections.NativeArray<byte> rgba, long ptsUs);
            string Close();                          // the saved file
        }

        // ---------------- PC: ffmpeg
        class FfmpegSink : IVideoSink
        {
            System.Diagnostics.Process _proc; System.IO.Stream _in; string _file;
            System.Threading.Thread _th; readonly System.Collections.Generic.Queue<byte[]> _q = new System.Collections.Generic.Queue<byte[]>();
            bool _stop;

            static string FindFfmpeg()
            {
                var dir = System.IO.Path.GetDirectoryName(Application.dataPath);
                foreach (var c in new[] { System.IO.Path.Combine(dir, "ffmpeg.exe"), System.IO.Path.Combine(dir, "ffmpeg"), System.IO.Path.Combine(Application.dataPath, "ffmpeg.exe") })
                    if (System.IO.File.Exists(c)) return c;
                var path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var d in path.Split(System.IO.Path.PathSeparator))
                {
                    try { foreach (var n in new[] { "ffmpeg.exe", "ffmpeg" }) { var f = System.IO.Path.Combine(d.Trim('"'), n); if (System.IO.File.Exists(f)) return f; } } catch { }
                }
                return null;
            }

            public string Open(int w, int h, int fps)
            {
                var exe = FindFfmpeg();
                if (exe == null) return null;
                var vids = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos);
                if (string.IsNullOrEmpty(vids)) vids = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Videos");
                var dir = System.IO.Path.Combine(vids, "Rago Extract");
                System.IO.Directory.CreateDirectory(dir);
                _file = System.IO.Path.Combine(dir, Stamp());
                var psi = new System.Diagnostics.ProcessStartInfo(exe,
                    "-y -loglevel error -f rawvideo -pix_fmt rgba -s " + w + "x" + h + " -r " + fps + " -i - -vf vflip -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \"" + _file + "\"")
                { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
                _proc = System.Diagnostics.Process.Start(psi);
                _in = _proc.StandardInput.BaseStream;
                _th = new System.Threading.Thread(Pump) { IsBackground = true };
                _th.Start();
                return "Videos\\Rago Extract";
            }

            void Pump()
            {
                while (true)
                {
                    byte[] f = null;
                    lock (_q) { while (_q.Count == 0 && !_stop) System.Threading.Monitor.Wait(_q); if (_q.Count > 0) f = _q.Dequeue(); else if (_stop) break; }
                    if (f != null) try { _in.Write(f, 0, f.Length); } catch { break; }
                }
            }

            public void Frame(Unity.Collections.NativeArray<byte> rgba, long ptsUs)
            {
                var b = rgba.ToArray();
                lock (_q) { if (_q.Count < 60) _q.Enqueue(b); System.Threading.Monitor.Pulse(_q); }   // ffmpeg falling behind: drop, don't eat memory
            }

            public string Close()
            {
                lock (_q) { _stop = true; System.Threading.Monitor.Pulse(_q); }
                try { _th?.Join(5000); } catch { }
                try { _in?.Close(); } catch { }
                try { if (_proc != null && !_proc.WaitForExit(15000)) _proc.Kill(); } catch { }
                return _file != null && System.IO.File.Exists(_file) ? System.IO.Path.GetFileName(_file) : null;
            }
        }

        // ---------------- Quest / Android: MediaCodec H.264 + MediaMuxer, saved through MediaStore
        class AndroidSink : IVideoSink
        {
            AndroidJavaObject _codec, _muxer, _info, _pfd, _resolver, _uri;
            int _track = -1; bool _started; int _w, _h; sbyte[] _yuv; string _name; long _lastPts = -1;

            public string Open(int w, int h, int fps)
            {
                _w = w; _h = h; _yuv = new sbyte[w * h * 3 / 2];
                _name = Stamp();
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var media = new AndroidJavaClass("android.provider.MediaStore$Video$Media"))
                using (var cv = new AndroidJavaObject("android.content.ContentValues"))
                {
                    _resolver = act.Call<AndroidJavaObject>("getContentResolver");
                    cv.Call("put", "_display_name", _name);
                    cv.Call("put", "mime_type", "video/mp4");
                    cv.Call("put", "relative_path", "Movies/Rago Extract");
                    using (var one = new AndroidJavaObject("java.lang.Integer", 1)) cv.Call("put", "is_pending", one);
                    using (var col = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI")) _uri = _resolver.Call<AndroidJavaObject>("insert", col, cv);
                    if (_uri == null) return null;
                    _pfd = _resolver.Call<AndroidJavaObject>("openFileDescriptor", _uri, "rw");
                }
                using (var fd = _pfd.Call<AndroidJavaObject>("getFileDescriptor")) _muxer = new AndroidJavaObject("android.media.MediaMuxer", fd, 0);
                using (var fmtC = new AndroidJavaClass("android.media.MediaFormat"))
                using (var fmt = fmtC.CallStatic<AndroidJavaObject>("createVideoFormat", "video/avc", w, h))
                {
                    fmt.Call("setInteger", "color-format", 21);        // YUV420 semi-planar (NV12)
                    fmt.Call("setInteger", "bitrate", 6000000);
                    fmt.Call("setInteger", "frame-rate", fps);
                    fmt.Call("setInteger", "i-frame-interval", 1);
                    using (var mc = new AndroidJavaClass("android.media.MediaCodec")) _codec = mc.CallStatic<AndroidJavaObject>("createEncoderByType", "video/avc");
                    // configure(format, no surface, no crypto, ENCODE) - by exact signature, the nulls would confuse the lookup
                    var cfg = AndroidJNI.GetMethodID(_codec.GetRawClass(), "configure", "(Landroid/media/MediaFormat;Landroid/view/Surface;Landroid/media/MediaCrypto;I)V");
                    AndroidJNI.CallVoidMethod(_codec.GetRawObject(), cfg, new[] { new jvalue { l = fmt.GetRawObject() }, new jvalue { l = System.IntPtr.Zero }, new jvalue { l = System.IntPtr.Zero }, new jvalue { i = 1 } });
                }
                _codec.Call("start");
                _info = new AndroidJavaObject("android.media.MediaCodec$BufferInfo");
                return "Movies/Rago Extract";
            }

            public void Frame(Unity.Collections.NativeArray<byte> rgba, long ptsUs)
            {
                if (_codec == null) return;
                if (ptsUs <= _lastPts) ptsUs = _lastPts + 1;
                _lastPts = ptsUs;
                ToNv12(rgba);
                int idx = _codec.Call<int>("dequeueInputBuffer", 0L);
                if (idx < 0) { Drain(false); return; }   // encoder busy: skip this frame
                using (var buf = _codec.Call<AndroidJavaObject>("getInputBuffer", idx))
                {
                    buf.Call<AndroidJavaObject>("clear").Dispose();
                    buf.Call<AndroidJavaObject>("put", _yuv).Dispose();
                }
                _codec.Call("queueInputBuffer", idx, 0, _yuv.Length, ptsUs, 0);
                Drain(false);
            }

            /// <summary>RGBA (bottom row first, as the GPU gives it) to NV12: the picture flipped the right way up.</summary>
            void ToNv12(Unity.Collections.NativeArray<byte> src)
            {
                int w = _w, h = _h, uv = w * h;
                for (int y = 0; y < h; y++)
                {
                    int sr = (h - 1 - y) * w * 4, dr = y * w;
                    bool chromaRow = (y & 1) == 0; int cr = uv + (y >> 1) * w;
                    for (int x = 0; x < w; x++)
                    {
                        int i = sr + x * 4;
                        int r = src[i], g = src[i + 1], b = src[i + 2];
                        _yuv[dr + x] = (sbyte)(((66 * r + 129 * g + 25 * b + 128) >> 8) + 16);
                        if (chromaRow && (x & 1) == 0)
                        {
                            _yuv[cr + x] = (sbyte)(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128);
                            _yuv[cr + x + 1] = (sbyte)(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128);
                        }
                    }
                }
            }

            void Drain(bool end)
            {
                for (int guard = 0; guard < 400; guard++)
                {
                    int idx = _codec.Call<int>("dequeueOutputBuffer", _info, end ? 10000L : 0L);
                    if (idx == -1) { if (!end) return; continue; }     // try again later
                    if (idx == -2)                                      // format known: start the file
                    {
                        using (var f = _codec.Call<AndroidJavaObject>("getOutputFormat")) _track = _muxer.Call<int>("addTrack", f);
                        _muxer.Call("start"); _started = true;
                        continue;
                    }
                    if (idx < 0) continue;
                    int flags = _info.Get<int>("flags"), size = _info.Get<int>("size");
                    if ((flags & 2) == 0 && size > 0 && _started)       // not the codec-config buffer
                        using (var ob = _codec.Call<AndroidJavaObject>("getOutputBuffer", idx)) _muxer.Call("writeSampleData", _track, ob, _info);
                    _codec.Call("releaseOutputBuffer", idx, false);
                    if ((flags & 4) != 0) return;                       // end of stream
                }
            }

            public string Close()
            {
                if (_codec == null) return null;
                try
                {
                    int idx = -1;
                    for (int t = 0; t < 50 && idx < 0; t++) idx = _codec.Call<int>("dequeueInputBuffer", 10000L);
                    if (idx >= 0) _codec.Call("queueInputBuffer", idx, 0, 0, _lastPts + 1, 4);   // END_OF_STREAM
                    Drain(true);
                }
                catch (System.Exception e) { Debug.LogWarning("[Rago] creator cam drain: " + e.Message); }
                bool ok = _started;
                try { _codec.Call("stop"); } catch { }
                try { _codec.Call("release"); } catch { }
                try { if (_started) _muxer.Call("stop"); } catch { ok = false; }
                try { _muxer.Call("release"); } catch { }
                try { _pfd.Call("close"); } catch { }
                try
                {
                    if (ok) using (var cv = new AndroidJavaObject("android.content.ContentValues"))
                    using (var zero = new AndroidJavaObject("java.lang.Integer", 0))
                    {
                        cv.Call("put", "is_pending", zero);
                        var up = AndroidJNI.GetMethodID(_resolver.GetRawClass(), "update", "(Landroid/net/Uri;Landroid/content/ContentValues;Ljava/lang/String;[Ljava/lang/String;)I");
                        AndroidJNI.CallIntMethod(_resolver.GetRawObject(), up, new[] { new jvalue { l = _uri.GetRawObject() }, new jvalue { l = cv.GetRawObject() }, new jvalue { l = System.IntPtr.Zero }, new jvalue { l = System.IntPtr.Zero } });
                    }
                    else
                    {
                        var del = AndroidJNI.GetMethodID(_resolver.GetRawClass(), "delete", "(Landroid/net/Uri;Ljava/lang/String;[Ljava/lang/String;)I");
                        AndroidJNI.CallIntMethod(_resolver.GetRawObject(), del, new[] { new jvalue { l = _uri.GetRawObject() }, new jvalue { l = System.IntPtr.Zero }, new jvalue { l = System.IntPtr.Zero } });
                    }
                }
                catch { }
                foreach (var o in new[] { _codec, _muxer, _info, _pfd, _uri, _resolver }) try { o?.Dispose(); } catch { }
                _codec = null;
                return ok ? _name : null;
            }
        }
    }
}
