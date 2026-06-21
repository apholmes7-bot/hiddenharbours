namespace HiddenHarbours.Core
{
    /// <summary>
    /// The stage of the fishing interaction (VS-13). Phases drive the transient rod gauge and let
    /// audio/HUD react to discrete beats (bite, land, snap) without knowing the fight maths.
    /// </summary>
    public enum FishingPhase
    {
        Idle = 0,    // nothing happening — gauge hidden
        Waiting,     // line is out, waiting for a bite
        Bite,        // a bite — a brief, forgiving hook beat
        Fighting,    // the tension/landing fight (rod & reel)
        Tending,     // the lighter hand-gather "tend" variant (crab/mussel) — no snap
        Landed,      // success result beat
        Snapped,     // the line threw the hook — cozy fail, no penalty
        NoBite       // nothing was biting this cast
    }

    /// <summary>
    /// A read-only snapshot of the live fishing interaction. Published on <see cref="FishingStateChanged"/>
    /// so UI/audio can render or react to it WITHOUT referencing the Fishing module (cross-module talk
    /// through Core only). The transient rod gauge consumes this today; ui-ux re-skins/relocates it into
    /// the formal HUD later (VS-14) by consuming the same struct — no logic changes needed.
    ///
    /// Cross-module contract: lives in Core (lead-architect's area) so both the Fishing publisher and a
    /// future UI subscriber depend only on Core.
    /// </summary>
    public readonly struct FishingState
    {
        public readonly FishingPhase Phase;
        public readonly float Tension01;   // 0..1 line strain; 1 = snap
        public readonly float Landing01;   // 0..1 how landed the fish is; 1 = aboard
        public readonly string FishId;     // resolved species id, or null before a bite
        public readonly string DisplayName;// resolved species name, or null before a bite
        public readonly FishCategory Category;
        public readonly float WeightKg;    // for sizing the on-line silhouette

        public FishingState(FishingPhase phase, float tension01, float landing01,
                            string fishId, string displayName, FishCategory category, float weightKg)
        {
            Phase = phase;
            Tension01 = tension01;
            Landing01 = landing01;
            FishId = fishId;
            DisplayName = displayName;
            Category = category;
            WeightKg = weightKg;
        }

        /// <summary>The idle snapshot (nothing on the line).</summary>
        public static FishingState Idle => new FishingState(
            FishingPhase.Idle, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f);

        /// <summary>True while the interaction is live (cast through result) — i.e. the gauge should show.</summary>
        public bool IsActive => Phase != FishingPhase.Idle;
    }

    /// <summary>
    /// Raised whenever the fishing interaction state changes — on every phase transition and each tick
    /// of an active fight so a gauge can animate the bars smoothly. Payload is a value struct (no GC).
    /// </summary>
    public readonly struct FishingStateChanged
    {
        public readonly FishingState State;
        public FishingStateChanged(FishingState state) { State = state; }
    }
}
