using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Flopzone
{
    /// <summary>
    /// Tiny JSON reader/writer for the server protocol. Objects become
    /// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
    /// (Unity's JsonUtility can't do free-form objects, and this keeps the
    /// project free of package dependencies.)
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = 0;
            try
            {
                var v = ReadValue(text, ref i);
                return v;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            return Parse(text) as Dictionary<string, object>;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("eof");
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (c == 't' && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return ReadNumber(s, ref i);
        }

        static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (s[i] != ':') throw new FormatException("expected :");
                i++;
                d[key] = ReadValue(s, ref i);
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("expected , or }");
            }
        }

        static List<object> ReadArray(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ReadValue(s, ref i));
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException("expected , or ]");
            }
        }

        static string ReadString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("expected string");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("unterminated string");
        }

        static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder(128);
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string str: WriteString(sb, str); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case float f: WriteNumber(sb, f); return;
                case double d: WriteNumber(sb, d); return;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case float[] fa:
                    sb.Append('[');
                    for (int k = 0; k < fa.Length; k++) { if (k > 0) sb.Append(','); WriteNumber(sb, fa[k]); }
                    sb.Append(']');
                    return;
                case IDictionary dict:
                    sb.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry kv in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key.ToString());
                        sb.Append(':');
                        WriteValue(sb, kv.Value);
                    }
                    sb.Append('}');
                    return;
                case IEnumerable list:
                    sb.Append('[');
                    bool f1 = true;
                    foreach (var item in list) { if (!f1) sb.Append(','); f1 = false; WriteValue(sb, item); }
                    sb.Append(']');
                    return;
                default:
                    WriteString(sb, v.ToString());
                    return;
            }
        }

        static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append('0'); return; }
            sb.Append(Math.Round(d, 3).ToString("0.###", CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Convenience readers for parsed messages.</summary>
    public static class JsonExt
    {
        public static string Str(this Dictionary<string, object> d, string key, string def = "")
        {
            if (d != null && d.TryGetValue(key, out var v) && v != null) return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
            return def;
        }

        public static double Num(this Dictionary<string, object> d, string key, double def = 0)
        {
            if (d != null && d.TryGetValue(key, out var v) && v != null)
            {
                if (v is double dd) return dd;
                if (v is bool b) return b ? 1 : 0;
                if (v is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p)) return p;
                try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { }
            }
            return def;
        }

        public static int Int(this Dictionary<string, object> d, string key, int def = 0)
        {
            return (int)Math.Round(d.Num(key, def));
        }

        public static bool Bool(this Dictionary<string, object> d, string key, bool def = false)
        {
            if (d != null && d.TryGetValue(key, out var v) && v != null)
            {
                if (v is bool b) return b;
                if (v is double n) return n != 0;
            }
            return def;
        }

        public static List<object> Arr(this Dictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out var v)) return v as List<object>;
            return null;
        }

        public static Dictionary<string, object> Obj(this Dictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out var v)) return v as Dictionary<string, object>;
            return null;
        }

        public static bool Has(this Dictionary<string, object> d, string key)
        {
            return d != null && d.TryGetValue(key, out var v) && v != null;
        }

        /// <summary>A number array, whether it came off the wire (List) or from LocalServer (float[]).</summary>
        public static float[] FloatArr(this Dictionary<string, object> d, string key)
        {
            if (d == null || !d.TryGetValue(key, out var v) || v == null) return null;
            if (v is float[] fa) return fa;
            return (v as List<object>).Floats();
        }

        public static float[] Floats(this List<object> list)
        {
            if (list == null) return null;
            var a = new float[list.Count];
            for (int i = 0; i < a.Length; i++) a[i] = list[i] is double d ? (float)d : 0f;
            return a;
        }
    }

    /// <summary>Build a message: Msg.Of("t", "hit", "target", id, "dmg", 20).</summary>
    public static class Msg
    {
        public static Dictionary<string, object> Of(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }
    }
}
