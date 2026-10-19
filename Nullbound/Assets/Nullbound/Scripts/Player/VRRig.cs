using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Nullbound
{
    /// <summary>
    /// The local player's VR rig (camera + hands). It isn't networked: it rides on your own <see cref="Fighter"/>,
    /// which reads your head and hands from it every tick and sends them to everyone else.
    ///
    /// Without a headset it falls back to a desktop mode: hold right mouse to look around, left click / G to punch
    /// (the hand really shoots forward, so the same punch code runs), H to block, left click on buttons to press them.
    /// </summary>
    public class VRRig : MonoBehaviour
    {
        public static VRRig Instance { get; private set; }

        public Transform head;
        public Transform leftHand;
        public Transform rightHand;

        [Tooltip("Used when your headset reports a head height too low to be standing (seated play / no floor tracking).")]
        public float standingEyeHeight = 1.65f;

        [Header("Desktop fallback")]
        public float mouseSensitivity = 0.15f;
        public Vector3 desktopLeftHand = new Vector3(-0.2f, -0.3f, 0.35f);
        public Vector3 desktopRightHand = new Vector3(0.2f, -0.3f, 0.35f);

        Fighter attached;
        float recenterAt = -1f, recenterAgainAt = -1f;
        float yaw, pitch;
        float punchLeftAt = -1f, punchRightAt = -1f;

        public static bool IsXR => XRSettings.isDeviceActive;

        /// <summary>Fires right before each frame is drawn, after the newest headset and controller poses are in.</summary>
        public static event System.Action AfterPoses;

        void Awake()
        {
            if (Instance != null && Instance != this) Destroy(Instance.gameObject);
            Instance = this;
            if (leftHand != null && leftHand.GetComponent<UIPointer>() == null) leftHand.gameObject.AddComponent<UIPointer>().node = XRNode.LeftHand;
            if (rightHand != null && rightHand.GetComponent<UIPointer>() == null) rightHand.gameObject.AddComponent<UIPointer>().node = XRNode.RightHand;
        }

        void OnEnable() { Application.onBeforeRender += BeforeRender; }
        void OnDisable() { Application.onBeforeRender -= BeforeRender; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void BeforeRender()
        {
            ApplyXRPoses();
            AfterPoses?.Invoke();
        }

        /// <summary>Read the headset and controller poses straight from Unity's XR system (works without the Input System).</summary>
        void ApplyXRPoses()
        {
            if (!IsXR) return;
            ApplyPose(head, XRNode.CenterEye, UnityEngine.XR.CommonUsages.centerEyePosition, UnityEngine.XR.CommonUsages.centerEyeRotation);
            ApplyHandPose(leftHand, XRNode.LeftHand);
            ApplyHandPose(rightHand, XRNode.RightHand);
        }

        static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");

        /// <summary>
        /// Hands: the position is the grip (the middle of your palm, where your real hand is), and the direction is the
        /// controller's pointing direction, which is where your knuckles point when you punch. (The grip's own rotation
        /// follows the angled handle, which made the gloves look tilted against your real hands.)
        /// </summary>
        static void ApplyHandPose(Transform t, XRNode node)
        {
            if (t == null) return;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 pos)) t.localPosition = pos;
            bool Ok(Quaternion q) => q.w * q.w + q.x * q.x + q.y * q.y + q.z * q.z > 0.5f;
            if (device.TryGetFeatureValue(PointerRotation, out Quaternion aim) && Ok(aim)) t.localRotation = aim;
            else if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rot) && Ok(rot))
                t.localRotation = rot * Quaternion.Euler(GripToAimDegrees, 0f, 0f);
        }

        /// <summary>Only used if the controller doesn't report a pointing direction: tilt the grip to match it.</summary>
        const float GripToAimDegrees = -35f;

        static void ApplyPose(Transform t, XRNode node, InputFeatureUsage<Vector3> posUsage, InputFeatureUsage<Quaternion> rotUsage)
        {
            if (t == null) return;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return;
            if (device.TryGetFeatureValue(posUsage, out Vector3 pos)) t.localPosition = pos;
            if (device.TryGetFeatureValue(rotUsage, out Quaternion rot) && rot.w * rot.w + rot.x * rot.x + rot.y * rot.y + rot.z * rot.z > 0.5f)
                t.localRotation = rot;
        }

        public bool IsAttachedTo(Fighter f) => f != null && attached == f;

        public void AttachTo(Fighter fighter)
        {
            attached = fighter;
            transform.SetParent(fighter.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            ScheduleRecenter();
        }

        public void Detach()
        {
            attached = null;
            if (transform.parent == null) return;
            looseFloorY = transform.parent.position.y; // the fighter's feet
            transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        void ScheduleRecenter()
        {
            // Tracking can take a moment to settle; recenter twice.
            recenterAt = Time.time + 0.2f;
            recenterAgainAt = Time.time + 1.5f;
        }

        /// <summary>Put your head over your fighter, at standing height even if you play seated.</summary>
        public void Recenter()
        {
            if (head == null || attached == null) return;
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
            Vector3 offset = attached.transform.position - head.position;
            offset.y = 0f;
            transform.position += offset;
        }

        /// <summary>
        /// Room scale off: keep your head over the fighter's middle (stepping in your room doesn't move you). Room
        /// scale on: the fighter moves with you instead (see Fighter), so nothing to do here.
        /// </summary>
        public void KeepHeadCentered()
        {
            if (!IsXR || attached == null || head == null) return;
            Vector3 offset = attached.transform.position - head.position;
            offset.y = 0f;
            transform.position += offset;
        }

        bool floorTracking, warnedNoVR, placedInMenu;
        float missingSince = -1f;

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

        void CheckControllers()
        {
            if (!FightInput.ControllersMissing) { missingSince = -1f; return; }
            if (missingSince < 0f) { missingSince = Time.time; return; }
            if (Time.time - missingSince < 4f) return;
            missingSince = float.MaxValue; // warn once
            const string msg = "Controllers not found! In Unity: Nullbound > Set Up VR (Quest). Also turn the controllers on and check Quest Link.";
            Debug.LogWarning("[Nullbound] " + msg);
            SessionManager.Instance?.SetStatus(msg, 15f);
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
                SessionManager.Instance?.SetStatus("VR didn't start, so the controllers can't work. In Unity: Nullbound > Set Up VR (Quest), then build the APK again.", 120f);
            }
#endif
            var mine = Fighter.Local;
            if (mine != null && attached != mine) AttachTo(mine);
            // In the main menu: stand at the menu's spot (once per scene load).
            if (attached == null && !placedInMenu && MainMenu.Instance != null && SessionManager.Instance != null && SessionManager.Instance.InMenu)
            {
                placedInMenu = true;
                transform.SetPositionAndRotation(MainMenu.Instance.StandPoint, MainMenu.Instance.StandRotation);
                looseFloorY = MainMenu.Instance.StandPoint.y;
                if (!IsXR) { head.localPosition = new Vector3(0f, standingEyeHeight, 0f); yaw = transform.eulerAngles.y; }
            }

            if (recenterAt > 0f && Time.time >= recenterAt) { recenterAt = -1f; Recenter(); }
            if (recenterAgainAt > 0f && Time.time >= recenterAgainAt) { recenterAgainAt = -1f; Recenter(); }
            if (FightInput.RecenterPressed) Recenter();

            if (!IsXR) DesktopControls();
            else if (attached == null) LooseControls();
        }

        bool looseTurnLatched;
        float looseFloorY;

        /// <summary>Backup for VR before your fighter appears: walk with the left stick, turn with the right.</summary>
        void LooseControls()
        {
            if (head == null || transform.parent != null) return;
            // Keep the rig's floor on the menu's floor, lifted only if the headset reports you sitting / no floor. This is
            // set fresh every frame: it used to ADD a lift while the headset was still starting up, and once floor
            // tracking kicked in that lift stayed, so you stood about 1.6 m too tall in the main menu.
            float local = head.localPosition.y;
            float lift = local < 1.2f ? standingEyeHeight - local : 0f;
            var p = transform.position;
            transform.position = new Vector3(p.x, looseFloorY + lift, p.z);
            float degrees = GameSettings.TurnAmount(FightInput.Turn, ref looseTurnLatched, Time.deltaTime);
            if (degrees != 0f) transform.RotateAround(head.position, Vector3.up, degrees);
            Vector2 stick = FightInput.Move;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            transform.position += (fwd * stick.y + right * stick.x) * 4f * Time.deltaTime;
        }

        void DesktopControls()
        {
            var mouse = Mouse.current;
            if (head == null) return;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
            }
            head.localRotation = Quaternion.Euler(pitch, yaw - transform.eulerAngles.y, 0f);
            // Not in a fighter (main menu): walk around with W/A/S/D.
            if (attached == null)
            {
                Vector2 stick = FightInput.Move;
                Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized, right = Vector3.Cross(Vector3.up, fwd);
                transform.position += (fwd * stick.y + right * stick.x) * 3f * Time.deltaTime;
            }

            // Clicking a menu button wins over punching.
            bool clickedButton = false;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var cam = head.GetComponent<Camera>();
                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                    var hits = Physics.RaycastAll(ray, 15f, ~0, QueryTriggerInteraction.Collide);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var h in hits)
                    {
                        var button = h.collider.GetComponent<PokeButton>();
                        if (button != null) { button.Press(); clickedButton = true; break; }
                    }
                }
            }
            if (FightInput.KeyPunchRight && !clickedButton) punchRightAt = Time.time;
            if (FightInput.KeyPunchLeft) punchLeftAt = Time.time;

            // Hands in front of you, turning with your view. A punch shoots the hand out and back (fast enough to count),
            // H raises both into a guard in front of your face.
            Quaternion look = Quaternion.Euler(pitch * 0.5f, yaw - transform.eulerAngles.y, 0f);
            bool guard = FightInput.KeyBlockHeld;
            Vector3 l = guard ? new Vector3(-0.1f, -0.05f, 0.25f) : desktopLeftHand, r = guard ? new Vector3(0.1f, -0.05f, 0.25f) : desktopRightHand;
            l += Vector3.forward * PunchReach(punchLeftAt);
            r += Vector3.forward * PunchReach(punchRightAt);
            if (leftHand) { leftHand.localPosition = head.localPosition + look * l; leftHand.localRotation = look; }
            if (rightHand) { rightHand.localPosition = head.localPosition + look * r; rightHand.localRotation = look; }
        }

        /// <summary>Out 0.45 m in 0.07 s, back in 0.15 s.</summary>
        static float PunchReach(float startedAt)
        {
            if (startedAt < 0f) return 0f;
            float t = Time.time - startedAt;
            if (t < 0.07f) return 0.45f * t / 0.07f;
            if (t < 0.22f) return 0.45f * (1f - (t - 0.07f) / 0.15f);
            return 0f;
        }
    }
}
