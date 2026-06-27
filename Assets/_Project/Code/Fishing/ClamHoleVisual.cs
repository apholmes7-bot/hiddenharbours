using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The on-flats <b>look</b> of a clam hole (St Peters opening): the sprite a player walks up to. It is the
    /// visible half of a dig spot — <see cref="ClamDig"/> owns the gate logic (exposed? shovel? bucket room?),
    /// the once-only yield, and the cosmetic squirt cadence; this drives the picture off it so the hole is
    /// <b>only drawn while its ground is bared by the falling tide</b>. When the flood covers the ground the
    /// hole vanishes — the same single tide number that gates the dig also decides whether you can see it, so
    /// the picture can never disagree with the gate (the one-height-map discipline ADR 0009/0010 enforce —
    /// render and gameplay read the same deterministic <c>WaterLevelAt</c> + <c>ITidalTerrain.ElevationAt</c>).
    ///
    /// <para><b>Two renderers, never a sprite-swap (squirt = OVERLAY).</b> The two-holes sprite lives on the
    /// <em>base</em> renderer and stays visible the whole time the spot is exposed. The "two squirting holes"
    /// tell is an <em>added effect on a SEPARATE overlay renderer</em> drawn one order on top — it appears and
    /// disappears with <see cref="ClamDig.ShowingSquirt"/> but never replaces the holes underneath. (The old
    /// build swapped the single renderer's sprite, so the holes disappeared whenever the squirt played; you
    /// now always see the holes when exposed, with the squirt layered over them.) The base renderer is the one
    /// already on this GameObject (placed by the builder); the overlay is a child this component creates and
    /// owns. Submerged → both hidden.</para>
    ///
    /// <para><b>Skittish clams (proximity escape).</b> A clam is shy: if the on-foot player loiters within
    /// <see cref="_escapeRadius"/> of an exposed, un-spent hole for <see cref="_escapeSeconds"/> (owner-editable,
    /// default 4&#160;s), the clam burrows away — the holes play a brief <b>sink-into-the-sand</b> animation
    /// (fade + shrink over <see cref="_sinkSeconds"/>), then the hole is spent (<see cref="ClamDig.MarkConsumed"/>)
    /// and hidden, leaving nothing to dig. So you must approach and dig PROMPTLY, not stand around. The linger
    /// timer resets the instant the player steps back outside the radius, so a passer-by doesn't spook it. The
    /// player position comes from the in-lane <see cref="ClamDigger.TryGetPlayerPosition"/> beacon (no Player
    /// reference). This is a <b>real-time cosmetic reaction</b> like the squirt cue — it touches no sim/save
    /// state, so determinism is unaffected (rule 5).</para>
    ///
    /// <para><b>Once-spent → gone.</b> Whether the hole was dug (it yielded) or escaped (it burrowed away),
    /// <see cref="ClamDig.Consumed"/> goes true; a dug hole vanishes promptly, an escaped one after its sink
    /// animation. Either way the spent hole disappears for the rest of the play-session. A reload rebuilds the
    /// deterministic hole field afresh (the spent state isn't saved), so nothing here touches determinism.</para>
    ///
    /// <para><b>Cheap &amp; allocation-free.</b> Two <see cref="SpriteRenderer"/>s, a throttled tick for the
    /// show/hide + frame step, and an index into a cached frame array — no per-frame GC (rule 7). The escape
    /// timer runs each frame on an exposed, un-spent hole (a cheap distance compare); it idles otherwise.</para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ClamHoleVisual : MonoBehaviour
    {
        [Tooltip("The dig this hole belongs to — read for tide exposure (IsExposedNow), the squirt tell " +
                 "(ShowingSquirt) and the spent state (Consumed). Defaults to a ClamDig on this GameObject.")]
        [SerializeField] private ClamDig _dig;

        [Tooltip("The still two-holes sprite, shown on the BASE renderer the whole time the ground is bared.")]
        [SerializeField] private Sprite _holeSprite;

        [Tooltip("The 'two squirting holes' flip-book frames, cycled on a SEPARATE overlay renderer ON TOP of " +
                 "the holes while the dig's squirt tell shows. Empty → no overlay (the holes just don't squirt).")]
        [SerializeField] private Sprite[] _squirtFrames;

        [Tooltip("Flip-book speed of the squirt overlay (frames per second).")]
        [Min(1f)] [SerializeField] private float _squirtFps = 6f;

        [Tooltip("Vertical nudge (local units, positive = up) of the squirt OVERLAY so it animates CENTERED on " +
                 "the two-holes base sprite. The base art's squirt holes sit above its pivot, so the overlay is " +
                 "lifted to line up. Owner-editable — not a magic number.")]
        [SerializeField] private float _squirtVerticalOffset = 0.5f;

        [Header("Skittish clam (proximity escape — real-time cosmetic, never saved)")]
        [Tooltip("How close (m) the on-foot player must linger for the clam to take fright. Tunable, not a " +
                 "magic number — the clam takes fright from well across the flat, so loitering anywhere near it " +
                 "(not just on the hole) spooks it.")]
        [SerializeField] private float _escapeRadius = 7.0f;

        [Tooltip("How long (real seconds) the player may linger inside the escape radius before the clam " +
                 "burrows away. Owner-editable; default 4 s. The timer resets if the player leaves the radius.")]
        [Min(0.1f)] [SerializeField] private float _escapeSeconds = 4f;

        [Tooltip("How long (real seconds) the 'sink into the sand' animation plays before the escaped hole is " +
                 "spent and hidden. A brief give-away that the clam got away.")]
        [Min(0.05f)] [SerializeField] private float _sinkSeconds = 0.6f;

        [Tooltip("Refresh cadence (Hz) of the show/hide tick. The tide is slow; a few Hz is plenty. The escape " +
                 "timer and the sink animation run every frame for a smooth reaction.")]
        [Min(1f)] [SerializeField] private float _refreshHz = 10f;

        private SpriteRenderer _holeRenderer;     // BASE: the two-holes sprite, on while exposed
        private SpriteRenderer _squirtRenderer;   // OVERLAY: the squirt effect, one order on top
        private float _refreshTimer;
        private float _squirtClock;

        private float _lingerTimer;   // how long the player has been inside the escape radius (resets on leave)
        private bool _sinking;        // mid sink-into-sand animation (escape fired)
        private float _sinkClock;     // progress through the sink animation
        private bool _hidden;         // spent (dug or escaped) → gone for the session

        private Vector3 _baseScale;
        private Color _holeBaseColor;
        private bool _initialised;

        private void Awake() => EnsureInit();

        // Idempotent wiring: grab the base renderer, create the overlay child, cache the base scale/colour.
        // Called from Awake AND lazily from Tick / the test observers, so it works even when Awake hasn't run
        // (e.g. components added in EditMode tests don't get Awake — the dig/digger tests configure explicitly).
        private void EnsureInit()
        {
            if (_initialised) return;
            _initialised = true;

            _holeRenderer = GetComponent<SpriteRenderer>();
            if (_dig == null) _dig = GetComponent<ClamDig>();
            if (_holeSprite != null && _holeRenderer != null) _holeRenderer.sprite = _holeSprite;
            if (_holeRenderer != null) _holeRenderer.enabled = false;   // hidden until a tick proves it's bared

            _baseScale = transform.localScale;
            _holeBaseColor = _holeRenderer != null ? _holeRenderer.color : Color.white;

            EnsureSquirtRenderer();
        }

        // The squirt is a SEPARATE renderer one sorting-order above the holes, so it layers OVER them rather
        // than replacing them. Created as a child the visual owns (the builder only places the base renderer).
        private void EnsureSquirtRenderer()
        {
            if (_squirtRenderer != null) return;
            var child = new GameObject("ClamSquirtOverlay");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0f, _squirtVerticalOffset, 0f);   // centered on the holes
            _squirtRenderer = child.AddComponent<SpriteRenderer>();
            if (_holeRenderer != null)
            {
                _squirtRenderer.sortingLayerID = _holeRenderer.sortingLayerID;
                _squirtRenderer.sortingOrder = _holeRenderer.sortingOrder + 1;   // ON TOP of the holes
            }
            _squirtRenderer.enabled = false;
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Advance the visual by <paramref name="dt"/> real seconds: the proximity-escape timer, the
        /// sink animation, and the throttled holes/squirt-overlay draw. Driven by <see cref="Update"/> in play;
        /// public so EditMode tests can step it deterministically without the game loop. Cosmetic only — it
        /// touches no sim/save state (rule 5).</summary>
        public void Tick(float dt)
        {
            EnsureInit();
            if (_hidden) return;

            // Spent by a dig elsewhere (the hole yielded its clam) → vanish promptly. The escape path runs its
            // sink animation first (below), so only a NOT-yet-sinking consumed hole vanishes here.
            if (_dig != null && _dig.Consumed && !_sinking) { Hide(); return; }

            // The sink-into-the-sand animation (escape fired): fade + shrink the holes, then spend + hide.
            if (_sinking) { TickSink(dt); return; }

            bool exposed = _dig != null && _dig.IsExposedNow();

            // The skittish-clam proximity timer runs whenever the hole is exposed and un-spent.
            if (exposed) TickEscape(dt);
            else _lingerTimer = 0f;   // submerged holes don't spook (and aren't visible anyway)

            // If lingering just spooked the clam, hand off to the sink animation this same frame.
            if (_sinking) { TickSink(dt); return; }

            // Throttled show/hide + squirt-overlay step. The escape timer above is per-frame; the picture isn't.
            _refreshTimer -= dt;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 1f / _refreshHz;
            DrawExposed(exposed);
        }

        // Show the holes (base) while exposed and layer the squirt overlay on top while the tell shows.
        private void DrawExposed(bool exposed)
        {
            if (!exposed)
            {
                if (_holeRenderer.enabled) _holeRenderer.enabled = false;
                if (_squirtRenderer != null && _squirtRenderer.enabled) _squirtRenderer.enabled = false;
                return;
            }

            // BASE: the two-holes sprite, always on while exposed (never swapped away by the squirt).
            if (!_holeRenderer.enabled) _holeRenderer.enabled = true;
            if (_holeSprite != null && _holeRenderer.sprite != _holeSprite) _holeRenderer.sprite = _holeSprite;

            // OVERLAY: the squirt effect on its own renderer, ON TOP of the holes — added, not a swap.
            bool squirting = _dig.ShowingSquirt && _squirtFrames != null && _squirtFrames.Length > 0
                             && _squirtRenderer != null;
            if (squirting)
            {
                _squirtClock += 1f / _refreshHz;
                int frame = Mathf.FloorToInt(_squirtClock * _squirtFps) % _squirtFrames.Length;
                if (_squirtFrames[frame] != null) _squirtRenderer.sprite = _squirtFrames[frame];
                if (!_squirtRenderer.enabled) _squirtRenderer.enabled = true;
            }
            else
            {
                _squirtClock = 0f;
                if (_squirtRenderer != null && _squirtRenderer.enabled) _squirtRenderer.enabled = false;
            }
        }

        // Linger logic: count up while the player is inside the escape radius; reset the instant they leave.
        // Fire once the player has lingered the full escape time → start the sink animation.
        private void TickEscape(float dt)
        {
            if (!ClamDigger.TryGetPlayerPosition(out Vector2 playerPos)) { _lingerTimer = 0f; return; }

            Vector2 spot = _dig.SpotPos;
            bool inside = (playerPos - spot).sqrMagnitude <= _escapeRadius * _escapeRadius;
            if (!inside) { _lingerTimer = 0f; return; }   // stepped back in time — the clam settles

            _lingerTimer += dt;
            if (_lingerTimer >= _escapeSeconds) StartSink();
        }

        private void StartSink()
        {
            _sinking = true;
            _sinkClock = 0f;
            if (_dig != null) _dig.MarkConsumed();        // spent — no more yield (it burrowed away)
            if (_squirtRenderer != null) _squirtRenderer.enabled = false;   // the tell's gone
            Debug.Log("[ClamHoleVisual] A clam took fright and burrowed away — you lingered too long.");
        }

        // Fade + shrink the holes into the sand over _sinkSeconds, then hide for good.
        private void TickSink(float dt)
        {
            _sinkClock += dt;
            float t = Mathf.Clamp01(_sinkClock / Mathf.Max(0.05f, _sinkSeconds));

            if (_holeRenderer != null)
            {
                var c = _holeBaseColor; c.a = _holeBaseColor.a * (1f - t);
                _holeRenderer.color = c;
            }
            transform.localScale = Vector3.Lerp(_baseScale, _baseScale * 0.2f, t);

            if (t >= 1f) Hide();
        }

        // Spent and gone for the session: both renderers off, scale/colour restored so a reused object is clean.
        private void Hide()
        {
            _hidden = true;
            if (_holeRenderer != null) { _holeRenderer.enabled = false; _holeRenderer.color = _holeBaseColor; }
            if (_squirtRenderer != null) _squirtRenderer.enabled = false;
            transform.localScale = _baseScale;
        }

        /// <summary>Wire the visual in one call (editor / builder).</summary>
        public void Configure(ClamDig dig, Sprite holeSprite, Sprite[] squirtFrames)
        {
            _dig = dig;
            _holeSprite = holeSprite;
            _squirtFrames = squirtFrames;
        }

        /// <summary>Set the skittish-clam tunables (tests / editor). Negative values leave a field at its
        /// serialized default.</summary>
        public void ConfigureEscape(float escapeRadius, float escapeSeconds, float sinkSeconds)
        {
            if (escapeRadius >= 0f) _escapeRadius = escapeRadius;
            if (escapeSeconds >= 0f) _escapeSeconds = escapeSeconds;
            if (sinkSeconds >= 0f) _sinkSeconds = sinkSeconds;
        }

        /// <summary>Set the squirt overlay's vertical offset (tests / editor) and apply it to the live overlay
        /// so the squirt animates centered on the two-holes base sprite. Positive = up.</summary>
        public void ConfigureSquirtOffset(float verticalOffset)
        {
            _squirtVerticalOffset = verticalOffset;
            EnsureInit();
            if (_squirtRenderer != null)
                _squirtRenderer.transform.localPosition = new Vector3(0f, _squirtVerticalOffset, 0f);
        }

        // ---- Test-facing observers (no behaviour; let EditMode assert the rendered state) ------------------
        /// <summary>The base two-holes renderer (always on while exposed). Exposed for EditMode assertions.</summary>
        public SpriteRenderer HoleRenderer { get { EnsureInit(); return _holeRenderer; } }
        /// <summary>The squirt OVERLAY renderer (a separate child, on top of the holes).</summary>
        public SpriteRenderer SquirtRenderer { get { EnsureInit(); return _squirtRenderer; } }
        /// <summary>True while the holes are sinking into the sand (the escape animation is playing).</summary>
        public bool IsSinking => _sinking;
        /// <summary>True once the hole is spent and gone for the session (dug or escaped).</summary>
        public bool IsHidden => _hidden;
        /// <summary>Seconds the player has currently lingered inside the escape radius (resets on leave).</summary>
        public float LingerTimer => _lingerTimer;
    }
}
