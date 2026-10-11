using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Biome Recipe")]
    public sealed class BiomeRecipeDef : ScriptableObject
    {
        public string Id;
        [Range(0, 1)] public float YoungChance = 0.3f;
        public PlantMaterialRecipe[] Materials = Array.Empty<PlantMaterialRecipe>();
        public PlantMaterialRecipe ForMaterial(int material)
        {
            foreach (var row in Materials) if (row.Material == material) return row;
            return null;
        }
    }
}
