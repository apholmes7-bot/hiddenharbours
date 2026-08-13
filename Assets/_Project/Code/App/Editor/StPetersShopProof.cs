#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE EYEBALL FOR ST PETERS' TWO SHOPS</b> — the general store and the post office, shut and
    /// open, plus the village wide. The rendering lives in <see cref="ShopProof"/> (Nine Mile Creek's
    /// shops wanted the same tool); this file is the island's ANSWERS to it: which scene, which frames,
    /// where to look.
    /// </summary>
    public static class StPetersShopProof
    {
        const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";
        const string OutputFolder = "artifacts/shop-proof";
        const string LogTag = "[shop-proof]";

        [MenuItem("Hidden Harbours/Dev/Bake St Peters Shop Proofs (village + reveal)")]
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
            // The village, wide: the lane, the green, the houses and both shops in one frame — the shot
            // that answers "do the two kits stand at the same scale beside each other".
            var wides = new List<ShopProof.Wide>
            {
                new ShopProof.Wide("village-wide", new Vector2(6f, 24f), 80f),
            };

            var subjects = new List<ShopProof.Subject>();
            foreach (var site in StPetersShops.Sites)
                subjects.Add(new ShopProof.Subject(site.Key, site.Position));

            return ShopProof.Bake(ScenePath, OutputFolder, wides, subjects, LogTag);
        }
    }
}
#endif
