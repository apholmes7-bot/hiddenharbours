using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One baked PNG, for the report the menu prints.</summary>
    public sealed class CliffAssetBake
    {
        public string Name, AssetPath;
        public CliffAssetKind Kind;
        public int Width, Height;
        public long PngBytes;

        public override string ToString() =>
            $"{Kind,-7} {Name,-28} {Width}×{Height}  {PngBytes / 1024.0:F1} KiB";
    }

    public sealed class CliffBakeResult
    {
        public string EngineName;
        public readonly List<CliffAssetBake> Assets = new List<CliffAssetBake>();
        public double RenderMilliseconds, TotalMilliseconds;
        public long TotalPngBytes;
        public int FaceCount, StripCount, LedgeCount, ProfileCount;
    }

    /// <summary>
    /// Bakes the <b>Cliff Face &amp; Terrain Ledge kit v10</b> from
    /// <c>docs/art/rigs/cliff-face-kit/bake/cliffRig.js</c> into <see cref="CliffCatalog.BakeRoot"/> —
    /// faces, brow/toe decals, iso ledge sheets and the plan-displacement profiles.
    ///
    /// <para><b>⭐ THIS BAKER'S OUTPUT IS NOT COMMITTED, AND THAT INVERTS THE SIBLING PATTERN.</b> Every
    /// other kit in the repo commits its sheets. This one cannot: the kit is parametric and the numbers
    /// are brutal — the FULL set is 720 face PNGs (the rig's README: ~90 MB), and even the narrow St
    /// Peters subset this baker ships by default measured <b>16.0 MB across 63 PNGs</b>. Sixteen
    /// megabytes of LFS binaries that go stale the moment a coefficient moves, to pin bytes that are
    /// really just the PNG encoder's, is a bad trade. So: the RIG is committed, this baker is committed,
    /// <c>CliffRigBakeTests</c> drives the rig through V8 on every CI run, and the pixels are a menu
    /// item away whenever anyone needs them. <see cref="CliffCatalog.BakeRoot"/> is gitignored.</para>
    ///
    /// <para><b>What is baked by default, and why so little of it.</b> St Peters is a red sandstone
    /// coast, so <see cref="DefaultRock"/> is the only rock. All five aspects, because the aspect law IS
    /// the look. Three batters — the 90° wall the owner asked for, plus <c>steep</c> and <c>ramp</c> so a
    /// cliff run can transition into a beach instead of stepping. One wear step, because a wear ladder is
    /// a painted channel and nothing paints it yet. Three of the four channels, because this project
    /// lights its walls LIVE (see <see cref="CliffCatalog.LiveChannels"/>) — the pre-lit albedo is dead
    /// weight here and the README forbids mixing the two paths on one wall.</para>
    ///
    /// <para>Like every sibling baker this stops at "PNGs on disk"; the import contract is
    /// <see cref="ApplyImportSettings"/>'s (README §6 — Repeat/Point on the faces, linear on the data
    /// channels, bilinear on the profile because it is geometry rather than pixels).</para>
    /// </summary>
    public static class CliffBaker
    {
        /// <summary>The rock St Peters is made of — "red sandstone cliffs" in the region docs.</summary>
        public const string DefaultRock = "sandstone";

        /// <summary>The batters baked by default, as indices into <see cref="CliffCatalog.Batters"/>:
        /// wall (90°), steep (76°), ramp (62°). <c>bank</c> (48°) is omitted — a 48° slope is not a
        /// cliff, and the owner's complaint is precisely that the current coast reads as one.</summary>
        public static readonly int[] DefaultBatters = { 0, 1, 2 };

        public static string DefaultOutputFolder => CliffCatalog.BakeRoot;

        /// <summary>The JS global one render is parked on, so its four channels are read without
        /// rendering four times — a face set costs ~830 ms, so this is not a micro-optimisation.</summary>
        const string ResultGlobal = "__hhCliffBake";
        const string G = CliffCatalog.RigGlobalName;

        // =====================================================================================
        //  the bake
        // =====================================================================================

        public static CliffBakeResult BakeAll(string outputFolder = null, string rock = null,
                                              Action<string, float> progress = null)
        {
            outputFolder ??= DefaultOutputFolder;
            rock ??= DefaultRock;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);

            var result = new CliffBakeResult { EngineName = host.EngineName };
            var renderClock = new Stopwatch();

            foreach (CliffAssetKind kind in new[] { CliffAssetKind.Face, CliffAssetKind.Strip,
                                                    CliffAssetKind.Ledge, CliffAssetKind.Profile })
                Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, SubFolder(outputFolder, kind)));

            BakeFaces(host, result, renderClock, outputFolder, rock, progress);
            BakeStrips(host, result, renderClock, outputFolder, progress);
            BakeLedges(host, result, renderClock, outputFolder, rock, progress);
            BakeProfiles(host, result, renderClock, outputFolder, rock, progress);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        static void BakeFaces(IRigScriptHost host, CliffBakeResult result, Stopwatch clock,
                              string outputFolder, string rock, Action<string, float> progress)
        {
            int done = 0, todo = CliffCatalog.Aspects.Length * DefaultBatters.Length;

            foreach (int batter in DefaultBatters)
            {
                string slope = CliffCatalog.Batters[batter];
                foreach (string aspect in CliffCatalog.Aspects)
                {
                    progress?.Invoke($"face {rock} {aspect} {slope}", (float)done++ / todo);

                    clock.Start();
                    host.Execute($"globalThis.{ResultGlobal} = {G}.face({Js(rock)}, {Js(aspect)}, " +
                                 $"{CliffCatalog.BaseStep}, {{slope: {Js(slope)}}});");
                    int w = (int)host.EvaluateNumber($"{ResultGlobal}.W");
                    int h = (int)host.EvaluateNumber($"{ResultGlobal}.H");
                    clock.Stop();

                    AssertFaceCanvas(rock, aspect, slope, w, h);

                    // Three reads off ONE render — the channels are co-registered by construction, and
                    // re-rendering per channel would both cost 3× and risk them drifting apart.
                    foreach (string channel in CliffCatalog.LiveChannels)
                    {
                        byte[] rgba = host.EvaluateBytes($"{ResultGlobal}.{ChannelField(channel)}");
                        AssertChannel(rock, aspect, slope, channel, w, h, rgba);

                        string name = CliffCatalog.FaceName(rock, aspect, batter,
                                                            CliffCatalog.BaseStep, channel);
                        Write(result, CliffAssetKind.Face, name, outputFolder, rgba, w, h);
                    }
                    result.FaceCount++;
                }
            }
        }

        static void BakeStrips(IRigScriptHost host, CliffBakeResult result, Stopwatch clock,
                               string outputFolder, Action<string, float> progress)
        {
            foreach (string aspect in CliffCatalog.Aspects)
            {
                progress?.Invoke($"brow/toe {aspect}", 0.6f);

                clock.Start();
                host.Execute($"globalThis.{ResultGlobal} = {G}.brow({Js(aspect)}, {CliffCatalog.BaseStep});");
                byte[] brow = ReadStrip(host, out int bw, out int bh);
                clock.Stop();
                AssertStripCanvas($"brow {aspect}", bw, bh, brow);
                Write(result, CliffAssetKind.Strip, "Brow_" + CliffCatalog.BrowName(aspect, CliffCatalog.BaseStep),
                      outputFolder, brow, bw, bh);

                // Both toe features, because the batter chooses between them per sector: a wall is
                // undercut into a notch, a ramp keeps its debris and slumps
                // (CliffCatalog.ToeFeatureForBatter). Baking one would tie the coast to one batter.
                foreach (string feature in new[] { "notch", "slump" })
                {
                    clock.Start();
                    string opts = feature == "notch" ? "{}" : $"{{feature: {Js(feature)}}}";
                    host.Execute($"globalThis.{ResultGlobal} = " +
                                 $"{G}.toe({Js(aspect)}, {CliffCatalog.BaseStep}, {opts});");
                    byte[] toe = ReadStrip(host, out int tw, out int th);
                    clock.Stop();
                    AssertStripCanvas($"toe {aspect} {feature}", tw, th, toe);
                    Write(result, CliffAssetKind.Strip,
                          "Toe_" + CliffCatalog.ToeName(aspect, CliffCatalog.BaseStep, feature),
                          outputFolder, toe, tw, th);
                }
                result.StripCount += 3;
            }
        }

        /// <summary>
        /// The ledge sheet: 12 pieces across × 3 bands down, of 32 px cells — the vocabulary a walkable
        /// bench at the cliff's foot is built from.
        ///
        /// <para><b>⚠ <c>gx</c>/<c>gy</c> are the cell's place in a CONTINUOUS noise field, not a
        /// variant roll.</b> They are passed as the sheet's own column and row so adjacent cells butt
        /// the way the rig drew them; hashing them instead would put a visible seam on every cell
        /// boundary — the same trap <c>StPetersShoreMap.GroundVariant</c> documents for the shoreline
        /// kit's four ground columns.</para>
        /// </summary>
        static void BakeLedges(IRigScriptHost host, CliffBakeResult result, Stopwatch clock,
                               string outputFolder, string rock, Action<string, float> progress)
        {
            progress?.Invoke($"ledges {rock}", 0.8f);

            var sheet = new byte[CliffCatalog.LedgeSheetWidth * CliffCatalog.LedgeSheetHeight * 4];
            for (int row = 0; row < CliffCatalog.LedgeBands.Length; row++)
            {
                string band = CliffCatalog.LedgeBands[row];
                for (int col = 0; col < CliffCatalog.LedgePieces.Length; col++)
                {
                    clock.Start();
                    host.Execute($"globalThis.{ResultGlobal} = {G}.ledge({Js(rock)}, " +
                                 $"{Js(CliffCatalog.LedgePieces[col])}, {{band: {Js(band)}, " +
                                 $"gx: {col}, gy: {row}, step: {CliffCatalog.BaseStep}}});");
                    // ⚠ ledge()/contact() return LOWERCASE w/h where face()/brow()/toe() return W/H.
                    // The kit manifest does not say so; the rig does. Pinned by CliffRigBakeTests.
                    int cw = (int)host.EvaluateNumber($"{ResultGlobal}.w");
                    int ch = (int)host.EvaluateNumber($"{ResultGlobal}.h");
                    byte[] cell = host.EvaluateBytes($"{ResultGlobal}.data");
                    clock.Stop();

                    AssertCell($"ledge {rock} {CliffCatalog.LedgePieces[col]} {band}", cw, ch, cell);
                    Blit(cell, cw, ch, sheet, CliffCatalog.LedgeSheetWidth,
                         col * CliffCatalog.LedgeCell, row * CliffCatalog.LedgeCell);
                }
            }
            Write(result, CliffAssetKind.Ledge,
                  CliffCatalog.LedgeSheetName(rock, CliffCatalog.BaseStep), outputFolder,
                  sheet, CliffCatalog.LedgeSheetWidth, CliffCatalog.LedgeSheetHeight);

            var contact = new byte[CliffCatalog.ContactSheetWidth * CliffCatalog.ContactSheetHeight * 4];
            for (int col = 0; col < CliffCatalog.ContactPieces.Length; col++)
            {
                clock.Start();
                host.Execute($"globalThis.{ResultGlobal} = {G}.contact({Js(CliffCatalog.ContactPieces[col])});");
                int cw = (int)host.EvaluateNumber($"{ResultGlobal}.w");
                int ch = (int)host.EvaluateNumber($"{ResultGlobal}.h");
                byte[] cell = host.EvaluateBytes($"{ResultGlobal}.data");
                clock.Stop();

                AssertCell($"contact {CliffCatalog.ContactPieces[col]}", cw, ch, cell);
                Blit(cell, cw, ch, contact, CliffCatalog.ContactSheetWidth,
                     col * CliffCatalog.LedgeCell, 0);
            }
            Write(result, CliffAssetKind.Ledge, "Contact", outputFolder,
                  contact, CliffCatalog.ContactSheetWidth, CliffCatalog.ContactSheetHeight);
            result.LedgeCount += 2;
        }

        /// <summary>
        /// The plan-displacement profiles — one per batter, aspect- and wear-independent.
        ///
        /// <para><b>⚠ THE RIG HANDS BACK FLOATS IN METRES; THE GREY ENCODING IS THIS BAKER'S.</b>
        /// <c>profile().disp</c> is a float array (measured −0.61 … +0.69 m on this drop), NOT the
        /// 0..255 image the README describes — that describes the FILE. Encoding is
        /// <c>128 + disp/1.15 × 127</c> so the shader's documented decode,
        /// <c>(sample − 0.5) × 2 × 1.15</c>, returns the metres the rig meant. Get this backwards and
        /// the wall's silhouette is displaced by a silently wrong scale, which reads as "the profile
        /// does nothing" rather than as an error.</para>
        /// </summary>
        static void BakeProfiles(IRigScriptHost host, CliffBakeResult result, Stopwatch clock,
                                 string outputFolder, string rock, Action<string, float> progress)
        {
            foreach (int batter in DefaultBatters)
            {
                float angle = CliffCatalog.BatterAngles[batter];
                progress?.Invoke($"profile {rock} {angle:F0}deg", 0.9f);

                clock.Start();
                // Aspect is passed because the API takes it, and 'S' is the canonical one — the rig
                // ignores it for the profile, which CliffRigBakeTests asserts rather than assumes.
                host.Execute($"globalThis.{ResultGlobal} = " +
                             $"{G}.profile({Js(rock)}, 'S', {{slope: {angle}}});");
                int w = (int)host.EvaluateNumber($"{ResultGlobal}.W");
                int h = (int)host.EvaluateNumber($"{ResultGlobal}.H");
                float metres = (float)host.EvaluateNumber($"{ResultGlobal}.metres");
                // The disp array is floats, so it cannot come back through EvaluateBytes — encode it
                // to the grey image in JS, where the numbers still are what the rig produced.
                host.Execute(
                    $"globalThis.{ResultGlobal}.__png = (function(p){{" +
                    $"  var o = new Uint8ClampedArray(p.W * p.H * 4);" +
                    $"  for (var i = 0; i < p.disp.length; i++) {{" +
                    $"    var v = Math.round(128 + p.disp[i] / p.metres * 127);" +
                    $"    v = v < 0 ? 0 : (v > 255 ? 255 : v);" +
                    $"    o[i*4] = v; o[i*4+1] = v; o[i*4+2] = v; o[i*4+3] = 255;" +
                    $"  }} return o; }})(globalThis.{ResultGlobal});");
                byte[] grey = host.EvaluateBytes($"{ResultGlobal}.__png");
                clock.Stop();

                if (!Mathf.Approximately(metres, CliffCatalog.ProfileMetres))
                    throw new InvalidOperationException(
                        $"[cliff-baker] profile {rock} {angle:F0}°: the rig encodes ±{metres} m but " +
                        $"CliffCatalog.ProfileMetres says ±{CliffCatalog.ProfileMetres}. The shader " +
                        "decodes with the catalog's number, so a drift here silently rescales every " +
                        "displaced vertex.");
                AssertChannel(rock, "-", $"{angle:F0}deg", "profile", w, h, grey);

                Write(result, CliffAssetKind.Profile, CliffCatalog.ProfileName(rock, batter),
                      outputFolder, grey, w, h);
                result.ProfileCount++;
            }
        }

        // =====================================================================================
        //  the rig
        // =====================================================================================

        /// <summary>
        /// Loads <c>cliffRig.js</c> and asserts the global + the whole public API up front, rather than
        /// discovering a renamed export as a confusing type error on face 40 of 45.
        /// </summary>
        public static void InstallRig(IRigScriptHost host)
        {
            string path = Path.Combine(RigCatalog.RepoRoot, CliffCatalog.RigScriptPath);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Cliff rig source missing at {path}. The kit is committed under " +
                    "docs/art/rigs/cliff-face-kit/ — if this fired, the branch predates that import.",
                    path);
            host.Execute(File.ReadAllText(path));

            if (!host.EvaluateBool($"typeof {G} === 'object' && {G} !== null"))
                throw new InvalidOperationException(
                    $"'{CliffCatalog.RigScriptPath}' ran but did not install globalThis.{G} " +
                    "— the rig changed shape.");

            foreach (string fn in new[] { "face", "profile", "brow", "toe", "ledge", "contact" })
                if (!host.EvaluateBool($"typeof {G}.{fn} === 'function'"))
                    throw new InvalidOperationException(
                        $"{G}.{fn}() is missing — this baker calls the rig's public API directly " +
                        "(no shim), so a renamed export must fail loudly here.");

            foreach (string arr in new[] { "ROCKS", "ASPECTS", "PIECES", "BANDS", "CPIECES" })
                if (!host.EvaluateBool($"Array.isArray({G}.{arr})"))
                    throw new InvalidOperationException($"{G}.{arr} is missing — the rig changed shape.");

            if (!host.EvaluateBool($"typeof {G}.SLOPES === 'object' && {G}.SLOPES !== null"))
                throw new InvalidOperationException($"{G}.SLOPES is missing — the rig changed shape.");
        }

        /// <summary>The rig's field name for a channel: <c>_unlit</c> → <c>unlit</c>, and the pre-lit
        /// albedo (empty suffix) → <c>data</c>.</summary>
        static string ChannelField(string channelSuffix) =>
            string.IsNullOrEmpty(channelSuffix) ? "data" : channelSuffix.TrimStart('_');

        /// <summary>
        /// Reads the brow/toe strip parked on <see cref="ResultGlobal"/>: a single <c>data</c> buffer
        /// with straight alpha.
        ///
        /// <para><b>⚠ UPPERCASE W/H here.</b> <c>brow()</c> and <c>toe()</c> follow <c>face()</c> and
        /// return <c>W</c>/<c>H</c>, while <c>ledge()</c> and <c>contact()</c> return lowercase
        /// <c>w</c>/<c>h</c>. The kit's manifest documents neither; both were measured off the rig and
        /// are pinned by <c>CliffRigBakeTests</c>. Reading the wrong case yields <c>undefined</c> → 0,
        /// which writes an empty strip rather than throwing.</para>
        /// </summary>
        static byte[] ReadStrip(IRigScriptHost host, out int w, out int h)
        {
            w = (int)host.EvaluateNumber($"{ResultGlobal}.W");
            h = (int)host.EvaluateNumber($"{ResultGlobal}.H");
            return host.EvaluateBytes($"{ResultGlobal}.data");
        }

        // =====================================================================================
        //  the contract — checked on the actual pixels, before anything reaches disk
        // =====================================================================================

        /// <summary>
        /// 🔴 A face that is not the canvas it declared is a face the wall shader will tile at the wrong
        /// metre scale — and because the error is a smooth rescale rather than a visible break, it would
        /// ship as "the bedding looks a bit off" instead of as a bug.
        /// </summary>
        public static void AssertFaceCanvas(string rock, string aspect, string slope, int w, int h)
        {
            if (w != CliffCatalog.FaceWidth || h != CliffCatalog.FaceHeight)
                throw new InvalidOperationException(
                    $"[cliff-baker] face {rock} {aspect} {slope}: canvas came back {w}×{h}, expected " +
                    $"{CliffCatalog.FaceWidth}×{CliffCatalog.FaceHeight}. The shader addresses this " +
                    $"texture as {CliffCatalog.FaceMetresS}×{CliffCatalog.FaceMetresT} m of SURFACE at " +
                    $"{CliffCatalog.PxPerMetre} px/m; a different canvas is a different metre scale.");
        }

        public static void AssertChannel(string rock, string aspect, string slope, string channel,
                                         int w, int h, byte[] rgba)
        {
            int expected = w * h * 4;
            if (rgba == null || rgba.Length != expected)
                throw new InvalidOperationException(
                    $"[cliff-baker] {rock} {aspect} {slope} {channel}: came back " +
                    $"{(rgba == null ? "null" : rgba.Length.ToString())} bytes, expected {expected} " +
                    $"for {w}×{h} RGBA. The canvas the rig rendered is not the one it declared.");
        }

        static void AssertStripCanvas(string what, int w, int h, byte[] rgba)
        {
            if (w != CliffCatalog.StripWidth || h != CliffCatalog.StripHeight)
                throw new InvalidOperationException(
                    $"[cliff-baker] {what}: strip came back {w}×{h}, expected " +
                    $"{CliffCatalog.StripWidth}×{CliffCatalog.StripHeight}.");
            AssertChannel("-", what, "-", "strip", w, h, rgba);
        }

        static void AssertCell(string what, int w, int h, byte[] rgba)
        {
            if (w != CliffCatalog.LedgeCell || h != CliffCatalog.LedgeCell)
                throw new InvalidOperationException(
                    $"[cliff-baker] {what}: cell came back {w}×{h}, expected " +
                    $"{CliffCatalog.LedgeCell}×{CliffCatalog.LedgeCell} — the iso ledge grid is that " +
                    "cell size and the sheet is blitted on it.");
            AssertChannel("-", what, "-", "cell", w, h, rgba);
        }

        // =====================================================================================
        //  pixels out
        // =====================================================================================

        static void Write(CliffBakeResult result, CliffAssetKind kind, string name,
                          string outputFolder, byte[] rgba, int w, int h)
        {
            string assetPath = $"{SubFolder(outputFolder, kind)}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.LoadRawTextureData(FlipRows(rgba, w, h));
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);

                result.TotalPngBytes += png.Length;
                result.Assets.Add(new CliffAssetBake
                {
                    Name = name, AssetPath = assetPath, Kind = kind,
                    Width = w, Height = h, PngBytes = png.Length,
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>The kit's own folder split — <c>tex/ brow/ toe/ ledges/ profile/</c> collapsed to
        /// four, because brow and toe are the same asset shape and share an import contract.</summary>
        public static string SubFolder(string outputFolder, CliffAssetKind kind) => kind switch
        {
            CliffAssetKind.Face    => $"{outputFolder}/Faces",
            CliffAssetKind.Strip   => $"{outputFolder}/Decals",
            CliffAssetKind.Ledge   => $"{outputFolder}/Ledges",
            _                      => $"{outputFolder}/Profiles",
        };

        /// <summary>
        /// A rig renders top-row-first (the canvas convention) and
        /// <see cref="Texture2D.LoadRawTextureData(byte[])"/> reads bottom-up, so every baker in this
        /// repo flips once on the way in. Skip it and <see cref="Texture2D.EncodeToPNG"/> writes a
        /// vertically mirrored sheet — which on a periodic wall texture is nearly invisible until the
        /// brow line turns up along the toe.
        /// </summary>
        static byte[] FlipRows(byte[] rgba, int w, int h)
        {
            var flipped = new byte[rgba.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(rgba, y * stride, flipped, (h - 1 - y) * stride, stride);
            return flipped;
        }

        /// <summary>Copy a cell into the sheet at a cell origin. Both buffers are top-row-first here —
        /// the single flip happens once, in <see cref="Write"/>, on the assembled sheet.</summary>
        static void Blit(byte[] cell, int cw, int ch, byte[] sheet, int sheetW, int atX, int atY)
        {
            for (int y = 0; y < ch; y++)
                Buffer.BlockCopy(cell, y * cw * 4, sheet, ((atY + y) * sheetW + atX) * 4, cw * 4);
        }

        // =====================================================================================
        //  import settings (README §6)
        // =====================================================================================

        /// <summary>
        /// The import contract, by asset kind. The three that matter most:
        ///
        /// <list type="number">
        /// <item><description><b>sRGB OFF on <c>_normal</c>, <c>_mask</c> and the profile.</b> They are
        /// DATA — a tangent-space basis, four packed lighting terms, and a displacement in metres. Import
        /// them as colour and every one is gamma-bent, which reads as "the lighting is subtly wrong
        /// everywhere" rather than as a broken texture.</description></item>
        /// <item><description><b>Repeat on the faces.</b> The kit is exactly periodic in <c>s</c> and
        /// carries no chunk offset by design; Clamp would stretch the last texel down a whole cliff
        /// run.</description></item>
        /// <item><description><b>Bilinear on the profile alone.</b> Everything else is point-filtered
        /// pixel art, but the profile is sampled to push vertices at 0.25 m — point-filtering it facets
        /// the silhouette into 32 px steps, which is the opposite of what it is for.</description></item>
        /// </list>
        ///
        /// <para><b>⚠ Compression stays off everywhere.</b> A block-compressed normal map is a
        /// blotchy normal map, and a block-compressed mask puts the cast-shadow term through a lossy
        /// filter it was never designed to survive.</para>
        /// </summary>
        public static bool ApplyImportSettings(string assetPath, CliffAssetKind kind, string channelSuffix)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            bool changed = false;
            bool sprite = CliffCatalog.IsSpriteAsset(kind);
            bool srgb = kind != CliffAssetKind.Profile && CliffCatalog.IsSrgb(channelSuffix);

            var want = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (importer.textureType != want) { importer.textureType = want; changed = true; }

            if (sprite && importer.spriteImportMode != SpriteImportMode.Multiple)
            { importer.spriteImportMode = SpriteImportMode.Multiple; changed = true; }
            if (sprite && !Mathf.Approximately(importer.spritePixelsPerUnit, CliffCatalog.PxPerMetre))
            { importer.spritePixelsPerUnit = CliffCatalog.PxPerMetre; changed = true; }

            if (importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }

            // The profile is the one asset the CPU reads: the wall mesh samples it per vertex to displace
            // the silhouette. Without this the sample throws rather than returning something wrong, which
            // is the good failure — but it throws at BUILD time on a fresh checkout, so set it here where
            // the rest of the contract lives rather than leaving it to whoever notices.
            bool readable = CliffCatalog.IsCpuReadable(kind);
            if (importer.isReadable != readable) { importer.isReadable = readable; changed = true; }

            FilterMode filter = CliffCatalog.Filter(kind);
            if (importer.filterMode != filter) { importer.filterMode = filter; changed = true; }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }

            bool alpha = CliffCatalog.AlphaIsTransparency(kind);
            if (importer.alphaIsTransparency != alpha)
            { importer.alphaIsTransparency = alpha; changed = true; }

            // Unity's importer carries ONE wrap mode plus per-axis overrides. Faces wrap both ways;
            // strips and profiles wrap along the shore and clamp top-to-bottom; ledges clamp.
            TextureWrapMode u = CliffCatalog.WrapU(kind), v = CliffCatalog.WrapV(kind);
            if (u == v)
            {
                if (importer.wrapMode != u) { importer.wrapMode = u; changed = true; }
            }
            else
            {
                if (importer.wrapMode != TextureWrapMode.Repeat)
                { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
                if (importer.wrapModeU != u) { importer.wrapModeU = u; changed = true; }
                if (importer.wrapModeV != v) { importer.wrapModeV = v; changed = true; }
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
            return changed;
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
