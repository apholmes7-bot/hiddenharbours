using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the marbled thin edge. On a near-flat bed the beach swash's along-shore offset is
    /// keyed to a shore direction the bed cannot give, so it turned to speckle, and the drawn edge's hard cut
    /// drew its contours as nested rings of water and sand. The fix is two dials: the offset fades out below a
    /// seabed slope (<see cref="BreakerSettings.SwashSteadyEdgeSlope"/>), and the water thins to a film over the
    /// last few centimetres of level inside the edge (<see cref="BreakerSettings.EdgeWetFilmMeters"/>).
    /// 0 on either gives the previous look. CI has no GPU, so the C# twins carry the law and the shader source
    /// is pinned to call them.
    /// </summary>
    public sealed class ShoreThinEdgeTests
    {
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string BridgePath = "Assets/_Project/Code/Art/WaveFieldBridge.cs";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        [Test]
        public void TheSteadyEdge_IsOne_WhenTheDialIsOff()
        {
            for (float slope = 0f; slope <= 2f; slope += 0.005f)
            {
                Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(slope, 0f), 0f, "0 = the previous look");
                Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(slope, -1f), 0f);
            }
        }

        [Test]
        public void TheSteadyEdge_KeepsRealBeaches_AndStillsFlats()
        {
            float steady = BreakerSettings.Default.SwashSteadyEdgeSlope;
            Assert.AreEqual(0f, BreakerMath.SwashDesyncKeep01(0f, steady), 0f, "a flat keeps no offset");
            Assert.AreEqual(0f, BreakerMath.SwashDesyncKeep01(0.25f * steady, steady), 1e-6f);
            Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(steady, steady), 1e-6f);
            Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(0.5f, steady), 0f, "a real beach is untouched");
            float prev = 0f, maxStep = 0f;
            for (float slope = 0f; slope <= 0.3f; slope += 0.0005f)
            {
                float k = BreakerMath.SwashDesyncKeep01(slope, steady);
                Assert.That(k, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(k + 1e-6f, prev, "the keep must not fall as the slope grows");
                maxStep = Mathf.Max(maxStep, k - prev);
                prev = k;
            }
            Assert.Less(maxStep, 0.02f, "no step in the keep between slope samples 0.0005 apart");
        }

        [Test]
        public void OnAFlat_NeighbouringStretchesSwashAsOne()
        {
            // The marbling, as the twin sees it: on a flat the along-shore noise sample jumps between
            // neighbouring pixels (the tangent turns round at every bump). Without the steady edge their
            // swash offsets differ; with it they are one line.
            float steady = BreakerSettings.Default.SwashSteadyEdgeSlope;
            const float vary = 0.35f, flatSlope = 0.01f, beachSlope = 0.2f;
            for (float t = 0f; t < 10f; t += 0.37f)
            {
                float a = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary * BreakerMath.SwashDesyncKeep01(flatSlope, steady), 0.1f, true);
                float b = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary * BreakerMath.SwashDesyncKeep01(flatSlope, steady), 0.9f, true);
                Assert.AreEqual(a, b, 1e-6f, "a flat swashes as one line");
                float c = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary * BreakerMath.SwashDesyncKeep01(beachSlope, steady), 0.1f, true);
                float d = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary, 0.1f, true);
                Assert.AreEqual(d, c, 0f, "a real beach keeps its offset exactly");
            }
            float spread = 0f;
            for (float t = 0f; t < 10f; t += 0.37f)
                spread = Mathf.Max(spread, Mathf.Abs(
                    WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary, 0.1f, true) -
                    WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary, 0.9f, true)));
            Assert.Greater(spread, 0.3f, "without the steady edge the same two stretches disagree (the defect)");
        }

        [Test]
        public void TheWetFilm_IsOne_WhenTheDialIsOff()
        {
            for (float q = -0.1f; q <= 0.5f; q += 0.005f)
                Assert.AreEqual(1f, BreakerMath.EdgeWetFilm01(q, 0f), 0f, "0 = the hard cut");
        }

        [Test]
        public void TheWetFilm_ThinsToClearAtTheDrawnEdge()
        {
            float film = BreakerSettings.Default.EdgeWetFilmMeters;
            Assert.AreEqual(0f, BreakerMath.EdgeWetFilm01(0f, film), 1e-6f, "clear at the drawn edge");
            Assert.AreEqual(1f, BreakerMath.EdgeWetFilm01(film, film), 1e-6f, "full water past the film");
            Assert.AreEqual(1f, BreakerMath.EdgeWetFilm01(1f, film), 0f, "deep water is untouched");
            float prev = 0f, maxStep = 0f;
            float dq = film / 120f;   // the film's own scale: a ramp, not a step, at any shipped width
            for (float q = 0f; q <= 0.1f; q += dq)
            {
                float a = BreakerMath.EdgeWetFilm01(q, film);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(a + 1e-6f, prev, "the film thickens inward");
                maxStep = Mathf.Max(maxStep, a - prev);
                prev = a;
            }
            Assert.Less(maxStep, 0.02f, "the film has no band: no step between samples 1/120 of its width apart");
        }

        [Test]
        public void TheShippedConfig_CarriesTheDials_AndOffMeansOff()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath);
            Assert.AreEqual(BreakerSettings.Default.SwashSteadyEdgeSlope, config.Breakers.SwashSteadyEdgeSlope, 1e-6f);
            Assert.AreEqual(BreakerSettings.Default.EdgeWetFilmMeters, config.Breakers.EdgeWetFilmMeters, 1e-6f);
            Assert.Greater(config.Breakers.SwashSteadyEdgeSlope, 0f);
            Assert.Greater(config.Breakers.EdgeWetFilmMeters, 0f);
            // The breakers-off publish uses default settings: both dials 0, the previous edge.
            var off = default(BreakerSettings);
            Assert.AreEqual(0f, off.SwashSteadyEdgeSlope, 0f);
            Assert.AreEqual(0f, off.EdgeWetFilmMeters, 0f);
        }

        [Test]
        public void TheShaderAndTheBridge_UseTheTwins()
        {
            string src = File.ReadAllText(WaterShaderPath);
            StringAssert.Contains("float4 _ShoreThinEdge;", src);
            StringAssert.Contains("return steady > 0.0 ? smoothstep(0.25 * steady, steady, SeabedSlopeMag(worldXY)) : 1.0;", src);
            Assert.AreEqual(2, CountOf(src, "* _SwashAlongShoreVary * 6.2831853 * SwashDesyncKeep(worldXY);"),
                "the swash and its rate twin must keep the same desync");
            // The art grid (11-26) puts the film's thinning in hard-threshold tones; the smooth twin is the ramp they cut.
            StringAssert.Contains("float wetFilm = smoothstep(0.0, _ShoreThinEdge.y, washEdgeQ);", src);
            StringAssert.Contains("wetFilm = ShoreToneHard(wetFilm);", src);
            // No dither on the shore foam or the film (the owner's "screen door"; the dithered tones read as confetti):
            // the tone function is thresholds only.
            int toneAt = src.IndexOf("float ShoreToneHard(float v01)", System.StringComparison.Ordinal);
            Assert.Greater(toneAt, 0, "the shore's hard tone function");
            string tone = src.Substring(toneAt, src.IndexOf("\n            }", toneAt, System.StringComparison.Ordinal) - toneAt);
            foreach (string dither in new[] { "Bayer", "BandValue01", "Hash", "Noise" })
                StringAssert.DoesNotContain(dither, tone, "the shore foam's tones are chosen by threshold, never a dither pattern");
            StringAssert.Contains("col.a *= wetFilm;", src);
            StringAssert.Contains("#include \"Assets/_Project/Art/Shaders/Include/CausticPattern.hlsl\"", src);
            StringAssert.DoesNotContain("#include \"Include/CausticPattern.hlsl\"", src);
            string bridge = File.ReadAllText(BridgePath);
            StringAssert.Contains("Shader.PropertyToID(\"_ShoreThinEdge\")", bridge);
            StringAssert.Contains("breakers.SwashSteadyEdgeSlope", bridge);
            StringAssert.Contains("breakers.EdgeWetFilmMeters", bridge);
        }

        // ---- The wash lip (the wet film's look): the white line sat on sand past the film, smooth and even ----

        [Test]
        public void TheLip_AtNoInset_IsThePreviousLineExactly()
        {
            for (float d = -0.3f; d <= 0.3f; d += 0.0125f)
                for (float land = -0.3f; land <= 0.3f; land += 0.0125f)
                    for (float shift = -0.2f; shift <= 0.2f; shift += 0.05f)
                        Assert.AreEqual(WaterSurface.WashLineDrawn(d, land, shift),
                            BreakerMath.WashLipDrawn(d, land, shift, BreakerMath.WashLipInsetMeters(0.03f, 0f)),
                            "inset 0 must be the previous ground-measured line");
            Assert.AreEqual(0f, BreakerMath.WashLipInsetMeters(0f, 0.5f), 0f, "no film, no inset");
            Assert.AreEqual(0.1f, BreakerMath.WashLipWidthMeters(0.1f, 0.13f, 0f), 0f, "no vary, the even width");
            Assert.AreEqual(0f, BreakerMath.WashLipAge01(0f, 0f, 0.7f), 0f);
        }

        [Test]
        public void TheLip_SitsWhereTheFilmShows_NotOnTheSandPastIt()
        {
            var s = BreakerSettings.Default;
            float inset = BreakerMath.WashLipInsetMeters(s.EdgeWetFilmMeters, s.WashLipFilmShare);
            Assert.Greater(inset, 0f, "the shipped lip sits inside the film");
            Assert.Less(inset, s.EdgeWetFilmMeters, "and never past the full water");
            // On a 1:100 flat (the St Peters low flat) with a 0.1 m line: walk up the shore normal.
            const float slope = 0.01f, width = 0.1f, water = -1f, shift = 0.02f;
            float firstLineX = float.NaN;
            for (float x = 0f; x < 6f; x += 0.005f)
            {
                float depth = water - (-1.04f + slope * x), land = water - (-1.04f + slope * (x + width));
                if (!float.IsNaN(firstLineX) || !BreakerMath.WashLipDrawn(depth, land, shift, inset)) continue;
                firstLineX = x;
                float film = BreakerMath.EdgeWetFilm01(depth + shift, s.EdgeWetFilmMeters);
                Assert.That(film, Is.InRange(0.3f, 0.95f), "the lip lies on water the film shows, not on clear sand");
            }
            Assert.IsFalse(float.IsNaN(firstLineX), "premise: the lip is drawn on the flat");
            // Without the inset the line is at the drawn edge, where the film is clear.
            for (float x = 0f; x < 6f; x += 0.005f)
            {
                float depth = water - (-1.04f + slope * x), land = water - (-1.04f + slope * (x + width));
                if (!BreakerMath.WashLipDrawn(depth, land, shift, 0f)) continue;
                Assert.Less(BreakerMath.EdgeWetFilm01(depth + shift, s.EdgeWetFilmMeters), 0.05f, "the defect: the old line sits on clear film");
                break;
            }
        }

        [Test]
        public void TheLip_BreaksAndBreathes_WithTheOneFoam()
        {
            var s = BreakerSettings.Default;
            Assert.Greater(s.WashLipAgeFloor + s.WashLipBackwashAge, 0f, "the shipped lip is drawn through the one foam sheet");
            Assert.Greater(s.WashLipWidthVary, 0f, "the shipped lip's width follows the lace");
            Assert.Less(BreakerMath.WashLipAge01(s.WashLipAgeFloor, s.WashLipBackwashAge, 0f),
                        BreakerMath.WashLipAge01(s.WashLipAgeFloor, s.WashLipBackwashAge, 1f), "older on the backwash");
            float prev = -1f;
            for (float b = 0f; b <= 1f; b += 0.01f)
            {
                float a = BreakerMath.WashLipAge01(s.WashLipAgeFloor, s.WashLipBackwashAge, b);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(a, prev);
                prev = a;
            }
            float lo = BreakerMath.WashLipWidthMeters(0.1f, 0f, s.WashLipWidthVary);
            float hi = BreakerMath.WashLipWidthMeters(0.1f, 1f, s.WashLipWidthVary);
            Assert.AreEqual(0.1f, BreakerMath.WashLipWidthMeters(0.1f, 0.5f, s.WashLipWidthVary), 1e-6f, "the lace's centre keeps the width");
            Assert.Less(lo, 0.1f); Assert.Greater(hi, 0.1f);
            Assert.GreaterOrEqual(BreakerMath.WashLipWidthMeters(0.1f, 0f, 3f), 0f, "never a negative width");
        }

        [Test]
        public void TheBore_KeepsTheHardRead_AndOnlyTheWashReadsTheLeeRamp()
        {
            // Rice Point at noon: fed the lee lattice's ramp, the bore's break line, clock and birth moved across
            // it, and each slab's hard foam edge cut it at its own place (angular pieces). The bore reads the
            // probe's own envelope; the wash's reach, its edge and its lee fade keep the ramp.
            string src = File.ReadAllText(WaterShaderPath);
            int lattice = IndexOf(src, "surfFetchFactors.low = lerp(leeFa.low, leeFb.low, leeFr);");
            int differ = IndexOf(src, "if (max(max(leeStep.x, leeStep.y), max(leeStep.z, leeStep.w)) > max(_BoreLeeFade.z, 0.0))");
            int env = IndexOf(src, "surfBoreEnv = FetchFactorAt(surfBoreFactors, clamp((int)(_WaveFieldParams.w + 0.5), 0, 7));");
            int gate = IndexOf(src, "surfBreaking = SurfBreaking01(surfGateDepth, surfBoreEnv);");
            int march = IndexOf(src, "SurfMarch(surfEvalXY, surfDir, surfBoreEnv, ageM, travelS);");
            int births = IndexOf(src, "surfFreqScale, surfBoreEnv, surfBoreFactors);");
            int reach = IndexOf(src, "float breakDepth = BreakerDepthAtEnv(_BreakerDepths.xyz, _BreakerDepths.w, surfFetchEnv);");
            // The cost pass: the step only marks the hard read as owed. The read is taken once the gate's depth is
            // known, and only where that depth could break under some envelope (shallower than the deepest outer
            // edge or break depth at the three anchors): past that the gate is 0 for any envelope, so skipping it
            // changes no pixel. The gate and the march read the dominant slot, which one march decides; the other
            // march is taken only where the births (which read every slot) run.
            int owed = IndexOf(src, "boreHardReadOwed = true;");
            int canBreak = IndexOf(src, "if (boreHardReadOwed && _BreakerOuter.w >= 0.5 && surfGateDepth > 0.0");
            int halfGate = IndexOf(src, "surfBoreEnv = FetchFactorAt(boreGateFactors, boreDominant);");
            int halfBirth = IndexOf(src, "surfBoreFactors = FetchFactorsFrom(boreHardFetch, boreHardShelter);");
            Assert.That(lattice < differ && differ < owed && owed < env && env < canBreak && canBreak < halfGate
                        && halfGate < gate && gate < march && march < halfBirth && halfBirth < births && births < reach,
                "one hard read for the bore where the lattice ramps and the gate can open, then the gate, the march and the " +
                "births on it, the reach on the ramp");
            StringAssert.Contains("&& surfGateDepth < max(max(max(_BreakerOuter.x, _BreakerOuter.y), _BreakerOuter.z),", src);
            StringAssert.Contains("max(max(_BreakerDepths.x, _BreakerDepths.y), _BreakerDepths.z) + 1e-3))", src);
            StringAssert.Contains("bool boreDominantIsSwell = _WaveSwell.x > 0.0 && boreDominant >= 6;", src);
            Assert.AreEqual(3, CountOf(src, "FetchMarch01(fetchProbeXY)"), "the one read with no lattice, and each half of the hard read once");
            Assert.AreEqual(3, CountOf(src, "SwellShelter01(fetchProbeXY)"), "the one read with no lattice, and each half of the hard read once");
            Assert.AreEqual(2, CountOf(src, "surfBoreFactors = surfFetchFactors;"), "one envelope where there is no ramp, and with no lattice");
            Assert.AreEqual(2, CountOf(src, "surfBoreEnv, surfBoreFactors);"), "both births");
            StringAssert.Contains("float boreEdgeLeeFade = BoreEdgeLeeFade01(surfFetchEnv);", src);
            StringAssert.Contains("float4 leeStep = max(abs(leeFa.low - leeFb.low), abs(leeFa.high - leeFb.high));", src);
            StringAssert.Contains("Mathf.Max(0f, breakers.BoreHardReadStep)", File.ReadAllText(BridgePath));

            // The twin: only a shadow line's step takes the third read; 0 = any difference, as the first pass did.
            float step = BreakerSettings.Default.BoreHardReadStep;
            Assert.Greater(step, 0f);
            Assert.IsFalse(BreakerMath.BoreTakesHardRead(0.5f * step, step), "a smooth fetch shares the blend");
            Assert.IsTrue(BreakerMath.BoreTakesHardRead(2f * step, step), "a shadow line takes the hard read");
            Assert.IsTrue(BreakerMath.BoreTakesHardRead(1e-5f, 0f), "0 = the hard read wherever the two differ");
            Assert.IsFalse(BreakerMath.BoreTakesHardRead(0f, 0f), "no difference, no third read");
        }

        private static int IndexOf(string src, string part)
        {
            int i = src.IndexOf(part, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0, "missing: " + part);
            return i;
        }

        [Test]
        public void TheShippedConfig_CarriesTheLipDials()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath);
            var d = BreakerSettings.Default;
            Assert.AreEqual(d.WashLipFilmShare, config.Breakers.WashLipFilmShare, 1e-6f);
            Assert.AreEqual(d.WashLipAgeFloor, config.Breakers.WashLipAgeFloor, 1e-6f);
            Assert.AreEqual(d.WashLipBackwashAge, config.Breakers.WashLipBackwashAge, 1e-6f);
            Assert.AreEqual(d.WashLipWidthVary, config.Breakers.WashLipWidthVary, 1e-6f);
            Assert.AreEqual(d.BoreHardReadStep, config.Breakers.BoreHardReadStep, 1e-6f);
            Assert.AreEqual(d.WashLipStillFresh, config.Breakers.WashLipStillFresh, 1e-6f);
            var off = default(BreakerSettings);
            Assert.AreEqual(0f, off.WashLipFilmShare + off.WashLipAgeFloor + off.WashLipBackwashAge + off.WashLipWidthVary
                                + off.WashLipStillFresh,
                            0f, "the breakers-off publish draws the previous line");
        }

        [Test]
        public void TheShaderAndTheBridge_UseTheLipAndLeeTwins()
        {
            string src = File.ReadAllText(WaterShaderPath);
            StringAssert.Contains("float4 _ShoreWashLip;", src);
            StringAssert.Contains("float washLipInset = max(_ShoreThinEdge.y, 0.0) * saturate(_ShoreWashLip.x);", src);
            StringAssert.Contains("if (washOnSand > 0.001 && washEdgeQ >= washLipInset - 1e-4 && dot(groundDir, groundDir) > 1e-6)", src);
            StringAssert.Contains("if (_ShoreWashLip.w > 0.0) washLipWidth *= max(1.0 + _ShoreWashLip.w * (2.0 * washLipLace - 1.0), 0.0);", src);
            StringAssert.Contains("if (washLandDepth + edgeSwash < washLipInset)", src);
            StringAssert.Contains("* FoamSheetCover(FoamSheetTear(washOnSand, washLipLace, washLipAge01, 0.0), sheetBay);", src);
            StringAssert.Contains("washLineColor = FoamAgedColor(washLipAge01, _FoamColor.rgb, _FoamSheetShore.x, col.rgb);", src);
            string bridge = File.ReadAllText(BridgePath);
            StringAssert.Contains("Shader.PropertyToID(\"_ShoreWashLip\")", bridge);
            foreach (var f in new[] { "WashLipFilmShare", "WashLipAgeFloor", "WashLipBackwashAge", "WashLipWidthVary" })
                StringAssert.Contains("breakers." + f, bridge);
        }

        [Test]
        public void TheLipOverStillWater_ReadsYounger_OnlyPastTheFilmAndWhereNoBoreRuns()
        {
            // The St Peters bar head (the owner's word of 11-23, "it can be stronger wash lip"): on a steep shore
            // no bore runs up, the lip's width reached past the film onto full water and its aged colour took that
            // water's, so it was all but gone. It now reads younger there; a flat and a bore's run-up keep their age.
            var s = BreakerSettings.Default;
            Assert.Greater(s.WashLipStillFresh, 0f);
            float film = s.EdgeWetFilmMeters;
            float inset = BreakerMath.WashLipInsetMeters(film, s.WashLipFilmShare);
            float age = BreakerMath.WashLipAge01(s.WashLipAgeFloor, s.WashLipBackwashAge, 0.5f);
            Assert.AreEqual(0f, BreakerMath.WashLipOverWater01(inset, inset, film), 1e-6f, "at the inset: in the film");
            Assert.AreEqual(1f, BreakerMath.WashLipOverWater01(film, inset, film), 1e-6f, "at the film's depth: full water");
            Assert.AreEqual(0f, BreakerMath.WashLipOverWater01(0f, 0f, 0f), 1e-6f, "no film, no inset: at the edge");
            Assert.AreEqual(1f, BreakerMath.WashLipOverWater01(1f, 0f, 0f), 1e-6f, "and past it, water");
            Assert.AreEqual(age, BreakerMath.WashLipStillAge01(age, 0f, 1f, 0f), 0f, "0 = the lip's age, bit-exact");
            Assert.AreEqual(age, BreakerMath.WashLipStillAge01(age, s.WashLipStillFresh, 0f, 0f), 0f, "in the film (a flat) it keeps its age");
            Assert.AreEqual(age, BreakerMath.WashLipStillAge01(age, s.WashLipStillFresh, 1f, 1f), 0f, "a bore's run-up keeps its age");
            float still = BreakerMath.WashLipStillAge01(age, s.WashLipStillFresh, 1f, 0f);
            Assert.AreEqual(age * (1f - s.WashLipStillFresh), still, 1e-6f, "over full water with no bore it is younger by the dial");
            Assert.Less(still, age);
            float prev = still;
            for (float b = 0.1f; b <= 1.0001f; b += 0.1f)
            {
                float a = BreakerMath.WashLipStillAge01(age, s.WashLipStillFresh, 1f, b);
                Assert.GreaterOrEqual(a, prev - 1e-6f, "a bore takes its share of the edge back to the lip's age");
                prev = a;
            }

            string src = File.ReadAllText(WaterShaderPath);
            int age01 = IndexOf(src, "float washLipAge01 = saturate(saturate(_ShoreWashLip.y)");
            int younger = IndexOf(src, "washLipAge01 *= 1.0 - saturate(_ShoreThinEdge.z)");
            int tear = IndexOf(src, "* FoamSheetCover(FoamSheetTear(washOnSand, washLipLace, washLipAge01, 0.0), sheetBay);");
            Assert.That(age01 < younger && younger < tear, "the still-water age is taken before the sheet reads it");
            StringAssert.Contains("* smoothstep(washLipInset, max(max(_ShoreThinEdge.y, 0.0), washLipInset + 1e-4), washEdgeQ)", src);
            StringAssert.Contains("* (1.0 - saturate(boreEdgeBlend));", src);
            StringAssert.Contains("Mathf.Clamp01(breakers.WashLipStillFresh)", File.ReadAllText(BridgePath));
        }

        [Test]
        public void TheEnvelope_IsReadOnlyWhereSomethingReadsIt()
        {
            // The Rice Point noon cost: the lee lattice's reads were taken over all the water in the surf block.
            // They feed only the break gate (0 past the deepest outer or break depth) and the shore's wash, edge
            // and fringe (each 0 past its own depth), so past all of those they are not taken. The shore zone and
            // the gate's depth read no envelope and are worked out first.
            string src = File.ReadAllText(WaterShaderPath);
            int shoreZone = IndexOf(src, "surfShore = saturate(-shoreTapDepth / max(surfBeachReach, 1e-3));");
            int gateDepth = IndexOf(src, "surfGateDepth = max(lerp(surfEvalDepth, boxDepth, surfShore), 1e-3);");
            int canOpen = IndexOf(src, "bool surfGateCanOpen = surfGateDepth > 0.0");
            int needs = IndexOf(src, "bool surfEnvRead = surfGateCanOpen || surfShore > 0.0");
            int reads = IndexOf(src, "if (surfEnvRead && surfFetchProbeM > 0.0 && dot(surfDir, surfDir) > 1e-6)");
            int lattice = IndexOf(src, "surfFetchFactors.low = lerp(leeFa.low, leeFb.low, leeFr);");
            int gate = IndexOf(src, "surfBreaking = SurfBreaking01(surfGateDepth, surfBoreEnv);");
            Assert.That(shoreZone < gateDepth && gateDepth < canOpen && canOpen < needs && needs < reads
                        && reads < lattice && lattice < gate,
                "the shore zone and the gate's depth, then what needs the envelope, then the reads, then the gate");
            // Each consumer's own depth, in the shader's own terms: the wash on the sand, the cosmetic shore band
            // (the drawn edge's swash), the fringe's swash reach, and its foam band less the dither's half.
            StringAssert.Contains("float surfEnvShoreDepth = max(max(_WashSeawardDepth, 1e-3), shoreCosmeticReach);", src);
            StringAssert.Contains("float surfEnvFringeDepth = max(max(_FoamWidth, 1e-3) * 2.0 + max(abs(_SwashAmplitude), 1e-3),", src);
            StringAssert.Contains("max(_FoamWidth, 1e-3) + 0.5 * abs(_FoamEdgeDither));", src);
            StringAssert.Contains("|| depth < surfEnvShoreDepth || depthC < surfEnvFringeDepth;", src);
            // The fringe's own terms these mirror (if one moves, this guard must move with it).
            StringAssert.Contains("float swashReach = max(_FoamWidth, 1e-3) * 2.0 + max(abs(_SwashAmplitude), 1e-3);", src);
            StringAssert.Contains("float washOnSand = washPresence * (1.0 - smoothstep(0.0, max(_WashSeawardDepth, 1e-3), depth));", src);
            StringAssert.Contains("if (swashEdgeReach > 0.0 && depth < shoreCosmeticReach)", src);
            StringAssert.Contains("float foamEdgeDither = (bay - 0.5) * _FoamEdgeDither;", src);
            // The band's depth may only be capped BELOW _FoamWidth (the ground bound), so the envelope's
            // max(_FoamWidth, ...) above stays a conservative reach.
            StringAssert.Contains("float foamBandDepth = max(_FoamWidth, 1e-3);", src);
            StringAssert.Contains("foamBandDepth = max(min(foamBandDepth, _FoamBandMaxMetres", src);
            StringAssert.Contains("float foamEdge = 1.0 - smoothstep(0.0, foamBandDepth, foamDepth + foamEdgeDither);", src);
        }

        private static int CountOf(string text, string part)
        {
            int n = 0;
            for (int i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0;
                 i = text.IndexOf(part, i + part.Length, System.StringComparison.Ordinal)) n++;
            return n;
        }
    }
}
