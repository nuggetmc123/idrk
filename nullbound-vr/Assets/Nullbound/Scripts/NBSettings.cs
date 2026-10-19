using UnityEngine;

namespace Nullbound
{
    /// Everything the player can change, saved on the headset with PlayerPrefs.
    public static class NBSettings
    {
        static int GetInt(string k, int d) => PlayerPrefs.GetInt(k, d);
        static void SetInt(string k, int v) { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); }
        static float GetFloat(string k, float d) => PlayerPrefs.GetFloat(k, d);
        static void SetFloat(string k, float v) { PlayerPrefs.SetFloat(k, v); PlayerPrefs.Save(); }

        public static bool SetupDone { get => GetInt("nb_setup", 0) == 1; set => SetInt("nb_setup", value ? 1 : 0); }
        public static bool LeftHanded { get => GetInt("nb_lefty", 0) == 1; set => SetInt("nb_lefty", value ? 1 : 0); }
        public static bool SnapTurn { get => GetInt("nb_snap", 1) == 1; set => SetInt("nb_snap", value ? 1 : 0); }
        public static int SnapAngle { get => GetInt("nb_snapdeg", 45); set => SetInt("nb_snapdeg", Mathf.Clamp(value, 15, 90)); }
        public static float TurnSpeed { get => GetFloat("nb_turnspd", 140f); set => SetFloat("nb_turnspd", Mathf.Clamp(value, 45f, 300f)); }
        public static bool Vignette { get => GetInt("nb_vig", 1) == 1; set => SetInt("nb_vig", value ? 1 : 0); }
        public static bool ReducedKnockback { get => GetInt("nb_lowkb", 0) == 1; set => SetInt("nb_lowkb", value ? 1 : 0); }
        public static bool Seated { get => GetInt("nb_seated", 0) == 1; set => SetInt("nb_seated", value ? 1 : 0); }
        /// Metres added to the tracked head height (seated play, or a floor that tracks too low/high).
        public static float HeightOffset { get => GetFloat("nb_hoff", 0f); set => SetFloat("nb_hoff", Mathf.Clamp(value, -1f, 1.5f)); }
        public static float Volume { get => GetFloat("nb_vol", 0.8f); set { SetFloat("nb_vol", Mathf.Clamp01(value)); AudioListener.volume = Volume; } }
        public static int Fighter { get => Mathf.Clamp(GetInt("nb_fighter", 0), 0, NBFighters.All.Length - 1); set => SetInt("nb_fighter", value); }
        public static int Bots { get => GetInt("nb_bots", 4); set => SetInt("nb_bots", Mathf.Clamp(value, 0, NBNet.MaxPlayers - 1)); }
        public static string JoinCode { get => PlayerPrefs.GetString("nb_code", ""); set { PlayerPrefs.SetString("nb_code", value); PlayerPrefs.Save(); } }
        public static string LanIp { get => PlayerPrefs.GetString("nb_ip", "192.168.1."); set { PlayerPrefs.SetString("nb_ip", value); PlayerPrefs.Save(); } }

        public static string PlayerName
        {
            get
            {
                var n = PlayerPrefs.GetString("nb_name", "");
                if (string.IsNullOrEmpty(n))
                {
                    n = "FIGHTER" + Random.Range(100, 999);
                    PlayerName = n;
                }
                return n;
            }
            set { PlayerPrefs.SetString("nb_name", Clean(value)); PlayerPrefs.Save(); }
        }

        /// Names go over the network and above heads: keep them short, plain and polite.
        public static string Clean(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in (raw ?? "").ToUpperInvariant())
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == ' ')
                    if (sb.Length < 14) sb.Append(c);
            var s = sb.ToString().Trim();
            var squashed = s.Replace(" ", "").Replace("_", "");
            foreach (var bad in Blocked)
                if (squashed.Contains(bad)) return "FIGHTER" + Random.Range(100, 999);
            return s.Length == 0 ? "FIGHTER" : s;
        }

        static readonly string[] Blocked =
        {
            "FUCK", "SHIT", "BITCH", "CUNT", "DICK", "COCK", "PUSSY", "NIGG", "NIGA", "FAG", "RETARD",
            "WHORE", "SLUT", "RAPE", "NAZI", "HITLER", "KKK", "PENIS", "VAGINA", "SEX", "PORN", "ASSHOLE"
        };
    }
}
