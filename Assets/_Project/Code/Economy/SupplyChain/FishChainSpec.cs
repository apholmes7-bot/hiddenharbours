using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy.SupplyChain
{
    public enum FishStoreKind { Wharf, Truck, Intake, Cold, Dry, Outlet }
    public enum FishOutletKind { Grocery, Restaurant, Hotel, Export }

    [Serializable]
    public sealed class FishStorageSpec
    {
        public string Id;
        public FishStoreKind Kind;
        public long MassCapacityGrams, VolumeCapacityMl;
        public int LotSlots;

        public FishStorageSpec Snapshot() => (FishStorageSpec)MemberwiseClone();
    }

    [Serializable]
    public sealed class FishLineSpec
    {
        public string Id, SpeciesId, OutputStorageId;
        public FishProduct Product;
        public int YieldPpm, BatchFish, ProcessSeconds, RequiredWorkers, LabourSecondsPerFish;
        public long WorkMassCapacityGrams, WorkVolumeCapacityMl;

        public FishLineSpec Snapshot() => (FishLineSpec)MemberwiseClone();
    }

    [Serializable]
    public sealed class FishOutletSpec
    {
        public string Id, StorageId, SpeciesId;
        public FishOutletKind Kind;
        public FishProduct Product;
        public int PricePerFish;
        public int[] DemandByWeekday = new int[7];
        public int[] SeasonalDemandPpm = new int[4];

        public FishOutletSpec Snapshot()
        {
            var copy = (FishOutletSpec)MemberwiseClone();
            copy.DemandByWeekday = (int[])DemandByWeekday?.Clone();
            copy.SeasonalDemandPpm = (int[])SeasonalDemandPpm?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class FishTruckRunSpec
    {
        public string Id, VehicleId, ExistingTripId, ExistingRouteId;
        public string OriginRegionId, DestinationRegionId;
        public bool LeavesIsland;
        public string SourceStorageId, CargoStorageId, DestinationStorageId;
        public int HandFishPerStep, HandSecondsPerFish, TravelSeconds;

        public FishTruckRunSpec Snapshot() => (FishTruckRunSpec)MemberwiseClone();
    }

    public sealed class FishChainSpec
    {
        public const int PartsPerMillion = 1000000;
        public string Id, RegionId, IntakeStorageId;
        public int StepSeconds, SecondsPerDay, DaysPerSeason, StartDayOfYear;
        public long DailyIntakeGrams;
        public FishStorageSpec[] Storages;
        public FishLineSpec[] Lines;
        public FishOutletSpec[] Outlets;
        public FishTruckRunSpec[] Runs;

        public void Validate()
        {
            Require(!string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(RegionId), "plant identity");
            Require(StepSeconds > 0 && SecondsPerDay >= StepSeconds && SecondsPerDay % StepSeconds == 0 &&
                DaysPerSeason > 0 && StartDayOfYear >= 0 && StartDayOfYear < 4L * DaysPerSeason && DailyIntakeGrams > 0,
                "plant calendar/capacity");
            Require(Storages != null && Lines != null && Outlets != null && Runs != null, "plant lists");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            ids.Add(Id);
            foreach (var s in Storages)
            {
                Require(s != null && ids.Add(s.Id ?? "") && !string.IsNullOrWhiteSpace(s.Id), "storage identity");
                Require(Enum.IsDefined(typeof(FishStoreKind), s.Kind) && s.MassCapacityGrams > 0 &&
                    s.VolumeCapacityMl > 0 && s.LotSlots > 0, "storage bounds");
            }
            Require(Store(IntakeStorageId).Kind == FishStoreKind.Intake, "plant intake");
            foreach (var l in Lines)
            {
                Require(l != null && ids.Add(l.Id ?? "") && !string.IsNullOrWhiteSpace(l.Id) &&
                    !string.IsNullOrWhiteSpace(l.SpeciesId), "line identity/species");
                Require(l.Product != FishProduct.Raw && Enum.IsDefined(typeof(FishProduct), l.Product) &&
                    l.YieldPpm > 0 && l.YieldPpm <= PartsPerMillion && l.BatchFish > 0 &&
                    l.ProcessSeconds > 0 && l.RequiredWorkers > 0 && l.LabourSecondsPerFish > 0 &&
                    l.WorkMassCapacityGrams > 0 && l.WorkVolumeCapacityMl > 0, "line bounds");
                Require(Store(l.OutputStorageId).Kind == (l.Product == FishProduct.Canned ? FishStoreKind.Dry : FishStoreKind.Cold),
                    "line cold/dry compatibility");
            }
            foreach (var o in Outlets)
            {
                Require(o != null && ids.Add(o.Id ?? "") && !string.IsNullOrWhiteSpace(o.Id) &&
                    !string.IsNullOrWhiteSpace(o.SpeciesId), "outlet identity/species");
                Require(Enum.IsDefined(typeof(FishOutletKind), o.Kind) && o.PricePerFish > 0 &&
                    Store(o.StorageId).Kind == FishStoreKind.Outlet, "outlet bounds/store");
                Require(Array.Exists(Lines, l => l.SpeciesId == o.SpeciesId && l.Product == o.Product), "outlet product");
                Require(o.DemandByWeekday != null && o.DemandByWeekday.Length == 7 &&
                    o.SeasonalDemandPpm != null && o.SeasonalDemandPpm.Length == 4, "outlet calendar");
                foreach (int n in o.DemandByWeekday) Require(n >= 0, "negative demand");
                foreach (int n in o.SeasonalDemandPpm) Require(n >= 0, "negative seasonal demand");
            }
            var cargoIds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var r in Runs)
            {
                Require(r != null && ids.Add(r.Id ?? "") && !string.IsNullOrWhiteSpace(r.Id) &&
                    !string.IsNullOrWhiteSpace(r.VehicleId) && !string.IsNullOrWhiteSpace(r.OriginRegionId) &&
                    (r.LeavesIsland || !string.IsNullOrWhiteSpace(r.DestinationRegionId)), "run identity");
                Require(r.HandFishPerStep > 0 && r.HandSecondsPerFish > 0 && r.HandSecondsPerFish <= StepSeconds &&
                    r.TravelSeconds > 0 && Store(r.CargoStorageId).Kind == FishStoreKind.Truck &&
                    cargoIds.Add(r.CargoStorageId), "run capacity/exclusive cargo");
                FishStoreKind source = Store(r.SourceStorageId).Kind, destination = Store(r.DestinationStorageId).Kind;
                bool inbound = source == FishStoreKind.Wharf && destination == FishStoreKind.Intake;
                bool outbound = (source == FishStoreKind.Cold || source == FishStoreKind.Dry) &&
                    destination == FishStoreKind.Outlet;
                Require(inbound || outbound, "run endpoints");
                Require(!inbound || r.DestinationStorageId == IntakeStorageId, "run plant intake");
                if (outbound)
                {
                    var outlet = Array.Find(Outlets, o => o.StorageId == r.DestinationStorageId);
                    Require(outlet != null && source == (outlet.Product == FishProduct.Canned ? FishStoreKind.Dry : FishStoreKind.Cold),
                        "outbound store compatibility");
                    Require(r.LeavesIsland == (outlet.Kind == FishOutletKind.Export), "export boundary declaration");
                }
                else Require(!r.LeavesIsland, "inbound remains on island");
            }
            foreach (var o in Outlets)
                Require(Array.FindAll(Outlets, x => x.StorageId == o.StorageId).Length == 1 &&
                    Array.FindAll(Runs, r => r.DestinationStorageId == o.StorageId).Length == 1, "exclusive outlet/run");
        }

        public FishStorageSpec Store(string id)
        {
            if (Storages != null)
                for (int i = 0; i < Storages.Length; i++) if (Storages[i].Id == id) return Storages[i];
            throw new ArgumentException("Unresolved chain storage: " + id);
        }

        private static void Require(bool condition, string rule)
        {
            if (!condition) throw new ArgumentException("Invalid fish chain: " + rule);
        }
    }
}
