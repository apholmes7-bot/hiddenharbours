using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the 2026-09-13 SHORE-CORNER HAIRLINE — the owner's *"the foam at the shoreline
    /// has these fine lines … very fine line effect at corners when the waves pulses in and out"*, ruled a
    /// draft on 09-12 and traced by r2 to the WET EDGE rather than to any foam family.
    ///
    /// <para><b>The defect.</b> The drawn edge is <c>clip(depth + edgeSwash)</c>, and <c>edgeSwash</c> was
    /// <c>lerp(cosmeticSwash, boreRunUp, boreEdgeBlend)</c> with the blend read at THE FRAGMENT'S OWN DEPTH.
    /// The break gate falls 1 → 0 across the break band; on a sheltered shore <c>SolveBreakDepth</c> lands
    /// that whole band within CENTIMETRES of the waterline, far narrower than the metres of level the two
    /// arms differ by. Weighting with it therefore ran a swing of up to 2·<c>_SwashMaxEdgeShift</c> through a
    /// ~5 cm window, so <c>d(depth + edgeSwash)/d(depth)</c> went NEGATIVE and the clip stopped describing
    /// one edge: it drew the shallow band the bore arm still held, dropped the middle where the cosmetic arm
    /// had taken over and retreated, then drew again past it — a ribbon of water stranded a finger's width
    /// off the sheet. On <c>PLATE-shore-corner.png</c> — the r1 diagnostic plate, 1280×960 across a 9.0 m
    /// half-height frame, so <b>0.009375 m per pixel</b>; the r2 sweep re-shot that same camera at
    /// 3662×1600, a finer 0.00563 m/px, which is why the two records quote different scales — 405 of 480
    /// columns cut, the stranded piece 5 px (0.047 m) wide standing 33 px (0.309 m) clear.</para>
    ///
    /// <para><b>The law these tests pin.</b> <c>depth ↦ depth + DrawnEdgeShift(…)</c> must be strictly
    /// increasing, because that — and only that — is what makes the drawn water ONE PIECE with a single
    /// edge. It holds exactly when the blend and the run-up are SHORE quantities, constant along the shore
    /// normal; the fix asks the gate once, at <see cref="WaterSurface.DrawnEdgeReferenceDepthMeters"/>
    /// (= <see cref="BreakerMath.MinDepthMeters"/>), which is the same pin the surf block already uses when
    /// it projects a dry fragment back to the waterline. That is ADR 0040 rev 3's own *"yield on the bore,
    /// not on the gate"* — which weighting by the gate had exactly inverted.</para>
    ///
    /// <para><b>What is measured and what is a parameter.</b> CI has no GPU, so the break band
    /// (<c>bd</c>/<c>od</c>, solved per tick by <c>BreakerMath.ContourFor</c> and pushed as
    /// <c>Shader.SetGlobalVector</c> globals) and the bore's run-up are PARAMETERS here, fixed at the values
    /// measured off the r2 plate's own inversion corner — the same discipline
    /// <c>WaterShoreBandAndSwashTests</c> uses for the GPU halves it cannot evaluate. The gate itself is not
    /// re-implemented: it is built from the shipped <see cref="WaveFetch.SmoothstepEdge"/> exactly as
    /// <c>BreakerMath.Breaking01FromContour</c> builds it. Everything else — the swash, the cap, the
    /// composition — is the shipped arithmetic, called through its own twins.</para>
    ///
    /// <para><b>Both halves are here on purpose.</b> A one-piece law can pass vacuously, so the witness test
    /// re-composes the edge the way it SHIPPED and requires it to be cut in two at the plate's numbers. If
    /// the witness ever goes green, this file has stopped measuring the defect.</para>
    /// </summary>
    public class WaterDrawnEdgeOnePieceTests
    {
        // ---- the shipped dials (Water.mat; TheShippedEdgeKnobs_… below reads them back) --------------
        private const float SwashSpeed = 0.16f;
        private const float SwashAmplitude = 1f;
        private const float SwashWavelength = 1.2f;
        private const float AlongShoreVary = 0.35f;
        private const float EdgeShift = 0.6f;
        private const float MaxEdgeShift = 0.35f;
        private const float SurfStrength = 1f;
        private const float RunUpStrength = 1f;

        // A calm sea state — the regime the owner's "sometimes smooth" lives in. Choppiness(s) == s, so
        // this IS the sea-state axis. It gates the COSMETIC arm's amplitude only, and a calmer sea makes the
        // inversion harder to reach, not easier, so the witness below is not leaning on this number.
        private const float Chop = 0.12f;
        private const float SeaStateLo = 0.10f;
        private const float SeaStateHi = 0.60f;
        private const float CalmGate = 0.70f;

        /// <summary>The seabed slope the shader actually applies across the r2 corner's beach band,
        /// measured off the committed height map rather than guessed.
        ///
        /// <para>Reproducing <c>SeabedSlopeMag</c> — the central difference of
        /// <c>NineMileCreekSeabed_HeightTex.png</c> over ±<c>_ShoreSampleStep</c> 0.4 m, bilinear, elevation
        /// −6…+6 m over a 760×560 m rect — sampled at 0.05 m over the r2 frame (camera (34.70, 11.50),
        /// half-height 4.5 m, 4:3) and kept to the 3,246 samples within one <c>_SwashMaxEdgeShift</c> of the
        /// waterline: the raw slope runs <b>0.240 … 4.309 m/m</b>, median 0.663. The shader then applies
        /// <c>max(saturate(raw), _ShoreSlopeFloor)</c>, so what the swash is actually multiplied by spans
        /// <b>0.240 … 1.000</b>, and 17.5 % of the band sits at the 1.0 ceiling.</para>
        ///
        /// <para>All three are swept because the slope is what scales the cosmetic swash, and so decides how
        /// far the two arms of the blend can differ. Sweeping one reading — particularly a gentle one, which
        /// shrinks the cosmetic arm — would be picking the number that makes this fixture's own argument
        /// easy.</para></summary>
        private static readonly float[] ShoreSlopes = { 0.240f, 0.663f, 1.000f };

        // ---- the ramp a GPU-free guard can build: a straight beach through the waterline -------------
        private const float DepthMin = -0.40f;      // past the wash's reach on the dry side
        private const float DepthMax = 1.20f;       // past the deepest break band measured
        private const float DepthStep = 0.0005f;    // 0.5 mm — ~19x finer than a diagnostic-plate px (9.4 mm)
        private const float ClipEpsilon = 1e-4f;    // the shader's own clip(… + 1e-4)

        // The swash carries a half-frequency partial, so the composite beat is 2/speed = 12.5 s, not 6.25.
        private const float SwashPeriodSeconds = 2f / SwashSpeed;
        private const int PhaseSamples = 64;

        /// <summary>The along-shore desync noise is a GPU value-noise lookup; headless it is a parameter,
        /// sampled across its whole 0…1 range so no phase of the desync escapes the sweep.</summary>
        private static readonly float[] NoiseSamples = { 0.03f, 0.27f, 0.50f, 0.73f, 0.97f };

        private static readonly float ReferenceDepth = WaterSurface.DrawnEdgeReferenceDepthMeters;

        /// <summary>The wash's reach above the still-water line — the surf block's own dry-side predicate
        /// (<c>depth &gt; -surfBeachReach</c>), which the fixed edge blend rides along with.</summary>
        private static float BeachReach => Mathf.Clamp01(RunUpStrength) * Mathf.Clamp01(MaxEdgeShift);

        /// <summary>Where every ramp scan starts: one step INSIDE the wash's reach. Below the reach the two
        /// compositions are bit-identical — the blend is off in both — so that stretch is not this change's
        /// to prove, and the single step where the blend switches on is common to both arms. What covers it
        /// is <see cref="TheDrySide_IsBitForBitWhatShipped"/>, which walks the whole dry side.</summary>
        private static float ReachStart => -BeachReach + DepthStep;

        /// <summary>A break band plus the bore reach that rode it, as MEASURED at the r2 corner by inverting
        /// the plate's stranded-ribbon geometry. bd 0.010…0.070 m with od 0.075…0.180 m is
        /// <c>SolveBreakDepth</c>'s bracket-floor regime: a train that barely breaks — a sheltered creek.
        /// That is also why the owner saw it only *sometimes*: where nothing breaks at all the gate is 0,
        /// the bore arm never enters, and the edge is the plain monotone cosmetic swash.</summary>
        private readonly struct Band
        {
            public readonly float BreakDepth, OuterDepth, RunUp;

            public Band(float breakDepth, float outerDepth, float runUp)
            {
                BreakDepth = breakDepth;
                OuterDepth = outerDepth;
                RunUp = runUp;
            }

            public override string ToString() =>
                string.Format(CultureInfo.InvariantCulture, "bd={0:0.000} od={1:0.000} R={2:0.000}",
                              BreakDepth, OuterDepth, RunUp);
        }

        private static readonly Band[] InvertingBands =
        {
            new Band(0.010f, 0.075f, 0.030f),   // the inversion corner: the narrowest band measured
            new Band(0.020f, 0.120f, 0.037f),   // mid of the inverted bracket
            new Band(0.020f, 0.180f, 0.350f),   // the widest band, run-up at its cap
        };

        // ==== the pieces of the shipped arithmetic, called through their own twins ====================

        /// <summary>The shader's <c>SurfBreaking01</c> with the band supplied directly instead of read from
        /// the per-tick globals — built from the same <see cref="WaveFetch.SmoothstepEdge"/> and the same
        /// <see cref="WaveFetch.MinGateBand"/> floor as <c>BreakerMath.Breaking01FromContour</c>.</summary>
        private static float BreakGate(float depth, Band band)
        {
            if (depth <= 0f) return 0f;                  // dry ground breaks nothing
            if (band.BreakDepth <= 0f) return 0f;
            float outer = Mathf.Max(band.OuterDepth, band.BreakDepth + WaveFetch.MinGateBand);
            return 1f - WaveFetch.SmoothstepEdge(band.BreakDepth, outer, depth);  // shallower = more broken
        }

        /// <summary>The cosmetic arm of the drawn edge: the beach swash, gated by sea state, scaled by the
        /// shore slope and the edge dial, hard-capped — <c>WaterSurface.SwashOffset</c> then
        /// <c>WaterSurface.SwashEdgeShift</c>, exactly as the shader composes them.</summary>
        private static float CosmeticShift(float depth, float time, float noise01, float slope)
        {
            float swash = WaterSurface.SwashOffset(time, SwashSpeed, SwashAmplitude, Mathf.Max(depth, 0f),
                                                   SwashWavelength, AlongShoreVary, noise01, true)
                          * WaterSurface.SwashSeaStateGate(Chop, SeaStateLo, SeaStateHi, CalmGate);
            return WaterSurface.SwashEdgeShift(swash * slope, EdgeShift, MaxEdgeShift);
        }

        /// <summary>THE FIX, spelled out as the shader now spells it: the edge asks the break gate ONCE, at
        /// the reference depth, so the blend is a shore quantity — constant along the shore normal. The two
        /// composers below are written out separately on purpose; sharing a "gate depth" parameter between
        /// them would make their agreement on dry ground true by construction instead of measured.</summary>
        private static float FixedShift(float depth, float time, float noise01, Band band, float slope)
        {
            if (depth <= -BeachReach)                       // past the wash's reach: the surf block never ran
                return WaterSurface.DrawnEdgeShift(CosmeticShift(depth, time, noise01, slope), 0f, 0f,
                                                   MaxEdgeShift);

            float blend = WaterSurface.BoreEdgeBlend(BreakGate(ReferenceDepth, band),
                                                     SurfStrength, RunUpStrength);
            return WaterSurface.DrawnEdgeShift(CosmeticShift(depth, time, noise01, slope), band.RunUp, blend,
                                               MaxEdgeShift);
        }

        /// <summary>WHAT SHIPPED, spelled out as the shader spelled it: the edge asks the gate at the
        /// FRAGMENT'S depth. Faithful, not a strawman — the dry side keeps the pin the surf block already
        /// applied there (<c>surfEvalDepth = 0.02</c>, projecting the fragment back down the shore slope to
        /// its own waterline point), so the two arms can differ only in the wet band.</summary>
        private static float ShippedShift(float depth, float time, float noise01, Band band, float slope)
        {
            if (depth <= -BeachReach)
                return WaterSurface.DrawnEdgeShift(CosmeticShift(depth, time, noise01, slope), 0f, 0f,
                                                   MaxEdgeShift);

            float evalDepth = depth;
            if (depth <= 0f)
                evalDepth = BreakerMath.MinDepthMeters;     // the surf block's own dry-side waterline pin
            float blend = WaterSurface.BoreEdgeBlend(BreakGate(evalDepth, band), SurfStrength, RunUpStrength);
            return WaterSurface.DrawnEdgeShift(CosmeticShift(depth, time, noise01, slope), band.RunUp, blend,
                                               MaxEdgeShift);
        }

        // ==== walking the ramp =========================================================================

        /// <summary>What a column of the drawn sea looks like along the shore normal.</summary>
        private struct RampScan
        {
            /// <summary>Contiguous runs of DRAWN fragments. The law says exactly one.</summary>
            public int Pieces;

            /// <summary>Widest run that CLOSED before the ramp ended — i.e. a ribbon stranded off the sheet.
            /// The sea body is the run still open at the deep end and is never counted here.</summary>
            public float WidestStrandedMeters;

            /// <summary>Widest bare stretch between two drawn runs: the hairline's dark side.</summary>
            public float WidestGapMeters;

            /// <summary>Smallest step of <c>depth ↦ depth + shift</c> across the reach, in metres. The law is
            /// that this stays positive; the defect is exactly that it did not.</summary>
            public float MinStepMeters;
        }

        private static RampScan Scan(Func<float, float> shiftAt)
        {
            int n = Mathf.RoundToInt((DepthMax - ReachStart) / DepthStep) + 1;
            var scan = new RampScan { MinStepMeters = float.MaxValue };
            bool prevDrawn = false, haveClosedRun = false;
            float runStart = 0f, prevRunEnd = 0f, prevDepth = 0f, prevF = 0f;

            for (int i = 0; i < n; i++)
            {
                float depth = ReachStart + i * DepthStep;
                float f = depth + shiftAt(depth);
                bool drawn = f + ClipEpsilon >= 0f;

                if (i > 0)
                    scan.MinStepMeters = Mathf.Min(scan.MinStepMeters, f - prevF);

                if (drawn && !prevDrawn)
                {
                    runStart = depth;
                    if (haveClosedRun)
                        scan.WidestGapMeters = Mathf.Max(scan.WidestGapMeters, depth - prevRunEnd);
                }
                else if (!drawn && prevDrawn)
                {
                    scan.Pieces++;
                    scan.WidestStrandedMeters = Mathf.Max(scan.WidestStrandedMeters, prevDepth - runStart);
                    prevRunEnd = prevDepth;
                    haveClosedRun = true;
                }

                prevDrawn = drawn;
                prevDepth = depth;
                prevF = f;
            }

            if (prevDrawn) scan.Pieces++;               // the sea body, still open at the deep end
            return scan;
        }

        private static float PhaseAt(int i) => SwashPeriodSeconds * i / PhaseSamples;

        // ==== (1) the law ==============================================================================

        [Test]
        public void TheDrawnEdge_IsOnePiece_AtEveryPhaseOfEveryInvertingBand()
        {
            foreach (float slope in ShoreSlopes)
                foreach (Band band in InvertingBands)
                    for (int p = 0; p < PhaseSamples; p++)
                        foreach (float noise in NoiseSamples)
                        {
                            float t = PhaseAt(p);
                            Band b = band;
                            float n = noise;
                            float sl = slope;
                            RampScan scan = Scan(d => FixedShift(d, t, n, b, sl));
                            string where = $"{band} slope={slope:0.00} t={t:0.000}s noise={noise:0.00}";

                            Assert.AreEqual(1, scan.Pieces,
                                $"The drawn sea is in {scan.Pieces} pieces at {where} — a single clip " +
                                "against a single edge must leave exactly one. A second piece IS the " +
                                "shore-corner hairline.");
                            Assert.AreEqual(0f, scan.WidestStrandedMeters, 1e-6f,
                                $"A ribbon {scan.WidestStrandedMeters:0.000} m wide is stranded off the " +
                                $"sheet at {where}.");
                            Assert.Greater(scan.MinStepMeters, 0f,
                                $"depth -> depth + DrawnEdgeShift stopped rising at {where} (worst step " +
                                $"{scan.MinStepMeters:0.000000} m). Strictly increasing IS the one-piece " +
                                "law: it holds while the blend and the run-up are shore quantities, and " +
                                "fails the moment either is read at the fragment's own depth.");
                        }
        }

        // ==== (2) the witness: the law is not vacuous ==================================================

        [Test]
        public void TheShippedComposition_CutTheDrawnSeaInTwo_AtThePlatesOwnNumbers()
        {
            // Every band is swept BEFORE anything is asserted, so a failure prints the whole table. One
            // band's numbers alone cannot tell you whether the composition moved or only that band did.
            int rows = InvertingBands.Length * ShoreSlopes.Length;
            var pieces = new int[rows];
            var stranded = new float[rows];
            var gaps = new float[rows];
            var steps = new float[rows];
            var label = new string[rows];
            var table = new StringBuilder();

            for (int i = 0; i < rows; i++)
            {
                Band band = InvertingBands[i % InvertingBands.Length];
                float slope = ShoreSlopes[i / InvertingBands.Length];
                label[i] = $"{band} slope={slope:0.00}";
                pieces[i] = 1;
                steps[i] = float.MaxValue;

                for (int p = 0; p < PhaseSamples; p++)
                    foreach (float noise in NoiseSamples)
                    {
                        float t = PhaseAt(p);
                        Band b = band;
                        float n = noise;
                        float sl = slope;
                        RampScan scan = Scan(d => ShippedShift(d, t, n, b, sl));
                        pieces[i] = Mathf.Max(pieces[i], scan.Pieces);
                        stranded[i] = Mathf.Max(stranded[i], scan.WidestStrandedMeters);
                        gaps[i] = Mathf.Max(gaps[i], scan.WidestGapMeters);
                        steps[i] = Mathf.Min(steps[i], scan.MinStepMeters);
                    }

                table.AppendLine($"  {label[i]}: worst {pieces[i]} pieces, stranded {stranded[i]:0.000} m, " +
                                 $"gap {gaps[i]:0.000} m, worst step {steps[i]:0.0000} m");
            }

            string what = Environment.NewLine + table;

            // What EVERY row must show is the inversion itself: depth -> depth + edgeSwash going backwards.
            // That is the mechanism, and the table above has it at all nine rows — every band, every slope,
            // down to -0.0001 m at the gentlest measured beach.
            for (int i = 0; i < rows; i++)
                Assert.Less(steps[i], 0f,
                    $"depth + edgeSwash never went backwards at {label[i]} — that inversion IS the " +
                    $"mechanism, so if it is gone this fixture no longer reproduces the defect.{what}");

            // Whether the inversion also DISCONNECTS the sheet depends on how steeply the gate falls
            // against the ramp, and that scales with the beach. As measured it is five rows of nine: all
            // three bands at the 1.00 saturation ceiling, two of three at the median 0.663, and NONE at the
            // gentlest measured 0.240, where every band inverts without ever parting. That gradient is the
            // owner's "sometimes" in numbers — the corner is where this shore steepens. The claim asserted
            // is therefore the one the table supports: the shipped composition CAN cut, which is what keeps
            // the law above from passing vacuously. Asking EVERY row to cut would be asking this ramp for a
            // number it did not produce.
            Assert.GreaterOrEqual(Mathf.Max(pieces), 2,
                "No swept row cuts the drawn sea any more. This witness is the only thing keeping the " +
                "one-piece law above from passing vacuously: if the composition has moved, re-derive both " +
                $"halves rather than deleting this one.{what}");
            Assert.Greater(Mathf.Max(stranded), 0.02f,
                "The widest stranded ribbon is too narrow to be the defect the owner reported — the plate's " +
                $"was 0.047 m, five pixels at the diagnostic plate's 0.009375 m/px.{what}");

            // The plate's own bare stretch was 0.309 m (33 px). This ramp does not reach that and is not
            // asked to: the plate's gap is measured ACROSS a corner, where two shores' wash reaches cross and
            // the bare wedge between them is a 2-D quantity. What a 1-D ramp can show is that the separation
            // exists at all, and is wider than a player could mistake for a seam — two plate pixels.
            Assert.Greater(Mathf.Max(gaps), 0.02f,
                "No swept row leaves bare ground between the ribbon and the sheet. Two plate pixels " +
                $"(0.019 m at the diagnostic plate) is the floor for a stretch a player could read as " +
                $"sand.{what}");
        }

        // ==== (3) the reference depth, and the shader that must still be asking at it ==================

        [Test]
        public void TheReferenceDepth_IsBreakerMathsOwnWaterlinePin()
        {
            Assert.AreEqual(BreakerMath.MinDepthMeters, WaterSurface.DrawnEdgeReferenceDepthMeters, 1e-7f,
                "The drawn edge's reference depth is not a tunable — it is the SAME waterline pin the surf " +
                "block uses when it projects a dry fragment back to the shore. If one moves, both move.");
        }

        [Test]
        public void TheShader_AsksTheEdgeGateAtTheReferenceDepth_AndLeavesTheFoamsGateAlone()
        {
            string src = ShaderSource();

            StringAssert.Contains("#define SURF_EDGE_REF_DEPTH 0.02", src,
                "The shader no longer pins the drawn edge's reference depth — see BreakerMath.MinDepthMeters.");
            StringAssert.Contains("SurfBreaking01(SURF_EDGE_REF_DEPTH", src,
                "The wet edge is no longer asking the break gate at the reference depth. Asking it at the " +
                "fragment's own depth is the 2026-09-13 shore-corner hairline.");
            StringAssert.Contains("surfEvalDepth = 0.02;", src,
                "The surf block's dry-side pin is gone — that pin is the precedent the edge's reference " +
                "depth descends from, and what makes the dry side bit-identical across this fix.");

            int reachGates = Regex.Matches(src, Regex.Escape("depth > -surfBeachReach")).Count;
            Assert.AreEqual(2, reachGates,
                $"Expected the wash's reach predicate twice (the surf block, and the edge blend that rides " +
                $"along with it) — found {reachGates}. Without the second the edge blend outlives the block " +
                "that feeds it, and the dry side stops being bit-identical.");

            // The FENCE: foam families A/B/C are not this change's business. The foam fringe keeps reading
            // the fragment-depth blend it has always read.
            StringAssert.Contains("surfRunUpM, boreEdgeBlend)", src,
                "The foam fringe no longer reads boreEdgeBlend. The wet-edge fix deliberately left the foam's " +
                "own gate alone; moving it is a different charter.");
        }

        // ==== (4) the dry side did not move ============================================================

        [Test]
        public void TheDrySide_IsBitForBitWhatShipped()
        {
            // The walk starts at DepthMin, well past the wash's reach, so it crosses the one step the ramp
            // scans skip: the reach boundary, where the blend switches on. Both arms switch there together
            // and by the same amount — which is why that step is not this change's to answer for.
            int dryCount = Mathf.RoundToInt(-DepthMin / DepthStep) + 1;

            foreach (float slope in ShoreSlopes)
                foreach (Band band in InvertingBands)
                    for (int p = 0; p < PhaseSamples; p += 4)
                        foreach (float noise in NoiseSamples)
                        {
                            float t = PhaseAt(p);
                            for (int i = 0; i < dryCount; i++)
                            {
                                float depth = DepthMin + i * DepthStep;
                                float shipped = ShippedShift(depth, t, noise, band, slope);
                                float repaired = FixedShift(depth, t, noise, band, slope);
                                if (shipped != repaired)
                                    Assert.Fail(
                                        $"The drawn edge moved on DRY ground at depth {depth:0.0000} m, " +
                                        $"{band}, slope={slope:0.00}, t={t:0.000}s: {shipped:R} -> " +
                                        $"{repaired:R}. ADR 0040 rev 3's run-up and the drain between " +
                                        "crests are NOT this fix's business; the change is confined to " +
                                        "refusing to cut the wet band out from under them.");
                            }
                        }
        }

        // ==== (5) the cap is still a hard bound ========================================================

        [Test]
        public void TheComposedShift_IsHardBounded_ForEveryInput()
        {
            float[] shifts = { -1e6f, -0.9f, -0.35f, -0.01f, 0f, 0.01f, 0.35f, 0.9f, 1e6f };
            float[] runUps = { -1e6f, -0.4f, 0f, 0.03f, 0.35f, 5f, 1e6f };
            float[] blends = { -3f, 0f, 0.25f, 0.936f, 1f, 4f };
            float[] caps = { -1f, 0f, 0.15f, 0.35f, 1f, 7f };

            foreach (float cap in caps)
            {
                float bound = Mathf.Clamp01(cap);
                foreach (float shift in shifts)
                    foreach (float runUp in runUps)
                        foreach (float blend in blends)
                        {
                            float composed = WaterSurface.DrawnEdgeShift(shift, runUp, blend, cap);
                            Assert.LessOrEqual(Mathf.Abs(composed), bound + 1e-6f,
                                $"DrawnEdgeShift({shift}, {runUp}, {blend}, {cap}) = {composed} escaped the " +
                                "SEE-not-FEEL cap. The drawn edge may diverge from the gameplay waterline " +
                                "only by _SwashMaxEdgeShift — that bound is what keeps the divergence a look " +
                                "and not a lie (P1 integrity, CLAUDE.md rule 5).");
                        }
            }
        }

        [Test]
        public void TheBoreEdgeBlend_IsAWeight_NeverAScale()
        {
            float[] values = { -3f, 0f, 0.5f, 1f, 9f };

            foreach (float gate in values)
                foreach (float surf in values)
                    foreach (float runUp in values)
                    {
                        float blend = WaterSurface.BoreEdgeBlend(gate, surf, runUp);
                        Assert.GreaterOrEqual(blend, 0f, "BoreEdgeBlend went negative — it is a lerp weight.");
                        Assert.LessOrEqual(blend, 1f, "BoreEdgeBlend passed 1 — it is a lerp weight.");
                    }

            // With either look dial down there is no bore arm at all: the edge is the cosmetic swash, exactly
            // as it draws on a sea that breaks nowhere — which is why the owner saw the line only SOMETIMES.
            Assert.AreEqual(0f, WaterSurface.BoreEdgeBlend(1f, 0f, 1f), 1e-7f);
            Assert.AreEqual(0f, WaterSurface.BoreEdgeBlend(1f, 1f, 0f), 1e-7f);
            Assert.AreEqual(0f, WaterSurface.BoreEdgeBlend(0f, 1f, 1f), 1e-7f);
            Assert.AreEqual(0.12f, WaterSurface.DrawnEdgeShift(0.12f, 0.3f, 0f, MaxEdgeShift), 1e-7f,
                "At blend 0 the drawn edge must be the cosmetic swash, untouched.");
            Assert.AreEqual(0.3f, WaterSurface.DrawnEdgeShift(0.12f, 0.3f, 1f, MaxEdgeShift), 1e-7f,
                "At blend 1 the drawn edge must be the bore's run-up, capped.");
        }

        // ==== (6) don't test a fiction: the dials these sweeps assume are the ones that draw ===========

        [Test]
        public void TheShippedEdgeKnobs_MatchWhatTheseTestsAssume()
        {
            Assert.AreEqual(MaxEdgeShift, ShippedFloat("_SwashMaxEdgeShift"), 1e-4f,
                "_SwashMaxEdgeShift moved — it is BOTH the SEE-not-FEEL cap and the wash's reach, so the " +
                "reach-boundary argument above must be re-derived at the new value.");
            Assert.AreEqual(EdgeShift, ShippedFloat("_SwashEdgeShift"), 1e-4f);
            Assert.AreEqual(SwashAmplitude, ShippedFloat("_SwashAmplitude"), 1e-4f);
            Assert.AreEqual(SwashWavelength, ShippedFloat("_SwashWavelength"), 1e-4f);
            Assert.AreEqual(SurfStrength, ShippedFloat("_SurfStrength"), 1e-4f,
                "_SurfStrength moved — at the shipped 1 the bore arm is at full authority, which is the worst " +
                "case for the one-piece law and the reason these sweeps use it.");
            Assert.AreEqual(RunUpStrength, ShippedFloat("_SurfRunUpStrength"), 1e-4f);
        }

        // ==== helpers ==================================================================================

        private const string LiveWaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string WaterShaderPath =
            "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        /// <summary>Read a float straight out of the material's serialized YAML — the value that SHIPS, not
        /// whatever the shader would fall back to. Fails loudly if the key is absent, which is the point.
        /// </summary>
        private static float ShippedFloat(string key)
        {
            Assert.IsTrue(File.Exists(LiveWaterMatPath), $"No material at '{LiveWaterMatPath}'.");
            Match m = Regex.Match(File.ReadAllText(LiveWaterMatPath),
                                  $@"-\s{Regex.Escape(key)}:\s*(-?[\d.eE+]+)");
            Assert.IsTrue(m.Success, $"'{LiveWaterMatPath}' does not serialize {key}.");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static string ShaderSource()
        {
            Assert.IsTrue(File.Exists(WaterShaderPath),
                          $"No shader at '{WaterShaderPath}'.");
            return File.ReadAllText(WaterShaderPath);
        }
    }
}
