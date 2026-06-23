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
        }
    }
}
