using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Feature = HiddenHarbours.Tests.EditMode.AmbientFleetGroundsMeasure.Feature;
using Measure = HiddenHarbours.Tests.EditMode.AmbientFleetGroundsMeasure;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The ambient fleet fishes all the water it can reach</b> (#886; the owner: "They should fish
    /// everywhere accessible"). At spring low a point is ground when it keeps
    /// <see cref="AmbientFleetDef.MinDepthMeters"/> unrelaxed, joins the water reached from the arrival
    /// route's landfall through water that keeps it, lies inside the map less its 20 m edge, and stands the
    /// clearance off every feature the arrival works, the passages, the marks and #875's wake frame occupy.
    /// Before this the shipped grounds were one rectangle, centre (70, −95) × 85 × 22: water, but 3.5% of
    /// the water the fleet can reach.
    ///
    /// <para><b>The subjects are the shipped asset and the planner on St Peters' own terrain:</b> the
    /// asset's keep-clear list, seed point and bounds, and <see cref="AmbientFleetPlan.PlanFleetDay"/>. The
    /// scene binds the analytic <see cref="TidalTerrain"/> (the only terrain component in
    /// <c>StPeters.unity</c>), configured by <see cref="StPetersBuilder.ConfigureTidalTerrain"/>, which is
    /// what this fixture builds.</para>
    ///
    /// <para><b>The bar is independent of the planner.</b> Depth is read straight off
    /// <see cref="TidalTerrain.ElevationAt"/> against spring low from the builder's tide constants. The
    /// water reached is the guard's own flood from <see cref="StPetersArrivalOpening.Route"/>'s landfall
    /// (<see cref="AmbientFleetGroundsMeasure"/>), never the planner's. The features are read from their
    /// sources, not from the asset, and the leg cap is worked here from the Def's schedule fields. Nothing
    /// here calls <see cref="AmbientFleetWater"/>'s own tests of depth, reach or clearance.</para>
    ///
    /// <para><b>The clearance</b> is <see cref="AmbientFleetDef.BoatAvoidRadius"/> +
    /// <see cref="AmbientFleetDef.PlayerAvoidRadius"/>. Avoidance can push a fleet boat up to the boat
    /// radius off her line, and from there a player standing at a feature's edge must still be outside the
    /// fleet's player ring.</para>
    ///
    /// <para>⚠ <b>PR 5 (painted-map adoption) re-points this guard at the painted map.</b> When St Peters
    /// stops binding the analytic <see cref="TidalTerrain"/>, the fixture below measures a sea nobody plays:
    /// build the painted terrain here instead, re-run every assertion against it, and read each keep-clear
    /// feature from wherever the painted map puts it.</para>
    /// </summary>
    public class AmbientFleetGroundsTests
    {
        private const string AssetPath = "Assets/_Project/Data/Boats/StPetersAmbientFleet.asset";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // #875's wake acceptance photographs St Peters with the fleet on. Its frame is read from these two
        // files: the run it frames from the test, and the plate's aspect from the accepted evidence.
        private const string WakeAcceptancePath =
            "Assets/Tests/PlayMode/WakeCentrePhotographPlayTests.GameplayAcceptance.cs";
        private const string WakeEvidencePath = "docs/verification/wake-v24/README.md";

        // The stated seeds: EnvironmentService's default world seed (12345), the edges of the int range's
        // sign, and a few arbitrary ones. Days 0–55 are eight weeks of fleet plans.
        private static readonly int[] Seeds = { 12345, 0, 1, 7, 42, 2026, -1, 99991 };
        private const int Days = 56;

        // How finely the guard samples a leg. Finer than the planner's own LegSampleStepMeters (4 m), so a
        // leg that clips a spur between the planner's samples still fails.
        private const float LegSampleMetres = 0.25f;

        // The guard's own flood grid, finer than the planner's AccessCellMeters.
        private const float FloodCellMetres = 2f;

        // Frozen margins from the pass-9 plan (docs/design/st-peters-terrain-pass-9.md §2.1, the passages
        // row): 10 m round each passage point and a 20 m band inside the map edge. They are design
        // numbers with no code constant.
        private const float PassageRoundMetres = 10f;
        private const float MapEdgeBandMetres = 20f;

        // How closely the asset's keep-clear list must match its sources. The asset holds a mark to seven
        // significant figures and the wake frame's corners rounded outward to 0.1 m.
        private const float TieToleranceMetres = 0.05f;

        // The spread: see Spots_SpreadOverTheAccessibleWater for the measure, the bar and the reason.
        private const float SpreadSquareMetres = 40f;
        private const double SpreadBar = 0.90;

        private static float SpringLow => StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;

        private GameObject _go;
        private TidalTerrain _terrain;
        private AmbientFleetDef _def;
        private float _secondsPerDay;
        private AmbientFleetWater _water;
        private double _waterMilliseconds;
        private List<FleetDay> _days;

        private struct FleetDay
        {
            public int Seed;
            public int Day;
            public Vector2[][] Spots;
            public float[] Margins;
        }

        [OneTimeSetUp]
        public void PlanTheStatedDays()
        {
            _go = new GameObject("TidalTerrain_AmbientFleetGroundsTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
            _def = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(AssetPath);
            Assert.IsNotNull(_def, $"{AssetPath} must load as an AmbientFleetDef");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} must load as a GameConfig");
            _secondsPerDay = config.SecondsPerDay;

            // What the presenter does on a region load: measure the water once, then plan each day from it.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            _water = AmbientFleetWater.For(_def, _terrain.ElevationAt, SpringLow);
            _water.EligibleCount(_def.MinDepthMeters);   // the flood runs on first ask
            _waterMilliseconds = clock.Elapsed.TotalMilliseconds;

            _days = new List<FleetDay>(Seeds.Length * Days);
            foreach (int seed in Seeds)
            for (int day = 0; day < Days; day++)
            {
                var margins = new float[_def.BoatCount];
                Vector2[][] spots = AmbientFleetPlan.PlanFleetDay(_def, _water, seed, day, _def.BoatCount,
                                                                  _secondsPerDay, margins);
                _days.Add(new FleetDay { Seed = seed, Day = day, Spots = spots, Margins = margins });
            }
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        private float DepthAtSpringLow(Vector2 p) => SpringLow - _terrain.ElevationAt(p);

        private float Clearance => _def.BoatAvoidRadius + _def.PlayerAvoidRadius;

        private static Rect Region => new Rect(StPetersBuilder.RegionWorldCenter - StPetersBuilder.RegionWorldSize * 0.5f,
                                               StPetersBuilder.RegionWorldSize);

        private static Rect Inset(Rect r, float by) => Rect.MinMaxRect(r.xMin + by, r.yMin + by, r.xMax - by, r.yMax - by);

        private static Vector2 Landfall => StPetersArrivalOpening.Route()[0];

        // Each leg a boat sails: spot k to spot k+1, the closing leg back to the first included. Two spots
        // make one leg, sailed out and back.
        private static IEnumerable<(Vector2 From, Vector2 To)> Legs(Vector2[] spots)
        {
            int n = spots == null ? 0 : spots.Length;
            if (n < 2) yield break;
            for (int k = 0; k < (n == 2 ? 1 : n); k++) yield return (spots[k], spots[(k + 1) % n]);
        }

        // =============================================================================================
        // 1. the asset's keep-clear list, seed point and bounds are their sources
        // =============================================================================================

        /// <summary>
        /// The planner reads the features it keeps clear of from the asset, as data. Each entry must be its
        /// source's feature (same name, same shape, within <see cref="TieToleranceMetres"/>), every source
        /// feature must have an entry, and nothing else may be listed. The seed point is the arrival route's
        /// landfall, and the bounds are the region less its 20 m edge.
        /// </summary>
        [Test]
        public void KeepClearAreas_SeedPointAndBounds_MatchTheirSources()
        {
            var failures = new StringBuilder();
            var listed = new Dictionary<string, FleetKeepClearArea>();
            foreach (FleetKeepClearArea k in _def.KeepClear ?? Array.Empty<FleetKeepClearArea>())
            {
                if (listed.ContainsKey(k.Name ?? "")) failures.AppendLine($"'{k.Name}' is listed twice");
                else listed.Add(k.Name ?? "", k);
            }

            var sourced = new HashSet<string>();
            foreach (Feature f in SourceFeatures())
            {
                sourced.Add(f.Name);
                if (!listed.TryGetValue(f.Name, out FleetKeepClearArea k))
                    failures.AppendLine($"'{f.Name}' is missing from the asset");
                else if (Mismatch(f, k) is string why)
                    failures.AppendLine($"'{f.Name}': {why}");
            }
            foreach (string name in listed.Keys)
                if (!sourced.Contains(name)) failures.AppendLine($"'{name}' is in the asset but has no source here");

            if (Vector2.Distance(_def.AccessSeedPoint, Landfall) > TieToleranceMetres)
                failures.AppendLine($"AccessSeedPoint {_def.AccessSeedPoint.ToString("F3")} is not the arrival " +
                                    $"route's landfall {Landfall.ToString("F3")}");

            Rect bounds = Inset(Region, MapEdgeBandMetres);
            if (Vector2.Distance(_def.GroundsCenter, bounds.center) > TieToleranceMetres ||
                Vector2.Distance(_def.GroundsSize, bounds.size) > TieToleranceMetres)
                failures.AppendLine($"GroundsCenter {_def.GroundsCenter} × GroundsSize {_def.GroundsSize} is not the " +
                                    $"region less its {MapEdgeBandMetres} m edge, {bounds.center} × {bounds.size}");

            Assert.IsTrue(failures.Length == 0,
                $"{AssetPath} must carry every keep-clear feature as its source places it:\n{failures}");
        }

        private static string Mismatch(Feature f, FleetKeepClearArea k)
        {
            bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) <= TieToleranceMetres;
            string radius = Mathf.Abs(k.Radius - f.HalfWidth) <= TieToleranceMetres
                ? null
                : $"radius {k.Radius} in the asset, {f.HalfWidth} at the source";

            if (f.IsBox)
            {
                if (k.Shape != FleetKeepClearShape.Box) return $"a box at the source, a {k.Shape} in the asset";
                Vector2 min = Vector2.Min(k.A, k.B), max = Vector2.Max(k.A, k.B);
                if (!Near(min, f.A) || !Near(max, f.B))
                    return $"box {min.ToString("F3")}–{max.ToString("F3")} in the asset, " +
                           $"{f.A.ToString("F3")}–{f.B.ToString("F3")} at the source";
                return radius;
            }

            if (k.Shape != FleetKeepClearShape.Capsule) return $"a capsule at the source, a {k.Shape} in the asset";
            if (!(Near(k.A, f.A) && Near(k.B, f.B)) && !(Near(k.A, f.B) && Near(k.B, f.A)))
                return $"{k.A.ToString("F3")}→{k.B.ToString("F3")} in the asset, " +
                       $"{f.A.ToString("F3")}→{f.B.ToString("F3")} at the source";
            return radius;
        }

        // =============================================================================================
        // 2. every boat fills its spots, unrelaxed, and every spot and leg holds the margin
        // =============================================================================================

        /// <summary>
        /// Over the stated seeds and days, every boat gets exactly <see cref="AmbientFleetDef.SpotsPerBoat"/>
        /// spots at the asset's margin (none relaxed). Every spot and every leg (the closing leg included)
        /// holds <see cref="AmbientFleetDef.MinDepthMeters"/> at spring low, and no leg is longer than the
        /// slowest boat sails between two work windows.
        /// </summary>
        [Test]
        public void EveryBoatFillsItsSpots_AndEverySpotAndLegHoldsTheMargin_Unrelaxed()
        {
            TestContext.WriteLine($"accessible water: {_water.Columns}×{_water.Rows} cells of {_water.CellMeters} m, " +
                                  $"{_water.EligibleCount(_def.MinDepthMeters)} eligible, measured in " +
                                  $"{_waterMilliseconds:F1} ms, about {_water.ApproximateBytes / 1024} KB");

            float margin = _def.MinDepthMeters;

            // The leg cap, worked from the Def's schedule: a boat bears away at the end of her work window
            // and must be on her next spot when the next window opens, at her slowest speed. The window's
            // end is held 1% of a slot past its start, as the presenter holds it.
            float start = Mathf.Clamp01(_def.WorkWindowStartFraction);
            float end = Mathf.Clamp(Mathf.Max(_def.WorkWindowEndFraction, start + 0.01f), start, 1f);
            float slotSeconds = _secondsPerDay / Mathf.Max(1, _def.SlotsPerDay);
            float maxLeg = _def.MinSpeedMetersPerSecond * ((1f - end) + start) * slotSeconds;

            int boats = 0, wrongCount = 0, relaxed = 0, spots = 0, shallowSpots = 0, legs = 0, shallowLegs = 0,
                longLegs = 0;
            var firstFailures = new StringBuilder();
            int noted = 0;

            void Note(string line)
            {
                if (noted++ < 8) firstFailures.AppendLine(line);
            }

            foreach (FleetDay d in _days)
            {
                Assert.AreEqual(_def.BoatCount, d.Spots.Length, $"seed {d.Seed} day {d.Day}: one plan per boat");
                for (int b = 0; b < d.Spots.Length; b++)
                {
                    boats++;
                    Vector2[] s = d.Spots[b];
                    int n = s == null ? 0 : s.Length;
                    if (n != _def.SpotsPerBoat)
                    {
                        wrongCount++;
                        Note($"seed {d.Seed} day {d.Day} boat {b}: {n} spots, wants {_def.SpotsPerBoat}");
                    }
                    if (d.Margins[b] != margin)
                    {
                        relaxed++;
                        Note($"seed {d.Seed} day {d.Day} boat {b}: planned at a {d.Margins[b]} m margin, not {margin} m");
                    }

                    for (int k = 0; k < n; k++)
                    {
                        spots++;
                        float depth = DepthAtSpringLow(s[k]);
                        if (depth >= margin) continue;
                        shallowSpots++;
                        Note($"seed {d.Seed} day {d.Day} boat {b} spot {k} {s[k]}: {depth:F2} m");
                    }

                    foreach ((Vector2 from, Vector2 to) in Legs(s))
                    {
                        legs++;
                        float worst = Measure.ShallowestAlong(DepthAtSpringLow, from, to, LegSampleMetres, out Vector2 at);
                        if (worst < margin)
                        {
                            shallowLegs++;
                            Note($"seed {d.Seed} day {d.Day} boat {b} leg {from}→{to}: {worst:F2} m at {at}");
                        }
                        float length = Vector2.Distance(from, to);
                        if (length > maxLeg + 0.01f)
                        {
                            longLegs++;
                            Note($"seed {d.Seed} day {d.Day} boat {b} leg {from}→{to}: {length:F1} m, cap {maxLeg:F1} m");
                        }
                    }
                }
            }

            Assert.IsTrue(wrongCount == 0 && relaxed == 0 && shallowSpots == 0 && shallowLegs == 0 && longLegs == 0,
                $"{_days.Count} plans, {boats} boat-days: {wrongCount} boats without exactly {_def.SpotsPerBoat} spots, " +
                $"{relaxed} planned at a relaxed margin, {shallowSpots} of {spots} spots and {shallowLegs} of {legs} legs " +
                $"under {margin} m at spring low ({SpringLow} m), {longLegs} legs over {maxLeg:F1} m.\n{firstFailures}");
        }

        // =============================================================================================
        // 3. every spot is in water reached from the arrival
        // =============================================================================================

        /// <summary>
        /// Every spot joins the water the guard floods from the arrival route's landfall, through
        /// <see cref="FloodCellMetres"/> cells whose centre keeps <see cref="AmbientFleetDef.MinDepthMeters"/>
        /// at spring low. With the legs holding the margin (test 2), every leg lies in that water too. A
        /// deep pool the arrival cannot reach at spring low is never ground.
        /// </summary>
        [Test]
        public void EverySpot_JoinsTheWaterReachedFromTheLandfall()
        {
            float margin = _def.MinDepthMeters;
            Measure.Sea sea = Measure.Flood(DepthAtSpringLow, Region, FloodCellMetres, Landfall, margin);
            Assert.Greater(sea.ReachedCount, 0,
                $"the landfall {Landfall} must keep {margin} m at spring low, or the flood reaches nothing");

            int spots = 0, cutOff = 0;
            var firstFailures = new StringBuilder();
            foreach (FleetDay d in _days)
            for (int b = 0; b < d.Spots.Length; b++)
            foreach (Vector2 s in d.Spots[b])
            {
                spots++;
                if (Measure.Joins(sea, s, DepthAtSpringLow, margin, LegSampleMetres)) continue;
                if (cutOff++ < 8) firstFailures.AppendLine($"seed {d.Seed} day {d.Day} boat {b} spot {s}");
            }

            Assert.AreEqual(0, cutOff,
                $"{cutOff} of {spots} spots do not join the water reached from {Landfall} at spring low:\n{firstFailures}");
        }

        // =============================================================================================
        // 4. every spot and leg stands clear of the features and the map edge
        // =============================================================================================

        /// <summary>
        /// Every spot and every leg stands the clearance off each feature (read from its source) and inside
        /// the map less its 20 m edge less the clearance. Legs keep clear too, so a fleet boat never sails
        /// through the arrival route, the passages or #875's frame.
        /// </summary>
        [Test]
        public void EverySpotAndLeg_StandsClearOfTheFeaturesAndTheMapEdge()
        {
            List<Feature> features = SourceFeatures();
            Rect inner = Inset(Inset(Region, MapEdgeBandMetres), Clearance);
            int spots = 0, legs = 0, closeSpots = 0, closeLegs = 0;
            var firstFailures = new StringBuilder();
            int noted = 0;

            void Note(string line)
            {
                if (noted++ < 8) firstFailures.AppendLine(line);
            }

            foreach (FleetDay d in _days)
            for (int b = 0; b < d.Spots.Length; b++)
            {
                Vector2[] s = d.Spots[b];
                foreach (Vector2 p in s)
                {
                    spots++;
                    string why = !inner.Contains(p) ? "the map edge" : null;
                    foreach (Feature f in features)
                        if (why == null && f.Gap(p) < Clearance) why = $"{f.Name}, {f.Gap(p):F1} m";
                    if (why == null) continue;
                    closeSpots++;
                    Note($"seed {d.Seed} day {d.Day} boat {b} spot {p}: {why}");
                }

                foreach ((Vector2 from, Vector2 to) in Legs(s))
                {
                    legs++;
                    // A straight leg between two points inside a rectangle stays inside it.
                    string why = !inner.Contains(from) || !inner.Contains(to) ? "the map edge" : null;
                    foreach (Feature f in features)
                        if (why == null && f.Gap(from, to) < Clearance) why = $"{f.Name}, {f.Gap(from, to):F1} m";
                    if (why == null) continue;
                    closeLegs++;
                    Note($"seed {d.Seed} day {d.Day} boat {b} leg {from}→{to}: {why}");
                }
            }

            Assert.IsTrue(closeSpots == 0 && closeLegs == 0,
                $"{closeSpots} of {spots} spots and {closeLegs} of {legs} legs stand within {Clearance} m " +
                $"(BoatAvoidRadius + PlayerAvoidRadius) of {features.Count} features or the map edge:\n{firstFailures}");
        }

        // =============================================================================================
        // 5. the fleet spreads over all its water
        // =============================================================================================

        /// <summary>
        /// <b>The spots spread over the accessible water.</b> The measure: cut the map into 40 m squares, weigh
        /// each by the accessible water in it (the guard's own flood, less the map edge and the features by
        /// the clearance), and take the share of that water whose square got at least one spot over the
        /// stated seeds and days. The bar is 90%.
        ///
        /// <para><b>Why 40 m and 90%.</b> The accessible water is about 231,000 m², so a whole square holds
        /// 0.7% of it, and the 3,584 spots planned here (8 seeds × 56 days × 4 boats × 2 spots) would put
        /// about 25 in it if they fell evenly. A whole square left empty is the planner avoiding water, not
        /// bad luck. Squares cut by the shore or a feature hold less water and weigh less, so the bar lets a
        /// few such slivers go empty and still fails any planner that leaves out a tenth of the bay. This
        /// planner scores 99.6%. The rectangle it replaced scored 3.5%.</para>
        /// </summary>
        [Test]
        public void Spots_SpreadOverTheAccessibleWater()
        {
            Measure.Sea sea = Measure.Flood(DepthAtSpringLow, Region, FloodCellMetres, Landfall, _def.MinDepthMeters);
            bool[] accessible = Measure.Accessible(sea, Inset(Inset(Region, MapEdgeBandMetres), Clearance),
                                                   SourceFeatures(), Clearance);
            var spots = new List<Vector2>();
            foreach (FleetDay d in _days)
            foreach (Vector2[] s in d.Spots)
                spots.AddRange(s);

            Measure.Coverage spread = Measure.Spread(sea, accessible, spots, SpreadSquareMetres);
            string map = HeatMap(spread);
            TestContext.WriteLine(map);
            Assert.GreaterOrEqual(spread.Share, SpreadBar,
                $"{spots.Count} spots cover {spread.Share:P1} of the accessible water ({spread.CellsWithSpots} of " +
                $"{spread.CellsWithWater} squares); the bar is {SpreadBar:P0}.\n{map}");
        }

        private static string HeatMap(Measure.Coverage spread)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"spots per {spread.CoarseMeters:F0} m square, north up (· = no accessible water): " +
                          $"{spread.Share:P1} of {spread.AccessibleArea:F0} m² covered, {spread.Outside} spots outside");
            int most = 0;
            foreach (int n in spread.Spots) most = Math.Max(most, n);
            int width = most.ToString(CultureInfo.InvariantCulture).Length + 1;   // a crowded square stays one column
            for (int r = spread.Rows - 1; r >= 0; r--)
            {
                for (int c = 0; c < spread.Columns; c++)
                {
                    int k = r * spread.Columns + c;
                    sb.Append((spread.Area[k] <= 0 ? "·" : spread.Spots[k].ToString(CultureInfo.InvariantCulture))
                              .PadLeft(Math.Max(4, width)));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // =============================================================================================
        // the features, read from their sources
        // =============================================================================================

        /// <summary>
        /// Every feature the fleet keeps clear of, read from its source: the arrival works, the sandbar and
        /// its gut, the passages, the marks (where the region's own mark pass puts them on this terrain), and
        /// #875's wake frame. Names match the asset's entries.
        /// </summary>
        private List<Feature> SourceFeatures()
        {
            var list = new List<Feature>();
            void Segment(string name, Vector2 a, Vector2 b, float halfWidth) => list.Add(Feature.Segment(name, a, b, halfWidth));
            void Point(string name, Vector3 p, float radius) => list.Add(Feature.Point(name, new Vector2(p.x, p.y), radius));
            void Box(string name, Rect r) => list.Add(Feature.Box(name, r));
            void Fairway(string name, NavChannel channel)
            {
                for (int i = 0; i + 1 < channel.Waypoints.Length; i++)
                    Segment($"{name} leg {i}", channel.Waypoints[i], channel.Waypoints[i + 1], channel.HalfWidthMetres);
            }

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
            Fairway("entrance fairway", StPetersNavMarks.Entrance);

            // The wharf deck: cells RootCellX..HeadCellX by MinCellY..MaxCellY.
            Box("wharf", Rect.MinMaxRect(StPetersWharf.RootCellX, StPetersWharf.MinCellY,
                                         StPetersWharf.HeadCellX + 1, StPetersWharf.MaxCellY + 1));

            // The sandbar, the gut cut through it, and the gut's buoyed fairway.
            Segment("sandbar", StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, StPetersBuilder.SandbarHalfWidth);
            Vector2 gut = StPetersNavMarks.GutCentre();
            Vector2 across = StPetersNavMarks.GutAxis();
            Segment("bar gut", gut - across * StPetersBuilder.SandbarHalfWidth,
                    gut + across * StPetersBuilder.SandbarHalfWidth, StPetersBuilder.ChannelHalfWidth);
            Fairway("bar gut fairway", StPetersNavMarks.BarGut);

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
            Assert.IsNotEmpty(marks.Marks, "the mark pass must place marks, or keeping clear of them proves nothing");
            foreach (PlannedNavMark m in marks.Marks)
                Point($"mark {m.Id}", m.At, 0f);

            Box("#875 wake frame", WakeFrame());
            return list;
        }

        private static Rect Band(Vector3 centre, Vector2 size) =>
            new Rect(new Vector2(centre.x, centre.y) - size * 0.5f, size);

        /// <summary>
        /// #875's wake frame, from its source. The gameplay acceptance puts the hull on the route's landfall
        /// (the intro hull is spawned there) and frames a run of fixed size on her with
        /// <c>FrameOn(boat.transform, new Bounds(boat.transform.position, new Vector3(w, h, 0f)))</c>. The
        /// fixture's <c>FrameOn</c> fits the run at the camera's aspect (half-height =
        /// max(extents.y, extents.x / aspect)), and the accepted plates are 2133 × 1600
        /// (docs/verification/wake-v24/README.md). If either file stops saying so, this fails: re-derive the
        /// frame and the asset's '#875 wake frame' entry.
        /// </summary>
        private static Rect WakeFrame()
        {
            string test = ReadProjectFile(WakeAcceptancePath);
            Assert.IsNotNull(test, $"{WakeAcceptancePath} is gone: find #875's frame again and re-derive its keep-clear box");
            StringAssert.IsMatch(@"boat\.transform\.position\s*=\s*opening\.Route\[0\]", test,
                $"{WakeAcceptancePath} no longer puts the hull on the route's landfall before framing her");
            Match run = Regex.Match(test,
                @"FrameOn\(\s*boat\.transform\s*,\s*new Bounds\(\s*boat\.transform\.position\s*,\s*new Vector3\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*,\s*0f\s*\)\s*\)\s*\)");
            Assert.IsTrue(run.Success, $"{WakeAcceptancePath} no longer frames a fixed run on the hull");

            string evidence = ReadProjectFile(WakeEvidencePath);
            Assert.IsNotNull(evidence, $"{WakeEvidencePath} is gone: find the plates' aspect again");
            Match plate = Regex.Match(evidence, @"original frames are (\d+)×(\d+)");
            Assert.IsTrue(plate.Success, $"{WakeEvidencePath} no longer states the plates' size");

            float w = float.Parse(run.Groups[1].Value, CultureInfo.InvariantCulture);
            float h = float.Parse(run.Groups[2].Value, CultureInfo.InvariantCulture);
            float aspect = int.Parse(plate.Groups[1].Value, CultureInfo.InvariantCulture) /
                           (float)int.Parse(plate.Groups[2].Value, CultureInfo.InvariantCulture);
            float halfHeight = Mathf.Max(h * 0.5f, w * 0.5f / aspect);
            Vector2 at = Landfall;
            return Rect.MinMaxRect(at.x - halfHeight * aspect, at.y - halfHeight,
                                   at.x + halfHeight * aspect, at.y + halfHeight);
        }

        private static string ReadProjectFile(string relative)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);
            return File.Exists(full) ? File.ReadAllText(full) : null;
        }
    }
}
