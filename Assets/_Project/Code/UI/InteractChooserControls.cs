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
        {
            if (!InteractChooser.IsOpen || InteractChooser.OpenedThisFrame)
            {
                // Closed, or the press that opened it is still down: take the axis without acting, so a
                // push held through the open is not counted as a fresh one.
                latch?.Absorb(intents.Navigate);
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

            int step = latch != null ? latch.Step(intents.Navigate) : 0;
            // +1 on the axis is UP the screen, and row 0 is the top row.
            if (step != 0) return InteractChooser.Step(-step) ? Outcome.Stepped : Outcome.Nothing;

            if (pointerMoved && hitRow >= 0 && InteractChooser.Select(hitRow)) return Outcome.Hovered;
            return Outcome.Nothing;
        }
    }
}
