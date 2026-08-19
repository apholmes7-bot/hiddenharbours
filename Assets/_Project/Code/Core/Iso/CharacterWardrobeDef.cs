using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What the player can be wearing, and what they were baked wearing</b> — the wardrobe's whole
    /// contents as one asset: the colour space to resolve keys in, the outfit the sheets came out of
    /// the oven in, the ordered list of outfits the player may choose, and the two facts about the
    /// rig's outline pass that a recolour cannot be computed without.
    ///
    /// <para><b>Why the BAKED keys live here rather than being inferred.</b> A swap replaces one colour
    /// with another, so it needs both ends. The target end is the player's choice; the source end is a
    /// fact about the ART — the <c>fisher</c> preset was baked in <c>outfit: teal</c> and
    /// <c>shirt: white</c>, and if a future drop bakes them in something else, every swap in the game
    /// silently stops matching any pixel. Naming them here makes that a one-line asset edit with a test
    /// behind it (<c>CharacterPaletteContentTests</c> reads
    /// <c>docs/art/rigs/character/presets.json</c> and asserts these agree), rather than a
    /// recolour that quietly does nothing.</para>
    ///
    /// <para><b>Why the outline facts live here rather than on <see cref="CharacterRampsDef"/>.</b>
    /// The ramps asset is GENERATED from the rig kit's <c>options.json</c>, and <c>options.json</c>
    /// does not carry the outline mix — it is in the rig's own source. Adding fields to a generated,
    /// already-shipped asset is the trap where a YAML that predates the field silently reads as the C#
    /// default; this asset is hand-authored, so its YAML always carries what it claims.</para>
    ///
    /// <para><b>Content is data (rule 2), and so is the wardrobe's ORDER.</b> The picker cycles
    /// <see cref="Outfits"/> in the order they sit here, so what the player sees first is an authoring
    /// decision, not a sort.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Wardrobe", fileName = "CharacterWardrobe")]
    public class CharacterWardrobeDef : ScriptableObject
    {
        /// <summary>
        /// Where the shipped wardrobe lives for a runtime load, following the library convention
        /// (<c>IconLibrary</c>, <c>FishSpeciesLibrary</c>, <c>SeaweedLibrary</c>): one asset under
        /// <c>Assets/_Project/Resources</c>, named by the type that reads it, so no scene and no
        /// builder has to carry a reference to it.
        /// </summary>
        public const string ResourcesPath = "CharacterWardrobe";

        [Header("Identity")]
        [Tooltip("Stable id, append-only: wardrobe.<who>. One wardrobe per character who has one; the " +
                 "player's is wardrobe.fisher.")]
        public string Id = "wardrobe.fisher";

        [Header("The colour space")]
        [Tooltip("The ramps every key below resolves in. Generated from the rig kit — never hand-edited.")]
        public CharacterRampsDef Ramps;

        [Header("What the SHEETS were baked wearing — the source end of every swap")]
        [Tooltip("The outfit key the baked Fisher_* sheets carry. The rig's fisher preset: teal.")]
        public string BakedOutfit = "teal";

        [Tooltip("The shirt key the baked Fisher_* sheets carry. The rig's fisher preset: white.")]
        public string BakedShirt = "white";

        [Header("The rig's tinted-outline pass — 27% of the figure's pixels")]
        [Tooltip("The rig's KEY colour (#101a19): silhouette and depth edges are drawn as a mix of " +
                 "this and the local ramp colour, so they must be swapped alongside it.")]
        public Color32 KeyTintColour = new Color32(0x10, 0x1a, 0x19, 0xff);

        [Tooltip("How much LOCAL colour that outline keeps — the rig's 0.22. Not a look tunable: " +
                 "change it and the swap stops matching the pixels the rig actually wrote.")]
        [Range(0f, 1f)] public float KeyTintAmount = 0.22f;

        [Header("The contents, in the order the picker shows them")]
        [Tooltip("Every outfit the player may wear. Append-only in spirit: an id that has shipped may " +
                 "be sitting in a save.")]
        public CharacterOutfitDef[] Outfits = Array.Empty<CharacterOutfitDef>();

        [Tooltip("What a new game wears, and what a save with an unreadable outfit falls back to. " +
                 "Should name the outfit whose colours ARE the baked ones, so a new player looks " +
                 "exactly like the shipped art.")]
        public string DefaultOutfitId = "";

        /// <summary>How many outfits this wardrobe holds. The one number a picker wants.</summary>
        public int Count => Outfits?.Length ?? 0;

        /// <summary>
        /// One outfit by id, or null. Ordinal match — these are ids, not prose. Returns null rather
        /// than the default: resolving an unknown id to something wearable is how a retired outfit
        /// would go unnoticed, and the caller is better placed to say so out loud.
        /// </summary>
        public CharacterOutfitDef Outfit(string id)
        {
            if (Outfits == null || string.IsNullOrEmpty(id)) return null;
            foreach (var o in Outfits)
                if (o != null && string.Equals(o.Id, id, StringComparison.Ordinal)) return o;
            return null;
        }

        /// <summary>The index of an id in <see cref="Outfits"/>, or -1. What a cycling picker steps.</summary>
        public int IndexOf(string id)
        {
            if (Outfits == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Outfits.Length; i++)
                if (Outfits[i] != null && string.Equals(Outfits[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// The axis changes that carry the baked look to <paramref name="outfit"/> — the input
        /// <see cref="CharacterPalette.TryBuild"/> takes. An outfit that leaves an axis empty keeps
        /// what was baked, which is why the pair is assembled here rather than in the Def: only the
        /// wardrobe knows what "what was baked" means.
        /// </summary>
        public List<CharacterAxisChange> ChangesFor(CharacterOutfitDef outfit)
        {
            var list = new List<CharacterAxisChange>(2);
            if (outfit == null) return list;

            if (!string.IsNullOrEmpty(outfit.Outfit))
                list.Add(new CharacterAxisChange("outfit", BakedOutfit, outfit.Outfit));
            if (!string.IsNullOrEmpty(outfit.Shirt))
                list.Add(new CharacterAxisChange("shirt", BakedShirt, outfit.Shirt));

            return list;
        }

        /// <summary>
        /// Resolve an outfit id to its swap list in one call — the whole recolour, refusals included.
        /// An id this wardrobe does not hold is <see cref="CharacterPaletteStatus.UnknownKey"/> with
        /// an empty list, never a quiet fallback to the default.
        /// </summary>
        public CharacterPaletteStatus TryBuildPalette(
            string outfitId, List<CharacterColourSwap> into, out string reason)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));

            var outfit = Outfit(outfitId);
            if (outfit == null)
            {
                into.Clear();
                reason = $"'{outfitId}' is not an outfit in {name} — the wardrobe holds " +
                         $"{Count} outfit(s), so nothing is applied and the baked look stands";
                return CharacterPaletteStatus.UnknownKey;
            }

            return CharacterPalette.TryBuild(
                Ramps, ChangesFor(outfit), KeyTintColour, KeyTintAmount, into, out reason);
        }
    }
}
