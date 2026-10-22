#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// NR1a: the creek's halves of Rice Point's SEA and STRAND doors, on the creek's west edge, and the two ways in
    /// that Rice Point's own halves ask the creek for. Rice Point (#956) lies west of the creek and authored its halves
    /// in its plan (<c>RicePointPlan.asset</c>: <c>door.rpt_sea</c>, <c>door.rpt_strand</c>); a region builder may not
    /// reach into another region's scene, so these are the creek's to place. The bands and points are
    /// <see cref="NineMileCreekMainland"/>'s, Rice Point's mirrored across the seam (the sum is written there).
    ///
    /// <para><b>Two writers, one answer.</b> <see cref="PlaceDoors"/> and <see cref="PlaceArrivals"/> are the builder's
    /// door and anchor sections: a full <see cref="NineMileCreekBuilder"/> run calls them. <see cref="PlanAll"/> is step
    /// 12, the same objects as one-root text patches to the committed scene (N1's rule: the baked scene changes by named
    /// steps, never by a rebuild), and an EditMode test holds the two to the same GameObjects, components and fields.</para>
    ///
    /// <para>Rice Point's third door, Route 91, is NR1b's: it waits on H4's road, and Rice Point keeps its own half shut
    /// until then (<see cref="LandDoor.Shut"/>).</para>
    /// </summary>
    public static class NineMileCreekRicePointDoors
    {
        public const string RegionAssetName = "RicePoint";
        public const string RegionAssetPath = "Assets/_Project/Data/Regions/" + RegionAssetName + ".asset";

        public const string SeaDoorName = "PassageToRicePointSea";
        public const string StrandDoorName = "PassageToRicePointStrand";

        /// <summary>The ways in the creek's doors ask Rice Point for: its plan's <c>OwnArrival</c>s, which its anchor
        /// authors.</summary>
        public const string SeaDoorArrivalKey = "nmc_sea";
        public const string StrandDoorArrivalKey = "nmc_strand";

        /// <summary>The ways in Rice Point's doors ask the creek for (its plan's <c>TargetArrival</c>s), which the
        /// creek's anchor authors. Their points take the names Rice Point's template gives its own (<c>LandSceneTemplate.AddAnchor</c>):
        /// the boat's point is the key, the walker's the key and <see cref="ShoreSuffix"/>.</summary>
        public const string SeaArrivalKey = "rpt_sea";
        public const string StrandArrivalKey = "rpt_strand";
        public const string ShoreSuffix = "_shore";

        /// <summary>A new door keeps <c>RegionPassage</c>'s own re-fire guard: the builder sets nothing else, so the step
        /// writes the component's default (the builder-parity test holds the two together).</summary>
        public const float PassageGuardSeconds = 1.5f;

        /// <summary>One of the creek's halves: its band, the way in it asks Rice Point for, and whether only a walker may
        /// take it (through <see cref="LandPassageAccess"/>, the policy Rice Point's own strand door carries).</summary>
        public readonly struct CreekHalf
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector2 Size;
            public readonly string ArrivalKey;
            public readonly bool OnFootOnly;

            public CreekHalf(string name, Vector3 position, Vector2 size, string arrivalKey, bool onFootOnly)
            {
                Name = name; Position = position; Size = size; ArrivalKey = arrivalKey; OnFootOnly = onFootOnly;
            }
        }

        /// <summary>One way in that Rice Point's doors ask the creek for: where the boat lies, and where the walker
        /// stands. A walk-in has no boat point: the player's boat is not with them, so she is left where she lies, as
        /// at the bar.</summary>
        public readonly struct WayIn
        {
            public readonly string Key;
            public readonly Vector3? Boat;
            public readonly Vector3 Walker;

            public WayIn(string key, Vector3? boat, Vector3 walker) { Key = key; Boat = boat; Walker = walker; }
        }

        public static readonly CreekHalf[] Halves =
        {
            new CreekHalf(SeaDoorName, NineMileCreekMainland.ToRicePointSeaPassagePos,
                          NineMileCreekMainland.RicePointSeaPassageBandSize, SeaDoorArrivalKey, onFootOnly: false),
            new CreekHalf(StrandDoorName, NineMileCreekMainland.ToRicePointStrandPassagePos,
                          NineMileCreekMainland.RicePointStrandPassageBandSize, StrandDoorArrivalKey, onFootOnly: true),
        };

        public static readonly WayIn[] WaysIn =
        {
            new WayIn(SeaArrivalKey, NineMileCreekMainland.RicePointSeaArrivalPos, NineMileCreekMainland.RicePointSeaLandingPos),
            new WayIn(StrandArrivalKey, null, NineMileCreekMainland.RicePointStrandLandingPos),
        };

        // =====================================================================================================
        //  The builder's sections
        // =====================================================================================================

        /// <summary>The door section: both halves, wired to Rice Point through the loader, the strand's for walkers
        /// only. Places nothing without Rice Point's RegionDef, the West Water's guard: never a dangling door.</summary>
        public static RegionPassage[] PlaceDoors(RegionDef ricePoint, RegionSceneLoader loader)
        {
            if (ricePoint == null) return Array.Empty<RegionPassage>();
            var placed = new RegionPassage[Halves.Length];
            for (int i = 0; i < Halves.Length; i++)
            {
                CreekHalf half = Halves[i];
                placed[i] = WaterSceneTemplate.AddPassage(half.Name, half.Position, half.Size, ricePoint, loader, half.ArrivalKey);
                if (half.OnFootOnly) placed[i].ConfigureAccess(placed[i].gameObject.AddComponent<LandPassageAccess>());
            }
            return placed;
        }

        /// <summary>The anchor section's part: Rice Point's two ways in, as points under the anchor. None without Rice
        /// Point's RegionDef: a way in that no door asks for is clutter.</summary>
        public static NamedArrival[] PlaceArrivals(RegionDef ricePoint, Transform anchor)
        {
            if (ricePoint == null) return Array.Empty<NamedArrival>();
            return WaysIn.Select(w => new NamedArrival(w.Key,
                w.Boat.HasValue ? Point(anchor, w.Key, w.Boat.Value) : null,
                Point(anchor, w.Key + ShoreSuffix, w.Walker))).ToArray();
        }

        /// <summary>The loader's region list: every region the creek's doors reach, an absent one left out rather than
        /// listed as a hole the router would have to trip over.</summary>
        public static Object[] LoaderRegions(params RegionDef[] regions) =>
            regions.Where(r => r != null).Cast<Object>().ToArray();

        static Transform Point(Transform parent, string name, Vector3 at)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = at;
            return point;
        }

        // =====================================================================================================
        //  Step 12: the same objects, as one-root text patches to the committed scene
        // =====================================================================================================

        public const string Step = "12-rice-point-doors";
        public const string PatchFolder = "artifacts/rice-point-nr1";
        public const string LoaderName = "RegionSceneLoader";

        const int GameObjectClass = 1, TransformClass = 4, BoxCollider2DClass = 61, MonoBehaviourClass = 114;
        const long MainObject = 11400000, ScriptObject = 11500000;
        const string Unlinked = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}";

        /// <summary>What the step's documents point at outside the scene, by guid: Rice Point's RegionDef and the two
        /// scripts. Read from their .meta files, so the step plans the same in the editor and without it.</summary>
        public sealed class StepAssets
        {
            public const string PassageScriptPath = "Assets/_Project/Code/World/RegionPassage.cs";
            public const string AccessScriptPath = "Assets/_Project/Code/App/LandPassageAccess.cs";

            public readonly ObjRef RicePoint, PassageScript, AccessScript;

            public StepAssets(string ricePoint, string passageScript, string accessScript)
            {
                RicePoint = new ObjRef(MainObject, ricePoint, 2);
                PassageScript = new ObjRef(ScriptObject, passageScript, 3);
                AccessScript = new ObjRef(ScriptObject, accessScript, 3);
            }

            /// <summary>The assets under <paramref name="projectRoot"/> ("" for the editor's own), or null without Rice
            /// Point's RegionDef: the builder places no doors then, and the step plans none.</summary>
            public static StepAssets Read(string projectRoot = "")
            {
                string def = Path.Combine(projectRoot, RegionAssetPath + ".meta");
                if (!File.Exists(def)) return null;
                return new StepAssets(GuidIn(def), GuidIn(Path.Combine(projectRoot, PassageScriptPath + ".meta")),
                                      GuidIn(Path.Combine(projectRoot, AccessScriptPath + ".meta")));
            }

            static string GuidIn(string meta)
            {
                Match m = Regex.Match(File.ReadAllText(meta), @"(?m)^guid: ([0-9a-f]{32})\r?$");
                return m.Success ? m.Groups[1].Value : throw new Refusal($"{Step}: no guid in {meta}.");
            }
        }

        /// <summary>Step 12 on <paramref name="sceneText"/>: each half a new root, then the loader's list, then the
        /// anchor's ways in, each patch planned on the text the one before it leaves. Returns the patches and, in
        /// <paramref name="after"/>, the scene they write. Plans nothing without <paramref name="assets"/>.</summary>
        public static List<LayerPatch> PlanAll(string sceneText, StepAssets assets, out string after)
        {
            var patches = new List<LayerPatch>();
            after = sceneText;
            if (assets == null) return patches;
            var planners = Halves.Select(h => (Func<SceneYaml, LayerPatch>)(s => Door(s, h, assets)))
                                 .Concat(new Func<SceneYaml, LayerPatch>[] { s => Loader(s, assets), Arrivals });
            foreach (Func<SceneYaml, LayerPatch> plan in planners)
            {
                SceneYaml scene = SceneYaml.Parse(after);
                LayerPatch patch = plan(scene);
                patches.Add(patch);
                after = patch.Apply(scene).Render();
            }
            return patches;
        }

        /// <summary>One half as its own root: the builder's GameObject, transform, trigger band and passage, and the
        /// strand's on-foot policy. A second plan finds the root and writes the same documents on the same ids.</summary>
        static LayerPatch Door(SceneYaml scene, CreekHalf half, StepAssets assets)
        {
            Doc loader = LoaderOf(scene);
            Doc root = RootOrNull(scene, half.Name);
            int components = half.OnFootOnly ? 4 : 3;
            var ids = new long[components + 1];
            if (root == null)
            {
                var alloc = new IdAllocator(scene);
                string[] kinds = { "GameObject", "Transform", "BoxCollider2D", "RegionPassage", "LandPassageAccess" };
                for (int i = 0; i < ids.Length; i++) ids[i] = alloc.Next($"{Step}/{half.Name}/{kinds[i]}");
            }
            else
            {
                List<long> found = root.FieldRefs("m_Component");
                if (found.Count != components)
                    throw new Refusal($"{Step}: '{half.Name}' carries {found.Count} components where the builder puts " +
                                      $"{components}; it was changed by hand.");
                ids[0] = root.FileId;
                for (int i = 0; i < components; i++) ids[i + 1] = found[i];
            }
            long go = ids[0], tr = ids[1], box = ids[2], passage = ids[3], access = half.OnFootOnly ? ids[4] : 0;

            var patch = new LayerPatch(Step, half.Name, go);
            Put(scene, patch, GameObjectYaml(go, half.Name, ids.Skip(1).ToArray()), $"{half.Name} (GameObject)");
            Put(scene, patch, TransformYaml(tr, go, half.Position, 0), $"{half.Name} (Transform)");
            Put(scene, patch, BoxYaml(box, go, half.Size), $"{half.Name} (BoxCollider2D)");
            Put(scene, patch, PassageYaml(passage, go, assets, loader.FileId, half.ArrivalKey, access), $"{half.Name} (RegionPassage)");
            if (half.OnFootOnly)
                Put(scene, patch, Behaviour(access, go, assets.AccessScript, typeof(LandPassageAccess)), $"{half.Name} (LandPassageAccess)");

            string band = $"band at {Vec2(half.Position)}, {N(half.Size.x)} x {N(half.Size.y)} m, asks Rice Point for " +
                          $"'{half.ArrivalKey}'" + (half.OnFootOnly ? ", on foot only" : "");
            if (root == null)
            {
                Doc list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, tr), $"the scene's SceneRoots list ('{half.Name}' joins it)");
                patch.Note(half.Name, "a new root", band);
            }
            else if (!patch.IsEmpty) patch.Note(half.Name, "re-placed", band);
            return patch.Seal(scene);
        }

        /// <summary>The loader's region list gains Rice Point at its end, where the builder lists it.</summary>
        static LayerPatch Loader(SceneYaml scene, StepAssets assets)
        {
            Doc go = scene.RootNamed(LoaderName);
            Doc loader = LoaderOf(scene);
            var patch = new LayerPatch(Step, LoaderName, go.FileId);
            List<List<string>> regions = Entries(loader, "_regions");
            if (!regions.Any(r => ObjRef.Parse(r[0]).SameTarget(assets.RicePoint)))
            {
                regions.Add(new List<string> { "  - " + assets.RicePoint.Yaml });
                patch.Edit(loader, WithEntries(loader, "_regions", regions), $"{LoaderName} (RegionSceneLoader: _regions)");
                patch.Note(LoaderName, "Rice Point joins its regions", $"{regions.Count} listed");
            }
            return patch.Seal(scene);
        }

        /// <summary>Rice Point's two ways in: the builder's points under the anchor, and its named arrivals after the
        /// ones the anchor names already. A second plan finds the points by name and writes the same documents.</summary>
        static LayerPatch Arrivals(SceneYaml scene)
        {
            string name = NineMileCreekGroundSteps.AnchorName;
            Doc go = scene.RootNamed(name);
            Doc tr = scene.TransformOf(go);
            RequireIdentity(tr, name);
            List<Doc> anchors = scene.ComponentsOf(go).Where(d => d.IsScript(typeof(RegionAnchor).FullName)).ToList();
            if (anchors.Count != 1) throw new Refusal($"{Step}: '{name}' carries {anchors.Count} RegionAnchors; the step needs one.");
            Doc anchor = anchors[0];

            var patch = new LayerPatch(Step, name, go.FileId);
            var ids = new IdAllocator(scene);
            List<long> children = tr.FieldRefs("m_Children");
            var ways = new List<List<string>>();
            foreach (WayIn w in WaysIn)
            {
                long boat = w.Boat.HasValue ? PointDocs(scene, patch, ids, tr, children, w.Key, w.Boat.Value) : 0;
                long walker = PointDocs(scene, patch, ids, tr, children, w.Key + ShoreSuffix, w.Walker);
                ways.Add(new List<string> { "  - Key: " + w.Key, "    ArrivalPoint: " + Ref(boat), "    DisembarkPoint: " + Ref(walker) });
            }
            patch.Edit(tr, WithEntries(tr, "m_Children", children.Select(c => new List<string> { "  - " + Ref(c) }).ToList()),
                       $"{name} (Transform: its points)");
            var mine = new HashSet<string>(WaysIn.Select(w => w.Key), StringComparer.OrdinalIgnoreCase);
            List<List<string>> kept = Entries(anchor, "_arrivals").Where(e => !mine.Contains(KeyOf(e))).ToList();
            string after = WithEntries(anchor, "_arrivals", kept.Concat(ways).ToList());
            if (after != anchor.Text)
            {
                patch.Edit(anchor, after, $"{name} (RegionAnchor: _arrivals)");
                patch.Note(name, "Rice Point's ways in", string.Join(", ", WaysIn.Select(w => w.Key)) +
                                                         $" after {string.Join(", ", kept.Select(KeyOf))}");
            }
            return patch.Seal(scene);
        }

        /// <summary>One point under the anchor, by name: the builder's GameObject and its transform, at the point's
        /// world position. Returns the transform's id.</summary>
        static long PointDocs(SceneYaml scene, LayerPatch patch, IdAllocator ids, Doc parent, List<long> children,
                              string name, Vector3 at)
        {
            List<Doc> named = parent.FieldRefs("m_Children").Select(id => scene.Require(id, "a child transform"))
                .Where(t => !t.Stripped).Select(t => scene.Require(t.FieldRef("m_GameObject"), "a child GameObject"))
                .Where(g => g.Field("m_Name") == name).ToList();
            if (named.Count > 1) throw new Refusal($"{Step}: the anchor holds {named.Count} points named '{name}'.");
            long go, tr;
            if (named.Count == 0)
            {
                go = ids.Next($"{Step}/{name}/GameObject");
                tr = ids.Next($"{Step}/{name}/Transform");
            }
            else
            {
                List<long> found = named[0].FieldRefs("m_Component");
                if (found.Count != 1)
                    throw new Refusal($"{Step}: '{name}' carries {found.Count} components where the builder puts a transform alone.");
                go = named[0].FileId;
                tr = found[0];
            }
            string label = NineMileCreekGroundSteps.AnchorName + "/" + name;
            Put(scene, patch, GameObjectYaml(go, name, new[] { tr }), $"{label} (GameObject)");
            Put(scene, patch, TransformYaml(tr, go, at, parent.FileId), $"{label} (Transform)");
            if (!children.Contains(tr)) children.Add(tr);
            if (named.Count == 0) patch.Note(label, "placed", $"at {Vec2(at)}");
            return tr;
        }

        /// <summary>A document the step writes: added when the scene lacks its id, else edited (an edit that changes
        /// nothing is skipped). An id holding another kind of document was changed by hand, and is refused.</summary>
        static void Put(SceneYaml scene, LayerPatch patch, string text, string name)
        {
            var want = new Doc(text);
            Doc now = scene.Get(want.FileId);
            if (now == null) { patch.Add(text, name); return; }
            if (now.ClassId != want.ClassId)
                throw new Refusal($"{Step}: &{want.FileId} holds a {now.Lines[1].TrimEnd(':')} where the step writes {name}.");
            patch.Edit(now, text, name);
        }

        /// <summary>The scene's one region loader, which every door of the creek's travels by.</summary>
        static Doc LoaderOf(SceneYaml scene)
        {
            List<Doc> hits = scene.ComponentsOf(scene.RootNamed(LoaderName))
                                  .Where(d => d.IsScript(typeof(RegionSceneLoader).FullName)).ToList();
            if (hits.Count != 1) throw new Refusal($"{Step}: '{LoaderName}' carries {hits.Count} loaders; the doors need one.");
            return hits[0];
        }

        /// <summary>The scene's one root with this name, or null when it has none yet (refuses two, or one below the top).</summary>
        static Doc RootOrNull(SceneYaml scene, string name) =>
            scene.Docs.Any(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == name)
                ? scene.RootNamed(name)
                : null;

        /// <summary>A two-space list field's entries, each as its lines: the "  - " line, then any four-space lines under it.</summary>
        static List<List<string>> Entries(Doc doc, string key)
        {
            string[] lines = doc.Lines;
            int at = Array.FindIndex(lines, l => l == "  " + key + ":" || l == "  " + key + ": []");
            if (at < 0) throw new Refusal($"{Step}: no '{key}' in &{doc.FileId}.");
            var entries = new List<List<string>>();
            for (int i = at + 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("  - ", StringComparison.Ordinal)) entries.Add(new List<string> { lines[i] });
                else if (lines[i].StartsWith("    ", StringComparison.Ordinal) && entries.Count > 0) entries[entries.Count - 1].Add(lines[i]);
                else break;
            }
            return entries;
        }

        /// <summary>The document with a list field's entries replaced, written <c>[]</c> when there are none.</summary>
        static string WithEntries(Doc doc, string key, List<List<string>> entries)
        {
            string[] lines = doc.Lines;
            int at = Array.FindIndex(lines, l => l == "  " + key + ":" || l == "  " + key + ": []");
            if (at < 0) throw new Refusal($"{Step}: no '{key}' in &{doc.FileId}.");
            int end = at + 1;
            while (end < lines.Length && (lines[end].StartsWith("  - ", StringComparison.Ordinal) ||
                                          lines[end].StartsWith("    ", StringComparison.Ordinal))) end++;
            var block = new List<string> { entries.Count == 0 ? "  " + key + ": []" : "  " + key + ":" };
            foreach (List<string> e in entries) block.AddRange(e);
            return string.Join("\n", lines.Take(at).Concat(block).Concat(lines.Skip(end)));
        }

        static string KeyOf(List<string> arrival) =>
            arrival[0].StartsWith("  - Key: ", StringComparison.Ordinal) ? arrival[0].Substring("  - Key: ".Length) : "";

        // ---- the documents, as the editor writes them in this scene (PassageToWestWater's, RicePoint.unity's policy) ----

        static string GameObjectYaml(long id, string name, IList<long> components)
        {
            var lines = new List<string> { Header(GameObjectClass, id, "GameObject"), Unlinked, "  serializedVersion: 6", "  m_Component:" };
            lines.AddRange(components.Select(c => "  - component: " + Ref(c)));
            lines.AddRange(new[] { "  m_Layer: 0", "  m_Name: " + name, "  m_TagString: Untagged", "  m_Icon: {fileID: 0}",
                                   "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0", "  m_IsActive: 1" });
            return string.Join("\n", lines);
        }

        static string TransformYaml(long id, long go, Vector3 at, long parent) => string.Join("\n",
            Header(TransformClass, id, "Transform"), Unlinked, "  m_GameObject: " + Ref(go), "  serializedVersion: 2",
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", $"  m_LocalPosition: {{x: {N(at.x)}, y: {N(at.y)}, z: {N(at.z)}}}",
            "  m_LocalScale: {x: 1, y: 1, z: 1}", "  m_ConstrainProportionsScale: 0", "  m_Children: []",
            "  m_Father: " + Ref(parent), "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");

        static string BoxYaml(long id, long go, Vector2 size) => string.Join("\n",
            Header(BoxCollider2DClass, id, "BoxCollider2D"), Unlinked, "  m_GameObject: " + Ref(go), "  m_Enabled: 1",
            "  serializedVersion: 3", "  m_Density: 1", "  m_Material: {fileID: 0}",
            "  m_IncludeLayers:", "    serializedVersion: 2", "    m_Bits: 0",
            "  m_ExcludeLayers:", "    serializedVersion: 2", "    m_Bits: 0",
            "  m_LayerOverridePriority: 0",
            "  m_ForceSendLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
            "  m_ForceReceiveLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
            "  m_ContactCaptureLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
            "  m_CallbackLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
            "  m_IsTrigger: 1", "  m_UsedByEffector: 0", "  m_CompositeOperation: 0", "  m_CompositeOrder: 0",
            "  m_Offset: {x: 0, y: 0}",
            "  m_SpriteTilingProperty:", "    border: {x: 0, y: 0, z: 0, w: 0}", "    pivot: {x: 0, y: 0}",
            "    oldSize: {x: 0, y: 0}", "    newSize: {x: 0, y: 0}", "    adaptiveTilingThreshold: 0",
            "    drawMode: 0", "    adaptiveTiling: 0",
            "  m_AutoTiling: 0", "  m_Size: " + Vec2(size), "  m_EdgeRadius: 0");

        static string PassageYaml(long id, long go, StepAssets assets, long loader, string arrivalKey, long access) =>
            string.Join("\n", Behaviour(id, go, assets.PassageScript, typeof(RegionPassage)),
                "  _target: " + assets.RicePoint.Yaml, "  _loader: " + Ref(loader), "  _arrivalKey: " + arrivalKey,
                "  _reentryCooldownSeconds: " + N(PassageGuardSeconds), "  _accessPolicy: " + Ref(access));

        /// <summary>A MonoBehaviour's head; Unity 6 writes the class into <c>m_EditorClassIdentifier</c> as
        /// <c>Assembly::Namespace.Class</c>, and an empty <c>m_Name</c> with its trailing space.</summary>
        static string Behaviour(long id, long go, ObjRef script, Type type) => string.Join("\n",
            Header(MonoBehaviourClass, id, "MonoBehaviour"), Unlinked, "  m_GameObject: " + Ref(go), "  m_Enabled: 1",
            "  m_EditorHideFlags: 0", "  m_Script: " + script.Yaml, "  m_Name: ",
            "  m_EditorClassIdentifier: " + type.Assembly.GetName().Name + "::" + type.FullName);

        static string Header(int classId, long id, string kind) =>
            $"--- !u!{classId.ToString(CultureInfo.InvariantCulture)} &{id.ToString(CultureInfo.InvariantCulture)}\n{kind}:";

        static string Ref(long id) => "{fileID: " + id.ToString(CultureInfo.InvariantCulture) + "}";
        static string N(float v) => NineMileCreekGroundSteps.N(v);
        static string Vec2(Vector2 v) => $"{{x: {N(v.x)}, y: {N(v.y)}}}";

        // =====================================================================================================
        //  The menu and the bake
        // =====================================================================================================

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Rice Point doors (dry run)")]
        public static void DryRun() => Run(apply: false);

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Apply Rice Point doors")]
        public static void Apply() => Run(apply: true);

        /// <summary>The bake's batch entry (Phase B): applies step 12, then plans it again on what it wrote, which must
        /// be empty. Exits 1 on any refusal, so a half-baked scene never reports success.</summary>
        public static void ApplyCli()
        {
            try
            {
                Run(apply: true);
                List<LayerPatch> again = PlanAll(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath), StepAssets.Read(), out _);
                if (again.Count == 0 || again.Any(p => !p.IsEmpty)) throw new Refusal($"{Step}: a second plan is not empty.");
                Debug.Log($"[{Step}] APPLIED; a second plan is empty.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Plans step 12 on the committed scene and writes each patch, and the step's summary, under
        /// <see cref="PatchFolder"/>; with <paramref name="apply"/>, writes the scene too (it must be closed).</summary>
        public static List<LayerPatch> Run(bool apply)
        {
            string path = NineMileCreekLayerRefresh.ScenePath;
            if (apply)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == path)
                        throw new Refusal("NineMileCreek is open. Close it before applying text patches.");
            StepAssets assets = StepAssets.Read();
            if (assets == null) throw new Refusal($"{Step}: no Rice Point RegionDef at {RegionAssetPath}; there is nothing to join.");
            string before = File.ReadAllText(path);
            List<LayerPatch> patches = PlanAll(before, assets, out string after);
            Write(PatchFolder, patches);
            if (apply && after != before) File.WriteAllText(path, after, new UTF8Encoding(false));
            Debug.Log($"[{Step}] {(apply ? "Applied" : "Dry run")}:\n" + string.Join("\n", patches.Select(p => p.Summary())));
            return patches;
        }

        /// <summary>Each patch as its own file under <paramref name="folder"/>, and the step's summary beside them.</summary>
        public static void Write(string folder, IReadOnlyList<LayerPatch> patches)
        {
            Directory.CreateDirectory(folder);
            var utf8 = new UTF8Encoding(false);
            foreach (LayerPatch p in patches)
                File.WriteAllText(Path.Combine(folder, NineMileCreekGroundSteps.PatchFileName(p)), p.ToYaml(), utf8);
            File.WriteAllText(Path.Combine(folder, Step + ".summary.txt"),
                              string.Join("\n", patches.Select(p => p.Summary())) + "\n", utf8);
        }
    }
}
#endif
