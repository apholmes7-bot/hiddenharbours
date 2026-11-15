using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Owner ruling 2026-11-15, slice 2: "Player chooses sleep duration. Time moves fast with player/s
    /// able to cancel with a button. Maximum 8 hours sleep before auto waking." The chosen length and its
    /// clamp (<see cref="SleepPlan"/>), the fast night against the instant one (same clock, same day
    /// rollovers, same needs, same save), the cancel that wakes and saves, tiredness relieved in
    /// proportion to the hours slept, and the quorum seam at one of one (<see cref="RestQuorum"/>).
    ///
    /// <para>Real <see cref="GameClock"/> (its own <c>FastForwardTo</c> fires the rollovers), real
    /// <see cref="NeedsService"/> on the test's own numbers, a save that snapshots the clock as the real
    /// one does and never touches a disk. Every hour count is the test's own config, never the shipped
    /// asset's, so the owner retuning <c>GameConfig</c> does not break it.</para>
    /// </summary>
    public class SleepFlowTests
    {
        const string Region = "region.fixture_cove";
        const string Bed = "fixture.interior.bed";

        sealed class SnapshottingSave : ISaveService
        {
            public int Saves;
            public double ClockAtWrite = double.NaN;
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }

            public void Save()
            {
                Saves++;
                IGameClock clock = GameServices.Clock;
                if (clock != null)
                {
                    Current.GameTimeSeconds = clock.TotalSeconds;
                    ClockAtWrite = clock.TotalSeconds;
                }
                LoadedExistingSave = true;
            }
        }

        /// <summary>One self-contained world: a clock, a save and a needs service reading them.</summary>
        sealed class Rig
        {
            public GameClock Clock;
            public SnapshottingSave Save;
            public NeedsService Needs;
            public readonly List<DayStarted> Days = new List<DayStarted>();
        }

        readonly List<Object> _spawned = new List<Object>();
        readonly List<GameSaved> _saved = new List<GameSaved>();
        readonly List<SleepFlowChanged> _changes = new List<SleepFlowChanged>();
        System.Action<GameSaved> _onSaved;
        System.Action<SleepFlowChanged> _onChange;
        System.Action<DayStarted> _onDay;
        Rig _live;

        GameConfig _config;
        IGameClock _previousClock;
        GameConfig _previousConfig;
        ISaveService _previousSave;
        INeedsService _previousNeeds;
        string _previousRegion;
        bool _responderWasInstalled;

        NeedsSettings _needs;

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _previousConfig = GameServices.Config;
            _previousSave = GameServices.Save;
            _previousNeeds = GameServices.Needs;
            _previousRegion = GameServices.CurrentRegionId;
            _responderWasInstalled = RestSaveResponder.Installed;
            SleepFlow.Reset();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.SleepMinHours = 1f;
            _config.SleepMaxHours = 8f;
            _config.SleepStepHours = 1f;
            _config.SleepGameHoursPerRealSecond = 1f;
            _config.WakeHour = 6f;
            _spawned.Add(_config);
            GameServices.Config = _config;
            GameServices.CurrentRegionId = Region;

            _needs = new NeedsSettings
            {
                Hunger = new NeedCurve { RisePerGameHour = 0.05f, MildAt = 0.4f, StrongAt = 0.8f },
                Tiredness = new NeedCurve { RisePerGameHour = 0.05f, MildAt = 0.5f, StrongAt = 0.9f },
                WalkFloor = 0.8f,
                SellFloor = 0.9f,
                TickGameMinutes = 5f,
                SleepReliefPerGameHour = 0.125f,
            };

            _saved.Clear();
            _changes.Clear();
            _onSaved = _saved.Add;
            _onChange = _changes.Add;
            _onDay = d => { if (_live != null) _live.Days.Add(d); };
            EventBus.Subscribe(_onSaved);
            EventBus.Subscribe(_onChange);
            EventBus.Subscribe(_onDay);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onSaved);
            EventBus.Unsubscribe(_onChange);
            EventBus.Unsubscribe(_onDay);
            SleepFlow.Reset();
            if (!_responderWasInstalled) RestSaveResponder.Uninstall();
            _live = null;

            GameServices.Clock = _previousClock;
            GameServices.Config = _previousConfig;
            GameServices.Save = _previousSave;
            GameServices.Needs = _previousNeeds;
            GameServices.CurrentRegionId = _previousRegion;

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- rig ------------------------------------------------------------------------------

        /// <summary>A fresh world at <paramref name="hour"/> on day 0, worn out
        /// (<paramref name="tiredness"/>) and a little hungry, made the live one.</summary>
        Rig World(double hour, float tiredness = 0.9f)
        {
            var go = new GameObject("SleepFlowClock");
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            FieldInfo field = typeof(GameClock).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "GameClock has no _config field");
            field.SetValue(clock, _config);
            clock.SeekTo(At(0, hour));

            var rig = new Rig { Clock = clock, Save = new SnapshottingSave() };
            rig.Save.Current.PlayerNeeds = new PlayerNeedsDto
            {
                Hunger = new NeedStateDto(0.2f, clock.TotalSeconds),
                Tiredness = new NeedStateDto(tiredness, clock.TotalSeconds),
            };
            NeedsSettings settings = _needs;
            double perHour = _config.SecondsPerDay / 24d;
            SnapshottingSave save = rig.Save;
            rig.Needs = new NeedsService(() => save, () => clock, () => settings, () => null, () => perHour);

            GameServices.Clock = clock;
            GameServices.Save = save;
            GameServices.Needs = rig.Needs;
            _live = rig;
            return rig;
        }

        double At(int day, double hour) =>
            day * (double)_config.SecondsPerDay + _config.SecondsPerDay * (hour / 24d);

        double GameHour => _config.SecondsPerDay / 24d;

        static RestSaveRequested Request() =>
            new RestSaveRequested("your bed", new Vector2(3f, 4f), 1, Bed);

        /// <summary>Run the fast night in <paramref name="wallStep"/>-second frames until it wakes.</summary>
        static int SleepThrough(float wallStep)
        {
            int frames = 0;
            while (SleepFlow.Phase == SleepPhase.Asleep && frames < 100000)
            {
                SleepFlow.Tick(wallStep);
                frames++;
            }
            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase, "the night ends on its own");
            return frames;
        }

        // =====================================================================================
        //  the chosen length: clamp, step, default
        // =====================================================================================

        [TestCase(12f, 8f)]
        [TestCase(8f, 8f)]
        [TestCase(5.5f, 5.5f)]
        [TestCase(0.25f, 1f)]
        [TestCase(-3f, 1f)]
        [TestCase(float.NaN, 1f)]
        public void TheChosenHours_AreHeldBetweenTheMinimumAndEight(float asked, float expected)
        {
            Assert.AreEqual(expected, SleepPlan.ClampHours(asked, 1f, 8f), 1e-6f);
        }

        [Test]
        public void NoConfig_CanMakeASleepLongerThanEightHours()
        {
            _config.SleepMaxHours = 20f;
            Assert.AreEqual(SleepPlan.RulingMaxHours, SleepPlan.MaxHours(_config));
            Assert.AreEqual(SleepPlan.RulingMaxHours, SleepPlan.ClampHours(24f, _config));
            _config.SleepMinHours = 12f;
            Assert.AreEqual(SleepPlan.RulingMaxHours, SleepPlan.MinHours(_config), "a minimum above the cap is the cap");
        }

        [Test]
        public void APress_StepsByTheConfigsStep_AndStopsAtTheEnds()
        {
            Assert.AreEqual(6f, SleepPlan.Step(5f, +1, 1f, 1f, 8f), 1e-6f);
            Assert.AreEqual(4f, SleepPlan.Step(5f, -1, 1f, 1f, 8f), 1e-6f);
            Assert.AreEqual(8f, SleepPlan.Step(8f, +1, 1f, 1f, 8f), 1e-6f);
            Assert.AreEqual(1f, SleepPlan.Step(1f, -1, 1f, 1f, 8f), 1e-6f);
            Assert.AreEqual(2.5f, SleepPlan.Step(2f, +1, 0.5f, 1f, 8f), 1e-6f);
        }

        [TestCase(22d, 8f)]   // to 06:00
        [TestCase(2d, 4f)]    // after midnight: this morning's 06:00
        [TestCase(12d, 8f)]   // noon: never past eight hours, wakes at 20:00
        [TestCase(5.5d, 1f)]  // half an hour from the wake hour: the minimum
        public void TheFirstOffer_IsUntilMorning_HeldInsideTheBounds(double hour, float expected)
        {
            Assert.AreEqual(expected, SleepPlan.DefaultHours(At(0, hour), _config), 1e-4f);
        }

        [Test]
        public void ASleepThatReachesTheWakeHour_LandsOnTheMorningBitForBit()
        {
            double start = At(0, 22d);
            Assert.AreEqual(RestMorning.WakeSeconds(start, _config), SleepPlan.WakeSeconds(start, 8f, _config));
            Assert.AreEqual(start + 3d * GameHour, SleepPlan.WakeSeconds(start, 3f, _config), 1e-9);
        }

        [Test]
        public void TheChooser_OffersTheDefault_AndAPressChangesIt()
        {
            World(22d);
            Assert.IsTrue(SleepFlow.Begin(Request()));
            Assert.AreEqual(SleepPhase.Choosing, SleepFlow.Phase);
            Assert.AreEqual(8f, SleepFlow.Hours, 1e-4f);
            Assert.AreEqual(SleepPhase.Choosing, _changes[_changes.Count - 1].Phase);

            SleepFlow.Adjust(+1);
            Assert.AreEqual(8f, SleepFlow.Hours, 1e-4f, "already at the cap");
            SleepFlow.Adjust(-1);
            SleepFlow.Adjust(-1);
            Assert.AreEqual(6f, SleepFlow.Hours, 1e-4f);
            Assert.AreEqual(At(1, 4d), _changes[_changes.Count - 1].WakeSeconds, 1e-6, "the offer says where it wakes");
            Assert.IsTrue(SleepFlow.HoldsTheWorld, "lying down parks the controls");
            Assert.IsTrue(ControlIntentGates.WorldStopped);
        }

        // =====================================================================================
        //  fast time is the same world as the instant night
        // =====================================================================================

        [Test]
        public void TheFastNight_EndsOnTheSameClock_Days_Needs_AndSave_AsTheInstantNight()
        {
            Rig instant = World(22d);
            RestSaveResponder.Fulfil(Request());
            double instantClock = instant.Clock.TotalSeconds;
            NeedStateDto instantTired = instant.Save.Current.PlayerNeeds.Tiredness;
            NeedStateDto instantHungry = instant.Save.Current.PlayerNeeds.Hunger;
            SleepPlace instantPlace = RestLocker.Place(instant.Save.Current);

            Rig fast = World(22d);
            Assert.IsTrue(SleepFlow.Begin(Request()));
            SleepFlow.Confirm();
            Assert.AreEqual(SleepPhase.Asleep, SleepFlow.Phase);
            int frames = SleepThrough(1f / 60f);

            Assert.Greater(frames, 1, "premise: the fast night took many frames");
            Assert.AreEqual(instantClock, fast.Clock.TotalSeconds, "the same instant, bit for bit");
            Assert.AreEqual(At(1, 6d), fast.Clock.TotalSeconds, 1e-6);
            Assert.AreEqual(instant.Days.Count, fast.Days.Count, "the same day rollovers");
            Assert.AreEqual(1, fast.Days.Count, "one midnight, one rollover");
            Assert.AreEqual(instant.Days[0].DayOfSeason, fast.Days[0].DayOfSeason);
            Assert.AreEqual(instant.Days[0].Season, fast.Days[0].Season);
            Assert.AreEqual(instantTired.Level, fast.Save.Current.PlayerNeeds.Tiredness.Level);
            Assert.AreEqual(instantTired.SinceGameSeconds, fast.Save.Current.PlayerNeeds.Tiredness.SinceGameSeconds);
            Assert.AreEqual(instantHungry.Level, fast.Save.Current.PlayerNeeds.Hunger.Level);
            Assert.AreEqual(instantHungry.SinceGameSeconds, fast.Save.Current.PlayerNeeds.Hunger.SinceGameSeconds);
            Assert.AreEqual(instant.Save.ClockAtWrite, fast.Save.ClockAtWrite, "the save keeps the same morning");
            Assert.AreEqual(1, fast.Save.Saves);
            SleepPlace fastPlace = RestLocker.Place(fast.Save.Current);
            Assert.AreEqual(instantPlace.Anchor.Region, fastPlace.Anchor.Region);
            Assert.AreEqual(instantPlace.Anchor.Position, fastPlace.Anchor.Position);
            Assert.AreEqual(instantPlace.BedId, fastPlace.BedId);
            Assert.AreEqual(instantPlace.WokeAtSeconds, fastPlace.WokeAtSeconds);
        }

        [Test]
        public void TheFrameRate_DoesNotChangeWhereTheNightEnds()
        {
            World(22d);
            SleepFlow.Begin(Request());
            SleepFlow.Adjust(-1);   // 7 hours
            SleepFlow.Confirm();
            SleepThrough(1f / 144f);
            double fine = GameServices.Clock.TotalSeconds;

            World(22d);
            SleepFlow.Begin(Request());
            SleepFlow.Adjust(-1);
            SleepFlow.Confirm();
            SleepThrough(0.37f);
            Assert.AreEqual(fine, GameServices.Clock.TotalSeconds);
            Assert.AreEqual(At(1, 5d), fine, 1e-6);
        }

        [Test]
        public void TheNightRunsAtTheConfigsPace()
        {
            World(22d);
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();
            SleepFlow.Tick(2f);   // two real seconds at one game hour per second
            Assert.AreEqual(At(0, 22d) + 2d * GameHour, GameServices.Clock.TotalSeconds, 1e-6);
            Assert.AreEqual(SleepPhase.Asleep, SleepFlow.Phase);
        }

        [Test]
        public void APausedGame_PausesTheNight()
        {
            Rig rig = World(22d);
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();
            rig.Clock.IsPaused = true;
            double before = rig.Clock.TotalSeconds;
            SleepFlow.Tick(5f);
            Assert.AreEqual(before, rig.Clock.TotalSeconds);
            Assert.AreEqual(SleepPhase.Asleep, SleepFlow.Phase);
        }

        // =====================================================================================
        //  waking: on its own, by the button; and the save
        // =====================================================================================

        [Test]
        public void TheSleep_WakesOnItsOwn_AtTheChosenLength_AndSaves()
        {
            Rig rig = World(22d);
            SleepFlow.Begin(Request());
            for (int i = 0; i < 5; i++) SleepFlow.Adjust(-1);   // 3 hours
            SleepFlow.Confirm();
            SleepThrough(0.25f);

            Assert.AreEqual(At(1, 1d), rig.Clock.TotalSeconds, 1e-6, "22:00 + 3 h");
            Assert.AreEqual(1, rig.Save.Saves);
            Assert.AreEqual(1, _saved.Count);
            Assert.AreEqual(SleepFlow.RestedText, _saved[0].Reason);
            Assert.AreEqual(SleepPhase.Awake, _changes[_changes.Count - 1].Phase);
        }

        [Test]
        public void TheButton_WakesEarly_AndStillSavesWhereYouSlept()
        {
            Rig rig = World(22d);
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();
            SleepFlow.Tick(2.5f);
            Assert.AreEqual(0, rig.Save.Saves, "nothing is written mid-sleep");

            SleepFlow.Cancel();

            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase);
            Assert.AreEqual(At(0, 22d) + 2.5d * GameHour, rig.Clock.TotalSeconds, 1e-6, "woke where the clock stood");
            Assert.AreEqual(1, rig.Save.Saves, "a cancelled sleep is still a sleep, and a sleep is a save");
            Assert.AreEqual(rig.Clock.TotalSeconds, rig.Save.ClockAtWrite);
            Assert.AreEqual(SleepFlow.WokeEarlyText, _saved[_saved.Count - 1].Reason);
            SleepPlace place = RestLocker.Place(rig.Save.Current);
            Assert.AreEqual(Region, place.Anchor.Region);
            Assert.AreEqual(new Vector2(3f, 4f), place.Anchor.Position);
            Assert.AreEqual(Bed, place.BedId);
            Assert.AreEqual(rig.Clock.TotalSeconds, place.WokeAtSeconds);
            Assert.IsFalse(RestQuorum.IsOccupied(Bed), "up and out of the bed");
        }

        [Test]
        public void GettingUpWithoutSleeping_MovesNoClock_AndSavesNothing()
        {
            Rig rig = World(22d);
            double before = rig.Clock.TotalSeconds;
            SleepFlow.Begin(Request());
            SleepFlow.Cancel();   // while choosing: get up

            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase);
            Assert.AreEqual(before, rig.Clock.TotalSeconds);
            Assert.AreEqual(0, rig.Save.Saves);
            Assert.AreEqual(0, _saved.Count);
            Assert.IsFalse(ControlIntentGates.WorldStopped, "the controls come back");
        }

        [Test]
        public void AQuitMidSleep_KeepsTheLastCompletedSleep()
        {
            Rig rig = World(22d);
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();
            SleepFlow.Tick(1f);
            Assert.IsTrue(SleepFlow.InProgress, "the save service refuses writes while this is true");

            SleepFlow.Abandon();

            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase);
            Assert.AreEqual(0, rig.Save.Saves, "no write: the reload is the last sleep that ended");
            Assert.IsFalse(RestLocker.Place(rig.Save.Current).IsSet);
        }

        [Test]
        public void WithAChooserPresent_TheRestRequest_LiesYouDownInsteadOfSleepingAtOnce()
        {
            Rig rig = World(22d);
            RestSaveResponder.Install();
            SleepFlow.ChooserPresent = true;
            double before = rig.Clock.TotalSeconds;

            EventBus.Publish(Request());

            Assert.AreEqual(SleepPhase.Choosing, SleepFlow.Phase);
            Assert.AreEqual(before, rig.Clock.TotalSeconds, "no clock moves until the player chooses");
            Assert.AreEqual(0, rig.Save.Saves);

            EventBus.Publish(Request());
            Assert.AreEqual(SleepPhase.Choosing, SleepFlow.Phase, "a second press while lying down is ignored");
        }

        [Test]
        public void AHeldHour_IsStillTheSaveOnlyBeat_WithTirednessUnchanged()
        {
            Rig rig = World(22d);
            rig.Clock.IsHeld = true;
            float tired = rig.Needs.Level(NeedKind.Tiredness);

            Assert.IsFalse(SleepFlow.Begin(Request()), "no night to choose");

            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase);
            Assert.AreEqual(1, rig.Save.Saves, "the day is still kept");
            Assert.AreEqual(RestSaveResponder.SavedText, _saved[_saved.Count - 1].Reason);
            Assert.AreEqual(tired, rig.Needs.Level(NeedKind.Tiredness), "no night slept, no relief");
        }

        // =====================================================================================
        //  relief in proportion to the hours slept; hunger on the clock
        // =====================================================================================

        [TestCase(2f, 0.65f)]
        [TestCase(4f, 0.4f)]
        [TestCase(8f, 0f)]
        public void Tiredness_FallsByTheHoursActuallySlept(float hours, float expected)
        {
            Rig rig = World(22d, tiredness: 0.9f);
            SleepFlow.Begin(Request());
            for (int i = 0; i < 10; i++) SleepFlow.Adjust(-1);   // down to the minimum, then up to the hours asked
            while (SleepFlow.Hours < hours - 1e-4f) SleepFlow.Adjust(+1);
            SleepFlow.Confirm();
            SleepThrough(0.1f);

            NeedStateDto tired = rig.Save.Current.PlayerNeeds.Tiredness;
            Assert.AreEqual(expected, tired.Level, 1e-4f, "0.9 at bedtime, 0.125 off per hour slept");
            Assert.AreEqual(rig.Clock.TotalSeconds, tired.SinceGameSeconds, "stamped at the waking");
        }

        [Test]
        public void WakingEarly_RelievesOnlyTheHoursSlept()
        {
            Rig rig = World(22d, tiredness: 0.9f);
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();      // eight hours chosen…
            SleepFlow.Tick(2f);       // …two slept
            SleepFlow.Cancel();
            Assert.AreEqual(0.65f, rig.Save.Current.PlayerNeeds.Tiredness.Level, 1e-4f);
        }

        [Test]
        public void Hunger_KeepsRisingOnTheClock_ThroughTheSleep()
        {
            Rig rig = World(22d);
            NeedStateDto hungerAtBedtime = rig.Save.Current.PlayerNeeds.Hunger;
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();
            SleepThrough(0.5f);

            Assert.AreEqual(hungerAtBedtime.Level, rig.Save.Current.PlayerNeeds.Hunger.Level, "the stamp is untouched");
            Assert.AreEqual(hungerAtBedtime.SinceGameSeconds, rig.Save.Current.PlayerNeeds.Hunger.SinceGameSeconds);
            Assert.AreEqual(0.2f + 8f * 0.05f, rig.Needs.Level(NeedKind.Hunger), 1e-4f, "eight hours hungrier");
        }

        [Test]
        public void ANoReliefNumber_IsTheOldRule_AnySleepRestsFully()
        {
            _needs.SleepReliefPerGameHour = 0f;
            Rig rig = World(22d, tiredness: 0.9f);
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();
            SleepFlow.Tick(1f);
            SleepFlow.Cancel();
            Assert.AreEqual(0f, rig.Save.Current.PlayerNeeds.Tiredness.Level);
        }

        // =====================================================================================
        //  the quorum seam: one of one today; one bed, one sleeper
        // =====================================================================================

        [Test]
        public void TheQuorum_IsOneOfOneToday()
        {
            Assert.AreEqual(1, RestQuorum.Players);
            Assert.IsTrue(RestQuorum.IsMet(1));
            Assert.IsFalse(RestQuorum.IsMet(0));
            Assert.IsTrue(RestQuorum.IsMet(2, 2));
            Assert.IsFalse(RestQuorum.IsMet(1, 2), "everyone present must be asleep");
            Assert.IsFalse(RestQuorum.IsMet(0, 0), "no players, no night");
        }

        [Test]
        public void OneBed_OneSleeper()
        {
            Assert.IsTrue(RestQuorum.TryClaim(Bed, 0));
            Assert.IsTrue(RestQuorum.TryClaim(Bed, 0), "your own bed is still yours");
            Assert.IsFalse(RestQuorum.TryClaim(Bed, 1), "someone is in it");
            RestQuorum.Release(Bed, 1);
            Assert.IsTrue(RestQuorum.IsOccupied(Bed), "only its sleeper gets out of it");
            RestQuorum.Release(Bed, 0);
            Assert.IsTrue(RestQuorum.TryClaim(Bed, 1));
            Assert.IsTrue(RestQuorum.TryClaim(string.Empty, 5), "a rest naming no bed claims nothing");
        }

        [Test]
        public void ABedSomeoneIsIn_RefusesTheRest()
        {
            Rig rig = World(22d);
            Assert.IsTrue(RestQuorum.TryClaim(Bed, 1));
            Assert.IsFalse(SleepFlow.Begin(Request()));
            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase);
            Assert.AreEqual(SleepFlow.OccupiedText, _saved[_saved.Count - 1].Reason);
            Assert.AreEqual(0, rig.Save.Saves);
        }

        // =====================================================================================
        //  the chooser's words
        // =====================================================================================

        [Test]
        public void TheChoosersWords_SayTheHoursAndTheWakeTime()
        {
            double perDay = _config.SecondsPerDay;
            var offer = new SleepFlowChanged(SleepPhase.Choosing, 8f, 1f, 8f, 0d, perDay + perDay * 0.25d);
            StringAssert.StartsWith("Sleep 8 hours, till about 06:00?", SleepChooser.PromptFor(offer, perDay));
            Assert.AreEqual("1 hour", SleepChooser.HoursText(1f));
            Assert.AreEqual("7½ hours", SleepChooser.HoursText(7.5f));
            Assert.AreEqual("20:00", SleepChooser.ClockText(perDay * (20d / 24d), perDay));
            Assert.AreEqual(string.Empty,
                            SleepChooser.PromptFor(new SleepFlowChanged(SleepPhase.Awake, 0f, 1f, 8f, 0d, 0d), perDay));
        }
    }
}
