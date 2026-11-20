using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    public sealed class StPetersPlantGround : MonoBehaviour, IPlantFieldGround, IGroundZoneSource
    {
        public PlantFieldGroundData Data;
        byte[] _cells;

        // The same painted ground the plants grow from tells the print layer what ground is underfoot.
        void OnEnable() => GroundZoneSources.Register(this);
        void OnDisable() => GroundZoneSources.Unregister(this);

        /// <summary>The terrain zone at a point: the cell's material slot read as a
        /// <see cref="TerrainPlanZones"/> name. False outside the grid and on excluded cells.</summary>
        public bool TryGetZone(Vector2 worldPos, out string zone)
        {
            zone = null;
            if (Data == null || Data.CellSize <= 0f || string.IsNullOrEmpty(Data.Payload)) return false;
            _cells ??= Data.Decode();
            int c = Mathf.FloorToInt((worldPos.x - Data.Origin.x) / Data.CellSize);
            int r = Mathf.FloorToInt((worldPos.y - Data.Origin.y) / Data.CellSize);
            if (c < 0 || c >= Data.Width || r < 0 || r >= Data.Height) return false;
            byte value = _cells[r * Data.Width + c];
            if (value == TerrainPlanZones.Unpainted) return false;
            int material = value & 31;
            if (material >= TerrainPlanZones.Names.Length) return false;
            zone = TerrainPlanZones.Names[material];
            return true;
        }

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
