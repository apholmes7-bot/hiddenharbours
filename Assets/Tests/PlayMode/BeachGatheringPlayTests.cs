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
    /// <b>THE VILLAGE GATHERS ROUND A FIRE ON THE SAND</b> — the visible end of a beach gathering, on two
    /// running villagers: on the evening of a gathering both stand round ONE place, facing it; the fire
    /// is there from the first arrival, stays a while after the last of them has gone, and is gone
    /// after that; and they say a few lines to each other while they are there.
    ///
    /// <para>The same fixture rules as <c>BeachLifePlayTests</c>: a clock the test sets, a synthetic
    /// village, no rendering (the fire is asserted as an object at a place; how it is drawn is the
    /// presentation service's business and CI has no graphics device).</para>
    /// </summary>
    public class BeachGatheringPlayTests
    {
        const string Region = "region.playtest_gathering";

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
            public int WorldSeed { get; set; } = 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Water;
            public float WaterLevelAt(double totalSeconds) => Water;
            public float SeaState01At(double totalSeconds) => Mathf.Clamp01(WindStrength / 14f);
            public WaveWindSample WaveWindAt(double t) =>
                new WaveWindSample(new Vector2(WindStrength, 0f), Mathf.Clamp01(WindStrength / 14f));
            public float VisibilityAt(double t) => 1f;
        }

        /// <summary>A plain beach: 5 m up at the head of the sand, falling 0.4 m a metre to the west.</summary>
        sealed class Slope : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x >= SandTopX ? 5f : 5f + (p.x - SandTopX) * 0.4f;
        }

        const float SandTopX = -42f;
        const float FreeFrom = 17f;
        const float FreeUntil = 21f;
        const float LingersMinutes = 30f;
        static readonly Vector2 Green = new Vector2(0f, 0f);
        static readonly Vector2 Head = new Vector2(-40f, 0f);
        static readonly Vector2 Work = new Vector2(0f, 12f);
        static readonly Vector2 Bed = new Vector2(0f, -12f);

        readonly List<Object> _spawned = new();
        readonly List<AmbientSpeechRequested> _said = new();
        DrivenClock _clock;
        FakeSea _sea;
        Slope _slope;
        VillagerRoutine _first;
        VillagerRoutine _second;
        BeachActivityDef _fire;
        BeachLifeLibrary _library;

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
                new RoutineStationEntry { Id = "s.green_a", Position = Green + new Vector2(-2f, 0f), LaneNodeName = "green", Why = "green" },
                new RoutineStationEntry { Id = "s.green_b", Position = Green + new Vector2(2f, 0f), LaneNodeName = "green", Why = "green" },
                new RoutineStationEntry { Id = "s.bed", Position = Bed, LaneNodeName = "green", Why = "bed" },
            });

            var beach = Make<BeachDef>();
            beach.Id = "beach.playtest_gathering";
            beach.RegionId = Region;
            beach.LaneNodeName = "head";
            beach.Gatherings = new[]
            {
                new BeachTransect { Top = new Vector2(SandTopX, 0f), Toe = new Vector2(-60f, 0f), Why = "open sand" },
            };

            NpcDef ada = Npc("npc.playtest_ada", "Ada");
            NpcDef ben = Npc("npc.playtest_ben", "Ben");

            _fire = Make<BeachActivityDef>();
            _fire.Id = "beachactivity.playtest_fire";
            _fire.Beach = beach;
            _fire.Kind = BeachActivityKind.Gather;
            _fire.ChancePerDay = 1f;
            _fire.EarliestHour = FreeFrom;
            _fire.LatestHour = FreeUntil + 0.5f;
            _fire.MaxWindStrength = 6f;
            _fire.MaxSeaState01 = 0.6f;
            _fire.ClearanceMetres = 0.5f;
            _fire.MinDepth01 = 0.5f;
            _fire.MaxDepth01 = 0.5f;
            _fire.GatherRadiusMetres = 1.5f;
            _fire.GatherPlaces = 4;
            _fire.CentreIsLit = true;
            _fire.CentreLingersMinutes = LingersMinutes;
            _fire.Talk = new[]
            {
                new BeachTalk
                {
                    Lines = new[]
                    {
                        new BeachTalkLine { Speaker = ada, Text = "It caught first time." },
                        new BeachTalkLine { Speaker = ben, Text = "It never does that for me." },
                    },
                },
            };

            _library = Make<BeachLifeLibrary>();
            _library.Activities = new[] { _fire };
            Assert.That(BeachLifeDirector.Instance, Is.Not.Null, "the director installs itself before the first scene");
            BeachLifeDirector.Instance.LoadLibrary(_library);

            _first = Villager("routine.playtest_a", ada, "s.green_a", stations, lanes);
            _second = Villager("routine.playtest_b", ben, "s.green_b", stations, lanes);

            _said.Clear();
            EventBus.Subscribe<AmbientSpeechRequested>(OnSaid);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AmbientSpeechRequested>(OnSaid);
            if (BeachLifeDirector.Instance != null)
                BeachLifeDirector.Instance.LoadLibrary(
                    Resources.Load<BeachLifeLibrary>(BeachLifeLibrary.ResourcesPath));
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        void OnSaid(AmbientSpeechRequested said) => _said.Add(said);

        NpcDef Npc(string id, string displayName)
        {
            var npc = Make<NpcDef>();
            npc.Id = id;
            npc.DisplayName = displayName;
            return npc;
        }

        VillagerRoutine Villager(string routineId, NpcDef npc, string greenStation, RoutineStations stations,
                                 RoutineLanes lanes)
        {
            var routine = Make<RoutineDef>();
            routine.Id = routineId;
            routine.RegionId = Region;
            routine.Npc = npc;
            routine.WalkSpeedMetresPerSecond = 1.2f;
            routine.ScheduleJitterMinutes = 0f;
            routine.Entries = new[]
            {
                new RoutineEntry { StartHour = 7f, StationId = "s.work", Activity = RoutineActivity.Work },
                new RoutineEntry { StartHour = FreeFrom, StationId = greenStation, Activity = RoutineActivity.Recreation },
                new RoutineEntry { StartHour = FreeUntil, StationId = "s.bed", Activity = RoutineActivity.Home },
            };

            var go = Spawn(routineId);
            go.transform.position = new Vector3(Green.x, Green.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            var villager = go.AddComponent<VillagerRoutine>();
            villager.Configure(routine, stations, lanes, renderer);
            return villager;
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
            BeachLifeDirector.Instance.RefreshGatheringsNow();
            yield return null;
        }

        static Vector2 Of(VillagerRoutine v) => v.transform.position;

        [UnityTest]
        public IEnumerator OnAGatheringEvening_TheyStandRoundOneFire_AndItBurnsDownAfterThem()
        {
            BeachLifeDirector director = BeachLifeDirector.Instance;

            yield return At(12f);
            Assert.That(director.MiddlesShown, Is.EqualTo(0), "no fire at noon: nobody has come down yet");

            yield return At(19.5f);
            Assert.That(_first.IsVariedToday && _second.IsVariedToday, Is.True, "both are at the gathering");
            float perHour = GameConfig.DefaultSecondsPerDay / 24f;
            Assert.That(_first.Today.SampleAt(19.5f, perHour).Walking, Is.False);
            Assert.That(_second.Today.SampleAt(19.5f, perHour).Walking, Is.False);

            Assert.That(director.MiddlesShown, Is.EqualTo(1), "one fire for the two of them");
            Vector2 fire = director.FirstMiddle.transform.position;
            Assert.That(fire.x, Is.LessThan(SandTopX), $"the fire is on the sand: {fire}");
            Assert.That(_slope.ElevationAt(fire), Is.GreaterThanOrEqualTo(_sea.Water + _fire.ClearanceMetres),
                        "and above the water");

            Assert.That(Vector2.Distance(Of(_first), fire), Is.EqualTo(_fire.GatherRadiusMetres).Within(0.05f),
                        $"the first is in the ring: {Of(_first)}");
            Assert.That(Vector2.Distance(Of(_second), fire), Is.EqualTo(_fire.GatherRadiusMetres).Within(0.05f),
                        $"the second is in the ring: {Of(_second)}");
            Assert.That(Vector2.Distance(Of(_first), Of(_second)), Is.GreaterThan(1f), "each in a place of their own");

            // After the last of them has gone home the fire burns on for a while, and then it is out.
            yield return At(FreeUntil + LingersMinutes / 60f * 0.5f);
            Assert.That(director.MiddlesShown, Is.EqualTo(1), "the fire is not carried home");
            yield return At(FreeUntil + LingersMinutes / 60f + 0.25f);
            Assert.That(director.MiddlesShown, Is.EqualTo(0), "and it is out by the end of the evening");
        }

        [UnityTest]
        public IEnumerator RoundTheFire_TheySayAFewLinesToEachOther_OnceAnEvening()
        {
            BeachLifeDirector director = BeachLifeDirector.Instance;

            yield return At(19.5f);
            Assert.That(_said.Count, Is.EqualTo(1), "the first line is asked for once both are standing there");
            Assert.That(_said[0].SpeakerId, Is.EqualTo("npc.playtest_ada"));
            Assert.That(_said[0].Anchor, Is.SameAs(_first.transform), "the bubble rides the one who speaks");
            Assert.That(director.IsTalkRunning, Is.True);

            // The line was read: the answer follows.
            EventBus.Publish(new AmbientSpeechEnded(_said[0].Id, AmbientSpeechEndReason.Dwelled));
            Assert.That(_said.Count, Is.EqualTo(2));
            Assert.That(_said[1].SpeakerId, Is.EqualTo("npc.playtest_ben"));
            Assert.That(_said[1].Anchor, Is.SameAs(_second.transform));

            EventBus.Publish(new AmbientSpeechEnded(_said[1].Id, AmbientSpeechEndReason.Dwelled));
            Assert.That(director.IsTalkRunning, Is.False, "two lines, and it is over");
            Assert.That(_first.IsTalking || _second.IsTalking, Is.False, "nobody is left held");

            yield return At(20f);
            Assert.That(_said.Count, Is.EqualTo(2), "one exchange an evening: it is not said again");
        }

        [UnityTest]
        public IEnumerator InAGale_ThereIsNoGathering_AndNoFire()
        {
            // A new sea, not a changed one: the weather of a day is a function of the clock, and the
            // director only plans again when the world it reads is replaced.
            _sea = new FakeSea { WindStrength = 12f };
            GameServices.Environment = _sea;
            yield return At(19.5f);
            Assert.That(_first.IsVariedToday || _second.IsVariedToday, Is.False, "nobody goes down in a gale");
            Assert.That(BeachLifeDirector.Instance.MiddlesShown, Is.EqualTo(0));
            Assert.That(_said, Is.Empty);
        }
    }
}
