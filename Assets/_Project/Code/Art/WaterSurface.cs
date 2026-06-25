using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The SIM-DRIVEN bridge for the layered water shader (ADR 0010 / design/water-rendering.md): feeds the
    /// deterministic <see cref="EnvironmentSample"/> + <see cref="IEnvironmentService.WaterLevelAt"/> into the
    /// <c>HiddenHarbours/Water</c> material each THROTTLED tick, so what the player SEES on the surface matches
    /// what the physics DOES (the P1 integrity rule):
    /// <list type="bullet">
    /// <item><description><b>Current → flow.</b> The tidal set (<see cref="EnvironmentSample.CurrentVector"/>)
    /// becomes the surface scroll direction + speed — the water visibly runs with the tide.</description></item>
    /// <item><description><b>Wind → roughness.</b> Wind strength (<see cref="EnvironmentSample.WindVector"/>)
    /// raises the whitecap / foam roughness — a breeze ruffles, a gale whitens.</description></item>
    /// <item><description><b>Water level → shoreline.</b> <see cref="IEnvironmentService.WaterLevelAt"/> drives
    /// the shader's <c>_WaterLevel</c>; combined with the baked seabed height map the depth gradient + foam band
    /// sweep across the SAME zero-crossing the walkability sim gates on.</description></item>
    /// <item><description><b>Sea-state → chop.</b> <see cref="EnvironmentSample.SeaState"/> sets the swell
    /// amplitude / choppiness — glassy to storm.</description></item>
    /// </list>
    ///
    /// <para><b>Depth source (first pass).</b> The shader needs the seabed elevation per pixel to draw shallow→deep
    /// and to place the foam band. This component BAKES <see cref="ITidalTerrain.ElevationAt"/> into a low-res
    /// height texture over the water plane's world bounds ONCE (the geometry is static), so the visible depth ==
    /// the physical depth the gameplay reads — they cannot disagree. The full per-pixel height-map texture
    /// (authored, not baked from the coarse zone function) is the refinement (design §9). With no terrain wired
    /// the shader falls back to uniform deep water (no false shoreline).</para>
    ///
    /// <para><b>Seam discipline (CLAUDE.md rule 4) &amp; determinism (rule 5).</b> Reads the sim only through the
    /// Core <see cref="GameServices.Environment"/> / <see cref="GameServices.TidalTerrain"/> accessors — never the
    /// Environment or World concrete classes — and never WRITES it. Visual-only: drives no simulation, saves
    /// nothing. The surface ANIMATION is <c>_Time</c> in the shader (motion only). Mapping is a pure function of
    /// the deterministic sample, so the look is reproducible from <c>(worldSeed, gameTime)</c>.</para>
    ///
    /// <para><b>Performance (rule 7).</b> Uniforms are pushed through a pooled <see cref="MaterialPropertyBlock"/>
    /// on a throttled tick (default a few Hz — the tide/wind are slow), NOT per frame, with no per-frame
    /// allocation. The height map is baked once on enable. Mobile-portable.</para>
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    public sealed class WaterSurface : MonoBehaviour
    {
        [Header("Sim → surface mapping (tunable; no magic numbers — rule 6)")]
        [Tooltip("Current speed (m/s) that maps to full surface scroll. The tidal set is scaled into 0..1 flow " +
                 "against this, then onto the material's base Flow.")]
        [Min(0.01f)] [SerializeField] private float _currentForFullFlow = 1.2f;
        [Tooltip("Wind speed (m/s) that maps to full surface roughness / whitecaps (saturates here).")]
        [Min(0.01f)] [SerializeField] private float _windForFullRoughness = 12f;
        [Tooltip("Base flow speed multiplier the normalized current scales — the material's own Flow is the floor; " +
                 "the live current adds on top so even slack water drifts a little.")]
        [Min(0f)] [SerializeField] private float _flowSpeedScale = 0.12f;

        [Header("Refresh")]
        [Tooltip("How often (Hz) the sim → uniform push runs. The sea is slow; a few Hz is plenty and cheap.")]
        [Min(0.5f)] [SerializeField] private float _refreshHz = 8f;

        [Header("Height map bake (the first-pass DEPTH source)")]
        [Tooltip("Bake ITidalTerrain.ElevationAt into a height texture so the shader's depth gradient + foam band " +
                 "match the gameplay seabed. Off → the shader reads uniform deep water (no false shoreline).")]
        [SerializeField] private bool _bakeHeightMap = true;
        [Tooltip("World-space rectangle the height map covers (centre + size). Should span the visible water.")]
        [SerializeField] private Vector2 _heightWorldCenter = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _heightWorldSize = new Vector2(160f, 120f);
        [Tooltip("Baked texture resolution (px). Coarse is fine — the gradient is smooth and the shader pixelizes.")]
        [Range(16, 256)] [SerializeField] private int _heightResolution = 96;
        [Tooltip("Elevation range (m above datum) the baked R channel maps across. Must bracket the terrain.")]
        [SerializeField] private float _heightMin = -4f;
        [SerializeField] private float _heightMax = 6f;

        // --- shader property ids (cached; no per-frame string lookups) ---
        private static readonly int IdWaterLevel = Shader.PropertyToID("_WaterLevel");
        private static readonly int IdFlow       = Shader.PropertyToID("_Flow");
        private static readonly int IdFlowDir    = Shader.PropertyToID("_FlowDir");
        private static readonly int IdRoughness  = Shader.PropertyToID("_Roughness");
        private static readonly int IdChop       = Shader.PropertyToID("_Chop");
        private static readonly int IdHeightTex  = Shader.PropertyToID("_HeightTex");
        private static readonly int IdHeightMin  = Shader.PropertyToID("_HeightMin");
        private static readonly int IdHeightMax  = Shader.PropertyToID("_HeightMax");
        private static readonly int IdHWorldMin  = Shader.PropertyToID("_HeightWorldMin");
        private static readonly int IdHWorldSize = Shader.PropertyToID("_HeightWorldSize");

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Texture2D _heightTex;
        private float _baseFlow = 0.06f;   // the material's authored Flow floor (read once)
        private float _timer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(IdFlow))
                _baseFlow = _renderer.sharedMaterial.GetFloat(IdFlow);
        }

        private void OnEnable()
        {
            BakeHeightMapIfNeeded();
            _timer = 0f;
            // Push once immediately so the surface is correct on the first frame (not a stale material default).
            PushUniforms();
        }

        private void OnDisable()
        {
            // Clear the per-renderer overrides so the shared material reads as authored if this is removed.
            if (_renderer != null) _renderer.SetPropertyBlock(null);
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;
            PushUniforms();
        }

        private void PushUniforms()
        {
            if (_renderer == null) return;
            var env = GameServices.Environment;
            if (env == null) return;   // no sim yet (EditMode / pre-boot) — leave the material at its defaults

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            float waterLevel = env.WaterLevelAt(now);
            EnvironmentSample s = env.Sample();

            float flow = FlowSpeed(s.CurrentVector, _baseFlow, _flowSpeedScale, _currentForFullFlow);
            Vector2 flowDir = FlowDirection(s.CurrentVector);
            float roughness = Roughness(s.WindVector, _windForFullRoughness);
            float chop = Choppiness(s.SeaState);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdWaterLevel, waterLevel);
            _mpb.SetFloat(IdFlow, flow);
            _mpb.SetVector(IdFlowDir, new Vector4(flowDir.x, flowDir.y, 0f, 0f));
            _mpb.SetFloat(IdRoughness, roughness);
            _mpb.SetFloat(IdChop, chop);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ---- the height-map bake (first-pass depth source) ----------------------------------------------

        private void BakeHeightMapIfNeeded()
        {
            if (!_bakeHeightMap) return;
            var terrain = GameServices.TidalTerrain;
            if (terrain == null) return;   // try again on the next enable; without it the shader reads deep water

            int res = Mathf.Clamp(_heightResolution, 16, 256);
            if (_heightTex == null || _heightTex.width != res)
            {
                _heightTex = new Texture2D(res, res, TextureFormat.R8, false, true)
                {
                    name = "WaterSurface.HeightBake",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            Vector2 min = _heightWorldCenter - _heightWorldSize * 0.5f;
            var pixels = new Color32[res * res];
            float span = Mathf.Max(_heightMax - _heightMin, 1e-3f);
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = min.x + (x + 0.5f) / res * _heightWorldSize.x;
                float wy = min.y + (y + 0.5f) / res * _heightWorldSize.y;
                float elev = terrain.ElevationAt(new Vector2(wx, wy));
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((elev - _heightMin) / span * 255f), 0, 255);
                pixels[y * res + x] = new Color32(r, r, r, 255);
            }
            _heightTex.SetPixels32(pixels);
            _heightTex.Apply(false, false);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(IdHeightTex, _heightTex);
            _mpb.SetFloat(IdHeightMin, _heightMin);
            _mpb.SetFloat(IdHeightMax, _heightMax);
            _mpb.SetVector(IdHWorldMin, new Vector4(min.x, min.y, 0f, 0f));
            _mpb.SetVector(IdHWorldSize, new Vector4(_heightWorldSize.x, _heightWorldSize.y, 0f, 0f));
            _renderer.SetPropertyBlock(_mpb);

            // Enable the shader's height-map branch on the shared material so the depth read uses the bake.
            if (_renderer.sharedMaterial != null)
                _renderer.sharedMaterial.EnableKeyword("_USE_HEIGHTTEX");
        }

        // ==== PURE sim → uniform mappings (testable; no Unity scene needed) ===============================

        /// <summary>
        /// Surface scroll speed from the tidal current. The material's authored <paramref name="baseFlow"/> is
        /// the floor (so slack water still drifts); the live current adds on top, its magnitude normalized
        /// against <paramref name="currentForFullFlow"/> and scaled by <paramref name="flowSpeedScale"/>.
        /// Deterministic; monotonic in current speed.
        /// </summary>
        public static float FlowSpeed(Vector2 currentVector, float baseFlow, float flowSpeedScale,
                                      float currentForFullFlow)
        {
            float norm = Mathf.Clamp01(currentVector.magnitude / Mathf.Max(currentForFullFlow, 1e-3f));
            return baseFlow + norm * Mathf.Max(flowSpeedScale, 0f);
        }

        /// <summary>
        /// Surface scroll DIRECTION from the tidal set: the normalized current vector. Slack water (near-zero
        /// current) falls back to +x so the surface never freezes to a NaN direction.
        /// </summary>
        public static Vector2 FlowDirection(Vector2 currentVector)
        {
            return currentVector.sqrMagnitude > 1e-6f ? currentVector.normalized : Vector2.right;
        }

        /// <summary>
        /// Surface roughness / whitecap amount (0..1) from wind strength: wind speed normalized against
        /// <paramref name="windForFullRoughness"/> and saturated. A breeze ruffles; a gale whitens.
        /// </summary>
        public static float Roughness(Vector2 windVector, float windForFullRoughness)
        {
            return Mathf.Clamp01(windVector.magnitude / Mathf.Max(windForFullRoughness, 1e-3f));
        }

        /// <summary>
        /// Choppiness / swell amplitude (0..1) from the sea-state scale (Glass=0 .. Storm=7), linear across the
        /// canon range. Glassy water is flat; a storm is fully choppy.
        /// </summary>
        public static float Choppiness(SeaState seaState)
        {
            int max = (int)SeaState.Storm;   // 7
            return max > 0 ? Mathf.Clamp01((int)seaState / (float)max) : 0f;
        }
    }
}
