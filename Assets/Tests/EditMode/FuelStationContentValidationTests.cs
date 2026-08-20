using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for fuel retail (the <c>ContentValidationTests</c> pattern, over the ACTUAL
    /// <c>Data/</c> assets).
    ///
    ///   • THE DEFS ARE SANE — unique non-empty ids, known grades only, no duplicate rows, and a price on
    ///     everything actually stocked. A grade string outside the fixed art contract is an authoring slip
    ///     that would silently never match a can.
    ///   • THE OWNER'S DIRECTION IS PINNED IN DATA — the wharf pumps charge MORE than the Route 91 station
    ///     for every grade both sell, and Route 91 stocks the lot. That is the whole design (convenience
    ///     costs money; completeness costs a walk), and it lives in two assets a re-price could quietly
    ///     invert. This is the test that would notice.
    ///   • CANON HOLDS — Nine Mile Creek sells gas AND diesel (<c>fuel-and-refuelling.md</c> §3). St Peters
    ///     being gas-only is not asserted here because St Peters has no station asset yet: its gas is sold
    ///     in cans over a counter, which is a SupplyDef, and a separate piece of work.
    ///   • THE SHIPPED CANS MEET THE SEAM — a real <c>FuelContainerDef</c> on a real
    ///     <c>FuelLevelPresenter</c> reports the right litres and takes a real fill. That is the join
    ///     between the art lane's 84 container Defs and the economy's pump, and neither suite alone
    ///     covers it.
    /// </summary>
    public class FuelStationContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string WharfId = "station.nmc_wharf_pumps";
        private const string RoadId = "station.route_91";

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static FuelStationDef ById(string id)
        {
            foreach (var s in LoadAll<FuelStationDef>())
                if (s.Id == id) return s;
            Assert.Fail($"no FuelStationDef with id '{id}' in {DataRoot}");
            return null;
        }

        // ---- the Defs are sane -------------------------------------------------------------------

        [Test]
        public void Stations_Exist_AndHaveNonEmptyUniqueIds()
        {
            var defs = LoadAll<FuelStationDef>();
            Assert.IsNotEmpty(defs, "the slice ships at least one FuelStationDef");

            var seen = new Dictionary<string, string>();
            foreach (var d in defs)
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Id), $"{path}: empty id");
                Assert.IsFalse(seen.ContainsKey(d.Id), $"duplicate FuelStationDef id '{d.Id}' in '{path}'");
                seen[d.Id] = path;
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.DisplayName), $"{path}: empty DisplayName");
                StringAssert.StartsWith("station.", d.Id, $"{path}: station ids are 'station.snake_case'");
            }
        }

        [Test]
        public void EveryAuthoredGrade_IsOneOfTheFixedSet()
        {
            // The grade strings are a FIXED CONTRACT shared with the art lane (a colourway is baked per
            // grade). A typo here would not fail loudly - it would simply never match a can, at one
            // pump, forever.
            foreach (var d in LoadAll<FuelStationDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsNotNull(d.Grades, $"{path}: null Grades array");
                foreach (var g in d.Grades)
                    Assert.IsTrue(FuelGrades.IsKnown(g.Grade),
                        $"{path}: '{g.Grade}' is not a known fuel grade");
            }
        }

        [Test]
        public void NoStation_AuthorsTheSameGradeTwice()
        {
            // FuelStationDef.Offer takes the FIRST match, so a duplicate row is a price the owner can edit
            // with no effect - the worst kind of authoring bug.
            foreach (var d in LoadAll<FuelStationDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                var seen = new HashSet<string>();
                foreach (var g in d.Grades)
                    Assert.IsTrue(seen.Add(g.Grade),
                        $"{path}: '{g.Grade}' authored twice - only the first row is ever read");
            }
        }

        [Test]
        public void EveryStockedGrade_HasAPrice()
        {
            // An UNAVAILABLE row may carry a price - that is the point of keeping the two fields apart
            // (a parked tuning the owner ticks back on). An AVAILABLE row at zero would be free fuel.
            foreach (var d in LoadAll<FuelStationDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                foreach (var g in d.Grades)
                {
                    Assert.GreaterOrEqual(g.PricePerLitre, 0f, $"{path}: '{g.Grade}' has a negative price");
                    if (g.Available)
                        Assert.Greater(g.PricePerLitre, 0f,
                            $"{path}: '{g.Grade}' is on sale at 0 a litre - free fuel is almost certainly a slip");
                }
            }
        }

        // ---- the owner's direction, pinned in data -----------------------------------------------

        [Test]
        public void BothNineMileCreekStations_Ship()
        {
            Assert.IsNotNull(ById(WharfId));
            Assert.IsNotNull(ById(RoadId));
        }

        [Test]
        public void NineMileCreek_SellsBothGasAndDiesel()
        {
            // Canon (fuel-and-refuelling.md §3): NMC has both pumps. The wharf is where a boat lies
            // alongside, so it is the wharf that must satisfy this, not the road station.
            var wharf = ById(WharfId);
            Assert.IsTrue(wharf.Sells(FuelGrades.Gas), "canon: Nine Mile Creek sells gas");
            Assert.IsTrue(wharf.Sells(FuelGrades.Diesel), "canon: Nine Mile Creek sells diesel");
        }

        [Test]
        public void TheWharfPumps_ChargeMoreThanTheRoadStation()
        {
            // THE OWNER'S DIRECTION, 2026-08-20: the wharf pumps charge a HIGHER price than the full
            // station up at Route 91. That difference IS the design - lying alongside is convenience you
            // pay for - and it lives entirely in two editable assets. A re-price that inverted it would
            // make the walk pointless and nothing else would notice.
            var wharf = ById(WharfId);
            var road = ById(RoadId);

            int compared = 0;
            foreach (string grade in FuelGrades.All)
            {
                if (!wharf.Sells(grade) || !road.Sells(grade)) continue;
                compared++;
                Assert.Greater(wharf.PricePerLitre(grade), road.PricePerLitre(grade),
                    $"'{grade}' must cost more at the wharf than at Route 91");
            }

            Assert.Greater(compared, 0, "the two stations must overlap on something, or there is no choice to make");
        }

        [Test]
        public void TheRoadStation_IsTheCompleteOne()
        {
            // "A full gas station" - it is the reference site, so it stocks every grade the world knows,
            // including the stove oil the island's houses burn (municipal-infrastructure.md).
            var road = ById(RoadId);
            foreach (string grade in FuelGrades.All)
                Assert.IsTrue(road.Sells(grade), $"the full station must stock '{grade}'");

            Assert.Less(ById(WharfId).StockedGradeCount, road.StockedGradeCount,
                "a couple of hoses on a wharf must stock less than a full station, or the walk buys nothing");
        }

        // ---- the shipped cans meet the pump's seam -----------------------------------------------

        [Test]
        public void TheShippedContainers_CoverEveryGradeTheStationsSell()
        {
            // A grade on sale with no vessel in the world that holds it is money you cannot spend.
            //
            // ⚠️ STOVE OIL IS THE ONE KNOWN GAP, and it is named here rather than hidden by a weaker
            // assertion. The fuel rig baked four colourways - gas, diesel, mixed, oil - so all 84 shipped
            // container Defs are one of those; stove_oil arrived later, with the municipal doc (the island
            // burns oil), and has no vessel yet. It is still SOLD, because the grade is real and the
            // station data is the owner's to tune; you simply cannot carry any home until the art lane
            // bakes a stove-oil colourway. Delete this exemption the day it does.
            var noVesselYet = new HashSet<string> { FuelGrades.StoveOil };

            var haveVessel = new HashSet<string>();
            foreach (var c in LoadAll<FuelContainerDef>())
                if (c.Carriable && c.CapacityLitres > 0f) haveVessel.Add(c.Grade);

            foreach (var station in LoadAll<FuelStationDef>())
                foreach (var g in station.Grades)
                {
                    if (!g.Available || noVesselYet.Contains(g.Grade)) continue;
                    Assert.IsTrue(haveVessel.Contains(g.Grade),
                        $"'{g.Grade}' is sold at '{station.Id}' but no carriable container Def holds it");
                }
        }

        [Test]
        public void ARealContainerDef_OnARealPresenter_ReportsLitresAndTakesAFill()
        {
            // The JOIN: the art lane's Def, the art lane's presenter, and the economy's IFuelVessel seam.
            // Neither lane's own suite crosses this line.
            FuelContainerDef can = null;
            foreach (var c in LoadAll<FuelContainerDef>())
                if (c.Id == "fuelstore.gas_jerry_s20") { can = c; break; }
            Assert.IsNotNull(can, "the 20 L gas jerry can is the hero object - it must ship");

            var go = new GameObject("can");
            _spawned.Add(go);
            var presenter = go.AddComponent<FuelLevelPresenter>();
            presenter.Container = can;
            presenter.Fill = 0f;

            var vessel = (IFuelVessel)presenter;
            Assert.AreEqual(can.Grade, vessel.Grade);
            Assert.AreEqual(can.CapacityLitres, vessel.CapacityLitres, 1e-4f);
            Assert.AreEqual(0f, vessel.Litres, 1e-4f);

            vessel.Deliver(can.CapacityLitres * 0.5f);
            Assert.AreEqual(can.CapacityLitres * 0.5f, vessel.Litres, 1e-3f);
            Assert.AreEqual(0.5f, presenter.Fill, 1e-3f, "litres and the drawn fill are ONE number");

            // Over-pouring stops at the brim rather than running the fraction past 1.
            vessel.Deliver(can.CapacityLitres * 10f);
            Assert.AreEqual(can.CapacityLitres, vessel.Litres, 1e-3f);
            Assert.AreEqual(1f, presenter.Fill, 1e-4f);
        }

        [Test]
        public void AVesselThatHoldsNothing_ReportsNoCapacity()
        {
            // The nozzles and dispenser heads in the kit are vessels in name only; a pump must read them
            // as "not something to fill" rather than dividing by their capacity.
            FuelContainerDef nozzle = null;
            foreach (var c in LoadAll<FuelContainerDef>())
                if (c.Id == "fuelstore.gas_nozzle_sauto") { nozzle = c; break; }
            if (nozzle == null) Assert.Ignore("no nozzle Def shipped under that id - nothing to prove here");

            var go = new GameObject("nozzle");
            _spawned.Add(go);
            var presenter = go.AddComponent<FuelLevelPresenter>();
            presenter.Container = nozzle;

            var offer = new FuelGradeOffer { Grade = nozzle.Grade, Available = true, PricePerLitre = 2f };
            Assert.AreEqual(FuelRefusal.NoVessel,
                FuelPricing.Quote(offer, (IFuelVessel)presenter, money: 1000).Refusal);
        }
    }
}
