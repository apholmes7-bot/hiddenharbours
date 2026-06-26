using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD — force-imports + force-compiles every project-authored shader AND the exact keyword
    /// VARIANT(s) its shipped material(s) use, and FAILS RED on ANY shader compile error. The goal: a magenta /
    /// broken-shader build can never again slip past a green CI run.
    ///
    /// WHY THIS EXISTS (PR #96 -> #97 -> #98). PR #96 shipped the water shader with TWO independent defects, and
    /// BOTH reached main because nothing force-compiled the shader's shipped variant:
    ///   (1) an <c>[unroll]</c> over a RUNTIME loop count in <c>Fbm()</c> (broke the painted-keyword HLSL variant),
    ///       fixed in #97; and
    ///   (2) a '+' character inside a <c>[Header(...)]</c> label ("Wind chop + syncopation ..."), which ShaderLab's
    ///       lexer rejects ("unexpected $undefined, expecting TVAL_ID or TVAL_VARREF") — a PARSE error that
    ///       short-circuits import BEFORE the HLSL stage, so the whole shader rendered MAGENTA regardless of #97.
    /// CI stayed GREEN through all of it because a shader compile error does not, by itself, fail a test run, and
    /// nothing in the suites force-compiled the shipped variant. This test closes that gap permanently.
    ///
    /// ZERO TOLERANCE — NO BENIGN BASELINE. Any compiler ERROR on a project shader (its import state OR its
    /// shipped keyword variant) fails this test. There is deliberately no "known-benign" allow-list: a parse error
    /// MASKS the HLSL stage behind it (exactly what hid the #96 [unroll] error), so "tolerating" one is how a
    /// magenta build slips through. If the shader emits an error, the fix is to fix the shader — never to widen a
    /// tolerance here. (Warnings are reported but do NOT fail, so a cosmetic shader note can't redden main.)
    ///
    /// HOW IT RUNS HEADLESS. CI invokes Unity with <c>-batchmode</c> but WITHOUT <c>-nographics</c> (see
    /// .github/workflows/ci.yml). Shader compilation is driven by the Unity SHADER COMPILER subprocess and the
    /// asset-IMPORT pipeline — NOT a live GPU device — so a forced synchronous reimport drives the importer and
    /// <c>ShaderUtil.GetShaderMessages</c> reads back the result. <c>ShaderUtil.CompilePass</c> additionally forces
    /// the shipped material's exact keyword variant through the compiler. Verified in 6000.5.0f1 batch mode
    /// (cold-import AND warm runs): a deliberately broken shader fails RED here; the fixed shader passes GREEN.
    ///
    /// LOG INTERCEPTION. The importer logs a shader error to the console, which Unity's test runner would otherwise
    /// turn into an "Unhandled log message" failure — i.e. the test would fail for the WRONG reason (a log it
    /// didn't expect) instead of via our own explicit assertion. We set <c>LogAssert.ignoreFailingMessages</c> so
    /// OUR <c>ShaderUtil.GetShaderMessages</c> read is the single authority on pass/fail and the failure message
    /// names the actual shader/material/keywords/line.
    ///
    /// HOW WE IDENTIFY "OUR" SHADERS. In batch mode <c>Shader.name</c> can come back EMPTY, so we filter by the
    /// shader's ASSET PATH (project shaders live under <c>Assets/</c>, never <c>Packages/</c>), which resolves
    /// correctly headless. Today that is exactly <c>HiddenHarboursWater.shader</c> / <c>Water.mat</c>; any future
    /// project shader is covered automatically the moment it lands.
    /// </summary>
    public class WaterShaderCompileGuardTests
    {
        // The owner's hero water material — the one that rendered magenta in #96. Asserted present as a load-bearing
        // anchor: if discovery ever stops finding THIS, the guard would silently pass while guarding nothing.
        private const string WaterMaterialPath = "Assets/_Project/Art/Materials/Water.mat";

        [Test]
        public void ProjectShaders_CompileTheirShippedVariant_NoShaderErrors()
        {
            // The importer/compiler logs shader errors to the console. Without this, Unity's test runner fails the
            // test on the unexpected log line ("Unhandled log message") instead of via our assertion below — wrong
            // reason, opaque message. We own the verdict here through ShaderUtil.GetShaderMessages.
            LogAssert.ignoreFailingMessages = true;

            Dictionary<string, List<Material>> shaderToMaterials = MapProjectShadersToMaterials();

            Assert.IsNotEmpty(
                shaderToMaterials,
                "Found no project-authored shader (a shader asset under Assets/ used by a material). Expected at least " +
                "HiddenHarboursWater.shader via Water.mat — an empty set leaves the magenta class UNGUARDED, so this " +
                "is a failure, not a pass.");

            // Anchor: Water.mat (the #96 magenta material) MUST be among what we check. A missing anchor is a failure.
            bool waterAnchored = false;
            foreach (var kvp in shaderToMaterials)
                foreach (var m in kvp.Value)
                    if (AssetDatabase.GetAssetPath(m) == WaterMaterialPath) { waterAnchored = true; break; }
            Assert.IsTrue(
                waterAnchored,
                $"The hero water material ('{WaterMaterialPath}') was not found among project-shader materials. " +
                "That leaves the magenta class UNGUARDED — treat it as a failure, not a pass.");

            var errors = new StringBuilder();
            var warnings = new StringBuilder();
            int compiledVariants = 0;

            foreach (var kvp in shaderToMaterials)
            {
                string shaderPath = kvp.Key;
                List<Material> materials = kvp.Value;

                // Force a fresh synchronous reimport so the shader compiler re-runs from the on-disk source (no stale
                // cache can hide a fresh break or fake a fresh one), then read the shader ASSET'S import-time state.
                AssetDatabase.ImportAsset(
                    shaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                Assert.IsNotNull(shader, $"Could not load shader asset at '{shaderPath}'.");

                CollectMessages(errors, warnings, shaderPath, "<import>", null, ShaderUtil.GetShaderMessages(shader));

                // Force-compile each shipped material's EXACT keyword variant (catches a variant that PARSES but
                // fails to compile only under specific keywords/targets — the precise #96 [unroll] magenta class).
                foreach (Material mat in materials)
                {
                    int passes = mat.passCount > 0 ? mat.passCount : 1;
                    for (int pass = 0; pass < passes; pass++)
                    {
                        ShaderUtil.CompilePass(mat, pass, true);
                        compiledVariants++;
                    }
                    CollectMessages(
                        errors, warnings, shaderPath, AssetPathOf(mat), DescribeKeywords(mat),
                        ShaderUtil.GetShaderMessages(mat.shader));
                }
            }

            Assert.That(
                compiledVariants, Is.GreaterThan(0),
                "No shader variants were compile-checked — the guard did not actually run, which is as bad as a magenta build slipping through.");

            // Warnings are surfaced (so a regression toward noise is visible) but do NOT fail the build.
            if (warnings.Length > 0)
                Debug.Log("[WaterShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(
                errors.ToString(),
                "Project shader(s) reported a COMPILER ERROR (import and/or shipped keyword variant). This is the " +
                "MAGENTA class: a broken shader renders as Unity's magenta error colour, as in #96. A parse error " +
                "also MASKS the HLSL stage behind it. Fix the shader until it imports and every shipped variant " +
                "compiles cleanly — do NOT silence this guard:\n" + errors);
        }

        /// <summary>Splits shader messages into errors (fatal) and warnings (reported only). De-duplicates each.</summary>
        private static void CollectMessages(
            StringBuilder errors, StringBuilder warnings, string shaderPath, string materialPath, string keywords,
            ShaderMessage[] messages)
        {
            foreach (ShaderMessage msg in messages)
            {
                bool isError = msg.severity == ShaderCompilerMessageSeverity.Error;
                StringBuilder sink = isError ? errors : warnings;
                string line =
                    $"[{(isError ? "ERROR" : "WARN")}] shader '{shaderPath}' (material '{materialPath}'" +
                    (keywords != null ? $", keywords: {keywords}" : "") + $")\n    {msg.message}\n" +
                    (string.IsNullOrEmpty(msg.messageDetails) ? "" : $"    {msg.messageDetails}\n") +
                    $"    platform={msg.platform}  line={msg.line}";
                if (sink.ToString().IndexOf(line, System.StringComparison.Ordinal) < 0)
                    sink.AppendLine(line);
            }
        }

        /// <summary>
        /// Builds {shaderAssetPath -> materials using it} for every PROJECT-AUTHORED shader (its shader asset lives
        /// under <c>Assets/</c>). Asset-path based so it resolves in batch mode where <c>Shader.name</c> is empty.
        /// </summary>
        private static Dictionary<string, List<Material>> MapProjectShadersToMaterials()
        {
            var map = new Dictionary<string, List<Material>>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null || mat.shader == null) continue;

                string shaderPath = AssetDatabase.GetAssetPath(mat.shader);
                if (!IsProjectShader(shaderPath)) continue;

                if (!map.TryGetValue(shaderPath, out var list))
                {
                    list = new List<Material>();
                    map[shaderPath] = list;
                }
                list.Add(mat);
            }
            return map;
        }

        // Project-authored shader sources live under Assets/ (built-in/package shaders live under Packages/ or have
        // no asset path). We only guard shaders WE author — package shaders are Unity's to keep compiling.
        private static bool IsProjectShader(string shaderPath)
        {
            return !string.IsNullOrEmpty(shaderPath) && shaderPath.StartsWith("Assets/");
        }

        private static string AssetPathOf(Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? obj.name : path;
        }

        private static string DescribeKeywords(Material mat)
        {
            string[] kw = mat.shaderKeywords;
            return (kw == null || kw.Length == 0) ? "<none>" : string.Join(" ", kw);
        }
    }
}
