using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.Tests.EditMode.IslandSupplyTestData;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The island supply headline numbers, pinned from the shipped data</b> (slice S1). Every input is read
    /// from an asset: the goods, carriers and roles, the route config, the shipped GameConfig, and the St Peters
    /// RegionDef's tide profile run through the real <c>TideModel.Height</c>
    /// (<c>Assets/_Project/Code/Environment/TideModel.cs:21-37</c>). If someone retunes a number, the test that
    /// moves names the consequence. The expected figures are in <c>docs/design/island-supply.md</c>.
    /// </summary>
    public class IslandSupplyPlanTests
    {
        private GameConfig _cfg;
        private IslandSupplyConfig _route;
        private Func<double, float> _water;
        private List<TideWindow> _windows;
        private double _month;

        private SupplyRoleDef Role(string id) => ById(LoadAll<SupplyRoleDef>(), r => r.Id, id);
        private CarrierDef Carrier(string id) => CarriersById()[id];
        private double Hours(double seconds) => CrossingMath.ToGameHours(seconds, _cfg);

        [OneTimeSetUp]
        public void Load()
        {
            _cfg = Config();
            _route = Route();
            _water = Water(_route, _cfg);
            _month = _cfg.LunarMonthDays * (double)_cfg.SecondsPerDay;
            _windows = CrossingMath.BarWindows(_water, _route, _cfg, 0.0, _month);
        }

        // ---- the windows ----------------------------------------------------------------------------

        [Test]
        public void ThereAre54BarWindowsIn28Days()
        {
            Assert.That(_cfg.LunarMonthDays, Is.EqualTo(28f), "the pins below are for a 28-day lunar month");
            Assert.That(_windows.Count, Is.EqualTo(54), "1.93 low waters a day: 28 x 24 / 12.4206 = 54.1");
        }

        [Test]
        public void TheBarIsDry784HoursAtSpringAnd105AtNeap()
        {
            double min = double.MaxValue, max = 0.0;
            foreach (TideWindow w in _windows)
            {
                min = Math.Min(min, Hours(w.DurationSeconds));
                max = Math.Max(max, Hours(w.DurationSeconds));
            }
            Assert.That(min, Is.EqualTo(7.84).Within(0.02), "spring window, game hours");
            Assert.That(max, Is.EqualTo(10.52).Within(0.02), "neap window, game hours (the closed form at exact neap is 10.54)");
        }

        [Test]
        public void TheWindowsAreDeterministic()
        {
            var again = CrossingMath.BarWindows(_water, _route, _cfg, 0.0, _month);
            Assert.That(again.Count, Is.EqualTo(_windows.Count));
            for (int i = 0; i < again.Count; i++)
            {
                Assert.That(again[i].StartSeconds, Is.EqualTo(_windows[i].StartSeconds));
                Assert.That(again[i].EndSeconds, Is.EqualTo(_windows[i].EndSeconds));
            }
        }

        [Test]
        public void TheGutWindowsForCartAndQuadSitInsideEveryBarWindow()
        {
            var cart = CrossingMath.GutWindows(_water, _route, Role("supplyrole.foot_errand_pair").MaxWadeDepthMetres, _cfg, 0.0, _month);
            Assert.That(cart.Count, Is.EqualTo(54));
            double min = double.MaxValue, max = 0.0;
            foreach (TideWindow w in cart)
            {
                min = Math.Min(min, Hours(w.DurationSeconds));
                max = Math.Max(max, Hours(w.DurationSeconds));
            }
            Assert.That(min, Is.EqualTo(4.99).Within(0.02), "0.3 m gut window at neap");
            Assert.That(max, Is.EqualTo(5.67).Within(0.02), "0.3 m gut window at spring");
        }

        // ---- crossings per window -------------------------------------------------------------------

        [Test]
        public void AWalkerCrossesOncePerWindow_SoTheFootPairStaysOver()
        {
            SupplyRoleDef foot = Role("supplyrole.foot_errand_pair");
            Assert.That(foot.Mode, Is.EqualTo(SupplyTripMode.StayOver));
            Assert.That(Hours(CrossingMath.BarLegSeconds(_route, foot.SandSpeedMetresPerSecond)), Is.EqualTo(5.82).Within(0.01),
                "611 m at 1.4 m/s = 436 s = 5.82 game h");
            foreach (TideWindow w in _windows)
                Assert.That(CrossingMath.MaxOneWayCrossingsInWindow(_water, w, foot, _route, _cfg), Is.EqualTo(1),
                    "window at " + Hours(w.StartSeconds).ToString("F2") + " h, " + Hours(w.DurationSeconds).ToString("F2") + " h long");
        }

        [Test]
        public void TheQuadGoesAndReturnsInsideEveryWindow()
        {
            SupplyRoleDef quad = Role("supplyrole.quad_runner");
            Assert.That(quad.Mode, Is.EqualTo(SupplyTripMode.SameWindowRoundTrip));
            Assert.That(Hours(CrossingMath.RoundTripBarHeadSeconds(quad, _route, _cfg)), Is.EqualTo(5.29).Within(0.01),
                "bar head to bar head: 2 x 611/5 + 2 x 372/6.5 + 0.5 h loading = 396.4 s");
            Assert.That(Hours(CrossingMath.RoundTripVillageSeconds(quad, _route, _cfg)), Is.EqualTo(5.50).Within(0.01),
                "village to village, the plan's 5.5 h");
            foreach (TideWindow w in _windows)
            {
                Assert.IsTrue(CrossingMath.TryRoundTripInWindow(_water, w, quad, _route, _cfg, double.MinValue, double.MaxValue, out _),
                    "no quad round trip in the window at " + Hours(w.StartSeconds).ToString("F2") + " h");
                Assert.That(CrossingMath.MaxOneWayCrossingsInWindow(_water, w, quad, _route, _cfg), Is.EqualTo(2));
            }
        }

        /// <summary>
        /// The plan said every day has a usable daylight window. For the CREST that is true (at least 9.4
        /// daylight hours every day), but a same-window quad round trip inside 06:00-20:00 fails on 4 days in
        /// 28: around springs low water falls near 07:00 and 19:00, so the morning window closes before a run
        /// that leaves at 06:00 can get back (day 13: bar dry 02:54-10:46, the return through the gut must
        /// start by about 05:00) and the evening run cannot be home by 20:00. Days 12-14 are consecutive, so
        /// the worst gap between possible quad runs is 4 days (day 11 to day 15): exactly the store's buffer.
        /// </summary>
        [Test]
        public void TheQuadHasADaylightRunOn24DaysIn28_AndTheBufferJustCoversTheGap()
        {
            SupplyRoleDef quad = Role("supplyrole.quad_runner");
            // Windows starting the evening before can still serve a morning, so search a day either side.
            var windows = CrossingMath.BarWindows(_water, _route, _cfg, -_cfg.SecondsPerDay, _month + 2.0 * _cfg.SecondsPerDay);
            int days = Mathf.RoundToInt(_cfg.LunarMonthDays);
            var without = new List<int>();
            int lastRunDay = -1, worstGap = 0;
            for (int day = 0; day <= days; day++)
            {
                bool run = CrossingMath.TryDaylightRoundTrip(_water, windows, day, quad, _route, _cfg, out _);
                if (!run) { if (day < days) without.Add(day); continue; }
                if (lastRunDay >= 0) worstGap = Math.Max(worstGap, day - lastRunDay);
                lastRunDay = day;
            }
            CollectionAssert.AreEqual(new[] { 12, 13, 14, 27 }, without, "days with no daylight quad round trip");
            Assert.That(worstGap, Is.EqualTo(4), "longest stretch between days a daylight quad run is possible");
            Assert.That(worstGap, Is.LessThanOrEqualTo(_route.BufferDays), "the store buffer must cover the worst gap");
        }

        // ---- demand, capacity, buffer ---------------------------------------------------------------

        [Test]
        public void TheIslandNeeds114KgAnd177LitresADayFromNmc()
        {
            var goods = LoadAll<IslandGoodDef>();
            MassVolume own = SupplyPlanMath.DailyBarDemand(goods, withPlayerAllowance: false);
            MassVolume all = SupplyPlanMath.DailyBarDemand(goods);
            Assert.That(own.MassKg, Is.EqualTo(99.7).Within(0.01));
            Assert.That(own.VolumeLitres, Is.EqualTo(155.0).Within(0.01));
            Assert.That(all.MassKg, Is.EqualTo(114.2).Within(0.01), "+25% on groceries, ice, bait, hardware = +14.5 kg");
            Assert.That(all.VolumeLitres, Is.EqualTo(177.25).Within(0.01), "+22.25 L (the plan rounded to +25)");
            Assert.That(all.MassKg * _cfg.LunarMonthDays, Is.EqualTo(3197.6).Within(0.1), "about 3.2 t a 28-day season");
        }

        [Test]
        public void TheRecommendedPlanMeetsWeeklyDemand_AndVolumeBinds()
        {
            SupplyPlanMath.Report r = SupplyPlanMath.Evaluate(LoadAll<IslandGoodDef>(), LoadAll<SupplyRoleDef>(),
                                                              CarriersById(), _route, _cfg);
            Assert.That(r.WeeklyDemand.MassKg, Is.EqualTo(799.4).Within(0.01));
            Assert.That(r.WeeklyDemand.VolumeLitres, Is.EqualTo(1240.75).Within(0.01));
            Assert.That(r.WeeklyCapacity.MassKg, Is.EqualTo(1543.5).Within(0.01), "quad 3.5 x 385 + foot pair 196");
            Assert.That(r.WeeklyCapacity.VolumeLitres, Is.EqualTo(1860.0).Within(0.01), "quad 3.5 x 420 + foot pair 390");
            Assert.IsTrue(r.CapacityMeetsDemand);
            Assert.That(r.Binding, Is.EqualTo(LoadDimension.Volume));
            Assert.That(r.MassHeadroom, Is.EqualTo(0.931).Within(0.001));
            Assert.That(r.VolumeHeadroom, Is.EqualTo(0.499).Within(0.001));
            Assert.That(r.WeeklyRoundTrips, Is.EqualTo(4.5).Within(1e-9));
            Assert.That(r.WeeklyBarCrossings, Is.EqualTo(11.0).Within(1e-9), "quad 7 + foot pair 4 person-crossings");
        }

        [Test]
        public void TheQuadAloneStillCarriesTheIsland_AndTwoDaysFitOneRun()
        {
            var goods = LoadAll<IslandGoodDef>();
            SupplyRoleDef quad = Role("supplyrole.quad_runner");
            MassVolume kit = SupplyPlanMath.KitCapacity(quad, CarriersById(), out int missing);
            Assert.That(missing, Is.Zero);
            Assert.That(kit.MassKg, Is.EqualTo(385.0).Within(1e-6));
            Assert.That(kit.VolumeLitres, Is.EqualTo(420.0).Within(1e-6));

            SupplyPlanMath.Report alone = SupplyPlanMath.Evaluate(goods, new[] { quad }, CarriersById(), _route, _cfg);
            Assert.IsTrue(alone.CapacityMeetsDemand);
            Assert.That(alone.VolumeHeadroom, Is.EqualTo(0.185).Within(0.001), "1470 L against 1240.75 L");

            MassVolume run = SupplyPlanMath.DailyBarDemand(goods).Times(quad.CadenceDays);
            LoadFit fit = SupplyPlanMath.Fit(kit.MassKg, kit.VolumeLitres, run.MassKg, run.VolumeLitres);
            Assert.IsTrue(fit.Fits);
            Assert.That(fit.Binding, Is.EqualTo(LoadDimension.Volume));
            Assert.That(fit.MassFraction, Is.EqualTo(0.593).Within(0.001), "228.4 kg of 385");
            Assert.That(fit.VolumeFraction, Is.EqualTo(0.844).Within(0.001), "354.5 L of 420");
        }

        [Test]
        public void WithoutTheQuad_WalkersMustMake12RoundTripsADay()
        {
            var carriers = CarriersById();
            double kg = carriers["carrier.hand_cart"].CapacityKg + carriers["carrier.rucksack"].CapacityKg + carriers["carrier.shopping_bag"].CapacityKg;
            double l = carriers["carrier.hand_cart"].CapacityLitres + carriers["carrier.rucksack"].CapacityLitres + carriers["carrier.shopping_bag"].CapacityLitres;
            Assert.That(kg, Is.EqualTo(93.0).Within(1e-6), "the walker kit, cart + rucksack + bag");
            Assert.That(l, Is.EqualTo(210.0).Within(1e-6));
            double trips = SupplyPlanMath.RoundTripsPerDayToCarry(SupplyPlanMath.DailyBarDemand(LoadAll<IslandGoodDef>()), new MassVolume(kg, l));
            Assert.That(trips, Is.EqualTo(1.228).Within(0.001), "114.2 / 93: weight binds for a walker");
        }

        [Test]
        public void TheStoreBufferIsFourDays_AndCoversOneMissedQuadRun()
        {
            SupplyPlanMath.Report r = SupplyPlanMath.Evaluate(LoadAll<IslandGoodDef>(), LoadAll<SupplyRoleDef>(),
                                                              CarriersById(), _route, _cfg);
            Assert.That(_route.BufferDays, Is.EqualTo(4f));
            Assert.That(r.BufferStock.MassKg, Is.EqualTo(456.8).Within(0.01));
            Assert.That(r.BufferStock.VolumeLitres, Is.EqualTo(709.0).Within(0.01));
            Assert.That(r.ShortestCadenceDays, Is.EqualTo(2));
            Assert.That(r.MissedRunsCovered, Is.EqualTo(1));
        }

        // ---- forks and bucket -----------------------------------------------------------------------

        [Test]
        public void TheForksRefuseAFourDrumPallet_AndTakeATwoDrumOne()
        {
            CarrierDef forks = Carrier("carrier.tractor_forks");
            CarrierDef pallet = Carrier("carrier.pallet_gma");
            CarrierDef drum = Carrier("carrier.drum_205l");

            double four = SupplyPlanMath.PalletOfFullContainersKg(pallet, drum, 4);
            Assert.That(four, Is.EqualTo(780.8).Within(0.01), "20 + 4 x 190.2");
            LoadFit refused = SupplyPlanMath.Fit(forks, four, 4 * drum.CapacityLitres);
            Assert.IsFalse(refused.Fits, "600 kg forks must refuse 780.8 kg");
            Assert.That(refused.Binding, Is.EqualTo(LoadDimension.Mass));

            double two = SupplyPlanMath.PalletOfFullContainersKg(pallet, drum, 2);
            Assert.That(two, Is.EqualTo(400.4).Within(0.01));
            Assert.IsTrue(SupplyPlanMath.Fit(forks, two, 2 * drum.CapacityLitres).Fits);
        }

        [TestCase("bulk.fish_on_ice", 297.5)]
        [TestCase("bulk.wet_seaweed", 210.0)]
        [TestCase("bulk.potatoes", 234.5)]
        public void TheBucketFillsByVolume(string materialId, double expectedKg)
        {
            CarrierDef bucket = Carrier("carrier.tractor_bucket");
            BulkMaterialDef material = ById(LoadAll<BulkMaterialDef>(), m => m.Id, materialId);
            double kg = SupplyPlanMath.BulkLoadKg(bucket, material, out LoadDimension binding);
            Assert.That(kg, Is.EqualTo(expectedKg).Within(0.01), "0.35 m3 x density");
            Assert.That(binding, Is.EqualTo(LoadDimension.Volume), "the 600 kg cap does not bite for these");
        }
    }
}
