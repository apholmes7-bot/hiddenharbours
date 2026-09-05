using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// A reusable, drop-on ADDITIVE 2D LIGHT (ADR 0016): the first concrete payoff of the day/night system —
    /// a stylized glow that CUTS THROUGH the dark night frame. Attach it to any object (a boat bow, a lamp
    /// post, a window) and it draws a soft CONE or RADIAL glow as an ADDITIVE quad ABOVE the day/night
    /// MULTIPLY overlay (ADR 0013), so it brightens the darkened frame — a lantern punching a hole in the
    /// dark. It AUTO-GATES to night: the <c>HiddenHarbours/AdditiveLight</c> shader reads the published
    /// <c>_DayNightTint</c> and scales the glow by the frame darkness, so a light is ~invisible at noon and
    /// full at night with ZERO per-light coupling to the cycle (this component just positions/orients/colours
    /// the quad and pushes the tunables; the gate is in the shader, mirrored + unit-tested in
    /// <see cref="LightMath"/>).
    ///
    /// <para><b>Why an additive quad, not a URP Light2D (ADR 0016).</b> The project's sprites are
    /// Sprite-UNLIT and night is a full-screen multiply overlay — a Light2D would do nothing. An additive
    /// quad drawn above the overlay (<c>sortingOrder</c> &gt; the overlay's ~32760) is the renderer-agnostic
    /// way to add brightness back. Visual-only: drives no sim, saves nothing (rule 5).</para>
    ///
    /// <para><b>Pooled, no per-frame alloc (rule 7).</b> ONE child quad (a <see cref="MeshRenderer"/> over a
    /// shared 1×1 mesh) is created ONCE and reused; the shape runs entirely in the shader; every light shares
    /// the one <c>Resources/AdditiveLight.mat</c> material via a <see cref="MaterialPropertyBlock"/>
    /// (GPU-batch friendly). The recompute (flicker) is on a throttled tick; the pose follows every frame.</para>
    ///
    /// <para><b>Mirrors the project's drop-on pattern</b> (<see cref="SpriteShadow"/> / <see cref="CottageDayNight"/>):
    /// no scene wiring beyond attaching it (or via the "Hidden Harbours ▸ Lighting ▸ Add Light to Selection"
    /// menu / the "Build Light Test" demo). <b>Determinism (rule 5):</b> the flicker is a deterministic hash
    /// of <c>(seed, time)</c> — never <see cref="System.Random"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLight : MonoBehaviour
    {
        /// <summary>The glow shape: a directional <see cref="Cone"/> beam or a round <see cref="Radial"/> halo.</summary>
        public enum LightShape { Cone, Radial }

        private const string LightMaterialPath = "AdditiveLight";          // Resources/AdditiveLight.mat
        private const string LightShaderName   = "HiddenHarbours/AdditiveLight";

        private static readonly int IdLightColor     = Shader.PropertyToID("_LightColor");
        private static readonly int IdIntensity      = Shader.PropertyToID("_Intensity");
        private static readonly int IdConeHalfAngle  = Shader.PropertyToID("_ConeHalfAngle");
        private static readonly int IdAngularSoft    = Shader.PropertyToID("_AngularSoftness");
        private static readonly int IdEdgeSoftness   = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int IdCoreBoost      = Shader.PropertyToID("_CoreBoost");
        private static readonly int IdGateThreshold  = Shader.PropertyToID("_GateThreshold");
        private static readonly int IdGateSoftness   = Shader.PropertyToID("_GateSoftness");
        private static readonly int IdGateFallback   = Shader.PropertyToID("_GateFallback");
        private static readonly int IdLampPos        = Shader.PropertyToID("_LampPos");
        private static readonly int IdThrow          = Shader.PropertyToID("_Throw");

        // ONE shared fallback material for the missing-Resources path, minted at most once across ALL lights
        // (the normal path loads Resources/AdditiveLight.mat). A per-instance material would leak + break the
        // shared-material batching this component relies on.
        private static Material _sharedFallbackMaterial;

        [Header("Shape")]
        [Tooltip("Cone = a directional BEAM (boat spotlight, torch). Radial = a round HALO (lantern, " +
                 "worklight, window glow). A cone with a 180 deg half-angle is effectively radial.")]
        [SerializeField] private LightShape _shape = LightShape.Cone;

        [Tooltip("Cone HALF-ANGLE in degrees: small = a tight beam, 180 = a full round glow. Ignored when " +
                 "Shape is Radial (treated as 180).")]
        [Range(0f, 180f)] [SerializeField] private float _coneHalfAngle = 30f;

        [Tooltip("How far the light throws (world units / metres) from its origin to the dark edge of the glow.")]
        [Min(0.01f)] [SerializeField] private float _range = 6f;

        [Header("Colour and strength")]
        [Tooltip("The light colour. A warm low-amber reads as a lantern/spotlight over the cold North-Atlantic " +
                 "night; a cool white reads as a worklight/LED. HDR allowed for a hot core.")]
        [ColorUsage(true, true)] [SerializeField] private Color _color = new Color(1f, 0.86f, 0.6f, 1f);

        [Tooltip("Master intensity multiplier. Higher = a brighter, further-reaching glow. The night-gate and " +
                 "flicker scale this down; daytime gates it to ~0.")]
        [Min(0f)] [SerializeField] private float _intensity = 1.2f;

        [Header("Softness")]
        [Tooltip("Radial edge softness: 0 = a fairly hard disc, 1 = a very soft halo that fades gently to the edge.")]
        [Range(0f, 1f)] [SerializeField] private float _edgeSoftness = 0.6f;

        [Tooltip("Angular edge softness of the cone (fraction of the half-angle the beam edge feathers over). " +
                 "0 = a hard-edged cone, 1 = a very soft-edged beam. Ignored for a radial.")]
        [Range(0f, 1f)] [SerializeField] private float _angularSoftness = 0.4f;

        [Tooltip("Bright core boost near the lamp so the source reads as a hot point. 0 = no extra core.")]
        [Range(0f, 4f)] [SerializeField] private float _coreBoost = 1f;

        [Header("Night gate (auto-fade with the day/night cycle)")]
        [Tooltip("Frame DARKNESS (0 = bright noon .. 1 = pitch black) at/below which the light is fully OFF, so " +
                 "it can't wash daytime out. The light fades in above this.")]
        [Range(0f, 1f)] [SerializeField] private float _gateThreshold = 0.12f;

        [Tooltip("Width of the fade-in band above the threshold. Small = a hard switch at dusk; wide = a slow " +
                 "ramp through twilight.")]
        [Range(0f, 1f)] [SerializeField] private float _gateSoftness = 0.35f;

        [Tooltip("What the light shows when the day/night cycle ISN'T running (EditMode / a bare art scene / " +
                 "the demo before Play): 1 = fully show (so you can see + tune the beam), 0 = hidden. Mirrors " +
                 "how the water shader treats an unset tint.")]
        [Range(0f, 1f)] [SerializeField] private float _gateFallback = 1f;

        [Header("Flicker (deterministic, optional)")]
        [Tooltip("Flicker amount: 0 = steady, up to 1 = a strong torch-like wobble. Deterministic (a hash of " +
                 "seed + time, never System.Random) — rule 5.")]
        [Range(0f, 1f)] [SerializeField] private float _flickerAmount = 0f;

        [Tooltip("Flicker speed multiplier. Higher = a faster, more nervous wobble.")]
        [Min(0f)] [SerializeField] private float _flickerSpeed = 1f;

        [Header("Render")]
        [Tooltip("Sorting order of the additive light quad. MUST be ABOVE the day/night overlay (~32760) so the " +
                 "light brightens the darkened frame instead of being darkened by it.")]
        [SerializeField] private int _sortingOrder = MaxSortingOrder;

        /// <summary>
        /// <b>The highest sorting order Unity can actually store</b> — and the reason this constant has
        /// to exist at all.
        ///
        /// <para><c>Renderer.sortingOrder</c> is a <b>signed 16-bit</b> field. The property is typed
        /// <c>int</c>, it accepts any int you hand it without a warning, and it then WRAPS: this light
        /// shipped asking for <b>32770</b> and the renderer stored <b>−32766</b> — 32770 − 65536 — which
        /// is not "above the day/night overlay" but below every sprite in the world, the sea and the
        /// seabed included. Measured in the running game on 2026-08-28: the quad's own renderer
        /// reported <c>order = −32766</c>.</para>
        ///
        /// <para><b>This is what ADR 0016's two failed "quad-sort fixes" were fighting.</b> The ADR
        /// concluded the URP 2D renderer would not composite an additive mesh over the custom-shader
        /// water and built an in-shader water term to get round it; the asymmetry it found most telling
        /// — that the day/night overlay DOES draw over the water — has a simpler explanation than depth
        /// ordering: the overlay asks for ~32760, which fits in a short, and the light asked for 32770,
        /// which does not. The in-shader water path stays exactly as it is (it is shipped, tuned, and it
        /// composes the beam with the foam and the reflections in a way a quad on top never could); this
        /// only makes the LAND quad land where its own doc always said it did.</para>
        /// </summary>
        public const int MaxSortingOrder = short.MaxValue;   // 32767

        [Tooltip("Depth (metres) IN FRONT of the active camera to place the light quad each frame, mirroring how " +
                 "the day/night overlay sits at the camera (so the light reliably composites ABOVE the world). " +
                 "This fixes the URP-2D mesh-vs-sprite ordering quirk where a light at the SAME world depth as a " +
                 "big water/ground SPRITE could be overdrawn by it despite a higher sorting order: pinning the " +
                 "quad's depth to the camera (like the overlay) plus the Sort-as-2D group below makes the high " +
                 "sorting order win against every sprite. The quad's X/Y still track the lamp in world space; " +
                 "only its DEPTH along the view axis is camera-relative. 0 = leave the quad at world depth (the " +
                 "old behaviour). The light is depth-test-Always + additive, so this never affects the look — " +
                 "only the compositing order.")]
        [Min(0f)] [SerializeField] private float _cameraDepthOffset = DefaultCameraDepthOffset;

        /// <summary>
        /// The shipped depth pin of a light quad (metres in front of the camera). Named so the
        /// lamp-shadow quads can sit strictly NEARER (<see cref="LampShadowSystem.ShadowDepthOffset"/>)
        /// and so draw AFTER the glow at the same sorting order — the tie the 2D renderer breaks
        /// back-to-front. A test pins the ordering of the three constants.
        /// </summary>
        public const float DefaultCameraDepthOffset = 0.1f;

        [Header("Cast shadows (ADR 0016, lights PR B)")]
        [Tooltip("Does this lamp throw shadows? A caster inside its range throws its silhouette AWAY " +
                 "from the lamp, at the lamp's own falloff there. Off = the glow alone, exactly as before.")]
        [SerializeField] private bool _castsShadows = true;

        [Tooltip("How high the lamp sits above the ground (or water) its casters stand on, metres. It is " +
                 "what makes a lamp not the sun: a low lamp throws a long rake behind a far caster, a high " +
                 "one a stub. The cast shadows read it, and so does the ground POOL — it is what shapes a " +
                 "pool instead of stamping a disc. The bloom is unchanged by it.")]
        [Min(0f)] [SerializeField] private float _lampHeightMeters = DefaultLampHeightMeters;

        [Tooltip("How far this lamp LIGHTS the ground, metres — its pool, which is NOT the same as Range. " +
                 "Range is the BLOOM: how big the lit fitting looks. This is the patch of ground the lamp " +
                 "makes brighter, drawn by LampPoolSystem. 0 = this lamp casts no pool (a boat's sidelight " +
                 "is a signal, not a floodlight). LightPresets.Apply sets it from ReachMetres; the " +
                 "searchlight sets it from its own throw.")]
        [Min(0f)] [SerializeField] private float _reachMetres;

        /// <summary>The height a lamp that never said (2.5 m — the searchlight's own shipped figure).</summary>
        public const float DefaultLampHeightMeters = 2.5f;

        [Tooltip("Local offset of the light ORIGIN from this transform (metres), e.g. push a boat spotlight to " +
                 "the bow. For a cone the beam is thrown along this transform's local UP (the boat heading).")]
        [SerializeField] private Vector2 _originOffset = Vector2.zero;

        [Tooltip("How often (Hz) the flicker/gate values are recomputed + pushed. The light is slow; a few Hz " +
                 "is plenty. The quad POSE follows every frame regardless.")]
        [Min(1f)] [SerializeField] private float _refreshHz = 20f;

        private Transform _quad;
        private MeshRenderer _quadRenderer;
        private UnityEngine.Rendering.SortingGroup _sortingGroup;   // Sort-as-2D so the mesh beats sprites by order
        private MaterialPropertyBlock _mpb;
        private int _flickerSeed;
        private float _timer;
        private static Mesh _sharedMesh;

        // ---- public surface (so BoatSpotlight / tools drive a light without touching serialized fields) ----

        /// <summary>
        /// The sorting order actually written to the quad — the serialized wish, clamped into the
        /// signed 16-bit range the renderer stores it in. Clamped rather than wrapped, because the two
        /// answers are opposites: a wrap puts the light at the very BOTTOM of the world, which is
        /// indistinguishable from the light being broken, and it fails silently.
        /// </summary>
        public int SafeSortingOrder => Mathf.Clamp(_sortingOrder, short.MinValue, MaxSortingOrder);

        /// <summary>The glow shape (cone beam vs round halo).</summary>
        public LightShape Shape { get => _shape; set => _shape = value; }
        /// <summary>Cone half-angle in degrees (ignored for a radial; 180 = full radial).</summary>
        public float ConeHalfAngle { get => _coneHalfAngle; set => _coneHalfAngle = Mathf.Clamp(value, 0f, 180f); }
        /// <summary>Throw distance (world units).</summary>
        public float Range { get => _range; set => _range = Mathf.Max(0.01f, value); }
        /// <summary>Light colour.</summary>
        public Color Color { get => _color; set => _color = value; }
        /// <summary>Master intensity (pre-gate / pre-flicker).</summary>
        public float Intensity { get => _intensity; set => _intensity = Mathf.Max(0f, value); }
        /// <summary>Local origin offset from this transform (e.g. the bow).</summary>
        public Vector2 OriginOffset { get => _originOffset; set => _originOffset = value; }
        /// <summary>Deterministic flicker amount (0 steady .. 1 strong).</summary>
        public float FlickerAmount { get => _flickerAmount; set => _flickerAmount = Mathf.Clamp01(value); }
        /// <summary>Radial edge softness (0 hard disc .. 1 soft halo).</summary>
        public float EdgeSoftness { get => _edgeSoftness; set => _edgeSoftness = Mathf.Clamp01(value); }
        /// <summary>Angular (cone) edge softness, as a fraction of the half-angle (0 hard .. 1 soft).</summary>
        public float AngularSoftness { get => _angularSoftness; set => _angularSoftness = Mathf.Clamp01(value); }

        /// <summary>
        /// The bright CORE — a small extra punch right at the lamp so the source reads as a hot point
        /// rather than as a haze (0 = none, the shipped default 1 = the fitting glows).
        ///
        /// <para>Settable since the boat glows were confined (owner's ruling 2026-09-03): a navigation
        /// lamp that shrank to the size of its own fitting needs the core to carry what the radius gave
        /// up, and a window SPILL needs it at zero — the spill's job is a wash on the deck, and a hot
        /// point at the wall would put back the blob the ruling took away.</para>
        /// </summary>
        public float CoreBoost { get => _coreBoost; set => _coreBoost = Mathf.Clamp(value, 0f, 4f); }

        // ---- what the lamp-shadow system reads (ADR 0016, lights PR B) ----

        /// <summary>Does this lamp throw cast shadows (<see cref="LampShadowSystem"/>)?</summary>
        public bool CastsShadows { get => _castsShadows; set => _castsShadows = value; }
        /// <summary>Lamp height above its casters' ground, metres — the cast shadows' rake lever, and what
        /// gives the ground pool its shape.</summary>
        public float LampHeightMeters { get => _lampHeightMeters; set => _lampHeightMeters = Mathf.Max(0f, value); }

        /// <summary>
        /// <b>How far this lamp LIGHTS the ground</b>, metres — its POOL, and not to be confused with
        /// <see cref="Range"/>, which since 2026-09-04 is the BLOOM: the size of the lit fitting.
        ///
        /// <para>The split was made in the preset library (<c>LightPresets.ReachMetres</c>) where the
        /// SITING could read it, and this is the other half — the runtime needs it too, because the thing
        /// that draws a pool is handed a <see cref="SceneLight"/> and not a preset kind. Set by
        /// <c>LightPresets.Apply</c> for a placed lamp and by <c>BoatSpotlight</c> for the searchlight.</para>
        ///
        /// <para><b>0 means this lamp casts no pool, and that is a real answer rather than a missing one.</b>
        /// A boat's sidelight is a SIGNAL — a coloured point another skipper reads your aspect from — and
        /// lighting the sea under it would be a lie about what it is for. Every boat lamp therefore leaves
        /// this at zero by default, and only the searchlight, which is aimed at the world, sets it.</para>
        /// </summary>
        public float ReachMetres { get => _reachMetres; set => _reachMetres = Mathf.Max(0f, value); }
        /// <summary>Night-gate darkness threshold (0..1) — read-only mirror for the shadow system.</summary>
        public float GateThreshold => _gateThreshold;
        /// <summary>Night-gate fade band (0..1).</summary>
        public float GateSoftness => _gateSoftness;
        /// <summary>What the light shows when no cycle runs (0..1).</summary>
        public float GateFallback => _gateFallback;
        /// <summary>The depth pin this light's quad uses (metres in front of the camera).</summary>
        public float CameraDepthOffset => _cameraDepthOffset;
        /// <summary>The lamp's world origin on the ground plane — this transform plus <see cref="OriginOffset"/>.</summary>
        public Vector2 WorldOrigin => transform.TransformPoint(new Vector3(_originOffset.x, _originOffset.y, 0f));
        /// <summary>The beam axis in world space (this transform's up — the boat heading for a spotlight).</summary>
        public Vector2 BeamDirection => transform.up;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            // A stable per-light seed (so two lights flicker out of phase) that is the SAME across runs for a
            // given object — deterministic. Hash the name + sibling index, not the volatile instance id.
            _flickerSeed = (name.GetHashCode() * 397) ^ transform.GetSiblingIndex();
            EnsureQuad();
        }

        private void OnEnable()
        {
            if (_quadRenderer != null) _quadRenderer.enabled = true;
            _timer = 0f;
            Tick();   // correct on the first frame
            PoseQuad();
            LampShadowSystem.RegisterLight(this);   // a lamp that is on may throw shadows (PR B)
        }

        private void OnDisable()
        {
            if (_quadRenderer != null) _quadRenderer.enabled = false;   // pooled, not destroyed
            LampShadowSystem.UnregisterLight(this);   // a dark lamp throws nothing
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.05f;
            Tick();
        }

        // The pose follows the carrier every frame (a boat turns/moves faster than the throttle); cheap, no alloc.
        private void LateUpdate() => PoseQuad();

        private void EnsureQuad()
        {
            if (_quad != null) return;

            var go = new GameObject("SceneLightQuad") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, worldPositionStays: false);
            _quad = go.transform;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedMesh();

            _quadRenderer = go.AddComponent<MeshRenderer>();
            _quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _quadRenderer.receiveShadows = false;
            _quadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _quadRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _quadRenderer.sortingOrder = SafeSortingOrder;   // ABOVE the day/night overlay (~32760)

            // ⚠️ READ THE NOTE ON MaxSortingOrder BEFORE THIS BLOCK. It was written as "the fix for the beam lit
            // land but NOT the water", and that diagnosis was wrong: the beam was invisible because the order
            // asked for did not fit in a short and wrapped to the bottom of the world. The Sort-as-2D group is
            // KEPT — a MeshRenderer genuinely does need it to sort against sprites by order at all, so it is
            // load-bearing — but it never was the cure for the missing beam, and the paragraph below describes a
            // mechanism that was never the one biting. Left standing, corrected rather than deleted, because the
            // sorting fact it states is true and worth knowing.
            //
            // SORT AS 2D. In the URP 2D renderer a MeshRenderer
            // does NOT reliably sort against SpriteRenderers by sortingOrder alone — for a mesh-vs-sprite pair it
            // falls back to world-space DEPTH, so this light quad (at the boat's world depth) could be OVERDRAWN by
            // the big Sea SPRITE that shares that depth, even though the light's sortingOrder (~32770) is far above
            // the Sea's (-5). Land is small unlit sprites the cone happened to win against; the full-screen water
            // sprite is not. A SortingGroup with sortAtRoot/"sort as 2D" makes the quad participate in 2D sorting
            // like a sprite, so its high sortingOrder is honoured against EVERY sprite (water included). It clears
            // the quad's depth info, which is fine — the light is ZTest Always and writes no depth, and nothing
            // depth-based reads it. (The day/night overlay dodges this quirk a different way — it sits AT the
            // camera near plane, the closest depth — which we ALSO mirror via _cameraDepthOffset in PoseQuad.)
            _sortingGroup = go.AddComponent<UnityEngine.Rendering.SortingGroup>();
            _sortingGroup.sortingOrder = SafeSortingOrder;
            _sortingGroup.sortAtRoot = true;   // "Sort as 2D": sort the group as one 2D element by its sorting order

            Material mat = SharedLightMaterial();
            if (mat != null) _quadRenderer.sharedMaterial = mat;
            else _quadRenderer.enabled = false;   // no shader/material yet -> no light (harmless)
        }

        /// <summary>
        /// <b>The one additive-light material every glow in the project draws with</b> — the shipped
        /// <c>Resources/AdditiveLight.mat</c> (which is also what force-compiles the shader, so a break
        /// fails CI RED rather than shipping magenta), with a runtime-built shared instance as the
        /// fallback for a scene that has no Resources folder. Null when the shader itself is missing,
        /// which every caller must treat as "draw nothing" rather than as an error.
        ///
        /// <para>Public because <see cref="BoatWindowGlow"/> draws lit window panes with the very same
        /// material and MUST NOT reach for it by a second copy of this path and this fallback: two
        /// copies is how one of them ends up loading a material the other does not.</para>
        /// </summary>
        public static Material SharedLightMaterial()
        {
            Material mat = Resources.Load<Material>(LightMaterialPath);
            if (mat != null) return mat;
            if (_sharedFallbackMaterial == null)
            {
                var shader = Shader.Find(LightShaderName);
                if (shader != null)
                    _sharedFallbackMaterial = new Material(shader) { name = "AdditiveLight (runtime shared)" };
            }
            return _sharedFallbackMaterial;
        }

        /// <summary>The active camera (MainCamera, else the first enabled one). Public so the window
        /// glow pins its pane mesh to the SAME camera this light pins its quad to — two answers to
        /// "which camera" is two depths, and the pair would composite in the wrong order.</summary>
        public static Camera ActiveCamera() => ResolveCamera();

        /// <summary>Push the (throttled) shape/colour/intensity/gate/flicker tunables to the shared material via the MPB.</summary>
        private void Tick()
        {
            if (_quadRenderer == null || _mpb == null) return;

            float halfAngle = _shape == LightShape.Radial ? 180f : _coneHalfAngle;

            // Deterministic flicker (rule 5): a pure hash of (seed, time). Use the unscaled game time so it is
            // reproducible and pause-aware; it is purely cosmetic and saves nothing.
            float flicker = LightMath.Flicker(_flickerSeed, Time.time, _flickerAmount, _flickerSpeed);

            // Geometry the shader needs to know in QUAD SPACE (q in [-1,1]^2): where the lamp sits and how far
            // it throws. A cone's lamp is at the bottom-centre (0,-1) throwing "up" 2 units across the quad; a
            // radial's lamp is at the centre (0,0) throwing 1 unit to the edge. PoseQuad scales the world quad so
            // these quad-space units == the world Range.
            bool radial = _shape == LightShape.Radial;
            Vector4 lampPos = radial ? new Vector4(0f, 0f, 0f, 0f) : new Vector4(0f, -1f, 0f, 0f);
            float throwQuad = radial ? 1f : 2f;

            _quadRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdLightColor, _color);
            _mpb.SetFloat(IdIntensity, Mathf.Max(_intensity, 0f) * flicker);
            _mpb.SetFloat(IdConeHalfAngle, halfAngle);
            _mpb.SetFloat(IdAngularSoft, _angularSoftness);
            _mpb.SetFloat(IdEdgeSoftness, _edgeSoftness);
            _mpb.SetFloat(IdCoreBoost, _coreBoost);
            _mpb.SetFloat(IdGateThreshold, _gateThreshold);
            _mpb.SetFloat(IdGateSoftness, _gateSoftness);
            _mpb.SetFloat(IdGateFallback, _gateFallback);
            _mpb.SetVector(IdLampPos, lampPos);
            _mpb.SetFloat(IdThrow, throwQuad);
            _quadRenderer.SetPropertyBlock(_mpb);

            _quadRenderer.sortingOrder = SafeSortingOrder;
            if (_sortingGroup != null) _sortingGroup.sortingOrder = SafeSortingOrder;   // keep the 2D-sort group in sync
            _quadRenderer.enabled = isActiveAndEnabled && _quadRenderer.sharedMaterial != null;
        }

        /// <summary>
        /// Pose + size the pooled quad each frame. The shared mesh is a CENTRED unit quad (X,Y ∈ [-1,1]). For a
        /// CONE the lamp is at the quad's bottom-centre (quad space (0,-1)) throwing along local +Y = this
        /// transform's UP (the boat heading): so we place the quad's CENTRE half a range forward of the origin
        /// and scale Y to 'range', X to the cone's far-end width. For a RADIAL the lamp is at the quad CENTRE
        /// (quad space (0,0)): the quad is centred on the origin, a 2·range square so the round halo reaches
        /// 'range' to the edge. The quad's DEPTH (Z along the view axis) is then pinned just in front of the
        /// active camera (like the day/night overlay) so the additive light reliably composites ABOVE the world
        /// sprites — this is the half of the over-water fix that handles the URP-2D mesh-vs-sprite depth quirk
        /// (the SortingGroup in EnsureQuad is the other half). Cheap, no alloc.
        /// </summary>
        private void PoseQuad()
        {
            if (_quad == null) return;

            Vector3 origin = transform.TransformPoint(new Vector3(_originOffset.x, _originOffset.y, 0f));
            _quad.rotation = transform.rotation;

            float r = Mathf.Max(_range, 0.01f);

            if (_shape == LightShape.Radial)
            {
                // Lamp at the quad centre: centre the quad on the origin. Quad spans 2 local units; scale by r so
                // the world quad spans 2·r (the halo reaches r to the edge).
                _quad.position = PinDepthToCamera(origin);
                _quad.localScale = new Vector3(r, r, 1f);
            }
            else
            {
                // Lamp at the quad bottom-centre: the quad must extend forward (local +Y, = transform.up) from
                // the origin. Quad spans 2 local-Y; scale Y by r/2 so the world height == r (bottom→top == r).
                // Centre sits r/2 forward of the lamp.
                float halfAngle = Mathf.Clamp(_coneHalfAngle, 0f, 180f);
                float halfWidth = r * Mathf.Tan(Mathf.Min(halfAngle, 89f) * Mathf.Deg2Rad);
                float worldWidth = Mathf.Clamp(2f * halfWidth, 0.1f, 8f * r);
                _quad.localScale = new Vector3(worldWidth * 0.5f, r * 0.5f, 1f);   // local span 2 -> world width/height
                _quad.position = PinDepthToCamera(origin + (Vector3)((Vector2)(transform.up) * (r * 0.5f)));
            }
        }

        /// <summary>
        /// Keep the quad's X/Y at the lamp's world position but pull its DEPTH (Z) to just in front of the active
        /// 2D camera, mirroring how the day/night overlay sits at the camera near plane. This is the world-depth
        /// half of the over-water fix: in the URP 2D renderer a MeshRenderer at the SAME world depth as a big
        /// water/ground SPRITE can be overdrawn by it regardless of sorting order, so placing the light at the
        /// camera's (closest) depth — the same trick the overlay uses to reliably draw over the water — makes the
        /// light win. The camera is orthographic and looks down +Z (camera at z≈-10 looking toward +Z), so
        /// "in front" is camera.z + offset. Look-direction-agnostic via the camera's forward. When there is no
        /// camera, or the offset is 0, the quad keeps its world depth (the old behaviour). PRESENTATION ONLY: the
        /// light is ZTest Always + additive, so depth never changes the LOOK — only the compositing order.
        /// </summary>
        private Vector3 PinDepthToCamera(Vector3 worldPos)
        {
            if (_cameraDepthOffset <= 0f) return worldPos;
            Camera cam = ResolveCamera();
            if (cam == null) return worldPos;
            // Depth along the view axis: place the quad _cameraDepthOffset metres in front of the camera, keeping
            // its X/Y. The pure depth math lives in LightMath.CameraDepthZ (unit-tested headless); for a 2D ortho
            // camera moving the quad along Z never changes its on-screen X/Y, only the compositing order.
            Transform ct = cam.transform;
            float aheadZ = LightMath.CameraDepthZ(ct.position.z, ct.forward.z, cam.nearClipPlane, _cameraDepthOffset);
            return new Vector3(worldPos.x, worldPos.y, aheadZ);
        }

        /// <summary>The active camera (MainCamera, else the first enabled one). Mirrors DayNightController.</summary>
        private static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            var all = Camera.allCameras;
            return (all != null && all.Length > 0) ? all[0] : null;
        }

        /// <summary>
        /// The shared CENTRED unit mesh: a quad spanning X,Y ∈ [-1,1] with UVs [0,1]² (so the shader maps uv to
        /// q ∈ [-1,1]²). The lamp position within it is set per-shape via the <c>_LampPos</c> uniform. Built
        /// once and reused by every light (rule 7).
        /// </summary>
        private static Mesh SharedMesh()
        {
            if (_sharedMesh != null) return _sharedMesh;
            var mesh = new Mesh { name = "SceneLightUnitQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3( 1f, -1f, 0f),
                new Vector3( 1f,  1f, 0f),
                new Vector3(-1f,  1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);   // never frustum-cull the moving light
            _sharedMesh = mesh;
            return mesh;
        }
    }
}
