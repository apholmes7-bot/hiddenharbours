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
        private const string LightIncludePath = "Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl";

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

        /// <summary>
        /// The material must actually EXPOSE the light response, and it must ship OFF. Both halves
        /// matter: a shader that compiles but whose properties were renamed leaves every placed tree
        /// binding sheets nothing reads, and a response that shipped ON would change the look of an
        /// already-approved forest without anyone deciding to.
        /// </summary>
        [Test]
        public void TreeMaterial_ExposesTheLightResponse_AndShipsItOff()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterialPath);
            Assert.IsNotNull(mat, $"The tree material ('{TreeMaterialPath}') was not found.");

            foreach (string p in new[] { "_LightMask", "_LightNormal" })
                Assert.IsTrue(mat.HasTexture(p),
                    $"{p} is gone from the shader. TreeTrunkAnchor binds this per tree; without it " +
                    "every placed tree silently hands its baked sheets to nothing.");

            Assert.IsTrue(mat.HasVector("_SpriteRootWS"),
                "_SpriteRootWS is gone. It is how a tree tells the shader WHERE IT STANDS, and it " +
                "cannot be replaced by unity_ObjectToWorld: a SpriteRenderer's object-to-world is " +
                "IDENTITY, so every tree in the scene would light from the world origin.");

            foreach (string p in new[] { "_LightResponse", "_LightChannels", "_KeyRelight",
                                         "_RimSteerAmount", "_LightFrontBand", "_LightDepthBias",
                                         "_SunKeyStrength", "_SunRimStrength",
                                         "_LampKeyStrength", "_LampRimStrength", "_LampElevation" })
                Assert.IsTrue(mat.HasFloat(p), $"{p} is gone from the shader.");

            Assert.AreEqual(0f, mat.GetFloat("_LightResponse"), 1e-6f,
                "🔴 The light response must SHIP AT 0. Tree.mat's flat look is the look the owner has " +
                "already seen and placed a forest against; turning the response on is HIS call, one " +
                "slider away. Everything under it is tuned for _LightResponse 1, so raising it is a " +
                "single move — but raising it here is not this feature's decision to make.");
            // The changeover width is a LOOK decision measured against the sun's actual daylight arc,
            // so the headless twin names it and the material must agree — otherwise the shipped rim
            // and the tested rim are two different rims.
            Assert.AreEqual(HiddenHarbours.Art.SpriteLightMath.DefaultFrontBand,
                mat.GetFloat("_LightFrontBand"), 1e-6f,
                "Tree.mat's front/back changeover width has drifted from SpriteLightMath." +
                "DefaultFrontBand, which is the value every test measures the rim against.");

            Assert.AreEqual(0f, mat.GetFloat("_LightChannels"), 1e-6f,
                "The material-level channels flag is the 'no sheets bound' default; TreeTrunkAnchor " +
                "raises it per renderer. A 1 here would make an unbound tree read opaque black as " +
                "'coverage everywhere, no normal anywhere'.");
        }

        /// <summary>
        /// The shared include is a real file the shader really includes. It is its own asset (rocks and
        /// shoreline plants adopt the same contract next), so a rename or a move breaks the tree
        /// shader at COMPILE time — which the guard above catches — but this names the cause.
        /// </summary>
        [Test]
        public void TheSharedLightInclude_ExistsAndIsIncluded_WithItsChannelOrderStated()
        {
            Assert.IsTrue(System.IO.File.Exists(LightIncludePath),
                $"The shared sprite-light include is missing from '{LightIncludePath}'.");

            string shader = System.IO.File.ReadAllText(TreeShaderPath);
            StringAssert.Contains(LightIncludePath, shader,
                "HiddenHarboursTreeWind must include the SHARED response, not carry a private copy — " +
                "a second copy is exactly the duplicated-maths problem the design doc's HLSL-twin " +
                "discipline exists to prevent.");

            // 🔴 The channel order is the one thing a reader of this file must not have to guess: every
            // public description of the technique says green = front, blue = rim, and ours is not that.
            string include = System.IO.File.ReadAllText(LightIncludePath);
            foreach (string macro in new[] { "SPRITE_LIGHT_MASK_KEY", "SPRITE_LIGHT_MASK_RIM",
                                             "SPRITE_LIGHT_MASK_DEPTH", "SPRITE_LIGHT_MASK_COVERAGE" })
                StringAssert.Contains(macro, include,
                    $"{macro} is gone — the mask contract is no longer named at the point of use.");
            StringAssert.Contains("R = KEY LIGHT", include,
                "The include must SAY that R is the key light. This order costs a day when it is " +
                "inferred from the reference technique instead of read from here.");
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
