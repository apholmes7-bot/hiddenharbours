using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Farm slice 1: the arithmetic (<see cref="FarmMath"/>) and the seam (<see cref="FarmService"/>). Every
    /// Def here is made by the test, so the maths is proven apart from the shipped numbers; the shipped
    /// assets have their own checks in <see cref="FarmContentValidationTests"/>.
    /// </summary>
    public class FarmMathTests
    {
        private const int SeasonDays = 28;
        private const int StartHighSummer = 28;   // the shipped start: High Summer day 1
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
            GameServices.Reset();
        }

        private CropDef Crop(string id = "crop.test")
        {
            var c = ScriptableObject.CreateInstance<CropDef>();
            c.Id = id;
            c.PlantFrom = new SeasonDay(Season.EarlySpring, 10);
            c.PlantTo = new SeasonDay(Season.EarlySpring, 24);
            c.HarvestFrom = new SeasonDay(Season.HighSummer, 20);
            c.HarvestTo = new SeasonDay(Season.TheTurn, 20);
            c.DaysToMature = 30;
            c.TillLeadDays = 3;
            c.TendEveryDays = 4;
            c.HarvestSpreadDays = 4;
            c.SproutAt = 0.15f;
            c.GrowingAt = 0.4f;
            c.YieldPerCell = 3;
            c.ProduceId = "produce.test";
            _made.Add(c);
            return c;
        }

        private FarmPlotDef Plot(string id, params string[] rotation)
        {
            var p = ScriptableObject.CreateInstance<FarmPlotDef>();
            p.Id = id;
            p.RegionId = "region.test";
            p.Cells = new Vector2Int(4, 3);
            p.CropRotation = rotation;
            p.TenderNpcIds = new[] { "npc.tender" };
            _made.Add(p);
            return p;
        }

        // ---- calendar -------------------------------------------------------------------------

        [Test]
        public void DayAt_MatchesTheClocksOneConversion()
        {
            for (long day = 0; day < 400; day += 7)
            {
                CalendarDate date = GameCalendar.AtDay(day, SeasonDays, StartHighSummer);
                FarmMath.CalendarDay farm = FarmMath.DayAt(day, SeasonDays, StartHighSummer);
                Assert.AreEqual(date.Year, farm.Year, "day " + day);
                Assert.AreEqual((int)date.Season * SeasonDays + date.DayOfSeason - 1, farm.DayOfYear, "day " + day);
            }
            Assert.AreEqual(28, FarmMath.DayAt(0, SeasonDays, StartHighSummer).DayOfYear, "a new game opens on High Summer 1");
        }

        // ---- determinism ----------------------------------------------------------------------

        [Test]
        public void SameInputs_SameState_AndQueryOrderIsIrrelevant()
        {
            CropDef crop = Crop();
            FarmPlotDef a = Plot("plot.a", "crop.test");
            FarmPlotDef b = Plot("plot.b", "crop.test");

            var forward = new List<FarmStage>();
            for (long d = 0; d < 300; d++)
            {
                forward.Add(FarmMath.StateAt(a, crop, 1234, d, SeasonDays, StartHighSummer).Stage);
                forward.Add(FarmMath.StateAt(b, crop, 1234, d, SeasonDays, StartHighSummer).Stage);
            }
            var backward = new List<FarmStage>();
            for (long d = 299; d >= 0; d--)
            {
                backward.Insert(0, FarmMath.StateAt(b, crop, 1234, d, SeasonDays, StartHighSummer).Stage);
                backward.Insert(0, FarmMath.StateAt(a, crop, 1234, d, SeasonDays, StartHighSummer).Stage);
            }
            CollectionAssert.AreEqual(forward, backward);

            FarmPlotState s1 = FarmMath.StateAt(a, crop, 99, 150, SeasonDays, StartHighSummer);
            FarmPlotState s2 = FarmMath.StateAt(a, crop, 99, 150, SeasonDays, StartHighSummer);
            Assert.AreEqual(s1.Stage, s2.Stage);
            Assert.AreEqual(s1.Growth01, s2.Growth01);
            Assert.AreEqual(s1.Schedule.PlantDay, s2.Schedule.PlantDay);
            Assert.AreEqual(s1.Schedule.HarvestDay, s2.Schedule.HarvestDay);
        }

        [Test]
        public void Service_AnswersTheSame_InAnyOrder_AsAFreshService()
        {
            CropDef crop = Crop();
            FarmPlotDef a = Plot("plot.a", "crop.test");
            FarmPlotDef b = Plot("plot.b", "crop.test", "");
            FarmService Make() => new FarmService(() => new[] { b, a }, () => new[] { crop },
                () => new ProduceDef[0], () => 42, () => SeasonDays, () => StartHighSummer);

            FarmService warm = Make();
            for (long d = 400; d >= 0; d -= 13) warm.TryGetState("plot.b", d, out _);
            FarmService fresh = Make();
            for (long d = 0; d <= 400; d += 5)
            {
                Assert.IsTrue(warm.TryGetState("plot.a", d, out FarmPlotState w));
                Assert.IsTrue(fresh.TryGetState("plot.a", d, out FarmPlotState f));
                Assert.AreEqual(f.Stage, w.Stage, "day " + d);
                Assert.AreEqual(f.Growth01, w.Growth01, "day " + d);
            }
            Assert.AreEqual("plot.a", warm.Plots[0].Id, "plots are ordinal by id");
            Assert.IsFalse(warm.TryGetState("plot.missing", 0, out _));
        }

        [Test]
        public void SowingDay_VariesBySeed_ButStaysInsideTheWindow()
        {
            CropDef crop = Crop();
            int from = FarmMath.DayOfYear(crop.PlantFrom, SeasonDays);
            int to = FarmMath.DayOfYear(crop.PlantTo, SeasonDays);
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                FarmPlotSchedule s = FarmMath.ScheduleFor("plot.a", crop, seed, 2, SeasonDays);
                Assert.That(s.PlantDay, Is.InRange(from, to), "seed " + seed);
                seen.Add(s.PlantDay);
            }
            Assert.Greater(seen.Count, 3, "the seed should spread sowing across the window");
        }

        // ---- stages ---------------------------------------------------------------------------

        [Test]
        public void ACrop_WalksItsStagesInOrder_AndIsNeverReadyOutsideItsWindow()
        {
            CropDef crop = Crop();
            int harvestFrom = FarmMath.DayOfYear(crop.HarvestFrom, SeasonDays);
            int harvestTo = FarmMath.DayOfYear(crop.HarvestTo, SeasonDays);
            for (int seed = 0; seed < 50; seed++)
            {
                FarmPlotSchedule s = FarmMath.ScheduleFor("plot.a", crop, seed, 3, SeasonDays);
                var stagesSeen = new HashSet<FarmStage>();
                FarmStage last = FarmStage.Fallow;
                for (int doy = 0; doy < FarmMath.DaysPerYear(SeasonDays); doy++)
                {
                    FarmPlotState st = FarmMath.StateOn(s, crop, 3, doy);
                    Assert.GreaterOrEqual((int)st.Stage, (int)last, "seed " + seed + " day " + doy + ": stages never go back");
                    if (st.IsReady)
                        Assert.That(doy, Is.InRange(harvestFrom, harvestTo), "seed " + seed + ": ready outside the window");
                    Assert.That(st.Growth01, Is.InRange(0f, 1f));
                    stagesSeen.Add(st.Stage);
                    last = st.Stage;
                }
                foreach (FarmStage stage in System.Enum.GetValues(typeof(FarmStage)))
                    Assert.IsTrue(stagesSeen.Contains(stage), "seed " + seed + " never showed " + stage);
            }
        }

        [Test]
        public void ARestYear_StaysFallow_WithNoChores()
        {
            CropDef crop = Crop();
            FarmPlotDef plot = Plot("plot.a", "crop.test", "");
            Assert.AreEqual("crop.test", FarmMath.CropIdForYear(plot, 1));
            Assert.IsNull(FarmMath.CropIdForYear(plot, 2));
            Assert.AreEqual("crop.test", FarmMath.CropIdForYear(plot, 3));

            FarmPlotSchedule rest = FarmMath.ScheduleFor("plot.a", null, 7, 2, SeasonDays);
            var chores = new List<FarmChore>();
            for (int doy = 0; doy < FarmMath.DaysPerYear(SeasonDays); doy++)
            {
                Assert.AreEqual(FarmStage.Fallow, FarmMath.StateOn(rest, null, 2, doy).Stage);
                Assert.AreEqual(0, FarmMath.ChoresDue(rest, crop, doy, chores));
            }
        }

        // ---- chores ---------------------------------------------------------------------------

        [Test]
        public void ChoresForADay_AreConsistentWithTheStage()
        {
            CropDef crop = Crop();
            var chores = new List<FarmChore>();
            for (int seed = 0; seed < 50; seed++)
            {
                FarmPlotSchedule s = FarmMath.ScheduleFor("plot.a", crop, seed, 1, SeasonDays);
                int tills = 0, plants = 0, harvests = 0, tends = 0;
                for (int doy = 0; doy < FarmMath.DaysPerYear(SeasonDays); doy++)
                {
                    chores.Clear();
                    FarmMath.ChoresDue(s, crop, doy, chores);
                    FarmStage stage = FarmMath.StateOn(s, crop, 1, doy).Stage;
                    foreach (FarmChore c in chores)
                    {
                        switch (c)
                        {
                            case FarmChore.Till:
                                tills++;
                                Assert.AreEqual(FarmStage.Tilled, stage, "till leaves the ground tilled");
                                break;
                            case FarmChore.Plant:
                                plants++;
                                Assert.AreEqual(FarmStage.Planted, stage, "planting day shows planted rows");
                                break;
                            case FarmChore.Tend:
                                tends++;
                                Assert.That(stage, Is.EqualTo(FarmStage.Planted).Or.EqualTo(FarmStage.Sprouting)
                                    .Or.EqualTo(FarmStage.Growing), "tend only a growing crop");
                                break;
                            case FarmChore.Harvest:
                                harvests++;
                                Assert.AreEqual(FarmStage.Stubble, stage, "harvest day leaves stubble");
                                Assert.AreEqual(FarmStage.Ready, FarmMath.StateOn(s, crop, 1, doy - 1).Stage,
                                    "the crop stood ready the day before it was lifted");
                                break;
                        }
                    }
                }
                Assert.AreEqual(1, tills, "seed " + seed);
                Assert.AreEqual(1, plants, "seed " + seed);
                Assert.AreEqual(1, harvests, "seed " + seed);
                Assert.Greater(tends, 0, "seed " + seed);
            }
        }

        [Test]
        public void TheService_ListsChores_AndTheTendersPlots()
        {
            CropDef crop = Crop();
            FarmPlotDef a = Plot("plot.a", "crop.test");
            var service = new FarmService(() => new[] { a }, () => new[] { crop }, () => new ProduceDef[0],
                () => 5, () => SeasonDays, () => StartHighSummer);
            // Year 2 starts on elapsed day 84 (a new game opens on High Summer 1 of year 1).
            FarmPlotSchedule s = FarmMath.ScheduleFor("plot.a", crop, 5, 2, SeasonDays);
            var chores = new List<FarmChore>();
            Assert.AreEqual(1, service.ChoresDue("plot.a", 84 + s.PlantDay, chores));
            Assert.AreEqual(FarmChore.Plant, chores[0]);

            var plots = new List<FarmPlotDef>();
            Assert.AreEqual(1, service.PlotsTendedBy("npc.tender", plots));
            Assert.AreEqual(0, service.PlotsTendedBy("npc.nobody", plots));
            Assert.AreEqual(36, FarmMath.YieldOf(a, crop), "12 cells x 3 per cell");
        }

        [Test]
        public void CellGrowth_IsSeeded_Bounded_AndNeverMovesTheStage()
        {
            CropDef crop = Crop();
            FarmPlotSchedule s = FarmMath.ScheduleFor("plot.a", crop, 3, 1, SeasonDays);
            FarmPlotState st = FarmMath.StateOn(s, crop, 1, s.PlantDay + 10);
            for (int cell = 0; cell < 12; cell++)
            {
                float g = FarmMath.CellGrowth01(st, crop, "plot.a", 3, cell);
                Assert.AreEqual(g, FarmMath.CellGrowth01(st, crop, "plot.a", 3, cell));
                Assert.That(g, Is.InRange(st.Growth01 - crop.CellVariation - 1e-5f, st.Growth01 + crop.CellVariation + 1e-5f));
            }
        }

        // ---- the day signal -------------------------------------------------------------------

        [Test]
        public void Observe_AnnouncesOnlyRealChanges_AndALoadIsSilent()
        {
            CropDef crop = Crop();
            FarmPlotDef a = Plot("plot.a", "crop.test");
            var service = new FarmService(() => new[] { a }, () => new[] { crop }, () => new ProduceDef[0],
                () => 5, () => SeasonDays, () => StartHighSummer);
            var heard = new List<FarmPlotStageChanged>();
            void Hear(FarmPlotStageChanged e) => heard.Add(e);
            EventBus.Subscribe<FarmPlotStageChanged>(Hear);
            try
            {
                service.Observe(84, announce: true);        // first sight only baselines
                Assert.IsEmpty(heard);
                int changes = 0;
                FarmStage last = FarmMath.StateAt(a, crop, 5, 84, SeasonDays, StartHighSummer).Stage;
                for (long d = 85; d < 84 + 112; d++)
                {
                    service.Observe(d, announce: true);
                    FarmStage now = FarmMath.StateAt(a, crop, 5, d, SeasonDays, StartHighSummer).Stage;
                    if (now != last) changes++;
                    last = now;
                }
                Assert.AreEqual(changes, heard.Count, "one signal per real stage change");
                Assert.Greater(heard.Count, 0);
                foreach (FarmPlotStageChanged e in heard) Assert.AreNotEqual(e.From, e.To);

                heard.Clear();
                service.Observe(84 + 50, announce: false);   // a load seeks: re-baseline, no signal
                Assert.IsEmpty(heard);
            }
            finally
            {
                EventBus.Unsubscribe<FarmPlotStageChanged>(Hear);
            }
        }
    }
}
