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
            public float MotorMaxSteerDegrees;   // sheet's baked authority: skiffs ±30, punt ±32 (0 = leave default)
            public OutboardMotorLayer.MotorVariant MotorVariant;
            public OutboardMotorLayer.MotorFit MotorFit;
            public float MotorRockRollDegrees;
            public float MotorRockPitchDegrees;      // the rigs' pitchA, in DEGREES — a rotation, not an offset
            public float MotorRockHeavePixels;
            public Vector3 MotorMountLocalMeters;    // the rigs' MOUNT (at motorMount's y - 0.03), boat-local m
            public float ArtBakeElevationDegrees;    // the rigs' DEFAULT_ELEV; 90 = not a bake, do not foreshorten
            public int SortingOrder;
            public bool FacingsAreCounterClockwise;
        }

        // THE ISO SHEETS ARE BAKED COUNTER-CLOCKWISE. Every iso kit here (dory, punt, skiffs) comes off the
        // same 3D rig recipe, and that recipe rotates the model CCW — projVert's `xr = x1·ct − y2·stt` with
        // th = +dir·45° and bow = +y — and then declares the cells clockwise ('N','NE','E',...). So cell i
        // actually DEPICTS heading −45°·i: the 'E' cell is a boat pointing West. Drawn-minus-true = −2·heading,
        // which is 0 at N/S (why it hid), 90° at the diagonals and a full 180° at E/W.
        //
        // This is an ART FACT of those sheets, and it is per-artwork rather than a blanket code fix for one
        // reason: the FishingBoat_* compass below is a DIFFERENT lineage — 8 hand-drawn files, labelled
        // CORRECTLY (verified from its pixels: E's bow points right). Mirroring everything would have fixed
        // the iso kits and broken the fishing boat and the whole ambient fleet that shares its facings.
        const bool IsoSheetsAreCounterClockwise = true;   // the rigs' bake order — not a feel knob
        const bool CompassFilesAreClockwise = false;      // the hand-drawn FishingBoat compass: already right

        // The per-hull motor ROCK amplitudes are ART FACTS read STRAIGHT off the outboard rigs' ROCK block —
        // not feel knobs, and no longer derived from anything. They pose the LEVEL-baked engine cells onto the
        // hull's rock, which is already baked into the hull's own frames. Get them wrong and the engine slides
        // on the transom; DOUBLE them onto the hull's rock and the boat shakes itself apart.
        //
        //   hull      rollA  pitchA  heaveA   (rig ROCK)     character
        //   dory       5.0    3.0     1.6     the reference: light, narrow, corks about
        //   punt       4.2    2.4     1.5     beamier than the dory → a stiffer roll than her
        //   sport      3.8    2.2     1.5     light glass hull, livelier
        //   console    3.4    1.9     1.3     heaviest hull, stiffest
        //
        // THE PITCH FIELD IS NOW THE RIG'S pitchA, IN DEGREES. It used to be screen-vertical METRES, filled by
        // pushing each rig's pitchA through an "exchange rate" of 0.02 m / 3.0° = 0.006667 m per degree —
        // borrowed from the DORY, whose 0.02 was itself hand-tuned and has no derivation anywhere in this
        // project. The rate was linear in pitchA, which looked principled, but the quantity it stood in for is
        // dominated by the MOUNT'S LEVER ARM, which it ignored entirely: the dory's 0.02 was tuned for oarlocks
        // near amidships, while an outboard hangs ~3.5 m AFT. The result was ~8x too small and, worse, the
        // wrong SIGN — a positive pitch is bow-UP, which drops the stern and everything clamped to it, but the
        // code LIFTED the engine. The motor rocked in anti-phase with its own transom: the owner's "the skiffs
        // motor doesnt rock/bounce in synch with the boat itself, it seems to bounce independtly".
        //
        // Nothing is converted now. MountedRockPoseMath re-runs the rig's own camera over the MOUNT below and
        // works the screen travel out per heading, so these three numbers are only ever transcription.
        const float ConsoleRockRoll = 3.4f, ConsoleRockPitch = 1.9f, ConsoleRockHeave = 1.3f;  // heavier hull, stiffer
        const float SportRockRoll   = 3.8f, SportRockPitch   = 2.2f, SportRockHeave   = 1.5f;  // light glass hull, livelier
        const float PuntRockRoll    = 4.2f, PuntRockPitch    = 2.4f, PuntRockHeave    = 1.5f;  // beamier boat, stiffer roll than the dory

        // WHERE THE ENGINE ACTUALLY HANGS, in boat-local metres (x = starboard, y = bow, z = up) — the rigs'
        //     const MOUNT = { x:0, y:-L/2, z:T[0][3]+T[0][2] }
        // read at the point motorMount() actually projects, which is MOUNT.y - 0.03 (just aft of the transom
        // top). Both skiffs are 7.0 m with a 0.06 keel + 0.66 transom depth; the punt is 5.2 m with 0.08 +
        // 0.48. This is the lever arm the whole rock pose turns on — it is not a nudge knob.
        static readonly Vector3 SkiffMotorMount = new Vector3(0f, -3.53f, 0.72f);   // consoleIsoRig / sportSkiffIsoRig
        static readonly Vector3 PuntMotorMount  = new Vector3(0f, -2.63f, 0.56f);   // puntIsoRig

        // The camera every iso rig bakes at (their DEFAULT_ELEV). It squashes along-heading distance on screen-Y
        // by sin(40°) ~ 0.643 and leaves screen-X alone — which is why anything pinned to a point ON the drawn
        // hull (the wake at the transom, the spray at the cutwater, the outboard's mount) has to be projected
        // rather than placed in top-down metres.
        const float IsoBakeElevation = 40f;

        // ...and the elevation for art that is NOT a rig bake. 90 = a plan view = no foreshortening = exactly
        // where the FishingBoat compass's wake has always gone. Same reasoning as CompassFilesAreClockwise
        // above: that art is a different lineage and must not be "fixed" by the iso kits' facts.
        const float PlanViewBakeElevation = 90f;

        // Steer authority baked into each kit's motor sheets, in degrees either side of dead ahead. NOT a
        // feel knob — it says what the 9 columns are DRAWN at, and the punt is genuinely different from the
        // skiffs: her rig is angle(f) = −32 + 64·f/8 (8° steps) where theirs is −30 + 60·f/8 (7.5° steps).
        const float SkiffMaxSteer = 30f;
        const float PuntMaxSteer  = 32f;

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
                AssetName = "DoryIso", FacingsAreCounterClockwise = IsoSheetsAreCounterClockwise, Id = "visual.dory_iso",
                HullPath = $"{ArtBoats}/DoryIso.png",
                RockPath = $"{ArtBoats}/DoryIsoRock.png",
                OarPortPath = $"{ArtBoats}/DoryOarPort.png",
                OarStarPath = $"{ArtBoats}/DoryOarStar.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10, SortingOrder = 1,
                // The dory is an iso bake like the rest. She has no motor, but her WAKE is anchored on her
                // drawn transom and so is foreshortened exactly the same way — which is the owner's "the wake
                // from the rowboat still doesnt ALWAYS seem accurate". Always: it was only ever right on the
                // E/W axis, where the ¾ camera happens not to squash the along-heading distance at all.
                ArtBakeElevationDegrees = IsoBakeElevation,
            },

            // The 8-direction fishing boat: the ODD ONE OUT of this library. Its compass is 8 SEPARATE
            // files, not a sliced sheet (see HullPaths), and it has NO rock grid and NO motor — so it wears
            // the static compass plus the legacy transform rock, which is exactly what it looked like
            // before. It is here so the owner can pilot it, not because it grew a rig.
            new Sheet
            {
                AssetName = "FishingBoat", FacingsAreCounterClockwise = CompassFilesAreClockwise, Id = "visual.fishing_boat",
                HullPaths = CompassFiles("FishingBoat"),
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns, SortingOrder = 1,
                // NOT a rig bake — 8 hand-drawn files with no camera to measure. Declared a plan view, which
                // leaves its wake (and the whole ambient fleet's, which wears these very facings) exactly where
                // it has always been. Foreshortening it would be inventing a camera it never had.
                ArtBakeElevationDegrees = PlanViewBakeElevation,
            },

            // THE CONSOLE SKIFF — the 7 m workboat. 8 headings + a 64-frame rock grid, and a SINGLE
            // graphite-cowl outboard on the centreline (72-cell sheets, 9 steer columns).
            new Sheet
            {
                AssetName = "ConsoleSkiff", FacingsAreCounterClockwise = IsoSheetsAreCounterClockwise, Id = "visual.console_skiff",
                HullPath = $"{ArtBoats}/ConsoleIso.png",
                RockPath = $"{ArtBoats}/ConsoleIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/SkiffMotorLower-Work.png",
                MotorUpperPath = $"{ArtBoats}/SkiffMotorUpper-Work.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns, MotorMaxSteerDegrees = SkiffMaxSteer,
                MotorVariant = OutboardMotorLayer.MotorVariant.Work,
                MotorFit = OutboardMotorLayer.MotorFit.Single,
                MotorRockRollDegrees = ConsoleRockRoll,
                MotorRockPitchDegrees = ConsoleRockPitch,
                MotorRockHeavePixels = ConsoleRockHeave,
                MotorMountLocalMeters = SkiffMotorMount,
                ArtBakeElevationDegrees = IsoBakeElevation,
                SortingOrder = 1,
            },

            // THE SPORT SKIFF — the console's glass sister, one white-cowl outboard.
            new Sheet
            {
                AssetName = "SportSkiffSingle", FacingsAreCounterClockwise = IsoSheetsAreCounterClockwise, Id = "visual.sport_skiff_single",
                HullPath = $"{ArtBoats}/SportSkiffIso.png",
                RockPath = $"{ArtBoats}/SportSkiffIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/SkiffMotorLower-Sport.png",
                MotorUpperPath = $"{ArtBoats}/SkiffMotorUpper-Sport.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns, MotorMaxSteerDegrees = SkiffMaxSteer,
                MotorVariant = OutboardMotorLayer.MotorVariant.Sport,
                MotorFit = OutboardMotorLayer.MotorFit.Single,
                MotorRockRollDegrees = SportRockRoll,
                MotorRockPitchDegrees = SportRockPitch,
                MotorRockHeavePixels = SportRockHeave,
                MotorMountLocalMeters = SkiffMotorMount,
                ArtBakeElevationDegrees = IsoBakeElevation,
                SortingOrder = 1,
            },

            // THE SPORT SKIFF, TWIN — byte-for-byte the SAME sheets as the single, with MotorFit.Twin. The
            // second engine costs NO art: the bake is orthographic, so OutboardMotorMath.MountOffset places a
            // ±0.34 m clamp shift exactly, and the layer blits the one sheet twice. This entry exists purely
            // so the twin is a hull the owner can select, not a runtime flag someone has to remember to set.
            new Sheet
            {
                AssetName = "SportSkiffTwin", FacingsAreCounterClockwise = IsoSheetsAreCounterClockwise, Id = "visual.sport_skiff_twin",
                HullPath = $"{ArtBoats}/SportSkiffIso.png",
                RockPath = $"{ArtBoats}/SportSkiffIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/SkiffMotorLower-Sport.png",
                MotorUpperPath = $"{ArtBoats}/SkiffMotorUpper-Sport.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns, MotorMaxSteerDegrees = SkiffMaxSteer,
                MotorVariant = OutboardMotorLayer.MotorVariant.Sport,
                MotorFit = OutboardMotorLayer.MotorFit.Twin,
                MotorRockRollDegrees = SportRockRoll,
                MotorRockPitchDegrees = SportRockPitch,
                MotorRockHeavePixels = SportRockHeave,
                MotorMountLocalMeters = SkiffMotorMount,
                ArtBakeElevationDegrees = IsoBakeElevation,
                SortingOrder = 1,
            },

            // THE PUNT, BASIC — the ~5.2 m TILLER punt on her starter engine (#210's kit). 8 headings + a
            // 64-frame rock grid, and a SINGLE weathered grey/black outboard on the centreline (72-cell
            // sheets, 9 steer columns). NO console, NO wheel, NO twin: she is tiller-steered and the art
            // ships no second engine.
            //
            // Two things here are NOT the skiffs' and must not be "tidied" into them:
            //   (1) ±32° of steer, in 8° steps (theirs is ±30 / 7.5°) — her rig bakes the columns wider;
            //   (2) her own cell + pivot (184×168 hull, 212×168 motor, origin y = 0.4405 vs the skiffs'
            //       0.4444). That lives in the slicer, and PuntSheetSliceTests guards the distinction.
            //
            // Her tiller-grip JSON (PuntMotorGrips.json) is committed but deliberately UNWIRED — it seats an
            // operator's aft hand, and no punt operator sprite exists yet. Do not fake one.
            new Sheet
            {
                AssetName = "PuntIsoBasic", FacingsAreCounterClockwise = IsoSheetsAreCounterClockwise, Id = "visual.punt_iso_basic",
                HullPath = $"{ArtBoats}/PuntIso.png",
                RockPath = $"{ArtBoats}/PuntIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/PuntMotorLower-Basic.png",
                MotorUpperPath = $"{ArtBoats}/PuntMotorUpper-Basic.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns, MotorMaxSteerDegrees = PuntMaxSteer,
                MotorVariant = OutboardMotorLayer.MotorVariant.Basic,
                MotorFit = OutboardMotorLayer.MotorFit.Single,
                MotorRockRollDegrees = PuntRockRoll,
                MotorRockPitchDegrees = PuntRockPitch,
                MotorRockHeavePixels = PuntRockHeave,
                MotorMountLocalMeters = PuntMotorMount,
                ArtBakeElevationDegrees = IsoBakeElevation,
                SortingOrder = 1,
            },

            // THE PUNT, UPGRADED — the SAME hull and the SAME rock, wearing the upgraded engine: ~15% larger
            // domed cowl, gloss-black pan, white top, red wrap stripe, brighter prop. The art README is
            // explicit that the two builds "share the SAME cell, pivot, steer cols and grip JSON — the sheets
            // are drop-in swaps", so the upgrade costs exactly two PNG paths and one MotorVariant. Everything
            // else on this entry is byte-for-byte the basic's, and a test asserts that rather than trusting it.
            new Sheet
            {
                AssetName = "PuntIsoUpgraded", FacingsAreCounterClockwise = IsoSheetsAreCounterClockwise, Id = "visual.punt_iso_upgraded",
                HullPath = $"{ArtBoats}/PuntIso.png",
                RockPath = $"{ArtBoats}/PuntIsoRock.png",
                MotorLowerPath = $"{ArtBoats}/PuntMotorLower-Upgraded.png",
                MotorUpperPath = $"{ArtBoats}/PuntMotorUpper-Upgraded.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                MotorColumns = OutboardMotorMath.SteerColumns, MotorMaxSteerDegrees = PuntMaxSteer,
                MotorVariant = OutboardMotorLayer.MotorVariant.Upgraded,
                MotorFit = OutboardMotorLayer.MotorFit.Single,
                MotorRockRollDegrees = PuntRockRoll,
                MotorRockPitchDegrees = PuntRockPitch,
                MotorRockHeavePixels = PuntRockHeave,
                MotorMountLocalMeters = PuntMotorMount,
                ArtBakeElevationDegrees = IsoBakeElevation,
                SortingOrder = 1,
            },

            // THE CAPE ISLANDER — the ~12.9 m inshore working boat, and the first hull in this library with
            // NEITHER oars NOR an outboard: she is inboard-diesel, so her kit ships a hull sheet and a rock
            // grid and nothing else. That is not a gap to be filled in later — there is no engine drawn on
            // her because there is no engine to draw, and adding one would be inventing art.
            //
            // Her ArtBakeElevationDegrees still matters, and matters MORE than on the small boats: her wake
            // anchors at LengthMeters·0.5 = 6.45 m astern, so a wrong elevation would throw the plume metres
            // out rather than centimetres.
            //
            // FacingsAreCounterClockwise = true, like every other iso kit — and MEASURED for this artwork
            // rather than assumed from the others. CapeIslanderFacingTests reads her pixels: the un-
            // foreshortened principal axis, disambiguated bow-from-stern by the raised forefoot, walks
            // 0/320/278/226/180/134/82/37 across the 8 cells. That is 4.1° from the CCW sequence and 86.1°
            // from the CW labelling — the same method reproduces the dory/punt/console to 1.5–2.9°, so the
            // margin is not marginal. See the block comment on IsoSheetsAreCounterClockwise above.
            new Sheet
            {
                AssetName = "CapeIslanderIso", FacingsAreCounterClockwise = IsoSheetsAreCounterClockwise,
                Id = "visual.cape_islander_iso",
                HullPath = $"{ArtBoats}/CapeIslanderIso.png",
                RockPath = $"{ArtBoats}/CapeIslanderIsoRock.png",
                HeadingCount = 8, RockFrames = 8, OarColumns = 10,
                ArtBakeElevationDegrees = IsoBakeElevation,
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
            // Which way this artwork's cells actually run. The iso rigs bake CCW and label CW; the
            // hand-drawn FishingBoat compass is genuinely CW. Stated per sheet above, never assumed.
            def.FacingsAreCounterClockwise = sheet.FacingsAreCounterClockwise;
            def.RockFrameCount = Mathf.Max(1, sheet.RockFrames);
            def.OarColumnCount = Mathf.Max(1, sheet.OarColumns);
            def.MotorColumnCount = Mathf.Max(1, sheet.MotorColumns);
            // Non-positive = leave the asset's existing authority alone, mirroring the rock amplitudes below:
            // a hull with no motor (the dory) has nothing to describe, and silently writing 0 would tell the
            // layer this sheet's engine cannot leave dead ahead.
            if (sheet.MotorMaxSteerDegrees > 0f) def.MotorMaxSteerDegrees = sheet.MotorMaxSteerDegrees;
            def.MotorVariant = sheet.MotorVariant;
            def.MotorFit = sheet.MotorFit;
            if (sheet.MotorRockRollDegrees > 0f) def.MotorRockRollDegrees = sheet.MotorRockRollDegrees;
            if (sheet.MotorRockPitchDegrees > 0f) def.MotorRockPitchDegrees = sheet.MotorRockPitchDegrees;
            if (sheet.MotorRockHeavePixels > 0f) def.MotorRockHeavePixels = sheet.MotorRockHeavePixels;
            if (sheet.MotorMountLocalMeters.sqrMagnitude > 1e-6f)
                def.MotorMountLocalMeters = sheet.MotorMountLocalMeters;
            // Written UNCONDITIONALLY, unlike the motor block above: every sheet either has a camera or is
            // declaring at 90 that it has none, and a silent 0 would foreshorten every anchor on that hull
            // onto the boat's own middle.
            def.ArtBakeElevationDegrees = Mathf.Clamp(sheet.ArtBakeElevationDegrees, 0f, 90f);

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
                      $"motor {(def.HasMotor() ? $"WIRED ({def.MotorVariant}, {def.MotorFit}, ±{def.MotorMaxSteerDegrees:0.#}° steer)" : "none")}.");
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
