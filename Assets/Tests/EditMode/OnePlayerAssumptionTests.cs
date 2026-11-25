using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class OnePlayerAssumptionTests
    {
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const string Remedy = "Pass the acting player in through Core instead of selecting one global player. " +
            "For an existing assumption removed from code, delete only its baseline line. " +
            "A new shared service needs an explicit ownership decision, not a larger allowance.";
        private Dictionary<string, int> _actual;
        private List<OnePlayerAssumptionCensus.Member> _members;

        [OneTimeSetUp]
        public void ReadRuntimeSourcesOnce()
        {
            Assert.That(Directory.Exists(OnePlayerAssumptionCensus.Root), Is.True);
            _actual = new Dictionary<string, int>(StringComparer.Ordinal);
            _members = new List<OnePlayerAssumptionCensus.Member>();
            var buffer = new OnePlayerAssumptionCensus.Buffer();
            foreach (string file in Directory.EnumerateFiles(OnePlayerAssumptionCensus.Root, "*.cs", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/').Substring(OnePlayerAssumptionCensus.Root.Length);
                if (OnePlayerAssumptionCensus.RuntimePath(path))
                    _members.AddRange(OnePlayerAssumptionCensus.Scan(path, File.ReadAllText(file), _actual, buffer).Where(m => m.Static));
            }
            Assert.That(_members.Any(m => m.Owner == "GameServices" && m.Name == "Hands"), Is.True,
                "The runtime census must include the Core locator, even in a headless run.");
        }

        [Test]
        public void SourceCensus_HasExactlyTheNamedFileAndMemberAllowances()
        {
            var expected = ReadBaseline();
            Assert.That(Differences(expected, _actual), Is.Empty, Remedy);
            TestContext.WriteLine("Census: " + _actual.Count + " file/member findings; " +
                _actual.Values.Sum() + " occurrences. No scene or service was instantiated.");
        }

        [Test]
        public void ReflectedStaticState_IsCoveredByTheSourceCensus()
        {
            var failures = new List<string>();
            var reflected = new HashSet<string>(StringComparer.Ordinal);
            foreach (string definition in Directory.EnumerateFiles(OnePlayerAssumptionCensus.Root, "*.asmdef", SearchOption.AllDirectories))
            {
                string relative = definition.Replace('\\', '/').Substring(OnePlayerAssumptionCensus.Root.Length);
                if (!OnePlayerAssumptionCensus.RuntimePath(Path.ChangeExtension(relative, ".cs"))) continue;
                Assembly assembly = Assembly.Load(JsonUtility.FromJson<AssemblyNameRow>(File.ReadAllText(definition)).name);
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;
                    foreach (FieldInfo field in type.GetFields(StaticMembers))
                        if (!field.IsLiteral && !field.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
                            ReflectedState(type, field.FieldType, field.Name, true))
                            CheckReflected(type, field.Name, "static", reflected, failures);
                    foreach (PropertyInfo property in type.GetProperties(StaticMembers))
                    {
                        bool stored = property.GetSetMethod(true) != null ||
                            type.GetField("<" + property.Name + ">k__BackingField", StaticMembers) != null;
                        if (ReflectedState(type, property.PropertyType, property.Name, stored))
                            CheckReflected(type, property.Name, "static", reflected, failures);
                    }
                    foreach (MethodInfo method in type.GetMethods(StaticMembers))
                        if (!method.IsSpecialName && method.GetParameters().Length == 0 && PersonalType(method.ReturnType))
                            CheckReflected(type, method.Name, "accessor", reflected, failures);
                }
            }
            Assert.That(reflected, Does.Contain("static|Core/Services/GameServices.cs|GameServices.Hands"));
            Assert.That(reflected, Does.Contain("static|Core/Services/GameServices.cs|GameServices.Wallet"));
            Assert.That(failures, Is.Empty, string.Join("\n", failures) + "\n" + Remedy);
            TestContext.WriteLine("Reflection: " + reflected.Count + " static members checked without invoking getters.");
        }

        [Test]
        public void Document_IsTheOnlyBaselineAndNamesAllOwnershipOutcomes()
        {
            string text = File.ReadAllText(OnePlayerAssumptionCensus.Baseline);
            Assert.That(text, Does.Contain(" -- PER PLAYER -- ").And.Contain(" -- SHARED -- ").And.Contain(" -- UNSURE -- "));
            Assert.That(ReadBaseline(), Is.Not.Empty);
        }

        private void CheckReflected(Type type, string name, string rule, HashSet<string> reflected, List<string> failures)
        {
            string owner = Regex.Replace(type.FullName, @"`\d+", "");
            var declarations = _members.Where(m => m.FullOwner == owner && m.Name == name).ToArray();
            if (declarations.Length == 0)
            {
                failures.Add("Unmapped static member " + type.FullName + "." + name + "; include its source declaration.");
                return;
            }
            foreach (var declaration in declarations)
            {
                string key = rule + "|" + declaration.Key;
                reflected.Add(key);
                if (!_actual.ContainsKey(key)) failures.Add("Reflection found an uncounted source member: " + key);
            }
        }

        private static bool PersonalType(Type type)
        {
            if (type.IsArray || type.IsGenericType) return false;
            if (OnePlayerAssumptionCensus.Personal(type.Name)) return true;
            foreach (Type contract in type.GetInterfaces())
                if (OnePlayerAssumptionCensus.Personal(contract.Name)) return true;
            return type.BaseType != null && type.BaseType != typeof(object) && PersonalType(type.BaseType);
        }

        private static bool ReflectedState(Type owner, Type type, string member, bool stored) =>
            OnePlayerAssumptionCensus.StaticState(owner.Name, type.Name, member, stored) || PersonalType(type);

        private static Dictionary<string, int> ReadBaseline()
        {
            var expected = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in File.ReadLines(OnePlayerAssumptionCensus.Baseline))
            {
                if (!line.StartsWith("- `", StringComparison.Ordinal)) continue;
                int end = line.IndexOf('`', 3);
                Assert.That(end, Is.GreaterThan(3), "Close the baseline key's backtick.");
                string key = line.Substring(3, end - 3);
                if (!key.Contains("|")) continue;
                Assert.That(line.Substring(end + 1), Does.StartWith(" ("), key + " needs an exact occurrence count.");
                int close = line.IndexOf(')', end + 3);
                Assert.That(close, Is.GreaterThan(end + 3), key);
                int count = int.Parse(line.Substring(end + 3, close - end - 3), System.Globalization.CultureInfo.InvariantCulture);
                string explanation = line.Substring(close + 1);
                Assert.That(explanation.StartsWith(" -- PER PLAYER -- ", StringComparison.Ordinal) ||
                    explanation.StartsWith(" -- SHARED -- ", StringComparison.Ordinal) ||
                    explanation.StartsWith(" -- UNSURE -- ", StringComparison.Ordinal), Is.True,
                    key + " needs an ownership classification followed by its smallest change.");
                Assert.That(explanation.Substring(explanation.LastIndexOf(" -- ", StringComparison.Ordinal) + 4).Trim(),
                    Is.Not.Empty, key + " needs its smallest change.");
                Assert.That(count, Is.GreaterThan(0), key);
                Assert.That(expected.ContainsKey(key), Is.False, "Duplicate allowance: " + key);
                expected.Add(key, count);
            }
            Assert.That(expected, Is.Not.Empty);
            return expected;
        }

        private static string[] Differences(Dictionary<string, int> expected, Dictionary<string, int> actual)
        {
            var differences = new List<string>();
            foreach (var hit in actual)
                if (!expected.TryGetValue(hit.Key, out int count) || count != hit.Value)
                    differences.Add(hit.Key + ": found " + hit.Value + ", allowed " + count);
            foreach (var old in expected)
                if (!actual.ContainsKey(old.Key)) differences.Add("Remove resolved allowance: " + old.Key);
            differences.Sort(StringComparer.Ordinal);
            return differences.ToArray();
        }

        [TestCase("public static CarryHands Actor;", "static|World/Example.cs|Example.Actor")]
        [TestCase("public static CarryHands Actor => null;", "static|World/Example.cs|Example.Actor")]
        [TestCase("public static CarryHands Actor() => null;", "accessor|World/Example.cs|Example.Actor")]
        [TestCase("static Example Instance;", "static|World/Example.cs|Example.Instance")]
        [TestCase("static Example s_instance;", "static|World/Example.cs|Example.s_instance")]
        [TestCase("public static UnityEngine.Transform PlayerBody;", "static|World/Example.cs|Example.PlayerBody")]
        [TestCase("void Act() { Object.FindAnyObjectByType<CarryHands>(); }", "lookup|World/Example.cs|Example.Act|FindAnyObjectByType<CarryHands>")]
        [TestCase("void Act() { FindFirstObjectByType < A.PlayerWalkController > (); }", "lookup|World/Example.cs|Example.Act|FindFirstObjectByType<PlayerWalkController>")]
        [TestCase("void Act() { FindObjectOfType(typeof(PlayerWallet)); }", "lookup|World/Example.cs|Example.Act|FindObjectOfType<PlayerWallet>")]
        [TestCase("void Act() { FindObjectsByType<BoatController>()[0]; }", "lookup|World/Example.cs|Example.Act|FindObjectsByType<BoatController>")]
        [TestCase("object Actor => FindObjectOfType<CarryHands>();", "lookup|World/Example.cs|Example.Actor|FindObjectOfType<CarryHands>")]
        [TestCase("object _actor = FindObjectOfType<CarryHands>();", "lookup|World/Example.cs|Example._actor|FindObjectOfType<CarryHands>")]
        [TestCase("void Act() { GameObject.Find(\"Player\"); }", "lookup|World/Example.cs|Example.Act|Find")]
        [TestCase("void Act() { GameObject.FindWithTag(_actorTag); }", "lookup|World/Example.cs|Example.Act|FindWithTag")]
        [TestCase("void Act() { Use(RestQuorum.LocalSleeper); }", "identity-use|World/Example.cs|Example.Act|RestQuorum.LocalSleeper")]
        [TestCase("void Act() { Use(RelationshipGraph.PlayerId); }", "identity-use|World/Example.cs|Example.Act|RelationshipGraph.PlayerId")]
        [TestCase("void Act() { var s = $\"actor {FindAnyObjectByType<CarryHands>()}\"; }", "lookup|World/Example.cs|Example.Act|FindAnyObjectByType<CarryHands>")]
        [TestCase("void Act() { Use(GameServices.Hands); }", "service-use|World/Example.cs|Example.Act|GameServices.Hands")]
        [TestCase("void Act() { FindObjectOfType(actorType); }", "lookup|World/Example.cs|Example.Act|FindObjectOfType<dynamic>")]
        public void SourceScan_NamesTheOwningMember(string body, string expected)
        {
            var hits = Scan("class Example { " + body + " }");
            Assert.That(hits.Keys, Is.EquivalentTo(new[] { expected }));
            Assert.That(hits[expected], Is.EqualTo(1));
        }

        [Test]
        public void SourceScan_NewLocatorTypeCannotHideBehindAnUnfamiliarName()
        {
            var hits = Scan("static class GameServices { public static UnfamiliarState Context { get; set; } }");
            Assert.That(hits.Keys, Is.EquivalentTo(new[] { "static|World/Example.cs|GameServices.Context" }));
        }

        [Test]
        public void SourceScan_TupleGenericFieldIsStoredStateAndAliasesAreResolved()
        {
            var hits = Scan("using S = Example.GameServices; using H = Example.CarryHands; " +
                "class GameServices { static Dictionary<string, (object First, object Second)> Rows = new(); } " +
                "class Example { static H Actor; void Act() { S.Hands.Take(); FindAnyObjectByType<H>(); } }");
            Assert.That(hits.Keys, Is.EquivalentTo(new[] {
                "static|World/Example.cs|GameServices.Rows", "static|World/Example.cs|Example.Actor",
                "service-use|World/Example.cs|Example.Act|GameServices.Hands",
                "lookup|World/Example.cs|Example.Act|FindAnyObjectByType<CarryHands>" }));
        }

        [Test]
        public void SourceScan_ResolvesStaticImportsAndQualifiedNestedOwners()
        {
            var hits = new Dictionary<string, int>();
            var members = OnePlayerAssumptionCensus.Scan("World/Example.cs",
                "using static A.GameServices; namespace A { class Example { class Nested { " +
                "static CarryHands Actor; void Act() { Hands.Take(); } } } } namespace B { class Example {} }", hits);
            Assert.That(hits.Keys, Is.EquivalentTo(new[] {
                "static|World/Example.cs|Nested.Actor", "service-use|World/Example.cs|Nested.Act|GameServices.Hands" }));
            Assert.That(members.Single(m => m.Name == "Actor").FullOwner, Is.EqualTo("A.Example+Nested"));
        }

        [Test]
        public void ReflectedClassification_FollowsInterfacesWithoutReadingTheInstance()
        {
            Assert.That(PersonalType(typeof(OpaqueActorState)), Is.True);
            Assert.That(PersonalType(typeof(Dictionary<string, OpaqueActorState>)), Is.False);
        }

        private interface ICarrier { }
        private sealed class OpaqueActorState : ICarrier { }

        [Test]
        public void SourceScan_IgnoresCommentsTextAndExplicitActorInputs()
        {
            const string source = "class Example { // FindObjectOfType<CarryHands>()\n" +
                "/* static CarryHands Actor; */ string s = @\"FindObjectOfType<CarryHands>()\"; " +
                "string escaped = \"\\\"FindWithTag(\\\"Player\\\")\"; " +
                "CarryHands _ownHands; Dictionary<string, CarryHands> _roster; " +
                "public static void Act(CarryHands actor) {} }";
            Assert.That(Scan(source), Is.Empty);
        }

        [Test]
        public void Ratchet_RejectsDuplicateAndMovedLookupsAndAllowsOnlyExplicitDeletion()
        {
            const string call = "FindAnyObjectByType<CarryHands>();";
            var original = Scan("class Example { void Act() { " + call + " } }");
            var duplicate = Scan("class Example { void Act() { " + call + call + " } }");
            var moved = Scan("class Example { void Other() { " + call + " } }");
            Assert.That(Differences(original, duplicate).Length, Is.EqualTo(1));
            Assert.That(Differences(original, moved).Length, Is.EqualTo(2));
            Assert.That(Differences(original, Scan("class Example {} ")).Length, Is.EqualTo(1));
            original.Clear();
            Assert.That(Differences(original, Scan("class Example {}")), Is.Empty);
        }

        [Test]
        public void SourceScan_KeepsInactiveBranchesAndNestedMemberOwnership()
        {
            var hits = Scan("class Example {\n#if ANOTHER_BUILD\nstatic CarryHands Actor;\n#endif\n" +
                "class Nested { void Act() { FindAnyObjectByType<CarryHands>(); } } }");
            Assert.That(hits.Keys, Is.EquivalentTo(new[] {
                "static|World/Example.cs|Example.Actor",
                "lookup|World/Example.cs|Nested.Act|FindAnyObjectByType<CarryHands>" }));
        }

        [TestCase("NewFeature/Actor.cs", true)]
        [TestCase("Art/PlayerOutfitVisual.cs", true)]
        [TestCase("Audio/ActorVoice.cs", true)]
        [TestCase("Core/Save/SaveData.cs", true)]
        [TestCase("App/Editor/Builder.cs", false)]
        [TestCase("Tools/Editor/Builder.cs", false)]
        public void Scope_IncludesEveryRuntimeFolder(string path, bool expected) =>
            Assert.That(OnePlayerAssumptionCensus.RuntimePath(path), Is.EqualTo(expected));

        private static Dictionary<string, int> Scan(string source)
        {
            var hits = new Dictionary<string, int>(StringComparer.Ordinal);
            OnePlayerAssumptionCensus.Scan("World/Example.cs", source, hits);
            return hits;
        }

        [Serializable] private sealed class AssemblyNameRow { public string name = ""; }
    }
}
