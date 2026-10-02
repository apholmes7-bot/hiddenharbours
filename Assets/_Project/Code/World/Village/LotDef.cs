using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Authored village data; id references are resolved by VillagePlanDerivation.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Village/LotDef", fileName = "LotDef")]
    public sealed class LotDef : ScriptableObject
    {
        public string Id;
        public string BuildingId;
        public string BakeKey;
        public string StreetId;
        public Vector2 Position;
        public Vector2 DoorOffset;
        public string FootprintId;
        public int FacingCell;
        public string Entry;
        public string DoorFaces;
        public float RoofTop;
        public float ArtBottom;
        public Vector2 Gate;
        public Vector2 Approach;
        public Vector2[] Outline = Array.Empty<Vector2>();
        public float SetbackMetres;
        public float FrontageMetres;
        public float DepthMetres;
        public string WalkId;
    }
}
