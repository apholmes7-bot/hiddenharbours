using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>One region's fixed shore lines. The height texture identifies the region without scene edits.
    /// Tide marks are elevations above datum; the strength dial selects the kit's three authored steps.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Art/Terrain Shore Lines")]
    public sealed class TerrainShoreLinesDef : ScriptableObject
    {
        public enum Step { Off = -1, Low, Normal, High }

        public string Id;
        public Texture2D HeightTexture;
        public float SpringHigh = 2.2f;
        public float MeanHigh = 1.595f;
        public float MeanLow = -1.595f;
        public Step Turf = Step.Normal;
        public Step Scarp = Step.Normal;
        public Step Wrack = Step.Normal;
        public Step Weedline = Step.Normal;
        public Step Reefedge = Step.Normal;

        [Min(.125f)] public float AddressTexelMetres = 1f;
        [Min(.01f)] public float BlendMetres = .25f;
        [Min(.01f)] public float UnderlyingBlendMetres = 1f;
        [Header("Brow trace, inland from spring high, through the painted height field")]
        [Min(.125f)] public float BrowStepMetres = .5f;
        [Min(1f)] public float BrowSearchMetres = 40f;
        [Min(0f)] public float BrowRiseMetres = .25f;
        [Range(.01f, .99f)] public float BrowSlopeFallFraction = .35f;
        [Min(.001f)] public float ScarpMinimumSlope = .12f;
        [Min(.001f)] public float TurfMinimumSlope = 1f;

        public Step Strength(int kind) => kind switch
        {
            0 => Turf, 1 => Scarp, 2 => Wrack, 3 => Weedline, 4 => Reefedge, _ => Step.Off,
        };

        internal static TerrainShoreLinesDef Find(Texture2D height)
        {
            if (height == null) return null;
            foreach (var def in Resources.LoadAll<TerrainShoreLinesDef>("TerrainShoreLines"))
                if (def.HeightTexture == height) return def;
            return null;
        }
    }
}
