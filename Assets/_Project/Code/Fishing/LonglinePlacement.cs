using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Checks both ends and every hook against the sea at the set's instant.</summary>
    public static class LonglinePlacement
    {
        public static bool CanPlaceAt(LonglineDef def, int tubs, Vector2 endA, Vector2 endB,
                                      IEnvironmentService environment, ITidalTerrain terrain,
                                      double setTime, out LonglineRefusal refusal)
        {
            if (!LonglineGeometry.TryValidate(def, tubs, endA, endB, out int count, out refusal))
                return false;
            refusal = LonglineRefusal.MissingWater;
            if (environment == null || terrain == null || double.IsNaN(setTime) || double.IsInfinity(setTime))
                return false;
            refusal = DepthRefusal(def, TrapPlacement.DepthAt(environment, terrain, setTime, endA), true);
            if (refusal != LonglineRefusal.None) return false;
            refusal = DepthRefusal(def, TrapPlacement.DepthAt(environment, terrain, setTime, endB), true);
            if (refusal != LonglineRefusal.None) return false;
            for (int i = 0; i < count; i++)
            {
                Vector2 hook = LonglineGeometry.HookPosition(endA, endB, count, i);
                refusal = DepthRefusal(def, TrapPlacement.DepthAt(environment, terrain, setTime, hook), false);
                if (refusal != LonglineRefusal.None) return false;
            }
            return true;
        }

        private static LonglineRefusal DepthRefusal(LonglineDef def, float depth, bool end)
        {
            if (float.IsNaN(depth) || float.IsInfinity(depth)) return LonglineRefusal.MissingWater;
            if (depth <= 0f) return end ? LonglineRefusal.EndOnLand : LonglineRefusal.HookOnLand;
            if (depth < def.MinSetDepthMeters)
                return end ? LonglineRefusal.EndTooShallow : LonglineRefusal.HookTooShallow;
            if (depth > def.MaxSetDepthMeters)
                return end ? LonglineRefusal.EndTooDeep : LonglineRefusal.HookTooDeep;
            return LonglineRefusal.None;
        }
    }
}
