using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The DISPLACED water surface (ADR 0023 phase 2, step 1) — the sea as a real vertex-displaced
    /// mesh behind a dev A/B toggle, riding beside the flat <see cref="WaterSurface"/> on the same
    /// GameObject. This component owns the PLUMBING; the shading lives in the two passes of
    /// <c>HiddenHarboursWater.shader</c>:
    ///
    /// <list type="bullet">
    /// <item><b>The mesh children</b> — a grid of chunked meshes (default one vertex per
    /// <see cref="DisplacedWaterMath.DefaultGridPixels"/> screen pixels; rule 7) whose material is
    /// a runtime INSTANCE of the live Water.mat with the flat Universal2D pass disabled: the only
    /// pass left is <c>HHWater</c>, which nothing draws in-scene — the chunks exist solely for
    /// <see cref="IsoFacetHullFeature"/>'s off-screen water pass (the ADR 0022 pattern: private
    /// depth buffer, never the scene depth — a depth-writing mesh there punches holes in every
    /// later sprite). Chunks carry <see cref="DisplacedWaterRegistry.RenderingLayer"/> so the
    /// off-screen renderer list picks up exactly them and never the flat Sea sprite (whose
    /// material carries the same shader).</item>
    /// <item><b>The overlay quad</b> — the in-scene face: samples the feature's resolved
    /// <c>_HHWaterScreenTex</c> and sorts through a SortingGroup at the flat sprite's exact
    /// sorting slot ("sort as 2D" — mesh renderers do not sort against sprites on their own).</item>
    /// <item><b>The A/B toggle</b> — the owner's readability verdict instrument (the ADR 0022 dev
    /// A/B pattern: DevBoatPicker's V key, here <see cref="ToggleKey"/>). OFF is the contract:
    /// the flat water renders EXACTLY as today (this component registers nothing, the feature
    /// records nothing, zero cost). ON hides the flat sprite renderer and shows the displaced
    /// surface — same material values, same sim uniforms, same waterline.</item>
    /// </list>
    ///
    /// <para><b>Uniform plumbing (the ONE-SEA rule).</b> Every sim-driven uniform reaches the
    /// displaced chunks by COPYING the flat renderer's MaterialPropertyBlock each throttled tick —
    /// the exact block <see cref="WaterSurface"/> pushes (water level, flow, weather palette,
    /// height map…), so the two representations can never read different seas. On top of that copy
    /// this component sets only the displaced-pass inputs: the shared exaggeration (ADR 0023 §(2):
    /// default ×1.5) and the DERIVED shore-fade band
    /// (<see cref="DisplacedWaterMath.BandMeters"/> = coefficient × live envelope × exaggeration ×
    /// shore gradient — rule 6: derived, not free; the live envelope is read from the
    /// <c>_WaveFieldParams</c> global the WaveFieldBridge publishes). Both are OWNER DATA (arc
    /// step 3): the wired <see cref="GameConfig"/>'s <c>DisplacedWater</c> block is the live
    /// source, re-read every tick; the serialized fields are the unwired fallback.</para>
    ///
    /// <para><b>Seam discipline (rule 4/5).</b> Reads sim state only through the published shader
    /// globals and the flat renderer's block; drives no simulation; saves nothing. Presentation
    /// only — the walkable waterline, the clip contour and every physics read are untouched by
    /// construction (the fragment clips at the UNDISPLACED ground position; see the shader).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class DisplacedWaterSurface : MonoBehaviour
    {
        [Header("A/B toggle (the owner's readability verdict instrument)")]
        [Tooltip("Start with the DISPLACED surface showing? OFF (the default) = the flat water " +
                 "renders exactly as today — the safe A side. The dev key below flips at runtime.")]
        [SerializeField] private bool _displaced = false;
        [Tooltip("DEV A/B (ADR 0023): flip flat vs displaced water in place, same sim, same " +
                 "material. O for Ocean — free of every other binding (WASD/arrows helm, Space " +
                 "brace/haul, E interact, Q mooring, P buy, B sell, T trap-drop, G grant, H haul, " +
                 "Y auto-yaw, F fleet, V hull variant, L spotlight, C camera).")]
        [SerializeField] private Key _toggleKey = Key.O;

        [Header("Mesh density (rule 7 — the ADR 0023 perf envelope)")]
        [Tooltip("Vertex grid pitch in SCREEN pixels. 8 is the production start (the spike proved " +
                 "4 px affordable on desktop); raise it if a low-end machine needs slack, lower it " +
                 "only if crest silhouettes visibly facet.")]
        [Range(2, 64)] [SerializeField] private int _gridPixels = DisplacedWaterMath.DefaultGridPixels;
        [Tooltip("The project's pixels-per-unit (world metres are PPU pixels). 32 everywhere.")]
        [Min(1f)] [SerializeField] private float _pixelsPerUnit = 32f;
        [Tooltip("World rect the displaced mesh covers (centre + size) — the same rect as the flat " +
                 "water plane / the height map. The builder sets this.")]
        [SerializeField] private Vector2 _meshWorldCenter = Vector2.zero;
        [SerializeField] private Vector2 _meshWorldSize = new Vector2(160f, 120f);
        [Tooltip("Padding (m) on the overlay quad and the chunk culling bounds so lifted crests " +
                 "near an edge are neither culled nor cropped. Must exceed the largest possible " +
                 "lift (envelope × exaggeration ≈ 1.6 m at the reference sea).")]
        [Min(0f)] [SerializeField] private float _overlayPadMeters = 4f;

        [Header("GameConfig (ADR 0023 arc step 3 — the owner's tuning surface)")]
        [Tooltip("The shared GameConfig (Data/Config/GameConfig.asset; the builders wire this). " +
                 "When wired, its Displaced Water block is the LIVE source for the exaggeration and " +
                 "the band coefficient — read every throttled tick, so tuning the config asset in " +
                 "Play moves the sea within a second, no code (rule 6). Left null (EditMode tests, " +
                 "hand scenes) the serialized fallbacks below apply; they mirror the config defaults.")]
        [SerializeField] private GameConfig _config;

        [Header("Seam + exaggeration (fallbacks — GameConfig.DisplacedWater overrides when wired)")]
        [Tooltip("The SHARED displacement exaggeration (ADR 0023 §(2)): ×1.5 is the readability " +
                 "sweet spot (×1 = sim-true, ×3 breaks the iso framing). Phase 3 hands this SAME " +
                 "value to hull heave — never retune one consumer alone (the overlay-pose lesson). " +
                 "FALLBACK: with a GameConfig wired above, GameConfig.DisplacedWater.WaveExaggeration " +
                 "is the live source and this field is ignored.")]
        [Min(0f)] [SerializeField] private float _exaggeration = 1.5f;
        [Tooltip("Safety coefficient of the DERIVED shore-fade band (band = coeff × envelope × " +
                 "exaggeration × gradient). 2 covers both analytic tear hazards with margin " +
                 "(ShoreFadeMath.RecommendedBandCoefficient — the proven Core value). FALLBACK: with " +
                 "a GameConfig wired above, GameConfig.DisplacedWater.ShoreBandCoefficient is the " +
                 "live source and this field is ignored.")]
        [Min(0f)] [SerializeField] private float _bandCoefficient = ShoreFadeMath.RecommendedBandCoefficient;
        [Tooltip("Largest seabed gradient |∇elevation| (m per m) within the shallow band on this " +
                 "coast — how steeply the shore shelves. St Peters' steepest shelf ≈ 0.5. " +
                 "Overestimating only widens the fade band (safe); underestimating risks tear.")]
        [Min(0f)] [SerializeField] private float _maxShoreGradient = 0.5f;

        [Header("Refresh")]
        [Tooltip("How often (Hz) the uniform copy + band derivation runs. Matches WaterSurface's " +
                 "own throttled cadence; the sea is slow.")]
        [Min(0.5f)] [SerializeField] private float _refreshHz = 8f;

        [Header("Wiring (the builder sets this)")]
        [Tooltip("The WaterOverlay material (HiddenHarbours/WaterOverlay) for the in-scene quad. " +
                 "Auto-found by Shader.Find when left empty.")]
        [SerializeField] private Material _overlayMaterial;

        // --- cached shader ids (no per-tick string lookups — rule 7) ---
        private static readonly int IdWaveExaggeration = Shader.PropertyToID("_WaveExaggeration");
        private static readonly int IdShoreFadeBand    = Shader.PropertyToID("_ShoreFadeBand");
        private static readonly int IdWaveFieldParams  = Shader.PropertyToID("_WaveFieldParams");
        private static readonly int IdWaterIsoDepth    = Shader.PropertyToID("_WaterIsoDepth");
        private static readonly int IdHeightWorldMin   = Shader.PropertyToID("_HeightWorldMin");

        private Renderer _flatRenderer;
        private MaterialPropertyBlock _mpb;
        private Material _displacedMaterial;
        private Material _runtimeOverlayMaterial;   // only when _overlayMaterial was left empty
        private GameObject _meshRoot;
        private GameObject _overlayGo;
        private readonly List<MeshRenderer> _chunkRenderers = new List<MeshRenderer>();
        private readonly List<Mesh> _chunkMeshes = new List<Mesh>();
        private Mesh _overlayQuad;
        private bool _built;
        private bool _registered;
        private float _timer;

        /// <summary>Is the displaced surface currently showing (the B side of the A/B)?</summary>
        public bool Displaced => _displaced;

        /// <summary>The dev toggle key (diagnostics/tests).</summary>
        public Key ToggleKey => _toggleKey;

        /// <summary>Defaults pinned by the tests against the ADR (grid 8 px, exaggeration ×1.5,
        /// coefficient = the Core constant).</summary>
        public int GridPixels => _gridPixels;

        /// <summary>
        /// The EFFECTIVE shared exaggeration (ADR 0023 §(2)) — the value <see cref="SyncUniforms"/>
        /// pushes: the wired GameConfig's <c>DisplacedWater.WaveExaggeration</c> (the owner's live
        /// tuning surface, arc step 3), or the serialized fallback when no config is wired. Phase 3's
        /// hull heave reads the SAME config accessor (<see cref="GameConfig.WaveExaggeration"/>).
        /// </summary>
        public float Exaggeration => _config != null ? _config.DisplacedWater.WaveExaggeration : _exaggeration;

        /// <summary>The EFFECTIVE band coefficient — config-sourced like <see cref="Exaggeration"/>.</summary>
        public float BandCoefficient => _config != null ? _config.DisplacedWater.ShoreBandCoefficient : _bandCoefficient;

        /// <summary>
        /// Wire the surface in one call — the builder's path (mirrors WaterSurface's Configure
        /// convention, so a scene re-build gives a working A/B with no Inspector work). Unity-
        /// generic args only (rule 4).
        /// </summary>
        public void Configure(Vector2 meshWorldCenter, Vector2 meshWorldSize, Material overlayMaterial)
        {
            _meshWorldCenter = meshWorldCenter;
            _meshWorldSize = meshWorldSize;
            _overlayMaterial = overlayMaterial;
        }

        /// <summary>
        /// The arc-step-3 builder path: <see cref="Configure(Vector2, Vector2, Material)"/> plus the
        /// shared <see cref="GameConfig"/> whose <c>DisplacedWater</c> block becomes the LIVE source
        /// for exaggeration + band coefficient (Core type — rule 4's sanctioned direction).
        /// </summary>
        public void Configure(Vector2 meshWorldCenter, Vector2 meshWorldSize, Material overlayMaterial,
                              GameConfig config)
        {
            Configure(meshWorldCenter, meshWorldSize, overlayMaterial);
            _config = config;
        }

        private void Awake()
        {
            _flatRenderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;   // the A/B is a Play instrument; edit mode is inert
            if (_displaced) Activate();
        }

        private void OnDisable()
        {
            DisplacedSea.Clear(this);
            if (_registered)
            {
                DisplacedWaterRegistry.Unregister(this);
                _registered = false;
            }
            // Never leave the sea invisible: whatever state the toggle was in, the flat water
            // comes back when this component goes away (the "flat keeps working" contract).
            if (_flatRenderer != null) _flatRenderer.enabled = true;
            if (_meshRoot != null) _meshRoot.SetActive(false);
            if (_overlayGo != null) _overlayGo.SetActive(false);
        }

        private void OnDestroy() => ReleaseOwned();

        private void Update()
        {
            if (!Application.isPlaying) return;

            var kb = Keyboard.current;   // New Input System ONLY (legacy Input throws here)
            if (kb != null && kb[_toggleKey].wasPressedThisFrame)
                SetDisplaced(!_displaced);

            if (!_displaced) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;
            SyncUniforms();
        }

        /// <summary>
        /// Flip the A/B (public so tests and dev tooling can drive it without the input loop).
        /// OFF restores the flat water exactly: sprite renderer back on, children off, registry
        /// empty — the feature then records nothing and today's path runs untouched.
        /// </summary>
        public void SetDisplaced(bool displaced)
        {
            _displaced = displaced;
            if (!Application.isPlaying) return;
            if (displaced) Activate();
            else Deactivate();
            EventBus.Publish(new DevNotice(displaced
                ? "Water → DISPLACED surface (ADR 0023). Press " + _toggleKey + " to flip back."
                : "Water → flat (today's look)."));
        }

        private void Activate()
        {
            EnsureBuilt();
            if (!_built) { _displaced = false; return; }
            SyncUniforms();
            _meshRoot.SetActive(true);
            _overlayGo.SetActive(true);
            _flatRenderer.enabled = false;
            if (!_registered)
            {
                DisplacedWaterRegistry.Register(this);
                _registered = true;
            }
        }

        private void Deactivate()
        {
            // The OFF contract, boats included: no state ⇒ no ride, no resting draft — the flat
            // water AND the fleet render exactly as before ADR 0023 phase 3.
            DisplacedSea.Clear(this);
            if (_registered)
            {
                DisplacedWaterRegistry.Unregister(this);
                _registered = false;
            }
            if (_meshRoot != null) _meshRoot.SetActive(false);
            if (_overlayGo != null) _overlayGo.SetActive(false);
            if (_flatRenderer != null) _flatRenderer.enabled = true;
        }

        // ---- uniform plumbing ------------------------------------------------------------------

        /// <summary>
        /// Copy the flat renderer's live MaterialPropertyBlock onto every chunk (the SAME sim
        /// uniforms WaterSurface pushes — one sea, two representations), add the displaced-pass
        /// inputs (shared exaggeration + the DERIVED band), and keep the runtime material instance
        /// tracking the owner's live Water.mat so his in-play tuning drives both sides of the A/B.
        /// Allocation-free per tick (the block and lists are cached).
        /// </summary>
        private void SyncUniforms()
        {
            if (_flatRenderer == null || !_built) return;

            // Track the owner's live material: values first (cheap native copy), then re-disable
            // the in-scene pass (belt and braces — property copies must never re-enable it).
            Material live = _flatRenderer.sharedMaterial;
            if (live != null && _displacedMaterial != null)
            {
                _displacedMaterial.CopyPropertiesFromMaterial(live);
                _displacedMaterial.SetShaderPassEnabled("Universal2D", false);
            }

            _flatRenderer.GetPropertyBlock(_mpb);
            // The DERIVED band (rule 6): coefficient × LIVE envelope × exaggeration × gradient.
            // Envelope comes from the same _WaveFieldParams global the shader itself reads
            // (WaveFieldBridge publishes it; 0 in edit mode/no-field scenes → band 0, and with no
            // trains the height is 0 too, so the surface is simply flat). Exaggeration + coefficient
            // resolve through the properties — the wired GameConfig's DisplacedWater block (the
            // owner's live tuning surface, arc step 3) or the serialized fallbacks — re-read EVERY
            // tick, so an in-Play config edit reaches the sea within one refresh.
            float exaggeration = Exaggeration;
            float envelope = Shader.GetGlobalVector(IdWaveFieldParams).z;
            float band = DisplacedWaterMath.BandMeters(envelope, exaggeration, _maxShoreGradient,
                                                       BandCoefficient);
            _mpb.SetFloat(IdWaveExaggeration, exaggeration);
            _mpb.SetFloat(IdShoreFadeBand, band);
            for (int i = 0; i < _chunkRenderers.Count; i++)
                _chunkRenderers[i].SetPropertyBlock(_mpb);

            // ADR 0023 phase 3 step 2 — the SHARED HEAVE: publish the EXACT values pushed to the
            // vertex stage above through the Core seam, so boat heave rides the same exaggeration
            // and the same shore fade as the surface it is drawn on (re-published every tick — a
            // live config edit reaches the boats within one refresh, never a stale copy).
            DisplacedSea.Publish(this, new DisplacedSeaState(exaggeration, band));

            PublishIsoDepthFrame(exaggeration);
        }

        /// <summary>
        /// Publish the calibrated iso-depth frame the mesh hulls translate into while the
        /// displaced sea is live (ADR 0023 phase 3 — the waterline's cross-object z convention).
        /// Every value is read from the SAME source the water shader itself samples — the copied
        /// property block first (production pushes <c>_HeightWorldMin</c> there), the live
        /// material else — so hull and water cannot disagree by construction. Re-published each
        /// throttled tick; cleared by the registry when this surface unregisters.
        /// <paramref name="exaggeration"/> is the tick's EFFECTIVE exaggeration — the exact value
        /// pushed to the vertex stage above, never a re-read — so the watertight clamp bounds the
        /// same lift the shader draws (the see-what-you-clamp discipline).
        /// </summary>
        private void PublishIsoDepthFrame(float exaggeration)
        {
            if (_displacedMaterial == null) return;
            Vector4 iso = _displacedMaterial.GetVector(IdWaterIsoDepth);
            Vector4 heightMin = _mpb.HasVector(IdHeightWorldMin)
                ? _mpb.GetVector(IdHeightWorldMin)
                : _displacedMaterial.GetVector(IdHeightWorldMin);
            // The chunk vertices rest at this transform's world z (local z 0 under the mesh root).
            var frame = new WaterIsoDepthFrame(heightMin.y, iso.x, iso.y, transform.position.z,
                                               exaggeration);
            DisplacedWaterRegistry.PublishIsoDepthFrame(this, in frame);
        }

        // ---- construction ----------------------------------------------------------------------

        private void EnsureBuilt()
        {
            if (_built) return;
            if (_flatRenderer == null) _flatRenderer = GetComponent<Renderer>();
            Material live = _flatRenderer != null ? _flatRenderer.sharedMaterial : null;
            if (live == null || !live.shader.name.Contains("HiddenHarbours/Water"))
            {
                Debug.LogWarning("[DisplacedWaterSurface] The flat renderer does not carry the " +
                                 "HiddenHarbours/Water material — displaced surface unavailable.");
                return;
            }

            // The displaced material: the LIVE Water.mat's values and keywords (so every painted-
            // texture variant the owner enabled stays enabled), minus the in-scene pass. Only the
            // HHWater pass remains, and only the feature's off-screen renderer list draws that.
            _displacedMaterial = new Material(live)
            {
                name = live.name + " (Displaced)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _displacedMaterial.SetShaderPassEnabled("Universal2D", false);

            BuildChunks();
            BuildOverlay(live);
            _built = _meshRoot != null && _overlayGo != null;
        }

        /// <summary>
        /// Build the chunked vertex grid. Vertices are authored in WORLD-METRE offsets from this
        /// transform's position (all chunks at local origin with the parent's scale compensated),
        /// so a scaled Sea plane cannot distort the grid; chunk borders share exact vertex
        /// positions by construction (one global grid, indexed per chunk) so the surface is
        /// crack-free. Sizing math is <see cref="DisplacedWaterMath"/> — pinned headless.
        /// </summary>
        private void BuildChunks()
        {
            float cell = DisplacedWaterMath.CellMeters(_gridPixels, _pixelsPerUnit);
            int cellsX = DisplacedWaterMath.CellCount(_meshWorldSize.x, cell);
            int cellsY = DisplacedWaterMath.CellCount(_meshWorldSize.y, cell);
            int chunksX = DisplacedWaterMath.ChunkCount(cellsX, DisplacedWaterMath.MaxChunkCells);
            int chunksY = DisplacedWaterMath.ChunkCount(cellsY, DisplacedWaterMath.MaxChunkCells);

            _meshRoot = new GameObject("DisplacedWaterMesh") { hideFlags = HideFlags.DontSave };
            _meshRoot.transform.SetParent(transform, false);
            _meshRoot.transform.localScale = InverseScale(transform.lossyScale);

            Vector2 min = _meshWorldCenter - _meshWorldSize * 0.5f;
            Vector3 origin = transform.position;

            for (int cy = 0; cy < chunksY; cy++)
            for (int cx = 0; cx < chunksX; cx++)
            {
                int chunkCellsX = DisplacedWaterMath.ChunkCells(cellsX, DisplacedWaterMath.MaxChunkCells, cx);
                int chunkCellsY = DisplacedWaterMath.ChunkCells(cellsY, DisplacedWaterMath.MaxChunkCells, cy);
                float x0 = min.x + cx * DisplacedWaterMath.MaxChunkCells * cell;
                float y0 = min.y + cy * DisplacedWaterMath.MaxChunkCells * cell;

                var mesh = BuildChunkMesh(x0, y0, chunkCellsX, chunkCellsY, cell, origin);
                _chunkMeshes.Add(mesh);

                var go = new GameObject($"Chunk_{cx}_{cy}") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(_meshRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _displacedMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.allowOcclusionWhenDynamic = false;
                // Membership in the off-screen water pass is THIS bit (see DisplacedWaterRegistry).
                mr.renderingLayerMask = DisplacedWaterRegistry.RenderingLayer;
                _chunkRenderers.Add(mr);
            }

            _meshRoot.SetActive(false);
        }

        private Mesh BuildChunkMesh(float x0, float y0, int cellsX, int cellsY, float cell, Vector3 origin)
        {
            int vx = cellsX + 1, vy = cellsY + 1;
            var verts = new Vector3[vx * vy];
            for (int y = 0; y < vy; y++)
            for (int x = 0; x < vx; x++)
                verts[y * vx + x] = new Vector3(x0 + x * cell - origin.x, y0 + y * cell - origin.y, 0f);

            var tris = new int[DisplacedWaterMath.ChunkIndexCount(cellsX, cellsY)];
            int t = 0;
            for (int y = 0; y < cellsY; y++)
            for (int x = 0; x < cellsX; x++)
            {
                int i0 = y * vx + x;
                tris[t++] = i0; tris[t++] = i0 + vx; tris[t++] = i0 + vx + 1;
                tris[t++] = i0; tris[t++] = i0 + vx + 1; tris[t++] = i0 + 1;
            }

            var mesh = new Mesh { name = "HHWaterChunk", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, false);
            // Pad the culling bounds so a crest lifted above the chunk's flat rect is not culled
            // at the screen edge (the vertex stage moves geometry the culler cannot see).
            // Bounds.Expand grows the SIZE by the amount (half per side), so pad ×2 to guarantee
            // a full _overlayPadMeters on every side — comfortably past the largest possible lift.
            Bounds b = new Bounds(
                new Vector3(x0 + cellsX * cell * 0.5f - origin.x, y0 + cellsY * cell * 0.5f - origin.y, 0f),
                new Vector3(cellsX * cell, cellsY * cell, 0f));
            b.Expand(2f * _overlayPadMeters);
            mesh.bounds = b;
            return mesh;
        }

        /// <summary>
        /// The in-scene overlay quad: covers the water rect (padded so lifted crests near the top
        /// edge stay inside it) and sorts through a SortingGroup at the EXACT slot the flat sprite
        /// occupies — boats, characters and props stack against the displaced sea exactly as they
        /// stack against the flat one.
        /// </summary>
        private void BuildOverlay(Material live)
        {
            Material overlayMat = _overlayMaterial;
            if (overlayMat == null)
            {
                var shader = Shader.Find("HiddenHarbours/WaterOverlay");
                if (shader == null)
                {
                    Debug.LogWarning("[DisplacedWaterSurface] HiddenHarbours/WaterOverlay shader " +
                                     "missing — displaced surface unavailable.");
                    return;
                }
                _runtimeOverlayMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                overlayMat = _runtimeOverlayMaterial;
            }

            Vector3 origin = transform.position;
            Vector2 min = _meshWorldCenter - _meshWorldSize * 0.5f - Vector2.one * _overlayPadMeters;
            Vector2 max = _meshWorldCenter + _meshWorldSize * 0.5f + Vector2.one * _overlayPadMeters;

            _overlayQuad = new Mesh { name = "HHWaterOverlayQuad", hideFlags = HideFlags.HideAndDontSave };
            _overlayQuad.SetVertices(new[]
            {
                new Vector3(min.x - origin.x, min.y - origin.y, 0f),
                new Vector3(max.x - origin.x, min.y - origin.y, 0f),
                new Vector3(max.x - origin.x, max.y - origin.y, 0f),
                new Vector3(min.x - origin.x, max.y - origin.y, 0f),
            });
            _overlayQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            _overlayGo = new GameObject("WaterOverlay") { hideFlags = HideFlags.DontSave };
            _overlayGo.transform.SetParent(transform, false);
            _overlayGo.transform.localScale = InverseScale(transform.lossyScale);
            _overlayGo.AddComponent<MeshFilter>().sharedMesh = _overlayQuad;
            var mr = _overlayGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = overlayMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;

            // Sort exactly where the flat water sorts. Mesh renderers do not compete with sprites
            // by sortingOrder on their own — the SortingGroup ("sort as 2D") is the documented
            // workaround, same as mesh hulls and the validator's rule.
            var group = _overlayGo.AddComponent<SortingGroup>();
            group.sortingLayerID = _flatRenderer.sortingLayerID;
            group.sortingOrder = _flatRenderer.sortingOrder;
            mr.sortingLayerID = _flatRenderer.sortingLayerID;
            mr.sortingOrder = _flatRenderer.sortingOrder;

            _overlayGo.SetActive(false);
        }

        private static Vector3 InverseScale(Vector3 s) => new Vector3(
            Mathf.Abs(s.x) > 1e-5f ? 1f / s.x : 1f,
            Mathf.Abs(s.y) > 1e-5f ? 1f / s.y : 1f,
            Mathf.Abs(s.z) > 1e-5f ? 1f / s.z : 1f);

        private void ReleaseOwned()
        {
            static void Kill(Object o)
            {
                if (o == null) return;
                if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
            }

            DisplacedSea.Clear(this);
            if (_registered)
            {
                DisplacedWaterRegistry.Unregister(this);
                _registered = false;
            }
            if (_meshRoot != null) Kill(_meshRoot);
            if (_overlayGo != null) Kill(_overlayGo);
            foreach (var m in _chunkMeshes) Kill(m);
            _chunkMeshes.Clear();
            _chunkRenderers.Clear();
            Kill(_overlayQuad);
            Kill(_displacedMaterial);
            Kill(_runtimeOverlayMaterial);
            _meshRoot = null;
            _overlayGo = null;
            _overlayQuad = null;
            _displacedMaterial = null;
            _runtimeOverlayMaterial = null;
            _built = false;
        }
    }
}
