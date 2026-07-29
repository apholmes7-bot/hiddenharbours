using System;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// Measures what widening the shared wave field costs the water shader (ADR 0027 P2 / rule 7).
    ///
    /// <para><b>Why this is a tool and not a test.</b> CI has no graphics device, and anything that
    /// drives the shader compiler there is the class of thing that crashes the editor outright
    /// rather than failing clean. This is a LOCAL instrument: run it before and after a change to
    /// <c>WAVE_MAX_TRAINS</c> and put both numbers in the PR. It is never part of the suite.</para>
    ///
    /// <para><b>Two numbers, deliberately.</b> The compiled fragment bytecode size is empirical but
    /// indirect (DXBC size tracks instruction count, it is not instruction count). The analytic
    /// model is exact and reviewable: the wave field's cost is entirely determined by how many
    /// <c>WaveFieldSample()</c> call sites the fragment can take and how many trains each unrolls,
    /// and both are compile-time constants. Quote them together — if they disagree about the
    /// direction of a change, the change is not understood yet.</para>
    ///
    /// <para>Run headless:
    /// <c>Unity -batchmode -projectPath . -executeMethod HiddenHarbours.Tools.Editor.WaveFieldCostReport.RunHeadless -logFile -</c>
    /// (no <c>-quit</c>; the entry point exits itself once the report is written).</para>
    /// </summary>
    public static class WaveFieldCostReport
    {
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        /// <summary>Fragment-side <c>WaveFieldSample()</c> call sites, counted from the shader by hand
        /// and re-counted by <see cref="CountCallSites"/> so this constant cannot rot silently:
        /// 1 main sample + 4 caustic-curvature taps + 4 foam-convergence taps.</summary>
        private const int ExpectedFragmentCallSites = 9;

        [MenuItem("Hidden Harbours/Water/Wave-field cost report", priority = 400)]
        public static void Run() => Debug.Log(BuildReport());

        public static void RunHeadless()
        {
            string report = BuildReport();
            Console.WriteLine(report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        private static string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WAVE-FIELD COST REPORT (ADR 0027 P2, rule 7) ===");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShaderPath);
            if (shader == null)
            {
                sb.AppendLine($"!! shader not found at {WaterShaderPath}");
                return sb.ToString();
            }

            string source = System.IO.File.ReadAllText(WaterShaderPath);
            int maxTrains = ParseDefine(source, "WAVE_MAX_TRAINS");
            int callSites = CountCallSites(source);

            sb.AppendLine($"WAVE_MAX_TRAINS            : {maxTrains}");
            sb.AppendLine($"WaveFieldSample call sites : {callSites} in the file " +
                          $"({ExpectedFragmentCallSites} fragment-reachable + 1 vertex)");

            // ---- the analytic model -------------------------------------------------------------
            // Per train, per call, the unrolled body evaluates: 1 sin, 1 cos, 2 pow, 1 dot2,
            // and ~8 mad/mul/add. The transcendentals dominate; count them separately because a
            // pow is not a mad on any of the hardware this ships to.
            const int TranscendentalsPerTrain = 4;   // sin + cos + 2x pow
            const int AluPerTrain = 12;              // the mads/muls/adds around them

            int fragTrainEvals = ExpectedFragmentCallSites * maxTrains;
            sb.AppendLine();
            sb.AppendLine("-- analytic worst-case fragment cost (every gate open) --");
            sb.AppendLine($"train evaluations / pixel  : {fragTrainEvals} " +
                          $"({ExpectedFragmentCallSites} calls x {maxTrains} trains, [unroll]ed: dead slots still cost)");
            sb.AppendLine($"transcendentals / pixel    : {fragTrainEvals * TranscendentalsPerTrain} " +
                          $"(sin, cos, 2x pow per train)");
            sb.AppendLine($"other ALU / pixel          : ~{fragTrainEvals * AluPerTrain}");

            // ---- the empirical number -----------------------------------------------------------
            sb.AppendLine();
            sb.AppendLine("-- compiled fragment bytecode (D3D11, the shipped variant) --");
            AppendCompiledSize(sb, shader);

            sb.AppendLine();
            sb.AppendLine("Compare against the same report on the base revision; quote BOTH numbers.");
            return sb.ToString();
        }

        private static void AppendCompiledSize(StringBuilder sb, Shader shader)
        {
            try
            {
                ShaderData data = ShaderUtil.GetShaderData(shader);
                if (data == null) { sb.AppendLine("   (no shader data)"); return; }

                ShaderData.Subshader subshader = data.ActiveSubshader;
                if (subshader == null) { sb.AppendLine("   (no active subshader)"); return; }

                int total = 0;
                for (int p = 0; p < subshader.PassCount; p++)
                {
                    ShaderData.Pass pass = subshader.GetPass(p);
                    ShaderData.VariantCompileInfo info = pass.CompileVariant(
                        ShaderType.Fragment, Array.Empty<string>(),
                        ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64,
                        GraphicsTier.Tier1, false);

                    if (!info.Success)
                    {
                        sb.AppendLine($"   pass {p} ({pass.Name}): COMPILE FAILED");
                        foreach (var m in info.Messages) sb.AppendLine($"      {m.message}");
                        continue;
                    }

                    int bytes = info.ShaderData?.Length ?? 0;
                    total += bytes;
                    sb.AppendLine($"   pass {p} ({pass.Name}): {bytes} bytes");
                }
                sb.AppendLine($"   TOTAL fragment bytecode: {total} bytes");
            }
            catch (Exception e)
            {
                // The compile API moves between Unity versions; the analytic model above is the
                // number that must survive, so a failure here is reported, not thrown.
                sb.AppendLine($"   (compile-variant probe unavailable: {e.GetType().Name}: {e.Message})");
            }
        }

        private static int ParseDefine(string source, string name)
        {
            int at = source.IndexOf("#define " + name, StringComparison.Ordinal);
            if (at < 0) return -1;
            int eol = source.IndexOf('\n', at);
            string line = eol < 0 ? source.Substring(at) : source.Substring(at, eol - at);
            string tail = line.Substring(("#define " + name).Length).Trim();
            return int.TryParse(tail, out int value) ? value : -1;
        }

        private static int CountCallSites(string source)
        {
            int count = 0, at = 0;
            while ((at = source.IndexOf("WaveFieldSample(", at, StringComparison.Ordinal)) >= 0)
            {
                // Skip the declaration itself and any mention inside a comment line.
                int lineStart = source.LastIndexOf('\n', at) + 1;
                string prefix = source.Substring(lineStart, at - lineStart).TrimStart();
                if (!prefix.StartsWith("//", StringComparison.Ordinal) &&
                    !prefix.StartsWith("void ", StringComparison.Ordinal))
                    count++;
                at += "WaveFieldSample(".Length;
            }
            return count;
        }
    }
}
