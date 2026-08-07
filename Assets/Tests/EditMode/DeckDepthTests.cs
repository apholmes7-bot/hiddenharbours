using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>HOW FAR FROM THE CAMERA A DECK POINT IS</b> — <see cref="DeckAreaMath.DeckDepth"/>, the
    /// number that lets a mesh hull occlude the person standing on her (owner playtest 2026-08-07:
    /// "rider/player sprites visible THROUGH closed cabins").
    ///
    /// <para><b>Why depth and not sorting.</b> A mesh hull composes through ONE overlay quad at one
    /// sorting order, so a crew sprite compared against her is wholly in front of the boat or wholly
    /// behind — and behind is invisible, since the sole under their boots is hull pixels too. Nor can
    /// a cleverer screen-space rule save it: under a ¾ bake a point's screen height is
    /// <c>alongView·sin(elev) + height·cos(elev)</c> while its distance is
    /// <c>alongView·cos(elev) − height·sin(elev)</c>, two INDEPENDENT combinations of one pair, so no
    /// cut through the picture separates "nearer than the fisher" from "further".
    /// <see cref="TheTwoRowsAreIndependent_SoNoScreenSpaceRuleCouldReplaceThis"/> is that claim as a
    /// test, kept because it is the whole justification for the depth-buffer approach.</para>
    ///
    /// <para><b>The headline is <see cref="TheDecksDepthRow_IsTheRigsOwnDepthRow"/>.</b> The deck walk
    /// projects the player with <see cref="DeckAreaMath.DeckToWorld"/> while the hull's own geometry is
    /// projected by <see cref="IsoFacetMath.RigToWorld"/>, and the crew is depth-tested against that
    /// geometry. If the two ever disagreed about which way is "into the screen", the fisher would be
    /// occluded by the wrong half of the boat — and no screenshot would say why. So rather than
    /// trusting a transcription, this pins the deck's depth to the rig matrix's own third row across
    /// every heading, offset, height and elevation. They are two rows of ONE projection; this makes
    /// that a fact the suite enforces rather than a comment.</para>
    ///
    /// <para>Pure maths, no engine state, no GPU — these run everywhere, including the graphics-less
    /// CI where the pixel tests cannot.</para>
    /// </summary>
    public class DeckDepthTests
    {
        private const float IsoElevation = 40f;      // every boat rig bakes at 40° above the horizon

        /// <summary>
        /// THE ANCHOR. A hull-frame point's depth as the deck computes it must equal the depth the
        /// RIG's own object matrix gives the same point — the matrix the facet pass actually poses the
        /// hull's vertices with, and therefore the depths the private z-buffer is filled from.
        ///
        /// <para>Swept over a full turn, both sides of the centreline, above and below the keel, and
        /// at both a real bake elevation and a plan view. The heading is converted through the
        /// PRODUCTION mapping (<see cref="HullMeshMath.HeadingToDirUnits"/>), so this pins the whole
        /// chain a crew member's occlusion actually travels, not a convenient slice of it.</para>
        /// </summary>
        [Test]
        public void TheDecksDepthRow_IsTheRigsOwnDepthRow()
        {
            float[] headings = { 0f, 23f, 45f, 90f, 137f, 180f, 225f, 270f, 315f, 359f };
            Vector3[] points =
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 3.2f, 0.5f), new Vector3(0f, -2.75f, 0.5f),
                new Vector3(1.4f, 0.6f, 1.9f), new Vector3(-1.4f, -0.6f, 0.28f),
                new Vector3(0.9f, 6.1f, 2.4f), new Vector3(-0.35f, -5.4f, -0.2f),
            };

            foreach (float elevation in new[] { IsoElevation, DeckAreaMath.PlanViewElevationDegrees })
            foreach (float heading in headings)
            {
                // The rig turntable angle this heading draws at — the boats' own bake convention
                // (counter-clockwise, zero at north), through the function the driver uses.
                float dirUnits = HullMeshMath.HeadingToDirUnits(heading, 0f, azimuthCounterClockwise: true);
                Matrix4x4 rig = IsoFacetMath.RigToWorld(dirUnits, elevation);

                foreach (Vector3 p in points)
                {
                    float mine = DeckAreaMath.DeckDepth(new Vector2(p.x, p.y), p.z, heading, elevation);
                    float rigDepth = rig.MultiplyVector(p).z;
                    Assert.AreEqual(rigDepth, mine, 1e-4f,
                        $"The deck and the rig disagree about how far away a deck point is " +
                        $"(heading {heading}°, elev {elevation}°, hull-frame {p}). They are two rows " +
                        "of ONE projection — if they drift, the crew is depth-tested against a hull " +
                        "drawn somewhere else and is occluded by the wrong half of the boat.");
                }
            }
        }

        /// <summary>
        /// The justification for the whole approach, as an assertion: screen height and distance are
        /// INDEPENDENT combinations of (alongView, height), so two points can share a screen row and
        /// still differ in depth — and no ordering by screen position, sorting order, or any other
        /// per-object number can separate them. That is why the crew is composited into the hull's
        /// recording and settled by a z-buffer instead of sorted against her.
        /// </summary>
        [Test]
        public void TheTwoRowsAreIndependent_SoNoScreenSpaceRuleCouldReplaceThis()
        {
            const float heading = 0f;

            // A fisher standing on the sole, and a wheelhouse roof edge forward of and above them,
            // contrived to draw on the SAME screen row.
            var fisher = new Vector2(0f, 0f);
            const float fisherHeight = 0.5f;
            float sin = Mathf.Sin(IsoElevation * Mathf.Deg2Rad), cos = Mathf.Cos(IsoElevation * Mathf.Deg2Rad);

            // Screen y = alongView·sin + height·cos. Pick the roof so its screen y matches the fisher's.
            const float roofHeight = 2.4f;
            float roofAlong = (fisher.y * sin + fisherHeight * cos - roofHeight * cos) / sin;
            var roof = new Vector2(0f, roofAlong);

            float fisherScreenY = DeckAreaMath.DeckToWorld(fisher, fisherHeight, heading, IsoElevation).y;
            float roofScreenY = DeckAreaMath.DeckToWorld(roof, roofHeight, heading, IsoElevation).y;
            Assert.AreEqual(fisherScreenY, roofScreenY, 1e-4f,
                "harness: the two points must share a screen row for the claim to mean anything");

            float fisherDepth = DeckAreaMath.DeckDepth(fisher, fisherHeight, heading, IsoElevation);
            float roofDepth = DeckAreaMath.DeckDepth(roof, roofHeight, heading, IsoElevation);
            Assert.Less(roofDepth, fisherDepth,
                "The roof edge is NEARER the camera than the fisher while drawing on the same screen " +
                "row — so it must cover them, and nothing about their screen positions could have " +
                "said so. This is the defect: a sorting order is one number per OBJECT and the answer " +
                "is per PIXEL.");
        }

        /// <summary>The two coefficients, stated exactly. A metre further along the view is
        /// <c>cos(elev)</c> further away; a metre of height is <c>sin(elev)</c> NEARER — the exact
        /// complement of the foreshortening the screen row loses, because the metre the screen gives
        /// up is the metre that went into depth.</summary>
        [Test]
        public void AlongViewSendsYouFurther_AndHeightBringsYouNearer_ByTheBakesOwnCosineAndSine()
        {
            const float heading = 0f;   // bow north: +y along the deck is straight into the screen
            float baseline = DeckAreaMath.DeckDepth(Vector2.zero, 0f, heading, IsoElevation);

            float oneMetreAlong = DeckAreaMath.DeckDepth(new Vector2(0f, 1f), 0f, heading, IsoElevation);
            Assert.AreEqual(Mathf.Cos(IsoElevation * Mathf.Deg2Rad), oneMetreAlong - baseline, 1e-5f,
                "a metre toward the bow of a north-pointing hull is cos(elev) further from the camera");

            float oneMetreUp = DeckAreaMath.DeckDepth(Vector2.zero, 1f, heading, IsoElevation);
            Assert.AreEqual(-Mathf.Sin(IsoElevation * Mathf.Deg2Rad), oneMetreUp - baseline, 1e-5f,
                "and a metre of height is sin(elev) NEARER — a masthead leans toward the camera");

            Assert.AreEqual(Mathf.Cos(IsoElevation * Mathf.Deg2Rad),
                            DeckAreaMath.DepthPerAlongView(IsoElevation), 1e-6f);
            Assert.AreEqual(Mathf.Sin(IsoElevation * Mathf.Deg2Rad),
                            DeckAreaMath.DepthPerHeight(IsoElevation), 1e-6f);
        }

        /// <summary>A standing body is not at one distance: its head is nearer than its boots by
        /// <c>tan(elev)</c> per metre of SCREEN rise. That slope is what makes a wheelhouse which
        /// clears the boots still cross the head, and it is what the crew billboard interpolates.</summary>
        [Test]
        public void DepthPerScreenRise_IsTangentOfTheBakeElevation()
        {
            Assert.AreEqual(Mathf.Tan(IsoElevation * Mathf.Deg2Rad),
                            DeckAreaMath.DepthPerScreenRise(IsoElevation), 1e-5f);

            // A plan view has no screen rise to slope against (height carries no screen travel), so
            // the figure is a flat card — the honest answer, not a divide by zero.
            Assert.AreEqual(0f, DeckAreaMath.DepthPerScreenRise(DeckAreaMath.PlanViewElevationDegrees), 0f);
            Assert.AreEqual(0f, DeckAreaMath.DepthPerScreenRise(0f), 0f);
        }

        /// <summary>Artwork no camera ever foreshortened (the hand-drawn compass hulls, elevation 90)
        /// has only one thing depth can be: height. The deck's along-keel axis lies flat in the
        /// picture, so walking it changes nothing about who is in front.</summary>
        [Test]
        public void AtAPlanView_OnlyHeightIsDepth()
        {
            foreach (float heading in new[] { 0f, 90f, 210f })
            {
                Assert.AreEqual(-1.75f,
                    DeckAreaMath.DeckDepth(new Vector2(3f, -4f), 1.75f, heading,
                                           DeckAreaMath.PlanViewElevationDegrees), 1e-5f,
                    "a plan view's depth is exactly minus the height, at every heading");
            }
        }

        /// <summary>NaN in never means NaN out — the same fail-safe every other member of
        /// <see cref="DeckAreaMath"/> carries, because a NaN depth would silently disable the crew's
        /// depth test rather than fail loudly.</summary>
        [Test]
        public void NaNInputs_DegradeToZero_NeverToNaN()
        {
            Assert.IsFalse(float.IsNaN(DeckAreaMath.DeckDepth(new Vector2(float.NaN, 1f), 0.5f, 30f, IsoElevation)));
            Assert.IsFalse(float.IsNaN(DeckAreaMath.DeckDepth(Vector2.one, float.NaN, 30f, IsoElevation)));
            Assert.IsFalse(float.IsNaN(DeckAreaMath.DeckDepth(Vector2.one, 0.5f, float.NaN, IsoElevation)));
            Assert.IsFalse(float.IsNaN(DeckAreaMath.DeckDepth(Vector2.one, 0.5f, 30f, float.NaN)));
        }
    }
}
