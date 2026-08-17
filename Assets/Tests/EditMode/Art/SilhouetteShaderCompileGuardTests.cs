using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// MAGENTA GUARD for the foliage-silhouette family (owner ruling, 2026-08-16) — the twin of
    /// <see cref="IsoFacetShaderCompileGuardTests"/>, and it covers THREE shaders because the
    /// mechanism spans three:
    ///
    /// <list type="number">
    /// <item><b>The mask</b> — <c>HiddenHarbours/SilhouetteMask</c>, which stamps the fisher's shape
    /// into the screen texture. Found BY NAME at runtime by <c>SilhouetteMaskSource</c>, so the name
    /// assertion below is not cosmetic: a rename silently returns dense woods to swallowing her, with
    /// only one error in the log to say so.</item>
    /// <item><b>The tree</b> and <b>the lit sprite</b> — the two consumers. They are here because this
    /// change edits their fragment stages and adds a texture declaration to the include they SHARE
    /// (Include/SpriteLitDecor.hlsl). A broken declaration there would take out every tree, shrub and
    /// shoreline plant in both regions at once, which is the largest magenta blast radius in the
    /// project.</item>
    /// </list>
    ///
    /// <para><b>Why compiling is the right headless check.</b> Same argument the IsoFacet guard makes:
    /// CI has no graphics device and can never RENDER foliage (the rendering acceptance lives in
    /// <c>FoliageSilhouettePlayTests</c> and skips loudly on the Null device) — but compiling is not
    /// rendering, runs headless, and catches the whole magenta class: ShaderLab parse errors, HLSL
    /// variant errors, a broken include path.</para>
    /// </summary>
    public class SilhouetteShaderCompileGuardTests
    {
        private static readonly (string path, string name)[] Shaders =
        {
            ("Assets/_Project/Art/Shaders/HiddenHarboursSilhouetteMask.shader",
             "HiddenHarbours/SilhouetteMask"),
            // The consumers. Both call SilhouetteThrough from the shared include; if that function or
            // its two global declarations are malformed, this is where it shows up headless.
            ("Assets/_Project/Art/Shaders/HiddenHarboursTreeWind.shader",
             "HiddenHarbours/TreeWind"),
            ("Assets/_Project/Art/Shaders/HiddenHarboursLitSprite.shader",
             "HiddenHarbours/LitSprite"),
        };

        [Test]
        public void SilhouetteShaders_CompileTheirVariants_NoShaderErrors()
        {
            // The importer logs shader errors to the console; without this the runner would fail on
            // "Unhandled log message" instead of via our own assertion.
            LogAssert.ignoreFailingMessages = true;

            var errors = new StringBuilder();
            var warnings = new StringBuilder();

            foreach (var (path, name) in Shaders)
            {
                AssetDatabase.ImportAsset(
                    path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.IsNotNull(shader,
                    $"Could not load '{path}'. A missing/renamed shader leaves the foliage-silhouette " +
                    "magenta class UNGUARDED — treat it as a failure, not a pass.");
                Assert.AreEqual(name, shader.name,
                    $"'{path}' no longer declares shader '{name}' — Shader.Find in " +
                    "SilhouetteMaskSource would silently stop finding it and the fisher would go back " +
                    "to being lost behind the trees.");

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
                Debug.Log("[SilhouetteShaderCompileGuard] Non-fatal shader warnings:\n" + warnings);

            Assert.IsEmpty(errors.ToString(),
                "A foliage-silhouette shader reported a COMPILER ERROR. This is the MAGENTA class — " +
                "fix the shader until it imports and compiles cleanly; do NOT silence this guard:\n"
                + errors);
        }

        /// <summary>
        /// ⚠️ THE MASK MUST NEVER BE VISIBLE. It is an ordinary <see cref="SpriteRenderer"/> parented to
        /// the fisher and sitting in the scene like any other; the ONLY thing keeping it out of the
        /// picture is that its shader has no pass the visible sprite queue will draw. If a
        /// <c>Universal2D</c> pass were ever added — by a copy-paste from the deck-occluded sprite it is
        /// modelled on, or by someone "fixing" it so it previews in the editor — the fisher would grow a
        /// flat white duplicate of herself, drawn over the ground, in every scene.
        ///
        /// <para>Pinned textually because there is no cheaper honest observable: a renderer that draws
        /// nothing and a renderer that is merely covered up look identical in a screenshot, and the
        /// PlayMode fixture's canopy would hide exactly this defect.</para>
        /// </summary>
        [Test]
        public void TheMaskShader_HasNoPassTheVisibleQueueWillDraw()
        {
            const string path = "Assets/_Project/Art/Shaders/HiddenHarboursSilhouetteMask.shader";
            string src = System.IO.File.ReadAllText(
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path));

            StringAssert.Contains("\"LightMode\" = \"HHSilhouette\"", src,
                "the mask pass has lost its LightMode tag — the feature's filtered list would find " +
                "nothing and the mask would be empty");
            StringAssert.DoesNotContain("Universal2D", src,
                "the mask shader has gained a Universal2D pass, so the hidden child now DRAWS: the " +
                "fisher would carry a flat white duplicate of herself over the ground in every scene.");
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
