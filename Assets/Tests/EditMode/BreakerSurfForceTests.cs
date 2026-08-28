using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The teeth (ADR 0040, PR 3): broken water shoves a hull shoreward, and the pocket slews her.</b>
    ///
    /// <para>The force rides the SAME channel as the swell — one <c>SeakeepingForce</c>, one
    /// <c>AddForce</c>, one <c>AddTorque</c> — because the charter is explicit that there is no second
    /// force path. What is new is the term, not the plumbing.</para>
    ///
    /// <para><b>⭐ The most important test in this file is
    /// <see cref="TheSurfPushes_WhereTheSwellsOwnExposureReadsZERO"/>.</b> It guards a trap that would
    /// have shipped the whole feature dead: the swell's exposure is a DEPTH RAMP that treats shallow
    /// water as sheltered, and at the shipped tuning it is exactly 0 at the break depth. Every unit test
    /// of the shove would still have passed while the boat never felt a thing.</para>
    /// </summary>
    public class BreakerSurfForceTests
    {
        private const float G = 9.81f;

        /// <summary>A 1:25 sandy shoal rising toward +X: shore-normal is +X, so that is the way the surf
        /// runs and the way a hull in it must be pushed.</summary>
        private sealed class SandyShoal : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 0.04f * worldPos.x - 3f;
        }

        private sealed class FlatDeepBed : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => -30f;
        }

        private static WaveTrain Swell(float amplitude = 0.5f, float wavelength = 18f)
            => new WaveTrain(Vector2.right, wavelength, amplitude, 0f, G);

        private static SeakeepingSettings Settings => SeakeepingSettings.Default;
        private static BreakerSettings Breakers => BreakerSettings.Default;

        /// <summary>A lively small boat, the way <c>ResponseFrom</c> builds one.</summary>
        private static SeakeepingResponse Dory => SeakeepingForcesMath.ResponseFrom(1f, 1f, 0f);
        private static SeakeepingResponse Trader => SeakeepingForcesMath.ResponseFrom(4f, 1f, 0f);

        private static SurfState SurfAt(float x, float waterLevel = 0f, ITidalTerrain terrain = null)
        {
            terrain = terrain ?? new SandyShoal();
            var train = Swell();
            var settings = Breakers;
            BreakerContour contour = BreakerMath.ContourFor(in train, 1f, in settings);
            return BreakerMath.SurfAt(new Vector2(x, 0f), waterLevel, terrain, in contour, 1f,
                                      2f * train.Amplitude, train.Wavelength, in settings);
        }

        /// <summary>Walk shoreward and return the first working-surf position shallower than a depth.</summary>
        private static float FirstXShallowerThan(float depthMeters)
        {
            for (float x = 0f; x < 120f; x += 0.25f)
            {
                SurfState s = SurfAt(x);
                if (s.IsWorking && s.DepthMeters < depthMeters) return x;
            }
            return float.NaN;
        }

        /// <summary>Walk shoreward and return the first position whose surf is actually working.</summary>
        private static float FirstWorkingSurfX()
        {
            for (float x = 0f; x < 120f; x += 0.25f)
                if (SurfAt(x).IsWorking) return x;
            return float.NaN;
        }

        // =========================================================================================
        //  ⭐ The trap that would have shipped the feature dead
        // =========================================================================================

        [Test]
        public void TheSurfPushes_WhereTheSwellsOwnExposureReadsZERO()
        {
            // ⭐⭐ THE REGRESSION GUARD THIS FILE EXISTS FOR.
            //
            // SeakeepingForcesMath.Exposure01 is a DEPTH RAMP: 0 in shallow water, 1 offshore, because
            // the open sea's swell is what it models and a hull tucked into the shallows is genuinely
            // sheltered from swell. Surf is the opposite phenomenon — it exists ONLY in shallow water.
            //
            // At the shipped tuning the break depth is ~0.92 m and ShelterDepthMeters is 1 m, so exposure
            // at the break line is EXACTLY ZERO. Had the surf shove been routed through it — which is
            // what "scaled by SeaState01 x exposure" reads like it asks for — the feature would have been
            // multiplied away precisely where it acts, silently, and every other test in this file would
            // still have passed.
            var settings = Settings;

            // ⚠ Sample INSIDE the surf zone, not at the gate's outer edge. The first position where the
            // surf is working at all sits at the OUTER gate depth (1.42 m here), which is still a little
            // deeper than the 1 m shelter depth — exposure reads 0.084 there, not 0. The trap bites
            // further in, where the bore is actually running, and that is where the claim belongs.
            float x = FirstXShallowerThan(settings.ShelterDepthMeters * 0.6f);
            Assert.IsFalse(float.IsNaN(x), "the shoal must have water well inside the shelter depth");

            SurfState surf = SurfAt(x);
            Assert.IsTrue(surf.IsWorking, $"the surf must be working at {surf.DepthMeters:F2} m");

            float exposure = SeakeepingForcesMath.Exposure01(
                surf.DepthMeters, settings.ShelterDepthMeters, settings.FullExposureDepthMeters);

            Assert.AreEqual(0f, exposure, 1e-4f,
                $"the swell's exposure at {surf.DepthMeters:F2} m must be 0 — if this ever stops being " +
                "true the trap below has changed shape and wants re-reading");

            SeakeepingForce push = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, in settings);
            Assert.Greater(push.Force.magnitude, 0f,
                "the surf must push in water the swell's exposure calls fully sheltered — that is the " +
                "whole point of the surf having its own gate");
        }

        // =========================================================================================
        //  The shove
        // =========================================================================================

        [Test]
        public void TheShovePointsShoreward()
        {
            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);
            SeakeepingForce push = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, Settings);

            // This shoal rises toward +X, so shore-normal is +X.
            Assert.Greater(push.Force.x, 0f, "the bore must push toward the shore");
            Assert.AreEqual(0f, push.Force.y, push.Force.magnitude * 0.05f,
                "and not sideways along a shore-parallel contour");
        }

        [Test]
        public void TheShoveDies_AsTheBoreRunsUpTheBeach()
        {
            // The whitewater's energy decays on a real clock, so a hull in dead foam at the top of the
            // beach is barely pushed while one in the boil takes the lot. That difference IS the age
            // model earning its keep at the force level.
            // ⚠ Measured against the shove's PEAK, not against the gate's outer edge. Walking shoreward,
            // `breaking` is still CLIMBING from 0 to 1 while the standing height and the whitewater are
            // already falling, so the first working position is not the strongest one — the first draft
            // compared the wrong pair and read a 35 % drop where it expected 50 %. The honest claim is
            // that the shove peaks somewhere in the boil and is spent by the top of the beach.
            float peak = 0f, peakDepth = 0f, last = 0f, lastDepth = 0f;
            for (float x = 0f; x < 120f; x += 0.25f)
            {
                SurfState s = SurfAt(x);
                if (!s.IsWorking) continue;
                float push = SeakeepingForcesMath.SurfShove(in s, Vector2.up, Dory, Settings).Force.magnitude;
                if (push > peak) { peak = push; peakDepth = s.DepthMeters; }
                last = push; lastDepth = s.DepthMeters;
            }

            Debug.Log($"[surf-force] the shove peaks at {peak:F1} in {peakDepth:F2} m and is {last:F1} " +
                      $"at the last working water ({lastDepth:F2} m).");

            Assert.Greater(peak, 0f, "there must be a shove in the boil");
            Assert.Less(last, peak * 0.5f,
                $"the shove must be spent by the top of the beach ({peak:F1} peak vs {last:F1} at the " +
                "shoreward end) — the bore's own age and its shrinking standing height both drive this");
        }

        [Test]
        public void TwoHullsOfDifferentMass_OrderCorrectly()
        {
            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);

            float dory = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, Settings).Force.magnitude;
            float trader = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Trader, Settings).Force.magnitude;

            Assert.Greater(dory, trader, "a light hull must be shoved harder than a heavy one");
            Assert.Greater(trader, 0f, "but the heavy one is not immune");
        }

        // =========================================================================================
        //  The broach
        // =========================================================================================

        [Test]
        public void ABowOnHull_IsPushedButNotSlewed()
        {
            // A shove straight up the bow (or dead astern) has no beam component, so there is nothing to
            // turn her. Being caught bow-on to the surf is the safe way to take it, and that falls out of
            // the geometry rather than being asserted anywhere.
            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);

            // Heading +X = straight into the shoreward push.
            SeakeepingForce push = SeakeepingForcesMath.SurfShove(in surf, Vector2.right, Dory, Settings);

            Assert.Greater(push.Force.magnitude, 0f, "she is still pushed");
            Assert.AreEqual(0f, push.Torque, push.Force.magnitude * 0.02f, "but barely slewed");
        }

        [Test]
        public void ABeamOnHull_IsSlewed()
        {
            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);

            // Heading +Y = beam-on to a shoreward (+X) push.
            SeakeepingForce beamOn = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, Settings);
            SeakeepingForce bowOn = SeakeepingForcesMath.SurfShove(in surf, Vector2.right, Dory, Settings);

            Assert.Greater(Mathf.Abs(beamOn.Torque), Mathf.Abs(bowOn.Torque) + 1e-3f,
                "beam-on in the boil must slew her far harder than bow-on — that is the broach");
        }

        [Test]
        public void TheBroachTorqueFlips_WithTheSideTheSurfIsOn()
        {
            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);

            SeakeepingForce port = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, Settings);
            SeakeepingForce starboard = SeakeepingForcesMath.SurfShove(in surf, Vector2.down, Dory, Settings);

            Assert.AreNotEqual(Mathf.Sign(port.Torque), Mathf.Sign(starboard.Torque),
                "surf on one beam and surf on the other must slew her opposite ways");
        }

        [Test]
        public void TheBroachDial_CanBeTurnedOffWithoutTouchingTheShove()
        {
            var noBroach = Settings;
            noBroach.SurfBroachTorque = 0f;

            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);

            SeakeepingForce full = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, Settings);
            SeakeepingForce pushed = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, in noBroach);

            Assert.AreEqual(0f, pushed.Torque, "the broach dial must turn the slew off");
            Assert.AreEqual(full.Force, pushed.Force, "and leave the shove exactly alone");
        }

        // =========================================================================================
        //  The OFF paths — calm and sheltered water is untouched, by construction
        // =========================================================================================

        [Test]
        public void DeepWater_GlassAndDryGround_AllPushNothing()
        {
            var settings = Settings;
            var train = Swell();
            var breakers = Breakers;
            BreakerContour contour = BreakerMath.ContourFor(in train, 1f, in breakers);

            // Deep water: nothing breaks, so nothing pushes.
            var deep = BreakerMath.SurfAt(Vector2.zero, 0f, new FlatDeepBed(), in contour, 1f,
                                          2f * train.Amplitude, train.Wavelength, in breakers);
            Assert.AreEqual(SeakeepingForce.None.Force,
                            SeakeepingForcesMath.SurfShove(in deep, Vector2.up, Dory, in settings).Force,
                            "deep water pushes nothing");

            // Glass: no contour at all.
            var glass = new WaveTrain(Vector2.right, 18f, 0f, 0f, G);
            BreakerContour none = BreakerMath.ContourFor(in glass, 1f, in breakers);
            var calm = BreakerMath.SurfAt(new Vector2(50f, 0f), 0f, new SandyShoal(), in none, 1f, 0f, 18f, in breakers);
            Assert.IsFalse(calm.IsWorking, "glass breaks nowhere, so there is no surf state at all");
            Assert.AreEqual(SeakeepingForce.None.Force,
                            SeakeepingForcesMath.SurfShove(in calm, Vector2.up, Dory, in settings).Force,
                            "glass pushes nothing");

            // Dry ground.
            var aground = SurfAt(90f);   // past the shoreline on this shoal
            Assert.IsFalse(aground.IsWorking, "dry ground carries no surf");
        }

        [Test]
        public void BothSwitches_RestoreTodaysHandlingExactly()
        {
            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);

            var surfOff = Settings; surfOff.SurfEnabled = false;
            var allOff = Settings; allOff.Enabled = false;
            var noStrength = Settings; noStrength.SurfShoveStrength = 0f;

            foreach (var (settings, why) in new[]
            {
                (surfOff, "the surf switch"), (allOff, "the master seakeeping switch"),
                (noStrength, "a zeroed shove strength"),
            })
            {
                SeakeepingForce push = SeakeepingForcesMath.SurfShove(in surf, Vector2.up, Dory, in settings);
                Assert.AreEqual(Vector2.zero, push.Force, $"{why} must silence the shove");
                Assert.AreEqual(0f, push.Torque, $"{why} must silence the broach");
            }
        }

        [Test]
        public void AnInertHull_IsUnmoved()
        {
            float x = FirstWorkingSurfX();
            SurfState surf = SurfAt(x);
            SeakeepingForce push = SeakeepingForcesMath.SurfShove(in surf, Vector2.up,
                                                                  SeakeepingResponse.Inert, Settings);
            Assert.AreEqual(Vector2.zero, push.Force, "an inert hull takes no shove");
        }

        // =========================================================================================
        //  The tide moves the teeth too, and determinism
        // =========================================================================================

        [Test]
        public void TheTideMovesWhereTheSurfPushes()
        {
            // The same claim as the drawn surf, now at the force level: the shove is where the break is,
            // and the break is where the tide put it.
            float lowWaterPush = float.NaN, highWaterPush = float.NaN;
            var terrain = new SandyShoal();

            for (float x = 0f; x < 120f; x += 0.25f)
            {
                var s = SurfAt(x, -0.6f, terrain);
                if (s.IsWorking) { lowWaterPush = x; break; }
            }
            for (float x = 0f; x < 120f; x += 0.25f)
            {
                var s = SurfAt(x, 0.6f, terrain);
                if (s.IsWorking) { highWaterPush = x; break; }
            }

            Assert.IsFalse(float.IsNaN(lowWaterPush), "the surf must push somewhere at low water");
            Assert.IsFalse(float.IsNaN(highWaterPush), "and somewhere at high water");
            Assert.Greater(highWaterPush, lowWaterPush,
                "a rising tide must carry the shoved water further inshore, exactly as it carries the " +
                "drawn break line");
        }

        [Test]
        public void TheWholeChain_IsDeterministic()
        {
            float x = FirstWorkingSurfX();
            for (int i = 0; i < 8; i++)
            {
                SurfState a = SurfAt(x + i * 0.75f);
                SurfState b = SurfAt(x + i * 0.75f);
                Assert.AreEqual(a.Breaking01, b.Breaking01, "the surf state is bit-stable");
                Assert.AreEqual(a.Whitewater01, b.Whitewater01, "the surf state is bit-stable");

                var pa = SeakeepingForcesMath.SurfShove(in a, Vector2.up, Dory, Settings);
                var pb = SeakeepingForcesMath.SurfShove(in b, Vector2.up, Dory, Settings);
                Assert.AreEqual(pa.Force, pb.Force, "the shove is bit-stable");
                Assert.AreEqual(pa.Torque, pb.Torque, "the broach is bit-stable");
            }
        }

        [Test]
        public void NoOutputIsEverNaN_AcrossAHostileSweep()
        {
            var settings = Settings;
            foreach (float waterLevel in new[] { -60f, -0.001f, 0f, 60f })
            foreach (float x in new[] { -500f, 0f, 41.9f, 74f, 500f })
            foreach (var heading in new[] { Vector2.up, Vector2.right, Vector2.zero, new Vector2(1e-9f, 0f) })
            {
                SurfState surf = SurfAt(x, waterLevel);
                SeakeepingForce push = SeakeepingForcesMath.SurfShove(in surf, heading, Dory, in settings);
                Assert.IsFalse(float.IsNaN(push.Force.x) || float.IsNaN(push.Force.y), "force never NaN");
                Assert.IsFalse(float.IsNaN(push.Torque), "torque never NaN");
                Assert.IsFalse(float.IsInfinity(push.Force.magnitude), "force never infinite");
            }
        }

        [Test]
        public void TheDefaults_ShipOnAsTheCharterAsks()
        {
            var d = SeakeepingSettings.Default;
            Assert.IsTrue(d.SurfEnabled, "the surf's teeth ship ON (GameConfig toggle, default ON)");
            Assert.Greater(d.SurfShoveStrength, 0f, "with a shove");
            Assert.Greater(d.SurfBroachTorque, 0f, "and a broach");
        }
    }
}
