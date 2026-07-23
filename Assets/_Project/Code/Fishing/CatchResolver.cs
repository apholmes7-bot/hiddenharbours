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

        /// <summary>Full v2 constructor — a cast with a player-held depth (the weighted-rig branch).</summary>
        public CatchContext(string regionId, float tideHeight, float hourOfDay, Season season, Gear gear,
                            float heldDepthM, float floorDepthM)
        {
            RegionId = regionId;
            TideHeight = tideHeight;
            HourOfDay = hourOfDay;
            Season = season;
            Gear = gear;
            HeldDepthM = heldDepthM;
            FloorDepthM = floorDepthM;
        }

        /// <summary>Legacy constructor (preserved) — a depth-less cast; the roll is depth-neutral.</summary>
        public CatchContext(string regionId, float tideHeight, float hourOfDay, Season season, Gear gear)
            : this(regionId, tideHeight, hourOfDay, season, gear, NoDepth, 0f)
        {
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
        {
            if (pool == null || pool.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (Matches(pool[i], in ctx))
                    total += EffectiveWeight(pool[i], in ctx, in depth);

            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            double acc = 0.0;
            for (int i = 0; i < pool.Count; i++)
            {
                FishSpeciesDef f = pool[i];
                if (!Matches(f, in ctx)) continue;
                acc += EffectiveWeight(f, in ctx, in depth);
                if (roll <= acc) return f;
            }
            return null; // floating-point edge; effectively unreachable
        }

        /// <summary>One species' weight in the roll: the authored <c>SpawnWeight</c> (floored, as ever)
        /// times the depth affinity — 1 exactly when the context has no depth or the species is
        /// depth-neutral, so the legacy balance is untouched.</summary>
        public static float EffectiveWeight(FishSpeciesDef f, in CatchContext ctx, in DepthDropSettings depth)
        {
            float w = Mathf.Max(0.0001f, f.SpawnWeight);
            if (!ctx.HasDepth) return w;
            return w * DepthDropMath.SpeciesDepthAffinity(f.DepthBands, f.IsBottomFish,
                                                          ctx.HeldDepthM, ctx.FloorDepthM, in depth);
        }

        /// <summary>Roll a catch weight within the species' size range.</summary>
        public static float RollWeight(FishSpeciesDef f, System.Random rng)
            => f.MinWeightKg + (float)rng.NextDouble() * (f.MaxWeightKg - f.MinWeightKg);
    }
}
