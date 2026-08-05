using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The chartplotter's MAXIMIZED face (ADR 0025 S6 PR 4): the layer/tool rail, the waypoint and
    /// route manager column, the range/bearing measure line and the depth-profile strip.
    ///
    /// <para><b>Every new switch is proved by a DIFFERENTIAL, and every one has a negative control
    /// (the #421 lesson).</b> A rail button, a selection, a measure line and a name field are all
    /// things that can be plumbed, compile, pass, and move no pixel — which is exactly how the helm's
    /// night panel shipped dead twice. So each is asserted by showing the PICTURE CHANGES, per control;
    /// and the four DORMANT rail rows get the opposite assertion, that they move nothing on the chart,
    /// which is the only way to tell "inert on purpose" from "wired and broken".</para>
    ///
    /// <para>Pure <see cref="DrawSurface"/> and a fake terrain — no scene, no canvas, no texture.</para>
    /// </summary>
    public class ChartplotterMaxFaceTests
    {
        private const string Region = "region.st_peters";
        private static readonly Rect Bounds = new Rect(0f, 0f, 760f, 520f);

        /// <summary>The same shelving coast the console face's tests use, so the two suites are reading
        /// one seabed.</summary>
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

        private static readonly List<NavWaypoint> Waypoints = new()
        {
            new NavWaypoint(Region, "SHOAL", new Vector2(470f, 380f), NavWaypointKind.Hazard),
            new NavWaypoint(Region, "MARK 2", new Vector2(400f, 320f), NavWaypointKind.Mark),
            new NavWaypoint(Region, "ANCHOR", new Vector2(340f, 280f), NavWaypointKind.Anchor),
        };
        private static readonly List<Vector2> Route = new()
            { new(360f, 300f), new(430f, 340f), new(470f, 380f) };
        private static readonly List<Vector2> Track = new()
            { new(300f, 300f), new(330f, 302f), new(352f, 306f) };

        private static NavChartSource Chart(bool night)
        {
            var c = new NavChartSource();
            c.EnsureBaked(Bounds, night, new FakeCoast());
            return c;
        }

        private static NavRigState State(bool night = false, float heading = 62f, float tide = 2.1f)
            => new NavRigState(new Vector2(360f, 300f), true, heading, 6.4f, 0.2f,
                               NavRigGeometry.Orient.North, night, true, tide, true);

        private static NavMaxState Max(NavChartTool tool = NavChartTool.Pan,
                                       NavChartLayers layers = NavChartLayers.All,
                                       int sel = -1,
                                       NavMeasureState measure = NavMeasureState.Off,
                                       bool editing = false, string text = "")
            => new NavMaxState(tool, layers, sel, measure,
                               new Vector2(330f, 290f), new Vector2(430f, 350f),
                               128f, 1.4f, editing, text);

        private static DrawSurface PaintMax(in NavRigState st, in NavMaxState mx, NavChartSource chart)
        {
            var s = new DrawSurface(NavRigGeometry.MaxW, NavRigGeometry.MaxH);
            var profile = new NavDepthProfile();
            NavRigGeometry.Layout L = MaxLayout;
            profile.EnsureBaked(chart, st.Boat, st.HasBoat, st.HeadingDeg, st.RangeNM,
                                Mathf.Min(NavDepthProfile.MaxSamples, L.Profile.width));
            NavRigRender.RenderMax(s, 0, 0, NavRigGeometry.MaxW, NavRigGeometry.MaxH, in st, in mx,
                                   chart, Waypoints, Route, Track, profile);
            return s;
        }

        private static NavRigGeometry.Layout MaxLayout
            => ChartplotterOverlayLayout.NativeLayout(NavRigGeometry.Face.Max);

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

        /// <summary>How many DISTINCT colours a box holds — the cheapest honest way to say "something
        /// was actually drawn here" rather than "the background is still showing".</summary>
        private static int ColourCount(DrawSurface s, RectInt r)
        {
            var seen = new HashSet<int>();
            for (int y = r.y; y < r.y + r.height; y++)
                for (int x = r.x; x < r.x + r.width; x++)
                {
                    Color32 c = s.Pixels[y * s.Width + x];
                    seen.Add((c.r << 24) | (c.g << 16) | (c.b << 8) | c.a);
                }
            return seen.Count;
        }

        private static Vector2 Centre(RectInt r)
            => new Vector2(r.x + r.width / 2f, r.y + r.height / 2f);

        // ---- the rail's geometry (navRig.js:258-260) --------------------------------------------------

        [Test]
        public void TheRail_HasElevenButtonsAndTheRigsOneSpacer_AllInsideTheRail()
        {
            NavRigGeometry.Layout L = MaxLayout;
            Assert.That(L.HasMax, Is.True);
            Assert.That(NavRigGeometry.RailIds.Length, Is.EqualTo(12),
                        "twelve slots — the pitch is floor(rail.h/12) over ALL of them, so compacting " +
                        "the spacer out would move every button below it");

            int drawn = 0;
            RectInt previous = default;
            for (int slot = 0; slot < NavRigGeometry.RailIds.Length; slot++)
            {
                bool has = NavRigGeometry.TryRailButton(in L, slot, out RectInt b);
                if (slot == NavRigGeometry.RailSeparatorSlot)
                {
                    Assert.That(has, Is.False, "the rig's '|' is a gap, not a button");
                    continue;
                }
                Assert.That(has, Is.True, $"slot {slot} ({NavRigGeometry.RailIds[slot]}) draws");
                Assert.That(b.x, Is.GreaterThanOrEqualTo(L.Rail.x));
                Assert.That(b.x + b.width, Is.LessThanOrEqualTo(L.Rail.x + L.Rail.width));
                Assert.That(b.y, Is.GreaterThanOrEqualTo(L.Rail.y));
                Assert.That(b.y + b.height, Is.LessThanOrEqualTo(L.Rail.y + L.Rail.height));
                if (drawn > 0 && previous.height > 0)
                    Assert.That(b.y, Is.GreaterThanOrEqualTo(previous.y + previous.height),
                                $"slot {slot} must not overlap the button above it");
                previous = b;
                drawn++;
            }
            Assert.That(drawn, Is.EqualTo(11));
        }

        [Test]
        public void TheRail_SplitsIntoSevenLayersAndFourTools_AtTheRigsBoundary()
        {
            for (int slot = 0; slot < NavRigGeometry.RailIds.Length; slot++)
            {
                bool tool = NavRigGeometry.RailSlotIsTool(slot);
                Assert.That(tool, Is.EqualTo(slot > 7), $"slot {slot}: tools are i>7 (navRig.js:260)");
                if (tool)
                    Assert.That(NavRigGeometry.RailLayer(slot), Is.EqualTo(NavChartLayers.None),
                                "a tool slot switches no layer");
                else if (slot != NavRigGeometry.RailSeparatorSlot)
                    Assert.That(NavRigGeometry.RailLayer(slot), Is.Not.EqualTo(NavChartLayers.None),
                                $"slot {slot} ({NavRigGeometry.RailIds[slot]}) names a layer");
            }

            Assert.That(NavRigGeometry.RailTool(8), Is.EqualTo(NavChartTool.Pan));
            Assert.That(NavRigGeometry.RailTool(9), Is.EqualTo(NavChartTool.Mark));
            Assert.That(NavRigGeometry.RailTool(10), Is.EqualTo(NavChartTool.Route));
            Assert.That(NavRigGeometry.RailTool(11), Is.EqualTo(NavChartTool.Measure));
        }

        // ---- the manager column's geometry (navRig.js:422-445) ----------------------------------------

        [Test]
        public void TheManagerColumn_StacksItsSectionsInOrder_AndFitsInsideTheColumn()
        {
            NavRigGeometry.Layout L = MaxLayout;
            NavRigGeometry.Manager m = NavRigGeometry.ComputeManager(in L, 3, 3, selected: true);

            Assert.That(m.WaypointRowCount, Is.EqualTo(3));
            Assert.That(m.RouteRowCount, Is.EqualTo(2), "three points is two legs");

            Assert.That(m.WaypointHeaderY, Is.LessThan(m.WaypointRowsY));
            Assert.That(m.WaypointRowsY, Is.LessThan(m.RouteHeaderY));
            Assert.That(m.RouteHeaderY, Is.LessThan(m.CursorHeaderY));
            Assert.That(m.CursorHeaderY, Is.LessThan(m.TideHeaderY));

            for (int i = 1; i < m.WaypointRowCount; i++)
                Assert.That(m.WaypointRow(i).y,
                            Is.EqualTo(m.WaypointRow(i - 1).y + NavRigGeometry.ManagerRowH),
                            "rows are the rig's 9 px pitch");

            Assert.That(m.NameButton.width, Is.GreaterThan(0), "a selected row offers its actions");
            Assert.That(m.NameButton.y, Is.GreaterThan(m.TideHeaderY),
                        "the one affordance the rig does not author sits BELOW everything it does, so " +
                        "nothing the source places is shifted by it");
            Assert.That(m.DeleteButton.x, Is.GreaterThan(m.NameButton.x + m.NameButton.width - 1),
                        "…and the two do not overlap");
            Assert.That(m.DeleteButton.x + m.DeleteButton.width,
                        Is.LessThanOrEqualTo(m.Right.x + m.Right.width));
            Assert.That(m.NameButton.y + m.NameButton.height,
                        Is.LessThanOrEqualTo(m.Right.y + m.Right.height),
                        "the rig leaves room under its own content — the buttons must live in it");
        }

        [Test]
        public void TheManagerColumn_ListsAtMostTheRigsSixWaypointsAndFiveLegs()
        {
            NavRigGeometry.Layout L = MaxLayout;
            NavRigGeometry.Manager m = NavRigGeometry.ComputeManager(in L, 40, 40, selected: false);
            Assert.That(m.WaypointRowCount, Is.EqualTo(NavRigGeometry.ManagerWaypointRows));
            Assert.That(m.RouteRowCount, Is.EqualTo(NavRigGeometry.ManagerRouteRows));
            Assert.That(m.NameButton.width, Is.EqualTo(0), "no selection, no actions");
        }

        // ---- the hit map -------------------------------------------------------------------------------

        [Test]
        public void EveryRailButton_HitsItsOwnSlot_AndTheSpacerHitsNothing()
        {
            NavRigGeometry.Layout L = MaxLayout;
            for (int slot = 0; slot < NavRigGeometry.RailIds.Length; slot++)
            {
                if (!NavRigGeometry.TryRailButton(in L, slot, out RectInt b)) continue;
                int hit = ChartplotterOverlayLayout.HitTestMax(Centre(b), 3, 3, false);
                Assert.That(hit, Is.EqualTo(ChartplotterOverlayLayout.RailBase + slot),
                            $"{NavRigGeometry.RailIds[slot]} must be its own control");
                Assert.That(ChartplotterOverlayLayout.RailSlotOf(hit), Is.EqualTo(slot));
            }
        }

        [Test]
        public void TheMaxHitMap_FindsTheKeysStripsRowsAndPanes()
        {
            NavRigGeometry.Layout L = MaxLayout;

            for (int i = 0; i < 4; i++)
                Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(L.Key(i)), 3, 3, false),
                            Is.EqualTo(i), $"keys[{i}] keeps its identity on the MAX face");

            Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(L.Chart), 3, 3, false),
                        Is.EqualTo(ChartplotterOverlayLayout.ChartHit));
            Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(L.Profile), 3, 3, false),
                        Is.EqualTo(ChartplotterOverlayLayout.ProfileHit),
                        "the strip is a read — knowably inert, not merely 'off the instrument'");
            Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(L.BotBar), 3, 3, false),
                        Is.EqualTo(ChartplotterOverlayLayout.RouteHit));

            RectInt orient = ChartplotterOverlayLayout.OrientBox(in L);
            Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(orient), 3, 3, false),
                        Is.EqualTo(ChartplotterOverlayLayout.OrientHit));
            Assert.That(ChartplotterOverlayLayout.HitTestMax(
                            new Vector2(L.TopBar.x + 4f, L.TopBar.y + L.TopBar.height / 2f), 3, 3, false),
                        Is.EqualTo(ChartplotterOverlayLayout.NightHit),
                        "the rest of the strip is still the backlight tap");

            NavRigGeometry.Manager m = NavRigGeometry.ComputeManager(in L, 3, 3, selected: true);
            for (int i = 0; i < 3; i++)
                Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(m.WaypointRow(i)), 3, 3, true),
                            Is.EqualTo(ChartplotterOverlayLayout.WaypointRowBase + i),
                            $"row {i} is the row you clicked");
            Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(m.NameButton), 3, 3, true),
                        Is.EqualTo(ChartplotterOverlayLayout.ActionName));
            Assert.That(ChartplotterOverlayLayout.HitTestMax(Centre(m.DeleteButton), 3, 3, true),
                        Is.EqualTo(ChartplotterOverlayLayout.ActionDelete));

            Assert.That(ChartplotterOverlayLayout.HitTestMax(new Vector2(-20f, -20f), 3, 3, false),
                        Is.EqualTo(ChartplotterOverlayLayout.NoHit));
        }

        [Test]
        public void WithNothingSelected_TheActionButtonsAreNotLive()
        {
            NavRigGeometry.Layout L = MaxLayout;
            NavRigGeometry.Manager withSel = NavRigGeometry.ComputeManager(in L, 3, 3, selected: true);
            // The SAME pixel that is the NAME button when a row is selected must be inert when none is:
            // a button you cannot see must not be a button you can press.
            int hit = ChartplotterOverlayLayout.HitTestMax(Centre(withSel.NameButton), 3, 3, false);
            Assert.That(hit, Is.EqualTo(ChartplotterOverlayLayout.ManagerHit));
        }

        [Test]
        public void AChartTap_ConvertsThroughTheMaxChartBox_NotTheConsoleOne()
        {
            // The MAX chart is narrower and shifted right (rail on the left, manager on the right), so
            // the same rig pixel is different WATER on the two faces. Converting a MAX tap through the
            // console's box is a bug that would be invisible until a waypoint landed in the wrong place.
            NavRigState st = State();
            Vector2 probe = Centre(MaxLayout.Chart);

            Assert.That(ChartplotterOverlayLayout.TryChartTapToWorld(
                            probe, in st, NavRigGeometry.Face.Max, out Vector2 onMax, out float rMax),
                        Is.True);
            Assert.That(rMax, Is.GreaterThan(0f));
            Assert.That(onMax.x, Is.EqualTo(st.Boat.x).Within(1.5f),
                        "the centre of the MAX chart is own ship");
            Assert.That(onMax.y, Is.EqualTo(st.Boat.y).Within(1.5f));

            ChartplotterOverlayLayout.TryChartTapToWorld(probe, in st, NavRigGeometry.Face.Console,
                                                         out Vector2 onConsole, out _);
            Assert.That((onConsole - onMax).magnitude, Is.GreaterThan(2f),
                        "…and the console box would have put it somewhere else entirely");
        }

        // ---- the expanded card -------------------------------------------------------------------------

        [Test]
        public void TheMaxCard_IsAnIntegerScaleOfTheMaxFace_AndCentred()
        {
            Rect card = ChartplotterOverlayLayout.ExpandedCardRect(1920f, 1080f,
                                                                   NavRigGeometry.Face.Max);
            float scale = card.width / NavRigGeometry.MaxW;
            Assert.That(scale, Is.EqualTo(Mathf.Round(scale)).Within(1e-4f),
                        "a fractional scale resamples the 3x5 rail into mush");
            Assert.That(scale, Is.GreaterThanOrEqualTo(1f));
            Assert.That(card.height / NavRigGeometry.MaxH, Is.EqualTo(scale).Within(1e-4f));
            Assert.That(card.center.x, Is.EqualTo(960f).Within(0.5f));
            Assert.That(card.center.y, Is.EqualTo(540f).Within(0.5f));

            Rect small = ChartplotterOverlayLayout.ExpandedCardRect(640f, 400f,
                                                                    NavRigGeometry.Face.Max);
            Assert.That(small.width, Is.EqualTo((float)NavRigGeometry.MaxW).Within(1e-3f),
                        "never below 1x — a downscaled rig is illegible");
        }

        [Test]
        public void TheDefaultOverload_IsStillTheConsoleFace()
        {
            // A pre-existing caller must not silently start getting a MAX-sized card.
            Assert.That(ChartplotterOverlayLayout.ExpandedCardRect(1920f, 1080f),
                        Is.EqualTo(ChartplotterOverlayLayout.ExpandedCardRect(
                                       1920f, 1080f, NavRigGeometry.Face.Console)));
        }

        // ---- the three panes actually draw -------------------------------------------------------------

        [Test]
        public void TheMaxFace_DrawsARailAManagerAndAProfile_WhereTheConsoleFaceHasChart()
        {
            NavChartSource chart = Chart(false);
            DrawSurface s = PaintMax(State(), Max(sel: 0), chart);
            NavRigGeometry.Layout L = MaxLayout;

            Assert.That(ColourCount(s, L.Rail), Is.GreaterThan(3),
                        "the rail is buttons and text, not a flat panel");
            Assert.That(ColourCount(s, L.Right), Is.GreaterThan(3),
                        "the manager column lists something");
            Assert.That(ColourCount(s, L.Profile), Is.GreaterThan(2),
                        "the profile strip has a seabed under its sky");
        }

        [Test]
        public void EveryStringTheMaxFacePrints_HasGlyphsInTheSharedFont()
        {
            // The loud-tofu guard logs an ERROR for a character the 3x5 table cannot draw, which is a
            // test failure in itself. Two of the source's own labels ('&' in "TIDE & SET", the tide's
            // up/down arrows) have no glyph in the SOURCE's font either, and are substituted at their
            // call sites — this is the assertion that says so rather than the comment.
            RigDrawUtil.ResetUnknownGlyphReports();
            NavChartSource chart = Chart(false);

            PaintMax(State(), Max(NavChartTool.Measure, NavChartLayers.All, 1,
                                  NavMeasureState.Complete, true, "WRECK 12"), chart);
            PaintMax(State(night: true), Max(NavChartTool.Route, NavChartLayers.All, 0), chart);
            // …and the honesty branches, which print different words.
            PaintMax(new NavRigState(Vector2.zero, false, 0f, 0f, 0.2f, NavRigGeometry.Orient.North,
                                     false, true, float.NaN, false),
                     new NavMaxState(NavChartTool.Pan, NavChartLayers.All, -1, NavMeasureState.Off,
                                     Vector2.zero, Vector2.zero, float.NaN, float.NaN, false, ""),
                     new NavChartSource());

            LogAssert.NoUnexpectedReceived();
        }

        // ---- the differentials, one per control --------------------------------------------------------

        [Test]
        public void EachLIVELayer_MovesTheChart()
        {
            NavChartSource chart = Chart(false);
            NavRigState st = State();
            RectInt ch = MaxLayout.Chart;
            DrawSurface all = PaintMax(in st, Max(), chart);

            foreach (NavChartLayers layer in new[]
                     { NavChartLayers.Depth, NavChartLayers.Poi, NavChartLayers.Track })
            {
                NavMaxState off = Max(layers: NavChartLayers.All & ~layer);
                Assert.That(RegionDiff(all, PaintMax(in st, in off, chart), ch), Is.GreaterThan(0),
                            $"{layer} is a LIVE switch — turning it off must change the chart, or it " +
                            "is a light that does nothing (#421)");
            }
        }

        [Test]
        public void EachDORMANTLayer_MovesNothingOnTheChart()
        {
            // The other half of the same lesson. LAND, ROCK, BUOY and TRFC have no source in this world
            // (see NavChartLayers), and the rig itself never reads layers.land. They light in the rail —
            // the source authors that — and they must move NOT ONE chart pixel, so the difference
            // between "inert on purpose" and "broken" is a fact rather than a claim.
            NavChartSource chart = Chart(false);
            NavRigState st = State();
            RectInt ch = MaxLayout.Chart;
            DrawSurface all = PaintMax(in st, Max(), chart);

            foreach (NavChartLayers layer in new[]
                     { NavChartLayers.Land, NavChartLayers.Rocks, NavChartLayers.Buoys,
                       NavChartLayers.Traffic })
            {
                Assert.That((layer & NavChartLayers.Live), Is.EqualTo(NavChartLayers.None),
                            $"{layer} is documented dormant");
                NavMaxState off = Max(layers: NavChartLayers.All & ~layer);
                Assert.That(RegionDiff(all, PaintMax(in st, in off, chart), ch), Is.EqualTo(0),
                            $"{layer} has no source here — it must not pretend to switch one");
            }
        }

        [Test]
        public void TheMeasureLine_DrawsOnTheChartAndReadsOutInTheColumn()
        {
            NavChartSource chart = Chart(false);
            NavRigState st = State();
            NavRigGeometry.Layout L = MaxLayout;

            DrawSurface off = PaintMax(in st, Max(NavChartTool.Measure), chart);
            NavMaxState complete = Max(NavChartTool.Measure, measure: NavMeasureState.Complete);
            DrawSurface on = PaintMax(in st, in complete, chart);

            Assert.That(RegionDiff(off, on, L.Chart), Is.GreaterThan(0), "the line is on the water");
            Assert.That(RegionDiff(off, on, L.Right), Is.GreaterThan(0),
                        "…and its range and bearing are in the CURSOR pane, not only on the chart");

            // Anchored (mid-drag) draws the line but not the label — the source's own two states.
            NavMaxState anchored = Max(NavChartTool.Measure, measure: NavMeasureState.Anchored);
            Assert.That(RegionDiff(PaintMax(in st, in anchored, chart), on, L.Chart),
                        Is.GreaterThan(0), "a half-drawn line is not a finished one");
        }

        [Test]
        public void SelectingAWaypoint_RingsItOnTheChartAndLightsItsRow()
        {
            NavChartSource chart = Chart(false);
            NavRigState st = State();
            NavRigGeometry.Layout L = MaxLayout;

            DrawSurface none = PaintMax(in st, Max(), chart);
            DrawSurface picked = PaintMax(in st, Max(sel: 1), chart);

            Assert.That(RegionDiff(none, picked, L.Right), Is.GreaterThan(0),
                        "the manager's row lights (navRig.js:427)");
            Assert.That(RegionDiff(none, picked, L.Chart), Is.GreaterThan(0),
                        "…and the mark you picked is ringed on the water (navRig.js:293), which is what " +
                        "makes the column and the chart one instrument");
        }

        [Test]
        public void TheNameEditor_ShowsItsBufferInTheColumn()
        {
            NavChartSource chart = Chart(false);
            NavRigState st = State();
            RectInt col = MaxLayout.Right;

            DrawSurface closed = PaintMax(in st, Max(sel: 0), chart);
            NavMaxState typing = Max(sel: 0, editing: true, text: "WRECK");
            DrawSurface open = PaintMax(in st, in typing, chart);
            NavMaxState typedMore = Max(sel: 0, editing: true, text: "WRECKS");

            Assert.That(RegionDiff(closed, open, col), Is.GreaterThan(0), "the field appears");
            Assert.That(RegionDiff(open, PaintMax(in st, in typedMore, chart), col),
                        Is.GreaterThan(0), "…and each character reaches the glass");
        }

        [Test]
        public void NightReachesEveryNewPane()
        {
            NavRigGeometry.Layout L = MaxLayout;
            DrawSurface day = PaintMax(State(), Max(sel: 0), Chart(false));
            DrawSurface night = PaintMax(State(night: true), Max(sel: 0), Chart(true));

            Assert.That(RegionDiff(day, night, L.Rail), Is.GreaterThan(0), "the rail follows the OR rule");
            Assert.That(RegionDiff(day, night, L.Right), Is.GreaterThan(0), "so does the manager");
            Assert.That(RegionDiff(day, night, L.Profile), Is.GreaterThan(0), "so does the profile");
        }

        [Test]
        public void TheToolSelection_LightsTheRail()
        {
            NavChartSource chart = Chart(false);
            NavRigState st = State();
            RectInt rail = MaxLayout.Rail;
            DrawSurface pan = PaintMax(in st, Max(NavChartTool.Pan), chart);

            foreach (NavChartTool tool in new[]
                     { NavChartTool.Mark, NavChartTool.Route, NavChartTool.Measure })
            {
                NavMaxState picked = Max(tool);
                Assert.That(RegionDiff(pan, PaintMax(in st, in picked, chart), rail),
                            Is.GreaterThan(0), $"{tool} must light its rail button");
            }
        }

        // ---- NEGATIVE CONTROL --------------------------------------------------------------------------

        [Test]
        public void NegativeControl_TheDiffProbe_CanRegisterNoChange()
        {
            NavChartSource chart = Chart(false);
            NavRigState st = State();
            NavMaxState mx = Max(sel: 0);
            Assert.That(DiffCount(PaintMax(in st, in mx, chart), PaintMax(in st, in mx, chart)),
                        Is.EqualTo(0),
                        "the MAX render is deterministic, or every diff above is noise");
        }

        // ---- the depth-ahead profile -------------------------------------------------------------------

        [Test]
        public void TheProfile_ReSamplesPerLine_AndNotPerCall()
        {
            NavChartSource chart = Chart(false);
            var profile = new NavDepthProfile();
            var at = new Vector2(360f, 300f);

            Assert.That(profile.EnsureBaked(chart, at, true, 62f, 0.2f, 90), Is.True, "the first call");
            Assert.That(profile.BakeCount, Is.EqualTo(1));
            for (int i = 0; i < 50; i++)
                Assert.That(profile.EnsureBaked(chart, at, true, 62f, 0.2f, 90), Is.False,
                            "…and no later call with the same line");
            Assert.That(profile.BakeCount, Is.EqualTo(1), "rule 7: never per frame");

            Assert.That(profile.EnsureBaked(chart, at, true, 63f, 0.2f, 90), Is.True,
                        "a degree of heading is a different line");
            Assert.That(profile.EnsureBaked(chart, at + new Vector2(60f, 0f), true, 63f, 0.2f, 90),
                        Is.True, "…and so is moving the boat");
        }

        [Test]
        public void TheProfile_ReadsTheSameSurveyTheChartShades()
        {
            NavChartSource chart = Chart(false);
            var profile = new NavDepthProfile();
            var at = new Vector2(200f, 300f);
            profile.EnsureBaked(chart, at, true, 90f, 0.2f, 60);      // due east, into deeper water

            Assert.That(profile.HasProfile, Is.True);
            Assert.That(profile.Count, Is.EqualTo(60));
            Assert.That(profile.Depths[0], Is.EqualTo(Mathf.Max(0f, chart.DepthAt(at))).Within(0.01f),
                        "the first sample IS the depth under the boat — one survey, two readers");
            Assert.That(profile.Depths[59], Is.GreaterThan(profile.Depths[0]),
                        "the fake coast deepens eastward, and the strip must show it");
            Assert.That(profile.MaxDepth, Is.GreaterThanOrEqualTo(6f), "the rig's axis floor (js:451)");
            Assert.That(profile.MaxDepth % 2f, Is.EqualTo(0f).Within(1e-4f), "…and its even rule");
        }

        [Test]
        public void WithNoSurvey_ThereIsNoProfile_RatherThanASeabedAtZero()
        {
            var empty = new NavChartSource();          // never baked — no terrain
            var profile = new NavDepthProfile();
            profile.EnsureBaked(empty, new Vector2(360f, 300f), true, 62f, 0.2f, 60);
            Assert.That(profile.HasProfile, Is.False, "the chart's own honesty rule");

            Assert.DoesNotThrow(() => PaintMax(State(), Max(), empty),
                                "and the whole face still draws, blank");
        }

        [Test]
        public void WithNoFix_ThereIsNoProfileEither()
        {
            NavChartSource chart = Chart(false);
            var profile = new NavDepthProfile();
            profile.EnsureBaked(chart, new Vector2(360f, 300f), hasBoat: false, 62f, 0.2f, 60);
            Assert.That(profile.HasProfile, Is.False,
                        "DEPTH AHEAD is measured from own ship — with no fix there is no 'ahead'");
        }

        // ---- the name editor's charset rule ------------------------------------------------------------

        [Test]
        public void TheNameEditor_FoldsWhatYouTypeIntoTheFontsCharset()
        {
            var ed = new NavNameEditor();
            ed.Open(0, "");

            foreach (char ch in "wreck-1.a/b") Assert.That(ed.Type(ch), Is.True, $"'{ch}' is drawable");
            Assert.That(ed.Text, Is.EqualTo("WRECK-1.A/B"), "upper-cased, because the glass is");

            ed.Open(0, "");
            Assert.That(ed.Type('é'), Is.False, "an accented letter has no glyph — dropped, not tofu'd");
            Assert.That(ed.Type('#'), Is.False);
            Assert.That(ed.Type(':'), Is.False, "the font HAS a colon, but a name does not want one");
            Assert.That(ed.Type('·'), Is.False,
                        "…and U+00B7 is an authored BLANK, so typing it would be an invisible character");
            Assert.That(ed.Text, Is.EqualTo(""), "…and none of them reached the buffer");
        }

        [Test]
        public void TheNameEditor_RefusesALeadingSpace_ACapAndAnEmptyCommit()
        {
            var ed = new NavNameEditor();
            ed.Open(0, "");
            Assert.That(ed.Type(' '), Is.False, "a leading space is invisible and sorts wrong");

            for (int i = 0; i < NavNameEditor.MaxLength + 6; i++) ed.Type('A');
            Assert.That(ed.Text.Length, Is.EqualTo(NavNameEditor.MaxLength),
                        "the chart plate is sized to the string — an unbounded name smears over the water");

            Assert.That(ed.Backspace(), Is.True);
            Assert.That(ed.Text.Length, Is.EqualTo(NavNameEditor.MaxLength - 1));

            ed.Open(1, "");
            Assert.That(ed.TryCommit(out string blank), Is.False,
                        "Enter on an empty field must not blank a waypoint's label");
            Assert.That(blank, Is.Empty);
            Assert.That(ed.IsOpen, Is.False, "…and it still closes");
        }

        [Test]
        public void TheNameEditor_SanitisesTheSEEDToo_NotJustTypedKeys()
        {
            var ed = new NavNameEditor();
            ed.Open(2, "wrecké #7");
            Assert.That(ed.Text, Is.EqualTo("WRECK 7"),
                        "a name from an older build or an importer cannot smuggle an undrawable " +
                        "character in through the seed");
            Assert.That(ed.Target, Is.EqualTo(2));

            Assert.That(NavNameEditor.Sanitize(null), Is.Empty);
            Assert.That(NavNameEditor.Sanitize("MARK 1"), Is.EqualTo("MARK 1"),
                        "the identity on a name that is already clean — including the rig's own");
        }

        [Test]
        public void TheNameEditor_CommitsTrimmed_AndCloses()
        {
            var ed = new NavNameEditor();
            ed.Open(0, "");
            foreach (char ch in "FUEL  ") ed.Type(ch);
            Assert.That(ed.TryCommit(out string name), Is.True);
            Assert.That(name, Is.EqualTo("FUEL"), "trailing space is not part of the name");
            Assert.That(ed.IsOpen, Is.False);
            Assert.That(ed.Type('X'), Is.False, "a closed editor takes nothing");
        }
    }
}
