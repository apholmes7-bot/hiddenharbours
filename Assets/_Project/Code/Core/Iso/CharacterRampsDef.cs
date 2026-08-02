using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One colour choice on one axis: the rig's key, and the ramp it stands for.</summary>
    [Serializable]
    public sealed class CharacterRamp
    {
        [Tooltip("Stable id, append-only (CLAUDE.md §5): ramp.<axis>.<key>, e.g. ramp.skin.fair.")]
        public string Id;

        [Tooltip("The rig's OWN key for this value — 'fair', 'ginger', 'teal'. What a build asks for.")]
        public string Key;

        [Tooltip("What the owner reads in a creator or a shop. Display only; never matched on.")]
        public string Label;

        [Tooltip("The ramp, DARK → LIGHT, exactly as the rig ships it. The rig holds each material " +
                 "to ~3 of these steps so cloth does not read as speckle at 32 px/m — so the count " +
                 "is part of the art, not padding to a fixed length.")]
        public Color32[] Shades = Array.Empty<Color32>();

        public bool IsUsable => !string.IsNullOrEmpty(Key) && Shades != null && Shades.Length > 0;
    }

    /// <summary>Every legal value of one colour axis, in the rig's own order.</summary>
    [Serializable]
    public sealed class CharacterRampAxis
    {
        [Tooltip("The build key this axis sets: skin, hair, outfit, shirt, hatCol, apronCol, eyes.")]
        public string Axis;

        [Tooltip("The rig's default for this axis — what a build that omits it gets.")]
        public string DefaultKey;

        public CharacterRamp[] Ramps = Array.Empty<CharacterRamp>();
    }

    /// <summary>
    /// <b>The character's COLOUR space, as data.</b> Seven axes of ramps — skin, hair, outfit, shirt,
    /// hat colour, apron colour, eyes — lifted verbatim from the rig kit's <c>options.json</c>.
    ///
    /// <para><b>Why colour is data and structure is not (ADR 0029).</b> No JavaScript runs in a shipped
    /// build (ADR 0021), so nothing at runtime can ask the rig to draw a person. Structure — sex, age,
    /// garment, hat, hair, beard, proportions — changes GEOMETRY, so it can only arrive pre-baked as
    /// sheets. Colour does not: every material on the figure is drawn from a ramp, so a different
    /// colour is the same pixels with a different palette. Holding the ramps here is what lets a
    /// shirt, an outfit or a hat band be recoloured at runtime, instantly and for free — which is in
    /// turn what makes clothes purchasable and swappable without a re-bake per combination.</para>
    ///
    /// <para><b>This asset is generated, not authored.</b> <c>CharacterRampsBuilder</c> writes it from
    /// <c>docs/art/rigs/character/options.json</c>, so the ramps in the game are the ramps in the rig
    /// by construction rather than by transcription — the same discipline as reading cell geometry
    /// from the rig instead of a README. Re-run it when a drop revises a palette; do not hand-edit
    /// the colours, they will be overwritten.</para>
    ///
    /// <para><b>Ids are append-only.</b> A <c>ramp.shirt.ochre</c> that ships in a save (as the
    /// player's build, or as a wardrobe item they bought) must keep resolving forever. A drop that
    /// RETIRES a key is a content migration, not a re-run.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Ramps", fileName = "CharacterRamps")]
    public class CharacterRampsDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only: ramps.character_iso.")]
        public string Id = "ramps.character_iso";

        [Header("Provenance (so a stale asset is obvious, not silent)")]
        [Tooltip("The rig file these ramps were lifted from.")]
        public string SourceRig;

        [Tooltip("The options contract they were generated from.")]
        public string SourceOptions;

        [Header("The seven colour axes, in the kit's order")]
        public CharacterRampAxis[] Axes = Array.Empty<CharacterRampAxis>();

        /// <summary>The axis block for a build key, or null. Ordinal match — these are ids, not prose.</summary>
        public CharacterRampAxis Axis(string axis)
        {
            if (Axes == null || string.IsNullOrEmpty(axis)) return null;
            foreach (var a in Axes)
                if (a != null && string.Equals(a.Axis, axis, StringComparison.Ordinal)) return a;
            return null;
        }

        /// <summary>
        /// One ramp by axis and the rig's key. Returns null rather than a fallback: silently resolving
        /// an unknown colour to a default is precisely the rigs' own failure mode, and reproducing it
        /// in the engine would hide a retired key until someone noticed the wrong shirt.
        /// </summary>
        public CharacterRamp Ramp(string axis, string key)
        {
            var a = Axis(axis);
            if (a?.Ramps == null || string.IsNullOrEmpty(key)) return null;
            foreach (var r in a.Ramps)
                if (r != null && string.Equals(r.Key, key, StringComparison.Ordinal)) return r;
            return null;
        }

        /// <summary>Total ramps across every axis — the one number a budget or a log wants.</summary>
        public int RampCount
        {
            get
            {
                int n = 0;
                if (Axes != null)
                    foreach (var a in Axes) n += a?.Ramps?.Length ?? 0;
                return n;
            }
        }
    }
}
