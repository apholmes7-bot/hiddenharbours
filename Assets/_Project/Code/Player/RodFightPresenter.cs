using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;   // RodLineMath — the Art lane's pure line/bobber/ripple maths (see the PR note)

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The ROD-FIGHT PRESENTER — the one component that turns the published <see cref="FishingState"/>
    /// into everything the player SEES of rod fishing: the rod overlay pinned to the fisher's hands, the
    /// fishing line (the instrument — sag, whiten, shudder), the bobber (fly/float/nibble/strike), the
    /// count-the-fall sink ripples + the hit-bottom slack pop, the fish (a circling shadow while she's
    /// deep, the visible dart/thrash once she surfaces, the held catch on the land beat) and the small
    /// splash flourishes. Rod Fishing v2 shipped playing entirely in data — every sheet and anchor baked
    /// and committed, every read published — with NO component assembling them; this is that component.
    ///
    /// <para><b>Cross-module via Core only (rule 4)</b> — the <see cref="PlayerFishingAnimator"/> twin:
    /// it never references the Fishing module. Everything arrives in <see cref="FishingStateChanged"/>
    /// snapshots (phase, tension/bend/slack, the fight's <c>FishOffsetX/Y</c> far end, and the presenter
    /// wave's <c>CastCharge01</c>/<c>CastAimX/Y</c>/<c>RigDepthM</c> reads). The line/bobber/ripple
    /// SHAPES are the Art lane's <c>RodLineMath</c> (authored for this presenter — its class doc names
    /// the LineRenderer + SurfaceRipple/SplashBurst reuse this component implements); the WHAT/WHERE
    /// mapping is the pure <see cref="RodPresenterMath"/>.</para>
    ///
    /// <para><b>Anchors are DATA.</b> Every pin — grip, tip, bobber stem, fish mouth, the fisher's hands
    /// — comes from the baked anchor sidecars, parsed by the start builder into the serialized
    /// <see cref="RodStateVisual"/>/<see cref="BobberStateVisual"/>/<see cref="FishSpeciesVisual"/>
    /// tables in world metres (rule 6: nothing eyeballed, nothing restated). The rod overlay syncs to
    /// the <see cref="PlayerFishingAnimator"/>'s live pose/row/frame so the grip pin always matches the
    /// drawn fisher cell; rows in <c>_rodBehindDirs</c> draw the rod UNDER the body (the kit's layering
    /// rule).</para>
    ///
    /// <para><b>Null-safe per element, allocation-free per frame (rule 7).</b> A missing sheet or empty
    /// anchor table leaves THAT element inert — never a throw, never a magenta box. All buffers and
    /// child renderers are made once in <c>Awake</c>; steady state does no allocation, no string work
    /// (the species lookup runs only when <c>FishId</c> actually changes), no <c>GetComponent</c>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    // After the animator + y-sort have settled this frame's pose/order, and after DeckRiderVisual (100)
    // has drawn her body for this frame: the rod is drawn in the hands of the body as DRAWN. Before
    // CarryHands (102), which reads DrawsRod to hold the carried rod out of the picture.
    [DefaultExecutionOrder(101)]
    public sealed class RodFightPresenter : MonoBehaviour
    {
        private const int Directions = 8;

        [Header("The rod kit (builder-wired from RodIsoAnchors.json + the Rod_<tier>_* sheets)")]
        [Tooltip("Rod sheets+anchors in RodPresenterMath order: hold, bite, strike, reel, land, " +
                 "castBack, castRelease. The wired tier IS the tier seam — re-wire for a better rod.")]
        [SerializeField] private RodStateVisual[] _rodStates;
        [Tooltip("Direction rows whose rod draws UNDER the fisher (the kit's behindDirs).")]
        [SerializeField] private int[] _rodBehindDirs;

        [Header("The bobber (builder-wired from BobberAnchors.json + the Bobber_* sheets)")]
        [Tooltip("Bobber states in fixed order: float, nibble, strike, fly.")]
        [SerializeField] private BobberStateVisual[] _bobberStates;

        [Header("The fish (builder-wired from FishIsoAnchors.json + the Fish_* sheets)")]
        [SerializeField] private FishSpeciesVisual[] _species;
        [Tooltip("The shared fish art table. A species this scene's own table has no size rungs for " +
                 "(a scene saved before the ladder, or a species added since) is drawn from here. Empty " +
                 "= the Resources copy (FishArtLibrary).")]
        [SerializeField] private FishArtLibrary _fishArt;
        [Tooltip("Seconds per fish animation frame. The rig's own cadences are shadow 0.28 s, " +
                 "dart 0.08 s and thrash 0.11 s; this serialized playback cadence is independent.")]
        [SerializeField, Min(0.01f)] private float _fishSecondsPerFrame = 0.16f;
        [Tooltip("Surfaced-fish speed (m/s) above which she draws with the DART sheet facing her travel; " +
                 "below it she station-holds on the THRASH sheet. The rig's dart motion is 1.4 m/s.")]
        [SerializeField, Min(0f)] private float _fishDartSpeedMps = 1.2f;
        [Tooltip("FALLBACK seconds between a hooked JUMPER's jumps while she is up at the surface, for " +
                 "a species that states no FightJumpPeriodSeconds of its own. Only jumpers ever jump " +
                 "(FishSpeciesDef.BehaviorFlags Jumps, read through GameServices.FishBehaviour) - a cod " +
                 "never leaves the water however short this is. 0 = no jumps at all, the behaviour that " +
                 "predates the owner's 2026-09-06 ruling.")]
        [SerializeField, Min(0f)] private float _fightJumpPeriodSeconds = 4.5f;

        [Header("The fisher's hands (builder-wired from FisherFightAnchors.json — the land beat)")]
        [Tooltip("Midpoint of both hands, world m from the angler pivot, [dir·landFrames + frame].")]
        [SerializeField] private Vector2[] _landHandMid;
        [Tooltip("The right hand alone (one-handed holds), same indexing.")]
        [SerializeField] private Vector2[] _landHandRight;
        [SerializeField] private int _landFramesPerDir;

        [Header("Splash + ripple art (the RodLineMath-documented reuse)")]
        [SerializeField] private Sprite[] _splashFrames;
        [SerializeField, Min(0.1f)] private float _splashFps = 14f;
        [SerializeField] private Sprite _rippleSprite;

        [Header("The line (owner-tunable feel, rule 6)")]
        [Tooltip("Segments the line is drawn with (more = smoother sag/shudder). Visual only.")]
        [SerializeField, Range(2, 32)] private int _lineSegments = 12;
        [SerializeField, Min(0.005f)] private float _lineWidth = 0.03f;
        [Tooltip("World-metre belly at full slack — the hit-bottom sag read.")]
        [SerializeField, Min(0f)] private float _maxSagM = 0.55f;
        [SerializeField] private Color _lineColor = new Color(0.86f, 0.89f, 0.92f, 0.75f);
        [Tooltip("The near-snap colour the line whitens TOWARD as tension climbs.")]
        [SerializeField] private Color _strainColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("The colour a SLACK line falls to — dull and unlit, the far end of the same axis the " +
                 "strain colour sits at. One colour scale, one meaning: dull = she has given (REEL), " +
                 "bright = the line is loading (EASE OFF). Never two languages on one channel.")]
        [SerializeField] private Color _slackColor = new Color(0.52f, 0.57f, 0.62f, 0.62f);
        [Tooltip("How LATE the whitening arrives (RodLineMath.Whiten01 lateBias, ≥ 1). Lower = the line " +
                 "starts showing strain earlier. This is the fight's strain read now that the HUD bars " +
                 "are gone, so it arrives sooner than the old cozy 2.2.")]
        [SerializeField, Min(1f)] private float _whitenLateBias = 1.5f;
        [Tooltip("Tension at which the strain shudder starts (RodLineMath.StrainShudder01). Lower = the " +
                 "'ease off!' warning starts sooner — the player's only warning is what they can see.")]
        [SerializeField, Range(0f, 1f)] private float _shudderStart01 = 0.45f;
        [SerializeField, Min(0f)] private float _shudderAmpM = 0.09f;
        [SerializeField, Min(0f)] private float _shudderHz = 18f;
        [Tooltip("How far (degrees) the rod is hauled over toward the fish at FULL load — the fight's " +
                 "main instrument with the bars gone. It pivots about the hand, so the rod stays gripped. " +
                 "0 = off (the rod keeps whatever bend its sheet was drawn with).")]
        [SerializeField, Range(0f, 45f)] private float _loadLeanDegrees = 14f;
        [Tooltip("How far below the rod tip the line meets the water on the straight-down (weighted) " +
                 "path — the entry point the sink ripples ring.")]
        [SerializeField, Min(0f)] private float _entryDropM = 0.35f;

        [Header("Slack pop + rest sag (the tells)")]
        [Tooltip("How far past rest the belly kicks when the line suddenly slackens (0..1 of rest sag).")]
        [SerializeField, Range(0f, 1f)] private float _slackOvershoot01 = 0.8f;
        [SerializeField, Min(0.05f)] private float _slackSettleSeconds = 0.7f;
        [Tooltip("The resting line's taut read while waiting on a bobber.")]
        [SerializeField, Range(0f, 1f)] private float _restTaut = 0.35f;
        [Tooltip("The running line's taut read while the rig sinks.")]
        [SerializeField, Range(0f, 1f)] private float _sinkTaut = 0.7f;
        [Tooltip("A fight is never drawn slacker than this unless the slack tell says so.")]
        [SerializeField, Range(0f, 1f)] private float _fightTautFloor = 0.4f;

        [Header("Sink ripples (the count-the-fall read)")]
        [Tooltip("Rings per metre of fall — counting the pulses IS counting the depth (decision #4).")]
        [SerializeField, Min(0.01f)] private float _ripplesPerMeter = 0.75f;
        [SerializeField, Min(0.01f)] private float _rippleMinScale = 0.25f;
        [SerializeField, Min(0.01f)] private float _rippleMaxScale = 1.2f;

        [Header("Bobber + cast feel")]
        [Tooltip("How deep the nibble tell pulls the bobber under (m) at full dip.")]
        [SerializeField, Min(0f)] private float _bobberDipM = 0.16f;
        [Tooltip("Peak height (m) of the flying bobber's lob over the cast.")]
        [SerializeField, Min(0f)] private float _castArcHeightM = 0.8f;

        [Header("The deep shadow — she is UNSEEN, so this is how you read which way she's running")]
        [Tooltip("Idle drift (m) the shadow adds on top of her real position — a fish never holds " +
                 "perfectly still. Keep SMALL: this is garnish, and anything comparable to her real " +
                 "travel hides the very thing the player is trying to read.")]
        [SerializeField, Min(0f)] private float _shadowRadiusM = 0.18f;
        [SerializeField, Min(0.1f)] private float _shadowPeriodSeconds = 3.5f;
        [SerializeField, Range(0f, 1f)] private float _shadowSquash = 0.55f;
        [Tooltip("How firmly she must be RUNNING (0..1, eased) for the shadow to turn and face her run " +
                 "instead of drifting idly. Low: the whole point is that the shape points the way she is " +
                 "pulling the instant she starts pulling, because that is what you lean against.")]
        [SerializeField, Range(0f, 1f)] private float _shadowRunFacingThreshold01 = 0.25f;

        [Header("Her boil — the surface swirl over an unseen fish (the WHICH-WAY-TO-LEAN read)")]
        [Tooltip("Draw a swirl on the water directly over the deep fish, so her direction is legible " +
                 "at a glance instead of having to spot a dark shape. Reuses the sink-ripple sprite.")]
        [SerializeField] private bool _showDeepBoil = true;
        [Tooltip("Boil size (m) when she is running her hardest. It breathes with her effort, so the " +
                 "swirl SWELLING is itself the 'she's running now' tell.")]
        [SerializeField, Min(0f)] private float _boilRadiusM = 0.85f;
        [Tooltip("Boil size (m) in a slack window — she has stopped pulling and the water settles.")]
        [SerializeField, Min(0f)] private float _boilCalmRadiusM = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _boilAlpha = 0.5f;

        // The rod, the line and the splash ride the fisher's own order (the rod ±1; the line behind him and
        // the splash in front, Core EffectSorting, ADR 0059). The water elements below are not yet
        // re-anchored: an open point of step 2 (see EffectSortingTests' low-floor guard).
        [Header("Sorting (water elements)")]
        [SerializeField] private int _waterElementOrder = 49;    // bobber / surfaced fish / held under line
        [SerializeField] private int _shadowSortingOrder = 20;   // the deep shape sits under water detail
        [SerializeField] private int _rippleSortingOrder = 15;

        // ---- runtime ----------------------------------------------------------------------------
        private PlayerFishingAnimator _animator;
        private HiddenHarbours.Core.IsoCharacterSprite _isoSkin;
        private SpriteRenderer _playerSr;
        // Where her body is DRAWN against this root (aboard: the deck the hull's picture is at, tide and
        // rock included). Resolved once, on the first frame she fishes, like CarryHands' wiring: the
        // builder may add the rider after this component. Null = the root is where she is drawn.
        private DeckRiderVisual _rider;
        private bool _riderResolved;

        /// <summary>Pose ordinal → index into <c>_rodStates</c>, resolved by rig state NAME once in
        /// <c>Awake</c>/<c>Configure</c> (see <see cref="ResolveRodSwap"/>). −1 = unwired.</summary>
        private readonly int[] _rodSwap = new int[RodPresenterMath.PoseCount];

        private SpriteRenderer _rodSr;
        private Vector2 _rodGrip;              // where the rod is held this frame — the load bends it about here
        private Quaternion _rodBaseRotation;   // the unloaded pose the lean is measured off

        // Her TRUE travel while deep — the read the shadow faces and the boil trails. Tracked from the
        // published far end frame to frame, so it is her honest motion, never a decorative loop.
        private Vector2 _deepTravelDir = Vector2.right;
        private float _deepRunning01;      // eased 0..1: is she pulling right now (and so, is there a way to lean)
        private SpriteRenderer _bobberSr;
        private SpriteRenderer _fishSr;
        private SpriteRenderer _heldSr;
        private SpriteRenderer _splashSr;
        private SpriteRenderer _rippleSr;

        // The four pieces a villager's rod fight shares with this one (each reads no clock: this
        // presenter keeps its clocks and hands them the seconds they need every frame).
        private RodLineStrand _strand;
        private RodSplashFlourish _splash;
        private SurfacedFishDrawer _fish;
        private LandedCatchHold _landHold;

        /// <summary>How quickly her drawn speed (dart or thrash) follows this frame's, per frame. The
        /// player's own feel, kept on the player's side of the shared fish drawer.</summary>
        private const float FishSpeedSmoothing01 = 0.35f;

        private FishingState _s = FishingState.Idle;
        private float _stateClock;        // seconds in the current published phase
        private float _bobberClock;       // the bobber flipbook / dip-loop clock
        private float _slackEdgeClock;    // seconds since SlackWindowOpen turned true (the pop)
        private bool _slackWasOpen;
        private float _shadowTheta;       // the deep shadow's circling angle

        // HOW BIG SHE FEELS (owner's ask 2026-11-13; GameConfig.FishFeel through FishFeelMath). All of
        // it is derived from the published fight state and this presenter's own fight clock — nothing
        // is drawn at random, so a fight replays identically (rule 5) — and none of it allocates.
        private FishFeelSettings _feel = FishFeelSettings.Default;   // re-read once per LateUpdate
        private float _fightClock;        // seconds since the hook-set (the slam + the head-shake clock)
        private float _turnClock = float.PositiveInfinity;   // seconds since her last turn (the tip kick)
        private Vector2 _lastRunDir;      // her last published run direction — a turn is measured off it
        private Vector2 _runSwing;        // her run direction, eased: where the tip is being dragged
        private float _shakeSeed01;       // this fight's head-shake phase, from species + weight

        /// <summary>The rod's lean off its unloaded pose this frame, in degrees (signed, toward the
        /// pull). For tests/plates: it is the answer to "how far over is the rod", which is otherwise
        /// only a rotation on a child renderer.</summary>
        public float RodLeanDegrees { get; private set; }

        // The hooked jumper (owner's ruling 2026-09-06): resolved once per fight in SurfacedFishDrawer and
        // then read; nothing is drawn at random, so a fight replays identically (rule 5).
        private float _surfaceClock;      // seconds she has been UP — the frame the cadence is measured in

        /// <summary>
        /// IS SHE OUT OF THE WATER RIGHT NOW — the jump made observable, the same way
        /// <c>FishSchoolPresenter.VisibleSwimmers</c> makes its shoal observable. A plate fixture asserts
        /// on this rather than on "the code ran"; a PlayMode guard asserts a bass leaves the water and a
        /// cod never does.
        /// </summary>
        public bool FishAirborne => _fish != null && _fish.FishAirborne;

        /// <summary>How many jumps THIS fight has drawn. Reset when the species on the line changes, so
        /// it counts one fight and not a session.</summary>
        public int JumpsDrawn => _fish != null ? _fish.JumpsDrawn : 0;
        private Vector2 _lastFarEnd;      // where the line last ended (splash anchor for results)

        private float _splashClock;       // seconds since the live splash began (RodSplashFlourish)

        /// <summary>The species entry drawn for the current hook, or −1. For tests/tooling.</summary>
        public int SpeciesIndex => _fish != null ? _fish.SpeciesIndex : -1;

        /// <summary>
        /// The size rung drawn for the current hook, or null. For tests/tooling — it is the answer to
        /// "which of the three baked sizes is on the line", which nothing else can be asked.
        /// </summary>
        public FishRungVisual Rung => _fish?.Rung;

        /// <summary>Is the fight drawing its own rod in his hands this frame, sprite or mesh? While it is,
        /// the carried rod tool is not drawn (<c>CarryHands</c>), so he holds one rod and not two.</summary>
        public bool DrawsRod => DrawsMeshRod || MeshFigureOwnsRod || (_rodSr != null && _rodSr.enabled);

        /// <summary>True on a frame a mesh figure draws him and the rod he carries has a held mesh for it
        /// (<see cref="HeldRodMesh.SpriteMayDraw"/>): the mesh is then the rod's only look, so neither the
        /// fight's sprite rod nor the carried rod's sprite is drawn, even on a frame the mesh refuses.</summary>
        public bool MeshFigureOwnsRod { get; private set; }

        /// <summary>True on a frame the rod is drawn as its facet mesh in the mesh figure's hand
        /// (ADR 0053, <see cref="HeldRodMesh"/>); the sprite rod is off on exactly those frames.</summary>
        public bool DrawsMeshRod { get; private set; }

        /// <summary>The mesh rod's driver, once the rod has been asked for. For tests and plates.</summary>
        public HeldRodMesh MeshRod => _meshRod;

        /// <summary>Where the line left the rod on the last frame it was drawn, in world metres.</summary>
        public Vector2 LineAnchorWorld { get; private set; }

        private HeldRodMesh _meshRod;
        private CarryHands _hands;
        private DeckRiderMeshPresenter _meshPresenter;

        private void Awake()
        {
            _animator = GetComponent<PlayerFishingAnimator>();
            _isoSkin = GetComponent<HiddenHarbours.Core.IsoCharacterSprite>();
            _playerSr = GetComponent<SpriteRenderer>();
            // The exclamation over her head when a big one is hooked rides along with the rod fight, so
            // no scene or prefab needs editing to get it (GameConfig.FishFeel.BigOneReactionEnabled).
            if (GetComponent<BigOneReactionPresenter>() == null) gameObject.AddComponent<BigOneReactionPresenter>();

            _rodSr = MakeChild("RodOverlay", 0);
            _bobberSr = MakeChild("Bobber", _waterElementOrder);
            _fishSr = MakeChild("FishOnTheLine", _waterElementOrder);
            _heldSr = MakeChild("HeldCatch", 0);
            _splashSr = MakeChild("Splash", 0);   // ordered over the fisher as it plays (RenderSplash)
            _rippleSr = MakeChild("SinkRipple", _rippleSortingOrder);
            _strand = new RodLineStrand(transform, _lineWidth, _lineSegments);
            _splash = new RodSplashFlourish(_splashSr);
            _fish = new SurfacedFishDrawer(_fishSr);
            _landHold = new LandedCatchHold(transform);
            ResolveRodSwap();
            HideAll();
        }

        /// <summary>
        /// Resolve, ONCE, which wired entry each pose's rod comes from — by the entry's RIG STATE
        /// NAME, not by where it happens to sit in the array. A wired array is data someone can
        /// re-order (a prefab, a hand-fixed scene), and a swap that trusts position will then hand
        /// the cast beat the hold rod, or the strike rod, and look almost right while doing it.
        /// Resolving by name makes that a loud miss instead of a quiet substitution.
        ///
        /// <para>Also the one place the whole wired set is checked to be ONE rod
        /// (<see cref="RodPresenterMath.SameRod"/>). The importer checks it too, at bake time; this
        /// catches the prefab that was wired before the rig was fixed.</para>
        /// </summary>
        private void ResolveRodSwap()
        {
            for (int i = 0; i < _rodSwap.Length; i++) _rodSwap[i] = -1;
            if (_rodStates == null) return;

            for (int i = 0; i < _rodSwap.Length; i++)
            {
                string want = RodPresenterMath.RodStateFor((FishingPose)i);
                for (int s = 0; s < _rodStates.Length; s++)
                {
                    if (_rodStates[s] == null) continue;
                    if (string.Equals(_rodStates[s].State, want, System.StringComparison.Ordinal))
                    { _rodSwap[i] = s; break; }
                }
                if (_rodSwap[i] < 0)
                    Debug.LogWarning($"[RodFightPresenter] No wired rod state '{want}' for pose " +
                                     $"{(FishingPose)i} — that beat draws the hold rod. Re-run the " +
                                     "start builder after a fishing-kit bake.", this);
            }

            RodStateVisual reference = null;
            for (int s = 0; s < _rodStates.Length; s++)
            {
                if (_rodStates[s] == null) continue;
                if (reference == null) { reference = _rodStates[s]; continue; }
                if (!RodPresenterMath.SameRod(reference, _rodStates[s], out string why))
                    Debug.LogError($"[RodFightPresenter] Rod state '{_rodStates[s].State}' is not the " +
                                   $"same rod as '{reference.State}': {why}. One rod serves every " +
                                   "state — swapping to it would teleport or resize the rod in the " +
                                   "fisher's hand. Re-bake the fishing kit and re-run the builder.", this);
            }
        }

        private void OnEnable() => EventBus.Subscribe<FishingStateChanged>(OnFishingState);

        private void OnDisable()
        {
            EventBus.Unsubscribe<FishingStateChanged>(OnFishingState);
            HideAll();
            _splash?.Stop();
        }

        /// <summary>
        /// Public so tests can drive the presenter through the same path the bus uses. Safe before
        /// <c>Awake</c> (edit mode, an inactive object): the state and the clocks are taken, and the
        /// shared pieces, which Awake makes, simply catch up on the next publish after it.
        /// </summary>
        public void OnFishingState(FishingStateChanged e)
        {
            FishingState prev = _s;
            FishingState next = e.State;
            _s = next;

            // THE HOOK-SET: the frame a fight opens from anything that was not one. The slam and the
            // head-shake train are timed from here; her run memory starts clean.
            if (next.IsFightPhase && !prev.IsFightPhase)
            {
                _fightClock = 0f;
                _turnClock = float.PositiveInfinity;
                _lastRunDir = Vector2.zero;
                _runSwing = Vector2.zero;
                _shakeSeed01 = (SurfacedFishDrawer.FightSeed(next.FishId, next.WeightKg) & 0xFFFF) / 65536f;
            }

            // HER TURNS: a published run direction that swings far enough off the last one she ran in
            // kicks the tip (FishFeelMath.IsTurn — the threshold is data). Read off the SAME vector the
            // lean is scored against, never inferred from her drawn position.
            if (next.IsFightPhase)
            {
                if (FishFeelMath.IsTurn(_lastRunDir.x, _lastRunDir.y,
                                        next.FishRunDirX, next.FishRunDirY, in _feel))
                    _turnClock = 0f;
                if (next.FishRunDirX * next.FishRunDirX + next.FishRunDirY * next.FishRunDirY > 1e-8f)
                    _lastRunDir = new Vector2(next.FishRunDirX, next.FishRunDirY);
            }

            if (next.Phase != prev.Phase)
            {
                _stateClock = 0f;
                if (next.Phase == FishingPhase.Bite || next.Phase == FishingPhase.BiteNibble
                    || next.Phase == FishingPhase.Cast) _bobberClock = 0f;
                if (next.Phase == FishingPhase.FightDeep) _shadowTheta = 0f;
                // A jump is scheduled from the moment she came UP, so the first one cannot land in the
                // instant the surface phase opens (which would read as a glitch, not a fish).
                if (next.Phase == FishingPhase.FightSurface && prev.Phase != FishingPhase.FightSurface)
                {
                    _surfaceClock = 0f;
                    _fish?.OnSurfaced();
                }

                // The splash flourishes — surface breaks on the beats that break the surface. Which edge
                // throws one is the pure RodPresenterMath.SplashFor (first match wins); where is ours.
                Vector2 angler = transform.position;
                switch (RodPresenterMath.SplashFor(in prev, in next))
                {
                    case RodSplashEdge.Touchdown:
                        SplashAt(angler + new Vector2(next.CastAimX, next.CastAimY)); break;
                    case RodSplashEdge.PulledUnder:
                        SplashAt(angler + new Vector2(prev.CastAimX, prev.CastAimY)); break;
                    case RodSplashEdge.Breaks:
                        SplashAt(angler + new Vector2(next.FishOffsetX, next.FishOffsetY)); break;
                    case RodSplashEdge.Result:
                        SplashAt(_lastFarEnd); break;
                }
            }

            // The slack tell's edge — the pop plays from the false→true transition (RodLineMath doc).
            if (next.SlackWindowOpen && !_slackWasOpen) _slackEdgeClock = 0f;
            _slackWasOpen = next.SlackWindowOpen;

            // Species lookup only when the id actually changes, and the RUNG only when the species or
            // the weight moves (SurfacedFishDrawer.Resolve). What she does on the line is asked once
            // per species through the Core seam (rule 4 — Player does not reference Fishing).
            if (_fish != null && _fish.Resolve(in next, _species, _fishArt, GameServices.FishBehaviour, _fightJumpPeriodSeconds))
            {
                // The same fish as a mesh for the landing, at the sized kind the hands will carry it as.
                FishArtLibrary table = _fishArt != null ? _fishArt : FishArtLibrary.Shared;
                _landHold.Resolve(table != null ? table.CatchArt : null, next.FishId, next.WeightKg);
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _stateClock += dt;
            if (_s.Phase == FishingPhase.FightSurface) _surfaceClock += dt;
            _bobberClock += dt;
            _slackEdgeClock += dt;
            if (_splash.Active) _splashClock += dt;
            TickFishFeel(dt);
            if (_s.Phase == FishingPhase.FightDeep)
                _shadowTheta += dt * (2f * Mathf.PI) / Mathf.Max(0.1f, _shadowPeriodSeconds);

            RenderSplash();   // a flourish may outlive the interaction (the result beat) — always ticked

            if (!_s.IsActive)
            {
                HideInteraction(keepMeshRod: true);
                // THE CARRIED ROD (ADR 0053 D1): out of a fishing state the rod he carries is drawn as
                // its mesh at the rig's held pose on his hand, when the mesh body draws him and the rod's
                // Def asks for it. DrawsRod then holds the carried sprite out of the picture (CarryHands),
                // so he carries one rod, not two. A refusal hands the rod back to the carried sprite only
                // while his body is a sprite (MeshFigureOwnsRod): a mesh figure never shows the old sprite.
                DrawsMeshRod = TryDrawMeshRod();
                if (!DrawsMeshRod) _meshRod?.Hide();
                return;
            }

            // TWO ORIGINS. The water's elements (the bobber, the fish, the rings) are placed off the root,
            // as the cast and the fight publish them. What is IN HER HANDS (the rod, a landed fish) is
            // placed off the body as it is DRAWN: aboard, the hull's picture rides the tide and the rock
            // and this root does not, so a rod off the root alone floated above her at low water.
            Vector2 angler = transform.position;
            Vector2 hands = angler + DrawnBodyOffset();
            Vector2 castAim = new Vector2(_s.CastAimX, _s.CastAimY);
            bool castPath = castAim.sqrMagnitude > 1e-4f;
            RodElements show = RodPresenterMath.ElementsFor(_s.Phase, castPath);

            bool rodDrawn = RenderRod(show, hands, out Vector2 spriteTip);
            // THE MESH ROD (ADR 0053): while the mesh body plays the performance, the rod is its baked
            // facet mesh in his hand, aimed by the clip's own tool track, and the sprite rod is off. Any
            // refusal leaves the sprite rod exactly as RenderRod drew it.
            // A beat that draws no fight rod (the snap, no bite) still has the rod he carries in his hand:
            // on a mesh figure that is the mesh too, never the carried sprite (CarryHands reads DrawsRod).
            MeshFigureOwnsRod = false;
            DrawsMeshRod = TryDrawMeshRod();
            if (!DrawsMeshRod) _meshRod?.Hide();   // a refusal must not leave the last pose up
            if (DrawsMeshRod)
            {
                _rodSr.enabled = false;
                if ((show & RodElements.Rod) != 0)
                {
                    _rodGrip = _meshRod.GripWorld;
                    rodDrawn = true;
                }
            }
            else if (MeshFigureOwnsRod)
            {
                // A mesh figure never shows the old sprite rod: on a frame the mesh refuses, the line still
                // leaves from where the sprite's tip would be, and no rod is drawn.
                _rodSr.enabled = false;
            }
            Vector2 tip = RodPresenterMath.LineAnchor(DrawsMeshRod, DrawsMeshRod ? _meshRod.TipWorld : spriteTip, spriteTip);
            Vector2 unloadedTip = tip;   // the rod's own tip, before the fight's load bends it over

            // ---- the line's far end (and the element that lives there) ---------------------------
            Vector2 far = tip;
            bool farValid = rodDrawn;

            if ((show & RodElements.Bobber) != 0)
            {
                if (RenderBobber(angler, castAim, out Vector2 attach)) { far = attach; farValid = rodDrawn; }
                else if (castPath) far = angler + castAim;
            }
            else _bobberSr.enabled = false;

            Vector2 entry = tip + Vector2.down * _entryDropM;   // the straight-down path's water entry
            bool ripplesShown = (show & RodElements.SinkRipples) != 0;
            if (ripplesShown) RenderRipple(entry);

            if ((show & RodElements.FishShadow) != 0)
            {
                entry = angler + new Vector2(_s.FishOffsetX, _s.FishOffsetY);
                TrackDeepTravel(dt);
                RenderShadow(entry);
                // Her BOIL rides the same renderer the sink rings use — it is idle through a fight, and a
                // swirl on the water over an unseen fish is the most legible "she's over THERE, running
                // THAT way" the world can give without a single pixel of UI.
                if (_showDeepBoil && !ripplesShown) { RenderBoil(entry); ripplesShown = true; }
                far = entry;
            }
            else if ((show & RodElements.FishSurface) != 0)
            {
                far = RenderSurfaceFish(angler, dt);
                // She's visible now, but the READ must not change under the player halfway through the
                // fight: the same swirl keeps marking the same run direction, so the lean is one
                // continuous skill from the hookup to the net rather than two different games.
                TrackDeepTravel(dt);
                if (_showDeepBoil && !ripplesShown) { RenderBoil(far); ripplesShown = true; }
            }
            else
            {
                _fish.Hide();   // the on-the-line fish never lingers into land/result beats
            }

            if (!ripplesShown) _rippleSr.enabled = false;
            bool fishShown = (show & (RodElements.FishShadow | RodElements.FishSurface)) != 0;
            if (!fishShown) _deepRunning01 = 0f;

            if ((show & RodElements.HeldFish) != 0) RenderHeldFish(hands);
            else { _heldSr.enabled = false; HideLandMesh(); }

            // The weighted path's line runs straight down to the entry point.
            if ((show & RodElements.Line) != 0 && !castPath
                && (_s.Phase == FishingPhase.Waiting || _s.Phase == FishingPhase.Sinking
                    || _s.Phase == FishingPhase.Bite || _s.Phase == FishingPhase.BiteNibble
                    || _s.Phase == FishingPhase.Fighting))
                far = tip + Vector2.down * _entryDropM;

            // THE ROD IS THE GAUGE (owner's ruling 2026-07-23 — the fight's HUD bars are gone). Now that
            // the far end is known, haul the rod over toward whatever is pulling on it, by how hard it's
            // loaded. Done here rather than in RenderRod because the pull direction IS the far end.
            RodLeanDegrees = 0f;
            if (rodDrawn && _s.IsFightPhase) tip = LeanRodUnderLoad(unloadedTip, far);

            if ((show & RodElements.Line) != 0 && farValid) { RenderLine(tip, far); LineAnchorWorld = tip; }
            else _strand.Hide();

            if (farValid) _lastFarEnd = far;
        }

        /// <summary>Where her body is drawn against this root, in world metres
        /// (<see cref="DeckRiderVisual.DrawnBodyOffset"/>, stated by the rider at order 100, before this
        /// component's 101). Zero with no rider and zero ashore.</summary>
        private Vector2 DrawnBodyOffset()
        {
            if (!_riderResolved)
            {
                _rider = GetComponent<DeckRiderVisual>();
                _riderResolved = true;
            }
            return _rider != null ? _rider.DrawnBodyOffset : Vector2.zero;
        }

        // ---- the mesh rod (ADR 0053) -------------------------------------------------------------

        /// <summary>
        /// Ask <see cref="HeldRodMesh"/> to draw the rod in the mesh figure's hand this frame: the rod he
        /// carries (<see cref="CarryHands.HeldFightRod"/>), the figure the deck presenter is drawing him
        /// with right now (aboard or ashore), and <see cref="GameConfig.MeshRodInHand"/>. The figure was
        /// posed for this frame at order 100, before this component's 101. The wiring is looked up with
        /// TryGetComponent while missing (the builder may add it after this component), which does not
        /// allocate.
        /// </summary>
        private bool TryDrawMeshRod()
        {
            GameConfig config = GameServices.Config;
            bool on = config != null && config.MeshRodInHand;
            MeshFigureOwnsRod = false;
            if (!on && _meshRod == null) return false;
            if (_hands == null) TryGetComponent(out _hands);
            if (_meshPresenter == null) TryGetComponent(out _meshPresenter);
            _meshRod ??= new HeldRodMesh(transform);
            // The rod he carries: in his hand, or tucked away for a catch (CarryHands.SlungFightRod). While
            // he swims (CarryHands.ToolsStowed) it is not in his hand: a rod whose Def slings it there
            // (StowWhileSwimming) rides his back, any other draws nothing. Every input is state he already
            // has; nothing here is saved.
            ToolDef rod = null;
            HeldRodMesh.Use use = HeldRodMesh.Use.Free;
            if (_hands != null)
            {
                rod = _hands.HeldFightRod;
                if (rod == null) rod = _hands.SlungFightRod;
                if (_hands.ToolsStowed && rod != null && !(rod.StowOnBack && rod.StowWhileSwimming)) rod = null;
                use = _s.IsActive ? HeldRodMesh.Use.Fishing
                    : _hands.FightRodHandsBusy || _hands.ToolsStowed ? HeldRodMesh.Use.HandsBusy
                    : HeldRodMesh.Use.Free;
            }
            float width = config != null ? config.HeldPropLineWidthPx : GameConfig.DefaultHeldPropLineWidthPx;
            IsoCharacterFigureRenderer figure = LiveFigure();
            ToolDef carried = rod != null ? rod : _hands != null ? _hands.HeldFightRod : null;
            MeshFigureOwnsRod = !HeldRodMesh.SpriteMayDraw(on, carried, figure != null);
            return _meshRod.TryDraw(on, rod, figure, width, use, Time.deltaTime);
        }

        /// <summary>The figure drawing him this frame, or null while he is a sprite.</summary>
        private IsoCharacterFigureRenderer LiveFigure()
        {
            DeckRiderMeshPresenter p = _meshPresenter;
            if (p == null || !p.DrawsInsteadOfSprite) return null;
            return p.DrawsAshore ? p.AshoreFigure : p.Figure;
        }

        // ---- the landed fish as a mesh (ADR 0053) --------------------------------------------------

        /// <summary>The landed-fish mesh, once made. For tests and plates.</summary>
        public HeldCatchMesh LandMesh => _landHold?.Mesh;

        /// <summary>
        /// The fish he holds up at the landing, as its mesh hanging from his FREE hand (the land clip aims
        /// the rod in his right, so the fish hangs from the left socket), when the mesh figure plays the
        /// landing and the species has a mesh at its size rung. False draws the sprite as before. The
        /// hand pick and the drawing are <see cref="LandedCatchHold"/>'s, on the figure drawing him now.
        /// </summary>
        private bool TryDrawLandMesh()
        {
            GameConfig config = GameServices.Config;
            if (!_landHold.Wants(config)) return false;
            if (_meshPresenter == null) TryGetComponent(out _meshPresenter);
            return _landHold.TryDraw(config, LiveFigure());
        }

        private void HideLandMesh() => _landHold?.Hide();

        private void OnDestroy()
        {
            _landHold?.Dispose();
            _meshRod?.Dispose();
            _meshRod = null;
        }

        // ---- the rod overlay ---------------------------------------------------------------------

        private bool RenderRod(RodElements show, Vector2 angler, out Vector2 tip)
        {
            tip = angler;
            if ((show & RodElements.Rod) == 0 || _rodStates == null
                || _rodStates.Length != RodPresenterMath.RodStateCount || _playerSr == null)
            {
                _rodSr.enabled = false;
                return false;
            }

            bool posed = _animator != null && _animator.OwnsRenderer;
            FishingPose pose = posed ? _animator.Pose : FishingPose.None;
            int row = posed ? _animator.Row
                            : (_isoSkin != null && _isoSkin.HasArt ? _isoSkin.FacingRow
                                                                   : (_animator != null ? _animator.Row : Directions / 2));
            int frame = posed ? _animator.Frame : 0;

            int p = (int)pose;
            int slot = p >= 0 && p < _rodSwap.Length ? _rodSwap[p] : -1;
            RodStateVisual state = slot >= 0 && slot < _rodStates.Length
                ? _rodStates[slot]
                : _rodStates[RodPresenterMath.RodSheetFor(pose)];
            if (state == null || state.Frames == null || state.Frames.Length == 0)
            {
                _rodSr.enabled = false;
                return false;
            }

            int idx = RodPresenterMath.SheetIndex(row, Mathf.Min(frame, state.FramesPerDir - 1),
                                                  state.FramesPerDir, state.Frames.Length);
            if (idx < 0 || state.Frames[idx] == null
                || state.GripOffsets == null || idx >= state.GripOffsets.Length
                || state.TipOffsets == null || idx >= state.TipOffsets.Length)
            {
                _rodSr.enabled = false;
                return false;
            }

            Vector2 grip = angler + state.GripOffsets[idx];
            tip = grip + state.TipOffsets[idx];
            _rodGrip = grip;

            _rodSr.transform.position = new Vector3(grip.x, grip.y, 0f);
            // The sheet's own pose is the baseline every frame (local identity — exactly how the rod drew
            // before the load lean existed); LeanRodUnderLoad then bends it off this, in WORLD terms.
            _rodSr.transform.localRotation = Quaternion.identity;
            _rodBaseRotation = _rodSr.transform.rotation;

            _rodSr.sprite = state.Frames[idx];
            _rodSr.sortingLayerID = _playerSr.sortingLayerID;
            _rodSr.sortingOrder = _playerSr.sortingOrder + (IsBehindRow(row) ? -1 : 1);
            _rodSr.enabled = true;
            return true;
        }

        /// <summary>
        /// Haul the rod over toward whatever is pulling on it — the fight's primary instrument now that the
        /// bars are gone. The rod pivots about the GRIP (the hand; the sheet's own pin, so the art stays in
        /// the fisher's hands) toward the line's far end, by <see cref="_loadLeanDegrees"/> scaled by how
        /// loaded it is. Load is the WORSE of what she's doing to the rod (<c>RodBend01</c> — her run plus
        /// any leaning-with-her) and what the line is actually taking (<c>Tension01</c>), so the rod is bowed
        /// over hardest exactly when the line is nearest parting — and it springs straight the instant she
        /// gives. Returns the rotated tip so the line still leaves the rod at its tip.
        /// </summary>
        private Vector2 LeanRodUnderLoad(Vector2 tip, Vector2 far)
        {
            float baseLoad = FightLoad01();
            // HER SIZE AND HER MOVEMENT ON THE ROD (owner's ask 2026-11-13, GameConfig.FishFeel). A
            // hand-gathered catch (Tending) is not a hooked fish and keeps the plain lean.
            bool hooked = _s.Phase != FishingPhase.Tending;
            float size = hooked ? _s.Size01 : 0f;
            float load = hooked
                ? FishFeelMath.RodLoad01(baseLoad, size, _s.SlackWindowOpen, in _feel) : baseLoad;
            float maxLean = hooked
                ? FishFeelMath.MaxLeanDegrees(_loadLeanDegrees, size, in _feel) : _loadLeanDegrees;

            // The sharp, short moves that ride on top of the held bow: her head-shakes (a pulse train
            // paced and sized by her weight, live only while she is working), the kick of a turn, and
            // the hook-set slam. All closed-form in this fight's own clocks.
            float extra = 0f;
            if (hooked)
            {
                float effort = _s.SlackWindowOpen ? 0f : baseLoad;
                extra = FishFeelMath.TipPulseDegrees(_fightClock, size, effort, _shakeSeed01, in _feel)
                      + FishFeelMath.TurnKickDegrees(_turnClock, size, in _feel)
                      + FishFeelMath.HookSlamDegrees(_fightClock, size, in _feel);
            }

            Vector2 shaft = tip - _rodGrip;
            Vector2 pull = far - _rodGrip;
            // HER RUN DRAGS THE TIP: the point the rod leans toward is carried along her (eased) run
            // direction, further for a heavier fish — so a run reads on the rod, and a turn swings it.
            if (hooked) pull += _runSwing * FishFeelMath.RunPullMetres(size, in _feel);
            if ((load <= 1e-3f && extra <= 1e-3f) || _loadLeanDegrees <= 0f
                || shaft.sqrMagnitude < 1e-6f || pull.sqrMagnitude < 1e-6f) return tip;

            // Lean TOWARD the pull, never past it (a rod bends to the fish, it doesn't overshoot her).
            float toPull = Vector2.SignedAngle(shaft, pull);
            float lean = Mathf.Clamp(toPull, -maxLean, maxLean) * load;
            if (hooked)
            {
                lean += (toPull >= 0f ? 1f : -1f) * extra;
                lean = Mathf.Clamp(lean, -_feel.MaxTotalLeanDegrees, _feel.MaxTotalLeanDegrees);
            }
            RodLeanDegrees = lean;
            var rot = Quaternion.Euler(0f, 0f, lean);              // a WORLD-space delta off the baseline
            if (DrawsMeshRod) _meshRod.Lean(lean);                 // the same picture-space turn, about the grip
            else _rodSr.transform.rotation = rot * _rodBaseRotation;   // composes with any parent rotation
            return _rodGrip + (Vector2)(rot * shaft);
        }

        /// <summary>The fight's own load read, 0..1: the WORSE of what she is doing to the rod
        /// (<c>RodBend01</c>) and what the line is taking (<c>Tension01</c>).</summary>
        private float FightLoad01() => RodPresenterMath.FightLoad01(_s.RodBend01, _s.Tension01);

        /// <summary>One frame of the big-fish feel's own state: re-read the dials (so the owner can
        /// tune <c>GameConfig.FishFeel</c> live), advance the fight clocks, and ease the run swing toward
        /// her published run direction (or back to rest when she is not running). No allocation.</summary>
        private void TickFishFeel(float dt)
        {
            _feel = GameServices.Config != null ? GameServices.Config.FishFeel : FishFeelSettings.Default;
            if (!_s.IsFightPhase) { _runSwing = Vector2.zero; return; }

            float step = Mathf.Max(0f, dt);
            _fightClock += step;
            if (!float.IsPositiveInfinity(_turnClock)) _turnClock += step;

            var run = new Vector2(_s.FishRunDirX, _s.FishRunDirY);
            float mag = run.magnitude;
            Vector2 target = mag > 1e-4f ? run / mag : Vector2.zero;
            _runSwing = Vector2.Lerp(_runSwing, target, 1f - Mathf.Exp(-_feel.RunSwingPerSec * step));
        }

        private bool IsBehindRow(int row)
        {
            if (_rodBehindDirs == null) return false;
            for (int i = 0; i < _rodBehindDirs.Length; i++)
                if (_rodBehindDirs[i] == row) return true;
            return false;
        }

        // ---- the bobber ----------------------------------------------------------------------------

        // Fixed state order in _bobberStates (the builder's contract): float, nibble, strike, fly.
        private const int BobFloat = 0, BobNibble = 1, BobStrike = 2, BobFly = 3;

        private bool RenderBobber(Vector2 angler, Vector2 castAim, out Vector2 attach)
        {
            attach = default;
            if (_bobberStates == null || _bobberStates.Length < 4) { _bobberSr.enabled = false; return false; }

            int stateIdx = _s.Phase switch
            {
                // §10.2 split: BiteNibble is the TEASE (the small dip you must not strike on); Bite is
                // the TRUE TAKE — the strike art plays once and the bobber stays gone: pulled under IS
                // the read. A legacy no-BiteDef species publishes Bite for its whole forgiving window,
                // so its bobber now reads taken-under rather than nibbling — the honest picture, since
                // that bite always hooks.
                FishingPhase.Cast => BobFly,
                FishingPhase.BiteNibble => BobNibble,
                FishingPhase.Bite => BobStrike,
                FishingPhase.Fighting => BobStrike,
                _ => BobFloat,
            };
            BobberStateVisual state = _bobberStates[stateIdx];
            if (state == null || state.Frames == null || state.Frames.Length == 0)
            {
                _bobberSr.enabled = false;
                return false;
            }

            int frame;
            if (stateIdx == BobStrike)
            {
                // Pulled under: the strike plays ONCE as the legacy fight opens, then the bobber is gone.
                frame = RodPresenterMath.OnceFlipFrame(_stateClock, state.SecondsPerFrame, state.Frames.Length);
                if (frame >= state.Frames.Length) { _bobberSr.enabled = false; return false; }
            }
            else
            {
                frame = RodPresenterMath.FlipFrame(_bobberClock, state.SecondsPerFrame, state.Frames.Length);
            }

            Vector2 pos = angler + castAim;
            if (_s.Phase == FishingPhase.Cast)
                pos.y += RodPresenterMath.ArcLift(_s.CastCharge01, _castArcHeightM);
            if (_s.Phase == FishingPhase.BiteNibble)   // the tease's small dip (§10.2)
            {
                float loopSeconds = Mathf.Max(1e-3f, state.SecondsPerFrame * state.Frames.Length);
                pos.y -= _bobberDipM * RodLineMath.BobberDip01(_bobberClock / loopSeconds);
            }

            _bobberSr.transform.position = new Vector3(pos.x, pos.y, 0f);
            _bobberSr.sprite = state.Frames[frame];
            _bobberSr.sortingOrder = _waterElementOrder;
            _bobberSr.enabled = true;

            attach = pos;
            if (state.LineAttachOffsets != null && frame < state.LineAttachOffsets.Length)
                attach += state.LineAttachOffsets[frame];
            return true;
        }

        // ---- the fish ------------------------------------------------------------------------------

        /// <summary>
        /// Follow WHICH WAY SHE IS PULLING, straight off the published <c>FishRunDirX/Y</c> — the very
        /// vector the player's lean is scored against, so the shape on the water and the maths behind it
        /// can never disagree. (Deriving it from her frame-to-frame movement instead would: she can be
        /// mid-run — a direction you are judged on — while hovering nearly still. See the Core field.)
        /// Eased so the shape TURNS rather than snapping, and held through her slack windows: when she
        /// stops pulling the arrow simply stops being drawn, it doesn't spin to a fresh lie.
        /// </summary>
        private void TrackDeepTravel(float dt)
        {
            var run = new Vector2(_s.FishRunDirX, _s.FishRunDirY);
            float mag = run.magnitude;
            if (mag > 1e-4f)
            {
                float k = 1f - Mathf.Exp(-8f * Mathf.Max(0f, dt));
                _deepTravelDir = Vector2.Lerp(_deepTravelDir, run / mag, k).normalized;
                _deepRunning01 = Mathf.Lerp(_deepRunning01, 1f, k);
            }
            else
            {
                _deepRunning01 = Mathf.Lerp(_deepRunning01, 0f, 1f - Mathf.Exp(-8f * Mathf.Max(0f, dt)));
            }
        }

        /// <summary>
        /// HER BOIL — the swirl of water an unseen fish pushes up as she works below (owner ask
        /// 2026-07-24: make the fight readable, effects allowed, no UI). It rides her true position, so
        /// watching it slide tells you which way to lean; and it BREATHES with her effort — swelling as
        /// she runs, settling in a slack window — so the same swirl also says when to reel. Two of the
        /// three reads the player needs, in one diegetic mark, on a renderer that was idle anyway.
        /// </summary>
        private void RenderBoil(Vector2 pos)
        {
            if (_rippleSprite == null) { _rippleSr.enabled = false; return; }

            // Effort read straight off the published rod load: 1 while she runs, ~0 when she gives.
            float working = Mathf.Clamp01(_s.RodBend01);
            float radius = Mathf.Lerp(_boilCalmRadiusM, _boilRadiusM, working);
            if (radius <= 0f) { _rippleSr.enabled = false; return; }

            // A slow churn so it never reads as a frozen decal, plus a swell on her effort.
            float churn = 1f + 0.12f * Mathf.Sin(Time.time * 3.1f);
            float scale = radius * churn;

            _rippleSr.transform.position = new Vector3(pos.x, pos.y, 0f);

            // THE SHAPE POINTS THE WAY. Round while she holds; DRAWN OUT along her run once she pulls —
            // water shouldered aside by a fish going somewhere. So even a glance at the swirl answers
            // "which way is she taking me", which is the whole question the lean asks.
            float stretch = Mathf.Lerp(1f, 1.7f, _deepRunning01);
            _rippleSr.transform.localScale = new Vector3(scale * stretch, scale * _shadowSquash / stretch, 1f);
            _rippleSr.transform.rotation = _deepRunning01 > 0.05f
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(_deepTravelDir.y, _deepTravelDir.x) * Mathf.Rad2Deg)
                : Quaternion.identity;
            _rippleSr.sprite = _rippleSprite;
            _rippleSr.sortingOrder = _rippleSortingOrder;

            Color c = _rippleSr.color;
            c.a = _boilAlpha * Mathf.Lerp(0.55f, 1f, working);   // she is loudest when she is pulling
            _rippleSr.color = c;
            _rippleSr.enabled = true;
        }

        private void RenderShadow(Vector2 entry)
        {
            _fish.HideUnderwater();   // the deep shadow is drawn whole on her renderer
            FishRungVisual sp = _fish.Rung;
            if (sp == null || sp.ShadowFrames == null || sp.ShadowFrames.Length == 0)
            {
                _fishSr.enabled = false;
                return;
            }

            Vector2 pos = entry + RodPresenterMath.ShadowOffset(_shadowTheta, _shadowRadiusM, _shadowSquash);

            // SHE FACES WHERE SHE IS ACTUALLY GOING (owner ask 2026-07-24 — "more readable as to what to
            // do"). This used to take its heading purely from the decorative circle above, so the shape
            // pointed wherever a 3.5 s loop happened to point — the player watching the one thing that
            // should say "lean the other way" was reading noise. Her real travel wins whenever she is
            // meaningfully moving; only when she is holding station does the idle drift supply a facing.
            float heading = _deepRunning01 > _shadowRunFacingThreshold01
                ? RodPresenterMath.HeadingDegrees(_deepTravelDir.x, _deepTravelDir.y)
                : RodPresenterMath.ShadowHeadingDegrees(_shadowTheta, _shadowSquash);
            int row = IsoFacing.HeadingToFacingIndex(heading, Directions, 0f, false);
            int frame = RodPresenterMath.FlipFrame(_stateClock, _fishSecondsPerFrame, sp.ShadowFramesPerDir);
            int idx = RodPresenterMath.SheetIndex(row, frame, sp.ShadowFramesPerDir, sp.ShadowFrames.Length);
            if (idx < 0 || sp.ShadowFrames[idx] == null) { _fishSr.enabled = false; return; }

            _fishSr.transform.position = new Vector3(pos.x, pos.y, 0f);
            _fishSr.sprite = sp.ShadowFrames[idx];
            _fishSr.sortingOrder = _shadowSortingOrder;
            _fishSr.enabled = true;
        }

        /// <summary>The surfaced fight: dart/thrash at the published offset, facing her travel, the
        /// line attached at the baked MOUTH anchor (<see cref="SurfacedFishDrawer"/>). Returns the line's
        /// far end. A hooked jumper's re-entry splash comes back to this presenter's own flourish, the
        /// same surface break every other beat of this fight uses.</summary>
        private Vector2 RenderSurfaceFish(Vector2 angler, float dt)
        {
            // Behind him while she is in the water beyond his feet, in front only short of them (Core
            // EffectSorting.FishOnLineOrder); in his hands she is the held catch, in front by its own rule.
            int fishOrder = _playerSr != null
                ? EffectSorting.FishOnLineOrder(_playerSr.sortingOrder, _s.FishOffsetY)
                : _waterElementOrder;
            Vector2 mouth = _fish.Render(angler, in _s, dt, _stateClock, _surfaceClock, _fishSecondsPerFrame,
                                         _fishDartSpeedMps, FishSpeedSmoothing01, fishOrder, 1f,
                                         out bool splashDue, out Vector2 splashAt);
            if (splashDue) SplashAt(splashAt);
            return mouth;
        }

        /// <summary>The landed catch in the fisher's hands: the species' held sheet (gill or tail — a
        /// build-time pick from the rig's hold.hands) pinned to the LAND pose's baked hand anchors,
        /// riding the fisher's live row/frame. Skipped whole when the land pose isn't showing.</summary>
        private void RenderHeldFish(Vector2 angler)
        {
            FishRungVisual sp = _fish.Rung;
            bool landPosed = _animator != null && _animator.OwnsRenderer && _animator.Pose == FishingPose.Land;
            if (landPosed && TryDrawLandMesh()) { _heldSr.enabled = false; return; }
            if (sp == null || sp.HeldFrames == null || sp.HeldFrames.Length == 0 || !landPosed
                || _landFramesPerDir <= 0 || _playerSr == null)
            {
                _heldSr.enabled = false;
                return;
            }

            int row = _animator.Row;
            Vector2[] hands = sp.TwoHanded ? _landHandMid : _landHandRight;
            int handIdx = RodPresenterMath.SheetIndex(row, Mathf.Min(_animator.Frame, _landFramesPerDir - 1),
                                                      _landFramesPerDir, hands != null ? hands.Length : 0);
            if (handIdx < 0) { _heldSr.enabled = false; return; }

            int frame = RodPresenterMath.FlipFrame(_stateClock, _fishSecondsPerFrame, sp.HeldFramesPerDir);
            int idx = RodPresenterMath.SheetIndex(row, frame, sp.HeldFramesPerDir, sp.HeldFrames.Length);
            if (idx < 0 || sp.HeldFrames[idx] == null) { _heldSr.enabled = false; return; }

            Vector2 pos = angler + hands[handIdx];
            _heldSr.transform.position = new Vector3(pos.x, pos.y, 0f);
            _heldSr.sprite = sp.HeldFrames[idx];
            _heldSr.sortingLayerID = _playerSr.sortingLayerID;
            _heldSr.sortingOrder = _playerSr.sortingOrder + 1;   // in the hands, in front of the body
            _heldSr.enabled = true;
        }

        // ---- the line ------------------------------------------------------------------------------

        /// <summary>
        /// How this presenter's line looks, read from its inspector fields on EVERY render (never cached
        /// in Awake), so a value tuned live is in the very next frame.
        /// </summary>
        private RodLineLook LineLook() => new RodLineLook
        {
            MaxSagM = _maxSagM,
            LineColor = _lineColor,
            StrainColor = _strainColor,
            SlackColor = _slackColor,
            WhitenLateBias = _whitenLateBias,
            ShudderStart01 = _shudderStart01,
            ShudderAmpM = _shudderAmpM,
            ShudderHz = _shudderHz,
            RestTaut = _restTaut,
            SinkTaut = _sinkTaut,
            FightTautFloor = _fightTautFloor,
            SlackOvershoot01 = _slackOvershoot01,
            SlackSettleSeconds = _slackSettleSeconds,
        };

        private void RenderLine(Vector2 tip, Vector2 far)
        {
            // ADR 0059: the line rides the fisher's own order, behind him (Core EffectSorting.RodLineRungs),
            // not a parked 50. The shudder's phase is taken at Time.time, as it always was for the player.
            bool sorted = _playerSr != null;
            int lineOrder = sorted ? EffectSorting.OrderOver(SortedEffect.RodLine, _playerSr.sortingOrder) : 0;
            RodLineLook look = LineLook();
            _strand.Render(tip, far, in _s, in look, in _feel, FightLoad01(), _slackEdgeClock,
                           Time.time, sorted, lineOrder);
        }

        // ---- ripples + splash ------------------------------------------------------------------------

        private void RenderRipple(Vector2 entry)
        {
            if (_rippleSprite == null || _s.RigDepthM <= 0f) { _rippleSr.enabled = false; return; }

            float phase = RodLineMath.SinkRipplePhase(_s.RigDepthM, _ripplesPerMeter);
            float age = RodLineMath.RingAge01(phase);
            float scale = Mathf.Lerp(_rippleMinScale, _rippleMaxScale, age);

            _rippleSr.transform.position = new Vector3(entry.x, entry.y, 0f);
            _rippleSr.transform.localScale = new Vector3(scale, scale, 1f);
            _rippleSr.sprite = _rippleSprite;
            var c = Color.white;
            c.a = (1f - age) * 0.8f;
            _rippleSr.color = c;
            _rippleSr.enabled = true;
        }

        private void SplashAt(Vector2 pos)
        {
            if (_splash != null && _splash.Begin(pos, _splashFrames)) _splashClock = 0f;
        }

        private void RenderSplash()
        {
            // The splash rides the fisher's own order (ADR 0059): on the water behind him when it lands beyond
            // his feet, one rung in front only short of them (Core EffectSorting.RodSplashOrder).
            bool sorted = _playerSr != null;
            int splashOrder = sorted
                ? EffectSorting.RodSplashOrder(_playerSr.sortingOrder, _splash.Position.y - transform.position.y)
                : 0;
            _splash.Render(_splashClock, _splashFrames, _splashFps, sorted, splashOrder);
        }

        // ---- plumbing --------------------------------------------------------------------------------

        private void HideInteraction() => HideInteraction(keepMeshRod: false);

        private void HideInteraction(bool keepMeshRod)
        {
            if (_rodSr != null) _rodSr.enabled = false;
            if (!keepMeshRod)
            {
                _meshRod?.Hide();
                DrawsMeshRod = false;
                MeshFigureOwnsRod = false;
            }
            if (_bobberSr != null) _bobberSr.enabled = false;
            if (_fishSr != null) _fishSr.enabled = false;
            _fish?.HideUnderwater();
            if (_heldSr != null) _heldSr.enabled = false;
            HideLandMesh();
            if (_rippleSr != null) _rippleSr.enabled = false;
            _strand?.Hide();
        }

        private void HideAll()
        {
            HideInteraction();
            _splash?.Hide();
        }

        private SpriteRenderer MakeChild(string childName, int order)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <summary>Wire the presenter in one call (the start builder / tests) — every table is the
        /// parsed-and-converted kit data; null/empty entries leave that element inert.</summary>
        public void Configure(RodStateVisual[] rodStates, int[] rodBehindDirs,
                              BobberStateVisual[] bobberStates, FishSpeciesVisual[] species,
                              Vector2[] landHandMid, Vector2[] landHandRight, int landFramesPerDir,
                              Sprite[] splashFrames, Sprite rippleSprite)
        {
            _rodStates = rodStates;
            _rodBehindDirs = rodBehindDirs;
            _bobberStates = bobberStates;
            _species = species;
            _landHandMid = landHandMid;
            _landHandRight = landHandRight;
            _landFramesPerDir = landFramesPerDir;
            _splashFrames = splashFrames;
            _rippleSprite = rippleSprite;
            ResolveRodSwap();
        }
    }
}
