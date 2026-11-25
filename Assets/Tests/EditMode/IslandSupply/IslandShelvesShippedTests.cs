using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shipped St Peters store shelves (docs/design/st-peters-living-economy.md §5, slice 2) against the
    /// shipped back-room ledger, the shipped store, the shipped GameConfig and the island's real tide. Pins the
    /// wiring, that a full back room fills every shelf, that a shelf stands empty only on a day the run did not
    /// come, and that the player's absence changes nothing.
    /// </summary>
    public class IslandShelvesShippedTests
    {
        private const string ShelvesId = "islandshelves.st_peters_general";
        private const string LedgerId = "islandledger.st_peters";

        private GameConfig _cfg;
        private IslandSupplyConfig _route;
        private IslandStoreShelvesDef _def;
        private Dictionary<string, CarrierDef> _carriers;

        [OneTimeSetUp]
        public void Load()
        {
            _cfg = IslandSupplyTestData.Config();
            _route = IslandSupplyTestData.Route();
            _carriers = IslandSupplyTestData.CarriersById();
            List<IslandStoreShelvesDef> all = IslandSupplyTestData.LoadAll<IslandStoreShelvesDef>();
            Assert.That(all.Count(d => d.Id == ShelvesId), Is.EqualTo(1), "exactly one " + ShelvesId);
            _def = all.First(d => d.Id == ShelvesId);
        }

        private IslandStoreShelves Build(int lastDay)
        {
            Func<int, bool> possible = IslandRunCalendar.DaylightRunDays(
                IslandSupplyTestData.Water(_route, _cfg), _def.Ledger.Runner, _route, _cfg, 0, lastDay);
            return IslandStoreShelves.FromDef(_def, _route, _carriers, possible, _cfg.SecondsPerDay);
        }

        private double At(int day, double hour) => (day + hour / 24.0) * _cfg.SecondsPerDay;

        [Test]
        public void TheShelvesAreWiredToTheIslandsBackRoomAndItsStore()
        {
            Assert.That(_def.Ledger, Is.Not.Null);
            Assert.That(_def.Ledger.Id, Is.EqualTo(LedgerId));
            Assert.That(_def.OpensHour, Is.LessThan(_def.ClosesHour));

            ShopDef shop = IslandSupplyTestData.LoadAll<ShopDef>().SingleOrDefault(s => s.Spec.Id == _def.ShopId);
            Assert.That(shop, Is.Not.Null, "the shelves belong to a shipped store: " + _def.ShopId);
            var shopLineIds = new HashSet<string>(shop.Spec.Stock.Select(l => l.Id));

            Assert.That(_def.Lines.Count, Is.GreaterThan(0));
            var ids = new HashSet<string>();
            var shown = new Dictionary<string, double>();
            foreach (IslandShelfLine line in _def.Lines)
            {
                Assert.That(line.Id, Does.Match("^line\\.[a-z0-9_]+$"), line.Id);
                Assert.IsTrue(ids.Add(line.Id), "duplicate line " + line.Id);
                Assert.IsFalse(shopLineIds.Contains(line.Id), line.Id + " reuses one of the store's weekday lines");
                Assert.That(line.Good, Is.Not.Null, line.Id);
                // Several lines may share a good (bread, oatcakes, beans off the groceries), never more than all of it.
                shown.TryGetValue(line.Good.Id, out double had);
                shown[line.Good.Id] = had + IslandStoreShelves.ShareOf(line);
                Assert.That(shown[line.Good.Id], Is.LessThanOrEqualTo(1.0 + 1e-6), "the shelves show more than all of " + line.Good.Id);
                Assert.That(line.Good.Source, Is.EqualTo(GoodSource.MainlandOverBar), line.Id);
                Assert.IsTrue(_def.Ledger.Goods.Contains(line.Good), line.Id + " shows a good the back room does not hold");
                Assert.That(line.ItemName, Is.Not.Empty, line.Id);
            }
        }

        [Test]
        public void EachShelfHoldsTheWholeBufferTarget_ToWithinOneItem()
        {
            foreach (IslandShelfLine line in _def.Lines)
            {
                double target = _route.BufferDays * line.Good.DailyUnits * (1.0 + line.Good.PlayerAllowanceFraction)
                                * IslandStoreShelves.ShareOf(line);
                Assert.That(line.MaxOnShelf * (double)line.UnitsPerItem, Is.EqualTo(target).Within(line.UnitsPerItem),
                            line.Id + ": a full back room should read as a full shelf, and a thinning one as thinning");
            }
        }

        [Test]
        public void OnTheRealTide_AShelfStandsEmptyOnlyOnADayTheRunDidNotCome_AndTheShelvesMove()
        {
            int month = (int)_cfg.LunarMonthDays;
            int first = _def.Ledger.LookbackDays, last = first + 2 * month - 1;
            IslandStoreShelves shelves = Build(last);

            var emptyDays = new List<string>();
            foreach (IslandShelfLine line in _def.Lines)
            {
                var seen = new HashSet<int>();
                for (int day = first; day <= last; day++)
                {
                    int open = shelves.CountAt(line.Id, At(day, _def.OpensHour));
                    int close = shelves.CountAt(line.Id, At(day, _def.ClosesHour));
                    seen.Add(open);
                    Assert.That(open, Is.InRange(0, line.MaxOnShelf));
                    Assert.That(close, Is.InRange(0, open), line.Id + " day " + day + ": the day's buying only thins it");
                    if (open == 0)
                    {
                        Assert.IsFalse(shelves.RunCameOn(day), line.Id + " empty on run day " + day);
                        emptyDays.Add(line.Id + "@" + day + " (" + shelves.DaysSinceRun(day) + " days since the run)");
                    }
                }
                Assert.That(seen.Count, Is.GreaterThan(1), line.Id + " never changes over two lunar months");
            }
            TestContext.WriteLine("Empty at opening on the real tide, days " + first + ".." + last + ": " +
                                  (emptyDays.Count == 0 ? "none" : string.Join(", ", emptyDays)));
        }

        [Test]
        public void OnTheRealTide_ReadingStraightToADayEqualsWatchingEveryDayBeforeIt()
        {
            int last = _def.Ledger.LookbackDays + (int)_cfg.LunarMonthDays;
            IslandStoreShelves watched = Build(last);
            var opening = new Dictionary<(string, int), int>();
            for (int day = 0; day <= last; day++)
                foreach (IslandShelfLine line in _def.Lines)
                    for (int h = 0; h < 24; h += 4)
                    {
                        int c = watched.CountAt(line.Id, At(day, h));
                        if (h == 8) opening[(line.Id, day)] = c;
                    }

            for (int day = last; day > last - 7; day--)
            {
                IslandStoreShelves slept = Build(last);
                foreach (IslandShelfLine line in _def.Lines)
                    Assert.That(slept.CountAt(line.Id, At(day, 8)), Is.EqualTo(opening[(line.Id, day)]),
                                line.Id + " day " + day);
            }
        }
    }
}
