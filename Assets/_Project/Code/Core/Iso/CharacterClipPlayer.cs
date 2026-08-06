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

        /// <summary>True when a running ONE-SHOT has played out (always false for a looping clip, and
        /// for no clip at all). A held one-shot stays <see cref="IsPlaying"/> while this is true.</summary>
        public bool IsFinished
        {
            get
            {
                var def = ResolvedVisual();
                if (_clip == CharacterClip.None || def == null) return false;
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
            _scaledDuration = Mathf.Max(0f, scaleToSeconds);
            _holdOnFinish = holdOnFinish;
            _headingDegrees = headingDegrees;
            _lastApplied = null;
            Apply();
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
            _frame = CharacterClipMath.FrameFor(_elapsed, _scaledDuration,
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
