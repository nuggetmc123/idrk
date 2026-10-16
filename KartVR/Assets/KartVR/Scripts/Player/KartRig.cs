using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// The local player's VR rig (camera + hands). It isn't networked. In the
    /// Hub it rides on your walking <see cref="PlayerAvatar"/>; in every other
    /// mode it's parented to your kart's seat so you drive in first person.
    /// Recenter (X button) lines your view back up.
    ///
    /// Without a headset it falls back to a desktop mode: hold right mouse to
    /// look around, left click to press buttons.
    /// </summary>
    public class KartRig : MonoBehaviour
    {
        public static KartRig Instance { get; private set; }

        public Transform head;
        public Transform leftHand;
        public Transform rightHand;

        [Header("On foot")]
        [Tooltip("Used when your headset reports a head height too low to be standing (seated play / no floor tracking).")]
        public float standingEyeHeight = 1.6f;

        [Header("Desktop fallback")]
        public float mouseSensitivity = 0.15f;
        public Vector3 desktopLeftHand = new Vector3(-0.22f, -0.35f, 0.4f);
        public Vector3 desktopRightHand = new Vector3(0.22f, -0.35f, 0.4f);

        KartController attachedKart;
        PlayerAvatar attachedAvatar;
        float recenterAt = -1f, recenterAgainAt = -1f;
        float yaw, pitch;

        public static bool IsXR => XRSettings.isDeviceActive;

        void Awake()
        {
            // A fresh scene load brings a fresh rig; drop any old one that survived.
            if (Instance != null && Instance != this) Destroy(Instance.gameObject);
            Instance = this;
            // Your own helmet is on a layer only the Quest Cam sees (see AvatarBody.HiddenFromSelfLayer).
            var cam = head != null ? head.GetComponent<Camera>() : null;
            if (cam != null) cam.cullingMask &= ~(1 << AvatarBody.HiddenFromSelfLayer);

            // Laser pointers for clicking buttons from a distance (trigger).
            if (leftHand != null && leftHand.GetComponent<UIPointer>() == null) leftHand.gameObject.AddComponent<UIPointer>().node = XRNode.LeftHand;
            if (rightHand != null && rightHand.GetComponent<UIPointer>() == null) rightHand.gameObject.AddComponent<UIPointer>().node = XRNode.RightHand;
        }

        void OnEnable() { Application.onBeforeRender += ApplyXRPoses; }
        void OnDisable() { Application.onBeforeRender -= ApplyXRPoses; }

        /// <summary>
        /// Backup tracking: read the headset and controller poses straight from Unity's XR system, so your hands
        /// follow the controllers even if the Input System isn't receiving them.
        /// </summary>
        void ApplyXRPoses()
        {
            if (!IsXR) return;
            ApplyPose(head, XRNode.CenterEye, UnityEngine.XR.CommonUsages.centerEyePosition, UnityEngine.XR.CommonUsages.centerEyeRotation);
            ApplyPose(leftHand, XRNode.LeftHand, UnityEngine.XR.CommonUsages.devicePosition, UnityEngine.XR.CommonUsages.deviceRotation);
            ApplyPose(rightHand, XRNode.RightHand, UnityEngine.XR.CommonUsages.devicePosition, UnityEngine.XR.CommonUsages.deviceRotation);
        }

        static void ApplyPose(Transform t, XRNode node, UnityEngine.XR.InputFeatureUsage<Vector3> posUsage, UnityEngine.XR.InputFeatureUsage<Quaternion> rotUsage)
        {
            if (t == null) return;
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return;
            if (device.TryGetFeatureValue(posUsage, out Vector3 pos)) t.localPosition = pos;
            if (device.TryGetFeatureValue(rotUsage, out Quaternion rot) && rot.w * rot.w + rot.x * rot.x + rot.y * rot.y + rot.z * rot.z > 0.5f)
                t.localRotation = rot;
        }

        float missingSince = -1f;

        /// <summary>Warn (once) if the headset is on but Unity can't see the controllers.</summary>
        void CheckControllers()
        {
            if (!KartInput.ControllersMissing) { missingSince = -1f; return; }
            if (missingSince < 0f) { missingSince = Time.time; return; }
            if (Time.time - missingSince < 4f) return;
            missingSince = float.MaxValue; // warn once
            const string msg = "Controllers not found! In Unity: KartVR > Set Up VR (Quest). Also turn the controllers on and check Quest Link.";
            Debug.LogWarning("[KartVR] " + msg + " (OpenXR needs the 'Oculus Touch Controller Profile' under XR Plug-in Management > OpenXR.)");
            if (SessionManager.Instance != null) SessionManager.Instance.SetStatus(msg, 15f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsAttachedTo(KartController kart) => kart != null && attachedKart == kart;
        public bool IsAttachedTo(PlayerAvatar avatar) => avatar != null && attachedAvatar == avatar;

        void SetHandVisuals(bool visible)
        {
            // When a body is shown, its gloves are your hands; the rig's own spheres only show when detached.
            foreach (var h in new[] { leftHand, rightHand })
            {
                var v = h != null ? h.Find("Visual") : null;
                if (v) v.gameObject.SetActive(visible);
            }
        }

        public void AttachTo(KartController kart)
        {
            attachedAvatar = null;
            attachedKart = kart;
            var seat = kart.seatAnchor != null ? kart.seatAnchor : kart.transform;
            transform.SetParent(seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            SetHandVisuals(false);
            ScheduleRecenter();
        }

        public void AttachTo(PlayerAvatar avatar)
        {
            attachedKart = null;
            attachedAvatar = avatar;
            transform.SetParent(avatar.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            SetHandVisuals(false); // your body's gloves are your hands
            ScheduleRecenter();
        }

        void ScheduleRecenter()
        {
            // Tracking can take a moment to settle; recenter twice.
            recenterAt = Time.time + 0.2f;
            recenterAgainAt = Time.time + 1.5f;
        }

        public void Detach()
        {
            attachedKart = null;
            attachedAvatar = null;
            SetHandVisuals(true);
            if (transform.parent == null) return;
            transform.SetParent(null, true);
            // Get back into the normal scene (the kart may have lived in DontDestroyOnLoad).
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        public void Recenter()
        {
            if (head == null) return;
            if (attachedKart != null) RecenterInKart();
            else if (attachedAvatar != null) RecenterOnFoot();
        }

        /// <summary>Move the rig so your real head sits at the kart's head anchor, facing forward.</summary>
        void RecenterInKart()
        {
            var anchor = attachedKart.headAnchor != null ? attachedKart.headAnchor : attachedKart.transform;
            if (!IsXR)
            {
                head.localPosition = Vector3.zero;
                transform.localPosition = attachedKart.seatAnchor != null
                    ? attachedKart.seatAnchor.InverseTransformPoint(anchor.position)
                    : Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            Vector3 up = anchor.up;
            Vector3 headFwd = Vector3.ProjectOnPlane(head.forward, up);
            Vector3 seatFwd = Vector3.ProjectOnPlane(anchor.forward, up);
            if (headFwd.sqrMagnitude > 0.001f && seatFwd.sqrMagnitude > 0.001f)
                transform.RotateAround(head.position, up, Vector3.SignedAngle(headFwd, seatFwd, up));
            transform.position += anchor.position - head.position;
        }

        /// <summary>On foot: keep real room-scale walking, but fix the height if you're playing seated.</summary>
        void RecenterOnFoot()
        {
            transform.localRotation = Quaternion.identity;
            if (!IsXR)
            {
                head.localPosition = Vector3.zero;
                transform.localPosition = new Vector3(0f, standingEyeHeight, 0f);
                return;
            }
            transform.localPosition = Vector3.zero;
            float headHeight = head.localPosition.y;
            if (headHeight < 1.2f) transform.localPosition = new Vector3(0f, standingEyeHeight - headHeight, 0f);
            // Put your head over the avatar's origin.
            Vector3 offset = attachedAvatar.transform.position - head.position;
            offset.y = 0f;
            transform.position += offset;
        }

        bool floorTracking, warnedNoVR;

        /// <summary>
        /// Ask the headset to measure height from the floor (on Quest standalone it can start at eye level,
        /// which put you half under the ground).
        /// </summary>
        void RequestFloorTracking()
        {
            if (floorTracking || !IsXR) return;
            var inputs = new System.Collections.Generic.List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputs);
            foreach (var input in inputs)
            {
                if (!input.running) continue;
                if ((input.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                    input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                floorTracking = true;
                ScheduleRecenter();
            }
        }

        void Update()
        {
            ApplyXRPoses();
            RequestFloorTracking();
            CheckControllers();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsXR && !warnedNoVR && Time.realtimeSinceStartup > 6f)
            {
                warnedNoVR = true;
                SessionManager.Instance?.SetStatus("VR didn't start, so the controllers can't work. In Unity: KartVR > Set Up VR (Quest), then build the APK again.", 120f);
            }
#endif
            ChooseAttachment();

            if (recenterAt > 0f && Time.time >= recenterAt) { recenterAt = -1f; Recenter(); }
            if (recenterAgainAt > 0f && Time.time >= recenterAgainAt) { recenterAgainAt = -1f; Recenter(); }
            if (KartInput.RecenterPressed) Recenter();

            if (!IsXR) DesktopControls();
            else if (attachedAvatar == null && attachedKart == null && !BuildMode.Active) LooseControls();
        }

        bool looseTurnLatched;

        /// <summary>
        /// Backup for VR when you aren't on your player or in your kart (e.g. it hasn't appeared): you can still
        /// walk around with the left stick and turn with the right, at standing height, instead of being stuck.
        /// </summary>
        void LooseControls()
        {
            if (head == null) return;
            if (transform.parent != null) return;
            // Standing height even if the headset measures from eye level / you're sitting.
            float headY = head.position.y;
            if (headY < 1.2f) transform.position += Vector3.up * (standingEyeHeight - headY);

            float degrees = GameSettings.TurnAmount(KartInput.Turn, ref looseTurnLatched, Time.deltaTime);
            if (degrees != 0f) transform.RotateAround(head.position, Vector3.up, degrees);

            Vector2 stick = KartInput.Move;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            transform.position += (fwd * stick.y + right * stick.x) * 4.5f * Time.deltaTime;
        }

        /// <summary>Hub = walk on foot, any other mode = sit in your kart.</summary>
        void ChooseAttachment()
        {
            var gs = GameState.Instance;
            if (gs == null || !gs.IsReady)
            {
                // The game state isn't ready (yet): you're in the hub anyway, so walk around on foot.
                var walker = PlayerAvatar.Local;
                if (walker != null && attachedAvatar != walker && attachedKart == null) AttachTo(walker);
                return;
            }
            if (gs.Mode == KartMode.Hub)
            {
                var avatar = PlayerAvatar.Local;
                if (avatar != null && attachedAvatar != avatar) AttachTo(avatar);
            }
            else
            {
                if (BuildMode.Active) return; // flying around freely in build mode
                var kart = KartController.Local;
                if (kart != null && attachedKart != kart) AttachTo(kart);
            }
        }

        void DesktopControls()
        {
            var mouse = Mouse.current;
            if (mouse == null || head == null) return;

            if (mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
            }
            head.localRotation = Quaternion.Euler(pitch, yaw, 0f);

            // Hands sit in front of you, turning with your view, so menus can be clicked.
            Quaternion look = Quaternion.Euler(0f, yaw, 0f);
            if (leftHand) { leftHand.localPosition = head.localPosition + look * desktopLeftHand; leftHand.localRotation = look * Quaternion.Euler(-30f, 0f, 0f); }
            if (rightHand) { rightHand.localPosition = head.localPosition + look * desktopRightHand; rightHand.localRotation = look; }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var cam = head.GetComponent<Camera>();
                if (cam == null) return;
                var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                var hits = Physics.RaycastAll(ray, 15f, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    var button = h.collider.GetComponent<PokeButton>();
                    if (button != null) { button.Press(); break; }
                }
            }
        }
    }
}
