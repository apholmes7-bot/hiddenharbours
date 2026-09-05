using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// ONE caster's state for the lamp-shadow system — everything the shadow shader needs to draw
    /// its silhouette: where its feet are, the world rect its image fills, and the silhouette
    /// source (a sprite sheet + the cell's uv rect, or a mesh hull whose pixels sit in the feature's
    /// resolved screen texture). A plain value type: no scene, no GPU, so the packing is testable.
    /// </summary>
    public struct LampShadowCasterState
    {
        /// <summary>World ground-contact point — the pivot, the feet (ADR 0026).</summary>
        public Vector2 Foot;
        /// <summary>World AABB of the caster's UNSHEARED image (the sprite cell / the hull's overlay quad).</summary>
        public Vector2 RectMin, RectMax;
        /// <summary>Sprite path: the caster's sheet. Null for a hull.</summary>
        public Texture Sheet;
        /// <summary>Sprite path: the cell's uv rect as (u0, v0, du, dv) — see <see cref="LampShadowMath.SpriteUvRect"/>.</summary>
        public Vector4 UvRect;
        /// <summary>Hull path: the mesh hull whose id block marks her pixels. Null for a sprite.</summary>
        public IsoFacetHullRenderer Hull;

        public bool IsHull => Hull != null;
        public bool IsValid => RectMax.x > RectMin.x && RectMax.y > RectMin.y && (Hull != null || Sheet != null);
    }

    /// <summary>Anything that throws a lamp shadow. <see cref="SpriteShadow"/> (every sun caster)
    /// and <see cref="HullLampShadowCaster"/> (every mesh hull) implement it.</summary>
    public interface ILampShadowCaster
    {
        /// <summary>This caster's state right now; <c>false</c> when it has nothing to cast with.</summary>
        bool TryGetLampShadowCaster(out LampShadowCasterState state);
    }

    /// <summary>
    /// <b>The lamps cast shadows</b> (ADR 0016, lights PR B) — the second half of the owner's
    /// sentence: <i>"the spotlights and headlights need to put shadows ... the light needs to
    /// affect the environment, create shadows."</i>
    ///
    /// <para><b>What it does.</b> Every <see cref="SceneLight"/> registers here on enable; so does
    /// every caster (each sun-shadow caster, each mesh hull). On a throttled tick the system pairs
    /// every live, night-gated lamp with every caster inside its range, keeps the NEAREST pairs up
    /// to the pool budget, and gives each pair one pooled quad drawn with
    /// <c>HiddenHarbours/LampShadow</c>: the caster's own silhouette, sheared AWAY from that lamp
    /// through the caster's feet, by a length that grows with the caster's distance from the lamp
    /// and shrinks with the lamp's height (<see cref="LampShadowMath"/>), at an alpha that is the
    /// lamp's own falloff at the feet — so a shadow is exactly as strong as the light it blocks,
    /// feathers out with the beam's edge, and vanishes with the beam. The pose follows every frame
    /// (a beam sweeps, a boat sails); only the pairing is throttled.</para>
    ///
    /// <para><b>Where it draws, and why (the sorting law).</b> A lamp's light is ADDED after ADR
    /// 0013's whole-frame multiply — the glow quads at <see cref="SceneLight.MaxSortingOrder"/>, the
    /// water beam inside the water shader — so a shadow must come AFTER the glow or it darkens
    /// nothing the night did not already crush. The pooled quads sort at that same ceiling order
    /// (there is no order above it; the field is 16-bit) and win the tie by DEPTH: the 2D renderer
    /// breaks equal orders back-to-front along the view axis, so a quad pinned
    /// <see cref="ShadowDepthOffset"/> in front of the camera — nearer than a light quad's
    /// <see cref="SceneLight.DefaultCameraDepthOffset"/>, farther than the overlay's
    /// <see cref="DayNightController.OverlayNearOffset"/> — draws after every glow. The three
    /// constants are pinned in that order by a test. The shader then MULTIPLIES the frame down
    /// (never a second illumination model — ADR 0016's additive glow stays the glow, the water's
    /// relief stays the water's).</para>
    ///
    /// <para><b>Budget (rule 7).</b> <see cref="LampShadowProfile.MaxShadows"/> quads (24 shipped),
    /// one shared mesh, two shared materials, one property block, no per-frame allocation. The
    /// pairing scan is O(lamps × casters) at <see cref="LampShadowProfile.RefreshHz"/>: St Peters
    /// today is six lamps against ~1,000 casters, six thousand squared distances ten times a
    /// second. Past the pool the nearest lamp-to-caster pairs win.</para>
    ///
    /// <para><b>Determinism and seams (rules 4, 5).</b> Visual-only: a shadow is a pure function of
    /// the published day/night tint, the lamps' state and the casters' state; nothing is saved. It
    /// references only Art types; casters and lamps reach it through this file's own interface and
    /// <see cref="SceneLight"/>. Self-installing on the <see cref="WaterLightBridge"/> pattern.
    /// <see cref="PublishFrame"/> is public so a fixture can drive one deterministic frame with its
    /// own camera and no play session.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]   // after the hulls (-110), the lamps (-105) and the lights (0) have posed
    public sealed class LampShadowSystem : MonoBehaviour
    {
        /// <summary>
        /// Metres in front of the camera the shadow quads are pinned. Strictly between the day/night
        /// overlay's <see cref="DayNightController.OverlayNearOffset"/> (which must draw first —
        /// though its lower order already guarantees that) and a light quad's
        /// <see cref="SceneLight.DefaultCameraDepthOffset"/>: at an equal sorting order the 2D
        /// renderer draws the NEARER last, and this is what puts the shadow over the glow.
        /// </summary>
        public const float ShadowDepthOffset = 0.06f;

        /// <summary>Resources/LampShadowProfile.asset — the owner's tunables (optional; defaults otherwise).</summary>
        public const string ProfileResourcePath = "LampShadowProfile";
        /// <summary>Resources/LampShadow.mat — the sprite-silhouette variant.</summary>
        public const string SpriteMaterialPath = "LampShadow";
        /// <summary>Resources/LampShadowHull.mat — the mesh-hull variant (<see cref="HullKeyword"/> on).</summary>
        public const string HullMaterialPath = "LampShadowHull";
        public const string ShaderName = "HiddenHarbours/LampShadow";
        public const string HullKeyword = "HH_LAMP_SHADOW_HULL";

        /// <summary>
        /// The tint luminance at/above which the day/night cycle counts as RUNNING — mirrors the
        /// additive-light shader's <c>step(0.02, lum)</c>, so a shadow gates exactly as its lamp does
        /// (and, like the lamp, shows in edit mode / a bare art scene through the gate fallback).
        /// </summary>
        public const float CycleActiveLuminance = 0.02f;

        private static readonly int IdDayNightTint    = Shader.PropertyToID("_DayNightTint");
        private static readonly int IdMainTex         = Shader.PropertyToID("_MainTex");
        private static readonly int IdShadowColor     = Shader.PropertyToID("_ShadowColor");
        private static readonly int IdShadowDir       = Shader.PropertyToID("_ShadowDir");
        private static readonly int IdShadowFoot      = Shader.PropertyToID("_ShadowFoot");
        private static readonly int IdSpriteRectWorld = Shader.PropertyToID("_SpriteRectWorld");
        private static readonly int IdSpriteRectUV    = Shader.PropertyToID("_SpriteRectUV");
        private static readonly int IdHullIds         = Shader.PropertyToID("_HullIds");

        // The live lamps and casters. Static so a component registers from its own OnEnable without
        // finding the host (the WaterLightBridge shape). The set is the membership test; the list is
        // the deterministic iteration order.
        private static readonly List<SceneLight> Lights = new List<SceneLight>(16);
        private static readonly List<ILampShadowCaster> Casters = new List<ILampShadowCaster>(1024);
        private static readonly HashSet<ILampShadowCaster> CasterSet = new HashSet<ILampShadowCaster>();
        private static LampShadowSystem s_Instance;
        private static Mesh s_UnitQuad;
        private static Texture2D s_White;
        private static Material s_FallbackSprite, s_FallbackHull;

        private struct Slot
        {
            public Transform T;
            public MeshRenderer Renderer;
            public SortingGroup Group;
            public SceneLight Light;
            public ILampShadowCaster Caster;
            public bool HullMaterial;
        }

        private struct Pair
        {
            public SceneLight Light;
            public ILampShadowCaster Caster;
            public float DistSq;
        }

        private Slot[] _slots = System.Array.Empty<Slot>();
        private Pair[] _chosen = System.Array.Empty<Pair>();
        private LampShadowCasterState[] _casterScratch = System.Array.Empty<LampShadowCasterState>();

        /// <summary>
        /// Each scratch caster's own <see cref="Transform"/>, gathered in the same pass — the CARRIER rule
        /// in <see cref="Select"/> needs it, and an interface reference does not carry one.
        /// </summary>
        private Transform[] _casterCarrier = System.Array.Empty<Transform>();
        private int _active;
        private MaterialPropertyBlock _mpb;
        private Material _spriteMaterial, _hullMaterial;
        private LampShadowProfile _profile;
        private float _timer;

        // ---- public surface ----------------------------------------------------------------------

        /// <summary>The live system (null before install / in a bare edit-mode scene).</summary>
        public static LampShadowSystem Instance => s_Instance;
        /// <summary>How many lamps are registered right now. Diagnostics and tests.</summary>
        public static int LiveLightCount => Lights.Count;
        /// <summary>How many casters are registered right now. Diagnostics and tests.</summary>
        public static int LiveCasterCount => Casters.Count;

        /// <summary>How many shadows the last pairing chose (≤ the pool). The budget plate's number.</summary>
        public int ActiveShadowCount => _active;
        /// <summary>The pool size actually built (the profile's MaxShadows once the pool exists).</summary>
        public int PoolSize => _slots.Length;

        /// <summary>The profile in force — Resources/LampShadowProfile, else the built-in default. Settable for tests.</summary>
        public LampShadowProfile Profile
        {
            get => EnsureProfile();
            set => _profile = value;
        }

        /// <summary>Register a lamp (from its <c>OnEnable</c>). Ignores nulls and duplicates.</summary>
        public static void RegisterLight(SceneLight light)
        {
            if (light == null || Lights.Contains(light)) return;
            Lights.Add(light);
        }

        /// <summary>Unregister a lamp (from its <c>OnDisable</c>), so a dead lamp throws nothing.</summary>
        public static void UnregisterLight(SceneLight light)
        {
            if (light == null) return;
            Lights.Remove(light);
        }

        /// <summary>Register a caster (from its <c>OnEnable</c>). Ignores nulls and duplicates.</summary>
        public static void RegisterCaster(ILampShadowCaster caster)
        {
            if (caster == null || !CasterSet.Add(caster)) return;
            Casters.Add(caster);
        }

        /// <summary>Unregister a caster (from its <c>OnDisable</c>).</summary>
        public static void UnregisterCaster(ILampShadowCaster caster)
        {
            if (caster == null || !CasterSet.Remove(caster)) return;
            Casters.Remove(caster);
        }

        /// <summary>Empty both registries. For fixtures only — a test must not inherit another's lamps.</summary>
        public static void ClearRegistries()
        {
            Lights.Clear();
            Casters.Clear();
            CasterSet.Clear();
        }

        /// <summary>The pooled renderer in slot <paramref name="i"/> (null before the pool exists). Tests.</summary>
        public MeshRenderer SlotRenderer(int i) => i >= 0 && i < _slots.Length ? _slots[i].Renderer : null;
        /// <summary>The lamp slot <paramref name="i"/> is drawing for (null when the slot is idle). Tests.</summary>
        public SceneLight SlotLight(int i) => i >= 0 && i < _active ? _slots[i].Light : null;
        /// <summary>The caster slot <paramref name="i"/> is drawing (null when the slot is idle). Tests.</summary>
        public ILampShadowCaster SlotCaster(int i) => i >= 0 && i < _active ? _slots[i].Caster : null;
        /// <summary>Is slot <paramref name="i"/> drawing with the hull-silhouette material? Tests.</summary>
        public bool SlotIsHull(int i) => i >= 0 && i < _slots.Length && _slots[i].HullMaterial;

        // ---- lifecycle ---------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // "Enter Play Mode without domain reload" keeps statics: start every session empty so a
            // previous session's destroyed lamps and casters cannot hold pairs.
            ClearRegistries();
            var host = new GameObject("LampShadowSystem") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<LampShadowSystem>();
        }

        private void Awake() => s_Instance = this;

        private void OnEnable()
        {
            if (s_Instance == null) s_Instance = this;
            _timer = 0f;
        }

        private void OnDisable()
        {
            DisableAll();
            if (s_Instance == this) s_Instance = null;
        }

        private void LateUpdate()
        {
            LampShadowProfile p = EnsureProfile();
            _timer -= Time.deltaTime;
            bool reselect = _timer <= 0f;
            if (reselect) _timer = p.RefreshHz > 0f ? 1f / p.RefreshHz : 0.1f;
            PublishFrame(ResolveCamera(), reselect);
        }

        /// <summary>
        /// One frame of the system: (re)pair lamps with casters when <paramref name="reselect"/>,
        /// then pose every chosen shadow against <paramref name="camera"/>. Public so a fixture can
        /// drive a deterministic frame without a play session.
        /// </summary>
        public void PublishFrame(Camera camera, bool reselect = true)
        {
            EnsureProfile();
            EnsureMaterials();
            EnsurePool();
            _mpb ??= new MaterialPropertyBlock();
            if (reselect) Select();
            Pose(camera);
        }

        /// <summary>Stand every shadow down (a stopped session, a disabled system, strength 0).</summary>
        public void DisableAll()
        {
            _active = 0;
            for (int i = 0; i < _slots.Length; i++) SetEnabled(ref _slots[i], false);
        }

        // ---- the pairing (throttled) -------------------------------------------------------------

        private void Select()
        {
            LampShadowProfile p = _profile;
            _active = 0;
            if (p.Strength <= 0f || Lights.Count == 0 || Casters.Count == 0) return;

            // Gather every caster's state ONCE per tick; each lamp then reads the scratch, not the
            // caster — a thousand casters against six lamps must not cost six thousand bounds reads.
            int casterCount = Casters.Count;
            if (_casterScratch.Length < casterCount)
            {
                _casterScratch = new LampShadowCasterState[Mathf.NextPowerOfTwo(casterCount)];
                _casterCarrier = new Transform[_casterScratch.Length];
            }
            for (int i = 0; i < casterCount; i++)
            {
                ILampShadowCaster caster = Casters[i];
                // An interface reference cannot see Unity's fake null; ask the object itself.
                if (caster == null || (caster is Object uo && uo == null) ||
                    !caster.TryGetLampShadowCaster(out LampShadowCasterState state))
                    state = default;
                _casterScratch[i] = state;
                _casterCarrier[i] = caster is Component c ? c.transform : null;
            }

            Color tint = Shader.GetGlobalColor(IdDayNightTint);
            float luma = LightMath.Luminance(tint);
            bool cycleActive = luma >= CycleActiveLuminance;

            int count = 0;
            for (int li = 0; li < Lights.Count; li++)
            {
                SceneLight light = Lights[li];
                if (!LightIsLive(light, luma, cycleActive, out _)) continue;
                Vector2 lamp = light.WorldOrigin;
                // ⭐ THE CARRIER: a lamp never throws the silhouette of a caster it is MOUNTED ON.
                //
                // Until the land lamp posts (LampPosts) there was no such caster — every lamp in the game
                // was somewhere else from every caster, so the case could not arise. A lamp POST carries
                // both a SceneLight and a SpriteShadow on ONE GameObject, and its lamp-to-feet distance is
                // therefore just the light's own origin offset: ~0.2 m, the smallest distance anywhere in
                // the scene. Insert() keeps the nearest pairs GLOBALLY, so without this rule every post
                // sorts to the very front of the pool and spends one of the profile's MaxShadows slots
                // throwing a stub of itself at its own foot — nine posts would take nine of twenty-four
                // slots away from the bollards and hulls the lamps were placed to reveal.
                //
                // The test is deliberately EXACT — the same GameObject, one reference compare in the inner
                // loop — because that is the mounting this PR creates and it has no false positives.
                // ⚠ A light on a CHILD of its carrier (a walker's headlamp, where the beam hangs off the
                // player and her SpriteShadow is on the root) is NOT covered: that wants an ancestor walk,
                // and it belongs to the PR that introduces the mounting.
                Transform carrier = light.transform;
                // ⚠️⚠️ PAIRED BY THE LAMP'S REACH, NOT BY ITS BLOOM — and reading the wrong one of those
                // two silently switched every lamp shadow in the game OFF.
                //
                // This line read `light.Range` until 2026-09-04. Range then WAS the pool a lamp lights, so
                // "is this caster inside the light" and "how big is the glow" were the same question and
                // the same number. #733 split them on the owner's ruling: Range became the BLOOM, the size
                // of the lit fitting, and a lantern post's dropped from 3.6 m to 0.14 m. A bollard three
                // and a half metres away is not within fourteen centimetres of anything, so the pairing
                // loop stopped finding it — no error, no warning, and every plate simply came back with no
                // lamp shadows in it. Measured on the St Peters pier: 0 shadow pixels where there had been
                // a bollard's rake.
                //
                // ⭐ The lesson is the general one: when a number stops meaning what it meant, the bug is
                // not where you changed it — it is in every OTHER reader of the old meaning. Grep them.
                // A lamp throws a shadow of whatever stands in the ground it LIGHTS, so the reach is the
                // right number and always was — it simply did not exist under its own name until #733.
                float r = Mathf.Max(ShadowReachOf(light), 1e-4f);
                float r2 = r * r;
                for (int ci = 0; ci < casterCount; ci++)
                {
                    ref LampShadowCasterState s = ref _casterScratch[ci];
                    if (!s.IsValid) continue;
                    if (ReferenceEquals(_casterCarrier[ci], carrier)) continue;
                    float d2 = (s.Foot - lamp).sqrMagnitude;
                    if (d2 >= r2) continue;
                    count = Insert(light, Casters[ci], d2, count);
                }
            }

            _active = count;
            for (int i = 0; i < count; i++)
            {
                _slots[i].Light = _chosen[i].Light;
                _slots[i].Caster = _chosen[i].Caster;
            }
        }

        /// <summary>
        /// Insertion-sort a pair into the nearest-N slots by lamp-to-feet distance and return the
        /// new count. O(N) with N = the pool size, allocation-free — the shape <see cref="WaterLightBridge"/>
        /// keeps its four water lights in. A pair farther than every kept one is dropped.
        /// </summary>
        private int Insert(SceneLight light, ILampShadowCaster caster, float distSq, int count)
        {
            int cap = _chosen.Length;
            int at = count;
            while (at > 0 && _chosen[at - 1].DistSq > distSq) at--;
            if (at >= cap) return count;
            for (int j = Mathf.Min(count, cap - 1); j > at; j--) _chosen[j] = _chosen[j - 1];
            _chosen[at] = new Pair { Light = light, Caster = caster, DistSq = distSq };
            return Mathf.Min(count + 1, cap);
        }

        /// <summary>
        /// Is this lamp throwing shadows right now? Enabled, opted in, lit, and open at the night
        /// gate — the SAME gate the additive shader applies (<see cref="LightMath.NightGateWithFallback"/>),
        /// so the shadow and its glow fade together at dawn and are both absent at noon.
        /// </summary>
        /// <summary>
        /// How far this lamp throws shadows: its <see cref="SceneLight.ReachMetres"/> — the ground it
        /// lights — falling back to <see cref="SceneLight.Range"/> for a light that has never published a
        /// reach. The fallback is what keeps a bare hand-placed <c>SceneLight</c> behaving exactly as it did
        /// before the split, and it is safe because a light with no reach has no pool either: for such a
        /// light the two numbers still mean the one thing they always meant.
        /// </summary>
        internal static float ShadowReachOf(SceneLight light) =>
            light == null ? 0f : (light.ReachMetres > 0f ? light.ReachMetres : light.Range);

        private static bool LightIsLive(SceneLight light, float tintLuminance, bool cycleActive, out float gate)
        {
            gate = 0f;
            if (light == null || !light.isActiveAndEnabled || !light.CastsShadows) return false;
            if (light.Intensity <= 0f) return false;
            gate = LightMath.NightGateWithFallback(tintLuminance, light.GateThreshold, light.GateSoftness,
                                                  cycleActive, light.GateFallback);
            return gate > 0f;
        }

        // ---- the pose (every frame) --------------------------------------------------------------

        private void Pose(Camera camera)
        {
            LampShadowProfile p = _profile;
            float z = PinnedDepth(camera);
            Color tint = Shader.GetGlobalColor(IdDayNightTint);
            float luma = LightMath.Luminance(tint);
            bool cycleActive = luma >= CycleActiveLuminance;
            Color shadowColor = p.ShadowColor;

            for (int i = 0; i < _slots.Length; i++)
            {
                ref Slot slot = ref _slots[i];
                if (i >= _active) { SetEnabled(ref slot, false); continue; }

                SceneLight light = slot.Light;
                ILampShadowCaster caster = slot.Caster;
                if (!LightIsLive(light, luma, cycleActive, out float gate) ||
                    caster == null || (caster is Object uo && uo == null) ||
                    !caster.TryGetLampShadowCaster(out LampShadowCasterState s) || !s.IsValid)
                {
                    SetEnabled(ref slot, false);
                    continue;
                }

                Vector2 lamp = light.WorldOrigin;
                Vector2 foot = p.PixelSnap ? LampShadowMath.SnapToPixels(s.Foot, p.PixelsPerUnit) : s.Foot;
                bool radial = light.Shape == SceneLight.LightShape.Radial;
                float shape = LampShadowMath.LampShapeAtFoot(
                    lamp, light.BeamDirection, light.Range, radial ? 180f : light.ConeHalfAngle,
                    light.AngularSoftness, light.EdgeSoftness, foot);
                float alpha = LampShadowMath.ShadowAlpha(
                    p.Strength, shape, gate, LampShadowMath.IntensityShare(light.Intensity));
                if (alpha <= 0f) { SetEnabled(ref slot, false); continue; }

                Vector2 fallbackDir = radial ? Vector2.down : light.BeamDirection;
                Vector2 dir = LampShadowMath.ShadowDirection(lamp, foot, fallbackDir);
                float dist = (foot - lamp).magnitude;
                float elevation = LampShadowMath.LampElevation(light.LampHeightMeters, dist, p.MinLampHeightMeters);
                float len = LampShadowMath.ShadowLengthMultiple(elevation, p.LengthAtNoon, p.LengthAtHorizon, p.MaxLength);
                len = LampShadowMath.ClampShearFold(dir, len, p.MinShearDenominator);

                LampShadowMath.ShearedBounds(s.RectMin, s.RectMax, foot, dir, len, out Vector2 bmin, out Vector2 bmax);
                if (p.PixelSnap)
                {
                    // Floor the corner and ceil the far edge to the grid so the quad always covers the
                    // whole sheared image; the ANCHOR (the feet) is what pixel-snaps the shadow itself.
                    float ppu = Mathf.Max(p.PixelsPerUnit, 1e-3f);
                    bmin = new Vector2(Mathf.Floor(bmin.x * ppu) / ppu, Mathf.Floor(bmin.y * ppu) / ppu);
                    bmax = new Vector2(Mathf.Ceil(bmax.x * ppu) / ppu, Mathf.Ceil(bmax.y * ppu) / ppu);
                }
                Vector2 size = bmax - bmin;
                if (size.x <= 1e-4f || size.y <= 1e-4f) { SetEnabled(ref slot, false); continue; }

                slot.T.position = new Vector3(bmin.x, bmin.y, z);
                slot.T.rotation = Quaternion.identity;
                slot.T.localScale = new Vector3(size.x, size.y, 1f);

                bool hull = s.IsHull;
                if (slot.HullMaterial != hull || slot.Renderer.sharedMaterial == null)
                {
                    slot.Renderer.sharedMaterial = hull ? _hullMaterial : _spriteMaterial;
                    slot.HullMaterial = hull;
                }

                // Every property, every frame: a property block is sticky, and a slot that drew a hull
                // last frame must not hand its ids to the sprite it draws this frame.
                slot.Renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(IdShadowColor, new Color(shadowColor.r, shadowColor.g, shadowColor.b, alpha));
                _mpb.SetVector(IdShadowDir, new Vector4(dir.x, dir.y, 0f, 0f));
                _mpb.SetVector(IdShadowFoot, new Vector4(foot.x, foot.y, len, 0f));
                if (hull)
                {
                    IsoFacetHullRenderer h = s.Hull;
                    _mpb.SetVector(IdHullIds, new Vector4(h.HullId / 255f, h.ForeHullId / 255f,
                                                          IsoFacetHullRenderer.DeckOccupantSlots, 0f));
                    _mpb.SetTexture(IdMainTex, White());
                    _mpb.SetVector(IdSpriteRectWorld, new Vector4(0f, 0f, 1f, 1f));
                    _mpb.SetVector(IdSpriteRectUV, new Vector4(0f, 0f, 1f, 1f));
                }
                else
                {
                    Vector2 rs = s.RectMax - s.RectMin;
                    _mpb.SetVector(IdHullIds, Vector4.zero);
                    _mpb.SetTexture(IdMainTex, s.Sheet);
                    _mpb.SetVector(IdSpriteRectWorld, new Vector4(s.RectMin.x, s.RectMin.y, 1f / rs.x, 1f / rs.y));
                    _mpb.SetVector(IdSpriteRectUV, s.UvRect);
                }
                slot.Renderer.SetPropertyBlock(_mpb);

                slot.Renderer.sortingOrder = SceneLight.MaxSortingOrder;
                slot.Group.sortingOrder = SceneLight.MaxSortingOrder;
                SetEnabled(ref slot, true);
            }
        }

        /// <summary>
        /// The world Z the shadow quads sit at: <see cref="ShadowDepthOffset"/> in front of the camera
        /// (the same <see cref="LightMath.CameraDepthZ"/> the light quads use, with a smaller offset).
        /// No camera ⇒ world depth 0.
        /// </summary>
        public static float PinnedDepth(Camera camera)
        {
            if (camera == null) return 0f;
            Transform ct = camera.transform;
            return LightMath.CameraDepthZ(ct.position.z, ct.forward.z, camera.nearClipPlane, ShadowDepthOffset);
        }

        private static void SetEnabled(ref Slot slot, bool on)
        {
            if (slot.Renderer != null && slot.Renderer.enabled != on) slot.Renderer.enabled = on;
        }

        // ---- resources ---------------------------------------------------------------------------

        private LampShadowProfile EnsureProfile()
        {
            if (_profile != null) return _profile;
            _profile = Resources.Load<LampShadowProfile>(ProfileResourcePath);
            if (_profile == null) _profile = LampShadowProfile.CreateDefault();
            return _profile;
        }

        private void EnsureMaterials()
        {
            if (_spriteMaterial == null)
            {
                _spriteMaterial = Resources.Load<Material>(SpriteMaterialPath);
                if (_spriteMaterial == null)
                {
                    // Missing Resources material: ONE shared fallback for every system (never per
                    // instance — that would leak and break the shared-material batching).
                    if (s_FallbackSprite == null)
                    {
                        var shader = Shader.Find(ShaderName);
                        if (shader != null) s_FallbackSprite = new Material(shader) { name = "LampShadow (runtime shared)" };
                    }
                    _spriteMaterial = s_FallbackSprite;
                }
            }
            if (_hullMaterial == null)
            {
                _hullMaterial = Resources.Load<Material>(HullMaterialPath);
                if (_hullMaterial == null)
                {
                    if (s_FallbackHull == null)
                    {
                        var shader = Shader.Find(ShaderName);
                        if (shader != null)
                        {
                            s_FallbackHull = new Material(shader) { name = "LampShadowHull (runtime shared)" };
                            s_FallbackHull.EnableKeyword(HullKeyword);
                        }
                    }
                    _hullMaterial = s_FallbackHull;
                }
            }
        }

        private void EnsurePool()
        {
            int wanted = Mathf.Clamp(_profile.MaxShadows, 1, 64);
            if (_slots.Length == wanted) return;

            for (int i = wanted; i < _slots.Length; i++)
            {
                if (_slots[i].Renderer == null) continue;
                if (Application.isPlaying) Destroy(_slots[i].Renderer.gameObject);
                else DestroyImmediate(_slots[i].Renderer.gameObject);
            }
            var slots = new Slot[wanted];
            for (int i = 0; i < wanted; i++)
            {
                if (i < _slots.Length && _slots[i].Renderer != null) { slots[i] = _slots[i]; continue; }
                slots[i] = MakeSlot(i);
            }
            _slots = slots;
            _chosen = new Pair[wanted];
            if (_active > wanted) _active = wanted;
        }

        private Slot MakeSlot(int index)
        {
            var go = new GameObject("LampShadow_" + index) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = UnitQuad();

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.sortingOrder = SceneLight.MaxSortingOrder;
            mr.enabled = false;

            // Sort as 2D, like the light quads: a MeshRenderer competes with sprites by order only
            // through a SortingGroup, and the ORDER is what puts this above the world.
            var group = go.AddComponent<SortingGroup>();
            group.sortingOrder = SceneLight.MaxSortingOrder;
            group.sortAtRoot = true;

            return new Slot { T = go.transform, Renderer = mr, Group = group };
        }

        /// <summary>A unit quad from (0,0) to (1,1) with matching uvs; the slot transform scales it to the shadow's box.</summary>
        private static Mesh UnitQuad()
        {
            if (s_UnitQuad != null) return s_UnitQuad;
            var mesh = new Mesh { name = "LampShadowUnitQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            s_UnitQuad = mesh;
            return mesh;
        }

        private static Texture2D White()
        {
            if (s_White != null) return s_White;
            s_White = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "LampShadowWhite", hideFlags = HideFlags.HideAndDontSave };
            s_White.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
                                        new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            s_White.Apply(false, true);
            return s_White;
        }

        private static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            var all = Camera.allCameras;
            return (all != null && all.Length > 0) ? all[0] : null;
        }
    }
}
