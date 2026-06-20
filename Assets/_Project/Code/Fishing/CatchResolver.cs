using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>The situation a cast happens in — fed to the resolver.</summary>
    public readonly struct CatchContext
    {
        public readonly string RegionId;
        public readonly float TideHeight;
        public readonly float HourOfDay;
        public readonly Season Season;
        public readonly Gear Gear;

        public CatchContext(string regionId, float tideHeight, float hourOfDay, Season season, Gear gear)
        {
            RegionId = regionId;
            TideHeight = tideHeight;
            HourOfDay = hourOfDay;
            Season = season;
            Gear = gear;
        }
    }

    /// <summary>
    /// Decides what (if anything) bites: filter the pool by the context, then pick one by weighted
    /// chance. Pure and RNG-injected so it is fully unit-testable and reproducible with a seeded
    /// <see cref="System.Random"/>. See design/fish-and-content.md (catch resolution).
    /// </summary>
    public static class CatchResolver
    {
        /// <summary>True if this species can be caught in the given situation.</summary>
        public static bool Matches(FishSpeciesDef f, in CatchContext ctx)
        {
            return f != null
                && f.RegionAllowed(ctx.RegionId)
                && f.GearAllowed(ctx.Gear)
                && f.SeasonAllowed(ctx.Season)
                && f.TimeAllowed(ctx.HourOfDay)
                && f.TideAllowed(ctx.TideHeight);
        }

        /// <summary>Pick a species by weighted chance among those that match, or null if none bite.</summary>
        public static FishSpeciesDef Resolve(IReadOnlyList<FishSpeciesDef> pool, in CatchContext ctx, System.Random rng)
        {
            if (pool == null || pool.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (Matches(pool[i], in ctx))
                    total += Mathf.Max(0.0001f, pool[i].SpawnWeight);

            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            double acc = 0.0;
            for (int i = 0; i < pool.Count; i++)
            {
                FishSpeciesDef f = pool[i];
                if (!Matches(f, in ctx)) continue;
                acc += Mathf.Max(0.0001f, f.SpawnWeight);
                if (roll <= acc) return f;
            }
            return null; // floating-point edge; effectively unreachable
        }

        /// <summary>Roll a catch weight within the species' size range.</summary>
        public static float RollWeight(FishSpeciesDef f, System.Random rng)
            => f.MinWeightKg + (float)rng.NextDouble() * (f.MaxWeightKg - f.MinWeightKg);
    }
}
