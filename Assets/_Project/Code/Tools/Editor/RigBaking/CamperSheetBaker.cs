using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the camper kit: two sheets per length, laid out as
    /// <b>columns = door-swing frames, rows = facings</b>.
    ///
    /// <para>Why this kit needs its own baker rather than joining <see cref="BuildingRigBaker"/>, which
    /// it otherwise resembles closely: that one bakes ONE facing set per sheet and has no second axis.
    /// The camper's door is an animation, so every sheet carries a swing axis, and the crop has to be
    /// unioned over every frame of it as well as over every facing.</para>
    ///
    /// <para><b>ONE crop rect per LENGTH, not per sheet.</b> The building baker unions its crop over
    /// eight facings so a building does not shift when it turns. This goes one further and unions over
    /// <i>both roles</i> of a variant — rest and enter — so the camper does not shift when its DOOR
    /// opens either. The two sheets then share a cell size and a pivot by construction rather than by
    /// coincidence; measured on this drop they would have agreed anyway (220×159 bantam, 322×231
    /// clipper, both roles), but "they happen to agree today" is not something a slicer can rely on.
    /// <c>CamperBakeTests</c> pins the property.</para>
    ///
    /// <para><b>No silent skips and no silent defaults.</b> Every render passes every option
    /// explicitly, every cell is checked for ink before it is packed, and every enter sheet is checked
    /// for actual door motion — because the two ways this rig produces a plausible wrong picture
    /// (a compass string for <c>dir</c>; an awning over the cue) both fail quietly.</para>
    /// </summary>
    public static class CamperSheetBaker
    {
        /// <summary>
        /// Opaque pixels a single cell must carry before the bake will pack it.
        ///
        /// <para>🔴 <b>This is the guard against the trap that would otherwise cost a whole cycle.</b>
        /// <c>render(dir, opts)</c> takes an INTEGER INDEX 0..7, not the compass string the kit's
        /// README talks in: <c>camBasis</c> computes <c>dir·π/4</c>, so <c>render('N')</c> makes the
        /// angle <c>NaN</c>, every projected vertex becomes <c>NaN</c>, and the call returns a fully
        /// transparent cell <b>with no error thrown</b>. A baker that loops the rig's own
        /// <c>order</c> array by name writes eight empty sheets and reports success.</para>
        ///
        /// <para>Measured: the emptiest legitimate cell in this kit is the Bantam at N, 7833 opaque px;
        /// the NaN cell is 0. A floor of 1000 sits between them with room on both sides, and it is
        /// asserted per CELL rather than per sheet so one bad facing cannot hide behind seven good
        /// ones.</para>
        /// </summary>
        public const int MinOpaquePixelsPerCell = 1000;

        /// <summary>
        /// Pixels the door cue must repaint, at its best facing, before an <c>enter</c> sheet is
        /// accepted.
        ///
        /// <para>🔴 <b>The guard against the second measured trap.</b> The Clipper fits an awning by
        /// default, on the curb side, directly over its own door — so the cue it is supposed to show
        /// happens under a canopy. Measured swing 0→1 repaint, worst facing of eight:</para>
        /// <list type="bullet">
        ///   <item>unawninged — bantam <b>1566</b>, clipper <b>1641</b></item>
        ///   <item>awning fitted — bantam <b>710</b>, clipper <b>546</b></item>
        /// </list>
        /// <para>1000 sits in that gap with margin either side. An <c>enter</c> build that comes in
        /// under it is showing a few pixels twitching under a canopy, which is worse than no cue at
        /// all because it looks deliberate.</para>
        /// </summary>
        public const int MinCueDeltaPx = 1000;

        public sealed class SheetResult
        {
            public string Key, Variant, Role, AssetPath;
            public bool Awning;
            public int NativeW, NativeH, CropX, CropY, CellW, CellH, PivotX, PivotY;
            public int Columns, Rows, SheetW, SheetH, Facings, Frames, PngBytes, MaxCueDeltaPx;
            public bool PivotInsideInk;
            public CamperKit.AnchorEntry[] Anchors;

            public int CroppedPixels => SheetW * SheetH;

            public override string ToString() =>
                $"{Key}: cell {CellW}×{CellH} pivot {PivotX},{PivotY} → {Columns}×{Rows} " +
                $"sheet {SheetW}×{SheetH} ({PngBytes:N0} B)";
        }

        public sealed class BakeReport
        {
            public readonly List<SheetResult> Sheets = new();
            public CamperRigAzimuthProbe.Result Probe;
            public AzimuthConvention MeasuredConvention;
            public double TotalMilliseconds, RenderMilliseconds;
            public string EngineName;

            public long TotalPixels => Sheets.Sum(s => (long)s.CroppedPixels);
            public long TotalPngBytes => Sheets.Sum(s => (long)s.PngBytes);

            /// <summary>Runtime cost at RGBA32, which is what the import settings lock — the number the
            /// performance budget actually pays (CLAUDE.md rule 7), not the PNG size on disk.</summary>
            public double RuntimeMegabytes => TotalPixels * 4.0 / (1024 * 1024);
        }

        public static BakeReport BakeAll(IReadOnlyList<CamperKit.Build> builds = null)
        {
            builds ??= CamperKit.Builds;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get(CamperKit.RigKey);
            RigGeometry geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var report = new BakeReport { EngineName = host.EngineName };

            AssertKitMatchesRig(host, g, geo);

            // ---- MEASURE the convention, then cross-check the catalog's declaration ---------------
            // Probed on a plain build of the longer variant: the reading is about the TURNTABLE, which
            // no fit-out toggle touches, and the clipper's hitch offset is the larger of the two.
            report.Probe = CamperRigAzimuthProbe.Measure(
                host, g, OptsJs("clipper", awning: false, swing: 0), geo.Width, geo.Height, geo.PivotX);
            report.MeasuredConvention = report.Probe.Convention;

            if (report.Probe.Convention != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"AZIMUTH MISMATCH on rig '{CamperKit.RigKey}'.\n" +
                    $"  RigCatalog.CamperKit declares : {entry.DeclaredConvention}\n" +
                    $"  the measurement says          : {report.Probe.Convention}\n\n" +
                    report.Probe.Report + "\n\n" +
                    "The bake is refusing rather than guessing. Getting this wrong does not fail — it " +
                    "writes every facing in reverse order, which is how this mislabel shipped defects " +
                    "in five kits. Decide which is right and correct the catalog — do not relax this.");

            var clock = new Stopwatch();

            // ---- one crop per VARIANT, over every cell of every role it owns ----------------------
            foreach (var byVariant in builds.GroupBy(b => b.Variant, StringComparer.Ordinal))
            {
                var rendered = byVariant.ToDictionary(
                    b => b.Key,
                    b => RenderBuild(host, g, geo, b, report.Probe.Convention, clock),
                    StringComparer.Ordinal);

                var everyCell = rendered.Values.SelectMany(r => r.Cells).ToList();

                if (!IsoPropSheetBaker.MeasureCell(everyCell, geo.Width, geo.Height,
                                                   Mathf.RoundToInt((float)geo.PivotX),
                                                   Mathf.RoundToInt((float)geo.PivotY),
                                                   out int cw, out int ch,
                                                   out int pivotX, out int pivotY,
                                                   out int left, out int top, out bool pivotInsideInk))
                    throw new InvalidOperationException(
                        $"Every cell of '{byVariant.Key}' rendered fully transparent. The variant " +
                        "resolved but drew no pixels — refusing rather than writing an empty sheet.");

                int cropX = Mathf.RoundToInt((float)geo.PivotX) + left;
                int cropY = Mathf.RoundToInt((float)geo.PivotY) + top;

                foreach (var b in byVariant)
                    report.Sheets.Add(Pack(host, g, geo, b, rendered[b.Key],
                                           cropX, cropY, cw, ch, pivotX, pivotY, pivotInsideInk));
            }

            WriteContract(report);

            total.Stop();
            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            report.RenderMilliseconds = clock.Elapsed.TotalMilliseconds;
            return report;
        }

        // ---- rendering ---------------------------------------------------------------------------

        public sealed class RenderedBuild
        {
            public byte[][] Cells;          // facing-major: [facing * frames + frame]
            public int[] RigDirs;           // the rig dir each facing ROW was rendered at
            public bool Awning;             // as RESOLVED by the rig, never as assumed
            public int MaxCueDeltaPx;
        }

        /// <summary>
        /// Renders every cell of one build and applies both of this kit's guards.
        ///
        /// <para><paramref name="awningOverride"/> exists so a test can drive THIS code path with the
        /// awning forced on — the second measured trap — rather than assert against a stand-in that
        /// could drift from what the bake really does. Production callers leave it null.</para>
        /// </summary>
        public static RenderedBuild RenderBuild(IRigScriptHost host, string g, in RigGeometry geo,
                                                in CamperKit.Build b, AzimuthConvention convention,
                                                Stopwatch clock, bool? awningOverride = null)
        {
            // The awning is READ, not restated: a rest sheet takes the variant's own default so a drop
            // that re-fits a variant re-bakes correctly without anyone editing CamperKit.
            bool awning = awningOverride ?? b.AwningOverride
                          ?? host.EvaluateBool($"!!{g}.resolve({{variant:'{b.Variant}'}}).awning");

            int frames = b.Frames, facings = CamperKit.Facings;

            var built = new RenderedBuild
            {
                Cells = new byte[facings * frames][],
                RigDirs = new int[facings],
                Awning = awning,
            };

            for (int facing = 0; facing < facings; facing++)
            {
                // The whole handedness correction lives here: a counter-clockwise rig is emitted as
                // render((8−k)%8) so the SHEET that lands on disk is genuinely clockwise.
                double dir = RigBaker.DirForCell(facing, facings, convention);
                built.RigDirs[facing] = (int)Math.Round(dir);
                string d = dir.ToString("R", CultureInfo.InvariantCulture);

                for (int frame = 0; frame < frames; frame++)
                {
                    double swing = frames == 1 ? 0.0 : (double)frame / (frames - 1);

                    clock.Start();
                    byte[] rgba = RenderCell(host, g, geo, d, OptsJs(b.Variant, awning, swing),
                                             $"'{b.Key}' facing {facing} (rig dir {d}) frame {frame} " +
                                             $"(swing {swing:F2})");
                    clock.Stop();

                    built.Cells[facing * frames + frame] = rgba;
                }
            }

            built.MaxCueDeltaPx = frames == 1
                ? 0
                : Enumerable.Range(0, facings)
                            .Max(f => CountDiffering(built.Cells[f * frames],
                                                     built.Cells[f * frames + frames - 1]));

            if (b.Role == CamperKit.Role.Enter && built.MaxCueDeltaPx < MinCueDeltaPx)
                throw new InvalidOperationException(
                    $"'{b.Key}' is an ENTER sheet whose door cue barely moves: the widest swing 0→1 " +
                    $"repaint over all {facings} facings is {built.MaxCueDeltaPx} px, under the " +
                    $"{MinCueDeltaPx} px floor.\n\n" +
                    $"The build was rendered with awning={awning}. That is almost certainly the cause: " +
                    "the Clipper's default awning stands on the curb side directly over its own door, " +
                    "and measured, it takes the worst-facing repaint from 1641 px to 546. Bake the cue " +
                    "with the awning OFF (CamperKit.Build.AwningOverride already does this for every " +
                    "Enter row) rather than lowering this floor — a cue this small looks like a bug " +
                    "that someone shipped on purpose.");

            return built;
        }

        /// <summary>
        /// One render, with both shape guards on it: the buffer is the size the rig declares, and the
        /// cell actually drew something.
        ///
        /// <para><paramref name="dirJs"/> is spliced into the call verbatim, which is what lets a test
        /// hand it a compass string and watch the ink guard fire on the real code path.</para>
        /// </summary>
        public static byte[] RenderCell(IRigScriptHost host, string g, in RigGeometry geo,
                                        string dirJs, string optsJs, string what)
        {
            int expected = geo.Width * geo.Height * 4;
            byte[] rgba = host.EvaluateBytes($"{g}.render({dirJs},{optsJs})");

            if (rgba == null || rgba.Length != expected)
                throw new InvalidOperationException(
                    $"{g}.render({dirJs},…) came back {(rgba?.Length.ToString() ?? "null")} bytes, " +
                    $"expected {expected} for a {geo.Width}×{geo.Height} RGBA buffer. This rig " +
                    "returns a BARE RGBA array; a wrapper means its return shape changed.");

            int ink = CountOpaque(rgba);
            if (ink >= MinOpaquePixelsPerCell) return rgba;

            throw new InvalidOperationException(
                $"{what} drew {ink} opaque px, under the {MinOpaquePixelsPerCell} px floor.\n\n" +
                "⚠ If that number is ZERO, the overwhelmingly likely cause is that `dir` reached the " +
                "rig as something other than an integer 0..7. camBasis computes dir·π/4, so a compass " +
                "string ('N', 'SW') makes the angle NaN, every projected vertex becomes NaN, and " +
                "render returns a fully transparent cell WITHOUT THROWING. The kit's README talks in " +
                "compass names throughout, which is exactly why this is asserted per cell instead of " +
                "trusted. Measured for scale: the emptiest real cell in this kit is 7833 opaque px.");
        }

        /// <summary>
        /// The render options, spelled out in full on every single call.
        ///
        /// <para>Nothing in this rig has a fall-through that produces a plausible wrong picture the way
        /// the fuel kit's <c>wear</c> does — <c>resolve</c> is a clean per-key default. The options are
        /// still written out because a sheet on disk should say what it is, and because the next drop
        /// is the one that grows a cache key.</para>
        /// </summary>
        public static string OptsJs(string variant, bool awning, double swing)
        {
            string s = swing.ToString("R", CultureInfo.InvariantCulture);
            string w = CamperKit.BakedWeather.ToString("R", CultureInfo.InvariantCulture);
            return $"{{variant:'{variant}',paint:{CamperKit.BakedPaint},weather:{w},swing:{s}," +
                   $"awning:{(awning ? "true" : "false")},winAwn:false,vents:true,rack:false," +
                   "propane:true,jacks:true,chocks:true,hookups:true,step:true,night:false," +
                   "outline:false}";
        }

        // ---- packing -----------------------------------------------------------------------------

        static SheetResult Pack(IRigScriptHost host, string g, in RigGeometry geo,
                                in CamperKit.Build b, RenderedBuild built,
                                int cropX, int cropY, int cw, int ch, int pivotX, int pivotY,
                                bool pivotInsideInk)
        {
            int facings = CamperKit.Facings, frames = b.Frames;

            // Columns are the swing axis and rows are the facings, which is what keeps the widest sheet
            // inside the import cap — see CamperKit.ImportSizeCap for why the obvious layout does not.
            int cols = frames, rows = facings;
            int pw = cols * cw, ph = rows * ch;

            if (pw > CamperKit.ImportSizeCap || ph > CamperKit.ImportSizeCap)
                throw new InvalidOperationException(
                    $"'{b.Key}' packs to {pw}×{ph}, past this kit's {CamperKit.ImportSizeCap} px " +
                    "import cap. A sheet between the cap and Unity's hard 4096 BAKES FINE and then " +
                    "imports SILENTLY DOWNSCALED with the sprite count still correct, which poisons " +
                    "every slice rect. Reduce CamperKit.CueFrames rather than lifting the cap.");

            var pixels = new Color32[pw * ph];
            for (int facing = 0; facing < facings; facing++)
                for (int frame = 0; frame < frames; frame++)
                    BuildingRigBaker.BlitCropped(built.Cells[facing * frames + frame],
                                                 geo.Width, geo.Height, cropX, cropY, cw, ch,
                                                 pixels, pw, ph,
                                                 col: frame, rowFromTop: facing);

            var result = new SheetResult
            {
                Key = b.Key, Variant = b.Variant, Role = b.Role.ToString().ToLowerInvariant(),
                Awning = built.Awning,
                NativeW = geo.Width, NativeH = geo.Height,
                CropX = cropX, CropY = cropY,
                CellW = cw, CellH = ch, PivotX = pivotX, PivotY = pivotY,
                PivotInsideInk = pivotInsideInk,
                Columns = cols, Rows = rows, SheetW = pw, SheetH = ph,
                Facings = facings, Frames = frames,
                MaxCueDeltaPx = built.MaxCueDeltaPx,
                Anchors = ReadAnchors(host, g, b, built.RigDirs, cropX, cropY),
                AssetPath = $"{CamperKit.OutputFolder}/{b.Key}.png",
            };

            WritePng(pixels, pw, ph, result);
            return result;
        }

        /// <summary>
        /// The rig's own anchors, moved into cropped-cell space.
        ///
        /// <para>⚠️ The same arithmetic that moves the pivot has to move these, and getting it wrong
        /// does not fail loudly — the enter cue simply plays at the wrong spot on every camper, by the
        /// crop offset, consistently enough to look like a placement bug rather than a bake one.</para>
        /// </summary>
        static CamperKit.AnchorEntry[] ReadAnchors(IRigScriptHost host, string g,
                                                   in CamperKit.Build b, int[] rigDirs,
                                                   int cropX, int cropY)
        {
            var outp = new CamperKit.AnchorEntry[rigDirs.Length];

            for (int facing = 0; facing < rigDirs.Length; facing++)
            {
                string a = $"{g}.anchors({rigDirs[facing]},{{variant:'{b.Variant}'}})";
                outp[facing] = new CamperKit.AnchorEntry
                {
                    facing = facing,
                    rigDir = rigDirs[facing],
                    doorX = (float)(host.EvaluateNumber($"{a}.door.x") - cropX),
                    doorY = (float)(host.EvaluateNumber($"{a}.door.y") - cropY),
                    stepX = (float)(host.EvaluateNumber($"{a}.step.x") - cropX),
                    stepY = (float)(host.EvaluateNumber($"{a}.step.y") - cropY),
                    hitchX = (float)(host.EvaluateNumber($"{a}.hitch.x") - cropX),
                    hitchY = (float)(host.EvaluateNumber($"{a}.hitch.y") - cropY),
                };
            }

            return outp;
        }

        // ---- guards ------------------------------------------------------------------------------

        /// <summary>
        /// The facts <see cref="CamperKit"/> restates about the rig, checked against the live rig.
        ///
        /// <para>A variant added upstream and missed here bakes a short kit — silently, because every
        /// sheet it did bake is correct.</para>
        /// </summary>
        static void AssertKitMatchesRig(IRigScriptHost host, string g, in RigGeometry geo)
        {
            string variants = host.EvaluateString($"{g}.list().join(' ')");
            string expected = string.Join(" ", CamperKit.Variants);
            if (!string.Equals(variants, expected, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"The rig lists variants '{variants}' but CamperKit.Variants is '{expected}'. A " +
                    "length added upstream and missed here bakes a short kit and nothing says so — " +
                    "add the row rather than letting the tables drift.");

            int px = (int)host.EvaluateNumber($"{g}.PX");
            if (px != CamperKit.PixelsPerUnit)
                throw new InvalidOperationException(
                    $"The rig bakes at {px} px = 1 m but CamperKit.PixelsPerUnit is " +
                    $"{CamperKit.PixelsPerUnit}. This repo has two pixel grids in play and they are " +
                    "not interchangeable — a sheet imported at the wrong PPU is the right picture at " +
                    "the wrong size, everywhere, forever.");

            if (geo.NativeDirs != CamperKit.Facings)
                throw new InvalidOperationException(
                    $"The rig declares DIRS={geo.NativeDirs} but CamperKit.Facings is " +
                    $"{CamperKit.Facings}.");
        }

        static int CountOpaque(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 0) n++;
            return n;
        }

        static int CountDiffering(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] ||
                    a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) n++;
            return n;
        }

        // ---- the contract --------------------------------------------------------------------------

        static void WriteContract(BakeReport report)
        {
            var contract = new CamperKit.Contract
            {
                generated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rigKey = CamperKit.RigKey,
                globalName = CamperKit.GlobalName,
                rigConvention = report.MeasuredConvention.ToString(),
                pixelsPerUnit = CamperKit.PixelsPerUnit,
                importSizeCap = CamperKit.ImportSizeCap,
                weather = CamperKit.BakedWeather,
                paint = CamperKit.BakedPaint,
                sheets = report.Sheets.Select(s => new CamperKit.SheetEntry
                {
                    key = s.Key, variant = s.Variant, role = s.Role,
                    awning = s.Awning, weather = CamperKit.BakedWeather,
                    facings = s.Facings, frames = s.Frames,
                    cellW = s.CellW, cellH = s.CellH,
                    cropX = s.CropX, cropY = s.CropY,
                    pivotX = s.PivotX, pivotY = s.PivotY,
                    columns = s.Columns, rows = s.Rows,
                    sheetW = s.SheetW, sheetH = s.SheetH,
                    maxCueDeltaPx = s.MaxCueDeltaPx,
                    pivotInsideInk = s.PivotInsideInk,
                    anchors = s.Anchors,
                }).ToArray(),
            };

            string abs = Path.Combine(RigCatalog.RepoRoot, CamperKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
        }

        static void WritePng(Color32[] pixels, int w, int h, SheetResult result)
        {
            string outAbs = Path.Combine(RigCatalog.RepoRoot,
                                         Path.GetDirectoryName(result.AssetPath) ?? "");
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
