using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    public class SchoolFieldTests
    {
        private FishSpeciesDef _fish;
        private SchoolField _field;
        private static readonly SchoolFieldEnvironment DeepWater = new SchoolFieldEnvironment(0f, 0.5f);

        [SetUp]
        public void SetUp()
        {
            _field = new SchoolField(31415, "region.test_water", 1800d, 100d);
            _fish = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _fish.Id = "fish.test_cod";
            _fish.RegionIds = new[] { "region.test_water" };
            _fish.MinSchoolMarks = 3;
            _fish.MaxSchoolMarks = 7;
            _fish.SchoolsPerSquareKilometre = 1000f;
            _fish.LivingBehaviour = new LivingFishBehaviour
            {
                Enabled = true, Kind = LivingFishKind.Schooling, AllowedHabitats = FishHabitat.Rock,
                VegetationPreferenceMultiplier = 2f, PreferredVegetation = FishVegetation.Kelp,
                MinDepthMetres = 2f, MaxDepthMetres = 8f, BedClearanceMetres = 1f,
                PatrolRadiusMetres = 6f, PatrolMetresPerGameSecond = 0.3f,
                NormalFootprintMetres = 2f, ScatteredFootprintMetres = 5f,
                ScatterDistanceMetres = 4f, ReactionDurationGameSeconds = 12d,
                DailyDepthStrength01 = 0.6f, TidalDepthStrength01 = 0.4f
            };
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_fish);

        private static SchoolHabitatPatch Patch(int x = 0, int y = 0, FishHabitat habitat = FishHabitat.Rock,
                                                FishVegetation vegetation = FishVegetation.Kelp, bool known = true)
            => new SchoolHabitatPatch(new Vector2(x * 100f + 50f, y * 100f + 50f), 40f, -20f,
                                      habitat, vegetation, known);

        private SchoolFieldSample At(double time, SchoolReaction reaction = default)
        {
            Assert.IsTrue(_field.TrySample(_fish, 0, 0, time, Patch(), DeepWater, reaction, out var s));
            return s;
        }

        private static void Same(in SchoolFieldSample a, in SchoolFieldSample b)
        {
            Assert.AreEqual(a.SpeciesId, b.SpeciesId);
            Assert.AreEqual(a.Key, b.Key);
            Assert.AreEqual(a.Centre, b.Centre);
            Assert.AreEqual(a.DepthMetres, b.DepthMetres);
            Assert.AreEqual(a.FootprintMetres, b.FootprintMetres);
            Assert.AreEqual(a.Tightness01, b.Tightness01);
            Assert.AreEqual(a.Individuals, b.Individuals);
            Assert.AreEqual(a.MaxPlanarMetresPerGameSecond, b.MaxPlanarMetresPerGameSecond);
        }

        [Test]
        public void DefaultProfile_IsDisabled_AndCannotChangeExistingSpecies()
        {
            _fish.LivingBehaviour = default;
            Assert.IsFalse(_fish.LivingBehaviour.Enabled);
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 50d, Patch(), DeepWater, default, out _));
        }

        [Test]
        public void DepthCycleBudget_AcceptsSinglePrecisionOne_AndRejectsAnOverBudgetProfile()
        {
            var b = _fish.LivingBehaviour;
            // Authored decimal tenths round to single precision. Mono may keep their addition wider.
            Assert.IsTrue(b.IsValid, "0.6f plus 0.4f is the authored unit budget");
            b.TidalDepthStrength01 = 0.40001f;
            Assert.IsFalse(b.IsValid, "numeric roundoff must not admit a genuinely excessive budget");
            b.DailyDepthStrength01 = 1f; b.TidalDepthStrength01 = 0f;
            Assert.IsTrue(b.IsValid);
            b.TidalDepthStrength01 = 0.000001f;
            Assert.IsFalse(b.IsValid);
        }

        [Test]
        public void SameSeedTime_IsExact_AfterReverseAndUnrelatedQueries_AndNewInstance()
        {
            var expected = new SchoolFieldSample[32];
            for (int i = 0; i < expected.Length; i++) expected[i] = At(i * 147.125d);
            for (int i = expected.Length - 1; i >= 0; i--)
            {
                _field.TrySample(_fish, -2, 3, i * 9d, Patch(-2, 3), DeepWater, default, out _);
                Same(expected[i], At(i * 147.125d));
            }
            _field = new SchoolField(31415, "region.test_water", 1800d, 100d);
            Same(expected[7], At(7 * 147.125d));
        }

        [Test]
        public void SeedSpeciesCellAndRegion_AllParticipateInIdentity()
        {
            var original = At(50d);
            _field = new SchoolField(31416, "region.test_water", 1800d, 100d);
            Assert.AreNotEqual(original.Key, At(50d).Key);
            _field = new SchoolField(31415, "region.test_water", 1800d, 100d);
            _fish.Id = "fish.test_herring";
            Assert.AreNotEqual(original.Key, At(50d).Key);
            _fish.Id = "fish.test_cod";
            Assert.IsTrue(_field.TrySample(_fish, -1, 0, 50d, Patch(-1), DeepWater, default, out var neighbour));
            Assert.AreNotEqual(original.Key, neighbour.Key);
            _fish.RegionIds = new[] { "region.other_water" };
            _field = new SchoolField(31415, "region.other_water", 1800d, 100d);
            Assert.AreNotEqual(original.Key, At(50d).Key);
        }

        [TestCase(0d)]
        [TestCase(9000d)]
        [TestCase(13140000d)]
        public void Patrol_IsContinuous_AtLoopDayOldSlotAndLongSaveBoundaries(double origin)
        {
            const double dt = 0.025d;
            double loop = 2d * Math.PI * 6d / 0.3d;
            double[] boundaries = { origin, origin + 1800d, origin + loop, origin + 9000d };
            foreach (double boundary in boundaries)
            {
                var previous = At(boundary - 2d);
                for (int i = 1; i <= 160; i++)
                {
                    var next = At(boundary - 2d + i * dt);
                    Assert.AreEqual(previous.Key, next.Key);
                    Assert.That(Vector2.Distance(previous.Centre, next.Centre), Is.LessThanOrEqualTo(0.3d * dt + 0.00002d));
                    previous = next;
                }
            }
        }

        [TestCase(-17f)]
        [TestCase(-16f)]
        [TestCase(0f)]
        public void Depth_StaysInAuthoredBand_AndAboveHighestBed_ForDayAndTide(float level)
        {
            for (int i = 0; i <= 500; i++)
            {
                var environment = new SchoolFieldEnvironment(level, i / 500f);
                Assert.IsTrue(_field.TrySample(_fish, 0, 0, i * 53d, Patch(), environment, default, out var s));
                Assert.That(s.DepthMetres, Is.InRange(2f, 8f));
                Assert.That(s.DepthMetres, Is.LessThanOrEqualTo(level + 19f));
            }
        }

        [Test]
        public void InsufficientWater_Rejects_InsteadOfMovingSpeciesOutsideItsBand()
        {
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(), new SchoolFieldEnvironment(-18f, 0f), default, out _));
        }

        [TestCase(LivingFishKind.Schooling)]
        [TestCase(LivingFishKind.Ambusher)]
        [TestCase(LivingFishKind.BottomDweller)]
        [TestCase(LivingFishKind.Crawler)]
        [TestCase(LivingFishKind.Sessile)]
        public void WholeFootprint_StaysInsideCertifiedHabitat_EvenAtMaximumScatter(LivingFishKind kind)
        {
            var b = _fish.LivingBehaviour;
            b.Kind = kind; b.PatrolRadiusMetres = 200f;
            _fish.LivingBehaviour = b;
            var reaction = new SchoolReaction(0d, new Vector2(-300f, 400f), 2f);
            for (int i = 0; i <= 600; i++)
            {
                var s = At(i / 25d, reaction);
                Assert.That(Vector2.Distance(s.Centre, new Vector2(50f, 50f)) + s.FootprintMetres,
                            Is.LessThanOrEqualTo(40.00002f));
                Assert.That(s.Tightness01, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void UnknownWrongHabitat_WrongRegion_AndUncertifiedCell_AreEmpty()
        {
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(habitat: FishHabitat.Unknown), DeepWater, default, out _));
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(habitat: FishHabitat.Sand), DeepWater, default, out _));
            Assert.IsFalse(_field.TrySample(_fish, 1, 0, 0d, Patch(), DeepWater, default, out _));
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, default, DeepWater, default, out _));
            _fish.RegionIds = new[] { "region.other_water" };
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(), DeepWater, default, out _));
        }

        [Test]
        public void PreferredKnownVegetation_IncreasesPresence_UnknownIsNeutral_WithoutMovingFish()
        {
            _fish.SchoolsPerSquareKilometre = 50f; // 0.5 per 100 m cell; kelp preference doubles it.
            int neutralCount = 0, preferredCount = 0;
            for (int x = -64; x < 64; x++)
            {
                bool neutral = _field.TrySample(_fish, x, 0, 80d, Patch(x, vegetation: FishVegetation.None), DeepWater, default, out var a);
                bool unknown = _field.TrySample(_fish, x, 0, 80d, Patch(x, known: false), DeepWater, default, out var u);
                bool preferred = _field.TrySample(_fish, x, 0, 80d, Patch(x), DeepWater, default, out var p);
                Assert.AreEqual(neutral, unknown);
                if (neutral) { neutralCount++; Same(a, u); Same(a, p); }
                if (preferred) preferredCount++;
            }
            Assert.AreEqual(128, preferredCount);
            Assert.That(neutralCount, Is.InRange(40, 88)); // Broad statistical bound, not a copied hash implementation.
        }

        [Test]
        public void Reaction_HasZeroEndpoints_BoundedPeak_AndExactReformation()
        {
            var reaction = new SchoolReaction(100d, Vector2.right, 1f);
            Same(At(99d), At(99d, reaction));
            Same(At(100d), At(100d, reaction));
            var peak = At(106d, reaction);
            Assert.That(Vector2.Distance(At(106d).Centre, peak.Centre), Is.EqualTo(4f).Within(0.00001f));
            Assert.AreEqual(0f, peak.Tightness01);
            Assert.AreEqual(5f, peak.FootprintMetres);
            Same(At(112d), At(112d, reaction));
            Same(At(10000d), At(10000d, reaction));
        }

        [Test]
        public void ReactionSpeed_IsBounded_ThroughStartPeakAndEnd()
        {
            var reaction = new SchoolReaction(100d, Vector2.up, 1f);
            const double dt = 0.01d;
            double speedLimit = 0.3d + 4d * Math.PI / 12d;
            var previous = At(99d, reaction);
            for (int i = 1; i <= 1400; i++)
            {
                var next = At(99d + i * dt, reaction);
                Assert.That(next.MaxPlanarMetresPerGameSecond, Is.EqualTo(speedLimit).Within(0.000001d));
                Assert.That(Vector2.Distance(previous.Centre, next.Centre), Is.LessThanOrEqualTo(speedLimit * dt + 0.00002d));
                previous = next;
            }
        }

        [Test]
        public void Sessile_IsStationary_AndAmbusherIsSolitary()
        {
            var b = _fish.LivingBehaviour; b.Kind = LivingFishKind.Sessile; _fish.LivingBehaviour = b;
            var a = At(0d);
            var c = At(1000000d, new SchoolReaction(999994d, Vector2.one, 1f));
            Assert.AreEqual(new Vector2(50f, 50f), a.Centre);
            Assert.AreEqual(a.Centre, c.Centre);
            Assert.AreEqual(1, c.Individuals);
            Assert.AreEqual(1f, c.Tightness01);
            Assert.AreEqual(0d, c.MaxPlanarMetresPerGameSecond);
            b.Kind = LivingFishKind.Ambusher; _fish.LivingBehaviour = b;
            Assert.AreEqual(1, At(0d).Individuals);
        }

        [Test]
        public void InvalidEnabledData_AndNonfiniteInputs_FailClosed()
        {
            var original = _fish.LivingBehaviour;
            var b = original; b.PatrolRadiusMetres = float.NaN; _fish.LivingBehaviour = b;
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(), DeepWater, default, out _));
            b = original; b.MaxDepthMetres = 1f; _fish.LivingBehaviour = b;
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(), DeepWater, default, out _));
            b = original; b.ReactionDurationGameSeconds = 0d; _fish.LivingBehaviour = b;
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(), DeepWater, default, out _));
            _fish.LivingBehaviour = original;
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, double.NaN, Patch(), DeepWater, default, out _));
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(), new SchoolFieldEnvironment(float.PositiveInfinity, 0f), default, out _));
            _fish.SchoolsPerSquareKilometre = 0f;
            Assert.IsFalse(_field.TrySample(_fish, 0, 0, 0d, Patch(), DeepWater, default, out _));
        }

        [Test]
        public void FillCell_IsBounded_AndCatalogueOrderCannotChangeSpeciesResults()
        {
            var other = UnityEngine.Object.Instantiate(_fish);
            try
            {
                other.Id = "fish.test_other";
                var forward = new SchoolFieldSample[2];
                var reverse = new SchoolFieldSample[2];
                Assert.AreEqual(2, _field.FillCell(new[] { _fish, other }, 0, 0, 7d, Patch(), DeepWater, default, forward));
                Assert.AreEqual(2, _field.FillCell(new[] { other, _fish }, 0, 0, 7d, Patch(), DeepWater, default, reverse));
                Same(forward[0], reverse[1]); Same(forward[1], reverse[0]);
                Assert.AreEqual(1, _field.FillCell(new[] { _fish, other }, 0, 0, 7d, Patch(), DeepWater, default, new SchoolFieldSample[1]));
                Assert.AreEqual(0, _field.FillCell(new[] { _fish }, 0, 0, 7d, Patch(), DeepWater, default, Array.Empty<SchoolFieldSample>()));
            }
            finally { UnityEngine.Object.DestroyImmediate(other); }
        }

        [Test]
        public void Query_DoesNotAllocateManagedMemory_AfterWarmup()
        {
            var patch = Patch();
            for (int i = 0; i < 128; i++) _field.TrySample(_fish, 0, 0, i, patch, DeepWater, default, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allPresent = true;
            for (int i = 0; i < 1024; i++)
                allPresent &= _field.TrySample(_fish, 0, 0, i, patch, DeepWater, default, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allPresent);
            Assert.AreEqual(0L, allocated);
        }
    }
}
