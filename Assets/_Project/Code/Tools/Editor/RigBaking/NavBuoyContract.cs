using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The committed oracle for the nav-buoy kit: every cell, pivot and sheet plan, MEASURED from
    /// the live rig and then asserted against on every bake.
    ///
    /// <para>Same discipline as <see cref="IsoPackContract"/>. The realistic failure this catches is
    /// not a rig regression — it is a baker that computes the cell by a subtly different rule and
    /// produces plausible sheets that disagree with the oracle on every key. A cell that no longer
    /// reproduces REFUSES the bake rather than writing a differently-sized sheet.</para>
    /// </summary>
    [Serializable]
    public sealed class NavBuoyContract
    {
        [Serializable]
        public sealed class Cell
        {
            public string key;             // "CardinalW|s20"
            public string type;
            public string size;

            /// <summary>The buffer the rig's own <c>cell(type,size)</c> hands back, before cropping.</summary>
            public int nativeW, nativeH, nativePivotX, nativePivotY;

            /// <summary>Where the cropped cell starts inside that buffer.</summary>
            public int cropX, cropY;

            /// <summary>The cropped cell and the pivot inside it — what a sprite rect actually uses.</summary>
            public int cellW, cellH, pivotX, pivotY;

            /// <summary>True when the pivot lands inside painted ink on at least one facing. Emitted
            /// because the pivot-seeding in the cell rule is INVISIBLE when it is true.</summary>
            public bool pivotInsideInk;

            /// <summary>How many of the 8 facings are byte-distinct <b>at the baked wear</b>
            /// (<see cref="NavBuoyKit.BakeWear"/>) — see <see cref="NavBuoyContract.FacingCollapseNote"/>
            /// for why the wear state is load-bearing here.</summary>
            public int distinctFacings;

            /// <summary>The committed sheet plan. A sheet packed to a plan that no longer matches its
            /// cell still satisfies the arithmetic — it just lands every slice rect off the art.</summary>
            public int cols, rows, sheetW, sheetH;
        }

        public string kit = "nav-buoy-kit";
        public int version = 1;
        public string generated;

        public int ppu = NavBuoyKit.PxPerM;
        public double elev = NavBuoyKit.ElevationDeg;
        public int facings = NavBuoyKit.Facings;
        public int importSizeCap = NavBuoyKit.ImportSizeCap;

        public string convention;             // "clockwise"
        public double groundStepDeg;          // -45 — THE handedness test
        public double screenMeanStepDeg;      // -46.7525 — recorded, does NOT discriminate

        public string bakeWear = NavBuoyKit.BakeWear;
        public bool bakeLit = NavBuoyKit.BakeLit;

        /// <summary>The pivot is the STILL WATERLINE, not the ground — a floating mark's bob is
        /// measured from it. Verified rather than declared: model <c>z = 0</c> projects to
        /// <c>(cx, cy)</c> identically at all 8 facings, and the drop's own
        /// <c>waterlineY</c> equals its <c>pivot.y</c> on all 70 cells.</summary>
        public string pivotRule =
            "the still waterline: model z = 0, identical in all 8 facings (NOT the ground/keel)";

        public string cellRule =
            "pivot-INCLUSIVE union of the INK bbox across all 8 facings AND all 3 wear states, on " +
            "the rig's fixed cell(type,size) buffer. Wear-invariant so fresh/derelict slice with " +
            "these same rects.";

        public List<Cell> cells = new List<Cell>();

        /// <summary>
        /// ⚠️ <b>Facing collapse is a <c>fresh</c>-only property, and the shipped sheets do not have
        /// it.</b> At <c>fresh</c> several marks are bodies of revolution and render byte-identically
        /// at opposite facings — the plain can/nun/<c>Regulatory</c> collapse to FOUR distinct cells
        /// ({0,4}{1,5}{2,6}{3,7}, 180° symmetry), <c>Spar</c>/<c>Mooring</c>/<c>StbdLit</c> to six,
        /// <c>PortLit</c>/<c>CardinalE</c>/<c>Isolated</c>/<c>Special</c> to seven.
        ///
        /// <para><b>At <c>working</c> and <c>derelict</c>, all fourteen have eight distinct facings.</b>
        /// Rust takes whole facets rather than speckling, so which rusted facets face the camera
        /// depends on the facing and the symmetry breaks. Since the kit bakes <c>working</c>, NOTHING
        /// in the shipped sheets is collapsible — a packer acting on the <c>fresh</c> figure would
        /// flatten art that genuinely differs.</para>
        ///
        /// <para><see cref="Cell.distinctFacings"/> is therefore recorded at the BAKED wear, not at a
        /// nominal one. Two sweeps disagreed on this and the disagreement was the finding.</para>
        /// </summary>
        public const string FacingCollapseNote =
            "distinctFacings is measured at the BAKED wear; collapse exists only at 'fresh', where " +
            "un-rusted bodies of revolution are 180-degree symmetric — not a missing facing";

        // ---- load / save -------------------------------------------------------------------------

        public static NavBuoyContract Load()
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, NavBuoyKit.ContractPath);
            if (!File.Exists(abs))
                throw new FileNotFoundException(
                    $"No nav-buoy contract at {NavBuoyKit.ContractPath}. Run " +
                    "\"Hidden Harbours/Art/Measure Nav Buoy Kit (emit contract)\" first — the bake " +
                    "asserts against this file and will not invent one.", abs);

            var c = JsonUtility.FromJson<NavBuoyContract>(File.ReadAllText(abs));
            if (c == null || c.cells == null || c.cells.Count == 0)
                throw new InvalidOperationException(
                    $"{NavBuoyKit.ContractPath} parsed to nothing. JsonUtility returns a zeroed object " +
                    "rather than throwing on a shape mismatch, so this is asserted.");
            return c;
        }

        public void Save()
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, NavBuoyKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(this, prettyPrint: true) + "\n");
        }

        public Cell Get(string key) =>
            cells.FirstOrDefault(c => string.Equals(c.key, key, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Nav-buoy contract has no cell '{key}'. Known: {cells.Count} cells. Re-emit the " +
                "contract if the rig gained a type or size.");

        // ---- the assertions the bake runs before it writes a pixel --------------------------------

        /// <summary>The catalog DECLARES a handedness and this contract MEASURED one. A disagreement
        /// writes eight good facings in reverse order rather than failing, so it is checked.</summary>
        public void AssertConventionAgrees(AzimuthConvention declared)
        {
            string want = declared == AzimuthConvention.Clockwise ? "clockwise" : "counterClockwise";
            if (!string.Equals(convention, want, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"nav-buoy handedness disagrees: RigCatalog declares {declared}, the contract " +
                    $"measured '{convention}'. Applying the wrong one MIRRORS all eight cells of all " +
                    "seventy — refusing. Re-measure before changing either.");

            if (Math.Sign(groundStepDeg) != (declared == AzimuthConvention.Clockwise ? -1 : 1))
                throw new InvalidOperationException(
                    $"nav-buoy ground step {groundStepDeg}°/dir does not carry the sign {declared} " +
                    "implies. The ground-plane bearing is the handedness test; the screen mean " +
                    $"({screenMeanStepDeg}°) is NOT and must never be substituted for it.");
        }

        public void AssertMatches(string key, int cropX, int cropY, int cellW, int cellH,
                                  int pivotX, int pivotY)
        {
            var c = Get(key);
            if (c.cropX == cropX && c.cropY == cropY && c.cellW == cellW && c.cellH == cellH &&
                c.pivotX == pivotX && c.pivotY == pivotY) return;

            throw new InvalidOperationException(
                $"nav-buoy {key} measured crop ({cropX},{cropY}) cell {cellW}×{cellH} pivot " +
                $"({pivotX},{pivotY}); the contract says crop ({c.cropX},{c.cropY}) cell " +
                $"{c.cellW}×{c.cellH} pivot ({c.pivotX},{c.pivotY}). The cell rule and the render " +
                "have diverged — refusing rather than writing a differently-sized sheet.");
        }

        /// <summary>⚠️ Both sides, and the cap is the IMPORT cap too. A sheet over it comes in
        /// silently downscaled with its sprite count still correct.</summary>
        public void AssertSheetFits(string key, int sheetW, int sheetH)
        {
            if (sheetW <= importSizeCap && sheetH <= importSizeCap) return;
            throw new InvalidOperationException(
                $"nav-buoy {key} packs to {sheetW}×{sheetH}, past the {importSizeCap} px cap. Unity " +
                "would import it DOWNSCALED with the sprite count still correct, which poisons every " +
                "slice rect. Refusing.");
        }

        public void AssertSelfConsistent()
        {
            foreach (var c in cells)
            {
                if (c.cols * c.cellW != c.sheetW || c.rows * c.cellH != c.sheetH)
                    throw new InvalidOperationException(
                        $"nav-buoy contract {c.key}: {c.cols}×{c.rows} of {c.cellW}×{c.cellH} is not " +
                        $"{c.sheetW}×{c.sheetH}.");
                if (c.cols * c.rows != facings)
                    throw new InvalidOperationException(
                        $"nav-buoy contract {c.key}: a {c.cols}×{c.rows} grid holds {c.cols * c.rows} " +
                        $"cells, not {facings} facings.");
                if (c.pivotX < 0 || c.pivotY < 0 || c.pivotX >= c.cellW || c.pivotY >= c.cellH)
                    throw new InvalidOperationException(
                        $"nav-buoy contract {c.key}: pivot ({c.pivotX},{c.pivotY}) is outside its own " +
                        $"{c.cellW}×{c.cellH} cell.");
            }
        }

        /// <summary>The widest sheet in the kit, by the measure a texture cap actually binds on.</summary>
        public (string key, int w, int h, int maxDim, int headroom) WidestSheet()
        {
            Cell worst = null;
            int max = -1;
            foreach (var c in cells)
            {
                int m = Mathf.Max(c.sheetW, c.sheetH);
                if (m <= max) continue;
                max = m; worst = c;
            }
            return (worst!.key, worst.sheetW, worst.sheetH, max, importSizeCap - max);
        }
    }
}
