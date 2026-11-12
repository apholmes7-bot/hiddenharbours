using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Economy.SupplyChain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>Requires Unity asset loading / Market lifecycle. Native proof does not claim these ran.</summary>
    public sealed class FishChainUnityTests
    {
        private const string Root = "Assets/_Project/Data/FishSupplyChain";

        [Test]
        public void ContentValidation_NewPlantDefsLoadWithUniqueIdsAndResolvedContentReferences()
        {
            var plant = AssetDatabase.LoadAssetAtPath<FishPlantDef>(Root + "/NineMileCreekFishPlant.asset");
            Assert.That(plant, Is.Not.Null);
            FishChainSpec spec = plant.BuildSpec();
            Assert.That(spec.SecondsPerDay, Is.EqualTo(plant.ClockConfig.SecondsPerDay));
            Assert.That(spec.StartDayOfYear, Is.EqualTo(plant.ClockConfig.StartDayOfYear));
            Assert.That(spec.Id, Is.EqualTo("fishplant.nmc_industrial"));
            Assert.That(spec.RegionId, Is.EqualTo("region.nine_mile_creek"));
            Assert.That(spec.Storages.Length, Is.EqualTo(21)); Assert.That(spec.Lines.Length, Is.EqualTo(4));
            Assert.That(spec.Outlets.Length, Is.EqualTo(6)); Assert.That(spec.Runs.Length, Is.EqualTo(9));
            Assert.That(AssetDatabase.FindAssets("t:FishPlantDef", new[] { Root }).Length, Is.EqualTo(1));
            Assert.That(AssetDatabase.FindAssets("t:FishStorageDef", new[] { Root }).Length, Is.EqualTo(21));
            Assert.That(AssetDatabase.FindAssets("t:FishLineDef", new[] { Root }).Length, Is.EqualTo(4));
            Assert.That(AssetDatabase.FindAssets("t:FishOutletDef", new[] { Root }).Length, Is.EqualTo(6));
            Assert.That(AssetDatabase.FindAssets("t:FishTruckRunDef", new[] { Root }).Length, Is.EqualTo(9));
            var ids = new HashSet<string> { spec.Id };
            foreach (var row in spec.Storages) CheckId(ids, row.Id);
            foreach (var row in spec.Lines) CheckId(ids, row.Id);
            foreach (var row in spec.Outlets) CheckId(ids, row.Id);
            foreach (var row in spec.Runs) CheckId(ids, row.Id);
            HashSet<string> regions = ContentIds("RegionDef"), species = ContentIds("FishSpeciesDef");
            HashSet<string> vehicles = ContentIds("VehicleDef"), trips = ContentIds("VehicleTripDef"), routes = ContentIds("RouteDef");
            Assert.That(regions.Contains(spec.RegionId), Is.True);
            foreach (var row in spec.Lines) Assert.That(species.Contains(row.SpeciesId), Is.True, row.Id);
            foreach (var row in spec.Outlets) Assert.That(species.Contains(row.SpeciesId), Is.True, row.Id);
            int groceries = 0, restaurants = 0, hotels = 0, exports = 0;
            foreach (var row in spec.Outlets)
            {
                if (row.Kind == FishOutletKind.Grocery) groceries++;
                if (row.Kind == FishOutletKind.Restaurant) restaurants++;
                if (row.Kind == FishOutletKind.Hotel) hotels++;
                if (row.Kind == FishOutletKind.Export) exports++;
                Assert.That(row.DemandByWeekday.Length, Is.EqualTo(7));
                Assert.That(row.SeasonalDemandPpm.Length, Is.EqualTo(4));
                Assert.That(row.DemandByWeekday[0], Is.Positive);
            }
            Assert.That(groceries, Is.EqualTo(2)); Assert.That(restaurants, Is.EqualTo(2));
            Assert.That(hotels, Is.EqualTo(1)); Assert.That(exports, Is.EqualTo(1));
            int incoming = 0, offIsland = 0;
            var landings = new HashSet<string>();
            foreach (var row in spec.Runs)
            {
                Assert.That(vehicles.Contains(row.VehicleId), Is.True, row.Id);
                Assert.That(regions.Contains(row.OriginRegionId), Is.True, row.Id);
                if (!row.LeavesIsland) Assert.That(regions.Contains(row.DestinationRegionId), Is.True, row.Id);
                if (!string.IsNullOrEmpty(row.ExistingTripId)) Assert.That(trips.Contains(row.ExistingTripId), Is.True, row.Id);
                if (!string.IsNullOrEmpty(row.ExistingRouteId)) Assert.That(routes.Contains(row.ExistingRouteId), Is.True, row.Id);
                if (spec.Store(row.SourceStorageId).Kind == FishStoreKind.Wharf) { incoming++; landings.Add(row.OriginRegionId); }
                if (row.LeavesIsland) offIsland++;
            }
            Assert.That(incoming, Is.EqualTo(3)); Assert.That(offIsland, Is.EqualTo(1));
            CollectionAssert.AreEquivalent(new[] { "region.nine_mile_creek", "region.rice_point", "region.st_peters" }, landings);
        }

        private static void CheckId(HashSet<string> ids, string id)
        {
            StringAssert.IsMatch(@"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$", id);
            Assert.That(ids.Add(id), Is.True, id);
        }

        private static HashSet<string> ContentIds(string type)
        {
            var ids = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets/_Project/Data" }))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var field = new SerializedObject(asset).FindProperty("Id");
                if (field != null) ids.Add(field.stringValue);
            }
            return ids;
        }

        private sealed class MarketPort : IFishChainMarket
        {
            private readonly Market _market;
            private long _registered;
            public bool Blocked;
            public MarketPort(Market market) { _market = market; }
            public void Report(in FishChainPressure pressure)
            {
                if (pressure.SpeciesId != FishChainModelTests.Cod) return;
                int delta = (int)(pressure.LandedFish - _registered);
                if (delta > 0) _market.RegisterSale(FishCategory.InshoreGroundfish, delta);
                _registered = pressure.LandedFish; Blocked |= pressure.PlantBlocked;
            }
        }

        [Test]
        public void FullPlant_BackedUpWharfPriceFallsThroughExistingMarketInterfaceWithoutDuplicateSales()
        {
            var go = new GameObject("FishChainMarketProof");
            try
            {
                var market = go.AddComponent<Market>(); market.Configure(null, MarketId.NineMileCreek);
                market.SetCategoryDemand(FishCategory.InshoreGroundfish, 100f);
                var port = new MarketPort(market); var work = new FishChainModelTests.Work();
                var spec = FishChainModelTests.Fixture(); spec.DailyIntakeGrams = 2000;
                var model = new FishChainModel(spec, 17, work, work, port);
                Assert.That(model.TryLand("wharf", new FishLot(FishChainModelTests.Cod, 10, 1000, 1000)), Is.True);
                model.AdvanceTo(40);
                int before = market.NextUnitPrice(FishCategory.InshoreGroundfish, 100, 0.2f);
                Assert.That(model.TryLand("wharf", new FishLot(FishChainModelTests.Cod, 30, 1000, 1000)), Is.True);
                model.AdvanceTo(200);
                Assert.That(port.Blocked, Is.True); Assert.That(model.CountAt("wharf", FishChainModelTests.Cod), Is.EqualTo(30));
                Assert.That(market.SupplyOf(FishCategory.InshoreGroundfish), Is.EqualTo(40f));
                Assert.That(market.NextUnitPrice(FishCategory.InshoreGroundfish, 100, 0.2f), Is.LessThan(before));
                model.AdvanceTo(300);
                Assert.That(market.SupplyOf(FishCategory.InshoreGroundfish), Is.EqualTo(40f), "snapshots cannot resell the backlog");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
