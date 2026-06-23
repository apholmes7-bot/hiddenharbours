using NUnit.Framework;
using UnityEngine;
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
    }
}
