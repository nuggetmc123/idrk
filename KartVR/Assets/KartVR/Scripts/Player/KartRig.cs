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
            SetHandVisuals(false);
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

        void Update()
        {
            ChooseAttachment();

            if (recenterAt > 0f && Time.time >= recenterAt) { recenterAt = -1f; Recenter(); }
            if (recenterAgainAt > 0f && Time.time >= recenterAgainAt) { recenterAgainAt = -1f; Recenter(); }
            if (KartInput.RecenterPressed) Recenter();

            if (!IsXR) DesktopControls();
        }

        /// <summary>Hub = walk on foot, any other mode = sit in your kart.</summary>
        void ChooseAttachment()
        {
            var gs = GameState.Instance;
            if (gs == null || !gs.IsReady) return;
            if (gs.Mode == KartMode.Hub)
            {
                var avatar = PlayerAvatar.Local;
                if (avatar != null && attachedAvatar != avatar) AttachTo(avatar);
            }
            else
            {
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
