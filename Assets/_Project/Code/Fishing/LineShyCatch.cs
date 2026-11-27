using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>The rod's catch roll with the line's small-fish rule</b> (rods from parts, owner 2026-11-25: a
    /// heavier line "maybe deters certain smaller fish").
    ///
    /// <para>With a line that puts nothing off this IS <see cref="CatchResolver.Resolve(IReadOnlyList{FishSpeciesDef}, in CatchContext, in DepthDropSettings, in BaitTackleSettings, System.Random)"/>,
    /// called unchanged, so every shipped rod rolls exactly as before. With a line that does, the roll
    /// keeps its ONE random draw over the SAME total and the SAME walk as the resolver's; a small fish
    /// bites on only <see cref="RodFit.ShyFactor(in RodFishingProfile, float)"/> of its own stretch, and
    /// the rest is "nothing bit" (null). So a heavy line catches fewer small fish, never more big ones,
    /// and the same seed replays the same result (rule 5).</para>
    /// </summary>
    public static class LineShyCatch
    {
        public static FishSpeciesDef Resolve(IReadOnlyList<FishSpeciesDef> pool, in CatchContext ctx,
                                             in DepthDropSettings depth, in BaitTackleSettings kit,
                                             in RodFishingProfile rod, System.Random rng)
        {
            if (!rod.PutsSmallFishOff) return CatchResolver.Resolve(pool, in ctx, in depth, in kit, rng);
            if (pool == null || pool.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (CatchResolver.Matches(pool[i], in ctx))
                    total += CatchResolver.EffectiveWeight(pool[i], in ctx, in depth, in kit);

            if (total <= 0f) return null;

            // Each species keeps its own stretch of the roll, exactly where the resolver puts it, and bites
            // only on the first ShyFactor of that stretch: the rest is the bite the line put off. So every
            // roll that lands a bigger fish without this line lands the same fish with it.
            double roll = rng.NextDouble() * total;
            double acc = 0.0;
            for (int i = 0; i < pool.Count; i++)
            {
                FishSpeciesDef f = pool[i];
                if (!CatchResolver.Matches(f, in ctx)) continue;
                float weight = CatchResolver.EffectiveWeight(f, in ctx, in depth, in kit);
                if (weight <= 0f) continue;
                double start = acc;
                acc += weight;
                if (roll <= acc)
                    return roll <= start + weight * RodFit.ShyFactor(in rod, f.MaxWeightKg) ? f : null;
            }
            return null; // floating-point edge; effectively unreachable
        }
    }
}
