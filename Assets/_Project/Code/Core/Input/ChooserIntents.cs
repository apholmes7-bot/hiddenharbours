using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One frame of what the player asks of the interaction chooser</b> (owner ask 2026-11-15: "select
    /// the desired action with keys or mouse"). Read by the chooser's view in UI, only while the list is
    /// up (ADR 0043: intents in Core, bindings as data).
    ///
    /// <para><see cref="Navigate"/> is the vertical axis, +1 = up the list (the view latches it, one row
    /// per push; with the pad in hand a held push runs on after a delay, timings in GameConfig). <see cref="Pick"/> is a number key that went down this frame, 1..9, or 0. The rest are
    /// edges, true on the frame the control went down, except <see cref="Point"/> (screen pixels, bottom-
    /// left origin), which is where the pointer is.</para>
    /// </summary>
    public readonly struct ChooserIntents
    {
        public readonly float Navigate;
        public readonly int Pick;
        public readonly bool Confirm;
        public readonly bool Cancel;
        public readonly bool Click;
        public readonly Vector2 Point;

        public ChooserIntents(float navigate, int pick, bool confirm, bool cancel, bool click, Vector2 point)
        {
            Navigate = navigate;
            Pick = pick;
            Confirm = confirm;
            Cancel = cancel;
            Click = click;
            Point = point;
        }

        public static ChooserIntents None => default;
    }

    /// <summary>The scripted chooser source. Presses are edges: set for one frame, then
    /// <see cref="HeldIntents{TIntents}.Release"/>.</summary>
    public sealed class HeldChooserIntents : HeldIntents<ChooserIntents>
    {
        public void PressConfirm() => Set(new ChooserIntents(0f, 0, true, false, false, Vector2.zero));
        public void PressCancel() => Set(new ChooserIntents(0f, 0, false, true, false, Vector2.zero));
        public void PressNumber(int n) => Set(new ChooserIntents(0f, n, false, false, false, Vector2.zero));
        public void Push(float navigate) => Set(new ChooserIntents(navigate, 0, false, false, false, Vector2.zero));
        public void ClickAt(Vector2 point) => Set(new ChooserIntents(0f, 0, false, false, true, point));
        public void PointAt(Vector2 point) => Set(new ChooserIntents(0f, 0, false, false, false, point));
    }

    /// <summary>
    /// <b>Where the chooser's device-backed source is installed</b> (the <see cref="HudInput"/> shape):
    /// the reader is in UI and the device layer in Player, and UI cannot reference Player (rule 4).
    /// </summary>
    public static class ChooserInput
    {
        private static IControlIntentSource<ChooserIntents> _device;

        /// <summary>The bindings asset's chooser source, or null before the device layer installed one.</summary>
        public static IControlIntentSource<ChooserIntents> Device => _device;

        public static void InstallDevice(IControlIntentSource<ChooserIntents> source) => _device = source;
    }
}
