using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>How a run of yard pieces is laid: pictures by direction, bays, posts, a gate in a short gap, the walk's
    /// ends and bends, and the walls a fence gives.</summary>
    public sealed class PropRunLayoutTests
    {
        static Vector2[] Seg(float ax, float ay, float bx, float by) => new[] { new Vector2(ax, ay), new Vector2(bx, by) };

        [Test]
        public void DirectionIndex_PicksTheNearestDrawnAngle_AndFlipsAFallingRun()
        {
            Assert.AreEqual(0, PropRunLayout.DirectionIndex(Vector2.right, 10f, 10, out bool f0));
            Assert.IsFalse(f0);
            Assert.AreEqual(0, PropRunLayout.DirectionIndex(Vector2.left, 10f, 10, out bool f1), "a run looks the same either way");
            Assert.IsFalse(f1);
            Assert.AreEqual(9, PropRunLayout.DirectionIndex(Vector2.up, 10f, 10, out _));
            Assert.AreEqual(3, PropRunLayout.DirectionIndex(new Vector2(1f, -Mathf.Tan(31f * Mathf.Deg2Rad)), 10f, 10, out bool f2));
            Assert.IsTrue(f2, "a falling run is the rising picture flipped");
            Assert.AreEqual(0, PropRunLayout.DirectionIndex(Vector2.up, 10f, 1, out _), "a single picture is the only one");
        }

        [Test]
        public void PictureFor_TurnsAnOrientedPieceHalfRound_WhenItPointsWest()
        {
            PropRunLayout.PictureFor(Vector2.left, 10f, 10, true, out bool fx, out bool fy);
            Assert.IsTrue(fx && fy);
            PropRunLayout.PictureFor(Vector2.right, 10f, 10, true, out fx, out fy);
            Assert.IsFalse(fx || fy);
            PropRunLayout.PictureFor(Vector2.left, 10f, 10, false, out fx, out fy);
            Assert.IsFalse(fx || fy, "a plain piece is not turned");
        }

        [Test]
        public void Fence_TilesBaysWithoutGaps_PostsItsEnds_AndBandsEverySegment()
        {
            var layout = PropRunLayout.Fence(new List<Vector2[]> { Seg(0, 0, 4, 0) }, 1.5f, 0, 20f, 1.6f, 1f, 70f, 0.35f, true);
            List<PropRunLayout.Part> bays = layout.Parts.Where(p => p.Role == PropRunLayout.Role.Bay).ToList();
            Assert.AreEqual(3, bays.Count, "4 m of 1.5 m bays rounds up to three");
            Assert.AreEqual(2, layout.Parts.Count(p => p.Role == PropRunLayout.Role.End));
            Assert.AreEqual(0, layout.Parts.Count(p => p.Role == PropRunLayout.Role.Gate), "one run has no gap");
            Assert.AreEqual(1, layout.Walls.Count);
            Assert.AreEqual(4, layout.Walls[0].Length);
        }

        [Test]
        public void Fence_PutsAnAlternateBayEveryNth_AndACornerPostAtATurn()
        {
            var layout = PropRunLayout.Fence(new List<Vector2[]> { Seg(0, 0, 6, 0), Seg(6, 0, 6, 3) }, 1.5f, 3, 20f, 1.6f, 1f, 70f, 0.35f, false);
            Assert.AreEqual(1, layout.Parts.Count(p => p.Role == PropRunLayout.Role.Corner));
            Assert.AreEqual(new Vector2(6, 0), layout.Parts.Single(p => p.Role == PropRunLayout.Role.Corner).At);
            int bays = layout.Parts.Count(p => p.Role == PropRunLayout.Role.Bay || p.Role == PropRunLayout.Role.Alternate);
            Assert.AreEqual(bays / 3, layout.Parts.Count(p => p.Role == PropRunLayout.Role.Alternate));
        }

        [Test]
        public void Fence_RoundToItsGateway_IsOneRun_WithAGateInTheGap()
        {
            // The garden fence's shape: authored from the gate, round the yard, back to the gate's other post.
            var runs = new List<Vector2[]>
            {
                Seg(187.2f, 58f, 189.1f, 58f), Seg(190.2f, 58f, 191.8f, 58f), Seg(191.8f, 58f, 191.8f, 61.6f),
                Seg(191.8f, 61.6f, 187.2f, 61.6f), Seg(187.2f, 61.6f, 187.2f, 58f),
            };
            Assert.AreEqual(1, PropRunLayout.Chains(runs).Count, "the run goes on through its first post");
            var layout = PropRunLayout.Fence(runs, 1.5f, 0, 20f, 1.6f, 1f, 70f, 0.35f, true);
            Assert.AreEqual(1, layout.Parts.Count(p => p.Role == PropRunLayout.Role.Gate));
            Assert.AreEqual(2, layout.Parts.Count(p => p.Role == PropRunLayout.Role.End), "the gate's two posts");
            Assert.AreEqual(4, layout.Parts.Count(p => p.Role == PropRunLayout.Role.Corner), "the yard's four corners");
            PropRunLayout.Part gate = layout.Parts.Single(p => p.Role == PropRunLayout.Role.Gate);
            Assert.Greater(gate.At.y, 58f, "the gate swings into the yard");
            Assert.AreEqual(runs.Count, layout.Walls.Count, "no wall across the gateway");
        }

        [Test]
        public void Walk_EndsInsideItsEnds_AndBendsWhereItTurns()
        {
            var layout = PropRunLayout.Walk(new List<Vector2[]> { Seg(0, 0, 5, 0), Seg(5, 0, 9, 2) }, 1f, 0.6f, 12f, true, true);
            List<PropRunLayout.Part> ends = layout.Parts.Where(p => p.Role == PropRunLayout.Role.End).ToList();
            Assert.AreEqual(2, ends.Count);
            Assert.AreEqual(0.3f, ends[0].At.x, 1e-4f, "the first end's outer edge is the walk's start");
            Assert.Less(ends[0].Direction.x, 0f, "the first end faces out of the walk");
            Assert.AreEqual(1, layout.Parts.Count(p => p.Role == PropRunLayout.Role.Bend));
            Assert.IsEmpty(layout.Walls, "a walk is walked on");
        }
    }
}
