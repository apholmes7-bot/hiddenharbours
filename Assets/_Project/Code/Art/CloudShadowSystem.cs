using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Art
{
    /// <summary>One world-anchored multiply per world camera. No simulation, stencil claim or saved state.</summary>
    [DisallowMultipleComponent]
    public sealed class CloudShadowSystem : MonoBehaviour
    {
        static readonly int NoiseId = Shader.PropertyToID("_CloudNoise");
        static readonly int OffsetsId = Shader.PropertyToID("_CloudOffsets");
        static readonly int ShapeId = Shader.PropertyToID("_CloudShape");
        static readonly int ScaleId = Shader.PropertyToID("_CloudScale");
        static CloudShadowSystem _instance;
        CloudShadowDef _def;
        DayNightProfile _day;
        GameObject _draw;
        Mesh _mesh;
        Material _material;
        Texture2D _noise;
        MeshRenderer _renderer;
        MaterialPropertyBlock _properties;
        bool _casting = true, _initialized;
        int _seed;
        double _lastTime, _x, _y, _secondaryX, _secondaryY;

        public static CloudShadowSystem Instance => _instance;
        public bool Casting => _casting;
        public CloudShadowDef Definition => _def;
        public MeshRenderer ShadowRenderer => _renderer;
        public int DrawCount => _renderer != null && _renderer.enabled && _renderer.gameObject.activeInHierarchy ? 1 : 0;
        public Vector4 Offsets { get; private set; }
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
            ReleaseDraw(); _initialized = false;
            _def = def != null && def.IsValid() ? def : null;
            _day = Resources.Load<DayNightProfile>("DayNightProfile");
        }

        public void SetCasting(bool enabled)
        {
            _casting = enabled;
            if (!enabled) ReleaseDraw();
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += BeginCamera;
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeginCamera; ReleaseDraw(); }
        void OnDestroy() { ReleaseDraw(); if (_instance == this) _instance = null; }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_def == null || !_def.Enabled || !_casting) { ReleaseDraw(); return; }
            if (HeldFrame.HasValue) { Prepare(camera, HeldFrame.Value); return; }
            var clock = GameServices.Clock; var environment = GameServices.Environment;
            if (clock == null || environment == null) { Hide(); return; }
            var weather = environment.Sample();
            Prepare(camera, new CloudShadowFrame(environment.WorldSeed, clock.TotalSeconds, weather.WindVector,
                CloudShadowMath.Overcast(weather.Visibility, weather.SeaState01, _day), Shader.GetGlobalFloat("_SunElevation")));
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
            Advance(frame);
            float strength = CloudShadowMath.Strength(_def, frame.Overcast, frame.Elevation);
            if (strength <= 0) { Hide(); return; }
            EnsureDraw();
            float half = camera.orthographicSize * _def.CullingOversize;
            _draw.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward *
                (camera.nearClipPlane + DayNightController.OverlayNearOffset), camera.transform.rotation);
            _draw.transform.localScale = new Vector3(half * camera.aspect * 2, half * 2, 1);
            _properties.SetVector(OffsetsId, Offsets);
            _properties.SetVector(ShapeId, new Vector4(Mathf.Clamp01(_def.Coverage.Evaluate(frame.Overcast)),
                _def.Softness, strength, _def.SecondaryWeight));
            _properties.SetVector(ScaleId, new Vector4(_def.ScaleMetres, _def.SecondaryScale, _def.WorldPlaneZ, 0));
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = true;
        }

        void Advance(CloudShadowFrame frame)
        {
            if (!_initialized || frame.Seed != _seed || frame.Seconds < _lastTime)
            {
                _seed = frame.Seed;
                var origin = CloudShadowMath.SeedOrigin(_seed, _def);
                _x = origin.x; _y = origin.y; _secondaryX = origin.y; _secondaryY = origin.x;
                _lastTime = 0; _initialized = true;
                if (_noise != null) ReleaseDraw();
            }
            double delta = frame.Seconds - _lastTime;
            double rate = _def.DriftMetresPerGameSecondPerMps;
            _x = CloudShadowMath.Wrap(_x + frame.Wind.x * delta * rate, _def.ScaleMetres);
            _y = CloudShadowMath.Wrap(_y + frame.Wind.y * delta * rate, _def.ScaleMetres);
            _secondaryX = CloudShadowMath.Wrap(_secondaryX + frame.Wind.x * delta * rate * _def.SecondaryRate, _def.ScaleMetres);
            _secondaryY = CloudShadowMath.Wrap(_secondaryY + frame.Wind.y * delta * rate * _def.SecondaryRate, _def.ScaleMetres);
            _lastTime = frame.Seconds;
            Offsets = new Vector4((float)_x, (float)_y, (float)_secondaryX, (float)_secondaryY);
        }

        void EnsureDraw()
        {
            if (_draw != null) return;
            _noise = new Texture2D(_def.NoiseSize, _def.NoiseSize, TextureFormat.RGBA32, false, true)
                { name = "CloudNoise", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[_def.NoiseSize * _def.NoiseSize];
            var random = new System.Random(_seed);
            for (int i = 0; i < pixels.Length; i++) { byte value = (byte)random.Next(256); pixels[i] = new Color32(value, value, value, byte.MaxValue); }
            _noise.SetPixels32(pixels); _noise.Apply(false, true);
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
    }
}
