using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Sitting and dining (kitchens slice 1, owner ask 2026-11-15: "players can also seat and dine which
    /// gives them a greater food boost, sitting also recovers tiredness to maximum amount that does not
    /// match sleeping"). Pins: a meal is worth more seated and most at a table; a dining seat is one with a
    /// table or counter on its floor within reach; sitting brings tiredness down to a floor strictly above
    /// where a night's sleep lands, never below it; seated rest stepped equals seated rest in one go; and
    /// the needs service and the seat session say all of it on the bus.
    /// </summary>
    public class DiningAndSeatedRestTests
    {
        const double PerHour = 100d;   // game seconds per game hour

        sealed class FakeSaveService : ISaveService
        {
            public SaveData Current { get; set; } = SaveMigration.NewGame();
            public bool LoadedExistingSave => false;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

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

        readonly List<PlayerDined> _dined = new List<PlayerDined>();
        readonly List<PlayerSat> _sat = new List<PlayerSat>();
        readonly List<PlayerStood> _stood = new List<PlayerStood>();
        System.Action<PlayerDined> _onDined;
        System.Action<PlayerSat> _onSat;
        System.Action<PlayerStood> _onStood;

        [SetUp]
        public void SetUp()
        {
            _dined.Clear(); _sat.Clear(); _stood.Clear();
            _onDined = _dined.Add; _onSat = _sat.Add; _onStood = _stood.Add;
            EventBus.Subscribe(_onDined);
            EventBus.Subscribe(_onSat);
            EventBus.Subscribe(_onStood);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onDined);
            EventBus.Unsubscribe(_onSat);
            EventBus.Unsubscribe(_onStood);
        }

        static NeedsService Service(FakeSaveService save, MovingClock clock)
            => new NeedsService(() => save, () => clock, () => NeedsSettings.Default, () => null, () => PerHour,
                                () => KitchenSettings.Default);

        // ---- the rules ------------------------------------------------------------------------------

        [Test]
        public void SittingsFloorIsStrictlyAboveWhereSleepLands()
        {
            KitchenSettings s = KitchenSettings.Default;
            Assert.Greater(s.SitRestFloorInForce, 0f, "sleep brings tiredness to 0; sitting must never match it");
            Assert.Greater(new KitchenSettings { SitRestFloor = 0f }.SitRestFloorInForce, 0f,
                           "an unset floor reads as the default, never as 'sitting is a bed'");
        }

        [Test]
        public void AMealIsWorthMoreSeated_AndMostAtATable()
        {
            KitchenSettings s = KitchenSettings.Default;
            float standing = DiningRules.Relief(0.2f, EatingPosture.Standing, s);
            float seated = DiningRules.Relief(0.2f, EatingPosture.Seated, s);
            float atTable = DiningRules.Relief(0.2f, EatingPosture.AtTable, s);
            Assert.AreEqual(0.2f, standing, 1e-6f, "on your feet a meal is its own worth");
            Assert.Greater(seated, standing);
            Assert.Greater(atTable, seated, "the greater food boost of sitting down to eat");

            var lowDining = new KitchenSettings { SeatedReliefMultiplier = 1.4f, DiningReliefMultiplier = 1.2f };
            Assert.GreaterOrEqual(DiningRules.Multiplier(EatingPosture.AtTable, lowDining),
                                  DiningRules.Multiplier(EatingPosture.Seated, lowDining),
                                  "a table never does worse than a bare seat");
        }

        [Test]
        public void ADiningSeatHasATableOrCounterOnItsFloorWithinReach()
        {
            var seat = new KitchenPiece("seat", KitchenPieceKind.Seat, "house", new Vector3(0f, 0f, 1f), Vector3.zero);
            var nearTable = new KitchenPiece("table", KitchenPieceKind.Table, "house", new Vector3(1.0f, 0f, 1.2f), Vector3.zero);
            var farTable = new KitchenPiece("table", KitchenPieceKind.Table, "house", new Vector3(2.0f, 0f, 1.2f), Vector3.zero);
            var otherFloor = new KitchenPiece("table", KitchenPieceKind.Table, "deck", new Vector3(0.5f, 0f, 1.2f), Vector3.zero);
            var counter = new KitchenPiece("prep", KitchenPieceKind.Prep, "house", new Vector3(0f, 1.1f, 0f), Vector3.zero);
            var stove = new KitchenPiece("cook", KitchenPieceKind.Cook, "house", new Vector3(0f, 0.5f, 0f), Vector3.zero);
            KitchenSettings s = KitchenSettings.Default;

            Assert.IsTrue(KitchenLayout.IsDiningSeat(seat, new[] { nearTable }, s));
            Assert.IsFalse(KitchenLayout.IsDiningSeat(seat, new[] { farTable }, s), "too far to eat at");
            Assert.IsFalse(KitchenLayout.IsDiningSeat(seat, new[] { otherFloor }, s), "another floor");
            Assert.IsTrue(KitchenLayout.IsDiningSeat(seat, new[] { counter }, s), "you eat at the counter you cooked on");
            Assert.IsFalse(KitchenLayout.IsDiningSeat(seat, new[] { stove }, s), "a stove is not a table");
            Assert.IsFalse(KitchenLayout.IsDiningSeat(nearTable, new[] { nearTable }, s), "only a seat dines");
        }

        [Test]
        public void SittingNeverBringsTirednessBelowTheFloor()
        {
            KitchenSettings s = KitchenSettings.Default;
            float floor = s.SitRestFloorInForce;
            float rise = NeedsSettings.Default.Tiredness.RisePerGameHour;
            Assert.AreEqual(0.8f - s.SitRecoveryInForce, SeatedRest.LevelAfter(0.8f, 1d, rise, s), 1e-6f);
            Assert.AreEqual(floor, SeatedRest.LevelAfter(0.8f, 100d, rise, s), 1e-6f, "a long sit stops at the floor");
            Assert.AreEqual(floor, SeatedRest.LevelAfter(0.1f, 100d, rise, s), 1e-6f,
                            "rested already: sitting does not keep you rested past the floor");
            Assert.AreEqual(0.1f + rise, SeatedRest.LevelAfter(0.1f, 1d, rise, s), 1e-6f, "below it, you tire as normal");
        }

        // ---- through the needs service --------------------------------------------------------------

        [Test]
        public void EatingAtATableTakesTheDiningBoostOffHunger_AndSaysSo()
        {
            var save = new FakeSaveService();
            var clock = new MovingClock { T = 500d };
            save.Current.PlayerNeeds.Hunger = new NeedStateDto(0.8f, 500d);
            NeedsService needs = Service(save, clock);

            needs.Eat(0.2f, EatingPosture.AtTable);
            float relief = 0.2f * KitchenSettings.Default.DiningReliefInForce;
            Assert.AreEqual(0.8f - relief, needs.Level(NeedKind.Hunger), 1e-5f);
            Assert.AreEqual(1, _dined.Count);
            Assert.AreEqual(EatingPosture.AtTable, _dined[0].Posture);
            Assert.AreEqual(relief, _dined[0].Relief, 1e-6f);

            save.Current.PlayerNeeds.Hunger = new NeedStateDto(0.8f, 500d);
            needs.Eat(0.2f, EatingPosture.Standing);
            Assert.AreEqual(0.6f, needs.Level(NeedKind.Hunger), 1e-5f, "standing, the meal's own worth");
            Assert.AreEqual(1, _dined.Count, "eating on your feet is not dining");
        }

        [Test]
        public void SeatedRestSteppedEqualsSeatedRestInOneGo([Values(0.8f, 0.1f)] float start)
        {
            var saveA = new FakeSaveService();
            var saveB = new FakeSaveService();
            var clockA = new MovingClock();
            var clockB = new MovingClock();
            saveA.Current.PlayerNeeds.Tiredness = new NeedStateDto(start, 0d);
            saveB.Current.PlayerNeeds.Tiredness = new NeedStateDto(start, 0d);
            NeedsService stepped = Service(saveA, clockA);
            NeedsService oneGo = Service(saveB, clockB);

            double banked = 0d;
            for (int i = 1; i <= 12; i++)
            {
                clockA.T = i * 0.25d * PerHour;   // bank every quarter hour for three hours
                stepped.RestSeated(banked);
                banked = clockA.T;
            }
            clockB.T = 3d * PerHour;
            oneGo.RestSeated(0d);

            Assert.AreEqual(oneGo.Level(NeedKind.Tiredness), stepped.Level(NeedKind.Tiredness), 1e-5f);
            Assert.GreaterOrEqual(stepped.Level(NeedKind.Tiredness), KitchenSettings.Default.SitRestFloorInForce - 1e-6f);
        }

        [Test]
        public void ANightsSleepStillBeatsTheLongestSit()
        {
            var save = new FakeSaveService();
            var clock = new MovingClock();
            save.Current.PlayerNeeds.Tiredness = new NeedStateDto(0.9f, 0d);
            NeedsService needs = Service(save, clock);

            clock.T = 48d * PerHour;
            needs.RestSeated(0d);
            float sat = needs.Level(NeedKind.Tiredness);
            needs.Sleep();
            float slept = needs.Level(NeedKind.Tiredness);
            Assert.Greater(sat, slept, "sitting takes the edge off; only the bunk takes it all");
        }

        [Test]
        public void TheSeatSessionSitsBanksAndStands()
        {
            var save = new FakeSaveService();
            var clock = new MovingClock { T = 1000d };
            save.Current.PlayerNeeds.Tiredness = new NeedStateDto(0.8f, 1000d);
            NeedsService needs = Service(save, clock);
            var session = new SeatSession(needs, () => clock);

            session.Sit("fixture.boat.test.seat", atDiningSeat: true);
            Assert.AreEqual(1, _sat.Count);
            Assert.IsTrue(_sat[0].AtDiningSeat);
            Assert.AreEqual(EatingPosture.AtTable, session.Posture);

            clock.T = 1000d + 1d * PerHour;
            session.Stand();
            Assert.AreEqual(1, _stood.Count);
            Assert.AreEqual(PerHour, _stood[0].SeatedGameSeconds, 1e-9);
            Assert.AreEqual(0.8f - KitchenSettings.Default.SitRecoveryInForce, needs.Level(NeedKind.Tiredness), 1e-5f);
            Assert.AreEqual(EatingPosture.Standing, session.Posture);
        }
    }
}
