using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Player level. XP comes from raids: every raider you kill (bosses are worth a lot), getting out alive, and how
    /// much loot you bring out. Traders only sell a gun (or a better sight / attachment / bag) once you're high
    /// enough level for it - anything you find in a raid is yours to use whatever your level.
    /// </summary>
    public static class Levels
    {
        public const int Max = 30;
        public const int KillXp = 60, BossXp = 600, ExtractXp = 250;

        /// <summary>Total XP needed to reach a level (level 1 = 0).</summary>
        public static int XpFor(int level)
        {
            level = Mathf.Clamp(level, 1, Max);
            int xp = 0;
            for (int l = 1; l < level; l++) xp += 400 + 250 * (l - 1);   // 400, 650, 900 ... per level
            return xp;
        }

        public static int LevelOf(int xp)
        {
            int l = 1;
            while (l < Max && xp >= XpFor(l + 1)) l++;
            return l;
        }

        public static int Current => Profile.Current != null ? LevelOf(Profile.Current.xp) : 1;

        /// <summary>"Level 4  (1,230 / 1,950 XP)"</summary>
        public static string Describe(int xp)
        {
            int l = LevelOf(xp);
            return l >= Max ? "Level " + l + " (max)" : "Level " + l + "  (" + xp.ToString("N0") + " / " + XpFor(l + 1).ToString("N0") + " XP)";
        }

        /// <summary>Give XP; announces a level up.</summary>
        public static void Award(int amount, string why = null)
        {
            var p = Profile.Current;
            if (p == null || amount <= 0) return;
            int before = LevelOf(p.xp);
            p.xp += amount;
            int after = LevelOf(p.xp);
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (after > before)
            {
                Sfx.UI(Synth.Extracted(), 0.6f);
                pl.Toast("LEVEL UP!  Level " + after + (Unlocks(after) > 0 ? "  -  " + Unlocks(after) + " new item(s) at the traders" : ""));
            }
            else if (why != null) pl.Toast("+" + amount + " XP  " + why);
        }

        /// <summary>The level a trader wants before they'll sell you this. Guns by rarity and size; gear by rarity.</summary>
        public static int Required(ItemDef d)
        {
            if (d == null) return 1;
            if (d is GunDef g)
            {
                int tier = g.rarity == Rarity.Common ? 0 : g.rarity == Rarity.Uncommon ? 1 : g.rarity == Rarity.Rare ? 2 : 3;
                int size = g.cls == GunClass.Pistol || g.cls == GunClass.Revolver ? 0 : g.cls == GunClass.SMG || g.cls == GunClass.Shotgun ? 1
                         : g.cls == GunClass.Rifle ? 2 : g.cls == GunClass.LMG ? 4 : 3;   // battle rifles / DMRs / snipers 3
                if (tier == 0) return size == 0 ? 1 : 1 + size;           // 1 .. 5
                if (tier == 1) return 5 + size;                           // 5 .. 9
                if (tier == 2) return 10 + size * 2;                      // 10 .. 18
                return 20 + size;
            }
            if (d.kind == ItemKind.Sight || d.kind == ItemKind.Attachment || d.kind == ItemKind.Backpack || d.kind == ItemKind.Armor)
                return d.rarity == Rarity.Common ? 1 : d.rarity == Rarity.Uncommon ? 4 : d.rarity == Rarity.Rare ? 9 : 15;
            return 1;   // ammo, meds, grenades, knives
        }

        static int Unlocks(int level)
        {
            int n = 0;
            foreach (var t in TraderStall.Defaults()) foreach (var id in t.stock) if (Required(Db.Get(id)) == level) n++;
            return n;
        }
    }
}
