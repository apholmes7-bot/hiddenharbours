using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PEOPLE OF ST PETERS USE THEIR BEACH</b> — a whole year of it, planned against the real
    /// thing: the villagers, stations, lanes and ground the St Peters scene ships (opened read-only and
    /// closed without saving), the shipped beach and activity assets, and the tide and weather laws.
    ///
    /// <para>What is held: the same day gives the same beach (rule 5); a gale or thick fog empties it;
    /// nobody is ever placed below the water; no two people share a spot; work, meals and sleep are
    /// exactly where the authored day has them; winter is nearly empty; and every walk fits its block,
    /// so nobody is still on the road at midnight when the next day's plan takes over.</para>
    /// </summary>
    public class StPetersBeachLifeTests
    {
        const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";
        const string LibraryPath = "Assets/_Project/Resources/BeachLifeLibrary.asset";
        const string Region = "region.st_peters";
        const int WorldSeed = 4242;
        const int DaysInTheYear = 112;

        sealed class Sky : IBeachSky
        {
            public GameConfig Cfg;
            public int Seed;
            public TideProfile Tide;
            public WindProfile Wind = WindProfile.CoddleCove;
            public float ForceWind = -1f;
            public float ForceVisibility = -1f;

            public float WaterLevelAt(double t) => TideModel.Height(t, Tide, Cfg);
            public float WindStrengthAt(double t) => ForceWind >= 0f
                ? ForceWind
                : WeatherModel.SampleWind(t, Seed, Cfg.SecondsPerDay / 24.0, Wind).magnitude;
            public float SeaState01At(double t) => WeatherModel.SeaState01(WindStrengthAt(t));
            public float Visibility01At(double t)
            {
                if (ForceVisibility >= 0f) return ForceVisibility;
                WeatherModel.Sample(t, Seed, Cfg, Wind, out _, out _, out float visibility);
                return visibility;
            }
        }

        sealed class Evening
        {
            public int Day;
            public int Rank;
            public RoutineDef Routine;
            public RoutineStop[] Authored;
            public RoutinePlan AuthoredPlan;
            public BeachDay BeachDay;
            public bool Went;
            public RoutineStop[] Stops;
            public BeachOuting Outing;
            public RoutinePlan Plan;
        }

        byte[] _sceneBytes;
        Scene _scene;
        bool _openedHere;
        GameConfig _cfg;
        Sky _sky;
        float _perHour;
        TidalTerrain _terrain;
        RoutineLanes _lanes;
        RoutineStations _stations;
        BeachLifeLibrary _library;
        RoutineDef[] _routines;
        List<Evening> _year;

        [OneTimeSetUp]
        public void OpenTheVillage()
        {
            _sceneBytes = System.IO.File.ReadAllBytes(ScenePath);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _openedHere = !_scene.isLoaded;
            if (_openedHere) _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var villagers = new List<VillagerRoutine>();
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                villagers.AddRange(root.GetComponentsInChildren<VillagerRoutine>(true));
                if (_terrain == null) _terrain = root.GetComponentInChildren<TidalTerrain>(true);
                if (_lanes == null) _lanes = root.GetComponentInChildren<RoutineLanes>(true);
                if (_stations == null) _stations = root.GetComponentInChildren<RoutineStations>(true);
            }
            Assert.That(_terrain, Is.Not.Null, "the scene has no TidalTerrain");
            Assert.That(_lanes, Is.Not.Null, "the scene has no RoutineLanes");
            Assert.That(_stations, Is.Not.Null, "the scene has no RoutineStations");

            _routines = villagers.Select(v => v.Routine)
                                 .Where(r => r != null && r.RegionId == Region)
                                 .GroupBy(r => r.Id).Select(g => g.First())
                                 .OrderBy(r => r.Id, System.StringComparer.Ordinal).ToArray();
            Assert.That(_routines.Length, Is.GreaterThanOrEqualTo(4), "St Peters' villagers were not found");

            _library = AssetDatabase.LoadAssetAtPath<BeachLifeLibrary>(LibraryPath);
            Assert.That(_library, Is.Not.Null, LibraryPath);

            _cfg = ScriptableObject.CreateInstance<GameConfig>();
            _perHour = _cfg.SecondsPerDay / RoutineSchedule.HoursPerDay;
            _sky = NewSky();
            _year = PlanTheYear(_sky);
        }

        [OneTimeTearDown]
        public void CloseTheVillageUnsaved()
        {
            if (_openedHere && _scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
            if (_cfg != null) Object.DestroyImmediate(_cfg);
            CollectionAssert.AreEqual(_sceneBytes, System.IO.File.ReadAllBytes(ScenePath),
                                      "reading the village must never write the scene");
        }

        Sky NewSky() => new Sky
        {
            Cfg = _cfg,
            Seed = WorldSeed,
            Tide = new TideProfile
            {
                MeanLevel = StPetersBuilder.TideMean,
                Amplitude = StPetersBuilder.TideAmplitude,
                PhaseHours = StPetersBuilder.TidePhaseHours,
            },
        };

        List<Evening> PlanTheYear(IBeachSky sky)
        {
            var year = new List<Evening>();
            for (int day = 0; day < DaysInTheYear; day++)
            {
                Season season = GameCalendar.AtDay(day, _cfg.DaysPerSeason, _cfg.StartDayOfYear).Season;
                var beachDay = new BeachDay(WorldSeed, day, season, _perHour);
                for (int rank = 0; rank < _routines.Length; rank++)
                {
                    RoutineDef routine = _routines[rank];
                    RoutineStop[] authored = RoutinePlanner.ResolveStops(routine, _stations, _lanes, WorldSeed,
                                                                        out string problem);
                    Assert.That(authored, Is.Not.Null, problem);

                    var evening = new Evening
                    {
                        Day = day, Rank = rank, Routine = routine, Authored = authored, BeachDay = beachDay,
                    };
                    evening.Went = BeachPlanning.TryPlanDay(
                        routine.Id, authored, _lanes, routine.WalkSpeedMetresPerSecond, _library.Activities,
                        Region, rank, _terrain, sky, beachDay, out evening.Stops, out evening.Outing);
                    if (evening.Went)
                    {
                        evening.Plan = RoutinePlanner.BuildFromStops(
                            routine.Id, evening.Stops, _lanes, routine.WalkSpeedMetresPerSecond,
                            routine.CatchUpSpeedMetresPerSecond, out _, out problem);
                        Assert.That(evening.Plan, Is.Not.Null, problem);
                        evening.AuthoredPlan = RoutinePlanner.BuildFromStops(
                            routine.Id, authored, _lanes, routine.WalkSpeedMetresPerSecond,
                            routine.CatchUpSpeedMetresPerSecond, out _, out problem);
                        Assert.That(evening.AuthoredPlan, Is.Not.Null, problem);
                    }
                    year.Add(evening);
                }
            }
            return year;
        }

        IEnumerable<Evening> Outings => _year.Where(e => e.Went);

        static string Name(Evening e) =>
            $"day {e.Day} {e.Routine.Id} {(e.Went ? e.Outing.Activity.Id : "(stayed)")}";

        // ---- the content ------------------------------------------------------------------------

        [Test]
        public void TheLibrary_ListsEveryShippedActivityOnce_AndEachCanRun()
        {
            var shipped = AssetDatabase.FindAssets("t:BeachActivityDef", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<BeachActivityDef>(AssetDatabase.GUIDToAssetPath(g)))
                .ToArray();
            Assert.That(shipped, Is.Not.Empty);
            CollectionAssert.AreEquivalent(shipped, _library.Activities,
                "every activity in Data/Beaches is in the library exactly once, and nothing else is");

            foreach (BeachActivityDef a in _library.Activities)
            {
                Assert.That(a, Is.Not.Null);
                Assert.That(a.Id, Does.StartWith("beachactivity."), a.name);
                Assert.That(a.IsRunnable, Is.EqualTo(string.IsNullOrEmpty(a.WaitsOn)),
                    a.Id + ": an activity runs unless it says what it waits on");
                if (a.Kind == BeachActivityKind.Wade)
                {
                    Assert.That(a.MinWadeDepthMetres, Is.LessThanOrEqualTo(a.MaxWadeDepthMetres), a.Id);
                    Assert.That(a.MaxWadeDepthMetres, Is.LessThanOrEqualTo(a.DeepestWadeMetres), a.Id);
                    Assert.That(a.Beach.Shallows, Is.Not.Empty, a.Id + ": wading needs shallows lines");
                }
                Assert.That(a.Beach.Id, Does.StartWith("beach."), a.Id);
                Assert.That(a.Replaces, Is.EqualTo(RoutineActivity.Recreation),
                    a.Id + ": the beach fills free time; it never takes work, a meal or sleep");
                Assert.That(a.EarliestHour, Is.LessThan(a.LatestHour), a.Id);
                Assert.That(a.MinDepth01, Is.LessThanOrEqualTo(a.MaxDepth01), a.Id);
                Assert.That(a.ClearanceMetres, Is.GreaterThan(0f), a.Id + ": people keep above the water");
                Assert.That(a.InEarlySpring || a.InHighSummer || a.InTheTurn || a.InHardWinter, Is.True, a.Id);
            }
            Assert.That(_library.Activities.Select(a => a.Id).Distinct().Count(),
                        Is.EqualTo(_library.Activities.Length), "activity ids are unique");
        }

        [Test]
        public void TheBeach_HangsOffARealLane_AndItsDryLinesStartAboveTheHighestTide()
        {
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            foreach (BeachDef beach in _library.Activities.Select(a => a.Beach).Distinct())
            {
                Assert.That(beach.RegionId, Is.EqualTo(Region), beach.Id);
                Assert.That(_lanes.IndexOfNode(beach.LaneNodeName), Is.GreaterThanOrEqualTo(0),
                    $"{beach.Id}: lane node '{beach.LaneNodeName}' is not in St Peters' lane network");
                Assert.That(StPetersRoutines.AllLaneNodeNames, Does.Contain(beach.LaneNodeName), beach.Id);
                Assert.That(beach.Spots, Is.Not.Empty, beach.Id);
                Assert.That(beach.Promenade.Length, Is.GreaterThanOrEqualTo(2), beach.Id);
                Assert.That(beach.Flats, Is.Not.Empty, beach.Id);

                BeachTransect[] shallows = beach.Shallows;
                for (int i = 0; i < shallows.Length; i++)
                {
                    Assert.That(_terrain.ElevationAt(shallows[i].Toe),
                        Is.LessThan(StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude * 0.4f),
                        $"{beach.Id}: the shallows line at {shallows[i].Top} must reach well below mid tide");
                    for (int j = i + 1; j < shallows.Length; j++)
                        Assert.That(Vector2.Distance(shallows[i].Top, shallows[j].Top), Is.GreaterThanOrEqualTo(3f),
                            $"{beach.Id}: shallows lines {i} and {j} are too close for two people to wander");
                }

                foreach (BeachTransect line in beach.Spots.Concat(beach.Promenade).Concat(beach.Shallows))
                {
                    Assert.That(_terrain.ElevationAt(line.Top), Is.GreaterThan(springHigh + 1f),
                        $"{beach.Id}: the top of the line at {line.Top} must stay dry on a spring high");
                    Assert.That(_terrain.ElevationAt(line.Toe), Is.LessThan(_terrain.ElevationAt(line.Top)),
                        $"{beach.Id}: the line at {line.Top} runs downhill to the water");
                }
                foreach (BeachTransect line in beach.Flats)
                    Assert.That(_terrain.ElevationAt(line.Top), Is.LessThan(springHigh),
                        $"{beach.Id}: flats are ground the tide covers; {line.Top} never floods");
            }
        }

        // ---- the gates --------------------------------------------------------------------------

        [Test]
        public void TheSameClockTideAndWeather_GiveTheSameBeach()
        {
            List<Evening> again = PlanTheYear(NewSky());
            Assert.That(again.Count, Is.EqualTo(_year.Count));
            for (int i = 0; i < _year.Count; i++)
            {
                Evening a = _year[i], b = again[i];
                Assert.That(b.Went, Is.EqualTo(a.Went), Name(a));
                if (!a.Went) continue;
                Assert.That(b.Outing.Activity, Is.SameAs(a.Outing.Activity), Name(a));
                Assert.That(b.Stops.Length, Is.EqualTo(a.Stops.Length), Name(a));
                for (int k = 0; k < a.Stops.Length; k++)
                {
                    Assert.That(b.Stops[k].DepartureHour, Is.EqualTo(a.Stops[k].DepartureHour), Name(a));
                    Assert.That(b.Stops[k].Station.Position, Is.EqualTo(a.Stops[k].Station.Position), Name(a));
                }
            }
        }

        [Test]
        public void OverAYear_EveryFairWeatherActivityHappens()
        {
            foreach (BeachActivityDef activity in _library.Activities.Where(a => a.IsRunnable))
                Assert.That(Outings.Count(e => e.Outing.Activity == activity), Is.GreaterThan(0),
                    activity.Id + " never happened in a whole year: its gates cannot all pass");

            int evenings = Outings.Select(e => e.Day).Distinct().Count();
            Assert.That(evenings, Is.GreaterThan(DaysInTheYear / 4),
                "the beach is lived on: somebody is there on a good share of the year's evenings");
            Assert.That(Outings.Count(), Is.LessThan(_year.Count * 3 / 4),
                "and the green still has its evenings: the beach does not take everybody every day");
        }

        [Test]
        public void DeepWinter_IsNearlyEmpty()
        {
            Evening[] winter = _year.Where(e => e.BeachDay.Season == Season.HardWinter).ToArray();
            Assert.That(winter, Is.Not.Empty);
            int went = winter.Count(e => e.Went);
            Assert.That(went, Is.LessThanOrEqualTo(winter.Length / 6),
                $"{went} of {winter.Length} winter evenings were spent on the beach");
            foreach (Evening e in winter.Where(e => e.Went))
                Assert.That(e.Outing.Activity.InHardWinter, Is.True, Name(e));
        }

        [Test]
        public void AGale_EmptiesTheBeach()
        {
            Sky gale = NewSky();
            gale.ForceWind = 12f;   // WeatherModel's Gale band starts at 11
            Evening[] went = PlanTheYear(gale).Where(e => e.Went).ToArray();
            Assert.That(went, Is.Empty, went.Length > 0 ? Name(went[0]) : "");
        }

        [Test]
        public void ThickFog_EmptiesTheBeach()
        {
            Sky fog = NewSky();
            fog.ForceVisibility = 0.1f;
            Evening[] went = PlanTheYear(fog).Where(e => e.Went).ToArray();
            Assert.That(went, Is.Empty, went.Length > 0 ? Name(went[0]) : "");
        }

        [Test]
        public void SomebodyWithNoFreeTime_NeverGoes()
        {
            RoutineDef[] busy = _routines
                .Where(r => r.Entries.All(entry => entry.Activity != RoutineActivity.Recreation)).ToArray();
            Assert.That(busy, Is.Not.Empty, "the fixture expects at least one villager with no free block");
            foreach (RoutineDef routine in busy)
                Assert.That(_year.Where(e => e.Routine == routine).Any(e => e.Went), Is.False, routine.Id);
        }

        // ---- where people are -------------------------------------------------------------------

        [Test]
        public void NobodyIsEverBelowTheWater()
        {
            int samples = 0;
            float lowest = float.MaxValue;
            foreach (Evening e in Outings)
            {
                BeachOuting o = e.Outing;
                int node = _lanes.IndexOfNode(o.Activity.Beach.LaneNodeName);
                Vector2 head = _lanes.NodePositions[node];
                int firstBlock = o.FirstStop, afterBlock = o.FirstStop + o.StopCount;

                for (float h = o.DepartureHour; h < 23.9f; h += 1f / 60f)
                {
                    RoutinePose pose = e.Plan.SampleAt(h, _perHour);
                    if (pose.BlockIndex < firstBlock || pose.BlockIndex > afterBlock) continue;
                    if (pose.Shelter != RoutineShelter.Outdoors) continue;
                    // The block after the beach is the walk away from it: held to the water only while
                    // still on the sand (the lanes beyond are the authored village's own ground).
                    if (pose.BlockIndex == afterBlock && Vector2.Distance(pose.Position, head) > 60f) continue;

                    float ground = _terrain.ElevationAt(pose.Position);
                    float water = _sky.WaterLevelAt(e.BeachDay.TimeAt(h));
                    samples++;
                    if (o.Activity.Kind == BeachActivityKind.Wade)
                    {
                        // Waders are in the water on purpose, and never over the depth the activity allows.
                        Assert.That(water - ground, Is.LessThanOrEqualTo(o.Activity.DeepestWadeMetres + 0.02f),
                            $"{Name(e)} at {h:0.00} h is at {pose.Position}: {water - ground:0.00} m of water");
                        continue;
                    }
                    lowest = Mathf.Min(lowest, ground - water);
                    Assert.That(ground, Is.GreaterThan(water + 0.05f),
                        $"{Name(e)} at {h:0.00} h is at {pose.Position}: ground {ground:0.00} m, water {water:0.00} m");

                    if (!pose.Walking && pose.BlockIndex < afterBlock)
                        Assert.That(ground, Is.GreaterThanOrEqualTo(water + o.Activity.ClearanceMetres - 0.05f),
                            $"{Name(e)} at {h:0.00} h stands at {pose.Position} closer to the water than " +
                            "the activity's clearance");
                }
            }
            Assert.That(samples, Is.GreaterThan(1000), "the year put people on the sand to be checked");
            TestContext.WriteLine($"lowest ground above water over {samples} samples: {lowest:0.00} m");
        }

        [Test]
        public void Waders_GoIn_FollowTheEdge_AndComeBackOut()
        {
            Evening[] wades = Outings.Where(e => e.Outing.Activity.Kind == BeachActivityKind.Wade).ToArray();
            Assert.That(wades, Is.Not.Empty, "nobody waded in a whole year");

            float deepest = 0f;
            foreach (Evening e in wades)
            {
                BeachOuting o = e.Outing;
                Assert.That(e.BeachDay.Season, Is.EqualTo(Season.HighSummer), Name(e) + ": warm days only");
                Assert.That(o.StopCount, Is.GreaterThanOrEqualTo(3), Name(e) + ": dry sand, the water, dry sand");

                // First and last places are the same dry place, above the water for the whole stay.
                RoutineStop first = e.Stops[o.FirstStop], last = e.Stops[o.FirstStop + o.StopCount - 1];
                Assert.That(last.Station.Position, Is.EqualTo(first.Station.Position), Name(e));
                Assert.That(_terrain.ElevationAt(first.Station.Position),
                            Is.GreaterThanOrEqualTo(o.KeepAboveMetres - 0.01f), Name(e));

                // In between they stand in water: wet at the middle of a standing place.
                bool wet = false;
                for (int k = o.FirstStop + 1; k < o.FirstStop + o.StopCount - 1; k++)
                {
                    float reached = e.Plan.DepartureHours[k] + e.Plan.TravelHours(k, _perHour);
                    float leaves = e.Plan.DepartureHours[k + 1];
                    float mid = (reached + leaves) * 0.5f;
                    float depth = _sky.WaterLevelAt(e.BeachDay.TimeAt(mid))
                                  - _terrain.ElevationAt(e.Stops[k].Station.Position);
                    deepest = Mathf.Max(deepest, depth);
                    if (depth > 0f) wet = true;
                    Assert.That(BeachPlanning.GatesPass(o.Activity, _sky, e.BeachDay, mid), Is.True,
                                $"{Name(e)} place {k} at {mid:0.00} h: in the water after the weather turned");
                    Assert.That(depth, Is.GreaterThan(-0.15f), $"{Name(e)} place {k}: the edge has left them behind");
                }
                Assert.That(wet, Is.True, Name(e) + " never got their feet wet");

            }
            TestContext.WriteLine($"{wades.Length} wades; deepest standing water {deepest:0.00} m");
        }

        [Test]
        public void NoTwoPeopleStandOnOneSpot()
        {
            float closest = float.MaxValue;
            foreach (IGrouping<int, Evening> day in Outings.GroupBy(e => e.Day))
            {
                Evening[] there = day.ToArray();
                for (float h = 12f; h < 23f; h += 1f / 30f)
                    for (int i = 0; i < there.Length; i++)
                        for (int j = i + 1; j < there.Length; j++)
                        {
                            if (!StandingOnTheBeach(there[i], h, out Vector2 a)) continue;
                            if (!StandingOnTheBeach(there[j], h, out Vector2 b)) continue;
                            float apart = Vector2.Distance(a, b);
                            closest = Mathf.Min(closest, apart);
                            Assert.That(apart, Is.GreaterThan(0.8f),
                                $"{Name(there[i])} and {Name(there[j])} at {h:0.00} h");
                        }
            }
            TestContext.WriteLine($"closest two people standing on the beach: {closest:0.00} m");
        }

        bool StandingOnTheBeach(Evening e, float hour, out Vector2 at)
        {
            RoutinePose pose = e.Plan.SampleAt(hour, _perHour);
            at = pose.Position;
            return !pose.Walking && pose.BlockIndex >= e.Outing.FirstStop &&
                   pose.BlockIndex < e.Outing.FirstStop + e.Outing.StopCount;
        }

        // ---- the day around it ------------------------------------------------------------------

        [Test]
        public void WorkMealsAndSleep_AreExactlyWhereTheAuthoredDayHasThem()
        {
            foreach (Evening e in Outings)
            {
                BeachOuting o = e.Outing;
                int block = o.AuthoredBlock;
                Assert.That(block, Is.GreaterThan(0).And.LessThan(e.Authored.Length - 1), Name(e));
                Assert.That(e.Authored[block].Activity, Is.EqualTo(RoutineActivity.Recreation), Name(e));

                // Every block but the one given to the beach is still there, at its own hour and place.
                for (int i = 0; i < block; i++) AssertSameStop(e.Stops[i], e.Authored[i], e);
                int extra = e.Stops.Length - e.Authored.Length;
                for (int i = block + 1; i < e.Authored.Length; i++)
                    AssertSameStop(e.Stops[i + extra], e.Authored[i], e);

                // Until she sets off for the beach, she is exactly where she would have been.
                for (float h = 0f; h < o.DepartureHour - 0.01f; h += 0.25f)
                    Assert.That(Vector2.Distance(e.Plan.SampleAt(h, _perHour).Position,
                                                 e.AuthoredPlan.SampleAt(h, _perHour).Position),
                                Is.LessThan(0.01f), $"{Name(e)} at {h:0.00} h");

                // And at midnight she is home and still, so tomorrow's plan takes over without a step.
                RoutinePose late = e.Plan.SampleAt(23.98f, _perHour);
                Assert.That(late.Walking, Is.False, Name(e) + " is still on the road at midnight");
                Assert.That(Vector2.Distance(late.Position, e.AuthoredPlan.SampleAt(23.98f, _perHour).Position),
                            Is.LessThan(0.01f), Name(e));
            }
        }

        static void AssertSameStop(in RoutineStop got, in RoutineStop authored, Evening e)
        {
            Assert.That(got.Station.Id, Is.EqualTo(authored.Station.Id), Name(e));
            Assert.That(got.DepartureHour, Is.EqualTo(authored.DepartureHour), Name(e));
            Assert.That(got.Activity, Is.EqualTo(authored.Activity), Name(e));
        }

        [Test]
        public void EveryWalkToAlongAndFromTheBeach_FitsItsBlock()
        {
            foreach (Evening e in Outings)
            {
                RoutinePlan plan = e.Plan;
                int last = e.Outing.FirstStop + e.Outing.StopCount;   // the block that walks off the sand
                for (int k = e.Outing.FirstStop; k <= last && k < plan.BlockCount; k++)
                {
                    float starts = plan.DepartureHours[k];
                    float ends = k + 1 < plan.BlockCount ? plan.DepartureHours[k + 1]
                                                         : plan.DepartureHours[0] + RoutineSchedule.HoursPerDay;
                    Assert.That(ends, Is.GreaterThan(starts), $"{Name(e)} block {k}: hours out of order");
                    Assert.That(plan.TravelHours(k, _perHour), Is.LessThan(ends - starts),
                        $"{Name(e)} block {k}: the walk is longer than the block");
                }
                Assert.That(e.Outing.LeaveHour - e.Outing.ArrivalHour,
                            Is.GreaterThanOrEqualTo(e.Outing.Activity.MinStayMinutes / 60f - 1e-3f), Name(e));
            }
        }

        [Test]
        public void WhenTheStayIsCutShort_TheyGoBackToWhereTheirEveningWas()
        {
            Evening[] cut = Outings.Where(e => e.Outing.GoesBackFirst).ToArray();
            foreach (Evening e in cut)
            {
                int back = e.Outing.FirstStop + e.Outing.StopCount;
                Assert.That(e.Stops[back].Station.Id, Is.EqualTo(e.Authored[e.Outing.AuthoredBlock].Station.Id),
                            Name(e));
                Assert.That(e.Stops[back].DepartureHour, Is.EqualTo(e.Outing.LeaveHour).Within(1e-4f), Name(e));
            }
            TestContext.WriteLine($"{cut.Length} of {Outings.Count()} stays ended early");
        }
    }
}
