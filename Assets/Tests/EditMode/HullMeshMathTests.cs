using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The mesh-hull pose maths (ADR 0022 phase 4): the compass→rig-dir mapping — the CCW saga's
    /// last edge, pinned in numbers — and the rig's rock cycle as a continuous function of phase.
    /// Headless, deterministic, no scene.
    /// </summary>
    public class HullMeshMathTests
    {
        // ---- heading → dir units --------------------------------------------------------------

        [Test]
        public void HeadingToDirUnits_North_IsAlwaysDirZero()
        {
            Assert.AreEqual(0f, HullMeshMath.HeadingToDirUnits(0f, 0f, azimuthCounterClockwise: true), 1e-5f);
            Assert.AreEqual(0f, HullMeshMath.HeadingToDirUnits(0f, 0f, azimuthCounterClockwise: false), 1e-5f);
        }

        [Test]
        public void HeadingToDirUnits_CcwRig_NegatesTheCompass()
        {
            // The measured boat-rig convention: dir d depicts compass −45°·d, so compass h needs
            // dir −h/45. East (90°) = dir −2. THE defect class this project shipped five times —
            // if someone "simplifies" the sign, this is the test that names the bug.
            Assert.AreEqual(-2f, HullMeshMath.HeadingToDirUnits(90f, 0f, true), 1e-5f,
                "East on a CCW rig is dir −2 — a positive sign here mirrors the boat (stern-first at E/W)");
            Assert.AreEqual(-4f, HullMeshMath.HeadingToDirUnits(180f, 0f, true), 1e-5f);
            Assert.AreEqual(-6f, HullMeshMath.HeadingToDirUnits(270f, 0f, true), 1e-5f);
        }

        [Test]
        public void HeadingToDirUnits_CwRig_IsTheStraightScale()
        {
            Assert.AreEqual(2f, HullMeshMath.HeadingToDirUnits(90f, 0f, false), 1e-5f);
            Assert.AreEqual(1.5f, HullMeshMath.HeadingToDirUnits(67.5f, 0f, false), 1e-5f);
        }

        [Test]
        public void HeadingToDirUnits_IsContinuous_NotQuantised()
        {
            // The point of the mesh path: fractional headings are genuine poses, not bucket picks.
            Assert.AreEqual(-0.25f, HullMeshMath.HeadingToDirUnits(11.25f, 0f, true), 1e-5f,
                "one 32-facing step (11.25°) is a quarter dir unit — continuously representable");
            Assert.AreEqual(-0.1f, HullMeshMath.HeadingToDirUnits(4.5f, 0f, true), 1e-5f);
        }

        [Test]
        public void HeadingToDirUnits_ZeroHeadingOffset_ShiftsTheOrigin()
        {
            Assert.AreEqual(0f, HullMeshMath.HeadingToDirUnits(30f, 30f, true), 1e-5f);
            Assert.AreEqual(-1f, HullMeshMath.HeadingToDirUnits(75f, 30f, true), 1e-5f);
        }

        // ---- the rock cycle -------------------------------------------------------------------

        [Test]
        public void RockPose_Crest_PeaksRollAndHeave_PitchThroughZero()
        {
            // The rig's rockMotion at a = 90° (the crest, DoryRockMath's convention): roll = rollA,
            // heave = heaveA, pitch = pitchA·cos(90°) = 0. The lobster's own amplitudes.
            HullMeshMath.RockPose(90f, 2.8f, 1.6f, 1.2f, out float roll, out float pitch, out float heave);
            Assert.AreEqual(2.8f, roll, 1e-4f);
            Assert.AreEqual(0f, pitch, 1e-4f);
            Assert.AreEqual(1.2f, heave, 1e-4f);
        }

        [Test]
        public void RockPose_Trough_IsTheMirror()
        {
            HullMeshMath.RockPose(270f, 2.8f, 1.6f, 1.2f, out float roll, out float pitch, out float heave);
            Assert.AreEqual(-2.8f, roll, 1e-4f);
            Assert.AreEqual(0f, pitch, 1e-4f);
            Assert.AreEqual(-1.2f, heave, 1e-4f);
        }

        [Test]
        public void RockPose_RisingZeroCrossing_IsAllPitch()
        {
            // a = 0: roll/heave zero, pitch at its full lead (the rig's sin(a+π/2) = +pitchA).
            HullMeshMath.RockPose(0f, 2.8f, 1.6f, 1.2f, out float roll, out float pitch, out float heave);
            Assert.AreEqual(0f, roll, 1e-4f);
            Assert.AreEqual(1.6f, pitch, 1e-4f);
            Assert.AreEqual(0f, heave, 1e-4f);
        }

        [Test]
        public void RockPose_MatchesTheBakedFrames_AtEveryFrameCentre()
        {
            // The sprite grid's frame i (of N) was baked at a = i·360/N. The continuous pose sampled
            // at those phases must give exactly the baked frames' roll/pitch/heave — that is what
            // makes a mesh hull and a sprite hull rock in lockstep on the same swell.
            const int frames = 8;
            const float rollA = 2.8f, pitchA = 1.6f, heaveA = 1.2f;
            for (int i = 0; i < frames; i++)
            {
                float a = i * 360f / frames;
                HullMeshMath.RockPose(a, rollA, pitchA, heaveA,
                                      out float roll, out float pitch, out float heave);
                float rad = a * Mathf.Deg2Rad;
                Assert.AreEqual(rollA * Mathf.Sin(rad), roll, 1e-4f, $"roll at frame {i}");
                Assert.AreEqual(pitchA * Mathf.Cos(rad), pitch, 1e-4f, $"pitch at frame {i}");
                Assert.AreEqual(heaveA * Mathf.Sin(rad), heave, 1e-4f, $"heave at frame {i}");
            }
        }
    }
}
