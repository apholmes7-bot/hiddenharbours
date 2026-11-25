using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    public class FishEcologyReferenceTests
    {
        private const string EcologyRoot = "Assets/_Project/Data/FishEcology";

        [Test]
        public void EveryAuthoredSpecies_HasExactlyOneValidReference()
        {
            var species = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data" }))
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(species.Add(fish.Id), Is.True, "duplicate species " + fish.Id);
            }
            Assert.That(species, Does.Contain("fish.atlantic_cod"), "the source catalogue must actually load");

            var references = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:FishEcologyDef"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(path, Does.StartWith(EcologyRoot + "/"));
                var record = AssetDatabase.LoadAssetAtPath<FishEcologyDef>(path);
                var errors = new List<string>();
                FishEcologyValidation.Validate(record, errors);
                Assert.That(errors, Is.Empty, path + "\n" + string.Join("\n", errors));
                Assert.That(references.Add(record.SpeciesId), Is.True, "duplicate reference " + path);
                Assert.That(ids.Add(record.Id), Is.True, "duplicate ecology id " + path);
                Assert.That(species, Does.Contain(record.SpeciesId), "orphan reference " + path);
            }
            CollectionAssert.AreEquivalent(species, references, "every fish, crustacean and mollusc needs a row");
        }

        [TestCase("SoftShellClam", EcologyGrouping.SessileBed, EcologyLayer.Benthic)]
        [TestCase("AmericanLobster", EcologyGrouping.Solitary, EcologyLayer.Benthic)]
        [TestCase("RockCrab", EcologyGrouping.Solitary, EcologyLayer.Benthic)]
        [TestCase("Mackerel", EcologyGrouping.Schooling, EcologyLayer.Pelagic)]
        [TestCase("AtlanticHerring", EcologyGrouping.Schooling, EcologyLayer.Pelagic)]
        public void BodyPlanAndGrouping_DoNotTurnShellfishIntoSwimmers(string name, EcologyGrouping group,
            EcologyLayer layer)
        {
            var record = AssetDatabase.LoadAssetAtPath<FishEcologyDef>(EcologyRoot + "/" + name + ".asset");
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Grouping, Is.EqualTo(group));
            Assert.That(record.Layer, Is.EqualTo(layer));
        }

        private static FishEcologyDef ValidRecord()
        {
            var record = ScriptableObject.CreateInstance<FishEcologyDef>();
            record.Id = "ecology.test_fish";
            record.SpeciesId = "fish.test_fish";
            record.ScientificName = "Test reference";
            record.LifeStageAndScope = record.DepthEvidence = record.VegetationEvidence =
                record.HabitEvidence = record.BiologyNotes = "Explicit test evidence";
            record.TypicalMinDepthMetres = 0f;
            record.TypicalMaxDepthMetres = 20f;
            record.Habitats = EcologyHabitat.Sand;
            record.Layer = EcologyLayer.Benthic;
            record.Grouping = EcologyGrouping.Solitary;
            record.DayHabit = EcologyDayHabit.Variable;
            record.TideHabit = EcologyTideHabit.NoHardRestriction;
            record.SourceUrls = new[] { "https://www.fisheries.noaa.gov/" };
            return record;
        }

        [TestCase("missing identity")]
        [TestCase("trailing newline identity")]
        [TestCase("wrong stable suffix")]
        [TestCase("missing life stage")]
        [TestCase("missing evidence")]
        [TestCase("negative depth")]
        [TestCase("inverted band")]
        [TestCase("nan depth")]
        [TestCase("infinite depth")]
        [TestCase("unknown habitat")]
        [TestCase("missing habitat")]
        [TestCase("unknown vegetation")]
        [TestCase("unknown grouping")]
        [TestCase("missing day habit")]
        [TestCase("missing tide habit")]
        [TestCase("swimming sessile bed")]
        [TestCase("missing source")]
        [TestCase("insecure source")]
        [TestCase("duplicate source")]
        public void ValidatorRejectsInvalidAuthoring(string defect)
        {
            FishEcologyDef record = ValidRecord();
            try
            {
                switch (defect)
                {
                    case "missing identity": record.SpeciesId = ""; break;
                    case "trailing newline identity": record.SpeciesId += "\n"; record.Id += "\n"; break;
                    case "wrong stable suffix": record.Id = "ecology.wrong"; break;
                    case "missing life stage": record.LifeStageAndScope = " "; break;
                    case "missing evidence": record.DepthEvidence = ""; break;
                    case "negative depth": record.TypicalMinDepthMetres = -1f; break;
                    case "inverted band": record.TypicalMinDepthMetres = 21f; break;
                    case "nan depth": record.TypicalMinDepthMetres = float.NaN; break;
                    case "infinite depth": record.TypicalMaxDepthMetres = float.PositiveInfinity; break;
                    case "unknown habitat": record.Habitats = (EcologyHabitat)1024; break;
                    case "missing habitat": record.Habitats = EcologyHabitat.None; break;
                    case "unknown vegetation": record.PreferredVegetation = (EcologyVegetation)1024; break;
                    case "unknown grouping": record.Grouping = (EcologyGrouping)1024; break;
                    case "missing day habit": record.DayHabit = EcologyDayHabit.Unstated; break;
                    case "missing tide habit": record.TideHabit = EcologyTideHabit.Unstated; break;
                    case "swimming sessile bed": record.Grouping = EcologyGrouping.SessileBed;
                        record.Layer = EcologyLayer.Pelagic; break;
                    case "missing source": record.SourceUrls = Array.Empty<string>(); break;
                    case "insecure source": record.SourceUrls = new[] { "http://example.invalid" }; break;
                    case "duplicate source": record.SourceUrls = new[] { record.SourceUrls[0], record.SourceUrls[0] }; break;
                    default: Assert.Fail("unhandled sabotage case"); break;
                }
                var errors = new List<string>();
                FishEcologyValidation.Validate(record, errors);
                Assert.That(errors, Is.Not.Empty, defect);
            }
            finally { UnityEngine.Object.DestroyImmediate(record); }
        }

        [Test]
        public void ValidatingReferenceData_DoesNotMutateItOrErasePriorFindings()
        {
            FishEcologyDef record = ValidRecord();
            try
            {
                string before = JsonUtility.ToJson(record);
                var errors = new List<string> { "prior finding" };
                FishEcologyValidation.Validate(record, errors);
                CollectionAssert.AreEqual(new[] { "prior finding" }, errors);
                Assert.That(JsonUtility.ToJson(record), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(record); }
        }
    }
}
