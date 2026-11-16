using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The cross-region resolver (<see cref="RoutineLegs"/>) — owner ask 2026-11-15, NPC routines that span
    /// scenes. Headless: the shipped routine and crossing assets, a clock that is just a number, and the
    /// always-open gate or <see cref="ScheduledCrossingGate"/>.
    ///
    /// <para>What is held: an ordinary day resolves to exactly the block the old engine picks; across a
    /// whole week sampled every game minute the errand-runner is in exactly one region; a shut gate holds
    /// him at the near end; and the answer is a pure function of (seed, time, gate).</para>
    /// </summary>
    public class RoutineLegsTests
    {
        const string JuniorPath = "Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset";
        const string BarPath = "Assets/_Project/Data/Regions/Crossings/StPetersNineMileCreekBar.asset";
        const string StPeters = "region.st_peters";
        const string Nmc = "region.nine_mile_creek";
        const int Seed = 20261115;
        const int DaysPerWeek = 7;
        const int Wednesday = (int)Weekday.Wednesday;

        static float SecondsPerGameHour => GameConfig.DefaultSecondsPerDay / RoutineSchedule.HoursPerDay;

        static RoutineLegContext Ctx(ICrossingGate gate = null, int seed = Seed) => new RoutineLegContext
        {
            WorldSeed = seed, SecondsPerGameHour = SecondsPerGameHour, DaysPerWeek = DaysPerWeek, Gate = gate,
        };

        static RoutineDef Junior()
        {
            var r = AssetDatabase.LoadAssetAtPath<RoutineDef>(JuniorPath);
            Assert.That(r, Is.Not.Null, JuniorPath);
            return r;
        }

        static RegionCrossingDef Bar()
        {
            var c = AssetDatabase.LoadAssetAtPath<RegionCrossingDef>(BarPath);
            Assert.That(c, Is.Not.Null, BarPath);
            return c;
        }

        static IEnumerable<RoutineDef> AllRoutines() =>
            AssetDatabase.FindAssets("t:RoutineDef", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<RoutineDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(r => r != null);

        // ---- 1. every existing day resolves exactly as before ------------------------------------

        [Test]
        public void OrdinaryDays_ResolveToTheSameBlockTheOldEngineChooses_AcrossAWeek()
        {
            int checkedRoutines = 0;
            foreach (RoutineDef r in AllRoutines())
            {
                if (!r.IsWalkable) continue;
                checkedRoutines++;
                float[] authored = r.Entries.Select(e => RoutineSchedule.Wrap24(e.StartHour)).ToArray();
                uint seed = RoutineSchedule.RoutineSeed(Seed, r.Id);
                float[] deps = Enumerable.Range(0, authored.Length)
                    .Select(i => RoutineSchedule.DepartureHour(authored, i, seed, r.ScheduleJitterMinutes)).ToArray();

                var it = new RoutineItinerary(r);
                for (int day = 0; day < DaysPerWeek; day++)
                {
                    RoutineLegs.EntriesFor(r, day, DaysPerWeek, out int weeklyToday);
                    RoutineLegs.EntriesFor(r, day - 1, DaysPerWeek, out int weeklyBefore);
                    if (weeklyToday >= 0 || weeklyBefore >= 0) continue;   // not an ordinary day

                    for (int minute = 0; minute < 24 * 60; minute += 7)
                    {
                        float hour = (minute + 0.5f) / 60f;
                        RoutineWhereabouts w = it.Resolve(day * 24d + hour, Ctx());
                        int expected = RoutineSchedule.BlockIndexAt(hour, deps);
                        string at = $"'{r.Id}' day {day} hour {hour:0.000}";
                        Assert.That(w.RegionId, Is.EqualTo(r.RegionId), at);
                        Assert.That(w.Kind, Is.EqualTo(RoutineLegKind.InRegion), at);
                        Assert.That(w.HasPosition, Is.False, at);
                        Assert.That(w.Block, Is.EqualTo(expected), at);
                        Assert.That(w.Activity, Is.EqualTo(r.Entries[expected].Activity), at);
                        Assert.That(w.StationId, Is.EqualTo(r.Entries[expected].StationId), at);
                    }
                }
            }
            Assert.That(checkedRoutines, Is.GreaterThan(1), "no walkable routine assets were found");
        }

        [Test]
        public void JuniorsOrdinaryMonday_IsPinned()
        {
            RoutineDef r = Junior();
            const int monday = 0;   // day 0 is a Monday (GameClock: TotalDays % DaysPerWeek)
            Assert.That(RoutineLegs.WeekdayIndex(monday, DaysPerWeek), Is.EqualTo((int)Weekday.Monday));

            void Pin(float hour, string station, RoutineActivity activity)
            {
                RoutineWhereabouts w = RoutineLegs.Resolve(r, monday * 24d + hour, Ctx());
                Assert.That(w.RegionId, Is.EqualTo(StPeters), $"{hour}");
                Assert.That(w.StationId, Is.EqualTo(station), $"{hour}");
                Assert.That(w.Activity, Is.EqualTo(activity), $"{hour}");
            }

            Pin(3f, "station.st_peters.home_sage_a", RoutineActivity.Home);
            Pin(10f, "station.st_peters.flats_head", RoutineActivity.Work);
            Pin(13f, "station.st_peters.store_customer_b", RoutineActivity.Errand);
            Pin(20f, "station.st_peters.green_d", RoutineActivity.Recreation);
            Pin(23.5f, "station.st_peters.home_sage_a", RoutineActivity.Home);
        }

        // ---- 2. one region at every instant ------------------------------------------------------

        [Test]
        public void ErrandWeek_SampledEveryGameMinute_IsAlwaysInExactlyOneRegion()
        {
            RoutineDef r = Junior();
            var it = new RoutineItinerary(r);
            RoutineWhereabouts prev = default;
            bool havePrev = false;
            int minutesInNmc = 0;

            for (int day = 0; day < DaysPerWeek; day++)
            for (int minute = 0; minute < 24 * 60; minute++)
            {
                double t = day * 24d + minute / 60d;
                RoutineWhereabouts w = it.Resolve(t, Ctx());
                string at = $"day {day} {minute / 60}:{minute % 60:00}";

                bool here = VillagerRoutine.IsPresentIn(w, StPeters);
                bool there = VillagerRoutine.IsPresentIn(w, Nmc);
                Assert.That(here ^ there, Is.True,
                            $"{at}: present in St Peters {here}, in Nine Mile Creek {there} (region '{w.RegionId}')");

                if (there)
                {
                    minutesInNmc++;
                    Assert.That(day, Is.EqualTo(Wednesday), $"{at}: in Nine Mile Creek on a day with no errand");
                }

                if (havePrev && prev.RegionId != w.RegionId)
                    Assert.That(prev.Kind == RoutineLegKind.Crossing && w.Kind == RoutineLegKind.Crossing, Is.True,
                                $"{at}: changed region from '{prev.RegionId}' to '{w.RegionId}' outside a crossing " +
                                $"({prev.Kind} then {w.Kind})");

                if (w.Kind == RoutineLegKind.Crossing && w.RegionId == StPeters)
                    Assert.That(w.Position.x, Is.InRange(-356.01f, -54.99f), $"{at}: off the bar at {w.Position}");

                prev = w;
                havePrev = true;
            }

            Assert.That(minutesInNmc, Is.GreaterThan(60), "the Wednesday errand never reached Nine Mile Creek");
        }

        [Test]
        public void Crossing_HandsOverAtTheSeam_AndBothDirectionsAgree()
        {
            RegionCrossingDef bar = Bar();
            float seam = bar.FromLengthMetres;
            Assert.That(bar.LengthMetres, Is.EqualTo(bar.FromLengthMetres + bar.ToLengthMetres).Within(1e-3f));

            bar.PointAt(seam - 0.01f, false, out string before, out _);
            bar.PointAt(seam, false, out string after, out _);
            Assert.That(before, Is.EqualTo(StPeters));
            Assert.That(after, Is.EqualTo(Nmc));

            Vector2 start = bar.PointAt(0f, false, out string r0, out _);
            Vector2 end = bar.PointAt(bar.LengthMetres, false, out string r1, out _);
            Assert.That(r0, Is.EqualTo(StPeters));
            Assert.That(r1, Is.EqualTo(Nmc));
            Assert.That(Vector2.Distance(start, bar.FromPath[0]), Is.LessThan(1e-3f));
            Assert.That(Vector2.Distance(end, bar.ToPath[bar.ToPath.Length - 1]), Is.LessThan(1e-3f));

            // Walking back, the same distance from the far end is the same point.
            for (float m = 0f; m <= bar.LengthMetres; m += 7.3f)
            {
                Vector2 fwd = bar.PointAt(m, false, out string rf, out _);
                Vector2 back = bar.PointAt(bar.LengthMetres - m, true, out string rb, out _);
                Assert.That(Vector2.Distance(fwd, back), Is.LessThan(1e-2f), $"{m} m");
                if (Mathf.Abs(m - seam) > 0.01f) Assert.That(rb, Is.EqualTo(rf), $"{m} m");
            }
        }

        // ---- 3. a shut gate holds ------------------------------------------------------------------

        [Test]
        public void ShutBar_HoldsHimAtTheBarHead_UntilItOpens()
        {
            RoutineDef r = Junior();
            RegionCrossingDef bar = Bar();
            double wed = Wednesday * 24d;
            var gate = new ScheduledCrossingGate(bar.Id).Closed(wed, wed + 12d);

            RoutineWhereabouts held = RoutineLegs.Resolve(r, wed + 9.5d, Ctx(gate));
            Assert.That(held.Kind, Is.EqualTo(RoutineLegKind.HeldAtCrossing));
            Assert.That(held.RegionId, Is.EqualTo(StPeters));
            Assert.That(held.HasPosition, Is.True);
            Assert.That(Vector2.Distance(held.Position, bar.FromPath[0]), Is.LessThan(1e-3f));

            for (double t = wed; t < wed + 12d; t += 1d / 60d)
                Assert.That(RoutineLegs.Resolve(r, t, Ctx(gate)).RegionId, Is.EqualTo(StPeters),
                            $"in Nine Mile Creek at {t - wed:0.00} h with the bar shut");

            RoutineWhereabouts going = RoutineLegs.Resolve(r, wed + 12d + 1d / 60d, Ctx(gate));
            Assert.That(going.Kind, Is.EqualTo(RoutineLegKind.Crossing), "sets out the moment the bar opens");

            // The later blocks waited for him: he still makes Nine Mile Creek and comes home the same day.
            bool reachedNmc = false;
            for (double t = wed + 12d; t < wed + 24d; t += 1d / 60d)
                reachedNmc |= RoutineLegs.Resolve(r, t, Ctx(gate)).RegionId == Nmc;
            Assert.That(reachedNmc, Is.True);
            Assert.That(RoutineLegs.Resolve(r, wed + 23.9d, Ctx(gate)).RegionId, Is.EqualTo(StPeters));
        }

        [Test]
        public void ShutBarOnTheWayBack_HoldsHimAtTheNineMileCreekLanding()
        {
            RoutineDef r = Junior();
            RegionCrossingDef bar = Bar();
            double wed = Wednesday * 24d;
            var gate = new ScheduledCrossingGate(bar.Id).Closed(wed + 11d, wed + 15d);

            RoutineWhereabouts held = RoutineLegs.Resolve(r, wed + 13d, Ctx(gate));
            Assert.That(held.Kind, Is.EqualTo(RoutineLegKind.HeldAtCrossing));
            Assert.That(held.RegionId, Is.EqualTo(Nmc));
            Assert.That(Vector2.Distance(held.Position, bar.ToPath[bar.ToPath.Length - 1]), Is.LessThan(1e-3f));
            Assert.That(RoutineLegs.Resolve(r, wed + 15d + 1d / 60d, Ctx(gate)).Kind,
                        Is.EqualTo(RoutineLegKind.Crossing));
            Assert.That(RoutineLegs.Resolve(r, wed + 20d, Ctx(gate)).RegionId, Is.EqualTo(StPeters));
        }

        [Test]
        public void BarThatNeverOpens_KeepsHimOnTheIsland_AllWeek()
        {
            RoutineDef r = Junior();
            var gate = new ScheduledCrossingGate(Bar().Id).Closed(0d, double.PositiveInfinity);
            var it = new RoutineItinerary(r);
            for (double t = 0d; t < DaysPerWeek * 24d; t += 5d / 60d)
                Assert.That(it.Resolve(t, Ctx(gate)).RegionId, Is.EqualTo(StPeters), $"{t:0.00} h");
        }

        // ---- 4. determinism --------------------------------------------------------------------------

        [Test]
        public void SameSeedAndTime_GiveTheSameAnswer()
        {
            RoutineDef r = Junior();
            var a = new RoutineItinerary(r);
            var b = new RoutineItinerary(r);
            for (double t = Wednesday * 24d; t < (Wednesday + 1) * 24d; t += 3d / 60d)
            {
                RoutineWhereabouts x = a.Resolve(t, Ctx());
                RoutineWhereabouts y = b.Resolve(t, Ctx());
                RoutineWhereabouts z = RoutineLegs.Resolve(r, t, Ctx());
                foreach (RoutineWhereabouts o in new[] { y, z })
                {
                    Assert.That(o.RegionId, Is.EqualTo(x.RegionId));
                    Assert.That(o.Kind, Is.EqualTo(x.Kind));
                    Assert.That(o.Block, Is.EqualTo(x.Block));
                    Assert.That(o.Position, Is.EqualTo(x.Position));
                }
            }
        }

        static double OutboundStart(RoutineDef r, int day, int seed)
        {
            var window = new List<RoutineSegment>();
            RoutineLegs.BuildWindow(r, day, Ctx(seed: seed), window);
            return window.First(s => s.Day == day && s.Kind == RoutineLegKind.Crossing).Start - day * 24d;
        }

        [Test]
        public void ErrandDeparture_VariesFromWeekToWeek_OnlyWithinItsStatedBound()
        {
            RoutineDef r = Junior();
            RoutineWeeklyDay errand = r.WeeklyDays.Single(d => d.Weekday == Weekday.Wednesday);
            double bound = errand.DayJitterMinutes / 60d;

            var starts = Enumerable.Range(0, 30).Select(w => OutboundStart(r, Wednesday + 7 * w, Seed)).ToArray();
            double spread = starts.Max() - starts.Min();
            Assert.That(spread, Is.GreaterThan(0d), "every week left at the same instant — no per-day variation");
            Assert.That(spread, Is.LessThanOrEqualTo(2d * bound + 1e-4),
                        $"departures spread {spread * 60d:0.0} min across weeks; the stated bound is ±{errand.DayJitterMinutes} min");

            // A different world is a different set of minutes (the seed matters).
            Assert.That(OutboundStart(r, Wednesday, Seed + 1), Is.Not.EqualTo(starts[0]));
        }

        [Test]
        public void ErrandCrossing_TakesItsLengthAtTheQuadsSpeed()
        {
            RoutineDef r = Junior();
            RegionCrossingDef bar = Bar();
            var window = new List<RoutineSegment>();
            RoutineLegs.BuildWindow(r, Wednesday, Ctx(), window);
            RoutineSegment s = window.First(x => x.Day == Wednesday && x.Kind == RoutineLegKind.Crossing);
            RoutineEntry e = r.WeeklyDays.Single(d => d.Weekday == Weekday.Wednesday).Entries[s.Block];

            Assert.That(e.CrossingSpeedMetresPerSecond, Is.GreaterThan(r.WalkSpeedMetresPerSecond),
                        "the quad is meant to be faster than his walk");
            double expected = bar.LengthMetres / e.CrossingSpeedMetresPerSecond / SecondsPerGameHour;
            Assert.That(s.End - s.Start, Is.EqualTo(expected).Within(1e-6));
        }
    }
}
