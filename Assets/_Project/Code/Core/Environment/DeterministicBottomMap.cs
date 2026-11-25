using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Proposed simulation geology, never a classifier of existing painted terrain.
    /// Copies tuning at setup; samples allocate nothing. Integer hash constants are algorithm constants.</summary>
    public sealed class DeterministicBottomMap : IBottomMap
    {
        private readonly int _seed;
        private readonly BottomMapSettings _settings;

        public DeterministicBottomMap(int worldSeed, in BottomMapSettings settings)
        {
            if (!settings.IsValid) throw new ArgumentException("Invalid bottom-map settings.", nameof(settings));
            _seed = worldSeed;
            _settings = settings;
        }

        public BottomType BottomAt(Vector2 worldPosition)
        {
            if (!BottomMapSettings.Finite(worldPosition.x) || !BottomMapSettings.Finite(worldPosition.y))
                return BottomType.Unknown;
            double x = Math.Floor(((double)worldPosition.x - _settings.Origin.x) / _settings.CellMetres);
            double y = Math.Floor(((double)worldPosition.y - _settings.Origin.y) / _settings.CellMetres);
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
                return BottomType.Unknown;
            uint h;
            unchecked
            {
                h = (uint)_seed ^ (uint)_settings.Salt;
                h = (h ^ (uint)(int)x) * 16777619u;
                h = (h ^ (uint)(int)y) * 16777619u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
            }
            double total = (double)_settings.RockWeight + _settings.MudWeight + _settings.SandWeight;
            double pick = h / 4294967296d * total;
            if (pick < _settings.RockWeight) return BottomType.Rock;
            if (pick < (double)_settings.RockWeight + _settings.MudWeight) return BottomType.Mud;
            return BottomType.Sand;
        }
    }
}
