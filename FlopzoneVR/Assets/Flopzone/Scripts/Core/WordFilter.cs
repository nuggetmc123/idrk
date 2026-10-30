using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Flopzone
{
    /// <summary>
    /// Name and chat filter. Kept in sync by hand with Server/src/filter.js —
    /// the server re-checks everything, this copy just gives instant feedback
    /// (and covers offline play).
    ///
    /// Chat may say "fuck", "shit" and "damn" (and words built on them).
    /// Anything else on these lists auto-mutes the sender for 15 minutes.
    /// Names allow no swearing at all.
    /// </summary>
    public static class WordFilter
    {
        public const int NameMin = 3;
        public const int NameMax = 16;
        public const int AutoMuteMinutes = 15;

        static readonly string[] SubstringWords =
        {
            "niger", "niga", "nigr", "negro", "fagot", "faget", "trany", "tranie", "retard",
            "chink", "kike", "beaner", "wetback", "raghead", "towelhead", "cunt", "whore",
            "slut", "bitch", "pusy", "penis", "vagina", "dildo", "porn", "nazi", "hitler",
            "wank", "twat", "bastard", "jiz", "ashole", "dick", "molest", "pedophil",
            "bolock", "skank", "hentai", "blowjob", "handjob", "nude", "naked", "orgasm", "clit"
        };

        static readonly HashSet<string> WholeWords = new HashSet<string>
        {
            "ass", "asses", "dumbass", "jackass", "smartass", "fatass", "badass", "kissass",
            "cock", "cocks", "cum", "cums", "cumming", "tit", "tits", "titty", "titties",
            "boob", "boobs", "fag", "fags", "spic", "spics", "coon", "coons", "gook", "gooks",
            "dyke", "dykes", "homo", "kys", "rape", "raped", "rapist", "raping", "hoe", "hoes",
            "thot", "cuck", "sex", "sexy", "milf", "chode", "jap", "japs", "paki", "pakis",
            "wop", "kraut", "tard", "tards", "piss", "pissed", "pissing", "anal", "horny",
            "boner", "boners", "semen", "prick", "pricks", "pedo", "pedos"
        };

        static readonly string[] Phrases = { "kilyourself", "kilurself", "gokilyourself", "hangyourself", "unalive" };

        static readonly string[] ChatAllowedRoots = { "fuck", "shit", "damn" };

        static readonly string[] NameExtra = { "fuck", "shit", "damn", "fck", "fuk", "sht", "dumbass", "jackass", "cock", "boob", "titty", "tities", "sexy" };

        static readonly string[] Reserved = { "admin", "moderator", "mod", "owner", "flopzone", "sloppy", "system", "server" };

        static readonly Regex Repeat1 = new Regex(@"(.)\1+");
        static readonly Regex Repeat2 = new Regex(@"(.)\1{2,}");
        static readonly Regex NonLetters = new Regex("[^a-z]+");
        static readonly Regex[] RootPatterns = ChatAllowedRoots
            .Select(r => new Regex(string.Concat(r.Select(ch => ch + "+")))).ToArray();

        static string Collapse1(string s) => Repeat1.Replace(s, "$1");
        static string Collapse2(string s) => Repeat2.Replace(s, "$1$1");

        static string Leet(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char raw in s.ToLowerInvariant())
            {
                char c = raw;
                switch (raw)
                {
                    case '@': case '4': c = 'a'; break;
                    case '3': c = 'e'; break;
                    case '1': case '!': case '|': c = 'i'; break;
                    case '0': c = 'o'; break;
                    case '5': case '$': c = 's'; break;
                    case '7': case '+': c = 't'; break;
                    case '8': c = 'b'; break;
                    case '9': c = 'g'; break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string StripInvisible(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c < 32 || c == 127) continue;
                if (c >= '​' && c <= '‏') continue;
                if (c >= '‪' && c <= '‮') continue;
                if (c >= '⁠' && c <= '⁤') continue;
                if (c == '﻿') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        static bool WordIsBad(string word, bool allowRoots)
        {
            string w2 = Collapse2(word);
            if (allowRoots) foreach (var re in RootPatterns) w2 = re.Replace(w2, " ");
            foreach (var part in w2.Split(' '))
            {
                if (part.Length == 0) continue;
                if (WholeWords.Contains(part)) return true;
                string p1 = Collapse1(part);
                foreach (var b in SubstringWords) if (p1.Contains(b)) return true;
            }
            return false;
        }

        /// <summary>Null if the chat message is fine, else why it isn't.</summary>
        public static string CheckChat(string text)
        {
            string clean = Leet(StripInvisible(text ?? ""));
            string letters = Collapse1(NonLetters.Replace(clean, ""));
            foreach (var p in Phrases) if (letters.Contains(p)) return "harmful phrase";
            var words = NonLetters.Split(clean).Where(w => w.Length > 0).ToList();
            foreach (var w in words) if (WordIsBad(w, true)) return "bad language";
            // spaced-out words ("n i g g e r"): glue runs of single letters back together
            var joined = new List<string>();
            bool lastSingle = false;
            foreach (var w in words)
            {
                if (w.Length == 1 && lastSingle) joined[joined.Count - 1] += w;
                else joined.Add(w);
                lastSingle = w.Length == 1;
            }
            foreach (var w in joined) if (w.Length > 2 && WordIsBad(w, true)) return "bad language";
            return null;
        }

        /// <summary>Null if the name is allowed, else a message for the player.</summary>
        public static string CheckName(string name)
        {
            string raw = StripInvisible(name ?? "").Trim();
            if (raw.Length < NameMin) return "Names need at least " + NameMin + " characters.";
            if (raw.Length > NameMax) return "Names can be at most " + NameMax + " characters.";
            foreach (char c in raw)
                if (!(c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                    return "Use only letters, numbers and _ in your name.";
            string norm = Leet(raw).Replace("_", "");
            string lettersOnly = NonLetters.Replace(norm, "");
            string c1 = Collapse1(lettersOnly);
            string c2 = Collapse2(lettersOnly);
            foreach (var b in SubstringWords) if (c1.Contains(b)) return "That name is not allowed.";
            foreach (var b in NameExtra) if (c1.Contains(Collapse1(b))) return "That name is not allowed.";
            foreach (var p in Phrases) if (c1.Contains(p)) return "That name is not allowed.";
            var pieces = Regex.Split(Leet(raw), "[_0-9]+").Where(p => p.Length > 0).Select(Collapse2);
            if (pieces.Any(p => WholeWords.Contains(p)) || WholeWords.Contains(c2)) return "That name is not allowed.";
            string lower = raw.ToLowerInvariant().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (Reserved.Contains(lower)) return "That name is reserved.";
            return null;
        }
    }
}
