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

        // The weed under the water (PlantField.JoinsUnderWater). Declared last for the same reason as Clump;
        // an asset written before these existed reads the defaults below.
        public const float DefaultUnderWaterSwayM = 0.12f;
        public const float DefaultUnderWaterSwayPeriodS = 7f;
        public const float DefaultUnderWaterSwayWavelengthM = 9f;
        /// <summary>The smallest period and wavelength the sway takes; the shader guards with the same value.</summary>
        public const float MinUnderWaterSwaySpan = 0.1f;
        [Tooltip("Show the shore weed under the tide through the water, as the floor is shown? ON: a tidal " +
                 "species' chunk under the sea plane is drawn by the sea over its floor, darker and bluer with " +
                 "depth, so a kelp bed reads from a boat. OFF: the sea's bed covers it, as before. Needs " +
                 "GameConfig.ShallowsSubmergedProps on too (desktop only).")]
        public bool UnderWater = true;
        [Tooltip("How far (m) the top of a frond rocks with the swell's surge under the water. 0 holds it still.")]
        [Min(0)] public float UnderWaterSwayM = DefaultUnderWaterSwayM;
        [Tooltip("Seconds for one rock of the weed under the water, there and back: the swell's own period.")]
        [Min(MinUnderWaterSwaySpan)] public float UnderWaterSwayPeriodS = DefaultUnderWaterSwayPeriodS;
        [Tooltip("Metres between weed rocking the same way, along the wind: the surge travels across the bed.")]
        [Min(MinUnderWaterSwaySpan)] public float UnderWaterSwayWavelengthM = DefaultUnderWaterSwayWavelengthM;
    }

    [Serializable]
    public sealed class PlantFieldSite
    {
        public string Id;
        public Vector2 At;
        public int Species, Stage, Variant;
    }
}
