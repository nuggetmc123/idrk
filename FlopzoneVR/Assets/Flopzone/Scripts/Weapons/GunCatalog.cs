using UnityEngine;

namespace Flopzone
{
    public enum GunClass { Pistol, Shotgun, Sniper, AssaultRifle, SMG }
    public enum FireMode { Semi, Auto, Burst, Bolt, Pump }

    /// <summary>Stats and look of one gun.</summary>
    public class GunDef
    {
        public int index;
        public string id, name;
        public GunClass cls;
        public FireMode mode;
        public float damage;        // per pellet
        public int pellets = 1;
        public float rpm;           // rounds per minute
        public int magSize;
        public float spread;        // degrees
        public float range = 120f;
        public float recoil;        // visual kick, degrees
        public float headMult = 2f;
        public string sound;
        public float pitch = 1f;
        public bool scope;
        public Color accent;
        // model
        public float bodyLen, barrelLen, height, stockLen;
        public bool drum, pump, twinBarrel, revolver, longMag;

        public float Interval => 60f / Mathf.Max(1f, rpm);
        public float MaxHit => damage * pellets * headMult;
    }

    /// <summary>All 30 guns. New guns go on the end so saved loadouts keep pointing at the same gun.</summary>
    public static class GunCatalog
    {
        public static readonly GunDef[] All = Build();

        static GunDef G(string id, string name, GunClass c, FireMode m, float dmg, int pellets, float rpm, int mag, float spread, float recoil, string snd, float pitch, string hex,
                        float body, float barrel, float h, float stock)
        {
            ColorUtility.TryParseHtmlString(hex, out var col);
            return new GunDef
            {
                id = id, name = name, cls = c, mode = m, damage = dmg, pellets = pellets, rpm = rpm, magSize = mag,
                spread = spread, recoil = recoil, sound = snd, pitch = pitch, accent = col,
                bodyLen = body, barrelLen = barrel, height = h, stockLen = stock
            };
        }

        static GunDef[] Build()
        {
            var list = new[]
            {
                // ---- pistols ----
                G("flop9",    "Flop 9",          GunClass.Pistol, FireMode.Semi,  20, 1, 420, 12, 1.0f, 6,  "shot_pistol", 1.05f, "#f2a33a", 0.15f, 0.04f, 0.035f, 0),
                G("deagle",   "Duck Deagle",     GunClass.Pistol, FireMode.Semi,  48, 1, 160,  7, 1.4f, 14, "shot_pistol", 0.8f,  "#d9d9d9", 0.2f,  0.06f, 0.045f, 0),
                G("buzz",     "Buzz Mini",       GunClass.Pistol, FireMode.Auto,  12, 1, 950, 24, 3.0f, 3,  "shot_smg",    1.2f,  "#5fd1ff", 0.16f, 0.03f, 0.04f,  0),
                G("revolt",   "Revolt Revolver", GunClass.Pistol, FireMode.Semi,  55, 1, 130,  6, 0.8f, 16, "shot_pistol", 0.7f,  "#a46cff", 0.17f, 0.11f, 0.04f,  0),
                G("squeak",   "Squeak .22",      GunClass.Pistol, FireMode.Semi,  15, 1, 600, 15, 0.6f, 3,  "shot_pistol", 1.4f,  "#7cff6b", 0.13f, 0.07f, 0.03f,  0),
                G("hammer",   "Twin Hammer",     GunClass.Pistol, FireMode.Burst, 17, 1, 800, 18, 1.6f, 5,  "shot_pistol", 1.15f, "#ff5a5a", 0.17f, 0.05f, 0.04f,  0),
                // ---- shotguns ----
                G("boom",     "Boomstick",       GunClass.Shotgun, FireMode.Pump, 12, 8, 70,  6,  6.0f, 18, "shot_shotgun", 1.0f, "#c78a4a", 0.32f, 0.38f, 0.05f, 0.22f),
                G("sawed",    "Sawed Slam",      GunClass.Shotgun, FireMode.Semi, 12, 10, 240, 2, 8.0f, 22, "shot_shotgun", 0.9f, "#8a5a2b", 0.24f, 0.2f,  0.05f, 0.12f),
                G("thunder",  "Auto Thunder",    GunClass.Shotgun, FireMode.Semi, 10, 6, 200, 8,  5.0f, 12, "shot_shotgun", 1.1f, "#3a3a3a", 0.34f, 0.32f, 0.055f, 0.2f),
                G("brute",    "Drum Brute",      GunClass.Shotgun, FireMode.Auto,  8, 6, 300, 12, 6.5f, 10, "shot_shotgun", 1.2f, "#ff8c1a", 0.36f, 0.28f, 0.06f, 0.2f),
                G("slug",     "Slug Lord",       GunClass.Shotgun, FireMode.Pump, 70, 1, 75,  5,  0.6f, 20, "shot_shotgun", 0.8f, "#e8d24a", 0.32f, 0.42f, 0.05f, 0.22f),
                // ---- snipers ----
                G("longshot", "Longshot",        GunClass.Sniper, FireMode.Bolt, 90, 1, 45,  5,  0.05f, 20, "shot_sniper", 1.0f, "#3c6e47", 0.42f, 0.5f,  0.05f, 0.26f),
                G("whisper",  "Whisper",         GunClass.Sniper, FireMode.Semi, 60, 1, 150, 10, 0.15f, 12, "shot_sniper", 1.25f, "#2b2b2b", 0.4f, 0.45f,  0.05f, 0.24f),
                G("elephant", "Elephant Gun",    GunClass.Sniper, FireMode.Bolt, 130, 1, 32, 3,  0.05f, 30, "shot_sniper", 0.75f, "#7a4b2a", 0.46f, 0.6f, 0.06f, 0.28f),
                G("marksman", "Marksman DMR",    GunClass.Sniper, FireMode.Semi, 45, 1, 240, 15, 0.3f, 9,  "shot_sniper", 1.35f, "#6b7b8c", 0.42f, 0.36f, 0.055f, 0.24f),
                G("needle",   "Needle",          GunClass.Sniper, FireMode.Bolt, 78, 1, 70,  6,  0.05f, 14, "shot_sniper", 1.5f, "#e0e0ff", 0.38f, 0.55f, 0.045f, 0.22f),
                // ---- assault rifles ----
                G("rattle",   "Rattle AR",       GunClass.AssaultRifle, FireMode.Auto,  22, 1, 650, 30, 1.6f, 4, "shot_ar", 1.0f,  "#556b2f", 0.4f, 0.24f, 0.06f, 0.22f),
                G("bulldog",  "Bulldog",         GunClass.AssaultRifle, FireMode.Auto,  27, 1, 540, 25, 1.8f, 6, "shot_ar", 0.85f, "#8b5a3c", 0.36f, 0.2f,  0.065f, 0.18f),
                G("viper",    "Viper Carbine",   GunClass.AssaultRifle, FireMode.Auto,  18, 1, 820, 35, 2.0f, 3, "shot_ar", 1.2f,  "#2fbf71", 0.34f, 0.18f, 0.055f, 0.18f),
                G("bravo",    "Burst Bravo",     GunClass.AssaultRifle, FireMode.Burst, 25, 1, 900, 30, 1.0f, 4, "shot_ar", 1.1f,  "#3f6fd8", 0.4f, 0.26f,  0.06f, 0.22f),
                G("hmr",      "Heavy HMR",       GunClass.AssaultRifle, FireMode.Auto,  32, 1, 450, 20, 1.4f, 8, "shot_ar", 0.7f,  "#4a4a4a", 0.46f, 0.3f,  0.07f, 0.24f),
                G("spitfire", "Spitfire",        GunClass.AssaultRifle, FireMode.Auto,  16, 1, 960, 40, 2.4f, 3, "shot_ar", 1.3f,  "#ff4fa3", 0.36f, 0.2f,  0.055f, 0.2f),
                // ---- added with the skill tree (index 22+) ----
                G("classic",  "Classic 45",      GunClass.Pistol, FireMode.Semi,  34, 1, 300,  8, 1.0f, 10, "shot_pistol", 0.9f, "#9b7b5a", 0.2f, 0.05f, 0.04f, 0),
                G("hornet",   "Hornet",          GunClass.SMG, FireMode.Auto,     17, 1, 800, 30, 1.8f, 3,  "shot_smg", 1.1f,  "#e2c044", 0.3f, 0.06f, 0.05f, 0.2f),
                G("slab",     "Slab",            GunClass.SMG, FireMode.Auto,     23, 1, 600, 25, 2.0f, 4,  "shot_smg", 0.9f,  "#6d6d6d", 0.3f, 0.06f, 0.05f, 0.22f),
                G("bullhorn", "Bullhorn",        GunClass.SMG, FireMode.Auto,     15, 1, 900, 50, 2.2f, 2,  "shot_smg", 1.25f, "#3b4a3f", 0.5f, 0.05f, 0.06f, 0f),
                G("twintube", "Twin Tube",       GunClass.Shotgun, FireMode.Pump, 13, 8, 80, 14, 5.5f, 17, "shot_shotgun", 0.95f, "#2d2d2d", 0.45f, 0.38f, 0.06f, 0f),
                G("fiftycal", "Fifty Cal",       GunClass.Sniper, FireMode.Semi, 110, 1, 90, 10, 0.1f, 26, "shot_sniper", 0.65f, "#55575c", 0.66f, 0.54f, 0.07f, 0.28f),
                G("mosin",    "Old Mosin",       GunClass.Sniper, FireMode.Bolt, 100, 1, 40,  5, 0.05f, 22, "shot_sniper", 0.85f, "#8a5a32", 0.36f, 0.6f, 0.05f, 0.36f),
                G("gecko",    "Gecko",           GunClass.AssaultRifle, FireMode.Auto, 21, 1, 750, 30, 1.5f, 4, "shot_ar", 1.15f, "#4d5c4a", 0.33f, 0.18f, 0.06f, 0.2f),
            };
            for (int i = 0; i < list.Length; i++)
            {
                var g = list[i];
                g.index = i;
                g.scope = g.cls == GunClass.Sniper;
                g.range = g.cls == GunClass.Shotgun ? 45f : g.cls == GunClass.Pistol ? 80f : g.cls == GunClass.SMG ? 100f : 160f;
                g.headMult = g.cls == GunClass.Sniper ? 2f : 1.6f;
                g.pump = g.mode == FireMode.Pump;
                g.twinBarrel = g.id == "sawed";
                g.revolver = g.id == "revolt";
                g.drum = g.id == "brute" || g.id == "hmr";
                g.longMag = g.id == "spitfire" || g.id == "buzz" || g.id == "hornet" || g.id == "slab";
            }
            return list;
        }

        public static GunDef Get(int index) => index >= 0 && index < All.Length ? All[index] : null;

        public static GunDef ById(string id)
        {
            foreach (var g in All) if (g.id == id) return g;
            return null;
        }

        public static string ClassName(GunClass c)
        {
            switch (c)
            {
                case GunClass.Pistol: return "Pistols";
                case GunClass.Shotgun: return "Shotguns";
                case GunClass.Sniper: return "Snipers";
                case GunClass.SMG: return "SMGs";
                default: return "Assault Rifles";
            }
        }
    }
}
