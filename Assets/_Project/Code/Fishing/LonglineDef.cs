using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>One kind of fishing tub. All balance values belong to its asset.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Longline")]
    public sealed class LonglineDef : ScriptableObject
    {
        public string Id;
        [Min(1)] public int HooksPerTub;
        [Min(0f)] public float TubLengthMeters;
        [Min(1)] public int MaxTubsPerSet;
        [Min(0f)] public float MinSetDepthMeters;
        [Min(0f)] public float MaxSetDepthMeters;
        [Min(0f)] public float SoakHoursToPeak;
        [Min(0f)] public float StripHours;
        [Min(1)] public int HooksPerBaitUnit;
        [Min(1)] public int BaitFavourMultiplier;
        [Range(0f, 1f)] public float PartStrain01;
        public string DeckWorkId;

        public bool HasValidTuning => !string.IsNullOrEmpty(Id)
            && HooksPerTub > 0 && MaxTubsPerSet > 0
            && HooksPerTub <= int.MaxValue / MaxTubsPerSet
            && Positive(TubLengthMeters) && Positive(SoakHoursToPeak) && Positive(StripHours)
            && Finite(MinSetDepthMeters) && MinSetDepthMeters >= 0f
            && Finite(MaxSetDepthMeters) && MaxSetDepthMeters > MinSetDepthMeters
            && HooksPerBaitUnit > 0 && BaitFavourMultiplier >= 1
            && Positive(PartStrain01) && PartStrain01 <= 1f
            && !string.IsNullOrEmpty(DeckWorkId);

        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
