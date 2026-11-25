using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    // Audit-only guards: explicit-input models must survive unrelated queries and backward seeks.
    // These tests do not promise replay of player inputs, physics, or mutable world transactions.
    public class SimulationReplayAuditTests
    {
        [TestCase(0)]
        [TestCase(42)]
        [TestCase(-17)]
        [TestCase(int.MaxValue)]
        public void IndependentRandomChannels_ReplayAfterOtherWorldsAndReverseQueries(int seed)
        {
            var expected = new (int Action, int Animal, uint School, float Pot)[64];
            for (int i = 0; i < expected.Length; i++) expected[i] = Channels(seed, i);
            for (int i = expected.Length - 1; i >= 0; i--)
            {
                Channels(unchecked(seed + 1), i + 100);
                Assert.That(Channels(seed, i), Is.EqualTo(expected[i]), $"channel set {i}");
            }
        }

        private static (int, int, uint, float) Channels(int seed, int index)
            => (StableHash.CatchSeed(seed, "audit.cast", index * 75.125),
                TrapFill.AnimalCatchSeed(seed, "audit.pot", 100.25, index),
                FishSchoolMath.CellSlotKey(seed, "region.audit", index - 32, -7, index / 4),
                NpcPotDay.Hash01(seed, "potday.audit", index / 4, index % 4, 3));

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(-17)]
        [TestCase(int.MaxValue)]
        public void PotCounts_ReconstructAtEveryFillStepWithoutPreviousHauls(int seed)
        {
            const int capacity = 12;
            var waited = new int[65];
            for (int i = 0; i < waited.Length; i++)
                waited[i] = TrapFill.ResolveCount(i / 64f, capacity, seed, "audit.pot", 100.25);
            Assert.That(waited[0], Is.EqualTo(1));
            Assert.That(waited[64], Is.EqualTo(capacity));
            for (int i = 64; i >= 0; i--)
            {
                TrapFill.ResolveCount(1f, 19, unchecked(seed + 1), "other.pot", 500.5);
                Assert.That(TrapFill.ResolveCount(i / 64f, capacity, seed, "audit.pot", 100.25),
                    Is.EqualTo(waited[i]), $"fill step {i}");
                if (i > 0) Assert.That(waited[i], Is.GreaterThanOrEqualTo(waited[i - 1]));
            }
        }

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(-17)]
        [TestCase(int.MaxValue)]
        public void RoutineDepartures_ReconstructWithDifferentQueryOrder(int seed)
        {
            var authored = new[] { 0f, 6f, 12f, 18f };
            uint key = RoutineSchedule.RoutineSeed(seed, "routine.audit");
            var expected = new float[4];
            for (int i = 0; i < 4; i++) expected[i] = RoutineSchedule.DepartureHour(authored, i, key, 15f);
            for (int i = 3; i >= 0; i--)
            {
                RoutineSchedule.DepartureHour(authored, i, key ^ 123u, 30f);
                Assert.That(RoutineSchedule.DepartureHour(authored, i, key, 15f), Is.EqualTo(expected[i]));
            }
            for (int hour = 23; hour >= 0; hour--)
            {
                int block = RoutineSchedule.BlockIndexAt(hour, expected);
                var rebuilt = new float[4];
                for (int i = 3; i >= 0; i--) rebuilt[i] = RoutineSchedule.DepartureHour(authored, i, key, 15f);
                Assert.That(RoutineSchedule.BlockIndexAt(hour, rebuilt), Is.EqualTo(block), $"hour {hour}");
            }
        }

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(-17)]
        [TestCase(int.MaxValue)]
        public void CurrentField_ReplaysBackwardAcrossDayBoundaries(int seed)
        {
            double[] times = { 0, 0.125, 899.875, 900, 1799.875, 1800, 50400, 201600 };
            var expected = new Vector2[times.Length];
            for (int i = 0; i < times.Length; i++) expected[i] = CurrentAt(times[i], seed);
            for (int i = times.Length - 1; i >= 0; i--)
            {
                CurrentAt(times[i] + 75, unchecked(seed + 1));
                Assert.That(CurrentAt(times[i], seed), Is.EqualTo(expected[i]), $"time {times[i]}");
            }
        }

        private static Vector2 CurrentAt(double time, int seed)
            => CurrentModel.SampleCurrent(time, seed, 75, new Vector2(3, 4), 2f, 0.125f,
                CurrentProfile.CoddleCove);

        [TestCase(StorageMode.Ambient)]
        [TestCase(StorageMode.Live)]
        [TestCase(StorageMode.Frozen)]
        [TestCase(StorageMode.Iced)]
        public void Freshness_OneClockJumpEqualsQuarterDaySettlements(StorageMode mode)
        {
            // Binary-exact fixture numbers isolate clock/history dependence from rounding tolerance.
            var policy = new SpoilPolicy(1f, 0f, 0f, 0.25f, 0.9f);
            var landed = Freshness.Landed(0, mode);
            var stepped = landed;
            for (int i = 1; i <= 3; i++) stepped = Freshness.Settle(stepped, i * 256, 0.5f, 1024f, policy);
            var jumped = Freshness.Settle(landed, 768, 0.5f, 1024f, policy);
            Assert.That(jumped.SpoilAccrued, Is.EqualTo(stepped.SpoilAccrued));
            Assert.That(jumped.LastSettleGameSeconds, Is.EqualTo(stepped.LastSettleGameSeconds));
            Assert.That(jumped.Mode, Is.EqualTo(stepped.Mode));
            var rewound = Freshness.Settle(jumped, 256, 0.5f, 1024f, policy);
            Assert.That(rewound.SpoilAccrued, Is.EqualTo(jumped.SpoilAccrued));
            Assert.That(rewound.LastSettleGameSeconds, Is.EqualTo(768));
        }

        [Test]
        public void NavigationPhases_AreIndependentOfDictionaryInsertionOrder()
        {
            var marks = new[] { ("mark.z", "Fl"), ("mark.b", "Oc"), ("mark.a", "Fl"), ("mark.y", "Oc") };
            var forward = NavLightPhasePlan.Spread(marks);
            Array.Reverse(marks);
            var reverse = NavLightPhasePlan.Spread(marks);
            Assert.That(reverse.Count, Is.EqualTo(4));
            foreach (var mark in marks) Assert.That(reverse[mark.Item1], Is.EqualTo(forward[mark.Item1]));
        }

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(-17)]
        [TestCase(int.MaxValue)]
        public void TideAndWeather_ReconstructAfterOtherSeedsAndBackwardSeeks(int seed)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                double day = config.SecondsPerDay;
                double[] times = { 0, day / 4, day - 0.125, day, day * 28, day * 112 };
                var expected = new (float Height, float Rate, Vector2 Wind, SeaState Sea, float Visibility)[times.Length];
                for (int i = 0; i < times.Length; i++) expected[i] = SeaAt(times[i], seed, config);
                for (int i = times.Length - 1; i >= 0; i--)
                {
                    SeaAt(times[i] + day, unchecked(seed + 1), config);
                    Assert.That(SeaAt(times[i], seed, config), Is.EqualTo(expected[i]), $"sample {i}");
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        private static (float, float, Vector2, SeaState, float) SeaAt(double time, int seed, GameConfig config)
        {
            WeatherModel.Sample(time, seed, config, out var wind, out var sea, out float visibility);
            return (TideModel.Height(time, TideProfile.CoddleCove, config),
                TideModel.Rate(time, TideProfile.CoddleCove, config), wind, sea, visibility);
        }

        [Test]
        public void MarketSettlement_IsIndependentOfCategoryInsertionOrder()
        {
            var a = new GameObject("audit market forward");
            var b = new GameObject("audit market reverse");
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var forward = a.AddComponent<Market>();
                var reverse = b.AddComponent<Market>();
                forward.Configure(config, MarketId.Cove);
                reverse.Configure(config, MarketId.Cove);
                var categories = (FishCategory[])Enum.GetValues(typeof(FishCategory));
                for (int i = 0; i < categories.Length; i++) forward.RegisterSale(categories[i], i + 1);
                for (int i = categories.Length - 1; i >= 0; i--) reverse.RegisterSale(categories[i], i + 1);
                for (int day = 0; day < 5; day++)
                {
                    forward.SettleDaily();
                    reverse.SettleDaily();
                    foreach (var category in categories)
                        Assert.That(reverse.SupplyOf(category), Is.EqualTo(forward.SupplyOf(category)),
                            $"day {day}, category {category}");
                }
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(config); }
        }
    }
}
