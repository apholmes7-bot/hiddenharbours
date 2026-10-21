using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    // Serializable authored rows. Positions are world XY; distances are ground metres.
    [Serializable]
    public sealed class VillageNamedFloat
    {
        public string Name;
        public float Value;
    }

    [Serializable]
    public sealed class VillageNamedPoint
    {
        public string Name;
        public Vector2 Position;
    }

    [Serializable]
    public sealed class VillageFootprint
    {
        public string Id;
        public Vector4 LocalBounds;
        public string Source;
        public bool Provisional;
    }

    [Serializable]
    public sealed class VillagePlacement
    {
        public string PieceId;
        public string Kind;
        public Vector2 Position;
        public string RouteId;
        public string Road;
        public string MetadataJson;
    }

    [Serializable]
    public sealed class VillagePiece
    {
        public string Id;
        public string Kit;
        public string Piece;
        public Vector2 Position;
        public float Z;
        public int Dir;
        public bool Pending;
        public string OptionsJson;
        public string MetadataJson;
        // A grouped fence piece represents one caster anchor per panel. Empty uses Position.
        public Vector2[] CasterAnchors = Array.Empty<Vector2>();
    }

    [Serializable]
    public sealed class VillageTreeNode
    {
        public string Node;
        public string Parent;
        public Vector2 Position;
        public Vector2[] Via = Array.Empty<Vector2>();
        public string Reason;
    }

    [Serializable]
    public sealed class VillageCycleLink
    {
        public string Link;
        public string[] Between = Array.Empty<string>();
        public string Via;
        public string WouldServe;
    }

    [Serializable]
    public sealed class VillageWindowAnnotation
    {
        public string Key;
        public Vector4 Bounds;
        public string Focal;
        public bool Wide;
    }

    [Serializable]
    public sealed class VillageStation
    {
        public string Id;
        public Vector2 Position;
        public string Node;
        public string Who;
    }

    [Serializable]
    public sealed class VillageLampComparison
    {
        public string Kind;
        public Vector2 Position;
        public string Owner;
        public string Preset;
        public float Reach;
        public string When;
        public bool Active;
    }

    [Serializable]
    public sealed class VillageFenceSummary
    {
        public int Panels;
        public int Posts;
        public int Gates;
        public int Offcuts;
        public float OffcutMetres;
        public float Metres;
    }

}
