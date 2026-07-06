using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The <b>deterministic</b> catch resolution for a placed trap (trap-fishing arc Build 3). When a soaked
    /// trap is hauled (or the dev "check" fires), this decides <em>what</em> it caught — and it is a pure
    /// function of the trap's placement facts, so a save→load→haul lands the <b>identical</b> catch (rule 5).
    ///
    /// <para><b>Reuses the one roller (no new catch logic).</b> The species pick and the size roll are the
    /// existing <see cref="CatchResolver"/> — the same <see cref="CatchResolver.Resolve"/> weighted pick and
    /// <see cref="CatchResolver.RollWeight"/> the rod uses. This class only does two trap-specific things
    /// around it: (1) seed a <see cref="System.Random"/> from a <b>stable</b> hash of the placement facts so
    /// the roll is reproducible (<see cref="StableHash.TrapCatchSeed"/>, NOT <c>string.GetHashCode()</c>,
    /// which is per-process randomized and would break reload determinism), and (2) apply the bait's
    /// <b>soft weight</b> toward its favoured species.</para>
    ///
    /// <para><b>Bait soft-weights, it doesn't gate (owner's call).</b> Bait <em>nudges</em> the roll toward
    /// the species its <see cref="Economy.BaitDef.FavorsSpeciesIds"/> lists — both catches stay possible, herring
    /// just leans lobster and fish-scrap leans crab. We express the nudge by <b>repeating</b> a favoured
    /// species in the pool <see cref="BaitFavourMultiplier"/>× before handing it to the unchanged resolver,
    /// so a favoured species carries proportionally more of the weighted total — reusing the roller exactly
    /// rather than reaching into its weighting. (A required-bait <em>gate</em>, if a Def wants one, is a
    /// separate check the caller applies; this class only weights.)</para>
    /// </summary>
    public static class PlacedTrapCatch
    {
        /// <summary>How many times over a bait's favoured species is entered into the pool — the strength of
        /// the soft nudge. 3 = a favoured species carries 3× its base weight vs an unfavoured one in the same
        /// pool; both remain possible. A tunable, not a magic number (see <see cref="PlacedTrap"/>, which
        /// surfaces it as a serialized field so the owner can tune the lean). Kept here as the shared default.
        /// </summary>
        public const int BaitFavourMultiplier = 3;

        /// <summary>
        /// Resolve the trap's catch deterministically. Returns the caught <see cref="CatchItem"/>, or null if
        /// nothing was landed (an empty/unresolved pool, or the context gates everything out). The
        /// <paramref name="rng"/> is injected so tests are reproducible and the production caller can pass a
        /// stream seeded from <see cref="StableHash.TrapCatchSeed"/>; given the same seed + pool + context +
        /// bait favours, the result is bit-identical every run.
        /// </summary>
        /// <param name="pool">The trap's allowed catch species (resolved from
        /// <see cref="TrapDef.AllowedCatchFishIds"/> via <see cref="FishSpeciesRegistry"/>).</param>
        /// <param name="ctx">Region/tide/hour/season/gear the haul happens in (gear = <see cref="Gear.Trap"/>).</param>
        /// <param name="baitFavours">The loaded bait's favoured species ids (its soft-weight targets), or null
        /// for an unbaited trap (no nudge).</param>
        /// <param name="favourMultiplier">How strongly favoured species lean (≥1; 1 = no nudge).</param>
        /// <param name="rng">The injected, seeded RNG (deterministic per placement).</param>
        public static CatchItem? Resolve(
            IReadOnlyList<FishSpeciesDef> pool,
            in CatchContext ctx,
            IReadOnlyList<string> baitFavours,
            int favourMultiplier,
            System.Random rng)
        {
            if (pool == null || pool.Count == 0 || rng == null) return null;

            // Build the weighted pool: repeat a bait-favoured species so it carries more of the total. Order
            // is stable (base pool order, favoured copies appended in pool order) so the resolver's weighted
            // walk over the same seed is reproducible. Reuses CatchResolver.Resolve UNCHANGED on this pool.
            List<FishSpeciesDef> weighted = BuildWeightedPool(pool, baitFavours, favourMultiplier);

            FishSpeciesDef fish = CatchResolver.Resolve(weighted, in ctx, rng);
            if (fish == null) return null;

            float weightKg = CatchResolver.RollWeight(fish, rng);   // same size roll the rod uses
            return new CatchItem(fish.Id, fish.DisplayName, fish.Category,
                                 weightKg, fish.BaseValue, fish.SupplyElasticity);
        }

        /// <summary>
        /// The base pool, with each bait-favoured species repeated <paramref name="favourMultiplier"/>× so the
        /// unchanged weighted resolver leans toward it (a soft nudge, both still possible). A null/empty
        /// favours list or a multiplier ≤ 1 returns the base pool unchanged (no nudge). Exposed for tests so
        /// the bias can be asserted directly.
        /// </summary>
        public static List<FishSpeciesDef> BuildWeightedPool(
            IReadOnlyList<FishSpeciesDef> pool, IReadOnlyList<string> baitFavours, int favourMultiplier)
        {
            var weighted = new List<FishSpeciesDef>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) weighted.Add(pool[i]);

            if (baitFavours == null || baitFavours.Count == 0 || favourMultiplier <= 1)
                return weighted;

            int extra = Mathf.Max(0, favourMultiplier - 1);   // "3×" = the base entry + 2 extra copies
            int baseCount = weighted.Count;
            for (int i = 0; i < baseCount; i++)
            {
                FishSpeciesDef f = weighted[i];
                if (f != null && Favours(baitFavours, f.Id))
                    for (int c = 0; c < extra; c++) weighted.Add(f);
            }
            return weighted;
        }

        private static bool Favours(IReadOnlyList<string> favours, string id)
        {
            if (favours == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < favours.Count; i++)
                if (string.Equals(favours[i], id, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
