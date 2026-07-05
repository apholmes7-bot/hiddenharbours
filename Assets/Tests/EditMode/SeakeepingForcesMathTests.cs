using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE SEA PUSHES THE BOAT — the B3 seakeeping force decomposition (<see cref="SeakeepingForcesMath"/>,
    /// ADR 0018). These pin the owner's ratified bite as pure, headless math: a HEAD sea retards headway,
    /// a BEAM sea shoves sideways AND yaws (demands steering), a FOLLOWING sea surges; the force is exactly
    /// zero on glass / in shelter / with the toggle off (calm sheltered handling UNCHANGED by construction),
    /// scales with SeaState01 (TIME) and exposure (PLACE), differs per hull, and is deterministic. All pure
    /// math — no physics step, no scene, no allocation.
    /// </summary>
    public class SeakeepingForcesMathTests
    {
        private const float Eps = 1e-5f;

        private static readonly Vector2 North = Vector2.up;    // heading +Y (bow)
        // starboard of North is East (+X) — the (y,−x) convention shared with BoatWaveMotionMath.

        private static SeakeepingSettings Settings() => SeakeepingSettings.Default;

        /// <summary>A lively hull (a dory: low mass factor, high liveliness) — the reference response.</summary>
        private static SeakeepingResponse Dory() => SeakeepingForcesMath.ResponseFrom(1f, 1f, 0f);

        // A canonical wave sample: build a helper that fabricates a WaveSample with a chosen slope/height.
        private static WaveSample WaveWith(Vector2 slope, float height) => new WaveSample(height, slope, 0f);

        // ---- the owner's ask, point of sail by point of sail -------------------------------------

        [Test]
        public void HeadSea_RetardsHeadway_ForceOpposesTheBow()
        {
            // Heading north; the surface rises AHEAD (slope along +Y) — a head sea. The boat is on the
            // up-slope, pushed back down it: the force must have a component ASTERN (−bow, i.e. −Y).
            var wave = WaveWith(new Vector2(0f, 0.5f), 0f);
            var f = SeakeepingForcesMath.Resolve(in wave, North, exposure01: 1f, seaState01: 1f,
                                                 in RefResponse, Settings());
            Assert.Less(f.Force.y, -Eps, "a head sea must push the boat astern — punching in costs headway");
            Assert.AreEqual(0f, f.Force.x, Eps, "a pure head sea has no beam (lateral) shove");
            Assert.AreEqual(0f, f.Torque, Eps, "a pure head sea does not yaw the boat");
        }

        [Test]
        public void BeamSea_ShovesSideways_AndYaws()
        {
            // Heading north; the surface rises to STARBOARD (slope along +X, ⊥ heading) — a beam sea.
            // Pushed down-slope: a lateral shove to PORT (−X), plus a yaw — the dangerous point of sail.
            var wave = WaveWith(new Vector2(0.5f, 0f), 0f);
            var f = SeakeepingForcesMath.Resolve(in wave, North, exposure01: 1f, seaState01: 1f,
                                                 in RefResponse, Settings());
            Assert.Less(f.Force.x, -Eps, "a beam sea shoves the hull sideways (down-slope, to port here)");
            Assert.AreEqual(0f, f.Force.y, Eps, "a pure beam sea has no head/following (along-bow) shove");
            Assert.AreNotEqual(0f, f.Torque, "a beam sea yaws the boat — holding course demands the helm");
        }

        [Test]
        public void FollowingSea_SurgesForward_AlongTheBow()
        {
            // Heading north; the surface rises ASTERN (slope along −Y) — a following sea overtaking. The
            // boat sits on the forward face and is surged AHEAD (+bow, +Y).
            var wave = WaveWith(new Vector2(0f, -0.5f), 0f);
            var f = SeakeepingForcesMath.Resolve(in wave, North, exposure01: 1f, seaState01: 1f,
                                                 in RefResponse, Settings());
            Assert.Greater(f.Force.y, Eps, "a following sea surges the boat forward");
            Assert.AreEqual(0f, f.Force.x, Eps, "a pure following sea has no beam shove");
        }

        [Test]
        public void HeadVsFollowing_WeightedDifferently_PunchingInCostsMore()
        {
            // Same |slope| on the bow axis, head vs following: the default weights make the head-sea shove
            // stronger than the following-sea surge (punching in is the harder point of sail).
            var head = SeakeepingForcesMath.Resolve(in WaveHead, North, 1f, 1f, in RefResponse, Settings());
            var follow = SeakeepingForcesMath.Resolve(in WaveFollow, North, 1f, 1f, in RefResponse, Settings());
            Assert.Greater(Mathf.Abs(head.Force.y), Mathf.Abs(follow.Force.y),
                "the default HeadSeaWeight > FollowingSeaWeight — punching into a sea costs more than surging along");
        }

        [Test]
        public void TurningTheBoat_RetargetsTheSameWave_BeamBecomesHead()
        {
            // THE mechanic: the SAME wave read on the beam (yaw + lateral) vs bow-on (retarding, no yaw).
            var wave = WaveWith(new Vector2(0.5f, 0f), 0f);   // surface rises to the east
            var onBeam = SeakeepingForcesMath.Resolve(in wave, North, 1f, 1f, in RefResponse, Settings());
            var bowInto = SeakeepingForcesMath.Resolve(in wave, Vector2.right, 1f, 1f, in RefResponse, Settings());

            Assert.AreNotEqual(0f, onBeam.Torque, "abeam: the wave yaws you");
            Assert.AreEqual(0f, bowInto.Torque, Eps, "bow-into-it: the yaw trades away — turn to face the sea and hold course");
        }

        // ---- glass / shelter / toggle: today's handling is preserved exactly -----------------------

        [Test]
        public void GlassSeaState_ZeroForce_CalmIsUnchanged()
        {
            // seaState01 = 0: even with a (stale) non-zero slope handed in, the sea bite is exactly 0.
            var wave = WaveWith(new Vector2(0.5f, 0.5f), 1f);
            var f = SeakeepingForcesMath.Resolve(in wave, North, exposure01: 1f, seaState01: 0f,
                                                 in RefResponse, Settings());
            Assert.AreEqual(Vector2.zero, f.Force, "glass never pushes — calm handling is unchanged");
            Assert.AreEqual(0f, f.Torque, 0f);
        }

        [Test]
        public void GlassSeaState_FullField_DeadCalm()
        {
            // The real pipeline at seaState01 = 0: TrainsFrom flattens all amplitudes, Sample is flat,
            // the force is identically zero. Glass = glass, all the way through.
            var trains = WaveMath.TrainsFrom(new Vector2(6f, 2f), 0f, WaveFieldSettings.Default);
            var wave = WaveMath.Sample(new Vector2(120f, -45f), 3600.0, in trains);
            var f = SeakeepingForcesMath.Resolve(in wave, North, 1f, 0f, in RefResponse, Settings());
            Assert.AreEqual(Vector2.zero, f.Force, "a dead-calm sea pushes nothing");
            Assert.AreEqual(0f, f.Torque, 0f);
        }

        [Test]
        public void FullShelter_ZeroForce_TheLeeOfLand()
        {
            // exposure = 0 (the sheltered lee): no force even in a full gale.
            var f = SeakeepingForcesMath.Resolve(in WaveBeam, North, exposure01: 0f, seaState01: 1f,
                                                 in RefResponse, Settings());
            Assert.AreEqual(Vector2.zero, f.Force, "the lee of land is sheltered — no environmental force");
            Assert.AreEqual(0f, f.Torque, 0f);
        }

        [Test]
        public void ToggleOff_ZeroForce_FeelIdenticalToToday()
        {
            var off = Settings();
            off.Enabled = false;
            var f = SeakeepingForcesMath.Resolve(in WaveBeam, North, exposure01: 1f, seaState01: 1f,
                                                 in RefResponse, off);
            Assert.AreEqual(Vector2.zero, f.Force, "toggle off = zero environmental force = today's handling");
            Assert.AreEqual(0f, f.Torque, 0f);
        }

        [Test]
        public void ZeroStrength_ZeroForce()
        {
            var s = Settings();
            s.Strength = 0f;
            var f = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in RefResponse, s);
            Assert.AreEqual(Vector2.zero, f.Force, "strength 0 = no bite (the same as off)");
        }

        [Test]
        public void InertHull_ZeroForce()
        {
            var f = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in SeakeepingResponse.Inert, Settings());
            Assert.AreEqual(Vector2.zero, f.Force, "an inert hull (null/zero response) is unmoved by the sea");
        }

        // ---- the two-axis modulation: force grows with SeaState01 (TIME) and exposure (PLACE) -------

        [Test]
        public void Force_GrowsWithSeaState()
        {
            var mild = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 0.3f, in RefResponse, Settings());
            var wild = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 0.9f, in RefResponse, Settings());
            Assert.Greater(wild.Force.magnitude, mild.Force.magnitude,
                "a wilder sea (higher SeaState01) shoves harder — TIME modulation");
        }

        [Test]
        public void Force_GrowsWithExposure()
        {
            var sheltered = SeakeepingForcesMath.Resolve(in WaveBeam, North, 0.25f, 1f, in RefResponse, Settings());
            var open = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in RefResponse, Settings());
            Assert.Greater(open.Force.magnitude, sheltered.Force.magnitude,
                "open water bites harder than a partial lee — PLACE modulation");
        }

        [Test]
        public void Force_ScalesLinearlyWithExposure()
        {
            var full = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in RefResponse, Settings());
            var half = SeakeepingForcesMath.Resolve(in WaveBeam, North, 0.5f, 1f, in RefResponse, Settings());
            Assert.AreEqual(full.Force.magnitude * 0.5f, half.Force.magnitude, Eps,
                "exposure scales the force linearly (a clean, predictable falloff for the owner to tune)");
        }

        // ---- exposure from depth (PLACE — the deterministic signal) --------------------------------

        [Test]
        public void Exposure_Deep_IsFullyExposed()
        {
            float e = SeakeepingForcesMath.Exposure01(20f, shelterDepthMeters: 1f, fullExposureDepthMeters: 6f);
            Assert.AreEqual(1f, e, Eps, "deep/offshore water = full open sea");
        }

        [Test]
        public void Exposure_Shallow_IsSheltered()
        {
            float e = SeakeepingForcesMath.Exposure01(0.5f, 1f, 6f);
            Assert.AreEqual(0f, e, Eps, "the shallow near-shore lee is sheltered");
        }

        [Test]
        public void Exposure_Midband_RampsSmoothly()
        {
            float e = SeakeepingForcesMath.Exposure01(3.5f, 1f, 6f);  // halfway through [1, 6]
            Assert.AreEqual(0.5f, e, Eps, "exposure ramps linearly between the shelter and full-exposure depths");
        }

        [Test]
        public void Exposure_OpenWater_InfiniteDepth_IsFullyExposed()
        {
            // BoatCrossing.DepthAt returns +Infinity when no seabed map is wired ("open water").
            float e = SeakeepingForcesMath.Exposure01(float.PositiveInfinity, 1f, 6f);
            Assert.AreEqual(1f, e, Eps, "open water (no height map) reads as fully exposed — the sea is felt");
        }

        [Test]
        public void Exposure_NaNDepth_FailsSafeToSheltered()
        {
            float e = SeakeepingForcesMath.Exposure01(float.NaN, 1f, 6f);
            Assert.AreEqual(0f, e, 0f, "a NaN depth never leaks into the force — fail safe to sheltered");
        }

        [Test]
        public void Exposure_DegenerateBand_NeverDividesByZeroOrInverts()
        {
            // Crossed/equal depths: a step at the higher depth, never NaN, never an inverted ramp.
            float below = SeakeepingForcesMath.Exposure01(2f, shelterDepthMeters: 6f, fullExposureDepthMeters: 6f);
            float above = SeakeepingForcesMath.Exposure01(9f, 6f, 6f);
            Assert.AreEqual(0f, below, "below the collapsed band = sheltered");
            Assert.AreEqual(1f, above, "at/above the collapsed band = exposed");
        }

        // ---- per-hull response: a dory corks about, a heavy hull shrugs ----------------------------

        [Test]
        public void PerHull_HeavyHull_ShrugsWhereADoryCorks()
        {
            var dory = SeakeepingForcesMath.ResponseFrom(seakeepingMassFactor: 1f, liveliness: 1f, damping: 0f);
            var trader = SeakeepingForcesMath.ResponseFrom(seakeepingMassFactor: 6f, liveliness: 0.5f, damping: 0f);

            var doryForce = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in dory, Settings());
            var traderForce = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in trader, Settings());

            Assert.Greater(doryForce.Force.magnitude, traderForce.Force.magnitude,
                "the same sea moves the light dory far more than the heavy trader (rule 2 — per-hull data)");
        }

        [Test]
        public void PerHull_ResponseScalesForceLinearly()
        {
            var a = SeakeepingForcesMath.ResponseFrom(1f, 1f, 0f);
            var b = SeakeepingForcesMath.ResponseFrom(1f, 2f, 0f);   // twice as lively
            var fa = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in a, Settings());
            var fb = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in b, Settings());
            Assert.AreEqual(fa.Force.magnitude * 2f, fb.Force.magnitude, Eps,
                "response multiplies the whole force (a linear per-hull dial)");
        }

        [Test]
        public void ResponseFrom_NeverDividesByZero_OnZeroMassFactor()
        {
            var r = SeakeepingForcesMath.ResponseFrom(seakeepingMassFactor: 0f, liveliness: 1f, damping: 0f);
            Assert.IsFalse(float.IsNaN(r.Response) || float.IsInfinity(r.Response), "a zero mass factor never divides by zero");
        }

        // ---- conventions & safety ------------------------------------------------------------------

        [Test]
        public void ZeroHeading_FallsBackDeterministically_NeverNaN()
        {
            var zero = SeakeepingForcesMath.Resolve(in WaveBeam, Vector2.zero, 1f, 1f, in RefResponse, Settings());
            var north = SeakeepingForcesMath.Resolve(in WaveBeam, North, 1f, 1f, in RefResponse, Settings());
            Assert.IsFalse(float.IsNaN(zero.Force.x) || float.IsNaN(zero.Force.y) || float.IsNaN(zero.Torque), "never NaN");
            Assert.AreEqual(north.Force.x, zero.Force.x, Eps, "a zero heading reads as the +Y fallback, deterministically");
            Assert.AreEqual(north.Force.y, zero.Force.y, Eps);
        }

        [Test]
        public void UnnormalizedHeading_ReadsSameAsUnit()
        {
            var unit = SeakeepingForcesMath.Resolve(in WaveBeam, new Vector2(0f, 1f), 1f, 1f, in RefResponse, Settings());
            var scaled = SeakeepingForcesMath.Resolve(in WaveBeam, new Vector2(0f, 12.5f), 1f, 1f, in RefResponse, Settings());
            Assert.AreEqual(unit.Force.x, scaled.Force.x, Eps, "heading magnitude never leaks into the force");
            Assert.AreEqual(unit.Force.y, scaled.Force.y, Eps);
            Assert.AreEqual(unit.Torque, scaled.Torque, Eps);
        }

        // ---- determinism (rule 5) ------------------------------------------------------------------

        [Test]
        public void FullPipeline_SameInputs_SameForce_Forever()
        {
            // (wind, seaState01, field) → trains → sample → resolve, twice: bit-identical.
            Vector2 wind = new Vector2(-4.2f, 7.1f);
            Vector2 pos = new Vector2(310.5f, -12.25f);
            Vector2 heading = new Vector2(0.6f, 0.8f);
            const float sea = 0.65f;
            const double t = 12345.678;
            var response = SeakeepingForcesMath.ResponseFrom(1.4f, 0.9f, 0.2f);

            var trainsA = WaveMath.TrainsFrom(wind, sea, WaveFieldSettings.Default);
            var waveA = WaveMath.Sample(pos, t, in trainsA);
            var a = SeakeepingForcesMath.Resolve(in waveA, heading, 0.8f, sea, in response, Settings());

            var trainsB = WaveMath.TrainsFrom(wind, sea, WaveFieldSettings.Default);
            var waveB = WaveMath.Sample(pos, t, in trainsB);
            var b = SeakeepingForcesMath.Resolve(in waveB, heading, 0.8f, sea, in response, Settings());

            Assert.AreEqual(a.Force.x, b.Force.x, 0f, "deterministic: same inputs, same force, bit-exact");
            Assert.AreEqual(a.Force.y, b.Force.y, 0f);
            Assert.AreEqual(a.Torque, b.Torque, 0f);
        }

        [Test]
        public void FullPipeline_ModerateExposedSea_ActuallyPushes()
        {
            // Guard against a silently-dead feature: a real exposed sea on a real heading pushes the boat.
            var trains = WaveMath.TrainsFrom(new Vector2(9f, 0f), 0.7f, WaveFieldSettings.Default);
            var response = Dory();
            bool anyForce = false;
            for (int i = 0; i < 8 && !anyForce; i++)
            {
                var wave = WaveMath.Sample(new Vector2(i * 3.7f, i * 1.9f), 100.0 + i * 0.9, in trains);
                var f = SeakeepingForcesMath.Resolve(in wave, North, 1f, 0.7f, in response, Settings());
                anyForce = f.Force.magnitude > 1e-3f || Mathf.Abs(f.Torque) > 1e-3f;
            }
            Assert.IsTrue(anyForce, "a moderate exposed sea must push the boat somewhere along its track");
        }

        // ---- shared fixtures -----------------------------------------------------------------------

        private static readonly SeakeepingResponse RefResponse = SeakeepingForcesMath.ResponseFrom(1f, 1f, 0f);
        private static readonly WaveSample WaveHead = new WaveSample(0f, new Vector2(0f, 0.5f), 0f);   // rises ahead
        private static readonly WaveSample WaveFollow = new WaveSample(0f, new Vector2(0f, -0.5f), 0f);// rises astern
        private static readonly WaveSample WaveBeam = new WaveSample(0f, new Vector2(0.5f, 0f), 0f);   // rises to stbd
    }
}
