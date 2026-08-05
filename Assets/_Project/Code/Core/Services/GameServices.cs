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
        /// The active boat's piloting-control seam (ADR 0025 S1): which diegetic control the helm
        /// shows (tiller/lever), the live drive+steer to draw it with, and the input intents the
        /// overlay may send back (detent steps, drag-to-set). Same lifetime + discipline as
        /// <see cref="ActiveBoat"/>: OPTIONAL, NOT part of <see cref="Ready"/>, self-registered by
        /// the Boats-lane producer (<c>HelmControlRelay</c>) and null on foot / in EditMode —
        /// consumers must null-check (ADR 0007). The drive it reports IS the controller's own
        /// <c>_throttle</c> (one state, one owner — never a UI-side copy).
        /// FLAG lead-architect: new Core contract (the ADR 0025 S1 helm-control seam).
        /// </summary>
        public static IHelmControl HelmControl { get; set; }

        /// <summary>
        /// The active hull's instrument GLASS seam (ADR 0025 S2): what is fitted in this boat's helm, what
        /// those instruments read from the live sim, and the per-hull display preferences. Sibling of
        /// <see cref="HelmControl"/> — that one MOVES the boat, this one REPORTS the dash — and same
        /// lifetime + discipline: OPTIONAL, NOT part of <see cref="Ready"/>, self-registered by the
        /// Boats-lane producer (<c>HelmControlRelay</c>), null on foot / in EditMode. Consumers must
        /// null-check (ADR 0007).
        /// FLAG lead-architect: new Core contract (the ADR 0025 S2 helm-instrument seam).
        /// </summary>
        public static IHelmInstruments HelmInstruments { get; set; }

        /// <summary>
        /// Where the fish are (ADR 0025 S3): the schools the fish finder DRAWS and the fishing path
        /// FISHES — one model, read by both, so the marks on the glass are the same object that changes
        /// the bite (the owner's honesty invariant, <see cref="IFishSchools"/>).
        ///
        /// <para><b>⚠ Unlike every other optional service on this class, this one is NEVER null — read it
        /// without a null check.</b> Absent a registered model it is <see cref="EmptyFishSchools"/>, an
        /// honest empty sea. That is deliberate and is what lets the finder and the fish be built in
        /// parallel: the UI host draws an empty sonar with no model in the project, and the gameplay lane
        /// swaps its own in with a single assignment here, changing nothing on the UI side. It is also the
        /// right SHIPPED behaviour in a bare art scene, in EditMode, and in a region with no fish authored
        /// yet — an empty sonar is the truth there; a <c>NullReferenceException</c> is not.</para>
        ///
        /// <para>Same lifetime as <see cref="ActiveBoat"/>/<see cref="HelmInstruments"/> otherwise: the
        /// producer self-registers, and <b>must clear this on disable</b> (assign null — the getter turns
        /// that back into the empty sea) so a torn-down region cannot leave a dead model behind.</para>
        /// FLAG lead-architect: new Core contract (the ADR 0025 S3 fish-school seam).
        /// </summary>
        public static IFishSchools FishSchools
        {
            get
            {
                IFishSchools model = _fishSchools;
                // ⚠️ NEVER `_fishSchools ?? EmptyFishSchools.Instance` here. A producer is very likely a
                // MonoBehaviour, and once its scene unloads the reference is Unity's FAKE-null: `??` and
                // `?.` bypass UnityEngine.Object's overloaded `==`, so a DESTROYED producer would read as
                // live and every call through it would throw MissingReferenceException. Compile-clean,
                // runtime-red. Ask the Unity way first, then fall back.
                if (model is UnityEngine.Object producer && producer == null) return EmptyFishSchools.Instance;
                return model != null ? model : EmptyFishSchools.Instance;
            }
            set => _fishSchools = value;
        }

        private static IFishSchools _fishSchools;

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
        /// The world rectangle the CURRENT region occupies — <c>RegionDef.WorldCenter</c> /
        /// <c>WorldSizeMeters</c>, the one authored extent the sea sprite, the flat backdrop, the
        /// shader's height bake and the displaced mesh all already read. Same writer and same
        /// lifetime as <see cref="CurrentRegionId"/>: the active region's anchor reports it on enable
        /// (which covers BOOT as well as a hop), and clears it on disable only if it still owns it.
        ///
        /// <para>Read by the camera's bounds clamp (<c>CameraFollow</c>), which lives on the
        /// PERSISTENT core and therefore cannot carry a baked rectangle of its own — it would be the
        /// start region's extent forever. OPTIONAL and NOT part of <see cref="Ready"/>: a zero-size
        /// rect is "no region has reported", under which the clamp is inert.</para>
        ///
        /// <para>⚠️ Deliberately NOT a second place to author an extent. Anything that needs the
        /// region's rectangle reads it from the <c>RegionDef</c> like everyone else; this is the
        /// travel-aware relay for consumers that outlive the region scene, nothing more.</para>
        /// FLAG lead-architect: new Core contract (sibling of the travel-aware region seam above).
        /// </summary>
        public static UnityEngine.Rect CurrentRegionBounds { get; set; }

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

        /// <summary>The wind-fetch model's tunables (ADR 0027 #1), same contract as
        /// <see cref="WaveField"/> including the <c>Config != null</c> discipline. Read by BOTH the
        /// shader bridge (which publishes them as globals) and every sim consumer that samples the
        /// field, so the lee the player sees is the lee the hull feels — the one-settings-instance
        /// rule that makes that true by construction rather than by hand. Falls back to
        /// <see cref="WaveFetchSettings.Default"/>, which is OFF.</summary>
        public static WaveFetchSettings WaveFetch =>
            Config != null ? Config.WaveFetch : WaveFetchSettings.Default;

        /// <summary>
        /// The wind-fetch amplitude envelope at a world position — the model resolved against the LIVE
        /// services (<see cref="TidalTerrain"/>, <see cref="Environment"/>, <see cref="WaveFetch"/>).
        /// Returns 1 (the exact passthrough) when the model is off, when there is no sim, or when no
        /// height map is wired.
        ///
        /// <para><b>Resolve this ONCE per consumer per tick</b> — at a hull's centre, not per probe.
        /// The march is <see cref="WaveFetch.MarchSteps"/> terrain samples and the envelope does not
        /// meaningfully vary across a hull (see <see cref="WaveFetch"/>'s slowly-varying-envelope
        /// note).</para>
        /// </summary>
        public static float FetchEnvelopeAt(UnityEngine.Vector2 worldPos)
        {
            WaveFetchSettings settings = WaveFetch;
            if (settings.Strength <= 0f) return 1f;      // cheapest OFF: no service reads at all
            IEnvironmentService environment = Environment;
            if (environment == null) return 1f;
            IGameClock clock = Clock;
            if (clock == null) return 1f;
            return WaveFetch_EnvelopeAt(worldPos, environment, clock, in settings);
        }

        private static float WaveFetch_EnvelopeAt(UnityEngine.Vector2 worldPos,
                                                  IEnvironmentService environment, IGameClock clock,
                                                  in WaveFetchSettings settings)
        {
            EnvironmentSample sample = environment.Sample();
            float waterLevel = environment.WaterLevelAt(clock.TotalSeconds);
            // ⚠️ Fully qualified on purpose: the SETTINGS property above is also called `WaveFetch`,
            // so the bare name binds to it and `WaveFetchSettings` has no EnvelopeAt.
            return HiddenHarbours.Core.WaveFetch.EnvelopeAt(worldPos, sample.WindVector, waterLevel,
                                                            TidalTerrain, in settings);
        }

        /// <summary>
        /// The world's day length in REAL seconds — the owner's <see cref="GameConfig.SecondsPerDay"/>
        /// when a config is wired, else <see cref="GameConfig.DefaultSecondsPerDay"/>. Same contract as
        /// <see cref="WaveField"/>, including the <c>Config != null</c> discipline (never <c>?.</c>/<c>??</c>
        /// on a <c>UnityEngine.Object</c>).
        ///
        /// <para><b>Why this accessor exists (2026-08-01).</b> The day length is the denominator under
        /// every in-game-hour pace in the game, and two self-installing hosts — <c>MoonCycle</c> and
        /// <c>DayNightController</c> — each carried their own serialized <c>1200f</c> copy of it because
        /// Core did not expose one. The moment the owner's tide-pacing ruling moved the dial to 1800 those
        /// copies became a real defect rather than untidiness: the moon's phase would advance 1.5× faster
        /// than the tide's spring/neap envelope, so the FULL MOON ON A SPRING TIDE alignment
        /// (vision-and-pillars §5.5, proved by <c>MoonMath.Phase01</c>) would drift apart within the first
        /// in-game week. One dial, one reader.</para>
        /// FLAG lead-architect: new Core contract (the clock-pace seam).
        /// </summary>
        public static float SecondsPerDay =>
            Config != null ? Config.SecondsPerDay : GameConfig.DefaultSecondsPerDay;

        /// <summary>The lunar month in in-game DAYS — the other half of the moon/tide alignment, same
        /// contract as <see cref="SecondsPerDay"/>. The tide's envelope reads
        /// <see cref="GameConfig.LunarMonthDays"/> directly; every VISUAL moon consumer reads it here, so
        /// the two can never be tuned apart.</summary>
        public static float LunarMonthDays =>
            Config != null ? Config.LunarMonthDays : GameConfig.DefaultLunarMonthDays;

        /// <summary>The tide table's owner tunables (VS-06) — how far the almanac page looks ahead and
        /// how finely it hunts each turn. Same contract as <see cref="WaveField"/>, including the
        /// <c>Config != null</c> discipline (never <c>?.</c>/<c>??</c> on a <c>UnityEngine.Object</c>).
        /// Falls back to <see cref="TideTableSettings.Default"/>, which reproduces the step sizes the
        /// HUD gauge and the editor tool already used.</summary>
        public static TideTableSettings TideTable =>
            Config != null ? Config.TideTable : TideTableSettings.Default;

        /// <summary>The notched helm throttle's tunables (ADR 0025 — detent counts, hold-to-repeat,
        /// the neutral snap window). Same contract as <see cref="WaveField"/>, including the
        /// <c>Config != null</c> discipline (never <c>?.</c>/<c>??</c> on a <c>UnityEngine.Object</c>).
        /// Read by the Boats input layer AND the UI overlay so a detent means the same thing to a
        /// key, a gamepad button and a mouse drag.</summary>
        public static HelmThrottleSettings HelmThrottle =>
            Config != null ? Config.HelmThrottle : HelmThrottleSettings.Default;

        /// <summary>The helm instrument overlay's tunables (ADR 0025 S1 — card placement, the
        /// click-to-focus enlargement, pointer radii). Same contract as <see cref="WaveField"/>,
        /// including the <c>Config != null</c> discipline.</summary>
        public static HelmOverlaySettings HelmOverlay =>
            Config != null ? Config.HelmOverlay : HelmOverlaySettings.Default;

        /// <summary>The depth sounder's tunables (ADR 0025 S2 — sounding cadence, the shallow-alarm
        /// defaults and travel, the flash rate, the card's placement, and the placeholder water
        /// temperature). Same contract as <see cref="WaveField"/>, including the <c>Config != null</c>
        /// discipline (never <c>?.</c>/<c>??</c> on a <c>UnityEngine.Object</c>). Read by BOTH the Boats
        /// relay (which takes the sounding) and the UI card (which draws it), so the instrument's
        /// behaviour and its picture can never be tuned apart.</summary>
        public static DepthSounderSettings DepthSounder =>
            Config != null ? Config.DepthSounder : DepthSounderSettings.Default;

        /// <summary>The fish finder's tunables (ADR 0025 S3 — the sonar upgrade). Same contract as
        /// <see cref="WaveField"/>, including the <c>Config != null</c> discipline (never <c>?.</c>/<c>??</c>
        /// on a <c>UnityEngine.Object</c>). Read by the UI card that draws the scale AND by the save
        /// migration's default (through <see cref="FishFinderSettings.Default"/>, which stays asset-free),
        /// so a fresh finder and a healed old save can never disagree about the range.</summary>
        public static FishFinderSettings FishFinder =>
            Config != null ? Config.FishFinder : FishFinderSettings.Default;

        /// <summary>The grabbable steering wheel's spin-feel tunables (ADR 0025 S2a — lock-to-lock
        /// turns, coast friction, self-centre, rim-grab pad). Same contract as
        /// <see cref="WaveField"/>, including the <c>Config != null</c> discipline.</summary>
        public static HelmWheelSettings HelmWheel =>
            Config != null ? Config.HelmWheel : HelmWheelSettings.Default;

        /// <summary>The masthead pennant's tunables (VS-19) — the boat's own wind instrument. Same
        /// contract as <see cref="WaveField"/>, including the <c>Config != null</c> discipline (never
        /// <c>?.</c>/<c>??</c> on a <c>UnityEngine.Object</c>). Falls back to
        /// <see cref="TelltaleSettings.Default"/> so an unwired art scene still flies a burgee.</summary>
        public static TelltaleSettings Telltale =>
            Config != null ? Config.Telltale : TelltaleSettings.Default;

        /// <summary>The mesh fleet's keyline gate (ADR 0031 — the keyline retirement): does the
        /// fullscreen resolve still flood the legacy 1 px outline around every mesh hull? Same contract
        /// as <see cref="WaveField"/>, including the <c>Config != null</c> discipline (never
        /// <c>?.</c>/<c>??</c> on a <c>UnityEngine.Object</c>). Falls back to
        /// <see cref="GameConfig.DefaultHullKeylineFlood"/> — <b>OFF, the retired-outline style</b> — so
        /// an unwired scene ships the new look, never the legacy one. Read per camera per frame by
        /// <c>IsoFacetHullFeature</c>; the GPU oracle fixtures force it ON through this same dial so the
        /// resolve's rig-verbatim claim stays pinned (ADR 0022 phase 3 / ADR 0031).
        /// FLAG lead-architect: new Core contract (the ADR 0031 world-art style seam).</summary>
        public static bool HullKeylineFlood =>
            Config != null ? Config.HullKeylineFlood : GameConfig.DefaultHullKeylineFlood;

        /// <summary>
        /// Rebuilds a <see cref="CatchItem"/> from a stable species id — the save-restore's species
        /// resolver (M1 §7.3: the save carries the reference, the Def's stats re-cache at load).
        /// Registered by the Fishing module's registrar bootstrap; OPTIONAL and NOT part of
        /// <see cref="Ready"/> (null in EditMode / a stripped build) — consumers skip unresolvable
        /// records rather than throwing.
        /// FLAG lead-architect: new Core contract (the hold-persistence species seam, M1 §7.3).
        /// </summary>
        public static ICatchItemFactory CatchFactory { get; set; }

        /// <summary>
        /// The player's four bus volumes (M1 §7.8's settings sheet). Registered by the self-installing
        /// <c>AudioDirector</c>, which owns the faders; read/written by the settings UI, which references
        /// only Core and therefore cannot reach the Audio module directly (rule 4). OPTIONAL and NOT part
        /// of <see cref="Ready"/> — null in EditMode and before the director installs, so the sheet shows
        /// its rows disabled rather than pretending to move a mix that isn't there.
        /// FLAG lead-architect: new Core contract (the shell's audio seam).
        /// </summary>
        public static IAudioMix AudioMix { get; set; }

        public static bool Ready => Clock != null && Environment != null;

        /// <summary>Clear references (scene teardown / tests).</summary>
        public static void Reset()
        {
            Clock = null;
            Environment = null;
            Wallet = null;
            Licenses = null;
            ActiveBoat = null;
            HelmControl = null;
            HelmInstruments = null;
            FishSchools = null;          // → EmptyFishSchools.Instance; this property is never null
            Save = null;
            TidalTerrain = null;
            CurrentRegionId = null;
            CurrentRegionBounds = default;
            Config = null;
            CatchFactory = null;
            AudioMix = null;
        }
    }
}
