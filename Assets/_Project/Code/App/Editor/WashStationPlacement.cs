#if UNITY_EDITOR
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE FIRST PLACED WASH STATIONS</b> (washrooms plan sections 3 and 4): the two prefabs and their
    /// names and ids.
    ///
    /// <list type="bullet">
    /// <item><description><b>St Peters, Aunt Ginny's cottage, the player's room upstairs:</b> a washstand
    /// (<c>washstation.sink</c>). The earliest wash the player can reach: he sleeps in the next bed.</description></item>
    /// <item><description><b>Nine Mile Creek, the wharf's land side:</b> a rinse shower
    /// (<c>washstation.shower</c>), where the plan says a wharf shower naturally belongs, a few steps from
    /// the harbourmaster's office.</description></item>
    /// </list>
    ///
    /// <para><b>Where they are placed.</b> Both are rows of <see cref="InteriorFixturePlacement"/> now (the
    /// kitchens and bathrooms plan's generic step), with the same names and ids they were first placed
    /// with, so its re-plan of the committed scenes is empty.</para>
    ///
    /// <para><b>Placeholder art, said plainly.</b> No basin or shower art exists. The washstand wears the
    /// catch kit's empty galvanised pail; the shower is that pail under a grey post and head made of the
    /// greybox <c>Square.png</c>. No new binary art.</para>
    /// </summary>
    public static class WashStationPlacement
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/Washrooms";
        public const string WashStandPrefab = PrefabFolder + "/WashStand.prefab";
        public const string WharfShowerPrefab = PrefabFolder + "/WharfShower.prefab";
        public const string StPetersScene = "Assets/_Project/Scenes/StPeters.unity";
        public const string NineMileCreekScene = "Assets/_Project/Scenes/NineMileCreek.unity";

        /// <summary>The root the Nine Mile Creek station hangs under (at the origin, so local = world).</summary>
        public const string NmcRootName = "NmcWashStations";

        /// <summary>The ginny-cottage washstand's interact id.</summary>
        public const string GinnyWashstandId = "fixture.st_peters.ginny_cottage.washstand";

        /// <summary>The wharf shower's interact id.</summary>
        public const string NmcWharfShowerId = "fixture.nmc.wharf.shower";

        const string PailSheet = "Assets/_Project/Art/Fishing/Storage/Bucket_pail_empty.png";
        const string SquareSprite = "Assets/_Project/Art/Sprites/Square.png";

        /// <summary>The pail cell the placeholders wear (d0: the rig's first facing).</summary>
        const string PailCell = "Bucket_pail_empty_d0_f0";

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
    }
}
#endif
