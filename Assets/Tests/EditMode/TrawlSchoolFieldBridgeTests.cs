using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    // Requires the real Unity ScriptableObject runtime; managed-only proof does not claim these ran.
    public class TrawlSchoolFieldBridgeTests
    {
        private FishSpeciesDef _species;
        private SchoolField _field;
        private SchoolHabitatPatch _patch;
        private readonly SchoolFieldEnvironment _environment = new SchoolFieldEnvironment(0f, 0.5f);

        [SetUp]
        public void SetUp()
        {
            _field = new SchoolField(17, "region.test", 1800d, 100d);
            _patch = new SchoolHabitatPatch(new Vector2(50f, 50f), 40f, -30f, FishHabitat.Sand, FishVegetation.None, true);
            _species = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _species.Id = "fish.test_cod"; _species.RegionIds = new[] { "region.test" };
            _species.MinSchoolMarks = 12; _species.MaxSchoolMarks = 12; _species.SchoolsPerSquareKilometre = 1000f;
            _species.LivingBehaviour = new LivingFishBehaviour
            {
                Enabled = true, Kind = LivingFishKind.BottomDweller, AllowedHabitats = FishHabitat.Sand,
                VegetationPreferenceMultiplier = 1f, MinDepthMetres = 18f, MaxDepthMetres = 19f,
                NormalFootprintMetres = 1f, ScatteredFootprintMetres = 1f, ReactionDurationGameSeconds = 1d
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_species);

        private bool Sample(bool eligible, out TrawlPopulationSample sample)
            => TrawlSchoolFieldBridge.TrySample(_field, _species, eligible, 0, 0, 123d, _patch, _environment, default, out sample);

        [Test]
        public void Bridge_ReusesTheExactExistingSchoolFieldSampleAndSpeciesSizeFacts()
        {
            Assert.IsTrue(_field.TrySample(_species, 0, 0, 123d, _patch, _environment, default, out var school));
            Assert.IsTrue(Sample(true, out var population));
            Assert.AreEqual(school, population.School);
            Assert.AreEqual(_species.Id, population.Species.Id);
            Assert.AreEqual(_species.MinWeightKg, population.Species.MinWeightKg);
            Assert.AreEqual(_species.MaxWeightKg, population.Species.MaxWeightKg);
            Assert.AreEqual(_species.Seasons, population.Species.Seasons);
        }

        [Test]
        public void MissingReviewedTrawlEligibility_NeverInterpretsTheCurrentGearMaskAsPermission()
        {
            _species.AllowedGear = Gear.Net;
            Assert.IsFalse(Sample(false, out _));
            Assert.IsTrue(Sample(true, out _));
        }

        [Test]
        public void DisabledLivingProfile_DoesNotFallBackToTheLegacySchoolOrCatchTable()
        { _species.LivingBehaviour = default; Assert.IsFalse(Sample(true, out _)); }

        [Test]
        public void UnknownHabitatCertificate_ProducesNoTrawlPopulation()
        { _patch = default; Assert.IsFalse(Sample(true, out _)); }

        [Test]
        public void InvalidSizeFacts_AreRefusedWithoutRetuningTheSpecies()
        { _species.MinWeightKg = float.NaN; Assert.IsFalse(Sample(true, out _)); Assert.IsTrue(float.IsNaN(_species.MinWeightKg)); }
    }
}
