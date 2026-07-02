using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The GLOBAL 24-hour lighting controller (ADR 0013): the one self-installing service that makes the
    /// WHOLE game darken and warm/cool together with the deterministic clock, with a hook for weather to
    /// dim it. It reads the time + weather through Core, evaluates the pure
    /// <see cref="DayNightMath"/> against the owner's <see cref="DayNightProfile"/>, and applies the result
    /// THREE ways every throttled tick:
    /// <list type="number">
    /// <item><description><b>A full-screen MULTIPLY tint overlay</b> (ADR 0013 decision (b)) — a single
    /// camera-filling quad drawn ABOVE all world sprites (and below the screen-space HUD) whose colour is
    /// the global <c>_DayNightTint</c>. This is what darkens the UNLIT sprites + tilemaps the project uses
    /// (they sample no 2D light), AND the self-lit water/grass, CONSISTENTLY in one place — the whole
    /// composited frame is multiplied at once, so nothing can drift bright while the rest goes dark.</description></item>
    /// <item><description><b><c>_SunDir</c> + <c>_SunElevation</c> globals</b> — the sun's ground-plane
    /// direction + height, so the water shader's specular glints agree with where the light comes from
    /// (it reads <c>_SunDir</c> in place of its hand-authored <c>_LightDir</c> while the cycle runs), and
    /// so the PR-2 projected shadows fall the right way.</description></item>
    /// <item><description><b>The <c>_DayNightTint</c> global itself</b> (<see cref="Shader.SetGlobalColor"/>)
    /// — the canonical colour the overlay material reads, also available to any future custom/Sprite-Lit
    /// shader that wants per-layer control.</description></item>
    /// <item><description><b>The <c>_ShadowStrength</c> global</b> — how firmly a cast shadow reads RIGHT
    /// NOW (0 none .. 1 full), folding the sun being up AND the live weather (overcast/storm fades it). It
    /// is the single live weather source the PR-2 <see cref="SpriteShadow"/> reads so its alpha softens
    /// under cloud — without each caster re-reading the sim. Pure <see cref="DayNightMath.ShadowStrength"/>
    /// of the same <c>(hour, weather, profile)</c> the tint uses.</description></item>
    /// </list>
    ///
    /// <para><b>Self-installing (mirrors <see cref="GrassWindBridge"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns a single hidden <c>[DontDestroyOnLoad]</c> host
    /// before the first scene loads, so it works in EVERY scene with NO wiring — neither the owner nor
    /// world-content places anything. It supersedes any per-scene static <c>m_AmbientSkyColor</c> (moot for
    /// unlit sprites, but the overlay is now the single source of the global look).</para>
    ///
    /// <para><b>Seam discipline (rule 4) &amp; determinism (rule 5).</b> Reads time/weather ONLY through the
    /// Core <see cref="GameServices.Clock"/> / <see cref="GameServices.Environment"/> accessors — never a
    /// concrete sim class — and never writes them. The look is a pure function of <c>(hour, weather,
    /// profile)</c>; nothing is saved or randomised. <b>Performance (rule 7):</b> one quad, one material,
    /// four global sets on a throttled tick (light is slow), no per-frame allocation, no per-sprite cost.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayNightController : MonoBehaviour
    {
        // Global shader uniforms (set via Shader.SetGlobal*, read by the overlay + water shaders).
        private static readonly int IdDayNightTint   = Shader.PropertyToID("_DayNightTint");
        private static readonly int IdSunDir         = Shader.PropertyToID("_SunDir");
        private static readonly int IdSunElevation   = Shader.PropertyToID("_SunElevation");
        private static readonly int IdShadowStrength = Shader.PropertyToID("_ShadowStrength");

        private const string ProfileResourcePath = "DayNightProfile"; // Resources/DayNightProfile.asset (optional)
        private const string OverlayMaterialPath = "DayNight";        // Resources/DayNight.mat (the multiply overlay)
        private const string OverlayShaderName   = "HiddenHarbours/DayNight";

        [Tooltip("How often (Hz) the light is recomputed + pushed. The cycle is slow, but a smooth scrub " +
                 "wants a few updates a second; ~10 Hz is plenty and cheap.")]
        [Min(1f)] [SerializeField] private float _refreshHz = 10f;

        [Tooltip("Hour (0..24) used when there is NO clock yet (EditMode / pre-boot / a bare art scene), so " +
                 "such scenes render in plain daylight rather than a stale/blank tint.")]
        [Range(0f, 24f)] [SerializeField] private float _fallbackHour = 12f;

        [Tooltip("Oversize the screen-fill overlay by this factor so a zoom / window-resize between ticks " +
                 "can never reveal an un-tinted gap at the screen edge. 1.1 = 10% margin (off-screen).")]
        [Min(1f)] [SerializeField] private float _overlayOversize = 1.1f;

        // ---- moonlight (the moon softly lifts the night tint; strength + colour live in the profile) ----
        // These four mirror MoonCycle's serialized lunar tunables (which mirror GameConfig — see MoonCycle's
        // header note for why GameConfig can't be auto-loaded here). KEEP THEM IN SYNC with MoonCycle so the
        // moonlight on the LAND agrees with the moon the player sees reflected in the WATER.
        [Header("Moon (KEEP IN SYNC with MoonCycle/GameConfig — the lift must match the visible moon)")]
        [Tooltip("Lunar month in in-game DAYS. Mirrors MoonCycle/GameConfig.LunarMonthDays (canon 28).")]
        [Min(0.1f)] [SerializeField] private float _lunarMonthDays = 28f;

        [Tooltip("In-game SECONDS per day. Mirrors MoonCycle/GameConfig.SecondsPerDay (default 1200).")]
        [Min(1f)] [SerializeField] private float _secondsPerDay = 1200f;

        [Tooltip("Days offsetting the start of the lunar cycle. Mirrors MoonCycle (ships at 14 = a new game " +
                 "starts on a FULL moon; must stay a multiple of the HALF-lunar period — see MoonMath.Phase01).")]
        [SerializeField] private float _phaseOffsetDays = 14f;

        [Tooltip("Day fraction (0..1) the moon RISES. Mirrors MoonCycle (0.78 ≈ dusk).")]
        [Range(0f, 1f)] [SerializeField] private float _moonriseFraction = 0.78f;

        [Tooltip("Day fraction (0..1) the moon SETS (wraps midnight). Mirrors MoonCycle (0.30 ≈ after dawn).")]
        [Range(0f, 1f)] [SerializeField] private float _moonsetFraction = 0.30f;

        private DayNightProfile _profile;
        private float _timer;

        private Transform _overlay;       // the persistent full-screen quad (child of this host)
        private MeshRenderer _overlayRenderer;
        private MaterialPropertyBlock _overlayMpb;

        /// <summary>
        /// Spawn the single self-installing host before the first scene. Guarded against double-install
        /// (domain reloads / additive loads). Mirrors the project's other self-installing services.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("DayNightController") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<DayNightController>();
        }

        private static bool _installed;

        private void Awake()
        {
            _profile = Resources.Load<DayNightProfile>(ProfileResourcePath);
            if (_profile == null) _profile = DayNightProfile.CreateDefault();
            _overlayMpb = new MaterialPropertyBlock();
            EnsureOverlay();
        }

        private void OnEnable()
        {
            _timer = 0f;
            Tick();   // push once immediately so the first frame is correct, not a stale default
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.1f;
            Tick();
        }

        // The TINT compute is throttled (Update), but the overlay must FOLLOW the camera every frame so a
        // fast pan/zoom can never reveal an un-tinted screen edge between ticks. The follow is a cheap
        // transform set; it runs after camera movement (LateUpdate).
        private void LateUpdate() => FitOverlayToCamera();

        private void Tick()
        {
            // --- read time + weather through Core only (rule 4); tolerate their absence ---
            var clock = GameServices.Clock;
            float hour = clock != null ? clock.HourOfDay : _fallbackHour;

            float visibility = 1f;            // clear by default (no fog) when there is no sim yet
            SeaState seaState = SeaState.Glass;
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample s = env.Sample();
                visibility = s.Visibility;
                seaState = s.SeaState;
            }

            // --- the moon's phase + arc off the same clock (deterministic, same MoonMath the water's
            //     reflected moon uses) so a lit, risen moon can lift the night tint. No clock (EditMode /
            //     pre-boot) → no moon → exactly the moonless tint, as before.
            float moonIllumination = 0f, moonElevation = 0f;
            if (clock != null)
            {
                float phase01 = MoonMath.Phase01(clock.TotalSeconds, _lunarMonthDays, _secondsPerDay,
                                                 _phaseOffsetDays);
                moonIllumination = MoonMath.IlluminatedFraction(phase01);
                MoonMath.MoonArc(clock.DayFraction, out _, out moonElevation,
                                 _moonriseFraction, _moonsetFraction);
            }

            // --- evaluate the pure model (the moonlight lift is folded INTO the one published tint, so
            //     _DayNightTint keeps meaning "the whole-frame multiply colour" for every consumer) ---
            Color tint = DayNightMath.DayNightTint(hour, _profile, visibility, seaState,
                                                   moonIllumination, moonElevation);
            float sunrise       = _profile != null ? _profile.SunriseHour : 6f;
            float sunset        = _profile != null ? _profile.SunsetHour : 20f;
            float bias          = _profile != null ? _profile.ShadowSouthBias : 0.2f;
            float lift          = _profile != null ? _profile.ShadowNoonLift : 0.9f;
            float overcastFades = _profile != null ? _profile.OvercastFadesShadow : 0.85f;
            Vector2 sunDir = DayNightMath.SunDirection(hour, sunrise, sunset, bias, lift);
            float elevation = DayNightMath.SunElevation(hour, sunrise, sunset);

            // Live cast-shadow strength: the sun being up folded with the live weather (overcast/storm
            // fades it). Computed here — where the real weather already is — and published as a global so
            // SpriteShadow gets a true weather source without each caster re-reading the sim (rule 4/7).
            float weatherDim     = DayNightMath.WeatherDim(visibility, seaState, _profile);
            float shadowStrength = DayNightMath.ShadowStrength(hour, sunrise, sunset, weatherDim, overcastFades);

            // --- push the globals (read by the overlay + the water specular + the projected shadows) ---
            Shader.SetGlobalColor(IdDayNightTint, tint);
            Shader.SetGlobalVector(IdSunDir, new Vector4(sunDir.x, sunDir.y, 0f, 0f));
            Shader.SetGlobalFloat(IdSunElevation, elevation);
            Shader.SetGlobalFloat(IdShadowStrength, shadowStrength);

            // --- colour the overlay (belt-and-braces: the material reads the global, but we also push the
            //     colour via a property block so it is correct even on the shared default material) ---
            ApplyOverlayColor(tint);
        }

        // ---- the full-screen multiply overlay -------------------------------------------------------------

        private void EnsureOverlay()
        {
            if (_overlay != null) return;

            var go = new GameObject("DayNightOverlay") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            _overlay = go.transform;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildQuad();

            _overlayRenderer = go.AddComponent<MeshRenderer>();
            _overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _overlayRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // Draw OVER all world sprites; the screen-space HUD canvas still renders after the camera, so it
            // stays readable (the glanceable tide/wind/time must not go dark — P1/UX).
            _overlayRenderer.sortingOrder = 32760;

            Material mat = Resources.Load<Material>(OverlayMaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find(OverlayShaderName);
                if (shader != null) mat = new Material(shader) { name = "DayNight (runtime)" };
            }
            if (mat != null) _overlayRenderer.sharedMaterial = mat;
            else _overlayRenderer.enabled = false;   // no shader/material → skip the overlay, still push globals
        }

        /// <summary>Push the day/night colour onto the overlay renderer (throttled, from <see cref="Tick"/>).</summary>
        private void ApplyOverlayColor(Color tint)
        {
            if (_overlayRenderer == null || _overlayMpb == null) return;
            _overlayRenderer.GetPropertyBlock(_overlayMpb);
            _overlayMpb.SetColor(IdDayNightTint, tint);
            _overlayRenderer.SetPropertyBlock(_overlayMpb);
        }

        /// <summary>
        /// Fit the screen-filling quad to the active camera (every frame, from <see cref="LateUpdate"/>, so a
        /// fast pan/zoom never reveals an un-tinted edge). Placed just in front of the camera, facing it,
        /// sized to the frustum and oversized a little for safety.
        /// </summary>
        private void FitOverlayToCamera()
        {
            if (_overlay == null || _overlayRenderer == null) return;

            Camera cam = ResolveCamera();
            if (cam == null) { _overlayRenderer.enabled = false; return; }
            if (!_overlayRenderer.enabled && _overlayRenderer.sharedMaterial != null) _overlayRenderer.enabled = true;

            Transform ct = cam.transform;
            float near = cam.nearClipPlane + 0.02f;
            _overlay.position = ct.position + ct.forward * near;
            _overlay.rotation = ct.rotation;

            float halfH, halfW;
            if (cam.orthographic)
            {
                halfH = cam.orthographicSize;
                halfW = halfH * cam.aspect;
            }
            else
            {
                // Perspective fallback (2D URP is ortho, but stay correct): fit the frustum at 'near'.
                halfH = near * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                halfW = halfH * cam.aspect;
            }
            float os = Mathf.Max(_overlayOversize, 1f);
            _overlay.localScale = new Vector3(halfW * 2f * os, halfH * 2f * os, 1f);
        }

        private static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            // No MainCamera-tagged camera (some scenes) — take the first enabled camera.
            var all = Camera.allCameras;
            return (all != null && all.Length > 0) ? all[0] : null;
        }

        /// <summary>A 1×1 quad centred on the origin in the XY plane, facing −z (toward the camera).</summary>
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "DayNightOverlayQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            // Big bounds so the 2D renderer never frustum-culls the camera-locked quad.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            return mesh;
        }
    }
}
