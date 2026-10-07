using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Authored village data; id references are resolved by VillagePlanDerivation.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Village/YardDef", fileName = "YardDef")]
    public sealed class YardDef : ScriptableObject
    {
        public string Id;
        public string LotId;
        public YardFence Fence;
        public MownStyle Mown;
        public Vector2[] Outline = Array.Empty<Vector2>();
        public Vector2 Gate;
        public VillagePlacement[] Dressing = Array.Empty<VillagePlacement>();
        public VillageFenceSummary FenceSummary;
    }
}
