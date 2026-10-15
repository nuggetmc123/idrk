using UnityEngine;

namespace KartVR
{
    public enum KartPattern : byte { Solid, RacingStripes, TwoTone, Checker, Flames, Camo }
    public enum KartBooster : byte { None, TwinRockets, Jet, MegaRockets }
    public enum KartWheels : byte { Standard, Chunky, Racing }
    public enum KartSpoiler : byte { None, Lip, Wing, BigWing }

    /// <summary>Everything you can change about your kart in your garage.</summary>
    [System.Serializable]
    public struct KartLook
    {
        public int paint;    // index into PlayerProfile.Palette
        public int accent;   // index into PlayerProfile.Palette
        public KartPattern pattern;
        public KartBooster booster;
        public KartWheels wheels;
        public KartSpoiler spoiler;

        /// <summary>Packs the look into one int so it's a single networked value.</summary>
        public int Pack()
        {
            return (paint & 0xF) | (accent & 0xF) << 4 | ((int)pattern & 0xF) << 8 | ((int)booster & 0xF) << 12 |
                   ((int)wheels & 0xF) << 16 | ((int)spoiler & 0xF) << 20;
        }

        public static KartLook Unpack(int v)
        {
            return new KartLook
            {
                paint = Mathf.Clamp(v & 0xF, 0, PlayerProfile.Palette.Length - 1),
                accent = Mathf.Clamp(v >> 4 & 0xF, 0, PlayerProfile.Palette.Length - 1),
                pattern = (KartPattern)Mathf.Clamp(v >> 8 & 0xF, 0, Count<KartPattern>() - 1),
                booster = (KartBooster)Mathf.Clamp(v >> 12 & 0xF, 0, Count<KartBooster>() - 1),
                wheels = (KartWheels)Mathf.Clamp(v >> 16 & 0xF, 0, Count<KartWheels>() - 1),
                spoiler = (KartSpoiler)Mathf.Clamp(v >> 20 & 0xF, 0, Count<KartSpoiler>() - 1),
            };
        }

        public static int Count<T>() => System.Enum.GetValues(typeof(T)).Length;

        public Color PaintColor => PlayerProfile.GetColor(paint);
        public Color AccentColor => PlayerProfile.GetColor(accent);
    }

    /// <summary>Names shown in the garage, and what each booster does.</summary>
    public static class KartParts
    {
        public static string Name(KartPattern p)
        {
            switch (p)
            {
                case KartPattern.RacingStripes: return "Racing Stripes";
                case KartPattern.TwoTone: return "Two-Tone";
                case KartPattern.Checker: return "Checkered";
                case KartPattern.Flames: return "Flames";
                case KartPattern.Camo: return "Camo";
                default: return "Solid";
            }
        }

        public static string Name(KartBooster b)
        {
            switch (b)
            {
                case KartBooster.TwinRockets: return "Twin Rockets";
                case KartBooster.Jet: return "Jet Engine";
                case KartBooster.MegaRockets: return "Mega Rockets";
                default: return "No Booster";
            }
        }

        public static string Name(KartWheels w)
        {
            switch (w)
            {
                case KartWheels.Chunky: return "Chunky Off-Road";
                case KartWheels.Racing: return "Racing Slicks";
                default: return "Standard";
            }
        }

        public static string Name(KartSpoiler s)
        {
            switch (s)
            {
                case KartSpoiler.Lip: return "Lip Spoiler";
                case KartSpoiler.Wing: return "Wing";
                case KartSpoiler.BigWing: return "Big Wing";
                default: return "No Spoiler";
            }
        }

        /// <summary>Boost button stats. Every booster is a trade-off so none is strictly best.</summary>
        public struct BoosterStats
        {
            public float seconds;    // how long one boost lasts
            public float recharge;   // seconds to refill from empty
            public float speedMul;   // top speed multiplier while boosting
        }

        public static BoosterStats Stats(KartBooster b)
        {
            switch (b)
            {
                case KartBooster.TwinRockets: return new BoosterStats { seconds = 1.2f, recharge = 5f, speedMul = 1.3f };
                case KartBooster.Jet: return new BoosterStats { seconds = 2.2f, recharge = 8f, speedMul = 1.22f };
                case KartBooster.MegaRockets: return new BoosterStats { seconds = 0.9f, recharge = 7f, speedMul = 1.5f };
                default: return new BoosterStats { seconds = 0f, recharge = 1f, speedMul = 1f };
            }
        }

        public static string Describe(KartBooster b)
        {
            if (b == KartBooster.None) return "Add a booster to get a boost button (A)";
            var s = Stats(b);
            return "Boost " + s.seconds.ToString("0.0") + "s  x" + s.speedMul.ToString("0.00") + " speed  recharge " + s.recharge.ToString("0") + "s";
        }
    }
}
