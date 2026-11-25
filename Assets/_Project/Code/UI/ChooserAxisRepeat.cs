using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The chooser's three navigation numbers</b>, read from <see cref="GameConfig"/> (rule 6), with the
    /// config's own defaults when none is wired. A struct of three floats: reading it each frame allocates
    /// nothing.
    /// </summary>
    public readonly struct ChooserAxisTuning
    {
        /// <summary>How far the axis must travel to count as a push, and fall back before the next.</summary>
        public readonly float Threshold;

        /// <summary>Seconds a held push waits before it runs on.</summary>
        public readonly float RepeatDelay;

        /// <summary>Seconds between steps once it runs on.</summary>
        public readonly float RepeatInterval;

        public ChooserAxisTuning(float threshold, float repeatDelay, float repeatInterval)
        {
            Threshold = threshold;
            RepeatDelay = repeatDelay;
            RepeatInterval = repeatInterval;
        }

        /// <summary>The config's numbers, or its defaults when no config is wired.</summary>
        public static ChooserAxisTuning Current
        {
            get
            {
                GameConfig c = GameServices.Config;
                return c != null
                    ? new ChooserAxisTuning(c.InteractChooserPushThreshold, c.InteractChooserRepeatDelaySeconds,
                                            c.InteractChooserRepeatIntervalSeconds)
                    : Default;
            }
        }

        public static ChooserAxisTuning Default => new ChooserAxisTuning(
            GameConfig.DefaultInteractChooserPushThreshold,
            GameConfig.DefaultInteractChooserRepeatDelaySeconds,
            GameConfig.DefaultInteractChooserRepeatIntervalSeconds);
    }

    /// <summary>
    /// <b>One step per push, and a held push that runs on</b> (ADR 0043 PR 2, the chooser's pad rows).
    ///
    /// <para><b>Without repeat it is <see cref="AxisLatch"/> exactly:</b> the axis must pass the threshold
    /// to step, and fall back inside it before it can step again. That is the keyboard's rule, and the
    /// view asks for it whenever the keyboard is the active device, so W/S and the arrows still move one
    /// row per press.</para>
    ///
    /// <para><b>With repeat</b> (the view allows it while the pad is the active device): a push held past
    /// <see cref="ChooserAxisTuning.RepeatDelay"/> steps again, then every
    /// <see cref="ChooserAxisTuning.RepeatInterval"/>, in the direction it was first pushed. At most one
    /// step a frame, so a hitch never bursts through the list. A push that was already held when the list
    /// opened (<see cref="Absorb"/>) never runs on: it must come back to neutral first.</para>
    ///
    /// <para>Pure: a float and a delta in, a step out. No device, no Unity lifecycle.</para>
    /// </summary>
    public sealed class ChooserAxisRepeat
    {
        private bool _latched;
        private int _direction;
        private float _untilNext;

        /// <summary>True while the axis is still pushed from the last step.</summary>
        public bool IsLatched => _latched;

        /// <summary>
        /// Feed this frame's axis. Returns +1, -1, or 0 when nothing steps this frame.
        /// </summary>
        /// <param name="axis">The vertical axis, +1 up.</param>
        /// <param name="deltaTime">Seconds since the last feed (unscaled: a held stick counts real time).</param>
        /// <param name="allowRepeat">Whether a held push may run on this frame.</param>
        /// <param name="tuning">The threshold and the two repeat timings.</param>
        public int Step(float axis, float deltaTime, bool allowRepeat, in ChooserAxisTuning tuning)
        {
            if (Mathf.Abs(axis) < tuning.Threshold)
            {
                _latched = false;
                _direction = 0;
                return 0;
            }

            if (!_latched)
            {
                _latched = true;
                _direction = axis > 0f ? 1 : -1;
                _untilNext = tuning.RepeatDelay;
                return _direction;
            }

            // Held. An absorbed push has no direction and never runs on; without repeat, the clock waits
            // at the full delay so a switch to the pad mid-hold starts the wait afresh.
            if (_direction == 0) return 0;
            if (!allowRepeat)
            {
                _untilNext = tuning.RepeatDelay;
                return 0;
            }

            _untilNext -= Mathf.Max(0f, deltaTime);
            if (_untilNext > 0f) return 0;
            _untilNext = tuning.RepeatInterval;
            return _direction;
        }

        /// <summary>Take the axis without stepping (the list is closed, or the press that opened it is
        /// still down). It still latches, so a push held through the open is not a fresh one, and it can
        /// never run on.</summary>
        public void Absorb(float axis, in ChooserAxisTuning tuning)
        {
            _latched = Mathf.Abs(axis) >= tuning.Threshold;
            _direction = 0;
            _untilNext = tuning.RepeatDelay;
        }

        /// <summary>Forget the push.</summary>
        public void Reset()
        {
            _latched = false;
            _direction = 0;
            _untilNext = 0f;
        }
    }
}
