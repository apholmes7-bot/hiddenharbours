using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the flower shader — the flower twin of <see cref="GrassShaderCompileGuardTests"/>. It
    /// force-reimports <c>HiddenHarboursFlower.shader</c> from its on-disk source AND force-compiles the shipped
    /// keyword variant of ALL THREE tier materials, then FAILS RED on ANY shader compiler error.
    ///
    /// A shader error does NOT fail a normal test run, and nothing else force-compiles these variants, so without
    /// this a magenta flower material would sail past green CI — and magenta flowers are exactly the kind of thing
    /// that gets misread as "the import broke" rather than "the shader broke". The classic traps (both of which
    /// cost this project hours on the water shader): an operator character in a <c>[Header(...)]</c> label or
    /// property string = a ShaderLab PARSE error; an <c>[unroll]</c> over a runtime loop bound = an HLSL variant
    /// compile error. This shader has both a Header-heavy property block and an unrolled footstep loop, so it is
    /// exposed to both.
    /// </summary>
    public class FlowerShaderCompileGuardTests
    {
        private const string FlowerShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursFlower.shader";

        private static readonly string[] TierMaterialPaths =
        {
            "Assets/_Project/Art/Materials/Flower_Single.mat",
            "Assets/_Project/Art/Materials/Flower_Clump.mat",
            "Assets/_Project/Art/Materials/Flower_Patch.mat",
        };

        [Test]
        public void FlowerShader_CompilesEveryShippedTierVariant_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on the
            // "Unhandled log message" instead of via our own assertion (wrong reason, opaque message).
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                FlowerShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FlowerShaderPath);
            Assert.IsNotNull(
                shader,
                $"Could not load the flower shader at '{FlowerShaderPath}'. A missing/renamed shader leaves the " +
                "flower magenta class UNGUARDED — treat it as a failure, not a pass.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();

            CollectMessages(errors, warnings, "<import>", null, ShaderUtil.GetShaderMessages(shader));

            foreach (string path in TierMaterialPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(
                    mat,
                    $"The flower material '{path}' was not found — that leaves one whole tier's magenta class " +
                    "UNGUARDED. Treat it as a failure, not a pass.");
                Assert.AreEqual(
                    shader, mat.shader,
                    $"'{path}' is not using HiddenHarbours/FlowerWind — the guard would be compiling the wrong shader.");

                int passes = mat.passCount > 0 ? mat.passCount : 1;
                for (int pass = 0; pass < passes; pass++)
                    ShaderUtil.CompilePass(mat, pass, true);
                CollectMessages(errors, warnings, path, DescribeKeywords(mat),
                    ShaderUtil.GetShaderMessages(mat.shader));
            }

            if (warnings.Length > 0)
                Debug.Log("[FlowerShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(
                errors.ToString(),
                "The flower shader reported a COMPILER ERROR (import and/or a shipped Flower_*.mat variant). This " +
                "is the MAGENTA class — fix the shader until it imports and every shipped variant compiles " +
                "cleanly; do NOT silence this guard:\n" + errors);
        }

        private static void CollectMessages(
            StringBuilder errors, StringBuilder warnings, string materialPath, string keywords,
            ShaderMessage[] messages)
        {
            if (messages == null) return;
            foreach (var m in messages)
            {
                var sb = m.severity == ShaderCompilerMessageSeverity.Error ? errors : warnings;
                sb.Append($"  [{materialPath}");
                if (!string.IsNullOrEmpty(keywords)) sb.Append($" | {keywords}");
                sb.Append($"] {m.message}");
                if (!string.IsNullOrEmpty(m.messageDetails)) sb.Append($" — {m.messageDetails}");
                if (!string.IsNullOrEmpty(m.file)) sb.Append($" ({m.file}:{m.line})");
                sb.AppendLine();
            }
        }

        private static string DescribeKeywords(Material m)
        {
            var kw = m.shaderKeywords;
            return kw == null || kw.Length == 0 ? "no keywords" : string.Join(" ", kw);
        }
    }
}
