using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy.SupplyChain
{
    /// <summary>Unplaced NPC-chain proposal. Region identity only; no geometry or live registration.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish chain/Plant")]
    public sealed class FishPlantDef : ScriptableObject
    {
        public string Id, RegionId, IntakeStorageId;
        public GameConfig ClockConfig;
        public int StepSeconds;
        public long DailyIntakeGrams;
        public FishStorageDef[] Storages;
        public FishLineDef[] Lines;
        public FishOutletDef[] Outlets;
        public FishTruckRunDef[] Runs;

        /// <summary>Setup only. Snapshot the asset fields and validate before starting the slow tick.</summary>
        public FishChainSpec BuildSpec()
        {
            if (ClockConfig == null || ClockConfig.SecondsPerDay != System.Math.Truncate(ClockConfig.SecondsPerDay) ||
                ClockConfig.DaysPerWeek != 7)
                throw new System.ArgumentException("Fish chain requires the shared seven-day clock with integral day seconds.");
            var spec = new FishChainSpec
            {
                Id = Id, RegionId = RegionId, IntakeStorageId = IntakeStorageId,
                StepSeconds = StepSeconds, SecondsPerDay = checked((int)ClockConfig.SecondsPerDay),
                DaysPerSeason = ClockConfig.DaysPerSeason, StartDayOfYear = ClockConfig.StartDayOfYear,
                DailyIntakeGrams = DailyIntakeGrams,
                // These are managed data snapshots, independent of Unity's JSON serialization path.
                Storages = System.Array.ConvertAll(Storages, d => d.Spec.Snapshot()),
                Lines = System.Array.ConvertAll(Lines, d => d.Spec.Snapshot()),
                Outlets = System.Array.ConvertAll(Outlets, d => d.Spec.Snapshot()),
                Runs = System.Array.ConvertAll(Runs, d => d.Spec.Snapshot())
            };
            spec.Validate();
            return spec;
        }
    }
}
