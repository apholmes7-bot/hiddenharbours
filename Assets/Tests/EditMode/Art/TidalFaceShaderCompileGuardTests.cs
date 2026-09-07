using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for <c>HiddenHarboursTidalFace.shader</c> — the quay-face twin of
    /// <see cref="CliffShaderCompileGuardTests"/> and <c>IsoFacetShaderCompileGuardTests</c>.
    ///
    /// <para>A shader error does NOT fail an ordinary test run, so without a guard a broken quay face
    /// would sail past green CI — and this is a bad one to ship magenta: the mooring face is 84 m of
    /// wall filling the bottom of the frame in the one region the owner plays, and a large flat magenta
    /// block reads as a deliberate colour choice rather than as an error for exactly as long as it takes
    /// somebody to open the scene.</para>
    ///
    /// <para><b>The NAME is asserted too, and that is not cosmetic:</b>
    /// <see cref="TidalFaceWaterline"/> finds this shader BY NAME at runtime, so a rename returns every
    /// wall to drawing dry through the tide with nothing but one warning in the log to say so.</para>
    /// </summary>
    public class TidalFaceShaderCompileGuardTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursTidalFace.shader";

        [Test]
        public void TidalFaceShader_CompilesItsVariants_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner fails on
            // "Unhandled log message" instead of through our own assertion (wrong reason, opaque text).
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                ShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader,
                $"Could not load '{ShaderPath}'. A missing or renamed shader leaves the quay-face " +
                "magenta class UNGUARDED — treat it as a failure, not a pass.");
            Assert.AreEqual(TidalFaceWaterline.ShaderName, shader.name,
                $"'{ShaderPath}' no longer declares '{TidalFaceWaterline.ShaderName}' — the Shader.Find " +
                "in TidalFaceWaterline would silently stop finding it and every wall would go back to " +
                "standing dry through the tide.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();
            Collect(errors, warnings, "<import>", ShaderUtil.GetShaderMessages(shader));

            var mat = new Material(shader);
            try
            {
                int passes = mat.passCount > 0 ? mat.passCount : 1;
                for (int pass = 0; pass < passes; pass++)
                    ShaderUtil.CompilePass(mat, pass, true);
                Collect(errors, warnings, ShaderPath, ShaderUtil.GetShaderMessages(shader));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }

            if (warnings.Length > 0)
                Debug.Log("[TidalFaceShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(errors.ToString(),
                "The quay-face shader reported a COMPILER ERROR. This is the MAGENTA class — fix the " +
                "shader until it imports and compiles cleanly; do NOT silence this guard:\n" + errors);
        }

        private static void Collect(StringBuilder errors, StringBuilder warnings, string where,
                                    ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] {where}\n    {msg.message}\n" +
                    (string.IsNullOrEmpty(msg.messageDetails) ? "" : $"    {msg.messageDetails}\n") +
                    $"    platform={msg.platform}  line={msg.line}";
                if (sink.ToString().IndexOf(line, System.StringComparison.Ordinal) < 0)
                    sink.AppendLine(line);
            }
        }
    }
}
