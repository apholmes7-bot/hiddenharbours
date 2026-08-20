using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The one wall system, pinned.</b> <see cref="PropertyBoundary"/> exists because the owner's
    /// 2026-08-19 walk found three separately-broken walls (the wharf's missing, the shipyard's
    /// misplaced, the cliffs' never built). These tests hold the properties that make it worth having
    /// exactly one: it converges rather than stacking, it does not leak at corners, and it says what it
    /// built without needing a physics step to prove it.
    ///
    /// <para><b>⚠ Geometry is asserted off the collider PATHS, never off a physics query.</b> EditMode
    /// has no simulation stepping to rely on, and a test that quietly depends on one is a test that
    /// passes for the wrong reason.</para>
    /// </summary>
    public class PropertyBoundaryTests
    {
        GameObject _go;
        PropertyBoundary _boundary;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("PropertyBoundary_Test");
            _boundary = _go.AddComponent<PropertyBoundary>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static List<Vector2> Line(params float[] xy)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            return pts;
        }

        PolygonCollider2D[] Walls()
        {
            Transform walls = _go.transform.Find(PropertyBoundary.CollidersChildName);
            return walls == null
                ? System.Array.Empty<PolygonCollider2D>()
                : walls.GetComponents<PolygonCollider2D>();
        }

        [Test]
        public void Configure_MakesOneColliderPerSegment()
        {
            _boundary.Configure(new[] { Line(0, 0, 10, 0, 10, 10) });

            Assert.AreEqual(2, _boundary.SegmentCount);
            Assert.AreEqual(2, Walls().Length,
                "one collider per segment — several PATHS on one collider turn their overlaps into " +
                "HOLES, and every corner of every run is an overlap.");
            foreach (var c in Walls())
                Assert.AreEqual(1, c.pathCount, "each wall carries exactly one path, for the same reason.");
        }

        [Test]
        public void Rebuild_Converges_RatherThanStacking()
        {
            _boundary.Configure(new[] { Line(0, 0, 10, 0, 10, 10) });
            _boundary.Rebuild();
            _boundary.Rebuild();

            int children = 0;
            foreach (Transform t in _go.transform)
                if (t.name == PropertyBoundary.CollidersChildName) children++;

            Assert.AreEqual(1, children, "a rebuild destroys what it made before making it again.");
            Assert.AreEqual(2, Walls().Length, "…and does not double the walls doing it.");
        }

        [Test]
        public void Configure_Twice_ReplacesTheRunsRatherThanAppending()
        {
            _boundary.Configure(new[] { Line(0, 0, 10, 0, 10, 10) });
            _boundary.Configure(new[] { Line(0, 0, 5, 0) });

            Assert.AreEqual(1, _boundary.Runs.Count);
            Assert.AreEqual(1, _boundary.SegmentCount);
            Assert.AreEqual(1, Walls().Length);
        }

        [Test]
        public void EmptyConfigure_BuildsNothingAtAll()
        {
            _boundary.Configure(new List<List<Vector2>>());
            Assert.AreEqual(0, _boundary.SegmentCount);
            Assert.AreEqual(0, Walls().Length);
            Assert.IsNull(_go.transform.Find(PropertyBoundary.CollidersChildName));
        }

        [Test]
        public void RunsShorterThanTwoPoints_AreDropped_NotCrashed()
        {
            _boundary.Configure(new[] { Line(1, 1), Line(0, 0, 4, 0) });
            Assert.AreEqual(1, _boundary.Runs.Count, "a one-point run is not a wall.");
            Assert.AreEqual(1, Walls().Length);
        }

        [Test]
        public void PointsRoundTripThroughANonZeroTransform()
        {
            _go.transform.position = new Vector3(100f, -50f, 0f);
            _boundary.Configure(new[] { Line(100, -50, 110, -50) });

            List<Vector2[]> world = _boundary.WorldRuns();
            Assert.AreEqual(1, world.Count);
            Assert.AreEqual(new Vector2(100f, -50f), world[0][0]);
            Assert.AreEqual(new Vector2(110f, -50f), world[0][1]);

            Assert.AreEqual(new Vector2(0f, 0f), _boundary.Runs[0].Points[0],
                "stored LOCAL, so moving the object moves its walls with it.");
        }

        // =============================================================================================
        //  ⭐ THE TRANSFORM CONTRACT — translation only, and it REFUSES rather than disagreeing quietly
        // =============================================================================================
        //
        // Configure and WorldRuns convert with `point − transform.position`, a SHIFT. The colliders
        // those points become are transformed by the host's FULL TRS at physics time. On a flat,
        // unscaled host the two agree exactly; on a rotated or scaled one the drawn wall and the
        // blocking wall become two different shapes with NO error at all — a player who "sometimes"
        // slips through, which reads as a physics glitch rather than as a modelling mistake. No caller
        // rotates one of these today, which is exactly why the assumption is worth pinning: as the one
        // wall system this component will outlive every caller that exists.

        [Test]
        public void AFlatUnscaledHost_IsWhatThisComponentIsFor()
        {
            _go.transform.position = new Vector3(12f, -7f, 0f);
            _go.transform.rotation = Quaternion.identity;
            _go.transform.localScale = Vector3.one;

            Assert.DoesNotThrow(() => _boundary.Configure(new[] { Line(12, -7, 22, -7) }));
            Assert.AreEqual(1, Walls().Length);
        }

        [Test]
        public void ARotatedHost_IsRefused_RatherThanSilentlyDisagreeingWithPhysics()
        {
            _go.transform.rotation = Quaternion.Euler(0f, 0f, 30f);

            var ex = Assert.Throws<System.InvalidOperationException>(
                () => _boundary.Configure(new[] { Line(0, 0, 10, 0) }),
                "a rotated host makes WorldRuns() and the collider paths two different shapes.");
            StringAssert.Contains("pure translation", ex.Message);
        }

        [Test]
        public void AScaledHost_IsRefused_ForTheSameReason()
        {
            _go.transform.localScale = new Vector3(2f, 2f, 1f);

            Assert.Throws<System.InvalidOperationException>(
                () => _boundary.Configure(new[] { Line(0, 0, 10, 0) }),
                "a scaled host doubles the collider and leaves WorldRuns() reporting the original.");
        }

        [Test]
        public void AFlatChildOfATurnedParent_IsJustAsRefused()
        {
            // The case somebody actually reaches, and the reason the check is on the WORLD transform
            // rather than the local one: this object's own rotation is identity and it is still wrong.
            var parent = new GameObject("TurnedParent");
            try
            {
                parent.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                _go.transform.SetParent(parent.transform, worldPositionStays: false);

                Assert.AreEqual(Quaternion.identity, _go.transform.localRotation,
                    "precondition: the boundary's OWN rotation is identity — only the parent turns.");
                Assert.Throws<System.InvalidOperationException>(
                    () => _boundary.Configure(new[] { Line(0, 0, 10, 0) }));
            }
            finally
            {
                _go.transform.SetParent(null);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TheTolerances_AdmitFloatNoise_AndNothingDeliberate()
        {
            // A transform assembled from a parent chain does not come back bit-exact, so the gate has to
            // admit noise. What it must NOT admit is a deliberate turn: one degree of yaw already moves
            // the far end of a 15 m run by 0.26 m, which is most of a wall thickness.
            Assert.Less(PropertyBoundary.RotationToleranceDegrees, 1f);
            Assert.Greater(PropertyBoundary.RotationToleranceDegrees, 0f);
            Assert.Less(PropertyBoundary.ScaleTolerance, 0.01f);
            Assert.Greater(PropertyBoundary.ScaleTolerance, 0f);

            _go.transform.rotation =
                Quaternion.Euler(0f, 0f, PropertyBoundary.RotationToleranceDegrees * 0.5f);
            Assert.DoesNotThrow(() => _boundary.Configure(new[] { Line(0, 0, 10, 0) }),
                "noise under the tolerance must pass, or every parented boundary is a coin toss.");
        }

        [Test]
        public void WallQuad_IsTheThicknessItWasAskedFor_AndOverrunsTheEnds()
        {
            const float thickness = 0.5f;
            _boundary.Configure(new[] { Line(0, 0, 10, 0) }, thickness);

            PolygonCollider2D wall = Walls()[0];
            Vector2[] path = wall.GetPath(0);
            Assert.AreEqual(4, path.Length);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in path)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }

            Assert.AreEqual(thickness, maxY - minY, 1e-3f, "across the run: the thickness.");
            Assert.AreEqual(10f + thickness, maxX - minX, 1e-3f,
                "along the run: half a thickness past each end. Butt-ended quads leave a wedge of " +
                "nothing on the outside of every corner, which is a pinhole exactly where a player " +
                "pressed into that corner is trying to get through.");
        }

        [Test]
        public void ACorner_IsCoveredByTheTwoQuadsThatMeetThere()
        {
            const float thickness = 0.4f;
            _boundary.Configure(new[] { Line(0, 0, 10, 0, 10, 10) }, thickness);

            // Just OUTSIDE the turn, within the wall's own thickness of the corner: the point a
            // butt-ended pair would leave open.
            var probe = new Vector2(10f + thickness * 0.4f, 0f - thickness * 0.4f);

            bool covered = false;
            foreach (var wall in Walls())
                if (PathContains(wall, probe)) { covered = true; break; }

            Assert.IsTrue(covered, "the outside of a corner must be walled by one of the two quads.");
        }

        [Test]
        public void TotalLength_IsTheFenceBill()
        {
            _boundary.Configure(new[] { Line(0, 0, 10, 0, 10, 10), Line(0, 20, 0, 25) });
            Assert.AreEqual(25f, _boundary.TotalLengthMetres(), 1e-3f);
        }

        [Test]
        public void ZeroLengthSegment_MakesNoWall()
        {
            _boundary.Configure(new[] { Line(5, 5, 5, 5, 9, 5) });
            Assert.AreEqual(2, _boundary.SegmentCount, "the run still describes two segments…");
            Assert.AreEqual(1, Walls().Length, "…but a duplicated point is not a wall.");
        }

        /// <summary>Point-in-quad against a collider's own path, in the collider's local space (which is
        /// the boundary's, since the walls child sits at its origin).</summary>
        static bool PathContains(PolygonCollider2D collider, Vector2 localPoint)
        {
            Vector2[] path = collider.GetPath(0);
            bool inside = false;
            for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
            {
                Vector2 a = path[i], b = path[j];
                if ((a.y > localPoint.y) == (b.y > localPoint.y)) continue;
                float t = (localPoint.y - a.y) / (b.y - a.y);
                if (localPoint.x < a.x + t * (b.x - a.x)) inside = !inside;
            }
            return inside;
        }
    }
}
