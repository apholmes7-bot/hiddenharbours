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
    /// <b>The islander's word at the end of the first week on St Peters, as a rule and as shipped
    /// data.</b> When the five facts hold and it is evening, an ordinary Talk with the gate's islander
    /// is the word; heard out, the word is given once: one saved flag, one signal. With the facts but
    /// not the hour she asks him back; before the facts hold she adds one line about the first thing
    /// still to do, and the Tasks note says the same thing. A save that never started the island week
    /// meets none of it.
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
        private const float Evening = 19f;
        private const float Noon = 12f;

        /// <summary>The flag service a save file is behind: flags live in the save's own list and nowhere else.</summary>
        internal sealed class FlagSave : ISaveService
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

        /// <summary>A clock that is whatever day and hour the case says.</summary>
        internal sealed class FixedClock : IGameClock
        {
            public int Day;
            public float Hour = Evening;
            public double TotalSeconds => (Day + Hour / 24.0) * GameConfig.DefaultSecondsPerDay;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => Day;
            public int DayOfSeason => Day + 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => Hour;
            public float DayFraction => Hour / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private StoryGateDef _gate;
        private IslandWeekRulesDef _rules;
        private IslandWeekProgress _progress;
        private FlagSave _save;
        private SaveFlagStore _flags;
        private FixedClock _clock;
        private int _wordsGiven;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            DemoBuild.Override = false;
            _gate = AssetDatabase.LoadAssetAtPath<StoryGateDef>(GatePath);
            _rules = AssetDatabase.LoadAssetAtPath<IslandWeekRulesDef>(RulesPath);
            Assert.IsNotNull(_gate, "the shipped gate asset");
            Assert.IsNotNull(_rules, "the shipped island-week rules");
            _progress = new IslandWeekProgress(_rules);
            _save = new FlagSave();
            GameServices.Save = _save;
            GameServices.IslandWeek = _progress;
            _clock = new FixedClock { Day = _rules.EarliestDay - 1 };
            GameServices.Clock = _clock;
            _flags = new SaveFlagStore();
            StoryGates.Load(_gate);
            _wordsGiven = 0;
            EventBus.Subscribe<StoryWordGiven>(CountWord);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<StoryWordGiven>(CountWord);
            StoryGates.Load(null);
            DemoBuild.Override = null;
            GameServices.Reset();
        }

        private void CountWord(StoryWordGiven e) => _wordsGiven++;

        // ---- fixtures ---------------------------------------------------------------------------

        private int EarliestDayIndex => _rules.EarliestDay - 1;

        /// <summary>A save in its island week holding exactly the saved facts of <paramref name="held"/>.</summary>
        internal static SaveData WeekSave(StoryGateDef gate, IslandWeekRulesDef rules, IslandWeekFact held, bool firstWeek = true)
        {
            var save = new SaveData { SchemaVersion = SaveMigration.CurrentVersion };
            save.RepairedBoats = new List<string>();
            save.OwnedLicenses = new List<string>();
            save.OnboardingFlags = new List<SaveFlag>();
            if (firstWeek) save.OnboardingFlags.Add(new SaveFlag(gate.LiveWhileFlag, true));
            if ((held & IslandWeekFact.DoryMended) != 0) save.RepairedBoats.Add(rules.DoryId);
            if ((held & IslandWeekFact.CodLicence) != 0) save.OwnedLicenses.Add(rules.LicenceId);
            if ((held & IslandWeekFact.FishFromDory) != 0)
                save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.FishFromDoryKey, true));
            if ((held & IslandWeekFact.ShellfishSold) != 0)
                save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.ShellfishSoldKey, true));
            return save;
        }

        private SaveData WeekSave(IslandWeekFact held, bool firstWeek = true) => WeekSave(_gate, _rules, held, firstWeek);

        private int DayFor(IslandWeekFact held)
            => (held & IslandWeekFact.DayReached) != 0 ? EarliestDayIndex : EarliestDayIndex - 1;

        private StoryGateTalk TalkOn(SaveData save, int dayIndex, float hour = Evening, bool demo = false)
        {
            _save.Current = save;
            IslandWeekSnapshot facts = _progress.Read(save, dayIndex);
            return StoryGate.Talk(_gate, _flags, in facts, hour, demo);
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
                    Assert.AreEqual(StoryGateTalkKind.Word, talk.Kind, "every fact holds, in the evening");
                    CollectionAssert.AreEqual(_gate.Word.FirstLines, talk.Lines, "the word is the gate's own lines");
                    Assert.AreSame(_gate, talk.Gate);
                }
                else
                {
                    Assert.AreNotEqual(StoryGateTalkKind.Word, talk.Kind, $"mask {mask} ({held}) is not the whole week");
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
                foreach (float hour in new[] { Noon, Evening })
                {
                    StoryGateTalk talk = TalkOn(save, DayFor(held), hour);
                    Assert.AreEqual(StoryGateTalkKind.Hint, talk.Kind, $"mask {mask} at {hour}");
                    CollectionAssert.AreEqual(new[] { note.SpokenHint }, talk.Lines, $"mask {mask} at {hour}");
                }

                IslandWeekSnapshot facts = _progress.Read(save, DayFor(held));
                Assert.AreEqual(note.TaskNote, StoryGate.TaskNote(_gate, _flags, in facts), $"mask {mask}");
            }
        }

        [Test]
        public void WithEveryFactButNotTheHour_SheAsksHimBack_AndTheWordWaitsForTheEvening()
        {
            SaveData save = WeekSave(IslandWeekFact.All);
            foreach (float hour in new[] { _gate.ToHour, 6f, Noon, _gate.FromHour - 0.01f })
            {
                StoryGateTalk talk = TalkOn(save, EarliestDayIndex, hour);
                Assert.AreEqual(StoryGateTalkKind.Hint, talk.Kind, $"hour {hour}");
                CollectionAssert.AreEqual(new[] { _gate.OutsideHoursLine }, talk.Lines, $"hour {hour}");
            }
            foreach (float hour in new[] { _gate.FromHour, 21f, 23.99f, 0f, _gate.ToHour - 0.01f })
                Assert.AreEqual(StoryGateTalkKind.Word, TalkOn(save, EarliestDayIndex, hour).Kind, $"hour {hour}");

            IslandWeekSnapshot facts = _progress.Read(save, EarliestDayIndex);
            Assert.AreEqual(_gate.AllHoldNote, StoryGate.TaskNote(_gate, _flags, in facts),
                "with nothing left to do the note sends him to her");
            Assert.IsEmpty(_save.Writes, "asking writes nothing");
        }

        [Test]
        public void TheHours_RunPastMidnight_AndAGateWithNoHoursIsAnyHour()
        {
            var gate = Object.Instantiate(_gate);
            try
            {
                gate.FromHour = 17f; gate.ToHour = 4.5f;
                Assert.IsTrue(gate.InHours(17f)); Assert.IsTrue(gate.InHours(2f)); Assert.IsTrue(gate.InHours(23.5f));
                Assert.IsFalse(gate.InHours(4.5f)); Assert.IsFalse(gate.InHours(16.9f));
                gate.FromHour = 9f; gate.ToHour = 12f;
                Assert.IsTrue(gate.InHours(9f)); Assert.IsFalse(gate.InHours(12f)); Assert.IsFalse(gate.InHours(8f));
                gate.FromHour = 0f; gate.ToHour = 0f;
                Assert.IsTrue(gate.InHours(0f)); Assert.IsTrue(gate.InHours(13f));
            }
            finally { Object.DestroyImmediate(gate); }
        }

        [Test]
        public void TheDayIsTheStartOfTheSeventhDay_NotOneDayEarlier()
        {
            IslandWeekFact four = IslandWeekFact.All & ~IslandWeekFact.DayReached;
            Assert.AreEqual(StoryGateTalkKind.Hint, TalkOn(WeekSave(four), EarliestDayIndex - 1).Kind);
            Assert.AreEqual(StoryGateTalkKind.Word, TalkOn(WeekSave(four), EarliestDayIndex).Kind);
            Assert.AreEqual(StoryGateTalkKind.Word, TalkOn(WeekSave(four), EarliestDayIndex + 30).Kind,
                "an evening he missed on the day is still there later");
        }

        // ---- giving the word --------------------------------------------------------------------

        [Test]
        public void WhileAFactIsMissing_OrItIsNotEvening_NoWordIsGiven()
        {
            for (int mask = 0; mask < 31; mask++)
            {
                var held = (IslandWeekFact)mask;
                _save.Current = WeekSave(held);
                _clock.Day = DayFor(held);
                Assert.IsFalse(StoryGates.Give(_gate, _flags), $"mask {mask}");
            }

            _save.Current = WeekSave(IslandWeekFact.All);
            _clock.Day = EarliestDayIndex;
            _clock.Hour = Noon;
            Assert.IsFalse(StoryGates.Give(_gate, _flags), "every fact, but at noon");

            Assert.AreEqual(0, _wordsGiven);
            Assert.IsEmpty(_save.Writes, "nothing was written");
        }

        [Test]
        public void MetInTheEvening_TheWordIsGivenOnce_NeverTwice_AndStaysGivenThroughTheFile()
        {
            SaveData save = WeekSave(IslandWeekFact.All);
            _save.Current = save;
            Assert.AreEqual(StoryGateTalkKind.Word, StoryGates.TalkWith(_gate.NpcId, _flags).Kind);

            Assert.IsTrue(StoryGates.Give(_gate, _flags), "heard out, it is given");
            Assert.AreEqual(1, _wordsGiven, "one signal");
            Assert.AreEqual(1, _save.Writes[_gate.SetsFlag], "one write");

            // Every later talk, any hour, any day: no second word.
            for (int i = 0; i < 3; i++)
            {
                _clock.Day = EarliestDayIndex + i;
                Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith(_gate.NpcId, _flags).Kind);
                Assert.IsFalse(StoryGates.Give(_gate, _flags));
            }
            Assert.AreEqual(1, _wordsGiven, "never twice");
            Assert.AreEqual(1, _save.Writes[_gate.SetsFlag], "set once");
            Assert.IsNull(StoryGates.TaskNote(_flags), "nothing left on the note");

            // Through the file and back: the flag is in the list the save already had, and still set.
            string json = JsonUtility.ToJson(save);
            SaveData back = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(json));
            Assert.AreEqual(SaveMigration.CurrentVersion, back.SchemaVersion);
            _save.Current = back;
            Assert.IsTrue(_save.GetFlag(_gate.SetsFlag), "the flag came back from the file");
            Assert.AreEqual(save.OnboardingFlags.Count, back.OnboardingFlags.Count, "no flag gained or lost");
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith(_gate.NpcId, _flags).Kind);
            Assert.IsFalse(StoryGates.Give(_gate, _flags));
            Assert.AreEqual(1, _wordsGiven, "loading a save that holds the word raises nothing");
        }

        [Test]
        public void TheSignal_NamesTheGateAndTheFlag_AndNoPlayer()
        {
            StoryWordGiven? heard = null;
            void Listen(StoryWordGiven e) => heard = e;
            EventBus.Subscribe<StoryWordGiven>(Listen);
            try
            {
                _save.Current = WeekSave(IslandWeekFact.All);
                Assert.IsTrue(StoryGates.Give(_gate, _flags));
            }
            finally { EventBus.Unsubscribe<StoryWordGiven>(Listen); }

            Assert.IsTrue(heard.HasValue);
            Assert.AreEqual(_gate.Id, heard.Value.GateId);
            Assert.AreEqual(_gate.SetsFlag, heard.Value.Flag);
            Assert.AreEqual(_gate.EndsDemo, heard.Value.EndsDemo);
            CollectionAssert.AreEquivalent(new[] { "GateId", "Flag", "EndsDemo" },
                typeof(StoryWordGiven).GetFields().Select(f => f.Name).ToArray(),
                "the word is the household's: the signal carries nobody's name");
        }

        [Test]
        public void TheWordItself_OpensNoSeaEdge()
        {
            _save.Current = WeekSave(IslandWeekFact.All);
            List<CampaignBarrierDef> walls = StPetersWalls();
            foreach (CampaignBarrierDef wall in walls)
            {
                Assert.AreNotEqual(_gate.SetsFlag, wall.LiftedByFlag, wall.Id + " does not wait on the word");
                Assert.AreEqual(_gate.LiveWhileFlag, wall.StandsWhileFlag, wall.Id + " stands in the gate's week");
            }

            Assert.IsTrue(StoryGates.Give(_gate, _flags));
            foreach (CampaignBarrierDef wall in walls)
                Assert.IsTrue(CampaignBarrierDirector.Stands(wall), wall.Id + " still stands the evening she speaks");
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
                foreach (bool demo in new[] { false, true })
                    Assert.AreEqual(StoryGateTalkKind.Normal, TalkOn(save, DayFor(held), Evening, demo).Kind, $"mask {mask}");
                Assert.IsNull(StoryGates.TaskNote(_flags), $"mask {mask}");
                Assert.IsNull(StoryGates.FirstLive(_flags), $"mask {mask}");
            }

            _save.Current = old;
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith(_gate.NpcId, _flags).Kind);
            Assert.IsFalse(StoryGates.Give(_gate, _flags));
            Assert.AreEqual(before, JsonUtility.ToJson(old), "the gate wrote nothing to the save");
            Assert.IsEmpty(_save.Writes, "and set no flag");
            Assert.AreEqual(0, _wordsGiven);
        }

        [Test]
        public void OnlyTheGatesOwnIslander_CarriesIt()
        {
            _save.Current = WeekSave(IslandWeekFact.All);
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith("npc.basil_samson", _flags).Kind);
            Assert.AreEqual(StoryGateTalkKind.Normal, StoryGates.TalkWith(null, _flags).Kind);
            Assert.AreEqual(StoryGateTalkKind.Word, StoryGates.TalkWith(_gate.NpcId, _flags).Kind);

            GameServices.Clock = null;      // no clock: day 0, so the live reading is short of the day
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
            Assert.IsFalse(StoryGates.Give(_gate, _flags));
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
                mute.DemoWord = null;
                _save.Current = WeekSave(IslandWeekFact.All);
                IslandWeekSnapshot facts = _progress.Read(_save.Current, EarliestDayIndex);
                Assert.AreEqual(StoryGateTalkKind.Normal, StoryGate.Talk(mute, _flags, in facts, Evening, false).Kind);
                Assert.IsFalse(StoryGates.Give(mute, _flags));
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
        public void TheShippedGate_HasAnEvening_ADemoWord_AndEveryLineItCanSay()
        {
            Assert.IsFalse(Mathf.Approximately(_gate.FromHour, _gate.ToHour), "the word has hours");
            Assert.IsTrue(_gate.InHours(Evening));
            Assert.IsFalse(_gate.InHours(Noon));
            Assert.IsTrue(_gate.EndsDemo, "a demo ends at this word");
            Assert.IsNotNull(_gate.DemoWord, "and has its own word for it");
            CollectionAssert.AreNotEqual(_gate.WordLines(false), _gate.WordLines(true),
                "the demo's word promises no crossing");
            foreach (string line in new[] { _gate.OutsideHoursLine, _gate.AllHoldNote, _gate.DemoAfterLine })
                Assert.IsFalse(string.IsNullOrWhiteSpace(line));
            Assert.LessOrEqual(QuestPanelLayout.Rows(_gate.AllHoldNote).Count, QuestPanelLayout.MaxLines,
                "the last note fits the Tasks note");
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
