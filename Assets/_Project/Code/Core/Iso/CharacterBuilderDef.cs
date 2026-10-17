using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Everything the character builder reads, in one place</b> (character PR 2b, ADR 0051): the
    /// rig's contract, its ramps, the clothing fit its body is rigged on, and the ten builds whose
    /// baked bodies it may hand out. One asset under <c>Resources</c>, so the player's look resolves
    /// with no scene wiring, the same way <see cref="CharacterWardrobeDef"/> and the carry poses load.
    ///
    /// <para><b>Presets only.</b> <see cref="Builds"/> holds the ten <c>core</c> presets the bake
    /// bakes and nothing else: the twenty NPC presets come with the wardrobe, after PR 3, and a
    /// recipe naming one is refused by <see cref="CharacterBuilder"/>, by name.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Builder", fileName = "CharacterBuilder10")]
    public sealed class CharacterBuilderDef : ScriptableObject
    {
        /// <summary>Where the shipped catalog lives under <c>Resources</c>.</summary>
        public const string ResourcesPath = "CharacterBuilder10";

        [Header("Identity")]
        [Tooltip("Stable id, append-only: builder.character_iso10.")]
        public string Id = "builder.character_iso10";

        [Header("What the builder reads")]
        public CharacterRigContractDef Contract;
        [Tooltip("The rig's 69 axis ramps and its fixed ones.")]
        public CharacterRampsDef Ramps;
        [Tooltip("The fit the bodies are rigged on: fit.character_v10.")]
        public CharacterClothingFitDef Fit;
        [Tooltip("The ten core presets' builds, one per preset the bake bakes.")]
        public CharacterBuildDef[] Builds = Array.Empty<CharacterBuildDef>();

        [Header("The player")]
        [Tooltip("The build an empty recipe starts from.")]
        public string PlayerDefaultBuildId = "char_build.fisher";

        /// <summary>The fit's id, or empty when no fit is wired.</summary>
        public string FitId => Fit != null && Fit.Spec != null ? Fit.Spec.Id ?? "" : "";

        public CharacterBuildDef Build(string id)
        {
            if (Builds == null || string.IsNullOrEmpty(id)) return null;
            foreach (CharacterBuildDef b in Builds)
                if (b != null && string.Equals(b.Id, id, StringComparison.Ordinal)) return b;
            return null;
        }

        public CharacterBuildDef BuildForPreset(string preset)
        {
            if (Builds == null || string.IsNullOrEmpty(preset)) return null;
            foreach (CharacterBuildDef b in Builds)
                if (b != null && string.Equals(b.Preset, preset, StringComparison.Ordinal)) return b;
            return null;
        }

        /// <summary>The build ids, comma-separated, for a refusal that lists what is offered.</summary>
        public string BuildList()
        {
            if (Builds == null) return "";
            var ids = new string[Builds.Length];
            for (int i = 0; i < Builds.Length; i++) ids[i] = Builds[i] != null ? Builds[i].Id : "";
            return string.Join(", ", ids);
        }

        /// <summary>The shipped catalog, or null when it failed to load (the caller draws as baked and
        /// says so).</summary>
        public static CharacterBuilderDef LoadShipped() => Resources.Load<CharacterBuilderDef>(ResourcesPath);
    }
}
