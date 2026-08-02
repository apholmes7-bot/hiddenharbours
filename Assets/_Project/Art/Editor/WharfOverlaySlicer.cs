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
    /// Slices <c>WharfOverlays.png</c> — the wharf kit's 14 deck FITTINGS (wood and pipe railings in
    /// three runs each, horn cleat, cast bollard, recessed ring, timber dolphin, access ladder, tyre
    /// fender, pile head, aluminium gangway). These draw AFTER the deck tile they sit on.
    ///
    /// <para><b>Why this sheet can't be grid-sliced.</b> Fourteen items at fourteen sizes, from a 6×32
    /// vertical rail to a 16×40 gangway — but the real reason is the PIVOTS, which mean four different
    /// things depending on what the fitting is:</para>
    /// <list type="bullet">
    ///   <item><b>Rails</b> pivot on their EDGE LINE — the line they run along, so a rail lands on the
    ///         deck edge rather than straddling it.</item>
    ///   <item><b>Cleat / bollard / dolphin / pile head</b> pivot on their BASE — they stand on the deck
    ///         (or in the water) and must plant, not float.</item>
    ///   <item><b>The recessed ring</b> pivots on its CENTRE — it is set INTO the deck, so it has no base.</item>
    ///   <item><b>Ladder / tyre / gangway</b> pivot on their TOP — they HANG DOWN off a deck face, so the
    ///         anchor is where they attach, and the sprite falls away from it.</item>
    /// </list>
    ///
    /// <para>One uniform pivot rule would be wrong for at least three of those four groups, which is why
    /// the rects and pivots come from <c>WharfOverlays.json</c> — the kit's own sidecar, shipped beside
    /// the PNG. Nothing here is hand-copied.</para>
    ///
    /// <para><b>Coordinate flip.</b> The sidecar measures <c>x,y</c> from the sheet's TOP-left and
    /// <c>pivotX,pivotY</c> from that ITEM's top-left, in pixels; Unity wants bottom-origin rects and
    /// normalized pivots. Both axes flip here. Note this sheet's items are TOP-aligned (every
    /// <c>y</c> is 0) where the shoreline kit's rock sheet is BOTTOM-aligned — the flip is written
    /// generally, so neither packing is special-cased.</para>
    ///
    /// <para>Slices carry the sidecar's item NAMES (<c>WharfOverlays_cleat</c>, <c>_gangway</c>, …).
    /// Same reasoning as the shoreline rock sheet: this repo names slices by grid position to stop art
    /// claiming a DIRECTION it may not have, and "cleat" is a thing, not a bearing. The three rail runs
    /// (<c>_h</c>, <c>_v</c>, <c>_d</c>) do describe orientation — see the note in
    /// <see cref="WharfKitCatalog"/>, which is where that claim is recorded and where it gets corrected
    /// if it proves wrong.</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Wharf Overlay
    /// Sprites</c> (re-runnable; existing
    /// spriteIDs are re-used so a no-op re-slice writes a byte-identical .meta).</para>
    /// </summary>
    public static class WharfOverlaySlicer
    {
        public const string SheetPath = WharfKitCatalog.WharfDir + "/WharfOverlays.png";
        public const string SidecarPath = WharfKitCatalog.WharfDir + "/WharfOverlays.json";

        /// <summary>
        /// One fitting as the sidecar states it: top-left origin rect, pixel pivot from the item's own
        /// top-left. Note the sidecar spells the pivot as two scalar fields, where the shoreline kit's
        /// used a two-element array — same idea, different shape, so the two sidecar readers stay separate
        /// rather than sharing a struct that fits neither.
        /// </summary>
        [Serializable]
        public struct SidecarItem
        {
            public string name;
            public int x, y, w, h;
            public int pivotX, pivotY;
            public string note;
        }

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Wharf Overlay Sprites", priority = 209)]
        public static void SliceMenu()
        {
            if (Slice(out int n)) Debug.Log($"[WharfOverlaySlicer] Sliced {n} wharf fitting(s).");
        }

        /// <summary>
        /// Read the sidecar and apply it. Returns false (loudly) if the sheet or sidecar is missing or
        /// if any rect falls outside the texture — a repack that drifted must fail, not slice garbage.
        /// </summary>
        public static bool Slice(out int count)
        {
            count = 0;

            if (!File.Exists(SheetPath) || !File.Exists(SidecarPath))
            {
                Debug.LogWarning($"[WharfOverlaySlicer] '{SheetPath}' or its sidecar is not on disk yet " +
                                 "— nothing to slice.");
                return false;
            }

            List<SidecarItem> items;
            try
            {
                items = ParseSidecar(File.ReadAllText(SidecarPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[WharfOverlaySlicer] '{SidecarPath}' is not readable: {e.Message}");
                return false;
            }

            if (items.Count == 0)
            {
                Debug.LogError($"[WharfOverlaySlicer] '{SidecarPath}' lists no frames.");
                return false;
            }

            var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[WharfOverlaySlicer] '{SheetPath}' has no TextureImporter.");
                return false;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (tex == null)
            {
                Debug.LogError($"[WharfOverlaySlicer] '{SheetPath}' failed to load as Texture2D.");
                return false;
            }

            foreach (var it in items)
            {
                if (it.x < 0 || it.y < 0 || it.x + it.w > tex.width || it.y + it.h > tex.height)
                {
                    Debug.LogError($"[WharfOverlaySlicer] fitting '{it.name}' rect " +
                                   $"({it.x},{it.y},{it.w},{it.h}) falls outside the {tex.width}×{tex.height} " +
                                   "sheet. Not slicing — the sidecar and the PNG are out of step.");
                    return false;
                }
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

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
        /// Pull the fittings out of the sidecar, in the order the file lists them.
        ///
        /// <para>The kit spells <c>frames</c> as a JSON OBJECT keyed by fitting name
        /// (<c>"frames": { "cleat": {…}, … }</c>) rather than an array, which <see cref="JsonUtility"/>
        /// cannot deserialize — so this goes through <see cref="MiniJson"/>, the repo's editor JSON
        /// reader, which exists for exactly this dictionary-shaped-sidecar case.</para>
        ///
        /// <para><b>Results are sorted by pack position (x, then y), NOT by the order the JSON happens to
        /// list them.</b> MiniJson builds a plain <see cref="Dictionary{TKey,TValue}"/>, whose iteration
        /// order is an implementation detail and not contractual — relying on it would make the slice
        /// order silently runtime-dependent, and slice order is what the <c>.meta</c> records. Sorting by
        /// the sheet's own left-to-right packing is both deterministic and the order a human reads the
        /// PNG in.</para>
        ///
        /// <para>Throws <see cref="FormatException"/> on malformed JSON or a frame missing a field —
        /// a sidecar the slicer cannot fully read must stop the slice, not slice most of it.</para>
        /// </summary>
        public static List<SidecarItem> ParseSidecar(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            var frames = MiniJson.Dict(root, "frames");
            if (frames == null) throw new FormatException("WharfOverlays.json has no \"frames\" object");

            var items = new List<SidecarItem>(frames.Count);
            foreach (var kv in frames)
            {
                var f = kv.Value as Dictionary<string, object>;
                if (f == null) throw new FormatException($"frame '{kv.Key}' is not an object");

                items.Add(new SidecarItem
                {
                    name = kv.Key,
                    x = Field(f, "x", kv.Key),
                    y = Field(f, "y", kv.Key),
                    w = Field(f, "w", kv.Key),
                    h = Field(f, "h", kv.Key),
                    pivotX = Field(f, "pivotX", kv.Key),
                    pivotY = Field(f, "pivotY", kv.Key),
                });
            }

            items.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x)
                               : a.y != b.y ? a.y.CompareTo(b.y)
                               : string.CompareOrdinal(a.name, b.name));
            return items;
        }

        /// <summary>
        /// Read one required integer. <see cref="MiniJson.Int"/> falls back silently on a missing key,
        /// which is right for optional anchor data and wrong here — a fitting with no width would slice
        /// to a zero-size sprite and look like art corruption rather than a bad sidecar.
        /// </summary>
        static int Field(Dictionary<string, object> frame, string key, string frameName)
        {
            if (!frame.TryGetValue(key, out object v) || !(v is double n))
                throw new FormatException($"frame '{frameName}' has no numeric \"{key}\"");
            return (int)Math.Round(n);
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

                // Sheet-space flip: the sidecar's y is the item's TOP edge measured from the sheet's top.
                float rectY = sheetHeight - (it.y + it.h);

                // Item-space flip + normalize: pivotY is measured DOWN from the item's own top.
                float px = it.pivotX / (float)it.w;
                float py = (it.h - it.pivotY) / (float)it.h;

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
