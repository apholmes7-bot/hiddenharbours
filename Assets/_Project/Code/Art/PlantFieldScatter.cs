using System;

namespace HiddenHarbours.Art
{
    [Serializable]
    public sealed class PlantChoice
    {
        public int Species;
        public float Weight = 1, MinElevation = -100, MaxElevation = 100;
    }

    [Serializable]
    public sealed class PlantMaterialRecipe
    {
        public int Material;
        public float Density;
        public PlantChoice[] Choices = Array.Empty<PlantChoice>();
    }

    public static class PlantFieldScatter
    {
        public const int MaxSpecies = 63;
        public static double Hash(int x, int y, int seed, int salt)
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)x) * 16777619; h = (h ^ (uint)y) * 16777619;
                h = (h ^ (uint)seed) * 16777619; h = (h ^ (uint)salt) * 16777619;
                h ^= h >> 13; h *= 0x85ebca6b; h ^= h >> 16;
                return h / 4294967296.0;
            }
        }
        public static byte Pack(int species, int stage)
        {
            if (species < 1 || species > MaxSpecies || stage < 0 || stage > 2)
                throw new ArgumentOutOfRangeException();
            return (byte)(species | (stage << 6));
        }
        public static int Species(byte cell) => cell & 63;
        public static int Stage(byte cell) => cell >> 6; // mature, young, seedling
        public static int Variant(int x, int y, int seed) => (int)(Hash(x, y, seed, 5) * 4);
        public static float Offset(int x, int y, int seed, bool north, float cellSize) =>
            (float)((Hash(x, y, seed, north ? 2 : 1) - 0.5) * cellSize * 0.8);

        public static byte Choose(PlantMaterialRecipe row, float elevation, double cellArea,
            int x, int y, int seed, float youngChance)
        {
            if (row == null) return 0;
            if (double.IsNaN(cellArea) || double.IsInfinity(cellArea) || cellArea <= 0 ||
                float.IsNaN(row.Density) || float.IsInfinity(row.Density) || row.Density < 0 ||
                youngChance < 0 || youngChance > 1 || float.IsNaN(youngChance)) throw new ArgumentException("Invalid scatter input");
            if (Hash(x, y, seed, 3) >= Math.Min(1, row.Density * cellArea)) return 0;
            double total = 0;
            foreach (var c in row.Choices)
            {
                if (c.Species < 1 || c.Species > MaxSpecies || c.Weight < 0 || float.IsNaN(c.Weight) || float.IsInfinity(c.Weight))
                    throw new ArgumentException("Invalid plant choice");
                if (elevation >= c.MinElevation && elevation <= c.MaxElevation) total += c.Weight;
            }
            if (total <= 0) return 0;
            double roll = Hash(x, y, seed, 4) * total;
            foreach (var c in row.Choices)
            {
                if (elevation < c.MinElevation || elevation > c.MaxElevation || c.Weight <= 0) continue;
                roll -= c.Weight;
                if (roll < 0) return Pack(c.Species, Hash(x, y, seed, 6) < youngChance ? 1 : 0);
            }
            throw new InvalidOperationException("Plant choice sum changed");
        }
    }
}
