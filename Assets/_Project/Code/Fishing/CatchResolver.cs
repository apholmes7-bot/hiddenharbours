using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>The situation a cast happens in — fed to the resolver.
    ///
    /// <para><b>Rod Fishing v2 grows this additively</b> (the <c>FishingState</c> discipline): the two
    /// depth fields carry the player's HELD column position from the depth drop (design §2.3) so depth can
    /// weight the roll; the original 5-arg constructor is preserved and marks the context depth-less
    /// (<see cref="HeldDepthM"/> = <see cref="NoDepth"/>), so every existing caller — the trap walk, the
    /// legacy cast — compiles unchanged AND rolls exactly as before.</para></summary>
    public readonly struct CatchContext
    {
        /// <summary>Sentinel for "no depth game on this cast" — the legacy/bobber path. Any negative
        /// held depth reads as this.</summary>
        public const float NoDepth = -1f;

        public readonly string RegionId;
        public readonly float TideHeight;
        public readonly float HourOfDay;
        public readonly Season Season;
        public readonly Gear Gear;

        /// <summary>The depth (m) the player is HOLDING the weighted rig at — the depth drop's chosen
        /// band (design §2.3). &lt; 0 (<see cref="NoDepth"/>) = no depth game: the roll is depth-neutral.</summary>
        public readonly float HeldDepthM;

        /// <summary>The floor of the reachable band here (m) — min(bathymetry, line), per
        /// <see cref="DepthDropMath.FloorMeters"/>. Only read when <see cref="HasDepth"/>.</summary>
        public readonly float FloorDepthM;

        /// <summary>True when this cast carries a player-chosen depth (the weighted-rig branch).</summary>
        public bool HasDepth => HeldDepthM >= 0f;

        /// <summary>
        /// WHAT'S ON THE HOOK — the lure presentation the tackle tied on is making
        /// (<see cref="LureTag.None"/> = nothing tied on / bait alone). Tested against each species'
        /// <see cref="FishSpeciesDef.FavoredLures"/> as a soft weight, exactly the seam
        /// <see cref="LureTag"/>'s own doc reserved: a mackerel chases feathers, a haddock ignores them.
        /// </summary>
        public readonly LureTag Lure;

        /// <summary>
        /// The species ids the BAIT on the hook draws (from <c>BaitDef.FavorsSpeciesIds</c>) — null or
        /// empty when fishing a bare lure. Passed as the bait's own list rather than mirrored onto every
        /// species so a bait's preferences live in ONE asset, which is how the trap arc already models it
        /// (<c>PlacedTrapCatch</c>) and what lets a new bait ship without touching a single fish.
        /// </summary>
        public readonly System.Collections.Generic.IReadOnlyList<string> BaitFavours;

        /// <summary>True when real bait is on the hook — the fish has something to EAT, not just
        /// something to chase. The mode the owner's "no bait, no bait-fishing" rule turns on.</summary>
        public bool HasBait => BaitFavours != null && BaitFavours.Count > 0;

        /// <summary>Full constructor — a cast carrying depth, tackle and bait.</summary>
        public CatchContext(string regionId, float tideHeight, float hourOfDay, Season season, Gear gear,
                            float heldDepthM, float floorDepthM,
                            LureTag lure,
                            System.Collections.Generic.IReadOnlyList<string> baitFavours)
        {
            RegionId = regionId;
            TideHeight = tideHeight;
            HourOfDay = hourOfDay;
            Season = season;
            Gear = gear;
            HeldDepthM = heldDepthM;
            FloorDepthM = floorDepthM;
            Lure = lure;
            BaitFavours = baitFavours;
        }

        /// <summary>Depth constructor (preserved) — no tackle, no bait, so the roll is exactly the
        /// depth-weighted one that shipped before bait and tackle existed.</summary>
        public CatchContext(string regionId, float tideHeight, float hourOfDay, Season season, Gear gear,
                            float heldDepthM, float floorDepthM)
            : this(regionId, tideHeight, hourOfDay, season, gear, heldDepthM, floorDepthM,
                   LureTag.None, null)
        {
        }

        /// <summary>Legacy constructor (preserved) — a depth-less cast; the roll is depth-neutral.</summary>
        public CatchContext(string regionId, float tideHeight, float hourOfDay, Season season, Gear gear)
            : this(regionId, tideHeight, hourOfDay, season, gear, NoDepth, 0f)
        {
        }

        /// <summary>True when this bait draws <paramref name="speciesId"/>. Ordinal, case-sensitive —
        /// ids are canonical (rule 2).</summary>
        public bool BaitFavours_Contains(string speciesId)
        {
            if (BaitFavours == null || string.IsNullOrEmpty(speciesId)) return false;
            for (int i = 0; i < BaitFavours.Count; i++)
                if (string.Equals(BaitFavours[i], speciesId, System.StringComparison.Ordinal)) return true;
            return false;
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

        /// <summary>Pick a species by weighted chance among those that match, or null if none bite.
        /// Depth-neutral (the legacy roll): even a context CARRYING a depth rolls flat here — use the
        /// <see cref="Resolve(IReadOnlyList{FishSpeciesDef}, in CatchContext, in DepthDropSettings, System.Random)"/>
        /// overload to let the held depth weight the pick.</summary>
        public static FishSpeciesDef Resolve(IReadOnlyList<FishSpeciesDef> pool, in CatchContext ctx, System.Random rng)
            => Resolve(pool, in ctx, DepthDropSettings.Default, rng);

        /// <summary>
        /// Pick a species by weighted chance among those that match, with the player's HELD DEPTH folded in
        /// as one more soft weight (Rod Fishing v2 §2.3/§6.1 — depth as the species-targeting tactic). Each
        /// matching species' weight is <c>SpawnWeight × depth affinity</c>
        /// (<see cref="DepthDropMath.SpeciesDepthAffinity"/>): a boost in its preferred zones, a damp
        /// outside them, a further boost for a Bottom species held just off the floor — and exactly ×1 for
        /// a depth-less context or an unauthored species, so this is the SAME roll as the legacy overload
        /// whenever depth has nothing to say (the resolver's balance is not rewritten). One
        /// <see cref="System.Random.NextDouble"/> per call, exactly as before — same seed, same stream,
        /// same result structure (rule 5).
        /// </summary>
        public static FishSpeciesDef Resolve(IReadOnlyList<FishSpeciesDef> pool, in CatchContext ctx,
                                             in DepthDropSettings depth, System.Random rng)
            => Resolve(pool, in ctx, in depth, BaitTackleSettings.Default, rng);

        /// <summary>
        /// The full roll, with the owner's BAIT and TACKLE weights (<see cref="GameConfig.BaitTackle"/>)
        /// threaded in beside the depth ones. Still exactly one <c>NextDouble</c> per call over a
        /// weighted walk — the kit changes the weights, never the stream (rule 5), so a seeded catch with
        /// the same kit replays bit-for-bit.
        /// </summary>
        public static FishSpeciesDef Resolve(IReadOnlyList<FishSpeciesDef> pool, in CatchContext ctx,
                                             in DepthDropSettings depth, in BaitTackleSettings kit,
                                             System.Random rng)
        {
            if (pool == null || pool.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (Matches(pool[i], in ctx))
                    total += EffectiveWeight(pool[i], in ctx, in depth, in kit);

            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            double acc = 0.0;
            for (int i = 0; i < pool.Count; i++)
            {
                FishSpeciesDef f = pool[i];
                if (!Matches(f, in ctx)) continue;
                acc += EffectiveWeight(f, in ctx, in depth, in kit);
                if (roll <= acc) return f;
            }
            return null; // floating-point edge; effectively unreachable
        }

        /// <summary>One species' weight in the roll: the authored <c>SpawnWeight</c> (floored, as ever)
        /// times the depth affinity — 1 exactly when the context has no depth or the species is
        /// depth-neutral, so the legacy balance is untouched.</summary>
        public static float EffectiveWeight(FishSpeciesDef f, in CatchContext ctx, in DepthDropSettings depth)
            => EffectiveWeight(f, in ctx, in depth, BaitTackleSettings.Default);

        /// <summary>
        /// One species' weight in the roll, with the owner's BAIT and TACKLE weights folded in beside the
        /// depth affinity. Every term is a multiplier on the authored <c>SpawnWeight</c> and every one of
        /// them is a soft WEIGHT, never a filter: the wrong bait and the wrong lure catch <i>less</i>,
        /// they never catch <i>nothing</i> — the same promise depth makes, and the reason a beginner with
        /// the wrong kit still catches fish.
        ///
        /// <para>Bait and tackle are independent multipliers because they are independent choices: a
        /// squid strip on a cod jig should be better for a cod than either alone. Neutral (×1) whenever
        /// there is nothing to say — no bait, no tackle, or an unauthored species — so a context carrying
        /// neither is bit-for-bit the roll that shipped before this existed.</para>
        /// </summary>
        public static float EffectiveWeight(FishSpeciesDef f, in CatchContext ctx,
                                            in DepthDropSettings depth, in BaitTackleSettings kit)
        {
            float w = Mathf.Max(0.0001f, f.SpawnWeight);

            if (ctx.HasDepth)
                w *= DepthDropMath.SpeciesDepthAffinity(f.DepthBands, f.IsBottomFish,
                                                        ctx.HeldDepthM, ctx.FloorDepthM, in depth);

            w *= BaitAffinity(f, in ctx, in kit);
            w *= LureAffinity(f, in ctx, in kit);
            return w;
        }

        /// <summary>
        /// The BAIT multiplier: a boost for a species this bait draws, a damp for one it doesn't, and
        /// exactly 1 when no bait is on the hook (a lure-only cast is judged on its tackle alone).
        /// </summary>
        public static float BaitAffinity(FishSpeciesDef f, in CatchContext ctx, in BaitTackleSettings kit)
        {
            if (!ctx.HasBait) return 1f;
            return ctx.BaitFavours_Contains(f.Id)
                ? Mathf.Max(1f, kit.BaitFavourBoost)
                : Mathf.Clamp01(kit.WrongBaitDamp01);
        }

        /// <summary>
        /// The TACKLE multiplier: a boost when the tackle tied on is one this species chases — matched
        /// EITHER from the species' <see cref="FishSpeciesDef.FavoredLures"/> mask or (checked by the
        /// caller, via <see cref="CatchContext.BaitFavours"/>'s tackle twin) the tackle's own favours
        /// list. Exactly 1 with nothing tied on, so bait-only fishing is unaffected by this term.
        ///
        /// <para>The damp for the WRONG lure is deliberately gentler than the wrong bait's: a fish that
        /// wants food will refuse the wrong food outright, but a curious fish will still occasionally
        /// hit the wrong lure. That asymmetry is what makes bait the precise tool and tackle the
        /// broad one.</para>
        /// </summary>
        public static float LureAffinity(FishSpeciesDef f, in CatchContext ctx, in BaitTackleSettings kit)
        {
            if (ctx.Lure == LureTag.None) return 1f;
            return f.LureFavored(ctx.Lure)
                ? Mathf.Max(1f, kit.LureFavourBoost)
                : Mathf.Clamp01(kit.WrongLureDamp01);
        }

        /// <summary>Roll a catch weight within the species' size range.</summary>
        public static float RollWeight(FishSpeciesDef f, System.Random rng)
            => f.MinWeightKg + (float)rng.NextDouble() * (f.MaxWeightKg - f.MinWeightKg);
    }
}
