using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public partial class ModuleBoundaryTests
    {
        // State/models: Core, Environment, Fishing, Economy, World, Boats, Vehicles.
        // App/Player compose inputs. Keep Art/UI/Audio: interaction hosts can live there too.
        // Editor path segments are excluded, but conditional editor code in runtime files is reviewed.
        private static readonly string[] SimulationFolders =
        {
            "Core", "Environment", "Fishing", "Economy", "World", "Boats", "Vehicles",
            "App", "Player", "Art", "UI", "Audio"
        };

        // Conservative TEXT policy, intentionally including comments and literals. The baseline marks
        // non-code mentions explicitly. No lexer can swallow an interpolated expression or a new call.
        // The additional alias/static-import alternatives fail closed instead of silently bypassing
        // the named APIs. This is not semantic analysis or proof of seed provenance at other lines.
        private static readonly Regex NondeterministicSource = new Regex(
            @"\b(?:UnityEngine\s*\.\s*Random|System\s*\.\s*Random|Guid\s*\.\s*NewGuid|" +
            @"DateTime\s*\.\s*(?:Now|UtcNow)|Time\s*\.\s*(?:time|realtimeSinceStartup|unscaledTime)(?:AsDouble)?|" +
            @"Environment\s*\.\s*TickCount(?:64)?|Stopwatch|new\s+Random)\b|" +
            @"\bRandom\s*\.\s*\w+|" +
            @"\busing\s+(?:static\s+(?:(?:global::)?(?:System|UnityEngine)\s*\.\s*)?" +
            @"(?:Random|Guid|DateTime|Time|Environment|Stopwatch)|" +
            @"\w+\s*=\s*(?:global::)?(?:System|UnityEngine)(?:\s*\.\s*Diagnostics)?" +
            @"(?:\s*\.\s*(?:Random|Guid|DateTime|Time|Environment|Stopwatch))?)\s*;",
            RegexOptions.CultureInvariant);

        [Test]
        public void RuntimeSource_RandomnessAndClocksMatchReviewedAllowlist()
        {
            Assert.That(Directory.Exists(CodeRoot), Is.True, "Source root missing; do not pass an empty scan.");
            var paths = Directory.GetFiles(CodeRoot, "*.cs", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/').Substring(CodeRoot.Length + 1))
                .Where(IsRuntimeSource).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (string folder in SimulationFolders)
                Assert.That(paths.Any(p => p.StartsWith(folder + "/", StringComparison.Ordinal)), Is.True,
                    "Missing runtime folder: " + folder);
            var actual = new List<SourceHit>();
            foreach (string path in paths)
            {
                Assert.That(SimulationFolders.Contains(path.Split('/')[0]), Is.True,
                    "Review the scan boundary for new runtime folder: " + path);
                actual.AddRange(FindSourceHits(path, File.ReadAllText(CodeRoot + "/" + path)));
            }
            Assert.That(SourceDifferences(actual, ReviewedSource).ToArray(), Is.Empty,
                "Review each new/changed use; remove stale allowances. Known faults are not fixed by this guard.");
        }

        [TestCase("UnityEngine.Random.value")]
        [TestCase("new System.Random()")]
        [TestCase("new Random()")]
        [TestCase("Random.Range(0, 2)")]
        [TestCase("Guid.NewGuid()")]
        [TestCase("DateTime.Now")]
        [TestCase("System.DateTime.UtcNow")]
        [TestCase("Time.time")]
        [TestCase("Time.timeAsDouble")]
        [TestCase("Time.realtimeSinceStartup")]
        [TestCase("Time.realtimeSinceStartupAsDouble")]
        [TestCase("Time.unscaledTime")]
        [TestCase("Time.unscaledTimeAsDouble")]
        [TestCase("Environment.TickCount")]
        [TestCase("System.Environment.TickCount64")]
        [TestCase("Stopwatch.StartNew()")]
        [TestCase("System.Diagnostics.Stopwatch.GetTimestamp()")]
        [TestCase("UnityEngine /* explanation */ . Random.value")]
        [TestCase("Time // explanation\n . time")]
        [TestCase("Time\n .\n unscaledTime")]
        [TestCase("using Clock = UnityEngine.Time;")]
        [TestCase("using Engine = UnityEngine;")]
        [TestCase("using static System.Guid;")]
        [TestCase("using static global::UnityEngine.Time;")]
        [TestCase("// Time.time is only a mention")]
        [TestCase("var label = $\"now {DateTime.UtcNow}\";")]
        public void RuntimeSourcePolicy_DetectsNewEntropyAndClockUses(string source)
        {
            Assert.That(FindSourceHits("Core/Probe.cs", source).Count, Is.EqualTo(1));
            Assert.That(SourceDifferences(FindSourceHits("Core/Probe.cs", source),
                Array.Empty<SourceAllowance>()).Count(), Is.EqualTo(1));
        }

        [TestCase("Time.timeScale = 0;")]
        [TestCase("var delta = Time.deltaTime;")]
        [TestCase("var rng = new StableRandom(seed);")]
        public void RuntimeSourcePolicy_UsesIdentifierBoundaries(string source)
            => Assert.That(FindSourceHits("Core/Probe.cs", source), Is.Empty);

        [Test]
        public void RuntimeSourcePolicy_RejectsAddedChangedDuplicatedAndStaleAllowances()
        {
            const string source = "var now = Time.time;";
            var hit = FindSourceHits("Core/Probe.cs", source).Single();
            var reviewed = new[] { new SourceAllowance(hit.Path, hit.Fingerprint, 1) };
            Assert.That(SourceDifferences(new[] { hit }, reviewed), Is.Empty);
            Assert.That(SourceDifferences(FindSourceHits(hit.Path, source + "\n" + source), reviewed).Count(), Is.EqualTo(1));
            Assert.That(SourceDifferences(FindSourceHits(hit.Path, source + " var rng = new System.Random();"), reviewed).Count(), Is.EqualTo(2));
            Assert.That(SourceDifferences(FindSourceHits(hit.Path, "var now = Time.unscaledTime;"), reviewed).Count(), Is.EqualTo(2));
            Assert.That(SourceDifferences(FindSourceHits("Fishing/Other.cs", source), reviewed).Count(), Is.EqualTo(2));
            Assert.That(SourceDifferences(Array.Empty<SourceHit>(), reviewed).Count(), Is.EqualTo(1));
            // Line movement and indentation do not change the reviewed statement.
            Assert.That(SourceDifferences(FindSourceHits(hit.Path, "\n    " + source + "\n"), reviewed), Is.Empty);
        }

        [TestCase("Core/Editor/Probe.cs", false)]
        [TestCase("App/Editor/Nested/Probe.cs", false)]
        [TestCase("World/EditorLike/Probe.cs", true)]
        [TestCase("NewModule/Probe.cs", true)]
        public void RuntimeSourcePolicy_ExcludesOnlyEditorPathSegments(string path, bool expected)
            => Assert.That(IsRuntimeSource(path), Is.EqualTo(expected));

        private static bool IsRuntimeSource(string path) => !path.Split('/').Contains("Editor");

        private sealed class SourceHit
        {
            public string Path, Fingerprint;
            public int Line;
            public string Key => Path + "|" + Fingerprint;
        }

        private sealed class SourceAllowance
        {
            public readonly string Path, Fingerprint;
            public readonly int Count;
            public string Key => Path + "|" + Fingerprint;
            public SourceAllowance(string path, string fingerprint, int count)
            { Path = path; Fingerprint = fingerprint; Count = count; }
        }

        private static List<SourceHit> FindSourceHits(string path, string source)
        {
            // Replace comments with equal-length whitespace for token separation; scan both
            // versions, retaining comment mentions from the original as well as split executable tokens.
            string separated = Regex.Replace(source, @"/\*[\s\S]*?\*/|//[^\r\n]*",
                m => Regex.Replace(m.Value, @"[^\r\n]", " "));
            var spans = new HashSet<string>();
            var hits = new List<SourceHit>();
            foreach (string text in new[] { source, separated })
                foreach (Match match in NondeterministicSource.Matches(text))
                {
                    int start = source.LastIndexOf('\n', match.Index) + 1;
                    int end = source.IndexOf('\n', match.Index + match.Length);
                    if (end < 0) end = source.Length;
                    if (!spans.Add(start + ":" + end)) continue;
                    string normalized = Regex.Replace(source.Substring(start, end - start).Trim(), @"\s+", " ");
                    using (var sha = SHA256.Create())
                        hits.Add(new SourceHit
                        {
                            Path = path,
                            Fingerprint = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized)))
                                .Replace("-", "").ToLowerInvariant(),
                            Line = 1 + source.Substring(0, start).Count(c => c == '\n')
                        });
                }
            return hits;
        }

        private static IEnumerable<string> SourceDifferences(IEnumerable<SourceHit> hits, SourceAllowance[] allowances)
        {
            var actual = hits.GroupBy(h => h.Key).ToDictionary(g => g.Key, g => g.ToArray());
            var expected = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var allowance in allowances)
            {
                if (allowance.Count <= 0 || expected.ContainsKey(allowance.Key))
                { yield return "Invalid or duplicate allowance: " + allowance.Path; continue; }
                expected.Add(allowance.Key, allowance.Count);
            }
            foreach (var group in actual.OrderBy(g => g.Key, StringComparer.Ordinal))
                if (!expected.TryGetValue(group.Key, out int count) || count != group.Value.Length)
                    yield return group.Value[0].Path + ":" + group.Value[0].Line +
                        " unreviewed/changed count " + group.Value.Length + " (expected " + count + ")";
            foreach (var allowance in expected.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (!actual.ContainsKey(allowance.Key)) yield return "Stale allowance: " + allowance.Key;
        }
    }
}
