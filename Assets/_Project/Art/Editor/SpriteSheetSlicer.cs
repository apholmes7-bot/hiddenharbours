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
    /// General, manifest-driven grid slicer for the non-foliage sprite sheets in the art director's
    /// environment/VFX drop (see <c>Art/imported-assets.md</c>). Where <see cref="FoliageSheetSlicer"/>
    /// keys tiers off a filename suffix under one folder, this drives off an explicit
    /// <see cref="Sheets"/> manifest of <c>(assetPath, cols, rows, cellW, cellH, alignment, pivot)</c> so
    /// each sheet's exact grid + pivot is spelled out and dimension-guarded.
    ///
    /// <para>What it bakes (equal-cell grids that <see cref="ArtImportPipeline"/> deliberately does not —
    /// the postprocessor only stamps the pixel-art import lock + a Single-mode default pivot):</para>
    /// <list type="bullet">
    ///   <item><b>Shoreline finds</b> — <c>Sprites/Shore/Finds/*</c>, each 2 cols (variant a/b) × 2 rows
    ///         (wet/dry) → 4 sprites, centre pivot.</item>
    ///   <item><b>Seaweed clumps</b> — <c>Sprites/Shore/Seaweed*</c>, each 3 cols (float-a · float-b ·
    ///         beached) × 1 row → 3 sprites, centre pivot.</item>
    ///   <item><b>Deck fish tray</b> — <c>Sprites/Gear/FishTray</c>, 5 cols (fill 0..4) × 3 rows
    ///         (lobster / crab / mixed) → 15 sprites, bottom-centre pivot.</item>
    /// </list>
    ///
    /// <para>Slices are named <c>&lt;FileStem&gt;_&lt;index&gt;</c>, row-major from the <b>top-left</b> cell
    /// (Unity's sprite rects are bottom-origin, so the top row maps to the highest Y). This matches the
    /// repo's <c>PlayerHaul_0..N</c> / flower-sheet scheme so sheet loaders sort predictably.</para>
    ///
    /// <para>Import/slice only — this never wires a sprite into a scene, prefab, or spawner. Sheets not in
    /// the manifest are untouched; a sheet whose on-disk dimensions don't match its manifest cell grid
    /// fails loudly (no silent garbage slice).</para>
    /// </summary>
    public static class SpriteSheetSlicer
    {
        /// <summary>One sheet in the drop: its asset path, cell grid, and pivot.</summary>
        private readonly struct SheetSpec
        {
            public readonly string AssetPath;
            public readonly int Cols, Rows, CellW, CellH;
            public readonly SpriteAlignment Alignment;
            public readonly Vector2 Pivot;

            public SheetSpec(string assetPath, int cols, int rows, int cellW, int cellH,
                             SpriteAlignment alignment, Vector2 pivot)
            {
                AssetPath = assetPath; Cols = cols; Rows = rows; CellW = cellW; CellH = cellH;
                Alignment = alignment; Pivot = pivot;
            }

            public int Count => Cols * Rows;
            public int SheetW => Cols * CellW;
            public int SheetH => Rows * CellH;
            public string Stem => Path.GetFileNameWithoutExtension(AssetPath);
        }

        private const string Root = "Assets/_Project/Art/";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

        // The iso-dory waterline pivot: the art director's README fixes the anchor at (80, 88) measured
        // from each 160×156 cell's TOP-LEFT (the hull's waterline contact point). Unity pivots are
        // normalized from the BOTTOM-left, so the bottom-origin y is (156−88)=68 → (80/160, 68/156) =
        // ≈(0.5, 0.4359). Every heading/frame slice shares it so a heading- or rock-frame swap never
        // shifts the boat (README: "so a heading- or frame-swap never shifts the boat").
        private static readonly Vector2 DoryWaterline = new Vector2(80f / 160f, 68f / 156f);

        // The skiff-fleet boat origin (amidships, keel bottom, centreline), derived exactly as
        // DoryWaterline above. The kit README fixes the anchor at (122,120) from a 244×216 HULL cell's
        // TOP-LEFT and at (136,120) from a 272×216 MOTOR cell's TOP-LEFT — two different cell widths
        // pinning the SAME world point (the motor cell is wider on purpose so hard-over and raised poses
        // never clip). Flipping to Unity's bottom-left origin, y = (216−120) = 96 for both:
        //     hull  → (122/244, 96/216) = (0.5, 0.4444…)
        //     motor → (136/272, 96/216) = (0.5, 0.4444…)
        // Both normalize to the SAME pivot — that identity IS the mechanism that lands the wider motor
        // cell on the transom, so ONE const serves every sheet in the fleet (a second motor-only const
        // would be the same numbers). Verified pixel-exact (zero diff over all 8 headings, both hulls,
        // both paint builds) by re-compositing the kit's _preview-*.png reference sheets from these
        // slices at this pivot. README: "Composite every layer by pinning its pivot to one screen point.
        // Do NOT align by the top-left corner."
        private static readonly Vector2 SkiffOrigin = new Vector2(122f / 244f, 96f / 216f);

        // The iso-punt boat origin (amidships, keel bottom, centreline), derived exactly as the two consts
        // above — but from the punt kit's OWN, SMALLER cells, so this is NOT SkiffOrigin and must not be
        // folded into it. The punt README fixes the anchor at (92,94) from a 184×168 HULL cell's TOP-LEFT
        // and at (106,94) from a 212×168 MOTOR cell's TOP-LEFT — again two cell widths pinning the SAME
        // world point (the motor cell is wider on purpose so hard-over and raised poses never clip).
        // Flipping to Unity's bottom-left origin, y = (168−94) = 74 for both:
        //     hull  → (92/184,  74/168) = (0.5, 0.440476…)
        //     motor → (106/212, 74/168) = (0.5, 0.440476…)
        // Both normalize to the SAME pivot — that identity IS the mechanism that lands the wider motor cell
        // on the transom, so ONE const serves every sheet in the kit. The y differs from the skiffs'
        // (0.4405 vs 0.4444): same anchor *concept*, different cell — reusing SkiffOrigin would sink the
        // punt ~0.7 px at PPU 32. Verified pixel-exact (zero RGB diff over all 8 headings, both paint
        // builds) by re-compositing the kit's _preview-*.png reference sheets from these slices at this
        // pivot in the documented draw order. README: "Composite every layer by pinning its pivot to one
        // screen point. Do NOT align by the top-left corner."
        private static readonly Vector2 PuntOrigin = new Vector2(92f / 184f, 74f / 168f);

        // The Cape Islander's boat origin — and THE ONE NUMBER IN HER KIT THAT WAS MEASURED, NOT DECLARED.
        // Every other iso kit shipped a README fixing its anchor in cell pixels (dory (80,88), skiffs
        // (122,120), punt (92,94)); the Cape Islander arrived as two loose PNGs with no README and no rig,
        // so her (228, 263) from the 456×420 cell's TOP-LEFT was recovered from the pixels. Flipped to
        // Unity's bottom-left origin, y = (420−263) = 157 → (228/456, 157/420) = (0.5, 0.373809…).
        //
        // HOW IT WAS RECOVERED (two independent estimators, each calibrated against the three kits whose
        // true anchors ARE documented, so the method is checked before it is trusted):
        //   x — the cardinal cells' silhouettes are mirror-symmetric about the exact cell centre
        //       (measured 227.5 = (456−1)/2, matching the dory's 79.5 and the skiffs' 121.5). x = 0.5.
        //   y(a) — the punt rig's projection is sy = cy − (yr·sin e + z·cos e)·32, so in the BROADSIDE
        //       cell screen-x is the along-boat coordinate at full scale with NO foreshortening, and the
        //       extreme left/right columns are the stem and transom ON THE CENTRELINE. The mean of the
        //       silhouette's bottom row at those two ends reproduces the documented anchor to +2/−2/−4/+1.5
        //       px on dory/punt/console/sport; on the Cape Islander it gives 263.
        //   y(b) — the lowest drawn pixel of the bow-away cell sits a fixed fraction of the drawn hull
        //       length below the anchor: 0.295/0.308/0.313/0.305 across the four known kits (sd 0.008).
        //       At her 412 px drawn length that puts the anchor at 262.3.
        // The two agree to 0.7 px (≈0.02 m at PPU 32); 263 is the integer they bracket. Residual scatter
        // across the calibration kits is ≈±4 px (≈0.12 m), and THAT is the honest uncertainty on this
        // number — it is the one thing in her kit a README would settle outright. If the owner ever gets
        // the rig or the README from his art director, check this const first.
        private static readonly Vector2 CapeIslanderOrigin = new Vector2(228f / 456f, 157f / 420f);

        // The lobster boat needed none of the calibration agony above: she is baked in-engine, so
        // her pivot is READ FROM THE RIG (LobsterBoatIso.pivot = 228,258 measured from the TOP-left)
        // and converted once — (228/456, (420−258)/420). Exact, not inferred from drawn length.
        // That is the quiet win of ADR 0021: the metadata survives the export instead of being
        // re-measured by eye afterwards.
        private static readonly Vector2 LobsterBoatOrigin = new Vector2(228f / 456f, 162f / 420f);

        // The art director's README, as data. Cell sizes are verbatim from Art/imported-assets.md.
        // NOTE: CatchSparkle (VFX/CatchSparkle.png) is intentionally absent — it already shipped sliced
        // in an earlier PR; re-slicing here would rewrite its .meta (new sprite GUIDs) and break refs.
        private static readonly SheetSpec[] Sheets =
        {
            // ---- Shoreline finds: 2 cols (variant a/b) × 2 rows (wet/dry) → 4 each, centre pivot ----
            new SheetSpec(Root + "Sprites/Shore/Finds/Bone.png",         2, 2, 22, 12, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/CrabMoult.png",    2, 2, 20, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Driftwood.png",    2, 2, 32, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/GullFeather.png",  2, 2, 22, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Mussel.png",       2, 2, 18, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Oyster.png",       2, 2, 22, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Periwinkle.png",   2, 2, 16, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/SandDollar.png",   2, 2, 16, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Scallop.png",      2, 2, 18, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/SeaGlass.png",     2, 2, 14, 12, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/SoftShellClam.png",2, 2, 18, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Starfish.png",     2, 2, 20, 20, SpriteAlignment.Center, Centre),

            // ---- Seaweed clumps: 3 cols (float-a · float-b · beached) × 1 row → 3 each, centre pivot --
            new SheetSpec(Root + "Sprites/Shore/SeaweedWisp.png",   3, 1, 12, 8,  SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/SeaweedClump.png",  3, 1, 20, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/SeaweedMat.png",    3, 1, 32, 24, SpriteAlignment.Center, Centre),

            // ---- Deck fish tray: 5 cols (fill 0..4) × 3 rows (lobster/crab/mixed) → 15, bottom-centre --
            new SheetSpec(Root + "Sprites/Gear/FishTray.png",       5, 3, 32, 24, SpriteAlignment.BottomCenter, Bottom),

            // ---- Iso dory (player boat, wave-coupled rock) — custom waterline pivot on every slice ----
            //   DoryIso: 8 cols × 1 row → 8 static hull headings (N,NE,E,SE,S,SW,W,NW; index = heading).
            //   DoryIsoRock: 8 cols (rock frame 0..7) × 8 rows (heading) → 64; index = heading×8 + frame
            //     (row-major from top-left, per BuildRects), i.e. heading = index/8, frame = index%8.
            new SheetSpec(Root + "Boats/DoryIso.png",     8, 1, 160, 156, SpriteAlignment.Custom, DoryWaterline),
            new SheetSpec(Root + "Boats/DoryIsoRock.png", 8, 8, 160, 156, SpriteAlignment.Custom, DoryWaterline),

            // ---- Iso dory INDEPENDENT OAR overlays (per-side, layered over the hull) -------------------
            //   DoryOarPort/DoryOarStar: 10 cols (row-stroke frame 0..7, 8 = resting/shipped, 9 = trailing)
            //   × 8 rows (heading N..NW, same CW order as DoryIsoRock) → 80 each; index = heading×10 + col
            //   (row-major from top-left, per BuildRects), i.e. heading = index/10, col = index%10. Same
            //   160×156 cell + waterline pivot as the hull sheets, so the oar overlays register pixel-perfect
            //   on the hull at identical localPosition (art README: all layers pinned to pivot (80,88)).
            new SheetSpec(Root + "Boats/DoryOarPort.png", 10, 8, 160, 156, SpriteAlignment.Custom, DoryWaterline),
            new SheetSpec(Root + "Boats/DoryOarStar.png", 10, 8, 160, 156, SpriteAlignment.Custom, DoryWaterline),

            // ---- Skiff fleet HULLS: two 7 m centre-console skiffs off one keel (console workboat +
            //      sport glass sister). Same envelope/transom/pivot, so the outboard drops onto either.
            //   ConsoleIso/SportSkiffIso: 8 cols × 1 row → 8 static hull headings; index = heading
            //     (0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW — same CW order as the dory).
            //   ConsoleIsoRock/SportSkiffIsoRock: 8 cols (wave frame 0..7) × 8 rows (heading) → 64 each;
            //     index = heading×8 + frame (row-major from top-left, per BuildRects), i.e.
            //     heading = index/8, frame = index%8. README: play ~7 fps to idle on the water.
            new SheetSpec(Root + "Boats/ConsoleIso.png",         8, 1, 244, 216, SpriteAlignment.Custom, SkiffOrigin),
            new SheetSpec(Root + "Boats/SportSkiffIso.png",      8, 1, 244, 216, SpriteAlignment.Custom, SkiffOrigin),
            new SheetSpec(Root + "Boats/ConsoleIsoRock.png",     8, 8, 244, 216, SpriteAlignment.Custom, SkiffOrigin),
            new SheetSpec(Root + "Boats/SportSkiffIsoRock.png",  8, 8, 244, 216, SpriteAlignment.Custom, SkiffOrigin),

            // ---- Skiff fleet OUTBOARD: one remote-steer engine, two paint builds (Work → console,
            //      Sport → sport skiff), each shipping in two layers (Upper = bracket + cowl,
            //      Lower = leg + plate + skeg + prop). Wider 272×216 cell, SAME SkiffOrigin pivot.
            //   9 cols (steer) × 8 rows (heading) → 72 each; index = heading×9 + steerCol (row-major
            //     from top-left, per BuildRects), i.e. heading = index/9, steerCol = index%9.
            //   steerCol: 0 = −30° (full port) … 4 = dead ahead … 8 = +30° (full starboard), 7.5° steps.
            //   Draw order per heading (README, verified against the previews): UPPER always over the
            //     hull; LOWER goes UNDER the hull for the stern-away headings SE/S/SW (3,4,5) and over
            //     it everywhere else. There is NO tiller — the whole engine swivels on its clamp.
            new SheetSpec(Root + "Boats/SkiffMotorUpper-Work.png",  9, 8, 272, 216, SpriteAlignment.Custom, SkiffOrigin),
            new SheetSpec(Root + "Boats/SkiffMotorLower-Work.png",  9, 8, 272, 216, SpriteAlignment.Custom, SkiffOrigin),
            new SheetSpec(Root + "Boats/SkiffMotorUpper-Sport.png", 9, 8, 272, 216, SpriteAlignment.Custom, SkiffOrigin),
            new SheetSpec(Root + "Boats/SkiffMotorLower-Sport.png", 9, 8, 272, 216, SpriteAlignment.Custom, SkiffOrigin),

            // ---- Iso punt HULL: the ~5.2 m tiller punt — flat-floored, beamier and slightly longer than
            //      the dory, wide low transom cut for an outboard. Her OWN cell + pivot (PuntOrigin), NOT
            //      the skiffs'.
            //   PuntIso: 8 cols × 1 row → 8 static hull headings; index = heading
            //     (0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW — same CW order as the dory/skiffs).
            //   PuntIsoRock: 8 cols (wave frame 0..7) × 8 rows (heading) → 64; index = heading×8 + frame
            //     (row-major from top-left, per BuildRects), i.e. heading = index/8, frame = index%8.
            //     README: play ~7 fps to idle; she is beamier than the dory, so she rolls stiffer.
            new SheetSpec(Root + "Boats/PuntIso.png",     8, 1, 184, 168, SpriteAlignment.Custom, PuntOrigin),
            new SheetSpec(Root + "Boats/PuntIsoRock.png", 8, 8, 184, 168, SpriteAlignment.Custom, PuntOrigin),

            // ---- Iso punt OUTBOARD: one engine, two PAINT BUILDS (Basic = weathered grey/black starter,
            //      Upgraded = larger domed cowl, gloss pan, red stripe). Both builds share the same cell,
            //      pivot, steer cols and grip JSON — they are drop-in swaps, picked per boat instance.
            //      Two layers each (Upper = bracket + cowl + tiller, Lower = leg + plate + skeg + prop).
            //      Wider 212×168 cell, SAME PuntOrigin pivot.
            //   9 cols (steer) × 8 rows (heading) → 72 each; index = heading×9 + steerCol (row-major from
            //     top-left, per BuildRects), i.e. heading = index/9, steerCol = index%9.
            //   steerCol: 0 = −32° (full port) … 4 = dead ahead … 8 = +32° (full starboard), 8° steps
            //     (rig: angle(f) = −32 + 64f/8). NOTE this is NOT the skiffs' ±30° / 7.5° steps — the punt
            //     is TILLER-steered (steering swings the tiller across the transom and the operator's aft
            //     hand follows it — see PuntMotorGrips.json), where the skiff engine swivels whole on its
            //     clamp under a remote helm. There is no console, no wheel, and no twin fit.
            //   Draw order per heading (README, verified pixel-exact against the previews): UPPER always
            //     over the hull (the tiller arcs inboard, above the deck); LOWER goes UNDER the hull for
            //     the stern-away headings SE/S/SW (3,4,5) and over it everywhere else.
            new SheetSpec(Root + "Boats/PuntMotorUpper-Basic.png",    9, 8, 212, 168, SpriteAlignment.Custom, PuntOrigin),
            new SheetSpec(Root + "Boats/PuntMotorLower-Basic.png",    9, 8, 212, 168, SpriteAlignment.Custom, PuntOrigin),
            new SheetSpec(Root + "Boats/PuntMotorUpper-Upgraded.png", 9, 8, 212, 168, SpriteAlignment.Custom, PuntOrigin),
            new SheetSpec(Root + "Boats/PuntMotorLower-Upgraded.png", 9, 8, 212, 168, SpriteAlignment.Custom, PuntOrigin),

            // ---- Cape Islander HULL: the ~12.9 m inshore working boat — the biggest hull in the kit by a
            //      wide margin (her cell is nearly 3× the dory's on a side, ~8× the area). Inboard diesel:
            //      she ships NO motor sheet and NO oar sheets, so hull + rock is her whole skin.
            //   CapeIslanderIso: 8 cols × 1 row → 8 static hull headings; index = heading
            //     (0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW — the same CW LABELLING as every iso kit,
            //     and, like every iso kit, baked COUNTER-CLOCKWISE: see BoatVisualLibraryBuilder).
            //   CapeIslanderIsoRock: 8 cols (wave frame 0..7) × 8 ROWS (heading) → 64; index = heading×8 +
            //     frame (row-major from top-left, per BuildRects). NOTE THE AXIS FLIP between the two
            //     sheets — the base sheet's COLUMNS are facings, the rock sheet's ROWS are. That is not a
            //     quirk of this kit: the dory, punt and skiff rock sheets all do it, and row-major indexing
            //     is exactly what turns it into the heading×8 + frame contract above.
            //
            // ⚠ SIZE: the rock sheet is 3648×3360 — BOTH dimensions over Unity's default 2048 cap, so it
            // imports DOWNSCALED to 0.56× unless the cap is lifted. SliceOne lifts it automatically (to
            // NextPowerOfTwo(3648) = 4096, which loses nothing), but the trap is worth naming here because
            // a downscale is SILENT: the sprite COUNT still comes out 64 and only the cell-size/pivot
            // asserts in CapeIslanderSheetSliceTests catch it.
            new SheetSpec(Root + "Boats/CapeIslanderIso.png",     8, 1, 456, 420, SpriteAlignment.Custom, CapeIslanderOrigin),
            new SheetSpec(Root + "Boats/CapeIslanderIsoRock.png", 8, 8, 456, 420, SpriteAlignment.Custom, CapeIslanderOrigin),

            // ---- Lobster boat: the ~12.0 m Tier 3 hull, and the FIRST sheet in this repo baked
            //      IN-ENGINE from the art director's rig rather than hand-exported from a browser
            //      (ADR 0021 / RigBaker). Inboard diesel like the Cape Islander: no motor sheet and
            //      no oars, so hull + rock is her whole skin. Same 456×420 cell as the Cape
            //      Islander, so the importer settings and the 4096 cap lift carry over unchanged.
            //
            // ⚠ SHE IS 32 FACINGS, NOT 8 — the owner's decision, and the reason the baker exists.
            //   Cells are 8 COLS × N ROWS, row-major from top-left (BuildRects order), flat
            //   index = heading×rockFrames + frame:
            //     LobsterBoatIso      32 cells = 8 × 4 rows → 3648×1680, index = heading
            //     LobsterBoatIsoRock0 64 cells = 8 × 8 rows → 3648×3360, headings  0–15 × 4 frames
            //     LobsterBoatIsoRock1 64 cells = 8 × 8 rows → 3648×3360, headings 16–31 × 4 frames
            //
            //   Large hulls get 4 rock frames, small hulls 8 (ADR 0021 §2): a 12 m boat genuinely
            //   rocks less than a 5 m punt, so this is art direction and not only a memory budget.
            //   TWO PAGES because 32×4 = 128 cells on one sheet would stand 6720 px tall — over the
            //   4096 cap, and a downscale is SILENT (the sprite COUNT still matches; only the
            //   cell-size/pivot asserts catch it).
            //
            // ⚠ Her facings are GENUINELY CLOCKWISE, unlike every hand-exported kit above. The
            //   baker measured the rig's counter-clockwise convention from rendered pixels and
            //   applied the correction at bake time, so she wants
            //   FacingsAreCounterClockwise = FALSE. Do not "fix" her to match her neighbours.
            new SheetSpec(Root + "Boats/LobsterBoatIso.png",      8, 4, 456, 420, SpriteAlignment.Custom, LobsterBoatOrigin),
            new SheetSpec(Root + "Boats/LobsterBoatIsoRock0.png", 8, 8, 456, 420, SpriteAlignment.Custom, LobsterBoatOrigin),
            new SheetSpec(Root + "Boats/LobsterBoatIsoRock1.png", 8, 8, 456, 420, SpriteAlignment.Custom, LobsterBoatOrigin),

            // ---- Shoreline ISO tile kit (v7): the PEI red-sandstone coast rebuilt to MATCH THE BOAT BAKE
            //      — square 32×32 cells, 32 px = 1 m, ¾ camera from the SOUTH at 40° (the fleet's turntable
            //      elevation), band-edge-only Bayer dither world-locked to global pixel coords. These
            //      REPLACE the older near-plan Shore*/Grass/Sand/Rock tiles sitting loose in Tilesets/,
            //      which are left untouched so nothing already painted breaks.
            //
            // ⚠ THE KIT BAKES ZERO WATER, ON PURPOSE (ADR 0010/0012/0023). The shader owns the waterline:
            //   it clips at the live depth-0 tide contour, rides foam/swash on it, and pins the displaced
            //   surface to the same line. Every ground material is authored to read right DRY AND
            //   SUBMERGED because the tide sweeps whole flats. Rule-tiles carry terrain-TYPE edges only
            //   (grass↔sand↔rock) plus permanent landforms. Do not author a foam/waterline tile against
            //   these — butt land straight at the shader water and there is nothing to line up.
            //
            // Cell 32×32 everywhere, Center pivot (a tilemap places by cell, so centre is the only pivot
            // that keeps a cliff band stacked on the cell it was painted into). Slice names are GEOMETRIC
            // (`<Stem>_<index>`, row-major from top-left); the row/col SEMANTICS live in
            // ShorelineIsoCatalog, which reads them off the kit's own ShorelineIso.json contract. That
            // split is deliberate: the cliff columns carry compass-ish labels (cornSW, sideW, faceS…) and
            // this repo has shipped mislabelled compass art five times — a slice name states which cell,
            // never which way it looks (same rule as CharacterSheetSlicer).
            new SheetSpec(Root + "Tilesets/ShorelineIso/ShoreIsoGround.png",  3, 6, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/ShorelineIso/ShoreIsoFringe.png", 12, 3, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/ShorelineIso/ShoreIsoCliff.png",  10, 3, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/ShorelineIso/ShoreIsoDune.png",    9, 1, 32, 32, SpriteAlignment.Center, Centre),
            // ShoreIsoSprites.png is NOT here: it is a packed sheet of freestanding rock at seven
            // DIFFERENT sizes with per-item base-centre pivots, so it has no uniform grid to slice.
            // ShorelineIsoSpriteSlicer reads its rects and pivots from the ShoreIsoSprites.json sidecar.

            // ---- Road / path / sidewalk kit: flat 32×32 NEAR-PLAN ground tiles that sit IN the ground
            //      plane exactly like Grass.png/Dirt.png, so they register with the iso houses, the wharf
            //      deck and the shoreline flats. One pre-baked reference atlas per surface at `new` wear
            //      over a grass verge, no markings; wear states, other verges, markings and whole painted
            //      maps all bake from roadPathRig.js.
            //
            // 12 cols × 4 rows = 48 cells holding the canonical 47-tile blob autotiler set (isolated ·
            // caps · straights · bends · tees · crosses), sorted by neighbour mask — so the LAST cell
            // (index 47) is spare padding, not a tile. Index → neighbour mask is RoadKit.BLOB47's order;
            // ShorelineIsoCatalog.RoadBlobCount names the 47 so nothing indexes the 48th by accident.
            new SheetSpec(Root + "Tilesets/Roads/RoadIso_dirt_new_blob47.png",     12, 4, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/Roads/RoadIso_gravel_new_blob47.png",   12, 4, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/Roads/RoadIso_concrete_new_blob47.png", 12, 4, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/Roads/RoadIso_asphalt_new_blob47.png",  12, 4, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/Roads/RoadIso_cobble_new_blob47.png",   12, 4, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/Roads/RoadIso_sand_new_blob47.png",     12, 4, 32, 32, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Tilesets/Roads/RoadIso_brick_new_blob47.png",    12, 4, 32, 32, SpriteAlignment.Center, Centre),
        };

        // ---- entry points -------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Environment + VFX Sheets")]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[SpriteSheetSlicer] Sliced {n} sheet(s) ({skipped} skipped, {failed} failed).");
        }

        /// <summary>
        /// Batch entry point for <c>-executeMethod</c>. Refreshes so any freshly-copied PNGs import first,
        /// slices every manifest sheet, then exits non-zero if any failed so headless/CI bakes fail loudly
        /// instead of committing a half-sliced sheet.
        /// </summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int skipped, out int failed);
                Debug.Log($"[SpriteSheetSlicer] (batch) Sliced {n} sheet(s) " +
                          $"({skipped} skipped, {failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[SpriteSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpriteSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Batch verifier for <c>-executeMethod</c>: loads every manifest sheet with
        /// <see cref="AssetDatabase.LoadAllAssetsAtPath"/> (Multiple-mode sheets return null from
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> — LoadAllAssets is the rule) and asserts the per-sheet
        /// slice count and pivot. Exits non-zero on any mismatch so a bad bake fails loudly.
        /// </summary>
        public static void VerifyAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                bool ok = VerifyAll(logEachPass: true);
                if (!ok)
                {
                    Debug.LogError("[SpriteSheetSlicer] VERIFY FAILED — see mismatches above.");
                    EditorApplication.Exit(1);
                }
                else
                {
                    Debug.Log("[SpriteSheetSlicer] VERIFY PASSED — all environment/VFX sheets sliced correctly.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpriteSheetSlicer] verify threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Assert every manifest sheet imports Multiple-mode with the expected sprite count and pivot.
        /// Returns true only if every sheet passes.
        /// </summary>
        public static bool VerifyAll(bool logEachPass)
        {
            bool allOk = true;
            int checkedCount = 0;
            foreach (var spec in Sheets)
            {
                if (!File.Exists(spec.AssetPath))
                {
                    Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{spec.AssetPath}' missing on disk.");
                    allOk = false;
                    continue;
                }

                checkedCount++;

                var importer = AssetImporter.GetAtPath(spec.AssetPath) as TextureImporter;
                if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{spec.AssetPath}' is not Multiple-mode.");
                    allOk = false;
                    continue;
                }

                var sprites = AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath).OfType<Sprite>().ToArray();
                if (sprites.Length != spec.Count)
                {
                    Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{spec.Stem}' has {sprites.Length} sprites, " +
                                   $"expected {spec.Count} ({spec.Cols}×{spec.Rows}).");
                    allOk = false;
                    continue;
                }

                bool pivotOk = true;
                float expX = spec.Pivot.x * spec.CellW;
                float expY = spec.Pivot.y * spec.CellH;
                foreach (var s in sprites)
                {
                    if (Mathf.Abs(s.pivot.x - expX) > 0.01f || Mathf.Abs(s.pivot.y - expY) > 0.01f)
                    {
                        Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{s.name}' pivot {s.pivot} " +
                                       $"expected ({expX},{expY}).");
                        pivotOk = false;
                    }
                }
                if (!pivotOk) { allOk = false; continue; }

                if (logEachPass)
                    Debug.Log($"[SpriteSheetSlicer] VERIFY OK: {spec.Stem} = {sprites.Length} sprites " +
                              $"({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}, {spec.Alignment}).");
            }

            Debug.Log($"[SpriteSheetSlicer] VERIFY: checked {checkedCount} sheet(s) — " +
                      (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk && checkedCount == Sheets.Length;
        }

        // ---- the work -----------------------------------------------------------------------------

        /// <summary>
        /// Slice every manifest sheet. Returns the number sliced; reports how many were skipped (not on
        /// disk yet) and how many failed (dimension mismatch / no importer).
        /// </summary>
        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0;
            failed = 0;
            int sliced = 0;
            foreach (var spec in Sheets)
            {
                switch (SliceOne(spec))
                {
                    case SliceResult.Sliced:  sliced++;  break;
                    case SliceResult.Skipped: skipped++; break;
                    case SliceResult.Failed:  failed++;  break;
                }
            }
            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>
        /// Slice ONE manifest sheet, by asset path. Returns false if the path is not in
        /// <see cref="Sheets"/> or the slice failed. Exists so a caller (notably the idempotence
        /// fixture) can re-slice a single sheet without paying for the whole manifest's reimports.
        /// </summary>
        public static bool SliceSheet(string assetPath)
        {
            foreach (var spec in Sheets)
            {
                if (!string.Equals(spec.AssetPath, assetPath, StringComparison.Ordinal)) continue;
                bool ok = SliceOne(spec) == SliceResult.Sliced;
                AssetDatabase.SaveAssets();
                return ok;
            }
            Debug.LogError($"[SpriteSheetSlicer] '{assetPath}' is not in the sheet manifest.");
            return false;
        }

        private enum SliceResult { Sliced, Skipped, Failed }

        private static SliceResult SliceOne(SheetSpec spec)
        {
            if (!File.Exists(spec.AssetPath))
            {
                Debug.LogWarning($"[SpriteSheetSlicer] '{spec.AssetPath}' not on disk yet — skipping.");
                return SliceResult.Skipped;
            }

            var importer = AssetImporter.GetAtPath(spec.AssetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[SpriteSheetSlicer] '{spec.AssetPath}' has no TextureImporter — skipping.");
                return SliceResult.Failed;
            }

            // Lift the size cap FIRST, before anything reads the texture. Unity's default maxTextureSize is
            // 2048: a sheet wider/taller than that imports DOWNSCALED, and slicing a downscaled sheet is
            // silent poison — the grid rects are authored in source pixels, then the reimport refits them to
            // the smaller texture and they come back alpha-trimmed with the pivot thrown away (this bit the
            // 2448×1728 skiff-motor sheets: every rect landed ~20×21 with pivot (0,0)). The manifest's
            // SheetW/SheetH are the source of truth for what "native" means, so raise the cap to the next
            // power of two that holds the sheet, then reimport so the on-disk texture is native-res again.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(spec.SheetW, spec.SheetH));
            if (importer.maxTextureSize < needed)
            {
                Debug.Log($"[SpriteSheetSlicer] '{spec.Stem}' is {spec.SheetW}×{spec.SheetH} but the importer " +
                          $"caps at {importer.maxTextureSize} — raising maxTextureSize to {needed} so the sheet " +
                          "imports at native res (a downscaled sheet cannot be grid-sliced).");
                importer.maxTextureSize = needed;
                importer.SaveAndReimport();
            }

            // Guard the sheet size: a re-export that drifted the grid must fail loudly, not slice garbage.
            // Load AFTER the reimport above — a mid-build import invalidates any texture read before it.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.AssetPath);
            if (tex == null)
            {
                Debug.LogError($"[SpriteSheetSlicer] '{spec.AssetPath}' failed to load as Texture2D — skipping.");
                return SliceResult.Failed;
            }
            if (tex.width != spec.SheetW || tex.height != spec.SheetH)
            {
                Debug.LogError(
                    $"[SpriteSheetSlicer] '{spec.AssetPath}' is {tex.width}×{tex.height} but the manifest " +
                    $"expects {spec.SheetW}×{spec.SheetH} ({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}). " +
                    "Not slicing — fix the export or the manifest entry.");
                return SliceResult.Failed;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // ⚠️ Re-use the spriteID any already-sliced name carries. GUID.Generate() on every run made a
            // re-slice rewrite every spriteID in every .meta — 43 files of pure diff noise the last time
            // the owner ran it, 25 of them under Art/Boats, and it recurs on every rig bake now that
            // RigBaker invokes this slicer. (Sprite references resolve by internalID, not spriteID, so
            // this was never a broken-reference bug — only noise that buries the owner's real changes.)
            // Same fix as CharacterSheetSlicer (PR #218): slicing is idempotent now — re-running over
            // unchanged art produces a byte-identical .meta.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(spec, existingIds);
            dp.SetSpriteRects(rects);

            // Keep name→fileID stable across future reimports (mirrors the package's own slicer) so any
            // later reference to a slice survives a re-bake.
            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIdDp != null)
            {
                nameIdDp.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            }

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[SpriteSheetSlicer] Sliced '{spec.Stem}' → {rects.Length} sprites " +
                      $"({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}, {spec.Alignment}).");
            return SliceResult.Sliced;
        }

        /// <summary>
        /// Build the grid of <see cref="SpriteRect"/>s, row-major from the TOP-LEFT cell. Unity's rects are
        /// bottom-origin, so the top row (r=0) maps to the highest Y = (Rows-1)*CellH.
        ///
        /// <para><paramref name="existingIds"/> maps an already-sliced slice name to the spriteID it
        /// already carries; those are re-used so a re-slice of unchanged art is a no-op on the
        /// <c>.meta</c>. Only genuinely new names get a fresh GUID.</para>
        /// </summary>
        private static SpriteRect[] BuildRects(SheetSpec spec,
                                               IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[spec.Count];
            for (int r = 0; r < spec.Rows; r++)
            {
                for (int c = 0; c < spec.Cols; c++)
                {
                    int index = r * spec.Cols + c;
                    float x = c * spec.CellW;
                    float y = (spec.Rows - 1 - r) * spec.CellH; // top row → top of the (bottom-origin) sheet
                    string name = $"{spec.Stem}_{index}";
                    rects[index] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(x, y, spec.CellW, spec.CellH),
                        alignment = spec.Alignment,
                        pivot = spec.Pivot,
                        border = Vector4.zero,
                    };
                }
            }
            return rects;
        }
    }
}
#endif
