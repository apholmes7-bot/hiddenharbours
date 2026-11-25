using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    [Serializable]
    public struct TrapHabitatSettings
    {
        public string SpeciesId;
        public float RockAffinity01, MudAffinity01, SandAffinity01;
        public Vector2 Centre, MigrationVector;
        public float RadiusMetres, OffCentreDensity01;
        public double PeriodGameSeconds, Phase01;

        public bool IsValid => !string.IsNullOrEmpty(SpeciesId) && Unit(RockAffinity01)
            && Unit(MudAffinity01) && Unit(SandAffinity01)
            && (RockAffinity01 > 0f || MudAffinity01 > 0f || SandAffinity01 > 0f)
            && Finite(Centre) && Finite(MigrationVector) && BottomMapSettings.Finite(RadiusMetres)
            && RadiusMetres > 0f && Unit(OffCentreDensity01)
            && BottomMapSettings.Finite(PeriodGameSeconds) && PeriodGameSeconds > 0d
            && BottomMapSettings.Finite(Phase01) && Phase01 >= 0d && Phase01 < 1d;

        public static bool Finite(Vector2 p) => BottomMapSettings.Finite(p.x) && BottomMapSettings.Finite(p.y);
        private static bool Unit(float v) => BottomMapSettings.Finite(v) && v >= 0f && v <= 1f;
    }

    /// <summary>Serializable proposal data, one species per record; no implicit lookup or activation.</summary>
    [Serializable]
    public sealed class TrapHabitatDef
    {
        public string Id = "trap_habitat.";
        public TrapHabitatSettings Settings;
    }
}
