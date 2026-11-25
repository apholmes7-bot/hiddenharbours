using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The demo ends at her word, and only the demo.</b> In the demo configuration the word that
    /// ends the island week writes the ending into the save's existing flag list and raises the
    /// ending once; opened again later, the game still reads as ended and nothing is replayed. In the
    /// full game the same word ends nothing. A guest raises the ending and writes nothing: the save is
    /// the host's.
    /// </summary>
    public class DemoWeekTests
    {
        private const string GatePath = "Assets/_Project/Data/Resources/StoryGates/StPetersWeather.asset";
        private const string RulesPath = "Assets/_Project/Data/Resources/IslandWeek.asset";

        private StoryGateDef _gate;
        private IslandWeekRulesDef _rules;
        private StoryGateTests.FlagSave _save;
        private StoryGateTests.FixedClock _clock;
        private SaveFlagStore _flags;
        private int _ended;
        private int _words;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _gate = AssetDatabase.LoadAssetAtPath<StoryGateDef>(GatePath);
            _rules = AssetDatabase.LoadAssetAtPath<IslandWeekRulesDef>(RulesPath);
            _save = new StoryGateTests.FlagSave
            {
                Current = StoryGateTests.WeekSave(_gate, _rules, IslandWeekFact.All),
            };
            GameServices.Save = _save;
            GameServices.IslandWeek = new IslandWeekProgress(_rules);
            _clock = new StoryGateTests.FixedClock { Day = _rules.EarliestDay - 1 };
            GameServices.Clock = _clock;
            _flags = new SaveFlagStore();
            StoryGates.Load(_gate);
            DemoBuild.Override = true;
            DemoWeek.IsHost = null;
            DemoWeek.Listen();
            _ended = 0;
            _words = 0;
            EventBus.Subscribe<DemoEnded>(CountEnded);
            EventBus.Subscribe<StoryWordGiven>(CountWord);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DemoEnded>(CountEnded);
            EventBus.Unsubscribe<StoryWordGiven>(CountWord);
            StoryGates.Load(null);
            DemoBuild.Override = null;
            DemoWeek.IsHost = null;
            GameServices.Reset();
        }

        private void CountEnded(DemoEnded e) => _ended++;
        private void CountWord(StoryWordGiven e) => _words++;

        [Test]
        public void InTheDemo_SheGivesTheDemosOwnWord()
        {
            StoryGateTalk talk = StoryGates.TalkWith(_gate.NpcId, _flags);
            Assert.AreEqual(StoryGateTalkKind.Word, talk.Kind);
            CollectionAssert.AreEqual(_gate.DemoWord.FirstLines, talk.Lines);
            foreach (string line in talk.Lines)
                StringAssert.DoesNotContain("Nine Mile Creek", line, "the demo's word promises no crossing");
        }

        [Test]
        public void InTheDemo_HerWordEndsIt_Once_AndTheEndingIsInTheSave()
        {
            Assert.IsFalse(DemoWeek.Ended, "not before the word");

            Assert.IsTrue(StoryGates.Give(_gate, _flags));
            Assert.AreEqual(1, _words);
            Assert.AreEqual(1, _ended, "the ending is raised once");
            Assert.IsTrue(DemoWeek.Ended);
            Assert.AreEqual(1, _save.Writes[_gate.SetsFlag], "the word, written once");
            Assert.AreEqual(1, _save.Writes[DemoWeek.EndedKey], "the ending, written once");

            // Asked again, raised again: nothing more.
            Assert.IsFalse(StoryGates.Give(_gate, _flags));
            Assert.IsFalse(DemoWeek.OnWord(new StoryWordGiven(_gate.Id, _gate.SetsFlag, true)));
            EventBus.Publish(new StoryWordGiven(_gate.Id, _gate.SetsFlag, true));
            Assert.AreEqual(1, _ended, "never twice");
            Assert.AreEqual(1, _save.Writes[DemoWeek.EndedKey]);
        }

        [Test]
        public void WithAFactMissing_OrBeforeEvening_NoWord_AndNothingEnds()
        {
            _clock.Hour = 12f;
            Assert.IsFalse(StoryGates.Give(_gate, _flags), "noon");
            _clock.Hour = 19f;
            _save.Current = StoryGateTests.WeekSave(_gate, _rules, IslandWeekFact.All & ~IslandWeekFact.ShellfishSold);
            Assert.IsFalse(StoryGates.Give(_gate, _flags), "no shellfish sold");

            Assert.AreEqual(0, _words);
            Assert.AreEqual(0, _ended);
            Assert.IsFalse(DemoWeek.Ended);
            Assert.IsEmpty(_save.Writes);
        }

        [Test]
        public void OpenedAgainAfterwards_TheWordStaysGiven_TheDemoReadsEnded_AndNothingReplays()
        {
            Assert.IsTrue(StoryGates.Give(_gate, _flags));
            string json = JsonUtility.ToJson(_save.Current);

            // A new launch: fresh services, the same file.
            GameServices.Reset();
            var relaunched = new StoryGateTests.FlagSave
            {
                Current = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(json)),
            };
            GameServices.Save = relaunched;
            GameServices.IslandWeek = new IslandWeekProgress(_rules);
            GameServices.Clock = new StoryGateTests.FixedClock { Day = _rules.EarliestDay + 3 };
            var flags = new SaveFlagStore();
            _ended = 0;
            _words = 0;

            Assert.IsTrue(relaunched.GetFlag(_gate.SetsFlag), "the word stays given");
            Assert.IsTrue(DemoWeek.Ended, "the demo reads as ended");

            StoryGateTalk talk = StoryGates.TalkWith(_gate.NpcId, flags);
            Assert.AreEqual(StoryGateTalkKind.Hint, talk.Kind, "she has one line more, and no second word");
            CollectionAssert.AreEqual(new[] { _gate.DemoAfterLine }, talk.Lines);
            Assert.IsFalse(StoryGates.Give(_gate, flags));
            Assert.IsNull(StoryGates.TaskNote(flags));

            Assert.AreEqual(0, _words, "no word is raised by loading or talking");
            Assert.AreEqual(0, _ended, "and the ending is not replayed");
            Assert.IsEmpty(relaunched.Writes, "nothing is written");
        }

        [Test]
        public void InTheFullGame_HerWordIsGiven_AndNothingEnds()
        {
            DemoBuild.Override = false;

            StoryGateTalk talk = StoryGates.TalkWith(_gate.NpcId, _flags);
            CollectionAssert.AreEqual(_gate.Word.FirstLines, talk.Lines, "the full game's own word");

            Assert.IsTrue(StoryGates.Give(_gate, _flags));
            Assert.AreEqual(1, _words, "the word is given");
            Assert.AreEqual(0, _ended, "and nothing ends");
            Assert.IsFalse(DemoWeek.Ended);
            Assert.IsFalse(_save.Writes.ContainsKey(DemoWeek.EndedKey), "the full game never writes the demo's key");
            StoryGateTalk after = StoryGates.TalkWith(_gate.NpcId, _flags);
            Assert.AreEqual(StoryGateTalkKind.Hint, after.Kind, "the full game waits for a morning sleep");
            CollectionAssert.AreEqual(_gate.PendingDialogue.FirstLines, after.Lines);

            // Even a save that somehow carries the demo's key is not an ended game outside the demo.
            _save.SetFlag(DemoWeek.EndedKey, true);
            Assert.IsFalse(DemoWeek.Ended);
        }

        [Test]
        public void AWordThatEndsNoDemo_EndsNothing_EvenInTheDemo()
        {
            Assert.IsFalse(DemoWeek.OnWord(new StoryWordGiven("story_gate.some_other", "some.flag", false)));
            Assert.AreEqual(0, _ended);
            Assert.IsEmpty(_save.Writes);
        }

        [Test]
        public void AGuest_IsShownTheEnding_AndWritesNothing_TheSaveIsTheHosts()
        {
            DemoWeek.IsHost = () => false;
            Assert.IsTrue(DemoWeek.OnWord(new StoryWordGiven(_gate.Id, _gate.SetsFlag, true)));
            Assert.AreEqual(1, _ended, "the demo ends for everyone");
            Assert.IsEmpty(_save.Writes, "only the host writes");

            DemoWeek.IsHost = () => true;
            Assert.IsTrue(DemoWeek.OnWord(new StoryWordGiven(_gate.Id, _gate.SetsFlag, true)));
            Assert.AreEqual(1, _save.Writes[DemoWeek.EndedKey]);
        }

        [Test]
        public void TheEndingIsAFlagInTheListTheSaveAlreadyHas()
        {
            int flagsBefore = _save.Current.OnboardingFlags.Count;
            Assert.IsTrue(StoryGates.Give(_gate, _flags));
            Assert.AreEqual(flagsBefore + 2, _save.Current.OnboardingFlags.Count, "two keys, nothing else");
            Assert.AreEqual(SaveMigration.CurrentVersion, _save.Current.SchemaVersion);
            CollectionAssert.IsSubsetOf(new[] { _gate.SetsFlag, DemoWeek.EndedKey },
                _save.Current.OnboardingFlags.Where(f => f.Value).Select(f => f.Key).ToArray());
        }

        [Test]
        public void TheClosingCardsWords_AreShort_AndNameNoTitle()
        {
            string[] lines =
            {
                ShellStrings.DemoEndTitle, ShellStrings.DemoEndLine1, ShellStrings.DemoEndLine2,
                ShellStrings.DemoEndLeave, ShellStrings.DemoEndSaved,
            };
            foreach (string line in lines)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(line));
                Assert.LessOrEqual(line.Length, 80, line);
                StringAssert.DoesNotContain(ShellStrings.GameTitle, line);
            }
        }
    }
}
