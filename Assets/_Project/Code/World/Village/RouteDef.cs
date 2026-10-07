using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Authored village data; id references are resolved by VillagePlanDerivation.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Village/RouteDef", fileName = "RouteDef")]
    public sealed class RouteDef : ScriptableObject
    {
        public string Id;
        public string Class;
        public float WidthMetres;
        public Vector2[] Points = Array.Empty<Vector2>();
        public string LotId;
    }
}
