using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The rod itself</b>, one of the three parts a fishing rod is made from (owner, 2026-11-25: "rods
    /// should be customizable, with different cast reaches"). The rod's length gives the cast reach; it
    /// also sets how the bite comes (the delays and the auto-hook), how plainly the tip shows a take, its
    /// own lift in the fight, and the old fight's easy and hard ends. The line and the reel are separate
    /// parts (<see cref="FishingLineDef"/>, <see cref="FishingReelDef"/>); <see cref="RodFit.Compose"/>
    /// puts the three together. One asset per rod, stable append-only id (rule 2); every number the
    /// owner's to tune (rule 6).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Rod Part", fileName = "RodPart")]
    public sealed class RodBlankDef : ScriptableObject
    {
        [Tooltip("Stable id, rod.snake_case, APPEND-ONLY.")]
        public string Id;

        [Tooltip("What the game calls this rod ('coast rod').")]
        public string DisplayName;

        [Tooltip("One line for the store and the pocket page. Greybox copy.")]
        [TextArea(2, 4)]
        public string Flavor;

        [Tooltip("The owned-gear id this part is sold and owned as. Empty until the store lists it.")]
        public string GearId;

        [Tooltip("The part's tier, 1 for the starter part. For lists and sorting; the numbers below are what play.")]
        [Min(1)] public int Tier = 1;

        [Tooltip("How far this rod casts (m): its length.")]
        public float CastMetres = 12f;

        [Tooltip("Shortest wait before a bite (s).")]
        public float MinBiteDelaySeconds = 1.5f;

        [Tooltip("Longest wait before a bite (s).")]
        public float MaxBiteDelaySeconds = 4f;

        [Tooltip("How long a plain bite stays on before it is gone (s).")]
        public float AutoHookSeconds = 1.2f;

        [Tooltip("How plainly the tip shows a true take (multiplies the take window).")]
        public float TakeWindowMultiplier = 1f;

        [Tooltip("The rod's own lift in the fight (multiplies how fast a won pull brings the fish in). The " +
                 "line's and the reel's fight ease are added to it.")]
        public float LandingMultiplier = 1f;

        [Header("The old fight's easy and hard ends (species strength 0 and 1)")]
        public float TensionRiseEasy = 0.60f;
        public float TensionRiseHard = 1.00f;
        public float TensionFallEasy = 0.70f;
        public float TensionFallHard = 0.50f;
        public float LandingEasy = 0.40f;
        public float LandingHard = 0.22f;
        public float SurgeIntervalEasy = 3f;
        public float SurgeIntervalHard = 1.6f;
        public float SurgeDurationSeconds = 0.8f;
        public float SurgePressureEasy = 0.15f;
        public float SurgePressureHard = 0.40f;

        /// <summary>Why this rod cannot be fished, or null when it can. The fitted profile's own check
        /// covers the numbers; this adds the id.</summary>
        public string Problem()
        {
            if (string.IsNullOrEmpty(Id) || !Id.StartsWith(RodFit.RodIdPrefix, System.StringComparison.Ordinal))
                return $"id must start with '{RodFit.RodIdPrefix}'";
            return null;
        }
    }
}
