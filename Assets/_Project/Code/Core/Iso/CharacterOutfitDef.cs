using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One thing the player can be wearing</b> — a named pairing of an outfit colour and a shirt
    /// colour, as data. One asset per file under <c>Data/Characters/Outfits</c>, keyed by a stable,
    /// append-only <see cref="Id"/> (<c>outfit.snake_case</c>).
    ///
    /// <para><b>Why this Def carries exactly two axes, and it is a measurement rather than a
    /// preference.</b> The player is drawn from 37 baked <c>Fisher_*</c> sheets, and every one of the
    /// 1,034,817 opaque pixels in them is either a literal entry of one of the character rig's ramps
    /// or that entry run through the rig's tinted-outline pass — nothing else, no anti-aliasing, no
    /// shading off the palette. Classifying all of them says which colour axes the art actually
    /// carries on THIS build:</para>
    ///
    /// <list type="bullet">
    ///   <item><c>outfit</c> — 31.6% of the fisher's pixels (the overalls). Six keys.</item>
    ///   <item><c>hair</c> — 20.6%. Nine keys.</item>
    ///   <item><c>skin</c> — 16.4%. Nine keys.</item>
    ///   <item><c>shirt</c> — 13.8%. Seven keys.</item>
    ///   <item><c>hatCol</c>, <c>apronCol</c>, <c>eyes</c> — <b>ZERO pixels each</b>. The fisher's
    ///   baked build wears no hat and no apron, and at 32 px/m the head rig draws the iris in
    ///   <c>INK</c> rather than the eye ramp. A wardrobe offering these would be a Def promising
    ///   art that is not on the sheet.</item>
    /// </list>
    ///
    /// <para>The remaining 17.3% is boots, a belt and buckles — and those are <b>not axes at all</b>:
    /// <c>BOOT</c>, <c>LEATHER</c> and <c>BRASS</c> are fixed ramps in the rig with no options, so no
    /// wardrobe can ever recolour them without an art-director change.</para>
    ///
    /// <para><b>Of the four live axes, a wardrobe holds two</b> (owner ruling, 2026-08-18): outfit and
    /// shirt are clothes; skin and hair are who you are, and ADR 0029 already sites those in the
    /// character-creator slice. The distinction costs nothing to honour later — the swap mechanism is
    /// identical for all four, so widening the wardrobe is adding fields here and presets on disk, not
    /// new code.</para>
    ///
    /// <para><b>Presets, not a colour picker.</b> The rig's ramps are a solved palette: each material
    /// holds ~3 steps so cloth does not read as speckle at 32 px/m. A free RGB choice would leave that
    /// palette immediately, which is why what the player picks is a NAMED pairing an art director
    /// approved, not two sliders.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Outfit", fileName = "CharacterOutfit")]
    public class CharacterOutfitDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (outfit.snake_case, e.g. outfit.harbour_teal). An id that has " +
                 "shipped may sit in a save as what the player is wearing, so it must resolve forever.")]
        public string Id = "outfit.example";

        [Tooltip("What the player reads on the wardrobe's card. Display only; never matched on.")]
        public string Label = "";

        [Header("The colours — keys into CharacterRampsDef, never raw RGB")]
        [Tooltip("Key into the ramps' outfit axis (teal, navy, oil, rust, slate, moss). The overalls: " +
                 "the single biggest surface on the fisher.")]
        public string Outfit = "";

        [Tooltip("Key into the ramps' shirt axis (white, cream, navy, red, moss, slate, ochre). The " +
                 "sleeves and collar showing either side of the bib.")]
        public string Shirt = "";

        /// <summary>
        /// The colour choices as (axis, key) pairs, skipping the empty ones — the exact list a palette
        /// swap has to apply and the exact list content validation has to check against
        /// <see cref="CharacterRampsDef"/>. One place, so the two can never disagree about what an axis
        /// is called. Mirrors <see cref="CharacterBuildDef.ColourOverrides"/> deliberately: a wardrobe
        /// entry and a cast build are the same kind of statement about a person, made by different
        /// authors.
        ///
        /// <para>An empty key means "leave that axis as the baked build wears it", so an outfit that
        /// changes only the shirt is a complete, legal outfit.</para>
        /// </summary>
        public (string axis, string key)[] ColourChoices()
        {
            var all = new (string axis, string key)[] { ("outfit", Outfit), ("shirt", Shirt) };

            int n = 0;
            foreach (var p in all) if (!string.IsNullOrEmpty(p.key)) n++;
            var set = new (string, string)[n];
            int i = 0;
            foreach (var p in all) if (!string.IsNullOrEmpty(p.key)) set[i++] = p;
            return set;
        }

        /// <summary>True when this outfit names at least one colour and so has something to apply.</summary>
        public bool HasAnyChoice => !string.IsNullOrEmpty(Outfit) || !string.IsNullOrEmpty(Shirt);
    }
}
