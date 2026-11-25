using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Nine Mile Creek lobster fleet as a whole, on the committed Defs: the boats keep clear of each
    /// other all day, each fishes her own ground under her own colours, each lands at her own ladder and
    /// her own patch of the buyer's counter, and a frame's worth of lookups for the whole fleet is cheap
    /// and allocates nothing.
    /// </summary>
    public class NpcPotFleetTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string FishFolder = "Assets/_Project/Data/Fish";
        private const double Day = GameConfig.DefaultSecondsPerDay;
        private static readonly int[] Seeds = { 0, 1, 4242, 987654321 };
        private const int DaysProbed = 6;
        private const double TrackStepClockSeconds = 0.25;

        private const int FleetSize = 3;
        /// <summary>Two hulls' centres never come closer than this: a boat's length, so they pass abeam and never overlap.</summary>
        private const float HullGapMeters = 12f;
        /// <summary>Two owners' pots never lie closer than this, so no boat lies to her pot over another's rope.</summary>
        private const float PotGapMeters = 15f;
        private const float WallFaceY = 86f;
        private const float CounterReachMeters = 2.5f;
        private const float ColourGap = 0.25f;

        /// <summary>Frames timed for the proof; each is one lookup of everything the presenter asks for every boat.</summary>
        private const int PerfFrames = 20000;
        /// <summary>The fleet's share of a 16.6 ms frame for its clock lookups. Generous: a shared CI runner is slow.</summary>
        private const double PerfBudgetMsPerFrame = 0.25;

        private List<NpcPotDayDef> _defs;

        private static float Tide(double t) => 1.1f * Mathf.Sin((float)(t / Day * 4.0 * Mathf.PI));

        [SetUp]
        public void SetUp()
        {
            FishSpeciesRegistry.Reset();
            _defs = new List<NpcPotDayDef>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(NpcPotDayDef)}", new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<NpcPotDayDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) _defs.Add(def);
            }
            _defs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(FishSpeciesDef)}", new[] { FishFolder }))
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (fish != null) FishSpeciesRegistry.Register(fish);
            }
        }

        [TearDown]
        public void TearDown() => FishSpeciesRegistry.Reset();

        [Test]
        public void TheFleet_IsSeveralBoats_EachWithHerOwnBerth_HourAndColours()
        {
            Assert.That(_defs.Count, Is.GreaterThanOrEqualTo(FleetSize), "the creek works a fleet, not one boat");
            for (int a = 0; a < _defs.Count; a++)
            {
                for (int b = a + 1; b < _defs.Count; b++)
                {
                    string pair = $"{_defs[a].Id} / {_defs[b].Id}";
                    Assert.That((_defs[a].BerthPosition - _defs[b].BerthPosition).magnitude, Is.GreaterThan(HullGapMeters),
                                $"{pair}: two boats in one berth");
                    Assert.That(_defs[a].DepartHour, Is.Not.EqualTo(_defs[b].DepartHour), $"{pair}: they cast off together");
                    Color ca = _defs[a].BuoyColour, cb = _defs[b].BuoyColour;
                    float apart = Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g) + Mathf.Abs(ca.b - cb.b);
                    Assert.That(apart, Is.GreaterThan(ColourGap), $"{pair}: their buoys cannot be told apart");
                }
            }
        }

        [Test]
        public void NoTwoHulls_EverOverlap_AtTheBerthsOnPassageOrOnTheGrounds()
        {
            float closest = float.MaxValue;
            string where = "";
            var days = new NpcPotDay[_defs.Count];
            foreach (int seed in Seeds)
            {
                for (int day = 0; day < DaysProbed; day++)
                {
                    for (int k = 0; k < _defs.Count; k++) days[k] = NpcPotDay.Build(_defs[k], seed, day, Day);
                    double t0 = days[0].DayStartSeconds;
                    for (double t = t0; t < t0 + Day; t += TrackStepClockSeconds)
                    {
                        for (int a = 0; a < _defs.Count; a++)
                        {
                            NpcPotBoatPose p = days[a].PoseAt(t);
                            for (int b = a + 1; b < _defs.Count; b++)
                            {
                                NpcPotBoatPose q = days[b].PoseAt(t);
                                float gap = (p.Position - q.Position).magnitude;
                                if (gap >= closest) continue;
                                closest = gap;
                                where = $"{_defs[a].Id} ({p.Stage}) and {_defs[b].Id} ({q.Stage}), seed {seed} day {day}, " +
                                        $"{(t - t0) / Day * 24.0:0.00} h, at {p.Position} and {q.Position}";
                            }
                        }
                    }
                }
            }
            TestContext.WriteLine($"closest two hulls come: {closest:0.0} m ({where})");
            Assert.That(closest, Is.GreaterThanOrEqualTo(HullGapMeters), where);
        }

        [Test]
        public void EveryOwner_FishesHerOwnGround()
        {
            foreach (int seed in Seeds)
            {
                for (int day = -1; day < DaysProbed; day++)
                {
                    for (int a = 0; a < _defs.Count; a++)
                    {
                        for (int b = a + 1; b < _defs.Count; b++)
                        {
                            for (int i = 0; i < _defs[a].PotCount; i++)
                            {
                                // Today's and yesterday's sets are both on the bottom through the morning.
                                for (int j = 0; j < _defs[b].PotCount; j++)
                                {
                                    for (int back = 0; back <= 1; back++)
                                    {
                                        Vector2 pa = NpcPotDay.PotPosition(_defs[a], seed, day, i);
                                        Vector2 pb = NpcPotDay.PotPosition(_defs[b], seed, day - back, j);
                                        Assert.That((pa - pb).magnitude, Is.GreaterThanOrEqualTo(PotGapMeters),
                                                    $"{_defs[a].Id} pot {i} at {pa} and {_defs[b].Id} pot {j} at {pb}, seed {seed} day {day}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void EverySkipper_HasHerOwnLadder_AndHerOwnPatchOfTheCounter()
        {
            Vector2 buyer = NineMileCreekMainland.FishBuyerPos;
            var crates = new List<(string Id, Vector2 At)>();
            foreach (NpcPotDayDef def in _defs)
            {
                Assert.IsNull(NpcPotLanding.Problem(def), $"{def.Id}: {NpcPotLanding.Problem(def)}");
                Vector2 top = def.LandingRoute[0];
                Vector2 rail = NpcPotLanding.ToWorld(def, def.StepAshoreLocalMeters);
                Assert.That(top.y, Is.GreaterThan(WallFaceY), $"{def.Id}: the top of her ladder is on the wharf");
                Assert.That(rail.y, Is.LessThan(WallFaceY), $"{def.Id}: she steps off from the boat's side of the wall");
                Assert.That(Mathf.Abs(top.x - rail.x), Is.LessThan(0.5f), $"{def.Id}: the ladder is at her own rail");
                Assert.That((top - rail).magnitude, Is.LessThan(8f), $"{def.Id}: the ladder is a climb, not a jump");

                Vector2 counter = def.LandingRoute[def.LandingRoute.Length - 1];
                Assert.That((counter - buyer).magnitude, Is.LessThanOrEqualTo(CounterReachMeters),
                            $"{def.Id}: her route ends {(counter - buyer).magnitude:0.0} m from the buyer's counter");

                int grades = (def.GradeBreaksMm != null ? def.GradeBreaksMm.Length : 0) + 1;
                for (int g = 0; g < grades; g++)
                {
                    Vector2 at = counter + def.CounterCrateOffsetMeters
                                 + Vector2.right * ((g - (grades - 1) * 0.5f) * def.CounterCrateSpacingMeters);
                    foreach ((string id, Vector2 other) in crates)
                    {
                        Assert.That((at - other).magnitude, Is.GreaterThanOrEqualTo(def.CounterCrateSpacingMeters * 0.9f),
                                    $"{def.Id}'s crate {g} is put down on top of {id}'s at {other}");
                    }
                    crates.Add((def.Id + " crate " + g, at));
                }
            }
        }

        [Test]
        public void LoadedMidDay_EveryBoatIsWhereTheClockSays()
        {
            foreach (NpcPotDayDef def in _defs)
            {
                foreach (int seed in Seeds)
                {
                    Plan first = Plan.Build(def, seed, 2);
                    Plan again = Plan.Build(def, seed, 2);
                    for (double f = 0.05; f < 1.0; f += 0.05)
                    {
                        double t = first.Today.DayStartSeconds + f * Day;
                        NpcPotBoatPose p = first.Today.PoseAt(t), q = again.Today.PoseAt(t);
                        Assert.That(q.Position, Is.EqualTo(p.Position), $"{def.Id} seed {seed} at {f:0.00} of the day");
                        Assert.That(q.Stage, Is.EqualTo(p.Stage));
                        NpcLandingMoment m = first.Landing.MomentAt(t), n = again.Landing.MomentAt(t);
                        Assert.That(n.Position, Is.EqualTo(m.Position), $"{def.Id} seed {seed}: the landing at {f:0.00}");
                        Assert.That(n.Crate, Is.EqualTo(m.Crate));
                        Assert.That(again.Catch.StowedAt(t), Is.EqualTo(first.Catch.StowedAt(t)));
                        Assert.That(again.Landing.AtCounterAt(t), Is.EqualTo(first.Landing.AtCounterAt(t)));
                    }
                }
            }
        }

        [Test]
        public void AFrameOfTheWholeFleet_IsCheap_AndAllocatesNothing()
        {
            var plans = new Plan[_defs.Count];
            for (int k = 0; k < _defs.Count; k++) plans[k] = Plan.Build(_defs[k], 4242, 2);
            double t0 = plans[0].Today.DayStartSeconds;

            float sink = Frame(plans, t0 + 1.0);   // warm: JIT and statics
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int f = 0; f < PerfFrames; f++) sink += Frame(plans, t0 + Day * f / PerfFrames);
            watch.Stop();
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            double perFrame = watch.Elapsed.TotalMilliseconds / PerfFrames;
            TestContext.WriteLine($"{_defs.Count} boats: {perFrame * 1000.0:0.00} µs a frame of clock lookups over a whole day " +
                                  $"({PerfFrames} frames, {allocated} bytes allocated, checksum {sink:0})");
            Assert.That(allocated, Is.EqualTo(0L), "a frame's lookups allocate nothing");
            Assert.That(perFrame, Is.LessThan(PerfBudgetMsPerFrame));
        }

        /// <summary>Everything the presenter asks the plans for one frame: every hull, pot, deck moment and landing moment.</summary>
        private static float Frame(Plan[] plans, double now)
        {
            float sum = 0f;
            for (int k = 0; k < plans.Length; k++)
            {
                Plan p = plans[k];
                NpcPotBoatPose pose = p.Today.PoseAt(now);
                sum += pose.Position.x + pose.HeadingDegrees;
                for (int i = 0; i < p.Def.PotCount; i++)
                {
                    NpcPotState state = NpcPotDay.PotAt(p.Today, p.Yesterday, i, now);
                    sum += state.Seabed.y;
                    sum += (int)p.Catch.MomentAt(i, now).Step;
                }
                sum += p.Catch.StowedAt(now);
                NpcLandingMoment moment = p.Landing.MomentAt(now);
                sum += moment.Position.y + p.Landing.AtCounterAt(now);
            }
            return sum;
        }

        private sealed class Plan
        {
            public NpcPotDayDef Def;
            public NpcPotDay Today, Yesterday;
            public NpcPotDayCatch Catch;
            public NpcPotLanding Landing;

            public static Plan Build(NpcPotDayDef def, int seed, int day)
            {
                var p = new Plan { Def = def };
                p.Today = NpcPotDay.Build(def, seed, day, Day);
                p.Yesterday = NpcPotDay.Build(def, seed, day - 1, Day);
                p.Catch = NpcPotDayCatch.Build(def, seed, p.Today, p.Yesterday, Season.EarlySpring, Tide, Day);
                p.Landing = NpcPotLanding.Build(def, p.Today, p.Catch);
                return p;
            }
        }
    }
}
