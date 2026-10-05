using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Authored village data; id references are resolved by VillagePlanDerivation.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Village/LightPostDef", fileName = "LightPostDef")]
    public sealed class LightPostDef : ScriptableObject
    {
        public string Id;
        public string LotId;
        public string CommonsId;
        public string RouteId;
        public string KitPiece;
        public Vector2 Position;
        public string Preset;
        public string LitBy;
        public float Reach;
        public bool CastsShadow;
    }
}
