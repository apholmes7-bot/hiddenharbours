using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The break line, inverted once per tick (ADR 0040, PR 2).</b> These pin the contour solve and
    /// — the part that matters — <b>MEASURE the approximation it carries</b> rather than asserting it is
    /// small.
    ///
    /// <para>The renderer cannot afford PR 1's forward criterion: it costs a <c>tanh</c>, two
    /// <c>pow</c>s, a <c>sinh</c> and a <c>sqrt</c>, and the whitewater march needs it sixteen times per
    /// pixel. So the criterion is inverted on the sim tick into a break DEPTH, and the per-pixel
    /// question becomes a depth compare. Two things have to be true for that to be honest, and each has
    /// a test here:</para>
    /// <list type="number">
    /// <item><description>The inverted contour must put the break line where the forward criterion puts
    /// it — <b>exactly</b>, since it is the same equation solved the other way round.</description></item>
    /// <item><description>The piecewise-in-envelope interpolation that avoids re-solving per pixel must
    /// stay close to the exact per-envelope solve, and <b>how close is a measured number</b>, not a
    /// hope.</description></item>
    /// </list>
    /// </summary>
    public class BreakerContourTests
    {
        private const float G = 9.81f;

        private sealed class SandyShoal : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 0.04f * worldPos.x - 3f;
        }

        private static WaveTrain Swell(float amplitude = 0.5f, float wavelength = 18f)
            => new WaveTrain(Vector2.right, wavelength, amplitude, 0f, G);

        // A sweep wide enough to cover every sea this game makes: short harbour chop through long
        // offshore swell, a ripple through a storm wave.
        private static readonly float[] Wavelengths = { 6f, 8f, 12f, 18f, 25f, 40f };
        private static readonly float[] Amplitudes = { 0.05f, 0.1f, 0.2f, 0.5f, 0.8f, 1.2f, 2f, 3f };

        // =========================================================================================
        //  The inversion is the same equation, solved the other way
        // =========================================================================================

        [Test]
        public void TheSolvedBreakDepth_IsWhereTheForwardCriterionActuallyBreaks()
        {
            var settings = BreakerSettings.Default;
            foreach (float lambda in Wavelengths)
            foreach (float amplitude in Amplitudes)
            {
                var train = Swell(amplitude, lambda);
                float d = BreakerMath.SolveBreakDepth(in train, 1f, settings.BreakerIndex, 1f);
                if (d <= 0f) continue;                       // too small to break: a real answer

                // The forward criterion, evaluated a hair either side of the solved depth.
                float justInside = BreakerMath.BreakRatioAtDepth(in train, d * 0.98f, 1f, settings.BreakerIndex);
                float justOutside = BreakerMath.BreakRatioAtDepth(in train, d * 1.02f, 1f, settings.BreakerIndex);

                Assert.Greater(justInside, 1f, $"shallower than the solved depth must be breaking (L0={lambda}, A={amplitude})");
                Assert.Less(justOutside, 1f, $"deeper than it must not be (L0={lambda}, A={amplitude})");
            }
        }

        [Test]
        public void TheRatio_IsStrictlyDecreasingInDepth_WhichIsWhatMakesTheInversionSingleValued()
        {
            // The whole method rests on there being exactly ONE crossing. If the ratio were not
            // monotone the bisection would find an arbitrary one of several and the surf line would
            // jump between them as the tide moved.
            var settings = BreakerSettings.Default;
            foreach (float lambda in Wavelengths)
            foreach (float amplitude in new[] { 0.2f, 0.5f, 1.2f, 3f })
            {
                var train = Swell(amplitude, lambda);
                float previous = float.MaxValue;
                for (float d = 0.05f; d < 40f; d *= 1.15f)
                {
                    float r = BreakerMath.BreakRatioAtDepth(in train, d, 1f, settings.BreakerIndex);
                    Assert.Less(r, previous, $"ratio must fall with depth (L0={lambda}, A={amplitude}, d={d})");
                    previous = r;
                }
            }
        }

        [Test]
        public void TheOuterGateEdge_IsAlwaysDeeperThanTheBreakItself()
        {
            var settings = BreakerSettings.Default;
            foreach (float lambda in Wavelengths)
            foreach (float amplitude in Amplitudes)
            {
                var contour = BreakerMath.ContourFor(Swell(amplitude, lambda), 0.25f, in settings);
                if (!contour.Breaks) continue;
                for (int i = 0; i < 3; i++)
                    Assert.GreaterOrEqual(contour.OuterDepths[i], contour.BreakDepths[i],
                        $"the surf must start coming in BEFORE it breaks (L0={lambda}, A={amplitude}, anchor {i})");
            }
        }

        // =========================================================================================
        //  The approximation, MEASURED
        // =========================================================================================

        [Test]
        public void TheEnvelopeInterpolation_StaysWithinItsMeasuredError_OfTheExactSolve()
        {
            // ⭐ Measured 2026-08-28 over wavelengths 6-40 m x amplitudes 0.05-3.0 m x every envelope
            // from the lee floor to 1: WORST 2.77% of the break depth, at the shipped lee floor 0.25.
            //
            // Two rejected alternatives, both measured rather than reasoned about:
            //   - a two-point lerp (anchors at 1 and the lee floor only): worst 5.28%
            //   - the closed form dBreak(e) = dBreak(1) * e^0.8, which follows from the shallow-water
            //     limit and reads entirely plausible: worst 38%. Big waves break in INTERMEDIATE
            //     depth, not the shallow limit, so the exponent is not universal. That one is the
            //     reason this is a measurement and not an argument.
            // For scale, Fenton & McKee's own shoaling error is ~1.7%.
            const float leeFloor = 0.25f;
            var settings = BreakerSettings.Default;
            float worst = 0f;
            string worstAt = "none";

            foreach (float lambda in Wavelengths)
            foreach (float amplitude in Amplitudes)
            {
                var train = Swell(amplitude, lambda);
                var contour = BreakerMath.ContourFor(in train, leeFloor, in settings);
                if (!contour.Breaks) continue;

                for (int i = 0; i <= 40; i++)
                {
                    float envelope = leeFloor + (1f - leeFloor) * (i / 40f);
                    float exact = BreakerMath.SolveBreakDepth(in train, envelope, settings.BreakerIndex, 1f);
                    if (exact <= 0f) continue;

                    float model = BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, envelope);
                    float error = Mathf.Abs(model - exact) / exact;
                    if (error > worst)
                    {
                        worst = error;
                        worstAt = $"L0={lambda}, A={amplitude}, env={envelope:0.000}";
                    }
                }
            }

            Assert.Less(worst, 0.035f,
                $"the interpolation must stay near the exact solve — worst {worst:P2} at {worstAt}");
            Assert.Greater(worst, 0.001f,
                "and it must actually BE an approximation — a zero here means the sweep stopped measuring");
        }

        [Test]
        public void AtTheThreeAnchors_TheInterpolationIsTheExactSolve()
        {
            // The anchors are solved, not interpolated, so they must be exact there — the property that
            // bounds the error above to the space between them.
            const float leeFloor = 0.25f;
            var settings = BreakerSettings.Default;
            var train = Swell();
            var contour = BreakerMath.ContourFor(in train, leeFloor, in settings);
            float mid = BreakerMath.MidEnvelopeFor(leeFloor);

            foreach (var (envelope, expected) in new[]
            {
                (1f, contour.BreakDepths.x), (mid, contour.BreakDepths.y), (leeFloor, contour.BreakDepths.z),
            })
            {
                Assert.AreEqual(expected,
                    BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, envelope), 1e-4f,
                    $"anchor at envelope {envelope} must be exact");
            }
        }

        [Test]
        public void WithFetchOff_TheContourCollapsesToOneAnchor_AndTheInterpolationIsANoOp()
        {
            var settings = BreakerSettings.Default;
            var contour = BreakerMath.ContourFor(Swell(), 1f, in settings);

            Assert.AreEqual(contour.BreakDepths.x, contour.BreakDepths.y, 1e-5f, "all three anchors coincide");
            Assert.AreEqual(contour.BreakDepths.x, contour.BreakDepths.z, 1e-5f, "all three anchors coincide");
            foreach (float envelope in new[] { 0f, 0.3f, 0.75f, 1f })
                Assert.AreEqual(contour.BreakDepths.x,
                    BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, envelope), 1e-5f,
                    "with fetch off the envelope cannot move the break line at all");
        }

        // =========================================================================================
        //  The cheap gate agrees with the physical one where it must
        // =========================================================================================

        [Test]
        public void TheCheapGate_AgreesWithThePhysicalOne_AtBothEdgesOfTheBand()
        {
            // The two gates are the same criterion: one Hermite in ratio-space, one in depth-space.
            // They MUST agree at the edges (that is where the criterion is defined) and are free to
            // differ inside the band, which is a smoothing choice. Both facts are pinned so nobody
            // later "reconciles" them by moving a break line.
            var settings = BreakerSettings.Default;
            var train = Swell();
            var contour = BreakerMath.ContourFor(in train, 1f, in settings);
            Assert.IsTrue(contour.Breaks);

            float breakDepth = contour.BreakDepths.x;
            float outerDepth = contour.OuterDepths.x;

            Assert.AreEqual(1f, BreakerMath.Breaking01FromContour(breakDepth * 0.9f, in contour, 1f), 1e-3f,
                            "well inside the break depth: fully broken, both ways");
            Assert.AreEqual(0f, BreakerMath.Breaking01FromContour(outerDepth * 1.1f, in contour, 1f), 1e-3f,
                            "well outside the gate: not breaking, both ways");

            float physicalInside = BreakerMath.Breaking01(
                BreakerMath.ShoaledHeightAt(in train, breakDepth * 0.9f, 1f), breakDepth * 0.9f, in settings);
            float physicalOutside = BreakerMath.Breaking01(
                BreakerMath.ShoaledHeightAt(in train, outerDepth * 1.1f, 1f), outerDepth * 1.1f, in settings);
            Assert.AreEqual(1f, physicalInside, 1e-3f, "the physical gate agrees inside");
            Assert.AreEqual(0f, physicalOutside, 1e-3f, "the physical gate agrees outside");
        }

        [Test]
        public void TheTwoGates_DifferOnlyInsideTheBand_AndByAMeasuredAmount()
        {
            // Stated as a number so the difference is a known, bounded smoothing choice rather than a
            // surprise someone finds while chasing a seam.
            var settings = BreakerSettings.Default;
            var train = Swell();
            var contour = BreakerMath.ContourFor(in train, 1f, in settings);
            float worst = 0f;

            for (float d = contour.BreakDepths.x * 0.5f; d <= contour.OuterDepths.x * 1.5f; d += 0.01f)
            {
                float cheap = BreakerMath.Breaking01FromContour(d, in contour, 1f);
                float physical = BreakerMath.Breaking01(BreakerMath.ShoaledHeightAt(in train, d, 1f), d, in settings);
                worst = Mathf.Max(worst, Mathf.Abs(cheap - physical));
            }

            Assert.Less(worst, 0.25f, "the two smoothings must stay recognisably the same gate");
        }

        // =========================================================================================
        //  The march off the contour is the march from PR 1
        // =========================================================================================

        [Test]
        public void TheContourMarch_ReproducesTheForwardMarch_AlongTheSameHeading()
        {
            // MetersSinceBreakAlong is MetersSinceBreak with a cheaper gate and a supplied heading.
            // Handed the train's own direction and the same sea, the two must agree closely — they are
            // the same march, and the only difference is the gate's in-band shape.
            var settings = BreakerSettings.Default;
            var terrain = new SandyShoal();
            var train = Swell();
            var contour = BreakerMath.ContourFor(in train, 1f, in settings);

            int compared = 0;
            for (float x = 38f; x < 74f; x += 0.5f)
            {
                var pos = new Vector2(x, 0f);
                float forward = BreakerMath.MetersSinceBreak(pos, in train, 0f, terrain, 1f, in settings);
                float contourMarch = BreakerMath.MetersSinceBreakAlong(pos, train.Direction, 0f, terrain,
                                                                       in contour, 1f, in settings);
                Assert.AreEqual(forward, contourMarch, 1.5f,
                    $"the two marches must agree within a fraction of a step at x={x}");
                compared++;
            }
            Assert.Greater(compared, 50, "the comparison must actually cover the surf zone");
        }

        [Test]
        public void TheHeadingIsAParameter_SoTheRendererCanMarchShoreNormal()
        {
            // The renderer marches along the seabed gradient (a shoaling wave refracts toward
            // shore-normal), not along the train's deep-water heading. That is only legitimate if the
            // heading is genuinely an input — pinned here so nobody "simplifies" it back to the train.
            var settings = BreakerSettings.Default;
            var terrain = new SandyShoal();          // rises toward +X, so shore-normal IS +X here
            var train = new WaveTrain(new Vector2(0.6f, 0.8f), 18f, 0.5f, 0f, G);   // arriving obliquely
            var contour = BreakerMath.ContourFor(in train, 1f, in settings);
            var pos = new Vector2(60f, 0f);

            float alongTrain = BreakerMath.MetersSinceBreakAlong(pos, train.Direction, 0f, terrain,
                                                                 in contour, 1f, in settings);
            float alongShoreNormal = BreakerMath.MetersSinceBreakAlong(pos, Vector2.right, 0f, terrain,
                                                                       in contour, 1f, in settings);

            Assert.AreNotEqual(alongTrain, alongShoreNormal,
                "an oblique swell and the shore normal must give different ages — the heading is real");
            Assert.Greater(alongShoreNormal, 0f, "and marching shore-normal must find the surf zone");
        }

        // =========================================================================================
        //  Sacred cases and determinism
        // =========================================================================================

        [Test]
        public void GlassAndAStaleSettingsStruct_BothBreakNowhere()
        {
            var settings = BreakerSettings.Default;
            var glass = new WaveTrain(Vector2.right, 18f, 0f, 0f, G);

            Assert.IsFalse(BreakerMath.ContourFor(in glass, 1f, in settings).Breaks, "glass breaks nowhere");
            Assert.IsFalse(BreakerMath.ContourFor(Swell(), 1f, default(BreakerSettings)).Breaks,
                           "a zeroed settings struct is inert");
            Assert.AreEqual(0f, BreakerMath.Breaking01FromContour(1f, BreakerContour.None, 1f),
                            "the None contour gates nothing");
            Assert.AreEqual(0f, BreakerMath.MetersSinceBreakAlong(Vector2.zero, Vector2.right, 0f,
                                                                 new SandyShoal(), BreakerContour.None, 1f, in settings),
                            "and marches nothing");
        }

        [Test]
        public void TheSolve_IsDeterministic_AndBounded()
        {
            var settings = BreakerSettings.Default;
            var seen = new List<float>();
            foreach (float lambda in Wavelengths)
            foreach (float amplitude in Amplitudes)
            {
                var train = Swell(amplitude, lambda);
                var a = BreakerMath.ContourFor(in train, 0.25f, in settings);
                var b = BreakerMath.ContourFor(in train, 0.25f, in settings);
                Assert.AreEqual(a.BreakDepths, b.BreakDepths, "the solve is bit-stable");
                Assert.AreEqual(a.OuterDepths, b.OuterDepths, "the solve is bit-stable");

                seen.Clear();
                seen.AddRange(new[] { a.BreakDepths.x, a.BreakDepths.y, a.BreakDepths.z,
                                      a.OuterDepths.x, a.OuterDepths.y, a.OuterDepths.z });
                foreach (float v in seen)
                {
                    Assert.IsFalse(float.IsNaN(v), "no depth may be NaN");
                    Assert.That(v, Is.InRange(0f, BreakerContour.MaxSolveDepthMeters),
                                "every depth stays inside the solve bracket");
                }
            }
        }

        [Test]
        public void ALeeShoresSmallerWave_CarriesFurtherInBeforeItBreaks()
        {
            // The reason the contour carries three anchors at all: with the shipped fetch tuning a deep
            // lee runs near 0.4x amplitude, which roughly halves the break depth. That is a visible
            // shift of the surf line, not a rounding error.
            var settings = BreakerSettings.Default;
            var contour = BreakerMath.ContourFor(Swell(), 0.25f, in settings);
            Assert.IsTrue(contour.Breaks);

            float exposed = BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, 1f);
            float sheltered = BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, 0.4f);

            Assert.Less(sheltered, exposed, "a sheltered shore breaks in shallower water — further in");
            Assert.Greater(sheltered, 0f, "but it still breaks");
        }
    }
}
