using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE ONE FOAM SHEET</b>, in the owner's words (2026-11-19): <i>"wake foam should be the same as any
    /// foam, not a drawn special piece special to just the wake. there needs to be consistency amongst the
    /// in game foam"</i>, then the shore line, the storm streaks and whitecaps, and the convergence foam.
    ///
    /// <para>Every foam source keeps its own COVERAGE logic and hands an amount and an age to three shared
    /// steps (<c>FoamSheetLace</c>, <c>FoamSheetTear</c>, <c>FoamSheetCover</c>), then the one colour walk
    /// (<c>FoamAgedColor</c>). "One substance" cannot be kept by copies that agree today, so these guards
    /// are structural: each step is transcribed once, every source is wired through it, and the dials the
    /// owner tunes in <see cref="GameConfig.FoamSheet"/> match the shader's own defaults. Source and YAML
    /// only: they run on CI's GPU-less agent. What the sea LOOKS like is the owner's eye on the plates.</para>
    /// </summary>
    public class FoamSheetTests
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private static string Source() => File.ReadAllText(ShaderPath, Encoding.UTF8).Replace("\r\n", "\n");

        private static string CodeOnly(string src)
            => string.Join("\n", src.Split('\n').Select(line =>
            {
                int i = line.IndexOf("//", System.StringComparison.Ordinal);
                return i >= 0 ? line.Substring(0, i) : line;
            }));

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static string Body(string code, string signature)
        {
            int at = code.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.Greater(at, 0, signature + " not found");
            int end = code.IndexOf("\n            }", at, System.StringComparison.Ordinal);
            return code.Substring(at, end - at);
        }

        private static float[] VectorDefault(string src, string prop)
        {
            var m = Regex.Match(src, @"(?m)^\s*" + Regex.Escape(prop) + @"\s*\(""[^""]*"",\s*Vector\)\s*=\s*\(([^)]*)\)");
            Assert.IsTrue(m.Success, prop + " is not a Vector property");
            return m.Groups[1].Value.Split(',').Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
        }

        private static float FloatDefault(string src, string prop)
        {
            var m = Regex.Match(src, @"(?m)^\s*" + Regex.Escape(prop) + @"\s*\(""[^""]*"",\s*[^)]*\)?\)\s*=\s*(-?[\d.]+)");
            Assert.IsTrue(m.Success, prop + " is not a scalar property");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        // =========================================================================================
        //  ONE SET OF STEPS, transcribed once
        // =========================================================================================

        [Test]
        public void TheSheetsThreeSteps_AreEachWrittenOnce()
        {
            string code = CodeOnly(Source());
            Assert.AreEqual(1, CountOf(code, "float FoamSheetLace(float2 worldXY)"));
            Assert.AreEqual(1, CountOf(code, "float FoamSheetTear(float amount, float torn, float age01, float extraTear)"));
            Assert.AreEqual(1, CountOf(code, "float FoamSheetCover(float amount, float bayer)"));
            // One blob scale for every foam: the lace rides the shared _FoamBlobScale through BandFreq,
            // on the whitecaps' own wind stretch, and boils with the sea's evolving field.
            string lace = Body(code, "float FoamSheetLace(float2 worldXY)");
            StringAssert.Contains("BandFreq(_NoiseScale) * 3.0 * _FoamBlobScale", lace);
            StringAssert.Contains("max(_FoamStreakStretch, 1.0)", lace);
            StringAssert.Contains("EvolvingField(", lace);
            // The bands are the world Bayer cell's, at the sheet's one band count.
            StringAssert.Contains("BandValue01(cover, _WakeFoamBands, 0.5, bayer)", Body(code, "float FoamSheetCover(float amount, float bayer)"));
            // The age tear: hearts only by half age, the larger of a source's own tear and the sheet's.
            StringAssert.Contains("max(extraTear, _FoamSheetAgeTear)", Body(code, "float FoamSheetTear("));
        }

        [Test]
        public void TheWake_DrawsThroughTheSheet()
        {
            string wake = Body(CodeOnly(Source()), "float WakeFoamCoverage(");
            StringAssert.Contains("FoamSheetTear(stored, FoamSheetLace(worldXY), 1.0 - freshness, _HHHullWaveFoamAge.z)", wake);
            StringAssert.Contains("FoamSheetCover(stored, bayer)", wake);
            Assert.AreEqual(0, CountOf(wake, "EvolvingField("), "the wake's lace is the sheet's, not a copy");
        }

        [Test]
        public void TheShoreLine_DrawsThroughTheSheet_AndTheOneColourWalk()
        {
            string code = CodeOnly(Source());
            StringAssert.Contains("float lineSheet = FoamSheetCover(FoamSheetTear(lineAmount, FoamSheetLace(worldXY),", code,
                "the shore line's amount takes the sheet's lace, tear, threshold and bands");
            StringAssert.Contains("foamCoverage = lerp(foamCoverage, lineSheet, saturate(_FoamSheetShore.x));", code);
            StringAssert.Contains("float3 fringeFoam = FoamAgedColor(fringeAge01, _FoamColor.rgb, _FoamSheetShore.x, col.rgb);", code,
                "the shore line walks the same colour ramp as the wake, caps and surf at its own age");
            StringAssert.Contains("col.rgb = lerp(col.rgb, fringeFoam, foamCoverage * _FoamColor.a);", code);
            // Its age is the swash's: older on the backwash, older to seaward of the wet edge. Geometry and
            // the swash clock, never the thresholded coverage (the decaying-quantity law).
            var m = Regex.Match(code, @"fringeAge01\s*=\s*saturate\(([^;]*)\);");
            Assert.IsTrue(m.Success, "fringeAge01 must be assigned in one expression");
            StringAssert.Contains("BeachSwashBackwash01(worldXY, depthC, t)", m.Groups[1].Value);
            StringAssert.DoesNotContain("foamCoverage", m.Groups[1].Value);
            // The line stays a line: its amount is the wet-edge band sharpened, never a free-standing field.
            StringAssert.Contains("pow(foamEdge, max(_FoamSheetShore.y, 0.1))", code);
        }

        [Test]
        public void TheCaps_DrawThroughTheSheet_AtTheirOwnAge()
        {
            string code = CodeOnly(Source());
            // The caps keep their lifecycle, gates and clumping for WHERE and HOW MUCH; the finished opacity,
            // scaled by the cap amount, is torn at the cap's lifecycle age and banded by the sheet.
            StringAssert.Contains("float capSheet = FoamSheetCover(FoamSheetTear(saturate(capOpacity * _FoamSheetCaps.y),", code);
            StringAssert.Contains("FoamSheetLace(worldXY), capAge01, 0.0), bay);", code);
            StringAssert.Contains("capOpacity = lerp(capOpacity, capSheet, saturate(_FoamSheetCaps.x));", code);
            int clump = code.IndexOf("capOpacity = saturate(capOpacity * clumpGate) * capShoreFade;", System.StringComparison.Ordinal);
            int sheet = code.IndexOf("float capSheet = ", System.StringComparison.Ordinal);
            int walk = code.IndexOf("FoamAgedColor(capAge01, _FoamColor.rgb, _CapAgeStrength, col.rgb)", System.StringComparison.Ordinal);
            Assert.That(clump > 0 && clump < sheet && sheet < walk,
                "the sheet takes the caps' FINISHED opacity (after clumping and the shore fade) and the walk colours it");
        }

        [Test]
        public void TheSurf_DrawsThroughTheSheet_OffTheWash()
        {
            string code = CodeOnly(Source());
            Assert.AreEqual(1, CountOf(code, "float FoamSheetJoin(float legacy, float amount, float age01, float torn, float bayer, float join)"));
            StringAssert.Contains("if (join <= 0.001) return legacy;", Body(code, "float FoamSheetJoin("), "join 0 is the source's own read, bit-exact");
            // One lace for the whitewater and the lip.
            StringAssert.Contains("float surfTorn = _FoamSheetCaps.z > 0.001 ? FoamSheetLace(worldXY) : 0.5;", code);
            StringAssert.Contains("FoamSheetJoin(SurfBandValue(surfCoverDrawn, surfBay), surfCoverDrawn * _FoamSheetCaps.w,", code);
            StringAssert.Contains("surfAge01, surfTorn, surfBay, _FoamSheetCaps.z);", code,
                "the whitewater tears at its own age (its energy, read off the march)");
            StringAssert.Contains("FoamSheetJoin(SurfBandValue(saturate(surfLip), surfBay), surfLip * _FoamSheetCaps.w,", code);
            StringAssert.Contains("0.0, surfTorn, surfBay, _FoamSheetCaps.z);", code, "the lip is the newest foam: age 0");
            // The wash on the sand keeps its smooth, unbanded sheet.
            StringAssert.Contains("float cover = lerp(surfBanded, saturate(surfCoverDrawn),", code);
            StringAssert.Contains("float lip = lerp(lipBanded, saturate(surfLip), washThin)", code);
        }

        [Test]
        public void TheSwashAge_IsTheRateOfTheSameSwash()
        {
            // BeachSwashBackwash01 must read BeachSwash's own phase: the same shoreward term, desync and
            // beats, so its run-up is the edge's run-up.
            string code = CodeOnly(Source());
            string swash = Body(code, "float BeachSwash(float2 worldXY, float depth, float t)");
            string rate = Body(code, "float BeachSwashBackwash01(float2 worldXY, float depth, float t)");
            foreach (string line in new[] {
                "float alongCoord = dot(Pixelize(worldXY), tangent) * 0.35;",
                "float shoreward = haveShore ? (max(depth, 0.0) * _SwashWavelength) : 0.0;",
                "float base = t * _SwashSpeed * 6.2831853 + shoreward;" })
            {
                StringAssert.Contains(line, swash);
                StringAssert.Contains(line, rate);
            }
            // d/dt of 0.7 sin(b) + 0.3 sin(b/2) is 0.7 cos(b) + 0.15 cos(b/2) (times the clock rate).
            StringAssert.Contains("cos(base + desync) * 0.7 + cos(base * 0.5 + desync * 1.7) * 0.15", rate);
        }

        // =========================================================================================
        //  THE OWNER'S DIALS: GameConfig and the shader's defaults are one set of numbers
        // =========================================================================================

        [Test]
        public void TheConfigDefaults_MatchTheShaderDefaults()
        {
            string src = Source();
            FoamSheetSettings d = FoamSheetSettings.Default;
            CollectionAssert.AreEqual(new[] { d.ShoreVector.x, d.ShoreVector.y, d.ShoreVector.z, d.ShoreVector.w },
                VectorDefault(src, "_FoamSheetShore"), "_FoamSheetShore");
            CollectionAssert.AreEqual(new[] { d.CapsVector.x, d.CapsVector.y, d.CapsVector.z, d.CapsVector.w },
                VectorDefault(src, "_FoamSheetCaps"), "_FoamSheetCaps");
            Assert.AreEqual(d.AgeTear, FloatDefault(src, "_FoamSheetAgeTear"), 1e-6f, "_FoamSheetAgeTear");
        }

        [Test]
        public void TheShippedConfig_JoinsEverySourceToTheSheet()
        {
            string yaml = File.ReadAllText(ConfigAssetPath, Encoding.UTF8).Replace("\r\n", "\n");
            var block = Regex.Match(yaml, @"(?m)^  FoamSheet:\n((?:    .*\n)+)");
            Assert.IsTrue(block.Success, "GameConfig.asset carries no FoamSheet block");
            FoamSheetSettings d = FoamSheetSettings.Default;
            foreach (var f in typeof(FoamSheetSettings).GetFields())
            {
                var m = Regex.Match(block.Groups[1].Value, @"(?m)^    " + f.Name + @": (-?[\d.]+)$");
                Assert.IsTrue(m.Success, "FoamSheet." + f.Name + " missing from the asset");
                Assert.AreEqual((float)f.GetValue(d), float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1e-6f,
                    "FoamSheet." + f.Name + " ships at its default");
            }
            Assert.AreEqual(1f, d.Shore, "the shore line ships on the sheet (the owner's ruling)");
            Assert.AreEqual(1f, d.Caps, "the whitecaps ship on the sheet (the owner's ruling)");
            Assert.AreEqual(1f, d.Surf, "the surf ships on the sheet (the owner's ruling)");
        }
    }
}
