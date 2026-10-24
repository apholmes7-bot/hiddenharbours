#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using G = HiddenHarbours.App.Editor.NineMileCreekGroundPlan;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>12: Nine Mile Creek's roads in kit v4 (NMC H4).</b> Kit v4's road floor draws the network
    /// (<see cref="NineMileCreekRoadNetwork"/>), so the ground's splat gains the streamer that feeds it; 06's one
    /// platform gives way to the network's three plain standable decks (Q7), each an instance of its own prefab under
    /// a new <see cref="G.BridgesRootName"/> root; and the planting that stands on a v4 carriageway, or in a hedge
    /// clearance the network keeps, is deleted by name. Step 06, re-lined, paints only what kit v3 keeps
    /// (<see cref="G.PaveKeptV3"/>). Each patch is one-root, every kept document keeps its id, and planning 06 and 12
    /// again on what they write is empty.
    ///
    /// <para><b>Nothing here is dressing.</b> The decks carry no sprite (the road floor draws them) and the gates are
    /// held, so only their clearance is laid.</para>
    /// </summary>
    public static partial class NineMileCreekGroundSteps
    {
        public const string RoadsV4Step = "12-roads-v4-g3";
        public const string StreamerScriptPath = "Assets/_Project/Code/Art/RoadFloorStreamer.cs";
        const string StreamerClass = "HiddenHarbours.Art.RoadFloorStreamer";
        const string PlatformClass = "HiddenHarbours.World.StandablePlatform";
        const int GameObjectClassId = 1, TransformClassId = 4;

        /// <summary>The planting roots step 12 clears, in the order it plans them.</summary>
        public static readonly string[] ClearedPlantingRoots =
        {
            NineMileCreekFieldPlanter.HedgeRootName, NineMileCreekFieldPlanter.TreeRootName,
            NineMileCreekFieldPlanter.UnderstoreyRootName, NineMileCreekFieldPlanter.MarshRootName,
        };

        static IEnumerable<(string step, Func<SceneYaml, LayerPatch> plan)> RoadsV4Planners(Inputs inputs)
        {
            NineMileCreekRoadNetwork.Network net = NineMileCreekRoadNetwork.Load(inputs.ProjectRoot);
            yield return (RoadsV4Step, s => RoadFloor(s, inputs));
            yield return (RoadsV4Step, RetiredBridge);
            yield return (RoadsV4Step, s => Bridges(s, inputs, net));
            foreach (string root in ClearedPlantingRoots)
                yield return (RoadsV4Step, s => Clearing(s, root, net));
        }

        // =====================================================================================================
        //  12: the road floor's streamer, on the ground's splat
        // =====================================================================================================

        /// <summary>The splat gains its <see cref="RoadFloorStreamer"/> beside its surface: the bake's folder and the
        /// pool's defaults, the builder's own. A streamer already there is written to the same fields on its own id.</summary>
        static LayerPatch RoadFloor(SceneYaml scene, Inputs inputs)
        {
            const string step = RoadsV4Step;
            Doc go = scene.RootNamed(SplatRootName);
            var patch = new LayerPatch(step, SplatRootName, go.FileId);
            List<Doc> comps = scene.ComponentsOf(go);
            Doc surface = One(comps.Where(d => d.IsScript("HiddenHarbours.Art.TerrainSplatSurface")), step, "the splat surface");
            List<Doc> found = comps.Where(d => d.IsScript(StreamerClass)).ToList();
            if (found.Count > 1) throw new Refusal($"{step}: {found.Count} road floor streamers on the splat.");
            Doc streamer = found.FirstOrDefault();
            long id = streamer?.FileId ?? new IdAllocator(scene).Next(step + "/" + SplatRootName + "/RoadFloorStreamer");
            var script = new ObjRef(11500000, inputs.Guid(StreamerScriptPath), 3);
            Put(patch, streamer, Streamer(surface, id, script), SplatRootName + "/RoadFloorStreamer");
            if (streamer == null)
            {
                patch.Edit(go, WithListed(go.Text, "m_Component", id, "component: "), SplatRootName + "/components");
                patch.Note(SplatRootName, "road floor streamer",
                    $"{RoadFloorStreamer.DefaultFolder}, {RoadFloorStreamer.DefaultCapacity} slots, " +
                    $"{RoadFloorStreamer.DefaultUploadsPerFrame} uploads a frame, {RoadFloorStreamer.DefaultDecodesInFlight} decodes, " +
                    $"a ring of {RoadFloorStreamer.DefaultRingChunks}");
            }
            return patch.Seal(scene);
        }

        /// <summary>The streamer's document: the surface's own MonoBehaviour head (the same GameObject, flags and
        /// layout) on the streamer's id and script, then its fields in declaration order.</summary>
        static string Streamer(Doc surface, long id, ObjRef script)
        {
            int at = Array.FindIndex(surface.Lines, l => l.StartsWith("  m_EditorClassIdentifier:", StringComparison.Ordinal));
            if (at < 0) throw new Refusal($"{RoadsV4Step}: the splat surface &{surface.FileId} has no class identifier.");
            string head = Set(Header(string.Join("\n", surface.Lines.Take(at)), id), "m_Script", script.Yaml);
            return string.Join("\n",
                head,
                "  m_EditorClassIdentifier: HiddenHarbours.Art::" + StreamerClass,
                $"  _surface: {Ref(surface.FileId)}",
                $"  _folder: {RoadFloorStreamer.DefaultFolder}",
                $"  _capacity: {RoadFloorStreamer.DefaultCapacity.ToString(CultureInfo.InvariantCulture)}",
                $"  _uploadsPerFrame: {RoadFloorStreamer.DefaultUploadsPerFrame.ToString(CultureInfo.InvariantCulture)}",
                $"  _decodesInFlight: {RoadFloorStreamer.DefaultDecodesInFlight.ToString(CultureInfo.InvariantCulture)}",
                $"  _ringChunks: {RoadFloorStreamer.DefaultRingChunks.ToString(CultureInfo.InvariantCulture)}",
                "  _camera: {fileID: 0}");
        }

        // =====================================================================================================
        //  12: the bridges
        // =====================================================================================================

        /// <summary>06's platform under the roads root is deleted: its GameObject, transform and platform, each named,
        /// and its line in the root's children. The network's deck replaces it under the bridges' root.</summary>
        static LayerPatch RetiredBridge(SceneYaml scene)
        {
            const string step = RoadsV4Step;
            Doc root = scene.RootNamed(NineMileCreekRoadPainter.RootName);
            Doc rootTr = scene.TransformOf(root);
            var patch = new LayerPatch(step, NineMileCreekRoadPainter.RootName, root.FileId);
            Doc old = Child(scene, rootTr, G.BridgeName, step);
            if (old != null)
            {
                Doc oldTr = scene.TransformOf(old);
                if (oldTr.FieldRefs("m_Children").Count != 0) throw new Refusal($"{step}: 06's {G.BridgeName} has children.");
                List<Doc> comps = scene.ComponentsOf(old);
                if (comps.Count != 2 || comps.Count(c => c.IsScript(PlatformClass)) != 1)
                    throw new Refusal($"{step}: 06's {G.BridgeName} is not a transform and a platform; hand work is not deleted.");
                patch.Edit(rootTr, SetList(rootTr.Text, "m_Children", rootTr.FieldRefs("m_Children").Where(c => c != oldTr.FileId)),
                    NineMileCreekRoadPainter.RootName + "/children");
                foreach (Doc c in comps)
                    patch.Delete(c, $"{G.BridgeName}/{(c.FileId == oldTr.FileId ? "Transform" : "StandablePlatform")} (06's, retired)");
                patch.Delete(old, $"{G.BridgeName} (06's GameObject, retired)");
                patch.Note(G.BridgeName, "retired", $"06's platform under {NineMileCreekRoadPainter.RootName}; the network's deck " +
                                                     $"stands under {G.BridgesRootName}");
            }
            return patch.Seal(scene);
        }

        /// <summary>
        /// The network's three decks, each an instance of its prefab (<see cref="G.BridgePrefabPath"/>) at its
        /// footprint's centre, under the <see cref="G.BridgesRootName"/> root, which the patch adds (written as the
        /// roads root is) when the scene has none. An instance already there is re-placed on its own ids. A prefab whose
        /// deck is not the network's is refused: the prefab is generated from the network and must match it.
        /// </summary>
        static LayerPatch Bridges(SceneYaml scene, Inputs inputs, NineMileCreekRoadNetwork.Network net)
        {
            const string step = RoadsV4Step;
            Doc roads = scene.RootNamed(NineMileCreekRoadPainter.RootName);
            Doc roadsTr = scene.TransformOf(roads);
            RequireIdentity(roadsTr, step);
            Doc root = RootOrNull(scene, G.BridgesRootName);
            Doc rootTr = root != null ? scene.TransformOf(root) : null;
            if (rootTr != null) RequireIdentity(rootTr, step);
            var ids = new IdAllocator(scene);
            long go = root?.FileId ?? ids.Next(step + "/" + G.BridgesRootName + "/GameObject");
            long tr = rootTr?.FileId ?? ids.Next(step + "/" + G.BridgesRootName + "/Transform");
            var patch = new LayerPatch(step, G.BridgesRootName, go);

            List<long> standing = rootTr?.FieldRefs("m_Children") ?? new List<long>();
            var children = new List<long>(standing);
            foreach (NineMileCreekRoadNetwork.Bridge bridge in net.Bridges)
            {
                BridgePrefab prefab = BridgePrefab.Read(inputs, bridge.Prefab);
                if (prefab.Id != bridge.Id || prefab.Footprint != bridge.Footprint || prefab.Deck != bridge.Deck)
                    throw new Refusal($"{step}: {prefab.Path} is not {bridge.Id}'s deck as the network lays it; regenerate it.");
                Doc pi = null, piTr = null;
                foreach (long c in standing)
                {
                    Doc t = scene.Require(c, "a bridge's transform");
                    if (!t.Stripped || ObjRef.Parse(t.Field("m_CorrespondingSourceObject")).Guid != prefab.Guid) continue;
                    if (pi != null) throw new Refusal($"{step}: two instances of {prefab.Path}.");
                    piTr = t;
                    pi = scene.Require(t.FieldRef("m_PrefabInstance"), "a bridge's prefab instance");
                }
                long piId = pi?.FileId ?? ids.Next($"{step}/{bridge.Id}/PrefabInstance");
                long piTrId = piTr?.FileId ?? ids.Next($"{step}/{bridge.Id}/Transform");
                Put(patch, pi, BridgeInstance(piId, tr, prefab, bridge.Footprint.center), $"{bridge.Prefab} (PrefabInstance)");
                Put(patch, piTr, StrippedTransform(piTrId, piId, prefab), $"{bridge.Prefab}/Transform (stripped)");
                if (!children.Contains(piTrId)) children.Add(piTrId);
                if (pi == null)
                    patch.Note(bridge.Prefab, "a plain standable deck",
                        $"{bridge.Id}, {bridge.On} over {bridge.Over}: footprint {Box(bridge.Footprint)}, deck {N(bridge.Deck)} m");
            }

            Put(patch, root, NamedGameObject(roads.Text, go, G.BridgesRootName, tr), G.BridgesRootName);
            string rootTrText = SetList(Set(Header(roadsTr.Text, tr), "m_GameObject", Ref(go)), "m_Children", children);
            Put(patch, rootTr, rootTrText, G.BridgesRootName + "/Transform");
            if (root == null)
            {
                Doc list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, tr), $"the scene's SceneRoots list ('{G.BridgesRootName}' joins it)");
                patch.Note(G.BridgesRootName, "a new root", $"{net.Bridges.Count} decks; joins the scene's SceneRoots list");
            }
            return patch.Seal(scene);
        }

        /// <summary>One bridge prefab as its file holds it: the GUID, the root's documents, and the deck.</summary>
        sealed class BridgePrefab
        {
            public string Path, Name, Guid, Id;
            public long GameObject, Transform;
            public Rect Footprint;
            public float Deck;

            public static BridgePrefab Read(Inputs inputs, string name)
            {
                string path = G.BridgePrefabPath(name);
                string file = System.IO.Path.Combine(inputs.ProjectRoot, path);
                if (!File.Exists(file)) throw new Refusal($"{RoadsV4Step}: {path} is not on disk.");
                string text = File.ReadAllText(file).Replace("\r\n", "\n");
                int start = text.IndexOf("--- !u!", StringComparison.Ordinal);
                if (start < 0) throw new Refusal($"{RoadsV4Step}: {path} holds no documents.");
                List<Doc> docs = Regex.Split(text.Substring(start).TrimEnd('\n'), @"\n(?=--- !u!)").Select(t => new Doc(t)).ToList();
                Doc go = One(docs.Where(d => d.ClassId == GameObjectClassId), RoadsV4Step, path + "'s GameObject");
                Doc tr = One(docs.Where(d => d.ClassId == TransformClassId), RoadsV4Step, path + "'s Transform");
                Doc platform = One(docs.Where(d => d.IsScript(PlatformClass)), RoadsV4Step, path + "'s platform");
                if (docs.Count != 3 || go.Field("m_Name") != name || tr.FieldRef("m_Father") != 0)
                    throw new Refusal($"{RoadsV4Step}: {path} is not one plain GameObject named {name} with its platform.");
                return new BridgePrefab
                {
                    Path = path, Name = name, Guid = inputs.Guid(path), Id = platform.Field("_id"),
                    GameObject = go.FileId, Transform = tr.FileId,
                    Footprint = ReadRect(platform, "_footprint"), Deck = ParseFloat(platform.Field("_deckElevation")),
                };
            }
        }

        /// <summary>A bridge's instance as the editor writes one dropped under a root at the origin: its name, and the
        /// ten records an instance's root transform carries, ordered by target.</summary>
        static string BridgeInstance(long id, long parent, BridgePrefab prefab, Vector2 at)
        {
            var records = new List<(long target, string path, string value)>
            {
                (prefab.GameObject, "m_Name", prefab.Name),
                (prefab.Transform, "m_LocalPosition.x", N(at.x)), (prefab.Transform, "m_LocalPosition.y", N(at.y)),
                (prefab.Transform, "m_LocalPosition.z", "0"),
                (prefab.Transform, "m_LocalRotation.w", "1"), (prefab.Transform, "m_LocalRotation.x", "0"),
                (prefab.Transform, "m_LocalRotation.y", "0"), (prefab.Transform, "m_LocalRotation.z", "0"),
                (prefab.Transform, "m_LocalEulerAnglesHint.x", "0"), (prefab.Transform, "m_LocalEulerAnglesHint.y", "0"),
                (prefab.Transform, "m_LocalEulerAnglesHint.z", "0"),
            };
            var lines = new List<string>
            {
                $"--- !u!1001 &{id.ToString(CultureInfo.InvariantCulture)}",
                "PrefabInstance:",
                "  m_ObjectHideFlags: 0",
                "  serializedVersion: 2",
                "  m_Modification:",
                "    serializedVersion: 3",
                $"    m_TransformParent: {Ref(parent)}",
                "    m_Modifications:",
            };
            foreach (var r in records.OrderBy(r => r.target))
            {
                lines.Add($"    - target: {{fileID: {r.target.ToString(CultureInfo.InvariantCulture)}, guid: {prefab.Guid}, type: 3}}");
                lines.Add("      propertyPath: " + r.path);
                lines.Add("      value: " + r.value);
                lines.Add("      objectReference: {fileID: 0}");
            }
            lines.Add("    m_RemovedComponents: []");
            lines.Add("    m_RemovedGameObjects: []");
            lines.Add("    m_AddedGameObjects: []");
            lines.Add("    m_AddedComponents: []");
            lines.Add($"  m_SourcePrefab: {{fileID: 100100000, guid: {prefab.Guid}, type: 3}}");
            return string.Join("\n", lines);
        }

        static string StrippedTransform(long id, long instance, BridgePrefab prefab) => string.Join("\n",
            $"--- !u!4 &{id.ToString(CultureInfo.InvariantCulture)} stripped",
            "Transform:",
            $"  m_CorrespondingSourceObject: {{fileID: {prefab.Transform.ToString(CultureInfo.InvariantCulture)}, guid: {prefab.Guid}, type: 3}}",
            $"  m_PrefabInstance: {Ref(instance)}",
            "  m_PrefabAsset: {fileID: 0}");

        static Doc RootOrNull(SceneYaml scene, string name)
        {
            var hits = scene.Docs.Where(d => d.ClassId == GameObjectClassId && !d.Stripped && d.Field("m_Name") == name &&
                                             scene.TransformOf(d).FieldRef("m_Father") == 0).ToList();
            if (hits.Count > 1) throw new Refusal($"{RoadsV4Step}: {hits.Count} roots named '{name}'.");
            return hits.FirstOrDefault();
        }

        static string Box(Rect r) => $"({N(r.x)}, {N(r.y)}) {N(r.width)} x {N(r.height)}";

        // =====================================================================================================
        //  12: the planting kit v4's roads and the hedges' clearances need clear
        // =====================================================================================================

        /// <summary>
        /// A planting root's children that stand on a v4 carriageway, or in a hedge clearance the network keeps, are
        /// deleted whole (every document under each), each named with its place and its reason; the rest keep their
        /// documents byte for byte. The rule is <see cref="NineMileCreekRoadClearings.Why"/>.
        /// </summary>
        static LayerPatch Clearing(SceneYaml scene, string rootName, NineMileCreekRoadNetwork.Network net)
        {
            const string step = RoadsV4Step;
            Doc root = scene.RootNamed(rootName);
            Doc rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, step);
            var patch = new LayerPatch(step, rootName, root.FileId);
            bool hedged = rootName == NineMileCreekFieldPlanter.HedgeRootName || rootName == NineMileCreekFieldPlanter.TreeRootName;
            List<long> all = rootTr.FieldRefs("m_Children");
            var kept = new List<long>(all.Count);
            var cleared = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (long c in all)
            {
                Doc tr = scene.Require(c, "a plant's transform");
                if (tr.Stripped) throw new Refusal($"{step}: a prefab instance under {rootName}; the planter makes none.");
                Doc go = scene.Require(tr.FieldRef("m_GameObject"), "a plant");
                Vector3 p = Position(tr);
                string why = NineMileCreekRoadClearings.Why(net, p, hedged);
                if (why == null) { kept.Add(c); continue; }
                string label = $"{go.Field("m_Name")} &{go.FileId.ToString(CultureInfo.InvariantCulture)} at {Vec2(p)}";
                foreach (long id in scene.SubtreeOf(go.FileId).OrderBy(id => id))
                {
                    Doc d = scene.Require(id, "a plant's document");
                    patch.Delete(d, $"{label}: {Part(d, go)} ({why})");
                }
                string reason = why.Split(':')[0];
                cleared[reason] = (cleared.TryGetValue(reason, out int n) ? n : 0) + 1;
            }
            if (kept.Count != all.Count)
            {
                patch.Edit(rootTr, SetList(rootTr.Text, "m_Children", kept), rootName + "/children");
                foreach (var kv in cleared) patch.Note(rootName, "cleared", $"{kv.Value} {kv.Key}");
            }
            return patch.Seal(scene);
        }

        static string Part(Doc d, Doc go)
        {
            if (d.FileId == go.FileId) return "GameObject";
            if (d.ClassId == GameObjectClassId) return "GameObject " + d.Field("m_Name");
            if (d.ClassId == TransformClassId) return "Transform";
            string cls = d.Field("m_EditorClassIdentifier");
            return string.IsNullOrEmpty(cls) ? "class " + d.ClassId.ToString(CultureInfo.InvariantCulture) : cls.Substring(cls.LastIndexOf('.') + 1);
        }
    }

    /// <summary>
    /// <b>Where planting may not stand once kit v4 lays Nine Mile Creek's roads (NMC H4).</b> A pure function of the
    /// network file: a plant's point inside any route's carriageway (its line, at half its width), whatever the class;
    /// and, for the hedges and the trees that stand in them, the clearances the network names: Wharf Road's north side
    /// cleared over its span, the south side's gate spans (the gates are held, so only their clearance is laid), and a
    /// field boundary's hedge cut back to the end of its line.
    ///
    /// <para><b>The bands are the planter's own</b> (<see cref="NineMileCreekFields"/>): a road's hedge stands within
    /// <see cref="NineMileCreekFields.HedgeGiveWayMetres"/> of its road's line, and a boundary's shrubs stand on its
    /// stations, every <see cref="NineMileCreekFields.HedgeSpacingMetres"/> from the region's west edge, wandering up
    /// to <see cref="NineMileCreekFields.HedgeWanderMetres"/> off its line. A road's own hedge piece that happens to
    /// stand near a boundary is off that grid, and stays.</para>
    /// </summary>
    public static class NineMileCreekRoadClearings
    {
        public const string OnACarriageway = "on a v4 carriageway";
        public const string InAHedgeClearance = "in a hedge clearance";

        /// <summary>The station grid's tolerance: a boundary station's x is the west edge plus a whole number of
        /// spacings, written to float precision.</summary>
        const float StationTolerance = 1e-3f;

        /// <summary>Why a plant at <paramref name="p"/> must go, or null when it may stand. <paramref name="hedged"/>:
        /// the plant is a hedge shrub or a tree, which the hedge clearances also take.</summary>
        public static string Why(NineMileCreekRoadNetwork.Network net, Vector2 p, bool hedged)
        {
            foreach (NineMileCreekRoadNetwork.Route r in net.Routes)
                if (Inside(r, p)) return $"{OnACarriageway}: {r}";
            if (!hedged) return null;
            foreach (NineMileCreekRoadNetwork.Clearance c in net.Clearances)
            {
                if (c.Line != null)
                {
                    if (PastTheLine(c.Line, p)) return $"{InAHedgeClearance}: {c.Id} stops at x {NineMileCreekGroundSteps.N(c.Line[c.Line.Length - 1].x)}";
                    continue;
                }
                List<NineMileCreekRoadNetwork.Route> road = net.All(RoadOf(c.Id)).ToList();
                if (road.Count == 0) throw new Refusal($"{c.Id} names a road the network does not lay.");
                if (!Beside(road, p, c.Side == "north")) continue;
                if (c.Clear != Vector2.zero && p.x >= c.Clear.x && p.x <= c.Clear.y)
                    return $"{InAHedgeClearance}: {c.Id} {c.Side} side clear x {NineMileCreekGroundSteps.N(c.Clear.x)}..{NineMileCreekGroundSteps.N(c.Clear.y)}";
                foreach (Vector2 g in c.Gates)
                    if (p.x >= g.x && p.x <= g.y)
                        return $"{InAHedgeClearance}: {c.Id} {c.Side} side gate x {NineMileCreekGroundSteps.N(g.x)}..{NineMileCreekGroundSteps.N(g.y)}";
            }
            return null;
        }

        /// <summary>The road a road hedge's id names: <c>hedge.nmc_wharf_road</c> is road.nmc_wharf_road's.</summary>
        public static string RoadOf(string hedgeId) => "road." + hedgeId.Substring("hedge.".Length);

        public static bool Inside(NineMileCreekRoadNetwork.Route r, Vector2 p)
        {
            if (r.Points.Length == 1) return Vector2.Distance(p, r.Points[0]) < r.HalfWidth;
            for (int i = 0; i + 1 < r.Points.Length; i++)
                if (NineMileCreekRoadNetwork.PointToSegment(p, r.Points[i], r.Points[i + 1]) < r.HalfWidth) return true;
            return false;
        }

        /// <summary>Is <paramref name="p"/> one of an east–west boundary's stations east of where its line now ends?</summary>
        static bool PastTheLine(Vector2[] line, Vector2 p)
        {
            Vector2 end = line[line.Length - 1], before = line[line.Length - 2];
            if (before.y != end.y || end.x <= before.x)
                throw new Refusal("a boundary's line that does not end eastward along x; the cut is laid along x.");
            float west = NineMileCreekMainland.RegionWorldCenter.x - NineMileCreekMainland.RegionWorldSize.x * 0.5f;
            float station = (p.x - west) / NineMileCreekFields.HedgeSpacingMetres;
            return p.x > end.x && Mathf.Abs(station - Mathf.Round(station)) < StationTolerance &&
                   Mathf.Abs(p.y - end.y) <= NineMileCreekFields.HedgeWanderMetres;
        }

        /// <summary>Is <paramref name="p"/> within the road's hedge band, on its north (or south) side?</summary>
        static bool Beside(IEnumerable<NineMileCreekRoadNetwork.Route> road, Vector2 p, bool north)
        {
            float best = float.MaxValue, side = 0f;
            foreach (NineMileCreekRoadNetwork.Route r in road)
                for (int i = 0; i + 1 < r.Points.Length; i++)
                {
                    Vector2 a = r.Points[i], b = r.Points[i + 1];
                    float d = NineMileCreekRoadNetwork.PointToSegment(p, a, b);
                    if (d >= best) continue;
                    best = d;
                    float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                    side = b.x >= a.x ? cross : -cross;    // > 0: north of the segment walked eastward
                }
            return best <= NineMileCreekFields.HedgeGiveWayMetres && (north ? side > 0f : side < 0f);
        }
    }
}
#endif
