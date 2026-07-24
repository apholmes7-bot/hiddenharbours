#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// A tiny, dependency-free JSON reader for the EDITOR import path — the rig bakes' anchor sidecars
    /// (<c>RodIsoAnchors.json</c> and friends) are dictionary-shaped (states/tiers/species as object
    /// keys), which <c>JsonUtility</c> cannot read, and the project deliberately carries no JSON package.
    /// Returns plain object graphs: <see cref="Dictionary{TKey,TValue}"/> (string→object),
    /// <see cref="List{T}"/> (object), <see cref="string"/>, <see cref="double"/>, <see cref="bool"/>,
    /// or null. Malformed input throws <see cref="FormatException"/> — import callers catch and degrade
    /// (the null-safe greybox rule). Editor-only by design: nothing at runtime parses JSON (the builder
    /// converts anchors to serialized world-metre tables once, at build time).
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("MiniJson: null input");
            int i = 0;
            object value = ParseValue(json, ref i);
            SkipWhitespace(json, ref i);
            if (i != json.Length) throw new FormatException($"MiniJson: trailing content at {i}");
            return value;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("MiniJson: unexpected end of input");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;   // '{'
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == '}') { i++; return d; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != '"') throw new FormatException($"MiniJson: expected key at {i}");
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != ':') throw new FormatException($"MiniJson: expected ':' at {i}");
                i++;
                d[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                char c = Peek(s, i);
                if (c == ',') { i++; continue; }
                if (c == '}') { i++; return d; }
                throw new FormatException($"MiniJson: expected ',' or '}}' at {i}");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var a = new List<object>();
            i++;   // '['
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == ']') { i++; return a; }
            while (true)
            {
                a.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                char c = Peek(s, i);
                if (c == ',') { i++; continue; }
                if (c == ']') { i++; return a; }
                throw new FormatException($"MiniJson: expected ',' or ']' at {i}");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++;   // '"'
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("MiniJson: unterminated string");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("MiniJson: dangling escape");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("MiniJson: short \\u escape");
                        sb.Append((char)ushort.Parse(s.Substring(i, 4), NumberStyles.HexNumber,
                                                     CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException($"MiniJson: bad escape '\\{e}'");
                }
            }
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            if (Peek(s, i) == '-') i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                                    || s[i] == '+' || s[i] == '-'))
                i++;
            string token = s.Substring(start, i - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new FormatException($"MiniJson: bad number '{token}' at {start}");
            return v;
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                throw new FormatException($"MiniJson: expected '{word}' at {i}");
            i += word.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static char Peek(string s, int i)
            => i < s.Length ? s[i] : throw new FormatException("MiniJson: unexpected end of input");

        // ---- typed access helpers (importers read the graph through these; missing = default) ------

        public static Dictionary<string, object> Dict(object node, string key)
            => node is Dictionary<string, object> d && d.TryGetValue(key, out object v)
                ? v as Dictionary<string, object> : null;

        public static List<object> List(object node, string key)
            => node is Dictionary<string, object> d && d.TryGetValue(key, out object v)
                ? v as List<object> : null;

        public static float Float(object node, string key, float fallback = 0f)
            => node is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v is double n
                ? (float)n : fallback;

        public static int Int(object node, string key, int fallback = 0)
            => node is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v is double n
                ? (int)System.Math.Round(n) : fallback;
    }
}
#endif
