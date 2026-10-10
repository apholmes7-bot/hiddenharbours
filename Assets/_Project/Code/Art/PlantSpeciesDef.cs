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

        public PlantCell Find(int stage, int season, int variant, int tide)
        {
            foreach (var c in Cells)
                if (c.Stage == stage && c.Season == season && c.Variant == variant && c.Tide == tide) return c;
            // Missing states are explicit bake gaps, never another species or season.
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
        public Rect Rect;
        public Vector2 Pivot;
        public float HeightM;
    }
}
