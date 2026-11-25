using System;

namespace HiddenHarbours.Core
{
    /// <summary>The four authored rod contributions. A caller may add a player's skill outside this value.</summary>
    [Serializable]
    public struct RodFishingProfile
    {
        public float CastMetres;
        public float LineMetres;
        public float ReelUpMetresPerSecond;
        /// <summary>A dimensionless contribution to fight help, not a multiplier or a player skill.</summary>
        public float FightHelp;

        public bool IsValid => Positive(CastMetres) && Positive(LineMetres)
            && Positive(ReelUpMetresPerSecond) && Positive(FightHelp);

        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    }

    /// <summary>Reads one equipment instance's authored numbers without selecting or modifying a player.</summary>
    public interface IRodFishingEquipment
    {
        /// <summary>False with a default value for absent, non-rod or invalid equipment.</summary>
        bool TryGetRodFishing(out RodFishingProfile profile);
    }
}
