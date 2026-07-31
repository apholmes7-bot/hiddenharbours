using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the terrain splat shader (ADR 0028) — the ground twin of
    /// <see cref="GrassShaderCompileGuardTests"/>. It force-reimports
    /// <c>HiddenHarboursTerrainSplat.shader</c> from its on-disk source AND force-compiles the shipped
    /// <c>TerrainSplat.mat</c> keyword variant, then FAILS RED on ANY shader compiler error. The water
    /// guard's sweep would pick the material up too, but the ground is EXPLICITLY anchored here: if
    /// material discovery ever changed, or TerrainSplat.mat were renamed/removed, this still names the
    /// exact asset that must compile and fails loudly rather than silently guarding nothing — a magenta
    /// GROUND is the whole region breaking, not one prop.
    ///
    /// The classic traps (both cost this project hours on the water shader): an operator character in a
    /// <c>[Header(...)]</c>/property label = a ShaderLab PARSE error -> magenta; an <c>[unroll]</c> over
    /// a runtime loop bound = an HLSL variant compile error -> magenta. A shader error does NOT fail a
    /// normal test run, and nothing else force-compiles the shipped variant.
    /// </summary>
    public class TerrainSplatShaderCompileGuardTests
    {
        private const string SplatShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader";
        private const string SplatMaterialPath = "Assets/_Project/Art/Materials/TerrainSplat.mat";

        [Test]
        public void TerrainSplatShader_CompilesItsShippedVariant_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on the
            // "Unhandled log message" instead of via our own assertion (wrong reason, opaque message).
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                SplatShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SplatShaderPath);
            Assert.IsNotNull(
                shader,
                $"Could not load the terrain splat shader at '{SplatShaderPath}'. A missing/renamed shader " +
                "leaves the ground magenta class UNGUARDED — treat it as a failure, not a pass.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();

            CollectMessages(errors, warnings, "<import>", null, ShaderUtil.GetShaderMessages(shader));

            var splatMat = AssetDatabase.LoadAssetAtPath<Material>(SplatMaterialPath);
            Assert.IsNotNull(
                splatMat,
                $"The terrain splat material ('{SplatMaterialPath}') was not found — that leaves the ground " +
                "magenta class UNGUARDED. Treat it as a failure, not a pass.");
            Assert.AreEqual(
                shader, splatMat.shader,
                "TerrainSplat.mat is not using HiddenHarbours/TerrainSplat — the guard would be compiling " +
                "the wrong shader.");

            int passes = splatMat.passCount > 0 ? splatMat.passCount : 1;
            for (int pass = 0; pass < passes; pass++)
                ShaderUtil.CompilePass(splatMat, pass, true);
            CollectMessages(errors, warnings, SplatMaterialPath, DescribeKeywords(splatMat),
                ShaderUtil.GetShaderMessages(splatMat.shader));

            if (warnings.Length > 0)
                Debug.Log("[TerrainSplatShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(
                errors.ToString(),
                "The terrain splat shader reported a COMPILER ERROR (import and/or the shipped " +
                "TerrainSplat.mat variant). This is the MAGENTA class — fix the shader until it imports and " +
                "every shipped variant compiles cleanly; do NOT silence this guard:\n" + errors);
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
                    $"[{(isError ? "ERROR" : "WARN")}] terrain splat shader (material '{materialPath}'" +
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
