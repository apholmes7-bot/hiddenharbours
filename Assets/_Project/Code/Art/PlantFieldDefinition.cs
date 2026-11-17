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
        // Where plants sort against a hull (PlantHullSorting). Declared last for the same reason as Clump;
        // an asset written before these existed reads the defaults below.
        [Tooltip("A species no taller than this (m) is ground cover: it lies under every hull.")]
        [Min(0)] public float GroundCoverMaxHeightM = 0.5f;
        [Tooltip("Habitats whose growth lies flat when the tide leaves it (intertidal weed): under every hull.")]
        public string[] LyingHabitats = { "fringe", "mid" };
        [Tooltip("How far (m) above her drawn outline what stands on a hull's deck reaches up the screen: a " +
                 "standing plant behind her waterline and inside this draws under her.")]
        [Min(0)] public float HullPictureRiseM = 6f;
    }

    [Serializable]
    public sealed class PlantFieldSite
    {
        public string Id;
        public Vector2 At;
        public int Species, Stage, Variant;
    }
}
