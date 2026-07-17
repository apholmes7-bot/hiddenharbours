#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The one place that knows where boat-skin sheets live on disk</b> — and the ONLY place that
    /// should. It imports the owner's sliced sheets into a <see cref="BoatVisualDef"/> asset, after which
    /// everything downstream (the start builder, <see cref="OwnedFleet"/>, the ambient fleet, the tests)
    /// reads DATA and never a path.
    ///
    /// <para>This exists because "how the boat looks" used to be a <c>const bool</c> plus a fistful of
    /// <c>const string</c> art paths buried in <see cref="PersistentCoreBuilder"/>. Art paths are an
    /// IMPORT concern, not a gameplay one: they belong in an editor-only importer that runs once and
    /// writes an asset, not in the rig that stands the boat up. Everything past the asset is data (rule
    /// 2), so a new hull's look is a new asset, not a new branch in C#.</para>
    ///
    /// <para><b>The generated asset is committed</b> — the owner does NOT need to run this after every
    /// pull. Run it only when the art changes: if a sheet is RE-SLICED the sprite sub-asset ids change and
    /// the def's refs go stale (the whole compass turns to None), which is what re-running repairs. It is
    /// non-destructive: it refreshes an existing asset in place, keeping its guid, so nothing that points
    /// at the def is broken by a re-run.</para>
    ///
    /// <para><b>Adding a skin for a new hull:</b> add a <see cref="Sheet"/> entry below and point that
    /// hull's <see cref="BoatHullDef.Visual"/> at the asset it writes. Nothing else in the codebase needs
    /// to learn the new hull exists.</para>
    /// </summary>
    public static class BoatVisualLibraryBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Build Boat Visual Defs";
        const string VisualsFolder = "Assets/_Project/Data/Boats/Visuals";

        /// <summary>One skin's worth of sheets on disk → one <see cref="BoatVisualDef"/> asset.</summary>
        struct Sheet
        {
            public string AssetName;      // file written under VisualsFolder
            public string Id;             // stable def id (append-only)
            public string HullPath;       // static hull headings: index = heading (N..NW, CW)
            public string RockPath;       // rock grid: index = heading·RockFrames + frame ("" = none)
            public string OarPortPath;    // port oar sheet: index = heading·OarColumns + column ("" = none)
            public string OarStarPath;    // starboard oar sheet ("" = none)
            public int HeadingCount;
            public int RockFrames;
            public int OarColumns;
            public int SortingOrder;
        }

        // The player's iso dory (#202 hull + rock, #204 independent oars). 8 static headings; a 64-frame
        // heading×rock grid; two 80-cell oar sheets (10 columns per heading: 0..7 the stroke cycle, 8
        // resting/shipped, 9 trailing). Every layer shares the 160×156 cell + waterline pivot, so the
        // overlays register pixel-perfect on the hull at localPosition zero.
        static readonly Sheet[] Sheets =
        {
            new Sheet
            {
                AssetName = "DoryIso", Id = "visual.dory_iso",
                HullPath = "Assets/_Project/Art/Boats/DoryIso.png",
                RockPath = "Assets/_Project/Art/Boats/DoryIsoRock.png",
                OarPortPath = "Assets/_Project/Art/Boats/DoryOarPort.png",
                OarStarPath = "Assets/_Project/Art/Boats/DoryOarStar.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10, SortingOrder = 1,
            },
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            EnsureFolder(VisualsFolder);
            int ok = 0;
            foreach (var sheet in Sheets)
                if (BuildOne(sheet)) ok++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BoatVisualLibraryBuilder] Refreshed {ok}/{Sheets.Length} boat visual def(s) in " +
                      $"{VisualsFolder}. Commit them; re-run only when the sheets are re-sliced.");
        }

        static bool BuildOne(Sheet sheet)
        {
            string path = $"{VisualsFolder}/{sheet.AssetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<BoatVisualDef>();

            def.Id = sheet.Id;
            def.SortingOrder = sheet.SortingOrder;
            def.ZeroHeadingDegrees = 0f;             // element 0 is the North-facing picture
            def.RockFrameCount = Mathf.Max(1, sheet.RockFrames);
            def.OarColumnCount = Mathf.Max(1, sheet.OarColumns);

            // All-or-nothing per block, mirroring BoatVisualDef's own gates: a partial sheet is dropped
            // whole rather than half-bound, because one missing slice would index a stale cell.
            def.Facings = TakeExactly(sheet.HullPath, sheet.HeadingCount);
            def.RockGrid = TakeExactly(sheet.RockPath, sheet.HeadingCount * sheet.RockFrames);
            def.OarPort = TakeExactly(sheet.OarPortPath, sheet.HeadingCount * sheet.OarColumns);
            def.OarStar = TakeExactly(sheet.OarStarPath, sheet.HeadingCount * sheet.OarColumns);

            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);

            if (!def.HasFullCompass())
            {
                Debug.LogWarning($"[BoatVisualLibraryBuilder] {sheet.AssetName}: '{sheet.HullPath}' gave " +
                                 $"{def.Facings.Length}/{sheet.HeadingCount} ordered slices — the compass is " +
                                 "EMPTY, so any hull pointing here renders its plain rotating Sprite. Slice " +
                                 "the sheet (Hidden Harbours ▸ Art ▸ Slice…) and re-run.");
                return false;
            }

            Debug.Log($"[BoatVisualLibraryBuilder] {sheet.AssetName}: {def.HeadingCount} facings, rock grid " +
                      $"{(def.HasRockGrid() ? "WIRED" : "none")}, oars {(def.HasOarSheets() ? "WIRED" : "none")}.");
            return true;
        }

        /// <summary>
        /// The sheet's sprites in slice order, but ONLY if it gives exactly the expected count — otherwise
        /// an empty set (the block's all-or-nothing gate). Sliced sheets import as spriteMode Multiple, so
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null and the sub-assets must be loaded and ordered
        /// by their <c>&lt;Stem&gt;_&lt;index&gt;</c> suffix — the project's row-major convention.
        /// </summary>
        static Sprite[] TakeExactly(string path, int expected)
        {
            if (string.IsNullOrEmpty(path) || expected <= 0) return System.Array.Empty<Sprite>();
            var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                                      .OrderBy(SpriteIndex).ToArray();
            return (frames.Length == expected && frames.All(s => s != null))
                ? frames : System.Array.Empty<Sprite>();
        }

        static int SpriteIndex(Sprite s)
        {
            int u = s.name.LastIndexOf('_');
            return (u >= 0 && int.TryParse(s.name.Substring(u + 1), out int n)) ? n : 0;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
#endif
