using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Is the thing in front of you?</b> — the forward-arc test, as one pure function, so that every
    /// consumer of "facing" in this project asks the same question and gets the same answer.
    ///
    /// <para><b>Why it was extracted.</b> <see cref="InteractResolver"/> has carried this test since the
    /// M2-39 seam landed, inline in its scan loop. The plain-text-UI consolidation gives it a second
    /// caller — <c>WorldInteractor</c>, which picks conversation partners by radius and now has to pick
    /// the one you are LOOKING at — and a second hand-written copy of an angle test is exactly the kind
    /// of quietly-diverging duplicate the deck sidecars were built to stop. One rule, one file, one set
    /// of tests.</para>
    ///
    /// <para><b>The cosine is precomputed by the CALLER, on purpose.</b> Both callers run this once per
    /// candidate inside a per-frame scan, and <see cref="Mathf.Cos"/> per candidate would be a rule-7
    /// regression on a hot path for no gain: the arc is a per-frame constant, not a per-candidate one. So
    /// the API is deliberately two calls — <see cref="CosHalfArc"/> once, outside the loop, and
    /// <see cref="Contains"/> per candidate. It is allocation-free, branch-cheap, and it does exactly one
    /// <see cref="Mathf.Sqrt"/> per candidate.</para>
    ///
    /// <para><b>Pure and total</b> (rule 5): no components, no scene, no time, no randomness. Which is
    /// what makes the facing rule provable headless in EditMode — the only place an agent with no editor
    /// can prove it at all.</para>
    /// </summary>
    public static class InteractArc
    {
        /// <summary>
        /// Below this squared distance the actor is standing ON the thing, and the direction to it is
        /// numerically meaningless (0.1 mm). <b>Facing can never exclude something you are standing on</b>
        /// — otherwise a candidate directly underfoot would flicker in and out of reach as floating-point
        /// noise swung its bearing around the compass.
        /// </summary>
        public const float SameSpotSqrMeters = 1e-8f;

        /// <summary>
        /// The half-arc as a cosine — computed ONCE per frame by the caller and handed to
        /// <see cref="Contains"/> for each candidate.
        ///
        /// <para><paramref name="arcDegrees"/> is the FULL angle, centred on the facing, so 180 means
        /// "the half-plane you are looking at" (±90°). It is clamped to [0, 360]: at 0 nothing but a
        /// dead-ahead bearing passes, and at 360 the test cannot exclude anything, which is the honest
        /// reading of both extremes rather than an error.</para>
        /// </summary>
        public static float CosHalfArc(float arcDegrees)
            => Mathf.Cos(Mathf.Clamp(arcDegrees, 0f, 360f) * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        /// Does <paramref name="delta"/> (the vector FROM the actor TO the thing) lie inside the arc
        /// centred on <paramref name="unitFacing"/>?
        ///
        /// <para><b>The un-normalised angle test.</b> <c>dot(delta, facing) &gt;= cos·|delta|</c> is the
        /// usual <c>angle &lt;= halfArc</c> written so it costs one square root instead of a division and
        /// an <c>Acos</c>. It is correct for obtuse arcs too (where the cosine is negative), because
        /// <c>|delta|</c> is never negative and the inequality keeps its sense.</para>
        ///
        /// <para><b>Two things always pass</b>, and both are deliberate: a facing of exactly zero
        /// (<see cref="InteractActor.Facing"/>'s documented "unknown" — every direction is forward rather
        /// than a guessed heading), and a thing you are standing on (see
        /// <see cref="SameSpotSqrMeters"/>).</para>
        /// </summary>
        /// <param name="delta">Actor → thing, in world metres.</param>
        /// <param name="unitFacing">The actor's facing. Need not be normalised — it is normalised here if
        /// it is not — and <see cref="Vector2.zero"/> means "unknown", which passes.</param>
        /// <param name="cosHalfArc">From <see cref="CosHalfArc"/>.</param>
        public static bool Contains(Vector2 delta, Vector2 unitFacing, float cosHalfArc)
        {
            float facingSqr = unitFacing.sqrMagnitude;
            if (facingSqr <= 0f) return true;                    // facing unknown → everything is forward

            float sqr = delta.sqrMagnitude;
            if (sqr <= SameSpotSqrMeters) return true;           // you are standing on it

            // Tolerate an un-normalised facing rather than trusting the caller: the one caller that hands
            // in a raw heading vector would otherwise get a silently wrong arc, and the correction costs a
            // compare on the already-normalised path.
            if (facingSqr < 0.999999f || facingSqr > 1.000001f) unitFacing /= Mathf.Sqrt(facingSqr);

            float dot = delta.x * unitFacing.x + delta.y * unitFacing.y;
            return dot >= cosHalfArc * Mathf.Sqrt(sqr);
        }

        /// <summary>
        /// The whole test in one call, for a caller with a single candidate (or a test) where the
        /// per-candidate cosine does not matter. <b>Do not use this inside a scan loop</b> — that is what
        /// the two-call form above exists for.
        /// </summary>
        public static bool Contains(Vector2 fromPosition, Vector2 unitFacing, Vector2 targetPosition,
                                    float arcDegrees)
            => Contains(targetPosition - fromPosition, unitFacing, CosHalfArc(arcDegrees));
    }
}
