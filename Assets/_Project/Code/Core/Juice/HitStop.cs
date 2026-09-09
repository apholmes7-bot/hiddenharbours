using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The landing frame's hit-stop as a POCO (juice charter §4.1): a dip of the world's time scale to
    /// <c>scale</c> for <c>seconds</c> of UNSCALED time, then an exact return to 1. Owns no Unity —
    /// the host (<c>HitStopHost</c>, App) writes <c>Time.timeScale</c> from <see cref="Tick"/> and
    /// restores what it found. Feel timers everywhere run on unscaled time, so the stop freezes the
    /// world (the clock, the sim, the sprites) and nothing else.
    ///
    /// <para>The world clock (<c>GameClock</c>) integrates <c>Time.deltaTime × TimeScale</c>, so a
    /// hit-stop is a 0.1 s pause of the world — the charter says that is fine. Sim steps that
    /// integrate <c>Time.deltaTime</c> directly pause with it (they read the same scaled delta).</para>
    /// </summary>
    public sealed class HitStop
    {
        private float _remaining;
        private float _scale = 1f;
        private bool _active;

        /// <summary>A stop is live: the scale to apply is <see cref="Scale"/>, not 1.</summary>
        public bool Active => _active;

        /// <summary>The time scale the world should run at right now: the dip while live, exactly 1 at rest.</summary>
        public float Scale => _active ? _scale : 1f;

        /// <summary>Unscaled seconds of stop still to run (0 at rest).</summary>
        public float Remaining => _active ? _remaining : 0f;

        /// <summary>
        /// Start a stop of <paramref name="seconds"/> at <paramref name="scale"/> (clamped 0..1). A
        /// zero or negative duration is a no-op. A second trigger on a live stop keeps the LONGER
        /// remaining time and the DEEPER dip — two landings in one frame do not shorten each other.
        /// </summary>
        public void Trigger(float scale, float seconds)
        {
            if (seconds <= 0f) return;
            float s = Mathf.Clamp01(scale);
            if (_active)
            {
                _remaining = Mathf.Max(_remaining, seconds);
                _scale = Mathf.Min(_scale, s);
                return;
            }
            _active = true;
            _remaining = seconds;
            _scale = s;
        }

        /// <summary>
        /// Advance by <paramref name="unscaledDt"/> seconds and return the scale to apply for this
        /// frame. The frame the stop expires on returns exactly 1 and goes to rest — never a partial
        /// scale on the way out (a knob is not a time machine: the stop is a pure function of elapsed
        /// unscaled time).
        /// </summary>
        public float Tick(float unscaledDt)
        {
            if (!_active) return 1f;
            _remaining -= Mathf.Max(0f, unscaledDt);
            if (_remaining <= 0f)
            {
                _active = false;
                _remaining = 0f;
                return 1f;
            }
            return _scale;
        }

        public void Reset()
        {
            _active = false;
            _remaining = 0f;
            _scale = 1f;
        }
    }
}
