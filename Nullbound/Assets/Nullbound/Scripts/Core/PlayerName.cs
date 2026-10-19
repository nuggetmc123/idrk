using UnityEngine;

namespace Nullbound
{
    /// <summary>Your fighter name (a random one the first time; saved on this headset).</summary>
    public static class PlayerName
    {
        const string Key = "nb.name";
        static readonly string[] First = { "Iron", "Neon", "Storm", "Shadow", "Blaze", "Frost", "Volt", "Rogue", "Titan", "Swift", "Night", "Crimson" };
        static readonly string[] Second = { "Fist", "Fang", "Wolf", "Hawk", "Viper", "Ronin", "Comet", "Golem", "Ghost", "Drake" };

        public static string Get()
        {
            string n = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(n))
            {
                n = First[Random.Range(0, First.Length)] + Second[Random.Range(0, Second.Length)];
                if (n.Length > 14) n = n.Substring(0, 14);
                PlayerPrefs.SetString(Key, n);
                PlayerPrefs.Save();
            }
            return n;
        }

        /// <summary>Roll a new random name.</summary>
        public static string Reroll()
        {
            PlayerPrefs.DeleteKey(Key);
            return Get();
        }
    }
}
