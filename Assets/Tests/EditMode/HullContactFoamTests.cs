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
        public void Ahead_TheBowWave_PartsRoundHerStem_AlongEachFlank()
        {
            // The owner, 2026-11-16: "the bow foam looks irregular its sqaure". One bar across the stem,
            // its back half under her hull, left a square slab ahead of her; the bow wave is now two arms
            // that meet at the stem and run aft along her tapering waterline.
            HullContactFoam.Stamp port = HullContactFoam.Lead(3f, HalfLength, HalfBeam, in S, starboardArm: false);
            HullContactFoam.Stamp star = HullContactFoam.Lead(3f, HalfLength, HalfBeam, in S, starboardArm: true);
            Assert.IsTrue(port.IsActive && star.IsActive);
            float stem = HalfBeam * S.ContactStemHalfBeams;
            Assert.AreEqual(HalfLength, port.From.y, 1e-6f, "each arm starts at her stem");
            Assert.AreEqual(HalfLength, star.From.y, 1e-6f);
            Assert.AreEqual(-stem, port.From.x, 1e-6f, "at the stem the arms are as close as her waterline");
            Assert.AreEqual(stem, star.From.x, 1e-6f);
            Assert.Less(port.To.y, HalfLength, "and run aft");
            Assert.AreEqual(port.To.y, star.To.y, 1e-6f, "the two arms are mirror images");
            Assert.AreEqual(-port.To.x, star.To.x, 1e-6f);
            Assert.AreEqual(HalfBeam * S.LeadSpanHalfBeams, star.To.x, 1e-6f, "out to the span");
            Assert.LessOrEqual(star.To.x, HalfBeam, "inside her beam");

            // Every point of an arm lies on her waterline as the foam transport draws it: her half-beam aft
            // of the taper, narrowing straight to the stem.
            float taperFrom = HalfLength - 2f * HalfLength * S.ContactBowTaper;
            for (float t = 0f; t <= 1f; t += 0.125f)
            {
                Vector2 p = Vector2.Lerp(star.From, star.To, t);
                float along01 = Mathf.Clamp01((p.y - taperFrom) / (HalfLength - taperFrom));
                float waterline = HalfBeam * (1f - along01 * (1f - S.ContactStemHalfBeams));
                Assert.AreEqual(waterline, p.x, 1e-4f, $"at {t:0.000} of the arm");
            }
        }

        [Test]
        public void Ahead_NoPartOfTheBowWave_IsASlabAheadOfHerStem()
        {
            // Nothing a capsule lays reaches further ahead of her stem than its own radius: a rounded point,
            // never a bar the width of her bow.
            foreach (bool starboard in new[] { false, true })
            {
                HullContactFoam.Stamp arm = HullContactFoam.Lead(3f, HalfLength, HalfBeam, in S, starboard);
                Assert.LessOrEqual(Mathf.Max(arm.From.y, arm.To.y), HalfLength + 1e-6f);
                float acrossAtStem = Mathf.Abs(arm.From.y >= arm.To.y ? arm.From.x : arm.To.x);
                Assert.Less(acrossAtStem, 0.5f * HalfBeam * S.LeadSpanHalfBeams,
                    "the end at her stem sits on her centreline's side of the span, not out at a bar's end");
            }
        }
        [Test]
        public void Astern_TheTransomThrowsTheLeadingFoam()
        {
            HullContactFoam.Stamp lead = HullContactFoam.Lead(-3f, HalfLength, HalfBeam, in S, false);
            Assert.IsTrue(lead.IsActive);
            Assert.AreEqual(-HalfLength, lead.From.y, 1e-6f);
            Assert.AreEqual(-HalfLength, lead.To.y, 1e-6f);
            Assert.Less(lead.From.x, 0f, "astern it is still a bar across her square transom");
            Assert.Greater(lead.To.x, 0f);
        }

        [Test]
        public void TheLeadingFoam_GrowsWithSpeed_AndIsNoneAtRest()
        {
            Assert.IsFalse(HullContactFoam.Lead(0f, HalfLength, HalfBeam, in S, true).IsActive);
            float slow = HullContactFoam.Lead(0.5f, HalfLength, HalfBeam, in S, true).Rate01;
            float fast = HullContactFoam.Lead(2.5f, HalfLength, HalfBeam, in S, true).Rate01;
            Assert.Greater(slow, 0f);
            Assert.Greater(fast, slow);
            Assert.LessOrEqual(HullContactFoam.Lead(50f, HalfLength, HalfBeam, in S, true).Rate01, 1f);
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
            Assert.IsFalse(HullContactFoam.Lead(5f, HalfLength, HalfBeam, in off, true).IsActive);
            Assert.IsFalse(HullContactFoam.Side(2f, 1f, HalfLength, HalfBeam, in off).IsActive);
        }

        [Test]
        public void AHullWithNoMeasuredLength_LaysNoContactFoam()
        {
            Assert.IsFalse(HullContactFoam.Lead(5f, 0f, HalfBeam, in S, true).IsActive);
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
