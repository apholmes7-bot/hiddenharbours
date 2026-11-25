using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HiddenHarbours.Fishing
{
    /// <summary>Reference authoring checks only. Never interprets a typical band as a spawn gate.</summary>
    public static class FishEcologyValidation
    {
        private const EcologyHabitat KnownHabitats = EcologyHabitat.OpenWater | EcologyHabitat.Sand |
            EcologyHabitat.Mud | EcologyHabitat.Gravel | EcologyHabitat.Rock | EcologyHabitat.Estuary;
        private const EcologyVegetation KnownVegetation = EcologyVegetation.Eelgrass | EcologyVegetation.Kelp;

        /// <summary>Appends all diagnostics; does not clear caller-owned findings or mutate the record.</summary>
        public static void Validate(FishEcologyDef record, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (record == null) { errors.Add("Missing ecology record"); return; }
            string label = record.SpeciesId ?? "(missing species id)";
            if (string.IsNullOrEmpty(record.SpeciesId) ||
                !Regex.IsMatch(record.SpeciesId, @"\Afish\.[a-z][a-z0-9_]*\z"))
                errors.Add(label + ": invalid species id");
            else if (record.Id != "ecology." + record.SpeciesId.Substring("fish.".Length))
                errors.Add(label + ": ecology id must follow the stable species suffix");

            Required(record.ScientificName, label, "scientific name", errors);
            Required(record.LifeStageAndScope, label, "life stage and scope", errors);
            Required(record.DepthEvidence, label, "depth evidence", errors);
            Required(record.VegetationEvidence, label, "vegetation evidence", errors);
            Required(record.HabitEvidence, label, "habit evidence", errors);
            Required(record.BiologyNotes, label, "biology notes", errors);

            if (!Finite(record.TypicalMinDepthMetres) || !Finite(record.TypicalMaxDepthMetres) ||
                record.TypicalMinDepthMetres < 0f ||
                record.TypicalMaxDepthMetres <= record.TypicalMinDepthMetres)
                errors.Add(label + ": typical depth band must be finite, nonnegative and increasing");
            if (record.Habitats == EcologyHabitat.None || (record.Habitats & ~KnownHabitats) != 0)
                errors.Add(label + ": habitats must be stated using known flags");
            if ((record.PreferredVegetation & ~KnownVegetation) != 0)
                errors.Add(label + ": unknown vegetation flags");
            CheckEnum(record.Layer, label, "water layer", errors);
            CheckEnum(record.Grouping, label, "grouping", errors);
            CheckEnum(record.DayHabit, label, "day habit", errors);
            CheckEnum(record.TideHabit, label, "tide habit", errors);

            if (record.Grouping == EcologyGrouping.SessileBed && record.Layer != EcologyLayer.Benthic)
                errors.Add(label + ": a sessile bed must be benthic");
            if (record.SourceUrls == null || record.SourceUrls.Length == 0)
                errors.Add(label + ": source URLs required");
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string url in record.SourceUrls)
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri parsed) ||
                        parsed.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(parsed.Host) || !seen.Add(url))
                        errors.Add(label + ": source URL must be unique absolute HTTPS");
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Required(string value, string label, string field, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add(label + ": missing " + field);
        }

        private static void CheckEnum<T>(T value, string label, string field, List<string> errors)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value) || Convert.ToInt32(value) == 0)
                errors.Add(label + ": " + field + " must be explicitly stated");
        }
    }
}
