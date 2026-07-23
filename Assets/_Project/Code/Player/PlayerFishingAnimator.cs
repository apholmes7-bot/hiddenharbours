using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>What the fishing fisher's body is DOING this beat — which of the baked fight sheets
    /// owns the renderer. Derived purely from the published <see cref="FishingState"/> (see
    /// <see cref="PlayerFishingAnimMath"/>).</summary>
    public enum FishingPose
    {
        /// <summary>No pose — the normal iso walk skin owns the renderer.</summary>
        None = 0,
        /// <summary>A bite is on (the tell beat) — the rod-tip two-tap sheet loops.</summary>
        Bite = 1,
        /// <summary>The hook was just set — the strike sheet plays ONCE as the fight opens.</summary>
        Strike = 2,
        /// <summary>The fight proper (deep or surface, or the legacy fight) — the reel cycle loops.</summary>
        Reel = 3,
        /// <summary>She's aboard — the land sheet plays once and holds its last frame.</summary>
        Land = 4,
    }

    /// <summary>
    /// The PURE maths behind the rod-fight animation — published fishing state → pose → sheet frame →
    /// facing row. Split out (the <see cref="PlayerHaulAnimMath"/> pattern) so the whole state→frame
    /// mapping is EditMode-testable headless. No engine state, no <c>Time</c>, no RNG.
    /// </summary>
    public static class PlayerFishingAnimMath
    {
        /// <summary>
        /// The pose for a published phase: <see cref="FishingPhase.Bite"/> plays the bite tell; any rod
        /// FIGHT phase (legacy <see cref="FishingPhase.Fighting"/> or the v2
        /// <see cref="FishingPhase.FightDeep"/>/<see cref="FishingPhase.FightSurface"/> arc) opens with
        /// the STRIKE (its first <paramref name="strikeSeconds"/>) then settles into the REEL loop;
        /// <see cref="FishingPhase.Landed"/> plays the land beat. <see cref="FishingPhase.Tending"/> is
        /// deliberately None — the hand-gather has no rod to strike or reel. Everything else hands the
        /// renderer back.
        /// </summary>
        /// <param name="phase">The published phase.</param>
        /// <param name="secondsInFight">Seconds since a fight phase was entered (the caller's clock).</param>
        /// <param name="strikeSeconds">How long the strike beat owns the opening of the fight (s).</param>
        public static FishingPose PoseFor(FishingPhase phase, float secondsInFight, float strikeSeconds)
        {
            switch (phase)
            {
                case FishingPhase.Bite:
                    return FishingPose.Bite;
                case FishingPhase.Fighting:
                case FishingPhase.FightDeep:
                case FishingPhase.FightSurface:
                    return secondsInFight < Mathf.Max(0f, strikeSeconds) ? FishingPose.Strike : FishingPose.Reel;
                case FishingPhase.Landed:
                    return FishingPose.Land;
                default:
                    return FishingPose.None;
            }
        }

        /// <summary>The looping frame at <paramref name="elapsed"/> seconds into a cycle sheet.
        /// Negative-safe; count ≤ 0 returns 0.</summary>
        public static int LoopFrame(float elapsed, float fps, int frameCount)
        {
            if (frameCount <= 0) return 0;
            float phase = Mathf.Max(0f, elapsed) * Mathf.Max(0f, fps);
            return Mathf.FloorToInt(phase) % frameCount;
        }

        /// <summary>The play-once frame (strike/land): advances at <paramref name="fps"/> then HOLDS the
        /// last frame. Negative-safe; count ≤ 0 returns 0.</summary>
        public static int OnceFrame(float elapsed, float fps, int frameCount)
        {
            if (frameCount <= 0) return 0;
            float phase = Mathf.Max(0f, elapsed) * Mathf.Max(0f, fps);
            return Mathf.Min(Mathf.FloorToInt(phase), frameCount - 1);
        }

        /// <summary>
        /// The direction ROW that faces the line's far end: the published
        /// <c>FishingState.FishOffsetX/Y</c> (angler → fish/entry point) turned into a compass heading
        /// (the <see cref="IsoCharacterMath.HeadingFor"/> convention — 0 = North, CW) and bucketed by
        /// the shared <see cref="IsoFacing.HeadingToFacingIndex"/>. An offset shorter than
        /// <paramref name="minOffset"/> (deep fight straight under the boat, or a phase publishing
        /// neutral) HOLDS <paramref name="fallbackRow"/> — the fisher keeps facing where the action
        /// was, never snapping to North.
        /// </summary>
        public static int FacingRowFor(float fishOffsetX, float fishOffsetY, float minOffset,
                                       int rowCount, bool rowsAreCounterClockwise, int fallbackRow)
        {
            float x = float.IsNaN(fishOffsetX) ? 0f : fishOffsetX;
            float y = float.IsNaN(fishOffsetY) ? 0f : fishOffsetY;
            float min = Mathf.Max(0f, minOffset);
            if (x * x + y * y < min * min) return fallbackRow;
            float heading = Mathf.Atan2(x, y) * Mathf.Rad2Deg;   // 0 = +Y (North), CW toward +X (East)
            return IsoFacing.HeadingToFacingIndex(heading, rowCount, 0f, rowsAreCounterClockwise);
        }
    }

    /// <summary>
    /// Plays the baked FIGHT sheets (<c>Fisher_bite / Fisher_strike / Fisher_reel / Fisher_land</c> —
    /// the Rod Fishing v2 character kit, 8 directions × N frames) on the fisher while a rod interaction
    /// is live: the two-tap BITE tell, the STRIKE as the hook is set, the REEL cycle through the fight
    /// (deep, surface, or the legacy fight), and the LAND beat over the result — then hands the
    /// renderer back to the iso walk skin exactly as it was.
    ///
    /// <para><b>Cross-module via Core only (rule 4)</b> — the <see cref="PlayerHaulAnimator"/> twin: it
    /// never references Fishing. Everything it needs arrives in the <see cref="FishingStateChanged"/>
    /// snapshots (phase + the Wave-3 <c>FishOffsetX/Y</c> line-far-end read, which also picks the
    /// direction row so the fisher faces the fish she's fighting). The pose/frame/row mapping is the
    /// pure <see cref="PlayerFishingAnimMath"/>.</para>
    ///
    /// <para><b>A light Update, only while posed.</b> Unlike the haul (whose cycle keys to the line
    /// hauled), the bite/strike/land beats are TIME-shaped and the state machine doesn't publish during
    /// them (transitions only) — so this component advances a local clock in <c>Update</c>. It is
    /// gated: while no pose owns the renderer the update does nothing but a phase check (rule 7), and
    /// event handling does the rest.</para>
    ///
    /// <para><b>Frames.</b> Four sheets in <c>direction·framesPerDirection + frame</c> order (the
    /// CharacterVisualLibraryBuilder d/f order), wired by the start builder from the sliced
    /// <c>Fisher_*.png</c>. A sheet whose count isn't a clean 8 rows is dropped whole; missing art →
    /// the component is inert (null-safe greybox rule). Frame counts per state are READ from the
    /// arrays, never restated.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerFishingAnimator : MonoBehaviour
    {
        /// <summary>The bake's direction-row count — the character iso kit contract (8 compass rows).</summary>
        public const int Directions = 8;

        // Below this published offset (m) the far end is "under the boat" and the facing row holds —
        // a read threshold, not a feel dial.
        private const float MinFacingOffsetM = 0.05f;

        [Header("The baked fight sheets (8 dir × N frames, d/f order — wired by the start builder)")]
        [Tooltip("Fisher_bite: the rod-tip two-tap tell. Loops for the whole bite window.")]
        [SerializeField] private Sprite[] _biteFrames;
        [Tooltip("Fisher_strike: the hook-set. Plays ONCE as the fight opens, then the reel takes over.")]
        [SerializeField] private Sprite[] _strikeFrames;
        [Tooltip("Fisher_reel: the fight cycle. Loops through FightDeep/FightSurface/legacy Fighting.")]
        [SerializeField] private Sprite[] _reelFrames;
        [Tooltip("Fisher_land: she's aboard. Plays once over the Landed beat and holds its last frame.")]
        [SerializeField] private Sprite[] _landFrames;

        [Header("Playback (owner-tunable feel, rule 6)")]
        [Tooltip("Bite tell playback (fps) — the two-tap cadence of the baked sheet.")]
        [SerializeField, Min(0.1f)] private float _biteFps = 8f;
        [Tooltip("Strike playback (fps). The strike owns the fight's opening for frames/fps seconds.")]
        [SerializeField, Min(0.1f)] private float _strikeFps = 12f;
        [Tooltip("Reel cycle playback (fps).")]
        [SerializeField, Min(0.1f)] private float _reelFps = 10f;
        [Tooltip("Land beat playback (fps).")]
        [SerializeField, Min(0.1f)] private float _landFps = 10f;
        [Tooltip("Which way this artwork's direction rows run. The character rig bakes CLOCKWISE as " +
                 "labelled (the corrected rig — unlike the boat kits), so false. Per-artwork DATA, " +
                 "never assumed.")]
        [SerializeField] private bool _rowsAreCounterClockwise = false;

        private SpriteRenderer _renderer;
        private HiddenHarbours.Core.IsoCharacterSprite _isoSkin;
        private bool _isoSkinResolved;

        private bool _active;             // a pose owns the renderer right now
        private Sprite _restoreSprite;    // hand-back state (the PlayerHaulAnimator dance)
        private bool _restoreFlipX;

        private FishingPhase _phase = FishingPhase.Idle;
        private float _poseElapsed;       // seconds in the current pose (drives the frame)
        private float _fightElapsed;      // seconds since a fight phase was entered (strike → reel)
        private int _row;                 // current direction row (held when the offset vanishes)
        private FishingPose _pose = FishingPose.None;

        /// <summary>The pose currently shown (None when the walk skin owns the renderer). For tests.</summary>
        public FishingPose Pose => _pose;

        /// <summary>The direction row currently shown. For tests/tooling.</summary>
        public int Row => _row;

        private void OnEnable()
        {
            _row = Directions / 2;   // South — facing the camera until the state says otherwise
            EventBus.Subscribe<FishingStateChanged>(OnFishingStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FishingStateChanged>(OnFishingStateChanged);
            EndPose();   // disabling mid-fight hands the renderer back (never a stuck frame)
        }

        /// <summary>Public so tests can drive the mapping through the same path the bus uses.</summary>
        public void OnFishingStateChanged(FishingStateChanged e)
        {
            bool wasFight = IsFight(_phase);
            _phase = e.State.Phase;
            if (IsFight(_phase) && !wasFight) _fightElapsed = 0f;   // the strike beat starts here

            _row = PlayerFishingAnimMath.FacingRowFor(e.State.FishOffsetX, e.State.FishOffsetY,
                                                      MinFacingOffsetM, Directions,
                                                      _rowsAreCounterClockwise, _row);
            Apply();
        }

        private void Update()
        {
            if (_pose == FishingPose.None && !IsPosed(_phase)) return;   // idle: nothing to advance (rule 7)
            float dt = Time.deltaTime;
            _poseElapsed += dt;
            if (IsFight(_phase)) _fightElapsed += dt;
            Apply();
        }

        private static bool IsFight(FishingPhase p)
            => p == FishingPhase.Fighting || p == FishingPhase.FightDeep || p == FishingPhase.FightSurface;

        private static bool IsPosed(FishingPhase p)
            => p == FishingPhase.Bite || p == FishingPhase.Landed || IsFight(p);

        /// <summary>Resolve pose + frame from the cached phase and local clocks, and push the cell.</summary>
        private void Apply()
        {
            float strikeSeconds = FrameCount(_strikeFrames) / Mathf.Max(0.1f, _strikeFps);
            FishingPose pose = PlayerFishingAnimMath.PoseFor(_phase, _fightElapsed, strikeSeconds);

            // A pose only counts if its sheet is actually wired — otherwise fall through to None so a
            // partial art drop degrades to the walk skin, never to a missing sprite.
            Sprite[] frames = FramesFor(pose);
            if (pose != FishingPose.None && frames == null) pose = FishingPose.None;

            if (pose != _pose)
            {
                if (pose == FishingPose.None) { EndPose(); return; }
                if (!_active) BeginOwnership();
                _pose = pose;
                _poseElapsed = 0f;
            }
            if (_pose == FishingPose.None) return;

            frames = FramesFor(_pose);
            int perDir = frames.Length / Directions;
            bool loops = _pose == FishingPose.Bite || _pose == FishingPose.Reel;
            float fps = FpsFor(_pose);
            int frame = loops
                ? PlayerFishingAnimMath.LoopFrame(_poseElapsed, fps, perDir)
                : PlayerFishingAnimMath.OnceFrame(_poseElapsed, fps, perDir);

            int idx = _row * perDir + frame;
            if (idx < 0 || idx >= frames.Length || frames[idx] == null) return;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;
            _renderer.flipX = false;   // all 8 rows are drawn — never mirrored
            _renderer.sprite = frames[idx];
        }

        private void BeginOwnership()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;
            _restoreSprite = _renderer.sprite;
            _restoreFlipX = _renderer.flipX;
            _active = true;
            ResolveIsoSkin();
            if (_isoSkin != null) _isoSkin.Suspend();
        }

        /// <summary>Hand the renderer back to the walk skin (idempotent — safe on disable/repeats).</summary>
        private void EndPose()
        {
            _pose = FishingPose.None;
            _poseElapsed = 0f;
            if (!_active) return;
            _active = false;
            if (_isoSkin != null) _isoSkin.Release();
            if (_renderer == null) return;
            _renderer.sprite = _restoreSprite;
            _renderer.flipX = _restoreFlipX;
            _restoreSprite = null;
        }

        private Sprite[] FramesFor(FishingPose pose) => pose switch
        {
            FishingPose.Bite => ValidSheet(_biteFrames),
            FishingPose.Strike => ValidSheet(_strikeFrames),
            FishingPose.Reel => ValidSheet(_reelFrames),
            FishingPose.Land => ValidSheet(_landFrames),
            _ => null,
        };

        private float FpsFor(FishingPose pose) => pose switch
        {
            FishingPose.Bite => _biteFps,
            FishingPose.Strike => _strikeFps,
            FishingPose.Reel => _reelFps,
            FishingPose.Land => _landFps,
            _ => 0f,
        };

        /// <summary>A sheet is usable only as complete 8-direction rows (the all-or-nothing gate the
        /// visual-def builder applies) — anything else is treated as absent.</summary>
        private static Sprite[] ValidSheet(Sprite[] frames)
            => frames != null && frames.Length > 0 && frames.Length % Directions == 0 ? frames : null;

        private static int FrameCount(Sprite[] frames)
        {
            Sprite[] valid = ValidSheet(frames);
            return valid == null ? 0 : valid.Length / Directions;
        }

        private void ResolveIsoSkin()
        {
            if (_isoSkinResolved) return;
            _isoSkinResolved = true;
            _isoSkin = GetComponent<HiddenHarbours.Core.IsoCharacterSprite>();
        }

        /// <summary>Wire the animator in one call (tests / editor builder): the four sliced fight
        /// sheets in d/f order. Negative fps values leave the serialized defaults.</summary>
        public void Configure(Sprite[] bite, Sprite[] strike, Sprite[] reel, Sprite[] land,
                              float biteFps = -1f, float strikeFps = -1f, float reelFps = -1f,
                              float landFps = -1f)
        {
            _biteFrames = bite;
            _strikeFrames = strike;
            _reelFrames = reel;
            _landFrames = land;
            if (biteFps > 0f) _biteFps = biteFps;
            if (strikeFps > 0f) _strikeFps = strikeFps;
            if (reelFps > 0f) _reelFps = reelFps;
            if (landFps > 0f) _landFps = landFps;
        }
    }
}
