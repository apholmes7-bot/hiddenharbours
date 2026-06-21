using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The cozy one-thumb fishing interaction (VS-13; design/ux-and-mobile-controls.md §3.3).
    /// A single hold/release action drives the whole thing through a small state machine:
    ///
    ///   Idle ──press──▶ Waiting ──(bite delay)──▶ Bite ──press/auto-hook──▶ Fighting/Tending
    ///        ◀── result (Landed / Snapped / NoBite, brief) ◀──────────────┘
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
    /// Input is fed each frame by DevFishingInput now (Space), the InputService later — this component
    /// owns no input; it just advances on <see cref="Tick"/>.
    /// </summary>
    public class FishingController : MonoBehaviour
    {
        [Header("Region & gear")]
        [SerializeField] private string _regionId = "region.coddle_cove";
        [SerializeField] private Gear _gear = Gear.Handline;
        [Tooltip("The fish that can be caught in this region (region scenes provide these).")]
        [SerializeField] private FishSpeciesDef[] _regionFish;
        [Tooltip("A GameObject carrying an IHold (the boat's ShipHold).")]
        [SerializeField] private GameObject _holdProvider;
        [Tooltip("0 = time-seeded RNG; set non-zero for reproducible bites/fights in testing.")]
        [SerializeField] private int _rngSeed = 0;

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

        /// <summary>Read-only snapshot of the live interaction (for tests / direct readers).</summary>
        public FishingState State => _state;
        public FishingPhase Phase => _phase;
        public Gear Gear { get => _gear; set => _gear = value; }

        private void Awake()
        {
            _rng = _rngSeed == 0 ? new System.Random() : new System.Random(_rngSeed);
            EnsureHold();
            if (_hold == null)
                Debug.LogWarning("[FishingController] No IHold found on the hold provider.", this);
        }

        private void Update() { /* input-driven: DevFishingInput (or the InputService) calls Tick. */ }

        /// <summary>
        /// Advance the interaction by <paramref name="dt"/> seconds. <paramref name="actionHeld"/> is the
        /// single fishing action's held state (Space down / Action button held). Casting and the hook beat
        /// fire on the press edge; the fight reads the continuous hold as reel-vs-ease.
        /// </summary>
        public void Tick(float dt, bool actionHeld)
        {
            bool pressed = actionHeld && !_actionWasHeld;
            _actionWasHeld = actionHeld;

            switch (_phase)
            {
                case FishingPhase.Idle:
                    if (pressed) BeginCast();
                    break;

                case FishingPhase.Waiting:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) OnBite();
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

        /// <summary>Wire the controller without the scene lifecycle (tests / editor). Seed 0 = time-seeded.</summary>
        public void Configure(IHold hold, FishSpeciesDef[] regionFish, string regionId, Gear gear, int seed)
        {
            _hold = hold;
            _regionFish = regionFish;
            _regionId = regionId;
            _gear = gear;
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
            _phase = FishingPhase.Idle;
            _state = FishingState.Idle;
            _actionWasHeld = false;
        }

        // ---- phase transitions --------------------------------------------------------------

        private void BeginCast()
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
            _phaseTimer = RandomBiteDelay();
            Emit(FishingPhase.Waiting, 0f, 0f);
        }

        private void OnBite()
        {
            // Resolve WHICH fish at bite-time via the existing resolver (we don't rewrite catch logic).
            CatchContext ctx = BuildContext();
            FishSpeciesDef fish = CatchResolver.Resolve(_regionFish, in ctx, _rng);
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
            Emit(FishingPhase.Idle, 0f, 0f);
        }

        // ---- helpers ------------------------------------------------------------------------

        private void Emit(FishingPhase phase, float tension, float landing)
        {
            _phase = phase;
            string id = _pendingFish != null ? _pendingFish.Id : null;
            string name = _pendingFish != null ? _pendingFish.DisplayName : null;
            FishCategory cat = _pendingFish != null ? _pendingFish.Category : FishCategory.InshoreGroundfish;
            _state = new FishingState(phase, tension, landing, id, name, cat, _pendingWeight);
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

        private CatchContext BuildContext()
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            float tide = env != null ? env.Sample().TideHeight : 0f;
            float hour = clock != null ? clock.HourOfDay : 12f;
            Season season = clock != null ? clock.Season : Season.HighSummer;
            return new CatchContext(_regionId, tide, hour, season, _gear);
        }
    }
}
