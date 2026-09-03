using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>Which way is OUTBOARD</b> — the decision the owner's second press makes:
    /// <i>"it depends which way you face when you place the next button, either in the boat or in the
    /// water if facing each."</i>
    ///
    /// <para>The sea under St Peters' berth is 4 m deep, so the interesting assertions here are the ones
    /// about NOT going in: a rider walking the gunwale is not leaving the boat, a corner is two rails at
    /// once rather than whichever edge won a coin toss, and a hull with no width has no side to go over.</para>
    /// </summary>
    public class OverTheSideMathTests
    {
        // The eight facings this game thinks in, as deck bearings (0 = looking at the bow, +90 = starboard).
        private static readonly float[] EightFacings = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        private static Vector2 Facing(float deckBearing) =>
            OverTheSideMath.FacingFromDeckBearing(deckBearing);

        // =============================================================================================
        //  1. the facing vector agrees with the deck-bearing convention
        // =============================================================================================

        [Test]
        public void ADeckBearing_PointsTheWayTheDeckFrameMeans()
        {
            AssertV2(new Vector2(0f, 1f), Facing(0f), "0° looks at the BOW (deck +y)");
            AssertV2(new Vector2(1f, 0f), Facing(90f), "90° looks to STARBOARD (deck +x)");
            AssertV2(new Vector2(0f, -1f), Facing(180f), "180° looks astern");
            AssertV2(new Vector2(-1f, 0f), Facing(270f), "270° looks to PORT");
        }

        [Test]
        public void ABrokenBearing_LooksAtTheBow_RatherThanReturningANaN()
        {
            AssertV2(new Vector2(0f, 1f), Facing(float.NaN), "NaN");
            AssertV2(new Vector2(0f, 1f), Facing(float.PositiveInfinity), "infinity");
        }

        // =============================================================================================
        //  2. the outward normal of the walkable BOX
        // =============================================================================================

        [Test]
        public void EachSideOfTheBox_PointsAwayFromHer()
        {
            var c = Vector2.zero;
            var h = new Vector2(1f, 4f);                          // a narrow hull: 2 m beam, 8 m long

            AssertV2(new Vector2(1f, 0f), OverTheSideMath.OutwardNormalOnBox(c, h, new Vector2(1f, 0f)),
                     "the starboard rail points to starboard");
            AssertV2(new Vector2(-1f, 0f), OverTheSideMath.OutwardNormalOnBox(c, h, new Vector2(-1f, 0f)),
                     "the port rail points to port");
            AssertV2(new Vector2(0f, 1f), OverTheSideMath.OutwardNormalOnBox(c, h, new Vector2(0f, 4f)),
                     "the stem points forward");
            AssertV2(new Vector2(0f, -1f), OverTheSideMath.OutwardNormalOnBox(c, h, new Vector2(0f, -4f)),
                     "the transom points aft");
        }

        /// <summary>⭐ On the quarter, BOTH the side and the transom are the rail — the honest outward
        /// direction is the diagonal between them, not whichever edge won a floating-point coin toss.</summary>
        [Test]
        public void OnACorner_TheNormalIsTheDiagonal_NotOneEdgeOrTheOther()
        {
            var n = OverTheSideMath.OutwardNormalOnBox(Vector2.zero, new Vector2(1f, 1f),
                                                       new Vector2(1f, -1f));      // starboard quarter
            AssertV2(new Vector2(1f, -1f).normalized, n, "starboard-quarter corner");
        }

        [Test]
        public void ADegenerateBox_HasNoOutboardAtAll_RatherThanANaN()
        {
            var n = OverTheSideMath.OutwardNormalOnBox(Vector2.zero, Vector2.zero, Vector2.zero);
            Assert.AreEqual(Vector2.zero, n, "a hull with no width has no side to go over");
            Assert.IsFalse(OverTheSideMath.GoesOverTheSide(Facing(90f), n),
                           "…and nothing can send her over it");
        }

        // =============================================================================================
        //  3. ⭐ THE PREDICATE — 8 facings × 4 rails, and the tie rule
        // =============================================================================================

        /// <summary>
        /// ⭐ The whole decision table. Three of the eight facings clear each rail; two of the remaining
        /// five are the TIES — facing straight along that rail — and both stay in the boat.
        /// </summary>
        [Test]
        public void EightFacingsAgainstFourRails_OnlyTheThreeThatFaceTheSeaGoOver(
            [Values("port", "starboard", "bow", "stern")] string rail)
        {
            Vector2 n = rail switch
            {
                "port"      => new Vector2(-1f, 0f),
                "starboard" => new Vector2(1f, 0f),
                "bow"       => new Vector2(0f, 1f),
                _           => new Vector2(0f, -1f),
            };
            float[] expectedOver = rail switch
            {
                "port"      => new[] { 225f, 270f, 315f },
                "starboard" => new[] { 45f, 90f, 135f },
                "bow"       => new[] { 315f, 0f, 45f },
                _           => new[] { 135f, 180f, 225f },
            };

            foreach (float bearing in EightFacings)
            {
                bool expected = System.Array.IndexOf(expectedOver, bearing) >= 0;
                bool actual = OverTheSideMath.GoesOverTheSide(Facing(bearing), n);
                Assert.AreEqual(expected, actual,
                    $"facing {bearing:F0}° against the {rail} rail: expected " +
                    $"{(expected ? "OVER THE SIDE" : "to stay aboard")}");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>The tie rule, stated on its own because it is a SAFETY rule.</b> A rider facing exactly
        /// along the rail is walking the gunwale, not stepping off it — and the water under this berth is
        /// 4 m deep. Both parallel facings stay aboard.
        /// </summary>
        [Test]
        public void FacingStraightAlongTheRail_StaysInTheBoat()
        {
            var port = new Vector2(-1f, 0f);
            Assert.IsFalse(OverTheSideMath.GoesOverTheSide(Facing(0f), port),
                "walking forward along the port rail is not going over it");
            Assert.IsFalse(OverTheSideMath.GoesOverTheSide(Facing(180f), port),
                "…nor is walking aft along it");

            // …and a hair either side of the tie still stays in, which is what the dead band is for.
            Assert.IsFalse(OverTheSideMath.GoesOverTheSide(Facing(180.001f), port),
                "a thousandth of a degree past parallel is arithmetic noise, not an intention");
        }

        /// <summary>…and the mirror: square-on to the rail always goes over, or the verb is unusable.</summary>
        [Test]
        public void FacingSquareAtTheSea_AlwaysGoesOver()
        {
            foreach (var n in new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f),
                                      new Vector2(0f, 1f), new Vector2(0f, -1f) })
                Assert.IsTrue(OverTheSideMath.GoesOverTheSide(n, n),
                              $"facing straight out over the {n} rail must go over it");
        }

        private static void AssertV2(Vector2 expected, Vector2 actual, string what = "")
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, what + " (x)");
            Assert.AreEqual(expected.y, actual.y, 1e-3f, what + " (y)");
        }
    }
}
