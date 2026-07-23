using NUnit.Framework;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2 Wave 3 — the fish's run↔slack rhythm generator (<see cref="RodFightRhythm"/>).
    /// Pins: determinism under a seed (a seeded fight replays its rhythm bit-for-bit), the cadence
    /// SHAPE matching the authored <see cref="StaminaCadence"/> numbers (run/slack averages, the
    /// jitter's bounds), the she-opens-running contract, and the big-dt phase-crossing correctness
    /// headless tests rely on (frame counts are not time — dt is injected).
    /// </summary>
    public class RodFightRhythmTests
    {
        private static StaminaCadence Cadence(float run, float slack, float jitter)
            => new StaminaCadence { RunSeconds = run, SlackSeconds = slack, Jitter01 = jitter };

        /// <summary>Sample Effort01 at a fixed dt for a duration; returns the trace.</summary>
        private static float[] Trace(RodFightRhythm r, float dt, int ticks)
        {
            var trace = new float[ticks];
            for (int i = 0; i < ticks; i++) { r.Tick(dt); trace[i] = r.Effort01; }
            return trace;
        }

        [Test]
        public void SameSeed_ReplaysTheRhythmBitForBit()
        {
            var a = new RodFightRhythm(Cadence(2.0f, 1.4f, 0.5f), new System.Random(1234));
            var b = new RodFightRhythm(Cadence(2.0f, 1.4f, 0.5f), new System.Random(1234));
            CollectionAssert.AreEqual(Trace(a, 0.02f, 3000), Trace(b, 0.02f, 3000),
                "a seeded fight must replay its run/slack rhythm identically");
        }

        [Test]
        public void DifferentSeeds_Diverge()
        {
            var a = new RodFightRhythm(Cadence(2.0f, 1.4f, 0.5f), new System.Random(1));
            var b = new RodFightRhythm(Cadence(2.0f, 1.4f, 0.5f), new System.Random(2));
            CollectionAssert.AreNotEqual(Trace(a, 0.02f, 3000), Trace(b, 0.02f, 3000),
                "jittered rhythms under different seeds should not be identical");
        }

        [Test]
        public void OpensRunning()
        {
            var r = new RodFightRhythm(Cadence(1.0f, 1.0f, 0f), new System.Random(7));
            Assert.IsTrue(r.IsRunning, "the strike is the moment she bolts — a fight opens mid-RUN");
            Assert.AreEqual(1f, r.Effort01);
        }

        [Test]
        public void ZeroJitter_IsMetronomic_AtTheAuthoredLengths()
        {
            // Run 2.0 s then slack 1.0 s, exactly, forever (no jitter). Sample at 0.01 s.
            var r = new RodFightRhythm(Cadence(2.0f, 1.0f, 0f), new System.Random(42));
            float[] trace = Trace(r, 0.01f, 900);   // 9 s = 3 full cycles

            // Measure each phase length off the trace.
            var lengths = PhaseLengths(trace, 0.01f);
            Assert.GreaterOrEqual(lengths.runs.Count, 2, "should see several runs in 9 s");
            foreach (float run in lengths.runs) Assert.AreEqual(2.0f, run, 0.03f, "authored RunSeconds");
            foreach (float slack in lengths.slacks) Assert.AreEqual(1.0f, slack, 0.03f, "authored SlackSeconds");
        }

        [Test]
        public void Jitter_StaysInsideTheAuthoredBounds_AndAveragesTheAuthoredLengths()
        {
            // ±40% jitter: every phase must land in [0.6, 1.4]× its base, and the long-run average
            // must come back to the authored number (uniform jitter is centred).
            const float run = 1.5f, slack = 0.9f, jitter = 0.4f;
            var r = new RodFightRhythm(Cadence(run, slack, jitter), new System.Random(99));
            float[] trace = Trace(r, 0.01f, 60000);   // 600 s — plenty of phases

            var lengths = PhaseLengths(trace, 0.01f);
            Assert.GreaterOrEqual(lengths.runs.Count, 100, "long trace should hold many runs");

            float runSum = 0f, slackSum = 0f;
            foreach (float len in lengths.runs)
            {
                Assert.That(len, Is.InRange(run * (1f - jitter) - 0.03f, run * (1f + jitter) + 0.03f),
                    "a run length outside the authored jitter bounds");
                runSum += len;
            }
            foreach (float len in lengths.slacks)
            {
                Assert.That(len, Is.InRange(slack * (1f - jitter) - 0.03f, slack * (1f + jitter) + 0.03f),
                    "a slack length outside the authored jitter bounds");
                slackSum += len;
            }
            Assert.AreEqual(run, runSum / lengths.runs.Count, run * 0.08f,
                "runs should average the authored RunSeconds");
            Assert.AreEqual(slack, slackSum / lengths.slacks.Count, slack * 0.08f,
                "slacks should average the authored SlackSeconds");
        }

        [Test]
        public void LargeDt_CrossesPhases_InsteadOfStalling()
        {
            // One 10 s step across a 1 s / 1 s metronome must land mid-phase correctly (10 = 5 full
            // cycles → she's exactly where a fine-grained ticker would put her).
            var coarse = new RodFightRhythm(Cadence(1f, 1f, 0f), new System.Random(5));
            coarse.Tick(10.25f);
            var fine = new RodFightRhythm(Cadence(1f, 1f, 0f), new System.Random(5));
            for (int i = 0; i < 1025; i++) fine.Tick(0.01f);
            Assert.AreEqual(fine.IsRunning, coarse.IsRunning,
                "a coarse dt must cross phases like the equivalent fine ticks (headless dt injection)");
        }

        [Test]
        public void BadDt_IsANoOp()
        {
            var r = new RodFightRhythm(Cadence(1f, 1f, 0f), new System.Random(3));
            bool before = r.IsRunning;
            r.Tick(float.NaN);
            r.Tick(-1f);
            r.Tick(0f);
            Assert.AreEqual(before, r.IsRunning, "NaN/negative/zero dt must not advance the rhythm");
        }

        // ---- helpers ------------------------------------------------------------------------

        private static (System.Collections.Generic.List<float> runs,
                        System.Collections.Generic.List<float> slacks) PhaseLengths(float[] trace, float dt)
        {
            var runs = new System.Collections.Generic.List<float>();
            var slacks = new System.Collections.Generic.List<float>();
            int start = 0;
            for (int i = 1; i < trace.Length; i++)
            {
                if (trace[i] == trace[i - 1]) continue;
                float len = (i - start) * dt;
                // Drop the FIRST segment (it began before sampling — its length is partial).
                if (start > 0) (trace[i - 1] > 0.5f ? runs : slacks).Add(len);
                start = i;
            }
            return (runs, slacks);
        }
    }
}
