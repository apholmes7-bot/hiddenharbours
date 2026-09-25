using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The ambient fleet fishes on WATER.</b> Terrain pass 9, decision 12: the shipped grounds
    /// rectangle, centre (5, −32) × 85 × 22, lay on the island. Of its 7,480 half-metre cells, 7,300 were
    /// dry at mean tide and 13 held the 0.4 m margin at spring low. Over the 448 plans this guard runs,
    /// 1,551 of 1,792 boat-days got no spot at all and the boat was hidden for the day. The other 241 got
    /// one spot of two, and 110 of those spots held the margin only because
    /// <see cref="AmbientFleetPlan"/> halves it when it cannot fill a boat.
    ///
    /// <para><b>The subjects are the shipped asset and the planner on St Peters' own terrain.</b> The
    /// scene binds the analytic <see cref="TidalTerrain"/> (the only terrain component in
    /// <c>StPeters.unity</c>), configured by <see cref="StPetersBuilder.ConfigureTidalTerrain"/>, which is
    /// what this fixture builds.</para>
    ///
    /// <para><b>The bar is independent of the planner.</b> Depth is read straight off
    /// <see cref="TidalTerrain.ElevationAt"/> against spring low from the builder's tide constants, and the
    /// margin is the asset's <see cref="AmbientFleetDef.MinDepthMeters"/>. Nothing here calls
    /// <see cref="AmbientFleetPlan.IsPlannable"/> or <see cref="AmbientFleetPlan.IsLegClear"/>, and the
    /// planner's relaxation is exactly what the spot and leg checks would catch.</para>
    ///
    /// <para><b>The clearance</b> is <see cref="AmbientFleetDef.BoatAvoidRadius"/> +
    /// <see cref="AmbientFleetDef.PlayerAvoidRadius"/>. Avoidance can push a fleet boat up to the boat
    /// radius outside the rectangle, and from there a player standing at a feature's edge must still be
    /// outside the fleet's player ring. Feature positions are read from their sources.</para>
    ///
    /// <para>⚠ <b>PR 5 (painted-map adoption) must move this guard to the scene's new terrain.</b> When
    /// St Peters stops binding the analytic <see cref="TidalTerrain"/>, the fixture below measures a sea
    /// nobody plays; build the painted terrain here instead and re-run every assertion against it.</para>
    /// </summary>
    public class AmbientFleetGroundsTests
    {
        private const string AssetPath = "Assets/_Project/Data/Boats/StPetersAmbientFleet.asset";

        // The stated seeds: EnvironmentService's default world seed (12345), the edges of the int range's
        // sign, and a few arbitrary ones. Days 0–55 are eight weeks of fleet plans.
        private static readonly int[] Seeds = { 12345, 0, 1, 7, 42, 2026, -1, 99991 };
        private const int Days = 56;

        // How finely the guard samples a rectangle and a leg. Finer than the planner's own
        // LegSampleStepMeters (4 m), so a leg that clips a spur between the planner's samples still fails.
        private const float AreaSampleMetres = 0.5f;
        private const float LegSampleMetres = 0.25f;

        // Frozen margins from the pass-9 plan (docs/design/st-peters-terrain-pass-9.md §2.1, the passages
        // row): 10 m round each passage point and a 20 m band inside the map edge. They are design
        // numbers with no code constant.
        private const float PassageRoundMetres = 10f;
        private const float MapEdgeBandMetres = 20f;

        private static float SpringLow => StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;

        private GameObject _go;
        private TidalTerrain _terrain;
        private AmbientFleetDef _def;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_AmbientFleetGroundsTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
            _def = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(AssetPath);
            Assert.IsNotNull(_def, $"{AssetPath} must load as an AmbientFleetDef");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        private Rect Grounds => new Rect(_def.GroundsCenter - _def.GroundsSize * 0.5f, _def.GroundsSize);

        private float DepthAtSpringLow(Vector2 p) => SpringLow - _terrain.ElevationAt(p);

        // =============================================================================================
        // 1. the rectangle is water
        // =============================================================================================

        /// <summary>
        /// Every point of the rectangle, grown by <see cref="AmbientFleetDef.DepthLookAheadMeters"/>, holds
        /// <see cref="AmbientFleetDef.MinDepthMeters"/> at spring low. The growth means the live depth
        /// probe, which looks that far ahead of a moving boat, never sees shoal water on a planned route.
        /// </summary>
        [Test]
        public void GroundsRectangle_GrownByTheLookAhead_HoldsTheMarginAtSpringLow()
        {
            Rect r = Grounds;
            float grow = _def.DepthLookAheadMeters;
            var area = Rect.MinMaxRect(r.xMin - grow, r.yMin - grow, r.xMax + grow, r.yMax + grow);

            int cols = Mathf.CeilToInt(area.width / AreaSampleMetres);
            int rows = Mathf.CeilToInt(area.height / AreaSampleMetres);
            int samples = 0, shallow = 0;
            float worst = float.MaxValue;
            Vector2 worstAt = default;
            for (int j = 0; j <= rows; j++)
            for (int i = 0; i <= cols; i++)
            {
                var p = new Vector2(Mathf.Min(area.xMin + i * AreaSampleMetres, area.xMax),
                                    Mathf.Min(area.yMin + j * AreaSampleMetres, area.yMax));
                float d = DepthAtSpringLow(p);
                samples++;
                if (d < _def.MinDepthMeters) shallow++;
                if (d < worst) { worst = d; worstAt = p; }
            }

            Assert.AreEqual(0, shallow,
                $"grounds {r} grown by {grow} m: {shallow} of {samples} samples hold less than " +
                $"{_def.MinDepthMeters} m at spring low ({SpringLow} m). Shallowest {worst:F2} m at {worstAt}.");
        }

        // =============================================================================================
        // 2. the planner fills every boat, unrelaxed
        // =============================================================================================

        /// <summary>
        /// Over the stated seeds and days, every boat gets exactly <see cref="AmbientFleetDef.SpotsPerBoat"/>
        /// spots, and every spot and every leg (the closing leg back to the first spot included) holds
        /// <see cref="AmbientFleetDef.MinDepthMeters"/> at spring low. A spot the planner reached by
        /// halving the margin fails here.
        /// </summary>
        [Test]
        public void EveryBoatFillsItsSpots_AndEverySpotAndLegHoldsTheMargin()
        {
            Rect grounds = Grounds;
            float margin = _def.MinDepthMeters;
            int plans = 0, boats = 0, wrongCount = 0, spots = 0, shallowSpots = 0, legs = 0, shallowLegs = 0;
            var firstFailures = new StringBuilder();
            int noted = 0;

            void Note(string line)
            {
                if (noted++ < 8) firstFailures.AppendLine(line);
            }

            foreach (int seed in Seeds)
            for (int day = 0; day < Days; day++)
            {
                Vector2[][] plan = AmbientFleetPlan.PlanFleet(seed, _def.Id, day, _def.BoatCount,
                    _def.SpotsPerBoat, grounds, _terrain.ElevationAt, SpringLow, margin,
                    _def.SpotSpacingMeters, _def.LegSampleStepMeters, _def.MaxCandidateTries);
                plans++;
                Assert.AreEqual(_def.BoatCount, plan.Length, $"seed {seed} day {day}: one plan per boat");

                for (int b = 0; b < plan.Length; b++)
                {
                    boats++;
                    Vector2[] s = plan[b];
                    int n = s == null ? 0 : s.Length;
                    if (n != _def.SpotsPerBoat)
                    {
                        wrongCount++;
                        Note($"seed {seed} day {day} boat {b}: {n} spots, wants {_def.SpotsPerBoat}");
                    }

                    for (int k = 0; k < n; k++)
                    {
                        spots++;
                        float d = DepthAtSpringLow(s[k]);
                        if (d < margin)
                        {
                            shallowSpots++;
                            Note($"seed {seed} day {day} boat {b} spot {k} {s[k]}: {d:F2} m");
                        }

                        if (n < 2) continue;
                        legs++;
                        Vector2 from = s[k], to = s[(k + 1) % n];
                        float legWorst = ShallowestAlong(from, to, out Vector2 at);
                        if (legWorst < margin)
                        {
                            shallowLegs++;
                            Note($"seed {seed} day {day} boat {b} leg {from}→{to}: {legWorst:F2} m at {at}");
                        }
                    }
                }
            }

            Assert.IsTrue(wrongCount == 0 && shallowSpots == 0 && shallowLegs == 0,
                $"grounds {grounds}, {plans} plans, {boats} boat-days: {wrongCount} boats without exactly " +
                $"{_def.SpotsPerBoat} spots, {shallowSpots} of {spots} spots and {shallowLegs} of {legs} legs " +
                $"under {margin} m at spring low ({SpringLow} m).\n{firstFailures}");
        }

        private float ShallowestAlong(Vector2 from, Vector2 to, out Vector2 at)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / LegSampleMetres));
            float worst = float.MaxValue;
            at = from;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                float d = DepthAtSpringLow(p);
                if (d < worst) { worst = d; at = p; }
            }
            return worst;
        }

        // =============================================================================================
        // 3. the rectangle is clear of what the pass froze
        // =============================================================================================

        /// <summary>
        /// The rectangle stands at least <see cref="AmbientFleetDef.BoatAvoidRadius"/> +
        /// <see cref="AmbientFleetDef.PlayerAvoidRadius"/> from every feature terrain pass 9 froze
        /// (docs/design/st-peters-terrain-pass-9.md §2.1), and that far inside the 20 m map-edge band.
        /// </summary>
        [Test]
        public void GroundsRectangle_StandsClearOfTheFrozenFeatures()
        {
            Rect r = Grounds;
            float clearance = _def.BoatAvoidRadius + _def.PlayerAvoidRadius;
            var failures = new StringBuilder();
            int checkedFeatures = 0;

            void Check(string name, float gap)
            {
                checkedFeatures++;
                if (gap < clearance) failures.AppendLine($"{name}: {gap:F1} m");
            }

            void Segment(string name, Vector2 a, Vector2 b, float halfWidth) =>
                Check(name, RectToSegment(r, a, b) - halfWidth);

            void Path(string name, IReadOnlyList<Vector2> points, float halfWidth)
            {
                for (int i = 0; i + 1 < points.Count; i++)
                    Segment($"{name} leg {i}", points[i], points[i + 1], halfWidth);
            }

            void Point(string name, Vector3 p, float radius) =>
                Check(name, RectToPoint(r, new Vector2(p.x, p.y)) - radius);

            void Box(string name, Rect box) => Check(name, RectToRect(r, box));

            // The beach slip and the dredged approach.
            Segment("beach slip", StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo, StPetersBuilder.BerthHalfWidth);
            Segment("dredged approach", StPetersBuilder.ApproachFrom, StPetersBuilder.ApproachTo,
                    StPetersBuilder.ApproachHalfWidth);

            // The dock, where the player steps off, the arrival point and the dory's berth.
            Point("dock", StPetersBuilder.DockZonePos, 0f);
            Point("disembark", StPetersBuilder.DisembarkPos, 0f);
            Point("arrival", StPetersBuilder.ArrivalPos, 0f);
            Point("dory berth", StPetersBuilder.DoryMooredPos, 0f);

            // The arrival route is the entrance fairway, read off the channel the marks are laid on.
            NavChannel entrance = StPetersNavMarks.Entrance;
            Path("entrance fairway", entrance.Waypoints, entrance.HalfWidthMetres);

            // The wharf deck: cells RootCellX..HeadCellX by MinCellY..MaxCellY.
            Box("wharf", Rect.MinMaxRect(StPetersWharf.RootCellX, StPetersWharf.MinCellY,
                                         StPetersWharf.HeadCellX + 1, StPetersWharf.MaxCellY + 1));

            // The sandbar, the gut cut through it, and the gut's buoyed fairway.
            Segment("sandbar", StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, StPetersBuilder.SandbarHalfWidth);
            Vector2 gut = StPetersNavMarks.GutCentre();
            Vector2 across = StPetersNavMarks.GutAxis();
            Segment("bar gut", gut - across * StPetersBuilder.SandbarHalfWidth,
                    gut + across * StPetersBuilder.SandbarHalfWidth, StPetersBuilder.ChannelHalfWidth);
            NavChannel barGut = StPetersNavMarks.BarGut;
            Path("bar gut fairway", barGut.Waypoints, barGut.HalfWidthMetres);

            // The passages: 10 m round each point, and each trigger band as the builder sizes it.
            Point("Nine Mile Creek passage", StPetersBuilder.ToNineMileCreekPassagePos, PassageRoundMetres);
            Point("West Water passage", StPetersBuilder.ToWestWaterPassagePos, PassageRoundMetres);
            Point("East Water passage", StPetersBuilder.ToEastWaterPassagePos, PassageRoundMetres);
            Point("East Water arrival", StPetersBuilder.FromEastWaterArrivalPos, PassageRoundMetres);
            Box("Nine Mile Creek band", Band(StPetersBuilder.ToNineMileCreekPassagePos,
                                             new Vector2(3f, StPetersBuilder.SandbarHalfWidth * 2f)));
            Box("West Water band", Band(StPetersBuilder.ToWestWaterPassagePos, StPetersBuilder.WestWaterPassageBandSize));
            Box("East Water band", Band(StPetersBuilder.ToEastWaterPassagePos, StPetersBuilder.EastWaterPassageBandSize));

            // The nav marks, where the region's own mark pass puts them on this terrain.
            NavMarkPlanResult marks = StPetersNavMarks.Plan(_terrain);
            Assert.IsNotEmpty(marks.Marks, "the mark pass must place marks, or this clearance proves nothing");
            foreach (PlannedNavMark m in marks.Marks)
                Point($"mark {m.Id}", m.At, 0f);

            // #875's east-water wake frame. It is not on main at c25d952d, so it has no source to read yet;
            // when it lands, read it from there instead of this literal.
            Box("#875 wake frame", Rect.MinMaxRect(306f, 19f, 366f, 63f));

            // The map edge: the rectangle stays inside the region, less the 20 m band, less the clearance.
            Vector2 half = StPetersBuilder.RegionWorldSize * 0.5f;
            Vector2 c = StPetersBuilder.RegionWorldCenter;
            Check("map edge west", r.xMin - (c.x - half.x + MapEdgeBandMetres));
            Check("map edge east", (c.x + half.x - MapEdgeBandMetres) - r.xMax);
            Check("map edge south", r.yMin - (c.y - half.y + MapEdgeBandMetres));
            Check("map edge north", (c.y + half.y - MapEdgeBandMetres) - r.yMax);

            Assert.IsTrue(failures.Length == 0,
                $"grounds {r} must stand {clearance} m (BoatAvoidRadius + PlayerAvoidRadius) clear of " +
                $"{checkedFeatures} features; these are closer:\n{failures}");
        }

        private static Rect Band(Vector3 centre, Vector2 size) =>
            new Rect(new Vector2(centre.x, centre.y) - size * 0.5f, size);

        private static float RectToPoint(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float RectToRect(Rect a, Rect b)
        {
            float dx = Mathf.Max(a.xMin - b.xMax, 0f, b.xMin - a.xMax);
            float dy = Mathf.Max(a.yMin - b.yMax, 0f, b.yMin - a.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Shortest distance between a rectangle and a segment; zero when they touch.</summary>
        private static float RectToSegment(Rect r, Vector2 a, Vector2 b)
        {
            if (r.Contains(a) || r.Contains(b)) return 0f;
            var corners = new[]
            {
                new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
            };
            float best = Mathf.Min(RectToPoint(r, a), RectToPoint(r, b));
            for (int i = 0; i < 4; i++)
            {
                Vector2 c0 = corners[i], c1 = corners[(i + 1) % 4];
                if (SegmentsCross(a, b, c0, c1)) return 0f;
                best = Mathf.Min(best, PointToSegment(c0, a, b));
            }
            return best;
        }

        private static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float Cross(Vector2 o, Vector2 u, Vector2 v) => (u.x - o.x) * (v.y - o.y) - (u.y - o.y) * (v.x - o.x);
            float d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2);
            float d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }
    }
}
