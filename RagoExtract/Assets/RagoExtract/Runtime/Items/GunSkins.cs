using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Gun paint jobs from the bunker's paint booth. A skin repaints the gun's body (not its optic, attachments or mag)
    /// with a procedural pattern mapped tri-planar, so it wraps any gun shape. Saved on the gun (ItemData.skin) and sent
    /// with it, so other players see it too.
    /// </summary>
    public static class GunSkins
    {
        public class Skin { public string id, name, pack; public Color spray; public System.Func<Material> mat; }   // pack: a DLC pack that unlocks it (null = free)
        // patterns modelled on real camouflage and real gun coatings, sized for a gun (a few centimetres per blob),
        // painted in the gun's own space so they stay put when it moves, with worn edges showing bare metal
        public static readonly List<Skin> All = new List<Skin>
        {
            new Skin { id = "", name = "Factory finish", spray = new Color(0.25f, 0.25f, 0.26f) },
            new Skin { id = "desert", name = "Flat Dark Earth", spray = new Color(0.6f, 0.5f, 0.36f), mat = () => Coat("desert", new Color(0.6f, 0.5f, 0.36f), 0.22f) },
            new Skin { id = "woodland", name = "M81 Woodland", spray = new Color(0.4f, 0.42f, 0.3f), mat = () => Paint("woodland", Woodland, 3f) },
            new Skin { id = "multi", name = "Multi-Terrain", spray = new Color(0.62f, 0.56f, 0.42f), mat = () => Paint("multi", MultiTerrain, 3.2f) },
            new Skin { id = "arctic", name = "Arctic Splinter", spray = new Color(0.9f, 0.92f, 0.94f), mat = () => Paint("arctic", ArcticSplinter, 2.6f) },
            new Skin { id = "urban", name = "Urban Digital", spray = new Color(0.5f, 0.52f, 0.55f), mat = () => Paint("urban", UrbanDigital, 3.5f, true) },
            new Skin { id = "tiger", name = "Tiger Stripe", spray = new Color(0.42f, 0.42f, 0.26f), mat = () => Paint("tiger", TigerStripe, 2.8f) },
            new Skin { id = "crimson", name = "Crimson Cerakote", spray = new Color(0.5f, 0.07f, 0.07f), mat = () => Coat("crimson", new Color(0.5f, 0.06f, 0.06f), 0.32f) },
            new Skin { id = "goldrush", name = "Gold Rush", spray = new Color(1f, 0.8f, 0.25f), mat = () => Obj(Mats.Lit("skin_gold", new Color(1f, 0.76f, 0.34f), 1f, 0.82f, Mats.Metal, null, 0.06f, 0.18f, true, 3f)) },
            new Skin { id = "carbon", name = "Carbon Fibre", spray = new Color(0.1f, 0.1f, 0.11f), mat = () => Paint("carbon", CarbonTwill, 14f, false, 0.25f, 0.9f, 0.08f, 0.05f) },
            // ---- DLC STORE packs
            new Skin { id = "candy", name = "Candy Cane", pack = "holiday", spray = new Color(0.9f, 0.15f, 0.15f), mat = () => Paint("candy", CandyCane, 3f, false, 0.05f, 0.6f, 0.08f, 0.2f) },
            new Skin { id = "festive", name = "Ugly Sweater", pack = "holiday", spray = new Color(0.15f, 0.45f, 0.2f), mat = () => Paint("festive", UglySweater, 3f, true, 0f, 0.2f, 0.15f, 0.2f) },
            new Skin { id = "frost", name = "Frostbite", pack = "holiday", spray = new Color(0.6f, 0.8f, 0.95f), mat = () => Paint("frost", Frostbite, 2.5f, false, 0.6f, 0.75f, 0.05f, 0.25f) },
            new Skin { id = "neon", name = "Neon Circuit", pack = "street", spray = new Color(0.2f, 1f, 0.5f), mat = () => Paint("neon", NeonCircuit, 3f, false, 0.2f, 0.7f, 0.05f, 0.1f) },
            new Skin { id = "toxic", name = "Hazard", pack = "street", spray = new Color(0.95f, 0.8f, 0.1f), mat = () => Paint("toxic", Hazard, 3f, false, 0.05f, 0.4f, 0.35f, 0.5f) },
            new Skin { id = "graffiti", name = "Graffiti", pack = "street", spray = new Color(0.9f, 0.3f, 0.7f), mat = () => Paint("graffiti", Graffiti, 2.5f, false, 0.05f, 0.45f, 0.2f, 0.3f) },
            new Skin { id = "rust", name = "Rust Bucket", pack = "survivor", spray = new Color(0.55f, 0.28f, 0.12f), mat = () => Paint("rust", Rust, 3f, false, 0.4f, 0.25f, 0.4f, 0.5f) },
            new Skin { id = "dragon", name = "Dragon Scale", pack = "collector", spray = new Color(0.6f, 0.08f, 0.05f), mat = () => Paint("dragon", DragonScale, 4f, false, 0.55f, 0.7f, 0.1f, 0.2f) },
            new Skin { id = "galaxy", name = "Galaxy", pack = "collector", spray = new Color(0.35f, 0.15f, 0.6f), mat = () => Paint("galaxy", Galaxy, 2f, false, 0.1f, 0.85f, 0.03f, 0.1f) },
            new Skin { id = "damascus", name = "Damascus Steel", pack = "collector", spray = new Color(0.6f, 0.6f, 0.62f), mat = () => Paint("damascus", Damascus, 3f, false, 0.95f, 0.8f, 0.05f, 0.15f) },
            new Skin { id = "bone", name = "Bone", pack = "collector", spray = new Color(0.88f, 0.85f, 0.76f), mat = () => Paint("bone", Bone, 3f, false, 0f, 0.35f, 0.25f, 0.2f) },
            new Skin { id = "ocean", name = "Deep Ocean", pack = "survivor", spray = new Color(0.1f, 0.3f, 0.55f), mat = () => Paint("ocean", Ocean, 3f, false, 0.1f, 0.5f, 0.15f, 0.3f) },
            new Skin { id = "cherry", name = "Cherry Blossom", pack = "survivor", spray = new Color(0.9f, 0.55f, 0.65f), mat = () => Paint("cherry", Cherry, 3f, false, 0f, 0.35f, 0.15f, 0.25f) },
            // ---- the South Pole present vault (not sold anywhere)
            // gold pack
            new Skin { id = "gold", name = "Solid Gold", pack = "gold", spray = new Color(1f, 0.78f, 0.3f), mat = () => Paint("gold", GoldLeaf, 3f, false, 1f, 0.85f, 0.03f, 0.08f) },
            new Skin { id = "blackgold", name = "Black Gold", pack = "gold", spray = new Color(0.12f, 0.1f, 0.06f), mat = () => Paint("blackgold", BlackGold, 2.5f, false, 0.7f, 0.75f, 0.05f, 0.15f) },
            new Skin { id = "chrome", name = "Mirror Chrome", pack = "gold", spray = new Color(0.9f, 0.9f, 0.92f), mat = () => Paint("chrome", Chrome, 3f, false, 1f, 0.95f, 0.02f, 0.05f) },
            // synthwave pack
            new Skin { id = "synth", name = "Synthwave", pack = "synthwave", spray = new Color(0.95f, 0.3f, 0.7f), mat = () => Paint("synth", Synthwave, 2f, false, 0.1f, 0.7f, 0.03f, 0.1f) },
            new Skin { id = "vapor", name = "Vaporwave", pack = "synthwave", spray = new Color(0.6f, 0.85f, 0.95f), mat = () => Paint("vapor", Vaporwave, 2.5f, false, 0.05f, 0.6f, 0.03f, 0.1f) },
            new Skin { id = "lasergrid", name = "Laser Grid", pack = "synthwave", spray = new Color(0.1f, 0.9f, 1f), mat = () => Paint("lasergrid", LaserGrid, 3f, false, 0.2f, 0.75f, 0.03f, 0.1f) },
            // predator pack
            new Skin { id = "zebra", name = "Zebra", pack = "predator", spray = new Color(0.9f, 0.9f, 0.88f), mat = () => Paint("zebra", Zebra, 2.5f, false, 0f, 0.35f, 0.15f, 0.25f) },
            new Skin { id = "leopard", name = "Leopard", pack = "predator", spray = new Color(0.8f, 0.6f, 0.3f), mat = () => Paint("leopard", Leopard, 3f, false, 0f, 0.3f, 0.15f, 0.25f) },
            new Skin { id = "python", name = "Python", pack = "predator", spray = new Color(0.45f, 0.4f, 0.2f), mat = () => Paint("python", Python, 3.5f, false, 0.1f, 0.5f, 0.15f, 0.2f) },
            // elements pack
            new Skin { id = "lava", name = "Lava", pack = "elements", spray = new Color(1f, 0.4f, 0.05f), mat = () => Paint("lava", Lava, 2.5f, false, 0.1f, 0.4f, 0.1f, 0.2f) },
            new Skin { id = "ice", name = "Ice Crystal", pack = "elements", spray = new Color(0.7f, 0.9f, 1f), mat = () => Paint("ice", IceCrystal, 2.5f, false, 0.3f, 0.9f, 0.02f, 0.1f) },
            new Skin { id = "lightning", name = "Lightning", pack = "elements", spray = new Color(0.6f, 0.4f, 1f), mat = () => Paint("lightning", Lightning, 2.5f, false, 0.2f, 0.7f, 0.03f, 0.1f) },
            new Skin { id = "slime", name = "Toxic Slime", pack = "elements", spray = new Color(0.4f, 1f, 0.2f), mat = () => Paint("slime", ToxicSlime, 2.5f, false, 0.05f, 0.8f, 0.05f, 0.1f) },
            // operator pack
            new Skin { id = "ranger", name = "Ranger Green", pack = "operator", spray = new Color(0.25f, 0.3f, 0.2f), mat = () => Coat("ranger", new Color(0.24f, 0.29f, 0.19f), 0.28f) },
            new Skin { id = "shards", name = "Mountain Shards", pack = "operator", spray = new Color(0.45f, 0.45f, 0.43f), mat = () => Paint("shards", MountainShards, 3f, false, 0.05f, 0.25f, 0.3f, 0.45f) },
            new Skin { id = "digidesert", name = "Desert Digital", pack = "operator", spray = new Color(0.7f, 0.6f, 0.42f), mat = () => Paint("digidesert", DesertDigital, 3.5f, true, 0f, 0.22f, 0.3f, 0.45f) },
            // ---- the three DLC packs on sale (3.37+): premium skins, the bright parts glow in the dark
            new Skin { id = "cybercircuit", name = "Cyber Circuit", pack = "neonops", spray = new Color(0.2f, 0.95f, 1f), mat = () => Paint("cybercircuit", CyberCircuit, 2.4f, false, 0.35f, 0.75f, 0.04f, 0.12f, 2.2f, 0.62f, 512) },
            new Skin { id = "plasmacore", name = "Plasma Core", pack = "neonops", spray = new Color(0.85f, 0.25f, 1f), mat = () => Paint("plasmacore", PlasmaCore, 2.2f, false, 0.2f, 0.8f, 0.03f, 0.1f, 2.6f, 0.6f, 512) },
            new Skin { id = "hologram", name = "Hologram", pack = "neonops", spray = new Color(0.6f, 0.9f, 1f), mat = () => Paint("hologram", Hologram, 2.6f, false, 0.6f, 0.92f, 0.02f, 0.06f, 0.9f, 0.82f, 512) },
            new Skin { id = "moltencore", name = "Molten Core", pack = "inferno", spray = new Color(1f, 0.45f, 0.05f), mat = () => Paint("moltencore", MoltenCore, 2.2f, false, 0.1f, 0.45f, 0.08f, 0.15f, 3f, 0.55f, 512) },
            new Skin { id = "hellfire", name = "Hellfire", pack = "inferno", spray = new Color(1f, 0.25f, 0.05f), mat = () => Paint("hellfire", Hellfire, 2f, false, 0.15f, 0.6f, 0.05f, 0.12f, 2.2f, 0.6f, 512) },
            new Skin { id = "obsidian", name = "Obsidian Ember", pack = "inferno", spray = new Color(0.25f, 0.1f, 0.3f), mat = () => Paint("obsidian", ObsidianEmber, 2.6f, false, 0.55f, 0.95f, 0.02f, 0.08f, 3f, 0.6f, 512) },
            new Skin { id = "filigree", name = "Royal Filigree", pack = "prestige", spray = new Color(0.95f, 0.75f, 0.3f), mat = () => Paint("filigree", RoyalFiligree, 3.2f, false, 0.85f, 0.88f, 0.03f, 0.08f, 0f, 0.9f, 512) },
            new Skin { id = "pearlgold", name = "Pearl & Gold", pack = "prestige", spray = new Color(0.95f, 0.93f, 0.9f), mat = () => Paint("pearlgold", PearlGold, 2.8f, false, 0.45f, 0.9f, 0.02f, 0.05f, 0f, 0.9f, 512) },
            new Skin { id = "diamondcut", name = "Diamond Cut", pack = "prestige", spray = new Color(0.75f, 0.9f, 1f), mat = () => Paint("diamondcut", DiamondCut, 3.4f, false, 0.7f, 0.95f, 0.02f, 0.05f, 0.6f, 0.86f, 512) },
            // a gift from the owner to beta testers (owner panel > Give Beta skin)
            new Skin { id = "beta", name = "Beta Tester", pack = "beta", spray = new Color(1f, 0.45f, 0.05f), mat = () => Paint("beta", BetaTester, 3f, false, 0.3f, 0.7f, 0.05f, 0.15f) },
            new Skin { id = "aurora", name = "Aurora", pack = "northpole", spray = new Color(0.3f, 0.95f, 0.7f), mat = () => Paint("aurora", Aurora, 2f, false, 0.3f, 0.85f, 0.03f, 0.08f) },
            new Skin { id = "giftwrap", name = "Gift Wrap", pack = "northpole", spray = new Color(0.8f, 0.1f, 0.1f), mat = () => Paint("giftwrap", GiftWrap, 3f, false, 0.2f, 0.6f, 0.05f, 0.1f) },
            new Skin { id = "gingerbread", name = "Gingerbread", pack = "northpole", spray = new Color(0.6f, 0.38f, 0.2f), mat = () => Paint("gingerbread", Gingerbread, 3f, false, 0f, 0.3f, 0.15f, 0.2f) },
            new Skin { id = "northstar", name = "North Star", pack = "northpole", spray = new Color(1f, 0.95f, 0.8f), mat = () => Paint("northstar", NorthStar, 2.5f, false, 0.9f, 0.85f, 0.03f, 0.1f) },
        };

        /// <summary>Yours to paint with: won on the Skin Wheel, or its DLC pack is owned. (No free skins: the factory
        /// finish - "" - is just stripping a gun back.)</summary>
        public static bool Owned(Skin s)
        {
            if (s == null) return false;
            if (s.mat == null) return true;
            var p = Profile.Current; if (p == null) return false;
            if (p.skins != null && p.skins.Contains(s.id)) return true;
            return s.pack != null && p.dlc != null && p.dlc.Contains(s.pack);
        }

        /// <summary>The skins on the Market's Skin Wheel: every skin that isn't in a DLC pack or the South Pole vault.</summary>
        public static List<Skin> WheelSkins() => All.FindAll(s => s.mat != null && s.pack == null);

        /// <summary>Where a skin comes from, for the paint booth and the wheel.</summary>
        public static string Source(Skin s) => s == null || s.mat == null ? "Strip the paint back to the factory finish" : s.pack == null ? "Won on the Skin Wheel" : "From " + PackName(s.pack);
        public static string PackName(string pack)
        {
            if (pack == "northpole") return "the South Pole vault";
            if (pack == "beta") return "a gift for beta testers";
            var p = Dlc.Get(pack);
            if (p == null) return pack;
            var n = p.name.ToLowerInvariant(); return char.ToUpper(n[0]) + n.Substring(1).Replace(" pack", " Pack").Replace("'s", "'s");
        }

        public static Skin Get(string id) { foreach (var s in All) if (s.id == (id ?? "")) return s; return All[0]; }

        class Original : MonoBehaviour { public readonly Dictionary<Renderer, Material[]> mats = new Dictionary<Renderer, Material[]>(); }

        /// <summary>Paint a gun (or put its factory finish back with "").</summary>
        public static void Apply(Gun g, string id)
        {
            if (g == null || g.rig == null || g.rig.root == null) return;
            var skin = Get(id);
            g.skin = skin.id;
            var orig = g.rig.root.GetComponent<Original>() ?? g.rig.root.AddComponent<Original>();
            foreach (var r in g.rig.root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponentInParent<Item>() != g) continue;        // the optic, attachments and mag keep their own look
                if (!orig.mats.ContainsKey(r)) orig.mats[r] = r.sharedMaterials;
                if (skin.mat == null) { r.sharedMaterials = orig.mats[r]; continue; }
                var m = skin.mat();
                var src = orig.mats[r]; var arr = new Material[src.Length];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = src[i] != null && (src[i].name.Contains("glass") || src[i].name.Contains("lens") || src[i].name.Contains("glow") || src[i].name.Contains("emit")) ? src[i] : m;   // sights / glowing bits stay
                r.sharedMaterials = arr;
            }
        }

        // ---------------------------------------------------------------- patterns
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static Texture2D Make(string key, int size, System.Func<int, int, Color> px)
        {
            if (_tex.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4, name = "skin_" + key };
            var c = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) c[y * size + x] = px(x, y);
            t.SetPixels(c); t.Apply(true, true);
            return _tex[key] = t;
        }

        static Material Obj(Material m) { m.SetFloat("_Triplanar", 2f); return m; }   // mapped in the gun's own space

        /// <summary>A solid factory coating (Cerakote): satin, a faint mottle, edges worn to bare metal.</summary>
        static Material Coat(string key, Color c, float smooth)
        {
            var tex = Make(key, 128, (x, y) => { float n = Fbm(x / 128f, y / 128f, 6f, 3.1f); return c * (0.94f + n * 0.12f); });
            return Obj(Mats.Lit("skin_" + key, Color.white, 0.05f, smooth, tex, null, 0.25f, 0.5f, true, 4f));
        }

        static Material Paint(string key, System.Func<float, float, Color> px, float scale, bool point = false, float metal = 0f, float smooth = 0.24f, float grime = 0.3f, float scratch = 0.45f, float glow = 0f, float glowAt = 0.75f, int size = 256)
        {
            int n = size;
            var tex = Make(key, n, (x, y) => px(x / (float)n, y / (float)n));
            if (point) tex.filterMode = FilterMode.Point;
            var m = Obj(Mats.Lit("skin_" + key, Color.white, metal, smooth, tex, null, grime, scratch, true, scale));
            if (glow > 0f) { m.SetFloat("_GlowGain", glow); m.SetFloat("_GlowThresh", glowAt); }   // its brightest lines glow
            return m;
        }

        /// <summary>M81 woodland: light green ground, field-drab and brown shapes, thin black branches.</summary>
        static Color Woodland(float u, float v)
        {
            Color green = new Color(0.4f, 0.42f, 0.3f), drab = new Color(0.56f, 0.49f, 0.35f), brown = new Color(0.34f, 0.25f, 0.16f), black = new Color(0.07f, 0.07f, 0.06f);
            float wu = u + (Fbm(u, v, 3f, 5.5f) - 0.5f) * 0.12f, wv = v + (Fbm(u, v, 3f, 8.2f) - 0.5f) * 0.12f;   // organic warp
            float a = Fbm(wu, wv, 3f, 1.3f), b = Fbm(wu, wv, 3f, 4.7f), k = Fbm(wu, wv, 4f, 2.9f);
            Color c = green;
            if (b > 0.55f) c = drab;
            if (a > 0.56f) c = brown;
            if (Mathf.Abs(k - 0.5f) < 0.035f && Fbm(u, v, 3f, 7.1f) > 0.5f) c = black;   // branch strokes
            return c * (0.97f + Fbm(u, v, 24f, 0.4f) * 0.06f);   // a little print texture
        }

        /// <summary>Multi-terrain: a soft tan-to-olive ground, blended brown and green patches, small dark and cream twigs.</summary>
        static Color MultiTerrain(float u, float v)
        {
            Color tan = new Color(0.66f, 0.59f, 0.45f), olive = new Color(0.47f, 0.47f, 0.33f), brown = new Color(0.45f, 0.35f, 0.24f),
                  dark = new Color(0.27f, 0.21f, 0.15f), cream = new Color(0.82f, 0.78f, 0.66f), green = new Color(0.36f, 0.4f, 0.27f);
            float g = Fbm(u, v, 2f, 3.3f);
            Color c = Color.Lerp(tan, olive, Mathf.SmoothStep(0.4f, 0.6f, g));
            float p = Fbm(u, v, 4f, 6.6f), q = Fbm(u, v, 4f, 9.1f);
            c = Color.Lerp(c, brown, Mathf.SmoothStep(0.55f, 0.6f, p));
            c = Color.Lerp(c, green, Mathf.SmoothStep(0.57f, 0.62f, q) * 0.9f);
            // small dark and cream spots (the "twigs")
            float t1 = Fbm(u, v, 16f, 1.8f), t2 = Fbm(u, v, 16f, 4.4f);
            if (t1 > 0.72f && p > 0.45f) c = dark;
            else if (t2 > 0.74f) c = cream;
            return c;
        }

        /// <summary>Arctic splinter: white ground broken by sharp grey shards in two directions.</summary>
        static Color ArcticSplinter(float u, float v)
        {
            Color white = new Color(0.9f, 0.92f, 0.93f), light = new Color(0.72f, 0.75f, 0.78f), mid = new Color(0.5f, 0.53f, 0.57f), dark = new Color(0.3f, 0.32f, 0.35f);
            // splinter: flat-shaded angular patches - noise sampled on a coarse rotated grid, then hard thresholds
            float s1 = Shard(u, v, 0.35f, 4f, 1.1f), s2 = Shard(u, v, -0.95f, 5f, 3.7f);
            Color c = white;
            if (s1 > 0.55f) c = light;
            if (s2 > 0.6f) c = mid;
            if (s1 > 0.68f && s2 > 0.55f) c = dark;
            return c;
        }
        static float Shard(float u, float v, float ang, float freq, float seed)
        {
            // value noise stretched hard along one direction: long thin angular slivers
            // quantise along one direction: the patches get straight edges like hand-cut splinter camo
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            float stripe = Mathf.Floor((u * ca + v * sa) * 9f) / 9f;
            return Fbm(u + stripe * 0.37f, v - stripe * 0.21f, freq, seed);
        }

        /// <summary>Urban digital: square pixels of four greys in clusters.</summary>
        static Color UrbanDigital(float u, float v)
        {
            Color l = new Color(0.66f, 0.68f, 0.7f), m = new Color(0.47f, 0.49f, 0.52f), d = new Color(0.3f, 0.31f, 0.34f), k = new Color(0.12f, 0.12f, 0.13f);
            float cu = Mathf.Floor(u * 64f) / 64f, cv = Mathf.Floor(v * 64f) / 64f;   // 4-texel pixels
            float a = Fbm(cu, cv, 4f, 2.2f), b = Fbm(cu, cv, 4f, 5.9f), c2 = Fbm(cu, cv, 8f, 8.8f);
            if (c2 > 0.66f) return k;
            if (a > 0.57f) return d;
            if (b > 0.52f) return m;
            return l;
        }

        /// <summary>Tiger stripe: olive ground with horizontal brush strokes of brown and black.</summary>
        static Color TigerStripe(float u, float v)
        {
            Color olive = new Color(0.44f, 0.44f, 0.28f), green = new Color(0.33f, 0.36f, 0.22f), brown = new Color(0.32f, 0.23f, 0.13f), black = new Color(0.08f, 0.08f, 0.07f);
            // horizontal brush strokes: noise squashed vertically (v scaled up), wrapped
            float sv = v * 7f - Mathf.Floor(v * 7f);
            float a = Fbm(u, sv, 3f, 1.4f) * 0.7f + Fbm(u, v, 6f, 9.4f) * 0.3f, b = Fbm(u, sv, 3f, 6.2f) * 0.7f + Fbm(u, v, 6f, 2.6f) * 0.3f, g = Fbm(u, v, 2f, 3.3f);
            Color c = Color.Lerp(olive, green, Mathf.SmoothStep(0.4f, 0.6f, g));
            if (b > 0.6f) c = brown;
            if (a > 0.64f) c = black;
            return c;
        }

        /// <summary>Carbon fibre: a 2x2 twill weave with each tow catching the light differently.</summary>
        static Color CarbonTwill(float u, float v)
        {
            int cells = 16;
            float x = u * cells, y = v * cells;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            bool warp = ((ix + iy / 2 * 2 + (iy % 2)) / 2 + iy) % 2 == 0;   // 2x2 twill: steps diagonally
            float fx = x - ix, fy = y - iy;
            float across = warp ? fx : fy;   // which way this tow runs
            float shade = 0.07f + 0.08f * Mathf.Sin(across * Mathf.PI) + (warp ? 0.03f : 0f);
            return new Color(shade, shade, shade * 1.1f);
        }

        /// <summary>BETA TESTER: dark carbon with bright orange chevrons running along the gun and a thin teal pinstripe.</summary>
        static Color BetaTester(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.06f, 0.06f, 0.07f), new Color(0.13f, 0.13f, 0.15f), ((int)(u * 64f) + (int)(v * 64f)) % 2 == 0 ? 0.3f : 0.7f);   // carbon weave
            float band = Mathf.Abs(v - 0.5f);
            float chev = Fr(u * 6f + Mathf.Abs(v - 0.5f) * 3f);
            if (band < 0.18f && chev < 0.45f) c = new Color(1f, 0.45f, 0.05f);   // >>> chevrons
            if (Mathf.Abs(band - 0.24f) < 0.012f) c = new Color(0.1f, 0.85f, 0.8f);   // pinstripes
            return c;
        }

        // ---- DLC patterns
        static Color CandyCane(float u, float v) { float t = (u + v) * 6f; return (t - Mathf.Floor(t)) < 0.5f ? new Color(0.88f, 0.1f, 0.1f) : new Color(0.95f, 0.95f, 0.93f); }
        static Color UglySweater(float u, float v)
        {
            // knitted rows: red / green bands with white zig-zags and little snowflake dots
            float cu = Mathf.Floor(u * 64f), cv = Mathf.Floor(v * 64f);
            int band = (int)(cv / 8f) % 4;
            Color c = band == 0 || band == 2 ? new Color(0.7f, 0.08f, 0.08f) : new Color(0.1f, 0.38f, 0.16f);
            if (band == 1 && Mathf.Abs((cu % 8) - 3.5f) + (cv % 8) < 4.5f && (cv % 8) > 2) c = new Color(0.95f, 0.95f, 0.92f);
            if ((band == 0 || band == 2) && (cu % 6 == 2) && (cv % 8 == 4)) c = new Color(0.95f, 0.95f, 0.92f);
            if (cv % 8 == 0) c = new Color(0.95f, 0.95f, 0.92f);
            return c;
        }
        static Color Frostbite(float u, float v)
        {
            float n = Fbm(u, v, 3f, 2.2f), cr = Mathf.Abs(Fbm(u, v, 6f, 5.1f) - 0.5f);
            Color c = Color.Lerp(new Color(0.35f, 0.55f, 0.75f), new Color(0.85f, 0.93f, 1f), n);
            if (cr < 0.025f) c = Color.white;
            return c;
        }
        static Color NeonCircuit(float u, float v)
        {
            Color c = new Color(0.04f, 0.04f, 0.05f);
            float gu = u * 12f, gv = v * 12f; float fu = gu - Mathf.Floor(gu), fv = gv - Mathf.Floor(gv);
            float pick = Fbm(Mathf.Floor(gu) / 12f, Mathf.Floor(gv) / 12f, 12f, 3.3f);
            if (pick > 0.5f && fv > 0.45f && fv < 0.55f) c = new Color(0.2f, 1f, 0.5f);
            if (pick < 0.45f && fu > 0.45f && fu < 0.55f) c = new Color(0.2f, 0.7f, 1f);
            if (fu > 0.4f && fu < 0.6f && fv > 0.4f && fv < 0.6f && pick > 0.7f) c = Color.white;
            return c;
        }
        static Color Hazard(float u, float v) { float t = (u - v) * 8f; return (t - Mathf.Floor(t)) < 0.5f ? new Color(0.9f, 0.75f, 0.08f) : new Color(0.06f, 0.06f, 0.06f); }
        static Color DragonScale(float u, float v)
        {
            float su = u * 16f, sv = v * 16f; float row = Mathf.Floor(sv); su += (row % 2) * 0.5f;
            float fu = su - Mathf.Floor(su) - 0.5f, fv = sv - row;
            float d = Mathf.Sqrt(fu * fu + fv * fv * 0.8f);
            Color a = new Color(0.55f, 0.06f, 0.04f), b = new Color(0.95f, 0.55f, 0.1f);
            return Color.Lerp(b, a, Mathf.SmoothStep(0.1f, 0.55f, d)) * (d > 0.5f ? 0.4f : 1f);
        }
        static Color Galaxy(float u, float v)
        {
            float n = Fbm(u, v, 2f, 1.7f), m = Fbm(u, v, 4f, 6.3f);
            Color c = Color.Lerp(new Color(0.03f, 0.02f, 0.08f), new Color(0.35f, 0.1f, 0.55f), Mathf.SmoothStep(0.45f, 0.75f, n));
            c = Color.Lerp(c, new Color(0.1f, 0.35f, 0.7f), Mathf.SmoothStep(0.55f, 0.8f, m) * 0.7f);
            if (Fbm(u, v, 64f, 9.9f) > 0.83f) c = Color.white;
            return c;
        }
        static Color Damascus(float u, float v)
        {
            float w = Mathf.Sin((v + Fbm(u, v, 3f, 4.4f) * 0.35f) * 60f);
            return Color.Lerp(new Color(0.3f, 0.3f, 0.32f), new Color(0.82f, 0.82f, 0.84f), w * 0.5f + 0.5f);
        }
        static Color Bone(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.78f, 0.74f, 0.64f), new Color(0.92f, 0.9f, 0.82f), Fbm(u, v, 4f, 3.6f));
            if (Mathf.Abs(Fbm(u, v, 5f, 8.1f) - 0.5f) < 0.012f) c *= 0.45f;
            return c;
        }
        static Color Ocean(float u, float v)
        {
            float cu = Mathf.Floor(u * 64f) / 64f, cv = Mathf.Floor(v * 64f) / 64f;
            float a = Fbm(cu, cv, 4f, 2.8f), b = Fbm(cu, cv, 4f, 7.4f);
            Color c = new Color(0.12f, 0.28f, 0.5f);
            if (a > 0.55f) c = new Color(0.05f, 0.14f, 0.3f);
            if (b > 0.6f) c = new Color(0.3f, 0.55f, 0.75f);
            return c;
        }
        static Color Cherry(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.95f, 0.85f, 0.88f), new Color(0.88f, 0.6f, 0.7f), Fbm(u, v, 3f, 1.9f));
            float p = Fbm(u, v, 12f, 5.5f);
            if (p > 0.7f) c = new Color(0.85f, 0.35f, 0.5f);
            if (Mathf.Abs(Fbm(u, v, 3f, 8.8f) - 0.5f) < 0.02f) c = new Color(0.25f, 0.15f, 0.12f);   // branches
            return c;
        }
        static Color Graffiti(float u, float v)
        {
            Color c = new Color(0.12f, 0.12f, 0.14f);
            Color[] cans = { new Color(0.95f, 0.25f, 0.65f), new Color(0.2f, 0.85f, 0.95f), new Color(0.98f, 0.85f, 0.15f), new Color(0.35f, 0.95f, 0.35f) };
            for (int k = 0; k < cans.Length; k++) { float n = Fbm(u, v, 3f, 1.7f + k * 2.3f); if (n > 0.62f) c = Color.Lerp(c, cans[k], Mathf.SmoothStep(0.62f, 0.66f, n)); }
            if (Mathf.Abs(Fbm(u, v, 4f, 9.3f) - 0.5f) < 0.018f) c = Color.white;   // tag outlines
            return c;
        }
        static Color Rust(float u, float v)
        {
            float n = Fbm(u, v, 5f, 2.4f), m = Fbm(u, v, 12f, 6.1f);
            Color paint = new Color(0.25f, 0.33f, 0.28f), rust = Color.Lerp(new Color(0.45f, 0.2f, 0.08f), new Color(0.7f, 0.38f, 0.15f), m);
            return Color.Lerp(paint, rust, Mathf.SmoothStep(0.45f, 0.6f, n)) * (0.9f + m * 0.2f);
        }
        static Color Aurora(float u, float v)
        {
            float wv = v + Fbm(u, v, 2f, 3.1f) * 0.3f;
            Color g = new Color(0.2f, 0.95f, 0.6f), p = new Color(0.55f, 0.25f, 0.9f), sky = new Color(0.02f, 0.04f, 0.1f);
            float band = Mathf.Sin(wv * Mathf.PI * 4f) * 0.5f + 0.5f;
            Color c = Color.Lerp(sky, Color.Lerp(g, p, Fbm(u, v, 3f, 6.6f)), band * band);
            if (Fbm(u, v, 64f, 2.2f) > 0.85f) c = Color.white;
            return c;
        }
        static Color GiftWrap(float u, float v)
        {
            float fu = u * 4f - Mathf.Floor(u * 4f), fv = v * 4f - Mathf.Floor(v * 4f);
            Color c = new Color(0.75f, 0.06f, 0.08f);
            if (Fbm(u, v, 16f, 4.4f) > 0.72f) c = new Color(0.95f, 0.85f, 0.4f);   // little gold dots
            if (Mathf.Abs(fu - 0.5f) < 0.06f || Mathf.Abs(fv - 0.5f) < 0.06f) c = new Color(0.95f, 0.78f, 0.25f);   // ribbon
            return c;
        }
        static Color Gingerbread(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.5f, 0.3f, 0.15f), new Color(0.65f, 0.42f, 0.22f), Fbm(u, v, 6f, 1.3f));
            float fv = v * 6f - Mathf.Floor(v * 6f);
            if (Mathf.Abs(fv - 0.5f - Mathf.Sin(u * Mathf.PI * 24f) * 0.12f) < 0.04f) c = new Color(0.97f, 0.96f, 0.94f);   // icing squiggle
            return c;
        }
        static Color NorthStar(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.85f, 0.85f, 0.88f), new Color(1f, 0.85f, 0.5f), Fbm(u, v, 3f, 2.9f));
            float fu = u * 6f - Mathf.Floor(u * 6f) - 0.5f, fv = v * 6f - Mathf.Floor(v * 6f) - 0.5f;
            if (Mathf.Abs(fu) * Mathf.Abs(fv) < 0.002f && Mathf.Abs(fu) + Mathf.Abs(fv) < 0.4f) c = Color.white;   // four-point stars
            return c;
        }

        // ---- DLC patterns
        static float Fr(float x) => x - Mathf.Floor(x);
        static Color GoldLeaf(float u, float v)
        {
            Color dark = new Color(0.75f, 0.52f, 0.15f), light = new Color(1f, 0.86f, 0.45f);
            float brush = Fbm(u, v, 48f, 3.3f) * 0.35f + Fbm(u, v, 4f, 1.2f) * 0.65f;
            return Color.Lerp(dark, light, brush);
        }
        static Color BlackGold(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.04f, 0.04f, 0.05f), new Color(0.1f, 0.09f, 0.08f), Fbm(u, v, 6f, 2.2f));
            for (int k = 0; k < 3; k++) if (Mathf.Abs(Fbm(u, v, 3f, 4.1f + k * 3.7f) - 0.5f) < 0.007f) c = new Color(1f, 0.78f, 0.3f);   // gold filigree
            return c;
        }
        static Color Chrome(float u, float v)
        {
            float n = Fbm(u, v, 2f, 5.5f);
            return Color.Lerp(new Color(0.7f, 0.72f, 0.75f), new Color(0.98f, 0.98f, 1f), Mathf.SmoothStep(0.3f, 0.7f, n));
        }
        static Color Synthwave(float u, float v)
        {
            // a sunset sky (orange to pink to deep purple) over a neon grid
            Color c = v < 0.5f ? Color.Lerp(new Color(1f, 0.55f, 0.1f), new Color(0.95f, 0.2f, 0.6f), v * 2f) : Color.Lerp(new Color(0.95f, 0.2f, 0.6f), new Color(0.2f, 0.05f, 0.35f), (v - 0.5f) * 2f);
            if (Fr(v * 10f) < 0.06f && Fr(v * 10f + 0.5f) > 0.12f) c = Color.Lerp(c, new Color(0.15f, 0.05f, 0.25f), 0.7f);   // sun stripes
            if (Fr(u * 8f) < 0.04f || Fr(v * 8f) < 0.04f) c = new Color(0.2f, 1f, 1f);                                      // the grid
            return c;
        }
        static Color Vaporwave(float u, float v)
        {
            Color c = Color.Lerp(new Color(1f, 0.7f, 0.85f), new Color(0.55f, 0.9f, 1f), Mathf.Sin((u + v) * Mathf.PI * 2f) * 0.5f + 0.5f);
            bool chk = ((int)(u * 8f) + (int)(v * 8f)) % 2 == 0;
            if (chk && Fbm(u, v, 2f, 8.2f) > 0.6f) c = Color.Lerp(c, Color.white, 0.5f);
            return c;
        }
        static Color LaserGrid(float u, float v)
        {
            Color c = new Color(0.02f, 0.02f, 0.05f);
            float gu = Fr(u * 16f), gv = Fr(v * 16f);
            if (gu < 0.05f || gv < 0.05f) c = new Color(0.1f, 0.95f, 1f);
            if ((gu < 0.05f || gv < 0.05f) && Fbm(u, v, 4f, 3.9f) > 0.6f) c = new Color(1f, 0.2f, 0.8f);
            return c;
        }
        static Color Zebra(float u, float v)
        {
            float w = Mathf.Sin((u * 14f + Fbm(u, v, 3f, 2.6f) * 3f) * Mathf.PI);
            return w > 0.1f ? new Color(0.06f, 0.06f, 0.06f) : new Color(0.93f, 0.92f, 0.88f);
        }
        static Color Leopard(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.85f, 0.65f, 0.35f), new Color(0.75f, 0.5f, 0.22f), Fbm(u, v, 4f, 1.4f));
            float n = Fbm(u, v, 12f, 6.6f);
            if (n > 0.66f) c = n > 0.72f ? new Color(0.55f, 0.35f, 0.15f) : new Color(0.1f, 0.07f, 0.05f);   // rosettes: dark ring, brown inside
            return c;
        }
        static Color Python(float u, float v)
        {
            float su = u * 24f, sv = v * 24f; float row = Mathf.Floor(sv); su += (row % 2) * 0.5f;
            float fu = Fr(su) - 0.5f, fv = sv - row - 0.5f;
            float d = Mathf.Abs(fu) + Mathf.Abs(fv);
            Color b = Fbm(u, v, 4f, 7.7f) > 0.55f ? new Color(0.2f, 0.15f, 0.08f) : new Color(0.55f, 0.5f, 0.25f);   // big blotches
            return b * (d > 0.42f ? 0.55f : 1f - d * 0.4f);
        }
        static Color Lava(float u, float v)
        {
            Color rock = Color.Lerp(new Color(0.06f, 0.05f, 0.05f), new Color(0.18f, 0.12f, 0.1f), Fbm(u, v, 8f, 2.1f));
            float crack = Mathf.Abs(Fbm(u, v, 4f, 5.2f) - 0.5f);
            if (crack < 0.06f) rock = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.3f, 0.02f), crack / 0.06f);   // glowing cracks
            return rock;
        }
        static Color IceCrystal(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.55f, 0.78f, 0.95f), new Color(0.85f, 0.95f, 1f), Fbm(u, v, 3f, 3.4f));
            for (int k = 0; k < 2; k++) if (Mathf.Abs(Fbm(u, v, 5f, 8.8f + k * 2.1f) - 0.5f) < 0.01f) c = Color.white;   // frost lines
            return c;
        }
        static Color Lightning(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.03f, 0.03f, 0.1f), new Color(0.12f, 0.06f, 0.25f), Fbm(u, v, 3f, 6.1f));
            float bolt = Mathf.Abs(Fbm(u, v, 6f, 1.9f) - 0.5f);
            if (bolt < 0.025f) c = Color.Lerp(Color.white, new Color(0.65f, 0.45f, 1f), bolt / 0.025f);
            return c;
        }
        static Color ToxicSlime(float u, float v)
        {
            float drip = v + Mathf.Sin(u * Mathf.PI * 10f) * 0.05f + Fbm(u, v, 4f, 3.3f) * 0.2f;
            Color c = Fr(drip * 3f) < 0.55f ? new Color(0.35f, 0.95f, 0.15f) : new Color(0.08f, 0.12f, 0.06f);
            if (Fbm(u, v, 16f, 9.1f) > 0.75f) c = Color.Lerp(c, new Color(0.8f, 1f, 0.6f), 0.6f);   // bubbles
            return c;
        }
        static Color MountainShards(float u, float v)
        {
            Color[] tones = { new Color(0.55f, 0.55f, 0.52f), new Color(0.4f, 0.4f, 0.38f), new Color(0.25f, 0.25f, 0.24f), new Color(0.12f, 0.12f, 0.12f) };
            float a = Fbm(Mathf.Floor(u * 24f) / 24f, Mathf.Floor(v * 24f + Fr(u * 24f) * 0.6f) / 24f, 3f, 4.4f);   // angular scales
            int i = Mathf.Clamp((int)(a * 4.4f) - 1, 0, 3);
            return tones[i];
        }
        static Color DesertDigital(float u, float v)
        {
            Color l = new Color(0.82f, 0.72f, 0.52f), m = new Color(0.66f, 0.55f, 0.38f), d = new Color(0.5f, 0.4f, 0.28f), k = new Color(0.35f, 0.3f, 0.22f);
            float cu = Mathf.Floor(u * 64f) / 64f, cv = Mathf.Floor(v * 64f) / 64f;
            float a = Fbm(cu, cv, 4f, 3.2f), b = Fbm(cu, cv, 4f, 6.9f), c2 = Fbm(cu, cv, 8f, 1.8f);
            if (c2 > 0.68f) return k;
            if (a > 0.57f) return d;
            if (b > 0.52f) return m;
            return l;
        }

        // ---------------------------------------------------------------- premium packs (3.37)
        static float H2(int x, int y, int s) => MathX.Hash(x, y, s);
        /// <summary>Distance to the nearest and second-nearest point of a tiling Voronoi grid (n cells across), plus the nearest cell's id.</summary>
        static void Voronoi(float u, float v, int n, int seed, out float d1, out float d2, out float id)
        {
            d1 = d2 = 9f; id = 0f;
            float x = u * n, y = v * n; int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j, wx = ((gx % n) + n) % n, wy = ((gy % n) + n) % n;
                    float px = gx + H2(wx, wy, seed), py = gy + H2(wx, wy, seed + 7);
                    float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                    if (d < d1) { d2 = d1; d1 = d; id = H2(wx, wy, seed + 13); } else if (d < d2) d2 = d;
                }
        }
        static Color Hue(float h, float s, float v) => Color.HSVToRGB(MathX.Frac(h), s, v);

        /// <summary>Cyber Circuit: matte carbon-black with a printed circuit board of glowing cyan traces, pads and vias.</summary>
        static Color CyberCircuit(float u, float v)
        {
            const int N = 24;
            float x = u * N, y = v * N; int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y); float fx = x - cx, fy = y - cy;
            int wx = cx % N, wy = cy % N;
            float weave = (Mathf.Sin(u * 400f) * Mathf.Sin(v * 400f)) * 0.02f;
            Color c = new Color(0.05f + weave, 0.055f + weave, 0.065f + weave);
            Color trace = new Color(0.25f, 0.95f, 1f), dim = new Color(0.08f, 0.32f, 0.36f);
            float h = H2(wx, wy, 31), h2 = H2(wx, wy, 47);
            bool right = H2(wx, wy, 5) < 0.55f, up = H2(wx, wy, 9) < 0.45f;
            bool rightIn = H2((wx + N - 1) % N, wy, 5) < 0.55f, upIn = H2(wx, (wy + N - 1) % N, 9) < 0.45f;
            float w = 0.09f;
            bool line = (right && fy > 0.5f - w && fy < 0.5f + w && fx >= 0.5f) || (rightIn && fy > 0.5f - w && fy < 0.5f + w && fx <= 0.5f)
                     || (up && fx > 0.5f - w && fx < 0.5f + w && fy >= 0.5f) || (upIn && fx > 0.5f - w && fx < 0.5f + w && fy <= 0.5f);
            float pd = Mathf.Sqrt((fx - 0.5f) * (fx - 0.5f) + (fy - 0.5f) * (fy - 0.5f));
            int links = (right ? 1 : 0) + (rightIn ? 1 : 0) + (up ? 1 : 0) + (upIn ? 1 : 0);
            bool pad = links == 1 && pd < 0.2f, via = links >= 3 && pd < 0.16f && pd > 0.08f;
            bool bright = h < 0.6f;   // some traces are lit, the rest are dim copper
            if (line || via) c = bright ? trace : dim;
            if (pad) c = pd < 0.1f ? new Color(0.9f, 1f, 1f) : trace;
            if (!line && !pad && h2 > 0.93f && pd < 0.32f && pd > 0.24f) c = new Color(0.12f, 0.14f, 0.16f);   // chip outlines
            return c;
        }

        /// <summary>Plasma Core: deep space-black with branching violet-to-white energy veins.</summary>
        static Color PlasmaCore(float u, float v)
        {
            float wu = u + (Fbm(u, v, 3f, 2.2f) - 0.5f) * 0.25f, wv = v + (Fbm(u, v, 3f, 6.1f) - 0.5f) * 0.25f;
            float a = Mathf.Abs(Fbm(wu, wv, 4f, 1.7f) - 0.5f), b = Mathf.Abs(Fbm(wu, wv, 8f, 3.9f) - 0.5f);
            float vein = Mathf.Max(1f - MathX.Step(0f, 0.035f, a), (1f - MathX.Step(0f, 0.02f, b)) * 0.7f);
            Color bg = Color.Lerp(new Color(0.02f, 0.01f, 0.05f), new Color(0.12f, 0.03f, 0.2f), Fbm(u, v, 2f, 8.8f));
            Color hot = Color.Lerp(new Color(0.75f, 0.2f, 1f), new Color(1f, 0.92f, 1f), MathX.Step(0.6f, 1f, vein));
            float halo = (1f - MathX.Step(0f, 0.09f, a)) * 0.35f;
            return Color.Lerp(bg + new Color(0.35f, 0.08f, 0.5f) * halo, hot, vein);
        }

        /// <summary>Hologram: a shifting rainbow film over chrome with a faint glowing hex lattice.</summary>
        static Color Hologram(float u, float v)
        {
            float h = u * 1f + v * 2f + Fbm(u, v, 3f, 4.4f) * 0.8f;
            Color film = Hue(h, 0.45f, 0.95f);
            Voronoi(u, v, 14, 77, out float d1, out float d2, out float id);
            float edge = 1f - MathX.Step(0.02f, 0.06f, d2 - d1);
            return Color.Lerp(film * (0.75f + id * 0.2f), new Color(0.9f, 1f, 1f), edge * 0.8f);
        }

        /// <summary>Molten Core: cooled black basalt crust split by white-hot, orange and red lava cracks.</summary>
        static Color MoltenCore(float u, float v)
        {
            Voronoi(u, v, 10, 21, out float d1, out float d2, out float id);
            float crack = d2 - d1 + (Fbm(u, v, 8f, 1.1f) - 0.5f) * 0.08f;
            Color crust = Color.Lerp(new Color(0.04f, 0.035f, 0.035f), new Color(0.16f, 0.11f, 0.09f), Fbm(u, v, 12f, 3.3f) * 0.7f + id * 0.3f);
            float heat = 1f - MathX.Step(0.0f, 0.14f, crack);
            Color lava = heat > 0.75f ? Color.Lerp(new Color(1f, 0.6f, 0.1f), new Color(1f, 0.97f, 0.7f), (heat - 0.75f) * 4f) : Color.Lerp(new Color(0.55f, 0.06f, 0.02f), new Color(1f, 0.6f, 0.1f), heat / 0.75f);
            return Color.Lerp(crust, lava, MathX.Step(0.15f, 0.5f, heat));
        }

        /// <summary>Hellfire: licking flames - black at the base through blood red and orange to yellow tips.</summary>
        static Color Hellfire(float u, float v)
        {
            float n = Fbm(u * 1f, v - 0f, 4f, 9.4f), m = Fbm(u, v, 8f, 2.7f);
            float f = MathX.Frac(v * 2f) + (n - 0.5f) * 0.9f + (m - 0.5f) * 0.3f;   // two rows of flames per tile
            f = Mathf.Clamp01(f);
            Color c = f < 0.35f ? Color.Lerp(new Color(0.03f, 0.01f, 0.01f), new Color(0.45f, 0.02f, 0.02f), f / 0.35f)
                    : f < 0.65f ? Color.Lerp(new Color(0.45f, 0.02f, 0.02f), new Color(1f, 0.35f, 0.03f), (f - 0.35f) / 0.3f)
                    : Color.Lerp(new Color(1f, 0.35f, 0.03f), new Color(1f, 0.92f, 0.45f), (f - 0.65f) / 0.35f);
            return c;
        }

        /// <summary>Obsidian Ember: glossy volcanic glass with a violet sheen and a scatter of glowing embers.</summary>
        static Color ObsidianEmber(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.02f, 0.02f, 0.03f), new Color(0.16f, 0.05f, 0.2f), MathX.Step(0.45f, 0.8f, Fbm(u, v, 3f, 5.5f)));
            Voronoi(u, v, 6, 17, out float e1, out float e2, out float _);
            float vein = 1f - MathX.Step(0.0f, 0.035f, e2 - e1);
            c = Color.Lerp(c, new Color(0.55f, 0.18f, 0.75f), vein * 0.85f);   // glowing violet seams in the glass
            Voronoi(u, v, 22, 63, out float d1, out float d2, out float id);
            if (id > 0.72f && d1 < 0.13f) c = Color.Lerp(new Color(1f, 0.88f, 0.5f), new Color(1f, 0.38f, 0.05f), d1 / 0.13f);   // embers
            else if (id > 0.72f && d1 < 0.22f) c = Color.Lerp(c, new Color(0.55f, 0.12f, 0.02f), 1f - (d1 - 0.13f) / 0.09f);
            return c;
        }

        /// <summary>Royal Filigree: black chrome engraved with curling gold scrollwork and a fine border grid.</summary>
        static Color RoyalFiligree(float u, float v)
        {
            Color black = new Color(0.06f, 0.06f, 0.07f), gold = new Color(1f, 0.78f, 0.32f);
            const int N = 4;
            float x = u * N, y = v * N; int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y); float fx = x - cx - 0.5f, fy = y - cy - 0.5f;
            float r = Mathf.Sqrt(fx * fx + fy * fy), a = Mathf.Atan2(fy, fx);
            bool flip = ((cx + cy) & 1) == 1;
            float spiral = MathX.Frac((r * 9f + (flip ? -a : a) / (Mathf.PI * 2f) * 1f) );
            bool scroll = r < 0.44f && Mathf.Abs(spiral - 0.5f) < 0.09f;
            bool ring = Mathf.Abs(r - 0.47f) < 0.012f;
            bool leaf = Mathf.Abs(Mathf.Abs(fx) - Mathf.Abs(fy)) < 0.02f && r > 0.46f;   // the diagonal flourish between scrolls
            bool border = Mathf.Abs(fx) > 0.49f || Mathf.Abs(fy) > 0.49f;
            Color c = black * (0.9f + Fbm(u, v, 16f, 1.4f) * 0.2f);
            if (scroll || ring || leaf || border) c = gold * (0.85f + Fbm(u, v, 32f, 2.2f) * 0.3f);
            return c;
        }

        /// <summary>Pearl &amp; Gold: lustrous white pearl with a soft rainbow sheen, split by fine gold pinstripes.</summary>
        static Color PearlGold(float u, float v)
        {
            float n = Fbm(u, v, 3f, 7.7f), sheen = MathX.Step(0.3f, 0.7f, Fbm(u, v, 2f, 1.9f));
            Color pearl = Color.Lerp(new Color(0.86f, 0.84f, 0.82f), Hue(0.5f + n * 0.6f, 0.16f, 0.98f), 0.35f + sheen * 0.4f);
            float stripe = MathX.Frac((u + v) * 4f);
            Color gold = new Color(0.95f, 0.7f, 0.25f) * (0.85f + Fbm(u, v, 32f, 2.4f) * 0.3f);
            if (stripe < 0.035f) return gold;                                  // the main pinstripe
            if (Mathf.Abs(stripe - 0.5f) < 0.01f) return gold * 0.9f;          // and a fine one between
            return pearl * (0.95f + Fbm(u, v, 24f, 3.1f) * 0.08f);
        }

        /// <summary>Diamond Cut: faceted crystal - each facet a different ice-blue shade, bright sparkling edges.</summary>
        static Color DiamondCut(float u, float v)
        {
            Voronoi(u, v, 12, 91, out float d1, out float d2, out float id);
            Color facet = Color.Lerp(new Color(0.45f, 0.62f, 0.8f), new Color(0.92f, 0.97f, 1f), id);
            float edge = 1f - MathX.Step(0.01f, 0.05f, d2 - d1);
            if (H2(Mathf.FloorToInt(u * 64), Mathf.FloorToInt(v * 64), 3) > 0.985f) return Color.white;   // sparkles
            return Color.Lerp(facet, Color.white, edge);
        }

        /// <summary>Tileable fractal noise in 0..1: periodic gradient noise (wraps exactly at u/v = 0..1, no stretching).</summary>
        static float Fbm(float u, float v, float freq, float seed)
        {
            float s = 0, amp = 1, tot = 0;
            int f = Mathf.Max(1, Mathf.RoundToInt(freq));
            int sd = (int)(seed * 1000f);
            for (int o = 0; o < 4; o++)
            {
                s += Grad(u * f, v * f, f, sd + o * 101) * amp; tot += amp;
                amp *= 0.5f; f *= 2;
            }
            return Mathf.Clamp01(0.5f + 0.5f * (s / tot) * 1.6f);
        }
        static float Grad(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float Dot(int ix, int iy, float dx, float dy)
            {
                int hx = ((ix % period) + period) % period, hy = ((iy % period) + period) % period;
                uint h = (uint)(hx * 374761393 + hy * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                float a = (h & 0xffff) / 65535f * Mathf.PI * 2f;
                return Mathf.Cos(a) * dx + Mathf.Sin(a) * dy;
            }
            float sx = fx * fx * fx * (fx * (fx * 6 - 15) + 10), sy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
            float n00 = Dot(x0, y0, fx, fy), n10 = Dot(x0 + 1, y0, fx - 1, fy), n01 = Dot(x0, y0 + 1, fx, fy - 1), n11 = Dot(x0 + 1, y0 + 1, fx - 1, fy - 1);
            float a0 = n00 + (n10 - n00) * sx, a1 = n01 + (n11 - n01) * sx;
            return (a0 + (a1 - a0) * sy) * 1.4f;
        }
    }

    /// <summary>
    /// The paint booth on the Armory workbench: clamp a gun on the red mat, pick a skin on the board (&lt; &gt;) and
    /// press PAINT - a spray can shakes, then sweeps up and down the gun in a cloud of paint, and the gun comes out in
    /// its new skin. Customer stands at local -Z.
    /// </summary>
    public class PaintBooth : MonoBehaviour
    {
        ItemContainer _clamp; TextMesh _name, _hint, _src, _count; int _pick; Transform _preview, _swatch; string _shownKey;
        Transform _can; ParticleSystem _mist; AudioSource _hiss; float _sprayT = -1f; string _paintTo; WorldButton _paintBtn;
        const float SprayTime = 3.2f;

        public static PaintBooth Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("PaintBooth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var b = go.AddComponent<PaintBooth>();
            b.Build();
            return b;
        }

        void Build()
        {
            // a red rubber mat with two clamps, on the workbench top
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.915f, 0), new Vector3(1.3f, 0.01f, 0.45f), new Color(0.55f, 0.08f, 0.07f), 0.004f);
            foreach (var x in new[] { -0.35f, 0.35f })
            {
                mb.Sub(1).Box(new Vector3(x, 0.95f, 0), new Vector3(0.05f, 0.06f, 0.12f), new Color(0.2f, 0.2f, 0.22f), 0.01f);
                mb.Sub(1).Box(new Vector3(x, 0.99f, 0.05f), new Vector3(0.05f, 0.02f, 0.04f), new Color(0.75f, 0.6f, 0.1f), 0.006f);
            }
            mb.Build("Mat", transform, Mats.Lit("paint_mat", Color.white, 0f, 0.2f, Mats.Noise, null, 0.3f, 0.1f), Mats.Lit("paint_clamp", Color.white, 0.6f, 0.4f, Mats.Metal, null, 0.3f, 0.3f));
            // guns lie along the bench (barrel to your right)
            _clamp = ItemContainer.Grid(transform, new Vector3(0, 0.93f, 0), Quaternion.identity, 1, 1, new Vector2(1.2f, 0.4f), 1.15f, it => it is Gun, Quaternion.Euler(0, 90, 0));
            _clamp.onChanged = Refresh;
            // a few plain cans of primer on the bench
            var cans = new MB(2);
            var canCols = new[] { new Color(0.25f, 0.25f, 0.26f), new Color(0.6f, 0.6f, 0.58f), new Color(0.85f, 0.85f, 0.82f), new Color(0.45f, 0.4f, 0.3f) };
            for (int k = 0; k < 4; k++)
            {
                float x = -0.6f + k * 0.07f, z = 0.18f;
                cans.Sub(0).Cyl(new Vector3(x, 0.91f, z), new Vector3(x, 1.05f, z), 0.028f, 0.028f, 10, canCols[k]);
                cans.Sub(1).Cyl(new Vector3(x, 1.05f, z), new Vector3(x, 1.075f, z), 0.012f, 0.01f, 8, new Color(0.85f, 0.85f, 0.85f));
            }
            cans.Build("Cans", transform, Mats.Lit("paint_cans", Color.white, 0.5f, 0.6f, null, null, 0.1f, 0.1f), Mats.Lit("paint_nozzle", Color.white, 0.2f, 0.4f));
            // the board on the pegboard behind: a big preview of the gun in the chosen skin, its name and where it's
            // from, < > through the skins you own, and PAINT. Nothing goes on until you press PAINT.
            var board = UI.Panel(transform, "PaintBoard", new Vector3(0, 1.62f, 0.36f), Quaternion.identity, new Vector2(1.5f, 0.8f), null, true, Skin.Steel);
            UI.Text(board, "PAINT BOOTH", new Vector3(0, 0.34f, -0.003f), 0.05f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _preview = new GameObject("Preview").transform; _preview.SetParent(board, false); _preview.localPosition = new Vector3(-0.3f, 0.05f, -0.09f);
            _name = UI.Text(board, "", new Vector3(0.42f, 0.17f, -0.003f), 0.05f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _src = UI.Text(board, "", new Vector3(0.42f, 0.09f, -0.003f), 0.024f, new Color(0.75f, 0.8f, 0.85f), TextAnchor.MiddleCenter);
            _count = UI.Text(board, "", new Vector3(0.42f, 0.02f, -0.003f), 0.028f, UI.Dim, TextAnchor.MiddleCenter);
            _swatch = new MB().Box(Vector3.zero, new Vector3(0.36f, 0.025f, 0.01f), Color.white, 0.004f).Build("Swatch", board, Mats.Lit("paint_swatch", Color.white, 0.3f, 0.5f)).transform;
            _swatch.localPosition = new Vector3(0.42f, 0.235f, -0.004f);
            _hint = UI.Text(board, "", new Vector3(0, -0.3f, -0.003f), 0.024f, UI.Dim, TextAnchor.MiddleCenter);
            UI.Button(board, "<", new Vector3(0.17f, -0.07f, 0), new Vector2(0.1f, 0.09f), () => { Step(-1); Refresh(); }, new Color(0.3f, 0.3f, 0.32f), BtnStyle.Key);
            UI.Button(board, ">", new Vector3(0.67f, -0.07f, 0), new Vector2(0.1f, 0.09f), () => { Step(1); Refresh(); }, new Color(0.3f, 0.3f, 0.32f), BtnStyle.Key);
            _paintBtn = UI.Button(board, "PAINT", new Vector3(0.42f, -0.18f, 0), new Vector2(0.36f, 0.1f), Paint, new Color(0.55f, 0.15f, 0.12f), BtnStyle.Key);
            // the spray can that does the work (parked at the side)
            var can = new MB(2);
            can.Sub(0).Cyl(new Vector3(0, -0.07f, 0), new Vector3(0, 0.07f, 0), 0.03f, 0.03f, 12, Color.white);
            can.Sub(1).Cyl(new Vector3(0, 0.07f, 0), new Vector3(0, 0.1f, 0), 0.013f, 0.01f, 8, new Color(0.8f, 0.8f, 0.8f));
            _can = can.Build("SprayCan", transform, Mats.Lit("spray_can", Color.white, 0.5f, 0.6f, null, null, 0.1f, 0.1f), Mats.Lit("paint_nozzle", Color.white, 0.2f, 0.4f)).transform;
            Park();
            // paint mist
            var mgo = new GameObject("Mist"); mgo.transform.SetParent(_can, false); mgo.transform.localPosition = new Vector3(0, 0.1f, 0);
            mgo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            _mist = mgo.AddComponent<ParticleSystem>();
            _mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _mist.main; main.startLifetime = 0.7f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.maxParticles = 400; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.05f; main.playOnAwake = false;
            var em = _mist.emission; em.rateOverTime = 150f;
            var sh = _mist.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.005f;
            var col = _mist.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0, 1) }); col.color = gr;
            var sol = _mist.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 2.5f)));
            mgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Unlit("fx_paint_mist", Color.white, Mats.Smoke);
            _hiss = gameObject.AddComponent<AudioSource>(); _hiss.clip = Synth.SprayHiss(); _hiss.loop = true; _hiss.spatialBlend = 1f; _hiss.minDistance = 0.5f; _hiss.maxDistance = 8f; _hiss.volume = 0f;
            Refresh();
        }

        Gun Clamped => _clamp.slots[0].item as Gun;
        void Park() { _can.localPosition = new Vector3(0.55f, 0.99f, 0.15f); _can.localRotation = Quaternion.identity; }

        /// <summary>What you can pick: the skins you own, plus the factory finish if the gun on the mat is painted.</summary>
        List<GunSkins.Skin> Options()
        {
            var l = new List<GunSkins.Skin>();
            var g = Clamped;
            if (g != null && !string.IsNullOrEmpty(g.skin)) l.Add(GunSkins.All[0]);
            foreach (var s in GunSkins.All) if (s.mat != null && GunSkins.Owned(s) && !l.Contains(s)) l.Add(s);
            return l;
        }

        GunSkins.Skin Current { get { var o = Options(); if (o.Count == 0) return null; _pick = Mathf.Clamp(_pick, 0, o.Count - 1); return o[_pick]; } }

        void Refresh()
        {
            var o = Options();
            var s = Current;
            var g = Clamped;
            if (s == null)
            {
                _name.text = "NO SKINS YET"; _src.text = "Win skins at the SKIN WHEEL in the Market\nor get them in DLC packs"; _count.text = "";
                _swatch.gameObject.SetActive(false);
            }
            else
            {
                _name.text = s.name; _src.text = GunSkins.Source(s); _count.text = (_pick + 1) + " / " + o.Count;
                _swatch.gameObject.SetActive(s.mat != null);
                if (s.mat != null) { var r = _swatch.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = s.mat(); }
            }
            _hint.text = _sprayT >= 0f ? "painting..." : g == null ? "Put a gun on the red mat to see it in each skin" : s == null ? "" : g.skin == s.id ? "The " + g.g.name + " already has this" : "Press PAINT to put it on the " + g.g.name;
            _paintBtn.Interactable = g != null && s != null && _sprayT < 0f && g.skin != s.id;
            if (_paintBtn.Label != null) _paintBtn.Label.text = s != null && s.mat == null ? "STRIP" : "PAINT";
            ShowPreview(g, s);
        }

        /// <summary>The gun on the mat (or a sample rifle) shown big on the board in the chosen skin, turning slowly.</summary>
        void ShowPreview(Gun g, GunSkins.Skin s)
        {
            ItemDef def = g != null ? (ItemDef)g.g : Db.Guns.Find(x => x.TwoHanded);
            string key = (def != null ? def.id : "") + "|" + (s != null ? s.id : "-");
            if (key == _shownKey) return;
            _shownKey = key;
            SpawnBrowser.ReturnPreviews(_preview);
            if (def == null) return;
            SpawnBrowser.Preview(def, _preview, 0.62f, 0.4f, s != null ? s.id : "", 0f);
        }

        /// <summary>Next / previous of the skins you can pick.</summary>
        void Step(int dir)
        {
            int n = Options().Count;
            _pick = n == 0 ? 0 : ((_pick + dir) % n + n) % n;
            Sfx.UI(Synth.UiClick(), 0.3f);
        }

        void Paint()
        {
            var g = Clamped;
            if (g == null || _sprayT >= 0f) return;
            var s = Current;
            if (s == null || !GunSkins.Owned(s)) return;
            _paintTo = s.id;
            _sprayT = 0f;
            var main = _mist.main; main.startColor = new ParticleSystem.MinMaxGradient(s.spray, Color.Lerp(s.spray, Color.white, 0.25f));
            Sfx.Play(Synth.CanRattle(), _can.position, 0.7f, 1f, 0.4f, 6f);
            Refresh();
        }

        void LateUpdate()
        {
            if (_preview != null) _preview.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 0.6f) * 35f, 0);
            if (Time.frameCount % 20 == 0 && _sprayT < 0f) Refresh();   // a gun put on / taken off the mat, a skin just won
        }

        void Update()
        {
            if (_sprayT < 0f) return;
            var g = Clamped;
            if (g == null) { StopSpray(); return; }   // they took it off mid-spray
            _sprayT += Time.deltaTime;
            float shake = 0.6f;   // shake the can first
            if (_sprayT < shake)
            {
                _can.localPosition = new Vector3(0.45f, 1.15f + Mathf.Sin(_sprayT * 60f) * 0.03f, -0.05f);
                _can.localRotation = Quaternion.Euler(0, 0, 180f + Mathf.Sin(_sprayT * 60f) * 15f);
                return;
            }
            float t = (_sprayT - shake) / SprayTime;
            if (!_mist.isPlaying) { _mist.Play(); _hiss.Play(); }
            _hiss.volume = 0.55f;
            // sweep along the gun and back, top then side, aiming down at it
            float along = Mathf.Sin(t * Mathf.PI * 3f) * 0.5f;
            float height = 1.2f + Mathf.Sin(t * Mathf.PI * 7f) * 0.03f;
            _can.localPosition = new Vector3(along, height, -0.12f + Mathf.Sin(t * Mathf.PI) * 0.06f);
            _can.localRotation = Quaternion.LookRotation(Vector3.down + Vector3.forward * 0.5f) * Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 180);
            var mt = _mist.transform; mt.rotation = Quaternion.LookRotation((g.transform.position + Vector3.up * 0.02f - mt.position).normalized);
            // the new paint goes on half way through, under the cloud
            if (t >= 0.5f && _paintTo != null) { GunSkins.Apply(g, _paintTo); _paintTo = null; }
            if (t >= 1f)
            {
                StopSpray();
                Sfx.UI(Synth.Cash(), 0.4f);
                RagoGame.I?.Player?.Toast(g.g.name + " painted: " + GunSkins.Get(g.skin).name);
            }
        }

        void StopSpray()
        {
            if (_paintTo != null && Clamped != null) GunSkins.Apply(Clamped, _paintTo);
            _paintTo = null; _sprayT = -1f;
            _mist.Stop(true, ParticleSystemStopBehavior.StopEmitting); _hiss.Stop(); _hiss.volume = 0f;
            Park(); Refresh();
        }

        /// <summary>Leaving the bunker: a gun left in the clamp goes back to the stash.</summary>
        /// <summary>The gun in the clamp, saved without taking it out (the bunker saves it with the stash).</summary>
        public void SaveTo(List<ItemData> into) { var sl = _clamp.slots[0]; if (sl.item != null) into.Add(sl.item.Save()); }

        public void SweepToStash(List<ItemData> into)
        {
            var sl = _clamp.slots[0];
            if (sl.item != null) { var it = sl.item; into.Add(it.Save()); sl.Release(it); Item.Discard(it); }
        }
    }
}
