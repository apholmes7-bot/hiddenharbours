using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What a rig field is to the builder: structure the bake fixed, or a colour it swaps.</summary>
    public enum CharacterFieldKind
    {
        /// <summary>Moves geometry, so only a bake can change it (shape, garment, hat, …).</summary>
        Structure = 0,
        /// <summary>Picks a ramp and never touches geometry (skin, hair, outfit, …).</summary>
        Colour = 1,
    }

    /// <summary>One of the rig's <c>FIELDS</c> and every value its <c>OPTIONS</c> allow.</summary>
    [Serializable]
    public sealed class CharacterField
    {
        [Tooltip("The rig's own name for the field: shape, hairStyle, hatCol.")]
        public string Name = "";
        public CharacterFieldKind Kind;
        [Tooltip("Every value the rig's OPTIONS allow, as the rig prints them ('0.25', 'fair').")]
        public string[] Options = Array.Empty<string>();
        [Tooltip("Values the cast may have and the player is never offered (ruling 2: age child).")]
        public string[] CastOnly = Array.Empty<string>();

        public bool Allows(string value) => Options != null && Array.IndexOf(Options, value) >= 0;
        public bool IsCastOnly(string value) => CastOnly != null && Array.IndexOf(CastOnly, value) >= 0;
    }

    /// <summary>A preset's own value for every field, in the contract's field order (the rig's
    /// <c>normBuild</c> of it).</summary>
    [Serializable]
    public sealed class CharacterPresetValues
    {
        public string Key = "";
        public string[] Values = Array.Empty<string>();
    }

    /// <summary>Where a material's colours come from.</summary>
    public enum CharacterPaintSource
    {
        /// <summary>The ramp a colour field picks, as the ramps asset holds it.</summary>
        Axis = 0,
        /// <summary>One of the ramps asset's fixed ramps: no recipe changes it.</summary>
        Fixed = 1,
        /// <summary>A row of the rule's own table, keyed by one or more colour fields: the rig's mixed
        /// and single-shade colours (the stubble, the brow, the lids), lifted rather than recomputed.</summary>
        Table = 2,
    }

    /// <summary>One row of a <see cref="CharacterPaintSource.Table"/> rule.</summary>
    [Serializable]
    public sealed class CharacterPaintRow
    {
        [Tooltip("One value per TableFields entry, in that order.")]
        public string[] Keys = Array.Empty<string>();
        public Color32[] Colors = Array.Empty<Color32>();
    }

    /// <summary>How the rig paints one material role, and the tone it shades it with.</summary>
    [Serializable]
    public sealed class CharacterPaintRule
    {
        [Tooltip("The material role, which is the baked def's material name: skin, overD, stub.")]
        public string Role = "";
        public CharacterPaintSource Source;
        [Tooltip("Axis: the colour field whose ramp paints the role.")]
        public string Field = "";
        [Tooltip("Fixed: the key of the ramps asset's fixed ramp.")]
        public string FixedKey = "";
        [Tooltip("Table: the colour fields a row is keyed by.")]
        public string[] TableFields = Array.Empty<string>();
        public CharacterPaintRow[] Rows = Array.Empty<CharacterPaintRow>();

        [Header("The tone, as the bake writes it on the def's material")]
        public int Offset;
        public float Gain = 1f;
        public float Bias;
        public int ToneLo;
        public int ToneHi;

        /// <summary>The row whose keys are the values of <see cref="TableFields"/> in
        /// <paramref name="values"/>, or null.</summary>
        public CharacterPaintRow Row(CharacterRigContractDef contract, string[] values)
        {
            if (Rows == null || TableFields == null || contract == null || values == null) return null;
            foreach (CharacterPaintRow row in Rows)
            {
                if (row?.Keys == null || row.Keys.Length != TableFields.Length) continue;
                bool match = true;
                for (int k = 0; k < TableFields.Length && match; k++)
                {
                    int f = contract.FieldIndex(TableFields[k]);
                    match = f >= 0 && f < values.Length &&
                            string.Equals(row.Keys[k], values[f], StringComparison.Ordinal);
                }
                if (match) return row;
            }
            return null;
        }
    }

    /// <summary>
    /// <b>The rig's offer and its paint, as data</b> (character PR 2b, ADR 0051): every field and
    /// option rig 10.3 accepts, the ten presets the bake bakes, the NPC presets it holds back, and
    /// for each of the rig's material roles where its colours come from. Lifted from the rig run in V8
    /// by the editor (<c>CharacterBuilderContractBaker</c>), never hand-edited, so the builder carries
    /// no rig literal and runs no JS.
    ///
    /// <para><b>Colour never touches structure.</b> Candidate C of the ADR: the bodies are the ten
    /// baked <see cref="CharacterSkinDef"/>s and a recipe only swaps the colours their materials
    /// ramp through. The oracle (<c>CharacterBuilderOracleTests</c>) holds that to the rig, byte for
    /// byte, on every preset and every colour option.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Rig Contract", fileName = "CharacterContract")]
    public sealed class CharacterRigContractDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only: contract.character_iso10.")]
        public string Id = "contract.character_iso10";

        [Header("Provenance (so a stale asset is obvious, not silent)")]
        [Tooltip("The rig and revision this was lifted from: characterIsoRig10 10.3.")]
        public string SourceRig = "";
        [Tooltip("The sha256 of the rig's script it was lifted from.")]
        public string SourceRigSha256 = "";

        [Header("The offer")]
        [Tooltip("The rig's FIELDS, in its order: CharacterRecipe.Fields.")]
        public CharacterField[] Fields = Array.Empty<CharacterField>();
        [Tooltip("The presets the bake bakes (the rig's CAST10), with their own values.")]
        public CharacterPresetValues[] Presets = Array.Empty<CharacterPresetValues>();
        [Tooltip("Presets the rig builds that the builder refuses: the NPCs, who come with the wardrobe.")]
        public string[] NpcPresets = Array.Empty<string>();

        [Header("The paint")]
        [Tooltip("One rule per material role of the rig's shading contract, in its order.")]
        public CharacterPaintRule[] Paint = Array.Empty<CharacterPaintRule>();

        public int FieldIndex(string name)
        {
            if (Fields == null || string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < Fields.Length; i++)
                if (Fields[i] != null && string.Equals(Fields[i].Name, name, StringComparison.Ordinal)) return i;
            return -1;
        }

        public CharacterField Field(string name)
        {
            int i = FieldIndex(name);
            return i >= 0 ? Fields[i] : null;
        }

        public CharacterPresetValues Preset(string key)
        {
            if (Presets == null || string.IsNullOrEmpty(key)) return null;
            foreach (CharacterPresetValues p in Presets)
                if (p != null && string.Equals(p.Key, key, StringComparison.Ordinal)) return p;
            return null;
        }

        public bool IsNpcPreset(string key) =>
            NpcPresets != null && !string.IsNullOrEmpty(key) && Array.IndexOf(NpcPresets, key) >= 0;

        public CharacterPaintRule Rule(string role)
        {
            if (Paint == null || string.IsNullOrEmpty(role)) return null;
            foreach (CharacterPaintRule r in Paint)
                if (r != null && string.Equals(r.Role, role, StringComparison.Ordinal)) return r;
            return null;
        }

        /// <summary>The preset keys, comma-separated, for a refusal that lists what is offered.</summary>
        public string PresetList()
        {
            if (Presets == null) return "";
            var keys = new string[Presets.Length];
            for (int i = 0; i < Presets.Length; i++) keys[i] = Presets[i]?.Key ?? "";
            return string.Join(", ", keys);
        }
    }
}
