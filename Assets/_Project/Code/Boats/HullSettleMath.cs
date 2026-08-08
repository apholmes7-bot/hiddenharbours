using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where a hull SETTLES — the design waterline, honoured exactly</b> (owner playtest
    /// 2026-08-07: <i>"the hulls of most boats do not tend to stay at the waterline level … generally
    /// they should level out at the boats water line"</i>). Pure, stateless, allocation-free and
    /// engine-light (Mathf only) — the same discipline as <see cref="BoatWaveMotionMath"/> and
    /// <see cref="StormRockMath"/>, and headless-testable for the same reason.
    ///
    /// <para><b>The bounce is not the defect.</b> Heave, loft and the storm's free-fall detachment
    /// (ADR 0018 B2.5) are wanted and untouched — the owner said so in the same breath. What this
    /// file fixes is the LEVEL the hull returns to: at rest, and averaged across any sea, the water
    /// must meet her planking at her own design waterline and nowhere else.</para>
    ///
    /// <para><b>The defect, in one line: a metre of sink does not draw a metre of waterline.</b>
    /// The hull's vertical ride and the sea's vertical lift are both applied as plain world +Y
    /// translations (<c>ws.y += lift</c> in the water's vertex stage; <c>HeavePixels / PxPerMetre</c>
    /// on the hull), but the two objects are COMPARED in the shared private z-buffer through the
    /// calibrated iso-depth convention, and depth and screen-y foreshorten differently. Solving the
    /// shipped z-test at the hull's root line (the law is stated in full on
    /// <c>DisplacedWaterMath.WatertightZHeaveMeters</c>; here it is taken at <c>ry = 0</c> with the
    /// honest, unclamped <c>zHeave = H</c>):</para>
    ///
    /// <code>
    ///   water covers a face at rig height r  ⟺  r·(cos²E + sinE) &lt; L·(cosE + sinE) − H·sinE − H·cosE
    ///   ⇒  the drawn waterline sits at   r_wl = (L − H) · (cosE + sinE) / (cos²E + sinE)
    /// </code>
    ///
    /// <para>L is the sea's lift under her, H is her own screen ride, E is the rig's bake elevation
    /// (40° for every boat rig in the fleet). The bracket <c>(L − H)</c> is exactly the sink the
    /// driver applied — so the water is drawn climbing <b>1.1457 rig-metres of planking for every
    /// metre the hull was sunk</b>. <c>HullMeshDef.RestingDraftMeters</c> is documented as
    /// "how deep this hull's design waterline sits above the rig origin" and was applied raw, so
    /// every mesh hull in the fleet has been drawing her waterline <b>14.57 % deeper than her own
    /// datum says</b>: +16 mm on the dory, +73 mm on the lobster boat, +231 mm on a stern trawler,
    /// +360 mm on the tanker. Constant, per-hull, and squarely a MEAN-level error — it does not
    /// average out, because it is not a wobble.</para>
    ///
    /// <para><b>The fix is to invert the projection, not to re-tune the data.</b>
    /// <see cref="AppliedSinkMeters"/> pre-divides the datum by the gain, so
    /// <c>r_wl = W</c> exactly and the number an owner types into a def is the number the sea draws
    /// on the planking. The gain is derived from the def's own <c>ElevationDeg</c> — an art fact the
    /// baker writes — so there is no magic number here and a rig baked at a different elevation is
    /// right for free (rule 6).</para>
    ///
    /// <para><b>Why the mean is the testable property.</b> The sea's lift under the hull and the
    /// hull's own ride are the SAME sample (<c>H = L − sink</c> by construction), so
    /// <c>r_wl = gain·sink</c> is a constant and its mean is trivially itself. What actually varies
    /// is which water sample shares a pixel with the planking — a metre or two ahead of her, where
    /// the sea is at a different point of the same wave. That differential is the waterline
    /// BREATHING up and down the planking, which is correct and wanted; it is zero-mean over the
    /// field, so the mean waterline is the settle level and the settle level is the datum. That is
    /// the invariant <c>HullWaterlineSettleTests</c> measures over a deterministic sea.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Every function here is a pure function of its arguments —
    /// no clock, no RNG, no state. The sea it is composed with is already deterministic from
    /// <c>(worldSeed, gameTime)</c>, so the settle level is too.</para>
    /// </summary>
    public static class HullSettleMath
    {
        /// <summary>
        /// The rig bake elevation to assume when a def carries none (0 or an out-of-range value).
        /// 40° is the fleet's, and every boat rig's, bake elevation — but this is a GUARD against a
        /// zeroed field, not a tunable: a def that carries its own <c>ElevationDeg</c> always wins.
        /// </summary>
        public const float DefaultBakeElevationDegrees = 40f;

        /// <summary>
        /// <b>How much drawn waterline one metre of sink buys</b> — <c>(cosE + sinE)/(cos²E + sinE)</c>,
        /// the projection gain solved out of the shared z-buffer's own law (see the class doc).
        /// 1.14574 at the fleet's 40° bake.
        ///
        /// <para>Well-conditioned across the whole legal range: the denominator
        /// <c>cos²E + sinE</c> never drops below 1 on [0°, 90°] (it is exactly 1 at both ends and
        /// peaks at 1.25 around 30°), so there is no singularity to guard, and the gain returns
        /// smoothly to exactly 1 at a degenerate side-on (0°) or plan (90°) bake. The gain itself
        /// peaks at ≈1.2257 near 63°. An elevation outside the range is a broken def, not a pose,
        /// and falls back to <see cref="DefaultBakeElevationDegrees"/>.</para>
        /// </summary>
        public static float IsoWaterlineGain(float bakeElevationDegrees)
        {
            float e = SaneElevationDegrees(bakeElevationDegrees) * Mathf.Deg2Rad;
            float c = Mathf.Cos(e);
            float s = Mathf.Sin(e);
            return (c + s) / (c * c + s);
        }

        /// <summary>
        /// <b>The sink to apply so the sea draws the waterline exactly at
        /// <paramref name="designWaterlineMeters"/></b> — the datum divided by
        /// <see cref="IsoWaterlineGain"/>. 0.8728 × the datum at 40°.
        ///
        /// <para>A hull with no waterline (0 — an unset def, or a sprite whose pivot already IS her
        /// waterline) sinks by exactly 0, which is bit-identical to the pre-fix path: the A/B
        /// contract survives the correction, because the correction is a SCALE and scaling zero is
        /// zero.</para>
        /// </summary>
        public static float AppliedSinkMeters(float designWaterlineMeters, float bakeElevationDegrees)
        {
            float w = Mathf.Max(0f, designWaterlineMeters);
            if (w <= 0f) return 0f;               // exactly 0, never 0/gain's rounding
            return w / IsoWaterlineGain(bakeElevationDegrees);
        }

        /// <summary>
        /// <b>The level this hull settles at</b> (metres of screen-vertical ride): the sea's
        /// displaced lift under her, less the sink her design waterline demands.
        ///
        /// <para><b>One law, two application sites — and exactly one sink per hull.</b>
        /// <see cref="MeshHullDriver"/> applies it as it folds the ride into the renderer's
        /// heave-pixels channel; <see cref="BoatWaveMotion"/> applies it on the sprite paths, where
        /// the ride is a transform write instead. The split is deliberate and load-bearing: a mesh
        /// hull with no <see cref="BoatWaveMotion"/> at all (a bare rig, an ambient decor boat) must
        /// still sit at her waterline, so the mesh sink belongs to the driver — the property
        /// <c>SharedHeaveTests</c> pins. ⚠️ Which is also why
        /// <see cref="IBoatHullPresenter.SetDisplacedHeaveMeters"/> carries the SEA'S LIFT and not a
        /// settled ride: sinking it again on the way in would sink a mesh hull twice.</para>
        ///
        /// <para>Riding the sea's own lift is what keeps her ON the water (ADR 0023 §(2) — the ONE
        /// shared displaced-height rule, never a per-consumer copy); subtracting the sink is what
        /// puts her AT her waterline. The sea's lift is unbounded by design (envelope ×
        /// exaggeration), so nothing is clamped here — a crest that lifts her is the owner's wanted
        /// heave, and the storm filter above this is what gives it weight.</para>
        /// </summary>
        public static float SettleRideMeters(float surfaceLiftMeters, float designWaterlineMeters,
                                             float bakeElevationDegrees)
            => surfaceLiftMeters - AppliedSinkMeters(designWaterlineMeters, bakeElevationDegrees);

        /// <summary>
        /// <b>The inverse: what the sea actually DRAWS on the planking</b> — the height above the
        /// keel (rig metres) that the water covers, given the sea's lift at the pixel and the hull's
        /// own ride. This is the shipped z-test law solved for r at the root line, and it is what
        /// the tests measure: feed it a real sea and a real hull pose and it answers the question
        /// the owner asked with his eyes.
        ///
        /// <para>Negative means the water is BELOW her keel (she is airborne over a trough — the
        /// loft B2.5 built and the owner wants kept); the caller decides whether that is worth
        /// reporting, and this function does not hide it behind a clamp.</para>
        /// </summary>
        public static float DrawnWaterlineMeters(float surfaceLiftMeters, float hullRideMeters,
                                                 float bakeElevationDegrees)
            => (surfaceLiftMeters - hullRideMeters) * IsoWaterlineGain(bakeElevationDegrees);

        /// <summary>An elevation we are willing to build a projection from: a def's own value when
        /// it is a real pose, otherwise the fleet's bake. Guards a zero-filled or corrupted field
        /// (the <c>GameConfig</c> zero-fill class of defect) rather than silently drawing a hull
        /// through a degenerate projection.</summary>
        private static float SaneElevationDegrees(float bakeElevationDegrees)
            => bakeElevationDegrees > 0f && bakeElevationDegrees <= 90f
                ? bakeElevationDegrees
                : DefaultBakeElevationDegrees;
    }
}
