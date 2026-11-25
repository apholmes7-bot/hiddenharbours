using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// The island store's shelves (docs/design/st-peters-living-economy.md Â§5, slice 2) on synthetic goods, so
    /// each rule shows in the numbers: the shelf holds the back room after the morning run, the islanders'
    /// buying thins it through the store's hours, it equals the back room at dusk, a tide-blocked stretch
    /// empties it, and the player's purchases come off it. Every read is a pure function
    /// of the moment asked (rule 5), so the player's absence changes nothing.
    /// </summary>
    public class IslandStoreShelvesTests
    {
        // Synthetic inputs, chosen so the arithmetic is easy to follow by hand.
        private const double SecondsPerDay = 1800.0;
        private const double Opens = 7.0, Closes = 19.0;
        private const int Cadence = 2, Lookback = 28;
        private const double BufferDays = 4.0;
        private const string Gas = "line.test_gas_can", Grocery = "line.test_grocery_box";
        private static readonly MassVolume Plenty = new MassVolume(1_000_000.0, 1_000_000.0);
        private static readonly Func<int, bool> Always = _ => true;

        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private IslandGoodDef _gas, _groceries, _unstocked;

        [SetUp]
        public void SetUp()
        {
            _gas = Good("islandgood.test_gasoline", 20f, 17f, 24f, 0f);          // target 80 L: 4 cans of 20 L
            _groceries = Good("islandgood.test_groceries", 18f, 18f, 40f, 0.25f); // target 90 kg: 18 boxes of 5 kg
            _unstocked = Good("islandgood.test_wood", 5f, 5f, 10f, 0f, GoodSource.Island);
        }

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

        private IslandStoreLedger Ledger(Func<int, bool> possible) =>
            new IslandStoreLedger(new[] { _gas, _groceries, _unstocked }, Plenty, Cadence, BufferDays, Lookback, possible);

        private List<IslandShelfLine> Lines() => new List<IslandShelfLine>
        {
            new IslandShelfLine { Id = Gas, Good = _gas, ItemName = "can", UnitsPerItem = 20f, MaxOnShelf = 4 },
            new IslandShelfLine { Id = Grocery, Good = _groceries, ItemName = "box", UnitsPerItem = 5f, MaxOnShelf = 18 },
        };

        private IslandStoreShelves Shelves(Func<int, bool> possible) =>
            new IslandStoreShelves("shop.test_store", Ledger(possible), Lines(), SecondsPerDay, Opens, Closes);

        private static double At(int day, double hour) => (day + hour / 24.0) * SecondsPerDay;

        private static int Items(double units, double perItem, int max) =>
            Math.Min(max, Math.Max(0, (int)Math.Floor(units / perItem + 1e-9)));

        // ---- determinism and the absent player ------------------------------------------------------------

        [Test]
        public void TheSameMomentGivesTheSameShelves_InAnyReadOrder_AndWhetherOrNotEarlierDaysWereWatched()
        {
            Func<int, bool> possible = d => d % 7 != 3;
            var watched = Shelves(possible);
            var record = new List<(double t, int gas, int grocery)>();
            for (int day = 0; day < 60; day++)
                for (int h = 0; h < 24; h += 3)
                {
                    double t = At(day, h);
                    record.Add((t, watched.CountAt(Gas, t), watched.CountAt(Grocery, t)));
                }

            var backwards = Shelves(possible);
            for (int i = record.Count - 1; i >= 0; i--)
            {
                Assert.That(backwards.CountAt(Gas, record[i].t), Is.EqualTo(record[i].gas), "gas at " + record[i].t);
                Assert.That(backwards.CountAt(Grocery, record[i].t), Is.EqualTo(record[i].grocery), "groceries at " + record[i].t);
            }

            // The player slept for fifty days: a fresh read of day 55 is what watching every day gave.
            var slept = Shelves(possible);
            foreach (var r in record)
            {
                if (r.t < At(55, 0) || r.t >= At(56, 0)) continue;
                Assert.That(slept.CountAt(Gas, r.t), Is.EqualTo(r.gas));
                Assert.That(slept.CountAt(Grocery, r.t), Is.EqualTo(r.grocery));
            }
        }

        [Test]
        public void WithNoPlayerAtAll_TheShelvesStillFollowTheRuns()
        {
            var shelves = Shelves(Always);
            var seen = new HashSet<int>();
            for (int day = 30; day < 40; day++) seen.Add(shelves.CountAt(Grocery, At(day, Opens)));
            Assert.That(seen.Count, Is.GreaterThan(1), "the island's own use and runs move the shelf with nobody buying");
        }

        // ---- the shelf is the back room ------------------------------------------------------------------

        [Test]
        public void AtOpeningTheShelfHoldsTheBackRoomAfterTheRun_AndAtClosingTheBackRoomAtDusk()
        {
            Func<int, bool> possible = d => d % 7 != 3 && (d < 40 || d > 45);
            var shelves = Shelves(possible);
            var reference = Ledger(possible);
            for (int day = 0; day < 70; day++)
            {
                reference.Settle(day);
                double gasMorning = reference.LevelAtDawn(0) + reference.Delivered(0);
                double groceryMorning = reference.LevelAtDawn(1) + reference.Delivered(1);
                Assert.That(shelves.CountAt(Gas, At(day, Opens)), Is.EqualTo(Items(gasMorning, 20, 4)), "gas opening, day " + day);
                Assert.That(shelves.CountAt(Grocery, At(day, Opens)), Is.EqualTo(Items(groceryMorning, 5, 18)), "grocery opening, day " + day);
                Assert.That(shelves.CountAt(Gas, At(day, Closes)), Is.EqualTo(Items(reference.LevelAtDusk(0), 20, 4)), "gas closing, day " + day);
                Assert.That(shelves.CountAt(Grocery, At(day, Closes)), Is.EqualTo(Items(reference.LevelAtDusk(1), 5, 18)), "grocery closing, day " + day);
            }
        }

        [Test]
        public void ThroughTheStoreHours_TheIslandersBuyingThinsTheShelf()
        {
            var shelves = Shelves(Always);
            int day = 30; // a run day: 90 kg of groceries in the morning, 18 kg bought by closing
            Assert.That(shelves.CountAt(Grocery, At(day, 0.5)), Is.EqualTo(18), "before opening the morning stock stands");
            Assert.That(shelves.CountAt(Grocery, At(day, Opens)), Is.EqualTo(18));
            Assert.That(shelves.CountAt(Grocery, At(day, 13.0)), Is.EqualTo(16), "half the day's use gone by midday: 81 kg");
            Assert.That(shelves.CountAt(Grocery, At(day, Closes)), Is.EqualTo(14), "72 kg at closing");
            Assert.That(shelves.CountAt(Grocery, At(day, 23.5)), Is.EqualTo(14), "nothing more is bought after closing");
        }

        // ---- a different day differs sensibly -------------------------------------------------------------

        [Test]
        public void BetweenRunsTheShelvesNeverRise_AndARunDayRefillsThem()
        {
            var shelves = Shelves(Always);
            int prevGas = shelves.CountAt(Gas, At(20, 0)), prevGrocery = shelves.CountAt(Grocery, At(20, 0));
            int prevDay = 20;
            for (int step = 1; step < 20 * 24; step++)
            {
                double t = At(20, step);
                int day = 20 + step / 24;
                int gas = shelves.CountAt(Gas, t), grocery = shelves.CountAt(Grocery, t);
                bool newRunDay = day != prevDay && shelves.RunCameOn(day);
                if (!newRunDay)
                {
                    Assert.That(gas, Is.LessThanOrEqualTo(prevGas), "gas rose without a run at " + t);
                    Assert.That(grocery, Is.LessThanOrEqualTo(prevGrocery), "groceries rose without a run at " + t);
                }
                prevGas = gas; prevGrocery = grocery; prevDay = day;
            }

            Assert.IsTrue(shelves.RunCameOn(30));
            Assert.That(shelves.CountAt(Gas, At(30, Opens)), Is.EqualTo(4), "a full top-up fills the gas shelf");
            Assert.That(shelves.CountAt(Grocery, At(30, Opens)), Is.EqualTo(18), "and the grocery shelf");
            Assert.That(shelves.CountAt(Gas, At(31, Opens)), Is.EqualTo(3), "the day after: one day's gas gone");
            Assert.That(shelves.CountAt(Grocery, At(31, Opens)), Is.EqualTo(14));
        }

        [Test]
        public void ATideBlockedStretchEmptiesTheShelves_AndTheNextRunRefillsThem()
        {
            Func<int, bool> possible = d => d < 20 || d >= 30; // the bar shut for days 20 to 29
            var shelves = Shelves(possible);
            Assert.IsTrue(shelves.RunCameOn(18), "the last run before the gap");
            for (int day = 20; day < 30; day++) Assert.IsFalse(shelves.RunCameOn(day), "no run on day " + day);

            for (int day = 23; day < 30; day++)
            {
                Assert.That(shelves.CountAt(Gas, At(day, Opens)), Is.EqualTo(0), "gas shelf on day " + day);
                Assert.That(shelves.CountAt(Grocery, At(day, Opens)), Is.EqualTo(0), "grocery shelf on day " + day);
                Assert.IsFalse(shelves.TryTakeOne(Gas, At(day, 10.0)), "nothing to take on day " + day);
            }
            Assert.That(shelves.DaysSinceRun(29), Is.EqualTo(11), "the keeper can say how long the quad has been away");

            Assert.IsTrue(shelves.RunCameOn(30));
            Assert.That(shelves.DaysSinceRun(30), Is.EqualTo(0));
            Assert.That(shelves.CountAt(Gas, At(30, Opens)), Is.EqualTo(4), "the run refills the gas");
            Assert.That(shelves.CountAt(Grocery, At(30, Opens)), Is.EqualTo(18), "and the groceries");
        }

        // ---- the player's purchases --------------------------------------------------------------------

        [Test]
        public void ThePlayersPurchaseComesOffTheShelf_LastsTheDay_AndTheNextFullRunClearsIt()
        {
            var shelves = Shelves(Always);
            var untouched = Shelves(Always);
            double t = At(21, 10.0); // a day after a run: 60 L in the morning, 55 L at 10:00 = 2 cans
            Assert.That(shelves.CountAt(Gas, t), Is.EqualTo(2));
            Assert.IsTrue(shelves.TryTakeOne(Gas, t));
            Assert.That(shelves.CountAt(Gas, t), Is.EqualTo(1), "the can he took is gone from the shelf");
            Assert.That(shelves.CountAt(Gas, At(21, 9.0)), Is.EqualTo(untouched.CountAt(Gas, At(21, 9.0))),
                        "an earlier moment is not rewritten");
            Assert.That(shelves.CountAt(Gas, At(21, Closes)), Is.EqualTo(1), "60 - 20 used - 20 bought = 20 L");
            Assert.That(untouched.CountAt(Gas, At(21, Closes)), Is.EqualTo(2));
            Assert.That(shelves.CountAt(Grocery, At(21, Closes)), Is.EqualTo(untouched.CountAt(Grocery, At(21, Closes))),
                        "other shelves are untouched");

            for (int day = 22; day < 40; day++)
                Assert.That(shelves.CountAt(Gas, At(day, Opens)), Is.EqualTo(untouched.CountAt(Gas, At(day, Opens))),
                            "after the next full top-up the shelf is the calendar's again, day " + day);
        }

        [Test]
        public void HeldPurchasesStayBounded_ByTheLedgersLookback()
        {
            var shelves = Shelves(Always);
            Assert.IsTrue(shelves.TryTakeOne(Grocery, At(2, 10.0)));
            Assert.IsTrue(shelves.TryTakeOne(Grocery, At(3, 10.0)));
            Assert.That(shelves.HeldDrawCount, Is.EqualTo(2));
            Assert.IsTrue(shelves.TryTakeOne(Grocery, At(2 + Lookback + 10, 10.0)));
            Assert.That(shelves.HeldDrawCount, Is.EqualTo(1), "purchases older than the replay window are dropped");
        }

        // ---- bounds and validation -----------------------------------------------------------------------

        [Test]
        public void CountsStayWithinTheShelf_AndBadAsksAreRefused()
        {
            Func<int, bool> possible = d => d % 5 != 0 && (d < 15 || d > 24);
            var shelves = Shelves(possible);
            Assert.That(shelves.CountAt(Gas, -500.0), Is.EqualTo(shelves.CountAt(Gas, 0.0)), "before the game began reads as day 0");
            for (int step = 0; step < 50 * 24; step++)
            {
                double t = At(0, step);
                Assert.That(shelves.CountAt(Gas, t), Is.InRange(0, 4));
                Assert.That(shelves.CountAt(Grocery, t), Is.InRange(0, 18));
            }
            Assert.IsTrue(shelves.HasLine(Gas));
            Assert.IsFalse(shelves.HasLine("line.test_nothing"));
            Assert.IsFalse(shelves.HasLine(null));
            Assert.That(shelves.MaxOnShelf(Grocery), Is.EqualTo(18));
            Assert.That(shelves.ShopId, Is.EqualTo("shop.test_store"));
            Assert.Throws<KeyNotFoundException>(() => shelves.CountAt("line.test_nothing", 0.0));
            Assert.Throws<KeyNotFoundException>(() => shelves.TryTakeOne(null, 0.0));
            Assert.That(shelves.DaysSinceRun(-1), Is.EqualTo(-1));
        }

        [Test]
        public void TheShelvesRefuseLinesTheBackRoomCannotFill()
        {
            Func<List<IslandShelfLine>, IslandStoreShelves> make = lines =>
                new IslandStoreShelves("shop.test_store", Ledger(Always), lines, SecondsPerDay, Opens, Closes);

            var unstocked = Lines();
            unstocked.Add(new IslandShelfLine { Id = "line.test_wood", Good = _unstocked, UnitsPerItem = 1f, MaxOnShelf = 1 });
            Assert.Throws<ArgumentException>(() => make(unstocked), "an island-own good never reaches the back room");

            var duplicate = Lines();
            duplicate.Add(new IslandShelfLine { Id = Gas, Good = _groceries, UnitsPerItem = 1f, MaxOnShelf = 1 });
            Assert.Throws<ArgumentException>(() => make(duplicate), "duplicate line id");

            var twice = Lines();
            twice.Add(new IslandShelfLine { Id = "line.test_more_gas", Good = _gas, UnitsPerItem = 1f, MaxOnShelf = 1 });
            Assert.Throws<ArgumentException>(() => make(twice), "two shelves for one good");

            Assert.Throws<ArgumentException>(() =>
                new IslandStoreShelves("shop.test_store", Ledger(Always), Lines(), SecondsPerDay, Closes, Opens), "closes before it opens");
            Assert.Throws<ArgumentException>(() =>
                new IslandStoreShelves("shop.test_store", Ledger(Always), Lines(), 0.0, Opens, Closes), "no day length");
        }
    }
}
