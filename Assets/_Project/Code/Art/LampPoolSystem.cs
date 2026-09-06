using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE PATCH OF GROUND A LAMP MAKES BRIGHTER</b> — the half of a lamp nothing in this game has ever
    /// drawn (ADR 0016 amendment, world-lighting PR 2c).
    ///
    /// <para><b>The owner, 2026-09-04:</b> <i>"dock lights are just a round glow, it should glow from within
    /// the lamp reasilitcally."</i> #733 answered the first half — every lamp's additive quad came down to
    /// the size of its lit fitting, so a lantern reads as a lantern. It also left the pier honestly dark,
    /// because ADR 0016's quad is the SOURCE'S OWN BLOOM and can only add a sheet of cream to the frame. This
    /// is the second half, and it is a different picture of the same lamp: the ground.</para>
    ///
    /// <para><b>⭐⭐ WHY IT IS A SCREEN-SPACE PASS AND NOT A LIGHTING TERM IN A SHADER.</b> The obvious design
    /// — extend <c>SpriteLitDecor.hlsl</c> and the terrain splat with a point-light term — lights the wrong
    /// things here, and the wharf is where the owner was looking. <b>Every wharf deck tile, every fitting and
    /// every lamp post is a plain <c>SpriteRenderer</c> with no material set</b> (grep both regions'
    /// builders: not one <c>sharedMaterial</c> or <c>SpriteLightBinder</c> among them) — only trees, shrubs
    /// and shore plants are on the lit-decor path. And there is no painted ground at a pier either: the splat
    /// shader clips below its paint floor, and the St Peters pier stands over a slip dredged to −1.0 m, so
    /// under those planks is sea. A lit-path term would therefore light the shore, the trees and the yards,
    /// and light NOTHING at either wharf. A pass that multiplies the assembled frame lights whatever occupies
    /// the pixel — planks, bollards, a mesh hull, the walker — with no per-family art and no bake.</para>
    ///
    /// <para><b>⚠️ The cost, stated rather than bounded away.</b> Screen space cannot tell a plank from a
    /// gull: something ABOVE the ground passing over a pool is brightened as though it were standing in it.
    /// <see cref="LampShadowSystem"/> has accepted exactly this cost since #698 and the sun's shade arm since
    /// #727; this is the third member of that family and the trade is the same one. <see cref="Enabled"/> is
    /// the way back.</para>
    ///
    /// <para><b>The ladder.</b> The pool sits at <see cref="SceneLight.MaxSortingOrder"/> like every other
    /// lamp quad, with the camera-depth tiebreak putting it FARTHEST of the three, so the three draw in the
    /// only order that composes:</para>
    /// <list type="number">
    ///   <item><b>the pool</b> (this, depth <see cref="PoolDepthOffset"/> 0.14) — multiplies the ground UP;</item>
    ///   <item><b>the bloom</b> (<see cref="SceneLight"/>, depth 0.10) — adds the lit fitting on top, so the
    ///         lamp itself is still the hottest thing in frame;</item>
    ///   <item><b>the shadows</b> (<see cref="LampShadowSystem"/>, depth 0.06) — multiply back DOWN, so a
    ///         bollard's shadow is cut into the light this pass just laid, which is what makes the two of
    ///         them one picture rather than two.</item>
    /// </list>
    ///
    /// <para><b>Budget (rule 7).</b> <see cref="LampShadowProfile.MaxPools"/> quads, one shared mesh, one
    /// shared material, one property block, no per-frame allocation; the nearest lamps to the camera win the
    /// slots. A lamp with no <see cref="SceneLight.ReachMetres"/> — every boat lamp, by default — is not a
    /// candidate at all, so a wharf full of moored hulls costs nothing.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Visual only: it reads the registry, the published day/night tint
    /// and the camera, drives no sim and saves nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LampPoolSystem : MonoBehaviour
    {
        /// <summary>
        /// Metres in front of the camera the pool quads are pinned — FARTHER than the bloom's
        /// <see cref="SceneLight.DefaultCameraDepthOffset"/> (0.10) and the shadows'
        /// <see cref="LampShadowSystem.ShadowDepthOffset"/> (0.06), because the 2D renderer breaks equal
        /// sorting orders back-to-front along the view axis and this must be the FIRST of the three to draw.
        /// Pinned by a test alongside the other two.
        /// </summary>
        public const float PoolDepthOffset = 0.14f;

        private const string PoolShaderName = "HiddenHarbours/LampPool";
        private const string PoolMaterialPath = "LampPool";          // Resources/LampPool.mat
        private const string ProfileResourcePath = "LampShadowProfile";

        /// <summary>Below this tint luminance the day/night cycle is treated as not running (the same
        /// threshold <see cref="LampShadowSystem"/> uses, so the two agree about what "night" is).</summary>
        private const float CycleActiveLuminance = 0.001f;

        private static readonly int IdPoolColor = Shader.PropertyToID("_PoolColor");
        private static readonly int IdPoolLamp  = Shader.PropertyToID("_PoolLamp");
        private static readonly int IdPoolCone  = Shader.PropertyToID("_PoolCone");
        private static readonly int IdPoolGate  = Shader.PropertyToID("_PoolGate");
        private static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");

        private struct Slot
        {
            public Transform T;
            public MeshRenderer Renderer;
            public SortingGroup Group;
            public SceneLight Light;
        }

        private static LampPoolSystem s_Instance;
        private static Mesh s_UnitQuad;
        private static Material s_Material;

        private Slot[] _slots = new Slot[0];
        private readonly List<SceneLight> _chosen = new List<SceneLight>(8);
        private readonly List<float> _chosenDistSq = new List<float>(8);
        private LampShadowProfile _profile;
        private MaterialPropertyBlock _mpb;
        private float _timer;
        private int _active;

        // ---- public surface ----------------------------------------------------------------------

        /// <summary>The live system (null before install / in a bare edit-mode scene).</summary>
        public static LampPoolSystem Instance => s_Instance;

        /// <summary>How many pools the last selection drew. The budget plate's number.</summary>
        public int ActivePoolCount => _active;

        /// <summary>The pool size actually built. Tests.</summary>
        public int PoolSize => _slots.Length;

        /// <summary>The lamp slot <paramref name="i"/> is pooling for (null when idle). Tests.</summary>
        public SceneLight SlotLight(int i) => i >= 0 && i < _active ? _slots[i].Light : null;

        /// <summary>The pooled renderer in slot <paramref name="i"/> (null before the pool exists). Tests.</summary>
        public MeshRenderer SlotRenderer(int i) => i >= 0 && i < _slots.Length ? _slots[i].Renderer : null;

        /// <summary>The profile in force — the lamp system's own, so one asset tunes both halves.</summary>
        public LampShadowProfile Profile
        {
            get => EnsureProfile();
            set => _profile = value;
        }

        /// <summary>
        /// Does a lamp cast a pool at all? A lamp must be live, gated on by the night, and — the part that
        /// makes this cheap — must have BOTH a reach and a height. No reach means it does not claim to light
        /// the ground; no height means it cannot say at what angle, and a pool without an angle is the flat
        /// disc this whole arc removed.
        /// </summary>
        public static bool PoolsLight(SceneLight light, float tintLuminance, bool cycleActive, out float gate)
        {
            gate = 0f;
            if (light == null || !light.isActiveAndEnabled) return false;
            if (light.Intensity <= 0f || light.ReachMetres <= 0f || light.LampHeightMeters <= 0f) return false;
            gate = LightMath.NightGateWithFallback(tintLuminance, light.GateThreshold, light.GateSoftness,
                                                   cycleActive, light.GateFallback);
            return gate > 0f;
        }

        // ---- lifecycle ---------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("LampPoolSystem") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<LampPoolSystem>();
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
        /// One frame of the system: (re)choose the nearest lamps when <paramref name="reselect"/>, then pose
        /// every chosen pool against <paramref name="camera"/>. Public so a fixture can drive a deterministic
        /// frame without a play session — the shape <see cref="LampShadowSystem.PublishFrame"/> keeps.
        /// </summary>
        public void PublishFrame(Camera camera, bool reselect = true)
        {
            LampShadowProfile p = EnsureProfile();
            if (!p.PoolsEnabled || p.PoolStrength <= 0f)
            {
                DisableAll();
                return;
            }
            EnsureMaterial();
            EnsurePool(p);
            _mpb ??= new MaterialPropertyBlock();
            if (reselect) Select(camera);
            Pose(camera, p);
        }

        /// <summary>Stand every pool down (the dial at zero, a disabled system, a stopped session).</summary>
        public void DisableAll()
        {
            _active = 0;
            for (int i = 0; i < _slots.Length; i++) SetEnabled(ref _slots[i], false);
        }

        // ---- the choosing (throttled) -------------------------------------------------------------

        private void Select(Camera camera)
        {
            _chosen.Clear();
            _chosenDistSq.Clear();
            _active = 0;

            IReadOnlyList<SceneLight> lights = LampShadowSystem.LiveLights;
            if (lights.Count == 0) return;

            Color tint = Shader.GetGlobalColor(IdDayNightTint);
            float luma = LightMath.Luminance(tint);
            bool cycleActive = luma >= CycleActiveLuminance;
            Vector2 eye = camera != null ? (Vector2)camera.transform.position : Vector2.zero;
            int cap = _slots.Length;

            for (int i = 0; i < lights.Count; i++)
            {
                SceneLight light = lights[i];
                if (!PoolsLight(light, luma, cycleActive, out _)) continue;
                // Nearest to the CAMERA, not to the player: the slots should go to the pools that are on
                // screen. The same rule WaterLightBridge picks its four water lights by.
                float d2 = ((Vector2)light.WorldOrigin - eye).sqrMagnitude;
                Insert(light, d2, cap);
            }
            _active = _chosen.Count;
        }

        /// <summary>Insertion-sort a lamp into the nearest-N slots by camera distance. O(N) with N = the pool
        /// size and allocation-free — the shape <see cref="LampShadowSystem"/> and <see cref="WaterLightBridge"/>
        /// both keep. A lamp farther than every kept one is dropped.</summary>
        private void Insert(SceneLight light, float distSq, int cap)
        {
            if (cap <= 0) return;
            int at = _chosen.Count;
            while (at > 0 && _chosenDistSq[at - 1] > distSq) at--;
            if (at >= cap) return;

            _chosen.Insert(at, light);
            _chosenDistSq.Insert(at, distSq);
            if (_chosen.Count > cap)
            {
                _chosen.RemoveAt(_chosen.Count - 1);
                _chosenDistSq.RemoveAt(_chosenDistSq.Count - 1);
            }
        }

        // ---- the pose (every frame) ---------------------------------------------------------------

        private void Pose(Camera camera, LampShadowProfile p)
        {
            float z = PinnedDepth(camera);
            Color tint = Shader.GetGlobalColor(IdDayNightTint);
            float luma = LightMath.Luminance(tint);
            bool cycleActive = luma >= CycleActiveLuminance;

            int drawn = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref Slot slot = ref _slots[i];
                SceneLight light = i < _active ? _chosen[i] : null;
                if (light == null || !PoolsLight(light, luma, cycleActive, out float gate))
                {
                    SetEnabled(ref slot, false);
                    continue;
                }

                Vector2 lamp = light.WorldOrigin;
                float reach = light.ReachMetres;

                // The quad covers the pool and not one metre more: a square of side 2·reach centred on the
                // lamp. The shader clips itself by distance, so an oversized quad is pure fill rate.
                slot.T.position = new Vector3(lamp.x, lamp.y, z);
                slot.T.rotation = Quaternion.identity;
                slot.T.localScale = new Vector3(reach, reach, 1f);

                // A cone lamp pools a WEDGE — the searchlight sweeping a dock. A radial ships cosHalf at −1,
                // which makes the shader's angular gate exactly 1 in every direction: one code path, and the
                // round case is the cone case with the gate wide open.
                bool cone = light.Shape == SceneLight.LightShape.Cone && light.ConeHalfAngle < 180f;
                float cosHalf = cone ? Mathf.Cos(Mathf.Clamp(light.ConeHalfAngle, 0f, 180f) * Mathf.Deg2Rad) : -1f;
                Vector2 axis = cone ? light.BeamDirection.normalized : Vector2.up;

                Color c = light.Color;
                // ⚠⚠ DIVIDED BY THE NIGHT'S OWN LUMINANCE, and that division is what makes the pass
                // visible at all. A multiply is bounded by what it multiplies, and by the time this pass
                // runs ADR 0013's tint has crushed the pier to a mean luminance around 0.04 — so a naive
                // dst × 1.6 lifted a plank by six values in 255 and the first measured run reported ZERO
                // pixels changed. The factor that reconstructs albedo × (ambient + lamp) from a frame
                // holding albedo × ambient is exactly 1 + lamp/ambient. See LightMath.PoolBaseGain.
                float gain = LightMath.PoolBaseGain(p.PoolStrength, light.Intensity, gate, luma);

                slot.Renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(IdPoolColor, new Color(c.r, c.g, c.b, gain));
                _mpb.SetVector(IdPoolLamp, new Vector4(lamp.x, lamp.y, light.LampHeightMeters, reach));
                _mpb.SetVector(IdPoolCone, new Vector4(p.PoolEdgeSoftness, cosHalf, axis.x, axis.y));
                // The night gate is already inside `gain` (PoolBaseGain takes it), so the shader's own
                // gate slot ships at 1: applying it twice would square a fraction and put dusk out.
                _mpb.SetVector(IdPoolGate, new Vector4(1f, 0f, 0f, 0f));
                slot.Renderer.SetPropertyBlock(_mpb);

                slot.Renderer.sortingOrder = SceneLight.MaxSortingOrder;
                slot.Group.sortingOrder = SceneLight.MaxSortingOrder;
                slot.Light = light;
                SetEnabled(ref slot, true);
                drawn++;
            }
            _active = drawn;
        }

        /// <summary>The world Z the pool quads sit at: <see cref="PoolDepthOffset"/> in front of the camera,
        /// through the same <see cref="LightMath.CameraDepthZ"/> the bloom and the shadows use. No camera ⇒
        /// world depth 0.</summary>
        public static float PinnedDepth(Camera camera)
        {
            if (camera == null) return 0f;
            Transform ct = camera.transform;
            return LightMath.CameraDepthZ(ct.position.z, ct.forward.z, camera.nearClipPlane, PoolDepthOffset);
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

        private static void EnsureMaterial()
        {
            if (s_Material != null) return;
            s_Material = Resources.Load<Material>(PoolMaterialPath);
            if (s_Material != null) return;
            // The runtime fallback for a scene with no Resources copy. It carries no keywords at all (the
            // pass declares none), so there is no variant for a stripper to drop out of a player build.
            Shader sh = Shader.Find(PoolShaderName);
            if (sh != null) s_Material = new Material(sh) { name = "LampPool (runtime shared)" };
        }

        private void EnsurePool(LampShadowProfile p)
        {
            int want = Mathf.Max(0, p.MaxPools);
            if (_slots.Length == want) return;

            for (int i = want; i < _slots.Length; i++)
                if (_slots[i].T != null) Destroy(_slots[i].T.gameObject);

            var grown = new Slot[want];
            for (int i = 0; i < want; i++)
            {
                if (i < _slots.Length && _slots[i].T != null) { grown[i] = _slots[i]; continue; }

                var go = new GameObject($"LampPool_{i}") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, worldPositionStays: false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = UnitQuad();
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mr.sharedMaterial = s_Material;
                mr.enabled = false;
                // Sort as 2D, for the reason SceneLight documents: a MeshRenderer does not sort against
                // SpriteRenderers by order alone in the URP 2D renderer without it.
                var group = go.AddComponent<SortingGroup>();
                group.sortAtRoot = true;
                grown[i] = new Slot { T = go.transform, Renderer = mr, Group = group };
            }
            _slots = grown;
        }

        /// <summary>The shared CENTRED unit quad (X,Y ∈ [−1,1]), so a localScale of <c>reach</c> spans
        /// 2·reach — exactly the pool's diameter. Built once for every slot (rule 7).</summary>
        private static Mesh UnitQuad()
        {
            if (s_UnitQuad != null) return s_UnitQuad;
            var mesh = new Mesh { name = "LampPoolUnitQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f),   new Vector3(-1f, 1f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);   // never frustum-cull a moving pool
            s_UnitQuad = mesh;
            return mesh;
        }

        /// <summary>The active camera (MainCamera, else the first enabled one) — the same resolution the
        /// bloom and the shadows make, so all three quads land at one camera's depth.</summary>
        private static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            Camera[] all = Camera.allCameras;
            return (all != null && all.Length > 0) ? all[0] : null;
        }
    }
}
