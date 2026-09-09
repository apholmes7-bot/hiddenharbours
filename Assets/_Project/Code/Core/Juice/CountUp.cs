using System.Text;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A readout that LANDS (juice charter §4.1, §4.2): an integer counts from one value to another
    /// over a duration with an ease-out, and the frame it arrives is a landing — <see cref="Landed"/>
    /// is true for exactly one tick, and <see cref="PopScale"/> then bumps over a short pop and comes
    /// back to exactly 1. Owns no Unity, allocates nothing after construction: the digits are written
    /// into a caller-supplied, pre-sized <see cref="StringBuilder"/> by <see cref="WriteTo"/>, never
    /// through <c>int.ToString</c>.
    ///
    /// <para>Weights count in TENTHS (the notebook shows one decimal) — the same integer count-up
    /// with <see cref="WriteTo"/>'s <c>decimals = 1</c>. Coins count in whole coins.</para>
    /// </summary>
    public sealed class CountUp
    {
        private int _from;
        private int _to;
        private float _seconds;
        private float _elapsed;
        private bool _running;
        private float _sinceLanded = -1f;   // < 0 = never landed (no pop pending)

        /// <summary>The value to show right now.</summary>
        public int Value { get; private set; }

        /// <summary>True while the count is still moving.</summary>
        public bool Running => _running;

        /// <summary>True on the ONE tick that arrived at the target.</summary>
        public bool Landed { get; private set; }

        /// <summary>Seconds since the count landed, or a negative number if it never has.</summary>
        public float SinceLanded => _sinceLanded;

        /// <summary>Begin counting from <paramref name="from"/> to <paramref name="to"/> over
        /// <paramref name="seconds"/>. Zero seconds lands on the first tick.</summary>
        public void Start(int from, int to, float seconds)
        {
            _from = from;
            _to = to;
            _seconds = Mathf.Max(0f, seconds);
            _elapsed = 0f;
            _running = true;
            _sinceLanded = -1f;
            Landed = false;
            Value = from;
        }

        /// <summary>Show <paramref name="value"/> at rest, no count, no landing.</summary>
        public void Set(int value)
        {
            _running = false;
            Landed = false;
            _sinceLanded = -1f;
            Value = value;
            _from = _to = value;
        }

        /// <summary>
        /// Advance by <paramref name="dt"/> seconds (the caller's clock — feel timers use unscaled
        /// time). Returns true when <see cref="Value"/> changed this tick or the count landed.
        /// </summary>
        public bool Tick(float dt)
        {
            float d = Mathf.Max(0f, dt);
            Landed = false;
            if (_sinceLanded >= 0f) _sinceLanded += d;
            if (!_running) return false;

            _elapsed += d;
            int before = Value;
            if (_seconds <= 0f || _elapsed >= _seconds)
            {
                Value = _to;
                _running = false;
                Landed = true;
                _sinceLanded = 0f;
                return true;
            }
            Value = CountUpMath.ValueAt(_from, _to, _elapsed / _seconds);
            return Value != before;
        }

        /// <summary>
        /// The scale to draw the readout at: exactly 1 at rest and before landing; after the landing
        /// a bump to <paramref name="popScale"/> at the middle of <paramref name="popSeconds"/> and back
        /// to exactly 1 at its end (a half-sine). Zero pop seconds = no pop.
        /// </summary>
        public float PopScale(float popScale, float popSeconds)
            => CountUpMath.Pop(_sinceLanded, popScale, popSeconds);

        /// <summary>True while the pop is still playing (the caller keeps ticking for it).</summary>
        public bool Popping(float popSeconds) => _sinceLanded >= 0f && _sinceLanded < popSeconds;

        /// <summary>Write <see cref="Value"/> into <paramref name="sb"/> (cleared first) as digits, with
        /// <paramref name="decimals"/> of them behind a point (1 for tenths). No allocation.</summary>
        public void WriteTo(StringBuilder sb, int decimals = 0) => CountUpMath.WriteFixed(sb, Value, decimals);
    }

    /// <summary>The pure curves behind <see cref="CountUp"/> — EditMode-tested on their own.</summary>
    public static class CountUpMath
    {
        /// <summary>The value at <paramref name="t01"/> of the way from <paramref name="from"/> to
        /// <paramref name="to"/>: an ease-out (fast first, settling on the last digits), rounded, and
        /// monotone toward the target.</summary>
        public static int ValueAt(int from, int to, float t01)
        {
            float t = Mathf.Clamp01(t01);
            float eased = 1f - (1f - t) * (1f - t);
            float v = from + (to - from) * eased;
            int rounded = Mathf.RoundToInt(v);
            // Never overshoot and never show the target early (the landing is the target's frame).
            if (t < 1f)
            {
                if (to > from) rounded = Mathf.Clamp(rounded, from, to - 1);
                else if (to < from) rounded = Mathf.Clamp(rounded, to + 1, from);
                else rounded = from;
            }
            return rounded;
        }

        /// <summary>A half-sine bump: 1 outside [0, popSeconds), peak <paramref name="popScale"/> at the middle.</summary>
        public static float Pop(float sinceLanded, float popScale, float popSeconds)
        {
            if (sinceLanded < 0f || popSeconds <= 0f || sinceLanded >= popSeconds) return 1f;
            float u = sinceLanded / popSeconds;
            return 1f + (popScale - 1f) * Mathf.Sin(u * Mathf.PI);
        }

        /// <summary>Digits of <paramref name="value"/> into <paramref name="sb"/> (cleared first), the last
        /// <paramref name="decimals"/> behind a point, zero-padded so 3 tenths reads "0.3". No allocation:
        /// <c>StringBuilder.Append(char)</c> only.</summary>
        public static void WriteFixed(StringBuilder sb, int value, int decimals)
        {
            sb.Clear();
            bool negative = value < 0;
            long v = negative ? -(long)value : value;
            int d = Mathf.Max(0, decimals);

            // Count the digits, then emit high to low with a point before the last `decimals` of them.
            int digits = 1;
            for (long p = v; p >= 10; p /= 10) digits++;
            if (digits < d + 1) digits = d + 1;   // at least one digit ahead of the point

            if (negative) sb.Append('-');
            long div = 1;
            for (int i = 1; i < digits; i++) div *= 10;
            for (int i = digits - 1; i >= 0; i--)
            {
                if (d > 0 && i == d - 1) sb.Append('.');
                sb.Append((char)('0' + (int)(v / div % 10)));
                div /= 10;
            }
        }
    }
}
