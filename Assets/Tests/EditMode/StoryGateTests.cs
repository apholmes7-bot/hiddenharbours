using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The end of the first week on St Peters, as a rule and as shipped data.</b> When the five
    /// facts hold, an ordinary Talk with the gate's islander is the word; the word sets one saved flag;
    /// that flag is what the four St Peters weather walls wait on. Before the facts hold she adds one
    /// line about the first thing still to do, and the Tasks note says the same thing. A save that
    /// never started the island week meets none of it.
    ///
    /// <para>Everything here runs on the shipped assets (the gate, the rules, the walls) and a real
    /// <see cref="SaveData"/> behind a small flag service, so a change to the data is a change to what
    /// these tests read.</para>
    /// </summary>
    public class StoryGateTests
    {
        private const string GatePath = "Assets/_Project/Data/Resources/StoryGates/StPetersWeather.asset";
        private const string RulesPath = "Assets/_Project/Data/Resources/IslandWeek.asset";
        private const string DataRoot = "Assets/_Project/Data";

        /// <summary>The flag service a save file is behind: flags live in the save's own list and nowhere else.</summary>
        private sealed class FlagSave : ISaveService
        {
            public readonly Dictionary<string, int> Writes = new Dictionary<string, int>();
            public SaveData Current { get; set; } = new SaveData();

            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (SaveFlag flag in Current.OnboardingFlags)
                    if (flag.Key == key) return flag.Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                Writes.TryGetValue(key, out int n);
                Writes[key] = n + 1;
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key)
                    {
                        Current.OnboardingFlags[i] = new SaveFlag(key, value);
                        return;
                    }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }

            public void Save() { }
        }

        private StoryGateDef _gate;
        private IslandWeekRulesDef _rules;
        private IslandWeekProgress _progress;
        private FlagSave _save;
        private SaveFlagStore _flags;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _gate = AssetDatabase.LoadAssetAtPath<StoryGateDef>(GatePath);
            _rules = AssetDatabase.LoadAssetAtPath<IslandWeekRulesDef>(RulesPath);
            Assert.IsNotNull(_gate, "the shipped gate asset");
            Assert.IsNotNull(_rules, "the shipped island-week rules");
            _progress = new IslandWeekProgress(_rules);
            _save = new FlagSave();
            GameServices.Save = _save;
            GameServices.IslandWeek = _progress;
            _flags = new SaveFlagStore();
            StoryGates.Load(_gate);
        }

        [TearDown]
        public void TearDown()
        {
            StoryGates.Load(null);
            GameServices.Reset();
        }

        // ---- fixtures ---------------------------------------------------------------------------

        private int EarliestDayIndex => _rules.EarliestDay - 1;

        /// <summary>A save in its island week holding exactly the saved facts of <paramref name="held"/>.</summary>
        private SaveData WeekSave(IslandWeekFact held, bool firstWeek = true)
        {
            var save = new SaveData { SchemaVersion = SaveMigration.CurrentVersion };
            save.RepairedBoats = new List<string>();
            save.OwnedLicenses = new List<string>();
            save.OnboardingFlags = new List<SaveFlag>();
            if (firstWeek) save.OnboardingFlags.Add(new SaveFlag(_gate.LiveWhileFlag, true));
            if ((held & IslandWeekFact.DoryMended) != 0) save.RepairedBoats.Add(_rules.DoryId);
            if ((held & IslandWeekFact.CodLicence) != 0) save.OwnedLicenses.Add(_rules.LicenceId);
            if ((held & IslandWeekFact.FishFromDory) != 0)
                save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.FishFromDoryKey, true));
            if ((held & IslandWeekFact.ShellfishSold) != 0)
                save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.ShellfishSoldKey, true));
            return save;
        }

        private int DayFor(IslandWeekFact held)
            => (held & IslandWeekFact.DayReached) != 0 ? EarliestDayIndex : EarliestDayIndex - 1;

        private StoryGateTalk TalkOn(SaveData save, int dayIndex)
        {
            _save.Current = save;
            IslandWeekSnapshot facts = _progress.Read(save, dayIndex);
            return StoryGate.Talk(_gate, _flags, in facts);
        }

        private static List<CampaignBarrierDef> StPetersWalls()
        {
            var walls = Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder)
                .Where(w => w != null && w.RegionId == "region.st_peters").ToList();
            Assert.AreEqual(4, walls.Count, "one weather wall per sea edge of the island");
            return walls;
        }

        // ---- the rule ---------------------------------------------------------------------------

        [Test]
        public void OfAllThirtyTwoWeeks_OnlyTheOneWhereEveryFactHolds_IsTheWord()
        {
            int words = 0;
            for (int mask = 0; mask < 32; mask++)
            {
                var held = (IslandWeekFact)mask;
                StoryGateTalk talk = TalkOn(WeekSave(held), DayFor(held));
                if (held == IslandWeekFact.All)
                {
                    words++;
                    Assert.AreEqual(StoryGateTalkKind.Word, talk.Kind, "every fact holds");
                    CollectionAssert.AreEqual(_gate.Word.FirstLines, talk.Lines, "the word is the gate's own lines");
                    Assert.AreEqual(_gate.SetsFlag, talk.SetsFlag);
                }
                else
                {
                    Assert.AreNotEqual(StoryGateTalkKind.Word, talk.Kind, $"mask {mask} ({held}) is not the whole week");
                    Assert.IsNull(talk.SetsFlag, $"mask {mask}: nothing is set by an unfinished week");
                }
            }
            Assert.AreEqual(1, words);
        }

        [Test]
        public void BeforeTheFactsHold_SheSpeaksOfTheFirstMissingOne_AndTheNoteSaysTheSame()
        {
            IslandWeekFact[] order =
            {
                IslandWeekFact.DoryMended, IslandWeekFact.CodLicence, IslandWeekFact.FishFromDory,
                IslandWeekFact.ShellfishSold, IslandWeekFact.DayReached,
            };
            string[] keys =
            {
                _rules.DoryMendedTextKey, _rules.CodLicenceTextKey, _rules.FishFromDoryTextKey,
                _rules.ShellfishSoldTextKey, _rules.DayReachedTextKey,
            };

            for (int mask = 0; mask < 31; mask++)
            {
                var held = (IslandWeekFact)mask;
                int first = System.Array.FindIndex(order, f => (held & f) == 0);
                Assert.IsTrue(_gate.TryNote(keys[first], out StoryGateNote note), keys[first]);

                SaveData save = WeekSave(held);
                StoryGateTalk talk = TalkOn(save, DayFor(held));
                Assert.AreEqual(StoryGateTalkKind.Hint, talk.Kind, $"mask {mask}");
                CollectionAssert.AreEqual(new[] { note.SpokenHint }, talk.Lines, $"mask {mask}");

                IslandWeekSnapshot facts = _progress.Read(save, DayFor(held));
                Assert.AreEqual(note.TaskNote, StoryGate.TaskNote(_gate, _flags, in facts), $"mask {mask}");
            }
        }

        [Test]
        public void TheDayIsTheStartOfTheSeventhDay_NotOneDayEarlier()
        {
            IslandWeekFact four = IslandWeekFact.All & ~IslandWeekFact.DayReached;
            Assert.AreEqual(StoryGateTalkKind.Hint, TalkOn(WeekSave(four), EarliestDayIndex - 1).Kind);
            Assert.AreEqual(StoryGateTalkKind.Word, TalkOn(WeekSave(four), EarliestDayIndex).Kind);
            Assert.AreEqual(StoryGateTalkKind.Word, TalkOn(WeekSave(four), EarliestDayIndex + 30).Kind,
                "a talk he missed on the day is still there later");
        }

        [Test]
        public void OnceTheWordIsHeard_TheFlagIsWrittenOnce_SurvivesTheFile_AndTalkIsOrdinaryAgain()
        {
            SaveData save = WeekSave(IslandWeekFact.All);
            StoryGateTalk talk = TalkOn(save, EarliestDayIndex);
            Assert.AreEqual(StoryGateTalkKind.Word, talk.Kind);

            _flags.Set(talk.SetsFlag, true);          // what the talk does when the word is heard out
            Assert.AreEqual(1, _save.Writes[_gate.SetsFlag]);

            // Every later talk is her ordinary talk: there is no second word to set the flag again.
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(StoryGateTalkKind.Normal, TalkOn(save, EarliestDayIndex + i).Kind);
            Assert.AreEqual(1, _save.Writes[_gate.SetsFlag], "set once");
            Assert.IsNull(StoryGates.TaskNote(_flags), "nothing left on the note");

            // Through the file and back: the flag is in the list the save already had, and still set.
            string json = JsonUtility.ToJson(save);
            SaveData back = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(json));
            Assert.AreEqual(SaveMigration.CurrentVersion, back.SchemaVersion);
            _save.Current = back;
            Assert.IsTrue(_save.GetFlag(_gate.SetsFlag), "the flag came back from the file");
            Assert.AreEqual(StoryGateTalkKind.Normal, TalkOn(back, EarliestDayIndex).Kind);
            Assert.AreEqual(save.OnboardingFlags.Count, back.OnboardingFlags.Count, "no flag gained or lost");
            Assert.IsTrue(_progress.Read(back, EarliestDayIndex).AllHold, "and the week's facts came back with it");
        }

        [Test]
        public void TheWord_BringsAllFourStPetersWallsDown()
        {
            _save.Current = WeekSave(IslandWeekFact.All);
            List<CampaignBarrierDef> walls = StPetersWalls();
            foreach (CampaignBarrierDef wall in walls)
            {
                Assert.AreEqual(_gate.SetsFlag, wall.LiftedByFlag, wall.Id + " waits on the gate's flag");
                Assert.AreEqual(_gate.LiveWhileFlag, wall.StandsWhileFlag, wall.Id + " stands in the gate's week");
                Assert.IsTrue(CampaignBarrierDirector.Stands(wall), wall.Id + " stands through the week");
            }

            _flags.Set(TalkOn(_save.Current, EarliestDayIndex).SetsFlag, true);

            foreach (CampaignBarrierDef wall in walls)
                Assert.IsFalse(CampaignBarrierDirector.Stands(wall), wall.Id + " is down the moment the flag is set");
        }

        [Test]
        public void AnOlderSave_ThatNeverStartedTheWeek_HasNoWallsNoWordAndNoNote_AndLosesNothing()
        {
            // Every fact a long-played save could hold, and no first-week key: the save any player had
            // before the island week existed.
            SaveData old = WeekSave(IslandWeekFact.All, firstWeek: false);
            old.Money = 1234;
            string before = JsonUtility.ToJson(old);
            _save.Current = old;

            foreach (CampaignBarrierDef wall in StPetersWalls())
                Assert.IsFalse(CampaignBarrierDirector.Stands(wall), wall.Id + " never stands for this save");

            for (int mask = 0; mask < 32; mask++)
            {
                var held = (IslandWeekFact)mask;
                SaveData save = WeekSave(held, firstWeek: false);
                Assert.AreEqual(StoryGateTalkKind.Normal, TalkOn(save, DayFor(held)).Kind, $"mask {mask}");
                Assert.IsNull(StoryGates.TaskNote(_flags), $"mask {mask}");
                Assert.IsNull(StoryGates.LiveFor(_gate.NpcId, _flags), $"mask {mask}");
            }

            _save.Current = old;
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith(_gate.NpcId, _flags).Kind);
            Assert.AreEqual(before, JsonUtility.ToJson(old), "reading the gate wrote nothing to the save");
            Assert.IsEmpty(_save.Writes, "and set no flag");
        }

        [Test]
        public void OnlyTheGatesOwnIslander_CarriesIt()
        {
            _save.Current = WeekSave(IslandWeekFact.All);
            GameServices.Clock = null;      // no clock: day 0, so the live reading is short of the day
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith("npc.basil_samson", _flags).Kind);
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith(null, _flags).Kind);
            Assert.AreEqual(StoryGateTalkKind.Hint, StoryGates.TalkWith(_gate.NpcId, _flags).Kind,
                "her own talk reads the live facts: four held, the day not yet reached");
        }

        [Test]
        public void WithNoRulesLoaded_NoFactHolds_AndNothingIsSaidOrShown()
        {
            GameServices.IslandWeek = NullIslandWeekProgress.Instance;
            _save.Current = WeekSave(IslandWeekFact.All);
            IslandWeekSnapshot none = GameServices.IslandWeek.Read(_save.Current, EarliestDayIndex + 100);
            Assert.IsFalse(none.AllHold);
            Assert.AreEqual(IslandWeekFact.None, none.Held);
            Assert.IsEmpty(none.MissingTextKeys);
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith(_gate.NpcId, _flags).Kind);
            Assert.IsNull(StoryGates.TaskNote(_flags));
        }

        [Test]
        public void TheCoreSeam_IsNeverNull_AndReadsTheShippedRules()
        {
            GameServices.Reset();
            IIslandWeekProgress seam = GameServices.IslandWeek;
            Assert.IsNotNull(seam);
            Assert.IsInstanceOf<IslandWeekProgress>(seam, "built from the shipped rules asset on first read");
            Assert.IsTrue(seam.Read(WeekSave(IslandWeekFact.All), EarliestDayIndex).AllHold);

            GameServices.IslandWeek = NullIslandWeekProgress.Instance;
            Assert.AreSame(NullIslandWeekProgress.Instance, GameServices.IslandWeek, "a caller may assign its own");
            GameServices.Reset();
            Assert.IsInstanceOf<IslandWeekProgress>(GameServices.IslandWeek, "Reset puts the shipped one back");
        }

        [Test]
        public void AGateWithNoWord_StaysShut()
        {
            var mute = Object.Instantiate(_gate);
            try
            {
                mute.Word = null;
                _save.Current = WeekSave(IslandWeekFact.All);
                IslandWeekSnapshot facts = _progress.Read(_save.Current, EarliestDayIndex);
                Assert.AreEqual(StoryGateTalkKind.Normal, StoryGate.Talk(mute, _flags, in facts).Kind);
                Assert.IsNotNull(mute.ValidationError());
            }
            finally { Object.DestroyImmediate(mute); }
        }

        // ---- the shipped data -------------------------------------------------------------------

        [Test]
        public void TheShippedGate_IsWhole_AndNamesARealIslanderAndTheShippedRules()
        {
            Assert.IsNull(_gate.ValidationError(), _gate.ValidationError());
            Assert.AreEqual("story_gate.st_peters_weather", _gate.Id);
            Assert.AreEqual(_rules.Id, _gate.ProgressId, "the gate reads the rules that ship");

            var loaded = Resources.LoadAll<StoryGateDef>(StoryGates.ResourcesFolder);
            CollectionAssert.Contains(loaded, _gate, "the game finds the gate where it looks for gates");
            Assert.AreEqual(loaded.Length, loaded.Select(g => g.Id).Distinct().Count(), "gate ids are unique");

            NpcDef islander = AssetDatabase.FindAssets("t:NpcDef", new[] { DataRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<NpcDef>)
                .SingleOrDefault(n => n != null && n.Id == _gate.NpcId);
            Assert.IsNotNull(islander, _gate.NpcId + " is a shipped islander");
            Assert.AreEqual(InteractKind.Talk, islander.Kind, "somebody he talks to");
            Assert.IsTrue(islander.HasDialogue, "with an ordinary talk for the gate to ride on");
        }

        [Test]
        public void EveryFactOfTheRules_HasItsTasksNoteAndItsSpokenLine_AndEachNoteFitsTheNote()
        {
            string[] keys =
            {
                _rules.DoryMendedTextKey, _rules.CodLicenceTextKey, _rules.FishFromDoryTextKey,
                _rules.ShellfishSoldTextKey, _rules.DayReachedTextKey,
            };
            foreach (string key in keys)
            {
                Assert.IsTrue(_gate.TryNote(key, out StoryGateNote note), key + " has a row on the gate");
                Assert.IsFalse(string.IsNullOrWhiteSpace(note.TaskNote), key + ": a Tasks note");
                Assert.IsFalse(string.IsNullOrWhiteSpace(note.SpokenHint), key + ": a spoken line");
                Assert.LessOrEqual(QuestPanelLayout.Rows(note.TaskNote).Count, QuestPanelLayout.MaxLines,
                    key + ": the note fits the Tasks note");
            }
            Assert.AreEqual(keys.Length, _gate.Notes.Length, "no row for a fact the rules do not have");
        }

        [Test]
        public void TheFlagTheGateSets_IsOneTheFlagRegistryKnows()
        {
            CollectionAssert.Contains(QuestContentValidationTests.SettableFlags().ToList(), _gate.SetsFlag);
        }
    }
}
