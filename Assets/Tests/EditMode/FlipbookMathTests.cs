using NUnit.Framework;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pins the pure frame-mapping math the trap-kit art rides (<see cref="FlipbookMath"/>): the deck
    /// animals' crawl LOOP and the one-shot splash BURST. The frames and the fps are data on the Defs;
    /// this pins that the mapping from (elapsed, fps, count) to a frame index is exact, wraps/finishes
    /// correctly, and never throws on degenerate art (the empty-slot greybox rule).
    /// </summary>
    public class FlipbookMathTests
    {
        // ---- the crawl loop (frames 0-5 of the deck sheets, per the artist's README) ----

        [Test]
        public void LoopFrame_WalksEveryFrameInOrder_ThenWraps()
        {
            const float fps = 8f;
            const int count = 6;   // the deck sheets' crawl cycle length
            for (int step = 0; step < count * 3; step++)
            {
                double t = (step + 0.5) / fps;   // mid-frame, no boundary ambiguity
                Assert.AreEqual(step % count, FlipbookMath.LoopFrame(t, fps, count),
                    $"loop frame at t={t}");
            }
        }

        [Test]
        public void LoopFrame_ZeroFps_FreezesOnFrameZero()
            => Assert.AreEqual(0, FlipbookMath.LoopFrame(123.4, 0f, 6));

        [Test]
        public void LoopFrame_NegativeElapsed_ClampsToFrameZero()
            => Assert.AreEqual(0, FlipbookMath.LoopFrame(-1.0, 8f, 6));

        [Test]
        public void LoopFrame_NoFrames_SaysNothingToShow()
            => Assert.AreEqual(-1, FlipbookMath.LoopFrame(1.0, 8f, 0));

        // ---- the one-shot splash burst (8 frames, ~16 fps per the artist's brief) ----

        [Test]
        public void OneShotFrame_PlaysEveryFrameOnce_ThenFinishes()
        {
            const float fps = 16f;
            const int count = 8;   // SplashBurst's cell count
            for (int step = 0; step < count; step++)
            {
                double t = (step + 0.5) / fps;
                Assert.AreEqual(step, FlipbookMath.OneShotFrame(t, fps, count), $"burst frame at t={t}");
            }
            Assert.AreEqual(-1, FlipbookMath.OneShotFrame((count + 0.5) / fps, fps, count),
                "past the last frame the burst is over");
        }

        [Test]
        public void OneShotFrame_StartsOnFrameZero()
            => Assert.AreEqual(0, FlipbookMath.OneShotFrame(0.0, 16f, 8));

        [Test]
        public void OneShotFrame_DegenerateArt_NeverPlays()
        {
            Assert.AreEqual(-1, FlipbookMath.OneShotFrame(0.1, 0f, 8), "fps 0 can't advance");
            Assert.AreEqual(-1, FlipbookMath.OneShotFrame(0.1, 16f, 0), "no frames, nothing to show");
        }

        [Test]
        public void OneShotSeconds_IsCountOverFps()
        {
            Assert.AreEqual(0.5f, FlipbookMath.OneShotSeconds(16f, 8), 1e-5f,
                "the 8-frame burst at 16 fps plays half a second");
            Assert.AreEqual(0f, FlipbookMath.OneShotSeconds(0f, 8), "unplayable burst has no span");
        }
    }
}
