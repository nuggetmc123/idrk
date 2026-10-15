using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// The local player's VR rig (camera + hands). It isn't networked: when your
    /// kart spawns, the rig is parented to the kart's seat so you ride in it in
    /// first person. Recenter (X button) lines your real head up with the
    /// driver's seat, whether you play seated or standing.
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

        [Header("Desktop fallback")]
        public float mouseSensitivity = 0.15f;
        public Vector3 desktopLeftHand = new Vector3(-0.22f, -0.35f, 0.4f);
        public Vector3 desktopRightHand = new Vector3(0.22f, -0.35f, 0.4f);

        KartController attachedKart;
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

        public void AttachTo(KartController kart)
        {
            attachedKart = kart;
            var seat = kart.seatAnchor != null ? kart.seatAnchor : kart.transform;
            transform.SetParent(seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            // Tracking can take a moment to settle after spawning; recenter twice.
            recenterAt = Time.time + 0.2f;
            recenterAgainAt = Time.time + 1.5f;
        }

        public void Detach()
        {
            attachedKart = null;
            if (transform.parent == null) return;
            transform.SetParent(null, true);
            // Get back into the normal scene (the kart may have lived in DontDestroyOnLoad).
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        /// <summary>Move the rig so your real head sits at the kart's head anchor, facing forward.</summary>
        public void Recenter()
        {
            if (attachedKart == null || head == null) return;
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

            // Yaw: rotate the rig around the head so head forward == kart forward.
            Vector3 up = anchor.up;
            Vector3 headFwd = Vector3.ProjectOnPlane(head.forward, up);
            Vector3 seatFwd = Vector3.ProjectOnPlane(anchor.forward, up);
            if (headFwd.sqrMagnitude > 0.001f && seatFwd.sqrMagnitude > 0.001f)
                transform.RotateAround(head.position, up, Vector3.SignedAngle(headFwd, seatFwd, up));

            // Position: shift so head == anchor.
            transform.position += anchor.position - head.position;
        }

        void Update()
        {
            if (recenterAt > 0f && Time.time >= recenterAt) { recenterAt = -1f; Recenter(); }
            if (recenterAgainAt > 0f && Time.time >= recenterAgainAt) { recenterAgainAt = -1f; Recenter(); }
            if (KartInput.RecenterPressed) Recenter();

            if (!IsXR) DesktopControls();
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

            // Park the hands in view so the wrist menu can be clicked.
            if (leftHand) { leftHand.localPosition = head.localPosition + desktopLeftHand; leftHand.localRotation = Quaternion.Euler(-30f, 0f, 0f); }
            if (rightHand) { rightHand.localPosition = head.localPosition + desktopRightHand; rightHand.localRotation = Quaternion.identity; }

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
