using HiddenHarbours.Tests.Support;
using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    internal static class SchoolFieldRegressionData
    {
        internal const string Region = "region.field_regression";
        internal const double SecondsPerDay = 1800d;
        internal const double CellSize = 100d;
        internal static readonly SchoolFieldEnvironment Water = new SchoolFieldEnvironment(0f, 0.5f);

        internal static FishSpeciesDef Species(string id, LivingFishKind kind, float density = 1000f)
        {
            var fish = ScriptableObject.CreateInstance<FishSpeciesDef>();
            fish.Id = id;
            fish.RegionIds = new[] { Region };
            fish.MinSchoolMarks = 3;
            fish.MaxSchoolMarks = 7;
            fish.SchoolsPerSquareKilometre = density;
            fish.LivingBehaviour = new LivingFishBehaviour
            {
                Enabled = true, Kind = kind, AllowedHabitats = FishHabitat.Rock,
                PreferredVegetation = FishVegetation.Kelp, VegetationPreferenceMultiplier = 1.5f,
                MinDepthMetres = 2f, MaxDepthMetres = 8f, BedClearanceMetres = 1f,
                PatrolRadiusMetres = 6f, PatrolMetresPerGameSecond = 0.3f,
                NormalFootprintMetres = 2f, ScatteredFootprintMetres = 5f,
                ScatterDistanceMetres = 4f, ReactionDurationGameSeconds = 12d,
                DailyDepthStrength01 = 0.6f, TidalDepthStrength01 = 0.4f
            };
            return fish;
        }

        internal static SchoolHabitatPatch Patch(int x, int y)
            => new SchoolHabitatPatch(new Vector2(x * 100f + 50f, y * 100f + 50f), 40f, -20f,
                                      FishHabitat.Rock, FishVegetation.Kelp, true);

        internal static SchoolField Field(int seed) => new SchoolField(seed, Region, SecondsPerDay, CellSize);

        internal static void Same(in SchoolFieldSample a, in SchoolFieldSample b, string context)
        {
            Assert.AreEqual(a.SpeciesId, b.SpeciesId, context);
            Assert.AreEqual(a.Key, b.Key, context);
            // Compare bits, never Vector2's approximate operator or a floating-point tolerance.
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.Centre.x), BitConverter.SingleToInt32Bits(b.Centre.x), context);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.Centre.y), BitConverter.SingleToInt32Bits(b.Centre.y), context);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.DepthMetres), BitConverter.SingleToInt32Bits(b.DepthMetres), context);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.FootprintMetres), BitConverter.SingleToInt32Bits(b.FootprintMetres), context);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.Tightness01), BitConverter.SingleToInt32Bits(b.Tightness01), context);
            Assert.AreEqual(a.Individuals, b.Individuals, context);
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(a.MaxPlanarMetresPerGameSecond),
                            BitConverter.DoubleToInt64Bits(b.MaxPlanarMetresPerGameSecond), context);
        }
    }

    public class SchoolFieldSoakTests
    {
        private const int SeedCount = 1000;
        private FishSpeciesDef _fish, _other;
        private static readonly double[] Instants =
        {
            0d, 0.125d, 1799.9990234375d, 1800d, 1800.0009765625d,
            1800d * 365d, 1800d * 36500d, 4294967296.125d, 1000000000000.25d
        };

        [TearDown]
        public void TearDown()
        {
            if (_fish != null) UnityEngine.Object.DestroyImmediate(_fish);
            if (_other != null) UnityEngine.Object.DestroyImmediate(_other);
        }

        [TestCase(LivingFishKind.Schooling)]
        [TestCase(LivingFishKind.Ambusher)]
        [TestCase(LivingFishKind.BottomDweller)]
        [TestCase(LivingFishKind.Crawler)]
        [TestCase(LivingFishKind.Sessile)]
        public void ThousandSeeds_AllSampleBitsSurviveHistoryAndFreshQueries_AtLongGameTimes(LivingFishKind kind)
        {
            _fish = SchoolFieldRegressionData.Species("fish.soak_subject", kind);
            _other = SchoolFieldRegressionData.Species("fish.soak_neighbour", LivingFishKind.Schooling);
            var catalogue = new[] { _other, _fish };
            var buffer = new SchoolFieldSample[2];
            var truncated = new SchoolFieldSample[1];
            var expected = new SchoolFieldSample[Instants.Length];
            int comparisons = 0;
            for (int index = 0; index < SeedCount; index++)
            {
                int seed = SeedAt(index), x = index % 7 - 3, y = index % 5 - 2;
                var patch = SchoolFieldRegressionData.Patch(x, y);
                var field = SchoolFieldRegressionData.Field(seed);
                for (int t = 0; t < Instants.Length; t++)
                {
                    var reaction = ReactionAt(Instants[t], t);
                    Assert.IsTrue(field.TrySample(_fish, x, y, Instants[t], patch,
                        SchoolFieldRegressionData.Water, reaction, out expected[t]));
                    Assert.That(expected[t].DepthMetres, Is.InRange(2f, 8f));
                    Assert.That(Vector2.Distance(expected[t].Centre, patch.Centre) + expected[t].FootprintMetres,
                                Is.LessThanOrEqualTo(patch.RadiusMetres));
                    Assert.That(expected[t].Tightness01, Is.InRange(0f, 1f));
                    if (kind == LivingFishKind.Sessile || kind == LivingFishKind.Ambusher)
                        Assert.AreEqual(1, expected[t].Individuals);
                    else
                        Assert.That(expected[t].Individuals, Is.InRange(3, 7));
                    if (kind == LivingFishKind.Sessile)
                    {
                        Assert.AreEqual(patch.Centre, expected[t].Centre);
                        Assert.AreEqual(0d, expected[t].MaxPlanarMetresPerGameSecond);
                    }
                }
                for (int t = Instants.Length - 1; t >= 0; t--)
                {
                    double time = Instants[t];
                    var reaction = ReactionAt(time, t);
                    string context = "kind=" + kind + " seed=" + seed + " instant=" + t;
                    var elsewhere = SchoolFieldRegressionData.Patch(x + 1, y - 1);
                    field.TrySample(_other, x + 1, y - 1, time + 123d, elsewhere,
                                    SchoolFieldRegressionData.Water, default, out _);
                    Assert.AreEqual(1, field.FillCell(catalogue, x, y, time, patch,
                        SchoolFieldRegressionData.Water, reaction, truncated), context);
                    Assert.AreEqual(2, field.FillCell(catalogue, x, y, time, patch,
                        SchoolFieldRegressionData.Water, reaction, buffer), context);
                    SchoolFieldRegressionData.Same(expected[t], buffer[1], context);
                    Assert.IsTrue(SchoolFieldRegressionData.Field(seed).TrySample(_fish, x, y, time, patch,
                        SchoolFieldRegressionData.Water, reaction, out var direct), context);
                    SchoolFieldRegressionData.Same(expected[t], direct, context);
                    comparisons += 2;
                }
            }
            Assert.AreEqual(SeedCount * Instants.Length * 2, comparisons);
            TestContext.WriteLine(kind + ": seeds=" + SeedCount + " instants=" + Instants.Length +
                                          " exact sample comparisons=" + comparisons);
        }

        [Test]
        public void ThousandSeeds_SparsePresenceAndAbsenceSurviveLongTimeJumps()
        {
            _fish = SchoolFieldRegressionData.Species("fish.sparse_soak", LivingFishKind.Schooling, 35f);
            int present = 0, absent = 0;
            for (int index = 0; index < SeedCount; index++)
            {
                int seed = SeedAt(index);
                var field = SchoolFieldRegressionData.Field(seed);
                var patch = SchoolFieldRegressionData.Patch(-2, 3);
                bool original = field.TrySample(_fish, -2, 3, 0d, patch,
                    SchoolFieldRegressionData.Water, default, out _);
                if (original) present++; else absent++;
                for (int t = Instants.Length - 1; t >= 0; t--)
                {
                    string context = "seed=" + seed + " instant=" + t;
                    bool replayed = field.TrySample(_fish, -2, 3, Instants[t], patch,
                        SchoolFieldRegressionData.Water, default, out var replay);
                    bool direct = SchoolFieldRegressionData.Field(seed).TrySample(_fish, -2, 3, Instants[t], patch,
                        SchoolFieldRegressionData.Water, default, out var fresh);
                    Assert.AreEqual(original, replayed, context);
                    Assert.AreEqual(original, direct, context);
                    SchoolFieldRegressionData.Same(fresh, replay, context);
                }
            }
            Assert.AreEqual(SeedCount, present + absent);
            // Each side must be exercised substantially; no golden hash or one-result smoke check.
            Assert.That(present, Is.InRange(350, 700));
            Assert.That(absent, Is.InRange(300, 650));
            TestContext.WriteLine("Sparse soak: seeds=" + SeedCount + " present=" + present + " absent=" + absent);
        }

        private static int SeedAt(int index)
            => index == 0 ? int.MinValue : index == 1 ? int.MaxValue : unchecked((index - 2) * -1640531527);

        private static SchoolReaction ReactionAt(double time, int instant)
            => instant % 2 == 0 ? default : new SchoolReaction(time - 6d, new Vector2(-3f, 4f), 1f);
    }

    public class SchoolFieldCostTests
    {
        // Regression workload and CPU allowance, not live gameplay knobs. See the companion cost note.
        private const int CellsPerTick = 32, SpeciesPerCell = 16, WarmupTicks = 32;
        private const int Rounds = 7, TicksPerRound = 16;
        private const double MillisecondsPerTickBudget = 2d;
        private FishSpeciesDef[] _species;
        private SchoolHabitatPatch[] _patches;
        private SchoolFieldSample[] _buffer;
        private SchoolField _field;

        [SetUp]
        public void SetUp()
        {
            _field = SchoolFieldRegressionData.Field(31415);
            _species = new FishSpeciesDef[SpeciesPerCell];
            for (int i = 0; i < _species.Length; i++)
                _species[i] = SchoolFieldRegressionData.Species("fish.cost_" + i, (LivingFishKind)(i % 5));
            _buffer = new SchoolFieldSample[SpeciesPerCell];
            _patches = new SchoolHabitatPatch[CellsPerTick];
            for (int i = 0; i < _patches.Length; i++) _patches[i] = SchoolFieldRegressionData.Patch(i % 8 - 4, i / 8 - 2);
            for (int tick = 0; tick < WarmupTicks; tick++) Assert.AreEqual(CellsPerTick * SpeciesPerCell, Tick(tick));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var fish in _species) UnityEngine.Object.DestroyImmediate(fish);
        }

        [Test]
        public void SlowTick_512SuccessfulSamples_MedianStaysWithinTwoMilliseconds()
        {
            var elapsed = new long[Rounds];
            int samples = 0;
            for (int round = 0; round < Rounds; round++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int tick = 0; tick < TicksPerRound; tick++) samples += Tick(round * TicksPerRound + tick);
                elapsed[round] = Stopwatch.GetTimestamp() - start;
            }
            Assert.AreEqual(Rounds * TicksPerRound * CellsPerTick * SpeciesPerCell, samples);
            Array.Sort(elapsed);
            double median = elapsed[Rounds / 2] * 1000d / Stopwatch.Frequency / TicksPerRound;
            TestContext.WriteLine("SchoolField slow tick: cells=" + CellsPerTick + " species=" + SpeciesPerCell +
                " successful samples/tick=" + CellsPerTick * SpeciesPerCell + " rounds=" + Rounds +
                " ticks/round=" + TicksPerRound + " median ms/tick=" + median.ToString("F4") +
                " budget ms/tick=" + MillisecondsPerTickBudget);
            Assert.That(median, Is.LessThanOrEqualTo(MillisecondsPerTickBudget),
                        "Warmed field CPU envelope exceeded; investigate before changing the budget.");
        }

        [Test]
        public void SlowTick_ReusesCallerBuffersWithoutManagedAllocations()
        {
            using var allocation = new ManagedAllocationProbe();
            allocation.Begin();
            int samples = 0;
            for (int tick = 0; tick < TicksPerRound; tick++) samples += Tick(tick);
            long allocationEvents = allocation.End();
            Assert.AreEqual(TicksPerRound * CellsPerTick * SpeciesPerCell, samples);
            Assert.AreEqual(0L, allocationEvents);
        }

        private int Tick(int tick)
        {
            double time = 1000000000000.25d + tick * 0.25d;
            var reaction = new SchoolReaction(time - 6d, new Vector2(-3f, 4f), 1f);
            int written = 0;
            for (int i = 0; i < CellsPerTick; i++)
                written += _field.FillCell(_species, i % 8 - 4, i / 8 - 2, time, _patches[i],
                                          SchoolFieldRegressionData.Water, reaction, _buffer);
            return written;
        }
    }
}
