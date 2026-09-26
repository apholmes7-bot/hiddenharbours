using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The PAINTED still-water source (ADR 0046) — a plain POCO that decodes a region's <b>still-level
    /// map</b> once and answers <see cref="IStillWater.StillLevelAt"/>: the fresh-water surface standing
    /// above the tide at a plan point (a pond, a brook's fresh reach), or
    /// <see cref="StillWaterLevels.None"/> where there is none. <see cref="PaintedHeightMap"/> builds it from
    /// its optional still-level texture and <see cref="PaintedTidalTerrain"/> registers it into
    /// <see cref="GameServices.StillWater"/>, beside the terrain.
    ///
    /// <para><b>The map.</b> The R channel holds the still surface on the HEIGHT MAP's own scale — the same
    /// world rectangle and the same <c>min..max</c> range — at 16 bits; <b>code 0 is "no still water"</b>.
    /// It is DERIVED data (the terrain plan's ponds and brook steps, rendered by a deriver), never painted
    /// by hand and never saved (rule 5).</para>
    ///
    /// <para><b>Render == sim.</b> The water and tidal-face shaders sample the SAME texture (through
    /// <see cref="Map"/>) with the same world→uv mapping and bilinear filtering, THEN apply the same "0 is
    /// none" decode (<see cref="StillWaterLevels.DecodeCode01"/>). So this filters the normalized CODES —
    /// through <see cref="PaintedHeightField"/>'s shader-matching bilinear — and decodes the filtered value,
    /// never the other way round: filtering decoded levels would average a pond with −∞ at its rim. Off the
    /// rectangle there is no still water (the height map clamps to its edge; a pond must not).</para>
    ///
    /// <para><b>Performance (rule 7).</b> One decode into a flat <c>float[]</c> (4 bytes a texel: 6.3 MB for
    /// a 1520×1040 map), then four array reads and a lerp per query — never a texture read per call.</para>
    /// </summary>
    public sealed class PaintedStillWater : IStillWater
    {
        // The filtered NORMALIZED CODES (0..1, 0 = none), not metres — see the class doc for why.
        private readonly PaintedHeightField _codes;
        private readonly float _minLevel;
        private readonly float _maxLevel;
        private readonly StillWaterMap _map;

        /// <summary>
        /// Decode <paramref name="texture"/>'s R channel (at the texture's own precision,
        /// <see cref="PaintedHeightMap.ReadNormalizedR"/>) over the world rectangle given by its centre and
        /// size, on the <paramref name="minLevel"/>..<paramref name="maxLevel"/> scale. A null or
        /// non-readable texture reports no still water anywhere, with an unbound <see cref="Map"/> — the
        /// render must not draw what the sim cannot read.
        /// </summary>
        public PaintedStillWater(Texture2D texture, Vector2 worldCenter, Vector2 worldSize,
                                 float minLevel, float maxLevel)
        {
            _minLevel = minLevel;
            _maxLevel = maxLevel;

            float[] codes = PaintedHeightMap.ReadNormalizedR(texture);
            if (codes == null) return;   // _codes null, _map default: no still water, nothing to draw

            _codes = new PaintedHeightField(codes, texture.width, texture.height, worldCenter, worldSize);
            _map = new StillWaterMap(texture, _codes.WorldMin, _codes.WorldSize, minLevel, maxLevel);
        }

        /// <summary>True when a readable still map was decoded.</summary>
        public bool IsBound => _codes != null;

        /// <inheritdoc/>
        public StillWaterMap Map => _map;

        /// <inheritdoc/>
        public float StillLevelAt(Vector2 worldPos)
        {
            if (_codes == null) return StillWaterLevels.None;

            // Off the map's rectangle there is no still water. Written so a NaN position fails it too.
            Vector2 min = _codes.WorldMin, size = _codes.WorldSize;
            float u = (worldPos.x - min.x) / size.x;
            float v = (worldPos.y - min.y) / size.y;
            if (!(u >= 0f && u <= 1f && v >= 0f && v <= 1f)) return StillWaterLevels.None;

            return StillWaterLevels.DecodeCode01(_codes.ElevationAt(worldPos), _minLevel, _maxLevel);
        }
    }
}
