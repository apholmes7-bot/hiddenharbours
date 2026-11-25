using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy.PlantIntake
{
    public static class FishPlantUnits
    {
        public const int PartsPerMillion = 1000000;
        public const int GramsPerKg = 1000;

        internal static void Require(bool condition, string rule)
        {
            if (!condition) throw new ArgumentException("Invalid graded fish plant: " + rule);
        }

        internal static bool HasId(string id, string type)
            => id != null && Regex.IsMatch(id, "^" + type + @"\.[a-z][a-z0-9_]*\z");
    }

    public sealed class FishGradeSpec
    {
        public string Id { get; }
        public int MinimumFreshnessPpm { get; }
        public int PricePerKg { get; }

        public FishGradeSpec(string id, int minimumFreshnessPpm, int pricePerKg)
        {
            FishPlantUnits.Require(FishPlantUnits.HasId(id, "fishgrade"), "grade id");
            FishPlantUnits.Require(minimumFreshnessPpm > 0 && minimumFreshnessPpm <= FishPlantUnits.PartsPerMillion,
                "grade freshness");
            FishPlantUnits.Require(pricePerKg > 0, "grade price");
            Id = id; MinimumFreshnessPpm = minimumFreshnessPpm; PricePerKg = pricePerKg;
        }
    }

    public sealed class FishIntakeSpec
    {
        private readonly HashSet<string> _species;
        private readonly FishGradeSpec[] _grades;
        public string Id { get; }
        public int MaximumDeliveryGrams { get; }

        public FishIntakeSpec(string id, int maximumDeliveryGrams, string[] species, FishGradeSpec[] grades)
        {
            FishPlantUnits.Require(FishPlantUnits.HasId(id, "fishintake"), "intake id");
            FishPlantUnits.Require(maximumDeliveryGrams > 0, "delivery capacity");
            FishPlantUnits.Require(species != null && species.Length > 0 && grades != null && grades.Length > 0,
                "species and grades");
            _species = new HashSet<string>(StringComparer.Ordinal);
            foreach (string s in species)
                FishPlantUnits.Require(FishPlantUnits.HasId(s, "fish") && _species.Add(s), "unique species ids");
            _grades = (FishGradeSpec[])grades.Clone();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _grades.Length; i++)
            {
                FishGradeSpec g = _grades[i];
                FishPlantUnits.Require(g != null && ids.Add(g.Id), "unique grade ids");
                if (i > 0)
                    FishPlantUnits.Require(_grades[i - 1].MinimumFreshnessPpm > g.MinimumFreshnessPpm &&
                        _grades[i - 1].PricePerKg >= g.PricePerKg, "descending grade thresholds and prices");
            }
            Id = id; MaximumDeliveryGrams = maximumDeliveryGrams;
        }

        internal FishGradeSpec Grade(string species, int grams, int freshnessPpm)
        {
            if (species == null || !_species.Contains(species) || grams <= 0 || grams > MaximumDeliveryGrams ||
                freshnessPpm < 0 || freshnessPpm > FishPlantUnits.PartsPerMillion) return null;
            for (int i = 0; i < _grades.Length; i++)
                if (freshnessPpm >= _grades[i].MinimumFreshnessPpm) return _grades[i];
            return null;
        }
    }

    public sealed class GradedFishLineSpec
    {
        public string Id { get; }
        public string SpeciesId { get; }
        public FishProduct Product { get; }
        public int MaximumInputGrams { get; }
        public int YieldPpm { get; }
        public int ProcessGameSeconds { get; }
        public int SlowTickSeconds { get; }

        public GradedFishLineSpec(string id, string speciesId, FishProduct product, int maximumInputGrams,
            int yieldPpm, int processGameSeconds, int slowTickSeconds)
        {
            FishPlantUnits.Require(FishPlantUnits.HasId(id, "gradedfishline") && FishPlantUnits.HasId(speciesId, "fish"),
                "line identity/species");
            FishPlantUnits.Require(Enum.IsDefined(typeof(FishProduct), product) && product != FishProduct.Raw,
                "processed product");
            FishPlantUnits.Require(maximumInputGrams > 0 && yieldPpm > 0 && yieldPpm <= FishPlantUnits.PartsPerMillion &&
                processGameSeconds > 0 && slowTickSeconds > 0, "line capacity/yield/time");
            Id = id; SpeciesId = speciesId; Product = product; MaximumInputGrams = maximumInputGrams;
            YieldPpm = yieldPpm; ProcessGameSeconds = processGameSeconds; SlowTickSeconds = slowTickSeconds;
        }
    }
}
