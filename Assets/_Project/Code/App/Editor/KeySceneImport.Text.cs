#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The import's two texts: the package's JSON, read in its own order with every number kept as CD typed
    /// it, and the asset's YAML, written as Unity's own writer writes it (so an asset the editor saves again
    /// comes back byte for byte) and read back for the diff against what is there.
    /// </summary>
    public static partial class KeySceneImport
    {
        // =====================================================================================
        //  the package's JSON
        // =====================================================================================

        /// <summary>A JSON value as the package wrote it. An object keeps its keys in order and a number
        /// keeps its own text, so an option reaches the asset as CD typed it (<c>0.35</c>, never <c>0.349999</c>).</summary>
        public sealed class Json
        {
            public enum Of { Object, Array, String, Number, Bool, Null }

            public readonly Of Kind;
            /// <summary>A string's value, a number's text, or <c>true</c>, <c>false</c>, <c>null</c>.</summary>
            public readonly string Text;
            public readonly List<KeyValuePair<string, Json>> Fields;
            public readonly List<Json> Items;

            Json(Of kind, string text, List<KeyValuePair<string, Json>> fields, List<Json> items)
            {
                Kind = kind; Text = text; Fields = fields; Items = items;
            }

            public Json Get(string key)
            {
                if (Fields == null) return null;
                foreach (KeyValuePair<string, Json> f in Fields) if (f.Key == key) return f.Value;
                return null;
            }

            public bool Has(string key) => Get(key) != null;

            public string Str(string what) =>
                Kind == Of.String ? Text : throw new Refusal($"{what} is not a string ({Compact()}).");

            public float Float(string what)
            {
                if (Kind != Of.Number) throw new Refusal($"{what} is not a number ({Compact()}).");
                float v = float.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (float.IsInfinity(v)) throw new Refusal($"{what} ({Text}) does not fit a float.");
                return v;
            }

            public int Int(string what)
            {
                if (Kind != Of.Number || !int.TryParse(Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v))
                    throw new Refusal($"{what} is not a whole number ({Compact()}).");
                return v;
            }

            public bool IsTrue => Kind == Of.Bool && Text == "true";

            /// <summary>The value as compact JSON: no spaces, numbers as CD typed them, anything past ASCII as
            /// <c>\uXXXX</c>, so the asset's text stays ASCII.</summary>
            public string Compact()
            {
                var sb = new StringBuilder();
                WriteCompact(sb);
                return sb.ToString();
            }

            void WriteCompact(StringBuilder sb)
            {
                switch (Kind)
                {
                    case Of.String: Quote(sb, Text); break;
                    case Of.Object:
                        sb.Append('{');
                        for (int i = 0; i < Fields.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            Quote(sb, Fields[i].Key);
                            sb.Append(':');
                            Fields[i].Value.WriteCompact(sb);
                        }
                        sb.Append('}');
                        break;
                    case Of.Array:
                        sb.Append('[');
                        for (int i = 0; i < Items.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            Items[i].WriteCompact(sb);
                        }
                        sb.Append(']');
                        break;
                    default: sb.Append(Text); break;
                }
            }

            static void Quote(StringBuilder sb, string s)
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
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < 0x20 || c > 0x7E) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else sb.Append(c);
                            break;
                    }
                }
                sb.Append('"');
            }

            public static Json Parse(string text, string what)
            {
                int i = 0;
                Json v = Value(text, ref i, what);
                Skip(text, ref i);
                if (i != text.Length) throw new Refusal($"{what}: text after the JSON's end (offset {i}).");
                return v;
            }

            static void Skip(string s, ref int i)
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
            }

            static Json Value(string s, ref int i, string what)
            {
                Skip(s, ref i);
                if (i >= s.Length) throw new Refusal($"{what}: the JSON ends early.");
                char c = s[i];
                if (c == '{')
                {
                    i++;
                    var fields = new List<KeyValuePair<string, Json>>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; return new Json(Of.Object, null, fields, null); }
                    while (true)
                    {
                        Skip(s, ref i);
                        if (i >= s.Length || s[i] != '"') throw new Refusal($"{what}: a key expected at offset {i}.");
                        string key = String(s, ref i, what);
                        if (!seen.Add(key)) throw new Refusal($"{what}: the key '{key}' twice in one object (offset {i}).");
                        Skip(s, ref i);
                        if (i >= s.Length || s[i] != ':') throw new Refusal($"{what}: ':' expected at offset {i}.");
                        i++;
                        fields.Add(new KeyValuePair<string, Json>(key, Value(s, ref i, what)));
                        Skip(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        if (i < s.Length && s[i] == '}') { i++; return new Json(Of.Object, null, fields, null); }
                        throw new Refusal($"{what}: ',' or '}}' expected at offset {i}.");
                    }
                }
                if (c == '[')
                {
                    i++;
                    var items = new List<Json>();
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; return new Json(Of.Array, null, null, items); }
                    while (true)
                    {
                        items.Add(Value(s, ref i, what));
                        Skip(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        if (i < s.Length && s[i] == ']') { i++; return new Json(Of.Array, null, null, items); }
                        throw new Refusal($"{what}: ',' or ']' expected at offset {i}.");
                    }
                }
                if (c == '"') return new Json(Of.String, String(s, ref i, what), null, null);
                if (Word(s, ref i, "true")) return new Json(Of.Bool, "true", null, null);
                if (Word(s, ref i, "false")) return new Json(Of.Bool, "false", null, null);
                if (Word(s, ref i, "null")) return new Json(Of.Null, "null", null, null);
                Match m = NumberRx.Match(s, i);
                if (m.Success && m.Index == i && m.Length > 0)
                {
                    i += m.Length;
                    return new Json(Of.Number, m.Value, null, null);
                }
                throw new Refusal($"{what}: no JSON value at offset {i}.");
            }

            static readonly Regex NumberRx = new Regex(@"-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);

            static bool Word(string s, ref int i, string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
                i += word.Length;
                return true;
            }

            static string String(string s, ref int i, string what)
            {
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (i >= s.Length) throw new Refusal($"{what}: a string runs off the end.");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw new Refusal($"{what}: a raw control character in a string (offset {i - 1}).");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) throw new Refusal($"{what}: an escape runs off the end.");
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
                            if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int u))
                                throw new Refusal($"{what}: a bad \\u escape at offset {i - 2}.");
                            sb.Append((char)u);
                            i += 4;
                            break;
                        default: throw new Refusal($"{what}: an unknown escape '\\{e}' at offset {i - 2}.");
                    }
                }
            }
        }

        // =====================================================================================
        //  the asset, as Unity's YAML writer writes it
        // =====================================================================================

        /// <summary>Unity's writer (libyaml's emitter) folds a long scalar at the first single space once the
        /// line is past this column. Measured on committed assets: <c>NotPlacedBecause</c> in the interiors breaks
        /// at column 81, 82 and 83, never before 81.</summary>
        public const int FoldColumn = 80;

        /// <summary>Writes one asset's text, field by field: a string is plain, single-quoted or double-quoted
        /// by libyaml's rules, folded past <see cref="FoldColumn"/> with its continuation two spaces in from its
        /// key, everything past ASCII escaped (<c>\u2019</c>, <c>\xB7</c>). Lines end in LF, as Unity writes them.</summary>
        public sealed class YamlWriter
        {
            readonly StringBuilder _sb = new StringBuilder();

            public void Line(string text) => _sb.Append(text).Append('\n');

            /// <summary><c>key: value</c>, the value a string. <paramref name="item"/> writes it as the first field
            /// of a sequence item, its dash two to the left of <paramref name="indent"/>.</summary>
            public void Str(int indent, string key, string value, bool item = false)
            {
                int column = Key(indent, key, item);
                Scalar(_sb, ref column, indent + 2, value, key);
                _sb.Append('\n');
            }

            /// <summary><c>key: text</c>, the text as it stands: a number, a 0/1 bool, a flow mapping.</summary>
            public void Raw(int indent, string key, string text, bool item = false)
            {
                Key(indent, key, item);
                _sb.Append(' ').Append(text).Append('\n');
            }

            /// <summary>A list of strings: <c>key: []</c>, or the key and one <c>- item</c> line each, indentless.</summary>
            public void List(int indent, string key, IReadOnlyList<string> items, bool item = false)
            {
                Key(indent, key, item);
                if (items.Count == 0) { _sb.Append(" []\n"); return; }
                _sb.Append('\n');
                foreach (string s in items)
                {
                    _sb.Append(' ', indent).Append('-');
                    int column = indent + 1;
                    Scalar(_sb, ref column, indent + 2, s, key);
                    _sb.Append('\n');
                }
            }

            /// <summary>The key of a list of mappings: <c>key: []</c> when it is empty, else the key alone and
            /// the caller writes each item's fields at <paramref name="indent"/> + 2, the first with its dash.</summary>
            public bool Items(int indent, string key, int count, bool item = false)
            {
                Key(indent, key, item);
                _sb.Append(count == 0 ? " []\n" : "\n");
                return count > 0;
            }

            int Key(int indent, string key, bool item)
            {
                if (item) _sb.Append(' ', indent - 2).Append("- ");
                else _sb.Append(' ', indent);
                _sb.Append(key).Append(':');
                return indent + key.Length + 1;
            }

            public override string ToString() => _sb.ToString();
        }

        enum Style { Plain, Single, Double }

        static bool IsBreak(char c) => c == '\n' || c == '\r' || c == '\u0085' || c == '\u2028' || c == '\u2029';
        static bool IsBlankZ(char c) => c == ' ' || c == '\t' || IsBreak(c);

        /// <summary>libyaml's <c>yaml_emitter_analyze_scalar</c> and <c>select_scalar_style</c>, for a mapping
        /// value or a sequence item in block context, as Unity calls it: every string plain-implicit, so
        /// <c>14</c> and <c>true</c> stay plain, and nothing past ASCII left raw.</summary>
        static Style StyleOf(string s)
        {
            if (s.Length == 0) return Style.Plain;
            bool blockIndicators = s.StartsWith("---", StringComparison.Ordinal) || s.StartsWith("...", StringComparison.Ordinal);
            bool specials = false, lineBreaks = false, leadingSpace = false, leadingBreak = false;
            bool trailingSpace = false, trailingBreak = false, breakSpace = false, spaceBreak = false;
            bool previousSpace = false, previousBreak = false, precededByWhitespace = true;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool followedByWhitespace = i + 1 >= s.Length || IsBlankZ(s[i + 1]);
                if (i == 0)
                {
                    if ("#,[]{}&*!|>'\"%@`".IndexOf(c) >= 0) blockIndicators = true;
                    if ((c == '?' || c == ':') && followedByWhitespace) blockIndicators = true;
                    if (c == '-' && followedByWhitespace) blockIndicators = true;
                }
                else
                {
                    if (c == ':' && followedByWhitespace) blockIndicators = true;
                    if (c == '#' && precededByWhitespace) blockIndicators = true;
                }
                if (!(c == '\n' || (c >= 0x20 && c <= 0x7E))) specials = true;
                if (IsBreak(c)) lineBreaks = true;
                if (c == ' ')
                {
                    if (i == 0) leadingSpace = true;
                    if (i == s.Length - 1) trailingSpace = true;
                    if (previousBreak) breakSpace = true;
                    previousSpace = true; previousBreak = false;
                }
                else if (IsBreak(c))
                {
                    if (i == 0) leadingBreak = true;
                    if (i == s.Length - 1) trailingBreak = true;
                    if (previousSpace) spaceBreak = true;
                    previousSpace = false; previousBreak = true;
                }
                else { previousSpace = false; previousBreak = false; }
                precededByWhitespace = IsBlankZ(c);
            }
            bool plain = !(leadingSpace || leadingBreak || trailingSpace || trailingBreak || breakSpace || spaceBreak
                           || specials || lineBreaks || blockIndicators);
            if (plain) return Style.Plain;
            return breakSpace || spaceBreak || specials ? Style.Double : Style.Single;
        }

        /// <summary>One string scalar, from just after its <c>key:</c> (or its dash) at <paramref name="column"/>,
        /// folded onto lines at <paramref name="indent"/>. A line break in a string is a stop: nothing CD's
        /// pieces carry has one, and the asset never needs the block styles.</summary>
        static void Scalar(StringBuilder sb, ref int column, int indent, string s, string what)
        {
            foreach (char c in s)
                if (IsBreak(c)) throw new Refusal($"a line break in {what} ('{s}'); the importer writes none.");

            Style style = StyleOf(s);
            if (style == Style.Plain)
            {
                // Unity writes the space even before an empty value: "State: ".
                sb.Append(' '); column++;
                bool spaces = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == ' ')
                    {
                        if (!spaces && column > FoldColumn && !(i + 1 < s.Length && s[i + 1] == ' ')) Fold(sb, ref column, indent);
                        else { sb.Append(' '); column++; }
                        spaces = true;
                    }
                    else { sb.Append(c); column++; spaces = false; }
                }
                return;
            }

            if (style == Style.Single)
            {
                sb.Append(" '"); column += 2;
                bool spaces = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == ' ')
                    {
                        if (!spaces && column > FoldColumn && i != 0 && i != s.Length - 1 && s[i + 1] != ' ') Fold(sb, ref column, indent);
                        else { sb.Append(' '); column++; }
                        spaces = true;
                    }
                    else
                    {
                        if (c == '\'') { sb.Append('\''); column++; }
                        sb.Append(c); column++;
                        spaces = false;
                    }
                }
                sb.Append('\''); column++;
                return;
            }

            sb.Append(" \""); column += 2;
            bool sp = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool printable = c >= 0x20 && c <= 0x7E;
                if (!printable || c == '"' || c == '\\')
                {
                    int cp = c;
                    if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) cp = char.ConvertToUtf32(c, s[++i]);
                    string e;
                    switch (cp)
                    {
                        case 0x00: e = "0"; break;
                        case 0x07: e = "a"; break;
                        case 0x08: e = "b"; break;
                        case 0x09: e = "t"; break;
                        case 0x0B: e = "v"; break;
                        case 0x0C: e = "f"; break;
                        case 0x1B: e = "e"; break;
                        case 0x22: e = "\""; break;
                        case 0x5C: e = "\\"; break;
                        case 0xA0: e = "_"; break;
                        default:
                            e = cp <= 0xFF ? "x" + cp.ToString("X2", CultureInfo.InvariantCulture)
                              : cp <= 0xFFFF ? "u" + cp.ToString("X4", CultureInfo.InvariantCulture)
                              : "U" + cp.ToString("X8", CultureInfo.InvariantCulture);
                            break;
                    }
                    sb.Append('\\').Append(e); column += 1 + e.Length;
                    sp = false;
                }
                else if (c == ' ')
                {
                    if (!sp && column > FoldColumn && i != 0 && i != s.Length - 1)
                    {
                        Fold(sb, ref column, indent);
                        if (s[i + 1] == ' ') { sb.Append('\\'); column++; }
                    }
                    else { sb.Append(' '); column++; }
                    sp = true;
                }
                else { sb.Append(c); column++; sp = false; }
            }
            sb.Append('"'); column++;
        }

        static void Fold(StringBuilder sb, ref int column, int indent)
        {
            sb.Append('\n').Append(' ', indent);
            column = indent;
        }

        /// <summary>One string field as the asset writes it, for the tests: <c>key:</c> at
        /// <paramref name="indent"/>, the value, any folded lines, and the line's end.</summary>
        public static string ScalarText(string key, int indent, string value)
        {
            var w = new YamlWriter();
            w.Str(indent, key, value);
            return w.ToString();
        }

        // =====================================================================================
        //  the asset read back: the subset the importer writes, and #913's two by hand
        // =====================================================================================

        /// <summary>One value read back from an asset: a string (a scalar decoded, or a flow mapping's text as
        /// it stands), a list, or a mapping (a list's item).</summary>
        public sealed class YamlNode
        {
            public string Scalar;
            public List<YamlNode> Items;
            public List<KeyValuePair<string, YamlNode>> Map;

            public YamlNode Get(string key)
            {
                if (Map == null) return null;
                foreach (KeyValuePair<string, YamlNode> f in Map) if (f.Key == key) return f.Value;
                return null;
            }

            /// <summary>The value as one comparable text: a scalar as itself, a list's items in brackets between
            /// bars, a mapping's fields in braces as <c>key=value</c>.</summary>
            public string Canon()
            {
                if (Scalar != null) return Scalar;
                var sb = new StringBuilder();
                if (Items != null)
                {
                    sb.Append('[');
                    for (int i = 0; i < Items.Count; i++) { if (i > 0) sb.Append(" | "); sb.Append(Items[i].Canon()); }
                    return sb.Append(']').ToString();
                }
                sb.Append('{');
                for (int i = 0; i < Map.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(Map[i].Key).Append('=').Append(Map[i].Value.Canon()); }
                return sb.Append('}').ToString();
            }
        }

        static readonly Regex KeyLineRx = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*):(?: (.*))?$", RegexOptions.CultureInvariant);

        /// <summary>The asset's top mapping (the MonoBehaviour's fields), read from its text.</summary>
        public static List<KeyValuePair<string, YamlNode>> ReadAsset(string text, string what)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int i = 0;
            while (i < lines.Length && lines[i] != "MonoBehaviour:") i++;
            if (i == lines.Length) throw new Refusal($"{what}: no MonoBehaviour in it.");
            i++;
            List<KeyValuePair<string, YamlNode>> map = ReadMapping(lines, ref i, 2, what);
            while (i < lines.Length && lines[i].Length == 0) i++;
            if (i != lines.Length) throw new Refusal($"{what}: line {i + 1} is not the asset's ('{lines[i]}').");
            return map;
        }

        static int Indent(string line)
        {
            int n = 0;
            while (n < line.Length && line[n] == ' ') n++;
            return n;
        }

        static List<KeyValuePair<string, YamlNode>> ReadMapping(string[] lines, ref int i, int indent, string what)
        {
            var map = new List<KeyValuePair<string, YamlNode>>();
            while (i < lines.Length && lines[i].Length > 0)
            {
                string line = lines[i];
                int at = Indent(line);
                if (at < indent) break;
                if (at > indent) throw new Refusal($"{what}: line {i + 1} is indented where a key was expected.");
                if (line.Substring(at).StartsWith("- ", StringComparison.Ordinal)) break;
                Match m = KeyLineRx.Match(line.Substring(at));
                if (!m.Success) throw new Refusal($"{what}: line {i + 1} is not a key ('{line}').");
                string key = m.Groups[1].Value;
                i++;
                YamlNode node;
                if (!m.Groups[2].Success)
                {
                    node = i < lines.Length && Indent(lines[i]) == indent && lines[i].Substring(indent).StartsWith("- ", StringComparison.Ordinal)
                        ? ReadSequence(lines, ref i, indent, what)
                        : new YamlNode { Scalar = "" };
                }
                else
                {
                    string first = m.Groups[2].Value;
                    var more = new List<string>();
                    while (i < lines.Length && lines[i].Length > 0 && Indent(lines[i]) > indent) more.Add(lines[i++].TrimStart(' '));
                    node = first == "[]" && more.Count == 0 ? new YamlNode { Items = new List<YamlNode>() } : new YamlNode { Scalar = Decode(first, more, what) };
                }
                foreach (KeyValuePair<string, YamlNode> f in map)
                    if (f.Key == key) throw new Refusal($"{what}: the key {key} twice in one mapping.");
                map.Add(new KeyValuePair<string, YamlNode>(key, node));
            }
            return map;
        }

        static YamlNode ReadSequence(string[] lines, ref int i, int indent, string what)
        {
            var items = new List<YamlNode>();
            while (i < lines.Length && lines[i].Length > 0 && Indent(lines[i]) == indent
                   && lines[i].Substring(indent).StartsWith("- ", StringComparison.Ordinal))
            {
                string after = lines[i].Substring(indent + 2);
                if (KeyLineRx.IsMatch(after))
                {
                    lines[i] = new string(' ', indent + 2) + after;
                    items.Add(new YamlNode { Map = ReadMapping(lines, ref i, indent + 2, what) });
                    continue;
                }
                i++;
                var more = new List<string>();
                while (i < lines.Length && lines[i].Length > 0 && Indent(lines[i]) > indent) more.Add(lines[i++].TrimStart(' '));
                items.Add(new YamlNode { Scalar = Decode(after, more, what) });
            }
            return new YamlNode { Items = items };
        }

        /// <summary>A scalar back to its string: folded lines joined by one space (the writer folds only at a
        /// single space), then the quoting undone.</summary>
        static string Decode(string first, List<string> more, string what)
        {
            string joined = more.Count == 0 ? first : first + " " + string.Join(" ", more);
            if (joined.StartsWith("'", StringComparison.Ordinal))
            {
                if (joined.Length < 2 || !joined.EndsWith("'", StringComparison.Ordinal)) throw new Refusal($"{what}: an unclosed quote: {joined}");
                return joined.Substring(1, joined.Length - 2).Replace("''", "'");
            }
            if (!joined.StartsWith("\"", StringComparison.Ordinal)) return joined;
            if (joined.Length < 2 || !joined.EndsWith("\"", StringComparison.Ordinal)) throw new Refusal($"{what}: an unclosed quote: {joined}");
            string body = joined.Substring(1, joined.Length - 2);
            var sb = new StringBuilder();
            for (int k = 0; k < body.Length; k++)
            {
                char c = body[k];
                if (c != '\\') { sb.Append(c); continue; }
                if (++k >= body.Length) throw new Refusal($"{what}: an escape at the end: {joined}");
                char e = body[k];
                int hex = 0;
                switch (e)
                {
                    case '0': sb.Append('\0'); break;
                    case 'a': sb.Append('\a'); break;
                    case 'b': sb.Append('\b'); break;
                    case 't': case '\t': sb.Append('\t'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'v': sb.Append('\v'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'e': sb.Append('\u001B'); break;
                    case ' ': sb.Append(' '); break;
                    case '"': sb.Append('"'); break;
                    case '/': sb.Append('/'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'N': sb.Append('\u0085'); break;
                    case '_': sb.Append('\u00A0'); break;
                    case 'L': sb.Append('\u2028'); break;
                    case 'P': sb.Append('\u2029'); break;
                    case 'x': hex = 2; break;
                    case 'u': hex = 4; break;
                    case 'U': hex = 8; break;
                    default: throw new Refusal($"{what}: an unknown escape '\\{e}': {joined}");
                }
                if (hex == 0) continue;
                if (k + hex >= body.Length || !int.TryParse(body.Substring(k + 1, hex), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int cp))
                    throw new Refusal($"{what}: a bad escape: {joined}");
                sb.Append(char.ConvertFromUtf32(cp));
                k += hex;
            }
            return sb.ToString();
        }
    }
}
#endif
