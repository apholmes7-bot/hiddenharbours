using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>One frame of the player's hands, applied to the open chooser</b> — the whole input rule of the
    /// list, pure, so "the opening press does not also pick", "a number key on a taken bunk does nothing"
    /// and "a held arrow moves one row" are EditMode assertions (the view only feeds it).
    ///
    /// <para><b>Precedence, one action per frame:</b> cancel, then a number key, then a click on a row,
    /// then confirm, then the up/down step, then a hover. A frame that put the list down acts on nothing
    /// else.</para>
    /// </summary>
    public static class InteractChooserControls
    {
        /// <summary>What the frame did, for the view's bookkeeping and the tests.</summary>
        public enum Outcome
        {
            Nothing = 0,
            Cancelled = 1,
            Picked = 2,
            Confirmed = 3,
            Stepped = 4,
            Hovered = 5,
            Refused = 6,
        }

        /// <param name="intents">This frame's intents, already through the gates.</param>
        /// <param name="latch">The view's own latch on the vertical axis.</param>
        /// <param name="hitRow">The row under the pointer, or -1 (<see cref="InteractChooserLayout.HitRow"/>).</param>
        /// <param name="pointerMoved">Whether the pointer moved this frame: a still mouse resting on a row
        /// must not drag the selection back every time the keys move it.</param>
        public static Outcome Apply(in ChooserIntents intents, AxisLatch latch, int hitRow, bool pointerMoved)
            => Apply(intents, latch, null, default, 0f, false, hitRow, pointerMoved);

        /// <summary>
        /// The same frame, with a held push that may run on (ADR 0043 PR 2: a pad's stick or d-pad held on
        /// the list). <paramref name="allowRepeat"/> false is the latch exactly, so the keyboard's one row
        /// per press is unchanged.
        /// </summary>
        /// <param name="axis">The view's own repeat on the vertical axis.</param>
        /// <param name="tuning">The threshold and repeat timings (<see cref="ChooserAxisTuning.Current"/>).</param>
        /// <param name="deltaTime">Unscaled seconds since the last frame.</param>
        /// <param name="allowRepeat">Whether a held push may run on (the view: the pad is the active device).</param>
        public static Outcome Apply(in ChooserIntents intents, ChooserAxisRepeat axis, in ChooserAxisTuning tuning,
                                    float deltaTime, bool allowRepeat, int hitRow, bool pointerMoved)
            => Apply(intents, null, axis, tuning, deltaTime, allowRepeat, hitRow, pointerMoved);

        private static Outcome Apply(in ChooserIntents intents, AxisLatch latch, ChooserAxisRepeat axis,
                                     in ChooserAxisTuning tuning, float deltaTime, bool allowRepeat,
                                     int hitRow, bool pointerMoved)
        {
            if (!InteractChooser.IsOpen || InteractChooser.OpenedThisFrame)
            {
                // Closed, or the press that opened it is still down: take the axis without acting, so a
                // push held through the open is not counted as a fresh one.
                latch?.Absorb(intents.Navigate);
                axis?.Absorb(intents.Navigate, tuning);
                return Outcome.Nothing;
            }

            if (intents.Cancel)
            {
                InteractChooser.Cancel();
                return Outcome.Cancelled;
            }

            if (intents.Pick > 0)
                return InteractChooser.Pick(intents.Pick - 1) ? Outcome.Picked : Outcome.Refused;

            if (intents.Click && hitRow >= 0)
                return InteractChooser.Pick(hitRow) ? Outcome.Picked : Outcome.Refused;

            if (intents.Confirm)
                return InteractChooser.Confirm() ? Outcome.Confirmed : Outcome.Refused;

            int step = latch != null ? latch.Step(intents.Navigate)
                     : axis != null ? axis.Step(intents.Navigate, deltaTime, allowRepeat, tuning)
                     : 0;
            // +1 on the axis is UP the screen, and row 0 is the top row.
            if (step != 0) return InteractChooser.Step(-step) ? Outcome.Stepped : Outcome.Nothing;

            if (pointerMoved && hitRow >= 0 && InteractChooser.Select(hitRow)) return Outcome.Hovered;
            return Outcome.Nothing;
        }
    }
}
