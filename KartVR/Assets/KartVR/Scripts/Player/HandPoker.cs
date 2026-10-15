using UnityEngine;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// Put on each VR hand (with a small trigger SphereCollider and a kinematic
    /// Rigidbody). Touching a <see cref="PokeButton"/> with it presses the button.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HandPoker : MonoBehaviour
    {
        public XRNode node = XRNode.RightHand;

        public void Buzz(float amplitude = 0.4f, float duration = 0.05f)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
