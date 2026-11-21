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
    /// <item><c>_HHHullWaveB[i]</c> = (k, phase now, ±1 / band width, packed travel: 0 = a whole ring)</item>
    /// <item><c>_HHHullWaveParams</c> = (live count, drawn lift per metre, 1 / spread metres, on)</item>
    /// <item><c>_HHHullWaveShade</c> = (face light, crest light, trough dark, crest foam)</item>
    /// <item><c>_HHHullWaveFoam</c> = (crest foam onset m, crest foam ramp m, 0, 0)</item>
    /// <item><c>_HHHullWaveLook</c> = (shade ceiling, breakup share, 1 / breakup metres, wobble metres)</item>
    /// <item><c>_HHHullWaveArcs</c> = (arc share, arcs round a ring, 0, 0): rings packed with a negative 1/width</item>
    /// <item><c>_HHHullWaveV</c> = (window low, 1 / window span, share, 0): the V arms of a ring packed with a
    /// travel direction (a hull under motor or sail, owner 11-21; <see cref="HullWaveMath.VArmsVector"/>)</item>
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
        public static readonly int IdLook = Shader.PropertyToID("_HHHullWaveLook");
        public static readonly int IdFoamAge = Shader.PropertyToID("_HHHullWaveFoamAge");
        public static readonly int IdLowLight = Shader.PropertyToID("_HHHullWaveLowLight");
        public static readonly int IdArcs = Shader.PropertyToID("_HHHullWaveArcs");
        public static readonly int IdV = Shader.PropertyToID("_HHHullWaveV");

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
            Shader.SetGlobalVector(IdParams, Vector4.zero); Shader.SetGlobalVector(IdFoamAge, Vector4.zero);
        }

        private void OnDisable()
        {
            HullWaves.Uninstall(_field);
            _field.Clear();
            Shader.SetGlobalVector(IdParams, Vector4.zero); Shader.SetGlobalVector(IdFoamAge, Vector4.zero);
        }

        private void LateUpdate()
        {
            HullWaveSettings s = GameServices.HullWaves;
            if (!s.Enabled)
            {
                _field.Clear();
                Shader.SetGlobalVector(IdParams, Vector4.zero); Shader.SetGlobalVector(IdFoamAge, Vector4.zero);
                return;
            }

            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.FlowSeconds : Time.timeAsDouble;
            _field.Limit = s.MaxSources;
            _field.Advance(now);
            Vector3 arms = HullWaveMath.VArmsVector(in s);
            _field.VArms = arms;

            bool cull = TryViewRect(out Rect view);
            int count = _field.Pack(_a, _b, view, cull);

            Shader.SetGlobalVectorArray(IdA, _a);
            Shader.SetGlobalVectorArray(IdB, _b);
            Shader.SetGlobalVector(IdParams, new Vector4(count, Mathf.Max(s.DrawnLift, 0f),
                s.SpreadMetres > 0f ? 1f / s.SpreadMetres : 0f, 1f));
            Shader.SetGlobalVector(IdShade, new Vector4(Mathf.Max(s.FaceLight, 0f), Mathf.Max(s.CrestLight, 0f),
                Mathf.Max(s.TroughDark, 0f), Mathf.Clamp01(s.CrestFoam)));
            Shader.SetGlobalVector(IdFoam, new Vector4(Mathf.Max(s.CrestFoamOnsetMetres, 0f),
                Mathf.Max(s.CrestFoamRampMetres, 1e-3f), Mathf.Clamp01(s.TroughCeilingShare), 0f));
            Shader.SetGlobalVector(IdFoamAge, new Vector4(Mathf.Max(s.CrestFoamSteepness, 0f),
                Mathf.Max(s.CrestFoamSteepnessRamp, 1e-3f), Mathf.Clamp01(s.FoamAgeTear), 0f));
            Shader.SetGlobalVector(IdLook, new Vector4(Mathf.Max(s.ShadeCeiling, 0f), Mathf.Clamp01(s.BreakupShare),
                1f / Mathf.Max(s.BreakupMetres, 0.05f), Mathf.Max(s.WobbleMetres, 0f)));
            Shader.SetGlobalVector(IdLowLight, new Vector4(Mathf.Max(s.LowLightGain, 0f),
                Mathf.Clamp01(s.LowLightStartLuminance), Mathf.Clamp01(s.LowLightFullLuminance), 0f));
            Shader.SetGlobalVector(IdArcs, ArcsVector(in s));
            Shader.SetGlobalVector(IdV, new Vector4(arms.x, arms.y, arms.z, 0f));
        }

        /// <summary>The short hulls' broken rings for the shader: (arc share, whole arcs round the ring, 0, 0).</summary>
        public static Vector4 ArcsVector(in HullWaveSettings s)
            => new Vector4(Mathf.Clamp01(s.SmallHullArcShare), Mathf.Max(1f, Mathf.Round(s.SmallHullArcLobes)), 0f, 0f);

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
