using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One beachcombing find in one weathering state — 8 lie angles × 3 variants on a sheet.</summary>
    public readonly struct ShoreFindsBakeRequest
    {
        /// <summary>A find from the rig's own <c>list()</c> — <c>"SoftshellClam"</c>, <c>"RopeScrap"</c>…</summary>
        public readonly string Find;

        /// <summary>A weathering state from the contract's <c>states</c> — <c>wet</c>/<c>dry</c>/<c>bleached</c>.</summary>
        public readonly string State;

        public readonly string OutputFolder;
        public readonly string BaseName;

        public ShoreFindsBakeRequest(string find, string state, string outputFolder, string baseName = null)
        {
            Find = find;
            State = state;
            OutputFolder = outputFolder;
            BaseName = string.IsNullOrEmpty(baseName) ? $"{find}_{state}" : baseName;
        }
    }

    public sealed class ShoreFindsBakeResult
    {
        public string Find, State, AssetPath, EngineName;
        public int CellWidth, CellHeight, PivotX, PivotY;
        public int Columns, Rows, SheetWidth, SheetHeight, Cells, LieAngles, Variants, PngBytes;
        public double RenderMilliseconds, TotalMilliseconds;

        /// <summary>Unity wants the pivot normalised from the BOTTOM-left.</summary>
        public Vector2 NormalisedPivot =>
            new Vector2(PivotX / (float)CellWidth, (CellHeight - PivotY) / (float)CellHeight);

        public override string ToString() =>
            $"shoreFinds.{Find}.{State}: {CellWidth}×{CellHeight} pivot {PivotX},{PivotY} → " +
            $"{Columns}×{Rows} sheet {SheetWidth}×{SheetHeight} ({PngBytes:N0} B)";
    }

    /// <summary>
    /// Bakes the shoreline finds — 36 finds × 3 weathering states — against their committed contract.
    ///
    /// <para><b>Its own baker, not a "non-directional mode" on <see cref="IsoPropSheetBaker"/>.</b> The
    /// two share nothing but the oracle: every single thing the prop baker does per piece is different
    /// here, and a shared code path would be a parameter switch at each of them.</para>
    ///
    /// <list type="number">
    ///   <item><b>There are no facings.</b> The finds lie FLAT on sand — there is no <c>project()</c> and
    ///         no turntable, only a LIE ANGLE: 8 canonical steps of the object's long axis rotated in the
    ///         GENERATOR before projection. So this baker must NOT call
    ///         <see cref="RigBaker.DirForCell"/>: the counter-clockwise correction its three siblings
    ///         carry would mirror art that has no facing to correct. <c>IsoPackContract.Facings</c>
    ///         THROWS for this family rather than quietly answering, which is the guard that keeps a
    ///         copy-pasted turntable loop from compiling into a plausible wrong sheet.</item>
    ///   <item><b>The cell is ANALYTIC.</b> <c>cellOf(key)</c> computes it from the find's form
    ///         parameters and never rasterises, so nothing the renderer does can move it — the keyline
    ///         gate included, which is why these 36 cells did not shift when the gate landed while
    ///         wharfDecor's 61 and utilityIso's 42 all did. Nothing here measures ink.</item>
    ///   <item><b>A THIRD render return shape.</b> The prop rigs return a bare RGBA array and the wharf
    ///         kit returns <c>{data,w,h,px,py,wet}</c>; this one returns
    ///         <c>{w,h,<b>rgba</b>,pivot,anchors,params,ramps,report}</c> — the pixels are on
    ///         <c>.rgba</c> and <c>.data</c> is <c>undefined</c>. Reading <c>.data</c> here does not
    ///         throw at the JS boundary, it hands back nothing.</item>
    ///   <item><b>One sheet per find per STATE</b>, not one per piece. wet/dry/bleached are ramp
    ///         transforms over identical geometry, so all three share the find's one cell — but they are
    ///         separate textures, and 36 × 3 = <b>108 sheets</b> is the shipping inventory.</item>
    /// </list>
    ///
    /// <para><b>⚠️ <c>ShoreFinds.cellOf</c> is not the rig's internal <c>cellOf</c>.</b> The rig declares
    /// both <c>cellOf</c> and <c>cellFull</c> and exports the latter UNDER THE FORMER'S NAME
    /// (<c>cellOf: cellFull</c>). The internal one returns <c>pvy: 0</c>; only <c>cellFull</c> fills in
    /// <c>pvy = h − _half</c>. Since 0 is a perfectly legitimate-looking pivot row, porting the internal
    /// function's body — the obvious thing to do when reading the rig top to bottom — would plant every
    /// find by its top edge and never fail a shape check. Call the EXPORT.</para>
    ///
    /// <para><b>Sheet layout.</b> 24 cells at flat index <c>variant × 8 + lieAngle</c>, row-major from
    /// the top-left of the committed grid — the same indexing every other sheet in this repo uses. Note
    /// the contract's <c>sheetAxes</c> prose ("8 lie angles across × 3 variants down") describes the
    /// LOGICAL axes, which only coincide with the physical grid at 8 columns; the committed plans are
    /// 24×1 and 12×2, so the flat index is what bridges them. Slices are named by index and never by a
    /// compass label — a name states which cell, not which way it lies.</para>
    /// </summary>
    public static class ShoreFindsSheetBaker
    {
        public const string RigKey = "shoreFinds";

        /// <summary>Where the sheets land — beside the contract that measures them.</summary>
        public const string DefaultOutputFolder = "Assets/_Project/Art/Sprites/Shore/FindsIso";

        /// <summary>JS scratch global the render lands in, so its fields are read without re-rendering.</summary>
        const string Scratch = "globalThis.__hhFindCell";

        public static ShoreFindsBakeResult Bake(ShoreFindsBakeRequest req)
        {
            var total = Stopwatch.StartNew();
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var contract = IsoPackContract.Load(RigKey);
            Install(host, contract);
            var result = Bake(req, host, contract);
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// Install the rig into a caller-owned host and run the standing guards ONCE, so a 108-sheet
        /// batch pays for the rig install and the contract vetting a single time.
        /// </summary>
        public static void Install(IRigScriptHost host, IsoPackContract contract)
        {
            if (contract.Rule != IsoPackContract.CellRule.Analytic)
                throw new InvalidOperationException(
                    $"{contract.RigName} is not the analytic family — it belongs to a different baker. " +
                    $"{nameof(ShoreFindsSheetBaker)} serves shoreFinds only.");

            contract.AssertSelfConsistent();

            // InstallModule, never Install: this rig exposes no pivot global and Install throws on that.
            // (Its DIRS is a string ARRAY too, so Install's `typeof DIRS === 'number'` would report 0
            // facings silently — but the pivot is what actually stops it.)
            var entry = RigCatalog.Get(RigKey);
            RigCatalog.InstallModule(host, entry);

            // Install FIRST — the gate probes the rig's own global, which does not exist until the
            // source has been executed into the host.
            contract.AssertKeylineGated(host, entry.GlobalName, RigKey);
            AssertAxesAgree(host, entry.GlobalName, contract);
        }

        /// <summary>Bake one sheet through an already-installed host.</summary>
        public static ShoreFindsBakeResult Bake(ShoreFindsBakeRequest req, IRigScriptHost host,
                                                IsoPackContract contract)
        {
            string g = RigCatalog.Get(RigKey).GlobalName;

            AssertFindExists(host, g, req.Find);
            AssertStateExists(contract, req.State);

            // ---- the cell: cellOf(key) VERBATIM, no render and no raster ----------------------------
            host.Execute($"{Scratch} = {g}.cellOf({Quote(req.Find)});");
            int cw, ch, pivotX, pivotY;
            try
            {
                cw = (int)host.EvaluateNumber($"{Scratch}.w");
                ch = (int)host.EvaluateNumber($"{Scratch}.h");
                pivotX = (int)host.EvaluateNumber($"{Scratch}.pvx");
                pivotY = (int)host.EvaluateNumber($"{Scratch}.pvy");
            }
            finally
            {
                host.Execute($"{Scratch} = null;");
            }

            // ---- THE ORACLE --------------------------------------------------------------------------
            contract.AssertMatchesContract(req.Find, cw, ch, pivotX, pivotY);

            int lies = contract.LieAngleCount, variants = contract.Variants;
            int cells = contract.CellsPerSheet;

            // ---- pack ----------------------------------------------------------------------------------
            contract.GridFor(req.Find, out int cols, out int rows);
            int pw = cols * cw, ph = rows * ch;
            contract.AssertSheetFits(req.Find, cols, rows, pw, ph);

            var pixels = new Color32[pw * ph];
            var clock = new Stopwatch();

            for (int v = 0; v < variants; v++)
            {
                for (int lie = 0; lie < lies; lie++)
                {
                    // ⚠️ The lie angle index goes in RAW. No DirForCell, no convention correction: a flat
                    // find has no facing to be clockwise or counter-clockwise about, and the contract
                    // records azimuth.convention "none" for exactly that reason.
                    clock.Start();
                    host.Execute($"{Scratch} = {g}.render({Quote(req.Find)},{lie}," +
                                 $"{{state:{Quote(req.State)},variant:{v}}});");
                    clock.Stop();

                    byte[] rgba = ReadCell(host, req, lie, v, cw, ch, pivotX, pivotY);

                    int index = v * lies + lie;
                    BuildingRigBaker.BlitCropped(rgba, cw, ch, cropX: 0, cropY: 0, cw: cw, ch: ch,
                                                 dst: pixels, pw: pw, ph: ph,
                                                 col: index % cols, rowFromTop: index / cols);
                }
            }

            var result = new ShoreFindsBakeResult
            {
                Find = req.Find,
                State = req.State,
                EngineName = host.EngineName,
                CellWidth = cw,
                CellHeight = ch,
                PivotX = pivotX,
                PivotY = pivotY,
                Columns = cols,
                Rows = rows,
                SheetWidth = pw,
                SheetHeight = ph,
                Cells = cells,
                LieAngles = lies,
                Variants = variants,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
                AssetPath = $"{req.OutputFolder}/{req.BaseName}.png",
            };

            WritePng(pixels, pw, ph, result);
            return result;
        }

        /// <summary>Every find the rig knows, in its own order.</summary>
        public static IReadOnlyList<string> Finds(IRigScriptHost host) =>
            host.EvaluateString($"{RigCatalog.Get(RigKey).GlobalName}.list().join(',')")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        // ---- read + guard the render ----------------------------------------------------------------

        /// <summary>
        /// Pull one rendered cell out of the scratch global, asserting the return SHAPE as it goes.
        /// The shape checks are the point: this rig's buffer is on <c>.rgba</c>, and every plausible
        /// wrong read of it fails quietly rather than loudly.
        /// </summary>
        static byte[] ReadCell(IRigScriptHost host, in ShoreFindsBakeRequest req, int lie, int v,
                               int cw, int ch, int pivotX, int pivotY)
        {
            try
            {
                if (!host.EvaluateBool($"typeof {Scratch}.rgba !== 'undefined'"))
                    throw new InvalidOperationException(
                        $"shoreFinds.{req.Find}.{req.State} lie {lie} variant {v}: render() returned no " +
                        "`rgba`. This rig hands the pixels back on .rgba — NOT .data like the wharf kit " +
                        "and NOT as a bare array like the prop rigs. If this fired, the return shape " +
                        "changed; do not switch to .data, which is undefined here.");

                int w = (int)host.EvaluateNumber($"{Scratch}.w");
                int h = (int)host.EvaluateNumber($"{Scratch}.h");
                int px = (int)host.EvaluateNumber($"{Scratch}.pivot.x");
                int py = (int)host.EvaluateNumber($"{Scratch}.pivot.y");

                // The whole sheet is one cell size, so a render that disagrees with cellOf() would land
                // in the grid at the wrong scale — and, at these sizes, look merely "a bit off".
                if (w != cw || h != ch || px != pivotX || py != pivotY)
                    throw new InvalidOperationException(
                        $"shoreFinds.{req.Find}.{req.State} lie {lie} variant {v} rendered {w}×{h} at " +
                        $"pivot {px},{py}, but cellOf() says {cw}×{ch} at {pivotX},{pivotY}. Every " +
                        "state, variant and lie angle of one find must share ONE cell — the cell is " +
                        "sized for the largest variant precisely so they do.");

                byte[] rgba = host.EvaluateBytes($"{Scratch}.rgba");
                if (rgba == null || rgba.Length != cw * ch * 4)
                    throw new InvalidOperationException(
                        $"shoreFinds.{req.Find}.{req.State} lie {lie} variant {v}: buffer is " +
                        $"{(rgba?.Length.ToString() ?? "null")} bytes, expected {cw * ch * 4} for a " +
                        $"{cw}×{ch} RGBA cell.");

                return rgba;
            }
            finally
            {
                host.Execute($"{Scratch} = null;");
            }
        }

        // ---- the standing guards ---------------------------------------------------------------------

        /// <summary>
        /// Cross-check the sheet axes the contract declares against the ones the rig exposes. The finds
        /// sheet is the only one in the pack whose cell count is a PRODUCT (lie angles × variants), so a
        /// drift in either factor silently changes how many cells a sheet holds.
        /// </summary>
        static void AssertAxesAgree(IRigScriptHost host, string g, IsoPackContract contract)
        {
            int rigLies = (int)host.EvaluateNumber($"{g}.DIRS.length");
            int rigVariants = (int)host.EvaluateNumber($"{g}.variants");
            int rigStates = (int)host.EvaluateNumber($"{g}.STATES.length");

            if (rigLies != contract.LieAngleCount || rigVariants != contract.Variants ||
                rigStates != contract.StateNames.Count)
                throw new InvalidOperationException(
                    $"shoreFinds axes disagree — rig says {rigLies} lie angles × {rigVariants} variants " +
                    $"× {rigStates} states, contract says {contract.LieAngleCount} × " +
                    $"{contract.Variants} × {contract.StateNames.Count}. One of the two is stale; a " +
                    "sheet's cell count is the PRODUCT of the first two, so this cannot be papered over.");

            // ⚠️ Read the count off DIRS.LENGTH, never off `RigGeometry.NativeDirs`. DIRS here is a
            // string array, so Install's `typeof DIRS === 'number'` test is false and it reports 0 —
            // silently, because 0 legitimately means "the rig does not say".
            if (host.EvaluateBool($"typeof {g}.DIRS === 'number'"))
                throw new InvalidOperationException(
                    $"{g}.DIRS is now a number. It was a string array, which is why this family loads " +
                    "with InstallModule and takes its axes from the contract. Re-check the rig shape.");
        }

        static void AssertFindExists(IRigScriptHost host, string g, string find)
        {
            if (host.EvaluateBool($"{g}.list().indexOf({Quote(find)}) >= 0")) return;

            throw new InvalidOperationException(
                $"'{find}' is not in {g}.list(). ⚠️ This rig does NOT throw on an unknown key — " +
                "`byKey[key] || FINDS[0]` falls back to the first find, so an unknown key bakes a " +
                "perfectly good sheet OF THE WRONG OBJECT under the requested name. Known: " +
                string.Join(", ", Finds(host)) + ".");
        }

        static void AssertStateExists(IsoPackContract contract, string state)
        {
            foreach (string s in contract.StateNames)
                if (string.Equals(s, state, StringComparison.Ordinal)) return;

            throw new InvalidOperationException(
                $"'{state}' is not a weathering state. ⚠️ Same silent fallback as the key: the rig does " +
                "`STATES.indexOf(o.state) >= 0 ? o.state : 'dry'`, so a typo bakes the DRY ramp into a " +
                $"sheet named for the state you asked for. Known: {string.Join(", ", contract.StateNames)}.");
        }

        static string Quote(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        // ---- write --------------------------------------------------------------------------------

        static void WritePng(Color32[] pixels, int w, int h, ShoreFindsBakeResult result)
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
