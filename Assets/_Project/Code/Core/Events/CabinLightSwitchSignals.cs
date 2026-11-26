using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Somebody threw a boat's cabin-light switch.</b> Published once per press, by whichever of the
    /// boat's switches was pressed (the one at the helm, or the one by the entry door on a boat whose
    /// helm is far from it). Both switches drive one state per boat, so a listener never needs to know
    /// which was used to know what the cabin is now.
    ///
    /// <para>For audio (the click), co-op (the state is the boat's, any player aboard can press it), and
    /// anything else that wants the moment rather than the state. The look of the lit windows is NOT
    /// driven by this event: the switch hands its state straight to the boat's lights.</para>
    /// </summary>
    public readonly struct CabinLightSwitchThrown
    {
        /// <summary>The boat ROOT's id, the same id the cabin bus (<see cref="CabinEntered"/>) speaks in.</summary>
        public readonly EntityId HullId;

        /// <summary>The switch fixture's own id (<c>fixture.boat.&lt;interior&gt;.light_switch.&lt;spot&gt;</c>).</summary>
        public readonly string SwitchId;

        /// <summary>True when the press turned the cabin lights ON, false when it turned them OFF.</summary>
        public readonly bool On;

        public CabinLightSwitchThrown(EntityId hullId, string switchId, bool on)
        {
            HullId = hullId;
            SwitchId = switchId ?? "";
            On = on;
        }
    }
}
