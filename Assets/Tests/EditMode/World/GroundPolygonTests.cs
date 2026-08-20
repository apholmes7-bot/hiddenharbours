using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The boundary arithmetic, pinned.</b> <see cref="GroundPolygon"/> is where a yard, a quay edge
    /// and a hoarding all become the same kind of thing, and every one of its failure modes is silent —
    /// the ground still paints and the fence still stands, and the only symptom is a player stopped
    /// somewhere that looks like nothing. So the maths is asserted here, headless, rather than found in
    /// a screenshot.
    /// </summary>
    public class GroundPolygonTests
    {
        static GroundPolygon UnitSquare(float size = 10f) => new GroundPolygon(new[]
        {
            new Vector2(0f, 0f), new Vector2(size, 0f), new Vector2(size, size), new Vector2(0f, size),
        });

        [Test]
        public void FewerThanThreeCorners_Refused()
        {
            Assert.Throws<System.ArgumentException>(
                () => new GroundPolygon(new[] { Vector2.zero, Vector2.one }),
                "two corners are a line, and a line encloses nothing — this has to be a hard failure, " +
                "because a boundary that quietly encloses nothing blocks nobody and clears no grass.");
        }

        [Test]
        public void Default_IsInvalidAndAnswersNothing()
        {
            var empty = default(GroundPolygon);
            Assert.IsFalse(empty.IsValid);
            Assert.IsFalse(empty.Contains(Vector2.zero), "a default struct must not claim ground.");
            Assert.AreEqual(0, empty.Count);
        }

        [Test]
        public void AreaPerimeterAndCentre_AreTheSquareTheyDescribe()
        {
            GroundPolygon sq = UnitSquare(10f);
            Assert.AreEqual(100f, sq.Area, 1e-3f);
            Assert.AreEqual(40f, sq.Perimeter, 1e-3f);
            Assert.AreEqual(new Vector2(5f, 5f), sq.Centre);
            Assert.IsTrue(sq.IsCounterClockwise, "authored anticlockwise, so the winding must read so.");
        }

        [Test]
        public void Contains_InsideOutsideAndBoundingBoxRejection()
        {
            GroundPolygon sq = UnitSquare(10f);
            Assert.IsTrue(sq.Contains(new Vector2(5f, 5f)));
            Assert.IsTrue(sq.Contains(new Vector2(0.5f, 9.5f)));
            Assert.IsFalse(sq.Contains(new Vector2(-0.5f, 5f)));
            Assert.IsFalse(sq.Contains(new Vector2(5f, 10.5f)));
            Assert.IsFalse(sq.Contains(new Vector2(1000f, 1000f)), "the box has to reject the far field.");
        }

        [Test]
        public void Contains_HandlesAConcaveShape()
        {
            // An L, so the notch is genuinely outside — a convex-only test would pass on a broken
            // implementation and the first hand-authored yard that is not a rectangle would leak.
            var l = new GroundPolygon(new[]
            {
                new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 4f),
                new Vector2(4f, 4f), new Vector2(4f, 10f), new Vector2(0f, 10f),
            });
            Assert.IsTrue(l.Contains(new Vector2(2f, 8f)), "the upright of the L is inside.");
            Assert.IsTrue(l.Contains(new Vector2(8f, 2f)), "the foot of the L is inside.");
            Assert.IsFalse(l.Contains(new Vector2(8f, 8f)), "the notch is OUTSIDE.");
        }

        [Test]
        public void DistanceToEdge_IsTheSameQuestionInsideAndOut()
        {
            GroundPolygon sq = UnitSquare(10f);
            Assert.AreEqual(2f, sq.DistanceToEdge(new Vector2(2f, 5f)), 1e-3f, "inside.");
            Assert.AreEqual(3f, sq.DistanceToEdge(new Vector2(-3f, 5f)), 1e-3f, "outside.");
            Assert.AreEqual(0f, sq.DistanceToEdge(new Vector2(0f, 5f)), 1e-3f, "on the edge.");
        }

        [Test]
        public void ContainsCircle_IsTheUnambiguousContainmentQuestion()
        {
            GroundPolygon sq = UnitSquare(10f);
            Assert.IsTrue(sq.ContainsCircle(new Vector2(5f, 5f), 4.9f));
            Assert.IsFalse(sq.ContainsCircle(new Vector2(5f, 5f), 5.1f),
                "a disc wider than the yard is not held by it, however central its centre.");
            Assert.IsFalse(sq.ContainsCircle(new Vector2(-1f, 5f), 0.1f), "centre outside is not held.");
        }

        [Test]
        public void Rectangle_FacesWhatItIsToldTo()
        {
            // Front toward +X: the rectangle's DEPTH runs along X and its frontage along Y.
            GroundPolygon r = GroundPolygon.Rectangle(new Vector2(10f, 10f), new Vector2(4f, 8f),
                                                 new Vector2(100f, 10f));
            Assert.AreEqual(32f, r.Area, 1e-3f);
            Assert.AreEqual(6f, r.Bounds.xMin, 1e-3f);
            Assert.AreEqual(14f, r.Bounds.xMax, 1e-3f);
            Assert.AreEqual(8f, r.Bounds.yMin, 1e-3f);
            Assert.AreEqual(12f, r.Bounds.yMax, 1e-3f);
        }

        [Test]
        public void Rectangle_DegenerateFacingFallsBackRatherThanExploding()
        {
            GroundPolygon r = GroundPolygon.Rectangle(Vector2.zero, new Vector2(2f, 4f), Vector2.zero);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(8f, r.Area, 1e-3f);
        }

        [Test]
        public void PerimeterRuns_WithNoGates_IsOneClosedRun()
        {
            GroundPolygon sq = UnitSquare(10f);
            List<Vector2[]> runs = sq.PerimeterRuns(null, 0f);
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(5, runs[0].Length, "four corners and the first repeated to close it.");
            Assert.AreEqual(runs[0][0], runs[0][4]);
        }

        [Test]
        public void PerimeterRuns_OneGate_LeavesOneRunWithAHoleInIt()
        {
            GroundPolygon sq = UnitSquare(10f);
            // A gate in the middle of the bottom edge.
            List<Vector2[]> runs = sq.PerimeterRuns(new[] { new Vector2(5f, 0f) }, 2f);

            Assert.AreEqual(1, runs.Count,
                "a closed boundary with ONE gate is ONE open run — if this says two, the walk is " +
                "leaving a seam at corner 0 and the fence will have a phantom break in it.");

            float length = 0f;
            Vector2[] run = runs[0];
            for (int i = 0; i + 1 < run.Length; i++) length += Vector2.Distance(run[i], run[i + 1]);
            Assert.AreEqual(38f, length, 0.3f, "40 m of boundary less a 2 m gate.");
        }

        [Test]
        public void PerimeterRuns_TwoGatesOnOppositeEdges_LeaveTwoRuns()
        {
            GroundPolygon sq = UnitSquare(10f);
            List<Vector2[]> runs = sq.PerimeterRuns(
                new[] { new Vector2(5f, 0f), new Vector2(5f, 10f) }, 2f);

            Assert.AreEqual(2, runs.Count);
            float total = 0f;
            foreach (var run in runs)
                for (int i = 0; i + 1 < run.Length; i++) total += Vector2.Distance(run[i], run[i + 1]);
            Assert.AreEqual(36f, total, 0.6f, "40 m less two 2 m gates.");
        }

        [Test]
        public void PerimeterRuns_GateStraddlingCornerZero_IsOneGapNotTwo()
        {
            GroundPolygon sq = UnitSquare(10f);
            // Corner 0 is (0,0); a gate right on it must cut ONE opening across the corner.
            List<Vector2[]> runs = sq.PerimeterRuns(new[] { new Vector2(0f, 0f) }, 3f);
            Assert.AreEqual(1, runs.Count,
                "the arc distance wraps, so a gate on the start corner is a single opening.");
        }

        [Test]
        public void PerimeterRuns_KeepsCornersRatherThanRoundingThem()
        {
            GroundPolygon sq = UnitSquare(10f);
            Vector2[] run = sq.PerimeterRuns(new[] { new Vector2(5f, 0f) }, 2f)[0];

            // Every corner of the square except the gated edge's interior must appear in the run.
            foreach (var corner in new[]
                     { new Vector2(10f, 0f), new Vector2(10f, 10f), new Vector2(0f, 10f), new Vector2(0f, 0f) })
            {
                bool found = false;
                foreach (var p in run) if (Vector2.Distance(p, corner) < 0.05f) { found = true; break; }
                Assert.IsTrue(found, $"corner {corner} was walked over — a fence that rounds its corners " +
                                     "leaves a gap on the outside of every turn.");
            }
        }

        [Test]
        public void ArcDistance_TakesTheShortWayRound()
        {
            Assert.AreEqual(1f, GroundPolygon.ArcDistance(39f, 40f, 40f), 1e-4f);
            Assert.AreEqual(1f, GroundPolygon.ArcDistance(39f, 0f, 40f), 1e-4f, "wraps.");
            Assert.AreEqual(20f, GroundPolygon.ArcDistance(0f, 20f, 40f), 1e-4f, "the far side.");
        }
    }
}
