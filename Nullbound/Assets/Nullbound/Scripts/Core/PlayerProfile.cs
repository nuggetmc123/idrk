using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Your name, saved on this headset. You pick it in the main menu before you can play, it has to be one nobody
    /// else has (see <see cref="NameRegistry"/>), and it's locked after that: only a moderator can make you change it.
    /// </summary>
    public static class PlayerProfile
    {
        const string NameKey = "nb.name", LockedKey = "nb.nameLocked";

        public static string Nickname
        {
            get
            {
                if (!PlayerPrefs.HasKey(NameKey))
                {
                    PlayerPrefs.SetString(NameKey, "Player" + Random.Range(1000, 9999));
                    PlayerPrefs.Save();
                }
                return PlayerPrefs.GetString(NameKey);
            }
            private set
            {
                PlayerPrefs.SetString(NameKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True once you've picked your name.</summary>
        public static bool NameLocked => PlayerPrefs.GetInt(LockedKey, 0) == 1;

        /// <summary>Saves your chosen name for good (checked with NameRules and the registry first).</summary>
        public static void SetLockedName(string name)
        {
            Nickname = name.Trim();
            PlayerPrefs.SetInt(LockedKey, 1);
            PlayerPrefs.Save();
            if (Fighter.Local != null && Fighter.Local.IsReady) Fighter.Local.FighterName = Nickname;
        }

        /// <summary>A moderator asked for a new name: you get a temporary one until you pick it.</summary>
        public static void UnlockName()
        {
            PlayerPrefs.SetInt(LockedKey, 0);
            Nickname = "Player" + Random.Range(1000, 9999);
            if (Fighter.Local != null && Fighter.Local.IsReady) Fighter.Local.FighterName = Nickname;
        }
    }
}
