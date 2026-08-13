#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE EYEBALL FOR THE WHARF'S TWO SHOPS</b> — the fish market and the restaurant, shut and open,
    /// plus the yard wide. Sibling of <see cref="StPetersShopProof"/> over the shared
    /// <see cref="ShopProof"/>.
    ///
    /// <para><b>⭐ The wide shot is the one that matters here</b>, and it is why this exists at all. B2's
    /// two sites are PROPOSALS that clear their tightest neighbour by under half a metre — the EditMode
    /// suite proves they do not overlap anything, and cannot say whether a 9 × 11.5 m restaurant simply
    /// looks wrong wedged between the fish store and the quay. The yard frame puts both new buildings,
    /// the boatyard, the shed row and the quay in one picture so the owner can rule on the thing a
    /// clearance table cannot show him.</para>
    /// </summary>
    public static class NineMileCreekShopProof
    {
        const string ScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";
        const string OutputFolder = "artifacts/creek-shop-proof";
        const string LogTag = "[creek-shop-proof]";

        [MenuItem("Hidden Harbours/Dev/Bake Nine Mile Creek Shop Proofs (yard + reveal)")]
        public static void BakeMenu() => Bake();

        public static void BakeFromCommandLine()
        {
            try
            {
                int n = Bake();
                Debug.Log($"{LogTag} (batch) wrote {n} sheet(s).");
                if (n == 0)
                {
                    Debug.LogError($"{LogTag} wrote nothing — see above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LogTag} threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Render the sheets. Returns how many were written.</summary>
        public static int Bake()
        {
            // The working yard, wide enough to hold the quay, both new shops, the shed row and the
            // boatyard's west edge — DERIVED from the spit rather than typed, so re-siting anything on
            // it keeps the frame honest.
            Vector2 yardCentre = new Vector2(
                (NineMileCreekMainland.FishMarketPos.x + NineMileCreekMainland.RestaurantLotPos.x) * 0.5f,
                (NineMileCreekMainland.FishMarketPos.y + NineMileCreekMainland.RestaurantLotPos.y) * 0.5f);

            var wides = new List<ShopProof.Wide>
            {
                // 110 m: from the quay deck below to the shed row above, and wide enough that both shops
                // and their neighbours are in one frame — the "is this yard full?" picture.
                new ShopProof.Wide("wharf-yard-wide", yardCentre, 110f),

                // …and the quay itself, so the restaurant's clearance over the working deck — the whole
                // reason its lot moved — can be seen rather than read off a slack table.
                new ShopProof.Wide("restaurant-over-the-quay",
                    new Vector2(NineMileCreekMainland.RestaurantLotPos.x,
                                NineMileCreekMainland.RestaurantLotPos.y - 6f), 46f),
            };

            var subjects = new List<ShopProof.Subject>();
            foreach (var site in NineMileCreekShops.Sites)
                subjects.Add(new ShopProof.Subject(site.Key, site.Position));

            return ShopProof.Bake(ScenePath, OutputFolder, wides, subjects, LogTag);
        }
    }
}
#endif
