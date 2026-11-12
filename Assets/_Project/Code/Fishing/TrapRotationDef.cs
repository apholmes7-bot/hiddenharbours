using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    [Serializable]
    public struct TrapRotationSettings
    {
        public float PoorCatchPerGameHour, MinimumDensityGain01, MinSpacingMetres, MaxMoveMetres;
        public float MinDepthMetres, MaxDepthMetres;
        public bool IsValid => BottomMapSettings.Finite(PoorCatchPerGameHour) && PoorCatchPerGameHour > 0f
            && BottomMapSettings.Finite(MinimumDensityGain01) && MinimumDensityGain01 >= 0f && MinimumDensityGain01 <= 1f
            && BottomMapSettings.Finite(MinSpacingMetres) && MinSpacingMetres > 0f
            && BottomMapSettings.Finite(MaxMoveMetres) && MaxMoveMetres >= MinSpacingMetres
            && BottomMapSettings.Finite(MinDepthMetres) && MinDepthMetres > 0f
            && BottomMapSettings.Finite(MaxDepthMetres) && MaxDepthMetres >= MinDepthMetres;
    }

    /// <summary>Serializable proposal data; no live asset is installed.</summary>
    [Serializable]
    public sealed class TrapRotationDef
    {
        public string Id = "trap_rotation.";
        public TrapRotationSettings Settings;
    }
}
