using UnityEngine;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// The steering wheel you can grab in VR. Squeeze grip with a hand near the
    /// wheel to hold it, then turn it like a real wheel. One hand or both hands
    /// both work. The kart steers from the wheel's angle, so turning is as smooth
    /// as your hands. Let go and the wheel springs back to the middle.
    ///
    /// When nobody is holding it, the wheel follows the thumbstick (or A/D keys)
    /// smoothly instead of snapping, so steering is never jerky either way.
    /// </summary>
    public class KartSteeringWheel
    {
        /// <summary>How far the wheel can turn each way (degrees).</summary>
        public const float Lock = 135f;
        /// <summary>Turning the wheel this far gives full steering.</summary>
        public const float FullSteerAngle = 100f;
        /// <summary>The wheel's tilt towards the driver (matches KartVisuals).</summary>
        public static readonly Quaternion Tilt = Quaternion.Euler(-60f, 0f, 0f);

        const float GrabRadius = 0.3f;        // how close your hand must be to the wheel's center
        const float ReturnSpeed = 300f;       // degrees/second the wheel recenters / follows the stick

        /// <summary>Wheel angle in degrees: + = turned right (clockwise as the driver sees it).</summary>
        public float Angle { get; private set; }
        public float Steer => Mathf.Clamp(Angle / FullSteerAngle, -1f, 1f);
        public bool Held => left.held || right.held;
        public bool LeftHeld => left.held;
        public bool RightHeld => right.held;

        struct Hand
        {
            public bool held;
            public bool wasGripping;
            public float lastAngle;
        }

        Hand left, right;

        /// <param name="pivot">The steering wheel's pivot (its parent's rotation defines the kart frame).</param>
        /// <param name="leftHand">Your real left hand, or null when not in VR.</param>
        public void Tick(Transform pivot, Transform leftHand, Transform rightHand, bool leftGrip, bool rightGrip,
            float stickSteer, bool allowGrab, float dt)
        {
            if (pivot == null)
            {
                Angle = Mathf.MoveTowards(Angle, stickSteer * FullSteerAngle, ReturnSpeed * dt);
                return;
            }

            Quaternion frame = (pivot.parent != null ? pivot.parent.rotation : Quaternion.identity) * Tilt;
            Vector3 center = pivot.position;
            Vector3 normal = frame * Vector3.up;        // points out of the wheel, towards the driver
            Vector3 reference = frame * Vector3.forward; // a fixed direction lying in the wheel's plane

            float sum = 0f;
            int holding = 0;
            UpdateHand(ref left, leftHand, leftGrip && allowGrab, center, normal, reference, XRNode.LeftHand, ref sum, ref holding);
            UpdateHand(ref right, rightHand, rightGrip && allowGrab, center, normal, reference, XRNode.RightHand, ref sum, ref holding);

            if (holding > 0)
                Angle += sum / holding; // two hands: use the average of how far each moved
            else
                Angle = Mathf.MoveTowards(Angle, stickSteer * FullSteerAngle, ReturnSpeed * dt);
            Angle = Mathf.Clamp(Angle, -Lock, Lock);
        }

        static void UpdateHand(ref Hand h, Transform hand, bool gripping, Vector3 center, Vector3 normal, Vector3 reference,
            XRNode node, ref float sum, ref int holding)
        {
            if (hand == null || !gripping)
            {
                h.held = false;
                h.wasGripping = gripping;
                return;
            }

            Vector3 offset = Vector3.ProjectOnPlane(hand.position - center, normal);
            bool justPressed = !h.wasGripping;
            h.wasGripping = true;

            if (!h.held)
            {
                // Grab only if you squeeze while your hand is on the wheel (not if you were already squeezing).
                if (!justPressed || (hand.position - center).magnitude > GrabRadius || offset.magnitude < 0.03f) return;
                h.held = true;
                h.lastAngle = Vector3.SignedAngle(reference, offset, normal);
                Buzz(node, 0.25f, 0.05f);
                return;
            }

            if (offset.magnitude < 0.03f) return; // hand at the very center: angle is meaningless, wait
            float a = Vector3.SignedAngle(reference, offset, normal);
            float delta = Mathf.DeltaAngle(h.lastAngle, a);
            h.lastAngle = a;
            sum += delta;
            holding++;
        }

        static void Buzz(XRNode node, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
