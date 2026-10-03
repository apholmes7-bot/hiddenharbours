using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Authored village data; id references are resolved by VillagePlanDerivation.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Village/VillagePlanDef", fileName = "VillagePlanDef")]
    public sealed class VillagePlanDef : ScriptableObject
    {
        public string Id;
        public VillageNamedFloat[] Lines = Array.Empty<VillageNamedFloat>();
        public VillageNamedFloat[] Tunables = Array.Empty<VillageNamedFloat>();
        public VillageNamedPoint[] FixedPoints = Array.Empty<VillageNamedPoint>();
        public VillageFootprint[] Footprints = Array.Empty<VillageFootprint>();
        public string[] LotIds = Array.Empty<string>();
        public string[] YardIds = Array.Empty<string>();
        public string[] RouteIds = Array.Empty<string>();
        public string[] CommonsIds = Array.Empty<string>();
        public string[] LightIds = Array.Empty<string>();
        public VillagePiece[] Pieces = Array.Empty<VillagePiece>();
        public VillagePlacement[] Roadside = Array.Empty<VillagePlacement>();
        public Vector2 HearthRingPosition;
        public float HearthRingRadius;
        public VillageTreeNode[] Tree = Array.Empty<VillageTreeNode>();
        public VillageCycleLink[] CycleLinks = Array.Empty<VillageCycleLink>();
        public VillageWindowAnnotation[] Windows = Array.Empty<VillageWindowAnnotation>();
        public float WindowWidth;
        public float WindowHeight;
        public Vector4 WindowExtent;
        public VillageLampComparison[] LampComparisons = Array.Empty<VillageLampComparison>();
    }
}
