using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the three mesh-hull shaders (ADR 0022 phase 3) — the IsoFacet twin of
    /// <see cref="WaterShaderCompileGuardTests"/>. Force-reimports each shader from its on-disk
    /// source AND force-compiles its default variant, failing RED on ANY compiler error.
    ///
    /// <para><b>Why compiling is the right headless check.</b> CI has no graphics device, so it
    /// can never RENDER a hull (the real rendering acceptance lives in IsoFacetUrpPassTests and
    /// skips loudly on the Null device) — but compiling is not rendering, runs headless, and
    /// catches the whole magenta class: ShaderLab parse errors, HLSL variant errors, a broken
    /// include path in the Blit-based resolve. There are no shipped .mat assets for these (the
    /// materials are built at runtime per hull), so the guard compiles a transient material per
    /// shader instead of anchoring a .mat.</para>
    /// </summary>
    public class IsoFacetShaderCompileGuardTests
    {
        private static readonly (string path, string name)[] Shaders =
        {
            ("Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader",
             "HiddenHarbours/IsoFacet"),
            ("Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetResolve.shader",
             "Hidden/HiddenHarbours/IsoFacetResolve"),
            ("Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetOverlay.shader",
             "HiddenHarbours/IsoFacetOverlay"),
        };

        [Test]
        public void IsoFacetShaders_CompileTheirVariants_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail
            // on "Unhandled log message" instead of via our own assertion.
            LogAssert.ignoreFailingMessages = true;

            var errors = new StringBuilder();
            var warnings = new StringBuilder();

            foreach (var (path, name) in Shaders)
            {
                AssetDatabase.ImportAsset(
                    path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.IsNotNull(shader,
                    $"Could not load '{path}'. A missing/renamed shader leaves the mesh-hull " +
                    "magenta class UNGUARDED — treat it as a failure, not a pass.");
                Assert.AreEqual(name, shader.name,
                    $"'{path}' no longer declares shader '{name}' — Shader.Find in " +
                    "IsoFacetHullRenderer/IsoFacetHullFeature would silently stop finding it.");

                Collect(errors, warnings, path, ShaderUtil.GetShaderMessages(shader));

                var mat = new Material(shader);
                try
                {
                    int passes = mat.passCount > 0 ? mat.passCount : 1;
                    for (int pass = 0; pass < passes; pass++)
                        ShaderUtil.CompilePass(mat, pass, true);
                    Collect(errors, warnings, path, ShaderUtil.GetShaderMessages(shader));
                }
                finally
                {
                    Object.DestroyImmediate(mat);
                }
            }

            if (warnings.Length > 0)
                Debug.Log("[IsoFacetShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(errors.ToString(),
                "A mesh-hull shader reported a COMPILER ERROR. This is the MAGENTA class — fix " +
                "the shader until it imports and compiles cleanly; do NOT silence this guard:\n" + errors);
        }

        private static void Collect(StringBuilder errors, StringBuilder warnings, string path,
                                    ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] {path}\n    {msg.message}\n" +
                    (string.IsNullOrEmpty(msg.messageDetails) ? "" : $"    {msg.messageDetails}\n") +
                    $"    platform={msg.platform}  line={msg.line}";
                if (sink.ToString().IndexOf(line, System.StringComparison.Ordinal) < 0)
                    sink.AppendLine(line);
            }
        }
    }
}
