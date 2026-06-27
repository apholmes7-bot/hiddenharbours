using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the tree-wind shader — the tree twin of <see cref="GrassShaderCompileGuardTests"/>. It
    /// force-reimports <c>HiddenHarboursTreeWind.shader</c> from its on-disk source AND force-compiles the shipped
    /// <c>Tree.mat</c> keyword variant, then FAILS RED on ANY shader compiler error. Why a dedicated test when the
    /// water guard already sweeps every project shader: explicit anchoring — if material discovery ever changed, or
    /// Tree.mat were renamed/removed, this still names the exact asset that must compile and fails loudly rather
    /// than silently guarding nothing.
    ///
    /// The classic traps this catches (both cost this project hours on the water shader): a '+' (or other operator)
    /// in a <c>[Header(...)]</c> label or property string = a ShaderLab PARSE error -> magenta; and an
    /// <c>[unroll]</c> over a runtime loop bound = an HLSL variant compile error -> magenta. (TreeWind has no loops,
    /// but the guard still proves it.) A shader error does NOT fail a normal test run, and nothing else
    /// force-compiles the shipped variant, so without this a magenta tree material would sail past green CI.
    /// </summary>
    public class TreeWindShaderCompileGuardTests
    {
        private const string TreeShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursTreeWind.shader";
        private const string TreeMaterialPath = "Assets/_Project/Art/Materials/Tree.mat";

        [Test]
        public void TreeShader_CompilesItsShippedVariant_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on the
            // "Unhandled log message" instead of via our own assertion (wrong reason, opaque message).
            LogAssert.ignoreFailingMessages = true;

            // Force a fresh synchronous reimport so the compiler re-runs from source (no stale cache can hide a
            // fresh break or fake one), then read the shader asset's import-time messages.
            AssetDatabase.ImportAsset(
                TreeShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(TreeShaderPath);
            Assert.IsNotNull(
                shader,
                $"Could not load the tree shader at '{TreeShaderPath}'. A missing/renamed shader leaves the " +
                "tree magenta class UNGUARDED — treat it as a failure, not a pass.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();

            CollectMessages(errors, warnings, "<import>", null, ShaderUtil.GetShaderMessages(shader));

            // Anchor + variant compile: the shipped Tree.mat must exist and its exact keyword variant must compile.
            var treeMat = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterialPath);
            Assert.IsNotNull(
                treeMat,
                $"The tree material ('{TreeMaterialPath}') was not found — that leaves the tree magenta class " +
                "UNGUARDED. Treat it as a failure, not a pass.");
            Assert.AreEqual(
                shader, treeMat.shader,
                "Tree.mat is not using HiddenHarbours/TreeWind — the guard would be compiling the wrong shader.");

            int passes = treeMat.passCount > 0 ? treeMat.passCount : 1;
            for (int pass = 0; pass < passes; pass++)
                ShaderUtil.CompilePass(treeMat, pass, true);
            CollectMessages(errors, warnings, TreeMaterialPath, DescribeKeywords(treeMat),
                ShaderUtil.GetShaderMessages(treeMat.shader));

            if (warnings.Length > 0)
                Debug.Log("[TreeWindShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(
                errors.ToString(),
                "The tree shader reported a COMPILER ERROR (import and/or the shipped Tree.mat variant). This is " +
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
                    $"[{(isError ? "ERROR" : "WARN")}] tree shader (material '{materialPath}'" +
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
