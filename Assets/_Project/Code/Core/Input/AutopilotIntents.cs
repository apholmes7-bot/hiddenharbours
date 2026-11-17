namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One frame of what the player at the helm asks of the autopilot</b>: flick it on, or off
    /// (ADR 0043). An EDGE, true on the frame the control went down. The <c>Helm/Autopilot</c> action
    /// of the bindings asset (P on the keyboard).
    ///
    /// <para>A struct of its own rather than a field on the helm's <c>HelmIntents</c>, which has not
    /// landed (ADR 0043 PR 1): when it does, this press folds into it and the reader does not change.
    /// The reader is the helm itself (<c>DevBoatInput</c>), which only runs while the player is at
    /// the wheel, so the press means nothing on foot or on deck by construction.</para>
    /// </summary>
    public readonly struct AutopilotIntents
    {
        public readonly bool Toggle;

        public AutopilotIntents(bool toggle)
        {
            Toggle = toggle;
        }

        public static AutopilotIntents None => default;
    }

    /// <summary>The scripted autopilot source. A press is an edge: set it for one frame, then
    /// <see cref="HeldIntents{TIntents}.Release"/>, or every frame flicks the switch again.</summary>
    public sealed class HeldAutopilotIntents : HeldIntents<AutopilotIntents>
    {
        public void PressToggle() => Set(new AutopilotIntents(true));
    }

    /// <summary>
    /// <b>Where the autopilot's device-backed source is installed</b> (ADR 0043, rule 4). The reader
    /// is in Boats and the device layer in Player, and Boats cannot reference Player; so the device
    /// layer installs its source here and the reader takes it from Core (the <see cref="HudInput"/>
    /// shape). A test or a scripted driver hands its own source to the reader, never to this slot.
    /// </summary>
    public static class AutopilotInput
    {
        private static IControlIntentSource<AutopilotIntents> _device;

        /// <summary>The bindings asset's autopilot source, or null before the device layer installed
        /// one (EditMode, or a stripped headless rig): the reader then hears nothing.</summary>
        public static IControlIntentSource<AutopilotIntents> Device => _device;

        public static void InstallDevice(IControlIntentSource<AutopilotIntents> source) => _device = source;
    }
}
