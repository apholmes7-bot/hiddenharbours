using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless twin tests for the ADR 0027 #6 ADVECTED FOAM BUFFER — <see cref="FoamBuffer"/>, the
    /// C# mirror of <c>HiddenHarboursFoamBufferAdvect.shader</c> (change one, change BOTH in the same
    /// PR; the two tripwires at the bottom read the shader source so the seam cannot drift quietly).
    /// Contracts pinned:
    ///
    /// <list type="bullet">
    /// <item><b>THE CELL LAW</b> — the window origin is anchored to a WORLD cell lattice, so it does
    /// not move at all for a sub-cell pan and moves by whole cells otherwise;</item>
    /// <item><b>and the camera-relative version DOES crawl</b>, MEASURED as a camera-offset-dependent
    /// cell delta rather than asserted as an adjective — that is what makes the world snap a
    /// demonstrated fix instead of a claim (the <c>WaterReflectionWarpTests</c> discipline);</item>
    /// <item><b>a mark stays on its patch of water</b> across a pan and a drift — the end-to-end
    /// statement of the law, and the test that catches a sign flip in the scroll;</item>
    /// <item><b>decay is exponential and composes</b>, so a trail's lifetime does not depend on frame
    /// rate — and with the half-life sabotaged the trail measurably never dies;</item>
    /// <item><b>the injection shaping is super-linear</b>, which is the owner's "a slap churns more
    /// than a gentle rise-and-fall" made numeric;</item>
    /// <item><b>a hull with no inertia never churns</b> — the invariant that shows the bob channel is
    /// modelling relative motion and not just re-reading the wave height.</item>
    /// </list>
    ///
    /// <para>All pure maths: no graphics device, no scene, nothing to skip on CI.</para>
    /// </summary>
    public class FoamBufferTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursFoamBufferAdvect.shader";

        static string ReadShaderSource()
        {
            string path = Path.Combine(Application.dataPath, "..", ShaderPath);
            Assert.IsTrue(File.Exists(path), "Foam advect shader not found at " + ShaderPath);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// The shader source with comments removed — for guards that must read what the pass DOES,
        /// not what it says about itself.
        ///
        /// <para>⚠️ Learned the hard way: the negative guard below scanned raw source and was tripped
        /// by the shader's OWN comment explaining why it avoids <c>_PixelsPerUnit</c>. A guard that
        /// forbids naming the hazard forbids documenting it, which is precisely backwards — the
        /// comment is the thing that stops the next person reaching for that property.</para>
        /// </summary>
        static string StripComments(string source)
        {
            string withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(withoutBlocks, @"//[^\n]*", " ");
        }

        // ================= THE CELL LAW =========================================================

        [Test]
        public void WorldCellOrigin_DoesNotMoveAtAll_UnderASubCellPan()
        {
            const float extent = 96f;
            float cell = FoamBuffer.CellSize;

            // Start the camera at a position that is NOT already on the lattice, so the test cannot
            // pass by the accident of a tidy starting value.
            var start = new Vector2(13.377f, -4.911f);
            Vector2 reference = FoamBuffer.WorldCellOrigin(start, extent);

            // Sweep a full cell in 32 sub-cell steps. Every one of them must land on the same origin
            // until the sweep crosses a cell boundary.
            int moved = 0;
            for (int i = 1; i < 32; i++)
            {
                Vector2 origin = FoamBuffer.WorldCellOrigin(
                    start + new Vector2(cell * i / 32f, 0f), extent);
                if (origin != reference) moved++;
            }

            Assert.AreEqual(0, moved,
                "The foam window origin moved during a SUB-CELL camera pan. That is the crawl: every " +
                "foam cell would slide under the sea on every frame the camera moves, and the wake " +
                "would read as a screen filter instead of a mark left on the water (ADR 0027 #6).");
        }

        [Test]
        public void WorldCellOrigin_MovesOnlyInWholeCells()
        {
            const float extent = 96f;
            var start = new Vector2(13.377f, -4.911f);
            Vector2 previous = FoamBuffer.WorldCellOrigin(start, extent);

            for (int i = 1; i <= 400; i++)
            {
                // An irrational-ish step so the pan never happens to be a whole number of cells.
                Vector2 camera = start + new Vector2(0.0713f * i, -0.0411f * i);
                Vector2 origin = FoamBuffer.WorldCellOrigin(camera, extent);

                Vector2 deltaCells = (origin - previous) * FoamBuffer.CellsPerUnit;
                Assert.AreEqual(Mathf.Round(deltaCells.x), deltaCells.x, 1e-3f,
                    "The window origin moved a FRACTION of a cell in x. A fractional scroll has to be " +
                    "resampled, and resampling the buffer into itself every frame is a blur filter — " +
                    "the wake would smear away within seconds.");
                Assert.AreEqual(Mathf.Round(deltaCells.y), deltaCells.y, 1e-3f,
                    "The window origin moved a FRACTION of a cell in y (see the x message).");
                previous = origin;
            }
        }

        [Test]
        public void SABOTAGE_DropTheWorldCellSnap_AndTheWholeTRAILCrawls()
        {
            // THE measurement this item turns on, stated as precisely as it can be.
            //
            // Take one fixed patch of sea and ask WHERE IN ITS CELL it sits — the fractional part of
            // its buffer coordinate. That fraction is what decides whether an INTEGER texel scroll can
            // carry the mark truthfully:
            //
            //   • World-anchored: the origin is always on the cell lattice, so the fraction NEVER
            //     changes. Every window move is a whole number of cells, an integer scroll carries the
            //     mark exactly, and the foam stays on its water. Zero crawl by construction.
            //   • Camera-relative: the origin slides with the camera, so the fraction sweeps
            //     continuously — the cell boundary passes across the mark. No integer scroll can
            //     express that, so the content has to be resampled and EVERY foam cell slides under
            //     the sea on every frame the camera moves. That is the crawl, and it is the one
            //     artefact that would make this read as a screen filter rather than a wake.
            const float extent = 96f;
            float cell = FoamBuffer.CellSize;
            var water = new Vector2(20.25f, 7.5f);
            var start = new Vector2(13.377f, -4.911f);

            static float FractionInCell(Vector2 worldXY, Vector2 origin)
            {
                float coordinate = (worldXY.x - origin.x) * FoamBuffer.CellsPerUnit;
                return coordinate - Mathf.Floor(coordinate);
            }

            float worldReference = FractionInCell(water, FoamBuffer.WorldCellOrigin(start, extent));
            float cameraReference = FractionInCell(water, FoamBuffer.CameraRelativeOrigin(start, extent));

            float worstWorld = 0f, worstCamera = 0f;
            const int steps = 64;
            for (int i = 1; i <= steps; i++)
            {
                // A pan of LESS than one whole cell, in sub-cell steps.
                Vector2 camera = start + new Vector2(cell * i / (steps + 1f), 0f);

                worstWorld = Mathf.Max(worstWorld, Mathf.Abs(
                    FractionInCell(water, FoamBuffer.WorldCellOrigin(camera, extent)) - worldReference));
                worstCamera = Mathf.Max(worstCamera, Mathf.Abs(
                    FractionInCell(water, FoamBuffer.CameraRelativeOrigin(camera, extent)) - cameraReference));
            }

            Assert.AreEqual(0f, worstWorld, 1e-5f,
                $"THE SHIPPED LAW CRAWLS. A fixed patch of sea moved {worstWorld:F4} of a cell within " +
                "its own cell during a sub-cell camera pan, so no integer scroll can carry its foam " +
                "truthfully and the whole trail slides under every pan (ADR 0027 #6's cell law).");

            // The sabotage, measured — this is the number the world snap buys.
            Assert.Greater(worstCamera, 0.5f,
                "The camera-relative origin did NOT crawl, so this test can no longer prove the world " +
                "snap is doing anything. Either the wrong answer stopped being wrong (check " +
                "FoamBuffer.CameraRelativeOrigin) or the measurement broke.");
            TestContext.WriteLine(
                $"MEASURED CRAWL — across a camera pan of {cell * steps / (steps + 1f):F4} m (less than " +
                $"ONE {cell:F3} m cell): camera-relative addressing slides a fixed patch of sea " +
                $"{worstCamera:F4} of a cell inside its own cell — i.e. the cell boundary sweeps clean " +
                $"across the mark and the trail crawls. World-anchored addressing slides {worstWorld:F4}.");
        }

        [Test]
        public void AMark_StaysOnItsPatchOfWater_AcrossAPanAndADrift()
        {
            // The end-to-end statement of the cell law, and the test that catches a SIGN FLIP in the
            // scroll — which would slide the wake the wrong way under every pan.
            const float extent = 96f;
            var water = new Vector2(20.25f, 7.5f);

            var cameraBefore = new Vector2(13.377f, -4.911f);
            var cameraAfter = new Vector2(15.902f, -3.334f);   // a real pan, several cells

            Vector2 originBefore = FoamBuffer.WorldCellOrigin(cameraBefore, extent);
            Vector2 originAfter = FoamBuffer.WorldCellOrigin(cameraAfter, extent);

            // Where the mark WAS stored, and where it must now be read from.
            Vector2Int cellBefore = FoamBuffer.SampleCell(water, originBefore);
            Vector2Int cellAfter = FoamBuffer.SampleCell(water, originAfter);

            // No drift this frame, so the mark stays on its water.
            Vector2Int offset = FoamBuffer.SourceOffsetCells(originBefore, originAfter, Vector2Int.zero);

            // The advect pass computes dst[i] = prev[i + offset]. For the mark to survive the pan,
            // reading at its NEW cell must land on its OLD cell.
            Assert.AreEqual(cellBefore, cellAfter + offset,
                "The scroll offset does not carry a mark from its old cell to its new one, so the wake " +
                "slides across the sea whenever the camera moves. A negated offset is the likely cause " +
                "— see FoamBuffer.SourceOffsetCells.");
        }

        [Test]
        public void AMark_TravelsWithTheDrift_ByExactlyTheDriftedCells()
        {
            // The camera holds station; the wind carries the foam. The mark must move downwind by
            // exactly the whole cells the drift earned — no more (it would outrun the wind), no less
            // (the trail would sit glued where it was laid).
            const float extent = 96f;
            var camera = new Vector2(13.377f, -4.911f);
            Vector2 origin = FoamBuffer.WorldCellOrigin(camera, extent);

            var driftCells = new Vector2Int(3, -2);
            Vector2Int offset = FoamBuffer.SourceOffsetCells(origin, origin, driftCells);

            Assert.AreEqual(new Vector2Int(-3, 2), offset,
                "The drift scroll is not carrying content downwind by the cells it earned. Content " +
                "moves by MINUS the source offset, so the offset must be the negation of the drift.");
        }

        [Test]
        public void AdvectCells_BanksTheSubCellRemainder_SoNoDriftIsLost()
        {
            // A boat's wake drifts at a fraction of a cell per frame. Dropping that remainder would
            // make the foam travel measurably slower than the wind that is supposed to be carrying it.
            var residual = Vector2.zero;
            var perFrame = new Vector2(0.031f, -0.017f);   // well under one cell (0.125 m)
            const int frames = 600;

            var total = Vector2Int.zero;
            for (int i = 0; i < frames; i++)
                total += FoamBuffer.AdvectCells(ref residual, perFrame);

            Vector2 travelled = new Vector2(total.x, total.y) * FoamBuffer.CellSize;
            Vector2 expected = perFrame * frames;

            // Everything but the final banked remainder must have been spent, in both directions.
            Assert.AreEqual(expected.x, travelled.x, FoamBuffer.CellSize + 1e-4f,
                "Sub-cell drift is being LOST: after 600 frames the foam has travelled measurably " +
                "less than the wind carried it. The remainder must be banked, not discarded.");
            Assert.AreEqual(expected.y, travelled.y, FoamBuffer.CellSize + 1e-4f,
                "Sub-cell drift is being lost in the NEGATIVE direction (see the x message) — the " +
                "classic truncate-instead-of-floor asymmetry.");
        }

        [Test]
        public void Resolution_IsDerivedFromTheExtent_SoOneTexelIsExactlyOneWorldCell()
        {
            foreach (float extent in new[] { 32f, 48f, 96f, 128f })
            {
                int res = FoamBuffer.ResolutionForExtent(extent);
                Assert.AreEqual(extent * FoamBuffer.CellsPerUnit, res, 1e-3f,
                    $"At a {extent} m window the buffer is {res} texels, so one texel is not one " +
                    "world cell. The whole cell law rests on that identity.");
            }
        }

        // ================= DECAY ===============================================================

        [Test]
        public void DecayFactor_HalvesAtTheHalfLife_AndComposes()
        {
            const float halfLife = 6f;
            Assert.AreEqual(0.5f, FoamBuffer.DecayFactor(halfLife, halfLife), 1e-5f,
                "A half-life that does not halve is not a half-life.");

            // Frame-rate independence: 60 small steps must decay exactly as far as one big one, or
            // the trail's visible lifetime changes with the frame rate.
            float coarse = FoamBuffer.Decay(1f, halfLife, 1f);
            float fine = 1f;
            for (int i = 0; i < 60; i++) fine = FoamBuffer.Decay(fine, halfLife, 1f / 60f);
            Assert.AreEqual(coarse, fine, 1e-5f,
                "Decay does not compose, so a wake outlives itself at high frame rates and dies " +
                "early at low ones.");
        }

        [Test]
        public void DecayFactor_DegenerateInputs_FailSafe()
        {
            Assert.AreEqual(1f, FoamBuffer.DecayFactor(6f, 0f), 1e-6f, "No time passed, no decay.");
            Assert.AreEqual(0f, FoamBuffer.DecayFactor(0f, 0.016f), 1e-6f,
                "A zero half-life must kill the foam INSTANTLY, not preserve it forever — the safe " +
                "end of a degenerate tunable is 'the effect vanishes', never 'the effect is stuck on'.");
        }

        [Test]
        public void SABOTAGE_BreakTheHalfLife_AndTheTrailNeverDIES()
        {
            // The measured sabotage the handoff asks for: with decay removed, buffered foam is still
            // at full strength after minutes, so every wake the boat has ever laid stays on the sea.
            const float halfLife = 6f;
            const float dt = 1f / 60f;
            const int frames = 60 * 120;   // two minutes

            float correct = 1f;
            float sabotaged = 1f;
            for (int i = 0; i < frames; i++)
            {
                correct = FoamBuffer.Decay(correct, halfLife, dt);
                sabotaged *= 1f;           // the sabotage: the decay multiplier stuck at 1
            }

            Assert.Less(correct, 1e-5f,
                "After two minutes a 6 s half-life must have taken the foam to essentially nothing " +
                "(0.5^20 ≈ 9.5e-7 exactly; the bound is loose enough for float32 accumulation).");
            Assert.AreEqual(1f, sabotaged, 1e-6f);
            TestContext.WriteLine(
                $"MEASURED DECAY SABOTAGE — after 120 s at a {halfLife} s half-life the shipped model " +
                $"leaves {correct:E3} of the foam; with the half-life broken it leaves {sabotaged:F6} " +
                "— i.e. every wake ever laid is still on the sea at full strength, and the buffer " +
                "saturates to solid white.");
        }

        [Test]
        public void HalfLife_SetsAVisibleLifetime_NotAnInstantPop()
        {
            // A sanity band on the tunable itself: at the 6 s default a wake is still clearly visible
            // after 6 s and essentially gone by 30 s — "fade over seconds", the owner's check.
            Assert.Greater(FoamBuffer.Decay(1f, 6f, 6f), 0.4f);
            Assert.Less(FoamBuffer.Decay(1f, 6f, 30f), 0.05f);
        }

        // ================= INJECTION SHAPING ====================================================

        [Test]
        public void Shape01_IsZeroAtRest_MonotoneAndSaturating()
        {
            Assert.AreEqual(0f, FoamBuffer.Shape01(0f, 0.5f, 2.5f), 1e-6f, "A still hull churns nothing.");
            Assert.AreEqual(0f, FoamBuffer.Shape01(-1f, 0.5f, 2.5f), 1e-6f, "A negative rate is not foam.");
            Assert.AreEqual(1f, FoamBuffer.Shape01(5f, 0.5f, 2.5f), 1e-6f, "Well past the knee it saturates.");

            float previous = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float value = FoamBuffer.Shape01(i * 0.02f, 0.5f, 2.5f);
                Assert.GreaterOrEqual(value, previous, "The shaping curve must never fall as the hull works harder.");
                previous = value;
            }
        }

        [Test]
        public void Shape01_AboveExponentOne_MakesASlapChurnDisproportionately()
        {
            // The owner's ask as a number: doubling the rate must MORE than double the foam, or a
            // gentle rise-and-fall churns half as much as a hard slap and the two read alike.
            const float knee = 1f;
            float gentle = FoamBuffer.Shape01(0.2f, knee, 2.5f);
            float hard = FoamBuffer.Shape01(0.4f, knee, 2.5f);
            Assert.Greater(hard, gentle * 2f,
                "At an exponent above 1 a slap twice as hard must churn MORE than twice the foam. " +
                "This is the difference between a hull working the water and a hull breathing on it.");

            // …and at exactly 1 the response is linear, which is the documented meaning of the floor.
            Assert.AreEqual(FoamBuffer.Shape01(0.4f, knee, 1f),
                            FoamBuffer.Shape01(0.2f, knee, 1f) * 2f, 1e-5f);
        }

        [Test]
        public void Injection01_CombinesBothChannels_AndEitherAloneCanChurn()
        {
            // The bobbing case, which is the reason this item was pulled forward: NO way on at all,
            // and the hull still churns.
            float bobbingAtAnchor = FoamBuffer.Injection01(
                horizontalSpeed: 0f, verticalRate: 0.4f,
                wakeKnee: 3f, wakeExponent: 1f, wakeWeight: 1f,
                slapKnee: 0.5f, slapExponent: 2.5f, slapWeight: 1f);
            Assert.Greater(bobbingAtAnchor, 0f,
                "A hull bobbing at her mooring churned NOTHING. That is the exact defect the owner " +
                "raised on 2026-08-01 and the reason this item was moved forward.");

            float makingWayOnGlass = FoamBuffer.Injection01(
                2f, 0f, 3f, 1f, 1f, 0.5f, 2.5f, 1f);
            Assert.Greater(makingWayOnGlass, 0f, "A boat under way on flat water still leaves a wake.");

            float both = FoamBuffer.Injection01(2f, 0.4f, 3f, 1f, 1f, 0.5f, 2.5f, 1f);
            Assert.Greater(both, Mathf.Max(bobbingAtAnchor, makingWayOnGlass),
                "Punching to windward in a chop must churn more than either motion alone.");
            Assert.LessOrEqual(both, 1f, "The combined injection must stay a 0..1 coverage.");
        }

        [Test]
        public void Injection01_WeightZero_SilencesItsChannelExactly()
        {
            Assert.AreEqual(0f, FoamBuffer.Injection01(4f, 0.6f, 3f, 1f, 0f, 0.5f, 2.5f, 0f), 1e-6f,
                "Both channel weights at 0 must be an exact passthrough to no foam at all.");
            Assert.AreEqual(0f, FoamBuffer.Injection01(4f, 0f, 3f, 1f, 0f, 0.5f, 2.5f, 1f), 1e-6f,
                "Wake weight 0 with no vertical motion must churn nothing.");
        }

        // ================= THE BOB SIGNAL =======================================================

        [Test]
        public void AHullWithNoInertia_TracksTheSurface_AndNeverChurns()
        {
            // The invariant that proves the bob channel is reading RELATIVE motion, not just
            // re-reading the wave height: a hull that follows the sea exactly displaces nothing.
            const float dt = 1f / 60f;
            float hull = 0f, previousHull = 0f, previousSurface = 0f;

            for (int i = 1; i <= 200; i++)
            {
                float surface = Mathf.Sin(i * dt * 3f) * 0.6f;   // a lively chop
                hull = FoamBuffer.FollowSurface(previousHull, surface, tauSeconds: 0f, dt);
                float rate = FoamBuffer.RelativeHeaveRate(surface, hull, previousSurface, previousHull, dt);

                Assert.AreEqual(0f, rate, 1e-5f,
                    "A hull with zero response time is riding the surface exactly, so its relative " +
                    "vertical velocity must be identically zero — no lag, no slap, no churn. " +
                    "hullResponseSeconds = 0 is meant to be a real off switch.");
                previousSurface = surface;
                previousHull = hull;
            }
        }

        [Test]
        public void AHullWithInertia_LagsTheSurface_AndChurnsInAChop()
        {
            const float dt = 1f / 60f;
            float hull = 0f, previousHull = 0f, previousSurface = 0f;
            float peakRate = 0f;

            for (int i = 1; i <= 400; i++)
            {
                float surface = Mathf.Sin(i * dt * 3f) * 0.6f;
                hull = FoamBuffer.FollowSurface(previousHull, surface, tauSeconds: 0.2f, dt);
                if (i > 60)   // past the settling transient
                    peakRate = Mathf.Max(peakRate,
                        FoamBuffer.RelativeHeaveRate(surface, hull, previousSurface, previousHull, dt));
                previousSurface = surface;
                previousHull = hull;
            }

            Assert.Greater(peakRate, 0.05f,
                "A hull with real inertia in a real chop showed almost no relative vertical motion, " +
                "so the bobbing case would churn nothing.");
            TestContext.WriteLine(
                $"Peak relative heave rate for a 0.2 s hull in a 0.6 m / 0.48 Hz chop: {peakRate:F3} m/s " +
                "(the slap channel's default knee is 0.5 m/s).");
        }

        [Test]
        public void ASteeperSea_ChurnsMoreThanALazySwell_AtTheSameHeight()
        {
            // Same wave HEIGHT, different periods. The fast one must churn more, which is the whole
            // reason the signal is a RATE and not an amplitude.
            static float PeakRate(float frequencyHz)
            {
                const float dt = 1f / 60f;
                float hull = 0f, previousHull = 0f, previousSurface = 0f, peak = 0f;
                for (int i = 1; i <= 800; i++)
                {
                    float surface = Mathf.Sin(i * dt * frequencyHz * Mathf.PI * 2f) * 0.6f;
                    hull = FoamBuffer.FollowSurface(previousHull, surface, 0.2f, dt);
                    if (i > 120)
                        peak = Mathf.Max(peak, FoamBuffer.RelativeHeaveRate(
                            surface, hull, previousSurface, previousHull, dt));
                    previousSurface = surface;
                    previousHull = hull;
                }
                return peak;
            }

            float lazySwell = PeakRate(0.12f);    // a long ocean swell
            float shortChop = PeakRate(0.8f);     // a short harbour slop
            Assert.Greater(shortChop, lazySwell * 2f,
                "A short steep chop and a long lazy swell of the SAME height churned about the same " +
                "amount. The bob signal is supposed to be a rate — a hull rides a swell and fights a " +
                "chop.");
            TestContext.WriteLine(
                $"Same 0.6 m height: lazy swell (0.12 Hz) peaks at {lazySwell:F3} m/s, short chop " +
                $"(0.8 Hz) at {shortChop:F3} m/s.");
        }

        // ================= THE DRAWN WINDOW (owner eyeball 2026-08-27, defect 3) =================
        //
        // "the whole foam band shifts by 1-2 px as ONE unit ... it's noticeable it's a separate entity
        // from the water; they shift in large groups". The content scrolls in WHOLE cells and banks the
        // remainder, which it must — a sub-texel scroll resamples the buffer into itself every frame and
        // smudges the wake. The defect was that the buffer was also DRAWN at the bare lattice origin, so
        // the bank was invisible until it crossed a cell and the entire band teleported 0.125 m at once.

        [Test]
        public void TheDrawnFoam_AdvancesByExactlyTheDrift_EveryFrame_WithNoJumps()
        {
            // THE INVARIANT, and it is the whole fix: drawn displacement = whole cells scrolled +
            // banked remainder, which AdvectCells guarantees advances by exactly this frame's drift
            // for ANY sequence of drifts. So the frame the content jumps a cell, the residual drops by
            // the same cell and the two cancel. Nothing the eye can see moves discontinuously.
            var residual = Vector2.zero;
            Vector2 lattice = FoamBuffer.WorldCellOrigin(Vector2.zero, 96f);
            Vector2 contentMoved = Vector2.zero;
            Vector2 expected = Vector2.zero;
            float previousStep = -1f;
            var previousDrawn = new Vector2(float.NaN, float.NaN);

            // A drift that crosses a cell boundary many times over the run, at an awkward ratio so the
            // crossings never line up with the frames.
            var perFrame = new Vector2(0.0413f, -0.0271f);
            for (int frame = 0; frame < 200; frame++)
            {
                Vector2Int cells = FoamBuffer.AdvectCells(ref residual, perFrame);
                contentMoved += new Vector2(cells.x, cells.y) * FoamBuffer.CellSize;
                expected += perFrame;

                Vector2 drawnOrigin = FoamBuffer.DrawOrigin(lattice, residual);
                // Where a mark laid at world 0 on frame 0 is DRAWN now: the content has moved
                // `contentMoved`, and the window it is read through is offset by the residual.
                Vector2 drawn = contentMoved + (drawnOrigin - lattice);

                Assert.AreEqual(expected.x, drawn.x, 1e-3f,
                    $"frame {frame}: the drawn foam is not where the wind has carried it. The whole " +
                    "point of publishing lattice+residual is that the DRAWN position tracks the true " +
                    "drift continuously while the buffer still scrolls in whole texels.");
                Assert.AreEqual(expected.y, drawn.y, 1e-3f, $"frame {frame} (y)");

                // …and EVERY frame moves it the same distance. Measured off the drawn position alone,
                // so this is not a restatement of the assert above: with the bare lattice this number
                // is 0 on most frames and a whole 0.125 m cell on the rest, which is the group shift
                // the owner saw. previousStep starts negative to skip the first frame.
                if (!float.IsNaN(previousDrawn.x))
                {
                    float step = (drawn - previousDrawn).magnitude;
                    if (previousStep >= 0f)
                        Assert.AreEqual(previousStep, step, 1e-3f,
                            $"frame {frame}: the drawn band moved a DIFFERENT distance this frame than " +
                            "last. That is the 1-2 px group shift — a whole-cell teleport showing through.");
                    previousStep = step;
                }
                previousDrawn = drawn;
            }
        }

        [Test]
        public void SABOTAGE_DrawAtTheBareLattice_AndTheWholeBandTELEPORTS()
        {
            // The pre-fix behaviour, measured rather than described. Drawing at the lattice origin
            // means the drawn position only ever changes when a whole cell is spent: it sits perfectly
            // still for many frames and then jumps a full 0.125 m — 4 screen px at PPU 32, and every
            // texel of the band does it in the same frame, which is why it read as one entity.
            var residual = Vector2.zero;
            var perFrame = new Vector2(0.0213f, 0f);
            Vector2 contentMoved = Vector2.zero;

            int stationary = 0, jumps = 0;
            float biggestJump = 0f;
            for (int frame = 0; frame < 200; frame++)
            {
                Vector2Int cells = FoamBuffer.AdvectCells(ref residual, perFrame);
                float step = cells.x * FoamBuffer.CellSize;          // the BARE-lattice drawn step
                contentMoved.x += step;
                if (Mathf.Approximately(step, 0f)) stationary++;
                else { jumps++; biggestJump = Mathf.Max(biggestJump, Mathf.Abs(step)); }
            }

            Assert.Greater(stationary, jumps * 2,
                "The sabotage is not reproducing the defect — most frames must draw the band as " +
                "perfectly stationary for the jump to read as a teleport.");
            Assert.GreaterOrEqual(biggestJump, FoamBuffer.CellSize - 1e-4f,
                $"…and when it does move it moves a whole cell ({FoamBuffer.CellSize} m) at once. " +
                "That is what DrawOrigin exists to spread smoothly across the frames in between.");
        }

        [Test]
        public void DrawOrigin_IsTheLatticeOrigin_BitExact_WithNoDrift()
        {
            // The A/B: on a windless, currentless sea the residual never leaves zero, so this round's
            // change is provably invisible — the same window, to the bit, that shipped before it.
            var residual = Vector2.zero;
            for (int i = 0; i < 50; i++) FoamBuffer.AdvectCells(ref residual, Vector2.zero);

            Vector2 lattice = FoamBuffer.WorldCellOrigin(new Vector2(-71.31f, 22.87f), 96f);
            Vector2 drawn = FoamBuffer.DrawOrigin(lattice, residual);
            Assert.AreEqual(lattice.x, drawn.x, 0f, "x is not bit-exact with no drift");
            Assert.AreEqual(lattice.y, drawn.y, 0f, "y is not bit-exact with no drift");
        }

        [Test]
        public void ASubTexelWindowAdvance_MovesTheComposedFoam_BySubTexelSteps_AcrossTheBoundary()
        {
            // The handoff's pin, run right through the frame that used to teleport. Advance the window
            // an EIGHTH of a texel at a time: on step 8 the bank crosses a whole cell and the content
            // scrolls, and the drawn foam must still have moved one eighth of a cell — not a whole one,
            // and not nothing on the seven steps before it.
            var residual = Vector2.zero;
            Vector2 lattice = FoamBuffer.WorldCellOrigin(new Vector2(4f, 4f), 96f);
            float eighth = FoamBuffer.CellSize / 8f;

            Vector2 contentMoved = Vector2.zero;
            float previousDrawn = float.NaN;
            bool crossedACell = false;
            for (int i = 0; i < 24; i++)
            {
                Vector2Int cells = FoamBuffer.AdvectCells(ref residual, new Vector2(eighth, 0f));
                if (cells.x != 0) crossedACell = true;
                contentMoved += new Vector2(cells.x, cells.y) * FoamBuffer.CellSize;

                // Where a mark laid before the run is drawn now: content displacement, read through a
                // window offset by the residual it has not spent yet.
                float drawn = contentMoved.x + FoamBuffer.DrawOrigin(lattice, residual).x - lattice.x;
                if (!float.IsNaN(previousDrawn))
                    Assert.AreEqual(eighth, drawn - previousDrawn, 1e-5f,
                        $"step {i}: the composed foam moved {drawn - previousDrawn:0.0000} m instead of " +
                        $"{eighth:0.0000} m. On the steps between cell crossings the bare-lattice window " +
                        "moved it 0, and on the crossing it moved it a whole cell — that pair IS the " +
                        "owner's \"shifts as one unit\".");
                previousDrawn = drawn;
            }
            Assert.IsTrue(crossedACell,
                "the run never crossed a cell boundary, so it never exercised the frame that used to " +
                "teleport — the test would be passing without testing anything.");
        }

        // ================= THE FRESHNESS CHANNEL (owner eyeball 2026-08-27, defect 2) ============

        [Test]
        public void Freshness_IsAClock_ResetByChurnAndNeverAccumulating()
        {
            // The property that made the second channel worth its byte: unlike coverage, this CANNOT
            // saturate, because churn takes a max rather than adding. Hammer it for a second at 60 fps
            // at full vigour and it sits at 1, not above it and not pinned there by accumulation.
            float decay = FoamBuffer.DecayFactor(4f, 1f / 60f);
            float fresh = 0f;
            for (int i = 0; i < 60; i++) fresh = FoamBuffer.Freshness(fresh, decay, 1f);
            Assert.AreEqual(1f, fresh, 1e-5f, "full-vigour churn must read exactly fresh");

            // …and a HALF-vigour churn marks half, however long it goes on. Coverage would have added
            // its way to the ceiling and lost the distinction entirely — that is the round-1 defect.
            fresh = 0f;
            for (int i = 0; i < 600; i++) fresh = FoamBuffer.Freshness(fresh, decay, 0.5f);
            Assert.AreEqual(0.5f, fresh, 1e-5f,
                "Freshness is a CLOCK, not an accumulation: a hull working the water half as hard " +
                "marks half as fresh no matter how long it works. Adding here would re-create the " +
                "saturation that made the wake draw one flat white.");
        }

        [Test]
        public void Freshness_DecaysMonotonically_OnceTheChurnStops()
        {
            float decay = FoamBuffer.DecayFactor(4f, 0.25f);
            float fresh = 1f, previous = 2f;
            for (int i = 0; i < 80; i++)
            {
                fresh = FoamBuffer.Freshness(fresh, decay, 0f);
                Assert.Less(fresh, previous, "water that has aged never gets younger");
                previous = fresh;
            }
            Assert.Less(fresh, 0.05f, "after 20 s at a 4 s half-life the churn must be long cold");
        }

        [Test]
        public void Freshness_HalvesAtItsOwnHalfLife_LikeTheCoverageChannel()
        {
            // Both channels are exponential and therefore frame-rate independent; they simply run at
            // different rates. Ten small steps must age exactly as far as one big one.
            float oneStep = FoamBuffer.Freshness(1f, FoamBuffer.DecayFactor(4f, 4f), 0f);
            float many = 1f;
            for (int i = 0; i < 40; i++) many = FoamBuffer.Freshness(many, FoamBuffer.DecayFactor(4f, 0.1f), 0f);
            Assert.AreEqual(0.5f, oneStep, 1e-4f, "one half-life must halve the freshness");
            Assert.AreEqual(oneStep, many, 1e-4f, "the decay must compose across step sizes");
        }

        [Test]
        public void TheFreshnessUpdate_MatchesTheShader()
        {
            // The twin tripwire, same discipline as the cell grid and the slot count above: the max is
            // the load-bearing operator, and an `+=` here would silently rebuild the saturation defect
            // on the GPU side only, where no C# test could see it.
            string code = StripComments(ReadShaderSource());
            Assert.IsTrue(Regex.IsMatch(code, @"fresh\s*=\s*max\s*\(\s*fresh\s*,"),
                "The advect pass no longer MAXes its freshness channel. Adding into it re-creates " +
                "exactly the saturation that made the round-1 age proxy collapse — see " +
                "FoamBuffer.Freshness and WakeFoamAgeingMeasurementTests.");
            Assert.IsTrue(code.Contains("_HHFoamAgeDecay"),
                "The freshness channel must decay on its OWN half-life (FoamShaderIds.AgeDecay). " +
                "Sharing the coverage half-life puts the sea's blues in the tail the alpha has " +
                "already faded to nothing.");
            Assert.IsTrue(Regex.IsMatch(code, @"return\s+float4\s*\(\s*saturate\s*\(\s*foam\s*\)\s*,\s*saturate\s*\(\s*fresh\s*\)"),
                "The pass must write BOTH channels: r = coverage, g = freshness. A single-channel " +
                "write leaves the age reading whatever the target was cleared to.");
        }

        // ================= THE SHADER SEAM (tripwires) ==========================================

        [Test]
        public void CellGrid_MatchesTheShader()
        {
            Match m = Regex.Match(ReadShaderSource(), @"#define\s+FOAM_CELLS_PER_UNIT\s+([0-9.]+)");
            Assert.IsTrue(m.Success,
                "FOAM_CELLS_PER_UNIT is gone from the foam advect shader. It is one half of a seam " +
                "with FoamBuffer.CellsPerUnit — if the buffer's grid moved, both halves move in the " +
                "same PR.");
            Assert.AreEqual(FoamBuffer.CellsPerUnit,
                            float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1e-4f,
                "The C# twin and the HLSL pass put the buffer's cells on DIFFERENT world grids, so " +
                "the origin the feature snaps to is not the lattice the shader injects on — foam " +
                "would land half a cell off its hull and the whole crawl argument stops holding.");
        }

        [Test]
        public void InjectorSlotCount_MatchesTheShader()
        {
            Match m = Regex.Match(ReadShaderSource(), @"#define\s+FOAM_MAX_INJECTORS\s+(\d+)");
            Assert.IsTrue(m.Success,
                "FOAM_MAX_INJECTORS is gone from the foam advect shader. It is one half of a seam " +
                "with FoamBuffer.MaxInjectors.");
            Assert.AreEqual(FoamBuffer.MaxInjectors, int.Parse(m.Groups[1].Value),
                "The registry would pack more (or fewer) injection slots than the shader reads. " +
                "⚠️ The count must also stay a COMPILE-TIME constant: ADR 0027 flags [unroll] over a " +
                "runtime bound as a known magenta trap.");
        }

        [Test]
        public void TheBufferGrid_IsNotTheMaterialsPixelsPerUnit()
        {
            // Not a style preference — a measured hazard. Water.mat ships _PixelsPerUnit at 24 (the
            // owner has dragged that slider), so a buffer quantized through it would run on a 24
            // cells/m lattice while the C# side — which cannot read a material — stayed on its own.
            // The two would disagree silently and the foam would land off its hull. Same ruling, same
            // reason, as WaveFetch.PixelsPerUnit / FETCH_MARCH_PPU.
            //
            // COMMENTS ARE STRIPPED FIRST: this guards what the pass DOES. The shader is expected —
            // wanted — to NAME _PixelsPerUnit in a comment explaining why it does not use it, and a
            // guard that made documenting the hazard impossible would be exactly backwards.
            string code = StripComments(ReadShaderSource());
            Assert.IsFalse(code.Contains("_PixelsPerUnit"),
                "The foam advect pass now references _PixelsPerUnit IN CODE. That is an ART knob the " +
                "owner drags (Water.mat ships it at 24, not 32) and the C# twin cannot read a " +
                "material — quantizing through it makes the two halves of this seam drift silently. " +
                "The buffer owns FOAM_CELLS_PER_UNIT for exactly this reason.");
        }
    }
}
