using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the shared lit-sprite shader — the sibling of
    /// <see cref="TreeWindShaderCompileGuardTests"/> and <c>GrassShaderCompileGuardTests</c>. It
    /// force-reimports <c>HiddenHarboursLitSprite.shader</c> from its on-disk source AND force-compiles
    /// every shipped material variant, then FAILS RED on ANY shader compiler error.
    ///
    /// <para><b>Why this matters more here than for a one-material shader.</b> A shader error does NOT
    /// fail a normal test run — it is a magenta sprite at runtime and a green CI log. This shader is the
    /// one every future rig family attaches to, so a break in it takes the plants, the shrubs and the
    /// next consumer down together, and takes them down in a way that only shows on a GPU nobody in CI
    /// has. The classic traps: a '+' (or other operator character) in a <c>[Header(...)]</c> label or
    /// property string is a ShaderLab PARSE error, and an <c>[unroll]</c> over a runtime loop bound is an
    /// HLSL variant compile error. Both cost this project hours on the water shader.</para>
    /// </summary>
    public class LitSpriteShaderCompileGuardTests
    {
        const string LitShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursLitSprite.shader";
        const string ShorePlantMaterialPath = "Assets/_Project/Art/Materials/LitShorePlant.mat";
        const string ShrubMaterialPath = "Assets/_Project/Art/Materials/LitShrub.mat";

        static readonly string[] ShippedMaterials = { ShorePlantMaterialPath, ShrubMaterialPath };

        [Test]
        public void LitSpriteShader_CompilesEveryShippedVariant_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on the
            // "Unhandled log message" instead of via our own assertion (wrong reason, opaque message).
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                LitShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
            Assert.IsNotNull(
                shader,
                $"Could not load the lit-sprite shader at '{LitShaderPath}'. A missing or renamed shader " +
                "leaves the magenta class UNGUARDED for every rig family that attaches to it — treat it " +
                "as a failure, not a pass.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();
            CollectMessages(errors, warnings, "<import>", null, ShaderUtil.GetShaderMessages(shader));

            foreach (string path in ShippedMaterials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat,
                    $"The shipped material '{path}' was not found — that leaves its variant UNGUARDED.");
                Assert.AreEqual(shader, mat.shader,
                    $"'{path}' is not using HiddenHarbours/LitSprite — the guard would be compiling the " +
                    "wrong shader, and the material the planters assign would go unchecked.");

                int passes = mat.passCount > 0 ? mat.passCount : 1;
                for (int pass = 0; pass < passes; pass++)
                    ShaderUtil.CompilePass(mat, pass, true);
                CollectMessages(errors, warnings, path, DescribeKeywords(mat),
                    ShaderUtil.GetShaderMessages(mat.shader));
            }

            if (warnings.Length > 0)
                Debug.Log("[LitSpriteShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(
                errors.ToString(),
                "The lit-sprite shader reported a COMPILER ERROR (import and/or a shipped material " +
                "variant). This is the MAGENTA class — fix the shader until it imports and every shipped " +
                "variant compiles cleanly; do NOT silence this guard:\n" + errors);
        }

        /// <summary>
        /// The materials must EXPOSE the shared contract, and they must ship with the response ON.
        ///
        /// <para>⚠️ That is the opposite of <c>Tree.mat</c>'s call, deliberately. Tree.mat ships at 0
        /// because a forest was placed and approved under the FLAT look, and turning the response on
        /// would change art the owner already signed off. The plants and the shrubs have never been lit
        /// at all — shipping THEM at 0 would deliver the exact dead channel this work exists to fix.</para>
        /// </summary>
        [Test]
        public void TheDecorMaterials_ExposeTheSharedContract_AndShipTheResponseOn()
        {
            foreach (string path in ShippedMaterials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat, $"The shipped material '{path}' was not found.");

                foreach (string p in new[]
                         {
                             SpriteLightBinding.MaskProperty,
                             SpriteLightBinding.NormalProperty,
                             SpriteLightBinding.RimGateProperty,
                         })
                    Assert.IsTrue(mat.HasTexture(p),
                        $"{p} is gone from the shader, so '{path}' binds its baked sheets to nothing.");

                foreach (string p in new[]
                         {
                             SpriteLightBinding.RimGateChannelProperty,
                             SpriteLightBinding.RootProperty,
                         })
                    Assert.IsTrue(mat.HasVector(p), $"{p} is gone from the shader ('{path}').");

                foreach (string p in new[] { "_LightResponse", "_LightChannels", "_KeyRelight",
                                             "_RimSteerAmount", "_LightFrontBand", "_LightDepthBias",
                                             "_SunKeyStrength", "_SunRimStrength",
                                             "_LampKeyStrength", "_LampRimStrength", "_LampElevation" })
                    Assert.IsTrue(mat.HasFloat(p), $"{p} is gone from the shader ('{path}').");

                Assert.Greater(mat.GetFloat("_LightResponse"), 0f,
                    $"'{path}' ships the light response at 0, which draws the decor exactly as flat as it " +
                    "was before this work. The whole point of the change is that these rigs have been " +
                    "baking a light channel that NOTHING read.");

                Assert.AreEqual(0f, mat.GetFloat("_LightChannels"), 1e-6f,
                    $"'{path}' ships the channels flag raised. It is the 'no sheets bound' default and " +
                    "SpriteLightBinder raises it per renderer; a 1 here would make an unbound sprite read " +
                    "the opaque-black fallback as 'coverage everywhere'.");

                Assert.AreEqual(SpriteLightMath.DefaultFrontBand, mat.GetFloat("_LightFrontBand"), 1e-6f,
                    $"'{path}'s front/back changeover width has drifted from SpriteLightMath." +
                    "DefaultFrontBand, which is the value every test measures the rim against.");

                Assert.AreEqual(Vector4.zero, mat.GetVector(SpriteLightBinding.RimGateChannelProperty),
                    $"'{path}' ships a non-zero rim-gate selector. The selector is PER RENDERER (the " +
                    "species' own rig decides the channel) and a material-level one would be dotted " +
                    "against the fallback texture for every sprite that binds no gate sheet.");
            }
        }

        static void CollectMessages(
            StringBuilder errors, StringBuilder warnings, string materialPath, string keywords,
            ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] lit-sprite shader (material '{materialPath}'" +
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
