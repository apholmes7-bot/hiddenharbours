using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One tree's PASS-4 wind: the three baked data maps and the shared palette
    /// <c>HiddenHarboursTreeWind</c>'s maps path reads, and the species numbers that drive them, all
    /// as <c>Trees.json</c> publishes them. Filled by the Acadian tree catalog and published by
    /// <see cref="TreeTrunkAnchor"/> on the same property block as the anchor.
    ///
    /// <para>Every number is the bake's (rule 6): the wind response is <c>TreeMaps4.constants()</c> at
    /// the baked stage, the palette row is the glue's, and the cell and sheet are the contract's.
    /// Nothing here is tuned by hand, so there is nothing here to tune; the shared feel (loop length,
    /// gust, wave, jitter, response) lives on the material.</para>
    /// </summary>
    [System.Serializable]
    public struct TreeWindMaps
    {
        [Tooltip("The _wind sheet: R lean, G sway, B and A the packed class, palette and stamp bits. " +
                 "Data, so it imports linear.")]
        public Texture2D Wind;
        [Tooltip("The _phase sheet: R wave, G play, B depth, A the packed bits. Data, imports linear.")]
        public Texture2D Phase;
        [Tooltip("The _snow sheet: the cover byte at which each pixel turns to snow; A is 0 outside " +
                 "the tree. Data, imports linear.")]
        public Texture2D Snow;
        [Tooltip("TreePalette.png, shared by every species: row 0 the snow colours, then each " +
                 "season's gap row. Colour, imports sRGB.")]
        public Texture2D Palette;

        [Tooltip("Trunk bend at a full gale, in px (Trees.json wind.bendPx).")]
        public float BendPx;
        [Tooltip("Limb reach at a full gale, in px (wind.limbPx).")]
        public float LimbPx;
        [Tooltip("A limb's vertical bob, as a share of its reach (wind.bob).")]
        public float Bob;
        [Tooltip("The species' flutter rate (wind.flutter).")]
        public float Flutter;
        [Tooltip("Shimmer strength × calm share: the still-air flutter a broadleaf keeps. 0 for a " +
                 "species without shimmer.")]
        public float ShimmerCalm;
        [Tooltip("A conifer's needle tuft flutters sideways; a broadleaf's also lifts.")]
        public bool Conifer;
        [Tooltip("This season's gap row in the palette, bottom up as Unity numbers texels. Row 0 is " +
                 "the snow row, so a gap row is never 0.")]
        public int PaletteRow;

        [Tooltip("One cell, in texels: the rig's cell, padding included.")]
        public int CellW, CellH;
        [Tooltip("The whole sheet, in texels: one row of variant cells.")]
        public int SheetW, SheetH;

        /// <summary>
        /// Whether the shader can draw from these: all four textures, a gap row that is on the
        /// palette and is not the snow row, and a sheet that is ONE row of whole cells.
        /// <para>⚠️ The last is the maps path's own assumption, not tidiness: it finds a texel's cell
        /// by dividing x alone (<c>TreeWindRigOfTexel</c>), so a sheet with sway rows, or a ragged last
        /// cell, would read another tree's pixels. Anything less than all of it leaves
        /// <c>_TreeMaps</c> at 0 and the tree draws as pass 3 did.</para>
        /// </summary>
        public bool IsComplete =>
            Wind != null && Phase != null && Snow != null && Palette != null &&
            PaletteRow >= 1 && PaletteRow < Palette.height &&
            CellW > 0 && CellH > 0 && SheetW >= CellW && SheetW % CellW == 0 && SheetH == CellH;
    }

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
    /// <para><b>It also carries this tree's LIGHT SHEETS.</b> The same bake that produced the albedo
    /// produced a <c>_mask</c> (R key · G rim · B depth · A coverage) and a <c>_normal</c> sheet, and
    /// <c>HiddenHarboursTreeWind</c>'s light response samples them at the albedo's uv. Those are
    /// per-SPECIES — every species is its own sheet — so they ride the SAME property block as the
    /// anchor rather than forcing ten materials or a second component. There is no per-frame write:
    /// the block is rebuilt on enable/validate only, exactly as the anchor always was (rule 7).</para>
    ///
    /// <para><b>And, for a pass-4 tree, its WIND MAPS</b> (<see cref="TreeWindMaps"/>): the rig's own
    /// wind, read per pixel from baked maps instead of a sway bent into the quad. Same block, same
    /// rebuild-on-enable rule. <c>_TreeMaps</c> goes to 1 only when the whole set is here; anything less
    /// leaves it 0 and the tree draws exactly as a pass-3 tree does, because half a set is a wrong
    /// picture, not a partial one.</para>
    ///
    /// <para>Visual-only: it writes shader properties and nothing else — no sim, no save (rule 5), and
    /// every number is data from the bake, never a literal (rule 6).</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class TreeTrunkAnchor : MonoBehaviour
    {
        /// <summary>The shader float this drives. Matches <c>HiddenHarboursTreeWind.shader</c>.</summary>
        public const string ShaderProperty = "_TrunkAnchor";

        /// <summary>The mask sheet property: R key light · G back rim · B depth · A coverage.
        /// <b>That order is OURS</b> — the reference technique this serves says green = front and
        /// blue = rim, and a snippet ported from it looks subtly wrong rather than broken. Pinned at
        /// the bake by <c>TreeRigBakeTests</c> and at consumption by <c>SpriteLightMath</c>.
        /// <para>Forwards to <see cref="SpriteLightBinding.MaskProperty"/> — the trees now share the
        /// lit-sprite path with the shoreline plants and the shrubs, and a second copy of the property
        /// NAME is exactly how a shared path quietly stops being shared.</para></summary>
        public const string MaskProperty = SpriteLightBinding.MaskProperty;

        /// <summary>The view-space normal sheet property. Smaller in coverage than the mask: the rig's
        /// 1 px keyline ring is opaque in the albedo but has no surface normal, which is why the
        /// shader samples both through the ALBEDO's mesh and falls back to the mask where the normal
        /// is absent.</summary>
        public const string NormalProperty = SpriteLightBinding.NormalProperty;

        /// <summary>Set to 1 when the light sheet is bound. The shader's fallback textures are opaque
        /// black, which would read as "coverage everywhere" — so the response is gated on an explicit
        /// flag rather than on sniffing a texture that is never null.
        /// <para>⚠️ The SHARED path requires only the light sheet; the normal is optional, because the
        /// plant and shrub rigs bake none. <b>A tree still binds both</b> — see
        /// <see cref="HasLightSheets"/> for why that stays a TREE rule rather than becoming everyone's.</para></summary>
        public const string ChannelsProperty = SpriteLightBinding.ChannelsProperty;

        /// <summary>
        /// Where this tree STANDS, in world space (<c>xy</c>), with <c>w</c> = 1 meaning "published".
        /// The shader needs it to point a LOCAL light — the boat lamp — at the right tree.
        ///
        /// <para>🔴 <b>It cannot come from <c>unity_ObjectToWorld</c>.</b> Unity submits SpriteRenderer
        /// meshes with their vertices ALREADY IN WORLD SPACE and an IDENTITY object-to-world matrix, so
        /// <c>_m03/_m13</c> reads (0,0) for every tree in the scene. Measured 2026-07-29: a 3 m-range
        /// lamp parked on the first tree of a three-tree lineup lit all three equally, because every one
        /// of them evaluated the lamp from the world origin. The renderer has to publish its own stand
        /// position — the same lesson the facet renderer learned with <c>_HullOrigin</c>.</para>
        /// </summary>
        public const string RootProperty = SpriteLightBinding.RootProperty;

        /// <summary>Upper bound of the shader property's own <c>Range(0, 0.8)</c>. A value past it
        /// would be silently clamped by the material inspector but NOT by a property block, so clamp
        /// here rather than let a bad bake plant a tree up to its crown.</summary>
        public const float MaxAnchor = 0.8f;

        // ---- pass 4: the rows TREE_WIND_MAPS_MATERIAL_ROWS declares, and the maps it reads ---------

        /// <summary>1 when this renderer's <see cref="TreeWindMaps"/> are complete: the shader then
        /// takes its wind per pixel from the maps and leaves the quad unbent.</summary>
        public const string MapsProperty = "_TreeMaps";
        public const string WindTexProperty = "_TreeWindTex";
        public const string PhaseTexProperty = "_TreePhaseTex";
        public const string SnowTexProperty = "_TreeSnowTex";
        public const string PaletteProperty = "_TreePalette";

        /// <summary>(bendPx, limbPx, bob, flutter).</summary>
        public const string Wind0Property = "_TreeWind0";

        /// <summary>(shimmerCalm, conifer 0 or 1, palette row, 0).</summary>
        public const string Wind1Property = "_TreeWind1";

        /// <summary>(cellW, cellH, sheetW, sheetH), in texels.</summary>
        public const string CellProperty = "_TreeCell";

        private static readonly int AnchorId = Shader.PropertyToID(ShaderProperty);
        private static readonly int MapsId = Shader.PropertyToID(MapsProperty);
        private static readonly int WindTexId = Shader.PropertyToID(WindTexProperty);
        private static readonly int PhaseTexId = Shader.PropertyToID(PhaseTexProperty);
        private static readonly int SnowTexId = Shader.PropertyToID(SnowTexProperty);
        private static readonly int PaletteId = Shader.PropertyToID(PaletteProperty);
        private static readonly int Wind0Id = Shader.PropertyToID(Wind0Property);
        private static readonly int Wind1Id = Shader.PropertyToID(Wind1Property);
        private static readonly int CellId = Shader.PropertyToID(CellProperty);

        [Tooltip("uv.y below which this tree stays planted (its near-root flare pad / cell height). " +
                 "Comes from Trees.json via the Acadian tree builder or the Tree Paint Tool — do not " +
                 "hand-tune it to taste; re-bake instead.")]
        [Range(0f, MaxAnchor)]
        [SerializeField] private float _trunkAnchor = 0.12f;

        [Tooltip("This species' baked light mask sheet (R key, G rim, B depth, A coverage). Set by " +
                 "the Acadian tree builder / Tree Paint Tool alongside the albedo. Leave both this " +
                 "and the normal empty and the tree simply draws unlit, as it did before the light " +
                 "response existed.")]
        [SerializeField] private Texture2D _lightMask;

        [Tooltip("This species' baked view-space normal sheet. Must be the SAME sheet dimensions as " +
                 "the albedo — the shader samples it at the albedo's uv.")]
        [SerializeField] private Texture2D _lightNormal;

        [Tooltip("Pass 4: this species' baked wind, phase and snow maps, the shared palette and the " +
                 "numbers that drive them. Set by the Acadian tree builder and the Tree Paint Tool. " +
                 "Leave it incomplete and the tree sways as a pass-3 tree does.")]
        [SerializeField] private TreeWindMaps _windMaps;

        private SpriteRenderer _sr;
        private MaterialPropertyBlock _block;

        /// <summary>This tree's planted fraction. Setting it re-applies immediately.</summary>
        public float Anchor
        {
            get => _trunkAnchor;
            set { _trunkAnchor = Mathf.Clamp(value, 0f, MaxAnchor); Apply(); }
        }

        /// <summary>
        /// Bind this species' baked light sheets (or clear them by passing nulls). Both must be
        /// present for the shader's response to run — half a pair is not a partial effect, it is a
        /// wrong one, so <see cref="ChannelsProperty"/> only goes to 1 when both are here.
        /// Re-applies immediately.
        /// </summary>
        public void SetLightSheets(Texture2D mask, Texture2D normal)
        {
            _lightMask = mask;
            _lightNormal = normal;
            Apply();
        }

        /// <summary>True when this tree has BOTH baked sheets. Used by the tests and by tools reporting
        /// what a placed tree will actually do.
        /// <para>⚠️ The shared path would light a tree off the mask alone, and deliberately does so for
        /// the plants and shrubs whose rigs bake no normal. <b>A TREE is still held to both</b>: its rig
        /// DOES bake a normal, every one of its tuned strengths was measured with that normal present,
        /// and a tree that lost half its pair is a bake that went wrong — not a rig family that ships
        /// without one. Reporting that as "lit" would hide the very thing worth catching.</para></summary>
        public bool HasLightSheets => _lightMask != null && _lightNormal != null;

        /// <summary>
        /// Bind this tree's pass-4 wind maps, or clear them with <c>default</c>. Re-applies
        /// immediately; <see cref="MapsProperty"/> goes to 1 only when the set is complete.
        /// </summary>
        public void SetWindMaps(TreeWindMaps maps)
        {
            _windMaps = maps;
            Apply();
        }

        /// <summary>The maps this tree carries, as set.</summary>
        public TreeWindMaps WindMaps => _windMaps;

        /// <summary>True when this tree draws its wind from its maps (the set is complete).</summary>
        public bool HasWindMaps => _windMaps.IsComplete;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();
        private void OnEnable() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }
        private void OnValidate() { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); Apply(); }

#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY, and only outside Play: re-publish the stand position when the owner DRAGS the
        /// tree in the scene view. <c>OnValidate</c> does not fire for a transform move, so without this
        /// a dragged tree keeps lighting from where it used to be. Compiled out of every build, skipped
        /// in Play (trees are static there), and it writes the block only when the position ACTUALLY
        /// changed — so it is a Vector3 compare per editor frame, never a per-frame MPB write (rule 7).
        /// </summary>
        private void Update()
        {
            if (Application.isPlaying) return;
            Vector3 here = transform.position;
            if ((here - _publishedRoot).sqrMagnitude <= 1e-10f) return;
            Apply();
        }

        private Vector3 _publishedRoot = new Vector3(float.NaN, float.NaN, float.NaN);
#endif

        private void Apply()
        {
            if (_sr == null) return;
            _block ??= new MaterialPropertyBlock();

            // The LIGHT half goes through the shared writer — the same call the shoreline plants and the
            // shrubs make, so there is exactly one piece of code that knows how to bind these properties
            // (and one place the two MaterialPropertyBlock traps are paid for). A tree binds no rim gate:
            // no tree pixel is forbidden a rim, so the selector stays zero and the shader's gate resolves
            // to exactly 1.0 — the multiply is the IEEE 754 identity and the tree renders bit-identically
            // to how it did before the path was shared.
            //
            // ⚠️ Both sheets or neither, for a TREE (see HasLightSheets). The shared writer would happily
            // light off the mask alone; passing a half pair here would silently accept a broken bake.
            // GET once, write both halves, SET once — Fill (rather than Apply) exists precisely so a
            // caller with its own property to add does not pay for a second SetPropertyBlock.
            _sr.GetPropertyBlock(_block);

            bool both = _lightMask != null && _lightNormal != null;
            SpriteLightBinding.Fill(
                _block,
                both ? _lightMask : null,
                both ? _lightNormal : null,
                rimGate: null,
                rimGateChannel: SpriteLightBinding.RimGateNone,
                root: transform.position);

            // The ANCHOR is the tree's own property and no part of the shared lighting contract.
            _block.SetFloat(AnchorId, Mathf.Clamp(_trunkAnchor, 0f, MaxAnchor));

            // PASS 4's maps, on the same block. Bind them only when the set is complete (SetTexture
            // throws on null), and let the flag turn the path off otherwise, the shared writer's rule:
            // a map left in the block from an earlier set is never read while the flag says 0. The
            // stand position the loop's phase needs is already here: Fill always publishes it.
            bool maps = _windMaps.IsComplete;
            if (maps)
            {
                _block.SetTexture(WindTexId, _windMaps.Wind);
                _block.SetTexture(PhaseTexId, _windMaps.Phase);
                _block.SetTexture(SnowTexId, _windMaps.Snow);
                _block.SetTexture(PaletteId, _windMaps.Palette);
                _block.SetVector(Wind0Id, new Vector4(
                    _windMaps.BendPx, _windMaps.LimbPx, _windMaps.Bob, _windMaps.Flutter));
                _block.SetVector(Wind1Id, new Vector4(
                    _windMaps.ShimmerCalm, _windMaps.Conifer ? 1f : 0f, _windMaps.PaletteRow, 0f));
                _block.SetVector(CellId, new Vector4(
                    _windMaps.CellW, _windMaps.CellH, _windMaps.SheetW, _windMaps.SheetH));
            }
            _block.SetFloat(MapsId, maps ? 1f : 0f);

            _sr.SetPropertyBlock(_block);

#if UNITY_EDITOR
            _publishedRoot = transform.position;
#endif
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
        /// Read back the <see cref="ChannelsProperty"/> flag this renderer will actually hand the
        /// shader: 1 when both baked sheets reached it, 0 otherwise. Same shape as
        /// <see cref="AnchorOn"/> — asserted against the RENDERER rather than the fields that were
        /// written to it, because "the value reached the block" is the thing that can break.
        /// </summary>
        public static float LightChannelsOn(SpriteRenderer renderer, float fallback = -1f) =>
            SpriteLightBinding.ChannelsOn(renderer, fallback);

        /// <summary>
        /// Read back the stand position this renderer will hand the shader, or a <c>w</c> of 0 when
        /// nothing published one. Same shape as <see cref="AnchorOn"/>: asserted against the RENDERER,
        /// because "the position reached the block" is exactly what silently did not happen when the
        /// shader tried to read it from <c>unity_ObjectToWorld</c>.
        /// </summary>
        public static Vector4 RootOn(SpriteRenderer renderer) => SpriteLightBinding.RootOn(renderer);

        /// <summary>The texture this renderer will hand <see cref="MaskProperty"/>, or null.</summary>
        public static Texture MaskOn(SpriteRenderer renderer) => SpriteLightBinding.MaskOn(renderer);

        /// <summary>The texture this renderer will hand <see cref="NormalProperty"/>, or null.</summary>
        public static Texture NormalOn(SpriteRenderer renderer) => SpriteLightBinding.NormalOn(renderer);

        /// <summary>
        /// Read back the <see cref="MapsProperty"/> flag this renderer will hand the shader: 1 when a
        /// complete pass-4 set reached it, 0 otherwise. Same shape as <see cref="AnchorOn"/>.
        /// </summary>
        public static float TreeMapsOn(SpriteRenderer renderer, float fallback = -1f)
        {
            var block = BlockOn(renderer);
            return block != null && block.HasFloat(MapsId) ? block.GetFloat(MapsId) : fallback;
        }

        /// <summary>The <see cref="Wind0Property"/> row this renderer will hand the shader, or
        /// <paramref name="fallback"/>.</summary>
        public static Vector4 Wind0On(SpriteRenderer renderer, Vector4 fallback = default) =>
            VectorOn(renderer, Wind0Id, fallback);

        /// <summary>The <see cref="Wind1Property"/> row, or <paramref name="fallback"/>.</summary>
        public static Vector4 Wind1On(SpriteRenderer renderer, Vector4 fallback = default) =>
            VectorOn(renderer, Wind1Id, fallback);

        /// <summary>The <see cref="CellProperty"/> row, or <paramref name="fallback"/>.</summary>
        public static Vector4 CellOn(SpriteRenderer renderer, Vector4 fallback = default) =>
            VectorOn(renderer, CellId, fallback);

        /// <summary>The texture this renderer will hand <see cref="WindTexProperty"/>, or null.</summary>
        public static Texture WindTexOn(SpriteRenderer renderer) => TextureOn(renderer, WindTexId);

        /// <summary>The texture this renderer will hand <see cref="PhaseTexProperty"/>, or null.</summary>
        public static Texture PhaseTexOn(SpriteRenderer renderer) => TextureOn(renderer, PhaseTexId);

        /// <summary>The texture this renderer will hand <see cref="SnowTexProperty"/>, or null.</summary>
        public static Texture SnowTexOn(SpriteRenderer renderer) => TextureOn(renderer, SnowTexId);

        /// <summary>The texture this renderer will hand <see cref="PaletteProperty"/>, or null.</summary>
        public static Texture PaletteOn(SpriteRenderer renderer) => TextureOn(renderer, PaletteId);

        private static MaterialPropertyBlock BlockOn(SpriteRenderer renderer)
        {
            if (renderer == null || !renderer.HasPropertyBlock()) return null;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block;
        }

        private static Vector4 VectorOn(SpriteRenderer renderer, int id, Vector4 fallback)
        {
            var block = BlockOn(renderer);
            return block != null && block.HasVector(id) ? block.GetVector(id) : fallback;
        }

        private static Texture TextureOn(SpriteRenderer renderer, int id)
        {
            var block = BlockOn(renderer);
            return block != null && block.HasTexture(id) ? block.GetTexture(id) : null;
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
