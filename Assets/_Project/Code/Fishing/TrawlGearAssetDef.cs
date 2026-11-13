using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>One registered gear entity. Review is a data gate only; it grants no ground,
    /// occupant, inventory or scene activation rights. Unreviewed assets fail closed.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Trawl Gear", fileName = "TrawlGear")]
    public sealed class TrawlGearAssetDef : ScriptableObject
    {
        public string DisplayName;
        public TrawlGearDef Spec;
        public bool ReviewedForUse;

        public bool TryGetReviewedSpec(out TrawlGearDef spec)
        {
            spec = default;
            if (!ReviewedForUse || !Spec.IsValid) return false;
            spec = Spec;
            return true;
        }
    }
}
