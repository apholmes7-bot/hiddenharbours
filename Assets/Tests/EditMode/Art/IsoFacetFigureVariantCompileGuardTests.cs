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
    /// MAGENTA GUARD for <c>HiddenHarbours/IsoFacet</c> in ALL THREE of its keyword states: none (every
    /// hull without a room and every rig 7 figure), <c>HH_LEVEL_GATE</c> (a hull that carries a room) and
    /// <c>HH_FIGURE</c> (a v9 character). It force-reimports the shader from its on-disk source,
    /// force-compiles every pass of each state on a transient material, and FAILS RED on any compiler
    /// error.
    ///
    /// <para><see cref="IsoFacetShaderCompileGuardTests"/> compiles the family's DEFAULT variant only (a
    /// bare <c>new Material(shader)</c>), so a keyword variant that failed to compile would pass it, and
    /// would draw magenta on exactly the hulls and figures that turn the keyword on. There is no shipped
    /// material to anchor either state (both renderers build theirs at runtime), so each state is compiled
    /// on its own <c>new Material(shader)</c>, as that guard does.</para>
    ///
    /// <para><b>The keyword control</b> is <see cref="CliffShaderCompileGuardTests"/>'s: each keyword is
    /// first proved to EXIST in the shader's keyword space. A renamed or dropped keyword would otherwise
    /// make <c>EnableKeyword</c> a no-op, and this guard would compile the default variant three times
    /// and call it three states. There is no broken-shader control: nothing in this repo builds a shader
    /// from text, and the keyword control is what proves the guard compiled what it names.</para>
    /// </summary>
    public class IsoFacetFigureVariantCompileGuardTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader";
        const string ShaderName = "HiddenHarbours/IsoFacet";

        // The shader's one multi_compile_local set, and the three states it compiles to (null = none).
        static readonly string[] Keywords =
        {
            IsoFacetShaderIds.LevelGateKeyword,
            IsoFacetFigureShaderIds.FigureKeyword,
        };

        static readonly string[] States =
        {
            null,
            IsoFacetShaderIds.LevelGateKeyword,
            IsoFacetFigureShaderIds.FigureKeyword,
        };

        [Test]
        public void IsoFacet_CompilesAllThreeKeywordStates_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on
            // "Unhandled log message" instead of via our own assertion (wrong reason, opaque message).
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                ShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader,
                $"Could not load '{ShaderPath}'. A missing/renamed shader leaves every facet variant " +
                "UNGUARDED — treat it as a failure, not a pass.");
            Assert.AreEqual(ShaderName, shader.name,
                $"'{ShaderPath}' no longer declares '{ShaderName}' — the hull and figure renderers find it " +
                "by that name.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();
            Collect(errors, warnings, "<import>", ShaderUtil.GetShaderMessages(shader));

            foreach (string kw in Keywords)
                Assert.IsTrue(shader.keywordSpace.FindKeyword(kw).isValid,
                    $"HiddenHarbours/IsoFacet declares no '{kw}' keyword. Its state would compile the default " +
                    "variant again, and the renderer that turns it on would set a keyword nothing reads.");

            foreach (string state in States)
            {
                var mat = new Material(shader);
                try
                {
                    foreach (string kw in Keywords)
                    {
                        if (kw == state) mat.EnableKeyword(kw);
                        else mat.DisableKeyword(kw);
                    }
                    foreach (string kw in Keywords)
                        Assert.AreEqual(kw == state, mat.IsKeywordEnabled(kw),
                            $"Could not put the transient material in the {NameOf(state)} state ('{kw}').");

                    int passes = mat.passCount > 0 ? mat.passCount : 1;
                    for (int pass = 0; pass < passes; pass++)
                        ShaderUtil.CompilePass(mat, pass, true);
                    Collect(errors, warnings, NameOf(state), ShaderUtil.GetShaderMessages(shader));
                }
                finally
                {
                    Object.DestroyImmediate(mat);
                }
            }

            if (warnings.Length > 0)
                Debug.Log("[IsoFacetFigureVariantCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(errors.ToString(),
                "HiddenHarbours/IsoFacet reported a COMPILER ERROR in one of its keyword states. This is " +
                "the MAGENTA class — fix the shader until every state imports and compiles cleanly; do NOT " +
                "silence this guard:\n" + errors);
        }

        static string NameOf(string state) => state ?? "no-keyword";

        static void Collect(StringBuilder errors, StringBuilder warnings, string state, ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] {ShaderPath} ({state})\n    {msg.message}\n" +
                    (string.IsNullOrEmpty(msg.messageDetails) ? "" : $"    {msg.messageDetails}\n") +
                    $"    platform={msg.platform}  line={msg.line}";
                if (sink.ToString().IndexOf(line, System.StringComparison.Ordinal) < 0)
                    sink.AppendLine(line);
            }
        }
    }
}
