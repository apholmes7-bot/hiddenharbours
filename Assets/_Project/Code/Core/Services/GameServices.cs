namespace HiddenHarbours.Core
{
    /// <summary>
    /// A deliberately tiny service locator. The composition root (GameRoot, in the App
    /// assembly) constructs the services at boot and assigns them here; feature modules read
    /// them through the Core interfaces. This is the "start simple" wiring noted in
    /// docs/architecture/tech-architecture.md §2 — a full DI container can replace it later
    /// without changing call sites.
    /// </summary>
    public static class GameServices
    {
        public static IGameClock Clock { get; set; }
        public static IEnvironmentService Environment { get; set; }
        public static IWallet Wallet { get; set; }

        /// <summary>
        /// The player's license wallet (St Peters opening): which fishing/gear licenses they hold.
        /// Lets Fishing gate the rod-fishes-cod catch on the cod license WITHOUT referencing Economy —
        /// the same indirection as <see cref="Wallet"/>/<c>IHold</c>. OPTIONAL and NOT part of
        /// <see cref="Ready"/>: it is null until Economy's <c>LicenseService</c> registers itself
        /// (e.g. in EditMode, or before the opening scene). Consumers must null-check — a null service
        /// means "no gating", so ungated content stays catchable.
        /// </summary>
        public static ILicenseService Licenses { get; set; }

        /// <summary>
        /// The active boat's heading + course-over-ground reporter (VS-19 compass / set-&amp;-drift).
        /// OPTIONAL and scene-scoped — like <see cref="Wallet"/> it is NOT part of <see cref="Ready"/>:
        /// it is null on foot / before a boat is aboard, and the producer (ActiveBoatProbe) registers
        /// itself when present rather than being wired on the persistent GameRoot. Consumers must
        /// null-check it (ADR 0007).
        /// </summary>
        public static IActiveBoatService ActiveBoat { get; set; }

        /// <summary>
        /// The versioned save system (VS-08). Self-installing and persistent (SaveService bootstraps
        /// itself before the first scene), so unlike the others it is not wired by GameRoot. The world
        /// reads/writes persisted flags through it (the onboarding-flags consolidation off PlayerPrefs).
        /// Optional — null before the bootstrap runs (e.g. EditMode) — so consumers must null-check.
        /// </summary>
        public static ISaveService Save { get; set; }

        /// <summary>
        /// The active region's terrain-elevation source — the "height map" the tidal-exposure seam reads
        /// (St Peters falling tide; the future water depth-gradient shader). The <b>world</b> registers
        /// its terrain here when a region scene loads; <b>gameplay</b>/UI resolve elevation through this
        /// accessor WITHOUT referencing the World module — the same Core-mediated indirection as
        /// <see cref="ActiveBoat"/>/<see cref="Licenses"/> (CLAUDE.md rule 4, ADR 0007/0009). OPTIONAL and
        /// scene-scoped: NOT part of <see cref="Ready"/>, and null before a region wires itself (EditMode,
        /// pre-first-scene boot). <b>A null terrain means "open water"</b> — consumers treat the absence of
        /// a height map as everywhere-submerged / no walkable ground rather than throwing.
        /// </summary>
        public static ITidalTerrain TidalTerrain { get; set; }

        /// <summary>
        /// The stable id of the region the player is CURRENTLY in (e.g. <c>"region.st_peters"</c>) —
        /// the travel-aware read gameplay resolves per-region content against (which fish bite HERE,
        /// now). The <b>App</b> travel rig is the writer (the active region's anchor reports itself;
        /// a region hop re-points it); <b>gameplay</b> reads it at act-time (a cast, a dig) WITHOUT
        /// referencing the App module — the same Core-mediated indirection as
        /// <see cref="TidalTerrain"/> (rule 4). OPTIONAL and NOT part of <see cref="Ready"/>: null/empty
        /// before any region reports (EditMode, a test rig, pre-boot) — consumers then fall back to
        /// their own authored region id, so nothing breaks where travel isn't wired.
        /// FLAG lead-architect: new Core contract (this fix's travel-aware region seam).
        /// </summary>
        public static string CurrentRegionId { get; set; }

        public static bool Ready => Clock != null && Environment != null;

        /// <summary>Clear references (scene teardown / tests).</summary>
        public static void Reset()
        {
            Clock = null;
            Environment = null;
            Wallet = null;
            Licenses = null;
            ActiveBoat = null;
            Save = null;
            TidalTerrain = null;
            CurrentRegionId = null;
        }
    }
}
