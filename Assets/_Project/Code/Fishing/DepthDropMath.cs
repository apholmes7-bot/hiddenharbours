using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PURE maths of the <b>depth drop</b> — Rod Fishing v2's standout mechanic
    /// (design/rod-fishing-v2-brainstorm.md §2.1/§2.3/§6): drop a weighted rig with no cast, COUNT THE
    /// FALL to judge depth (heavier rigs fall faster — the owner's decision #4: no depth gauge, fall-time
    /// + the slack tell are the whole read), click to re-engage the reel and hold a mid-column band, or
    /// let it run until the rig BOTTOMS OUT and the line goes slack — then reel up slightly to sit just
    /// off the floor, the bottom-fishing sweet spot. The held depth then feeds the catch roll as a soft
    /// WEIGHT (<see cref="SpeciesDepthAffinity"/>): depth is the species-targeting tactic.
    ///
    /// <para><b>The <see cref="RodFightMath"/>/<c>TrapHaulMath</c> lane discipline:</b> pure, side-effect
    /// free statics — no <c>Time</c>, no RNG, no scene, nothing saved, NaN-safe (rule 5), every constant a
    /// passed parameter backed by <see cref="DepthDropSettings"/> on the GameConfig (rule 6). The caller
    /// (<see cref="FishingController"/>) owns the accumulator and integrates with ITS dt
    /// (<see cref="FallStep"/>/<see cref="ReelStep"/>); this class never accumulates.</para>
    ///
    /// <para><b>Who reads what.</b> The controller publishes <c>FishingState.Depth01</c>
    /// (<see cref="Depth01"/>) and the bottom-slack state (<see cref="IsBottomed"/>) on the Core signal;
    /// the Art lane's <c>RodLineMath</c> (sink ripples, the slack-overshoot pop) is DRIVEN by those
    /// published values — it is never called from here (rule 4: presentation meets gameplay only at the
    /// Core contract).</para>
    /// </summary>
    public static class DepthDropMath
    {
        // ==== the gear branch (design §2.1 — which way do we fish?) ==================================

        /// <summary>
        /// True when the selected gear fishes the DEPTH branch (drop and read the column) rather than the
        /// cast/bobber branch: <see cref="Gear.Jig"/> and <see cref="Gear.Longline"/> always do; a
        /// <see cref="Gear.Handline"/> does when rigged at or above
        /// <paramref name="weightedHandlineMinKg"/> (the light handline keeps the cast path). Every other
        /// gear (nets, traps, dredge, clam fork) never drops a line down a column. Pure, NaN-safe (a NaN
        /// rig weight reads as weightless → the cast branch).
        /// </summary>
        /// <param name="gear">The player's selected gear (one value, per <see cref="CatchContext"/>).</param>
        /// <param name="rigWeightKg">The rig/lure weight tied on (kg).</param>
        /// <param name="weightedHandlineMinKg">A handline at/above this weight fishes the depth branch
        /// (<see cref="DepthDropSettings.WeightedHandlineMinKg"/>).</param>
        public static bool IsWeightedRig(Gear gear, float rigWeightKg, float weightedHandlineMinKg)
        {
            if ((gear & (Gear.Jig | Gear.Longline)) != 0) return true;
            if ((gear & Gear.Handline) != 0)
                return Safe(rigWeightKg) >= Mathf.Max(0f, Safe(weightedHandlineMinKg));
            return false;
        }

        // ==== the fall (count-the-fall — heavier is faster, decision #4) =============================

        /// <summary>
        /// How fast a rig of <paramref name="rigWeightKg"/> sinks (m/s): <c>perKg · kg</c>, clamped to
        /// [<paramref name="minMps"/>, <paramref name="maxMps"/>]. <b>Monotonically non-decreasing in
        /// weight</b> — the property the whole diegetic read rests on: heavier falls faster, so counting
        /// the fall IS reading the depth, and lure weight is a real tactical choice (reach the deep band
        /// quickly vs fish a slow mid-column). Pure, NaN-safe (NaN weight → the minimum speed).
        /// </summary>
        public static float SinkSpeedMps(float rigWeightKg, float perKgMps, float minMps, float maxMps)
        {
            float lo = Mathf.Max(0f, Safe(minMps));
            float hi = Mathf.Max(lo, Safe(maxMps));
            float v = Mathf.Max(0f, Safe(rigWeightKg)) * Mathf.Max(0f, Safe(perKgMps));
            return Mathf.Clamp(v, lo, hi);
        }

        /// <summary>
        /// One integration step of the fall: <c>depth + sinkSpeed · dt</c>, clamped into
        /// [0, <paramref name="floorM"/>] — the rig can never fall past the floor of the reachable band
        /// (that's the bottom-out, read via <see cref="IsBottomed"/>). Negative dt is a no-op (never rise
        /// on a bad frame). Pure, NaN-safe.
        /// </summary>
        public static float FallStep(float depthM, float sinkSpeedMps, float dt, float floorM)
        {
            float floor = Mathf.Max(0f, Safe(floorM));
            float d = Mathf.Max(0f, Safe(depthM)) + Mathf.Max(0f, Safe(sinkSpeedMps)) * Mathf.Max(0f, Safe(dt));
            return Mathf.Min(d, floor);
        }

        /// <summary>One integration step of reeling UP (the "reel up slightly" move): <c>depth − reelUpMps
        /// · dt</c>, never below the surface (0). Pure, NaN-safe.</summary>
        public static float ReelStep(float depthM, float reelUpMps, float dt)
            => Mathf.Max(0f, Mathf.Max(0f, Safe(depthM)) - Mathf.Max(0f, Safe(reelUpMps)) * Mathf.Max(0f, Safe(dt)));

        // ==== the reachable band + the bottom tell (design §2.3) =====================================

        /// <summary>
        /// The FLOOR of the reachable band (m): the shallower of the bathymetric water column here and the
        /// line the reel carries — <c>min(waterColumnM, maxLineM)</c>, never negative. Pass
        /// <see cref="float.PositiveInfinity"/> as the column when no bathymetry is authored (open water /
        /// no tidal terrain service — the same "service absent → gate off" posture as
        /// <c>TidalWalkability</c>): the band is then line-length-capped only. A dry or negative column
        /// (the tide left the rig's spot bare) floors at 0 — the rig is on the bottom immediately. Pure,
        /// NaN-safe (NaN column → 0, the cautious floor).
        /// </summary>
        public static float FloorMeters(float waterColumnM, float maxLineM)
        {
            float line = Mathf.Max(0f, Safe(maxLineM));
            float column = float.IsNaN(waterColumnM) ? 0f : Mathf.Max(0f, waterColumnM);
            return Mathf.Min(column, line);
        }

        /// <summary>
        /// The published depth read, 0 (surface) .. 1 (on the floor of the reachable band) — the value the
        /// controller writes into <c>FishingState.Depth01</c> each tick so presentation (the sinking-line
        /// ripples) and the catch context stay one number. A collapsed band (floor ≤ 0) reads 1: the rig
        /// is on the bottom the moment it's wet. Pure, NaN-safe.
        /// </summary>
        public static float Depth01(float depthM, float floorM)
        {
            float floor = Mathf.Max(0f, Safe(floorM));
            if (floor <= 0f) return 1f;
            return Mathf.Clamp01(Safe(depthM) / floor);
        }

        /// <summary>
        /// True when the rig has BOTTOMED OUT — depth has reached the floor of the reachable band
        /// (<c>depthM ≥ floorM</c>; a collapsed band is always bottomed). This is the moment the line goes
        /// slack: the controller flips <c>FishingState.SlackWindowOpen</c> on it, and the Art lane's
        /// <c>RodLineMath.SlackOvershoot</c> pops the sag off that transition — "you felt the floor", no
        /// number. Exact at the floor by design (≥, not &gt;), so the tell triggers precisely when
        /// <see cref="FallStep"/>'s clamp lands the rig. Pure, NaN-safe.
        /// </summary>
        public static bool IsBottomed(float depthM, float floorM)
        {
            float floor = Mathf.Max(0f, Safe(floorM));
            if (floor <= 0f) return true;
            return Safe(depthM) >= floor;
        }

        /// <summary>
        /// True while the rig is held JUST OFF the floor — inside <paramref name="sweetWindowM"/> metres
        /// above it but NOT resting on it (the bottom-fishing sweet spot, §2.3 step 4: bottom out, then
        /// reel up slightly). Sitting on the floor is deliberately outside the window — the lift is the
        /// skill beat, and <see cref="SpeciesDepthAffinity"/> only pays the bottom boost here. Pure,
        /// NaN-safe.
        /// </summary>
        public static bool InBottomSweetWindow(float depthM, float floorM, float sweetWindowM)
        {
            float floor = Mathf.Max(0f, Safe(floorM));
            if (floor <= 0f) return false;                       // collapsed band: on the bottom, no window
            float d = Safe(depthM);
            if (d >= floor) return false;                        // resting ON the floor — lift first
            return d >= floor - Mathf.Max(0f, Safe(sweetWindowM));
        }

        // ==== the depth zones + the catch weighting (design §2.3 "why it matters", §6.1) =============

        /// <summary>
        /// Which fishing depth zone an absolute depth (m) reads as, from the five owner thresholds
        /// (<see cref="DepthDropSettings"/>): ≤ tidepoolMax → Tidepool; then Shallows, Inshore, Midwater,
        /// Deep at their thresholds (inclusive on the shallower side, tiling without gaps — the
        /// <c>TidalExposure.BandForDepth</c> pattern); deeper than <paramref name="deepMaxM"/> → Abyssal.
        /// Always returns exactly one flag. Pure, NaN-safe (NaN depth reads as the surface → Tidepool).
        /// </summary>
        public static FishDepthBand ZoneForDepth(float depthM, float tidepoolMaxM, float shallowsMaxM,
                                                 float inshoreMaxM, float midwaterMaxM, float deepMaxM)
        {
            float d = Mathf.Max(0f, Safe(depthM));
            // Each threshold is at least the previous, so authoring a crossed pair can't invert the bands.
            float t0 = Mathf.Max(0f, Safe(tidepoolMaxM));
            float t1 = Mathf.Max(t0, Safe(shallowsMaxM));
            float t2 = Mathf.Max(t1, Safe(inshoreMaxM));
            float t3 = Mathf.Max(t2, Safe(midwaterMaxM));
            float t4 = Mathf.Max(t3, Safe(deepMaxM));
            if (d <= t0) return FishDepthBand.Tidepool;
            if (d <= t1) return FishDepthBand.Shallows;
            if (d <= t2) return FishDepthBand.Inshore;
            if (d <= t3) return FishDepthBand.Midwater;
            if (d <= t4) return FishDepthBand.Deep;
            return FishDepthBand.Abyssal;
        }

        /// <summary>
        /// The catch-roll WEIGHT multiplier the held depth gives one species — how depth becomes the
        /// species-targeting tactic (§2.3): hold in a zone the species lives in
        /// (<paramref name="speciesBands"/> includes the zone of <paramref name="heldDepthM"/>) →
        /// ×<see cref="DepthDropSettings.InBandAffinity"/>; hold outside its zones →
        /// ×<see cref="DepthDropSettings.OffBandAffinity"/> (damped, never zero — a weight alongside
        /// bait/season/weather/time, NOT a filter); and a <see cref="FishFlags.Bottom"/> species held just
        /// off the floor (<see cref="InBottomSweetWindow"/>) is further
        /// ×<see cref="DepthDropSettings.BottomWindowAffinity"/> — the payoff for bottoming out and
        /// lifting slightly.
        ///
        /// <para><b>Neutral by construction</b> when there is no read to weight: a negative
        /// <paramref name="heldDepthM"/> (no depth game — the legacy cast path) or an unauthored species
        /// (<see cref="FishDepthBand.None"/>, no Bottom flag) multiplies by exactly 1, so every existing
        /// pool and every legacy cast rolls precisely as before. The result is clamped to a small positive
        /// minimum so no tuning can zero a species out. Pure, deterministic, NaN-safe.</para>
        /// </summary>
        /// <param name="speciesBands">The species' preferred depth zones (<c>FishSpeciesDef.DepthBands</c>);
        /// None = depth-neutral.</param>
        /// <param name="bottomSpecies">Whether the species carries <see cref="FishFlags.Bottom"/>.</param>
        /// <param name="heldDepthM">The depth the player is holding the rig at (m); &lt; 0 = no depth game.</param>
        /// <param name="floorM">The floor of the reachable band here (m), per <see cref="FloorMeters"/>.</param>
        /// <param name="s">The owner's depth-drop tuning (<c>GameConfig.DepthDrop</c>).</param>
        public static float SpeciesDepthAffinity(FishDepthBand speciesBands, bool bottomSpecies,
                                                 float heldDepthM, float floorM, in DepthDropSettings s)
        {
            if (float.IsNaN(heldDepthM) || heldDepthM < 0f) return 1f;   // no depth game → neutral

            float affinity = 1f;

            if (speciesBands != FishDepthBand.None)
            {
                FishDepthBand zone = ZoneForDepth(heldDepthM, s.TidepoolMaxMeters, s.ShallowsMaxMeters,
                                                  s.InshoreMaxMeters, s.MidwaterMaxMeters, s.DeepMaxMeters);
                affinity *= (speciesBands & zone) != 0
                    ? Mathf.Max(1f, Safe(s.InBandAffinity))
                    : Mathf.Clamp(Safe(s.OffBandAffinity), 0.01f, 1f);
            }

            if (bottomSpecies && InBottomSweetWindow(heldDepthM, floorM, s.BottomSweetWindowMeters))
                affinity *= Mathf.Max(1f, Safe(s.BottomWindowAffinity));

            return Mathf.Max(0.0001f, affinity);
        }

        // ---- guards -----------------------------------------------------------------------------

        /// <summary>NaN → 0 (the safe, neutral value) — Unity's <c>Mathf.Clamp</c> passes NaN through, so
        /// inputs are sanitized first (the <see cref="RodFightMath"/>/<c>TrapHaulMath</c> guard).</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
