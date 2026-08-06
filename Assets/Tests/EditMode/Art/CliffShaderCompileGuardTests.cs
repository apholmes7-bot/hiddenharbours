using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the cliff face shader — the cliff twin of
    /// <see cref="GrassShaderCompileGuardTests"/>. It force-reimports
    /// <c>HiddenHarboursCliffFace.shader</c> from its on-disk source AND force-compiles the shipped
    /// <c>CliffFace.mat</c> keyword variant, then FAILS RED on ANY shader compiler error.
    ///
    /// <para>The two traps this catches have each cost this project hours: a <c>+</c> (or other
    /// operator character) inside a <c>[Header(...)]</c> label or a property display string is a
    /// ShaderLab PARSE error → magenta; and an <c>[unroll]</c> over a runtime loop bound is an HLSL
    /// variant compile error → magenta. A shader error does NOT fail a normal test run, and nothing
    /// else force-compiles the shipped variant, so without this a magenta cliff would sail past green
    /// CI — and a magenta cliff is worse than most, because the wall is a large flat area that reads as
    /// a deliberate colour block rather than as an error.</para>
    ///
    /// <para><b>Observed failing under sabotage</b> (both re-verified by hand before this landed):
    /// inserting <c>_Sky ("Sky floor plus ambient", Range(0,1)) = 0.3</c> — a bare <c>plus</c> written
    /// as the operator character — into Properties fails this test with a ShaderLab parse error; and
    /// renaming <c>_SkyFloor</c> in the CBUFFER but not in Properties fails it with an HLSL
    /// undeclared-identifier error.</para>
    /// </summary>
    public class CliffShaderCompileGuardTests
    {
        const string CliffShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursCliffFace.shader";
        const string CliffMaterialPath = "Assets/_Project/Art/Materials/CliffFace.mat";

        [Test]
        public void CliffShader_CompilesItsShippedVariant_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on
            // "Unhandled log message" instead of via our own assertion (wrong reason, opaque message).
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                CliffShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CliffShaderPath);
            Assert.IsNotNull(
                shader,
                $"Could not load the cliff shader at '{CliffShaderPath}'. A missing/renamed shader " +
                "leaves the cliff magenta class UNGUARDED — treat it as a failure, not a pass.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();
            CollectMessages(errors, warnings, "<import>", null, ShaderUtil.GetShaderMessages(shader));

            var cliffMat = AssetDatabase.LoadAssetAtPath<Material>(CliffMaterialPath);
            Assert.IsNotNull(
                cliffMat,
                $"The cliff material ('{CliffMaterialPath}') was not found — that leaves the cliff " +
                "magenta class UNGUARDED. Treat it as a failure, not a pass.");
            Assert.AreEqual(
                shader, cliffMat.shader,
                "CliffFace.mat is not using HiddenHarbours/CliffFace — the guard would be compiling " +
                "the wrong shader.");

            int passes = cliffMat.passCount > 0 ? cliffMat.passCount : 1;
            for (int pass = 0; pass < passes; pass++)
                ShaderUtil.CompilePass(cliffMat, pass, true);
            CollectMessages(errors, warnings, CliffMaterialPath, DescribeKeywords(cliffMat),
                            ShaderUtil.GetShaderMessages(cliffMat.shader));

            if (warnings.Length > 0)
                Debug.Log("[CliffShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(
                errors.ToString(),
                "The cliff shader reported a COMPILER ERROR (import and/or the shipped CliffFace.mat " +
                "variant). This is the MAGENTA class — fix the shader until it imports and every " +
                "shipped variant compiles cleanly; do NOT silence this guard:\n" + errors);
        }

        /// <summary>
        /// ⭐ <b>THE SHIPPED MATERIAL'S TEXTURE SLOTS MUST STAY EMPTY — the brow and toe decals depend
        /// on it, and nothing else would say so.</b>
        ///
        /// <para>The kit's decal strips ship ONE pre-lit RGBA channel (README §8: they are fixed-sun by
        /// nature). They ride this same shader with only <c>_Unlit</c> pushed, so <c>_Normal</c> and
        /// <c>_Mask</c> fall through to the shader's own declared defaults — a flat "bump" and a white
        /// mask — and the lighting maths collapses to exactly the right thing: a decal lit as a FLAT
        /// texel of the wall it sits on. That is what buys this project a decal path with no second
        /// cliff shader to force-compile.</para>
        ///
        /// <para><b>The failure it guards is silent.</b> Assign any texture to those slots in the
        /// inspector and every decal on the coast is suddenly shaded by some unrelated rock's normals and
        /// cast shadow — which renders, and renders wrong, and looks like the decals were drawn badly.
        /// Property blocks override per renderer, so the FACE bands would be untouched and only the
        /// decals would go strange.</para>
        /// </summary>
        [Test]
        public void TheShippedMaterialLeavesNormalAndMaskUnset_WhichIsWhatLightsTheDecals()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CliffMaterialPath);
            Assert.IsNotNull(mat, $"The cliff material ('{CliffMaterialPath}') was not found.");

            foreach (string slot in new[] { "_Unlit", "_Normal", "_Mask" })
            {
                Assert.IsTrue(mat.HasProperty(slot),
                    $"CliffFace.mat has no {slot} property — the wall pushes it per chunk through a " +
                    "property block and would be pushing into nothing");
                Assert.IsNull(mat.GetTexture(slot),
                    $"CliffFace.mat carries a texture in {slot}. Every channel arrives PER CHUNK through " +
                    "a MaterialPropertyBlock (the bake root is gitignored, so there is nothing to assign " +
                    "here anyway) — and the brow/toe decals rely on _Normal and _Mask falling through to " +
                    "the shader's flat-bump and white defaults to be lit as flat texels of their wall.");
            }
        }

        static void CollectMessages(StringBuilder errors, StringBuilder warnings, string materialPath,
                                    string keywords, ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] cliff shader (material '{materialPath}'" +
                    (keywords != null ? $", keywords: {keywords}" : "") + $")\n    {msg.message}\n" +
                    (string.IsNullOrEmpty(msg.messageDetails) ? "" : $"    {msg.messageDetails}\n") +
                    $"    platform={msg.platform}  line={msg.line}";
                if (sink.ToString().IndexOf(line, System.StringComparison.Ordinal) < 0)
                    sink.AppendLine(line);
            }
        }

        static string DescribeKeywords(Material mat)
        {
            string[] kw = mat.shaderKeywords;
            return (kw == null || kw.Length == 0) ? "<none>" : string.Join(" ", kw);
        }
    }
}
