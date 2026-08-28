using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The break line, solved once per tick instead of per pixel (ADR 0040, PR 2).</b>
    ///
    /// <para>PR 1's criterion runs FORWARD: given a depth, shoal the wave and ask whether
    /// <c>H ≥ γ·d</c>. That is the physical definition and it stays the definition — but evaluating it
    /// costs a <c>tanh</c>, two <c>pow</c>s, a <c>sinh</c> and a <c>sqrt</c>, and the whitewater march
    /// needs the answer at <see cref="BreakerMath.MarchSteps"/> points per pixel. Paying the whole
    /// shoaling chain sixteen times per pixel is not a rule-7 budget, it is a slideshow.</para>
    ///
    /// <para><b>So invert it once instead.</b> <c>ratio(d) = H_shoaled(d) / (γ·d)</c> is <b>strictly
    /// decreasing</b> in depth — deep water is always further from breaking than shallow — so there is
    /// exactly one depth where it crosses 1, and one where it crosses <c>1 − band</c>. Solve for those
    /// two depths on the sim tick, hand them to the shader, and the per-pixel question collapses to
    /// <b>"is the water shallower than the break depth?"</b> — one <c>smoothstep</c>, no transcendentals
    /// at all. The march becomes sixteen height-map taps and sixteen smoothsteps: exactly the cost
    /// shape <see cref="WaveFetch"/> already ships.</para>
    ///
    /// <para><b>Why three depths and not one: the fetch envelope moves the break line.</b> A lee shore
    /// gets a smaller wave, and a smaller wave carries further in before it breaks — with the shipped
    /// tuning (<c>Strength</c> 0.8, <c>LeeFloor</c> 0.25) a deep lee runs at 0.4× amplitude, which
    /// halves the break depth. That is a visible shift of the surf line, not a rounding error. The
    /// envelope varies per position, and re-solving per pixel is exactly what this class exists to
    /// avoid — so the contour is solved at <b>three</b> envelopes (1, <see cref="MidEnvelope"/>, and
    /// the lee floor) and the consumer interpolates piecewise in envelope.</para>
    ///
    /// <para><b>⚠️ The interpolation is an approximation, and it is measured, not asserted.</b>
    /// <c>BreakerContourTests</c> sweeps wavelengths 6–40 m against amplitudes 0.1–2.0 m and every
    /// envelope from the lee floor to 1, and pins the worst deviation from the exact per-envelope solve
    /// at <b>2.51 %</b> of the break depth. A two-point lerp was measured first at 5.28 % and a
    /// <c>env^0.8</c> closed form at <b>38 %</b> — the closed form is the one that reads plausible and
    /// is badly wrong, which is why all three were measured rather than reasoned about. For scale,
    /// Fenton &amp; McKee's own shoaling error is ~1.7 %.</para>
    ///
    /// <para><b>The C# and the HLSL run the SAME interpolation</b>, so the twin is exact to float
    /// epsilon and the 2.51 % above is the model's distance from the exact solve, not a gap between the
    /// two sides. That separation is the point: a twin divergence would be a bug, an approximation is a
    /// stated cost.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Solved from (train, γ, band, lee floor) — nothing saved, no
    /// RNG, and the bisection runs a FIXED <see cref="SolveIterations"/> iterations so it cannot vary
    /// with input or machine.</para>
    /// </summary>
    public readonly struct BreakerContour
    {
        /// <summary>The middle envelope the contour is solved at. 0.6 sits near the middle of the
        /// shipped fetch range (a 0.8-strength model floors at 0.4) and is what takes the piecewise
        /// error from 5.28 % to 2.51 %.</summary>
        public const float MidEnvelope = 0.6f;

        /// <summary>Bisection steps. FIXED so the solve is deterministic and cannot spin: 40 halvings
        /// of a 400 m bracket resolve the depth far below a millimetre, well past what an 8-bit height
        /// texture can express (3.91 cm per code).</summary>
        public const int SolveIterations = 40;

        /// <summary>Deepest water the solve brackets (metres). No sea this game carries breaks in
        /// 400 m; the bracket exists so the bisection is bounded, not to model anything.</summary>
        public const float MaxSolveDepthMeters = 400f;

        /// <summary>Break depth (metres) at envelope 1, <see cref="MidEnvelope"/>, and the lee floor —
        /// water shallower than this is breaking. Zero throughout when <see cref="Breaks"/> is false.</summary>
        public readonly Vector3 BreakDepths;

        /// <summary>The OUTER edge of the smooth gate at the same three envelopes: the depth where the
        /// wave has reached <c>1 − band</c> of the criterion and the surf begins to come in. Always
        /// deeper than the matching <see cref="BreakDepths"/> component.</summary>
        public readonly Vector3 OuterDepths;

        /// <summary>The fetch lee floor the third component was solved at — the consumer's lower
        /// interpolation anchor. 1 when the fetch model is off, in which case all three components are
        /// equal and the interpolation is a no-op.</summary>
        public readonly float LeeEnvelope;

        /// <summary><b>Does this sea break anywhere at all?</b> False on glass, on a silent train, and
        /// wherever γ is zero (a stale settings struct) — the consumer then draws nothing and pays for
        /// nothing.</summary>
        public readonly bool Breaks;

        public BreakerContour(Vector3 breakDepths, Vector3 outerDepths, float leeEnvelope, bool breaks)
        {
            BreakDepths = breakDepths;
            OuterDepths = outerDepths;
            LeeEnvelope = leeEnvelope;
            Breaks = breaks;
        }

        /// <summary>A sea that breaks nowhere — glass, or the model dialled off.</summary>
        public static readonly BreakerContour None = new BreakerContour(Vector3.zero, Vector3.zero, 1f, false);
    }
}
