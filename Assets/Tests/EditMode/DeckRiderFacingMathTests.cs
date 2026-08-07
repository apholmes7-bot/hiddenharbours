using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A rider's facing is composed, and composing it cannot drift</b> —
    /// <see cref="DeckRiderFacingMath"/>.
    ///
    /// <para>The defect these close (owner playtest 2026-08-07): <i>"the sprite doesn't follow the
    /// orientation of the boat, they slowly lose reference and spin horizontally."</i> The signature —
    /// accumulating drift — is what a per-frame delta produces, so the headline test here is the one that
    /// runs a hull round <b>ten full turns</b> and demands the answer be EXACT at every step, not merely
    /// close. A composition of two live reads passes that trivially; anything that integrates cannot.</para>
    ///
    /// <para>The other half is convention. A bearing measured in the DECK frame is composed with a COMPASS
    /// heading, and the two must speak the same language or the fisher faces the wrong way at every heading
    /// but north. That is pinned against <see cref="IsoCharacterMath.HeadingFor"/> — the shipped world-frame
    /// read — rather than against a number typed in here.</para>
    /// </summary>
    public class DeckRiderFacingMathTests
    {
        private const float Eps = 1e-4f;

        // ---- the deck bearing: the same convention as the world read, applied in the hull's frame ----

        [Test]
        public void DeckBearing_BowIsZero_StarboardIsNinety_PortIsTwoSeventy()
        {
            // Deck frame: +y toward the bow, +x abeam to starboard. Walking each way in turn.
            Assert.AreEqual(0f, DeckRiderFacingMath.DeckBearing(new Vector2(0f, 2f), 0.05f, 123f), Eps,
                            "walking toward the bow is bearing 0 — the deck frame's own north");
            Assert.AreEqual(90f, DeckRiderFacingMath.DeckBearing(new Vector2(2f, 0f), 0.05f, 123f), Eps,
                            "walking to starboard is +90 (clockwise, like a compass)");
            Assert.AreEqual(180f, Mathf.Abs(DeckRiderFacingMath.DeckBearing(new Vector2(0f, -2f), 0.05f, 0f)),
                            Eps, "walking aft is astern of the bow — 180 either way round");
            Assert.AreEqual(-90f, DeckRiderFacingMath.DeckBearing(new Vector2(-2f, 0f), 0.05f, 123f), Eps,
                            "and to port is −90 (atan2's range; the composition wraps it)");
        }

        [Test]
        public void DeckBearing_IsTheSameRuleAsTheWorldFrameRead_NotASecondCopyOfIt()
        {
            // The deck frame and the world frame share ONE bearing convention. Two independent copies of
            // atan2 would be two chances to disagree about which way north is, so this pins the identity
            // rather than the digits — a change to either side fails here first.
            var samples = new[]
            {
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(1f, -1f),
                new Vector2(0f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 0f), new Vector2(-1f, 1f),
                new Vector2(0.31f, -2.7f), new Vector2(-4.2f, 0.03f),
            };
            foreach (var v in samples)
                Assert.AreEqual(IsoCharacterMath.HeadingFor(v, 0.05f, 17f),
                                DeckRiderFacingMath.DeckBearing(v, 0.05f, 17f), 0f,
                                $"the deck bearing of {v} must BE the shared heading rule, bit for bit");
        }

        [Test]
        public void DeckBearing_BelowTheStepFloor_HoldsWhereTheyWereLooking()
        {
            // A fisher who stops keeps looking where they were going, rather than snapping to the bow.
            Assert.AreEqual(214f, DeckRiderFacingMath.DeckBearing(new Vector2(0.01f, -0.02f), 0.05f, 214f), Eps);
            Assert.AreEqual(214f, DeckRiderFacingMath.DeckBearing(Vector2.zero, 0.05f, 214f), Eps,
                            "and a fisher standing perfectly still most of all");
        }

        // ---- the composition ----------------------------------------------------------------------

        [Test]
        public void CompassHeading_IsTheHullsHeadingPlusTheDeckBearing_WrappedToABearing()
        {
            Assert.AreEqual(0f, DeckRiderFacingMath.CompassHeading(0f, 0f), Eps,
                            "bow-north hull, fisher facing the bow: they face north");
            Assert.AreEqual(90f, DeckRiderFacingMath.CompassHeading(90f, 0f), Eps,
                            "swing her bow east and the fisher goes with her");
            Assert.AreEqual(180f, DeckRiderFacingMath.CompassHeading(90f, 90f), Eps,
                            "east-bound hull, fisher looking to starboard: they look south");
            Assert.AreEqual(10f, DeckRiderFacingMath.CompassHeading(350f, 20f), Eps,
                            "and the sum wraps rather than reading 370");
            Assert.AreEqual(270f, DeckRiderFacingMath.CompassHeading(0f, -90f), Eps,
                            "a negative bearing wraps into a bearing too");
        }

        [Test]
        public void DeckBearingFor_IsTheExactInverse_SoSteppingAboardIsContinuous()
        {
            // Seeding the bearing from the facing the fisher ARRIVED with is what stops boarding spinning
            // them round to face forward. That only works if the seed is the exact inverse.
            for (int compass = 0; compass < 360; compass += 7)
            {
                for (int hull = 0; hull < 360; hull += 13)
                {
                    float bearing = DeckRiderFacingMath.DeckBearingFor(compass, hull);
                    Assert.AreEqual(compass, DeckRiderFacingMath.CompassHeading(hull, bearing), 1e-3f,
                                    $"seeding at compass {compass} on a hull at {hull} must reproduce {compass}");
                }
            }
        }

        // ---- the headline: no drift, however long she turns ----------------------------------------

        [Test]
        public void AStandingFisher_TracksTenFullTurns_WithNoAccumulatedDrift()
        {
            // THE DEFECT, as a number. A fisher standing still holds one deck bearing; their compass facing
            // must therefore be the hull's heading plus that constant, EXACTLY, at every heading she passes
            // through — a thousand steps in, as at the first. Nothing here integrates, so nothing can creep.
            const float bearing = 37.5f;
            float worstError = 0f;
            for (int step = 0; step < 1000; step++)
            {
                float hull = step * 3.6f;                       // 10 complete turns, 0.36° at a time
                float held = DeckRiderFacingMath.DeckBearing(Vector2.zero, 0.05f, bearing);
                float faced = DeckRiderFacingMath.CompassHeading(hull, held);
                float expected = (hull + bearing) % 360f;
                worstError = Mathf.Max(worstError, Mathf.Abs(Mathf.DeltaAngle(faced, expected)));
            }
            Assert.AreEqual(0f, worstError, 1e-3f,
                            "a composed facing cannot drift — if this is nonzero something is integrating");
        }

        [Test]
        public void AWalkingFisher_FacesTheirWalk_WhateverTheHullIsDoing()
        {
            // The other half of the same rule: their own step decides the bearing, and the hull's heading
            // only carries it. A fisher walking to starboard on a south-bound hull looks WEST.
            float bearing = DeckRiderFacingMath.DeckBearing(new Vector2(2.5f, 0f), 0.05f, 0f);
            Assert.AreEqual(270f, DeckRiderFacingMath.CompassHeading(180f, bearing), Eps);
            // …and walking toward the bow of that same hull, they look south with her.
            bearing = DeckRiderFacingMath.DeckBearing(new Vector2(0f, 2.5f), 0.05f, bearing);
            Assert.AreEqual(180f, DeckRiderFacingMath.CompassHeading(180f, bearing), Eps);
        }

        [Test]
        public void TheComposedHeading_IsAlwaysABearing_ForAnyPairOfInputs()
        {
            // It is published as IsoCharacterSprite.HeadingDegrees and read by the sheet-row rule, so it
            // must stay a sane bearing however many turns either side has made.
            for (int hull = -720; hull <= 720; hull += 37)
            {
                for (int bearing = -720; bearing <= 720; bearing += 53)
                {
                    float h = DeckRiderFacingMath.CompassHeading(hull, bearing);
                    Assert.GreaterOrEqual(h, 0f, $"hull {hull}, bearing {bearing}");
                    Assert.Less(h, 360f, $"hull {hull}, bearing {bearing}");
                }
            }
        }
    }
}
