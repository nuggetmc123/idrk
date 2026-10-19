using UnityEngine;

namespace Nullbound
{
    public enum MoveKind
    {
        Projectile,   // fires one or more shots along your aim
        DashStrike,   // you rocket forward and hit everyone you pass through
        Slam,         // shockwave around you (optionally after a wind-up)
        Launcher,     // close-range hit that throws people into the air
        Beam,         // a long laser along your aim, ticks damage
        Teleport,     // blink behind the enemy you are looking at and hit them
        Buff,         // power yourself up for a few seconds
        Meteor,       // a strike falls where you aim after a warning circle
        Pull,         // yank enemies in front of you towards you
        Flurry,       // teleport onto the nearest enemy and land a chain of hits
    }

    public enum BuffKind { None = 0, Armor = 1, Speed = 2, Power = 3, Veil = 4 }

    public class MoveDef
    {
        public string name;
        public MoveKind kind;
        public float cooldown = 8f;
        public float damage = 8f;
        public float range = 10f;      // distance / beam length / dash length
        public float radius = 1.5f;    // hit radius
        public float knock = 8f;       // horizontal knockback speed
        public float lift = 2f;        // upward knockback speed
        public float stun = 0.6f;      // seconds the victim can't act
        public float speed = 30f;      // projectile speed
        public int count = 1;          // projectiles / flurry hits
        public float spread = 0f;      // degrees between projectiles
        public float delay = 0f;       // wind-up before it lands
        public float duration = 0.2f;  // dash time / beam time / buff time
        public float explode = 0f;     // projectile splash radius
        public BuffKind buff = BuffKind.None;
        public bool unblockable;
        public bool guardBreak;
        public string hint = "";
    }

    public class FighterDef
    {
        public string name, title, blurb;
        public Color main, accent;
        public float speed = 1f;
        public float punch = 1f;
        public MoveDef[] moves;
        public MoveDef ult;
    }

    /// The roster. Every fighter is an original character; the move system is generic, so a new
    /// fighter is just another entry here.
    public static class NBFighters
    {
        public static readonly FighterDef[] All =
        {
            new FighterDef
            {
                name = "KAIZEN", title = "The Unbroken Fist",
                blurb = "A street brawler who trained until the ground cracked. Simple, brutal, reliable.",
                main = new Color(0.95f, 0.75f, 0.15f), accent = new Color(0.9f, 0.2f, 0.15f), punch = 1.15f,
                moves = new[]
                {
                    new MoveDef { name = "HAMMER RUSH", kind = MoveKind.DashStrike, cooldown = 9, damage = 10, range = 12, radius = 1.5f, knock = 14, lift = 3, stun = 0.8f, duration = 0.28f, guardBreak = true, hint = "Rocket forward fist-first" },
                    new MoveDef { name = "RISING COMET", kind = MoveKind.Launcher, cooldown = 10, damage = 9, range = 3f, radius = 1.4f, knock = 2, lift = 15, stun = 1.2f, hint = "Uppercut that launches" },
                    new MoveDef { name = "QUAKE STOMP", kind = MoveKind.Slam, cooldown = 12, damage = 11, radius = 6, knock = 10, lift = 7, stun = 0.9f, delay = 0.15f, hint = "Shockwave around you" },
                    new MoveDef { name = "SHOCK PALM", kind = MoveKind.Projectile, cooldown = 8, damage = 9, radius = 1.0f, speed = 26, range = 40, knock = 12, lift = 3, stun = 0.6f, hint = "Air-pressure blast" },
                },
                ult = new MoveDef { name = "ZERO HOUR", kind = MoveKind.Beam, cooldown = 1, damage = 4, range = 34, radius = 1.6f, knock = 6, lift = 2, stun = 0.4f, delay = 0.45f, duration = 1.6f, unblockable = true, hint = "A beam that erases the horizon" },
            },
            new FighterDef
            {
                name = "VOLTEX", title = "Storm Engine",
                blurb = "A rogue combat cyborg running on stolen lightning. Fast shots, faster knees.",
                main = new Color(0.2f, 0.75f, 1f), accent = new Color(0.95f, 0.95f, 1f), speed = 1.08f,
                moves = new[]
                {
                    new MoveDef { name = "ARC BOLTS", kind = MoveKind.Projectile, cooldown = 6, damage = 4, radius = 0.45f, speed = 48, range = 45, count = 3, spread = 7, knock = 4, lift = 1, stun = 0.35f, hint = "Three fast bolts" },
                    new MoveDef { name = "THRUSTER KNEE", kind = MoveKind.DashStrike, cooldown = 8, damage = 9, range = 14, radius = 1.4f, knock = 8, lift = 9, stun = 0.9f, duration = 0.24f, hint = "Jet dash that launches" },
                    new MoveDef { name = "STATIC FIELD", kind = MoveKind.Slam, cooldown = 13, damage = 7, radius = 5, knock = 3, lift = 1, stun = 1.6f, delay = 0.2f, hint = "Stuns everyone close" },
                    new MoveDef { name = "OVERCLOCK", kind = MoveKind.Buff, cooldown = 18, buff = BuffKind.Speed, duration = 6, hint = "Move and dash much faster" },
                },
                ult = new MoveDef { name = "ION CANNON", kind = MoveKind.Meteor, cooldown = 1, damage = 36, range = 40, radius = 7, knock = 16, lift = 12, stun = 1.4f, delay = 1.1f, unblockable = true, hint = "Orbital strike where you aim" },
            },
            new FighterDef
            {
                name = "NYX", title = "Shadow Between Seconds",
                blurb = "An assassin who is always behind you. Low health, high pressure.",
                main = new Color(0.55f, 0.25f, 0.95f), accent = new Color(0.1f, 0.05f, 0.15f), speed = 1.12f, punch = 0.95f,
                moves = new[]
                {
                    new MoveDef { name = "BLINK STRIKE", kind = MoveKind.Teleport, cooldown = 9, damage = 8, range = 22, radius = 1.6f, knock = 6, lift = 4, stun = 0.9f, hint = "Teleport behind who you look at" },
                    new MoveDef { name = "STAR FAN", kind = MoveKind.Projectile, cooldown = 6, damage = 3, radius = 0.35f, speed = 40, range = 35, count = 5, spread = 9, knock = 3, lift = 0.5f, stun = 0.3f, hint = "Five throwing stars" },
                    new MoveDef { name = "CRESCENT KICK", kind = MoveKind.Launcher, cooldown = 9, damage = 8, range = 3.2f, radius = 1.6f, knock = 4, lift = 14, stun = 1.1f, guardBreak = true, hint = "Breaks guard and launches" },
                    new MoveDef { name = "VEIL", kind = MoveKind.Buff, cooldown = 20, buff = BuffKind.Veil, duration = 4, hint = "Turn invisible and quick" },
                },
                ult = new MoveDef { name = "THOUSAND CUTS", kind = MoveKind.Flurry, cooldown = 1, damage = 3.5f, range = 30, radius = 2f, count = 9, knock = 18, lift = 10, stun = 0.5f, duration = 1.2f, unblockable = true, hint = "Become a blur of blades" },
            },
            new FighterDef
            {
                name = "GOLIATH", title = "Living Landslide",
                blurb = "A mountain that learned to walk. Slow, armored, and hits like an avalanche.",
                main = new Color(0.55f, 0.42f, 0.3f), accent = new Color(0.3f, 0.85f, 0.4f), speed = 0.9f, punch = 1.3f,
                moves = new[]
                {
                    new MoveDef { name = "BOULDER TOSS", kind = MoveKind.Projectile, cooldown = 10, damage = 14, radius = 1.4f, speed = 20, range = 40, knock = 15, lift = 5, stun = 0.9f, explode = 3f, hint = "Throw a huge rock" },
                    new MoveDef { name = "BULL CHARGE", kind = MoveKind.DashStrike, cooldown = 10, damage = 12, range = 15, radius = 1.8f, knock = 18, lift = 4, stun = 1f, duration = 0.42f, guardBreak = true, hint = "Unstoppable charge" },
                    new MoveDef { name = "EARTHSPLITTER", kind = MoveKind.Slam, cooldown = 13, damage = 13, radius = 8, knock = 7, lift = 11, stun = 1.1f, delay = 0.35f, hint = "Huge ground slam" },
                    new MoveDef { name = "IRON HIDE", kind = MoveKind.Buff, cooldown = 20, buff = BuffKind.Armor, duration = 5, hint = "Take less damage, can't be stunned" },
                },
                ult = new MoveDef { name = "MOUNTAIN FALL", kind = MoveKind.Meteor, cooldown = 1, damage = 42, range = 32, radius = 9, knock = 20, lift = 14, stun = 1.6f, delay = 1.3f, unblockable = true, hint = "Drop a meteor" },
            },
            new FighterDef
            {
                name = "RIME", title = "Monk of the Still Winter",
                blurb = "Freezes the fight in place. Every hit slows the enemy down.",
                main = new Color(0.65f, 0.95f, 1f), accent = new Color(0.2f, 0.35f, 0.8f),
                moves = new[]
                {
                    new MoveDef { name = "ICE LANCE", kind = MoveKind.Projectile, cooldown = 7, damage = 8, radius = 0.5f, speed = 50, range = 50, knock = 5, lift = 1, stun = 1.0f, hint = "Freezing spear" },
                    new MoveDef { name = "FROST STEP", kind = MoveKind.DashStrike, cooldown = 8, damage = 6, range = 10, radius = 1.6f, knock = 3, lift = 1, stun = 1.3f, duration = 0.2f, hint = "Dash that freezes" },
                    new MoveDef { name = "GLACIAL PULL", kind = MoveKind.Pull, cooldown = 11, damage = 5, range = 15, radius = 4f, knock = 16, lift = 3, stun = 0.8f, hint = "Drag enemies to you" },
                    new MoveDef { name = "BLIZZARD RING", kind = MoveKind.Slam, cooldown = 13, damage = 8, radius = 6, knock = 4, lift = 2, stun = 1.6f, delay = 0.2f, hint = "Freeze everyone around you" },
                },
                ult = new MoveDef { name = "ABSOLUTE ZERO", kind = MoveKind.Slam, cooldown = 1, damage = 30, radius = 15, knock = 8, lift = 6, stun = 3.2f, delay = 0.6f, unblockable = true, hint = "Freeze the whole arena" },
            },
            new FighterDef
            {
                name = "EMBER", title = "Dancer in the Furnace",
                blurb = "Turns every fight into a fire show. Explosions everywhere.",
                main = new Color(1f, 0.4f, 0.1f), accent = new Color(1f, 0.85f, 0.3f), speed = 1.04f,
                moves = new[]
                {
                    new MoveDef { name = "FIREBALL", kind = MoveKind.Projectile, cooldown = 7, damage = 9, radius = 0.6f, speed = 32, range = 45, knock = 9, lift = 4, stun = 0.6f, explode = 3.5f, hint = "Exploding fireball" },
                    new MoveDef { name = "FLAME DASH", kind = MoveKind.DashStrike, cooldown = 8, damage = 8, range = 13, radius = 1.5f, knock = 10, lift = 3, stun = 0.7f, duration = 0.24f, hint = "Burning dash" },
                    new MoveDef { name = "INFERNO PILLAR", kind = MoveKind.Meteor, cooldown = 11, damage = 12, range = 24, radius = 3.2f, knock = 4, lift = 16, stun = 1f, delay = 0.65f, hint = "Fire erupts where you aim" },
                    new MoveDef { name = "HEAT SURGE", kind = MoveKind.Buff, cooldown = 18, buff = BuffKind.Power, duration = 6, hint = "Hit 40% harder" },
                },
                ult = new MoveDef { name = "SUPERNOVA", kind = MoveKind.Slam, cooldown = 1, damage = 38, radius = 13, knock = 26, lift = 12, stun = 1.5f, delay = 0.9f, unblockable = true, hint = "Explode like a star" },
            },
        };

        public static FighterDef Get(int id) => All[Mathf.Clamp(id, 0, All.Length - 1)];

        // ---- shared "battlegrounds" rules ----
        public const float MaxHealth = 100f;
        public const float UltMax = 100f;
        public const float AwakenTime = 20f;
        public const float RespawnTime = 4f;
        public const float DashCooldown = 2f;
        public const float EvasiveCooldown = 14f;
        public const float M1Damage = 3.2f;
        public const float M1Stun = 0.5f;
        public const float ComboWindow = 1.3f;
        public const float FinisherCooldown = 1.1f;
        public const float UppercutCooldown = 4f;
        public const float DownslamCooldown = 4f;
    }
}
