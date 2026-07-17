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

        const string ArtBoats = "Assets/_Project/Art/Boats";

        /// <summary>One skin's worth of sheets on disk → one <see cref="BoatVisualDef"/> asset.</summary>
        struct Sheet
        {
            public string AssetName;      // file written under VisualsFolder
            public string Id;             // stable def id (append-only)
            public string HullPath;       // static hull headings as ONE sliced sheet: index = heading (N..NW, CW)
            public string[] HullPaths;    // ...or one FILE PER HEADING, in the same N..NW order. Either/or.
            public string RockPath;       // rock grid: index = heading·RockFrames + frame ("" = none)
            public string OarPortPath;    // port oar sheet: index = heading·OarColumns + column ("" = none)
            public string OarStarPath;    // starboard oar sheet ("" = none)
            public string MotorLowerPath; // motor lower sheet: index = heading·MotorColumns + col ("" = none)
            public string MotorUpperPath; // motor upper sheet ("" = none)
            public int HeadingCount;
            public int RockFrames;
            public int OarColumns;
            public int MotorColumns;
            public OutboardMotorLayer.MotorVariant MotorVariant;
            public OutboardMotorLayer.MotorFit MotorFit;
            public float MotorRockRollDegrees;
            public float MotorRockPitchOffsetMeters;
            public float MotorRockHeavePixels;
            public int SortingOrder;
        }

        // The per-hull motor ROCK amplitudes are ART FACTS read off the outboard rigs, not feel knobs: they
        // pose the LEVEL-baked engine cells onto the hull's rock, which is already baked into the hull's own
        // frames. Get them wrong and the engine slides on the transom; DOUBLE them onto the hull's rock and
        // the boat shakes itself apart. The dory's reference read is roll 5° / pitch 3° / heave 1.6 px, and
        // the dory converts its 3° of pitch to 0.02 m of screen-vertical travel in the ¾ view — the same
        // ratio puts the console's 1.9° at 0.0127 m and the sport's 2.2° at 0.0147 m.
        const float ConsoleRockRoll = 3.4f, ConsoleRockPitch = 0.0127f, ConsoleRockHeave = 1.3f;  // heavier hull, stiffer
        const float SportRockRoll   = 3.8f, SportRockPitch   = 0.0147f, SportRockHeave   = 1.5f;  // light glass hull, livelier

        // The 8 compass points in the project's canonical order: element 0 = North, then CLOCKWISE. The
        // FishingBoat art ships as one file per heading rather than a sheet, so the ORDER lives here — and
        // it must be this order, because DirectionalBoatSprite indexes facings by it.
        static readonly string[] CompassSuffixes = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        static readonly Sheet[] Sheets =
        {
            // The player's iso dory (#202 hull + rock, #204 independent oars). 8 static headings; a 64-frame
            // heading×rock grid; two 80-cell oar sheets (10 columns per heading: 0..7 the stroke cycle, 8
            // resting/shipped, 9 trailing). Every layer shares the 160×156 cell + waterline pivot, so the
            // overlays register pixel-perfect on the hull at localPosition zero. ROWED — no motor.
            new Sheet
            {
                AssetName = "DoryIso", Id = "visual.dory_iso",
                HullPath = $"{ArtBoats}/DoryIso.png",
                RockPath = $"{ArtBoats}/DoryIsoRock.png",
                OarPortPath = $"{ArtBoats}/DoryOarPort.png",
                OarStarPath = $"{ArtBoats}/DoryOarStar.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10, SortingOrder = 1,
            },

            // The 8-direction fishing boat: the ODD ONE OUT of this library. Its compass is 8 SEPARATE
            // files, not a sliced sheet (see HullPaths), and it has NO rock grid and NO motor — so it wears
            // the static compass plus the legacy transform rock, which is exactly what it looked like
            // before. It is here so the owner can pilot it, not because it grew a rig.
            new Sheet
            {
                AssetName = "FishingBoat", Id = "visual.fishing_boat",
                HullPaths = CompassFiles("FishingBoat"),
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns, SortingOrder = 1,
            },

            // THE CONSOLE SKIFF — the 7 m workboat. 8 headings + a 64-frame rock grid, and a SINGLE
            // graphite-cowl outboard on the centreline (72-cell sheets, 9 steer columns).
            new Sheet
            {
                AssetName = "ConsoleSkiff", Id = "visual.console_skiff",
                HullPath = $"{ArtBoats}/ConsoleIso.png",
                RockPath = $"{ArtBoats}/ConsoleIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/SkiffMotorLower-Work.png",
                MotorUpperPath = $"{ArtBoats}/SkiffMotorUpper-Work.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns,
                MotorVariant = OutboardMotorLayer.MotorVariant.Work,
                MotorFit = OutboardMotorLayer.MotorFit.Single,
                MotorRockRollDegrees = ConsoleRockRoll,
                MotorRockPitchOffsetMeters = ConsoleRockPitch,
                MotorRockHeavePixels = ConsoleRockHeave,
                SortingOrder = 1,
            },

            // THE SPORT SKIFF — the console's glass sister, one white-cowl outboard.
            new Sheet
            {
                AssetName = "SportSkiffSingle", Id = "visual.sport_skiff_single",
                HullPath = $"{ArtBoats}/SportSkiffIso.png",
                RockPath = $"{ArtBoats}/SportSkiffIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/SkiffMotorLower-Sport.png",
                MotorUpperPath = $"{ArtBoats}/SkiffMotorUpper-Sport.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns,
                MotorVariant = OutboardMotorLayer.MotorVariant.Sport,
                MotorFit = OutboardMotorLayer.MotorFit.Single,
                MotorRockRollDegrees = SportRockRoll,
                MotorRockPitchOffsetMeters = SportRockPitch,
                MotorRockHeavePixels = SportRockHeave,
                SortingOrder = 1,
            },

            // THE SPORT SKIFF, TWIN — byte-for-byte the SAME sheets as the single, with MotorFit.Twin. The
            // second engine costs NO art: the bake is orthographic, so OutboardMotorMath.MountOffset places a
            // ±0.34 m clamp shift exactly, and the layer blits the one sheet twice. This entry exists purely
            // so the twin is a hull the owner can select, not a runtime flag someone has to remember to set.
            new Sheet
            {
                AssetName = "SportSkiffTwin", Id = "visual.sport_skiff_twin",
                HullPath = $"{ArtBoats}/SportSkiffIso.png",
                RockPath = $"{ArtBoats}/SportSkiffIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/SkiffMotorLower-Sport.png",
                MotorUpperPath = $"{ArtBoats}/SkiffMotorUpper-Sport.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns,
                MotorVariant = OutboardMotorLayer.MotorVariant.Sport,
                MotorFit = OutboardMotorLayer.MotorFit.Twin,
                MotorRockRollDegrees = SportRockRoll,
                MotorRockPitchOffsetMeters = SportRockPitch,
                MotorRockHeavePixels = SportRockHeave,
                SortingOrder = 1,
            },
        };

        /// <summary>The 8 per-heading files of a compass-as-separate-files skin, N..NW clockwise.</summary>
        static string[] CompassFiles(string stem)
        {
            var paths = new string[CompassSuffixes.Length];
            for (int i = 0; i < paths.Length; i++) paths[i] = $"{ArtBoats}/{stem}_{CompassSuffixes[i]}.png";
            return paths;
        }

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
            def.MotorColumnCount = Mathf.Max(1, sheet.MotorColumns);
            def.MotorVariant = sheet.MotorVariant;
            def.MotorFit = sheet.MotorFit;
            if (sheet.MotorRockRollDegrees > 0f) def.MotorRockRollDegrees = sheet.MotorRockRollDegrees;
            if (sheet.MotorRockPitchOffsetMeters > 0f) def.MotorRockPitchOffsetMeters = sheet.MotorRockPitchOffsetMeters;
            if (sheet.MotorRockHeavePixels > 0f) def.MotorRockHeavePixels = sheet.MotorRockHeavePixels;

            // All-or-nothing per block, mirroring BoatVisualDef's own gates: a partial sheet is dropped
            // whole rather than half-bound, because one missing slice would index a stale cell.
            def.Facings = sheet.HullPaths != null && sheet.HullPaths.Length > 0
                ? TakeOnePerFile(sheet.HullPaths, sheet.HeadingCount)
                : TakeExactly(sheet.HullPath, sheet.HeadingCount);
            def.RockGrid = TakeExactly(sheet.RockPath, sheet.HeadingCount * sheet.RockFrames);
            def.OarPort = TakeExactly(sheet.OarPortPath, sheet.HeadingCount * sheet.OarColumns);
            def.OarStar = TakeExactly(sheet.OarStarPath, sheet.HeadingCount * sheet.OarColumns);
            def.MotorLower = TakeExactly(sheet.MotorLowerPath, sheet.HeadingCount * sheet.MotorColumns);
            def.MotorUpper = TakeExactly(sheet.MotorUpperPath, sheet.HeadingCount * sheet.MotorColumns);

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

            // Oars + an outboard on one hull is a z-fighting authoring mistake, not a boat (their sorting
            // bands overlap). Nothing in this library does it; catch it HERE, at authoring time, rather
            // than leaving the owner to notice an engine flickering behind an oar.
            if (def.HasConflictingOverlays())
            {
                Debug.LogError($"[BoatVisualLibraryBuilder] {sheet.AssetName} binds BOTH oar sheets and " +
                               "motor sheets — their sorting bands overlap and the skinner will drop the " +
                               "motor. Author the hull as rowed OR powered.");
                return false;
            }

            Debug.Log($"[BoatVisualLibraryBuilder] {sheet.AssetName}: {def.HeadingCount} facings, rock grid " +
                      $"{(def.HasRockGrid() ? "WIRED" : "none")}, oars {(def.HasOarSheets() ? "WIRED" : "none")}, " +
                      $"motor {(def.HasMotor() ? $"WIRED ({def.MotorVariant}, {def.MotorFit})" : "none")}.");
            return true;
        }

        /// <summary>
        /// One sprite per FILE, in the caller's given order — the shape the <c>FishingBoat_*</c> compass
        /// ships in (8 separate single-mode PNGs, not a sliced sheet). Order comes from the path list, NOT
        /// from a name suffix, because these files are named by BEARING (<c>_NE</c>) rather than by index,
        /// so there is no number to sort on. All-or-nothing like every other block: one missing file drops
        /// the whole compass rather than leaving a hole that would snap to a stale facing mid-turn.
        /// </summary>
        static Sprite[] TakeOnePerFile(string[] paths, int expected)
        {
            if (paths == null || paths.Length != expected) return System.Array.Empty<Sprite>();

            var frames = new Sprite[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                // These import single-mode, so LoadAssetAtPath works — but fall back to the sub-asset scan
                // anyway, so a later re-slice to Multiple doesn't silently empty the compass.
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i])
                         ?? AssetDatabase.LoadAllAssetsAtPath(paths[i]).OfType<Sprite>().FirstOrDefault();
                if (frames[i] == null) return System.Array.Empty<Sprite>();
            }
            return frames;
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
