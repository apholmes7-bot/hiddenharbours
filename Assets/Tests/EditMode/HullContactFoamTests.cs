using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The whole hull makes foam (the owner, 2026-11-02: "i would like the whole hull body to be responsive
    /// to making wake/foam, right now it just comes from stern from moving forward and looks off when going
    /// in reverse"). These pin the three laws of <see cref="HullContactFoam"/>: which end sheds the trail,
    /// which end throws the leading foam, and which side foams when she slides or turns.
    /// </summary>
    public sealed class HullContactFoamTests
    {
        const float HalfLength = 3f, HalfBeam = 1f;
        static readonly HullFoamSettings S = HullFoamSettings.Default;

        // ---- the trail is shed from the end that trails --------------------------------------------

        [Test]
        public void Ahead_TheTrailIsShedFromTheTransom()
            => Assert.IsFalse(HullContactFoam.TrailFromBow(2f, wasFromBow: true, in S));

        [Test]
        public void Astern_TheTrailIsShedFromTheBow()
            => Assert.IsTrue(HullContactFoam.TrailFromBow(-2f, wasFromBow: false, in S));

        [Test]
        public void AtRest_TheTrailKeepsTheEndItHad()
        {
            float inside = S.TrailFlipSpeed * 0.5f;
            Assert.IsTrue(HullContactFoam.TrailFromBow(-inside, true, in S));
            Assert.IsTrue(HullContactFoam.TrailFromBow(inside, true, in S));
            Assert.IsFalse(HullContactFoam.TrailFromBow(-inside, false, in S));
            Assert.IsFalse(HullContactFoam.TrailFromBow(inside, false, in S));
        }

        [Test]
        public void AZeroedFlipSpeed_StillHasADeadBand()
        {
            var zero = S; zero.TrailFlipSpeed = 0f;
            Assert.IsFalse(HullContactFoam.TrailFromBow(-0.01f, false, in zero),
                "a hull drifting a centimetre a second astern must not swap ends");
            Assert.IsTrue(HullContactFoam.TrailFromBow(-1f, false, in zero));
        }

        // ---- the leading end ---------------------------------------------------------------------

        [Test]
        public void Ahead_TheBowThrowsTheLeadingFoam_AcrossHer()
        {
            HullContactFoam.Stamp lead = HullContactFoam.Lead(3f, HalfLength, HalfBeam, in S);
            Assert.IsTrue(lead.IsActive);
            Assert.AreEqual(HalfLength, lead.From.y, 1e-6f);
            Assert.AreEqual(HalfLength, lead.To.y, 1e-6f);
            Assert.Less(lead.From.x, 0f);
            Assert.Greater(lead.To.x, 0f);
            Assert.LessOrEqual(lead.To.x, HalfBeam, "the bar stays inside her beam");
        }

        [Test]
        public void Astern_TheTransomThrowsTheLeadingFoam()
        {
            HullContactFoam.Stamp lead = HullContactFoam.Lead(-3f, HalfLength, HalfBeam, in S);
            Assert.IsTrue(lead.IsActive);
            Assert.AreEqual(-HalfLength, lead.From.y, 1e-6f);
            Assert.AreEqual(-HalfLength, lead.To.y, 1e-6f);
        }

        [Test]
        public void TheLeadingFoam_GrowsWithSpeed_AndIsNoneAtRest()
        {
            Assert.IsFalse(HullContactFoam.Lead(0f, HalfLength, HalfBeam, in S).IsActive);
            float slow = HullContactFoam.Lead(0.5f, HalfLength, HalfBeam, in S).Rate01;
            float fast = HullContactFoam.Lead(2.5f, HalfLength, HalfBeam, in S).Rate01;
            Assert.Greater(slow, 0f);
            Assert.Greater(fast, slow);
            Assert.LessOrEqual(HullContactFoam.Lead(50f, HalfLength, HalfBeam, in S).Rate01, 1f);
        }

        // ---- the pushed side ---------------------------------------------------------------------

        [Test]
        public void SlidingToStarboard_FoamsTheWholeStarboardSide()
        {
            HullContactFoam.Stamp side = HullContactFoam.Side(1f, 0f, HalfLength, HalfBeam, in S);
            Assert.IsTrue(side.IsActive);
            Assert.AreEqual(HalfBeam, side.From.x, 1e-6f);
            Assert.AreEqual(HalfBeam, side.To.x, 1e-6f);
            Assert.Less(side.From.y, 0f);
            Assert.Greater(side.To.y, 0f);
        }

        [Test]
        public void SlidingToPort_FoamsThePortSide()
        {
            HullContactFoam.Stamp side = HullContactFoam.Side(-1f, 0f, HalfLength, HalfBeam, in S);
            Assert.AreEqual(-HalfBeam, side.From.x, 1e-6f);
            Assert.AreEqual(-HalfBeam, side.To.x, 1e-6f);
        }

        [Test]
        public void PivotingToPort_FoamsOppositeQuarters_BowToPort_SternToStarboard()
        {
            // Counter-clockwise: her bow swings to port, her stern to starboard.
            HullContactFoam.Stamp side = HullContactFoam.Side(0f, 0.6f, HalfLength, HalfBeam, in S);
            Assert.IsTrue(side.IsActive);
            Assert.Greater(side.To.y, 0f, "To is the bow quarter");
            Assert.AreEqual(-HalfBeam, side.To.x, 1e-6f);
            Assert.AreEqual(HalfBeam, side.From.x, 1e-6f);
        }

        [Test]
        public void LateralSpeed_IsSwayPlusWhatTheTurnAddsAtThatEnd()
        {
            Assert.AreEqual(1f, HullContactFoam.LateralSpeedAt(1f, 0f, 2f), 1e-6f);
            Assert.AreEqual(-1f, HullContactFoam.LateralSpeedAt(0f, 0.5f, 2f), 1e-6f);
            Assert.AreEqual(1f, HullContactFoam.LateralSpeedAt(0f, 0.5f, -2f), 1e-6f);
        }

        [Test]
        public void RunningDeadStraight_TheSidesAreQuiet()
            => Assert.IsFalse(HullContactFoam.Side(0f, 0f, HalfLength, HalfBeam, in S).IsActive);

        // ---- the dials -----------------------------------------------------------------------------

        [Test]
        public void ZeroWeights_LayNothing_TheGameAsItShipped()
        {
            var off = S; off.LeadWeight = 0f; off.SideWeight = 0f;
            Assert.IsFalse(HullContactFoam.Lead(5f, HalfLength, HalfBeam, in off).IsActive);
            Assert.IsFalse(HullContactFoam.Side(2f, 1f, HalfLength, HalfBeam, in off).IsActive);
        }

        [Test]
        public void AHullWithNoMeasuredLength_LaysNoContactFoam()
        {
            Assert.IsFalse(HullContactFoam.Lead(5f, 0f, HalfBeam, in S).IsActive);
            Assert.IsFalse(HullContactFoam.Side(2f, 1f, 0f, HalfBeam, in S).IsActive);
        }

        [Test]
        public void TheDefaultSettings_AreTheOnesGameConfigShips()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(S.LeadWeight, config.HullFoam.LeadWeight);
                Assert.AreEqual(S.SideWeight, config.HullFoam.SideWeight);
                Assert.AreEqual(S.TrailFlipSpeed, config.HullFoam.TrailFlipSpeed);
            }
            finally { Object.DestroyImmediate(config); }
        }

        // ---- the hull frame, as drawn --------------------------------------------------------------

        [Test]
        public void AHullPoint_IsDrawnForeshortened_TheWayHerArtIs()
        {
            Vector2 centre = new Vector2(10f, 20f);
            // Heading north at a 30 degree bake: a point 4 m ahead is drawn 2 m up the screen.
            Vector2 bow = HullContactFoam.ToDrawn(centre, Vector2.up, new Vector2(0f, 4f), 30f);
            Assert.AreEqual(10f, bow.x, 1e-5f);
            Assert.AreEqual(22f, bow.y, 1e-5f);
            // Starboard of a hull heading north is east, and east-west is not foreshortened.
            Vector2 starboard = HullContactFoam.ToDrawn(centre, Vector2.up, new Vector2(1f, 0f), 30f);
            Assert.AreEqual(11f, starboard.x, 1e-5f);
            Assert.AreEqual(20f, starboard.y, 1e-5f);
            // Heading east, her starboard side is south, and that is foreshortened.
            Vector2 south = HullContactFoam.ToDrawn(centre, Vector2.right, new Vector2(1f, 0f), 30f);
            Assert.AreEqual(10f, south.x, 1e-5f);
            Assert.AreEqual(19.5f, south.y, 1e-5f);
        }

        [Test]
        public void TheBowPoint_AgreesWithTheWakeRootProjection()
        {
            Vector2 centre = new Vector2(-3f, 7f);
            Vector2 heading = new Vector2(0.6f, 0.8f);
            Vector2 mine = HullContactFoam.ToDrawn(centre, heading, new Vector2(0f, 2.5f), 40f);
            Vector2 theirs = WakeRootMath.ProjectAlongHeading(centre, heading, 2.5f, 40f);
            Assert.AreEqual(theirs.x, mine.x, 1e-5f);
            Assert.AreEqual(theirs.y, mine.y, 1e-5f);
        }
    }
}
