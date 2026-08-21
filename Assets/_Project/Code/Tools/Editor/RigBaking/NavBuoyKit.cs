using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The navigation-buoy kit: what it bakes, at what cap, and the conventions that were MEASURED
    /// rather than read off its README.
    ///
    /// <para><b>This is the SECOND buoy family and it must not be confused with the first.</b>
    /// <c>buoyIso</c> (deck-loop kit, <see cref="RigCatalog"/>) is the LOBSTER SPAR FLOAT — eight
    /// fleet colour schemes over one 10×32 geometry, baked to <c>DeckLoopSheets/Buoys</c> and
    /// consumed by the trap loop. This kit is the aids to navigation: steel, up to 6.6 m tall, and
    /// it says "the channel is here". Different global (<c>NavBuoy</c> vs <c>BuoyIso</c>),
    /// different sheet folder, different consumer.</para>
    ///
    /// <para><b>The two kits share ONE turntable, and that is measured, not assumed.</b> The drop
    /// shipped its own <c>isoSolid.js</c>, which is character-identical to the deck-loop kit's
    /// already-registered copy (same SHA once line endings are normalised). Rather than commit a
    /// second copy of a registered global's source — the drift <c>docs/art/rigs/README.md</c>'s
    /// no-edit rule exists to prevent — this kit declares <c>deckIsoSolid</c> as its prerequisite.
    /// The substitution is proven at PIXEL level, not by file hash: with the deck-loop turntable
    /// loaded, this rig reproduces all four of the drop's own reference sheets byte-for-byte
    /// (see <c>docs/art/rigs/nav-buoy-kit/IMPORT.md</c> §"The faithfulness control").</para>
    /// </summary>
    public static class NavBuoyKit
    {
        public const string RigKey = "navBuoy";
        public const string SheetFolder = "Assets/_Project/Art/Sprites/NavBuoys/Iso";
        public const string ContractPath = SheetFolder + "/navBuoyKit.contract.json";

        /// <summary>
        /// The 8 ADR-0006 facings, <c>N NE E SE S SW W NW</c>.
        ///
        /// <para>⚠️ Facings come from HERE and from the contract, never from a <c>DIRS</c> field —
        /// this rig declares none (measured: <c>typeof NavBuoy.DIRS === 'undefined'</c>), and #452
        /// is the precedent where <c>Install</c> reported 0 facings for three of four packs.</para>
        /// </summary>
        public const int Facings = 8;

        /// <summary>
        /// ⚠️ <b>ONE constant for two jobs: the grid is packed against this AND the texture is
        /// imported at this.</b> Split them and a sheet over the importer's cap comes in SILENTLY
        /// DOWNSCALED with its sprite count still correct, which poisons every slice rect
        /// (<c>unity-texture-size-cap</c>; the deck-loop kit records the same rule).
        ///
        /// <para>2048 is Unity's DEFAULT importer cap, so nothing here needs the
        /// <c>maxTextureSize</c> lift that <c>IsoPackSheetSlicer</c> applies to the 3784 px iso-pack
        /// sheets. That is a MEASURED claim, and the margin is large: the widest sheet in the whole
        /// 70-cell kit is <c>Mooring|s30</c> at 816×126 — <b>1,232 px under the cap</b>.</para>
        ///
        /// <para>The margin is a consequence of cropping. The rig's NATIVE cells carry a 16° tilt
        /// allowance so a buoy at full roll never leaves its own quad, which makes them 55–79%
        /// larger than the paint: <c>PortLit|s30</c> is 256×452 native and 96×308 cropped. Baked
        /// UNCROPPED the worst sheet is <c>PortLit|s30</c> at exactly 2048 px — landing precisely
        /// ON the cap with zero headroom. We bake the static pose, so the allowance is dead weight;
        /// <see cref="NavBuoySheetBakeTests"/> pins the headroom so a future roll-baked variant
        /// cannot quietly reclaim it.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>
        /// ⚠️ <b>CLOCKWISE, and that is MEASURED.</b> The +X ground-plane bearing steps
        /// <b>−45°/dir</b> (depth un-squashed by <c>/sin 40°</c>) — the same turntable as the
        /// deck-loop kit, and the OPPOSITE handedness to every boat in the catalog. Applying the
        /// fleet's CounterClockwise correction here mirrors all eight cells of all seventy.
        ///
        /// <para>⚠️ The SCREEN mean is −46.7525°/step, numerically identical to the figure the
        /// iso-rig-pack contracts record for their <i>CounterClockwise</i> rigs. The screen mean is
        /// an alternating, foreshortened quantity and <b>is not a handedness test</b>. Only the
        /// un-squashed ground-plane bearing is. Measured by <see cref="NavBuoyRegistrationProbe"/>
        /// from the live rig before any bake; this constant is the prior it is checked against.</para>
        /// </summary>
        public const AzimuthConvention MeasuredConvention = AzimuthConvention.Clockwise;

        /// <summary>Ground-plane bearing step per dir, degrees. The handedness test.</summary>
        public const double GroundStepDeg = -45.0;

        /// <summary>Screen-space mean step per dir. Recorded ONLY to show it does not discriminate.</summary>
        public const double ScreenMeanStepDeg = -46.7525;

        public const double ElevationDeg = 40.0;
        public const int PxPerM = 32;

        /// <summary>
        /// What the first bake ships — the owner's `_confirm` 1 default, taken literally: the full
        /// cardinal set, both lateral pairs (plain and lighted), mooring and isolated danger.
        ///
        /// <para>The four parked types — <c>SafeWater Special Regulatory Spar</c> — are one Build
        /// away: they are in the rig, in the contract, and in <see cref="AllTypes"/>; only this list
        /// gates them. Nothing about the pipeline changes when the owner asks for them.</para>
        /// </summary>
        public static readonly IReadOnlyList<string> BakeTypes = new[]
        {
            "CardinalN", "CardinalS", "CardinalE", "CardinalW",
            "PortCan", "StbdNun", "PortLit", "StbdLit",
            "Mooring", "Isolated",
        };

        /// <summary>All 14 the rig carries. ⚠️ The rig's own header comment says "13 TYPES" and is
        /// WRONG — <c>NavBuoy.TYPE_IDS.length</c> measures 14, and the drop's contract agrees.
        /// Recorded as data, not corrected in his file.</summary>
        public static readonly IReadOnlyList<string> AllTypes = new[]
        {
            "PortCan", "StbdNun", "PortLit", "StbdLit",
            "CardinalN", "CardinalS", "CardinalE", "CardinalW",
            "Isolated", "SafeWater", "Special", "Regulatory", "Mooring", "Spar",
        };

        /// <summary>All five hull diameters, 1.2 m … 3.0 m, matched to 0–10 … 20–100 m of water.
        /// Every size bakes: which mark suits which water is the owner's mapping session, and a
        /// depth ladder he cannot see is one he cannot lay.</summary>
        public static readonly IReadOnlyList<string> Sizes = new[] { "s12", "s18", "s20", "s24", "s30" };

        public static readonly IReadOnlyList<string> WearStates = new[] { "fresh", "working", "derelict" };

        /// <summary>
        /// The wear the sheets ship at. <c>working</c> is the rig's own working default and what
        /// three of the drop's four reference sheets are rendered in.
        ///
        /// <para><c>fresh</c> and <c>derelict</c> are one Build away and — deliberately — will slice
        /// with THESE rects: <see cref="MeasureCell"/> unions over all three wear states, so the
        /// cell is wear-invariant and a later wear bake needs no re-slice.</para>
        /// </summary>
        public const string BakeWear = "working";

        /// <summary>
        /// Daylight. <c>lit</c> brightens the lens material only — measured at 936 of 432,896 px
        /// (0.2%) on <c>CardinalW|s20</c>, and no glow, halo or reflection is baked at all (the
        /// kit README is explicit that those are the renderer's). Flashing at night is its own
        /// feature (`_confirm` 2); a whole second sheet set for a lens highlight is not the way to
        /// park it, so the art is left one Build away instead.
        /// </summary>
        public const bool BakeLit = false;

        /// <summary>Composite key: a cell is identified by type AND size, unlike every sibling
        /// family's single key axis.</summary>
        public static string CellKey(string type, string size) => $"{type}|{size}";

        /// <summary>Sheet stem, e.g. <c>NavBuoy_CardinalW_s20</c>.</summary>
        public static string SheetName(string type, string size) => $"NavBuoy_{type}_{size}";

        // ---- the cell rule ---------------------------------------------------------------------

        /// <summary>
        /// The cell: <b>pivot-inclusive union of the INK bbox across all 8 facings AND ALL THREE
        /// WEAR STATES</b>, on the rig's own fixed <c>cell(type,size)</c> buffer.
        ///
        /// <para><b>Why all three wear states when only one bakes.</b> Rust takes whole facets and
        /// the growth band sits at the waterline, so a derelict hull paints pixels a fresh one does
        /// not. Unioning over wear makes the cell — and therefore the slice rects — invariant, so
        /// the owner can ask for <c>fresh</c> or <c>derelict</c> later and get sheets that drop into
        /// the same rects. Union over the baked state alone would save a few pixels and force a
        /// re-slice of everything the day the second wear ships.</para>
        ///
        /// <para><b>Why seeded at the pivot.</b> Same rule as <c>wharfDecorRig</c>/<c>utilityIsoRig</c>:
        /// a cell must contain its own pivot even if no ink does. Measured: all 70 cells have the
        /// pivot inside the paint anyway (the waterline crosses the hull by definition), so the
        /// seeding is invisible here — which is exactly why it is asserted rather than assumed.</para>
        /// </summary>
        public static void MeasureCell(IRigScriptHost host, string globalName, string type, string size,
                                       int nativeW, int nativeH, int pivotX, int pivotY,
                                       out int cropX, out int cropY, out int cellW, out int cellH,
                                       out int cellPivotX, out int cellPivotY, out bool pivotInsideInk)
        {
            int x0 = pivotX, y0 = pivotY, x1 = pivotX, y1 = pivotY;   // SEEDED AT THE PIVOT
            bool inside = false;

            foreach (string wear in WearStates)
            {
                for (int dir = 0; dir < Facings; dir++)
                {
                    byte[] buf = RenderRaw(host, globalName, type, dir, size, wear, lit: BakeLit);
                    if (!InkBox(buf, nativeW, nativeH, out int bx0, out int by0, out int bx1, out int by1))
                        continue;

                    x0 = Mathf.Min(x0, bx0); y0 = Mathf.Min(y0, by0);
                    x1 = Mathf.Max(x1, bx1); y1 = Mathf.Max(y1, by1);
                    if (pivotX >= bx0 && pivotX <= bx1 && pivotY >= by0 && pivotY <= by1) inside = true;
                }
            }

            cropX = x0; cropY = y0;
            cellW = x1 - x0 + 1; cellH = y1 - y0 + 1;
            cellPivotX = pivotX - x0; cellPivotY = pivotY - y0;
            pivotInsideInk = inside;
        }

        /// <summary>Renders one facing and returns the raw RGBA buffer. Rendered ONCE into a scratch
        /// global — evaluating the expression per field would rasterise the buoy again for each.</summary>
        public static byte[] RenderRaw(IRigScriptHost host, string globalName, string type, int dir,
                                       string size, string wear, bool lit)
        {
            string opts = RigRecipe.Js(OptsOf(size, wear, lit));
            host.Execute($"globalThis.__nbScratch = {globalName}.render('{type}',{dir},{opts});");
            return host.EvaluateBytes("globalThis.__nbScratch");
        }

        /// <summary>The render options as a dict — the source <see cref="RenderRaw"/>'s JS literal is
        /// derived from, so the call and the sheet's recipe cannot say different things.</summary>
        public static RigRecipe.OptionDict OptsOf(string size, string wear, bool lit) =>
            new RigRecipe.OptionDict()
                .Set("size", size)
                .Set("wear", wear)
                .Set("lit", lit);

        /// <summary>
        /// FNV-1a over a rendered buffer. Used ONLY to count how many facings are byte-distinct, so
        /// a non-cryptographic hash is the right tool — and <c>SHA256.HashData</c> is .NET 5+, which
        /// the editor's .NET Framework profile does not have.
        /// </summary>
        public static ulong ContentHash(byte[] bytes)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in bytes)
            {
                h ^= b;
                h *= 1099511628211UL;
            }
            return h;
        }

        public static bool InkBox(byte[] rgba, int w, int h,
                                  out int x0, out int y0, out int x1, out int y1)
        {
            x0 = int.MaxValue; y0 = int.MaxValue; x1 = int.MinValue; y1 = int.MinValue;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (rgba[(row + x) * 4 + 3] == 0) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
            return x1 >= x0;
        }

        // ---- packing ---------------------------------------------------------------------------

        /// <summary>
        /// The grid: the LARGEST DIVISOR of the facing count whose sheet fits the cap on BOTH sides.
        /// Divisors only — which is why no sheet here carries a ragged last row. Same rule as the
        /// deck-loop kit, restated so the baker and the contract emitter cannot drift.
        /// </summary>
        public static bool PlanSheet(int cellW, int cellH, int cells, int cap,
                                     out int cols, out int rows, out int sheetW, out int sheetH)
        {
            for (int c = cells; c >= 1; c--)
            {
                if (cells % c != 0) continue;
                int r = cells / c;
                if (c * cellW > cap || r * cellH > cap) continue;
                cols = c; rows = r; sheetW = c * cellW; sheetH = r * cellH;
                return true;
            }
            cols = rows = sheetW = sheetH = 0;
            return false;
        }
    }
}
