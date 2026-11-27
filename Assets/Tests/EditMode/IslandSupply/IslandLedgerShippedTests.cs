using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

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

        [Test]
        public void ShippedSeaAndLedger_HaveIdenticalCrossingWindowsAcrossALunarMonth()
        {
            var region = IslandSupplyTestData.Region(_route.IslandRegionId);
            string scenePath = "Assets/_Project/Scenes/" + region.SceneName + ".unity";
            // Read the service actually referenced by the shipped core, independently of the RegionDef
            // used by Water(). A preview neither replaces the editor's scenes nor runs the game clock.
            var scene = EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                var roots = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameRoot>(true)).ToArray();
                Assert.That(roots.Length, Is.EqualTo(1), scenePath + ": one shared environment publisher");
                var core = new SerializedObject(roots[0]);
                var service = core.FindProperty("_environment").objectReferenceValue as EnvironmentService;
                Assert.That(service, Is.Not.Null, scenePath + ": GameRoot must publish the sea service");
                var serialized = new SerializedObject(service);
                Assert.That(serialized.FindProperty("_config").objectReferenceValue, Is.SameAs(_cfg),
                    "The sea and ledger must read the same shipped clock and tide configuration.");

                IEnvironmentService sea = service; // Exercise WaterLevelAt, including its default interface path.
                Func<double, float> seaWater = sea.WaterLevelAt;
                Func<double, float> ledgerWater = IslandSupplyTestData.Water(_route, _cfg);
                double monthSeconds = _cfg.LunarMonthDays * (double)_cfg.SecondsPerDay;
                int days = (int)Math.Ceiling(_cfg.LunarMonthDays);
                Assert.That(monthSeconds, Is.GreaterThan(0.0));
                TideProfile profile = sea.ActiveTideProfile;
                string source = $"Seed {sea.WorldSeed}, t=[0,{monthSeconds:R}) clock seconds; " +
                    $"shared sea mean/amplitude/phase={profile.MeanLevel:R}/{profile.Amplitude:R}/{profile.PhaseHours:R}; " +
                    $"ledger RegionDef={region.TideMeanLevel:R}/{region.TideAmplitude:R}/{region.TidePhaseHours:R}. " +
                    "The ledger helper has no seed input: TideModel.Height is seed-independent. ";

                // A lunar month contains two complete spring-neap envelopes. Both paths sample the same
                // times at the shipped search step and bisect threshold edges with the same algorithm.
                var seaBar = CrossingMath.BarWindows(seaWater, _route, _cfg, 0.0, monthSeconds);
                var ledgerBar = CrossingMath.BarWindows(ledgerWater, _route, _cfg, 0.0, monthSeconds);
                Assert.That(seaBar, Is.Not.Empty, source + "No bar windows sampled.");
                AssertSameWindows(seaBar, ledgerBar, source + "Bar crest: ");
                var seaGut = CrossingMath.GutWindows(seaWater, _route, _def.Runner.MaxWadeDepthMetres,
                    _cfg, 0.0, monthSeconds);
                var ledgerGut = CrossingMath.GutWindows(ledgerWater, _route, _def.Runner.MaxWadeDepthMetres,
                    _cfg, 0.0, monthSeconds);
                Assert.That(seaGut, Is.Not.Empty, source + "No runner gut windows sampled.");
                AssertSameWindows(seaGut, ledgerGut, source + "Runner gut: ");

                var seaDays = IslandRunCalendar.DaylightRunDays(seaWater, _def.Runner, _route, _cfg, 0, days - 1);
                var ledgerDays = IslandRunCalendar.DaylightRunDays(ledgerWater, _def.Runner, _route, _cfg, 0, days - 1);
                var seaLedger = IslandStoreLedger.FromDef(_def, _route, _carriers, seaDays);
                var ledger = IslandStoreLedger.FromDef(_def, _route, _carriers, ledgerDays);
                for (int day = 0; day < days; day++)
                {
                    Assert.That(ledgerDays(day), Is.EqualTo(seaDays(day)), source + $"Day {day}: daylight feasibility differs.");
                    Assert.That(ledger.RunsOn(day), Is.EqualTo(seaLedger.RunsOn(day)), source + $"Day {day}: delivery differs.");
                }
                TestContext.WriteLine(source + $"Matched {seaBar.Count} bar windows, {seaGut.Count} gut windows and {days} days.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AssertSameWindows(IList<TideWindow> sea, IList<TideWindow> ledger, string context)
        {
            Assert.That(ledger.Count, Is.EqualTo(sea.Count), context + "Window count differs (ledger vs shared sea).");
            for (int i = 0; i < sea.Count; i++)
            {
                string difference = context + $"Window {i}: ledger [{ledger[i].StartSeconds:R}, {ledger[i].EndSeconds:R}), " +
                    $"shared sea [{sea[i].StartSeconds:R}, {sea[i].EndSeconds:R}) clock seconds. " +
                    "Keep this equality guard; resolve the tide source before changing supply rules.";
                Assert.That(ledger[i].StartSeconds, Is.EqualTo(sea[i].StartSeconds), difference);
                Assert.That(ledger[i].EndSeconds, Is.EqualTo(sea[i].EndSeconds), difference);
            }
        }
    }
}
