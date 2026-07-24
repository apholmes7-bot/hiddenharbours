using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Shared closed-loop drivers for the v2 rod fight — the "competent pulse-and-steer"
    /// player and the "blind pin" (P5 cozy guarantees, design §7). Used by the sim tests, the
    /// controller tests and the authored-Def content sweep, so every layer judges fights by the same
    /// two hands.</summary>
    internal static class RodFightPolicies
    {
        /// <summary>The tension height at which the competent hand stops pulling and lets the line
        /// breathe — a POLICY choice of the simulated player, not a game tunable.</summary>
        public const float PulseTensionCap = 0.6f;

        /// <summary>Play the fight competently: PULL only in her slack windows while tension is low
        /// (the pulse), MAINTAIN through her runs, counter-steer once she's up. Returns the result
        /// (None = the cap elapsed undecided).</summary>
        public static FishFightResult PlayCompetent(RodFightSim sim, float maxSeconds = 180f, float dt = 0.02f)
        {
            for (float t = 0f; t < maxSeconds && !sim.IsOver; t += dt)
            {
                bool reeling = sim.Effort01 <= 0f && sim.Tension01 < PulseTensionCap;
                float steer = sim.Phase == RodFightPhase.Surface ? -1f : 0f;
                sim.Tick(dt, reeling, steer);
            }
            return sim.Result;
        }

        /// <summary>Just hold the reel to the wall, steer nowhere. Must SNAP (skill is a pulse, not a
        /// pin — the forgiving-cove invariant made observable).</summary>
        public static FishFightResult PlayBlindPin(RodFightSim sim, float maxSeconds = 60f, float dt = 0.02f)
        {
            for (float t = 0f; t < maxSeconds && !sim.IsOver; t += dt)
                sim.Tick(dt, reeling: true, steerAlignment: 0f);
            return sim.Result;
        }
    }

    /// <summary>
    /// Rod Fishing v2 Wave 3 — the fight sim (<see cref="RodFightSim"/>): the integration of
    /// <see cref="RodFightMath"/>'s rates over the run rhythm and the deep→surface arc. Pins: seeded
    /// determinism, the cozy closed loop (competent hand lands, blind pin snaps), the one-way
    /// Deep→Surface crossing, the lean counting from the hookup (owner's ruling 2026-07-23 — it used to
    /// pin the opposite, that Deep was steer-blind), and the Strength dial's direction.
    /// </summary>
    public class RodFightSimTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>A forgiving, template-shaped personality (satisfies both invariants at its
        /// effective strength).</summary>
        internal RodFightDef MakeDef(
            RodFightMovement pattern = RodFightMovement.Darter, float strength = 0.5f,
            float run = 1.6f, float slack = 1.2f, float jitter = 0.3f,
            float rise = 0.65f, float fall = 0.7f, float fill = 0.32f,
            float runPressure = 0.3f, float relief = 0.45f, float surfaceAt = 0.5f)
        {
            var def = ScriptableObject.CreateInstance<RodFightDef>();
            def.Id = "rodfight.test";
            def.MovementPattern = pattern;
            def.Strength = strength;
            def.StaminaCadence = new StaminaCadence { RunSeconds = run, SlackSeconds = slack, Jitter01 = jitter };
            def.tensionRisePerSec = rise;
            def.tensionFallPerSec = fall;
            def.landingFillPerSec = fill;
            def.runTensionPressure = runPressure;
            def.counterSteerRelief = relief;
            def.surfaceThreshold01 = surfaceAt;
            _spawned.Add(def);
            return def;
        }

        [Test]
        public void CompetentHand_LandsTheFish()
        {
            var sim = new RodFightSim(MakeDef(), new System.Random(777));
            Assert.AreEqual(FishFightResult.Landed, RodFightPolicies.PlayCompetent(sim),
                "pulse-and-steer must land the everyday fish (P5: daily fish stay forgiving)");
        }

        [Test]
        public void BlindPin_SnapsBeforeItLands()
        {
            var sim = new RodFightSim(MakeDef(), new System.Random(777));
            Assert.AreEqual(FishFightResult.Snapped, RodFightPolicies.PlayBlindPin(sim),
                "holding the reel to the wall must part the line first — skill is a pulse, not a pin");
        }

        [Test]
        public void SameSeed_ReplaysTheWholeFightBitForBit()
        {
            var a = new RodFightSim(MakeDef(jitter: 0.6f), new System.Random(31337));
            var b = new RodFightSim(MakeDef(jitter: 0.6f), new System.Random(31337));
            for (int i = 0; i < 6000 && !a.IsOver; i++)
            {
                bool reeling = a.Effort01 <= 0f && a.Tension01 < RodFightPolicies.PulseTensionCap;
                float steer = a.Phase == RodFightPhase.Surface ? -1f : 0f;
                a.Tick(0.02f, reeling, steer);
                b.Tick(0.02f, reeling, steer);
                Assert.AreEqual(a.Tension01, b.Tension01, $"tension diverged at tick {i}");
                Assert.AreEqual(a.Landing01, b.Landing01, $"landing diverged at tick {i}");
                Assert.AreEqual(a.Effort01, b.Effort01, $"rhythm diverged at tick {i}");
                Assert.AreEqual(a.FishOffset(2f, 0.45f), b.FishOffset(2f, 0.45f), $"choreography diverged at tick {i}");
            }
            Assert.AreEqual(a.Result, b.Result);
        }

        [Test]
        public void TheCrossing_IsOneWay_AtTheAuthoredThreshold()
        {
            var sim = new RodFightSim(MakeDef(surfaceAt: 0.4f), new System.Random(9));
            bool surfaced = false;
            for (int i = 0; i < 12000 && !sim.IsOver; i++)
            {
                bool reeling = sim.Effort01 <= 0f && sim.Tension01 < RodFightPolicies.PulseTensionCap;
                sim.Tick(0.02f, reeling, sim.Phase == RodFightPhase.Surface ? -1f : 0f);
                if (sim.Phase == RodFightPhase.Surface)
                {
                    surfaced = true;
                    Assert.GreaterOrEqual(sim.Landing01, 0.4f, "she surfaces AT the authored threshold");
                }
                if (surfaced)
                    Assert.AreEqual(RodFightPhase.Surface, sim.Phase,
                        "landing never falls, so the Deep→Surface crossing can never reverse");
            }
            Assert.IsTrue(surfaced, "a competently-fought fish must break the surface");
        }

        [Test]
        public void DeepPhase_TheLeanCountsFromTheHookup()
        {
            // Owner's ruling 2026-07-23: you lean against her ALWAYS, deep included. Two identical
            // seeded fights leaned hard OPPOSITE ways while she is still down — the one leaning against
            // her must be plainly better off before she ever shows herself. (The old contract asserted
            // these two stayed identical; the deep half is no longer a steer-free waiting game.)
            var with = new RodFightSim(MakeDef(), new System.Random(55));
            var against = new RodFightSim(MakeDef(), new System.Random(55));
            int deepTicks = 0;
            for (int i = 0; i < 4000; i++)
            {
                if (with.Phase == RodFightPhase.Surface || with.IsOver || against.IsOver) break;
                bool reeling = with.Effort01 <= 0f && with.Tension01 < RodFightPolicies.PulseTensionCap;
                with.Tick(0.02f, reeling, +1f);      // going WITH her run
                against.Tick(0.02f, reeling, -1f);   // leaning AGAINST it
                deepTicks++;
            }

            Assert.Greater(deepTicks, 0, "the fight must actually spend time deep for this to mean anything");
            Assert.Less(against.Tension01, with.Tension01,
                "leaning against her keeps the line safer than going with her — while she is still DEEP");
        }

        [Test]
        public void DeepPhase_HerRunHasADirectionToLeanAgainst()
        {
            // The lean is only playable if the fight publishes which way she's going from the hookup,
            // and if the line's entry point MOVES to show it. Both were zero while deep before.
            var sim = new RodFightSim(MakeDef(), new System.Random(7));
            sim.Tick(0.02f, false, 0f);
            Assert.AreEqual(RodFightPhase.Deep, sim.Phase, "she opens the fight deep");
            Assert.AreNotEqual(Vector2.zero, sim.DartDir, "she is running from the hookup — there IS an answer");

            Vector2 first = sim.FishOffset(2.5f, 0.45f);
            bool moved = false;
            for (int i = 0; i < 200 && sim.Phase == RodFightPhase.Deep && !sim.IsOver; i++)
            {
                sim.Tick(0.02f, false, 0f);
                if ((sim.FishOffset(2.5f, 0.45f) - first).sqrMagnitude > 1e-4f) { moved = true; break; }
            }
            Assert.IsTrue(moved, "the line's entry point works around while she's deep — that IS the read");
        }

        [Test]
        public void DeepRoam_IsSmallerThanTheSurfaceRoam()
        {
            // A fish well down moves the surface entry point a little; a fish on top moves it a lot.
            var deep = new RodFightSim(MakeDef(), new System.Random(21));
            for (int i = 0; i < 60; i++) deep.Tick(0.02f, false, 0f);
            Assert.AreEqual(RodFightPhase.Deep, deep.Phase);

            float cramped = deep.FishOffset(3f, 0.4f).magnitude;
            float full = deep.FishOffset(3f, 1f).magnitude;
            Assert.Less(cramped, full, "the deep fraction genuinely shrinks the excursion");
        }

        // ---- the Strength dial (Wave-3 carried thread) --------------------------------------

        [Test]
        public void EffectiveRunPressure_ScalesMonotonically_AndIsNeutralAtTheDefault()
        {
            Assert.AreEqual(0.35f, RodFightStrength.EffectiveRunPressure(0.35f, RodFightStrength.NeutralStrength01),
                1e-5f, "Strength 0.5 (the field default) runs the authored pressure exactly as written");
            float last = -1f;
            for (float s = 0f; s <= 1f; s += 0.1f)
            {
                float eff = RodFightStrength.EffectiveRunPressure(0.35f, s);
                Assert.Greater(eff, last, "a stronger fish always loads the line harder (the tooltip promise)");
                last = eff;
            }
            Assert.AreEqual(0f, RodFightStrength.EffectiveRunPressure(0.35f, 0f), 1e-5f);
            Assert.AreEqual(0.7f, RodFightStrength.EffectiveRunPressure(0.35f, 1f), 1e-5f);
        }

        [Test]
        public void StrongerFish_SnapsABlindPinSooner()
        {
            // Same personality, same seed, only the Strength dial differs: under the same blind pin,
            // the stronger fish's runs load the line harder, so the snap arrives earlier.
            int TicksToSnap(float strength)
            {
                var sim = new RodFightSim(MakeDef(strength: strength, jitter: 0f), new System.Random(1000));
                for (int i = 1; i <= 10000; i++)
                {
                    sim.Tick(0.02f, reeling: true, steerAlignment: 0f);
                    if (sim.IsOver) { Assert.AreEqual(FishFightResult.Snapped, sim.Result); return i; }
                }
                Assert.Fail("the blind pin never resolved");
                return -1;
            }

            Assert.Less(TicksToSnap(0.9f), TicksToSnap(0.1f),
                "the barn door must punish the same blind pin sooner than the schoolie");
        }

        // ---- the phase mapping (the ONE place — Wave-3 carried thread) ----------------------

        [Test]
        public void PhaseMap_RoundTrips_TheTwoFightPhases()
        {
            Assert.AreEqual(FishingPhase.FightDeep, RodFightPhases.ToFishingPhase(RodFightPhase.Deep));
            Assert.AreEqual(FishingPhase.FightSurface, RodFightPhases.ToFishingPhase(RodFightPhase.Surface));

            Assert.IsTrue(RodFightPhases.TryFromFishingPhase(FishingPhase.FightDeep, out var deep));
            Assert.AreEqual(RodFightPhase.Deep, deep);
            Assert.IsTrue(RodFightPhases.TryFromFishingPhase(FishingPhase.FightSurface, out var surface));
            Assert.AreEqual(RodFightPhase.Surface, surface);

            foreach (RodFightPhase p in System.Enum.GetValues(typeof(RodFightPhase)))
            {
                Assert.IsTrue(RodFightPhases.TryFromFishingPhase(RodFightPhases.ToFishingPhase(p), out var back));
                Assert.AreEqual(p, back, "the mapping must round-trip");
            }
        }

        [Test]
        public void PhaseMap_RefusesNonFightPhases_WithTheSafeDefault()
        {
            foreach (FishingPhase p in System.Enum.GetValues(typeof(FishingPhase)))
            {
                if (p == FishingPhase.FightDeep || p == FishingPhase.FightSurface) continue;
                Assert.IsFalse(RodFightPhases.TryFromFishingPhase(p, out var fight),
                    $"{p} is not a v2 fight phase");
                Assert.AreEqual(RodFightPhase.Deep, fight, "the safe, steer-ignoring default");
            }
        }
    }
}
