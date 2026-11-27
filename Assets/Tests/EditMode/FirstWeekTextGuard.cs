using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Checks authored words against a supplied boundary. No scene, clock or player state.</summary>
    public sealed class FirstWeekTextGuard
    {
        public sealed class Place
        {
            public readonly string Id;
            public readonly string Name;
            public Place(string id, string name) { Id = id; Name = name; }
        }

        public sealed class TextField
        {
            public readonly string File;
            public readonly string Field;
            public readonly string Text;
            public TextField(string file, string field, string text)
            {
                File = file; Field = field; Text = text ?? "";
            }
            public string Key => File + " :: " + Field;
        }

        public sealed class Mention
        {
            public readonly TextField Source;
            public readonly string PlaceId;
            public readonly string Matched;
            public readonly int Offset;
            public Mention(TextField source, string placeId, string matched, int offset)
            {
                Source = source; PlaceId = placeId; Matched = matched; Offset = offset;
            }
            public override string ToString() => Source.Key + " mentions " + PlaceId + " (" + Matched + "): " + Source.Text;
        }

        [Serializable]
        public sealed class ExceptionLine
        {
            public string File;
            public string Field;
            public string Text;
            public string Reason;
            public string Key => File + " :: " + Field;
        }

        [Serializable]
        public sealed class ExceptionAsset
        {
            public string Comment;
            public ExceptionLine[] Lines;
        }

        readonly List<(string Id, Regex Pattern)> _places = new List<(string, Regex)>();

        public FirstWeekTextGuard(IEnumerable<Place> outside)
        {
            if (outside == null) throw new ArgumentNullException(nameof(outside));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var place in outside.OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(place.Id) || string.IsNullOrWhiteSpace(place.Name))
                    throw new ArgumentException("An outside place needs its id and display name.");
                if (!ids.Add(place.Id)) throw new ArgumentException("Duplicate outside place: " + place.Id);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { place.Name };
                // Authoring qualifiers are not part of a place's spoken name.
                string spoken = Regex.Replace(place.Name, @"\s+\([^)]*\)$", "");
                names.Add(spoken);
                if (spoken.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) names.Add(spoken.Substring(4));
                string alternatives = string.Join("|", names.OrderByDescending(n => n.Length)
                    .ThenBy(n => n, StringComparer.Ordinal).Select(NamePattern));
                string word = @"[\p{L}\p{M}\p{N}_]";
                string idWord = @"[\p{L}\p{M}\p{N}_.]";
                _places.Add((place.Id, new Regex(
                    "(?<!" + word + ")(?:" + alternatives + ")(?!" + word + ")|(?<!" + idWord + ")"
                    + Regex.Escape(place.Id) + "(?!" + idWord + ")",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
            }
        }

        static string NamePattern(string name) => string.Join(@"\s+", Regex.Split(name.Trim(), @"\s+")
            .Select(part => Regex.Escape(part.Replace('’', '\'')).Replace("'", "['’]")));

        public List<Mention> Scan(IEnumerable<TextField> fields)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            var result = new List<Mention>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(field.File) || string.IsNullOrWhiteSpace(field.Field))
                    throw new ArgumentException("A text field needs its file and field name.");
                if (!keys.Add(field.Key)) throw new ArgumentException("Duplicate text field: " + field.Key);
                foreach (var place in _places)
                    foreach (Match match in place.Pattern.Matches(field.Text))
                        result.Add(new Mention(field, place.Id, match.Value, match.Index));
            }
            return result;
        }

        /// <summary>Each exception admits one exact authored line, never a file or a place in general.</summary>
        public List<string> Problems(IReadOnlyList<Mention> mentions, IEnumerable<ExceptionLine> exceptions)
        {
            if (mentions == null) throw new ArgumentNullException(nameof(mentions));
            if (exceptions == null) throw new ArgumentNullException(nameof(exceptions));
            var errors = new List<string>();
            var allowed = new Dictionary<string, ExceptionLine>(StringComparer.Ordinal);
            foreach (var line in exceptions)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.File) || string.IsNullOrWhiteSpace(line.Field)
                    || string.IsNullOrWhiteSpace(line.Text) || string.IsNullOrWhiteSpace(line.Reason))
                {
                    errors.Add("An exception needs a file, field, exact text and reason.");
                    continue;
                }
                if (allowed.ContainsKey(line.Key)) errors.Add("Duplicate exception: " + line.Key);
                else allowed.Add(line.Key, line);
            }
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mention in mentions)
            {
                if (allowed.TryGetValue(mention.Source.Key, out var line) && line.Text == mention.Source.Text)
                    used.Add(line.Key);
                else errors.Add("Unexcepted: " + mention);
            }
            foreach (var line in allowed.Values)
                if (!used.Contains(line.Key)) errors.Add("Stale exception; remove or review changed text: " + line.Key);
            errors.Sort(StringComparer.Ordinal);
            return errors;
        }
    }
}
