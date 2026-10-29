using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>One player's outfit. Packs into a single number so it rides along with their pose.</summary>
    public struct Look
    {
        public int hat, hatColor, top, topColor, pants, shoes, gloves;

        // bits: hat 5 · hat colour 4 · top 4 · top colour 4 · pants 3 · shoes 3 · gloves 3  (26 in all)
        public int Pack() =>
            Mathf.Clamp(hat, 0, 31) | Mathf.Clamp(hatColor, 0, 15) << 5 | Mathf.Clamp(top, 0, 15) << 9 | Mathf.Clamp(topColor, 0, 15) << 13 |
            Mathf.Clamp(pants, 0, 7) << 17 | Mathf.Clamp(shoes, 0, 7) << 20 | Mathf.Clamp(gloves, 0, 7) << 23;

        public static Look Unpack(int v) => new Look
        {
            hat = Mathf.Min(v & 31, Cosmetics.Hats.Length - 1),
            hatColor = Mathf.Min(v >> 5 & 15, Cosmetics.Colors.Length - 1),
            top = Mathf.Min(v >> 9 & 15, Cosmetics.Tops.Length - 1),
            topColor = Mathf.Min(v >> 13 & 15, Cosmetics.Colors.Length - 1),
            pants = Mathf.Min(v >> 17 & 7, Cosmetics.Pants.Length - 1),
            shoes = Mathf.Min(v >> 20 & 7, Cosmetics.Shoes.Length - 1),
            gloves = Mathf.Min(v >> 23 & 7, Cosmetics.Gloves.Length - 1),
        };

        /// <summary>A random outfit (bots get one from their ID, so everyone sees the same thing).</summary>
        public static Look Random(System.Random r) => new Look
        {
            hat = r.NextDouble() < 0.75 ? r.Next(1, Cosmetics.Hats.Length) : 0,
            hatColor = r.Next(Cosmetics.Colors.Length),
            top = r.NextDouble() < 0.85 ? r.Next(1, Cosmetics.Tops.Length) : 0,
            topColor = r.Next(Cosmetics.Colors.Length),
            pants = r.Next(Cosmetics.Pants.Length),
            shoes = r.Next(Cosmetics.Shoes.Length),
            gloves = r.NextDouble() < 0.6 ? r.Next(1, Cosmetics.Gloves.Length) : 0,
        };

        public static int FromSeed(string seed) => Random(new System.Random(string.IsNullOrEmpty(seed) ? 7 : StableHash(seed))).Pack();

        static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }
    }

    /// <summary>
    /// Hats, tops, pants, shoes and gloves for the white mannequin players,
    /// all built from shapes. Customise them at the Outfitter in the hub.
    /// </summary>
    public static class Cosmetics
    {
        public static readonly string[] Hats =
        {
            "None", "Baseball Cap", "Backwards Cap", "Beanie", "Cowboy Hat", "Top Hat", "Party Hat", "Viking Helmet",
            "Crown", "Hard Hat", "Chef Hat", "Halo", "Propeller Cap", "Headphones", "Army Helmet", "Pirate Hat",
            "Wizard Hat", "Bucket Hat", "Traffic Cone", "Santa Hat"
        };
        /// <summary>Hats that take the hat colour.</summary>
        static readonly bool[] HatColored = { false, true, true, true, false, false, true, false, false, false, false, false, true, true, false, false, true, true, false, false };

        public static readonly string[] Tops =
        {
            "None", "T-Shirt", "Hoodie", "Tank Top", "Suit & Tie", "Camo Jacket", "Hawaiian Shirt", "Hi-Vis Vest",
            "Sports Jersey", "Puffer Jacket", "Lab Coat"
        };
        static readonly bool[] TopColored = { false, true, true, true, false, false, true, false, true, true, false };

        public static readonly string[] Pants = { "None", "Jeans", "Shorts", "Camo Pants", "Track Pants", "Cargo Pants" };
        public static readonly string[] Shoes = { "None", "Sneakers", "Boots", "Combat Boots", "High-Tops" };
        public static readonly string[] Gloves = { "None", "Fingerless", "Boxing Gloves", "Work Gloves", "Rubber Gloves", "Cartoon Gloves", "Tactical", "Mittens" };

        public static readonly (string name, Color color)[] Colors =
        {
            ("White", new Color(0.92f, 0.92f, 0.9f)), ("Black", new Color(0.1f, 0.1f, 0.11f)), ("Red", new Color(0.82f, 0.16f, 0.14f)),
            ("Orange", new Color(0.95f, 0.5f, 0.1f)), ("Yellow", new Color(0.97f, 0.82f, 0.18f)), ("Green", new Color(0.2f, 0.66f, 0.3f)),
            ("Teal", new Color(0.1f, 0.65f, 0.65f)), ("Blue", new Color(0.18f, 0.38f, 0.85f)), ("Purple", new Color(0.52f, 0.27f, 0.78f)),
            ("Pink", new Color(0.95f, 0.45f, 0.7f)), ("Brown", new Color(0.45f, 0.29f, 0.16f)), ("Grey", new Color(0.5f, 0.52f, 0.55f)),
        };

        public static bool HatTakesColor(int hat) => hat >= 0 && hat < HatColored.Length && HatColored[hat];
        public static bool TopTakesColor(int top) => top >= 0 && top < TopColored.Length && TopColored[top];

        static Material L(Color c, float smooth = 0.35f, float metal = 0f, Surface s = Surface.Fabric) => Prims.Lit(c, smooth, metal, s);
        static Material L(string hex, float smooth = 0.35f, float metal = 0f, Surface s = Surface.Fabric) => Prims.Lit(Prims.Hex(hex), smooth, metal, s);

        /// <summary>What a body part looks like without clothes.</summary>
        public struct Body
        {
            public Transform head, torso, pelvis;
            public Transform[] upperArm, lowerArm, hand, thigh, shin, foot;
            public Renderer torsoBase, pelvisBase, neck;
            public Renderer[] upperArmVis, lowerArmVis, thighVis, shinVis, footBase;
            public List<Renderer>[] handParts;
        }

        /// <summary>Dress a body. Everything added goes in <paramref name="made"/>; base parts only get new materials.</summary>
        public static void Apply(Body b, Look look, List<GameObject> made)
        {
            var hatC = Colors[look.hatColor].color;
            var topC = Colors[look.topColor].color;
            if (look.hat > 0) Hat(b.head, look.hat, hatC, made);
            if (look.top > 0) Top(b, look.top, topC, made);
            if (look.pants > 0) PantsOn(b, look.pants, made);
            if (look.shoes > 0) ShoesOn(b, look.shoes, topC, made);
            if (look.gloves > 0) GlovesOn(b, look.gloves, made);
        }

        static GameObject Add(List<GameObject> made, GameObject go) { made?.Add(go); return go; }

        /* ======================= hats ======================= */

        /// <summary>A stack of cylinders narrowing from d0 to d1: cones, wizard hats, party hats.</summary>
        static void Taper(Transform h, List<GameObject> made, float y0, float y1, float d0, float d1, int n, System.Func<int, Material> mat, float lean = 0f)
        {
            float step = (y1 - y0) / n;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float x = lean * t * t;
                Add(made, Prims.Cyl(h, new Vector3(x, y0 + step * (i + 0.5f), 0), Mathf.Lerp(d0, d1, t), step * 1.05f, mat(i), Vector3.up));
            }
        }

        /// <summary>Build a hat on a head (head space: top of the head is about y = 0.145, the face looks along +z).</summary>
        public static void Hat(Transform h, int hat, Color c, List<GameObject> made)
        {
            var col = L(c, 0.3f);
            var dark = L(Color.Lerp(c, Color.black, 0.45f), 0.3f);
            var black = L("161618", 0.35f);
            var white = L("f2f2f2", 0.3f);
            var gold = Prims.Lit(Prims.Hex("f2c230"), 0.85f, 0.9f, Surface.Metal);
            switch (hat)
            {
                case 1: // baseball cap
                case 2: // backwards
                {
                    float dir = hat == 1 ? 1 : -1;
                    Add(made, Prims.Ball(h, new Vector3(0, 0.1f, 0), new Vector3(0.226f, 0.13f, 0.206f), col));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.085f, dir * 0.115f), new Vector3(0.17f, 0.014f, 0.14f), dark));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.163f, 0), new Vector3(0.025f, 0.015f, 0.025f), dark));
                    if (hat == 2) Add(made, Prims.Box(h, new Vector3(0, 0.08f, 0.1f), new Vector3(0.07f, 0.035f, 0.01f), dark, false));
                    break;
                }
                case 3: // beanie with a pom-pom
                    Add(made, Prims.Ball(h, new Vector3(0, 0.1f, 0), new Vector3(0.23f, 0.16f, 0.21f), col));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.07f, 0), new Vector3(0.232f, 0.025f, 0.212f), dark, false));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.2f, 0), new Vector3(0.065f, 0.06f, 0.065f), white));
                    break;
                case 4: // cowboy hat
                {
                    var leather = L("8a5a2b", 0.25f, 0f, Surface.Wood);
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.008f, 0.36f), leather, false));
                    foreach (float s in new[] { -1f, 1f })
                        Add(made, Prims.Box(h, new Vector3(s * 0.17f, 0.12f, 0), new Vector3(0.08f, 0.012f, 0.26f), leather, false, Quaternion.Euler(0, 0, s * 30f)));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.17f, 0), new Vector3(0.21f, 0.065f, 0.19f), leather, false));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.235f, 0), new Vector3(0.21f, 0.04f, 0.19f), leather));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.122f, 0), new Vector3(0.214f, 0.012f, 0.194f), L("3a2414", 0.3f), false));
                    break;
                }
                case 5: // top hat
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.118f, 0), new Vector3(0.3f, 0.006f, 0.28f), black, false));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.235f, 0), new Vector3(0.19f, 0.115f, 0.18f), black, false));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.145f, 0), new Vector3(0.194f, 0.018f, 0.184f), L("9c1c1c", 0.4f), false));
                    break;
                case 6: // party hat: a striped cone with a puff on top
                    Taper(h, made, 0.11f, 0.38f, 0.17f, 0.02f, 9, i => i % 2 == 0 ? col : L("f5e663", 0.4f));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.39f, 0), Vector3.one * 0.05f, white));
                    break;
                case 7: // viking helmet
                {
                    var steel = Prims.Lit(Prims.Hex("8f959c"), 0.7f, 0.8f, Surface.Metal);
                    var bronze = Prims.Lit(Prims.Hex("b07a35"), 0.6f, 0.8f, Surface.Metal);
                    var horn = L("efe6cf", 0.4f, 0f, Surface.Plastic);
                    Add(made, Prims.Ball(h, new Vector3(0, 0.1f, 0), new Vector3(0.236f, 0.17f, 0.216f), steel));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.07f, 0), new Vector3(0.24f, 0.016f, 0.22f), bronze, false));
                    Add(made, Prims.Box(h, new Vector3(0, 0.13f, 0), new Vector3(0.02f, 0.1f, 0.222f), bronze, false));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Add(made, Prims.Capsule(h, new Vector3(s * 0.135f, 0.12f, 0), 0.05f, 0.1f, horn, new Vector3(s, 0.5f, 0)));
                        Add(made, Prims.Capsule(h, new Vector3(s * 0.185f, 0.185f, 0), 0.038f, 0.09f, horn, new Vector3(s * 0.45f, 1, 0)));
                        Add(made, Prims.Capsule(h, new Vector3(s * 0.195f, 0.245f, 0), 0.024f, 0.05f, horn, new Vector3(-s * 0.2f, 1, 0)));
                    }
                    break;
                }
                case 8: // crown
                {
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.03f, 0.19f), gold, false));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        var p = new Vector3(Mathf.Sin(a) * 0.098f, 0.2f, Mathf.Cos(a) * 0.092f);
                        Add(made, Prims.Box(h, p, new Vector3(0.035f, 0.035f, 0.01f), gold, false, Quaternion.Euler(0, a * Mathf.Rad2Deg, 45)));
                        Add(made, Prims.Ball(h, p + Vector3.up * 0.03f, Vector3.one * 0.016f, gold));
                    }
                    Add(made, Prims.Ball(h, new Vector3(0, 0.152f, 0.098f), Vector3.one * 0.028f, Prims.Lit(Prims.Hex("d0102a"), 0.95f, 0.2f, Surface.Plastic)));
                    foreach (float s in new[] { -1f, 1f })
                        Add(made, Prims.Ball(h, new Vector3(s * 0.07f, 0.152f, 0.072f), Vector3.one * 0.02f, Prims.Lit(Prims.Hex("1a5ad0"), 0.95f, 0.2f, Surface.Plastic)));
                    break;
                }
                case 9: // hard hat
                {
                    var y = L("f5c400", 0.55f, 0f, Surface.Plastic);
                    Add(made, Prims.Ball(h, new Vector3(0, 0.1f, 0), new Vector3(0.24f, 0.18f, 0.23f), y));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.085f, 0.02f), new Vector3(0.29f, 0.016f, 0.3f), y));
                    Add(made, Prims.Box(h, new Vector3(0, 0.185f, 0), new Vector3(0.03f, 0.025f, 0.2f), y, false));
                    break;
                }
                case 10: // chef hat
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.15f, 0), new Vector3(0.21f, 0.05f, 0.195f), white, false));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3;
                        Add(made, Prims.Ball(h, new Vector3(Mathf.Sin(a) * 0.065f, 0.24f, Mathf.Cos(a) * 0.06f), Vector3.one * 0.12f, white));
                    }
                    Add(made, Prims.Ball(h, new Vector3(0, 0.28f, 0), Vector3.one * 0.13f, white));
                    break;
                case 11: // halo
                {
                    var ring = Add(made, Prims.Empty("halo", h, new Vector3(0, 0.25f, 0))).transform;
                    var glow = Prims.Emissive(Prims.Hex("ffe27a"), 2.5f);
                    for (int i = 0; i < 18; i++)
                    {
                        float a = i * Mathf.PI * 2 / 18;
                        Prims.Ball(ring, new Vector3(Mathf.Sin(a) * 0.11f, 0, Mathf.Cos(a) * 0.11f), new Vector3(0.04f, 0.018f, 0.04f), glow);
                    }
                    var sp = ring.gameObject.AddComponent<Spinner>();
                    sp.speed = 30f; sp.bob = 0.01f;
                    break;
                }
                case 12: // propeller cap
                {
                    Add(made, Prims.Ball(h, new Vector3(0, 0.1f, 0), new Vector3(0.226f, 0.13f, 0.206f), col));
                    Add(made, Prims.Box(h, new Vector3(0, 0.12f, 0), new Vector3(0.02f, 0.12f, 0.2f), L("f5e663", 0.4f), false));
                    Add(made, Prims.Box(h, new Vector3(0, 0.12f, 0), new Vector3(0.226f, 0.12f, 0.02f), L("e63b2e", 0.4f), false));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.085f, 0.115f), new Vector3(0.16f, 0.014f, 0.12f), dark));
                    Add(made, Prims.Cyl(h, new Vector3(0, 0.185f, 0), 0.012f, 0.05f, black, Vector3.up));
                    var prop = Add(made, Prims.Empty("propeller", h, new Vector3(0, 0.212f, 0))).transform;
                    Prims.Box(prop, Vector3.zero, new Vector3(0.24f, 0.006f, 0.035f), L("e63b2e", 0.5f, 0f, Surface.Plastic), false, Quaternion.Euler(12, 0, 0));
                    Prims.Box(prop, Vector3.zero, new Vector3(0.035f, 0.006f, 0.24f), L("2a7fff", 0.5f, 0f, Surface.Plastic), false, Quaternion.Euler(0, 0, 12));
                    prop.gameObject.AddComponent<Spinner>().speed = 420f;
                    break;
                }
                case 13: // headphones
                {
                    for (int i = 0; i <= 8; i++)
                    {
                        float a = Mathf.Lerp(-80f, 80f, i / 8f) * Mathf.Deg2Rad;
                        Add(made, Prims.Box(h, new Vector3(Mathf.Sin(a) * 0.12f, 0.02f + Mathf.Cos(a) * 0.14f, 0), new Vector3(0.035f, 0.016f, 0.035f), black, false, Quaternion.Euler(0, 0, -a * Mathf.Rad2Deg)));
                    }
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Add(made, Prims.Cyl(h, new Vector3(s * 0.115f, 0.0f, 0), 0.1f, 0.045f, col, Vector3.right));
                        Add(made, Prims.Cyl(h, new Vector3(s * 0.14f, 0.0f, 0), 0.06f, 0.01f, black, Vector3.right));
                    }
                    break;
                }
                case 14: // army helmet
                {
                    var olive = Prims.Lit(Prims.Hex("4b5320"), 0.25f, 0.2f, Surface.PaintedMetal);
                    Add(made, Prims.Ball(h, new Vector3(0, 0.095f, -0.005f), new Vector3(0.25f, 0.19f, 0.245f), olive));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.065f, -0.005f), new Vector3(0.275f, 0.02f, 0.27f), olive));
                    Add(made, Prims.Box(h, new Vector3(0, 0.12f, 0.115f), new Vector3(0.06f, 0.03f, 0.02f), black, false));
                    foreach (float s in new[] { -1f, 1f })
                        Add(made, Prims.Box(h, new Vector3(s * 0.1f, -0.03f, 0.02f), new Vector3(0.008f, 0.14f, 0.012f), black, false, Quaternion.Euler(0, 0, -s * 8f)));
                    break;
                }
                case 15: // pirate hat (a tricorn with a skull)
                {
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.16f, 0), new Vector3(0.22f, 0.05f, 0.21f), black, false));
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (i * 120f + 0f) * Mathf.Deg2Rad;
                        var p = new Vector3(Mathf.Sin(a) * 0.14f, 0.16f, Mathf.Cos(a) * 0.13f);
                        Add(made, Prims.Box(h, p, new Vector3(0.2f, 0.11f, 0.012f), black, false, Quaternion.Euler(-18f, a * Mathf.Rad2Deg, 0)));
                        Add(made, Prims.Box(h, p + Vector3.up * 0.056f, new Vector3(0.2f, 0.008f, 0.016f), gold, false, Quaternion.Euler(-18f, a * Mathf.Rad2Deg, 0)));
                    }
                    Add(made, Prims.Ball(h, new Vector3(0, 0.17f, 0.15f), new Vector3(0.04f, 0.04f, 0.01f), white));
                    break;
                }
                case 16: // wizard hat with stars
                {
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.11f, 0), new Vector3(0.34f, 0.006f, 0.32f), col, false));
                    Taper(h, made, 0.11f, 0.48f, 0.2f, 0.02f, 12, i => col, -0.06f);
                    var star = Prims.Emissive(Prims.Hex("ffe27a"), 2f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.3f, y = 0.15f + i * 0.055f, r = Mathf.Lerp(0.1f, 0.04f, i / 5f);
                        Add(made, Prims.Box(h, new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r), Vector3.one * 0.02f, star, false, Quaternion.Euler(0, a * 57f, 45)));
                    }
                    break;
                }
                case 17: // bucket hat
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.16f, 0), new Vector3(0.22f, 0.045f, 0.205f), col, false));
                    Add(made, Prims.Ball(h, new Vector3(0, 0.2f, 0), new Vector3(0.22f, 0.03f, 0.205f), col));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.11f, 0), new Vector3(0.31f, 0.01f, 0.29f), dark, false));
                    break;
                case 18: // traffic cone
                {
                    var orange = L("ff6a13", 0.45f, 0f, Surface.Plastic);
                    Add(made, Prims.Box(h, new Vector3(0, 0.13f, 0), new Vector3(0.27f, 0.02f, 0.27f), orange, false));
                    Taper(h, made, 0.14f, 0.43f, 0.21f, 0.04f, 8, i => i == 3 || i == 5 ? white : orange);
                    break;
                }
                case 19: // santa hat, flopping to one side
                {
                    var red = L("c8202a", 0.25f);
                    Taper(h, made, 0.12f, 0.36f, 0.215f, 0.04f, 9, i => red, 0.1f);
                    Add(made, Prims.Make(PrimitiveType.Cylinder, h, new Vector3(0, 0.11f, 0), new Vector3(0.235f, 0.03f, 0.22f), white, false));
                    Add(made, Prims.Ball(h, new Vector3(0.1f, 0.36f, 0), Vector3.one * 0.06f, white));
                    break;
                }
            }
        }

        /* ======================= tops ======================= */

        static void Top(Body b, int top, Color c, List<GameObject> made)
        {
            var t = b.torso;
            Material body, upper, lower = null;
            var col = L(c, 0.3f);
            var dark = L(Color.Lerp(c, Color.black, 0.4f), 0.3f);
            var white = L("f2f2f2", 0.3f);
            switch (top)
            {
                case 1: // t-shirt
                    body = upper = col;
                    Add(made, Prims.Make(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.11f, 0.012f, 0.1f), dark, false));
                    break;
                case 2: // hoodie
                    body = upper = lower = col;
                    Add(made, Prims.Ball(t, new Vector3(0, 0.27f, -0.08f), new Vector3(0.21f, 0.13f, 0.12f), col));
                    Add(made, Prims.Box(t, new Vector3(0, -0.2f, 0.096f), new Vector3(0.15f, 0.07f, 0.012f), dark, false));
                    foreach (float s in new[] { -1f, 1f })
                        Add(made, Prims.Capsule(t, new Vector3(s * 0.025f, 0.2f, 0.1f), 0.008f, 0.09f, white, Vector3.up));
                    break;
                case 3: // tank top
                    body = col;
                    upper = null;
                    break;
                case 4: // suit and tie
                {
                    var suit = L("23262e", 0.35f);
                    body = upper = lower = suit;
                    Add(made, Prims.Box(t, new Vector3(0, 0.17f, 0.09f), new Vector3(0.08f, 0.16f, 0.012f), white, false, Quaternion.Euler(-14, 0, 0)));
                    Add(made, Prims.Box(t, new Vector3(0, 0.07f, 0.1f), new Vector3(0.03f, 0.17f, 0.01f), L("b3192a", 0.5f), false));
                    Add(made, Prims.Box(t, new Vector3(0, 0.16f, 0.101f), new Vector3(0.035f, 0.03f, 0.014f), L("8a1020", 0.5f), false));
                    foreach (float y in new[] { -0.08f, -0.16f })
                        Add(made, Prims.Ball(t, new Vector3(0.03f, y, 0.1f), Vector3.one * 0.014f, L("111111", 0.6f)));
                    break;
                }
                case 5: // camo jacket
                {
                    var camo = Prims.Lit(Color.white, 0.15f, 0f, Surface.Camo);
                    body = upper = lower = camo;
                    Add(made, Prims.Make(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.13f, 0.025f, 0.12f), L("3c4220", 0.2f), false));
                    foreach (float s in new[] { -1f, 1f })
                        Add(made, Prims.Box(t, new Vector3(s * 0.06f, 0.08f, 0.097f), new Vector3(0.07f, 0.06f, 0.014f), L("4b5320", 0.2f), false));
                    break;
                }
                case 6: // hawaiian shirt with flowers
                {
                    body = upper = col;
                    var rnd = new System.Random(3);
                    var petals = new[] { white, L("ffe066", 0.4f), L("ff7eb6", 0.4f) };
                    for (int i = 0; i < 16; i++)
                    {
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2, y = -0.3f + (float)rnd.NextDouble() * 0.48f;
                        var p = new Vector3(Mathf.Sin(a) * 0.118f, y, Mathf.Cos(a) * 0.098f);
                        Add(made, Prims.Ball(t, p, new Vector3(0.035f, 0.035f, 0.035f), petals[i % 3]));
                    }
                    break;
                }
                case 7: // hi-vis vest
                {
                    body = L("ffb000", 0.4f, 0f, Surface.Plastic);
                    upper = null;
                    var stripe = Prims.Lit(Prims.Hex("e8eef2"), 0.9f, 0.6f, Surface.Metal);
                    foreach (float y in new[] { -0.15f, 0.0f })
                        Add(made, Prims.Make(PrimitiveType.Cylinder, t, new Vector3(0, y, 0), new Vector3(0.247f, 0.014f, 0.207f), stripe, false));
                    break;
                }
                case 8: // sports jersey with a number
                {
                    body = upper = col;
                    var num = Color.Lerp(c, Color.white, 0.9f);
                    if (c.grayscale > 0.75f) num = new Color(0.1f, 0.1f, 0.12f);
                    foreach (float s in new[] { 1f, -1f })
                    {
                        var tx = UIKit.Text(t, "7", new Vector3(0, -0.02f, s * 0.104f), 0.16f, num);
                        tx.transform.localRotation = Quaternion.Euler(0, s > 0 ? 180 : 0, 0);
                        tx.fontStyle = FontStyle.Bold;
                        Add(made, tx.gameObject);
                    }
                    Add(made, Prims.Make(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.11f, 0.014f, 0.1f), L(num, 0.3f), false));
                    break;
                }
                case 9: // puffer jacket
                    body = upper = lower = col;
                    for (int i = 0; i < 5; i++)
                        Add(made, Prims.Make(PrimitiveType.Cylinder, t, new Vector3(0, -0.3f + i * 0.11f, 0), new Vector3(0.262f, 0.03f, 0.222f), col, false));
                    Add(made, Prims.Make(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.15f, 0.04f, 0.14f), dark, false));
                    break;
                case 10: // lab coat
                {
                    var coat = L("f4f6f8", 0.3f);
                    body = upper = lower = coat;
                    Add(made, Prims.Make(PrimitiveType.Cylinder, b.pelvis, new Vector3(0, -0.06f, 0), new Vector3(0.245f, 0.13f, 0.205f), coat, false));
                    Add(made, Prims.Box(t, new Vector3(-0.06f, 0.05f, 0.096f), new Vector3(0.06f, 0.05f, 0.012f), L("e4e8ec", 0.3f), false));
                    Add(made, Prims.Cyl(t, new Vector3(-0.07f, 0.09f, 0.1f), 0.01f, 0.06f, L("2a5ad0", 0.6f, 0f, Surface.Plastic), Vector3.up));
                    Add(made, Prims.Box(t, new Vector3(0, 0.14f, 0.09f), new Vector3(0.09f, 0.14f, 0.012f), L("cfd5dc", 0.3f), false, Quaternion.Euler(-14, 0, 0)));
                    break;
                }
                default: return;
            }
            b.torsoBase.sharedMaterial = body;
            for (int s = 0; s < 2; s++)
            {
                if (upper != null) b.upperArmVis[s].sharedMaterial = upper;
                if (lower != null) b.lowerArmVis[s].sharedMaterial = lower;
            }
        }

        /* ======================= pants and shoes ======================= */

        static void PantsOn(Body b, int pants, List<GameObject> made)
        {
            Material m;
            bool shins = true;
            switch (pants)
            {
                case 1: m = L("33507f", 0.2f); break;
                case 2: m = L("b99a62", 0.2f); shins = false; break;
                case 3: m = Prims.Lit(Color.white, 0.15f, 0f, Surface.Camo); break;
                case 4: m = L("1c1d22", 0.35f); break;
                default: m = L("8c8460", 0.2f); break;
            }
            b.pelvisBase.sharedMaterial = m;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                b.thighVis[s].sharedMaterial = m;
                if (shins) b.shinVis[s].sharedMaterial = m;
                if (pants == 4)
                    foreach (var vis in new[] { b.thighVis[s].transform, b.shinVis[s].transform })
                        Add(made, Prims.Box(vis, new Vector3(side * 0.48f, 0, 0), new Vector3(0.12f, 1.9f, 0.18f), L("f2f2f2", 0.3f), false));
                if (pants == 5)
                    Add(made, Prims.Box(b.thighVis[s].transform, new Vector3(side * 0.5f, -0.1f, 0.05f), new Vector3(0.2f, 0.35f, 0.6f), L("756d4c", 0.2f), false));
            }
            Add(made, Prims.Make(PrimitiveType.Cylinder, b.pelvis, new Vector3(0, 0.07f, 0), new Vector3(0.205f, 0.014f, 0.175f),
                pants == 4 ? L("f2f2f2", 0.3f) : L("3a2414", 0.4f, 0f, Surface.Rubber), false));
            if (pants != 4) Add(made, Prims.Box(b.pelvis, new Vector3(0, 0.07f, 0.088f), new Vector3(0.035f, 0.025f, 0.01f), Prims.Lit(Prims.Hex("c9ccd1"), 0.8f, 0.9f, Surface.Metal), false));
        }

        static void ShoesOn(Body b, int shoes, Color c, List<GameObject> made)
        {
            Material m, sole = L("f2f2f2", 0.35f, 0f, Surface.Rubber);
            bool ankle = false;
            switch (shoes)
            {
                case 1: m = L("d63a2f", 0.4f, 0f, Surface.Plastic); break;
                case 2: m = L("6b4524", 0.3f, 0f, Surface.Wood); ankle = true; sole = L("2a1c10", 0.3f, 0f, Surface.Rubber); break;
                case 3: m = L("17181a", 0.35f, 0f, Surface.Rubber); ankle = true; sole = L("0d0d0d", 0.3f, 0f, Surface.Rubber); break;
                default: m = L("6a3fc4", 0.45f, 0f, Surface.Plastic); ankle = true; break;
            }
            for (int s = 0; s < 2; s++)
            {
                b.footBase[s].sharedMaterial = m;
                var f = b.foot[s];
                Add(made, Prims.Box(f, new Vector3(0, 0.006f, 0.05f), new Vector3(0.082f, 0.016f, 0.21f), sole, false));
                if (ankle) Add(made, Prims.Cyl(f, new Vector3(0, 0.075f, 0.0f), 0.085f, 0.1f, m, Vector3.up));
                if (shoes == 1 || shoes == 4)
                    for (int i = 0; i < 3; i++) Add(made, Prims.Box(f, new Vector3(0, 0.06f, 0.06f + i * 0.025f), new Vector3(0.04f, 0.006f, 0.008f), sole, false));
                if (shoes == 4) Add(made, Prims.Cyl(f, new Vector3(0, 0.075f, 0.0f), 0.087f, 0.02f, sole, Vector3.up));
            }
        }

        /* ======================= gloves ======================= */

        /// <summary>The colours for the first-person hands: (main, cuff).</summary>
        public static (Color main, Color cuff) GloveColors(int gloves)
        {
            switch (gloves)
            {
                case 1: return (new Color(0.08f, 0.08f, 0.09f), new Color(0.08f, 0.08f, 0.09f));
                case 2: return (new Color(0.8f, 0.1f, 0.1f), new Color(0.95f, 0.95f, 0.95f));
                case 3: return (new Color(0.72f, 0.56f, 0.33f), new Color(0.45f, 0.3f, 0.15f));
                case 4: return (new Color(0.97f, 0.82f, 0.15f), new Color(0.97f, 0.82f, 0.15f));
                case 5: return (new Color(0.96f, 0.96f, 0.96f), new Color(0.96f, 0.96f, 0.96f));
                case 6: return (new Color(0.29f, 0.33f, 0.13f), new Color(0.12f, 0.12f, 0.12f));
                case 7: return (new Color(0.8f, 0.15f, 0.18f), new Color(0.96f, 0.96f, 0.96f));
                default: return (new Color(0.15f, 0.15f, 0.17f), UIKit.Accent);
            }
        }

        static void GlovesOn(Body b, int gloves, List<GameObject> made)
        {
            var (mainC, cuffC) = GloveColors(gloves);
            var main = L(mainC, gloves == 4 ? 0.85f : 0.3f, 0f, gloves == 3 ? Surface.Wood : Surface.Rubber);
            var cuff = L(cuffC, 0.3f);
            for (int s = 0; s < 2; s++)
            {
                var h = b.hand[s];
                var parts = b.handParts[s];
                // parts[0] is the palm, the rest are fingers (the last one is the thumb)
                if (gloves == 1) parts[0].sharedMaterial = main;
                else if (gloves != 2) foreach (var r in parts) r.sharedMaterial = main;
                switch (gloves)
                {
                    case 2: // boxing gloves: a big padded mitt
                        Add(made, Prims.Ball(h, new Vector3(0, 0.005f, 0.015f), new Vector3(0.105f, 0.09f, 0.13f), main));
                        Add(made, Prims.Cyl(h, new Vector3(0, 0, -0.055f), 0.075f, 0.05f, cuff, Vector3.forward));
                        break;
                    case 4: // long rubber cuffs
                        Add(made, Prims.Cyl(h, new Vector3(0, 0, -0.07f), 0.07f, 0.08f, main, Vector3.forward));
                        break;
                    case 5: // cartoon gloves: puffy cuff and three lines on the back
                        Add(made, Prims.Cyl(h, new Vector3(0, 0, -0.06f), 0.078f, 0.03f, cuff, Vector3.forward));
                        for (int i = 0; i < 3; i++) Add(made, Prims.Box(h, new Vector3((i - 1) * 0.014f, 0.013f, -0.01f), new Vector3(0.004f, 0.003f, 0.04f), L("111111", 0.3f), false));
                        break;
                    case 6: // tactical: knuckle pad
                        Add(made, Prims.Box(h, new Vector3(0, 0.014f, 0.022f), new Vector3(0.06f, 0.012f, 0.025f), cuff, false));
                        Add(made, Prims.Cyl(h, new Vector3(0, 0, -0.055f), 0.066f, 0.03f, cuff, Vector3.forward));
                        break;
                    case 7: // mittens
                        Add(made, Prims.Ball(h, new Vector3(0, 0, 0.02f), new Vector3(0.075f, 0.045f, 0.12f), main));
                        Add(made, Prims.Cyl(h, new Vector3(0, 0, -0.055f), 0.072f, 0.035f, cuff, Vector3.forward));
                        break;
                    case 3: // work gloves: a cuff
                        Add(made, Prims.Cyl(h, new Vector3(0, 0, -0.055f), 0.07f, 0.035f, cuff, Vector3.forward));
                        break;
                }
            }
        }
    }
}
