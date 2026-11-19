using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OrbitalAnte.Verification
{
    /// <summary>
    /// A small JSON reader and writer with no Unity types, so review files can be checked inside Unity and
    /// outside it with the same code. Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers double, and the rest string, bool or null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("No JSON text.");
            int i = 0;
            object value = ReadValue(text, ref i);
            SkipSpace(text, ref i);
            if (i != text.Length) throw new FormatException("Unexpected text after the JSON value at character " + i + ".");
            return value;
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r' || s[i] == '﻿')) i++;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON ended early.");
            char c = s[i];
            if (c == '{')
            {
                var map = new Dictionary<string, object>();
                i++;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return map; }
                while (true)
                {
                    SkipSpace(s, ref i);
                    if (i >= s.Length || s[i] != '"') throw new FormatException("Expected a name in quotes at character " + i + ".");
                    string key = ReadString(s, ref i);
                    SkipSpace(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':' at character " + i + ".");
                    i++;
                    map[key] = ReadValue(s, ref i);
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return map; }
                    throw new FormatException("Expected ',' or '}' at character " + i + ".");
                }
            }
            if (c == '[')
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
                    if (i < s.Length && s[i] == ']') { i++; return list; }
                    throw new FormatException("Expected ',' or ']' at character " + i + ".");
                }
            }
            if (c == '"') return ReadString(s, ref i);
            if (Matches(s, i, "true")) { i += 4; return true; }
            if (Matches(s, i, "false")) { i += 5; return false; }
            if (Matches(s, i, "null")) { i += 4; return null; }
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            if (i == start) throw new FormatException("Unexpected character '" + c + "' at " + i + ".");
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                throw new FormatException("Bad number at character " + start + ".");
            return number;
        }

        static bool Matches(string s, int i, string word) => string.CompareOrdinal(s, i, word, 0, word.Length) == 0;

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (true)
            {
                if (i >= s.Length) throw new FormatException("A string was never closed.");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("A string was never closed.");
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("Bad \\u escape.");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
        }

        // ------------------------------------------------------------------ writing

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value, int depth)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string text: WriteString(sb, text); break;
                case bool flag: sb.Append(flag ? "true" : "false"); break;
                case IDictionary<string, object> map:
                    if (map.Count == 0) { sb.Append("{}"); break; }
                    sb.Append("{\n");
                    int n = 0;
                    foreach (KeyValuePair<string, object> pair in map)
                    {
                        Indent(sb, depth + 1);
                        WriteString(sb, pair.Key);
                        sb.Append(": ");
                        WriteValue(sb, pair.Value, depth + 1);
                        sb.Append(++n < map.Count ? ",\n" : "\n");
                    }
                    Indent(sb, depth);
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    var items = new List<object>();
                    foreach (object item in list) items.Add(item);
                    if (items.Count == 0) { sb.Append("[]"); break; }
                    sb.Append("[\n");
                    for (int k = 0; k < items.Count; k++)
                    {
                        Indent(sb, depth + 1);
                        WriteValue(sb, items[k], depth + 1);
                        sb.Append(k + 1 < items.Count ? ",\n" : "\n");
                    }
                    Indent(sb, depth);
                    sb.Append(']');
                    break;
                case IFormattable number: sb.Append(number.ToString(null, CultureInfo.InvariantCulture)); break;
                default: WriteString(sb, value.ToString()); break;
            }
        }

        static void Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 2);

        static void WriteString(StringBuilder sb, string text)
        {
            sb.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ reading helpers

        public static Dictionary<string, object> Obj(object value) => value as Dictionary<string, object>;
        public static List<object> List(object value) => value as List<object>;

        public static object Get(Dictionary<string, object> map, string key)
            => map != null && map.TryGetValue(key, out object value) ? value : null;

        public static string Str(Dictionary<string, object> map, string key) => Get(map, key) as string;

        public static double? Num(Dictionary<string, object> map, string key)
            => Get(map, key) is double d ? d : (double?)null;

        public static bool? Flag(Dictionary<string, object> map, string key)
            => Get(map, key) is bool b ? b : (bool?)null;

        public static List<object> Items(Dictionary<string, object> map, string key)
            => Get(map, key) as List<object> ?? new List<object>();
    }
}
