using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE PLAYABLE HAUL (trap-fishing arc Build 4, redesigned Build 6) — lay the boat alongside a set trap,
    /// interact to start hauling, then <b>haul WITH the swell</b> until the pot surfaces (the owner's
    /// verdict: a richer, faster, DIEGETIC action, not a rhythm tap). This is the driver that turns the pure
    /// <see cref="TrapHaulMath"/> into a live interaction: it reads the swell's lift/drop under the buoy, owns
    /// the HOLD input, drives the world rope (its taut/slack shape + a strain shudder), and on surface lands
    /// the trap's already-deterministic catch (Build 3) into the active boat's hold.
    ///
    /// <list type="bullet">
    ///   <item><b>Hold on the lift, ease on the fall.</b> When the shared deterministic wave field
    ///   (<see cref="WaveMath"/>) LIFTS the boat/pot the rope eases — HOLD to take line in cheaply. When it
    ///   DROPS into the trough the rope loads — holding through the drop strains and SLIPS line back (the
    ///   rope fights you). So the action is continuous engagement timed to the sea, read straight off the same
    ///   height the buoy bobs on and the boat rocks to (<c>BoatWaveMotion</c>). In a glassy calm there's no
    ///   swell to work, so a hold just winds line in steadily — quick and forgiving; a gale is a real fight
    ///   (P5 teeth) — the <see cref="TrapHaulMath.HoldLineRate"/> swell-coupling.</item>
    ///   <item><b>Diegetic, no HUD (owner's strong direction).</b> The read is the ROPE in the world: a
    ///   greybox <see cref="LineRenderer"/> from the rail to the buoy that goes <b>slack on the lift</b> (take
    ///   now) and <b>taut + shuddering on the drop</b> (ease off), shaded by strain; plus a
    ///   <see cref="TrapHaulStateChanged"/> signal the audio lane voices as a creak/strain cue. No meter, no
    ///   bar, and — crucially — no per-pull timing TEXT: the rope carries the timing, the toasts carry only
    ///   OUTCOMES (surfaced/empty/drifted).</item>
    ///   <item><b>No penalty (owner's M2 call).</b> Missing the phase slips line back (costs TIME) but you
    ///   never lose the catch, the pot, or take damage. Only a READY (soaked) trap yields; hauling an unsoaked
    ///   pot surfaces it empty.</item>
    ///   <item><b>The catch is fixed, this is the ACT of retrieving it.</b> On surface the haul calls
    ///   <see cref="PlacedTrapService.HaulTrap"/>, which resolves Build 3's seeded catch, lands it via
    ///   <see cref="IHold.TryAdd"/> + <see cref="FishCaught"/> (the rod/clam land path → sellable), and
    ///   removes the trap. The minigame never re-rolls or gates WHAT you catch (rule 5 — determinism
    ///   intact). Any cosmetic jitter here NEVER seeds or writes sim/saved state.</item>
    /// </list>
    ///
    /// <para><b>Feel path vs sim path (honesty).</b> For a rope that reads on the SAME sea the player watches,
    /// the swell height is sampled through the presentation <see cref="WaveFieldAnimator"/> (eased,
    /// phase-continuous — the <c>BoatWaveMotion</c> path), NOT the pure <c>WaveMath.Sample(pos, gameTime)</c>.
    /// That is fine and correct here: the animator is presentation-only and the minigame has NO sim/saved
    /// consequence (no penalty, no re-roll) — the FEEL is the point. The determinism that matters (the CATCH)
    /// lives entirely in Build 3's seeded resolve, untouched. The scoring MATH is the pure, EditMode-pinned
    /// <see cref="TrapHaulMath"/>.</para>
    ///
    /// <para><b>Seam discipline (rule 4).</b> Fishing-lane component: reads the sea + clock through
    /// <see cref="GameServices"/> (Core), the trap runtime through the sibling <see cref="PlacedTrapService"/>
    /// (Fishing), and lands into an <see cref="IHold"/> (Core). It never references Boats/Player/World
    /// concrete types — the "rail" the rope is made fast to is a plain <see cref="Transform"/>, and input is
    /// dev-keyed for the greybox (an InputService/prompt replaces it later, ui-ux). Stands down under a modal
    /// dialogue via <see cref="InteractionGate"/> so it never fights the board/talk key.</para>
    /// </summary>
    public sealed class TrapHaulController : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The service that owns placed traps (find-nearest / haul). Required.")]
        [SerializeField] private PlacedTrapService _service;
        [Tooltip("The RAIL the haul rope is made fast to — the boat/deck the player hauls from (the rope runs " +
                 "from here to the buoy). Defaults to this object's transform.")]
        [SerializeField] private Transform _rail;
        [Tooltip("A GameObject carrying an IHold (the active boat's ShipHold) the surfaced catch lands into.")]
        [SerializeField] private GameObject _holdProvider;
        [Tooltip("The region id passed in the haul's CatchContext — the pool is FILTERED by this (a species " +
                 "must allow this region to be caught). It is the CATCH region, distinct from where the trap " +
                 "physically sits: in the greybox the lobster/crab are authored for region.coddle_cove, so a " +
                 "St-Peters-set pot uses that catch region until the species are region-tagged for St Peters " +
                 "(an economy-sim/world follow-up). Determinism is unaffected — the region only filters the pool.")]
        [SerializeField] private string _catchRegionId = "region.coddle_cove";

        [Header("Reach (lay alongside the buoy to haul)")]
        [Tooltip("How close (m) the rail must be to a set trap's buoy to start hauling it — lay the boat " +
                 "ALONGSIDE. A tunable, not a magic number: forgiving enough to be cozy, tight enough that you " +
                 "must actually come up on the mark (P1 — wind/current set you off it).")]
        [SerializeField] private float _reachRadius = 4f;

        [Header("Haul with the swell (the take rates — tunables, rule 6)")]
        [Tooltip("Line taken IN per second while holding in a GLASSY CALM — the steady wind-in when there's no " +
                 "swell to work. Bigger = a quicker calm haul (a clean calm haul takes about 1/this seconds). " +
                 "This is the 'slightly faster' knob — raise it and every haul lands sooner.")]
        [SerializeField] private float _calmHaulRate = 0.55f;
        [Tooltip("Line taken per second at a FULL LIFT in a big sea — how fast the rising swell feeds you line " +
                 "when you HOLD on the lift. Also the rate the rope SLIPS back if you hold THROUGH the matching " +
                 "drop (the fight). Bigger = more dramatic swings: a faster clean haul but a harder sloppy one.")]
        [SerializeField] private float _swellTakeRate = 0.95f;
        [Tooltip("How strongly ROUGH SEAS take over from the steady wind-in (0..1) — the P5 knob. 0 = sea " +
                 "state doesn't matter (always the forgiving calm wind-in); 1 = a full gale is PURE swell " +
                 "timing (hold the lift, ease the fall, or slip). The coupling between the swell and the fight.")]
        [Range(0f, 1f)][SerializeField] private float _swellCoupling = 0.85f;
        [Tooltip("Normalized-height rate (per second) that reads as a FULL lift/drop — how brisk the swell " +
                 "must heave to feel like a full ±1 lift signal. Lower = the swell reads 'fully lifting' sooner " +
                 "(more forgiving timing); higher = only the steepest part of the swing counts. A shaping knob " +
                 "for the lift READ, not the take amount.")]
        [SerializeField] private float _liftReferenceRate = 2.0f;

        [Header("Diegetic rope (greybox LineRenderer — the read, no HUD)")]
        [Tooltip("How much a nearly-surfaced pot RELIEVES ambient rope strain (0..1): 0 = strain is pure sea " +
                 "state the whole haul; 1 = a fully surfaced pot reads slack regardless of sea. The strain " +
                 "shade eases as you win line.")]
        [Range(0f, 1f)][SerializeField] private float _lineRelief = 0.5f;
        [Tooltip("Seconds for the fight-strain to BUILD as you hold through a drop — small = the rope snaps " +
                 "taut and shudders quickly when you fight it.")]
        [SerializeField] private float _strainBuildSeconds = 0.15f;
        [Tooltip("Seconds for the rope strain to EASE back once you stop fighting (you released, or the swell " +
                 "lifted) — the rope relaxing.")]
        [SerializeField] private float _strainEaseSeconds = 0.4f;
        [Tooltip("Width of the greybox rope line (m). Visual only.")]
        [SerializeField] private float _ropeWidth = 0.1f;
        [Tooltip("Rope colour at SLACK / low strain. Visual only.")]
        [SerializeField] private Color _ropeSlackColor = new Color(0.80f, 0.70f, 0.45f, 1f);
        [Tooltip("Rope colour at FULL strain (a strained, whitened line). Visual only — the strain SHADE.")]
        [SerializeField] private Color _ropeStrainColor = new Color(0.95f, 0.92f, 0.85f, 1f);
        [Tooltip("How far a SLACK rope droops (m) at its slackest, drawn as a catenary belly. Visual only.")]
        [SerializeField] private float _slackSagAmount = 0.7f;
        [Tooltip("How far (m) a fully-strained rope SHUDDERS side-to-side — the visible 'she's straining' " +
                 "vibration on the drop. Visual only.")]
        [SerializeField] private float _ropeShudderAmount = 0.06f;
        [Tooltip("Shudder frequency (Hz) of the strained rope. Visual only.")]
        [SerializeField] private float _ropeShudderHz = 20f;
        [Tooltip("Segments the rope is drawn with (more = smoother sag/shudder). Visual only.")]
        [SerializeField] private int _ropeSegments = 14;

        [Header("Wave field (parity: keep identical to the shader bridge / BoatWaveMotion — see BoatWaveMotion)")]
        [SerializeField] private WaveFieldSettings _settings = WaveFieldSettings.Default;
        [SerializeField] private WaveFieldAnimatorSettings _animatorSettings = WaveFieldAnimatorSettings.Default;

        [Header("Keys (dev only — replaced by the InputService later, ui-ux)")]
        [Tooltip("Start hauling the nearest ready trap in reach.")]
        [SerializeField] private Key _startKey = Key.H;
        [Tooltip("HOLD to haul with the swell (take line as she lifts, ease as she falls). Mouse-hold + " +
                 "gamepad South also hold.")]
        [SerializeField] private Key _pullKey = Key.Space;

        // Above this lift signal, a tick that gained line reads as a "good take on the lift" — the clean-pull
        // clunk cue audio voices off TrapHaulState.PullOnBeat. A read threshold, not a balance number.
        private const float GoodTakeLift = 0.15f;

        // ---- runtime state (all real-time / presentation — NOTHING saved, no sim path, rule 5) ----
        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();
        private IHold _hold;
        private LineRenderer _rope;
        private Vector2[] _curveBuffer;

        private PlacedTrap _hauling;       // the trap being hauled, or null when idle
        private float _line01;             // haul progress 0..1
        private float _strain01;           // the displayed strain shade (max of ambient + active fight)
        private float _fightStrain;        // eased active-fight strain (holding through the drop)
        private float _swellLoad;          // the taut/slack phase read (drop = taut, lift = slack)

        private float _lastHeight;         // last sampled swell height under the buoy (for the lift finite-diff)
        private bool _hasLastHeight;
        private bool _hasLastTime;
        private double _lastTimeSeconds;
        private int _lastStrainBucket = -1;   // throttles the strain publish (quantized to 10 buckets)
        private bool _wasGoodTake;            // edge-detects the "good take" clunk publish
        // Hauling is a DECK action (owner's Build-5 split): only live while standing ON DECK — never at
        // the helm (you're steering) and never on foot (ControlModeChanged, via Core).
        private bool _onDeck;

        /// <summary>True while a haul is live (a trap is being pulled up). The rope shows only then.</summary>
        public bool IsHauling => _hauling != null;

        /// <summary>Haul progress 0..1 of the live haul (0 when idle). For dev readouts / tooling.</summary>
        public float Line01 => _line01;

        /// <summary>The live rope-strain read 0..1 (the diegetic "how hard she pulls"). For tooling.</summary>
        public float Strain01 => _strain01;

        private Transform Rail => _rail != null ? _rail : transform;

        private void Awake()
        {
            if (_rail == null) _rail = transform;
            if (_holdProvider != null) _hold = _holdProvider.GetComponent<IHold>();
            BuildRopeVisual();
        }

        /// <summary>True while the haul keys are live — ON DECK and not under a modal dialogue.
        /// Public + input-free so the gate itself is EditMode-testable.</summary>
        public bool GearKeysLive => _onDeck && !InteractionGate.IsBlocked;

        private void OnEnable()
        {
            _hasLastTime = false;
            _hasLastHeight = false;
            _animator.Reset();
            // Hauling is a DECK action — track the mode through Core so the keys only work on deck and
            // leaving the deck (helm or ashore) cozily drops a live haul (no penalty). Fresh components
            // start un-decked; every transition (and the region-arrival re-assert) republishes the mode.
            _onDeck = false;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            CancelHaul();   // dropping the component / leaving the scene never leaves a rope dangling
        }

        /// <summary>Public so tests can drive the deck gate through the same path the bus uses.</summary>
        public void OnControlModeChanged(ControlModeChanged e)
        {
            _onDeck = e.Mode == ControlMode.OnDeck;
            if (!_onDeck) CancelHaul();   // took the helm / stepped ashore mid-haul → let go (no penalty)
        }

        private void Update()
        {
            if (!GearKeysLive) { return; }   // hauling is worked from the DECK; keys are dead elsewhere

            var kb = Keyboard.current;
            float dt = Time.deltaTime;

            if (!IsHauling)
            {
                if (kb != null && kb[_startKey].wasPressedThisFrame) TryStartHaul();
                return;
            }

            // --- live haul: HOLD with the swell (take line as she lifts, ease as she falls) ---
            bool holding = (kb != null && kb[_pullKey].isPressed)
                           || (Mouse.current != null && Mouse.current.leftButton.isPressed)
                           || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
            TickHaul(dt, holding);
        }

        // ---- start / cancel -------------------------------------------------------------------

        // On-screen greybox feedback (DevNotice → DevToast) so the owner reads the loop without the
        // Console. OUTCOMES ONLY (owner's redesign: the TIMING is diegetic — the rope carries it, NOT text).
        // Event-time strings, never per frame. The landed-catch name arrives separately via FishCaught.
        private const string NoticeNoPot = "No pot alongside — lay up to a buoy";
        private const string NoticeDrifted = "Drifted off the buoy";
        private const string NoticeEmpty = "Empty pot — not ready yet";
        // Teach the new action ONCE at haul-start (owner legibility) — the only haul TEXT that remains. Fires
        // once per haul (never per frame / per tick). "swell" + "lifts" name the hold-with-the-swell action.
        private const string NoticeHaulStart = "Haul with the swell — take line as she lifts";

        /// <summary>Begin hauling the nearest set trap in reach of the rail (any state — a not-yet-soaked
        /// trap still hauls, and surfaces empty). Public so a test/tool can drive it without input. Returns
        /// true iff a haul started.</summary>
        public bool TryStartHaul()
        {
            if (_service == null) return false;
            PlacedTrap best = NearestInReach();
            if (best == null)
            {
                EventBus.Publish(new DevNotice(NoticeNoPot));
                return false;
            }

            _hauling = best;
            _line01 = 0f;
            _strain01 = 0f;
            _fightStrain = 0f;
            _swellLoad = 0f;
            _hasLastHeight = false;
            _lastStrainBucket = -1;
            _wasGoodTake = false;
            Publish(TrapHaulPhase.Hauling, pullOnBeat: false);
            EventBus.Publish(new DevNotice(NoticeHaulStart));   // teach the action on screen (owner legibility)
            Debug.Log($"[TrapHaul] Hauling — hold with the swell, take line as she lifts (readiness: {best.StateAt(NowSeconds())}).");
            return true;
        }

        /// <summary>Stop the live haul without landing (leaving the trap down, no penalty) — used on disable /
        /// if the boat drifts out of reach. Idempotent.</summary>
        public void CancelHaul()
        {
            if (_hauling == null) { HideRope(); return; }
            _hauling = null;
            _line01 = 0f;
            _strain01 = 0f;
            _fightStrain = 0f;
            _swellLoad = 0f;
            _hasLastHeight = false;
            HideRope();
            Publish(TrapHaulPhase.Idle, pullOnBeat: false);
        }

        // ---- the live haul --------------------------------------------------------------------

        /// <summary>
        /// Advance the live haul by <paramref name="dt"/> seconds while the player is (or isn't)
        /// <paramref name="holding"/> the pull. HOLD on the LIFT to take line in, ease on the FALL — holding
        /// through the drop strains and slips line back (the rope fights you). When not holding, the winch
        /// pawl holds the line where it is (releasing on the fall is safe — that's the whole play). Drives the
        /// diegetic rope + a throttled strain publish, and lands the catch on reaching the surface. Public so a
        /// test/tool can drive it without real input. Returns true iff this tick gained line.
        /// </summary>
        public bool TickHaul(float dt, bool holding)
        {
            if (_hauling == null) return false;

            // If the boat drifted off the mark, cozily let go (no penalty; re-approach to resume).
            if (!InReach(_hauling))
            {
                EventBus.Publish(new DevNotice(NoticeDrifted));
                CancelHaul();
                return false;
            }

            float step = Mathf.Max(0f, dt);
            float sea = SeaState01();
            float lift = SampleLiftUnderBuoy();   // + lifting, − dropping; 0 in a glassy calm

            // Take line while holding: fast on the lift, a slip on the drop, a steady wind-in in a calm.
            float before = _line01;
            if (holding)
            {
                float rate = TrapHaulMath.HoldLineRate(lift, sea, _swellCoupling, _calmHaulRate, _swellTakeRate);
                _line01 = Mathf.Clamp01(_line01 + rate * step);
            }
            // else: the pawl holds — the line stays put (easing on the fall is the correct, safe play).

            // The active fight: strain builds while holding through a drop, eases otherwise (eased so the
            // shudder ramps, not pops). fps-independent — shared with the wave-motion smoothers.
            float fightTarget = TrapHaulMath.FightStrain01(holding, lift, sea);
            float tau = fightTarget > _fightStrain ? _strainBuildSeconds : _strainEaseSeconds;
            _fightStrain = WaveFieldAnimator.Smooth(_fightStrain, fightTarget, step, tau);

            // The displayed strain shade = the "how heavy she is" ambient baseline OR the active fight,
            // whichever reads harder. The rope taut/slack read comes from the swell load (phase), always
            // visible so the drop can be read COMING.
            float ambient = TrapHaulMath.RopeStrain01(sea, _line01, _lineRelief);
            _strain01 = Mathf.Max(ambient, _fightStrain);
            _swellLoad = TrapHaulMath.SwellRopeLoad01(lift, sea);
            UpdateRope();

            bool gained = _line01 > before + 1e-5f;
            MaybePublishStrain(goodTake: gained && lift > GoodTakeLift);

            if (_line01 >= 1f) { Surface(); return gained; }
            return gained;
        }

        /// <summary>The pot has broken the surface — land the (already-deterministic) catch through the
        /// service and end the haul. A ready trap lands its Build-3 catch; an unready one comes up empty.</summary>
        private void Surface()
        {
            PlacedTrap trap = _hauling;
            _hauling = null;                    // end the haul first — HaulTrap may remove the trap object
            HideRope();
            if (trap == null) { Publish(TrapHaulPhase.Idle, false); return; }

            EnsureHold();
            CatchContext ctx = BuildContext();
            bool landed = _service != null && _service.HaulTrap(trap, _hold, in ctx);

            if (landed)
            {
                Publish(TrapHaulPhase.Surfaced, pullOnBeat: false);
                Debug.Log("[TrapHaul] The pot's aboard — catch landed and sellable.");
            }
            else
            {
                // Ready-gate: an unsoaked (or empty-pool / no-room) haul surfaces nothing. The trap stays down
                // to keep soaking (HaulTrap leaves it on a not-ready/empty result). Cozy — no penalty.
                Publish(TrapHaulPhase.Empty, pullOnBeat: false);
                EventBus.Publish(new DevNotice(NoticeEmpty));
                Debug.Log("[TrapHaul] Up she comes — empty. Not ready yet (or no room aboard). Left her to soak.");
            }
            _line01 = 0f;
            _strain01 = 0f;
            _fightStrain = 0f;
            _swellLoad = 0f;
            _hasLastHeight = false;
        }

        // ---- swell read (the presentation animator path — matches BoatWaveMotion's feel) --------

        /// <summary>The signed lift signal (−1 dropping .. +1 lifting) under the trap's buoy right now — the
        /// swell's vertical velocity, the read the hold times to. Ticks the eased, phase-continuous field (the
        /// BoatWaveMotion path) so the rope reads on the SAME sea the player watches, then folds the change in
        /// height since last tick into the lift via <see cref="TrapHaulMath.LiftSignal"/>. Presentation-only
        /// (no sim/saved state).</summary>
        private float SampleLiftUnderBuoy()
        {
            var env = GameServices.Environment;
            if (_hauling == null || env == null) { _hasLastHeight = false; return 0f; }

            double time = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            float gameDt = _hasLastTime ? Mathf.Max(0f, (float)(time - _lastTimeSeconds)) : Time.deltaTime;
            _lastTimeSeconds = time;
            _hasLastTime = true;

            EnvironmentSample sample = env.Sample();
            WaveTrains trains = _animator.Tick(gameDt, sample.WindVector, sample.SeaState01, in _settings, in _animatorSettings);
            WaveSample wave = _animator.Sample((Vector2)_hauling.transform.position);
            float height = wave.Height;

            float lift = 0f;
            if (_hasLastHeight)
                lift = TrapHaulMath.LiftSignal(height, _lastHeight, trains.TotalAmplitude, gameDt, _liftReferenceRate);
            _lastHeight = height;
            _hasLastHeight = true;
            return lift;
        }

        private float SeaState01()
        {
            var env = GameServices.Environment;
            return env != null ? env.Sample().SeaState01 : 0f;
        }

        // ---- reach / context / hold -----------------------------------------------------------

        private PlacedTrap NearestInReach()
        {
            var live = _service.Live;
            Vector2 from = Rail.position;
            PlacedTrap best = null;
            float bestSqr = _reachRadius * _reachRadius;
            for (int i = 0; i < live.Count; i++)
            {
                PlacedTrap t = live[i];
                if (t == null) continue;
                float sqr = ((Vector2)t.transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = t; }
            }
            return best;
        }

        private bool InReach(PlacedTrap trap)
            => trap != null && ((Vector2)trap.transform.position - (Vector2)Rail.position).sqrMagnitude
                               <= _reachRadius * _reachRadius;

        private CatchContext BuildContext()
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            float tide = env != null ? env.Sample().TideHeight : 0f;
            float hour = clock != null ? clock.HourOfDay : 12f;
            Season season = clock != null ? clock.Season : Season.HighSummer;
            return new CatchContext(_catchRegionId, tide, hour, season, Gear.Trap);
        }

        private double NowSeconds() => GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;

        private void EnsureHold()
        {
            if (_hold == null && _holdProvider != null) _hold = _holdProvider.GetComponent<IHold>();
        }

        private void Publish(TrapHaulPhase phase, bool pullOnBeat)
            => EventBus.Publish(new TrapHaulStateChanged(new TrapHaulState(phase, _strain01, _line01, pullOnBeat)));

        /// <summary>Publish a live-haul snapshot only on a meaningful CHANGE — a quantized strain bucket shift
        /// or the rising edge of a good take — so audio can follow the strain/clunk WITHOUT a per-frame bus
        /// publish (rule 7). Bounded to ~a dozen strain steps + a few takes per haul.</summary>
        private void MaybePublishStrain(bool goodTake)
        {
            int bucket = Mathf.RoundToInt(Mathf.Clamp01(_strain01) * 10f);
            bool takeEdge = goodTake && !_wasGoodTake;
            _wasGoodTake = goodTake;
            if (bucket != _lastStrainBucket || takeEdge)
            {
                _lastStrainBucket = bucket;
                Publish(TrapHaulPhase.Hauling, goodTake);
            }
        }

        // ---- the greybox diegetic rope (LineRenderer; taut+shuddering on the drop, slack on the lift) ----

        private void BuildRopeVisual()
        {
            var go = new GameObject("HaulRope");
            go.transform.SetParent(transform, false);
            _rope = go.AddComponent<LineRenderer>();
            _rope.useWorldSpace = true;
            _rope.numCapVertices = 2;
            _rope.startWidth = _ropeWidth;
            _rope.endWidth = _ropeWidth;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _rope.material = new Material(shader);   // null in a headless test → skip (no error)
            _rope.sortingOrder = 50;   // above the water, under any HUD
            _rope.positionCount = 0;
            _rope.enabled = false;
        }

        private void UpdateRope()
        {
            if (_rope == null || _hauling == null) return;

            Vector2 rail = Rail.position;
            Vector2 pot = _hauling.transform.position;

            // Taut/slack from the swell load (slack on the lift, taut on the drop) OR the active fight,
            // whichever is tauter — the diegetic phase read the player acts on.
            float taut = TrapHaulMath.RopeTaut01(_swellLoad, _fightStrain);

            int n = Mathf.Max(2, _ropeSegments);
            if (_curveBuffer == null || _curveBuffer.Length != n) _curveBuffer = new Vector2[n];
            TrapHaulMath.SampleHaulRope(rail, pot, taut, _slackSagAmount, _curveBuffer);

            // A cross-rope shudder when she strains (∝ fight strain) — the "she's loading, ease off" read.
            // Perpendicular to the rope, tapered to zero at both ends, biggest in the belly. Visual only.
            float shudder = _ropeShudderAmount * Mathf.Clamp01(_fightStrain);
            Vector2 span = pot - rail;
            float len = span.magnitude;
            Vector2 perp = len > 1e-4f ? new Vector2(-span.y, span.x) / len : Vector2.up;
            float phase = Time.time * _ropeShudderHz * (2f * Mathf.PI);

            Color c = Color.Lerp(_ropeSlackColor, _ropeStrainColor, Mathf.Clamp01(_strain01));
            _rope.startColor = c; _rope.endColor = c;
            _rope.startWidth = _ropeWidth; _rope.endWidth = _ropeWidth;

            if (!_rope.enabled) _rope.enabled = true;
            _rope.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = _curveBuffer[i];
                if (shudder > 1e-4f && i > 0 && i < n - 1)
                {
                    float t = i / (float)(n - 1);
                    float taper = Mathf.Sin(t * Mathf.PI);   // 0 at the ends, 1 in the belly
                    p += perp * (Mathf.Sin(phase + i * 1.7f) * shudder * taper);
                }
                _rope.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
        }

        private void HideRope()
        {
            if (_rope == null) return;
            _rope.positionCount = 0;
            _rope.enabled = false;
        }

        /// <summary>Wire the haul controller in one call (tests / editor builder). <paramref name="catchRegionId"/>
        /// filters the catch pool (the species must allow this region) — see <see cref="_catchRegionId"/>. The
        /// optional tuning args let a test drive the take without the serialized defaults (pass a negative to
        /// leave a field untouched): <paramref name="calmHaulRate"/> (line/second the calm wind-in takes),
        /// <paramref name="swellTakeRate"/> (line/second at a full lift / slip in a big sea) and
        /// <paramref name="swellCoupling"/> (how much rough seas take over — the P5 knob).</summary>
        public void Configure(PlacedTrapService service, Transform rail, IHold hold, string catchRegionId,
                              float calmHaulRate = -1f, float swellTakeRate = -1f, float swellCoupling = -1f)
        {
            _service = service;
            _rail = rail;
            _hold = hold;
            _catchRegionId = catchRegionId;
            if (calmHaulRate >= 0f) _calmHaulRate = calmHaulRate;
            if (swellTakeRate >= 0f) _swellTakeRate = swellTakeRate;
            if (swellCoupling >= 0f) _swellCoupling = swellCoupling;
        }
    }
}
