using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The line on the reel</b>, one of the three rod parts (owner, 2026-11-25: "line strength is
    /// different purchasable lines, heavier is stronger but maybe deters certain smaller fish"). It sets
    /// how deep the hook can go (its length), how much strain it takes (its strength), which small fish
    /// it puts off, and a small fight ease.
    ///
    /// <para><b>The small-fish rule.</b> A species whose TOP weight (its own data's <c>MaxWeightKg</c>) is
    /// under <see cref="ShyBelowKg"/> bites less, by <see cref="RodFit.ShyFactor"/>: the smallest fish down
    /// to <see cref="ShyDamp01"/> of their usual rate, a fish at the mark not at all. Nothing rolls on its
    /// own; the catch roll keeps its single draw (see the design, "Rods from parts").</para>
    ///
    /// <para>Id prefix <c>fishing_line.</c>, because <c>line.</c> already names the store's stock lines.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Fishing Line", fileName = "FishingLine")]
    public sealed class FishingLineDef : ScriptableObject
    {
        [Tooltip("Stable id, fishing_line.snake_case, APPEND-ONLY.")]
        public string Id;

        [Tooltip("What the game calls this line ('heavy line').")]
        public string DisplayName;

        [Tooltip("One line for the store and the pocket page. Greybox copy.")]
        [TextArea(2, 4)]
        public string Flavor;

        [Tooltip("The owned-gear id this part is sold and owned as. Empty until the store lists it.")]
        public string GearId;

        [Tooltip("The part's tier, 1 for the starter part. For lists and sorting; the numbers below are what play.")]
        [Min(1)] public int Tier = 1;

        [Tooltip("How much line is on (m): the deepest the hook can be dropped.")]
        public float LineMetres = 60f;

        [Tooltip("The line's strength (rising strain in the fight is divided by it).")]
        public float Strength = 1f;

        [Tooltip("Fish whose top weight is under this (kg) are put off by this line. 0 puts nothing off.")]
        [Min(0f)] public float ShyBelowKg;

        [Tooltip("How much the smallest fish still bite on this line (0..1 of their usual rate).")]
        [Range(0f, 1f)] public float ShyDamp01 = 1f;

        [Tooltip("A small fight ease added to the rod's lift. Kept well under the skill's own fight help.")]
        [Min(0f)] public float FightEase;

        /// <summary>Why this line cannot be fitted, or null when it can.</summary>
        public string Problem()
        {
            if (string.IsNullOrEmpty(Id) || !Id.StartsWith(RodFit.LineIdPrefix, System.StringComparison.Ordinal))
                return $"id must start with '{RodFit.LineIdPrefix}'";
            if (!(LineMetres > 0f) || !(Strength > 0f)) return "length and strength must be above zero";
            if (!(ShyBelowKg >= 0f) || !(ShyDamp01 >= 0f && ShyDamp01 <= 1f)) return "shyness out of range";
            if (!(FightEase >= 0f)) return "fight ease must not be negative";
            return null;
        }
    }
}
