using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Acceptance contracts from clock-rollover-semantics: assertions run AFTER dispatch,
    /// because the bus logs subscriber exceptions and continues delivery.</summary>
    public class GameClockRolloverTests
    {
        readonly List<Object> _objects = new List<Object>();
        readonly List<Action> _unsubscribe = new List<Action>();
        readonly List<object[]> _trace = new List<object[]>();
        GameClock _clock;
        GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _objects.Add(_config);
            _config.SecondsPerDay = 1800f;
            _config.DaysPerSeason = 28;
            _config.StartSeason = Season.HighSummer;
            _config.StartDayOfSeason = 1;
            _config.DaysPerWeek = 7;
            _config.MarketDayIndex = 4;
            _config.MarketDailyRecovery = 0.5f;
            _clock = Component<GameClock>();
            SetField("_config", _config);
            _clock.SeekTo(1650d);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _unsubscribe.Count - 1; i >= 0; i--) _unsubscribe[i]();
            _unsubscribe.Clear();
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            _trace.Clear();
        }

        T Component<T>() where T : UnityEngine.Component
        {
            var root = new GameObject(typeof(T).Name);
            root.SetActive(false);
            _objects.Add(root);
            return root.AddComponent<T>();
        }

        void SetField(string name, object value) => typeof(GameClock)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_clock, value);

        void Listen<T>(Action<T> handler)
        {
            EventBus.Subscribe(handler);
            _unsubscribe.Add(() => EventBus.Unsubscribe(handler));
        }

        Market ListeningMarket()
        {
            var market = Component<Market>();
            market.Configure(_config, MarketId.Cove);
            // Exercise the production subscription, including its duplicate-enable protection.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Market).GetMethod("OnEnable", flags).Invoke(market, null);
            _unsubscribe.Add(() => typeof(Market).GetMethod("OnDisable", flags).Invoke(market, null));
            return market;
        }

        object[] Read(string kind, int payloadDay, Season season, int year) => new object[]
        {
            kind, payloadDay, season, year, _clock.DayIndex, _clock.TotalSeconds,
            _clock.FlowSeconds, _clock.Now.TotalSeconds, _clock.DayOfSeason, _clock.Season,
            _clock.Year, _clock.Weekday, _clock.IsMarketDay, _clock.HourOfDay, _clock.DayFraction
        };

        void Observe()
        {
            Listen<DayStarted>(e => _trace.Add(Read("day", e.DayOfSeason, e.Season, e.Year)));
            Listen<SeasonChanged>(e => _trace.Add(Read("season", 0, e.Season, e.Year)));
        }

        object[] Expected(int day, string kind = "day", double gap = 0d)
        {
            var date = GameCalendar.AtDay(day, _config.DaysPerSeason, _config.StartDayOfYear);
            double midnight = day * (double)_config.SecondsPerDay;
            return new object[]
            {
                kind, kind == "day" ? date.DayOfSeason : 0, date.Season, date.Year,
                day, midnight, midnight + gap, midnight, date.DayOfSeason, date.Season,
                date.Year, (Weekday)(day % _config.DaysPerWeek),
                day % _config.DaysPerWeek == _config.MarketDayIndex, 0f, 0f
            };
        }

        void Trace(params object[][] expected) => Assert.That(_trace, Is.EqualTo(expected),
            "ordered payload, day/time/flow/Now, calendar, weekday/market and hour/fraction reads");

        void End(double time, double flow)
        {
            Assert.AreEqual(time, _clock.TotalSeconds);
            Assert.AreEqual(time, _clock.Now.TotalSeconds);
            Assert.AreEqual(flow, _clock.FlowSeconds);
        }

        void Move(bool fast, double target)
        {
            if (fast) Assert.IsTrue(_clock.FastForwardTo(target));
            else _clock.Advance((float)(target - _clock.TotalSeconds));
        }

        void Gap()
        {
            _clock.IsHeld = true;
            _clock.Advance(10f);
            _clock.IsHeld = false;
        }

        [Test]
        public void NoBoundaryAndZeroStepPreserveTheFloatProductBeforeWidening()
        {
            Observe();
            _clock.TimeScale = 3.7f;
            double step = 0.1f * 3.7f;
            _clock.Advance(0f);
            End(1650d, 1650d);
            _clock.Advance(0.1f);
            End(1650d + step, 1650d + step);
            Trace();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AConfiguredClockDoesNotAnnounceDayZero(bool fast)
        {
            _clock = Component<GameClock>();
            SetField("_config", _config); // No Awake or SeekTo: first-use trackers must rebaseline.
            Observe();
            _clock.Advance(0f);
            Trace();
            Move(fast, 1800d);
            Trace(Expected(1));
            End(1800d, 1800d);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OneMidnightWithOvershootReportsTheBoundaryThenRestoresTheRemainder(bool fast)
        {
            Observe();
            Move(fast, 1900d);
            Trace(Expected(1));
            End(1900d, 1900d);
        }

        [TestCase("advance")]
        [TestCase("fast")]
        [TestCase("partitioned")]
        public void ThreeMidnightsHaveTheSameTraceSupplyBitsAndQuote(string path)
        {
            _config.MarketDemandCove = 1f;
            _config.MarketPriceLevelCove = 1f;
            var market = ListeningMarket();
            market.RegisterSale(FishCategory.Pelagic, 8);
            var supply = new List<float>();
            Listen<DayStarted>(_ => supply.Add(market.SupplyOf(FishCategory.Pelagic)));
            Observe();
            if (path == "partitioned")
                foreach (float step in new[] { 150f, 1800f, 1800f, 450f }) _clock.Advance(step);
            else Move(path == "fast", 5850d);

            Trace(Expected(1), Expected(2), Expected(3));
            Assert.That(supply, Is.EqualTo(new[] { 4f, 2f, 1f }));
            Assert.That(BitConverter.GetBytes(market.SupplyOf(FishCategory.Pelagic)),
                Is.EqualTo(BitConverter.GetBytes(1f)));
            Assert.AreEqual(50, market.NextUnitPrice(FishCategory.Pelagic, 100, 1f));
            End(5850d, 5850d);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExactEndpointIsIncludedOnceAndStartingMidnightIsExcluded(bool fast)
        {
            _clock.SeekTo(1800d);
            Observe();
            Move(fast, 5400d);
            Trace(Expected(2), Expected(3));
            End(5400d, 5400d);
            _clock.Advance(0f);
            _clock.Advance(1f);
            Trace(Expected(2), Expected(3));
            End(5401d, 5401d);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AFullYearAnnouncesEveryDayAndFourOrderedSeasonTurns(bool fast)
        {
            Observe();
            Move(fast, 1650d + 112d * 1800d);
            var expected = new List<object[]>();
            for (int day = 1; day <= 112; day++)
            {
                expected.Add(Expected(day));
                if (day % 28 == 0) expected.Add(Expected(day, "season"));
            }
            Trace(expected.ToArray());
            Assert.AreEqual(116, _trace.Count);
            Assert.That(new[] { _trace[28][2], _trace[57][2], _trace[86][2], _trace[115][2] },
                Is.EqualTo(new[] { Season.TheTurn, Season.HardWinter, Season.EarlySpring, Season.HighSummer }));
            Assert.That(new[] { _trace[28][3], _trace[57][3], _trace[86][3], _trace[115][3] },
                Is.EqualTo(new[] { 1, 1, 2, 2 }));
            End(203250d, 203250d);
            Assert.AreEqual(2, _clock.Year);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AuthoredOffsetLengthsAndWeekKeepTheirOwnCoordinates(bool fast)
        {
            _config.DaysPerSeason = 3;
            _config.StartSeason = Season.HardWinter;
            _config.StartDayOfSeason = 99; // Existing normalization clamps to the season's last day.
            _config.DaysPerWeek = 5;
            _config.MarketDayIndex = 2;
            _clock.SeekTo(1650d);
            Observe();
            Move(fast, 7200d);
            Trace(Expected(1), Expected(1, "season"), Expected(2), Expected(3),
                Expected(4), Expected(4, "season"));
            Assert.That(new[] { _trace[0][1], _trace[0][2], _trace[0][3], _trace[0][11] },
                Is.EqualTo(new object[] { 1, Season.EarlySpring, 2, Weekday.Tuesday }));
            Assert.AreEqual(true, _trace[2][12]);
            Assert.AreEqual(Season.HighSummer, _clock.Season);
            Assert.AreEqual(1, _clock.DayOfSeason);
            End(7200d, 7200d);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingFlowGapSurvivesEveryBoundaryAndCompletion(bool fast)
        {
            Gap();
            Observe();
            Move(fast, 5850d);
            Trace(Expected(1, gap: 10d), Expected(2, gap: 10d), Expected(3, gap: 10d));
            End(5850d, 5860d);
        }

        [Test]
        public void HoldPauseAndAbsoluteFastForwardKeepTheirDifferentGates()
        {
            Observe();
            _clock.IsHeld = true;
            _clock.Advance(4200f);
            End(1650d, 5850d);
            Trace();
            Assert.IsFalse(_clock.FastForwardTo(5850d));
            _clock.IsPaused = true;
            _clock.Advance(100f);
            End(1650d, 5850d);
            _clock.IsHeld = false;
            _clock.Advance(100f);
            End(1650d, 5850d);
            _clock.TimeScale = -3f;
            Assert.IsTrue(_clock.FastForwardTo(5850d));
            Trace(Expected(1, gap: 4200d), Expected(2, gap: 4200d), Expected(3, gap: 4200d));
            End(5850d, 10050d);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SeekResetsFlowWithoutSettlementAndNextStepDoesNotReplay(bool fast)
        {
            var market = ListeningMarket();
            market.RegisterSale(FishCategory.Pelagic, 8);
            Gap();
            Observe();
            _clock.SeekTo(5400d);
            _clock.Advance(0f);
            End(5400d, 5400d);
            Trace();
            Assert.AreEqual(8f, market.SupplyOf(FishCategory.Pelagic));
            Move(fast, 7200d);
            Trace(Expected(4));
            Assert.AreEqual(4f, market.SupplyOf(FishCategory.Pelagic));
            End(7200d, 7200d);
            _clock.SeekTo(-1d);
            End(0d, 0d);
            Trace(Expected(4));
        }

        // Adjacent representable doubles, with no epsilon. All boundaries here are positive.
        static double Neighbor(double value, int direction) =>
            BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + direction);

        [TestCase(0.1f, 1)]
        [TestCase(0.1f, 1000000001)]
        [TestCase(0.7f, 1073741825)]
        [TestCase(1234.567f, 2147483646)]
        public void FractionalCanonicalMidnightAndItsNeighborsAgreeWithEveryGetter(float length, int day)
        {
            _config.SecondsPerDay = length;
            double boundary = day * (double)length;
            _clock.SeekTo(Neighbor(boundary, -1));
            Assert.AreEqual(day - 1, _clock.DayIndex);
            Observe();
            Assert.IsTrue(_clock.FastForwardTo(boundary));
            var expected = new List<object[]> { Expected(day) };
            if (GameCalendar.AtDay(day - 1, 28, 28).Season != GameCalendar.AtDay(day, 28, 28).Season)
                expected.Add(Expected(day, "season"));
            Trace(expected.ToArray());
            End(boundary, boundary);
            Assert.AreEqual(day, _clock.DayIndex);
            Assert.AreEqual(0f, _clock.HourOfDay);
            Assert.AreEqual(0f, _clock.DayFraction);
            Assert.IsTrue(_clock.FastForwardTo(Neighbor(boundary, 1)));
            _clock.Advance(0f);
            Trace(expected.ToArray());
            Assert.AreEqual(day, _clock.DayIndex);
            Assert.Greater(_clock.DayFraction, 0f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FractionalMidnightReachedByEitherEntryKeepsItsIntegerIdentity(bool fast)
        {
            _config.SecondsPerDay = 0.7f;
            _clock.SeekTo(0d);
            Observe();
            if (fast) Assert.IsTrue(_clock.FastForwardTo((double)_config.SecondsPerDay * 3));
            else
            {
                _clock.Advance(_config.SecondsPerDay);
                _clock.Advance(_config.SecondsPerDay);
                _clock.Advance(_config.SecondsPerDay);
            }
            Trace(Expected(1), Expected(2), Expected(3));
            End((double)_config.SecondsPerDay * 3, (double)_config.SecondsPerDay * 3);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidWallDeltaIsAtomic(float input)
        {
            Observe();
            _clock.Advance(input);
            End(1650d, 1650d);
            Trace();
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.MaxValue)]
        public void InvalidRateOrFloatProductOverflowIsAtomic(float rate)
        {
            Observe();
            _clock.TimeScale = rate;
            _clock.Advance(2f);
            End(1650d, 1650d);
            Trace();
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void FiniteNonpositiveRateClampsToZero(float rate)
        {
            Observe();
            _clock.TimeScale = rate;
            _clock.Advance(4200f);
            End(1650d, 1650d);
            Trace();
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(double.MaxValue)]
        public void InvalidSeekOrFastTargetCannotPoisonTheNextAdvance(double target)
        {
            Observe();
            Assert.IsFalse(_clock.FastForwardTo(target));
            _clock.SeekTo(target);
            End(1650d, 1650d);
            Trace();
            _clock.Advance(150f);
            Trace(Expected(1));
            End(1800d, 1800d);
        }

        [TestCase(-1d)]
        [TestCase(0d)]
        [TestCase(1650d)]
        public void NonForwardTargetRefusesWithoutSettlement(double target)
        {
            Observe();
            Assert.IsFalse(_clock.FastForwardTo(target));
            End(1650d, 1650d);
            Trace();
        }

        [TestCase("length-zero")]
        [TestCase("length-negative")]
        [TestCase("length-nan")]
        [TestCase("length-infinite")]
        [TestCase("week-zero")]
        [TestCase("week-negative")]
        [TestCase("season-zero")]
        [TestCase("season-negative")]
        [TestCase("offset-overflow")]
        [TestCase("missing")]
        public void InvalidConfigurationRefusesTheWholeRequest(string problem)
        {
            int events = 0;
            Listen<DayStarted>(_ => events++);
            Listen<SeasonChanged>(_ => events++);
            switch (problem)
            {
                case "length-zero": _config.SecondsPerDay = 0f; break;
                case "length-negative": _config.SecondsPerDay = -1f; break;
                case "length-nan": _config.SecondsPerDay = float.NaN; break;
                case "length-infinite": _config.SecondsPerDay = float.PositiveInfinity; break;
                case "week-zero": _config.DaysPerWeek = 0; break;
                case "week-negative": _config.DaysPerWeek = -1; break;
                case "season-zero": _config.DaysPerSeason = 0; break;
                case "season-negative": _config.DaysPerSeason = -1; break;
                case "offset-overflow": _config.DaysPerSeason = int.MaxValue; _config.StartSeason = Season.HardWinter; break;
                case "missing": SetField("_config", null); break;
            }
            _clock.Advance(1f);
            End(1650d, 1650d);
            Assert.IsFalse(_clock.FastForwardTo(1651d));
            End(1650d, 1650d);
            Assert.AreEqual(0, events);
        }

        [TestCase("_t", double.NaN)]
        [TestCase("_t", double.PositiveInfinity)]
        [TestCase("_t", double.NegativeInfinity)]
        [TestCase("_t", -1d)]
        [TestCase("_t", double.MaxValue)]
        [TestCase("_flow", double.NaN)]
        [TestCase("_flow", double.PositiveInfinity)]
        [TestCase("_flow", double.NegativeInfinity)]
        public void InvalidStateRefusesWithoutPartialChanges(string field, double value)
        {
            SetField(field, value);
            double time = _clock.TotalSeconds, flow = _clock.FlowSeconds;
            int events = 0;
            Listen<DayStarted>(_ => events++);
            Listen<SeasonChanged>(_ => events++);
            _clock.Advance(1f);
            End(time, flow);
            Assert.IsFalse(_clock.FastForwardTo(1800d));
            End(time, flow);
            Assert.AreEqual(0, events);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LastRepresentableDayDoesNotOverflowTheLoopAndNextDayIsRejected(bool fast)
        {
            _config.SecondsPerDay = 1f;
            _clock.SeekTo(int.MaxValue - 0.5d);
            Observe();
            Move(fast, int.MaxValue);
            Trace(Expected(int.MaxValue));
            End(int.MaxValue, int.MaxValue);
            _clock.Advance(1f);
            Assert.IsFalse(_clock.FastForwardTo((double)int.MaxValue + 1d));
            _clock.SeekTo((double)int.MaxValue + 1d);
            Trace(Expected(int.MaxValue));
            End(int.MaxValue, int.MaxValue);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedMovesRefuseAndChangedFlagsApplyOnlyToTheNextCall(bool fast)
        {
            _config.DaysPerSeason = 1;
            _clock.SeekTo(1650d);
            var nestedResults = new List<bool>();
            void Mutate()
            {
                _clock.Advance(1f);
                nestedResults.Add(_clock.FastForwardTo(_clock.TotalSeconds + 1d));
                _clock.SeekTo(42d);
                _clock.IsHeld = true;
                _clock.IsPaused = true;
                _clock.TimeScale = 2f;
            }
            Listen<DayStarted>(_ => Mutate());
            Listen<SeasonChanged>(_ => Mutate());
            Observe();
            Move(fast, 5850d);
            Trace(Expected(1), Expected(1, "season"), Expected(2), Expected(2, "season"),
                Expected(3), Expected(3, "season"));
            Assert.That(nestedResults, Is.EqualTo(new[] { false, false, false, false, false, false }));
            End(5850d, 5850d);
            Assert.IsTrue(_clock.IsHeld);
            Assert.IsTrue(_clock.IsPaused);
            Assert.AreEqual(2f, _clock.TimeScale);
            _clock.Advance(1f);
            End(5850d, 5850d);
            _clock.IsPaused = false;
            Assert.IsFalse(_clock.FastForwardTo(6000d));
            _clock.Advance(1f);
            End(5850d, 5852d);
            _clock.IsHeld = false;
            _clock.Advance(1f);
            End(5852d, 5854d);
            Assert.AreEqual(6, _trace.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AThrowingHandlerIsLoggedOnceAndDoesNotStopDeliveryOrLeaveTheGuardSet(bool fast)
        {
            int calls = 0;
            Listen<DayStarted>(_ => { if (++calls == 1) throw new InvalidOperationException("rollover-test"); });
            Observe();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: rollover-test"));
            Move(fast, 5850d);
            Trace(Expected(1), Expected(2), Expected(3));
            Assert.AreEqual(3, calls);
            End(5850d, 5850d);
            _clock.SeekTo(7100d);
            Move(fast, 7200d);
            Trace(Expected(1), Expected(2), Expected(3), Expected(4));
            End(7200d, 7200d);
            Assert.AreEqual(4, calls);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(false, 0f)]
        [TestCase(true, 0f)]
        [TestCase(false, 0.5f)]
        [TestCase(true, 0.5f)]
        [TestCase(false, 1f)]
        [TestCase(true, 1f)]
        public void MarketSubscribersKeepTheirOrderAndCategoriesSettleOncePerBoundary(bool fast, float recovery)
        {
            _config.MarketDailyRecovery = recovery;
            Market market = null;
            var reads = new List<float[]>();
            Listen<DayStarted>(_ => reads.Add(new[] { market.SupplyOf(FishCategory.Pelagic), market.SupplyOf(FishCategory.InshoreGroundfish) }));
            market = ListeningMarket();
            var empty = ListeningMarket();
            market.RegisterSale(FishCategory.Pelagic, 8);
            market.RegisterSale(FishCategory.InshoreGroundfish, 4);
            Listen<DayStarted>(_ => reads.Add(new[] { market.SupplyOf(FishCategory.Pelagic), market.SupplyOf(FishCategory.InshoreGroundfish) }));
            Observe();
            Move(fast, 5850d);
            var expected = new List<float[]>();
            float a = 8f, b = 4f;
            for (int day = 1; day <= 3; day++)
            {
                expected.Add(new[] { a, b });
                a *= 1f - recovery; b *= 1f - recovery;
                expected.Add(new[] { a, b });
            }
            Assert.That(reads, Is.EqualTo(expected));
            Trace(Expected(1), Expected(2), Expected(3));
            Assert.AreEqual(a, market.SupplyOf(FishCategory.Pelagic));
            Assert.AreEqual(b, market.SupplyOf(FishCategory.InshoreGroundfish));
            Assert.AreEqual(0f, empty.SupplyOf(FishCategory.Pelagic));
            Assert.AreEqual(0f, empty.SupplyOf(FishCategory.InshoreGroundfish));
            End(5850d, 5850d);
        }
    }
}
