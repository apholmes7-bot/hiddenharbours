using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Shared, read-only authoring validation. Call on catalogue edits/recipe changes, not per frame.
    /// This checks interchange and compatibility, not ownership or Unity mesh readiness. A baker
    /// must additionally resolve source geometry/materials, bind poses, weights and source hashes.
    /// </summary>
    public static class CharacterClothingValidation
    {
        static readonly Regex Sha256 = new Regex(@"\A[a-f0-9]{64}\z", RegexOptions.CultureInvariant);

        public static List<string> ValidateCatalogue(CharacterClothingCatalogue catalogue)
        {
            var errors = new List<string>();
            if (catalogue == null)
            {
                errors.Add("Catalogue is missing.");
                return errors;
            }
            Version(catalogue.SchemaVersion, "Catalogue", errors);
            var fits = new Dictionary<string, CharacterClothingFitSpec>(StringComparer.Ordinal);
            foreach (var fit in catalogue.Fits ?? Array.Empty<CharacterClothingFitSpec>())
            {
                if (fit == null) { errors.Add("Catalogue contains a missing fit."); continue; }
                string context = "Fit '" + fit.Id + "'";
                Version(fit.SchemaVersion, context, errors);
                Id(fit.Id, "fit", context, errors);
                if (!string.IsNullOrEmpty(fit.Id))
                {
                    if (fits.ContainsKey(fit.Id)) errors.Add(context + " has a duplicate ID.");
                    else fits.Add(fit.Id, fit);
                }
                if (!Sha256.IsMatch(fit.SourceRigSha256 ?? ""))
                    errors.Add(context + " needs a lowercase SHA-256 of its source rig.");
                Id(fit.SkeletonId, "skeleton", context, errors);
                Strings(fit.BoneIds, context + " BoneIds", true, errors);
                Strings(fit.SourceSectionIds, context + " SourceSectionIds", true, errors);
                Strings(fit.CoverageTags, context + " CoverageTags", false, errors);
                foreach (string slot in Strings(fit.RequiredSlots, context + " RequiredSlots", false, errors))
                    if (!CharacterClothingSchema.IsSlot(slot)) errors.Add(context + " has unknown required slot '" + slot + "'.");
            }
            if (fits.Count == 0) errors.Add("Catalogue needs at least one explicit fit.");

            var garmentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var garment in catalogue.Garments ?? Array.Empty<CharacterGarmentSpec>())
            {
                if (garment == null) { errors.Add("Catalogue contains a missing garment."); continue; }
                string context = "Garment '" + garment.Id + "'";
                Version(garment.SchemaVersion, context, errors);
                Id(garment.Id, "garment", context, errors);
                if (!garmentIds.Add(garment.Id ?? "")) errors.Add(context + " has a duplicate ID.");
                Text(garment.DisplayName, context + " DisplayName", errors);
                if (!CharacterClothingSchema.IsSlot(garment.Category) && garment.Category != "outfit")
                    errors.Add(context + " has an unknown category.");
                if (garment.Price < 0) errors.Add(context + " price cannot be negative.");
                Strings(garment.SellerIds, context + " SellerIds", false, errors);
                var slots = Strings(garment.OccupiedSlots, context + " OccupiedSlots", true, errors);
                foreach (string slot in slots)
                    if (!CharacterClothingSchema.IsSlot(slot)) errors.Add(context + " has unknown slot '" + slot + "'.");
                var coverage = Strings(garment.CoverageTags, context + " CoverageTags", false, errors);
                var tags = Strings(garment.CompatibilityTags, context + " CompatibilityTags", false, errors);
                var blocked = Strings(garment.IncompatibleTags, context + " IncompatibleTags", false, errors);
                foreach (string tag in tags)
                    if (blocked.Contains(tag)) errors.Add(context + " both declares and blocks tag '" + tag + "'.");

                var usedFits = new HashSet<string>(StringComparer.Ordinal);
                foreach (var variant in garment.Fits ?? Array.Empty<CharacterGarmentFitSpec>())
                {
                    if (variant == null) { errors.Add(context + " contains a missing fit variant."); continue; }
                    if (!usedFits.Add(variant.FitId ?? "")) errors.Add(context + " repeats fit '" + variant.FitId + "'.");
                    var sections = Strings(variant.SourceSectionIds, context + " SourceSectionIds", true, errors);
                    if (!fits.TryGetValue(variant.FitId ?? "", out var fit))
                    {
                        errors.Add(context + " references unknown fit '" + variant.FitId + "'.");
                        continue;
                    }
                    foreach (string section in sections)
                        if (!Contains(fit.SourceSectionIds, section))
                            errors.Add(context + " has unresolved section '" + section + "' for " + fit.Id + ".");
                    foreach (string tag in coverage)
                        if (!Contains(fit.CoverageTags, tag))
                            errors.Add(context + " has unresolved coverage '" + tag + "' for " + fit.Id + ".");
                }
                if (usedFits.Count == 0) errors.Add(context + " needs at least one fitted geometry variant.");

                var colourIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var colour in garment.Colourways ?? Array.Empty<CharacterGarmentColourway>())
                {
                    if (colour == null) { errors.Add(context + " contains a missing colourway."); continue; }
                    Text(colour.Id, context + " colourway ID", errors);
                    Text(colour.DisplayName, context + " colourway DisplayName", errors);
                    if (!colourIds.Add(colour.Id ?? "")) errors.Add(context + " repeats colourway '" + colour.Id + "'.");
                    var materials = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var role in colour.MaterialRoles ?? Array.Empty<CharacterGarmentMaterialRole>())
                    {
                        if (role == null) { errors.Add(context + " contains a missing material role."); continue; }
                        Text(role.SourceMaterial, context + " SourceMaterial", errors);
                        Text(role.Role, context + " material Role", errors);
                        Text(role.RampId, context + " RampId", errors);
                        if (!materials.Add(role.SourceMaterial ?? ""))
                            errors.Add(context + " colourway '" + colour.Id + "' maps a source material twice.");
                    }
                }
                if (colourIds.Count == 0) errors.Add(context + " needs at least one named colourway (even if unchanged).");
            }
            return errors;
        }

        public static List<string> ValidateRecipe(CharacterAppearanceRecipe recipe, CharacterClothingCatalogue catalogue)
        {
            var errors = ValidateCatalogue(catalogue);
            if (recipe == null) { errors.Add("Appearance recipe is missing."); return errors; }
            Version(recipe.SchemaVersion, "Appearance recipe", errors);
            if (recipe.Identity == null) { errors.Add("Appearance identity is missing."); return errors; }
            Id(recipe.Identity.BuildId, "char_build", "Appearance identity", errors);
            Id(recipe.Identity.FitId, "fit", "Appearance identity", errors);
            CharacterClothingFitSpec identityFit = null;
            foreach (var fit in catalogue?.Fits ?? Array.Empty<CharacterClothingFitSpec>())
                if (fit != null && fit.Id == recipe.Identity.FitId) { identityFit = fit; break; }
            if (identityFit == null) errors.Add("Appearance identity references an unknown fit.");

            var selected = new List<CharacterGarmentSpec>();
            var slots = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sections = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in recipe.Garments ?? Array.Empty<CharacterGarmentSelection>())
            {
                if (choice == null) { errors.Add("Appearance contains a missing garment selection."); continue; }
                if (!ids.Add(choice.GarmentId ?? "")) errors.Add("Appearance repeats garment '" + choice.GarmentId + "'.");
                CharacterGarmentSpec garment = null;
                foreach (var candidate in catalogue?.Garments ?? Array.Empty<CharacterGarmentSpec>())
                    if (candidate != null && candidate.Id == choice.GarmentId) { garment = candidate; break; }
                if (garment == null) { errors.Add("Appearance references missing garment '" + choice.GarmentId + "'."); continue; }
                bool hasColour = false;
                foreach (var colour in garment.Colourways ?? Array.Empty<CharacterGarmentColourway>())
                    if (colour != null && colour.Id == choice.ColourwayId) hasColour = true;
                if (!hasColour) errors.Add(garment.Id + " has no colourway '" + choice.ColourwayId + "'.");
                CharacterGarmentFitSpec variant = null;
                foreach (var fit in garment.Fits ?? Array.Empty<CharacterGarmentFitSpec>())
                    if (fit != null && fit.FitId == recipe.Identity.FitId) { variant = fit; break; }
                if (variant == null) errors.Add(garment.Id + " does not fit " + recipe.Identity.FitId + ".");
                else
                    foreach (string section in variant.SourceSectionIds ?? Array.Empty<string>())
                        if (!sections.Add(section ?? "")) errors.Add("Appearance uses section '" + section + "' twice.");
                foreach (string slot in garment.OccupiedSlots ?? Array.Empty<string>())
                    if (!slots.Add(slot ?? "")) errors.Add("Appearance has conflicting garments in slot '" + slot + "'.");
                foreach (var previous in selected)
                    if (Conflicts(garment, previous) || Conflicts(previous, garment))
                        errors.Add(garment.Id + " is incompatible with " + previous.Id + ".");
                selected.Add(garment);
            }
            foreach (string slot in identityFit?.RequiredSlots ?? Array.Empty<string>())
                if (!slots.Contains(slot)) errors.Add("Appearance is missing required fitted slot '" + slot + "'.");
            return errors;
        }

        static bool Conflicts(CharacterGarmentSpec a, CharacterGarmentSpec b)
        {
            foreach (string tag in a.IncompatibleTags ?? Array.Empty<string>())
                if (Contains(b.CompatibilityTags, tag)) return true;
            return false;
        }

        static bool Contains(string[] values, string value) => values != null && Array.IndexOf(values, value) >= 0;

        static void Version(int version, string context, List<string> errors)
        {
            if (version != CharacterClothingSchema.Version) errors.Add(context + " has unsupported schema version " + version + ".");
        }

        static void Id(string value, string prefix, string context, List<string> errors)
        {
            if (!Regex.IsMatch(value ?? "", @"\A" + prefix + @"\.[a-z0-9]+(_[a-z0-9]+)*\z", RegexOptions.CultureInvariant))
                errors.Add(context + " needs a stable " + prefix + ".snake_case ID.");
        }

        static void Text(string value, string context, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add(context + " is missing.");
        }

        static HashSet<string> Strings(string[] values, string context, bool required, List<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(value)) errors.Add(context + " contains an empty entry.");
                else if (!seen.Add(value)) errors.Add(context + " repeats '" + value + "'.");
            }
            if (required && seen.Count == 0) errors.Add(context + " must not be empty.");
            return seen;
        }
    }
}
