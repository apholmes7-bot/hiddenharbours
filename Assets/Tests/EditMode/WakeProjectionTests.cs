using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE WAKE LANDS ON THE TRANSOM AT EVERY HEADING — the owner's "see the skiff wake in the screenshot? not
    /// even connected to it and way off to the stern", and, for the dory, "the wake from the rowboat still
    /// doesnt <b>always</b> seem accurate". Always was the word that mattered: the placement was only ever right
    /// on the E/W axis.
    ///
    /// <para><b>The bug.</b> <c>SternAnchor</c> walked half a hull astern in honest top-down world metres, but
    /// the hull is DRAWN by a 40° camera, which squashes along-heading distance on screen-Y by sin(40°) ≈ 0.643
    /// and leaves screen-X alone. So a skiff's apex went 3.80 m astern while her drawn transom was only 2.20 m
    /// astern at N — a 1.6 m hole — and the two coincided at E. <b>The gap breathed as she turned.</b></para>
    ///
    /// <para><b>What is asserted, and why not the obvious thing.</b> Not "the gap is a constant number of screen
    /// metres" — it isn't, and shouldn't be: 0.3 m of water seen end-on IS shorter on screen than 0.3 m seen
    /// broadside, and that is the projection being right rather than wrong. What must hold is that the anchor
    /// lands where the ART would draw a point <c>PlumeAsternOffset</c> metres astern of the transom. So these
    /// cases pin the anchor against <c>MountedRockPoseMath.Project</c> — a straight port of the rigs' own
    /// <c>projVert</c>, arrived at from a completely different direction — rather than against the wake code's
    /// own arithmetic. Two derivations agreeing is evidence; one derivation agreeing with itself is what let
    /// #212 ship green.</para>
    /// </summary>
    public class WakeProjectionTests
    {
        private const float IsoElev = 40f;    // every iso rig's DEFAULT_ELEV
        private const float PlanElev = 90f;   // "not a bake" — the hand-drawn compass
        private const float SkiffLength = 7.0f, DoryLength = 4.5f, Astern = 0.3f;

        private static Vector2 BowFromHeading(float headingDeg)
            => new Vector2(Mathf.Sin(headingDeg * Mathf.Deg2Rad), Mathf.Cos(headingDeg * Mathf.Deg2Rad));

        // ==== THE BUG, in the numbers the owner is looking at =============================================

        [Test]
        public void SternAnchor_AtNorth_LandsAtTheDRAWNTransom_NotSevenPixelsPastIt()
        {
            // Heading N: the drawn transom is 3.5 m astern ON THE WATER, which the ¾ camera puts at
            // 3.5·sin(40°) = 2.25 m of screen. The old top-down anchor went the full 3.8 m — 1.55 m (≈50 px)
            // of open water between the boat and her own wake.
            Vector2 apex = WakeGrading.SternAnchor(Vector2.zero, Vector2.up, SkiffLength, Astern, IsoElev);

            float drawnTransom = -(SkiffLength * 0.5f) * Mathf.Sin(IsoElev * Mathf.Deg2Rad);   // −2.250
            float expected = -(SkiffLength * 0.5f + Astern) * Mathf.Sin(IsoElev * Mathf.Deg2Rad);  // −2.443

            Assert.AreEqual(0f, apex.x, 1e-4f);
            Assert.AreEqual(expected, apex.y, 1e-3f,
                "the apex must be projected the way the hull is drawn. The old code put it at −3.80 here, " +
                "1.55 m clear of a transom drawn at −2.25 — the owner's 'not even connected to it'.");
            Assert.Greater(apex.y, -3.7f, "IF THIS IS RED the anchor is back in top-down metres");

            float gap = drawnTransom - apex.y;
            Assert.Greater(gap, 0f, "the plume still starts BEHIND the transom, never under the hull");
            Assert.Less(gap, Astern + 1e-3f, "…and by no more than the nudge, seen foreshortened");
        }

        [Test]
        public void SternAnchor_AtEast_IsUnchanged_BecauseNothingIsForeshortenedThere()
        {
            // Broadside, the along-heading offset runs entirely across screen-X, which the camera does not
            // touch. This is why the bug hid: the only heading anyone checks is the one where it is invisible.
            Vector2 apex = WakeGrading.SternAnchor(Vector2.zero, Vector2.right, SkiffLength, Astern, IsoElev);
            Assert.AreEqual(-(SkiffLength * 0.5f + Astern), apex.x, 1e-4f, "the full 3.80 m, unsquashed");
            Assert.AreEqual(0f, apex.y, 1e-4f);
        }

        [Test]
        public void SternAnchor_TheGapFromTheDRAWNTransom_NoLongerBreathesAsSheTurns()
        {
            // THE BUG, stated as an invariant. Measure the anchor and the drawn transom the SAME way (both
            // projected), and the plume's stand-off is the same fraction of the hull at every heading. Before
            // the fix this ratio ran from 1.086 at E to 1.689 at N.
            float expectedRatio = (SkiffLength * 0.5f + Astern) / (SkiffLength * 0.5f);   // 1.0857

            // The transom is located by the ART RIG's projection, NOT by the wake's own — measuring both ends
            // with the same ruler is precisely how #212 stayed green while shipping the bug. If the wake code
            // reverts to top-down, this ratio breathes and the assert catches it.
            for (float h = 0f; h < 360f; h += 7.5f)
            {
                Vector2 bow = BowFromHeading(h);
                Vector2 apex = WakeGrading.SternAnchor(Vector2.zero, bow, SkiffLength, Astern, IsoElev);
                Vector2 transom = MountedRockPoseMath.Project(new Vector3(0f, -SkiffLength * 0.5f, 0f),
                                                             -h * Mathf.Deg2Rad, 0f, 0f, IsoElev * Mathf.Deg2Rad);

                Assert.AreEqual(expectedRatio, apex.magnitude / transom.magnitude, 1e-3f,
                    $"heading {h}: the apex must sit the same relative stand-off past the DRAWN transom at " +
                    "EVERY heading. Before the fix this ran from 1.086 at E to 1.689 at N — the gap breathing " +
                    "as she turned is the bug the owner saw.");
            }
        }

        // ==== assert against the ART, not against ourselves ===============================================

        [Test]
        public void SternAnchor_AgreesWithTheARTRIGSOwnProjection_AtEveryHeading()
        {
            // The rigs project a boat-local point through projVert at turntable angle θ. #212 established that
            // cell i depicts heading −45°·i — i.e. θ = −heading. If the wake's world-space foreshortening and
            // the rig's own camera are both right, they must agree for EVERY continuous heading, and that
            // relationship (θ = −h) falls straight out. Two independent derivations, one answer.
            float back = SkiffLength * 0.5f + Astern;
            var local = new Vector3(0f, -back, 0f);          // dead astern, on the centreline, at the waterline
            var pos = new Vector2(3f, -2f);

            for (float h = 0f; h < 360f; h += 5f)
            {
                Vector2 apex = WakeGrading.SternAnchor(pos, BowFromHeading(h), SkiffLength, Astern, IsoElev);
                Vector2 rig = pos + MountedRockPoseMath.Project(local, -h * Mathf.Deg2Rad, 0f, 0f,
                                                                IsoElev * Mathf.Deg2Rad);
                Assert.AreEqual(rig.x, apex.x, 1e-3f, $"heading {h}: x disagrees with the rig's own camera");
                Assert.AreEqual(rig.y, apex.y, 1e-3f, $"heading {h}: y disagrees with the rig's own camera");
            }
        }

        // ==== the OTHER art lineage must not move =========================================================

        [Test]
        public void PlanViewArt_IsNotForeshortened_TheFishingBoatAndTheAmbientFleetDoNotMove()
        {
            // The hand-drawn FishingBoat_* compass is not a rig bake and has no camera to measure; the whole
            // ambient fleet wears those facings. Foreshortening it would be inventing a projection it never
            // had — the exact trap #212 avoided by making the mirror per-artwork instead of global.
            for (float h = 0f; h < 360f; h += 15f)
            {
                Vector2 bow = BowFromHeading(h);
                Vector2 apex = WakeGrading.SternAnchor(Vector2.zero, bow, 6f, 0.25f, PlanElev);
                Vector2 old = -bow.normalized * (3f + 0.25f);     // the pre-fix, top-down placement, verbatim
                Assert.AreEqual(old.x, apex.x, 1e-4f, $"heading {h}: plan-view art must be placed exactly as before");
                Assert.AreEqual(old.y, apex.y, 1e-4f, $"heading {h}");
            }
        }

        [Test]
        public void ForeshortenY_90IsTheIdentity_AndBadDataDegradesToIt()
        {
            Assert.AreEqual(1f, WakeGrading.ForeshortenY(90f), 1e-6f, "a plan view squashes nothing");
            Assert.AreEqual(Mathf.Sin(40f * Mathf.Deg2Rad), WakeGrading.ForeshortenY(40f), 1e-6f);

            // The #212 trap: a committed .asset that predates the field deserialises it to ZERO, and sin(0) = 0
            // would collapse every anchor onto the boat's own middle. Non-positive degrades to the OLD
            // behaviour, which is wrong-but-familiar rather than catastrophic. (The shipped assets carry the
            // field explicitly — see PilotableFleetContentTests — this is the belt to that pair of braces.)
            Assert.AreEqual(1f, WakeGrading.ForeshortenY(0f), 1e-6f, "a zeroed field must not staple the wake to the hull");
            Assert.AreEqual(1f, WakeGrading.ForeshortenY(-5f), 1e-6f);
            Assert.AreEqual(1f, WakeGrading.ForeshortenY(float.NaN), 1e-6f);
        }

        // ==== the bow spray is the same fix at the other end ==============================================

        [Test]
        public void BowAnchor_IsProjectedToo_AndMirrorsTheStern()
        {
            Vector2 impact = BowSprayGrading.BowAnchor(Vector2.zero, Vector2.up, DoryLength, 0.05f, IsoElev);
            Assert.AreEqual((DoryLength * 0.5f + 0.05f) * Mathf.Sin(IsoElev * Mathf.Deg2Rad), impact.y, 1e-3f,
                "the cutwater is foreshortened exactly as the transom is — otherwise the spray slides off the " +
                "stem as she turns, for the same reason the plume slid off the transom");

            // The exact mirror: astern and ahead of the same hull are the same point, negated.
            Vector2 bow = BowFromHeading(37f);
            Vector2 s = WakeGrading.SternAnchor(Vector2.zero, bow, DoryLength, 0.2f, IsoElev);
            Vector2 b = BowSprayGrading.BowAnchor(Vector2.zero, bow, DoryLength, 0.2f, IsoElev);
            Assert.AreEqual(-s.x, b.x, 1e-4f);
            Assert.AreEqual(-s.y, b.y, 1e-4f);
        }

        [Test]
        public void DegenerateBow_StillFallsBackToUp_NoNaN()
        {
            Vector2 a = WakeGrading.SternAnchor(Vector2.zero, Vector2.zero, DoryLength, 0.3f, IsoElev);
            Assert.IsFalse(float.IsNaN(a.x) || float.IsNaN(a.y));
            Assert.AreEqual(-(DoryLength * 0.5f + 0.3f) * Mathf.Sin(IsoElev * Mathf.Deg2Rad), a.y, 1e-3f,
                "falls back to +Y as the bow, and is projected like any other heading");
        }
    }
}
