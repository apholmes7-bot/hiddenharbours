using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Plant Field")]
    public sealed class PlantFieldDefinition : ScriptableObject
    {
        public string Id;
        public Vector2 Origin;
        public int Width, Height, Seed;
        public float CellSize, GroundMetresPerY, ChunkWidth, SlowTickSeconds;
        public float HeightProjection, WindMargin;
        [Min(0)] public float SunShadowStrength = 1;
        public int SubmergedOrder;
        public BiomeRecipeDef[] Recipes = Array.Empty<BiomeRecipeDef>();
        public PlantSpeciesDef[] Species = Array.Empty<PlantSpeciesDef>();
        public Material Material;
        public PlantFieldSite[] Authored = Array.Empty<PlantFieldSite>();
        // The clump law the recipe scatter grows under. None = the scatter as it shipped. Declared after
        // Authored so binding it adds one line at the foot of the asset and moves no authored byte.
        public PlantClumpDef Clump;
    }

    [Serializable]
    public sealed class PlantFieldSite
    {
        public string Id;
        public Vector2 At;
        public int Species, Stage, Variant;
    }
}
