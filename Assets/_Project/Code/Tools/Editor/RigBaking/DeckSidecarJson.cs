using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// A small, strict JSON reader — enough to read the art director's gameplay sidecars and nothing
    /// more. Editor-only, and deliberately hand-rolled: the sidecars carry nested arrays of arrays
    /// (<c>polygon3d</c> is <c>[[x,y,z], …]</c>), which is exactly the shape Unity's
    /// <c>JsonUtility</c> cannot express, and the project has no JSON package to reach for.
    ///
    /// <para>Values come back as <see cref="Dictionary{TKey,TValue}"/> (object),
    /// <see cref="List{T}"/> of object (array), <see cref="string"/>, <see cref="double"/>,
    /// <see cref="bool"/> or null. Malformed input throws with a character offset — a sidecar that
    /// will not parse is a loud failure, never a partial import.</para>
    /// </summary>
    public static class DeckSidecarJson
    {
        /// <summary>Parse a whole JSON document. Throws <see cref="FormatException"/> on anything
        /// malformed, including trailing content after the top-level value.</summary>
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON: null input.");
            int i = 0;
            object value = ParseValue(text, ref i);
            SkipWhitespace(text, ref i);
            if (i != text.Length) throw new FormatException($"JSON: trailing content at {i}.");
            return value;
        }

        // ---- typed readers the importer actually uses ------------------------------------------

        public static Dictionary<string, object> AsObject(object v) => v as Dictionary<string, object>;
        public static List<object> AsArray(object v) => v as List<object>;

        /// <summary>A member of an object, or null when the object (or the member) is absent — the
        /// sidecar contract's "absent section = the hull does not support that feature".</summary>
        public static object Member(object owner, string key)
        {
            var o = owner as Dictionary<string, object>;
            return (o != null && o.TryGetValue(key, out object v)) ? v : null;
        }

        public static string String(object v) => v as string;

        public static bool TryDouble(object v, out double d)
        {
            if (v is double dd) { d = dd; return true; }
            d = 0;
            return false;
        }

        /// <summary>A JSON number as a float, or <paramref name="fallback"/> when it is absent or not
        /// a number.</summary>
        public static float Float(object v, float fallback = 0f)
            => TryDouble(v, out double d) ? (float)d : fallback;

        // ---- the parser -------------------------------------------------------------------------

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON: unexpected end of input.");

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
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            i++;                                            // '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return map; }

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException($"JSON: expected a key at {i}.");
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException($"JSON: expected ':' at {i}.");
                i++;
                map[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON: unterminated object.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return map; }
                throw new FormatException($"JSON: expected ',' or '}}' at {i}.");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;                                            // '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }

            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON: unterminated array.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException($"JSON: expected ',' or ']' at {i}.");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++;                                            // opening quote
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
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
                        if (i + 4 > s.Length) throw new FormatException($"JSON: truncated \\u at {i}.");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: throw new FormatException($"JSON: bad escape '\\{e}' at {i}.");
                }
            }
            throw new FormatException("JSON: unterminated string.");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                                    ((s[i] == '-' || s[i] == '+') && (s[i - 1] == 'e' || s[i - 1] == 'E'))))
                i++;

            string span = s.Substring(start, i - start);
            if (!double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                throw new FormatException($"JSON: '{span}' is not a number (at {start}).");
            return d;
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
                throw new FormatException($"JSON: expected '{literal}' at {i}.");
            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }
    }
}
