using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Who a build is for. The cast may be any of the ten presets; the player is never
    /// offered a cast-only value (ruling 2: children are the cast's).</summary>
    public enum CharacterAudience
    {
        Player = 0,
        Cast = 1,
    }

    /// <summary>
    /// What <see cref="CharacterBuilder"/> answers: the build, its baked body, the twenty values it
    /// resolved and the colours for each of the body's materials.
    /// </summary>
    public sealed class CharacterLook
    {
        public readonly CharacterBuildDef Build;

        /// <summary>The baked body: the preset's own def, untouched (structure is baked).</summary>
        public readonly CharacterSkinDef Skin;

        /// <summary>The resolved value of every field, in the contract's (the rig's) order.</summary>
        public readonly string[] Values;

        /// <summary>One ramp per entry of <c>Skin.Materials</c>, in that order, each the length the
        /// def bakes. READ-ONLY: an entry may be the ramps asset's own array.</summary>
        public readonly Color32[][] Colours;

        public CharacterLook(CharacterBuildDef build, CharacterSkinDef skin, string[] values, Color32[][] colours)
        {
            Build = build;
            Skin = skin;
            Values = values;
            Colours = colours;
        }
    }

    /// <summary>
    /// <b>THE CHARACTER BUILDER</b> (character PR 2b, ADR 0051, candidate C): a recipe in, a baked
    /// body and its colours out, with no JS and no colour arithmetic. Structure is the ten baked
    /// defs; colour is a lookup in tables lifted from the rig (<see cref="CharacterRigContractDef"/>,
    /// <see cref="CharacterRampsDef"/>). Pure and static, so every refusal is testable with no scene.
    ///
    /// <para><b>It refuses rather than guesses, and names what it refused.</b> The rig quietly makes
    /// an unknown preset or option the Fisher's; the builder never does. An unknown, NPC or custom
    /// preset, an unknown option, a structural value that is not the preset's own (presets only), a
    /// cast-only value for the player, and a def the contract does not describe are each refused with
    /// a sentence that names the field and the value.</para>
    /// </summary>
    public static class CharacterBuilder
    {
        /// <summary>The rig's prefix for a build passed as JSON (<c>buildOf('b:{…}')</c>).</summary>
        public const string CustomBuildPrefix = "b:";

        /// <summary>The prefix of a build id (<c>char_build.fisher</c>).</summary>
        public const string BuildIdPrefix = "char_build.";

        /// <summary>
        /// Null if the builder builds the rig preset <paramref name="preset"/> for
        /// <paramref name="audience"/>; else why not, naming it. The rig's own refusal
        /// (<c>buildOf</c> throws on an unknown key) is a subset of this one.
        /// </summary>
        public static string RefusePreset(CharacterBuilderDef builder, string preset, CharacterAudience audience)
        {
            string missing = MissingTables(builder);
            if (missing != null) return missing;
            CharacterRigContractDef contract = builder.Contract;

            if (string.IsNullOrEmpty(preset))
                return "No preset named. The builder never falls back to the Fisher the way the rig does.";
            if (preset.StartsWith(CustomBuildPrefix, StringComparison.Ordinal))
                return $"'{Short(preset)}' is a custom build. Rig 10's builder builds presets only (ADR 0051).";
            if (contract.IsNpcPreset(preset))
                return $"'{preset}' is an NPC preset. The NPCs come with the wardrobe, after PR 3; the " +
                       $"builder builds the ten core presets ({contract.PresetList()}).";

            CharacterPresetValues own = contract.Preset(preset);
            if (own == null)
                return $"Unknown preset '{preset}' (presets: {contract.PresetList()}).";
            if (audience == CharacterAudience.Player)
            {
                string castOnly = CastOnlyValue(contract, own.Values);
                if (castOnly != null)
                    return $"'{preset}' is cast-only ({castOnly}): the player is never offered it.";
            }
            if (builder.BuildForPreset(preset) == null)
                return $"'{preset}' has no build in {builder.Id} ({builder.BuildList()}).";
            return null;
        }

        /// <summary>
        /// Null if <paramref name="recipe"/> builds for <paramref name="audience"/>; else why not,
        /// naming the field and value. The colour check runs too, so a null here means
        /// <see cref="TryBuild"/> succeeds.
        /// </summary>
        public static string Refuse(CharacterBuilderDef builder, CharacterRecipe recipe, CharacterAudience audience) =>
            TryBuild(builder, recipe, audience, out _, out string refusal) ? null : refusal;

        /// <summary>
        /// Resolve <paramref name="recipe"/> to the baked body and the colours of each of its
        /// materials. False, with <paramref name="refusal"/> naming the problem, for anything the
        /// builder does not build.
        /// </summary>
        public static bool TryBuild(CharacterBuilderDef builder, CharacterRecipe recipe, CharacterAudience audience,
            out CharacterLook look, out string refusal)
        {
            look = null;
            refusal = Resolve(builder, recipe, audience, out CharacterBuildDef build, out string[] values);
            if (refusal != null) return false;

            CharacterSkinDef skin = build.Visual.Skin;
            refusal = Paint(builder, skin, values, out Color32[][] colours);
            if (refusal != null) return false;

            look = new CharacterLook(build, skin, values, colours);
            return true;
        }

        // ---------------------------------------------------------------- the values

        private static string Resolve(CharacterBuilderDef builder, CharacterRecipe recipe, CharacterAudience audience,
            out CharacterBuildDef build, out string[] values)
        {
            build = null;
            values = null;
            string missing = MissingTables(builder);
            if (missing != null) return missing;
            if (recipe == null) return "No recipe.";
            if (recipe.SchemaVersion > CharacterRecipe.Version)
                return $"The recipe is v{recipe.SchemaVersion}, written by a newer game; this one reads " +
                       $"v{CharacterRecipe.Version}.";

            string fit = recipe.FitId ?? "";
            if (fit.Length > 0 && !string.Equals(fit, builder.FitId, StringComparison.Ordinal))
                return $"Fit '{fit}' is not the builder's fit '{builder.FitId}'.";

            CharacterRigContractDef contract = builder.Contract;
            string buildId = string.IsNullOrEmpty(recipe.BuildId) ? builder.PlayerDefaultBuildId : recipe.BuildId;
            build = builder.Build(buildId);
            if (build == null)
            {
                string key = buildId.StartsWith(BuildIdPrefix, StringComparison.Ordinal)
                    ? buildId.Substring(BuildIdPrefix.Length) : buildId;
                return contract.IsNpcPreset(key)
                    ? $"Build '{buildId}' is an NPC preset's. The NPCs come with the wardrobe, after PR 3."
                    : $"Unknown build '{buildId}' (builds: {builder.BuildList()}).";
            }

            string presetRefusal = RefusePreset(builder, build.Preset, audience);
            if (presetRefusal != null) return presetRefusal;
            if (build.Visual == null || build.Visual.Skin == null)
                return $"Build '{build.Id}' has no baked body: its visual names no skin.";

            CharacterPresetValues own = contract.Preset(build.Preset);
            CharacterField[] fields = contract.Fields;
            if (own.Values == null || own.Values.Length != fields.Length)
                return $"The contract's '{own.Key}' carries {own.Values?.Length ?? 0} values for " +
                       $"{fields.Length} fields. Re-run the builder's bake.";
            values = (string[])own.Values.Clone();

            // The build's own colour overrides, then the recipe's: an empty value is the layer below.
            foreach ((string axis, string key) in build.ColourOverrides())
            {
                string refused = Take(contract, builder.Ramps, own, values, audience, axis, key, $"Build '{build.Id}'");
                if (refused != null) return refused;
            }
            for (int i = 0; i < fields.Length; i++)
            {
                string name = fields[i]?.Name;
                string value = recipe.Get(name);
                if (value == null)
                    return $"The contract names a field '{name}' the recipe does not carry. Re-run the " +
                           "builder's bake, or give CharacterRecipe the field.";
                if (value.Length == 0) continue;
                string refused = Take(contract, builder.Ramps, own, values, audience, name, value, "The recipe");
                if (refused != null) return refused;
            }
            return null;
        }

        private static string Take(CharacterRigContractDef contract, CharacterRampsDef ramps, CharacterPresetValues own,
            string[] values, CharacterAudience audience, string name, string value, string who)
        {
            int i = contract.FieldIndex(name);
            if (i < 0) return $"{who} sets '{name}', which is not one of the rig's fields.";
            CharacterField field = contract.Fields[i];
            if (!field.Allows(value))
                return $"{who}'s {name} '{value}' is not one of the rig's options " +
                       $"({string.Join(", ", field.Options ?? Array.Empty<string>())}).";
            if (audience == CharacterAudience.Player && field.IsCastOnly(value))
                return $"{who}'s {name} '{value}' is cast-only: the player is never offered it.";
            if (field.Kind == CharacterFieldKind.Structure)
            {
                if (!string.Equals(value, own.Values[i], StringComparison.Ordinal))
                    return $"{who}'s {name} '{value}' is not {own.Key}'s own '{own.Values[i]}'. The builder " +
                           "builds presets only, so the structure is the preset's.";
            }
            else if (ramps.Ramp(name, value) == null)
            {
                return $"{who}'s {name} '{value}' has no ramp in {ramps.Id}. Re-run the builder's bake.";
            }
            values[i] = value;
            return null;
        }

        // ---------------------------------------------------------------- the colours

        private static string Paint(CharacterBuilderDef builder, CharacterSkinDef skin, string[] values,
            out Color32[][] colours)
        {
            colours = null;
            CharacterSkinDef.Material[] mats = skin.Materials ?? Array.Empty<CharacterSkinDef.Material>();
            int max = CharacterSkinDef.MaxMaterials(skin.ToneRule);
            if (mats.Length > max)
                return $"'{skin.Id}' paints {mats.Length} materials, more than the {max} a figure carries.";

            CharacterRigContractDef contract = builder.Contract;
            var result = new Color32[mats.Length][];
            for (int m = 0; m < mats.Length; m++)
            {
                CharacterSkinDef.Material mat = mats[m];
                CharacterPaintRule rule = contract.Rule(mat.Name);
                if (rule == null)
                    return $"'{skin.Id}' paints '{mat.Name}', which {contract.Id} does not describe. Re-run " +
                           "the builder's bake on the rig the def was baked from.";
                if (mat.Offset != rule.Offset || mat.Gain != rule.Gain || mat.Bias != rule.Bias ||
                    mat.ToneLo != rule.ToneLo || mat.ToneHi != rule.ToneHi)
                    return $"'{skin.Id}' shades '{mat.Name}' with off {mat.Offset}, gain {mat.Gain}, bias " +
                           $"{mat.Bias}, tones {mat.ToneLo}–{mat.ToneHi}; {contract.Id} says off {rule.Offset}, " +
                           $"gain {rule.Gain}, bias {rule.Bias}, tones {rule.ToneLo}–{rule.ToneHi}. One of " +
                           "them is stale.";

                Color32[] c = ColoursOf(contract, builder.Ramps, rule, values);
                if (c == null)
                    return $"{contract.Id} has no colours for '{mat.Name}' at " +
                           $"{Describe(contract, rule, values)}. Re-run the builder's bake.";
                int baked = mat.Colors?.Length ?? 0;
                if (c.Length != baked)
                    return $"'{mat.Name}' takes {c.Length} colours at {Describe(contract, rule, values)}, " +
                           $"where '{skin.Id}' bakes {baked}.";
                result[m] = c;
            }
            colours = result;
            return null;
        }

        private static Color32[] ColoursOf(CharacterRigContractDef contract, CharacterRampsDef ramps,
            CharacterPaintRule rule, string[] values)
        {
            switch (rule.Source)
            {
                case CharacterPaintSource.Axis:
                {
                    int f = contract.FieldIndex(rule.Field);
                    return f >= 0 ? ramps.Ramp(rule.Field, values[f])?.Shades : null;
                }
                case CharacterPaintSource.Fixed:
                    return ramps.FixedRamp(rule.FixedKey)?.Shades;
                case CharacterPaintSource.Table:
                    return rule.Row(contract, values)?.Colors;
                default:
                    return null;
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string MissingTables(CharacterBuilderDef builder)
        {
            if (builder == null)
                return $"No character builder is loaded (Resources/{CharacterBuilderDef.ResourcesPath}).";
            if (builder.Contract == null) return $"{builder.Id} names no rig contract.";
            if (builder.Ramps == null) return $"{builder.Id} names no ramps.";
            if (builder.Contract.Fields == null || builder.Contract.Fields.Length == 0)
                return $"{builder.Contract.Id} lists no fields. Re-run the builder's bake.";
            return null;
        }

        private static string CastOnlyValue(CharacterRigContractDef contract, string[] values)
        {
            if (values == null) return null;
            for (int i = 0; i < contract.Fields.Length && i < values.Length; i++)
            {
                CharacterField f = contract.Fields[i];
                if (f != null && f.IsCastOnly(values[i])) return $"{f.Name} {values[i]}";
            }
            return null;
        }

        private static string Describe(CharacterRigContractDef contract, CharacterPaintRule rule, string[] values)
        {
            switch (rule.Source)
            {
                case CharacterPaintSource.Axis:
                {
                    int f = contract.FieldIndex(rule.Field);
                    return $"{rule.Field} '{(f >= 0 ? values[f] : "?")}'";
                }
                case CharacterPaintSource.Fixed:
                    return $"fixed '{rule.FixedKey}'";
                default:
                {
                    var parts = new string[rule.TableFields?.Length ?? 0];
                    for (int k = 0; k < parts.Length; k++)
                    {
                        int f = contract.FieldIndex(rule.TableFields[k]);
                        parts[k] = $"{rule.TableFields[k]} '{(f >= 0 ? values[f] : "?")}'";
                    }
                    return string.Join(", ", parts);
                }
            }
        }

        private static string Short(string s) => s.Length <= 24 ? s : s.Substring(0, 24) + "…";
    }
}
