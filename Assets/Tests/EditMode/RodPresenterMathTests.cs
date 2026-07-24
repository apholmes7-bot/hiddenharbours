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
    }
}
