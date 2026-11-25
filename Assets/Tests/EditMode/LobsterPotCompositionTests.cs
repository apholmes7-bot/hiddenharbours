using System;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class LobsterPotCompositionTests
    {
        // Synthetic fixtures, not proposed regional biology, legal gauges or live balance values.
        private static readonly LobsterPotSettings Settings = new LobsterPotSettings(1800, 28, 28, 600);
        private static readonly LobsterPotSeason Mixed = new LobsterPotSeason(0.75, 0.4, 0.2);
        private static readonly double[] Times = { 600, 1800, 50400, 201600, 1e9, 1e12 + 0.125 };

        private static LobsterPotGround Ground(LobsterPotSeason mix, string id = "ground.test", int capacity = 32)
            => new LobsterPotGround(id, capacity, 60, 80, 140, 70, mix, mix, mix, mix);

        private static LobsterPotSummary Compose(int seed, double time, double soak, LobsterPotGround ground,
            LobsterPotAnimal[] buffer, LobsterPotSettings? settings = null)
        {
            var config = settings ?? Settings;
            Assert.IsTrue(LobsterPotComposition.TryCompose(seed, time, ground, soak, config, buffer, out var summary));
            return summary;
        }

        private static void Same(LobsterPotAnimal a, LobsterPotAnimal b)
        {
            Assert.AreEqual(a.CarapaceMm, b.CarapaceMm);
            Assert.AreEqual(a.IsShort, b.IsShort);
            Assert.AreEqual(a.IsBerried, b.IsBerried);
        }

        [Test]
        public void ThousandSeeds_ReplayEveryAnimalAndSummaryAfterUnrelatedCallsAtLongTimes()
        {
            var ground = Ground(Mixed);
            var first = new LobsterPotAnimal[ground.Capacity];
            var replay = new LobsterPotAnimal[ground.Capacity];
            int comparisons = 0;
            for (int index = 0; index < 1000; index++)
            {
                int seed = index == 0 ? int.MinValue : index == 1 ? int.MaxValue : index * 7919;
                foreach (double time in Times)
                {
                    var a = Compose(seed, time, 600, ground, first);
                    Compose(~seed, time + 1, 300, Ground(Mixed, "ground.other"), replay);
                    var b = Compose(seed, time, 600, ground, replay);
                    Assert.AreEqual(a.Season, b.Season);
                    Assert.AreEqual(a.Count, b.Count);
                    Assert.AreEqual(a.Shorts, b.Shorts);
                    Assert.AreEqual(a.Berried, b.Berried);
                    Assert.AreEqual(a.ShortsAndBerried, b.ShortsAndBerried);
                    for (int i = 0; i < a.Count; i++) Same(first[i], replay[i]);
                    comparisons++;
                }
            }
            Assert.AreEqual(6000, comparisons);
        }

        [TestCase(0.0, 0.0, 0, 0, 32)]
        [TestCase(1.0, 0.0, 32, 0, 0)]
        [TestCase(0.0, 1.0, 0, 32, 0)]
        [TestCase(1.0, 1.0, 32, 32, 0)]
        public void EndpointShares_ProduceExactFlagsAndOverlappingExclusions(double shortShare,
            double berriedShare, int shorts, int berried, int eligible)
        {
            var ground = Ground(new LobsterPotSeason(1, shortShare, berriedShare));
            var buffer = new LobsterPotAnimal[32];
            var summary = Compose(7, 1200, 600, ground, buffer);
            Assert.AreEqual(32, summary.Count);
            Assert.AreEqual(shorts, summary.Shorts);
            Assert.AreEqual(berried, summary.Berried);
            Assert.AreEqual(shortShare == 1 && berriedShare == 1 ? 32 : 0, summary.ShortsAndBerried);
            Assert.AreEqual(eligible, summary.SizeAndEggEligible);
            foreach (var animal in buffer)
            {
                Assert.AreEqual(shortShare == 1, animal.IsShort);
                Assert.AreEqual(berriedShare == 1, animal.IsBerried);
                Assert.AreEqual(animal.CarapaceMm < 80, animal.IsShort);
                Assert.That(animal.CarapaceMm, Is.InRange(animal.IsBerried ? 70 : 60, 140));
            }
        }

        [Test]
        public void ZeroSoakAndEmptyGround_LeaveStorageUntouched()
        {
            var buffer = new LobsterPotAnimal[32];
            var full = Ground(new LobsterPotSeason(1, 0, 0));
            Compose(7, 1200, 600, full, buffer);
            var sentinel = (LobsterPotAnimal[])buffer.Clone();
            Assert.AreEqual(0, Compose(7, 1200, 0, full, buffer).Count);
            Assert.AreEqual(0, Compose(7, 1200, 600, Ground(new LobsterPotSeason(0, 1, 1)), buffer).Count);
            for (int i = 0; i < buffer.Length; i++) Same(sentinel[i], buffer[i]);
        }

        [Test]
        public void LongerSoakAtFixedHaul_AddsSlotsWithoutChangingAnimalsAndSaturates()
        {
            var ground = Ground(new LobsterPotSeason(1, 0.4, 0.2));
            var before = new LobsterPotAnimal[32];
            var after = new LobsterPotAnimal[32];
            var saturated = Compose(22, 2400, 600, ground, after);
            Assert.AreEqual(32, saturated.Count);
            int previousCount = 0;
            foreach (double soak in new[] { 0d, 1, 100, 300, 599, 600, 1200, 2400 })
            {
                var summary = Compose(22, 2400, soak, ground, before);
                Assert.That(summary.Count, Is.InRange(previousCount, 32));
                int search = 0;
                for (int i = 0; i < summary.Count; i++)
                {
                    while (search < 32 && (after[search].CarapaceMm != before[i].CarapaceMm ||
                        after[search].IsShort != before[i].IsShort || after[search].IsBerried != before[i].IsBerried)) search++;
                    Assert.Less(search, 32);
                    Same(before[i], after[search++]);
                }
                previousCount = summary.Count;
                if (soak >= 600)
                    for (int i = 0; i < 32; i++) Same(after[i], before[i]);
            }
        }

        [Test]
        public void SeasonalRows_FollowConfiguredCalendarOffsetBoundariesAndYearWrap()
        {
            var ground = new LobsterPotGround("ground.seasons", 32, 60, 80, 140, 70,
                new LobsterPotSeason(1, 1, 0), new LobsterPotSeason(1, 0, 1),
                new LobsterPotSeason(1, 1, 1), new LobsterPotSeason(0, 0, 0));
            var config = new LobsterPotSettings(10, 2, 1, 1);
            var buffer = new LobsterPotAnimal[32];
            double[] instants = { 0, 9.999, 10, 29.999, 30, 49.999, 50, 69.999, 70, 1e12 + 10 };
            Season[] seasons = { Season.EarlySpring, Season.EarlySpring, Season.HighSummer,
                Season.HighSummer, Season.TheTurn, Season.TheTurn, Season.HardWinter, Season.HardWinter,
                Season.EarlySpring, Season.HighSummer };
            for (int i = 0; i < instants.Length; i++)
            {
                var summary = Compose(1, instants[i], Math.Min(1, instants[i]), ground, buffer, config);
                Assert.AreEqual(seasons[i], summary.Season);
                if (instants[i] == 0) { Assert.AreEqual(0, summary.Count); continue; }
                Assert.AreEqual(seasons[i] == Season.HardWinter ? 0 : 32, summary.Count);
                Assert.AreEqual(seasons[i] == Season.EarlySpring || seasons[i] == Season.TheTurn ? 32 : 0, summary.Shorts);
                Assert.AreEqual(seasons[i] == Season.HighSummer || seasons[i] == Season.TheTurn ? 32 : 0, summary.Berried);
            }
        }

        [Test]
        public void SeasonalDistributions_TrackAuthoredOccupancyShortAndBerriedShares()
        {
            var spring = new LobsterPotSeason(0.8, 0.2, 0.3);
            var summer = new LobsterPotSeason(0.9, 0.5, 0.1);
            var autumn = new LobsterPotSeason(0.6, 0.4, 0.2);
            var winter = new LobsterPotSeason(0.2, 0.3, 0.4);
            var ground = new LobsterPotGround("ground.distribution", 32, 60, 80, 140, 70, spring, summer, autumn, winter);
            var rows = new[] { spring, summer, autumn, winter };
            var buffer = new LobsterPotAnimal[32];
            for (int season = 0; season < 4; season++)
            {
                int count = 0, shorts = 0, berried = 0, overlap = 0, min = int.MaxValue, max = 0;
                double time = (season + 1) * 50400d + 600; // Config starts in summer; cycle through all rows.
                int row = (season + 2) % 4;
                for (int seed = 0; seed < 10000; seed++)
                {
                    var summary = Compose(seed, time, 600, ground, buffer);
                    Assert.AreEqual((Season)row, summary.Season);
                    count += summary.Count; shorts += summary.Shorts; berried += summary.Berried;
                    overlap += summary.ShortsAndBerried;
                    int actualShorts = 0, actualBerried = 0, actualOverlap = 0;
                    for (int i = 0; i < summary.Count; i++)
                    {
                        var a = buffer[i];
                        if (a.CarapaceMm < 80) actualShorts++;
                        if (a.IsBerried) actualBerried++;
                        if (a.IsShort && a.IsBerried) actualOverlap++;
                        Assert.AreEqual(a.CarapaceMm < 80, a.IsShort);
                        Assert.That(a.CarapaceMm, Is.InRange(a.IsBerried ? 70 : 60, 140));
                        min = Math.Min(min, a.CarapaceMm); max = Math.Max(max, a.CarapaceMm);
                    }
                    Assert.AreEqual(actualShorts, summary.Shorts);
                    Assert.AreEqual(actualBerried, summary.Berried);
                    Assert.AreEqual(actualOverlap, summary.ShortsAndBerried);
                }
                Assert.AreEqual(rows[row].FullSoakOccupancy01, count / 320000d, 0.01);
                Assert.AreEqual(rows[row].ShortShare01, shorts / (double)count, 0.01);
                Assert.AreEqual(rows[row].BerriedShare01, berried / (double)count, 0.01);
                Assert.AreEqual(rows[row].ShortShare01 * rows[row].BerriedShare01, overlap / (double)count, 0.01);
                Assert.AreEqual(60, min);
                Assert.AreEqual(140, max);
            }
        }

        [Test]
        public void SeedGroundAndSubsecondHaulTime_AllAffectTheStream()
        {
            var ground = Ground(new LobsterPotSeason(1, 0.4, 0.2));
            var first = new LobsterPotAnimal[32];
            var other = new LobsterPotAnimal[32];
            Compose(12, 1e12, 600, ground, first);
            for (int variation = 0; variation < 3; variation++)
            {
                Compose(variation == 0 ? 13 : 12, variation == 2 ? 1e12 + 0.125 : 1e12,
                    600, variation == 1 ? Ground(new LobsterPotSeason(1, 0.4, 0.2), "ground.other") : ground, other);
                int different = 0;
                for (int i = 0; i < 32; i++)
                    if (first[i].CarapaceMm != other[i].CarapaceMm || first[i].IsShort != other[i].IsShort ||
                        first[i].IsBerried != other[i].IsBerried) different++;
                Assert.That(different, Is.InRange(16, 32));
            }
        }

        [TestCase(double.NaN, 0)]
        [TestCase(double.PositiveInfinity, 0)]
        [TestCase(-1, 0)]
        [TestCase(1200, double.NaN)]
        [TestCase(1200, double.PositiveInfinity)]
        [TestCase(1200, -1)]
        [TestCase(1200, 1201)]
        public void InvalidTimeOrSoak_IsRejectedWithoutWrites(double time, double soak)
        {
            Reject(Ground(Mixed), Settings, time, soak);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void InvalidGroundOrSettings_IsRejectedWithoutWrites(int variant)
        {
            var invalidMix = new LobsterPotSeason(variant == 5 ? double.NaN : 1,
                variant == 6 ? -0.1 : 0.4, variant == 7 ? 1.1 : 0.2);
            var ground = new LobsterPotGround(variant == 0 ? " " : "ground.test", variant == 1 ? 0 : 32,
                variant == 2 ? 80 : 60, 80, variant == 3 ? 79 : 140, variant == 4 ? 80 : 70,
                invalidMix, Mixed, Mixed, Mixed);
            var settings = new LobsterPotSettings(variant == 8 ? double.PositiveInfinity : 1800,
                variant == 9 ? 0 : 28, variant == 10 ? 112 : 28, variant == 11 ? 0 : 600);
            Reject(ground, settings, 1200, 600);
        }

        private static void Reject(LobsterPotGround ground, LobsterPotSettings settings, double time, double soak)
        {
            var buffer = new LobsterPotAnimal[32];
            Compose(1, 1200, 600, Ground(new LobsterPotSeason(1, 1, 1)), buffer);
            var sentinel = (LobsterPotAnimal[])buffer.Clone();
            Assert.IsFalse(LobsterPotComposition.TryCompose(1, time, ground, soak, settings, buffer, out var summary));
            Assert.AreEqual(0, summary.Count);
            Assert.AreEqual(0, summary.Shorts);
            Assert.AreEqual(0, summary.Berried);
            Assert.AreEqual(0, summary.ShortsAndBerried);
            for (int i = 0; i < buffer.Length; i++)
            {
                Same(sentinel[i], buffer[i]);
            }
        }

        [Test]
        public void MissingOrShortStorageAndDefaultProfiles_AreRejected()
        {
            var ground = Ground(Mixed);
            Assert.IsFalse(LobsterPotComposition.TryCompose(1, 1200, ground, 600, Settings, null, out _));
            var shortBuffer = new LobsterPotAnimal[31];
            Assert.IsFalse(LobsterPotComposition.TryCompose(1, 1200, ground, 600, Settings, shortBuffer, out _));
            foreach (var a in shortBuffer) Assert.AreEqual(0, a.CarapaceMm);
            Reject(default, Settings, 1200, 600);
            Reject(ground, default, 1200, 600);
        }

        [Test]
        public void SingleMillimetreBandsAndOversizedBuffer_RespectGaugeAndUntouchedSuffix()
        {
            var ground = new LobsterPotGround("ground.narrow", 1, 79, 80, 80, 79,
                new LobsterPotSeason(1, 1, 1), new LobsterPotSeason(1, 1, 1), Mixed, Mixed);
            var buffer = new LobsterPotAnimal[2];
            Assert.AreEqual(1, Compose(1, 1200, 600, ground, buffer).Count);
            Assert.AreEqual(79, buffer[0].CarapaceMm);
            Assert.AreEqual(0, buffer[1].CarapaceMm);
            ground = new LobsterPotGround("ground.narrow", 1, 79, 80, 80, 79,
                new LobsterPotSeason(1, 0, 0), new LobsterPotSeason(1, 0, 0), Mixed, Mixed);
            Compose(1, 1200, 600, ground, buffer);
            Assert.AreEqual(80, buffer[0].CarapaceMm);
            Assert.IsFalse(buffer[0].IsShort);
            Assert.AreEqual(0, buffer[1].CarapaceMm);
        }

        [Test]
        public void FiniteExtremeClockScale_DoesNotOverflowCalendarPhase()
        {
            var settings = new LobsterPotSettings(2e307, 2, 7, 1);
            var summary = Compose(1, 1.5e308, 1, Ground(new LobsterPotSeason(1, 0, 0)),
                new LobsterPotAnimal[32], settings);
            Assert.AreEqual(Season.HardWinter, summary.Season);
            Assert.AreEqual(32, summary.Count);
        }

        [Test]
        public void HotPath_AllocatesZeroBytes_WhenCounterPassesPositiveControl()
        {
            GC.GetAllocatedBytesForCurrentThread();
            long baseline = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096];
            long positiveBytes = GC.GetAllocatedBytesForCurrentThread() - baseline;
            GC.KeepAlive(probe);
            if (positiveBytes < 4096)
                Assert.Ignore("Allocation counter failed positive control; zero bytes would not be proof.");
            var ground = Ground(Mixed);
            var buffer = new LobsterPotAnimal[32];
            int checksum = 0;
            bool success = true;
            for (int i = 0; i < 100; i++)
                success &= LobsterPotComposition.TryCompose(i, 1200, ground, 600, Settings, buffer, out _);
            baseline = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
            {
                success &= LobsterPotComposition.TryCompose(i, 1200, ground, 600, Settings, buffer, out var summary);
                checksum += summary.Count;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - baseline;
            Assert.IsTrue(success);
            Assert.That(checksum, Is.InRange(90000, 110000));
            Assert.AreEqual(0, allocated);
        }
    }
}
