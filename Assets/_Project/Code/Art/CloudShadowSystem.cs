using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The soft cloud shade over land and water: one world-anchored multiply per world camera, drawn in the
    /// sun-shade rung (above the day/night tint, below the lamps' glow, so it never dims a lamp and never
    /// touches the screen-space HUD). One texture made per world seed, one quad, one material, one property
    /// block. The wind carries it; the weather and the sun decide how much there is; indoors there is none.
    /// No simulation, stencil claim or saved state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CloudShadowSystem : MonoBehaviour
    {
        static readonly int NoiseId = Shader.PropertyToID("_CloudShadeNoise");
        static readonly int OffsetsId = Shader.PropertyToID("_CloudShadeOffsets");
        static readonly int ShapeId = Shader.PropertyToID("_CloudShadeShape");
        static readonly int ScaleId = Shader.PropertyToID("_CloudShadeScale");
        static readonly int ColourId = Shader.PropertyToID("_CloudShadeColour");
        static readonly int SunElevationId = Shader.PropertyToID("_SunElevation");
        static CloudShadowSystem _instance;
        CloudShadowDef _def;
        DayNightProfile _day;
        GameObject _draw;
        Mesh _mesh;
        Material _material;
        Texture2D _noise;
        MeshRenderer _renderer;
        MaterialPropertyBlock _properties;
        readonly CloudDrift _drift = new CloudDrift();
        readonly HeldWind _heldWind = new HeldWind();
        bool _casting = true, _subscribed;
        int _noiseSeed;
        bool _inCabin;
        EntityId _cabinHull;
        IWaveWindSource _preparedWind;
        double _preparedSeconds = double.NaN;
        int _preparedSeed;
        float _outdoors = 1;

        public static CloudShadowSystem Instance => _instance;
        public bool Casting => _casting;
        public CloudShadowDef Definition => _def;
        public MeshRenderer ShadowRenderer => _renderer;
        public int DrawCount => _renderer != null && _renderer.enabled && _renderer.gameObject.activeInHierarchy ? 1 : 0;
        public Vector4 Offsets { get; private set; }
        /// <summary>The strength last sent to the shader (0 = not drawn).</summary>
        public float LastStrength { get; private set; }
        /// <summary>1 outdoors, 0 indoors, easing between over <see cref="CloudShadowDef.IndoorFadeSeconds"/>.</summary>
        public float Outdoors => _outdoors;
        /// <summary>A fixed frame for looks and tests; when set, the live clock and weather are not read.</summary>
        public CloudShadowFrame? HeldFrame { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHost() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;
            var def = Resources.Load<CloudShadowDef>(nameof(CloudShadowDef));
            if (def == null || !def.Enabled || !def.IsValid()) return;
            var host = new GameObject(nameof(CloudShadowSystem)) { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<CloudShadowSystem>();
            _instance.Configure(def);
        }

        public void Configure(CloudShadowDef def)
        {
            ReleaseDraw();
            _def = def != null && def.IsValid() ? def : null;
            _day = Resources.Load<DayNightProfile>("DayNightProfile");
            _preparedSeconds = double.NaN;
        }

        public void SetCasting(bool enabled)
        {
            _casting = enabled;
            if (!enabled) ReleaseDraw();
        }

        /// <summary>Snap the indoor fade (tests and looks); live play eases it in <see cref="LateUpdate"/>.</summary>
        public void SetOutdoors(float outdoors) => _outdoors = Mathf.Clamp01(outdoors);

        /// <summary>Indoors is a building that holds the player (Core's <see cref="PlayerStorey"/>) or a
        /// boat cabin he has stepped into.</summary>
        public bool Indoors => PlayerStorey.Indoors || _inCabin;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            if (!_subscribed)
            {
                EventBus.Subscribe<CabinEntered>(OnCabinEntered);
                EventBus.Subscribe<CabinEntryLandingEntered>(OnCabinLanding);
                EventBus.Subscribe<CabinLeft>(OnCabinLeft);
                _subscribed = true;
            }
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            if (_subscribed)
            {
                EventBus.Unsubscribe<CabinEntered>(OnCabinEntered);
                EventBus.Unsubscribe<CabinEntryLandingEntered>(OnCabinLanding);
                EventBus.Unsubscribe<CabinLeft>(OnCabinLeft);
                _subscribed = false;
            }
            ReleaseDraw();
        }

        void OnDestroy() { ReleaseDraw(); if (_instance == this) _instance = null; }

        void OnCabinEntered(CabinEntered e) { _inCabin = true; _cabinHull = e.HullId; }
        void OnCabinLanding(CabinEntryLandingEntered e) { _inCabin = true; _cabinHull = e.HullId; }
        void OnCabinLeft(CabinLeft e) { if (e.HullId == _cabinHull) _inCabin = false; }

        void LateUpdate()
        {
            if (_def == null) return;
            float target = Indoors ? 0 : 1;
            float fade = _def.IndoorFadeSeconds;
            _outdoors = fade > 0 ? Mathf.MoveTowards(_outdoors, target, Time.unscaledDeltaTime / fade) : target;
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_def == null || !_def.Enabled || !_casting) { ReleaseDraw(); return; }
            if (HeldFrame.HasValue) { Prepare(camera, HeldFrame.Value); return; }
            var clock = GameServices.Clock; var environment = GameServices.Environment;
            if (clock == null || environment == null) { Hide(); return; }
            var weather = environment.Sample();
            IWaveWindSource wind = environment as IWaveWindSource;
            if (wind == null) { _heldWind.Wind = weather.WindVector; wind = _heldWind; }
            double seconds = clock.TotalSeconds;
            float cover = CloudShadowMath.Cover(_def, environment.WorldSeed, seconds,
                CloudShadowMath.Overcast(weather.Visibility, weather.SeaState01, _day));
            Prepare(camera, new CloudShadowFrame(environment.WorldSeed, seconds, wind, cover, Shader.GetGlobalFloat(SunElevationId)));
        }

        public static bool IsWorldCamera(Camera camera)
        {
            if (camera == null || !camera.orthographic || camera.cameraType != CameraType.Game || (camera.cullingMask & 1) == 0) return false;
            return !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.renderType == CameraRenderType.Base;
        }

        public void Prepare(Camera camera, CloudShadowFrame frame)
        {
            if (_def == null || !_def.Enabled || !_casting) { ReleaseDraw(); return; }
            if (!IsWorldCamera(camera)) { Hide(); return; }
            float strength = CloudShadowMath.Strength(_def, frame.Cover, frame.Elevation, _outdoors);
            LastStrength = strength;
            if (strength <= 0) { Hide(); return; }
            Advance(frame);
            EnsureDraw(frame.Seed);
            float half = camera.orthographicSize * _def.CullingOversize;
            _draw.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward *
                (camera.nearClipPlane + DayNightController.OverlayNearOffset), camera.transform.rotation);
            _draw.transform.localScale = new Vector3(half * camera.aspect * 2, half * 2, 1);
            _properties.SetVector(OffsetsId, Offsets);
            _properties.SetVector(ShapeId, new Vector4(Mathf.Clamp01(_def.Coverage.Evaluate(frame.Cover)),
                _def.Softness, strength, _def.SecondaryWeight));
            _properties.SetVector(ScaleId, new Vector4(_def.ScaleMetres, _def.SecondaryScale, _def.WorldPlaneZ, 0));
            _properties.SetColor(ColourId, _def.ShadeColour);
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = true;
        }

        void Advance(CloudShadowFrame frame)
        {
            // Every camera this frame asks for the same moment: work it out once.
            if (frame.Seconds == _preparedSeconds && frame.Seed == _preparedSeed && ReferenceEquals(frame.Wind, _preparedWind)) return;
            Offsets = _drift.Offsets(frame.Wind, frame.Seed, frame.Seconds, GameServices.SecondsPerDay, _def);
            _preparedSeconds = frame.Seconds; _preparedSeed = frame.Seed; _preparedWind = frame.Wind;
        }

        void EnsureDraw(int seed)
        {
            if (_draw != null && _noiseSeed == seed) return;
            if (_draw != null) ReleaseDraw();
            _noiseSeed = seed;
            int size = _def.NoiseSize;
            _noise = new Texture2D(size, size, TextureFormat.R8, false, true)
                { name = "CloudShadeNoise", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            _noise.SetPixelData(CloudShadowMath.BuildNoise(seed, size, _def.NoiseCells, _def.NoiseOctaves), 0);
            _noise.Apply(false, true);
            _material = new Material(_def.Shader) { name = "CloudShadow (runtime)", hideFlags = HideFlags.HideAndDontSave };
            _mesh = new Mesh { name = "CloudShadowQuad", hideFlags = HideFlags.HideAndDontSave };
            _mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            _mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; _mesh.RecalculateBounds();
            _draw = new GameObject("CloudShade") { hideFlags = HideFlags.HideAndDontSave };
            _draw.transform.SetParent(transform, false);
            _draw.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _draw.AddComponent<MeshRenderer>(); _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off; _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off; _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.sortingOrder = SortingBands.SunShade;
            var group = _draw.AddComponent<SortingGroup>(); group.sortingOrder = SortingBands.SunShade; group.sortAtRoot = true;
            _properties = new MaterialPropertyBlock(); _properties.SetTexture(NoiseId, _noise);
        }

        void Hide() { if (_renderer != null) _renderer.enabled = false; }
        void ReleaseDraw()
        {
            Hide();
            if (_draw != null) { _draw.SetActive(false); Dispose(_draw); }
            Dispose(_material); Dispose(_mesh); Dispose(_noise);
            _draw = null; _renderer = null; _mesh = null; _material = null; _noise = null; _properties = null;
        }
        static void Dispose(Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

        /// <summary>The fallback when the environment offers no wind law: today's wind, held.</summary>
        sealed class HeldWind : IWaveWindSource
        {
            public Vector2 Wind;
            public WaveWindSample WaveWindAt(double gameTimeSeconds) => new WaveWindSample(Wind, 0);
        }
    }
}
