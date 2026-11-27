using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Clothes for your operator (picked at the WARDROBE in your bunker): a shirt, trousers, a hat and something on your
    /// face, each in a colour. Without picks you wear the combat uniform. Worn under any armour you put on. Saved in your profile as a short text ("look") and sent
    /// to other players so they see you dressed the same.
    /// </summary>
    public static class Cosmetics
    {
        // (3.48) everyone is dressed now: the first choice is the combat uniform (it used to be NONE, which left you a bare
        // figure) and NONE moved to the end, so saved picks keep their meaning
        public static readonly string[] Shirts = { "COMBAT SHIRT", "T-SHIRT", "LONG SLEEVE", "HOODIE", "JACKET", "TANK TOP", "NONE" };
        public static readonly string[] Pants = { "COMBAT PANTS", "CARGO PANTS", "JEANS", "SHORTS", "NONE" };
        public const int ShirtNone = 6, PantsNone = 4;
        public static readonly string[] Hats = { "NONE", "CAP", "BEANIE", "BANDANA" };
        public static readonly string[] Faces = { "NONE", "SUNGLASSES", "NECK GAITER", "GAS MASK" };
        /// <summary>(3.66) Shoes: combat boots, or OLIVER KICKS once you've put a pair on (hold one, pull the trigger).</summary>
        public static readonly string[] Shoes = { "COMBAT BOOTS", "OLIVER KICKS" };
        public static readonly (string name, Color col)[] Colors =
        {
            ("BLACK", new Color(0.07f, 0.07f, 0.08f)), ("WHITE", new Color(0.85f, 0.85f, 0.83f)), ("GREY", new Color(0.38f, 0.39f, 0.4f)),
            ("OLIVE", new Color(0.3f, 0.33f, 0.2f)), ("TAN", new Color(0.62f, 0.52f, 0.36f)), ("NAVY", new Color(0.1f, 0.14f, 0.28f)),
            ("DENIM", new Color(0.2f, 0.3f, 0.48f)), ("RED", new Color(0.6f, 0.1f, 0.08f)), ("ORANGE", new Color(0.85f, 0.4f, 0.08f)),
            ("FOREST", new Color(0.12f, 0.3f, 0.16f)), ("SKY BLUE", new Color(0.38f, 0.6f, 0.82f)), ("PURPLE", new Color(0.36f, 0.18f, 0.52f)),
            ("PINK", new Color(0.86f, 0.45f, 0.6f)), ("YELLOW", new Color(0.88f, 0.75f, 0.15f)), ("BROWN", new Color(0.32f, 0.2f, 0.12f)),
        };

        public struct Look { public int shirt, shirtCol, pants, pantsCol, hat, hatCol, face, shoes; }

        public static Look Default => new Look { shirt = 0, shirtCol = 3, pants = 0, pantsCol = 2, hat = 0, hatCol = 0, face = 0 };   // olive combat shirt, grey combat pants

        public static string Encode(Look l) => l.shirt + "," + l.shirtCol + "," + l.pants + "," + l.pantsCol + "," + l.hat + "," + l.hatCol + "," + l.face + "," + l.shoes;

        public static Look Decode(string s)
        {
            var l = Default;
            if (string.IsNullOrEmpty(s)) return l;
            var p = s.Split(',');
            int I(int i, int max, int def) => p.Length > i && int.TryParse(p[i], out var v) ? Mathf.Clamp(v, 0, max - 1) : def;
            l.shirt = I(0, Shirts.Length, 0); l.shirtCol = I(1, Colors.Length, 3);
            l.pants = I(2, Pants.Length, 0); l.pantsCol = I(3, Colors.Length, 2);
            l.hat = I(4, Hats.Length, 0); l.hatCol = I(5, Colors.Length, 0);
            l.face = I(6, Faces.Length, 0);
            l.shoes = I(7, Shoes.Length, 0);
            return l;
        }

        public static Color Col(int i) => Colors[Mathf.Clamp(i, 0, Colors.Length - 1)].col;

        /// <summary>The local player's look (from their profile).</summary>
        public static string Mine => Profile.Current != null ? Profile.Current.look ?? "" : "";
    }
}
