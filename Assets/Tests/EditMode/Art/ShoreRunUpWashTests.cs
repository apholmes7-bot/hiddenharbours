using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for THE RUN-UP WASH (owner shore clip review, 2026-11-15: "just these few foam items
    /// need fixing before posting"). On the St Peters beach the bore's run-up drew four faults, each traced to
    /// a line of <c>HiddenHarboursWater.shader</c>:
    /// <list type="number">
    /// <item>a flat OPAQUE white sheet: the bore's residue in the advected foam buffer, composed at full
    /// coverage (<c>WakeFoamCoverage</c> banded to 1 × <c>_FoamColor.a</c>), and the surf's top band;</item>
    /// <item>a white SLIVER stranded past see-through water: the fringe, which yields to the surf only through
    /// <c>surfSupersede</c> (a FRAGMENT read of the break gate and the bore's age), so it came back at the
    /// wet edge wherever that read dipped;</item>
    /// <item>a 4-px SAWTOOTH seaward edge and a 1-px CHECKER: the buffer's point read on its 1/8 m lattice
    /// (4 screen px a cell), and <c>SurfBandValue</c>'s Bayer dither at low surf cover;</item>
    /// <item>square NOTCHES on the landward edge: the same point-read cells meeting the drawn edge, and the
    /// fringe drawn where boreEdgeBlend is partial. NOT fixed here: where the fetch march's lee flips
    /// boreEdgeBlend to 0 cell by cell (FetchPixelize), the drawn edge itself steps back to the still line;
    /// that is the edge law (WaterSurface twin), not the foam.</item>
    /// </list>
    /// The fix reads every wash quantity off <c>boreEdgeBlend</c>, the drawn edge's own shore quantity (constant
    /// along the shore normal, WaterDrawnEdgeOnePieceTests), so the sheet, its leading foam line and the
    /// fringe's hand-back agree with the edge. CI has no GPU, so these pin the source contract and the
    /// tunables; the look itself is verified on the plate (the PR's v3 clip).
    /// </summary>
    public class ShoreRunUpWashTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        static readonly (string key, string value)[] Dials =
        {
            ("_WashSheetOpacity", "0.2"),
            ("_WashLineWidth", "0.1"),
            ("_WashLineOpacity", "0.85"),
            ("_WashSeawardDepth", "0.8"),
            ("_WashPresenceRamp", "0.1"),
            // v4 (the owner's v3 clip review): the lee's slab and the residue's stair.
            ("_WashLeeShare", "1"),
            ("_WashResidueSoften", "0.125"),
            ("_WashLineSlopeCap", "0.5"),
            // v6 (the owner's v5 clip review, "the return/pull is jarring"): the foam a wave leaves on the sand.
            ("_WashLeftFoamSeconds", "2"),
            ("_WashLeftFoamOpacity", "0.6"),
        };

        static string Src() => File.ReadAllText(ShaderPath, Encoding.UTF8).Replace("\r\n", "\n");

        static string[] Materials() => new[] { "Assets/_Project/Art/Materials/Water.mat" }
            .Concat(Directory.GetFiles("Assets/_Project/Art/Materials/WaterPresets", "*.mat")
                .OrderBy(p => p, System.StringComparer.Ordinal))
            .ToArray();

        static int IndexOf(string src, string needle)
        {
            int at = src.IndexOf(needle, System.StringComparison.Ordinal);
            Assert.Greater(at, 0, "not found in the water shader: " + needle);
            return at;
        }

        [Test]
        public void TheWashDials_AreDeclaredOnce_AtTheirDefaults()
        {
            string src = Src();
            foreach (var (key, value) in Dials)
            {
                var decl = Regex.Matches(src, @"(?m)^ *" + Regex.Escape(key) + @" +\(""[^""]*"", Range\([^)]*\)\) = " +
                                              Regex.Escape(value) + "$");
                Assert.AreEqual(1, decl.Count, key + " must be declared once in Properties at " + value);
            }
            Assert.AreEqual(1, Regex.Matches(src,
                @"float  _WashSheetOpacity, _WashLineWidth, _WashLineOpacity, _WashSeawardDepth, _WashPresenceRamp;").Count,
                "the five wash uniforms must be declared once, beside the surf's");
            Assert.AreEqual(1, Regex.Matches(src,
                    @"float  _WashLeeShare, _WashResidueSoften, _WashLineSlopeCap;").Count,
                "the three v4 wash uniforms must be declared once");
            Assert.AreEqual(1, Regex.Matches(src, @"float  _WashLeftFoamSeconds, _WashLeftFoamOpacity;").Count,
                "the two v6 left-foam uniforms must be declared once");
        }

        // ---- v6: the snap-back (owner clip review 2026-11-15: "it snaps back to the ocean instantaneiously as the
        // new wave approaches shore ... the return/pull is jarring"; "remove any jaggedness") -------------------

        const float G = 9.81f;

        static WaveTrains GroupTrains()
        {
            // The CurlingBreakerTests group sea: a 30 m swell beating with a 21 m train, so its crests are born unequal.
            var swell = new WaveTrain(Vector2.up, 30f, 0.3f, 0f, G);
            var group = new WaveTrain(Vector2.up, 21f, 0.27f, 0f, G);
            var silent = new WaveTrain(Vector2.up, 30f, 0f, 0f, G);
            return new WaveTrains(swell, group, silent, silent, 2, 2.6f);
        }

        static float WorstStep(bool drawn, out int jumps) => WorstStep(drawn, out jumps, out _);

        static float WorstStep(bool drawn, out int jumps, out float runInDiff)
        {
            WaveTrains trains = GroupTrains();
            WaveTrain swell = trains.Dominant;
            BreakerSettings s = BreakerSettings.Default;
            float period = BreakerMath.PeriodSeconds(in swell);
            var breakLine = new Vector2(0f, -4f);
            const float travel = 1.5f;
            const int PerPeriod = 600;                    // 0.007 s at this period: finer than a 60 fps frame
            float worst = 0f, last = float.NaN;
            jumps = 0;
            runInDiff = 0f;
            for (int i = 0; i <= 8 * PerPeriod; i++)
            {
                double t = 100d + period * i / (double)PerPeriod;
                float phase = BreakerMath.BorePhaseDegrees(in swell, breakLine, travel, 1f, t);
                float since = BreakerMath.BoreBirthEnergy01(in trains, breakLine, travel, phase, period,
                                                            WaveFetchSample.Open, s.BoreSetStrength, 1f, t);
                float birth = since;
                if (drawn)
                {
                    float previous = BreakerMath.BoreBirthEnergy01(in trains, breakLine, travel + period, phase, period,
                                                                   WaveFetchSample.Open, s.BoreSetStrength, 1f, t);
                    float sinceS = BreakerMath.SecondsSinceTheCrest(phase, period);
                    birth = WaterSurface.DrawnPulseBirth01(since, previous, sinceS, period);
                    if (sinceS >= period * 0.5f) runInDiff = Mathf.Max(runInDiff, Mathf.Abs(birth - since));
                }
                float bore = BreakerMath.BorePulse01(phase, s.BorePulseSharpness) * birth;
                if (!float.IsNaN(last))
                {
                    float step = Mathf.Abs(bore - last);
                    worst = Mathf.Max(worst, step);
                    if (step >= 0.05f) jumps++;
                }
                last = bore;
            }
            return worst;
        }

        [Test]
        public void TheBorePulse_KeepsTheRunInsReachThroughTheFront_SoTheEdgeNeverJumps()
        {
            // The run-up is the pulse times its crest's birth. The shipped birth (seconds SINCE the last crest)
            // changed owner AT the front, where the pulse is 1: in a lull the run-up and the drawn edge jumped from
            // the run-in's reach to the small new crest's in one frame (the snap-back, and the notch racing along
            // the waterline where that front crossed it). The drawn birth keeps the run-in exactly and drains from
            // its reach, easing to the new crest's by half a period, where the pulse is 0.
            float shipped = WorstStep(false, out int shippedJumps);
            float fixedStep = WorstStep(true, out int fixedJumps, out float runInDiff);
            Debug.Log($"[left-foam] bore step per 1/600 period: since-crest {shipped:F3} ({shippedJumps} jumps), " +
                      $"drawn {fixedStep:F3} ({fixedJumps} jumps), run-in difference {runInDiff:F6}");
            Assert.GreaterOrEqual(shippedJumps, 3, "premise: the since-crest pulse jumps at the group's unequal fronts");
            Assert.AreEqual(0, fixedJumps, "the drawn pulse must be continuous in time");
            Assert.Less(fixedStep, 0.03f, "the drawn pulse must move only as fast as its own shape");
            Assert.AreEqual(0f, runInDiff, 0f, "the run-in (\"the in is great\") must be exactly the shipped one");
            Assert.AreEqual(BreakerMath.BorePulse01(90f, 2.6f), 1f, 1e-5f, "premise: the pulse peaks at the front");
            // The blend itself: the previous birth at the front, the new one from half a period on.
            Assert.AreEqual(0.9f, WaterSurface.DrawnPulseBirth01(0.4f, 0.9f, 0f, 8f), 1e-6f);
            Assert.AreEqual(0.4f, WaterSurface.DrawnPulseBirth01(0.4f, 0.9f, 4f, 8f), 1e-6f);
            Assert.AreEqual(0.4f, WaterSurface.DrawnPulseBirth01(0.4f, 0.9f, 7f, 8f), 1e-6f);
            Assert.AreEqual(0.65f, WaterSurface.DrawnPulseBirth01(0.4f, 0.9f, 2f, 8f), 1e-6f);
        }

        [Test]
        public void TheShader_DrainsFromTheRunInsReach_AndAgesTheSheetOnTheLastCrest()
        {
            string src = Src();
            int fn = IndexOf(src, "float SurfDrawnBoreBirth01(float sinceCrestBirth, float previousBirth, float phaseDeg, float periodS)");
            int sheet = IndexOf(src, "float sheetBirth = SurfBoreBirth01(SurfBoreClockPoint(breakLinePt), travelS, borePhase, periodS,");
            int prev = IndexOf(src, "float previousBirth = SurfBoreBirth01(SurfBoreClockPoint(breakLinePt), travelS + periodS, borePhase,");
            int drawn = IndexOf(src, "float birth = SurfDrawnBoreBirth01(sheetBirth, previousBirth, borePhase, periodS);");
            int pulse = IndexOf(src, "surfBore = SurfBorePulse01(borePhase, _BreakerBore.x) * birth;");
            int sheetUse = IndexOf(src, "surfSheet = SurfBoreSheet01(borePhase, periodS) * sheetBirth;");
            Assert.That(fn < sheet && sheet < prev && prev < drawn && drawn < pulse && pulse < sheetUse);
            string body = src.Substring(fn, src.IndexOf("\n            }", fn, System.StringComparison.Ordinal) - fn);
            StringAssert.Contains("if (halfS <= 1e-4 || sinceS >= halfS) return sinceCrestBirth;", body,
                "the twin of WaterSurface.DrawnPulseBirth01 keeps the run-in unread");
            StringAssert.Contains("return lerp(previousBirth, sinceCrestBirth, u * u * (3.0 - 2.0 * u));", body);
            StringAssert.Contains("leftFoamReachM = SurfRunUpMeters(standingAtBreak, surfAlive, surfXi, previousBirth) * boreEdge;", src,
                "the foam left on the sand is the reach the edge got at the last front");
        }

        [Test]
        public void TheFoamAWaveLeaves_StaysWhereItReached_AndFadesInPlace()
        {
            // The C# twin of the shader's leftFoamOnly + its fade (WaterSurface.LeftFoamDrawn / LeftFoamFade01).
            const float Cap = 0.35f, Blend = 1f, Secs = 2f, Op = 0.6f;
            const float reach = 0.3f;                    // the last crest got 0.3 m of level up the beach
            // Behind the draining edge, inside the reach: left foam.
            Assert.IsTrue(WaterSurface.LeftFoamDrawn(-0.2f, 0f, 0.05f, reach, Blend, Cap, Secs, Op));
            // Still under the live edge: water, not left foam.
            Assert.IsFalse(WaterSurface.LeftFoamDrawn(-0.02f, 0f, 0.05f, reach, Blend, Cap, Secs, Op));
            // Beyond where the crest ever reached: dry sand.
            Assert.IsFalse(WaterSurface.LeftFoamDrawn(-0.32f, 0f, 0.05f, reach, Blend, Cap, Secs, Op));
            // Either dial at 0, or a bore that does not own the edge: none.
            Assert.IsFalse(WaterSurface.LeftFoamDrawn(-0.2f, 0f, 0.05f, reach, Blend, Cap, 0f, Op));
            Assert.IsFalse(WaterSurface.LeftFoamDrawn(-0.2f, 0f, 0.05f, reach, Blend, Cap, Secs, 0f));
            Assert.IsFalse(WaterSurface.LeftFoamDrawn(-0.2f, 0f, 0.05f, reach, 0f, Cap, Secs, Op));

            // The fade: 1 as its crest passes, then down smoothly, never a step (60 fps frames over 3 periods).
            Assert.AreEqual(1f, WaterSurface.LeftFoamFade01(0f, Secs), 1e-6f);
            float prev = 1f, worst = 0f;
            for (int f = 1; f <= 60 * 15; f++)
            {
                float v = WaterSurface.LeftFoamFade01(f / 60f, Secs);
                Assert.LessOrEqual(v, prev, "the left foam only ever thins");
                worst = Mathf.Max(worst, prev - v);
                prev = v;
            }
            Assert.Less(worst, 1f / 60f / Secs + 1e-4f, "no frame takes more than its share of the fade");
            Assert.AreEqual(0f, WaterSurface.LeftFoamFade01(1f, 0f), "0 seconds = no left foam");

            string src = Src();
            StringAssert.Contains("float leftFade = exp(-max(leftFoamAgeS, 0.0) / max(_WashLeftFoamSeconds, 1e-3));", src);
            StringAssert.Contains("leftFoamAgeS = SurfSecondsSinceCrest(borePhase, periodS);", src,
                "the left foam ages on the since-crest clock: pure time, no history buffer");
            StringAssert.Contains("if (!leftFoamOnly) clip(depth + edgeSwash + 1e-4);", src);
        }

        [Test]
        public void EveryWaterMaterial_SerializesTheWashDials_AtTheShaderDefaults()
        {
            // 'Apply water preset' copies wholesale (BreakerBoreLookTests): a preset missing a key stamps
            // the shader default over the hero material. Serialized everywhere, at the defaults.
            string[] mats = Materials();
            Assert.AreEqual(9, mats.Length);
            foreach (string mat in mats)
            {
                string yaml = File.ReadAllText(mat, Encoding.UTF8).Replace("\r\n", "\n");
                foreach (var (key, value) in Dials)
                    Assert.AreEqual(1, Regex.Matches(yaml, @"(?m)^    - " + Regex.Escape(key) + ": " +
                                                            Regex.Escape(value) + "$").Count,
                        Path.GetFileName(mat) + " must carry " + key + ": " + value);
            }
        }

        [Test]
        public void TheWash_IsReadOffTheDrawnEdge_AfterItsClip()
        {
            string src = Src();
            int clip = IndexOf(src, "clip(depth + edgeSwash + 1e-4);");
            int presence = IndexOf(src,
                "float washPresence = max(smoothstep(0.0, max(_WashPresenceRamp, 1e-3), boreEdgeBlend), washSeaBreaks);");
            // v4: on the lee of the SAME beach, in a sea that breaks, the wash keeps its look (_WashLeeShare):
            // where boreEdgeBlend fell to 0 along the shore the fringe came back as a detached opaque slab.
            int lee = IndexOf(src, "* saturate(_WashLeeShare) : 0.0;");
            int gate = IndexOf(src,
                "float washOnSand = washPresence * (1.0 - smoothstep(0.0, max(_WashSeawardDepth, 1e-3), depth));");
            Assert.That(lee < presence && presence < gate);
            // v4: the line's width is measured on the drawn edge's own screen gradient, not through the floored
            // swashSlope, which on a flat put metres of sand inside the line (the white wedge).
            int edgeQ = IndexOf(src, "float washEdgeQ  = depth + edgeSwash;");
            // ...capped at the steepest real edge, so a JUMP in the run-up across the sheet draws no seam line.
            int lead = IndexOf(src, "float washLeadM  = washEdgeQ / clamp(washGradM, 1e-4, max(_WashLineSlopeCap, 1e-4));");
            Assert.Less(edgeQ, lead);
            int line = IndexOf(src,
                "float washLine   = washOnSand * step(washLeadM, max(_WashLineWidth, 0.0)) * saturate(_WashLineOpacity);");
            Assert.That(clip < gate && gate < lead && lead < line,
                "the wash must read the SAME edgeSwash the clip draws, after it: the leading line is then " +
                "attached to the drawn edge by construction, a hard step on whole pixels with no dither");
            // v6: the line's screen gradient is taken BEFORE the clip. Taken after a discard only some lanes of a 2x2
            // quad run (the left foam keeps its lanes), it came back undefined where a left-foam pixel shared the quad
            // with the edge's first wet row: the line drew on even rows only, a 2-px stair (v6f1 clip scan, 11-15).
            int grad = IndexOf(src, "float washGradM  = length(float2(ddx(washEdgeQ), ddy(washEdgeQ)))");
            Assert.That(edgeQ < grad && grad < clip, "the edge's derivative is taken before anything is discarded");
            Assert.AreEqual(1, Regex.Matches(src, @"ddx\(washEdgeQ\)").Count);
        }

        [Test]
        public void TheFringe_YieldsToTheWash_ThroughTheEdgesShoreQuantity()
        {
            string src = Src();
            int supersede = IndexOf(src, "foamCoverage *= (1.0 - surfSupersede);");
            int yield = IndexOf(src, "foamCoverage *= 1.0 - washPresence * step(0.001, _WashLineOpacity);");
            int compose = IndexOf(src, "col.rgb = lerp(col.rgb, _FoamColor.rgb, foamCoverage * _FoamColor.a);");
            Assert.That(supersede < yield && yield < compose,
                "the fringe's hand-back to the wash must sit between the surf supersede and the fringe compose");
            // The fence the one-piece edge fix set stays: the fringe's DEPTH still rides boreFoamBlend.
            StringAssert.Contains("surfRunUpM, boreFoamBlend)", src);
        }

        [Test]
        public void TheWashSheet_HasNoBayerChecker_AndTheWashOpacity()
        {
            string src = Src();
            // v4: inside the wash the sheet is smooth (no hard bands racing along the shore as a patch).
            StringAssert.Contains("float cover = lerp(SurfBandValue(surfCover, washOnSand > 0.001 ? 0.5 : bay), saturate(surfCover),", src);
            StringAssert.Contains("* lerp(1.0, saturate(_WashSheetOpacity), saturate(washOnSand));", src);
            int lip = IndexOf(src, "col.a   = max(col.a, lip * _SurfLipColor.a);");
            int washLine = IndexOf(src, "col.rgb = lerp(col.rgb, _FoamColor.rgb, washLine);");
            Assert.Greater(washLine, lip, "the leading line is laid over the surf, last");
        }

        [Test]
        public void TheResidue_IsReadSmooth_OnTheWashOnly()
        {
            string src = Src();
            int fnAt = IndexOf(src, "float WashResidueCoverage(float2 worldXY, out float freshness)");
            string body = src.Substring(fnAt, src.IndexOf("\n            }", fnAt, System.StringComparison.Ordinal) - fnAt);
            StringAssert.Contains("float o = 0.5 * max(_WashResidueSoften, 0.0) * _HHFoamBufferWorld.w;", body);
            Assert.AreEqual(4, Regex.Matches(body,
                    @"SAMPLE_TEXTURE2D_LOD\(_HHFoamBufferTex, sampler_LinearClamp, uv \+ float2\([ -]o, +[ -]o\), 0\)\.rg").Count,
                "a 4-tap LINEAR tent over coverage AND freshness is what turns the buffer's 1/8 m cells into a " +
                "smooth outline: a point-read of either drew the 4-px stair");
            StringAssert.Contains("freshness = tent.y;", body, "the wash's colour age is read through the same tent");
            StringAssert.DoesNotContain("BandValue01", body, "no hard bands on the wash: they drew a racing patch");
            StringAssert.Contains("saturate(_WashSheetOpacity)", body, "the residue wears the wash's opacity");

            // Open water keeps the lattice: the wake's own read is still the buffer's point sampler.
            int wakeAt = IndexOf(src, "float WakeFoamCoverage(");
            string wake = src.Substring(wakeAt, src.IndexOf("\n            }", wakeAt, System.StringComparison.Ordinal) - wakeAt);
            StringAssert.Contains("SAMPLE_TEXTURE2D(_HHFoamBufferTex, sampler_HHFoamBufferTex, uv)", wake);

            int zone = IndexOf(src, "float washZone = saturate(washOnSand);");
            int call = IndexOf(src, "float wakeFoam = WakeFoamCoverage(worldXY, washZone > 0.001 ? 0.5 : bay, wakeFresh);");
            int swap = IndexOf(src, "wakeFoam  = lerp(wakeFoam, WashResidueCoverage(worldXY, washFresh), washZone);");
            int fresh = IndexOf(src, "wakeFresh = lerp(wakeFresh, washFresh, washZone);");
            Assert.That(zone < call && call < swap && swap < fresh,
                "the residue swap rides the wake read, gated to the wash, with no Bayer in the hand-off band");
        }

        // ---- the arithmetic the no-checker claim rests on (twin of SurfBandValue / BandValue01) ----------

        static readonly float[] Bayer4 =
            { 0.5f, 8.5f, 2.5f, 10.5f, 12.5f, 4.5f, 14.5f, 6.5f, 3.5f, 11.5f, 1.5f, 9.5f, 15.5f, 7.5f, 13.5f, 5.5f };

        static float Sat(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static float SurfBandValue(float v01, float bay, float bands, float dither)
        {
            if (bands < 2f) return Sat(v01);
            float steps = bands - 1f;
            float dithered = Sat(v01) + (bay - 0.5f) * (Sat(dither) / System.Math.Max(steps, 1f));
            return (float)System.Math.Floor(Sat(dithered) * steps + 0.5f) / steps;
        }

        static float BandValue01(float v01, float bandCount, float ditherWin, float bayer)
        {
            float bands = System.Math.Max(bandCount, 2f);
            float x = Sat(v01) * (bands - 1f);
            float fb = (float)System.Math.Floor(x);
            float win = System.Math.Min(System.Math.Max(ditherWin, 1e-3f), 1f);
            float e = Sat(((x - fb) - (0.5f - 0.5f * win)) / win);
            return (fb + (e > bayer ? 1f : 0f)) / (bands - 1f);
        }

        [Test]
        public void OnTheWash_EveryDitherCellAgrees_SoNoCheckerCanForm()
        {
            // The shipped dials (Water.mat): _SurfBands 4, _SurfBandDither 0.7, _WakeFoamBands 3.
            // The wash path hands 0.5 in place of the cell's Bayer threshold, so it is plain rounding
            // to the nearest band: one value for every pixel of a cell, whatever its Bayer slot.
            for (int i = 0; i < 512; i++)
            {
                float v = (i + 0.37f) / 512f; // off the exact ties, where BandValue01's 1e-3 window decides

                Assert.AreEqual(System.Math.Floor(v * 3f + 0.5f) / 3f, SurfBandValue(v, 0.5f, 4f, 0.7f), 1e-6, "surf v=" + v);
                Assert.AreEqual(System.Math.Floor(v * 2f + 0.5f) / 2f, BandValue01(v, 3f, 0f, 0.5f), 1e-6, "residue v=" + v);
            }

            // ...where the shipped path (each pixel's own Bayer threshold) splits a 4x4 cell into two
            // values around every surf band boundary: the 1-px checker the owner saw on the sand.
            foreach (float boundary in new[] { 1f / 6f, 0.5f, 5f / 6f })
            {
                var seen = new System.Collections.Generic.HashSet<float>();
                foreach (float b in Bayer4) seen.Add(SurfBandValue(boundary, b / 16f, 4f, 0.7f));
                Assert.AreEqual(2, seen.Count, "the shipped dither splits the cell at v=" + boundary);
            }
        }
    }
}
