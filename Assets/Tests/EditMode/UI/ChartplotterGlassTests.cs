using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The chartplotter GLASS (ADR 0025 S6): the world↔screen transform, the survey taken from the
    /// terrain seam, and that the picture actually responds to the three controls that could each
    /// silently do nothing.
    ///
    /// <para><b>Why the differential assertions are the load-bearing ones (the #421 lesson).</b> Night,
    /// orientation and range are all parameters that compile, pass, and can be quietly ignored — the
    /// night panel shipped dead TWICE that way. So each is tested by proving the PICTURE CHANGES, per
    /// control, with a control proving the probe can also register "no change".</para>
    ///
    /// <para>Pure <see cref="DrawSurface"/> and a fake terrain — no scene, no canvas, no texture, so
    /// this runs headless.</para>
    /// </summary>
    public class ChartplotterGlassTests
    {
        private const string Region = "region.st_peters";
        private static readonly Rect Bounds = new Rect(0f, 0f, 760f, 520f);   // St Peters' real size

        /// <summary>A shelving coast: land in the west, deepening east, with a shoal. Analytic and
        /// deterministic — the same shape the proof bake uses.</summary>
        private sealed class FakeCoast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p)
            {
                float shore = Mathf.Lerp(6f, -22f, Mathf.InverseLerp(60f, 620f, p.x));
                float shoal = 9f * Mathf.Exp(-Mathf.Pow((p.x - 470f) / 60f, 2f)
                                             - Mathf.Pow((p.y - 380f) / 45f, 2f));
                return shore + shoal;
            }
        }

        private static NavRigState State(float rangeNM, NavRigGeometry.Orient orient, bool night,
                                         float heading = 62f)
            => new NavRigState(new Vector2(360f, 300f), true, heading, 6.4f, rangeNM, orient, night,
                               true, 2.1f, true);

        private static DrawSurface Paint(in NavRigState st, NavChartSource chart)
        {
            var s = new DrawSurface(NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH);
            NavRigRender.Render(s, 0, 0, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH, in st, chart,
                                Waypoints, Route, Track);
            return s;
        }

        private static readonly List<NavWaypoint> Waypoints = new()
        {
            new NavWaypoint(Region, "SHOAL", new Vector2(470f, 380f), NavWaypointKind.Hazard),
        };
        private static readonly List<Vector2> Route = new() { new(360f, 300f), new(430f, 340f) };
        private static readonly List<Vector2> Track = new() { new(300f, 300f), new(330f, 302f) };

        private static NavChartSource Chart(bool night)
        {
            var c = new NavChartSource();
            c.EnsureBaked(Bounds, night, new FakeCoast());
            return c;
        }

        private static int RegionDiff(DrawSurface a, DrawSurface b, RectInt r)
        {
            int n = 0;
            for (int y = r.y; y < r.y + r.height; y++)
                for (int x = r.x; x < r.x + r.width; x++)
                {
                    int i = y * a.Width + x;
                    Color32 p = a.Pixels[i], q = b.Pixels[i];
                    if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) n++;
                }
            return n;
        }

        private static int DiffCount(DrawSurface a, DrawSurface b)
        {
            int n = 0;
            for (int i = 0; i < a.Pixels.Length; i++)
            {
                Color32 p = a.Pixels[i], q = b.Pixels[i];
                if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) n++;
            }
            return n;
        }

        // ---- the transform ---------------------------------------------------------------------------

        [Test]
        public void ScreenToWorld_IsTheExactInverseOfWorldToScreen_InBothOrientations()
        {
            var chartBox = new RectInt(10, 20, 600, 300);
            foreach (var orient in new[] { NavRigGeometry.Orient.North, NavRigGeometry.Orient.Head })
            {
                NavRigGeometry.View v = NavRigGeometry.MakeView(chartBox, new Vector2(360f, 300f),
                                                                0.2f, orient, 62f);
                foreach (Vector2 w in new[] { new Vector2(360f, 300f), new Vector2(410f, 265f),
                                              new Vector2(300f, 340f) })
                {
                    Vector2 back = NavRigGeometry.ScreenToWorld(in v,
                                       NavRigGeometry.WorldToScreen(in v, w));
                    Assert.AreEqual(w.x, back.x, 1e-2f, $"{orient} x round-trip");
                    Assert.AreEqual(w.y, back.y, 1e-2f, $"{orient} y round-trip");
                }
            }
        }

        [Test]
        public void NorthUp_PutsNorthAtTheTopOfTheGlass()
        {
            // World +Y is north; screen +Y is DOWN. A point north of the centre must draw ABOVE it, or
            // the chart is mirrored — the single most likely silent defect in this transform.
            var chartBox = new RectInt(0, 0, 600, 300);
            var centre = new Vector2(360f, 300f);
            NavRigGeometry.View v = NavRigGeometry.MakeView(chartBox, centre, 0.2f,
                                                            NavRigGeometry.Orient.North, 0f);
            Vector2 mid = NavRigGeometry.WorldToScreen(in v, centre);
            Vector2 north = NavRigGeometry.WorldToScreen(in v, centre + new Vector2(0f, 50f));
            Vector2 east = NavRigGeometry.WorldToScreen(in v, centre + new Vector2(50f, 0f));

            Assert.Less(north.y, mid.y, "north of the boat draws ABOVE the boat");
            Assert.Greater(east.x, mid.x, "east of the boat draws to the RIGHT");
        }

        [Test]
        public void HeadUp_PutsTheCourseAtTheTop()
        {
            var chartBox = new RectInt(0, 0, 600, 300);
            var centre = new Vector2(360f, 300f);
            const float heading = 90f;                       // steering due east
            NavRigGeometry.View v = NavRigGeometry.MakeView(chartBox, centre, 0.2f,
                                                            NavRigGeometry.Orient.Head, heading);
            Vector2 mid = NavRigGeometry.WorldToScreen(in v, centre);
            Vector2 ahead = NavRigGeometry.WorldToScreen(in v, centre + new Vector2(50f, 0f)); // east

            Assert.Less(ahead.y, mid.y - 1f, "steering east, the water to the east is AHEAD — up-glass");
            Assert.AreEqual(mid.x, ahead.x, 1f, "and dead ahead stays on the centreline");
        }

        // ---- the survey ------------------------------------------------------------------------------

        [Test]
        public void TheSurvey_ReadsDatumDepthFromTheTerrainSeam_NotAnInventedField()
        {
            NavChartSource c = Chart(false);
            var terrain = new FakeCoast();
            foreach (Vector2 p in new[] { new Vector2(200f, 260f), new Vector2(400f, 300f),
                                          new Vector2(470f, 380f) })
            {
                // depth below datum IS the negated elevation above it — the sounder's own arithmetic.
                Assert.AreEqual(-terrain.ElevationAt(p), c.DepthAt(p), 1.5f,
                    "the chart must report the terrain's own number, within one sample cell");
            }
        }

        [Test]
        public void TheSurvey_BandsShoalWaterDarkerThanDeep_AndCallsGroundAboveDatumLand()
        {
            Assert.AreEqual(0, NavChartSource.BandOf(1f), "under 2 m is the shoalest band");
            Assert.AreEqual(4, NavChartSource.BandOf(25f), "past 18 m is the deepest");
            Assert.Greater(NavChartSource.BandOf(12f), NavChartSource.BandOf(3f), "monotonic");

            NavChartSource c = Chart(false);
            Assert.IsTrue(c.IsLand(new Vector2(70f, 260f)), "the shore is above datum");
            Assert.IsFalse(c.IsLand(new Vector2(600f, 260f)), "and the offing is not");
        }

        [Test]
        public void TheSurvey_NeverRebakesWhileSailing()
        {
            // Rule 7's claim, asserted rather than commented: panning, ranging and turning must all
            // leave the survey alone. Only region or palette may move it.
            var c = new NavChartSource();
            var terrain = new FakeCoast();
            Assert.IsTrue(c.EnsureBaked(Bounds, false, terrain), "the first call bakes");
            for (int i = 0; i < 100; i++)
                Assert.IsFalse(c.EnsureBaked(Bounds, false, terrain), "…and no later call does");

            Assert.IsTrue(c.EnsureBaked(Bounds, true, terrain), "the panel lights coming on re-bakes once");
            Assert.IsFalse(c.EnsureBaked(Bounds, true, terrain));
            Assert.IsTrue(c.EnsureBaked(new Rect(0f, 0f, 400f, 400f), true, terrain),
                          "and so does changing region");
        }

        [Test]
        public void WithNoTerrainRegistered_ThereIsNoChart_RatherThanAnInventedSeabed()
        {
            var c = new NavChartSource();
            Assert.IsFalse(c.EnsureBaked(Bounds, false, null));
            Assert.IsFalse(c.HasChart, "no survey is the honest answer with no height map");
            Assert.IsTrue(float.IsNaN(c.DepthAt(new Vector2(100f, 100f))));
            Assert.DoesNotThrow(() => NavRigRender.Render(
                new DrawSurface(300, 200), 0, 0, 300, 200, State(0.2f, NavRigGeometry.Orient.North, false),
                c, Waypoints, Route, Track), "and the glass still draws, blank");
        }

        // ---- the three controls that could each silently do nothing ----------------------------------

        [Test]
        public void NightChangesThePicture()
        {
            Assert.Greater(DiffCount(Paint(State(0.2f, NavRigGeometry.Orient.North, false), Chart(false)),
                                     Paint(State(0.2f, NavRigGeometry.Orient.North, true), Chart(true))),
                           20000, "the night palette must reach the glass");
        }

        [Test]
        public void RangeChangesThePicture()
        {
            NavChartSource c = Chart(false);
            Assert.Greater(DiffCount(Paint(State(0.1f, NavRigGeometry.Orient.North, false), c),
                                     Paint(State(0.4f, NavRigGeometry.Orient.North, false), c)),
                           20000, "zooming must move the chart, not just the number in the corner");
        }

        [Test]
        public void OrientationChangesThePicture_AtANonZeroHeading()
        {
            NavChartSource c = Chart(false);
            Assert.Greater(DiffCount(Paint(State(0.2f, NavRigGeometry.Orient.North, false, 62f), c),
                                     Paint(State(0.2f, NavRigGeometry.Orient.Head, false, 62f), c)),
                           20000, "head-up must actually rotate the chart");
        }

        // ---- formatters ------------------------------------------------------------------------------

        [Test]
        public void Formatters_MatchTheRig_AndSurviveTheHarbourScale()
        {
            Assert.AreEqual("0.05", NavRigGeometry.FmtNM(0.05f), "sub-1 NM keeps two decimals — the " +
                            "whole re-scaled ladder lives down here");
            Assert.AreEqual("1.6", NavRigGeometry.FmtNM(1.6f));
            Assert.AreEqual("12", NavRigGeometry.FmtNM(12.4f));
            Assert.AreEqual("007", NavRigGeometry.FmtDeg(7f));
            Assert.AreEqual("062", NavRigGeometry.FmtDeg(62f));
            Assert.AreEqual("359", NavRigGeometry.FmtDeg(359f));
            Assert.AreEqual("000", NavRigGeometry.FmtDeg(359.7f), "a compass has no 360");
            Assert.AreEqual("N", NavRigGeometry.Cardinal8(2f));
            Assert.AreEqual("SE", NavRigGeometry.Cardinal8(135f));
            Assert.AreEqual("01.30", NavRigGeometry.FmtHM(90f));
            Assert.AreEqual("--.--", NavRigGeometry.FmtHM(float.PositiveInfinity),
                            "a stopped boat has no ETA and the glass says so");
        }

        [Test]
        public void TheScaleBar_CanDrawAtHarbourRanges()
        {
            // The rig's own step table starts at 0.1 NM, which is a quarter of a whole region — at the
            // closest range no bar from it would fit, and the chart would show none at all.
            var chartBox = new RectInt(0, 0, 600, 300);
            NavRigGeometry.View v = NavRigGeometry.MakeView(chartBox, Vector2.zero, 0.05f,
                                                            NavRigGeometry.Orient.North, 0f);
            double pxPerNM = v.Scale * NavMath.MetresPerNauticalMile;
            float nm = NavRigGeometry.ScaleBarNM(pxPerNM);
            Assert.Greater(nm * pxPerNM, 10.0, "a bar shorter than ten pixels is not a scale bar");
            Assert.LessOrEqual(nm * pxPerNM, 100.0, "and it must still fit the rig's ~100 px budget");
        }

        // ---- NEGATIVE CONTROL ------------------------------------------------------------------------

        [Test]
        public void NegativeControl_TheDiffProbe_CanRegisterNoChange()
        {
            // Every "changes the picture" test above is vacuous if the probe cannot report zero. Two
            // identical paints must differ in nothing, and a control that does not move the chart must
            // not move the pixels either.
            NavChartSource c = Chart(false);
            NavRigState st = State(0.2f, NavRigGeometry.Orient.North, false);
            Assert.AreEqual(0, DiffCount(Paint(in st, c), Paint(in st, c)),
                            "the render is deterministic, or every diff above is noise");

            // North-up at heading 0 vs head-up at heading 0 draw the SAME CHART — rotation by nothing is
            // nothing. If this differed, the orientation test could be passing on a rounding artefact.
            // Only the CHART BODY is compared: the top strip legitimately reads "NORTH-UP" vs "HEAD-UP",
            // which is a label, not a rotation.
            NavRigGeometry.Layout L = NavRigGeometry.ComputeLayout(
                0, 0, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH, NavRigGeometry.Face.Console);
            Assert.AreEqual(0, RegionDiff(Paint(State(0.2f, NavRigGeometry.Orient.North, false, 0f), c),
                                          Paint(State(0.2f, NavRigGeometry.Orient.Head, false, 0f), c),
                                          L.Chart),
                            "head-up on a northerly course IS north-up");
        }
    }
}
