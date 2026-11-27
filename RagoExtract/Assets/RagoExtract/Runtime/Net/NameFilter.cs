using System;
using System.Collections.Generic;
using System.Text;

namespace Rago.Relay
{
    /// <summary>
    /// (3.58) Callsigns nobody should have: slurs, swearing and sexual words, also when spelled with numbers (5H1T),
    /// stretched (FUUUCK) or split with _ . Shared by the game (the callsign terminal refuses them) and the server (a
    /// player who has one is renamed OPERATOR on connect). The words are stored ROT13'd so the source isn't a list of them.
    /// </summary>
    public static class NameFilter
    {
        // anywhere in the name
        const string Anywhere = "SHPX SHPP SHX SIPX FUVG FULG PHAG CHFFL CHFFV CRAVF INTVAN OBBOF OBBOVRF GVGGVRF GVGGL GVGF AVCCYR CBEA URAGNV WVMM FRZRA BETNFZ ZNFGHEO JNAX OYBJWBO UNAQWBO QVYQB UBEAL ZVYS ENCVFG ENCVAT ZBYRFG CRQBCUVY CRQBSVY ANMV UVGYRE XXX FVRTURVY JUBER FYHG OVGPU ONFGNEQ NFFUBYR NEFRUBYR OBYYBPX GJNG SNTTBG SNTBG AVTT AVTN AVTRE ARTEB PUVAX XVXR JRGONPX GENAAL ERGNEQ ORNARE QLXR XVYYLBHEFRYS QVPX PBPXFHPX CVFF FRKL KKK PHZFUBG PHZZVAT OHGGUBYR AHGFNPX ONYYFNPX FPEBGHZ GRFGVPYR RERPGVBA OBARE PYVG FXNAX TBBX";
        // the whole name only (with any digits on the end): these hide inside ordinary words (ASSAULT, ARSENAL, ANALOG)
        const string Whole = "NFF NFFRF UBR UBRF GVG WNC CNXV PBBA FCVP XLF PBPX PBPXF NANY NAHF NEFR CEVPX";
        // the start of the name
        const string Starts = "FRK PHZ ENCR CRQB URVY GUBG";

        static string[] _any, _whole, _starts;

        static string Rot(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= 'A' && c <= 'Z' ? (char)('A' + (c - 'A' + 13) % 26) : c);
            return sb.ToString();
        }
        static string[] Words(string s) { var w = s.Split(' ', StringSplitOptions.RemoveEmptyEntries); for (int i = 0; i < w.Length; i++) w[i] = Rot(w[i]); return w; }
        static void Init() { if (_any != null) return; _whole = Words(Whole); _starts = Words(Starts); _any = Words(Anywhere); }

        /// <summary>Letters only: numbers read as the letters they look like (one is tried as I and as L), _ and the rest dropped.</summary>
        static string Letters(string n, char one)
        {
            var sb = new StringBuilder(n.Length);
            foreach (char ch in n.ToUpperInvariant())
            {
                char c = ch switch { '0' => 'O', '1' => one, '3' => 'E', '4' => 'A', '5' => 'S', '6' => 'G', '7' => 'T', '8' => 'B', '9' => 'G', '$' => 'S', '@' => 'A', '!' => 'I', _ => ch };
                if (c >= 'A' && c <= 'Z') sb.Append(c);
            }
            return sb.ToString();
        }

        static string Squash(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (sb.Length == 0 || sb[sb.Length - 1] != c) sb.Append(c);
            return sb.ToString();
        }

        static bool HasDouble(string w) { for (int i = 1; i < w.Length; i++) if (w[i] == w[i - 1]) return true; return false; }

        /// <summary>Is this callsign (with or without its #tag) not allowed?</summary>
        public static bool Bad(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            Init();
            int h = name.IndexOf('#'); if (h >= 0) name = name.Substring(0, h);
            string raw = name.ToUpperInvariant();
            if (raw.Contains("1488")) return true;
            // the whole name, with digits / _ on the end taken off
            string core = raw.Replace("_", "").TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            foreach (char one in new[] { 'I', 'L' })
            {
                string c = Letters(core, one);   // C0CK
                foreach (var w in _whole) if (core == w || c == w) return true;
                string n = Letters(raw, one), sq = Squash(n);
                foreach (var w in _starts) if (n.StartsWith(w, StringComparison.Ordinal) || sq.StartsWith(w, StringComparison.Ordinal)) return true;
                foreach (var w in _any)
                {
                    if (n.Contains(w, StringComparison.Ordinal)) return true;
                    if (!HasDouble(w) && sq.Contains(w, StringComparison.Ordinal)) return true;   // FUUUCK
                }
            }
            return false;
        }
    }
}
