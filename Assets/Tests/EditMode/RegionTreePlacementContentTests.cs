using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>No tree or shrub stands on the beach, in the water or on a road</b> (owner, 2026-11-15: "Did trees get
    /// placed on the beach in that screenshot?"). Held against what SHIPS, in every region scene: each live tree and
    /// shrub (a SpriteRenderer drawn with <c>Tree.mat</c> or <c>LitShrub.mat</c>) at its world pivot, over the
    /// ground the region's own <c>TerrainSplatSurface</c> paints, decoded straight from the committed height PNG.
    ///
    /// <para><b>The floor is the region's own number</b>: the splat surface's <c>_floorMarram</c>, the height below
    /// which the ground shader paints bare sand (and below that the wet strand and the water). A region drawn by
    /// rectangle zones (<see cref="RectTidalTerrain"/>) has no painted sand line; there a plant must stand on a zone's
    /// flat top, never on the falloff that IS its beach or quay edge.</para>
    ///
    /// <para><b>Roads</b>: Nine Mile Creek's road network file (every route and corner flare at its carriageway,
    /// <see cref="NineMileCreekRoadClearings.Inside"/>, the planter's own clearing rule), and wherever a splat
    /// surface carries a path channel (<c>_splatF</c> red, St Peters), the painted path.</para>
    ///
    /// <para>The census these tests hold, measured offline on main 77be3b85be: Nine Mile Creek had 46 trees and 149
    /// shrubs below the floor (the y −192 and y +192 field-boundary hedgerows running out over the strand, a road hedge
    /// marching 80 m into the sea at x −222, and the north wood lot's creek edge). They were retired whole, each
    /// GameObject with every document under it, as the layer refresh's step 12 retires planting on a carriageway.
    /// St Peters (259 trees, 32 shrubs) and Greywick (11) had none.</para>
    /// </summary>
    public class RegionTreePlacementContentTests
    {
        const string ScenesFolder = "Assets/_Project/Scenes";
        const string TreeMaterial = "Assets/_Project/Art/Materials/Tree.mat";
        const string ShrubMaterial = "Assets/_Project/Art/Materials/LitShrub.mat";
        const string SplatClass = "HiddenHarbours.Art.TerrainSplatSurface";
        const string RectTerrainClass = "HiddenHarbours.World.RectTidalTerrain";
        const string NineMileCreekScene = "NineMileCreek";
        const int GameObjectClass = 1, SpriteRendererClass = 212;

        /// <summary>A path texel counts as path at half its channel or more (8-bit red).</summary>
        const int PathChannelThreshold = 128;

        static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);
        static readonly Regex NumRx = new Regex(@"(\w+): (-?[\d.eE+-]+)", RegexOptions.CultureInvariant);

        public static IEnumerable<string> RegionScenes() =>
            Directory.GetFiles(ScenesFolder, "*.unity").Select(Path.GetFileNameWithoutExtension).OrderBy(s => s, StringComparer.Ordinal);

        // =============================================================================================================
        //  THE RULE, PER REGION
        // =============================================================================================================

        [TestCaseSource(nameof(RegionScenes))]
        public void NoTreeOrShrubStandsBelowTheDryGroundFloorOrOnARoad(string region)
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{region}.unity"));
            List<Plant> plants = Plants(scene);
            if (plants.Count == 0) { Assert.Pass($"{region}: no trees or shrubs."); return; }

            Ground ground = Ground.Of(scene, region);
            Assert.IsNotNull(ground, $"{region} carries {plants.Count} trees and shrubs but no ground this test can read " +
                                     "(a TerrainSplatSurface or a RectTidalTerrain) — so nothing proves they stand on land");
            var roads = region == NineMileCreekScene ? NineMileCreekRoadNetwork.Load() : null;

            var offenders = new List<string>();
            foreach (Plant p in plants)
            {
                string why = ground.WhyNot(p.At);
                if (why == null && roads != null)
                {
                    foreach (var r in roads.Routes.Concat(roads.Flares))
                        if (NineMileCreekRoadClearings.Inside(r, p.At)) { why = $"on {r}'s carriageway"; break; }
                }
                if (why != null) offenders.Add($"{p.Name} &{p.FileId} at ({p.At.x:F1}, {p.At.y:F1}): {why}");
            }
            TestContext.WriteLine($"[TreePlacement] {region}: {plants.Count(p => p.Tree)} trees, {plants.Count(p => !p.Tree)} shrubs, " +
                                  $"{offenders.Count} off the dry ground.");
            Assert.IsEmpty(offenders, $"{region}: {offenders.Count} trees or shrubs stand on the beach, in the water or " +
                                      "on a road:\n  " + string.Join("\n  ", offenders.Take(25)));
        }

        // =============================================================================================================
        //  THE PREMISES — so the rule above cannot pass by reading nothing
        // =============================================================================================================

        /// <summary>The owner's strand shot (camera −344, −190) is beach by this test's own reading, and a tree deep in
        /// the middle wood lot is dry ground: the sampler sees the ground the player sees.</summary>
        [Test]
        public void TheOwnersStrandSpotReadsAsBeachAndTheWoodLotAsDryGround()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{NineMileCreekScene}.unity"));
            Ground ground = Ground.Of(scene, NineMileCreekScene);
            Assert.IsNotNull(ground);
            Assert.IsNotNull(ground.WhyNot(new Vector2(-344f, -192f)), "the strand under the owner's shot reads as dry ground");
            Assert.IsNull(ground.WhyNot(new Vector2(-75f, 5f)), "the middle wood lot's heart reads as beach or water");
            Assert.IsNotNull(ground.WhyNot(new Vector2(-230f, -270f)), "open water south of the strand reads as dry ground");
        }

        [Test]
        public void EveryRegionWithPlantingIsFound()
        {
            var counts = RegionScenes().ToDictionary(r => r,
                r => Plants(SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{r}.unity"))).Count);
            TestContext.WriteLine("[TreePlacement] " + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value)));
            Assert.That(counts[NineMileCreekScene], Is.GreaterThan(500), "Nine Mile Creek's planting was not found");
            Assert.That(counts["StPeters"], Is.GreaterThan(200), "St Peters' woods were not found");
        }

        // =============================================================================================================
        //  THE REPLANTING (11-15) — wild ground dense, lived-in ground deliberate
        // =============================================================================================================

        const string PlanAsset = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset";
        const string PlantingRoot = "CreekPlanting";

        /// <summary>A new plant stands this far above the sand line: the planter's bank floor (2.2 m) less the slack
        /// this test allows, never the sand line itself.</summary>
        const float ReplantGroundMargin = 0.5f;
        const float ReplantTreeRoadClear = 2.0f, ReplantShrubRoadClear = 1.0f;
        const float ReplantTreeGap = 2.9f, ReplantShrubGap = 1.2f, ReplantMixedGap = 1.7f;
        const float ReplantTreeLotSetback = 2.5f, ReplantShrubLotSetback = 0.8f, ReplantCampSetback = 2.5f;
        /// <summary>A door's apron: half its width either side of the door's centre line, this deep out from the wall.</summary>
        const float DoorApronHalfWidth = 3f, DoorApronDepth = 9f;
        /// <summary>A yard tree stands within this of a house's lot, or this close to a road's edge (a lane row).</summary>
        const float YardTreeHouseReach = 16f, YardTreeLaneReach = 8f;
        /// <summary>A stand tree has at least this many trees within this radius: a stand, never a lone tree.</summary>
        const int StandNeighbours = 3; const float StandRadius = 10f;
        /// <summary>A thicket shrub stands from this far inside a wood's rim to this far outside it.</summary>
        const float ThicketInside = 6f, ThicketOutside = 7f;
        /// <summary>The plan's north wood lot (its <c>at</c> and <c>r</c> are read from the plan).</summary>
        const string NorthWoodLot = "woodlot.nmc_north";
        /// <summary>The trees the north wood lot keeps: it held 57 before the 11-15 sand-line retirement and holds 57
        /// pulled back onto the green (11-16). The floor is the ratchet's, with room for a later nudge.</summary>
        const int NorthWoodTreesAtLeast = 50;

        /// <summary>
        /// The owner, 11-15: "Have some areas quite dense and some seen as planted in more lived in areas with more
        /// central focused trees." Every plant under <c>CreekPlanting</c> stands on dry ground above the sand line,
        /// clear of every road, hedge clearance, lot, door apron and campground piece, spaced from every other plant;
        /// a yard tree stands by a house or along a lane; a stand tree stands inside a wood lot among other trees; a
        /// thicket shrub stands at a wood's rim.
        /// </summary>
        [Test]
        public void TheReplantingStandsWhereTheRulesSay()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{NineMileCreekScene}.unity"));
            List<Plant> all = Plants(scene);
            List<Plant> planted = all.Where(p => p.Path.StartsWith(PlantingRoot + "/", StringComparison.Ordinal)).ToList();
            Assert.That(planted.Count(p => p.Tree), Is.GreaterThan(50), $"the replanting's trees under {PlantingRoot} were not found");
            Assert.That(planted.Count(p => !p.Tree), Is.GreaterThan(50), $"the replanting's shrubs under {PlantingRoot} were not found");

            var ground = Ground.Of(scene, NineMileCreekScene) as PaintedGround;
            Assert.IsNotNull(ground, "Nine Mile Creek's ground is not a painted splat surface");
            var net = NineMileCreekRoadNetwork.Load();
            var lots = PlanOption("nmc.plan.lots");
            var buildingLots = new[] { "town", "working", "south_shore_houses", "waterfront" }
                .SelectMany(g => lots.Get(g)?.Items ?? new List<KeySceneImport.Json>()).Where(HasSize).ToList();
            var houseLots = new[] { "town", "south_shore_houses", "waterfront" }
                .SelectMany(g => lots.Get(g)?.Items ?? new List<KeySceneImport.Json>()).Where(HasSize).ToList();
            Assert.That(buildingLots.Count, Is.GreaterThan(20), "the plan's building lots were not found");
            var camp = lots.Get("campground");
            var campBoxes = camp.Get("pitches").Items.Concat(camp.Get("buildings").Items).Where(HasSize).Select(BoxOf).ToList();
            var woods = lots.Get("wood_lots").Items
                .Select(w => (at: new Vector2(w.Get("at").Items[0].Float("wood.at"), w.Get("at").Items[1].Float("wood.at")), r: w.Get("r").Float("wood.r")))
                .ToList();
            Assert.That(woods.Count, Is.GreaterThanOrEqualTo(4), "the plan's wood lots were not found");

            var offenders = new List<string>();
            foreach (Plant p in planted)
            {
                void Off(string why) => offenders.Add($"{p.Path} at ({p.At.x:F1}, {p.At.y:F1}): {why}");
                float e = ground.ElevationAt(p.At);
                if (e < ground.Floor + ReplantGroundMargin) Off($"ground {e:F2} m, under {ground.Floor + ReplantGroundMargin:F1} m");
                string road = NineMileCreekRoadClearings.Why(net, p.At, hedged: p.Tree);
                if (road != null) Off(road);
                float edge = RoadEdge(net, p.At);
                if (edge < (p.Tree ? ReplantTreeRoadClear : ReplantShrubRoadClear)) Off($"{edge:F2} m from a road's edge");
                foreach (Plant q in all)
                {
                    if (q == p) continue;
                    float d = Vector2.Distance(p.At, q.At);
                    float gap = p.Tree && q.Tree ? ReplantTreeGap : !p.Tree && !q.Tree ? ReplantShrubGap : ReplantMixedGap;
                    if (d < gap) Off($"{d:F2} m from {q.Path}");
                }
                foreach (var lot in buildingLots)
                {
                    string id = lot.Get("id").Str("lot.id");
                    if (Distance(BoxOf(lot), p.At) < (p.Tree ? ReplantTreeLotSetback : ReplantShrubLotSetback)) Off($"against {id}'s walls");
                    if (InDoorApron(lot, p.At)) Off($"in {id}'s door apron");
                }
                if (campBoxes.Any(b => Distance(b, p.At) < ReplantCampSetback)) Off("on a campground pitch or building");
                if (p.Path.Contains("/Yards/") && p.Tree &&
                    !(houseLots.Any(l => Distance(BoxOf(l), p.At) <= YardTreeHouseReach) || edge <= YardTreeLaneReach))
                    Off("a yard tree by no house and no lane");
                if (p.Path.Contains("/Stands/"))
                {
                    float rim = woods.Min(w => Vector2.Distance(p.At, w.at) - w.r);
                    if (p.Tree)
                    {
                        if (rim > 0f) Off($"a stand tree {rim:F1} m outside every wood lot");
                        int n = all.Count(q => q.Tree && q != p && Vector2.Distance(q.At, p.At) < StandRadius);
                        if (n < StandNeighbours) Off($"a stand tree with {n} trees within {StandRadius} m");
                    }
                    else if (rim < -ThicketInside || rim > ThicketOutside) Off($"a thicket shrub {rim:F1} m from a wood's rim");
                }
            }
            TestContext.WriteLine($"[TreePlacement] replanting: {planted.Count(p => p.Tree)} trees, {planted.Count(p => !p.Tree)} shrubs; " +
                                  $"{offenders.Count} off the rules.");
            Assert.IsEmpty(offenders, $"{offenders.Count} replanted trees or shrubs break the rules:\n  " + string.Join("\n  ", offenders.Take(25)));
        }

        /// <summary>
        /// The owner, 11-16: "North yard pulled back." Every live tree inside the plan's north wood lot, whichever
        /// root it hangs under, stands on the green: at or above the region's own grass line (the splat surface's
        /// <c>_floorGrass</c>), never on the dune band between the sand line and the grass. The wood keeps its trees.
        /// </summary>
        [Test]
        public void TheNorthWoodStandsOnTheGreen()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{NineMileCreekScene}.unity"));
            var ground = Ground.Of(scene, NineMileCreekScene) as PaintedGround;
            Assert.IsNotNull(ground, "Nine Mile Creek's ground is not a painted splat surface");
            string grassField = scene.Docs.First(d => d.IsScript(SplatClass)).Field("_floorGrass");
            Assert.IsNotNull(grassField, "the splat surface names no _floorGrass");
            float grass = ParseFloat(grassField);
            Assert.That(grass, Is.GreaterThan(ground.Floor), "the grass line is not above the sand line");
            var lot = PlanOption("nmc.plan.lots").Get("wood_lots").Items.Single(w => w.Get("id").Text == NorthWoodLot);
            var at = new Vector2(lot.Get("at").Items[0].Float("at"), lot.Get("at").Items[1].Float("at"));
            float r = lot.Get("r").Float("r");

            List<Plant> trees = Plants(scene).Where(p => p.Tree && Vector2.Distance(p.At, at) <= r).ToList();
            var offenders = trees.Where(p => ground.ElevationAt(p.At) < grass)
                .Select(p => $"{p.Path} at ({p.At.x:F1}, {p.At.y:F1}): ground {ground.ElevationAt(p.At):F2} m, under the {grass} m grass line")
                .ToList();
            TestContext.WriteLine($"[TreePlacement] north wood: {trees.Count} trees, {offenders.Count} on the dune band.");
            Assert.IsEmpty(offenders, $"{offenders.Count} north wood trees stand on the dune band:\n  " + string.Join("\n  ", offenders.Take(25)));
            Assert.That(trees.Count, Is.GreaterThanOrEqualTo(NorthWoodTreesAtLeast),
                $"the north wood lot holds {trees.Count} trees: pulled back onto the green, it keeps the 57 it held");
        }

        /// <summary>No two live trees stand closer than this: two trunks inside one crown read as one tree drawn twice.</summary>
        const float TreeMinimumGap = 2.0f;

        /// <summary>The pairs closer than <see cref="TreeMinimumGap"/> a region holds today, a ratchet that may only
        /// fall. St Peters' fourteen are in IslandWoods (the closest 0.67 m, two red spruce at (127.9, -40.6)); this
        /// pass moves none of them.</summary>
        static readonly Dictionary<string, int> KnownClosePairs = new Dictionary<string, int> { ["StPeters"] = 14 };

        [TestCaseSource(nameof(RegionScenes))]
        public void NoTwoTreesStandInsideOneCrown(string region)
        {
            List<Plant> trees = Plants(SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{region}.unity"))).Where(p => p.Tree).ToList();
            var close = new List<string>();
            for (int i = 0; i < trees.Count; i++)
                for (int j = i + 1; j < trees.Count; j++)
                {
                    float d = Vector2.Distance(trees[i].At, trees[j].At);
                    if (d < TreeMinimumGap) close.Add($"{d:F2} m: {trees[i].Path} and {trees[j].Path} at ({trees[i].At.x:F1}, {trees[i].At.y:F1})");
                }
            int allowed = KnownClosePairs.TryGetValue(region, out int k) ? k : 0;
            TestContext.WriteLine($"[TreePlacement] {region}: {close.Count} tree pairs under {TreeMinimumGap} m (allowed {allowed})\n  " +
                                  string.Join("\n  ", close));
            Assert.That(close.Count, Is.LessThanOrEqualTo(allowed),
                $"{region}: {close.Count} tree pairs stand under {TreeMinimumGap} m apart, {allowed} known:\n  " + string.Join("\n  ", close.Take(25)));
        }

        /// <summary>The scripts that mark a way in or a building's interior.</summary>
        static readonly string[] DoorClasses =
        {
            "HiddenHarbours.World.BuildingInterior", "HiddenHarbours.Economy.HomeDoor", "HiddenHarbours.World.DoorPortal",
            "HiddenHarbours.World.InteriorDoor", "HiddenHarbours.World.RegionDoor",
        };
        const float DoorClear = 4f;

        [TestCaseSource(nameof(RegionScenes))]
        public void NoTreeOrShrubStandsInADoorway(string region)
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{region}.unity"));
            var doors = new List<(Vector2 at, string what)>();
            foreach (Doc d in scene.Docs)
            {
                string cls = DoorClasses.FirstOrDefault(d.IsScript);
                if (cls == null) continue;
                Doc go = scene.Get(d.FieldRef("m_GameObject"));
                if (go == null || !LiveInHierarchy(scene, go)) continue;
                doors.Add((WorldPivot(scene, go), $"{go.Field("m_Name")} ({cls.Substring(cls.LastIndexOf('.') + 1)})"));
            }
            var offenders = new List<string>();
            foreach (Plant p in Plants(scene))
                foreach (var door in doors)
                    if (Vector2.Distance(p.At, door.at) < DoorClear)
                        offenders.Add($"{p.Path} {Vector2.Distance(p.At, door.at):F2} m from {door.what}");
            TestContext.WriteLine($"[TreePlacement] {region}: {doors.Count} doors, {offenders.Count} plants within {DoorClear} m of one.");
            Assert.IsEmpty(offenders, $"{region}: plants stand in a doorway:\n  " + string.Join("\n  ", offenders.Take(25)));
        }

        static KeySceneImport.Json PlanOption(string key)
        {
            var top = KeySceneImport.ReadAsset(File.ReadAllText(PlanAsset), PlanAsset);
            foreach (var row in top.Single(f => f.Key == "Pieces").Value.Items)
                foreach (var o in row.Get("Options")?.Items ?? new List<KeySceneImport.YamlNode>())
                    if (o.Get("Key")?.Scalar == key)
                        return KeySceneImport.Json.Parse(o.Get("Value").Scalar, key);
            Assert.Fail($"{PlanAsset} holds no {key} option");
            return null;
        }

        static bool HasSize(KeySceneImport.Json o) => o.Get("size") is { Kind: KeySceneImport.Json.Of.Array } s && s.Items.Count == 2;

        /// <summary>A lot's box about its <c>at</c>: its size along x, turned when it faces east or west.</summary>
        static Rect BoxOf(KeySceneImport.Json o)
        {
            string id = o.Get("id")?.Text ?? "a lot";
            var at = o.Get("at").Items; var size = o.Get("size").Items;
            bool turned = o.Get("faces") is { Kind: KeySceneImport.Json.Of.String } f && (f.Text == "E" || f.Text == "W");
            float w = size[turned ? 1 : 0].Float(id + ".size"), h = size[turned ? 0 : 1].Float(id + ".size");
            float x = at[0].Float(id + ".at"), y = at[1].Float(id + ".at");
            return Rect.MinMaxRect(x - w / 2f, y - h / 2f, x + w / 2f, y + h / 2f);
        }

        static float Distance(Rect r, Vector2 p) =>
            new Vector2(Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax)).magnitude;

        /// <summary>Is <paramref name="p"/> in the strip in front of the lot's door, the way the lot faces?</summary>
        static bool InDoorApron(KeySceneImport.Json lot, Vector2 p)
        {
            if (!(lot.Get("faces") is { Kind: KeySceneImport.Json.Of.String } f)) return false;
            Rect box = BoxOf(lot);
            Vector2 d = p - box.center;
            float along, outward, half;
            switch (f.Text)
            {
                case "N": along = d.x; outward = d.y; half = box.height / 2f; break;
                case "S": along = d.x; outward = -d.y; half = box.height / 2f; break;
                case "E": along = d.y; outward = d.x; half = box.width / 2f; break;
                case "W": along = d.y; outward = -d.x; half = box.width / 2f; break;
                default: return false;
            }
            return Mathf.Abs(along) <= DoorApronHalfWidth && outward >= half && outward <= half + DoorApronDepth;
        }

        /// <summary>How far <paramref name="p"/> stands from the nearest road's or corner flare's edge (negative on it).</summary>
        static float RoadEdge(NineMileCreekRoadNetwork.Network net, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var r in net.Routes.Concat(net.Flares))
            {
                float d = r.Points.Length == 1 ? Vector2.Distance(p, r.Points[0]) : float.MaxValue;
                for (int i = 0; i + 1 < r.Points.Length; i++) d = Mathf.Min(d, NineMileCreekRoadNetwork.PointToSegment(p, r.Points[i], r.Points[i + 1]));
                best = Mathf.Min(best, d - r.HalfWidth);
            }
            return best;
        }

        // =============================================================================================================
        //  PLANTS
        // =============================================================================================================

        sealed class Plant
        {
            public string Name; public long FileId; public Vector2 At; public bool Tree;
            /// <summary>The GameObject's hierarchy path, root first (<c>CreekPlanting/Stands/RedMaple_1</c>).</summary>
            public string Path;
        }

        static List<Plant> Plants(SceneYaml scene)
        {
            string tree = AssetDatabase.AssetPathToGUID(TreeMaterial), shrub = AssetDatabase.AssetPathToGUID(ShrubMaterial);
            Assert.IsNotEmpty(tree, TreeMaterial); Assert.IsNotEmpty(shrub, ShrubMaterial);
            var list = new List<Plant>();
            foreach (Doc sr in scene.Docs)
            {
                if (sr.ClassId != SpriteRendererClass || sr.Stripped || sr.Field("m_Enabled") != "1") continue;
                string mat = FirstMaterialGuid(sr);
                if (mat != tree && mat != shrub) continue;
                Doc go = scene.Get(sr.FieldRef("m_GameObject"));
                if (go == null || go.ClassId != GameObjectClass || !LiveInHierarchy(scene, go)) continue;
                list.Add(new Plant { Name = go.Field("m_Name"), FileId = go.FileId, At = WorldPivot(scene, go), Tree = mat == tree,
                                     Path = PathOf(scene, go) });
            }
            return list;
        }

        static string PathOf(SceneYaml scene, Doc go)
        {
            var names = new List<string>();
            Doc tr = scene.TransformOf(go);
            while (tr != null)
            {
                names.Add(scene.Get(tr.FieldRef("m_GameObject"))?.Field("m_Name") ?? "?");
                long father = tr.FieldRef("m_Father");
                tr = father == 0 ? null : scene.Get(father);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        static string FirstMaterialGuid(Doc sr)
        {
            string[] lines = sr.Lines;
            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (lines[i] != "  m_Materials:") continue;
                if (!lines[i + 1].StartsWith("  - ", StringComparison.Ordinal)) return null;
                Match m = GuidRx.Match(lines[i + 1]);
                return m.Success ? m.Groups[1].Value : null;
            }
            return null;
        }

        static bool LiveInHierarchy(SceneYaml scene, Doc go)
        {
            Doc tr = scene.TransformOf(go);
            while (tr != null)
            {
                Doc owner = scene.Get(tr.FieldRef("m_GameObject"));
                if (owner != null && owner.Field("m_IsActive") != "1") return false;
                long father = tr.FieldRef("m_Father");
                tr = father == 0 ? null : scene.Get(father);
            }
            return true;
        }

        /// <summary>The GameObject's pivot in world metres: its own local position through every ancestor's scale,
        /// z-rotation and position.</summary>
        static Vector2 WorldPivot(SceneYaml scene, Doc go)
        {
            Doc tr = scene.TransformOf(go);
            Vector2 p = Xy(tr.Field("m_LocalPosition"));
            long father = tr.FieldRef("m_Father");
            while (father != 0)
            {
                Doc f = scene.Require(father, "a plant's ancestor");
                var s = Nums(f.Field("m_LocalScale"));
                var q = Nums(f.Field("m_LocalRotation"));
                float angle = 2f * Mathf.Atan2(Get(q, "z", 0f), Get(q, "w", 1f));
                var scaled = new Vector2(p.x * Get(s, "x", 1f), p.y * Get(s, "y", 1f));
                float c = Mathf.Cos(angle), n = Mathf.Sin(angle);
                p = Xy(f.Field("m_LocalPosition")) + new Vector2(c * scaled.x - n * scaled.y, n * scaled.x + c * scaled.y);
                father = f.FieldRef("m_Father");
            }
            return p;
        }

        static Dictionary<string, float> Nums(string yaml)
        {
            var d = new Dictionary<string, float>();
            if (yaml == null) return d;
            foreach (Match m in NumRx.Matches(yaml)) d[m.Groups[1].Value] = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return d;
        }

        static float Get(Dictionary<string, float> d, string k, float fallback) => d.TryGetValue(k, out float v) ? v : fallback;
        static Vector2 Xy(string yaml) { var d = Nums(yaml); return new Vector2(Get(d, "x", 0f), Get(d, "y", 0f)); }

        // =============================================================================================================
        //  GROUND
        // =============================================================================================================

        abstract class Ground
        {
            /// <summary>Why a plant may not stand at <paramref name="p"/>, or null when it is dry land.</summary>
            public abstract string WhyNot(Vector2 p);

            public static Ground Of(SceneYaml scene, string region)
            {
                Doc splat = scene.Docs.FirstOrDefault(d => d.IsScript(SplatClass));
                if (splat != null) return new PaintedGround(splat, region);
                Doc rect = scene.Docs.FirstOrDefault(d => d.IsScript(RectTerrainClass));
                if (rect != null) return new ZoneGround(rect);
                return null;
            }
        }

        /// <summary>The splat surface's height PNG, sampled as the GPU samples it (bilinear on texel centres), with
        /// its sand line and its path channel.</summary>
        sealed class PaintedGround : Ground
        {
            readonly Vector2 _centre, _size;
            readonly float _min, _max, _floor;
            readonly ushort[] _codes; readonly int _w, _h;
            readonly byte[] _path; readonly int _pw, _ph;

            /// <summary>The sand line: the height below which the ground shader paints bare sand.</summary>
            public float Floor => _floor;

            public PaintedGround(Doc splat, string region)
            {
                _centre = Xy(splat.Field("_worldCenter")); _size = Xy(splat.Field("_worldSize"));
                _min = ParseFloat(splat.Field("_heightMin")); _max = ParseFloat(splat.Field("_heightMax"));
                _floor = ParseFloat(splat.Field("_floorMarram"));
                var height = TerrainPlanPng.ReadFile(PathOf(splat, "_heightTexture", region));
                Assert.AreEqual(16, height.BitDepth, $"{region}: the height map is not 16-bit");
                Assert.AreEqual(1, height.Channels, $"{region}: the height map is not greyscale");
                _codes = height.Samples; _w = height.Width; _h = height.Height;
                string f = splat.Field("_splatF");
                if (f != null && GuidRx.IsMatch(f))
                {
                    var path = TerrainPlanPng.ReadFile(PathOf(splat, "_splatF", region));
                    Assert.AreEqual(8, path.BitDepth, $"{region}: the path splat is not 8-bit");
                    _path = path.Channel8(0); _pw = path.Width; _ph = path.Height;
                }
            }

            static string PathOf(Doc splat, string key, string region)
            {
                Match m = GuidRx.Match(splat.Field(key) ?? "");
                Assert.IsTrue(m.Success, $"{region}: the splat surface names no {key}");
                string asset = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                Assert.IsNotEmpty(asset, $"{region}: {key} names a missing asset");
                return Path.GetFullPath(asset);
            }

            float Code(int x, int y)   // y counts from the bottom row, as the texture's v does
            {
                x = Mathf.Clamp(x, 0, _w - 1); y = Mathf.Clamp(y, 0, _h - 1);
                return _codes[(_h - 1 - y) * _w + x] / 65535f;
            }

            public float ElevationAt(Vector2 p)
            {
                float u = (p.x - (_centre.x - _size.x * 0.5f)) / _size.x, v = (p.y - (_centre.y - _size.y * 0.5f)) / _size.y;
                float fx = u * _w - 0.5f, fy = v * _h - 0.5f;
                int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0, ty = fy - y0;
                float r = Mathf.Lerp(Mathf.Lerp(Code(x0, y0), Code(x0 + 1, y0), tx), Mathf.Lerp(Code(x0, y0 + 1), Code(x0 + 1, y0 + 1), tx), ty);
                return _min + (_max - _min) * r;
            }

            public override string WhyNot(Vector2 p)
            {
                float e = ElevationAt(p);
                if (!(e >= _floor)) return $"ground at {e:F2} m, below the {_floor} m sand line (beach, strand or water)";
                if (_path != null)
                {
                    float u = (p.x - (_centre.x - _size.x * 0.5f)) / _size.x, v = (p.y - (_centre.y - _size.y * 0.5f)) / _size.y;
                    int x = Mathf.Clamp((int)(u * _pw), 0, _pw - 1), y = Mathf.Clamp((int)(v * _ph), 0, _ph - 1);
                    if (_path[(_ph - 1 - y) * _pw + x] >= PathChannelThreshold) return "on a painted path";
                }
                return null;
            }
        }

        /// <summary>A rectangle-zone region: land is a zone's flat top; its falloff is the beach or quay edge.</summary>
        sealed class ZoneGround : Ground
        {
            readonly RectTidalTerrain.LandZone[] _zones;

            public ZoneGround(Doc rect)
            {
                var zones = new List<RectTidalTerrain.LandZone>();
                string[] lines = rect.Lines;
                for (int i = 0; i + 3 < lines.Length; i++)
                {
                    if (!lines[i].StartsWith("  - Center:", StringComparison.Ordinal)) continue;
                    zones.Add(new RectTidalTerrain.LandZone(Xy(lines[i].Substring(lines[i].IndexOf(':') + 1)),
                        Xy(lines[i + 1].Substring(lines[i + 1].IndexOf(':') + 1)),
                        ParseFloat(lines[i + 2].Substring(lines[i + 2].IndexOf(':') + 1).Trim()),
                        ParseFloat(lines[i + 3].Substring(lines[i + 3].IndexOf(':') + 1).Trim())));
                }
                Assert.IsNotEmpty(zones, "a RectTidalTerrain with no zones");
                _zones = zones.ToArray();
            }

            public override string WhyNot(Vector2 p)
            {
                foreach (var z in _zones)
                    if (Mathf.Abs(p.x - z.Center.x) <= z.HalfSize.x && Mathf.Abs(p.y - z.Center.y) <= z.HalfSize.y) return null;
                return "off every land zone's flat top (on its beach or quay falloff, or in the water)";
            }
        }
    }
}
