using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The post-processing GRADE that reads the sea's mood (juice charter PR 1 — P1 "The Sea Has Moods",
    /// P5 "Cozy but with Teeth"). One global URP <see cref="Volume"/>, one runtime profile, and a blend of
    /// the owner's five authored <see cref="MoodGrade"/> keys chosen by the facts the world already
    /// publishes: the hour (against <see cref="DayNightProfile"/>'s sunrise/sunset), the visibility, the
    /// sea state, and the region. Warm lift at golden hour, cold shadows and blooming lamps at night, the
    /// vignette closing in as the fog does — the art bible's §4.2 in numbers.
    ///
    /// <para><b>Self-installing (mirrors <see cref="DayNightController"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns one hidden <c>DontDestroyOnLoad</c> host before
    /// the first scene, carrying the Volume. Nothing is placed in any scene — St Peters is not rebuilt,
    /// no builder is touched. The host turns <c>renderPostProcessing</c> on for the main camera at runtime
    /// (the scenes ship with it off) and turns it back off if the owner disables the grade.</para>
    ///
    /// <para><b>Seam discipline (rule 4) &amp; determinism (rule 5).</b> Reads the clock, the environment,
    /// the region id and the config ONLY through <see cref="GameServices"/>; never writes them. The grade
    /// is a pure function of those facts (<see cref="MoodGradeMath"/>) — no accumulator, no smoothing,
    /// nothing saved.</para>
    ///
    /// <para><b>Budget (rule 7).</b> One Volume; at most <see cref="MoodGradeStack.MaxActiveEffects"/>
    /// overrides active (the stack caps it); the blend runs on a throttled tick at
    /// <c>GameConfig.Juice.GradeRefreshHz</c>, not per frame; no per-tick allocation after Awake. The frame
    /// cost of the post pass itself is measured on the owner's slot (profiler ms at the St Peters landing,
    /// before/after) — if it exceeds 1.0 ms at 1080p on the RTX 4060, an effect is cut (charter §2).</para>
    ///
    /// <para><b>UI is untouched.</b> Every canvas in the project is Screen Space – Overlay, which URP
    /// composites AFTER post-processing, so the notebook and HUD are never graded.</para>
    /// <para><b>Fixture guard (a self-installing host runs in EVERY scene and EVERY PlayMode test).</b>
    /// The host installs everywhere, but it GRADES only while three facts hold, re-read every tick
    /// (<see cref="MayGrade"/>): the camera it would switch post-processing on for is the persistent
    /// core's (its GameObject lives in the <c>DontDestroyOnLoad</c> scene, where <c>PersistentObject</c>
    /// promotes the Main Camera — a camera a fixture builds lives in the fixture's own scene); a region
    /// anchor has reported through <see cref="GameServices.CurrentRegionId"/>; and there is a graphics
    /// device (URP post-processing on the Null device CI runs is a pass with a cost and no picture).
    /// Otherwise the Volume stays disabled and no camera flag is touched, so a PlayMode fixture with its
    /// own camera and no region sees exactly what it saw before this host existed.
    /// <c>GameConfig.Juice.GradeEnabled</c> is the OWNER's switch; it is not this guard.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoodGradeDirector : MonoBehaviour
    {
        private const string ProfileResourcePath         = "MoodGradeProfile";  // Resources/MoodGradeProfile.asset
        private const string RegionOverridesResourceDir  = "MoodGrade";         // Resources/MoodGrade/*.asset
        private const string DayNightProfileResourcePath = "DayNightProfile";   // sunrise/sunset live there

        /// <summary>
        /// Above the URP global-settings default volume (0) and any scene volume a builder might add later,
        /// so the grade is the one that wins.
        /// </summary>
        public const float VolumePriority = 100f;

        [Tooltip("Hour (0..24) used when there is NO clock yet (a bare art scene / pre-boot), so such scenes " +
                 "grade as plain daylight rather than a stale look.")]
        [Range(0f, 24f)] [SerializeField] private float _fallbackHour = 12f;

        private MoodGradeProfile _profile;
        private DayNightProfile _dayNight;
        private readonly Dictionary<string, MoodGradeRegionOverride> _regions = new Dictionary<string, MoodGradeRegionOverride>();

        private Volume _volume;
        private VolumeProfile _runtimeProfile;
        private MoodGradeStack _stack;
        private float _timer;
        private Camera _camera;
        private bool _postEnabledByUs;

        /// <summary>The single installed host (null before install / in EditMode).</summary>
        public static MoodGradeDirector Instance { get; private set; }

        /// <summary>What the last tick wrote — read by the slot's plate fixture, never by gameplay.</summary>
        public MoodGrade   LastGrade   { get; private set; }
        public MoodWeights LastWeights { get; private set; }
        public string      LastRegionId { get; private set; }

        /// <summary>The master strength the last tick used — see <see cref="StrengthFor"/>. 0 is the
        /// pre-juice frame; the shipped asset carries a fraction (owner's ruling, 2026-09-09).</summary>
        public float       LastStrength { get; private set; }

        public int ActiveEffectCount  => _stack?.ActiveCount ?? 0;
        public int DroppedEffectCount => _stack?.DroppedCount ?? 0;
        public bool GradeIsOn => _volume != null && _volume.enabled;

        /// <summary>What the last tick decided under <see cref="MayGrade"/> — read by tests and the slot.</summary>
        public bool LastTickGraded { get; private set; }

        /// <summary>The scene <c>DontDestroyOnLoad</c> promotes root objects into; the persistent core's camera lives there.</summary>
        public const string PersistentSceneName = "DontDestroyOnLoad";

        /// <summary>
        /// Pure: the grade runs only for the persistent core's camera, in a region an anchor has reported,
        /// on a real graphics device. Every other camera (a fixture's), every pre-boot tick and the Null
        /// device leave the frame untouched.
        /// </summary>
        public static bool MayGrade(bool cameraIsPersistent, bool regionReported, bool hasGraphicsDevice)
            => cameraIsPersistent && regionReported && hasGraphicsDevice;

        /// <summary>True for the persistent core's camera; false for a camera a fixture built in its own scene, and for null.</summary>
        public static bool IsPersistentCamera(Camera cam)
            => cam != null && cam.gameObject.scene.name == PersistentSceneName;

        /// <summary>False on the Null device (CI, <c>-nographics</c>) — the same probe the Art render features use.</summary>
        public static bool HasGraphicsDevice => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("MoodGradeDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<MoodGradeDirector>();
        }

        private void Awake()
        {
            Instance = this;

            _profile = Resources.Load<MoodGradeProfile>(ProfileResourcePath);
            if (_profile == null) _profile = MoodGradeProfile.CreateDefault();

            _dayNight = Resources.Load<DayNightProfile>(DayNightProfileResourcePath);
            if (_dayNight == null) _dayNight = DayNightProfile.CreateDefault();

            foreach (var r in Resources.LoadAll<MoodGradeRegionOverride>(RegionOverridesResourceDir))
            {
                if (r == null || string.IsNullOrEmpty(r.RegionId)) continue;
                _regions[r.RegionId] = r;   // last one wins; the asset test forbids duplicates
            }

            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _runtimeProfile.name = "MoodGrade (runtime)";
            _runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            _stack = MoodGradeStack.Build(_runtimeProfile);

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = VolumePriority;
            _volume.weight = 1f;
            _volume.profile = _runtimeProfile;
        }

        private void OnEnable()
        {
            _timer = 0f;
            Tick();   // the first frame is graded, not a stale default
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SetCameraPost(false);
            // The test runner tears the scene down in edit mode at exit: Destroy is an error there.
            if (_runtimeProfile != null)
            {
                if (Application.isPlaying) Destroy(_runtimeProfile); else DestroyImmediate(_runtimeProfile);
            }
        }

        private void Update()
        {
            // Unscaled: the look keeps following the clock through a pause menu or a hit-stop.
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            float hz = CurrentJuice().GradeRefreshHz;
            _timer = hz > 0f ? 1f / hz : 0.1f;
            Tick();
        }


        /// <summary>
        /// WHICH master strength is in force right now: <c>GradeIntroStrength</c> while the arrival
        /// opening is running (<see cref="GameServices.OpeningCinematicRunning"/> — a Core fact, never a
        /// scene name), <c>GradeStrength</c> in ordinary play. The owner ruled on 2026-09-09 that the
        /// full-strength grade filtered the world but that the intro may keep it, softened; this is
        /// where the two numbers meet, and it is the only place that choice is made.
        /// </summary>
        public static float StrengthFor(in JuiceSettings juice) =>
            GameServices.OpeningCinematicRunning ? juice.GradeIntroStrength : juice.GradeStrength;
        private static JuiceSettings CurrentJuice()
        {
            var cfg = GameServices.Config;
            return cfg != null ? cfg.Juice : JuiceSettings.Default;
        }

        /// <summary>One evaluation: read the facts through Core, blend, write the volume.</summary>
        public void Tick()
        {
            var juice = CurrentJuice();
            if (!juice.GradeEnabled)
            {
                if (_volume != null) _volume.enabled = false;
                SetCameraPost(false);
                return;
            }
            var cam = ResolveCamera();
            if (!MayGrade(IsPersistentCamera(cam), !string.IsNullOrEmpty(GameServices.CurrentRegionId), HasGraphicsDevice))
            {
                // The fixture guard: not the persistent camera, no region reported, or no device — leave the frame alone.
                LastTickGraded = false;
                if (_volume != null) _volume.enabled = false;
                SetCameraPost(false);
                return;
            }
            LastTickGraded = true;
            if (_volume != null && !_volume.enabled) _volume.enabled = true;

            var clock = GameServices.Clock;
            float hour = clock != null ? clock.HourOfDay : _fallbackHour;

            float visibility = 1f, seaState01 = 0f;
            var env = GameServices.Environment;
            if (env != null)
            {
                var s = env.Sample();
                visibility = s.Visibility;
                seaState01 = s.SeaState01;
            }

            string regionId = GameServices.CurrentRegionId;
            MoodGradeRegionOverride region = null;
            if (!string.IsNullOrEmpty(regionId)) _regions.TryGetValue(regionId, out region);

            float strength = StrengthFor(juice);
            var grade = MoodGradeMath.Evaluate(_profile, region, hour,
                                               _dayNight.SunriseHour, _dayNight.SunsetHour,
                                               visibility, seaState01, juice, strength, out var weights);
            _stack.Write(in grade);

            LastGrade = grade;
            LastWeights = weights;
            LastRegionId = regionId;
            LastStrength = strength;

            SetCameraPost(true);
        }

        /// <summary>The camera the grade would switch post on for. Re-resolved when the cached one is gone or off.</summary>
        private Camera ResolveCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            return _camera;
        }

        /// <summary>
        /// Post-processing is a per-camera flag the scenes ship OFF. Turn it on for the main camera while
        /// the grade is on, and back off (only if we were the one who set it) when it is not.
        /// </summary>
        private void SetCameraPost(bool on)
        {
            if (ResolveCamera() == null) return;
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            if (on)
            {
                if (!data.renderPostProcessing)
                {
                    data.renderPostProcessing = true;
                    _postEnabledByUs = true;
                }
            }
            else if (_postEnabledByUs && data.renderPostProcessing)
            {
                data.renderPostProcessing = false;
                _postEnabledByUs = false;
            }
        }
    }
}
