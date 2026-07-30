using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The bite/hook timing core (owner drop §10.2, PR'd as the minigame lane's slice 1). Pins the
    /// contracts the moment leans on: a strike inside the TRUE take hooks; striking on a teasing
    /// false nibble (or at nothing) is EARLY and ends the pass; sleeping through the window is LATE;
    /// a miss is never instantly fatal — "It keeps nibbling", she may return, bounded by MaxPasses;
    /// the whole sequence replays bit-for-bit under a seed (the RodFightSim discipline). Pure POCO —
    /// no scene, no clock, no input.
    /// </summary>
    public class BiteSequenceSimTests
    {
        private const float Dt = 0.02f;   // a 50 Hz tick — small enough to land inside every window

        // A profile with no randomness where it matters: fixed nibble count, pinned gaps.
        private static BiteProfile Fixed(int nibbles, float window = 0.8f,
                                         float returnChance = 0f, int maxPasses = 3)
            => new BiteProfile(nibbles, nibbles, 1f, 1f, 0.3f, window,
                               returnChance, 1f, 1f, maxPasses);

        private static BiteSequenceSim Sim(in BiteProfile p, int seed = 7)
            => new BiteSequenceSim(p, new System.Random(seed));

        /// <summary>Tick with no strike until <paramref name="phase"/> (fails the test after
        /// <paramref name="maxSeconds"/> — a sequence must never stall).</summary>
        private static void TickUntil(BiteSequenceSim sim, BitePhase phase, float maxSeconds = 60f)
        {
            float t = 0f;
            while (sim.Phase != phase)
            {
                sim.Tick(Dt, strike: false);
                t += Dt;
                if (t > maxSeconds) Assert.Fail($"never reached {phase} (stuck in {sim.Phase})");
            }
        }

        // ---- the hook ---------------------------------------------------------------------------

        [Test]
        public void StrikeInsideTheTake_Hooks()
        {
            var sim = Sim(Fixed(nibbles: 1));
            TickUntil(sim, BitePhase.TakeOpen);

            sim.Tick(Dt, strike: true);

            Assert.AreEqual(BitePhase.Hooked, sim.Phase);
            Assert.IsTrue(sim.IsOver);
            Assert.AreEqual(BiteMiss.None, sim.LastMiss);
        }

        [Test]
        public void AStrikeOnTheLapsingTick_StillHooks()
        {
            // The strike is judged BEFORE time advances: pressing on the very tick the window would
            // lapse is a hook, not a heartbreak.
            var sim = Sim(Fixed(nibbles: 0, window: 0.1f));
            TickUntil(sim, BitePhase.TakeOpen);
            while (sim.Phase == BitePhase.TakeOpen && sim.PhaseSeconds + Dt < 0.1f)
                sim.Tick(Dt, strike: false);

            sim.Tick(Dt, strike: true);   // this tick would have lapsed the window
            Assert.AreEqual(BitePhase.Hooked, sim.Phase);
        }

        // ---- the false-nibble game ----------------------------------------------------------------

        [Test]
        public void StrikeOnATease_IsEarly_AndEndsThePass()
        {
            var sim = Sim(Fixed(nibbles: 2, returnChance: 0f));
            TickUntil(sim, BitePhase.NibbleDip);

            sim.Tick(Dt, strike: true);

            Assert.AreEqual(BiteMiss.Early, sim.LastMiss, "striking a tease is the mistake the game is about");
            Assert.AreEqual(BitePhase.Gone, sim.Phase, "no return chance → she's off");
        }

        [Test]
        public void StrikeAtNothing_IsEarlyToo()
        {
            var sim = Sim(Fixed(nibbles: 1, returnChance: 0f));
            Assert.AreEqual(BitePhase.Approach, sim.Phase, "a pass opens on the quiet approach");

            sim.Tick(Dt, strike: true);

            Assert.AreEqual(BiteMiss.Early, sim.LastMiss);
            Assert.IsTrue(sim.IsOver);
        }

        [Test]
        public void SleepingThroughTheWindow_IsLate()
        {
            var sim = Sim(Fixed(nibbles: 0, window: 0.4f, returnChance: 0f));
            TickUntil(sim, BitePhase.TakeOpen);

            for (float t = 0f; t < 1f && !sim.IsOver; t += Dt) sim.Tick(Dt, strike: false);

            Assert.AreEqual(BiteMiss.Late, sim.LastMiss, "she spat it");
            Assert.AreEqual(BitePhase.Gone, sim.Phase);
        }

        // ---- "It keeps nibbling" (owner §10.2) ----------------------------------------------------

        [Test]
        public void AMiss_WithReturnChance_BringsHerBackForAnotherPass()
        {
            var sim = Sim(Fixed(nibbles: 1, returnChance: 1f, maxPasses: 3));
            TickUntil(sim, BitePhase.NibbleDip);
            sim.Tick(Dt, strike: true);   // early — pass 0 ends

            Assert.AreEqual(BitePhase.Away, sim.Phase, "she's circling back, not gone");
            Assert.AreEqual(1, sim.PassIndex);

            TickUntil(sim, BitePhase.Approach);
            Assert.AreEqual(BiteMiss.None, sim.LastMiss, "a fresh pass wipes the old mistake");
            TickUntil(sim, BitePhase.TakeOpen);
            sim.Tick(Dt, strike: true);
            Assert.AreEqual(BitePhase.Hooked, sim.Phase, "the second chance is a real one");
        }

        [Test]
        public void StrikingWhileSheIsAway_DoesNothing()
        {
            var sim = Sim(Fixed(nibbles: 0, returnChance: 1f, maxPasses: 2));
            TickUntil(sim, BitePhase.TakeOpen);
            for (float t = 0f; t < 2f && sim.Phase == BitePhase.TakeOpen; t += Dt) sim.Tick(Dt, false);
            Assert.AreEqual(BitePhase.Away, sim.Phase);

            sim.Tick(Dt, strike: true);   // hammering the key at empty water

            Assert.AreEqual(BitePhase.Away, sim.Phase, "there is nothing there to strike");
            Assert.IsFalse(sim.IsOver);
        }

        [Test]
        public void Passes_AreBoundedByMaxPasses()
        {
            var sim = Sim(Fixed(nibbles: 0, window: 0.3f, returnChance: 1f, maxPasses: 3));
            for (int pass = 0; pass < 3; pass++)
            {
                if (sim.IsOver) break;
                TickUntil(sim, BitePhase.TakeOpen);
                for (float t = 0f; t < 1f && sim.Phase == BitePhase.TakeOpen; t += Dt) sim.Tick(Dt, false);
            }

            Assert.AreEqual(BitePhase.Gone, sim.Phase,
                "even a fish that always returns runs out of patience — one cast can't nibble forever");
            Assert.AreEqual(3, sim.PassIndex);
        }

        [Test]
        public void AMiss_WithNoReturnChance_IsGone()
        {
            var sim = Sim(Fixed(nibbles: 0, window: 0.3f, returnChance: 0f));
            TickUntil(sim, BitePhase.TakeOpen);
            for (float t = 0f; t < 1f && !sim.IsOver; t += Dt) sim.Tick(Dt, false);
            Assert.AreEqual(BitePhase.Gone, sim.Phase);
        }

        // ---- determinism (the RodFightSim discipline) ---------------------------------------------

        [Test]
        public void SameSeed_SameInputs_ReplaysBitForBit()
        {
            var wide = new BiteProfile(1, 4, 0.5f, 2.5f, 0.3f, 0.7f, 0.75f, 1f, 3f, 4);
            List<string> TraceOf(int seed)
            {
                var sim = new BiteSequenceSim(wide, new System.Random(seed));
                var trace = new List<string>();
                for (int i = 0; i < 4000 && !sim.IsOver; i++)
                {
                    bool strike = i % 611 == 610;   // scripted, identical for both runs
                    sim.Tick(Dt, strike);
                    trace.Add($"{sim.Phase}|{sim.PassIndex}|{sim.NibbleIndex}|{sim.LastMiss}");
                }
                return trace;
            }

            CollectionAssert.AreEqual(TraceOf(1234), TraceOf(1234),
                "same seed + same strikes must replay the identical sequence");
            Assert.AreNotEqual(string.Join(",", TraceOf(1234)), string.Join(",", TraceOf(4321)),
                "different seeds genuinely differ (the schedule is really random)");
        }

        [Test]
        public void NibbleCounts_StayInsideTheAuthoredRange()
        {
            var p = new BiteProfile(1, 3, 0.2f, 0.4f, 0.1f, 0.2f, 0f, 0f, 0f, 1);
            for (int seed = 0; seed < 50; seed++)
            {
                var sim = new BiteSequenceSim(p, new System.Random(seed));
                TickUntil(sim, BitePhase.TakeOpen, maxSeconds: 30f);
                Assert.That(sim.NibbleIndex, Is.InRange(1, 3),
                    $"seed {seed}: the tease count must honour the authored band");
            }
        }

        [Test]
        public void TakeWindow01_IsTheUrgencyRead()
        {
            var sim = Sim(Fixed(nibbles: 0, window: 1.0f, returnChance: 0f));
            Assert.AreEqual(0f, sim.TakeWindow01, 1e-6f, "0 outside the window");
            TickUntil(sim, BitePhase.TakeOpen);
            sim.Tick(0.5f, strike: false);
            Assert.AreEqual(0.5f, sim.TakeWindow01, 1e-3f, "halfway through the open window");
        }
    }
}
