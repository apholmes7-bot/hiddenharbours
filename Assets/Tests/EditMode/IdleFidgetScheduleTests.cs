using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The idle fidgets' schedule</b> (<see cref="IdleFidgetSchedule"/>, <see cref="IdleFidgetClock"/>,
    /// <see cref="IdleFidgetDef"/>): pure functions of (world seed, game time, who), so every claim here is
    /// arithmetic and none needs a scene.
    ///
    /// <list type="bullet">
    /// <item>The same inputs give the same fidgets; the key moves with the seed, the skin and the figure.</item>
    /// <item>Two people side by side do not fidget in step.</item>
    /// <item>The gap between fidgets stays inside the library's bounds.</item>
    /// <item>The weights steer the choice, a weight of 0 is never chosen, and repeats are rare.</item>
    /// <item>A fidget starts only after the settle, and any move or press ends it the same step.</item>
    /// <item>A Clip fidget is drawable only on a skin that carries its clip; the gaze track and the clip frame
    /// read as authored.</item>
    /// <item>The per-frame calls allocate nothing (rule 7).</item>
    /// </list>
    ///
    /// <para>Skin null is "a pure schedule": every Look fidget is drawable, no Clip fidget is.</para>
    /// </summary>
    public sealed class IdleFidgetScheduleTests
    {
        private const double Step = 0.1;
        private const double Hour = 3600.0;

        private readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            IdleFidgetPreview.Clear();
            IdleFidgetInput.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            IdleFidgetPreview.Clear();
            IdleFidgetInput.Reset();
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // =================================================================== fixtures

        private IdleFidgetDef Look(string id, float weight, float seconds, float yaw = 60f, float up = 0f)
        {
            var d = ScriptableObject.CreateInstance<IdleFidgetDef>();
            _made.Add(d);
            d.Id = id;
            d.Weight = weight;
            d.DurationSeconds = seconds;
            d.Kind = IdleFidgetDef.Channel.Look;
            d.Gaze = new[]
            {
                new IdleFidgetDef.GazeKey { AtSeconds = 0f },
                new IdleFidgetDef.GazeKey { AtSeconds = seconds * 0.25f, YawDegrees = yaw, UpDegrees = up },
                new IdleFidgetDef.GazeKey { AtSeconds = seconds * 0.75f, YawDegrees = yaw, UpDegrees = up },
                new IdleFidgetDef.GazeKey { AtSeconds = seconds },
            };
            return d;
        }

        private IdleFidgetDef Clip(string id, float weight, float seconds, string clipKey)
        {
            var d = ScriptableObject.CreateInstance<IdleFidgetDef>();
            _made.Add(d);
            d.Id = id;
            d.Weight = weight;
            d.DurationSeconds = seconds;
            d.Kind = IdleFidgetDef.Channel.Clip;
            d.ClipKey = clipKey;
            return d;
        }

        private IdleFidgetLibraryDef Library(float minGap, float maxGap, float settle, params IdleFidgetDef[] fidgets)
        {
            var lib = ScriptableObject.CreateInstance<IdleFidgetLibraryDef>();
            _made.Add(lib);
            lib.Enabled = true;
            lib.MinGapSeconds = minGap;
            lib.MaxGapSeconds = maxGap;
            lib.SettleSeconds = settle;
            lib.LookDistanceMetres = 6f;
            lib.Fidgets = fidgets;
            return lib;
        }

        /// <summary>The shipped pace with three Look fidgets of the shipped lengths.</summary>
        private IdleFidgetLibraryDef Shipped() =>
            Library(4f, 11f, 2f,
                    Look("idle.a", 3f, 4.2f), Look("idle.b", 2f, 3.4f, 0f, 30f), Look("idle.c", 2f, 2.8f, 20f, -40f));

        /// <summary>Who played what over <paramref name="seconds"/>, sampled every <see cref="Step"/>, for a
        /// figure that has stood idle since long before.</summary>
        private static List<string> Sweep(IdleFidgetLibraryDef lib, uint key, double from, double seconds)
        {
            var seen = new List<string>();
            for (double t = from; t < from + seconds; t += Step)
            {
                IdleFidgetSchedule.TryActive(lib, null, key, t, double.MinValue / 4, out IdleFidgetMoment m);
                seen.Add(m.Active ? $"{m.Def.Id}@{m.StartSeconds:R}" : "-");
            }
            return seen;
        }

        // =================================================================== determinism

        [Test]
        public void TheSameWorldAndTheSameFigureReplayTheSameFidgets()
        {
            IdleFidgetLibraryDef lib = Shipped();
            uint key = IdleFidgetSchedule.KeyFor(1234, "charskin.villager_a", 77u);
            Assert.AreEqual(key, IdleFidgetSchedule.KeyFor(1234, "charskin.villager_a", 77u), "KeyFor is not pure");

            List<string> first = Sweep(lib, key, 5 * Hour, 600);
            List<string> second = Sweep(lib, key, 5 * Hour, 600);
            CollectionAssert.AreEqual(first, second, "Two sweeps of one schedule disagree.");
            Assert.Greater(first.FindAll(s => s != "-").Count, 0, "harness: nothing played in ten minutes");

            // A fresh clock stepped over the same span agrees with the schedule it reads.
            var clock = new IdleFidgetClock();
            int active = 0;
            for (double t = 0; t < 600; t += Step)
                if (clock.Step(true, t, lib, null, key)) active++;
            Assert.Greater(active, 0, "A clock idle for ten minutes never played a fidget.");
        }

        [Test]
        public void TheKeyMovesWithTheSeedTheSkinAndTheFigure()
        {
            uint a = IdleFidgetSchedule.KeyFor(1, "charskin.a", 10u);
            Assert.AreNotEqual(a, IdleFidgetSchedule.KeyFor(2, "charskin.a", 10u), "the world seed is ignored");
            Assert.AreNotEqual(a, IdleFidgetSchedule.KeyFor(1, "charskin.b", 10u), "the skin is ignored");
            Assert.AreNotEqual(a, IdleFidgetSchedule.KeyFor(1, "charskin.a", 11u), "the figure is ignored");
            Assert.DoesNotThrow(() => IdleFidgetSchedule.KeyFor(1, null, 0u), "a null skin id throws");
        }

        // =================================================================== never in step

        [Test]
        public void TwoPeopleSideBySideOfOneSkinDoNotFidgetInStep()
        {
            IdleFidgetLibraryDef lib = Shipped();
            const string skin = "charskin.villager_a";
            var keys = new uint[8];
            for (int i = 0; i < keys.Length; i++) keys[i] = IdleFidgetSchedule.KeyFor(99, skin, (uint)(1000 + i * 7919));

            double sum = 0;
            int pairs = 0;
            for (int i = 0; i < keys.Length; i++)
            for (int j = i + 1; j < keys.Length; j++)
            {
                Assert.AreNotEqual(IdleFidgetSchedule.SlotSeconds(lib, null, keys[i]),
                                   IdleFidgetSchedule.SlotSeconds(lib, null, keys[j]),
                                   $"figures {i} and {j} share one slot length, so they can lock in step");
                int both = 0, inStep = 0;
                for (double t = 2 * Hour; t < 3 * Hour; t += Step)
                {
                    bool a = IdleFidgetSchedule.TryActive(lib, null, keys[i], t, 0, out IdleFidgetMoment ma);
                    bool b = IdleFidgetSchedule.TryActive(lib, null, keys[j], t, 0, out IdleFidgetMoment mb);
                    if (!a || !b) continue;
                    both++;
                    if (ReferenceEquals(ma.Def, mb.Def) && Math.Abs(ma.StartSeconds - mb.StartSeconds) < 0.5) inStep++;
                }
                // In step = the same fidget begun within half a second of the other's. Chance alone gives
                // about a tenth of the time both fidget (they overlap only when their starts are near, and
                // there are three fidgets to share); one shared schedule would give all of it.
                double frac = both > 0 ? (double)inStep / both : 0;
                sum += frac;
                pairs++;
                Assert.Less(frac, 0.25,
                    $"figures {i} and {j} were in step for {frac:P0} of the time both fidgeted ({inStep}/{both} samples)");
            }
            Assert.Less(sum / pairs, 0.15, $"on average the pairs were in step {sum / pairs:P0} of the time both fidgeted");
        }

        // =================================================================== the pace

        [Test]
        public void TheGapBetweenFidgetsStaysInsideTheLibrarysBounds()
        {
            IdleFidgetLibraryDef lib = Shipped();
            double shortest = 2.8, longest = 4.2;
            const double eps = 1e-6;
            foreach (uint key in new[] { 1u, 0xDEADBEEFu, IdleFidgetSchedule.KeyFor(7, "charskin.x", 3u) })
            {
                for (long n = -5; n < 500; n++)
                {
                    Assert.IsTrue(IdleFidgetSchedule.TrySlot(lib, null, key, n, out IdleFidgetDef a, out double sa));
                    Assert.IsTrue(IdleFidgetSchedule.TrySlot(lib, null, key, n + 1, out IdleFidgetDef _, out double sb));
                    double gap = sb - (sa + a.DurationSeconds);
                    Assert.GreaterOrEqual(gap, lib.MinGapSeconds - eps, $"key {key:X8} slot {n}: gap {gap:0.000} s");
                    Assert.LessOrEqual(gap, lib.MaxGapSeconds + (longest - shortest) + eps,
                        $"key {key:X8} slot {n}: gap {gap:0.000} s");
                }
            }
        }

        [Test]
        public void TheSlotThatHoldsATimeIsTheOneThatCanPlayAtIt()
        {
            IdleFidgetLibraryDef lib = Shipped();
            uint key = IdleFidgetSchedule.KeyFor(5, "charskin.y", 9u);
            for (double t = 0; t < 900; t += 0.37)
            {
                if (!IdleFidgetSchedule.TryActive(lib, null, key, t, double.MinValue / 4, out IdleFidgetMoment m)) continue;
                Assert.IsTrue(IdleFidgetSchedule.TrySlotAt(lib, null, key, t, out long slot));
                Assert.AreEqual(slot, m.Slot, $"at {t:0.00} s the moment's slot is not the slot holding the time");
                Assert.IsTrue(IdleFidgetSchedule.TrySlot(lib, null, key, slot, out IdleFidgetDef d, out double start));
                Assert.AreSame(d, m.Def);
                Assert.AreEqual(start, m.StartSeconds, 1e-9);
                Assert.AreEqual(t - start, m.LocalSeconds, 1e-9);
                Assert.Less(m.LocalSeconds, m.Def.DurationSeconds);
            }
        }

        // =================================================================== the choice

        [Test]
        public void TheWeightsSteerTheChoiceAndAZeroWeightIsNeverChosen()
        {
            IdleFidgetDef heavy = Look("idle.heavy", 3f, 3f), light = Look("idle.light", 1f, 3f),
                          mid = Look("idle.mid", 2f, 3f), never = Look("idle.never", 0f, 3f);
            IdleFidgetLibraryDef lib = Library(4f, 11f, 2f, heavy, light, mid, never);
            var count = new Dictionary<IdleFidgetDef, int> { { heavy, 0 }, { light, 0 }, { mid, 0 }, { never, 0 } };
            IdleFidgetDef previous = null;
            int repeats = 0, slots = 4000;
            uint key = IdleFidgetSchedule.KeyFor(3, "charskin.z", 1u);
            for (long n = 0; n < slots; n++)
            {
                Assert.IsTrue(IdleFidgetSchedule.TrySlot(lib, null, key, n, out IdleFidgetDef d, out _));
                count[d]++;
                if (ReferenceEquals(d, previous)) repeats++;
                previous = d;
            }
            Assert.AreEqual(0, count[never], "a fidget of weight 0 was chosen");
            Assert.Greater(count[heavy], count[light], $"weight 3 chose {count[heavy]}, weight 1 chose {count[light]}");
            Assert.Greater(count[light], slots / 20, "the light fidget is starved");
            // The look-back is bounded (four slots), so a repeat can survive: about one slot in nine for
            // 3:2:1. Without the rule it would be the sum of the squared shares, about two in five.
            Assert.Less(repeats, slots / 6, $"{repeats} of {slots} slots repeated the slot before");
        }

        [Test]
        public void ALibrarySwitchedOffOrEmptyPlaysNothing()
        {
            IdleFidgetLibraryDef lib = Shipped();
            lib.Enabled = false;
            Assert.AreEqual(0, Sweep(lib, 1u, 0, 120).FindAll(s => s != "-").Count, "a library switched off played");
            Assert.AreEqual(0d, IdleFidgetSchedule.SlotSeconds(lib, null, 1u));

            IdleFidgetLibraryDef empty = Library(4f, 11f, 2f);
            Assert.AreEqual(0, Sweep(empty, 1u, 0, 120).FindAll(s => s != "-").Count, "an empty library played");
            Assert.IsFalse(IdleFidgetSchedule.TryActive(null, null, 1u, 10, 0, out _), "a null library played");
        }

        // =================================================================== settle and cancel

        [Test]
        public void AFidgetStartsOnlyAfterTheSettleSoItNeverPopsInMidTurn()
        {
            IdleFidgetLibraryDef lib = Shipped();
            uint key = IdleFidgetSchedule.KeyFor(8, "charskin.s", 2u);
            Assert.IsTrue(IdleFidgetSchedule.TrySlot(lib, null, key, 40, out IdleFidgetDef d, out double start));
            double mid = start + d.DurationSeconds * 0.5;

            Assert.IsTrue(IdleFidgetSchedule.TryActive(lib, null, key, mid, start - lib.SettleSeconds, out _),
                "idle exactly the settle before the start: the fidget should play");
            Assert.IsFalse(IdleFidgetSchedule.TryActive(lib, null, key, mid, start - lib.SettleSeconds + 0.01, out _),
                "idle less than the settle before the start: the fidget played");
            Assert.IsFalse(IdleFidgetSchedule.TryActive(lib, null, key, mid, mid, out _),
                "a person who stopped mid-slot was dropped into the middle of a fidget");
        }

        [Test]
        public void AnyMoveOrPressEndsTheFidgetTheSameStepAndTheNextWaitsForTheSettle()
        {
            IdleFidgetLibraryDef lib = Shipped();
            uint key = IdleFidgetSchedule.KeyFor(8, "charskin.s", 2u);
            Assert.IsTrue(IdleFidgetSchedule.TrySlot(lib, null, key, 40, out IdleFidgetDef d, out double start));
            double mid = start + d.DurationSeconds * 0.5;

            var clock = new IdleFidgetClock();
            for (double t = start - 30; t < mid; t += Step) clock.Step(true, t, lib, null, key);
            Assert.IsTrue(clock.Step(true, mid, lib, null, key), "harness: the fidget is not playing at its middle");
            Assert.AreSame(d, clock.Current.Def);

            Assert.IsFalse(clock.Step(false, mid + Step, lib, null, key), "a move did not end the fidget");
            Assert.IsFalse(clock.Current.Active);
            Assert.IsTrue(double.IsNaN(clock.IdleSinceSeconds), "the idle survived a move");

            for (double t = mid + 2 * Step; t < start + d.DurationSeconds; t += Step)
                Assert.IsFalse(clock.Step(true, t, lib, null, key), $"the cancelled fidget came back at {t - start:0.0} s in");

            // A clock that runs backwards (a load that seeks back) restarts the idle rather than trusting it.
            clock.Step(true, mid - 100, lib, null, key);
            Assert.AreEqual(mid - 100, clock.IdleSinceSeconds, 1e-9);
        }

        [Test]
        public void AControlNotedThisFrameOrTheOneBeforeCountsAsActing()
        {
            Assert.IsFalse(IdleFidgetInput.ActedRecently, "nothing was noted");
            IdleFidgetInput.Note();
            Assert.IsTrue(IdleFidgetInput.ActedRecently, "a note this frame did not count");
            IdleFidgetInput.Reset();
            Assert.IsFalse(IdleFidgetInput.ActedRecently, "Reset kept the note");
        }

        [Test]
        public void OnlyThePlainStandingIdleClipMayFidget()
        {
            Assert.IsTrue(IdleFidgetSchedule.IsStandingIdle(CharacterSkinStateMap.Idle));
            foreach (string other in new[] { "walk", "run", "carry", "carry_idle", "helm", "oars", "sit", "work", "Idle", "", null })
                Assert.IsFalse(IdleFidgetSchedule.IsStandingIdle(other), $"'{other}' was taken for standing idle");
        }

        [Test]
        public void APlateHoldOverridesTheScheduleOnlyForAnEligibleFigure()
        {
            IdleFidgetLibraryDef lib = Shipped();
            IdleFidgetDef held = lib.Fidgets[1];
            var clock = new IdleFidgetClock();
            IdleFidgetPreview.Hold(held, 1.25);
            Assert.IsTrue(clock.Step(true, 10, lib, null, 1u));
            Assert.AreSame(held, clock.Current.Def);
            Assert.AreEqual(1.25, clock.Current.LocalSeconds, 1e-9);
            Assert.IsFalse(clock.Step(false, 11, lib, null, 1u), "a hold made a walking figure fidget");
            IdleFidgetPreview.Clear();
            Assert.IsFalse(IdleFidgetPreview.TryGet(out _, out _));
        }

        // =================================================================== the channels

        [Test]
        public void AClipFidgetIsDrawableOnlyOnASkinThatCarriesItsClip()
        {
            IdleFidgetDef scratch = Clip("idle.scratch", 1f, 2f, "idleScratch");
            IdleFidgetDef look = Look("idle.look", 1f, 2f);
            var bare = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _made.Add(bare);

            Assert.IsFalse(scratch.AvailableFor(null), "a clip fidget drew with no skin");
            Assert.IsFalse(scratch.AvailableFor(bare), "a clip fidget drew on a skin without its clip");
            Assert.IsTrue(look.AvailableFor(null), "a look fidget is not drawable on a pure schedule");
            Assert.IsFalse(look.AvailableFor(bare), "a look fidget drew on a skin with no look");

            bare.Clips = new[]
            {
                new CharacterSkinDef.SkinClip { Anim = "idleScratch", State = "idleScratch", FrameCount = 1,
                                                FramesPerSecond = 6f, Keys = Array.Empty<CharacterSkinDef.BoneKey>() },
            };
            Assert.IsTrue(bare.TryGetClip("idleScratch", out _), "harness: the synthetic clip is not found by its key");
            Assert.IsTrue(scratch.AvailableFor(bare), "a clip fidget did not draw on a skin with its clip");

            // A library of clip fidgets only, on a skin without them, is an empty library.
            IdleFidgetLibraryDef lib = Library(4f, 11f, 2f, Clip("idle.hips", 1f, 3f, "idleHips"));
            Assert.AreEqual(0d, IdleFidgetSchedule.SlotSeconds(lib, bare, 1u));
        }

        [Test]
        public void TheGazeTrackReadsAsAuthored()
        {
            IdleFidgetDef d = Look("idle.g", 1f, 4f, 60f, -20f);   // keys at 0, 1, 3, 4
            Assert.IsTrue(d.TrySampleGaze(-1, out float y, out float u));
            Assert.AreEqual(0f, y); Assert.AreEqual(0f, u);
            Assert.IsTrue(d.TrySampleGaze(0.5, out y, out u));
            Assert.AreEqual(30f, y, 1e-4f); Assert.AreEqual(-10f, u, 1e-4f);
            Assert.IsTrue(d.TrySampleGaze(2.0, out y, out u));
            Assert.AreEqual(60f, y, 1e-4f); Assert.AreEqual(-20f, u, 1e-4f);
            Assert.IsTrue(d.TrySampleGaze(9.0, out y, out u));
            Assert.AreEqual(0f, y); Assert.AreEqual(0f, u);

            IdleFidgetDef c = Clip("idle.c", 1f, 2f, "idleShift");
            Assert.IsFalse(c.TrySampleGaze(1.0, out _, out _), "a clip fidget answered a gaze");

            // + yaw is the figure's right (+x), + up is up (+z), forward is +y.
            Vector3 eye = new Vector3(0f, 0f, 1.6f);
            Vector3 right = IdleFidgetDef.GazeTarget(eye, 90f, 0f, 6f);
            Vector3 ahead = IdleFidgetDef.GazeTarget(eye, 0f, 0f, 6f);
            Vector3 sky = IdleFidgetDef.GazeTarget(eye, 0f, 90f, 6f);
            Assert.AreEqual(6f, right.x, 1e-4f); Assert.AreEqual(0f, right.y, 1e-4f);
            Assert.AreEqual(6f, ahead.y, 1e-4f); Assert.AreEqual(1.6f, ahead.z, 1e-4f);
            Assert.AreEqual(7.6f, sky.z, 1e-4f);
        }

        [Test]
        public void AClipFidgetPlaysItsClipOnceAndHoldsTheLastFrame()
        {
            var clip = new CharacterSkinDef.SkinClip { FrameCount = 6, FramesPerSecond = 6f };
            Assert.AreEqual(0, IdleFidgetDef.ClipFrame(clip, -1));
            Assert.AreEqual(0, IdleFidgetDef.ClipFrame(clip, 0.1));
            Assert.AreEqual(1, IdleFidgetDef.ClipFrame(clip, 0.17));
            Assert.AreEqual(5, IdleFidgetDef.ClipFrame(clip, 0.9));
            Assert.AreEqual(5, IdleFidgetDef.ClipFrame(clip, 100), "the clip looped instead of holding");
            Assert.AreEqual(0, IdleFidgetDef.ClipFrame(new CharacterSkinDef.SkinClip { FrameCount = 1 }, 3));
        }

        // =================================================================== rule 7

        [Test]
        public void TheScheduleAndTheClockAllocateNothingPerFrame()
        {
            IdleFidgetLibraryDef lib = Shipped();
            uint key = IdleFidgetSchedule.KeyFor(1, "charskin.alloc", 1u);
            var clock = new IdleFidgetClock();
            double t = 1000;
            IdleFidgetMoment m;
            // Warm every path once so first-call costs are not counted.
            IdleFidgetSchedule.TryActive(lib, null, key, t, 0, out m);
            clock.Step(true, t, lib, null, key);
            if (m.Active) m.Def.TrySampleGaze(m.LocalSeconds, out _, out _);

            Assert.That(() => { GC.KeepAlive(new byte[1024]); },
                        UnityEngine.TestTools.Constraints.Is.AllocatingGCMemory(),
                        "harness: a known allocation must be detected before zero is believed");
            Assert.That(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    t += Step;
                    clock.Step(true, t, lib, null, key);
                    IdleFidgetMoment c = clock.Current;
                    if (c.Active) c.Def.TrySampleGaze(c.LocalSeconds, out _, out _);
                    IdleFidgetSchedule.TrySlotAt(lib, null, key, t, out _);
                }
                clock.Step(false, t + Step, lib, null, key);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}
