using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Walk (or drive) through it to go to another mode.
    /// Public: puts you in a public room on that map. Private lobby: that map in your lobby.
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
        /// Go to a map. Public play: a public room on that map (the fullest with space). Private lobby: that map in
        /// your lobby, on your own (anyone in a lobby can go anywhere). Returns false if busy connecting.
        /// </summary>
        public static bool Travel(KartMode mode)
        {
            var sessions = SessionManager.Instance;
            if (sessions == null || sessions.Busy || !sessions.IsConnected) return false;
            AudioManager.Play(AudioManager.Sfx.Teleport);
            if (sessions.IsSolo || sessions.IsTutorial) // solo: the map just changes on this headset, no internet needed
            {
                if (GameState.Instance != null) GameState.Instance.RequestMode(mode);
            }
            else
            {
                sessions.Travel(mode);
            }
            return true;
        }
    }
}
