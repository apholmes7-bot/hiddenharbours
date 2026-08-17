using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The Eastward read, at the corners.</b> The bubble slides to stay on screen; the tail keeps
    /// pointing at whoever is talking. Pure maths, so the cases that are a nuisance to walk to in-game —
    /// a speaker hard against the left edge, one at the very top of the frame, a bubble wider than the
    /// screen — are assertions here instead of something somebody has to notice.
    /// </summary>
    public class DialogueBubbleLayoutTests
    {
        static readonly Vector2 ScreenSize = new Vector2(1280f, 720f);
        static readonly Vector2 Bubble = new Vector2(400f, 120f);
        const float Margin = 16f;
        const float Tail = 18f;
        const float Inset = 26f;

        static DialogueBubblePlacement Solve(float x, float y)
            => DialogueBubbleLayout.Solve(new Vector2(x, y), Bubble, ScreenSize, Margin, Tail, Inset);

        [Test]
        public void OverASpeakerInTheOpen_TheBubbleIsCentredOnThemAndTheTailPointsStraightDown()
        {
            DialogueBubblePlacement p = Solve(640f, 300f);

            Assert.That(p.BubbleCentre.x, Is.EqualTo(640f).Within(0.01f),
                        "with room on both sides the bubble sits squarely over the speaker");
            Assert.That(p.BubbleCentre.y, Is.EqualTo(300f + Tail + Bubble.y * 0.5f).Within(0.01f),
                        "its bottom edge clears their head by the tail's length");
            Assert.That(p.TailRoot.x, Is.EqualTo(640f).Within(0.01f));
            Assert.That(p.TailRoot.y, Is.EqualTo(p.BubbleCentre.y - Bubble.y * 0.5f).Within(0.01f),
                        "the tail leaves the bubble's bottom edge");
            Assert.That(p.TailPointsDown, Is.True);
        }

        [Test]
        public void AgainstTheLeftEdge_TheBubbleSlidesInButTheTailStaysOnTheSpeaker()
        {
            // 100 px in: far enough left that the bubble must slide, close enough that the tail can
            // still reach them (past the inset it parks — that is the next test).
            DialogueBubblePlacement p = Solve(100f, 300f);

            float leftmost = Margin + Bubble.x * 0.5f;
            Assert.That(p.BubbleCentre.x, Is.EqualTo(leftmost).Within(0.01f),
                        "the bubble stops at the margin rather than hanging off the screen");
            Assert.That(p.BubbleCentre.x, Is.GreaterThan(100f), "it genuinely slid off the speaker");
            Assert.That(p.TailRoot.x, Is.EqualTo(100f).Within(0.01f),
                        "⭐ THE WHOLE TRICK: the bubble stopped, the tail did not — it is still aimed at " +
                        "the speaker, which is what stops the bubble reading as somebody else's");
            Assert.That(p.TailRoot.x, Is.GreaterThanOrEqualTo(p.BubbleCentre.x - Bubble.x * 0.5f),
                        "and it is still ON the bubble");
        }

        [Test]
        public void AgainstTheRightEdge_TheSameHolds()
        {
            DialogueBubblePlacement p = Solve(ScreenSize.x - 80f, 300f);

            Assert.That(p.BubbleCentre.x, Is.EqualTo(ScreenSize.x - Margin - Bubble.x * 0.5f).Within(0.01f));
            Assert.That(p.BubbleCentre.x, Is.LessThan(ScreenSize.x - 80f), "it genuinely slid off the speaker");
            Assert.That(p.TailRoot.x, Is.EqualTo(ScreenSize.x - 80f).Within(0.01f));
            Assert.That(p.TailRoot.x, Is.LessThanOrEqualTo(p.BubbleCentre.x + Bubble.x * 0.5f));
        }

        [Test]
        public void FarOutsideTheBubblesWidth_TheTailStopsAtTheInsetRatherThanLeavingTheCorner()
        {
            // A speaker in the very corner: the bubble can only lean so far before the tail would be
            // hanging off its own rounded corner. Past that the tail parks at the inset — the honest
            // limit, and the one thing that must never happen is a tail drawn off the bubble.
            DialogueBubblePlacement p = Solve(0f, 300f);

            float left = p.BubbleCentre.x - Bubble.x * 0.5f;
            Assert.That(p.TailRoot.x, Is.EqualTo(left + Inset).Within(0.01f));
            Assert.That(p.TailRoot.x, Is.GreaterThan(left),
                        "a tail past the bubble's own edge would be drawn on nothing");
        }

        [Test]
        public void ASpeakerNearTheTop_FlipsTheBubbleUnderThemAndTurnsTheTailOver()
        {
            DialogueBubblePlacement p = Solve(640f, ScreenSize.y - 20f);

            Assert.That(p.TailPointsDown, Is.False,
                        "there is no headroom above them, so the bubble hangs below and the tail points up");
            Assert.That(p.BubbleCentre.y, Is.LessThan(ScreenSize.y - 20f),
                        "and the bubble is genuinely below the speaker, not merely clamped over them");
            Assert.That(p.TailRoot.y, Is.EqualTo(p.BubbleCentre.y + Bubble.y * 0.5f).Within(0.01f),
                        "a flipped tail leaves the TOP edge");
        }

        [Test]
        public void ASpeakerLowDown_KeepsTheBubbleAboveThem()
        {
            DialogueBubblePlacement p = Solve(640f, 40f);

            Assert.That(p.TailPointsDown, Is.True, "there is plenty of room above — no reason to flip");
            Assert.That(p.BubbleCentre.y, Is.GreaterThan(40f));
        }

        [Test]
        public void EveryPlacementStaysInsideTheMargins_SweptOverTheWholeScreenAndPastIt()
        {
            // The property, rather than a case: wherever the speaker is — including off the edges, which
            // happens for a frame as somebody walks out of shot — the bubble is on screen.
            for (float x = -200f; x <= ScreenSize.x + 200f; x += 37f)
            for (float y = -200f; y <= ScreenSize.y + 200f; y += 41f)
            {
                DialogueBubblePlacement p = Solve(x, y);
                Assert.That(p.BubbleCentre.x - Bubble.x * 0.5f, Is.GreaterThanOrEqualTo(Margin - 0.01f),
                            $"bubble left edge escaped at ({x}, {y})");
                Assert.That(p.BubbleCentre.x + Bubble.x * 0.5f,
                            Is.LessThanOrEqualTo(ScreenSize.x - Margin + 0.01f),
                            $"bubble right edge escaped at ({x}, {y})");
                Assert.That(p.BubbleCentre.y - Bubble.y * 0.5f, Is.GreaterThanOrEqualTo(Margin - 0.01f),
                            $"bubble bottom edge escaped at ({x}, {y})");
                Assert.That(p.BubbleCentre.y + Bubble.y * 0.5f,
                            Is.LessThanOrEqualTo(ScreenSize.y - Margin + 0.01f),
                            $"bubble top edge escaped at ({x}, {y})");

                Assert.That(p.TailRoot.x, Is.GreaterThanOrEqualTo(p.BubbleCentre.x - Bubble.x * 0.5f - 0.01f),
                            $"tail left the bubble at ({x}, {y})");
                Assert.That(p.TailRoot.x, Is.LessThanOrEqualTo(p.BubbleCentre.x + Bubble.x * 0.5f + 0.01f),
                            $"tail left the bubble at ({x}, {y})");
            }
        }

        [Test]
        public void ABubbleWiderThanTheScreen_IsCentredRatherThanFightingTwoClamps()
        {
            var wide = new Vector2(ScreenSize.x + 200f, 120f);
            DialogueBubblePlacement p =
                DialogueBubbleLayout.Solve(new Vector2(100f, 300f), wide, ScreenSize, Margin, Tail, Inset);

            Assert.That(p.BubbleCentre.x, Is.EqualTo(ScreenSize.x * 0.5f).Within(0.01f),
                        "two opposing clamps with no room between them would jitter; centring does not");
        }

        [Test]
        public void AnAnchorWellOffScreen_IsReportedAsSuch_SoThePresenterCanHideRatherThanPointAtNobody()
        {
            Assert.That(DialogueBubbleLayout.AnchorIsOnScreen(new Vector2(640f, 300f), ScreenSize, Margin),
                        Is.True);
            Assert.That(DialogueBubbleLayout.AnchorIsOnScreen(new Vector2(-400f, 300f), ScreenSize, Margin),
                        Is.False);
            Assert.That(DialogueBubbleLayout.AnchorIsOnScreen(new Vector2(640f, ScreenSize.y + 400f), ScreenSize,
                                                             Margin), Is.False);
            Assert.That(DialogueBubbleLayout.AnchorIsOnScreen(new Vector2(-4f, 300f), ScreenSize, Margin),
                        Is.True, "a speaker a few pixels past the edge is still worth pointing at — the " +
                                 "tolerance is the margin, so the bubble does not blink at the frame line");
        }
    }
}
