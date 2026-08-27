using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One step per push.</b> A move axis, latched: it must fall back inside the dead zone before it
    /// can step again.
    ///
    /// <para><b>Why this is a rule and not four lines copied twice.</b> Without the latch a held key rips
    /// through four rows in four frames, and a player who meant to look at the second row is looking at
    /// the last one. The dialogue option picker earned that rule first; the seller's wares book needs
    /// exactly the same one, and a second transcription of it is a second place for the dead zone to
    /// drift. It lives in Core because World and Economy both read it and neither may name the
    /// other.</para>
    ///
    /// <para><b>Pure — no input device, no Unity lifecycle.</b> It takes a plain float, so it does not
    /// care whether that came from W/S, the arrows, or a stick, and "a held key cannot step twice" is an
    /// EditMode assertion rather than something to watch for in play.</para>
    /// </summary>
    public sealed class AxisLatch
    {
        /// <summary>How far the axis must travel before it counts as a push, and how far back it must
        /// fall before the next one counts. Input plumbing, not owner feel: big enough that stick drift
        /// cannot step a row, small enough that a deliberate tap always does.</summary>
        public const float Threshold = 0.5f;

        private bool _latched;

        /// <summary>True while the axis is still pushed from the last step — the next step waits for it
        /// to come back to neutral. Exposed because it is the half of the latch a test can see.</summary>
        public bool IsLatched => _latched;

        /// <summary>
        /// Feed this frame's axis. Returns <c>+1</c> for a push in the positive direction, <c>-1</c> for
        /// the negative, and <c>0</c> when nothing steps this frame — because the axis is inside the dead
        /// zone, or because it is still latched from the last push.
        /// </summary>
        public int Step(float axis)
        {
            if (Mathf.Abs(axis) < Threshold) { _latched = false; return 0; }
            if (_latched) return 0;

            _latched = true;
            return axis > 0f ? 1 : -1;
        }

        /// <summary>
        /// Take the axis WITHOUT stepping — for a list too short to move through.
        ///
        /// <para>It still latches, which matters: a push held over a one-row list and then released onto
        /// a longer one must not be counted as a fresh push the instant the list grows.</para>
        /// </summary>
        public void Absorb(float axis) => _latched = Mathf.Abs(axis) >= Threshold;

        /// <summary>Forget the push — for a list that has just been rebuilt under the cursor.</summary>
        public void Reset() => _latched = false;
    }
}
