using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the water shader's CONVERGENCE (Jacobian) foam gate
    /// (ADR 0027 #3 — <c>ConvergenceGate</c> in <c>HiddenHarboursWater.shader</c>; change one,
    /// change BOTH in the same PR).
    ///
    /// <para><b>Why.</b> Foam today is tall-wave only (<c>_FoamCrestGate</c> + the whitecap
    /// lifecycle), so crossing trains never foam at their intersections. Real foam also spawns where
    /// the surface <b>pinches</b>: surface water drifts toward crests (the Gerstner horizontal
    /// displacement), and where that drift field COMPRESSES — its Jacobian determinant dropping
    /// below 1, negative when the surface folds — the sea whitens. That convergence term is what
    /// produces a confused sea at train intersections.</para>
    ///
    /// <para><b>The model.</b> Approximate the horizontal drift as <c>D(x) = pinch · (drift toward
    /// crests)</c>, whose gradient is <c>pinch</c> × the height field's second derivatives. For
    /// <c>h = a·cos(kx)</c> the Gerstner displacement gives <c>∂D/∂x = (Q/k)·h_xx</c> — so
    /// <paramref name="pinch"/> (metres) plays the physical role of <c>Q/k</c>, "how far surface
    /// water is drawn toward a crest". The Jacobian of <c>x → x + D(x)</c> is then
    /// <c>J = (1 + q·h_xx)(1 + q·h_yy) − (q·h_xy)²</c>. Curvature is NEGATIVE at a crest, so a
    /// crest CONVERGES (J &lt; 1); the cross term is what two crossing trains write. The gate is
    /// <c>saturate(1 − J)</c>: 0 on a flat sea, 0 at zero pinch, positive where the surface
    /// pinches, growing as it folds.</para>
    ///
    /// <para>The second derivatives are produced in-shader by central differences of
    /// <c>WaveFieldSample()</c>'s ANALYTIC slope (4 taps at <c>_FoamConvergenceStep</c>); this twin
    /// takes them as plain floats so the Jacobian shape is locked headless. Note <c>h_xy</c> enters
    /// SQUARED — its sign is structurally irrelevant (a sign flip there is inert by construction;
    /// the sabotage tests target the q sign and the cross term instead). Visual-only downstream:
    /// the gate feeds the existing thresholded/banded cap field and never touches depth/clip()/
    /// <c>_WaterLevel</c>/the sim (P1 integrity, CLAUDE.md rule 5).</para>
    /// </summary>
    public static class WaterFoam
    {
        /// <summary>
        /// The convergence gate: <c>saturate(1 − J)</c> of the pinch mapping described on the class.
        /// 0 on a flat sea (all second derivatives 0), 0 at <paramref name="pinch"/> ≤ 0, positive
        /// where the surface pinches (crests, and strongest where crossing trains superpose).
        /// </summary>
        /// <param name="hxx">∂²h/∂x² of the wave height field (1/m). Negative at a crest.</param>
        /// <param name="hyy">∂²h/∂y² of the wave height field (1/m).</param>
        /// <param name="hxy">∂²h/∂x∂y — the cross term two crossing trains write (1/m).</param>
        /// <param name="pinch">The drift amount toward crests, metres (≈ Gerstner Q/k;
        /// <c>_FoamConvergencePinch</c>). Clamped ≥ 0.</param>
        public static float Convergence(float hxx, float hyy, float hxy, float pinch)
        {
            float q = Mathf.Max(pinch, 0f);
            float jxx = 1f + q * hxx;
            float jyy = 1f + q * hyy;
            float jxy = q * hxy;
            float j = jxx * jyy - jxy * jxy;
            return Mathf.Clamp01(1f - j);
        }
    }
}
