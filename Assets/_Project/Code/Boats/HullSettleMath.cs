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
    ///   water covers a face at rig height r  ⟺  r / sinE  &lt;  L·(cosE + sinE) − H·sinE − H·cosE
    ///   ⇒  the drawn waterline sits at   r_wl = (L − H) · sinE · (cosE + sinE)
    /// </code>
    ///
    /// <para>L is the sea's lift under her, H is her own screen ride, E is the rig's bake elevation
    /// (40° for every boat rig in the fleet). The bracket <c>(L − H)</c> is exactly the sink the
    /// driver applied — so the water is drawn climbing <b>0.9056 rig-metres of planking for every
    /// metre the hull was sunk</b>. <c>HullMeshDef.RestingDraftMeters</c> is documented as
    /// "how deep this hull's design waterline sits above the rig origin" and was applied raw, so
    /// every mesh hull in the fleet drew her waterline at the wrong depth until the gain was
    /// inverted out — the defect this file exists to close.</para>
    ///
    /// <para><b>⚠️ RE-DERIVED UNDER ADR 0033, and the number moved.</b> The law's first line used to
    /// read <c>r·(cos²E + sinE) &lt; …</c>, giving a gain of <c>(cos+sin)/(cos²+sin) = 1.1457</c>.
    /// That coefficient was never a fact about flotation: it was the hull's depth ramp being
    /// 1/sin(E) too steep (#491), the same unit error that put a north-sailing stern 1.64 m in front
    /// of the sea. ADR 0033's y→z shear lands the height axis on the true iso relation
    /// <c>1/sin(E)</c> — the derivation is written out on
    /// <c>DisplacedWaterMath.WatertightZHeaveMeters</c>, of which this is the <c>ry = 0</c> case —
    /// so the gain becomes <c>sinE·(cosE + sinE) = 0.9056</c> at 40°.</para>
    ///
    /// <para><b>What that does and does not change for the fleet.</b> The gain is INVERTED by
    /// <see cref="AppliedSinkMeters"/>, so the drawn waterline is <c>W</c> exactly, before and
    /// after — no <c>RestingDraftMeters</c> in any def needs re-typing, and that identity is the
    /// thing to check first when reading the fleet table. What moves is the applied sink, from
    /// <c>0.8728·W</c> to <c>1.1043·W</c> (×1.2652): every mesh hull now sits <c>0.2315·W</c> metres
    /// lower on screen — 25 mm on the dory, 116 mm on the lobster boat, 572 mm on the tanker — which
    /// is the visible half of this change and is why the whole fleet's flotation was re-rendered
    /// rather than re-tuned.</para>
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
        /// The shallowest bake elevation the shared depth contract is willing to draw through — a
        /// GUARD, not a tunable, and new with ADR 0033 (see <see cref="IsoWaterlineGain"/> for why
        /// the old law needed none).
        ///
        /// <para>Derived from where the gain's <c>sinE</c> stops being a scale and starts being a
        /// singularity: at 10° the gain is 0.201, so a hull sinks ~5× her datum — already a broken
        /// picture, but bounded; at 5° it is 12×, at 1° it is 57× and climbing hyperbolically. No
        /// rig in the fleet bakes anywhere near it (every one is 40°), so nothing shipped moves —
        /// this only decides what a CORRUPTED elevation field does, and falling back to the fleet's
        /// bake is strictly better than drawing a boat fifty draughts under the sea.</para>
        /// </summary>
        public const float MinBakeElevationDegrees = 10f;

        /// <summary>
        /// <b>How much drawn waterline one metre of sink buys</b> — <c>sinE·(cosE + sinE)</c>, the
        /// projection gain solved out of the shared z-buffer's own law (see the class doc).
        /// 0.90558 at the fleet's 40° bake.
        ///
        /// <para><b>⚠️ It is no longer well-conditioned, and that is why
        /// <see cref="MinBakeElevationDegrees"/> exists.</b> The old gain
        /// <c>(cos+sin)/(cos²+sin)</c> sat on a denominator that never dropped below 1, so it
        /// returned smoothly to exactly 1 at both degenerate bakes and needed no guard. This one
        /// carries a bare <c>sinE</c> — it is 0 at a side-on (0°) bake and 1 at a plan (90°) one,
        /// peaking at ≈1.2071 near 67.5° — and <see cref="AppliedSinkMeters"/> DIVIDES by it. At 1°
        /// that would sink a hull 57× her own draft, clean out of sight under the sea. The vanishing
        /// is not this function's alone: ADR 0033's whole depth contract carries a <c>1/sin</c> (the
        /// shear itself is <c>cos(1−sin)/sin</c>), so a near-side-on bake is degenerate for the
        /// render, not merely awkward here.</para>
        ///
        /// <para>An elevation outside <c>[<see cref="MinBakeElevationDegrees"/>, 90°]</c> is a
        /// broken def, not a pose, and falls back to <see cref="DefaultBakeElevationDegrees"/>.</para>
        /// </summary>
        public static float IsoWaterlineGain(float bakeElevationDegrees)
        {
            float e = SaneElevationDegrees(bakeElevationDegrees) * Mathf.Deg2Rad;
            float c = Mathf.Cos(e);
            float s = Mathf.Sin(e);
            return s * (c + s);
        }

        /// <summary>
        /// <b>The sink to apply so the sea draws the waterline exactly at
        /// <paramref name="designWaterlineMeters"/></b> — the datum divided by
        /// <see cref="IsoWaterlineGain"/>. 1.1043 × the datum at 40° (0.8728 × it before ADR 0033 —
        /// the drawn waterline is <c>W</c> either way; what moved is how deep she must sit to draw it).
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
            => bakeElevationDegrees >= MinBakeElevationDegrees && bakeElevationDegrees <= 90f
                ? bakeElevationDegrees
                : DefaultBakeElevationDegrees;
    }
}
