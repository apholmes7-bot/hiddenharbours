using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One preset of the ISO wharf structure kit, through the 8-facing turntable.</summary>
    public readonly struct WharfIsoBakeRequest
    {
        /// <summary>A preset from the rig's own <c>presets()</c> — <c>"floatSet"</c>, <c>"timberQuay"</c>…</summary>
        public readonly string Preset;
        public readonly string OutputFolder;
        public readonly string BaseName;

        public WharfIsoBakeRequest(string preset, string outputFolder, string baseName = null)
        {
            Preset = preset;
            OutputFolder = outputFolder;
            BaseName = string.IsNullOrEmpty(baseName) ? preset : baseName;
        }
    }

    public sealed class WharfIsoBakeResult
    {
        public string Preset, AssetPath, EngineName;
        public int CellWidth, CellHeight, PivotX, PivotY;
        public int Columns, Rows, SheetWidth, SheetHeight, Facings, PngBytes;
        public double RenderMilliseconds, TotalMilliseconds;

        /// <summary>Unity wants the pivot normalised from the BOTTOM-left.</summary>
        public Vector2 NormalisedPivot =>
            new Vector2(PivotX / (float)CellWidth, (CellHeight - PivotY) / (float)CellHeight);

        public override string ToString() =>
            $"wharfIso.{Preset}: {CellWidth}×{CellHeight} pivot {PivotX},{PivotY} → " +
            $"{Columns}×{Rows} sheet {SheetWidth}×{SheetHeight} ({PngBytes:N0} B)";
    }

    /// <summary>
    /// Bakes the ISO wharf structure kit — 7 families, 17 presets — against its committed contract.
    ///
    /// <para><b>This kit does not work like its siblings, in three ways that each fail silently.</b></para>
    ///
    /// <list type="number">
    ///   <item><b>It has no fixed sheet and no pivot global</b>, so it loads with
    ///         <c>InstallModule</c>; plain <c>Install</c> throws on the missing pivot. The rig sizes its
    ///         own buffer PER BAKE and hands back <c>{data,w,h,px,py,wet}</c> — an object, not the bare
    ///         RGBA array the prop rigs return — with <c>px,py</c> FRACTIONAL.</item>
    ///   <item><b>The cell is the union of those BUFFERS, not of the ink.</b> Measuring ink gives
    ///         <c>floatSet</c> 751×556 against the committed 757×592, and then every one of the 17 keys
    ///         disagrees at once. The gap is real: a float plus its gangway swings its origin's
    ///         projection so far with the facing that the pivot-aligned union runs 1.58× wider than the
    ///         largest single facing.</item>
    ///   <item><b>The cell is PARAMETRIC, so a fixed cap is not a fixed guarantee.</b> At rig defaults
    ///         every preset fits; at <c>bays: 8</c> the same pier packs far larger, and a Fundy
    ///         <c>tideRange: 14</c> larger again. This baker therefore reads the RENDERED cell every
    ///         time and never a table — and bakes at rig defaults, which is what the contract measured.</item>
    /// </list>
    ///
    /// <para>Because the buffers differ per facing and are smaller than the union cell, each facing is
    /// INSET into the cell at its own pivot-aligned offset. That is <see cref="BuildingRigBaker.BlitCropped"/>
    /// with a NEGATIVE crop origin — the same flip and the same bounds-skip, rather than a second blit
    /// that could drift from it by a mirror.</para>
    /// </summary>
    public static class WharfIsoSheetBaker
    {
        public const string RigKey = "wharfIso";

        /// <summary>JS scratch global the render lands in, so its fields are read without re-rendering
        /// (the rasteriser is the expensive half — it z-buffers and dithers every face in JS).</summary>
        const string Scratch = "globalThis.__hhWharfCell";

        public static WharfIsoBakeResult Bake(WharfIsoBakeRequest req)
        {
            var total = Stopwatch.StartNew();
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var result = Bake(req, host);
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        public static WharfIsoBakeResult Bake(WharfIsoBakeRequest req, IRigScriptHost host)
        {
            var contract = IsoPackContract.Load(RigKey);
            var entry = RigCatalog.Get(RigKey);

            contract.AssertSelfConsistent();

            // InstallModule, never Install: this rig exposes no pivot and Install throws on that.
            RigCatalog.InstallModule(host, entry);
            string g = entry.GlobalName;

            // Install FIRST — the gate is a probe of the rig's own global, which does not exist until
            // the source has been executed into the host.
            contract.AssertKeylineGated(host, g, RigKey);
            AssertPresetExists(host, g, req.Preset);

            var facings = RenderFacings(host, g, req.Preset, contract.Facings,
                                        entry.DeclaredConvention, out double renderMs);

            // ---- the cell: pivot-aligned union of the BUFFER extents, floor/ceil --------------------
            // Continuous, because px,py are fractional. floor/ceil rather than round is what the
            // contract measured, and the two disagree by a pixel often enough to matter.
            double left = double.MaxValue, right = double.MinValue;
            double top = double.MaxValue, bottom = double.MinValue;

            foreach (var f in facings)
            {
                left   = Math.Min(left,   -f.PivotX);
                right  = Math.Max(right,  f.Width  - 1 - f.PivotX);
                top    = Math.Min(top,    -f.PivotY);
                bottom = Math.Max(bottom, f.Height - 1 - f.PivotY);
            }

            int cw = (int)Math.Ceiling(right) - (int)Math.Floor(left) + 1;
            int ch = (int)Math.Ceiling(bottom) - (int)Math.Floor(top) + 1;
            int pivotX = -(int)Math.Floor(left), pivotY = -(int)Math.Floor(top);

            // ---- THE ORACLE ------------------------------------------------------------------------
            contract.AssertMatchesContract(req.Preset, cw, ch, pivotX, pivotY);

            // ---- pack ------------------------------------------------------------------------------
            BuildingRigBaker.ChooseGrid(cw, ch, contract.Facings, out int cols, out int rows,
                                        contract.ImportSizeCap);

            int pw = cols * cw, ph = rows * ch;
            contract.AssertSheetFits(req.Preset, cols, rows, pw, ph);

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < contract.Facings; cell++)
            {
                var f = facings[cell];

                // Where this facing's buffer pixel (0,0) lands inside the union cell. Exact placement is
                // guaranteed in range by construction — pivotX ≥ px because pivotX = −floor(min(−px)) —
                // and the bounds are integers, so rounding cannot push it out. Asserted anyway: a
                // one-pixel slip here reads as art jitter as the structure turns, not as a bake error.
                int offsetX = Mathf.RoundToInt((float)(pivotX - f.PivotX));
                int offsetY = Mathf.RoundToInt((float)(pivotY - f.PivotY));

                if (offsetX < 0 || offsetY < 0 || offsetX + f.Width > cw || offsetY + f.Height > ch)
                    throw new InvalidOperationException(
                        $"wharfIso.{req.Preset} facing {cell}: a {f.Width}×{f.Height} buffer insets to " +
                        $"({offsetX},{offsetY}) in a {cw}×{ch} cell, which does not fit. The union and " +
                        "the per-facing pivots disagree — the cell rule and the render have diverged.");

                BuildingRigBaker.BlitCropped(f.Data, f.Width, f.Height,
                                             cropX: -offsetX, cropY: -offsetY, cw: cw, ch: ch,
                                             dst: pixels, pw: pw, ph: ph,
                                             col: cell % cols, rowFromTop: cell / cols);
            }

            var result = new WharfIsoBakeResult
            {
                Preset = req.Preset,
                EngineName = host.EngineName,
                CellWidth = cw,
                CellHeight = ch,
                PivotX = pivotX,
                PivotY = pivotY,
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

        // ---- render ---------------------------------------------------------------------------------

        readonly struct Facing
        {
            public readonly byte[] Data;
            public readonly int Width, Height;
            /// <summary>FRACTIONAL — the rig reports where the model origin projected, per bake.</summary>
            public readonly double PivotX, PivotY;

            public Facing(byte[] data, int width, int height, double pivotX, double pivotY)
            {
                Data = data; Width = width; Height = height; PivotX = pivotX; PivotY = pivotY;
            }
        }

        static Facing[] RenderFacings(IRigScriptHost host, string g, string preset, int facings,
                                      AzimuthConvention convention, out double renderMs)
        {
            var outp = new Facing[facings];
            var clock = new Stopwatch();

            try
            {
                for (int cell = 0; cell < facings; cell++)
                {
                    double dir = RigBaker.DirForCell(cell, facings, convention);
                    string d = dir.ToString("R", CultureInfo.InvariantCulture);

                    // Render ONCE into a scratch global, then read its fields. Evaluating
                    // `render(...).w` and `render(...).data` separately would rasterise twice per field.
                    clock.Start();
                    host.Execute($"{Scratch} = {g}.render({Quote(preset)},{d},{{}});");
                    clock.Stop();

                    int w = (int)host.EvaluateNumber($"{Scratch}.w");
                    int h = (int)host.EvaluateNumber($"{Scratch}.h");
                    double px = host.EvaluateNumber($"{Scratch}.px");
                    double py = host.EvaluateNumber($"{Scratch}.py");
                    byte[] data = host.EvaluateBytes($"{Scratch}.data");

                    if (w <= 0 || h <= 0)
                        throw new InvalidOperationException(
                            $"wharfIso.{preset} facing {cell} came back {w}×{h}. The preset resolved but " +
                            "rendered nothing — refusing rather than writing an empty cell.");

                    if (data == null || data.Length != w * h * 4)
                        throw new InvalidOperationException(
                            $"wharfIso.{preset} facing {cell}: buffer is " +
                            $"{(data?.Length.ToString() ?? "null")} bytes, expected {w * h * 4} for " +
                            $"{w}×{h} RGBA. This rig returns {{data,w,h,px,py,wet}} — a bare array here " +
                            "means the return shape changed.");

                    outp[cell] = new Facing(data, w, h, px, py);
                }
            }
            finally
            {
                host.Execute($"{Scratch} = null;");
            }

            renderMs = clock.Elapsed.TotalMilliseconds;
            return outp;
        }

        /// <summary>Every preset the rig knows, in its own order.</summary>
        public static IReadOnlyList<string> Presets(IRigScriptHost host, string globalName) =>
            host.EvaluateString($"{globalName}.presets().join(',')")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        static void AssertPresetExists(IRigScriptHost host, string g, string preset)
        {
            if (host.EvaluateBool($"{g}.presets().indexOf({Quote(preset)}) >= 0")) return;

            throw new InvalidOperationException(
                $"'{preset}' is not in {g}.presets(). Known: " +
                string.Join(", ", Presets(host, g)) + ".\n" +
                "⚠️ Note this baker takes a PRESET NAME and passes it positionally. Passing it as an " +
                "option object instead ({preset:'…'}) renders the rig DEFAULT with no error at all — " +
                "the same trap the building kit documents.");
        }

        static string Quote(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        // ---- write ----------------------------------------------------------------------------------

        static void WritePng(Color32[] pixels, int w, int h, WharfIsoBakeResult result)
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
