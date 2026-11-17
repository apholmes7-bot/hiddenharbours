using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One frame of what the player asks of the HUD itself</b>, in every control mode: hide every
    /// boat-UI window at once, or bring them all back (the 2026-08-07 windowing ruling's one input,
    /// M on the keyboard). An EDGE, true on the frame the control went down (ADR 0043).
    ///
    /// <para>A mode of its own rather than a press in each mode's struct: the watch face shows ashore
    /// and the hide-all restore is its recovery path, so the press is the same on foot, on deck and at
    /// the helm, and the bindings asset says it ONCE, in its <c>Hud</c> map (H2).</para>
    ///
    /// <para>The TASKS note's fold (2026-11-16) is the map's second press, with the pointer's click and
    /// position for a click on the note's head or its folded chip (<see cref="TasksNoteFold"/>).</para>
    /// </summary>
    public readonly struct HudIntents
    {
        public readonly bool HideAll;

        /// <summary>Fold the TASKS note down to its chip, or open it again (owner ask 2026-11-16). An
        /// edge, like <see cref="HideAll"/>.</summary>
        public readonly bool ToggleTasks;

        /// <summary>The pointer's press went down this frame (an edge). Read with <see cref="Point"/> by
        /// the HUD's own hit test: the TASKS note's head, or its folded chip.</summary>
        public readonly bool Click;

        /// <summary>Where the pointer is, in screen pixels (bottom-left origin).</summary>
        public readonly Vector2 Point;

        public HudIntents(bool hideAll) : this(hideAll, false, false, Vector2.zero) { }

        public HudIntents(bool hideAll, bool toggleTasks, bool click, Vector2 point)
        {
            HideAll = hideAll;
            ToggleTasks = toggleTasks;
            Click = click;
            Point = point;
        }

        public static HudIntents None => default;
    }

    /// <summary>The scripted HUD source. A press is an edge: set it for one frame, then
    /// <see cref="HeldIntents{TIntents}.Release"/>, or every frame toggles again.</summary>
    public sealed class HeldHudIntents : HeldIntents<HudIntents>
    {
        public void PressHideAll() => Set(new HudIntents(true));

        public void PressToggleTasks() => Set(new HudIntents(false, true, false, Vector2.zero));

        public void ClickAt(Vector2 point) => Set(new HudIntents(false, false, true, point));
    }

    /// <summary>
    /// <b>Where the HUD's device-backed source is installed</b> (ADR 0043, rule 4). The HUD's reader
    /// lives in UI and the device layer in Player, and UI cannot reference Player; so the device layer
    /// installs its source here and the reader takes it from Core. The slot holds the asset's source
    /// only: a test or a scripted driver hands its own to the reader, never to this slot.
    /// </summary>
    public static class HudInput
    {
        private static IControlIntentSource<HudIntents> _device;

        /// <summary>The bindings asset's HUD source, or null before the device layer installed one
        /// (EditMode, or a stripped headless rig): the reader then hears nothing.</summary>
        public static IControlIntentSource<HudIntents> Device => _device;

        public static void InstallDevice(IControlIntentSource<HudIntents> source) => _device = source;
    }
}
