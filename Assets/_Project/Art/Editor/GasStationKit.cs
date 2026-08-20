using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The gas-station kit's identity: which pieces bake, at what facings and in what state, and the
    /// contract schema the slicer and the runtime presenter read back.
    ///
    /// <para>Everything here is DATA. The scope of a bake is <see cref="Builds"/> — one row per baked
    /// sheet — so re-scoping the kit is a table edit, never a code change. Cell geometry, pivots and
    /// the crop rect are read FROM THE RIG at bake time (ADR 0021 §4); this file never restates
    /// them.</para>
    /// </summary>
    public static class GasStationKit
    {
        /// <summary><see cref="HiddenHarbours.Tools.RigBaking.RigCatalog"/> key for the shell.</summary>
        public const string RigKey = "gasStation";

        /// <summary>
        /// The interior's catalog key. Installing THIS one installs the whole kit — it declares the
        /// shell as its prerequisite, which declares the grade table, which declares the turntable —
        /// so the baker loads this key and gets a host with all four globals in the one order that
        /// works.
        /// </summary>
        public const string InteriorRigKey = "stationInterior";

        public const string GlobalName = "StationIso";
        public const string InteriorGlobalName = "StationInterior";

        public const string OutputFolder = "Assets/_Project/Art/Sprites/GasStation/Iso";
        public const string ContractPath = OutputFolder + "/GasStation.json";

        /// <summary>
        /// Unity's DEFAULT import ceiling, and the one this kit packs itself under.
        ///
        /// <para>⚠️ The bake cap and the IMPORT cap must be the SAME number, and they fail in
        /// opposite directions: over Unity's hard limit a bake refuses, but BETWEEN this value and
        /// that limit a sheet bakes fine and imports SILENTLY DOWNSCALED with the sprite count still
        /// correct — which poisons every slice rect without one error.</para>
        ///
        /// <para>This kit has no reason to lift it the way <c>ShopKit</c> does (4096). The C-store is
        /// the worst piece at 556×431 cropped, which laid out as one strip of eight would be 4448 px
        /// wide — so the sheets are GRID-packed (see <see cref="Columns"/>) and the widest that
        /// results is 1792. Packing is the cheaper fix than a lifted ceiling, and it keeps every
        /// sheet inside the default the rest of the project assumes.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Pixels per metre. The fleet's scale, and the rig's own <c>S</c>.</summary>
        public const int PixelsPerUnit = 32;

        // ---- the facing axis -------------------------------------------------------------------

        /// <summary>Facings for a piece with eight distinct appearances. The ADR-0006 recipe.</summary>
        public const int Facings = 8;

        /// <summary>
        /// Facings for a piece the rig draws with 180° rotational symmetry — see
        /// <see cref="Fold180Types"/>.
        /// </summary>
        public const int FoldedFacings = 4;

        /// <summary>
        /// Types whose facing <c>d</c> and <c>d+4</c> are the SAME PICTURE, so only four need baking
        /// and the presenter reads cell <c>dir % 4</c>.
        ///
        /// <para>⚠️ <b>This is a symmetry FOLD, not a decimation, and the difference is not
        /// cosmetic.</b> Baking "4 facings" the ordinary way means dirs 0·2·4·6 and a heading snapped
        /// to four quarters — which for a 180°-symmetric piece renders only TWO distinct pictures and
        /// throws away half the angular resolution for nothing. The fold bakes dirs 0·1·2·3 and keeps
        /// the full eight-way resolution at half the pixels. <c>RigBaker.DirForCell(cell, 4, …)</c>
        /// implements the decimation, so the baker deliberately does NOT call it for these types.</para>
        ///
        /// <para>MEASURED, not assumed (2026-08-20, standalone V8 harness): canopy <c>sB2/sB3/sB4</c>
        /// are byte-identical between <c>d</c> and <c>d+4</c> at all four pairs — zero pixels of
        /// 107,802 and 221,695. Islands <c>s1…s4</c> differ by <b>1 to 4 pixels</b> of 24,288–168,000
        /// (≤0.008%), which is rasteriser tie-breaking on a symmetric kerb rather than geometry: a
        /// plain hash comparison calls them 5- and 8-distinct and is exactly what would talk you out
        /// of a real saving. <c>GasStationKitTests</c> re-measures the fold at
        /// <see cref="Fold180TolerancePixels"/> and goes red if a drop ever makes one of these pieces
        /// genuinely asymmetric.</para>
        /// </summary>
        public static readonly IReadOnlyList<string> Fold180Types = new[] { "island", "canopy" };

        /// <summary>
        /// The measured worst case of the fold, and nothing more. Four pixels is what the islands
        /// cost; the canopies cost zero. Raising this to make a new piece fit would be hiding a real
        /// difference, so it is a tripwire and not a tuning knob.
        /// </summary>
        public const int Fold180TolerancePixels = 4;

        public static bool IsFolded(string type) => Fold180Types.Contains(type);

        public static int FacingsFor(string type) => IsFolded(type) ? FoldedFacings : Facings;

        // ---- the state the kit bakes at --------------------------------------------------------

        /// <summary>
        /// The grade list every sheet bakes with, passed EXPLICITLY on every render.
        ///
        /// <para>This is the rig's ONE RULE made concrete: a station is a list of grade positions, and
        /// the valance stripes, boot cups, sign rows and apron fill caps are all generated from it. So
        /// this is not a colour choice — it decides how many hoses a dispenser draws, how many rows
        /// the price sign carries and how many caps sit in the apron. Three is the rig's own default
        /// and the set the committed sidecar was generated with (verified: the sidecar reproduces
        /// byte-for-byte from <c>gameplayAll({grades:['gas','diesel','mixed']})</c>).</para>
        ///
        /// <para>⚠️ OWNER'S CALL, costed in the PR: a station selling four or five grades is a
        /// different sheet, not a tint of this one.</para>
        /// </summary>
        public static readonly IReadOnlyList<string> BakedGrades = new[] { "gas", "diesel", "mixed" };

        /// <summary>
        /// The wear state every sheet bakes at, passed explicitly on every render.
        ///
        /// <para>Unlike the fuel kit — whose omitted <c>wear</c> renders a phantom fourth state that
        /// COLLIDES in the geometry cache — this rig funnels every option through <c>resolve()</c>
        /// before both <c>wearPass</c> and the cache key, so an omitted option and an explicitly
        /// defaulted one are the same picture under the same key. MEASURED across all 21 pieces in
        /// both call orders: no divergence, no order dependence. It is still spelled out on every
        /// call, because that costs nothing and the day a drop changes that funnel it is the
        /// difference between a red test and a poisoned sheet.</para>
        /// </summary>
        public const string BakedWear = "working";

        /// <summary>
        /// Lamps off. <c>lit</c> is a MATERIAL axis rather than a geometry one, so a night set is a
        /// second sheet over the same rects rather than a re-measure — but it is a full second copy
        /// of the kit's pixels and nothing consumes a lit station yet. Costed in the PR, not baked.
        /// </summary>
        public const bool BakedLit = false;

        /// <summary>Storefront sliding entry, 0 = shut. Doors default SHUT (the sidecar's own rule).</summary>
        public const float BakedOpen = 0f;

        /// <summary>Canopy fascia rather than the lean-to tin, and one island row of span.</summary>
        public const string BakedStyle = "fascia";

        public const int BakedSpan = 1;

        /// <summary>Every nozzle holstered — <c>out: -1</c> means no hose is off the hook.</summary>
        public const int BakedOut = -1;

        public const bool BakedBollards = true;

        public const bool BakedBin = false;

        // ---- the build table -------------------------------------------------------------------

        /// <summary>
        /// Every piece the rig draws, in <c>TYPE_ORDER</c>, with its sizes in <c>sizesOf()</c> order.
        /// Cross-checked against the live rig at bake time, because a piece added upstream and missed
        /// here bakes a short kit in silence.
        ///
        /// <para>⚠️ <c>sLock</c>, NOT <c>sCardlock</c>. The rig's own header comment names the seventh
        /// dispenser <c>sCardlock</c>; the table key is <c>sLock</c>, and
        /// <c>resolveSize('dispenser','sCardlock')</c> returns <c>'sKerb'</c> — so following the rig's
        /// own documentation bakes a 25×52 KERB POST under the cardlock's name and nothing complains.
        /// See <c>StationRegistrationProbe.MeasureDocumentedNameDefects</c>.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Sizes =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["dispenser"] = new[] { "sKerb", "sVintage", "sDock", "sTwin", "sMpd", "sTruck", "sLock" },
                ["island"] = new[] { "s1", "s2", "s3", "s4" },
                ["canopy"] = new[] { "sB2", "sB3", "sB4" },
                ["sign"] = new[] { "sPylon", "sTotem", "sAframe" },
                ["store"] = new[] { "sKiosk", "sStore" },
                ["fillport"] = new[] { "sCluster", "sVent" },
            };

        /// <summary>
        /// The kit's grade vocabulary — <c>FuelIso</c>'s four by reference plus this rig's stove oil.
        /// Cross-checked against the live rig's <c>GRADE_ORDER</c> at bake time.
        /// </summary>
        public static readonly IReadOnlyList<string> RigGradeKeys =
            new[] { "gas", "diesel", "mixed", "oil", "kero" };

        /// <summary>
        /// The published grade id for a rig grade key — the fixed contract the economy lane builds
        /// against: <c>gas | diesel | mixed | oil | stove_oil</c>. Only <c>kero</c> is renamed; the
        /// rig's internal key is three letters, the published id is the harbour's word for it.
        /// </summary>
        public static string GradeId(string rigKey) => rigKey == "kero" ? "stove_oil" : rigKey;

        /// <summary>The storefront sizes the interior rig paints a sales floor into.</summary>
        public static readonly IReadOnlyList<string> InteriorSizes = new[] { "sKiosk", "sStore" };

        /// <summary>One baked sheet: one piece at one size, all its facings.</summary>
        public readonly struct Build
        {
            /// <summary>Rig type (<c>dispenser</c>…), or <c>interior</c> for the sales floor.</summary>
            public readonly string Type;

            public readonly string Size;

            /// <summary>True when the sheet comes from <c>StationInterior</c> rather than <c>StationIso</c>.</summary>
            public readonly bool Interior;

            public Build(string type, string size, bool interior = false)
            {
                Type = type;
                Size = size;
                Interior = interior;
            }

            /// <summary>Sheet stem, e.g. <c>dispenser_sTwin</c>. Also the contract key.</summary>
            public string Key => $"{Type}_{Size}";

            public override string ToString() => Key;
        }

        /// <summary>
        /// The whole kit: 21 exterior pieces plus the 2 sales floors = 23 sheets.
        ///
        /// <para>The interior sheets are separate BUILDS but not separate CELLS — the interior rig
        /// paints into <c>StationIso.cell('store', size)</c>, the exterior's own quad and pivot
        /// (verified identical at both sizes, quad AND pivot). They crop to their own ink, which is
        /// 34–47% smaller, and still register to the pixel because both carry the same world
        /// pivot.</para>
        /// </summary>
        public static IReadOnlyList<Build> Builds =>
            Sizes.SelectMany(kv => kv.Value.Select(size => new Build(kv.Key, size)))
                 .Concat(InteriorSizes.Select(size => new Build("interior", size, interior: true)))
                 .ToArray();

        // ---- packing ---------------------------------------------------------------------------

        /// <summary>
        /// How many columns a sheet of <paramref name="facings"/> cells of
        /// <paramref name="cellW"/>×<paramref name="cellH"/> packs into, staying inside
        /// <see cref="ImportSizeCap"/> on BOTH axes.
        ///
        /// <para>Prefers the widest layout that fits, so a small piece stays the familiar single
        /// strip and only the big ones fold into rows. Returns 0 when nothing fits, which the baker
        /// turns into a refusal rather than a silent downscale.</para>
        ///
        /// <para>⚠️ <b>An EXACT divisor of the facing count comes first, even when a wider layout
        /// would fit.</b> Widest-that-fits alone put the C-store in a 3×3 grid — nine slots for eight
        /// facings — and that ninth slot is not free: it ships as a fully transparent 556×431 cell,
        /// 239,636 dead pixels, 0.9 MB of RGBA32 that no sprite rect ever names. Costs nothing to
        /// avoid, and a blank cell on a sheet is the kind of thing that later reads as a missing
        /// facing. The uneven fallback is kept for the case where no divisor fits at all.</para>
        /// </summary>
        public static int Columns(int facings, int cellW, int cellH)
        {
            int uneven = 0;

            for (int cols = facings; cols >= 1; cols--)
            {
                int rows = Mathf.CeilToInt(facings / (float)cols);
                if (cols * cellW > ImportSizeCap || rows * cellH > ImportSizeCap) continue;

                if (facings % cols == 0) return cols;
                if (uneven == 0) uneven = cols;
            }

            return uneven;
        }

        // ---- the contract ----------------------------------------------------------------------

        /// <summary>
        /// One sheet, as written to <see cref="ContractPath"/>. Plain fields and
        /// <see cref="JsonUtility"/> both ways, so the serialiser and the parser are one code path.
        /// </summary>
        [Serializable]
        public class SheetEntry
        {
            public string key;              // dispenser_sTwin
            public string type, size;
            public string label;            // the rig's own, e.g. "TWIN SLAB"
            public bool interior;
            public string wear;
            public bool lit;
            public string[] grades;
            public int facings;             // cells baked
            public bool fold180;            // cell index is dir % facings, not a snapped quarter
            public int cellW, cellH;        // the CROPPED cell
            public int pivotX, pivotY;      // top-left origin, inside the cropped cell
            public int columns, rows;
            public int sheetW, sheetH;
            public int nativeW, nativeH;    // the rig's own uncropped cell
            public bool pivotInsideInk;

            /// <summary>Unity's normalised, BOTTOM-origin sprite pivot. The y term is
            /// <c>(H − pivotY)/H</c> per ADR 0026 — a piece's pivot is a projected POINT (the ground
            /// centre of its own footprint), not a chosen row.</summary>
            public Vector2 NormalisedPivot =>
                new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);

            /// <summary>Sprite name for one baked cell.</summary>
            public string SpriteName(int cell) => $"{key}_d{cell}";

            /// <summary>
            /// Which baked cell shows facing <paramref name="dir"/> (0…7). For a folded piece this is
            /// <c>dir % 4</c> — the fold, not a snap.
            /// </summary>
            public int CellForDir(int dir) => ((dir % facings) + facings) % facings;

            public int ColumnOf(int cell) => cell % columns;

            public int RowOf(int cell) => cell / columns;
        }

        [Serializable]
        public class Contract
        {
            public string generated;
            public string rigKey, interiorRigKey, globalName, interiorGlobalName, convention;
            public int pixelsPerUnit;
            public int importSizeCap;
            public string bakedWear;
            public bool bakedLit;
            public string[] bakedGrades;
            public int facings, foldedFacings;
            public string[] gradeIds;
            public float slotPitch;
            public SheetEntry[] sheets;

            public SheetEntry Find(string key) =>
                sheets?.FirstOrDefault(s => string.Equals(s.key, key, StringComparison.Ordinal));
        }
    }
}
