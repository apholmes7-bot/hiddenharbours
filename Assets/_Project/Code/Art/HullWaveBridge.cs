using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Publishes the waves hulls make to the water shader. Owns the one shared
    /// <see cref="HullWaveField"/>, installs it on the Core <see cref="HullWaves"/> seam (hulls lay
    /// sources into it from their <see cref="FoamInjector"/>; splashes add through
    /// <see cref="HullWaves.AddRipple"/>), and each frame, after every injector has laid its sources,
    /// ages it to the clock and uploads the live sources as packed shader globals:
    ///
    /// <list type="bullet">
    /// <item><c>_HHHullWaveA[i]</c> = (x, y, height now, band radius now)</item>
    /// <item><c>_HHHullWaveB[i]</c> = (k, phase now, 1 / band width, reach)</item>
    /// <item><c>_HHHullWaveParams</c> = (live count, drawn lift per metre, 1 / spread metres, on)</item>
    /// <item><c>_HHHullWaveShade</c> = (face light, crest light, trough dark, crest foam)</item>
    /// <item><c>_HHHullWaveFoam</c> = (crest foam onset m, crest foam ramp m, 0, 0)</item>
    /// </list>
    ///
    /// <para>Ages and phases are worked out here in double, so the GPU only ever sees small numbers.
    /// Sources whose rings cannot reach the camera's view are not uploaded. The arrays are allocated
    /// once (rule 7). Self-installing: one hidden persistent host, like the wake emitter, so no scene or
    /// builder changes.</para>
    ///
    /// <para><c>on</c> = 0 (hull waves switched off in <see cref="GameConfig"/>, or this host absent)
    /// leaves the water exactly as it was, older drawn wake train included.</para>
    /// </summary>
    [DefaultExecutionOrder(60)]   // after FoamInjector (30), which lays this frame's sources
    public sealed class HullWaveBridge : MonoBehaviour
    {
        public static readonly int IdA = Shader.PropertyToID("_HHHullWaveA");
        public static readonly int IdB = Shader.PropertyToID("_HHHullWaveB");
        public static readonly int IdParams = Shader.PropertyToID("_HHHullWaveParams");
        public static readonly int IdShade = Shader.PropertyToID("_HHHullWaveShade");
        public static readonly int IdFoam = Shader.PropertyToID("_HHHullWaveFoam");

        /// <summary>Metres added round the camera's view before a ring counts as out of sight (the
        /// displaced lift moves water up the screen, and the view must not clip a crest).</summary>
        private const float ViewMarginMetres = 6f;

        private readonly HullWaveField _field = new HullWaveField(HullWaveField.ShaderCapacity);
        private readonly Vector4[] _a = new Vector4[HullWaveField.ShaderCapacity];
        private readonly Vector4[] _b = new Vector4[HullWaveField.ShaderCapacity];

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _installed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("HullWaveBridge") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<HullWaveBridge>();
        }

        private void OnEnable()
        {
            HullWaves.Install(_field);
            // The arrays' size is fixed by their first upload: always the full bound.
            Shader.SetGlobalVectorArray(IdA, _a);
            Shader.SetGlobalVectorArray(IdB, _b);
            Shader.SetGlobalVector(IdParams, Vector4.zero);
        }

        private void OnDisable()
        {
            HullWaves.Uninstall(_field);
            _field.Clear();
            Shader.SetGlobalVector(IdParams, Vector4.zero);
        }

        private void LateUpdate()
        {
            HullWaveSettings s = GameServices.HullWaves;
            if (!s.Enabled)
            {
                _field.Clear();
                Shader.SetGlobalVector(IdParams, Vector4.zero);
                return;
            }

            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.FlowSeconds : Time.timeAsDouble;
            _field.Limit = s.MaxSources;
            _field.Advance(now);

            bool cull = TryViewRect(out Rect view);
            int count = _field.Pack(_a, _b, view, cull);

            Shader.SetGlobalVectorArray(IdA, _a);
            Shader.SetGlobalVectorArray(IdB, _b);
            Shader.SetGlobalVector(IdParams, new Vector4(count, Mathf.Max(s.DrawnLift, 0f),
                s.SpreadMetres > 0f ? 1f / s.SpreadMetres : 0f, 1f));
            Shader.SetGlobalVector(IdShade, new Vector4(Mathf.Max(s.FaceLight, 0f), Mathf.Max(s.CrestLight, 0f),
                Mathf.Max(s.TroughDark, 0f), Mathf.Clamp01(s.CrestFoam)));
            Shader.SetGlobalVector(IdFoam, new Vector4(Mathf.Max(s.CrestFoamOnsetMetres, 0f),
                Mathf.Max(s.CrestFoamRampMetres, 1e-3f), 0f, 0f));
        }

        private static bool TryViewRect(out Rect view)
        {
            view = default;
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return false;
            float halfH = cam.orthographicSize + ViewMarginMetres;
            float halfW = cam.orthographicSize * cam.aspect + ViewMarginMetres;
            Vector3 p = cam.transform.position;
            view = new Rect(p.x - halfW, p.y - halfH, 2f * halfW, 2f * halfH);
            return true;
        }
    }
}
