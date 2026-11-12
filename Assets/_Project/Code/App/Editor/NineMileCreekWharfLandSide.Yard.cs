#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        public const string YardStep = "16-wharf-boat-yard", YardRetireStep = "17-retire-old-yard";
        public static void YardDryRunFromCommandLine() => NineMileCreekLayerRefresh.Run(false);
        public static void YardApplyFromCommandLine() => NineMileCreekLayerRefresh.Run(true);
        public static void YardRepairAndReviewFromCommandLine() => ReviewYardRepair("Evidence~/nmc-h6/pr3b");
        public static void YardFinalRepairAndReviewFromCommandLine() => ReviewYardRepair("Evidence~/nmc-h6/pr3m");
        public static void YardRunwayRepairAndReviewFromCommandLine() => ReviewYardRepair("Evidence~/nmc-h6/pr3p");
        static void ReviewYardRepair(string evidence)
        {
            string ready = evidence + "/review-ready.txt", proceed = evidence + "/review-proceed.txt";
            if (File.Exists(ready) || File.Exists(proceed)) throw new Refusal("Use fresh yard repair review files.");
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(NineMileCreekLayerRefresh.ScenePath).isLoaded)
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
            var plans = NineMileCreekLayerRefresh.Run(false);
            File.WriteAllText(evidence + "/dry-run.yaml", string.Join("\n", plans.SelectMany(s => s.Patches).Select(p => p.ToYaml())));
            File.WriteAllText(ready, DateTime.UtcNow.ToString("o"));
            var deadline = DateTime.UtcNow.AddMinutes(5);
            while (!File.Exists(proceed) && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(200);
            if (!File.Exists(proceed)) throw new Refusal("The yard repair dry run was not reviewed.");
            NineMileCreekLayerRefresh.Run(true);
            byte[] first = File.ReadAllBytes(NineMileCreekLayerRefresh.ScenePath);
            NineMileCreekLayerRefresh.Run(true);
            byte[] second = File.ReadAllBytes(NineMileCreekLayerRefresh.ScenePath);
            using var hash = System.Security.Cryptography.SHA256.Create();
            string Sum(byte[] bytes) => string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2")));
            File.WriteAllLines(evidence + "/second-apply-sha256.txt", new[] { Sum(first), Sum(second) });
            if (!first.SequenceEqual(second)) throw new Refusal("The yard repair's second apply changed a byte.");
            var stores = StoreWiringPatch.Run(false);
            if (stores.Any(p => Regex.IsMatch(p.ToYaml(), @"(?m)^- fileID:")))
                throw new Refusal("The yard repair needs an additional store operation reviewed.");
            StoreWiringPatch.Run(true);
        }
        public static void YardBakeAndDryRunFromCommandLine()
        {
            NmcShipyardKit.BakeFromCommandLine();
            NineMileCreekLayerRefresh.Run(false);
        }
        public static void YardApplyTwiceAndStoreFromCommandLine()
        {
            const string evidence = "Evidence~/nmc-h6/pr3";
            NineMileCreekLayerRefresh.Run(true);
            byte[] before = File.ReadAllBytes(NineMileCreekLayerRefresh.ScenePath);
            NineMileCreekLayerRefresh.Run(true);
            byte[] after = File.ReadAllBytes(NineMileCreekLayerRefresh.ScenePath);
            using var hash = System.Security.Cryptography.SHA256.Create();
            string Sum(byte[] bytes) => string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2")));
            File.WriteAllLines(evidence + "/second-apply-sha256.txt", new[] { Sum(before), Sum(after) });
            if (!before.SequenceEqual(after)) throw new Refusal("The yard's second apply changed the scene.");
            StoreWiringPatch.Run(false);
            File.WriteAllText(evidence + "/store-review-ready.txt", "Review the named store operations before proceeding.");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!File.Exists(evidence + "/store-review-proceed.txt"))
            {
                if (clock.Elapsed.TotalMinutes > 5) throw new Refusal("The yard store patch review did not finish.");
                System.Threading.Thread.Sleep(250);
            }
            StoreWiringPatch.Run(true);
        }
        public static IEnumerable<(string step, Func<SceneYaml, LayerPatch> plan)> YardPlanners()
        {
            yield return (YardStep, PlaceYard);
            yield return (YardStep, PlaceYardCatalogue);
            yield return (YardStep, PlaceYardDoryTrade);
            yield return (YardStep, PlaceYardOutboardTrade);
            yield return (YardStep, MoveDamagedDory);
            yield return (YardStep, MoveYardSeller);
            yield return (YardStep, OpenYardRunways);
            yield return (YardRetireStep, s => RetireYardRoot(s, "ShipwrightDoryYard", 1499338001));
            yield return (YardRetireStep, s => RetireYardRoot(s, "ShipwrightShed", 2028377143));
            yield return (YardRetireStep, s => RetireYardRoot(s, "UsedOutboardSeller", 1268888616));
            yield return (YardRetireStep, s => RemoveChildren(s, "Yards", name => name == "BoatShed", 1, YardRetireStep));
            yield return (YardRetireStep, RetireOldShipyardLot);
            yield return (VendorAnchorsStep, RegisterVendorAnchors);
        }
        public static LayerPatch RetireOldShipyardLot(SceneYaml scene)
        {
            Doc old = scene.Docs.SingleOrDefault(d => d.ClassId == 1 && d.Field("m_Name") == "Shipyard_smallYard");
            if (old != null)
            {
                long[] expected = { 2083176806, 2083176807, 2083176808, 2083176809, 2083176810 };
                if (!scene.SubtreeOf(old.FileId).OrderBy(id => id).SequenceEqual(expected) ||
                    scene.TransformOf(old).FieldRef("m_Father") != 1152889076)
                    throw new Refusal("The retired smallYard differs from Ruling 26's five named documents.");
            }
            return RemoveChildren(scene, NineMileCreekLots.RootName, name => name == "Shipyard_smallYard", 1, YardRetireStep);
        }
        static LayerPatch RetireYardRoot(SceneYaml scene, string name, long id) => Retire(scene, YardRetireStep, name, id);

        public static LayerPatch PlaceYard(SceneYaml scene)
        {
            const string name = NineMileCreekWharfYard.RootName;
            Doc current = ExistingRoot(scene, name);
            var ids = new IdAllocator(scene, current == null ? new HashSet<long>() : scene.SubtreeOf(current.FileId));
            long go = Allocate(ids, name, "go"), tr = Allocate(ids, name, "tr");
            var wanted = new List<(string name, string text)>(); var children = new List<long>();
            var parsed = DeckSidecarJson.Parse(File.ReadAllText(NmcShipyardKit.TodayPath));
            var rawPieces = DeckSidecarJson.AsArray(DeckSidecarJson.Member(parsed, "pieces"));
            var material = new EditorKeySceneAssets().SpriteMaterial;
            foreach (var piece in NineMileCreekWharfYard.Read().pieces)
            {
                SetPieceDef def = NineMileCreekWharfYard.Definition(piece.key);
                if (def.Id != piece.id) throw new Refusal("The yard Def differs from its source: " + piece.key);
                var at = new Vector2(piece.gameplay.placement.at[0], piece.gameplay.placement.at[1]);
                long pg = Allocate(ids, name, piece.key + "/go"), pt = Allocate(ids, name, piece.key + "/tr");
                long sr = Allocate(ids, name, piece.key + "/sprite"), binding = Allocate(ids, name, piece.key + "/source");
                var components = new List<long> { pt, sr, binding };
                SpriteRef sprite = SpriteAsset(def.SpriteFor(0));
                int order = def.Layer == "floor" ? SortingBands.DecorFloor : YSortSprite.OrderFor(at.y, SortingBands.DecorBase,
                    SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
                wanted.Add((piece.key + "/sprite", SpriteRendererYaml(sr, pg, material, sprite, order)));
                wanted.Add((piece.key + "/source", MonoBehaviourYaml(binding, pg,
                    Script("Assets/_Project/Code/Art/SetPiecePlacement.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.SetPiecePlacement"),
                    new[] { "  _def: " + Asset("Assets/_Project/Data/SetPieces/SetPiece_" + piece.key + ".asset").Yaml,
                        "  _definitionId: " + piece.id, "  _placementId: " + NineMileCreekWharfYard.RowId, "  _rigDir: 0" })));
                var walls = WallsOf(def.Colliders, 0, piece.id);
                if (walls.Count > 0)
                {
                    long c = Allocate(ids, name, piece.key + "/walls"); components.Add(c);
                    wanted.Add((piece.key + "/walls", PolygonColliderYaml(c, pg, walls)));
                }
                object raw = rawPieces.Single(p => DeckSidecarJson.String(DeckSidecarJson.Member(p, "key")) == piece.key);
                var walks = DeckSidecarJson.AsArray(DeckSidecarJson.Member(DeckSidecarJson.Member(raw, "gameplay"), "walkSurfaces"));
                int index = 0;
                foreach (object walk in walks)
                {
                    var poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(walk, "poly"));
                    var points = poly.Select(q => DeckSidecarJson.AsArray(q)).Select(q => new Vector2(
                        DeckSidecarJson.Float(q[0]), DeckSidecarJson.Float(q[1])) + at).ToArray();
                    Rect rect = Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
                    // Each delivered walk is rectangular, on cardinal axes. Refuse a future shape this component cannot express.
                    if (points.Length != 4 || points.Any(p => (Math.Abs(p.x - rect.xMin) > 0.001f && Math.Abs(p.x - rect.xMax) > 0.001f) ||
                        (Math.Abs(p.y - rect.yMin) > 0.001f && Math.Abs(p.y - rect.yMax) > 0.001f)))
                        throw new Refusal(piece.id + ": this walk is not a StandablePlatform rectangle.");
                    // StandablePlatform is single-instance; each walk gets its own child.
                    string key = piece.key + "/walk" + index++;
                    long wg = Allocate(ids, name, key + "/go"), wt = Allocate(ids, name, key + "/tr"), ws = Allocate(ids, name, key + "/surface");
                    children.Add(wt);
                    wanted.Add((key + "/go", GameObjectYaml(wg, key, new[] { wt, ws })));
                    wanted.Add((key + "/tr", TransformYaml(wt, wg, Vector2.zero, Array.Empty<long>(), tr)));
                    wanted.Add((key + "/surface", MonoBehaviourYaml(ws, wg,
                        Script("Assets/_Project/Code/World/StandablePlatform.cs", "HiddenHarbours.World", "HiddenHarbours.World.StandablePlatform"),
                        new[] { "  _id: " + piece.id + ".walk", "  _footprint:", "    serializedVersion: 2",
                            "    x: " + F(rect.x), "    y: " + F(rect.y), "    width: " + F(rect.width), "    height: " + F(rect.height),
                            "  _deckElevation: " + F(piece.gameplay.placement.elevation + DeckSidecarJson.Float(DeckSidecarJson.Member(walk, "z"))) })));
                }
                wanted.Add((piece.key + "/go", GameObjectYaml(pg, piece.id, components)));
                wanted.Add((piece.key + "/tr", TransformYaml(pt, pg, at, Array.Empty<long>(), tr))); children.Add(pt);
            }
            wanted.Insert(0, (name + "/go", GameObjectYaml(go, name, new[] { tr })));
            wanted.Insert(1, (name + "/tr", TransformYaml(tr, go, Vector2.zero, children, 0)));
            return WriteRoot(scene, name, go, wanted, YardStep);
        }

        static LayerPatch CloneYardCounter(SceneYaml scene, string name, string oldName, Vector3 at, string talkNpc, params string[] scripts)
        {
            Doc current = ExistingRoot(scene, name), template = current ?? scene.RootNamed(oldName);
            var ids = new IdAllocator(scene, current == null ? new HashSet<long>() : scene.SubtreeOf(current.FileId));
            long go = Allocate(ids, name, "go"), tr = Allocate(ids, name, "tr");
            var monos = scene.ComponentsOf(template).Where(d => scripts.Any(d.IsScript)).ToArray();
            if (monos.Length == 0) throw new Refusal(name + ": no vendor template.");
            var map = monos.ToDictionary(d => d.FileId, d => Allocate(ids, name, d.Field("m_EditorClassIdentifier") + "/" + d.Field("_offer")));
            map[template.FileId] = go; map[scene.TransformOf(template).FileId] = tr;
            var talk = talkNpc == null ? default : TalkAnchor(ids, name, go, talkNpc);
            var components = new[] { tr }.Concat(monos.Select(d => map[d.FileId])).ToList();
            if (talkNpc != null) components.Add(talk.id);
            var wanted = new List<(string name, string text)>
            {
                (name + "/go", GameObjectYaml(go, name, components)),
                (name + "/tr", TransformYaml(tr, go, at, Array.Empty<long>(), 0)),
            };
            foreach (Doc mono in monos)
            {
                string text = Regex.Replace(mono.Text, @"^--- !u!114 &-?\d+", "--- !u!114 &" + map[mono.FileId]);
                text = Regex.Replace(text, @"\{fileID: (-?\d+)\}", m => map.TryGetValue(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), out long target)
                    ? "{fileID: " + target + "}" : m.Value);
                wanted.Add((name + "/" + map[mono.FileId], text));
            }
            if (talkNpc != null) wanted.Add((name + "/talk", talk.text));
            return WriteRoot(scene, name, go, wanted, YardStep, besideRoot: talkNpc != null);
        }
        public static LayerPatch PlaceYardCatalogue(SceneYaml s) => CloneYardCounter(s, NineMileCreekWharfYard.CatalogueName, "ShipwrightShed",
            NineMileCreekWharfYard.Door, YardNpcPath, "HiddenHarbours.Economy.Shipwright", "HiddenHarbours.Economy.PotShop");
        static LayerPatch PlaceYardDoryTrade(SceneYaml s) => CloneYardCounter(s, NineMileCreekWharfYard.DoryTradeName, "ShipwrightDoryYard",
            NineMileCreekWharfYard.Door, null, "HiddenHarbours.Economy.Shipwright");
        static LayerPatch PlaceYardOutboardTrade(SceneYaml s) => CloneYardCounter(s, NineMileCreekWharfYard.OutboardTradeName, "UsedOutboardSeller",
            NineMileCreekBuilder.HectorsBarrelPos, null, "HiddenHarbours.Economy.Shipwright");
        static LayerPatch MoveDamagedDory(SceneYaml scene) => MoveYardObject(scene, NineMileCreekDory.RootName, NineMileCreekWharfYard.DamagedDory);
        static LayerPatch MoveYardSeller(SceneYaml scene) => MoveYardObject(scene, NineMileCreekTrips.OutboardManAssetName, NineMileCreekBuilder.HectorsBarrelPos);
        static LayerPatch MoveYardObject(SceneYaml scene, string name, Vector3 at)
        {
            Doc go = scene.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == name);
            var patch = new LayerPatch(YardStep, name, go.FileId); Doc tr = scene.TransformOf(go);
            patch.Edit(tr, SetField(tr.Text, "m_LocalPosition", Vec3Yaml(at.x, at.y, at.z)), name + "/yard position");
            return patch.Seal(scene);
        }
        /// <summary>The two delivered pier spans, with ruling 33's half-metre walking clearance.</summary>
        public static IReadOnlyList<Vector2> YardRunwayOpenings()
        {
            var pieces = NineMileCreekWharfYard.Read().pieces;
            float clearance = NineMileCreekGroundPlan.RunwayClearance;
            return new[] { "pierW", "pierE" }.Select(key =>
            {
                var p = pieces.Single(p => p.key == key).gameplay;
                return new Vector2(p.placement.at[0] + p.bounds.x[0] - clearance,
                                   p.placement.at[0] + p.bounds.x[1] + clearance);
            }).ToArray();
        }
        static LayerPatch OpenYardRunways(SceneYaml scene)
        {
            Doc root = scene.RootNamed(NineMileCreekWharf.RootName);
            Doc walls = scene.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == NineMileCreekWalls.RootName && scene.SubtreeOf(root.FileId).Contains(d.FileId));
            Doc colliderRoot = scene.ChildNamed(scene.TransformOf(walls), PropertyBoundary.CollidersChildName);
            Doc collider = scene.ComponentsOf(colliderRoot).First(d => d.ClassId == 60);
            var patch = new LayerPatch(YardStep, NineMileCreekWharf.RootName + "/" + NineMileCreekWalls.RootName, walls.FileId);
            float half = NineMileCreekGroundPlan.BlockerHalfThickness;
            var paths = new List<Vector2[]>();
            var runs = NineMileCreekGroundSteps.QuayRuns(half);
            paths.Add(NineMileCreekGroundSteps.Quad(runs[0][2], runs[0][3], half));
            float start = runs[1][0].x;
            foreach (Vector2 opening in YardRunwayOpenings())
            {
                paths.Add(NineMileCreekGroundSteps.Quad(new Vector2(start, 86),
                    new Vector2(opening.x - half, 86), half));
                start = opening.y + half;
            }
            paths.Add(NineMileCreekGroundSteps.Quad(new Vector2(start, 86), runs[1][1], half));
            string polygon = PolygonColliderYaml(collider.FileId, collider.FieldRef("m_GameObject"), paths);
            string points = Regex.Match(polygon, @"(?ms)^  m_Points:.*?(?=^  m_UseDelaunayMesh:)").Value;
            string text = Regex.Replace(collider.Text, @"(?ms)^  m_Points:.*?(?=^  m_UseDelaunayMesh:)", _ => points);
            patch.Edit(collider, text, "QuayWalls/north paths open only at the yard's two runways");
            return patch.Seal(scene);
        }
    }
}
#endif
