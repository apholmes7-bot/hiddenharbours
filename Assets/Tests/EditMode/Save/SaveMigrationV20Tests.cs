using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v19 -> v20: THE WHOLE SLEEP PLACE (owner ruling 2026-11-15: "Reloading restarts the day where you
    /// last slept. A sleep is a save."). Which bed, when the sleep ended, and for a bunk aboard where the
    /// boat lay, beside the v13 anchor. Pins the three obligations of a schema bump (an older save survives
    /// untouched, the new fields mean the right thing when absent, and the thing they exist for works) and
    /// the reload half: a rest's save, round-tripped through the real JSON, wakes the player at the same
    /// region, position and storey, with the wake instant recorded.
    /// </summary>
    public class SaveMigrationV20Tests
    {
        const string StP = "region.st_peters";
        const string NMC = "region.nine_mile_creek";

        sealed class FakeSaveService : ISaveService
        {
            public int Saves;
            public SaveData Current { get; set; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        /// <summary>A clock that moves when asked (the interface default refuses). No rollovers.</summary>
        sealed class MovingClock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public bool FastForwardTo(double totalSeconds)
            {
                if (totalSeconds <= T) return false;
                T = totalSeconds;
                return true;
            }
        }

        GameObject _player;
        readonly List<SleepPlaceWakeRequested> _wakeRequests = new List<SleepPlaceWakeRequested>();
        System.Action<SleepPlaceWakeRequested> _onWakeRequest;
        IGameClock _previousClock;
        GameConfig _previousConfig;

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _previousConfig = GameServices.Config;
            GameServices.Clock = null;
            GameServices.Config = null;
            EventBus.Clear<GameSaved>();
            EventBus.Clear<PlayerWokeAt>();
            GameServices.Save = null;
            GameServices.CurrentRegionId = null;
            GameServices.PlayerTransform = null;
            _wakeRequests.Clear();
            _onWakeRequest = _wakeRequests.Add;
            EventBus.Subscribe(_onWakeRequest);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onWakeRequest);
            GameServices.Clock = _previousClock;
            GameServices.Config = _previousConfig;
            GameServices.Save = null;
            GameServices.CurrentRegionId = null;
            GameServices.PlayerTransform = null;
            EventBus.Clear<GameSaved>();
            EventBus.Clear<PlayerWokeAt>();
            if (_player != null) { Object.DestroyImmediate(_player); _player = null; }
        }

        Transform StandPlayerAt(Vector2 at)
        {
            _player = new GameObject("player");
            _player.transform.position = new Vector3(at.x, at.y, -3.5f);
            GameServices.PlayerTransform = _player.transform;
            return _player.transform;
        }

        // =====================================================================================
        //  the migration
        // =====================================================================================

        [Test]
        public void CurrentVersion_IsAtLeastTwenty_BecauseTheSleepPlaceShipped()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 20);
        }

        [Test]
        public void AV19SaveWithABedAnchor_KeepsItsBed_AndGainsHonestEmpties()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":19,\"GameTimeSeconds\":5000.0,\"RestRegion\":\"region.st_peters\"," +
                "\"RestPosX\":3.5,\"RestPosY\":-2.25,\"RestLevel\":1}");

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            SleepPlace place = RestLocker.Place(save);
            Assert.IsTrue(place.IsSet, "the v13 anchor is untouched: the player still wakes in that bed");
            Assert.AreEqual(StP, place.Anchor.Region);
            Assert.AreEqual(new Vector2(3.5f, -2.25f), place.Anchor.Position);
            Assert.AreEqual(1, place.Anchor.Level);
            Assert.AreEqual("", place.BedId, "which bed is unknown, never invented");
            Assert.AreEqual(0d, place.WokeAtSeconds, "the wake instant is unknown, never taken from the autosave");
            Assert.IsFalse(place.Aboard, "a pre-v20 bunk recorded nothing, so nothing is aboard");
        }

        [Test]
        public void AV19SaveWithNoAnchor_StillWakesAtTheDefaultSpawn()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":19,\"GameTimeSeconds\":900.0}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsFalse(RestLocker.Place(save).IsSet, "never slept: the old default spawn stands");
            Assert.IsNotNull(save.RestBedId);
            Assert.IsNotNull(save.RestBoatHullId);
            Assert.IsNotNull(save.RestBoatRegion);
        }

        [Test]
        public void AV19ObjectWithNullStrings_IsRepaired()
        {
            var old = new SaveData { SchemaVersion = 19, RestBedId = null, RestBoatHullId = null, RestBoatRegion = null };
            SaveData save = SaveMigration.Migrate(old);
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual("", save.RestBedId);
            Assert.AreEqual("", save.RestBoatHullId);
            Assert.AreEqual("", save.RestBoatRegion);
        }

        [Test]
        public void AFullPlaceAboard_SurvivesTheRealJson_AndASecondMigration()
        {
            SaveData save = SaveMigration.NewGame();
            var place = new SleepPlace(new RestAnchor(NMC, new Vector2(40.5f, -12.25f), 1), "fixture.boat.bunk",
                                       98765.5d, true, "boat.cape_islander", NMC, new Vector2(39.75f, -13f), 92.5f);
            Assert.IsTrue(RestLocker.StampPlace(save, place));

            SaveData back = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));
            SleepPlace read = RestLocker.Place(back);

            Assert.IsTrue(read.IsSet);
            Assert.AreEqual(NMC, read.Anchor.Region);
            Assert.AreEqual(new Vector2(40.5f, -12.25f), read.Anchor.Position);
            Assert.AreEqual(1, read.Anchor.Level);
            Assert.AreEqual("fixture.boat.bunk", read.BedId);
            Assert.AreEqual(98765.5d, read.WokeAtSeconds);
            Assert.IsTrue(read.Aboard);
            Assert.AreEqual("boat.cape_islander", read.BoatHullId);
            Assert.AreEqual(NMC, read.BoatRegion);
            Assert.AreEqual(new Vector2(39.75f, -13f), read.BoatPosition);
            Assert.AreEqual(92.5f, read.BoatRotationDegrees);
        }

        // =====================================================================================
        //  the locker
        // =====================================================================================

        [Test]
        public void StampPlace_RefusesAnUnsetPlace_AndKeepsTheGoodOne()
        {
            SaveData save = SaveMigration.NewGame();
            RestLocker.StampPlace(save, new SleepPlace(new RestAnchor(StP, Vector2.one, 0), "fixture.interior.bed", 10d));

            Assert.IsFalse(RestLocker.StampPlace(save, SleepPlace.None));
            Assert.AreEqual("fixture.interior.bed", RestLocker.Place(save).BedId);
        }

        [Test]
        public void StampPlace_TheSamePlaceTwice_IsNotAChange()
        {
            SaveData save = SaveMigration.NewGame();
            var place = new SleepPlace(new RestAnchor(StP, Vector2.one, 0), "fixture.interior.bed", 10d);
            Assert.IsTrue(RestLocker.StampPlace(save, place));
            Assert.IsFalse(RestLocker.StampPlace(save, place));
        }

        [Test]
        public void ABedAshore_ClearsLastNightsBoat()
        {
            SaveData save = SaveMigration.NewGame();
            RestLocker.StampPlace(save, new SleepPlace(new RestAnchor(StP, Vector2.one, 1), "fixture.boat.bunk", 5d,
                                                       true, "boat.dory", StP, new Vector2(2f, 2f), 10f));
            RestLocker.StampPlace(save, new SleepPlace(new RestAnchor(StP, Vector2.zero, 1), "fixture.interior.bed", 9d));

            SleepPlace read = RestLocker.Place(save);
            Assert.IsFalse(read.Aboard);
            Assert.AreEqual("", read.BoatHullId, "an ashore place carries no boat");
            Assert.AreEqual(Vector2.zero, read.BoatPosition);
        }

        [Test]
        public void Forget_ClearsTheWholePlace()
        {
            SaveData save = SaveMigration.NewGame();
            RestLocker.StampPlace(save, new SleepPlace(new RestAnchor(StP, Vector2.one, 1), "fixture.boat.bunk", 5d,
                                                       true, "boat.dory", StP, new Vector2(2f, 2f), 10f));
            Assert.IsTrue(RestLocker.Forget(save));
            Assert.IsFalse(RestLocker.Place(save).IsSet);
            Assert.AreEqual("", save.RestBedId);
            Assert.IsFalse(save.RestAboard);
            Assert.AreEqual("", save.RestBoatHullId);
            Assert.AreEqual(0d, save.RestWokeAtSeconds);
        }

        // =====================================================================================
        //  a sleep is a save, and the reload restores it
        // =====================================================================================

        [Test]
        public void ARest_RecordsTheBed_TheRegion_ThePosition_AndTheWakeInstant()
        {
            var service = new FakeSaveService();
            GameServices.Save = service;
            GameServices.CurrentRegionId = StP;
            var clock = new MovingClock { T = GameServices.SecondsPerDay * (22d / 24d) };
            GameServices.Clock = clock;

            RestSaveResponder.Fulfil(new RestSaveRequested("your bed", new Vector2(3f, 4f), 1, "fixture.interior.bed"));

            SleepPlace place = RestLocker.Place(service.Current);
            Assert.AreEqual(1, service.Saves, "the sleep wrote the save");
            Assert.AreEqual(StP, place.Anchor.Region);
            Assert.AreEqual(new Vector2(3f, 4f), place.Anchor.Position);
            Assert.AreEqual(1, place.Anchor.Level);
            Assert.AreEqual("fixture.interior.bed", place.BedId);
            Assert.AreEqual(clock.T, place.WokeAtSeconds, "the wake instant is the clock AFTER the sleep");
            Assert.Greater(clock.T, GameServices.SecondsPerDay * (22d / 24d), "and the sleep did pass time");
        }

        [Test]
        public void TheReload_PutsThePlayerBackWhereTheySlept()
        {
            // Write the place the way play does, round-trip it through the real JSON, then load it.
            var writer = new FakeSaveService();
            GameServices.Save = writer;
            GameServices.CurrentRegionId = StP;
            RestSaveResponder.Fulfil(new RestSaveRequested("your bed", new Vector2(7.5f, -1.25f), 1, "fixture.interior.bed"));
            string json = SaveSerialization.ToJson(writer.Current);

            var reader = new FakeSaveService { Current = SaveSerialization.FromJson(json) };
            GameServices.Save = reader;
            Transform player = StandPlayerAt(new Vector2(100f, 100f));   // wherever the boot put them

            RestAnchor woke = RestWakeRestorer.Wake();

            Assert.IsTrue(woke.IsSet, "woken where they slept");
            Assert.AreEqual(new Vector3(7.5f, -1.25f, -3.5f), player.position, "the bedside spot, depth untouched");
            Assert.AreEqual(1, woke.Level, "on the storey they slept on");
            Assert.AreEqual(0, _wakeRequests.Count, "same region ashore needs no travel");
        }

        [Test]
        public void SleepingInAnotherRegion_AsksTheTravelSeamToTakeThePlayerThere()
        {
            var service = new FakeSaveService();
            RestLocker.StampPlace(service.Current,
                                  new SleepPlace(new RestAnchor(NMC, new Vector2(1f, 2f), 0), "fixture.interior.bed", 42d));
            GameServices.Save = service;
            GameServices.CurrentRegionId = StP;
            Transform player = StandPlayerAt(new Vector2(9f, 9f));

            Assert.IsFalse(RestWakeRestorer.Wake().IsSet, "not applied in the wrong region's frame");
            Assert.AreEqual(new Vector3(9f, 9f, -3.5f), player.position, "the player is not moved");
            Assert.AreEqual(1, _wakeRequests.Count, "the travel seam is asked once");
            Assert.IsTrue(_wakeRequests[0].OtherRegion);
            Assert.AreEqual(NMC, _wakeRequests[0].Place.Anchor.Region);
            Assert.AreEqual(42d, _wakeRequests[0].Place.WokeAtSeconds);
            Assert.IsTrue(RestLocker.Place(service.Current).IsSet, "and the save keeps the place");
        }

        [Test]
        public void SleepingAboard_IsDeclinedOnLoad_AndHandedToTheSeam()
        {
            var service = new FakeSaveService();
            RestLocker.StampPlace(service.Current,
                                  new SleepPlace(new RestAnchor(StP, new Vector2(1f, 2f), 1), "fixture.boat.bunk", 42d,
                                                 true, "boat.dory", StP, new Vector2(1f, 1f), 0f));
            GameServices.Save = service;
            GameServices.CurrentRegionId = StP;
            Transform player = StandPlayerAt(new Vector2(9f, 9f));

            Assert.IsFalse(RestWakeRestorer.Wake().IsSet);
            Assert.AreEqual(new Vector3(9f, 9f, -3.5f), player.position, "never onto a sole whose boat is elsewhere");
            Assert.AreEqual(1, _wakeRequests.Count);
            Assert.IsFalse(_wakeRequests[0].OtherRegion);
            Assert.IsTrue(_wakeRequests[0].Place.Aboard);
        }
    }
}
