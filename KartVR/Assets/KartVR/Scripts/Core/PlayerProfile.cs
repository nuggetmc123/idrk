using UnityEngine;

namespace KartVR
{
    /// <summary>Local, saved-on-device player settings: name and kart color.</summary>
    public static class PlayerProfile
    {
        const string NameKey = "kartvr.name";
        const string ColorKey = "kartvr.color";
        const string LookKey = "kartvr.look";

        public static readonly Color[] Palette =
        {
            new Color(0.90f, 0.20f, 0.20f), // red
            new Color(0.15f, 0.45f, 0.95f), // blue
            new Color(0.20f, 0.80f, 0.30f), // green
            new Color(0.98f, 0.80f, 0.15f), // yellow
            new Color(0.65f, 0.30f, 0.90f), // purple
            new Color(1.00f, 0.50f, 0.10f), // orange
            new Color(0.95f, 0.40f, 0.70f), // pink
            new Color(0.15f, 0.85f, 0.85f), // cyan
            new Color(0.95f, 0.95f, 0.95f), // white
            new Color(0.12f, 0.12f, 0.14f), // black
            new Color(0.62f, 0.66f, 0.72f), // silver
            new Color(0.60f, 0.95f, 0.15f), // lime
        };

        public static string Nickname
        {
            get
            {
                if (!PlayerPrefs.HasKey(NameKey))
                {
                    PlayerPrefs.SetString(NameKey, "Racer" + Random.Range(1000, 9999));
                    PlayerPrefs.Save();
                }
                return PlayerPrefs.GetString(NameKey);
            }
            set
            {
                PlayerPrefs.SetString(NameKey, value);
                PlayerPrefs.Save();
            }
        }

        public static int ColorIndex
        {
            get
            {
                if (!PlayerPrefs.HasKey(ColorKey))
                {
                    PlayerPrefs.SetInt(ColorKey, Random.Range(0, Palette.Length));
                    PlayerPrefs.Save();
                }
                return Mathf.Clamp(PlayerPrefs.GetInt(ColorKey), 0, Palette.Length - 1);
            }
            set
            {
                PlayerPrefs.SetInt(ColorKey, Mathf.Clamp(value, 0, Palette.Length - 1));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Your saved kart customization (paint, accent, pattern, booster, wheels, spoiler).</summary>
        public static KartLook Look
        {
            get
            {
                if (!PlayerPrefs.HasKey(LookKey))
                {
                    int paint = ColorIndex;
                    return new KartLook { paint = paint, accent = (paint + 3) % Palette.Length };
                }
                return KartLook.Unpack(PlayerPrefs.GetInt(LookKey));
            }
            set
            {
                PlayerPrefs.SetInt(LookKey, value.Pack());
                PlayerPrefs.SetInt(ColorKey, value.paint);
                PlayerPrefs.Save();
            }
        }

        public static Color GetColor(int index)
        {
            return Palette[Mathf.Abs(index) % Palette.Length];
        }
    }
}
