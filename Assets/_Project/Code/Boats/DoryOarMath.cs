using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The iso dory's INDEPENDENT OARS: turns each oar's real per-oar state
    /// (<see cref="BoatController.LeftOar"/> / <see cref="BoatController.RightOar"/> — forward +1, back-water
    /// −1, idle 0) into a column of that side's baked sheet, plus the small transform that keeps a rock-free
    /// oar cell riding the leaning gunwale of a rock-BAKED hull frame.
    ///
    /// <para><b>The sheet (art README, #204).</b> <c>DoryOarPort</c>/<c>DoryOarStar</c> are 10 cols × 8 heading
    /// rows (index = <c>heading×10 + col</c>, rows N..NW clockwise — the same order as <c>DoryIsoRock</c>).
    /// Cols <b>0–7 are the row-stroke cycle</b>, col <b>8 is resting/shipped</b>, col <b>9 is trailing</b>.
    /// The forward stroke plays 0→7; <b>BACK-WATER is the very same cycle played in REVERSE</b> (7→0) — that is
    /// the art's design, not a missing animation.</para>
    ///
    /// <para><b>Why a signed phase.</b> Each oar keeps its own phase accumulator (they are independent — one
    /// side can pull while the other backs). The phase advances FORWARD for a forward pull and BACKWARD for a
    /// back-water, and the column is simply the floor of it, so a mid-stroke flip continues from where the oar
    /// IS and sweeps back the way it came (…3 → 2 → 1 → 0 → 7…) instead of snapping to the top of the cycle —
    /// phase continuity with no visual pop, for free.</para>
    ///
    /// <para><b>Rock coupling.</b> The hull's rock is BAKED into its frames (#202) while the oar cells are
    /// drawn rock-free, so the oars must be leaned onto the hull by hand. The baked cycle's parameters (README):
    /// for rock frame <c>i</c>, <c>a = i·45°</c>, roll = 5°·sin(a), pitch = 3°·cos(a), heave = 1.6 px·sin(a).
    /// <see cref="RockPose"/> reproduces the HEAVE exactly (in metres) and approximates roll/pitch as a small
    /// screen-space rotation + offset — enough for a 2-3 px oar overlay to sit on the gunwale; a full
    /// projection-accurate derivation isn't worth the cost at this size (see <see cref="DoryOarLayer"/>).</para>
    ///
    /// <para>Pure, static, allocation-free, deterministic — the same discipline (and EditMode coverage) as
    /// <see cref="DoryRockMath"/> / <see cref="WakeGrading"/>. Visual-only: this READS the oar state the
    /// controller already computed and never feeds anything back into the sim (rule 5); every amplitude is an
    /// owner tunable on <see cref="DoryOarLayer"/> (rule 6).</para>
    /// </summary>
    public static class DoryOarMath
    {
        /// <summary>Columns per heading row in an oar sheet: 8 stroke frames + resting + trailing.</summary>
        public const int ColumnsPerHeading = 10;

        /// <summary>Frames in the row-stroke cycle (cols 0..7). Forward plays 0→7; back-water plays 7→0.</summary>
        public const int StrokeColumns = 8;

        /// <summary>The oars are SHIPPED (stowed, at rest) — drawn when neither oar has worked for a while.</summary>
        public const int RestingColumn = 8;

        /// <summary>The oar TRAILS in the water — this side is idle while the boat is still being rowed.</summary>
        public const int TrailingColumn = 9;

        /// <summary>Baked rock cycle: degrees of wave phase per rock frame (8 frames → 45°). Art fact, not a
        /// tunable — it must match the sheet <see cref="DoryRockMath"/> selects frames from.</summary>
        public const float RockDegreesPerFrame = 45f;

        /// <summary>The rock-free/level pose sentinel: <see cref="DirectionalBoatSprite.RockFrame"/> is −1 when
        /// the hull draws its static facing (calm sea, or no rock grid) — the oars must then sit level too.</summary>
        public const int LevelRockFrame = -1;

        /// <summary>The row-major index into a heading×column oar sheet: <c>headingRow·columns + col</c>.
        /// Matches the <c>DoryOarPort</c>/<c>DoryOarStar</c> slice order (row = heading, col = frame).</summary>
        public static int OarGridIndex(int headingRow, int column, int columnsPerHeading)
            => headingRow * columnsPerHeading + column;

        /// <summary>True when an oar is being worked — its |state| clears the deadzone. Below it the oar is
        /// idle (trailing or shipped), never mid-stroke.</summary>
        public static bool IsWorking(float oarState, float deadzone)
            => Mathf.Abs(oarState) > Mathf.Max(0f, deadzone);

        /// <summary>
        /// The signed direction the stroke phase runs for an oar state: +1 for a forward pull, −1 for a
        /// back-water (the cycle in reverse), 0 when idle (the phase holds where it is, so resuming a stroke
        /// picks up mid-sweep instead of snapping).
        /// </summary>
        public static float StrokeDirection(float oarState, float deadzone)
            => IsWorking(oarState, deadzone) ? Mathf.Sign(oarState) : 0f;

        /// <summary>
        /// The stroke rate (frames/sec) for a pull of magnitude <paramref name="magnitude"/> (|oar state|,
        /// clamped to 0..1). <paramref name="effortInfluence"/> is how much a gentle pull slows the sweep:
        /// 0 = every stroke runs at <paramref name="baseFramesPerSecond"/>; 1 = the rate is fully proportional
        /// to effort (a half-hearted pull strokes at half speed). Never negative.
        /// </summary>
        public static float StrokeFramesPerSecond(float baseFramesPerSecond, float magnitude, float effortInfluence)
        {
            float k = Mathf.Clamp01(effortInfluence);
            float m = Mathf.Clamp01(Mathf.Abs(magnitude));
            return Mathf.Max(0f, baseFramesPerSecond) * ((1f - k) + k * m);
        }

        /// <summary>
        /// Advance an oar's own stroke phase by <paramref name="dt"/> seconds at
        /// <paramref name="framesPerSecond"/>, running <paramref name="direction"/> (+1 forward / −1 back-water
        /// / 0 hold), and wrap it into [0, <paramref name="strokeColumns"/>). Because the SAME accumulator runs
        /// both ways, a mid-stroke reversal continues from the current phase and sweeps back — never a snap to
        /// 0. A non-finite or out-of-range phase is re-seeded to 0 rather than propagating NaN.
        /// </summary>
        public static float AdvanceStrokePhase(float phase, float direction, float framesPerSecond,
                                               float dt, int strokeColumns)
        {
            int columns = Mathf.Max(1, strokeColumns);
            if (float.IsNaN(phase) || float.IsInfinity(phase)) phase = 0f;
            phase += direction * Mathf.Max(0f, framesPerSecond) * Mathf.Max(0f, dt);
            phase %= columns;
            if (phase < 0f) phase += columns;
            return phase;
        }

        /// <summary>The stroke column a phase is drawing: the floor of the phase, clamped into
        /// [0, <paramref name="strokeColumns"/>) so a phase landing exactly on the top of the cycle can never
        /// index past the last stroke frame.</summary>
        public static int StrokeColumnFromPhase(float phase, int strokeColumns)
        {
            int columns = Mathf.Max(1, strokeColumns);
            if (float.IsNaN(phase)) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(phase), 0, columns - 1);
        }

        /// <summary>
        /// The column ONE oar draws this frame, given its own state and the boat's:
        /// <list type="bullet">
        ///   <item>working (|state| &gt; deadzone) → its stroke frame, <see cref="StrokeColumnFromPhase"/>;</item>
        ///   <item>idle while the OTHER oar works → <see cref="TrailingColumn"/> (dragging in the water);</item>
        ///   <item>idle with both oars idle, but only just → still trailing, until…</item>
        ///   <item>…both oars have been idle for <paramref name="restGraceSeconds"/> → <see cref="RestingColumn"/>
        ///   (shipped). The grace stops a brief pause between strokes from stowing the oars.</item>
        /// </list>
        /// <paramref name="bothIdleSeconds"/> is how long BOTH oars have been idle (0 while either works).
        /// Pure — the caller owns the accumulators.
        /// </summary>
        public static int ColumnForOar(bool thisOarWorking, float phase, bool otherOarWorking,
                                       float bothIdleSeconds, float restGraceSeconds, int strokeColumns)
        {
            if (thisOarWorking) return StrokeColumnFromPhase(phase, strokeColumns);
            if (otherOarWorking) return TrailingColumn;
            return bothIdleSeconds >= Mathf.Max(0f, restGraceSeconds) ? RestingColumn : TrailingColumn;
        }

        /// <summary>The small screen-space pose that leans a rock-free oar cell onto a rock-BAKED hull
        /// frame. All fields are 0 at the level pose.</summary>
        public readonly struct OarRockPose
        {
            /// <summary>Additive z-rotation (degrees, +CCW) approximating the hull frame's baked roll.</summary>
            public readonly float RollDegrees;
            /// <summary>Screen-vertical (world +Y) offset in METRES: the baked heave, exactly, plus the small
            /// pitch approximation.</summary>
            public readonly float OffsetY;

            public OarRockPose(float rollDegrees, float offsetY)
            {
                RollDegrees = rollDegrees;
                OffsetY = offsetY;
            }

            /// <summary>The level pose — no lean, no lift (the calm hull, or the rock coupling switched off).</summary>
            public static OarRockPose Level => new OarRockPose(0f, 0f);
        }

        /// <summary>The baked rock cycle's phase (degrees) for rock frame <paramref name="rockFrame"/>:
        /// <c>a = i·(360/frameCount)</c> — 45° per frame on the shipped 8-frame sheet, so crest (frame 2) is
        /// 90° and trough (frame 6) is 270°, matching <see cref="DoryRockMath"/>.</summary>
        public static float RockPhaseDegrees(int rockFrame, int frameCount)
        {
            int count = Mathf.Max(1, frameCount);
            int frame = rockFrame % count;
            if (frame < 0) frame += count;
            return frame * (360f / count);
        }

        /// <summary>
        /// The pose that rides a rock-free oar overlay on the hull's currently-drawn rock frame. The baked
        /// cycle (art README) is <c>a = i·45°</c>, roll = 5°·sin(a), pitch = 3°·cos(a), heave = 1.6 px·sin(a):
        /// <list type="bullet">
        ///   <item><b>Heave is reproduced EXACTLY</b> — <c>(heavePixels/pixelsPerUnit)·sin(a)</c> metres of
        ///   screen-vertical lift, so the oars rise and fall with the gunwale to the pixel.</item>
        ///   <item><b>Roll</b> → <c>rollDegreesAmplitude·sin(a)</c> of additive z-rotation about the shared
        ///   waterline pivot (the tunable stand-in for the hull's baked lean).</item>
        ///   <item><b>Pitch</b> → <c>pitchOffsetMeters·cos(a)</c> of further screen-vertical offset — a ¾ view
        ///   reads a bow-up/bow-down tip mostly as vertical travel at this scale.</item>
        /// </list>
        /// <paramref name="strength"/> scales the whole thing (0 = oars sit level on a rocking hull; 1 = the
        /// tuned read). <paramref name="rockFrame"/> &lt; 0 (<see cref="LevelRockFrame"/> — the hull is drawing
        /// its static/calm facing) returns <see cref="OarRockPose.Level"/>, so a still hull never gets moving
        /// oars. Roll/pitch are approximations by design; the residual is a sub-pixel slide of the loom at the
        /// extremes of the cycle.
        /// </summary>
        public static OarRockPose RockPose(int rockFrame, int frameCount, float rollDegreesAmplitude,
                                           float pitchOffsetMeters, float heavePixels, float pixelsPerUnit,
                                           float strength)
        {
            if (rockFrame < 0 || strength <= 0f) return OarRockPose.Level;

            float a = RockPhaseDegrees(rockFrame, frameCount) * Mathf.Deg2Rad;
            float sin = Mathf.Sin(a);
            float cos = Mathf.Cos(a);

            float heaveMeters = heavePixels / Mathf.Max(1e-3f, pixelsPerUnit);   // guard: never divide by 0 PPU
            return new OarRockPose(
                rollDegreesAmplitude * sin * strength,
                (heaveMeters * sin + pitchOffsetMeters * cos) * strength);
        }
    }
}
