using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Vault, Boss }   // Boss: only ever carried by a map boss
    public enum ItemKind { Gun, Sight, Magazine, Ammo, Med, Valuable, Key, Armor, Backpack, Throwable, Melee, Attachment }
    public enum ArmorSlot { Helmet, Vest }
    public enum GunClass { Pistol, Revolver, SMG, Shotgun, Rifle, BattleRifle, DMR, Sniper, LMG }
    public enum FireAction { Semi, Auto, Burst, Bolt, Pump, Break, Revolver }
    public enum Caliber { C9mm, C45, C57, C556, C545, C762, C308, C338, C50BMG, C12g, C50AE, C357, C20mm }
    /// <summary>What's loaded in a magazine: standard ball, armour-piercing, or incendiary (sets people on fire).</summary>
    public enum AmmoType : byte { Standard, AP, Incendiary }
    public enum GunStyle { Pistol, Revolver, SmgBox, SmgMac, SmgVector, Bullpup, AR, AK, Battle, Bolt, Pump, Double, BoxShotgun, Lmg, AntiMat, Tommy }
    public enum Finish { Black, Fde, Od, Wood, Chrome, Gold, Tan, Plum, Gray }

    public class ItemDef
    {
        public string id, name, desc;
        public ItemKind kind;
        public Rarity rarity;
        public int price;
        public bool large; // long guns: back slots / stash only
        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new Color(0.75f, 0.75f, 0.75f);
                case Rarity.Uncommon: return new Color(0.35f, 0.85f, 0.35f);
                case Rarity.Rare: return new Color(0.3f, 0.6f, 1f);
                case Rarity.Epic: return new Color(0.75f, 0.4f, 1f);
                case Rarity.Boss: return new Color(1f, 0.22f, 0.18f);
                default: return new Color(1f, 0.72f, 0.2f);
            }
        }
    }

    public class GunDef : ItemDef
    {
        public GunClass cls;
        public GunStyle style;
        public FireAction action;
        public Caliber cal;
        public float damage;
        public int rpm;
        public int magSize;
        public float recoil;      // 0.3 (soft) .. 3 (brutal)
        public float spread;      // degrees hip/cone
        public float velocity;    // m/s
        public bool optics;       // has a rail for holo sights
        public bool suppressed;
        public Finish finish, furniture;
        public float barrel = 0.35f; // barrel length metres
        public int pellets = 1;
        public bool drum;
        public bool TwoHanded => cls != GunClass.Pistol && cls != GunClass.Revolver;

        GunDef _quiet;
        /// <summary>This gun with a suppressor screwed on (same gun, quiet shot sound / flash / noise).</summary>
        public GunDef Quiet
        {
            get
            {
                if (suppressed) return this;
                if (_quiet == null) { _quiet = (GunDef)MemberwiseClone(); _quiet.suppressed = true; _quiet._quiet = _quiet; }
                return _quiet;
            }
        }
    }

    public enum AttachSlot { Muzzle, Side, Grip }

    /// <summary>
    /// A gun attachment: muzzle devices (suppressors, brakes) screw onto the barrel, lights and lasers clip onto the
    /// side rail (under the frame on pistols), grips go under the handguard of long guns.
    /// </summary>
    public class AttachDef : ItemDef
    {
        public AttachSlot slot;
        public int model;
        public float recoilMul = 1f, spreadMul = 1f;
        public bool suppress, light, laser;
        public Color beam = Color.red;
        public Finish finish;
    }

    public class SightDef : ItemDef
    {
        public Color reticle;
        public int style;     // shader style
        public float size;
        public int model;     // 0 holo box, 1 micro dot, 2 open reflex, 3 hex holo, 4 tactical reflex, 5 hunting scope, 6 long-range scope, 7 prism
        public float mag;     // magnified scopes: 3x, 4x, 8x (0 = red dot / holo)
        public Finish finish;
    }

    /// <summary>Wearable armor: a helmet protects your head, a vest your chest and stomach.</summary>
    public class ArmorDef : ItemDef
    {
        public ArmorSlot slot;
        public float protection;  // fraction of damage stopped (0..1)
        public int style;         // visual variant
        public int tier;          // T1..T6 (0 = boss armour, which keeps its own look)
        public Color color, trim;

        /// <summary>Armour class 1..6 (from how protective it is): soft vest ~1.5, plate carrier 3, heavy 4, assault 5, juggernaut 6.5.</summary>
        public float Class => protection * 10f;

        /// <summary>How hard a round punches through armour (same scale as the armour class).</summary>
        public static float Penetration(string gunId)
        {
            if (gunId == "frag") return 3.2f;   // fragments
            var g = !string.IsNullOrEmpty(gunId) ? Db.Get<GunDef>(gunId) : null;
            if (g == null) return 0.8f;          // fists, knives
            switch (g.cal)
            {
                case Caliber.C9mm: return 2.0f;
                case Caliber.C45: return 2.0f;
                case Caliber.C357: return 2.6f;
                case Caliber.C50AE: return 3.2f;
                case Caliber.C57: return 3.6f;
                case Caliber.C12g: return 1.4f;
                case Caliber.C556: return 4.4f;
                case Caliber.C545: return 4.4f;
                case Caliber.C762: return 4.8f;
                case Caliber.C308: return 5.6f;
                case Caliber.C338: return 6.6f;
                case Caliber.C50BMG: return 8.5f;
                case Caliber.C20mm: return 11f;
            }
            return 3f;
        }

        /// <summary>Damage multiplier for a hit given the helmet and vest worn (null = none) and what it was hit with.
        /// A round weaker than the armour is stopped (you still take a bruising 12-35%); a stronger one goes through
        /// (70-100%); right around the armour's class it's a coin flip.</summary>
        public static float Reduction(BodyPart part, ArmorDef helmet, ArmorDef vest, string gunId = null, AmmoType ammo = AmmoType.Standard)
        {
            ArmorDef a = part == BodyPart.Head ? helmet : (part == BodyPart.Thorax || part == BodyPart.Stomach) ? vest : null;
            if (a == null) return 1f;
            float cls = a.Class * (part == BodyPart.Stomach ? 0.7f : 1f);   // the vest covers the belly less well
            if (cls <= 0.01f) return 1f;
            float pen = Penetration(gunId) + (ammo == AmmoType.AP ? 2.2f : ammo == AmmoType.Incendiary ? -0.6f : 0f);
            float diff = pen - cls;
            bool through = diff >= 0.7f || (diff > -0.7f && Random.value < 0.5f + diff / 1.4f);
            if (through) return Mathf.Clamp(0.72f + 0.08f * Mathf.Max(0f, diff), 0.72f, 1f);
            return Mathf.Clamp(0.12f + 0.2f * Mathf.Clamp01(pen / cls), 0.12f, 0.35f);
        }
    }

    /// <summary>A backpack: an open main compartment (pull it off your left shoulder to open it) plus side straps for long guns.</summary>
    public class BackpackDef : ItemDef
    {
        public Vector3 inner;   // open compartment, metres: width (x), height (y), depth (z)
        public int sides;       // long-gun straps on the sides (0-2)
        public Color color;
        public int style;
        public int Liters => Mathf.RoundToInt(inner.x * inner.y * inner.z * 1000f);
    }

    public class SimpleDef : ItemDef
    {
        public float heal;
        public float healTime = 0.5f;   // seconds the heal is spread over
        public bool stopsBleed;
        public float energy;            // energy drinks: seconds of boosted stamina / sprint
        public float focus;             // DUBBY: seconds of FOCUS (steadier aim: less recoil and hand sway)
        public Caliber ammoCal;
        public int ammoCount;
        public AmmoType ammoType;   // AP / incendiary boxes: found in raids only
        public int shape; // visual variant
        public Color color = Color.gray;
    }

    public static class Db
    {
        public static readonly Dictionary<string, ItemDef> All = new Dictionary<string, ItemDef>();
        public static readonly List<GunDef> Guns = new List<GunDef>();
        public static readonly List<SightDef> Sights = new List<SightDef>();
        public static readonly List<AttachDef> Attachments = new List<AttachDef>();
        public static readonly List<SimpleDef> Misc = new List<SimpleDef>();
        public static readonly List<ArmorDef> Armor = new List<ArmorDef>();
        public static readonly List<BackpackDef> Packs = new List<BackpackDef>();
        static bool _init;

        public static T Get<T>(string id) where T : ItemDef
        {
            Init();
            if (id != null && All.TryGetValue(id, out var d)) return d as T;
            return null;
        }
        public static ItemDef Get(string id) => Get<ItemDef>(id);

        public static string CalName(Caliber c)
        {
            switch (c)
            {
                case Caliber.C9mm: return "9x19mm";
                case Caliber.C45: return ".45 ACP";
                case Caliber.C57: return "5.7x28mm";
                case Caliber.C556: return "5.56x45mm";
                case Caliber.C545: return "5.45x39mm";
                case Caliber.C762: return "7.62x39mm";
                case Caliber.C308: return "7.62x51mm";
                case Caliber.C338: return ".338 Lapua";
                case Caliber.C50BMG: return ".50 BMG";
                case Caliber.C12g: return "12 Gauge";
                case Caliber.C50AE: return ".50 AE";
                case Caliber.C20mm: return "20mm";
                default: return ".357 Mag";
            }
        }

        /// <summary>Registers an item by id. Two items with the same id used to silently replace each other (the Reaper 45
        /// was really a Reaper 416, the Tundra AK-12 an AN-9) - now it's an error you see straight away.</summary>
        static void Reg(string id, ItemDef d)
        {
            if (All.ContainsKey(id)) Debug.LogError("[Rago] DUPLICATE item id '" + id + "': " + All[id].name + " and " + d.name);
            All[id] = d;
        }

        static GunDef G(string id, string name, GunClass cls, GunStyle style, FireAction act, Caliber cal, float dmg, int rpm, int mag,
            float recoil, float spread, Rarity r, int price, bool optics, Finish fin, Finish furn, float barrel, string desc)
        {
            var g = new GunDef
            {
                id = id, name = name, cls = cls, style = style, action = act, cal = cal, damage = dmg, rpm = rpm, magSize = mag,
                recoil = recoil, spread = spread, rarity = r, price = price, optics = optics, finish = fin, furniture = furn, barrel = barrel,
                kind = ItemKind.Gun, desc = desc
            };
            g.large = g.TwoHanded;
            g.velocity = cal == Caliber.C12g ? 400 : cls == GunClass.Pistol || cls == GunClass.SMG || cls == GunClass.Revolver ? 380 : cls == GunClass.Sniper ? 880 : 760;
            if (cal == Caliber.C12g) g.pellets = 8;
            // handguns were out-shooting rifles: less damage, slower to spam, wider shots (machine pistols most of all)
            if (cls == GunClass.Pistol)
            {
                bool auto = act == FireAction.Auto;
                g.damage = Mathf.Round(dmg * (auto ? 0.62f : 0.7f));
                g.rpm = auto ? Mathf.Min(rpm, 750) : Mathf.Min(rpm, 300);
                g.spread = spread * (auto ? 2f : 1.7f);
                g.recoil = recoil * 1.3f;
            }
            else if (cls == GunClass.Revolver) { g.damage = Mathf.Round(dmg * 0.84f); g.spread = spread * 1.4f; }
            Guns.Add(g); Reg(id, g);
            return g;
        }

        static SightDef S(string id, string name, int model, Color ret, int style, float size, Rarity r, int price, Finish fin, string desc)
        {
            var s = new SightDef { id = id, name = name, model = model, reticle = ret, style = style, size = size, rarity = r, price = price, finish = fin, kind = ItemKind.Sight, desc = desc };
            Sights.Add(s); Reg(id, s); return s;
        }

        static AttachDef X(string id, string name, AttachSlot slot, int model, Rarity r, int price, float recoil, float spread, Finish fin, string desc)
        {
            var a = new AttachDef { id = id, name = name, slot = slot, model = model, rarity = r, price = price, recoilMul = recoil, spreadMul = spread, finish = fin, kind = ItemKind.Attachment, desc = desc };
            Attachments.Add(a); Reg(id, a); return a;
        }

        static SimpleDef M(string id, string name, ItemKind kind, Rarity r, int price, int shape, Color col, string desc)
        {
            var m = new SimpleDef { id = id, name = name, kind = kind, rarity = r, price = price, shape = shape, color = col, desc = desc };
            Misc.Add(m); Reg(id, m); return m;
        }

        public static void Init()
        {
            if (_init) return;
            _init = true;
            var C = Rarity.Common; var U = Rarity.Uncommon; var R = Rarity.Rare; var E = Rarity.Epic; var V = Rarity.Vault;

            // ---------------- PISTOLS / REVOLVERS (9)
            G("p9", "P9 Service", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C9mm, 28, 450, 17, 0.6f, 1.2f, C, 450, true, Finish.Black, Finish.Black, 0.11f, "Reliable polymer service pistol. Found everywhere.");
            G("mako45", "Mako .45", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C45, 36, 380, 8, 0.85f, 1.3f, C, 600, false, Finish.Gray, Finish.Wood, 0.13f, "All-steel classic with walnut grips.");
            G("duchess", "Duchess Compact", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C9mm, 26, 500, 10, 0.55f, 1.5f, C, 350, false, Finish.Chrome, Finish.Black, 0.09f, "Small, cheap and easy to conceal.");
            G("viper17", "Viper 17", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C9mm, 29, 480, 17, 0.55f, 1.0f, U, 800, true, Finish.Black, Finish.Fde, 0.12f, "Competition-tuned frame with an optic cut.");
            G("hornet", "Hornet MP", GunClass.Pistol, GunStyle.Pistol, FireAction.Auto, Caliber.C9mm, 24, 1100, 25, 0.8f, 2.2f, U, 1400, true, Finish.Black, Finish.Od, 0.12f, "Full-auto machine pistol. Hold on tight.");
            G("talon50", "Talon .50", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C50AE, 62, 220, 7, 2.2f, 1.1f, R, 3200, true, Finish.Chrome, Finish.Black, 0.16f, "Hand cannon. Big recoil, bigger holes.");
            G("falcon10", "Falcon Compact", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C9mm, 27, 480, 15, 0.5f, 1.1f, C, 550, true, Finish.Black, Finish.Od, 0.1f, "Compact striker 9mm with an OD frame. Easy to shoot.");
            G("warhawk", "Warhawk .45", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C45, 38, 380, 10, 0.9f, 1.1f, U, 1500, true, Finish.Fde, Finish.Fde, 0.13f, "Full-size .45 in flat dark earth. Hits like a truck.");
            G("frontier", "Frontier Six", GunClass.Revolver, GunStyle.Revolver, FireAction.Revolver, Caliber.C357, 54, 240, 6, 1.6f, 0.8f, U, 1700, false, Finish.Chrome, Finish.Wood, 0.16f, "Polished long-barrel .357 with walnut grips.");
            G("sovereign", "Gilded Sovereign", GunClass.Pistol, GunStyle.Pistol, FireAction.Semi, Caliber.C45, 44, 420, 10, 0.7f, 0.8f, V, 9000, true, Finish.Gold, Finish.Wood, 0.13f, "Vault-only. Gold plated, hand-fitted, deadly accurate.");
            G("ranger357", "Ranger .357", GunClass.Revolver, GunStyle.Revolver, FireAction.Revolver, Caliber.C357, 48, 260, 6, 1.4f, 1.0f, C, 700, false, Finish.Gray, Finish.Wood, 0.1f, "Six rounds of dependable stopping power.");
            G("bulldog44", "Bulldog Hunter", GunClass.Revolver, GunStyle.Revolver, FireAction.Revolver, Caliber.C357, 58, 220, 6, 1.8f, 0.7f, R, 2600, true, Finish.Chrome, Finish.Black, 0.2f, "Long-barrel hunting revolver with a top rail.");

            // ---------------- SMGs (8)
            G("wasp", "Wasp MP9", GunClass.SMG, GunStyle.SmgMac, FireAction.Auto, Caliber.C9mm, 22, 950, 30, 0.6f, 2.0f, C, 1600, true, Finish.Black, Finish.Black, 0.12f, "Compact bullet hose with a folding stock.");
            G("bandit", "Bandit M11", GunClass.SMG, GunStyle.SmgMac, FireAction.Auto, Caliber.C9mm, 21, 1200, 32, 0.7f, 2.6f, C, 1200, false, Finish.Gray, Finish.Gray, 0.1f, "Stamped steel, absurd fire rate, zero manners.");
            G("kestrel", "Kestrel 9", GunClass.SMG, GunStyle.SmgBox, FireAction.Auto, Caliber.C9mm, 25, 800, 30, 0.45f, 1.4f, U, 2400, true, Finish.Black, Finish.Black, 0.22f, "Roller-delayed classic. Smooth and controllable.");
            G("nomad", "Nomad 45", GunClass.SMG, GunStyle.SmgBox, FireAction.Auto, Caliber.C45, 31, 650, 25, 0.6f, 1.5f, U, 2600, true, Finish.Black, Finish.Od, 0.2f, "Polymer .45 sub-gun with a side-folding stock.");
            G("rattler", "Rattler PDW", GunClass.SMG, GunStyle.SmgVector, FireAction.Auto, Caliber.C57, 24, 900, 50, 0.4f, 1.3f, R, 4200, true, Finish.Gray, Finish.Black, 0.2f, "High-capacity personal defense weapon.");
            G("cyclone", "Cyclone 45", GunClass.SMG, GunStyle.SmgVector, FireAction.Auto, Caliber.C45, 30, 1150, 25, 0.35f, 1.3f, R, 4800, true, Finish.Black, Finish.Fde, 0.16f, "Recoil-mitigating chassis. Lasers at close range.");
            G("stinger", "Stinger PDW", GunClass.SMG, GunStyle.Bullpup, FireAction.Auto, Caliber.C57, 25, 900, 50, 0.35f, 1.2f, E, 6200, true, Finish.Tan, Finish.Black, 0.26f, "Bullpup PDW with a 50-round top magazine.");
            var ph = G("phantom", "Phantom SD", GunClass.SMG, GunStyle.SmgBox, FireAction.Auto, Caliber.C9mm, 28, 850, 30, 0.35f, 1.0f, V, 11000, true, Finish.Black, Finish.Black, 0.3f, "Vault-only. Integrally suppressed. Quiet death.");
            G("vespa", "Vespa 9", GunClass.SMG, GunStyle.SmgVector, FireAction.Auto, Caliber.C9mm, 25, 1000, 33, 0.4f, 1.4f, U, 2800, true, Finish.Gray, Finish.Black, 0.18f, "Recoil-redirecting 9mm. Hoses a room flat.");
            G("reaper45", "Reaper 45", GunClass.SMG, GunStyle.SmgBox, FireAction.Auto, Caliber.C45, 31, 700, 30, 0.55f, 1.5f, R, 3800, true, Finish.Black, Finish.Black, 0.22f, "Heavy-hitting .45 sub-gun with a long handguard.");
            ph.suppressed = true;

            // ---------------- SHOTGUNS (6)
            G("breacher", "Breacher 870", GunClass.Shotgun, GunStyle.Pump, FireAction.Pump, Caliber.C12g, 14, 70, 6, 2.0f, 4.5f, C, 1500, true, Finish.Black, Finish.Black, 0.48f, "Pump-action door opener.");
            G("homestead", "Homestead Double", GunClass.Shotgun, GunStyle.Double, FireAction.Break, Caliber.C12g, 15, 300, 2, 2.4f, 5f, C, 900, false, Finish.Gray, Finish.Wood, 0.6f, "Grandpa's side-by-side. Two shots, make them count.");
            G("riot", "Riot Pump", GunClass.Shotgun, GunStyle.Pump, FireAction.Pump, Caliber.C12g, 14, 80, 8, 1.8f, 4.8f, U, 2200, true, Finish.Black, Finish.Od, 0.42f, "Tactical pump with pistol grip and extended tube.");
            G("harvester", "Harvester Pump", GunClass.Shotgun, GunStyle.Pump, FireAction.Pump, Caliber.C12g, 14, 75, 7, 2.0f, 4.6f, C, 1300, true, Finish.Black, Finish.Wood, 0.5f, "Farm-house pump with a walnut stock. Reliable.");
            G("mauler", "Mauler 12", GunClass.Shotgun, GunStyle.BoxShotgun, FireAction.Semi, Caliber.C12g, 13, 300, 8, 1.9f, 4.5f, R, 4600, true, Finish.Black, Finish.Black, 0.45f, "Box-fed semi-auto shotgun on a rifle chassis.");
            var th = G("thunder", "Thunder Auto-12", GunClass.Shotgun, GunStyle.BoxShotgun, FireAction.Auto, Caliber.C12g, 12, 330, 20, 1.6f, 5f, E, 7800, true, Finish.Od, Finish.Black, 0.4f, "Full-auto drum shotgun. Rooms get very quiet.");
            th.drum = true;
            var dr = G("dragon", "Dragon's Breath", GunClass.Shotgun, GunStyle.BoxShotgun, FireAction.Semi, Caliber.C12g, 17, 360, 12, 1.5f, 3.6f, V, 12500, true, Finish.Black, Finish.Gold, 0.45f, "Vault-only. Drum-fed, tight pattern, gold accents.");
            dr.drum = true;

            // ---------------- ASSAULT RIFLES (12)
            G("vanguard", "Vanguard Carbine", GunClass.Rifle, GunStyle.AR, FireAction.Auto, Caliber.C556, 30, 700, 30, 0.7f, 1.2f, C, 2800, true, Finish.Black, Finish.Od, 0.36f, "Budget AR carbine. Does the job.");
            G("vostok74", "Vostok 74", GunClass.Rifle, GunStyle.AK, FireAction.Auto, Caliber.C545, 31, 650, 30, 0.6f, 1.3f, C, 3000, true, Finish.Black, Finish.Plum, 0.41f, "Low-recoil 5.45 rifle with plum polymer furniture.");
            G("vostokm", "Vostok M", GunClass.Rifle, GunStyle.AK, FireAction.Auto, Caliber.C762, 40, 600, 30, 1.0f, 1.5f, C, 3200, true, Finish.Black, Finish.Wood, 0.41f, "Wood-furniture 7.62 classic. Hits like a truck.");
            G("comrade", "Comrade SKS", GunClass.Rifle, GunStyle.Bolt, FireAction.Semi, Caliber.C762, 42, 300, 10, 0.9f, 0.8f, C, 1600, false, Finish.Gray, Finish.Wood, 0.52f, "Semi-auto wood carbine with a fixed magazine.");
            G("sentinel", "Sentinel M4", GunClass.Rifle, GunStyle.AR, FireAction.Auto, Caliber.C556, 32, 800, 30, 0.6f, 1.0f, U, 4200, true, Finish.Black, Finish.Black, 0.37f, "The standard. Modular, light, accurate.");
            G("fennec", "Fennec Bullpup", GunClass.Rifle, GunStyle.Bullpup, FireAction.Auto, Caliber.C556, 32, 700, 30, 0.55f, 0.9f, U, 4600, true, Finish.Od, Finish.Od, 0.45f, "Compact bullpup with a long barrel.");
            G("hailstorm", "Hailstorm A2", GunClass.Rifle, GunStyle.AR, FireAction.Burst, Caliber.C556, 33, 850, 30, 0.6f, 0.8f, U, 3800, true, Finish.Black, Finish.Black, 0.5f, "Three-round-burst rifleman's rifle.");
            G("corsair", "Corsair CQB", GunClass.Rifle, GunStyle.AR, FireAction.Auto, Caliber.C556, 30, 850, 30, 0.62f, 1.15f, U, 3600, true, Finish.Fde, Finish.Fde, 0.26f, "Short-barrel 5.56 carbine for room clearing.");
            G("vostoksu", "Vostok SU", GunClass.Rifle, GunStyle.AK, FireAction.Auto, Caliber.C545, 29, 700, 30, 0.6f, 1.4f, U, 2900, true, Finish.Black, Finish.Black, 0.22f, "Stubby folding-stock 5.45. Loud and handy.");
            G("tundra12", "Tundra AK-12", GunClass.Rifle, GunStyle.AK, FireAction.Auto, Caliber.C545, 31, 700, 30, 0.5f, 1.1f, R, 5400, true, Finish.Gray, Finish.Black, 0.41f, "Modern railed AK. Soft-shooting and accurate.");
            G("tundra", "Tundra AN-9", GunClass.Rifle, GunStyle.AK, FireAction.Burst, Caliber.C545, 33, 1000, 30, 0.45f, 0.9f, R, 5200, true, Finish.Black, Finish.Black, 0.42f, "Hyper-burst rifle. Two rounds before the recoil arrives.");
            G("spectre", "Spectre ACR", GunClass.Rifle, GunStyle.AR, FireAction.Auto, Caliber.C556, 33, 750, 30, 0.5f, 0.9f, R, 5800, true, Finish.Fde, Finish.Black, 0.4f, "Adaptive combat rifle with an ambi chassis.");
            G("reaper", "Reaper 416", GunClass.Rifle, GunStyle.AR, FireAction.Auto, Caliber.C556, 34, 850, 30, 0.5f, 0.85f, R, 6400, true, Finish.Tan, Finish.Black, 0.36f, "Piston-driven premium carbine.");
            G("ironclad", "Ironclad X", GunClass.Rifle, GunStyle.AR, FireAction.Auto, Caliber.C556, 36, 900, 40, 0.4f, 0.7f, E, 8800, true, Finish.Gray, Finish.Tan, 0.38f, "Prototype rifle with a 40-round mag.");
            G("oblivion", "Oblivion X-9", GunClass.Rifle, GunStyle.AK, FireAction.Auto, Caliber.C762, 44, 800, 40, 0.55f, 0.6f, V, 16000, true, Finish.Black, Finish.Gold, 0.42f, "Vault-only. Black and gold 7.62 monster.");

            // ---------------- BATTLE RIFLES / DMRs (6)
            G("grizzly", "Grizzly G3", GunClass.BattleRifle, GunStyle.Battle, FireAction.Auto, Caliber.C308, 52, 550, 20, 1.2f, 1.0f, U, 5200, true, Finish.Black, Finish.Od, 0.45f, "Heavy roller-delayed battle rifle.");
            G("longshot", "Longshot 14", GunClass.DMR, GunStyle.Battle, FireAction.Semi, Caliber.C308, 58, 400, 20, 1.1f, 0.4f, U, 4800, true, Finish.Gray, Finish.Wood, 0.56f, "Wood-stocked marksman rifle.");
            G("bastion", "Bastion 17H", GunClass.BattleRifle, GunStyle.Battle, FireAction.Auto, Caliber.C308, 54, 600, 20, 1.1f, 0.8f, R, 7400, true, Finish.Fde, Finish.Black, 0.41f, "Modern 7.62 rifle with a folding stock.");
            G("bulwark", "Bulwark 308", GunClass.BattleRifle, GunStyle.Battle, FireAction.Auto, Caliber.C308, 50, 600, 20, 1.15f, 0.95f, R, 7800, true, Finish.Tan, Finish.Black, 0.43f, "Tan 7.62 battle rifle. Slow, heavy, punches through plates.");
            G("vigil", "Vigil DMR", GunClass.DMR, GunStyle.AR, FireAction.Semi, Caliber.C308, 62, 450, 20, 0.95f, 0.3f, R, 7000, true, Finish.Black, Finish.Fde, 0.5f, "Precision gas gun. One shot, one problem solved.");
            G("sentry", "Sentry SR-25", GunClass.DMR, GunStyle.AR, FireAction.Semi, Caliber.C308, 60, 450, 20, 1.0f, 0.3f, R, 7600, true, Finish.Fde, Finish.Fde, 0.5f, "Semi-auto 7.62 marksman AR with a cheek riser.");
            G("lynx", "Lynx Marksman", GunClass.DMR, GunStyle.Bolt, FireAction.Semi, Caliber.C308, 66, 350, 10, 1.3f, 0.25f, E, 8200, true, Finish.Black, Finish.Wood, 0.62f, "Long semi-auto marksman rifle with a skeleton stock.");
            G("executioner", "Executioner .50", GunClass.DMR, GunStyle.AntiMat, FireAction.Semi, Caliber.C50BMG, 140, 180, 5, 3.0f, 0.2f, V, 22000, true, Finish.Black, Finish.Tan, 0.74f, "Vault-only. Semi-auto anti-materiel rifle.");

            // ---------------- SNIPERS (5)
            G("volga", "Volga Bolt", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C762, 75, 50, 5, 1.6f, 0.2f, C, 1800, false, Finish.Gray, Finish.Wood, 0.72f, "Ancient but lethal bolt-action.");
            G("hunter", "Bolt Hunter 700", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C308, 82, 50, 5, 1.5f, 0.15f, C, 2400, true, Finish.Black, Finish.Wood, 0.6f, "Hunting rifle with a rail for optics.");
            G("polar", "Polar AW", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C308, 92, 55, 10, 1.4f, 0.1f, R, 9000, true, Finish.Od, Finish.Od, 0.66f, "Arctic-proof precision rifle.");
            G("kodiak", "Kodiak .338", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C338, 115, 45, 5, 2.1f, 0.08f, E, 12000, true, Finish.Tan, Finish.Black, 0.7f, "Magnum precision. Rarely seen, never forgotten.");
            G("leviathan", "Leviathan .50", GunClass.Sniper, GunStyle.AntiMat, FireAction.Bolt, Caliber.C50BMG, 170, 40, 5, 3.2f, 0.06f, V, 26000, true, Finish.Black, Finish.Black, 0.82f, "Vault-only. Bolt-action .50 cal. Overkill is underrated.");

            // more bolt-actions
            G("marsh", "Marsh M44", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C762, 72, 45, 5, 1.7f, 0.22f, C, 1300, false, Finish.Gray, Finish.Wood, 0.48f, "Stubby war-surplus carbine. Iron sights, stripper clips, a big kick.");
            G("ranger", "Ranger Scout", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C308, 78, 55, 5, 1.4f, 0.16f, U, 3200, false, Finish.Black, Finish.Od, 0.5f, "Short, handy scout rifle with proper notch-and-post iron sights.");
            G("prairie", "Prairie Varmint", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C556, 58, 65, 5, 0.75f, 0.1f, C, 1500, true, Finish.Gray, Finish.Wood, 0.66f, "Light 5.56 bolt gun with a rail. Soft kick, quick follow-ups.");
            G("harrier", "Harrier LR", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C338, 120, 45, 5, 2.0f, 0.07f, R, 10500, true, Finish.Black, Finish.Tan, 0.68f, "Long-range .338 chassis rifle with a 5-round box.");
            G("glacier", "Glacier .338", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C338, 118, 45, 5, 2.0f, 0.07f, E, 13000, true, Finish.Gray, Finish.Black, 0.72f, "White-and-grey arctic .338. Reaches across the whole map.");
            G("anzio", "Anzio 20mm", GunClass.Sniper, GunStyle.AntiMat, FireAction.Bolt, Caliber.C20mm, 260, 30, 3, 4.2f, 0.05f, E, 32000, true, Finish.Gray, Finish.Black, 0.95f, "A 20mm anti-materiel rifle. Punches straight through a wall and whoever is behind it. Kicks like a car crash.");

            // ---------------- BOSS-ONLY GUNS: only a map boss carries one; kill the boss to take it
            var lancet = G("boss_lancet", "Kessler's Lancet", GunClass.SMG, GunStyle.SmgVector, FireAction.Auto, Caliber.C57, 30, 1100, 40, 0.3f, 0.7f, Rarity.Boss, 18000, true, Finish.Chrome, Finish.Black, 0.3f, "Boss-only (Doctor Kessler, Underground). Chrome, integrally suppressed, 1100 rounds a minute.");
            lancet.suppressed = true;
            var anchor = G("boss_anchor", "Harbormaster's Anchor", GunClass.Shotgun, GunStyle.BoxShotgun, FireAction.Auto, Caliber.C12g, 16, 360, 20, 1.4f, 3.6f, Rarity.Boss, 20000, true, Finish.Gray, Finish.Wood, 0.46f, "Boss-only (The Harbormaster, Dockyard). Full-auto drum shotgun with a wooden deck stock.");
            anchor.drum = true;
            G("boss_longbow", "Warden's Longbow", GunClass.Sniper, GunStyle.Bolt, FireAction.Bolt, Caliber.C338, 135, 55, 5, 1.8f, 0.05f, Rarity.Vault, 24000, true, Finish.Od, Finish.Wood, 0.78f, "Vault find (The Warden is gone). Hand-built .338 that never misses.");
            // the Thompson: the air-drop boss's gun - one of the best guns in the game. Hits like a rifle at SMG speed,
            // 100-round drum, very accurate for a sub-gun
            var tommy = G("boss_tommy", "Chicago Typewriter", GunClass.SMG, GunStyle.Tommy, FireAction.Auto, Caliber.C45, 44, 800, 100, 0.42f, 0.55f, Rarity.Boss, 30000, true, Finish.Black, Finish.Wood, 0.3f, "Boss-only (The Courier, from the air drop). A .45 Thompson with a finned barrel, Cutts compensator and a 100-round drum. One of the best guns there is.");
            tommy.drum = true;
            G("boss_parachute", "Golden Parachute", GunClass.Rifle, GunStyle.Bullpup, FireAction.Auto, Caliber.C762, 46, 750, 30, 0.55f, 0.6f, Rarity.Vault, 26000, true, Finish.Gold, Finish.Black, 0.48f, "Vault find (The Banker is gone). Gold-plated 7.62 bullpup.");

            // ---------------- LMGs (4)
            var lan = G("lancer", "Lancer RPK", GunClass.LMG, GunStyle.AK, FireAction.Auto, Caliber.C545, 31, 600, 75, 0.6f, 1.4f, U, 6200, true, Finish.Black, Finish.Wood, 0.59f, "Squad support AK with a 75-round drum.");
            lan.drum = true;
            G("brute", "Brute 249", GunClass.LMG, GunStyle.Lmg, FireAction.Auto, Caliber.C556, 32, 750, 100, 0.65f, 1.6f, R, 9800, true, Finish.Black, Finish.Fde, 0.47f, "Belt-fed suppression machine.");
            G("anvil", "Anvil LMG", GunClass.LMG, GunStyle.Lmg, FireAction.Auto, Caliber.C556, 31, 800, 100, 0.6f, 1.7f, E, 10500, true, Finish.Od, Finish.Black, 0.45f, "Lightweight belt-fed 5.56 in olive drab.");
            G("hammer", "Hammer PKM", GunClass.LMG, GunStyle.Lmg, FireAction.Auto, Caliber.C308, 48, 650, 100, 1.0f, 1.6f, E, 11500, true, Finish.Gray, Finish.Wood, 0.6f, "General-purpose 7.62 machine gun.");
            G("goliath", "Goliath MG3", GunClass.LMG, GunStyle.Lmg, FireAction.Auto, Caliber.C308, 50, 1200, 100, 1.1f, 1.8f, V, 20000, true, Finish.Black, Finish.Black, 0.56f, "Vault-only. Buzzsaw. 1200 rounds a minute.");

            // ---------------- ATTACHMENTS (attachment boxes, crates, Mara): bring one to the gun and let go
            var supC = X("sup_compact", "Compact Suppressor", AttachSlot.Muzzle, 0, U, 1800, 0.92f, 1f, Finish.Black, "Short can for pistol-calibre guns (pistols, SMGs). Quiet shots, small flash, raiders only hear you up close.");
            var supL = X("sup_long", "Long Suppressor", AttachSlot.Muzzle, 1, R, 3600, 0.85f, 0.9f, Finish.Fde, "Full-length can for rifle-calibre guns (rifles, DMRs, bolt-actions). Quiet, and it tames recoil a little.");
            supC.suppress = true; supL.suppress = true;   // (forgotten before: the cans fitted but shots stayed loud)
            X("brake_muzzle", "Muzzle Brake", AttachSlot.Muzzle, 2, C, 600, 0.75f, 1f, Finish.Black, "Vents gas sideways: much less kick, much louder.");
            X("comp_triport", "Tri-Port Compensator", AttachSlot.Muzzle, 3, U, 1100, 0.8f, 0.92f, Finish.Gray, "Keeps the muzzle down and the shots tight.");
            var wml = X("light_wml", "Weapon Light", AttachSlot.Side, 0, C, 700, 1f, 1f, Finish.Black, "Bright light on the rail. On while you hold the gun. Great in the Underground.");
            wml.light = true;
            var lr = X("laser_red", "Red Laser", AttachSlot.Side, 1, U, 900, 1f, 0.85f, Finish.Black, "Red aiming laser. On while you hold the gun.");
            lr.laser = true; lr.beam = new Color(1f, 0.08f, 0.05f);
            var lg = X("laser_green", "Green Laser", AttachSlot.Side, 1, R, 1600, 1f, 0.8f, Finish.Od, "Brighter green aiming laser.");
            lg.laser = true; lg.beam = new Color(0.15f, 1f, 0.2f);
            var peq = X("combo_peq", "PEQ Light + Laser", AttachSlot.Side, 2, E, 4200, 1f, 0.8f, Finish.Fde, "Light and green laser in one box.");
            peq.light = true; peq.laser = true; peq.beam = new Color(0.15f, 1f, 0.2f);
            X("grip_vertical", "Vertical Grip", AttachSlot.Grip, 0, C, 400, 0.85f, 1f, Finish.Black, "Under the handguard. Less recoil.");
            X("grip_angled", "Angled Grip", AttachSlot.Grip, 1, U, 900, 0.82f, 0.9f, Finish.Fde, "Angled foregrip. Less recoil, tighter shots.");
            X("grip_stubby", "Stubby Grip", AttachSlot.Grip, 2, C, 300, 0.9f, 1f, Finish.Od, "A short grip that barely gets in the way.");

            // ---------------- SIGHTS (found in attachment boxes)
            S("holo_ex3", "EX3 Holographic", 0, new Color(4f, 0.25f, 0.15f), 0, 0.012f, U, 900, Finish.Black, "Classic 65 MOA ring + dot holographic sight.");
            S("holo_ex3g", "EX3 Holo (Green)", 0, new Color(0.4f, 4f, 0.5f), 0, 0.012f, R, 1400, Finish.Fde, "Green-reticle holographic. Easier on the eyes.");
            S("micro_dot", "Micro Dot T2", 1, new Color(4f, 0.2f, 0.12f), 1, 0.01f, C, 500, Finish.Black, "Tiny tube red dot. Fits pistols and rifles.");
            S("reflex_pan", "Panorama Reflex", 2, new Color(4f, 0.3f, 0.1f), 1, 0.01f, C, 450, Finish.Black, "Open reflex sight with a wide window.");
            S("tac_reflex", "Tac Reflex", 4, new Color(4f, 2.2f, 0.2f), 3, 0.014f, R, 1700, Finish.Od, "Amber chevron reflex. Fast target pickup.");
            S("prism_3x", "Combat 3x Prism", 7, new Color(4f, 0.3f, 0.15f), 2, 0f, U, 2200, Finish.Black, "3x prism sight with a glowing red chevron. Short and quick.").mag = 3f;
            S("scope_4x", "Hunter 4x Scope", 5, Color.black, 0, 0f, R, 3500, Finish.Black, "4x hunting scope with a duplex crosshair. Made for bolt-actions.").mag = 4f;
            S("scope_8x", "Longshot 8x Scope", 6, new Color(3f, 0.2f, 0.1f), 1, 0f, E, 7000, Finish.Tan, "8x long-range scope, mil-dot crosshair with a red centre dot.").mag = 8f;
            S("hex_holo", "Hex Holo Mk.II", 3, new Color(0.3f, 2.8f, 4f), 2, 0.016f, V, 6000, Finish.Gray, "Vault-only. Cyan chevron hex holographic.");

            // ---------------- MEDS
            var bandage = M("bandage", "Bandage", ItemKind.Med, C, 120, 0, new Color(0.9f, 0.88f, 0.82f), "Stops bleeding - the only way besides a medkit, it doesn't stop on its own. Hold it to your chest and squeeze trigger.");
            bandage.stopsBleed = true; bandage.heal = 5;
            var medkit = M("medkit", "Field Medkit", ItemKind.Med, U, 600, 1, new Color(0.75f, 0.12f, 0.1f), "Heals 60 HP over a few seconds and stops bleeding.");
            medkit.heal = 60; medkit.stopsBleed = true; medkit.healTime = 4f;
            var pills = M("painkillers", "Painkillers", ItemKind.Med, C, 180, 3, new Color(0.9f, 0.45f, 0.1f), "Quick 15 HP. Pop them at your mouth.");
            pills.heal = 15; pills.healTime = 1f;
            var ifak = M("ifak", "IFAK", ItemKind.Med, U, 900, 4, new Color(0.3f, 0.34f, 0.24f), "Individual first aid kit: 40 HP and stops bleeding.");
            ifak.heal = 40; ifak.stopsBleed = true; ifak.healTime = 3f;
            var trauma = M("trauma_kit", "Trauma Kit", ItemKind.Med, R, 2400, 1, new Color(0.15f, 0.35f, 0.75f), "Surgical trauma kit: heals 90 HP over a few seconds.");
            trauma.heal = 90; trauma.stopsBleed = true; trauma.healTime = 5f;
            var stim = M("stim", "Adrenal Stim", ItemKind.Med, R, 1200, 2, new Color(0.2f, 0.75f, 0.9f), "Syringe. Press it to a broken arm and pull the trigger to fix it - nothing else does. Or at your chest: instant 30 HP and a burst of stamina.");
            // night vision goggles: put them on your head, flip them down with an empty hand at your forehead + trigger
            M("cc_cam", "Camera", ItemKind.Valuable, Rarity.Epic, 6000, 0, new Color(0.08f, 0.08f, 0.09f), "A helmet camera that records video. Hold it to your helmet and let go to clip it on (right side). One button: reach to it with an empty hand and pull the trigger to RECORD / stop (desktop: C). Grab it to take it off.");
            M("cc_trophy", "CC Trophy", ItemKind.Valuable, Rarity.Vault, 1, 0, new Color(0.95f, 0.75f, 0.2f), "The Content Creator trophy: a gold cup with a play button on it, handed out by the owner to the people who make videos of the game. Put it on show in your bunker.");
            M("nvg", "PNV-10 Night Vision", ItemKind.Valuable, Rarity.Epic, 9500, 0, new Color(0.18f, 0.2f, 0.17f), "Night vision goggles. Hold them to your head and let go to wear them; reach to your forehead with an empty hand and pull the trigger to flip them down / up (desktop: V). Grab at your forehead to take them off.");
            // energy drinks: hold the can to your mouth and pull the trigger
            var volt = M("energy_volt", "Volt Energy", ItemKind.Med, C, 260, 5, new Color(0.35f, 0.85f, 0.2f), "Energy drink. Full stamina and faster sprinting for 30 s, +5 HP.");
            volt.heal = 5; volt.energy = 30f;
            var rush = M("energy_rush", "Red Rush", ItemKind.Med, U, 420, 5, new Color(0.85f, 0.12f, 0.12f), "Strong energy drink. Full stamina and faster sprinting for 60 s, +10 HP.");
            rush.heal = 10; rush.energy = 60f;
            // DUBBY (a sponsor): slim 355 ml cans, the label's "ZERO SUGAR" box reads SPONSOR
            var dsb = M("energy_dubby_sb", "Dubby Strawberry Banana", ItemKind.Med, U, 480, 6, new Color(0.95f, 0.22f, 0.6f), "DUBBY Focus Energy Drink - Strawberry Banana. Crack it open at your mouth: FOCUS for 60 s - faster sprinting, stamina refills twice as fast, a third less recoil - plus full stamina and +10 HP.");
            dsb.heal = 10; dsb.energy = 60f; dsb.focus = 60f;
            var dmo = M("energy_dubby_mo", "Dubby Mango Orange Cream", ItemKind.Med, U, 480, 7, new Color(0.98f, 0.8f, 0.15f), "DUBBY Focus Energy Drink - Mango Orange Cream. Crack it open at your mouth: FOCUS for 60 s - faster sprinting, stamina refills twice as fast, a third less recoil - plus full stamina and +10 HP.");
            dmo.heal = 10; dmo.energy = 60f; dmo.focus = 60f;
            var cola = M("energy_nuke", "Nuke Cola", ItemKind.Med, C, 160, 5, new Color(0.15f, 0.15f, 0.18f), "Fizzy, sugary, mildly radioactive. Full stamina and 15 s of faster sprinting."); 
            cola.heal = 3; cola.energy = 15f;
            // throwables + knives
            M("frag", "M67 Frag Grenade", ItemKind.Throwable, U, 900, 0, new Color(0.3f, 0.34f, 0.22f), "Pull the trigger to pull the pin, then throw. Goes off 3.6 s later. Lethal within a few metres.");
            M("flash", "M84 Flashbang", ItemKind.Throwable, U, 650, 1, new Color(0.55f, 0.57f, 0.58f), "Pull the pin and throw. Blinds and deafens anyone looking at it - raiders can't see or shoot for a few seconds.");
            var knife = M("knife", "Combat Knife", ItemKind.Melee, C, 450, 0, new Color(0.15f, 0.15f, 0.15f), "Swing or stab. The harder you swing, the deeper it goes.");
            knife.heal = 45f;
            var hunt = M("knife_hunt", "Hunting Knife", ItemKind.Melee, U, 1100, 1, new Color(0.42f, 0.28f, 0.16f), "Longer, heavier blade. Hits harder.");
            hunt.heal = 62f;
            M("impact", "RGN Impact Grenade", ItemKind.Throwable, R, 1400, 2, new Color(0.22f, 0.27f, 0.2f), "Pull the pin and throw: it goes off the moment it hits something (or 8 s after the pin, whichever comes first). Don't drop it at your feet.");
            // more melee (damage in the "heal" slot, like the knives): shape picks the model
            void Mel(string id, string nm, Rarity r, int price, int shape, float dmg, Color col, string desc) { var m = M(id, nm, ItemKind.Melee, r, price, shape, col, desc); m.heal = dmg; }
            Mel("bat", "Baseball Bat", C, 600, 2, 55f, new Color(0.62f, 0.45f, 0.28f), "Ash wood, taped grip. Swing it like you mean it - knocks raiders off their feet.");
            Mel("crowbar", "Crowbar", C, 500, 3, 48f, new Color(0.6f, 0.08f, 0.06f), "Opens crates, opens heads. Good against padlocks.");
            Mel("machete", "Machete", U, 1200, 4, 62f, new Color(0.15f, 0.15f, 0.15f), "A long, heavy blade. Big swings, deep cuts.");
            Mel("sledge", "Sledgehammer", R, 1900, 5, 88f, new Color(0.25f, 0.25f, 0.27f), "Slow and heavy - one good swing drops most raiders. Smashes padlocks.");
            Mel("fireaxe", "Fire Axe", R, 1700, 6, 76f, new Color(0.75f, 0.1f, 0.08f), "Red, sharp and heavy.");
            Mel("pan", "Frying Pan", C, 350, 7, 40f, new Color(0.12f, 0.12f, 0.12f), "Cast iron. The sound it makes is worth it.");
            Mel("candycane", "Candy Cane", U, 1500, 8, 50f, new Color(0.9f, 0.15f, 0.15f), "A festive club. Peppermint scented. (Holiday Pack)");
            stim.heal = 30;

            // ---------------- AMMO (adds to your reserve when stashed / picked up)
            Ammo("ammo_9", Caliber.C9mm, 60, 90); Ammo("ammo_45", Caliber.C45, 50, 110); Ammo("ammo_57", Caliber.C57, 60, 160);
            Ammo("ammo_556", Caliber.C556, 60, 200); Ammo("ammo_545", Caliber.C545, 60, 180); Ammo("ammo_762", Caliber.C762, 60, 220);
            Ammo("ammo_308", Caliber.C308, 40, 300); Ammo("ammo_338", Caliber.C338, 15, 450); Ammo("ammo_50", Caliber.C50BMG, 10, 600);
            Ammo("ammo_12g", Caliber.C12g, 24, 140); Ammo("ammo_50ae", Caliber.C50AE, 21, 260); Ammo("ammo_357", Caliber.C357, 24, 160);
            Ammo("ammo_20", Caliber.C20mm, 6, 900);
            // special rounds: never sold - you find them in raids. The box says exactly what's in it.
            foreach (Caliber c in System.Enum.GetValues(typeof(Caliber)))
            {
                if (c == Caliber.C12g) continue;   // no AP / incendiary buckshot
                int n = c == Caliber.C20mm ? 3 : c == Caliber.C50BMG || c == Caliber.C338 ? 5 : c == Caliber.C308 ? 10 : 20;
                SpecialAmmo("ammo_ap_" + c, c, n, AmmoType.AP);
                SpecialAmmo("ammo_inc_" + c, c, n, AmmoType.Incendiary);
            }

            // ---------------- VALUABLES (sell to the Fence)
            // junk & barter loot: sell it to the Fence (cheap scrap up to jackpots)
            M("junk_scrap", "Scrap Metal", ItemKind.Valuable, C, 40, 8, new Color(0.45f, 0.42f, 0.38f), "Worth almost nothing. Almost.");
            M("junk_tin", "Tin Can", ItemKind.Valuable, C, 25, 9, new Color(0.7f, 0.7f, 0.72f), "Empty. Someone ate the beans.");
            M("junk_cigs", "Cigarettes", ItemKind.Valuable, C, 150, 10, new Color(0.85f, 0.15f, 0.12f), "Currency of the wasteland.");
            M("junk_lighter", "Zippo Lighter", ItemKind.Valuable, C, 220, 11, new Color(0.75f, 0.75f, 0.78f), "Still sparks.");
            M("junk_battery", "Battery Pack", ItemKind.Valuable, C, 300, 12, new Color(0.2f, 0.25f, 0.2f), "Always in demand.");
            M("junk_whiskey", "Whiskey Bottle", ItemKind.Valuable, U, 650, 13, new Color(0.55f, 0.3f, 0.1f), "Aged twelve years. Mostly full.");
            M("val_phone", "Smartphone", ItemKind.Valuable, U, 950, 14, new Color(0.08f, 0.08f, 0.1f), "Cracked screen, good parts.");
            M("val_radio", "Military Radio", ItemKind.Valuable, R, 3500, 15, new Color(0.3f, 0.33f, 0.24f), "Encrypted military handset. In a raid, hold it and pull the trigger to call in an AIR DROP where you stand (not underground). Found only at a few spots on each map.");
            // more junk: cheap stuff lying around everywhere, sells for a little at the Exchange
            M("junk_tape", "Duct Tape", ItemKind.Valuable, C, 120, 20, new Color(0.55f, 0.56f, 0.58f), "Fixes everything. Traders love it.");
            M("junk_screws", "Box of Screws", ItemKind.Valuable, C, 70, 21, new Color(0.6f, 0.45f, 0.25f), "Rattles when you shake it.");
            M("junk_wire", "Copper Wire", ItemKind.Valuable, C, 180, 22, new Color(0.85f, 0.5f, 0.25f), "A coil of salvaged copper.");
            M("junk_bulb", "Light Bulb", ItemKind.Valuable, C, 45, 23, new Color(0.95f, 0.95f, 0.85f), "Might still work. Probably doesn't.");
            M("junk_tp", "Toilet Paper", ItemKind.Valuable, C, 90, 24, new Color(0.95f, 0.95f, 0.93f), "Worth more than gold, some days.");
            M("junk_matches", "Matches", ItemKind.Valuable, C, 35, 25, new Color(0.75f, 0.2f, 0.15f), "A full box. Keep them dry.");
            M("junk_bolts", "Bolts", ItemKind.Valuable, C, 55, 26, new Color(0.5f, 0.5f, 0.52f), "Heavy hex bolts. Someone needs these.");
            M("junk_carbattery", "Car Battery", ItemKind.Valuable, U, 520, 27, new Color(0.15f, 0.15f, 0.16f), "Heavy, but still holds a charge.");
            M("junk_board", "Circuit Board", ItemKind.Valuable, U, 380, 28, new Color(0.1f, 0.45f, 0.2f), "Pulled from something electronic.");
            M("junk_coffee", "Instant Coffee", ItemKind.Valuable, C, 140, 29, new Color(0.35f, 0.2f, 0.12f), "Half a jar. Smells like heaven.");
            M("junk_tags", "Dog Tags", ItemKind.Valuable, U, 450, 30, new Color(0.75f, 0.75f, 0.78f), "Somebody's name. Somebody's story.");
            M("junk_wrench", "Rusty Wrench", ItemKind.Valuable, C, 95, 31, new Color(0.5f, 0.35f, 0.25f), "Seized up, but it's good steel.");
            // (3.64) streamer drops: only ever in the once-a-raid STREAMER BOX (sb_ ids are kept out of every other loot pool)
            M("sb_oliver", "Oliver Kicks", ItemKind.Valuable, E, 7500, 36, new Color(0.07f, 0.07f, 0.08f), "A streamer drop: OLIVER's signature high-tops - black and purple with a gold lightning bolt, OLIVER on the side and a glowing mint sole. Find the pair.");
            M("sb_skid", "S1KDM0R3 Statue", ItemKind.Valuable, E, 9500, 37, new Color(0.05f, 0.05f, 0.06f), "A streamer drop: S1KDM0R3 - the grim reaper, hooded, scythe in hand, eyes glowing purple, on the LIVE display base. Collectors pay a fortune.");
            M("val_oways", "Oways Cereal", ItemKind.Valuable, E, 8000, 35, new Color(0.95f, 0.55f, 0.1f), "A streamer collectible: OWAYS CEREAL, the streamer's own cereal - a sealed box of crunchy rings with the LIVE sticker on the front. Collectors pay a fortune.");
            M("val_shush", "Shushed Figurine", ItemKind.Valuable, E, 9000, 33, new Color(0.08f, 0.08f, 0.09f), "A streamer collectible: SHUSHED, the operator in full Juggernaut armour, red helmet on, one finger to his lips - shhh. Collectors pay a fortune.");
            M("dog_ball", "Tennis Ball", ItemKind.Valuable, C, 15, 34, new Color(0.8f, 0.92f, 0.2f), "Manda's favourite. Throw it and she'll fetch it.");
            M("val_ring", "Silver Ring", ItemKind.Valuable, U, 1100, 16, new Color(0.85f, 0.85f, 0.9f), "Engraved. Not yours.");
            M("val_laptop", "Rugged Laptop", ItemKind.Valuable, R, 6500, 17, new Color(0.25f, 0.27f, 0.25f), "Field laptop full of data.");
            M("val_idol", "Gold Idol", ItemKind.Valuable, E, 16000, 18, new Color(1f, 0.75f, 0.25f), "A small golden statue. Huge payday.");
            M("val_crown", "Crown Jewel", ItemKind.Valuable, V, 45000, 19, new Color(0.9f, 0.15f, 0.25f), "Vault-only. The best thing you can carry out.");
            M("val_chain", "Gold Chain", ItemKind.Valuable, C, 900, 0, new Color(1f, 0.78f, 0.3f), "Gaudy, heavy, worth something.");
            M("val_watch", "Luxury Watch", ItemKind.Valuable, U, 1800, 1, new Color(0.8f, 0.8f, 0.85f), "Still ticking.");
            M("val_drive", "Encrypted Drive", ItemKind.Valuable, U, 2200, 2, new Color(0.15f, 0.15f, 0.18f), "Someone wants what's on this.");
            M("val_intel", "Intel Folder", ItemKind.Valuable, R, 4500, 3, new Color(0.65f, 0.55f, 0.35f), "Classified documents. The Fence pays well.");
            M("val_gpu", "Graphics Card", ItemKind.Valuable, R, 5200, 4, new Color(0.2f, 0.22f, 0.25f), "High-end GPU. Worth its weight in gold.");
            M("val_wallet", "Cold Wallet", ItemKind.Valuable, E, 9000, 5, new Color(0.9f, 0.5f, 0.1f), "Crypto hardware wallet. Jackpot.");
            M("val_goldbar", "Gold Bar", ItemKind.Valuable, V, 15000, 6, new Color(1f, 0.75f, 0.25f), "Vault-only. Pure gold.");
            M("val_diamond", "Blue Diamond", ItemKind.Valuable, V, 24000, 7, new Color(0.4f, 0.75f, 1f), "Vault-only. Flawless.");

            // ---------------- KEYS
            // ---------------- ARMOR (nobody starts with it: find it in raids)
            var olive = new Color(0.33f, 0.36f, 0.26f); var tan = new Color(0.62f, 0.54f, 0.4f); var blk = new Color(0.13f, 0.13f, 0.14f); var gray = new Color(0.36f, 0.38f, 0.38f); var gold = new Color(0.85f, 0.65f, 0.2f);
            // six tiers each: T1 stops fragments and pistol rounds sometimes ... T6 shrugs off most rifle fire (vault-only)
            T(1, "helm_bump", "T1 Scout Bump Helmet", ArmorSlot.Helmet, C, 450, tan, blk, "Tier 1. A light vented shell. Stops fragments and the odd pistol round.");
            T(2, "helm_ballistic", "T2 Patrol Helmet", ArmorSlot.Helmet, U, 1400, olive, blk, "Tier 2. A full-cut covered helmet with goggles and ear protection. Stops most pistol rounds.");
            T(3, "helm_fast", "T3 Raptor High-Cut", ArmorSlot.Helmet, R, 3400, blk, gray, "Tier 3. High-cut with rails, comms, a helmet light and an NVG mount. Stops pistols and buckshot.");
            T(4, "helm_ronin", "T4 Ronin Mandible Helmet", ArmorSlot.Helmet, R, 5800, gray, blk, "Tier 4. High-cut with armoured ear plates and a bolted jaw guard. Stops most rifle rounds some of the time.");
            T(5, "helm_visor", "T5 Vanguard Visor Helmet", ArmorSlot.Helmet, E, 9000, new Color(0.24f, 0.27f, 0.22f), blk, "Tier 5. A heavy full-cut helmet with a wide ballistic face visor.");
            T(6, "helm_jugg", "T6 Titan Juggernaut Helm", ArmorSlot.Helmet, V, 16000, blk, gold, "Tier 6 - vault-only. A full-face helm with a glowing eye slit. Only the heaviest rounds get through.");
            T(1, "vest_soft", "T1 Soft Shell Vest", ArmorSlot.Vest, C, 550, olive, blk, "Tier 1. Concealable quilted soft armour.");
            T(2, "vest_carrier", "T2 Light Plate Carrier", ArmorSlot.Vest, U, 1900, tan, olive, "Tier 2. A slick plate carrier with three low-profile mag pouches.");
            T(3, "vest_ranger", "T3 Ranger Chest Rig", ArmorSlot.Vest, R, 3600, new Color(0.42f, 0.4f, 0.3f), olive, "Tier 3. Plates under a full chest rig: four rifle mags, pistol mags, a radio and a tourniquet.");
            T(4, "vest_heavy", "T4 Heavy Plate Carrier", ArmorSlot.Vest, R, 6000, olive, blk, "Tier 4. Thick plates, side plates, shoulder armour and a collar.");
            T(5, "vest_assault", "T5 Assault Armor", ArmorSlot.Vest, E, 9800, blk, gray, "Tier 5. Full torso coverage: groin flap, layered shoulder armour and a throat guard.");
            // boss-only armour (worn by the map bosses, dropped when they die)
            A("boss_helm_courier", "Courier's Steel Pot", ArmorSlot.Helmet, 0.55f, 1, Rarity.Boss, 14000, new Color(0.16f, 0.16f, 0.18f), new Color(0.45f, 0.08f, 0.08f), "Boss-only (The Courier, air drops). Charcoal steel helmet with a red band.");
            A("boss_vest_courier", "Courier's Drop Rig", ArmorSlot.Vest, 0.6f, 3, Rarity.Boss, 17000, new Color(0.16f, 0.16f, 0.18f), new Color(0.45f, 0.08f, 0.08f), "Boss-only (The Courier, air drops). Heavy charcoal plate carrier.");
            A("boss_helm_kessler", "Kessler's Hazard Visor", ArmorSlot.Helmet, 0.55f, 3, Rarity.Boss, 14000, new Color(0.85f, 0.86f, 0.84f), new Color(0.75f, 0.12f, 0.1f), "Boss-only. A lab-white visor helmet with a red cross.");
            A("boss_vest_kessler", "Kessler's Lab Plate", ArmorSlot.Vest, 0.58f, 3, Rarity.Boss, 16000, new Color(0.85f, 0.86f, 0.84f), new Color(0.75f, 0.12f, 0.1f), "Boss-only. White full-torso plates.");
            A("boss_helm_harbor", "Harbormaster's Dive Helm", ArmorSlot.Helmet, 0.6f, 4, Rarity.Boss, 15000, new Color(0.62f, 0.3f, 0.12f), new Color(0.8f, 0.62f, 0.3f), "Boss-only. A rust-orange full-face helm with brass trim.");
            A("boss_vest_harbor", "Harbormaster's Deck Armor", ArmorSlot.Vest, 0.55f, 2, Rarity.Boss, 15000, new Color(0.7f, 0.32f, 0.1f), new Color(0.15f, 0.15f, 0.16f), "Boss-only. Heavy orange plates and shoulder guards.");
            A("boss_helm_warden", "Warden's Ranger Helmet", ArmorSlot.Helmet, 0.5f, 1, V, 12000, new Color(0.2f, 0.3f, 0.16f), new Color(0.38f, 0.28f, 0.16f), "Vault find (The Warden is gone). Forest-green helmet with ear protection.");
            A("boss_vest_warden", "Warden's Field Rig", ArmorSlot.Vest, 0.55f, 1, V, 13000, new Color(0.2f, 0.3f, 0.16f), new Color(0.38f, 0.28f, 0.16f), "Vault find (The Warden is gone). Green plate carrier, light and quiet.");
            A("boss_helm_banker", "Banker's Gilded Helmet", ArmorSlot.Helmet, 0.55f, 2, V, 16000, new Color(0.1f, 0.1f, 0.11f), new Color(0.9f, 0.7f, 0.2f), "Vault find (The Banker is gone). Black high-cut helmet with gold rails.");
            A("boss_vest_banker", "Banker's Gold Plate", ArmorSlot.Vest, 0.6f, 3, V, 20000, new Color(0.88f, 0.68f, 0.22f), new Color(0.1f, 0.1f, 0.11f), "Vault find (The Banker is gone). Gold-plated assault armour. Very subtle.");
            T(6, "vest_jugg", "T6 Titan Juggernaut Armor", ArmorSlot.Vest, V, 18000, blk, gold, "Tier 6 - vault-only. A massive layered shell with huge pauldrons. Walk through gunfire.");

            // ---------------- BACKPACKS (bigger = more loot out of the raid)
            P("pack_day", "Daypack", new Vector3(0.26f, 0.32f, 0.17f), 1, 0, C, 400, new Color(0.3f, 0.32f, 0.3f), "Small pack. Meds, junk and a pistol. One rifle strap.");
            P("pack_assault", "Assault Pack", new Vector3(0.3f, 0.4f, 0.22f), 1, 1, U, 1500, new Color(0.32f, 0.34f, 0.26f), "Military pack. Fits a helmet or a folded vest. One rifle strap.");
            P("pack_raid", "Raid Pack", new Vector3(0.34f, 0.46f, 0.25f), 2, 2, R, 3800, new Color(0.55f, 0.47f, 0.34f), "Big pack with two rifle straps. An SMG fits standing up.");
            P("pack_tactical", "Tactical Pack", new Vector3(0.38f, 0.52f, 0.27f), 2, 3, R, 5600, new Color(0.14f, 0.14f, 0.15f), "Frame pack with MOLLE and two rifle straps.");
            P("pack_expedition", "Expedition Pack", new Vector3(0.42f, 0.62f, 0.3f), 2, 4, E, 11000, new Color(0.22f, 0.3f, 0.4f), "Tall expedition pack with a hip belt and sleeping mat. Armor, SMGs and a lot of loot.");
            P("pack_mule", "Mule Hauler", new Vector3(0.5f, 0.74f, 0.34f), 2, 5, V, 22000, new Color(0.45f, 0.38f, 0.16f), "Vault-only. Hauling frame pack - even a rifle fits inside.");

            M("gift", "Christmas Present", ItemKind.Valuable, U, 500, 0, new Color(0.75f, 0.08f, 0.08f), "A small wrapped present. Pick it up and HOLD THE TRIGGER to open it - it could be anything from bandages to a top gun... or a live grenade.");
            M("gift_santa", "Santa's Present", ItemKind.Valuable, V, 5000, 0, new Color(0.72f, 0.05f, 0.05f), "A big present. Hold the trigger to open it: a North Pole Keycard is inside, with vault-grade loot.");
            M("key_xmas", "North Pole Keycard", ItemKind.Key, V, 30000, 1, new Color(0.75f, 0.06f, 0.06f), "A big festive keycard. Opens the giant present vault on the North Pole - inside: the best loot in the game and four skins you can't get anywhere else. Only ever found inside Christmas presents - most often on the North Pole.");
            M("key_vault", "Vault Keycard", ItemKind.Key, E, 14000, 0, new Color(0.85f, 0.15f, 0.12f), "Opens any raid vault: hold it to the card reader by the vault door. Found in safes and lying around on desks.");
        }

        /// <summary>Tier armour: protection 0.15 (T1) up to 0.65 (T6) - armour class 1.5 .. 6.5 against the round's penetration.</summary>
        static ArmorDef T(int tier, string id, string name, ArmorSlot slot, Rarity r, int price, Color col, Color trim, string desc)
        {
            var a = A(id, name, slot, 0.05f + tier * 0.1f, tier - 1, r, price, col, trim, desc);
            a.tier = tier; return a;
        }

        static ArmorDef A(string id, string name, ArmorSlot slot, float prot, int style, Rarity r, int price, Color col, Color trim, string desc)
        {
            var a = new ArmorDef { id = id, name = name, slot = slot, protection = prot, style = style, rarity = r, price = price, color = col, trim = trim, kind = ItemKind.Armor, desc = desc };
            Armor.Add(a); Reg(id, a); return a;
        }

        static BackpackDef P(string id, string name, Vector3 inner, int sides, int style, Rarity r, int price, Color col, string desc)
        {
            var b = new BackpackDef { id = id, name = name, inner = inner, sides = sides, style = style, rarity = r, price = price, color = col, kind = ItemKind.Backpack, desc = desc };
            Packs.Add(b); Reg(id, b); return b;
        }

        static void Ammo(string id, Caliber cal, int count, int price)
        {
            var m = M(id, CalName(cal) + " x" + count, ItemKind.Ammo, Rarity.Common, price, 0, new Color(0.35f, 0.4f, 0.28f), "Ammunition. Store it to add to your reserve.");
            m.ammoCal = cal; m.ammoCount = count;
        }

        static void SpecialAmmo(string id, Caliber cal, int count, AmmoType t)
        {
            bool ap = t == AmmoType.AP;
            var m = M(id, CalName(cal) + (ap ? " AP" : " INCENDIARY") + " x" + count, ItemKind.Ammo, ap ? Rarity.Uncommon : Rarity.Rare, ap ? 400 : 550, 0,
                      ap ? new Color(0.12f, 0.12f, 0.14f) : new Color(0.7f, 0.12f, 0.08f),
                      ap ? "Armour-piercing rounds: much better against armour and walls. Store in your mag pouch, then switch to AP there (B / Y)."
                         : "Incendiary rounds: set whoever they hit on fire. Store in your mag pouch, then switch to INCENDIARY there (B / Y).");
            m.ammoCal = cal; m.ammoCount = count; m.ammoType = t;
        }

        public static string AmmoIdFor(Caliber c)
        {
            Init();
            foreach (var m in Misc) if (m.kind == ItemKind.Ammo && m.ammoCal == c && m.ammoType == AmmoType.Standard) return m.id;
            return "ammo_9";
        }

        // ------------------------------------------------------------ loot tables
        public enum LootTable { Crate, WeaponCase, OpticsBox, Vault, NpcBody, Medical, Safe, Armor }

        static float Weight(Rarity r, LootTable t)
        {
            if (r == Rarity.Boss) return 0f;   // only bosses carry these
            if (r == Rarity.Vault) return t == LootTable.Vault ? 10f : 0f;
            if (t == LootTable.Vault) return r == Rarity.Epic ? 3f : r == Rarity.Rare ? 1.5f : 0f;
            switch (r)
            {
                case Rarity.Common: return 60;
                case Rarity.Uncommon: return 26;
                case Rarity.Rare: return t == LootTable.WeaponCase || t == LootTable.Safe ? 10 : 5;
                default: return t == LootTable.WeaponCase || t == LootTable.Safe ? 3 : 0.8f;
            }
        }

        public static ItemDef Roll(LootTable t, Rng rng)
        {
            Init();
            List<ItemDef> pool = new List<ItemDef>();
            switch (t)
            {
                case LootTable.WeaponCase: pool.AddRange(Guns); break;
                case LootTable.OpticsBox: pool.AddRange(Sights); pool.AddRange(Attachments); break;
                case LootTable.Vault:
                    if (rng.Chance(0.55f)) pool.AddRange(Guns.FindAll(g => g.rarity >= Rarity.Epic));
                    else if (rng.Chance(0.35f)) { pool.AddRange(Sights.FindAll(s => s.rarity >= Rarity.Rare)); pool.AddRange(Attachments.FindAll(s => s.rarity >= Rarity.Rare)); }
                    else if (rng.Chance(0.4f)) { pool.AddRange(Armor.FindAll(a => a.rarity >= Rarity.Epic)); pool.AddRange(Packs.FindAll(b => b.rarity >= Rarity.Epic)); }
                    else pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Valuable && m.rarity >= Rarity.Rare));
                    break;
                case LootTable.Armor: pool.AddRange(Armor); if (rng.Chance(0.35f)) pool.AddRange(Packs); break;
                case LootTable.Medical: pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Med)); break;
                case LootTable.Safe: pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Valuable || m.kind == ItemKind.Key)); break;
                case LootTable.NpcBody:
                    pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Med || m.kind == ItemKind.Ammo || m.kind == ItemKind.Throwable || m.kind == ItemKind.Melee || (m.kind == ItemKind.Valuable && m.rarity <= Rarity.Uncommon)));
                    break;
                default:
                    float roll = rng.Value;
                    if (roll < 0.35f) pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Ammo));
                    else if (roll < 0.55f) pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Med));
                    else if (roll < 0.76f) pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Valuable || m.kind == ItemKind.Key));
                    else if (roll < 0.83f) pool.AddRange(Armor);
                    else if (roll < 0.86f) pool.AddRange(Packs);
                    else if (roll < 0.91f) { pool.AddRange(Sights); pool.AddRange(Attachments); }
                    else if (roll < 0.96f) pool.AddRange(Misc.FindAll(m => m.kind == ItemKind.Throwable || m.kind == ItemKind.Melee));
                    else pool.AddRange(Guns.FindAll(g => g.cls == GunClass.Pistol || g.cls == GunClass.Revolver || g.cls == GunClass.SMG));
                    break;
            }
            // vault keycards and military radios only lie at their own spots on each map - never in crates, safes or on bodies
            pool.RemoveAll(d => d.id.StartsWith("sb_"));   // (3.64) streamer drops: the streamer box only
            pool.RemoveAll(d => d.kind == ItemKind.Key || d.id == "val_radio" || d.id == "candycane" || d.id == "key_xmas" || d.id == "gift" || d.id == "gift_santa" || d.id == "cc_trophy");   // the CC trophy: only from the owner
            float total = 0;
            foreach (var d in pool) total += Weight(d.rarity, t) * (d.kind == ItemKind.Key ? 0.08f : 1f);
            if (total <= 0) return Get("bandage");
            float pick = rng.Value * total;
            foreach (var d in pool)
            {
                pick -= Weight(d.rarity, t) * (d.kind == ItemKind.Key ? 0.08f : 1f);
                if (pick <= 0) return d;
            }
            return pool[pool.Count - 1];
        }
    }
}
