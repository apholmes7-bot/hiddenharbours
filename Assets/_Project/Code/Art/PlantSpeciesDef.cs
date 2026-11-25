using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Plant Species")]
    public sealed class PlantSpeciesDef : ScriptableObject
    {
        public string Id, SpeciesKey, Family, Habitat;
        public int Code;
        public float StandingHeightM, MinElevation, MaxElevation;
        public float[] TideLadder = Array.Empty<float>();
        public PlantSheetPage[] Pages = Array.Empty<PlantSheetPage>();
        public PlantCell[] Cells = Array.Empty<PlantCell>();
        // Declared after Cells so binding them adds lines at the foot of the asset and moves no baked byte.
        [Tooltip("The hue this species takes under the water (PlantField's under-water pass), at each pixel's " +
                 "own brightness. The sea then darkens and blues it with depth, so this is its colour in air.")]
        public Color UnderWaterTint = new Color(0.40f, 0.38f, 0.16f, 1f);
        [Tooltip("How far the under-water drawing moves toward that hue: 0 draws the baked sheet as it is; 1 " +
                 "takes the hue wholly. For a sheet whose wet states are baked in a cold water ramp, which the " +
                 "sea's own column would otherwise blue twice.")]
        [Range(0f, 1f)] public float UnderWaterTintAmount;
        [Tooltip("How bright this species is drawn under the water, after the hue: 1 as bright as the sheet; " +
                 "less darkens every frond toward a silhouette, so a kelp bed reads as a dark mass over the " +
                 "lighter floor, as real kelp does under clear water. The sea's depth darkening comes on top.")]
        [Range(0f, 1f)] public float UnderWaterShade = 1f;

        public PlantCell Find(int stage, int season, int variant, int tide)
        {
            foreach (var c in Cells)
                if (c.Stage == stage && c.Season == season && c.Variant == variant && c.Tide == tide) return c;
            // An absent lookup is an unbound state, distinct from explicit dormancy.
            return null;
        }
    }
    [Serializable]
    public sealed class PlantSheetPage
    {
        public Texture2D Albedo, Normal, Light, Detail;
    }
    [Serializable]
    public sealed class PlantCell
    {
        public int Stage, Season, Variant, Tide, Page;
        public bool Dormant;
        public Rect Rect;
        public Vector2 Pivot;
        public float HeightM;
    }
}
