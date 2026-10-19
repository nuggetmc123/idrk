using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Which fighter (0..<see cref="FighterInfo.Count"/>-1). The first two have names here; the rest are just numbers
    /// into <see cref="FighterInfo"/>'s roster.
    /// </summary>
    public enum FighterKind : byte { Brawler = 0, Speedster = 1 }

    public enum Rarity : byte { Common, Rare, Epic, Legendary, Mythic }
    /// <summary>Body build: sets the fighter's stats (so rarity is only looks and style, never power).</summary>
    public enum BodyType : byte { Normal, Slim, Bulky, Giant, Tiny, Robot, Beast, Spirit }
    public enum HeadType : byte { Round, Box, Helmet, Hood, Skull, Mask }
    public enum TopType : byte { None, Spikes, Mohawk, Horns, Crown, Halo, Antennae, Ears, Ponytail, Flames }
    public enum BackType : byte { None, Cape, Wings, Tail, Jetpack, Blades, Orbs, Spines }
    public enum GloveType : byte { Normal, Big, Claws, Glow, Metal }
    public enum SpecialType : byte
    {
        GroundSlam, FlashStep, Fireball, IceShard, Lightning, Shockwave, Telekinesis, ShadowStrike,
        Clones, TitanQuake, Uppercut, Whirlwind, Beam, Meteor, Heal, Barrier,
    }

    /// <summary>Everything about one fighter: look, stats and special.</summary>
    public struct FighterStats
    {
        public string name, style, special, specialHow, strengths, weakness;
        public Rarity rarity;
        public BodyType body;
        public HeadType head;
        public TopType top;
        public BackType back;
        public GloveType glove;
        public SpecialType specialType;
        public Color main, accent, eyes;
        public float maxHealth, power, punchCooldown, moveSpeed, jumpSpeed;
        public float dashSpeed, dashTime, dashCooldown, specialCooldown, knockbackTaken;
        /// <summary>How big the body is drawn (giants and tiny fighters).</summary>
        public float scale;
        /// <summary>How hard this fighter's special (super) hits, on top of power. 1 = normal.</summary>
        public float superPower;
        // Model look (see FighterRecipe): pants / lining colour, skin, hair, outfit style and extras.
        public Color secondary, skin, hair;
        public Outfit outfit;
        public bool outfitSet, scarf, shoulderPads;
    }

    public static class FighterInfo
    {
        public static int Count => Roster.Length;

        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.75f, 0.78f, 0.82f),
            Rarity.Rare => new Color(0.3f, 0.65f, 1f),
            Rarity.Epic => new Color(0.75f, 0.35f, 1f),
            Rarity.Legendary => new Color(1f, 0.72f, 0.15f),
            _ => new Color(1f, 0.25f, 0.4f),
        };

        public static string RarityName(Rarity r) => r.ToString().ToUpperInvariant();

        public static FighterStats Get(FighterKind kind) => Roster[Mathf.Clamp((int)kind, 0, Roster.Length - 1)];
        public static FighterStats Get(int id) => Roster[Mathf.Clamp(id, 0, Roster.Length - 1)];

        static Color C(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;

        // What each special does (cooldown, how to use it). Names can be different per fighter.
        static float SpecialCooldown(SpecialType t) => t switch
        {
            SpecialType.GroundSlam => 9f, SpecialType.FlashStep => 6f, SpecialType.Fireball => 4f, SpecialType.IceShard => 5f,
            SpecialType.Lightning => 7f, SpecialType.Shockwave => 8f, SpecialType.Telekinesis => 9f, SpecialType.ShadowStrike => 7f,
            SpecialType.Clones => 10f, SpecialType.TitanQuake => 13f, SpecialType.Uppercut => 7f, SpecialType.Whirlwind => 9f,
            SpecialType.Beam => 8f, SpecialType.Meteor => 12f, SpecialType.Heal => 16f, _ => 12f,
        };

        public static string SpecialHow(SpecialType t) => t switch
        {
            SpecialType.GroundSlam => "Leap up and smash the ground: launches everyone near you.",
            SpecialType.FlashStep => "Teleport behind the enemy you're looking at. Your next punch hits double.",
            SpecialType.Fireball => "Throw a fireball where you look. It explodes and sets them burning.",
            SpecialType.IceShard => "Fire an ice shard where you look. It freezes whoever it hits for a moment.",
            SpecialType.Lightning => "Call lightning down on the enemy you're looking at.",
            SpecialType.Shockwave => "Blast a wave of force in front of you that throws people back.",
            SpecialType.Telekinesis => "Lift the enemy you're looking at into the air and slam them down.",
            SpecialType.ShadowStrike => "Vanish and strike the enemy you're looking at from behind.",
            SpecialType.Clones => "Send out two shadow clones that rush the nearest enemies and explode.",
            SpecialType.TitanQuake => "Stomp so hard the whole area shakes: huge launch around you.",
            SpecialType.Uppercut => "Rocket forward with an uppercut that launches them sky high.",
            SpecialType.Whirlwind => "Spin like a tornado, hitting everyone around you again and again.",
            SpecialType.Beam => "Charge for a moment, then fire an energy beam straight ahead.",
            SpecialType.Meteor => "Drop a meteor where you're looking. Big boom after a second.",
            SpecialType.Heal => "Heal a big chunk of health and take half damage for a few seconds.",
            _ => "Throw up a barrier: nothing gets through for a few seconds, and hits bounce back.",
        };

        static string BodyStrength(BodyType b) => b switch
        {
            BodyType.Slim => "Fast punches and moves", BodyType.Bulky => "Hits hard, tough", BodyType.Giant => "Hardest hits, toughest, barely knocked back",
            BodyType.Tiny => "Fastest of all", BodyType.Robot => "Tough and steady", BodyType.Beast => "Quick and strong",
            BodyType.Spirit => "Quick dashes", _ => "All-rounder",
        };

        static string BodyWeakness(BodyType b) => b switch
        {
            BodyType.Slim => "Less health", BodyType.Bulky => "Slower", BodyType.Giant => "Slowest, big target", BodyType.Tiny => "Fragile, flies far when hit",
            BodyType.Robot => "Slow dashes", BodyType.Beast => "Average guard", BodyType.Spirit => "Weaker punches", _ => "No big strengths",
        };

        static FighterStats F(string name, Rarity rarity, BodyType body, HeadType head, TopType top, BackType back, GloveType glove,
            string main, string accent, SpecialType special, string specialName, string style, string eyes = null)
        {
            var s = new FighterStats
            {
                name = name, rarity = rarity, body = body, head = head, top = top, back = back, glove = glove,
                main = C(main), accent = C(accent), eyes = eyes != null ? C(eyes) : C(accent),
                specialType = special, special = specialName, specialHow = "Click the right stick: " + SpecialHow(special), style = style,
                strengths = BodyStrength(body), weakness = BodyWeakness(body), specialCooldown = SpecialCooldown(special),
                jumpSpeed = 6f, dashSpeed = 18f, dashTime = 0.17f, scale = 1f, superPower = 1f,
            };
            // Default looks, picked from the name so they stay the same everywhere.
            int h = 0;
            foreach (char ch in name) h = h * 31 + ch;
            h &= 0x7fffffff;
            s.skin = C(Skins[h % Skins.Length]);
            s.hair = C(Hairs[(h / 7) % Hairs.Length]);
            s.secondary = Color.Lerp(s.main, new Color(0.08f, 0.08f, 0.1f), 0.6f);
            switch (body)
            {
                case BodyType.Slim: s.maxHealth = 95; s.power = 0.9f; s.moveSpeed = 5f; s.punchCooldown = 0.24f; s.dashCooldown = 0.9f; s.knockbackTaken = 1.15f; s.dashSpeed = 20f; break;
                case BodyType.Bulky: s.maxHealth = 125; s.power = 1.2f; s.moveSpeed = 3.8f; s.punchCooldown = 0.36f; s.dashCooldown = 1.3f; s.knockbackTaken = 0.8f; s.scale = 1.08f; break;
                case BodyType.Giant: s.maxHealth = 145; s.power = 1.35f; s.moveSpeed = 3.3f; s.punchCooldown = 0.42f; s.dashCooldown = 1.6f; s.knockbackTaken = 0.6f; s.dashSpeed = 14f; s.scale = 1.22f; s.jumpSpeed = 5.4f; break;
                case BodyType.Tiny: s.maxHealth = 85; s.power = 0.85f; s.moveSpeed = 5.6f; s.punchCooldown = 0.2f; s.dashCooldown = 0.7f; s.knockbackTaken = 1.35f; s.dashSpeed = 22f; s.scale = 0.82f; s.jumpSpeed = 6.8f; break;
                case BodyType.Robot: s.maxHealth = 120; s.power = 1.1f; s.moveSpeed = 4f; s.punchCooldown = 0.32f; s.dashCooldown = 1.2f; s.knockbackTaken = 0.85f; s.scale = 1.05f; break;
                case BodyType.Beast: s.maxHealth = 115; s.power = 1.1f; s.moveSpeed = 4.8f; s.punchCooldown = 0.27f; s.dashCooldown = 1f; s.knockbackTaken = 1f; break;
                case BodyType.Spirit: s.maxHealth = 100; s.power = 0.95f; s.moveSpeed = 4.8f; s.punchCooldown = 0.28f; s.dashCooldown = 0.8f; s.knockbackTaken = 1.1f; s.dashSpeed = 21f; break;
                default: s.maxHealth = 110; s.power = 1f; s.moveSpeed = 4.4f; s.punchCooldown = 0.3f; s.dashCooldown = 1.1f; s.knockbackTaken = 1f; break;
            }
            return s;
        }

        static readonly string[] Skins = { "f2c9a8", "e0b090", "c98a5a", "a86a42", "7a4a2c", "f6dcc8" };
        static readonly string[] Hairs = { "1a1410", "3a2416", "6a4022", "d9b46a", "1a1a22", "8a2a1a" };

        /// <summary>Per-fighter model details (anything left null keeps the default).</summary>
        static FighterStats Look(FighterStats s, Outfit? outfit = null, string skin = null, string hair = null, string secondary = null, bool scarf = false, bool pads = false)
        {
            if (outfit.HasValue) { s.outfit = outfit.Value; s.outfitSet = true; }
            if (skin != null) s.skin = C(skin);
            if (hair != null) s.hair = C(hair);
            if (secondary != null) s.secondary = C(secondary);
            s.scarf = scarf;
            s.shoulderPads = pads;
            return s;
        }

        static FighterStats Tuned(FighterStats s, float hp, float power, float punchCd, float speed, float jump, float dashSpeed, float dashCd, float kb)
        {
            s.maxHealth = hp; s.power = power; s.punchCooldown = punchCd; s.moveSpeed = speed; s.jumpSpeed = jump;
            s.dashSpeed = dashSpeed; s.dashCooldown = dashCd; s.knockbackTaken = kb;
            return s;
        }

        /// <summary>
        /// The whole roster. 0 and 1 are the starters everyone has; the rest come out of crates. Change looks, names
        /// and specials freely; stats start from the body type, then <see cref="Unbalance"/> makes every fighter different.
        /// </summary>
        static readonly FighterStats[] Roster = Unbalance(new[]
        {
            Look(Tuned(F("BRAWLER", Rarity.Common, BodyType.Bulky, HeadType.Round, TopType.Spikes, BackType.None, GloveType.Big, "f2522e", "ffc740", SpecialType.GroundSlam, "GROUND SLAM",
                "Heavy punches, ground slams, big knockback"), 130, 1.3f, 0.38f, 3.6f, 5.6f, 15f, 1.4f, 0.75f), Outfit.Tank, skin: "d9a77c", hair: "2a1a12", secondary: "34343e"),
            Look(Tuned(F("SPEEDSTER", Rarity.Common, BodyType.Slim, HeadType.Round, TopType.Spikes, BackType.None, GloveType.Normal, "33ccff", "fff24d", SpecialType.FlashStep, "FLASH STEP",
                "Lightning-fast punches, teleport dodges, rapid combos"), 90, 0.85f, 0.2f, 5.2f, 6.4f, 22f, 0.75f, 1.25f), Outfit.Suit, skin: "f2c9a8", hair: "f5e6a0", secondary: "14506a", scarf: true),
            Look(F("ELEMENTALIST", Rarity.Epic, BodyType.Normal, HeadType.Hood, TopType.Flames, BackType.Cape, GloveType.Glow, "c43a1a", "ff9a1f", SpecialType.Fireball, "FIRE BLAST", "Fire, ice and lightning in their fists"), secondary: "3a1208"),
            Look(F("PSYCHIC", Rarity.Epic, BodyType.Slim, HeadType.Round, TopType.Halo, BackType.Orbs, GloveType.Glow, "7a3cff", "ff6bd6", SpecialType.Telekinesis, "TELEKINESIS", "Moves enemies with their mind"), Outfit.Robe, skin: "f0d0c0", hair: "e8e0ff", secondary: "3a1a7a"),
            Look(F("SHADOW FIGHTER", Rarity.Legendary, BodyType.Slim, HeadType.Mask, TopType.Spikes, BackType.Cape, GloveType.Claws, "1a1424", "9d4dff", SpecialType.ShadowStrike, "SHADOW STRIKE", "Shadow dashes and surprise attacks", "c27bff"), hair: "120e18", secondary: "0d0a12", scarf: true),
            Look(F("TITAN", Rarity.Legendary, BodyType.Giant, HeadType.Helmet, TopType.Horns, BackType.Spines, GloveType.Metal, "5f6670", "ff8a2a", SpecialType.TitanQuake, "TITAN QUAKE", "Massive ground pounds and area attacks"), secondary: "2a2e34", pads: true),
            Look(F("FROSTBITE", Rarity.Rare, BodyType.Normal, HeadType.Helmet, TopType.Spikes, BackType.None, GloveType.Glow, "9fdcff", "ffffff", SpecialType.IceShard, "ICE SHARD", "Freezes enemies in their tracks"), secondary: "dff4ff"),
            Look(F("VOLTAGE", Rarity.Rare, BodyType.Slim, HeadType.Round, TopType.Mohawk, BackType.Jetpack, GloveType.Glow, "ffe033", "33e0ff", SpecialType.Lightning, "THUNDERCLAP", "Crackling with electricity"), Outfit.Suit, skin: "8a5a3c", hair: "111111", secondary: "222228"),
            Look(F("INFERNO", Rarity.Epic, BodyType.Bulky, HeadType.Skull, TopType.Flames, BackType.Wings, GloveType.Claws, "7a1010", "ff6a1a", SpecialType.Meteor, "HELLFIRE METEOR", "A demon of fire", "ffb020"), skin: "5a1010", secondary: "2a0808"),
            F("MECHA-7", Rarity.Rare, BodyType.Robot, HeadType.Box, TopType.Antennae, BackType.Jetpack, GloveType.Metal, "b8bec8", "ff3b3b", SpecialType.Beam, "LASER CANNON", "A battle robot with a chest laser"),
            Look(F("WILDCLAW", Rarity.Common, BodyType.Beast, HeadType.Round, TopType.Ears, BackType.Tail, GloveType.Claws, "8a5a32", "e8c48a", SpecialType.Whirlwind, "CLAW FRENZY", "Wild, fast and furry"), skin: "a87048", hair: "6a4022", secondary: "4a2a14"),
            Look(F("MONK", Rarity.Common, BodyType.Normal, HeadType.Round, TopType.None, BackType.None, GloveType.Normal, "ff9a1f", "7a4a1a", SpecialType.Heal, "INNER PEACE", "Calm, patient, hard to put down"), skin: "c98a5a", secondary: "7a4a1a"),
            Look(F("BULWARK", Rarity.Rare, BodyType.Bulky, HeadType.Helmet, TopType.None, BackType.Cape, GloveType.Big, "2a4fb8", "ffd24d", SpecialType.Barrier, "IRON WALL", "A living shield"), secondary: "1a2a5a", pads: true),
            Look(F("ROCKET", Rarity.Common, BodyType.Normal, HeadType.Round, TopType.Spikes, BackType.Jetpack, GloveType.Big, "3cc45a", "ffffff", SpecialType.Uppercut, "ROCKET UPPERCUT", "Jetpack-powered uppercuts"), Outfit.Suit, skin: "f2c9a8", hair: "c46a1a", secondary: "1f5a2a"),
            F("PHANTOM", Rarity.Epic, BodyType.Spirit, HeadType.Hood, TopType.None, BackType.Cape, GloveType.Glow, "9ff5f0", "1fb5a8", SpecialType.Clones, "HAUNTING", "A ghost who fights with copies of itself"),
            Look(F("SANDSTORM", Rarity.Rare, BodyType.Normal, HeadType.Mask, TopType.None, BackType.Cape, GloveType.Normal, "d9b46a", "8a5a2a", SpecialType.Whirlwind, "DUST DEVIL", "Whips up a storm of sand"), skin: "b5835a", secondary: "6a4a2a", scarf: true),
            F("STORMCALLER", Rarity.Epic, BodyType.Slim, HeadType.Hood, TopType.Halo, BackType.Orbs, GloveType.Glow, "1a2a6a", "4de0ff", SpecialType.Lightning, "STORM STRIKE", "Commands the thunder"),
            F("GEARHEAD", Rarity.Common, BodyType.Robot, HeadType.Box, TopType.None, BackType.None, GloveType.Metal, "ff8a2a", "6a6f78", SpecialType.Shockwave, "STEAM BLAST", "Bolts, steam and a short fuse"),
            Look(F("VIPER", Rarity.Rare, BodyType.Slim, HeadType.Mask, TopType.Ponytail, BackType.Tail, GloveType.Claws, "2fa84a", "111111", SpecialType.ShadowStrike, "VIPER STRIKE", "Strikes from where you aren't looking"), skin: "e0c0a0", hair: "1a3a1a", secondary: "143a1c"),
            Look(F("COLOSSUS", Rarity.Epic, BodyType.Giant, HeadType.Box, TopType.Crown, BackType.None, GloveType.Big, "b0772a", "4dd96a", SpecialType.TitanQuake, "COLOSSAL STOMP", "An ancient bronze giant"), Outfit.Armor, secondary: "5a3a14", pads: true),
            Look(F("NOVA", Rarity.Legendary, BodyType.Normal, HeadType.Helmet, TopType.Halo, BackType.Wings, GloveType.Glow, "ffffff", "ffcf33", SpecialType.Beam, "SUPERNOVA BEAM", "A star in a fighter's body"), secondary: "e8e0c0"),
            Look(F("GLACIER", Rarity.Epic, BodyType.Bulky, HeadType.Box, TopType.Spikes, BackType.Spines, GloveType.Big, "7fd0ff", "e8fbff", SpecialType.IceShard, "GLACIAL SPIKE", "Cold, slow, unstoppable"), Outfit.Armor, secondary: "bfe9ff"),
            Look(F("KITSUNE", Rarity.Legendary, BodyType.Beast, HeadType.Round, TopType.Ears, BackType.Tail, GloveType.Glow, "ff8a2a", "ffffff", SpecialType.Fireball, "FOX FIRE", "A trickster fox spirit"), Outfit.Robe, skin: "f6dcc8", hair: "ff8a2a", secondary: "c41a3a"),
            Look(F("GOLEM", Rarity.Rare, BodyType.Giant, HeadType.Box, TopType.None, BackType.None, GloveType.Big, "8a8a80", "6aa84a", SpecialType.GroundSlam, "BOULDER SLAM", "A walking mountain"), Outfit.Armor, secondary: "6a6a62"),
            Look(F("PIXIE", Rarity.Common, BodyType.Tiny, HeadType.Round, TopType.Antennae, BackType.Wings, GloveType.Normal, "ff7ad9", "b6ff4d", SpecialType.FlashStep, "PIXIE DASH", "Tiny, quick and annoying"), skin: "ffe0e8", hair: "c43a9a", secondary: "6ab030"),
            Look(F("SAMURAI", Rarity.Rare, BodyType.Normal, HeadType.Helmet, TopType.Horns, BackType.None, GloveType.Metal, "c41a1a", "1a1a1a", SpecialType.Whirlwind, "BLADE STORM", "Honour and steel"), secondary: "1a1a1a", pads: true),
            Look(F("NINJA", Rarity.Common, BodyType.Slim, HeadType.Mask, TopType.Ponytail, BackType.None, GloveType.Normal, "2a2a30", "e83030", SpecialType.ShadowStrike, "SMOKE STRIKE", "Silent and deadly"), hair: "111111", secondary: "1a1a1e"),
            Look(F("PALADIN", Rarity.Epic, BodyType.Bulky, HeadType.Helmet, TopType.Crown, BackType.Cape, GloveType.Metal, "d8dce4", "ffcf33", SpecialType.Heal, "HOLY LIGHT", "A shining knight"), secondary: "8a8e96", pads: true),
            Look(F("GRAVITY", Rarity.Legendary, BodyType.Normal, HeadType.Round, TopType.Halo, BackType.Orbs, GloveType.Glow, "6a2acc", "111111", SpecialType.Telekinesis, "SINGULARITY", "Bends gravity around them"), Outfit.Suit, skin: "c98a5a", hair: "1a1a1a", secondary: "1a0a3a"),
            Look(F("BRUTE", Rarity.Common, BodyType.Bulky, HeadType.Round, TopType.Mohawk, BackType.None, GloveType.Big, "4a9a3a", "8a3acc", SpecialType.GroundSlam, "BRUTE SMASH", "Smash first, think never"), skin: "7ab060", secondary: "3a2a4a"),
            Look(F("BLITZ", Rarity.Common, BodyType.Slim, HeadType.Round, TopType.Spikes, BackType.None, GloveType.Normal, "ffd21f", "2a5aff", SpecialType.FlashStep, "BLITZ STEP", "Gone before you see them"), Outfit.Suit, skin: "f2c9a8", hair: "2a5aff", secondary: "1a1a2a", scarf: true),
            F("REAPER", Rarity.Mythic, BodyType.Spirit, HeadType.Skull, TopType.None, BackType.Cape, GloveType.Claws, "0d0d10", "4dff6a", SpecialType.ShadowStrike, "SOUL HARVEST", "Death itself came to fight", "4dff6a"),
            Look(F("DRAGONHEART", Rarity.Mythic, BodyType.Bulky, HeadType.Helmet, TopType.Horns, BackType.Wings, GloveType.Claws, "b0102a", "ffcf33", SpecialType.Meteor, "DRAGON METEOR", "Half dragon, all fury"), secondary: "4a0810", pads: true),
            Look(F("CYBER RONIN", Rarity.Legendary, BodyType.Robot, HeadType.Mask, TopType.Mohawk, BackType.Blades, GloveType.Metal, "e01aa8", "1ae0ff", SpecialType.FlashStep, "NEON SLASH", "A cyborg swordsman"), skin: "d0d6e0", secondary: "2a0a24"),
            Look(F("TIDECALLER", Rarity.Epic, BodyType.Normal, HeadType.Hood, TopType.Crown, BackType.Cape, GloveType.Glow, "1ac4c4", "1a4ab8", SpecialType.Shockwave, "TIDAL WAVE", "Rules the sea"), secondary: "0a2a5a"),
            Look(F("BONECRUSHER", Rarity.Rare, BodyType.Giant, HeadType.Skull, TopType.Horns, BackType.Spines, GloveType.Big, "e8dcc0", "6a4a2a", SpecialType.Uppercut, "SKULL CRACKER", "Crushes bones for fun"), Outfit.Primal, skin: "9a8070", hair: "4a3020", secondary: "3a2a1a"),
            Look(F("SPARK", Rarity.Common, BodyType.Tiny, HeadType.Round, TopType.Antennae, BackType.Jetpack, GloveType.Glow, "1ae0ff", "ffe033", SpecialType.Lightning, "ZAP", "Small but shocking"), skin: "f2d0b0", hair: "1a1a1a", secondary: "0a4a5a"),
            Look(F("ECHO", Rarity.Rare, BodyType.Slim, HeadType.Helmet, TopType.None, BackType.Orbs, GloveType.Normal, "1ab5a0", "ffffff", SpecialType.Clones, "ECHO SQUAD", "Never fights alone"), Outfit.Suit, secondary: "0a3a34"),
            Look(F("THUNDER OX", Rarity.Rare, BodyType.Beast, HeadType.Round, TopType.Horns, BackType.Tail, GloveType.Big, "6a4022", "ffe033", SpecialType.GroundSlam, "STAMPEDE", "Charges like a storm"), skin: "6a4022", hair: "3a2010", secondary: "2a1a0a"),
            F("MIRAGE", Rarity.Epic, BodyType.Slim, HeadType.Hood, TopType.None, BackType.Cape, GloveType.Glow, "ffcf33", "8a3acc", SpecialType.Clones, "DESERT ILLUSION", "Which one is real?"),
            Look(F("IRON FIST", Rarity.Rare, BodyType.Normal, HeadType.Round, TopType.Mohawk, BackType.None, GloveType.Metal, "8a96a8", "e83030", SpecialType.Uppercut, "IRON UPPERCUT", "Fists of steel"), Outfit.Gi, skin: "c98a5a", hair: "1a1a1a", secondary: "2a2e36"),
            Look(F("SOLAR", Rarity.Legendary, BodyType.Normal, HeadType.Round, TopType.Flames, BackType.Wings, GloveType.Glow, "ff7a1a", "ffe033", SpecialType.Beam, "SOLAR FLARE", "Burns as bright as the sun"), Outfit.Suit, skin: "d9a77c", hair: "8a2a1a", secondary: "6a2a0a"),
            F("LUNAR", Rarity.Legendary, BodyType.Slim, HeadType.Hood, TopType.Halo, BackType.Orbs, GloveType.Glow, "c8d0e0", "4a7aff", SpecialType.Barrier, "MOON SHIELD", "Cool, calm, untouchable"),
            Look(F("VENOM", Rarity.Epic, BodyType.Beast, HeadType.Mask, TopType.Spikes, BackType.Tail, GloveType.Claws, "6a1a8a", "6aff3a", SpecialType.Shockwave, "TOXIC BURST", "Poison in every punch"), skin: "6a3a7a", hair: "2a0a3a", secondary: "2a0a3a"),
            Look(F("JUGGERNAUT", Rarity.Epic, BodyType.Giant, HeadType.Helmet, TopType.None, BackType.Jetpack, GloveType.Metal, "2a2a30", "ff8a1a", SpecialType.Barrier, "UNSTOPPABLE", "Armoured from head to toe"), secondary: "1a1a1e", pads: true),
            Look(F("ZEPHYR", Rarity.Rare, BodyType.Tiny, HeadType.Round, TopType.Ponytail, BackType.Wings, GloveType.Normal, "9ff09a", "ffffff", SpecialType.Whirlwind, "CYCLONE", "Light as the wind"), Outfit.Gi, skin: "f6dcc8", hair: "e8fff0", secondary: "3a8a5a", scarf: true),
            Look(F("OMEGA", Rarity.Mythic, BodyType.Robot, HeadType.Helmet, TopType.Crown, BackType.Blades, GloveType.Glow, "111114", "ff1a3a", SpecialType.Meteor, "OMEGA STRIKE", "The final machine", "ff1a3a"), secondary: "0a0a0c", pads: true),
        });

        /// <summary>
        /// NOT balanced on purpose: rarer fighters punch harder, have stronger supers, more health and shorter
        /// special cooldowns, and every fighter also gets its own random-but-fixed twist (picked from its name, so
        /// it's the same on every headset) to punch damage, super damage, health and speed. The two starters keep
        /// their hand-tuned stats.
        /// </summary>
        static FighterStats[] Unbalance(FighterStats[] roster)
        {
            // (Local, not static fields: static fields below the roster would still be null when it's built.)
            // Rarity:               Common  Rare   Epic   Legendary  Mythic
            float[] rarityPunch =    { 1f,   1.08f, 1.18f, 1.3f,  1.45f };
            float[] raritySuper =    { 1f,   1.12f, 1.25f, 1.4f,  1.65f };
            float[] rarityHealth =   { 0f,   5f,    10f,   18f,   28f };
            float[] raritySpeed =    { 1f,   1.02f, 1.04f, 1.06f, 1.1f };
            float[] rarityCooldown = { 1f,   0.95f, 0.9f,  0.85f, 0.78f };
            for (int i = 2; i < roster.Length; i++)
            {
                var s = roster[i];
                uint h = 2166136261u;
                foreach (char ch in s.name) h = (h ^ ch) * 16777619u;
                float Roll(float min, float max) { h = h * 1664525u + 1013904223u; return min + (h >> 8) / 16777216f * (max - min); }
                int r = (int)s.rarity;
                s.power = Mathf.Round(s.power * rarityPunch[r] * Roll(0.85f, 1.15f) * 100f) / 100f;
                s.superPower = Mathf.Round(raritySuper[r] * Roll(0.8f, 1.25f) * 100f) / 100f;
                s.maxHealth = Mathf.Round(s.maxHealth * Roll(0.9f, 1.1f) + rarityHealth[r]);
                s.moveSpeed *= raritySpeed[r] * Roll(0.92f, 1.08f);
                s.specialCooldown = Mathf.Round(s.specialCooldown * rarityCooldown[r] * Roll(0.85f, 1.15f) * 10f) / 10f;
                roster[i] = s;
            }
            return roster;
        }

        /// <summary>"Punch x1.30  Super x1.65" for menus.</summary>
        public static string PowerText(FighterStats s) => "Punch x" + s.power.ToString("0.00") + "   Super x" + (s.power * s.superPower).ToString("0.00");
    }
}