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
            if (Travel(targetMode)) nextAllowed = Time.time + 3f;
        }

        /// <summary>
        /// Go to a map. Public play: join that map's session on your server number.
        /// Private lobby: take the whole lobby there. Returns false if busy connecting.
        /// </summary>
        public static bool Travel(KartMode mode)
        {
            var sessions = SessionManager.Instance;
            if (sessions == null || sessions.Busy || !sessions.IsConnected) return false;
            AudioManager.Play(AudioManager.Sfx.Teleport);
            if (sessions.IsPrivate || sessions.IsSolo) // the whole lobby moves together (solo: just you, no internet needed)
            {
                if (GameState.Instance != null) GameState.Instance.RequestMode(mode);
            }
            else
            {
                sessions.JoinPublic(mode);
            }
            return true;
        }
    }
}
