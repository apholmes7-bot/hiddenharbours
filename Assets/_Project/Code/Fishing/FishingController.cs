using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // CatchLicensePolicy + LicenseDef: the catch-side licence gate (St Peters)

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The fishing interaction (VS-13 core loop + the Rod Fishing v2 FLICK-CAST, design/
    /// rod-fishing-v2-brainstorm.md §2.2). A hold/drag/release gesture opens the loop, then the small
    /// state machine runs it home:
    ///
    ///   Idle ──press──▶ WindBack ──release──▶ Cast ──(flight)──▶ Waiting ──(bite delay)──▶ Bite
    ///        ◀── result (Landed / Snapped / NoBite, brief) ◀── Fighting/Tending ◀──press/auto-hook──┘
    ///
    /// <para><b>The flick-cast (replaces the old press-to-cast / the two discrete casts).</b> Holding the
    /// action starts the WIND-BACK: the pointer is sampled while the player drags the mouse behind the
    /// character (the rod rig follows it — the baked Fisher_castBack sheet animates off the WindBack
    /// phase). Releasing evaluates the whole gesture through the pure <see cref="FlickCastMath"/>:
    /// direction = the flick vector, power = sweep speed + length (capped per rod — the cap is
    /// <see cref="GameConfig.FlickCast"/> data today, per-gear later), quality = release timing. A good
    /// flick flies the line (Cast phase) out to <see cref="LastCast"/>.LandingPoint; a bad one is just a
    /// SHORT cast, and a gesture that never wound back simply doesn't fly — reel in, recast, no penalty.</para>
    ///
    /// The WHICH-fish decision is the existing <see cref="CatchResolver"/>, run at bite-time — the
    /// mini-game is only how an already-resolved catch is delivered (we don't re-implement catch logic).
    /// A clean land adds the fish to the boat's hold via the unchanged <see cref="IHold"/> /
    /// <see cref="FishCaught"/> path. A snap is cozy: "it threw the hook", no penalty.
    ///
    /// State is published read-only via the Core <see cref="FishingStateChanged"/> signal so the
    /// transient rod gauge (and, later, ui-ux's formal HUD — VS-14) can render it WITHOUT referencing
    /// this module. The fight maths live in the testable <see cref="FishFight"/> POCO.
    ///
    /// Input is fed each frame by DevFishingInput now (Space/mouse + pointer), the InputService later —
    /// this component owns no input; it just advances on <see cref="Tick"/>.
    /// </summary>
    public class FishingController : MonoBehaviour
    {
        [Header("Region & gear")]
        [SerializeField] private string _regionId = "region.coddle_cove";
        [SerializeField] private Gear _gear = Gear.Handline;
        [Tooltip("The fish that can be caught in this region (region scenes provide these).")]
        [SerializeField] private FishSpeciesDef[] _regionFish;
        [Tooltip("The authored licences in play (e.g. the cod licence) — supplied by the region/world the " +
                 "same way the region fish are. Used ONLY to look up which licence (if any) a species " +
                 "requires at land-time (CatchLicensePolicy); the player's held licences come from the " +
                 "Core ILicenseService. Null/empty = nothing is licence-gated (all species land freely).")]
        [SerializeField] private LicenseDef[] _licenses;
        [Tooltip("A GameObject carrying an IHold (the boat's ShipHold).")]
        [SerializeField] private GameObject _holdProvider;
        [Tooltip("0 = time-seeded RNG; set non-zero for reproducible bites/fights in testing.")]
        [SerializeField] private int _rngSeed = 0;
        [Tooltip("Shared GameConfig for the owner's flick-cast tuning (GameConfig.FlickCast — no magic " +
                 "numbers, rule 6). Left unset, the code falls back to FlickCastSettings.Default, so a " +
                 "test/greybox rig without a config still casts.")]
        [SerializeField] private GameConfig _config;

        [Header("Interaction tuning (forgiving — no magic numbers)")]
        [Tooltip("Shortest wait for a bite after casting (real seconds).")]
        [SerializeField] private float _minBiteDelay = 1.5f;
        [Tooltip("Longest wait for a bite after casting (real seconds).")]
        [SerializeField] private float _maxBiteDelay = 4.0f;
        [Tooltip("Forgiving hook window after a bite: press to hook early, else it auto-hooks (real seconds).")]
        [SerializeField] private float _hookWindow = 1.2f;
        [Tooltip("How long a Landed/Snapped/NoBite result is shown before returning to Idle (real seconds).")]
        [SerializeField] private float _resultDisplay = 1.5f;

        private IHold _hold;
        private System.Random _rng;

        // ---- live interaction state ---------------------------------------------------------
        private FishingPhase _phase = FishingPhase.Idle;
        private float _phaseTimer;
        private bool _actionWasHeld;
        private FishFight _fight;
        private FishSpeciesDef _pendingFish;
        private float _pendingWeight;
        private FishingState _state = FishingState.Idle;

        // ---- flick-cast gesture state (see the class doc; the maths is FlickCastMath) --------
        // A preallocated RING of gesture samples — no per-frame GC in the input path (rule 7). The
        // capacity is a performance bound, not a feel dial (≈4 s of gesture at 60 Hz; a stationary
        // pointer keeps re-recording its position, so the wind-back apex is never aged out of a live
        // gesture by holding still).
        private const int GestureCapacity = 256;
        private readonly FlickSample[] _gestureRing = new FlickSample[GestureCapacity];
        private readonly FlickSample[] _gestureScratch = new FlickSample[GestureCapacity]; // linearized for Evaluate
        private int _gestureHead;      // next write slot in the ring
        private int _gestureCount;     // how many valid entries the ring holds (≤ capacity)
        private float _gestureClock;   // seconds since the wind-back began (integrated from injected dt)
        private FlickCastResult _lastCast;

        /// <summary>Read-only snapshot of the live interaction (for tests / direct readers).</summary>
        public FishingState State => _state;
        public FishingPhase Phase => _phase;
        public Gear Gear { get => _gear; set => _gear = value; }

        /// <summary>The resolved result of the most recent successful flick (direction, power, quality,
        /// landing point). Valid from the Cast phase on; presentation (line/bobber art) reads the landing
        /// point here — it is deliberately NOT added to the Core FishingState struct.</summary>
        public FlickCastResult LastCast => _lastCast;

        private void Awake()
        {
            _rng = _rngSeed == 0 ? new System.Random() : new System.Random(_rngSeed);
            EnsureHold();
            if (_hold == null)
                Debug.LogWarning("[FishingController] No IHold found on the hold provider.", this);
        }

        private void Update() { /* input-driven: DevFishingInput (or the InputService) calls Tick. */ }

        /// <summary>Legacy two-arg tick (no pointer). The flick-cast needs a pointer to aim, so a press
        /// here can never START a cast — but every in-flight phase (waiting/bite/fight/result) still
        /// advances normally, which is exactly what the gate-forced <c>held=false</c> path relies on.</summary>
        public void Tick(float dt, bool actionHeld) => Tick(dt, actionHeld, default, pointerValid: false);

        /// <summary>
        /// Advance the interaction by <paramref name="dt"/> seconds. <paramref name="actionHeld"/> is the
        /// single fishing action's held state (Space/mouse-button down). The flick-cast reads the edges:
        /// a press with a valid pointer starts the WIND-BACK, the held drag is sampled, and the RELEASE
        /// evaluates the gesture and (if it flew) casts. The hook beat fires on the press edge; the fight
        /// reads the continuous hold as reel-vs-ease.
        /// </summary>
        /// <param name="pointerWorld">The pointer in world space (the mouse under the camera).</param>
        /// <param name="pointerValid">False when no pointer is available this tick (no mouse/camera) —
        /// the gesture then can't start, and a wind-back in progress simply records nothing.</param>
        public void Tick(float dt, bool actionHeld, Vector2 pointerWorld, bool pointerValid)
        {
            bool pressed = actionHeld && !_actionWasHeld;
            _actionWasHeld = actionHeld;

            switch (_phase)
            {
                case FishingPhase.Idle:
                    // A weighted rig needs NO pointer — the press drops the rig straight down (§2.1:
                    // "no cast — drop and read the column"); only the cast GESTURE needs the mouse.
                    // BeginWindBack runs the hold checks first, then forks to BeginDrop before it
                    // touches the pointer, so the pointer gate applies only to the gesture path.
                    if (pressed && (pointerValid ||
                        DepthDropMath.IsWeightedRig(_gear, _rigWeightKg, DepthSettings.WeightedHandlineMinKg)))
                        BeginWindBack(pointerWorld);
                    break;

                case FishingPhase.WindBack:
                    if (!actionHeld) ReleaseFlick();
                    else RecordGestureSample(dt, pointerWorld, pointerValid);
                    break;

                case FishingPhase.Cast:
                    // The line is in flight. Touchdown hands off to the waiting/bite flow at the landing
                    // point. (Depth-drop seam: a weighted rig's Sinking phase slots in HERE, between
                    // touchdown and Waiting — the sink/column game is its own build, not this one.)
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) BeginWaiting();
                    break;

                case FishingPhase.Waiting:
                    if (_depthGame) TickDepthHold(dt, actionHeld);   // hold = reel up slightly (§2.3 step 4)
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) OnBite();
                    break;

                case FishingPhase.Sinking:
                    TickSinking(dt, pressed);                        // the depth drop (§2.3)
                    break;

                case FishingPhase.Bite:
                    _phaseTimer -= dt;
                    if (pressed || _phaseTimer <= 0f) BeginFight(); // press hooks early; else auto-hook (forgiving)
                    break;

                case FishingPhase.Fighting:
                case FishingPhase.Tending:
                    _fight.Tick(dt, actionHeld);                    // hold = reel/tend
                    if (_fight.Result == FishFightResult.Landed)      OnLanded();
                    else if (_fight.Result == FishFightResult.Snapped) OnSnapped();
                    else Emit(_phase, _fight.Tension01, _fight.Landing01);
                    break;

                case FishingPhase.Landed:
                case FishingPhase.Snapped:
                case FishingPhase.NoBite:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) ToIdle();
                    break;
            }
        }

        /// <summary>Wire the controller without the scene lifecycle (tests / editor). Seed 0 = time-seeded.
        /// Licences default to none (nothing gated); use the overload to supply the licence data.</summary>
        public void Configure(IHold hold, FishSpeciesDef[] regionFish, string regionId, Gear gear, int seed)
            => Configure(hold, regionFish, regionId, gear, seed, null);

        /// <summary>Wire the controller, including the authored licence data used for the land-time gate
        /// (the cod licence gates cod). The player's HELD licences still come from
        /// <see cref="GameServices.Licenses"/> at land-time — this is only the species→licence mapping.</summary>
        public void Configure(IHold hold, FishSpeciesDef[] regionFish, string regionId, Gear gear, int seed,
                              LicenseDef[] licenses)
        {
            _hold = hold;
            _regionFish = regionFish;
            _regionId = regionId;
            _gear = gear;
            _licenses = licenses;
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
            _phase = FishingPhase.Idle;
            _state = FishingState.Idle;
            _actionWasHeld = false;
            _gestureHead = 0;
            _gestureCount = 0;
            _gestureClock = 0f;
            _lastCast = FlickCastResult.NoCast;
            ResetDepthGame();
        }

        /// <summary>Wire the controller including the shared GameConfig (the owner's FlickCast tuning).
        /// Null falls back to <see cref="FlickCastSettings.Default"/>.</summary>
        public void Configure(IHold hold, FishSpeciesDef[] regionFish, string regionId, Gear gear, int seed,
                              LicenseDef[] licenses, GameConfig config)
        {
            Configure(hold, regionFish, regionId, gear, seed, licenses);
            _config = config;
        }

        // ---- phase transitions --------------------------------------------------------------

        #region The flick-cast (WindBack → Cast → touchdown; the maths is FlickCastMath)

        /// <summary>The press edge: start winding the rod back and sampling the drag. The hold-full
        /// refusal lives here (same UX as the retired press-to-cast: a full hold never starts a cast).</summary>
        private void BeginWindBack(Vector2 pointerWorld)
        {
            EnsureHold();
            if (_hold == null) { Debug.LogWarning("[Fishing] No hold to land into.", this); return; }
            if (_hold.UsedUnits >= _hold.CapacityUnits)
            {
                Debug.Log("[Fishing] Hold is full — head in and sell.");
                return; // stay Idle; nothing to cast into
            }

            _pendingFish = null;
            _pendingWeight = 0f;

            // Gear decides the branch (v2 §2.1): a weighted rig DROPS and reads the column — no cast,
            // no gesture, no bobber (rebase resolution: the depth branch moved here from the retired
            // press-to-cast entry — the press itself starts the drop). Cast gear winds back as below.
            if (DepthDropMath.IsWeightedRig(_gear, _rigWeightKg, DepthSettings.WeightedHandlineMinKg))
            {
                BeginDrop();
                return;
            }

            _gestureHead = 0;
            _gestureCount = 0;
            _gestureClock = 0f;
            RecordGestureSample(0f, pointerWorld, pointerValid: true);
            Emit(FishingPhase.WindBack, 0f, 0f);   // the rod rig / Fisher_castBack sheet animates off this
        }

        /// <summary>One tick of the held drag: advance the gesture clock and record the pointer into the
        /// ring (overwrite-oldest — see the capacity note on the field). An invalid pointer records
        /// nothing but keeps time, so a hiccup mid-gesture degrades, never corrupts.</summary>
        private void RecordGestureSample(float dt, Vector2 pointerWorld, bool pointerValid)
        {
            _gestureClock += dt;
            if (!pointerValid) return;
            _gestureRing[_gestureHead] = new FlickSample(pointerWorld, _gestureClock);
            _gestureHead = (_gestureHead + 1) % GestureCapacity;
            if (_gestureCount < GestureCapacity) _gestureCount++;
        }

        /// <summary>The release edge: linearize the ring and resolve the whole gesture through the pure
        /// <see cref="FlickCastMath"/>. A gesture that never became a cast (no wind-back / a twitch) is
        /// the cozy nothing — back to Idle, recast at will. A cast flies the line (Cast phase) for
        /// distance/flight-speed seconds, then touches down into the waiting flow.</summary>
        private void ReleaseFlick()
        {
            int start = _gestureCount < GestureCapacity ? 0 : _gestureHead; // oldest entry in the ring
            for (int i = 0; i < _gestureCount; i++)
                _gestureScratch[i] = _gestureRing[(start + i) % GestureCapacity];

            FlickCastSettings s = _config != null ? _config.FlickCast : FlickCastSettings.Default;
            // The per-gear cap seam: today the cap is the GameConfig field; a rod/tackle GearDef's own
            // cap slots in here later (P4 upgrades extend the reach) without touching the maths.
            float cap = s.MaxCastDistanceMetres;

            FlickCastResult cast = FlickCastMath.Evaluate(
                _gestureScratch, _gestureCount, transform.position, in s, cap);
            _gestureCount = 0;

            if (!cast.IsCast)
            {
                Debug.Log("[Fishing] The rod never loaded up — no cast. Wind back and flick again.");
                ToIdle();
                return;
            }

            _lastCast = cast;
            _phaseTimer = cast.DistanceMetres / Mathf.Max(0.01f, s.LineFlightMetresPerSec);
            Emit(FishingPhase.Cast, 0f, 0f);
        }

        /// <summary>Touchdown: the line is in the water at <see cref="LastCast"/>.LandingPoint — start
        /// the (unchanged) wait-for-a-bite flow. (The weighted-rig depth branch never reaches here —
        /// it forks to <see cref="BeginDrop"/> at the press, in <see cref="BeginWindBack"/>.)</summary>
        private void BeginWaiting()
        {
            _phaseTimer = RandomBiteDelay();
            Emit(FishingPhase.Waiting, 0f, 0f);
        }

        /// <summary>Abort an un-flown wind-back (the input gate closed — a modal opened, a haul took the
        /// key). Cozy: nothing was in the water yet, so nothing is lost — straight back to Idle. Any
        /// LATER phase is untouched (an in-flight cast/bite/fight still eases to its own resolution).</summary>
        public void CancelCastGesture()
        {
            if (_phase != FishingPhase.WindBack) return;
            _gestureCount = 0;
            ToIdle();
        }

        #endregion

        private void OnBite()
        {
            // Resolve WHICH fish at bite-time via the existing resolver (we don't rewrite catch logic).
            CatchContext ctx = BuildContext();
            FishSpeciesDef fish = CatchResolver.Resolve(_regionFish, in ctx, DepthSettings, _rng);
            if (fish == null)
            {
                _phaseTimer = _resultDisplay;
                Debug.Log("[Fishing] Nothing biting here, now.");
                Emit(FishingPhase.NoBite, 0f, 0f);
                return;
            }

            _pendingFish = fish;
            _pendingWeight = CatchResolver.RollWeight(fish, _rng);
            _phaseTimer = _hookWindow;
            Emit(FishingPhase.Bite, 0f, 0f);
        }

        private void BeginFight()
        {
            var tuning = FishFightTuning.For(
                _pendingFish.Category, _pendingWeight, _pendingFish.MinWeightKg, _pendingFish.MaxWeightKg);
            _fight = new FishFight(in tuning, _rng);

            bool tend = FishFightTuning.IsHandGathered(_pendingFish.Category);
            Emit(tend ? FishingPhase.Tending : FishingPhase.Fighting, 0f, 0f);
        }

        private void OnLanded()
        {
            FishSpeciesDef fish = _pendingFish;
            float tension = _fight != null ? _fight.Tension01 : 0f;
            _fight = null;

            // --- Licence gate (St Peters, P5 cozy): some species may only be LANDED with the right
            // licence (the rod takes cod only once you hold the cod licence). The mapping species→licence
            // is the authored licence data (CatchLicensePolicy); the player's held licences come from the
            // Core ILicenseService — Fishing never references Economy's concrete service. Unlicensed is
            // cozy: the fish slips back (the existing no-penalty "released" result), nudging you to the
            // harbourmaster. Ungated species are unaffected; a null licence service fails closed only for
            // gated species, so unlicensed cod can't be taken before you can hold a licence.
            if (!CatchLicensePolicy.MayLand(fish.Id, _licenses, GameServices.Licenses))
            {
                _phaseTimer = _resultDisplay;
                Debug.Log($"[Fishing] {fish.DisplayName} — you need the licence to land that. (Slipped it back, no harm done.)");
                Emit(FishingPhase.Snapped, tension, 1f);   // reuse the cozy "it got away" result; nothing added
                return;
            }

            var item = new CatchItem(fish.Id, fish.DisplayName, fish.Category,
                                     _pendingWeight, fish.BaseValue, fish.SupplyElasticity);

            if (_hold != null && _hold.TryAdd(item))
            {
                EventBus.Publish(new FishCaught(item));          // unchanged land path
                Debug.Log($"[Fishing] Landed {item}!");
            }
            else
            {
                Debug.Log("[Fishing] Landed it, but the hold's full — nowhere to stow it.");
            }

            _phaseTimer = _resultDisplay;
            Emit(FishingPhase.Landed, tension, 1f);
        }

        private void OnSnapped()
        {
            float landing = _fight != null ? _fight.Landing01 : 0f;
            _fight = null;
            _phaseTimer = _resultDisplay;
            Debug.Log("[Fishing] The line ran slack — it threw the hook. (No harm done.)");
            Emit(FishingPhase.Snapped, 1f, landing);
        }

        private void ToIdle()
        {
            _pendingFish = null;
            _pendingWeight = 0f;
            _fight = null;
            ResetDepthGame();
            Emit(FishingPhase.Idle, 0f, 0f);
        }

        // ---- helpers ------------------------------------------------------------------------

        private void Emit(FishingPhase phase, float tension, float landing)
        {
            _phase = phase;
            string id = _pendingFish != null ? _pendingFish.Id : null;
            string name = _pendingFish != null ? _pendingFish.DisplayName : null;
            FishCategory cat = _pendingFish != null ? _pendingFish.Category : FishCategory.InshoreGroundfish;
            _state = new FishingState(phase, tension, landing, id, name, cat, _pendingWeight,
                                      depth01: DepthRead01(phase), slackWindowOpen: BottomSlackOpen(phase),
                                      rodBend01: 0f);
            EventBus.Publish(new FishingStateChanged(_state));
        }

        private void EnsureHold()
        {
            if (_hold == null && _holdProvider != null)
                _hold = _holdProvider.GetComponent<IHold>();
        }

        private float RandomBiteDelay()
        {
            float lo = Mathf.Min(_minBiteDelay, _maxBiteDelay);
            float hi = Mathf.Max(_minBiteDelay, _maxBiteDelay);
            return lo + (float)(_rng?.NextDouble() ?? 0.0) * (hi - lo);
        }

        #region Depth drop (Rod Fishing v2 Wave 2 — design §2.1/§2.3; maths in DepthDropMath)
        // The weighted-rig branch: no cast — the rig DROPS (Sinking), the player counts the fall
        // (heavier = faster), clicks to re-engage the reel and hold a band (→ Waiting at that depth), or
        // lets it bottom out (line goes slack — Depth01 = 1 + SlackWindowOpen, the "you felt the floor"
        // tell the Art lane's RodLineMath.SlackOvershoot pops on) and then HOLDS to reel up slightly into
        // the off-floor sweet spot. The held depth feeds the catch roll through BuildContext (a soft
        // weight — CatchResolver.EffectiveWeight). Everything here is deliberately self-contained so the
        // parallel flick-cast wave (WindBack/Cast) rebases past it trivially.

        // The owner's DepthDrop tunables ride the SAME serialized GameConfig field the flick-cast uses
        // (declared with the cast fields above) — one config, both settings blocks (rebase dedupe).

        [Header("Depth drop (Rod Fishing v2 — the weighted-rig branch)")]
        [Tooltip("The rig/lure weight tied on (kg) — HEAVIER FALLS FASTER, so this is the count-the-fall " +
                 "tactical choice (§2.3, owner decision #4). A Handline at/above the config's " +
                 "WeightedHandlineMinKg fishes the depth branch; below it, the cast/bobber branch. " +
                 "Jig/Longline gear always fish the depth branch. Gear/tackle UI drives this later.")]
        [Min(0f)] [SerializeField] private float _rigWeightKg = 0.05f;
        [Tooltip("Dev/test override for the bathymetric water column here (m). < 0 = read the real " +
                 "bathymetry (TidalTerrain + water level — the TidalWalkability composition).")]
        [SerializeField] private float _waterColumnOverrideM = -1f;

        // Live depth-game state. NOT saved — the drop is a live interaction, re-run from input.
        private bool _depthGame;    // this cast is the weighted-rig branch
        private float _depthM;      // how deep the rig is (m)
        private float _floorM;      // floor of the reachable band (m) — min(bathymetry, line)
        private bool _bottomed;     // resting on the floor (the slack tell)

        /// <summary>The rig depth read, 0 surface .. 1 floor — what <c>FishingState.Depth01</c> publishes.</summary>
        public float Depth01 => _depthGame ? DepthDropMath.Depth01(_depthM, _floorM) : 0f;

        private DepthDropSettings DepthSettings => _config != null ? _config.DepthDrop : DepthDropSettings.Default;

        /// <summary>Wire the depth game without the scene lifecycle (tests / dev): the rig weight, a fixed
        /// water column (PositiveInfinity = uncapped open water), and optionally the config. Call after
        /// <see cref="Configure(IHold, FishSpeciesDef[], string, Gear, int)"/>.</summary>
        public void ConfigureDepthDrop(float rigWeightKg, float waterColumnMeters = float.PositiveInfinity,
                                       GameConfig config = null)
        {
            _rigWeightKg = rigWeightKg;
            _waterColumnOverrideM = waterColumnMeters;   // +Infinity = uncapped; FloorMeters clamps to the line
            if (config != null) _config = config;
        }

        /// <summary>Start the drop: freeze this spot's reachable band, spool loose, publish Sinking.</summary>
        private void BeginDrop()
        {
            _depthGame = true;
            _depthM = 0f;
            _bottomed = false;
            _floorM = DepthDropMath.FloorMeters(WaterColumnMeters(), DepthSettings.MaxLineMeters);
            Emit(FishingPhase.Sinking, 0f, 0f);
        }

        /// <summary>The Sinking tick: integrate the fall (weight → speed, DepthDropMath.SinkSpeedMps), then
        /// either a CLICK re-engages the reel (hold this band → Waiting), or the floor arrives and the line
        /// goes slack (→ Waiting, bottomed). Publishes every tick so Depth01 is a continuous read.</summary>
        private void TickSinking(float dt, bool pressed)
        {
            DepthDropSettings s = DepthSettings;
            float speed = DepthDropMath.SinkSpeedMps(_rigWeightKg, s.SinkSpeedPerKgMps,
                                                     s.MinSinkSpeedMps, s.MaxSinkSpeedMps);
            _depthM = DepthDropMath.FallStep(_depthM, speed, dt, _floorM);

            if (pressed) { EngageReel(); return; }                       // click → hold this band (§2.3 step 2)
            if (DepthDropMath.IsBottomed(_depthM, _floorM)) { OnBottomedOut(); return; }
            Emit(FishingPhase.Sinking, 0f, 0f);
        }

        /// <summary>The Waiting-with-a-held-depth tick: HOLD reels the rig UP a little (the lift off the
        /// floor into the sweet window — §2.3 step 4). The reel is engaged, so releasing just holds the
        /// band; the bite timer runs in the caller as ever.</summary>
        private void TickDepthHold(float dt, bool actionHeld)
        {
            if (!actionHeld) return;
            _depthM = DepthDropMath.ReelStep(_depthM, DepthSettings.ReelUpMps, dt);
            _bottomed = DepthDropMath.IsBottomed(_depthM, _floorM);
            Emit(FishingPhase.Waiting, 0f, 0f);
        }

        /// <summary>Click during the fall: the reel re-engages and the rig holds this band — Waiting, at
        /// the held Depth01, bite timer running against the depth-weighted pool.</summary>
        private void EngageReel()
        {
            _bottomed = DepthDropMath.IsBottomed(_depthM, _floorM);
            _phaseTimer = RandomBiteDelay();
            Emit(FishingPhase.Waiting, 0f, 0f);
        }

        /// <summary>The rig hit the floor of the reachable band: the line goes SLACK — Depth01 = 1 and
        /// SlackWindowOpen turns on in one publish (the transition the Art lane pops its overshoot from).
        /// The reel winds on; the bait sits on the mud until the player lifts it.</summary>
        private void OnBottomedOut()
        {
            _bottomed = true;
            _phaseTimer = RandomBiteDelay();
            Emit(FishingPhase.Waiting, 0f, 0f);
        }

        private void ResetDepthGame()
        {
            _depthGame = false;
            _depthM = 0f;
            _floorM = 0f;
            _bottomed = false;
        }

        /// <summary>The bathymetric water column here (m) — the TidalWalkability composition (water level −
        /// ground elevation) over the Core seams, never the World/Player concretes (rule 4). Either service
        /// absent → no authored bathymetry → an uncapped column (the band is line-length-capped only, the
        /// same "service absent → gate off" posture). The dev/test override wins when set.</summary>
        private float WaterColumnMeters()
        {
            if (_waterColumnOverrideM >= 0f) return _waterColumnOverrideM;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            IEnvironmentService env = GameServices.Environment;
            if (terrain == null || env == null) return float.PositiveInfinity;
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            return TidalExposure.WaterDepth(env.WaterLevelAt(now), terrain.ElevationAt(transform.position));
        }

        /// <summary>What <c>FishingState.Depth01</c> should carry for a publish: the live rig depth during
        /// the depth game (including through bite/fight — the rig IS at that depth), 0 on the legacy path
        /// and once the interaction resolves back to Idle.</summary>
        private float DepthRead01(FishingPhase phase)
            => _depthGame && phase != FishingPhase.Idle ? DepthDropMath.Depth01(_depthM, _floorM) : 0f;

        /// <summary>What <c>FishingState.SlackWindowOpen</c> should carry for a publish: the BOTTOM slack
        /// tell — true only while the bottomed rig rests on the floor pre-bite (Sinking/Waiting). Fight
        /// phases own the field's fish-slack meaning in the fight waves; results/idle never signal it.</summary>
        private bool BottomSlackOpen(FishingPhase phase)
            => _depthGame && _bottomed && (phase == FishingPhase.Sinking || phase == FishingPhase.Waiting);

        #endregion

        private CatchContext BuildContext()
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            float tide = env != null ? env.Sample().TideHeight : 0f;
            float hour = clock != null ? clock.HourOfDay : 12f;
            Season season = clock != null ? clock.Season : Season.HighSummer;
            return new CatchContext(_regionId, tide, hour, season, _gear,
                                    _depthGame ? _depthM : CatchContext.NoDepth, _floorM);
        }
    }
}
