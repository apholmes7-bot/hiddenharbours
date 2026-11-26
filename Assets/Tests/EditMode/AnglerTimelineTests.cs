using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A villager's fishing session is a pure function of the clock</b> (ADR 0053 §11, rule 5):
    /// <see cref="AnglerTimeline"/> gives bit-identical beats for equal inputs in any order of asking; plays
    /// its beats in order with a catch and without; carries the catch; finds its slot in O(1) at year 30;
    /// begins a slot only when the villager is there for the whole of it; moves with the seed and the key;
    /// and rests out an invalid script. <see cref="FigureKeyHash"/> is pinned to FNV-1a.
    /// </summary>
    public sealed class AnglerTimelineTests
    {
        private const int Seed = 1234;
        private const string Key = "npc.test_angler";
        private const string Cod = "fish.atlantic_cod";
        private const float Heading = 90f;
        private const double Step = 0.01d;
        private const double SecondsPerYear = 365d * 24d * 3600d;

        private static AnglerScript Script() => new AnglerScript
        {
            SlotSeconds = 120f,
            CastBackSeconds = 1f, CastSeconds = 1.5f, WaitMinSeconds = 3f, WaitMaxSeconds = 8f,
            BiteSeconds = 1.2f, StrikeSeconds = 0.6f,
            DeepMinSeconds = 4f, DeepMaxSeconds = 8f, SurfaceMinSeconds = 3f, SurfaceMaxSeconds = 6f,
            LandSeconds = 2.5f, EmptyRetrieveSeconds = 3f,
            CastReachM = 6f, CastBearingDegrees = 0f, CastBearingJitterDegrees = 20f,
            RunSwingM = 1.5f, TensionMin01 = 0.3f, TensionMax01 = 0.8f, FightWaveHz = 0.4f,
        };

        private static readonly ShoreCatch CodCatch = new ShoreCatch(Cod, 2.4f, 0.55f);
        private static uint KeyHash => FigureKeyHash.Of(Key);

        private static AnglerBeat At(in AnglerScript script, double now, bool hasCatch = true, int seed = Seed,
                                     uint? key = null, ShoreCatch? shoreCatch = null)
        {
            AnglerTimeline.Evaluate(in script, shoreCatch ?? CodCatch, hasCatch, seed, key ?? KeyHash, Heading, now,
                                    out AnglerBeat beat);
            return beat;
        }

        [Test]
        public void EqualInputs_GiveTheBitIdenticalBeat_InAnyOrderOfAsking()
        {
            AnglerScript script = Script();
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 5);
            var times = new List<double>();
            for (double t = start; t < start + script.SlotSeconds; t += 0.37d) times.Add(t);

            var forward = new List<string>();
            foreach (double t in times) forward.Add(Fingerprint(At(in script, t)));
            var backward = new List<string>();
            for (int i = times.Count - 1; i >= 0; i--) backward.Add(Fingerprint(At(in script, times[i])));
            backward.Reverse();
            Assert.AreEqual(forward, backward, "the beat depends on the order it was asked in");
        }

        [Test]
        public void WithACatch_TheBeatsComeInOrder_AndTheBodyFollowsThem()
        {
            AnglerScript script = Script();
            List<FishingPhase> phases = PhasesOfSlot(in script, 3, hasCatch: true, out List<string> keys);
            CollectionAssert.AreEqual(new[]
            {
                FishingPhase.WindBack, FishingPhase.Cast, FishingPhase.Waiting, FishingPhase.Bite,
                FishingPhase.FightDeep, FishingPhase.FightSurface, FishingPhase.Landed, FishingPhase.Idle,
            }, phases);
            CollectionAssert.AreEqual(new[] { "castBack", "castRelease", "hold", "bite", "strike", "reel", "land", null }, keys);
        }

        [Test]
        public void WithNoCatch_HeCastsWaitsAndReelsInEmpty_AndNoFishIsEverOnHisLine()
        {
            AnglerScript script = Script();
            List<FishingPhase> phases = PhasesOfSlot(in script, 3, hasCatch: false, out _);
            CollectionAssert.AreEqual(new[]
            {
                FishingPhase.WindBack, FishingPhase.Cast, FishingPhase.Waiting, FishingPhase.NoBite, FishingPhase.Idle,
            }, phases);

            // A catch with no fish id is no catch.
            List<FishingPhase> empty = PhasesOfSlot(in script, 3, hasCatch: true, out _, new ShoreCatch(null, 1f, 0.5f));
            CollectionAssert.AreEqual(phases, empty);

            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 3);
            for (double t = start; t < start + script.SlotSeconds; t += 0.25d)
                Assert.IsNull(At(in script, t, hasCatch: false).State.FishId, $"a fish on an empty line at {t - start:F2}s");
        }

        [Test]
        public void TheCatch_IsCarriedFromTheBite_ToTheLanding()
        {
            AnglerScript script = Script();
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 9);
            int seen = 0;
            for (double t = start; t < start + script.SlotSeconds; t += 0.1d)
            {
                AnglerBeat b = At(in script, t);
                bool fish = b.Phase == FishingPhase.Bite || b.Phase == FishingPhase.FightDeep
                            || b.Phase == FishingPhase.FightSurface || b.Phase == FishingPhase.Landed;
                if (!fish)
                {
                    Assert.IsNull(b.State.FishId, $"{b.Phase}: a fish before the bite or after the session");
                    continue;
                }
                seen++;
                Assert.AreEqual(Cod, b.State.FishId);
                Assert.AreEqual(CodCatch.WeightKg, b.State.WeightKg);
                Assert.AreEqual(CodCatch.Size01, b.State.Size01);
                if (b.Phase == FishingPhase.Landed) Assert.AreEqual(1f, b.State.Landing01);
                if (b.Phase == FishingPhase.FightDeep || b.Phase == FishingPhase.FightSurface)
                {
                    Assert.That(b.State.Tension01, Is.InRange(script.TensionMin01, script.TensionMax01));
                    Assert.That(b.State.Landing01, Is.InRange(0f, 1f));
                }
            }
            Assert.Greater(seen, 0, "harness: the slot had no fish beats");
        }

        [Test]
        public void TheSlot_IsFoundInO1_AtYearThirty_AndThePhaseClockKeepsItsPrecision()
        {
            AnglerScript script = Script();
            double now = 30d * SecondsPerYear + 12345.678d;
            long slot = AnglerTimeline.SlotIndex(in script, Seed, KeyHash, now);
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, slot);
            Assert.That(start, Is.LessThanOrEqualTo(now));
            Assert.That(now, Is.LessThan(start + script.SlotSeconds));

            AnglerBeat b = At(in script, start + 0.5d);
            Assert.AreEqual(slot, b.Slot);
            Assert.AreEqual(start, b.SlotStartSeconds);
            Assert.AreEqual(FishingPhase.WindBack, b.Phase);
            Assert.AreEqual(0.5f, b.SecondsInPhase, 1e-4f, "the seconds in a phase lost precision at year 30");
            Assert.AreEqual(0.5f, b.Scrub01, 1e-4f, "the wind-back scrubs by its progress");

            // The session at year 30 is the same shape as on day one.
            List<FishingPhase> phases = PhasesOfSlot(in script, slot, hasCatch: true, out _);
            Assert.AreEqual(FishingPhase.WindBack, phases[0]);
            Assert.AreEqual(FishingPhase.Idle, phases[phases.Count - 1]);
            Assert.AreEqual(8, phases.Count);
        }

        [Test]
        public void ASlotBegins_OnlyWhenHeIsThereForAllOfIt()
        {
            AnglerScript script = Script();
            const long slot = 40;
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, slot);
            double end = start + script.SlotSeconds;

            Assert.IsTrue(AnglerTimeline.SlotMayBegin(in script, Seed, KeyHash, slot, start, end), "exactly there");
            Assert.IsTrue(AnglerTimeline.SlotMayBegin(in script, Seed, KeyHash, slot, start - 50d, end + 50d));
            Assert.IsFalse(AnglerTimeline.SlotMayBegin(in script, Seed, KeyHash, slot, start + 0.001d, end + 500d),
                           "a mid-slot arrival must not begin that slot");
            Assert.IsTrue(AnglerTimeline.SlotMayBegin(in script, Seed, KeyHash, slot + 1, start + 0.001d, end + 500d),
                          "a mid-slot arrival begins the next slot");
            Assert.IsFalse(AnglerTimeline.SlotMayBegin(in script, Seed, KeyHash, slot, start - 50d, end - 0.001d),
                           "a slot ending after his departure must not begin");
        }

        [Test]
        public void TheSeedAndTheKey_EachChangeHisSessions()
        {
            AnglerScript script = Script();
            Assert.AreNotEqual(AnglerTimeline.KeyPhaseSeconds(in script, Seed, KeyHash),
                               AnglerTimeline.KeyPhaseSeconds(in script, Seed + 1, KeyHash), "the seed must move his grid");
            Assert.AreNotEqual(AnglerTimeline.KeyPhaseSeconds(in script, Seed, KeyHash),
                               AnglerTimeline.KeyPhaseSeconds(in script, Seed, FigureKeyHash.Of("npc.other")),
                               "the key must move his grid");
            double phase = AnglerTimeline.KeyPhaseSeconds(in script, Seed, KeyHash);
            Assert.That(phase, Is.GreaterThanOrEqualTo(0d).And.LessThan(script.SlotSeconds));

            // Slot by slot, the cast's bearing comes off (seed, key, slot).
            Assert.IsTrue(AimsDiffer(in script, Seed, KeyHash, Seed + 1, KeyHash), "the seed did not change any cast");
            Assert.IsTrue(AimsDiffer(in script, Seed, KeyHash, Seed, FigureKeyHash.Of("npc.other")), "the key did not change any cast");
        }

        [Test]
        public void AnInvalidScript_IsAllRest()
        {
            AnglerScript script = Script();
            script.SlotSeconds = 10f;   // shorter than the longest session
            Assert.IsFalse(script.IsValid(out string why));
            StringAssert.Contains("slot", why);
            AnglerBeat b = At(in script, 1000d);
            Assert.AreEqual(FishingPhase.Idle, b.Phase);
            Assert.IsFalse(b.InSession);
            Assert.IsNull(b.StateKey);
            Assert.IsTrue(float.IsNaN(b.Scrub01));

            AnglerScript band = Script();
            band.TensionMax01 = 1.5f;
            Assert.IsFalse(band.IsValid(out _));
            AnglerScript min = Script();
            min.WaitMaxSeconds = 1f;
            Assert.IsFalse(min.IsValid(out _));
            Assert.IsTrue(Script().IsValid(out string none), none);
        }

        [Test]
        public void FigureKeyHash_IsFnv1a_AndZeroForNoKey()
        {
            Assert.AreEqual(0u, FigureKeyHash.Of(null));
            Assert.AreEqual(0u, FigureKeyHash.Of(string.Empty));
            Assert.AreEqual(0xE40C292Cu, FigureKeyHash.Of("a"), "FNV-1a of \"a\"");
            Assert.AreEqual(0xBF9CF968u, FigureKeyHash.Of("foobar"), "FNV-1a of \"foobar\"");
        }

        // ================================================================ harness

        private static bool AimsDiffer(in AnglerScript script, int seedA, uint keyA, int seedB, uint keyB)
        {
            double castIn = script.CastBackSeconds + script.CastSeconds * 0.5d;
            for (long slot = 0; slot < 8; slot++)
            {
                AnglerBeat a = At(in script, AnglerTimeline.SlotStartSeconds(in script, seedA, keyA, slot) + castIn, seed: seedA, key: keyA);
                AnglerBeat b = At(in script, AnglerTimeline.SlotStartSeconds(in script, seedB, keyB, slot) + castIn, seed: seedB, key: keyB);
                Assert.AreEqual(FishingPhase.Cast, a.Phase, "harness: not sampled in the cast");
                Assert.AreEqual(FishingPhase.Cast, b.Phase, "harness: not sampled in the cast");
                if (a.State.CastAimX != b.State.CastAimX || a.State.CastAimY != b.State.CastAimY) return true;
            }
            return false;
        }

        private static List<FishingPhase> PhasesOfSlot(in AnglerScript script, long slot, bool hasCatch,
                                                       out List<string> keys, ShoreCatch? shoreCatch = null)
        {
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, slot);
            var phases = new List<FishingPhase>();
            keys = new List<string>();
            bool first = true;
            string lastKey = null;
            for (double t = start; t < start + script.SlotSeconds; t += Step)
            {
                AnglerBeat b = At(in script, t, hasCatch, shoreCatch: shoreCatch);
                Assert.AreEqual(slot, b.Slot, "harness: sampled outside the slot");
                if (phases.Count == 0 || phases[phases.Count - 1] != b.Phase) phases.Add(b.Phase);
                if (first || !string.Equals(lastKey, b.StateKey, StringComparison.Ordinal)) keys.Add(b.StateKey);
                lastKey = b.StateKey;
                first = false;
                if (b.InSession)
                {
                    Assert.That(b.SecondsInPhase, Is.GreaterThanOrEqualTo(0f), $"{b.Phase}: negative seconds in the phase");
                    Assert.AreEqual(t - b.PhaseStartSeconds, b.SecondsInPhase, 1e-3d, $"{b.Phase}: the phase start disagrees");
                }
            }
            return phases;
        }

        private static string Fingerprint(in AnglerBeat b)
        {
            FishingState s = b.State;
            return string.Join("|", b.Slot, Bits(b.SlotStartSeconds), b.Phase, Bits(b.PhaseStartSeconds), Bits(b.SecondsInPhase),
                               b.StateKey, Bits(b.BeatStartSeconds), Bits(b.Scrub01), s.Phase, Bits(s.Tension01),
                               Bits(s.Landing01), s.FishId, Bits(s.WeightKg), Bits(s.Depth01), Bits(s.RodBend01),
                               Bits(s.FishOffsetX), Bits(s.FishOffsetY), Bits(s.CastCharge01), Bits(s.CastAimX),
                               Bits(s.CastAimY), Bits(s.FishRunDirX), Bits(s.FishRunDirY), Bits(s.Size01));
        }

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);
        private static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
    }
}
