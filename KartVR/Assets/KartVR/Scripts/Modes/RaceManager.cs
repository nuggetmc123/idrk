using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using UnityEngine;

namespace KartVR
{
    public enum RaceState : byte { Idle, Countdown, Racing, Results }

    /// <summary>
    /// Runs the race loop while the session is in Race mode:
    /// Countdown -> Racing -> Results -> next race. Lives on the GameState object;
    /// the master client drives the state, every kart tracks its own laps.
    /// </summary>
    public class RaceManager : NetworkBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public int totalLaps = 3;
        public float countdownSeconds = 5f;
        public float resultsSeconds = 12f;
        [Tooltip("After the first kart finishes, everyone else has this long.")]
        public float finishGraceSeconds = 30f;

        const float MaxRaceSeconds = 3600f;

        [Networked] public RaceState State { get; set; }
        [Networked] public int RaceId { get; set; }
        [Networked] TickTimer Phase { get; set; }
        [Networked] TickTimer RaceClock { get; set; }

        Checkpoint[] allCheckpoints = new Checkpoint[0];
        Checkpoint[] mapCheckpoints = new Checkpoint[0];
        KartMode checkpointsFor = KartMode.Hub;

        /// <summary>The checkpoints of the race map the session is on, in order.</summary>
        Checkpoint[] checkpoints
        {
            get
            {
                var mode = GameState.CurrentMode;
                if (mode != checkpointsFor)
                {
                    checkpointsFor = mode;
                    mapCheckpoints = allCheckpoints.Where(c => c != null && c.map == mode).OrderBy(c => c.index).ToArray();
                }
                return mapCheckpoints;
            }
        }
        public int CheckpointCount => checkpoints.Length;
        public bool IsReady => Object != null && Object.IsValid;

        public float PhaseRemaining => IsReady ? Phase.RemainingTime(Runner) ?? 0f : 0f;
        public float Elapsed => IsReady && RaceClock.IsRunning
            ? MaxRaceSeconds - (RaceClock.RemainingTime(Runner) ?? 0f)
            : 0f;

        void Awake()
        {
            allCheckpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        }

        public override void Spawned() { Instance = this; }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        public Checkpoint GetCheckpoint(int index)
        {
            if (checkpoints.Length == 0) return null;
            return checkpoints[((index % checkpoints.Length) + checkpoints.Length) % checkpoints.Length];
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (!KartModeInfo.IsRace(GameState.CurrentMode))
            {
                if (State != RaceState.Idle) State = RaceState.Idle;
                return;
            }

            switch (State)
            {
                case RaceState.Idle:
                    StartCountdown();
                    break;

                case RaceState.Countdown:
                    if (Phase.Expired(Runner))
                    {
                        State = RaceState.Racing;
                        RaceClock = TickTimer.CreateFromSeconds(Runner, MaxRaceSeconds);
                        Phase = TickTimer.None;
                    }
                    break;

                case RaceState.Racing:
                    var racers = KartController.All.Where(k => k.IsReady).ToList();
                    int finished = racers.Count(k => k.Finished);
                    bool everyoneDone = racers.Count > 0 && finished == racers.Count;
                    if (finished > 0 && !Phase.IsRunning)
                        Phase = TickTimer.CreateFromSeconds(Runner, finishGraceSeconds);
                    if (everyoneDone || Phase.Expired(Runner) || racers.Count == 0)
                    {
                        State = RaceState.Results;
                        Phase = TickTimer.CreateFromSeconds(Runner, resultsSeconds);
                    }
                    break;

                case RaceState.Results:
                    if (Phase.Expired(Runner)) StartCountdown();
                    break;
            }
        }

        void StartCountdown()
        {
            RaceId++;
            State = RaceState.Countdown;
            Phase = TickTimer.CreateFromSeconds(Runner, countdownSeconds);
            RaceClock = TickTimer.None;
        }

        /// <summary>How far round the course a kart is; higher = further ahead.</summary>
        public float Progress(KartController kart)
        {
            int n = Mathf.Max(1, checkpoints.Length);
            if (kart.Lap <= 0) return 0f;
            int passedThisLap = kart.NextCheckpoint == 0 ? n : kart.NextCheckpoint;
            float score = (kart.Lap - 1) * n + passedThisLap;
            var next = GetCheckpoint(kart.NextCheckpoint);
            if (next != null)
            {
                // Tie-break by distance to the next checkpoint (closer = further ahead).
                float d = Vector3.Distance(kart.transform.position, next.transform.position);
                score += 1f - Mathf.Clamp01(d / 200f);
            }
            return score;
        }

        public List<KartController> Standings()
        {
            return KartController.All
                .Where(k => k.IsReady)
                .OrderByDescending(k => k.Finished)
                .ThenBy(k => k.Finished ? k.FinishTime : 0f)
                .ThenByDescending(k => Progress(k))
                .ToList();
        }

        public int PositionOf(KartController kart)
        {
            return Standings().IndexOf(kart) + 1;
        }

        public string StandingsText(int max = 8)
        {
            var sb = new StringBuilder();
            var list = Standings();
            for (int i = 0; i < list.Count && i < max; i++)
            {
                var k = list[i];
                sb.Append(i + 1).Append(". ").Append(k.DisplayName);
                if (k.Finished) sb.Append("  ").Append(KartUI.FormatTime(k.FinishTime));
                else if (State == RaceState.Results) sb.Append("  DNF");
                else sb.Append("  Lap ").Append(Mathf.Max(1, k.Lap)).Append('/').Append(totalLaps);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
