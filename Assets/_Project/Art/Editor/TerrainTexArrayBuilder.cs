using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Packs the terrain material kit (docs/art/rigs/terrain — ADR 0028 PR 2) into the ONE
    /// Texture2DArray the splat shader samples: a 256-class array (8 m tiles), three slices per
    /// material (_Lo / base / _Hi — the kit's intensity ladder, README §2).
    ///
    /// <para>One array since the px flip (2026-09-17, owner ruling A2). The greenery px kit ships
    /// every plan material at 256 px / 8 m (one pixel per texel), so the seven that used to fill a
    /// 512-class array were APPENDED to <see cref="Order256"/> after Lawn and the 512 array was
    /// retired. See <see cref="Array512Path"/> for the one name that outlives it.</para>
    ///
    /// <para>The array is a DERIVED asset (never hand-edited): re-run this after the kit's PNGs
    /// change. The asset is created once and thereafter written IN PLACE (CopySerialized) so its
    /// GUID — which scenes reference — survives every rebuild (the ShoreIso2 lesson, inverted: it IS
    /// scene-referenced, so the GUID must be stable).</para>
    ///
    /// <para>⚠ <see cref="Order256"/> and the slice layout are MIRRORED by the static tables in
    /// <c>HiddenHarboursTerrainSplat.shader</c> (MAT_SLICE/MAT_METRES/MAT_OFFSET). A pin test holds
    /// the two in sync — change one, change both.</para>
    ///
    /// <para>Only the kit's PLAN-projection materials are packed here. Sandstone and Bank are cliff
    /// FACES (their UVs run along and down a wall, not over the ground) and the five edge strips are
    /// non-square decals laid along a spline — both are imported but belong to arrays this builder
    /// does not own.</para>
    /// </summary>
    public static class TerrainTexArrayBuilder
    {
        public const string TexDir = "Assets/_Project/Art/Terrain";
        public const string DerivedDir = TexDir + "/Derived";
        public const string Array256Path = DerivedDir + "/TerrainDetail256.asset";
        /// <summary>⚠ OUTLIVES ITS ASSET. The 512-class array was retired by the px flip (2026-09-17,
        /// owner ruling A2): nothing builds it and the asset is deleted. The path stays only because
        /// the two region builders (StPetersBuilder, NineMileCreekBuilder — exporter-tracked, left
        /// untouched on purpose) still load it for <c>TerrainSplatSurface.ConfigureDetail</c>'s second
        /// argument, which now loads null and is ignored. Drop the path and that argument at the
        /// builders' next legitimate edit.</summary>
        public const string Array512Path = DerivedDir + "/TerrainDetail512.asset";

        /// <summary>The plan materials (256 px, 8 m tiles) in canonical slice order — slice base =
        /// index × 3. APPEND ONLY: the shader's MAT_SLICE table reads these positions by number.
        /// Eelgrass and Irishmoss arrived with kit v3 (materials.json sizes them 256).</summary>
        public static readonly string[] Order256 =
        {
            "Grass", "Marram", "Sand", "Shelf", "Dirt", "Marsh", "Sedge", "Ledge", "Rockweed",
            "Eelgrass", "Irishmoss",
            // The mown dooryard lawn (2026-08-26). APPENDED, because the shader's MAT_SLICE table
            // holds each material's base slice as a literal — inserting anywhere but the end would
            // silently repaint every material after it.
            "Lawn",
            // The px flip (2026-09-17, owner ruling A2): the kit ships these seven at 256 px / 8 m, so
            // they left the retired 512 array and were APPENDED here, after Lawn (slices 36..54). Their
            // splat indices (3, 4, 6, 10, 11, 14, 15) do not move; only their MAT_SLICE and MAT_METRES
            // rows in the shader do.
            "Shingle", "Ripple", "Silt", "Foreshore", "Talus", "Musselbed", "Oysterreef",
            // Mud, new with the px kit (2026-09-17, owner ruling M1): splat index 19, _SplatE.a. Slice 57.
            "Mud",
        };

        /// <summary>The ladder suffixes, in slice order (README §2: _Lo = 0, base = 1, _Hi = 2).</summary>
        public static readonly string[] LadderSteps = { "_Lo", "", "_Hi" };

        [MenuItem("Hidden Harbours/Art/Build Terrain Texture Arrays", priority = 24)]
        public static void BuildMenu() => Build();

        /// <summary>Build the array. Returns the slices written, 0 if the kit is absent
        /// (warn-and-skip, the shore painter's convention — never half a kit).</summary>
        public static int Build()
        {
            var a256 = BuildArray(Order256, 256);
            if (a256 == null) return 0;

            if (!AssetDatabase.IsValidFolder(DerivedDir))
                AssetDatabase.CreateFolder(TexDir, "Derived");
            SaveInPlace(a256, Array256Path);
            AssetDatabase.SaveAssets();
            // ⚠ Read the PERSISTED assets, never the temps: on the REBUILD path SaveInPlace has just
            // CopySerialized'd the temp into the existing asset and DESTROYED the temp — so `a256.depth`
            // here was a MissingReferenceException that killed every St Peters build on any machine that
            // had built the arrays before (first hit: the owner's, 2026-08-02; every agent machine and CI
            // builds into a fresh worktree, takes the CreateAsset path, and never sees it). Reading back
            // from the path also proves the save actually landed.
            var p256 = AssetDatabase.LoadAssetAtPath<Texture2DArray>(Array256Path);
            int slices = p256 != null ? p256.depth : 0;
            Debug.Log($"[TerrainTexArrayBuilder] Packed {slices} slices " +
                      $"({Order256.Length} materials x {LadderSteps.Length} steps).");
            return slices;
        }

        private static Texture2DArray BuildArray(string[] order, int size)
        {
            int depth = order.Length * LadderSteps.Length;
            // linear:false — the kit is sRGB albedo (materials.json), sampled as colour.
            var arr = new Texture2DArray(size, size, depth, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = $"TerrainDetail{size}",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,   // the kit contract: Repeat + Point (README §6)
                anisoLevel = 0,
            };

            for (int m = 0; m < order.Length; m++)
            for (int s = 0; s < LadderSteps.Length; s++)
            {
                string path = $"{TexDir}/{order[m]}{LadderSteps[s]}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null || tex.width != size || tex.height != size)
                {
                    Debug.LogWarning($"[TerrainTexArrayBuilder] '{path}' missing or not {size}px — " +
                                     "terrain detail arrays not built (the shader falls back to flat colours).");
                    return null;
                }
                if (!tex.isReadable)
                {
                    Debug.LogWarning($"[TerrainTexArrayBuilder] '{path}' is not readable — check the import " +
                                     "settings (isReadable must be on for the pack).");
                    return null;
                }
                arr.SetPixels32(tex.GetPixels32(), m * LadderSteps.Length + s);
            }

            arr.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return arr;
        }

        private static void SaveInPlace(Texture2DArray built, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return;
            }
            EditorUtility.CopySerialized(built, existing);   // keep the GUID scenes reference
            Object.DestroyImmediate(built);
        }
    }
}
