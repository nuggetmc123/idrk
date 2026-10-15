using System;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Spins an object (bumper arena sweeper, portal rings, signs). The angle comes
    /// from the real-world clock, so every player's copy is in the same position
    /// without sending anything over the network. If it has a kinematic Rigidbody
    /// it's moved with MoveRotation so it shoves karts properly.
    /// </summary>
    public class Rotator : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 30f;

        Quaternion start;
        Rigidbody body;

        void Awake()
        {
            start = transform.localRotation;
            body = GetComponent<Rigidbody>();
        }

        float Angle()
        {
            double seconds = (DateTime.UtcNow - new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            return (float)((seconds * degreesPerSecond) % 360.0);
        }

        void FixedUpdate()
        {
            if (body == null) return;
            var local = start * Quaternion.AngleAxis(Angle(), axis);
            body.MoveRotation(transform.parent ? transform.parent.rotation * local : local);
        }

        void Update()
        {
            if (body != null) return;
            transform.localRotation = start * Quaternion.AngleAxis(Angle(), axis);
        }
    }
}
