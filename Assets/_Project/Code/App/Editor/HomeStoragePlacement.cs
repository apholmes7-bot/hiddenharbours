#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>WHERE THE PLAYER'S STORAGE STANDS</b> (owner 2026-11-17: "give them storage with memory at ginnys"):
    /// the prefabs for the rows of <see cref="HomeStoragePlan"/> that have a position, and the camper's shore
    /// power lead from the cottage, as sites of the one room-fixture placement step
    /// (<see cref="InteriorFixturePlacement"/>). This file writes no planner of its own.
    ///
    /// <list type="bullet">
    /// <item><description><b>Ginny's rod rack and gear locker</b>, in the player's room upstairs, at the plan's
    /// model points. The scene instance carries the row's <c>storage.*</c> id as the rack's <c>_id</c> (the save
    /// key); the prefab's own id is left empty so no prefab ever claims a save row.</description></item>
    /// <item><description><b>The shore power lead</b>, a cable on the ground from Ginny's cottage round the
    /// camper's tail to its <c>shore_power</c> hookup (the shell sidecar's <c>HOOKUPS</c>). It is a picture
    /// only: the freezer and fridge are powered because the kitchen power seam's default answers "on"
    /// (<c>AlwaysOnKitchenPower</c>), not because of this lead.</description></item>
    /// </list>
    ///
    /// <para><b>The camper's own stores are NOT stood here.</b> The camper has no room the player can enter
    /// yet (its door answers "the inside isn't finished"), so a rack or freezer placed inside it could only be
    /// reached through its wall. Its rows keep <c>HasPosition = false</c> until the room is baked and walked
    /// into (docs/world/camper-home-plan.md C2 and C3); the room rig's sidecar already names their anchors.</para>
    ///
    /// <para><b>Placeholder art, said plainly.</b> No rack, locker or cable art exists. The rack and the locker
    /// are tinted greybox <c>Square.png</c> boards; the lead is a chain of thin dark strips of the same
    /// sprite. No new binary art.</para>
    /// </summary>
    public static class HomeStoragePlacement
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/Props/Storage";
        public const string GinnyRodRackPrefab = PrefabFolder + "/GinnyRodRack.prefab";
        public const string GinnyGearLockerPrefab = PrefabFolder + "/GinnyGearLocker.prefab";
        public const string ShoreLeadPrefab = PrefabFolder + "/CamperShoreLead.prefab";

        /// <summary>The scene name of the lead's instance (open air, under the scene's open-air root).</summary>
        public const string ShoreLeadName = "CamperShoreLead";

        /// <summary>The shell sidecar's hookup the lead plugs into.</summary>
        public const string ShorePowerHookupId = "shore_power";

        const string SquareSprite = "Assets/_Project/Art/Sprites/Square.png";
        const string StowageFolder = "Assets/_Project/Data/Stowage";

        // ---- the lead's route (layout, not balance: where a cable lies on the grass) ----------------------

        /// <summary>How far from the cottage's centre, toward the camper, the lead leaves the house. Well inside
        /// the cottage's 8 m length, so its end is under the house and the house is drawn over it.</summary>
        public const float CottageTapMetres = 2f;

        /// <summary>How far clear of the camper's tail (and of its curb side) the lead runs round it.</summary>
        public const float TailClearanceMetres = 0.6f;

        /// <summary>The cable's drawn thickness in metres (about two pixels at 32 px to the metre).</summary>
        public const float LeadThicknessMetres = 0.08f;

        static readonly Color LeadTint = new Color(0.86f, 0.70f, 0.16f);    // a yellow shore cord, read on grass
        static readonly Color RackTint = new Color(0.55f, 0.40f, 0.25f);    // a board of uprights, wood
        static readonly Color LockerTint = new Color(0.42f, 0.50f, 0.46f);  // a painted steel cabinet

        /// <summary>Drawn height (m) of the greybox rack and locker, standing on their floor point.</summary>
        const float RackHeightMetres = 1.4f, LockerHeightMetres = 1.8f;

        // ---- the sites -------------------------------------------------------------------------------------

        /// <summary>The prefab a positioned plan row stands as, or null for a row this file does not stand.</summary>
        public static string PrefabFor(HomeStoragePlan.Row row) => row.DefId switch
        {
            "stowage.ginny_rod_rack" => GinnyRodRackPrefab,
            "stowage.ginny_gear_locker" => GinnyGearLockerPrefab,
            _ => null,
        };

        /// <summary>The GameObject name of a row: the last part of its id, <c>ginny_rod_rack</c> is <c>GinnyRodRack</c>.</summary>
        public static string NameOf(HomeStoragePlan.Row row) =>
            InteriorFixturePlacement.NameOf(row.StorageId.Substring(row.StorageId.LastIndexOf('.') + 1));

        /// <summary>The plan rows this file stands: every positioned row (Ginny's two).</summary>
        public static IEnumerable<HomeStoragePlan.Row> PlacedRows() =>
            HomeStoragePlan.Rows.Where(r => r.HasPosition);

        /// <summary>
        /// Every storage site: the positioned rows, then the lead. A row is in the UpperLevelFor frame, the one
        /// the bed and the dresser are planned in: front door at <c>+y</c>. That IS the plan frame, so its site is a
        /// plan-frame site and takes the same half turn into a footprint whose door is in the <c>-y</c> wall as a
        /// fixture row does (stood without it, a rack lands mirrored through the room's centre, in his doorway).
        /// </summary>
        public static List<InteriorFixturePlacement.Site> Sites()
        {
            var sites = new List<InteriorFixturePlacement.Site>();
            foreach (HomeStoragePlan.Row row in PlacedRows())
                sites.Add(new InteriorFixturePlacement.Site(HomeStoragePlan.Scene, PrefabFor(row), NameOf(row),
                    row.StorageId, row.At,
                    $"{row.BuildingKey}, level {row.Level}: the plan's {row.Kind} ({row.DefId}), remembered as {row.StorageId}",
                    StPetersGinnyPlot.RootName, row.RoomKey, row.Level, planFrame: true));
            if (TryShoreLeadPath(out List<Vector2> path))
                sites.Add(new InteriorFixturePlacement.Site(HomeStoragePlan.Scene, ShoreLeadPrefab, ShoreLeadName, null,
                    path[path.Count - 1],
                    "the camper's shore power lead: from Ginny's cottage round the camper's tail to its shore_power hookup"));
            return sites;
        }

        // ---- the lead's geometry ----------------------------------------------------------------------------

        /// <summary>A point in the parked camper's body frame (+x the curb, where the door is; +y the hitch) on
        /// the ground, in world metres. The frame the room and the lot shed follow (<see cref="StPetersCamperLot.NoseDirection"/>).</summary>
        public static Vector2 CamperGround(float curbMetres, float hitchMetres)
        {
            Vector2 curb = StPetersCamperLot.Approach - StPetersCamperLot.LotPos;
            curb = curb.sqrMagnitude < 1e-6f ? Vector2.down : curb.normalized;
            return StPetersCamperLot.LotPos + curb * curbMetres + StPetersCamperLot.NoseDirection * hitchMetres;
        }

        /// <summary>The ground point under the camper's shore power hookup, read off the shell sidecar.</summary>
        public static bool TryHookupGround(out Vector2 ground)
        {
            ground = default;
            if (!CamperSidecar.TryReadHookup(StPetersCamperLot.Variant, ShorePowerHookupId, out Vector3 rig)) return false;
            ground = CamperGround(rig.x, rig.y);
            return true;
        }

        /// <summary>
        /// The lead, in world metres, from the cottage to the hookup: out of the cottage toward the camper, to the
        /// curb-side corner past the tail, along the tail to the street side, and along the street side (behind
        /// the camper, which is drawn over it) to the hookup's foot. False with no readable sidecar.
        /// </summary>
        public static bool TryShoreLeadPath(out List<Vector2> path)
        {
            path = null;
            CamperSidecar.Dimensions d = StPetersCamperLot.Dimensions;
            if (!d.IsValid || !CamperSidecar.TryReadHookup(StPetersCamperLot.Variant, ShorePowerHookupId, out Vector3 rig))
                return false;
            Vector2 cottage = StPetersGinnyPlot.CottagePos;
            Vector2 toCamper = StPetersCamperLot.LotPos - cottage;
            toCamper = toCamper.sqrMagnitude < 1e-6f ? Vector2.up : toCamper.normalized;
            float pastTail = -(d.TailMetres + TailClearanceMetres);
            path = new List<Vector2>
            {
                cottage + toCamper * CottageTapMetres,
                CamperGround(d.WidthMetres * 0.5f + TailClearanceMetres, pastTail),
                CamperGround(rig.x, pastTail),
                CamperGround(rig.x, rig.y),
            };
            return true;
        }

        // ---- prefabs ---------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/World/Home Storage/Build the Storage Prefabs")]
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            Sprite square = AssetDatabase.LoadAllAssetsAtPath(SquareSprite).OfType<Sprite>().FirstOrDefault();
            if (square == null) Debug.LogWarning($"[HomeStoragePlacement] No sprite in {SquareSprite}: the storage stands invisible.");

            BuildRack(GinnyRodRackPrefab, "GinnyRodRack", "stowage.ginny_rod_rack", HomeStoragePlan.Kind.RodRack,
                      square, RackTint, RackHeightMetres);
            BuildRack(GinnyGearLockerPrefab, "GinnyGearLocker", "stowage.ginny_gear_locker", HomeStoragePlan.Kind.GearLocker,
                      square, LockerTint, LockerHeightMetres);

            if (TryShoreLeadPath(out List<Vector2> path))
                BuildLead(path, square);
            else
                Debug.LogWarning("[HomeStoragePlacement] No readable camper sidecar or shore_power hookup: no lead built.");

            AssetDatabase.SaveAssets();
            Debug.Log($"[HomeStoragePlacement] Built the storage prefabs in {PrefabFolder}.");
        }

        static void BuildRack(string prefabPath, string name, string defId, HomeStoragePlan.Kind kind, Sprite square,
                              Color tint, float height)
        {
            StowageDef def = FindStowageDef(defId);
            if (def == null) Debug.LogWarning($"[HomeStoragePlacement] No StowageDef '{defId}' in {StowageFolder}: {name} is inert.");
            Vector2 footprint = HomeStoragePlan.Footprint(kind);
            var go = new GameObject(name);
            try
            {
                var art = new GameObject("Art");
                art.transform.SetParent(go.transform, false);
                art.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                art.transform.localScale = new Vector3(footprint.x, height, 1f);
                var sr = art.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.color = tint;
                var sort = art.AddComponent<YSortSprite>();
                using (var so = new SerializedObject(sort))
                {
                    so.FindProperty("_pivotYOffset").floatValue = -height * 0.5f;   // sort by its foot
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var box = go.AddComponent<BoxCollider2D>();
                box.size = footprint;
                go.AddComponent<StowageRack>().Configure(def, null, "");
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void BuildLead(List<Vector2> path, Sprite square)
        {
            Vector2 origin = path[path.Count - 1];
            var go = new GameObject(ShoreLeadName);
            try
            {
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    Vector2 a = path[i] - origin, b = path[i + 1] - origin;
                    Vector2 run = b - a;
                    var seg = new GameObject($"Run{i}");
                    seg.transform.SetParent(go.transform, false);
                    seg.transform.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
                    seg.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg);
                    // a hair longer than the run so the corners join without a gap
                    seg.transform.localScale = new Vector3(run.magnitude + LeadThicknessMetres, LeadThicknessMetres, 1f);
                    var sr = seg.AddComponent<SpriteRenderer>();
                    sr.sprite = square;
                    sr.color = LeadTint;
                    // It lies on the grass: at the bottom of the decor band, so the cottage, the camper and
                    // everything standing on the ground draws over it.
                    sr.sortingOrder = SortingBands.DecorFloor;
                }
                PrefabUtility.SaveAsPrefabAsset(go, ShoreLeadPrefab);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The lead's runs as the prefab holds them, as world segments when its root stands at
        /// <paramref name="origin"/> (tests: the prefab still matches the route).</summary>
        public static List<(Vector2 a, Vector2 b)> LeadRuns(GameObject prefab, Vector2 origin)
        {
            var runs = new List<(Vector2, Vector2)>();
            foreach (Transform seg in prefab.transform)
            {
                float half = (seg.localScale.x - LeadThicknessMetres) * 0.5f;
                Vector2 dir = seg.localRotation * Vector3.right;
                Vector2 mid = (Vector2)seg.localPosition + origin;
                runs.Add((mid - dir * half, mid + dir * half));
            }
            return runs;
        }

        static StowageDef FindStowageDef(string id) =>
            AssetDatabase.FindAssets("t:" + nameof(StowageDef), new[] { StowageFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<StowageDef>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(d => d != null && d.Id == id);
    }
}
#endif
