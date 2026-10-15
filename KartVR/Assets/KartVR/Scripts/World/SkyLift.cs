using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// A beam of light that carries you up to a map's Sky Zone. Walk or drive into
    /// it: you float up the beam, and at the top you're pushed off sideways onto
    /// the floating island. To come back down, just jump off the edge.
    ///
    /// It's checked by position (not physics triggers), so it works the same for
    /// karts (Rigidbody) and players on foot (CharacterController). Only your own
    /// kart/avatar uses it; everyone else sees you move through the network.
    /// </summary>
    public class SkyLift : MonoBehaviour
    {
        static readonly List<SkyLift> all = new List<SkyLift>();

        [Tooltip("How wide the beam is (metres).")]
        public float radius = 2.2f;
        [Tooltip("Height above this object where rising stops and you get pushed off towards the island.")]
        public float topHeight = 20f;
        [Tooltip("Which way you're pushed at the top (local, horizontal).")]
        public Vector3 exitDirection = Vector3.forward;
        public float riseSpeed = 14f;
        public float pushSpeed = 6f;

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>The lift whose beam contains this point, or null.</summary>
        public static SkyLift At(Vector3 point)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i].Contains(point)) return all[i];
            return null;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 local = point - transform.position;
            if (local.y < -1.5f || local.y > topHeight + 3f) return false;
            local.y = 0f;
            return local.sqrMagnitude <= radius * radius;
        }

        /// <summary>The velocity something inside the beam should have right now.</summary>
        public Vector3 Velocity(Vector3 point)
        {
            Vector3 local = point - transform.position;
            if (local.y < topHeight)
            {
                // Rise, drifting gently to the middle of the beam.
                var toAxis = new Vector3(-local.x, 0f, -local.z);
                return toAxis * 3f + Vector3.up * riseSpeed;
            }
            Vector3 exit = transform.TransformDirection(exitDirection);
            exit.y = 0f;
            if (exit.sqrMagnitude < 0.001f) exit = transform.forward;
            return exit.normalized * pushSpeed + Vector3.up * 3f;
        }
    }
}
