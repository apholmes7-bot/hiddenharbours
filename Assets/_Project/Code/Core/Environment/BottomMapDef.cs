using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    [Serializable]
    public struct BottomMapSettings
    {
        public Vector2 Origin;
        public float CellMetres;
        public int Salt;
        public float RockWeight, MudWeight, SandWeight;

        public bool IsValid => Finite(Origin.x) && Finite(Origin.y) && Finite(CellMetres) && CellMetres > 0f
            && Finite(RockWeight) && RockWeight >= 0f && Finite(MudWeight) && MudWeight >= 0f
            && Finite(SandWeight) && SandWeight >= 0f
            && (double)RockWeight + MudWeight + SandWeight > 0d;

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Serializable proposal data, following the plain trawl Def pattern. No Unity asset,
    /// identity-ledger registration or scene is installed by this inactive logic slice.</summary>
    [Serializable]
    public sealed class BottomMapDef
    {
        public string Id = "bottom_map.";
        public BottomMapSettings Settings;

        public DeterministicBottomMap CreateMap(int worldSeed)
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("bottom_map.", StringComparison.Ordinal)
                || Id.Length == "bottom_map.".Length) throw new ArgumentException("A stable bottom-map id is required.");
            return new DeterministicBottomMap(worldSeed, in Settings);
        }
    }
}
