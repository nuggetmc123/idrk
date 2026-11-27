using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The CAMERA: a little action camera that clips onto the side of your helmet. It only
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

        // ---- on a gun: let go of it against the side of a gun you're holding and it clips on (right side of the
        // receiver, lens down the barrel). Saved with the gun. An empty hand on it + trigger = RECORD / stop.
        public Gun onGun;
        public bool Rec;
        public override Gun MountedOn => onGun;
        public static CamItem GunCam;
        const float GunScale = 0.55f;   // on a gun it's about half size (a small action camera), so it doesn't hide the gun   // the one on a gun you're carrying (for the record button)

        /// <summary>Clip onto a gun. at: where on the gun (its local space) - wherever you let go of it, pulled onto the gun's
        /// surface; null = the right side of the receiver. The lens always looks down the barrel.</summary>
        public void MountOn(Gun g, Vector3? at = null)
        {
            if (g == null) return;
            onGun = g; g.camAtt = this;
            SetMounted(true);
            SetCollidersTrigger(true);
            transform.SetParent(g.transform, false);
            float y = g.rig != null ? g.rig.boreY + 0.03f : 0.08f;
            Vector3 lp = at ?? new Vector3(0.042f, y, 0.06f);
            if (at.HasValue)
            {
                // onto the outside of the gun: inside its bounds it's pushed out to the nearest side, outside it's pulled in
                var b = g.localBounds; b.Expand(0.03f);
                lp = b.ClosestPoint(lp);
                Vector3 c = g.localBounds.center, e = g.localBounds.extents + Vector3.one * 0.013f;   // (half the small camera's size: it sits on the surface)
                Vector3 d = lp - c;
                float fx = e.x - Mathf.Abs(d.x), fy = e.y - Mathf.Abs(d.y);
                if (fx >= 0f && fy >= 0f && Mathf.Abs(d.z) <= e.z) { if (fx < fy) lp.x = c.x + Mathf.Sign(d.x == 0 ? 1 : d.x) * e.x; else lp.y = c.y + Mathf.Sign(d.y == 0 ? 1 : d.y) * e.y; }
            }
            transform.localPosition = lp;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * GunScale;   // a compact action-cam size on a gun
            g.camPos = lp;
        }

        bool NearGun(Gun g)
        {
            var b = g.localBounds; b.Expand(0.1f);
            return b.Contains(g.transform.InverseTransformPoint(transform.position));
        }

        public void ToggleRec(PlayerRig pl)
        {
            Rec = !CreatorCamRecorder.Active;
            CreatorCamRecorder.Set(pl, Rec, transform);
            var old = transform.Find("CamModel"); if (old != null) Destroy(old.gameObject);
            Model(transform, Rec);
            Sfx.Play(Synth.Beep(), transform.position, 0.3f, Rec ? 1.4f : 0.9f, 0.3f, 4f);
            pl.Toast(Rec ? "REC - recording from your gun" : "Recording stopped");
        }

        /// <summary>(3.58) While it records it stays put on the gun - stop recording first to take it off (reaching to
        /// press RECORD kept pulling it off).</summary>
        public override bool CanGrab(Hand h)
        {
            if (onGun != null && Rec && CreatorCamRecorder.Active)
            {
                if (Time.time - _lockToastT > 3f) { _lockToastT = Time.time; RagoGame.I?.Player?.Toast("Recording - trigger on the camera to stop, then take it off"); }
                return false;
            }
            return true;
        }
        static float _lockToastT = -9f;

        public override void OnGrab(Hand h)
        {
            if (onGun != null)
            {
                onGun.camAtt = null; onGun = null; SetMounted(false);   // (still recording if it was: it films from your hand now)
                transform.localScale = Vector3.one;   // full size again in your hand
                Sfx.Play(Synth.MagOut(), transform.position, 0.4f, 1.5f, 0.5f, 6f);
            }
            base.OnGrab(h);
        }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead)
            {
                // a gun in either hand, the camera let go next to its receiver
                foreach (var hh in new[] { pl.HandL, pl.HandR })
                    if (hh != null && hh.Held is Gun g && g.camAtt == null && g.rig != null && NearGun(g))
                    { MountOn(g, g.transform.InverseTransformPoint(transform.position)); h.Haptic(0.4f, 0.08f); pl.Toast("Camera on your gun - an empty hand on it + trigger to RECORD"); return true; }
                if (pl.Gear.TryWearCam(this, h)) return true;
            }
            return base.TrySlot(h);
        }

        protected override void OnDestroy() { base.OnDestroy(); }   // (recording carries on: the recorder finds the camera again wherever it went)
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
        Camera _cam; Transform _head, _src; PlayerRig _p;
        Quaternion _rot; Vector3 _pos;
        RenderTexture _rt;
        float _nextFrame; long _frames; bool _busy; string _err;
        Texture2D _cpu;   // the slow way, where the GPU can't hand frames back by itself
        IVideoSink _sink;

        /// <summary>Start / stop recording. source: what it films from - null = the helmet (your head, smoothed), or the
        /// vest (your chest) or a camera fitted to a gun.</summary>
        public static void Set(PlayerRig p, bool on, Transform source = null)
        {
            var r = p.GetComponent<CreatorCamRecorder>() ?? p.gameObject.AddComponent<CreatorCamRecorder>();
            r._head = p.Head; r._p = p; r._src = source; r._follow = source != null && source.GetComponentInParent<CamItem>() != null;
            if (on) r.Begin(); else r.End();
        }

        /// <summary>Recording right now (whoever has the camera).</summary>
        public static bool Active => Since >= 0f;
        /// <summary>Keep recording, but film from somewhere else: null = your helmet, your chest (vest), or the camera
        /// itself wherever it goes (taken off, handed to another player, on their gun / helmet / vest).</summary>
        public static void Retarget(PlayerRig p, Transform source)
        {
            if (p == null) return;
            var r = p.GetComponent<CreatorCamRecorder>(); if (r == null) return;
            r._src = source; r._follow = source != null && source.GetComponentInParent<CamItem>() != null;
        }
        public static void Stop(PlayerRig p) { if (p == null) return; var r = p.GetComponent<CreatorCamRecorder>(); if (r != null) r.End(); }

        bool _follow; float _searchT;
        void Awake() { Vitals.OnLocalDeath += OnDied; }
        void OnDied(int killer) { if (Active) { End(); } }   // the person recording died: that's the end of the video

        /// <summary>The camera's gone from where we were filming (picked up by someone else, put on their helmet or vest):
        /// find it again - our own copy if we have one, else the one we see on another player.</summary>
        Transform FindCamera()
        {
            foreach (var it in Item.All) if (it is CamItem ci && ci != null && !ci.Consumed) return ci.transform;
            if (NetGame.I != null)
                foreach (var kv in NetGame.I.Remotes)
                {
                    if (kv.Value == null) continue;
                    var t = FindDeep(kv.Value.transform, "CamModel");
                    if (t != null) return t;
                }
            return null;
        }
        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var f = FindDeep(t.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        void Begin()
        {
            End();
            Since = Time.unscaledTime; _err = null;
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
            _pos = (_src != null ? _src : _head).position; _rot = (_src != null ? _src : _head).rotation;
            _frames = 0; _nextFrame = Time.unscaledTime;
            string where;
            try
            {
                if (Application.platform == RuntimePlatform.Android) { _sink = new AndroidSink(); where = _sink.Open(W, H, Fps); }
                else
                {
                    // PC: an MP4 through ffmpeg if it's there, otherwise our own video file (MJPEG .avi - plays in the Windows
                    // Media Player / Films & TV app and VLC, no extra download needed)
                    _sink = new FfmpegSink(); where = _sink.Open(W, H, Fps);
                    if (where == null) { _sink = new AviSink(); where = _sink.Open(W, H, Fps); }
                }
            }
            catch (System.Exception e) { _sink = null; where = null; _err = e.Message; Debug.LogWarning("[Rago] creator cam: " + e); }
            if (_sink == null || where == null)
            {
                _sink = null;
                Toast("Can't save video on this device" + (_err != null ? " (" + _err + ")" : ""));
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
                if (file != null) Toast("Video saved (" + _frames + " frames): " + file);
                else Toast(_frames == 0 ? "No video was captured - nothing saved" : "The video couldn't be saved");
            }
            Since = -1f;
        }

        void Toast(string m) { try { if (_p != null) _p.Toast(m); } catch { } }

        void OnDestroy() { Vitals.OnLocalDeath -= OnDied; End(); if (_rt != null) { _rt.Release(); Destroy(_rt); } if (_cpu != null) Destroy(_cpu); }
        void OnApplicationQuit() { End(); }

        void LateUpdate()
        {
            if (Since < 0f || _cam == null || _head == null) return;
            // following the camera and it's gone (another player picked it up): look for it again twice a second; meanwhile
            // the picture holds where it was
            if (_follow && _src == null)
            {
                _searchT -= Time.unscaledDeltaTime;
                if (_searchT <= 0f) { _searchT = 0.5f; _src = FindCamera(); }
                if (_src == null) { if (Time.unscaledTime >= _nextFrame) { _nextFrame = Time.unscaledTime + 1f / Fps; _cam.Render(); } return; }
            }
            // first person, from the helmet: follows your head but smoothed, so the footage isn't shaky
            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime), kr = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
            Vector3 wantP = _src != null ? _src.position + _src.forward * (_src == _head ? 0f : 0.17f) + _src.up * 0.08f : _head.position + _head.up * 0.06f;
            Quaternion wantR = _src != null ? _src.rotation : _head.rotation;
            if (_src != null && _src.GetComponentInParent<Gun>() != null) { wantP = _src.position + _src.forward * 0.04f; wantR = _src.rotation; }   // on a gun: straight down the barrel
            if (_src != null && _src != _head)
            {
                // a gun or a vest moves all the time: a steadier picture - slower to follow, and the horizon kept level
                Vector3 f = wantR * Vector3.forward;
                if (Mathf.Abs(f.y) < 0.98f) wantR = Quaternion.LookRotation(f, Vector3.up);
                k = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime); kr = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            }
            _pos = Vector3.Lerp(_pos, wantP, k);
            _rot = Quaternion.Slerp(_rot, wantR, kr);
            _cam.transform.SetPositionAndRotation(_pos, _rot);
            if (Time.unscaledTime < _nextFrame) return;
            _nextFrame = Mathf.Max(_nextFrame + 1f / Fps, Time.unscaledTime - 0.1f);
            _cam.Render();
            if (_sink == null || _busy) return;
            long pts = (long)((Time.unscaledTime - Since) * 1000000.0);
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                // read it back the slow way (stalls a moment; fine at this size)
                try
                {
                    if (_cpu == null) _cpu = new Texture2D(W, H, TextureFormat.RGBA32, false);
                    var prev = RenderTexture.active; RenderTexture.active = _rt;
                    _cpu.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
                    RenderTexture.active = prev;
                    _sink.Frame(_cpu.GetRawTextureData<byte>(), pts); _frames++;
                }
                catch (System.Exception e) { Debug.LogWarning("[Rago] creator cam frame (cpu): " + e.Message); }
                return;
            }
            _busy = true;
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

        // ---------------- PC without ffmpeg: Motion-JPEG in an .avi (our own writer, JPEGs made on a worker thread)
        class AviSink : IVideoSink
        {
            System.IO.FileStream _f; string _file; int _w, _h, _fps;
            readonly System.Collections.Generic.List<(long off, int size)> _index = new System.Collections.Generic.List<(long, int)>();
            long _moviStart, _riffSizeAt, _framesAt1, _framesAt2, _moviSizeAt;
            byte[] _last; long _written;
            System.Threading.Thread _th; readonly System.Collections.Generic.Queue<(byte[] px, long pts)> _q = new System.Collections.Generic.Queue<(byte[], long)>();
            bool _stop;

            public string Open(int w, int h, int fps)
            {
                _w = w; _h = h; _fps = fps;
                var vids = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos);
                if (string.IsNullOrEmpty(vids)) vids = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Videos");
                var dir = System.IO.Path.Combine(vids, "Rago Extract");
                System.IO.Directory.CreateDirectory(dir);
                _file = System.IO.Path.Combine(dir, Stamp().Replace(".mp4", ".avi"));
                _f = new System.IO.FileStream(_file, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite);
                var b = new System.IO.BinaryWriter(_f);
                void Four(string s) => b.Write(System.Text.Encoding.ASCII.GetBytes(s));
                Four("RIFF"); _riffSizeAt = _f.Position; b.Write(0); Four("AVI ");
                Four("LIST"); b.Write(4 + 8 + 56 + 8 + 4 + 8 + 56 + 8 + 40); Four("hdrl");
                Four("avih"); b.Write(56);
                b.Write(1000000 / fps); b.Write(w * h * 3 * fps); b.Write(0); b.Write(0x10);   // has an index
                _framesAt1 = _f.Position; b.Write(0); b.Write(0); b.Write(1); b.Write(w * h * 3); b.Write(w); b.Write(h); b.Write(0); b.Write(0); b.Write(0); b.Write(0);
                Four("LIST"); b.Write(4 + 8 + 56 + 8 + 40); Four("strl");
                Four("strh"); b.Write(56); Four("vids"); Four("MJPG"); b.Write(0); b.Write((short)0); b.Write((short)0); b.Write(0);
                b.Write(1); b.Write(fps); b.Write(0); _framesAt2 = _f.Position; b.Write(0); b.Write(w * h * 3); b.Write(-1); b.Write(0);
                b.Write((short)0); b.Write((short)0); b.Write((short)w); b.Write((short)h);
                Four("strf"); b.Write(40); b.Write(40); b.Write(w); b.Write(h); b.Write((short)1); b.Write((short)24); Four("MJPG"); b.Write(w * h * 3); b.Write(0); b.Write(0); b.Write(0); b.Write(0);
                Four("LIST"); _moviSizeAt = _f.Position; b.Write(0); _moviStart = _f.Position; Four("movi");
                b.Flush();
                _th = new System.Threading.Thread(Pump) { IsBackground = true };
                _th.Start();
                return "Videos\\Rago Extract";
            }

            public void Frame(Unity.Collections.NativeArray<byte> rgba, long ptsUs)
            {
                var px = rgba.ToArray();
                lock (_q) { if (_q.Count < 20) _q.Enqueue((px, ptsUs)); System.Threading.Monitor.Pulse(_q); }
            }

            void Pump()
            {
                int row = _w * 4; var flip = new byte[row * _h];
                while (true)
                {
                    (byte[] px, long pts) f = (null, 0);
                    lock (_q) { while (_q.Count == 0 && !_stop) System.Threading.Monitor.Wait(_q); if (_q.Count > 0) f = _q.Dequeue(); else break; }
                    try
                    {
                        // GPU rows come bottom-first; a JPEG is top-first
                        for (int y = 0; y < _h; y++) System.Buffer.BlockCopy(f.px, (_h - 1 - y) * row, flip, y * row, row);
                        var jpg = ImageConversion.EncodeArrayToJPG(flip, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, (uint)_w, (uint)_h, 0, 80);
                        // keep the clock right: a frame that came late fills the gap with copies of the last one
                        long want = f.pts * _fps / 1000000L;
                        for (int fill = 0; _last != null && _written < want && fill < 60; fill++) Write(_last);
                        Write(jpg); _last = jpg;
                    }
                    catch (System.Exception e) { Debug.LogWarning("[Rago] avi frame: " + e.Message); }
                }
            }

            void Write(byte[] jpg)
            {
                lock (_f)
                {
                    long off = _f.Position - _moviStart;
                    var b = new System.IO.BinaryWriter(_f);
                    b.Write(System.Text.Encoding.ASCII.GetBytes("00dc")); b.Write(jpg.Length); b.Write(jpg);
                    if ((jpg.Length & 1) == 1) b.Write((byte)0);
                    _index.Add((off, jpg.Length));
                    _written++;
                }
            }

            public string Close()
            {
                lock (_q) { _stop = true; System.Threading.Monitor.Pulse(_q); }
                try { _th?.Join(10000); } catch { }
                if (_f == null) return null;
                lock (_f)
                {
                    var b = new System.IO.BinaryWriter(_f);
                    long moviEnd = _f.Position;
                    b.Write(System.Text.Encoding.ASCII.GetBytes("idx1")); b.Write(_index.Count * 16);
                    foreach (var e in _index) { b.Write(System.Text.Encoding.ASCII.GetBytes("00dc")); b.Write(0x10); b.Write((int)e.off); b.Write(e.size); }
                    long end = _f.Position;
                    _f.Position = _riffSizeAt; b.Write((int)(end - 8));
                    _f.Position = _moviSizeAt; b.Write((int)(moviEnd - _moviStart));
                    _f.Position = _framesAt1; b.Write(_index.Count);
                    _f.Position = _framesAt2; b.Write(_index.Count);
                    b.Flush(); _f.Close();
                }
                if (_index.Count == 0) { try { System.IO.File.Delete(_file); } catch { } return null; }
                return System.IO.Path.GetFileName(_file);
            }
        }

        // ---------------- Quest / Android: MediaCodec H.264 + MediaMuxer, saved through MediaStore
        class AndroidSink : IVideoSink
        {
            AndroidJavaObject _codec, _muxer, _info, _pfd, _resolver, _uri;
            int _track = -1; bool _started, _planar; int _w, _h; sbyte[] _yuv; string _name; long _lastPts = -1;

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
                    fmt.Call("setInteger", "color-format", 21);        // YUV420 semi-planar (NV12); I420 if the encoder won't take it
                    fmt.Call("setInteger", "bitrate", 6000000);
                    fmt.Call("setInteger", "frame-rate", fps);
                    fmt.Call("setInteger", "i-frame-interval", 1);
                    foreach (int cf in new[] { 21, 19 })
                    {
                        fmt.Call("setInteger", "color-format", cf);
                        using (var mc = new AndroidJavaClass("android.media.MediaCodec")) _codec = mc.CallStatic<AndroidJavaObject>("createEncoderByType", "video/avc");
                        // configure(format, no surface, no crypto, ENCODE) - by exact signature, the nulls would confuse the lookup
                        var cfg = AndroidJNI.GetMethodID(_codec.GetRawClass(), "configure", "(Landroid/media/MediaFormat;Landroid/view/Surface;Landroid/media/MediaCrypto;I)V");
                        AndroidJNI.CallVoidMethod(_codec.GetRawObject(), cfg, new[] { new jvalue { l = fmt.GetRawObject() }, new jvalue { l = System.IntPtr.Zero }, new jvalue { l = System.IntPtr.Zero }, new jvalue { i = 1 } });
                        if (AndroidJNI.ExceptionOccurred() != System.IntPtr.Zero) { AndroidJNI.ExceptionClear(); try { _codec.Call("release"); } catch { } _codec.Dispose(); _codec = null; continue; }
                        _planar = cf == 19;
                        break;
                    }
                    if (_codec == null) throw new System.Exception("the video encoder won't start");
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
                            sbyte u = (sbyte)(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128), v = (sbyte)(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128);
                            if (_planar) { int ci = (y >> 1) * (w >> 1) + (x >> 1); _yuv[uv + ci] = u; _yuv[uv + uv / 4 + ci] = v; }   // I420: all U, then all V
                            else { _yuv[cr + x] = u; _yuv[cr + x + 1] = v; }                                                     // NV12: U V interleaved
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

    /// <summary>
    /// The CC TROPHY: a gold cup on a black plinth with a red play button on the front - the owner hands it to content
    /// creators (owner panel > Give CC trophy, it turns up on their Deliveries screen). Just for show: it can't be sold for anything.
    /// </summary>
    public class TrophyItem : LootItem
    {
        public static TrophyItem Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var it = go.AddComponent<TrophyItem>();
            it.def = d;
            Model(go.transform);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.085f, 0); col.size = new Vector3(0.13f, 0.17f, 0.09f);
            it.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            it.SetupPhysics(0.9f);
            return it;
        }

        public static GameObject Model(Transform parent)
        {
            var mb = new MB(3);
            Color gold = new Color(1f, 0.78f, 0.25f), black = new Color(0.06f, 0.06f, 0.07f), red = new Color(0.9f, 0.08f, 0.08f);
            // plinth
            mb.Sub(1).Box(new Vector3(0, 0.017f, 0), new Vector3(0.085f, 0.034f, 0.07f), black, 0.004f);
            mb.Sub(0).Box(new Vector3(0, 0.037f, 0), new Vector3(0.065f, 0.006f, 0.055f), gold, 0.002f);
            // stem and cup
            mb.Sub(0).Lathe(new Vector3(0, 0.04f, 0), Quaternion.identity, new[] {
                new Vector2(0.022f, 0f), new Vector2(0.01f, 0.012f), new Vector2(0.007f, 0.035f), new Vector2(0.012f, 0.045f),
                new Vector2(0.03f, 0.06f), new Vector2(0.04f, 0.085f), new Vector2(0.043f, 0.12f), new Vector2(0.04f, 0.121f), new Vector2(0.036f, 0.09f), new Vector2(0.001f, 0.075f) }, 24, gold);
            // handles
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sub(0).Capsule(new Vector3(sx * 0.04f, 0.15f, 0), new Vector3(sx * 0.062f, 0.14f, 0), 0.004f, 8, gold);
                mb.Sub(0).Capsule(new Vector3(sx * 0.062f, 0.14f, 0), new Vector3(sx * 0.058f, 0.115f, 0), 0.004f, 8, gold);
                mb.Sub(0).Capsule(new Vector3(sx * 0.058f, 0.115f, 0), new Vector3(sx * 0.034f, 0.105f, 0), 0.004f, 8, gold);
            }
            // the play button on the front of the plinth (+Z)
            mb.Sub(2).Box(new Vector3(0, 0.018f, 0.0355f), new Vector3(0.05f, 0.026f, 0.003f), red, 0.003f);
            mb.Sub(1).Push(new Vector3(0, 0.018f, 0.0375f), Quaternion.Euler(0, -90f, 0));
            mb.Sub(1).Extrude(new[] { new Vector2(-0.006f, -0.008f), new Vector2(-0.006f, 0.008f), new Vector2(0.008f, 0f) }, 0.002f, Color.white, 0.0005f);
            mb.Pop();
            return mb.Build("Trophy", parent,
                Mats.Lit("trophy_gold", Color.white, 1f, 0.85f, Mats.Metal, null, 0.05f, 0.1f),
                Mats.Lit("trophy_base", Color.white, 0.1f, 0.6f, null, null, 0f, 0f),
                Mats.Lit("trophy_red", Color.white, 0.1f, 0.6f, null, null, 0f, 0f, false, 0.5f, new Color(0.4f, 0.02f, 0.02f)));
        }
    }
}
