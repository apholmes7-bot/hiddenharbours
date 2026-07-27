using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Drives <c>_TrunkAnchor</c> on ONE tree renderer — the uv.y below which
    /// <c>HiddenHarbours/TreeWind</c> holds the tree planted and above which the canopy sways.
    /// The value belongs to the SPECIES, not to the material: it is that species' near-root flare
    /// pad as a fraction of its cell (<c>nearFlarePad / cellH</c>, published per species in
    /// <c>Art/Foliage/Trees/Trees.json</c>), and the measured spread across the Acadian kit is
    /// <b>0.0833 (Black Spruce) to 0.1447 (Red Oak)</b>.
    ///
    /// <para><b>Why this exists at all.</b> <c>Art/Materials/Tree.mat</c> ships a single
    /// material-wide 0.14 — the very TOP of that range — so one shared material over-anchors eight
    /// of the ten species, freezing canopy that should be moving. Nothing about that reads as a bug;
    /// the trees simply look stiffer than they are. This component is what makes the shipped
    /// material's constant stop mattering.</para>
    ///
    /// <para><b>Why a component and not just a MaterialPropertyBlock.</b> A property block is
    /// runtime-only state on the Renderer — Unity does not serialize it, so one set at author time
    /// does not survive into a saved prefab or across a domain reload. The serialized float lives
    /// here and is re-applied on enable/validate, which is the same shape
    /// <see cref="YSortSprite"/> uses to own <c>sortingOrder</c>.</para>
    ///
    /// <para><b>Why not a material per species.</b> One shared material keeps the owner's sway
    /// tuning (amount, speed, gust, lean) in ONE asset instead of ten that drift apart, and keeps a
    /// re-bake from having to author new assets. The batching that a property block costs is
    /// batching these trees never had: sprites batch only when material, TEXTURE and sorting order
    /// all line up, every species is its own sheet (its own texture), and
    /// <see cref="YSortSprite"/> gives each tree a sorting order off its world Y — so two trees in a
    /// stand almost never share a batch key in the first place. Measured by
    /// <c>AcadianTreePlacementTests.APropertyBlockCostsBatchingTheseTreesNeverHad</c>.</para>
    ///
    /// <para>Visual-only: it writes one shader float and nothing else — no sim, no save (rule 5),
    /// and the number is data from the bake, never a literal (rule 6).</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class TreeTrunkAnchor : MonoBehaviour
    {
        /// <summary>The shader float this drives. Matches <c>HiddenHarboursTreeWind.shader</c>.</summary>
        public const string ShaderProperty = "_TrunkAnchor";

        /// <summary>Upper bound of the shader property's own <c>Range(0, 0.8)</c>. A value past it
        /// would be silently clamped by the material inspector but NOT by a property block, so clamp
        /// here rather than let a bad bake plant a tree up to its crown.</summary>
        public const float MaxAnchor = 0.8f;

        private static readonly int AnchorId = Shader.PropertyToID(ShaderProperty);

        [Tooltip("uv.y below which this tree stays planted (its near-root flare pad / cell height). " +
                 "Comes from Trees.json via the Acadian tree builder or the Tree Paint Tool — do not " +
                 "hand-tune it to taste; re-bake instead.")]
        [Range(0f, MaxAnchor)]
        [SerializeField] private float _trunkAnchor = 0.12f;

        private SpriteRenderer _sr;
        private MaterialPropertyBlock _block;

        /// <summary>This tree's planted fraction. Setting it re-applies immediately.</summary>
        public float Anchor
        {
            get => _trunkAnchor;
            set { _trunkAnchor = Mathf.Clamp(value, 0f, MaxAnchor); Apply(); }
        }

        private void Awake() => _sr = GetComponent<SpriteRenderer>();
        private void OnEnable() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }
        private void OnValidate() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }

        private void Apply()
        {
            if (_sr == null) return;
            _block ??= new MaterialPropertyBlock();

            // GET first, then modify, then SET. A SpriteRenderer keeps its own per-renderer
            // overrides (the sprite texture among them) in this same block — handing it a FRESH
            // block drops them, and Tree.mat's _MainTex is empty, so the tree would draw as a white
            // rectangle. This is the difference between a working tree and a very confusing one.
            _sr.GetPropertyBlock(_block);
            _block.SetFloat(AnchorId, Mathf.Clamp(_trunkAnchor, 0f, MaxAnchor));
            _sr.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Read back what this renderer will actually hand the shader. Returns
        /// <paramref name="fallback"/> when no block is set — used by the tests, so "the value
        /// reached the renderer" is asserted against the renderer rather than against the field
        /// that was just written to it.
        /// </summary>
        public static float AnchorOn(SpriteRenderer renderer, float fallback = -1f)
        {
            if (renderer == null || !renderer.HasPropertyBlock()) return fallback;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.HasFloat(AnchorId) ? block.GetFloat(AnchorId) : fallback;
        }

        /// <summary>
        /// The shader's own canopy weight at a height <paramref name="uvY"/> up the cell:
        /// <c>smoothstep(anchor, 1, uv.y)²</c>, exactly as
        /// <c>HiddenHarboursTreeWind.shader</c> computes it in the vertex stage. Pure, so a test can
        /// measure what a WRONG anchor actually costs in motion instead of just asserting a float.
        /// </summary>
        public static float CanopyWeight(float anchor, float uvY)
        {
            float t = Mathf.Clamp01(uvY);
            float s = Mathf.Clamp01((t - anchor) / Mathf.Max(1f - anchor, 1e-6f));
            s = s * s * (3f - 2f * s);   // smoothstep
            return s * s;                // the shader squares it
        }
    }
}
