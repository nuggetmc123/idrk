using System.Linq;
using System.Text;

namespace KartVR
{
    /// <summary>
    /// What names are allowed: 3-16 letters, numbers or _, nothing rude (also when spelled with numbers like
    /// "b4d" or "sh1t"), no pretending to be staff, and not a moderator's or banned name.
    /// Add more blocked names in moderation.json ("blockedNames").
    /// </summary>
    public static class NameRules
    {
        public const int MinLength = 3, MaxLength = 16;

        // Matched anywhere in the (cleaned up) name.
        static readonly string[] Rude =
        {
            "fuck", "fuk", "fck", "shit", "bitch", "cunt", "dick", "cock", "pussy", "nigg", "niga", "fag", "retard",
            "whore", "slut", "rape", "nazi", "hitler", "porn", "penis", "vagina", "boob", "tits", "dildo", "anal",
            "bastard", "wank", "twat", "kys", "killyourself", "semen", "horny", "sexy", "molest", "pedo",
        };
        // Short ones only count as the whole name (with numbers around it), so "class" and "grass" are fine.
        static readonly string[] RudeShort = { "ass", "sex", "cum", "tit", "kkk", "fu", "stfu" };
        static readonly string[] Staff = { "admin", "moderator", "owner", "staff", "developer", "official", "kartvrteam" };

        public static bool Valid(string name, out string problem)
        {
            problem = null;
            name = (name ?? "").Trim();
            if (name.Length < MinLength) { problem = "Too short (at least " + MinLength + ")"; return false; }
            if (name.Length > MaxLength) { problem = "Too long (" + MaxLength + " at most)"; return false; }
            if (!name.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_'))) { problem = "Only letters, numbers and _"; return false; }

            string clean = Clean(name, false), squashed = Clean(name, true); // "fuuuck" -> "fuck", but "boob" stays "boob" too
            string letters = new string(name.ToLowerInvariant().Where(char.IsLetter).ToArray()); // "ass69" -> "ass"
            if (IsRude(clean) || IsRude(squashed) || RudeShort.Contains(letters)) { problem = "That name isn't allowed"; return false; }
            if (!Moderation.IAmMod && Staff.Any(clean.Contains)) { problem = "You can't pretend to be staff"; return false; }
            if (Moderation.NameReservedForSomeoneElse(name)) { problem = "That name is taken"; return false; }
            if (Moderation.NameBlocked(name)) { problem = "That name isn't allowed"; return false; }
            return true;
        }

        static bool IsRude(string clean) =>
            Rude.Any(clean.Contains) ||
            RudeShort.Contains(clean);

        /// <summary>Lower case, look-alike numbers turned into letters, _ removed, and (if squash) repeated letters squashed.</summary>
        static string Clean(string name, bool squash)
        {
            var sb = new StringBuilder();
            foreach (char raw in name.ToLowerInvariant())
            {
                char c = raw;
                switch (c)
                {
                    case '0': c = 'o'; break;
                    case '1': c = 'i'; break;
                    case '3': c = 'e'; break;
                    case '4': c = 'a'; break;
                    case '5': c = 's'; break;
                    case '7': c = 't'; break;
                    case '8': c = 'b'; break;
                    case '_': continue;
                }
                if (squash && sb.Length > 0 && sb[sb.Length - 1] == c) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
