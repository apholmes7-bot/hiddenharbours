using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The iso-dory's wave-coupled rock selection (<see cref="DoryRockMath"/>, ADR 0018 B2): the wave
    /// under the hull is turned into the drawn rock frame so the rock <b>corresponds to the waves</b>
    /// (owner's ask). These pin the sync the art director's README asks for — the wave CREST maps to
    /// rock frame 2 and the TROUGH to frame 6 — plus the phase reconstruction, the frame rounding, the
    /// hysteresis, and the heading→row mapping the grid is indexed by. All pure math, headless, no
    /// physics step, no allocation (same discipline as <see cref="BoatWaveMotionMathTests"/>).
    /// </summary>
    public class DoryRockMathTests
    {
        private const int Frames = 8;         // the DoryIsoRock sheet: 8 rock frames per heading
        private const float Step = 360f / Frames;

        // ---- phase reconstruction: Height = A·sin(phase), slope·d = A·k·cos(phase) → phase ----------

        [Test]
        public void PhaseDegrees_ReconstructsTheTrainPhase_OnItsAxis()
        {
            // Build a synthetic single-train surface at a known phase and confirm PhaseDegrees recovers it.
            const float A = 0.8f;
            const float k = 0.5f;
            Vector2 d = new Vector2(0f, 1f);   // swell running north (+Y)

            for (int deg = 0; deg < 360; deg += 15)
            {
                float rad = deg * Mathf.Deg2Rad;
                float height = A * Mathf.Sin(rad);                 // = A·sin(phase)
                Vector2 slope = (A * k * Mathf.Cos(rad)) * d;      // = A·k·cos(phase) along d
                float got = DoryRockMath.PhaseDegrees(height, slope, d, k);
                Assert.AreEqual(deg, got, 1e-2f, $"phase {deg}° must round-trip");
            }
        }

        [Test]
        public void PhaseDegrees_WorksOffAxis_ProjectsSlopeOntoSwell()
        {
            // A non-cardinal swell direction: the cosine part must be recovered on the swell's OWN axis.
            const float A = 0.5f;
            const float k = 0.9f;
            Vector2 d = new Vector2(0.6f, 0.8f).normalized;

            float rad = 130f * Mathf.Deg2Rad;
            float height = A * Mathf.Sin(rad);
            Vector2 slope = (A * k * Mathf.Cos(rad)) * d;
            Assert.AreEqual(130f, DoryRockMath.PhaseDegrees(height, slope, d, k), 1e-2f);
        }

        [Test]
        public void PhaseDegrees_CrestIsNinety_TroughIsTwoSeventy()
        {
            const float A = 0.7f;
            const float k = 0.4f;
            Vector2 d = Vector2.up;
            // Crest: height max, slope zero.
            Assert.AreEqual(90f, DoryRockMath.PhaseDegrees(A, Vector2.zero, d, k), 1e-2f, "crest → 90°");
            // Trough: height min, slope zero.
            Assert.AreEqual(270f, DoryRockMath.PhaseDegrees(-A, Vector2.zero, d, k), 1e-2f, "trough → 270°");
        }

        [Test]
        public void PhaseDegrees_FlatSample_IsZero_NeverNaN()
        {
            float got = DoryRockMath.PhaseDegrees(0f, Vector2.zero, Vector2.up, 0.5f);
            Assert.IsFalse(float.IsNaN(got), "flat sample never NaN");
            Assert.AreEqual(0f, got, 1e-4f, "a flat sample reads phase 0");
        }

        [Test]
        public void PhaseDegrees_DegenerateSwellDir_FallsBackToNorth_NeverNaN()
        {
            float got = DoryRockMath.PhaseDegrees(0.3f, new Vector2(0.1f, 0.2f), Vector2.zero, 0.5f);
            Assert.IsFalse(float.IsNaN(got), "zero swell dir → +Y fallback, never NaN");
        }

        // ---- frame from phase: the README's sync (crest → 2, trough → 6) ----------------------------

        [Test]
        public void FrameFromPhase_CrestIsFrame2_TroughIsFrame6()
        {
            Assert.AreEqual(2, DoryRockMath.FrameFromPhaseDegrees(90f, Frames, 0f), "crest (90°) → frame 2");
            Assert.AreEqual(6, DoryRockMath.FrameFromPhaseDegrees(270f, Frames, 0f), "trough (270°) → frame 6");
        }

        [Test]
        public void FrameFromPhase_EachFramePhase_MapsOneToOne()
        {
            // Frame i sits at phase i·45°, per the README (a = i·45°).
            for (int i = 0; i < Frames; i++)
                Assert.AreEqual(i, DoryRockMath.FrameFromPhaseDegrees(i * Step, Frames, 0f),
                    $"phase {i * Step}° → frame {i}");
        }

        [Test]
        public void FrameFromPhase_FullLoop_IsMonotoneRoundTheCycle()
        {
            // Walking the phase 0→360 must step the frame 0,1,…,7 and wrap to 0 — never jump backwards
            // (beyond the single wrap at the top). Guards the "in lockstep with the swell" feel.
            int prev = DoryRockMath.FrameFromPhaseDegrees(0f, Frames, 0f);
            int wraps = 0;
            for (float p = 0f; p <= 360f; p += 2.5f)
            {
                int f = DoryRockMath.FrameFromPhaseDegrees(p, Frames, 0f);
                int delta = f - prev;
                if (delta < 0) { wraps++; Assert.AreEqual(0, f, "the only backward step is the 7→0 wrap"); }
                else Assert.LessOrEqual(delta, 1, $"frame advances by at most 1 across {p}°");
                prev = f;
            }
            Assert.AreEqual(1, wraps, "exactly one 7→0 wrap over a full loop");
        }

        [Test]
        public void FrameFromPhase_Calibration_ShiftsTheMapping()
        {
            // A +45° calibration moves the crest onto frame 3 (the owner-tunable nudge; default 0).
            Assert.AreEqual(3, DoryRockMath.FrameFromPhaseDegrees(90f, Frames, 45f), "+45° cal → crest on frame 3");
        }

        [Test]
        public void FrameFromPhase_AlwaysInRange_AndDegenerateCountSafe()
        {
            for (float p = -720f; p <= 720f; p += 7f)
                Assert.That(DoryRockMath.FrameFromPhaseDegrees(p, Frames, 0f), Is.InRange(0, Frames - 1));
            Assert.AreEqual(0, DoryRockMath.FrameFromPhaseDegrees(123f, 0, 0f), "count 0 → 0, no divide-by-zero");
        }

        // ---- hysteresis: no flip-flop at a frame boundary -------------------------------------------

        [Test]
        public void AdvanceFrame_Uninitialised_PicksTheIdealFrame()
        {
            Assert.AreEqual(2, DoryRockMath.AdvanceFrame(-1, 90f, Frames, 0f, 8f), "first pick = nearest frame");
        }

        [Test]
        public void AdvanceFrame_HoldsThroughSmallWobbleAtBoundary()
        {
            // Sitting on frame 2 (centre 90°): a nudge just past the 112.5° edge (still within the
            // half-step + hysteresis band) must HOLD frame 2 rather than flip to 3.
            Assert.AreEqual(2, DoryRockMath.AdvanceFrame(2, 113f, Frames, 0f, 8f), "small wobble holds");
            // A clear move past the band advances to the neighbour.
            Assert.AreEqual(3, DoryRockMath.AdvanceFrame(2, 125f, Frames, 0f, 8f), "a real move advances");
        }

        [Test]
        public void AdvanceFrame_ZeroHysteresis_PicksNearestEveryTick()
        {
            // With no hysteresis the band collapses to half a step, so 113° (just past 112.5°) flips to 3.
            Assert.AreEqual(3, DoryRockMath.AdvanceFrame(2, 113f, Frames, 0f, 0f), "0 hysteresis = nearest");
        }

        // ---- grid index + heading→row (the grid is indexed heading×8 + frame) -----------------------

        [Test]
        public void RockGridIndex_IsRowMajor_HeadingTimesCountPlusFrame()
        {
            Assert.AreEqual(0, DoryRockMath.RockGridIndex(0, 0, Frames), "N, frame 0 → 0");
            Assert.AreEqual(2, DoryRockMath.RockGridIndex(0, 2, Frames), "N, crest frame → 2");
            Assert.AreEqual(16, DoryRockMath.RockGridIndex(2, 0, Frames), "E (row 2), frame 0 → 16");
            Assert.AreEqual(63, DoryRockMath.RockGridIndex(7, 7, Frames), "NW, frame 7 → 63 (last slice)");
        }

        [Test]
        public void HeadingToRow_ClockwiseFromNorth_MatchesTheSheetRows()
        {
            // The rock grid's rows are headings CW from north — the same mapping the static facings use.
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(0f,   Frames, 0f), "N → row 0");
            Assert.AreEqual(1, DirectionalBoatSprite.HeadingToFacingIndex(45f,  Frames, 0f), "NE → row 1");
            Assert.AreEqual(2, DirectionalBoatSprite.HeadingToFacingIndex(90f,  Frames, 0f), "E → row 2");
            Assert.AreEqual(4, DirectionalBoatSprite.HeadingToFacingIndex(180f, Frames, 0f), "S → row 4");
            Assert.AreEqual(6, DirectionalBoatSprite.HeadingToFacingIndex(270f, Frames, 0f), "W → row 6");
        }

        // ---- integration: a real WaveMath swell → crest sample lands on frame 2 ----------------------

        [Test]
        public void Integration_RealSwell_CrestSampleSelectsFrame2_TroughFrame6()
        {
            // A single pure-sine train (no secondaries, no crest sharpening) so the reconstruction is exact.
            var settings = WaveFieldSettings.Default;
            settings.SecondaryTrainCount = 0;
            settings.CrestSharpening = 1f;
            var trains = WaveMath.TrainsFrom(new Vector2(0f, 10f), 1f, settings); // swell running north
            WaveTrain primary = trains[0];
            float k = (2f * Mathf.PI) / primary.Wavelength;
            Vector2 pos = new Vector2(3.5f, -8.25f);

            // Sweep time; find the crest (max height) and trough (min height) samples along the loop.
            float maxH = float.NegativeInfinity, minH = float.PositiveInfinity;
            int crestFrame = -1, troughFrame = -1;
            for (double t = 0.0; t < 40.0; t += 0.02)
            {
                WaveSample s = WaveMath.Sample(pos, t, in trains);
                float phase = DoryRockMath.PhaseDegrees(s.Height, s.Slope, primary.Direction, k);
                int frame = DoryRockMath.FrameFromPhaseDegrees(phase, Frames, 0f);
                if (s.Height > maxH) { maxH = s.Height; crestFrame = frame; }
                if (s.Height < minH) { minH = s.Height; troughFrame = frame; }
            }

            Assert.Greater(maxH, 0f, "the swell must actually crest");
            Assert.Less(minH, 0f, "the swell must actually trough");
            Assert.AreEqual(2, crestFrame, "the wave crest under the hull selects rock frame 2");
            Assert.AreEqual(6, troughFrame, "the wave trough selects rock frame 6");
        }

        [Test]
        public void Integration_IsDeterministic_SameSwellSameFrame()
        {
            var trains = WaveMath.TrainsFrom(new Vector2(6f, 2f), 0.7f, WaveFieldSettings.Default);
            WaveTrain primary = trains[0];
            float k = (2f * Mathf.PI) / primary.Wavelength;
            Vector2 pos = new Vector2(12.3f, 4.1f);

            WaveSample a = WaveMath.Sample(pos, 123.4, in trains);
            WaveSample b = WaveMath.Sample(pos, 123.4, in trains);
            int fa = DoryRockMath.FrameFromPhaseDegrees(
                DoryRockMath.PhaseDegrees(a.Height, a.Slope, primary.Direction, k), Frames, 0f);
            int fb = DoryRockMath.FrameFromPhaseDegrees(
                DoryRockMath.PhaseDegrees(b.Height, b.Slope, primary.Direction, k), Frames, 0f);
            Assert.AreEqual(fa, fb, "deterministic: same swell, same drawn frame");
        }
    }
}
