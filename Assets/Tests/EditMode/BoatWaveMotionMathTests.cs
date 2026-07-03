using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The B2 wave-motion decomposition (<see cref="BoatWaveMotionMath"/>, ADR 0018): the sampled
    /// surface slope projected onto the hull's own axes. These pin the owner's ask, verbatim — a
    /// wave to the BEAM rolls the vessel (and only rolls it), a sea to the BOW pitches bow and
    /// stern (and only pitches), a quartering sea does both, glass is dead still, strength 0 is
    /// identically zero — plus the determinism guard on the full WaveMath → Decompose pipeline.
    /// All pure math, headless, no physics step, no allocation.
    /// </summary>
    public class BoatWaveMotionMathTests
    {
        private const float Eps = 1e-5f;

        private static readonly Vector2 North = Vector2.up;      // heading +Y
        private static readonly Vector2 East = Vector2.right;    // starboard of North

        // ---- the owner's ask, axis by axis ---------------------------------------------------

        [Test]
        public void BeamSea_PureRoll_ZeroPitch()
        {
            // Heading north; the surface rises to the EAST (slope ⊥ heading) — a wave to the beam.
            var m = BoatWaveMotionMath.Decompose(new Vector2(0.4f, 0f), 0f, North);
            Assert.AreEqual(0f, m.Pitch, Eps, "a pure beam sea must not pitch the bow");
            Assert.AreEqual(0.4f, m.Roll, Eps, "the beam slope lands entirely in roll (starboard-up positive)");
        }

        [Test]
        public void HeadSea_PurePitch_ZeroRoll()
        {
            // Heading north; the surface rises AHEAD (slope ∥ heading) — sailing through the waves to the bow.
            var m = BoatWaveMotionMath.Decompose(new Vector2(0f, 0.4f), 0f, North);
            Assert.AreEqual(0.4f, m.Pitch, Eps, "the bow-axis slope lands entirely in pitch (bow riding up the face)");
            Assert.AreEqual(0f, m.Roll, Eps, "a pure head sea must not roll the deck");
        }

        [Test]
        public void QuarteringSea_BothPitchAndRoll()
        {
            // Slope at 45° off the bow: both axes read, evenly.
            Vector2 slope = new Vector2(0.3f, 0.3f);
            var m = BoatWaveMotionMath.Decompose(slope, 0f, North);
            Assert.AreEqual(0.3f, m.Pitch, Eps, "quartering: the bow component pitches");
            Assert.AreEqual(0.3f, m.Roll, Eps, "quartering: the beam component rolls");
        }

        [Test]
        public void TurningTheBoat_RetargetsTheSameWave_RollBecomesPitch()
        {
            // THE mechanic: the SAME wave, read on the beam vs on the bow, as the player turns.
            Vector2 slope = new Vector2(0.5f, 0f);  // surface rises to the east

            var onTheBeam = BoatWaveMotionMath.Decompose(slope, 0f, North);        // wave abeam → roll
            var bowIntoIt = BoatWaveMotionMath.Decompose(slope, 0f, East);         // turned east → head sea

            Assert.AreEqual(0.5f, onTheBeam.Roll, Eps, "heading north: the easterly slope is a beam sea");
            Assert.AreEqual(0f, onTheBeam.Pitch, Eps);
            Assert.AreEqual(0.5f, bowIntoIt.Pitch, Eps, "turned bow-into-it: the same slope is now a head sea");
            Assert.AreEqual(0f, bowIntoIt.Roll, Eps, "…and the roll trades away entirely");
        }

        [Test]
        public void Bob_IsTheWaveHeight()
        {
            var m = BoatWaveMotionMath.Decompose(Vector2.zero, 0.75f, North);
            Assert.AreEqual(0.75f, m.Bob, Eps, "bob = the surface height under the hull");
        }

        // ---- glass, strength, conventions ----------------------------------------------------

        [Test]
        public void Glass_ZeroSample_IdenticallyZeroMotion()
        {
            var m = BoatWaveMotionMath.Decompose(Vector2.zero, 0f, North);
            Assert.AreEqual(0f, m.Pitch, 0f, "glass is sacred: exactly zero, no floor");
            Assert.AreEqual(0f, m.Roll, 0f);
            Assert.AreEqual(0f, m.Bob, 0f);
        }

        [Test]
        public void GlassSeaState_FullPipeline_DeadStill()
        {
            // The whole B2 read at seaState01 = 0: TrainsFrom flattens every amplitude to exactly 0,
            // Sample returns the flat surface, the decomposition is identically zero. Glass = glass.
            var trains = WaveMath.TrainsFrom(new Vector2(6f, 2f), 0f, WaveFieldSettings.Default);
            WaveSample wave = WaveMath.Sample(new Vector2(120f, -45f), 3600.0, in trains);
            var m = BoatWaveMotionMath.Decompose(wave.Slope, wave.Height, new Vector2(0.6f, 0.8f));
            Assert.AreEqual(0f, m.Pitch, 0f, "a dead-calm sea moves nothing");
            Assert.AreEqual(0f, m.Roll, 0f);
            Assert.AreEqual(0f, m.Bob, 0f);
        }

        [Test]
        public void StrengthZero_IsTheOffSwitch_IdenticallyZero()
        {
            var m = BoatWaveMotionMath.Decompose(new Vector2(0.7f, -0.3f), 1.2f, North, strength: 0f);
            Assert.AreEqual(0f, m.Pitch, 0f, "master strength 0 = off, exactly");
            Assert.AreEqual(0f, m.Roll, 0f);
            Assert.AreEqual(0f, m.Bob, 0f);
        }

        [Test]
        public void Strength_ScalesAllThreeAxesLinearly()
        {
            Vector2 slope = new Vector2(0.2f, 0.4f);
            var one = BoatWaveMotionMath.Decompose(slope, 0.5f, North, strength: 1f);
            var half = BoatWaveMotionMath.Decompose(slope, 0.5f, North, strength: 0.5f);
            Assert.AreEqual(one.Pitch * 0.5f, half.Pitch, Eps);
            Assert.AreEqual(one.Roll * 0.5f, half.Roll, Eps);
            Assert.AreEqual(one.Bob * 0.5f, half.Bob, Eps);
        }

        [Test]
        public void NegativeStrength_ClampsToZero_NeverInverts()
        {
            var m = BoatWaveMotionMath.Decompose(new Vector2(0.5f, 0.5f), 1f, North, strength: -2f);
            Assert.AreEqual(0f, m.Pitch, 0f, "strength is clamped ≥ 0 — a mis-tuned negative never mirrors the sea");
            Assert.AreEqual(0f, m.Roll, 0f);
            Assert.AreEqual(0f, m.Bob, 0f);
        }

        [Test]
        public void Starboard_OfNorth_IsEast_RightHanded()
        {
            Vector2 stbd = BoatWaveMotionMath.Starboard(North);
            Assert.AreEqual(1f, stbd.x, Eps, "starboard of a north heading is east");
            Assert.AreEqual(0f, stbd.y, Eps);
            // And it stays exactly perpendicular for an arbitrary heading.
            Vector2 h = new Vector2(0.6f, -0.8f);
            Assert.AreEqual(0f, Vector2.Dot(h, BoatWaveMotionMath.Starboard(h)), Eps, "starboard ⊥ heading, always");
        }

        [Test]
        public void UnnormalizedHeading_ReadsSameAsUnit_MagnitudeMustNotLeakIn()
        {
            // transform.up is unit-length, but the math must not care (a scaled rig, a raw velocity).
            Vector2 slope = new Vector2(0.3f, 0.1f);
            var unit = BoatWaveMotionMath.Decompose(slope, 0.2f, new Vector2(0f, 1f));
            var scaled = BoatWaveMotionMath.Decompose(slope, 0.2f, new Vector2(0f, 12.5f));
            Assert.AreEqual(unit.Pitch, scaled.Pitch, Eps, "heading is normalized inside — magnitude never scales the read");
            Assert.AreEqual(unit.Roll, scaled.Roll, Eps);
        }

        [Test]
        public void ZeroHeading_FallsBackDeterministically_NeverNaN()
        {
            var m = BoatWaveMotionMath.Decompose(new Vector2(0.4f, 0.2f), 0.3f, Vector2.zero);
            Assert.IsFalse(float.IsNaN(m.Pitch) || float.IsNaN(m.Roll) || float.IsNaN(m.Bob), "never NaN");
            // The defined fallback is +Y (north) — the WaveTrain / HeadingDegreesFromBow convention.
            var north = BoatWaveMotionMath.Decompose(new Vector2(0.4f, 0.2f), 0.3f, North);
            Assert.AreEqual(north.Pitch, m.Pitch, Eps, "zero heading reads as the +Y fallback, deterministically");
            Assert.AreEqual(north.Roll, m.Roll, Eps);
        }

        // ---- determinism (rule 5) --------------------------------------------------------------

        [Test]
        public void FullPipeline_SameInputs_SameMotion_Forever()
        {
            // (wind, seaState01, settings) → trains → sample → decompose, twice: bit-identical.
            Vector2 wind = new Vector2(-4.2f, 7.1f);
            Vector2 pos = new Vector2(310.5f, -12.25f);
            Vector2 heading = new Vector2(0.6f, 0.8f);
            const float sea = 0.65f;
            const double t = 12345.678;

            var trainsA = WaveMath.TrainsFrom(wind, sea, WaveFieldSettings.Default);
            WaveSample waveA = WaveMath.Sample(pos, t, in trainsA);
            var a = BoatWaveMotionMath.Decompose(waveA.Slope, waveA.Height, heading);

            var trainsB = WaveMath.TrainsFrom(wind, sea, WaveFieldSettings.Default);
            WaveSample waveB = WaveMath.Sample(pos, t, in trainsB);
            var b = BoatWaveMotionMath.Decompose(waveB.Slope, waveB.Height, heading);

            Assert.AreEqual(a.Pitch, b.Pitch, 0f, "deterministic: same inputs, same pitch, bit-exact");
            Assert.AreEqual(a.Roll, b.Roll, 0f);
            Assert.AreEqual(a.Bob, b.Bob, 0f);
        }

        [Test]
        public void FullPipeline_ModerateSea_ActuallyMoves()
        {
            // Guard against a silently-dead feature: a real sea on a real heading produces motion.
            var trains = WaveMath.TrainsFrom(new Vector2(8f, 0f), 0.6f, WaveFieldSettings.Default);
            bool anyMotion = false;
            for (int i = 0; i < 8 && !anyMotion; i++)
            {
                WaveSample wave = WaveMath.Sample(new Vector2(i * 3.7f, i * 1.9f), 100.0 + i * 0.9, in trains);
                var m = BoatWaveMotionMath.Decompose(wave.Slope, wave.Height, North);
                anyMotion = Mathf.Abs(m.Pitch) > 1e-4f || Mathf.Abs(m.Roll) > 1e-4f || Mathf.Abs(m.Bob) > 1e-4f;
            }
            Assert.IsTrue(anyMotion, "a moderate sea must rock the boat somewhere along its track");
        }
    }
}
