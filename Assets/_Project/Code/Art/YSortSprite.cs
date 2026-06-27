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
    /// ONCE on enable — no per-frame cost no matter how many tufts a clearing holds. A mover (the player) sets
    /// <see cref="_dynamic"/> so it re-sorts in <c>LateUpdate</c> (after it has moved this frame). In the EDITOR
    /// (<c>[ExecuteAlways]</c>) it also re-sorts continuously so the Scene view shows the right layering WHILE
    /// you drag decor around — but that edit-mode work never runs in a build.</para>
    ///
    /// <para>Visual-only: it writes only <see cref="SpriteRenderer.sortingOrder"/> — no sim, no save (rule 5).</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class YSortSprite : MonoBehaviour
    {
        [Tooltip("Tick for things that MOVE (the player) so they re-sort every frame. Leave OFF for static " +
                 "decor (grass, trees) — those compute their order once and cost nothing per frame.")]
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

        private void Awake() => _sr = GetComponent<SpriteRenderer>();
        private void OnEnable() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }

        private void OnValidate() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }

        // Edit-mode WYSIWYG: keep decor sorted as it's dragged in the Scene view. Never runs in a build.
        private void Update() { if (!Application.isPlaying) Apply(); }

        // Play-mode movers re-sort AFTER they've moved this frame. Static sprites skip this (sorted once on enable).
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
