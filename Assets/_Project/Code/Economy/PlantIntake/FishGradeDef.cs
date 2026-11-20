using UnityEngine;

namespace HiddenHarbours.Economy.PlantIntake
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish plant/Grade")]
    public sealed class FishGradeDef : ScriptableObject
    {
        public string Id;
        public int MinimumFreshnessPpm;
        public int PricePerKg;

        public FishGradeSpec BuildSpec() => new FishGradeSpec(Id, MinimumFreshnessPpm, PricePerKg);
    }
}
