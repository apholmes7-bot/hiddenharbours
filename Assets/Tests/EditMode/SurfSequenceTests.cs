using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>B1: source/default guards run on CI. Render acceptance is deliberately NOT a
    /// C# reimplementation of the HLSL gate. Null-device cases skip, never certify the look.
    /// GPU measurements use the shipped shore fixture, actual production shaders and public maths.
    /// No dependency on the uncommitted B1Capture assembly or its evidence.</summary>
    public class SurfSequenceTests
    {
        const string WaterPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string AdvectPath = "Assets/_Project/Art/Shaders/HiddenHarboursFoamBufferAdvect.shader";
        const string Dial = "_SurfFringeSequenceStrength";
        const string BaseWaterHash = "9dce086bcef9e50154f5f9ddcd6b6c7ce42b67a5f7d4a0a028cb3b86d55e9136";
        // Approved b5825e1f; re-approved at W1 Phase D (#932, owner d20) and C2a Phase D (owner, clarity 1) and at water gains B7 (#992, the seat's lean under the standing word of 10-29) and at crest glow L1 (#1004, the owner's ruling of 11-01) and at the foam lattice hash (the open-sea repeat fix, a named moved premise) and at the run-up wash (fix/shore-runup-foam, the owner's shore clip review of 11-15) and at the run-up wash v4 (fix/shore-runup-foam, the owner's v3 clip review of 11-15) and at the shore-normal pulse (fix/shore-runup-foam, the owner's v4 clip review of 11-15) and at the wash v7 (fix/shore-runup-foam, the PR 1225 editor look of 11-15) and at the wash v8 (the f07a839 editor look of 11-15) and at the wash v9 (the 7e16896 editor look of 11-15) and at the kelp under the water (feat/kelp-reads-under-water, the weed's clarity in the seabed composite, outside the surf paths) and at real hull waves (feat/real-wake-waves, the owner's ask of 11-17) and at the hull waves' soft look (feat/real-wake-waves, the owner's look of 11-19) and at the one foam sheet (feat/one-foam-everywhere, the owner's rulings of 11-19: the shore line, then the caps and surf, then the storm streaks, then the convergence foam, then the caps' and streaks' amounts tuned from the looks, then main merged in under the same ruling, then the wash in the run-up through the sheet under the owner's ruling of 11-19, "Yes, unify surf": its join, amount and backwash age, join 0 the older smooth sheet bit-exact, then the wash's amount tuned from the looks, then the owner's oar look of 11-19: the open-water foam read soft and torn through _FoamSheetEdge, both dials 0 the older point read bit-exact, and the hull waves' low-light share laid after the palette grade, 0 at noon, then the owner's oar look of 11-19 again: the one sheet's band edges broken by a fixed torn world noise through _FoamSheetDither (x 0 the Bayer cell, bit-exact), and a short hull's rings packed with a negative width drawn as broken arcs through _HHHullWaveArcs, every other ring bit-exact; no surf, bore or wash term moved) and at the cut-off pools (fix/cut-off-pools-stay-still-impl, the owner's ask of 11-19 that a pool the falling tide has cut off stays still: the sea's reach multiplies the swell, the surf's outputs, the drawn edge's swash, the wash and the shore line; exactly 1, so bit-exact, wherever no reach map is bound or the water is joined to the sea) and at the shore-wave turn (feat/waves-follow-the-seabed: each train cross-faded toward a copy along the shoreline contours inside the shore band, bend 0 or no field the straight sum bit-exact; no surf, bore or wash term moved) and at the shore whitecaps (feat/whitecaps-meet-the-shore, the owner's ruling of 11-20 that the whitecaps turn and fade near shore: inside the same shore band the wind caps and storm streaks thin out by area and swing blob by blob onto the turned crest line, both dials 0 or no field the caps and streaks bit-exact; no surf, bore or wash term moved) and at the dusk lighting pass (fix/dusk-lighting-pass, the owner's ruling of 11-20 that dusk may be fixed with game lighting changes: NightFactor gains a sun-elevation cutoff through _NightSunCutoff so the moon and stars stay off while the sun is up, 0 the older darkness-only gate bit-exact; no surf, bore, wash or foam term moved) and at main merged into the shore whitecaps (the dusk lighting pass, the shore-wave turn and the shore whitecaps together, each as approved; no term of any moved) and at the motor V wake (feat/motor-v-wake, the owner's ask of 11-21 to make motored boats a V wake: a hull wave ring packed with its hull's travel keeps only the arcs on the arms of her V through _HHHullWaveV, the reach slot now carries that travel and the reach is worked out of the band radius and width, the same number; a ring with no travel, the rowed dory's and every splash's, bit-exact; no surf, bore, wash or foam term moved) and at the smooth V arms (feat/motor-v-wake, the owner's look of 11-21 to smooth the arms and drop the repeating half circles: a V ring draws the straight piece of her arm it touches, its crests locked to its front, its arm span in the width slot and its width from k and its least length from that span through _HHHullWaveV2; a ring with no travel bit-exact; no surf, bore, wash or foam term moved) and at the V's dusk lift (feat/motor-v-wake, the owner's look of 11-22 that the V's waves read too hard at sunset: the low-light share is lerped toward its own gain in _HHHullWaveLowLight.w by the V's share of the wave, summed by height in the loop; where only rowed rings reach, bit-exact; noon untouched; no surf, bore, wash or foam term moved) and at main merged into the motor V wake (the boat reflection and the motor V wake together, each as approved; no term of either moved) and at the low-sun sea (fix/sunset-row-water-and-foam, the owner's ask of 11-21 to fix the sunset water colour and the foam on it: inside one window over the sun's elevation the sky's glow is added with the sky mirror through _LowSunSheen, the finished sea keeps a share of its own hue through _LowSunHueHold, the hull waves' low-light share is scaled by the sheen's lift, and the sun path may be drawn as sparkles through _SunGlitterForm; every default the older arithmetic, and the window's weight exactly 0 at high sun and at night; no surf, bore, wash or foam term moved) and at the rejoin pale band (fix/pool-rejoin-no-pale-band, under the owner's ask of 11-20 that cut-off pools stay still: the fringe's hand-back to the wash reads the open sea's presence, so a reconnecting pool's shallows are no longer handed back to the fringe at part strength while the reach blends; exactly the older arithmetic wherever the reach is 1, so the open sea is bit-exact; no surf, bore or wash amount moved). UTF-8 without BOM, LF. These are scope guards, not copies of physics.
        const string LegacyDepth =
            "                float foamDepth  = depthC - lerp(BeachSwash(worldXY, depthC, t) * swashSlope * swashGate,  // local, foam-only\n" +
            "                                                 surfRunUpM, boreFoamBlend);   // …and the foam rides the bore's wash too\n";
        static string Read(string path) => File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
        static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        static string Code(string text) => Regex.Replace(
            Regex.Replace(text, @"/\*[\s\S]*?\*/|//[^\n]*", ""), @"\s+", "");
        static string Section(string text, string part)
        {
            var match = Regex.Match(text, @"(?m)^ *// B1 FRINGE " + part + @" BEGIN\n([\s\S]*?)^ *// B1 FRINGE " + part + @" END\n");
            Assert.That(match.Success, Is.True, "missing B1 block " + part);
            return match.Groups[1].Value;
        }
        static string MainSource(string source)
        {
            foreach (string part in new[] { "PROPERTY", "UNIFORM", "DEPTH", "COVER" })
            {
                Section(source, part); // A missing marker must not silently change the reconstruction.
                source = Regex.Replace(source, @"(?m)^ *// B1 FRINGE " + part + @" BEGIN\n[\s\S]*?^ *// B1 FRINGE " + part + @" END\n",
                    part == "DEPTH" ? LegacyDepth : "");
            }
            // Approved F2 declarations only: require each exact line before reconstructing B1's control.
            foreach (string line in new[] {
                "        // ADR 0027 F2: CPU-read owner policy. Zero leaves the existing history path untouched.",
                "        _FoamTransportStrain (\"Foam transport strain (m/s)\", Float) = 0",
                "        _FoamTransportCurl (\"Foam transport curl (m/s)\", Float) = 0",
                "        _FoamTransportCollection (\"Foam transport collection (m/s)\", Float) = 0",
                "                float _FoamTransportStrain, _FoamTransportCurl, _FoamTransportCollection;" })
            {
                Assert.That(Regex.Matches(source, "(?m)^" + Regex.Escape(line) + "$").Count, Is.EqualTo(1), line);
                source = source.Replace(line + "\n", "");
            }
            Assert.That(Hash(source), Is.EqualTo(BaseWaterHash),
                "The reconstructed control must be EXACTLY approved main, not another candidate arm.");
            return source;
        }

        sealed class Coast : ITidalTerrain
        {
            readonly int kind;
            public Coast(int kind) { this.kind = kind; }
            public float ElevationAt(Vector2 p) =>
                (kind == 0 ? .04f : kind == 1 ? .25f : .12f) * p.x - 3f +
                (kind == 2 ? .002f * p.y * p.y : 0f);
        }
        static WaveTrains Field(WaveTrain train) => WaveTrains.From(new[] { train }, 1, 2.6f, 0);

        [TestCase(0, TestName = "SurfSequence_ImpactAndResidueShareBorePhase_Spilling")]
        [TestCase(1, TestName = "SurfSequence_ImpactAndResidueShareBorePhase_Plunging")]
        [TestCase(2, TestName = "SurfSequence_ImpactAndResidueShareBorePhase_ObliqueCorner")]
        public void SurfSequence_ImpactAndResidueShareBorePhase(int kind)
        {
            var bed = new Coast(kind);
            var settings = BreakerSettings.Default;
            var train = new WaveTrain(kind == 2 ? new Vector2(1f, .5f).normalized : Vector2.right, 18f, .5f, 0f, 9.81f);
            var field = Field(train);
            var contour = BreakerMath.ContourFor(train, 1f, settings);
            Vector2 at = default;
            float best = 0f;
            for (float x = 0f; x < 75f; x += .125f)
            {
                var p = new Vector2(x, kind == 2 ? 4f : 0f);
                var s = BreakerMath.SurfAt(p, 0f, bed, contour, 1f, field, 9.81f, settings);
                float spatial = s.Breaking01 * s.Whitewater01;
                if (spatial > best) { best = spatial; at = p; }
            }
            Assert.That(best, Is.GreaterThan(.1f), "non-vacuous active coast");
            var first = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings);
            train = new WaveTrain(train.Direction, train.Wavelength, train.Amplitude,
                train.PhaseOffset + Mathf.DeltaAngle(first.BorePhaseDegrees, 90f) * Mathf.Deg2Rad, 9.81f);
            field = Field(train);
            var crest = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings);
            float period = BreakerMath.PeriodSeconds(train);
            var trough = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings, 1f, period / 2);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(crest.BorePhaseDegrees, 90f)), Is.LessThan(.05f));
            Assert.That(crest.Bore01, Is.GreaterThan(.9f));
            Assert.That(trough.Bore01, Is.LessThan(.001f));
            Assert.That(crest.Bore01, Is.EqualTo(BreakerMath.BorePulse01(crest.BorePhaseDegrees,
                settings.BorePulseSharpness) * crest.BirthEnergy01).Within(1e-6f));
            const double now = 17.25;
            var published = new WaveTrain(train.Direction, train.Wavelength, train.Amplitude,
                train.PhaseOffset - (float)(2 * Math.PI / train.Wavelength * train.PhaseSpeed * now), 9.81f);
            var a = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings, 1f, now);
            var b = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, Field(published), 9.81f, settings);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(a.BorePhaseDegrees, b.BorePhaseDegrees)), Is.LessThan(.05f));
            Assert.That(a.Bore01, Is.EqualTo(b.Bore01).Within(.0001f));
            Assert.That(a.RunUpMeters, Is.EqualTo(b.RunUpMeters).Within(.0001f));
            string deposit = Code(Read(AdvectPath));
            foreach (string expression in new[] {
                "floatphase=SurfBoreClockPhaseDeg(breakLinePt,travelS,fs);",
                "floatbore=SurfBorePulse01(phase,_BreakerBore.x)*birth;",
                "floatfront=breaking*alive*bore;",
                "foam+=front*depositStrength*SURF_DEPOSIT_RATE*max(_HHSurfDeposit.z,0.0);" })
                StringAssert.Contains(expression, deposit);
        }

        static bool CorrectConsumers(string source)
        {
            string depth = Code(Section(source, "DEPTH")), cover = Code(Section(source, "COVER"));
            return depth == "floatfringeDisplacement=lerp(BeachSwash(worldXY,depthC,t)*swashSlope*swashGate,surfRunUpM,boreFoamBlend);" +
                "floatfringeHandoff=saturate(_SurfFringeSequenceStrength)*saturate(_SurfBeatStrength)*boreEdgeBlend;" +
                "if(fringeHandoff>0.0)fringeDisplacement=lerp(fringeDisplacement,surfRunUpM,fringeHandoff);" +
                "floatfoamDepth=depthC-fringeDisplacement;" &&
                cover == "if(fringeHandoff>0.0){floatfrontPresence=saturate(surfBreaking*surfAlive*surfBore);" +
                "foamCoverage*=lerp(1.0,frontPresence,fringeHandoff);}";
        }
        [Test]
        public void SurfSequence_FringeUsesExistingBoreAndReferenceDomain()
        {
            string src = Read(WaterPath);
            Assert.That(CorrectConsumers(src), Is.True, "inspect actual executable producer AND consumer blocks");
            foreach (var mutation in new[] {
                src.Replace("* boreEdgeBlend;", "* boreFoamBlend;"),
                src.Replace("foamCoverage *= lerp(1.0, frontPresence", "unusedCoverage *= lerp(1.0, frontPresence"),
                src.Replace("float foamDepth = depthC - fringeDisplacement;", "float foamDepth = depthC;"),
                src.Replace("surfAlive * surfBore", "surfAlive * _Time.y") })
                Assert.That(CorrectConsumers(mutation), Is.False, "the wiring guard must reject disconnected/wrong-clock consumers");
            string code = Code(src);
            StringAssert.Contains("surfBore=SurfBorePulse01(borePhase,_BreakerBore.x)*birth;", code);
            StringAssert.Contains("float3fringeFoam=FoamAgedColor(fringeAge01,_FoamColor.rgb,_FoamSheetShore.x,col.rgb);", code);
            StringAssert.Contains("col.rgb=lerp(col.rgb,fringeFoam,foamCoverage*_FoamColor.a);", code);
            StringAssert.Contains("col.a=max(col.a,foamCoverage*_FoamColor.a);", code);
            MainSource(src); // Also rules out additional hidden consumers, time reads, samples or loops.
        }

        [Test]
        public void SurfSequence_FringeDialShipsAtZeroOnAllNineMaterials()
        {
            string src = Read(WaterPath);
            StringAssert.Contains("Range(0,1))=0", Code(Section(src, "PROPERTY")));
            Assert.That(Code(Section(src, "UNIFORM")), Is.EqualTo("float_SurfFringeSequenceStrength;"));
            var files = new[] { "Assets/_Project/Art/Materials/Water.mat" }.Concat(
                Directory.GetFiles("Assets/_Project/Art/Materials/WaterPresets", "*.mat").OrderBy(p => p, StringComparer.Ordinal)).ToArray();
            // Re-pinned at the run-up wash (fix/shore-runup-foam): each file gains exactly the four
            // _Wash* keys at the shader defaults (ShoreRunUpWashTests reads them); nothing else moved. Again at v4:
            // three more keys (_WashLeeShare, _WashResidueSoften, _WashLineSlopeCap) and _WashSheetOpacity 0.4 -> 0.2.
            // Again at the shore-normal pulse: two more keys (_SurfSwellYield 1, _SwellShoalDepth 1.5).
            // Again at the snap-back fix (owner v5 clip review 11-15): two more keys (_WashLeftFoamOpacity 0.6, _WashLeftFoamSeconds 2).
            // Again at the wash v7 (the PR 1225 editor look 11-15): _WashLineSlopeCap leaves with the screen-gradient line; two keys join (_WashAnatomyFade 0.05, _WashMaxCover 0.6).
            // Again at the wash v8 (the f07a839 editor look 11-15): two keys join (_WashSurfLift 0, _WashSurfReach 10).
            // Again at the wash v9 (the 7e16896 editor look 11-15): three keys join (_WashGroundScale 1.5, _WashSurfDetail 3, _WashSurfSmooth 1.5).
            string[] originalHashes = {
                "5dd039cd8a55c67e318e045fccebc8fee692051cc85e8aedc54b6cb51a222308",
                "ac4483c482ea5eb691b8c27eb4e14e15c6859c89577f7625060edaa6aa18a02e",
                "39c178b1a846e124e371240b2626f2632979b821b56d77f1efeea520b37d9133",
                "7b99b0cdd901c7b6ed250b9706c1df838e9ea42d92d27a6284aeb6bc5c934fc5",
                "128bbd72d5a9239308a2e8faa1058702eba4accd984b34ba5af617219466a50c",
                "9d725267a945bb41485f1006f04637fded71477b9fe426111f3dee0444151db0",
                "bcb666a283cdc1636ef072814d1f862e2290f570e69a1d6e2a7ef8ddd4ba0226",
                "6e5db5c75dca1fbc78d0bc1e5096763e0a5fce6c2f631aa92a013718e99d42bb",
                "8c19476dc3284362fc6fee40e08b27acc9c59bc280b068bf1254f8d9b26e9b44" };
            Assert.That(files.Length, Is.EqualTo(9));
            for (int i = 0; i < files.Length; i++)
            {
                string yaml = Read(files[i]);
                Assert.That(Regex.Matches(yaml, @"(?m)^    - " + Dial + @": 0$").Count, Is.EqualTo(1), files[i]);
                foreach (string key in new[] { "_FoamTransportStrain", "_FoamTransportCurl", "_FoamTransportCollection" })
                {
                    Assert.That(Regex.Matches(yaml, @"(?m)^    - " + key + @": 0$").Count, Is.EqualTo(1), files[i] + ": " + key);
                    yaml = yaml.Replace("    - " + key + ": 0\n", "");
                }
                // Owner pick (f+e) of 11-20, the sea too dark in a blow: every sea material gains
                // _SwellReadRelative 1 and Water_StormGrey's _PaletteValueFloor goes 0.07 -> 0.16.
                // Exactly those two values are authorized; they are stripped back before the hash.
                Assert.That(Regex.Matches(yaml, @"(?m)^    - _SwellReadRelative: 1$").Count, Is.EqualTo(1), files[i] + ": f+e relative read");
                yaml = yaml.Replace("    - _SwellReadRelative: 1\n", "");
                if (files[i].EndsWith("Water_StormGrey.mat", StringComparison.Ordinal))
                {
                    Assert.That(Regex.Matches(yaml, @"(?m)^    - _PaletteValueFloor: 0\.16$").Count, Is.EqualTo(1), files[i] + ": f+e floor");
                    yaml = yaml.Replace("    - _PaletteValueFloor: 0.16\n", "    - _PaletteValueFloor: 0.07\n");
                }
                // Sunset water fix (11-21): the base Water.mat's ripple band 0.45 -> 0.25 (the diagonal
                // grating it ruled across the sea) and its sun-glitter column 0.6 -> 0.25 (the orange
                // worms at dusk). Exactly those two values are authorized; they are mapped back before the hash.
                if (files[i].EndsWith("Materials/Water.mat", StringComparison.Ordinal))
                {
                    Assert.That(Regex.Matches(yaml, @"(?m)^    - _RippleStrength: 0\.25$").Count, Is.EqualTo(1), files[i] + ": sunset ripple");
                    Assert.That(Regex.Matches(yaml, @"(?m)^    - _SunGlitterStrength: 0\.25$").Count, Is.EqualTo(1), files[i] + ": sunset glitter");
                    yaml = yaml.Replace("    - _RippleStrength: 0.25\n", "    - _RippleStrength: 0.45\n")
                               .Replace("    - _SunGlitterStrength: 0.25\n", "    - _SunGlitterStrength: 0.6\n");
                    // Boat reflections show at dusk (11-21, the owner's look at PR 1370): the base Water.mat
                    // gains the object reflection's own chop fade 0.75 and its low-light lift 4. Exactly
                    // those two new keys are authorized; they are stripped before the hash.
                    Assert.That(Regex.Matches(yaml, @"(?m)^    - _ObjectReflectFadeChop: 0\.75$").Count, Is.EqualTo(1), files[i] + ": object reflection chop fade");
                    Assert.That(Regex.Matches(yaml, @"(?m)^    - _ObjectReflectLowLight: 4$").Count, Is.EqualTo(1), files[i] + ": object reflection low-light lift");
                    yaml = yaml.Replace("    - _ObjectReflectFadeChop: 0.75\n", "").Replace("    - _ObjectReflectLowLight: 4\n", "");
                    // The sea at sunset (11-22, the rowing clip): the base Water.mat gains the low-sun sheen,
                    // the hue hold and the sun sparkles. Exactly these ten new keys are authorized; they are
                    // stripped before the hash. WaterLowSunTests pins what the look-bearing ones hold.
                    foreach (string lowSunKey in new[] { "_SunGlitterForm", "_SunGlitterSpark", "_LowSunHueHold", "_LowSunWakeHold",
                                 "_SunGlitterCell", "_SunGlitterDash", "_LowSunWindow", "_LowSunSheen",
                                 "_LowSunSkyNear", "_LowSunSkyFar" })
                    {
                        string line = @"(?m)^    - " + lowSunKey + @": [^\n]*\n";
                        Assert.That(Regex.Matches(yaml, line).Count, Is.EqualTo(1), files[i] + ": " + lowSunKey);
                        yaml = Regex.Replace(yaml, line, "");
                    }
                }
                Assert.That(Hash(yaml.Replace("    - " + Dial + ": 0\n", "")), Is.EqualTo(originalHashes[i]),
                    files[i] + ": only the new zero key is authorized");
            }
            StringAssert.DoesNotContain(Dial, Read("Assets/_Project/Code/Art/WaterSurface.cs"));
        }

        [Test]
        public void SurfSequence_ProtectedBorePathsRemainUnchanged()
        {
            // Stage C changes shelter inputs and the birth read; the pinned source includes that change.
            // ADR 0040 B2 adds the curl handoff under the lip and barrel and the nearest-crest birth read; the pinned source includes them.
            // Water gains 2 and 3 (#992, ADR 0027 number 7) add the water type, its plumes and the crest glow outside the surf paths; the pinned source includes them.
            // Crest glow L1 (#1004, ADR 0027) lights the thin top on either facing and ships through the water type, outside the surf paths; the pinned source includes it.
            // The shore-normal pulse (fix/shore-runup-foam, owner clip review 11-15) reads the bore's phase and birth on the refracted bore clock (BoreClockPoint, a static jitter) and the fetch seaward of the pixel; the pinned sources include it.
            // The snap-back fix (fix/shore-runup-foam, owner v5 clip review 11-15) keeps the bore's run-in exactly and drains the DRAWN pulse from the reach the run-in got, easing to the new crest's birth by half a period (SurfDrawnBoreBirth01, twin WaterSurface.DrawnPulseBirth01; BreakerMath and SurfAt unchanged) so the run-up no longer jumps at a lull's front, and leaves the last crest's foam on the sand to fade in place (WaterSurface.LeftFoamDrawn / LeftFoamFade01); the pinned sources include it.
            // The wash v10 copies TWIN A (its new GroundFrame) verbatim into the advect pass, which never calls it, so the bore it deposits is unchanged; the pinned sources include it.
            // The wash v9 (fix/shore-runup-foam, the 7e16896 editor look 11-15) breaks the shore lip with a finer churn, boxes the break gate along the shore, reads the bore's deposited foam smooth beside a beach and steps the beach projection back onto the still line (_WashSurfDetail, _WashSurfSmooth, _WashGroundScale); the pinned source includes it.
            // The wash v8 (fix/shore-runup-foam, the f07a839 editor look 11-15) projects the beach's surf read from the warped point, so one shore normal reads one bore, and draws the surf beside a beach as churn thresholded against the break gate, smooth and capped like the wash (_WashSurfReach, _WashSurfLift); the pinned source includes it.
            // The wash v7 (fix/shore-runup-foam, the PR 1225 editor look 11-15) measures the leading line on the ground up the shore normal instead of the screen gradient (no ddx/ddy in the wash), hands the breaker's lip and barrel to the wash above the still line, and caps what the surf's layers hide of the sand (_WashMaxCover); the pinned source includes it.
            // The wake seam fix (fix/bow-foam-and-hull-sorting, owner 11-16 "the two lines that persist ... behind the stern") changes only the HULL dispersal block of the advect pass: its edge starts at half the band's radius and lays (1 - FoamProfile01) of the envelope. That block runs only for a hull injector (disp.x > 0) and adds to foam independently of the bore's deposit, which reads neither foam nor fresh, so the bore it deposits is unchanged; the pinned source includes it.
            // The kelp under the water (feat/kelp-reads-under-water) adds the global _HHUnderWaterClarity, a cover out-parameter on ShallowsPropsBed and UnderWaterPropTransmission, which raises the seabed column's banded transmission (bedT, local to the absorption block) only where a prop under the water covers the bed. It sits in the seabed composite, reads no surf, bore, foam, wash or clock term, and nothing in the surf paths reads it; the B1 blocks are byte-identical; the pinned source includes it.
            // The cut-off pools (fix/cut-off-pools-stay-still-impl, the owner's ask of 11-19) multiply the bore's deposit front by the sea's reach after the front line: exactly 1 wherever the water is joined to the sea, so the bore deposited on every open shore is unchanged, and a pool the tide has cut off takes no new surf residue; the pinned source includes it.
            // Boat reflections at dusk (fix/hull-reflection-shows, the owner's look at PR 1370, 11-21) give the object-reflection block in ObjectReflection() its own chop fade and a low-light lift (_ObjectReflectFadeChop, _ObjectReflectLowLight, both 0 = today's weight exactly) and clamp its final weight to 1. It composites col.rgb only, after the sky mirror, and reads no surf, bore, foam, wash or clock term; the pinned source includes it.
            // The shore wash lee fade (fix/shore-wash-hard-edge) adds _BoreLeeFade: the lee lattice in the surf probe and the run-up's share of the edge and its left foam growing with the break depth (BreakerMath.BoreEdgeLeeFade01 and BoreLeeLattice). With both settings at 0 the older arithmetic is bit-exact; no B1 block moved; the pinned sources include it.
            MainSource(Read(WaterPath)); // Pins the edge, clip, sheet and both twins to approved main.
            Assert.That(Hash(Read(AdvectPath)), Is.EqualTo("c44e6f51d9260d92c832e7c4e4d319fa848475710ba64aaad57a9bc9f5bc0dfa"));
            Assert.That(Hash(Read("Assets/_Project/Code/Core/Environment/BreakerMath.cs")),
                Is.EqualTo("f925b90f7d6431101ad6646a27f72d5c7b8392b11b08d69a2a6bb31ea33f325b"));
            Assert.That(Hash(Read("Assets/_Project/Code/Art/WaterSurface.cs")),
            // Intro at sea PR 2 moves the wind-shape lookup to the hour clock; the pinned source includes it.
            // The shore-normal pulse adds SwellShoreKeep, the CPU twin of the shader's swell yield; the pinned source includes it.
            // The snap-back fix adds DrawnPulseBirth01, LeftFoamDrawn and LeftFoamFade01, the CPU twins of the shader's drawn pulse birth and left foam; the pinned source includes them.
            // The wash v7 adds WashLineDrawn, WashBeachShare01 and WashCapScale, the CPU twins of the shader's ground-measured line, the anatomy's hand-over on the sand and the wash's cover cap; the pinned source includes them.
            // The wash v8 adds BeachSurfEvalPoint, ShoreSurfShare01, ShoreSurfCover01 and OpenSurfCover01, the CPU twins of the shader's beach projection and its shore surf; the pinned source includes them.
            // The wash v9 adds BeachSurfEvalPointRefined, AlongShoreGateDepth and ShoreLip01, the CPU twins of the shader's correcting step onto the still line, the break gate's along-shore box and the lip broken by the churn; the pinned source includes them.
            // The kelp under the water pushes _HHUnderWaterClarity (GameConfig.UnderWaterWeedClarity) beside the shallows floor's switch in PushShallowsFloor; no surf twin moved; the pinned source includes it.
            // The one foam sheet (feat/one-foam-everywhere, the owner's rulings of 11-19) adds PushFoamSheet, the GameConfig.FoamSheet dials pushed beside the shallows floor (the shore line, then the caps and surf, then the storm streaks, then the convergence foam, then the wash under the 11-19 ruling "Yes, unify surf"); the pinned source includes it.
            // The owner's oar look of 11-19 adds IdFoamSheetEdge and pushes GameConfig.FoamSheet.EdgeVector in PushFoamSheet; no surf twin moved; the pinned source includes it.
            // The owner's oar look of 11-19 (the band edges) adds IdFoamSheetDither and pushes GameConfig.FoamSheet.DitherVector in PushFoamSheet; no surf twin moved; the pinned source includes it.
            // The cut-off pools publish the sea's reach from the seabed the water draws (SeaReachGlobals.PublishPainted / PublishElevations, released in OnDisable, its blend refreshed in PushUniforms); no surf twin moved; the pinned source includes it.
            // The shore-wave turn (feat/waves-follow-the-seabed) adds FeedShoreWaveField, built once from the seabed beside the height texture and bound as _ShoreWaveTex; no surf twin moved; the pinned source includes it.
                Is.EqualTo("212e8192a0f94e0c1ba5b245ef763b4fa11c49d9e08067034ff0b6e2235c47e4"));
        }

        static void RequireGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: B1 sequence acceptance requires a GPU slot.");
        }
        readonly Dictionary<int, Measurements> measured = new Dictionary<int, Measurements>();
        Measurements Sequence(int site)
        {
            RequireGpu();
            if (!measured.TryGetValue(site, out var result))
            {
                using (var rig = new RenderRig()) { rig.Initialize(site); result = rig.Measure(); }
                measured.Add(site, result);
            }
            return result;
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SurfSequence_FringeDrainsBetweenEvents(int site)
        {
            var m = Sequence(site);
            foreach (float quiet in m.FringeQuiet)
                Assert.That(quiet, Is.GreaterThanOrEqualTo(.1f), "<5% fringe cover must last .1T in [.35,.65] on BOTH arrivals");
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SurfSequence_ArrivalReadableOverRetainedResidue(int site)
        {
            var m = Sequence(site);
            for (int eventIndex = 0; eventIndex < 2; eventIndex++)
            {
                Assert.That(m.ActiveBins[eventIndex], Is.GreaterThanOrEqualTo(3), "unverified: fewer than three active 1m bins");
                Assert.That(m.ContrastShare[eventIndex], Is.GreaterThanOrEqualTo(.75f), ">=.08 luminance rise in >=75% of active bins");
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SurfSequence_FrontTracksBorePhase(int site)
        {
            var m = Sequence(site);
            for (int e = 0; e < 2; e++)
            {
                Assert.That(m.RidgeSamples[e], Is.GreaterThanOrEqualTo(8), "unverified: no sustained identifiable crest");
                Assert.That(m.RidgeShare[e], Is.GreaterThanOrEqualTo(.75f), "observed ridge must stay within .5m of its phase-90 front");
            }
        }
        [TestCase(0, TestName = "SurfSequence_FringeControlArmsPreserveMain_Glass")]
        [TestCase(1, TestName = "SurfSequence_FringeControlArmsPreserveMain_BeatOff")]
        [TestCase(2, TestName = "SurfSequence_FringeControlArmsPreserveMain_RunUpOff")]
        public void SurfSequence_FringeControlArmsPreserveMain(int control)
        {
            RequireGpu();
            using (var rig = new RenderRig())
            {
                rig.InitializeAt(new Vector2(128f, -71f));
                rig.Control = control;
                for (int i = 0; i < 8; i++)
                {
                    rig.Publish(i * rig.Period / 4f);
                    var baseline = rig.Shot(true, 0f);
                    CollectionAssert.AreEqual(baseline, rig.Shot(false, 0f), "zero vs pinned MAIN");
                    CollectionAssert.AreEqual(baseline, rig.Shot(false, 1f), "disabled arm vs pinned MAIN");
                }
            }
        }
        // W4-3's lee law made the old site 2 a dead lee: no fetch upwind of the rig's wind, so a storm swell's long
        // trains have no room to grow there and break. The bar is read off the W4-3 lee plates (LeeSeaPlatePlayTests,
        // the storm at 0 m off the lee waterline): under the single lee factor the storm's broken surf covered at least
        // 6.5 % of the water within 4 m of the waterline on every shore shot (the least: Nine Mile Creek at high
        // water); under the lee law, none. A third of that least share still fails, and the bar stays above zero: the
        // claim is next to no surf, not none. The control arm, the same storm with the fetch model off, must clear the
        // same bar, so the square is shown able to draw surf.
        static readonly Vector2 DeadLee = new Vector2(50f, -22f);
        const float DeadLeeSurfBar = .02f;
        [Test]
        public void SurfSequence_DeadLeeDrawsNextToNoSurf()
        {
            RequireGpu();
            using (var rig = new RenderRig())
            {
                rig.InitializeAt(DeadLee);
                Assert.That(rig.StormSurfShare(default(WaveFetchSettings)), Is.GreaterThanOrEqualTo(DeadLeeSurfBar),
                    "control: with the fetch model off the storm must break surf in this square");
                Assert.That(rig.StormSurfShare(GameServices.WaveFetch), Is.LessThan(DeadLeeSurfBar),
                    "a dead lee under the storm swell must draw next to no surf");
            }
        }

        sealed class Measurements
        {
            public readonly float[] FringeQuiet = new float[2], ContrastShare = new float[2], RidgeShare = new float[2];
            public readonly int[] ActiveBins = new int[2], RidgeSamples = new int[2];
        }
        sealed class BakedBed : ITidalTerrain
        {
            public Texture2D Texture;
            public Vector4 Rect, Range;
            public float ElevationAt(Vector2 p) => Mathf.Lerp(Range.x, Range.y,
                Texture.GetPixelBilinear((p.x - Rect.x) / Rect.z, (p.y - Rect.y) / Rect.w).r);
        }

        // The existing fixture builds the real NMC shore and publishes the production field. Reflection
        // keeps this PR within its approved files; missing private helpers fail loudly rather than skip.
        // Test-only shader instrumentation redirects time/history bindings and RGB consumers, never
        // phase, geometry, coverage, alpha or deposit maths. All variants are in-memory, not assets.
        sealed class RenderRig : IDisposable
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            const int Pixels = 1120, Steps = 128;
            const float Span = 35f, Level = -2.2f, Extent = 96f;
            static readonly Vector2 Wind = new Vector2(6f, -5.3f);
            // Site 2 sat at (50, -22) until W4-3's lee law made that corner a dead lee: it breaks no surf to score, and
            // SurfSequence_DeadLeeDrawsNextToNoSurf guards it instead. (104.5, -65.5) is the nearest point, on a
            // half-metre search, where the three measures can score: most of its ROI and bins are water the GPU draws,
            // with surf at their predicted arrivals. BakedBed reads the baked texture half a texel (1.48 m, 1.09 m) off
            // the GPU, which leaves most of the ROI of every nearer candidate on the GPU's land.
            static readonly Vector2[] Sites = { new Vector2(128f, -71f), new Vector2(109f, 157.5f), new Vector2(104.5f, -65.5f) };
            // The storm the W4-3 lee plates were shot in (10 m/s), blown along Wind so _WindDir is unchanged.
            static readonly Vector2 StormWind = Wind.normalized * 10f;
            readonly List<Object> owned = new List<Object>();
            BreakerBoreLookTests fixture;
            GameConfig previousConfig;
            Camera camera;
            SpriteRenderer sprite;
            Material candidate, baseline, advect;
            MaterialPropertyBlock original;
            RenderTexture output, history, next;
            Texture2D readback;
            BakedBed bed;
            WaveTrains trains;
            BreakerContour contour;
            Vector2 aim, normal, origin, residual, drift;
            Vector4 historyWorld;
            float scale, halfLife, ageHalfLife, seconds, oldLook, oldDeposit, oldScale;
            bool registrySaved;
            int resolution, site;
            public int Control = -1;
            public float Period { get; private set; }
            T Own<T>(T value) where T : Object { owned.Add(value); return value; }
            T Get<T>(string name) => (T)typeof(BreakerBoreLookTests).GetField(name, Private).GetValue(fixture);
            object Call(string name, params object[] args) => typeof(BreakerBoreLookTests).GetMethod(name, Private).Invoke(fixture, args);

            static Shader Compile(string source, string name)
            {
                source = Regex.Replace(source, "Shader\\s+\"[^\"]+\"", "Shader \"Hidden/B1Sequence/" + name + "\"", RegexOptions.None);
                source = Regex.Replace(source, @"\b_Time\b", "_B1Time");
                source = source.Replace("_HHFoamBufferTex", "_B1HistoryTex").Replace("_HHFoamBufferWorld", "_B1HistoryWorld");
                source = source.Replace("CBUFFER_START(UnityPerMaterial)",
                    "float4 _B1Time; float4 _B1Fringe; float4 _B1Event;\nCBUFFER_START(UnityPerMaterial)");
                // Only the fringe's RGB target changes. _FoamColor also colours other layers: setting
                // that shared material colour would NOT isolate the fringe.
                source = source.Replace("col.rgb = lerp(col.rgb, fringeFoam, foamCoverage * _FoamColor.a);",
                    "col.rgb = lerp(col.rgb, lerp(fringeFoam, _B1Fringe.rgb, _B1Fringe.a), foamCoverage * _FoamColor.a);");
                source = source.Replace("col.rgb = lerp(col.rgb, surfFoam, cover * _SurfColor.a);",
                    "col.rgb = lerp(col.rgb, lerp(surfFoam, _B1Event.rgb, _B1Event.a), cover * _SurfColor.a);");
                source = source.Replace("col.rgb = lerp(col.rgb, lipFoam, lip * _SurfLipColor.a);",
                    "col.rgb = lerp(col.rgb, lerp(lipFoam, _B1Event.rgb, _B1Event.a), lip * _SurfLipColor.a);");
                var create = typeof(ShaderUtil).GetMethod("CreateShaderAsset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(bool) }, null);
                Assert.That(create, Is.Not.Null, "Unity's in-memory shader compiler API is required; no fake render pass");
                var shader = (Shader)create.Invoke(null, new object[] { source, false });
                Assert.That(shader, Is.Not.Null);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "instrumented production shader must compile");
                shader.hideFlags = HideFlags.HideAndDontSave;
                return shader;
            }

            public void Initialize(int index)
            {
                site = index; InitializeAt(Sites[index]);
                normal = Surf(aim).ShorewardDirection;
                Assert.That(normal.sqrMagnitude, Is.GreaterThan(.5f));
            }
            // A dead lee breaks no surf, so it has no shoreward direction to assert: its case starts here.
            public void InitializeAt(Vector2 at)
            {
                aim = at;
                previousConfig = GameServices.Config;
                GameServices.Config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
                Assert.That(GameServices.Config, Is.Not.Null);
                fixture = new BreakerBoreLookTests(); fixture.SetUp(); Call("BuildTheShore"); Call("PublishTheSea", 0f);
                camera = Get<Camera>("_cam"); sprite = Get<GameObject>("_seaGo").GetComponent<SpriteRenderer>();
                original = new MaterialPropertyBlock(); sprite.GetPropertyBlock(original);
                var shipped = sprite.sharedMaterial;
                string src = Read(WaterPath), main = MainSource(src);
                candidate = Own(new Material(shipped) { shader = Own(Compile(src, "Candidate")), hideFlags = HideFlags.HideAndDontSave });
                baseline = Own(new Material(shipped) { shader = Own(Compile(main, "Main")), hideFlags = HideFlags.HideAndDontSave });
                bed = new BakedBed { Texture = (Texture2D)Shader.GetGlobalTexture(SeabedGlobals.Tex),
                    Rect = Shader.GetGlobalVector(SeabedGlobals.Rect), Range = Shader.GetGlobalVector(SeabedGlobals.Range) };
                Assert.That(bed.Texture, Is.Not.Null);
                trains = Get<WaveTrains>("_trainsNow"); Period = BreakerMath.PeriodSeconds(trains.Dominant);
                contour = BreakerMath.ContourFor(trains.Dominant, WaveFetch.TrainEnvelope01(0f, trains.Dominant.Wavelength, GameServices.WaveFetch), GameServices.Breakers);
                scale = shipped.GetFloat("_OceanSwellScale") / (float)typeof(DisplacedWaterSurface).GetField("WaveLegacyScaleRef",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
                output = Get<RenderTexture>("_rt"); camera.targetTexture = null; output.Release();
                output.width = Pixels; output.height = Pixels; output.Create(); camera.targetTexture = output;
                camera.orthographicSize = Span / 2; camera.transform.position = new Vector3(aim.x, aim.y, -1000f);
                camera.nearClipPlane = 1f; camera.farClipPlane = 2000f;
                readback = Own(new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false));
                resolution = FoamBuffer.ResolutionForExtent(Extent);
                var format = FoamBuffer.SelectFormat(SystemInfo.SupportsRenderTextureFormat);
                history = MakeHistory(format); next = MakeHistory(format);
                origin = FoamBuffer.WorldCellOrigin(aim, Extent);
                historyWorld = new Vector4(origin.x, origin.y, Extent, 1f / Extent);
                advect = Own(new Material(Shader.Find("Hidden/HiddenHarbours/FoamBufferAdvect")) { hideFlags = HideFlags.HideAndDontSave });
                advect.SetVector(FoamShaderIds.Resolution, new Vector4(resolution, resolution, 1f / resolution, 1f / resolution));
                advect.SetVectorArray(FoamShaderIds.InjectSeg, new Vector4[FoamBuffer.MaxInjectors]);
                advect.SetVectorArray(FoamShaderIds.InjectShape, new Vector4[FoamBuffer.MaxInjectors]);
                advect.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                // Read shipped lifetime data without taking a URP assembly dependency or changing it.
                var renderer = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/Renderer2D.asset");
                Object feature = renderer.Single(x => x != null && x.GetType().Name == "IsoFacetHullFeature");
                halfLife = (float)feature.GetType().GetField("_foamHalfLifeSeconds", Private).GetValue(feature);
                ageHalfLife = (float)feature.GetType().GetField("_foamAgeHalfLifeSeconds", Private).GetValue(feature);
                drift = WaterSurface.FoamDriftDirection(Wind, Vector2.right, shipped.GetFloat("_FoamDriftWindVsCurrent")) * shipped.GetFloat("_Flow");
                oldLook = FoamInjectionRegistry.LookStrength; oldDeposit = FoamInjectionRegistry.SurfDepositStrength;
                oldScale = FoamInjectionRegistry.DrawnWaveScale; registrySaved = true;
                FoamInjectionRegistry.PublishLookStrength(0f); // Manual history pass, no automatic double step.
            }
            RenderTexture MakeHistory(RenderTextureFormat format)
            {
                var rt = Own(new RenderTexture(resolution, resolution, 0, format) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
                rt.Create(); var previous = RenderTexture.active; RenderTexture.active = rt; GL.Clear(false, true, Color.clear); RenderTexture.active = previous;
                return rt;
            }
            SurfState Surf(Vector2 p) => BreakerMath.SurfAt(p, Level, bed, contour,
                LeeEnvelopeAt(p), trains,
                GameServices.WaveField.Gravity, GameServices.Breakers, scale);
            // The published surf's twin: the dominant train's own lee factor, as GameServices.FetchEnvelopeAt and the
            // shader's break gate read it; the contour above anchors at its fetch-0 value, as WaveFieldBridge does.
            float LeeEnvelopeAt(Vector2 p) => WaveFetch.At(p, Wind, Level, bed, GameServices.WaveFetch).ForWavelength(trains.Dominant.Wavelength);
            public void Publish(float time)
            {
                seconds = time; Call("PublishTheSea", time); trains = Get<WaveTrains>("_trainsNow");
                if (Control == 0) { WaveFieldBridge.PublishGlobals(PackedWaveField.Empty); WaveFieldBridge.PublishBreakersOff(); }
                Shader.SetGlobalVector("_HHSeaLevelWorld", new Vector4(Level, 1, 1, 1));
            }
            // The fixture's PublishTheSea for the storm: the same packing and the same fetch and breaker calls, each
            // train's travel riding in its phase. The fetch settings are the dead-lee case's arm: shipped, or off.
            void PublishStorm(in WaveTrains storm, float time, in WaveFetchSettings fetch)
            {
                var advanced = new WaveTrain[WaveTrains.MaxTrains];
                for (int i = 0; i < storm.Count; i++)
                {
                    WaveTrain tr = storm[i];
                    float k = 2f * Mathf.PI / Mathf.Max(tr.Wavelength, WaveTrain.MinWavelengthMeters);
                    advanced[i] = new WaveTrain(tr.Direction, tr.Wavelength, tr.Amplitude, tr.PhaseOffset - k * tr.PhaseSpeed * time,
                        GameServices.WaveField.Gravity);
                }
                seconds = time; trains = WaveTrains.From(advanced, storm.Count, storm.CrestSharpening, storm.DominantIndex);
                WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in trains));
                WaveFieldBridge.PublishFetchGlobals(fetch, StormWind);
                WaveFieldBridge.PublishBreakerGlobals(trains.Dominant, fetch, GameServices.Breakers);
                Shader.SetGlobalVector("_HHSeaLevelWorld", new Vector4(Level, 1, 1, 1));
            }
            // The peak, over two of the storm's periods, of the share of this test's water in the axis-aligned 8 m
            // square on the aim (every 2nd pixel, as the ROI loop samples) where the shipped dial draws surf or lip.
            public float StormSurfShare(in WaveFetchSettings fetch)
            {
                var water = new List<int>();
                for (int y = 0; y < Pixels; y += 2)
                for (int x = 0; x < Pixels; x += 2)
                {
                    Vector2 p = aim + new Vector2((x + .5f) * Span / Pixels - Span / 2, (y + .5f) * Span / Pixels - Span / 2);
                    if (Mathf.Abs(p.x - aim.x) <= 4 && Mathf.Abs(p.y - aim.y) <= 4 && bed.ElevationAt(p) < Level) water.Add(y * Pixels + x);
                }
                Assert.That(water.Count, Is.GreaterThan(10));
                var storm = WaveMath.TrainsFrom(StormWind, WeatherModel.SeaState01(StormWind.magnitude), GameServices.WaveField);
                float period = BreakerMath.PeriodSeconds(storm.Dominant), peak = 0f;
                for (int frame = 0; frame < 2 * Steps; frame += 4)
                {
                    PublishStorm(storm, frame * period / Steps, fetch);
                    var black = Shot(false, 0f, 3); var white = Shot(false, 0f, 4);
                    peak = Mathf.Max(peak, water.Count(i => Contribution(black[i], white[i]) > .1f) / (float)water.Count);
                }
                return peak;
            }
            void Step(float dt)
            {
                var cells = FoamBuffer.AdvectCells(ref residual, drift * dt);
                var offset = FoamBuffer.SourceOffsetCells(origin, origin, cells);
                var draw = FoamBuffer.DrawOrigin(origin, residual);
                historyWorld = new Vector4(draw.x, draw.y, Extent, 1f / Extent);
                advect.SetTexture(FoamShaderIds.Prev, history); advect.SetVector(FoamShaderIds.BufferWorld, historyWorld);
                advect.SetVector(FoamShaderIds.Shift, new Vector4(offset.x, offset.y, 0, 0));
                advect.SetFloat(FoamShaderIds.Decay, FoamBuffer.DecayFactor(halfLife, dt));
                advect.SetFloat(FoamShaderIds.AgeDecay, FoamBuffer.DecayFactor(ageHalfLife, dt));
                advect.SetVector(FoamShaderIds.SurfDeposit, new Vector4(candidate.GetFloat("_SurfDepositStrength"), scale, dt, 0));
                using (var cmd = new CommandBuffer { name = "B1 sequence history" })
                {
                    cmd.SetRenderTarget(next); cmd.DrawProcedural(Matrix4x4.identity, advect, 0, MeshTopology.Triangles, 3, 1);
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                var swap = history; history = next; next = swap;
            }
            public Color32[] Shot(bool main, float dial, int isolation = 0)
            {
                var mat = main ? baseline : candidate; sprite.sharedMaterial = mat;
                mat.SetVector("_B1Time", new Vector4(seconds / 20, seconds, seconds * 2, seconds * 3));
                mat.SetTexture("_B1HistoryTex", history); mat.SetVector("_B1HistoryWorld", historyWorld);
                mat.SetVector("_B1Fringe", isolation == 1 ? new Vector4(0, 0, 0, 1) : isolation == 2 ? Vector4.one : Vector4.zero);
                mat.SetVector("_B1Event", isolation == 3 ? new Vector4(0, 0, 0, 1) : isolation == 4 ? Vector4.one : Vector4.zero);
                sprite.SetPropertyBlock(original); var block = new MaterialPropertyBlock(); sprite.GetPropertyBlock(block);
                block.SetFloat("_WaterLevel", Level); block.SetVector("_WindDir", new Vector4(Wind.normalized.x, Wind.normalized.y, 0, 0));
                block.SetFloat("_Flow", mat.GetFloat("_Flow"));
                if (!main) block.SetFloat(Dial, dial);
                if (Control == 1) block.SetFloat("_SurfBeatStrength", 0f);
                if (Control == 2) block.SetFloat("_SurfRunUpStrength", 0f);
                // Historical residue diagnostic, unchanged: residue-vs-bare, with surf disabled in BOTH.
                if (isolation == 5 || isolation == 6) block.SetFloat("_SurfStrength", 0f);
                if (isolation == 6) block.SetFloat("_WakeFoamStrength", 0f);
                sprite.SetPropertyBlock(block); camera.Render();
                var old = RenderTexture.active;
                try { RenderTexture.active = output; readback.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0); readback.Apply(); }
                finally { RenderTexture.active = old; }
                return readback.GetPixels32();
            }
            int Pixel(Vector2 p)
            {
                int x = Mathf.FloorToInt((p.x - aim.x + Span / 2) * Pixels / Span);
                int y = Mathf.FloorToInt((p.y - aim.y + Span / 2) * Pixels / Span);
                return x >= 0 && y >= 0 && x < Pixels && y < Pixels ? y * Pixels + x : -1;
            }
            static float Luma(Color32 c) => (.2126f * c.r + .7152f * c.g + .0722f * c.b) / 255f;
            static float Contribution(Color32 black, Color32 white) => Mathf.Max(0f, Luma(white) - Luma(black));
            static bool Visible(Color32 a, Color32 b) => Math.Abs(a.r - b.r) > 2 || Math.Abs(a.g - b.g) > 2 || Math.Abs(a.b - b.b) > 2;

            sealed class Bin
            {
                public Vector2 Point, Normal;
                public float Arrival;
                public readonly List<int> Pixels = new List<int>();
                public readonly List<Vector2> Samples = new List<Vector2>();
                public readonly List<float>[] Quiet = { new List<float>(), new List<float>() };
                public readonly float[] Peak = { float.NegativeInfinity, float.NegativeInfinity }, Active = new float[2];
            }
            float FirstArrival(Vector2 p, float after)
            {
                // Read the PUBLIC phase at the current published time, then follow that crest identity.
                float t = seconds + Mathf.Repeat(Surf(p).BorePhaseDegrees - 90f, 360f) / 360f * Period;
                while (t < after) t += Period;
                return t;
            }
            Vector2 Root(Vector2 guess)
            {
                for (int i = 0; i < 24; i++)
                {
                    Vector2 n = BreakerMath.ShorewardDirection(guess, GameServices.Breakers.SlopeProbeMeters, bed);
                    float env = LeeEnvelopeAt(guess);
                    float target = BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, env);
                    float error = Level - bed.ElevationAt(guess) - target;
                    if (Mathf.Abs(error) < .001f) return guess;
                    float slope = BreakerMath.BedSlopeAlong(guess, n, GameServices.Breakers.SlopeProbeMeters, bed);
                    Assert.That(slope, Is.GreaterThan(.0001f), "contour lost on a flat bed");
                    guess += n * Mathf.Clamp(error / slope, -.5f, .5f);
                }
                Assert.Fail("Contour root did not converge; cannot score an invented break contour.");
                return guess;
            }
            List<Bin> Bins()
            {
                var points = new List<Vector2> { Root(aim) };
                // Small steps follow one connected contour; reject jumps instead of choosing another root.
                foreach (int direction in new[] { -1, 1 })
                {
                    Vector2 p = points[0]; float arc = 0f;
                    for (int i = 0; i < 160; i++)
                    {
                        Vector2 n = BreakerMath.ShorewardDirection(p, GameServices.Breakers.SlopeProbeMeters, bed);
                        Vector2 q = Root(p + direction * new Vector2(-n.y, n.x) * .125f);
                        float length = Vector2.Distance(p, q);
                        Assert.That(length, Is.LessThan(.5f), "disconnected contour root");
                        if (Pixel(q) < 0 || Mathf.Abs(q.x - aim.x) > Span / 2 - 1 || Mathf.Abs(q.y - aim.y) > Span / 2 - 1) break;
                        arc += length; p = q;
                        if (arc >= 1f) { arc -= 1f; points.Add(p); }
                    }
                }
                var bins = new List<Bin>();
                foreach (Vector2 p in points)
                {
                    Vector2 n = BreakerMath.ShorewardDirection(p, GameServices.Breakers.SlopeProbeMeters, bed);
                    var bin = new Bin { Point = p + n * .125f, Normal = n };
                    bin.Arrival = FirstArrival(bin.Point, 8.1f * Period);
                    Vector2 tangent = new Vector2(-n.y, n.x);
                    for (float across = -.5f; across < .5f; across += 1f / 32)
                    for (float shore = 0f; shore < .5f; shore += 1f / 32)
                    {
                        int px = Pixel(p + tangent * across + n * shore);
                        if (px >= 0) bin.Pixels.Add(px);
                    }
                    // Fixed 1/8m quadrature of the same ribbon for the public-maths activity gate.
                    for (float across = -.4375f; across < .5f; across += .125f)
                    for (float shore = .0625f; shore < .5f; shore += .125f)
                        bin.Samples.Add(p + tangent * across + n * shore);
                    Assert.That(bin.Pixels.Count, Is.GreaterThan(0)); bins.Add(bin);
                }
                return bins;
            }

            public Measurements Measure()
            {
                float dt = Period / Steps;
                for (int i = 0; i < 8 * Steps; i++) { Publish(i * dt); Step(dt); }
                Publish(8 * Period);
                float arrival = FirstArrival(aim, 8.1f * Period);
                var bins = Bins(); var roi = new List<int>();
                Vector2 tangent = new Vector2(-normal.y, normal.x);
                for (int y = 0; y < Pixels; y += 2)
                for (int x = 0; x < Pixels; x += 2)
                {
                    Vector2 p = aim + new Vector2((x + .5f) * Span / Pixels - Span / 2, (y + .5f) * Span / Pixels - Span / 2);
                    Vector2 d = p - aim;
                    if (Mathf.Abs(Vector2.Dot(d, tangent)) <= 4 && Vector2.Dot(d, normal) >= 0 && Vector2.Dot(d, normal) <= 4 && bed.ElevationAt(p) < Level)
                    {
                        float env = LeeEnvelopeAt(p);
                        Assert.That(BreakerMath.Breaking01FromContour(.02f, contour, env), Is.GreaterThan(.999f), "criterion requires full shore domain");
                        roi.Add(y * Pixels + x);
                    }
                }
                Assert.That(roi.Count, Is.GreaterThan(10));
                var m = new Measurements(); float[] streak = new float[2], oldStreak = new float[2], oldQuiet = new float[2];
                int[] ridgeGood = new int[2]; float[] lastRidge = { float.NaN, float.NaN };
                string dir = "Evidence~/b1-sequence-tests/" + new[] { "sand", "ledge", "corner" }[site];
                Directory.CreateDirectory(dir);
                File.WriteAllText(dir + "/provenance.txt", FormattableString.Invariant(
                    $"base=b5825e1f772b66adf0b7c4cdabc82416888c02d7\nwater={Hash(Read(WaterPath))}\nheight={bed.Texture.width}x{bed.Texture.height}:{bed.Texture.format}\nheightBytes={Hash(Convert.ToBase64String(bed.Texture.GetRawTextureData()))}\nGPU={SystemInfo.graphicsDeviceName}\nT={Period:R}\nstepsPerT={Steps}\nwarmupT=8\nrecordT=3\nsize={Pixels}\nspan={Span}\nflat NMC fixture; displaced/tide/mood/cost acceptance still requires Phase C\n"));
                // Warm both compiled arms, then prove the test time binding holds the same frame.
                Shot(true, 0f); Shot(false, 0f); Shot(false, 1f);
                CollectionAssert.AreEqual(Shot(false, 1f), Shot(false, 1f), "controlled render time must be stable");
                using (var csv = new StreamWriter(dir + "/frames.csv"))
                {
                    csv.WriteLine("seconds,phase,fringeCover,originalResidueCover,ridgeDistance,phaseFrontDistance");
                    // Three periods allow two complete arrival+quiet windows for every phase-offset bin.
                    for (int frame = 0; frame <= 3 * Steps; frame++)
                    {
                        Publish((8 * Steps + frame) * dt); Step(dt);
                        var dressed = Shot(false, 1f);
                        var black = Shot(false, 1f, 1); var white = Shot(false, 1f, 2);
                        float fringe = roi.Count(i => Contribution(black[i], white[i]) > .1f) / (float)roi.Count;
                        var residue = Shot(false, 1f, 5); var bare = Shot(false, 1f, 6);
                        float oldCover = roi.Count(i => Visible(residue[i], bare[i])) / (float)roi.Count;
                        var eventBlack = Shot(false, 1f, 3); var eventWhite = Shot(false, 1f, 4);
                        float ridge = float.NaN, predicted = float.NaN;
                        for (int e = 0; e < 2; e++)
                        {
                            float age = (seconds - arrival) / Period - e;
                            if (age >= .35f && age <= .65f)
                            {
                                streak[e] = fringe < .05f ? streak[e] + 1f / Steps : 0f;
                                oldStreak[e] = oldCover < .05f ? oldStreak[e] + 1f / Steps : 0f;
                                m.FringeQuiet[e] = Mathf.Max(m.FringeQuiet[e], streak[e]);
                                oldQuiet[e] = Mathf.Max(oldQuiet[e], oldStreak[e]);
                            }
                            if (age >= 0f && age <= .25f)
                            {
                                // All scheduled front samples count, including ambiguous/missing ridges.
                                m.RidgeSamples[e]++;
                                predicted = PredictedFront(arrival + e * Period);
                                ridge = ObservedRidge(dressed, eventBlack, eventWhite, lastRidge[e]);
                                if (!float.IsNaN(ridge)) lastRidge[e] = ridge;
                                if (!float.IsNaN(ridge) && !float.IsNaN(predicted) && Mathf.Abs(ridge - predicted) <= .5f) ridgeGood[e]++;
                            }
                            foreach (var bin in bins)
                            {
                                float u = (seconds - bin.Arrival) / Period - e;
                                if (u >= -.1f && u <= .1f)
                                {
                                    foreach (Vector2 p in bin.Samples)
                                    {
                                        var s = Surf(p);
                                        bin.Active[e] = Mathf.Max(bin.Active[e], s.Breaking01 * s.Whitewater01 * s.Bore01);
                                    }
                                    bin.Peak[e] = Mathf.Max(bin.Peak[e], bin.Pixels.Average(i => Luma(dressed[i])));
                                }
                                if (u >= .35f && u <= .65f) bin.Quiet[e].Add(bin.Pixels.Average(i => Luma(dressed[i])));
                            }
                        }
                        csv.WriteLine(FormattableString.Invariant($"{seconds:R},{Surf(aim).BorePhaseDegrees:R},{fringe:R},{oldCover:R},{ridge:R},{predicted:R}"));
                        if (frame % 32 == 0)
                        {
                            Shot(false, 1f); File.WriteAllBytes(dir + "/" + frame.ToString("D3") + ".png", readback.EncodeToPNG());
                            var main = Shot(true, 0f);
                            CollectionAssert.AreEqual(main, Shot(false, 0f), "zero passthrough against hash-pinned main, same frame/history");
                            csv.Flush(); Debug.Log($"B1_SEQUENCE_PROGRESS site={site} frame={frame}/384");
                        }
                    }
                }
                using (var csv = new StreamWriter(dir + "/bins.csv"))
                {
                    csv.WriteLine("event,x,y,arrival,active,contrast");
                    for (int e = 0; e < 2; e++)
                    {
                        int good = 0;
                        foreach (var bin in bins)
                        {
                            Assert.That(bin.Quiet[e].Count, Is.GreaterThan(0), "incomplete quiet window");
                            bin.Quiet[e].Sort(); float contrast = bin.Peak[e] - bin.Quiet[e][bin.Quiet[e].Count / 2];
                            if (bin.Active[e] >= .1f) { m.ActiveBins[e]++; if (contrast >= .08f) good++; }
                            csv.WriteLine(FormattableString.Invariant($"{e},{bin.Point.x:R},{bin.Point.y:R},{bin.Arrival + e * Period:R},{bin.Active[e]:R},{contrast:R}"));
                        }
                        m.ContrastShare[e] = good / (float)Mathf.Max(1, m.ActiveBins[e]);
                        m.RidgeShare[e] = ridgeGood[e] / (float)Mathf.Max(1, m.RidgeSamples[e]);
                        Debug.Log($"B1_SEQUENCE site={site} event={e}: fringe quiet={m.FringeQuiet[e]:F4}T; original residue quiet={oldQuiet[e]:F4}T ({(oldQuiet[e] >= .1f ? "PASS" : "FAIL")}); active bins={m.ActiveBins[e]}/{bins.Count}; contrast share={m.ContrastShare[e]:F4}; ridge share={m.RidgeShare[e]:F4}. Older foam diagnostic is NOT relabeled.");
                    }
                }
                return m;
            }
            float PredictedFront(float crestArrival)
            {
                // Follow the same unwrapped crest arrival-time field, not whichever 90-degree root is nearest.
                float best = float.MaxValue, distance = float.NaN;
                for (float s = -.5f; s <= 12f; s += 1f / 32)
                {
                    var state = Surf(aim + normal * s);
                    if (state.Breaking01 <= 0f) continue;
                    float localArrival = seconds + Mathf.DeltaAngle(90f, state.BorePhaseDegrees) / 360f * Period;
                    if (Mathf.Abs(localArrival - crestArrival) > Period * .49f) continue;
                    float error = Mathf.Abs(localArrival - seconds);
                    if (error < best) { best = error; distance = s; }
                }
                return best < Period / Steps * 2 ? distance : float.NaN;
            }
            float ObservedRidge(Color32[] full, Color32[] black, Color32[] white, float previous)
            {
                // Independent, fixed image rule: strongest shoreward falling luminance edge, with event
                // RGB contribution present. Association only uses last observed ridge, not the prediction.
                var peaks = new List<(float strength, float distance)>();
                for (float s = -.5f; s <= 12f; s += 1f / 32)
                {
                    if (!float.IsNaN(previous) && (s < previous - .25f || s > previous + .75f)) continue;
                    int a = Pixel(aim + normal * (s - .0625f)), b = Pixel(aim + normal * (s + .0625f));
                    if (a < 0 || b < 0 || Contribution(black[a], white[a]) < .02f) continue;
                    float gradient = Luma(full[a]) - Luma(full[b]);
                    if (gradient > .02f) peaks.Add((gradient, s));
                }
                if (peaks.Count == 0) return float.NaN;
                peaks.Sort((a, b) => b.strength.CompareTo(a.strength));
                var best = peaks[0];
                if (peaks.Any(p => Mathf.Abs(p.distance - best.distance) > .5f && p.strength >= .9f * best.strength)) return float.NaN;
                return best.distance;
            }
            public void Dispose()
            {
                try { if (fixture != null) fixture.TearDown(); }
                finally
                {
                    foreach (Object obj in owned.AsEnumerable().Reverse())
                    {
                        if (obj is RenderTexture rt) rt.Release();
                        if (obj != null) Object.DestroyImmediate(obj);
                    }
                    if (registrySaved) { FoamInjectionRegistry.PublishLookStrength(oldLook); FoamInjectionRegistry.PublishSurfDeposit(oldDeposit, oldScale); }
                    GameServices.Config = previousConfig;
                }
            }
        }
    }
}
