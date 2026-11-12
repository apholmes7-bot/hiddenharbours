using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// When a hull leaves the water and how hard she comes back (owner, 2026-11-12), pinned as pure
    /// maths. Each run drives one hull at a steady speed straight into a regular head sea on the
    /// physics tick and counts what the flight law reports. The hulls are the shapes of the fleet's
    /// ends: a dory (4.5 m, 0.3 m draught), a small fast craft (3.2 m, 0.3 m) and a 10 m fast hull.
    /// </summary>
    public class HullFlightMathTests
    {
        const float Tick = 0.02f;
        static readonly float G = WaveFieldSettings.Default.Gravity;

        struct Run
        {
            public int Launches, Landings;
            public float PeakClearance, WorstImpact, WorstClosing, LongestAir;
            public List<float> Rides;
        }

        // A hull making `speed` m/s into a head sea of the given amplitude and wavelength. The water
        // under her is the mean of three points of her length, as the boat's own tick reads it.
        static Run Drive(float speed, float length, float halfBeam, float draught,
                         float amplitude, float wavelength, HullFlightSettings cfg,
                         float seconds = 40f, bool keepRides = false)
        {
            HullHeaveResponse heave = HullHeaveResponseMath.Resolve(length, halfBeam, draught, 0.6f, G,
                                                                    HullWeightSettings.Default);
            Assert.IsTrue(heave.IsHonest, "the test hull must resolve a real heave spring");
            float k = 2f * Mathf.PI / wavelength;
            float encounter = k * (Mathf.Sqrt(G / k) + speed);
            float footprint = (1f + 2f * Mathf.Cos(k * length / 3f)) / 3f;
            var state = default(HullFlightState);
            var run = new Run { Rides = keepRides ? new List<float>() : null };
            int ticks = Mathf.RoundToInt(seconds / Tick);
            for (int i = 0; i < ticks; i++)
            {
                float surface = amplitude * footprint * Mathf.Sin(encounter * i * Tick);
                HullFlightStep step = HullFlightMath.Step(ref state, surface, Tick, G,
                    heave.NaturalFrequencyRadPerSec, heave.DampingRatio, draught, length, speed, in cfg);
                if (step.Event == HullFlightEvent.Launched) run.Launches++;
                if (step.Event == HullFlightEvent.Landed)
                {
                    run.Landings++;
                    run.WorstImpact = Mathf.Max(run.WorstImpact, step.Impact01);
                    run.WorstClosing = Mathf.Max(run.WorstClosing, step.ClosingSpeed);
                    run.LongestAir = Mathf.Max(run.LongestAir, step.AirSeconds);
                }
                run.PeakClearance = Mathf.Max(run.PeakClearance, step.ClearanceMeters);
                if (keepRides) run.Rides.Add(state.RideMeters);
            }
            return run;
        }

        static Run Dory(float speed, float amplitude, float wavelength)
            => Drive(speed, 4.5f, 0.8f, 0.3f, amplitude, wavelength, HullFlightSettings.Default);
        static Run SmallFast(float speed, float amplitude, float wavelength)
            => Drive(speed, 3.2f, 0.6f, 0.3f, amplitude, wavelength, HullFlightSettings.Default);
        static Run FastHull(float speed, float amplitude, float wavelength)
            => Drive(speed, 10f, 1.4f, 0.6f, amplitude, wavelength, HullFlightSettings.Default);

        // ---- no launch below the threshold ---------------------------------------------------------

        [Test]
        public void ADoryAtWalkingPace_NeverLeavesTheWater()
        {
            foreach (float speed in new[] { 0f, 0.8f, 1.5f })
            {
                Run sea = Dory(speed, 0.5f, 15f);       // a 1 m sea
                Run storm = Dory(speed, 1.0f, 25f);     // a 2 m sea
                Run chop = Dory(speed, 0.25f, 5f);      // a steep short chop
                Assert.AreEqual(0, sea.Launches + storm.Launches + chop.Launches,
                    $"A dory making {speed} m/s left the water: she follows the surface at that pace.");
                Assert.AreEqual(0f, Mathf.Max(sea.PeakClearance, Mathf.Max(storm.PeakClearance, chop.PeakClearance)),
                    "…and reports no air under her keel.");
            }
        }

        [Test]
        public void AFastHullOnFlatOrNearlyFlatWater_NeverLeavesIt()
        {
            Assert.AreEqual(0, SmallFast(16f, 0f, 15f).Launches, "Glass launches nothing at any speed.");
            Assert.AreEqual(0, SmallFast(16f, 0.03f, 3f).Launches, "A ripple launches nothing.");
            Assert.AreEqual(0, FastHull(18f, 0.03f, 3f).Launches, "A ripple launches nothing.");
        }

        [Test]
        public void TheSameHullOnTheSameSea_FliesFast_AndNotSlow()
        {
            Assert.AreEqual(0, SmallFast(4f, 0.25f, 8f).Launches, "At 4 m/s she rides the chop.");
            Assert.Greater(SmallFast(16f, 0.25f, 8f).Launches, 0, "At 16 m/s the same chop throws her clear.");
            Assert.AreEqual(0, FastHull(6f, 1f, 25f).Launches, "At 6 m/s the 10 m hull rides a 2 m sea.");
            Assert.Greater(FastHull(18f, 1f, 25f).Launches, 0, "At 18 m/s the same sea throws her clear.");
            Assert.AreEqual(0, FastHull(18f, 0.5f, 15f).Launches, "A 1 m sea is not enough to throw 10 m of hull.");
        }

        // ---- launch above it -----------------------------------------------------------------------

        [Test]
        public void AFastHullOverASteepSea_LeavesTheWater_AndComesBackDown()
        {
            Run run = SmallFast(16f, 0.5f, 15f);
            Assert.Greater(run.Launches, 3, "She should fly off crest after crest.");
            Assert.That(run.Landings, Is.InRange(run.Launches - 1, run.Launches), "Every flight ends in a landing.");
            Assert.Greater(run.PeakClearance, 0.2f, "There is real air under her keel.");
            Assert.Less(run.PeakClearance, 6f, "…and not an absurd amount of it.");
            Assert.Greater(run.LongestAir, HullFlightSettings.Default.MinAirSeconds, "A flight lasts.");
            Assert.Greater(run.WorstImpact, 0f, "At least one landing is a slam.");
        }

        [Test]
        public void SwitchedOff_NothingEverFlies()
        {
            HullFlightSettings off = HullFlightSettings.Default;
            off.Enabled = false;
            Run run = Drive(16f, 3.2f, 0.6f, 0.3f, 0.5f, 15f, off);
            Assert.AreEqual(0, run.Launches + run.Landings);
            Assert.AreEqual(0f, run.PeakClearance);
        }

        [Test]
        public void AHopShorterThanTheMinimum_RaisesNoEvent()
        {
            HullFlightSettings patient = HullFlightSettings.Default;
            patient.MinAirSeconds = 30f;   // longer than any flight this sea can give
            Run run = Drive(16f, 3.2f, 0.6f, 0.3f, 0.5f, 15f, patient);
            Assert.AreEqual(0, run.Launches + run.Landings, "No hop reached the minimum, so none counts.");
            Assert.AreEqual(0f, run.PeakClearance, "…and an uncounted hop reports no clearance.");
        }

        // ---- landing impact scales with fall speed -------------------------------------------------

        // A hull already in a counted flight, dropped onto flat water from a height.
        static HullFlightStep DropFrom(float metres, HullFlightSettings cfg)
        {
            const float draught = 0.3f;
            var state = new HullFlightState
            {
                Primed = true, KeelClear = true, Airborne = true, AirSeconds = 1f,
                RideMeters = draught * cfg.KeelClearFraction + metres,
            };
            for (int i = 0; i < 2000; i++)
            {
                HullFlightStep step = HullFlightMath.Step(ref state, 0f, Tick, G, 5f, 0.6f, draught, 3.2f, 0f, in cfg);
                if (step.Event == HullFlightEvent.Landed) return step;
            }
            Assert.Fail("she never came down");
            return default;
        }

        [Test]
        public void TheLandingImpact_GrowsWithTheSpeedSheFallsAt()
        {
            HullFlightSettings cfg = HullFlightSettings.Default;
            float lastClosing = 0f, lastImpact = -1f;
            foreach (float height in new[] { 0.05f, 0.3f, 0.8f, 1.5f, 3f })
            {
                HullFlightStep landed = DropFrom(height, cfg);
                float freeFall = Mathf.Sqrt(2f * G * height);
                Assert.AreEqual(freeFall, landed.ClosingSpeed, 0.25f + G * Tick,
                    $"From {height} m she should land at about the free-fall speed.");
                Assert.Greater(landed.ClosingSpeed, lastClosing, "A higher drop lands faster.");
                Assert.GreaterOrEqual(landed.Impact01, lastImpact, "…and never softer.");
                lastClosing = landed.ClosingSpeed;
                lastImpact = landed.Impact01;
            }
            Assert.AreEqual(0f, DropFrom(0.05f, cfg).Impact01, "A drop of a few centimetres is not a slam.");
            Assert.AreEqual(1f, DropFrom(3f, cfg).Impact01, "A drop of three metres is as hard as it gets.");
        }

        [Test]
        public void AHarderSlam_TakesMoreSpeedOffHer()
        {
            HullFlightSettings cfg = HullFlightSettings.Default;
            Assert.AreEqual(1f, HullFlightMath.SpeedKept01(0f, in cfg), "A soft landing costs nothing.");
            Assert.AreEqual(1f - cfg.SpeedShedAtFullSlam, HullFlightMath.SpeedKept01(1f, in cfg), 1e-6f);
            Assert.Less(HullFlightMath.SpeedKept01(0.8f, in cfg), HullFlightMath.SpeedKept01(0.3f, in cfg));
            Assert.AreEqual(0f, HullFlightMath.Impact01(cfg.SlamSpeedKnee, in cfg));
            Assert.AreEqual(1f, HullFlightMath.Impact01(cfg.SlamFullSpeed, in cfg));
        }

        [Test]
        public void HerBowLiftsWithTheAirUnderHer_UpToTheCeiling()
        {
            HullFlightSettings cfg = HullFlightSettings.Default;
            Assert.AreEqual(0f, HullFlightMath.NoseUpDegrees(0f, in cfg));
            Assert.AreEqual(0.5f * cfg.NoseUpDegreesPerMetre, HullFlightMath.NoseUpDegrees(0.5f, in cfg), 1e-5f);
            Assert.AreEqual(cfg.MaxNoseUpDegrees, HullFlightMath.NoseUpDegrees(50f, in cfg));
        }

        // ---- determinism ---------------------------------------------------------------------------

        [Test]
        public void TheSameSeaAndTheSameHull_GiveTheSameFlight_BitForBit()
        {
            Run a = Drive(16f, 3.2f, 0.6f, 0.3f, 0.5f, 15f, HullFlightSettings.Default, 20f, true);
            Run b = Drive(16f, 3.2f, 0.6f, 0.3f, 0.5f, 15f, HullFlightSettings.Default, 20f, true);
            Assert.AreEqual(a.Launches, b.Launches);
            Assert.AreEqual(a.Landings, b.Landings);
            Assert.AreEqual(a.Rides.Count, b.Rides.Count);
            for (int i = 0; i < a.Rides.Count; i++)
                if (a.Rides[i] != b.Rides[i]) Assert.Fail($"tick {i}: {a.Rides[i]:R} against {b.Rides[i]:R}");
            Assert.AreEqual(a.WorstClosing, b.WorstClosing);
        }

        [Test]
        public void AStateResetToDefault_WakesOnTheWater()
        {
            HullFlightSettings cfg = HullFlightSettings.Default;
            var state = default(HullFlightState);
            HullFlightStep step = HullFlightMath.Step(ref state, 0.7f, Tick, G, 5f, 0.6f, 0.3f, 3.2f, 12f, in cfg);
            Assert.AreEqual(HullFlightEvent.None, step.Event);
            Assert.AreEqual(0.7f, state.RideMeters, 1e-4f, "She starts where the water is, not at zero.");
            Assert.IsFalse(state.Airborne);
        }

        [Test]
        public void SheNeverRidesDeeperThanHerDraught()
        {
            HullFlightSettings cfg = HullFlightSettings.Default;
            var state = default(HullFlightState);
            HullFlightMath.Step(ref state, 0f, Tick, G, 5f, 0.6f, 0.3f, 3.2f, 0f, in cfg);
            HullFlightMath.Step(ref state, 4f, Tick, G, 5f, 0.6f, 0.3f, 3.2f, 0f, in cfg);   // the sea jumps
            Assert.GreaterOrEqual(state.RideMeters, 4f - 0.3f - 1e-4f, "A risen sea carries her up with it.");
        }
    }
}
