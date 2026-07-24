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
    [DefaultExecutionOrder(60)]   // after the animator + y-sort have settled this frame's pose/order
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
        [Tooltip("Seconds per fish animation frame (shadow/dart/thrash/held all share the kit cadence).")]
        [SerializeField, Min(0.01f)] private float _fishSecondsPerFrame = 0.16f;
        [Tooltip("Surfaced-fish speed (m/s) above which she draws with the DART sheet facing her travel; " +
                 "below it she station-holds on the THRASH sheet.")]
        [SerializeField, Min(0f)] private float _fishDartSpeedMps = 1.2f;

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
        [Tooltip("The near-snap colour the line whitens TOWARD as tension climbs (late, cozy — P5).")]
        [SerializeField] private Color _strainColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("How LATE the whitening arrives (RodLineMath.Whiten01 lateBias, ≥ 1).")]
        [SerializeField, Min(1f)] private float _whitenLateBias = 2.2f;
        [Tooltip("Tension at which the strain shudder starts (RodLineMath.StrainShudder01).")]
        [SerializeField, Range(0f, 1f)] private float _shudderStart01 = 0.6f;
        [SerializeField, Min(0f)] private float _shudderAmpM = 0.05f;
        [SerializeField, Min(0f)] private float _shudderHz = 18f;
        [Tooltip("How far below the rod tip the line meets the water on the straight-down (weighted) " +
                 "path — the entry point the sink ripples ring.")]
        [SerializeField, Min(0f)] private float _entryDropM = 0.35f;

        [Header("Slack pop + rest sag (the tells)")]
        [Tooltip("How far past rest the belly kicks when the line suddenly slackens (0..1 of rest sag).")]
        [SerializeField, Range(0f, 1f)] private float _slackOvershoot01 = 0.5f;
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

        [Header("The deep shadow's circling (visual only — the fight is unchanged)")]
        [SerializeField, Min(0f)] private float _shadowRadiusM = 0.7f;
        [SerializeField, Min(0.1f)] private float _shadowPeriodSeconds = 3.5f;
        [SerializeField, Range(0f, 1f)] private float _shadowSquash = 0.55f;

        [Header("Sorting (world elements; the rod rides the fisher's own order ±1)")]
        [SerializeField] private int _lineSortingOrder = 50;     // the trap rope's precedent
        [SerializeField] private int _waterElementOrder = 49;    // bobber / surfaced fish / held under line
        [SerializeField] private int _shadowSortingOrder = 20;   // the deep shape sits under water detail
        [SerializeField] private int _rippleSortingOrder = 15;
        [SerializeField] private int _splashSortingOrder = 51;

        // ---- runtime ----------------------------------------------------------------------------
        private PlayerFishingAnimator _animator;
        private HiddenHarbours.Core.IsoCharacterSprite _isoSkin;
        private SpriteRenderer _playerSr;

        private SpriteRenderer _rodSr;
        private SpriteRenderer _bobberSr;
        private SpriteRenderer _fishSr;
        private SpriteRenderer _heldSr;
        private SpriteRenderer _splashSr;
        private SpriteRenderer _rippleSr;
        private LineRenderer _line;
        private Vector2[] _lineBuffer;

        private FishingState _s = FishingState.Idle;
        private float _stateClock;        // seconds in the current published phase
        private float _bobberClock;       // the bobber flipbook / dip-loop clock
        private float _slackEdgeClock;    // seconds since SlackWindowOpen turned true (the pop)
        private bool _slackWasOpen;
        private float _shadowTheta;       // the deep shadow's circling angle
        private Vector2 _lastFarEnd;      // where the line last ended (splash anchor for results)
        private Vector2 _lastFishPos;
        private float _fishSpeed;
        private int _fishRow = Directions / 2;
        private string _resolvedFishId;
        private int _speciesIndex = -1;

        private bool _splashActive;
        private float _splashClock;
        private Vector2 _splashPos;

        /// <summary>The species entry drawn for the current hook, or −1. For tests/tooling.</summary>
        public int SpeciesIndex => _speciesIndex;

        private void Awake()
        {
            _animator = GetComponent<PlayerFishingAnimator>();
            _isoSkin = GetComponent<HiddenHarbours.Core.IsoCharacterSprite>();
            _playerSr = GetComponent<SpriteRenderer>();

            _rodSr = MakeChild("RodOverlay", 0);
            _bobberSr = MakeChild("Bobber", _waterElementOrder);
            _fishSr = MakeChild("FishOnTheLine", _waterElementOrder);
            _heldSr = MakeChild("HeldCatch", 0);
            _splashSr = MakeChild("Splash", _splashSortingOrder);
            _rippleSr = MakeChild("SinkRipple", _rippleSortingOrder);
            _line = MakeLine();
            _lineBuffer = new Vector2[Mathf.Max(2, _lineSegments)];
            HideAll();
        }

        private void OnEnable() => EventBus.Subscribe<FishingStateChanged>(OnFishingState);

        private void OnDisable()
        {
            EventBus.Unsubscribe<FishingStateChanged>(OnFishingState);
            HideAll();
            _splashActive = false;
        }

        /// <summary>Public so tests can drive the presenter through the same path the bus uses.</summary>
        public void OnFishingState(FishingStateChanged e)
        {
            FishingState prev = _s;
            FishingState next = e.State;
            _s = next;

            if (next.Phase != prev.Phase)
            {
                _stateClock = 0f;
                if (next.Phase == FishingPhase.Bite || next.Phase == FishingPhase.Cast) _bobberClock = 0f;
                if (next.Phase == FishingPhase.FightDeep) _shadowTheta = 0f;

                // The splash flourishes — surface breaks on the beats that break the surface.
                Vector2 angler = transform.position;
                bool prevCastPath = prev.CastAimX * prev.CastAimX + prev.CastAimY * prev.CastAimY > 1e-4f;
                if (prev.Phase == FishingPhase.Cast && next.Phase == FishingPhase.Waiting)
                    SplashAt(angler + new Vector2(next.CastAimX, next.CastAimY));            // touchdown
                else if (prev.Phase == FishingPhase.Bite && IsHookSet(next.Phase) && prevCastPath)
                    SplashAt(angler + new Vector2(prev.CastAimX, prev.CastAimY));            // pulled under
                else if (prev.Phase == FishingPhase.FightDeep && next.Phase == FishingPhase.FightSurface)
                    SplashAt(angler + new Vector2(next.FishOffsetX, next.FishOffsetY));      // she breaks
                else if (next.Phase == FishingPhase.Landed || next.Phase == FishingPhase.Snapped)
                    SplashAt(_lastFarEnd);                                                   // the result
            }

            // The slack tell's edge — the pop plays from the false→true transition (RodLineMath doc).
            if (next.SlackWindowOpen && !_slackWasOpen) _slackEdgeClock = 0f;
            _slackWasOpen = next.SlackWindowOpen;

            // Species lookup only when the id actually changes (no per-publish string work).
            if (!string.Equals(next.FishId, _resolvedFishId, System.StringComparison.Ordinal))
            {
                _resolvedFishId = next.FishId;
                _speciesIndex = FindSpecies(next.FishId);
            }
        }

        private static bool IsHookSet(FishingPhase p)
            => p == FishingPhase.Fighting || p == FishingPhase.FightDeep || p == FishingPhase.FightSurface;

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _stateClock += dt;
            _bobberClock += dt;
            _slackEdgeClock += dt;
            if (_splashActive) _splashClock += dt;
            if (_s.Phase == FishingPhase.FightDeep)
                _shadowTheta += dt * (2f * Mathf.PI) / Mathf.Max(0.1f, _shadowPeriodSeconds);

            RenderSplash();   // a flourish may outlive the interaction (the result beat) — always ticked

            if (!_s.IsActive)
            {
                HideInteraction();
                return;
            }

            Vector2 angler = transform.position;
            Vector2 castAim = new Vector2(_s.CastAimX, _s.CastAimY);
            bool castPath = castAim.sqrMagnitude > 1e-4f;
            RodElements show = RodPresenterMath.ElementsFor(_s.Phase, castPath);

            bool rodDrawn = RenderRod(show, angler, out Vector2 tip);

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
            if ((show & RodElements.SinkRipples) != 0) RenderRipple(entry);
            else _rippleSr.enabled = false;

            if ((show & RodElements.FishShadow) != 0)
            {
                entry = angler + new Vector2(_s.FishOffsetX, _s.FishOffsetY);
                RenderShadow(entry);
                far = entry;
            }
            else if ((show & RodElements.FishSurface) != 0)
            {
                far = RenderSurfaceFish(angler, dt);
            }
            else
            {
                _fishSr.enabled = false;   // the on-the-line fish never lingers into land/result beats
            }

            if ((show & RodElements.HeldFish) != 0) RenderHeldFish(angler);
            else _heldSr.enabled = false;

            // The weighted path's line runs straight down to the entry point.
            if ((show & RodElements.Line) != 0 && !castPath
                && (_s.Phase == FishingPhase.Waiting || _s.Phase == FishingPhase.Sinking
                    || _s.Phase == FishingPhase.Bite || _s.Phase == FishingPhase.Fighting))
                far = tip + Vector2.down * _entryDropM;

            if ((show & RodElements.Line) != 0 && farValid) RenderLine(tip, far);
            else { _line.enabled = false; _line.positionCount = 0; }

            if (farValid) _lastFarEnd = far;
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

            RodStateVisual state = _rodStates[RodPresenterMath.RodSheetFor(pose)];
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

            _rodSr.transform.position = new Vector3(grip.x, grip.y, 0f);
            _rodSr.sprite = state.Frames[idx];
            _rodSr.sortingLayerID = _playerSr.sortingLayerID;
            _rodSr.sortingOrder = _playerSr.sortingOrder + (IsBehindRow(row) ? -1 : 1);
            _rodSr.enabled = true;
            return true;
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
                FishingPhase.Cast => BobFly,
                FishingPhase.Bite => BobNibble,
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
            if (_s.Phase == FishingPhase.Bite)
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

        private void RenderShadow(Vector2 entry)
        {
            FishSpeciesVisual sp = CurrentSpecies();
            if (sp == null || sp.ShadowFrames == null || sp.ShadowFrames.Length == 0)
            {
                _fishSr.enabled = false;
                return;
            }

            Vector2 pos = entry + RodPresenterMath.ShadowOffset(_shadowTheta, _shadowRadiusM, _shadowSquash);
            float heading = RodPresenterMath.ShadowHeadingDegrees(_shadowTheta, _shadowSquash);
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
        /// line attached at the baked MOUTH anchor. Returns the line's far end (the mouth — or the
        /// fish position when the species has no art, so the line still moves with her).</summary>
        private Vector2 RenderSurfaceFish(Vector2 angler, float dt)
        {
            Vector2 pos = angler + new Vector2(_s.FishOffsetX, _s.FishOffsetY);

            // Her speed picks dart vs thrash; her travel picks the row (held when she pauses).
            if (dt > 1e-5f)
            {
                float instant = (pos - _lastFishPos).magnitude / dt;
                _fishSpeed = Mathf.Lerp(_fishSpeed, instant, 0.35f);   // light smoothing, no state pop
                Vector2 vel = pos - _lastFishPos;
                if (vel.sqrMagnitude > 1e-6f)
                    _fishRow = IsoFacing.HeadingToFacingIndex(
                        RodPresenterMath.HeadingDegrees(vel.x, vel.y), Directions, 0f, false);
            }
            _lastFishPos = pos;

            FishSpeciesVisual sp = CurrentSpecies();
            if (sp == null) { _fishSr.enabled = false; return pos; }

            bool dart = RodPresenterMath.IsDarting(_fishSpeed, _fishDartSpeedMps);
            Sprite[] frames = dart ? sp.DartFrames : sp.ThrashFrames;
            int perDir = dart ? sp.DartFramesPerDir : sp.ThrashFramesPerDir;
            Vector2[] mouths = dart ? sp.DartMouthOffsets : sp.ThrashMouthOffsets;
            if (frames == null || frames.Length == 0) { _fishSr.enabled = false; return pos; }

            int frame = RodPresenterMath.FlipFrame(_stateClock, _fishSecondsPerFrame, perDir);
            int idx = RodPresenterMath.SheetIndex(_fishRow, frame, perDir, frames.Length);
            if (idx < 0 || frames[idx] == null) { _fishSr.enabled = false; return pos; }

            _fishSr.transform.position = new Vector3(pos.x, pos.y, 0f);
            _fishSr.sprite = frames[idx];
            _fishSr.sortingOrder = _waterElementOrder;
            _fishSr.enabled = true;

            Vector2 mouth = pos;
            if (mouths != null && idx < mouths.Length) mouth += mouths[idx];
            return mouth;
        }

        /// <summary>The landed catch in the fisher's hands: the species' held sheet (gill or tail — a
        /// build-time pick from the rig's hold.hands) pinned to the LAND pose's baked hand anchors,
        /// riding the fisher's live row/frame. Skipped whole when the land pose isn't showing.</summary>
        private void RenderHeldFish(Vector2 angler)
        {
            FishSpeciesVisual sp = CurrentSpecies();
            bool landPosed = _animator != null && _animator.OwnsRenderer && _animator.Pose == FishingPose.Land;
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

        private FishSpeciesVisual CurrentSpecies()
            => _species != null && _speciesIndex >= 0 && _speciesIndex < _species.Length
                ? _species[_speciesIndex] : null;

        private int FindSpecies(string fishId)
        {
            if (string.IsNullOrEmpty(fishId) || _species == null) return -1;
            for (int i = 0; i < _species.Length; i++)
                if (_species[i] != null && string.Equals(_species[i].FishId, fishId, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        // ---- the line ------------------------------------------------------------------------------

        private void RenderLine(Vector2 tip, Vector2 far)
        {
            if (_line == null) return;

            float taut = RodPresenterMath.TautFor(_s.Phase, _s.SlackWindowOpen, _s.RodBend01,
                                                  _s.Tension01, _restTaut, _sinkTaut, _fightTautFloor);

            // The hit-bottom / gone-slack POP: the belly kicks past rest and settles (RodLineMath).
            float sag = _maxSagM;
            if (_s.SlackWindowOpen)
                sag *= RodLineMath.SlackOvershoot(_slackEdgeClock, _slackOvershoot01, _slackSettleSeconds);

            RodLineMath.SampleLine(tip, far, taut, sag, _lineBuffer);

            // Strain: whiten late, shudder near the snap (one visual language with the haul rope).
            bool fight = _s.IsFightPhase;
            float whiten = fight ? RodLineMath.Whiten01(_s.Tension01, _whitenLateBias) : 0f;
            float shudderAmp = fight
                ? _shudderAmpM * RodLineMath.StrainShudder01(_s.Tension01, _shudderStart01) : 0f;

            Color c = Color.Lerp(_lineColor, _strainColor, whiten);
            _line.startColor = c;
            _line.endColor = c;

            int n = _lineBuffer.Length;
            Vector2 span = far - tip;
            float len = span.magnitude;
            Vector2 perp = len > 1e-4f ? new Vector2(-span.y, span.x) / len : Vector2.up;
            float phase = Time.time * _shudderHz * (2f * Mathf.PI);

            if (!_line.enabled) _line.enabled = true;
            _line.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = _lineBuffer[i];
                if (shudderAmp > 1e-5f)
                    p += perp * RodLineMath.ShudderOffset(i, n, phase, shudderAmp);
                _line.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
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
            if (_splashFrames == null || _splashFrames.Length == 0) return;
            _splashActive = true;
            _splashClock = 0f;
            _splashPos = pos;
        }

        private void RenderSplash()
        {
            if (!_splashActive) { _splashSr.enabled = false; return; }
            int frame = RodPresenterMath.OnceFlipFrame(_splashClock, 1f / Mathf.Max(0.1f, _splashFps),
                                                       _splashFrames.Length);
            if (frame >= _splashFrames.Length || _splashFrames[frame] == null)
            {
                _splashActive = false;
                _splashSr.enabled = false;
                return;
            }
            _splashSr.transform.position = new Vector3(_splashPos.x, _splashPos.y, 0f);
            _splashSr.sprite = _splashFrames[frame];
            _splashSr.enabled = true;
        }

        // ---- plumbing --------------------------------------------------------------------------------

        private void HideInteraction()
        {
            if (_rodSr != null) _rodSr.enabled = false;
            if (_bobberSr != null) _bobberSr.enabled = false;
            if (_fishSr != null) _fishSr.enabled = false;
            if (_heldSr != null) _heldSr.enabled = false;
            if (_rippleSr != null) _rippleSr.enabled = false;
            if (_line != null) { _line.enabled = false; _line.positionCount = 0; }
        }

        private void HideAll()
        {
            HideInteraction();
            if (_splashSr != null) _splashSr.enabled = false;
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

        private LineRenderer MakeLine()
        {
            var go = new GameObject("FishingLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.startWidth = _lineWidth;
            lr.endWidth = _lineWidth;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) lr.material = new Material(shader);   // null headless → skip (rope precedent)
            lr.sortingOrder = _lineSortingOrder;
            lr.positionCount = 0;
            lr.enabled = false;
            return lr;
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
        }
    }
}
