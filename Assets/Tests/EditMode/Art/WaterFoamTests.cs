using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless twin tests for the ADR 0027 #3 convergence (Jacobian) foam gate —
    /// <see cref="WaterFoam.Convergence"/>, the C# mirror of the shader's <c>ConvergenceGate</c>
    /// (change one, change BOTH in the same PR). Contracts pinned:
    ///
    /// <list type="bullet">
    /// <item>a FLAT sea converges nowhere (all second derivatives 0 ⇒ 0);</item>
    /// <item>zero pinch (the passthrough axis the shader's strength-0 branch rides) ⇒ 0;</item>
    /// <item>a single train's CREST converges, its TROUGH does not (the sign convention:
    /// curvature is negative at a crest, so J &lt; 1 there);</item>
    /// <item>CROSSING crests converge MORE than either train alone — the confused-sea payoff
    /// the ADR names ("crossing trains never foam at their intersections");</item>
    /// <item>the cross term h_xy adds exactly q²·h_xy² of convergence (the determinant shape);</item>
    /// <item>a golden value pins the whole Jacobian arithmetic to 1e-5.</item>
    /// </list>
    ///
    /// Sabotage MEASURED (2026-07-28, headless runs — see the PR): flipping the pinch sign in the
    /// diagonals (1 − q·h) turns every crest into expansion — 5 of 7 fail (the reference crest's
    /// convergence 0.740 → 0.000; golden value, determinant shape, cross-term and fold pins all go
    /// red) while the two sign-independent tests stay green. Dropping the cross term (J = jxx·jyy)
    /// fails exactly 2 of 7 — the cross-term pin (expected +0.0400, got 0.0) and the golden value
    /// (0.52 vs 0.53). Targeted failures, not a collapse. (A sign flip on h_xy itself is inert BY
    /// CONSTRUCTION — it enters squared — which is why the sabotage arms target the q sign and the
    /// term's presence instead.)
    /// </summary>
    public class WaterFoamTests
    {
        // A single sharpless sine train h = a·sin(k·(d·x)) has, at a crest,
        // h_along = −a·k² (curvature along its travel axis) and 0 across it.
        static float CrestCurvature(float amplitude, float wavelength)
        {
            float k = 2f * Mathf.PI / wavelength;
            return -amplitude * k * k;
        }

        [Test]
        public void FlatSea_ConvergesNowhere()
        {
            foreach (float q in new[] { 0f, 1f, 4f, 20f })
                Assert.AreEqual(0f, WaterFoam.Convergence(0f, 0f, 0f, q),
                    $"a flat sea must not converge (pinch {q}).");
        }

        [Test]
        public void ZeroPinch_IsAnExactZero()
        {
            // The passthrough axis: at zero strength the shader never calls this, but at zero
            // PINCH the gate itself must be exactly 0 whatever the surface does.
            Assert.AreEqual(0f, WaterFoam.Convergence(-0.5f, -0.3f, 0.2f, 0f));
            Assert.AreEqual(0f, WaterFoam.Convergence(0.5f, 0.3f, -0.2f, -3f),
                "negative pinch clamps to 0 — never an inverted mapping.");
        }

        [Test]
        public void SingleTrainCrest_Converges_TroughDoesNot()
        {
            float hxx = CrestCurvature(0.3f, 8f);   // a = 0.3 m, λ = 8 m -> −0.185 1/m
            float crest = WaterFoam.Convergence(hxx, 0f, 0f, 4f);
            float trough = WaterFoam.Convergence(-hxx, 0f, 0f, 4f);
            Assert.Greater(crest, 0.5f,
                $"a 0.3 m / 8 m crest at pinch 4 should converge strongly (got {crest:F4}).");
            Assert.AreEqual(0f, trough,
                "a trough EXPANDS (J > 1) — the gate must be exactly 0 there.");
        }

        [Test]
        public void CrossingCrests_ConvergeMoreThanEitherTrainAlone()
        {
            // Two orthogonal trains superposing crests at a point: hxx from train 1, hyy from
            // train 2, no cross term at the mutual crest of axis-aligned trains.
            float h1 = CrestCurvature(0.25f, 10f);
            float h2 = CrestCurvature(0.18f, 6f);
            float both = WaterFoam.Convergence(h1, h2, 0f, 3f);
            float only1 = WaterFoam.Convergence(h1, 0f, 0f, 3f);
            float only2 = WaterFoam.Convergence(0f, h2, 0f, 3f);
            Assert.Greater(both, only1, "the intersection must out-converge train 1 alone.");
            Assert.Greater(both, only2, "the intersection must out-converge train 2 alone.");
            // The determinant shape: 1 − (1−A)(1−B) = A + B − A·B — strictly more than either,
            // slightly less than the plain sum (pinned so a naive additive model fails here).
            float a = -3f * h1;   // q·|hxx|
            float b = -3f * h2;
            Assert.AreEqual(a + b - a * b, both, 1e-5f,
                "the intersection convergence must follow the DETERMINANT, not a plain sum.");
        }

        [Test]
        public void CrossTerm_AddsExactly_QSquaredHxySquared()
        {
            // Diagonal-crossing trains write h_xy; the determinant adds q²·h_xy² of convergence.
            const float q = 2.5f, hxx = -0.1f, hyy = -0.06f, hxy = 0.08f;
            float without = WaterFoam.Convergence(hxx, hyy, 0f, q);
            float with = WaterFoam.Convergence(hxx, hyy, hxy, q);
            Assert.AreEqual(q * q * hxy * hxy, with - without, 1e-6f,
                "the cross term's contribution must be exactly q²·h_xy² (the −jxy² of the det).");
            Assert.AreEqual(with, WaterFoam.Convergence(hxx, hyy, -hxy, q),
                "h_xy enters SQUARED — its sign is structurally irrelevant (documented).");
        }

        [Test]
        public void GoldenValue_PinsTheJacobianArithmetic()
        {
            // jxx = 1 + 2·(−0.2) = 0.6; jyy = 1 + 2·(−0.1) = 0.8; jxy = 2·0.05 = 0.1
            // J = 0.6·0.8 − 0.01 = 0.47 -> conv = 0.53. Any sign/term slip lands elsewhere.
            Assert.AreEqual(0.53f, WaterFoam.Convergence(-0.2f, -0.1f, 0.05f, 2f), 1e-5f);
        }

        [Test]
        public void Output_IsAlwaysZeroToOne()
        {
            foreach (var v in new[]
            {
                WaterFoam.Convergence(-5f, -5f, 3f, 10f),    // violent folding -> clamps to 1
                WaterFoam.Convergence(5f, 5f, 0f, 10f),      // violent expansion -> clamps to 0
                WaterFoam.Convergence(-0.01f, 0f, 0f, 0.1f), // barely anything
            })
            {
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
            // A FOLDING surface is J dipping BELOW 0 (one axis inverting): jxx = −0.5, jyy = 0.4
            // ⇒ J = −0.2 ⇒ the gate saturates at 1. (With BOTH axes inverted the determinant goes
            // positive again — the −5/−5 case above — and the gate reads 0: the surface has folded
            // through itself, beyond the pinch model's validity, and the clamp keeps it sane.)
            Assert.AreEqual(1f, WaterFoam.Convergence(-0.15f, -0.06f, 0f, 10f),
                "a folding surface (J below 0) saturates the gate at 1.");
        }
    }
}
