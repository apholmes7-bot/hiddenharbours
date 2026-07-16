using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Selects the iso-dory's ROCK FRAME from the wave passing under the hull (owner: <i>"I want the
    /// rock animation to correspond to the waves"</i>). The <c>DoryIsoRock</c> sheet is a canned
    /// roll-dominant rock cycle (art director's README): per frame <c>i</c> the phase is
    /// <c>a = i·45°</c>, heave/roll = <c>sin(a)</c>, so the boat sits at the wave <b>CREST</b> at
    /// <c>a = 90°</c> → <b>frame 2</b> and in the <b>TROUGH</b> at <c>a = 270°</c> → <b>frame 6</b>.
    /// This class turns the sampled surface (<c>WaveMath.WaveSample</c>) into that phase and rounds it
    /// to the nearest of the 8 frames, so the drawn rock tracks the swell in lockstep.
    ///
    /// <para><b>Why phase, not raw height.</b> A given height occurs twice per cycle — once rising,
    /// once falling — so height alone can't say where in the loop we are. Reconstructing the phase
    /// needs both the sine part (the height) and the cosine part (the surface slope along the swell,
    /// which peaks a quarter-cycle before the crest). For a travelling train
    /// <c>Height = A·sin(θ)</c> and <c>slope·d = A·k·cos(θ)</c> (analytic gradient, <c>WaveMath</c>),
    /// so <c>θ = atan2(Height, (slope·d)/k)</c> recovers the loop position: crest → 90°, trough →
    /// 270°, exactly the sync the README asks for.</para>
    ///
    /// <para>Pure, static, allocation-free, deterministic — same discipline (and EditMode coverage)
    /// as <see cref="BoatWaveMotionMath"/> / <c>WaveMath</c>. Visual-only: nothing here feeds the sim
    /// (rule 5) and every calibration/hysteresis knob is owner-tunable on <c>BoatWaveMotion</c>
    /// (rule 6).</para>
    /// </summary>
    public static class DoryRockMath
    {
        /// <summary>Guard floor on the wave number so a degenerate (near-zero-wavelength) train can
        /// never divide by zero when recovering the cosine part. A guard, not a tunable.</summary>
        public const float MinWaveNumber = 1e-6f;

        /// <summary>Below this squared magnitude a swell direction is treated as undefined and falls
        /// back to +Y (north) — the WaveTrain / heading convention. Never NaN.</summary>
        public const float MinSwellSqrMagnitude = 1e-12f;

        /// <summary>
        /// Reconstruct the wave phase (degrees in [0, 360)) under the hull so that
        /// <c>Height = A·sin(phase)</c>. <paramref name="height"/> and <paramref name="slope"/> come
        /// from the sampled surface (<c>WaveSample.Height</c> / <c>.Slope</c>);
        /// <paramref name="swellDir"/> and <paramref name="waveNumber"/> come from the DOMINANT train
        /// (its <c>Direction</c> and <c>k = 2π/λ</c>) so the slope's cosine part is recovered on the
        /// swell's own axis. Crest (height max, slope 0) → 90°; trough (height min) → 270°; rising
        /// zero-crossing → 0°; falling → 180°. A flat sample (height and slope both ~0) returns 0.
        /// </summary>
        public static float PhaseDegrees(float height, Vector2 slope, Vector2 swellDir, float waveNumber)
        {
            float sqr = swellDir.x * swellDir.x + swellDir.y * swellDir.y;
            Vector2 d;
            if (sqr < MinSwellSqrMagnitude)
            {
                d = Vector2.up;
            }
            else
            {
                float inv = 1f / Mathf.Sqrt(sqr);
                d = new Vector2(swellDir.x * inv, swellDir.y * inv);
            }

            float k = Mathf.Max(MinWaveNumber, waveNumber);
            float sinPart = height;                                 // = A·sin(phase)
            float cosPart = (slope.x * d.x + slope.y * d.y) / k;    // = A·cos(phase)

            float deg = Mathf.Atan2(sinPart, cosPart) * Mathf.Rad2Deg;
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return deg;
        }

        /// <summary>
        /// Round a wave phase to the nearest of <paramref name="frameCount"/> evenly-spaced rock
        /// frames, so that the crest (phase 90°) lands on frame 2 and the trough (270°) on frame 6 for
        /// the standard 8-frame sheet. <paramref name="calibrationDegrees"/> nudges the whole mapping
        /// (owner-tunable, default 0) should the art's crest frame ever be re-baked off phase. Half-up
        /// rounding matches <see cref="DirectionalBoatSprite.HeadingToFacingIndex"/> so bucket edges
        /// resolve deterministically. Result is always in [0, frameCount).
        /// </summary>
        public static int FrameFromPhaseDegrees(float phaseDegrees, int frameCount, float calibrationDegrees)
        {
            if (frameCount <= 0) return 0;
            float step = 360f / frameCount;
            int frame = Mathf.FloorToInt((phaseDegrees + calibrationDegrees) / step + 0.5f);
            frame %= frameCount;
            if (frame < 0) frame += frameCount;
            return frame;
        }

        /// <summary>
        /// The row-major index into a heading×frame rock grid: <c>headingRow·frameCount + frame</c>.
        /// Matches the <c>DoryIsoRock</c> slice order (row = heading, col = frame).
        /// </summary>
        public static int RockGridIndex(int headingRow, int frame, int frameCount)
            => headingRow * frameCount + frame;

        /// <summary>
        /// Frame selection WITH hysteresis, to stop a boundary-straddling phase from flip-flopping
        /// between two frames (the sea's cross-chop can jitter the reconstructed phase). Returns the
        /// ideal frame when <paramref name="currentFrame"/> is uninitialised (&lt; 0) or out of range;
        /// otherwise it only leaves the current frame once the phase has moved more than half a step
        /// PLUS <paramref name="hysteresisDegrees"/> from the current frame's centre — so a small
        /// wobble around the edge holds, but genuine progression (or a multi-frame jump) advances.
        /// </summary>
        public static int AdvanceFrame(int currentFrame, float phaseDegrees, int frameCount,
                                       float calibrationDegrees, float hysteresisDegrees)
        {
            if (frameCount <= 0) return 0;
            int ideal = FrameFromPhaseDegrees(phaseDegrees, frameCount, calibrationDegrees);
            if (currentFrame < 0 || currentFrame >= frameCount) return ideal;
            if (ideal == currentFrame) return currentFrame;

            float step = 360f / frameCount;
            // Phase at the centre of the current frame's bucket (inverse of FrameFromPhaseDegrees).
            float centre = currentFrame * step - calibrationDegrees;
            float distance = Mathf.Abs(Mathf.DeltaAngle(centre, phaseDegrees));
            float band = step * 0.5f + Mathf.Max(0f, hysteresisDegrees);
            return distance >= band ? ideal : currentFrame;
        }
    }
}
