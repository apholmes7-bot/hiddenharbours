using System;
using System.Collections.Generic;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>The village's newly held cells, pinned to the ground before its buildings moved.
    /// These are fixed R16 codes, never a fresh snapshot of the map being judged.</summary>
    public sealed class VillageGroundHold
    {
        readonly Dictionary<int, ushort> _codes = new Dictionary<int, ushort>();
        int _width, _height;
        double _mpp, _x0, _y1;
        float _min, _max;

        public int Count => _codes.Count;
        public IEnumerable<int> Cells => _codes.Keys;

        public static VillageGroundHold Read(string text)
        {
            var root = (Dictionary<string, object>)TerrainPlanSourcesJson.ParseDocument(text);
            if ((string)root["format"] != "hidden-harbours.village-ground-hold/1")
                throw new FormatException("Unknown village ground hold format.");
            var grid = (Dictionary<string, object>)root["grid"];
            var range = (List<object>)root["range"];
            var hold = new VillageGroundHold
            {
                _width = Integer(grid["w"]), _height = Integer(grid["h"]),
                _mpp = (double)grid["mpp"], _x0 = (double)grid["x0"], _y1 = (double)grid["y1"],
                _min = (float)(double)range[0], _max = (float)(double)range[1],
            };
            foreach (List<object> row in (List<object>)root["cells"])
            {
                int index = Integer(row[0]), code = Integer(row[1]);
                if (row.Count != 2 || index < 0 || index >= hold._width * hold._height || code < 0 || code > 65535)
                    throw new FormatException("Invalid village ground hold cell.");
                hold._codes.Add(index, (ushort)code);
            }
            if (hold.Count == 0) throw new FormatException("The village ground hold is empty.");
            return hold;
        }

        static int Integer(object value)
        {
            double d = (double)value;
            if (d != Math.Truncate(d) || d < 0 || d > int.MaxValue) throw new FormatException("Expected a nonnegative integer.");
            return (int)d;
        }

        public void Validate(TerrainPlanResult result, RegionTerrainPlanDef plan)
        {
            var grid = result.Grid;
            if (grid.W != _width || grid.H != _height || grid.Mpp != _mpp || grid.XG(0) != _x0 + _mpp / 2 ||
                grid.YG(0) != _y1 - _mpp / 2 || plan.HeightRange.x != _min || plan.HeightRange.y != _max)
                throw new InvalidOperationException("The village ground hold belongs to a different grid or height range.");
            if (result.GroundCodes == null || result.GroundCodes.Length != grid.Count)
                throw new InvalidOperationException("The village ground hold needs the imported ground codes.");
        }

        public bool Contains(int index) => _codes.ContainsKey(index);
        public double Elevation(int index) => TerrainPlanMaps.Decode(_codes[index], _min, _max);

        /// <summary>Exact code and decoded elevation: even a one-code edit to a newly held cell fails.</summary>
        public bool Matches(TerrainPlanResult result, int index) =>
            result.GroundCodes[TerrainPlanMaps.Pixel(result.Grid, index)] == _codes[index] &&
            result.E[index] == Elevation(index);

        public int ChangedCells(TerrainPlanResult result)
        {
            int count = 0;
            foreach (int index in Cells) if (!Matches(result, index)) count++;
            return count;
        }
    }
}
