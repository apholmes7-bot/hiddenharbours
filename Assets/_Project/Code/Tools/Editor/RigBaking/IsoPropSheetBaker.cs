using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One piece of a FIXED-SHEET ISO prop family, through the 8-facing turntable.
    ///
    /// <para>Both families this baker serves — <c>wharfDecor</c> (61 pieces of deck gear) and
    /// <c>utilityIso</c> (42 village services) — render into a fixed native buffer at a fixed pivot and
    /// crop to a pivot-inclusive ink union. They share a render signature and a sheet shape, so they
    /// share ONE parameterised baker rather than two that drift apart.</para>
    /// </summary>
    public readonly struct IsoPropBakeRequest
    {
        /// <summary><see cref="RigCatalog"/> key — <c>"wharfDecor"</c> or <c>"utilityIso"</c>.</summary>
        public readonly string RigKey;

        /// <summary>The piece, as the rig's own <c>list()</c> spells it (e.g. <c>"trapStack"</c>).</summary>
        public readonly string PieceKey;

        /// <summary>Project-relative output folder.</summary>
        public readonly string OutputFolder;

        /// <summary>Sheet stem. Defaults to <see cref="PieceKey"/>.</summary>
        public readonly string BaseName;

        public IsoPropBakeRequest(string rigKey, string pieceKey, string outputFolder, string baseName = null)
        {
            RigKey = rigKey;
            PieceKey = pieceKey;
            OutputFolder = outputFolder;
            BaseName = string.IsNullOrEmpty(baseName) ? pieceKey : baseName;
        }
    }

    public sealed class IsoPropBakeResult
    {
        public string RigKey, PieceKey, AssetPath, EngineName;
        public int NativeWidth, NativeHeight;
        public int CropX, CropY, CellWidth, CellHeight, PivotX, PivotY;
        public int Columns, Rows, SheetWidth, SheetHeight, Facings, PngBytes;
        public bool PivotInsideInk;
        public double RenderMilliseconds, TotalMilliseconds;

        /// <summary>Sprite pivot normalised from the BOTTOM-left, which is what Unity's importer wants.
        /// Top-left <c>PivotY</c> converts as <c>(CellHeight − PivotY) / CellHeight</c>; getting it
        /// upside-down is easy and silent.</summary>
        public Vector2 NormalisedPivot =>
            new Vector2(PivotX / (float)CellWidth, (CellHeight - PivotY) / (float)CellHeight);

        public override string ToString() =>
            $"{RigKey}.{PieceKey}: {CellWidth}×{CellHeight} pivot {PivotX},{PivotY} → " +
            $"{Columns}×{Rows} sheet {SheetWidth}×{SheetHeight} ({PngBytes:N0} B) in " +
            $"{TotalMilliseconds:F0} ms";
    }

    /// <summary>
    /// Bakes the two fixed-sheet ISO prop families against their committed contracts.
    ///
    /// <para><b>The contract is the oracle.</b> Every cell this baker produces is checked against
    /// <see cref="IsoPackContract"/> before a pixel is written, and a disagreement REFUSES the bake
    /// rather than shipping a differently-sized sheet. The realistic failure is not a rig regression —
    /// it is a baker measuring the wrong quantity, which produces a plausible cell that is wrong on
    /// every key at once.</para>
    ///
    /// <para><b>No silent skips.</b> A piece that renders empty, comes back the wrong size, or is
    /// missing from the contract throws. A bake that quietly drops a sheet thins the shipping
    /// inventory while still reporting success, and the count is what the pixel audit checks against.</para>
    /// </summary>
    public static class IsoPropSheetBaker
    {
        /// <summary>
        /// Families this baker serves. <c>wharfIso</c> and <c>shoreFinds</c> are deliberately NOT here:
        /// the wharf kit sizes its own buffer per bake and reports a fractional pivot with it, and the
        /// finds are not directional at all and take their cell from an analytic <c>cellOf()</c>.
        /// </summary>
        public static readonly IReadOnlyList<string> Families = new[] { "wharfDecor", "utilityIso" };

        public static IsoPropBakeResult Bake(IsoPropBakeRequest req)
        {
            var total = Stopwatch.StartNew();
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var result = Bake(req, host);
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Bake through a caller-owned host, so a batch pays the rig-install cost once.</summary>
        public static IsoPropBakeResult Bake(IsoPropBakeRequest req, IRigScriptHost host)
        {
            var contract = IsoPackContract.Load(req.RigKey);
            var entry = RigCatalog.Get(req.RigKey);
            var geo = InstallAndGuard(host, entry, contract, req.RigKey);

            var cells = RenderFacings(host, entry.GlobalName, req.PieceKey, contract, geo,
                                      entry.DeclaredConvention, out double renderMs);

            // ---- crop: pivot-INCLUSIVE union of the ink, seeded at the pivot ------------------------
            // Seeding at the pivot is what makes fireCabinet work. It is wall-hung, so nothing is drawn
            // at deck level and its ink stops 6 px ABOVE its own pivot; a crop merely "tight to ink"
            // would put the piece's ground contact outside its own cell. 1 of 61 pieces needs this and
            // 60 do not, which is exactly why it must be the rule rather than a special case.
            int px = Mathf.RoundToInt((float)geo.PivotX), py = Mathf.RoundToInt((float)geo.PivotY);
            int left = 0, right = 0, top = 0, bottom = 0;
            bool anyInk = false, pivotInsideInk = false;

            foreach (byte[] rgba in cells)
            {
                BuildingRigAzimuthProbe.AlphaBounds(rgba, geo.Width, geo.Height,
                                                    out int x0, out int y0, out int x1, out int y1);
                if (x1 < x0) continue;                       // this facing drew nothing
                anyInk = true;
                left   = Mathf.Min(left,   x0 - px);
                right  = Mathf.Max(right,  x1 - px);
                top    = Mathf.Min(top,    y0 - py);
                bottom = Mathf.Max(bottom, y1 - py);
                if (px >= x0 && px <= x1 && py >= y0 && py <= y1) pivotInsideInk = true;
            }

            if (!anyInk)
                throw new InvalidOperationException(
                    $"Every facing of {req.RigKey}.{req.PieceKey} rendered fully transparent. The key " +
                    "resolved but drew no pixels — refusing rather than writing an empty sheet.");

            int cw = right - left + 1, ch = bottom - top + 1;
            int pivotX = -left, pivotY = -top;

            // ---- THE ORACLE ------------------------------------------------------------------------
            contract.AssertMatchesContract(req.PieceKey, cw, ch, pivotX, pivotY);
            contract.AssertPivotInsideInk(req.PieceKey, pivotInsideInk);

            // ---- pack ------------------------------------------------------------------------------
            // The grid is chosen against the cap this family IMPORTS at, not Unity's hard 4096: between
            // the two, a sheet bakes fine and then imports SILENTLY DOWNSCALED with the sprite count
            // still correct, so every slice rect lands wrong and nothing reports an error.
            BuildingRigBaker.ChooseGrid(cw, ch, contract.Facings, out int cols, out int rows,
                                        contract.ImportSizeCap);

            int pw = cols * cw, ph = rows * ch;
            contract.AssertSheetFits(req.PieceKey, cols, rows, pw, ph);

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < contract.Facings; cell++)
                BuildingRigBaker.BlitCropped(cells[cell], geo.Width, geo.Height,
                                             px + left, py + top, cw, ch,
                                             pixels, pw, ph,
                                             col: cell % cols, rowFromTop: cell / cols);

            var result = new IsoPropBakeResult
            {
                RigKey = req.RigKey,
                PieceKey = req.PieceKey,
                EngineName = host.EngineName,
                NativeWidth = geo.Width,
                NativeHeight = geo.Height,
                CropX = px + left,
                CropY = py + top,
                CellWidth = cw,
                CellHeight = ch,
                PivotX = pivotX,
                PivotY = pivotY,
                PivotInsideInk = pivotInsideInk,
                Columns = cols,
                Rows = rows,
                SheetWidth = pw,
                SheetHeight = ph,
                Facings = contract.Facings,
                RenderMilliseconds = renderMs,
                AssetPath = $"{req.OutputFolder}/{req.BaseName}.png",
            };

            WritePng(pixels, pw, ph, result);
            return result;
        }

        // ---- install + the standing guards -------------------------------------------------------

        static RigGeometry InstallAndGuard(IRigScriptHost host, in RigEntry entry,
                                           IsoPackContract contract, string rigKey)
        {
            if (contract.NeedsInstallModule)
                throw new InvalidOperationException(
                    $"{rigKey} does not load with Install — it belongs to a different baker. " +
                    $"{nameof(IsoPropSheetBaker)} serves {string.Join(" and ", Families)} only.");

            contract.AssertSelfConsistent();
            AssertKeylineGated(host, entry, contract, rigKey);

            var geo = RigCatalog.Install(host, entry);

            if (geo.Width != contract.Proj.nativeSheetW || geo.Height != contract.Proj.nativeSheetH)
                throw new InvalidOperationException(
                    $"{rigKey} reports a {geo.Width}×{geo.Height} native sheet but its contract " +
                    $"measured {contract.Proj.nativeSheetW}×{contract.Proj.nativeSheetH}. The rig " +
                    "changed shape; regenerate the contract before baking against it.");

            // ⚠️ nativeDirs is NOT the facing count here, and reads 0 rather than throwing. Neither of
            // these two rigs declares a DIRS global at all, so Install's `typeof DIRS === 'number'`
            // test is false and it reports 0 — silently, because 0 legitimately means "the rig does not
            // say". Facings come from the contract; this only records that the rig stayed silent.
            if (geo.NativeDirs != 0 && geo.NativeDirs != contract.Facings)
                throw new InvalidOperationException(
                    $"{rigKey} now reports {geo.NativeDirs} native facings against the contract's " +
                    $"{contract.Facings}. One of the two is stale.");

            return geo;
        }

        /// <summary>
        /// Refuse to bake art that still carries its 1 px keyline ring.
        ///
        /// <para>The owner ruled (2026-08-06) that all four pack rigs gain a
        /// <c>KEYLINE_DEFAULT = false</c> gate BEFORE anything bakes, because baking pre-gate art bakes
        /// the ring in and forces a re-bake — the mistake this pack was explicitly steered away from.
        /// That ruling is a process gate everywhere else; here it is mechanical, so a bake cannot
        /// quietly land ringed sheets if the gate has not shipped.</para>
        ///
        /// <para><b>The ring is a PASS, not a colour.</b> Whoever implements the gate upstream must skip
        /// the pass and never filter by keyline colour: the art legitimately carries interior pixels at
        /// exactly that value with no transparent neighbour — <c>radioMast</c> alone has 551, in its
        /// lattice — and a colour match would punch holes straight through them.</para>
        /// </summary>
        static void AssertKeylineGated(IRigScriptHost host, in RigEntry entry,
                                       IsoPackContract contract, string rigKey)
        {
            string g = entry.GlobalName;
            bool hasGate = host.EvaluateBool(
                $"typeof {g} !== 'undefined' && typeof {g}.KEYLINE_DEFAULT !== 'undefined'");

            if (hasGate) return;

            throw new InvalidOperationException(
                $"REFUSING TO BAKE {rigKey}: its rig source exposes no KEYLINE_DEFAULT gate, so every " +
                "sheet would bake with the 1 px ring in it.\n\n" +
                "The owner ruled on 2026-08-06 that the art director adds KEYLINE_DEFAULT = false to " +
                "all four ISO-pack rigs BEFORE anything bakes, precisely so this pack does not need the " +
                "re-bake that #444 did. docs/art/rigs/** is the art director's lane — do NOT edit the " +
                "rig here to get past this.\n\n" +
                "When the gated rigs land, regenerate the contracts for wharfDecor and utilityIso in " +
                "the same commit: their cells shrink 2×2 px (fireCabinet 2×1). wharfIso's 17 cells and " +
                "shoreFinds' 36 do NOT move — wharfIso measures a buffer the geometry sizes before the " +
                "ring pass runs, and shoreFinds' cellOf() is analytic.\n\n" +
                $"(This contract was measured with the ring {(contract.KeylineDefault ? "ON" : "OFF")}.)");
        }

        // ---- render --------------------------------------------------------------------------------

        static byte[][] RenderFacings(IRigScriptHost host, string g, string pieceKey,
                                      IsoPackContract contract, in RigGeometry geo,
                                      AzimuthConvention convention, out double renderMs)
        {
            AssertPieceExists(host, g, pieceKey);

            int expected = geo.Width * geo.Height * 4;
            var cells = new byte[contract.Facings][];
            var clock = new Stopwatch();

            for (int cell = 0; cell < contract.Facings; cell++)
            {
                // DirForCell carries the counter-clockwise correction, so what lands on disk is
                // genuinely clockwise. All three directional pack rigs measured CCW, matching
                // houseIsoRig / wharfBuildingRig / interiorIsoRig.
                double dir = RigBaker.DirForCell(cell, contract.Facings, convention);
                string d = dir.ToString("R", CultureInfo.InvariantCulture);

                clock.Start();
                cells[cell] = host.EvaluateBytes($"{g}.render({Quote(pieceKey)},{d},{{}})");
                clock.Stop();

                if (cells[cell] == null || cells[cell].Length != expected)
                    throw new InvalidOperationException(
                        $"{g}.render({pieceKey}, dir {d}) came back " +
                        $"{(cells[cell]?.Length.ToString() ?? "null")} bytes, expected {expected} for " +
                        $"a {geo.Width}×{geo.Height} RGBA buffer. These rigs return a BARE RGBA array, " +
                        "not a {data,w,h} object — a wrapper here means the rig's return shape changed.");
            }

            renderMs = clock.Elapsed.TotalMilliseconds;
            return cells;
        }

        static void AssertPieceExists(IRigScriptHost host, string g, string pieceKey)
        {
            if (host.EvaluateBool($"{g}.list().indexOf({Quote(pieceKey)}) >= 0")) return;

            throw new InvalidOperationException(
                $"'{pieceKey}' is not in {g}.list(). Baking a key the rig does not know draws nothing " +
                "and would otherwise land as an empty sheet with a correct-looking sprite count.");
        }

        static string Quote(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        // ---- write ---------------------------------------------------------------------------------

        static void WritePng(Color32[] pixels, int w, int h, IsoPropBakeResult result)
        {
            string outAbs = Path.Combine(RigCatalog.RepoRoot, Path.GetDirectoryName(result.AssetPath) ?? "");
            Directory.CreateDirectory(outAbs);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, result.AssetPath), png);
                result.PngBytes = png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
