using System.Linq;
using System.Text;
using Fusion;

namespace KartVR
{
    public enum BumperState : byte { Idle, Playing, Results }

    /// <summary>
    /// Bumper kart rounds: ram people for points (+1 per solid hit), fall off
    /// the arena and lose a point. Highest score when the timer ends wins.
    /// </summary>
    public class BumperManager : NetworkBehaviour
    {
        public static BumperManager Instance { get; private set; }

        public float roundSeconds = 120f;
        public float resultsSeconds = 10f;

        [Networked] public BumperState State { get; set; }
        [Networked] public int RoundId { get; set; }
        [Networked] TickTimer Phase { get; set; }

        public bool IsReady => Object != null && Object.IsValid;
        public float PhaseRemaining => IsReady ? Phase.RemainingTime(Runner) ?? 0f : 0f;

        public override void Spawned() { Instance = this; }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (!KartModeInfo.IsBumper(GameState.CurrentMode))
            {
                if (State != BumperState.Idle) State = BumperState.Idle;
                return;
            }

            switch (State)
            {
                case BumperState.Idle:
                    StartRound();
                    break;
                case BumperState.Playing:
                    if (Phase.Expired(Runner))
                    {
                        State = BumperState.Results;
                        Phase = TickTimer.CreateFromSeconds(Runner, resultsSeconds);
                    }
                    break;
                case BumperState.Results:
                    if (Phase.Expired(Runner)) StartRound();
                    break;
            }
        }

        void StartRound()
        {
            RoundId++;
            State = BumperState.Playing;
            Phase = TickTimer.CreateFromSeconds(Runner, roundSeconds);
        }

        public string StandingsText(int max = 8)
        {
            var sb = new StringBuilder();
            var list = KartController.All.Where(k => k.IsReady).OrderByDescending(k => k.Score).ToList();
            for (int i = 0; i < list.Count && i < max; i++)
                sb.Append(i + 1).Append(". ").Append(list[i].Nickname.ToString()).Append("  ").Append(list[i].Score).Append('\n');
            return sb.ToString();
        }
    }
}
