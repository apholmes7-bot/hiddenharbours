using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the grass shader — the grass twin of <see cref="WaterShaderCompileGuardTests"/>. It
    /// force-reimports <c>HiddenHarboursGrass.shader</c> from its on-disk source AND force-compiles the shipped
    /// <c>Grass.mat</c> keyword variant, then FAILS RED on ANY shader compiler error. Why a dedicated test when the
    /// water guard already sweeps every project shader: so the grass surface is EXPLICITLY anchored — if material
    /// discovery ever changed, or Grass.mat were renamed/removed, this still names the exact asset that must
    /// compile and fails loudly rather than silently guarding nothing.
    ///
    /// The classic traps this catches (both cost this project hours on the water shader): a '+' (or other operator)
    /// in a <c>[Header(...)]</c> label or property string = a ShaderLab PARSE error -> magenta; and an
    /// <c>[unroll]</c> over a runtime loop bound = an HLSL variant compile error -> magenta. A shader error does
    /// NOT fail a normal test run, and nothing else force-compiles the shipped variant, so without this a magenta
    /// grass material would sail past green CI.
    /// </summary>
    public class GrassShaderCompileGuardTests
    {
        private const string GrassShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursGrass.shader";
        private const string GrassMaterialPath = "Assets/_Project/Art/Materials/Grass.mat";

        [Test]
        public void GrassShader_CompilesItsShippedVariant_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on the
            // "Unhandled log message" instead of via our own assertion (wrong reason, opaque message).
            LogAssert.ignoreFailingMessages = true;

            // Force a fresh synchronous reimport so the compiler re-runs from source (no stale cache can hide a
            // fresh break or fake one), then read the shader asset's import-time messages.
            AssetDatabase.ImportAsset(
                GrassShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GrassShaderPath);
            Assert.IsNotNull(
                shader,
                $"Could not load the grass shader at '{GrassShaderPath}'. A missing/renamed shader leaves the " +
                "grass magenta class UNGUARDED — treat it as a failure, not a pass.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();

            CollectMessages(errors, warnings, "<import>", null, ShaderUtil.GetShaderMessages(shader));

            // Anchor + variant compile: the shipped Grass.mat must exist and its exact keyword variant must compile.
            var grassMat = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            Assert.IsNotNull(
                grassMat,
                $"The grass material ('{GrassMaterialPath}') was not found — that leaves the grass magenta class " +
                "UNGUARDED. Treat it as a failure, not a pass.");
            Assert.AreEqual(
                shader, grassMat.shader,
                "Grass.mat is not using HiddenHarbours/GrassWind — the guard would be compiling the wrong shader.");

            int passes = grassMat.passCount > 0 ? grassMat.passCount : 1;
            for (int pass = 0; pass < passes; pass++)
                ShaderUtil.CompilePass(grassMat, pass, true);
            CollectMessages(errors, warnings, GrassMaterialPath, DescribeKeywords(grassMat),
                ShaderUtil.GetShaderMessages(grassMat.shader));

            if (warnings.Length > 0)
                Debug.Log("[GrassShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(
                errors.ToString(),
                "The grass shader reported a COMPILER ERROR (import and/or the shipped Grass.mat variant). This is " +
                "the MAGENTA class — fix the shader until it imports and every shipped variant compiles cleanly; do " +
                "NOT silence this guard:\n" + errors);
        }

        private static void CollectMessages(
            StringBuilder errors, StringBuilder warnings, string materialPath, string keywords,
            ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] grass shader (material '{materialPath}'" +
                    (keywords != null ? $", keywords: {keywords}" : "") + $")\n    {msg.message}\n" +
                    (string.IsNullOrEmpty(msg.messageDetails) ? "" : $"    {msg.messageDetails}\n") +
                    $"    platform={msg.platform}  line={msg.line}";
                if (sink.ToString().IndexOf(line, System.StringComparison.Ordinal) < 0)
                    sink.AppendLine(line);
            }
        }

        private static string DescribeKeywords(Material mat)
        {
            string[] kw = mat.shaderKeywords;
            return (kw == null || kw.Length == 0) ? "<none>" : string.Join(" ", kw);
        }
    }
}
