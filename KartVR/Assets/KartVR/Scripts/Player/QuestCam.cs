using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// The Quest Cam: a little camera you spawn from the menu (Camera page),
    /// grab with either grip and put wherever you like, for filming yourself.
    ///
    /// - A live preview shows on its back screen.
    /// - Pull the trigger of the hand holding it (or "Take Photo" in the menu) to
    ///   save a photo of what it sees to your Quest's pictures (PC: Pictures/KartVR).
    /// - "Follows you": it keeps its spot next to you (it rides along in your
    ///   kart). "Stays put": it stays where you left it in the world.
    /// - PC only, optional: "PC monitor: cam" shows its view on your monitor so OBS
    ///   can record it.
    /// - Videos: games can't write videos into the Quest's media library; use the
    ///   Quest's own recorder (Meta button > Camera > Record video).
    ///
    /// It's only on your computer; other players don't see it.
    /// </summary>
    public class QuestCam : MonoBehaviour
    {
        public static QuestCam Instance { get; private set; }

        const string FollowKey = "kartvr.camFollow", FovKey = "kartvr.camFov", MonitorKey = "kartvr.camMonitor", PreviewKey = "kartvr.camPreview";
        static readonly float[] Fovs = { 60f, 75f, 90f };
        static bool wanted; // stays on across scene reloads (changing map or server)

        public static bool IsOut => Instance != null;
        public static bool Follow
        {
            get => PlayerPrefs.GetInt(FollowKey, 1) == 1;
            set { PlayerPrefs.SetInt(FollowKey, value ? 1 : 0); PlayerPrefs.Save(); Instance?.RememberSpot(); }
        }
        /// <summary>Live picture on the cam's back screen. Off = the cam only renders when you take a photo.</summary>
        public static bool LivePreview
        {
            get => PlayerPrefs.GetInt(PreviewKey, 1) == 1;
            set { PlayerPrefs.SetInt(PreviewKey, value ? 1 : 0); PlayerPrefs.Save(); if (Instance != null && Instance.previewCam != null && !Instance.takingPhoto) Instance.previewCam.enabled = value; }
        }

        /// <summary>PC only, off by default: show the cam's view on your monitor instead of your eyes (for OBS).</summary>
        public static bool MonitorView
        {
            get => PlayerPrefs.GetInt(MonitorKey, 0) == 1;
            set { PlayerPrefs.SetInt(MonitorKey, value ? 1 : 0); PlayerPrefs.Save(); Instance?.ApplyMonitor(); }
        }

        /// <summary>Monitor view only makes sense on a PC with a headset (a standalone Quest has no monitor).</summary>
        public static bool MonitorSupported => XRSettings.isDeviceActive && Application.platform != RuntimePlatform.Android;

        public static float Fov
        {
            get => PlayerPrefs.GetFloat(FovKey, 75f);
            set { PlayerPrefs.SetFloat(FovKey, value); PlayerPrefs.Save(); }
        }

        Camera previewCam, screenCam;
        RenderTexture previewTexture;
        Transform view;          // where the footage is filmed from (smoothed copy of the prop)
        Renderer recLight;
        int heldBy = -1;         // 0 = left hand, 1 = right hand
        readonly bool[] wasGrip = new bool[2];
        Vector3 holdPos, followPos;
        Quaternion holdRot, followRot;
        bool usingScreen, wasTrigger;
        Renderer flash;
        float flashUntil;

        /// <summary>True while the given hand holds the camera (so that grip doesn't also grab the wheel).</summary>
        public static bool HeldBy(XRNode node) =>
            Instance != null && Instance.heldBy == (node == XRNode.LeftHand ? 0 : 1);

        // ---------------- menu actions

        public static void Toggle()
        {
            if (IsOut) Remove();
            else Spawn();
        }

        public static void Spawn()
        {
            wanted = true;
            if (Instance == null) new GameObject("QuestCam").AddComponent<QuestCam>();
            Instance.BringHere();
            AudioManager.Play(AudioManager.Sfx.Place);
            SessionManager.Instance?.SetStatus("Quest Cam spawned! Grab it with grip and point it at yourself.", 5f);
        }

        public static void Remove()
        {
            wanted = false;
            if (Instance != null) Destroy(Instance.gameObject);
            AudioManager.Play(AudioManager.Sfx.Delete);
        }

        public static void CycleFov()
        {
            int i = System.Array.IndexOf(Fovs, Fov);
            Fov = Fovs[(i + 1) % Fovs.Length];
        }

        /// <summary>Put the camera in front of you, looking back at you.</summary>
        public void BringHere()
        {
            var rig = KartRig.Instance;
            if (rig == null || rig.head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            transform.position = rig.head.position + fwd * 1.3f + Vector3.up * 0.1f;
            transform.rotation = Quaternion.LookRotation(rig.head.position - transform.position, Vector3.up);
            view.SetPositionAndRotation(transform.position, transform.rotation);
            RememberSpot();
        }

        // ---------------- setup

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            wanted = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); // the first scene is already loaded
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (runner == null)
            {
                var go = new GameObject("QuestCamWarmupRunner");
                DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
            if (!wanted) runner.StartCoroutine(Prewarm());
            // Changing map or server reloads the world; bring the camera along.
            if (wanted && Instance == null) new GameObject("QuestCam").AddComponent<QuestCam>();
        }

        void Awake()
        {
            Instance = this;
            BuildProp();

            view = new GameObject("QuestCamView").transform;
            view.SetParent(transform.parent, false);
            previewTexture = NewTexture(480, 270);
            // A plain camera that renders into a texture every frame, like an in-game mirror: the standard,
            // well-supported way in VR. It's never switched on and off every frame and never drawn by hand
            // (Camera.Render), which is what made the headset flash.
            previewCam = MakeCamera("Preview", previewTexture, -50f);
            previewCam.enabled = LivePreview;
            ApplyMonitor();
        }

        void ApplyMonitor()
        {
            bool on = MonitorView && MonitorSupported;
            if (on == usingScreen) return;
            usingScreen = on;
            if (on)
            {
                if (screenCam == null) screenCam = MakeCamera("Screen", null, 100f);
                screenCam.enabled = true;
                XRSettings.gameViewRenderMode = GameViewRenderMode.None; // stop mirroring your eyes onto the monitor
            }
            else
            {
                if (screenCam != null) screenCam.enabled = false;
                XRSettings.gameViewRenderMode = GameViewRenderMode.LeftEye;
            }
        }

        void Start()
        {
            if (KartRig.Instance != null && view.position == Vector3.zero) BringHere();
        }

        static RenderTexture NewTexture(int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "QuestCam" + w, antiAliasing = 1, useMipMap = false };
            rt.Create();
            return rt;
        }

        Camera MakeCamera(string name, RenderTexture target, float depth) => MakeCamera(view, name, target, depth);

        static Camera MakeCamera(Transform parent, string name, RenderTexture target, float depth)
        {
            // Set everything up while the object is inactive, so the camera never renders even one frame
            // with the wrong settings (e.g. straight into your headset).
            var go = new GameObject("QuestCam" + name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = false;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.targetTexture = target;
            cam.depth = depth;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = Fov;
            go.SetActive(true);
            return cam;
        }

        void BuildProp()
        {
            var body = KartUI.Mat(new Color(0.12f, 0.12f, 0.14f), 0.5f, 0.2f);
            KartUI.Part(transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.14f, 0.09f, 0.07f), body);
            KartUI.Part(transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.02f, 0.06f), KartUI.Chrome(), new Vector3(90f, 0f, 0f));
            KartUI.Part(transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.071f), new Vector3(0.045f, 0.002f, 0.045f), KartUI.Mat(new Color(0.05f, 0.1f, 0.25f), 0.95f), new Vector3(90f, 0f, 0f));
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, -0.075f, 0f), new Vector3(0.03f, 0.06f, 0.03f), body);
            flash = KartUI.Part(transform, PrimitiveType.Cube, new Vector3(-0.045f, 0.035f, 0.036f), new Vector3(0.025f, 0.012f, 0.004f), KartUI.Glow(Color.white)).GetComponent<Renderer>();
            recLight = KartUI.Part(transform, PrimitiveType.Sphere, new Vector3(0.05f, 0.05f, 0.025f), Vector3.one * 0.015f, KartUI.Glow(new Color(1f, 0.1f, 0.1f))).GetComponent<Renderer>();
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.033f), new Vector3(0.135f, 0.085f, 0.004f), KartUI.Mat(Color.black, 0.9f));
            KartUI.Label(transform, "QUEST CAM", new Vector3(0f, 0.056f, -0.036f), 0.12f, Color.white, new Vector2(0.14f, 0.02f)); // readable from behind
        }

        void OnEnable() => ApplyPreviewMaterial();

        void ApplyPreviewMaterial()
        {
            // Back screen: shows the live preview (an unlit-looking glow of the camera's picture).
            var existing = transform.Find("PreviewScreen");
            if (existing != null || previewTexture == null) return;
            var mat = new Material(KartUI.BaseMaterial != null ? KartUI.BaseMaterial : new Material(Shader.Find("Standard")));
            mat.color = Color.black;
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", previewTexture);
            mat.SetColor("_EmissionColor", Color.white);
            var quad = KartUI.Part(transform, PrimitiveType.Quad, new Vector3(0f, 0f, -0.036f), new Vector3(0.128f, 0.072f, 1f), mat, new Vector3(0f, 0f, 0f), "PreviewScreen");
            quad.localRotation = Quaternion.identity; // a quad faces -Z, i.e. out of the back of the camera
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (view != null) Destroy(view.gameObject);
            if (previewTexture != null) previewTexture.Release();
            if (usingScreen) XRSettings.gameViewRenderMode = GameViewRenderMode.LeftEye; // your eyes on the monitor again
        }

        // ---------------- every frame

        void RememberSpot()
        {
            var rig = KartRig.Instance;
            if (rig == null) return;
            followPos = rig.transform.InverseTransformPoint(transform.position);
            followRot = Quaternion.Inverse(rig.transform.rotation) * transform.rotation;
        }

        void LateUpdate()
        {
            var rig = KartRig.Instance;
            float dt = Time.deltaTime;
            if (rig != null)
            {
                Grab(rig, 0, rig.leftHand, KartInput.LeftGripHeld, XRNode.LeftHand);
                Grab(rig, 1, rig.rightHand, KartInput.RightGripHeld, XRNode.RightHand);

                if (heldBy >= 0)
                {
                    var hand = heldBy == 0 ? rig.leftHand : rig.rightHand;
                    transform.SetPositionAndRotation(hand.TransformPoint(holdPos), hand.rotation * holdRot);
                }
                else if (Follow)
                {
                    // Keep the same spot next to you, even as your kart drives along.
                    transform.SetPositionAndRotation(rig.transform.TransformPoint(followPos), rig.transform.rotation * followRot);
                }
            }

            // Smooth the footage a little (less when following, so it keeps up with a fast kart).
            float k = 1f - Mathf.Exp(-(heldBy >= 0 ? 8f : 20f) * dt);
            if ((view.position - transform.position).sqrMagnitude > 100f) k = 1f; // jumped (respawn, new map)
            view.SetPositionAndRotation(Vector3.Lerp(view.position, transform.position, k), Quaternion.Slerp(view.rotation, transform.rotation, k));

            float fov = Fov;
            previewCam.fieldOfView = fov;
            if (screenCam) screenCam.fieldOfView = fov;
            // The preview is only for you to aim with; on Quest render it every other frame to save power.

            // Trigger on the hand holding the cam = take a photo.
            if (heldBy >= 0)
            {
                bool trigger = (heldBy == 0 ? KartInput.Brake : KartInput.Gas) > 0.8f;
                if (trigger && !wasTrigger) TakePhoto();
                wasTrigger = trigger;
            }
            else wasTrigger = false;
            if (flash != null) flash.enabled = Time.time < flashUntil;
            if (recLight) recLight.enabled = Mathf.Repeat(Time.time, 1f) < 0.6f;
        }

        void Grab(KartRig rig, int index, Transform hand, bool grip, XRNode node)
        {
            if (hand == null) return;
            bool pressed = grip && !wasGrip[index];
            wasGrip[index] = grip;

            if (heldBy == index && !grip)
            {
                heldBy = -1;
                RememberSpot();
                Buzz(node, 0.2f, 0.05f);
                return;
            }
            if (heldBy < 0 && pressed && (hand.position - transform.position).sqrMagnitude < 0.2f * 0.2f)
            {
                heldBy = index;
                holdPos = hand.InverseTransformPoint(transform.position);
                holdRot = Quaternion.Inverse(hand.rotation) * transform.rotation;
                Buzz(node, 0.4f, 0.06f);
            }
        }

        // ---------------- photos (saved to the Quest's media / your PC's Pictures folder)

        public static void Photo()
        {
            if (Instance == null) { Spawn(); return; }
            Instance.TakePhoto();
        }

        bool takingPhoto;

        void TakePhoto()
        {
            if (!takingPhoto) StartCoroutine(PhotoRoutine());
        }

        System.Collections.IEnumerator PhotoRoutine()
        {
            takingPhoto = true;
            const int w = 1920, h = 1080;
            var big = NewTexture(w, h);
            // Let the camera draw one normal frame into the big texture, then read it.
            previewCam.targetTexture = big;
            previewCam.enabled = true;
            yield return new WaitForEndOfFrame();

            var prev = RenderTexture.active;
            RenderTexture.active = big;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            previewCam.targetTexture = previewTexture;
            previewCam.enabled = LivePreview;
            big.Release();
            Destroy(big);
            byte[] jpg = tex.EncodeToJPG(92);
            Destroy(tex);

            string name = "KartVR_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string where = PhotoSaver.Save(jpg, name);
            flashUntil = Time.time + 0.15f;
            AudioManager.Play(AudioManager.Sfx.Click, 1f, 0.6f);
            Buzz(heldBy == 0 ? XRNode.LeftHand : XRNode.RightHand, 0.6f, 0.08f);
            SessionManager.Instance?.SetStatus(where != null ? "Photo saved: " + where : "Couldn't save the photo (see the Console)", 5f);
            takingPhoto = false;
        }

        // ---------------- warm-up

        /// <summary>
        /// The first time a second camera renders, Unity prepares shaders for it, which can hitch the headset for
        /// a frame. Do that once while the world is loading instead of when you spawn the cam.
        /// </summary>
        static System.Collections.IEnumerator Prewarm()
        {
            var rt = NewTexture(64, 36);
            var holder = new GameObject("QuestCamWarmup");
            var cam = MakeCamera(holder.transform, "Warmup", rt, -60f);
            if (KartRig.Instance != null && KartRig.Instance.head != null)
                holder.transform.SetPositionAndRotation(KartRig.Instance.head.position, KartRig.Instance.head.rotation);
            yield return null;
            yield return null;
            Destroy(holder);
            rt.Release();
            Destroy(rt);
        }

        class Runner : MonoBehaviour { }
        static Runner runner;

        static void Buzz(XRNode node, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
