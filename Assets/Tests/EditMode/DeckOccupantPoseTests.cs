using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The fisher, composed for the hull that will draw them</b> —
    /// <see cref="DeckOccupantPoseMath.For"/>, the seam between the character's finished picture and
    /// the boat's geometry (owner playtest 2026-08-07: "sprites visible THROUGH closed cabins").
    ///
    /// <para><b>What is actually at risk here is argument ORDER, not arithmetic.</b> The call puts a
    /// sprite, a world point, a deck position, a deck height, a heading, an elevation, a lean, a flip
    /// and a tint into one line — five of them floats. A height passed where a roll belongs compiles
    /// perfectly, throws nothing, and draws a fisher occluded by the wrong half of the boat. So these
    /// tests deliberately move ONE input at a time and insist the right term of the pose moves with
    /// it: that is the only shape of test that can see a swap.</para>
    ///
    /// <para>The projection itself is pinned next door in <see cref="DeckDepthTests"/> (against the
    /// rig's own matrix), and the pixels in <c>IsoFacetUrpPassTests</c>. This is the wiring between.</para>
    /// </summary>
    public class DeckOccupantPoseTests
    {
        private const float IsoElevation = 40f;

        private static Sprite NewCell()
        {
            var tex = new Texture2D(2, 2);
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 32f);
        }

        [Test]
        public void TheDepthIsTheDecksOwnDepth_ForTheSameStandingPlace()
        {
            var sprite = NewCell();
            var deckLocal = new Vector2(0.6f, -1.9f);
            const float height = 0.5f, heading = 137f;

            HullDeckOccupantPose pose = DeckOccupantPoseMath.For(
                sprite, new Vector2(12f, -3f), deckLocal, height, heading, IsoElevation,
                rollDegrees: 4f, flipX: false, tint: Color.white);

            Assert.AreEqual(DeckAreaMath.DeckDepth(deckLocal, height, heading, IsoElevation),
                            pose.HullFrameDepth, 1e-6f,
                            "the pose's depth must BE the deck's depth for that spot — not a second " +
                            "opinion computed from the same numbers a different way");
            Assert.AreEqual(DeckAreaMath.DepthPerScreenRise(IsoElevation), pose.DepthPerScreenRise, 1e-6f,
                            "and the body's lean into the scene must be the bake's own tan(elev)");
            Object.DestroyImmediate(sprite.texture);
        }

        /// <summary>The swap guard for the pair most easily crossed: DECK HEIGHT and the LEAN are both
        /// small floats about a body, and only one of them belongs in the depth. Raising the deck a
        /// metre must move the fisher exactly sin(elev) nearer; leaning them must not move them at
        /// all.</summary>
        [Test]
        public void DeckHeightMovesTheDepth_AndTheLeanNeverDoes()
        {
            var sprite = NewCell();
            var deckLocal = new Vector2(0.2f, 1.1f);
            const float heading = 0f;

            HullDeckOccupantPose sole = DeckOccupantPoseMath.For(
                sprite, Vector2.zero, deckLocal, 0.5f, heading, IsoElevation, 0f, false, Color.white);
            HullDeckOccupantPose foredeck = DeckOccupantPoseMath.For(
                sprite, Vector2.zero, deckLocal, 1.5f, heading, IsoElevation, 0f, false, Color.white);

            Assert.AreEqual(-Mathf.Sin(IsoElevation * Mathf.Deg2Rad),
                            foredeck.HullFrameDepth - sole.HullFrameDepth, 1e-5f,
                            "a metre of DECK HEIGHT is sin(elev) nearer the camera — if this is 0 the " +
                            "height never reached the depth; if it is some other number it reached the " +
                            "wrong slot");

            HullDeckOccupantPose leaning = DeckOccupantPoseMath.For(
                sprite, Vector2.zero, deckLocal, 0.5f, heading, IsoElevation, 12f, false, Color.white);
            Assert.AreEqual(sole.HullFrameDepth, leaning.HullFrameDepth, 0f,
                            "LEANING a fisher must not move them toward or away from the camera — if it " +
                            "does, the roll has been passed into a geometry slot");
            Assert.AreEqual(12f, leaning.RollDegrees, 1e-6f, "and the lean must arrive as the lean");
            Object.DestroyImmediate(sprite.texture);
        }

        /// <summary>Walking toward the bow of a NORTH-pointing hull walks away from the camera. The
        /// heading has to be the heading for this to hold — a swapped heading and elevation gives a
        /// plausible-looking number with the wrong sign at most bearings.</summary>
        [Test]
        public void WalkingForwardOnANorthPointingHull_TakesYouFurtherAway()
        {
            var sprite = NewCell();
            HullDeckOccupantPose aft = DeckOccupantPoseMath.For(
                sprite, Vector2.zero, new Vector2(0f, -2f), 0.5f, 0f, IsoElevation, 0f, false, Color.white);
            HullDeckOccupantPose forward = DeckOccupantPoseMath.For(
                sprite, Vector2.zero, new Vector2(0f, 2f), 0.5f, 0f, IsoElevation, 0f, false, Color.white);

            Assert.Greater(forward.HullFrameDepth, aft.HullFrameDepth,
                "on a hull pointing north, the bow is further from the camera than the stern — so a " +
                "fisher who walks forward must end up BEHIND one who stayed aft, and be occluded by " +
                "anything between them");
            Object.DestroyImmediate(sprite.texture);
        }

        /// <summary>Everything about the LOOK travels untouched. The composition adds two numbers and
        /// re-decides nothing — the cell, the flip and the tint were chosen by the one authority that
        /// owns the character's picture, and a second opinion here is how two figures start to
        /// disagree about which frame they are on.</summary>
        [Test]
        public void ThePictureTravelsUntouched()
        {
            var sprite = NewCell();
            var feet = new Vector2(-7.25f, 3.5f);
            var tint = new Color(0.4f, 0.9f, 0.2f, 0.75f);

            HullDeckOccupantPose pose = DeckOccupantPoseMath.For(
                sprite, feet, new Vector2(1f, 1f), 0.3f, 42f, IsoElevation, -3f, flipX: true, tint: tint);

            Assert.AreSame(sprite, pose.Sprite);
            Assert.AreEqual(feet, pose.WorldFeet);
            Assert.IsTrue(pose.FlipX);
            Assert.AreEqual(tint, pose.Tint);
            Assert.AreEqual(-3f, pose.RollDegrees, 1e-6f);
            Object.DestroyImmediate(sprite.texture);
        }

        /// <summary>Artwork no camera foreshortened (the hand-drawn compass hulls at elevation 90)
        /// composes a FLAT card: there is no screen rise for a body to lean along. Not a degenerate
        /// case to guard against — the honest answer, and the one a sprite hull would want if it ever
        /// grew a depth buffer.</summary>
        [Test]
        public void PlanViewArtwork_ComposesAFlatCard()
        {
            var sprite = NewCell();
            HullDeckOccupantPose pose = DeckOccupantPoseMath.For(
                sprite, Vector2.zero, new Vector2(0f, 3f), 1f, 90f,
                DeckAreaMath.PlanViewElevationDegrees, 0f, false, Color.white);

            Assert.AreEqual(0f, pose.DepthPerScreenRise, 0f);
            Assert.AreEqual(-1f, pose.HullFrameDepth, 1e-5f,
                            "with no foreshortening, only height is depth");
            Object.DestroyImmediate(sprite.texture);
        }
    }
}
