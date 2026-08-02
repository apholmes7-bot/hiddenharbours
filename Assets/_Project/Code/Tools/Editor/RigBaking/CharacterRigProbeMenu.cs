using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The two MEASUREMENTS that must happen before (and independently of) a character bake: which
    /// way the rig's facings actually run, and whether its structural axes are separable layers.
    /// Neither writes art. Both write their report to the console and to
    /// <c>artifacts/character-rig-probe.txt</c>, so an architecture decision can quote numbers
    /// instead of recollection.
    /// </summary>
    public static class CharacterRigProbeMenu
    {
        public const string ReportPath = "artifacts/character-rig-probe.txt";

        [MenuItem("Hidden Harbours/Dev/Probe Character Rig (azimuth + layer separation)",
                  priority = 120)]
        public static void Probe()
        {
            string report = Run();
            Debug.Log(report);
            Debug.Log($"[character-probe] Written to {ReportPath} " +
                      "(artifacts/ is git-ignored — quote it in the ADR, don't commit it).");
        }

        /// <summary>The measurement itself, host and all. Returns the whole report.</summary>
        public static string Run()
        {
            var sb = new StringBuilder();
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            sb.AppendLine("=== CHARACTER RIG PROBE ==================================================");
            sb.AppendLine($"engine   : {host.EngineName}");
            sb.AppendLine($"rig      : {entry.ScriptPath} -> {g}");
            sb.AppendLine($"geometry : {geo}");
            sb.AppendLine($"pass     : {host.EvaluateNumber($"{g}.pass")}");

            // The prerequisites really did load — a body with no head rig renders wrong art in
            // silence, so this is reported rather than assumed.
            string head = host.EvaluateString(
                "typeof HeadIso === 'object' ? ('HeadIso pass ' + HeadIso.pass) : 'ABSENT'");
            string eye = host.EvaluateString(
                "typeof EyeIso === 'object' ? ('EyeIso pass ' + EyeIso.pass) : 'ABSENT'");
            sb.AppendLine($"head rig : {head}");
            sb.AppendLine($"eye rig  : {eye}");
            sb.AppendLine($"anims    : {host.EvaluateString($"Object.keys({g}.ANIMS).join(', ')")}");
            sb.AppendLine($"builds   : {host.EvaluateString($"{g}.CAST.join(', ')")}");
            sb.AppendLine();

            // ---- 1. azimuth ---------------------------------------------------------------------
            sb.AppendLine("--- AZIMUTH (measured from pixels; the catalog is only cross-checked) ---");
            try
            {
                var probe = CharacterRigAzimuthProbe.Measure(host, g, geo);
                sb.AppendLine(probe.Report);
                sb.AppendLine($"catalog declares: {entry.DeclaredConvention}");
                sb.AppendLine(probe.Convention == entry.DeclaredConvention
                    ? "AGREES — the bake will proceed."
                    : "MISMATCH — the bake will REFUSE, by design. Look at the art before touching " +
                      "the catalog.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("PROBE THREW: " + ex.Message);
            }
            sb.AppendLine();

            // ---- 2. where the figure sits in the cell -------------------------------------------
            // PlayerSubmergeVisual clips the waterline on the SPRITE's uv.y, so its tunables describe
            // the CELL, not the character — and the cell just changed shape. These are the numbers
            // PersistentCoreBuilder's wade calibration is derived from, measured rather than carried
            // over: the old pair was read off the pass-1 art and is not transferable.
            sb.AppendLine("--- FIGURE SPAN IN THE CELL (the wade calibration's source) -------------");
            sb.AppendLine(MeasureFigureSpan(host, g, geo));
            sb.AppendLine();

            // ---- 3. layer separation ------------------------------------------------------------
            var layers = CharacterLayerSeparationProbe.Measure(host, g, geo);
            sb.AppendLine(layers.Report);

            string text = sb.ToString();
            string abs = Path.Combine(RigCatalog.RepoRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, text);
            return text;
        }

        /// <summary>
        /// The topmost and bottommost opaque rows of the figure, across the idle cycle and every
        /// facing, for the tallest and shortest presets in the cast. In cell px (top-left origin) and
        /// in the sprite's own bottom-origin uv.y, which is the space the submerge shader clips in.
        /// </summary>
        static string MeasureFigureSpan(IRigScriptHost host, string g, in RigGeometry geo)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"cell {geo.Width}×{geo.Height}, pivot y {geo.PivotY} " +
                          $"(ground inset {geo.Height - geo.PivotY} px)");

            foreach (string preset in new[] { "fisher", "skipper", "girl" })
            {
                int top = int.MaxValue, bottom = -1;
                int frames = (int)host.EvaluateNumber($"{g}.ANIMS.idle.frames");
                for (int d = 0; d < geo.NativeDirs; d++)
                for (int f = 0; f < frames; f++)
                {
                    byte[] rgba = host.EvaluateBytes(
                        $"{g}.render({d},{{anim:'idle',frame:{f},build:{{preset:'{preset}'}}}})");
                    for (int y = 0; y < geo.Height; y++)
                    for (int x = 0; x < geo.Width; x++)
                        if (rgba[(y * geo.Width + x) * 4 + 3] >= 128)
                        {
                            if (y < top) top = y;
                            if (y > bottom) bottom = y;
                            break;
                        }
                }

                if (bottom < 0) { sb.AppendLine($"  {preset}: NOTHING DRAWN"); continue; }

                // Unity's uv.y is bottom-origin: row y from the top is (H − 1 − y) from the bottom.
                double uvTop = (geo.Height - 1 - top) / (double)geo.Height;
                double uvFoot = (geo.Height - 1 - bottom) / (double)geo.Height;
                double heightM = (bottom - top + 1) / 32.0;
                sb.AppendLine($"  {preset,-8}: rows {top}..{bottom} of {geo.Height}  " +
                              $"uv.y {uvFoot:F3} (feet) .. {uvTop:F3} (crown)  " +
                              $"≈ {heightM:F2} m of figure");
            }

            sb.Append("  => IsoCellHeightMeters = H/32; a neck-deep fraction must sit just under the " +
                      "TALLEST crown uv.y above, or the waterline runs off the top of the head.");
            return sb.ToString();
        }

        /// <summary>
        /// Headless entry point. ⚠️ Never invoke with -quit alongside -runTests: the two race and
        /// Unity exits 0 having written total=0, which reads as a pass.
        /// </summary>
        public static void ProbeFromCommandLine()
        {
            try
            {
                Debug.Log(Run());
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[character-probe] headless probe failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
