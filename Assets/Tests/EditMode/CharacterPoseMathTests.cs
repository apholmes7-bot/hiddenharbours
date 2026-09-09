using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests
{
    /// <summary>Pins the mesh presenter's pure math (ADR 0044): flipbook frame, deck tilt in the
    /// character's frame, anchor split. Promoted with the code out of the retired spike.</summary>
    public class CharacterPoseMathTests
    {
        // ---- PoseFrame ------------------------------------------------------------------------

        [Test]
        public void PoseFrame_WalksTheCycleAtTheStatedRate()
        {
            // 6 frames at 6 fps: one frame per 1/6 s, wrapping at 1 s.
            Assert.AreEqual(0, CharacterPoseMath.PoseFrame(0.0, 6, 6f));
            Assert.AreEqual(1, CharacterPoseMath.PoseFrame(1.0 / 6.0 + 1e-9, 6, 6f));
            Assert.AreEqual(5, CharacterPoseMath.PoseFrame(5.0 / 6.0 + 1e-9, 6, 6f));
            Assert.AreEqual(0, CharacterPoseMath.PoseFrame(1.0 + 1e-9, 6, 6f));
        }

        [Test]
        public void PoseFrame_IsDeterministic_AndGuardsDegenerateInputs()
        {
            Assert.AreEqual(CharacterPoseMath.PoseFrame(123.456, 12, 11.1f),
                            CharacterPoseMath.PoseFrame(123.456, 12, 11.1f));
            Assert.AreEqual(0, CharacterPoseMath.PoseFrame(3.0, 1, 10f));   // single frame
            Assert.AreEqual(0, CharacterPoseMath.PoseFrame(3.0, 6, 0f));    // zero rate
            Assert.AreEqual(0, CharacterPoseMath.PoseFrame(double.NaN, 6, 10f));
        }

        [Test]
        public void PoseFrame_NeverLeavesTheCycle()
        {
            for (double t = -3.0; t < 3.0; t += 0.037)
            {
                int f = CharacterPoseMath.PoseFrame(t, 8, 9f);
                Assert.That(f, Is.InRange(0, 7), $"clock {t}");
            }
        }

        // ---- DeckTiltToCharacter ---------------------------------------------------------------

        [Test]
        public void DeckTilt_FacingTheBow_IsTheRigsOwnContract()
        {
            // δ = 0: "feed a hull rig's rock(i) straight in" — identity.
            CharacterPoseMath.DeckTiltToCharacter(2.8f, 1.6f, 0f, out float roll, out float pitch);
            Assert.AreEqual(2.8f, roll, 1e-5f);
            Assert.AreEqual(1.6f, pitch, 1e-5f);
        }

        [Test]
        public void DeckTilt_AtNinetyDegrees_RollAndPitchTradePlaces()
        {
            CharacterPoseMath.DeckTiltToCharacter(2.8f, 1.6f, 90f, out float roll, out float pitch);
            Assert.AreEqual(-1.6f, roll, 1e-5f);   // the hull's pitch, seen as the character's roll
            Assert.AreEqual(2.8f, pitch, 1e-5f);   // the hull's roll, seen as the character's pitch
        }

        [Test]
        public void DeckTilt_FacingTheStern_IsTheMirror()
        {
            CharacterPoseMath.DeckTiltToCharacter(2.8f, 1.6f, 180f, out float roll, out float pitch);
            Assert.AreEqual(-2.8f, roll, 1e-4f);
            Assert.AreEqual(-1.6f, pitch, 1e-4f);
        }

        [Test]
        public void DeckTilt_PreservesTheTiltMagnitude_AtEveryHeading()
        {
            float mag = Mathf.Sqrt(2.8f * 2.8f + 1.6f * 1.6f);
            for (float d = 0f; d < 360f; d += 15f)
            {
                CharacterPoseMath.DeckTiltToCharacter(2.8f, 1.6f, d, out float r, out float p);
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
            CharacterPoseMath.SplitAnchor(new Vector2(0.35f, 1.2f), 32,
                                               out float x, out float heavePx);
            Assert.AreEqual(0.35f, x, 1e-5f);
            Assert.AreEqual(38.4f, heavePx, 1e-3f);   // 1.2 m × 32 px/m
        }
    }
}
