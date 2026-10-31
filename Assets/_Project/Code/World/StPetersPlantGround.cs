using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    public sealed class StPetersPlantGround : MonoBehaviour, IPlantFieldGround
    {
        public PlantFieldGroundData Data;
        byte[] _cells;

        public int PlantSeason => GameServices.Clock != null ? (int)GameServices.Clock.Season : (int)Season.EarlySpring;

        public PlantGroundSample Sample(float x, float y)
        {
            if (Data == null || Data.HeightMap == null || Data.HeightMap.Field == null)
                throw new InvalidOperationException("Plant field needs its painted ground");
            _cells ??= Data.Decode();
            int c = Mathf.FloorToInt((x - Data.Origin.x) / Data.CellSize);
            int r = Mathf.FloorToInt((y - Data.Origin.y) / Data.CellSize);
            float height = Data.HeightMap.Field.ElevationAt(new Vector2(x, y));
            if (c < 0 || c >= Data.Width || r < 0 || r >= Data.Height)
                return new PlantGroundSample(-1, 0, height, true);
            byte value = _cells[r * Data.Width + c];
            return new PlantGroundSample(value >> 5, value & 31, height, value == 255);
        }

        public float WaterLevelAt(float x, float y)
        {
            double time = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0;
            float tide = GameServices.Environment != null ? GameServices.Environment.WaterLevelAt(time) : StillWaterLevels.None;
            var still = Data != null && Data.HeightMap != null ? Data.HeightMap.StillWater : null;
            return Mathf.Max(tide, still != null ? still.StillLevelAt(new Vector2(x, y)) : StillWaterLevels.None);
        }
    }
}
