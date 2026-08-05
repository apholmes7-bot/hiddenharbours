using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Drop this on any SpriteRenderer whose rig baked a light channel, and it lights.</b> That
    /// sentence is the whole point of the component: before it existed there was exactly ONE lit sprite
    /// family — the trees — because lighting a second one meant writing a second shader and a second
    /// binder. This is the binder half, generalised; <c>HiddenHarbours/LitSprite</c> is the shader half.
    ///
    /// <para><b>What it publishes.</b> The light sheet (required), the view-space normal sheet
    /// (optional — only the tree rig bakes one), the no-rim gate sheet and its channel selector
    /// (optional), and where the sprite stands in world space. All through ONE
    /// <see cref="MaterialPropertyBlock"/> via <see cref="SpriteLightBinding"/>, which is the only writer
    /// of these properties in the project.</para>
    ///
    /// <para><b>⚠️ Why per-renderer and not per-material.</b> Every species is its own sheet, so a
    /// material per species would mean one asset per plant, per shrub, per rock — assets that drift apart
    /// and that a re-bake has to author. A property block keeps the owner's tuning in ONE material. The
    /// batching this costs is batching these sprites never had: sprites batch only when material, TEXTURE
    /// and sorting order all line up, and <see cref="YSortSprite"/> gives each one a sorting order off its
    /// world Y — so two neighbours almost never share a batch key in the first place. Measured for the
    /// trees by <c>AcadianTreePlacementTests.APropertyBlockCostsBatchingTheseTreesNeverHad</c>.</para>
    ///
    /// <para><b>⚠️ Why a component and not a one-shot call at build time.</b> A property block is
    /// runtime-only state on the Renderer — Unity does not serialize it, so one set at author time does
    /// not survive into a saved prefab or across a domain reload. The serialized fields live here and are
    /// re-applied on enable/validate, the same shape <see cref="YSortSprite"/> uses to own
    /// <c>sortingOrder</c> and <see cref="TreeTrunkAnchor"/> uses to own <c>_TrunkAnchor</c>.</para>
    ///
    /// <para><b>⚠️ THERE IS NO Update, AND THAT IS A PERFORMANCE DECISION.</b> St Peters places hundreds
    /// of these. An empty <c>Update</c> still costs a managed dispatch per instance per frame — the exact
    /// cost <see cref="ShorePlantTideView"/> and <see cref="YSortSprite"/> both refuse. The block is
    /// rebuilt on enable/validate only; a sprite that never moves never writes again (rule 7).</para>
    ///
    /// <para>Visual-only: it writes shader properties and nothing else — no sim, no save (rule 5), and
    /// every number is data from the bake, never a literal (rule 6).</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class SpriteLightBinder : MonoBehaviour
    {
        /// <summary>Which channel of the gate sheet means "this pixel may not rim". Named rather than
        /// numbered because the two shipped rigs use different ones and a bare index at a call site is
        /// unreadable — see <see cref="SpriteLightBinding.RimGateChannelProperty"/>.</summary>
        public enum RimGateChannel
        {
            /// <summary>No gate. Every pixel may rim — the tree, and the default.</summary>
            None = 0,
            /// <summary>RED. The SHRUB rig: <c>_calendar.png</c> R is 255 on veil pixels.</summary>
            Red = 1,
            /// <summary>GREEN. Unused by any shipped rig; here so the selector is complete rather than
            /// so narrow that the next rig has to widen it.</summary>
            Green = 2,
            /// <summary>BLUE. The SHORELINE-PLANT rig: <c>_tide.png</c> B is 255 on strap pixels.</summary>
            Blue = 3,
        }

        [Tooltip("This species' baked light sheet (R key, G rim, B depth, A coverage). REQUIRED — with " +
                 "no sheet the sprite simply draws unlit, exactly as it did before this component " +
                 "existed. Set by the planter/builder alongside the albedo.")]
        [SerializeField] private Texture2D _lightSheet;

        [Tooltip("This species' baked view-space normal sheet, if its rig bakes one (only the TREE rig " +
                 "does). Must be the SAME sheet dimensions as the albedo — the shader samples it at the " +
                 "albedo's uv. Leave empty for the shoreline plants and the shrubs: they resolve their " +
                 "normals at bake, and every light term already falls back to the baked mask.")]
        [SerializeField] private Texture2D _normalSheet;

        [Tooltip("The state sheet carrying this rig's no-rim flag, if it has one. Shoreline plants: the " +
                 "_tide sheet. Shrubs: the _calendar sheet. Leave empty for a rig with no such flag.")]
        [SerializeField] private Texture2D _rimGateSheet;

        [Tooltip("Which channel of the gate sheet is 255 where a rim is forbidden. Read it from the " +
                 "rig's own committed contract — the rigs disagree, and both contracts say in so many " +
                 "words: this is the branch, read it, do not infer it.")]
        [SerializeField] private RimGateChannel _rimGateChannel = RimGateChannel.None;

        private SpriteRenderer _sr;
        private MaterialPropertyBlock _block;

        /// <summary>True when a light sheet is bound and the shader's response can run.</summary>
        public bool HasLightSheet => _lightSheet != null;

        /// <summary>True when this binder also carries a view-space normal sheet (the tree rig only).</summary>
        public bool HasNormalSheet => _normalSheet != null;

        /// <summary>The gate channel this binder publishes. <see cref="RimGateChannel.None"/> unless a
        /// gate sheet is bound as well — an unbound sheet forces the selector to zero, because Unity's
        /// fallback black texture has an ALPHA of 1 and would otherwise read as "no rim anywhere".</summary>
        public RimGateChannel GateChannel => _rimGateSheet != null ? _rimGateChannel : RimGateChannel.None;

        /// <summary>
        /// Bind this species' baked sheets (or clear them by passing nulls) and re-apply immediately.
        /// The normal and the gate are optional; the light sheet is what turns the response on.
        /// </summary>
        public void SetSheets(
            Texture2D lightSheet, Texture2D normalSheet = null,
            Texture2D rimGateSheet = null, RimGateChannel rimGateChannel = RimGateChannel.None)
        {
            _lightSheet = lightSheet;
            _normalSheet = normalSheet;
            _rimGateSheet = rimGateSheet;
            _rimGateChannel = rimGateChannel;
            Apply();
        }

        private void Awake() => _sr = GetComponent<SpriteRenderer>();
        private void OnEnable() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }
        private void OnValidate() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }

#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY, and only outside Play: re-publish the stand position when the owner DRAGS the
        /// sprite in the scene view. <see cref="OnValidate"/> does not fire for a transform move, so
        /// without this a dragged plant keeps lighting from where it used to be. Compiled out of every
        /// build, skipped in Play (this decor is static there), and it writes the block only when the
        /// position ACTUALLY changed — a Vector3 compare per editor frame, never a per-frame write.
        /// </summary>
        private void Update()
        {
            if (Application.isPlaying) return;
            if ((transform.position - _publishedRoot).sqrMagnitude <= 1e-10f) return;
            Apply();
        }

        private Vector3 _publishedRoot = new Vector3(float.NaN, float.NaN, float.NaN);
#endif

        /// <summary>Re-publish this renderer's binding. Safe to call by hand; the planters do, once.</summary>
        public void Apply()
        {
            if (_sr == null) return;
            _block ??= new MaterialPropertyBlock();

            SpriteLightBinding.Apply(
                _sr, _block, _lightSheet, _normalSheet, _rimGateSheet,
                SelectorFor(_rimGateChannel), transform.position);

#if UNITY_EDITOR
            _publishedRoot = transform.position;
#endif
        }

        /// <summary>The one-hot selector for a channel. Pure, so a test can assert the mapping without a
        /// renderer — and so the mapping is stated once rather than at every call site.</summary>
        public static Vector4 SelectorFor(RimGateChannel channel) =>
            channel switch
            {
                RimGateChannel.Red => SpriteLightBinding.RimGateRed,
                RimGateChannel.Green => new Vector4(0f, 1f, 0f, 0f),
                RimGateChannel.Blue => SpriteLightBinding.RimGateBlue,
                _ => SpriteLightBinding.RimGateNone,
            };
    }
}
