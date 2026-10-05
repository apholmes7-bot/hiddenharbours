using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>ST PETERS' WALLS STAND ON THE GROUND THE GAME HAS (terrain PR 5w, amendment 1 §4.6).</b> The walls are laid
    /// from their Defs (<see cref="CliffWallDef"/>, one per wall by real id), and the Defs were measured off the committed
    /// height map (#921's import of the ground file). This holds the three together: the Defs, the scene's walls and the map.
    ///
    /// <list type="bullet">
    /// <item><b>The ground, station by station:</b> a station whose height was read off the map sits on it within 2 cm; at
    /// a station that kept the scene's height, the map is within 10 cm of pass 9's there (the Art desk's kept rule). A kept
    /// station's stored height is not compared with the map: a face inside one texel is more than a raster holds.</item>
    /// <item><b>The faces:</b> no wall stands where its face is under the builder's minimum.</item>
    /// <item><b>The beach:</b> no wall stands where the retired walls stood (044–055, the main beach's opened span), but
    /// 043's return, as its Def records it.</item>
    /// <item><b>The ids:</b> 000 to the last with no gap, each live, retired or held; each live id one scene wall named
    /// by its Def; a retired or held id none.</item>
    /// <item><b>The round trip:</b> the walls the Defs make are the scene's, to the bit.</item>
    /// <item><b>Where walls may not stand:</b> the cannery's guard, the arrival route's capsule (but the kept 000–011,
    /// where they stood) and the neck's ramp.</item>
    /// <item><b>The rocks:</b> each scatter records its count, and none stands on the main beach's sand or where the
    /// retired walls stood.</item>
    /// </list>
    ///
    /// <para><b>It needs no faces.</b> Every claim reads the Defs, the scene's text and the map; no baked PNG is loaded,
    /// so it holds on a CI runner, which has none.</para>
    /// </summary>
    public class StPetersWallsAgreeWithGroundTests
    {
        /// <summary>A station whose height was read off the map sits on it within this (m; amendment 1 §4.6, Q1).</summary>
        const float MeasuredWithinMetres = 0.02f;

        /// <summary>At a station that kept the scene's height, the map is within this of pass 9's (m; the desk's kept rule).</summary>
        const float KeptWithinMetres = 0.10f;

        /// <summary>The walls by real id after Phase A, as the owner ruled the counts (10-04): 189 Defs, 109 live, 42 new
        /// (147 to 188, each cut from a wall), the main beach's 12 retired (044 to 055) and the Head's ring held (079 to
        /// 146) until its own PR (amendment 1 §4.1, §4.2, §4.4; amendment 2 §4.1).</summary>
        const int DefCount = 189, LiveCount = 109, FirstNew = 147;
        const int FirstRetired = 44, LastRetired = 55, FirstHeld = 79, LastHeld = 146;

        /// <summary>The kept walls that stand inside the arrival route's capsule, by id, where they stood (Q2).</summary>
        const int LastInTheRoute = 11;

        /// <summary>The main beach's return: the one wall that stands where the retired walls stood (Q12).</summary>
        const string Return = "043";

        const string MainBeach = "bay.stp_main_beach";
        const string RocksFolder = "Assets/_Project/Data/Terrain/StPetersRocks";

        List<CliffWallDef> _defs;
        List<CliffWallDef> _live;
        List<StPetersCliffWalls.SceneWall> _walls;
        PaintedHeightField _ground;
        RegionTerrainPlanDef _plan;

        [OneTimeSetUp]
        public void ReadTheDefsTheSceneAndTheMap()
        {
            _defs = StPetersCliffWalls.LoadDefs();
            _live = _defs.Where(d => d.IsLive).ToList();
            _walls = StPetersCliffWalls.WallsInScene(File.ReadAllText(StPetersLayerRefreshTests.ScenePath));
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(StPetersTerrainPlan.SeabedPath);
            Assert.IsNotNull(map, StPetersTerrainPlan.SeabedPath + " is missing: the walls are measured against the committed map");
            _ground = map.Field;
            _plan = StPetersTerrainPlan.LoadPlan();
            Assert.IsNotNull(_plan, "no St Peters terrain plan");
        }

        // =========================================================================================
        //  1. the ground, station by station
        // =========================================================================================

        /// <summary>⭐ A station whose brow or toe height was read off the map sits on the committed map within 2 cm. Its
        /// brow is its toe's height plus its drop; the map is read as the game reads it (<see cref="PaintedHeightField.ElevationAt"/>).</summary>
        [Test]
        public void AMeasuredStation_SitsOnTheCommittedMap_WithinTwoCentimetres()
        {
            int brows = 0, toes = 0;
            float worstBrow = 0f, worstToe = 0f;
            string worstBrowAt = "", worstToeAt = "";
            var over = new List<string>();
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    if (d.BrowFrom[i] == CliffStationSource.Measured)
                    {
                        brows++;
                        float off = Mathf.Abs(_ground.ElevationAt(d.Brow[i]) - (d.ToeElevations[i] + d.DropMetres[i]));
                        if (off > worstBrow) { worstBrow = off; worstBrowAt = At(d, i); }
                        if (off > MeasuredWithinMetres) over.Add($"{At(d, i)} brow {off:F4} m");
                    }
                    if (d.ToeFrom[i] == CliffStationSource.Measured)
                    {
                        toes++;
                        float off = Mathf.Abs(_ground.ElevationAt(d.Toe[i]) - d.ToeElevations[i]);
                        if (off > worstToe) { worstToe = off; worstToeAt = At(d, i); }
                        if (off > MeasuredWithinMetres) over.Add($"{At(d, i)} toe {off:F4} m");
                    }
                }

            Assert.Greater(brows + toes, 0, "no station's height was read off the map: this asserts nothing");
            Assert.IsEmpty(over, $"{over.Count} measured station ends sit off the committed map by more than " +
                                 $"{MeasuredWithinMetres} m: {string.Join("; ", over.Take(12))}");
            Debug.Log($"[walls-on-ground] measured ends on the committed map: {brows} brows, worst {worstBrow:F4} m at " +
                      $"{worstBrowAt}; {toes} toes, worst {worstToe:F4} m at {worstToeAt} (within {MeasuredWithinMetres} m)");
        }

        /// <summary>⭐ At a station that kept the scene's height, the committed map is within 10 cm of pass 9's height there,
        /// which the Def records as the measure read it: the ground under a kept wall did not move from under it.</summary>
        [Test]
        public void AtAKeptStation_TheCommittedMapIsWithinTenCentimetresOfPassNine()
        {
            int brows = 0, toes = 0;
            float worstBrow = 0f, worstToe = 0f;
            string worstBrowAt = "", worstToeAt = "";
            var over = new List<string>();
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    if (d.BrowFrom[i] == CliffStationSource.Kept)
                    {
                        brows++;
                        float off = Mathf.Abs(_ground.ElevationAt(d.Brow[i]) - d.BrowPass9[i]);
                        if (off > worstBrow) { worstBrow = off; worstBrowAt = At(d, i); }
                        if (off > KeptWithinMetres) over.Add($"{At(d, i)} brow {off:F4} m");
                    }
                    if (d.ToeFrom[i] == CliffStationSource.Kept)
                    {
                        toes++;
                        float off = Mathf.Abs(_ground.ElevationAt(d.Toe[i]) - d.ToePass9[i]);
                        if (off > worstToe) { worstToe = off; worstToeAt = At(d, i); }
                        if (off > KeptWithinMetres) over.Add($"{At(d, i)} toe {off:F4} m");
                    }
                }

            Assert.Greater(brows + toes, 0, "no station kept the scene's height: this asserts nothing");
            Assert.IsEmpty(over, $"{over.Count} kept station ends stand where the committed map is more than " +
                                 $"{KeptWithinMetres} m off pass 9's: {string.Join("; ", over.Take(12))}");
            Debug.Log($"[walls-on-ground] kept ends, the committed map against pass 9: {brows} brows, worst {worstBrow:F4} m " +
                      $"at {worstBrowAt}; {toes} toes, worst {worstToe:F4} m at {worstToeAt} (within {KeptWithinMetres} m)");
        }

        // =========================================================================================
        //  2. the faces and the beach
        // =========================================================================================

        /// <summary>No wall stands where its face is under the builder's minimum: every station drops at least
        /// <see cref="StPetersCliffWalls.MinFaceDropMetres"/> and is battered at least
        /// <see cref="StPetersCliffWalls.MinBatterDegrees"/>.</summary>
        [Test]
        public void NoWallStandsWhereItsFaceIsUnderTheBuildersMinimum()
        {
            float leastDrop = float.MaxValue, leastBatter = float.MaxValue;
            string dropAt = "", batterAt = "";
            var under = new List<string>();
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    var s = new CliffWallSample(d.Brow[i], d.Toe[i], d.DropMetres[i], d.ToeElevations[i]);
                    float batter = CliffWallGeometry.BatterDegrees(in s);
                    if (s.DropMetres < leastDrop) { leastDrop = s.DropMetres; dropAt = At(d, i); }
                    if (batter < leastBatter) { leastBatter = batter; batterAt = At(d, i); }
                    if (s.DropMetres < StPetersCliffWalls.MinFaceDropMetres - 1e-3f ||
                        batter < StPetersCliffWalls.MinBatterDegrees - 1e-3f)
                        under.Add($"{At(d, i)} ({s.DropMetres:F3} m, {batter:F2}°)");
                }

            Assert.IsEmpty(under, $"{under.Count} stations stand with a face under the builder's minimum " +
                                  $"({StPetersCliffWalls.MinFaceDropMetres} m, {StPetersCliffWalls.MinBatterDegrees}°): " +
                                  string.Join("; ", under.Take(12)));
            Debug.Log($"[walls-on-ground] least drop {leastDrop:F3} m at {dropAt}; least batter {leastBatter:F2}° at {batterAt}");
        }

        /// <summary>⭐ The main beach is open: no wall stands where the retired walls stood (their footprints, brow to toe,
        /// as their Defs keep them), but 043's return, as its Def records it.</summary>
        [Test]
        public void NoWallStandsInTheBeachsOpenedSpan_But043sReturn()
        {
            List<Vector2[]> span = OpenedSpan();
            Assert.AreEqual(LastRetired - FirstRetired + 1, span.Count, "the opened span is the retired walls' footprints");

            var inside = new List<string>();
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    if (span.Any(f => Inside(d.Brow[i], f))) inside.Add($"{At(d, i)} brow");
                    if (span.Any(f => Inside(d.Toe[i], f))) inside.Add($"{At(d, i)} toe");
                }

            var others = inside.Where(s => !s.StartsWith(Return + " ")).ToList();
            Assert.IsEmpty(others, $"walls stand where the beach was opened: {string.Join("; ", others)}");
            CliffWallDef ret = _live.Single(d => d.RealId == Return);
            Assert.AreEqual(CliffWallStatus.Return, ret.Status, "043 is the beach's return");
            Debug.Log($"[walls-on-ground] stations inside the opened span: {(inside.Count == 0 ? "none" : string.Join("; ", inside))} " +
                      $"(043's return, {ret.Brow.Length} stations, is allowed)");
        }

        // =========================================================================================
        //  3. the ids and the round trip
        // =========================================================================================

        /// <summary>⭐ The ids run from 000 to the last with no gap, each live, retired or held: the 189, the 109 live, the
        /// 42 new each cut from a wall, the retired 044–055 and the held 079–146. A retired id is never a live wall's run
        /// or source again.</summary>
        [Test]
        public void TheIdsRunWithNoGap_EachLiveRetiredOrHeld()
        {
            Assert.AreEqual(DefCount, _defs.Count, "the walls' Defs");
            for (int n = 0; n < _defs.Count; n++)
                Assert.AreEqual($"wall.stp_{n:D3}", _defs[n].Id, $"the ids skip or repeat at {n:D3}");

            Assert.AreEqual(LiveCount, _live.Count, "the live walls");
            var retired = new HashSet<string>();
            foreach (CliffWallDef d in _defs)
            {
                int n = int.Parse(d.RealId);
                bool shouldRetire = n >= FirstRetired && n <= LastRetired, shouldHold = n >= FirstHeld && n <= LastHeld;
                Assert.AreEqual(shouldRetire, d.Status == CliffWallStatus.Retired, $"{d.Id} is {d.Status}");
                Assert.AreEqual(shouldHold, d.Status == CliffWallStatus.Held, $"{d.Id} is {d.Status}");
                Assert.AreEqual(n >= FirstNew, !string.IsNullOrEmpty(d.SplitFrom),
                    $"{d.Id}: a new id is cut from a wall, and only a new id is ({d.SplitFrom})");
                if (d.Status == CliffWallStatus.Retired) retired.Add(d.Id);
            }
            foreach (CliffWallDef d in _live)
            {
                Assert.IsFalse(retired.Contains(d.Follows), $"{d.Id} follows {d.Follows}, which is retired");
                Assert.IsFalse(retired.Contains(d.SplitFrom), $"{d.Id} is cut from {d.SplitFrom}, which is retired");
            }
            Debug.Log($"[walls-on-ground] {_defs.Count} Defs: {_live.Count} live " +
                      $"({_defs.Count(d => d.Status == CliffWallStatus.Kept)} kept), {retired.Count} retired, " +
                      $"{_defs.Count(d => d.Status == CliffWallStatus.Held)} held, {_defs.Count(d => int.Parse(d.RealId) >= FirstNew)} new");
        }

        /// <summary>⭐ Each live id has one scene wall, named by its Def; a retired or held id has none.</summary>
        [Test]
        public void EachLiveIdHasOneSceneWall_NamedByItsDef_AndNoRetiredOrHeldIdHasOne()
        {
            Dictionary<string, StPetersCliffWalls.SceneWall> byId = _walls.ToDictionary(w => w.RealId);
            Assert.AreEqual(_live.Count, _walls.Count, "the scene's walls against the live Defs");
            foreach (CliffWallDef d in _defs)
            {
                bool stands = byId.TryGetValue(d.RealId, out StPetersCliffWalls.SceneWall w);
                Assert.AreEqual(d.IsLive, stands, $"{d.Id} is {d.Status}, and the scene {(stands ? "holds" : "has no")} wall {d.RealId}");
                if (stands) Assert.AreEqual(StPetersCliffWalls.WallName(d), w.Name, $"the scene's wall {d.RealId} is not named by its Def");
            }
        }

        /// <summary>⭐ The round trip: the walls the Defs make (<see cref="StPetersCliffWalls.ChunksOfDefs"/>, as the builder
        /// lays them) are the scene's walls, station for station and field for field, to the bit.</summary>
        [Test]
        public void TheDefsAndTheScenesWalls_AgreeToTheBit()
        {
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(_defs, out List<CliffWallDef> owners);
            Assert.AreEqual(_live.Count, chunks.Count, "one chunk per live Def");
            Dictionary<string, StPetersCliffWalls.SceneWall> byId = _walls.ToDictionary(w => w.RealId);
            var off = new List<string>();
            for (int k = 0; k < chunks.Count; k++)
            {
                CliffWallDef d = owners[k];
                StPetersCliffWalls.ChunkFields f = StPetersCliffWalls.FieldsOf(chunks[k]);
                Assert.IsTrue(byId.TryGetValue(d.RealId, out StPetersCliffWalls.SceneWall w), $"no scene wall for {d.Id}");
                if (!Same(f.BrowPlan, w.Brow)) off.Add($"{d.RealId} brow");
                if (!Same(f.ToePlan, w.Toe)) off.Add($"{d.RealId} toe");
                if (!Same(f.DropMetres, w.DropMetres)) off.Add($"{d.RealId} drop");
                if (!Same(f.ToeElevations, w.ToeElevations)) off.Add($"{d.RealId} toe heights");
                if (f.AlongOffsetMetres != w.AlongOffsetMetres) off.Add($"{d.RealId} along {f.AlongOffsetMetres:R} vs {w.AlongOffsetMetres:R}");
                if (f.RowsBasisSurfaceMetres != w.RowsBasisSurfaceMetres) off.Add($"{d.RealId} rows basis {f.RowsBasisSurfaceMetres:R} vs {w.RowsBasisSurfaceMetres:R}");
                if (f.WallAzimuth != w.WallAzimuth) off.Add($"{d.RealId} azimuth {f.WallAzimuth:R} vs {w.WallAzimuth:R}");
                if (f.Batter != w.Batter) off.Add($"{d.RealId} batter {f.Batter:R} vs {w.Batter:R}");
                if (chunks[k].RunIndex != w.RunIndex) off.Add($"{d.RealId} run {chunks[k].RunIndex} vs {w.RunIndex}");
            }
            Assert.IsEmpty(off, $"the Defs' walls are not the scene's: {string.Join("; ", off.Take(20))}");
        }

        // =========================================================================================
        //  4. where walls may not stand
        // =========================================================================================

        /// <summary>No wall stands within the cannery's guard: the neck's ramp keeps 21.06 m of the cannery's pivot clear
        /// (<see cref="GroundRampDef.Guard"/>), and so do the walls.</summary>
        [Test]
        public void NoWallStandsWithinTheCannerysGuard()
        {
            GroundRampDef ramp = TheNecksRamp();
            float nearest = float.MaxValue;
            string nearestAt = "";
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                    foreach (Vector2 p in new[] { d.Brow[i], d.Toe[i] })
                    {
                        float r = Vector2.Distance(p, ramp.Guard);
                        if (r < nearest) { nearest = r; nearestAt = At(d, i); }
                    }
            Assert.Greater(nearest, ramp.GuardRadius,
                $"a wall stands {nearest:F2} m from the cannery's pivot {ramp.Guard}, inside its {ramp.GuardRadius} m guard, at {nearestAt}");
            Debug.Log($"[walls-on-ground] the nearest wall to the cannery's pivot: {nearestAt}, {nearest:F2} m (guard {ramp.GuardRadius} m)");
        }

        /// <summary>⭐ Inside the arrival route's capsule (its half-width and the keep's margin) stand only the kept 000 to
        /// 011, where they stood: kept, every station's height the scene's own.</summary>
        [Test]
        public void InsideTheArrivalRoutesCapsule_StandOnlyTheKept000To011()
        {
            TerrainPlanSources src = StPetersTerrainPlan.LoadFrozen();
            Assert.Greater(src.Entrance.Length, 1, "the frozen sources hold no arrival route");
            float reach = (float)(src.EntranceHalfWidth + _plan.Keep.EntranceExtra);

            var inside = new List<string>();
            float least = float.MaxValue, most = 0f;
            foreach (CliffWallDef d in _live)
            {
                float near = float.MaxValue;
                for (int i = 0; i < d.Brow.Length; i++)
                    near = Mathf.Min(near, Mathf.Min(ToPolyline(d.Brow[i], src.Entrance), ToPolyline(d.Toe[i], src.Entrance)));
                if (!(near < reach)) continue;
                inside.Add(d.RealId);
                least = Mathf.Min(least, near);
                most = Mathf.Max(most, near);
                Assert.AreEqual(CliffWallStatus.Kept, d.Status, $"{d.Id} stands {near:F1} m from the route and is not a kept wall");
                Assert.IsTrue(d.BrowFrom.All(s => s == CliffStationSource.Kept) && d.ToeFrom.All(s => s == CliffStationSource.Kept),
                    $"{d.Id} stands in the route's capsule, but not where it stood");
            }
            var expected = Enumerable.Range(0, LastInTheRoute + 1).Select(n => n.ToString("D3")).ToList();
            CollectionAssert.AreEqual(expected, inside, $"inside the route's {reach} m capsule stand {string.Join(", ", inside)}");
            Debug.Log($"[walls-on-ground] inside the route's {reach} m capsule: {string.Join(", ", inside)}, {least:F1} to {most:F1} m");
        }

        /// <summary>No wall crosses the neck's ramp: the ground that climbs from its <see cref="GroundRampDef.From"/> to its
        /// <see cref="GroundRampDef.To"/> over its <see cref="GroundRampDef.Inland"/> metres up to the Head's root line
        /// (the line that closes the Head's brow, its last point to its first).</summary>
        [Test]
        public void NoWallCrossesTheNecksRamp()
        {
            GroundRampDef ramp = TheNecksRamp();
            Assert.IsNotNull(ramp.Form, $"{ramp.Id} names no form");
            Vector2[] brow = ramp.Form.Brow;
            Vector2 a = brow[brow.Length - 1], b = brow[0];
            Vector2 t = (b - a).normalized, n = new Vector2(-t.y, t.x);       // n: seaward of the root line
            Vector2[] quad = { a, b, b - n * ramp.Inland, a - n * ramp.Inland };

            var crossing = new List<string>();
            float nearest = float.MaxValue;
            string nearestId = "";
            foreach (CliffWallDef d in _live)
            {
                Vector2[] foot = Footprint(d.Brow, d.Toe);
                bool crosses = foot.Any(p => Inside(p, quad)) || quad.Any(p => Inside(p, foot)) || Crosses(foot, quad);
                if (crosses) crossing.Add(d.RealId);
                foreach (Vector2 p in foot)
                {
                    float r = Inside(p, quad) ? 0f : ToPolygonEdge(p, quad);
                    if (r < nearest) { nearest = r; nearestId = d.RealId; }
                }
            }
            Assert.IsEmpty(crossing, $"walls cross the neck's ramp: {string.Join(", ", crossing)}");
            Debug.Log($"[walls-on-ground] the nearest wall to the neck's ramp: {nearestId}, {nearest:F2} m");
        }

        // =========================================================================================
        //  5. the rocks
        // =========================================================================================

        /// <summary>Each scatter records its count, the rule's output (amendment 1 §4.7: a later test reads it from there),
        /// and no rock stands on the main beach's sand, rim to shore, or where the retired walls stood.</summary>
        [Test]
        public void EachScatterRecordsItsCount_AndNoRockStandsOnTheBeachOrInTheOpenedSpan()
        {
            BayDef beach = _plan.Bays.Single(b => b != null && b.Id == MainBeach);
            Vector2[] sand = beach.Rim.Concat(Enumerable.Reverse(beach.Shore)).ToArray();
            List<Vector2[]> span = OpenedSpan();

            var scatters = new List<RockScatterDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(RockScatterDef), new[] { RocksFolder }))
                scatters.Add(AssetDatabase.LoadAssetAtPath<RockScatterDef>(AssetDatabase.GUIDToAssetPath(guid)));
            Assert.Greater(scatters.Count, 0, "no rock scatter in " + RocksFolder);

            var counts = new List<string>();
            var wrong = new List<string>();
            int total = 0;
            foreach (RockScatterDef s in scatters.OrderBy(s => s.Id, System.StringComparer.Ordinal))
            {
                Assert.AreEqual(s.Count, s.Rocks.Length, $"{s.Id} records {s.Count} rocks and lays {s.Rocks.Length}");
                total += s.Count;
                counts.Add($"{s.Id} {s.Count}");
                for (int k = 0; k < s.Rocks.Length; k++)
                {
                    Vector2 p = s.Rocks[k].At;
                    if (Inside(p, sand)) wrong.Add($"{s.Id}#{k} at {p} on the main beach's sand");
                    if (span.Any(f => Inside(p, f))) wrong.Add($"{s.Id}#{k} at {p} in the opened span");
                }
            }
            Assert.IsEmpty(wrong, string.Join("; ", wrong));
            Debug.Log($"[walls-on-ground] {total} scatter rocks: {string.Join(", ", counts)}");
        }

        // =========================================================================================
        //  helpers
        // =========================================================================================

        static string At(CliffWallDef d, int i) => $"{d.RealId} k{d.Stations[i]}";

        GroundRampDef TheNecksRamp()
        {
            GroundRampDef ramp = _plan.Ramps.SingleOrDefault(r => r != null && r.GuardRadius > 0f);
            Assert.IsNotNull(ramp, "the plan has no ramp with a guard (the neck's)");
            return ramp;
        }

        /// <summary>Where the retired walls stood: each one's footprint, brow to toe, as its Def keeps it.</summary>
        List<Vector2[]> OpenedSpan() =>
            _defs.Where(d => d.Status == CliffWallStatus.Retired).Select(d => Footprint(d.Brow, d.Toe)).ToList();

        /// <summary>A wall's footprint: its brow, then its toe back.</summary>
        static Vector2[] Footprint(Vector2[] brow, Vector2[] toe) => brow.Concat(Enumerable.Reverse(toe)).ToArray();

        /// <summary>Even-odd: inside the closed polygon.</summary>
        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Any edge of one closed polygon crosses any edge of the other.</summary>
        static bool Crosses(Vector2[] a, Vector2[] b)
        {
            for (int i = 0, j = a.Length - 1; i < a.Length; j = i++)
                for (int k = 0, l = b.Length - 1; k < b.Length; l = k++)
                    if (SegmentsCross(a[j], a[i], b[l], b[k])) return true;
            return false;
        }

        static bool SegmentsCross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
        {
            float d1 = Cross(q2 - q, p - q), d2 = Cross(q2 - q, p2 - q), d3 = Cross(p2 - p, q - p), d4 = Cross(p2 - p, q2 - p);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 v = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, v) / Vector2.Dot(v, v));
            return Vector2.Distance(p, a + v * t);
        }

        static float ToPolygonEdge(Vector2 p, Vector2[] poly)
        {
            float d = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) d = Mathf.Min(d, ToSegment(p, poly[j], poly[i]));
            return d;
        }

        static float ToPolyline(Vector2 p, PlanPoint[] line)
        {
            float d = float.MaxValue;
            for (int j = 0; j + 1 < line.Length; j++)
                d = Mathf.Min(d, ToSegment(p, new Vector2((float)line[j].X, (float)line[j].Y),
                                              new Vector2((float)line[j + 1].X, (float)line[j + 1].Y)));
            return d;
        }

        static bool Same(Vector2[] a, Vector2[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i].x != b[i].x || a[i].y != b[i].y) return false;
            return true;
        }

        static bool Same(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
