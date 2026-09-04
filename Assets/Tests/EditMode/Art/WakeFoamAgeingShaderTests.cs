using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The TWIN GUARD for the wake foam's age ramp (owner ask 2026-08-27).
    ///
    /// <para>The ramp exists on both sides of the sea: <see cref="WakeFoamAgeing"/> shades the particle
    /// wake, and a transcription in <c>HiddenHarboursWater.shader</c> shades the advected foam buffer
    /// (from its FRESHNESS channel — see <see cref="TheAgeProxy_IsTheBuffersFreshnessChannel"/> for why
    /// the round-1 coverage proxy was replaced).
    /// Two halves of one look is exactly the shape that drifts silently — this repo has paid for a
    /// twin-parity test that pinned a twin against itself rather than against the thing that actually
    /// draws, so these guards read the SHADER SOURCE and the C# SOURCE and compare them to each other.</para>
    ///
    /// <para>CPU-only by construction: no GPU, no render, no device — so CI adjudicates it, which is the
    /// whole point of a guard against a change nobody will notice until the owner looks at the water.</para>
    /// </summary>
    public class WakeFoamAgeingShaderTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string TwinPath = "Assets/_Project/Code/Core/Environment/WakeFoamAgeing.cs";

        static string Read(string projectRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", projectRelative);
            Assert.IsTrue(File.Exists(path), $"Not found: {projectRelative}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Reduce one function body to the shape both languages share, so a real difference in the MATHS
        /// fails and a difference in spelling does not. HLSL's <c>saturate</c> is <c>Mathf.Clamp01</c>,
        /// its <c>lerp</c> is <c>Color.Lerp</c>, its <c>max</c>/<c>min</c> are <c>Mathf.Max</c>/
        /// <c>Mathf.Min</c>, and its literals carry no <c>f</c> suffix and often a redundant <c>.0</c>. Everything else — the comparisons, the knots, the divisors, the halves —
        /// must match character for character after that.
        /// </summary>
        static string Normalize(string body)
        {
            string s = Regex.Replace(body, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            s = Regex.Replace(s, @"//[^\n]*", " ");
            s = s.Replace("Mathf.Clamp01", "saturate").Replace("Mathf.Clamp", "clamp");
            s = s.Replace("Mathf.Max", "max").Replace("Mathf.Min", "min");
            s = s.Replace("Color.Lerp", "lerp");
            s = Regex.Replace(s, @"\s+", "");
            s = Regex.Replace(s, @"([0-9.])f(?![A-Za-z0-9_])", "$1");   // 1f -> 1, 1e-4f -> 1e-4
            s = Regex.Replace(s, @"(\d)\.0(?![0-9])", "$1");            // 1.0 -> 1, 2.0 -> 2
            return s.ToLowerInvariant();
        }

        /// <summary>Pull one function's body (between its first { and its matching }) out of a source file.</summary>
        static string Body(string source, string signatureStart)
        {
            int at = source.IndexOf(signatureStart, System.StringComparison.Ordinal);
            Assert.Greater(at, -1,
                $"'{signatureStart}' is gone. It is one half of a twin seam — if the ramp moved, BOTH " +
                "halves move in the same PR, and this guard is how that is enforced.");
            int open = source.IndexOf('{', at);
            Assert.Greater(open, -1);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open + 1, i - open - 1);
                }
            }
            Assert.Fail($"Unbalanced braces after '{signatureStart}'.");
            return null;
        }

        // ==== the twin ================================================================================

        [Test]
        public void TheKnotCurve_IsTranscribedLineForLine()
        {
            string hlsl = Body(Read(ShaderPath),
                "float WakeFoamKnots(float t01, float whiteHold, float blueReach, float deepReach)");
            string csharp = Body(Read(TwinPath),
                "public static float Knots(float t01, float whiteHold, float blueReach, float deepReach)");

            Assert.AreEqual(Normalize(csharp), Normalize(hlsl),
                "The water shader's age curve has drifted from WakeFoamAgeing.Knots. The particle wake " +
                "and the advected foam buffer are two halves of ONE look — when they age differently, " +
                "the churn behind a boat is two different colours of water meeting along the edge of a " +
                "render target, and nothing on a CPU-only CI would ever see it.");
        }

        [Test]
        public void TheThreeStopRamp_IsTranscribedLineForLine()
        {
            string hlsl = Body(Read(ShaderPath),
                "float3 WakeFoamRamp3(float age01, float3 foam, float3 shallow, float3 mid)");
            string csharp = Body(Read(TwinPath),
                "public static Color Ramp3(float age01, Color foam, Color shallow, Color mid)");

            Assert.AreEqual(Normalize(csharp), Normalize(hlsl),
                "The shader's palette lookup has drifted from WakeFoamAgeing.Ramp3.");
        }

        // ==== the seam's numbers ======================================================================

        [Test]
        public void TheShaderKnotDefaults_MatchTheShippedRamp()
        {
            string src = Read(ShaderPath);
            var d = WakeAgeRamp.Default;

            Assert.AreEqual(d.WhiteHold, PropertyDefault(src, "_WakeFoamWhiteHold"), 1e-4f,
                "The shader's knot defaults and the C# ramp's are one look, tuned once. Two defaults " +
                "means the buffer's foam and the particle foam turn blue at different ages.");
            Assert.AreEqual(d.BlueReach, PropertyDefault(src, "_WakeFoamBlueReach"), 1e-4f);
            Assert.AreEqual(d.DeepReach, PropertyDefault(src, "_WakeFoamDeepReach"), 1e-4f);
        }

        static float PropertyDefault(string shaderSource, string property)
        {
            // Anchor on the DECLARATION form — the name, then its display-name string literal, then the
            // default after the '='. A looser pattern matches the shader's own COMMENTS about these
            // properties (e.g. "_WakeFoamAgeStrength = 0 returns ...") and reads a number out of prose;
            // a `\([^)]*\)` pattern is worse still, because it stops at the ')' inside `Range(0,1)` and
            // silently never matches at all — which is a guard that passes by not looking.
            Match m = Regex.Match(shaderSource,
                Regex.Escape(property) + @"\s*\(\s*""[^""]*""\s*,[^=\n]*=\s*([0-9.eE+-]+)");
            Assert.IsTrue(m.Success, $"{property} is gone from the water shader (or its declaration no " +
                                     "longer carries a display name and a default).");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        // ==== the defect, and the revert ==============================================================

        [Test]
        public void TheWakeCompose_NoLongerLerpsToOneFlatWhite()
        {
            string src = Read(ShaderPath);

            // THE DEFECT the owner reported: "the wakes behind the boat are still a solid white foam from
            // wherever the boat interacts with". That was this one line composing every texel of the
            // buffer — fresh churn and week-old drift alike — toward a single constant.
            Assert.IsFalse(
                Regex.IsMatch(src, @"lerp\s*\(\s*col\.rgb\s*,\s*_FoamColor\.rgb\s*,\s*saturate\s*\(\s*wakeFoam"),
                "The advected foam buffer is composing toward a single flat _FoamColor again. That is the " +
                "2026-08-27 defect verbatim: the buffer already knows each texel's age (it DECAYS), and " +
                "the compose is where that information was being thrown away.");

            Assert.IsTrue(Regex.IsMatch(src, @"WakeFoamAgedColor\s*\(\s*wakeFresh\s*\)"),
                "The wake compose must run the buffer's FRESHNESS through the age ramp — that is what " +
                "makes the churn walk through the sea's blues instead of sitting on white. Passing the " +
                "coverage instead is the round-1 defect (see TheAgeProxy_IsTheBuffersFreshnessChannel).");

            Assert.IsTrue(Regex.IsMatch(src, @"saturate\s*\(\s*wakeFoam\s*\)\s*\*\s*_FoamColor\.a"),
                "…while the COVERAGE stays the compose WEIGHT. The two channels do different jobs: " +
                "freshness picks the colour, coverage says how much of it is there. Swapping them makes " +
                "old foam opaque and fresh churn invisible.");
        }

        /// <summary>
        /// ⚠️ <b>Both of the guards below used to read <c>WakeFoamAgedColor</c>'s own body, and row 2
        /// moved what they guard.</b> The whitecaps and the surf's whitewater now walk the same ramp, so
        /// the walk itself was lifted into ONE entry point — <c>FoamAgedColor(age01, legacy, strength)</c>
        /// — and <c>WakeFoamAgedColor</c> became the wake's two-line adapter onto it. The properties
        /// asserted here did not change; the function that owns them did, and a guard that goes red on
        /// its own fix has to follow it rather than be deleted. Both halves are checked: the shared walk
        /// keeps the passthrough and the anchors, and the wake's adapter still hands it the wake's own
        /// legacy colour and the wake's own dial.
        /// </summary>
        [Test]
        public void TheAgeRamp_HasAnExactPassthroughAtZero()
        {
            string src = Read(ShaderPath);
            string walk = Body(src, "float3 FoamAgedColor(float age01, float3 legacy, float strength)");
            string wake = Body(src, "float3 WakeFoamAgedColor(float freshness)");

            // Every visual layer in this shader ships with a knob whose 0 is the previous look, bit-exact.
            // It is how the owner A/Bs a change and how a bad call gets reverted without a revert.
            Assert.IsTrue(Regex.IsMatch(walk, @"saturate\s*\(\s*strength\s*\)[\s\S]*?return\s+legacy\s*;"),
                "FoamAgedColor must return the caller's legacy colour unchanged at strength 0. Without " +
                "that the walk cannot be switched off, for any layer, and a look the owner dislikes " +
                "becomes a code change instead of a slider.");
            Assert.IsTrue(Regex.IsMatch(wake, @"_FoamColor\.rgb\s*,\s*_WakeFoamAgeStrength"),
                "…and the wake must be the layer passing _FoamColor.rgb as that legacy colour, at its " +
                "own dial: _WakeFoamAgeStrength 0 is still the single-white compose it always was.");
        }

        [Test]
        public void TheRamp_ReadsTheSeasOwnPaletteAnchors()
        {
            string body = Body(Read(ShaderPath), "float3 FoamAgedColor(float age01, float3 legacy, float strength)");

            // ADR 0015: the "different shades of blue" must come from the water's own bounded ramp, so a
            // preset swap moves them. A hand-picked blue here would look right in North Atlantic and
            // wrong in every other preset, and nothing would fail. Since row 2 this binds the caps and
            // the surf as well: there is one walk, so there is one place this can go wrong.
            foreach (string anchor in new[] { "_PaletteFoam", "_PaletteShallow", "_PaletteMid" })
                Assert.IsTrue(body.Contains(anchor),
                    $"The sea's age ramp no longer reads {anchor}. ADR 0015's palette anchors are where " +
                    "the sea's blues live; anything else is an invented hex that a preset swap will " +
                    "leave behind.");
        }

        /// <summary>
        /// ⚠️ <b>This guard used to assert the opposite, and the opposite was the defect.</b> #665 derived
        /// the age from the buffer's COVERAGE — "the buffer decays, so how much survives at a texel already
        /// IS how old that churn is" — and a test pinned exactly that. It reads well and it is wrong for a
        /// reason no amount of reasoning about the buffer would have found: by the time the compose sees a
        /// coverage it has been SATURATED by accumulation and then THRESHOLDED and POSTERIZED, so it can
        /// only take three values and 72–81% of a visible wake draws at age exactly 0. The owner's eyeball
        /// found it; <c>WakeFoamAgeingMeasurementTests</c> now measures it. Age comes from the freshness
        /// clock, which cannot clamp.
        /// </summary>
        [Test]
        public void TheAgeProxy_IsTheBuffersFreshnessChannel()
        {
            string body = Body(Read(ShaderPath), "float3 WakeFoamAgedColor(float freshness)");

            Assert.IsTrue(Regex.IsMatch(body, @"WakeFoamAge01\s*\(\s*freshness\s*,"),
                "The shader must derive age from the buffer's FRESHNESS channel. Deriving it from the " +
                "coverage is the round-1 defect: the coverage is saturated and posterized before the " +
                "compose can read it, so the ramp collapses onto three values and the band draws white.");

            Assert.IsFalse(Regex.IsMatch(body, @"1\.0\s*-\s*coverage\s*/"),
                "The coverage-as-age proxy is back. It cannot work — see this test's own doc comment.");
        }

        /// <summary>The NEW half of the twin seam: the proxy itself. The knots and the palette lookup were
        /// already compared line-for-line; the proxy is what round 2 changed, so it joins them rather than
        /// being the one piece of the ramp that only exists in one language.</summary>
        [Test]
        public void TheAgeProxy_IsTranscribedLineForLine()
        {
            string hlsl = Body(Read(ShaderPath),
                               "float WakeFoamAge01(float freshness, float freshFloor)");
            string csharp = Body(Read(TwinPath),
                                 "public static float Age01FromFreshness(float freshness, float freshFloor)");

            Assert.AreEqual(Normalize(csharp), Normalize(hlsl),
                "The water shader's age proxy has drifted from WakeFoamAgeing.Age01FromFreshness. The " +
                "particle wake and the buffered wake would then reach the sea's blues at different ages, " +
                "and the seam between them — which is the same water — would show.");
        }

        [Test]
        public void TheFreshFloorDefault_LeavesTheWhiteHoldToTheKnots()
        {
            // Two ways to spell "stay white a bit longer" is one way too many: the floor at 1 means age 0
            // is the instant of churn and nothing else, so WhiteHold is the only knob that holds white.
            // A floor below 1 silently adds a second hold on top of it and the owner's WhiteHold stops
            // meaning what its tooltip says.
            Assert.AreEqual(1f, PropertyDefault(Read(ShaderPath), "_WakeFoamFreshFloor"), 1e-4f,
                "_WakeFoamFreshFloor must ship at 1 — the white hold belongs to _WakeFoamWhiteHold.");
        }
    }
}
