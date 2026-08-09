using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Plays one whole-body CLIP on a character</b> — the small seam the pass-6.2 kit's event clips
    /// were missing. <see cref="IsoCharacterSprite"/> picks idle/walk/run from measured speed and a
    /// stance from context; neither can express "climb over this rail now", which is why the rig's
    /// <c>stagger</c> clip has been baked-but-unreachable since it landed. This is the path that reaches
    /// it, and the three clips the 6.2 drop adds (<c>board</c>, <c>boardDown</c>, <c>haul</c>,
    /// <c>ladderDown</c>) arrive through it too.
    ///
    /// <para><b>It reuses the hand-off that already exists</b> rather than inventing a second one:
    /// <see cref="IsoCharacterSprite.Suspend"/> / <see cref="IsoCharacterSprite.Release"/>, the counted
    /// claim <c>PlayerHaulAnimator</c> has used since the deck haul landed. While a clip runs the iso
    /// driver writes nothing at all, so the two can never fight over <c>SpriteRenderer.sprite</c>; when
    /// the clip stops, the renderer goes back exactly as it was found and the iso driver re-asserts its
    /// own cell on the next frame.</para>
    ///
    /// <para><b>Data, not paths</b> (rule 2). Every fact about a clip — frame count, rate, whether it
    /// loops, how many directions it was baked at — is read from the
    /// <see cref="CharacterVisualDef"/>, which by default is simply the one the iso skin beside it is
    /// already wearing. Nothing here names a sheet, a folder or a frame index.</para>
    ///
    /// <para><b>Facings are never assumed to be eight.</b> The row snap goes through
    /// <see cref="CharacterVisualDef.ClipFacingRowFor"/>, which resolves against the CLIP's own
    /// <see cref="CharacterClipSheets.FacingCount"/>. The pass-6.2 kit bakes all four clips at the full
    /// eight, but a later kit that ships a two-facing ladder climb drops in with no code change.</para>
    ///
    /// <para><b>Two ways to pace a clip, and only one of them is a clock.</b> <see cref="Play"/> runs a
    /// clip off <c>Time.deltaTime</c> — the right answer for a move with a duration, like the boarding
    /// vault. <see cref="PlayDriven"/> hands the pacing to the CALLER instead, for a presentation whose
    /// pace is a quantity: the deck haul's heave is keyed to the LINE hauled, so the hands stop when the
    /// rope stops and a brisk take runs them fast. A driven clip is never advanced by
    /// <see cref="LateUpdate"/>; it moves only when <see cref="SeekFrame"/> is called, which is also what
    /// keeps a snapshot-driven presenter free of per-frame work.</para>
    ///
    /// <para><b>Budget (rule 7).</b> No allocation, no <c>GetComponent</c> after the first play, and the
    /// sprite assignment is skipped when the cell has not changed. Inert — and costing one early-out —
    /// whenever no clip is running.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterClipPlayer : MonoBehaviour
    {
        [Tooltip("The skin whose clips this plays. Leave EMPTY and it uses the one the IsoCharacterSprite " +
                 "beside it is wearing, which is what the player wants — one skin, wired once.")]
        [SerializeField] private CharacterVisualDef _visual;

        private SpriteRenderer _renderer;
        private IsoCharacterSprite _isoSkin;
        private bool _resolved;

        private CharacterClip _clip = CharacterClip.None;
        private float _elapsed;
        private float _scaledDuration;
        private bool _holdOnFinish;
        private bool _driven;             // the CALLER paces this clip; the clock does not touch it
        private float _framePosition;     // where along the clip a driven caller has put it, in frames
        private float _headingDegrees;
        private int _facingRow;
        private int _frame;

        private Sprite _restoreSprite;
        private bool _restoreFlipX;
        private Sprite _lastApplied;

        /// <summary>The clip running right now, or <see cref="CharacterClip.None"/>.</summary>
        public CharacterClip Clip => _clip;

        /// <summary>True while a clip owns the renderer.</summary>
        public bool IsPlaying => _clip != CharacterClip.None;

        /// <summary>The frame of the running clip currently on screen. For tests / tooling.</summary>
        public int Frame => _frame;

        /// <summary>The direction ROW of the running clip currently on screen. For tests / tooling.</summary>
        public int FacingRow => _facingRow;

        /// <summary>How far into the running clip, in seconds. For tests / tooling.</summary>
        public float Elapsed => _elapsed;

        /// <summary>True while the running clip is paced by its CALLER rather than by the clock
        /// (see <see cref="PlayDriven"/>). False when nothing is playing.</summary>
        public bool IsDriven => _driven && _clip != CharacterClip.None;

        /// <summary>Where along a DRIVEN clip the caller has put it, in frames. For tests / tooling.</summary>
        public float FramePosition => _framePosition;

        /// <summary>True when a running ONE-SHOT has played out (always false for a looping clip, and
        /// for no clip at all). A held one-shot stays <see cref="IsPlaying"/> while this is true.</summary>
        public bool IsFinished
        {
            get
            {
                var def = ResolvedVisual();
                if (_clip == CharacterClip.None || def == null) return false;
                // A DRIVEN clip has no clock to be finished against — its progress IS the caller's
                // frame position, so a one-shot is done once that has walked past the last frame.
                if (_driven)
                    return !def.ClipLoops(_clip) && _framePosition >= def.ClipFrameCount(_clip);
                return CharacterClipMath.IsFinished(_elapsed, _scaledDuration,
                                                    def.ClipFrameCount(_clip),
                                                    def.ClipFramesPerSecond(_clip),
                                                    def.ClipLoops(_clip));
            }
        }

        /// <summary>Wire the skin explicitly (tests / editor tooling). Null hands it back to the iso
        /// skin's own def, which is the default.</summary>
        public void Configure(CharacterVisualDef visual) => _visual = visual;

        /// <summary>The def this player is actually reading — the serialized one, else the iso skin's.
        /// Null when neither exists, in which case no clip can play.</summary>
        public CharacterVisualDef ResolvedVisual()
        {
            if (_visual != null) return _visual;
            Resolve();
            return _isoSkin != null ? _isoSkin.Visual : null;
        }

        /// <summary>True when this clip has COMPLETE art on the resolved skin — i.e. asking to play it
        /// would actually show something. The gate a caller reads before committing to a presentation.</summary>
        public bool CanPlay(CharacterClip clip)
        {
            var def = ResolvedVisual();
            return clip != CharacterClip.None && def != null && def.HasClip(clip);
        }

        /// <summary>
        /// Start a clip. Returns FALSE — and changes nothing at all — when the clip has no complete art,
        /// so a caller can fall back to whatever it did before without a second availability check.
        ///
        /// <para><paramref name="scaleToSeconds"/> above zero stretches or compresses the WHOLE clip to
        /// span exactly that long: pass the move's own duration and the art fits the move rather than
        /// the move being retuned to fit the art. Zero plays it at its baked rate.</para>
        ///
        /// <para><paramref name="holdOnFinish"/> keeps a one-shot's last frame on screen after it has
        /// played out, until <see cref="Stop"/>. Callers that own the timing (a move that ends on its
        /// own clock) want this: without it a clip that rounds a hair short hands the renderer back for
        /// one frame, which reads as a flicker. Fire-and-forget callers leave it false and the clip
        /// cleans itself up.</para>
        /// </summary>
        public bool Play(CharacterClip clip, float headingDegrees, float scaleToSeconds = 0f,
                         bool holdOnFinish = false)
        {
            if (!Begin(clip, headingDegrees)) return false;

            _driven = false;
            _framePosition = 0f;
            _scaledDuration = Mathf.Max(0f, scaleToSeconds);
            _holdOnFinish = holdOnFinish;
            Apply();
            return true;
        }

        /// <summary>
        /// Start a clip whose pace the CALLER owns — the clock never touches it. Returns FALSE, and
        /// changes nothing, when the clip has no complete art, exactly like <see cref="Play"/>.
        ///
        /// <para>This is the answer for a presentation paced by a QUANTITY rather than by time. The deck
        /// haul is the case it was built for: its heave is keyed to the LINE hauled, so the hands move
        /// with the rope — they stop dead when the line stops and a brisk take on a big lift runs them
        /// fast — and a timer would drift off the rope inside one pot. Drive it with
        /// <see cref="SeekFrame"/>; nothing else will move it.</para>
        ///
        /// <para>Call it ONCE per presentation and seek thereafter. Calling it again restarts the clip at
        /// <paramref name="framePosition"/>, so a caller that re-plays every tick freezes its own clip.</para>
        /// </summary>
        public bool PlayDriven(CharacterClip clip, float headingDegrees, float framePosition = 0f)
        {
            if (!Begin(clip, headingDegrees)) return false;

            _driven = true;
            _framePosition = framePosition;
            _scaledDuration = 0f;
            _holdOnFinish = true;      // a driven clip is never taken off the renderer by the clock
            Apply();
            return true;
        }

        /// <summary>
        /// Move a DRIVEN clip to <paramref name="framePosition"/> frames along itself and push the cell.
        /// A looping clip wraps, so the position may run past the end for as many cycles as the caller's
        /// quantity covers; a one-shot clamps to its last frame. Harmless (and does nothing) when no clip
        /// is running or when the running one is paced by the clock.
        ///
        /// <para>A driven clip re-resolves its facing row here too, so a caller that is turning should
        /// call <see cref="SetHeading"/> before it seeks.</para>
        /// </summary>
        public void SeekFrame(float framePosition)
        {
            if (!_driven || _clip == CharacterClip.None) return;
            _framePosition = framePosition;
            Apply();
        }

        /// <summary>The take-over both play paths share: check the art, resolve the renderer, and claim
        /// it from the iso driver on the FIRST clip only. Leaves the pacing fields to the caller.</summary>
        private bool Begin(CharacterClip clip, float headingDegrees)
        {
            if (!CanPlay(clip)) return false;

            Resolve();
            if (_renderer == null) return false;

            if (_clip == CharacterClip.None)
            {
                // Take the renderer over: remember what was on it, and tell the iso driver to stand down
                // so it stops writing cells underneath us. Counted, so nesting with another claimant is safe.
                _restoreSprite = _renderer.sprite;
                _restoreFlipX = _renderer.flipX;
                if (_isoSkin != null) _isoSkin.Suspend();
            }

            _clip = clip;
            _elapsed = 0f;
            _headingDegrees = headingDegrees;
            _lastApplied = null;
            return true;
        }

        /// <summary>Re-aim a running clip. Cheap and idempotent; call it every tick with the live heading
        /// so a clip played on a turning deck keeps facing the way the character really is.</summary>
        public void SetHeading(float headingDegrees) => _headingDegrees = headingDegrees;

        /// <summary>
        /// Advance the running clip by <paramref name="deltaTime"/> and push the cell. Called for you
        /// every frame by <see cref="LateUpdate"/>; public so a test can drive it in SECONDS, because a
        /// frame count is not time and a clip scaled to a move must be pinned against the clock.
        /// Harmless when nothing is playing.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (_clip == CharacterClip.None) return;
            // A DRIVEN clip is paced by its caller. The clock must not touch it, or the haul's heave
            // would run on its own while the rope stands still.
            if (_driven) return;
            _elapsed += Mathf.Max(0f, deltaTime);

            if (!_holdOnFinish && IsFinished) { Stop(); return; }
            Apply();
        }

        /// <summary>Hand the renderer back (idempotent). The iso driver re-asserts its own cell on the
        /// next frame, and the sprite found on arrival is restored — so a character with no iso skin
        /// still ends exactly where it began.</summary>
        public void Stop()
        {
            if (_clip == CharacterClip.None) return;
            _clip = CharacterClip.None;
            _elapsed = 0f;
            _scaledDuration = 0f;
            _holdOnFinish = false;
            _driven = false;
            _framePosition = 0f;
            _frame = 0;
            _lastApplied = null;

            if (_isoSkin != null) _isoSkin.Release();
            if (_renderer == null) return;
            _renderer.sprite = _restoreSprite;
            _renderer.flipX = _restoreFlipX;
            _restoreSprite = null;
        }

        private void Reset() => Resolve();

        private void Awake() => Resolve();

        // Disabling mid-clip hands the renderer back — never a stuck boarding frame on a fisher who is
        // standing on the wharf again.
        private void OnDisable() => Stop();

        // LateUpdate for the same reason IsoCharacterSprite uses it: everything that could move the
        // character this frame has already run, and the iso driver's own LateUpdate is a no-op while
        // suspended, so the two orders cannot disagree about who wrote last.
        private void LateUpdate() => Advance(Time.deltaTime);

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _renderer = GetComponent<SpriteRenderer>();
            _isoSkin = GetComponent<IsoCharacterSprite>();
        }

        /// <summary>Resolve the row + frame and push the cell (only when it changed).</summary>
        private void Apply()
        {
            var def = ResolvedVisual();
            if (_renderer == null || def == null || _clip == CharacterClip.None) return;

            _facingRow = def.ClipFacingRowFor(_clip, _headingDegrees);
            _frame = _driven
                ? CharacterClipMath.FrameAt(_framePosition, def.ClipFrameCount(_clip), def.ClipLoops(_clip))
                : CharacterClipMath.FrameFor(_elapsed, _scaledDuration,
                                             def.ClipFrameCount(_clip),
                                             def.ClipFramesPerSecond(_clip),
                                             def.ClipLoops(_clip));

            Sprite cell = def.ClipSpriteFor(_clip, _facingRow, _frame);
            if (cell == null || ReferenceEquals(cell, _lastApplied)) return;

            // Every direction is DRAWN — nothing here is a mirror, so any flip a previous 4-way mirrored
            // sheet left on the renderer has to be cleared or every westward facing is inverted.
            _renderer.flipX = false;
            _renderer.sprite = cell;
            _lastApplied = cell;
        }
    }
}
