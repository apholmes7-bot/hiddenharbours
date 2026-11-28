using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.World;
using Field = HiddenHarbours.Tests.EditMode.FirstWeekTextGuard.TextField;
using Place = HiddenHarbours.Tests.EditMode.FirstWeekTextGuard.Place;
using ExceptionLine = HiddenHarbours.Tests.EditMode.FirstWeekTextGuard.ExceptionLine;

namespace HiddenHarbours.Tests.EditMode
{
    public class FirstWeekTextGuardTests
    {
        static FirstWeekTextGuard Guard() => new FirstWeekTextGuard(new[] { new Place("region.outer_haven", "Outer Haven") });
        static Field Words(string text) => new Field("fixture.asset", "FirstLines[0]", text);
        static ExceptionLine Exception(Field field) => new ExceptionLine
        {
            File = field.File, Field = field.Field, Text = field.Text, Reason = "Old direction awaiting replacement."
        };

        [TestCase("Go to Outer Haven.", "Outer Haven")]
        [TestCase("Go to OUTER HAVEN!", "OUTER HAVEN")]
        [TestCase("Outer\n  Haven's harbour.", "Outer\n  Haven")]
        [TestCase("Visit (region.outer_haven).", "region.outer_haven")]
        public void OutsideNamesAndIds_ReportTheExactFileFieldAndMention(string text, string matched)
        {
            var mentions = Guard().Scan(new[] { Words(text) });
            Assert.That(mentions.Count, Is.EqualTo(1));
            Assert.That(mentions[0].Source.File, Is.EqualTo("fixture.asset"));
            Assert.That(mentions[0].Source.Field, Is.EqualTo("FirstLines[0]"));
            Assert.That(mentions[0].PlaceId, Is.EqualTo("region.outer_haven"));
            Assert.That(mentions[0].Matched, Is.EqualTo(matched));
            Assert.That(mentions[0].Offset, Is.EqualTo(text.IndexOf(matched, StringComparison.Ordinal)));
        }

        [TestCase("Outer Havenwood")]
        [TestCase("MyOuter Haven")]
        [TestCase("Outer Haven_2")]
        [TestCase("Outer Havené")]
        [TestCase("Outer Haven\u0301")]
        [TestCase("region.outer_haven_extra")]
        [TestCase("region.outer_haven.child")]
        public void NamesInsideOtherWordsAndIds_AreNotMentions(string text)
            => Assert.That(Guard().Scan(new[] { Words(text) }), Is.Empty);

        [Test]
        public void EveryMentionInOneFieldIsReported_AndOrderingIsStable()
        {
            var first = Words("Outer Haven, then Outer Haven again.");
            var second = new Field("other.asset", "Body[0]", "region.outer_haven");
            var guard = Guard();
            var forward = guard.Scan(new[] { first, second });
            var reverse = guard.Scan(new[] { second, first });
            Assert.That(forward.Count, Is.EqualTo(3));
            CollectionAssert.AreEqual(forward.Select(m => m.ToString()), reverse.Select(m => m.ToString()));
            Assert.That(forward[1].Offset, Is.GreaterThan(forward[0].Offset));
        }

        [Test]
        public void RemovingAStillNeededExceptionFails()
        {
            var field = Words("Go to Outer Haven.");
            var guard = Guard();
            var mentions = guard.Scan(new[] { field });
            Assert.That(guard.Problems(mentions, new[] { Exception(field) }), Is.Empty);
            var problems = guard.Problems(mentions, Array.Empty<ExceptionLine>());
            Assert.That(problems.Count, Is.EqualTo(1));
            StringAssert.Contains("Unexcepted: fixture.asset :: FirstLines[0]", problems[0]);
        }

        [Test]
        public void CorrectedOrDeletedLineMakesItsExceptionStale()
        {
            var old = Exception(Words("Go to Outer Haven."));
            var guard = Guard();
            foreach (var fields in new[] { new[] { Words("Stay on the island.") }, Array.Empty<Field>() })
            {
                var problems = guard.Problems(guard.Scan(fields), new[] { old });
                Assert.That(problems.Count, Is.EqualTo(1));
                StringAssert.StartsWith("Stale exception", problems[0]);
            }
        }

        [Test]
        public void AnExceptionCannotCoverChangedWordsOrAnotherField()
        {
            var old = Exception(Words("Go to Outer Haven."));
            var guard = Guard();
            foreach (var changed in new[] { Words("Go to Outer Haven today."),
                new Field("fixture.asset", "FirstLines[1]", old.Text), new Field("other.asset", old.Field, old.Text) })
            {
                var problems = guard.Problems(guard.Scan(new[] { changed }), new[] { old });
                Assert.That(problems.Count, Is.EqualTo(2));
                Assert.That(problems.Count(p => p.StartsWith("Unexcepted:", StringComparison.Ordinal)), Is.EqualTo(1));
                Assert.That(problems.Count(p => p.StartsWith("Stale exception", StringComparison.Ordinal)), Is.EqualTo(1));
            }
        }

        [Test]
        public void DuplicatedOrUnexplainedExceptionsFail()
        {
            var field = Words("Go to Outer Haven.");
            var guard = Guard();
            var mentions = guard.Scan(new[] { field });
            var line = Exception(field);
            var duplicate = guard.Problems(mentions, new[] { line, line });
            Assert.That(duplicate.Count, Is.EqualTo(1));
            StringAssert.StartsWith("Duplicate exception", duplicate[0]);
            line.Reason = " ";
            Assert.That(guard.Problems(mentions, new[] { line }).Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateFieldAddressesAreRefused()
            => Assert.Throws<ArgumentException>(() => Guard().Scan(new[] { Words("one"), Words("two") }));

        [Test]
        public void DisplayNameQualifiersAndCurlyApostrophesDoNotHideAPlace()
        {
            var guard = new FirstWeekTextGuard(new[] { new Place("region.test", "The Skipper's Reach (working name)") });
            Assert.That(guard.Scan(new[] { Words("Skipper’s Reach") }).Count, Is.EqualTo(1));
            Assert.That(guard.Scan(new[] { Words("The Skipper's Reach (working name)") }).Count, Is.EqualTo(1));
        }

        [TestCase("FirstLines[0]")]
        [TestCase("Options[2].ReplyLines[0]")]
        [TestCase("Options[2].NothingToSellLines[0]")]
        [TestCase("Notes[0].TaskNote")]
        [TestCase("Notes[0].SpokenHint")]
        [TestCase("OutsideHoursLine")]
        [TestCase("DemoAfterLine")]
        [TestCase("Body[0]")]
        [TestCase("Groups[0].Entries[0].Name")]
        [TestCase("Gifts.Lines.Neutral[0]")]
        [TestCase("Flavor")]
        [TestCase("_arrivalText")]
        [TestCase("m_text")]
        public void EveryProseSurfaceIsSelected(string field) => Assert.That(FirstWeekTextCorpus.IsTextField(field), Is.True);

        [TestCase("Id")]
        [TestCase("RegionId")]
        [TestCase("ConditionalFlag")]
        [TestCase("Options[0].CatalogSellerId")]
        [TestCase("m_Name")]
        [TestCase("Description")]
        public void AuthoringMetadataIsNotPlayerText(string field) => Assert.That(FirstWeekTextCorpus.IsTextField(field), Is.False);

        [Test]
        public void SceneSignTextAndPrefabOverridesAreDecodedAndAddressedWithoutOpeningAScene()
        {
            string yaml = "--- !u!114 &41\nMonoBehaviour:\n  m_Name: Outer Haven\n"
                + "  _arrivalText: 'Outer\n    Haven''s quay.'\n  _label: \"Outer\\u0020Haven\"\n"
                + "  FirstLines:\n  - 'Outer Haven'\n  - 'Stay here'\n"
                + "--- !u!1001 &42\nPrefabInstance:\n    - target: {fileID: 7}\n"
                + "      propertyPath: m_text\n      value: Outer Haven\n      objectReference: {fileID: 0}\n";
            var fields = FirstWeekTextCorpus.SceneText("fixture.unity", yaml.Split('\n')).ToArray();
            Assert.That(fields.Length, Is.EqualTo(5));
            Assert.That(fields[0].Field, Is.EqualTo("&41._arrivalText#0"));
            Assert.That(fields[0].Text, Is.EqualTo("Outer Haven's quay."));
            Assert.That(fields[1].Text, Is.EqualTo("Outer Haven"));
            Assert.That(fields[4].Field, Is.EqualTo("&42.override.m_text#0"));
            Assert.That(Guard().Scan(fields).Count, Is.EqualTo(4));
        }

        [Test]
        public void UnsupportedSceneTextCannotSilentlyDisappear()
            => Assert.Throws<InvalidDataException>(() => FirstWeekTextCorpus.SceneText("fixture.unity",
                new[] { "--- !u!114 &1", "MonoBehaviour:", "  _text: |", "    Outer Haven" }).ToArray());

        [Test]
        public void SerializedDialogueIncludesConditionalOptionsRepliesAndEmptySaleReplies()
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueDef>();
            try
            {
                dialogue.Id = "region.outer_haven";
                dialogue.FirstLines = new[] { "Outer Haven" };
                dialogue.RepeatLines = new[] { "Outer Haven" };
                dialogue.ConditionalFlag = "region.outer_haven";
                dialogue.ConditionalLines = new[] { "Outer Haven" };
                dialogue.Options = new[] { new DialogueOption { Label = "Outer Haven", ReplyLines = new[] { "Outer Haven" },
                    NothingToSellLines = new[] { "Outer Haven" }, CatalogSellerId = "region.outer_haven" } };
                var fields = FirstWeekTextCorpus.AssetText(dialogue, "fixture.asset").ToArray();
                var mentions = Guard().Scan(fields);
                CollectionAssert.AreEquivalent(new[] { "FirstLines[0]", "RepeatLines[0]", "ConditionalLines[0]",
                    "Options[0].Label", "Options[0].ReplyLines[0]", "Options[0].NothingToSellLines[0]" },
                    mentions.Select(m => m.Source.Field));
            }
            finally { UnityEngine.Object.DestroyImmediate(dialogue); }
        }

        [Test]
        public void SerializedNotebookAndItemFlavourReachTheSameGuard()
        {
            var page = ScriptableObject.CreateInstance<KnowledgePageDef>();
            var item = ScriptableObject.CreateInstance<HiddenHarbours.Player.ToolDef>();
            try
            {
                page.Head = "Outer Haven";
                page.Body.Add("Outer Haven");
                item.Flavor = "Outer Haven";
                var fields = FirstWeekTextCorpus.AssetText(page, "page.asset")
                    .Concat(FirstWeekTextCorpus.AssetText(item, "item.asset"));
                CollectionAssert.AreEquivalent(new[] { "page.asset :: Head", "page.asset :: Body[0]", "item.asset :: Flavor" },
                    Guard().Scan(fields).Select(m => m.Source.Key));
            }
            finally { UnityEngine.Object.DestroyImmediate(page); UnityEngine.Object.DestroyImmediate(item); }
        }

        [Test]
        public void ShippedExceptionsAreExplicitAndCanOnlyShrink()
        {
            // The initial audit found eleven authored lines. This ceiling must fall as those lines are corrected.
            const int InitialOffendingLineCount = 11;
            var asset = JsonUtility.FromJson<FirstWeekTextGuard.ExceptionAsset>(File.ReadAllText(FirstWeekTextCorpus.ExceptionsPath));
            Assert.That(asset.Comment, Is.Not.Null.And.Not.Empty);
            Assert.That(asset.Lines.Length, Is.LessThanOrEqualTo(InitialOffendingLineCount));
            Assert.That(asset.Lines.Select(e => e.Key).Distinct().Count(), Is.EqualTo(asset.Lines.Length));
            Assert.That(asset.Lines.All(e => !string.IsNullOrWhiteSpace(e.Reason)), Is.True);
        }

        [Test]
        public void BoundaryFixtureUsesRegionDataAndTheRealFirstScene()
        {
            var bounds = JsonUtility.FromJson<FirstWeekTextCorpus.Bounds>(File.ReadAllText(FirstWeekTextCorpus.BoundsPath));
            Assert.That(EditorBuildSettings.scenes.First(s => s.enabled).path, Is.EqualTo(bounds.BootScenePath));
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");
            Assert.That(Path.GetFileNameWithoutExtension(bounds.BootScenePath), Is.EqualTo(region.SceneName));
        }
    }
}
