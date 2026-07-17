using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The skiff's REMOTE-STEER OUTBOARD: turns the helm (wheel/rudder) state into a column of the baked motor
    /// sheet, decides the per-heading draw order of its two layers against the hull, and places the twin
    /// engines' clamp offsets for the sport skiff.
    ///
    /// <para><b>The sheets (art README).</b> <c>SkiffMotorUpper-*</c>/<c>SkiffMotorLower-*</c> are 9 cols × 8
    /// heading rows (index = <c>heading×9 + col</c>, rows N..NW clockwise — the same order as the hull sheets).
    /// <b>There is no tiller</b>: steering is remote from the console wheel and the whole engine swivels on its
    /// clamp, so the column is a direct read of the helm — col 0 = −30° (full port), col 4 = dead ahead, col 8
    /// = +30° (full starboard), 7.5° steps. The rig's own mapping is <c>angle(f) = −30 + 60·f/8</c>, which
    /// <see cref="SteerDegreesForColumn"/> reproduces (generalised over the column count).</para>
    ///
    /// <para><b>Draw order (art README, verbatim).</b> "UPPER always composites OVER the hull. LOWER goes UNDER
    /// the hull for the stern-away headings SE, S, SW (indices 3,4,5), and over it everywhere else." So:
    /// lower → hull → upper for SE/S/SW; hull → lower → upper otherwise. The rig confirms the indices with
    /// <c>MOTOR.behind = [3,4,5]</c>. <see cref="LowerGoesUnderHull"/> is that decision, and it must be
    /// re-evaluated <b>every heading change</b> — see <see cref="SortingOrder"/>.</para>
    ///
    /// <para><b>Twin fit (sport only).</b> The bake is orthographic, so a lateral clamp shift is an EXACT
    /// per-heading screen offset — the same single-engine sheet blitted twice. The rig's
    /// <c>mountOffset(dir,mx,elev)</c> works in image pixels (y-DOWN) at S = 32 px/m:
    /// <c>dx = mx·cos(θ)·S, dy = −mx·sin(θ)·sin(elev)·S</c>. Our sheets import at PPU 32, so the px→unit divide
    /// cancels S exactly, and image-y-down flips to Unity-y-up — leaving
    /// <c>offset = (mx·cos(θ), mx·sin(θ)·sin(elev))</c> in METRES. See <see cref="MountOffset"/>. Verified
    /// against <c>_preview-sport-twin.png</c>: at N the engines separate purely horizontally, at E purely
    /// vertically and foreshortened by sin(40°).</para>
    ///
    /// <para><b>Rock.</b> The motor sheets carry NO rock dimension (they are baked level) while the hull's rock
    /// is baked into its frames, so the motor must be POSED onto the wave exactly like the dory's oars —
    /// reuse <see cref="DoryOarMath.RockPose"/> rather than re-deriving it (its
    /// <c>roll = rollAmp·sin(a)</c>, <c>offsetY = heave·sin(a) + pitch·cos(a)</c> at <c>a = frame·45°</c> is
    /// algebraically identical to these rigs' <c>rockMotion(i)</c>). Do NOT double-rock.</para>
    ///
    /// <para><b>Rules.</b> Pure, static, allocation-free, deterministic — the same discipline (and EditMode
    /// coverage) as <see cref="DoryOarMath"/> / <see cref="WakeGrading"/>: every entry point defensively clamps
    /// and never allocates. Visual-only: it READS helm state and never feeds the sim (rule 5); every rate,
    /// deadzone and amplitude is an owner tunable on <see cref="SkiffMotorLayer"/> (rule 6). The RAISED/TILT
    /// pose (tilt 0..40) is not on the sheets and is deliberately absent here.</para>
    /// </summary>
    public static class SkiffMotorMath
    {
        /// <summary>Steer columns per heading row in a motor sheet: col 0 = full port … col 4 = dead ahead …
        /// col 8 = full starboard. Art fact (the rig's <c>MOTOR.steerFrames</c>), not a tunable.</summary>
        public const int SteerColumns = 9;

        /// <summary>Steer authority at the sheet's extremes, in degrees either side of dead ahead (the rig's
        /// <c>MOTOR.maxSteer</c>). The 9 columns therefore step 7.5° apart.</summary>
        public const float MaxSteerDegrees = 30f;

        /// <summary>Heading rows in a motor sheet (N..NW clockwise) — must match the hull's facing count.</summary>
        public const int HeadingCount = 8;

        /// <summary>Elevation of the fixed ¾ iso camera the sheets were baked at, in degrees — the
        /// foreshortening term in <see cref="MountOffset"/>. Art fact (the rigs' <c>DEFAULT_ELEV</c>).</summary>
        public const float BakeElevationDegrees = 40f;

        /// <summary>Baked rock cycle: degrees of wave phase per rock frame (8 frames → 45°). Art fact — it must
        /// match the sheet the hull selects rock frames from.</summary>
        public const float RockDegreesPerFrame = 45f;

        /// <summary>The rock-free/level pose sentinel: <see cref="DirectionalBoatSprite.RockFrame"/> is −1 when
        /// the hull draws its static facing (calm sea, or no rock grid) — the motor must then sit level too.</summary>
        public const int LevelRockFrame = DoryOarMath.LevelRockFrame;

        /// <summary>The dead-ahead steer column for a sheet of <paramref name="steerColumns"/> columns — the
        /// centred engine (col 4 on the shipped 9-column sheets). <b>An unmanned helm draws this</b>: a dropped
        /// helm must never leave the motor hard-over.</summary>
        public static int CenterColumn(int steerColumns) => Mathf.Max(1, steerColumns) / 2;

        /// <summary>The steer angle (degrees, − = port / + = starboard) a sheet column is drawn at. Reproduces
        /// the rig's <c>angle(f) = −30 + 60·f/8</c>, generalised: the columns span
        /// [−<paramref name="maxSteerDegrees"/>, +<paramref name="maxSteerDegrees"/>] evenly, so col 0 is full
        /// port, the middle column is dead ahead and the last is full starboard. A single-column sheet is dead
        /// ahead. Out-of-range columns clamp rather than extrapolate.</summary>
        public static float SteerDegreesForColumn(int column, int steerColumns, float maxSteerDegrees)
        {
            int columns = Mathf.Max(1, steerColumns);
            if (columns == 1) return 0f;
            int col = Mathf.Clamp(column, 0, columns - 1);
            return -maxSteerDegrees + (2f * maxSteerDegrees * col) / (columns - 1);
        }

        /// <summary>The inverse of <see cref="SteerDegreesForColumn"/>: the column that best draws a steer
        /// angle. Rounds to the NEAREST column and clamps to the sheet, so a hard-over beyond the sheet's
        /// authority pins at the extreme instead of indexing off the end.</summary>
        public static int ColumnForSteerDegrees(float steerDegrees, int steerColumns, float maxSteerDegrees)
        {
            int columns = Mathf.Max(1, steerColumns);
            if (columns == 1 || maxSteerDegrees <= 0f) return CenterColumn(columns);
            if (float.IsNaN(steerDegrees)) return CenterColumn(columns);

            float t = Mathf.Clamp((steerDegrees + maxSteerDegrees) / (2f * maxSteerDegrees), 0f, 1f);
            return Mathf.Clamp(Mathf.RoundToInt(t * (columns - 1)), 0, columns - 1);
        }

        /// <summary>True when the helm is being HELD off centre — its |value| clears the deadzone. Below it the
        /// wheel reads as centred, so a resting stick never trickles the engine off dead ahead.</summary>
        public static bool IsSteering(float helm, float deadzone)
            => Mathf.Abs(helm) > Mathf.Max(0f, deadzone);

        /// <summary>
        /// The steer column a helm value (−1 full port … 0 centred … +1 full starboard) TARGETS. Inside the
        /// deadzone, or for a non-finite helm, this is dead ahead. The helm is clamped to ±1 first, so an
        /// over-driven input pins at the sheet's extreme.
        /// </summary>
        public static int TargetColumnForHelm(float helm, float deadzone, int steerColumns, float maxSteerDegrees)
        {
            int columns = Mathf.Max(1, steerColumns);
            if (float.IsNaN(helm) || float.IsInfinity(helm)) return CenterColumn(columns);
            if (!IsSteering(helm, deadzone)) return CenterColumn(columns);

            float h = Mathf.Clamp(helm, -1f, 1f);
            return ColumnForSteerDegrees(h * maxSteerDegrees, columns, maxSteerDegrees);
        }

        /// <summary>
        /// Step the DRAWN steer position toward <paramref name="targetColumn"/> at no more than
        /// <paramref name="columnsPerSecond"/> (the README's "step columns at ~8 fps"), so the engine swivels on
        /// its clamp instead of teleporting when the wheel is thrown over. Returns the new continuous position;
        /// <see cref="ColumnFromPosition"/> turns it into the column to draw. A non-finite position re-seeds to
        /// the target rather than propagating NaN; a zero/negative rate snaps straight to the target.
        /// </summary>
        public static float StepTowardColumn(float position, int targetColumn, float columnsPerSecond, float dt)
        {
            if (float.IsNaN(position) || float.IsInfinity(position)) return targetColumn;

            float rate = Mathf.Max(0f, columnsPerSecond);
            float step = rate * Mathf.Max(0f, dt);
            if (rate <= 0f || step <= 0f) return rate <= 0f ? targetColumn : position;

            return Mathf.MoveTowards(position, targetColumn, step);
        }

        /// <summary>The column a continuous steer position draws: the NEAREST column (the position is a real
        /// place between two swivel poses, so rounding — not flooring — picks the truthful picture), clamped
        /// into the sheet.</summary>
        public static int ColumnFromPosition(float position, int steerColumns)
        {
            int columns = Mathf.Max(1, steerColumns);
            if (float.IsNaN(position)) return CenterColumn(columns);
            return Mathf.Clamp(Mathf.RoundToInt(position), 0, columns - 1);
        }

        /// <summary>The row-major index into a heading×column motor sheet: <c>headingRow·columns + col</c>.
        /// Matches the slice order of <c>SkiffMotorUpper-*</c>/<c>SkiffMotorLower-*</c> (row = heading, col =
        /// steer), which slice row-major from the top-left as <c>&lt;Stem&gt;_&lt;index&gt;</c>.</summary>
        public static int MotorGridIndex(int headingRow, int column, int columnsPerHeading)
            => headingRow * columnsPerHeading + column;

        /// <summary>
        /// Does the LOWER layer (leg + plate + skeg + prop) draw UNDER the hull at this heading? True for the
        /// STERN-AWAY headings SE, S, SW — rows 3, 4, 5 on the shipped 8-heading sheets (the rig's
        /// <c>MOTOR.behind</c>) — where the transom is on the far side of the hull and the leg is hidden behind
        /// it. False everywhere else, where the leg reads in front of the hull. The UPPER layer is never under
        /// the hull, at any heading.
        ///
        /// <para>Expressed as a fraction of the heading circle (rows 3..5 of 8 → the arc from 135° to 225°, i.e.
        /// the stern-away half-quadrant band around due South) so a 16-way sheet would drop in unchanged.</para>
        /// </summary>
        public static bool LowerGoesUnderHull(int headingRow, int headingCount)
        {
            int count = Mathf.Max(1, headingCount);
            int row = headingRow % count;
            if (row < 0) row += count;

            // Rows 3,4,5 of 8 == the band [3/8, 5/8] of the circle, inclusive — S ± one facing step.
            float t = row / (float)count;
            return t >= 3f / 8f - 1e-4f && t <= 5f / 8f + 1e-4f;
        }

        /// <summary>Which layer of the outboard a renderer draws — the two parts the art ships.</summary>
        public enum MotorPart
        {
            /// <summary>Leg + cavitation plate + skeg + prop. Under the hull for SE/S/SW, over it elsewhere.</summary>
            Lower = 0,
            /// <summary>Clamp bracket + cowl. ALWAYS over the hull, at every heading.</summary>
            Upper = 1,
        }

        /// <summary>
        /// The sorting order a motor renderer takes on the HULL VISUAL's sorting layer, given the hull visual's
        /// own order. Realises the README's per-heading order — <b>lower → hull → upper</b> for the stern-away
        /// headings, <b>hull → lower → upper</b> otherwise — and, within each layer, draws the FAR engine first
        /// (below the near one) for the twin fit:
        /// <list type="bullet">
        ///   <item>lower, under the hull (SE/S/SW) → hull−2 (far), hull−1 (near);</item>
        ///   <item>lower, over the hull → hull+1 (far), hull+2 (near);</item>
        ///   <item>upper (always over) → hull+3 (far), hull+4 (near).</item>
        /// </list>
        /// Upper always outranks lower, and lower always outranks (or is outranked by) the hull per the band —
        /// so the stack is correct at every heading without the caller reasoning about it. Re-evaluate on every
        /// heading change: the lower band FLIPS across the stern-away arc.
        /// </summary>
        public static int SortingOrder(int hullSortingOrder, MotorPart part, int headingRow, int headingCount,
                                       bool isFarEngine)
        {
            int near = isFarEngine ? 0 : 1;

            if (part == MotorPart.Upper) return hullSortingOrder + 3 + near;
            return LowerGoesUnderHull(headingRow, headingCount)
                ? hullSortingOrder - 2 + near
                : hullSortingOrder + 1 + near;
        }

        /// <summary>
        /// The EXACT screen offset (in METRES, Unity axes: +X right, +Y up) of an engine clamped
        /// <paramref name="lateralMetres"/> off the centreline at heading <paramref name="headingRow"/>.
        ///
        /// <para>Reduced from the rig's <c>mountOffset(dir,mx,elev) = { dx: mx·cos(θ)·S, dy: −mx·sin(θ)·sin(e)·S }</c>
        /// (image pixels, y-DOWN, S = 32 px/m). Our sheets import at PPU 32, so px→units divides by exactly the
        /// same 32 and S cancels; the y-down→y-up flip kills the leading minus. Hence
        /// <c>(mx·cos(θ), mx·sin(θ)·sin(elev))</c>, with <c>θ = row·2π/count</c>.</para>
        ///
        /// <para>Because the bake is orthographic this is exact, not an approximation — which is what lets the
        /// twin fit reuse the single-engine sheets with no extra art. Sanity: at N (row 0, θ = 0) the engines
        /// separate purely horizontally (no vertical component); at E (row 2, θ = 90°) purely vertically and
        /// foreshortened by sin(40°) ≈ 0.643. Both confirmed against <c>_preview-sport-twin.png</c>.</para>
        /// </summary>
        public static Vector2 MountOffset(int headingRow, float lateralMetres, int headingCount,
                                          float elevationDegrees = BakeElevationDegrees)
        {
            int count = Mathf.Max(1, headingCount);
            int row = headingRow % count;
            if (row < 0) row += count;

            float theta = row * (2f * Mathf.PI / count);
            float sinElev = Mathf.Sin(elevationDegrees * Mathf.Deg2Rad);

            return new Vector2(
                lateralMetres * Mathf.Cos(theta),
                lateralMetres * Mathf.Sin(theta) * sinElev);
        }

        /// <summary>
        /// The depth key of a clamp offset at a heading — <c>mx·sin(θ)</c>, the term the rig's own z-buffer
        /// sorts on. <b>LARGER = FARTHER from the camera = drawn FIRST</b> (and, equivalently, higher on screen:
        /// it is the same quantity <see cref="MountOffset"/> foreshortens into +Y). Use it to order the twin
        /// engines within a layer. At N and S (θ = 0, π) both engines share a depth of 0 — they are exactly
        /// abeam of each other, do not overlap, and the order is a free choice.
        /// </summary>
        public static float MountScreenDepth(int headingRow, float lateralMetres, int headingCount)
        {
            int count = Mathf.Max(1, headingCount);
            int row = headingRow % count;
            if (row < 0) row += count;

            return lateralMetres * Mathf.Sin(row * (2f * Mathf.PI / count));
        }

        /// <summary>
        /// Is the engine clamped at <paramref name="lateralMetres"/> the FAR one of a twin pair (the other
        /// being at <paramref name="otherLateralMetres"/>) at this heading — i.e. the one to draw FIRST?
        /// Compares <see cref="MountScreenDepth"/>. When the two share a depth (N/S, where the engines sit
        /// abeam and cannot overlap) the port engine is deemed far, purely so the answer is stable and
        /// deterministic rather than order-dependent.
        /// </summary>
        public static bool IsFarEngine(float lateralMetres, float otherLateralMetres, int headingRow, int headingCount)
        {
            float mine = MountScreenDepth(headingRow, lateralMetres, headingCount);
            float theirs = MountScreenDepth(headingRow, otherLateralMetres, headingCount);

            if (Mathf.Approximately(mine, theirs)) return lateralMetres < otherLateralMetres;
            return mine > theirs;
        }
    }
}
