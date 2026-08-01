using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Packs the terrain material kit (docs/art/rigs/terrain — ADR 0028 PR 2) into the two
    /// Texture2DArrays the splat shader samples: one 256-class array and one 512-class array,
    /// three slices per material (_Lo / base / _Hi — the kit's intensity ladder, README §2).
    ///
    /// <para>The arrays are DERIVED assets (never hand-edited): re-run this after a kit re-bake.
    /// The asset is created once and thereafter written IN PLACE (CopySerialized) so its GUID —
    /// which scenes reference — survives every rebuild (the ShoreIso2 lesson, inverted: these ARE
    /// scene-referenced, so the GUID must be stable).</para>
    ///
    /// <para>⚠ <see cref="Order256"/>/<see cref="Order512"/> and the slice layout are MIRRORED by
    /// the static tables in <c>HiddenHarboursTerrainSplat.shader</c> (MAT_ARRAY/MAT_SLICE/
    /// MAT_METRES/MAT_OFFSET). A pin test holds the two in sync — change one, change both.</para>
    ///
    /// <para>Only the kit's PLAN-projection materials are packed here. Sandstone and Bank are cliff
    /// FACES (their UVs run along and down a wall, not over the ground) and the four edge strips are
    /// non-square decals laid along the shoreline spline — both are imported but belong to arrays
    /// this builder does not own.</para>
    /// </summary>
    public static class TerrainTexArrayBuilder
    {
        public const string TexDir = "Assets/_Project/Art/Terrain";
        public const string DerivedDir = TexDir + "/Derived";
        public const string Array256Path = DerivedDir + "/TerrainDetail256.asset";
        public const string Array512Path = DerivedDir + "/TerrainDetail512.asset";

        /// <summary>256-class materials (8 m tiles) in canonical slice order — slice base = index × 3.
        /// APPEND ONLY: the shader's MAT_SLICE table reads these positions by number.</summary>
        public static readonly string[] Order256 =
            { "Grass", "Marram", "Sand", "Shelf", "Dirt", "Marsh", "Sedge", "Ledge", "Rockweed" };

        /// <summary>512-class materials (16 m tiles) in canonical slice order. Append only.</summary>
        public static readonly string[] Order512 =
            { "Shingle", "Ripple", "Silt", "Foreshore", "Talus" };

        /// <summary>The ladder suffixes, in slice order (README §2: _Lo = 0, base = 1, _Hi = 2).</summary>
        public static readonly string[] LadderSteps = { "_Lo", "", "_Hi" };

        [MenuItem("Hidden Harbours/Art/Build Terrain Texture Arrays", priority = 24)]
        public static void BuildMenu() => Build();

        /// <summary>Build both arrays. Returns total slices written, 0 if the kit is absent
        /// (warn-and-skip, the shore painter's convention — never half a kit).</summary>
        public static int Build()
        {
            var a256 = BuildArray(Order256, 256);
            var a512 = BuildArray(Order512, 512);
            if (a256 == null || a512 == null) return 0;

            if (!AssetDatabase.IsValidFolder(DerivedDir))
                AssetDatabase.CreateFolder(TexDir, "Derived");
            SaveInPlace(a256, Array256Path);
            SaveInPlace(a512, Array512Path);
            AssetDatabase.SaveAssets();
            int slices = a256.depth + a512.depth;
            Debug.Log($"[TerrainTexArrayBuilder] Packed {slices} slices " +
                      $"({Order256.Length} + {Order512.Length} materials x {LadderSteps.Length} steps).");
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
