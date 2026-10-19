using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Your coins and the fighters you own, saved on this headset. You earn coins by fighting (landing hits, KOs,
    /// streaks) and spend them on crates in the main menu; each crate unlocks a fighter you don't have yet.
    /// Brawler and Speedster are free. Moderators have every fighter. No real money anywhere: all fighters are
    /// balanced by body type, so a rare fighter looks cooler but isn't stronger.
    /// </summary>
    public static class Collection
    {
        const string CoinsKey = "nb.coins", OwnedKey = "nb.owned", StartedKey = "nb.started";
        public const int StartingCoins = 300;

        public struct CrateType
        {
            public string name;
            public int cost;
            /// <summary>Chance of each rarity (Common, Rare, Epic, Legendary, Mythic), out of 100.</summary>
            public float[] odds;
            public Color color;
        }

        public static readonly CrateType[] Crates =
        {
            new CrateType { name = "FIGHTER CRATE", cost = 150, odds = new[] { 55f, 28f, 12f, 4.5f, 0.5f }, color = new Color(0.3f, 0.65f, 1f) },
            new CrateType { name = "ELITE CRATE", cost = 500, odds = new[] { 0f, 50f, 32f, 15f, 3f }, color = new Color(1f, 0.7f, 0.15f) },
        };

        /// <summary>Coins back when a crate has nothing new to give (by the rarity rolled).</summary>
        static readonly int[] DuplicateRefund = { 60, 120, 220, 400, 800 };

        public static event System.Action Changed;
        static float fraction;

        static void FirstRun()
        {
            if (PlayerPrefs.GetInt(StartedKey, 0) == 1) return;
            PlayerPrefs.SetInt(StartedKey, 1);
            PlayerPrefs.SetInt(CoinsKey, StartingCoins); // enough for two crates straight away
            PlayerPrefs.Save();
        }

        public static int Coins
        {
            get { FirstRun(); return PlayerPrefs.GetInt(CoinsKey, 0); }
            private set { PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static void Earn(int coins)
        {
            if (coins <= 0) return;
            Coins += coins;
        }

        /// <summary>For small amounts per hit: they add up and pay out whole coins.</summary>
        public static void EarnFractional(float coins)
        {
            fraction += Mathf.Max(0f, coins);
            if (fraction < 1f) return;
            int whole = Mathf.FloorToInt(fraction);
            fraction -= whole;
            Earn(whole);
        }

        static bool Owned(int id)
        {
            string owned = PlayerPrefs.GetString(OwnedKey, "");
            return id >= 0 && id < owned.Length && owned[id] == '1';
        }

        public static bool IsUnlocked(int id) => id == 0 || id == 1 || Moderation.IAmMod || Owned(id);

        public static int UnlockedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < FighterInfo.Count; i++) if (IsUnlocked(i)) n++;
                return n;
            }
        }

        static void Unlock(int id)
        {
            var chars = PlayerPrefs.GetString(OwnedKey, "").PadRight(FighterInfo.Count, '0').ToCharArray();
            chars[id] = '1';
            PlayerPrefs.SetString(OwnedKey, new string(chars));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public struct CrateResult
        {
            public bool opened;
            public int fighter;      // -1 if you already had everything of that rarity and got coins instead
            public Rarity rarity;
            public int refund;
            public string problem;
        }

        /// <summary>Spend coins on a crate. Rolls a rarity, then a fighter of that rarity you don't own yet.</summary>
        public static CrateResult Open(int crateIndex)
        {
            var crate = Crates[Mathf.Clamp(crateIndex, 0, Crates.Length - 1)];
            if (Coins < crate.cost) return new CrateResult { problem = "Not enough coins (" + crate.cost + " needed). Fight to earn more!" };
            Coins -= crate.cost;

            float roll = Random.value * 100f, sum = 0f;
            int rarity = 0;
            for (int i = 0; i < crate.odds.Length; i++)
            {
                sum += crate.odds[i];
                if (roll < sum) { rarity = i; break; }
                rarity = i;
            }
            while (rarity < crate.odds.Length && crate.odds[rarity] <= 0f) rarity++;

            var candidates = new List<int>();
            for (int i = 0; i < FighterInfo.Count; i++)
                if ((int)FighterInfo.Get(i).rarity == rarity && !Owned(i) && i > 1) candidates.Add(i);
            if (candidates.Count == 0)
            {
                // Nothing new at that rarity: try any fighter you don't have yet, else coins back.
                for (int i = 2; i < FighterInfo.Count; i++) if (!Owned(i)) candidates.Add(i);
                if (candidates.Count == 0)
                {
                    int refund = DuplicateRefund[Mathf.Clamp(rarity, 0, DuplicateRefund.Length - 1)];
                    Earn(refund);
                    return new CrateResult { opened = true, fighter = -1, rarity = (Rarity)rarity, refund = refund };
                }
            }
            int pick = candidates[Random.Range(0, candidates.Count)];
            Unlock(pick);
            return new CrateResult { opened = true, fighter = pick, rarity = FighterInfo.Get(pick).rarity };
        }
    }
}
