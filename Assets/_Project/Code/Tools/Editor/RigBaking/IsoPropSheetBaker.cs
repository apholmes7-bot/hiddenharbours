using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One piece of a FIXED-SHEET ISO prop family, through the 8-facing turntable.
    ///
    /// <para>Every family this baker serves — <c>wharfDecor</c> (61 pieces of wharf dressing),
    /// <c>utilityIso</c> (42 village services) and the deck-loop kit's five — renders into a fixed
    /// native buffer at a fixed pivot and crops to a pivot-inclusive ink union. They share a buffer
    /// shape and a cell rule, so they share ONE parameterised baker rather than several that drift
    /// apart. What they do NOT share is the shape of the <c>render()</c> CALL, which is what
    /// <see cref="IsoPropSheetBaker.RenderShape"/> carries.</para>
    /// </summary>
    public readonly struct IsoPropBakeRequest
    {
        /// <summary><see cref="RigCatalog"/> key — one of <see cref="IsoPropSheetBaker.Families"/>.</summary>
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

        /// <summary>Where the sheet's <c>&lt;stem&gt;.recipe.json</c> went — the rig call that
        /// produced it (see <see cref="RigRecipe"/>).</summary>
        public string RecipePath;
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
        /// How a family's <c>render()</c> is CALLED, and where its key vocabulary lives.
        ///
        /// <para><b>This is the thing that is NOT common across the families this baker serves, and it
        /// is why "measure which baker fits" is a per-rig question rather than a per-kit one.</b> All
        /// seven agree on the buffer, the pivot and the cell rule; they disagree on the call:</para>
        /// <list type="bullet">
        ///   <item><c>render(key, dir, opts)</c> — wharfDecor, utilityIso, deckGear, trap, buoyIso.</item>
        ///   <item><c>render(dir, opts)</c> — <b>fishTray2</b>. The family draws exactly one thing, so
        ///         the rig names no key and takes none. Passing its contract key <c>'tray'</c> in
        ///         anyway would be read as the DIR argument.</item>
        ///   <item><c>render(key, opts)</c> — <b>trapFauna</b>. What a pot comes up holding is not
        ///         directional; there is no dir to pass and the contract records <c>facings: 1</c>.</item>
        /// </list>
        ///
        /// <para><see cref="KeyTable"/> is the JS expression listing every key the family draws. The
        /// pack's two expose a <c>list()</c> function; the kit's expose plain arrays under four
        /// different names (<c>KINDS</c>, <c>BUILDS</c>, <c>FLEET</c>, <c>KINDS</c> again). It is null
        /// only for the one family that names no keys at all.</para>
        /// </summary>
        public readonly struct RenderShape
        {
            public readonly string KeyTable;
            public readonly bool TakesKey, TakesDir;

            public RenderShape(string keyTable, bool takesKey = true, bool takesDir = true)
            {
                KeyTable = keyTable; TakesKey = takesKey; TakesDir = takesDir;
            }
        }

        /// <summary>
        /// Families this baker serves, and the call shape each one answers to. <c>wharfIso</c> /
        /// <c>shipyardIso</c> and <c>shoreFinds</c> are deliberately NOT here: the structure kits size
        /// their own buffer per bake and report a fractional pivot with it (so
        /// <see cref="WharfIsoSheetBaker"/> takes them), and the finds are not directional at all and
        /// take their cell from an analytic <c>cellOf()</c>.
        ///
        /// <para><b>Measured, not assumed.</b> Each of the deck-loop five was checked against this
        /// baker's two preconditions before it was listed: it exposes the <c>W</c>/<c>H</c>/<c>pivot</c>
        /// triple that <c>RigCatalog.Install</c> requires, and its <c>render()</c> hands back a BARE
        /// <c>W×H×4</c> RGBA array rather than the <c>{data,w,h,px,py}</c> object the structure kits
        /// return. Both are re-checked at bake time — the first by <see cref="InstallAndGuard"/>, the
        /// second by the length check in <c>RenderFacings</c>.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, RenderShape> Shapes =
            new Dictionary<string, RenderShape>(StringComparer.Ordinal)
            {
                ["wharfDecor"] = new RenderShape("list()"),
                ["utilityIso"] = new RenderShape("list()"),

                // ---- the deck-loop kit ------------------------------------------------------------
                ["deckGear"]  = new RenderShape("KINDS"),
                ["trap"]      = new RenderShape("BUILDS"),
                ["buoyIso"]   = new RenderShape("FLEET"),
                ["fishTray2"] = new RenderShape(keyTable: null, takesKey: false, takesDir: true),
                ["trapFauna"] = new RenderShape("KINDS", takesKey: true, takesDir: false),
            };

        public static readonly IReadOnlyList<string> Families = Shapes.Keys.ToArray();

        static RenderShape ShapeOf(string rigKey) =>
            Shapes.TryGetValue(rigKey, out var s)
                ? s
                : throw new InvalidOperationException(
                      $"{rigKey} has no render shape registered with {nameof(IsoPropSheetBaker)}. " +
                      $"It serves {string.Join(", ", Families)}. A family added to the contract " +
                      "registry but not here has no way to be called — and guessing the call shape " +
                      "renders the rig DEFAULT with no error at all.");

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
                                      entry.DeclaredConvention, ShapeOf(req.RigKey), out double renderMs);

            // ---- crop: pivot-INCLUSIVE union of the ink, seeded at the pivot ------------------------
            int px = Mathf.RoundToInt((float)geo.PivotX), py = Mathf.RoundToInt((float)geo.PivotY);

            if (!MeasureCell(cells, geo.Width, geo.Height, px, py,
                             out int cw, out int ch, out int pivotX, out int pivotY,
                             out int left, out int top, out bool pivotInsideInk))
                throw new InvalidOperationException(
                    $"Every facing of {req.RigKey}.{req.PieceKey} rendered fully transparent. The key " +
                    "resolved but drew no pixels — refusing rather than writing an empty sheet.");

            // ---- THE ORACLE ------------------------------------------------------------------------
            contract.AssertMatchesContract(req.PieceKey, cw, ch, pivotX, pivotY);
            contract.AssertPivotInsideInk(req.PieceKey, pivotInsideInk);

            // ---- pack ------------------------------------------------------------------------------
            // The grid comes from the COMMITTED PLAN, not from ChooseGrid. Both families happen to agree
            // with ChooseGrid on all 103 pieces today, so this is not a live fix here — it is the same
            // rule its wharf sibling genuinely needs (see IsoPackContract.GridFor), and one packing rule
            // across the pack is what keeps that from being re-learned per baker.
            contract.GridFor(req.PieceKey, out int cols, out int rows);

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

            // ---- the recipe: what drew this sheet, beside it ----------------------------------------
            // The call template carries this baker's whole reason for existing: seven families that
            // agree on the buffer, the pivot and the cell rule and DISAGREE on the call — key+dir,
            // dir only, key only. Recording the template means nothing downstream has to keep a
            // second table of which family answers to which shape.
            var shape = ShapeOf(req.RigKey);
            var call = new RigRecipe.CallBlock();
            if (shape.TakesKey) call.Args.Add(req.PieceKey);
            if (shape.TakesDir) call.Args.Add("$dir");
            call.Args.Add("$opts");                       // these families bake at rig defaults: {}

            var grid = new RigRecipe.GridBlock { Columns = cols, Rows = rows };
            if (shape.TakesDir)
                grid.Axes.Add(RigRecipe.Axis.Facings(contract.Facings, entry.DeclaredConvention));

            result.RecipePath = RigRecipe.Write(result.AssetPath, new RigRecipe
            {
                Kit = RecipeKit,
                Baker = nameof(IsoPropSheetBaker),
                Rig = RigRecipe.RigBlockFor(req.RigKey),
                Call = call,
                Grid = grid,
                Pack = new RigRecipe.PackBlock
                {
                    NativeW = geo.Width, NativeH = geo.Height, NativePivotX = px, NativePivotY = py,
                    CropX = px + left, CropY = py + top,
                    CellW = cw, CellH = ch, PivotX = pivotX, PivotY = pivotY,
                    SheetW = pw, SheetH = ph,
                },
            });

            return result;
        }

        /// <summary>The kit id a recipe files itself under. One id for all seven families: they are
        /// one baker, one buffer shape and one cell rule, and the ledger groups by how a sheet was
        /// made rather than by which sprite folder it landed in.</summary>
        public const string RecipeKit = "iso-prop";

        /// <summary>
        /// THE CELL RULE for the two fixed-sheet families: the pivot-INCLUSIVE union of the ink bbox
        /// across every facing, seeded at the pivot. Pure — no host, no rig, no I/O — so a test can pin
        /// it against known buffers without baking anything.
        ///
        /// <para><b>The seeding at zero is the whole rule.</b> Starting the union at the pivot rather
        /// than at the first facing's ink is what makes <c>fireCabinet</c> work: it is wall-hung, nothing
        /// is drawn at deck level, and its ink stops 6 px ABOVE its own pivot. A crop merely "tight to
        /// ink" would put the piece's ground contact OUTSIDE its own cell — which does not fail loudly,
        /// it just stands in the wrong place. 1 of 61 pieces needs this and 60 do not, which is exactly
        /// why it must be the rule and not a special case.</para>
        ///
        /// <para>Returns <c>false</c> if every facing was fully transparent; the caller decides how loud
        /// that is.</para>
        /// </summary>
        public static bool MeasureCell(IReadOnlyList<byte[]> facings, int bufW, int bufH,
                                       int pivotX, int pivotY,
                                       out int cellW, out int cellH,
                                       out int cellPivotX, out int cellPivotY,
                                       out int left, out int top, out bool pivotInsideInk)
        {
            int right = 0, bottom = 0;
            left = 0; top = 0;
            bool anyInk = false;
            pivotInsideInk = false;

            foreach (byte[] rgba in facings)
            {
                BuildingRigAzimuthProbe.AlphaBounds(rgba, bufW, bufH,
                                                    out int x0, out int y0, out int x1, out int y1);
                if (x1 < x0) continue;                       // this facing drew nothing
                anyInk = true;
                left   = Mathf.Min(left,   x0 - pivotX);
                right  = Mathf.Max(right,  x1 - pivotX);
                top    = Mathf.Min(top,    y0 - pivotY);
                bottom = Mathf.Max(bottom, y1 - pivotY);
                if (pivotX >= x0 && pivotX <= x1 && pivotY >= y0 && pivotY <= y1) pivotInsideInk = true;
            }

            cellW = right - left + 1;
            cellH = bottom - top + 1;
            cellPivotX = -left;
            cellPivotY = -top;
            return anyInk;
        }

        /// <summary>Render one piece's facings through an installed host — the input to
        /// <see cref="MeasureCell"/>, exposed so a test measures without writing a PNG.</summary>
        public static byte[][] RenderFacings(IRigScriptHost host, string rigKey, string pieceKey,
                                             IsoPackContract contract, in RigGeometry geo,
                                             AzimuthConvention convention) =>
            RenderFacings(host, RigCatalog.Get(rigKey).GlobalName, pieceKey, contract, geo,
                          convention, ShapeOf(rigKey), out _);

        // ---- install + the standing guards -------------------------------------------------------

        public static RigGeometry InstallAndGuard(IRigScriptHost host, in RigEntry entry,
                                                  IsoPackContract contract, string rigKey)
        {
            if (contract.NeedsInstallModule)
                throw new InvalidOperationException(
                    $"{rigKey} does not load with Install — it belongs to a different baker. " +
                    $"{nameof(IsoPropSheetBaker)} serves {string.Join(", ", Families)} only.");

            contract.AssertSelfConsistent();

            // Before a facing correction is applied to anything: the contract MEASURED a handedness
            // and the catalog DECLARES one, and this baker's families no longer agree on it. Getting
            // it wrong does not fail — it writes the eight facings in reverse order.
            contract.AssertConventionAgrees(entry.DeclaredConvention, rigKey);

            // Install FIRST: the keyline gate is a probe of the rig's own global, which does not exist
            // until the source has been executed into the host.
            var geo = RigCatalog.Install(host, entry);
            contract.AssertKeylineGated(host, entry.GlobalName, rigKey);

            if (geo.Width != contract.Proj.nativeSheetW || geo.Height != contract.Proj.nativeSheetH)
                throw new InvalidOperationException(
                    $"{rigKey} reports a {geo.Width}×{geo.Height} native sheet but its contract " +
                    $"measured {contract.Proj.nativeSheetW}×{contract.Proj.nativeSheetH}. The rig " +
                    "changed shape; regenerate the contract before baking against it.");

            // ⚠️ nativeDirs is NOT the facing count here, and reads 0 rather than throwing. None of the
            // rigs this baker serves declares a DIRS global at all, so Install's
            // `typeof DIRS === 'number'` test is false and it reports 0 — silently, because 0
            // legitimately means "the rig does not say". Facings come from the contract; this only
            // records that the rig stayed silent. It matters most for trapFauna, whose contract says
            // 1: believing a 0 there would bake a sheet with no cells in it.
            if (geo.NativeDirs != 0 && geo.NativeDirs != contract.Facings)
                throw new InvalidOperationException(
                    $"{rigKey} now reports {geo.NativeDirs} native facings against the contract's " +
                    $"{contract.Facings}. One of the two is stale.");

            return geo;
        }

        // ---- render --------------------------------------------------------------------------------

        static byte[][] RenderFacings(IRigScriptHost host, string g, string pieceKey,
                                      IsoPackContract contract, in RigGeometry geo,
                                      AzimuthConvention convention, in RenderShape shape,
                                      out double renderMs)
        {
            AssertPieceExists(host, g, pieceKey, shape, contract);

            int expected = geo.Width * geo.Height * 4;
            var cells = new byte[contract.Facings][];
            var clock = new Stopwatch();

            for (int cell = 0; cell < contract.Facings; cell++)
            {
                // DirForCell carries the counter-clockwise correction, so what lands on disk is
                // genuinely clockwise. The pack's directional rigs measured CCW, matching houseIsoRig /
                // wharfBuildingRig / interiorIsoRig; the deck-loop kit measured CLOCKWISE and takes no
                // correction at all. IsoPackContract.AssertConventionAgrees is what stops those two
                // being confused — the wrong answer here writes eight good cells in reverse order.
                double dir = RigBaker.DirForCell(cell, contract.Facings, convention);
                string d = dir.ToString("R", CultureInfo.InvariantCulture);

                string call = shape.TakesKey && shape.TakesDir ? $"{Quote(pieceKey)},{d}"
                            : shape.TakesDir                   ? d
                            :                                    Quote(pieceKey);

                clock.Start();
                cells[cell] = host.EvaluateBytes($"{g}.render({call},{{}})");
                clock.Stop();

                if (cells[cell] == null || cells[cell].Length != expected)
                    throw new InvalidOperationException(
                        $"{g}.render({call}) came back " +
                        $"{(cells[cell]?.Length.ToString() ?? "null")} bytes, expected {expected} for " +
                        $"a {geo.Width}×{geo.Height} RGBA buffer. These rigs return a BARE RGBA array, " +
                        "not a {data,w,h} object — a wrapper here means the rig's return shape changed, " +
                        "or that this family belongs to WharfIsoSheetBaker instead.");
            }

            renderMs = clock.Elapsed.TotalMilliseconds;
            return cells;
        }

        /// <summary>
        /// Refuse a key the rig does not know. Baking one draws nothing and would otherwise land as an
        /// empty sheet with a correct-looking sprite count.
        ///
        /// <para>For the one family that names no keys, the check becomes "the contract must describe
        /// exactly this one thing" — because there is no rig-side vocabulary to check against, and a
        /// second key appearing in that family's contract would silently bake the same sheet twice
        /// under two names.</para>
        /// </summary>
        static void AssertPieceExists(IRigScriptHost host, string g, string pieceKey,
                                      in RenderShape shape, IsoPackContract contract)
        {
            if (shape.KeyTable == null)
            {
                if (contract.Count == 1 && contract.Cells[0].key == pieceKey) return;
                throw new InvalidOperationException(
                    $"{g} names no key table, so it draws exactly one thing — but its contract carries " +
                    $"{contract.Count} cells and this bake asked for '{pieceKey}'. A key-less render() " +
                    "would take that name as its DIR argument and bake a plausible, wrong sheet.");
            }

            if (host.EvaluateBool($"{g}.{shape.KeyTable}.indexOf({Quote(pieceKey)}) >= 0")) return;

            throw new InvalidOperationException(
                $"'{pieceKey}' is not in {g}.{shape.KeyTable}. Baking a key the rig does not know draws " +
                "nothing and would otherwise land as an empty sheet with a correct-looking sprite count.");
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
