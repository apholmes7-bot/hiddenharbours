using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What to bake. Facings and rock frames are the owner's dials.</summary>
    public readonly struct BakeRequest
    {
        public readonly string RigKey;
        public readonly int Facings;
        /// <summary>0 = base sheet only (no rock). Otherwise the number of rock frames.</summary>
        public readonly int RockFrames;
        public readonly string OutputFolder;   // project-relative, e.g. "Assets/_Project/Art/Boats"
        public readonly string BaseName;       // e.g. "LobsterBoatIso"
        /// <summary>Cells per page row. 8 keeps every page under the 4096 texture cap and matches
        /// the existing rock sheets' shape, so importer settings carry over.</summary>
        public readonly int Columns;
        /// <summary>Max rows on one page. 8 gives 3648×3360 for a 456×420 cell — the exact
        /// dimensions of the shipped CapeIslanderIsoRock sheet, which is already proven at
        /// maxTextureSize 4096.</summary>
        public readonly int MaxRowsPerPage;

        public BakeRequest(string rigKey, int facings, int rockFrames, string outputFolder,
                           string baseName, int columns = 8, int maxRowsPerPage = 8)
        {
            RigKey = rigKey; Facings = facings; RockFrames = rockFrames;
            OutputFolder = outputFolder; BaseName = baseName;
            Columns = columns; MaxRowsPerPage = maxRowsPerPage;
        }
    }

    public sealed class BakePage
    {
        public string AssetPath;
        public int Width, Height, Columns, Rows, CellCount;
        public override string ToString() =>
            $"{AssetPath}  {Width}×{Height}  {Columns}×{Rows} cells ({CellCount})";
    }

    public sealed class BakeResult
    {
        public string RigKey;
        public string EngineName;
        public RigGeometry Geometry;
        public AzimuthConvention MeasuredConvention;
        public string ConventionReport;
        public int Facings, RockFrames;
        public readonly List<BakePage> Pages = new List<BakePage>();
        public string AnchorJsonPath;
        public double RenderMilliseconds;   // time inside render+readback only
        public double TotalMilliseconds;    // whole bake including PNG encode + write
        public long TotalPngBytes;
        public int CellsRendered;

        /// <summary>Uncompressed RGBA32 texture memory the baked pages will occupy at runtime.</summary>
        public long RuntimeBytesRgba32
        {
            get { long t = 0; foreach (var p in Pages) t += (long)p.Width * p.Height * 4; return t; }
        }
    }

    /// <summary>
    /// Runs an art director rig and writes sprite-sheet PNGs.
    ///
    /// This deliberately stops at "PNG on disk". Slicing is <see cref="Art.Editor.SpriteSheetSlicer"/>'s
    /// job and Def-wiring is BoatVisualLibraryBuilder's — building a parallel pipeline here would
    /// fork the pivot/idempotency rules that those two already own and test.
    /// </summary>
    public static class RigBaker
    {
        /// <summary>Unity's hard texture cap. A sheet over this imports SILENTLY DOWNSCALED: the
        /// sprite COUNT still matches, so only a cell-size or pivot assert catches it. That is why
        /// pages are split rather than made taller.</summary>
        public const int MaxTextureSize = 4096;

        /// <summary>
        /// Rig <c>dir</c> argument for output cell <paramref name="cell"/>.
        ///
        /// THE WHOLE CORRECTION LIVES HERE. Every rig's <c>th = dir*Math.PI/4</c> means one dir unit
        /// is 45°, so an N-facing bake steps by <c>8/N</c> dir units (verified in ADR 0021:
        /// fractional dir is a genuine intermediate view, agreeing with the analytic anchor bearing
        /// to within ~1°, not a snapped or degenerate one).
        ///
        /// For a COUNTER-CLOCKWISE rig we emit cell k as <c>render((N−k) % N)</c> — the familiar
        /// 8-facing <c>(8−k)%8</c> generalised — so the sheet that lands on disk is genuinely
        /// clockwise. For a CLOCKWISE rig (characterIsoRig, rodIsoRig) we emit <c>render(k)</c>
        /// unchanged; applying the correction to those would RE-MIRROR art the art director already
        /// fixed, which is precisely the blanket-fix mistake the README warns about.
        ///
        /// ⇒ Anything baked here is genuinely clockwise, so its
        /// <c>BoatVisualDef.FacingsAreCounterClockwise</c> is <c>false</c>. The flag survives only
        /// for legacy hand-exported sheets until they are re-baked.
        /// </summary>
        public static double DirForCell(int cell, int facings, AzimuthConvention convention)
        {
            if (facings <= 0) throw new ArgumentOutOfRangeException(nameof(facings));
            int k = ((cell % facings) + facings) % facings;
            int index = convention == AzimuthConvention.CounterClockwise ? (facings - k) % facings : k;
            return index * (8.0 / facings);
        }

        public static BakeResult Bake(BakeRequest req)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get(req.RigKey);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new BakeResult
            {
                RigKey = req.RigKey,
                EngineName = host.EngineName,
                Geometry = geo,
                Facings = req.Facings,
                RockFrames = req.RockFrames,
            };

            // ---- MEASURE the azimuth convention from pixels, then cross-check the declaration ----
            // Probe at a quarter turn, in the rig's own native dir units (dir 2 of 8 == 90°).
            byte[] quarter = host.EvaluateBytes($"{g}.render({(8.0 / 4.0).ToString("R", CultureInfo.InvariantCulture)},{{}})");
            var probe = RigAzimuthProbe.MeasureFromQuarterTurn(quarter, geo.Width, geo.Height);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;

            if (probe.Convention != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"AZIMUTH MISMATCH on rig '{req.RigKey}'.\n" +
                    $"  docs/art/rigs/README.md declares : {entry.DeclaredConvention}\n" +
                    $"  the rendered pixels say          : {probe.Convention}\n\n" +
                    probe.Report + "\n\n" +
                    "The bake is refusing rather than guessing. A silent guess here is how this " +
                    "mislabel shipped defects in five kits. Look at the art, decide which is right, " +
                    "and correct the README (or the catalog) — do not relax this check.");

            if (probe.Elongation < 1.5)
                Debug.LogWarning($"[rig-baker] {req.RigKey}: silhouette elongation is only " +
                                 $"{probe.Elongation:F2} at a quarter turn — the principal axis is " +
                                 "weak, so treat the convention measurement with suspicion.");

            // ---- Bake ----
            int rockFrames = Mathf.Max(1, req.RockFrames);
            bool withRock = req.RockFrames > 0;
            int cellsTotal = req.Facings * rockFrames;
            int cellsPerPage = req.Columns * req.MaxRowsPerPage;
            int pageCount = Mathf.CeilToInt(cellsTotal / (float)cellsPerPage);

            var renderClock = new Stopwatch();
            string outAbs = Path.Combine(RigCatalog.RepoRoot, req.OutputFolder);
            Directory.CreateDirectory(outAbs);

            for (int page = 0; page < pageCount; page++)
            {
                int firstCell = page * cellsPerPage;
                int cellsHere = Mathf.Min(cellsPerPage, cellsTotal - firstCell);
                int rows = Mathf.CeilToInt(cellsHere / (float)req.Columns);
                int pw = req.Columns * geo.Width;
                int ph = rows * geo.Height;

                if (pw > MaxTextureSize || ph > MaxTextureSize)
                    throw new InvalidOperationException(
                        $"Page {page} would be {pw}×{ph}, over the {MaxTextureSize} cap. Unity would " +
                        "import it DOWNSCALED and the sprite count would still match, so this would " +
                        "only surface as a cell-size or pivot failure much later. Reduce " +
                        $"{nameof(BakeRequest.MaxRowsPerPage)} or {nameof(BakeRequest.Columns)}.");

                var pixels = new Color32[pw * ph];

                for (int i = 0; i < cellsHere; i++)
                {
                    int flat = firstCell + i;
                    int facing = flat / rockFrames;
                    int frame = flat % rockFrames;
                    double dir = DirForCell(facing, req.Facings, probe.Convention);
                    string d = dir.ToString("R", CultureInfo.InvariantCulture);

                    string expr = withRock
                        ? $"(function(){{var r={g}.rock({frame},{rockFrames});" +
                          $"return {g}.render({d},{{roll:r.roll,pitch:r.pitch,heave:r.heave}});}})()"
                        : $"{g}.render({d},{{}})";

                    renderClock.Start();
                    byte[] rgba = host.EvaluateBytes(expr);
                    renderClock.Stop();
                    result.CellsRendered++;

                    if (rgba.Length != geo.Width * geo.Height * 4)
                        throw new InvalidOperationException(
                            $"Cell {flat} came back {rgba.Length} bytes, expected " +
                            $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");

                    Blit(rgba, geo.Width, geo.Height, pixels, pw, ph,
                         col: i % req.Columns, rowFromTop: i / req.Columns);
                }

                string fileName = pageCount == 1
                    ? $"{req.BaseName}.png"
                    : $"{req.BaseName}{page}.png";
                string assetPath = $"{req.OutputFolder}/{fileName}";

                var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
                try
                {
                    tex.SetPixels32(pixels);
                    tex.Apply(false, false);
                    byte[] png = tex.EncodeToPNG();
                    string abs = Path.Combine(RigCatalog.RepoRoot, assetPath);
                    File.WriteAllBytes(abs, png);
                    result.TotalPngBytes += png.Length;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                result.Pages.Add(new BakePage
                {
                    AssetPath = assetPath, Width = pw, Height = ph,
                    Columns = req.Columns, Rows = rows, CellCount = cellsHere,
                });
            }

            // ---- Anchors ----
            result.AnchorJsonPath = WriteAnchors(host, entry, geo, req, probe.Convention, rockFrames, withRock);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// Copies one rig cell into a page. Both the rig buffer and the sheet layout are
        /// TOP-LEFT-origin row-major (cell 0 top-left, matching SpriteSheetSlicer.BuildRects);
        /// Unity's Color32 array is BOTTOM-LEFT-origin, so the row index is flipped exactly once,
        /// here. Flipping it twice, or not at all, produces a sheet that looks plausible and is
        /// vertically mirrored — check this first if a bake comes out upside-down.
        /// </summary>
        internal static void Blit(byte[] src, int cw, int ch, Color32[] dst, int pw, int ph,
                                  int col, int rowFromTop)
        {
            int x0 = col * cw;
            int yTop = rowFromTop * ch;
            for (int y = 0; y < ch; y++)
            {
                int imageY = yTop + y;
                int unityY = ph - 1 - imageY;
                int dstRow = unityY * pw + x0;
                int srcRow = y * cw * 4;
                for (int x = 0; x < cw; x++)
                {
                    int s = srcRow + x * 4;
                    dst[dstRow + x] = new Color32(src[s], src[s + 1], src[s + 2], src[s + 3]);
                }
            }
        }

        /// <summary>
        /// Anchor names the baker will emit if the rig exposes them. These are LIVE PROJECTIONS —
        /// they ride the wave, so they are evaluated per facing AND per rock frame rather than once
        /// per facing. Anything not present on a given rig is simply skipped: the lobster boat is
        /// inboard-diesel and has no motorMount or tillerGrip at all.
        /// </summary>
        static readonly string[] AnchorFunctions =
        {
            "motorMount", "tubMounts", "helmSeat", "tillerGrip",
            "haulerMount", "navMounts", "pilotStand",
        };

        static string WriteAnchors(IRigScriptHost host, in RigEntry entry, in RigGeometry geo,
                                   in BakeRequest req, AzimuthConvention convention,
                                   int rockFrames, bool withRock)
        {
            string g = entry.GlobalName;
            var present = new List<string>();
            foreach (var fn in AnchorFunctions)
                if (host.EvaluateBool($"typeof {g}.{fn} === 'function'"))
                    present.Add(fn);

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"rig\": \"{entry.ScriptPath}\",\n");
            sb.Append($"  \"global\": \"{g}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(geo.PivotX)}, \"y\": {Num(geo.PivotY)} }},\n");
            sb.Append($"  \"facings\": {req.Facings},\n");
            sb.Append($"  \"rockFrames\": {req.RockFrames},\n");
            // Recorded so a consumer never has to re-derive it, and so the correction is auditable.
            sb.Append($"  \"measuredRigConvention\": \"{convention}\",\n");
            sb.Append("  \"facingsAreCounterClockwise\": false,\n");
            sb.Append("  \"_note\": \"Baked in-engine with the rig's measured convention applied, so these cells are genuinely clockwise: cell k depicts heading 360*k/facings.\",\n");
            sb.Append("  \"anchors\": {\n");

            for (int a = 0; a < present.Count; a++)
            {
                sb.Append($"    \"{present[a]}\": [\n");
                for (int f = 0; f < req.Facings; f++)
                {
                    double dir = DirForCell(f, req.Facings, convention);
                    string d = dir.ToString("R", CultureInfo.InvariantCulture);
                    sb.Append("      [");
                    for (int r = 0; r < rockFrames; r++)
                    {
                        string opts = withRock
                            ? $"(function(){{var q={g}.rock({r},{rockFrames});" +
                              $"return {{roll:q.roll,pitch:q.pitch,heave:q.heave}};}})()"
                            : "{}";
                        string json = host.EvaluateString(
                            $"JSON.stringify({g}.{present[a]}({d},{opts}))");
                        sb.Append(json);
                        if (r < rockFrames - 1) sb.Append(", ");
                    }
                    sb.Append(f < req.Facings - 1 ? "],\n" : "]\n");
                }
                sb.Append(a < present.Count - 1 ? "    ],\n" : "    ]\n");
            }

            sb.Append("  }\n}\n");

            string assetPath = $"{req.OutputFolder}/{req.BaseName}Anchors.json";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, assetPath), sb.ToString());
            return assetPath;
        }

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The one line to change if the ~96 MB native V8 tax ever stops being worth it: implement
    /// <see cref="IRigScriptHost"/> over Jint and return it here. ADR 0021 keeps Jint's measured
    /// numbers so that swap needs no new spike.
    /// </summary>
    public static class RigScriptHostFactory
    {
        public static IRigScriptHost Create() => new V8RigScriptHost();
    }
}
