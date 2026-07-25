namespace HiddenHarbours.Core
{
    /// <summary>
    /// Which sounder is fitted in a helm's brow — the diegetic depth read. A boat carries the plain
    /// depth sounder OR the colour fish-finder, never both (they share one flush-mount cutout —
    /// <c>docs/art/rigs/ui/readmes/fish-finder.md</c>), or none at all on a bare helm. Maps to the
    /// rig <c>finder</c> signal (<c>depth</c>|<c>fish</c>).
    /// </summary>
    public enum SounderKind
    {
        None  = 0,   // no sounder fitted (blank brow)
        Depth = 1,   // DepthRig — flush-mount depth + shallow alarm (the basic instrument)
        Fish  = 2,   // FishRig — colour sonar upgrade (same cutout)
    }

    /// <summary>
    /// Which compass unit is fitted — maps to the rig <c>compass</c> signal. Dome sits on the crown
    /// (and slides the sounder aside); flush sinks into the dash (Novi &amp; Cape only). None = no compass.
    /// </summary>
    public enum CompassMount
    {
        None  = 0,
        Dome  = 1,
        Flush = 2,
    }

    /// <summary>
    /// Which helm rig draws a hull's dash. Names the runtime RENDERER (the art director's
    /// <c>ConsoleRig</c>/<c>SportRig</c>/<c>NoviRig</c>/<c>CapeRig</c>) — it carries no baked art, because
    /// these instruments are continuous-state (how they render is ADR 0025). <see cref="None"/> = the hull
    /// has no console dash (the bare/rowed dory).
    /// </summary>
    public enum ConsoleRigKind
    {
        None    = 0,
        Console = 1,   // ConsoleRig — centre-console skiff
        Sport   = 2,   // SportRig   — polished sport skiff
        Novi    = 3,   // NoviRig    — modern downeast pilothouse (radar + gps brow slots)
        Cape    = 4,   // CapeRig    — 1982 wheelhouse
    }

    /// <summary>
    /// The EFFECTIVE equipment fit of a boat's helm right now — its console renderer plus which
    /// instruments are actually fitted (the hull's default fit with the player's owned upgrades layered
    /// on). Produced by <c>HiddenHarbours.Boats.BoatEquipment.EffectiveFit</c>; a pure value of Core enums
    /// so any module (Boats to drive the sim, UI to draw the dash) can read it without a cross-module
    /// reference (rule 4). This is derived/recomputed data, not save state.
    /// </summary>
    public readonly struct HelmFit
    {
        public readonly ConsoleRigKind Rig;
        public readonly SounderKind Sounder;
        public readonly CompassMount Compass;
        public readonly bool Radar;
        public readonly bool Gps;

        public HelmFit(ConsoleRigKind rig, SounderKind sounder, CompassMount compass, bool radar, bool gps)
        {
            Rig = rig; Sounder = sounder; Compass = compass; Radar = radar; Gps = gps;
        }

        /// <summary>A hull with no console at all (the dory): no renderer, nothing fitted.</summary>
        public static HelmFit None => new HelmFit(ConsoleRigKind.None, SounderKind.None, CompassMount.None, false, false);

        /// <summary>True if this hull shows a helm console dash at all.</summary>
        public bool HasConsole => Rig != ConsoleRigKind.None;
    }
}
