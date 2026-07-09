using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE PLAYABLE HAUL (trap-fishing arc Build 4) — lay the boat alongside a set trap, interact to start
    /// hauling, then <b>pull the rope in rhythm with the passing swell</b> until the pot surfaces (the
    /// owner's pick). This is the driver that turns the pure <see cref="TrapHaulMath"/> into a live,
    /// diegetic, low-HUD interaction: it reads the swell under the buoy, owns the rhythmic pull input, drives
    /// the world rope (its taut shape + a strain shade), and on surface lands the trap's already-deterministic
    /// catch (Build 3) into the active boat's hold.
    ///
    /// <list type="bullet">
    ///   <item><b>The cadence IS the sea.</b> The beat comes from the shared deterministic wave field
    ///   (<see cref="WaveMath"/>) under the trap's buoy — the SAME height read the buoy bobs on and the boat
    ///   rocks to (<c>BoatWaveMotion</c>). A pull near the passing crest gains line; a mistimed one slips (no
    ///   gain). Calm ⇒ a broad, forgiving window; a big swell ⇒ the window tightens and the rope strains
    ///   (P5 teeth) — the <see cref="TrapHaulMath.OnBeatWindow"/> swell-coupling.</item>
    ///   <item><b>Diegetic, no HUD (owner's strong direction).</b> The read is the ROPE in the world: a
    ///   greybox <see cref="LineRenderer"/> from the rail to the buoy that goes taut on a pull and slack
    ///   between, shaded by strain; plus a <see cref="TrapHaulStateChanged"/> signal the audio lane voices
    ///   as a creak/strain cue. No meter, no bar.</item>
    ///   <item><b>No penalty (owner's M2 call).</b> A mistimed pull just gains nothing — you never lose the
    ///   catch or the pot. Only a READY (soaked) trap yields; hauling an unsoaked pot surfaces it empty.</item>
    ///   <item><b>The catch is fixed, this is the ACT of retrieving it.</b> On surface the haul calls
    ///   <see cref="PlacedTrapService.HaulTrap"/>, which resolves Build 3's seeded catch, lands it via
    ///   <see cref="IHold.TryAdd"/> + <see cref="FishCaught"/> (the rod/clam land path → sellable), and
    ///   removes the trap. The minigame never re-rolls or gates WHAT you catch (rule 5 — determinism
    ///   intact). Any cosmetic jitter here NEVER seeds or writes sim/saved state.</item>
    /// </list>
    ///
    /// <para><b>Feel path vs sim path (honesty).</b> For a rope that reads on the SAME sea the player watches,
    /// the swell phase is sampled through the presentation <see cref="WaveFieldAnimator"/> (eased,
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

        [Header("Rhythm (the swell-timed pull — tunables, rule 6)")]
        [Tooltip("On-beat half-window at GLASSY CALM (swell-phase units, 0..0.5). The forgiving width: a pull " +
                 "within this much of the passing crest gains line. Bigger = easier rhythm.")]
        [Range(0f, 0.5f)][SerializeField] private float _calmOnBeatWindow = 0.28f;
        [Tooltip("How strongly ROUGH SEAS tighten the on-beat window (0..1) — the P5 knob. 0 = sea state " +
                 "doesn't matter (always the calm window); 1 = a full gale closes the window entirely. The " +
                 "coupling between the swell and the timing difficulty.")]
        [Range(0f, 1f)][SerializeField] private float _swellCoupling = 0.75f;
        [Tooltip("Line fraction a PERFECT (dead-on-crest) pull gains (0..1). PullsToSurface ≈ 1 / this at " +
                 "best timing. Bigger = fewer pulls to surface the pot.")]
        [Range(0.01f, 1f)][SerializeField] private float _maxGainPerPull = 0.14f;
        [Tooltip("Fraction of the max gain a JUST-in-window pull earns (0..1) — the taper floor so a barely-" +
                 "on-beat pull still counts. 1 = flat (any on-beat pull is a perfect pull).")]
        [Range(0f, 1f)][SerializeField] private float _edgeGainFraction = 0.35f;
        [Tooltip("Minimum real seconds between pulls — a light debounce so a held/mashed key is one pull, not " +
                 "a stream. Keeps the rhythm a rhythm.")]
        [SerializeField] private float _pullCooldownSeconds = 0.25f;

        [Header("Diegetic rope (greybox LineRenderer — the read, no HUD)")]
        [Tooltip("How much a nearly-surfaced pot RELIEVES rope strain (0..1): 0 = strain is pure sea state the " +
                 "whole haul; 1 = a fully surfaced pot reads slack regardless of sea. The strain shade eases " +
                 "as you win line.")]
        [Range(0f, 1f)][SerializeField] private float _lineRelief = 0.5f;
        [Tooltip("How long (real seconds) a pull holds the rope TAUT before it eases back toward its strain " +
                 "baseline — the visible 'heave' of each pull. The rope snaps taut on the beat, then relaxes.")]
        [SerializeField] private float _pullTautHoldSeconds = 0.35f;
        [Tooltip("Width of the greybox rope line (m). Visual only.")]
        [SerializeField] private float _ropeWidth = 0.1f;
        [Tooltip("Rope colour at SLACK / low strain. Visual only.")]
        [SerializeField] private Color _ropeSlackColor = new Color(0.80f, 0.70f, 0.45f, 1f);
        [Tooltip("Rope colour at FULL strain (a strained, whitened line). Visual only — the strain SHADE.")]
        [SerializeField] private Color _ropeStrainColor = new Color(0.95f, 0.92f, 0.85f, 1f);
        [Tooltip("How far a SLACK rope droops (m) at its slackest, drawn as a catenary belly. Visual only.")]
        [SerializeField] private float _slackSagAmount = 0.7f;
        [Tooltip("Segments the rope is drawn with (more = smoother sag). Visual only.")]
        [SerializeField] private int _ropeSegments = 14;

        [Header("Wave field (parity: keep identical to the shader bridge / BoatWaveMotion — see BoatWaveMotion)")]
        [SerializeField] private WaveFieldSettings _settings = WaveFieldSettings.Default;
        [SerializeField] private WaveFieldAnimatorSettings _animatorSettings = WaveFieldAnimatorSettings.Default;

        [Header("Keys (dev only — replaced by the InputService later, ui-ux)")]
        [Tooltip("Start hauling the nearest ready trap in reach.")]
        [SerializeField] private Key _startKey = Key.H;
        [Tooltip("Pull the rope (time it to the passing swell). Mouse-click + gamepad South also pull.")]
        [SerializeField] private Key _pullKey = Key.Space;

        // ---- runtime state (all real-time / presentation — NOTHING saved, no sim path, rule 5) ----
        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();
        private IHold _hold;
        private LineRenderer _rope;
        private Vector2[] _curveBuffer;

        private PlacedTrap _hauling;       // the trap being hauled, or null when idle
        private float _line01;             // haul progress 0..1
        private float _strain01;           // the diegetic strain read
        private float _pullTautTimer;      // >0 while a pull holds the rope taut
        private float _pullCooldownTimer;  // >0 while a fresh pull is debounced
        private bool _hasLastTime;
        private double _lastTimeSeconds;
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
            if (_pullCooldownTimer > 0f) _pullCooldownTimer -= dt;
            if (_pullTautTimer > 0f) _pullTautTimer -= dt;

            if (!IsHauling)
            {
                if (kb != null && kb[_startKey].wasPressedThisFrame) TryStartHaul();
                return;
            }

            // --- live haul: sample the swell under the buoy, drive the rope, take pull input ---
            TickHaul(dt);

            bool pulled = (kb != null && kb[_pullKey].wasPressedThisFrame)
                          || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                          || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            if (pulled) Pull();
        }

        // ---- start / cancel -------------------------------------------------------------------

        // On-screen greybox feedback (DevNotice → DevToast) so the owner reads the loop without the
        // Console. Event-time strings only, never per frame. The landed-catch name arrives separately
        // via FishCaught (the toast side formats it), so a success here needs no extra notice.
        private const string NoticeNoPot = "No pot alongside — lay up to a buoy";
        private const string NoticeDrifted = "Drifted off the buoy";
        private const string NoticeEmpty = "Empty pot — not ready yet";
        // Legibility (owner "doesn't know how to haul"): teach the rhythm on screen. Event-time only — the
        // start fires once per haul, and per-pull cues fire on a (debounced) keypress, NEVER per frame.
        private const string NoticeHaulStart = "Haul! Tap in time with the swell";
        private const string NoticeHeave = "Heave!";     // a clean, on-beat pull won line
        private const string NoticeSlipping = "Slipping!"; // a mistimed pull — no line (no penalty, try the crest)

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
            _pullTautTimer = 0f;
            Publish(TrapHaulPhase.Hauling, pullOnBeat: false);
            EventBus.Publish(new DevNotice(NoticeHaulStart));   // teach the pull on screen (owner legibility)
            Debug.Log($"[TrapHaul] Hauling — pull the rope in time with the swell (readiness: {best.StateAt(NowSeconds())}).");
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
            HideRope();
            Publish(TrapHaulPhase.Idle, pullOnBeat: false);
        }

        // ---- the live haul --------------------------------------------------------------------

        private void TickHaul(float dt)
        {
            if (_hauling == null) return;

            // If the boat drifted off the mark, cozily let go (no penalty; re-approach to resume).
            if (!InReach(_hauling))
            {
                EventBus.Publish(new DevNotice(NoticeDrifted));
                CancelHaul();
                return;
            }

            // Drive the strain read from the live sea + progress, and ease the rope's taut/slack shape.
            float sea = SeaState01();
            _strain01 = TrapHaulMath.RopeStrain01(sea, _line01, _lineRelief);
            UpdateRope();
        }

        /// <summary>Take one pull, timed against the passing swell under the buoy. On-beat ⇒ gains line and
        /// (on reaching the surface) lands the catch; off-beat ⇒ no gain (no penalty). Public so a test/tool
        /// can drive it without input. Returns true iff the pull landed on the beat.</summary>
        public bool Pull()
        {
            if (_hauling == null) return false;
            if (_pullCooldownTimer > 0f) return false;   // debounce a mashed key into one pull
            _pullCooldownTimer = Mathf.Max(0f, _pullCooldownSeconds);

            float sea = SeaState01();
            float phase = SwellPhaseUnderBuoy();
            float window = TrapHaulMath.OnBeatWindow(_calmOnBeatWindow, sea, _swellCoupling);
            bool onBeat = TrapHaulMath.IsOnBeat(phase, window);
            float gain = TrapHaulMath.LineGain(phase, window, _maxGainPerPull, _edgeGainFraction);

            if (gain > 0f)
            {
                _line01 = Mathf.Clamp01(_line01 + gain);
                _pullTautTimer = Mathf.Max(0f, _pullTautHoldSeconds);   // the rope snaps taut on a good heave
            }

            _strain01 = TrapHaulMath.RopeStrain01(sea, _line01, _lineRelief);
            bool cleanPull = onBeat && gain > 0f;
            Publish(TrapHaulPhase.Hauling, pullOnBeat: cleanPull);
            // Per-pull on-screen cue so the owner FEELS the rhythm: a clean on-beat heave reads differently
            // from a mistimed slip. Event-time (a debounced keypress), never per frame — rule 7.
            EventBus.Publish(new DevNotice(cleanPull ? NoticeHeave : NoticeSlipping));
            UpdateRope();

            if (_line01 >= 1f) Surface();
            return onBeat;
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
        }

        // ---- swell read (the presentation animator path — matches BoatWaveMotion's feel) --------

        /// <summary>The swell phase (0..0.5, distance-from-crest) under the trap's buoy right now — the beat
        /// the pull times to. Ticks the eased, phase-continuous field (the BoatWaveMotion path) so the rope
        /// reads on the SAME sea the player watches, then folds the height into a phase via
        /// <see cref="TrapHaulMath.PhaseFromHeight"/>. Presentation-only (no sim/saved state).</summary>
        private float SwellPhaseUnderBuoy()
        {
            var env = GameServices.Environment;
            if (_hauling == null || env == null) return 0f;   // no sea → always on the beat (forgiving)

            double time = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            float dt = _hasLastTime ? Mathf.Max(0f, (float)(time - _lastTimeSeconds)) : Time.deltaTime;
            _lastTimeSeconds = time;
            _hasLastTime = true;

            EnvironmentSample sample = env.Sample();
            WaveTrains trains = _animator.Tick(dt, sample.WindVector, sample.SeaState01, in _settings, in _animatorSettings);
            WaveSample wave = _animator.Sample((Vector2)_hauling.transform.position);
            return TrapHaulMath.PhaseFromHeight(wave.Height, trains.TotalAmplitude);
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

        // ---- the greybox diegetic rope (LineRenderer; taut on a pull, slack + drooping between) ----

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

            // Taut on a pull (the heave), else eased toward the strain baseline — the diegetic taut/slack read.
            float pullTaut = _pullTautTimer > 0f ? 1f : 0f;
            float taut = TrapHaulMath.RopeTaut01(_strain01, pullTaut);

            int n = Mathf.Max(2, _ropeSegments);
            if (_curveBuffer == null || _curveBuffer.Length != n) _curveBuffer = new Vector2[n];
            TrapHaulMath.SampleHaulRope(rail, pot, taut, _slackSagAmount, _curveBuffer);

            Color c = Color.Lerp(_ropeSlackColor, _ropeStrainColor, Mathf.Clamp01(_strain01));
            _rope.startColor = c; _rope.endColor = c;
            _rope.startWidth = _ropeWidth; _rope.endWidth = _ropeWidth;

            if (!_rope.enabled) _rope.enabled = true;
            _rope.positionCount = n;
            for (int i = 0; i < n; i++)
                _rope.SetPosition(i, new Vector3(_curveBuffer[i].x, _curveBuffer[i].y, 0f));
        }

        private void HideRope()
        {
            if (_rope == null) return;
            _rope.positionCount = 0;
            _rope.enabled = false;
        }

        /// <summary>Wire the haul controller in one call (tests / editor builder). <paramref name="catchRegionId"/>
        /// filters the catch pool (the species must allow this region) — see <see cref="_catchRegionId"/>. The
        /// optional tuning args let a test drive the rhythm without the serialized defaults (pass a negative to
        /// leave a field untouched): <paramref name="maxGainPerPull"/> (line per perfect pull) and
        /// <paramref name="pullCooldownSeconds"/> (0 removes the debounce so a test can pull in a tight loop).</summary>
        public void Configure(PlacedTrapService service, Transform rail, IHold hold, string catchRegionId,
                              float maxGainPerPull = -1f, float pullCooldownSeconds = -1f)
        {
            _service = service;
            _rail = rail;
            _hold = hold;
            _catchRegionId = catchRegionId;
            if (maxGainPerPull >= 0f) _maxGainPerPull = maxGainPerPull;
            if (pullCooldownSeconds >= 0f) _pullCooldownSeconds = pullCooldownSeconds;
        }
    }
}
