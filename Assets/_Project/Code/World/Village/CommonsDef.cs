using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Authored village data; id references are resolved by VillagePlanDerivation.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Village/CommonsDef", fileName = "CommonsDef")]
    public sealed class CommonsDef : ScriptableObject
    {
        public string Id;
        public Vector2[] Outline = Array.Empty<Vector2>();
        public VillagePlacement[] Furniture = Array.Empty<VillagePlacement>();
        public VillageStation[] Stations = Array.Empty<VillageStation>();
    }
}
