using UnityEngine;
using UnityEngine.XR;

namespace Nullbound
{
    /// <summary>
    /// Laser pointer on each VR hand for clicking buttons from a distance: the
    /// wrist menu. It only appears when you're pointing at a button; pull
    /// that hand's trigger to click. (You can still poke buttons with your hand.)
    /// VRRig adds one to each hand at runtime.
    /// </summary>
    public class UIPointer : MonoBehaviour
    {
        public XRNode node = XRNode.RightHand;
        public float reach = 12f;

        static UIPointer left, right;
        LineRenderer line;
        Transform dot;
        PokeButton target;
        bool wasTrigger;

        /// <summary>True while this hand points at a button (so build mode doesn't also place a piece).</summary>
        public static bool IsPointingAtButton(XRNode hand)
        {
            var p = hand == XRNode.LeftHand ? left : right;
            return p != null && p.target != null;
        }

        void OnEnable()
        {
            if (node == XRNode.LeftHand) left = this; else right = this;
        }

        void Start()
        {
            var go = new GameObject("PointerLaser");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.004f;
            line.endWidth = 0.002f;
            line.sharedMaterial = NBUI.Glow(new Color(0.4f, 0.9f, 1f));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            dot = NBUI.Part(transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.012f, NBUI.Glow(Color.white), default(Vector3), "PointerDot");
            dot.gameObject.SetActive(false);
        }

        /// <summary>Straight out of the controller, where it points (the hand already follows the controller's pointing
        /// direction, so no extra tilt: the old downward tilt made the laser point at the floor).</summary>
        public Ray Aim => new Ray(transform.position, transform.forward);

        void Update()
        {
            target = null;
            if (!VRRig.IsXR || line == null)
            {
                if (line != null) line.enabled = false;
                if (dot != null) dot.gameObject.SetActive(false);
                return;
            }

            // Closest button along the ray. Buttons have trigger colliders, so ask for triggers too;
            // anything solid in front of the button blocks it.
            var ray = Aim;
            float best = reach;
            Vector3 hitPoint = Vector3.zero;
            foreach (var h in Physics.RaycastAll(ray, reach, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                if (h.distance >= best) continue;
                var button = h.collider.GetComponent<PokeButton>();
                if (button == null && h.collider.isTrigger) continue;       // other triggers (portals, checkpoints) don't block
                if (h.collider.attachedRigidbody != null && h.collider.attachedRigidbody.GetComponent<Fighter>() != null) continue;
                best = h.distance;
                target = button;                                             // null if something solid is in the way
                hitPoint = h.point;
            }
            // Don't count a button you're touching with this same hand (that's a poke, not a point).
            if (target != null && best < 0.05f) target = null;

            bool show = target != null;
            line.enabled = show;
            dot.gameObject.SetActive(show);
            bool trigger = (node == XRNode.LeftHand ? FightInput.LeftTrigger : FightInput.RightTrigger) > 0.7f;
            if (show)
            {
                line.SetPosition(0, ray.origin);
                line.SetPosition(1, hitPoint);
                dot.position = hitPoint;
                target.Hover();
                if (trigger && !wasTrigger && target.Press()) Buzz(0.35f, 0.05f);
            }
            wasTrigger = trigger;
        }

        void Buzz(float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
