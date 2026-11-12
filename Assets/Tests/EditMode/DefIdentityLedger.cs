using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Test-only inventory: historical baseline plus independently authored additions.
    /// Never deduplicates or infers a replacement for an established identity.</summary>
    internal static class DefIdentityLedger
    {
        internal const string BaselinePath = "Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json";
        internal const string AdditionsPath = "Assets/Tests/EditMode/Fixtures/DefIdentityAdditions";

        [Serializable]
        internal sealed class Identity
        {
            public string Type;
            public string Guid;
            public string Id;
        }

        [Serializable]
        internal sealed class Ledger
        {
            public string[] Types;
            public Identity[] Entries;
        }

        private static readonly Regex GuidPattern = new Regex("\\A[0-9a-f]{32}\\z");

        // Additions have only string fields. Match their entire flat JSON shape before decoding
        // tokens with the existing reader, whose object parser would otherwise overwrite repeated keys.
        private const string Space = @"[ \t\r\n]*";
        private const string JsonString = @"""(?:[^""\\\x00-\x1f]|\\(?:[""\\/bfnrt]|u[0-9a-fA-F]{4}))*""";
        private const string JsonField = "(?<key>" + JsonString + ")" + Space + ":" + Space
            + "(?<value>" + JsonString + ")" + Space;
        private static readonly Regex AdditionObject = new Regex(
            @"\A" + Space + @"\{" + Space + "(?:" + JsonField + "(?:," + Space + JsonField + ")*)?\\}" + Space + @"\z");

        internal static Ledger Load() => Load(BaselinePath, AdditionsPath);

        internal static Ledger Load(string baselinePath, string additionsPath) => Read(baselinePath,
            Directory.EnumerateFiles(additionsPath, "*.json", SearchOption.AllDirectories));

        // Explicit paths let tests reverse enumeration without depending on a filesystem's order.
        internal static Ledger Read(string baselinePath, IEnumerable<string> additionPaths)
        {
            var types = new Dictionary<string, string>(StringComparer.Ordinal);
            var guids = new Dictionary<string, string>(StringComparer.Ordinal);
            var ids = new Dictionary<(string Type, string Id), string>();
            var entries = new List<Identity>();
            var baseline = ReadBaseline(baselinePath);
            for (int i = 0; i < baseline.Types.Length; i++)
                AddType(baseline.Types[i], baselinePath + " Types[" + i + "]", types);
            for (int i = 0; i < baseline.Entries.Length; i++)
                AddIdentity(baseline.Entries[i], baselinePath + " Entries[" + i + "]", entries, guids, ids);

            foreach (string path in additionPaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                var fields = ReadAddition(path);
                if (path.EndsWith(".type.json", StringComparison.Ordinal))
                {
                    RequireFields(fields, path, "Type");
                    string type = ReadString(fields["Type"], path, "Type");
                    Require(Path.GetFileName(path) == type + ".type.json", path,
                        "type declaration filename must match Type + .type.json");
                    AddType(type, path, types);
                }
                else
                {
                    RequireFields(fields, path, "Type", "Guid", "Id");
                    var row = ReadIdentity(fields, path);
                    Require(Path.GetFileName(path) == row.Guid + ".json", path,
                        "identity filename must match Guid + .json");
                    AddIdentity(row, path, entries, guids, ids);
                }
            }

            foreach (var row in entries)
                Require(types.ContainsKey(row.Type), guids[row.Guid], "undeclared Def type " + row.Type);
            return new Ledger
            {
                Types = types.Keys.OrderBy(type => type, StringComparer.Ordinal).ToArray(),
                Entries = entries.OrderBy(row => row.Type, StringComparer.Ordinal)
                    .ThenBy(row => row.Id, StringComparer.Ordinal)
                    .ThenBy(row => row.Guid, StringComparer.Ordinal).ToArray(),
            };
        }

        private static Ledger ReadBaseline(string path)
        {
            object parsed;
            try { parsed = DeckSidecarJson.Parse(File.ReadAllText(path)); }
            catch (FormatException error) { throw new FormatException(path + ": " + error.Message, error); }
            var fields = parsed as Dictionary<string, object>;
            RequireFields(fields, path, "Types", "Entries");
            var types = fields["Types"] as List<object>;
            var entries = fields["Entries"] as List<object>;
            Require(types != null && entries != null, path, "Types and Entries must be arrays");
            return new Ledger
            {
                Types = types.Select(value => ReadString(value, path, "Types")).ToArray(),
                Entries = entries.Select(value => ReadIdentity(value as Dictionary<string, object>, path)).ToArray(),
            };
        }

        private static Dictionary<string, object> ReadAddition(string path)
        {
            var match = AdditionObject.Match(File.ReadAllText(path));
            Require(match.Success, path, "expected a JSON object containing only string fields");
            var fields = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < match.Groups["key"].Captures.Count; i++)
            {
                string key = (string)DeckSidecarJson.Parse(match.Groups["key"].Captures[i].Value);
                Require(!fields.ContainsKey(key), path, "duplicate JSON field " + key);
                fields.Add(key, DeckSidecarJson.Parse(match.Groups["value"].Captures[i].Value));
            }
            return fields;
        }

        private static Identity ReadIdentity(Dictionary<string, object> fields, string source)
        {
            RequireFields(fields, source, "Type", "Guid", "Id");
            return new Identity
            {
                Type = ReadString(fields["Type"], source, "Type"),
                Guid = ReadString(fields["Guid"], source, "Guid"),
                Id = ReadString(fields["Id"], source, "Id"),
            };
        }

        private static string ReadString(object value, string source, string field)
        {
            string text = value as string;
            Require(!string.IsNullOrWhiteSpace(text) && !text.Any(char.IsControl), source,
                field + " must be a nonempty string without control characters");
            return text;
        }

        private static void AddType(string type, string source, Dictionary<string, string> types)
        {
            Require(!types.TryGetValue(type, out string previous), source,
                "duplicate type declaration " + type + "; first recorded in " + previous);
            types.Add(type, source);
        }

        private static void AddIdentity(Identity row, string source, List<Identity> entries,
            Dictionary<string, string> guids, Dictionary<(string Type, string Id), string> ids)
        {
            Require(GuidPattern.IsMatch(row.Guid), source, "Guid must be 32 lowercase hexadecimal characters");
            Require(!guids.TryGetValue(row.Guid, out string previousGuid), source,
                "duplicate Guid " + row.Guid + "; first recorded in " + previousGuid);
            Require(!ids.TryGetValue((row.Type, row.Id), out string previousId), source,
                "duplicate (Type, Id) " + row.Type + " " + row.Id + "; first recorded in " + previousId);
            guids.Add(row.Guid, source);
            ids.Add((row.Type, row.Id), source);
            entries.Add(row);
        }

        private static void RequireFields(Dictionary<string, object> fields, string source, params string[] expected)
        {
            Require(fields != null && fields.Count == expected.Length && expected.All(fields.ContainsKey),
                source, "expected exactly these fields: " + string.Join(", ", expected));
        }

        private static void Require(bool condition, string source, string message)
        {
            if (!condition) throw new FormatException(source + ": " + message);
        }
    }
}
