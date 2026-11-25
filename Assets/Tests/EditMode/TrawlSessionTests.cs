using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class TrawlSessionTests
    {
        private sealed class Fixture
        {
            internal TrawlSessionDef Def = new TrawlSessionDef
            {
                Id = "trawl_session.test", DraughtMetres = 2d, MinKeelClearanceMetres = 1d,
                WarpDepthRatio = 3d, DoorSpreadMetres = 4d, SnagHazardPerHardMetre = 0d,
                ShallowChafePerMetre = 0.1d, CarryGameSecondsPerIndividual = 1d,
                InitialIntegrity01 = 1d, GradeWeightBreaksKg = new[] { 1d, 2d }
            };
            internal TrawlGearDef Gear = TrawlTestData.Gear;
            internal TrawlSessionGround Ground;
            internal TrawlTestData.Survey Survey;
            internal TrawlSession Session;
            internal double Start;

            internal Fixture(string asset = "SouthWaterShelf")
            {
                Ground = ReadGround(asset);
                var populations = new TrawlPopulationSample[Ground.SpeciesCount];
                for (int i = 0; i < populations.Length; i++)
                    populations[i] = TrawlTestData.Population(Ground.SpeciesAt(i), (uint)(i + 1), 12,
                        Ground.Center.x + 10f, Ground.Center.y, (float)(Ground.MinDepthMetres - 1d),
                        minWeight: 2d, maxWeight: 2d);
                Survey = new TrawlTestData.Survey
                {
                    Ground = new TrawlGroundSample(Ground.Id, Ground.MinDepthMetres, Ground.Hardness01, Season.EarlySpring),
                    Populations = populations
                };
            }

            internal void Build(int seed = 7, System.Random rng = null)
                => Session = new TrawlSession(Def, Gear, TrawlTestData.Stern, Ground, Survey,
                    rng ?? new System.Random(seed), "tow.session_test", new[] { Ground.SpeciesAt(0) });

            internal TrawlSessionInput Input(double time, double x = 0d, double speed = 2d, double? keel = null,
                double sea = 0d) => new TrawlSessionInput(Start + time, speed,
                    keel ?? Ground.MinDepthMetres - Def.DraughtMetres, sea,
                    new Vector2((float)(Ground.Center.x + x), Ground.Center.y));

            internal void Shoot()
            {
                Assert.IsTrue(Session.TryShoot(Input(0d), TrawlWorkStation.Astern));
                Assert.IsTrue(Session.TryFinishShooting(Input(4d)));
            }
            internal void Haul()
            {
                Assert.IsTrue(Session.Tick(Input(14d, 20d)));
                Assert.IsTrue(Session.TryBeginHauling(Input(14d, 20d, 0d), TrawlWorkStation.Astern));
                Assert.IsTrue(Session.TryFinishHauling(Start + 20d));
            }
            internal double SortAll(TrawlSortDestination destination = TrawlSortDestination.KeepOnDeck)
            {
                double time = Start + 20d;
                for (int i = 0; i < Session.LotCount; i++)
                {
                    int count = Session.LotAt(i).Catch.Count;
                    time += count / TrawlTestData.Stern.SortIndividualsPerGameSecond;
                    Assert.IsTrue(Session.SortByHand(time, i, count, destination));
                }
                return time;
            }
        }

        // Read the actual three authored data files. Do not duplicate their ids, bottoms,
        // species manifests or geometry in a second production table or a synthetic fixture.
        private static TrawlSessionGround ReadGround(string asset)
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            string relative = "Assets/_Project/Data/FishingGrounds/" + asset + ".asset";
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, relative))) directory = directory.Parent;
            Assert.IsNotNull(directory, "Repository ground asset must be present.");
            string yaml = File.ReadAllText(Path.Combine(directory.FullName, relative));
            string Field(string name) => Regex.Match(yaml, @"(?m)^  " + name + @": (.+)\r?$").Groups[1].Value.Trim();
            double Number(string name) => double.Parse(Field(name), CultureInfo.InvariantCulture);
            Vector2 Point(string name)
            {
                var values = Regex.Match(Field(name), @"\{x: ([^,]+), y: ([^}]+)\}");
                return new Vector2(float.Parse(values.Groups[1].Value, CultureInfo.InvariantCulture),
                    float.Parse(values.Groups[2].Value, CultureInfo.InvariantCulture));
            }
            var matches = Regex.Matches(yaml, @"(?m)^  - (fish\.[a-z0-9_]+)\r?$");
            var fishIds = new string[matches.Count];
            for (int i = 0; i < fishIds.Length; i++) fishIds[i] = matches[i].Groups[1].Value;
            return new TrawlSessionGround(Field("Id"), Field("RegionId"), Point("Center"), Point("HalfSize"),
                Number("MinDepthMetres"), Number("MaxDepthMetres"), (EcologyHabitat)(int)Number("Bottom"),
                Number("Hardness01"), fishIds);
        }

        [TestCase("SouthWaterShelf", 3, 36)]
        [TestCase("SouthWaterDeepMud", 2, 24)]
        [TestCase("SouthWaterBank", 2, 24)]
        public void TowOnEachSouthWaterGround_UsesAuthoredShapeAndFish(string asset, int species, int catchCount)
        {
            var f = new Fixture(asset); f.Build();
            Assert.AreEqual("region.south_water", f.Ground.RegionId);
            Assert.AreEqual(species, f.Ground.SpeciesCount);
            Assert.AreEqual(TrawlSessionPhase.GearReady, f.Session.Phase);
            Assert.AreEqual(TrawlCrewTask.ShootGear, f.Session.CrewTask);
            f.Shoot();
            Assert.AreEqual(f.Ground.MinDepthMetres * 3d, f.Session.WarpOutMetres);
            Assert.AreEqual(TrawlDoorState.Spread, f.Session.Doors);
            Assert.AreEqual(TrawlCrewTask.TendTow, f.Session.CrewTask);
            f.Haul();
            Assert.AreEqual(TrawlTowStatus.Complete, f.Session.LastSolveStatus);
            Assert.AreEqual(TrawlSessionPhase.CatchOnDeck, f.Session.Phase);
            Assert.AreEqual(catchCount, f.Session.CatchCount);
            Assert.AreEqual(catchCount, f.Session.UnsortedCount);
            Assert.AreEqual(catchCount - 12, f.Session.BycatchCount);
            Assert.AreEqual(0, f.Session.StowedCount);
            Assert.AreEqual(10d, f.Session.TowGameSeconds);
            for (int i = 0; i < f.Session.LotCount; i++)
            {
                Assert.IsTrue(f.Ground.Holds(f.Session.LotAt(i).Catch.SpeciesId));
                Assert.AreEqual(12, f.Session.LotAt(i).Catch.Count);
                Assert.AreEqual(2, f.Session.LotAt(i).Grade, "A weight exactly on a grade boundary reaches that grade.");
            }
            double time = f.SortAll();
            Assert.AreEqual(TrawlSessionPhase.Sorted, f.Session.Phase);
            for (int i = 0; i < f.Session.LotCount; i++)
            {
                int count = f.Session.LotAt(i).Catch.Count; time += count;
                Assert.IsTrue(f.Session.ConfirmHandStow(time, i, count, count * 2d));
            }
            Assert.AreEqual(TrawlSessionPhase.Stowed, f.Session.Phase);
            Assert.AreEqual(TrawlCrewTask.Complete, f.Session.CrewTask);
            Assert.AreEqual(catchCount, f.Session.StowedCount);
            Assert.AreEqual(0, f.Session.KeptOnDeckCount);
        }

        [Test]
        public void Determinism_FromExplicitSeedAndInputs_AtLongGameTimes()
        {
            bool sawDifferentSeed = false; string first = null;
            for (int seed = 0; seed < 256; seed++)
            {
                string[] receipts = new string[2];
                for (int replay = 0; replay < receipts.Length; replay++)
                {
                    var f = new Fixture { Start = 1000000000d }; f.Gear.CaptureFraction01 = 0.17d;
                    for (int i = 0; i < f.Survey.Populations.Length; i++)
                        f.Survey.Populations[i] = TrawlTestData.Population(f.Ground.SpeciesAt(i), (uint)(i + 1),
                            12, f.Ground.Center.x + 10f, f.Ground.Center.y, 19f, minWeight: 1d, maxWeight: 3d);
                    f.Build(seed); f.Shoot(); f.Haul();
                    string receipt = f.Session.CatchCount + ":" + f.Session.BycatchCount + ":" + f.Session.EndIntegrity01;
                    for (int i = 0; i < f.Session.LotCount; i++)
                    {
                        var lot = f.Session.LotAt(i);
                        receipt += "|" + lot.Catch.SpeciesId + ":" + lot.Catch.SchoolKey + ":" + lot.Catch.Count
                            + ":" + lot.Catch.UnitWeightKg.ToString("R", CultureInfo.InvariantCulture) + ":" + lot.Grade;
                    }
                    receipts[replay] = receipt;
                }
                Assert.AreEqual(receipts[0], receipts[1], "Replay seed " + seed);
                if (first == null) first = receipts[0];
                else if (receipts[0] != first) sawDifferentSeed = true;
            }
            Assert.IsTrue(sawDifferentSeed, "Explicit RNG must influence the reused solver's catch stream.");
        }

        [TestCase(0d)]
        [TestCase(0.99d)]
        [TestCase(7.99d)]
        public void TooShallow_RefusesToShootWithoutMutation(double keel)
        {
            var f = new Fixture(); f.Build();
            Assert.IsFalse(f.Session.TryShoot(f.Input(0d, keel: keel), TrawlWorkStation.Astern));
            Assert.AreEqual(TrawlSessionPhase.GearReady, f.Session.Phase);
            Assert.AreEqual(0d, f.Session.WarpOutMetres);
            Assert.AreEqual(TrawlDoorState.Closed, f.Session.Doors);
            Assert.AreEqual(0, f.Session.CatchCount);
        }

        [Test]
        public void FullHold_StopsStowAndPreservesCatchOnDeck()
        {
            var f = new Fixture(); f.Build(); f.Shoot(); f.Haul(); double time = f.SortAll();
            Assert.IsFalse(f.Session.ConfirmHandStow(time + 12d, 0, 12, 0d));
            Assert.IsFalse(f.Session.ConfirmHandStow(time + 12d, 0, 12, 23.99d));
            Assert.AreEqual(TrawlSessionPhase.Sorted, f.Session.Phase);
            Assert.AreEqual(36, f.Session.KeptOnDeckCount);
            Assert.AreEqual(0, f.Session.StowedCount);
            Assert.AreEqual(TrawlCrewTask.MakeHoldRoom, f.Session.TaskWithHoldRoom(0d));
            Assert.AreEqual(TrawlCrewTask.CarryToHold, f.Session.TaskWithHoldRoom(2d));
            Assert.IsFalse(f.Session.ConfirmHandStow(time + 11.99d, 0, 12, 24d), "Carrying still takes work time.");
            Assert.IsTrue(f.Session.ConfirmHandStow(time + 12d, 0, 12, 24d));
            Assert.AreEqual(12, f.Session.StowedCount);
            Assert.AreEqual(24, f.Session.KeptOnDeckCount);
            Assert.IsFalse(f.Session.ConfirmHandStow(time + 24d, 0, 1, 2d), "A placement cannot be acknowledged twice.");
        }

        [Test]
        public void HardBottomSnag_StopsTheTrackAndRequestsRecovery()
        {
            var f = new Fixture("SouthWaterBank"); f.Def.SnagHazardPerHardMetre = 100d; f.Build(); f.Shoot();
            Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d)));
            Assert.IsTrue(f.Session.Snagged);
            Assert.AreEqual(TrawlDoorState.Snagged, f.Session.Doors);
            Assert.AreEqual(TrawlCrewTask.RecoverSnag, f.Session.CrewTask);
            Assert.Greater(f.Session.SnagRisk01, 0d);
            Assert.Greater(f.Session.ChafeRisk01, 0d);
            Assert.Less(f.Session.TowGameSeconds, 10d);
            Assert.IsFalse(f.Session.Tick(f.Input(15d, 22d)));
            Assert.IsTrue(f.Session.TryBeginHauling(f.Input(14d, 20d, 0d), TrawlWorkStation.Astern));
            Assert.AreEqual(0, f.Session.CatchCount, "The schools beyond the snag cannot enter the codend.");
        }

        private sealed class ZeroDrawRandom : System.Random
        {
            public override double NextDouble() => 0d;
        }

        [Test]
        public void ImmediateSnag_HaulsAnEmptyCodendWithoutInventingTowTime()
        {
            var f = new Fixture("SouthWaterBank"); f.Def.SnagHazardPerHardMetre = 100d;
            f.Build(rng: new ZeroDrawRandom()); f.Shoot();
            Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d)));
            Assert.IsTrue(f.Session.Snagged); Assert.AreEqual(0d, f.Session.TowGameSeconds);
            Assert.IsTrue(f.Session.TryBeginHauling(f.Input(14d, 20d, 0d), TrawlWorkStation.Astern));
            Assert.AreEqual(0, f.Session.CatchCount);
        }

        [Test]
        public void SoftBottom_HasNoHardBottomSnagEvenWithHighHazardTuning()
        {
            var f = new Fixture("SouthWaterDeepMud"); f.Def.SnagHazardPerHardMetre = 100d; f.Build(); f.Shoot();
            Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d)));
            Assert.IsFalse(f.Session.Snagged); Assert.AreEqual(0d, f.Session.SnagRisk01);
            Assert.AreEqual(0d, f.Session.ChafeRisk01);
        }

        [Test]
        public void SnagReplay_IsStableAcrossEquivalentStraightTrackPartitions()
        {
            double[] endTimes = new double[2];
            double[] risks = new double[2];
            for (int partition = 0; partition < 2; partition++)
            {
                var f = new Fixture("SouthWaterBank"); f.Def.SnagHazardPerHardMetre = 1d; f.Build(); f.Shoot();
                if (partition == 0) Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d)));
                else
                    for (int i = 1; i <= 20 && !f.Session.Snagged; i++)
                        Assert.IsTrue(f.Session.Tick(f.Input(4d + i * 0.5d, i)));
                Assert.IsTrue(f.Session.Snagged);
                endTimes[partition] = f.Session.TowGameSeconds; risks[partition] = f.Session.SnagRisk01;
            }
            Assert.AreEqual(endTimes[0], endTimes[1], 1e-12d);
            Assert.AreEqual(risks[0], risks[1], 1e-12d);
        }

        [Test]
        public void TooShallowForPaidOutWarp_ClosesDoorsAndRaisesChafeWarning()
        {
            var f = new Fixture(); f.Build(); f.Shoot();
            Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d, keel: 8d)));
            Assert.IsTrue(f.Session.UnsafeWarp);
            Assert.AreEqual(TrawlDoorState.Collapsed, f.Session.Doors);
            Assert.AreEqual(TrawlCrewTask.HaulGear, f.Session.CrewTask);
            Assert.AreEqual(1d, f.Session.ChafeRisk01);
            Assert.AreEqual(60d, f.Session.WarpOutMetres, "A warning cannot silently reel gear in.");
        }

        [TestCase(0d)]
        [TestCase(3.01d)]
        public void SpeedOutsideTowWindow_CollapsesDoors(double speed)
        {
            var f = new Fixture(); f.Build(); f.Shoot();
            Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d, speed)));
            Assert.AreEqual(TrawlDoorState.Collapsed, f.Session.Doors);
            Assert.AreEqual(0d, f.Session.DoorSpreadMetres);
            Assert.IsTrue(f.Session.TryBeginHauling(f.Input(14d, 20d, 0d), TrawlWorkStation.Astern));
            Assert.AreEqual(0, f.Session.CatchCount);
        }

        [Test]
        public void TowingTick_AllocatesZeroBytes_WithPositiveCounterControl()
        {
            long beforeProbe = GC.GetAllocatedBytesForCurrentThread();
            var retained = new byte[1024]; GC.KeepAlive(retained);
            long positive = GC.GetAllocatedBytesForCurrentThread() - beforeProbe;
            if (positive == 0) Assert.Ignore("Runtime byte counter is stubbed; zero is not allocation proof.");
            Assert.Greater(positive, 0L);
            var f = new Fixture(); f.Gear.MaxTrackKnots = 8192; f.Gear.MaxTowGameSeconds = 10000d;
            f.Gear.MaxSamples = 5000; f.Build(); f.Shoot();
            for (int i = 1; i <= 32; i++) Assert.IsTrue(f.Session.Tick(f.Input(4d + i * 0.001d, i * 0.002d)));
            int accepted = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 33; i <= 4128; i++)
                if (f.Session.Tick(f.Input(4d + i * 0.001d, i * 0.002d))) accepted++;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(4096, accepted, "Every measured call must do towing work.");
            Assert.AreEqual(0L, bytes);
            Assert.AreEqual(TrawlSessionPhase.Towing, f.Session.Phase);
        }

        [Test]
        public void ShootingAndHauling_ExposeWarpProgressAndEnforceWorkTimes()
        {
            var f = new Fixture(); f.Build();
            Assert.IsFalse(f.Session.TryShoot(f.Input(0d), TrawlWorkStation.Port));
            Assert.IsTrue(f.Session.TryShoot(f.Input(0d), TrawlWorkStation.Astern));
            Assert.AreEqual(TrawlCrewTask.PayOutWarp, f.Session.CrewTask);
            Assert.IsTrue(f.Session.TickShooting(f.Input(2d))); Assert.AreEqual(30d, f.Session.WarpOutMetres);
            Assert.IsFalse(f.Session.TryFinishShooting(f.Input(3d)));
            Assert.IsFalse(f.Session.TryFinishShooting(f.Input(5d)), "Supply the canonical boundary snapshot.");
            Assert.IsTrue(f.Session.TryFinishShooting(f.Input(4d)));
            Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d)));
            Assert.IsFalse(f.Session.TryBeginHauling(f.Input(14d, 20d, 1.01d), TrawlWorkStation.Astern));
            Assert.IsTrue(f.Session.TryBeginHauling(f.Input(14d, 20d, 0d), TrawlWorkStation.Astern));
            Assert.IsTrue(f.Session.TickHauling(17d)); Assert.AreEqual(30d, f.Session.WarpOutMetres);
            Assert.IsFalse(f.Session.TryFinishHauling(19d));
            Assert.IsTrue(f.Session.TryFinishHauling(20d)); Assert.AreEqual(0d, f.Session.WarpOutMetres);
        }

        [Test]
        public void ManualSortingAndRelease_ConserveAllCatchWithoutAutomaticStow()
        {
            var f = new Fixture(); f.Build(); f.Shoot(); f.Haul();
            Assert.IsFalse(f.Session.ConfirmHandStow(100d, 0, 1, 100d));
            Assert.IsFalse(f.Session.SortByHand(20.49d, 0, 1, TrawlSortDestination.Release));
            double time = f.SortAll(TrawlSortDestination.Release);
            Assert.AreEqual(36, f.Session.ReleasedCount); Assert.AreEqual(0, f.Session.StowedCount);
            Assert.AreEqual(0, f.Session.UnsortedCount); Assert.AreEqual(TrawlSessionPhase.Sorted, f.Session.Phase);
            Assert.IsFalse(f.Session.SortByHand(time + 1d, 0, 1, TrawlSortDestination.KeepOnDeck));
            Assert.IsTrue(f.Session.ConfirmEmptyDeck()); Assert.AreEqual(TrawlSessionPhase.Stowed, f.Session.Phase);
        }

        [Test]
        public void EmptyKnownGround_StillRequiresSortingAcknowledgement()
        {
            var f = new Fixture(); f.Survey.Populations = new TrawlPopulationSample[0];
            f.Build(); f.Shoot(); f.Haul();
            Assert.AreEqual(0, f.Session.CatchCount); Assert.AreEqual(TrawlSessionPhase.CatchOnDeck, f.Session.Phase);
            Assert.IsFalse(f.Session.ConfirmEmptyDeck());
            Assert.IsTrue(f.Session.TryFinishSorting()); Assert.AreEqual(TrawlSessionPhase.Sorted, f.Session.Phase);
            Assert.IsTrue(f.Session.ConfirmEmptyDeck());
        }

        [Test]
        public void WrongGroundOrUnlistedSpecies_FailsClosedWithoutPublishingCatch()
        {
            for (int invalid = 0; invalid < 2; invalid++)
            {
                var f = new Fixture();
                if (invalid == 0) f.Survey.Ground = new TrawlGroundSample("ground.other", 20d, f.Ground.Hardness01, Season.EarlySpring);
                else f.Survey.Populations = new[] { TrawlTestData.Population("fish.not_in_ground") };
                f.Build(); f.Shoot(); Assert.IsTrue(f.Session.Tick(f.Input(14d, 20d)));
                Assert.IsFalse(f.Session.TryBeginHauling(f.Input(14d, 20d, 0d), TrawlWorkStation.Astern));
                Assert.AreEqual(TrawlTowStatus.UnknownGround, f.Session.LastSolveStatus);
                Assert.AreEqual(TrawlSessionPhase.Towing, f.Session.Phase); Assert.AreEqual(0, f.Session.CatchCount);
            }
        }

        [Test]
        public void InvalidTelemetryAndBoundaryCrossing_DoNotAdvanceOrConsumeBudget()
        {
            var f = new Fixture(); f.Build(); f.Shoot();
            Assert.IsFalse(f.Session.Tick(f.Input(3d, 2d)));
            Assert.IsFalse(f.Session.Tick(f.Input(5d, 2d, double.NaN)));
            Assert.IsFalse(f.Session.Tick(f.Input(5d, 2d, sea: 0.51d)));
            Assert.IsFalse(f.Session.Tick(f.Input(5d, f.Ground.HalfSize.x)));
            Assert.AreEqual(0d, f.Session.TowGameSeconds); Assert.AreEqual(0d, f.Session.ChafeRisk01);
            Assert.IsTrue(f.Session.Tick(f.Input(5d, 2d)));
            Assert.AreEqual(1d, f.Session.TowGameSeconds);
        }

        [Test]
        public void TrackAndTowBudgets_RefuseWithoutExtendingHistory()
        {
            var f = new Fixture(); f.Gear.MaxTrackKnots = 2; f.Build(); f.Shoot();
            Assert.IsTrue(f.Session.Tick(f.Input(5d, 2d)));
            Assert.AreEqual(TrawlCrewTask.HaulGear, f.Session.CrewTask);
            Assert.IsFalse(f.Session.Tick(f.Input(6d, 4d))); Assert.AreEqual(1d, f.Session.TowGameSeconds);
            var duration = new Fixture(); duration.Build(); duration.Shoot();
            Assert.IsFalse(duration.Session.Tick(duration.Input(104.01d, 20d)));
            Assert.AreEqual(0d, duration.Session.TowGameSeconds);
        }

        [Test]
        public void ConfigurationIsValidatedAndCopiedBeforeWork()
        {
            var f = new Fixture(); f.Def.GradeWeightBreaksKg = new[] { 2d, 1d };
            Assert.Throws<ArgumentException>(() => f.Build());
            f.Def.GradeWeightBreaksKg = new[] { 1d, 2d }; f.Build();
            f.Def.GradeWeightBreaksKg[0] = 100d; f.Shoot(); f.Haul();
            for (int i = 0; i < f.Session.LotCount; i++) Assert.AreEqual(2, f.Session.LotAt(i).Grade);
            string[] fish = { "fish.test" };
            var ground = new TrawlSessionGround("ground.test", "region.test", Vector2.zero, new Vector2(10f, 10f),
                10d, 10d, EcologyHabitat.Sand, 0d, fish);
            fish[0] = "fish.changed"; Assert.IsTrue(ground.Holds("fish.test")); Assert.IsFalse(ground.Holds("fish.changed"));
        }
    }
}
