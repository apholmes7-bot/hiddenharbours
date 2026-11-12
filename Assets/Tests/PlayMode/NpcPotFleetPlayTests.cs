using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The whole Nine Mile Creek lobster fleet on its real Defs and owners' hulls over a flat deep bed: every
    /// boat binds, works her own day and comes home to her own berth; and a frame of the whole fleet (hulls,
    /// decks, landings, ropes and buoys) costs a sliver of the frame and allocates nothing.
    /// </summary>
    public class NpcPotFleetPlayTests
    {
        const string DataRoot = "Assets/_Project/Data";
        const float BedElevation = -6f;
        const double Day = GameConfig.DefaultSecondsPerDay;
        const int WorkedDay = 2;
        const int Seed = 4242;

        /// <summary>Frames timed, spread over one whole day of the clock.</summary>
        const int PerfFrames = 3000;
        /// <summary>The fleet's share of a 16.6 ms frame (60 fps). Generous: a shared CI runner is slow.</summary>
        const double PerfBudgetMsPerFrame = 1.0;

        sealed class Sea : IEnvironmentService
        {
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => WaterLevelAt(totalSeconds);
            public float WaterLevelAt(double totalSeconds) => 1.1f * Mathf.Sin((float)(totalSeconds / Day * 4.0 * Mathf.PI));
        }

        sealed class SetClock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public bool IsPaused { get; set; } = true;
            public float TimeScale { get; set; }
            public int DayIndex => (int)System.Math.Floor(T / Day);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => (float)((T / Day) % 1.0);
            public float HourOfDay => DayFraction * 24f;
            public void SeekTo(double totalSeconds) => T = totalSeconds;
        }

        sealed class FlatBed : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => BedElevation;
        }

        readonly List<Object> _spawned = new();
        readonly List<NpcPotDayDef> _defs = new();
        readonly List<MooredBoat> _hulls = new();
        SetClock _clock;
        NpcPotBoats _boats;
        bool _autoBindWas;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _clock = new SetClock { T = WorkedDay * Day + 5.0 };
            GameServices.Environment = new Sea();
            GameServices.Clock = _clock;
            GameServices.TidalTerrain = new FlatBed();
            _autoBindWas = NpcPotBoats.AutoBind;
            NpcPotBoats.AutoBind = false;

#if UNITY_EDITOR
            var owners = new Dictionary<string, BoatOwnerDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:BoatOwnerDef", new[] { DataRoot }))
            {
                var o = AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (o != null) owners[o.Id] = o;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:NpcPotDayDef", new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<NpcPotDayDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                Assert.IsTrue(owners.TryGetValue(def.OwnerId, out BoatOwnerDef owner), def.OwnerId);
                var go = new GameObject($"Moored_{owner.Id}");
                go.SetActive(false);
                _spawned.Add(go);
                go.transform.position = new Vector3(def.BerthPosition.x, def.BerthPosition.y, 0f);
                var hull = go.AddComponent<MooredBoat>();
                hull.Configure(owner, def.BerthHeadingDegrees);
                go.SetActive(true);
                _defs.Add(def);
                _hulls.Add(hull);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data/Fish" }))
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (fish != null) FishSpeciesRegistry.Register(fish);
            }
#else
            Assert.Ignore("Needs the AssetDatabase: this drives the real committed Defs and owners.");
#endif

            _boats = NpcPotBoats.Instance;
            if (_boats == null)
            {
                var host = new GameObject("[NpcPotBoats] (test)");
                _spawned.Add(host);
                _boats = host.AddComponent<NpcPotBoats>();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_boats != null)
            {
                _boats.enabled = true;
                foreach (NpcPotDayDef def in _defs) _boats.Unbind(def.Id);
            }
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            _defs.Clear();
            _hulls.Clear();
            NpcPotBoats.AutoBind = _autoBindWas;
            FishSpeciesRegistry.Reset();
            GameServices.Reset();
        }

        IEnumerator At(double clockSeconds)
        {
            _clock.T = clockSeconds;
            float until = Time.unscaledTime + 0.3f;
            while (Time.unscaledTime < until) yield return null;
            yield return null;
            yield return null;
        }

        void BindAll()
        {
            Assert.That(_defs.Count, Is.GreaterThanOrEqualTo(3), "the creek works a fleet");
            for (int k = 0; k < _defs.Count; k++)
                Assert.IsTrue(_boats.TryBind(_defs[k], _hulls[k]), _defs[k].Id);
            Assert.That(_boats.BoundCount, Is.EqualTo(_defs.Count));
        }

        [UnityTest]
        public IEnumerator EveryBoat_GoesToHerOwnPots_AndComesHomeToHerOwnBerth()
        {
            BindAll();
            double dayStart = WorkedDay * Day;

            yield return At(dayStart + 5.0);
            for (int k = 0; k < _defs.Count; k++)
            {
                Assert.That(((Vector2)_hulls[k].transform.position - _defs[k].BerthPosition).magnitude, Is.LessThan(0.01f),
                            $"{_defs[k].Id} lies at her berth before she casts off");
            }

            // The middle of each boat's own haul: she lies to her own string, and the others are elsewhere.
            for (int k = 0; k < _defs.Count; k++)
            {
                NpcPotDay today = NpcPotDay.Build(_defs[k], Seed, WorkedDay, Day);
                NpcPotDay yesterday = NpcPotDay.Build(_defs[k], Seed, WorkedDay - 1, Day);
                int pot = today.PotCount / 2;
                yield return At((today.HaulStart[pot] + today.HaulEnd[pot]) * 0.5);
                Vector2 hull = _hulls[k].transform.position;
                Assert.That((hull - yesterday.SetPositions[pot]).magnitude, Is.LessThan(_defs[k].LayAlongsideMeters + 1f),
                            $"{_defs[k].Id} lies to her own pot {pot}");
                for (int other = 0; other < _defs.Count; other++)
                {
                    if (other == k) continue;
                    Assert.That((hull - (Vector2)_hulls[other].transform.position).magnitude, Is.GreaterThan(10f),
                                $"{_defs[k].Id} and {_defs[other].Id} are on top of each other");
                }
            }

            yield return At(dayStart + Day - 2.0);
            for (int k = 0; k < _defs.Count; k++)
            {
                Assert.That(((Vector2)_hulls[k].transform.position - _defs[k].BerthPosition).magnitude, Is.LessThan(0.01f),
                            $"{_defs[k].Id} is home at her own berth by the end of the day");
                Assert.IsTrue(_boats.LandingOf(_defs[k].Id).IsSoldAt(_clock.T) || !_boats.LandingOf(_defs[k].Id).HasLanding,
                              $"{_defs[k].Id}'s catch is landed by the end of the day");
            }
        }

        [UnityTest]
        public IEnumerator AFrameOfTheWholeFleet_IsASliverOfTheBudget_AndAllocatesNothing()
        {
            BindAll();
            double dayStart = WorkedDay * Day;
            yield return At(dayStart + 5.0);

            // Time the presenter's own frame, and only that: it is switched off so Unity does not also run it.
            _boats.enabled = false;
            _clock.T = dayStart + 1.0;
            _boats.Tick();   // warm

            double worstMs = 0.0;
            long allocated = 0;
            var watch = new Stopwatch();
            var one = new Stopwatch();
            for (int f = 0; f < PerfFrames; f++)
            {
                _clock.T = dayStart + 1.0 + (Day - 2.0) * f / PerfFrames;
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                one.Restart();
                watch.Start();
                _boats.Tick();
                watch.Stop();
                one.Stop();
                allocated += System.GC.GetAllocatedBytesForCurrentThread() - before;
                if (one.Elapsed.TotalMilliseconds > worstMs) worstMs = one.Elapsed.TotalMilliseconds;
                if (f % 500 == 499) yield return null;   // let the renderers take what was written
            }

            double meanMs = watch.Elapsed.TotalMilliseconds / PerfFrames;
            UnityEngine.Debug.Log($"[npc-lobster] fleet of {_defs.Count}: mean {meanMs * 1000.0:0.0} µs a frame, worst {worstMs * 1000.0:0.0} µs, " +
                                  $"{allocated} bytes allocated over {PerfFrames} frames of one whole day " +
                                  $"({meanMs / (1000.0 / 60.0) * 100.0:0.00}% of a 60 fps frame)");
            Assert.That(allocated, Is.EqualTo(0L), "a frame of the fleet allocates nothing");
            Assert.That(meanMs, Is.LessThan(PerfBudgetMsPerFrame), "the fleet's mean frame");
        }
    }
}
