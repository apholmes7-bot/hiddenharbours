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

        /// <summary>
        /// The owner's tuning asset, wired once by <c>GameRoot</c>. OPTIONAL and deliberately NOT part
        /// of <see cref="Ready"/>: EditMode, a bare art scene and every test rig run without it, and
        /// each derived read below falls back to its own <c>Default</c>, so nothing breaks unwired.
        ///
        /// <para><b>Read the derived policies, not this.</b> Exposed because <c>GameRoot</c> must set
        /// it and the wave lane must reach it, but a consumer poking at arbitrary config blocks
        /// through a global is how a service locator turns into a bag of globals. New blocks get their
        /// own accessor, as <see cref="WaveField"/> did.</para>
        /// FLAG lead-architect: new Core contract (the ADR 0018 §(5) settings unification).
        /// </summary>
        public static GameConfig Config { get; set; }

        /// <summary>
        /// The ONE wind → wave-train derivation every consumer reads (ADR 0018 §(5)): the shader
        /// bridge, the hull rocking, the seakeeping forces, the wake, the buoys, the trap haul and the
        /// drift weed. Falls back to <see cref="WaveFieldSettings.Default"/> with no config wired.
        ///
        /// <para>⚠️ <c>Config != null</c>, never <c>?.</c> or <c>??</c> — <see cref="GameConfig"/> is a
        /// <c>UnityEngine.Object</c>, and the null-propagating operators bypass its overloaded
        /// <c>==</c>, so a DESTROYED asset would read as non-null and throw. Compile-clean,
        /// runtime-red.</para>
        ///
        /// <para>Resolved on every read rather than cached, so dragging a slider in the GameConfig
        /// inspector during play moves the sea live — which is exactly how the owner judges it.</para>
        /// </summary>
        public static WaveFieldSettings WaveField =>
            Config != null ? Config.WaveField : WaveFieldSettings.Default;

        /// <summary>The shared presentation smoother's tunables (ADR 0018 addendum), same contract as
        /// <see cref="WaveField"/>. The SIM path does not ease — it reads the pure <c>WaveMath</c> at
        /// game time — so this governs LOOK consumers only.</summary>
        public static WaveFieldAnimatorSettings WaveFieldAnimator =>
            Config != null ? Config.WaveFieldAnimator : WaveFieldAnimatorSettings.Default;

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
            Config = null;
        }
    }
}
