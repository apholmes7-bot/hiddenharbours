#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The dwelling prefab builder</b> (ADR 0060 section 6, PR 5): one prefab per baked dwelling variant,
    /// generated from its committed <see cref="DwellingMeshDef"/> and never hand-edited.
    ///
    /// <para><b>What the prefab holds.</b> The root carries <see cref="BuildingInterior"/> (its inside test is
    /// the def's floor outline, <see cref="BuildingInterior.ConfigureDwelling"/>), the mesh reveal
    /// (<see cref="DwellingCutaway"/>) and the site and mesh host (<see cref="DwellingSite"/>, which installs
    /// the facet mesh and writes its sort order once at enable, in play). A <c>Walls</c> child holds the
    /// edge collider along the floor outline with the door gap cut. A <c>Fixtures</c> child holds one child
    /// per anchor, named by its anchor id, carrying the component the verb table names
    /// (<see cref="DwellingFixtureVerbs"/>), bound to the Def the anchor names. Nothing is drawn by the
    /// prefab itself: the shell, the room and every fixture's shape are the one dwelling mesh.</para>
    ///
    /// <para><b>The prefab claims no save row.</b> Every store's save key is the SITE's (ADR 0060 section 4):
    /// the prefab leaves each rack's id empty, and <see cref="ApplySite"/> writes the
    /// <see cref="HomeStoragePlan"/> keys of one site into one placed instance. Two homes from one prefab
    /// therefore get two keys. Placing the instance in a region is the walk-in step's (PR 6), not this
    /// file's.</para>
    /// </summary>
    public static class DwellingPrefabBuilder
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/Dwellings";
        public const string WallsName = "Walls";
        public const string FixturesName = "Fixtures";

        /// <summary>The wall collider's radius, metres: the half-thickness of a camper's liner, about two
        /// pixels at 32 px to the metre. Layout, not balance. It is also the inside test's inset (the
        /// interior's wall thickness), so "inside" means past the liner and never deeper than a body can
        /// stand.</summary>
        public const float WallRadiusMetres = 0.05f;

        /// <summary>The bed's place name for the rest-save notice, per dwelling id; the fallback otherwise.</summary>
        static readonly Dictionary<string, string> BedPlaceNames = new Dictionary<string, string>
        {
            { "dwelling.camper_clipper", "your bunk in the camper" },
        };
        const string FallbackBedPlaceName = "your bed";

        const string DataRoot = "Assets/_Project/Data";

        // ---- naming -------------------------------------------------------------------------------------

        /// <summary>The def id without its type prefix: <c>dwelling.camper_clipper</c> is <c>camper_clipper</c>.</summary>
        public static string ShortKey(DwellingMeshDef def)
        {
            string id = def != null ? def.Id ?? "" : "";
            int dot = id.IndexOf('.');
            return dot >= 0 ? id.Substring(dot + 1) : id;
        }

        /// <summary>The prefab path of a def: <c>CamperClipperDwelling.prefab</c>.</summary>
        public static string PrefabPathFor(DwellingMeshDef def) =>
            $"{PrefabFolder}/{InteriorFixturePlacement.NameOf(ShortKey(def))}Dwelling.prefab";

        /// <summary>An interactable id: <c>fixture.&lt;owner&gt;.&lt;anchor&gt;</c>. The prefab's owner is the
        /// dwelling's short key; a placed instance's is its site key.</summary>
        public static string FixtureId(string owner, string anchorId) => $"fixture.{owner}.{anchorId}";

        // ---- the menu -----------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/World/Dwellings/Build the Dwelling Prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabFolder);
            foreach (DwellingRigFleet.MeshDwelling d in DwellingRigFleet.MeshBaked)
            {
                var def = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(d.MeshAssetPath);
                if (def == null)
                {
                    Debug.LogError($"[DwellingPrefabBuilder] '{d.Key}': no dwelling mesh at {d.MeshAssetPath}; bake it first.");
                    continue;
                }
                GameObject root = Build(def, out List<string> problems);
                try
                {
                    if (root == null)
                    {
                        Debug.LogError($"[DwellingPrefabBuilder] '{def.Id}' refused: {string.Join("; ", problems)}");
                        continue;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPathFor(def));
                    Debug.Log($"[DwellingPrefabBuilder] built {PrefabPathFor(def)}: {def.Anchors.Length} fixtures.");
                }
                finally { if (root != null) Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }

        // ---- the build ----------------------------------------------------------------------------------

        /// <summary>
        /// Build the prefab's hierarchy in memory (the caller saves and destroys it). Returns null, with the
        /// reasons, when the def cannot be entered, a verb is not in the table, or a named Def is not on disk:
        /// a home with a fixture bound to nothing is refused, not shipped half-wired.
        /// </summary>
        public static GameObject Build(DwellingMeshDef def, out List<string> problems)
        {
            problems = new List<string>();
            if (def == null) { problems.Add("the def is null"); return null; }
            string defProblem = def.FirstProblem();
            if (defProblem != null) { problems.Add(defProblem); return null; }
            foreach (DwellingFixtureAnchor a in def.Anchors)
                if (!DwellingFixtureVerbs.TryGet(a.Verb, out _))
                    problems.Add($"anchor '{a.AnchorId}': verb '{a.Verb}' is not in the verb table");
            if (problems.Count > 0) return null;

            string owner = ShortKey(def);
            var root = new GameObject(InteriorFixturePlacement.NameOf(owner) + "Dwelling");
            try
            {
                var interior = root.AddComponent<BuildingInterior>();
                Vector2 extent = Extent(def);
                float depthScale = Mathf.Sin(def.ElevationDeg * Mathf.Deg2Rad);
                interior.Configure(null, null, null, extent.x * 2f, extent.y * 2f,
                                   0, DwellingSiteLayout.FacingsPerTurn, depthScale,
                                   WallRadiusMetres, def.Threshold.ClearWidthMeters + 2f * WallRadiusMetres);
                if (!interior.ConfigureDwelling(def))
                {
                    problems.Add("BuildingInterior refused the def's room");
                    Object.DestroyImmediate(root);
                    return null;
                }
                root.AddComponent<DwellingCutaway>();
                var site = root.AddComponent<DwellingSite>();

                var wallsGo = new GameObject(WallsName);
                wallsGo.transform.SetParent(root.transform, false);
                var walls = wallsGo.AddComponent<EdgeCollider2D>();

                var fixtures = new GameObject(FixturesName).transform;
                fixtures.SetParent(root.transform, false);
                var points = new List<DwellingSite.AnchorPoint>(def.Anchors.Length);
                foreach (DwellingFixtureAnchor a in def.Anchors)
                {
                    var child = new GameObject(a.AnchorId);
                    child.transform.SetParent(fixtures, false);
                    DwellingFixtureVerbs.TryGet(a.Verb, out DwellingFixtureVerbs.Row row);
                    string problem = Bind(child, def, a, row, owner, interior);
                    if (problem != null) problems.Add($"anchor '{a.AnchorId}': {problem}");
                    points.Add(new DwellingSite.AnchorPoint(a.AnchorId, child.transform));
                }

                site.Configure(walls, points.ToArray());
                site.Layout();
                if (problems.Count > 0) { Object.DestroyImmediate(root); return null; }
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        /// <summary>Add and wire the component <paramref name="row"/> names. Null, or the reason it cannot.</summary>
        static string Bind(GameObject go, DwellingMeshDef def, in DwellingFixtureAnchor a,
                           in DwellingFixtureVerbs.Row row, string owner, BuildingInterior interior)
        {
            string defId = DwellingFixtureVerbs.DefIdFor(a, row);
            string id = FixtureId(owner, a.AnchorId);
            switch (row.Binding)
            {
                case DwellingFixtureVerbs.Binding.Door:
                    return null;

                case DwellingFixtureVerbs.Binding.Bed:
                {
                    var bed = go.AddComponent<InteriorBed>();
                    if (!BedPlaceNames.TryGetValue(def.Id, out string place)) place = FallbackBedPlaceName;
                    using var so = new SerializedObject(bed);
                    so.FindProperty("_id").stringValue = id;
                    so.FindProperty("_isPlayerBed").boolValue = true;
                    so.FindProperty("_ownerName").stringValue = "";
                    so.FindProperty("_placeName").stringValue = place;
                    so.FindProperty("_interior").objectReferenceValue = interior;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return null;
                }

                case DwellingFixtureVerbs.Binding.CatchFreezer:
                {
                    FreezerDef freezer = Find<FreezerDef>(defId, d => d.Id);
                    if (freezer == null) return $"no FreezerDef '{defId}'";
                    go.AddComponent<CamperFreezer>().Configure(freezer);
                    return null;
                }

                case DwellingFixtureVerbs.Binding.Stowage:
                {
                    StowageDef stowage = Find<StowageDef>(defId, d => d.Id);
                    if (stowage == null) return $"no StowageDef '{defId}'";
                    // The prefab's own id stays empty: no prefab ever claims a save row (ApplySite writes it).
                    go.AddComponent<StowageRack>().Configure(stowage, null, "");
                    return null;
                }

                case DwellingFixtureVerbs.Binding.FoodStore:
                {
                    KitchenStorageDef store = Find<KitchenStorageDef>(defId, d => d.Id);
                    if (store == null) return $"no KitchenStorageDef '{defId}'";
                    go.AddComponent<KitchenStoreFixture>().Configure(store);
                    return null;
                }

                case DwellingFixtureVerbs.Binding.CookingStation:
                {
                    CookingStationDef station = Find<CookingStationDef>(defId, d => d.Id);
                    if (station == null) return $"no CookingStationDef '{defId}'";
                    go.AddComponent<CookingStationFixture>().Configure(station, id);
                    return null;
                }

                case DwellingFixtureVerbs.Binding.WashStation:
                {
                    WashStationDef wash = Find<WashStationDef>(defId, d => d.Id);
                    if (wash == null) return $"no WashStationDef '{defId}'";
                    if (!wash.Washes) return $"'{defId}' does not wash (a sit-only Def binds to the seat)";
                    var point = go.AddComponent<WashStationPoint>();
                    using var so = new SerializedObject(point);
                    so.FindProperty("_id").stringValue = id;
                    so.FindProperty("_stationId").stringValue = defId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return null;
                }

                case DwellingFixtureVerbs.Binding.Seat:
                {
                    // A seat with no Def (the settee) is bound and stays unavailable until a seat Def exists:
                    // SitPoint offers itself only for a shipped sit-only Def.
                    if (!string.IsNullOrEmpty(defId))
                    {
                        WashStationDef seat = Find<WashStationDef>(defId, d => d.Id);
                        if (seat == null) return $"no WashStationDef '{defId}'";
                        if (!seat.SitOnly) return $"'{defId}' is not a sit-only Def";
                    }
                    var sit = go.AddComponent<SitPoint>();
                    using var so = new SerializedObject(sit);
                    so.FindProperty("_id").stringValue = id;
                    so.FindProperty("_stationId").stringValue = defId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return null;
                }
            }
            return $"binding {row.Binding} has no builder case";
        }

        /// <summary>The half-extents (x across, y along), metres, of every level's floor: the rectangle the
        /// interior's sprite-path fields describe, kept for the code that still asks for one.</summary>
        static Vector2 Extent(DwellingMeshDef def)
        {
            Vector2 e = Vector2.zero;
            foreach (DwellingLevel level in def.Levels)
                if (level.Floor != null)
                    foreach (Vector2 p in level.Floor)
                        e = Vector2.Max(e, new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)));
            return e;
        }

        // ---- the site -----------------------------------------------------------------------------------

        /// <summary>The fixture bindings that hold a remembered store.</summary>
        public static bool IsStore(DwellingFixtureVerbs.Binding b) =>
            b == DwellingFixtureVerbs.Binding.CatchFreezer || b == DwellingFixtureVerbs.Binding.Stowage ||
            b == DwellingFixtureVerbs.Binding.FoodStore;

        /// <summary>
        /// The save key of the store at <paramref name="anchor"/> when its dwelling stands at the site
        /// <paramref name="siteKey"/>: the one <see cref="HomeStoragePlan"/> row with that building key and the
        /// anchor's Def. False, with the reason, when there is none or more than one.
        /// </summary>
        public static bool TrySiteStorageKey(string siteKey, in DwellingFixtureAnchor anchor, out string key,
                                             out string problem)
        {
            string defId = anchor.DefId;
            List<HomeStoragePlan.Row> rows = HomeStoragePlan.Rows
                .Where(r => r.BuildingKey == siteKey && r.DefId == defId).ToList();
            key = rows.Count == 1 ? rows[0].StorageId : null;
            problem = rows.Count == 1
                ? null
                : $"HomeStoragePlan has {rows.Count} rows for site '{siteKey}' and Def '{defId}' (expected one)";
            return rows.Count == 1;
        }

        /// <summary>
        /// <b>Make one placed instance this site's</b>: its key and bearing on <see cref="DwellingSite"/>, its
        /// fixtures' interactable ids under the site key, and each rack's save key from
        /// <see cref="HomeStoragePlan"/>. Through serialized properties, so on a scene instance these are the
        /// site's own values. Returns the problems found (empty when the site is complete): a store with no
        /// plan row is reported, never given an invented key.
        ///
        /// <para>The catch freezer's save key is fixed on its component today
        /// (<see cref="StorageIds.CamperFreezer"/>), and a food store carries no key yet
        /// (<see cref="KitchenStoreFixture"/>); for both, the plan's row is checked against what the component
        /// will use, so the day a second site exists the mismatch is a reported problem, not a shared row.</para>
        /// </summary>
        public static List<string> ApplySite(GameObject root, string siteKey, int groundBearingDegrees)
        {
            var problems = new List<string>();
            var site = root != null ? root.GetComponent<DwellingSite>() : null;
            var interior = root != null ? root.GetComponent<BuildingInterior>() : null;
            DwellingMeshDef def = interior != null ? interior.DwellingMesh : null;
            if (site == null || def == null) { problems.Add("not a dwelling prefab instance"); return problems; }
            if (string.IsNullOrEmpty(siteKey)) { problems.Add("the site key is empty"); return problems; }

            using (var so = new SerializedObject(site))
            {
                so.FindProperty("_siteKey").stringValue = siteKey;
                so.FindProperty("_groundBearingDegrees").intValue = DwellingSiteLayout.NormalizeBearing(groundBearingDegrees);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (DwellingSite.AnchorPoint p in site.Anchors)
            {
                if (p.Point == null || !def.TryGetAnchor(p.AnchorId, out DwellingFixtureAnchor a)) continue;
                if (!DwellingFixtureVerbs.TryGet(a.Verb, out DwellingFixtureVerbs.Row row)) continue;
                string id = FixtureId(siteKey, a.AnchorId);
                SetString(p.Point.GetComponent<InteriorBed>(), "_id", id);
                SetString(p.Point.GetComponent<CookingStationFixture>(), "_id", id);
                SetString(p.Point.GetComponent<WashStationPoint>(), "_id", id);
                SetString(p.Point.GetComponent<SitPoint>(), "_id", id);

                if (!IsStore(row.Binding)) continue;
                if (!TrySiteStorageKey(siteKey, a, out string key, out string problem))
                {
                    problems.Add($"anchor '{a.AnchorId}': {problem}");
                    continue;
                }
                switch (row.Binding)
                {
                    case DwellingFixtureVerbs.Binding.Stowage:
                        SetString(p.Point.GetComponent<StowageRack>(), "_id", key);
                        break;
                    case DwellingFixtureVerbs.Binding.CatchFreezer:
                        var freezer = p.Point.GetComponent<CamperFreezer>();
                        if (freezer != null && freezer.StorageId != key)
                            problems.Add($"anchor '{a.AnchorId}': the plan keys it '{key}' but the freezer " +
                                         $"component always saves as '{freezer.StorageId}'");
                        break;
                }
            }

            site.Layout();
            return problems;
        }

        static void SetString(Component c, string property, string value)
        {
            if (c == null) return;
            using var so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The Def of type <typeparamref name="T"/> under Data whose id is <paramref name="id"/>, or null.</summary>
        public static T Find<T>(string id, System.Func<T, string> idOf) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && idOf(asset) == id) return asset;
            }
            return null;
        }
    }
}
#endif
