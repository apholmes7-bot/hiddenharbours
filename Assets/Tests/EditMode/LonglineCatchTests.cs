using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    public class LonglineCatchTests
    {
        private const double Hour = 75d;

        [SetUp] public void SetUp() => GameServices.Reset();
        [TearDown] public void TearDown() => GameServices.Reset();
        [Test]
        public void SameLineAndPerHookHaulMoments_AreBitIdentical()
        {
            using var rig = new LonglineTestRig();
            var pool = new[] { rig.Fish("fish.fixture_a"), rig.Fish("fish.fixture_b") };
            var line = rig.Line();
            for (int i = 0; i < 30; i++)
            {
                double haul = (6d * Hour) + i * (Hour / 60d);
                var first = LonglineCatch.ResolveHook(rig.Def, in line, i, haul, 4242, pool, null,
                    rig.Environment, rig.Terrain, (p, t) => new CatchContext(line.Region, 0f,
                        (float)(t / Hour), Season.HighSummer, Gear.Handline), in rig.Depth);
                var second = LonglineCatch.ResolveHook(rig.Def, in line, i, haul, 4242, pool, null,
                    rig.Environment, rig.Terrain, (p, t) => new CatchContext(line.Region, 0f,
                        (float)(t / Hour), Season.HighSummer, Gear.Handline), in rig.Depth);
                Same(first, second);
            }
        }

        [Test]
        public void SaveRoundTrip_RecomputesTheSameHooksAndSizes()
        {
            using var rig = new LonglineTestRig();
            var pool = new[] { rig.Fish("fish.fixture_a"), rig.Fish("fish.fixture_b") };
            var line = rig.Line(1234.125d);
            line.HooksHauledFromA = 2;
            line.HooksHauledFromB = 3;
            var save = SaveMigration.NewGame();
            save.PlacedLonglines.Add(line);
            save.TubStock.Add(new TubStock(rig.Def.Id, 5));
            var restored = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.AreEqual(2, restored.PlacedLonglines[0].HooksHauledFromA);
            Assert.AreEqual(3, restored.PlacedLonglines[0].HooksHauledFromB);
            var before = rig.Haul(line, line.SetGameTimeSeconds + 8d * Hour, pool);
            var after = rig.Haul(restored.PlacedLonglines[0], line.SetGameTimeSeconds + 8d * Hour, pool);
            for (int i = 0; i < 30; i++) Same(before[i], after[i]);
        }

        [Test]
        public void BitesRiseThenStripPastTheirHours_AndFishPeakThenFall()
        {
            using var rig = new LonglineTestRig();
            var pool = new[] { rig.Fish("fish.fixture") };
            var line = rig.Line();
            var early = rig.Haul(line, (2d * Hour), pool);
            var peak = rig.Haul(line, (6d * Hour), pool);
            var oldestStillSafe = rig.Haul(line, (12d * Hour), pool);
            var late = rig.Haul(line, (18d * Hour), pool);
            Assert.AreEqual(0, FishCount(rig.Haul(line, 0d, pool)));
            Assert.Greater(FishCount(early), 0);
            Assert.Less(FishCount(early), 30);
            Assert.AreEqual(30, FishCount(peak));
            Assert.AreEqual(30, FishCount(oldestStillSafe));
            Assert.AreEqual(0, FishCount(late));
            Assert.AreEqual(30, late.Count(x => x.State == LonglineHookState.Stripped));
            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual(peak[i].BiteGameTimeSeconds, late[i].BiteGameTimeSeconds);
                Assert.Greater((18d * Hour) - late[i].BiteGameTimeSeconds, (12d * Hour));
                if (early[i].State == LonglineHookState.Fish)
                    Assert.AreEqual(early[i].BiteGameTimeSeconds, peak[i].BiteGameTimeSeconds);
            }
        }

        [Test]
        public void BaitLeansSpecies_NeverTheFishCount()
        {
            using var rig = new LonglineTestRig();
            var pool = new[] { rig.Fish("fish.fixture_a"), rig.Fish("fish.fixture_b") };
            int neutralA = 0, baitedA = 0;
            for (int set = 0; set < 50; set++)
            {
                var line = rig.Line();
                line.InstanceId = "line.fixture_" + set;
                var neutral = rig.Haul(line, (6d * Hour), pool);
                var baited = rig.Haul(line, (6d * Hour), pool, new[] { "fish.fixture_a" });
                Assert.AreEqual(30, FishCount(neutral));
                Assert.AreEqual(30, FishCount(baited));
                neutralA += neutral.Count(x => x.Catch.Value.SpeciesId == "fish.fixture_a");
                baitedA += baited.Count(x => x.Catch.Value.SpeciesId == "fish.fixture_a");
            }
            Assert.Greater(baitedA, neutralA);
            Assert.Less(baitedA, 1500, "the unfavoured species must remain possible");
        }

        [Test]
        public void SpeciesOutsideDepthBand_NeverBites_AndDepthUsesTheBiteMoment()
        {
            using var rig = new LonglineTestRig();
            var deep = rig.Fish("fish.deep_fixture", FishDepthBand.Deep);
            var inshore = rig.Fish("fish.inshore_fixture", FishDepthBand.Inshore);
            rig.Environment.LevelAt = time => time >= (6d * Hour) ? 100f : 0f;
            Assert.AreEqual(0, FishCount(rig.Haul(rig.Line(), (6d * Hour), new[] { deep })));
            var caught = rig.Haul(rig.Line(), (6d * Hour), new[] { deep, inshore });
            Assert.AreEqual(30, FishCount(caught), "all bite in 10 m before the haul's water level changes");
            Assert.IsTrue(caught.All(x => x.Catch.Value.SpeciesId == "fish.inshore_fixture"));
        }

        [Test]
        public void DawnOnlySpecies_BitesAcrossDawn_NeverInsideNight()
        {
            using var rig = new LonglineTestRig();
            var fish = rig.Fish("fish.dawn_fixture");
            fish.StartHour = 6f;
            fish.EndHour = 7f;
            var across = rig.Haul(rig.Line((5d * Hour)), (11d * Hour), new[] { fish });
            Assert.Greater(FishCount(across), 0);
            foreach (var hook in across.Where(x => x.State == LonglineHookState.Fish))
            {
                Assert.GreaterOrEqual(hook.BiteGameTimeSeconds, (6d * Hour));
                Assert.LessOrEqual(hook.BiteGameTimeSeconds, (7d * Hour));
            }
            Assert.IsTrue(rig.Haul(rig.Line((19d * Hour)), (24d * Hour), new[] { fish })
                .All(x => x.State == LonglineHookState.Empty));
        }

        [Test]
        public void AuthoredPollockAndMackerel_NeverComeUpOnLongline()
        {
            using var rig = new LonglineTestRig();
            var pool = new[] { "Pollock", "Mackerel" }.Select(name =>
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>("Assets/_Project/Data/Fish/" + name + ".asset")).ToArray();
            foreach (var fish in pool)
            {
                Assert.IsNotNull(fish);
                Assert.IsFalse(fish.GearAllowed(Gear.Longline));
                Assert.IsTrue(fish.GearAllowed(Gear.Handline));
            }
            var line = rig.Line((6d * Hour));
            line.Region = "region.st_peters";
            Assert.IsTrue(rig.Haul(line, (12d * Hour), pool).All(x => x.State == LonglineHookState.Empty));
        }

        private static int FishCount(LonglineHookResult[] hooks) => hooks.Count(x => x.State == LonglineHookState.Fish);

        private static void Same(LonglineHookResult a, LonglineHookResult b)
        {
            Assert.AreEqual(a.State, b.State);
            Assert.AreEqual(a.BiteGameTimeSeconds, b.BiteGameTimeSeconds);
            Assert.AreEqual(a.Catch.HasValue, b.Catch.HasValue);
            if (!a.Catch.HasValue) return;
            Assert.AreEqual(a.Catch.Value.SpeciesId, b.Catch.Value.SpeciesId);
            Assert.AreEqual(a.Catch.Value.WeightKg, b.Catch.Value.WeightKg);
        }
    }
}
