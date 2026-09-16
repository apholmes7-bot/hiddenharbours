using System;
using System.Linq;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class CharacterClothingContractTests
    {
        private CharacterClothingCatalogue _catalogue;
        private CharacterAppearanceRecipe _recipe;

        [SetUp]
        public void SetUp()
        {
            _catalogue = new CharacterClothingCatalogue
            {
                Fits = new[]
                {
                    new CharacterClothingFitSpec
                    {
                        Id = "fit.fisher", SkeletonId = "skeleton.fisher",
                        SourceRigSha256 = new string('a', 64),
                        BoneIds = new[] { "root", "pelvis", "head" },
                        SourceSectionIds = new[] { "torso", "trousers", "hat", "boots" },
                        CoverageTags = new[] { "forearms", "hair" }
                    }
                },
                Garments = new[]
                {
                    Garment("garment.overalls", "outerwear", new[] { "top", "bottom" },
                            new[] { "torso", "trousers" }),
                    Garment("garment.cap", "headwear", new[] { "headwear" }, new[] { "hat" })
                }
            };
            _recipe = new CharacterAppearanceRecipe
            {
                Identity = new CharacterIdentityRecipe
                {
                    BuildId = "char_build.fisher", FitId = "fit.fisher", Skin = "warm",
                    Hair = "brown", Eyes = "green"
                },
                Garments = new[] { Select("garment.overalls"), Select("garment.cap") }
            };
        }

        [Test]
        public void AuthoredCombinedGarmentAndIndependentHeadwearAreValid()
        {
            Assert.That(CharacterClothingValidation.ValidateCatalogue(_catalogue), Is.Empty);
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Empty);
        }

        [TestCase("duplicate garment")]
        [TestCase("duplicate fit")]
        [TestCase("duplicate bone")]
        [TestCase("unknown fit")]
        [TestCase("missing section")]
        [TestCase("unknown section")]
        [TestCase("unknown coverage")]
        [TestCase("missing colourways")]
        [TestCase("missing ramp")]
        [TestCase("missing material")]
        [TestCase("negative price")]
        [TestCase("unknown slot")]
        [TestCase("no occupied slots")]
        [TestCase("duplicate occupied slot")]
        [TestCase("missing name")]
        [TestCase("missing garment id")]
        [TestCase("missing skeleton")]
        [TestCase("missing source digest")]
        [TestCase("invalid source digest")]
        [TestCase("newline after source digest")]
        [TestCase("newline after garment id")]
        [TestCase("catalogue version")]
        [TestCase("garment version")]
        [TestCase("fit version")]
        public void BrokenAuthoringDataIsRejected(string defect)
        {
            CharacterGarmentSpec garment = _catalogue.Garments[0];
            CharacterClothingFitSpec fit = _catalogue.Fits[0];
            switch (defect)
            {
                case "duplicate garment": _catalogue.Garments[1].Id = garment.Id; break;
                case "duplicate fit": _catalogue.Fits = new[] { fit, fit }; break;
                case "duplicate bone": fit.BoneIds = new[] { "root", "root" }; break;
                case "unknown fit": garment.Fits[0].FitId = "fit.absent"; break;
                case "missing section": garment.Fits[0].SourceSectionIds = Array.Empty<string>(); break;
                case "unknown section": garment.Fits[0].SourceSectionIds = new[] { "absent" }; break;
                case "unknown coverage": garment.CoverageTags = new[] { "undeclared_surface" }; break;
                case "missing colourways": garment.Colourways = Array.Empty<CharacterGarmentColourway>(); break;
                case "missing ramp": garment.Colourways[0].MaterialRoles[0].RampId = ""; break;
                case "missing material": garment.Colourways[0].MaterialRoles[0].SourceMaterial = ""; break;
                case "negative price": garment.Price = -1; break;
                case "unknown slot": garment.OccupiedSlots = new[] { "nose" }; break;
                case "no occupied slots": garment.OccupiedSlots = Array.Empty<string>(); break;
                case "duplicate occupied slot": garment.OccupiedSlots = new[] { "top", "top" }; break;
                case "missing name": garment.DisplayName = ""; break;
                case "missing garment id": garment.Id = ""; break;
                case "missing skeleton": fit.SkeletonId = ""; break;
                case "missing source digest": fit.SourceRigSha256 = ""; break;
                case "invalid source digest": fit.SourceRigSha256 = "not-a-sha256"; break;
                case "newline after source digest": fit.SourceRigSha256 += "\n"; break;
                case "newline after garment id": garment.Id += "\n"; break;
                case "catalogue version": _catalogue.SchemaVersion++; break;
                case "garment version": garment.SchemaVersion++; break;
                case "fit version": fit.SchemaVersion++; break;
                default: Assert.Fail("Unimplemented defect: " + defect); break;
            }

            Assert.That(CharacterClothingValidation.ValidateCatalogue(_catalogue), Is.Not.Empty, defect);
        }

        [TestCase("null catalogue")]
        [TestCase("null fits")]
        [TestCase("null fit")]
        [TestCase("null garment")]
        [TestCase("null garment fit")]
        [TestCase("null colourway")]
        [TestCase("null material role")]
        public void MalformedCatalogueReturnsDiagnosticsInsteadOfThrowing(string defect)
        {
            switch (defect)
            {
                case "null catalogue": _catalogue = null; break;
                case "null fits": _catalogue.Fits = null; break;
                case "null fit": _catalogue.Fits[0] = null; break;
                case "null garment": _catalogue.Garments[0] = null; break;
                case "null garment fit": _catalogue.Garments[0].Fits[0] = null; break;
                case "null colourway": _catalogue.Garments[0].Colourways[0] = null; break;
                case "null material role": _catalogue.Garments[0].Colourways[0].MaterialRoles[0] = null; break;
            }

            Assert.That(CharacterClothingValidation.ValidateCatalogue(_catalogue), Is.Not.Empty, defect);
        }

        [TestCase("unsupported fit")]
        [TestCase("missing garment")]
        [TestCase("missing colourway")]
        [TestCase("duplicate selection")]
        [TestCase("recipe version")]
        [TestCase("null recipe")]
        [TestCase("null identity")]
        [TestCase("null selection")]
        public void InvalidPreviewRecipeIsRejectedWithoutThrowing(string defect)
        {
            switch (defect)
            {
                case "unsupported fit": _recipe.Identity.FitId = "fit.absent"; break;
                case "missing garment": _recipe.Garments[0].GarmentId = "garment.absent"; break;
                case "missing colourway": _recipe.Garments[0].ColourwayId = "absent"; break;
                case "duplicate selection": _recipe.Garments[1] = Select("garment.overalls"); break;
                case "recipe version": _recipe.SchemaVersion++; break;
                case "null recipe": _recipe = null; break;
                case "null identity": _recipe.Identity = null; break;
                case "null selection": _recipe.Garments[0] = null; break;
            }

            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Not.Empty, defect);
        }

        [Test]
        public void FullOutfitConflictsWithTrousersThroughItsSecondOccupiedSlot()
        {
            _catalogue.Garments[1] = Garment("garment.trousers", "bottom", new[] { "bottom" }, new[] { "trousers" });
            _recipe.Garments[1] = Select("garment.trousers");
            Assert.That(CharacterClothingValidation.ValidateCatalogue(_catalogue), Is.Empty);
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Not.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IncompatibilityIsEnforcedInEitherSelectionOrder(bool reverse)
        {
            _catalogue.Garments[0].IncompatibleTags = new[] { "bulky_hat" };
            _catalogue.Garments[1].CompatibilityTags = new[] { "bulky_hat" };
            if (reverse) Array.Reverse(_recipe.Garments);
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Not.Empty);
        }

        [Test]
        public void GarmentFitMustMatchTheSelectedPersonEvenWhenBothFitsExist()
        {
            var otherFit = new CharacterClothingFitSpec
            {
                Id = "fit.other", SkeletonId = "skeleton.other", SourceRigSha256 = new string('b', 64),
                BoneIds = new[] { "root" }, SourceSectionIds = new[] { "torso" },
                CoverageTags = Array.Empty<string>()
            };
            _catalogue.Fits = new[] { _catalogue.Fits[0], otherFit };
            _recipe.Identity.FitId = otherFit.Id;
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Not.Empty);
        }

        [Test]
        public void NamedOriginalColourwayNeedsNoMaterialOverrides()
        {
            _catalogue.Garments[0].Colourways[0].MaterialRoles = Array.Empty<CharacterGarmentMaterialRole>();
            Assert.That(CharacterClothingValidation.ValidateCatalogue(_catalogue), Is.Empty);
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Empty);
        }

        [Test]
        public void DistinctSlotsCannotDrawTheSameAuthoredSectionTwice()
        {
            _catalogue.Garments[1].Fits[0].SourceSectionIds = new[] { "torso" };
            Assert.That(CharacterClothingValidation.ValidateCatalogue(_catalogue), Is.Empty);
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Not.Empty);
        }

        [Test]
        public void MissingCatalogueWithARealRecipeReturnsDiagnostics()
        {
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, null), Is.Not.Empty);
        }

        [Test]
        public void NullOptionalListsMeanNoSelectionsAndAreNotRewritten()
        {
            _catalogue.Garments = null;
            _recipe.Garments = null;
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Empty);
            Assert.That(_catalogue.Garments, Is.Null);
            Assert.That(_recipe.Garments, Is.Null);
        }

        [Test]
        public void RequiredFittedSurfacesCannotBeRemovedIntoAHollowBody()
        {
            _catalogue.Fits[0].RequiredSlots = new[] { "top", "bottom" };
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Empty);
            _recipe.Garments = new[] { Select("garment.cap") };
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Not.Empty);
        }

        [Test]
        public void OptionalHeadwearCanBeRemovedWhileRequiredGarmentsRemain()
        {
            _catalogue.Fits[0].RequiredSlots = new[] { "top", "bottom" };
            _recipe.Garments = new[] { Select("garment.overalls") };
            Assert.That(CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue), Is.Empty);
        }

        [Test]
        public void UnknownRequiredSlotIsAnAuthoringError()
        {
            _catalogue.Fits[0].RequiredSlots = new[] { "elbow" };
            Assert.That(CharacterClothingValidation.ValidateCatalogue(_catalogue), Is.Not.Empty);
        }

        [Test]
        public void ValidationDoesNotChangeThePersonEquipmentOrAuthoringArrays()
        {
            var identity = _recipe.Identity;
            var selections = _recipe.Garments;
            var sections = _catalogue.Garments[0].Fits[0].SourceSectionIds;
            var occupied = _catalogue.Garments[0].OccupiedSlots;
            var originalSections = sections.ToArray();
            var originalOccupied = occupied.ToArray();
            _recipe.Garments[1].ColourwayId = "temporarily_missing";

            var first = CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue);
            var second = CharacterClothingValidation.ValidateRecipe(_recipe, _catalogue);

            Assert.That(second, Is.EqualTo(first), "Diagnostics are deterministic.");
            Assert.That(_recipe.Identity, Is.SameAs(identity));
            Assert.That(new[] { identity.BuildId, identity.FitId, identity.Skin, identity.Hair, identity.Eyes },
                Is.EqualTo(new[] { "char_build.fisher", "fit.fisher", "warm", "brown", "green" }));
            Assert.That(_recipe.Garments, Is.SameAs(selections));
            Assert.That(_recipe.Garments[1].ColourwayId, Is.EqualTo("temporarily_missing"),
                "Validation must not silently replace missing content.");
            Assert.That(_catalogue.Garments[0].Fits[0].SourceSectionIds, Is.SameAs(sections));
            Assert.That(sections, Is.EqualTo(originalSections));
            Assert.That(_catalogue.Garments[0].OccupiedSlots, Is.SameAs(occupied));
            Assert.That(occupied, Is.EqualTo(originalOccupied));
        }

        private static CharacterGarmentSelection Select(string garmentId) => new CharacterGarmentSelection
        {
            GarmentId = garmentId, ColourwayId = "navy"
        };

        private static CharacterGarmentSpec Garment(string id, string category, string[] slots, string[] sections)
            => new CharacterGarmentSpec
            {
                Id = id, DisplayName = id, Category = category, OccupiedSlots = slots,
                Fits = new[] { new CharacterGarmentFitSpec { FitId = "fit.fisher", SourceSectionIds = sections } },
                Colourways = new[]
                {
                    new CharacterGarmentColourway
                    {
                        Id = "navy", DisplayName = "Deep navy",
                        MaterialRoles = new[]
                        {
                            new CharacterGarmentMaterialRole { SourceMaterial = "cloth", Role = "fabric", RampId = "navy" }
                        }
                    }
                },
                Price = 25, SellerIds = new[] { "seller.leblancs" }
            };
    }
}
