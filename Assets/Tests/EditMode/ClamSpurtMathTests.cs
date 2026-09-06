using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The clam spurt is the art director's contract, so these tests hold
    /// <see cref="ClamSpurtMath"/> to the <b>committed sidecar</b> — not to numbers retyped here.
    ///
    /// <para>The sidecar (<c>docs/art/rigs/catch-pass-2-kit/sidecars/shellfishRig2.rig.json</c>) was
    /// written by the kit's own harness straight out of <c>shellfishRig2.js</c>, and it publishes
    /// both halves of the contract: the realised period span over a 400-hole sample, and the rise
    /// curve at seven points across one 420 ms window. Reading it means a re-exported rig that moves
    /// the timing turns these red, which is the whole point of pinning against a derived file rather
    /// than a memory of one.</para>
    /// </summary>
    public sealed class ClamSpurtMathTests
    {
        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static string SidecarPath => Path.Combine(
            RepoRoot, "docs", "art", "rigs", "catch-pass-2-kit", "sidecars", "shellfishRig2.rig.json");

        static string Sidecar()
        {
            Assert.IsTrue(File.Exists(SidecarPath),
                          $"the shellfish sidecar is missing at {SidecarPath} — the contract these " +
                          "tests exist to hold has left the tree");
            return File.ReadAllText(SidecarPath);
        }

        /// <summary>
        /// Every number inside a named JSON array, flattened.
        ///
        /// <para>⚠️ Scans to the MATCHING bracket rather than the first one: <c>spurtSampleAtDur420</c>
        /// is an array of [t, rise] PAIRS, so a lazy <c>[^\]]*</c> would stop inside the first pair
        /// and quietly hand back two numbers instead of fourteen — a shape defect that reads as a
        /// contract change.</para>
        /// </summary>
        static double[] NumbersOf(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"the sidecar carries no '{key}'");

            int open = json.IndexOf('[', at);
            Assert.GreaterOrEqual(open, 0, $"'{key}' is not an array");

            int depth = 0, end = -1;
            for (int i = open; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']' && --depth == 0) { end = i; break; }
            }
            Assert.GreaterOrEqual(end, 0, $"'{key}' has no matching close bracket");

            var outp = new List<double>();
            foreach (Match n in Regex.Matches(json.Substring(open + 1, end - open - 1),
                                              @"-?\d+(\.\d+)?([eE][-+]?\d+)?"))
                outp.Add(double.Parse(n.Value, CultureInfo.InvariantCulture));
            return outp.ToArray();
        }

        // -----------------------------------------------------------------------------------------
        // the timing
        // -----------------------------------------------------------------------------------------

        [Test]
        public void ThePeriodSpan_ContainsTheSidecarsOwn400HoleSample()
        {
            // The sidecar reports what holes(3, 400, 100, 100) actually realised. A 400-draw sample of
            // a uniform [2600, 7800) lands just inside both ends — so the assertion is CONTAINMENT,
            // not equality, and it is still tight: the sample must not escape the declared span, and
            // the span must not be padded far beyond what the rig can produce.
            double[] sample = NumbersOf(Sidecar(), "periodMs");
            Assert.AreEqual(2, sample.Length, "periodMs is a [min, max] pair");

            Assert.GreaterOrEqual(sample[0], ClamSpurtMath.MinPeriodMilliseconds,
                                  "the rig drew a period shorter than our declared floor");
            Assert.Less(sample[1], ClamSpurtMath.MaxPeriodMilliseconds,
                        "the rig drew a period longer than our declared ceiling");

            // And the span is not slack: 400 draws should reach within ~2% of each end.
            double span = ClamSpurtMath.MaxPeriodMilliseconds - ClamSpurtMath.MinPeriodMilliseconds;
            Assert.Less(sample[0] - ClamSpurtMath.MinPeriodMilliseconds, span * 0.02,
                        "our floor sits well below anything the rig draws — it is not the rig's floor");
            Assert.Less(ClamSpurtMath.MaxPeriodMilliseconds - sample[1], span * 0.02,
                        "our ceiling sits well above anything the rig draws — it is not the rig's ceiling");
        }

        [Test]
        public void TheSpurtLasts_ExactlyWhatTheSidecarSays()
        {
            double[] dur = NumbersOf(Sidecar(), "durMs");
            Assert.AreEqual(2, dur.Length);
            Assert.AreEqual(dur[0], dur[1], 0.0, "the rig writes a FLAT dur on every hole — it is not rolled");
            Assert.AreEqual(dur[0], ClamSpurtMath.SpurtMilliseconds, 0.0,
                            "our spurt length is not the rig's");
        }

        [Test]
        public void TheRiseCurve_MatchesTheSidecarsSampleAtEveryPoint()
        {
            // spurtSampleAtDur420 is a flat [t0, r0, t1, r1, ...] once the nesting is stripped.
            double[] flat = NumbersOf(Sidecar(), "spurtSampleAtDur420");
            Assert.IsTrue(flat.Length >= 4 && flat.Length % 2 == 0,
                          "spurtSampleAtDur420 should be (t, rise) pairs");

            // Drive a hole whose window we know: with phase 0 and any period, the window opens at t=0.
            const float period = 3507.4f;
            for (int i = 0; i < flat.Length; i += 2)
            {
                double t = flat[i];
                double wantRise = flat[i + 1];

                bool on = ClamSpurtMath.TrySpurt(t, phaseMs: 0f, periodMs: period,
                                                 out float rise, out float u);
                Assert.IsTrue(on, $"the sidecar samples t={t} ms, so the window must still be open there");
                Assert.AreEqual(wantRise, rise, 0.0015,
                                $"rise at t={t} ms disagrees with the sidecar (u={u:F3})");
            }
        }

        [Test]
        public void TheWindowOpensOnTheRigsPhaseConvention_NotAtTPhase()
        {
            // ⚠️ THE TRAP, asserted as a negative control. The rig opens the window when
            // (t + phase) % period <= dur, so a hole's first spurt is at t = period - (phase % period).
            // Reading `phase` as "when it starts" is the natural mistake and it is wrong; if this ever
            // stops failing at t = phase, someone has quietly redefined the convention.
            const float phase = 1234f, period = 5000f;

            float open = period - (phase % period);
            Assert.IsTrue(ClamSpurtMath.TrySpurt(open, phase, period, out float rise0, out _),
                          "the window must be open at t = period - (phase % period)");
            Assert.AreEqual(0f, rise0, 1e-5f, "and it opens at the lip, rise 0");

            Assert.IsTrue(ClamSpurtMath.TrySpurt(open + ClamSpurtMath.SpurtMilliseconds * 0.5f,
                                                 phase, period, out float crest, out _));
            Assert.AreEqual(1f, crest, 1e-5f, "the crest is half way through the window");

            // The negative control: t = phase is NOT the opening, for these numbers.
            Assert.IsFalse(ClamSpurtMath.TrySpurt(phase, phase, period, out _, out _),
                           "t = phase is NOT when a hole spurts — see ClamSpurtMath's warning");
        }

        [Test]
        public void OutsideTheWindow_NothingIsDrawn()
        {
            const float phase = 0f, period = 4000f;
            Assert.IsFalse(ClamSpurtMath.TrySpurt(ClamSpurtMath.SpurtMilliseconds + 1f, phase, period,
                                                  out float rise, out float u),
                           "one millisecond past the window is quiet");
            Assert.AreEqual(0f, rise, 0f, "a quiet hole reports no jet, so a careless caller draws nothing");
            Assert.AreEqual(0f, u, 0f);
        }

        [Test]
        public void ANegativeClock_StillSpurts_RatherThanGoingDark()
        {
            // A caller may hand a clock that has been rewound or that starts below zero. C#'s % keeps
            // the sign of the dividend, so without the wrap this would silently never fire.
            const float phase = 100f, period = 3000f;
            bool everOn = false;
            for (double t = -12000; t < 0; t += 10)
                if (ClamSpurtMath.TrySpurt(t, phase, period, out _, out _)) { everOn = true; break; }
            Assert.IsTrue(everOn, "a negative clock must still reach a spurt window");
        }

        [Test]
        public void ADegeneratePeriod_IsQuiet_NotADivideByZero()
        {
            Assert.IsFalse(ClamSpurtMath.TrySpurt(1000, 0f, 0f, out _, out _), "period 0 is quiet");
            Assert.IsFalse(ClamSpurtMath.TrySpurt(1000, 0f, -5f, out _, out _), "a negative period is quiet");
            Assert.IsFalse(ClamSpurtMath.TrySpurt(1000, 0f, float.NaN, out _, out _), "NaN is quiet");
        }

        // -----------------------------------------------------------------------------------------
        // the per-hole roll
        // -----------------------------------------------------------------------------------------

        [Test]
        public void EveryRolledHole_LandsInsideTheRigsOwnRanges()
        {
            for (int i = 0; i < 400; i++)
            {
                float x = (i % 20) * 0.37f, y = (i / 20) * 0.53f;
                ClamSpurtMath.TimingFor(12345, x, y, out float phase, out float period);

                Assert.GreaterOrEqual(period, ClamSpurtMath.MinPeriodMilliseconds, $"hole {i} period");
                Assert.Less(period, ClamSpurtMath.MaxPeriodMilliseconds, $"hole {i} period");
                Assert.GreaterOrEqual(phase, 0f, $"hole {i} phase");
                Assert.Less(phase, ClamSpurtMath.PhaseSpanMilliseconds, $"hole {i} phase");
            }
        }

        [Test]
        public void TheRollIsDeterministic_AndAHoleThatMovesIsADifferentHole()
        {
            ClamSpurtMath.TimingFor(7, 10.25f, -3.5f, out float p1, out float t1);
            ClamSpurtMath.TimingFor(7, 10.25f, -3.5f, out float p2, out float t2);
            Assert.AreEqual(p1, p2, 0f, "same seed and position, same phase — rule 5");
            Assert.AreEqual(t1, t2, 0f, "same seed and position, same period");

            ClamSpurtMath.TimingFor(8, 10.25f, -3.5f, out float p3, out _);
            Assert.AreNotEqual(p1, p3, "a different world seed is a different flat");

            ClamSpurtMath.TimingFor(7, 10.26f, -3.5f, out float p4, out _);
            Assert.AreNotEqual(p1, p4, "a hole one centimetre away rolls its own phase");
        }

        [Test]
        public void PositionsAreQuantisedToTheCentimetre_SoFloatNoiseCannotRetimeAHole()
        {
            ClamSpurtMath.TimingFor(3, 4.20f, 1.10f, out float a, out float pa);
            ClamSpurtMath.TimingFor(3, 4.2000004f, 1.0999996f, out float b, out float pb);
            Assert.AreEqual(a, b, 0f, "last-bit float noise must not re-time a hole between two builds");
            Assert.AreEqual(pa, pb, 0f);
        }

        [Test]
        public void NeighbouringHolesDoNotSpurtInUnison()
        {
            // The flat must not read as a sprinkler system. Across a grid of adjacent holes, the
            // phases should occupy the whole span rather than clumping — checked as coverage of ten
            // buckets, which a hash that ignored one input would fail outright.
            var buckets = new int[10];
            for (int i = 0; i < 200; i++)
            {
                ClamSpurtMath.TimingFor(99, 5f + i * 0.10f, 2f, out float phase, out _);
                buckets[Mathf.Clamp((int)(phase / ClamSpurtMath.PhaseSpanMilliseconds * 10), 0, 9)]++;
            }
            foreach (int b in buckets)
                Assert.Greater(b, 0, "a tenth of the phase span went unused — the holes are clumping");
        }

        [Test]
        public void TheDutyCycle_IsTheShareOfItsLifeAHoleSpendsSpurting()
        {
            Assert.AreEqual(ClamSpurtMath.SpurtMilliseconds / 4000f,
                            ClamSpurtMath.DutyCycle(4000f), 1e-6f);

            // Over the rig's own span that is 5.4%-16.2%; the sidecar's 400-hole sample averages ~9%.
            Assert.AreEqual(0.0538f, ClamSpurtMath.DutyCycle(ClamSpurtMath.MaxPeriodMilliseconds), 5e-4f);
            Assert.AreEqual(0.1615f, ClamSpurtMath.DutyCycle(ClamSpurtMath.MinPeriodMilliseconds), 5e-4f);
            Assert.AreEqual(0f, ClamSpurtMath.DutyCycle(0f), 0f, "a degenerate period is never spurting");
        }

        // -----------------------------------------------------------------------------------------
        // what the presenter draws
        // -----------------------------------------------------------------------------------------

        [Test]
        public void TheJetIsRiseTimesFourPixels_AndTheDropletDetachesLate()
        {
            Assert.AreEqual(4, ClamSpurtMath.JetPixels, "the sidecar says rise x 4 px");
            Assert.AreEqual(0f, ClamSpurtMath.JetHeightPixels(0f), 1e-6f);
            Assert.AreEqual(4f, ClamSpurtMath.JetHeightPixels(1f), 1e-6f);
            Assert.AreEqual(2f, ClamSpurtMath.JetHeightPixels(0.5f), 1e-6f);

            Assert.IsFalse(ClamSpurtMath.ShowsDroplet(0.44f), "no droplet before the sidecar's u = 0.45");
            Assert.IsTrue(ClamSpurtMath.ShowsDroplet(0.46f), "the droplet detaches past u = 0.45");
        }

        [Test]
        public void TheRiseIsSymmetric_RisingAndFallingThroughOneCrest()
        {
            const float period = 5000f;
            float crest = 0f; int crestAt = -1;
            var samples = new List<float>();
            for (int t = 0; t <= (int)ClamSpurtMath.SpurtMilliseconds; t += 10)
            {
                Assert.IsTrue(ClamSpurtMath.TrySpurt(t, 0f, period, out float rise, out _));
                samples.Add(rise);
                if (rise > crest) { crest = rise; crestAt = samples.Count - 1; }
            }

            Assert.AreEqual(1f, crest, 1e-4f, "the jet reaches full height exactly once");
            Assert.AreEqual((samples.Count - 1) / 2, crestAt, 1,
                            "the crest sits in the middle of the window");

            // Mirrored about the crest — a jet that rises and falls, not a saw.
            for (int i = 0; i < samples.Count / 2; i++)
                Assert.AreEqual(samples[i], samples[samples.Count - 1 - i], 2e-3f,
                                $"the rise is not symmetric at sample {i}");
        }
    }
}
