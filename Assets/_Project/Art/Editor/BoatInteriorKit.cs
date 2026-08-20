using System;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The boat-interiors kit's identity: where the sheets live, the one cap they are packed against,
    /// and the contract schema the slicer, the verifier and the tests all read back.
    ///
    /// <para>Everything here is DATA or a packing rule. <b>Not one cell dimension, pivot or px/m is
    /// stated in this file</b> — every one of those is read from the rig at bake time (ADR 0021 §4)
    /// and written into the contract. That matters more here than in any other kit, because the
    /// registration property this art exists for is precisely "the interior's cell and pivot ARE the
    /// exterior's": a number restated here could disagree with the rig, and the disagreement would
    /// look like art that is subtly off its boat rather than like a bug.</para>
    /// </summary>
    public static class BoatInteriorKit
    {
        /// <summary>The kit the sheets are rendered from — the interiors drop, landed verbatim.</summary>
        public const string KitFolder = "docs/art/rigs/boat-interiors-kit";

        /// <summary>THE interior renderer. One file, all 24 hulls.</summary>
        public const string InteriorRigFileName = "boatInteriorRig.js";

        /// <summary>The global <see cref="InteriorRigFileName"/> installs.</summary>
        public const string GlobalName = "BoatInterior";

        /// <summary>Beside the fleet's other baked hull art, because that is what these composite under.</summary>
        public const string OutputFolder = "Assets/_Project/Art/Boats/Interiors";

        public const string ContractPath = OutputFolder + "/BoatInteriors.json";

        public const string Schema = "hidden-harbours/boat-interior-sheets@1";

        /// <summary>
        /// The cap this kit packs against, and the cap the slicer imports at. <b>The two must be the
        /// same number</b> — they fail in opposite directions, and only one of the two failures is
        /// loud. Above Unity's hard 4096 a bake refuses; BETWEEN the import cap and 4096 the bake
        /// succeeds and the import silently DOWNSCALES with the sprite count still correct, which
        /// poisons every rect on the sheet without one error line.
        ///
        /// <para><b>Why 4096 and not the fuel kit's 2048.</b> This kit has no choice: the coastal
        /// packet's cell is 2112x1760 and the tanker's 1920x1600 — ONE cell of the packet is already
        /// past 2048 on its long side. Those numbers are the EXTERIOR hulls' cells and cannot be
        /// reduced without breaking registration, so the cap moves instead of the art. The shop kit
        /// (<see cref="ShopKit.ImportSizeCap"/>) lifted it for the same class of reason.</para>
        /// </summary>
        public const int ImportSizeCap = 4096;

        // ---- the render options, written down ---------------------------------------------------

        /// <summary>
        /// The options every cell is baked at, spelled out on every single call.
        ///
        /// <para>The interior rig has no geometry cache, so an omitted option cannot poison a later
        /// render the way the fuel rig's <c>wear</c> does. It would still SILENTLY pick a picture —
        /// <c>resolve()</c> defaults <c>weather</c> to 0.30 and <c>clutter</c> to true — and a default
        /// nobody wrote down is a number nobody can reproduce.</para>
        ///
        /// <para><b>The door is baked SHUT, and that is a scope statement rather than a preference.</b>
        /// The kit's door is an 8-frame cue (<c>doorOpen = k/7</c>, ~70 ms a frame, reversed on exit);
        /// baking it would multiply this kit by eight. Nothing consumes the cue yet — ADR 0038 ships
        /// no runtime — so the sheets bake at the resting fraction and the cue is left to the PR that
        /// plays it.</para>
        /// </summary>
        public const float BakedDoorOpen = 0f;

        public const bool BakedNight = false;
        public const bool BakedLamp = false;
        public const float BakedWeather = 0.30f;
        public const bool BakedClutter = true;

        /// <summary>Facings per level. The kit's own <c>cell.facings</c> is an 8-entry compass on all
        /// 27 sidecars; the bake ASSERTS the sidecar agrees rather than assuming it.</summary>
        public const int Facings = 8;

        // ---- the contract -----------------------------------------------------------------------

        /// <summary>One PNG. A hull whose cells do not fit the cap on one page gets several, exactly
        /// as the lobster boat's rock grid already does (<c>LobsterBoatIsoRock0/1.png</c>).</summary>
        [Serializable]
        public sealed class SheetPage
        {
            /// <summary>File name only — the folder is <see cref="OutputFolder"/>.</summary>
            public string file = "";

            public int columns, rows, sheetW, sheetH;

            /// <summary>Index of this page's first cell in the hull's flat cell order, which is
            /// LEVEL-MAJOR and FACING-MINOR: <c>cell = levelIndex * facings + facing</c>.</summary>
            public int firstCell;

            public int cellCount;

            public string AssetPath => $"{OutputFolder}/{file}";
        }

        /// <summary>One hull's interior art: 24 of these, one per cleared hull, and no others.</summary>
        [Serializable]
        public sealed class SheetEntry
        {
            /// <summary>The interior sidecar's own <c>hull_stem</c>.</summary>
            public string hullStem = "";

            /// <summary>The committed gameplay sidecar's file stem — the hull's name everywhere else in
            /// the project. Resolved from FIELDS by <c>BoatInteriorHullResolver</c>, never parsed out of
            /// a file name.</summary>
            public string hullFileStem = "";

            /// <summary>The matching <c>BoatInteriorDef</c>'s id.</summary>
            public string defId = "";

            /// <summary>The interior rig's own hull key — <c>lobster</c>, <c>tanker</c>, <c>lobvar</c>.</summary>
            public string interiorHullKey = "";

            /// <summary>
            /// The variant triple, as the committed gameplay sidecar DECLARES it. Empty on a rig that
            /// makes one boat.
            ///
            /// <para>⚠️ These are handed to the rig as an OBJECT. Handing the variants rig the joined
            /// STRING instead resolves to <c>v.size === undefined</c> and falls all the way through to
            /// <c>standard/hardtop/northumberland</c>. Measured, not feared: all eighteen variants then
            /// render ONE identical picture, filed under eighteen names, with no error anywhere.</para>
            /// </summary>
            public string variantSize = "", variantStyle = "", variantRegion = "";

            /// <summary>Cell and pivot, READ FROM THE RIG. The pivot is in cell pixels from the
            /// TOP-LEFT, the rigs' own screen origin.</summary>
            public int cellW, cellH, pivotX, pivotY;

            /// <summary>This hull's scale. <b>Per def, never assumed</b> — the tanker is 16 where the
            /// rest of the fleet is 32 (ADR 0038 Proposal 2), and it is this number, not a constant,
            /// that becomes the sheet's pixels-per-unit.</summary>
            public int pixelsPerMetre;

            /// <summary>Level ids in bake order, which is the sheet's row order.</summary>
            public string[] levels = Array.Empty<string>();

            public int facings;

            /// <summary>The measured azimuth convention of the EXTERIOR rig this room was cut from —
            /// the convention the cells were corrected by.</summary>
            public string convention = "";

            /// <summary>The options every cell was rendered at. Recorded so a re-bake is reproducible
            /// and so nobody has to read the rig's <c>resolve()</c> to learn what "default" meant.</summary>
            public float doorOpen;

            public bool night, lamp, clutter;
            public float weather;

            public SheetPage[] pages = Array.Empty<SheetPage>();

            /// <summary>Cells in this hull's whole sheet set.</summary>
            public int CellCount => (levels?.Length ?? 0) * facings;

            /// <summary>
            /// The Unity sprite pivot: normalised, BOTTOM-origin, from the rig's TOP-origin pivot.
            /// <c>(pivotX/W, (H − pivotY)/H)</c> — ADR 0026, and NOT <c>(H − 1 − pivotY)/H</c>.
            /// </summary>
            public Vector2 NormalisedPivot =>
                new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);

            /// <summary>The sprite name for one (level, facing) cell.</summary>
            public string SpriteName(int levelIndex, int facing) =>
                $"{hullFileStem}_{levels[levelIndex]}_d{facing}";

            /// <summary>Flat cell index — LEVEL-MAJOR, FACING-MINOR.</summary>
            public int CellIndex(int levelIndex, int facing) => levelIndex * facings + facing;
        }

        /// <summary>A hull the S0 ledger refused, carried so the contract states what is ABSENT and
        /// why. A kit that silently covers 24 of 27 hulls looks complete.</summary>
        [Serializable]
        public sealed class RefusedEntry
        {
            public string hullStem = "";
            public string verdict = "";
            public string upstreamAsk = "";
        }

        [Serializable]
        public sealed class Contract
        {
            public string schema = Schema;
            public string generated = "";
            public string interiorRig = "";
            public string interiorRigSha256 = "";
            public string engine = "";
            public int importSizeCap = ImportSizeCap;
            public SheetEntry[] sheets = Array.Empty<SheetEntry>();
            public RefusedEntry[] refused = Array.Empty<RefusedEntry>();
        }

        // ---- packing ----------------------------------------------------------------------------

        /// <summary>
        /// How one hull's cells are laid out under <see cref="ImportSizeCap"/>: columns are FACINGS,
        /// rows are cells wrapped from the flat level-major order.
        ///
        /// <para>Pages snap to whole LEVELS whenever more than one level fits, so a hull's levels do
        /// not straddle a page boundary unless the cap genuinely forbids it (the tanker and the packet,
        /// whose cells are so large that only four and two cells respectively fit at all). A level per
        /// page keeps the runtime's job — "switch on exactly one level" — a question about one texture
        /// rather than two.</para>
        /// </summary>
        public static void Pack(int cellW, int cellH, int cellsTotal, int facings,
                                out int columns, out int cellsPerPage)
        {
            columns = Mathf.Clamp(ImportSizeCap / Mathf.Max(1, cellW), 1, Mathf.Max(1, facings));
            int rowsPerPage = Mathf.Max(1, ImportSizeCap / Mathf.Max(1, cellH));
            cellsPerPage = Mathf.Max(1, columns * rowsPerPage);

            // Snap DOWN to whole levels when more than one level fits, so page boundaries land on
            // level boundaries. When a level does not fit at all, leave the raw figure: forcing it
            // would only make the pages smaller without making them align.
            if (facings > 0 && cellsPerPage >= facings)
                cellsPerPage = cellsPerPage / facings * facings;

            cellsPerPage = Mathf.Min(cellsPerPage, Mathf.Max(1, cellsTotal));
        }

        /// <summary>
        /// Where the <paramref name="indexOnPage"/>-th cell of a page sits on it: row-major from the
        /// TOP-LEFT.
        ///
        /// <para>Trivial arithmetic, and it lives here because it is the ONE place the baker and the
        /// slicer must agree. The baker blits a cell into this slot; the slicer names the rect at this
        /// slot. Written twice, the two would agree for every hull whose cells fit one page and part
        /// company on the first that pages — which is precisely the case nothing else can see, because
        /// a mis-slotted sheet still has the right count, the right cell size and the right pivot.</para>
        /// </summary>
        public static void Slot(int indexOnPage, int columns, out int col, out int rowFromTop)
        {
            int c = Mathf.Max(1, columns);
            col = indexOnPage % c;
            rowFromTop = indexOnPage / c;
        }

        /// <summary>The file name of page <paramref name="page"/> of <paramref name="pageCount"/> for
        /// a hull — <c>LobsterBoatIsoInterior.png</c>, or <c>…Interior0.png</c> when it pages.</summary>
        public static string PageFileName(string hullAssetName, int page, int pageCount)
            => pageCount == 1
                   ? $"{hullAssetName}Interior.png"
                   : $"{hullAssetName}Interior{page}.png";
    }
}
