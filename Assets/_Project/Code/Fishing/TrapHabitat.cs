using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Shared habitat/density evaluator for spawn weighting, pots and rotation.
    /// Does not spawn individuals, deplete a population or reinterpret shellfish as swimming schools.</summary>
    public static class TrapHabitat
    {
        public static Vector2 CentreAt(in TrapHabitatSettings settings, int worldSeed, double gameSeconds)
        {
            if (!settings.IsValid || !BottomMapSettings.Finite(gameSeconds) || gameSeconds < 0d)
                return new Vector2(float.NaN, float.NaN);
            uint h = StableHash.Finalize(StableHash.Fold((uint)worldSeed, settings.SpeciesId));
            double phase = gameSeconds % settings.PeriodGameSeconds / settings.PeriodGameSeconds
                + settings.Phase01 + h / 4294967296d;
            double displacement = Math.Sin(phase * (2d * Math.PI));
            return new Vector2((float)(settings.Centre.x + settings.MigrationVector.x * displacement),
                (float)(settings.Centre.y + settings.MigrationVector.y * displacement));
        }

        public static float DensityAt(IBottomMap bottom, Vector2 position, in TrapHabitatSettings settings,
            int worldSeed, double gameSeconds)
        {
            if (bottom == null || !settings.IsValid || !TrapHabitatSettings.Finite(position)) return 0f;
            Vector2 centre = CentreAt(in settings, worldSeed, gameSeconds);
            if (!TrapHabitatSettings.Finite(centre)) return 0f;
            float affinity;
            switch (bottom.BottomAt(position))
            {
                case BottomType.Rock: affinity = settings.RockAffinity01; break;
                case BottomType.Mud: affinity = settings.MudAffinity01; break;
                case BottomType.Sand: affinity = settings.SandAffinity01; break;
                default: return 0f;
            }
            double dx = (double)position.x - centre.x, dy = (double)position.y - centre.y;
            double falloff = Math.Max(0d, 1d - Math.Sqrt(dx * dx + dy * dy) / settings.RadiusMetres);
            return (float)(affinity * (settings.OffCentreDensity01 + (1d - settings.OffCentreDensity01) * falloff));
        }
    }
}
