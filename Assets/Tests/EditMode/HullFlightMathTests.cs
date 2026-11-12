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

        // The game's own seas, by the wind that raises them and the sea state: (wind m/s, sea 0..1).
        // The winds are the ones the weather itself gives for each sea state (WeatherModel.WindStrengthFor):
        // its storm tops out at 14 m/s.
        static readonly (float wind, float sea)[] Seas = { (1.625f, 0.25f), (5.7f, 0.55f), (9.8f, 0.8f), (14f, 1f) };
        const int Breeze = 0, Moderate = 1, Strong = 2, Gale = 3;

        // A hull making `speed` m/s straight into the sea the wave field raises for that wind. The water
        // under her is the mean over the law's own footprint points, exactly as the boat's tick reads it.
        static Run Drive(float speed, float length, float halfBeam, float draught, int sea,
                         HullFlightSettings cfg, float seconds = 60f, bool keepRides = false)
        {
            HullHeaveResponse heave = HullHeaveResponseMath.Resolve(length, halfBeam, draught, 0.6f, G,
                                                                    HullWeightSettings.Default);
            Assert.IsTrue(heave.IsHonest, "the test hull must resolve a real heave spring");
            WaveFieldSettings field = WaveFieldSettings.Default;
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(Seas[sea].wind, 0f), Seas[sea].sea, in field);
            Vector2 heading = new Vector2(-1f, 0f);   // into the sea
            var state = default(HullFlightState);
            var run = new Run { Rides = keepRides ? new List<float>() : null };
            int ticks = Mathf.RoundToInt(seconds / Tick);
            for (int i = 0; i < ticks; i++)
            {
                double t = i * (double)Tick;
                Vector2 at = heading * (float)(speed * t);
                float surface = 0f;
                for (int n = 0; n < HullFlightMath.FootprintPoints; n++)
                    surface += WaveMath.Sample(at + heading * HullFlightMath.FootprintOffset(n, length), t, in trains).Height;
                surface /= HullFlightMath.FootprintPoints;
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

        static Run Dory(float speed, int sea) => Drive(speed, 4.5f, 0.8f, 0.3f, sea, HullFlightSettings.Default);
        static Run SmallFast(float speed, int sea) => Drive(speed, 3.2f, 0.6f, 0.3f, sea, HullFlightSettings.Default);
        static Run FastHull(float speed, int sea) => Drive(speed, 10f, 1.4f, 0.6f, sea, HullFlightSettings.Default);
        static Run WorkBoat(float speed, int sea) => Drive(speed, 14f, 2.2f, 1.2f, sea, HullFlightSettings.Default);

        // ---- no launch below the threshold ---------------------------------------------------------

        [Test]
        public void ADoryAtWalkingPace_NeverLeavesTheWater()
        {
            // Up to 2.3 m/s, as fast as the rowed dory goes. (The outboard dory flat out at 3 m/s takes one
            // hop of under 0.2 m a minute in a gale or a storm: recorded in the PR, not pinned here.)
            foreach (float speed in new[] { 0f, 0.8f, 1.5f, 2.3f })
                for (int sea = Breeze; sea <= Gale; sea++)
                {
                    Run run = Dory(speed, sea);
                    Assert.AreEqual(0, run.Launches, $"A dory at {speed} m/s left the water in sea {sea}.");
                    Assert.AreEqual(0f, run.PeakClearance, $"…and reported air under her at {speed} m/s in sea {sea}.");
                }
        }

        [Test]
        public void AHeavyWorkBoat_NeverLeavesTheWater_InAnySea()
        {
            foreach (float speed in new[] { 5f, 9f })
                for (int sea = Breeze; sea <= Gale; sea++)
                    Assert.AreEqual(0, WorkBoat(speed, sea).Launches, $"14 m of boat flew at {speed} m/s in sea {sea}.");
        }

        [Test]
        public void AFastHullOnFlatWater_NeverLeavesIt()
        {
            HullFlightSettings cfg = HullFlightSettings.Default;
            var state = default(HullFlightState);
            for (int i = 0; i < 2000; i++)
            {
                HullFlightStep step = HullFlightMath.Step(ref state, 0f, Tick, G, 5f, 0.6f, 0.3f, 3.2f, 20f, in cfg);
                Assert.AreEqual(HullFlightEvent.None, step.Event, "Glass launches nothing at any speed.");
                Assert.AreEqual(0f, step.ClearanceMeters);
            }
        }

        // ---- the same hull, the same sea: speed decides ---------------------------------------------

        [Test]
        public void TheSameHullOnTheSameSea_FliesFast_AndNotSlow()
        {
            Assert.AreEqual(0, SmallFast(4f, Moderate).Launches, "At 4 m/s she rides a moderate sea.");
            Assert.Greater(SmallFast(16f, Moderate).Launches, 0, "At 16 m/s the same sea throws her clear.");
            Assert.AreEqual(0, FastHull(6f, Gale).Launches, "At 6 m/s the 10 m hull rides a gale's sea.");
            Assert.Greater(FastHull(18f, Gale).Launches, 0, "At 18 m/s the same sea throws her clear.");
            Assert.AreEqual(0, FastHull(18f, Moderate).Launches, "A moderate sea is not enough to throw 10 m of hull.");
        }

        // ---- launch above it -----------------------------------------------------------------------

        [Test]
        public void AFastHullOverASteepSea_LeavesTheWater_AndComesBackDown()
        {
            Run run = SmallFast(16f, Strong);
            Assert.Greater(run.Launches, 3, "She should fly off crest after crest.");
            Assert.That(run.Landings, Is.InRange(run.Launches - 1, run.Launches), "Every flight ends in a landing.");
            Assert.Greater(run.PeakClearance, 0.2f, "There is real air under her keel.");
            Assert.Less(run.PeakClearance, 4f, "…and not an absurd amount of it.");
            Assert.Greater(run.LongestAir, HullFlightSettings.Default.MinAirSeconds, "A flight lasts.");
            Assert.Less(run.LongestAir, 2f, "…and ends: nobody hangs in the air.");
            Assert.Greater(run.WorstImpact, 0f, "At least one landing is a slam.");
        }

        [Test]
        public void AStiffSpringAndHeavyDamping_StayBounded()
        {
            // The integrator must not blow up however stiff the planing spring or heavy the damper.
            HullFlightSettings cfg = HullFlightSettings.Default;
            cfg.MinDampingRatio = 3f;
            cfg.MaxPlaningStiffness = 12f;
            cfg.PlaningStiffnessPerFroude = 6f;
            for (int sea = Breeze; sea <= Gale; sea++)
            {
                Run run = Drive(20f, 3.2f, 0.6f, 0.3f, sea, cfg);
                Assert.Less(run.PeakClearance, 6f, $"sea {sea}");
                Assert.IsFalse(float.IsNaN(run.PeakClearance), $"sea {sea}");
            }
        }

        [Test]
        public void SwitchedOff_NothingEverFlies()
        {
            HullFlightSettings off = HullFlightSettings.Default;
            off.Enabled = false;
            Run run = Drive(16f, 3.2f, 0.6f, 0.3f, Strong, off);
            Assert.AreEqual(0, run.Launches + run.Landings);
            Assert.AreEqual(0f, run.PeakClearance);
        }

        [Test]
        public void AHopShorterThanTheMinimum_RaisesNoEvent()
        {
            HullFlightSettings patient = HullFlightSettings.Default;
            patient.MinAirSeconds = 30f;   // longer than any flight this sea can give
            Run run = Drive(16f, 3.2f, 0.6f, 0.3f, Strong, patient);
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
            Assert.AreEqual(1f, DropFrom(6f, cfg).Impact01, "A drop of six metres is as hard as it gets.");
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
            Run a = Drive(16f, 3.2f, 0.6f, 0.3f, Strong, HullFlightSettings.Default, 20f, true);
            Run b = Drive(16f, 3.2f, 0.6f, 0.3f, Strong, HullFlightSettings.Default, 20f, true);
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
        public void SheIsNeverPushedDeeperThanTheBuryLimit()
        {
            HullFlightSettings cfg = HullFlightSettings.Default;
            var state = default(HullFlightState);
            HullFlightMath.Step(ref state, 0f, Tick, G, 5f, 0.6f, 0.3f, 3.2f, 0f, in cfg);
            HullFlightMath.Step(ref state, 4f, Tick, G, 5f, 0.6f, 0.3f, 3.2f, 0f, in cfg);   // the sea jumps
            Assert.GreaterOrEqual(state.RideMeters, 4f - cfg.MaxBuryDraughts * 0.3f - 1e-4f, "A risen sea carries her up with it.");
        }
    }
}
