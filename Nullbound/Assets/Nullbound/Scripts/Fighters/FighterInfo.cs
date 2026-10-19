using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// The playable fighters. Two are in so far (the core combat comes first); Elementalist, Psychic, Shadow
    /// Fighter and Titan get added here later with their own specials.
    /// </summary>
    public enum FighterKind : byte { Brawler = 0, Speedster = 1 }

    /// <summary>Stats and look for each fighter. Balanced trade-offs: the hard hitter is slow, the fast one is fragile.</summary>
    public struct FighterStats
    {
        public string name, style, special, specialHow, strengths, weakness;
        public Color main, accent;
        public float maxHealth;
        /// <summary>Punch damage and knockback multiplier.</summary>
        public float power;
        /// <summary>Seconds between punches from the same hand.</summary>
        public float punchCooldown;
        public float moveSpeed, jumpSpeed;
        public float dashSpeed, dashTime, dashCooldown;
        public float specialCooldown;
        /// <summary>How much knockback this fighter takes (heavier = less).</summary>
        public float knockbackTaken;
    }

    public static class FighterInfo
    {
        public const int Count = 2;

        static readonly FighterStats[] All =
        {
            new FighterStats
            {
                name = "BRAWLER", style = "Heavy punches, ground slams, big knockback",
                special = "GROUND SLAM", specialHow = "Press X: leap up and smash the ground. Launches everyone near you.",
                strengths = "Hits hardest, tough, hard to knock away", weakness = "Slow to move and dash",
                main = new Color(0.95f, 0.32f, 0.18f), accent = new Color(1f, 0.78f, 0.25f),
                maxHealth = 130f, power = 1.3f, punchCooldown = 0.38f, moveSpeed = 3.6f, jumpSpeed = 5.6f,
                dashSpeed = 15f, dashTime = 0.18f, dashCooldown = 1.4f, specialCooldown = 9f, knockbackTaken = 0.75f,
            },
            new FighterStats
            {
                name = "SPEEDSTER", style = "Lightning-fast punches, teleport dodges, rapid combos",
                special = "FLASH STEP", specialHow = "Press X: teleport behind the enemy you're looking at. Your next punch hits double.",
                strengths = "Fastest punches, moves and dashes", weakness = "Low health, gets launched further",
                main = new Color(0.2f, 0.8f, 1f), accent = new Color(1f, 0.95f, 0.3f),
                maxHealth = 90f, power = 0.85f, punchCooldown = 0.2f, moveSpeed = 5.2f, jumpSpeed = 6.4f,
                dashSpeed = 22f, dashTime = 0.16f, dashCooldown = 0.75f, specialCooldown = 6f, knockbackTaken = 1.25f,
            },
        };

        public static FighterStats Get(FighterKind kind) => All[Mathf.Clamp((int)kind, 0, All.Length - 1)];
    }
}
