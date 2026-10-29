using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Plant Field Ground")]
    public sealed class PlantFieldGroundData : ScriptableObject
    {
        public string Id;
        public Vector2 Origin;
        public int Width, Height;
        public float CellSize;
        public string Payload;
        public PaintedHeightMap HeightMap;

        // Each run is uint16 length + byte (recipe * 32 + material); 255 excludes a cell.
        public byte[] Decode()
        {
            if (Width < 1 || Height < 1 || (long)Width * Height > 4000000 || CellSize <= 0)
                throw new InvalidOperationException("Invalid plant ground grid");
            var encoded = Convert.FromBase64String(Payload);
            if (encoded.Length % 3 != 0) throw new FormatException("Invalid plant ground runs");
            var cells = new byte[Width * Height]; int at = 0;
            for (int i = 0; i < encoded.Length; i += 3)
            {
                int n = encoded[i] | encoded[i + 1] << 8;
                if (n == 0 || n > cells.Length - at) throw new FormatException("Invalid plant ground run length");
                for (int j = 0; j < n; j++) cells[at++] = encoded[i + 2];
            }
            if (at != cells.Length) throw new FormatException("Incomplete plant ground grid");
            return cells;
        }
    }
}
