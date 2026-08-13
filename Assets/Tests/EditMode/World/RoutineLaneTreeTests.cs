using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;   // IsoCharacterMath — the measured-step twin of a stated leg heading
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The lane network's PURE maths (<see cref="RoutineLaneTree"/>) — is this a tree, what is the path
    /// between two junctions, what does that path look like as a walk, and where are you along it.
    ///
    /// <para>The one thing worth stating about what is tested here: the polyline cases assert the actual
    /// POINT SEQUENCE, not just its length. An edge whose bend points were laid in the wrong direction
    /// measures exactly the right distance along the mirror image of the path, so a length-only test would
    /// pass while every villager walked the wrong side of every curve.</para>
    /// </summary>
    public class RoutineLaneTreeTests
    {
        // A little village: green(0) ── lane(1) ── school(2)
        //                        │         └───── farm(3)
        //                        └──── shore(4)
        const int NoParent = RoutineLaneTree.NoParent;

        static int[] Parents() => new[] { NoParent, 0, 1, 1, 0 };

        static Vector2[] Positions() => new[]
        {
            new Vector2(0f, 0f),      // green
            new Vector2(0f, 16f),     // lane
            new Vector2(-12f, 20f),   // school
            new Vector2(14f, 14f),    // farm
            new Vector2(2f, -20f),    // shore
        };

        // ---- IsTree -----------------------------------------------------------------------------

        [Test]
        public void IsTree_AcceptsATree()
        {
            Assert.That(RoutineLaneTree.IsTree(Parents(), out int offender), Is.True);
            Assert.That(offender, Is.EqualTo(-1));
        }

        [Test]
        public void IsTree_RejectsTwoRoots()
        {
            Assert.That(RoutineLaneTree.IsTree(new[] { NoParent, NoParent, 0 }, out _), Is.False);
        }

        [Test]
        public void IsTree_RejectsNoRoot_AndACycle()
        {
            Assert.That(RoutineLaneTree.IsTree(new[] { 1, 0 }, out _), Is.False, "a two-node cycle");
            Assert.That(RoutineLaneTree.IsTree(new[] { NoParent, 2, 3, 1 }, out int offender), Is.False,
                        "1→2→3→1 is a cycle hanging off nothing");
            Assert.That(offender, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void IsTree_RejectsASelfParent_AndAnOutOfRangeParent()
        {
            Assert.That(RoutineLaneTree.IsTree(new[] { NoParent, 1 }, out _), Is.False);
            Assert.That(RoutineLaneTree.IsTree(new[] { NoParent, 7 }, out _), Is.False);
        }

        [Test]
        public void IsTree_RejectsNothingAtAll()
        {
            Assert.That(RoutineLaneTree.IsTree(System.Array.Empty<int>(), out _), Is.False);
            Assert.That(RoutineLaneTree.IsTree(null, out _), Is.False);
        }

        // ---- depth and the common ancestor -------------------------------------------------------

        [Test]
        public void Depth_CountsEdgesToTheRoot()
        {
            int[] p = Parents();
            Assert.That(RoutineLaneTree.Depth(p, 0), Is.EqualTo(0));
            Assert.That(RoutineLaneTree.Depth(p, 1), Is.EqualTo(1));
            Assert.That(RoutineLaneTree.Depth(p, 2), Is.EqualTo(2));
            Assert.That(RoutineLaneTree.Depth(p, 4), Is.EqualTo(1));
        }

        [Test]
        public void Depth_DoesNotSpinOnACycle()
        {
            Assert.That(RoutineLaneTree.Depth(new[] { 1, 0 }, 0), Is.EqualTo(-1));
        }

        [Test]
        public void CommonAncestor_IsWhereTheWalkTurnsAround()
        {
            int[] p = Parents();
            Assert.That(RoutineLaneTree.CommonAncestor(p, 2, 3), Is.EqualTo(1), "school ↔ farm meet on the lane");
            Assert.That(RoutineLaneTree.CommonAncestor(p, 2, 4), Is.EqualTo(0), "school ↔ shore meet on the green");
            Assert.That(RoutineLaneTree.CommonAncestor(p, 2, 2), Is.EqualTo(2), "and a place is its own");
            Assert.That(RoutineLaneTree.CommonAncestor(p, 2, 1), Is.EqualTo(1), "an ancestor of the other");
        }

        // ---- the node path -----------------------------------------------------------------------

        [Test]
        public void WriteNodePath_GoesUpToTheAncestorAndBackDown()
        {
            int[] p = Parents();
            var buffer = new int[16];

            int n = RoutineLaneTree.WriteNodePath(p, 2, 3, buffer);
            Assert.That(n, Is.EqualTo(3));
            Assert.That(new[] { buffer[0], buffer[1], buffer[2] }, Is.EqualTo(new[] { 2, 1, 3 }));

            n = RoutineLaneTree.WriteNodePath(p, 2, 4, buffer);
            Assert.That(n, Is.EqualTo(4));
            Assert.That(new[] { buffer[0], buffer[1], buffer[2], buffer[3] },
                        Is.EqualTo(new[] { 2, 1, 0, 4 }));
        }

        [Test]
        public void WriteNodePath_IsExactlyReversedWhenTheWalkIs()
        {
            int[] p = Parents();
            var there = new int[16];
            var back = new int[16];
            int a = RoutineLaneTree.WriteNodePath(p, 2, 4, there);
            int b = RoutineLaneTree.WriteNodePath(p, 4, 2, back);
            Assert.That(b, Is.EqualTo(a));
            for (int i = 0; i < a; i++)
                Assert.That(back[i], Is.EqualTo(there[a - 1 - i]),
                            "walking home has to be the same lane as walking out — in a tree there is only " +
                            "one, which is the whole reason this is a tree");
        }

        [Test]
        public void WriteNodePath_FromAPlaceToItself_IsJustThatPlace()
        {
            var buffer = new int[4];
            Assert.That(RoutineLaneTree.WriteNodePath(Parents(), 3, 3, buffer), Is.EqualTo(1));
            Assert.That(buffer[0], Is.EqualTo(3));
        }

        [Test]
        public void WriteNodePath_RefusesRatherThanOverrunsATightBuffer()
        {
            Assert.That(RoutineLaneTree.WriteNodePath(Parents(), 2, 4, new int[2]), Is.EqualTo(-1));
        }

        // ---- the polyline ------------------------------------------------------------------------

        [Test]
        public void WritePolyline_IsTheNodePositions_WhenNoEdgeBends()
        {
            int[] p = Parents();
            Vector2[] pos = Positions();
            var nodePath = new int[8];
            int count = RoutineLaneTree.WriteNodePath(p, 2, 4, nodePath);

            var poly = new Vector2[16];
            int n = RoutineLaneTree.WritePolyline(p, pos, new int[5], new int[5],
                                                  System.Array.Empty<Vector2>(), nodePath, count, poly);
            Assert.That(n, Is.EqualTo(4));
            Assert.That(poly[0], Is.EqualTo(pos[2]));
            Assert.That(poly[1], Is.EqualTo(pos[1]));
            Assert.That(poly[2], Is.EqualTo(pos[0]));
            Assert.That(poly[3], Is.EqualTo(pos[4]));
        }

        /// <summary>
        /// ⭐ THE BEND-DIRECTION CASE. Bend points are stored child→parent. Walking child→parent they come
        /// out in that order; walking parent→child they come out REVERSED. Both are asserted as sequences,
        /// because a mirrored curve is exactly the right length.
        /// </summary>
        [Test]
        public void WritePolyline_LaysEdgeBendsInTheDirectionOfTravel()
        {
            // green(0) ── shore(4), with the shore edge bending through two points. Stored child→parent,
            // i.e. from the shore back up to the green.
            int[] p = { NoParent, 0, 1, 1, 0 };
            Vector2[] pos = Positions();
            var viaStart = new int[5];
            var viaCount = new int[5];
            var bendNearShore = new Vector2(4f, -13f);
            var bendNearGreen = new Vector2(-1f, -6f);
            var via = new[] { bendNearShore, bendNearGreen };
            viaStart[4] = 0;
            viaCount[4] = 2;

            var nodePath = new int[8];
            var poly = new Vector2[16];

            // OUT: green → shore. The stored order is shore-first, so it must come out reversed.
            int count = RoutineLaneTree.WriteNodePath(p, 0, 4, nodePath);
            int n = RoutineLaneTree.WritePolyline(p, pos, viaStart, viaCount, via, nodePath, count, poly);
            Assert.That(n, Is.EqualTo(4));
            Assert.That(poly[0], Is.EqualTo(pos[0]));
            Assert.That(poly[1], Is.EqualTo(bendNearGreen), "the bend nearest the green comes first walking out");
            Assert.That(poly[2], Is.EqualTo(bendNearShore));
            Assert.That(poly[3], Is.EqualTo(pos[4]));

            // BACK: shore → green. The stored order IS the walking order.
            count = RoutineLaneTree.WriteNodePath(p, 4, 0, nodePath);
            n = RoutineLaneTree.WritePolyline(p, pos, viaStart, viaCount, via, nodePath, count, poly);
            Assert.That(n, Is.EqualTo(4));
            Assert.That(poly[0], Is.EqualTo(pos[4]));
            Assert.That(poly[1], Is.EqualTo(bendNearShore), "and nearest the shore first walking home");
            Assert.That(poly[2], Is.EqualTo(bendNearGreen));
            Assert.That(poly[3], Is.EqualTo(pos[0]));
        }

        [Test]
        public void WritePolyline_CollapsesARepeatedPoint()
        {
            // Two junctions on the same spot — what a dooryard that IS its lane node produces.
            int[] p = { NoParent, 0 };
            var pos = new[] { new Vector2(3f, 4f), new Vector2(3f, 4f) };
            var nodePath = new int[4];
            int count = RoutineLaneTree.WriteNodePath(p, 0, 1, nodePath);
            var poly = new Vector2[8];
            int n = RoutineLaneTree.WritePolyline(p, pos, new int[2], new int[2],
                                                  System.Array.Empty<Vector2>(), nodePath, count, poly);
            Assert.That(n, Is.EqualTo(1),
                        "a zero-length hop in the middle of a walk is a division waiting to happen");
        }

        [Test]
        public void WritePolyline_RefusesRatherThanOverrunsATightBuffer()
        {
            int[] p = Parents();
            var nodePath = new int[8];
            int count = RoutineLaneTree.WriteNodePath(p, 2, 4, nodePath);
            Assert.That(RoutineLaneTree.WritePolyline(p, Positions(), new int[5], new int[5],
                                                     System.Array.Empty<Vector2>(), nodePath, count,
                                                     new Vector2[2]),
                        Is.EqualTo(-1));
        }

        // ---- walking it --------------------------------------------------------------------------

        static Vector2[] Line() => new[]
        {
            new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 6f),
        };

        [Test]
        public void PolylineLength_AddsUpTheSegments()
        {
            Assert.That(RoutineLaneTree.PolylineLength(Line(), 0, 3), Is.EqualTo(16f).Within(1e-4f));
            Assert.That(RoutineLaneTree.PolylineLength(Line(), 0, 1), Is.EqualTo(0f));
            Assert.That(RoutineLaneTree.PolylineLength(null, 0, 3), Is.EqualTo(0f));
        }

        [Test]
        public void PointAlong_WalksTheCorner()
        {
            Vector2[] line = Line();
            Assert.That(RoutineLaneTree.PointAlong(line, 0, 3, 0f), Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(RoutineLaneTree.PointAlong(line, 0, 3, 4f), Is.EqualTo(new Vector2(4f, 0f)));
            Assert.That(RoutineLaneTree.PointAlong(line, 0, 3, 10f), Is.EqualTo(new Vector2(10f, 0f)));
            Assert.That(RoutineLaneTree.PointAlong(line, 0, 3, 13f), Is.EqualTo(new Vector2(10f, 3f)));
        }

        [Test]
        public void PointAlong_ClampsAtBothEnds_SoArrivalNeedsNoBranch()
        {
            Vector2[] line = Line();
            Assert.That(RoutineLaneTree.PointAlong(line, 0, 3, -5f), Is.EqualTo(line[0]));
            Assert.That(RoutineLaneTree.PointAlong(line, 0, 3, 16f), Is.EqualTo(line[2]));
            Assert.That(RoutineLaneTree.PointAlong(line, 0, 3, 1e6f), Is.EqualTo(line[2]),
                        "standing at the destination IS being past the end of the route");
        }

        [Test]
        public void PointAlong_SkipsAZeroLengthSegment_RatherThanDividingByIt()
        {
            var withRepeat = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 0f),
            };
            Vector2 p = RoutineLaneTree.PointAlong(withRepeat, 0, 3, 4f);
            Assert.That(float.IsNaN(p.x) || float.IsNaN(p.y), Is.False);
            Assert.That(p, Is.EqualTo(new Vector2(4f, 0f)));
        }

        [Test]
        public void PointAlong_ReadsASLICE_NotJustAWholeArray()
        {
            // Two routes flattened into one array, which is how RoutinePlan stores them.
            var flat = new[]
            {
                new Vector2(0f, 0f), new Vector2(4f, 0f),          // route A: [0,2)
                new Vector2(100f, 0f), new Vector2(100f, 9f),      // route B: [2,4)
            };
            Assert.That(RoutineLaneTree.PointAlong(flat, 2, 2, 3f), Is.EqualTo(new Vector2(100f, 3f)));
            Assert.That(RoutineLaneTree.PolylineLength(flat, 2, 2), Is.EqualTo(9f).Within(1e-4f));
        }

        [Test]
        public void HeadingAlong_IsTheBearingOfTheSegmentYouAreOn()
        {
            Vector2[] line = Line();
            // East along the first leg (0 = North, clockwise ⇒ East is 90).
            Assert.That(RoutineLaneTree.HeadingAlong(line, 0, 3, 4f, 180f), Is.EqualTo(90f).Within(1e-3f));
            // North up the second.
            Assert.That(RoutineLaneTree.HeadingAlong(line, 0, 3, 13f, 180f), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void HeadingAlong_IsAGroundBearing_SoAWalkedLegAgreesWithTheMeasuredStep()
        {
            // The waypoints are world positions and world XY is the squashed ground plane, while the row
            // this picks depicts a GROUND bearing (ADR 0034). A diagonal leg must therefore read 32.7°,
            // and must read exactly what the presenter reads off the same step — otherwise the villager
            // is drawn one facing off for the whole leg and then snaps when she arrives.
            var diagonal = new[] { Vector2.zero, new Vector2(10f, 10f) };
            Assert.That(RoutineLaneTree.HeadingAlong(diagonal, 0, 2, 5f, 180f),
                        Is.EqualTo(32.73f).Within(0.02f));
            Assert.That(RoutineLaneTree.HeadingAlong(diagonal, 0, 2, 5f, 180f),
                        Is.EqualTo(IsoCharacterMath.GroundHeadingFor(new Vector2(10f, 10f), 0.01f, 999f))
                          .Within(1e-3f),
                        "one plane, one answer — the stated leg and the measured step");
        }

        [Test]
        public void HeadingAlong_FallsBackForADegenerateSlice()
        {
            Assert.That(RoutineLaneTree.HeadingAlong(Line(), 0, 1, 0f, 123f), Is.EqualTo(123f));
            Assert.That(RoutineLaneTree.HeadingAlong(null, 0, 3, 0f, 123f), Is.EqualTo(123f));
        }
    }
}
