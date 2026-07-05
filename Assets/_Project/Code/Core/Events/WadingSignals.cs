namespace HiddenHarbours.Core
{
    /// <summary>
    /// The on-foot player's water state (the owner's three-band wade model, P1/P5). A projection of the
    /// on-foot <see cref="DepthBand"/> the fisher is standing in — <see cref="DepthBand.Deep"/> never
    /// occurs on foot (it is soft-walled off), so this enum stops at <see cref="Swim"/>.
    /// </summary>
    public enum OnFootWaterState
    {
        /// <summary>On dry, exposed ground — full walking speed.</summary>
        Dry = 0,
        /// <summary>Wading shallow water — walkable but slowed.</summary>
        Wade = 1,
        /// <summary>Swimming the escape band — very slow, the "get back to shore" state (never travel).</summary>
        Swim = 2,
    }

    /// <summary>
    /// Raised when the on-foot player's water state changes (dry ↔ wade ↔ swim) — the single edge the HUD
    /// hooks to show the canon "flood making — head in" warning (design/time-tides-weather.md §3.8) and
    /// audio/VFX hook a wade splash. It carries both the new and previous <see cref="OnFootWaterState"/>
    /// so a subscriber can tell an <em>entry</em> into the swim band (footing flooded — the moment to warn)
    /// from a return to shallower water, and a <see cref="Deepening"/> flag (the tide/step is taking the
    /// player DEEPER, not toward shore) so the warning only fires when it should.
    ///
    /// <para>Fired only on a genuine band transition (never per frame), from the Player lane; nothing
    /// consumes it yet — the HUD warning display + splash VFX are noted follow-ups in their own lanes
    /// (ui-ux / audio / art-pipeline). Lives in its own Core/Events file (additive), like
    /// <c>LicenseSignals</c>, rather than editing lead-architect's <c>GameSignals.cs</c>
    /// (coordination.md §1 — "keep it additive: new interface/event").</para>
    /// </summary>
    public readonly struct OnFootWaterStateChanged
    {
        /// <summary>The band the player is now in.</summary>
        public readonly OnFootWaterState State;
        /// <summary>The band the player was in the previous transition (so consumers can detect entries/exits).</summary>
        public readonly OnFootWaterState Previous;
        /// <summary>True when the change went to a DEEPER band (Dry→Wade, Wade→Swim) — the direction the
        /// "flood making — head in" warning cares about; false when returning toward shore.</summary>
        public readonly bool Deepening;
        /// <summary>Water depth over the player at the moment of the change (m) — for a graded warning/VFX.</summary>
        public readonly float Depth;

        public OnFootWaterStateChanged(OnFootWaterState state, OnFootWaterState previous, bool deepening, float depth)
        {
            State = state; Previous = previous; Deepening = deepening; Depth = depth;
        }
    }
}
