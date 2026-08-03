namespace HiddenHarbours.Core
{
    /// <summary>
    /// The active hull's <b>instrument GLASS</b> seam (ADR 0025 S2): what is actually fitted in this
    /// boat's helm right now, what those instruments read from the live sim, and the player's per-hull
    /// display preferences. The sibling of <see cref="IHelmControl"/>, which stays what S1 made it —
    /// the piloting CONTROL (drive/steer + intents).
    ///
    /// <para><b>Why a second seam and not more members on <see cref="IHelmControl"/>.</b> S3–S5 add a
    /// fish finder, a radar and a chartplotter to this same dash. Piling every instrument's read onto
    /// the piloting interface would grow it into a god-interface that every consumer of the throttle
    /// has to see. Split at the honest joint: one seam moves the boat, one seam reports the glass. Both
    /// are produced by the same Boats-lane relay and both are read through
    /// <see cref="GameServices"/> — the UI assembly references only Core (rule 4).</para>
    ///
    /// <para><b>Recompute, never store.</b> <see cref="Fit"/> is
    /// <c>BoatEquipment.EffectiveFit</c> resolved fresh from the hull's console + the owned-per-hull
    /// set; <see cref="TryReadDepth"/> is <see cref="DepthSounder.DisplayDepth"/> over the one shared
    /// height map. Neither is ever serialized (rule 5). Only the OWNERSHIP and the
    /// <see cref="SounderPrefs"/> are save state.</para>
    ///
    /// <para><b>Pull, not push; optional and scene-scoped</b> — sampled on demand like
    /// <see cref="IActiveBoatService"/>/<see cref="IHelmControl"/>, self-registered by the Boats-lane
    /// producer, and null on foot / in EditMode. Consumers must null-check (ADR 0007).</para>
    /// FLAG lead-architect: new Core contract (the ADR 0025 S2 helm-instrument seam).
    /// </summary>
    public interface IHelmInstruments
    {
        /// <summary>Stable hull id the fitted instruments belong to (e.g. <c>"boat.skiff"</c>); empty
        /// when there is no piloted hull. Instruments are owned PER HULL, so this is the key every
        /// ownership and preference read is made against.</summary>
        string HullId { get; }

        /// <summary>
        /// The EFFECTIVE fit of this hull's helm right now — the console's authored default with the
        /// player's owned upgrades layered on, resolved fresh (never stored). <see cref="HelmFit.None"/>
        /// when there is no piloted hull or the hull has no console (the dory).
        /// </summary>
        HelmFit Fit { get; }

        /// <summary>
        /// The live water depth under the hull in metres (≥ 0, clamped at 0 aground). Returns
        /// <c>false</c> — and the instrument should show nothing rather than a stale or invented number
        /// — when there is no transducer read to be had: no piloted hull, no environment service, or no
        /// terrain height map registered for the region.
        ///
        /// <para>Implementations read on a THROTTLED tick (the tide moves in minutes, not frames —
        /// <c>DepthSounderSettings.ReadIntervalSec</c>, rule 7); callers may poll this every frame.</para>
        /// </summary>
        bool TryReadDepth(out float metres);

        /// <summary>This hull's persisted sounder preferences (set-point, armed, units, night). Falls
        /// back to <see cref="SounderPrefs.FromDefaults"/> for a hull that has never been touched.</summary>
        SounderPrefs SounderPrefs { get; }

        /// <summary>Write this hull's sounder preferences back (a button press on the glass). Persists
        /// through the save service when one is wired; a no-op with no hull.</summary>
        void SetSounderPrefs(in SounderPrefs prefs);
    }
}
