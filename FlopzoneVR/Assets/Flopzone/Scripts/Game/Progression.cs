using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Levels, XP, skill points, the skill tree and ranked points. Saved on this headset.
    /// XP only comes from public and ranked matches (never private ones, see <see cref="Game"/>).
    /// </summary>
    public static class Progression
    {
        /* ---------------- the skill tree ---------------- */

        public class Node
        {
            public string id, name, branch, requires;
            public int cost;
            public string gun;                 // a gun id this unlocks
            public Slot slot; public int option = -1;   // or an attachment
        }

        public static readonly string[] Branches = { "Pistols", "SMGs", "Shotguns", "Rifles", "Snipers", "Attachments" };

        /// <summary>Guns everyone has from the start.</summary>
        public static readonly string[] Starters = { "flop9", "hornet", "boom", "rattle", "longshot" };

        public static readonly Node[] Tree = BuildTree();

        static Node[] BuildTree()
        {
            var l = new List<Node>();
            void G(string branch, string gun, int cost, string after)
            {
                var d = GunCatalog.ById(gun);
                l.Add(new Node { id = gun, name = d != null ? d.name : gun, branch = branch, cost = cost, gun = gun, requires = after });
            }
            void A(string id, Slot s, int v, int cost, string after)
                => l.Add(new Node { id = id, name = Attachments.Name(s, v), branch = "Attachments", cost = cost, slot = s, option = v, requires = after });

            G("Pistols", "squeak", 1, null);
            G("Pistols", "deagle", 2, "squeak");
            G("Pistols", "classic", 2, "squeak");
            G("Pistols", "hammer", 2, "classic");
            G("Pistols", "revolt", 3, "deagle");
            G("Pistols", "buzz", 3, "hammer");

            G("SMGs", "slab", 1, null);
            G("SMGs", "spitfire", 2, "slab");
            G("SMGs", "bullhorn", 3, "spitfire");

            G("Shotguns", "sawed", 1, null);
            G("Shotguns", "slug", 2, "sawed");
            G("Shotguns", "thunder", 2, "sawed");
            G("Shotguns", "twintube", 2, "thunder");
            G("Shotguns", "brute", 3, "twintube");

            G("Rifles", "viper", 1, null);
            G("Rifles", "gecko", 2, "viper");
            G("Rifles", "bulldog", 2, "viper");
            G("Rifles", "bravo", 3, "gecko");
            G("Rifles", "hmr", 3, "bravo");

            G("Snipers", "marksman", 1, null);
            G("Snipers", "mosin", 2, "marksman");
            G("Snipers", "whisper", 2, "marksman");
            G("Snipers", "needle", 2, "mosin");
            G("Snipers", "elephant", 3, "needle");
            G("Snipers", "fiftycal", 4, "elephant");

            A("a_reddot", Slot.Optic, 1, 1, null);
            A("a_holo", Slot.Optic, 2, 1, "a_reddot");
            A("a_acog", Slot.Optic, 3, 2, "a_holo");
            A("a_grip", Slot.Under, 1, 1, null);
            A("a_laser", Slot.Under, 2, 1, "a_grip");
            A("a_comp", Slot.Muzzle, 2, 1, null);
            A("a_supp", Slot.Muzzle, 1, 2, "a_comp");
            A("a_extmag", Slot.Mag, 1, 2, null);
            return l.ToArray();
        }

        public static Node Find(string id) => Array.Find(Tree, n => n.id == id);

        /* ---------------- saved data ---------------- */

        [Serializable]
        class Data
        {
            public int level = 1, xp, points, totalXp;
            public List<string> owned = new List<string>();
            public int rp, rankedWins, rankedLosses;
        }

        const string Key = "fz.progress.v1";
        static Data d;

        static Data D
        {
            get
            {
                if (d != null) return d;
                try { d = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key, "")); } catch { d = null; }
                if (d == null) d = new Data();
                if (d.owned == null) d.owned = new List<string>();
                d.level = Mathf.Max(1, d.level);
                return d;
            }
        }

        static void Store()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Level, XP, points, unlocks or rank changed.</summary>
        public static event Action Changed;

        public static int Level => D.level;
        public static int Xp => D.xp;
        public static int Points => D.points;
        public static int XpToNext(int level) => 400 + 100 * (level - 1);
        public static int XpToNext() => XpToNext(D.level);

        /// <summary>Skill points for reaching a level: one, plus a bonus one every fifth level.</summary>
        static int PointsFor(int level) => level % 5 == 0 ? 2 : 1;

        /* ---------------- unlocks ---------------- */

        public static bool Owns(string nodeId) => D.owned.Contains(nodeId);

        public static bool HasGun(int index)
        {
            var g = GunCatalog.Get(index);
            if (g == null) return false;
            if (Array.IndexOf(Starters, g.id) >= 0) return true;
            var n = Find(g.id);
            return n == null || Owns(n.id);   // a gun that isn't on the tree is free
        }

        public static bool HasAttachment(Slot s, int v)
        {
            if (v == 0) return true;
            foreach (var n in Tree) if (n.option == v && n.slot == s && n.option >= 0) return Owns(n.id);
            return true;
        }

        public enum NodeState { Owned, Available, NeedsPoints, Locked }

        public static NodeState State(Node n)
        {
            if (Owns(n.id)) return NodeState.Owned;
            if (n.requires != null && !Owns(n.requires)) return NodeState.Locked;
            return D.points >= n.cost ? NodeState.Available : NodeState.NeedsPoints;
        }

        public static bool Unlock(Node n)
        {
            if (State(n) != NodeState.Available) return false;
            D.points -= n.cost;
            D.owned.Add(n.id);
            Store();
            return true;
        }

        /// <summary>Make sure the saved loadout only uses guns you have.</summary>
        public static void FixLoadout()
        {
            var st = Save.Settings;
            if (!HasGun(st.primaryGun)) st.primaryGun = GunCatalog.ById("rattle").index;
            if (!HasGun(st.sidearmGun)) st.sidearmGun = GunCatalog.ById("flop9").index;
        }

        /* ---------------- XP ---------------- */

        /// <summary>Fires with the new level when you level up.</summary>
        public static event Action<int> LeveledUp;

        /// <summary>XP earned this match (shown at the end).</summary>
        public static int MatchXp { get; private set; }

        public static void StartMatch() => MatchXp = 0;

        public static void AddXp(int amount)
        {
            if (amount <= 0) return;
            MatchXp += amount;
            D.xp += amount;
            D.totalXp += amount;
            while (D.xp >= XpToNext(D.level))
            {
                D.xp -= XpToNext(D.level);
                D.level++;
                D.points += PointsFor(D.level);
                LeveledUp?.Invoke(D.level);
            }
            Store();
        }

        /* ---------------- ranked ---------------- */

        public const int RankedMinLevel = 5;
        public static int Rp => D.rp;
        public static int RankedWins => D.rankedWins;
        public static int RankedLosses => D.rankedLosses;

        static readonly (string name, int rp, string color)[] Tiers =
        {
            ("Bronze", 0, "#cd7f32"), ("Silver", 200, "#c0c6cc"), ("Gold", 500, "#ffcc33"),
            ("Platinum", 900, "#6fe3d2"), ("Diamond", 1400, "#7fb8ff"), ("Champion", 2000, "#ff5ad1"),
        };

        public static string Tier(int rp)
        {
            string t = Tiers[0].name;
            foreach (var x in Tiers) if (rp >= x.rp) t = x.name;
            return t;
        }

        public static Color TierColor(int rp)
        {
            string hex = Tiers[0].color;
            foreach (var x in Tiers) if (rp >= x.rp) hex = x.color;
            return Prims.Hex(hex.TrimStart('#'));
        }

        /// <summary>Points to the next tier (0 at the top).</summary>
        public static int ToNextTier(int rp)
        {
            foreach (var x in Tiers) if (rp < x.rp) return x.rp - rp;
            return 0;
        }

        /// <summary>A ranked match finished: +25 for a win, -20 for a loss (never below 0). Returns the change.</summary>
        public static int RankedResult(bool won, bool draw)
        {
            if (draw) return 0;
            int before = D.rp;
            if (won) { D.rp += 25; D.rankedWins++; }
            else { D.rp = Mathf.Max(0, D.rp - 20); D.rankedLosses++; }
            Store();
            return D.rp - before;
        }
    }
}
