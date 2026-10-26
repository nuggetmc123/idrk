using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>A season of races with points for the top ten (25-18-15-12-10-8-6-4-2-1), saved between sessions.</summary>
    public static class Championship
    {
        public static ChampionshipState S => SaveData.D.championship;

        public static void Start(List<int> calendar, int laps, int difficulty, int opponents)
        {
            opponents = Mathf.Min(opponents, RaceFinder.MaxRacers - 1);
            var s = new ChampionshipState { active = true, calendar = new List<int>(calendar), round = 0, laps = laps, difficulty = difficulty, opponents = opponents };
            s.names.Add(PlayerName);
            s.points.Add(0);
            for (int i = 0; i < opponents; i++) { s.names.Add(Teams.DriverName(i)); s.points.Add(0); }
            s.playerIndex = 0;
            SaveData.D.championship = s;
            SaveData.Write();
        }

        public static string PlayerName => SaveData.Profile.created ? SaveData.Profile.DisplayName : string.IsNullOrEmpty(Settings.PlayerName) ? "You" : Settings.PlayerName;

        public static int CurrentMap => S.active && S.round < S.calendar.Count ? S.calendar[S.round] : 0;
        public static bool Finished => S.active && S.round >= S.calendar.Count;

        public static void AddResults(RaceSession session)
        {
            var s = S;
            if (!s.active) return;
            foreach (var e in session.entrants)
            {
                if (e.position < 1 || e.position > RaceSession.Points.Length || e.eliminated || e.dnf) continue;
                string n = e.isPlayer ? PlayerName : e.name;
                int idx = s.names.IndexOf(n);
                if (e.isPlayer) idx = s.playerIndex;
                if (idx < 0) { s.names.Add(n); s.points.Add(0); idx = s.names.Count - 1; }
                int pts = RaceSession.Points[e.position - 1];
                // A point for the fastest lap (if in the top ten).
                if (session.fastestLapBy == e.name && e.position <= 10) pts += 1;
                s.points[idx] += pts;
            }
            var winner = session.entrants.Count > 0 ? session.entrants[0] : null;
            s.roundWinners.Add(winner != null ? (winner.isPlayer ? PlayerName : winner.name) : "-");
            s.round++;
            if (s.round >= s.calendar.Count)
            {
                var order = Standings();
                bool won = order.Count > 0 && order[0].Key == s.playerIndex;
                if (won) SaveData.D.championshipsWon++;
                SaveData.D.championshipHistory.Add(System.DateTime.Now.ToString("yyyy-MM-dd") + "  " + s.names[order[0].Key] + " (" + order[0].Value + " pts)" + (won ? "  - CHAMPION" : "  - you: P" + (order.FindIndex(p => p.Key == s.playerIndex) + 1)));
            }
            SaveData.Write();
        }

        /// <summary>(driver index, points) sorted by points.</summary>
        public static List<KeyValuePair<int, int>> Standings()
        {
            var list = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < S.names.Count; i++) list.Add(new KeyValuePair<int, int>(i, S.points[i]));
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }

        public static void End() { S.active = false; SaveData.Write(); }

        public static List<int> DefaultCalendar(int rounds)
        {
            var all = new List<int>();
            for (int i = 0; i < 15; i++) all.Add(i);
            var r = new System.Random();
            for (int i = all.Count - 1; i > 0; i--) { int j = r.Next(i + 1); int t = all[i]; all[i] = all[j]; all[j] = t; }
            return all.GetRange(0, Mathf.Clamp(rounds, 1, 15));
        }
    }
}
