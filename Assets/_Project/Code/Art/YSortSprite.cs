using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// AUTO-LAYERS a sprite by its world Y for the ¾ top-down view — a sprite lower on the screen (smaller Y,
    /// nearer the camera) draws IN FRONT of one higher up. Put it on anything that should interleave with the
    /// player by position — grass tufts, trees, the player — and the layering sorts itself out, so the owner
    /// never hand-tunes a sorting order per piece.
    ///
    /// <para><b>How it sorts.</b> <c>sortingOrder = clamp(round(baseOrder − worldY · orderPerUnit), min, max)</c>.
    /// The clamp keeps the result inside a SAFE band so a Y-sorted sprite can never slip behind the ground
    /// tiles / water (which sit at large negative orders) or above the HUD (large positive) — it only
    /// re-orders within the world-decor band. Every term is a tunable field (no magic numbers, rule 6); the
    /// defaults put a sprite at Y≈0 near the on-foot player's old fixed order, so existing scenes read the
    /// same until something actually moves past something else.</para>
    ///
    /// <para><b>Static vs dynamic (perf, rule 7).</b> Decor doesn't move, so a STATIC sprite computes its order
    /// ONCE on enable and then DISABLES itself in play mode — <c>enabled = false</c> stops the engine
    /// dispatching <c>Update</c>/<c>LateUpdate</c> at all, so a clearing of ~1300 tufts/trees costs literally
    /// nothing per frame instead of ~1300 empty calls. A mover (the player) sets <see cref="Dynamic"/> so it
    /// stays enabled and re-sorts in <c>LateUpdate</c> (after it has moved this frame); flipping
    /// <see cref="Dynamic"/> at runtime re-arms or stops that dispatch. A region toggle's <c>SetActive</c>
    /// leaves a parked sprite parked (the component is disabled, so <c>OnEnable</c> doesn't fire) — safe,
    /// because <c>sortingOrder</c> persists on the renderer and static decor never moves in play mode. To
    /// force a one-shot re-sort (e.g. after teleporting a "static" prop), set <c>enabled = true</c>: it
    /// re-sorts and stands itself down again.
    /// In the EDITOR (<c>[ExecuteAlways]</c>) it stays enabled and re-sorts continuously so the Scene view
    /// shows the right layering WHILE you drag decor around — that edit-mode work never runs in a build.</para>
    ///
    /// <para>Visual-only: it writes only <see cref="SpriteRenderer.sortingOrder"/> — no sim, no save (rule 5).</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class YSortSprite : MonoBehaviour
    {
        [Tooltip("Tick for things that MOVE (the player) so they re-sort every frame. Leave OFF for static " +
                 "decor (grass, trees) — those sort once on enable, then disable themselves in play mode so " +
                 "they cost nothing per frame. Flip at runtime via the Dynamic property, not this field.")]
        [SerializeField] private bool _dynamic;

        [Tooltip("Sorting order for a sprite sitting at world Y = 0. The default sits near the on-foot player's " +
                 "old fixed order so the scene reads the same until things actually pass each other.")]
        [SerializeField] private float _baseOrder = 10f;

        [Tooltip("How many sorting-order steps per world-metre of Y. Higher = finer depth steps (smoother " +
                 "front/back flips) but a wider order swing. 4 ≈ a step every 0.25 m.")]
        [SerializeField] private float _orderPerUnit = 4f;

        [Tooltip("Lowest order this may emit — keeps a far-'up' sprite from sinking behind water/ground.")]
        [SerializeField] private int _minOrder = 2;
        [Tooltip("Highest order this may emit — keeps a far-'down' sprite from rising above the HUD.")]
        [SerializeField] private int _maxOrder = 40;

        [Tooltip("Sort by a point offset from the transform along Y (metres). 0 sorts by the object's own " +
                 "position (the base, since our decor/player pivot at the feet). Rarely needs changing.")]
        [SerializeField] private float _pivotYOffset;

        private SpriteRenderer _sr;

        /// <summary>
        /// Whether this sprite re-sorts every frame (a mover) or was sorted once on enable (static decor).
        /// Static instances disable themselves in play mode to stop Update/LateUpdate dispatch, so a runtime
        /// flip must come through here: true re-arms the per-frame sort, false sorts once more at the resting
        /// spot and stands the dispatch down again.
        /// </summary>
        public bool Dynamic
        {
            get => _dynamic;
            set
            {
                _dynamic = value;
                if (!Application.isPlaying) return;
                if (value) enabled = true;  // OnEnable re-sorts (now or on activation); dynamic, so it stays on
                // Park only a LIVE dispatcher: on an inactive GO the component must stay enabled so its
                // first OnEnable still runs the one-shot sort (which then parks it itself).
                else if (isActiveAndEnabled) { Apply(); enabled = false; }
            }
        }

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        private void OnEnable()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            Apply();
            // Static decor is fully sorted now — disable so the engine stops dispatching Update/LateUpdate
            // to it entirely (rule 7). Edit mode stays enabled for the Scene-view WYSIWYG re-sort below.
            if (Application.isPlaying && !_dynamic) enabled = false;
        }

        private void OnValidate()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            Apply();
#if UNITY_EDITOR
            // Ticking _dynamic in the inspector DURING play must re-arm the self-disabled dispatch; enabled
            // can't be toggled inside OnValidate (it would SendMessage), so defer it one editor tick.
            if (Application.isPlaying && _dynamic && !enabled)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null && _dynamic) enabled = true; };
#endif
        }

        // Edit-mode WYSIWYG: keep decor sorted as it's dragged in the Scene view. Never runs in a build.
        private void Update() { if (!Application.isPlaying) Apply(); }

        // Play-mode movers re-sort AFTER they've moved this frame. Static sprites never get here (self-disabled).
        private void LateUpdate() { if (Application.isPlaying && _dynamic) Apply(); }

        private void Apply()
        {
            if (_sr == null) return;
            float y = transform.position.y + _pivotYOffset;
            _sr.sortingOrder = OrderFor(y, _baseOrder, _orderPerUnit, _minOrder, _maxOrder);
        }

        /// <summary>
        /// The Y → sorting-order mapping (pure; unit-tested headless). Lower world Y ⇒ higher order ⇒ drawn in
        /// front, clamped into the <paramref name="minOrder"/>..<paramref name="maxOrder"/> safe band.
        /// Monotonic non-increasing in <paramref name="worldY"/>.
        /// </summary>
        public static int OrderFor(float worldY, float baseOrder, float orderPerUnit, int minOrder, int maxOrder)
        {
            int order = Mathf.RoundToInt(baseOrder - worldY * orderPerUnit);
            return Mathf.Clamp(order, minOrder, maxOrder);
        }
    }
}
