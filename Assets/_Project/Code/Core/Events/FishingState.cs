namespace HiddenHarbours.Core
{
    /// <summary>
    /// The stage of the fishing interaction. Phases drive the transient rod gauge and let audio/HUD react
    /// to discrete beats (cast, bite, land, snap) without knowing the fight maths.
    ///
    /// <para><b>Contract — APPEND-ONLY.</b> These values are the cross-module vocabulary UI/audio switch on,
    /// AND they are serialized on components/assets, so the integer of an existing member is <b>frozen</b>.
    /// VS-13 shipped <see cref="Idle"/>..<see cref="NoBite"/> (0..7). Rod Fishing v2 grows the set for the
    /// flick-cast + depth-drop + deep→surface fight (design/rod-fishing-v2-brainstorm.md §2-3, §8) by
    /// <b>appending</b> members (8+). Never renumber, reuse, or reorder an existing member. The explicit
    /// ints below pin the wire format so a later edit can't silently shift them.</para>
    ///
    /// <para><b>Legacy vs. v2 fight.</b> <see cref="Fighting"/> (=3) is the VS-13 single-phase tension fight
    /// and stays a valid, supported phase — a species with <b>no</b> RodFightDef keeps fighting on it (the
    /// simple/legacy fight, the same opt-in shape as TrapDef→DeckWorkDef). A species that opts into a
    /// RodFightDef fights the v2 two-part arc: <see cref="FightDeep"/> (unseen, line straight down) then
    /// <see cref="FightSurface"/> (visible, mouse-steer). Both are "a fight" — group them with
    /// <see cref="FishingState.IsFightPhase"/> rather than hardcoding the list.</para>
    /// </summary>
    public enum FishingPhase
    {
        // ---- VS-13 baseline — serialized ints FROZEN (0..7) --------------------------------------
        Idle = 0,     // nothing happening — gauge hidden
        Waiting = 1,  // line is out, waiting for a bite
        Bite = 2,     // a bite — a brief, forgiving hook beat
        Fighting = 3, // the legacy single-phase tension fight (a species with no RodFightDef) — still valid
        Tending = 4,  // the lighter hand-gather "tend" variant (crab/mussel) — no snap
        Landed = 5,   // success result beat
        Snapped = 6,  // the line threw the hook — cozy fail, no penalty
        NoBite = 7,   // nothing was biting this cast

        // ---- Rod Fishing v2 — APPENDED (8+); see design §2.2 (cast), §2.3 (depth), §3 (fight) --------
        WindBack = 8,      // flick-cast wind-back — the rod is drawn back following the aim (§2.2)
        Cast = 9,          // the line is in flight after the flick/release (§2.2)
        Sinking = 10,      // the weighted rig is sinking through the column — the depth drop (§2.3)
        FightDeep = 11,    // fish unseen, line straight down — pure pull-on-slack timing, no steer yet (§3)
        FightSurface = 12, // fish visible, line moves around the screen — mouse-steer + pull to land (§3)
    }

    /// <summary>
    /// A read-only snapshot of the live fishing interaction. Published on <see cref="FishingStateChanged"/>
    /// so UI/audio can render or react to it WITHOUT referencing the Fishing module (cross-module talk
    /// through Core only). The transient rod gauge consumes this today; ui-ux re-skins/relocates it into
    /// the formal HUD later (VS-14) by consuming the same struct — no logic changes needed.
    ///
    /// Cross-module contract: lives in Core (lead-architect's area) so both the Fishing publisher and a
    /// future UI subscriber depend only on Core.
    ///
    /// <para><b>v2 growth is additive.</b> The seven VS-13 fields keep their exact meaning; Rod Fishing v2
    /// adds three diegetic reads — <see cref="Depth01"/> (the depth game, §2.3), <see cref="SlackWindowOpen"/>
    /// (the pull-on-slack tell, §3) and <see cref="RodBend01"/> (the bending-rod read, §0/§3). The original
    /// 7-arg constructor is preserved as an overload that defaults the new reads to neutral, so every
    /// VS-13 caller compiles unchanged — no consumer's field meaning changes.</para>
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

        // ---- Rod Fishing v2 diegetic reads (additive; default neutral for the legacy fight) ----------
        /// <summary>Where the rig is held in the water column: 0 = surface, 1 = on the floor of the
        /// reachable band. The depth game's continuous read (§2.3) — drives the sinking-line visual and,
        /// once wired, feeds the species-targeting depth weight. 0 for the legacy fight (no depth game).</summary>
        public readonly float Depth01;

        /// <summary>True while the fish is slack — the "PULL now" tell (§3): reeling here gains
        /// <see cref="Landing01"/> safely, whereas reeling into a run climbs <see cref="Tension01"/>
        /// toward a snap. The diegetic replacement for a HUD prompt (audio cue / line-goes-slack).
        /// Pre-bite (<see cref="FishingPhase.Sinking"/>/<see cref="FishingPhase.Waiting"/>) it carries the
        /// depth drop's BOTTOM tell instead (§2.3): the weighted rig is resting on the floor and the line
        /// has gone slack — the false→true transition is what presentation pops its slack-overshoot from.
        /// Always false in the legacy fight.</summary>
        public readonly bool SlackWindowOpen;

        /// <summary>0..1 diegetic rod-bend / line-load read for the bending-rod, taut-line feel (§0, §3).
        /// A presentation read the art rig maps to rod curvature — distinct from <see cref="Tension01"/>
        /// (the snap-danger axis) so the fight maths can shape the *look* of strain apart from its
        /// *danger*. 0 in the legacy fight (the VS-13 gauge reads <see cref="Tension01"/> only).</summary>
        public readonly float RodBend01;

        /// <summary>Full v2 constructor — sets every field, including the three diegetic reads.</summary>
        public FishingState(FishingPhase phase, float tension01, float landing01,
                            string fishId, string displayName, FishCategory category, float weightKg,
                            float depth01, bool slackWindowOpen, float rodBend01)
        {
            Phase = phase;
            Tension01 = tension01;
            Landing01 = landing01;
            FishId = fishId;
            DisplayName = displayName;
            Category = category;
            WeightKg = weightKg;
            Depth01 = depth01;
            SlackWindowOpen = slackWindowOpen;
            RodBend01 = rodBend01;
        }

        /// <summary>Legacy VS-13 constructor (preserved) — the three v2 reads default to neutral
        /// (Depth01 = 0, SlackWindowOpen = false, RodBend01 = 0). Keeps every existing caller compiling.</summary>
        public FishingState(FishingPhase phase, float tension01, float landing01,
                            string fishId, string displayName, FishCategory category, float weightKg)
            : this(phase, tension01, landing01, fishId, displayName, category, weightKg,
                   depth01: 0f, slackWindowOpen: false, rodBend01: 0f)
        {
        }

        /// <summary>The idle snapshot (nothing on the line).</summary>
        public static FishingState Idle => new FishingState(
            FishingPhase.Idle, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f);

        /// <summary>True while the interaction is live (cast through result) — i.e. the gauge should show.</summary>
        public bool IsActive => Phase != FishingPhase.Idle;

        /// <summary>True in any fight phase — the legacy <see cref="FishingPhase.Fighting"/>, the v2
        /// <see cref="FishingPhase.FightDeep"/>/<see cref="FishingPhase.FightSurface"/> arc, or the
        /// hand-gather <see cref="FishingPhase.Tending"/>. Consumers (audio strain, on-line struggle)
        /// should group "the fight" through this rather than re-listing phases as the set grows.</summary>
        public bool IsFightPhase => Phase == FishingPhase.Fighting
                                 || Phase == FishingPhase.FightDeep
                                 || Phase == FishingPhase.FightSurface
                                 || Phase == FishingPhase.Tending;
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
