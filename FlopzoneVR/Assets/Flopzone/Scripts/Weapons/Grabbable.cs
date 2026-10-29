using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>Something a hand can pick up.</summary>
    public class Grabbable : MonoBehaviour
    {
        public Rigidbody body;
        public Hand holder;
        /// <summary>Where this item goes when let go (a holster or a rack slot).</summary>
        public Transform home;
        public float returnDelay = 1.5f;
        /// <summary>Home is on the player's own body: no collider there, or it would snag their movement.</summary>
        public bool homeOnBody;
        float releasedAt = -1f;

        public bool Held => holder != null;
        public bool AtHome => home != null && transform.parent == home;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public virtual void OnGrab(Hand h)
        {
            holder = h;
            releasedAt = -1f;
            // no physics smoothing while it rides on a hand: it would drag the gun back to where
            // physics last saw it, so it jitters in your hand and lags off your hip
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            SetColliders(false);
            transform.SetParent(h.attach, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            AudioManager.Sfx("grab", transform.position, 0.6f);
        }

        public virtual void OnRelease(Hand h, Vector3 velocity, Vector3 angular)
        {
            holder = null;
            transform.SetParent(null, true);
            if (home != null && Vector3.Distance(transform.position, home.position) < 0.28f) { GoHome(); return; }
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            SetColliders(true);
            Rb.SetVelocity(body, velocity);
            body.angularVelocity = angular;
            releasedAt = Time.time;
        }

        public void GoHome()
        {
            if (home == null) return;
            releasedAt = -1f;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            SetColliders(!homeOnBody);
            transform.SetParent(home, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            AudioManager.Sfx("holster", transform.position, 0.5f);
        }

        public void SetColliders(bool on)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }

        protected virtual void Update()
        {
            if (!Held && releasedAt > 0 && home != null && Time.time - releasedAt > returnDelay) GoHome();
        }
    }
}
