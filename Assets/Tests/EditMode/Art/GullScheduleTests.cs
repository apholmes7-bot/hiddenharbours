using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>How many gulls a place has, and when.</b> <see cref="GullSchedule"/> turns a place, the seed,
    /// the clock, the weather and the tide into a count (rule 5): no camera, no history, no randomness
    /// but the hashes. These read the places as the repo holds them — the Def files and the library's
    /// tuning block, parsed here with no engine and no asset database — so they run anywhere, and a
    /// change to the data that breaks the owner's table fails here first.
    /// </summary>
    public class GullScheduleTests
    {
        private const int EarlySpring = (int)Season.EarlySpring;
        private const int HighSummer = (int)Season.HighSummer;
        private const int TheTurn = (int)Season.TheTurn;
        private const int HardWinter = (int)Season.HardWinter;

        private const string Cannery = "birdhaunt.stp_cannery";
        private const string FlakeYard = "birdhaunt.stp_flake_yard";

        private static readonly double[] NightHours = { 23.0, 3.0 };

        private static double E(in GullTuningSpec t, in GullHauntSpec p, in GullScheduleInputs x)
            => GullSchedule.Expected(t, p, x);

        // ── determinism ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void SameSeedAndTime_GiveTheSameBirdsTwice()
        {
            GullRegionPlaces stp = GullPlaceFiles.Region(GullPlaceFiles.StPeters);
            GullTuningSpec t = GullPlaceFiles.Tuning();
            GullCalendar cal = GullPlaceFiles.Calendar(3, 6.5, HighSummer);
            GullTestWeather calm = GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater);

            GullRoster a = GullPlaceFiles.Refreshed(stp, t, cal, calm, GullPlaceFiles.CanneryView);
            GullRoster b = GullPlaceFiles.Refreshed(stp, t, cal, calm, GullPlaceFiles.CanneryView);

            var failures = new List<string>();
            int placed = 0;
            foreach ((int p, int slot) in GullPlaceFiles.Slots(a, stp.Count))
            {
                bool inA = a.StateOf(p, slot, out GullRosterBird x);
                bool inB = b.StateOf(p, slot, out GullRosterBird y);
                if (inA != inB || !x.Equals(y))
                    failures.Add(stp.IdOf(p) + " slot " + slot + ": " + GullPlaceFiles.Describe(x) + " vs " +
                                 GullPlaceFiles.Describe(y));
                if (inA) placed++;
            }

            if (a.NearCount != b.NearCount || a.StoredCount != b.StoredCount)
                failures.Add("near " + a.NearCount + "/" + a.StoredCount + " vs " + b.NearCount + "/" + b.StoredCount);
            for (int i = 0; i < Math.Min(a.StoredCount, b.StoredCount); i++)
            {
                if (!a.Near[i].Equals(b.Near[i]))
                    failures.Add("near bird " + i + ": key " + a.Near[i].Key + " vs " + b.Near[i].Key);
                if (a.NearPool[i] != b.NearPool[i])
                    failures.Add("near bird " + i + ": pool " + a.NearPool[i] + " vs " + b.NearPool[i]);
            }
            if (!a.PoolKey.SequenceEqual(b.PoolKey)) failures.Add("the pools bound different birds");

            // Another seed: the same slots, other spots.
            GullCalendar other = cal;
            other.Seed = cal.Seed + 1;
            GullRoster c = GullPlaceFiles.Refreshed(stp, t, other, calm, GullPlaceFiles.CanneryView);
            int compared = 0;
            foreach ((int p, int slot) in GullPlaceFiles.Slots(a, stp.Count))
            {
                if (!a.StateOf(p, slot, out GullRosterBird x) || x.Where != GullWhere.AtHaunt) continue;
                if (!c.StateOf(p, slot, out GullRosterBird y) || y.Where != GullWhere.AtHaunt) continue;
                compared++;
                if (x.X == y.X || x.Y == y.Y)
                    failures.Add(stp.IdOf(p) + " slot " + slot + " sat on the same spot under seed " + other.Seed);
            }

            Assert.That(placed, Is.GreaterThan(0), "no bird was placed at all: the test proves nothing");
            Assert.That(compared, Is.GreaterThan(0), "no bird was at its haunt under both seeds");
            GullAssert.None(failures, "the same seed and time gave different birds");
        }

        // ── the hour ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NightMeansRoost()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning();
            GullTestWeather calm = GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater);
            var failures = new List<string>();
            int roosting = 0;

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                int n = region.Count;
                foreach (double hour in NightHours)
                {
                    for (int season = 0; season < 4; season++)
                        foreach (GullSky sky in GullSky.All)
                        {
                            GullScheduleInputs x = GullPlaceFiles.Inputs(season, hour, sky);
                            for (int p = 0; p < n; p++)
                            {
                                if (!places[p].IsRoost)
                                {
                                    double e = E(t, places[p], x);
                                    if (e != 0.0)
                                        failures.Add(region.IdOf(p) + " keeps " + e + " birds at " + hour + ":00 (" +
                                                     sky + ", season " + season + ")");
                                    continue;
                                }
                                double pop = 0.0;
                                for (int i = 0; i < n; i++)
                                    if (!places[i].IsRoost && places[i].RoostIndex == p)
                                        pop += GullSchedule.Population(t, places[i], x);
                                double want = Math.Min(places[p].Room, (1.0 + places[p].Residents[season]) * pop);
                                double got = GullSchedule.RoostCount(t, places, n, p, x);
                                if (Math.Abs(got - want) > 1e-9)
                                    failures.Add(region.IdOf(p) + " holds " + got + ", not " + want + " at " + hour +
                                                 ":00 (" + sky + ", season " + season + ")");
                            }
                        }

                    GullRoster roster = GullPlaceFiles.Refreshed(region, t, GullPlaceFiles.Calendar(3, hour, HighSummer),
                                                                 calm, GullPlaceFiles.CanneryView);
                    foreach ((int p, int slot) in GullPlaceFiles.Slots(roster, n))
                    {
                        if (!roster.StateOf(p, slot, out GullRosterBird bird)) continue;
                        if (bird.Where == GullWhere.AtRoost) roosting++;
                        else failures.Add(region.IdOf(p) + " slot " + slot + " is " + GullPlaceFiles.Describe(bird) +
                                          " at " + hour + ":00");
                    }
                }
            }

            Assert.That(roosting, Is.GreaterThan(0), "no bird roosted at night: the test proves nothing");
            GullAssert.None(failures, "a bird was out at night");
        }

        [Test]
        public void TheHourMovesTheCountTheRightWay()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning();
            var failures = new List<string>();

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    GullHauntSpec place = places[p];
                    double At(double hour) => E(t, place, GullPlaceFiles.Inputs(HighSummer, hour, GullSky.Calm));

                    string id = region.IdOf(p);
                    double dawn = At(6.5), morning = At(9.0), midday = At(13.0), evening = At(19.0);
                    if (!(dawn < morning)) failures.Add(id + ": 06:30 " + dawn + " is not under 09:00 " + morning);
                    bool works = place.Kind == BirdHauntKind.FishPlant || place.Kind == BirdHauntKind.Wharf;
                    if (works && !(midday < morning)) failures.Add(id + ": 13:00 " + midday + " is not under 09:00 " + morning);
                    if (!(evening < midday)) failures.Add(id + ": 19:00 " + evening + " is not under 13:00 " + midday);
                    if (At(23.0) != 0.0 || At(3.0) != 0.0) failures.Add(id + ": birds at night");

                    double last = -1.0;
                    for (int i = 0; i <= 30; i++)
                    {
                        double hour = 5.5 + 0.05 * i;
                        double e = At(hour);
                        if (e < last) failures.Add(id + ": the dawn count falls at " + hour.ToString("0.00"));
                        last = e;
                    }
                }
            }

            GullAssert.None(failures, "the hour moved a count the wrong way");
        }

        // ── the season ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheSeasonMovesTheCountTheRightWay()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning(jitter: false);
            var failures = new List<string>();
            int roosts = 0;

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                int n = region.Count;
                for (int r = 0; r < n; r++)
                {
                    if (!places[r].IsRoost) continue;
                    roosts++;
                    double Colony(int season)
                        => GullSchedule.ResidentsExpected(t, places, n, r, GullPlaceFiles.Inputs(season, 9.0, GullSky.Calm));
                    double es = Colony(EarlySpring), hs = Colony(HighSummer), turn = Colony(TheTurn), hw = Colony(HardWinter);
                    string id = region.IdOf(r);
                    if (!(es > turn)) failures.Add(id + ": Early Spring " + es + " is not over The Turn " + turn);
                    if (!(hs > turn)) failures.Add(id + ": High Summer " + hs + " is not over The Turn " + turn);
                    if (!(turn > hw)) failures.Add(id + ": The Turn " + turn + " is not over Hard Winter " + hw);
                }
            }

            GullRegionPlaces stp = GullPlaceFiles.Region(GullPlaceFiles.StPeters);
            GullHauntSpec flakes = stp.Places[stp.Index(FlakeYard)];
            double winter = E(t, flakes, GullPlaceFiles.Inputs(HardWinter, 9.0, GullSky.Calm));
            double summer = E(t, flakes, GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm));
            if (!(winter < summer)) failures.Add("the flake yard holds " + winter + " in Hard Winter, " + summer + " in High Summer");

            Assert.That(roosts, Is.GreaterThan(0), "no roost in the data");
            GullAssert.None(failures, "the season moved a count the wrong way");
        }

        [Test]
        public void TheYoungOfTheYearArriveLateInHighSummer()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning();
            Assert.That(GullSchedule.Young(t, HighSummer, 0.4), Is.EqualTo(1.0), "before halfway through High Summer");
            Assert.That(GullSchedule.Young(t, HighSummer, 0.95), Is.EqualTo(1.18).Within(1e-6), "late High Summer");
            Assert.That(GullSchedule.Young(t, TheTurn, 0.5), Is.EqualTo(1.15).Within(1e-6), "halfway through The Turn");
            foreach (double progress in new[] { 0.0, 0.5, 0.95 })
            {
                Assert.That(GullSchedule.Young(t, HardWinter, progress), Is.EqualTo(1.0), "Hard Winter");
                Assert.That(GullSchedule.Young(t, EarlySpring, progress), Is.EqualTo(1.0), "Early Spring");
            }

            double last = 0.0;
            for (int i = 0; i <= 100; i++)
            {
                double young = GullSchedule.Young(t, HighSummer, i / 100.0);
                Assert.That(young, Is.GreaterThanOrEqualTo(last), "High Summer's young fell at progress " + i / 100.0);
                last = young;
            }
        }

        // ── the weather ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void AGaleEmptiesTheExposedHaunts_AndGroundsTheSheltered()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning(jitter: false);
            Assert.That(t.ShelterKeep, Is.EqualTo(0.35).Within(1e-6), "the owner's lean: a sheltered place keeps 35%");
            var failures = new List<string>();
            var sheltered = new List<string>();

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    string id = region.IdOf(p);
                    if (places[p].Sheltered) sheltered.Add(id);
                    for (int hour = 7; hour <= 16; hour++)
                    {
                        double calm = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, hour, GullSky.Calm));
                        double gale = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, hour, GullSky.Gale));
                        if (!places[p].Sheltered)
                        {
                            if (gale != 0.0) failures.Add(id + " (exposed) keeps " + gale + " in a gale at " + hour + ":00");
                        }
                        else if (!(gale > 0.0) || Math.Abs(gale - t.ShelterKeep * calm) > 1e-12 * calm)
                        {
                            failures.Add(id + " (sheltered) keeps " + gale + " of " + calm + " in a gale at " + hour + ":00");
                        }
                    }
                }
            }

            for (int hour = 7; hour <= 16; hour++)
            {
                double share = GullSchedule.RoostShare(t, hour, GullSky.Gale.Storm, GullSky.Gale.Visibility);
                if (share != 1.0) failures.Add("in a gale at " + hour + ":00 only " + share + " of the absent birds roost");
            }

            GullConfig cfg = GullConfig.Default;
            double settled = GullSchedule.SettledShare(t, cfg.SettledFraction, cfg.FogSettledFraction,
                                                       cfg.GaleSettledFraction, GullSky.Gale.Storm, GullSky.Gale.Visibility);
            if (settled != 1.0) failures.Add("in a gale only " + settled + " of the birds that stay are on their feet");

            CollectionAssert.AreEquivalent(new[] { Cannery, "birdhaunt.stp_wharf", "birdhaunt.nmc_wharf" }, sheltered,
                                           "the sheltered places are the cannery and the two wharves");
            GullAssert.None(failures, "a gale left the wrong birds");
        }

        [Test]
        public void ARisingWindTellsAheadOfTheGale()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning();
            double tell = t.GaleTellHours * GullPlaceFiles.SecondsPerHour;
            Assert.That(tell, Is.EqualTo(GullPlaceFiles.SecondsPerHour), "the owner's lean: they read the storm a game hour ahead");

            double galeAt = GullPlaceFiles.At(3, 12.0);
            var weather = new GullTestWeather(s => s >= galeAt ? GullSky.Gale.Storm : GullSky.Calm.Storm,
                                              s => GullPlaceFiles.LowWater);
            GullCalendar cal = GullPlaceFiles.Calendar(3, 11.0, HighSummer);
            GullScheduleInputs told = GullSchedule.InputsAt(t, cal, weather, galeAt - tell);
            GullScheduleInputs before = GullSchedule.InputsAt(t, cal, weather, galeAt - tell - 1.0);
            Assert.That(told.Storm, Is.EqualTo(GullSky.Gale.Storm), "a game hour before the gale, the birds read it");
            Assert.That(before.Storm, Is.EqualTo(GullSky.Calm.Storm), "a second earlier, they do not");

            GullRegionPlaces stp = GullPlaceFiles.Region(GullPlaceFiles.StPeters);
            GullHauntSpec[] places = stp.Places;
            var failures = new List<string>();
            int exposed = 0;
            for (int p = 0; p < stp.Count; p++)
            {
                if (places[p].IsRoost || places[p].Sheltered) continue;
                exposed++;
                double gone = E(t, places[p], told), still = E(t, places[p], before);
                if (gone != 0.0) failures.Add(stp.IdOf(p) + " keeps " + gone + " a game hour before the gale");
                if (!(still > 0.0)) failures.Add(stp.IdOf(p) + " is empty before the birds could read the gale");
            }

            Assert.That(exposed, Is.GreaterThan(0), "no exposed place in St Peters");
            GullAssert.None(failures, "the birds did not read the gale ahead");
        }

        [Test]
        public void FogThinsTheFlock()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning(jitter: false);
            var failures = new List<string>();
            var fogs = new[] { (Visibility: 0.25, Keep: 0.475), (Visibility: 0.2, Keep: 0.4), (Visibility: 0.1, Keep: 0.4) };
            bool sawFineOnly = false;

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    double clear = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm));
                    // A place that works only on fine days loses its working-hours boost in the fog too.
                    bool fineOnly = places[p].BusyOnFineDaysOnly
                                    && GullSchedule.IsBusy(t, places[p], 9.0, GullSky.Calm.Storm, GullSky.Calm.Visibility);
                    sawFineOnly |= fineOnly;
                    foreach (var fog in fogs)
                    {
                        var sky = new GullSky("fog " + fog.Visibility, GullSky.Calm.Storm, fog.Visibility);
                        double thinned = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, 9.0, sky));
                        double want = fineOnly ? fog.Keep / places[p].BusyBoost : fog.Keep;
                        if (!(clear > 0.0) || Math.Abs(thinned / clear - want) > 1e-6)
                            failures.Add(region.IdOf(p) + " keeps " + thinned + " of " + clear + " at visibility " +
                                         fog.Visibility + ", not " + want);
                    }
                }
            }

            Assert.That(GullSchedule.RoostShare(t, 9.0, GullSky.Calm.Storm, 0.25), Is.EqualTo(0.55625).Within(1e-6));
            Assert.That(GullSchedule.RoostShare(t, 9.0, GullSky.Calm.Storm, 0.2), Is.EqualTo(0.6).Within(1e-6));
            double last = double.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                double share = GullSchedule.RoostShare(t, 9.0, GullSky.Calm.Storm, i * 0.05);
                if (share > last) failures.Add("the roost share rose as the fog cleared, at visibility " + i * 0.05);
                last = share;
            }

            Assert.IsTrue(sawFineOnly, "the flake yard works only on fine days: the fog test lost its case");
            GullAssert.None(failures, "the fog thinned the birds wrongly");
        }

        // ── the tide and the market ─────────────────────────────────────────────────────────────

        [Test]
        public void LowWaterFillsTheFlats()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning(jitter: false);
            var failures = new List<string>();
            var coveredAtHighWater = new HashSet<string> { "birdhaunt.stp_ne_bar", "birdhaunt.nmc_landing_flats" };
            int tidal = 0, beaches = 0, covered = 0;

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    string id = region.IdOf(p);
                    double low = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm, GullPlaceFiles.LowWater));
                    double high = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm, GullPlaceFiles.HighWater));
                    if (!places[p].HasTide)
                    {
                        if (low != high) failures.Add(id + " has no tide, yet holds " + low + " at low water and " + high + " at high");
                        continue;
                    }

                    tidal++;
                    if (!(low > 0.0)) { failures.Add(id + " is empty at low water"); continue; }
                    double keep = high / low;
                    if (Math.Abs(keep - places[p].HighWaterKeep) > 1e-9)
                        failures.Add(id + " keeps " + keep + " at high water, not its " + places[p].HighWaterKeep);
                    if (places[p].Kind == BirdHauntKind.Beach)
                    {
                        beaches++;
                        if (Math.Abs(keep - 0.45) > 1e-6) failures.Add(id + " (a beach) keeps " + keep + " at high water, not 0.45");
                    }
                    if (coveredAtHighWater.Contains(id))
                    {
                        covered++;
                        if (high != 0.0) failures.Add(id + " holds " + high + " at high water: it should be covered");
                    }
                }
            }

            Assert.That(tidal, Is.GreaterThan(0), "no tidal place in the data");
            Assert.That(beaches, Is.GreaterThan(0), "no beach in the data");
            Assert.That(covered, Is.EqualTo(coveredAtHighWater.Count), "a covered-at-high-water place is missing");
            GullAssert.None(failures, "the tide moved the birds wrongly");
        }

        [Test]
        public void EachInputMovesTheCountOneWay()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning();
            const double slack = 1e-12;
            var failures = new List<string>();

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    string id = region.IdOf(p);
                    foreach (double hour in new[] { 6.5, 9.0, 13.0, 19.0 })
                    {
                        double last = double.MaxValue;
                        for (int i = 0; i <= 20; i++)
                        {
                            double e = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, hour, new GullSky("storm", i * 0.05, 1.0)));
                            if (e > last + slack) failures.Add(id + " gains birds as the storm rises to " + i * 0.05 + " at " + hour);
                            last = e;
                        }

                        last = -1.0;
                        for (int i = 0; i <= 20; i++)
                        {
                            double e = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, hour, new GullSky("fog", GullSky.Calm.Storm, i * 0.05)));
                            if (e < last - slack) failures.Add(id + " loses birds as the fog clears to " + i * 0.05 + " at " + hour);
                            last = e;
                        }

                        last = double.MaxValue;
                        for (int i = 0; i <= 120; i++)
                        {
                            double tide = -3.0 + 0.05 * i;
                            double e = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, hour, GullSky.Calm, tide));
                            if (e > last + slack) failures.Add(id + " gains birds as the tide rises to " + tide + " at " + hour);
                            last = e;
                        }

                        last = -1.0;
                        for (int i = 0; i <= 100; i++)
                        {
                            double progress = i / 100.0;
                            double e = E(t, places[p], GullPlaceFiles.Inputs(HighSummer, hour, GullSky.Calm, progress: progress));
                            if (e < last - slack) failures.Add(id + " loses birds as High Summer runs on to " + progress + " at " + hour);
                            last = e;
                        }
                    }
                }
            }

            GullAssert.None(failures, "an input moved a count both ways");
        }

        [Test]
        public void TheCannerysShiftBringsBirds()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning(jitter: false);
            GullRegionPlaces stp = GullPlaceFiles.Region(GullPlaceFiles.StPeters);
            GullHauntSpec[] places = stp.Places;
            GullHauntSpec cannery = places[stp.Index(Cannery)];
            Assert.That(cannery.BusyBoost, Is.EqualTo(1.3).Within(1e-6), "the owner's lean: the shift brings 1.3x");

            double calm = GullSky.Calm.Storm;
            Assert.That(GullSchedule.BusyFactor(t, cannery, 6.9, calm, 1.0), Is.EqualTo(1.0), "06:54, before the shift");
            Assert.That(GullSchedule.BusyFactor(t, cannery, 7.0, calm, 1.0), Is.EqualTo(cannery.BusyBoost), "07:00, the shift starts");
            Assert.That(GullSchedule.BusyFactor(t, cannery, 15.9, calm, 1.0), Is.EqualTo(cannery.BusyBoost), "15:54, still working");
            Assert.That(GullSchedule.BusyFactor(t, cannery, 16.0, calm, 1.0), Is.EqualTo(1.0), "16:00, the shift is over");

            GullHauntSpec idle = cannery;
            idle.BusyBoost = 1.0;
            GullScheduleInputs nine = GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm);
            double working = E(t, cannery, nine), quiet = E(t, idle, nine);
            Assert.That(working, Is.EqualTo(cannery.BusyBoost * quiet).Within(1e-9 * working), "the shift's birds at 09:00");

            GullHauntSpec flakes = places[stp.Index(FlakeYard)];
            Assert.That(GullSchedule.BusyFactor(t, flakes, 13.0, GullSky.Calm.Storm, GullSky.Calm.Visibility),
                        Is.EqualTo(flakes.BusyBoost), "the flakes are out on a fine day");
            Assert.That(flakes.BusyBoost, Is.EqualTo(1.3).Within(1e-6), "the owner's lean: the flake yard brings 1.3x");
            Assert.That(GullSchedule.BusyFactor(t, flakes, 13.0, GullSky.Windy.Storm, GullSky.Windy.Visibility),
                        Is.EqualTo(1.0), "no flakes spread in the wind");
            Assert.That(GullSchedule.BusyFactor(t, flakes, 13.0, GullSky.Fog.Storm, GullSky.Fog.Visibility),
                        Is.EqualTo(1.0), "no flakes spread in the fog");
        }

        [Test]
        public void MarketDayFillsTheWharf()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning(jitter: false);
            var failures = new List<string>();
            int wharves = 0;

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    string id = region.IdOf(p);
                    GullScheduleInputs plain = GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm);
                    GullScheduleInputs market = GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm, marketDay: true);
                    double e0 = E(t, places[p], plain), e1 = E(t, places[p], market);
                    double p0 = GullSchedule.Population(t, places[p], plain), p1 = GullSchedule.Population(t, places[p], market);
                    if (places[p].Kind == BirdHauntKind.Wharf)
                    {
                        wharves++;
                        if (Math.Abs(e1 / e0 - 1.2) > 1e-6) failures.Add(id + ": market day brings " + e1 / e0 + "x, not 1.2x");
                        if (Math.Abs(p1 / p0 - 1.2) > 1e-6) failures.Add(id + ": market day's population is " + p1 / p0 + "x, not 1.2x");
                    }
                    else if (e0 != e1 || p0 != p1)
                    {
                        failures.Add(id + " is not a wharf, yet market day moves it: " + e0 + " -> " + e1);
                    }
                }
            }

            int weekday = GullSchedule.Mod(3, GullPlaceFiles.DaysPerWeek);
            GullCalendar cal = GullPlaceFiles.Calendar(3, 9.0, HighSummer, marketDayIndex: weekday);
            GullTestWeather calm = GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater);
            Assert.IsTrue(GullSchedule.InputsAt(t, cal, calm, cal.Seconds).MarketDay, "market day");
            Assert.IsFalse(GullSchedule.InputsAt(t, cal, calm, cal.Seconds + GullPlaceFiles.SecondsPerDay).MarketDay, "the day after");
            Assert.IsTrue(GullSchedule.InputsAt(t, cal, calm, cal.Seconds + GullPlaceFiles.DaysPerWeek * GullPlaceFiles.SecondsPerDay).MarketDay,
                          "a week on");

            Assert.That(wharves, Is.EqualTo(2), "the two wharves: St Peters' and Nine Mile Creek's");
            GullAssert.None(failures, "market day moved the wrong birds");
        }

        // ── surface schools ─────────────────────────────────────────────────────────────────────

        [Test]
        public void SurfaceSchoolsDrawBirds_DeepOnesDoNot()
        {
            double surface = DepthDropSettings.Default.ShallowsMaxMeters;
            Assert.IsTrue(GullSchedule.SchoolShows(surface, surface), "a school at the surface depth shows");
            Assert.IsFalse(GullSchedule.SchoolShows(surface + 0.01, surface), "a school below it does not");

            GullTuningSpec t = GullPlaceFiles.Tuning();
            double now = GullPlaceFiles.At(3, 9.0);
            GullHauntSpec school = GullPlaceFiles.School(t, 120.0, -60.0, 6.0, now - 1000.0, now + 1000.0);

            GullScheduleInputs nine = GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Calm);
            Assert.That(E(t, school, nine), Is.EqualTo(6.0).Within(1e-6), "a school's birds at 09:00");
            Assert.That(E(t, school, GullPlaceFiles.Inputs(HighSummer, 13.0, GullSky.Calm)), Is.EqualTo(4.2).Within(1e-6),
                        "a school's birds at 13:00");
            Assert.That(E(t, school, GullPlaceFiles.Inputs(HighSummer, 9.0, GullSky.Gale)), Is.EqualTo(0.0), "in a gale");
            GullScheduleInputs late = nine;
            late.Seconds = now + 1000.0;
            Assert.That(E(t, school, late), Is.EqualTo(0.0), "once the school has gone down");
            GullScheduleInputs early = nine;
            early.Seconds = now - 1000.5;
            Assert.That(E(t, school, early), Is.EqualTo(0.0), "before the school comes up");
            Assert.That(GullSchedule.Population(t, school, nine), Is.EqualTo(0.0), "a school has no birds off the water");
            Assert.That(E(t, school, nine), Is.EqualTo(GullSchedule.SchoolBirds(t, school, nine)));

            GullRegionPlaces stp = GullPlaceFiles.Region(GullPlaceFiles.StPeters);
            int n = stp.Count;
            GullHauntSpec[] places = stp.Places.Concat(new[] { school }).ToArray();
            var roster = new GullRoster(places.Length, GullFlock.MaxNearBirds, GullConfig.Default.Count);
            roster.Refresh(places, n, n + 1, t, GullPlaceFiles.Settings(stp.Bounds),
                           GullPlaceFiles.Calendar(3, 9.0, HighSummer),
                           GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater),
                           GullRect.Box(120.0, -60.0, 14.2, 8.0));

            var failures = new List<string>();
            roster.SlotRange(n, out int first, out int end);
            Assert.That(end - first, Is.EqualTo(6), "the school's slots");
            int over = 0;
            for (int slot = first; slot < end; slot++)
            {
                if (!roster.StateOf(n, slot, out GullRosterBird bird) || bird.Where != GullWhere.AtHaunt || bird.At != n)
                {
                    failures.Add("school slot " + slot + " is " + GullPlaceFiles.Describe(bird));
                    continue;
                }
                over++;
                double dx = bird.X - 120.0, dy = bird.Y + 60.0;
                if (dx * dx + dy * dy > 36.0 + 1e-9) failures.Add("school slot " + slot + " is off the school: " + GullPlaceFiles.Describe(bird));
            }

            var keys = new HashSet<int>();
            for (int i = 0; i < roster.StoredCount; i++)
                if (!keys.Add(roster.Near[i].Key)) failures.Add("key " + roster.Near[i].Key + " is listed twice");

            Assert.That(over, Is.EqualTo(6), "every bird over the school");
            GullAssert.None(failures, "the school's birds are wrong");
        }

        // ── bounds and order ────────────────────────────────────────────────────────────────────

        [Test]
        public void CountsNeverExceedCapacityOrRoom()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning();
            var failures = new List<string>();
            int bad = 0;
            void Fail(string what)
            {
                if (bad++ < 30) failures.Add(what);
            }

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                int n = region.Count;
                for (int day = 0; day < 7; day++)
                for (int season = 0; season < 4; season++)
                foreach (double progress in new[] { 0.0, 0.5, 0.95 })
                for (int hour = 0; hour <= 24; hour++)
                foreach (GullSky sky in GullSky.All)
                foreach (double tide in new[] { GullPlaceFiles.LowWater, 0.0, GullPlaceFiles.HighWater })
                foreach (bool market in new[] { false, true })
                {
                    GullScheduleInputs x = GullPlaceFiles.Inputs(season, hour, sky, tide, market, progress, day);
                    for (int p = 0; p < n; p++)
                    {
                        string At() => region.IdOf(p) + " day " + day + " season " + season + " " + progress + " " + hour +
                                       ":00 " + sky + " tide " + tide + (market ? " market" : "");
                        if (places[p].IsRoost)
                        {
                            double colony = GullSchedule.ResidentsExpected(t, places, n, p, x);
                            double roost = GullSchedule.RoostCount(t, places, n, p, x);
                            if (!(colony >= 0.0 && colony <= places[p].Room)) Fail(At() + ": colony " + colony);
                            if (!(roost >= 0.0 && roost <= places[p].Room)) Fail(At() + ": roost " + roost);
                            continue;
                        }
                        double e = E(t, places[p], x), pop = GullSchedule.Population(t, places[p], x);
                        if (!(e >= 0.0 && e <= pop && pop <= places[p].Capacity))
                            Fail(At() + ": " + e + " of " + pop + " (capacity " + places[p].Capacity + ")");
                    }
                }
            }

            GullAssert.None(failures, bad + " count(s) out of bounds");
        }

        [Test]
        public void BirdsArriveOneAtATime()
        {
            var failures = new List<string>();
            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    int capacity = places[p].Capacity;
                    for (int day = 0; day < 10; day++)
                    {
                        var thresholds = new double[capacity];
                        for (int k = 0; k < capacity; k++)
                            thresholds[k] = GullSchedule.SlotThreshold(GullPlaceFiles.Seed, places[p].IdHash, k, day);
                        for (int k = 1; k < capacity; k++)
                            if (!(thresholds[k] > thresholds[k - 1]))
                                failures.Add(region.IdOf(p) + " day " + day + ": slot " + k + " arrives before slot " + (k - 1));
                        for (int k = 1; k < capacity; k++)
                        {
                            double count = 0.5 * (thresholds[k - 1] + thresholds[k]);
                            for (int j = 0; j < capacity; j++)
                                if ((thresholds[j] < count) != (j < k))
                                    failures.Add(region.IdOf(p) + " day " + day + ": at " + count + " slot " + j +
                                                 (j < k ? " is missing" : " is there out of turn"));
                        }
                    }
                }
            }

            GullRegionPlaces stp = GullPlaceFiles.Region(GullPlaceFiles.StPeters);
            GullTuningSpec t = GullPlaceFiles.Tuning();
            GullRoster roster = GullPlaceFiles.Refreshed(stp, t, GullPlaceFiles.Calendar(3, 9.0, HighSummer),
                                                         GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater),
                                                         GullPlaceFiles.CanneryView);
            int present = 0;
            for (int p = 0; p < stp.Count; p++)
            {
                GullHauntSpec place = stp.Places[p];
                if (place.IsRoost) continue;
                int want = 0;
                for (int k = 0; k < place.Capacity; k++)
                    if (GullSchedule.SlotThreshold(GullPlaceFiles.Seed, place.IdHash, k, 3) < roster.NowExpected(p)) want++;
                roster.SlotRange(p, out int first, out int end);
                for (int slot = first; slot < end; slot++)
                {
                    bool here = roster.StateOf(p, slot, out GullRosterBird bird)
                                && (bird.Where == GullWhere.AtHaunt || (bird.Where == GullWhere.Commuting && bird.At == p));
                    if (here) present++;
                    if (here != (slot < want))
                        failures.Add(stp.IdOf(p) + " at 09:00: slot " + slot + (here ? " is there" : " is missing") +
                                     " with " + want + " expected");
                }
            }

            Assert.That(present, Is.GreaterThan(0), "no bird at any haunt at 09:00");
            GullAssert.None(failures, "the birds did not arrive one at a time");
        }

        [Test]
        public void TheDayJitterStaysInItsBand()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning();
            Assert.That(t.DayJitter, Is.EqualTo(0.15).Within(1e-6), "the owner's lean: a day is +/-15%");
            var failures = new List<string>();
            bool sawCannery = false;

            foreach (string regionId in GullPlaceFiles.Regions)
            {
                GullRegionPlaces region = GullPlaceFiles.Region(regionId);
                GullHauntSpec[] places = region.Places;
                for (int p = 0; p < region.Count; p++)
                {
                    if (places[p].IsRoost) continue;
                    var seen = new HashSet<double>();
                    for (int day = 0; day < 100; day++)
                    {
                        double j = GullSchedule.Jitter(t, GullPlaceFiles.Seed, places[p].IdHash, day);
                        if (Math.Abs(j - 1.0) > t.DayJitter + 1e-12) failures.Add(region.IdOf(p) + " day " + day + ": jitter " + j);
                        seen.Add(j);
                    }
                    if (region.IdOf(p) == Cannery)
                    {
                        sawCannery = true;
                        if (seen.Count < 95) failures.Add("the cannery has only " + seen.Count + " different days in 100");
                    }
                }

                GullTestWeather calm = GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater);
                for (int day = 0; day < 10; day++)
                {
                    GullCalendar cal = GullPlaceFiles.Calendar(day, 6.0, HighSummer);
                    GullScheduleInputs morning = GullSchedule.InputsAt(t, cal, calm, cal.Seconds);
                    GullScheduleInputs evening = GullSchedule.InputsAt(t, cal, calm, cal.Seconds + 12.0 * GullPlaceFiles.SecondsPerHour);
                    for (int p = 0; p < region.Count; p++)
                    {
                        double a = GullSchedule.Population(t, places[p], morning), b = GullSchedule.Population(t, places[p], evening);
                        if (a != b) failures.Add(region.IdOf(p) + " day " + day + ": " + a + " birds at 06:00, " + b + " at 18:00");
                    }
                }
            }

            Assert.IsTrue(sawCannery, "the cannery is missing from the data");
            GullAssert.None(failures, "the day's jitter left its band");
        }

        // ── the owner's table ───────────────────────────────────────────────────────────────────

        private readonly struct HauntCell
        {
            public readonly string Id;
            public readonly int Season;
            public readonly double Hour, Tide, Progress, Expected;
            public readonly GullSky Sky;
            public readonly bool Market;

            public HauntCell(string id, int season, double hour, GullSky sky, double tide, bool market, double progress,
                             double expected)
            {
                Id = id;
                Season = season;
                Hour = hour;
                Sky = sky;
                Tide = tide;
                Market = market;
                Progress = progress;
                Expected = expected;
            }
        }

        private readonly struct RoostCell
        {
            public readonly string Id;
            public readonly int Season;
            public readonly double Hour, Tide, Expected;

            public RoostCell(string id, int season, double hour, double tide, double expected)
            {
                Id = id;
                Season = season;
                Hour = hour;
                Tide = tide;
                Expected = expected;
            }
        }

        /// <summary>
        /// The owner's table (DESIGN section 6), at the day's jitter set to 1: thirteen moments the
        /// birds were checked against by hand. A cell that moves is a change in the data or the rules,
        /// and the owner hears about it.
        /// </summary>
        [Test]
        public void TheOwnerTableHolds()
        {
            GullTuningSpec t = GullPlaceFiles.Tuning(jitter: false);
            const double low = GullPlaceFiles.LowWater, high = GullPlaceFiles.HighWater;
            HauntCell[] haunts =
            {
                new HauntCell(Cannery, HighSummer, 9.0, GullSky.Calm, low, false, 0.5, 15.6),
                new HauntCell(Cannery, HighSummer, 13.0, GullSky.Gale, low, false, 0.5, 4.095),
                new HauntCell(Cannery, HighSummer, 9.0, GullSky.Calm, low, false, 0.95, 18.408),
                new HauntCell("birdhaunt.stp_wharf", HardWinter, 9.0, GullSky.Fog, high, false, 0.5, 4.199),
                new HauntCell("birdhaunt.stp_wharf", HighSummer, 9.0, GullSky.Calm, high, true, 0.5, 12.48),
                new HauntCell("birdhaunt.stp_crossing_mussels", TheTurn, 13.0, GullSky.Calm, low, false, 0.5, 6.44),
                new HauntCell("birdhaunt.stp_crossing_mussels", TheTurn, 13.0, GullSky.Calm, high, false, 0.5, 2.254),
                new HauntCell("birdhaunt.nmc_landing_flats", EarlySpring, 6.5, GullSky.Windy, low, false, 0.5, 2.625),
                new HauntCell(FlakeYard, HighSummer, 13.0, GullSky.Calm, 0.0, false, 0.5, 3.9),
                new HauntCell(FlakeYard, HighSummer, 13.0, GullSky.Fog, 0.0, false, 0.5, 1.425),
            };
            RoostCell[] roosts =
            {
                new RoostCell("birdroost.stp_whelps", HighSummer, 23.0, low, 40.0),
                new RoostCell("birdroost.stp_whelps", HighSummer, 9.0, low, 14.45),
                new RoostCell("birdroost.stp_sisters", TheTurn, 13.0, high, 7.52675),
            };

            var regions = GullPlaceFiles.Regions.Select(GullPlaceFiles.Region).ToArray();
            GullRegionPlaces RegionOf(string id)
            {
                GullRegionPlaces found = regions.FirstOrDefault(r => r.IndexOf(id) >= 0);
                Assert.IsNotNull(found, id + " is not in the data");
                return found;
            }

            var failures = new List<string>();
            foreach (HauntCell cell in haunts)
            {
                GullRegionPlaces region = RegionOf(cell.Id);
                GullHauntSpec place = region.Places[region.Index(cell.Id)];
                GullScheduleInputs x = GullPlaceFiles.Inputs(cell.Season, cell.Hour, cell.Sky, cell.Tide, cell.Market, cell.Progress);
                double got = E(t, place, x);
                if (Math.Abs(got - cell.Expected) > 1e-4)
                    failures.Add(cell.Id + " season " + cell.Season + " " + cell.Hour + ":00 " + cell.Sky + " tide " + cell.Tide +
                                 (cell.Market ? " market" : "") + " progress " + cell.Progress + ": " + got + ", not " + cell.Expected);
            }
            foreach (RoostCell cell in roosts)
            {
                GullRegionPlaces region = RegionOf(cell.Id);
                GullHauntSpec[] places = region.Places;
                GullScheduleInputs x = GullPlaceFiles.Inputs(cell.Season, cell.Hour, GullSky.Calm, cell.Tide);
                double got = GullSchedule.RoostCount(t, places, region.Count, region.Index(cell.Id), x);
                if (Math.Abs(got - cell.Expected) > 1e-4)
                    failures.Add(cell.Id + " season " + cell.Season + " " + cell.Hour + ":00 tide " + cell.Tide + ": " + got +
                                 ", not " + cell.Expected);
            }

            GullAssert.None(failures, "the owner's table moved");
        }
    }

    // ── the shared reading of the place files ───────────────────────────────────────────────────

    /// <summary>A sky to test under: a storm level (sea state 0..1) and a visibility (0..1).</summary>
    internal readonly struct GullSky
    {
        public readonly string Name;
        public readonly double Storm, Visibility;

        public GullSky(string name, double storm, double visibility)
        {
            Name = name;
            Storm = storm;
            Visibility = visibility;
        }

        public static readonly GullSky Calm = new GullSky("calm", 0.10, 1.0);
        public static readonly GullSky Windy = new GullSky("windy", 0.50, 1.0);
        public static readonly GullSky Gale = new GullSky("gale", 0.85, 0.8);
        public static readonly GullSky Fog = new GullSky("fog", 0.10, 0.25);
        public static readonly GullSky[] All = { Calm, Windy, Gale, Fog };

        public override string ToString() => Name;
    }

    /// <summary>The weather as two functions of game time, for the schedule's look ahead.</summary>
    internal sealed class GullTestWeather : IGullWeather
    {
        private readonly Func<double, double> _sea, _tide;

        public GullTestWeather(Func<double, double> sea, Func<double, double> tide)
        {
            _sea = sea;
            _tide = tide;
        }

        public double SeaStateAt(double gameSeconds) => _sea(gameSeconds);
        public double TideAt(double gameSeconds) => _tide(gameSeconds);

        public static GullTestWeather Steady(double storm, double tide) => new GullTestWeather(s => storm, s => tide);
    }

    internal static class GullAssert
    {
        /// <summary>Fails once with every collected failure (Unity's NUnit has no Assert.Multiple).</summary>
        public static void None(List<string> failures, string what)
        {
            if (failures.Count == 0) return;
            Assert.Fail(what + ": " + failures.Count + " failure(s)\n  " + string.Join("\n  ", failures.Take(40)));
        }
    }

    /// <summary>
    /// A small reader for the Unity YAML a ScriptableObject is saved in: maps, block sequences and
    /// plain scalars, enough for one MonoBehaviour's fields. It reads the files as the repo holds
    /// them, so the tests need no engine and no asset database.
    /// </summary>
    internal sealed class GullYaml
    {
        private static readonly Regex KeyLine = new Regex(@"^ *([A-Za-z_][A-Za-z0-9_]*):(?: (.*))?$");
        private static readonly Regex MapItem = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*:( |$)");
        private static readonly Regex Vector = new Regex(@"^\{x: ([^,]+), y: ([^}]+)\}$");

        public string Scalar;
        public Dictionary<string, GullYaml> Map;
        public List<GullYaml> Seq;

        public bool Has(string key) => Map != null && Map.ContainsKey(key);

        public GullYaml this[string key]
        {
            get
            {
                Assert.IsTrue(Has(key), "the YAML has no field " + key);
                return Map[key];
            }
        }

        public float AsFloat => float.Parse(Scalar, NumberStyles.Float, CultureInfo.InvariantCulture);
        public int AsInt => int.Parse(Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture);
        public bool AsBool => AsInt != 0;

        public Vector2 AsVector2
        {
            get
            {
                Match m = Vector.Match(Scalar ?? "");
                Assert.IsTrue(m.Success, "not a vector: " + Scalar);
                return new Vector2(float.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                                   float.Parse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
            }
        }

        public IReadOnlyList<GullYaml> Items
        {
            get
            {
                if (Seq != null) return Seq;
                Assert.IsTrue(Scalar == "" || Scalar == "[]", "not a list: " + Scalar);
                return new List<GullYaml>();
            }
        }

        public float[] AsFloats => Items.Select(i => i.AsFloat).ToArray();
        public string[] AsStrings => Items.Select(i => i.Scalar).ToArray();

        /// <summary>The first MonoBehaviour's fields in a .asset file.</summary>
        public static GullYaml ReadAsset(string path)
        {
            string[] lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
            int i = Array.FindIndex(lines, l => l.TrimEnd() == "MonoBehaviour:");
            Assert.That(i, Is.GreaterThanOrEqualTo(0), path + " holds no MonoBehaviour");
            i++;
            return ParseMap(lines, ref i, 2);
        }

        private static int Indent(string line)
        {
            int n = 0;
            while (n < line.Length && line[n] == ' ') n++;
            return n;
        }

        private static bool IsItem(string line, int indent)
            => line.Length > indent && line[indent] == '-' && (line.Length == indent + 1 || line[indent + 1] == ' ');

        private static bool Blank(string line) => line.Trim().Length == 0;

        private static GullYaml ParseMap(string[] lines, ref int i, int indent)
        {
            var node = new GullYaml { Map = new Dictionary<string, GullYaml>(StringComparer.Ordinal) };
            while (i < lines.Length)
            {
                string line = lines[i];
                if (Blank(line)) { i++; continue; }
                int at = Indent(line);
                if (at < indent || IsItem(line, at) || line.StartsWith("---", StringComparison.Ordinal)) break;
                Assert.That(at, Is.EqualTo(indent), "unexpected indent: " + line);
                Match m = KeyLine.Match(line);
                Assert.IsTrue(m.Success, "not a field: " + line);
                string key = m.Groups[1].Value;
                string value = m.Groups[2].Success ? m.Groups[2].Value.TrimEnd() : "";
                i++;

                if (value.Length > 0)
                {
                    // A long scalar wraps onto deeper lines.
                    var folded = new StringBuilder(value);
                    while (i < lines.Length && !Blank(lines[i]) && Indent(lines[i]) > indent && !IsItem(lines[i], Indent(lines[i])))
                        folded.Append(' ').Append(lines[i++].Trim());
                    node.Map[key] = new GullYaml { Scalar = folded.ToString() };
                }
                else if (i < lines.Length && !Blank(lines[i]) && Indent(lines[i]) >= indent && IsItem(lines[i], Indent(lines[i])))
                {
                    node.Map[key] = ParseSeq(lines, ref i, Indent(lines[i]));
                }
                else if (i < lines.Length && !Blank(lines[i]) && Indent(lines[i]) > indent)
                {
                    node.Map[key] = ParseMap(lines, ref i, Indent(lines[i]));
                }
                else
                {
                    node.Map[key] = new GullYaml { Scalar = "" };
                }
            }
            return node;
        }

        private static GullYaml ParseSeq(string[] lines, ref int i, int indent)
        {
            var node = new GullYaml { Seq = new List<GullYaml>() };
            while (i < lines.Length)
            {
                string line = lines[i];
                if (Blank(line)) { i++; continue; }
                if (Indent(line) != indent || !IsItem(line, indent)) break;
                string rest = line.Substring(indent + 1).Trim();
                if (MapItem.IsMatch(rest))
                {
                    // "- Key: value" opens a map whose keys sit two deeper than the dash.
                    lines[i] = new string(' ', indent + 2) + rest;
                    node.Seq.Add(ParseMap(lines, ref i, indent + 2));
                }
                else
                {
                    node.Seq.Add(new GullYaml { Scalar = rest });
                    i++;
                }
            }
            return node;
        }
    }

    /// <summary>One place, read from its Def file.</summary>
    internal sealed class GullPlaceFile
    {
        public string Path, Id, RegionId, RoostId;
        public BirdHauntKind Kind;
        public Vector2 Centre, HalfSize, AwayHeading;
        public bool Sheltered, BusyOnFineDaysOnly, HasTide;
        public float BusyFromHour, BusyToHour, BusyBoost, MarketDayBoost;
        public float TideOpensBelowMetres, TideRampMetres, HighWaterKeep, MiddayLevel, AfternoonLevel;
        public string[] PerchIds;
        public IReadOnlyList<GullYaml> Birds;

        public bool IsRoost => Kind == BirdHauntKind.Roost;

        public static GullPlaceFile Read(string path)
        {
            GullYaml d = GullYaml.ReadAsset(path);
            return new GullPlaceFile
            {
                Path = path,
                Id = d["Id"].Scalar,
                RegionId = d["RegionId"].Scalar,
                RoostId = d["RoostId"].Scalar,
                Kind = (BirdHauntKind)d["Kind"].AsInt,
                Centre = d["Centre"].AsVector2,
                HalfSize = d["HalfSize"].AsVector2,
                Sheltered = d["Sheltered"].AsBool,
                BusyFromHour = d["BusyFromHour"].AsFloat,
                BusyToHour = d["BusyToHour"].AsFloat,
                BusyBoost = d["BusyBoost"].AsFloat,
                BusyOnFineDaysOnly = d["BusyOnFineDaysOnly"].AsBool,
                MarketDayBoost = d["MarketDayBoost"].AsFloat,
                HasTide = d["HasTide"].AsBool,
                TideOpensBelowMetres = d["TideOpensBelowMetres"].AsFloat,
                TideRampMetres = d["TideRampMetres"].AsFloat,
                HighWaterKeep = d["HighWaterKeep"].AsFloat,
                MiddayLevel = d["MiddayLevel"].AsFloat,
                AfternoonLevel = d["AfternoonLevel"].AsFloat,
                PerchIds = d["PerchIds"].AsStrings,
                AwayHeading = d["AwayHeading"].AsVector2,
                Birds = d["Birds"].Items,
            };
        }

        /// <summary>The entry for one bird, as <see cref="BirdHauntDef.TryGetEntry"/> finds it: the first
        /// with that id. Null when the place has none.</summary>
        public GullYaml EntryFor(string speciesId)
            => Birds.FirstOrDefault(b => b.Has("SpeciesId") && string.Equals(b["SpeciesId"].Scalar, speciesId, StringComparison.Ordinal));

        /// <summary><see cref="BirdHauntDef.ToSpec"/>, line for line, on the file's numbers.</summary>
        public GullHauntSpec ToSpec(GullYaml entry, int roostIndex)
        {
            var spec = new GullHauntSpec
            {
                IdHash = GullHash.Id(Id),
                Kind = Kind,
                CentreX = Centre.x,
                CentreY = Centre.y,
                HalfX = Mathf.Abs(HalfSize.x),
                HalfY = Mathf.Abs(HalfSize.y),
                RoostIndex = IsRoost ? -1 : roostIndex,
                Sheltered = Sheltered,
                BusyFromHour = BusyFromHour,
                BusyToHour = BusyToHour,
                BusyBoost = BusyBoost,
                BusyOnFineDaysOnly = BusyOnFineDaysOnly,
                MarketDayBoost = MarketDayBoost,
                HasTide = HasTide,
                TideOpensBelowMetres = TideOpensBelowMetres,
                TideRampMetres = TideRampMetres,
                HighWaterKeep = HighWaterKeep,
                MiddayLevel = MiddayLevel,
                AfternoonLevel = AfternoonLevel,
            };

            float length = AwayHeading.magnitude;
            if (length > 0f)
            {
                spec.AwayX = AwayHeading.x / length;
                spec.AwayY = AwayHeading.y / length;
            }

            if (entry != null)
            {
                spec.Capacity = Mathf.Clamp(entry["Capacity"].AsInt, 0, GullRoster.MaxSlotsPerPlace);
                spec.Typical = Mathf.Max(0f, entry["Typical"].AsFloat);
                spec.SeasonWeights = Seasons(entry["SeasonWeights"].AsFloats);
                spec.Residents = Seasons(entry["Residents"].AsFloats);
                spec.Room = Mathf.Clamp(entry["Room"].AsInt, 0, GullRoster.MaxSlotsPerPlace);
            }
            return spec;
        }

        private static GullSeasons Seasons(float[] four)
            => new GullSeasons(At(four, 0), At(four, 1), At(four, 2), At(four, 3));

        private static double At(float[] values, int i) => i < values.Length ? values[i] : 0.0;
    }

    /// <summary>One region's places as the flock builds them: the library's order, the herring
    /// gull's entry, each haunt's roost by id, and every place's way out.</summary>
    internal sealed class GullRegionPlaces
    {
        public string RegionId;
        public GullPlaceFile[] Files;
        public GullRect Bounds;
        private GullHauntSpec[] _specs;

        public GullRegionPlaces(string regionId, GullPlaceFile[] files, GullHauntSpec[] specs, GullRect bounds)
        {
            RegionId = regionId;
            Files = files;
            _specs = specs;
            Bounds = bounds;
        }

        public int Count => Files.Length;

        /// <summary>A fresh copy each call: a test may change it.</summary>
        public GullHauntSpec[] Places => (GullHauntSpec[])_specs.Clone();

        public string IdOf(int i) => i >= 0 && i < Files.Length ? Files[i].Id : "(school)";
        public int IndexOf(string id) => Array.FindIndex(Files, f => f.Id == id);

        public int Index(string id)
        {
            int i = IndexOf(id);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), id + " is not in " + RegionId);
            return i;
        }
    }

    /// <summary>The places, the tuning, the clock and the coast the gull tests read.</summary>
    internal static class GullPlaceFiles
    {
        public const string LibraryPath = "Assets/_Project/Resources/BirdHauntLibrary.asset";
        public const string PlacesFolder = "Assets/_Project/Data/Birds/Haunts";
        public const string RegionsFolder = "Assets/_Project/Data/Regions";
        public const string SkyPath = "Assets/_Project/Resources/DayNightProfile.asset";

        public const string StPeters = "region.st_peters";
        public const string NineMileCreek = "region.nine_mile_creek";
        public static readonly string[] Regions = { StPeters, NineMileCreek };

        // The game's default clock: the flock reads the same numbers from GameServices.
        public const double SecondsPerHour = GameConfig.DefaultSecondsPerDay / DaySchedule.HoursPerDay;
        public const double SecondsPerDay = SecondsPerHour * DaySchedule.HoursPerDay;
        public const int DaysPerSeason = GameConfig.DefaultDaysPerSeason;
        public const int DaysPerWeek = (int)Weekday.Sunday + 1;

        /// <summary>The day of the season whose progress is one half.</summary>
        public const int HalfwayDayOfSeason = DaysPerSeason / 2 + 1;

        public const int Seed = 4242;
        public const double LowWater = -1.9;
        public const double HighWater = 1.9;

        /// <summary>A view on the cannery, and one along the shore far from it.</summary>
        public static readonly GullRect CanneryView = GullRect.Box(170.0, 16.0, 14.2, 8.0);
        public static readonly GullRect AwayView = GullRect.Box(-30.0, 16.0, 14.2, 8.0);

        public static double At(int day, double hour) => day * SecondsPerDay + hour * SecondsPerHour;

        public static GullScheduleInputs Inputs(int season, double hour, GullSky sky, double tide = LowWater,
                                                bool marketDay = false, double progress = 0.5, int day = 3, int seed = Seed)
            => new GullScheduleInputs
            {
                Seed = seed,
                Seconds = At(day, hour),
                Day = day,
                Hour = hour,
                Season = season,
                Progress = progress,
                MarketDay = marketDay,
                Storm = sky.Storm,
                Visibility = sky.Visibility,
                Tide = tide,
            };

        public static GullCalendar Calendar(int day, double hour, int season, double visibility = 1.0,
                                            int dayOfSeason = HalfwayDayOfSeason, int marketDayIndex = -1, int seed = Seed)
            => new GullCalendar
            {
                Seed = seed,
                Seconds = At(day, hour),
                SecondsPerHour = SecondsPerHour,
                Hour = hour,
                Day = day,
                Season = season,
                DayOfSeason = dayOfSeason,
                DaysPerSeason = DaysPerSeason,
                Weekday = GullSchedule.Mod(day, DaysPerWeek),
                DaysPerWeek = DaysPerWeek,
                MarketDayIndex = marketDayIndex,
                Visibility = visibility,
            };

        /// <summary>The calendar at any game time, as the clock would give it.</summary>
        public static GullCalendar CalendarAt(double seconds, int season, double visibility = 1.0)
        {
            int day = (int)Math.Floor(seconds / SecondsPerDay);
            GullCalendar cal = Calendar(day, 0.0, season, visibility);
            cal.Seconds = seconds;
            cal.Hour = (seconds - day * SecondsPerDay) / SecondsPerHour;
            return cal;
        }

        public static GullRosterSettings Settings(GullRect region)
        {
            GullConfig cfg = GullConfig.Default;
            return new GullRosterSettings
            {
                CommuteSpeedMetresPerSecond = cfg.CommuteSpeedMetresPerSecond,
                ShowMarginMetres = cfg.ShowMarginMetres,
                ShowHysteresisMetres = cfg.ShowHysteresisMetres,
                LiftMetres = 0.0,
                Region = region,
            };
        }

        /// <summary>A roster sized as the flock sizes it, refreshed once.</summary>
        public static GullRoster Refreshed(GullRegionPlaces region, in GullTuningSpec t, in GullCalendar cal,
                                           IGullWeather weather, in GullRect view)
        {
            GullHauntSpec[] places = region.Places;
            var roster = new GullRoster(places.Length, GullFlock.MaxNearBirds, GullConfig.Default.Count);
            roster.Refresh(places, places.Length, places.Length, t, Settings(region.Bounds), cal, weather, view);
            return roster;
        }

        /// <summary>Every slot of the first <paramref name="count"/> places.</summary>
        public static IEnumerable<(int Place, int Slot)> Slots(GullRoster roster, int count)
        {
            for (int p = 0; p < count; p++)
            {
                roster.SlotRange(p, out int first, out int end);
                for (int slot = first; slot < end; slot++) yield return (p, slot);
            }
        }

        public static string Describe(in GullRosterBird b)
            => b.Where + " at " + b.At + " (" + b.X.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
               b.Y.ToString("0.###", CultureInfo.InvariantCulture) + ")" +
               (b.Where == GullWhere.Commuting ? " from " + b.From + " leaving " + b.DepartSeconds : "");

        /// <summary>A surface school as the flock makes it a place (GullFlock.SchoolSpec).</summary>
        public static GullHauntSpec School(in GullTuningSpec t, double x, double y, double radius, double start, double end)
            => new GullHauntSpec
            {
                IdHash = GullHash.Id("school.test"),
                Kind = BirdHauntKind.Loafing,
                IsSchool = true,
                CentreX = x,
                CentreY = y,
                HalfX = radius,
                HalfY = radius,
                RoostIndex = -1,
                Capacity = Math.Max(0, Math.Min(GullRoster.MaxSchoolSlots, (int)Math.Ceiling(t.SchoolBirds * t.SchoolSeason.Max))),
                SchoolStartSeconds = start,
                SchoolEndSeconds = end,
                AwayX = 0.0,
                AwayY = 1.0,
            };

        /// <summary>
        /// One region's places, as <c>GullFlock.BuildRegion</c> makes them: the library's order, the
        /// first entry for the flock's bird, each haunt's roost found by id, the roosts' ways out
        /// first and then the haunts'.
        /// </summary>
        public static GullRegionPlaces Region(string regionId)
        {
            var byGuid = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string meta in Directory.GetFiles(PlacesFolder, "*.asset.meta", SearchOption.AllDirectories))
            {
                Match g = Regex.Match(File.ReadAllText(meta), @"^guid: ([0-9a-f]{32})", RegexOptions.Multiline);
                Assert.IsTrue(g.Success, "no guid in " + meta);
                byGuid[g.Groups[1].Value] = meta.Substring(0, meta.Length - ".meta".Length);
            }

            var files = new List<GullPlaceFile>();
            foreach (GullYaml item in GullYaml.ReadAsset(LibraryPath)["Haunts"].Items)
            {
                Match r = Regex.Match(item.Scalar ?? "", @"guid: ([0-9a-f]{32})");
                Assert.IsTrue(r.Success, "the library lists a place with no guid: " + item.Scalar);
                Assert.IsTrue(byGuid.TryGetValue(r.Groups[1].Value, out string path),
                              "the library lists " + r.Groups[1].Value + ", which is no place file");
                GullPlaceFile file = GullPlaceFile.Read(path);
                if (string.Equals(file.RegionId, regionId, StringComparison.Ordinal)) files.Add(file);
            }
            Assert.That(files.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(GullFlock.MaxAuthoredPlaces),
                        regionId + "'s places");

            int n = files.Count;
            string species = GullConfig.Default.SpeciesId;
            var specs = new GullHauntSpec[n];
            for (int i = 0; i < n; i++)
            {
                GullPlaceFile f = files[i];
                specs[i] = f.ToSpec(f.EntryFor(species), f.IsRoost ? -1 : RoostIndexOf(files, f.RoostId));
            }

            GullRect bounds = RegionBounds(regionId);
            for (int i = 0; i < n; i++)
                if (specs[i].IsRoost) WayOutFor(specs, ref specs[i], -1, bounds);
            for (int i = 0; i < n; i++)
                if (!specs[i].IsRoost) WayOutFor(specs, ref specs[i], specs[i].RoostIndex, bounds);

            return new GullRegionPlaces(regionId, files.ToArray(), specs, bounds);
        }

        private static int RoostIndexOf(List<GullPlaceFile> files, string roostId)
        {
            if (string.IsNullOrEmpty(roostId)) return -1;
            for (int i = 0; i < files.Count; i++)
                if (files[i].IsRoost && string.Equals(files[i].Id, roostId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static void WayOutFor(GullHauntSpec[] places, ref GullHauntSpec spec, int roost, in GullRect region)
        {
            if (spec.AwayX != 0.0 || spec.AwayY != 0.0) return;
            if (roost >= 0 && (places[roost].AwayX != 0.0 || places[roost].AwayY != 0.0))
            {
                spec.AwayX = places[roost].AwayX;
                spec.AwayY = places[roost].AwayY;
                return;
            }
            double dx = spec.CentreX - region.CentreX, dy = spec.CentreY - region.CentreY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (region.IsEmpty || !(length > 0.0))
            {
                spec.AwayX = 0.0;
                spec.AwayY = 1.0;
                return;
            }
            spec.AwayX = dx / length;
            spec.AwayY = dy / length;
        }

        /// <summary>A region's world rectangle, from its RegionDef as the anchor publishes it.</summary>
        public static GullRect RegionBounds(string regionId)
        {
            foreach (string path in Directory.GetFiles(RegionsFolder, "*.asset", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path).Replace("\r\n", "\n");
                if (!Regex.IsMatch(text, "^  Id: " + Regex.Escape(regionId) + "$", RegexOptions.Multiline)) continue;
                Vector2 centre = new GullYaml { Scalar = Field(text, "WorldCenter") }.AsVector2;
                Vector2 size = new GullYaml { Scalar = Field(text, "WorldSizeMeters") }.AsVector2;
                var rect = new Rect(centre - size * 0.5f, size);
                return new GullRect(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
            }
            Assert.Fail("no RegionDef with id " + regionId);
            return default;
        }

        /// <summary>
        /// The schedule's tuning as the flock reads it: the library's Tuning block (every field of
        /// <see cref="GullScheduleTuning"/> must be in it, and nothing else), with the sky profile's
        /// sunrise and sunset. <paramref name="jitter"/> false sets the day's jitter to none, the way
        /// the owner's table is read.
        /// </summary>
        public static GullTuningSpec Tuning(bool jitter = true)
        {
            var knobs = new GullScheduleTuning();
            GullYaml block = GullYaml.ReadAsset(LibraryPath)["Tuning"];
            FieldInfo[] fields = typeof(GullScheduleTuning).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo f in fields)
                Assert.IsTrue(block.Has(f.Name), "the library's Tuning block has no " + f.Name);
            foreach (KeyValuePair<string, GullYaml> kv in block.Map)
            {
                FieldInfo f = typeof(GullScheduleTuning).GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, "the library's Tuning block has a field the code does not: " + kv.Key);
                if (f.FieldType == typeof(float)) f.SetValue(knobs, kv.Value.AsFloat);
                else if (f.FieldType == typeof(float[])) f.SetValue(knobs, kv.Value.AsFloats);
                else Assert.Fail("a tuning field of an unread type: " + f.Name);
            }
            if (!jitter) knobs.DayJitter = 0f;

            string sky = File.ReadAllText(SkyPath).Replace("\r\n", "\n");
            return knobs.ToSpec(Hour(sky, "_sunriseHour"), Hour(sky, "_sunsetHour"));
        }

        private static double Hour(string sky, string field)
            => float.Parse(Field(sky, field), NumberStyles.Float, CultureInfo.InvariantCulture);

        private static string Field(string text, string name)
        {
            Match m = Regex.Match(text, "^  " + Regex.Escape(name) + ": (.+)$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "no " + name);
            return m.Groups[1].Value.Trim();
        }
    }
}
