using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>File-only ratchet for explicit SpriteRenderer references to the shared square.</summary>
    public class PlaceholderSquareCensusTests
    {
        const string SquareGuid = "0c1634d5861312443ac89b42a97625b6";
        const string SquareMeta = "Assets/_Project/Art/Sprites/Square.png.meta";
        const string CensusPath = "docs/art/placeholder-square-census.md";

        // Existing debt at f1b4ab2, not approval to add more placeholders. Remove a line when
        // the prefab stops using Square, along with its rows in the census. Never add new users.
        static readonly string[] KnownSquarePrefabs =
        {
            "Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab",
            "Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcCommunityHall.prefab",
            "Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab",
            "Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishingGearWholesaler.prefab",
            "Assets/_Project/Prefabs/Props/Storage/CamperShoreLead.prefab",
            "Assets/_Project/Prefabs/Props/Storage/GinnyGearLocker.prefab",
            "Assets/_Project/Prefabs/Props/Storage/GinnyRodRack.prefab",
        };

        [Test]
        public void EveryExplicitSquareRenderer_IsInTheShrinkingCensus()
        {
            Assert.That(Regex.Match(File.ReadAllText(SquareMeta), @"(?m)^guid: ([0-9a-f]{32})\r?$")
                .Groups[1].Value, Is.EqualTo(SquareGuid), "Square identity changed: review the census target.");
            string[] paths = Directory.GetFiles("Assets/_Project", "*", SearchOption.AllDirectories)
                .Where(p => string.Equals(Path.GetExtension(p), ".prefab", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.That(paths, Is.Not.Empty, "Prefab scan found no files.");
            var rows = new List<CensusRow>();
            var faults = new List<string>();
            foreach (string path in paths)
            {
                try { rows.AddRange(ReadPrefab(path, File.ReadAllText(path))); }
                catch (Exception ex) when (ex is FormatException || ex is IOException || ex is UnauthorizedAccessException)
                { faults.Add(path + ": " + ex.Message); }
            }
            faults.AddRange(ComparePrefabList(rows.Select(r => r.Prefab), KnownSquarePrefabs));
            faults.AddRange(CompareRows(rows, ReadCensus(File.ReadAllText(CensusPath))));
            TestContext.WriteLine($"Scanned {paths.Length} prefabs; {rows.Select(r => r.Prefab).Distinct().Count()} " +
                $"use Square on {rows.Count} explicit SpriteRenderers.");
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        static IEnumerable<string> ComparePrefabList(IEnumerable<string> actual, IEnumerable<string> known)
        {
            return actual.Except(known, StringComparer.Ordinal)
                .Select(p => "New Square prefab: " + p + ". Replace the placeholder; do not grow KnownSquarePrefabs.")
                .Concat(known.Except(actual, StringComparer.Ordinal)
                    .Select(p => "No longer uses Square: " + p + ". Delete its line from KnownSquarePrefabs and its census rows."));
        }

        sealed class CensusRow
        {
            public string Prefab, Renderer, ObjectName, Tint, Scale;
            public string Key => Prefab + " #" + Renderer;
            public string TableLine => "| " + Renderer + " | " + ObjectName + " | " + Tint + " | " + Scale + " |";
        }

        static IEnumerable<string> CompareRows(List<CensusRow> actual, List<CensusRow> expected)
        {
            var found = actual.ToDictionary(r => r.Key, StringComparer.Ordinal);
            var listed = expected.ToDictionary(r => r.Key, StringComparer.Ordinal);
            foreach (var row in actual)
            {
                if (!listed.TryGetValue(row.Key, out var old))
                    yield return "New Square renderer: " + row.Key + " (" + row.ObjectName + "). Replace the placeholder.";
                else if (row.TableLine != old.TableLine)
                    yield return "Census details changed for " + row.Key + ". Review and update its object/tint/local scale row: " + row.TableLine;
            }
            foreach (var row in expected.Where(r => !found.ContainsKey(r.Key)))
                yield return "No longer uses Square: " + row.Key + ". Delete this row from " + CensusPath + ".";
        }

        sealed class YamlBlock
        {
            public string Type, Id, Body;
        }

        static List<CensusRow> ReadPrefab(string path, string yaml)
        {
            var rows = new List<CensusRow>();
            if (!yaml.Contains(SquareGuid)) return rows;
            var blocks = Regex.Matches(yaml, @"(?ms)^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\z)")
                .Cast<Match>().Select(m => new YamlBlock { Type = m.Groups[1].Value, Id = m.Groups[2].Value,
                    Body = m.Groups[3].Value }).ToArray();
            var objects = blocks.Where(b => b.Type == "1").ToDictionary(b => b.Id, StringComparer.Ordinal);
            var transforms = blocks.Where(b => (b.Type == "4" || b.Type == "224") &&
                Regex.IsMatch(b.Body, @"(?m)^  m_LocalScale:"))
                .ToDictionary(b => FileId(RequiredField(b.Body, "m_GameObject")), StringComparer.Ordinal);

            // An explicit variant override must not bypass the direct-component census. No current
            // baseline row needs one; require deliberate parser support if this representation appears.
            foreach (var block in blocks.Where(b => b.Type == "1001"))
            {
                if (Regex.IsMatch(block.Body, @"(?m)^\s+propertyPath: m_Sprite\s*\r?\n\s+value:[^\n]*\n\s+objectReference: \{[^}\n]*\bguid: " + SquareGuid + @"\b"))
                    throw new FormatException("explicit Square m_Sprite prefab override: replace it or extend census resolution; do not ignore it");
            }
            foreach (var renderer in blocks.Where(b => b.Type == "212"))
            {
                string sprite = OptionalField(renderer.Body, "m_Sprite");
                if (!Regex.IsMatch(sprite, @"\bguid:\s*" + SquareGuid + @"\b") || FileId(sprite) == "0") continue;
                string go = FileId(RequiredField(renderer.Body, "m_GameObject"));
                if (!objects.TryGetValue(go, out var obj) || !transforms.TryGetValue(go, out var transform))
                    throw new FormatException("Square renderer " + renderer.Id + " has no local GameObject/Transform to census");
                rows.Add(new CensusRow { Prefab = path, Renderer = renderer.Id,
                    ObjectName = RequiredField(obj.Body, "m_Name"), Tint = RequiredField(renderer.Body, "m_Color"),
                    Scale = RequiredField(transform.Body, "m_LocalScale") });
            }
            return rows;
        }

        static string OptionalField(string block, string key) =>
            Regex.Match(block, @"(?m)^  " + Regex.Escape(key) + @":[ \t]*([^\r\n]*)").Groups[1].Value.Trim();

        static string RequiredField(string block, string key)
        {
            string value = OptionalField(block, key);
            if (value.Length == 0) throw new FormatException("missing " + key + " on a Square component");
            return value;
        }

        static string FileId(string reference)
        {
            var match = Regex.Match(reference, @"\bfileID:\s*(-?\d+)");
            if (!match.Success) throw new FormatException("missing fileID: " + reference);
            return match.Groups[1].Value;
        }

        // The documented rows are the per-renderer baseline; no second copy can drift from the doc.
        static List<CensusRow> ReadCensus(string markdown)
        {
            string prefab = null;
            var rows = new List<CensusRow>();
            foreach (string raw in markdown.Replace("\r", "").Split('\n'))
            {
                var heading = Regex.Match(raw, @"^### `(Assets/_Project/[^`]+\.prefab)`$");
                if (heading.Success) { prefab = heading.Groups[1].Value; continue; }
                if (!Regex.IsMatch(raw, @"^\| -?\d+ \|")) continue;
                string[] cells = raw.Split('|').Skip(1).Take(4).Select(s => s.Trim()).ToArray();
                if (prefab == null || raw.Split('|').Length != 6 || cells.Any(string.IsNullOrEmpty))
                    throw new FormatException("malformed census row: " + raw);
                rows.Add(new CensusRow { Prefab = prefab, Renderer = cells[0], ObjectName = cells[1], Tint = cells[2], Scale = cells[3] });
            }
            if (rows.GroupBy(r => r.Key).Any(g => g.Count() != 1)) throw new FormatException("duplicate census renderer row");
            return rows;
        }

        [Test]
        public void Parser_JoinsIdsInsteadOfDocumentOrder_AndIncludesDisabledRenderers()
        {
            var rows = ReadPrefab("example.prefab", ExampleYaml().Replace("\n", "\r\n"));
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].TableLine, Is.EqualTo("| 30 | Art | {r: 0.1, g: 0.2, b: 0.3, a: 0.4} | {x: 2, y: 3, z: 1} |"));
        }

        [TestCase("m_Icon")]
        [TestCase("_sprite")]
        public void NonSpriteRendererFields_DoNotEnterCensus(string field)
        {
            Assert.That(ReadPrefab("example.prefab", ExampleYaml().Replace("m_Sprite:", field + ":")), Is.Empty);
            Assert.That(ReadPrefab("example.prefab", ExampleYaml().Replace("!u!212", "!u!114")), Is.Empty);
        }

        [Test]
        public void AnotherSpriteGuid_DoesNotEnterCensus()
        {
            Assert.That(ReadPrefab("example.prefab", ExampleYaml().Replace(SquareGuid, new string('a', 32))), Is.Empty);
        }

        [Test]
        public void EveryRendererIsCounted_EvenWithTheSameObjectName()
        {
            var rows = ReadPrefab("example.prefab", ExampleYaml() +
                "--- !u!212 &31\nSpriteRenderer:\n  m_GameObject: {fileID: 10}\n  m_Sprite: {fileID: 21300000, guid: " +
                SquareGuid + ", type: 3}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n");
            Assert.That(rows.Select(r => r.Renderer), Is.EqualTo(new[] { "30", "31" }));
        }

        [Test]
        public void MissingObjectLink_FailsInsteadOfDroppingTheRenderer()
        {
            Assert.Throws<FormatException>(() => ReadPrefab("example.prefab", ExampleYaml().Replace("!u!1 &10", "!u!1 &99")));
        }

        [Test]
        public void ExplicitSquareVariantOverride_CannotBypassCensus()
        {
            string yaml = "--- !u!1001 &1\nPrefabInstance:\n  m_Modifications:\n  - target: {fileID: 30}\n" +
                "    propertyPath: m_Sprite\n    value: \n    objectReference: {fileID: 21300000, guid: " + SquareGuid + ", type: 3}\n";
            Assert.Throws<FormatException>(() => ReadPrefab("variant.prefab", yaml));
        }

        [Test]
        public void NewPrefabFails_AndRetiredPrefabRequestsLineDeletion()
        {
            string[] faults = ComparePrefabList(new[] { "new.prefab" }, new[] { "retired.prefab" }).ToArray();
            Assert.That(faults.Length, Is.EqualTo(2));
            StringAssert.Contains("New Square prefab: new.prefab", faults[0]);
            StringAssert.Contains("retired.prefab. Delete its line", faults[1]);
        }

        [Test]
        public void RetiredRendererRequestsRowDeletion_AndAnExtraRendererFails()
        {
            var old = ReadPrefab("known.prefab", ExampleYaml());
            var changed = ReadPrefab("known.prefab", ExampleYaml().Replace("!u!212 &30", "!u!212 &31"));
            string[] faults = CompareRows(changed, old).ToArray();
            Assert.That(faults.Length, Is.EqualTo(2));
            StringAssert.Contains("New Square renderer: known.prefab #31", faults[0]);
            StringAssert.Contains("known.prefab #30. Delete this row", faults[1]);
        }

        [Test]
        public void DocumentRows_MatchObjectTintAndLocalScale()
        {
            var rows = ReadPrefab("Assets/_Project/example.prefab", ExampleYaml());
            string doc = "### `Assets/_Project/example.prefab`\n" + rows[0].TableLine;
            Assert.That(CompareRows(rows, ReadCensus(doc)), Is.Empty);
            Assert.That(CompareRows(rows, ReadCensus(doc.Replace("{x: 2,", "{x: 4,"))).Count(), Is.EqualTo(1));
            Assert.Throws<FormatException>(() => ReadCensus(doc + "\n" + rows[0].TableLine));
        }

        static string ExampleYaml() =>
            "--- !u!212 &30\nSpriteRenderer:\n  m_GameObject: {fileID: 10}\n  m_Enabled: 0\n" +
            "  m_Sprite: {guid: " + SquareGuid + ", type: 3, fileID: 21300000}\n" +
            "  m_Color: {r: 0.1, g: 0.2, b: 0.3, a: 0.4}\n" +
            "--- !u!4 &20\nTransform:\n  m_GameObject: {fileID: 10}\n  m_LocalScale: {x: 2, y: 3, z: 1}\n" +
            "--- !u!1 &10\nGameObject:\n  m_Name: Art\n  m_IsActive: 0\n";
    }
}
