using System;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Your progress, saved on this headset: XP and level, the daily login streak, three new daily challenges
    /// every day, and your best lap on each track. Everything you do earns XP; levelling up unlocks underglow
    /// and trails for your kart (see <see cref="KartParts.UnderglowLevel"/>). The pop-ups are in <see cref="ProgressFx"/>.
    /// </summary>
    public static class Progress
    {
        public enum Goal { FinishRaces, WinRace, DriftSeconds, DriveKm, BumperHits, Tags, BoostPads, BigAir }

        struct Challenge
        {
            public Goal goal;
            public float target;
            public string text;
            public int xp;
        }

        static readonly Challenge[] Pool =
        {
            new Challenge { goal = Goal.FinishRaces, target = 2, text = "Finish 2 races", xp = 150 },
            new Challenge { goal = Goal.WinRace, target = 1, text = "Win a race", xp = 250 },
            new Challenge { goal = Goal.DriftSeconds, target = 30, text = "Drift for 30 seconds", xp = 150 },
            new Challenge { goal = Goal.DriveKm, target = 5, text = "Drive 5 km", xp = 150 },
            new Challenge { goal = Goal.BumperHits, target = 10, text = "Bump karts 10 times in Bumper", xp = 200 },
            new Challenge { goal = Goal.Tags, target = 3, text = "Tag 3 people", xp = 200 },
            new Challenge { goal = Goal.BoostPads, target = 10, text = "Hit 10 boost pads", xp = 120 },
            new Challenge { goal = Goal.BigAir, target = 5, text = "Get 5 big jumps", xp = 150 },
        };

        const string XpKey = "kartvr.xp", StreakKey = "kartvr.streak", LastDayKey = "kartvr.lastDay", DayKey = "kartvr.challengeDay";

        /// <summary>XP gained (amount, what for; what-for is null for quiet little amounts).</summary>
        public static event Action<int, string> XpGained;
        public static event Action<int> LeveledUp;
        public static event Action<string> ChallengeDone;
        public static event Action<string> Record;

        public static int TotalXp => PlayerPrefs.GetInt(XpKey, 0);
        public static int Streak => PlayerPrefs.GetInt(StreakKey, 0);

        /// <summary>Total XP needed to reach a level: 100 for Lv 2, 300 for Lv 3, 600 for Lv 4... (each level a bit longer).</summary>
        public static int XpForLevel(int level) => 50 * level * (level - 1);

        public static int Level
        {
            get
            {
                int xp = TotalXp, level = 1;
                while (level < 99 && xp >= XpForLevel(level + 1)) level++;
                return level;
            }
        }

        /// <summary>How far through the current level you are (0..1).</summary>
        public static float LevelProgress
        {
            get
            {
                int l = Level, a = XpForLevel(l), b = XpForLevel(l + 1);
                return Mathf.Clamp01((TotalXp - a) / (float)Mathf.Max(1, b - a));
            }
        }

        public static void Add(int xp, string why = null)
        {
            if (xp <= 0) return;
            int before = Level;
            PlayerPrefs.SetInt(XpKey, TotalXp + xp);
            if (why != null) PlayerPrefs.Save();
            XpGained?.Invoke(xp, why);
            int after = Level;
            if (after > before) LeveledUp?.Invoke(after);
        }

        // ---------------- daily streak + challenges

        static string Today => DateTime.Now.ToString("yyyyMMdd");

        /// <summary>Called once when the game starts: the daily login bonus (bigger for a longer streak, up to 7 days).</summary>
        public static void CheckDaily()
        {
            string today = Today, last = PlayerPrefs.GetString(LastDayKey, "");
            if (last == today) return;
            string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
            int streak = last == yesterday ? Streak + 1 : 1;
            PlayerPrefs.SetInt(StreakKey, streak);
            PlayerPrefs.SetString(LastDayKey, today);
            PlayerPrefs.Save();
            Add(50 * Mathf.Min(streak, 7), streak > 1 ? "Day " + streak + " streak bonus!" : "Daily bonus!");
        }

        /// <summary>Today's three challenges (the same for everyone on the same day).</summary>
        static string cachedDay;
        static int[] cachedPicks;

        public static int[] TodaysChallenges
        {
            get
            {
                if (cachedDay == Today && cachedPicks != null) return cachedPicks;
                var rng = new System.Random(int.Parse(Today));
                var picks = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    int p;
                    do p = rng.Next(Pool.Length); while (Array.IndexOf(picks, p, 0, i) >= 0);
                    picks[i] = p;
                }
                if (PlayerPrefs.GetString(DayKey, "") != Today)
                {
                    PlayerPrefs.SetString(DayKey, Today);
                    for (int i = 0; i < 3; i++) { PlayerPrefs.DeleteKey("kartvr.ch" + i); PlayerPrefs.DeleteKey("kartvr.chDone" + i); }
                }
                cachedDay = Today;
                cachedPicks = picks;
                return picks;
            }
        }

        public static string ChallengeText(int slot, out float progress, out float target, out bool done, out int xp)
        {
            var c = Pool[TodaysChallenges[slot]];
            progress = PlayerPrefs.GetFloat("kartvr.ch" + slot, 0f);
            target = c.target;
            done = PlayerPrefs.GetInt("kartvr.chDone" + slot, 0) == 1;
            xp = c.xp;
            return c.text;
        }

        /// <summary>Something happened that a challenge might count (a drift second, a kilometre, a tag...).</summary>
        public static void Count(Goal goal, float amount = 1f)
        {
            var picks = TodaysChallenges;
            for (int i = 0; i < 3; i++)
            {
                var c = Pool[picks[i]];
                if (c.goal != goal || PlayerPrefs.GetInt("kartvr.chDone" + i, 0) == 1) continue;
                float p = PlayerPrefs.GetFloat("kartvr.ch" + i, 0f) + amount;
                PlayerPrefs.SetFloat("kartvr.ch" + i, p);
                if (p >= c.target)
                {
                    PlayerPrefs.SetInt("kartvr.chDone" + i, 1);
                    PlayerPrefs.Save();
                    ChallengeDone?.Invoke(c.text);
                    Add(c.xp, "Challenge: " + c.text);
                }
            }
        }

        // ---------------- races and records

        public static float BestLap(KartMode track) => PlayerPrefs.GetFloat("kartvr.bestLap." + (int)track, 0f);

        public static void LapDone(KartMode track, float seconds)
        {
            if (seconds < 5f) return;
            float best = BestLap(track);
            if (best <= 0f || seconds < best)
            {
                PlayerPrefs.SetFloat("kartvr.bestLap." + (int)track, seconds);
                PlayerPrefs.Save();
                if (best > 0f) { Record?.Invoke("NEW LAP RECORD  " + KartUI.FormatTime(seconds)); Add(50, "New lap record!"); }
            }
        }

        public static void RaceFinished(int position, int racers)
        {
            Count(Goal.FinishRaces);
            int xp = Mathf.Max(30, 120 - 20 * (position - 1));
            Add(xp, "Finished P" + position);
            if (position == 1 && racers > 1) { Count(Goal.WinRace); Add(80, "Race win!"); }
            PlayerPrefs.Save();
        }

        /// <summary>Little XP that adds up (driving, drifting) without a pop-up for every bit.</summary>
        static float quietXp;
        public static void Trickle(float xp)
        {
            quietXp += xp;
            if (quietXp < 1f) return;
            int whole = Mathf.FloorToInt(quietXp);
            quietXp -= whole;
            Add(whole);
        }
    }
}
