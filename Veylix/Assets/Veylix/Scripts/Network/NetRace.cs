using Fusion;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The room's race state, owned by the master client: lobby -> countdown -> racing -> finished. Everyone's race
    /// session follows it, so the lights go out at the same moment for every player. If the host leaves, Fusion hands
    /// ownership to someone else and the race carries on.
    /// </summary>
    public class NetRace : NetworkBehaviour
    {
        public const int Lobby = 0, Countdown = 1, Racing = 2, Finished = 3;
        [Networked] public int Phase { get; set; }
        [Networked] public int Map { get; set; }
        [Networked] public int Laps { get; set; }
        [Networked] public TickTimer Timer { get; set; }
        [Networked] public int RaceNumber { get; set; }

        public static NetRace I { get; private set; }
        int seenPhase = -1, seenRace = -1;

        public override void Spawned()
        {
            I = this;
            if (Object.HasStateAuthority && Phase == Lobby && Laps == 0)
            {
                Map = Online.I != null ? Online.I.roomMap : 0;
                Laps = Online.I != null ? Online.I.roomLaps : 5;
                Timer = TickTimer.CreateFromSeconds(Runner, 45f);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) { if (I == this) I = null; }

        public bool IsMaster => Object != null && Object.HasStateAuthority;

        /// <summary>Host only: start the race now.</summary>
        public void StartRace()
        {
            if (!IsMaster || Phase != Lobby) return;
            Phase = Countdown;
            Timer = TickTimer.CreateFromSeconds(Runner, 6.5f);
        }

        public void NewRace()
        {
            if (!IsMaster) return;
            Phase = Lobby;
            RaceNumber++;
            Timer = TickTimer.CreateFromSeconds(Runner, 30f);
        }

        public override void FixedUpdateNetwork()
        {
            if (!IsMaster) return;
            int players = 0;
            foreach (var c in Online.Cars) if (c != null && c.Ready) players++;
            switch (Phase)
            {
                case Lobby:
                    // Auto-start once at least two drivers are in and the lobby timer has run out.
                    if (players >= 2 && Timer.Expired(Runner)) StartRace();
                    break;
                case Countdown:
                    if (Timer.Expired(Runner)) { Phase = Racing; Timer = TickTimer.None; }
                    break;
                case Racing:
                    bool anyFinished = false, allDone = true;
                    foreach (var c in Online.Cars) { if (c == null) continue; if (c.Finished) anyFinished = true; else allDone = false; }
                    if (anyFinished && allDone) { Phase = Finished; Timer = TickTimer.CreateFromSeconds(Runner, 25f); }
                    break;
                case Finished:
                    if (Timer.Expired(Runner)) NewRace();
                    break;
            }
        }

        public override void Render()
        {
            var s = RaceSession.I;
            if (s == null || !s.online) return;
            if (RaceNumber != seenRace && seenRace >= 0 && Phase == Lobby)
            {
                // A new race in the same room: back to the grid.
                seenRace = RaceNumber;
                if (Game.I != null) Game.I.Restart();
                return;
            }
            seenRace = RaceNumber;
            if (Phase == seenPhase) return;
            seenPhase = Phase;
            if (Phase == Countdown) s.RemoteCountdown();
            else if (Phase == Racing) s.RemoteGo();
            else if (Phase == Finished) s.ShowResults();
        }

        public float SecondsLeft => Timer.RemainingTime(Runner) ?? 0f;
    }
}
