using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The SNOW bridge for the foliage shaders. It turns the calendar into one global float,
    /// <c>_FoliageSnow</c> (0 bare .. 1 full cover), with <see cref="Shader.SetGlobalFloat(int, float)"/>,
    /// and every tree reads that one number with no per-object wiring. The pass-4 tree sheets carry their
    /// own snow map (the coverage at which each pixel turns white), so the shader only has to compare —
    /// the curve that decides how much snow there is today lives here, in <see cref="FoliageSnowMath"/>.
    ///
    /// <para><b>Named for all foliage, used by trees first.</b> Shrubs and grass may read the same global
    /// later (M2, with ground snow); nothing here is tree-specific, so they will need no second curve.</para>
    ///
    /// <para><b>Self-installing</b>, like <see cref="GrassWindBridge"/>: a
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns one hidden <c>[DontDestroyOnLoad]</c> host
    /// before the first scene, so nobody has to place anything.</para>
    ///
    /// <para><b>Once a day, never per frame (rule 7).</b> The cover changes a step a day, so the bridge has no
    /// <c>Update</c>. It publishes when a day starts (<see cref="DayStarted"/>, whose payload IS the day),
    /// when the season turns (<see cref="SeasonChanged"/>), and when a save lands (<see cref="GameLoaded"/> —
    /// a restore SEEKS the clock without replaying the day events, so without this a load would keep the
    /// snow of the session before it). Setting a global float allocates nothing.</para>
    ///
    /// <para><b>Recomputed, never saved (rule 5).</b> The value is a pure function of the clock's
    /// <c>(Season, DayOfSeason)</c> and the owner's <see cref="FoliageSnowSettings"/>. Seam discipline
    /// (rule 4): the clock, the calendar length and the curve come through <see cref="GameServices"/> only.
    /// With no clock (EditMode, pre-boot, an art scene with no services) it publishes nothing, so the global
    /// keeps its default of 0 and the trees draw bare — exactly as they drew before this bridge existed.</para>
    ///
    /// <para><b>The plate hook.</b> <see cref="OverrideCoverage"/> pins the cover for a screenshot or a test
    /// (snow at 0 / 50 / 100 % without touching the save or the clock); <see cref="ClearOverride"/> hands the
    /// global back to the calendar.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoliageSnowBridge : MonoBehaviour
    {
        /// <summary>The global the foliage shaders read, 0 (bare) .. 1 (full cover).</summary>
        public const string CoverageProperty = "_FoliageSnow";

        private static readonly int CoverageId = Shader.PropertyToID(CoverageProperty);

        private static bool _installed;
        private static bool _listening;
        private static bool _overridden;
        private static float _override;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _listening = false;
            _overridden = false;
            _override = 0f;
        }

        /// <summary>
        /// Spawn the single self-installing host before the first scene. Guarded so it never double-installs.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("FoliageSnowBridge") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<FoliageSnowBridge>();
        }

        private void OnEnable()
        {
            Listen();
            PublishFromClock();
        }

        private void OnDisable() => StopListening();

        /// <summary>Subscribe the day events, once. Internal so a test can drive the event path without a
        /// frame (EditMode never calls <c>OnEnable</c>).</summary>
        internal static void Listen()
        {
            if (_listening) return;
            _listening = true;
            EventBus.Subscribe<DayStarted>(OnDayStarted);
            EventBus.Subscribe<SeasonChanged>(OnSeasonChanged);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        internal static void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            EventBus.Unsubscribe<DayStarted>(OnDayStarted);
            EventBus.Unsubscribe<SeasonChanged>(OnSeasonChanged);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        private static void OnDayStarted(DayStarted e) => Publish(e.Season, e.DayOfSeason);
        private static void OnSeasonChanged(SeasonChanged _) => PublishFromClock();
        private static void OnGameLoaded(GameLoaded _) => PublishFromClock();

        /// <summary>
        /// The cover the trees wear on this calendar day: the owner's curve through
        /// <see cref="GameServices.FoliageSnow"/> and <see cref="GameServices.DaysPerSeason"/>, unless a plate
        /// has pinned it with <see cref="OverrideCoverage"/>.
        /// </summary>
        public static float CoverageFor(Season season, int dayOfSeason)
        {
            if (_overridden) return _override;
            FoliageSnowSettings settings = GameServices.FoliageSnow;
            return FoliageSnowMath.Coverage(season, dayOfSeason, GameServices.DaysPerSeason, settings);
        }

        /// <summary>Publish the cover for a calendar day. What <see cref="DayStarted"/> calls, with its own payload.</summary>
        public static void Publish(Season season, int dayOfSeason)
            => Shader.SetGlobalFloat(CoverageId, CoverageFor(season, dayOfSeason));

        /// <summary>
        /// Publish the cover for the day the clock is on. With no clock this publishes nothing (the global
        /// keeps what it had, 0 unless something set it) — unless a plate override is live, which wins.
        /// </summary>
        public static void PublishFromClock()
        {
            var clock = GameServices.Clock;
            if (clock != null) Publish(clock.Season, clock.DayOfSeason);
            else if (_overridden) Shader.SetGlobalFloat(CoverageId, _override);
        }

        /// <summary>
        /// Pin the cover for a plate or a test (clamped to 0..1), and publish it now. The clock and the
        /// save are untouched; the day events keep firing but publish the pinned value until
        /// <see cref="ClearOverride"/>.
        /// </summary>
        public static void OverrideCoverage(float coverage01)
        {
            _overridden = true;
            _override = Mathf.Clamp01(coverage01);
            Shader.SetGlobalFloat(CoverageId, _override);
        }

        /// <summary>
        /// Hand the global back to the calendar: republish from the clock, or back to the bare 0 when there
        /// is no clock (the pinned value must not outlive the plate that set it).
        /// </summary>
        public static void ClearOverride()
        {
            _overridden = false;
            if (GameServices.Clock != null) PublishFromClock();
            else Shader.SetGlobalFloat(CoverageId, 0f);
        }

        /// <summary>True while a plate has the cover pinned.</summary>
        public static bool IsOverridden => _overridden;

        /// <summary>The value the shaders read right now.</summary>
        public static float Published => Shader.GetGlobalFloat(CoverageId);
    }
}
