using UnityEngine;

namespace KartVR
{
    /// <summary>Local, saved-on-device player settings: name and kart color.</summary>
    public static class PlayerProfile
    {
        const string NameKey = "kartvr.name";
        const string ColorKey = "kartvr.color";

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

        public static Color GetColor(int index)
        {
            return Palette[Mathf.Abs(index) % Palette.Length];
        }
    }
}
