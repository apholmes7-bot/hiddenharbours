#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices <c>ShoreIsoSprites.png</c> — the shoreline kit's freestanding pure-rock sheet (sea stacks
    /// <c>reef/s/m/l</c> + slab boulders <c>bs/bm/bl</c>).
    ///
    /// <para><b>Why this sheet needs its own slicer.</b> Every other sheet in both terrain kits is a
    /// uniform grid and goes through <see cref="SpriteSheetSlicer"/>'s manifest. This one is a PACKED
    /// sheet: seven items at seven different sizes, each with its own base-centre pivot, laid out with
    /// gaps. A grid slice would cut it into garbage. The rects and pivots therefore come from the kit's
    /// own <c>ShoreIsoSprites.json</c> sidecar, which ships beside the PNG and is the art director's
    /// source of truth — no hand-copied table to drift.</para>
    ///
    /// <para><b>Coordinate flip.</b> The sidecar measures <c>x,y</c> from the sheet's TOP-left and each
    /// item's <c>pivot</c> from that ITEM's top-left, in pixels. Unity's sprite rects and pivots are
    /// bottom-origin (and pivots normalized 0..1), so both axes are flipped here. Every item in the v7
    /// sheet happens to sit flush with the sheet's bottom edge and pivots exactly 1 px above its own
    /// base — that is the "base-centre contact point" the README describes, and it means a stack
    /// dropped at a world position plants on the ground rather than floating. The flip is written out
    /// generally anyway, so a re-bake that repacks the sheet still slices correctly.</para>
    ///
    /// <para>Slice names here are the sidecar's item NAMES (<c>ShoreIsoSprites_reef</c>, <c>_bl</c>, …),
    /// not indices. That is the opposite of this repo's usual geometric rule, and deliberately so: the
    /// rule exists to stop art claiming a DIRECTION it may not have, and "reef" / "bl" are sizes, not
    /// bearings. Naming them by pack order would make every slice meaningless the first time the
    /// packer's order changed.</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Slice Shoreline Iso Rock Sprites</c> (re-runnable, and
    /// idempotent on the .meta — existing spriteIDs are re-used so a no-op re-slice writes no diff).</para>
    /// </summary>
    public static class ShorelineIsoSpriteSlicer
    {
        public const string SheetPath = ShorelineIsoCatalog.ShorelineIsoDir + "/ShoreIsoSprites.png";
        public const string SidecarPath = ShorelineIsoCatalog.ShorelineIsoDir + "/ShoreIsoSprites.json";

        /// <summary>One packed item as the sidecar states it: top-left origin, pixel pivot.</summary>
        [Serializable]
        public struct SidecarItem
        {
            public string name;
            public int x, y, w, h;
            public int[] pivot;     // [px, py] from the ITEM's top-left
        }

        [Serializable]
        private struct Sidecar
        {
            public string sheet;
            public SidecarItem[] items;
        }

        [MenuItem("Hidden Harbours/Art/Slice Shoreline Iso Rock Sprites")]
        public static void SliceMenu()
        {
            if (Slice(out int n)) Debug.Log($"[ShorelineIsoSpriteSlicer] Sliced {n} rock sprite(s).");
        }

        /// <summary>
        /// Read the sidecar and apply it to the sheet. Returns false (and logs loudly) if the sheet or
        /// sidecar is missing, or if any item's rect falls outside the texture — a repack that drifted
        /// must fail, not slice silently-wrong rects.
        /// </summary>
        public static bool Slice(out int count)
        {
            count = 0;

            if (!File.Exists(SheetPath) || !File.Exists(SidecarPath))
            {
                Debug.LogWarning($"[ShorelineIsoSpriteSlicer] '{SheetPath}' or its sidecar is not on disk " +
                                 "yet — nothing to slice.");
                return false;
            }

            SidecarItem[] items;
            try
            {
                items = JsonUtility.FromJson<Sidecar>(File.ReadAllText(SidecarPath)).items;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShorelineIsoSpriteSlicer] '{SidecarPath}' is not readable: {e.Message}");
                return false;
            }

            if (items == null || items.Length == 0)
            {
                Debug.LogError($"[ShorelineIsoSpriteSlicer] '{SidecarPath}' lists no items.");
                return false;
            }

            var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ShorelineIsoSpriteSlicer] '{SheetPath}' has no TextureImporter.");
                return false;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (tex == null)
            {
                Debug.LogError($"[ShorelineIsoSpriteSlicer] '{SheetPath}' failed to load as Texture2D.");
                return false;
            }

            foreach (var it in items)
            {
                if (it.x < 0 || it.y < 0 || it.x + it.w > tex.width || it.y + it.h > tex.height)
                {
                    Debug.LogError($"[ShorelineIsoSpriteSlicer] item '{it.name}' rect " +
                                   $"({it.x},{it.y},{it.w},{it.h}) falls outside the {tex.width}×{tex.height} " +
                                   "sheet. Not slicing — the sidecar and the PNG are out of step.");
                    return false;
                }
                if (it.pivot == null || it.pivot.Length != 2)
                {
                    Debug.LogError($"[ShorelineIsoSpriteSlicer] item '{it.name}' has no [x,y] pivot.");
                    return false;
                }
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use the spriteID an already-sliced name carries so a no-op re-slice writes a
            // byte-identical .meta (the diff-noise fix SpriteSheetSlicer/CharacterSheetSlicer both make).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(items, tex.height, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            count = rects.Length;
            return true;
        }

        /// <summary>
        /// Flip the sidecar's top-left pixel space into Unity's bottom-left rects and normalized pivots.
        /// Pure and static so an EditMode test can check the arithmetic without importing anything.
        /// </summary>
        public static SpriteRect[] BuildRects(IReadOnlyList<SidecarItem> items, int sheetHeight,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            string stem = Path.GetFileNameWithoutExtension(SheetPath);
            var rects = new SpriteRect[items.Count];

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                string name = $"{stem}_{it.name}";

                // Sheet-space flip: the sidecar's y is the item's TOP edge from the sheet's top.
                float rectY = sheetHeight - (it.y + it.h);

                // Item-space flip + normalize: the sidecar's pivot y is measured DOWN from the item's top.
                float px = it.pivot[0] / (float)it.w;
                float py = (it.h - it.pivot[1]) / (float)it.h;

                rects[i] = new SpriteRect
                {
                    name = name,
                    rect = new Rect(it.x, rectY, it.w, it.h),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(px, py),
                    border = Vector4.zero,
                    spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                        ? id
                        : GUID.Generate(),
                };
            }

            return rects;
        }
    }
}
#endif
