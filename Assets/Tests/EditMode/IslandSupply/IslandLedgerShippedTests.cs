using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shipped St Peters back-room ledger (docs/design/st-peters-living-economy.md §4, slice 1) against the
    /// shipped island-supply Defs, the shipped GameConfig and the island's real tide. Pins that the asset is
    /// wired to the same goods, runner and route as the supply plan, and that the replay's answer does not
    /// depend on how far back it starts: the property that lets the island be recomputed instead of saved.
    /// </summary>
    public class IslandLedgerShippedTests
    {
        private const string LedgerId = "islandledger.st_peters";
        private const string RunnerId = "supplyrole.quad_runner";

        private GameConfig _cfg;
        private IslandSupplyConfig _route;
        private IslandLedgerDef _def;
        private Dictionary<string, CarrierDef> _carriers;

        [OneTimeSetUp]
        public void Load()
        {
            _cfg = IslandSupplyTestData.Config();
            _route = IslandSupplyTestData.Route();
            _carriers = IslandSupplyTestData.CarriersById();
            List<IslandLedgerDef> all = IslandSupplyTestData.LoadAll<IslandLedgerDef>();
            Assert.That(all.Count(d => d.Id == LedgerId), Is.EqualTo(1), "exactly one " + LedgerId);
            _def = all.First(d => d.Id == LedgerId);
        }

        [Test]
        public void TheLedgerIsWiredToTheIslandsRouteRunnerAndEveryGoodThatCrossesTheBar()
        {
            Assert.That(_def.IslandRegionId, Is.EqualTo(_route.IslandRegionId));
            Assert.That(_def.Runner, Is.Not.Null);
            Assert.That(_def.Runner.Id, Is.EqualTo(RunnerId));
            Assert.That(_def.Runner.CadenceDays, Is.GreaterThan(0));
            Assert.That(_def.LookbackDays, Is.GreaterThanOrEqualTo(2 * _def.Runner.CadenceDays));

            var shipped = IslandSupplyTestData.LoadAll<IslandGoodDef>()
                .Where(g => g.Source == GoodSource.MainlandOverBar).Select(g => g.Id).ToList();
            var stocked = _def.Goods.Where(g => g != null).Select(g => g.Id).ToList();
            CollectionAssert.AllItemsAreUnique(stocked);
            CollectionAssert.AreEquivalent(shipped, stocked);

            SupplyPlanMath.KitCapacity(_def.Runner, _carriers, out int missing);
            Assert.That(missing, Is.EqualTo(0), "every carrier of the runner resolves");
        }

        [Test]
        public void OnTheRealTide_TheRunGoesAboutEverySecondDay_AndOnlyOnDaysTheBarAllows()
        {
            int month = (int)_cfg.LunarMonthDays;
            Func<int, bool> possible = IslandRunCalendar.DaylightRunDays(
                IslandSupplyTestData.Water(_route, _cfg), _def.Runner, _route, _cfg, 0, month - 1);
            var ledger = IslandStoreLedger.FromDef(_def, _route, _carriers, possible);

            int runs = 0;
            for (int d = 0; d < month; d++)
            {
                if (!ledger.RunsOn(d)) continue;
                runs++;
                Assert.IsTrue(possible(d), "a run on day " + d + " with no daylight round trip");
            }
            int scheduled = month / _def.Runner.CadenceDays;
            Assert.That(runs, Is.InRange(scheduled - 2, scheduled),
                        "one run per scheduled slot unless the bar is shut for the whole slot");
        }

        [Test]
        public void OnTheRealTide_DoublingTheLookbackChangesNothing()
        {
            int look = _def.LookbackDays;
            int first = 2 * look, last = first + 2 * (int)_cfg.LunarMonthDays - 1;
            Func<int, bool> possible = IslandRunCalendar.DaylightRunDays(
                IslandSupplyTestData.Water(_route, _cfg), _def.Runner, _route, _cfg, 0, last);
            MassVolume kit = SupplyPlanMath.KitCapacity(_def.Runner, _carriers, out _);

            var shipped = IslandStoreLedger.FromDef(_def, _route, _carriers, possible);
            var doubled = new IslandStoreLedger(_def.Goods, kit, _def.Runner.CadenceDays, _route.BufferDays,
                                                2 * look, possible);
            Assert.That(shipped.GoodCount, Is.EqualTo(doubled.GoodCount));

            for (int day = first; day <= last; day++)
            {
                IslandStoreDay a = shipped.Settle(day);
                IslandStoreDay b = doubled.Settle(day);
                Assert.That(a.RunToday, Is.EqualTo(b.RunToday), "day " + day);
                Assert.That(a.Shortfall.MassKg, Is.EqualTo(b.Shortfall.MassKg).Within(1e-9), "day " + day);
                for (int i = 0; i < shipped.GoodCount; i++)
                    Assert.That(shipped.LevelAtDusk(i), Is.EqualTo(doubled.LevelAtDusk(i)).Within(1e-9),
                                "day " + day + " " + shipped.Good(i).Id);
            }
        }
    }
}
