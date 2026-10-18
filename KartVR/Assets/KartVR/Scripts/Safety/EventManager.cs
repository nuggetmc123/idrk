using System.Linq;
using Fusion;
using UnityEngine;

namespace KartVR
{
    public enum LiveEvent : byte { None, DiscoSingAlong, LastKartStanding, LowGravity, TurboFrenzy, BallRain }

    /// <summary>
    /// Live events a moderator starts from the Mod Menu for everyone on the server:
    /// Disco Sing-Along (party song, lyrics, disco lights), Last Kart Standing (a red ring closes in; get caught
    /// outside it and you're out; last kart in wins), Low Gravity, Turbo Frenzy (everyone's much faster) and Ball
    /// Rain (beach balls fall from the sky). The effects themselves are in <see cref="LiveEventFx"/>.
    /// </summary>
    public class EventManager : NetworkBehaviour
    {
        public static EventManager Instance { get; private set; }

        public static readonly string[] Names = { "", "Disco Sing-Along", "Last Kart Standing", "Low Gravity", "Turbo Frenzy", "Ball Rain" };
        static readonly int[] Seconds = { 0, 120, 150, 60, 60, 60 };
        /// <summary>Last Kart Standing: how long the ring takes to close, and how small it gets.</summary>
        public const float RingCloseSeconds = 100f, RingMinRadius = 10f;

        [Networked] public LiveEvent Current { get; set; }
        [Networked] public int EventId { get; set; }
        [Networked] int StartedAt { get; set; }   // unix seconds
        [Networked] int EndsAt { get; set; }
        [Networked] public Vector3 Center { get; set; }
        [Networked] public float StartRadius { get; set; }
        [Networked] public NetworkString<_16> Winner { get; set; }

        public bool IsReady => Object != null && Object.IsValid;

        public static LiveEvent Running => Instance != null && Instance.IsReady ? Instance.Current : LiveEvent.None;
        public static bool Active(LiveEvent e) => Running == e;

        static double Now => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        public float Elapsed => IsReady ? (float)(Now - StartedAt) : 0f;
        public float Remaining => IsReady ? Mathf.Max(0f, (float)(EndsAt - Now)) : 0f;

        /// <summary>Last Kart Standing: the ring's radius right now.</summary>
        public float RingRadius => Mathf.Lerp(StartRadius, RingMinRadius, Mathf.Clamp01(Elapsed / RingCloseSeconds));

        /// <summary>Turbo Frenzy: how much faster karts go.</summary>
        public static float SpeedMultiplier => Active(LiveEvent.TurboFrenzy) ? 1.6f : 1f;

        public override void Spawned() { Instance = this; }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        // ---------------- moderator side

        public void StartEvent(LiveEvent e)
        {
            if (!IsReady || !Moderation.IAmMod) return;
            if (e == LiveEvent.LastKartStanding && GameState.CurrentMode == KartMode.Hub)
            {
                SessionManager.Instance?.SetStatus("Start Last Kart Standing on a map with karts (not the hub)", 5f);
                return;
            }
            Vector3 here = KartRig.Instance != null ? KartRig.Instance.head.position : Vector3.zero;
            RPC_Start(e, here);
        }

        public void StopEvent()
        {
            if (IsReady && Moderation.IAmMod) RPC_Stop();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Start(LiveEvent e, Vector3 modPosition, RpcInfo info = default)
        {
            if (!Moderation.IsVerifiedModPlayer(info.Source) || e == LiveEvent.None) return;
            if (e == LiveEvent.LastKartStanding && GameState.CurrentMode == KartMode.Hub) return; // karts are parked in the hub
            var map = KartMaps.Get(GameState.CurrentMode);
            Current = e;
            EventId++;
            StartedAt = (int)Now;
            EndsAt = StartedAt + Seconds[(int)e];
            Winner = "";
            // The shrinking ring is centred on the map, as big as the map allows.
            Center = map.center;
            StartRadius = Mathf.Clamp(map.radius - 12f, 25f, 130f);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Stop(RpcInfo info = default)
        {
            if (!Moderation.IsVerifiedModPlayer(info.Source)) return;
            Current = LiveEvent.None;
        }

        // ---------------- the session owner runs the clock

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Current == LiveEvent.None) return;
            if (Now >= EndsAt) { Current = LiveEvent.None; return; }

            if (Current == LiveEvent.LastKartStanding && Winner.ToString().Length == 0)
            {
                var karts = KartController.All.Where(k => k != null && k.IsReady).ToList();
                var alive = karts.Where(k => !k.Eliminated).ToList();
                bool over = karts.Count >= 2 ? alive.Count <= 1 : alive.Count == 0;
                if (over)
                {
                    Winner = alive.Count == 1 ? alive[0].DisplayName : "Nobody";
                    EndsAt = (int)Now + 8; // show the winner, then back to normal
                }
            }
        }
    }
}
