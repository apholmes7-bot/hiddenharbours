using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One surface's baked blob-47 atlas.</summary>
    public sealed class RoadSheetBake
    {
        public string Surface, AssetPath;
        public int Width, Height, Tiles;
        public long PngBytes;

        public override string ToString() =>
            $"{Surface}  {Width}×{Height}  {Tiles} tiles  {PngBytes / 1024.0:F1} KiB";
    }

    /// <summary>The whole run.</summary>
    public sealed class RoadKitBakeResult
    {
        public readonly List<RoadSheetBake> Sheets = new List<RoadSheetBake>();
        public long TotalPngBytes;
        public double RenderMs, TotalMs;
        public string ContractPath;

        public override string ToString() =>
            $"{Sheets.Count} sheets, {TotalPngBytes / 1024.0:F1} KiB, " +
            $"render {RenderMs:F0} ms of {TotalMs:F0} ms";
    }

    /// <summary>
    /// Bakes the ROAD KIT v3's eleven blob-47 atlases by running
    /// <c>docs/art/rigs/road-path-kit-v3/roadPathRig3.js</c> UNMODIFIED in the repo's own ClearScript
    /// V8 (ADR 0021). One 384×256 sheet per surface: 12 × 4 cells of 32×64, 47 tiles + one padding cell.
    ///
    /// <para><b>⭐ WHY THE PIXELS ARE BAKED HERE AND NOT IMPORTED FROM THE DROP.</b> The drop ships
    /// eleven reference atlases and they are the oracle this baker is checked against — but they are
    /// not what should ship. MEASURED: every fully-opaque pixel of the reference sheets matches the
    /// rig exactly, and <b>every single partial-alpha pixel differs</b> (381 of 381 on the dirt sheet),
    /// each explained to the byte by a premultiply→unpremultiply round-trip. The reference PNGs went
    /// out through a browser canvas, which stores premultiplied alpha and cannot round-trip a shadow
    /// pixel losslessly. This kit's contact shadows ARE partial alpha, by design (ShoreIso2 v8
    /// occlusion canon), so importing the drop's sheets would ship a quietly degraded version of the
    /// art. Baking from the rig gives the lossless buffer.</para>
    ///
    /// <para><b>⚠ The reference sheets remain the oracle, under a round-trip-aware comparison</b> —
    /// see <c>RoadKitBakeTests</c>. That comparison is TIGHT, not tolerant: an opaque pixel must be
    /// exactly equal and a partial-alpha pixel must round-trip to exactly the reference byte. It is
    /// not a fuzz threshold and must never be relaxed into one.</para>
    ///
    /// <para><b>⭐ NO <see cref="RigCatalog"/> ENTRY, deliberately</b> — that struct is built around
    /// <c>AzimuthConvention</c> for 8-direction turntables and a road tile has no heading. Same call
    /// and same reason as <c>ShorePlantBaker</c> and <c>GrassLibraryBaker</c>; nothing here calls
    /// <c>RigBaker.DirForCell</c>, and no azimuth probe can be run against a static tile.</para>
    ///
    /// <para><b>ZERO WATER</b>, the standing rule for this tile family (ADR 0010/0012/0023): the
    /// shader owns the waterline, foam and swash, so there is no wet edge to line up and none is baked.</para>
    /// </summary>
    public static class RoadKitSheetBaker
    {
        const string ResultGlobal = "__hhRoadCell";
        const string G = RoadKitV3.RigGlobalName;

        /// <summary>
        /// Bake every surface. Returns what was written; throws rather than writing a partial or
        /// wrong-geometry kit.
        /// </summary>
        public static RoadKitBakeResult BakeAll(Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var renderClock = new Stopwatch();
            var result = new RoadKitBakeResult();

            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, RoadKitV3.RoadsDir));

            using (IRigScriptHost host = new V8RigScriptHost())
            {
                LoadRig(host);
                AssertRigGeometry(host);
                AssertRigSurfaces(host);
                result.ContractPath = WriteContract(host);

                for (int s = 0; s < RoadKitV3.Surfaces.Length; s++)
                {
                    string surface = RoadKitV3.Surfaces[s];
                    progress?.Invoke(surface, (float)s / RoadKitV3.Surfaces.Length);

                    renderClock.Start();
                    byte[] atlas = RenderAtlas(host, surface);
                    renderClock.Stop();

                    string assetPath = RoadKitV3.AtlasPath(surface);
                    var tex = new Texture2D(RoadKitV3.SheetWidth, RoadKitV3.SheetHeight,
                                            TextureFormat.RGBA32, mipChain: false, linear: false);
                    try
                    {
                        tex.LoadRawTextureData(
                            FlipRows(atlas, RoadKitV3.SheetWidth, RoadKitV3.SheetHeight));
                        tex.Apply(false, false);
                        byte[] png = tex.EncodeToPNG();
                        File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);

                        result.TotalPngBytes += png.Length;
                        result.Sheets.Add(new RoadSheetBake
                        {
                            Surface = surface, AssetPath = assetPath,
                            Width = RoadKitV3.SheetWidth, Height = RoadKitV3.SheetHeight,
                            Tiles = RoadKitV3.BlobCount, PngBytes = png.Length,
                        });
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tex); }
                }
            }

            result.RenderMs = renderClock.Elapsed.TotalMilliseconds;
            result.TotalMs = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// Write the kit's contract: the 47 neighbour masks in ATLAS ORDER, with each cell's label and
        /// axis, plus the geometry and the solved bake parameters.
        ///
        /// <para><b>⭐ Why this file has to exist.</b> A placement pass has to turn "which of my
        /// neighbours are road?" into an atlas index, and that mapping is the rig's — <c>canon()</c>
        /// folds the diagonal bits that cannot apply, and <c>BLOB47</c> is the surviving 47 sorted by
        /// mask. Re-deriving that in C# would be a second transcription of the art director's maths,
        /// which is how a blob index goes quietly wrong: the tile draws perfectly on its own and tears
        /// at every junction. So the mapping is EXPORTED from the rig at bake time and read back, and
        /// C# never computes it.</para>
        /// </summary>
        public static string WriteContract(IRigScriptHost host)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"kit\": \"road-path-kit-v3\",");
            sb.AppendLine($"  \"rig\": \"{RoadKitV3.RigScriptPath}\",");
            sb.AppendLine($"  \"derivedFromRigSha256\": \"{RoadKitV3.RigSha256Lf}\",");
            sb.AppendLine($"  \"tile\": {RoadKitV3.Tile}, \"cellH\": {RoadKitV3.CellH}, " +
                          $"\"head\": {RoadKitV3.Head}, \"skirt\": {RoadKitV3.Skirt},");
            sb.AppendLine($"  \"zScale\": {host.EvaluateNumber($"{G}.ZS")},");
            sb.AppendLine($"  \"cols\": {RoadKitV3.Cols}, \"rows\": {RoadKitV3.Rows}, " +
                          $"\"blobCount\": {RoadKitV3.BlobCount},");
            sb.AppendLine($"  \"bake\": {{ \"wear\": \"{RoadKitV3.BakedWear}\", " +
                          $"\"profile\": \"{RoadKitV3.BakedProfile}\", " +
                          $"\"piece\": \"{RoadKitV3.BakedPiece}\", " +
                          $"\"ground\": \"{RoadKitV3.BakedGround}\", " +
                          $"\"seed\": {RoadKitV3.BakedSeed}, " +
                          $"\"patternStep\": {RoadKitV3.PatternStep} }},");
            sb.AppendLine("  \"tiles\": [");

            for (int i = 0; i < RoadKitV3.BlobCount; i++)
            {
                int mask = (int)host.EvaluateNumber($"{G}.BLOB47[{i}].mask");
                string label = host.EvaluateString($"{G}.BLOB47[{i}].label");
                string axis = host.EvaluateString($"{G}.BLOB47[{i}].axis");
                string comma = i == RoadKitV3.BlobCount - 1 ? "" : ",";
                sb.AppendLine($"    {{ \"index\": {i,2}, \"mask\": {mask,3}, " +
                              $"\"label\": \"{label}\", \"axis\": \"{axis}\" }}{comma}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            string path = Path.Combine(RigCatalog.RepoRoot, RoadKitV3.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            return RoadKitV3.ContractPath;
        }

        /// <summary>
        /// Render one surface's 384×256 atlas, top-down RGBA — the same buffer order the rig hands back.
        /// </summary>
        public static byte[] RenderAtlas(IRigScriptHost host, string surface)
        {
            var atlas = new byte[RoadKitV3.SheetWidth * RoadKitV3.SheetHeight * 4];
            int cellStride = RoadKitV3.Tile * 4;

            for (int i = 0; i < RoadKitV3.BlobCount; i++)
            {
                host.Execute($"globalThis.{ResultGlobal} = {CellExpression(surface, i)};");
                int w = (int)host.EvaluateNumber($"{ResultGlobal}.w");
                int h = (int)host.EvaluateNumber($"{ResultGlobal}.h");
                if (w != RoadKitV3.Tile || h != RoadKitV3.CellH)
                    throw new InvalidOperationException(
                        $"[RoadKitSheetBaker] '{surface}' cell {i} came back {w}×{h}, expected " +
                        $"{RoadKitV3.Tile}×{RoadKitV3.CellH}. The rig's cell geometry moved — do not " +
                        "re-slice around it; the contract in RoadKitV3 and the art director's file " +
                        "have to be reconciled first.");

                byte[] cell = host.EvaluateBytes($"{ResultGlobal}.data");
                int ox = RoadKitV3.ColOf(i) * RoadKitV3.Tile;
                int oy = RoadKitV3.RowOf(i) * RoadKitV3.CellH;
                for (int y = 0; y < RoadKitV3.CellH; y++)
                    Buffer.BlockCopy(cell, y * cellStride, atlas,
                                     ((oy + y) * RoadKitV3.SheetWidth + ox) * 4, cellStride);
            }
            return atlas;
        }

        /// <summary>
        /// The one call that turns a blob index into pixels — SOLVED against the reference sheets, not
        /// transcribed from the README. Every argument here is load-bearing; see <see cref="RoadKitV3"/>
        /// for what each was measured to be worth.
        ///
        /// <para>⚠️ <c>axis</c> is passed EXPLICITLY from the mask. <c>render()</c> will happily derive
        /// one when it is omitted, and the two disagree on the isolated and cap cells — 628 opaque
        /// pixels across the eleven sheets, silent.</para>
        /// </summary>
        public static string CellExpression(string surface, int i)
        {
            string axis = RoadKitV3.PassAxisFromMask ? $" o.axis = b.axis;" : string.Empty;
            return $@"(function(){{
                var b = {G}.BLOB47[{i}];
                var o = {{ con:b.con, diag:b.diag,
                          wear:{Js(RoadKitV3.BakedWear)}, profile:{Js(RoadKitV3.BakedProfile)},
                          piece:{Js(RoadKitV3.BakedPiece)}, markings:[],
                          ground:{Js(RoadKitV3.BakedGround)},
                          gx:{RoadKitV3.PatternX(i)}, gy:{RoadKitV3.PatternY(i)},
                          seed:{RoadKitV3.BakedSeed} }};{axis}
                return {G}.render({Js(surface)}, o);
            }})()";
        }

        // ---- rig loading + guards -------------------------------------------------------------------

        /// <summary>
        /// Runs the art director's file unmodified and proves the global appeared. A rig that fails to
        /// install does not throw on its own — the next call just finds <c>undefined</c> and the kit
        /// bakes nothing, or worse, something.
        /// </summary>
        public static void LoadRig(IRigScriptHost host)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, RoadKitV3.RigScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"[RoadKitSheetBaker] rig missing at '{RoadKitV3.RigScriptPath}'.", full);

            host.Execute(File.ReadAllText(full));
            if (!host.EvaluateBool($"typeof globalThis.{G} === 'object' && globalThis.{G} !== null"))
                throw new InvalidOperationException(
                    $"[RoadKitSheetBaker] '{RoadKitV3.RigScriptPath}' ran but did not install " +
                    $"globalThis.{G}.");
        }

        /// <summary>
        /// Cross-check the rig's OWN geometry against <see cref="RoadKitV3"/>. ADR 0021 §4: cell
        /// geometry comes from the rig, not from a README — so this is where a rig that re-based its
        /// vertical (as v3 did to v2, 12 → 24.5 px/m) fails loudly instead of baking sheets that no
        /// longer match the pivots and the slicer.
        /// </summary>
        public static void AssertRigGeometry(IRigScriptHost host)
        {
            Check("TILE", RoadKitV3.Tile);
            Check("CELL_H", RoadKitV3.CellH);
            Check("HEAD", RoadKitV3.Head);
            Check("SKIRT", RoadKitV3.Skirt);

            double zs = host.EvaluateNumber($"{G}.ZS");
            if (Math.Abs(zs - RoadKitV3.ZScale) > 1e-6)
                throw new InvalidOperationException(
                    $"[RoadKitSheetBaker] rig ZS is {zs}, contract says {RoadKitV3.ZScale}. The kit's " +
                    "vertical ruler moved: every face height, kerb and drop in the placement pass is " +
                    "measured against it.");

            int blob = (int)host.EvaluateNumber($"{G}.BLOB47.length");
            if (blob != RoadKitV3.BlobCount)
                throw new InvalidOperationException(
                    $"[RoadKitSheetBaker] rig BLOB47 has {blob} entries, contract says " +
                    $"{RoadKitV3.BlobCount}.");

            void Check(string name, int expected)
            {
                int got = (int)host.EvaluateNumber($"{G}.{name}");
                if (got != expected)
                    throw new InvalidOperationException(
                        $"[RoadKitSheetBaker] rig {name} is {got}, contract says {expected}.");
            }
        }

        /// <summary>
        /// The surface table is the rig's, not ours. A rig that gains or reorders a surface must fail
        /// here rather than bake ten sheets against a stale eleven-name list.
        /// </summary>
        public static void AssertRigSurfaces(IRigScriptHost host)
        {
            int n = (int)host.EvaluateNumber($"{G}.SURFACES.length");
            if (n != RoadKitV3.Surfaces.Length)
                throw new InvalidOperationException(
                    $"[RoadKitSheetBaker] rig ships {n} surfaces, contract lists " +
                    $"{RoadKitV3.Surfaces.Length}.");

            for (int i = 0; i < n; i++)
            {
                string got = host.EvaluateString($"{G}.SURFACES[{i}]");
                if (!string.Equals(got, RoadKitV3.Surfaces[i], StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"[RoadKitSheetBaker] rig surface {i} is '{got}', contract says " +
                        $"'{RoadKitV3.Surfaces[i]}'.");
            }
        }

        // ---- helpers ---------------------------------------------------------------------------------

        /// <summary>
        /// The rig hands back a TOP-DOWN buffer; <see cref="Texture2D.LoadRawTextureData(byte[])"/>
        /// reads BOTTOM-UP. One flip, the same one <c>GrassLibraryBaker</c> documents, so the answer is
        /// not re-derived per baker.
        /// </summary>
        public static byte[] FlipRows(byte[] rgba, int w, int h)
        {
            var flipped = new byte[rgba.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(rgba, y * stride, flipped, (h - 1 - y) * stride, stride);
            return flipped;
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
