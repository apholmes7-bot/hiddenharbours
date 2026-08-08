using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Player
{
    /// <summary>What the rope is doing right now.</summary>
    public enum MooringPhase
    {
        /// <summary>Nothing in hand. Either no cleat is in reach, or the player simply hasn't started.</summary>
        Idle = 0,
        /// <summary>The line is coiled and the flick is being wound back — the same gesture, and the same
        /// live preview, as a rod cast.</summary>
        WindBack = 1,
        /// <summary>A line is made fast between two cleats. Work it (tighten / slacken) or cast off.</summary>
        MadeFast = 2,
    }

    /// <summary>
    /// <b>Mooring lines — throw a rope to a cleat, make fast, and mind the tide</b> (M2-38; owner ask
    /// 2026-08-06, ratified in design/deck-boarding-cleats-and-interact-capture.md §3).
    ///
    /// <para>Stand by a cleat, throw a line with the <b>fishing-cast verb</b> to a cleat on the other side
    /// of the water — boat→shore or shore→boat — and make fast. From there the scope is yours to tighten or
    /// slacken, and a made-fast line genuinely CONSTRAINS the hull against wind and tide (the sim keeps
    /// computing; the rope restrains the result — <c>BoatMooring</c> owns that half). Miss the throw and the
    /// loop falls in the water: coil it and try again, no penalty. That is the cozy fail the backlog
    /// names.</para>
    ///
    /// <para><b>ONE VERB, TWO CONTEXTS — and one function.</b> The owner's ruling is verbatim that you
    /// "toss a line in the same manner you cast", so the toss resolves through
    /// <see cref="FlickCastMath.Evaluate"/> — the very function the rod uses — and previews through
    /// <see cref="FlickCastMath.WindBackAimOffset"/>, the very preview the rod shows. Nothing about the
    /// gesture is re-derived here: the arc the player watches creep across the water IS the arc the release
    /// resolves to, because there is only one piece of maths and it now lives in Core so both callers can
    /// reach it (rule 4). Re-implementing a "rope version" of the cast would be the classic
    /// compute-one-quantity-two-ways defect this codebase has paid for before.</para>
    ///
    /// <para><b>Standalone, and it does not touch the control switcher.</b> It READS
    /// <see cref="ControlMode"/> off the Core bus to know whether the player is ashore or on a deck, and
    /// nothing more — no transitions, no boarding, no ownership of E. Rope work rides its own key
    /// (<see cref="_workKey"/>) and the shared cast flick; the flick is arbitrated against the rod through
    /// <see cref="CastActionClaim"/>, claimed by PROXIMITY so the arbitration can never come down to
    /// component execution order (see that type's doc).</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Nothing here is random. The gesture is read from injected
    /// samples with caller-accumulated timestamps (never <c>Time</c> inside the maths), the tide enters
    /// only through <see cref="IEnvironmentService.WaterLevelAt"/>, and nothing about a line is saved —
    /// the constraint is recomputed from the two live cleats every tick.</para>
    ///
    /// <para><b>No magic numbers (rule 6), no per-frame allocation (rule 7).</b> Reach, catch radius,
    /// scope limits and step all come from <see cref="GameConfig.MooringLine"/>; the gesture buffer is
    /// allocated once and reused, exactly as the fishing controller's is.</para>
    /// </summary>
    public sealed class MooringController : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The boat this rope belongs to. Left unset, the controller finds the mooring on the " +
                 "active boat at bind — the greybox has one hull at a time.")]
        [SerializeField] private BoatMooring _mooring;

        [Tooltip("The player transform the gesture is anchored at (the hand throwing the line). Left " +
                 "unset, this component's own transform is used.")]
        [SerializeField] private Transform _hand;

        [Header("Input (New Input System only)")]
        [Tooltip("Key that WORKS a line already made fast: tap to tighten, hold Shift to slacken, and " +
                 "hold it down to cast off. Distinct from the cast flick, which THROWS the line.")]
        [SerializeField] private Key _workKey = Key.R;

        [Tooltip("Modifier that inverts tighten into slacken on the work key.")]
        [SerializeField] private Key _slackenModifier = Key.LeftShift;

        [Tooltip("How long (s) the work key must be held to CAST OFF rather than adjust scope. Long " +
                 "enough that letting a boat go is never a mistyped tap.")]
        [Min(0.05f)] [SerializeField] private float _castOffHoldSeconds = 0.6f;

        // ---- gesture state (the same shape FishingController keeps; the maths is FlickCastMath) -------
        // Allocated once and reused — a gesture must not allocate per frame (rule 7).
        private const int MaxSamples = 128;
        private readonly FlickSample[] _samples = new FlickSample[MaxSamples];
        private int _sampleCount;
        private float _gestureSeconds;
        private bool _actionWasHeld;

        private MooringPhase _phase = MooringPhase.Idle;
        private ControlMode _mode = ControlMode.OnFoot;
        private IMooringCleat _nearCleat;      // the cleat under the player's hand, if any
        private IMooringCleat _fromCleat;      // the cleat this throw's coil was picked up at
        private float _workHeldSeconds;
        private Camera _cam;

        /// <summary>What the rope is doing — for tests, and for any diegetic read.</summary>
        public MooringPhase Phase => _phase;

        /// <summary>The cleat currently in reach of the player's hand; null when none is.</summary>
        public IMooringCleat NearCleat => _nearCleat;

        /// <summary>The boat's mooring this controller works.</summary>
        public BoatMooring Mooring { get => _mooring; set => _mooring = value; }

        /// <summary>Where the throw is anchored from (the player's hand).</summary>
        public Vector2 HandPosition => _hand != null ? (Vector2)_hand.position : (Vector2)transform.position;

        /// <summary>The shared owner tuning, falling back to the reference values with no config wired
        /// (⚠ a YAML-omitted struct deserialises to C# defaults, so the fallback is <c>Default</c>).</summary>
        private static MooringLineSettings Settings
            => GameServices.Config != null ? GameServices.Config.MooringLine : MooringLineSettings.Default;

        /// <summary>
        /// Which side of the water the player's hands are on: standing on a deck they work the BOAT's
        /// cleats and throw to the SHORE; ashore it is the other way about. This one read is the only thing
        /// the controller wants from the control mode — it never drives a transition.
        /// </summary>
        public CleatSide NearSide => _mode == ControlMode.OnDeck ? CleatSide.Boat : CleatSide.Shore;

        /// <summary>The side a throw is aimed AT — always the opposite one. A line runs boat-to-shore;
        /// rafting boat-to-boat is explicitly out of scope.</summary>
        public CleatSide FarSide => NearSide == CleatSide.Boat ? CleatSide.Shore : CleatSide.Boat;

        private void OnEnable()
        {
            _mode = ControlMode.OnFoot;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            CastActionClaim.Reset();      // never leave the rod wedged off behind a torn-down rope
        }

        /// <summary>Public so tests can drive the mode gate through the same path the bus uses.</summary>
        public void OnControlModeChanged(ControlModeChanged e) => _mode = e.Mode;

        /// <summary>Wire the controller in one call (region builders / tests; the direct-configure
        /// convention).</summary>
        public void Configure(BoatMooring mooring, Transform hand)
        {
            _mooring = mooring;
            _hand = hand;
        }

        // ---- the frame ---------------------------------------------------------------------------

        private void Update()
        {
            bool actionHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed
                              || Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool workHeld = Keyboard.current != null && Keyboard.current[_workKey].isPressed;
            bool slacken = Keyboard.current != null && Keyboard.current[_slackenModifier].isPressed;
            bool pointerValid = TryPointerWorld(out Vector2 pointerWorld);

            Tick(Time.deltaTime, actionHeld, pointerWorld, pointerValid, workHeld, slacken);
        }

        /// <summary>
        /// Advance the rope by <paramref name="dt"/>. Input-free and public so the whole loop is testable
        /// headless — the tests drive this directly with synthesized gesture samples, exactly as the
        /// fishing tests drive <c>FishingController.Tick</c>.
        /// </summary>
        /// <param name="dt">Seconds since the last tick (the caller's, never <c>Time</c> inside the maths).</param>
        /// <param name="actionHeld">The cast flick: held while winding back, released to throw.</param>
        /// <param name="pointerWorld">The pointer in world metres — where the line is being aimed from.</param>
        /// <param name="pointerValid">False when there is no pointer (headless / no camera): a fresh
        /// wind-back then simply cannot start, and nothing throws.</param>
        /// <param name="workHeld">The work key: tap to adjust scope, hold to cast off.</param>
        /// <param name="slacken">True to slacken rather than tighten on the work key.</param>
        public void Tick(float dt, bool actionHeld, Vector2 pointerWorld, bool pointerValid,
                         bool workHeld, bool slacken)
        {
            RefreshNearCleat();
            SyncPhaseWithMooring();

            // The rope claims the cast flick by PROXIMITY (see CastActionClaim): in reach of a workable
            // cleat, the flick throws a line rather than a handline. Race-free by construction.
            CastActionClaim.IsClaimed = _nearCleat != null || _phase == MooringPhase.WindBack;

            if (_phase == MooringPhase.MadeFast) TickWorkKey(dt, workHeld, slacken);
            else TickToss(dt, actionHeld, pointerWorld, pointerValid);

            _actionWasHeld = actionHeld;
        }

        /// <summary>The cleat under the player's hand, or null. Only cleats on the player's OWN side count
        /// — you work the fitting you can touch.</summary>
        private void RefreshNearCleat()
            => MooringCleats.TryFindNearestNow(HandPosition, NearSide, Settings.CleatReachMetres,
                                               out _nearCleat);

        /// <summary>Keep the phase honest against the boat's own state — the line can end without the
        /// player: a slipped loop (the tide out-ran the scope) puts the rope back in the coil.</summary>
        private void SyncPhaseWithMooring()
        {
            bool madeFast = _mooring != null && _mooring.IsMadeFast;
            if (madeFast && _phase != MooringPhase.MadeFast) _phase = MooringPhase.MadeFast;
            else if (!madeFast && _phase == MooringPhase.MadeFast) { _phase = MooringPhase.Idle; ResetGesture(); }
        }

        // ---- the throw ---------------------------------------------------------------------------

        /// <summary>
        /// The toss: a rising edge at a cleat starts the wind-back, the held gesture is recorded, and the
        /// release resolves it through the SAME <see cref="FlickCastMath.Evaluate"/> the rod uses. A landed
        /// loop within the catch radius of an opposing cleat makes fast; anything else is the cozy nothing.
        /// </summary>
        private void TickToss(float dt, bool actionHeld, Vector2 pointerWorld, bool pointerValid)
        {
            if (_phase == MooringPhase.WindBack)
            {
                _gestureSeconds += Mathf.Max(0f, dt);
                if (pointerValid) Record(pointerWorld);
                if (!actionHeld) ReleaseToss();
                return;
            }

            // Rising edge, standing at a cleat, with a pointer to aim: pick up the coil.
            bool rising = actionHeld && !_actionWasHeld;
            if (!rising || !pointerValid || _nearCleat == null || _mooring == null) return;

            _phase = MooringPhase.WindBack;
            // The near end is the cleat the coil was picked up AT, captured now rather than re-read at
            // release: a player who drifts off the fitting mid-gesture is still throwing the line they
            // took a turn around, and re-reading would either lose it or silently swap it for a neighbour.
            _fromCleat = _nearCleat;
            _sampleCount = 0;
            _gestureSeconds = 0f;
            Record(pointerWorld);
        }

        /// <summary>Record one gesture point. The buffer is fixed and reused; when it is full the OLDEST
        /// sample is dropped rather than the newest, so a long slow wind-up still resolves off the part of
        /// the gesture that mattered (the sweep and the release).</summary>
        private void Record(Vector2 pointerWorld)
        {
            if (_sampleCount == MaxSamples)
            {
                System.Array.Copy(_samples, 1, _samples, 0, MaxSamples - 1);
                _sampleCount = MaxSamples - 1;
            }
            _samples[_sampleCount++] = new FlickSample(pointerWorld, _gestureSeconds);
        }

        /// <summary>
        /// The release. Resolves the gesture, then asks the ONE question that makes it a throw rather than
        /// a cast: did the loop land near enough to a cleat on the far side to catch?
        /// </summary>
        private void ReleaseToss()
        {
            MooringLineSettings s = Settings;
            FlickCastSettings cast = GameServices.Config != null
                ? GameServices.Config.FlickCast
                : FlickCastSettings.Default;

            FlickCastResult result = FlickCastMath.Evaluate(_samples, _sampleCount, HandPosition,
                                                            cast, cast.MaxCastDistanceMetres);
            IMooringCleat near = _fromCleat;      // read before the reset clears it
            ResetGesture();
            _phase = MooringPhase.Idle;

            // A gesture that never became a throw is the cozy nothing — no notice, no penalty, just coil.
            if (!result.IsCast || near == null) return;

            if (!MooringCleats.TryFindNearestNow(result.LandingPoint, FarSide, s.TossCatchRadiusMetres,
                                                 out IMooringCleat far))
            {
                // The loop fell in the water. Coil it and try again.
                EventBus.Publish(new MooringLineChanged(MooringLineEvent.TossMissed, "", "", 0f, 0f));
                return;
            }

            // Which end is which depends on where the player is standing, not on which was thrown.
            IMooringCleat boatCleat = near.Side == CleatSide.Boat ? near : far;
            IMooringCleat shoreCleat = near.Side == CleatSide.Boat ? far : near;

            if (_mooring != null && _mooring.MakeFast(boatCleat, shoreCleat, s.DefaultScopeMetres))
                _phase = MooringPhase.MadeFast;
        }

        private void ResetGesture()
        {
            _sampleCount = 0;
            _gestureSeconds = 0f;
            _fromCleat = null;
        }

        // ---- working a made-fast line ---------------------------------------------------------------

        /// <summary>
        /// Tighten / slacken / cast off. A TAP of the work key steps the scope (with the modifier to
        /// slacken); a sustained HOLD casts off. Hold-to-release rather than tap-to-release because
        /// letting a boat go should never be a mistyped key — the same reasoning behind every other
        /// deliberate act in this game.
        ///
        /// <para>Working the line requires standing at one of its OWN cleats: you can watch a moored boat
        /// from anywhere, but you adjust her from the fitting the rope is turned around.</para>
        /// </summary>
        private void TickWorkKey(float dt, bool workHeld, bool slacken)
        {
            if (_mooring == null) return;
            if (!AtOwnCleat()) { _workHeldSeconds = 0f; return; }

            if (workHeld)
            {
                _workHeldSeconds += Mathf.Max(0f, dt);
                if (_workHeldSeconds >= Mathf.Max(0.05f, _castOffHoldSeconds))
                {
                    _mooring.CastOff();
                    _phase = MooringPhase.Idle;
                    _workHeldSeconds = 0f;
                }
                return;
            }

            // Released before the cast-off threshold → it was a tap: step the scope.
            if (_workHeldSeconds > 0f)
            {
                _mooring.AdjustScope(slacken ? +1 : -1);
                _workHeldSeconds = 0f;
            }
        }

        /// <summary>True when the player stands at one of the cleats THIS line is made fast to — the near
        /// cleat is the line's own end, not merely some fitting nearby.</summary>
        private bool AtOwnCleat()
        {
            if (_nearCleat == null || _mooring == null) return false;
            return ReferenceEquals(_nearCleat, _mooring.BoatCleat)
                || ReferenceEquals(_nearCleat, _mooring.ShoreCleat);
        }

        // ---- pointer --------------------------------------------------------------------------------

        /// <summary>The mouse in world space under the main camera, or false when either is missing
        /// (headless / no camera yet) — a throw then simply cannot start, and nothing throws.</summary>
        private bool TryPointerWorld(out Vector2 world)
        {
            world = default;
            var mouse = Mouse.current;
            if (mouse == null) return false;
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
            if (_cam == null) return false;
            Vector2 screen = mouse.position.ReadValue();
            Vector3 w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            world = new Vector2(w.x, w.y);
            return true;
        }

        // ---- the live preview -----------------------------------------------------------------------

        /// <summary>
        /// Where the throw is currently aimed, as a world offset from the hand — the live arc the player
        /// watches while winding back. <b>This is <see cref="FlickCastMath.WindBackAimOffset"/> verbatim</b>,
        /// the same preview the rod draws, fed the same settings: the spot shown here is the spot a
        /// full-snap release lands on, and an under-snapped one falls short of it exactly as with a cast.
        /// Zero when nothing is wound back.
        /// </summary>
        public Vector2 AimOffset(Vector2 pointerWorld)
        {
            if (_phase != MooringPhase.WindBack) return Vector2.zero;
            FlickCastSettings cast = GameServices.Config != null
                ? GameServices.Config.FlickCast
                : FlickCastSettings.Default;
            return FlickCastMath.WindBackAimOffset(pointerWorld, HandPosition,
                                                   cast.FullRangeWindBackMetres, cast.MaxCastDistanceMetres);
        }

        /// <summary>The live wind-back charge 0..1, for any presentation that wants to scrub the throw
        /// animation — the same read the rod's castBack sheets use.</summary>
        public float WindBackCharge01(Vector2 pointerWorld)
        {
            if (_phase != MooringPhase.WindBack) return 0f;
            FlickCastSettings cast = GameServices.Config != null
                ? GameServices.Config.FlickCast
                : FlickCastSettings.Default;
            return FlickCastMath.WindBackCharge01(pointerWorld, HandPosition, cast.FullRangeWindBackMetres);
        }
    }
}
