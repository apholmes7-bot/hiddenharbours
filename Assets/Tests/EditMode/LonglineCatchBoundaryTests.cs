using System;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class LonglineCatchBoundaryTests
    {
        [SetUp] public void SetUp() => GameServices.Reset();
        [TearDown] public void TearDown() => GameServices.Reset();

        private static CatchContext Context() => new CatchContext("region.fixture", 0f, 12f,
            Season.HighSummer, Gear.Handline);

        [Test]
        public void Hook_IsEmptyBeforeItsBite_FishAtBite_AndStripsOnlyAfterTheExactDeadline()
        {
            using var rig = new LonglineTestRig();
            var line = rig.Line(1234.125d);
            var pool = new[] { rig.Fish("fish.fixture") };
            var peak = LonglineCatch.ResolveHook(rig.Def, in line, 0,
                line.SetGameTimeSeconds + TrapSoak.SoakSeconds(rig.Def.SoakHoursToPeak), 4242, pool, null,
                rig.Environment, rig.Terrain, (p, t) => Context(), in rig.Depth);
            Assert.AreEqual(LonglineHookState.Fish, peak.State);
            double bite = peak.BiteGameTimeSeconds;
            double deadline = bite + TrapSoak.SoakSeconds(rig.Def.StripHours);
            var before = LonglineCatch.ResolveHook(rig.Def, in line, 0, bite - 0.001d, 4242, pool, null,
                rig.Environment, rig.Terrain, (p, t) => Context(), in rig.Depth);
            Assert.AreEqual(LonglineHookState.Empty, before.State);
            Assert.IsNull(before.Catch);
            Assert.IsTrue(double.IsNaN(before.BiteGameTimeSeconds));
            foreach (double time in new[] { bite, deadline })
            {
                var result = LonglineCatch.ResolveHook(rig.Def, in line, 0, time, 4242, pool, null,
                    rig.Environment, rig.Terrain, (p, t) => Context(), in rig.Depth);
                Assert.AreEqual(LonglineHookState.Fish, result.State);
                Assert.AreEqual(bite, result.BiteGameTimeSeconds);
                Assert.AreEqual(peak.Catch.Value.WeightKg, result.Catch.Value.WeightKg);
            }
            var stripped = LonglineCatch.ResolveHook(rig.Def, in line, 0, deadline + 0.001d, 4242, pool, null,
                rig.Environment, rig.Terrain, (p, t) => Context(), in rig.Depth);
            Assert.AreEqual(LonglineHookState.Stripped, stripped.State);
            Assert.AreEqual(bite, stripped.BiteGameTimeSeconds);
            Assert.IsNull(stripped.Catch);
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidHaulTime_RefusesBeforeSamplingTheWorld(double haul)
        {
            using var rig = new LonglineTestRig();
            var line = rig.Line();
            var pool = new[] { rig.Fish("fish.fixture") };
            rig.Environment.LevelAt = t => throw new AssertionException("refused hook sampled water");
            var result = LonglineCatch.ResolveHook(rig.Def, in line, 0, haul, 4242, pool, null,
                rig.Environment, rig.Terrain, (p, t) => throw new AssertionException("refused hook sampled context"), in rig.Depth);
            Assert.AreEqual(LonglineHookState.Empty, result.State);
            Assert.IsNull(result.Catch);
            Assert.IsTrue(double.IsNaN(result.BiteGameTimeSeconds));
        }

        [TestCase(-1)]
        [TestCase(30)]
        public void InvalidHookIndex_OnAnOtherwiseValidLine_Throws(int index)
        {
            using var rig = new LonglineTestRig();
            var line = rig.Line();
            var pool = new[] { rig.Fish("fish.fixture") };
            Assert.Throws<ArgumentOutOfRangeException>(() => LonglineCatch.ResolveHook(rig.Def, in line, index,
                100d, 4242, pool, null, rig.Environment, rig.Terrain, (p, t) => Context(), in rig.Depth));
        }

        [Test]
        public void BiteChance_ClampsAtSetAndPeak_AndInvalidDefReadsZero()
        {
            using var rig = new LonglineTestRig();
            double span = TrapSoak.SoakSeconds(rig.Def.SoakHoursToPeak);
            Assert.AreEqual(0f, LonglineCatch.BiteChance01(rig.Def, 100d, 99d));
            Assert.AreEqual(0f, LonglineCatch.BiteChance01(rig.Def, 100d, 100d));
            Assert.AreEqual(0.5f, LonglineCatch.BiteChance01(rig.Def, 100d, 100d + span / 2d));
            Assert.AreEqual(1f, LonglineCatch.BiteChance01(rig.Def, 100d, 100d + span));
            Assert.AreEqual(1f, LonglineCatch.BiteChance01(rig.Def, 100d, 100d + span * 2d));
            Assert.AreEqual(0f, LonglineCatch.BiteChance01(null, 0d, span));
            rig.Def.SoakHoursToPeak = 0f;
            Assert.AreEqual(0f, LonglineCatch.BiteChance01(rig.Def, 0d, span));
        }
    }
}
