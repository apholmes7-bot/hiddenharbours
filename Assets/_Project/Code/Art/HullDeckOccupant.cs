using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>ONE PERSON DRAWN INTO A MESH HULL'S OWN IMAGE</b> — the deck half of ADR 0022 phase 3's
    /// depth contract (owner decision 2026-07-21), turned on by the owner's playtest of 2026-08-07:
    /// <i>"rider/player sprites visible THROUGH closed cabins on models with doors and a cockpit."</i>
    ///
    /// <para><b>Why the crew cannot simply be sorted.</b> A mesh hull's only in-scene face is one
    /// cell-sized overlay quad at one sorting order (<see cref="IsoFacetHullRenderer"/>), so a crew
    /// SPRITE compared against it is either wholly in front of the boat or wholly behind her — and
    /// behind her is invisible, because the sole they stand on is hull pixels too. Every shipped
    /// <c>BoatVisualDef</c> carries <c>SortingOrder</c> 1, below <c>SortingBands.DecorFloor</c>, while
    /// the player carries a Y-sorted order in the decor band — so the fisher won that comparison at
    /// every position and was drawn straight over the wheelhouse. Raising or lowering either number
    /// only moves which half is wrong: the question is per-PIXEL and sorting answers per-OBJECT.</para>
    ///
    /// <para><b>So the crew is not sorted against the boat at all — they are composited INTO her.</b>
    /// <see cref="IsoFacetHullFeature"/> keeps the facet pass's private z-buffer attached for a second
    /// renderer list, and anything with an <c>HHHullDeck</c> pass is depth-tested per pixel against
    /// the hull geometry and writes depth back. This component is one such renderer: a sprite-shaped
    /// quad drawn through <c>HiddenHarbours/HullDeckSprite</c>, carrying the hull's own id so the
    /// hull's overlay quad re-composes crew and boat as one image. The wheelhouse then occludes the
    /// fisher for exactly the reason it occludes the hull's own far gunwale, on every hull in the
    /// fleet, with no authored footprint and no re-bake.</para>
    ///
    /// <para><b>The depth it stands at.</b> The caller supplies the boots' depth IN THE HULL'S FRAME
    /// (<c>DeckAreaMath.DeckDepth</c> — the third row of the very projection the deck-walk
    /// already uses for the first two), and this adds the hull's own frame offset, which is where the
    /// waterline calibration lives (ADR 0023 phase 3: while a displaced sea is up, every hull's frame
    /// is translated into the water's iso-depth convention, so a raw world-z occupant would sit far
    /// nearer than its calibrated hull and win everywhere). Riding the hull's frame is exactly what
    /// the feature's own doc requires of a deck occupant.</para>
    ///
    /// <para><b>Budget (rule 7).</b> The quad is rebuilt ONLY when the sprite's geometry changes —
    /// a walk cycle's frames share one rect, so an animating fisher rebuilds nothing. Per frame it is
    /// one transform write and one property block: no allocation, no <c>GetComponent</c>.</para>
    ///
    /// <para><b>Visual-only</b> (rule 5): it reads a pose and draws it, and writes nothing back.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HullDeckOccupant : MonoBehaviour
    {
        /// <summary>The shader every deck occupant draws through. Resolved once per component.</summary>
        public const string ShaderName = "HiddenHarbours/HullDeckSprite";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int KeyColorId = Shader.PropertyToID("_KeyColor");
        private static readonly int HullIdId = Shader.PropertyToID("_HullId");
        private static readonly int FeetZId = Shader.PropertyToID("_FeetZ");
        private static readonly int FeetYId = Shader.PropertyToID("_FeetY");
        private static readonly int DepthPerScreenRiseId = Shader.PropertyToID("_DepthPerScreenRise");

        private Material _material;
        private Mesh _quad;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MaterialPropertyBlock _props;

        // The sheet geometry the current quad was built for. A cell from the same sheet row shares
        // all of it, so an animating figure never rebuilds the mesh.
        private Texture _builtTexture;
        private Rect _builtRect;
        private Vector2 _builtPivot;
        private float _builtPixelsPerUnit;
        private bool _built;

        /// <summary>True while a figure is actually being drawn. For tests and tooling.</summary>
        public bool IsDrawing => _renderer != null && _renderer.enabled;

        /// <summary>The sprite currently drawn, or null when standing down. For tests and tooling.</summary>
        public Sprite Sprite { get; private set; }

        /// <summary>The view depth (world z, metres) written at the figure's BOOTS last
        /// <see cref="Show"/>. For tests and tooling.</summary>
        public float FeetDepth { get; private set; }

        /// <summary>
        /// Draw <paramref name="sprite"/> standing on this hull.
        /// </summary>
        /// <param name="sprite">The character cell. Null stands the occupant down.</param>
        /// <param name="worldFeet">Where the boots meet the deck, in world metres (x, y) — the same
        /// screen position the in-scene sprite would have been drawn at, so nothing about WHERE the
        /// figure appears changes; only what may cover them.</param>
        /// <param name="hullFrameDepth">The boots' depth in the HULL's frame
        /// (<c>DeckAreaMath.DeckDepth</c>), before this hull's own frame offset.</param>
        /// <param name="frameZ">The hull frame's world z — <see cref="IsoFacetHullRenderer"/>'s posed
        /// mesh, which carries the waterline calibration.</param>
        /// <param name="depthPerScreenRise">tan(elev): how much nearer the camera one metre up the
        /// screen is, so a standing body leans into the scene
        /// (<c>DeckAreaMath.DepthPerScreenRise</c>).</param>
        /// <param name="rollDegrees">The figure's lean, about the boots.</param>
        /// <param name="flipX">Mirror the sheet horizontally, as a SpriteRenderer would.</param>
        /// <param name="tint">Colour multiplier, as a SpriteRenderer's would be.</param>
        /// <param name="hullId">The carrying hull's facet id (1..255). 0 stands the occupant down —
        /// an unregistered hull composes nothing, so a figure wearing id 0 would simply vanish.</param>
        /// <param name="keyline">The hull's keyline colour, for the resolve's flood rule (gated off
        /// in the shipped style by ADR 0031, but the target is part of the MRT contract).</param>
        public void Show(Sprite sprite, Vector2 worldFeet, float hullFrameDepth, float frameZ,
                         float depthPerScreenRise, float rollDegrees, bool flipX, Color tint,
                         int hullId, Color keyline)
        {
            if (sprite == null || hullId <= 0 || sprite.texture == null) { Hide(); return; }

            EnsureRenderer();
            if (!RebuildIfNeeded(sprite)) { Hide(); return; }

            Sprite = sprite;
            FeetDepth = frameZ + hullFrameDepth;

            // World XY is the boots; world z only has to keep the bounds near the boat, because the
            // shader replaces it with the interpolated depth before projecting.
            transform.position = new Vector3(worldFeet.x, worldFeet.y, frameZ);
            transform.rotation = Quaternion.Euler(0f, 0f, rollDegrees);
            transform.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);

            _props ??= new MaterialPropertyBlock();
            _props.SetTexture(MainTexId, sprite.texture);
            _props.SetColor(ColorId, tint);
            _props.SetColor(KeyColorId, keyline);
            _props.SetFloat(HullIdId, hullId / 255f);
            _props.SetFloat(FeetZId, FeetDepth);
            _props.SetFloat(FeetYId, worldFeet.y);
            _props.SetFloat(DepthPerScreenRiseId, depthPerScreenRise);
            _renderer.SetPropertyBlock(_props);

            if (!_renderer.enabled) _renderer.enabled = true;
        }

        /// <summary>Stop drawing a figure (nobody aboard, a sprite hull, a torn-down rig). Idempotent,
        /// and it leaves the renderer in place: boarding is frequent and rebuilding the quad each time
        /// would be churn for nothing.</summary>
        public void Hide()
        {
            Sprite = null;
            if (_renderer != null && _renderer.enabled) _renderer.enabled = false;
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
                _material = null;
            }
            if (_quad != null)
            {
                if (Application.isPlaying) Destroy(_quad); else DestroyImmediate(_quad);
                _quad = null;
            }
        }

        private void EnsureRenderer()
        {
            if (_renderer != null) return;

            _filter = gameObject.GetComponent<MeshFilter>();
            if (_filter == null) _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                // Found by NAME, exactly as IsoFacetHullRenderer finds the facet and overlay shaders
                // beside it — so this shader shares their build-inclusion story rather than inventing
                // a second one. If those ever need listing in Always Included Shaders, this goes with
                // them; HullDeckSpriteShaderCompileGuardTests is what keeps it compiling meanwhile.
                Debug.LogError($"[HullDeckOccupant] {ShaderName} missing; the crew will fall back to " +
                               "drawing in-scene, un-occluded by the cabin they stand in.");
                _renderer.enabled = false;
                return;
            }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _renderer.sharedMaterial = _material;

            // The same housekeeping the facet mesh child gets: this draws only in the feature's own
            // off-screen list, never in the scene's lighting or shadow work.
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;
            _renderer.enabled = false;
        }

        /// <summary>Rebuild the quad if this sprite's GEOMETRY differs from the one it was built for.
        /// Frames of a walk cycle share a rect, a pivot and a PPU, so the common case is one compare
        /// and no work at all.</summary>
        private bool RebuildIfNeeded(Sprite sprite)
        {
            if (_material == null || _filter == null) return false;

            Rect rect = sprite.rect;
            Vector2 pivot = sprite.pivot;
            float ppu = sprite.pixelsPerUnit;
            if (rect.width <= 0f || rect.height <= 0f || ppu <= 0f) return false;

            if (_built && ReferenceEquals(sprite.texture, _builtTexture) && rect == _builtRect &&
                pivot == _builtPivot && Mathf.Approximately(ppu, _builtPixelsPerUnit))
                return true;

            float texW = sprite.texture.width;
            float texH = sprite.texture.height;
            if (texW <= 0f || texH <= 0f) return false;

            // The card, in metres, with the ORIGIN AT THE PIVOT — which for the character sheets is
            // ground contact. That is what makes the lean rotate about the boots and the placement a
            // plain "put the feet here", with no offset to keep in step.
            float left = -pivot.x / ppu;
            float right = (rect.width - pivot.x) / ppu;
            float bottom = -pivot.y / ppu;
            float top = (rect.height - pivot.y) / ppu;

            float u0 = rect.xMin / texW, u1 = rect.xMax / texW;
            float v0 = rect.yMin / texH, v1 = rect.yMax / texH;

            if (_quad == null)
                _quad = new Mesh { name = "HHHullDeckOccupantQuad", hideFlags = HideFlags.HideAndDontSave };
            _quad.Clear();
            _quad.SetVertices(new[]
            {
                new Vector3(left, bottom, 0f), new Vector3(right, bottom, 0f),
                new Vector3(right, top, 0f), new Vector3(left, top, 0f),
            });
            _quad.SetUVs(0, new[]
            {
                new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1),
            });
            _quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            _quad.RecalculateBounds();
            _filter.sharedMesh = _quad;

            _builtTexture = sprite.texture;
            _builtRect = rect;
            _builtPivot = pivot;
            _builtPixelsPerUnit = ppu;
            _built = true;
            return true;
        }
    }
}
