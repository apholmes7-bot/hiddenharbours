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
    /// <b>THE ROOM-FIXTURE PLACEMENT STEP</b> (kitchens and bathrooms placement plan, section 3): the one
    /// named text patch that stands every placed room fixture (a basin, a shower, a bath, a toilet, a stove)
    /// in its region scene, and the builder hook that re-stands them on a rebuild. The later placement
    /// slices (R2 to R5, N1 and N2) add rows here; none of them writes its own planner.
    ///
    /// <para><b>What it grew from.</b> #1273's <see cref="WashStationPlacement"/> placed the first two wash
    /// stations by this same method. Its two sites are rows here now, with the same names and ids, so a
    /// re-plan of the committed scenes finds them and changes nothing.</para>
    ///
    /// <para><b>Why a text patch and not "open the scene and save it".</b> Opening and re-saving either big
    /// scene rewrites most of its 5 to 13 MB, so these scenes are only changed by named patches
    /// (<see cref="StPetersLayerRefresh"/>). This step plans from the committed scene text, adds one prefab
    /// instance per site (the stripped transform and its parent's child list with it) and re-plans to an
    /// empty patch.</para>
    ///
    /// <para><b>Interior sites are placed by MODEL coordinate</b>, the frame every furnishing in
    /// <see cref="StPetersInteriors"/> uses, through the building's own serialized footprint
    /// (<see cref="InteriorFootprint"/>), so a fixture lands where the room picture says even if the building
    /// is re-faced. Ground-floor fixtures hang under the building's furniture root, upstairs ones under the
    /// upper storey's, which <see cref="BuildingInterior"/> switches on only while the player is on that
    /// level.</para>
    ///
    /// <para><b>The pilot (R1).</b> Only Ginny's cottage gets a bathroom in this slice: the toilet, the
    /// basin and the shower of its <see cref="RoomFixturePlan"/> rows, on the ground floor. The closet
    /// partition around them, the kitchen and every other building are R2 onward.</para>
    ///
    /// <para><b>The player's storage</b> (owner 2026-11-17) rides the same step: <see cref="HomeStoragePlacement"/>
    /// adds Ginny's rod rack and gear locker (their interact id is the row's <c>storage.*</c> save key) and the
    /// camper's shore power lead, an open-air picture with no interact id under St Peters' own open-air root.</para>
    /// </summary>
    public static class InteriorFixturePlacement
    {
        public const string StepStPeters = "interior-fixtures-st-peters";
        public const string StepNmc = "interior-fixtures-nmc";
        public const string PatchFolder = "artifacts/interior-fixtures";

        public const string StPetersScene = WashStationPlacement.StPetersScene;
        public const string NineMileCreekScene = WashStationPlacement.NineMileCreekScene;

        public const string PrefabFolder = WashStationPlacement.PrefabFolder;
        public const string ToiletPrefab = PrefabFolder + "/Toilet.prefab";
        public const string ShowerPrefab = PrefabFolder + "/Shower.prefab";
        public const string BathPrefab = PrefabFolder + "/Bath.prefab";
        public const string OuthousePrefab = PrefabFolder + "/Outhouse.prefab";

        public const string KitchenFolder = "Assets/_Project/Prefabs/Props/Kitchen";

        /// <summary>The one building R1 places fixtures in.</summary>
        public const string PilotBuildingKey = "ginny_cottage";

        /// <summary>The named step for a scene.</summary>
        public static string StepFor(string scenePath) =>
            scenePath == NineMileCreekScene ? StepNmc
            : scenePath == StPetersScene ? StepStPeters
            : throw new Refusal($"interior-fixtures: no step for {scenePath}.");

        /// <summary>The prefab each planned kind stands as. The bathroom basin is #1273's washstand.</summary>
        public static string PrefabFor(RoomFixturePlan.Kind kind) => kind switch
        {
            RoomFixturePlan.Kind.PrepCounter => KitchenFolder + "/PrepCounter.prefab",
            RoomFixturePlan.Kind.Stove => KitchenFolder + "/Stove.prefab",
            RoomFixturePlan.Kind.Microwave => KitchenFolder + "/Microwave.prefab",
            RoomFixturePlan.Kind.BathroomSink => WashStationPlacement.WashStandPrefab,
            RoomFixturePlan.Kind.Shower => ShowerPrefab,
            RoomFixturePlan.Kind.Bath => BathPrefab,
            RoomFixturePlan.Kind.Toilet => ToiletPrefab,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>A slot as a GameObject name: <c>bathroom_sink</c> is <c>BathroomSink</c>.</summary>
        public static string NameOf(string slot) =>
            string.Concat(slot.Split('_').Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));

        // ---- the sites -----------------------------------------------------------------------------

        /// <summary>One placed fixture: which scene, which prefab, where, and what its interact id is.</summary>
        public readonly struct Site
        {
            public readonly string Scene, Prefab, Name, InteractId, Why;

            /// <summary>Interior sites: the root GameObject that holds the building, and the building's name
            /// under it. Null for an open-air site.</summary>
            public readonly string BuildingRoot, Building;

            /// <summary>Interior: model metres on <see cref="Level"/>, in the building's serialized footprint
            /// frame, or in the plan's door-at-+y frame when <see cref="PlanFrame"/> is set (see
            /// <see cref="ModelPoint"/>). Open air: world metres under <see cref="WashStationPlacement.NmcRootName"/>.</summary>
            public readonly Vector2 At;

            public readonly int Level;

            /// <summary>True for a <see cref="RoomFixturePlan"/> row, whose frame always has the front door
            /// at <c>+y</c>; false for a site written in the footprint's own frame.</summary>
            public readonly bool PlanFrame;

            public Site(string scene, string prefab, string name, string interactId, Vector2 at, string why,
                        string buildingRoot = null, string building = null, int level = 0, bool planFrame = false)
            {
                Scene = scene; Prefab = prefab; Name = name; InteractId = interactId; At = at; Why = why;
                BuildingRoot = buildingRoot; Building = building; Level = level; PlanFrame = planFrame;
            }

            public bool IsInterior => !string.IsNullOrEmpty(BuildingRoot);
        }

        /// <summary>A planned row as a site.</summary>
        public static Site SiteOf(RoomFixturePlan.Row row) =>
            new Site(RoomFixturePlan.Scene, PrefabFor(row.Kind), NameOf(RoomFixturePlan.Slot(row.Kind)), row.Id, row.At,
                     $"{row.BuildingKey}, room '{row.RoomKey}', level {row.Level}: the plan's {RoomFixturePlan.Slot(row.Kind)}",
                     row.BuildingRoot, row.Building, row.Level, planFrame: true);

        /// <summary>
        /// The site's point in the footprint's own model frame. A plan row's frame has the door at <c>+y</c>;
        /// a footprint whose door is in its <c>−y</c> wall (Ginny's cottage, which was not turned with the
        /// village) holds the same room half a turn round, so the row is negated. Without this the pilot
        /// bathroom stood in the opposite corner, at the door end, beside the table.
        /// </summary>
        public static Vector2 ModelPoint(InteriorFootprint footprint, Site site) =>
            site.PlanFrame && footprint.DoorSign < 0f ? -site.At : site.At;

        /// <summary>The rows this slice places: the pilot building's bathroom.</summary>
        public static IEnumerable<RoomFixturePlan.Row> PlacedRows() =>
            RoomFixturePlan.Rows.Where(r => r.BuildingKey == PilotBuildingKey && RoomFixturePlan.IsBathroom(r.Kind));

        /// <summary>Every placed fixture, in plan order: #1273's two wash stations, then the placed rows.</summary>
        public static readonly Site[] Sites = BuildSites();

        static Site[] BuildSites()
        {
            var sites = new List<Site>
            {
                new Site(StPetersScene, WashStationPlacement.WashStandPrefab, "WashStand", WashStationPlacement.GinnyWashstandId,
                         new Vector2(0.45f, 3.55f),
                         "the player's room upstairs at Ginny's: against the front wall, between the landing door " +
                         "and the head of his bed, clear of the doorway lane and the wardrobe",
                         StPetersGinnyPlot.RootName, StPetersGinnyPlot.CottageKey, level: 1),
                new Site(NineMileCreekScene, WashStationPlacement.WharfShowerPrefab, "WharfShower", WashStationPlacement.NmcWharfShowerId,
                         new Vector2(90.2f, 108.2f),
                         "the wharf's land side, on the open ground between the harbourmaster's office and the " +
                         "picnic table: the first dry ground a skipper crosses coming up off the boats"),
            };
            sites.AddRange(PlacedRows().Select(SiteOf));
            sites.AddRange(HomeStoragePlacement.Sites());   // Ginny's rod rack and locker, the camper's shore lead
            return sites.ToArray();
        }

        // ---- the live placement (the builder's path) -----------------------------------------------

        /// <summary>
        /// Stand every interior site whose building is <paramref name="buildingGo"/>: called by the region
        /// builder right after it stands the building's interior, so a rebuild keeps the fixtures. The same
        /// <see cref="Sites"/> rows and the same footprint maths as the text patch.
        /// </summary>
        public static int StandInterior(GameObject buildingGo, string buildingRoot)
        {
            if (buildingGo == null) return 0;
            var interior = buildingGo.GetComponent<BuildingInterior>();
            if (interior == null) return 0;
            int placed = 0;
            foreach (Site site in Sites)
            {
                if (!site.IsInterior || site.BuildingRoot != buildingRoot || site.Building != buildingGo.name) continue;
                Transform parent = site.Level > 0
                    ? buildingGo.transform.Find(StPetersInteriors.UpperPropsChildName)
                    : buildingGo.transform.Find(StPetersInteriors.PropsChildName);
                if (parent == null) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(site.Prefab);
                if (prefab == null)
                {
                    Debug.LogWarning($"[InteriorFixturePlacement] {site.Prefab} is missing.");
                    continue;
                }
                Transform old = parent.Find(site.Name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = site.Name;
                InteriorFootprint footprint = interior.FootprintFor(site.Level);
                Vector2 world = footprint.ModelToWorld(ModelPoint(footprint, site));
                go.transform.position = new Vector3(world.x, world.y, 0f);
                MonoBehaviour point = site.InteractId != null ? IdComponentOf(go) : null;
                if (point != null)
                {
                    var so = new SerializedObject(point);
                    so.FindProperty("_id").stringValue = site.InteractId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                placed++;
            }
            return placed;
        }

        /// <summary>The root component that serializes an <c>_id</c> (the interactable), or null.</summary>
        public static MonoBehaviour IdComponentOf(GameObject go)
        {
            MonoBehaviour found = null;
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                using var so = new SerializedObject(mb);
                SerializedProperty p = so.FindProperty("_id");
                if (p == null || p.propertyType != SerializedPropertyType.String) continue;
                if (found != null) return null;
                found = mb;
            }
            return found;
        }

        // ---- the text patch ------------------------------------------------------------------------

        /// <summary>One prefab as its file holds it: the GUID and the root's three documents' ids. The point is
        /// the root MonoBehaviour that serializes <c>_id</c>: at most one, or the prefab is refused; none (0) is a
        /// prefab with no interact id (a picture, such as the shore lead), which only a site with no id may stand.</summary>
        public sealed class PrefabFacts
        {
            public string Path, Name, Guid;
            public long GameObject, Transform, Point;

            public static PrefabFacts Read(string projectRoot, string path)
            {
                string file = System.IO.Path.Combine(projectRoot, path);
                if (!File.Exists(file)) throw new Refusal($"interior-fixtures: {path} is not on disk.");
                Match guid = Regex.Match(File.ReadAllText(file + ".meta"), @"(?m)^guid: ([0-9a-f]{32})\s*$");
                if (!guid.Success) throw new Refusal($"interior-fixtures: {path}.meta has no guid.");
                SceneYaml prefab = SceneYaml.Parse(File.ReadAllText(file).Replace("\r\n", "\n"));
                Doc rootTr = prefab.Docs.Single(d => d.ClassId == 4 && d.FieldRef("m_Father") == 0);
                Doc go = prefab.Require(rootTr.FieldRef("m_GameObject"), path + "'s root");
                var points = prefab.ComponentsOf(go)
                    .Where(d => d.ClassId == 114 && Regex.IsMatch(d.Text, @"(?m)^  _id: ")).ToList();
                if (points.Count > 1) throw new Refusal($"interior-fixtures: {path}'s root has {points.Count} components with an _id.");
                return new PrefabFacts
                {
                    Path = path, Name = go.Field("m_Name"), Guid = guid.Groups[1].Value,
                    GameObject = go.FileId, Transform = rootTr.FileId, Point = points.Count == 1 ? points[0].FileId : 0L,
                };
            }
        }

        /// <summary>The prefab a site stands, refused when the site names an interact id its root cannot carry.</summary>
        static PrefabFacts FactsFor(Site site, Func<string, PrefabFacts> prefabs)
        {
            PrefabFacts prefab = prefabs(site.Prefab);
            if (site.InteractId != null && prefab.Point == 0L)
                throw new Refusal($"interior-fixtures: {site.Prefab}'s root serializes no _id.");
            return prefab;
        }

        /// <summary>The root a scene's open-air sites hang under (at the origin, so local = world).</summary>
        public static string OpenAirRootFor(string scenePath) =>
            scenePath == StPetersScene ? StPetersOpenAirRootName : WashStationPlacement.NmcRootName;

        /// <summary>The St Peters open-air root (the camper's shore lead).</summary>
        public const string StPetersOpenAirRootName = "StPetersOpenAirFixtures";

        /// <summary>The allocator key of a site: its interact id, or its name when it has none.</summary>
        static string KeyOf(Site site) => site.InteractId ?? site.Name;

        /// <summary>Plan the scene's sites (every <see cref="Sites"/> row for <paramref name="scenePath"/>), one
        /// patch per root touched. Re-planning the patched text gives patches with no operations.</summary>
        public static List<LayerPatch> Plan(SceneYaml scene, string scenePath, Func<string, PrefabFacts> prefabs) =>
            Plan(scene, scenePath, prefabs, Sites);

        /// <summary>As <see cref="Plan(SceneYaml, string, Func{string, PrefabFacts})"/>, over any rows (tests).</summary>
        public static List<LayerPatch> Plan(SceneYaml scene, string scenePath, Func<string, PrefabFacts> prefabs, IReadOnlyList<Site> sites)
        {
            string step = StepFor(scenePath);
            var patches = new List<LayerPatch>();
            var open = sites.Where(s => s.Scene == scenePath && !s.IsInterior).ToList();
            foreach (var group in sites.Where(s => s.Scene == scenePath && s.IsInterior)
                                       .GroupBy(s => (s.BuildingRoot, s.Building)))
                patches.Add(PlanInterior(scene, step, group.ToList(), prefabs));
            if (open.Count > 0) patches.Add(PlanOpenAir(scene, step, open, prefabs));
            return patches;
        }

        /// <summary>The building's footprint on a level, from its serialized BuildingInterior.</summary>
        public static InteriorFootprint FootprintOf(SceneYaml scene, Site site, out Doc parentTr, out Doc rootGo)
        {
            rootGo = scene.RootNamed(site.BuildingRoot);
            Doc rootTr = scene.TransformOf(rootGo);
            Doc buildingGo = scene.Require(scene.ChildNamed(rootTr, site.Building).FileId, site.Building);
            Doc buildingTr = scene.TransformOf(buildingGo);
            Doc bi = scene.ComponentsOf(buildingGo).SingleOrDefault(d => d.IsScript("HiddenHarbours.World.BuildingInterior"))
                     ?? throw new Refusal($"interior-fixtures: {site.Building} has no BuildingInterior.");
            parentTr = scene.Require(bi.FieldRef(site.Level > 0 ? "_upperProps" : "_props"),
                                     site.Building + "'s furniture root");
            Vector2 building = WorldOf(scene, buildingTr);
            float upper = site.Level > 0 ? ParseFloat(bi.Field("_upperLevelY")) * site.Level : 0f;
            return new InteriorFootprint(building + new Vector2(0f, upper),
                ParseFloat(bi.Field("_widthMetres")), ParseFloat(bi.Field("_lengthMetres")),
                ParseInt(bi.Field("_facing")), ParseInt(bi.Field("_facings")), ParseFloat(bi.Field("_groundDepthScale")),
                bi.Field("_doorOnPlusY") == "1" ? 1f : -1f, ParseFloat(bi.Field("_doorAcrossMetres")));
        }

        /// <summary>The world point an interior site stands at, from the building's serialized footprint.</summary>
        public static Vector2 InteriorWorld(SceneYaml scene, Site site, out Doc parentTr, out Doc rootGo)
        {
            InteriorFootprint footprint = FootprintOf(scene, site, out parentTr, out rootGo);
            return footprint.ModelToWorld(ModelPoint(footprint, site));
        }

        /// <summary>A transform's world position by its chain of local positions; refuses a rotated or scaled
        /// ancestor, under which a local position would not be a world offset.</summary>
        public static Vector2 WorldOf(SceneYaml scene, Doc tr)
        {
            Vector2 at = Vector2.zero;
            for (Doc t = tr; t != null; )
            {
                if (t.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" || t.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                    throw new Refusal($"interior-fixtures: &{t.FileId} is rotated or scaled; a placed fixture would be off.");
                Vector3 p = ParseVec3(t.Field("m_LocalPosition"));
                at += new Vector2(p.x, p.y);
                long father = t.FieldRef("m_Father");
                t = father == 0 ? null : scene.Require(father, "a parent transform");
            }
            return at;
        }

        /// <summary>One building's sites, in one patch. Sites on both levels share the patch; each level's
        /// furniture root gains its own children.</summary>
        static LayerPatch PlanInterior(SceneYaml scene, string step, List<Site> sites, Func<string, PrefabFacts> prefabs)
        {
            Doc rootGo = scene.RootNamed(sites[0].BuildingRoot);
            var ids = new IdAllocator(scene);
            var patch = new LayerPatch(step, sites[0].BuildingRoot, rootGo.FileId);
            var childrenOf = new Dictionary<long, (Doc tr, List<long> before, List<long> after)>();
            foreach (Site site in sites)
            {
                PrefabFacts prefab = FactsFor(site, prefabs);
                Vector2 world = InteriorWorld(scene, site, out Doc parentTr, out _);
                Vector2 local = world - WorldOf(scene, parentTr);
                if (!childrenOf.TryGetValue(parentTr.FileId, out var kids))
                {
                    List<long> before = parentTr.FieldRefs("m_Children");
                    childrenOf[parentTr.FileId] = kids = (parentTr, before, new List<long>(before));
                }
                FindInstance(scene, kids.before, prefab, site.Name, out Doc pi, out Doc piTr);
                long piId = pi?.FileId ?? ids.Next($"{step}/{KeyOf(site)}/PrefabInstance");
                long trId = piTr?.FileId ?? ids.Next($"{step}/{KeyOf(site)}/Transform");
                Put(patch, pi, Instance(piId, parentTr.FileId, prefab, site.Name, local, site.InteractId), site.Name + " (PrefabInstance)");
                Put(patch, piTr, Stripped(trId, piId, prefab), site.Name + "/Transform (stripped)");
                if (!kids.after.Contains(trId)) kids.after.Add(trId);
                if (pi == null)
                    patch.Note(site.Name, "a room fixture", $"{site.InteractId} at model {At(site.At)} on level {site.Level} = world {At(world)}: {site.Why}");
            }
            foreach (var (tr, before, after) in childrenOf.Values)
                if (!before.SequenceEqual(after))
                    patch.Edit(tr, SetChildren(tr.Text, after), sites[0].Building + "'s furniture root/children");
            return patch.Seal(scene);
        }

        static LayerPatch PlanOpenAir(SceneYaml scene, string step, List<Site> sites, Func<string, PrefabFacts> prefabs)
        {
            string rootName = OpenAirRootFor(sites[0].Scene);
            Doc root = scene.Docs.SingleOrDefault(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == rootName &&
                                                       scene.TransformOf(d).FieldRef("m_Father") == 0);
            Doc rootTr = root != null ? scene.TransformOf(root) : null;
            if (rootTr != null) RequireIdentity(rootTr, rootName);
            var ids = new IdAllocator(scene);
            long go = root?.FileId ?? ids.Next($"{step}/{rootName}/GameObject");
            long tr = rootTr?.FileId ?? ids.Next($"{step}/{rootName}/Transform");
            var patch = new LayerPatch(step, rootName, go);
            List<long> standing = rootTr?.FieldRefs("m_Children") ?? new List<long>();
            var children = new List<long>(standing);
            foreach (Site site in sites)
            {
                PrefabFacts prefab = FactsFor(site, prefabs);
                FindInstance(scene, standing, prefab, site.Name, out Doc pi, out Doc piTr);
                long piId = pi?.FileId ?? ids.Next($"{step}/{KeyOf(site)}/PrefabInstance");
                long trId = piTr?.FileId ?? ids.Next($"{step}/{KeyOf(site)}/Transform");
                Put(patch, pi, Instance(piId, tr, prefab, site.Name, site.At, site.InteractId), site.Name + " (PrefabInstance)");
                Put(patch, piTr, Stripped(trId, piId, prefab), site.Name + "/Transform (stripped)");
                if (!children.Contains(trId)) children.Add(trId);
                if (pi == null) patch.Note(site.Name, "a room fixture", $"{site.InteractId} at {At(site.At)}: {site.Why}");
            }
            Put(patch, root, RootGameObject(go, tr, rootName), rootName);
            Put(patch, rootTr, RootTransform(tr, go, children), rootName + "/Transform");
            if (root == null)
            {
                Doc list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, tr), $"the scene's SceneRoots list ('{rootName}' joins it)");
                patch.Note(rootName, "a new root", "the open-air fixtures' parent, at the origin");
            }
            return patch.Seal(scene);
        }

        static void FindInstance(SceneYaml scene, List<long> children, PrefabFacts prefab, string name, out Doc pi, out Doc piTr)
        {
            pi = null; piTr = null;
            foreach (long c in children)
            {
                Doc t = scene.Require(c, "a child transform");
                if (!t.Stripped || ObjRef.Parse(t.Field("m_CorrespondingSourceObject")).Guid != prefab.Guid) continue;
                Doc instance = scene.Require(t.FieldRef("m_PrefabInstance"), "a room fixture's prefab instance");
                if (!instance.Text.Contains("      propertyPath: m_Name\n      value: " + name + "\n")) continue;
                if (pi != null) throw new Refusal($"interior-fixtures: two '{name}' instances of {prefab.Path}.");
                pi = instance; piTr = t;
            }
        }

        /// <summary>An instance as the editor writes one dropped under a parent: its name, its interact id
        /// and the root transform's ten records, ordered by target.</summary>
        static string Instance(long id, long parent, PrefabFacts prefab, string name, Vector2 at, string interactId)
        {
            var records = new List<(long target, string path, string value)>
            {
                (prefab.GameObject, "m_Name", Plain(name)),
                (prefab.Transform, "m_LocalPosition.x", F(at.x)), (prefab.Transform, "m_LocalPosition.y", F(at.y)),
                (prefab.Transform, "m_LocalPosition.z", "0"),
                (prefab.Transform, "m_LocalRotation.w", "1"), (prefab.Transform, "m_LocalRotation.x", "0"),
                (prefab.Transform, "m_LocalRotation.y", "0"), (prefab.Transform, "m_LocalRotation.z", "0"),
                (prefab.Transform, "m_LocalEulerAnglesHint.x", "0"), (prefab.Transform, "m_LocalEulerAnglesHint.y", "0"),
                (prefab.Transform, "m_LocalEulerAnglesHint.z", "0"),
            };
            if (interactId != null) records.Add((prefab.Point, "_id", Plain(interactId)));
            var lines = new List<string>
            {
                $"--- !u!1001 &{Id(id)}", "PrefabInstance:", "  m_ObjectHideFlags: 0", "  serializedVersion: 2",
                "  m_Modification:", "    serializedVersion: 3", $"    m_TransformParent: {Ref(parent)}", "    m_Modifications:",
            };
            foreach (var r in records.OrderBy(r => r.target))
            {
                lines.Add($"    - target: {{fileID: {Id(r.target)}, guid: {prefab.Guid}, type: 3}}");
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

        static string Stripped(long id, long instance, PrefabFacts prefab) => string.Join("\n",
            $"--- !u!4 &{Id(id)} stripped",
            "Transform:",
            $"  m_CorrespondingSourceObject: {{fileID: {Id(prefab.Transform)}, guid: {prefab.Guid}, type: 3}}",
            $"  m_PrefabInstance: {Ref(instance)}",
            "  m_PrefabAsset: {fileID: 0}");

        const string Unlinked = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}";

        static string RootGameObject(long id, long tr, string name) => string.Join("\n",
            $"--- !u!1 &{Id(id)}", "GameObject:", Unlinked, "  serializedVersion: 6", "  m_Component:",
            $"  - component: {Ref(tr)}", "  m_Layer: 0", "  m_Name: " + name, "  m_TagString: Untagged",
            "  m_Icon: {fileID: 0}", "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0", "  m_IsActive: 1");

        static string RootTransform(long id, long go, IList<long> children)
        {
            var lines = new List<string>
            {
                $"--- !u!4 &{Id(id)}", "Transform:", Unlinked, $"  m_GameObject: {Ref(go)}", "  serializedVersion: 2",
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", "  m_LocalPosition: {x: 0, y: 0, z: 0}",
                "  m_LocalScale: {x: 1, y: 1, z: 1}", "  m_ConstrainProportionsScale: 0",
                children.Count == 0 ? "  m_Children: []" : "  m_Children:",
            };
            foreach (long c in children) lines.Add($"  - {Ref(c)}");
            lines.Add("  m_Father: {fileID: 0}");
            lines.Add("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");
            return string.Join("\n", lines);
        }

        static string SetChildren(string text, IList<long> ids)
        {
            var lines = new List<string>(text.Split('\n'));
            int at = lines.FindIndex(l => l == "  m_Children:" || l == "  m_Children: []");
            if (at < 0) throw new Refusal($"interior-fixtures: no m_Children in {lines[0]}.");
            int end = at + 1;
            while (end < lines.Count && lines[end].StartsWith("  - ", StringComparison.Ordinal)) end++;
            lines.RemoveRange(at, end - at);
            var block = new List<string> { ids.Count == 0 ? "  m_Children: []" : "  m_Children:" };
            foreach (long id in ids) block.Add($"  - {Ref(id)}");
            lines.InsertRange(at, block);
            return string.Join("\n", lines);
        }

        static void Put(LayerPatch patch, Doc before, string after, string name)
        {
            if (before == null) patch.Add(after, name); else patch.Edit(before, after, name);
        }

        static string Id(long v) => v.ToString(CultureInfo.InvariantCulture);
        static string Ref(long v) => "{fileID: " + Id(v) + "}";
        static string At(Vector2 p) => $"({F(p.x)}, {F(p.y)})";

        static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Trim() != s || s.Contains(": ") || s.Contains(" #") || "!&*|>'\"%@`#,[]{}".IndexOf(s[0]) >= 0)
                throw new Refusal($"interior-fixtures: '{s}' would need quoting in the scene.");
            return s;
        }

        // ---- running it ----------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/World/Room Fixtures/Write the Patches (dry run)")]
        public static void DryRun() => Run(false);

        [MenuItem("Hidden Harbours/World/Room Fixtures/Apply the Patches")]
        public static void Apply() => Run(true);

        /// <summary>Plan (and with <paramref name="apply"/> write) every scene's patches. Refuses an open scene.</summary>
        public static List<LayerPatch> Run(bool apply)
        {
            string project = Directory.GetCurrentDirectory();
            var cache = new Dictionary<string, PrefabFacts>();
            PrefabFacts Facts(string p) => cache.TryGetValue(p, out PrefabFacts f) ? f : cache[p] = PrefabFacts.Read(project, p);
            var all = new List<LayerPatch>();
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            foreach (string path in Sites.Select(s => s.Scene).Distinct())
            {
                if (apply)
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                        if (SceneManager.GetSceneAt(i).path == path)
                            throw new Refusal($"{path} is open. Close it before applying text patches.");
                string before = File.ReadAllText(path);
                SceneYaml scene = SceneYaml.Parse(before);
                List<LayerPatch> patches = Plan(scene, path, Facts);
                foreach (LayerPatch p in patches)
                {
                    File.WriteAllText(Path.Combine(PatchFolder, $"{StepFor(path)}-{p.Root}.yaml"), p.ToYaml(), utf8);
                    scene = p.Apply(scene);
                }
                all.AddRange(patches);
                string after = scene.Render();
                if (apply && after != before) File.WriteAllText(path, after, utf8);
            }
            if (apply) AssetDatabase.Refresh();
            Debug.Log($"[InteriorFixturePlacement] {(apply ? "Applied" : "Dry run")}:\n" + string.Join("\n", all.Select(p => p.Summary())));
            return all;
        }
    }
}
#endif
