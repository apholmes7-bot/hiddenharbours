using System;
using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Explicit, inert editor intake of the third-issue ground. No scene or service registration.
    /// Like StPetersLayerRefresh.PlanGround, this adapts the runtime field without applying any analytic fill.
    /// Activation by the builder and layer refresh belongs to the later, coherent scene patch.</summary>
    public static class NineMileCreekGround
    {
        public const string Folder = "Assets/_Project/Data/Terrain/NineMileCreekGround";
        public const string HeightPath = Folder + "/NineMileCreekGround_Height.png";
        public const string StillPath = Folder + "/NineMileCreekGround_Still.png";
        public const string MapPath = Folder + "/NineMileCreekGround.asset";
        public const int Width = 1520, Height = 1120;
        public const float Minimum = -6f, Maximum = 6f, CodeMetres = 12f / 65535f;

        public static PlanGround Load(string projectRoot = ".")
        {
            var image = TerrainPlanPng.ReadFile(Path.Combine(projectRoot, HeightPath));
            if (image.Width != Width || image.Height != Height || image.BitDepth != 16 || image.ColourType != 0)
                throw new InvalidDataException("NMC ground must be 1520 by 1120, R16 greyscale.");
            var elevations = new float[Width * Height];
            for (int row = 0; row < Height; row++)
                for (int col = 0; col < Width; col++)
                    elevations[(Height - 1 - row) * Width + col] =
                        PaintedHeightField.DecodeElevation(image.Samples[row * Width + col] / 65535f, Minimum, Maximum);
            return new PlanGround(new PaintedHeightField(elevations, Width, Height,
                Vector2.zero, new Vector2(760f, 560f)));
        }

        public sealed class PlanGround : ITidalTerrain
        {
            public PaintedHeightField Field { get; }
            public PlanGround(PaintedHeightField field) => Field = field ?? throw new ArgumentNullException(nameof(field));
            public float ElevationAt(Vector2 worldPos) => Field.ElevationAt(worldPos);
        }
    }
}
