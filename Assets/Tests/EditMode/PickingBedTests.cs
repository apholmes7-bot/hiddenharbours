using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class PickingBedTests
    {
        private const string BedRoot = "Assets/_Project/Data/PickingBeds/";
        private PickingBedDef _bed;
        private FishSpeciesDef _species;
        private GameConfig _config;
        private TideProbe _tide;
        private Hands _hands;
        private Licences _licences;
        private PickingBedSession _session;

        private sealed class Licences : ILicenseService
        {
            public bool Held = true;
            public Action OnCheck;
            public bool IsLicensed(string id) { OnCheck?.Invoke(); return Held; }
            public void Grant(string id) { Held = true; }
            public int Count => Held ? 1 : 0;
        }

        private sealed class Hands : ICatchHands
        {
            public readonly List<CatchItem> Accepted = new();
            public bool Full;
            public Action OnPut;
            public bool TryPutInHand(in CatchItem item)
            {
                if (Full) return false;
                OnPut?.Invoke();
                Accepted.Add(item);
                EventBus.Publish(new CatchLanded(item));
                return true;
            }
            public bool TryTakeFromHand(out CatchItem item) { item = default; return false; }
            public bool TryPeekHand(out CatchItem item) { item = default; return false; }
        }

        private sealed class TideProbe : IEnvironmentService
        {
            public int WorldSeed { get; set; } = 1729;
            public TideProfile ActiveTideProfile { get; set; }
            public GameConfig Config;
            public float Level;
            public float WaterOffset;
            public bool RealTide;
            public double LastRead;
            public Action OnRead;
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double time) => RealTide
                ? TideModel.Height(time, ActiveTideProfile, Config) : Level;
            public float WaterLevelAt(double time)
            {
                LastRead = time;
                OnRead?.Invoke();
                return TideHeightAt(time) + WaterOffset;
            }
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            UnlicensedCatch.ResetCatalog();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<FishCaught>();
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(_config);
            var source = AssetDatabase.LoadAssetAtPath<PickingBedDef>(BedRoot + "StPetersMussels.asset");
            Assert.IsNotNull(source);
            _bed = Object.Instantiate(source);
            _species = Object.Instantiate(source.Species);
            _bed.Species = _species;
            _hands = new Hands();
            _tide = new TideProbe { Config = _config, Level = _bed.UncoveredBelowMetres - 1f };
            _licences = new Licences();
            _session = NewSession();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            UnlicensedCatch.ResetCatalog();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<FishCaught>();
            Object.DestroyImmediate(_bed);
            Object.DestroyImmediate(_species);
        }

        private PickingBedSession NewSession() => new PickingBedSession(_bed, _tide, _config.SecondsPerDay);
        private PickingBedCheck Pick(double time = 0d) => _session.TryPick(_bed.RegionId, _bed.Position, time, _hands, _licences);
        private double RegrowSeconds => (double)_bed.RegrowDays * _config.SecondsPerDay;

        [Test]
        public void OnePick_PutsOneFreshShellfishInHand_AndDoesNotClaimAContainerCatch()
        {
            int landed = 0, stowed = 0;
            EventBus.Subscribe<CatchLanded>(_ => landed++);
            EventBus.Subscribe<FishCaught>(_ => stowed++);
            PickingBedCheck result = Pick(17d);
            Assert.IsTrue(result.Kept);
            Assert.AreEqual(_bed.Capacity - 1, result.Remaining);
            Assert.AreEqual(1, _hands.Accepted.Count);
            Assert.AreEqual(1, landed, "the hands publish the event exactly once");
            Assert.AreEqual(0, stowed);
            CatchItem item = _hands.Accepted[0];
            Assert.AreEqual(_species.Id, item.SpeciesId);
            Assert.AreEqual(_species.DisplayName, item.DisplayName);
            Assert.AreEqual(_species.BaseValue, item.BaseValue);
            Assert.AreEqual(_species.SupplyElasticity, item.SupplyElasticity);
            Assert.AreEqual(_species.SpoilPerDay, item.SpoilPerDay);
            Assert.AreEqual(17d, item.Freshness.LastSettleGameSeconds);
            Assert.AreEqual(0f, item.Freshness.SpoilAccrued);
            Assert.That(item.WeightKg, Is.InRange(_species.MinWeightKg, _species.MaxWeightKg));
        }

        [TestCase("StPetersMussels")]
        [TestCase("StPetersOysters")]
        public void RealTide_OpensAndCoversEachBedAcrossSevenDays(string file)
        {
            var bed = AssetDatabase.LoadAssetAtPath<PickingBedDef>(BedRoot + file + ".asset");
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");
            Assert.IsNotNull(bed);
            Assert.IsNotNull(region);
            _tide.RealTide = true;
            _tide.ActiveTideProfile = new TideProfile
            {
                MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours,
            };
            var session = new PickingBedSession(bed, _tide, _config.SecondsPerDay);
            const int days = 7, samplesPerDay = 288;
            for (int day = 0; day < days; day++)
            {
                int open = 0, covered = 0;
                for (int sample = 0; sample < samplesPerDay; sample++)
                {
                    double time = (day + sample / (double)samplesPerDay) * _config.SecondsPerDay;
                    bool uncovered = TideModel.Height(time, _tide.ActiveTideProfile, _config) < bed.UncoveredBelowMetres;
                    PickingBedCheck check = session.Check(bed.RegionId, bed.Position, time, _licences);
                    Assert.AreEqual(uncovered ? PickingBedStatus.Ready : PickingBedStatus.Covered, check.Status,
                        file + " day " + day + " sample " + sample);
                    Assert.AreEqual(time, _tide.LastRead);
                    Assert.AreEqual(bed.Capacity, check.Remaining, "tides alone do not take stock");
                    if (uncovered) open++; else covered++;
                }
                Assert.Greater(open, 0, file + " needs a low-water window on day " + day);
                Assert.Greater(covered, 0, file + " must flood on day " + day);
                Assert.AreEqual(samplesPerDay, open + covered);
            }
        }

        [Test]
        public void WaterLevelAccessorWins_AndEqualityIsStillCovered()
        {
            _tide.Level = _bed.UncoveredBelowMetres - 1f;
            _tide.WaterOffset = 2f;
            Assert.AreEqual(PickingBedStatus.Covered, Pick().Status, "raw tide is lower than local water");
            _tide.WaterOffset = 0f;
            _tide.Level = _bed.UncoveredBelowMetres;
            Assert.AreEqual(PickingBedStatus.Covered, Pick().Status);
            _tide.Level -= 0.001f;
            Assert.AreEqual(PickingBedStatus.Picked, Pick().Status);
            Assert.AreEqual(1, _hands.Accepted.Count);
        }

        [Test]
        public void CapacityPicks_RunItOut_AndAnExtraPressHasNoEffect()
        {
            for (int i = 0; i < _bed.Capacity; i++)
            {
                PickingBedCheck result = Pick();
                Assert.AreEqual(PickingBedStatus.Picked, result.Status);
                Assert.AreEqual(_bed.Capacity - i - 1, result.Remaining);
            }
            Assert.AreEqual(PickingBedStatus.Empty, Pick().Status);
            Assert.AreEqual(_bed.Capacity, _hands.Accepted.Count);
            Assert.AreEqual(_bed.Capacity, _session.SnapshotAt(0d).TakenAtSeconds.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TwoPickersPreviewTheLastShellfish_OnlyTheFirstAttemptGetsIt(bool reverseOrder)
        {
            for (int i = 0; i < _bed.Capacity - 1; i++) Assert.IsTrue(Pick().Kept);
            var hands = new[] { new Hands(), new Hands() };
            var wallets = new[] { new Licences(), new Licences() };
            for (int i = 0; i < hands.Length; i++)
            {
                PickingBedCheck preview = _session.Check(_bed.RegionId, _bed.Position, 10d, wallets[i]);
                Assert.IsTrue(preview.CanKeep);
                Assert.AreEqual(1, preview.Remaining);
            }

            int first = reverseOrder ? 1 : 0;
            int second = 1 - first;
            int landed = 0, stowed = 0;
            EventBus.Subscribe<CatchLanded>(_ => landed++);
            EventBus.Subscribe<FishCaught>(_ => stowed++);
            PickingBedCheck winner = _session.TryPick(_bed.RegionId, _bed.Position, 10d, hands[first], wallets[first]);
            PickingBedCheck loser = _session.TryPick(_bed.RegionId, _bed.Position, 10d, hands[second], wallets[second]);

            Assert.AreEqual(PickingBedStatus.Picked, winner.Status);
            Assert.AreEqual(PickingBedStatus.Empty, loser.Status);
            Assert.AreEqual(0, winner.Remaining);
            Assert.AreEqual(0, loser.Remaining);
            Assert.AreEqual(1, hands[first].Accepted.Count);
            Assert.AreEqual(0, hands[second].Accepted.Count);
            Assert.AreEqual(_species.Id, hands[first].Accepted[0].SpeciesId);
            Assert.AreEqual(1, landed);
            Assert.AreEqual(0, stowed);
            Assert.AreEqual(_bed.Capacity, _session.SnapshotAt(10d).TakenAtSeconds.Count);
            Assert.AreEqual(0, _session.RemainingAt(10d));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryPickerUsesOneBedCount_AndRegrowthCanGoToADifferentPicker(int playerCount)
        {
            _bed.Capacity = playerCount;
            _session = NewSession();
            var hands = new Hands[playerCount];
            var wallets = new Licences[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                hands[i] = new Hands();
                wallets[i] = new Licences();
                PickingBedCheck result = _session.TryPick(_bed.RegionId, _bed.Position, i, hands[i], wallets[i]);
                Assert.AreEqual(PickingBedStatus.Picked, result.Status);
                Assert.AreEqual(playerCount - i - 1, result.Remaining);
                Assert.AreEqual(1, hands[i].Accepted.Count);
                Assert.AreEqual((double)i, hands[i].Accepted[0].Freshness.LastSettleGameSeconds);
            }
            PickingBedSnapshot empty = _session.SnapshotAt(playerCount);
            Assert.AreEqual(0, empty.Remaining);
            Assert.AreEqual(playerCount, empty.TakenAtSeconds.Count);
            for (int i = 0; i < playerCount; i++)
            {
                Assert.AreEqual(PickingBedStatus.Empty,
                    _session.TryPick(_bed.RegionId, _bed.Position, playerCount, hands[i], wallets[i]).Status);
                Assert.AreEqual(1, hands[i].Accepted.Count);
            }

            Assert.AreEqual(0, _session.RemainingAt(RegrowSeconds - 0.001d));
            Assert.AreEqual(1, _session.RemainingAt(RegrowSeconds));
            Assert.IsTrue(_session.TryPick(_bed.RegionId, _bed.Position, RegrowSeconds,
                hands[playerCount - 1], wallets[playerCount - 1]).Kept);
            Assert.AreEqual(0, _session.RemainingAt(RegrowSeconds));
            Assert.AreEqual(1, _session.RemainingAt(RegrowSeconds + 1d));
            Assert.AreEqual(0, empty.Remaining, "an earlier detached snapshot stays empty");
            for (int i = 0; i < playerCount; i++)
                Assert.AreEqual(i == playerCount - 1 ? 2 : 1, hands[i].Accepted.Count);
        }

        [Test]
        public void FullHandsLeaveTheLastShellfishForTheOtherPicker()
        {
            _bed.Capacity = 1;
            _session = NewSession();
            _hands.Full = true;
            Assert.AreEqual(PickingBedStatus.HandsFull, Pick(10d).Status);
            Assert.AreEqual(1, _session.RemainingAt(10d));
            Assert.AreEqual(0, _session.SnapshotAt(10d).TakenAtSeconds.Count);
            var otherHands = new Hands();
            Assert.IsTrue(_session.TryPick(_bed.RegionId, _bed.Position, 10d, otherHands, new Licences()).Kept);
            Assert.AreEqual(0, _hands.Accepted.Count);
            Assert.AreEqual(1, otherHands.Accepted.Count);
            Assert.AreEqual(0, _session.RemainingAt(10d));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EachPickerUsesHisOwnLicence_RegardlessOfTheGlobalWallet(bool globalLicensed)
        {
            GameServices.Licenses = new Licences { Held = globalLicensed };
            _bed.Capacity = 1;
            _session = NewSession();
            var unlicensed = new Licences { Held = false };
            var otherHands = new Hands();
            int released = 0;
            Action<UnlicensedCatchReleased> onRelease = _ => released++;
            EventBus.Subscribe(onRelease);
            try
            {
                Assert.AreEqual(PickingBedStatus.ReleasedUnlicensed,
                    _session.Check(_bed.RegionId, _bed.Position, 10d, unlicensed).Status);
                Assert.AreEqual(PickingBedStatus.ReleasedUnlicensed,
                    _session.TryPick(_bed.RegionId, _bed.Position, 10d, otherHands, unlicensed).Status);
                Assert.AreEqual(1, released);
                Assert.AreEqual(0, otherHands.Accepted.Count);
                Assert.AreEqual(1, _session.RemainingAt(10d));
                Assert.IsTrue(Pick(10d).Kept);
                Assert.AreEqual(1, _hands.Accepted.Count);
                Assert.AreEqual(0, _session.RemainingAt(10d));
                Assert.AreEqual(1, released);
            }
            finally { EventBus.Unsubscribe(onRelease); }
        }

        [Test]
        public void MissingPersonalWalletCannotBorrowTheGlobalLicence()
        {
            GameServices.Licenses = _licences;
            Assert.AreEqual(PickingBedStatus.LicenceServiceUnavailable,
                _session.TryPick(_bed.RegionId, _bed.Position, 10d, _hands, null).Status);
            Assert.AreEqual(0, _hands.Accepted.Count);
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(10d));
        }

        [TestCase("tide")]
        [TestCase("licence")]
        [TestCase("hands")]
        public void AnotherPickerCannotEnterDuringTheLastPick(string callback)
        {
            _bed.Capacity = 1;
            _session = NewSession();
            var otherHands = new Hands();
            var otherWallet = new Licences();
            int attempts = 0;
            Action tryOther = () =>
            {
                attempts++;
                Assert.AreEqual(PickingBedStatus.Busy,
                    _session.Check(_bed.RegionId, _bed.Position, 0d, otherWallet).Status);
                Assert.AreEqual(PickingBedStatus.Busy,
                    _session.TryPick(_bed.RegionId, _bed.Position, 0d, otherHands, otherWallet).Status);
            };
            if (callback == "tide") _tide.OnRead = tryOther;
            else if (callback == "licence") _licences.OnCheck = tryOther;
            else _hands.OnPut = tryOther;

            Assert.IsTrue(Pick().Kept);
            Assert.AreEqual(1, attempts);
            Assert.AreEqual(1, _hands.Accepted.Count);
            Assert.AreEqual(0, otherHands.Accepted.Count);
            Assert.AreEqual(0, _session.RemainingAt(0d));
            Assert.AreEqual(PickingBedStatus.Empty,
                _session.TryPick(_bed.RegionId, _bed.Position, 0d, otherHands, otherWallet).Status);
        }

        [TestCase("tide")]
        [TestCase("licence")]
        [TestCase("hands")]
        public void ThrowingCallbackReleasesTheAttemptWithoutTakingStock(string callback)
        {
            Action fail = () => throw new InvalidOperationException("Delivery unavailable.");
            if (callback == "tide") _tide.OnRead = fail;
            else if (callback == "licence") _licences.OnCheck = fail;
            else _hands.OnPut = fail;
            Assert.Throws<InvalidOperationException>(() => Pick());
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(0d));
            Assert.AreEqual(0, _session.SnapshotAt(0d).TakenAtSeconds.Count);
            Assert.AreEqual(0, _hands.Accepted.Count);
            _tide.OnRead = null;
            _licences.OnCheck = null;
            _hands.OnPut = null;
            var otherHands = new Hands();
            Assert.IsTrue(_session.TryPick(_bed.RegionId, _bed.Position, 0d, otherHands, new Licences()).Kept);
            Assert.AreEqual(1, otherHands.Accepted.Count);
            Assert.AreEqual(_bed.Capacity - 1, _session.RemainingAt(0d));
        }

        [Test]
        public void EveryRemovalRegrowsOnItsOwnDeadline_NotAtMidnightOrHighWater()
        {
            double first = _config.SecondsPerDay - 1d;
            double second = first + _config.SecondsPerDay / 2d;
            Assert.IsTrue(Pick(first).Kept);
            Assert.IsTrue(Pick(second).Kept);
            Assert.AreEqual(_bed.Capacity - 2, _session.RemainingAt(first + RegrowSeconds - 0.001d));
            Assert.AreEqual(_bed.Capacity - 1, _session.RemainingAt(first + RegrowSeconds));
            Assert.AreEqual(_bed.Capacity - 1, _session.RemainingAt(second + RegrowSeconds - 0.001d));
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(second + RegrowSeconds));
            _tide.Level = _bed.UncoveredBelowMetres + 1f;
            Assert.AreEqual(PickingBedStatus.Covered, Pick(second + RegrowSeconds).Status);
            _tide.Level = _bed.UncoveredBelowMetres - 1f;
            Assert.IsTrue(Pick(second + RegrowSeconds).Kept);
            Assert.AreEqual(1, _session.SnapshotAt(second + RegrowSeconds).TakenAtSeconds.Count);
        }

        [Test]
        public void ExhaustedBedIsFullAfterASleepSkip_WithoutTicks()
        {
            for (int i = 0; i < _bed.Capacity; i++) Assert.IsTrue(Pick().Kept);
            Assert.AreEqual(0, _session.RemainingAt(RegrowSeconds - 0.001d));
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(RegrowSeconds));
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(RegrowSeconds * 100d));
            Assert.AreEqual(0, _session.SnapshotAt(RegrowSeconds * 100d).TakenAtSeconds.Count);
        }

        [Test]
        public void DayLengthIsAnInput_AndDoesNotAssumeRealWorldSeconds()
        {
            var session = new PickingBedSession(_bed, _tide, _config.SecondsPerDay * 2d);
            Assert.IsTrue(session.TryPick(_bed.RegionId, _bed.Position, 0d, _hands, _licences).Kept);
            Assert.AreEqual(_bed.Capacity - 1, session.RemainingAt(RegrowSeconds));
            Assert.AreEqual(_bed.Capacity, session.RemainingAt(RegrowSeconds * 2d));
        }

        [Test]
        public void SameSeedTimeAndActions_ReproduceWeightsAndStock_RegardlessOfReadFrequency()
        {
            var other = NewSession();
            var hands = new Hands();
            for (int i = 0; i < _bed.Capacity; i++)
            {
                double time = i + 1d;
                Assert.AreEqual(_bed.Capacity - i, _session.SnapshotAt(time).Remaining);
                Assert.AreEqual(_bed.Capacity - i, _session.RemainingAt(time + 0.25d));
                Assert.AreEqual(_bed.Capacity - i, _session.RemainingAt(time));
                Assert.IsTrue(Pick(time).Kept);
                Assert.IsTrue(other.TryPick(_bed.RegionId, _bed.Position, time, hands, _licences).Kept);
                Assert.AreEqual(_hands.Accepted[i], hands.Accepted[i]);
                CollectionAssert.AreEqual(_session.SnapshotAt(time).TakenAtSeconds, other.SnapshotAt(time).TakenAtSeconds);
            }
            Assert.AreEqual(other.RemainingAt(RegrowSeconds), _session.RemainingAt(RegrowSeconds));
        }

        [Test]
        public void SeedAndBedIdentityChangeWeights_ButNotTheAuthoredCapacity()
        {
            var otherTide = new TideProbe { WorldSeed = _tide.WorldSeed + 1, Level = _tide.Level };
            var otherSeed = new PickingBedSession(_bed, otherTide, _config.SecondsPerDay);
            _bed.Id = "pickingbed.another_bed";
            var otherBed = NewSession();
            var seedHands = new Hands();
            var bedHands = new Hands();
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(Pick(i).Kept);
                Assert.IsTrue(otherSeed.TryPick(_bed.RegionId, _bed.Position, i, seedHands, _licences).Kept);
                Assert.IsTrue(otherBed.TryPick(_bed.RegionId, _bed.Position, i, bedHands, _licences).Kept);
            }
            CollectionAssert.AreNotEqual(_hands.Accepted, seedHands.Accepted);
            CollectionAssert.AreNotEqual(_hands.Accepted, bedHands.Accepted);
            Assert.AreEqual(_bed.Capacity - 4, otherSeed.RemainingAt(4d));
            Assert.AreEqual(_bed.Capacity - 4, otherBed.RemainingAt(4d));
        }

        [Test]
        public void RefusedHandsLeaveNoRemoval_AndRetryProducesTheSameItem()
        {
            Assert.AreEqual(PickingBedStatus.HandsUnavailable,
                _session.TryPick(_bed.RegionId, _bed.Position, 10d, null, _licences).Status);
            _hands.Full = true;
            Assert.AreEqual(PickingBedStatus.HandsFull, Pick(10d).Status);
            Assert.AreEqual(0, _session.SnapshotAt(10d).TakenAtSeconds.Count);
            _hands.Full = false;
            var fresh = NewSession();
            var otherHands = new Hands();
            Assert.IsTrue(Pick(10d).Kept);
            Assert.IsTrue(fresh.TryPick(_bed.RegionId, _bed.Position, 10d, otherHands, _licences).Kept);
            Assert.AreEqual(otherHands.Accepted[0], _hands.Accepted[0]);
        }

        [Test]
        public void WrongShoreAndDistantPlayerCannotTakeStock_ReachBoundaryIsInclusive()
        {
            Assert.AreEqual(PickingBedStatus.WrongRegion,
                _session.TryPick("region.nine_mile_creek", _bed.Position, 0d, _hands, _licences).Status);
            Assert.AreEqual(PickingBedStatus.OutOfReach,
                _session.TryPick(_bed.RegionId, _bed.Position + Vector2.up * (_bed.ReachMetres + 1f), 0d, _hands, _licences).Status);
            Assert.AreEqual(PickingBedStatus.OutOfReach,
                _session.TryPick(_bed.RegionId, new Vector2(float.NaN, 0f), 0d, _hands, _licences).Status);
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(0d));
            Assert.IsTrue(_session.TryPick(_bed.RegionId, _bed.Position + Vector2.up * _bed.ReachMetres, 0d, _hands, _licences).Kept);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        public void InvalidTimeNeverChangesStock(double time)
        {
            Assert.AreEqual(PickingBedStatus.InvalidTime, Pick(time).Status);
            Assert.Throws<ArgumentOutOfRangeException>(() => _session.RemainingAt(time));
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(0d));
            Assert.AreEqual(0, _hands.Accepted.Count);
        }

        [Test]
        public void RewindingAfterAPickIsRefused_AndAnotherWorldCannotUseTheSession()
        {
            Assert.IsTrue(Pick(100d).Kept);
            Assert.AreEqual(PickingBedStatus.InvalidTime, Pick(99d).Status);
            _tide.WorldSeed++;
            Assert.AreEqual(PickingBedStatus.TideUnavailable, Pick(100d).Status);
            Assert.AreEqual(_bed.Capacity - 1, _session.RemainingAt(100d));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonfiniteWaterFailsClosed(float level)
        {
            _tide.Level = level;
            Assert.AreEqual(PickingBedStatus.TideUnavailable, Pick().Status);
            Assert.AreEqual(_bed.Capacity, _session.RemainingAt(0d));
        }

        [Test]
        public void MissingServicesFailClosed()
        {
            var noTide = new PickingBedSession(_bed, null, _config.SecondsPerDay);
            Assert.AreEqual(PickingBedStatus.TideUnavailable,
                noTide.TryPick(_bed.RegionId, _bed.Position, 0d, _hands, _licences).Status);
            _licences = null;
            Assert.AreEqual(PickingBedStatus.LicenceServiceUnavailable, Pick().Status);
            Assert.AreEqual(0, _hands.Accepted.Count);
        }

        [Test]
        public void SnapshotIsDetached_AndANewSessionStartsFull()
        {
            Assert.IsTrue(Pick(10d).Kept);
            PickingBedSnapshot snapshot = _session.SnapshotAt(10d);
            Assert.AreEqual(_bed.Id, snapshot.BedId);
            Assert.AreEqual(_tide.WorldSeed, snapshot.WorldSeed);
            Assert.AreEqual(10d, snapshot.AtSeconds);
            Assert.AreEqual(10d, snapshot.LastPickSeconds);
            Assert.AreEqual(_bed.Capacity - 1, snapshot.Remaining);
            Assert.Throws<NotSupportedException>(() => ((IList<double>)snapshot.TakenAtSeconds)[0] = 0d);
            Assert.IsTrue(Pick(20d).Kept);
            CollectionAssert.AreEqual(new[] { 10d }, snapshot.TakenAtSeconds);
            CollectionAssert.AreEqual(new[] { 10d, 20d }, _session.SnapshotAt(20d).TakenAtSeconds);
            Assert.AreEqual(_bed.Capacity, NewSession().RemainingAt(20d));
        }

        [Test]
        public void RepeatedRegrowthKeepsOnlyCapacityBoundedRemovalState()
        {
            for (int cycle = 0; cycle < 20; cycle++)
            {
                double time = cycle * RegrowSeconds;
                for (int i = 0; i < _bed.Capacity; i++) Assert.IsTrue(Pick(time).Kept);
                PickingBedSnapshot snapshot = _session.SnapshotAt(time);
                Assert.AreEqual(_bed.Capacity, snapshot.TakenAtSeconds.Count);
                foreach (double taken in snapshot.TakenAtSeconds) Assert.AreEqual(time, taken);
                Assert.AreEqual(0, snapshot.Remaining);
            }
        }

        [Test]
        public void ReentrantHandCallbackCannotTakeASecondShellfish()
        {
            _hands.OnPut = () => Assert.AreEqual(PickingBedStatus.Busy, Pick().Status);
            Assert.IsTrue(Pick().Kept);
            Assert.AreEqual(1, _hands.Accepted.Count);
            Assert.AreEqual(_bed.Capacity - 1, _session.RemainingAt(0d));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.MaxValue)]
        public void InvalidDayLengthIsRejected(double secondsPerDay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickingBedSession(_bed, _tide, secondsPerDay));
        }

        [TestCase("id")]
        [TestCase("region")]
        [TestCase("species")]
        [TestCase("species_region")]
        [TestCase("gear")]
        [TestCase("protected")]
        [TestCase("capacity")]
        [TestCase("regrow_zero")]
        [TestCase("regrow_nan")]
        [TestCase("position")]
        [TestCase("reach")]
        [TestCase("threshold")]
        [TestCase("above_species_tide")]
        [TestCase("below_species_tide")]
        [TestCase("weight")]
        public void InvalidBedContentIsRejected(string field)
        {
            switch (field)
            {
                case "id": _bed.Id = "pickingbed.Bad Id"; break;
                case "region": _bed.RegionId = "st_peters"; break;
                case "species": _bed.Species = null; break;
                case "species_region": _species.RegionIds = new[] { "region.somewhere_else" }; break;
                case "gear": _species.AllowedGear = Gear.Handline; break;
                case "protected": _species.Protected = true; break;
                case "capacity": _bed.Capacity = 0; break;
                case "regrow_zero": _bed.RegrowDays = 0f; break;
                case "regrow_nan": _bed.RegrowDays = float.NaN; break;
                case "position": _bed.Position = new Vector2(float.PositiveInfinity, 0f); break;
                case "reach": _bed.ReachMetres = -1f; break;
                case "threshold": _bed.UncoveredBelowMetres = float.NaN; break;
                case "above_species_tide": _bed.UncoveredBelowMetres = _species.MaxTide + 1f; break;
                case "below_species_tide": _bed.UncoveredBelowMetres = _species.MinTide; break;
                case "weight": _species.MinWeightKg = -1f; break;
            }
            Assert.IsFalse(_bed.HasValidTuning, field);
            Assert.Throws<ArgumentException>(() => NewSession());
        }
    }
}
