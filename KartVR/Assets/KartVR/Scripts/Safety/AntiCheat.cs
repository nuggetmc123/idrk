using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Cheat protection that works without a game server.
    ///
    /// - A modified (hacked) copy of the game can't go online (Android's "is this the genuine app" check).
    /// - Every headset watches the other players: a kart or walker that moves impossibly fast for several checks
    ///   in a row gets flagged. Flagged players can't push you, and moderators see a warning next to them in the
    ///   mod menu so they can kick or ban them.
    /// - Elsewhere: bumps are capped (no launching people into space), building is rate-limited, and rude names
    ///   sent by a hacked game are shown as "Player".
    /// </summary>
    public class AntiCheat : MonoBehaviour
    {
        /// <summary>True if this copy of the game was changed after it was built.</summary>
        public static bool Tampered { get; private set; }

        const float CheckEvery = 0.5f;
        const float MaxKartSpeed = 55f;   // m/s; the fastest kart with a boost is well under this
        const float MaxWalkSpeed = 18f;   // m/s; running is 8
        const int StrikesToFlag = 3;

        class Track { public Vector3 pos; public int fast, strikes; }
        static readonly Dictionary<Object, Track> tracks = new Dictionary<Object, Track>();
        static readonly Dictionary<string, int> strikesById = new Dictionary<string, int>();
        float nextCheck;

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Application.genuineCheckAvailable && !Application.genuine)
            {
                Tampered = true;
                Debug.LogWarning("[KartVR] This copy of the game was modified; online play is off.");
            }
#endif
        }

        /// <summary>How many times this player was caught moving impossibly fast (this session).</summary>
        public static int Strikes(string playerId) => playerId != null && strikesById.TryGetValue(playerId, out int s) ? s : 0;
        public static bool Flagged(string playerId) => Strikes(playerId) >= StrikesToFlag;

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckEvery;
            foreach (var kart in KartController.All)
            {
                if (kart == null || !kart.IsReady || kart.IsLocal) continue;
                Check(kart, kart.transform.position, MaxKartSpeed, kart.PlayerId.ToString());
                // A flagged player's kart can't push you around.
                if (Flagged(kart.PlayerId.ToString()))
                    foreach (var c in kart.GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                if (avatar == null || !avatar.IsReady || avatar.IsLocal || !avatar.InHub) continue;
                Check(avatar, avatar.transform.position, MaxWalkSpeed, IdOf(avatar));
            }
        }

        static string IdOf(PlayerAvatar avatar)
        {
            foreach (var k in KartController.All)
                if (k != null && k.IsReady && avatar.Object != null && k.Object.StateAuthority == avatar.Object.StateAuthority) return k.PlayerId.ToString();
            return null;
        }

        static void Check(Object who, Vector3 pos, float maxSpeed, string playerId)
        {
            if (!tracks.TryGetValue(who, out var t)) { tracks[who] = new Track { pos = pos }; return; }
            float speed = (pos - t.pos).magnitude / CheckEvery;
            t.pos = pos;
            // One big jump is a respawn or teleport (fine). Several fast checks in a row is a speed hack.
            if (speed > maxSpeed && speed < 400f) t.fast++;
            else t.fast = 0;
            if (t.fast < 3) return;
            t.fast = 0;
            if (string.IsNullOrEmpty(playerId)) return;
            strikesById[playerId] = Strikes(playerId) + 1;
            Debug.LogWarning("[KartVR] Anti-cheat: " + playerId + " moved at " + speed.ToString("0") + " m/s (strike " + strikesById[playerId] + ")");
        }
    }
}
