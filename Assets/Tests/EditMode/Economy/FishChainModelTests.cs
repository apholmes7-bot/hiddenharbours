using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy.SupplyChain;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Economy
{
    public sealed class FishChainModelTests
    {
        internal const string Cod = "fish.atlantic_cod", Lobster = "fish.lobster";

        internal sealed class Work : IFishChainHands, IFishChainCarrier, IFishChainMarket
        {
            public bool Hands = true, Arrival = true, Route = true, ReturnArrival = true;
            public int Calls;
            public bool Blocked;
            public int ConfirmTransfer(in FishChainTransfer transfer) { Calls++; return Hands ? transfer.RequestedFish : 0; }
            public bool CanLoad(string run, string route, long now) => Route;
            public bool HasArrived(string run, bool returning, long departed, long now) => returning ? ReturnArrival : Arrival;
            public void Report(in FishChainPressure pressure) { Blocked |= pressure.PlantBlocked; }
        }

        internal static FishChainSpec Fixture()
        {
            var stores = new List<FishStorageSpec>();
            Add(stores, "wharf", FishStoreKind.Wharf); Add(stores, "intake", FishStoreKind.Intake);
            Add(stores, "cold", FishStoreKind.Cold); Add(stores, "dry", FishStoreKind.Dry);
            Add(stores, "in-truck", FishStoreKind.Truck);
            var lines = new FishLineSpec[4];
            for (int i = 0; i < lines.Length; i++)
                lines[i] = new FishLineSpec
                {
                    Id = "line." + i, SpeciesId = i == 3 ? Lobster : Cod,
                    Product = (FishProduct)(i + 1), OutputStorageId = i == 2 ? "dry" : "cold",
                    YieldPpm = 500000, BatchFish = 4, ProcessSeconds = 10, RequiredWorkers = 1,
                    LabourSecondsPerFish = 5, WorkMassCapacityGrams = 40000, WorkVolumeCapacityMl = 40000
                };
            var outlets = new FishOutletSpec[6];
            var runs = new FishTruckRunSpec[7];
            runs[0] = MakeRun("run.in", "wharf", "in-truck", "intake");
            for (int i = 0; i < outlets.Length; i++)
            {
                string store = "outlet." + i, cargo = "truck." + i;
                Add(stores, store, FishStoreKind.Outlet); Add(stores, cargo, FishStoreKind.Truck);
                FishProduct product = i == 0 ? FishProduct.FreshFillet : i == 1 ? FishProduct.Canned :
                    i == 2 ? FishProduct.FreshFillet : i == 3 ? FishProduct.Lobster : FishProduct.Frozen;
                outlets[i] = new FishOutletSpec
                {
                    Id = "customer." + i, StorageId = store, SpeciesId = product == FishProduct.Lobster ? Lobster : Cod,
                    Kind = i < 2 ? FishOutletKind.Grocery : i < 4 ? FishOutletKind.Restaurant :
                        i == 4 ? FishOutletKind.Hotel : FishOutletKind.Export,
                    Product = product, PricePerFish = i + 10,
                    DemandByWeekday = new[] { 2, 3, 4, 5, 6, 7, 8 },
                    SeasonalDemandPpm = new[] { 1000000, 2000000, 1000000, 500000 }
                };
                runs[i + 1] = MakeRun("run." + i, product == FishProduct.Canned ? "dry" : "cold", cargo, store);
                runs[i + 1].LeavesIsland = i == 5;
                if (i == 5) runs[i + 1].DestinationRegionId = "";
            }
            return new FishChainSpec
            {
                Id = "plant.test", RegionId = "region.nine_mile_creek", IntakeStorageId = "intake",
                StepSeconds = 10, SecondsPerDay = 600, DaysPerSeason = 2, DailyIntakeGrams = 1000000,
                Storages = stores.ToArray(), Lines = lines, Outlets = outlets, Runs = runs
            };
        }

        private static void Add(List<FishStorageSpec> stores, string id, FishStoreKind kind) => stores.Add(
            new FishStorageSpec { Id = id, Kind = kind, MassCapacityGrams = 10000000, VolumeCapacityMl = 10000000, LotSlots = 8 });

        private static FishTruckRunSpec MakeRun(string id, string source, string cargo, string target) => new FishTruckRunSpec
        {
            Id = id, VehicleId = "vehicle.dually_3500", ExistingRouteId = "test.route", OriginRegionId = "region.test",
            DestinationRegionId = "region.nine_mile_creek", SourceStorageId = source, CargoStorageId = cargo,
            DestinationStorageId = target, HandFishPerStep = 10, HandSecondsPerFish = 1, TravelSeconds = 10
        };

        internal static FishChainModel Start(FishChainSpec spec, Work work, uint seed = 17)
        {
            var model = new FishChainModel(spec, seed, work, work, work);
            for (int i = 0; i < spec.Lines.Length; i++) model.SetWorkers(i, 1);
            return model;
        }

        [Test]
        public void Calendar_UsesSharedCoreSeasonOffsetAtTheConfiguredDayBoundary()
        {
            var spec = Fixture(); spec.StartDayOfYear = 2;
            var model = Start(spec, new Work());
            model.AdvanceTo(0); Assert.That(model.RemainingDemand(0), Is.EqualTo(4), "summer at elapsed day zero");
            model.AdvanceTo(600); Assert.That(model.RemainingDemand(0), Is.EqualTo(6));
            model.AdvanceTo(1200); Assert.That(model.RemainingDemand(0), Is.EqualTo(4), "autumn begins on shared boundary");
        }

        [Test]
        public void Conservation_EveryTickPreservesEachSpeciesCountAndGramsIncludingYieldAndConsumption()
        {
            var spec = Fixture(); var work = new Work(); var model = Start(spec, work);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 120, 1000, 1000)), Is.True);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 17, 2300, 2700)), Is.True);
            Assert.That(model.TryLand("wharf", new FishLot(Lobster, 60, 700, 900)), Is.True);
            for (int now = 0; now <= 2400; now += 10)
            {
                model.AdvanceTo(now);
                Assert.That(model.TotalFish(Cod), Is.EqualTo(137), "cod at " + now);
                Assert.That(model.TotalGrams(Cod), Is.EqualTo(159100), "cod mass at " + now);
                Assert.That(model.TotalFish(Lobster), Is.EqualTo(60), "lobster at " + now);
                Assert.That(model.TotalGrams(Lobster), Is.EqualTo(42000), "lobster mass at " + now);
                foreach (var storage in spec.Storages)
                {
                    Assert.That(model.MassAt(storage.Id), Is.InRange(0L, storage.MassCapacityGrams));
                    Assert.That(model.VolumeAt(storage.Id), Is.InRange(0L, storage.VolumeCapacityMl));
                }
            }
            Assert.That(model.ByproductGrams(Cod), Is.Positive);
            Assert.That(model.Consumed(Cod), Is.Positive);
            Assert.That(model.Consumed(Lobster), Is.Positive);
        }

        [Test]
        public void Determinism_FromSeedAndTimeIsIndependentOfSlowTickPollingFrequency()
        {
            var spec = Fixture(); var a = Start(spec, new Work(), 912); var b = Start(spec, new Work(), 912);
            foreach (var model in new[] { a, b })
            {
                Assert.That(model.TryLand("wharf", new FishLot(Cod, 150, 1100, 1300)), Is.True);
                Assert.That(model.TryLand("wharf", new FishLot(Lobster, 40, 600, 800)), Is.True);
            }
            for (int now = 0; now < 2400; now += 7) a.AdvanceTo(now);
            a.AdvanceTo(2400); b.AdvanceTo(2400);
            Assert.That(a.Revenue, Is.EqualTo(b.Revenue));
            Assert.That(a.Revenue, Is.Positive);
            foreach (var storage in spec.Storages)
                for (int i = 0; i < storage.LotSlots; i++) Assert.That(a.LotAt(storage.Id, i), Is.EqualTo(b.LotAt(storage.Id, i)));
            for (int i = 0; i < spec.Outlets.Length; i++)
            {
                Assert.That(a.OutletConsumed(i), Is.EqualTo(b.OutletConsumed(i)));
                Assert.That(a.RemainingDemand(i), Is.EqualTo(b.RemainingDemand(i)));
            }
            for (int i = 0; i < spec.Runs.Length; i++) Assert.That(a.Phase(i), Is.EqualTo(b.Phase(i)));
            Assert.That(a.ByproductGrams(Cod), Is.EqualTo(b.ByproductGrams(Cod)));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.AdvanceTo(2399));
        }

        [Test]
        public void StarvedPlant_ProducesNoFishMoneyOrConsumption()
        {
            var model = Start(Fixture(), new Work()); model.AdvanceTo(2400);
            Assert.That(model.TotalFish(Cod), Is.Zero); Assert.That(model.TotalGrams(Lobster), Is.Zero);
            Assert.That(model.Revenue, Is.Zero); Assert.That(model.ByproductGrams(Cod), Is.Zero);
            for (int i = 0; i < 6; i++) Assert.That(model.OutletConsumed(i), Is.Zero);
        }

        [Test]
        public void FullPlant_BacksUpWharfAndRetainsBlockedCargo()
        {
            var spec = Fixture(); spec.DailyIntakeGrams = 2000;
            var work = new Work(); var model = Start(spec, work);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 50, 1000, 1000)), Is.True);
            model.AdvanceTo(200);
            Assert.That(model.AcceptedTodayGrams, Is.EqualTo(2000));
            Assert.That(model.CountAt("wharf", Cod), Is.EqualTo(40));
            Assert.That(model.CountAt("in-truck", Cod), Is.EqualTo(8));
            Assert.That(work.Blocked, Is.True); Assert.That(model.TotalFish(Cod), Is.EqualTo(50));
            model.AdvanceTo(620);
            Assert.That(model.AcceptedTodayGrams, Is.EqualTo(2000), "daily cap resets without discarding the waiting load");
            Assert.That(model.CountAt("in-truck", Cod), Is.EqualTo(6));
        }

        [Test]
        public void EachOutletKind_DrawsExactDailyAndSeasonalDemandFromDeliveredStock()
        {
            var spec = Fixture(); var model = Start(spec, new Work());
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 1000, 1000, 1000)), Is.True);
            Assert.That(model.TryLand("wharf", new FishLot(Lobster, 500, 1000, 1000)), Is.True);
            model.AdvanceTo(590);
            for (int i = 0; i < 6; i++) Assert.That(model.OutletConsumed(i), Is.EqualTo(2), "day zero " + i);
            model.AdvanceTo(1190);
            for (int i = 0; i < 6; i++) Assert.That(model.OutletConsumed(i), Is.EqualTo(5), "day one " + i);
            model.AdvanceTo(1790);
            for (int i = 0; i < 6; i++) Assert.That(model.OutletConsumed(i), Is.EqualTo(13), "summer " + i);
        }

        [Test]
        public void ProcessingLines_ShareDeliveriesSmallerThanOneBatchWithoutStarvingFrozenOrCanned()
        {
            var spec = Fixture(); spec.Runs[0].HandFishPerStep = 2;
            var model = Start(spec, new Work());
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 100, 1000, 1000)), Is.True);
            model.AdvanceTo(590);
            Assert.That(model.OutletConsumed(0), Is.EqualTo(2), "fresh");
            Assert.That(model.OutletConsumed(1), Is.EqualTo(2), "canned");
            Assert.That(model.OutletConsumed(4), Is.EqualTo(2), "frozen");
            Assert.That(model.TotalFish(Cod), Is.EqualTo(100));
        }

        [Test]
        public void Export_LeavesIslandOnlyAfterBoundaryArrivalAndHandUnloading()
        {
            var spec = Fixture(); var work = new Work(); var model = Start(spec, work);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 50, 1000, 1000)), Is.True);
            // Let the inbound run finish, then withhold all subsequent destination arrival receipts.
            model.AdvanceTo(20); work.Arrival = false; model.AdvanceTo(300);
            Assert.That(model.CountAt("truck.5", Cod), Is.EqualTo(2));
            Assert.That(model.Exported(5), Is.Zero); Assert.That(model.OutletConsumed(5), Is.Zero);
            work.Arrival = true; work.Hands = false; model.AdvanceTo(310);
            model.AdvanceTo(320); Assert.That(model.Exported(5), Is.Zero);
            work.Hands = true; model.AdvanceTo(330);
            Assert.That(model.Exported(5), Is.EqualTo(2)); Assert.That(model.OutletConsumed(5), Is.EqualTo(2));
            Assert.That(model.CountAt("truck.5", Cod), Is.Zero); Assert.That(model.TotalFish(Cod), Is.EqualTo(50));
        }

        [Test]
        public void MissingHandsOrRoute_CannotMoveGoodsEvenWhenTimePasses()
        {
            var work = new Work { Hands = false }; var model = Start(Fixture(), work);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 10, 1000, 1000)), Is.True);
            model.AdvanceTo(100); Assert.That(model.CountAt("wharf", Cod), Is.EqualTo(10));
            work.Hands = true; work.Route = false; model.AdvanceTo(200);
            Assert.That(model.CountAt("wharf", Cod), Is.EqualTo(10));
            work.Route = true; model.AdvanceTo(210);
            Assert.That(model.CountAt("in-truck", Cod), Is.EqualTo(10)); Assert.That(model.CountAt("intake", Cod), Is.Zero);
        }

        [Test]
        public void FullOutput_BlocksWorkWithoutLosingFishAndUnstaffedLineDoesNoWork()
        {
            var spec = Fixture(); spec.Store("cold").MassCapacityGrams = 1;
            var work = new Work(); var model = Start(spec, work);
            for (int i = 0; i < 4; i++) model.SetWorkers(i, 0);
            Assert.That(model.TryLand("wharf", new FishLot(Lobster, 4, 1000, 1000)), Is.True);
            model.AdvanceTo(100); Assert.That(model.CountAt("intake", Lobster), Is.EqualTo(4));
            model.SetWorkers(3, 1); model.AdvanceTo(200);
            Assert.That(model.CountAt("intake", Lobster), Is.Zero);
            Assert.That(model.MassAt("cold"), Is.Zero); Assert.That(model.TotalFish(Lobster), Is.EqualTo(4));
            Assert.That(model.TotalGrams(Lobster), Is.EqualTo(4000)); Assert.That(model.ByproductGrams(Lobster), Is.Zero);
        }

        [Test]
        public void StorageMassVolumeAndSlots_RejectWholeLandingsWithoutLaunderingIndividualSizes()
        {
            var spec = Fixture(); var wharf = spec.Store("wharf"); wharf.LotSlots = 1; wharf.VolumeCapacityMl = 2000;
            var model = Start(spec, new Work());
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 2, 1000, 1000)), Is.True);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 1, 500, 500)), Is.False);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 1, 1000, 1000)), Is.False);
            Assert.That(model.LotAt("wharf", 0).IndividualRawGrams, Is.EqualTo(1000));
            Assert.That(model.TotalFish(Cod), Is.EqualTo(2));
        }

        [Test]
        public void ZeroAllocationTick_BusyTransfersProcessingAndDemandUseNoManagedBytes()
        {
            long probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024]; GC.KeepAlive(probe);
            long probeBytes = GC.GetAllocatedBytesForCurrentThread() - probeBefore;
            if (probeBytes < 1024) Assert.Ignore("Allocation counter cannot observe a retained positive-control allocation.");
            var spec = Fixture(); var work = new Work(); var model = Start(spec, work);
            Assert.That(model.TryLand("wharf", new FishLot(Cod, 5000, 1000, 1000)), Is.True);
            Assert.That(model.TryLand("wharf", new FishLot(Lobster, 4000, 1000, 1000)), Is.True);
            model.AdvanceTo(600); int calls = work.Calls; long revenue = model.Revenue;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int now = 610; now <= 6000; now += 10) model.AdvanceTo(now);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero); Assert.That(work.Calls, Is.GreaterThan(calls));
            Assert.That(model.Revenue, Is.GreaterThan(revenue));
            Assert.That(model.TotalFish(Cod), Is.EqualTo(5000));
        }

        [Test]
        public void ContentValidation_RejectsBadReferencesSharedCargoAndWrongStorageTemperature()
        {
            var spec = Fixture(); Assert.DoesNotThrow(spec.Validate);
            spec.Runs[1].CargoStorageId = spec.Runs[0].CargoStorageId;
            Assert.Throws<ArgumentException>(spec.Validate);
            spec = Fixture(); spec.Lines[0].OutputStorageId = "dry"; Assert.Throws<ArgumentException>(spec.Validate);
            spec = Fixture(); spec.Outlets[0].SpeciesId = "fish.missing"; Assert.Throws<ArgumentException>(spec.Validate);
            spec = Fixture(); spec.Runs[0].DestinationStorageId = "cold"; Assert.Throws<ArgumentException>(spec.Validate);
        }
    }
}
