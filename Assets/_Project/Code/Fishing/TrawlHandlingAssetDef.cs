using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>One registered handling entity. The snapshot retains the existing plain logic's
    /// station and work-time rules; reviewing it does not activate a vessel or automate work.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Trawl Handling", fileName = "TrawlHandling")]
    public sealed class TrawlHandlingAssetDef : ScriptableObject
    {
        public string DisplayName;
        public TrawlHandlingDef Spec;
        public bool ReviewedForUse;

        public bool TryGetReviewedSpec(out TrawlHandlingDef spec)
        {
            spec = default;
            if (!ReviewedForUse || !Spec.IsValid) return false;
            spec = Spec;
            return true;
        }
    }
}
