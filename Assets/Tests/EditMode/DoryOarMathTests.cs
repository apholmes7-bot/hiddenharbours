using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The iso dory's INDEPENDENT OARS (#204) — pure, deterministic column/phase/rock math, covered the same
    /// way <see cref="DoryRockMath"/> is: the sheet contract (cols 0-7 stroke, 8 shipped, 9 trailing; index =
    /// heading×10 + col), the owner-visible behaviours (forward strokes 0→7, <b>back-water is the very same
    /// cycle REVERSED</b>, a lone idle oar trails, both idle past the grace ships), the no-pop guarantee (a
    /// mid-stroke flip continues from the current phase instead of snapping), and the rock coupling that leans
    /// rock-free oar cells onto the rock-BAKED hull frames (heave exact at the known frames).
    /// </summary>
    public class DoryOarMathTests
    {
        const float Deadzone = 0.05f;
        const int Columns = DoryOarMath.StrokeColumns;    // 8

        // ---- the sheet contract (the art's layout — a mis-index here draws the wrong oar) --------

        [Test]
        public void SheetContract_ColumnsAndTheSpecialCells_MatchTheArt()
        {
            Assert.AreEqual(10, DoryOarMath.ColumnsPerHeading, "10 cols per heading row: 8 stroke + rest + trail");
            Assert.AreEqual(8, DoryOarMath.StrokeColumns, "the row-stroke cycle is cols 0..7");
            Assert.AreEqual(8, DoryOarMath.RestingColumn, "col 8 is resting/shipped");
            Assert.AreEqual(9, DoryOarMath.TrailingColumn, "col 9 is trailing");
        }

        [Test]
        public void OarGridIndex_IsRowMajor_HeadingTimesColumnsPlusCol()
        {
            // The slicer lays the sheet out row-major from top-left: index = heading×10 + col.
            Assert.AreEqual(0, DoryOarMath.OarGridIndex(0, 0, 10), "north, first stroke frame");
            Assert.AreEqual(8, DoryOarMath.OarGridIndex(0, DoryOarMath.RestingColumn, 10), "north, shipped");
            Assert.AreEqual(9, DoryOarMath.OarGridIndex(0, DoryOarMath.TrailingColumn, 10), "north, trailing");
            Assert.AreEqual(10, DoryOarMath.OarGridIndex(1, 0, 10), "NE starts the next row");
            Assert.AreEqual(79, DoryOarMath.OarGridIndex(7, 9, 10), "NW trailing is the last of the 80 slices");
        }

        [Test]
        public void HeadingRow_MatchesTheHullsFacingIndex_ForEveryCompassPoint()
        {
            // The oars index their row with the SAME pure snap the hull's picture uses, so they can never
            // disagree with the facing under them. Parity across the full compass, including bucket edges.
            for (int i = 0; i < 8; i++)
            {
                float heading = i * 45f;
                int hullRow = DirectionalBoatSprite.HeadingToFacingIndex(heading, 8, 0f);
                Assert.AreEqual(i, hullRow, $"heading {heading}° is row {i}");

                // …and re-snapping an already-DRAWN (quantized) heading is idempotent — the property the
                // layer leans on when it reads DirectionalBoatSprite.DrawnHeadingDegrees().
                float drawn = DirectionalBoatSprite.SnapHeadingDegrees(heading + 5f, 8, 0f);
                Assert.AreEqual(DirectionalBoatSprite.HeadingToFacingIndex(drawn, 8, 0f),
                                DirectionalBoatSprite.HeadingToFacingIndex(heading + 5f, 8, 0f),
                                "snapping a snapped heading lands on the same row (oars can't drift off the hull)");
            }
        }

        // ---- working / direction / deadzone -------------------------------------------------------

        [Test]
        public void IsWorking_OnlyOutsideTheDeadzone()
        {
            Assert.IsFalse(DoryOarMath.IsWorking(0f, Deadzone), "a still oar is idle");
            Assert.IsFalse(DoryOarMath.IsWorking(Deadzone, Deadzone), "exactly on the deadzone is still idle");
            Assert.IsFalse(DoryOarMath.IsWorking(-Deadzone * 0.5f, Deadzone), "a hair of back-water is idle");
            Assert.IsTrue(DoryOarMath.IsWorking(0.2f, Deadzone), "a real pull works");
            Assert.IsTrue(DoryOarMath.IsWorking(-0.2f, Deadzone), "…and so does a real back-water");
        }

        [Test]
        public void StrokeDirection_IsSignedByTheOarState_AndZeroWhenIdle()
        {
            Assert.AreEqual(1f, DoryOarMath.StrokeDirection(1f, Deadzone), "forward pull runs the cycle forward");
            Assert.AreEqual(-1f, DoryOarMath.StrokeDirection(-1f, Deadzone), "back-water runs it in reverse");
            Assert.AreEqual(0f, DoryOarMath.StrokeDirection(0f, Deadzone), "an idle oar holds its phase (no drift)");
        }

        // ---- the forward stroke: cols 0→7, looping ------------------------------------------------

        [Test]
        public void ForwardStroke_WalksColumnsZeroToSeven_ThenLoops()
        {
            // One frame per step at 1 fps × 1 s: the column must advance 0,1,…,7 and wrap back to 0.
            float phase = 0f;
            for (int expected = 0; expected < Columns; expected++)
            {
                Assert.AreEqual(expected, DoryOarMath.StrokeColumnFromPhase(phase, Columns),
                    $"the forward stroke draws col {expected} at phase {phase}");
                phase = DoryOarMath.AdvanceStrokePhase(phase, +1f, 1f, 1f, Columns);
            }
            Assert.AreEqual(0, DoryOarMath.StrokeColumnFromPhase(phase, Columns),
                "past col 7 the cycle LOOPS back to col 0 (a continuous row, not a one-shot)");
        }

        [Test]
        public void Backwater_IsTheSameCycleWithTheColumnOrderReversed()
        {
            // The owner's "I think reverse is missing": it isn't — per the art README, REVERSED playback of
            // cols 0..7 IS the backing stroke. Starting at the top of the cycle, backing must walk 7,6,…,0.
            float phase = 0f;
            var seen = new int[Columns];
            for (int i = 0; i < Columns; i++)
            {
                phase = DoryOarMath.AdvanceStrokePhase(phase, -1f, 1f, 1f, Columns);
                seen[i] = DoryOarMath.StrokeColumnFromPhase(phase, Columns);
            }
            CollectionAssert.AreEqual(new[] { 7, 6, 5, 4, 3, 2, 1, 0 }, seen,
                "back-water plays the stroke cycle in REVERSE column order (7→0)");
        }

        // ---- phase continuity: a mid-stroke flip must never snap ----------------------------------

        [Test]
        public void StateFlip_ContinuesFromTheCurrentPhaseReversed_NeverSnapsToZero()
        {
            // Row forward to the middle of the sweep…
            float phase = 0f;
            for (int i = 0; i < 3; i++) phase = DoryOarMath.AdvanceStrokePhase(phase, +1f, 1f, 1f, Columns);
            Assert.AreEqual(3, DoryOarMath.StrokeColumnFromPhase(phase, Columns), "mid-sweep at col 3");

            // …then back-water. The very next frame must be col 2 (sweeping back the way it came), NOT col 0
            // (a snap to the top of the cycle) and NOT col 4 (carrying on forward).
            phase = DoryOarMath.AdvanceStrokePhase(phase, -1f, 1f, 1f, Columns);
            int afterFlip = DoryOarMath.StrokeColumnFromPhase(phase, Columns);
            Assert.AreEqual(2, afterFlip,
                "a flip to back-water continues from the CURRENT phase reversed — no pop, no snap to 0");

            // …and flipping back resumes forward from there.
            phase = DoryOarMath.AdvanceStrokePhase(phase, +1f, 1f, 1f, Columns);
            Assert.AreEqual(3, DoryOarMath.StrokeColumnFromPhase(phase, Columns),
                "flipping back to a pull resumes forward from where the oar IS");
        }

        [Test]
        public void FlippingAcrossTheCycleStart_WrapsBackwardToTheTop_NotToZero()
        {
            // Backing at the very start of the cycle must wrap DOWN to col 7 (a continuous sweep through the
            // loop join), never clamp at 0.
            float phase = DoryOarMath.AdvanceStrokePhase(0f, -1f, 1f, 1f, Columns);
            Assert.AreEqual(7, DoryOarMath.StrokeColumnFromPhase(phase, Columns),
                "backing off col 0 wraps to col 7 — the cycle is a loop in both directions");
            Assert.GreaterOrEqual(phase, 0f, "the phase stays wrapped into [0, 8)");
            Assert.Less(phase, Columns, "…and never runs past the top of the cycle");
        }

        [Test]
        public void IdleOar_HoldsItsPhase_SoResumingPicksUpMidSweep()
        {
            float phase = 0f;
            for (int i = 0; i < 5; i++) phase = DoryOarMath.AdvanceStrokePhase(phase, +1f, 1f, 1f, Columns);
            float held = DoryOarMath.AdvanceStrokePhase(phase, 0f, 1f, 10f, Columns);   // idle, 10 s
            Assert.AreEqual(phase, held, 1e-6f, "an idle oar's phase doesn't drift — it waits mid-sweep");
        }

        // ---- column selection: stroke / trailing / shipped ---------------------------------------

        [Test]
        public void WorkingOar_DrawsItsStrokeFrame()
        {
            int col = DoryOarMath.ColumnForOar(thisOarWorking: true, phase: 3.4f, otherOarWorking: false,
                                               bothIdleSeconds: 99f, restGraceSeconds: 1f, strokeColumns: Columns);
            Assert.AreEqual(3, col, "a working oar draws its own stroke frame regardless of the other side");
        }

        [Test]
        public void IdleOar_TrailsWhileTheOtherOarWorks()
        {
            // The one-sided stroke (W+A: port ahead only) — the idle side drags in the water, it doesn't ship.
            int col = DoryOarMath.ColumnForOar(thisOarWorking: false, phase: 0f, otherOarWorking: true,
                                               bothIdleSeconds: 0f, restGraceSeconds: 1f, strokeColumns: Columns);
            Assert.AreEqual(DoryOarMath.TrailingColumn, col,
                "an idle oar TRAILS (col 9) while the boat is still being rowed by the other side");
        }

        [Test]
        public void BothIdle_TrailsThroughTheGrace_ThenShips()
        {
            const float grace = 1.2f;
            int during = DoryOarMath.ColumnForOar(false, 0f, false, bothIdleSeconds: grace * 0.5f,
                                                  restGraceSeconds: grace, strokeColumns: Columns);
            Assert.AreEqual(DoryOarMath.TrailingColumn, during,
                "a brief pause between strokes must NOT stow the oars — they trail through the grace");

            int at = DoryOarMath.ColumnForOar(false, 0f, false, grace, grace, Columns);
            Assert.AreEqual(DoryOarMath.RestingColumn, at, "at the grace they ship (col 8)");

            int after = DoryOarMath.ColumnForOar(false, 0f, false, grace * 10f, grace, Columns);
            Assert.AreEqual(DoryOarMath.RestingColumn, after, "…and stay shipped while the boat idles");
        }

        // ---- stroke rate scales with effort ------------------------------------------------------

        [Test]
        public void StrokeRate_ScalesWithEffort_WhenTheInfluenceIsOn()
        {
            Assert.AreEqual(9f, DoryOarMath.StrokeFramesPerSecond(9f, 0.25f, 0f), 1e-4f,
                "influence 0 = every stroke runs at the base tempo (effort ignored)");
            Assert.AreEqual(9f, DoryOarMath.StrokeFramesPerSecond(9f, 1f, 1f), 1e-4f,
                "a full-effort pull always runs at the base tempo");
            Assert.AreEqual(4.5f, DoryOarMath.StrokeFramesPerSecond(9f, 0.5f, 1f), 1e-4f,
                "influence 1 = the rate is proportional to effort (a half pull strokes at half speed)");
            Assert.AreEqual(6.75f, DoryOarMath.StrokeFramesPerSecond(9f, 0.5f, 0.5f), 1e-4f,
                "influence 0.5 = a half pull strokes at three-quarter speed (the shipped default)");
            Assert.AreEqual(4.5f, DoryOarMath.StrokeFramesPerSecond(9f, -0.5f, 1f), 1e-4f,
                "MAGNITUDE drives the rate — a half-effort BACK-water strokes at half speed too");
        }

        // ---- rock coupling: rock-free oar cells lean onto the rock-BAKED hull frames -------------

        [Test]
        public void RockPhase_IsFortyFiveDegreesPerFrame_MatchingTheBakedCycle()
        {
            Assert.AreEqual(0f, DoryOarMath.RockPhaseDegrees(0, 8), 1e-4f, "frame 0 → a = 0°");
            Assert.AreEqual(90f, DoryOarMath.RockPhaseDegrees(2, 8), 1e-4f, "frame 2 → a = 90° (the CREST)");
            Assert.AreEqual(270f, DoryOarMath.RockPhaseDegrees(6, 8), 1e-4f, "frame 6 → a = 270° (the TROUGH)");
            Assert.AreEqual(45f, DoryOarMath.RockDegreesPerFrame, 1e-4f, "the 8-frame sheet steps 45° per frame");
        }

        [Test]
        public void RockPose_AtTheCrest_LiftsTheOarsByTheBakedHeave_Exactly()
        {
            // Frame 2 → a = 90° → sin = 1, cos = 0: the baked heave is at its MAXIMUM and the pitch is zero.
            // The heave must be reproduced EXACTLY: 1.6 px at 32 PPU = 0.05 m.
            var pose = DoryOarMath.RockPose(2, 8, rollDegreesAmplitude: 5f, pitchOffsetMeters: 0.02f,
                                            heavePixels: 1.6f, pixelsPerUnit: 32f, strength: 1f);
            Assert.AreEqual(1.6f / 32f, pose.OffsetY, 1e-6f,
                "at the crest the oars lift by the baked heave EXACTLY (1.6 px / 32 PPU), with no pitch term");
            Assert.AreEqual(5f, pose.RollDegrees, 1e-4f, "…and the roll approximation is at its peak (5°·sin90°)");
        }

        [Test]
        public void RockPose_AtTheTrough_MirrorsTheCrest()
        {
            var crest = DoryOarMath.RockPose(2, 8, 5f, 0.02f, 1.6f, 32f, 1f);
            var trough = DoryOarMath.RockPose(6, 8, 5f, 0.02f, 1.6f, 32f, 1f);   // a = 270° → sin = -1
            Assert.AreEqual(-crest.OffsetY, trough.OffsetY, 1e-6f, "the trough drops the oars by the same heave");
            Assert.AreEqual(-crest.RollDegrees, trough.RollDegrees, 1e-4f, "…and leans them the other way");
        }

        [Test]
        public void RockPose_AtFrameZero_HasNoHeave_AndThePitchIsAtItsPeak()
        {
            // Frame 0 → a = 0° → sin = 0, cos = 1: zero heave, maximum pitch (the zero-crossing of the cycle).
            var pose = DoryOarMath.RockPose(0, 8, rollDegreesAmplitude: 5f, pitchOffsetMeters: 0.02f,
                                            heavePixels: 1.6f, pixelsPerUnit: 32f, strength: 1f);
            Assert.AreEqual(0.02f, pose.OffsetY, 1e-6f, "no heave at a = 0°, so the offset is the pitch term alone");
            Assert.AreEqual(0f, pose.RollDegrees, 1e-4f, "…and the hull is level in roll there");
        }

        [Test]
        public void RockPose_OnACalmHull_IsLevel()
        {
            // RockFrame −1 = the hull is drawing its STATIC/level facing (glass calm, or no rock grid). A
            // still hull must never have moving oars.
            var pose = DoryOarMath.RockPose(DoryOarMath.LevelRockFrame, 8, 5f, 0.02f, 1.6f, 32f, 1f);
            Assert.AreEqual(0f, pose.OffsetY, 1e-6f, "a calm hull's oars sit level (no lift)");
            Assert.AreEqual(0f, pose.RollDegrees, 1e-6f, "…and unleaned");
        }

        [Test]
        public void RockPose_StrengthZero_DisablesTheCouplingEntirely()
        {
            var pose = DoryOarMath.RockPose(2, 8, 5f, 0.02f, 1.6f, 32f, strength: 0f);
            Assert.AreEqual(0f, pose.OffsetY, 1e-6f, "strength 0 = the oars sit level even on a rocking hull");
            Assert.AreEqual(0f, pose.RollDegrees, 1e-6f, "…the owner's off switch (rule 6)");
        }

        [Test]
        public void RockPose_ScalesLinearlyWithStrength()
        {
            var full = DoryOarMath.RockPose(2, 8, 5f, 0.02f, 1.6f, 32f, 1f);
            var half = DoryOarMath.RockPose(2, 8, 5f, 0.02f, 1.6f, 32f, 0.5f);
            Assert.AreEqual(full.OffsetY * 0.5f, half.OffsetY, 1e-6f, "the master strength scales the lift");
            Assert.AreEqual(full.RollDegrees * 0.5f, half.RollDegrees, 1e-6f, "…and the lean");
        }

        // ---- determinism + guards (no NaN, no divide-by-zero, no stale index) --------------------

        [Test]
        public void TheMathIsDeterministic_SameInputsSameOutputs()
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(DoryOarMath.AdvanceStrokePhase(2.5f, +1f, 9f, 0.016f, Columns),
                                DoryOarMath.AdvanceStrokePhase(2.5f, +1f, 9f, 0.016f, Columns), 0f,
                                "the phase advance is pure — no hidden state, no RNG");
                var a = DoryOarMath.RockPose(3, 8, 5f, 0.02f, 1.6f, 32f, 1f);
                var b = DoryOarMath.RockPose(3, 8, 5f, 0.02f, 1.6f, 32f, 1f);
                Assert.AreEqual(a.OffsetY, b.OffsetY, 0f, "…and so is the rock pose");
                Assert.AreEqual(a.RollDegrees, b.RollDegrees, 0f);
            }
        }

        [Test]
        public void Guards_NonFinitePhaseAndZeroPpu_NeverPropagateNaN()
        {
            float reseeded = DoryOarMath.AdvanceStrokePhase(float.NaN, +1f, 9f, 0.016f, Columns);
            Assert.IsFalse(float.IsNaN(reseeded), "a poisoned phase is re-seeded, never propagated");

            var pose = DoryOarMath.RockPose(2, 8, 5f, 0.02f, 1.6f, pixelsPerUnit: 0f, strength: 1f);
            Assert.IsFalse(float.IsNaN(pose.OffsetY), "a zero PPU is guarded — never a divide-by-zero");
            Assert.IsFalse(float.IsInfinity(pose.OffsetY), "…and never an infinite lift");
        }

        [Test]
        public void StrokeColumn_IsAlwaysInRange_EvenAtTheTopOfTheCycle()
        {
            Assert.AreEqual(Columns - 1, DoryOarMath.StrokeColumnFromPhase(7.999f, Columns),
                "just under the top of the cycle is the LAST stroke frame");
            Assert.AreEqual(Columns - 1, DoryOarMath.StrokeColumnFromPhase(8f, Columns),
                "a phase landing exactly on the top clamps to the last stroke frame — never indexes past it");
            Assert.AreEqual(0, DoryOarMath.StrokeColumnFromPhase(-0.001f, Columns),
                "…and a hair below zero clamps to the first");
        }

        [Test]
        public void NegativeAndOversizedRockFrames_WrapIntoTheCycle()
        {
            Assert.AreEqual(DoryOarMath.RockPhaseDegrees(2, 8), DoryOarMath.RockPhaseDegrees(10, 8), 1e-4f,
                "an out-of-range rock frame wraps into the 8-frame cycle (same pose, no exception)");
        }
    }
}
