using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Makes ONE renderer reflect in the water (ADR 0027 #8). It does three things and nothing else:
    ///
    /// <list type="number">
    /// <item><b>Sets the rendering-layer bit</b> (<see cref="ReflectionRegistry.RenderingLayer"/>)
    /// that admits this renderer to the <c>HHReflect</c> filtered list. Membership is EXPLICIT: the
    /// shader tag alone would sweep in every renderer sharing that shader — the trap ADR 0023 hit
    /// when the flat Sea sprite shared the water shader.</item>
    /// <item><b>Publishes the MIRROR AXIS</b> — this renderer's ground-contact pivot in world space
    /// (ADR 0026) — into its MaterialPropertyBlock as <c>_HHReflectOrigin</c>.</item>
    /// <item><b>Registers</b> with <see cref="ReflectionRegistry"/> so the renderer feature knows
    /// whether to enqueue anything at all (the zero-cost-when-idle gate).</item>
    /// </list>
    ///
    /// <para>🔴 <b>Why the axis has to be published.</b> Unity submits SpriteRenderer meshes with
    /// their vertices ALREADY IN WORLD SPACE and an IDENTITY object-to-world matrix, so the object
    /// origin reads <c>(0,0)</c> for every sprite in the scene. A shader that mirrors about "the
    /// object origin" therefore reflects every sprite about the world origin — not a subtle error, a
    /// total one. This repo measured it on 2026-07-29 in the tree lane (a 3 m-range lamp lit three
    /// trees equally), and the facet renderer learned it earlier with <c>_HullOrigin</c>. It is not
    /// an edge case; it is every sprite.</para>
    ///
    /// <para><b>The pivot, by renderer kind.</b> A SpriteRenderer's transform position IS its
    /// ground-contact pivot under the ADR 0026 convention (a tree's pivot is its trunk foot, a
    /// building's is its base) — so the default is <c>transform.position</c>, with an optional
    /// authored offset for art whose pivot is not on the contact point. For a mesh hull the axis is
    /// the ADR 0023 calibrated iso-depth waterplane, which is where that hull actually meets the
    /// sea; wire <see cref="_useWaterLevel"/> and the axis tracks the live tide instead of the
    /// transform.</para>
    ///
    /// <para><b>The distance gate</b> is the bounded-set rule's second half: a reflector whose pivot
    /// sits more than <see cref="_maxHeightAboveWater"/> above the water level is not near enough to
    /// the sea to reflect in it (a tree on a clifftop). Checked on enable and on a throttled tick —
    /// never per frame (rule 7).</para>
    ///
    /// <para>Visual only: it writes a rendering-layer bit and shader properties. No sim, no save
    /// (rule 5); every number is a serialized field, never a literal (rule 6).</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    public sealed class ReflectiveObject : MonoBehaviour
    {
        [Tooltip("Offset from this transform to the GROUND-CONTACT pivot the reflection mirrors " +
                 "about (ADR 0026). Leave at zero when the art's own pivot is already the contact " +
                 "point, which is the convention for the tree and building rigs.")]
        [SerializeField] private Vector2 _pivotOffset = Vector2.zero;

        [Tooltip("Take the mirror axis from the LIVE water level rather than this transform — the " +
                 "ADR 0023 calibrated waterplane, for anything that floats. A moored boat's " +
                 "reflection then meets her hull at the waterline as the tide moves.")]
        [SerializeField] private bool _useWaterLevel = false;

        [Tooltip("How far above the water level this reflector's pivot may sit and still reflect " +
                 "(m). The bounded-set rule's distance half: a clifftop tree does not reflect in " +
                 "the harbour below it.")]
        [SerializeField] private float _maxHeightAboveWater = 3f;

        [Tooltip("This source is LIGHT content at night (a lit wheelhouse, a lamp). Its reflection " +
                 "is pre-compensated for the day/night multiply and rides the post-grade bucket, " +
                 "exactly as the moon glitter does — otherwise the overlay crushes it to ~3%.")]
        [SerializeField] private bool _nightLitSource = false;

        [Tooltip("Seconds between distance-gate re-checks. Never per frame (rule 7); the tide moves " +
                 "in minutes, not milliseconds.")]
        [SerializeField] private float _gateInterval = 1f;

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private bool _registered;
        private float _timer;
        private uint _layerBeforeUs;
        private bool _layerCaptured;

        /// <summary>The world-space ground-contact pivot this renderer mirrors about — the value
        /// that reaches <c>_HHReflectOrigin</c>. Public so tests assert the axis rather than the
        /// fields that feed it.</summary>
        public Vector2 MirrorPivot
        {
            get
            {
                Vector3 p = transform.position;
                var pivot = new Vector2(p.x + _pivotOffset.x, p.y + _pivotOffset.y);
                if (_useWaterLevel) pivot.y = WaterLevel();
                return pivot;
            }
        }

        /// <summary>
        /// Whether this reflector currently passes the distance gate — i.e. whether the GROUND it
        /// stands on is near enough to the sea for it to reflect in it.
        ///
        /// <para>⚠️ <b>It reads terrain ELEVATION, not world Y.</b> In a top-down game world Y is a
        /// ground-plane coordinate (north), not height — the art fakes height by drawing UP the
        /// screen. Gating on <c>transform.position.y</c> would exclude everything at the north end
        /// of the map and admit every clifftop in the south, which is not a subtle error. Height
        /// above water is <c>ITidalTerrain.ElevationAt(pivot) − waterLevel</c>, the same read every
        /// other consumer of the one height map makes (§4). The mirror AXIS is a different quantity
        /// and correctly stays world Y — see <see cref="MirrorPivot"/>.</para>
        /// </summary>
        public bool WithinDistanceGate =>
            GroundElevation() - WaterLevel() <= Mathf.Max(_maxHeightAboveWater, 0f);

        /// <summary>Is this source pre-compensated light content (§11.6)?</summary>
        public bool NightLitSource => _nightLitSource;

        private void OnEnable()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            _timer = 0f;
            Refresh();
        }

        private void OnDisable() => Leave();
        private void OnValidate()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (isActiveAndEnabled) Refresh();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                // EDIT MODE: OnValidate does not fire for a transform MOVE, so without this a
                // dragged reflector keeps mirroring about where it used to stand. Refresh only when
                // the position ACTUALLY changed — a Vector3 compare per editor frame, never a
                // per-frame property-block write (rule 7). Same shape as TreeTrunkAnchor.
                Vector3 here = transform.position;
                if ((here - _publishedPosition).sqrMagnitude <= 1e-10f) return;
                _publishedPosition = here;
                Refresh();
#endif
                return;
            }

            // PLAY: the gate re-check is throttled. The tide moves in minutes and reflectors are
            // static scenery or a slow boat, so a per-frame test would be pure waste (rule 7).
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(_gateInterval, 0.05f);
            Refresh();
        }

#if UNITY_EDITOR
        private Vector3 _publishedPosition = new Vector3(float.NaN, float.NaN, float.NaN);
#endif

        /// <summary>
        /// Bring this renderer's reflection state up to date: join or leave the set by the distance
        /// gate, and re-publish the mirror axis. Cheap and idempotent — it writes the property block
        /// only while the reflector is IN the set.
        /// </summary>
        public void Refresh()
        {
            if (_renderer == null) return;
            if (!isActiveAndEnabled || !WithinDistanceGate) { Leave(); return; }

            if (!_layerCaptured)
            {
                _layerBeforeUs = _renderer.renderingLayerMask;
                _layerCaptured = true;
            }
            _renderer.renderingLayerMask = _layerBeforeUs | ReflectionRegistry.RenderingLayer;

            _block ??= new MaterialPropertyBlock();
            // GET first, then modify, then SET — a SpriteRenderer keeps its own per-renderer
            // overrides (the sprite texture among them) in this same block, and handing it a FRESH
            // one drops them. Same lesson as TreeTrunkAnchor.
            _renderer.GetPropertyBlock(_block);
            Vector2 pivot = MirrorPivot;
            // w = 1 says "published", which is what lets the shader tell a real pivot from the
            // (0,0,0,0) a material default hands it — and refuse to mirror rather than mirror about
            // the world origin.
            _block.SetVector(ReflectionShaderIds.ReflectOrigin, new Vector4(pivot.x, pivot.y, 0f, 1f));
            _block.SetFloat(ReflectionShaderIds.ReflectLit, _nightLitSource ? 1f : 0f);
            _renderer.SetPropertyBlock(_block);

            if (!_registered)
            {
                ReflectionRegistry.Register(this);
                _registered = true;
            }
        }

        private void Leave()
        {
            if (_registered)
            {
                ReflectionRegistry.Unregister(this);
                _registered = false;
            }
            // Put the renderer's layer mask back exactly as we found it. Clearing our bit blindly
            // would be wrong if the scene legitimately uses 1 << 29 for something else later.
            if (_renderer != null && _layerCaptured)
                _renderer.renderingLayerMask = _layerBeforeUs;
        }

        /// <summary>
        /// The water level to gate and (optionally) mirror against. Prefers the live deterministic
        /// tide; falls back to 0 when no environment service is up (edit mode / a bare art scene),
        /// which is the same "unset" convention the water shader's own fallbacks use. READ-only —
        /// this never writes the sim (rule 5).
        /// </summary>
        private static float WaterLevel()
        {
            var env = GameServices.Environment;
            if (env == null) return 0f;
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            return env.WaterLevelAt(now);
        }

        /// <summary>
        /// The authored ground elevation under this reflector, in metres above chart datum — the
        /// SAME one height map the water's depth read and the walkability check consume (§4, the
        /// one-map/three-consumers rule), so "is it near the water" cannot drift from "is it wet".
        /// Falls back to 0 with no terrain service (edit mode / a bare art scene), which puts a
        /// bare-scene reflector level with the default water and inside the gate — the sane preview.
        /// </summary>
        private float GroundElevation()
        {
            var terrain = GameServices.TidalTerrain;
            if (terrain == null) return 0f;
            Vector3 p = transform.position;
            return terrain.ElevationAt(new Vector2(p.x, p.y));
        }

        /// <summary>
        /// Read back the mirror axis this renderer will actually hand the shader, or <c>w = 0</c>
        /// when nothing published one. Asserted against the RENDERER rather than the fields that fed
        /// it, because "the pivot reached the block" is exactly what silently does not happen when a
        /// shader tries to read it from <c>unity_ObjectToWorld</c>.
        /// </summary>
        public static Vector4 OriginOn(Renderer renderer)
        {
            if (renderer == null || !renderer.HasPropertyBlock()) return Vector4.zero;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.HasVector(ReflectionShaderIds.ReflectOrigin)
                ? block.GetVector(ReflectionShaderIds.ReflectOrigin)
                : Vector4.zero;
        }

        /// <summary>True when this renderer carries the bit that admits it to the reflection list.</summary>
        public static bool IsInReflectionLayer(Renderer renderer)
        {
            return renderer != null &&
                   (renderer.renderingLayerMask & ReflectionRegistry.RenderingLayer) != 0u;
        }
    }
}
