using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy.PlantIntake
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish plant/Graded line")]
    public sealed class GradedFishLineDef : ScriptableObject
    {
        public string Id;
        public string SpeciesId;
        public FishProduct Product;
        public int MaximumInputGrams;
        public int YieldPpm;
        public int ProcessGameSeconds;
        public int SlowTickSeconds;

        public GradedFishLineSpec BuildSpec() => new GradedFishLineSpec(Id, SpeciesId, Product,
            MaximumInputGrams, YieldPpm, ProcessGameSeconds, SlowTickSeconds);
    }
}
