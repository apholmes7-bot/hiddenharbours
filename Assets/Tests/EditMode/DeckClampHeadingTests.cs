using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The deck-confinement fix: the walkable deck is the hull rectangle in the DRAWN facing's frame, not a
    /// fixed world-axis rect. Pins the drawn-heading quantization (<see cref="DirectionalBoatSprite"/> snaps
    /// the picture to N of its facings — the deck must clamp in that SAME frame), the world↔deck-frame
    /// rotation, and the oriented clamp/step at cardinal AND diagonal headings — the case the old world-axis
    /// clamp got wrong (an east-facing hull kept the portrait north-facing rectangle, so the sprite could
    /// stand visibly off the drawn deck).
    /// </summary>
    public class DeckClampHeadingTests
    {
        private static readonly Vector2 Half = new Vector2(0.7f, 1.6f);   // beam × length (the punt greybox)
        private static readonly Vector2 Centre = Vector2.zero;
        private const float Tol = 1e-4f;

        private static void AssertVec(Vector2 expected, Vector2 actual, string msg)
        {
            Assert.AreEqual(expected.x, actual.x, Tol, msg + " (x)");
            Assert.AreEqual(expected.y, actual.y, Tol, msg + " (y)");
        }

        // ---- the drawn heading (the frame the deck lives in) --------------------------------------

        [Test]
        public void SnapHeadingDegrees_QuantizesToTheDrawnFacing()
        {
            // 4-way art (N/E/S/W): the picture the player sees is the nearest facing.
            Assert.AreEqual(0f, DirectionalBoatSprite.SnapHeadingDegrees(10f, 4, 0f), Tol);
            Assert.AreEqual(90f, DirectionalBoatSprite.SnapHeadingDegrees(80f, 4, 0f), Tol);
            Assert.AreEqual(90f, DirectionalBoatSprite.SnapHeadingDegrees(45f, 4, 0f), Tol,
                "a bucket edge rounds to the next facing CW (the HeadingToFacingIndex rule)");
            Assert.AreEqual(0f, DirectionalBoatSprite.SnapHeadingDegrees(350f, 4, 0f), Tol);
            Assert.AreEqual(180f, DirectionalBoatSprite.SnapHeadingDegrees(190f, 4, 0f), Tol);
        }

        [Test]
        public void SnapHeadingDegrees_EightWayArt_GivesDiagonals()
        {
            Assert.AreEqual(45f, DirectionalBoatSprite.SnapHeadingDegrees(50f, 8, 0f), Tol,
                "8-way art snaps to the NE facing — the deck rectangle goes diagonal with it");
            Assert.AreEqual(225f, DirectionalBoatSprite.SnapHeadingDegrees(202.5f, 8, 0f), Tol,
                "the half-up boundary rule holds for diagonals");
        }

        [Test]
        public void SnapHeadingDegrees_NoFacingArt_IsTheTrueHeading()
        {
            Assert.AreEqual(123.4f, DirectionalBoatSprite.SnapHeadingDegrees(123.4f, 0, 0f), Tol,
                "no facings → the picture rotates with the hull → clamp at the true heading");
            Assert.AreEqual(270f, DirectionalBoatSprite.SnapHeadingDegrees(-90f, 0, 0f), Tol, "normalized to [0,360)");
        }

        // ---- world ↔ deck frame -------------------------------------------------------------------

        [Test]
        public void WorldToDeckFrame_TheDrawnBow_IsPlusY()
        {
            // Compass heading 90 = East: the bow points +X in the world → +Y in the deck frame.
            AssertVec(new Vector2(0f, 2f),
                      DeckWalkController.WorldToDeckFrame(new Vector2(2f, 0f), 90f),
                      "2 m ahead of an east-facing bow is 2 m along the keel");
            // And straight back out again.
            AssertVec(new Vector2(2f, 0f),
                      DeckWalkController.DeckFrameToWorld(new Vector2(0f, 2f), 90f),
                      "the inverse maps the keel back onto the world bow direction");
        }

        [Test]
        public void WorldToDeckFrame_RoundTrips_AtAnyHeading()
        {
            var v = new Vector2(0.53f, -1.21f);
            foreach (float heading in new[] { 0f, 37f, 90f, 135f, 222.5f, 359f })
            {
                Vector2 roundTrip = DeckWalkController.DeckFrameToWorld(
                    DeckWalkController.WorldToDeckFrame(v, heading), heading);
                AssertVec(v, roundTrip, $"world→deck→world is the identity at heading {heading}");
            }
        }

        // ---- the oriented clamp ---------------------------------------------------------------------

        [Test]
        public void ClampToDeckHeading_North_MatchesThePlainClamp()
        {
            var p = new Vector2(5f, -9f);
            AssertVec(DeckWalkController.ClampToDeck(p, Centre, Half),
                      DeckWalkController.ClampToDeckHeading(p, 0f, Centre, Half),
                      "a north-drawn hull is the old world-axis rectangle exactly");
        }

        [Test]
        public void ClampToDeckHeading_EastFacing_TheRectangleTurnsWithTheHull()
        {
            // Bow East: the LENGTH (1.6) now runs along world X, the BEAM (0.7) along world Y — the old
            // world-axis clamp had these swapped (the confinement bug).
            AssertVec(new Vector2(1.6f, 0f),
                      DeckWalkController.ClampToDeckHeading(new Vector2(2f, 0f), 90f, Centre, Half),
                      "toward the drawn bow (east) the deck runs the hull LENGTH");
            AssertVec(new Vector2(0f, 0.7f),
                      DeckWalkController.ClampToDeckHeading(new Vector2(0f, 1f), 90f, Centre, Half),
                      "abeam (north of an east-facing hull) it stops at the BEAM");
        }

        [Test]
        public void ClampToDeckHeading_DiagonalFacing_ClampsInTheRotatedFrame()
        {
            // An 8-way NE facing (45°): 3 m out along the drawn bow clamps to the 1.6 m half-length ALONG
            // THE DIAGONAL, and 2 m abeam (starboard) clamps to the 0.7 m half-beam on the diagonal too.
            float s = Mathf.Sin(45f * Mathf.Deg2Rad);
            AssertVec(new Vector2(1.6f * s, 1.6f * s),
                      DeckWalkController.ClampToDeckHeading(new Vector2(3f * s, 3f * s), 45f, Centre, Half),
                      "along the NE bow the deck ends at the half-length, on the diagonal");
            AssertVec(new Vector2(0.7f * s, -0.7f * s),
                      DeckWalkController.ClampToDeckHeading(new Vector2(2f * s, -2f * s), 45f, Centre, Half),
                      "abeam of the NE bow the deck ends at the half-beam, on the diagonal");
        }

        [Test]
        public void ClampToDeckHeading_InsideTheDeck_IsUntouched_AtAnyHeading()
        {
            var p = new Vector2(0.1f, 0.2f);
            foreach (float heading in new[] { 0f, 45f, 90f, 137f, 270f })
                AssertVec(p, DeckWalkController.ClampToDeckHeading(p, heading, Centre, Half),
                          $"a point on the deck stays put at heading {heading}");
        }

        // ---- the step -------------------------------------------------------------------------------

        [Test]
        public void StepOnDeck_EastFacing_WalksTheDrawnLength()
        {
            // Walking east along an east-facing hull reaches the 1.6 m bow, not the old 0.7 m world-x wall.
            Vector2 end = DeckWalkController.StepOnDeck(Vector2.zero, Vector2.right, speed: 10f, dt: 1f,
                                                        drawnHeadingDeg: 90f, Centre, Half);
            AssertVec(new Vector2(1.6f, 0f), end, "the walkable deck follows the drawn hull");
        }

        [Test]
        public void StepOnDeck_NoInput_StillReclampsWhileTheBoatTurns()
        {
            // The boat turned under the player (their world offset is now off the newly-drawn deck): even a
            // zero-input step pulls them back onto it — the player visibly stays on the hull mid-turn.
            Vector2 offDeck = new Vector2(0f, 1.5f);   // fine for a north hull, off an east-drawn one (beam 0.7)
            Vector2 end = DeckWalkController.StepOnDeck(offDeck, Vector2.zero, speed: 2.5f, dt: 0.016f,
                                                        drawnHeadingDeg: 90f, Centre, Half);
            AssertVec(new Vector2(0f, 0.7f), end, "zero input still re-clamps onto the drawn deck");
        }

        [Test]
        public void StepOnDeck_DiagonalInput_IsNotFaster()
        {
            Vector2 end = DeckWalkController.StepOnDeck(Vector2.zero, new Vector2(1f, 1f), speed: 2f, dt: 0.1f,
                                                        drawnHeadingDeg: 0f, Centre, Half);
            Assert.AreEqual(0.2f, end.magnitude, Tol, "diagonal input is magnitude-clamped (the on-foot rule)");
        }
    }
}
