using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the deck-occupant shader — the crew twin of
    /// <see cref="GrassShaderCompileGuardTests"/>. It force-reimports
    /// <c>HiddenHarboursHullDeckSprite.shader</c> from its on-disk source and force-compiles its one
    /// pass, then FAILS RED on any shader compiler error.
    ///
    /// <para><b>This shader needs the guard MORE than most, because it has no material asset.</b>
    /// <c>HullDeckOccupant</c> builds its material at runtime through <c>Shader.Find</c> (exactly as
    /// <c>IsoFacetHullRenderer</c> does for the facet and overlay shaders), so nothing in the project
    /// references it, nothing force-compiles it, and a shader error does not fail an ordinary test
    /// run. Without this, a broken deck shader would sail past green CI and land as a magenta slab
    /// where the fisher should be — inside the hull's own image, where it would also be composited
    /// over the boat.</para>
    ///
    /// <para>The classic traps, both of which have cost this project hours: an operator character in
    /// a property display string is a ShaderLab PARSE error, and an <c>[unroll]</c> over a runtime
    /// bound is an HLSL variant error. Neither shows up as anything but magenta at runtime.</para>
    /// </summary>
    public class HullDeckSpriteShaderCompileGuardTests
    {
        private const string DeckShaderPath =
            "Assets/_Project/Art/Shaders/HiddenHarboursHullDeckSprite.shader";
        private const string DeckShaderName = "HiddenHarbours/HullDeckSprite";

        [Test]
        public void HullDeckSpriteShader_Compiles_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner fails on the
            // "Unhandled log message" instead of through our own assertion (wrong reason, opaque text).
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                DeckShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DeckShaderPath);
            Assert.IsNotNull(shader,
                $"Could not load the deck-occupant shader at '{DeckShaderPath}'. A missing or renamed " +
                "shader leaves the crew drawing in-scene again — un-occluded by the cabin they stand " +
                "in — and leaves this magenta class UNGUARDED. Treat it as a failure, not a pass.");

            // The runtime finds it by NAME, so a renamed shader is as broken as a missing one even
            // when the file still compiles perfectly.
            Assert.AreEqual(DeckShaderName, shader.name,
                $"The shader at '{DeckShaderPath}' no longer declares '{DeckShaderName}'. " +
                "HullDeckOccupant.ShaderName finds it by that exact string; renaming one without the " +
                "other silently disables the whole per-pixel occlusion path at runtime.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();
            Collect(errors, warnings, "<import>", ShaderUtil.GetShaderMessages(shader));

            // No shipped .mat references this shader, so the guard makes the variant itself — which is
            // exactly what HullDeckOccupant does at runtime.
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                int passes = mat.passCount > 0 ? mat.passCount : 1;
                for (int pass = 0; pass < passes; pass++)
                    ShaderUtil.CompilePass(mat, pass, true);
                Collect(errors, warnings, "<runtime variant>", ShaderUtil.GetShaderMessages(shader));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }

            if (warnings.Length > 0)
                Debug.Log("[HullDeckSpriteShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(errors.ToString(),
                "The deck-occupant shader reported a COMPILER ERROR. This is the MAGENTA class — fix " +
                "the shader until it imports and compiles cleanly; do NOT silence this guard:\n" + errors);
        }

        private static void Collect(StringBuilder errors, StringBuilder warnings, string what,
                                    ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] deck-occupant shader ({what})\n    {msg.message}\n" +
                    (string.IsNullOrEmpty(msg.messageDetails) ? "" : $"    {msg.messageDetails}\n") +
                    $"    platform={msg.platform}  line={msg.line}";
                if (sink.ToString().IndexOf(line, System.StringComparison.Ordinal) < 0)
                    sink.AppendLine(line);
            }
        }
    }
}
