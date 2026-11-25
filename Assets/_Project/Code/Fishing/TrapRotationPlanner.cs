using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    public readonly struct TrapObservation
    {
        public readonly string InstanceId;
        public readonly Vector2 Position;
        public readonly float RecentCatchPerGameHour;
        public readonly bool HasRecentCatch;
        public TrapObservation(string instanceId, Vector2 position, float recentCatchPerGameHour, bool hasRecentCatch)
        { InstanceId = instanceId; Position = position; RecentCatchPerGameHour = recentCatchPerGameHour; HasRecentCatch = hasRecentCatch; }
    }

    /// <summary>A request to haul, work, carry, bait and reset. Applying it is the boat's job.</summary>
    public readonly struct TrapRotationOrder
    {
        public readonly string InstanceId;
        public readonly Vector2 From, ResetAt;
        public readonly float ExpectedDensityGain01;
        public TrapRotationOrder(string instanceId, Vector2 from, Vector2 resetAt, float gain)
        { InstanceId = instanceId; From = from; ResetAt = resetAt; ExpectedDensityGain01 = gain; }
    }

    /// <summary>Bounded deterministic slow-tick planner. Stable input/candidate order breaks ties.
    /// Caller owns all buffers and supplies surveyed candidates. No movement, catch, route or save writes.</summary>
    public static class TrapRotationPlanner
    {
        public static int Plan(TrapObservation[] traps, Vector2[] candidates, Rect region, ITidalTerrain terrain,
            float waterLevel, IBottomMap bottom, in TrapHabitatSettings habitat, int worldSeed, double gameSeconds,
            in TrapRotationSettings settings, TrapRotationOrder[] orders)
        {
            if (traps == null || candidates == null || orders == null || terrain == null || bottom == null
                || !settings.IsValid || !habitat.IsValid || !BottomMapSettings.Finite(waterLevel)
                || !BottomMapSettings.Finite(gameSeconds) || gameSeconds < 0d
                || !ValidRegion(region)) return 0;
            int count = 0;
            for (int i = 0; i < traps.Length && count < orders.Length; i++)
            {
                TrapObservation trap = traps[i];
                if (string.IsNullOrEmpty(trap.InstanceId) || !TrapHabitatSettings.Finite(trap.Position)
                    || !trap.HasRecentCatch || !BottomMapSettings.Finite(trap.RecentCatchPerGameHour)
                    || trap.RecentCatchPerGameHour < 0f || trap.RecentCatchPerGameHour >= settings.PoorCatchPerGameHour)
                    continue;
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                    if (string.Equals(traps[j].InstanceId, trap.InstanceId, StringComparison.Ordinal)) { duplicate = true; break; }
                if (duplicate) continue;
                float current = TrapHabitat.DensityAt(bottom, trap.Position, in habitat, worldSeed, gameSeconds);
                float best = current;
                int winner = -1;
                for (int c = 0; c < candidates.Length; c++)
                {
                    Vector2 point = candidates[c];
                    if (!Inside(region, point) || Squared(point, trap.Position) > (double)settings.MaxMoveMetres * settings.MaxMoveMetres)
                        continue;
                    float elevation = terrain.ElevationAt(point);
                    double depth = (double)waterLevel - elevation;
                    if (!BottomMapSettings.Finite(elevation) || depth < settings.MinDepthMetres || depth > settings.MaxDepthMetres
                        || Occupied(point, traps, i, orders, count, settings.MinSpacingMetres)) continue;
                    float score = TrapHabitat.DensityAt(bottom, point, in habitat, worldSeed, gameSeconds);
                    if (score > best && score - current >= settings.MinimumDensityGain01)
                    { best = score; winner = c; }
                }
                if (winner >= 0) orders[count++] = new TrapRotationOrder(trap.InstanceId, trap.Position, candidates[winner], best - current);
            }
            return count;
        }

        private static bool ValidRegion(Rect r) => BottomMapSettings.Finite(r.xMin) && BottomMapSettings.Finite(r.xMax)
            && BottomMapSettings.Finite(r.yMin) && BottomMapSettings.Finite(r.yMax) && r.width > 0f && r.height > 0f;
        private static bool Inside(Rect r, Vector2 p) => TrapHabitatSettings.Finite(p)
            && p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax;
        private static double Squared(Vector2 a, Vector2 b)
        { double x = (double)a.x - b.x, y = (double)a.y - b.y; return x * x + y * y; }
        private static bool Occupied(Vector2 p, TrapObservation[] traps, int own, TrapRotationOrder[] orders, int count, float spacing)
        {
            double minimum = (double)spacing * spacing;
            for (int i = 0; i < traps.Length; i++)
                if (i != own && TrapHabitatSettings.Finite(traps[i].Position) && Squared(p, traps[i].Position) < minimum) return true;
            for (int i = 0; i < count; i++)
                if (Squared(p, orders[i].ResetAt) < minimum) return true;
            return false;
        }
    }
}
