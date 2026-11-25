namespace HiddenHarbours.Core
{
    /// <summary>How a boat's cabin lights are switched.</summary>
    public enum CabinLightsSwitch
    {
        /// <summary>Nobody has touched the switch: the cabin follows the lamp regime (lit under way,
        /// or at a berth while somebody is aboard). The default, and what every boat did before.</summary>
        Auto = 0,
        /// <summary>Switched on: the cabin is lit at night whether or not anybody is below.</summary>
        On = 1,
        /// <summary>Switched off: the cabin stays dark, under way or not.</summary>
        Off = 2,
    }

    /// <summary>
    /// <b>This boat's cabin lights are on, off, or left to the regime.</b> Asked of whatever component
    /// sits under a boat's root (find it with <c>root.GetComponentInChildren&lt;ICabinLights&gt;()</c>),
    /// so a helm switch in gameplay never reaches into the Art class that draws the lit windows
    /// (rule 4). The drawing side answers by lighting or darkening the cabin's panes and the light
    /// they throw.
    ///
    /// <para>The switch state is presentation state only: it is not saved here. Whoever owns the
    /// switch decides whether it persists, and sets it again after a load.</para>
    ///
    /// <para>A derelict hull has no power: she stays dark whatever the switch says.</para>
    /// </summary>
    public interface ICabinLights
    {
        /// <summary>Where the switch stands now.</summary>
        CabinLightsSwitch CabinLights { get; }

        /// <summary>Throw the switch. Idempotent and allocation-free.</summary>
        void SetCabinLights(CabinLightsSwitch state);

        /// <summary>Is the cabin lit right now (the switch folded with the regime)? The night gate
        /// still applies on screen: a lit cabin's windows do not glow at noon.</summary>
        bool CabinLightsLit { get; }
    }
}
