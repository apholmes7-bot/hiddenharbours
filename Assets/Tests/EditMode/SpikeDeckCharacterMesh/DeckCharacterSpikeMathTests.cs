using HiddenHarbours.SpikeDeckCharacterMesh;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.SpikeDeckCharacterMesh
{
    /// <summary>⚠️ SPIKE (deck-character-mesh, draft ADR 0024) — pins the rig driver's pure math.</summary>
    public class DeckCharacterSpikeMathTests
    {
        // ---- PoseFrame ------------------------------------------------------------------------

        [Test]
        public void PoseFrame_WalksTheCycleAtTheStatedRate()
        {
            // 6 frames at 6 fps: one frame per 1/6 s, wrapping at 1 s.
            Assert.AreEqual(0, DeckCharacterSpikeMath.PoseFrame(0.0, 6, 6f));
            Assert.AreEqual(1, DeckCharacterSpikeMath.PoseFrame(1.0 / 6.0 + 1e-9, 6, 6f));
            Assert.AreEqual(5, DeckCharacterSpikeMath.PoseFrame(5.0 / 6.0 + 1e-9, 6, 6f));
            Assert.AreEqual(0, DeckCharacterSpikeMath.PoseFrame(1.0 + 1e-9, 6, 6f));
        }

        [Test]
        public void PoseFrame_IsDeterministic_AndGuardsDegenerateInputs()
        {
            Assert.AreEqual(DeckCharacterSpikeMath.PoseFrame(123.456, 12, 11.1f),
                            DeckCharacterSpikeMath.PoseFrame(123.456, 12, 11.1f));
            Assert.AreEqual(0, DeckCharacterSpikeMath.PoseFrame(3.0, 1, 10f));   // single frame
            Assert.AreEqual(0, DeckCharacterSpikeMath.PoseFrame(3.0, 6, 0f));    // zero rate
            Assert.AreEqual(0, DeckCharacterSpikeMath.PoseFrame(double.NaN, 6, 10f));
        }

        [Test]
        public void PoseFrame_NeverLeavesTheCycle()
        {
            for (double t = -3.0; t < 3.0; t += 0.037)
            {
                int f = DeckCharacterSpikeMath.PoseFrame(t, 8, 9f);
                Assert.That(f, Is.InRange(0, 7), $"clock {t}");
            }
        }

        // ---- DeckTiltToCharacter ---------------------------------------------------------------

        [Test]
        public void DeckTilt_FacingTheBow_IsTheRigsOwnContract()
        {
            // δ = 0: "feed a hull rig's rock(i) straight in" — identity.
            DeckCharacterSpikeMath.DeckTiltToCharacter(2.8f, 1.6f, 0f, out float roll, out float pitch);
            Assert.AreEqual(2.8f, roll, 1e-5f);
            Assert.AreEqual(1.6f, pitch, 1e-5f);
        }

        [Test]
        public void DeckTilt_AtNinetyDegrees_RollAndPitchTradePlaces()
        {
            DeckCharacterSpikeMath.DeckTiltToCharacter(2.8f, 1.6f, 90f, out float roll, out float pitch);
            Assert.AreEqual(-1.6f, roll, 1e-5f);   // the hull's pitch, seen as the character's roll
            Assert.AreEqual(2.8f, pitch, 1e-5f);   // the hull's roll, seen as the character's pitch
        }

        [Test]
        public void DeckTilt_FacingTheStern_IsTheMirror()
        {
            DeckCharacterSpikeMath.DeckTiltToCharacter(2.8f, 1.6f, 180f, out float roll, out float pitch);
            Assert.AreEqual(-2.8f, roll, 1e-4f);
            Assert.AreEqual(-1.6f, pitch, 1e-4f);
        }

        [Test]
        public void DeckTilt_PreservesTheTiltMagnitude_AtEveryHeading()
        {
            float mag = Mathf.Sqrt(2.8f * 2.8f + 1.6f * 1.6f);
            for (float d = 0f; d < 360f; d += 15f)
            {
                DeckCharacterSpikeMath.DeckTiltToCharacter(2.8f, 1.6f, d, out float r, out float p);
                Assert.AreEqual(mag, Mathf.Sqrt(r * r + p * p), 1e-4f, $"δ = {d}");
            }
        }

        // ---- SplitAnchor -----------------------------------------------------------------------

        [Test]
        public void SplitAnchor_LiftGoesToHeave_LateralToTheRoot()
        {
            // The load-bearing choice: the screen-y lift rides the HEAVE channel so the character
            // root can share its hull's world y — the calibrated iso-depth frame (ADR 0023 §24
            // deck corollary) keys off root y, and a deck-height root would sort the character
            // BEHIND the deck it stands on.
            DeckCharacterSpikeMath.SplitAnchor(new Vector2(0.35f, 1.2f), 32,
                                               out float x, out float heavePx);
            Assert.AreEqual(0.35f, x, 1e-5f);
            Assert.AreEqual(38.4f, heavePx, 1e-3f);   // 1.2 m × 32 px/m
        }
    }
}
