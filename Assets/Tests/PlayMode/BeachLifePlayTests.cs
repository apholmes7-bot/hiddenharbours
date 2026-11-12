using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>SHE SPENDS HER EVENING ON THE BEACH, WHEN THE DAY ALLOWS IT</b> — the visible end of beach
    /// life, on a running villager: with the clock at seven in the evening she is standing on the sand
    /// above the water; in a gale, or with the tide over the sand, she is on the green as her authored
    /// day says; and the rest of her day is where it always was.
    ///
    /// <para>The same fixture rules as <c>VillagerRoutinePlayTests</c>: a clock the test sets, a
    /// synthetic village (no region is loaded), and no rendering at all (CI has no graphics device).
    /// The director is the real self-installed one, given this fixture's library and handed the
    /// shipped one back in TearDown.</para>
    /// </summary>
    public class BeachLifePlayTests
    {
        const string Region = "region.playtest_beach";

        sealed class DrivenClock : IGameClock
        {
            public float Hour;
            public double TotalSeconds => Hour / 24.0 * GameConfig.DefaultSecondsPerDay;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => Hour;
            public float DayFraction => Hour / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        sealed class FakeSea : IEnvironmentService, IWaveWindSource, IVisibilitySource
        {
            public float Water;
            public float WindStrength = 2f;
            public float Visibility = 1f;
            public int WorldSeed { get; set; } = 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Water;
            public float WaterLevelAt(double totalSeconds) => Water;
            public float SeaState01At(double totalSeconds) => Mathf.Clamp01(WindStrength / 14f);
            public WaveWindSample WaveWindAt(double t) =>
                new WaveWindSample(new Vector2(WindStrength, 0f), Mathf.Clamp01(WindStrength / 14f));
            public float VisibilityAt(double t) => Visibility;
        }

        /// <summary>A plain beach: 5 m up at the head of the sand, falling 0.4 m a metre to the west.</summary>
        sealed class Slope : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x >= SandTopX ? 5f : 5f + (p.x - SandTopX) * 0.4f;
        }

        const float SandTopX = -42f;
        static readonly Vector2 Green = new Vector2(0f, 0f);
        static readonly Vector2 Head = new Vector2(-40f, 0f);
        static readonly Vector2 Work = new Vector2(0f, 12f);
        static readonly Vector2 Bed = new Vector2(0f, -12f);

        readonly List<Object> _spawned = new();
        DrivenClock _clock;
        FakeSea _sea;
        Slope _slope;
        VillagerRoutine _villager;
        BeachActivityDef _activity;

        [SetUp]
        public void SetUp()
        {
            Spawn("AudioListener").AddComponent<AudioListener>();

            GameServices.Reset();
            _clock = new DrivenClock { Hour = 6f };
            _sea = new FakeSea();
            _slope = new Slope();
            GameServices.Clock = _clock;
            GameServices.Environment = _sea;
            GameServices.TidalTerrain = _slope;

            var lanes = Spawn("RoutineLanes").AddComponent<RoutineLanes>();
            lanes.Configure(
                nodePositions: new[] { Green, Head },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0 },
                nodeNames: new[] { "green", "head" },
                viaStart: new int[2], viaCount: new int[2], via: System.Array.Empty<Vector2>());
            Assert.That(lanes.Validate(), Is.Empty);

            var stations = Spawn("RoutineStations").AddComponent<RoutineStations>();
            stations.Configure(new[]
            {
                new RoutineStationEntry { Id = "s.work", Position = Work, LaneNodeName = "green", Why = "work" },
                new RoutineStationEntry { Id = "s.green", Position = Green, LaneNodeName = "green", Why = "green" },
                new RoutineStationEntry { Id = "s.bed", Position = Bed, LaneNodeName = "green", Why = "bed" },
            });

            var routine = Make<RoutineDef>();
            routine.Id = "routine.playtest_beach";
            routine.RegionId = Region;
            routine.WalkSpeedMetresPerSecond = 1.2f;
            routine.ScheduleJitterMinutes = 0f;
            routine.Entries = new[]
            {
                new RoutineEntry { StartHour = 7f, StationId = "s.work", Activity = RoutineActivity.Work },
                new RoutineEntry { StartHour = 17f, StationId = "s.green", Activity = RoutineActivity.Recreation },
                new RoutineEntry { StartHour = 21.5f, StationId = "s.bed", Activity = RoutineActivity.Home },
            };

            var beach = Make<BeachDef>();
            beach.Id = "beach.playtest";
            beach.RegionId = Region;
            beach.LaneNodeName = "head";
            beach.Spots = new[]
            {
                new BeachTransect { Top = new Vector2(SandTopX, -3f), Toe = new Vector2(-60f, -3f), Why = "south" },
                new BeachTransect { Top = new Vector2(SandTopX, 3f), Toe = new Vector2(-60f, 3f), Why = "north" },
            };
            beach.Promenade = beach.Spots;
            beach.Flats = beach.Spots;

            _activity = Make<BeachActivityDef>();
            _activity.Id = "beachactivity.playtest_watch";
            _activity.Beach = beach;
            _activity.Kind = BeachActivityKind.Watch;
            _activity.ChancePerDay = 1f;
            _activity.MaxWindStrength = 6f;
            _activity.MaxSeaState01 = 0.6f;
            _activity.ClearanceMetres = 0.35f;
            _activity.MinDepth01 = 0.7f;
            _activity.MaxDepth01 = 1f;

            var library = Make<BeachLifeLibrary>();
            library.Activities = new[] { _activity };
            Assert.That(BeachLifeDirector.Instance, Is.Not.Null, "the director installs itself before the first scene");
            BeachLifeDirector.Instance.LoadLibrary(library);

            var go = Spawn("Villager");
            go.transform.position = new Vector3(Green.x, Green.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            _villager = go.AddComponent<VillagerRoutine>();
            _villager.Configure(routine, stations, lanes, renderer);
        }

        [TearDown]
        public void TearDown()
        {
            if (BeachLifeDirector.Instance != null)
                BeachLifeDirector.Instance.LoadLibrary(
                    Resources.Load<BeachLifeLibrary>(BeachLifeLibrary.ResourcesPath));
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        T Make<T>() where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _spawned.Add(made);
            return made;
        }

        IEnumerator At(float hour)
        {
            _clock.Hour = hour;
            yield return null;
            yield return null;
        }

        Vector2 Where => _villager.transform.position;

        [UnityTest]
        public IEnumerator OnAFairEvening_SheStandsOnTheSand_AboveTheWater()
        {
            yield return At(19f);
            Assert.That(_villager.IsLiving, Is.True);
            Assert.That(_villager.IsVariedToday, Is.True, "a fair summer evening with free time is a beach evening");

            Assert.That(Where.x, Is.LessThan(SandTopX), $"she is on the sand, not the green: {Where}");
            Assert.That(_slope.ElevationAt(Where), Is.GreaterThanOrEqualTo(_sea.Water + _activity.ClearanceMetres - 0.01f),
                        $"and above the water by the activity's clearance: {Where}");
            Assert.That(_villager.Today.SampleAt(19f, GameConfig.DefaultSecondsPerDay / 24f).Walking, Is.False,
                        "standing, looking out");
        }

        [UnityTest]
        public IEnumerator TheRestOfHerDay_IsWhereItAlwaysWas()
        {
            yield return At(12f);
            Assert.That(Vector2.Distance(Where, Work), Is.LessThan(0.05f), "at work at noon");
            yield return At(23.5f);
            Assert.That(Vector2.Distance(Where, Bed), Is.LessThan(0.05f), "home at night");
        }

        [UnityTest]
        public IEnumerator SheWalksThere_NobodyAppearsOnTheSand()
        {
            // 17:00 she sets off from work; a quarter hour on she is on the lane, between the two.
            yield return At(17.25f);
            Assert.That(_villager.Today.SampleAt(17.25f, GameConfig.DefaultSecondsPerDay / 24f).Walking, Is.True);
            Assert.That(Where.x, Is.LessThan(0.01f).And.GreaterThan(SandTopX - 0.01f),
                        $"on her way along the lane: {Where}");
        }

        [UnityTest]
        public IEnumerator InAGale_SheStaysOnTheGreen()
        {
            GameServices.Environment = new FakeSea { WindStrength = 12f };
            yield return At(19f);
            Assert.That(_villager.IsVariedToday, Is.False);
            Assert.That(Vector2.Distance(Where, Green), Is.LessThan(0.05f), $"the authored evening: {Where}");
        }

        [UnityTest]
        public IEnumerator WithTheTideOverTheSand_SheStaysOnTheGreen()
        {
            GameServices.Environment = new FakeSea { Water = 4.9f };
            yield return At(19f);
            Assert.That(_villager.IsVariedToday, Is.False, "there is no dry sand to stand on");
            Assert.That(Vector2.Distance(Where, Green), Is.LessThan(0.05f), $"the authored evening: {Where}");
        }

        [UnityTest]
        public IEnumerator WithNoLibrary_EveryDayIsTheAuthoredDay()
        {
            BeachLifeDirector.Instance.LoadLibrary(null);
            yield return At(19f);
            Assert.That(_villager.IsVariedToday, Is.False);
            Assert.That(Vector2.Distance(Where, Green), Is.LessThan(0.05f));
        }
    }
}
