using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Player level. XP comes from raids: every raider you kill (bosses are worth a lot), getting out alive, and how
    /// much loot you bring out. Better gear at the traders unlocks as you level up (Required), and costs more.
    /// </summary>
    public static class Levels
    {
        public const int Max = 30;
        public const int KillXp = 60, BossXp = 600, ExtractXp = 250, HeadshotXp = 50;
        /// <summary>This player's id in kill credit (1 offline; their network id + 1 online).</summary>
        public static int MyKillerId => NetGame.Live ? (int)(NetHub.Backend.LocalId % 100000) + 1 : 1;

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

        public static int Current => Profile.Current == null ? 1 : Spectator.IsTestAccount ? 9999 : LevelOf(Profile.Current.xp);   // the test account is level 9999

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
            amount *= LiveEvent.Mult(LiveEvent.DoubleXp);   // live event
            int before = LevelOf(p.xp);
            p.xp += amount;
            int after = LevelOf(p.xp);
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (after > before)
            {
                Sfx.UI(Synth.Extracted(), 0.6f);
                pl.Toast("LEVEL UP!  Level " + after);
            }
            else if (why != null) pl.Toast("+" + amount + " XP  " + why);
        }

        /// <summary>The level a trader wants before they'll sell you this: one ladder for guns, sights, attachments,
        /// armour, bags and NVGs - common at level 1, then every rarity 5 levels further. Ammo, meds, grenades and
        /// knives are always for sale. (The better it is, the more it costs too: TraderStall.Price.)</summary>
        public static int Required(ItemDef d)
        {
            if (d == null) return 1;
            if (d is GunDef || d.kind == ItemKind.Sight || d.kind == ItemKind.Attachment || d.kind == ItemKind.Backpack || d.kind == ItemKind.Armor || d.id == "nvg")
                return d.rarity == Rarity.Common ? 1 : d.rarity == Rarity.Uncommon ? 5 : d.rarity == Rarity.Rare ? 10 : d.rarity == Rarity.Epic ? 15 : 20;
            return 1;
        }

    }
}
