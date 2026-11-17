#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE FIRST PLACED WASH STATIONS</b> (washrooms plan sections 3 and 4): two prefabs and the named
    /// text patch that stands one instance of each in its region scene.
    ///
    /// <list type="bullet">
    /// <item><description><b>St Peters, Aunt Ginny's cottage, the player's room upstairs:</b> a washstand
    /// (<c>washstation.sink</c>). The earliest wash the player can reach: he sleeps in the next bed.</description></item>
    /// <item><description><b>Nine Mile Creek, the wharf's land side:</b> a rinse shower
    /// (<c>washstation.shower</c>), where the plan says a wharf shower naturally belongs, a few steps from
    /// the harbourmaster's office.</description></item>
    /// </list>
    ///
    /// <para><b>Why a text patch and not "open the scene and save it".</b> Opening and re-saving either
    /// scene in the editor rewrites most of its 5 to 13 MB (measured: ~150k lines each way), so these big
    /// scenes are only ever changed by named patches (N1 decision 1(b), <see cref="StPetersLayerRefresh"/>).
    /// This step is one of those: it plans from the committed scene text, adds one prefab instance per site
    /// (the stripped transform and its parent's child list with it) and re-plans to an empty patch.</para>
    ///
    /// <para><b>The interior site is placed by MODEL coordinate</b>, the frame every furnishing in
    /// <see cref="StPetersInteriors"/> uses, through the cottage's own serialized footprint
    /// (<see cref="InteriorFootprint"/>), so it lands where the room picture says even if the cottage is
    /// re-faced. It hangs under the upper storey's furniture root, which <see cref="BuildingInterior"/>
    /// switches on only while the player is upstairs; the station therefore registers only then.
    /// <see cref="StPetersGinnyPlot"/> calls <see cref="StandInterior"/> after it stands the cottage, so a
    /// full rebuild keeps the washstand.</para>
    ///
    /// <para><b>Placeholder art, said plainly.</b> No basin or shower art exists. The washstand wears the
    /// catch kit's empty galvanised pail; the shower is that pail under a grey post and head made of the
    /// greybox <c>Square.png</c>. No new binary art.</para>
    /// </summary>
    public static class WashStationPlacement
    {
        public const string Step = "wash-stations-01";
        public const string PatchFolder = "artifacts/wash-stations";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Washrooms";
        public const string WashStandPrefab = PrefabFolder + "/WashStand.prefab";
        public const string WharfShowerPrefab = PrefabFolder + "/WharfShower.prefab";
        public const string StPetersScene = "Assets/_Project/Scenes/StPeters.unity";
        public const string NineMileCreekScene = "Assets/_Project/Scenes/NineMileCreek.unity";

        /// <summary>The new root the Nine Mile Creek station hangs under (at the origin, so local = world).</summary>
        public const string NmcRootName = "NmcWashStations";

        const string PailSheet = "Assets/_Project/Art/Fishing/Storage/Bucket_pail_empty.png";
        const string SquareSprite = "Assets/_Project/Art/Sprites/Square.png";

        /// <summary>The pail cell the placeholders wear (d0: the rig's first facing).</summary>
        const string PailCell = "Bucket_pail_empty_d0_f0";

        // ---- the sites -----------------------------------------------------------------------------

        /// <summary>One placed station: which scene, which prefab, where, and what its interact id is.</summary>
        public readonly struct Site
        {
            public readonly string Scene, Prefab, Name, InteractId, Why;

            /// <summary>Interior sites: the root GameObject that holds the building, and the building's name
            /// under it. Null for an open-air site.</summary>
            public readonly string BuildingRoot, Building;

            /// <summary>Interior: the ROOM's model metres (StPetersInteriors' frame) on <see cref="Level"/>.
            /// Open air: world metres under <see cref="NmcRootName"/>.</summary>
            public readonly Vector2 At;

            public readonly int Level;

            public Site(string scene, string prefab, string name, string interactId, Vector2 at, string why,
                        string buildingRoot = null, string building = null, int level = 0)
            {
                Scene = scene; Prefab = prefab; Name = name; InteractId = interactId; At = at; Why = why;
                BuildingRoot = buildingRoot; Building = building; Level = level;
            }

            public bool IsInterior => !string.IsNullOrEmpty(BuildingRoot);
        }

        /// <summary>The ginny-cottage washstand's interact id.</summary>
        public const string GinnyWashstandId = "fixture.st_peters.ginny_cottage.washstand";

        /// <summary>The wharf shower's interact id.</summary>
        public const string NmcWharfShowerId = "fixture.nmc.wharf.shower";

        public static readonly Site[] Sites =
        {
            new Site(StPetersScene, WashStandPrefab, "WashStand", GinnyWashstandId, new Vector2(0.45f, 3.55f),
                     "the player's room upstairs at Ginny's: against the front wall, between the landing door " +
                     "and the head of his bed, clear of the doorway lane and the wardrobe",
                     StPetersGinnyPlot.RootName, StPetersGinnyPlot.CottageKey, level: 1),
            new Site(NineMileCreekScene, WharfShowerPrefab, "WharfShower", NmcWharfShowerId, new Vector2(90.2f, 108.2f),
                     "the wharf's land side, on the open ground between the harbourmaster's office and the " +
                     "picnic table: the first dry ground a skipper crosses coming up off the boats"),
        };

        // ---- prefabs -------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/World/Wash Stations/Build the Two Prefabs")]
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            Sprite pail = AssetDatabase.LoadAllAssetsAtPath(PailSheet).OfType<Sprite>().FirstOrDefault(s => s.name == PailCell);
            Sprite square = AssetDatabase.LoadAllAssetsAtPath(SquareSprite).OfType<Sprite>().FirstOrDefault();
            if (pail == null) Debug.LogWarning($"[WashStationPlacement] No '{PailCell}' in {PailSheet}: the stations stand invisible.");
            if (square == null) Debug.LogWarning($"[WashStationPlacement] No sprite in {SquareSprite}: the shower has no post.");

            var stand = new GameObject("WashStand");
            try
            {
                AddArt(stand, pail, Color.white, Vector2.zero, Vector2.one, 0f);
                AddFoot(stand, 0.3f);
                stand.AddComponent<WashStationPoint>().Configure(
                    "fixture.washstand", "washstation.sink", reachMeters: 1.2f, walkAwaySlackMeters: 0.6f,
                    WorldStrings.WashAtBasin, WorldStrings.WashBeginBasin, WorldStrings.WashDoneBasin);
                PrefabUtility.SaveAsPrefabAsset(stand, WashStandPrefab);
            }
            finally { Object.DestroyImmediate(stand); }

            var shower = new GameObject("WharfShower");
            try
            {
                AddArt(shower, pail, Color.white, Vector2.zero, Vector2.one, 0f);
                var post = new GameObject("Post");
                post.transform.SetParent(shower.transform, false);
                // A 0.15 x 2.3 m grey pipe standing behind the pail; it sorts by the pail's foot (+5 cm, so
                // just behind it).
                AddArt(post, square, new Color(0.55f, 0.57f, 0.6f), new Vector2(0f, 1.15f), new Vector2(0.3f, 4.6f), -1.1f);
                var head = new GameObject("Head");
                head.transform.SetParent(shower.transform, false);
                AddArt(head, square, new Color(0.45f, 0.47f, 0.5f), new Vector2(0.18f, 2.25f), new Vector2(0.9f, 0.25f), -2.2f);
                AddFoot(shower, 0.35f);
                shower.AddComponent<WashStationPoint>().Configure(
                    "fixture.wharf_shower", "washstation.shower", reachMeters: 1.3f, walkAwaySlackMeters: 0.6f,
                    WorldStrings.WashAtShower, WorldStrings.WashBeginShower, WorldStrings.WashDoneShower);
                PrefabUtility.SaveAsPrefabAsset(shower, WharfShowerPrefab);
            }
            finally { Object.DestroyImmediate(shower); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[WashStationPlacement] Built {WashStandPrefab} and {WharfShowerPrefab}.");
        }

        static void AddArt(GameObject go, Sprite sprite, Color tint, Vector2 local, Vector2 scale, float pivotYOffset)
        {
            go.transform.localPosition = new Vector3(local.x, local.y, 0f);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            var sort = go.AddComponent<YSortSprite>();
            if (pivotYOffset != 0f)
            {
                var so = new SerializedObject(sort);
                so.FindProperty("_pivotYOffset").floatValue = pivotYOffset;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>A small solid foot so the player stands AT the station, not in it.</summary>
        static void AddFoot(GameObject go, float radius)
        {
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = radius;
        }

        // ---- the live placement (the builder's path) -----------------------------------------------

        /// <summary>
        /// Stand every interior site whose building is <paramref name="buildingGo"/>: called by the region
        /// builder right after it stands the building's interior, so a rebuild keeps the station. The same
        /// <see cref="Sites"/> row and the same footprint maths as the text patch.
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
                    Debug.LogWarning($"[WashStationPlacement] {site.Prefab} is not built; run Build the Two Prefabs.");
                    continue;
                }
                Transform old = parent.Find(site.Name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = site.Name;
                Vector2 world = interior.FootprintFor(site.Level).ModelToWorld(site.At);
                go.transform.position = new Vector3(world.x, world.y, 0f);
                var point = go.GetComponent<WashStationPoint>();
                var so = new SerializedObject(point);
                so.FindProperty("_id").stringValue = site.InteractId;
                so.ApplyModifiedPropertiesWithoutUndo();
                placed++;
            }
            return placed;
        }

        // ---- the text patch ------------------------------------------------------------------------

        /// <summary>One prefab as its file holds it: the GUID and the root's three documents' ids.</summary>
        public sealed class PrefabFacts
        {
            public string Path, Name, Guid;
            public long GameObject, Transform, Point;

            public static PrefabFacts Read(string projectRoot, string path)
            {
                string file = System.IO.Path.Combine(projectRoot, path);
                if (!File.Exists(file)) throw new Refusal($"{Step}: {path} is not on disk; build the prefabs first.");
                Match guid = Regex.Match(File.ReadAllText(file + ".meta"), @"(?m)^guid: ([0-9a-f]{32})\s*$");
                if (!guid.Success) throw new Refusal($"{Step}: {path}.meta has no guid.");
                SceneYaml prefab = SceneYaml.Parse(File.ReadAllText(file).Replace("\r\n", "\n"));
                Doc rootTr = prefab.Docs.Single(d => d.ClassId == 4 && d.FieldRef("m_Father") == 0);
                Doc go = prefab.Require(rootTr.FieldRef("m_GameObject"), path + "'s root");
                Doc point = prefab.ComponentsOf(go).SingleOrDefault(d => d.IsScript("HiddenHarbours.World.WashStationPoint"))
                            ?? throw new Refusal($"{Step}: {path}'s root carries no WashStationPoint.");
                return new PrefabFacts
                {
                    Path = path, Name = go.Field("m_Name"), Guid = guid.Groups[1].Value,
                    GameObject = go.FileId, Transform = rootTr.FileId, Point = point.FileId,
                };
            }
        }

        /// <summary>Plan the scene's sites (every <see cref="Sites"/> row for <paramref name="scenePath"/>), one
        /// patch per root touched. Re-planning the patched text gives patches with no operations.</summary>
        public static List<LayerPatch> Plan(SceneYaml scene, string scenePath, Func<string, PrefabFacts> prefabs)
        {
            var patches = new List<LayerPatch>();
            var open = Sites.Where(s => s.Scene == scenePath && !s.IsInterior).ToList();
            foreach (Site s in Sites.Where(s => s.Scene == scenePath && s.IsInterior))
                patches.Add(PlanInterior(scene, s, prefabs(s.Prefab)));
            if (open.Count > 0) patches.Add(PlanOpenAir(scene, open, prefabs));
            return patches;
        }

        /// <summary>The world point an interior site stands at, from the building's serialized footprint.</summary>
        public static Vector2 InteriorWorld(SceneYaml scene, Site site, out Doc parentTr, out Doc rootGo)
        {
            rootGo = scene.RootNamed(site.BuildingRoot);
            Doc rootTr = scene.TransformOf(rootGo);
            Doc buildingGo = scene.Require(scene.ChildNamed(rootTr, site.Building).FileId, site.Building);
            Doc buildingTr = scene.TransformOf(buildingGo);
            Doc bi = scene.ComponentsOf(buildingGo).SingleOrDefault(d => d.IsScript("HiddenHarbours.World.BuildingInterior"))
                     ?? throw new Refusal($"{Step}: {site.Building} has no BuildingInterior.");
            parentTr = scene.Require(bi.FieldRef(site.Level > 0 ? "_upperProps" : "_props"),
                                     site.Building + "'s furniture root");
            Vector2 building = WorldOf(scene, buildingTr);
            float upper = site.Level > 0 ? ParseFloat(bi.Field("_upperLevelY")) * site.Level : 0f;
            var footprint = new InteriorFootprint(building + new Vector2(0f, upper),
                ParseFloat(bi.Field("_widthMetres")), ParseFloat(bi.Field("_lengthMetres")),
                ParseInt(bi.Field("_facing")), ParseInt(bi.Field("_facings")), ParseFloat(bi.Field("_groundDepthScale")),
                bi.Field("_doorOnPlusY") == "1" ? 1f : -1f, ParseFloat(bi.Field("_doorAcrossMetres")));
            return footprint.ModelToWorld(site.At);
        }

        /// <summary>A transform's world position by its chain of local positions; refuses a rotated or scaled
        /// ancestor, under which a local position would not be a world offset.</summary>
        public static Vector2 WorldOf(SceneYaml scene, Doc tr)
        {
            Vector2 at = Vector2.zero;
            for (Doc t = tr; t != null; )
            {
                if (t.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" || t.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                    throw new Refusal($"{Step}: &{t.FileId} is rotated or scaled; a placed station would be off.");
                Vector3 p = ParseVec3(t.Field("m_LocalPosition"));
                at += new Vector2(p.x, p.y);
                long father = t.FieldRef("m_Father");
                t = father == 0 ? null : scene.Require(father, "a parent transform");
            }
            return at;
        }

        static LayerPatch PlanInterior(SceneYaml scene, Site site, PrefabFacts prefab)
        {
            Vector2 world = InteriorWorld(scene, site, out Doc parentTr, out Doc rootGo);
            Vector2 local = world - WorldOf(scene, parentTr);
            var ids = new IdAllocator(scene);
            var patch = new LayerPatch(Step, site.BuildingRoot, rootGo.FileId);
            List<long> children = parentTr.FieldRefs("m_Children");
            FindInstance(scene, children, prefab, site.Name, out Doc pi, out Doc piTr);
            long piId = pi?.FileId ?? ids.Next($"{Step}/{site.InteractId}/PrefabInstance");
            long trId = piTr?.FileId ?? ids.Next($"{Step}/{site.InteractId}/Transform");
            Put(patch, pi, Instance(piId, parentTr.FileId, prefab, site.Name, local, site.InteractId), site.Name + " (PrefabInstance)");
            Put(patch, piTr, Stripped(trId, piId, prefab), site.Name + "/Transform (stripped)");
            if (!children.Contains(trId))
            {
                var next = new List<long>(children) { trId };
                patch.Edit(parentTr, SetChildren(parentTr.Text, next), site.Building + "'s furniture root/children");
            }
            if (pi == null)
                patch.Note(site.Name, "a wash station", $"{site.InteractId} at model {At(site.At)} on level {site.Level} = world {At(world)}: {site.Why}");
            return patch.Seal(scene);
        }

        static LayerPatch PlanOpenAir(SceneYaml scene, List<Site> sites, Func<string, PrefabFacts> prefabs)
        {
            Doc root = scene.Docs.SingleOrDefault(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == NmcRootName &&
                                                       scene.TransformOf(d).FieldRef("m_Father") == 0);
            Doc rootTr = root != null ? scene.TransformOf(root) : null;
            if (rootTr != null) RequireIdentity(rootTr, NmcRootName);
            var ids = new IdAllocator(scene);
            long go = root?.FileId ?? ids.Next($"{Step}/{NmcRootName}/GameObject");
            long tr = rootTr?.FileId ?? ids.Next($"{Step}/{NmcRootName}/Transform");
            var patch = new LayerPatch(Step, NmcRootName, go);
            List<long> standing = rootTr?.FieldRefs("m_Children") ?? new List<long>();
            var children = new List<long>(standing);
            foreach (Site site in sites)
            {
                PrefabFacts prefab = prefabs(site.Prefab);
                FindInstance(scene, standing, prefab, site.Name, out Doc pi, out Doc piTr);
                long piId = pi?.FileId ?? ids.Next($"{Step}/{site.InteractId}/PrefabInstance");
                long trId = piTr?.FileId ?? ids.Next($"{Step}/{site.InteractId}/Transform");
                Put(patch, pi, Instance(piId, tr, prefab, site.Name, site.At, site.InteractId), site.Name + " (PrefabInstance)");
                Put(patch, piTr, Stripped(trId, piId, prefab), site.Name + "/Transform (stripped)");
                if (!children.Contains(trId)) children.Add(trId);
                if (pi == null) patch.Note(site.Name, "a wash station", $"{site.InteractId} at {At(site.At)}: {site.Why}");
            }
            Put(patch, root, RootGameObject(go, tr), NmcRootName);
            Put(patch, rootTr, RootTransform(tr, go, children), NmcRootName + "/Transform");
            if (root == null)
            {
                Doc list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, tr), $"the scene's SceneRoots list ('{NmcRootName}' joins it)");
                patch.Note(NmcRootName, "a new root", "the wash stations' parent, at the origin");
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
                Doc instance = scene.Require(t.FieldRef("m_PrefabInstance"), "a wash station's prefab instance");
                if (!instance.Text.Contains("      propertyPath: m_Name\n      value: " + name + "\n")) continue;
                if (pi != null) throw new Refusal($"{Step}: two '{name}' instances of {prefab.Path}.");
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
                (prefab.Point, "_id", Plain(interactId)),
            };
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

        static string RootGameObject(long id, long tr) => string.Join("\n",
            $"--- !u!1 &{Id(id)}", "GameObject:", Unlinked, "  serializedVersion: 6", "  m_Component:",
            $"  - component: {Ref(tr)}", "  m_Layer: 0", "  m_Name: " + NmcRootName, "  m_TagString: Untagged",
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
            if (at < 0) throw new Refusal($"{Step}: no m_Children in {lines[0]}.");
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
                throw new Refusal($"{Step}: '{s}' would need quoting in the scene.");
            return s;
        }

        // ---- running it ----------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/World/Wash Stations/Write the Patches (dry run)")]
        public static void DryRun() => Run(false);

        [MenuItem("Hidden Harbours/World/Wash Stations/Apply the Patches")]
        public static void Apply() => Run(true);

        /// <summary>Plan (and with <paramref name="apply"/> write) both scenes' patches. Refuses an open scene.</summary>
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
                    File.WriteAllText(Path.Combine(PatchFolder, $"{Step}-{p.Root}.yaml"), p.ToYaml(), utf8);
                    scene = p.Apply(scene);
                }
                all.AddRange(patches);
                string after = scene.Render();
                if (apply && after != before) File.WriteAllText(path, after, utf8);
            }
            if (apply) AssetDatabase.Refresh();
            Debug.Log($"[WashStationPlacement] {(apply ? "Applied" : "Dry run")}:\n" + string.Join("\n", all.Select(p => p.Summary())));
            return all;
        }
    }
}
#endif
