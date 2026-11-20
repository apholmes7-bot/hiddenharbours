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
            // v7 (the editor look of 11-15): the lip and barrel give way on the sand; the cap on what the wash hides.
            // (_WashLineSlopeCap left with the screen-gradient line it capped.)
            ("_WashAnatomyFade", "0.05"),
            ("_WashMaxCover", "0.6"),
            // v6 (the owner's v5 clip review, "the return/pull is jarring"): the foam a wave leaves on the sand.
            ("_WashLeftFoamSeconds", "2"),
            ("_WashLeftFoamOpacity", "0.6"),
            // v8 (the editor look of f07a839): the shore surf seaward of the wash, thresholded in the wash's language.
            ("_WashSurfReach", "10"),
            ("_WashSurfLift", "0"),
            // v9 (the editor look of 7e16896): the shore churn's detail, the break gate's along-shore box, the ground's
            // read scale.
            ("_WashSurfDetail", "3"),
            ("_WashSurfSmooth", "1.5"),
            ("_WashGroundScale", "1.5"),
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
                    @"float  _WashLeeShare, _WashResidueSoften, _WashAnatomyFade, _WashMaxCover;").Count,
                "the v4 and v7 wash uniforms must be declared once");
            StringAssert.DoesNotContain("_WashLineSlopeCap", src, "the slope cap left with the screen-gradient line");
            Assert.AreEqual(1, Regex.Matches(src, @"float  _WashLeftFoamSeconds, _WashLeftFoamOpacity;").Count,
                "the two v6 left-foam uniforms must be declared once");
            Assert.AreEqual(1, Regex.Matches(src, @"float  _WashSurfReach, _WashSurfLift;").Count,
                "the two v8 shore-surf uniforms must be declared once");
            Assert.AreEqual(1, Regex.Matches(src, @"float  _WashSurfDetail, _WashSurfSmooth, _WashGroundScale;").Count,
                "the three v9 shore-surf uniforms must be declared once");
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
            int edgeQ = IndexOf(src, "float washEdgeQ  = depth + edgeSwash;");
            // v7 (the editor look of 11-15): the line is a per-pixel test on the GROUND one line-width up the shore
            // normal, with the run-up held, so it is _WashLineWidth metres of ground on a flat or a slope alike.
            // v8: up the LOCAL shore normal (groundDir), not the waterline point's: on the sand the line's tap was
            // aimed by a point that moves with the warp, so its ground step wandered at peak (the shredded line).
            int open = IndexOf(src, "if (washOnSand > 0.001 && washEdgeQ >= -1e-4 && dot(groundDir, groundDir) > 1e-6)");
            int land = IndexOf(src,
                "float washLandDepth = _WaterLevel - SeabedElevation(worldXY + warp + groundDir * max(_WashLineWidth, 0.0));");
            int past = IndexOf(src, "if (washLandDepth + edgeSwash < 0.0)");
            int line = IndexOf(src, "washLine = washOnSand * saturate(_WashLineOpacity);");
            Assert.That(edgeQ < clip && clip < gate && gate < open && open < land && land < past && past < line,
                "the wash must read the SAME edgeSwash the clip draws, after it: the leading line is then " +
                "attached to the drawn edge by construction, a hard step on whole pixels with no dither");
            // v4-v6 divided washEdgeQ by its SCREEN gradient. ddx/ddy are taken per 2x2 quad, so one value decided the
            // line for four pixels: where the edge's quad held left-foam or drained lanes the line skipped the edge's
            // first wet row, and the waterline drew on even rows only (the 2-px stair: 97-98 % of the drain frames'
            // columns on the PR 1225 editor look). The wash reads no screen derivative now; the only ones left in the
            // shader are the shallows floor's explicit texture gradients.
            StringAssert.DoesNotContain("washGradM", src);
            StringAssert.DoesNotContain("ddx(washEdgeQ)", src);
            Assert.AreEqual(1, Regex.Matches(src, @"\bddx\(").Count, "only the floor's ddx(worldXY)");
            Assert.AreEqual(1, Regex.Matches(src, @"\bddy\(").Count, "only the floor's ddy(worldXY)");
            Assert.AreEqual(0, Regex.Matches(src, @"\bfwidth\(").Count);
        }

        // ---- v7: the stair, the slab and the cap (the PR 1225 editor look, 11-15) --------------------------------

        const float PxPerM = 96f;                        // the look's framing: ortho 5.625 at 1080 rows
        const float CodeM = 12f / 255f;                  // NMC's 8-bit height: -6..6 m over 255 codes
        const float TexelM = 0.5f;                       // NMC's height map: 2 texels a metre, bilinear

        // An 8-bit, bilinear beach rising shoreward at `slope` (x = metres up the shore normal).
        static float Elev(float x, float slope, float e0)
        {
            float u = x / TexelM;
            int i = Mathf.FloorToInt(u);
            float Code(int k) => Mathf.Round((e0 + slope * k * TexelM + 6f) / CodeM);
            return -6f + Mathf.Lerp(Code(i), Code(i + 1), u - i) * CodeM;
        }

        [Test]
        public void TheLeadingLine_StartsOnTheEdgesFirstWetRow_AtEveryParity_AndIsItsWidthOfGround()
        {
            const float Water = 0.29f, RunUp = 0.35f, Width = 0.1f;
            int[] firstParity = new int[2];
            foreach (float slope in new[] { 0.05f, 0.1f, 0.165f, 0.3f, 0.5f })
            {
                float e0 = Water + RunUp - slope * 2f;       // the drawn edge 2 m up the frame
                for (int k = 0; k < 64; k++)                 // slide the beach under the pixel grid by 1/32 px
                {
                    float offset = k / (32f * PxPerM);
                    int firstWet = -1, firstLine = -1, lastLine = -1, lines = 0;
                    // Rows run from the land (row 0) down to the sea, as on the screen.
                    for (int row = 0; row < 400; row++)
                    {
                        float x = (400 - row) / PxPerM + offset;
                        float depth = Water - Elev(x, slope, e0);
                        float landward = Water - Elev(x + Width, slope, e0);
                        bool wet = depth + RunUp >= -1e-4f;
                        bool line = WaterSurface.WashLineDrawn(depth, landward, RunUp);
                        if (wet && firstWet < 0) firstWet = row;
                        if (line)
                        {
                            Assert.IsTrue(wet, "the line is only ever drawn on the wash");
                            if (firstLine < 0) firstLine = row;
                            Assert.IsTrue(lastLine < 0 || lastLine == row - 1, $"slope {slope}: the line is one piece");
                            lastLine = row; lines++;
                        }
                    }
                    Assert.GreaterOrEqual(firstWet, 0, "premise: the edge is in frame");
                    Assert.AreEqual(firstWet, firstLine, $"slope {slope}, offset {k}: the line starts on the first wet row");
                    Assert.That(lines, Is.InRange(Mathf.FloorToInt(Width * PxPerM), Mathf.CeilToInt(Width * PxPerM)),
                        $"slope {slope}: {Width} m of ground is {Width * PxPerM:F1} px at the look's framing");
                    firstParity[firstLine & 1]++;
                }
            }
            Debug.Log($"[wash line] first line row even {firstParity[0]}, odd {firstParity[1]}");
            Assert.Greater(firstParity[0], 0);
            Assert.Greater(firstParity[1], 0, "the edge resolves at 1 px: it lands on odd rows as readily as even ones");
        }

        [Test]
        public void OnTheSand_TheLipAndBarrelGiveWay_AndTheCapKeepsTheGroundShowing()
        {
            // The slab: every beach pixel reads the surf at its waterline point, so on a plunging shore the whole run-up
            // wore the waterline's lip at its top band. The anatomy is untouched in the water and gone a few cm of level up.
            const float Fade = 0.05f;
            Assert.AreEqual(0f, WaterSurface.WashBeachShare01(0.3f, 1f, Fade), "seaward: the breaker draws as it did");
            Assert.AreEqual(0f, WaterSurface.WashBeachShare01(0f, 1f, Fade), "at the still line: as it did");
            Assert.AreEqual(0.5f, WaterSurface.WashBeachShare01(-0.025f, 1f, Fade), 1e-6f);
            Assert.AreEqual(1f, WaterSurface.WashBeachShare01(-0.05f, 1f, Fade), 1e-6f);
            Assert.AreEqual(1f, WaterSurface.WashBeachShare01(-0.35f, 1f, Fade), 1e-6f);
            Assert.AreEqual(0f, WaterSurface.WashBeachShare01(-0.2f, 0f, Fade), "no wash, no hand-over");
            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float v = WaterSurface.WashBeachShare01(-0.4f * i / 100f, 1f, Fade);
                Assert.GreaterOrEqual(v, prev, "the hand-over only grows up the beach"); prev = v;
            }

            // The cap: whatever the heaviest surf layer weighs on the sand, what it hides of the ground is <= the cap,
            // and a layer already under the cap passes untouched (the sheet's 0.2 and the left foam's 0.6 are unchanged).
            foreach (float cap in new[] { 0f, 0.3f, 0.6f, 1f })
                for (int i = 0; i <= 100; i++)
                {
                    float w = i / 100f;
                    float scale = WaterSurface.WashCapScale(w, cap);
                    Assert.LessOrEqual(w * scale, cap + 1e-6f, $"cap {cap}, weight {w}");
                    if (w <= cap) Assert.AreEqual(1f, scale, 0f, $"cap {cap}, weight {w}: under the cap, untouched");
                }
            Assert.AreEqual(1f, WaterSurface.WashCapScale(0.2f, 0.6f), "the shipped sheet (0.2) is under the shipped cap");
        }

        [Test]
        public void TheShader_HandsTheAnatomyToTheWash_ThenCapsIt_BeforeTheLine()
        {
            string src = Src();
            int beach = IndexOf(src, "float washBeach = saturate(washPresence) * saturate(-depth / max(_WashAnatomyFade, 1e-3));");
            int barrelYield = IndexOf(src, "surfBarrel *= 1.0 - washBeach;");
            int lipYield = IndexOf(src, "surfLip    *= 1.0 - washBeach;");
            int under = IndexOf(src, "float3 washUnder = col.rgb;");
            int barrel = IndexOf(src,
                "float barrel = lerp(SurfBandValue(barrelV, washThin > 0.001 ? 0.5 : bay), barrelV, washThin) * strength;");
            int lip = IndexOf(src,
                "float lip = lerp(lipBanded, washLip, washThin)");
            int heaviest = IndexOf(src,
                "float washHeaviest = max(cover * _SurfColor.a, max(lip * _SurfLipColor.a, barrel * _SurfBarrelColor.a));");
            int cap = IndexOf(src, "col.rgb = lerp(washUnder, col.rgb, lerp(1.0, washCapScale, max(washBeach, shoreSurf)));");
            int washLine = IndexOf(src, "col.rgb = lerp(col.rgb, _FoamColor.rgb, washLine);");
            Assert.That(beach < barrelYield && barrelYield < lipYield && lipYield < under && under < barrel &&
                        barrel < lip && lip < heaviest && heaviest < cap && cap < washLine,
                "the anatomy yields before it is banded, the cap follows every surf layer, and the line is laid last");
            StringAssert.Contains("? saturate(_WashMaxCover) / max(washHeaviest, 1e-4) : 1.0;", src);
            StringAssert.Contains("* leftFade * washPresence * min(saturate(_WashLeftFoamOpacity), saturate(_WashMaxCover));", src,
                "the foam left on the sand wears the same cap");
        }

        [Test]
        public void TheFringe_YieldsToTheWash_ThroughTheEdgesShoreQuantity()
        {
            string src = Src();
            int supersede = IndexOf(src, "foamCoverage *= (1.0 - surfSupersede);");
            int yield = IndexOf(src, "foamCoverage *= 1.0 - washPresence * step(0.001, _WashLineOpacity);");
            int compose = IndexOf(src, "col.rgb = lerp(col.rgb, fringeFoam, foamCoverage * _FoamColor.a);");
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
            // The one foam sheet (2026-11-19) bands the whitewater through FoamSheetJoin off the wash, on the
            // same no-Bayer cell inside it.
            StringAssert.Contains("float surfBay = washThin > 0.001 ? 0.5 : bay;", src);
            StringAssert.Contains(
                "float surfBanded = FoamSheetJoin(SurfBandValue(surfCoverDrawn, surfBay), surfCoverDrawn * _FoamSheetCaps.w,", src);
            StringAssert.Contains(
                "float cover = lerp(surfBanded, washSheet,", src);
            // The wash joins the one foam sheet (11-19, "Yes, unify surf") on the same no-Bayer cell.
            StringAssert.Contains("washAge01, washTorn, surfSheetBay, _FoamSheetWash.x);", src);
            StringAssert.Contains("* lerp(1.0, saturate(_WashSheetOpacity), saturate(washOnSand));", src);
            int lip = IndexOf(src, "col.a   = max(col.a, lip * _SurfLipColor.a);");
            int washLine = IndexOf(src, "col.rgb = lerp(col.rgb, _FoamColor.rgb, washLine);");
            Assert.Greater(washLine, lip, "the leading line is laid over the surf, last");
        }

        // ---- v8: the shore surf's slab and the line at peak (the editor look of f07a839, 11-15) -------------------

        [Test]
        public void OnTheSand_EveryPixelOfOneShoreNormal_ReadsTheSurfAtTheSameWaterlinePoint()
        {
            // v7 projected from worldXY but by the depth read at worldXY + warp, so the read point scattered by the
            // warp's normal component and the bore's march, phase and run-up varied down one normal: the reach line's
            // test held a run-up that was not its neighbour's, and it shredded at peak. Projected from the warped point
            // by the tide depth read THERE, a planar beach puts every pixel of a normal on one waterline point.
            const float Water = 0.29f;
            var n = new Vector2(0.16f, 0.99f).normalized;
            var t = new Vector2(n.y, -n.x);
            var rng = new System.Random(1225);
            foreach (float slope in new[] { 0.15f, 0.3f, 0.6f })
            {
                const float c0 = 3f;                                  // the still line, metres along n
                float Elevation(Vector2 p) => Water + slope * (Vector2.Dot(p, n) - c0);
                for (int a = 0; a < 20; a++)
                {
                    Vector2 foot = t * (a * 0.37f);
                    float? first = null;
                    float oldMin = float.MaxValue, oldMax = float.MinValue;
                    for (int k = 0; k <= 40; k++)
                    {
                        Vector2 p = foot + n * (c0 + 0.3f * k / 40f);      // up the beach, past the still line
                        var warp = new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.6f;
                        float tideDepth = Water - Elevation(p + warp);
                        Vector2 e = WaterSurface.BeachSurfEvalPoint(p, warp, n, tideDepth, slope);
                        float along = Vector2.Dot(e, n);
                        Assert.AreEqual(c0, along, 1e-4f, $"slope {slope}: the read point lies on the still line");
                        first ??= along;
                        Assert.AreEqual(first.Value, along, 1e-4f, "one normal, one read point");
                        float old = Vector2.Dot(p + n * (tideDepth / slope), n);   // v7: origin worldXY, warped depth
                        oldMin = Mathf.Min(oldMin, old); oldMax = Mathf.Max(oldMax, old);
                    }
                    Assert.Greater(oldMax - oldMin, 0.05f, "premise: the v7 point scattered with the warp");
                }
            }
        }

        [Test]
        public void TheShoreSurf_IsOnlyBesideABeach_AndShowsWaterWhereTheOpenBandWasASlab()
        {
            const float Reach = 0.35f;
            Assert.AreEqual(0f, WaterSurface.ShoreSurfShare01(0.5f, Reach), "open water or a bar with water behind: open look");
            Assert.AreEqual(0f, WaterSurface.ShoreSurfShare01(0f, Reach));
            Assert.AreEqual(1f, WaterSurface.ShoreSurfShare01(-Reach, Reach), 1e-6f, "ground above the run-up: the shore look");
            Assert.AreEqual(1f, WaterSurface.ShoreSurfShare01(-3f, Reach), 1e-6f);
            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float v = WaterSurface.ShoreSurfShare01(0.2f - 0.6f * i / 100f, Reach);
                Assert.GreaterOrEqual(v, prev, "the hand-over only grows as the tap dries"); prev = v;
            }

            // The slab: at the crest (density = the crest boost, 1.6) the open band is 1 for EVERY churn, so no water
            // shows through it; the shore band, energy's lift at 0, is the churn thresholded: water between patches.
            const float Thr = 0.42f, Soft = 0.16f, Density = 1.6f;
            int openFull = 0, shoreClear = 0, shoreSolid = 0;
            for (int i = 0; i <= 100; i++)
            {
                float churn = i / 100f;
                if (WaterSurface.OpenSurfCover01(churn, Density, 1f, Thr, Soft) >= 1f - 1e-6f) openFull++;
                float s = WaterSurface.ShoreSurfCover01(churn, Density, 1f, 0f, Thr, Soft);
                if (s <= 1e-6f) shoreClear++;
                if (s >= 1f - 1e-6f) shoreSolid++;
                Assert.AreEqual(0f, WaterSurface.ShoreSurfCover01(churn, Density, 0f, 0f, Thr, Soft), "no break, no surf");
            }
            Assert.AreEqual(101, openFull, "premise: the open band at the crest is a slab");
            Assert.Greater(shoreClear, 20, "the shore band shows water through it");
            Assert.Greater(shoreSolid, 20, "and keeps broken patches of foam");
            // Lift 1 gives the open breaker's field back (gated inside the threshold rather than after it).
            Assert.AreEqual(1f, WaterSurface.ShoreSurfCover01(0f, Density, 1f, 1f, Thr, Soft), 1e-6f);
        }

        [Test]
        public void TheShader_DrawsTheShoreSurf_OnlyBesideABeach_InTheWashLanguage()
        {
            string src = Src();
            // v9: the share is read BEFORE the break gate, which reads the along-shore box beside a beach.
            int gate = IndexOf(src, "if (_WashSurfReach > 0.0 && dot(surfDir, surfDir) > 1e-6)");
            int tap = IndexOf(src, "float shoreTapDepth = _WaterLevel - SeabedElevation(surfEvalXY + surfDir * _WashSurfReach);");
            int share = IndexOf(src, "surfShore = saturate(-shoreTapDepth / max(surfBeachReach, 1e-3));");
            // The box is floored at 1e-3 so a tap on a dry plateau never dries a wet pixel (twin AlongShoreGateDepth).
            int box = IndexOf(src, "surfGateDepth = max(lerp(surfEvalDepth, boxDepth, surfShore), 1e-3);");
            int breaking = IndexOf(src, "surfBreaking = SurfBreaking01(surfGateDepth, surfFetchEnv);");
            int pocket = IndexOf(src, "surfCover = saturate(surfCover * lerp(1.0, max(_SurfPocketBoost, 1.0), pocket));");
            int fine = IndexOf(src, "max(_SurfNoiseScale, 1e-3) * max(_WashSurfDetail, 1.0),");
            int brk = IndexOf(src, "surfShoreBreak = smoothstep(thr - soft, thr + soft, churnShore);");
            int thin = IndexOf(src, "float washThin = max(saturate(washOnSand), shoreSurf);");
            int drawn = IndexOf(src, "float surfCoverDrawn = lerp(surfCover, surfCoverShore, shoreSurf);");
            int lip = IndexOf(src, "* strength * lerp(1.0, surfShoreBreak, shoreSurf);");
            Assert.That(gate < tap && tap < share && share < box && box < breaking && breaking < pocket
                        && pocket < fine && fine < brk && brk < thin && thin < drawn && drawn < lip);
            Assert.AreEqual(1, Regex.Matches(src, @"(?m)^ *surfShore = ").Count, "the share is set only inside its gate");
            Assert.AreEqual(1, Regex.Matches(src, @"\bsurfBreaking = SurfBreaking01\(").Count, "one break gate");
            StringAssert.Contains("float shoreSurf = surfShore * saturate(washPresence);", src);
            // The beach projection starts at the warped point and reads the tide depth there (twin BeachSurfEvalPoint),
            // down the ground's frame read over _WashGroundScale metres (v9), not the pixel's own slope.
            StringAssert.Contains(
                "surfEvalXY = worldXY + warp + groundDir * ((_WaterLevel - elevation) / max(groundSlope, 1e-3));", src);
            StringAssert.Contains("float3 groundFrame = GroundFrame(worldXY, _WashGroundScale);", src);
            // ...and one correcting step onto the still line, so a mound's pixels land where their neighbours read.
            StringAssert.Contains(
                "surfEvalXY += groundDir * ((_WaterLevel - SeabedElevation(surfEvalXY)) / max(groundSlope, 1e-3));", src);
        }

        // ---- v9: the slab, the seaward steps and the brown patches (the editor look of 7e16896, 11-15) ----------

        [Test]
        public void BesideABeach_TheGateDepth_RampsAcrossAPlateauStep_AndOutAtSeaItIsUnchanged()
        {
            // An 8-bit plateau shore: depth steps by one code (0.047 m) every 1.5 m along the shore. The gate read at
            // each pixel's own depth jumps at each step; the along-shore box over 1.5 m spreads it over the box.
            const float Code = 0.047f, Run = 1.5f, Half = 1.5f / 3f;
            float Depth(float s) => 0.4f + Code * Mathf.Floor(s / Run);
            float worstRaw = 0f, worstBox = 0f;
            for (int i = 1; i < 600; i++)
            {
                float s0 = (i - 1) * 0.01f, s1 = i * 0.01f;
                float r0 = Depth(s0), r1 = Depth(s1);
                float b0 = WaterSurface.AlongShoreGateDepth(r0, Depth(s0 + Half), Depth(s0 - Half), 1f);
                float b1 = WaterSurface.AlongShoreGateDepth(r1, Depth(s1 + Half), Depth(s1 - Half), 1f);
                worstRaw = Mathf.Max(worstRaw, Mathf.Abs(r1 - r0));
                worstBox = Mathf.Max(worstBox, Mathf.Abs(b1 - b0));
                Assert.AreEqual(r0, WaterSurface.AlongShoreGateDepth(r0, Depth(s0 + Half), Depth(s0 - Half), 0f), 1e-6f,
                    "out at sea the gate reads its own depth");
            }
            Assert.AreEqual(Code, worstRaw, 1e-4f, "premise: the raw gate jumps a whole code");
            Assert.LessOrEqual(worstBox, Code / 3f + 1e-4f, "the box splits each jump in three, a third of a plateau apart");
            // Wet water beside a plateau that stands above the still line stays wet (the lenses of the first v9 look).
            Assert.Greater(WaterSurface.AlongShoreGateDepth(0.02f, -0.03f, -0.03f, 1f), 0f, "a dry tap never dries a wet pixel");
        }

        [Test]
        public void OnAMound_TheCorrectingStep_PutsItsPixelsBackOnTheStillLine()
        {
            // A planar beach with a gravel mound (0.25 m high, 0.8 m across) standing on it 0.6 m up from the still line.
            // The first step projects by the pixel's own height, so the mound's pixels land past the still line by
            // their bump over the slope (the shards round the mounds); one Newton step on the ground brings them back.
            // (A mound standing ON the still line defeats one step - the step lands on the mound - which is why
            // a few small notches remain at the two mounds that sit at the waterline in the nmc2 frames.)
            const float Water = 0.25f, Slope = 0.3f, C0 = 3f;
            var n = new Vector2(0f, 1f);
            var mound = new Vector2(0f, C0 + 0.6f);
            float Elevation(Vector2 p) => Water + Slope * (Vector2.Dot(p, n) - C0)
                                        + 0.25f * Mathf.Exp(-(p - mound).sqrMagnitude / (0.4f * 0.4f));
            float TideDepth(Vector2 p) => Water - Elevation(p);
            float worstFirst = 0f, worstRefined = 0f;
            for (int k = 0; k <= 40; k++)
            {
                Vector2 p = mound + new Vector2(0.6f * (k - 20) / 20f, 0.1f);       // across the mound, just up the beach
                Vector2 first = WaterSurface.BeachSurfEvalPoint(p, Vector2.zero, n, TideDepth(p), Slope);
                Vector2 refined = WaterSurface.BeachSurfEvalPointRefined(p, Vector2.zero, n, TideDepth(p), Slope, TideDepth);
                worstFirst = Mathf.Max(worstFirst, Mathf.Abs(TideDepth(first)));
                worstRefined = Mathf.Max(worstRefined, Mathf.Abs(TideDepth(refined)));
            }
            Assert.Greater(worstFirst, 0.1f, "premise: the first step leaves the mound's pixels off the still line");
            Assert.Less(worstRefined, worstFirst * 0.25f, "the correcting step brings them back onto it");
            // On a planar beach the correcting step is 0.
            float Plane(Vector2 p) => Water - (Water + Slope * (Vector2.Dot(p, n) - C0));
            Vector2 q = new Vector2(1.3f, C0 + 0.2f);
            Assert.AreEqual(WaterSurface.BeachSurfEvalPoint(q, Vector2.zero, n, Plane(q), Slope).y,
                            WaterSurface.BeachSurfEvalPointRefined(q, Vector2.zero, n, Plane(q), Slope, Plane).y, 1e-5f);
        }

        [Test]
        public void BesideABeach_TheLipBreaksWithTheChurn_AndOutAtSeaItIsTheOpenLip()
        {
            const float Thr = 0.42f, Soft = 0.16f;
            int clear = 0;
            for (int i = 0; i <= 100; i++)
            {
                float brk = Mathf.Clamp01((i / 100f - (Thr - Soft)) / (2f * Soft));
                brk = brk * brk * (3f - 2f * brk);
                Assert.AreEqual(1f, WaterSurface.ShoreLip01(1f, brk, 0f), 1e-6f, "out at sea: the open lip");
                if (WaterSurface.ShoreLip01(1f, brk, 1f) <= 1e-6f) clear++;
            }
            Assert.Greater(clear, 20, "a full lip beside a beach shows water where the churn is low: no slab");
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

            // v9: and on the shore surf's zone, where the bore's deposited cells drew the band's seaward stair.
            int zone = IndexOf(src, "float washZone = max(saturate(washOnSand), surfShore * saturate(washPresence));");
            int call = IndexOf(src, "float wakeFoam = WakeFoamCoverage(worldXY, washZone > 0.001 ? 0.5 : sheetBay, hullCrestFoam, wakeFresh);");
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
