#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices an appliance sheet: a grid of <c>rows × cols</c> equal cells, every cell sharing one pivot (the
    /// floor centre of the piece), each sprite named by the caller. The appliance baker in RigBaking owns the
    /// table of sheets and calls this once per sheet; this owns only the importer.
    ///
    /// <para>Re-uses existing sprite ids by name, so re-slicing unchanged art leaves the .meta alone.</para>
    /// </summary>
    public static class ApplianceSheetSlicer
    {
        /// <summary>The import cap the sheets are packed under (a sheet over it would be downscaled).</summary>
        public const int ImportSizeCap = 4096;

        /// <summary>Apply the pixel-art import settings and slice the grid. Row 0 is the TOP row of the PNG.
        /// <paramref name="pivotFromTopLeftPx"/> is the pivot in the cell's own pixels, measured from its
        /// top-left corner (the rig's convention).</summary>
        public static bool Slice(string assetPath, int rows, int cols, int cellW, int cellH,
                                 Vector2 pivotFromTopLeftPx, int pixelsPerUnit, Func<int, int, string> nameFor)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ApplianceSheetSlicer] '{assetPath}' has no TextureImporter.");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = ImportSizeCap;
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null || tex.width != cols * cellW || tex.height != rows * cellH)
            {
                Debug.LogError($"[ApplianceSheetSlicer] '{assetPath}' is {(tex != null ? tex.width : 0)}×" +
                               $"{(tex != null ? tex.height : 0)}, expected {cols * cellW}×{rows * cellH}. Not slicing.");
                return false;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            var existing = dp.GetSpriteRects().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First().spriteID);
            Vector2 pivot = new Vector2(pivotFromTopLeftPx.x / cellW, (cellH - pivotFromTopLeftPx.y) / cellH);

            var rects = new List<SpriteRect>(rows * cols);
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                string name = nameFor(r, c);
                rects.Add(new SpriteRect
                {
                    name = name,
                    spriteID = existing.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                    rect = new Rect(c * cellW, (rows - 1 - r) * cellH, cellW, cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                });
            }

            dp.SetSpriteRects(rects.ToArray());
            dp.GetDataProvider<ISpriteNameFileIdDataProvider>()?
              .SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
            dp.Apply();
            importer.SaveAndReimport();
            return true;
        }
    }
}
#endif
