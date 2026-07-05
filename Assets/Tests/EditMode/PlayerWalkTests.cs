using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// On-foot walk controller (step 1 of the on-foot player). Covers the pure input→velocity/facing
    /// and the FisherSheet frame mapping / walk cycle. (Sprite animation timing is runtime/visual.)
    /// </summary>
    public class PlayerWalkTests
    {
        [Test]
        public void Velocity_ZeroInput_IsZero()
        {
            Assert.AreEqual(Vector2.zero, PlayerWalkController.VelocityFor(Vector2.zero, 3f));
        }

        [Test]
        public void Velocity_CardinalInput_ScalesToSpeed()
        {
            Assert.AreEqual(new Vector2(3f, 0f), PlayerWalkController.VelocityFor(new Vector2(1f, 0f), 3f));
            Assert.AreEqual(new Vector2(0f, -3f), PlayerWalkController.VelocityFor(new Vector2(0f, -1f), 3f));
        }

        [Test]
        public void Velocity_Diagonal_IsClamped_NotFaster()
        {
            // Pressing two keys must not be faster than one (diagonals clamped to the move speed).
            float mag = PlayerWalkController.VelocityFor(new Vector2(1f, 1f), 3f).magnitude;
            Assert.AreEqual(3f, mag, 1e-4f, "diagonal speed should equal the cardinal speed");
        }

        [Test]
        public void Facing_FollowsDominantAxis()
        {
            Assert.AreEqual(Facing.Down,  PlayerWalkController.FacingFor(new Vector2(0f, -1f), Facing.Up));
            Assert.AreEqual(Facing.Up,    PlayerWalkController.FacingFor(new Vector2(0f, 1f),  Facing.Down));
            Assert.AreEqual(Facing.Left,  PlayerWalkController.FacingFor(new Vector2(-1f, 0f), Facing.Down));
            Assert.AreEqual(Facing.Right, PlayerWalkController.FacingFor(new Vector2(1f, 0f),  Facing.Down));
            // Horizontal dominates a shallow diagonal.
            Assert.AreEqual(Facing.Right, PlayerWalkController.FacingFor(new Vector2(1f, 0.3f), Facing.Down));
        }

        [Test]
        public void Facing_KeepsCurrentWhenIdle()
        {
            Assert.AreEqual(Facing.Left, PlayerWalkController.FacingFor(Vector2.zero, Facing.Left));
            Assert.AreEqual(Facing.Up,   PlayerWalkController.FacingFor(Vector2.zero, Facing.Up));
        }

        [Test]
        public void FrameIndex_MapsFacingRowsAndColumns()
        {
            // Three drawn rows Down/Up/Side (0..2), cols idle/walk1/walk2 (0..2): index = sheetRow*3 + col.
            Assert.AreEqual(0, PlayerWalkController.FrameIndex(Facing.Down, 0));
            Assert.AreEqual(1, PlayerWalkController.FrameIndex(Facing.Down, 1));
            Assert.AreEqual(3, PlayerWalkController.FrameIndex(Facing.Up, 0));
            Assert.AreEqual(6, PlayerWalkController.FrameIndex(Facing.Left, 0));
            // Right reuses the Side row (it is the flipped mirror, not its own row).
            Assert.AreEqual(8, PlayerWalkController.FrameIndex(Facing.Right, 2));
            Assert.AreEqual(0, PlayerWalkController.FrameIndex(Facing.Down, -5), "column clamps to 0");
            Assert.AreEqual(2, PlayerWalkController.FrameIndex(Facing.Down, 9), "column clamps to 2");
        }

        [Test]
        public void SheetRow_LeftAndRight_ShareTheSideRow()
        {
            Assert.AreEqual(0, PlayerWalkController.SheetRow(Facing.Down));
            Assert.AreEqual(1, PlayerWalkController.SheetRow(Facing.Up));
            Assert.AreEqual(2, PlayerWalkController.SheetRow(Facing.Left));
            Assert.AreEqual(2, PlayerWalkController.SheetRow(Facing.Right),
                "Right must draw the same Side row as Left (it is the mirror, not a separate row)");
        }

        [Test]
        public void FlipX_OnlyRightIsMirrored()
        {
            // The Side art is drawn facing LEFT; only Right is flipped, so the fourth facing is a
            // guaranteed-matched mirror of the one drawn side. Down/Up/Left are never flipped.
            Assert.IsFalse(PlayerWalkController.FlipXFor(Facing.Down));
            Assert.IsFalse(PlayerWalkController.FlipXFor(Facing.Up));
            Assert.IsFalse(PlayerWalkController.FlipXFor(Facing.Left));
            Assert.IsTrue(PlayerWalkController.FlipXFor(Facing.Right));
        }

        [Test]
        public void LeftAndRight_UseSameFrames_DifferOnlyByFlip()
        {
            // Per column, Left and Right resolve to the identical sheet frame; the only difference is flipX.
            for (int col = 0; col <= 2; col++)
            {
                Assert.AreEqual(PlayerWalkController.FrameIndex(Facing.Left, col),
                                PlayerWalkController.FrameIndex(Facing.Right, col),
                                $"col {col}: Left/Right must share the Side-row frame");
            }
            Assert.AreNotEqual(PlayerWalkController.FlipXFor(Facing.Left),
                               PlayerWalkController.FlipXFor(Facing.Right),
                               "Left and Right must differ by flip so they don't look identical");
        }

        [Test]
        public void WalkCycle_IsWalk1_Idle_Walk2_Idle()
        {
            Assert.AreEqual(1, PlayerWalkController.WalkCycleColumn(0));
            Assert.AreEqual(0, PlayerWalkController.WalkCycleColumn(1));
            Assert.AreEqual(2, PlayerWalkController.WalkCycleColumn(2));
            Assert.AreEqual(0, PlayerWalkController.WalkCycleColumn(3));
            Assert.AreEqual(1, PlayerWalkController.WalkCycleColumn(4), "the cycle wraps");
        }

        // ---- The depth-aware wade edge (the owner's three-band water-travel model, P1/P5) ------

        // A shore where depth increases going WEST: depth(x) = 5 - x. So x≥5 dry, x=4.5 wade (0.5),
        // x=3.5 swim (1.5), x=2 deep (3.0). wadeDepth 0.5, swimLimit 2.0.
        static float DepthShore(Vector2 p) => 5f - p.x;
        const float Wade = 0.5f, Swim = 2.0f, WadeF = 0.6f, SwimF = 0.25f;

        [Test]
        public void WaterEdge_ScalesSpeedByDepthAtFeet_FullOnDry()
        {
            // Standing on dry ground (x=6, depth -1) → full speed, unimpeded onto drier ground.
            var v = PlayerWalkController.ApplyWaterEdge(new Vector2(2f, 0f), new Vector2(6f, 0f),
                        DepthShore, 0.5f, Wade, Swim, WadeF, SwimF);
            Assert.AreEqual(2f, v.x, 1e-5f, "on dry ground the move is full speed");
        }

        [Test]
        public void WaterEdge_WadingSlowsTheMove()
        {
            // Standing in the wade band at x=4.6 (depth 0.4) → scale = lerp(1,0.6, 0.4/0.5)=0.68.
            var origin = new Vector2(4.6f, 0f);
            var v = PlayerWalkController.ApplyWaterEdge(new Vector2(0f, 3f), origin,
                        DepthShore, 0.5f, Wade, Swim, WadeF, SwimF); // move along-shore (north) so no wall
            Assert.AreEqual(3f * 0.68f, v.y, 1e-4f, "wading scales the move toward WadeSlowFactor");
        }

        [Test]
        public void WaterEdge_SoftWallsSteppingIntoBoatOnlyDeep_ButAllowsWadeAndSwim()
        {
            // Stand at the wade/dry edge x=5 (depth 0). Probe 1 m: west probe x=4 → depth 1.0 (swim, allowed);
            // to hit deep (>2.0) we need to be near it. Put the fisher at x=3.5 (depth 1.5, swim band ORIGIN).
            // But origin in swim lifts the wall — so to test the wall we must stand shallow enough.
            // Stand at x=3.1 is swim too. Use wadeDepth big enough: keep default, stand at x=4.6 (wade, depth 0.4),
            // probe west 1 m → x=3.6 depth 1.4 (swim ≤ 2.0) → ALLOWED.
            var wadeOrigin = new Vector2(4.6f, 0f);
            var intoSwim = PlayerWalkController.ApplyWaterEdge(new Vector2(-3f, 0f), wadeOrigin,
                               DepthShore, 1f, Wade, Swim, WadeF, SwimF);
            Assert.Less(intoSwim.x, 0f, "stepping from wade toward swim water is allowed (scaled, not walled)");

            // Now a case that DOES hit deep: origin still in wade (depth ≤ wadeDepth) but a step lands in deep.
            // Make the shore steeper via a custom probe: depth jumps from 0.3 at origin to 3.0 one metre west.
            System.Func<Vector2, float> cliff = p => p.x >= 5f ? 0.3f : 3.0f; // origin wade, west = deep
            var atCliff = new Vector2(5f, 0f);
            var blocked = PlayerWalkController.ApplyWaterEdge(new Vector2(-3f, 2f), atCliff,
                              cliff, 1f, Wade, Swim, WadeF, SwimF);
            Assert.AreEqual(0f, blocked.x, 1e-5f, "a step into boat-only deep water is soft-walled");
            Assert.Greater(blocked.y, 0f, "but you still slide ALONG the shore (per-axis wall)");
        }

        [Test]
        public void WaterEdge_NeverTrapped_OriginInDeep_CanAlwaysMoveOut()
        {
            // Caught by a rising tide / just disembarked: standing in deep water (depth 3.0 at x=2).
            var deepOrigin = new Vector2(2f, 0f); // depth 3.0 > swimLimit
            var v = PlayerWalkController.ApplyWaterEdge(new Vector2(-3f, 2f), deepOrigin,
                        DepthShore, 1f, Wade, Swim, WadeF, SwimF);
            // Wall lifted → both axes survive (scaled to the swim crawl), even the deeper-going one.
            Assert.AreNotEqual(0f, v.x, "never zero all velocity when already caught — the player can swim out");
            Assert.AreNotEqual(0f, v.y, "the escape move is never fully blocked");
            Assert.AreEqual(SwimF, v.magnitude / new Vector2(-3f, 2f).magnitude, 1e-4f, "and it moves at the slow-swim crawl");
        }

        [Test]
        public void WaterEdge_NullProbe_PassesThrough()
        {
            var v = PlayerWalkController.ApplyWaterEdge(new Vector2(1f, 2f), Vector2.zero, null, 1f,
                        Wade, Swim, WadeF, SwimF);
            Assert.AreEqual(new Vector2(1f, 2f), v, "a null depth probe disables the wade model");
        }

        [Test]
        public void StateForBand_DeepFoldsToSwim_OnFootIsNeverDeep()
        {
            Assert.AreEqual(OnFootWaterState.Dry,  PlayerWalkController.StateForBand(DepthBand.Dry));
            Assert.AreEqual(OnFootWaterState.Wade, PlayerWalkController.StateForBand(DepthBand.Wade));
            Assert.AreEqual(OnFootWaterState.Swim, PlayerWalkController.StateForBand(DepthBand.Swim));
            Assert.AreEqual(OnFootWaterState.Swim, PlayerWalkController.StateForBand(DepthBand.Deep),
                "on foot the deep band is soft-walled → it reads as Swim (get to shore), never a Deep state");
        }
    }
}
