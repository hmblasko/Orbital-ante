using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// A small JSON reader with no Unity types, so the audio manifest is read by exactly the same code in the
    /// game and in the rule tests. Objects become dictionaries, arrays become lists, numbers become doubles.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int i = 0;
            object value = ReadValue(text, ref i);
            SkipSpace(text, ref i);
            if (i != text.Length) throw new FormatException("Unexpected text after JSON at character " + i);
            return value;
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON ended early");
            char c = s[i];
            switch (c)
            {
                case '{':
                {
                    var map = new Dictionary<string, object>();
                    i++;
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; return map; }
                    while (true)
                    {
                        SkipSpace(s, ref i);
                        string key = ReadString(s, ref i);
                        SkipSpace(s, ref i);
                        Expect(s, ref i, ':');
                        map[key] = ReadValue(s, ref i);
                        SkipSpace(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        Expect(s, ref i, '}');
                        return map;
                    }
                }
                case '[':
                {
                    var list = new List<object>();
                    i++;
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; return list; }
                    while (true)
                    {
                        list.Add(ReadValue(s, ref i));
                        SkipSpace(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        Expect(s, ref i, ']');
                        return list;
                    }
                }
                case '"':
                    return ReadString(s, ref i);
                case 't':
                    Word(s, ref i, "true");
                    return true;
                case 'f':
                    Word(s, ref i, "false");
                    return false;
                case 'n':
                    Word(s, ref i, "null");
                    return null;
                default:
                {
                    int start = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
                    if (i == start) throw new FormatException("Unexpected character '" + c + "' at " + i);
                    return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
                }
            }
        }

        static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c) throw new FormatException("Expected '" + c + "' at character " + i);
            i++;
        }

        static void Word(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException("Bad literal at character " + i);
            i += word.Length;
        }

        static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("Unterminated string");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("Unterminated escape");
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("Bad \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
        }

        // ------------------------------------------------------------------ typed reads with defaults

        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object> ?? new Dictionary<string, object>();

        public static List<object> List(Dictionary<string, object> o, string key)
        {
            return o != null && o.TryGetValue(key, out object v) && v is List<object> l ? l : new List<object>();
        }

        public static string Str(Dictionary<string, object> o, string key, string fallback = "")
        {
            return o != null && o.TryGetValue(key, out object v) && v is string s ? s : fallback;
        }

        public static double Num(Dictionary<string, object> o, string key, double fallback = 0)
        {
            return o != null && o.TryGetValue(key, out object v) && v is double d ? d : fallback;
        }

        public static bool Bool(Dictionary<string, object> o, string key, bool fallback = false)
        {
            return o != null && o.TryGetValue(key, out object v) && v is bool b ? b : fallback;
        }
    }
}
