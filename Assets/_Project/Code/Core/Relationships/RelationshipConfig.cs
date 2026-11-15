using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The relationship tunables</b> (relationships slice 1, rule 6): where each stage begins and the rule
    /// table that prices each event kind. One asset, <c>Data/Resources/Relationships/RelationshipConfig.asset</c>;
    /// every number is the owner's. The content validation test refuses thresholds out of order, a kind with
    /// no row or two rows, and a zero step or cap.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character/Relationship Config", fileName = "RelationshipConfig")]
    public sealed class RelationshipConfig : ScriptableObject
    {
        [Tooltip("Strength bounds and the edge of each stage (rival, strained, stranger, acquaintance, friend, close).")]
        public RelationshipStageThresholds Thresholds = RelationshipStageThresholds.Default;

        [Tooltip("One row per event kind: points per unit, a per-pair daily cap, and whether it may make rivals.")]
        public RelationshipRule[] Rules = new RelationshipRule[0];
    }
}
