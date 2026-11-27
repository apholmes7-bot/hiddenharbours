using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public partial class ModuleBoundaryTests
    {
        private const string OwnershipDebtsPath =
            "Assets/Tests/EditMode/Fixtures/FirstWeekPlayerStateKnownDebts.json";

        // These are lexical tripwires, not a claim that all actor state has been isolated.
        private const string ActorTypes =
            "PlayerWalkController|ControlSwitcher|CarryHands|ClamBucket|PlayerWallet|" +
            "ICarrier|IHandsCarrier|ICatchHands|IPlayerAppearance|INeedsService|IBodyService|" +
            "ICleanlinessService|IPlayerStrength|CatchRecordLog|GiftLedger|IActiveBoatService|" +
            "IHelmControl|IHelmInstruments|HelmSlot|LicenseService";

        private static readonly OwnershipRule[] OwnershipRules =
        {
            new OwnershipRule("SP001", @"\b(?:public|private|internal|protected)\s+static\s+(?:readonly\s+)?(?:" + ActorTypes + @")\s+\w+\s*(?=[{;=]|\r?$)"),
            new OwnershipRule("SP002", @"\bFind(?:ObjectOfType|AnyObjectByType|FirstObjectByType)\s*<\s*(?:[\w]+\.)*(?:" + ActorTypes + @"|ShipHold|DeckWalkController)\s*>"),
            new OwnershipRule("SP003", @"\b(?:public|private|internal|protected)\s+(?:readonly\s+)?(?:PlayerWalkController|ControlSwitcher|CarryHands|ClamBucket)\s+\w+\s*(?=[;=])"),
            new OwnershipRule("SP004", @"\bpublic\s+[\w.]+(?:\s*<[^;=\r\n]+>)?\s+(?:Player\w*|Rest\w*|ActiveHullId|BucketCatch|CaughtSpecies|WornOutfitId|NavWaypoints|NavRoute|NavTrack|NpcGiftDays)\s*(?=[;=])",
                "Core/Save/SaveData.cs"),
            new OwnershipRule("SP005", @"\bRelationshipGraph\s*\.\s*PlayerId\b"),
            new OwnershipRule("SP006", @"\bRestQuorum\s*\.\s*(?:LocalSleeper\b|IsMet\s*\(\s*1\s*\))|\bpublic\s+static\s+int\s+Players\s*=>\s*1\b")
        };

        [Test]
        public void RuntimePlayerState_HasOnlyNamedKnownDebts()
        {
            OwnershipDebt[] debts = ReadOwnershipDebts();
            var expected = new Dictionary<string, OwnershipDebt>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (OwnershipDebt debt in debts)
            {
                Assert.That(names.Add(debt.Id), Is.True, "Duplicate ownership debt name: " + debt.Id);
                Assert.That(debt.Reason, Is.Not.Null.And.Not.Empty, debt.Id + " needs an ownership reason.");
                Assert.That(debt.Count, Is.GreaterThan(0), debt.Id + " must name an existing occurrence.");
                Assert.That(expected.ContainsKey(debt.Key), Is.False, "Duplicate ownership allowance: " + debt.Key);
                expected.Add(debt.Key, debt);
            }

            var actual = new Dictionary<string, int>(StringComparer.Ordinal);
            var failures = new List<string>();
            foreach (string path in Directory.GetFiles(CodeRoot, "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                string relative = path.Replace('\\', '/').Substring(CodeRoot.Length + 1);
                if (!IsOwnershipRuntimePath(relative)) continue;
                foreach (OwnershipHit hit in ScanOwnership(relative, File.ReadAllText(path)))
                {
                    if (hit.Rule == "SP004" && IsPlayerKeyedSaveRows(hit.Token)) continue;
                    actual.TryGetValue(hit.Key, out int count);
                    actual[hit.Key] = ++count;
                    if (!expected.TryGetValue(hit.Key, out OwnershipDebt debt) || count > debt.Count)
                        failures.Add("[PLAYER-STATE-" + hit.Rule + "] " + relative + ":" + hit.Line +
                            " introduces an unreviewed single-player assumption: " + hit.Token);
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [TestCase("public static ICatchHands Hands { get; set; }", "SP001")]
        [TestCase("private static readonly CatchRecordLog Record = new CatchRecordLog();", "SP001")]
        [TestCase("Object.FindAnyObjectByType<ControlSwitcher>()", "SP002")]
        [TestCase("FindObjectOfType < PlayerWalkController > ()", "SP002")]
        [TestCase("FindFirstObjectByType<Example.CarryHands>()", "SP002")]
        [TestCase("[SerializeField] private PlayerWalkController _secondPlayer;", "SP003")]
        [TestCase("private\nCarryHands\n_hands;", "SP003")]
        [TestCase("public PlayerNeedsDto PlayerNeeds = new();", "SP004")]
        [TestCase("public string RestSecondBedId;", "SP004")]
        [TestCase("Record(RelationshipGraph.PlayerId, npc);", "SP005")]
        [TestCase("RestQuorum.IsMet(1)", "SP006")]
        [TestCase("RestQuorum.LocalSleeper", "SP006")]
        [TestCase("public static int Players => 1;", "SP006")]
        public void PlayerStateScan_RecognizesTheAuditedPatterns(string source, string rule)
        {
            OwnershipHit[] hits = ScanOwnership("Core/Save/SaveData.cs", source).ToArray();
            Assert.That(hits.Select(h => h.Rule), Does.Contain(rule));
        }

        [Test]
        public void PlayerStateScan_DoesNotTreatCommentsLiteralsOrExplicitActorInputsAsDebt()
        {
            const string source = "// FindAnyObjectByType<CarryHands>()\n" +
                "/* public static ICatchHands Hands; */\n" +
                "var text = \"FindObjectOfType<PlayerWalkController>()\";\n" +
                "var quoted = @\"private CarryHands _hands;\";\n" +
                "public bool Take(ICarrier actor) => actor != null;\n" +
                "private Dictionary<string, CarryHands> _players;\n" +
                "public static int Count(SaveData data) => 0;";
            Assert.That(ScanOwnership("World/Example.cs", source), Is.Empty);
        }

        [Test]
        public void PlayerStateScan_CountsRepeatedDebtAndKeepsSourceLines()
        {
            const string source = "/* first\nsecond */\nFindAnyObjectByType<CarryHands>();\nFindAnyObjectByType<CarryHands>();";
            OwnershipHit[] hits = ScanOwnership("World/Example.cs", source).ToArray();
            Assert.That(hits.Length, Is.EqualTo(2));
            Assert.That(hits[0].Key, Is.EqualTo(hits[1].Key));
            Assert.That(hits.Select(h => h.Line), Is.EqualTo(new[] { 3, 4 }));
        }

        [Test]
        public void PlayerStateScan_ScansEveryRuntimeModuleButNotEditorCode()
        {
            Assert.That(IsOwnershipRuntimePath("Fishing/NewModule/Example.cs"), Is.True);
            Assert.That(IsOwnershipRuntimePath("Core/Save/SaveData.cs"), Is.True);
            Assert.That(IsOwnershipRuntimePath("App/Editor/Builder.cs"), Is.False);
            Assert.That(IsOwnershipRuntimePath("Tools/Editor/Builder.cs"), Is.False);
            Assert.That(ScanOwnership("World/Example.cs", "public string PlayerName;"), Is.Empty,
                "The flat save-field rule applies only to the save DTO.");
        }

        [Test]
        public void PlayerStateScan_ExemptsOnlySaveRowsThatCarryAPlayerKey()
        {
            Assert.That(IsPlayerKeyedSaveRows("public List<PlayerClothingDto> PlayerClothing"), Is.True);
            Assert.That(IsPlayerKeyedSaveRows("public List<PlayerSkillDto> PlayerSkills"), Is.True);
            Assert.That(IsPlayerKeyedSaveRows("public List<PlayerNoteDto> PlayerNotes"), Is.False);
            Assert.That(IsPlayerKeyedSaveRows("public PlayerNeedsDto PlayerNeeds"), Is.False);
            Assert.That(IsPlayerKeyedSaveRows("public List<string> PlayerTraits"), Is.False);
            Assert.That(IsPlayerKeyedSaveRows("public List<PlayerSkillDto> NoSuchField"), Is.False);
        }

        // A save list whose every row names its player (a public string PlayerKey) is already keyed per
        // player, which is the migration the SP004 debts ask for; the field-name tripwire cannot see that.
        private static bool IsPlayerKeyedSaveRows(string token)
        {
            string name = Regex.Match(token, @"(\w+)\s*$").Groups[1].Value;
            FieldInfo field = typeof(HiddenHarbours.Core.SaveData).GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || !field.FieldType.IsGenericType ||
                field.FieldType.GetGenericTypeDefinition() != typeof(List<>)) return false;
            FieldInfo key = field.FieldType.GetGenericArguments()[0]
                .GetField("PlayerKey", BindingFlags.Public | BindingFlags.Instance);
            return key != null && key.FieldType == typeof(string);
        }

        private static bool IsOwnershipRuntimePath(string relative) =>
            !relative.StartsWith("Tools/", StringComparison.Ordinal) &&
            !relative.Split('/').Contains("Editor");

        private static IEnumerable<OwnershipHit> ScanOwnership(string relative, string source)
        {
            string code = Regex.Replace(source,
                @"//[^\r\n]*|/\*[\s\S]*?\*/|@""(?:""""|[^""])*""|""(?:\\[\s\S]|[^""\\])*""|'(?:\\[\s\S]|[^'\\])*'",
                match => Regex.Replace(match.Value, @"[^\r\n]", " "));
            foreach (OwnershipRule rule in OwnershipRules)
            {
                if (rule.Path != null && rule.Path != relative) continue;
                foreach (Match match in rule.Pattern.Matches(code))
                {
                    int line = 1;
                    for (int i = 0; i < match.Index; i++) if (code[i] == '\n') line++;
                    yield return new OwnershipHit(rule.Id, relative,
                        Regex.Replace(match.Value.Trim(), @"\s+", " "), line);
                }
            }
        }

        private static OwnershipDebt[] ReadOwnershipDebts()
        {
            Assert.That(File.Exists(OwnershipDebtsPath), Is.True, "The named ownership debt list must exist.");
            OwnershipDebtFile file = JsonUtility.FromJson<OwnershipDebtFile>(File.ReadAllText(OwnershipDebtsPath));
            Assert.That(file?.Rows, Is.Not.Null.And.Not.Empty, "The ownership audit must not silently scan zero debts.");
            return file.Rows;
        }

        private sealed class OwnershipRule
        {
            public readonly string Id, Path;
            public readonly Regex Pattern;
            public OwnershipRule(string id, string pattern, string path = null)
            {
                Id = id;
                Path = path;
                Pattern = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            }
        }

        private readonly struct OwnershipHit
        {
            public readonly string Rule, Path, Token;
            public readonly int Line;
            public string Key => Rule + "|" + Path + "|" + Token;
            public OwnershipHit(string rule, string path, string token, int line)
            { Rule = rule; Path = path; Token = token; Line = line; }
        }

#pragma warning disable 0649
        [Serializable] private sealed class OwnershipDebtFile { public OwnershipDebt[] Rows; }
        [Serializable] private sealed class OwnershipDebt
        {
            public string Id, Rule, Path, Token, Reason;
            public int Count;
            public string Key => Rule + "|" + Path + "|" + Token;
        }
#pragma warning restore 0649
    }
}
