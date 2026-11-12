using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE VILLAGE USES ITS BEACH</b> — the owner's 2026-11-11 ask: recreational beach activities by
    /// the local people of St Peters.
    ///
    /// <para><b>What it is.</b> The one <see cref="IRoutineDaySource"/>. When a villager asks for
    /// today, it hands <see cref="BeachPlanning"/> her authored stops, the ground, and the sea and sky
    /// at the hours of the day, and gives back a day in which one block of free time is spent on the
    /// beach (or null: the authored day). It moves nobody itself and has no Update: the villager's own
    /// <see cref="RoutinePlan.SampleAt"/> does the walking, exactly as on any other day.</para>
    ///
    /// <para><b>Nothing is saved and nothing is remembered</b> (rule 5). Ask twice for the same day and
    /// seed and the answer is the same; load a save at 19:30 and the people who should be on the sand
    /// are on it, where they would have been had the game run all day.</para>
    ///
    /// <para><b>Self-installing</b> (the <see cref="NpcConversationDirector"/> idiom): one hidden
    /// persistent host and a library from <c>Resources</c>, so a region gains a lived-on beach with no
    /// scene wiring and no scene re-save.</para>
    ///
    /// <para><b>Rule 4 clean.</b> Reads the clock, the environment (through Core's at-time seams
    /// <see cref="IWaveWindSource"/> and <see cref="IVisibilitySource"/>) and the tidal terrain from
    /// <see cref="GameServices"/>. It names no feature module and none names it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class BeachLifeDirector : MonoBehaviour, IRoutineDaySource, IBeachSky
    {
        /// <summary>How long a roster of the region's villagers is trusted, in real seconds. Villagers
        /// appear when a region loads; the roster only orders them so no two share a line.</summary>
        public const float RosterTrustSeconds = 3f;

        private static bool _installed;

        /// <summary>The one director, or null before the first scene.</summary>
        public static BeachLifeDirector Instance { get; private set; }

        private readonly List<BeachActivityDef> _activities = new List<BeachActivityDef>();
        private readonly List<string> _roster = new List<string>(16);
        private float _rosterAt = float.NegativeInfinity;
        private string _rosterRegion;
        private int _version = 1;
        private ITidalTerrain _terrainSeen;
        private IEnvironmentService _envSeen;
        private TideProfile _tideSeen;

        // The environment of the plan being made (IBeachSky is answered from these).
        private IEnvironmentService _env;
        private IWaveWindSource _wind;
        private IVisibilitySource _fog;

        /// <summary>How many activities were loaded. Zero is a working state: no library, no beach life,
        /// every day the authored day.</summary>
        public int ActivityCount => _activities.Count;

        /// <summary>
        /// Moves when the content is reloaded, the ground under the region is replaced, or the region's
        /// tide law changes (a region sets its own tide after the services are up, and a day planned
        /// against the previous region's tide would put people where this one's water goes). Compared
        /// here rather than heard about on an event: it costs a few compares per asking villager, and it
        /// cannot be lost to a service reset the way a subscription can.
        /// </summary>
        public int Version
        {
            get
            {
                ITidalTerrain terrain = GameServices.TidalTerrain;
                IEnvironmentService env = GameServices.Environment;
                TideProfile tide = env != null ? env.ActiveTideProfile : default;
                if (!ReferenceEquals(terrain, _terrainSeen) || !ReferenceEquals(env, _envSeen) ||
                    tide.MeanLevel != _tideSeen.MeanLevel || tide.Amplitude != _tideSeen.Amplitude ||
                    tide.PhaseHours != _tideSeen.PhaseHours)
                {
                    _terrainSeen = terrain;
                    _envSeen = env;
                    _tideSeen = tide;
                    _version++;
                }
                return _version;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("BeachLifeDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<BeachLifeDirector>();
        }

        private void Awake()
        {
            Instance = this;
            LoadLibrary(Resources.Load<BeachLifeLibrary>(BeachLifeLibrary.ResourcesPath));
        }

        private void OnEnable()
        {
            VillagerRoutine.DaySource = this;
        }

        private void OnDisable()
        {
            ClearGatherings();
            UnsubscribeTalk();
            if (ReferenceEquals(VillagerRoutine.DaySource, this)) VillagerRoutine.DaySource = null;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        /// <summary>Take a library (the boot path passes the Resources one; a fixture passes its own, or
        /// null for a village with no beach life). Keeps only the runnable activities, in order.</summary>
        public void LoadLibrary(BeachLifeLibrary library)
        {
            _activities.Clear();
            if (library != null && library.Activities != null)
                foreach (BeachActivityDef def in library.Activities)
                    if (def != null && def.IsRunnable) _activities.Add(def);
            ClearGatherings();
            _rosterAt = float.NegativeInfinity;
            _version++;
        }

        /// <inheritdoc/>
        public RoutinePlan PlanDay(VillagerRoutine villager, int dayIndex, float secondsPerGameHour,
                                   out BuildingInterior[] blockInteriors)
        {
            blockInteriors = null;
            if (_activities.Count == 0 || villager == null) return null;

            RoutineDef routine = villager.Routine;
            RoutineStop[] authored = villager.AuthoredStops;
            if (routine == null || authored == null || authored.Length < 3) return null;
            if (!AnyAuthoredFor(routine.RegionId)) return null;

            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (clock == null || env == null || terrain == null) return null;

            _env = env;
            _wind = env as IWaveWindSource;
            _fog = env as IVisibilitySource;

            var day = new BeachDay(env.WorldSeed, dayIndex, clock.Season, secondsPerGameHour);
            bool planned = BeachPlanning.TryPlanDay(
                routine.Id, authored, villager.Lanes, routine.WalkSpeedMetresPerSecond, _activities,
                routine.RegionId, RankOf(routine), terrain, this, day,
                out RoutineStop[] stops, out BeachOuting outing);

            _env = null;
            _wind = null;
            _fog = null;
            if (!planned) return null;
            NoteGathering(outing, dayIndex);

            return RoutinePlanner.BuildFromStops(routine.Id, stops, villager.Lanes,
                                                 routine.WalkSpeedMetresPerSecond,
                                                 routine.CatchUpSpeedMetresPerSecond,
                                                 out blockInteriors, out string _);
        }

        private bool AnyAuthoredFor(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return false;
            for (int i = 0; i < _activities.Count; i++)
                if (string.Equals(_activities[i].Beach.RegionId, regionId, System.StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// This routine's place among the routines being lived in its region, by id. Ordering by id
        /// (not by scene order, which nothing promises) is what makes two people's lines the same on
        /// every run of the same day.
        /// </summary>
        private int RankOf(RoutineDef routine)
        {
            float now = Time.unscaledTime;
            if (now - _rosterAt > RosterTrustSeconds ||
                !string.Equals(_rosterRegion, routine.RegionId, System.StringComparison.Ordinal))
            {
                _rosterAt = now;
                _rosterRegion = routine.RegionId;
                _roster.Clear();
                foreach (VillagerRoutine v in FindObjectsByType<VillagerRoutine>(FindObjectsSortMode.None))
                {
                    RoutineDef r = v != null ? v.Routine : null;
                    if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                    if (!string.Equals(r.RegionId, routine.RegionId, System.StringComparison.Ordinal)) continue;
                    if (!_roster.Contains(r.Id)) _roster.Add(r.Id);
                }
                _roster.Sort(System.StringComparer.Ordinal);
            }

            int rank = _roster.IndexOf(routine.Id);
            return rank < 0 ? 0 : rank;
        }

        // ---- IBeachSky: the environment, at a time ------------------------------------------------

        float IBeachSky.WaterLevelAt(double totalSeconds) => _env.WaterLevelAt(totalSeconds);

        float IBeachSky.WindStrengthAt(double totalSeconds) =>
            _wind != null ? _wind.WaveWindAt(totalSeconds).Wind.magnitude : 0f;

        float IBeachSky.SeaState01At(double totalSeconds) =>
            _wind != null ? _wind.WaveWindAt(totalSeconds).SeaState01 : _env.SeaState01At(totalSeconds);

        float IBeachSky.Visibility01At(double totalSeconds) =>
            _fog != null ? _fog.VisibilityAt(totalSeconds) : 1f;
    }
}
