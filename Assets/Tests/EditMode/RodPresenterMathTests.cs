using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rod-fight presenter's PURE mapping (<see cref="RodPresenterMath"/>): which elements each
    /// phase draws, which rod sheet each fisher pose pairs with, the shadow's circling geometry, the
    /// cast arc, the taut-line read and the safe sheet indexing — the whole what/where choreography,
    /// headless.
    /// </summary>
    public class RodPresenterMathTests
    {
        [Test]
        public void ElementsFor_DrawsTheRightShowPerPhase()
        {
            Assert.AreEqual(RodElements.Rod, RodPresenterMath.ElementsFor(FishingPhase.WindBack, false),
                "the wind-back is rod-only — no line is out yet");
            Assert.AreEqual(RodElements.Rod | RodElements.Line | RodElements.Bobber,
                RodPresenterMath.ElementsFor(FishingPhase.Cast, true), "the cast flies the bobber");
            Assert.AreEqual(RodElements.Rod | RodElements.Line | RodElements.Bobber,
                RodPresenterMath.ElementsFor(FishingPhase.Waiting, castPath: true));
            Assert.AreEqual(RodElements.Rod | RodElements.Line,
                RodPresenterMath.ElementsFor(FishingPhase.Waiting, castPath: false),
                "the weighted path has no bobber — the line runs straight down");
            Assert.AreEqual(RodElements.Rod | RodElements.Line | RodElements.SinkRipples,
                RodPresenterMath.ElementsFor(FishingPhase.Sinking, false),
                "the drop shows the count-the-fall rings");
            Assert.AreEqual(RodElements.Rod | RodElements.Line | RodElements.FishShadow,
                RodPresenterMath.ElementsFor(FishingPhase.FightDeep, false),
                "deep: a shape circles the entry, the fish herself unseen");
            Assert.AreEqual(RodElements.Rod | RodElements.Line | RodElements.FishSurface,
                RodPresenterMath.ElementsFor(FishingPhase.FightSurface, false));
            Assert.AreEqual(RodElements.Rod | RodElements.HeldFish,
                RodPresenterMath.ElementsFor(FishingPhase.Landed, false),
                "landed: the catch is IN HAND — no line to nowhere");
            foreach (var p in new[] { FishingPhase.Idle, FishingPhase.Snapped, FishingPhase.NoBite,
                                      FishingPhase.Tending })
                Assert.AreEqual(RodElements.None, RodPresenterMath.ElementsFor(p, true),
                    $"{p} draws nothing — results/toasts own those beats");
        }

        [Test]
        public void RodSheetFor_PairsEveryPose_AndNoneFallsToHold()
        {
            Assert.AreEqual(0, RodPresenterMath.RodSheetFor(FishingPose.None),
                "an unposed body with the line out still holds the rod (the walking wait)");
            Assert.AreEqual(0, RodPresenterMath.RodSheetFor(FishingPose.Hold));
            Assert.AreEqual(1, RodPresenterMath.RodSheetFor(FishingPose.Bite));
            Assert.AreEqual(2, RodPresenterMath.RodSheetFor(FishingPose.Strike));
            Assert.AreEqual(3, RodPresenterMath.RodSheetFor(FishingPose.Reel));
            Assert.AreEqual(4, RodPresenterMath.RodSheetFor(FishingPose.Land));
            Assert.AreEqual(5, RodPresenterMath.RodSheetFor(FishingPose.CastBack));
            Assert.AreEqual(6, RodPresenterMath.RodSheetFor(FishingPose.CastRelease));
        }

        [Test]
        public void HeadingDegrees_UsesTheIsoCompassConvention()
        {
            Assert.AreEqual(0f, RodPresenterMath.HeadingDegrees(0f, 1f), 1e-3f, "north = +Y");
            Assert.AreEqual(90f, RodPresenterMath.HeadingDegrees(1f, 0f), 1e-3f, "east = +X (CW)");
            Assert.AreEqual(180f, Mathf.Abs(RodPresenterMath.HeadingDegrees(0f, -1f)), 1e-3f, "south");
            Assert.AreEqual(-90f, RodPresenterMath.HeadingDegrees(-1f, 0f), 1e-3f, "west");
            Assert.AreEqual(0f, RodPresenterMath.HeadingDegrees(0f, 0f), "zero-safe");
        }

        [Test]
        public void ArcLift_PeaksMidFlight_AndPinsTheEnds()
        {
            Assert.AreEqual(0f, RodPresenterMath.ArcLift(0f, 1f), 1e-4f);
            Assert.AreEqual(1f, RodPresenterMath.ArcLift(0.5f, 1f), 1e-4f, "the lob peaks halfway");
            Assert.AreEqual(0f, RodPresenterMath.ArcLift(1f, 1f), 1e-4f, "touchdown is on the water");
            Assert.AreEqual(0f, RodPresenterMath.ArcLift(0.5f, -1f), "a negative height clamps to flat");
        }

        [Test]
        public void ShadowOffset_CirclesAnIsoSquashedEllipse_AndSwimsItsTangent()
        {
            Vector2 east = RodPresenterMath.ShadowOffset(0f, 2f, 0.5f);
            Assert.AreEqual(2f, east.x, 1e-4f);
            Assert.AreEqual(0f, east.y, 1e-4f);
            Vector2 north = RodPresenterMath.ShadowOffset(Mathf.PI / 2f, 2f, 0.5f);
            Assert.AreEqual(0f, north.x, 1e-4f);
            Assert.AreEqual(1f, north.y, 1e-4f, "the iso squash flattens the circle's y");
            // At theta 0 (the ellipse's east point) the tangent points north-ish (CCW travel).
            Assert.AreEqual(0f, RodPresenterMath.ShadowHeadingDegrees(0f, 1f), 1e-3f);
        }

        [Test]
        public void TautFor_ReadsTheTells()
        {
            Assert.AreEqual(0f, RodPresenterMath.TautFor(FishingPhase.Sinking, slackWindowOpen: true,
                0f, 0f, 0.35f, 0.7f, 0.4f), "the slack window IS the sag — taut collapses");
            Assert.AreEqual(0.7f, RodPresenterMath.TautFor(FishingPhase.Sinking, false,
                0f, 0f, 0.35f, 0.7f, 0.4f), 1e-4f, "a running drop draws taut-ish");
            Assert.AreEqual(0.35f, RodPresenterMath.TautFor(FishingPhase.Waiting, false,
                0f, 0f, 0.35f, 0.7f, 0.4f), 1e-4f, "the resting bobber line sags gently");
            Assert.AreEqual(0.9f, RodPresenterMath.TautFor(FishingPhase.FightSurface, false,
                0.9f, 0.2f, 0.35f, 0.7f, 0.4f), 1e-4f, "the fight follows the rod-bend read");
            Assert.AreEqual(0.4f, RodPresenterMath.TautFor(FishingPhase.FightDeep, false,
                0f, 0f, 0.35f, 0.7f, 0.4f), 1e-4f, "a fight is never drawn slack without the tell");
        }

        [Test]
        public void FlipFrames_AreDegenerateSafe()
        {
            Assert.AreEqual(0, RodPresenterMath.FlipFrame(0f, 0.1f, 4));
            Assert.AreEqual(2, RodPresenterMath.FlipFrame(0.25f, 0.1f, 4));
            Assert.AreEqual(0, RodPresenterMath.FlipFrame(0.4f, 0.1f, 4), "wraps");
            Assert.AreEqual(0, RodPresenterMath.FlipFrame(1f, 0.1f, 0), "no frames is safe");
            Assert.AreEqual(4, RodPresenterMath.OnceFlipFrame(99f, 0.1f, 4),
                "play-once reports one past the end when finished (the hide signal)");
        }

        [Test]
        public void SheetIndex_GuardsEveryEdge()
        {
            Assert.AreEqual(4 * 6 + 2, RodPresenterMath.SheetIndex(4, 2, 6, 48));
            Assert.AreEqual(-1, RodPresenterMath.SheetIndex(-1, 0, 6, 48), "row under");
            Assert.AreEqual(-1, RodPresenterMath.SheetIndex(0, 6, 6, 48), "frame over");
            Assert.AreEqual(-1, RodPresenterMath.SheetIndex(7, 5, 6, 40), "beyond a short array");
            Assert.AreEqual(-1, RodPresenterMath.SheetIndex(0, 0, 0, 48), "no frames per dir");
        }

        [Test]
        public void IsDarting_SplitsDartFromThrash()
        {
            Assert.IsTrue(RodPresenterMath.IsDarting(2f, 1.2f));
            Assert.IsFalse(RodPresenterMath.IsDarting(0.5f, 1.2f), "a station-holder thrashes");
        }

        private static float Heading(Vector2 v) => RodPresenterMath.HeadingDegrees(v.x, v.y);

        [Test]
        public void FlankRowHeading_ReeledStraightIn_SheIsDrawnAcrossTheLine_OnTheSideSheDrifts()
        {
            Vector2 south = new Vector2(0f, -4f);   // she is south of him; the line runs north-south

            // Straight in, no drift, no side held: across the line, the side whose heading points east.
            int side = 0;
            Vector2 h = RodPresenterMath.FlankRowHeading(new Vector2(0f, 0.5f), south, 30f, ref side);
            Assert.AreEqual(90f, Heading(h), 1e-3f, "reeled straight in she shows her flank, heading east");
            Assert.AreEqual(0.5f, h.magnitude, 1e-5f, "the turned heading keeps her speed");
            Assert.AreEqual(-1, side);

            // A little drift west (inside the angle): she turns to face west, and that side is now held.
            h = RodPresenterMath.FlankRowHeading(new Vector2(-0.05f, 0.5f), south, 30f, ref side);
            Assert.AreEqual(-90f, Heading(h), 1e-3f, "drifting west she faces west");
            Assert.AreEqual(1, side);
            h = RodPresenterMath.FlankRowHeading(new Vector2(0f, 0.5f), south, 30f, ref side);
            Assert.AreEqual(-90f, Heading(h), 1e-3f, "straight in again she keeps the side she showed");

            // Running away from him down the line is the same end-on picture, so it is turned too.
            h = RodPresenterMath.FlankRowHeading(new Vector2(0f, -0.5f), south, 30f, ref side);
            Assert.AreEqual(-90f, Heading(h), 1e-3f, "running out along the line she is still side-on");
        }

        [Test]
        public void FlankRowHeading_OffTheLine_OrTurnedOff_HerTravelPicksTheRow()
        {
            Vector2 south = new Vector2(0f, -4f);
            int side = 0;
            Vector2 diag = new Vector2(0.5f, 0.5f);   // 45 degrees off the line
            Assert.AreEqual(diag, RodPresenterMath.FlankRowHeading(diag, south, 30f, ref side),
                "outside the angle her own travel picks the row");
            Assert.AreEqual(-1, side, "her drift east is remembered as the side she shows next");

            Vector2 inside = new Vector2(0.2f, 0.5f);  // about 22 degrees off the line
            side = 0;
            Assert.AreEqual(inside, RodPresenterMath.FlankRowHeading(inside, south, 0f, ref side),
                "0 turns the rule off");
            Assert.AreEqual(inside, RodPresenterMath.FlankRowHeading(inside, Vector2.zero, 30f, ref side),
                "no line axis (she is at his feet): her travel");
            Assert.AreEqual(Vector2.zero, RodPresenterMath.FlankRowHeading(Vector2.zero, south, 30f, ref side),
                "no travel: nothing to turn");

            // The edge: exactly at the angle is still turned; just past it is not.
            float rad = 29.9f * Mathf.Deg2Rad;
            Vector2 at = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            side = 0;
            Assert.AreNotEqual(at, RodPresenterMath.FlankRowHeading(at, south, 30f, ref side), "29.9 degrees is inside 30");
            rad = 30.1f * Mathf.Deg2Rad;
            Vector2 past = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            Assert.AreEqual(past, RodPresenterMath.FlankRowHeading(past, south, 30f, ref side), "30.1 degrees is outside 30");
        }

        [Test]
        public void FlankRowHeading_ALineRunningEastWest_DefaultsToTheNorthSide()
        {
            int side = 0;
            Vector2 h = RodPresenterMath.FlankRowHeading(new Vector2(-0.5f, 0f), new Vector2(4f, 0f), 30f, ref side);
            Assert.AreEqual(0f, Heading(h), 1e-3f, "east of him, reeled straight in, she shows the side heading north");
        }

        [Test]
        public void FlankWithinLineDegrees_ShipsAt30()
        {
            Assert.AreEqual(30f, FishFeelSettings.Default.FlankWithinLineDegrees,
                "the side-on angle's reference value moved");
        }
    }
}
