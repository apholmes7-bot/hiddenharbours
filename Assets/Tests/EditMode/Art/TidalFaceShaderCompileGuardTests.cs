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
        const string MaterialPath =
            "Assets/_Project/Resources/" + TidalFaceWaterline.MaterialResourceName + ".mat";

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

            // ⭐⭐ THE SHIPPED MATERIAL, not a fresh one — and its EXISTENCE is half of what this guards.
            // A shader nothing references is stripped from a player build: the face would be perfect in
            // the editor and magenta on the owner's PC. Living under Resources is what keeps it in, so
            // "the asset is gone" and "the shader is broken" are the same class of defect and are caught
            // by the same test. TidalFaceWaterline loads it by this name at runtime.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(mat,
                $"The shipped quay-face material ('{MaterialPath}') is missing. It is what keeps " +
                $"'{TidalFaceWaterline.ShaderName}' out of the build stripper's way — without it every " +
                "quay face draws magenta in a player build while looking perfect in the editor. Treat " +
                "this as a failure, not a pass.");
            Assert.AreEqual(shader, mat.shader,
                $"'{MaterialPath}' is not using '{TidalFaceWaterline.ShaderName}' — the guard would be " +
                "compiling the wrong shader, and the runtime load would hand every face the wrong one.");

            int passes = mat.passCount > 0 ? mat.passCount : 1;
            for (int pass = 0; pass < passes; pass++)
                ShaderUtil.CompilePass(mat, pass, true);
            Collect(errors, warnings, MaterialPath, ShaderUtil.GetShaderMessages(shader));

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
