using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The NPC trawler's day, passage only: one boat out of Rice Point, across to her ground in the South
    /// Water and home. The plan is a function of the clock; these hold it deterministic, in one region at
    /// every instant, afloat on every leg at the lowest tide, and joined to the committed berth, channel,
    /// arrival and ground.
    /// </summary>
    public sealed class NpcTrawlDayTests
    {
        const string FleetPath = "Assets/_Project/Resources/NpcTrawlFleet.asset";
        const string DaysFolder = "Assets/_Project/Data/Trawlers";
        const string BerthFolder = "Assets/_Project/Data/Berths";
        const string GroundFolder = "Assets/_Project/Data/FishingGrounds";
        const string RegionFolder = "Assets/_Project/Data/Regions";
        const double Day = GameConfig.DefaultSecondsPerDay;
        const int Seed = 4242;
        const float SampleStepMeters = 0.5f;

        static NpcTrawlDayDef[] Days() =>
            AssetDatabase.FindAssets("t:NpcTrawlDayDef", new[] { DaysFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<NpcTrawlDayDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();

        static string[] DayIds() => Days().Select(d => d.Id).ToArray();
        static NpcTrawlDayDef DayDef(string id) => Days().Single(d => d.Id == id);

        static T[] All<T>(string folder) where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).ToArray();

        static NpcTrawlFleet Fleet()
        {
            var fleet = AssetDatabase.LoadAssetAtPath<NpcTrawlFleet>(FleetPath);
            Assert.That(fleet, Is.Not.Null, FleetPath);
            return fleet;
        }

        // ---- the plan --------------------------------------------------------------------------------

        [Test]
        public void ThereIsADay()
        {
            Assert.That(Days(), Is.Not.Empty, "at least one trawler works out of Rice Point");
        }

        [Test]
        public void TheDayCanBeBuilt_AndEndsBeforeMidnight([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            Assert.That(NpcTrawlDay.Problem(def), Is.Null);
            for (int day = 0; day < 40; day++)
            {
                var plan = NpcTrawlDay.Build(def, Seed, day, Day);
                Assert.That(plan.FitsTheDay(Day), Is.True, $"day {day}: she is fast again before midnight");
                Assert.That(plan.DepartSeconds, Is.LessThan(plan.OutsideFromSeconds));
                Assert.That(plan.OutsideFromSeconds, Is.LessThan(plan.OnGroundFromSeconds));
                Assert.That(plan.OnGroundFromSeconds, Is.LessThan(plan.OnGroundUntilSeconds));
                Assert.That(plan.OnGroundUntilSeconds, Is.LessThan(plan.InsideFromSeconds));
                Assert.That(plan.InsideFromSeconds, Is.LessThan(plan.ReturnSeconds));
            }
        }

        [Test]
        public void TheSameSeedAndDay_GiveTheSameDay_SecondForSecond([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            var a = NpcTrawlDay.Build(def, Seed, 7, Day);
            var b = NpcTrawlDay.Build(def, Seed, 7, Day);
            Assert.That(a.DepartSeconds, Is.EqualTo(b.DepartSeconds));
            Assert.That(a.ReturnSeconds, Is.EqualTo(b.ReturnSeconds));
            for (double t = a.DayStartSeconds; t < a.DayStartSeconds + Day; t += 1.7)
            {
                var pa = a.PoseAt(t); var pb = b.PoseAt(t);
                Assert.That(pa.Position, Is.EqualTo(pb.Position));
                Assert.That(pa.HeadingDegrees, Is.EqualTo(pb.HeadingDegrees));
                Assert.That(pa.Stage, Is.EqualTo(pb.Stage));
                Assert.That(pa.OnTheOpenWater, Is.EqualTo(pb.OnTheOpenWater));
            }
        }

        [Test]
        public void DaysDifferByTheHourSheLeaves_AndByNothingElse([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            double gameMinute = Day / 1440.0;
            double set = def.DepartHour / 24.0 * Day;
            var departs = new HashSet<double>();
            double length = double.NaN;
            for (int day = 0; day < 30; day++)
            {
                var plan = NpcTrawlDay.Build(def, Seed, day, Day);
                double offset = plan.DepartSeconds - plan.DayStartSeconds - set;
                Assert.That(Math.Abs(offset), Is.LessThanOrEqualTo(def.DepartJitterMinutes * gameMinute + 1e-6));
                departs.Add(Math.Round(offset, 3));
                double thisLength = plan.ReturnSeconds - plan.DepartSeconds;
                if (double.IsNaN(length)) length = thisLength;
                Assert.That(thisLength, Is.EqualTo(length).Within(1e-3), "the day is the same length whenever she leaves");
            }
            Assert.That(departs.Count, Is.GreaterThan(10), "she does not leave on the same minute every day");

            var other = NpcTrawlDay.Build(def, Seed + 1, 7, Day);
            var same = NpcTrawlDay.Build(def, Seed, 7, Day);
            Assert.That(other.DepartSeconds, Is.Not.EqualTo(same.DepartSeconds), "another world leaves at another minute");
        }

        [Test]
        public void SheIsInExactlyOneRegionAtEveryInstant_AndNeverJumpsInsideOne([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            var plan = NpcTrawlDay.Build(def, Seed, 3, Day);
            const double dt = 0.25;
            float fastest = Mathf.Max(def.HarbourSpeed, def.CruiseSpeed, def.WorkSpeed);
            int crossings = 0;
            NpcTrawlBoatPose before = plan.PoseAt(plan.DayStartSeconds);
            Assert.That(before.Stage, Is.EqualTo(NpcTrawlBoatStage.AtBerth));
            Assert.That(before.OnTheOpenWater, Is.False);
            int furthest = 0;
            for (double t = plan.DayStartSeconds + dt; t < plan.DayStartSeconds + Day; t += dt)
            {
                NpcTrawlBoatPose now = plan.PoseAt(t);
                Assert.That(now.OnTheOpenWater, Is.EqualTo(plan.IsOutside(t)), $"t={t}: the pose and the plan name the same region");
                if (now.OnTheOpenWater != before.OnTheOpenWater) crossings++;
                else
                    Assert.That((now.Position - before.Position).magnitude, Is.LessThanOrEqualTo(fastest * (float)dt + 0.01f),
                                $"t={t}: she moved further than her fastest speed allows inside one region");
                if (now.Stage != NpcTrawlBoatStage.AtBerth)
                {
                    Assert.That((int)now.Stage, Is.GreaterThanOrEqualTo(furthest), $"t={t}: the day never runs backwards");
                    furthest = (int)now.Stage;
                }
                before = now;
            }
            Assert.That(crossings, Is.EqualTo(2), "out through the seam once, home through it once");
            Assert.That(furthest, Is.EqualTo((int)NpcTrawlBoatStage.MakingFast), "the day reaches making fast");
            Assert.That(before.Stage, Is.EqualTo(NpcTrawlBoatStage.AtBerth), "and she ends it at her berth");
        }

        [Test]
        public void TheCrossingIsTheSameSecondOnBothSides([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            var plan = NpcTrawlDay.Build(def, Seed, 3, Day);
            NpcTrawlBoatPose lastInside = plan.PoseAt(plan.OutsideFromSeconds - 1e-3);
            NpcTrawlBoatPose firstOutside = plan.PoseAt(plan.OutsideFromSeconds);
            Assert.That(lastInside.OnTheOpenWater, Is.False);
            Assert.That(firstOutside.OnTheOpenWater, Is.True);
            Assert.That((lastInside.Position - def.HarbourOut[def.HarbourOut.Length - 1]).magnitude, Is.LessThan(0.05f),
                        "she leaves the harbour at the channel mouth");
            Assert.That((firstOutside.Position - def.PassageOut[0]).magnitude, Is.LessThan(0.05f),
                        "and is at the seam of the open water that same second");

            NpcTrawlBoatPose lastOutside = plan.PoseAt(plan.InsideFromSeconds - 1e-3);
            NpcTrawlBoatPose firstInside = plan.PoseAt(plan.InsideFromSeconds);
            Assert.That(lastOutside.OnTheOpenWater, Is.True);
            Assert.That(firstInside.OnTheOpenWater, Is.False);
            Assert.That((lastOutside.Position - def.PassageHome[def.PassageHome.Length - 1]).magnitude, Is.LessThan(0.05f));
            Assert.That((firstInside.Position - def.HarbourHome[0]).magnitude, Is.LessThan(0.05f));
        }

        [Test]
        public void SheBacksOutHoldingHerBerthHeading_AndComesHomeBowFirst([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            if (def.SternFirstLegCount == 0) Assert.Ignore("this boat turns and goes out bow first");
            var plan = NpcTrawlDay.Build(def, Seed, 3, Day);
            double firstLegSeconds = (def.HarbourOut[0] - def.BerthPosition).magnitude / def.HarbourSpeed;
            double under = plan.DepartSeconds + def.BoardSeconds + def.LetGoSeconds;
            NpcTrawlBoatPose backing = plan.PoseAt(under + firstLegSeconds * 0.5);
            Assert.That(backing.Stage, Is.EqualTo(NpcTrawlBoatStage.HarbourOut));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(backing.HeadingDegrees, def.BerthHeadingDegrees)), Is.LessThan(0.5f),
                        "going astern she keeps the heading she lay at");

            NpcTrawlBoatPose home = plan.PoseAt(plan.InsideFromSeconds + 5.0);
            float course = NpcPotDay.Bearing(def.HarbourHome[0], def.HarbourHome[1], 0f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(home.HeadingDegrees, course)), Is.LessThan(1f),
                        "coming home her bow is on her course");
        }

        [Test]
        public void OnTheGroundSheWorksHerTrack_ForTheHoursTheDefGives([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            var plan = NpcTrawlDay.Build(def, Seed, 3, Day);
            double hours = (plan.OnGroundUntilSeconds - plan.OnGroundFromSeconds) / Day * 24.0;
            Assert.That(hours, Is.EqualTo(def.OnGroundHours).Within(0.01));
            for (double t = plan.OnGroundFromSeconds + 0.5; t < plan.OnGroundUntilSeconds; t += 2.0)
            {
                NpcTrawlBoatPose pose = plan.PoseAt(t);
                Assert.That(pose.Stage, Is.EqualTo(NpcTrawlBoatStage.OnTheGround));
                Assert.That(DistanceToTrack(def.WorkTrack, pose.Position), Is.LessThan(0.05f), $"t={t}: she is on her track");
            }
        }

        [Test]
        public void ADefThatCannotMakeADay_KeepsHerAtHerBerth()
        {
            var def = ScriptableObject.CreateInstance<NpcTrawlDayDef>();
            try
            {
                def.Id = "trawlday.test_empty";
                def.BerthPosition = new Vector2(3f, 4f);
                def.BerthHeadingDegrees = 90f;
                Assert.That(NpcTrawlDay.Problem(def), Is.Not.Null);
                var plan = NpcTrawlDay.Build(def, Seed, 2, Day);
                NpcTrawlBoatPose pose = plan.PoseAt(2 * Day + Day * 0.5);
                Assert.That(pose.Stage, Is.EqualTo(NpcTrawlBoatStage.AtBerth));
                Assert.That(pose.Position, Is.EqualTo(def.BerthPosition));
                Assert.That(pose.OnTheOpenWater, Is.False);
                Assert.That(NpcTrawlDay.Build(null, Seed, 2, Day).PoseAt(0).Stage, Is.EqualTo(NpcTrawlBoatStage.AtBerth));
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        // ---- the committed content ---------------------------------------------------------------------

        [Test]
        public void HerBerthIsTheCommittedBerth([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            BerthDef berth = All<BerthDef>(BerthFolder).SingleOrDefault(b => b.Id == def.BerthId);
            Assert.That(berth, Is.Not.Null, def.BerthId);
            Vector2 middle = (berth.Stern + berth.Bow) * 0.5f;
            Assert.That((def.BerthPosition - middle).magnitude, Is.LessThan(0.02f), "she lies in the middle of her berth");
            float lie = NpcPotDay.Bearing(berth.Stern, berth.Bow, 0f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(def.BerthHeadingDegrees, lie)), Is.LessThan(0.05f), "bow where the berth's bow is");
            Assert.That(berth.MaxDraughtMeters, Is.GreaterThanOrEqualTo(def.DraughtMeters));

            RegionDef region = All<RegionDef>(RegionFolder).Single(r => r.Id == berth.RegionId);
            Assert.That(def.HarbourSceneName, Is.EqualTo(region.SceneName));

            Assert.That(def.HarbourHome.Length, Is.EqualTo(berth.Approach.Length), "the way in is the berth's own way in");
            for (int i = 0; i < berth.Approach.Length; i++)
            {
                Assert.That((def.HarbourHome[i] - berth.Approach[i]).magnitude, Is.LessThan(0.02f), $"way in, point {i}");
                Assert.That((def.HarbourOut[def.HarbourOut.Length - 1 - i] - berth.Approach[i]).magnitude, Is.LessThan(0.02f),
                            $"way out, point {i} from the mouth");
            }
        }

        [Test]
        public void HerSeamIsTheSouthWatersNamedArrivalFromRicePoint([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            RegionDef water = All<RegionDef>(RegionFolder).Single(r => r.Id == SouthWaterPlan.RegionId);
            Assert.That(def.GroundSceneName, Is.EqualTo(water.SceneName));
            Vector2 arrival = SouthWaterPlan.NamedArrivals.Single(a => a.Key == SouthWaterPlan.FromRicePointArrivalKey).Position;
            Assert.That(def.PassageOut.Any(p => (p - arrival).magnitude < 0.02f), Is.True,
                        $"her way out passes the arrival at {arrival}");
            Assert.That(def.PassageHome.Any(p => (p - arrival).magnitude < 0.02f), Is.True,
                        $"her way home passes the arrival at {arrival}");
            Assert.That(def.PassageOut[0].x, Is.EqualTo(arrival.x).Within(0.02f), "she comes in on the door's own line");
            Assert.That(def.PassageOut[0].y, Is.GreaterThan(arrival.y), "from the edge side of the arrival");
            Assert.That(def.PassageOut[0].y, Is.LessThanOrEqualTo(SouthWaterPlan.NorthEdgeY));
            Assert.That(def.PassageHome[def.PassageHome.Length - 1], Is.EqualTo(def.PassageOut[0]), "and leaves where she came in");
        }

        [Test]
        public void HerTrackLiesOnHerGround([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            FishingGroundDef ground = All<FishingGroundDef>(GroundFolder).SingleOrDefault(g => g.Id == def.GroundId);
            Assert.That(ground, Is.Not.Null, def.GroundId);
            Assert.That(ground.RegionId, Is.EqualTo(SouthWaterPlan.RegionId));
            foreach (Vector2 p in def.WorkTrack)
                Assert.That(ground.Contains(p), Is.True, $"{p} is on {ground.Id}");
        }

        [Test]
        public void EveryLegHasWaterForHerAtTheLowestTideOfTheYear([ValueSource(nameof(DayIds))] string id)
        {
            var def = DayDef(id);
            var plan = NpcTrawlDay.Build(def, Seed, 3, Day);
            LandScenePlanDef harbour = RicePointBuilder.LoadPlan();
            Assert.That(harbour, Is.Not.Null);
            float harbourLow = harbour.Region.TideMeanLevel - harbour.Region.TideAmplitude;
            float needs = def.DraughtMeters + def.KeelClearanceMeters;
            Assert.That(plan.LegCount, Is.GreaterThan(6));
            for (int i = 0; i < plan.LegCount; i++)
            {
                plan.LegAt(i, out Vector2 a, out Vector2 b, out bool outside, out NpcTrawlBoatStage stage, out _, out _);
                float length = (b - a).magnitude;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / SampleStepMeters));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                    float water = outside
                        ? SouthWaterPlan.SpringLowDepthAt(p)
                        : harbourLow - harbour.Ground.Field.ElevationAt(p);
                    Assert.That(water, Is.GreaterThanOrEqualTo(needs),
                                $"leg {i} ({stage}) at {p}: {water:F2} m at the lowest tide, she needs {needs:F2} m");
                }
            }
        }

        // ---- the fleet ---------------------------------------------------------------------------------

        [Test]
        public void TheFleetListsEveryDayOnce_WithAHullThatDraws()
        {
            NpcTrawlFleet fleet = Fleet();
            Assert.That(Resources.Load<NpcTrawlFleet>(NpcTrawlFleet.ResourcesPath), Is.SameAs(fleet), "the presenter finds it");
            var listed = fleet.Trawlers.Where(t => t.Day != null).Select(t => t.Day.Id).ToArray();
            Assert.That(listed, Is.Unique);
            Assert.That(listed, Is.EquivalentTo(DayIds()), "every day Def is in the fleet and nothing else is");
            foreach (NpcTrawlFleet.Trawler t in fleet.Trawlers)
            {
                Assert.That(t.Hull, Is.Not.Null, t.Day.Id);
                Assert.That(t.Hull.Visual, Is.Not.Null, $"{t.Day.Id}: {t.Hull.Id} has a visual");
                Assert.That(t.Day.DraughtMeters, Is.EqualTo(t.Hull.DraughtMeters).Within(0.001f), $"{t.Day.Id} draws what her hull draws");
                Assert.That(t.Day.Id, Does.StartWith("trawlday."));
            }
        }

        static float DistanceToTrack(Vector2[] track, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 1; i < track.Length; i++)
            {
                Vector2 a = track[i - 1], b = track[i];
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
            return best;
        }
    }
}
