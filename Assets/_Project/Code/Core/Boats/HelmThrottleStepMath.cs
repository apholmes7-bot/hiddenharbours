namespace HiddenHarbours.Core
{
    /// <summary>
    /// Auto-repeat state for one held throttle key/button (ahead or astern) — how long it has been
    /// held and how many repeats have already been emitted. Transient input state (never saved,
    /// rule 5); a plain struct so the input layer can keep two of them in fields with zero
    /// allocation.
    /// </summary>
    public struct HeldRepeatState
    {
        public float HeldSeconds;
        public int Emitted;
    }

    /// <summary>
    /// The pure maths of the <b>stepped-and-held keyed throttle</b> (owner directive 2026-08-03: a
    /// key can't hold an analog position, so each press bumps a detent and the drive STAYS there).
    /// Consumes <c>HelmThrottleSettings</c> values as parameters (no magic numbers, rule 6) and the
    /// detent arithmetic of <see cref="ThrottleDetentModel"/>; the input layer
    /// (<c>DevBoatInput</c>) and the helm relay (<c>HelmControlRelay</c>) both step through here so
    /// keyboard, gamepad and mouse can never disagree about what a detent is.
    ///
    /// <para>Pure + static + engine-free (<see cref="System.Math"/> only) so every rule is EditMode-
    /// testable with explicit <c>dt</c>s — no frame-time or input-device dependence (PlayMode frame
    /// count is not time).</para>
    /// </summary>
    public static class HelmThrottleStepMath
    {
        /// <summary>
        /// Advance one held key's repeat state and return how many detent steps it contributes this
        /// tick: <c>1</c> on the press EDGE, then — while <paramref name="held"/> — further steps
        /// once <paramref name="repeatDelaySec"/> has elapsed, at <paramref name="repeatPerSec"/>
        /// (the first repeat fires right at the delay). <paramref name="repeatPerSec"/> ≤ 0 = edge
        /// only (the deliberate binnacle feel). Releasing resets the state.
        /// </summary>
        public static int TickRepeat(ref HeldRepeatState state, bool pressedEdge, bool held,
                                     float dt, float repeatDelaySec, float repeatPerSec)
        {
            if (pressedEdge)
            {
                state.HeldSeconds = 0f;
                state.Emitted = 0;
                return 1;
            }
            if (!held)
            {
                state.HeldSeconds = 0f;
                state.Emitted = 0;
                return 0;
            }
            state.HeldSeconds += dt < 0f ? 0f : dt;
            if (repeatPerSec <= 0f || state.HeldSeconds < repeatDelaySec) return 0;
            int total = (int)((state.HeldSeconds - repeatDelaySec) * repeatPerSec) + 1;
            int emit = total - state.Emitted;
            if (emit < 0) emit = 0;
            state.Emitted = total;
            return emit;
        }

        /// <summary>
        /// One detent step from an ARBITRARY drive (the lever may sit off-notch after a mouse drag):
        /// stepping ahead lands on the next detent ABOVE the current drive, stepping astern on the
        /// next detent BELOW — so a lever left at 0.45 of 4 notches steps to 0.5, not past it.
        /// On-notch drives step exactly one detent. Clamps at full travel either end. Pure + static.
        /// </summary>
        public static float StepOnce(float drive, int direction, int aheadNotches, int asternNotches)
        {
            if (direction == 0) return drive;
            aheadNotches = aheadNotches < 1 ? 1 : aheadNotches;
            asternNotches = asternNotches < 1 ? 1 : asternNotches;
            if (drive > 1f) drive = 1f;
            else if (drive < -1f) drive = -1f;

            // Position in the CURRENT side's notch units (ahead notches above 0, astern below).
            double units = drive >= 0f ? (double)drive * aheadNotches : (double)drive * asternNotches;
            const double eps = 1e-4;   // an on-notch drive steps a FULL detent, never re-lands on itself
            int notch = direction > 0
                ? (int)System.Math.Floor(units + eps) + 1
                : (int)System.Math.Ceiling(units - eps) - 1;
            notch = ThrottleDetentModel.Clamp(notch, asternNotches, aheadNotches);
            return ThrottleDetentModel.DriveFor(notch, aheadNotches, asternNotches);
        }

        /// <summary>Apply a NET step count (ahead steps minus astern steps this tick) one detent at a
        /// time, so a multi-step tick (auto-repeat catch-up) still clamps + crosses neutral exactly
        /// like single presses would.</summary>
        public static float StepMany(float drive, int steps, int aheadNotches, int asternNotches)
        {
            int dir = steps > 0 ? 1 : -1;
            for (int i = steps > 0 ? steps : -steps; i > 0; i--)
                drive = StepOnce(drive, dir, aheadNotches, asternNotches);
            return drive;
        }

        /// <summary>The mouse-release rule: a lever left inside the neutral snap window (drive units
        /// around 0, data) clicks into the neutral detent; anywhere else it HOLDS where released.</summary>
        public static float ApplyNeutralSnap(float drive, float window01)
            => (drive < 0f ? -drive : drive) <= window01 ? 0f : drive;
    }
}
