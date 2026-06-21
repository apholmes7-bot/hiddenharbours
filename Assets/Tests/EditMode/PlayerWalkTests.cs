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
            // rows Down/Up/Left/Right (0..3), cols idle/walk1/walk2 (0..2): index = facing*3 + col.
            Assert.AreEqual(0,  PlayerWalkController.FrameIndex(Facing.Down, 0));
            Assert.AreEqual(1,  PlayerWalkController.FrameIndex(Facing.Down, 1));
            Assert.AreEqual(3,  PlayerWalkController.FrameIndex(Facing.Up, 0));
            Assert.AreEqual(6,  PlayerWalkController.FrameIndex(Facing.Left, 0));
            Assert.AreEqual(11, PlayerWalkController.FrameIndex(Facing.Right, 2));
            Assert.AreEqual(0,  PlayerWalkController.FrameIndex(Facing.Down, -5), "column clamps to 0");
            Assert.AreEqual(2,  PlayerWalkController.FrameIndex(Facing.Down, 9), "column clamps to 2");
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
