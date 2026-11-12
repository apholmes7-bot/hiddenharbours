using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Reads committed Force Text and existing Defs; never opens, rebuilds or saves a scene.</summary>
    public class RegionSeamAuditTests
    {
        private static readonly string[] Scenes = { "EastWater", "Greywick", "NineMileCreek", "StPeters", "RicePoint", "Cumberland", "Greybox", "SouthWater" };
        private static readonly Dictionary<string, SceneYaml> Parsed = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ScriptGuids = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> RegionScenes = Directory.GetFiles("Assets/_Project/Data/Regions", "*.asset")
            .ToDictionary(p => GuidOf(File.ReadAllText(p + ".meta")), p => AssetField(p, "SceneName"), StringComparer.Ordinal);

        private static SceneYaml Scene(string name)
        {
            if (!Parsed.TryGetValue(name, out var scene))
                Parsed[name] = scene = SceneYaml.Parse(File.ReadAllText($"Assets/_Project/Scenes/{name}.unity").Replace("\r\n", "\n"));
            return scene;
        }

        private static string AssetField(string path, string key)
            => Regex.Match(File.ReadAllText(path), @"^  " + Regex.Escape(key) + @":\s*([^\r\n]*)", RegexOptions.Multiline).Groups[1].Value.Trim();
        private static string GuidOf(string text) => Regex.Match(text ?? "", @"guid: ([0-9a-f]{32})").Groups[1].Value;
        private static string Value(Doc doc, string key) => (doc.Field(key) ?? "").Trim('"');
        private static string ScriptGuid(string name)
        {
            if (!ScriptGuids.TryGetValue(name, out var guid))
            {
                var files = Directory.GetFiles("Assets/_Project/Code", name + ".cs.meta", SearchOption.AllDirectories);
                Assert.That(files.Length, Is.EqualTo(1), name);
                ScriptGuids[name] = guid = GuidOf(File.ReadAllText(files[0]));
            }
            return guid;
        }
        private static bool IsScript(Doc doc, string name) => doc.ClassId == 114 && GuidOf(doc.Field("m_Script")) == ScriptGuid(name);
        private static Doc GameObject(SceneYaml scene, Doc component) => scene.Require(component.FieldRef("m_GameObject"), "component owner");
        private static IEnumerable<Doc> Scripts(string scene, string type) => Scene(scene).Docs.Where(d => IsScript(d, type));
        private static Doc Anchor(string scene) => Scripts(scene, "RegionAnchor").Single();

        private static bool Active(SceneYaml scene, Doc gameObject)
        {
            while (gameObject != null)
            {
                if (gameObject.Field("m_IsActive") != "1") return false;
                var transform = scene.TransformOf(gameObject);
                long parent = transform.FieldRef("m_Father");
                gameObject = parent == 0 ? null : GameObject(scene, scene.Require(parent, "parent transform"));
            }
            return true;
        }

        private static IEnumerable<Match> Named(Doc anchor) => Regex.Matches(anchor.Text,
            @"^  - Key: (.*?)\n    ArrivalPoint: (.*?)\n    DisembarkPoint: (.*?)$", RegexOptions.Multiline).Cast<Match>();

        private static List<RegionSeamEdge> ReadEdges()
        {
            var result = new List<RegionSeamEdge>();
            foreach (string name in Scenes)
            {
                var scene = Scene(name);
                foreach (var passage in Scripts(name, "RegionPassage"))
                {
                    var go = GameObject(scene, passage);
                    string objectName = Value(go, "m_Name"), key = Value(passage, "_arrivalKey");
                    RegionScenes.TryGetValue(GuidOf(passage.Field("_target")), out string target);
                    var policy = scene.Get(passage.FieldRef("_accessPolicy"));
                    var mode = objectName.Contains("route_91") ? RegionSeamMode.Road :
                        key == "bar" || (policy != null && IsScript(policy, "LandPassageAccess")) ? RegionSeamMode.Foot : RegionSeamMode.Sea;
                    var loader = scene.Get(passage.FieldRef("_loader"));
                    var bands = scene.ComponentsOf(go).Where(d => d.ClassId == 61).ToArray();
                    Assert.That(bands.Length, Is.EqualTo(1), name + "/" + objectName + " must have its audited box band");
                    bool enabled = Active(scene, go) && passage.Field("m_Enabled") == "1" &&
                        bands[0].Field("m_Enabled") == "1" && bands[0].Field("m_IsTrigger") == "1" &&
                        loader != null && IsScript(loader, "RegionSceneLoader");
                    result.Add(new RegionSeamEdge(name + "/" + objectName, name, target, key, mode, enabled));
                }
            }
            return result;
        }

        private static HashSet<string> BuildScenes() => new(Regex.Matches(File.ReadAllText("ProjectSettings/EditorBuildSettings.asset"),
            @"- enabled: 1\r?\n\s+path: Assets/_Project/Scenes/([^\r\n]+)\.unity").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);

        private static List<RegionSeamNode> ReadNodes()
        {
            var built = BuildScenes();
            var nodes = Scenes.Select(name => new RegionSeamNode(name, built.Contains(name),
                Named(Anchor(name)).Select(m => m.Groups[1].Value))).ToList();
            nodes.Add(new RegionSeamNode("WestWater", File.Exists("Assets/_Project/Scenes/WestWater.unity") && built.Contains("WestWater"), Array.Empty<string>()));
            return nodes;
        }
        private static RegionSeamGraph Graph() => new(ReadNodes(), ReadEdges());

        // Explicit expectations for the audited base; revise the design note with any accepted connection.
        private static readonly Dictionary<string, string[]> ExpectedEdges = new(StringComparer.Ordinal)
        {
            ["EastWater"] = new[] { "PassageToStPeters|StPeters|east_water|Sea|True" },
            ["Greywick"] = new[] { "PassageToCoddleCove|Greybox||Sea|True" },
            ["NineMileCreek"] = new[] { "PassageToWestWater|WestWater|nine_mile_creek|Sea|True", "PassageToStPeters|StPeters|bar|Foot|True", "PassageToCoddleCove|Greybox||Sea|True", "PassageToRicePointSea|RicePoint|nmc_sea|Sea|True", "PassageToRicePointStrand|RicePoint|nmc_strand|Foot|True" },
            ["StPeters"] = new[] { "PassageToNineMileCreek|NineMileCreek|bar|Foot|True", "PassageToWestWater|WestWater|st_peters|Sea|True", "PassageToEastWater|EastWater|st_peters|Sea|True" },
            ["RicePoint"] = new[] { "door.rpt_sea|NineMileCreek|rpt_sea|Sea|True", "door.rpt_route_91|NineMileCreek|rpt_route_91|Road|False", "door.rpt_strand|NineMileCreek|rpt_strand|Foot|True" },
            ["Cumberland"] = new[] { "door.cmb_route_91|NineMileCreek|cmb_route_91|Road|True", "door.cmb_north_beach|NineMileCreek|cmb_north_beach|Foot|True", "door.cmb_bay|NineMileCreek|cmb_bay|Sea|True" },
            ["Greybox"] = new[] { "PassageToPortGreywick|||Sea|True" },
            ["SouthWater"] = new[] { "PassageToRicePoint|RicePoint|south_water|Sea|True", "PassageToStPeters|StPeters|south_water|Sea|True", "PassageToEastWater|EastWater|south_water|Sea|True" },
        };

        private static readonly Dictionary<string, string[]> ExpectedKeys = new(StringComparer.Ordinal)
        {
            ["EastWater"] = new[] { "st_peters", "opening" },
            ["Greywick"] = Array.Empty<string>(),
            ["NineMileCreek"] = new[] { "bar", "rpt_sea", "rpt_strand" },
            ["StPeters"] = new[] { "east_water", "bar" },
            ["RicePoint"] = new[] { "nmc_sea", "nmc_strand", "nmc_route_91" },
            ["Cumberland"] = new[] { "nmc_route_91", "nmc_north_beach", "nmc_bay" },
            ["Greybox"] = Array.Empty<string>(),
            ["SouthWater"] = new[] { "rice_point", "st_peters", "east_water" },
        };

        private static readonly Dictionary<string, string[]> ExpectedPoints = new(StringComparer.Ordinal)
        {
            ["EastWater"] = new[] { "default.boat|-316.0|0.0|0.0", "st_peters.boat|-316.0|0.0|0.0", "opening.boat|-220.0|0.0|0.0" },
            ["Greywick"] = new[] { "default.boat|7.0|0.0|0.0", "default.foot|2.0|0.0|0.0" },
            ["NineMileCreek"] = new[] { "default.boat|86.2|83.1|0.0", "default.foot|86.2|87.35|0.0", "bar.foot|338.0|-197.7|0.0", "rpt_sea.boat|-366.0|-262.5|0.0", "rpt_sea.foot|-366.0|-190.0|0.0", "rpt_strand.foot|-366.0|-196.0|0.0" },
            ["StPeters"] = new[] { "default.boat|213.0|-0.266548|0.0", "default.foot|211.0|3.633452|0.0", "east_water.boat|316.0|40.0|0.0", "bar.foot|-344.0|0.0|0.0" },
            ["RicePoint"] = new[] { "default.boat|-53.285|-109.825|0.0", "default.foot|-56.0|-108.4|0.0", "nmc_sea.boat|366.0|-162.5|0.0", "nmc_sea.foot|366.0|-90.0|0.0", "nmc_strand.foot|366.0|-96.0|0.0", "nmc_route_91.foot|366.0|-42.0|0.0" },
            ["Cumberland"] = new[] { "default.boat|210.25|-128.25|0.0", "default.foot|210.25|-125.25|0.0", "nmc_route_91.foot|-186.0|-266.0|0.0", "nmc_north_beach.foot|67.0|-266.0|0.0", "nmc_bay.boat|240.0|-266.0|0.0", "nmc_bay.foot|45.25|-266.0|0.0" },
            ["Greybox"] = new[] { "default.boat|-2.5|-13.5|0.0", "default.foot|0.0|-10.5|0.0" },
            ["SouthWater"] = new[] { "default.boat|250.0|536.0|0.0", "rice_point.boat|-900.0|536.0|0.0", "st_peters.boat|250.0|536.0|0.0", "east_water.boat|950.0|536.0|0.0" },
        };


        [TestCase("EastWater")]
        [TestCase("Greywick")]
        [TestCase("NineMileCreek")]
        [TestCase("StPeters")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        [TestCase("Greybox")]
        [TestCase("SouthWater")]
        public void CommittedPassages_MatchTheDocumentedGraph(string name)
        {
            var actual = ReadEdges().Where(e => e.From == name).Select(e =>
                e.Id.Substring(name.Length + 1) + "|" + e.To + "|" + e.ArrivalKey + "|" + e.Mode + "|" + e.Enabled);
            Assert.That(actual.OrderBy(s => s, StringComparer.Ordinal), Is.EqualTo(ExpectedEdges[name].OrderBy(s => s, StringComparer.Ordinal)));
        }

        [TestCase("EastWater")]
        [TestCase("Greywick")]
        [TestCase("NineMileCreek")]
        [TestCase("StPeters")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        [TestCase("Greybox")]
        [TestCase("SouthWater")]
        public void NamedArrivals_MatchTheDocumentedKeys(string name)
            => Assert.That(Named(Anchor(name)).Select(m => m.Groups[1].Value).OrderBy(s => s, StringComparer.Ordinal),
                Is.EqualTo(ExpectedKeys[name].OrderBy(s => s, StringComparer.Ordinal)));

        [TestCase("EastWater")]
        [TestCase("Greywick")]
        [TestCase("NineMileCreek")]
        [TestCase("StPeters")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        [TestCase("Greybox")]
        [TestCase("SouthWater")]
        public void ArrivalTransforms_MatchTheDocumentedCoordinates(string name)
        {
            var scene = Scene(name);
            var anchor = Anchor(name);
            var points = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["default.boat"] = anchor.FieldRef("_arrivalPoint"), ["default.foot"] = anchor.FieldRef("_disembarkPoint")
            };
            foreach (var row in Named(anchor))
            {
                points[row.Groups[1].Value + ".boat"] = ParseRef(row.Groups[2].Value);
                points[row.Groups[1].Value + ".foot"] = ParseRef(row.Groups[3].Value);
            }
            Assert.That(points.Count(p => p.Value != 0), Is.EqualTo(ExpectedPoints[name].Length));
            foreach (string row in ExpectedPoints[name])
            {
                var cells = row.Split('|');
                Vector3 actual = WorldPosition(scene, points[cells[0]]);
                for (int axis = 0; axis < 3; axis++)
                    Assert.That(actual[axis], Is.EqualTo(float.Parse(cells[axis + 1], CultureInfo.InvariantCulture)).Within(0.0001f), name + "/" + cells[0]);
            }
        }

        private static float[] Numbers(string text) => Regex.Matches(text, @"[xyzw]: ([^,}]+)").Cast<Match>()
            .Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        private static Vector3 Vector(string text) { var n = Numbers(text); return new Vector3(n[0], n[1], n[2]); }
        private static Vector3 WorldPosition(SceneYaml scene, long id) => WorldPoint(scene, id, Vector3.zero);
        private static Vector3 WorldPoint(SceneYaml scene, long id, Vector3 local)
        {
            Vector3 result = local;
            for (long current = id; current != 0;)
            {
                var transform = scene.Require(current, "transform chain");
                var q = Numbers(transform.Field("m_LocalRotation"));
                result = Vector(transform.Field("m_LocalPosition")) + new Quaternion(q[0], q[1], q[2], q[3]) *
                    Vector3.Scale(Vector(transform.Field("m_LocalScale")), result);
                current = transform.FieldRef("m_Father");
            }
            return result;
        }

        [TestCase("EastWater")]
        [TestCase("Greywick")]
        [TestCase("NineMileCreek")]
        [TestCase("StPeters")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        [TestCase("Greybox")]
        [TestCase("SouthWater")]
        public void ArrivalCentres_AreOutsideEveryEnabledPassageBand(string name)
        {
            var scene = Scene(name); var anchor = Anchor(name);
            var points = new List<long> { anchor.FieldRef("_arrivalPoint"), anchor.FieldRef("_disembarkPoint") };
            foreach (var arrival in Named(anchor))
            {
                points.Add(ParseRef(arrival.Groups[2].Value));
                points.Add(ParseRef(arrival.Groups[3].Value));
            }
            int checkedPairs = 0;
            foreach (var passage in Scripts(name, "RegionPassage"))
            {
                var go = GameObject(scene, passage);
                if (!Active(scene, go) || passage.Field("m_Enabled") != "1") continue;
                var transform = scene.TransformOf(go);
                foreach (var box in scene.ComponentsOf(go).Where(d => d.ClassId == 61 && d.Field("m_Enabled") == "1"))
                {
                    var size = Numbers(box.Field("m_Size")); var offset = Numbers(box.Field("m_Offset"));
                    var corners = (from x in new[] { -1, 1 } from y in new[] { -1, 1 }
                        select WorldPoint(scene, transform.FileId, new Vector3(offset[0] + x * size[0] / 2, offset[1] + y * size[1] / 2, 0))).ToArray();
                    float minX = corners.Min(p => p.x), maxX = corners.Max(p => p.x);
                    float minY = corners.Min(p => p.y), maxY = corners.Max(p => p.y);
                    foreach (long point in points.Where(id => id != 0))
                    {
                        var p = WorldPosition(scene, point);
                        Assert.That(p.x < minX || p.x > maxX || p.y < minY || p.y > maxY, Is.True,
                            name + "/" + go.Field("m_Name") + " contains arrival centre " + point);
                        checkedPairs++;
                    }
                }
            }
            Assert.That(checkedPairs, Is.EqualTo(ExpectedPoints[name].Length *
                ReadEdges().Count(e => e.From == name && e.Enabled)), "Every enabled band and authored centre was checked.");
        }

        [Test]
        public void NormalStart_OnlyReachesTheDocumentedFiveBuiltScenes()
            => Assert.That(Graph().ReachableFrom("StPeters"), Is.EqualTo(new[] { "EastWater", "Greybox", "NineMileCreek", "RicePoint", "StPeters" }));

        [Test]
        public void MissingNamedArrivals_AreCumberlandsKeysTheShutRicePointRoadAndTheSouthWatersUnansweredDoors()
            => Assert.That(Graph().MissingNamedArrivals(), Is.EqualTo(new[] {
                "Cumberland/door.cmb_bay", "Cumberland/door.cmb_north_beach", "Cumberland/door.cmb_route_91", "RicePoint/door.rpt_route_91",
                "SouthWater/PassageToEastWater", "SouthWater/PassageToRicePoint", "SouthWater/PassageToStPeters" }));

        [Test]
        public void UnavailableTargets_AreTwoWestWaterDoorsAndTheUnwiredGreyboxExit()
            => Assert.That(Graph().UnavailableTargets(), Is.EqualTo(new[] {
                "Greybox/PassageToPortGreywick", "NineMileCreek/PassageToWestWater", "StPeters/PassageToWestWater" }));

        [Test]
        public void UnpairedLiveEdges_AreCumberlandTheTwoCoveEntriesAndTheSouthWatersThreeDoors()
            => Assert.That(Graph().UnpairedEdges(), Is.EqualTo(new[] {
                "Cumberland/door.cmb_bay", "Cumberland/door.cmb_north_beach", "Cumberland/door.cmb_route_91",
                "Greywick/PassageToCoddleCove", "NineMileCreek/PassageToCoddleCove",
                "SouthWater/PassageToEastWater", "SouthWater/PassageToRicePoint", "SouthWater/PassageToStPeters" }));

        [Test]
        public void TheSouthWater_IsNotReachableUntilANeighbourOpensItsHalf()
        {
            Assert.That(Graph().ReachableFrom("StPeters"), Does.Not.Contain("SouthWater"));
            Assert.That(Graph().ReachableFrom("SouthWater"), Is.EqualTo(new[] {
                "EastWater", "Greybox", "NineMileCreek", "RicePoint", "SouthWater", "StPeters" }));
        }

        [Test]
        public void BuildAndIdentityFacts_MatchTheAudit()
        {
            foreach (string scene in Scenes) Assert.That(BuildScenes().Contains(scene), Is.True, scene);
            Assert.That(BuildScenes().Contains("WestWater"), Is.False);
            Assert.That(File.Exists("Assets/_Project/Scenes/WestWater.unity"), Is.False);
            Assert.That(RegionScenes.Values.OrderBy(s => s, StringComparer.Ordinal), Is.EqualTo(new[] {
                "Cumberland", "EastWater", "Greybox", "NineMileCreek", "RicePoint", "SouthWater", "StPeters", "WestWater" }));
            Assert.That(Anchor("Greywick").Field("_regionId"), Is.EqualTo(SaveMigration.RenamedRegionFrom));
            Assert.That(Anchor("NineMileCreek").Field("_regionId"), Is.EqualTo(SaveMigration.RenamedRegionTo));
        }

        [Test]
        public void Opening_ReusesTheEastWaterAndHomeNamedArrivals()
        {
            const string path = "Assets/_Project/Data/Intro/ArrivalSea.asset";
            Assert.That(RegionScenes[GuidOf(AssetField(path, "SeaRegion"))], Is.EqualTo("EastWater"));
            Assert.That(RegionScenes[GuidOf(AssetField(path, "HomeRegion"))], Is.EqualTo("StPeters"));
            Assert.That(AssetField(path, "OpeningArrivalKey"), Is.EqualTo("opening"));
            Assert.That(AssetField(path, "HomeArrivalKey"), Is.EqualTo("east_water"));
            Assert.That(GuidOf(Scripts("StPeters", "ArrivalOpening").Single().Field("_seaOpening")), Is.EqualTo(GuidOf(File.ReadAllText(path + ".meta"))));
        }

        [Test]
        public void ReferencedPrefabs_CannotHideAdditionalTravelComponents()
        {
            var paths = Directory.GetFiles("Assets", "*.prefab.meta", SearchOption.AllDirectories)
                .ToDictionary(p => GuidOf(File.ReadAllText(p)), p => p.Substring(0, p.Length - 5), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>(Scenes.SelectMany(s => Scene(s).Docs)
                .Where(d => d.ClassId == 1001).Select(d => GuidOf(d.Field("m_SourcePrefab"))));
            while (pending.Count > 0)
            {
                string guid = pending.Dequeue();
                if (!seen.Add(guid)) continue;
                Assert.That(paths.ContainsKey(guid), Is.True, "Resolve prefab before accepting the graph: " + guid);
                var prefab = SceneYaml.Parse(File.ReadAllText(paths[guid]).Replace("\r\n", "\n"));
                foreach (var doc in prefab.Docs)
                {
                    Assert.That(IsScript(doc, "RegionPassage") || IsScript(doc, "RegionAnchor") || IsScript(doc, "RegionSceneLoader"),
                        Is.False, "A new prefab travel component needs inclusion in the audit: " + paths[guid]);
                    if (doc.ClassId == 1001) pending.Enqueue(GuidOf(doc.Field("m_SourcePrefab")));
                }
            }
            Assert.That(seen.Count, Is.EqualTo(13), "Audit every referenced prefab, including nested sources.");
        }

        [Test]
        public void GraphResults_AreIndependentOfInputEnumerationOrder()
        {
            var nodes = ReadNodes(); var edges = ReadEdges();
            var first = new RegionSeamGraph(nodes, edges);
            nodes.Reverse(); edges.Reverse();
            var second = new RegionSeamGraph(nodes, edges);
            Assert.That(second.ReachableFrom("StPeters"), Is.EqualTo(first.ReachableFrom("StPeters")));
            Assert.That(second.MissingNamedArrivals(), Is.EqualTo(first.MissingNamedArrivals()));
            Assert.That(second.UnavailableTargets(), Is.EqualTo(first.UnavailableTargets()));
            Assert.That(second.UnpairedEdges(), Is.EqualTo(first.UnpairedEdges()));
        }

        [Test]
        public void MissingArrival_StillTransfersStructurallyButIsReported()
        {
            var nodes = new[] { new RegionSeamNode("a", true, new[] { "BACK" }), new RegionSeamNode("b", true, Array.Empty<string>()) };
            var edges = new[] { new RegionSeamEdge("out", "a", "b", "missing", RegionSeamMode.Foot, true),
                new RegionSeamEdge("back", "b", "a", "back", RegionSeamMode.Foot, true) };
            var graph = new RegionSeamGraph(nodes, edges);
            Assert.That(graph.ReachableFrom("a"), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(graph.MissingNamedArrivals(), Is.EqualTo(new[] { "out" }));
            Assert.That(graph.UnpairedEdges(), Is.Empty);
        }

        [Test]
        public void ClosedAndUnavailableTargets_DoNotBecomeReachable()
        {
            var nodes = new[] { new RegionSeamNode("a", true, null), new RegionSeamNode("b", true, null), new RegionSeamNode("c", false, null) };
            var graph = new RegionSeamGraph(nodes, new[] {
                new RegionSeamEdge("closed", "a", "b", "", RegionSeamMode.Road, false),
                new RegionSeamEdge("unbuilt", "a", "c", "", RegionSeamMode.Sea, true) });
            Assert.That(graph.ReachableFrom("a"), Is.EqualTo(new[] { "a" }));
            Assert.That(graph.ReachableFrom("c"), Is.Empty);
            Assert.That(graph.ReachableFrom("unknown"), Is.Empty);
            Assert.That(graph.UnavailableTargets(), Is.EqualTo(new[] { "unbuilt" }));
        }
    }
}
