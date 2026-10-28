using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;
using G = HiddenHarbours.App.Editor.NineMileCreekGroundPlan;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE ROADS NINE MILE CREEK SHIPS</b> (NMC H2, PR 2): the activated scene's two road layers, Route 91 and its
    /// river bridge, and Wharf Road's final ramp and turning pad, held to G3. (The eight cliff faces that stood down for
    /// the ramp are retired walls since NMC H3's step 11: <c>NineMileCreekWallsAgreeWithGroundTests</c> holds the opening.)
    /// Every fixture reads NineMileCreek.unity as N1 writes it (<see cref="NineMileCreekLiveGround.Scene"/>) and
    /// probes G3 through PR 1's seam (<see cref="NineMileCreekLiveGround.Plan"/>), so what passes is what ships.
    ///
    /// <para><b>Whose numbers.</b> The thresholds are the scope's (PR 2 rows 7 to 9), measured on G3 by Phase A's road
    /// sweep before the scene moved (<see cref="Sweep"/>); each test prints what it measured. The pure paving on G3 is
    /// <c>NineMileCreekRoadsTests</c>' (it calls the painter's own <see cref="NineMileCreekGroundPlan.Pave"/>), the
    /// trips on it are <c>NineMileCreekTripsTests</c>' and <c>NineMileCreekRoutesTests</c>'; this class holds the
    /// scene to that paving and to the ground.</para>
    ///
    /// <para><b>NMC H4.</b> Step 06 keeps only <see cref="NineMileCreekGroundPlan.PaveKeptV3"/> in kit v3, the v4 road
    /// floor paints the rest of the network, and step 12 retires the v3 bridge for the network's three decks under
    /// <see cref="NineMileCreekGroundPlan.BridgesRootName"/>. "Painted" is read at a point as the shader reads the
    /// floor (<see cref="Painted"/>); the decks are read from the scene (<see cref="Decks"/>).</para>
    /// </summary>
    public class NineMileCreekSceneRoutesTests
    {
        static float High => NineMileCreekMainland.SpringHighWater;
        static float Ground(Vector2 at) => Plan.ElevationAt(at);
        static string Roads(string child) => NineMileCreekRoadPainter.RootName + "/" + child;
        static string At2(Vector2 v) => $"({Shown(v.x)}, {Shown(v.y)})";

        static NineMileCreekRoads.Way Way(string name) => G.Ways().Single(w => w.Name == name);
        static Doc Layer(string name) => Components(At(Roads(name)), TilemapClass).Single();

        /// <summary>
        /// Phase A's road sweep, as PR 2's scope measured rows 7 and 8: along each segment of a route,
        /// <c>ceil(L / 0.25)</c> steps; at each station the centre and both edges at the way's half width. The centre
        /// grade is taken between successive stations of one segment (<paramref name="step"/> apart).
        /// </summary>
        static IEnumerable<(Vector2 centre, Vector2[] across, bool first, float step)> Sweep(Vector2[] route, float halfWidth)
        {
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = route[i - 1], b = route[i];
                float length = Vector2.Distance(a, b);
                int n = Mathf.CeilToInt(length / 0.25f);
                var normal = new Vector2(a.y - b.y, b.x - a.x) / length;
                for (int j = 0; j <= n; j++)
                {
                    Vector2 p = Vector2.Lerp(a, b, j / (float)n);
                    yield return (p, new[] { p - normal * halfWidth, p, p + normal * halfWidth }, j == 0, length / n);
                }
            }
        }

        /// <summary>The steepest rise over any <paramref name="baseline"/> metres of a route's centre line, by arc
        /// length on quarter-metre stations, skipping any stretch that touches <paramref name="skip"/>.</summary>
        static (float grade, Vector2 at) SteepestOver(Vector2[] route, float baseline, Rect skip)
        {
            var stations = new List<(float s, Vector2 p)>();
            float run = 0f;
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = route[i - 1], b = route[i];
                float length = Vector2.Distance(a, b);
                int n = Mathf.CeilToInt(length / 0.25f);
                for (int j = i == 1 ? 0 : 1; j <= n; j++) stations.Add((run + length * j / n, Vector2.Lerp(a, b, j / (float)n)));
                run += length;
            }
            float[] z = stations.Select(t => Ground(t.p)).ToArray();
            (float grade, Vector2 at) worst = (0f, stations[0].p);
            for (int i = 0, k = 0; i < stations.Count; i++)
            {
                while (k < stations.Count && stations[k].s - stations[i].s < baseline - 1e-3f) k++;
                if (k >= stations.Count) break;
                bool touches = false;
                for (int m = i; m <= k && !touches; m++) touches = skip.Contains(stations[m].p);
                if (touches) continue;
                float grade = Mathf.Abs(z[k] - z[i]) / (stations[k].s - stations[i].s);
                if (grade > worst.grade) worst = (grade, stations[i].p);
            }
            return worst;
        }

        /// <summary>How near any solid outline the scene owns comes to a route's centre line (its edge or circle radius
        /// taken off), on quarter-metre stations, and the solid that does.</summary>
        static (float clearance, string who) NearestSolid(Vector2[] route,
                                                          List<(Vector2 a, Vector2 b, float radius, string who)> outlines)
        {
            (float clearance, string who) nearest = (float.MaxValue, "none");
            foreach (var station in Sweep(route, 0f))
                foreach (var o in outlines)
                {
                    float d = DistanceToSegment(station.centre, o.a, o.b) - o.radius;
                    if (d < nearest.clearance) nearest = (d, o.who);
                }
            return nearest;
        }

        // ---- NMC H4: the v4 floor and the network's decks, as the scene streams and stands them --------------------

        /// <summary>The road floor as the scene streams it: the one <see cref="RoadFloorStreamer"/>'s folder under
        /// StreamingAssets, its manifest (refused with any problem, as the streamer refuses it), and each chunk's light
        /// map decoded by the streamer's own reader, kept as its roles (the map's G), rows bottom up as decoded.</summary>
        sealed class Floor
        {
            public RoadFloorManifest Manifest;
            public readonly Dictionary<int, byte[]> Roles = new Dictionary<int, byte[]>();

            /// <summary>The role of the texel under <paramref name="p"/>, as RoadFloor4.hlsl finds it (RF4Pixel, RF4Slot,
            /// RF4Texel): a texel off the grid, or in a chunk the bake did not write, is not the road.</summary>
            public int RoleAt(Vector2 p)
            {
                const int T = RoadFloorMaps.ChunkTexels;
                int gx = Mathf.FloorToInt((p.x - Manifest.origin.x) * RoadFloorMaps.PixelsPerMetre);
                int gy = Mathf.FloorToInt((Manifest.origin.y - p.y) * RoadFloorMaps.PixelsPerMetre);
                int cx = gx >> 8, cy = gy >> 8;   // floor(G / 256), as the shader takes it, negative G included
                if (cx < 0 || cy < 0 || cx >= Manifest.grid.w || cy >= Manifest.grid.h) return RoadFloorMaps.RoleNone;
                if (!Roles.TryGetValue(Manifest.GridIndex(cx, cy), out byte[] g)) return RoadFloorMaps.RoleNone;
                return g[(T - 1 - (gy & (T - 1))) * T + (gx & (T - 1))];
            }
        }

        static Floor _floor;
        static Floor TheFloor => _floor ??= ReadFloor();

        static Floor ReadFloor()
        {
            Doc streamer = Scene.Docs.Single(d => !d.Stripped && d.IsScript("HiddenHarbours.Art.RoadFloorStreamer"));
            Assert.That(Enabled(streamer), Is.True, "the road floor's streamer is disabled");
            string root = Path.Combine(Application.streamingAssetsPath, streamer.Field("_folder"));
            var floor = new Floor
            {
                Manifest = RoadFloorManifest.Parse(File.ReadAllText(Path.Combine(root, RoadFloorStreamer.ManifestFile))),
            };
            Assert.That(floor.Manifest.Problems(), Is.Empty, "the streamer refuses the scene's road floor");
            const int LightMap = 1;   // RoadFloorMaps.MapSuffixes: "_light", the role in G
            byte[] rgba = new byte[RoadFloorMaps.MapBytes], scratch = new byte[RoadFloorPng.ScratchBytes];
            foreach (RoadFloorManifest.Chunk c in floor.Manifest.chunks)
            {
                string file = RoadFloorMaps.MapFile(c.name, LightMap);
                Assert.That(RoadFloorPng.Decode(File.ReadAllBytes(Path.Combine(root, file)), rgba, scratch), Is.Null,
                            $"the streamer refuses {file}");
                var g = new byte[RoadFloorMaps.ChunkTexels * RoadFloorMaps.ChunkTexels];
                for (int i = 0; i < g.Length; i++) g[i] = rgba[i * 4 + 1];
                floor.Roles[floor.Manifest.GridIndex(c.cx, c.cy)] = g;
            }
            return floor;
        }

        /// <summary>A deck as the scene stands it: an instance of a bridge prefab under
        /// <see cref="NineMileCreekGroundPlan.BridgesRootName"/>, its StandablePlatform read from the prefab with the
        /// instance's overrides on it.</summary>
        sealed class Deck
        {
            public string Id;
            public Rect Footprint;
            public float Elevation;
            public bool Live, Enabled;
            public Vector2 At;
        }

        static List<Deck> _decks;
        static List<Deck> Decks => _decks ??= ReadDecks();

        static List<Deck> ReadDecks()
        {
            Doc root = At(G.BridgesRootName);
            var decks = new List<Deck>();
            foreach (long child in Scene.TransformOf(root).FieldRefs("m_Children"))
            {
                Doc instance = Scene.Require(Scene.Require(child, "a bridge's transform").FieldRef("m_PrefabInstance"),
                                             "a bridge's prefab instance");
                Dictionary<(long, string), string> overrides = Overrides(instance);
                string Over(Doc doc, string key) => overrides.TryGetValue((doc.FileId, key), out string v) ? v : doc.Field(key);
                string guid = RefGuid(instance.Field("m_SourcePrefab"));
                string path = Directory.EnumerateFiles(G.BridgePrefabFolder, "*.prefab").Single(p => GuidOf(p) == guid);
                SceneYaml prefab = SceneYaml.Parse(File.ReadAllText(path).Replace("\r\n", "\n"));
                Doc go = prefab.Docs.Single(d => d.ClassId == 1);
                Doc tr = prefab.TransformOf(go);
                Doc platform = prefab.ComponentsOf(go).Single(d => d.IsScript("HiddenHarbours.World.StandablePlatform"));
                Rect footprint = RectOf(platform, "_footprint");
                Vector3 local = ParseVec3(tr.Field("m_LocalPosition"));
                float Part(Doc doc, string key, float own) => overrides.TryGetValue((doc.FileId, key), out string v) ? ParseFloat(v) : own;
                float Place(string axis, float own) => Part(tr, "m_LocalPosition." + axis, own);
                decks.Add(new Deck
                {
                    Id = Over(platform, "_id"),
                    Footprint = new Rect(Part(platform, "_footprint.x", footprint.x), Part(platform, "_footprint.y", footprint.y),
                                         Part(platform, "_footprint.width", footprint.width),
                                         Part(platform, "_footprint.height", footprint.height)),
                    Elevation = ParseFloat(Over(platform, "_deckElevation")),
                    Live = Live(root) && Over(go, "m_IsActive") == "1",
                    Enabled = Over(platform, "m_Enabled") == "1",
                    At = WorldPosition(root) + new Vector2(Place("x", local.x), Place("y", local.y)),
                });
            }
            return decks;
        }

        static bool OnADeck(Vector2 p) => Decks.Any(d => d.Footprint.Contains(p));

        /// <summary>NMC H4's painted road at a point, read as the shader reads the floor (the seat's ruling of
        /// 10-28): the kept v3 layer draws the point's cell (case 2 (a) holds it to PaveKeptV3), or the floor's texel
        /// under the point is the road (any role but none: a floor, a face, a face under a deck), or the point stands on
        /// one of the network's decks. A cell is read at its centre, as <see cref="NineMileCreekRoads.Pave"/> reads the
        /// deck and the bed; a station at its own point.</summary>
        static bool Painted(Vector2 p, HashSet<Vector2Int> top) =>
            top.Contains(NineMileCreekRoads.CellOf(p)) || TheFloor.RoleAt(p) != RoadFloorMaps.RoleNone || OnADeck(p);

        /// <summary>Stations <paramref name="apart"/> metres apart by arc length along a line from its first point,
        /// and its last point.</summary>
        static IEnumerable<Vector2> Stations(Vector2[] line, float apart)
        {
            float run = 0f, next = 0f;
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                float length = Vector2.Distance(a, b);
                if (length <= 0f) continue;
                for (; next <= run + length; next += apart) yield return Vector2.Lerp(a, b, (next - run) / length);
                run += length;
            }
            yield return line[line.Length - 1];
        }

        // ---- row 7: the two layers are G3's paving -------------------------------------------------------------

        /// <summary>Row 7's kept ids: the roads root and both layers' GameObject, Transform, renderer and Tilemap.</summary>
        static readonly (long id, int classId, string go)[] RoadIds =
        {
            (1447758930, 1, NineMileCreekRoadPainter.RootName), (1447758932, TransformClass, NineMileCreekRoadPainter.RootName),
            (879863773, 1, RoadKitV3.TopLayerName), (879863774, TransformClass, RoadKitV3.TopLayerName),
            (879863775, 483693784, RoadKitV3.TopLayerName), (879863776, TilemapClass, RoadKitV3.TopLayerName),
            (1501878149, 1, RoadKitV3.SkirtLayerName), (1501878150, TransformClass, RoadKitV3.SkirtLayerName),
            (1501878151, 483693784, RoadKitV3.SkirtLayerName), (1501878152, TilemapClass, RoadKitV3.SkirtLayerName),
        };

        [Test]
        public void TheTwoRoadLayersAreG3sPaving_CellForCellAndTileForTile()
        {
            foreach (var (id, classId, go) in RoadIds)
            {
                Doc doc = Scene.Get(id);
                Assert.That(doc, Is.Not.Null, $"&{id} ({go}) is gone: row 7 keeps it");
                Assert.That(doc.ClassId, Is.EqualTo(classId), $"&{id} changed class");
                Doc owner = classId == 1 ? doc : Scene.Require(doc.FieldRef("m_GameObject"), "a GameObject");
                Assert.That(Name(owner), Is.EqualTo(go), $"&{id} names another object");
                Assert.That(Live(owner), Is.True, $"{go} does not draw");
            }
            Assert.That(At(Roads(RoadKitV3.TopLayerName)).FileId, Is.EqualTo(879863773));
            Assert.That(At(Roads(RoadKitV3.SkirtLayerName)).FileId, Is.EqualTo(1501878149));

            // (a) The two layers are the paving step 06 keeps in kit v3, cell for cell and tile for tile.
            NineMileCreekRoads.Paving paving = G.Pave(Plan), kept = G.PaveKeptV3(Plan);
            var guids = new Dictionary<string, string>();
            var problems = new List<string>();
            foreach (string layer in new[] { RoadKitV3.TopLayerName, RoadKitV3.SkirtLayerName })
            {
                Doc map = Layer(layer);
                Assert.That(Enabled(map), Is.True, $"{layer}'s Tilemap is disabled");
                Dictionary<Vector2Int, string> tiles = Tiles(map);
                int missing = 0, extra = 0, wrong = 0;
                string first = null;
                foreach (NineMileCreekRoads.PavedCell cell in kept.Cells)
                {
                    int index = NineMileCreekRoads.AtlasIndexAt(kept, cell.Cell.x, cell.Cell.y);
                    string name = layer == RoadKitV3.TopLayerName
                        ? RoadTileLibrary.TopTileName(cell.Surface, index)
                        : RoadTileLibrary.SkirtTileName(cell.Surface, index);
                    if (!guids.TryGetValue(name, out string guid)) guids[name] = guid = GuidOf(RoadTileLibrary.TilePath(name));
                    if (!tiles.TryGetValue(cell.Cell, out string drawn)) { missing++; first ??= $"{cell.Cell} unpainted"; }
                    else if (drawn != guid) { wrong++; first ??= $"{cell.Cell} drawn {drawn}, the kept paving lays {name}"; }
                }
                foreach (Vector2Int cell in tiles.Keys)
                    if (!kept.Paved.Contains(cell)) { extra++; first ??= $"{cell} painted, the kept paving lays nothing"; }
                TestContext.WriteLine($"{layer}: {tiles.Count} cells; the kept paving {kept.Cells.Count} ({kept.Trimmed} trimmed by the " +
                                      $"water); missing {missing}, extra {extra}, wrong tile {wrong}");
                if (missing + extra + wrong > 0)
                    problems.Add($"{layer}: {missing} missing, {extra} extra, {wrong} wrong tile; first {first}");
            }
            Assert.That(problems, Is.Empty, "the scene's roads are not the kept paving: " + string.Join("; ", problems));

            // (b) RETIRED BY NAME (NMC H4): "the road layer is the paving the code derives" has no v4 twin in C#. The
            // floor's paint is derived by the network's painter alone (RoadKit4.paint, run in Node by
            // tools/nmc-roads/bake-chunks.cjs over the network file). Its holder is
            // RoadFloorManifestTests.TheCommittedBake_ParsesWithoutProblems_AndIsTheNetworksBake: the committed bake is
            // the committed network's by sha256, and a manifest with problems is refused.

            // (c) Every cell of G3's paving the kept v3 no longer draws is painted, but the two ways v4 retires, chosen
            // by name: the Bar Road's legacy leg (the network re-lines it) and the Parish Hall's walk (v3's derived
            // TownWalks, which the network does not carry).
            HashSet<Vector2Int> top = Cells(Layer(RoadKitV3.TopLayerName));
            string parishWalk = NineMileCreekRoads.TownWalkNamePrefix + NineMileCreekRoads.PublicTownLotNames[
                System.Array.IndexOf(NineMileCreekRoads.PublicTownLots, (Vector2)NineMileCreekMainland.ParishHallPos)];
            int bar = 0, parish = 0, others = 0;
            string firstOther = null;
            foreach (NineMileCreekRoads.PavedCell cell in paving.Cells)
            {
                if (kept.Paved.Contains(cell.Cell) || Painted(cell.Centre, top)) continue;
                if (cell.Way == NineMileCreekRoads.BarRoadName) bar++;
                else if (cell.Way == parishWalk) parish++;
                else { others++; firstOther ??= $"{cell.Cell} ({cell.Way})"; }
            }
            TestContext.WriteLine($"G3's paving {paving.Cells.Count}, the kept {kept.Cells.Count}; of the rest not painted: " +
                                  $"{NineMileCreekRoads.BarRoadName} {bar}, {parishWalk} {parish}, any other way {others}");
            Assert.That(others, Is.Zero, $"G3's paving the v4 roads leave unpainted, first {firstOther}");

            // ...and the Bar Road's own line in the network is painted end to end: where it runs on v3's legacy leg,
            // the name alone would not see a gap.
            NineMileCreekRoadNetwork.Route barRoad = NineMileCreekRoadNetwork.Load().Routes.Single(r => r.Id == BarRoadId);
            int stations = 0, unpainted = 0;
            string firstGap = null;
            foreach (Vector2 p in Stations(barRoad.Points, 1f))
            {
                stations++;
                if (!Painted(p, top)) { unpainted++; firstGap ??= At2(p); }
            }
            TestContext.WriteLine($"{BarRoadId}: {stations} stations a metre apart, {unpainted} unpainted");
            Assert.That(unpainted, Is.Zero, $"{BarRoadId} runs off the painted road at {firstGap}");
        }

        /// <summary>The network's Bar Road (its committed file's id).</summary>
        const string BarRoadId = "road.nmc_bar_road";

        /// <summary>NMC H4's one wet finding, PINNED BY NAME (a finding for G3's reissue 3): six cells of
        /// path.nmc_shore_path whose beds, read at their centres, the spring tide covers. A seventh wet cell is red, and
        /// so is one of the six reading dry. Texel by texel the same finding is 4,704 texels in 19 cells of the path,
        /// x 35..36, y -44..-31: its full width for the reissue, not a second pin.</summary>
        static readonly (Vector2Int cell, float bed)[] ShorePathWet =
        {
            (new Vector2Int(35, -40), 2.118f), (new Vector2Int(35, -39), 1.988f), (new Vector2Int(35, -38), 1.882f),
            (new Vector2Int(35, -37), 1.741f), (new Vector2Int(35, -36), 1.623f), (new Vector2Int(35, -35), 1.506f),
        };

        [Test]
        public void EveryCellTheRoadsDraw_StandsOnDryGroundOrTheBridgesDeck()
        {
            HashSet<Vector2Int> top = Cells(Layer(RoadKitV3.TopLayerName));
            RoadFloorManifest m = TheFloor.Manifest;
            float xMin = Decks.Select(d => d.Footprint.xMin).Append(m.origin.x).Min();
            float xMax = Decks.Select(d => d.Footprint.xMax).Append(m.origin.x + m.grid.w * RoadFloorMaps.ChunkMetres).Max();
            float yMin = Decks.Select(d => d.Footprint.yMin).Append(m.origin.y - m.grid.h * RoadFloorMaps.ChunkMetres).Min();
            float yMax = Decks.Select(d => d.Footprint.yMax).Append(m.origin.y).Max();
            var painted = new HashSet<Vector2Int>(top);
            for (int x = Mathf.FloorToInt(xMin); x < Mathf.CeilToInt(xMax); x++)
                for (int y = Mathf.FloorToInt(yMin); y < Mathf.CeilToInt(yMax); y++)
                    if (Painted(new Vector2(x + 0.5f, y + 0.5f), top)) painted.Add(new Vector2Int(x, y));

            int onDeck = 0;
            var wet = new Dictionary<Vector2Int, float>();
            foreach (Vector2Int cell in painted)
            {
                var centre = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                if (OnADeck(centre)) { onDeck++; continue; }
                float bed = Ground(centre);
                if (bed > High) continue;
                wet[cell] = bed;
            }
            TestContext.WriteLine($"{painted.Count} cells painted; {onDeck} on the network's decks; {wet.Count} off them at or below " +
                                  $"spring high {Shown(High)}: " + string.Join(", ", wet.OrderBy(w => w.Key.y).ThenBy(w => w.Key.x)
                                      .Select(w => $"{w.Key} at {w.Value:0.###}")));
            string[] unpinned = wet.Keys.Where(c => !ShorePathWet.Any(w => w.cell == c)).Select(c => $"{c} at {Shown(wet[c])}").ToArray();
            string[] dry = ShorePathWet.Where(w => !wet.ContainsKey(w.cell)).Select(w => $"{w.cell} (pinned at {Shown(w.bed)})").ToArray();
            Assert.That(unpinned, Is.Empty, "road cells the spring tide covers, beyond the six pinned on the shore path");
            Assert.That(dry, Is.Empty, "a pinned wet shore-path cell reads dry: the finding moved, re-pin it");
            Assert.That(onDeck, Is.GreaterThan(0), "no road crosses the bridge");
        }

        // ---- row 7: Route 91 and its bridge -----------------------------------------------------------------------

        /// <summary>The scope's margin: off the bridge, Route 91's final line stands at least this far above spring high
        /// across its full width (PR 2 row 7).</summary>
        const float Route91MarginAboveHigh = 3.272046f;

        [Test]
        public void Route91RunsItsG3LineEndToEnd_DryOffTheBridge_ClearOfEverySolid()
        {
            NineMileCreekRoads.Way way = Way(NineMileCreekRoads.ThroughRoadName);
            Assert.That(way.Route, Is.EqualTo(G.Route91), "the through-road is not Route 91's final line");
            Rect region = NineMileCreekRoads.RegionRect();
            Assert.That(G.Route91[0].y, Is.EqualTo(region.yMax), "Route 91 no longer leaves by the north edge");
            Assert.That(G.Route91[G.Route91.Length - 1].x, Is.EqualTo(region.xMin), "Route 91 no longer reaches the west edge");

            HashSet<Vector2Int> top = Cells(Layer(RoadKitV3.TopLayerName));
            var outlines = SolidOutlines(out var polygons);
            float lowest = float.MaxValue;
            Vector2 lowestAt = default;
            int inSolid = 0;
            string firstSolid = null, firstGap = null;
            foreach (var station in Sweep(G.Route91, way.HalfWidth))
            {
                if (!Painted(station.centre, top)) firstGap ??= At2(station.centre);
                foreach (Vector2 q in station.across)
                {
                    string solid = SolidAt(q, outlines, polygons);
                    if (solid != null) { inSolid++; firstSolid ??= $"{solid} at {At2(q)}"; }
                    if (G.BridgeDeck.Contains(q)) continue;
                    float bed = Ground(q);
                    if (bed < lowest) { lowest = bed; lowestAt = q; }
                }
            }
            var nearest = NearestSolid(G.Route91, outlines);
            TestContext.WriteLine($"Route 91, {way.Surface}, half width {Shown(way.HalfWidth)}: off the deck lowest {lowest:0.######} at " +
                                  $"{At2(lowestAt)} ({lowest - High:0.######} above spring high); nearest solid {nearest.who} " +
                                  $"{nearest.clearance:0.###} m from the centre line");
            Assert.That(firstGap, Is.Null, $"Route 91's centre line runs off the painted road at {firstGap}");
            Assert.That(lowest - High, Is.GreaterThanOrEqualTo(Route91MarginAboveHigh), $"Route 91 runs low at {At2(lowestAt)}");
            Assert.That(inSolid, Is.Zero, $"Route 91's carriageway runs through {firstSolid}");
            Assert.That(nearest.clearance, Is.GreaterThanOrEqualTo(way.HalfWidth + 0.125f), $"{nearest.who} stands on Route 91");

            // No Rice Point door: neither end of the line is a way out of the region.
            foreach (Doc passage in Scene.Docs.Where(d => !d.Stripped && d.IsScript("HiddenHarbours.World.RegionPassage")))
            {
                Doc go = Scene.Require(passage.FieldRef("m_GameObject"), "a GameObject");
                Vector2 at = WorldPosition(go);
                foreach (Vector2 end in new[] { G.Route91[0], G.Route91[G.Route91.Length - 1] })
                    Assert.That(Vector2.Distance(at, end), Is.GreaterThan(30f), $"{Name(go)} is a door at Route 91's end {At2(end)}");
            }

            // For the neighbours (the West Water's seam): Route 91's heights over its last 40 m to the west edge.
            var tail = Sweep(G.Route91, way.HalfWidth).Where(s => s.centre.x <= -340f).ToList();
            TestContext.WriteLine("Route 91 west of x -340: " + string.Join("; ", tail.Where((s, i) => i % 20 == 0 || i == tail.Count - 1)
                .Select(s => $"{At2(s.centre)} centre {Shown(Ground(s.centre))}, edges {Shown(Ground(s.across[0]))}/{Shown(Ground(s.across[2]))}")));
        }

        /// <summary>The bridge lands on its banks: at each end of the deck, and half a metre past it, G3 across the
        /// deck's whole width is within this of the deck. The scope's fix carried the 36 m span a metre onto the
        /// stable bank each way because the span's own south end stood 3.3 m over the ground (row 7).</summary>
        const float BankStepMetres = 0.11f;

        [Test]
        public void TheBridgeCarriesRoute91OverTheRiver_LandingOnBothBanks()
        {
            // NMC H4: the network's Route 91 deck, as the scene stands it under NineMileCreekBridges (step 12 retired
            // the v3 bridge under the roads root).
            NineMileCreekRoadNetwork.Bridge network = NineMileCreekRoadNetwork.Load().Bridges.Single(b => b.Id == G.BridgeId);
            Rect deck = network.Footprint;
            float elevation = network.Deck;
            Deck bridge = Decks.Single(d => d.Id == G.BridgeId);
            Assert.That(bridge.Live && bridge.Enabled, Is.True, "the bridge does not stand");
            Rect footprint = bridge.Footprint;
            Assert.That(Vector2.Distance(footprint.min, deck.min), Is.LessThanOrEqualTo(1e-4f), "the deck's footprint moved");
            Assert.That(Vector2.Distance(footprint.max, deck.max), Is.LessThanOrEqualTo(1e-4f), "the deck's footprint moved");
            Assert.That(bridge.Elevation, Is.EqualTo(elevation).Within(1e-5f));
            Assert.That(Vector2.Distance(bridge.At, deck.center), Is.LessThanOrEqualTo(1e-4f), "the bridge stands off its deck");

            float lowest = float.MaxValue;
            for (float x = deck.xMin; x <= deck.xMax; x += 0.25f)
                for (float y = deck.yMin; y <= deck.yMax; y += 0.25f)
                    lowest = Mathf.Min(lowest, Ground(new Vector2(x, y)));
            TestContext.WriteLine($"under the deck G3 falls to {lowest:0.######}; the deck stands at {Shown(elevation)}");
            Assert.That(lowest, Is.LessThan(High), "no river under the bridge: the deck carries the road over nothing");
            Assert.That(elevation, Is.GreaterThan(High), "the spring tide covers the deck");

            foreach (float end in new[] { deck.yMin, deck.yMax })
                foreach (float past in new[] { 0f, 0.5f })
                {
                    float y = end == deck.yMin ? end - past : end + past, worst = 0f;
                    for (int k = 0; k <= 190; k++)
                    {
                        float x = Mathf.Min(deck.xMin + k * 0.05f, deck.xMax);
                        worst = Mathf.Max(worst, Mathf.Abs(Ground(new Vector2(x, y)) - elevation));
                    }
                    TestContext.WriteLine($"bank at y {Shown(y)}: G3 within {worst:0.######} of the deck across its width");
                    Assert.That(worst, Is.LessThanOrEqualTo(BankStepMetres), $"the deck at y {Shown(y)} does not land on its bank");
                }

            // Every point of Route 91's carriageway the spring tide would take is on the deck.
            NineMileCreekRoads.Way way = Way(NineMileCreekRoads.ThroughRoadName);
            int carried = 0;
            foreach (var station in Sweep(G.Route91, way.HalfWidth))
                foreach (Vector2 q in station.across)
                {
                    if (Ground(q) > High) continue;
                    Assert.That(deck.Contains(q), Is.True, $"Route 91 runs into the water at {At2(q)}, off the deck");
                    carried++;
                }
            TestContext.WriteLine($"{carried} of Route 91's samples stand over water, all on the deck");
            Assert.That(carried, Is.GreaterThan(0));
        }

        // ---- row 8: Wharf Road's final ramp and the turning pad ----------------------------------------------------

        /// <summary>The scope's sampled numbers for the final ramp (PR 2 row 8): the steepest quarter-metre centre grade
        /// and the lowest bed across the full width. The accepted design grade is 14.1%; the scope says plainly that it
        /// is not a claim about every R16 quarter-metre slope, so it is held over a metre. The quarter-metre grade is held
        /// to the scope's number within one R16 code over a station: the sampler places a point in float texel
        /// coordinates, and the editor rounds them differently from .NET. The steepest station, (30.08, 112.32), reads
        /// 0.159691 in the editor and 0.159653 headless: a hundredth of a millimetre of height, where a code is 0.18 mm.</summary>
        const float RampLocalGrade = 0.15966738f, RampMinBed = 3.618219f, RampDesignGrade = 0.1415f;
        const float RampStationRoundOff = NineMileCreekGround.CodeMetres / 0.25f;

        [Test]
        public void WharfRoadsFinalRampClimbsAtItsAcceptedGrade_OnDryGround_ClearOfEverySolid()
        {
            NineMileCreekRoads.Way way = Way(NineMileCreekRoads.WharfRoadName);
            Assert.That(way.Route, Is.EqualTo(G.WharfRoad), "Wharf Road is not its final line");
            HashSet<Vector2Int> top = Cells(Layer(RoadKitV3.TopLayerName));
            var outlines = SolidOutlines(out var polygons);
            float steepest = 0f, lowest = float.MaxValue, last = 0f;
            Vector2 steepestAt = default, lowestAt = default;
            int inSolid = 0;
            string firstSolid = null, firstGap = null;
            foreach (var station in Sweep(G.WharfRoad, way.HalfWidth))
            {
                float z = Ground(station.centre);
                if (!station.first && Mathf.Abs(z - last) / station.step > steepest)
                {
                    steepest = Mathf.Abs(z - last) / station.step;
                    steepestAt = station.centre;
                }
                last = z;
                if (!Painted(station.centre, top)) firstGap ??= At2(station.centre);
                foreach (Vector2 q in station.across)
                {
                    float bed = Ground(q);
                    if (bed < lowest) { lowest = bed; lowestAt = q; }
                    string solid = SolidAt(q, outlines, polygons);
                    if (solid != null) { inSolid++; firstSolid ??= $"{solid} at {At2(q)}"; }
                }
            }
            var (design, designAt) = SteepestOver(G.WharfRoad, 1f, Rect.zero);
            var nearest = NearestSolid(G.WharfRoad, outlines);
            TestContext.WriteLine($"Wharf Road, {way.Surface}, half width {Shown(way.HalfWidth)}: steepest quarter-metre centre grade " +
                                  $"{steepest:0.######} at {At2(steepestAt)}; steepest over a metre {design:0.######} at {At2(designAt)}; " +
                                  $"lowest bed {lowest:0.######} at {At2(lowestAt)}; nearest solid {nearest.who} {nearest.clearance:0.###} m");
            Assert.That(firstGap, Is.Null, $"Wharf Road's centre line runs off the painted road at {firstGap}");
            Assert.That(steepest, Is.LessThanOrEqualTo(RampLocalGrade + RampStationRoundOff), $"the ramp steepens at {At2(steepestAt)}");
            Assert.That(design, Is.LessThanOrEqualTo(RampDesignGrade), $"the ramp climbs past its design grade at {At2(designAt)}");
            Assert.That(lowest, Is.GreaterThanOrEqualTo(RampMinBed), $"the ramp dips at {At2(lowestAt)}");
            Assert.That(inSolid, Is.Zero, $"Wharf Road's carriageway runs through {firstSolid}");
            Assert.That(nearest.clearance, Is.GreaterThanOrEqualTo(way.HalfWidth + 0.125f), $"{nearest.who} stands on Wharf Road");

            // The ramp ends on the turning pad, and every cell of the pad is painted.
            Vector2 end = G.WharfRoad[G.WharfRoad.Length - 1];
            Assert.That(Vector2.Distance(end, G.TurningPadCentre), Is.LessThan(G.TurningPadRadius), "the ramp stops short of the pad");
            int pad = 0, unpainted = 0;
            for (int x = Mathf.FloorToInt(G.TurningPadCentre.x - G.TurningPadRadius); x <= Mathf.CeilToInt(G.TurningPadCentre.x + G.TurningPadRadius); x++)
                for (int y = Mathf.FloorToInt(G.TurningPadCentre.y - G.TurningPadRadius); y <= Mathf.CeilToInt(G.TurningPadCentre.y + G.TurningPadRadius); y++)
                {
                    if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), G.TurningPadCentre) > G.TurningPadRadius) continue;
                    pad++;
                    if (!Painted(new Vector2(x + 0.5f, y + 0.5f), top)) unpainted++;
                }
            // The solids that stand on the pad are later-wave leftovers the owner accepted (PR 2's road-footprint
            // table); printed for the record, not held here.
            string[] onPad = outlines.Where(o => DistanceToSegment(G.TurningPadCentre, o.a, o.b) - o.radius < G.TurningPadRadius)
                                     .Select(o => o.who).Distinct().ToArray();
            TestContext.WriteLine($"turning pad: {pad} cells, {unpainted} unpainted; solids reaching into it: " +
                                  (onPad.Length == 0 ? "none" : string.Join(", ", onPad)));
            Assert.That(unpainted, Is.Zero, "the turning pad has holes");
        }
    }
}
