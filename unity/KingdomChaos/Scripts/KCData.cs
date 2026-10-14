using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomChaos
{
    // All the tuning numbers live here, so balancing never means digging through game logic.
    // su = startup, ac = active, re = recovery (seconds).
    public class AtkData
    {
        public float dmg, su, ac, re, range, kb, launch, stun, pop, shake, speed, slow;
        public int spread = 1, pierce;
        public bool breaks;
    }

    public class WeaponDef
    {
        public string key, name, rarity, kind, desc, stat;
        public int price, combo;
        public AtkData light, heavy;
    }

    public class PetDef
    {
        public string key, name, rarity, proj, desc;
        public int price;
        public float cd, dmg, burnDps, burnT;
        public Color color;
    }

    public class EnemyDef
    {
        public string name;
        public float hp, w, h, d, spd = 100;
        public int xp, coins;
        public bool mini, armor, shield, boss, prop;
    }

    public class WaveDef
    {
        public float x;
        public string[] list;
        public bool mini, boss;
    }

    public class StageDef
    {
        public string id, name;
        public float len;
        public float[] crates;
        public WaveDef[] waves;
        public bool boss, training;
    }

    [Serializable]
    public class SaveData
    {
        public int v = 1;
        public int level = 1;
        public int xp;
        public int coins;
        public List<string> weapons = new List<string> { "iron" };
        public string weapon = "iron";
        public List<string> pets = new List<string>();
        public string pet = "";
        public List<string> cleared = new List<string>();
        public int kills;
        public int bestCombo;
    }

    public static class KCData
    {
        public const float PetFuryCd = 20f;

        public static readonly WeaponDef[] Weapons =
        {
            new WeaponDef
            {
                key = "iron", name = "Iron Sword", rarity = "Common", price = 0, kind = "melee", combo = 3,
                desc = "Balanced speed and damage. Three-hit combo, heavy launches enemies for air juggles.",
                stat = "20 per hit · heavy 40",
                light = new AtkData { dmg = 20, su = .06f, ac = .09f, re = .13f, range = 74, kb = 110 },
                heavy = new AtkData { dmg = 40, su = .2f, ac = .1f, re = .26f, range = 84, kb = 260, launch = 560, breaks = true },
            },
            new WeaponDef
            {
                key = "hammer", name = "Titan Hammer", rarity = "Rare", price = 250, kind = "melee", combo = 2,
                desc = "Slow swings with huge knockback. Every hit smashes shields; heavy stuns smaller enemies.",
                stat = "30 per hit · heavy 55 + stun",
                light = new AtkData { dmg = 30, su = .15f, ac = .11f, re = .22f, range = 86, kb = 240, breaks = true },
                heavy = new AtkData { dmg = 55, su = .36f, ac = .12f, re = .32f, range = 98, kb = 440, launch = 380, stun = 1.1f, breaks = true, shake = 8 },
            },
            new WeaponDef
            {
                key = "bow", name = "Frost Bow", rarity = "Rare", price = 350, kind = "ranged", combo = 1,
                desc = "Arrows slow enemies for 1.5 s. Heavy fires a three-arrow spread that pierces.",
                stat = "25 per arrow · heavy 3 × 20",
                light = new AtkData { dmg = 25, su = .07f, ac = 0, re = .22f, speed = 820, slow = 1.5f },
                heavy = new AtkData { dmg = 20, su = .26f, ac = 0, re = .36f, speed = 760, slow = 1.5f, spread = 3, pierce = 1, breaks = true },
            },
        };

        public static readonly PetDef[] Pets =
        {
            new PetDef
            {
                key = "slime", name = "Slime Buddy", rarity = "Common", price = 120, cd = 10, dmg = 12, proj = "blob",
                color = KCPainter.Hex("#6fe06a"), desc = "Spits a blob for 12 damage every 10 s.",
            },
            new PetDef
            {
                key = "fox", name = "Ember Fox", rarity = "Rare", price = 300, cd = 8, dmg = 20, proj = "fire",
                color = KCPainter.Hex("#ff8a3d"), burnDps = 3, burnT = 3,
                desc = "Fire attack for 20 damage, then burns for 3 per second for 3 s. Every 8 s.",
            },
        };

        public static readonly Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>
        {
            { "slime",     new EnemyDef { name = "Green Slime",    hp = 40,  w = 20, h = 30,  d = 12, xp = 8,   coins = 3 } },
            { "minislime", new EnemyDef { name = "Slimelet",       hp = 25,  w = 14, h = 22,  d = 10, xp = 4,   coins = 1 } },
            { "scout",     new EnemyDef { name = "Goblin Scout",   hp = 60,  w = 16, h = 64,  d = 12, xp = 12,  coins = 5, spd = 125 } },
            { "archer",    new EnemyDef { name = "Goblin Archer",  hp = 45,  w = 16, h = 64,  d = 12, xp = 12,  coins = 5, spd = 115 } },
            { "boar",      new EnemyDef { name = "Thorn Boar",     hp = 100, w = 34, h = 44,  d = 16, xp = 20,  coins = 8, spd = 95 } },
            { "captain",   new EnemyDef { name = "Goblin Captain", hp = 380, w = 24, h = 88,  d = 16, xp = 80,  coins = 60, spd = 105, mini = true, armor = true, shield = true } },
            { "king",      new EnemyDef { name = "King Slime",     hp = 600, w = 74, h = 120, d = 42, xp = 220, coins = 150, boss = true, armor = true } },
            { "dummy",     new EnemyDef { name = "Dummy",          hp = 1e9f, w = 18, h = 72, d = 14, prop = true } },
            { "crate",     new EnemyDef { name = "Crate",          hp = 1,   w = 20, h = 40,  d = 14, prop = true } },
        };

        public static readonly StageDef[] Stages =
        {
            new StageDef
            {
                id = "1-1", name = "Forest Path", len = 3000, crates = new float[] { 620, 1500, 2300 },
                waves = new[]
                {
                    new WaveDef { x = 900,  list = new[] { "slime", "slime", "slime" } },
                    new WaveDef { x = 1750, list = new[] { "slime", "scout", "scout", "slime" } },
                    new WaveDef { x = 2500, list = new[] { "scout", "slime", "scout", "slime", "scout" } },
                },
            },
            new StageDef
            {
                id = "1-2", name = "Goblin Camp", len = 3300, crates = new float[] { 500, 1300, 2100, 2900 },
                waves = new[]
                {
                    new WaveDef { x = 900,  list = new[] { "scout", "scout", "archer" } },
                    new WaveDef { x = 1750, list = new[] { "slime", "slime", "archer", "slime", "archer" } },
                    new WaveDef { x = 2650, list = new[] { "scout", "archer", "scout", "scout", "archer" } },
                },
            },
            new StageDef
            {
                id = "1-3", name = "Boar Run", len = 3300, crates = new float[] { 700, 1500, 2400 },
                waves = new[]
                {
                    new WaveDef { x = 900,  list = new[] { "boar", "scout", "scout" } },
                    new WaveDef { x = 1750, list = new[] { "slime", "slime", "archer", "slime", "slime" } },
                    new WaveDef { x = 2650, list = new[] { "boar", "scout", "archer", "boar", "scout" } },
                },
            },
            new StageDef
            {
                id = "1-4", name = "Captain's Gate", len = 3100, crates = new float[] { 600, 1400, 2200 },
                waves = new[]
                {
                    new WaveDef { x = 900,  list = new[] { "scout", "scout", "archer", "archer" } },
                    new WaveDef { x = 1700, list = new[] { "boar", "slime", "slime", "slime", "scout" } },
                    new WaveDef { x = 2500, list = new[] { "captain" }, mini = true },
                },
            },
            new StageDef
            {
                id = "1-5", name = "The Slime Throne", len = 1500, crates = new float[] { 420 }, boss = true,
                waves = new[] { new WaveDef { x = 1000, list = new[] { "king" }, boss = true } },
            },
        };

        public static readonly StageDef Training = new StageDef
        {
            id = "T", name = "Training Room", len = 960, crates = new float[0], waves = new WaveDef[0], training = true,
        };

        public static readonly string[,] FutureWorlds =
        {
            { "Forgotten Graveyard", "Bone King" },
            { "Dustbite Desert", "Sand Wyrm" },
            { "Frostfall Peaks", "Frost Giant" },
            { "Emberforge", "Magma Titan" },
            { "Moonshade Woods", "Witch of Thorns" },
            { "Blackthorn Castle", "Dark Champion" },
            { "The Void Realm", "Void Emperor" },
        };

        public static WeaponDef Weapon(string key)
        {
            foreach (var w in Weapons) if (w.key == key) return w;
            return Weapons[0];
        }

        public static PetDef Pet(string key)
        {
            foreach (var p in Pets) if (p.key == key) return p;
            return null;
        }

        public static Color RarityColor(string r)
        {
            switch (r)
            {
                case "Rare": return KCPainter.Hex("#5c8cff");
                case "Epic": return KCPainter.Hex("#b473ff");
                case "Legendary": return KCPainter.Hex("#ffb13b");
                default: return KCPainter.Hex("#c9c3d6");
            }
        }

        public static int XpNeed(int lv) => 60 + lv * 40;
        public static float MaxHpFor(int lv) => 100 + (lv - 1) * 10;
    }
}
