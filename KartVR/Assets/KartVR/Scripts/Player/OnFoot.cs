using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Get out of your kart and walk around on any map: press Y (left controller) to hop out, press Y again to
    /// jump back in from wherever you are. In the hub you're always on foot. Your kart waits where you left it.
    /// </summary>
    public static class OnFoot
    {
        /// <summary>True while you're out of your kart (outside the hub).</summary>
        public static bool Active { get; private set; }

        static bool CanGetOut =>
            GameState.CurrentMode != KartMode.Hub && !BuildMode.Active &&
            KartController.Local != null && KartController.Local.IsReady && PlayerAvatar.Local != null && PlayerAvatar.Local.IsReady;

        public static void Toggle()
        {
            if (Active) GetIn();
            else GetOut();
        }

        public static void GetOut()
        {
            if (Active || !CanGetOut) return;
            Active = true;
            PlaceNextToKart();
            AudioManager.Play(AudioManager.Sfx.Click, 1f, 0.8f);
            SessionManager.Instance?.SetStatus("Walking! Press Y to get back in your kart", 5f);
        }

        public static void GetIn()
        {
            if (!Active) return;
            Active = false; // the rig climbs back into the kart by itself (KartRig.ChooseAttachment)
            AudioManager.Play(AudioManager.Sfx.Click, 1f, 1.2f);
        }

        /// <summary>A new map or the hub: you always start in (or, in the hub, out of) your kart.</summary>
        public static void Reset() => Active = false;

        /// <summary>Stand beside your kart (on whichever side has ground), facing the way it faces.</summary>
        public static void PlaceNextToKart()
        {
            var kart = KartController.Local;
            var avatar = PlayerAvatar.Local;
            if (kart == null || avatar == null) return;
            var k = kart.transform;
            Vector3 forward = Vector3.ProjectOnPlane(k.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            foreach (var offset in new[] { right * 1.8f, -right * 1.8f, -forward * 2.8f, forward * 2.8f })
            {
                Vector3 from = k.position + offset + Vector3.up * 3f;
                if (Physics.Raycast(from, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.collider.GetComponentInParent<KartController>() == null)
                {
                    avatar.Teleport(hit.point + Vector3.up * 0.05f, Quaternion.LookRotation(forward));
                    return;
                }
            }
            avatar.Teleport(k.position + Vector3.up * 1.2f, Quaternion.LookRotation(forward)); // no ground found: on top of the kart
        }
    }
}
