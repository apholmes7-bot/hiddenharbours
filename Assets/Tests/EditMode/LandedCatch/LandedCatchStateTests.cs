using System;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.LandedCatch;
using NUnit.Framework;

namespace HiddenHarbours.Tests.LandedCatch
{
    internal static class CatchFixtures
    {
        // Synthetic arithmetic fixtures, not species claims or shipping balance.
        internal static LandedCatchSettings Settings => new LandedCatchSettings(1000f,
            new SpoilPolicy(1f, 0f, 0f, 0f, 0.75f));
        internal static LandedCatchProfile Fish => new LandedCatchProfile(
            "research.still", "fish.fixture", false, false, null, 1f);
        internal static LandedCatchProfile Shell => new LandedCatchProfile(
            "research.live", "fish.shell_fixture", true, true, 100d, 1f);
        internal static LandedCatchState LandFish(StorageMode mode = StorageMode.Ambient, double? iceEnd = null)
            => LandedCatchState.Land(Fish, Settings, 0d, mode, iceEnd);
        internal static LandedCatchState LandShell(StorageMode mode = StorageMode.Ambient, double? iceEnd = null)
            => LandedCatchState.Land(Shell, Settings, 0d, mode, iceEnd);
        internal static void Equal(LandedCatchState actual, LandedCatchState expected)
        {
            Assert.That(actual.Life, Is.EqualTo(expected.Life));
            Assert.That(actual.FreshnessState.Mode, Is.EqualTo(expected.FreshnessState.Mode));
            Assert.That(actual.FreshnessState.SpoilAccrued, Is.EqualTo(expected.FreshnessState.SpoilAccrued));
            Assert.That(actual.FreshnessState.LastSettleGameSeconds, Is.EqualTo(expected.FreshnessState.LastSettleGameSeconds));
            Assert.That(actual.EvaluatedAtGameSeconds, Is.EqualTo(expected.EvaluatedAtGameSeconds));
            Assert.That(actual.AmbientLiveSecondsAccrued, Is.EqualTo(expected.AmbientLiveSecondsAccrued));
            Assert.That(actual.IceEndsAtGameSeconds, Is.EqualTo(expected.IceEndsAtGameSeconds));
        }
    }

    public sealed class LandedCatchLifeTests
    {
        [Test]
        public void StillFishLandsFreshAndNeverAcquiresLife()
        {
            var fish = CatchFixtures.LandFish();
            Assert.That(fish.IsStill, Is.True);
            Assert.That(fish.IsAlive, Is.False);
            Assert.That(fish.Quality, Is.EqualTo(1f));
            Assert.That(fish.Advance(500d).Life, Is.EqualTo(CatchLifeState.Dead));
            Assert.Throws<InvalidOperationException>(() => fish.WithStorage(500d, StorageMode.Live));
        }

        [Test]
        public void LiveHabitatPausesBothAuthoredExposureAndFixtureSpoilage()
        {
            var shell = CatchFixtures.LandShell(StorageMode.Live).Advance(2000d);
            Assert.That(shell.IsAlive, Is.True);
            Assert.That(shell.IsStill, Is.False);
            Assert.That(shell.AmbientLiveSecondsAccrued, Is.Zero);
            Assert.That(shell.Quality, Is.EqualTo(1f));
        }

        [TestCase(99d, CatchLifeState.Alive)]
        [TestCase(100d, CatchLifeState.Dead)]
        [TestCase(101d, CatchLifeState.Dead)]
        public void AuthoredExposureBoundaryIsExact(double now, CatchLifeState life)
        {
            var shell = CatchFixtures.LandShell().Advance(now);
            Assert.That(shell.Life, Is.EqualTo(life));
            Assert.That(shell.AmbientLiveSecondsAccrued, Is.EqualTo(Math.Min(now, 100d)));
            Assert.That(shell.FreshnessState.Mode, Is.EqualTo(StorageMode.Ambient));
        }

        [Test]
        public void ReturningToWetStoragePreservesExposureAndSpoilage()
        {
            var wet = CatchFixtures.LandShell().WithStorage(60d, StorageMode.Live).Advance(500d);
            Assert.That(wet.AmbientLiveSecondsAccrued, Is.EqualTo(60d));
            Assert.That(wet.FreshnessState.SpoilAccrued, Is.EqualTo(0.06f));
            var dry = wet.WithStorage(500d, StorageMode.Ambient);
            Assert.That(dry.Advance(539d).IsAlive, Is.True);
            Assert.That(dry.Advance(540d).Life, Is.EqualTo(CatchLifeState.Dead));
        }

        [Test]
        public void IcePreservesQualityButDoesNotInventALiveHabitat()
        {
            var iced = CatchFixtures.LandShell(StorageMode.Iced, 500d).Advance(100d);
            Assert.That(iced.IsStill, Is.True);
            Assert.That(iced.FreshnessState.Mode, Is.EqualTo(StorageMode.Iced));
            Assert.That(iced.IceEndsAtGameSeconds, Is.EqualTo(500d));
            Assert.That(iced.Quality, Is.EqualTo(1f));
        }

        [Test]
        public void FreezingKillsAndThawingCannotRevive()
        {
            var frozen = CatchFixtures.LandShell().WithStorage(50d, StorageMode.Frozen).Advance(500d);
            Assert.That(frozen.Life, Is.EqualTo(CatchLifeState.Dead));
            Assert.That(frozen.Quality, Is.EqualTo(0.95f));
            var thawed = frozen.WithStorage(500d, StorageMode.Ambient).Advance(600d);
            Assert.That(thawed.Quality, Is.EqualTo(0.85f));
            Assert.Throws<InvalidOperationException>(() => thawed.WithStorage(600d, StorageMode.Live));
        }

        [Test]
        public void StopLifeInWetStorageBanksOldRateAndStartsAmbientAtThatInstant()
        {
            var stopped = CatchFixtures.LandShell(StorageMode.Live).StopLife(500d);
            Assert.That(stopped.IsStill, Is.True);
            Assert.That(stopped.FreshnessState.Mode, Is.EqualTo(StorageMode.Ambient));
            Assert.That(stopped.Advance(600d).Quality, Is.EqualTo(0.9f));
            CatchFixtures.Equal(stopped.StopLife(500d), stopped);
        }

        [Test]
        public void MissingSurvivalDataAllowsCompatibleWetHoldingButRefusesLiveFloorOrIce()
        {
            var profile = new LandedCatchProfile("research.no_timer", "fish.fixture", true, true, null, 1f);
            var wet = LandedCatchState.Land(profile, CatchFixtures.Settings, 0d, StorageMode.Live);
            Assert.That(wet.Advance(100000d).IsAlive, Is.True);
            Assert.Throws<InvalidOperationException>(() => wet.WithStorage(5d, StorageMode.Ambient));
            Assert.Throws<InvalidOperationException>(() => wet.WithStorage(5d, StorageMode.Iced, 50d));
            Assert.That(wet.WithStorage(5d, StorageMode.Frozen).IsStill, Is.True);
            Assert.That(wet.StopLife(5d).FreshnessState.Mode, Is.EqualTo(StorageMode.Ambient));
        }

        [Test]
        public void SurvivalDurationComesFromProfile()
        {
            var profile = new LandedCatchProfile("research.longer", "fish.fixture", true, true, 200d, 1f);
            var shell = LandedCatchState.Land(profile, CatchFixtures.Settings, 0d, StorageMode.Ambient);
            Assert.That(shell.Advance(100d).IsAlive, Is.True);
            Assert.That(shell.Advance(200d).IsStill, Is.True);
        }
    }

    public sealed class LandedCatchQualityTests
    {
        [Test]
        public void CoolingBanksWarmHistoryAndNeverMakesQualityIncrease()
        {
            var fish = CatchFixtures.LandFish();
            var iced = fish.WithStorage(250d, StorageMode.Iced, 500d);
            Assert.That(iced.Quality, Is.EqualTo(0.75f));
            Assert.That(iced.Advance(500d).Quality, Is.EqualTo(0.75f));
            var warm = iced.Advance(750d);
            Assert.That(warm.Quality, Is.EqualTo(0.5f));
            Assert.That(warm.FreshnessState.Mode, Is.EqualTo(StorageMode.Ambient));
            Assert.That(fish.Quality, Is.EqualTo(1f), "Evaluation must not mutate the source.");
        }

        [TestCase(499d, StorageMode.Iced, 1f)]
        [TestCase(500d, StorageMode.Ambient, 1f)]
        [TestCase(750d, StorageMode.Ambient, 0.75f)]
        public void IceExpiryHasAnExactBoundary(double now, StorageMode mode, float quality)
        {
            var fish = CatchFixtures.LandFish(StorageMode.Iced, 500d).Advance(now);
            Assert.That(fish.FreshnessState.Mode, Is.EqualTo(mode));
            Assert.That(fish.Quality, Is.EqualTo(quality));
            Assert.That(fish.IceEndsAtGameSeconds, Is.EqualTo(mode == StorageMode.Iced ? (double?)500d : null));
        }

        [Test]
        public void IceEndingOnArrivalIsImmediatelyAmbient()
        {
            var fish = CatchFixtures.LandFish(StorageMode.Iced, 0d);
            Assert.That(fish.FreshnessState.Mode, Is.EqualTo(StorageMode.Ambient));
            Assert.That(fish.IceEndsAtGameSeconds, Is.Null);
            Assert.That(fish.Advance(250d).Quality, Is.EqualTo(0.75f));
        }

        [Test]
        public void BuyerRefusalUsesExistingSpoilPolicyEvenWhileCatchOccupiesStorage()
        {
            var fish = CatchFixtures.LandFish();
            Assert.That(fish.Advance(749d).IsSellable, Is.True);
            Assert.That(fish.Advance(750d).IsSellable, Is.False);
            Assert.That(fish.Advance(2000d).Quality, Is.Zero);
            Assert.That(fish.Advance(2000d).Profile.SpeciesId, Is.EqualTo("fish.fixture"));
        }

        [Test]
        public void SpeciesRateDayLengthAndStorageRatesAreInputs()
        {
            var settings = new LandedCatchSettings(2000f, new SpoilPolicy(2f, 0.5f, 0.25f, 0.5f, 0.8f));
            var profile = new LandedCatchProfile("research.rates", "fish.fixture", false, false, null, 0.5f);
            var fish = LandedCatchState.Land(profile, settings, 0d, StorageMode.Ambient);
            Assert.That(fish.Advance(1000d).Quality, Is.EqualTo(0.5f));
            Assert.That(fish.WithStorage(0d, StorageMode.Iced, 2000d).Advance(1000d).Quality, Is.EqualTo(0.875f));
            Assert.That(fish.WithStorage(0d, StorageMode.Frozen).Advance(1000d).Quality, Is.EqualTo(0.9375f));
        }

        [Test]
        public void LiveAndSellableAreIndependentFacts()
        {
            var settings = new LandedCatchSettings(1000f, new SpoilPolicy(1f, 1f, 0f, 0f, 0.75f));
            var shell = LandedCatchState.Land(CatchFixtures.Shell, settings, 0d, StorageMode.Live).Advance(800d);
            Assert.That(shell.IsAlive, Is.True);
            Assert.That(shell.IsSellable, Is.False);
        }

        [TestCase(StorageMode.Ambient)]
        [TestCase(StorageMode.Iced)]
        public void PollingAndSingleJumpAreBitEqualIncludingBothBoundaries(StorageMode mode)
        {
            var start = CatchFixtures.LandShell(mode, mode == StorageMode.Iced ? (double?)137d : null);
            var polled = start;
            for (int i = 1; i <= 1000; i++) polled = polled.Advance(i * 0.37d);
            CatchFixtures.Equal(polled, start.Advance(370d));
            CatchFixtures.Equal(polled.Advance(370d), polled);
        }

        [Test]
        public void SameHistoryReplaysExactlyWithoutASeedOrWallClock()
        {
            Func<LandedCatchState> replay = () => CatchFixtures.LandShell()
                .WithStorage(20d, StorageMode.Live)
                .WithStorage(200d, StorageMode.Iced, 250d)
                .Advance(300d).StopLife(300d)
                .WithStorage(300d, StorageMode.Frozen).Advance(2000d);
            CatchFixtures.Equal(replay(), replay());
        }
    }

    public sealed class LandedCatchValidationTests
    {
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidGameTimeIsRejectedWithoutMutation(double now)
        {
            var fish = CatchFixtures.LandFish();
            Assert.Throws<ArgumentOutOfRangeException>(() => fish.Advance(now));
            Assert.Throws<ArgumentOutOfRangeException>(() => LandedCatchState.Land(
                CatchFixtures.Fish, CatchFixtures.Settings, now, StorageMode.Ambient));
            Assert.That(fish.EvaluatedAtGameSeconds, Is.Zero);
        }

        [Test]
        public void RewindIsRejectedRatherThanResettingExposureOrFreshness()
        {
            var fish = CatchFixtures.LandFish().Advance(100d);
            Assert.Throws<ArgumentOutOfRangeException>(() => fish.Advance(99d));
            Assert.Throws<ArgumentOutOfRangeException>(() => fish.WithStorage(99d, StorageMode.Frozen));
            Assert.That(fish.Quality, Is.EqualTo(0.9f));
        }

        [TestCase(null)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void IceRequiresAFiniteCurrentOrFutureExpiry(double? end)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CatchFixtures.LandFish(StorageMode.Iced, end));
        }

        [Test]
        public void UnrelatedStorageCannotCarryAnIceExpiryOrAnUnknownMode()
        {
            Assert.Throws<ArgumentException>(() => CatchFixtures.LandFish(StorageMode.Ambient, 50d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CatchFixtures.LandFish((StorageMode)99));
            var incompatible = new LandedCatchProfile("research.no_live", "fish.fixture", true, false, 100d, 1f);
            Assert.Throws<InvalidOperationException>(() => LandedCatchState.Land(
                incompatible, CatchFixtures.Settings, 0d, StorageMode.Live));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidMortalityDurationIsRejected(double duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandedCatchProfile(
                "research.invalid", "fish.fixture", true, true, duration, 1f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDayLengthIsRejected(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandedCatchSettings(seconds, CatchFixtures.Settings.SpoilPolicy));
        }

        [Test]
        public void ProfilesRequireIdsAndFiniteNonnegativeSpoilageAndPolicies()
        {
            Assert.Throws<ArgumentException>(() => new LandedCatchProfile(" ", "fish.fixture", false, false, null, 1f));
            Assert.Throws<ArgumentException>(() => new LandedCatchProfile("research.invalid", null, false, false, null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandedCatchProfile("research.invalid", "fish.fixture", false, false, null, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandedCatchProfile("research.invalid", "fish.fixture", false, false, null, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandedCatchSettings(1000f, new SpoilPolicy(1f, -1f, 0f, 0f, 0.75f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandedCatchSettings(1000f, new SpoilPolicy(1f, 0f, 0f, 0f, float.NaN)));
            Assert.Throws<ArgumentNullException>(() => LandedCatchState.Land(null, CatchFixtures.Settings, 0d, StorageMode.Ambient));
            Assert.Throws<ArgumentNullException>(() => LandedCatchState.Land(CatchFixtures.Fish, null, 0d, StorageMode.Ambient));
        }
    }
}
