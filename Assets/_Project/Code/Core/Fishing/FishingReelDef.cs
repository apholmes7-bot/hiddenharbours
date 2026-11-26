using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The reel</b>, one of the three rod parts (owner, 2026-11-25: "reel speed is a different upgrade
    /// depending on reel bought"). It sets how fast the line comes up, and a small fight ease. One asset
    /// per reel, stable append-only id (rule 2).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Fishing Reel", fileName = "FishingReel")]
    public sealed class FishingReelDef : ScriptableObject
    {
        [Tooltip("Stable id, reel.snake_case, APPEND-ONLY.")]
        public string Id;

        [Tooltip("What the game calls this reel ('deep reel').")]
        public string DisplayName;

        [Tooltip("One line for the store and the pocket page. Greybox copy.")]
        [TextArea(2, 4)]
        public string Flavor;

        [Tooltip("The owned-gear id this part is sold and owned as. Empty until the store lists it.")]
        public string GearId;

        [Tooltip("The part's tier, 1 for the starter part. For lists and sorting; the numbers below are what play.")]
        [Min(1)] public int Tier = 1;

        [Tooltip("How fast the line comes up (m/s).")]
        public float ReelUpMetresPerSecond = 1.5f;

        [Tooltip("A small fight ease added to the rod's lift. Kept well under the skill's own fight help.")]
        [Min(0f)] public float FightEase;

        /// <summary>Why this reel cannot be fitted, or null when it can.</summary>
        public string Problem()
        {
            if (string.IsNullOrEmpty(Id) || !Id.StartsWith(RodFit.ReelIdPrefix, System.StringComparison.Ordinal))
                return $"id must start with '{RodFit.ReelIdPrefix}'";
            if (!(ReelUpMetresPerSecond > 0f)) return "reel speed must be above zero";
            if (!(FightEase >= 0f)) return "fight ease must not be negative";
            return null;
        }
    }
}
