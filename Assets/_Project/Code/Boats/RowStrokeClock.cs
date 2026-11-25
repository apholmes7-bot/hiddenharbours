using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>The stroke clock's tunables: the shared defaults live in <see cref="DoryOarMath"/>, the
    /// live values on <see cref="BoatController"/>'s inspector.</summary>
    public readonly struct RowStrokeTempo
    {
        /// <summary>Frames/sec the 8-frame stroke cycle plays at a full-effort pull.</summary>
        public readonly float FramesPerSecond;
        /// <summary>|oar state| at or below which the oar is idle and its phase holds.</summary>
        public readonly float Deadzone;
        /// <summary>How much a gentle pull slows the sweep (0 = never, 1 = fully proportional).</summary>
        public readonly float EffortInfluence;
        /// <summary>Columns/sec the second oar falls into step with the leading one (0 = never).</summary>
        public readonly float PhaseLockColumnsPerSecond;

        public RowStrokeTempo(float framesPerSecond, float deadzone, float effortInfluence, float phaseLockColumnsPerSecond)
        {
            FramesPerSecond = framesPerSecond;
            Deadzone = deadzone;
            EffortInfluence = effortInfluence;
            PhaseLockColumnsPerSecond = phaseLockColumnsPerSecond;
        }

        /// <summary>The shared defaults: the tempo both oar layers always rowed at.</summary>
        public static RowStrokeTempo Default => new RowStrokeTempo(DoryOarMath.DefaultStrokeFramesPerSecond,
            DoryOarMath.DefaultOarDeadzone, DoryOarMath.DefaultEffortInfluence,
            DoryOarMath.DefaultPhaseLockColumnsPerSecond);
    }

    /// <summary>
    /// <b>The one clock a rowed hull's stroke runs on.</b> Each oar's raw phase, in stroke columns
    /// (<see cref="DoryOarMath.StrokeColumns"/> per stroke), advanced on the PHYSICS step by
    /// <see cref="BoatController"/> from the real per-oar state, so the surge of the hull and the oars
    /// drawn over it cannot drift apart: the mesh oars read this clock (interpolated to the frame the
    /// way the body is), the rower's body follows the oars, and the hull's thrust reads the same phase.
    ///
    /// <para>The arithmetic is <see cref="DoryOarMath"/>'s, unchanged: the same signed accumulator
    /// (a back-water runs the cycle in reverse), the same effort curve, the same phase lock pulling the
    /// lighter-pulled oar into step. A <see cref="HiddenHarbours.Core.RowingStyle"/> scales only the
    /// tempo here; its timing warp is applied by whoever reads the phase.</para>
    ///
    /// <para>Deterministic from the inputs and the fixed step; never saved (a loaded boat starts with
    /// both phases at 0, as the oar layers always did). Allocation-free.</para>
    /// </summary>
    public struct RowStrokeClock
    {
        /// <summary>The port oar's raw phase in [0, StrokeColumns).</summary>
        public float PortPhase;
        /// <summary>The starboard oar's raw phase in [0, StrokeColumns).</summary>
        public float StarboardPhase;
        /// <summary>The port oar's signed rate on the last step, columns per second.</summary>
        public float PortRate;
        /// <summary>The starboard oar's signed rate on the last step, columns per second.</summary>
        public float StarboardRate;

        /// <summary>
        /// Advance both oars by <paramref name="dt"/> seconds for oar states
        /// <paramref name="portState"/> / <paramref name="starState"/> (-1..1), at
        /// <paramref name="tempoScale"/> times the tempo's frames per second.
        /// </summary>
        public void Step(float portState, float starState, float dt, in RowStrokeTempo tempo, float tempoScale)
        {
            int columns = DoryOarMath.StrokeColumns;
            float fps = tempo.FramesPerSecond * Mathf.Max(0f, tempoScale);
            float portDir = DoryOarMath.StrokeDirection(portState, tempo.Deadzone);
            float starDir = DoryOarMath.StrokeDirection(starState, tempo.Deadzone);
            float portFps = DoryOarMath.StrokeFramesPerSecond(fps, portState, tempo.EffortInfluence);
            float starFps = DoryOarMath.StrokeFramesPerSecond(fps, starState, tempo.EffortInfluence);

            PortRate = portDir * portFps;
            StarboardRate = starDir * starFps;
            PortPhase = DoryOarMath.AdvanceStrokePhase(PortPhase, portDir, portFps, dt, columns);
            StarboardPhase = DoryOarMath.AdvanceStrokePhase(StarboardPhase, starDir, starFps, dt, columns);

            // One rower, two hands: pulled the same way, the oars stroke together. The one pulled harder
            // leads (port on a tie) and the other falls into step.
            if (portDir != 0f && portDir == starDir && tempo.PhaseLockColumnsPerSecond > 0f)
            {
                float step = tempo.PhaseLockColumnsPerSecond * Mathf.Max(0f, dt);
                if (Mathf.Abs(portState) >= Mathf.Abs(starState))
                    StarboardPhase = DoryOarMath.LockPhaseToward(StarboardPhase, PortPhase, step, columns);
                else
                    PortPhase = DoryOarMath.LockPhaseToward(PortPhase, StarboardPhase, step, columns);
            }
        }

        /// <summary>The port phase <paramref name="seconds"/> from the last step at its last rate
        /// (negative = earlier), wrapped. What a frame between two physics steps draws.</summary>
        public float PortAt(float seconds) => At(PortPhase, PortRate, seconds);

        /// <summary>The starboard phase <paramref name="seconds"/> from the last step at its last rate.</summary>
        public float StarboardAt(float seconds) => At(StarboardPhase, StarboardRate, seconds);

        /// <summary>
        /// How far behind the last physics step a frame is drawn: an interpolated body is drawn
        /// <c>fixedDt − (time − fixedTime)</c> behind its simulated pose, so oars read at that offset sit
        /// on the hull exactly. Clamped into [0, fixedDt].
        /// </summary>
        public static float InterpolationLag(float time, float fixedTime, float fixedDeltaTime)
        {
            float step = Mathf.Max(0f, fixedDeltaTime);
            return step - Mathf.Clamp(time - fixedTime, 0f, step);
        }

        /// <summary>The raw stroke phase (0 = catch, 0.5 = release) of an oar at
        /// <paramref name="phaseColumns"/>.</summary>
        public static float StrokePhaseOfColumns(float phaseColumns) =>
            HiddenHarbours.Core.OarStrokeMath.PhaseFromOarTurns(phaseColumns / DoryOarMath.StrokeColumns,
                                                                DoryOarMeshPose.RowCatchTurns);

        private static float At(float phase, float rate, float seconds)
        {
            int columns = DoryOarMath.StrokeColumns;
            float p = Mathf.Repeat(phase + rate * seconds, columns);
            return p >= columns ? 0f : p;
        }
    }
}
