using UnityEngine;

namespace Nullbound.Art
{
    /// <summary>
    /// A tiny 5x7 block font for lettering painted INTO textures (crate stencils, panel numbers, floor markings),
    /// which costs nothing at runtime. Letters A-Z, digits and a few signs; lower case is drawn as upper case.
    /// </summary>
    public static partial class ProcTex
    {
        // Each glyph is 7 rows (top first) of 5 columns.
        static readonly string[] glyphSource =
        {
            "A", "01110 10001 10001 11111 10001 10001 10001", "B", "11110 10001 10001 11110 10001 10001 11110",
            "C", "01110 10001 10000 10000 10000 10001 01110", "D", "11100 10010 10001 10001 10001 10010 11100",
            "E", "11111 10000 10000 11110 10000 10000 11111", "F", "11111 10000 10000 11110 10000 10000 10000",
            "G", "01110 10001 10000 10111 10001 10001 01111", "H", "10001 10001 10001 11111 10001 10001 10001",
            "I", "01110 00100 00100 00100 00100 00100 01110", "J", "00111 00010 00010 00010 00010 10010 01100",
            "K", "10001 10010 10100 11000 10100 10010 10001", "L", "10000 10000 10000 10000 10000 10000 11111",
            "M", "10001 11011 10101 10101 10001 10001 10001", "N", "10001 10001 11001 10101 10011 10001 10001",
            "O", "01110 10001 10001 10001 10001 10001 01110", "P", "11110 10001 10001 11110 10000 10000 10000",
            "Q", "01110 10001 10001 10001 10101 10010 01101", "R", "11110 10001 10001 11110 10100 10010 10001",
            "S", "01111 10000 10000 01110 00001 00001 11110", "T", "11111 00100 00100 00100 00100 00100 00100",
            "U", "10001 10001 10001 10001 10001 10001 01110", "V", "10001 10001 10001 10001 10001 01010 00100",
            "W", "10001 10001 10001 10101 10101 10101 01010", "X", "10001 10001 01010 00100 01010 10001 10001",
            "Y", "10001 10001 10001 01010 00100 00100 00100", "Z", "11111 00001 00010 00100 01000 10000 11111",
            "0", "01110 10001 10011 10101 11001 10001 01110", "1", "00100 01100 00100 00100 00100 00100 01110",
            "2", "01110 10001 00001 00010 00100 01000 11111", "3", "11111 00010 00100 00010 00001 10001 01110",
            "4", "00010 00110 01010 10010 11111 00010 00010", "5", "11111 10000 11110 00001 00001 10001 01110",
            "6", "00110 01000 10000 11110 10001 10001 01110", "7", "11111 00001 00010 00100 01000 01000 01000",
            "8", "01110 10001 10001 01110 10001 10001 01110", "9", "01110 10001 10001 01111 00001 00010 01100",
            "-", "00000 00000 00000 11111 00000 00000 00000", ".", "00000 00000 00000 00000 00000 01100 01100",
            "/", "00001 00010 00010 00100 01000 01000 10000", ":", "00000 01100 01100 00000 01100 01100 00000",
            "#", "01010 01010 11111 01010 11111 01010 01010", "+", "00000 00100 00100 11111 00100 00100 00000",
            "!", "00100 00100 00100 00100 00100 00000 00100", ",", "00000 00000 00000 00000 01100 00100 01000",
            "^", "00100 01110 10101 00100 00100 00100 00100", ">", "01000 00100 00010 00001 00010 00100 01000",
            "<", "00010 00100 01000 10000 01000 00100 00010", "%", "11000 11001 00010 00100 01000 10011 00011",
            "(", "00010 00100 01000 01000 01000 00100 00010", ")", "01000 00100 00010 00010 00010 00100 01000",
            "'", "00100 00100 01000 00000 00000 00000 00000", "=", "00000 00000 11111 00000 11111 00000 00000",
        };

        // 35 bits per glyph: bit (row * 5 + col), row 0 = TOP.
        static readonly ulong[] glyphBits = BuildGlyphs();

        static ulong[] BuildGlyphs()
        {
            var bits = new ulong[128];
            for (int g = 0; g + 1 < glyphSource.Length; g += 2)
            {
                char ch = glyphSource[g][0];
                string rows = glyphSource[g + 1].Replace(" ", "");
                ulong b = 0;
                for (int i = 0; i < 35 && i < rows.Length; i++)
                    if (rows[i] == '1') b |= 1UL << i;
                bits[ch] = b;
            }
            return bits;
        }

        static bool GlyphPixel(char ch, int col, int rowFromBottom)
        {
            if (col < 0 || col > 4 || rowFromBottom < 0 || rowFromBottom > 6) return false;
            if (ch >= 'a' && ch <= 'z') ch = (char)(ch - 32);
            if (ch >= 128) return false;
            int row = 6 - rowFromBottom;
            return (glyphBits[ch] >> (row * 5 + col) & 1UL) != 0;
        }

        /// <summary>Width of a line of text drawn with Text() at this letter height (6 columns per letter, last gap trimmed).</summary>
        public static float TextWidth(string text, float height) => (text.Length * 6 - 1) * height / 7f;

        /// <summary>
        /// Coverage 0..1 of a line of block lettering at (x, y), in the caller's units (metres on a surface works well).
        /// The text's bottom-left corner is at (0, 0) and its letters are `height` tall. soft = edge softness (about one
        /// texel keeps it crisp but smooth); bleed &gt; 0 fattens the strokes (spray paint), &lt; 0 gives a dotted look.
        /// </summary>
        public static float Text(float x, float y, string text, float height, float soft, float bleed = 0f)
        {
            float cell = height / 7f;
            if (y < -cell || y > height + cell || x < -cell || x > TextWidth(text, height) + cell) return 0f;
            int col = Mathf.FloorToInt(x / cell), row = Mathf.FloorToInt(y / cell);
            float best = 9f;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int c = col + ox, r = row + oy;
                    if (c < 0) continue;
                    int letter = c / 6, inLetter = c % 6;
                    if (letter >= text.Length || !GlyphPixel(text[letter], inLetter, r)) continue;
                    float d = Box(x - (c + 0.5f) * cell, y - (r + 0.5f) * cell, cell * 0.5f + bleed, cell * 0.5f + bleed);
                    if (d < best) best = d;
                }
            return Fill(best, soft);
        }

        /// <summary>Text() centred on (0, 0) instead of starting there.</summary>
        public static float TextCentred(float x, float y, string text, float height, float soft, float bleed = 0f)
            => Text(x + TextWidth(text, height) * 0.5f, y + height * 0.5f, text, height, soft, bleed);
    }
}
