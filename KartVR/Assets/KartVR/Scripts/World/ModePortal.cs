using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Walk (or drive) through it to go to another mode.
    /// Public hub: matches you into a public session of that mode.
    /// Private lobby: takes the whole lobby there together.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ModePortal : MonoBehaviour
    {
        public KartMode targetMode = KartMode.Race;

        float nextAllowed;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        /// <summary>Called by the local player's avatar or kart when it enters the portal.</summary>
        public void Activate()
        {
            if (Time.time < nextAllowed) return;
            var sessions = SessionManager.Instance;
            if (sessions == null || sessions.Busy || !sessions.IsConnected) return;
            nextAllowed = Time.time + 3f;

            if (sessions.IsPrivate)
            {
                if (GameState.Instance != null) GameState.Instance.RequestMode(targetMode);
            }
            else
            {
                sessions.JoinPublic(targetMode);
            }
        }
    }
}
