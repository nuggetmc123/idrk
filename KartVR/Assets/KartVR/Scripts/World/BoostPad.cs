using UnityEngine;

namespace KartVR
{
    /// <summary>Drive over it for a burst of speed. Needs a trigger collider.</summary>
    [RequireComponent(typeof(Collider))]
    public class BoostPad : MonoBehaviour
    {
        public float boostSeconds = 1.5f;
        public float kick = 8f;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
