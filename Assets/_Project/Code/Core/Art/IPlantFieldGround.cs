namespace HiddenHarbours.Core
{
    public readonly struct PlantGroundSample
    {
        public readonly int Recipe, Material;
        public readonly float Elevation;
        public readonly bool Excluded;
        public PlantGroundSample(int recipe, int material, float elevation, bool excluded = false)
        { Recipe = recipe; Material = material; Elevation = elevation; Excluded = excluded; }
    }

    // Region adapters supply live terrain through Core. No cached plant sites enter a save.
    public interface IPlantFieldGround
    {
        PlantGroundSample Sample(float x, float y);
        float WaterLevelAt(float x, float y);
        int PlantSeason { get; }
    }
}
