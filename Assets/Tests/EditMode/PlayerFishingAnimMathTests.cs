using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2 Wave 3 — the fisher's fight-animation mapping (<see cref="PlayerFishingAnimMath"/>):
    /// published phase → pose (bite / strike / reel / land), the strike→reel handover at the fight's
    /// opening, loop vs play-once frame maths, and the facing row that follows the published fish
    /// offset (holding its last row when the line runs straight down).
    /// </summary>
    public class PlayerFishingAnimMathTests
    {
        [Test]
        public void PoseFor_MapsEveryPhase()
        {
            const float afterStrike = 10f, strike = 0.5f;
            Assert.AreEqual(FishingPose.Bite, PlayerFishingAnimMath.PoseFor(FishingPhase.Bite, 0f, strike));
            Assert.AreEqual(FishingPose.Reel, PlayerFishingAnimMath.PoseFor(FishingPhase.Fighting, afterStrike, strike),
                "the legacy fight reels too — the sheets serve both fights");
            Assert.AreEqual(FishingPose.Reel, PlayerFishingAnimMath.PoseFor(FishingPhase.FightDeep, afterStrike, strike));
            Assert.AreEqual(FishingPose.Reel, PlayerFishingAnimMath.PoseFor(FishingPhase.FightSurface, afterStrike, strike));
            Assert.AreEqual(FishingPose.Land, PlayerFishingAnimMath.PoseFor(FishingPhase.Landed, 0f, strike));
            // The presenter wave: the cast beats + the rod-out hold own the body too.
            Assert.AreEqual(FishingPose.CastBack, PlayerFishingAnimMath.PoseFor(FishingPhase.WindBack, 0f, strike));
            Assert.AreEqual(FishingPose.CastRelease, PlayerFishingAnimMath.PoseFor(FishingPhase.Cast, 0f, strike));
            Assert.AreEqual(FishingPose.Hold, PlayerFishingAnimMath.PoseFor(FishingPhase.Waiting, 0f, strike));
            Assert.AreEqual(FishingPose.Hold, PlayerFishingAnimMath.PoseFor(FishingPhase.Sinking, 0f, strike));
            // No pose anywhere else — the walk skin owns the renderer.
            foreach (FishingPhase p in new[] { FishingPhase.Idle, FishingPhase.Snapped,
                                               FishingPhase.NoBite, FishingPhase.Tending })
                Assert.AreEqual(FishingPose.None, PlayerFishingAnimMath.PoseFor(p, 0f, strike),
                    $"{p} must not own the renderer (Tending has no rod; a snap goes straight back to walking)");
        }

        [Test]
        public void TheHold_YieldsToAMovingAngler_ButTheShortBeatsDoNot()
        {
            const float strike = 0.5f;
            // Waiting/Sinking: the line can stay out for a long while and the player is free to walk —
            // a moving body must belong to the walk skin (no moonwalking hold pose).
            Assert.AreEqual(FishingPose.None,
                PlayerFishingAnimMath.PoseFor(FishingPhase.Waiting, 0f, strike, stationary: false));
            Assert.AreEqual(FishingPose.None,
                PlayerFishingAnimMath.PoseFor(FishingPhase.Sinking, 0f, strike, stationary: false));
            // The cast/bite/fight/land beats are short and own the renderer regardless (the shipped rule).
            Assert.AreEqual(FishingPose.CastBack,
                PlayerFishingAnimMath.PoseFor(FishingPhase.WindBack, 0f, strike, stationary: false));
            Assert.AreEqual(FishingPose.Bite,
                PlayerFishingAnimMath.PoseFor(FishingPhase.Bite, 0f, strike, stationary: false));
            Assert.AreEqual(FishingPose.Reel,
                PlayerFishingAnimMath.PoseFor(FishingPhase.FightDeep, 10f, strike, stationary: false));
            Assert.AreEqual(FishingPose.Land,
                PlayerFishingAnimMath.PoseFor(FishingPhase.Landed, 0f, strike, stationary: false));
        }

        [Test]
        public void ScrubFrame_FollowsProgress_AndClampsTheEnds()
        {
            Assert.AreEqual(0, PlayerFishingAnimMath.ScrubFrame(0f, 6), "unloaded = the first frame");
            Assert.AreEqual(3, PlayerFishingAnimMath.ScrubFrame(0.5f, 6));
            Assert.AreEqual(5, PlayerFishingAnimMath.ScrubFrame(1f, 6), "full charge = fully drawn back");
            Assert.AreEqual(5, PlayerFishingAnimMath.ScrubFrame(2f, 6), "over-charge clamps");
            Assert.AreEqual(0, PlayerFishingAnimMath.ScrubFrame(-1f, 6), "negative-safe");
            Assert.AreEqual(0, PlayerFishingAnimMath.ScrubFrame(0.5f, 0), "count ≤ 0 is safe");
        }

        [Test]
        public void TheStrike_OpensTheFight_ThenTheReelTakesOver()
        {
            const float strike = 0.5f;
            foreach (FishingPhase fight in new[] { FishingPhase.Fighting, FishingPhase.FightDeep,
                                                   FishingPhase.FightSurface })
            {
                Assert.AreEqual(FishingPose.Strike, PlayerFishingAnimMath.PoseFor(fight, 0.1f, strike),
                    $"{fight}: the hook-set beat plays first");
                Assert.AreEqual(FishingPose.Reel, PlayerFishingAnimMath.PoseFor(fight, 0.6f, strike),
                    $"{fight}: then the reel cycle");
            }
            Assert.AreEqual(FishingPose.Reel, PlayerFishingAnimMath.PoseFor(FishingPhase.Fighting, 0f, 0f),
                "a zero-length strike hands straight to the reel (missing strike art degrades cleanly)");
        }

        [Test]
        public void LoopFrame_CyclesAndIsSafe()
        {
            Assert.AreEqual(0, PlayerFishingAnimMath.LoopFrame(0f, 10f, 6));
            Assert.AreEqual(3, PlayerFishingAnimMath.LoopFrame(0.35f, 10f, 6));
            Assert.AreEqual(0, PlayerFishingAnimMath.LoopFrame(0.6f, 10f, 6), "wraps at the cycle end");
            Assert.AreEqual(0, PlayerFishingAnimMath.LoopFrame(-1f, 10f, 6), "negative-safe");
            Assert.AreEqual(0, PlayerFishingAnimMath.LoopFrame(1f, 10f, 0), "count ≤ 0 is safe");
        }

        [Test]
        public void OnceFrame_AdvancesThenHoldsTheLastFrame()
        {
            Assert.AreEqual(0, PlayerFishingAnimMath.OnceFrame(0f, 10f, 4));
            Assert.AreEqual(2, PlayerFishingAnimMath.OnceFrame(0.25f, 10f, 4));
            Assert.AreEqual(3, PlayerFishingAnimMath.OnceFrame(0.35f, 10f, 4));
            Assert.AreEqual(3, PlayerFishingAnimMath.OnceFrame(99f, 10f, 4), "holds the last frame forever");
        }

        [Test]
        public void FacingRow_FollowsThePublishedOffset_CWRows()
        {
            // Heading convention: 0 = North (+Y), CW; rows as labelled (the corrected character bake).
            Assert.AreEqual(0, PlayerFishingAnimMath.FacingRowFor(0f, 5f, 0.05f, 8, false, 4), "north");
            Assert.AreEqual(2, PlayerFishingAnimMath.FacingRowFor(5f, 0f, 0.05f, 8, false, 4), "east");
            Assert.AreEqual(4, PlayerFishingAnimMath.FacingRowFor(0f, -5f, 0.05f, 8, false, 0), "south");
            Assert.AreEqual(6, PlayerFishingAnimMath.FacingRowFor(-5f, 0f, 0.05f, 8, false, 4), "west");
            Assert.AreEqual(1, PlayerFishingAnimMath.FacingRowFor(5f, 5f, 0.05f, 8, false, 4), "north-east");
        }

        [Test]
        public void FacingRow_HoldsTheFallback_WhenTheLineRunsStraightDown()
        {
            Assert.AreEqual(3, PlayerFishingAnimMath.FacingRowFor(0.01f, 0.01f, 0.05f, 8, false, 3),
                "a sub-threshold offset (deep fight under the boat) holds the current row");
            Assert.AreEqual(5, PlayerFishingAnimMath.FacingRowFor(float.NaN, float.NaN, 0.05f, 8, false, 5),
                "NaN-safe: holds the fallback");
        }

        [Test]
        public void FacingRow_RespectsThePerArtworkRowDirection()
        {
            // The per-artwork data flag (character sheets bake CW today; the flag exists so a future
            // artwork can disagree — the boat kits still do).
            Assert.AreEqual(8 - 2, PlayerFishingAnimMath.FacingRowFor(5f, 0f, 0.05f, 8, true, 4),
                "CCW art: east is drawn in the mirrored row (the IsoFacing un-mirror)");
        }
    }
}
