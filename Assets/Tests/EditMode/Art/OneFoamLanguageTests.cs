using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>ONE FOAM LANGUAGE</b> — the water-fidelity register's row 2, in the owner's words: foam should
    /// churn <i>"through different shades of blue, distort and fade into the ambient ocean over
    /// time."</i>
    ///
    /// <para><b>What the plates showed and these guards keep fixed.</b> The sea drew its foam in two
    /// unrelated languages that met at a seam. The advected wake buffer has walked the sea's own ramp
    /// since #665 (<c>_PaletteFoam</c> → <c>_PaletteShallow</c> → <c>_PaletteMid</c> through
    /// <c>WakeFoamKnots</c>), and the whitecaps since #719; the SURF composited a flat
    /// <c>_SurfColor</c> — <b>(1,1,1) on all nine water materials, a neutral white no preset has ever
    /// moved</b>, on a sea whose own foam anchor is (0.86, 0.84, 0.74) in <c>Water_StirredBrown</c> and
    /// (0.86, 0.88, 0.89) in <c>Water_FoggySmother</c> — and it stayed that white out to its dying edge.</para>
    ///
    /// <para><b>Why the guards are shaped like this.</b> "One language" cannot be kept by three
    /// transcriptions of one ramp that happen to agree today; it is kept by there being ONE function. So
    /// the structural test is that every foam layer with an age composes through <c>FoamAgedColor</c>,
    /// and that the ramp and its knots are reached only through it. The numbers — how far the walk
    /// actually carries the surf band — are MEASURED in <c>BreakerWhitewaterAgeMeasurementTests</c> on
    /// the production maths at the live tuning, because a colour a test asserts from memory is a colour
    /// nobody re-measures when the sea is retuned.</para>
    ///
    /// <para>All of this is source and YAML: it runs on CI's GPU-less agent. What the sea LOOKS like is
    /// the owner's eye on the plate pair, which is the acceptance the charter asks for.</para>
    /// </summary>
    public class OneFoamLanguageTests
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string HeroMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";

        private static string Source() => File.ReadAllText(ShaderPath, Encoding.UTF8);

        private static IEnumerable<string> EveryWaterMaterial()
        {
            yield return HeroMatPath;
            foreach (string p in Directory.GetFiles(PresetsFolder, "Water_*.mat").OrderBy(x => x))
                yield return p;
        }

        /// <summary>The shader body with its line comments stripped, so a guard can be neither satisfied
        /// nor broken by prose. (The idiom <c>BreakerBoreLookTests</c> uses.)</summary>
        private static string CodeOnly(string src)
            => string.Join("\n", src.Split('\n').Select(line =>
            {
                int i = line.IndexOf("//", System.StringComparison.Ordinal);
                return i >= 0 ? line.Substring(0, i) : line;
            }));

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }

        private static float MatFloat(string path, string key)
        {
            var m = Regex.Match(File.ReadAllText(path, Encoding.UTF8),
                                "-\\s" + Regex.Escape(key) + ":\\s*(-?[\\d.eE+]+)");
            Assert.IsTrue(m.Success, path + " does not serialize " + key);
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static Color MatColor(string path, string key)
        {
            var m = Regex.Match(File.ReadAllText(path, Encoding.UTF8),
                                "-\\s" + Regex.Escape(key) +
                                ":\\s*\\{r:\\s*(-?[\\d.eE+]+),\\s*g:\\s*(-?[\\d.eE+]+),\\s*b:\\s*(-?[\\d.eE+]+)");
            Assert.IsTrue(m.Success, path + " does not serialize " + key + " as a colour");
            return new Color(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                             float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                             float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        // =========================================================================================
        //  ONE WALK — structural, so the layers agree by construction and not by coincidence
        // =========================================================================================

        [Test]
        public void TheAgeRamp_IsReachedOnlyThroughOneEntryPoint_AndEveryFoamLayerUsesIt()
        {
            string code = CodeOnly(Source());

            Assert.AreEqual(1, CountOf(code, "float WakeFoamKnots(float t01, float whiteHold, float blueReach, float deepReach)"),
                "the knot curve must be transcribed from WakeFoamAgeing.Knots exactly once");
            Assert.AreEqual(1, CountOf(code, "float3 WakeFoamRamp3(float age01, float3 foam, float3 shallow, float3 mid)"),
                "the three-stop lookup must be transcribed from WakeFoamAgeing.Ramp3 exactly once");
            Assert.AreEqual(1, CountOf(code, "float3 FoamAgedColor(float age01, float3 legacy, float strength)"),
                "there is ONE place a foam colour is chosen; a second one is a second language");

            // The ramp may be reached only THROUGH the shared entry point. A layer that calls
            // WakeFoamRamp3 itself has forked the walk even while it still passes the same knots — which
            // is exactly how the surf, the caps and the wake came to disagree in the first place. Two
            // occurrences each: the definition and the single call inside FoamAgedColor.
            Assert.AreEqual(2, CountOf(code, "WakeFoamRamp3("),
                "WakeFoamRamp3 is called from FoamAgedColor and nowhere else");
            Assert.AreEqual(2, CountOf(code, "WakeFoamKnots("),
                "WakeFoamKnots is called from FoamAgedColor and nowhere else");

            // ...and every layer that HAS an age is actually wired to it.
            StringAssert.Contains("FoamAgedColor(WakeFoamAge01(freshness, _WakeFoamFreshFloor),", code,
                "the advected wake buffer must compose through the shared walk");
            StringAssert.Contains("FoamAgedColor(capAge01, _FoamColor.rgb, _CapAgeStrength)", code,
                "the whitecaps must compose through the shared walk");
            StringAssert.Contains("FoamAgedColor(surfAge01, _SurfColor.rgb,    _SurfAgeStrength)", code,
                "the surf's whitewater must compose through the shared walk — this is row 2");
            StringAssert.Contains("FoamAgedColor(0.0,       _SurfLipColor.rgb, _SurfAgeStrength)", code,
                "the lip is the newest water in the frame: the shared walk at age 0, which is the sea's " +
                "own foam anchor rather than a pure white of the surf's own");
        }

        [Test]
        public void TheSurfsAge_IsTheEnergy_ReadBeforeAnythingCompressesIt()
        {
            // ⭐ The decaying-quantity law (#665), which cost that lane two rounds: an age derived from a
            // quantity that SATURATES, is THRESHOLDED and is POSTERIZED is not an age — 72–81 % of the
            // wake band drew at age exactly 0. surfAlive is exp(-(marched metres / sqrt(g·d)) / tau): a
            // smooth, strictly decreasing function of the march's own geometry. What matters is not that
            // it is smooth but WHERE it is read — before the density lift, the metaball threshold and the
            // posterize the coverage goes through. surfCover is the compressed one, and the age must
            // never be taken from it.
            string code = CodeOnly(Source());

            StringAssert.Contains("float surfAge01 = saturate(1.0 - surfAlive);", code,
                "the surf's age is its whitewater energy, read straight off the march");

            var m = Regex.Match(code, @"float\s+surfAge01\s*=\s*([^;]*);");
            Assert.IsTrue(m.Success, "surfAge01 must be assigned once, in one expression");
            StringAssert.DoesNotContain("surfCover", m.Groups[1].Value,
                "surfCover has already been through smoothstep and BandValue01 — an age taken from it is " +
                "the #665 defect, transplanted");
            StringAssert.DoesNotContain("_Time", m.Groups[1].Value,
                "the surf's age is geometry; a _Time term here would be a second clock");
        }

        [Test]
        public void TheBarrel_IsNotFoam_AndKeepsItsOwnShadowColour()
        {
            // A hollow in the water is not churn: it has no age to walk, and it is DARK because it is in
            // the lip's shadow, which is the whole reason a tube reads as a tube. Unifying it into the
            // foam palette would fill the barrel with foam.
            string code = CodeOnly(Source());
            StringAssert.Contains("col.rgb = lerp(col.rgb, _SurfBarrelColor.rgb, barrel * _SurfBarrelColor.a);", code,
                "the barrel composites its own shadow colour, not a foam colour");
        }

        // =========================================================================================
        //  ONE STRENGTH, ONE WHITE — on every material, because 'Apply water preset' is wholesale
        // =========================================================================================

        [Test]
        public void EveryWaterMaterial_WalksAtTheSameStrength()
        {
            // One language means ONE walk at ONE strength, not three tuned fractions of it. The wake has
            // shipped at 1 since #665; row 2 brings the caps (#719 built the dial and shipped it at 0 for
            // exactly this PR to turn up) and the surf alongside it.
            //
            // All nine, because 'Apply water preset' is a WHOLESALE copy: a preset missing a key stamps 0
            // over the hero material, and a preset carrying a different value would make the sea change
            // foam language whenever the weather leaned that way.
            var wrong = new List<string>();
            foreach (string path in EveryWaterMaterial())
            {
                foreach (string key in new[] { "_WakeFoamAgeStrength", "_CapAgeStrength", "_SurfAgeStrength" })
                {
                    float v = MatFloat(path, key);
                    if (!Mathf.Approximately(v, 1f)) wrong.Add(Path.GetFileName(path) + ": " + key + " = " + v);
                }
            }

            Assert.That(wrong, Is.Empty,
                "every foam layer walks the sea's ramp at full strength on every material:\n" +
                string.Join("\n", wrong));
        }

        [Test]
        public void TheSeaHasOneWhite_TheFringeIsBornWhereTheWalkStarts()
        {
            // The shore fringe has no age — it is the wet edge, renewed continuously — and it composites
            // _FoamColor. The walk STARTS at _PaletteFoam. So "one white" is only true while those two
            // are the same colour, and on every material today they are, per preset: (0.92, 0.97, 1) on
            // the hero, (0.86, 0.84, 0.74) in StirredBrown, (0.95, 0.99, 0.98) in Tropical.
            //
            // This is the one thing row 2 did not have to change and could most easily lose: nothing in
            // the shader couples them, so a retune that moved one and not the other would put a second
            // white back in the sea without touching a line of code.
            var wrong = new List<string>();
            foreach (string path in EveryWaterMaterial())
            {
                Color foam = MatColor(path, "_FoamColor");
                Color anchor = MatColor(path, "_PaletteFoam");
                if ((Vector4)foam != (Vector4)anchor)
                    wrong.Add(Path.GetFileName(path) + ": _FoamColor " + foam + " != _PaletteFoam " + anchor);
            }

            Assert.That(wrong, Is.Empty,
                "the fringe's white and the ramp's first stop are ONE colour on every material — move " +
                "one and you must move the other:\n" + string.Join("\n", wrong));
        }
    }
}
