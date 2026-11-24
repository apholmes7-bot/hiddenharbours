using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// The island's back-room ledger and its run calendar (docs/design/st-peters-living-economy.md §4), on
    /// synthetic goods so each rule is visible in the numbers. Pure and deterministic (rule 5): a day is
    /// recomputed from the day alone, so a replay from any long-enough lookback gives the same answer.
    /// </summary>
    public class IslandStoreLedgerTests
    {
        private const double Eps = 1e-9;
        private static readonly MassVolume Plenty = new MassVolume(1_000_000.0, 1_000_000.0);
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _spawned)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private IslandGoodDef Good(string id, float units, float kg, float litres, float allowance,
                                   GoodSource source = GoodSource.MainlandOverBar)
        {
            var g = ScriptableObject.CreateInstance<IslandGoodDef>();
            g.Id = id;
            g.Source = source;
            g.DailyUnits = units;
            g.DailyMassKg = kg;
            g.DailyVolumeLitres = litres;
            g.PlayerAllowanceFraction = allowance;
            g.WinterMultiplier = 1f;
            _spawned.Add(g);
            return g;
        }

        private IslandGoodDef Gasoline() => Good("islandgood.test_gasoline", 20f, 17f, 24f, 0f);
        private IslandGoodDef Groceries() => Good("islandgood.test_groceries", 18f, 18f, 40f, 0.25f);

        private static readonly Func<int, bool> Always = _ => true;

        // ---- the run calendar ------------------------------------------------------------------------

        [Test]
        public void Calendar_RunsOnEveryScheduledDay_WhenEveryDayIsPossible()
        {
            for (int d = 0; d < 20; d++)
                Assert.That(IslandRunCalendar.RunsOn(d, 2, Always), Is.EqualTo(d % 2 == 0), "day " + d);
        }

        [Test]
        public void Calendar_MakesUpAMissedRunOnTheFirstPossibleDay_AndOnlyOnce()
        {
            Func<int, bool> possible = d => d != 3;
            Assert.IsFalse(IslandRunCalendar.RunsOn(3, 3, possible), "scheduled but the bar is closed");
            Assert.IsTrue(IslandRunCalendar.RunsOn(4, 3, possible), "made up the next possible day");
            Assert.IsFalse(IslandRunCalendar.RunsOn(5, 3, possible), "only once");
            Assert.IsTrue(IslandRunCalendar.RunsOn(6, 3, possible), "the next scheduled run");
        }

        [Test]
        public void Calendar_MissesARun_WhenNoDayBeforeTheNextScheduledOneIsPossible()
        {
            Func<int, bool> possible = d => d != 2 && d != 3;
            Assert.IsFalse(IslandRunCalendar.RunsOn(2, 2, possible));
            Assert.IsFalse(IslandRunCalendar.RunsOn(3, 2, possible));
            Assert.IsTrue(IslandRunCalendar.RunsOn(4, 2, possible));
        }

        [Test]
        public void Calendar_NeverRuns_WithoutACadenceOrADayFunction()
        {
            Assert.IsFalse(IslandRunCalendar.RunsOn(0, 0, Always));
            Assert.IsFalse(IslandRunCalendar.RunsOn(4, 2, null));
            Assert.IsFalse(IslandRunCalendar.RunsOn(-2, 2, Always));
        }

        // ---- the ledger ------------------------------------------------------------------------------

        [Test]
        public void Ledger_StocksOnlyGoodsThatCrossTheBar()
        {
            var ledger = new IslandStoreLedger(new[]
            {
                Gasoline(),
                Good("islandgood.test_diesel", 50f, 42f, 60f, 0f, GoodSource.BySea),
                Good("islandgood.test_firewood", 30f, 30f, 60f, 0f, GoodSource.Island),
                Good("islandgood.test_nothing", 0f, 0f, 0f, 0f),
                null,
            }, Plenty, 2, 4.0, 28, Always);
            Assert.That(ledger.GoodCount, Is.EqualTo(1));
            Assert.That(ledger.Good(0).Id, Is.EqualTo("islandgood.test_gasoline"));
        }

        [Test]
        public void Ledger_WithEveryRunPossible_ARunDayRefillsTwoDaysAndEndsOneDayBelowTarget()
        {
            var ledger = new IslandStoreLedger(new[] { Gasoline() }, Plenty, 2, 4.0, 28, Always);
            Assert.That(ledger.Target(0, 40), Is.EqualTo(80.0).Within(Eps), "4 days of 20 L");

            IslandStoreDay day = ledger.Settle(40);
            Assert.IsTrue(day.RunToday);
            Assert.That(ledger.LevelAtDawn(0), Is.EqualTo(40.0).Within(Eps));
            Assert.That(ledger.Delivered(0), Is.EqualTo(40.0).Within(Eps), "two days of use");
            Assert.That(ledger.LevelAtDusk(0), Is.EqualTo(60.0).Within(Eps));
            Assert.That(day.Delivered.MassKg, Is.EqualTo(34.0).Within(Eps), "40 L at 0.85 kg/L");
            Assert.That(day.Delivered.VolumeLitres, Is.EqualTo(48.0).Within(Eps), "40 L at 1.2 L carried per L");
            Assert.That(day.Shortfall.MassKg, Is.EqualTo(0.0));

            IslandStoreDay next = ledger.Settle(41);
            Assert.IsFalse(next.RunToday);
            Assert.That(ledger.LevelAtDusk(0), Is.EqualTo(40.0).Within(Eps));
        }

        [Test]
        public void Ledger_ScalesEveryGoodTogether_WhenTheKitCannotCarryTheWholeWant()
        {
            IslandGoodDef a = Good("islandgood.test_a", 10f, 10f, 10f, 0f);
            IslandGoodDef b = Good("islandgood.test_b", 10f, 10f, 30f, 0f);
            // Want on day 2 is 20 units of each: 40 kg, 80 L. The kit takes 40 L, so half of each comes.
            var ledger = new IslandStoreLedger(new[] { a, b }, new MassVolume(1000.0, 40.0), 2, 4.0, 28, Always);

            IslandStoreDay day = ledger.Settle(2);
            Assert.IsTrue(day.RunToday);
            Assert.That(ledger.Delivered(0), Is.EqualTo(10.0).Within(Eps));
            Assert.That(ledger.Delivered(1), Is.EqualTo(10.0).Within(Eps));
            Assert.That(day.Delivered.VolumeLitres, Is.EqualTo(40.0).Within(Eps), "the kit, exactly full by volume");
            Assert.That(day.Delivered.MassKg, Is.EqualTo(20.0).Within(Eps));
            Assert.That(ledger.LevelAtDusk(0), Is.EqualTo(20.0).Within(Eps));
        }

        [Test]
        public void Ledger_AFiveDayGap_StarvesAGoodWithNoAllowance_ButNotOneWithAllowance()
        {
            // Runs on 0, 2, ..., 10; the bar is closed on days 11 to 14; the make-up run goes on day 15.
            Func<int, bool> possible = d => d < 11 || d >= 15;
            var ledger = new IslandStoreLedger(new[] { Groceries(), Gasoline() }, Plenty, 2, 4.0, 28, possible);

            IslandStoreDay closed = ledger.Settle(14);
            Assert.IsFalse(closed.RunToday);
            Assert.That(ledger.Shortfall(0), Is.EqualTo(0.0).Within(Eps), "groceries: 4 days x 1.25 lasts 5 days");
            Assert.That(ledger.LevelAtDusk(0), Is.EqualTo(0.0).Within(Eps), "and is used to the last kg");
            Assert.That(ledger.Shortfall(1), Is.EqualTo(20.0).Within(Eps), "gasoline: 4 days does not last 5");
            Assert.That(closed.Shortfall.MassKg, Is.EqualTo(17.0).Within(Eps));

            IslandStoreDay reopened = ledger.Settle(15);
            Assert.IsTrue(reopened.RunToday, "the make-up run");
            Assert.That(ledger.Shortfall(1), Is.EqualTo(0.0));
        }

        [Test]
        public void Ledger_IsAPureFunctionOfTheDay_InAnyOrder()
        {
            Func<int, bool> possible = d => d % 9 != 4;
            var ledger = new IslandStoreLedger(new[] { Groceries(), Gasoline() }, new MassVolume(100.0, 160.0),
                                               2, 4.0, 28, possible);
            ledger.Settle(40);
            double groceries = ledger.LevelAtDusk(0), gasoline = ledger.LevelAtDusk(1);
            ledger.Settle(17);
            ledger.Settle(3);
            ledger.Settle(40);
            Assert.That(ledger.LevelAtDusk(0), Is.EqualTo(groceries));
            Assert.That(ledger.LevelAtDusk(1), Is.EqualTo(gasoline));
        }

        [Test]
        public void Ledger_ReplayConverges_SoTheLookbackDoesNotChangeTheAnswer()
        {
            // A three-day closure every fourteen days and a kit that refills a gap only over several runs.
            Func<int, bool> possible = d => d % 14 < 11;
            var capacity = new MassVolume(100.0, 160.0);
            IslandGoodDef[] goods = { Groceries(), Gasoline() };
            var shortLook = new IslandStoreLedger(goods, capacity, 2, 4.0, 28, possible);
            var longLook = new IslandStoreLedger(goods, capacity, 2, 4.0, 56, possible);

            for (int day = 56; day < 140; day++)
            {
                IslandStoreDay a = shortLook.Settle(day);
                IslandStoreDay b = longLook.Settle(day);
                Assert.That(a.RunToday, Is.EqualTo(b.RunToday), "day " + day);
                for (int i = 0; i < goods.Length; i++)
                    Assert.That(shortLook.LevelAtDusk(i), Is.EqualTo(longLook.LevelAtDusk(i)).Within(Eps),
                                "day " + day + " good " + i);
            }
        }

        [Test]
        public void Ledger_StockBalances_DeliveredLessUsedIsTheChangeInLevel()
        {
            Func<int, bool> possible = d => d % 14 < 11;
            var ledger = new IslandStoreLedger(new[] { Groceries(), Gasoline() }, new MassVolume(100.0, 160.0),
                                               2, 4.0, 28, possible);
            ledger.Settle(59);
            double start = ledger.LevelAtDusk(1);
            double net = 0.0;
            for (int day = 60; day < 88; day++)
            {
                ledger.Settle(day);
                net += ledger.Delivered(1) - (ledger.DailyUse(1, day) - ledger.Shortfall(1));
            }
            Assert.That(ledger.LevelAtDusk(1), Is.EqualTo(start + net).Within(1e-6));
        }
    }
}
