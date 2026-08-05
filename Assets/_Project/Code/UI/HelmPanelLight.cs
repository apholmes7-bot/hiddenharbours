using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// THE one "are the panel lights on" decision for the helm (ADR 0025, the night panel). Every
    /// instrument that has an authored night face — the four dash chromes, the binnacle compass, the
    /// brow sounder and the colour finder — asks this and nothing else, so the helm can never light
    /// in pieces.
    ///
    /// <para><b>Why there is no new dusk constant here.</b> The project already rules when night
    /// falls: <see cref="WatchFaceState.NightAt"/> (06:00 / 19:00), the rule the diegetic watch's own
    /// backlight and the cottage windows both run on. A second threshold — a literal here, or a fresh
    /// <c>GameConfig</c> row — would be a second answer to a question that is already answered, and a
    /// config row that the shipped <c>GameConfig.asset</c> did not also carry would deserialize to
    /// ZERO and ship this feature inert while every test passed. So: no new member, no new row, and
    /// moving dusk moves the whole world at once (rule 6).</para>
    ///
    /// <para><b>No clock, no light.</b> A null clock reads DAY rather than throwing — the overlay
    /// hosts are self-installing singletons that run in EditMode/PlayMode rigs and on a boot frame
    /// before the sim registers, and a helm that draws its day face is the honest answer there.</para>
    /// </summary>
    public static class HelmPanelLight
    {
        /// <summary>Are the panel lights on at <paramref name="clock"/>'s hour? Pure (test seam — pass
        /// a fake clock rather than reaching for <see cref="GameServices"/>).</summary>
        public static bool IsNight(IGameClock clock)
            => clock != null && WatchFaceState.NightAt(clock.HourOfDay);

        /// <summary>Are the panel lights on right now, per the shared world clock?</summary>
        public static bool IsNight() => IsNight(GameServices.Clock);
    }
}
