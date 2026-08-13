#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices the ROAD KIT v3 atlases into <b>two sprites per cell</b> — a TOP and a SKIRT — because
    /// the kit's composite rule is two-pass and no single <c>Tilemap</c> can express it.
    ///
    /// <para><b>Why not a uniform 32×64 grid slice.</b> The obvious slice (one sprite per cell, the
    /// shape <see cref="SpriteSheetSlicer"/> already does for every other sheet) produces art that is
    /// correct alone and torn at every junction. A cell's skirt hangs 22 px below its own ground
    /// square and must draw IN FRONT of the ground square of the cell to its south; painter N→S over
    /// whole sprites draws that southern cell later, so it covers the northern cell's kerb and drop
    /// faces. Splitting the cell lets a road be two tilemaps on adjacent sorting orders — all tops,
    /// then all skirts — which is exactly the rig's own rule, and exactly how
    /// <c>StPetersShorePainter</c> already stacks its three shore layers.</para>
    ///
    /// <para><b>Both sprites pin the SAME world point</b> (the ground-square centre,
    /// <see cref="RoadKitV3.PivotRowFromTop"/>), which is what keeps the two layers registered. The
    /// skirt's normalized pivot is therefore &gt; 1 — deliberate, legal, and asserted by
    /// <c>RoadKitSliceTests</c> so nobody "fixes" it back into range.</para>
    ///
    /// <para>Index 47 of the 48-cell atlas is PADDING and is not sliced.</para>
    /// </summary>
    public static class RoadKitSlicer
    {
        /// <summary>Slice every surface. Returns how many sprites were written per sheet.</summary>
        public static Dictionary<string, int> SliceAll()
        {
            var written = new Dictionary<string, int>();
            foreach (string surface in RoadKitV3.Surfaces)
            {
                string path = RoadKitV3.AtlasPath(surface);
                if (Slice(path, out int n)) written[surface] = n;
            }
            return written;
        }

        /// <summary>
        /// Slice one atlas. Fails loudly rather than writing rects that do not fit the texture — a
        /// re-bake that changed the sheet size must stop the line, not slice garbage.
        /// </summary>
        public static bool Slice(string assetPath, out int count)
        {
            count = 0;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[RoadKitSlicer] no TextureImporter at '{assetPath}' — bake first.");
                return false;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogError($"[RoadKitSlicer] '{assetPath}' did not load as a Texture2D.");
                return false;
            }

            if (tex.width != RoadKitV3.SheetWidth || tex.height != RoadKitV3.SheetHeight)
            {
                Debug.LogError(
                    $"[RoadKitSlicer] '{assetPath}' is {tex.width}×{tex.height}, the kit's atlas is " +
                    $"{RoadKitV3.SheetWidth}×{RoadKitV3.SheetHeight}. ⚠️ If this is a POWER-OF-TWO " +
                    "downscale, the import cap is below the bake size — a sheet between the cap and " +
                    "4096 imports downscaled with the sprite COUNT still correct, so the slice looks " +
                    "fine and every pivot is half a cell out. Fix the importer, do not re-slice.");
                return false;
            }

            ApplyImportSettings(importer);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use the spriteID an already-sliced name carries, so a no-op re-slice writes a
            // byte-identical .meta instead of diff noise (sprite refs resolve by internalID anyway).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(Path.GetFileNameWithoutExtension(assetPath), existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            count = rects.Length;
            return true;
        }

        /// <summary>
        /// The rects, in top-down cell space flipped into Unity's bottom-left sheet space. Pure and
        /// static so an EditMode test can check the arithmetic without importing anything.
        ///
        /// <para>Order is deterministic: cell 0 top, cell 0 skirt, cell 1 top, … so a diff of the
        /// .meta reads as the atlas does.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[RoadKitV3.BlobCount * 2];

            for (int i = 0; i < RoadKitV3.BlobCount; i++)
            {
                int cellX = RoadKitV3.ColOf(i) * RoadKitV3.Tile;
                int cellTop = RoadKitV3.RowOf(i) * RoadKitV3.CellH;   // from the sheet's TOP

                // TOP: cell rows 0..41.
                rects[i * 2] = Make(
                    RoadKitV3.TopSpriteName(stem, i),
                    cellX, cellTop, RoadKitV3.TopRectHeight, RoadKitV3.TopPivot, existingIds);

                // SKIRT: cell rows 42..63.
                rects[i * 2 + 1] = Make(
                    RoadKitV3.SkirtSpriteName(stem, i),
                    cellX, cellTop + RoadKitV3.Skirt, RoadKitV3.SkirtRectHeight, RoadKitV3.SkirtPivot,
                    existingIds);
            }

            return rects;
        }

        static SpriteRect Make(string name, int x, int topFromSheetTop, int h, Vector2 pivot,
                               IReadOnlyDictionary<string, GUID> existingIds)
        {
            // Sheet-space flip: Unity rects are measured from the sheet's BOTTOM.
            float rectY = RoadKitV3.SheetHeight - (topFromSheetTop + h);
            return new SpriteRect
            {
                name = name,
                rect = new Rect(x, rectY, RoadKitV3.Tile, h),
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                border = Vector4.zero,
                spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                    ? id
                    : GUID.Generate(),
            };
        }

        /// <summary>
        /// Import settings for a road atlas. Point filtering, no compression, no mips — the same
        /// pixel-art lock the rest of the tilesets use — and 32 PPU so one cell is one world metre.
        ///
        /// <para>⚠️ <c>maxTextureSize</c> is pinned at 512 rather than left at Unity's 2048 default:
        /// not because the sheet is near it (it is 384×256), but because a silent downscale is this
        /// pipeline's signature failure and an explicit cap makes the intent reviewable.</para>
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = RoadKitV3.Tile;   // 32 px = 1 m
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.sRGBTexture = true;
        }

        /// <summary>
        /// Read the kit back the way its consumers will. The slice asserting its own rects proves
        /// nothing about the import: a sheet can be sliced, on disk, and still yield no sprite.
        /// </summary>
        public static (int found, int expected, List<string> missing) VerifyAll()
        {
            var missing = new List<string>();
            int expected = RoadKitV3.Surfaces.Length * RoadKitV3.BlobCount * 2;
            int found = 0;

            foreach (string surface in RoadKitV3.Surfaces)
            {
                string path = RoadKitV3.AtlasPath(surface);
                string stem = Path.GetFileNameWithoutExtension(path);
                var byName = AssetDatabase.LoadAllAssetsAtPath(path)
                                          .OfType<Sprite>()
                                          .ToDictionary(s => s.name, s => s);
                for (int i = 0; i < RoadKitV3.BlobCount; i++)
                {
                    foreach (string n in new[] { RoadKitV3.TopSpriteName(stem, i),
                                                 RoadKitV3.SkirtSpriteName(stem, i) })
                    {
                        if (byName.ContainsKey(n)) found++;
                        else missing.Add(n);
                    }
                }
            }

            return (found, expected, missing);
        }
    }
}
#endif
