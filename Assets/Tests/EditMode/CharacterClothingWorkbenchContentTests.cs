using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using NUnit.Framework;
#if HH_STANDALONE_CONTRACT_TESTS
using System.Text.Json;
#else
using UnityEngine;
#endif

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Reads the exact workbench interchange files through the production DTOs and validator.
    /// Unity runs JsonUtility; the disposable standalone runner exercises System.Text.Json
    /// with IncludeFields. Neither path claims mesh, renderer or ScriptableObject acceptance.
    /// </summary>
    public sealed class CharacterClothingWorkbenchContentTests
    {
        private const string Workbench = "docs/art/character-workbench";

        [Serializable]
        public sealed class PresetFile
        {
            public Preset[] Presets = Array.Empty<Preset>();
        }

        [Serializable]
        public sealed class Preset
        {
            public string Id = "";
            public string DisplayName = "";
            public CharacterAppearanceRecipe Recipe;
        }

        [Test]
        public void IndividualAuthoredFilesFormAValidCatalogueWithoutManualTranscription()
        {
            var catalogue = ReadIndividualCatalogue();
            Assert.That(catalogue.Fits, Is.Not.Empty);
            Assert.That(catalogue.Garments, Is.Not.Empty);
            Assert.That(CharacterClothingValidation.ValidateCatalogue(catalogue), Is.Empty);
        }

        [Test]
        public void EveryAuthoredRecipeValidatesAndRoundTripsTheSamePersonAndEquipment()
        {
            var catalogue = ReadIndividualCatalogue();
            var file = Read<PresetFile>(Path.Combine(Folder(), "wardrobe-recipes.json"));
            Assert.That(file.Presets, Is.Not.Empty);
            Assert.That(file.Presets.Select(x => x.Id).Distinct().Count(), Is.EqualTo(file.Presets.Length));
            foreach (var preset in file.Presets)
            {
                Assert.That(preset.Id, Is.Not.Empty);
                Assert.That(preset.DisplayName, Is.Not.Empty);
                Assert.That(CharacterClothingValidation.ValidateRecipe(preset.Recipe, catalogue), Is.Empty, preset.Id);
                string encoded = Serialize(preset.Recipe);
                var restored = Deserialize<CharacterAppearanceRecipe>(encoded);
                Assert.That(CharacterClothingValidation.ValidateRecipe(restored, catalogue), Is.Empty, preset.Id);
                Assert.That(Serialize(restored), Is.EqualTo(encoded), preset.Id);
            }
        }

        [Test]
        public void CatalogueRoundTripPreservesFitMaterialsPricesSellersAndStarterFlags()
        {
            var original = ReadIndividualCatalogue();
            string encoded = Serialize(original);
            var restored = Deserialize<CharacterClothingCatalogue>(encoded);
            Assert.That(CharacterClothingValidation.ValidateCatalogue(restored), Is.Empty);
            Assert.That(Serialize(restored), Is.EqualTo(encoded));
        }

        private static CharacterClothingCatalogue ReadIndividualCatalogue()
        {
            string folder = Folder();
            string[] fits = Directory.GetFiles(folder, "wardrobe-fit-*.json");
            string[] garments = Directory.GetFiles(folder, "wardrobe-garment-*.json");
            Array.Sort(fits, StringComparer.Ordinal);
            Array.Sort(garments, StringComparer.Ordinal);
            return new CharacterClothingCatalogue
            {
                Fits = fits.Select(Read<CharacterClothingFitSpec>).ToArray(),
                Garments = garments.Select(Read<CharacterGarmentSpec>).ToArray()
            };
        }

        private static T Read<T>(string path) => Deserialize<T>(File.ReadAllText(path));

        private static string Folder()
        {
#if HH_STANDALONE_CONTRACT_TESTS
            return Path.GetFullPath(Workbench);
#else
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", Workbench));
#endif
        }

        private static T Deserialize<T>(string text)
        {
#if HH_STANDALONE_CONTRACT_TESTS
            return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions { IncludeFields = true });
#else
            return JsonUtility.FromJson<T>(text);
#endif
        }

        private static string Serialize<T>(T value)
        {
#if HH_STANDALONE_CONTRACT_TESTS
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { IncludeFields = true });
#else
            return JsonUtility.ToJson(value);
#endif
        }
    }
}
