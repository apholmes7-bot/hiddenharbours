using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    public enum LonglineHookState { Empty, Fish, Stripped }

    public readonly struct LonglineHookResult
    {
        public readonly LonglineHookState State;
        public readonly CatchItem? Catch;
        public readonly double BiteGameTimeSeconds;

        public LonglineHookResult(LonglineHookState state, CatchItem? caught, double biteTime)
        {
            State = state;
            Catch = caught;
            BiteGameTimeSeconds = biteTime;
        }
    }

    /// <summary>Recomputes one hook from placement facts and its haul instant; stores no catch or RNG.</summary>
    public static class LonglineCatch
    {
        public static float BiteChance01(LonglineDef def, double setTime, double haulTime)
            => def != null && def.HasValidTuning
                ? TrapFill.Fill01(setTime, haulTime, 0f, def.SoakHoursToPeak) : 0f;

        public static LonglineHookResult ResolveHook(
            LonglineDef def, in PlacedLonglineDto line, int hookIndex, double haulTime, int worldSeed,
            IReadOnlyList<FishSpeciesDef> pool, IReadOnlyList<string> baitFavours,
            IEnvironmentService environment, ITidalTerrain terrain,
            Func<Vector2, double, CatchContext> contextAt, in DepthDropSettings depthSettings)
        {
            var empty = new LonglineHookResult(LonglineHookState.Empty, null, double.NaN);
            var endA = new Vector2(line.EndAX, line.EndAY);
            var endB = new Vector2(line.EndBX, line.EndBY);
            if (!LonglineGeometry.TryValidate(def, line.Tubs, endA, endB, out int count, out _)
                || line.LonglineDefId != def.Id || string.IsNullOrEmpty(line.InstanceId)
                || pool == null || environment == null || terrain == null || contextAt == null
                || !Finite(line.SetGameTimeSeconds) || !Finite(haulTime) || haulTime < line.SetGameTimeSeconds)
                return empty;
            if (hookIndex < 0 || hookIndex >= count) throw new ArgumentOutOfRangeException(nameof(hookIndex));

            // Invert the linear fill curve once per hook. The midpoint conversion is strictly in (0,1),
            // so a hook never bites at set time, and all have had a chance by the peak. Unlike rolling
            // a new time at haul, this preserves a bite's instant as the soak grows or the save reloads.
            uint hash = TrapFill.SlotHash(worldSeed, line.InstanceId, line.SetGameTimeSeconds, hookIndex);
            double fraction = (hash + 0.5d) / (uint.MaxValue + 1d);
            double biteTime = line.SetGameTimeSeconds + fraction * TrapSoak.SoakSeconds(def.SoakHoursToPeak);
            if (haulTime < biteTime) return empty;

            Vector2 hook = LonglineGeometry.HookPosition(endA, endB, count, hookIndex);
            float floor = TrapPlacement.DepthAt(environment, terrain, biteTime, hook);
            if (float.IsNaN(floor) || float.IsInfinity(floor) || floor <= 0f) return empty;
            CatchContext sea = contextAt(hook, biteTime);
            var ctx = new CatchContext(line.Region, sea.TideHeight, sea.HourOfDay, sea.Season,
                Gear.Longline, floor, floor, LureTag.None, null, sea.School,
                sea.TideRateMetresPerHour, sea.MovingWaterThresholdMetresPerHour);

            // The rod's off-band affinity stays soft. A groundline has a hard species-depth gate;
            // use the same configured zone boundaries, then the unchanged catch resolver.
            FishDepthBand zone = DepthDropMath.ZoneForDepth(floor, depthSettings.TidepoolMaxMeters,
                depthSettings.ShallowsMaxMeters, depthSettings.InshoreMaxMeters,
                depthSettings.MidwaterMaxMeters, depthSettings.DeepMaxMeters);
            var inDepth = new List<FishSpeciesDef>();
            for (int i = 0; i < pool.Count; i++)
            {
                FishSpeciesDef species = pool[i];
                if (species != null && (species.DepthBands == FishDepthBand.None || (species.DepthBands & zone) != 0))
                    inDepth.Add(species);
            }
            List<FishSpeciesDef> weighted = PlacedTrapCatch.BuildWeightedPool(inDepth, baitFavours, def.BaitFavourMultiplier);
            var rng = new System.Random(TrapFill.AnimalCatchSeed(worldSeed, line.InstanceId, line.SetGameTimeSeconds, hookIndex));
            FishSpeciesDef fish = CatchResolver.Resolve(weighted, in ctx, in depthSettings, rng);
            if (fish == null) return empty;
            if (haulTime - biteTime > TrapSoak.SoakSeconds(def.StripHours))
                return new LonglineHookResult(LonglineHookState.Stripped, null, biteTime);

            float weight = CatchResolver.RollWeight(fish, rng);
            // Landing/freshness and licence enforcement belong to the haul service, not this pure roll.
            var caught = new CatchItem(fish.Id, fish.DisplayName, fish.Category, weight, fish.BaseValue,
                fish.SupplyElasticity, fish.SpoilPerDay, default);
            return new LonglineHookResult(LonglineHookState.Fish, caught, biteTime);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
