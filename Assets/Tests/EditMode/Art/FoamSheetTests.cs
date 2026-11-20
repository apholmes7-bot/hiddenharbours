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
            // Off the wash the whitewater and lip are the banded reads above; on the wash, the wash's own join.
            StringAssert.Contains("float cover = lerp(surfBanded, washSheet,", code);
            StringAssert.Contains("float lip = lerp(lipBanded, washLip, washThin)", code);
        }

        [Test]
        public void TheWash_RunsUpAndDrainsBack_ThroughTheSheet()
        {
            // The owner, 11-19, "Yes, unify surf": the run-up's smooth sheet takes the one foam's lace, tear,
            // bands and colour walk, aged as it drains back, so the swash is the same foam as the wake.
            string code = CodeOnly(Source());
            StringAssert.Contains("if (_FoamSheetWash.x > 0.001)", code);
            StringAssert.Contains("washAge01 = saturate(surfAge01 + _FoamSheetWash.z * BeachSwashBackwash01(worldXY, depthC, t));", code,
                "the backwash reads older, off the swash's own phase");
            StringAssert.Contains("washTorn = FoamSheetLace(worldXY);", code, "the same lace as every foam");
            StringAssert.Contains("float washSheet = FoamSheetJoin(saturate(surfCoverDrawn), surfCoverDrawn * _FoamSheetWash.y,", code);
            StringAssert.Contains("washAge01, washTorn, surfBay, _FoamSheetWash.x);", code, "the wash tears at its own age");
            StringAssert.Contains("float washLip = FoamSheetJoin(saturate(surfLip), surfLip * _FoamSheetWash.y,", code);
            StringAssert.Contains("0.0, washTorn, surfBay, _FoamSheetWash.x);", code, "the wash's lip is fresh foam");
            StringAssert.Contains("float3 surfFoam = FoamAgedColor(lerp(surfAge01, washAge01, washThin), _SurfColor.rgb, _SurfAgeStrength, col.rgb);", code,
                "the wash walks the same ageing colour, at its own age");
            // It is still a sheet of water on the sand: the wash's opacity and the cap still apply after it.
            int sheet = code.IndexOf("float cover = lerp(surfBanded, washSheet,", System.StringComparison.Ordinal);
            int opacity = code.IndexOf("* lerp(1.0, saturate(_WashSheetOpacity), saturate(washOnSand));", sheet, System.StringComparison.Ordinal);
            int cap = code.IndexOf("float washCapScale = washHeaviest > saturate(_WashMaxCover)", sheet, System.StringComparison.Ordinal);
            Assert.Greater(opacity, sheet, "the wash keeps its sheet opacity");
            Assert.Greater(cap, opacity, "and the cap: never a solid mat on the sand");
        }

        [Test]
        public void WithTheWashJoinAtZero_TheOlderSmoothSheetComesBack()
        {
            // join 0: FoamSheetJoin returns its legacy argument, the age walk reads surfAge01, the lace is not read.
            string code = CodeOnly(Source());
            StringAssert.Contains("float washAge01 = surfAge01;", code);
            StringAssert.Contains("float washTorn = 0.5;", code);
            StringAssert.Contains("FoamSheetJoin(saturate(surfCoverDrawn),", code, "the legacy read is the old smooth sheet");
            StringAssert.Contains("FoamSheetJoin(saturate(surfLip),", code, "the legacy lip is the old smooth lip");
            // lerp(a, a, w) is a: the old colour walk.
            int set = code.IndexOf("float washAge01 = surfAge01;", System.StringComparison.Ordinal);
            int gate = code.IndexOf("if (_FoamSheetWash.x > 0.001)", System.StringComparison.Ordinal);
            Assert.Greater(gate, set, "the age only moves inside the join's gate");
        }

        [Test]
        public void TheStormStreaks_DrawThroughTheSheet_AsOldFoam()
        {
            string code = CodeOnly(Source());
            // The lanes' amount is one function; the older tint is a wrapper over it, so the two reads agree.
            Assert.AreEqual(1, CountOf(code, "float StormFoamLaneAmount(float2 worldXY, float dt, float t)"));
            StringAssert.Contains("float amount = StormFoamLaneAmount(worldXY, dt, t);", Body(code, "float3 StormFoamLanes("));
            StringAssert.Contains("float stormSheet = FoamSheetCover(FoamSheetTear(saturate(stormAmount * _FoamSheetStorm.y),", code);
            StringAssert.Contains("FoamSheetLace(worldXY), stormAge01, 0.0), bay);", code);
            StringAssert.Contains("float3 stormFoam = FoamAgedColor(stormAge01, _FoamColor.rgb, stormJoin, col.rgb);", code);
            StringAssert.Contains("col.rgb = lerp(col.rgb, stormFoam, stormSheet * stormJoin * _FoamColor.a);", code);
            // join 0 is the older tint, bit-exact.
            StringAssert.Contains("if (_FoamSheetStorm.x <= 0.001)", code);
            StringAssert.Contains("col.rgb += StormFoamLanes(worldXY, dt, t);", code);
        }

        [Test]
        public void TheConvergenceFoam_IsTheCapsFoam_AtItsOwnAge()
        {
            string code = CodeOnly(Source());
            // The pinch keeps WHERE and HOW MUCH; joined, it is the caps' foam at full body (not the
            // borrowed milky texture), and where it is the larger foam the caps' age walk reads its age.
            StringAssert.Contains("float convPart = capMilkyT * conv;", code);
            StringAssert.Contains("if (_FoamSheetConv.x > 0.001)", code);
            StringAssert.Contains("convPart = conv * lerp(capMilkyT, 1.0, convJoin);", code);
            StringAssert.Contains("capAge01 = lerp(capAge01, saturate(_FoamSheetConv.y), convShare);", code);
            StringAssert.Contains("lifecyclePart = max(lifecyclePart, convPart);", code);
            // The age is set before the caps draw through the sheet, so the convergence foam takes the
            // same tear and the same colour walk.
            int age = code.IndexOf("capAge01 = lerp(capAge01, saturate(_FoamSheetConv.y), convShare);", System.StringComparison.Ordinal);
            int sheet = code.IndexOf("FoamSheetLace(worldXY), capAge01, 0.0), bay);", System.StringComparison.Ordinal);
            Assert.That(age, Is.GreaterThan(0));
            Assert.That(sheet, Is.GreaterThan(age), "the convergence age must be set before the caps' sheet reads it");
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
            CollectionAssert.AreEqual(new[] { d.StormVector.x, d.StormVector.y, d.StormVector.z, d.StormVector.w },
                VectorDefault(src, "_FoamSheetStorm"), "_FoamSheetStorm");
            CollectionAssert.AreEqual(new[] { d.ConvVector.x, d.ConvVector.y, d.ConvVector.z, d.ConvVector.w },
                VectorDefault(src, "_FoamSheetConv"), "_FoamSheetConv");
            CollectionAssert.AreEqual(new[] { d.WashVector.x, d.WashVector.y, d.WashVector.z, d.WashVector.w },
                VectorDefault(src, "_FoamSheetWash"), "_FoamSheetWash");
            Assert.AreEqual(d.AgeTear, FloatDefault(src, "_FoamSheetAgeTear"), 1e-6f, "_FoamSheetAgeTear");
            CollectionAssert.AreEqual(new[] { d.EdgeVector.x, d.EdgeVector.y, d.EdgeVector.z, d.EdgeVector.w },
                VectorDefault(src, "_FoamSheetEdge"), "_FoamSheetEdge");
        }

        // The owner's oar look of 11-19: the oars' puddles drew as "hard-edged blocky white squares" and the
        // trail astern kept square-edged chunks: the buffer's 1/8 m cells read through one point sample.
        [Test]
        public void TheOpenWaterFoam_IsReadSoftAndTorn_NotCellByCell()
        {
            string src = Source();
            string read = Body(src, "float2 FoamSheetEdgeRead(");
            StringAssert.Contains("sampler_LinearClamp", read, "the edge read blends neighbouring cells");
            Assert.AreEqual(4, Regex.Matches(read, "SAMPLE_TEXTURE2D_LOD").Count, "a four-tap tent");
            StringAssert.Contains("ValueNoise(worldXY * invSize)", read, "the read point wanders on a fixed noise");
            StringAssert.DoesNotContain("_Time", read, "the edge does not crawl: a fixed world noise");
            string cover = Body(src, "float WakeFoamCoverage(");
            StringAssert.Contains("(_FoamSheetEdge.x > 0.0 || _FoamSheetEdge.y > 0.0)", cover);
            StringAssert.Contains("? FoamSheetEdgeRead(worldXY)", cover);
            StringAssert.Contains(": SAMPLE_TEXTURE2D(_HHFoamBufferTex, sampler_HHFoamBufferTex, uv).rg;", cover,
                "both edge dials 0 keep the older point read for an A/B");
            FoamSheetSettings d = FoamSheetSettings.Default;
            Assert.Greater(d.EdgeSoftenMetres, 0.125f, "the shipped soften spans more than one buffer cell");
            Assert.Greater(d.EdgeWarpMetres, 0f, "the shipped edge is torn");
            Assert.Less(d.EdgeWarpMetres, d.EdgeWarpSizeMetres, "the wander is smaller than its lumps: a ragged edge, not a smear");
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
            Assert.AreEqual(1f, d.Storm, "the storm streaks ship on the sheet (the owner's ruling)");
            Assert.AreEqual(1f, d.Convergence, "the convergence foam ships on the sheet (the owner's ruling)");
            Assert.AreEqual(1f, d.Wash, "the wash ships on the sheet (the owner's ruling of 11-19, 'Yes, unify surf')");
        }
    }
}
