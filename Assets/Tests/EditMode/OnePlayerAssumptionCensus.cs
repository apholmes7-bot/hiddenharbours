using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tests.EditMode
{
    // A bounded lexical census, not a C# compiler. Source offsets are kept until a finding is emitted.
    internal static class OnePlayerAssumptionCensus
    {
        internal const string Root = "Assets/_Project/Code/";
        internal const string Baseline = "docs/design/one-player-assumptions.md";
        internal static readonly HashSet<string> PersonalTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "ControlSwitcher", "CarryHands", "ClamBucket", "ICarrier", "IHandsCarrier", "ICatchHands",
            "IShoppingBagHands", "IPlayerAppearance", "INeedsService", "IBodyService", "ITraitService",
            "ICleanlinessService", "IPlayerStrength", "ICarriedLoad", "FoodEater", "CatchRecordLog",
            "GiftLedger", "IWorldWhereabouts", "IActiveBoatService", "IHelmControl", "IHelmInstruments",
            "HelmSlot", "PlayerStorey", "RestQuorum", "RodSkill", "RodParts", "HandSlots", "ShellPause"
        };
        internal static readonly HashSet<string> PersonalServices = new HashSet<string>(StringComparer.Ordinal)
        {
            "Appearance", "Traits", "Needs", "Body", "PlayerStrength", "CarriedLoad", "Cleanliness", "Food",
            "PlayerTransform", "Hands", "CatchHands", "BagHands", "PlayerHold", "CurrentRegionId",
            "PendingArrivalKey", "ConsumePendingArrivalKey", "CurrentRegionBounds", "Whereabouts",
            "ActiveBoat", "ActiveBoatFuel", "Helm", "HelmControl", "HelmInstruments"
        };

        internal static bool RuntimePath(string path) => path.EndsWith(".cs", StringComparison.Ordinal) &&
            !path.StartsWith("Tools/", StringComparison.Ordinal) &&
            !("/" + path).Contains("/Editor/");

        internal static string Simple(string name)
        {
            int dot = Math.Max(name.LastIndexOf('.'), name.LastIndexOf('+'));
            return name.Substring(dot + 1);
        }

        internal static bool Personal(string name)
        {
            name = Simple(name);
            return PersonalTypes.Contains(name) || name.StartsWith("Player", StringComparison.Ordinal) ||
                name.EndsWith("Hands", StringComparison.Ordinal) || name.EndsWith("Skill", StringComparison.Ordinal);
        }

        internal static bool StaticState(string owner, string type, string member, bool stored)
        {
            owner = Simple(owner);
            type = Simple(type);
            // All stored locator slots are audited, including new services with unfamiliar names.
            if (owner == "GameServices") return stored || Personal(type);
            if (owner == "RestQuorum" && member == "Players") return true;
            if (Personal(type) || (Personal(owner) && stored)) return true;
            string singleton = member.TrimStart('_');
            if (singleton.StartsWith("s_", StringComparison.Ordinal)) singleton = singleton.Substring(2);
            if (type == owner && (singleton.Equals("instance", StringComparison.OrdinalIgnoreCase) ||
                singleton.Equals("shared", StringComparison.OrdinalIgnoreCase) ||
                singleton.Equals("current", StringComparison.OrdinalIgnoreCase))) return true;
            return member.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0 && stored &&
                (type == "Transform" || type == "GameObject" || type == "Object" || type == "object");
        }

        internal sealed class Member
        {
            internal string Path, Owner, FullOwner, Name, Type;
            internal bool Static, Stored, Method, Constant, Parameterless;
            internal int Start, End, NameIndex;
            internal string Key => Path + "|" + Owner + "." + Name;
        }

        internal static void Add(Dictionary<string, int> hits, string key)
        {
            hits.TryGetValue(key, out int count);
            hits[key] = count + 1;
        }

        internal struct Token { internal int Start, Length; }
        internal sealed class Buffer
        {
            internal readonly List<Token> Tokens = new List<Token>();
            internal readonly List<int> Close = new List<int>();
            internal readonly Stack<int> Opens = new Stack<int>();
        }

        internal static List<Member> Scan(string path, string source, Dictionary<string, int> hits, Buffer buffer = null)
        {
            if (!MayContainFinding(source)) return new List<Member>();
            var unit = new Unit(source, buffer ?? new Buffer());
            var members = unit.Members(path);
            foreach (Member member in members)
            {
                if (member.Static && !member.Method && !member.Constant &&
                    StaticState(member.Owner, member.Type, member.Name, member.Stored))
                    Add(hits, "static|" + member.Key);
                if (member.Static && member.Method && member.Parameterless && Personal(member.Type))
                    Add(hits, "accessor|" + member.Key);
                if (member.Constant && ((Simple(member.Owner) == "RestQuorum" && member.Name == "LocalSleeper") ||
                    (Simple(member.Owner) == "RelationshipGraph" && member.Name == "PlayerId")))
                    Add(hits, "identity|" + member.Key);
                unit.Lookups(member, hits);
            }
            return members;
        }

        private static bool MayContainFinding(string source)
        {
            if (source.Contains("GameServices") || source.Contains("Find") || source.Contains("RestQuorum") ||
                source.Contains("RelationshipGraph")) return true;
            if (!source.Contains("static")) return false;
            if (source.Contains("Player") || source.Contains("Hands") || source.Contains("Skill") ||
                source.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.IndexOf("shared", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.IndexOf("current", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (string type in PersonalTypes) if (source.Contains(type)) return true;
            return false;
        }

        // This tokenizer retains comments/literals as no code, including disabled branches as code.
        // It intentionally does not interpret preprocessor symbols: every build variant is audited.
        private sealed class Unit
        {
            private readonly string _source;
            private readonly List<Token> _tokens;
            private readonly List<int> _close;
            private readonly Dictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            private bool _staticServices;

            internal Unit(string source, Buffer buffer)
            {
                _source = source;
                _tokens = buffer.Tokens; _tokens.Clear();
                _close = buffer.Close; _close.Clear();
                Tokenize(0, source.Length);
                var stack = buffer.Opens; stack.Clear();
                for (int i = 0; i < _tokens.Count; i++)
                {
                    _close.Add(i);
                    if (Is(i, "(") || Is(i, "[") || Is(i, "{")) stack.Push(i);
                    else if ((Is(i, ")") || Is(i, "]") || Is(i, "}")) && stack.Count > 0)
                        _close[stack.Pop()] = i;
                }
                for (int i = 0; i + 3 < _tokens.Count; i++)
                {
                    if (Is(i, "using") && Is(i + 2, "="))
                    {
                        int end = i + 3;
                        while (end < _tokens.Count && !Is(end, ";")) end++;
                        _aliases[Text(i + 1)] = Text(end - 1);
                    }
                    if (Is(i, "using") && Is(i + 1, "static"))
                    {
                        int end = i + 2;
                        while (end < _tokens.Count && !Is(end, ";")) end++;
                        _staticServices |= Is(end - 1, "GameServices");
                    }
                }
            }

            private bool Is(int i, string text) => i >= 0 && i < _tokens.Count &&
                _tokens[i].Length == text.Length &&
                string.CompareOrdinal(_source, _tokens[i].Start, text, 0, text.Length) == 0;
            private string Text(int i) => i >= 0 && i < _tokens.Count
                ? _source.Substring(_tokens[i].Start, _tokens[i].Length) : "";
            private int Close(int i) => i >= 0 && i < _close.Count ? _close[i] : i;
            private string Resolve(string name) => _aliases.TryGetValue(name, out string canonical) ? canonical : name;
            private void AddToken(int start, int end) => _tokens.Add(new Token { Start = start, Length = end - start });

            private void Tokenize(int start, int end)
            {
                for (int i = start; i < end;)
                {
                    char c = _source[i];
                    if (char.IsWhiteSpace(c)) { i++; continue; }
                    if (c == '/' && i + 1 < end && _source[i + 1] == '/')
                    { while (i < end && _source[i] != '\n') i++; continue; }
                    if (c == '/' && i + 1 < end && _source[i + 1] == '*')
                    {
                        i += 2;
                        while (i + 1 < end && !(_source[i] == '*' && _source[i + 1] == '/')) i++;
                        i = Math.Min(i + 2, end); continue;
                    }
                    if (c == '#') { while (i < end && _source[i] != '\n') i++; continue; }
                    int begin = i;
                    bool interpolated = c == '$' || (c == '@' && i + 1 < end && _source[i + 1] == '$');
                    bool verbatim = c == '@' || (c == '$' && i + 1 < end && _source[i + 1] == '@');
                    int quote = i;
                    while (quote < end && (_source[quote] == '$' || _source[quote] == '@')) quote++;
                    if (quote < end && (_source[quote] == '"' || _source[quote] == '\''))
                    {
                        char delimiter = _source[quote];
                        i = quote + 1;
                        int literal = begin;
                        while (i < end)
                        {
                            if (interpolated && _source[i] == '{')
                            {
                                if (i + 1 < end && _source[i + 1] == '{') { i += 2; continue; }
                                AddToken(literal, i);
                                int expression = ++i, depth = 1;
                                // Find the end without confusing a quoted brace with an interpolation boundary.
                                while (i < end && depth > 0)
                                {
                                    if (_source[i] == '"' || _source[i] == '\'')
                                    {
                                        char q = _source[i++];
                                        while (i < end && _source[i] != q)
                                        { if (_source[i] == '\\') i++; i++; }
                                    }
                                    else if (_source[i] == '{') depth++;
                                    else if (_source[i] == '}') depth--;
                                    if (depth > 0) i++;
                                }
                                Tokenize(expression, i);
                                literal = Math.Min(i + 1, end); i = literal;
                                continue;
                            }
                            if (!verbatim && _source[i] == '\\') { i = Math.Min(i + 2, end); continue; }
                            if (_source[i] == delimiter)
                            {
                                if (verbatim && i + 1 < end && _source[i + 1] == delimiter) { i += 2; continue; }
                                i++; break;
                            }
                            i++;
                        }
                        if (i > literal) AddToken(literal, i);
                        continue;
                    }
                    if (char.IsLetterOrDigit(c) || c == '_')
                    { do { i++; } while (i < end && (char.IsLetterOrDigit(_source[i]) || _source[i] == '_')); }
                    else if (c == '=' && i + 1 < end && _source[i + 1] == '>') i += 2;
                    else i++;
                    AddToken(begin, i);
                }
            }

            internal List<Member> Members(string path)
            {
                var result = new List<Member>();
                var scopes = new Stack<Tuple<int, string, bool>>();
                for (int i = 0; i < _tokens.Count - 1; i++)
                {
                    while (scopes.Count > 0 && scopes.Peek().Item1 < i) scopes.Pop();
                    if (Is(i, "namespace"))
                    {
                        int end = i + 1;
                        string ns = "";
                        while (end < _tokens.Count && !Is(end, "{") && !Is(end, ";")) ns += Text(end++);
                        if (scopes.Count > 0) ns = scopes.Peek().Item2 + "." + ns;
                        scopes.Push(Tuple.Create(Is(end, "{") ? Close(end) : _tokens.Count, ns, false));
                    }
                    if (Is(i, "class") || Is(i, "struct") || Is(i, "interface"))
                    {
                        // A generic constraint's 'class' has no following identifier/body of its own.
                        if (Is(i + 1, ",") || Is(i + 1, "{") || Is(i + 1, "?")) continue;
                        int brace = i + 2;
                        while (brace < _tokens.Count && !Is(brace, "{") && !Is(brace, ";")) brace++;
                        if (Is(brace, "{") && Close(brace) > brace)
                        {
                            string name = Text(i + 1);
                            string full = scopes.Count == 0 ? name : scopes.Peek().Item2 +
                                (scopes.Peek().Item3 ? "+" : ".") + name;
                            ReadType(path, name, full, brace, Close(brace), result);
                            scopes.Push(Tuple.Create(Close(brace), full, true));
                        }
                    }
                }
                return result;
            }

            private void ReadType(string path, string owner, string fullOwner, int begin, int end, List<Member> result)
            {
                for (int i = begin + 1; i < end;)
                {
                    if (Is(i, ";")) { i++; continue; }
                    while (Is(i, "[")) i = Close(i) + 1;
                    int start = i, paren = -1, equals = -1, angles = 0;
                    bool nested = false, isStatic = false, constant = false;
                    while (i < end && !Is(i, ";") && !Is(i, "{") && !Is(i, "=>"))
                    {
                        nested |= Is(i, "class") || Is(i, "struct") || Is(i, "interface") || Is(i, "enum");
                        isStatic |= Is(i, "static"); constant |= Is(i, "const");
                        if (Is(i, "=") && equals < 0) equals = i;
                        if (Is(i, "<")) angles++;
                        if (Is(i, ">")) angles--;
                        if (Is(i, "("))
                        {
                            if (paren < 0 && equals < 0 && angles == 0 &&
                                !Is(i - 1, "static") && !Is(i - 1, "public") && !Is(i - 1, "private")) paren = i;
                            i = Close(i);
                        }
                        if (Is(i, "[")) i = Close(i);
                        i++;
                    }
                    int separator = i;
                    bool method = paren >= 0;
                    int name = (method ? paren : equals >= 0 ? equals : separator) - 1;
                    if (method && Is(name, ">"))
                    { while (name > start && !Is(name, "<")) name--; name--; }
                    bool stored = !Is(separator, "=>");
                    if (Is(i, "{"))
                    {
                        int close = Close(i);
                        if (!method && equals < 0)
                        {
                            stored = false;
                            for (int p = i + 1; p < close; p++)
                                stored |= Is(p, "set") || Is(p, "init") || (Is(p, "get") && Is(p + 1, ";"));
                        }
                        i = close + 1;
                        if (equals >= 0 || Is(i, "="))
                        { while (i < end && !Is(i, ";")) { if (Is(i, "{") || Is(i, "(")) i = Close(i); i++; } }
                    }
                    else if (Is(i, "=>"))
                    {
                        while (i < end && !Is(i, ";"))
                        { if (Is(i, "{") || Is(i, "(")) i = Close(i); i++; }
                    }
                    if (Is(i, ";")) i++;
                    if (!nested && name >= start && i > start)
                    {
                        string type = Resolve(Text(name - 1));
                        result.Add(new Member { Path = path, Owner = owner, FullOwner = fullOwner, Name = Text(name), Type = type,
                            Static = isStatic || constant, Constant = constant, Stored = stored, Method = method,
                            Parameterless = method && Is(paren + 1, ")"),
                            Start = start, End = i, NameIndex = name });
                    }
                    if (i <= start) i++;
                }
            }

            internal void Lookups(Member member, Dictionary<string, int> hits)
            {
                for (int i = member.Start; i < member.End; i++)
                {
                    if (member.Method && i == member.NameIndex) continue;
                    bool find = Is(i, "FindObjectOfType") || Is(i, "FindFirstObjectByType") ||
                        Is(i, "FindAnyObjectByType") || Is(i, "FindObjectsOfType") || Is(i, "FindObjectsByType") ||
                        Is(i, "FindObjectsOfTypeAll");
                    if (find && (Is(i + 1, "<") || (Is(i + 1, "(") && Is(i + 2, "typeof"))))
                    {
                        int type = i + (Is(i + 1, "<") ? 2 : 4);
                        while (Is(type + 1, ".") || Is(type + 1, ":")) type += Is(type + 1, ".") ? 2 : 3;
                        string target = Resolve(Text(type));
                        Add(hits, "lookup|" + member.Key + "|" + Text(i) + "<" + target + ">");
                    }
                    else if (find && Is(i + 1, "("))
                        Add(hits, "lookup|" + member.Key + "|" + Text(i) + "<dynamic>");
                    // Name/tag lookup arguments may be variables; all such calls require an ownership decision.
                    if ((Is(i, "Find") && Is(i - 1, ".") && Is(i - 2, "GameObject")) ||
                        Is(i, "FindWithTag") || Is(i, "FindGameObjectWithTag") || Is(i, "FindGameObjectsWithTag"))
                        Add(hits, "lookup|" + member.Key + "|" + Text(i));
                    if ((Is(i, "RestQuorum") && Is(i + 1, ".") && Is(i + 2, "LocalSleeper")) ||
                        (Is(i, "RelationshipGraph") && Is(i + 1, ".") && Is(i + 2, "PlayerId")))
                        Add(hits, "identity-use|" + member.Key + "|" + Text(i) + "." + Text(i + 2));
                    if ((member.Owner == "PlayerGear" || member.Owner == "RestLocker") &&
                        Is(i, "GameServices") && Is(i + 2, "Save"))
                        Add(hits, "personal-save|" + member.Key);
                    if ((Is(i, "GameServices") || (_aliases.Count > 0 && Resolve(Text(i)) == "GameServices")) &&
                        Is(i + 1, ".") && PersonalServices.Contains(Text(i + 2)))
                        Add(hits, "service-use|" + member.Key + "|GameServices." + Text(i + 2));
                    if (_staticServices && !Is(i - 1, ".") && PersonalServices.Contains(Text(i)))
                        Add(hits, "service-use|" + member.Key + "|GameServices." + Text(i));
                }
            }
        }
    }
}
