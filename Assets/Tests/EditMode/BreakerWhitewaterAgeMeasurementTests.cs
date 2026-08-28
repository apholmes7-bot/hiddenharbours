using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The whitewater age, MEASURED — not argued (ADR 0040).</b>
    ///
    /// <para>This file exists because the living-wake lane made the same mistake twice in one lane, and
    /// the second time the author had written up the first as a lesson hours earlier. Both were caught
    /// by a measurement, never by re-reading code that read perfectly well:</para>
    /// <list type="number">
    /// <item><description><b>An age derived through a pipeline that saturates, thresholds and
    /// posterizes is not an age.</b> #665 read a wake texel's age out of its coverage; by the time the
    /// proxy saw it, the value could take three levels and 72–81% of the visible band drew at age
    /// exactly 0. Sweeping the threshold across its whole legal range only chose which flat colour the
    /// band was.</description></item>
    /// <item><description><b>A clock scaled by intensity is the same error one level down.</b> Scaling
    /// the freshness mark by the hull's vigour meant a dory's brand-new churn was born half-aged and
    /// she could never make white foam at all. The mark is a GATE, not a scale.</description></item>
    /// </list>
    ///
    /// <para>So these tests do not assert that <c>BreakerMath</c>'s age is well-designed. They MEASURE
    /// the shipped chain end to end and hold the numbers, so a retune re-runs the measurement instead
    /// of quietly measuring a world that no longer exists. The sabotage arm proves what the smooth
    /// break gate actually buys — without it the age collapses onto the march step.</para>
    /// </summary>
    public class BreakerWhitewaterAgeMeasurementTests
    {
        private const float G = 9.81f;

        /// <summary>A 1:25 sandy shoal, 3 m deep at the origin, rising toward +X. Shore at x = 75 m;
        /// at the default tuning a 1 m swell breaks near x = 42, so the surf zone is ~33 m — wide
        /// enough to exercise the whole decay and narrower than the march's 32 m reach at its
        /// inshore end.</summary>
        private sealed class SandyShoal : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 0.04f * worldPos.x - 3f;
        }

        /// <summary>An outer BAR that breaks, a deep LAGOON behind it, then a beach with its own
        /// shorebreak — the profile that separates "the age of this bore" from "all the surf I can
        /// see upwave".</summary>
        private sealed class BarLagoonBeach : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos)
            {
                float x = worldPos.x;
                if (x < 0f) return -6f;
                if (x < 8f) return -1f;          // the bar — breaks at low water
                if (x < 22f) return -5f;         // the lagoon — far too deep to break
                return -5f + 0.25f * (x - 22f);  // the beach, with its own shorebreak
            }
        }

        private static WaveTrain Swell(float amplitude = 0.5f, float wavelength = 18f)
            => new WaveTrain(Vector2.right, wavelength, amplitude, 0f, G);

        /// <summary>Walk the surf zone shoreward in 25 cm steps and record what the shipped chain
        /// actually produces at every point.</summary>
        private static (List<float> ages, List<float> energies) SweepTheSurfZone(in BreakerSettings settings)
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var ages = new List<float>();
            var energies = new List<float>();

            float breakX = float.NaN;
            for (float x = 0f; x < 120f; x += 0.05f)
            {
                if (BreakerMath.SampleAt(new Vector2(x, 0f), in train, 0f, terrain, 1f, in settings).Breaking01 >= 0.5f)
                { breakX = x; break; }
            }
            Assert.IsFalse(float.IsNaN(breakX), "the swell must break on this shoal at all");

            for (float x = breakX; ; x += 0.25f)
            {
                var pos = new Vector2(x, 0f);
                var sample = BreakerMath.SampleAt(pos, in train, 0f, terrain, 1f, in settings);
                if (sample.DepthMeters <= 0.05f) break;

                float age = BreakerMath.MetersSinceBreak(pos, in train, 0f, terrain, 1f, in settings);
                ages.Add(age);
                energies.Add(BreakerMath.WhitewaterEnergy01(age, sample.DepthMeters, G, in settings));
            }

            return (ages, energies);
        }

        private static int DistinctTo(IEnumerable<float> values, int decimals)
            => values.Select(v => (float)System.Math.Round(v, decimals)).Distinct().Count();

        private static float TopShare(IEnumerable<float> values, int decimals)
        {
            var rounded = values.Select(v => (float)System.Math.Round(v, decimals)).ToList();
            return rounded.GroupBy(v => v).Max(g => g.Count()) / (float)rounded.Count;
        }

        // =========================================================================================
        //  The measurement itself
        // =========================================================================================

        [Test]
        public void TheAge_KeepsItsResolution_AllTheWayThroughTheSurfZone()
        {
            // Measured 2026-08-27 at the shipped default tuning: 128 samples across the surf zone,
            // 128 DISTINCT ages. The age is linear in distance past the break line, and the smooth
            // break gate supplies the sub-step fraction, so it does NOT sit on the 2 m march grid.
            var settings = BreakerSettings.Default;
            var (ages, _) = SweepTheSurfZone(in settings);

            Assert.GreaterOrEqual(ages.Count, 100, "the sweep must actually cover a surf zone");
            Assert.GreaterOrEqual(DistinctTo(ages, 3), 100,
                "the age must not be quantized to the march step — this is the #665 defect's own metric");

            for (int i = 1; i < ages.Count; i++)
                Assert.GreaterOrEqual(ages[i], ages[i - 1] - 1e-4f,
                    $"the age must grow monotonically shoreward (sample {i})");

            Assert.AreEqual(0f, ages[0], 0.75f, "at the break line the water has just broken");
            Assert.Greater(ages[ages.Count - 1], 20f, "at the top of the beach the bore has run a long way");
        }

        [Test]
        public void TheEnergy_SpansItsWholeRange_WithNoValueDominating()
        {
            // The #665 metric, stated as a NUMBER so nobody has to re-derive the analysis: there, one
            // value covered 72-81% of the VISIBLE band. The analogue here is the foam that would
            // actually be drawn, so the share is measured over samples above a visibility floor.
            //
            // Measured 2026-08-27 at the default tuning: 101 visible samples, 80 distinct energies,
            // most common 4.0%. The remaining 27 samples are the inshore tail where the bore is
            // genuinely dead - binned at 1% they all read 0.00, which is 12.5% of the full sweep and
            // is dead foam being dead, not an age that lost its resolution. (The full-band figure was
            // measured for BOTH arms and does not discriminate: the sabotage arm reads 11.8% there.
            // The visible band does - 4.0% shipped against 8.3% sabotaged - and the distinct-age
            // count in the next test discriminates hardest of all.)
            const float visible = 0.02f;
            var settings = BreakerSettings.Default;
            var (_, energies) = SweepTheSurfZone(in settings);
            var drawn = energies.Where(e => e >= visible).ToList();

            Assert.Greater(energies.Max(), 0.95f, "fresh whitewater at the break must read essentially full");
            Assert.Less(energies.Min(), 0.05f, "and it must actually die before the top of the beach");
            Assert.GreaterOrEqual(drawn.Count, 60, "most of the surf zone must carry drawable foam");
            Assert.Less(TopShare(drawn, 2), 0.10f,
                "no single energy may dominate the drawn band - 72-81% at one value is what the wake defect looked like");
            Assert.GreaterOrEqual(DistinctTo(drawn, 2), 40, "the decay must be legible, not three levels");
        }

        [Test]
        public void ANearHardBreakGate_COLLAPSES_TheAge_WhichIsWhatTheSmoothGateBuys()
        {
            // ⭐ The sabotage arm. The march accumulates a product of GATES; deep inside the surf zone
            // every gate is exactly 1, so the sum would be an integer count of steps and the age would
            // sit on the 2 m grid. What supplies the fraction is the PARTIAL gate at the surf-zone
            // boundary — which exists only because the gate is a smoothstep. Narrow the band toward a
            // hard cutoff and the resolution collapses, measured, not argued.
            var shipped = BreakerSettings.Default;
            var hardCutoff = BreakerSettings.Default;
            hardCutoff.BreakBandRatio = 0.01f;

            var (shippedAges, _) = SweepTheSurfZone(in shipped);
            var (cutoffAges, _) = SweepTheSurfZone(in hardCutoff);

            int shippedDistinct = DistinctTo(shippedAges, 3);
            int cutoffDistinct = DistinctTo(cutoffAges, 3);

            // Measured 2026-08-27: 128 distinct with the shipped 0.15 band, 29 with a 0.01 band.
            Assert.Less(cutoffDistinct, shippedDistinct / 2,
                $"a near-hard gate must visibly quantize the age (shipped {shippedDistinct}, cutoff {cutoffDistinct})");
            Assert.LessOrEqual(cutoffDistinct, 60,
                "and it must land on roughly the march-step grid, which is the failure this guards");
        }

        [Test]
        public void TheBreakGate_IsNeverUsedAsAScaleOnTheAge_SoSmallSurfIsNotBornOld()
        {
            // The round-2 defect, transplanted: a dory's mark was scaled by her vigour, so her
            // brand-new churn was born half-aged and could never make white foam. Here: a small swell
            // and a big swell break in different depths and with different violence, and BOTH must
            // read essentially-fresh whitewater at their own break line.
            var settings = BreakerSettings.Default;
            var terrain = new SandyShoal();

            foreach (float amplitude in new[] { 0.15f, 0.5f, 1.4f })
            {
                var train = Swell(amplitude);
                float breakX = float.NaN;
                // Start in genuinely deep water. A 2.8 m swell breaks in 3.6 m, which on this 1:25
                // shoal is at x = -14 — searching from x = 0 would start INSIDE its own surf zone and
                // call the middle of the bore "the break line". (Caught by the first run of this test:
                // it read E = 0.50 at what it believed was fresh water.)
                for (float x = -300f; x < 120f; x += 0.05f)
                {
                    if (BreakerMath.SampleAt(new Vector2(x, 0f), in train, 0f, terrain, 1f, in settings).Breaking01 >= 0.5f)
                    { breakX = x; break; }
                }
                Assert.IsFalse(float.IsNaN(breakX), $"amplitude {amplitude} must break somewhere");

                // A metre inside the surf zone: young water, whatever the size of the day.
                var pos = new Vector2(breakX + 1f, 0f);
                var sample = BreakerMath.SampleAt(pos, in train, 0f, terrain, 1f, in settings);
                float age = BreakerMath.MetersSinceBreak(pos, in train, 0f, terrain, 1f, in settings);
                float energy = BreakerMath.WhitewaterEnergy01(age, sample.DepthMeters, G, in settings);

                Assert.Greater(energy, 0.8f,
                    $"freshly broken water must read fresh at amplitude {amplitude} — the age is a clock, not a scale");
            }
        }

        [Test]
        public void WhitewaterDoesNotCarryTheOuterBarsAge_AcrossADeepLagoon()
        {
            // The contiguity product (the WaveFetch land-shadow idiom): once the march steps out of
            // breaking water, nothing beyond it counts. A naive SUM over the same 16 steps reports
            // 4 m of age at this position, purely from the bar 32 m upwave whose foam died in the
            // lagoon long ago.
            var settings = BreakerSettings.Default;
            var terrain = new BarLagoonBeach();
            var train = Swell();

            // The bar really is breaking, and really is inside the march's reach — otherwise this
            // test would pass for the wrong reason.
            var onTheBar = BreakerMath.SampleAt(new Vector2(6f, 0f), in train, 0f, terrain, 1f, in settings);
            Assert.Greater(onTheBar.Breaking01, 0.9f, "the outer bar must actually be breaking");
            float reach = BreakerMath.MarchSteps * settings.WhitewaterStepMeters;
            Assert.LessOrEqual(36f - 6f, reach, "the bar must sit inside the march reach for this to prove anything");

            // Over the lagoon-side approach, in water far too deep to break: no bore here.
            var quiet = new Vector2(36f, 0f);
            Assert.AreEqual(0f, BreakerMath.SampleAt(quiet, in train, 0f, terrain, 1f, in settings).Breaking01, 1e-3f,
                            "this position is not itself breaking");
            Assert.AreEqual(0f, BreakerMath.MetersSinceBreak(quiet, in train, 0f, terrain, 1f, in settings), 1e-4f,
                            "and it must not inherit the outer bar's whitewater across the lagoon");
        }

        [Test]
        public void TheMarchReach_IsACap_AndItIsStatedNotHidden()
        {
            // No silent caps: the march sees MarchSteps x StepMeters upwave and saturates there. At
            // the default tuning that is 32 m, by which point a bore has lost over 99% of its energy —
            // so the cap is not reachable in a VISIBLE quantity. It is still a cap, and a surf zone
            // wider than the reach would read as uniformly old at its inshore end.
            var settings = BreakerSettings.Default;
            float reach = BreakerMath.MarchSteps * settings.WhitewaterStepMeters;
            Assert.AreEqual(32f, reach, 1e-4f, "the stated reach at the default tuning");

            // Everywhere breaking: the deepest possible surf, so the march saturates by construction.
            var everywhereBreaking = new AlwaysShallow();
            var train = Swell();
            float age = BreakerMath.MetersSinceBreak(new Vector2(50f, 0f), in train, 0f, everywhereBreaking, 1f, in settings);
            Assert.AreEqual(reach, age, 0.01f, "saturated at exactly the stated reach, never beyond");

            float energyAtTheCap = BreakerMath.WhitewaterEnergy01(reach, 0.4f, G, in settings);
            Assert.Less(energyAtTheCap, 0.01f, "and the energy is long dead by then, which is why the cap does not show");
        }

        private sealed class AlwaysShallow : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => -0.4f;   // 40 cm of water everywhere
        }

        [Test]
        public void TheWholeChain_IsDeterministic_SameInputsSameFoam()
        {
            var settings = BreakerSettings.Default;
            var terrain = new SandyShoal();
            var train = Swell();

            for (float x = 40f; x < 74f; x += 1.5f)
            {
                var pos = new Vector2(x, 0f);
                float a = BreakerMath.MetersSinceBreak(pos, in train, 0f, terrain, 1f, in settings);
                float b = BreakerMath.MetersSinceBreak(pos, in train, 0f, terrain, 1f, in settings);
                Assert.AreEqual(a, b, "the age is bit-stable");
                Assert.AreEqual(BreakerMath.WhitewaterEnergy01(a, 1f, G, in settings),
                                BreakerMath.WhitewaterEnergy01(b, 1f, G, in settings),
                                "the energy is bit-stable");
            }
        }
    }
}
