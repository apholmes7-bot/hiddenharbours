using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    public sealed class CharacterSheetBake
    {
        public string Anim;
        public string AssetPath;
        public int Width, Height, Frames;
        public override string ToString() =>
            $"{AssetPath}  {Width}×{Height}  ({Frames} frames × 8 direction rows)";
    }

    public sealed class CharacterBakeResult
    {
        public string RigKey;
        public string EngineName;
        public RigGeometry Geometry;
        public AzimuthConvention MeasuredConvention;
        public string ConventionReport;
        public readonly List<CharacterSheetBake> Sheets = new List<CharacterSheetBake>();
        public string AnchorJsonPath;
        public double RenderMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;
        public int CellsRendered;
    }

    /// <summary>
    /// Runs a CHARACTER rig and writes 8-direction ANIMATION sheets — the sibling of
    /// <see cref="RigBaker"/>, which is a boat TURNTABLE (N facings × rock frames) and cannot
    /// express "rows are directions, columns are animation frames driven by
    /// <c>render(dir, {anim, frame})</c>". The two stay separate deliberately: folding an anim axis
    /// into the turntable request would complicate the proven boat path for no shared code beyond
    /// the blit, which IS shared (<see cref="RigBaker.Blit"/>).
    ///
    /// Sheet contract (must match <c>CharacterSheetSlicer</c>, which slices these untouched):
    /// 8 rows = directions, row d at the TOP of the canvas is direction d; N columns = the anim's
    /// frames, read from the rig's own ANIMS table (ADR 0021 §4 — geometry from the rig, never a
    /// README); cell = the rig's W×H.
    ///
    /// ⚠️ CONVENTION: character rigs were fixed CLOCKWISE at source, so cells are emitted as
    /// <c>render(d)</c> UNCHANGED — the boat (N−k)%N mirror must never touch them (re-mirroring
    /// corrected art is the exact blanket-fix mistake <see cref="RigBaker.DirForCell"/> documents).
    /// That is not trusted from this comment: <see cref="CharacterRigAzimuthProbe"/> measures the
    /// convention from rendered pixels and the bake REFUSES on a mismatch with the catalog, exactly
    /// like the boat path. <see cref="RigBaker.DirForCell"/> is still used for the emission so the
    /// correction logic lives in one place — for a measured-clockwise rig it is the identity.
    ///
    /// Like the boat baker this deliberately stops at "PNG (+ anchors JSON) on disk": slicing is
    /// <c>CharacterSheetSlicer</c>'s job and import settings are <c>ArtImportPipeline</c>'s.
    /// </summary>
    public static class CharacterRigBaker
    {
        /// <summary>
        /// The DEFAULT importer texture cap. Boat pages check against Unity's hard 4096 cap because
        /// <c>SpriteSheetSlicer</c> lifts maxTextureSize from its manifest; the character slicer
        /// does no such lift, so a character sheet must fit under the untouched default — over it,
        /// the sheet imports SILENTLY DOWNSCALED while the sprite COUNT still matches, and only the
        /// slice tests' dimension/pivot asserts would ever notice.
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>
        /// Bake one sheet per anim, plus one combined anchors JSON. One host and one convention
        /// probe serve the whole batch — the states all come off the same rig.
        /// </summary>
        /// <param name="rigKey">Catalog key, e.g. "character".</param>
        /// <param name="anims">Anim names as declared in the rig's ANIMS table.</param>
        /// <param name="outputFolder">Project-relative, e.g. "Assets/_Project/Art/Characters/Iso".</param>
        /// <param name="baseNamePrefix">Sheet file prefix, e.g. "Fisher_" → Fisher_bite.png.</param>
        /// <param name="anchorFileName">File name for the combined anchors JSON, e.g.
        /// "FisherFightAnchors.json". Null/empty skips anchors.</param>
        /// <param name="progress">Optional (label, 0..1) callback for a progress bar.</param>
        public static CharacterBakeResult Bake(string rigKey, IReadOnlyList<string> anims,
                                               string outputFolder, string baseNamePrefix,
                                               string anchorFileName = null,
                                               Action<string, float> progress = null)
        {
            if (anims == null || anims.Count == 0)
                throw new ArgumentException("Nothing to bake — no anims named.", nameof(anims));

            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get(rigKey);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new CharacterBakeResult
            {
                RigKey = rigKey,
                EngineName = host.EngineName,
                Geometry = geo,
            };

            // ---- MEASURE the azimuth convention from pixels, then cross-check the declaration ----
            var probe = CharacterRigAzimuthProbe.Measure(host, g, geo);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;

            if (probe.Convention != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"AZIMUTH MISMATCH on rig '{rigKey}'.\n" +
                    $"  the catalog declares       : {entry.DeclaredConvention}\n" +
                    $"  the rendered pixels say    : {probe.Convention}\n\n" +
                    probe.Report + "\n\n" +
                    "The bake is refusing rather than guessing. A silent guess here is how this " +
                    "mislabel shipped defects in five kits — and this rig has been mislabelled " +
                    "twice before being fixed at source. Look at the art, decide which is right, " +
                    "and correct the catalog (or the rig) — do not relax this check.");

            // Validate the WHOLE recipe against the rig before writing anything, so a mistyped
            // state name fails with zero files on disk rather than a half-baked set.
            foreach (var anim in anims) FramesOf(host, g, anim);

            int dirs = geo.NativeDirs;
            var renderClock = new Stopwatch();
            string outAbs = Path.Combine(RigCatalog.RepoRoot, outputFolder);
            Directory.CreateDirectory(outAbs);

            // ---- Bake one sheet per anim -------------------------------------------------------
            for (int a = 0; a < anims.Count; a++)
            {
                string anim = anims[a];
                progress?.Invoke($"{baseNamePrefix}{anim}", (float)a / anims.Count);

                int frames = FramesOf(host, g, anim);
                int pw = frames * geo.Width;
                int ph = dirs * geo.Height;
                if (pw > ImportSizeCap || ph > ImportSizeCap)
                    throw new InvalidOperationException(
                        $"'{anim}' would bake to {pw}×{ph}, over the {ImportSizeCap} default import " +
                        "cap. Unity would import it DOWNSCALED with a matching sprite count, so this " +
                        "would only surface as a slice-test dimension failure much later. Split the " +
                        "state or page the sheet before raising this limit.");

                var pixels = new Color32[pw * ph];
                string animJs = JsString(anim);

                for (int d = 0; d < dirs; d++)
                {
                    // The one-place correction: identity for a measured-clockwise rig.
                    double dir = RigBaker.DirForCell(d, dirs, probe.Convention);
                    string ds = dir.ToString("R", CultureInfo.InvariantCulture);

                    for (int f = 0; f < frames; f++)
                    {
                        renderClock.Start();
                        byte[] rgba = host.EvaluateBytes(
                            $"{g}.render({ds},{{anim:{animJs},frame:{f.ToString(CultureInfo.InvariantCulture)}}})");
                        renderClock.Stop();
                        result.CellsRendered++;

                        if (rgba.Length != geo.Width * geo.Height * 4)
                            throw new InvalidOperationException(
                                $"'{anim}' dir {d} frame {f} came back {rgba.Length} bytes, expected " +
                                $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");

                        RigBaker.Blit(rgba, geo.Width, geo.Height, pixels, pw, ph,
                                      col: f, rowFromTop: d);
                    }
                }

                string assetPath = $"{outputFolder}/{baseNamePrefix}{anim}.png";
                var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
                try
                {
                    tex.SetPixels32(pixels);
                    tex.Apply(false, false);
                    byte[] png = tex.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);
                    result.TotalPngBytes += png.Length;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                result.Sheets.Add(new CharacterSheetBake
                {
                    Anim = anim, AssetPath = assetPath, Width = pw, Height = ph, Frames = frames,
                });
            }

            // ---- Anchors (the boat WriteAnchors pattern, per state × dir × frame) ---------------
            if (!string.IsNullOrEmpty(anchorFileName))
                result.AnchorJsonPath = WriteAnchors(host, entry, geo, anims, probe.Convention,
                                                     outputFolder, anchorFileName);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Frame count of one anim, from the rig's own ANIMS table — never a README.</summary>
        public static int FramesOf(IRigScriptHost host, string globalName, string anim)
        {
            string animJs = JsString(anim);
            if (!host.EvaluateBool($"typeof {globalName}.ANIMS[{animJs}] === 'object' && " +
                                   $"{globalName}.ANIMS[{animJs}] !== null"))
            {
                string known = host.EvaluateString(
                    $"Object.keys({globalName}.ANIMS).join(', ')");
                throw new ArgumentException(
                    $"Rig '{globalName}' declares no anim '{anim}'. Known: {known}. " +
                    "If the state genuinely does not exist yet, that is an art-director rig change, " +
                    "not a baker workaround.");
            }
            int frames = (int)host.EvaluateNumber($"{globalName}.ANIMS[{animJs}].frames");
            if (frames <= 0)
                throw new InvalidOperationException($"Anim '{anim}' declares {frames} frames.");
            return frames;
        }

        static string WriteAnchors(IRigScriptHost host, in RigEntry entry, in RigGeometry geo,
                                   IReadOnlyList<string> anims, AzimuthConvention convention,
                                   string outputFolder, string anchorFileName)
        {
            string g = entry.GlobalName;
            int dirs = geo.NativeDirs;

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"rig\": \"{entry.ScriptPath}\",\n");
            sb.Append($"  \"global\": \"{g}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(geo.PivotX)}, \"y\": {Num(geo.PivotY)} }},\n");
            sb.Append($"  \"dirs\": {dirs},\n");
            sb.Append($"  \"measuredRigConvention\": \"{convention}\",\n");
            sb.Append("  \"facingsAreCounterClockwise\": false,\n");
            sb.Append("  \"_note\": \"Baked in-engine with the rig's measured convention applied, so row d of every sheet depicts heading 360*d/dirs. Anchor cell px are TOP-LEFT origin, per direction row then per frame column.\",\n");
            sb.Append("  \"states\": {\n");

            for (int a = 0; a < anims.Count; a++)
            {
                string anim = anims[a];
                string animJs = JsString(anim);
                int frames = FramesOf(host, g, anim);

                sb.Append($"    \"{anim}\": {{ \"frames\": {frames}, \"anchors\": [\n");
                for (int d = 0; d < dirs; d++)
                {
                    double dir = RigBaker.DirForCell(d, dirs, convention);
                    string ds = dir.ToString("R", CultureInfo.InvariantCulture);
                    sb.Append("      [");
                    for (int f = 0; f < frames; f++)
                    {
                        string json = host.EvaluateString(
                            $"JSON.stringify({g}.anchors({ds},{{anim:{animJs},frame:{f}}}))");
                        sb.Append(json);
                        if (f < frames - 1) sb.Append(", ");
                    }
                    sb.Append(d < dirs - 1 ? "],\n" : "]\n");
                }
                sb.Append(a < anims.Count - 1 ? "    ] },\n" : "    ] }\n");
            }

            sb.Append("  }\n}\n");

            string assetPath = $"{outputFolder}/{anchorFileName}";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, assetPath), sb.ToString());
            return assetPath;
        }

        /// <summary>Single-quoted JS string literal for an anim name (names are plain identifiers,
        /// but escape defensively — a quote in a name must not become an injection).</summary>
        static string JsString(string s) =>
            "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
