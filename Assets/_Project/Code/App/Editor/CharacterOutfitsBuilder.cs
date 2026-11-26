#if UNITY_EDITOR
using System;
using HiddenHarbours.Tools.RigBaking;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Dresses the cast members who wear their own outfit</b> (<see cref="CharacterOutfits"/>), in
    /// dependency order: the outfit's visual def, its skin (baked from the live rig with the outfit
    /// installed in the host) linked onto that def, then the builds and the cast mapping that point
    /// the NpcDef at it.
    ///
    /// <para>⚠️ The region scene holds its own reference to the visual def it was built with; a scene
    /// that was placed before an outfit existed keeps drawing the base preset until it is rebuilt or
    /// its one reference is moved (the outfit PR moves St Peters' Basil).</para>
    /// </summary>
    public static class CharacterOutfitsBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Character Outfits (skin + build)";

        [MenuItem(MenuPath, priority = 234)]
        public static void Build()
        {
            try
            {
                int visuals = CharacterVisualLibraryBuilder.BuildOutfits();
                Debug.Log($"[outfits] {visuals}/{CharacterOutfits.All.Length} outfit visual def(s) with art.");

                foreach (CharacterOutfits.Outfit outfit in CharacterOutfits.All)
                {
                    if (outfit.Kit != CharacterSkinAssetBaker.LiveKit)
                        throw new InvalidOperationException(
                            $"Outfit '{outfit.Key}' is drawn from {outfit.Kit}, and the live kit is " +
                            $"{CharacterSkinAssetBaker.LiveKit}. An outfit is baked from the rig the cast is.");
                    var def = CharacterSkinAssetBaker.Bake(outfit.Key,
                        freshMeshStates: CharacterSkinAssetBaker.CastMeshStates);
                    string link = CharacterSkinAssetBaker.LinkSkin(outfit.Stem, outfit.Key);
                    Debug.Log($"[outfits] {outfit.Key} OK — {def.Id}, {def.Clips.Length} clips, " +
                              $"MeshStates [{string.Join(", ", def.MeshStates ?? Array.Empty<string>())}], {link}");
                }

                CharacterBuildsBuilder.Build();
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        /// <summary>Headless entry (<c>-executeMethod</c>). Grep the log for <c>[outfits] CLI OK</c>:
        /// a fresh worktree's first run skips <c>-executeMethod</c> and still exits 0.</summary>
        public static void BuildCli()
        {
            try
            {
                Build();
                AssetDatabase.SaveAssets();
                Debug.Log("[outfits] CLI OK");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[outfits] CLI FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
