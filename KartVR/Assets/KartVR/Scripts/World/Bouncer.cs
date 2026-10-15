using UnityEngine;

namespace KartVR
{
    /// <summary>A bouncy bumper: karts that hit it get flung away. Needs a (non-trigger) collider.</summary>
    public class Bouncer : MonoBehaviour
    {
        public float power = 9f;
        public float lift = 2f;
    }
}
