using System;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class BottomTypeAndTrapRotationTests
    {
        private sealed class SplitMap : IBottomMap
        {
            public BottomType BottomAt(Vector2 p) => p.y < -50f ? BottomType.Unknown : p.x < 0f ? BottomType.Mud : BottomType.Rock;
        }
        private sealed class Terrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.y > 50f ? 1f : p.y > 30f ? -100f : -5f;
        }
        private static readonly IBottomMap Bottom = new SplitMap();
        private static readonly ITidalTerrain Bed = new Terrain();
        private static readonly Rect Region = new Rect(-100f, -100f, 200f, 200f);
        private static object _allocationSink;

        private static BottomMapSettings Geology => new BottomMapSettings
        { CellMetres = 16f, Salt = 91, RockWeight = 1f, MudWeight = 1f, SandWeight = 1f };
        private static TrapHabitatSettings Lobster => new TrapHabitatSettings
        {
            SpeciesId = "fish.lobster", RockAffinity01 = 1f, MudAffinity01 = 0f, SandAffinity01 = 0.25f,
            RadiusMetres = 20f, OffCentreDensity01 = 1f, PeriodGameSeconds = 1000d
        };
        private static TrapRotationSettings Rotation => new TrapRotationSettings
        {
            PoorCatchPerGameHour = 1f, MinimumDensityGain01 = 0.1f, MinSpacingMetres = 2f,
            MaxMoveMetres = 100f, MinDepthMetres = 1f, MaxDepthMetres = 20f
        };
        private static TrapObservation Poor(string id = "pot.a", float x = -5f)
            => new TrapObservation(id, new Vector2(x, 0f), 0f, true);
        private static int Plan(TrapObservation[] traps, Vector2[] candidates, TrapRotationOrder[] orders)
        { var h = Lobster; var s = Rotation; return TrapRotationPlanner.Plan(traps, candidates, Region, Bed, 0f, Bottom, in h, 42, 100d, in s, orders); }

        [Test]
        public void Determinism_BottomIsRepeatableAcrossInstancesAndQueryOrder()
        {
            var s = Geology; var a = new DeterministicBottomMap(713, in s); var b = new DeterministicBottomMap(713, in s);
            var expected = new BottomType[121]; int n = 0;
            for (int y = -5; y <= 5; y++) for (int x = -5; x <= 5; x++) expected[n++] = a.BottomAt(new Vector2(x * 19f, y * 17f));
            for (int i = n - 1; i >= 0; i--)
                Assert.AreEqual(expected[i], b.BottomAt(new Vector2((i % 11 - 5) * 19f, (i / 11 - 5) * 17f)));
            CollectionAssert.AreEquivalent(new[] { BottomType.Rock, BottomType.Mud, BottomType.Sand }, Unique(expected));
        }
        private static System.Collections.Generic.HashSet<BottomType> Unique(BottomType[] values)
            => new System.Collections.Generic.HashSet<BottomType>(values);

        [Test]
        public void Determinism_SeedChangesTheMap()
        {
            var s = Geology; var a = new DeterministicBottomMap(1, in s); var b = new DeterministicBottomMap(2, in s); int differences = 0;
            for (int i = 0; i < 128; i++) if (a.BottomAt(new Vector2(i * 17, 0)) != b.BottomAt(new Vector2(i * 17, 0))) differences++;
            Assert.Greater(differences, 40);
        }

        [Test]
        public void BottomMap_CopiesDefTuningAtSetup()
        {
            var d = new BottomMapDef { Id = "bottom_map.test", Settings = Geology }; var map = d.CreateMap(42);
            BottomType expected = map.BottomAt(Vector2.zero); d.Settings = default;
            Assert.AreEqual(expected, map.BottomAt(Vector2.zero));
            Assert.Throws<ArgumentException>(() => new BottomMapDef().CreateMap(42));
        }

        [Test]
        public void BottomMap_UsesFloorForNegativeCellsAndHonoursOrigin()
        {
            var s = Geology; var map = new DeterministicBottomMap(42, in s);
            Assert.AreEqual(map.BottomAt(new Vector2(-0.001f, -0.001f)), map.BottomAt(new Vector2(-15.9f, -15.9f)));
            s.Origin = new Vector2(64f, 64f); var shifted = new DeterministicBottomMap(42, in s);
            for (int i = -10; i <= 10; i++)
                Assert.AreEqual(map.BottomAt(new Vector2(i * 16f, 0f)), shifted.BottomAt(new Vector2(i * 16f + 64f, 64f)));
        }

        [TestCase(BottomType.Rock)]
        [TestCase(BottomType.Mud)]
        [TestCase(BottomType.Sand)]
        public void BottomMap_OneNonzeroWeightProducesOnlyThatBottom(BottomType type)
        {
            var s = Geology; s.RockWeight = type == BottomType.Rock ? 1f : 0f;
            s.MudWeight = type == BottomType.Mud ? 1f : 0f; s.SandWeight = type == BottomType.Sand ? 1f : 0f;
            var map = new DeterministicBottomMap(52, in s);
            for (int i = -64; i <= 64; i++) Assert.AreEqual(type, map.BottomAt(new Vector2(i * 23f, i * 13f)));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void BottomMap_NonfinitePositionsAreUnknown(float v)
        {
            var s = Geology; var map = new DeterministicBottomMap(42, in s);
            Assert.AreEqual(BottomType.Unknown, map.BottomAt(new Vector2(v, 0f)));
            Assert.AreEqual(BottomType.Unknown, map.BottomAt(new Vector2(0f, v)));
        }

        [Test]
        public void BottomMap_RejectsInvalidTuningAndOutOfRangeCells()
        {
            Assert.Throws<ArgumentException>(() => new DeterministicBottomMap(1, default));
            var s = Geology; s.RockWeight = -1f; Assert.IsFalse(s.IsValid);
            s = Geology; s.CellMetres = float.NaN; Assert.IsFalse(s.IsValid);
            s = Geology; s.MudWeight = float.PositiveInfinity; Assert.IsFalse(s.IsValid);
            s = Geology; s.CellMetres = float.Epsilon; var map = new DeterministicBottomMap(1, in s);
            Assert.AreEqual(BottomType.Unknown, map.BottomAt(new Vector2(float.MaxValue, 0f)));
        }

        [Test]
        public void Habitat_LobsterOnRockOutfishesMud_AndCrabOnMudOutfishesRock_ThroughTrapFill()
        {
            var lobster = Lobster; var crab = Lobster; crab.SpeciesId = "fish.nmc_mud_crab_proposal";
            crab.MudAffinity01 = 1f; crab.RockAffinity01 = 0f;
            int lobsterRock = HabitatTrapCatch.Count(1f, 64, Bottom, new Vector2(5f, 0f), in lobster, 42, "pot.l", 0d);
            int lobsterMud = HabitatTrapCatch.Count(1f, 64, Bottom, new Vector2(-5f, 0f), in lobster, 42, "pot.l", 0d);
            int crabRock = HabitatTrapCatch.Count(1f, 64, Bottom, new Vector2(5f, 0f), in crab, 42, "pot.c", 0d);
            int crabMud = HabitatTrapCatch.Count(1f, 64, Bottom, new Vector2(-5f, 0f), in crab, 42, "pot.c", 0d);
            Assert.AreEqual(64, lobsterRock); Assert.AreEqual(1, lobsterMud);
            Assert.AreEqual(1, crabRock); Assert.AreEqual(64, crabMud);
        }

        [Test]
        public void Habitat_FillPreservesReadyFloorCapacityAndMonotonicSlots()
        {
            var h = Lobster; h.RockAffinity01 = 0.6f; int previous = 0;
            for (int i = 0; i <= 100; i++)
            {
                int count = HabitatTrapCatch.Count(i / 100f, 32, Bottom, Vector2.zero, in h, 42, "pot.a", 2d);
                Assert.GreaterOrEqual(count, previous); Assert.LessOrEqual(count, 32); previous = count;
            }
            Assert.AreEqual(1, HabitatTrapCatch.Count(0f, 32, Bottom, Vector2.zero, in h, 42, "pot.a", 2d));
        }

        [Test]
        public void Habitat_DeterminismMatchesUnmodifiedFillForNeutralDensity()
        {
            var h = Lobster;
            for (int i = 0; i <= 20; i++)
                Assert.AreEqual(TrapFill.ResolveCount(i / 20f, 32, 42, "pot.a", 2d),
                    HabitatTrapCatch.Count(i / 20f, 32, Bottom, Vector2.zero, in h, 42, "pot.a", 2d));
        }

        [Test]
        public void Habitat_MigrationMovesBestGroundOverASeason_WhileBottomStaysFixed()
        {
            var h = Lobster; h.MigrationVector = new Vector2(80f, 0f); h.OffCentreDensity01 = 0.05f;
            var s = Geology; s.MudWeight = s.SandWeight = 0f; var map = new DeterministicBottomMap(42, in s);
            Vector2 a = TrapHabitat.CentreAt(in h, 42, 0d), b = TrapHabitat.CentreAt(in h, 42, 500d);
            double ta = 0d, tb = 500d;
            if (Math.Abs(a.x - b.x) < 80f) { ta = 250d; tb = 750d; a = TrapHabitat.CentreAt(in h, 42, ta); b = TrapHabitat.CentreAt(in h, 42, tb); }
            Assert.Greater(Math.Abs(a.x - b.x), 80f);
            Assert.AreEqual(1f, TrapHabitat.DensityAt(map, a, in h, 42, ta));
            Assert.AreEqual(0.05f, TrapHabitat.DensityAt(map, b, in h, 42, ta));
            Assert.AreEqual(0.05f, TrapHabitat.DensityAt(map, a, in h, 42, tb));
            Assert.AreEqual(1f, TrapHabitat.DensityAt(map, b, in h, 42, tb));
            Assert.AreEqual(BottomType.Rock, map.BottomAt(a)); Assert.AreEqual(BottomType.Rock, map.BottomAt(b));
        }

        [Test]
        public void Habitat_DensityAndMigrationArePeriodicRepeatableAndSpeciesSeeded()
        {
            var h = Lobster; h.MigrationVector = new Vector2(50f, 20f); h.OffCentreDensity01 = 0.1f;
            Assert.AreEqual(TrapHabitat.CentreAt(in h, 3, 123d), TrapHabitat.CentreAt(in h, 3, 1123d));
            float a = TrapHabitat.DensityAt(Bottom, Vector2.zero, in h, 3, 123d);
            Assert.AreEqual(a, TrapHabitat.DensityAt(Bottom, Vector2.zero, in h, 3, 1123d));
            Vector2 first = TrapHabitat.CentreAt(in h, 3, 123d); h.SpeciesId = "fish.crab.proposal";
            Assert.AreNotEqual(first, TrapHabitat.CentreAt(in h, 3, 123d));
        }

        [Test]
        public void Habitat_InvalidDataUnknownBottomAndInvalidTimeAreNotHabitat()
        {
            var h = Lobster;
            Assert.AreEqual(0f, TrapHabitat.DensityAt(null, Vector2.zero, in h, 1, 0d));
            Assert.AreEqual(0f, TrapHabitat.DensityAt(Bottom, new Vector2(1f, -60f), in h, 1, 0d));
            Assert.AreEqual(0f, TrapHabitat.DensityAt(Bottom, Vector2.zero, in h, 1, double.NaN));
            Assert.AreEqual(0f, TrapHabitat.DensityAt(Bottom, Vector2.zero, in h, 1, -1d));
            h.RockAffinity01 = 1.1f; Assert.IsFalse(h.IsValid);
            h = Lobster; h.PeriodGameSeconds = 0d; Assert.IsFalse(h.IsValid);
            h = Lobster; h.Phase01 = 1d; Assert.IsFalse(h.IsValid);
        }

        [Test]
        public void Habitat_InvalidFillFactsNeverYieldAnimals()
        {
            var h = Lobster;
            Assert.AreEqual(0, HabitatTrapCatch.Count(float.NaN, 4, Bottom, Vector2.zero, in h, 1, "pot", 0d));
            Assert.AreEqual(0, HabitatTrapCatch.Count(1f, 0, Bottom, Vector2.zero, in h, 1, "pot", 0d));
            Assert.AreEqual(0, HabitatTrapCatch.Count(1f, 4, Bottom, Vector2.zero, in h, 1, "", 0d));
            Assert.AreEqual(0, HabitatTrapCatch.Count(1f, 4, Bottom, Vector2.zero, in h, 1, "pot", -1d));
            Assert.AreEqual(0, HabitatTrapCatch.Count(1f, 4, Bottom, new Vector2(1f, -60f), in h, 1, "pot", 0d));
        }

        [Test]
        public void Planner_MovesPoorTrapsTowardBetterBottom_WithoutMutatingPlacement()
        {
            var traps = new[] { Poor() }; var candidates = new[] { new Vector2(5f, 0f) }; var output = new TrapRotationOrder[1];
            Assert.AreEqual(1, Plan(traps, candidates, output));
            Assert.AreEqual("pot.a", output[0].InstanceId); Assert.AreEqual(traps[0].Position, output[0].From);
            Assert.AreEqual(candidates[0], output[0].ResetAt); Assert.AreEqual(1f, output[0].ExpectedDensityGain01);
            Assert.AreEqual(new Vector2(-5f, 0f), traps[0].Position);
        }

        [Test]
        public void Planner_NeverResetsOnLandOutsideBoundsUnknownBottomOrWrongDepth()
        {
            var output = new TrapRotationOrder[4]; var candidates = new[]
            { new Vector2(5f, 60f), new Vector2(101f, 0f), new Vector2(5f, -60f), new Vector2(5f, 40f), new Vector2(float.NaN, 0f) };
            Assert.AreEqual(0, Plan(new[] { Poor() }, candidates, output));
            Assert.AreEqual(1, Plan(new[] { Poor() }, new[] { new Vector2(5f, 0f) }, output));
        }

        [Test]
        public void Planner_RegionBoundsRejectOtherwiseReachableGoodBottom()
        {
            var output = new TrapRotationOrder[1]; var h = Lobster; var r = Rotation;
            Assert.AreEqual(0, TrapRotationPlanner.Plan(new[] { Poor() }, new[] { new Vector2(15f, 0f) },
                new Rect(-10f, -10f, 20f, 20f), Bed, 0f, Bottom, in h, 42, 0d, in r, output));
        }

        [Test]
        public void Planner_KeepsGoodTrapsAndTrapsWithoutValidHistory()
        {
            var output = new TrapRotationOrder[4];
            var traps = new[] { new TrapObservation("good", new Vector2(-5f, 0f), 1f, true),
                new TrapObservation("unknown", new Vector2(-10f, 0f), 0f, false),
                new TrapObservation("invalid", new Vector2(-15f, 0f), float.NaN, true),
                new TrapObservation("negative", new Vector2(-20f, 0f), -1f, true) };
            Assert.AreEqual(0, Plan(traps, new[] { Vector2.zero }, output));
        }

        [Test]
        public void Planner_StrictImprovementAndMinimumGainPreventChurn()
        {
            var output = new TrapRotationOrder[1];
            Assert.AreEqual(0, Plan(new[] { Poor("pot", 5f) }, new[] { new Vector2(10f, 0f) }, output));
            var h = Lobster; h.MudAffinity01 = 0.95f; var r = Rotation;
            Assert.AreEqual(0, TrapRotationPlanner.Plan(new[] { Poor() }, new[] { Vector2.zero }, Region, Bed, 0f, Bottom, in h, 42, 0d, in r, output));
        }

        [Test]
        public void Planner_DeterminismUsesStableCandidateOrderForEqualScores()
        {
            var traps = new[] { Poor() }; var points = new[] { new Vector2(10f, 0f), new Vector2(5f, 0f) };
            var a = new TrapRotationOrder[1]; var b = new TrapRotationOrder[1];
            Assert.AreEqual(1, Plan(traps, points, a)); Assert.AreEqual(1, Plan(traps, points, b));
            Assert.AreEqual(points[0], a[0].ResetAt); Assert.AreEqual(a[0].ResetAt, b[0].ResetAt);
        }

        [Test]
        public void Planner_NeverOverlapsOccupiedTrapsOrEarlierResetOrders()
        {
            var traps = new[] { Poor("a", -10f), Poor("b", -20f), new TrapObservation("kept", new Vector2(5f, 0f), 5f, true) };
            var points = new[] { new Vector2(5f, 0f), new Vector2(6f, 0f), new Vector2(10f, 0f), new Vector2(11f, 0f), new Vector2(15f, 0f) };
            var output = new TrapRotationOrder[3]; Assert.AreEqual(2, Plan(traps, points, output));
            Assert.AreEqual(points[2], output[0].ResetAt); Assert.AreEqual(points[4], output[1].ResetAt);
        }

        [Test]
        public void Planner_RespectsMaximumTravelAndOutputCapacity()
        {
            var output = new TrapRotationOrder[1];
            Assert.AreEqual(0, Plan(new[] { Poor("far", -99f) }, new[] { new Vector2(99f, 0f) }, output));
            Assert.AreEqual(0, Plan(new[] { Poor() }, new[] { Vector2.zero }, Array.Empty<TrapRotationOrder>()));
            Assert.AreEqual(1, Plan(new[] { Poor("a", -10f), Poor("b", -20f) }, new[] { Vector2.zero, new Vector2(5f, 0f) }, output));
            Assert.AreEqual("a", output[0].InstanceId);
        }

        [Test]
        public void Planner_DuplicateIdsProduceAtMostOneOrder()
        {
            var output = new TrapRotationOrder[2];
            Assert.AreEqual(1, Plan(new[] { Poor("a", -10f), Poor("a", -20f) }, new[] { Vector2.zero, new Vector2(5f, 0f) }, output));
        }

        [Test]
        public void Planner_InvalidSettingsInputsAndMissingTerrainFailClosed()
        {
            var traps = new[] { Poor() }; var points = new[] { Vector2.zero }; var output = new TrapRotationOrder[1]; var h = Lobster; var r = Rotation;
            Assert.AreEqual(0, TrapRotationPlanner.Plan(traps, points, Region, null, 0f, Bottom, in h, 42, 0d, in r, output));
            Assert.AreEqual(0, TrapRotationPlanner.Plan(traps, points, default, Bed, 0f, Bottom, in h, 42, 0d, in r, output));
            Assert.AreEqual(0, TrapRotationPlanner.Plan(traps, points, Region, Bed, float.NaN, Bottom, in h, 42, 0d, in r, output));
            Assert.AreEqual(0, TrapRotationPlanner.Plan(traps, points, Region, Bed, 0f, Bottom, in h, 42, double.PositiveInfinity, in r, output));
            r.MinSpacingMetres = 0f; Assert.IsFalse(r.IsValid);
            Assert.AreEqual(0, TrapRotationPlanner.Plan(traps, points, Region, Bed, 0f, Bottom, in h, 42, 0d, in r, output));
        }

        [Test]
        public void SlowTick_ZeroAllocations_RequiresSuccessfulPlansAndCalibratedCounter()
        {
            var traps = new[] { Poor() }; var points = new[] { new Vector2(5f, 0f), new Vector2(6f, 0f) }; var output = new TrapRotationOrder[1];
            var geology = Geology; var map = new DeterministicBottomMap(42, in geology); var h = Lobster;
            for (int i = 0; i < 512; i++) { Plan(traps, points, output); map.BottomAt(points[0]); HabitatTrapCatch.Count(0.5f, 32, Bottom, Vector2.zero, in h, 42, "pot", 0d); }
            long probeStart = GC.GetAllocatedBytesForCurrentThread(); _allocationSink = new byte[1024];
            long probe = GC.GetAllocatedBytesForCurrentThread() - probeStart; GC.KeepAlive(_allocationSink);
            int successful = 0, animals = 0, bottomChecksum = 0;
            if (probe > 0)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 4096; i++)
                { successful += Plan(traps, points, output); bottomChecksum += (int)map.BottomAt(points[0]); animals += HabitatTrapCatch.Count(0.5f, 32, Bottom, Vector2.zero, in h, 42, "pot", 0d); }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocated);
            }
            else
            {
                using (var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 8192, ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    Assert.IsTrue(recorder.Valid); recorder.Start(); recorder.Stop(); recorder.Reset();
                    recorder.Start(); _allocationSink = new byte[1024]; recorder.Stop();
                    Assert.Greater(recorder.Count, 0, "The recorder must detect the positive control."); GC.KeepAlive(_allocationSink);
                    recorder.Reset(); recorder.Start(); recorder.Stop(); Assert.AreEqual(0, recorder.Count, "Empty scope must be allocation-free.");
                    recorder.Reset(); recorder.Start();
                    for (int i = 0; i < 4096; i++)
                    { successful += Plan(traps, points, output); bottomChecksum += (int)map.BottomAt(points[0]); animals += HabitatTrapCatch.Count(0.5f, 32, Bottom, Vector2.zero, in h, 42, "pot", 0d); }
                    recorder.Stop(); Assert.AreEqual(0, recorder.Count);
                }
            }
            Assert.AreEqual(4096, successful); Assert.Greater(animals, 4096); Assert.Greater(bottomChecksum, 0);
        }
    }
}
