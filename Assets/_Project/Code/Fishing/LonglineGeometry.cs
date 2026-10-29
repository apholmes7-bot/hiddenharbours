using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    public enum LonglineRefusal
    {
        None, InvalidDefinition, InvalidTubCount, InvalidEnds, LineTooLong, MissingWater,
        EndOnLand, EndTooShallow, EndTooDeep, HookOnLand, HookTooShallow, HookTooDeep
    }

    /// <summary>Straight groundline, with equal gaps between anchors and the hooks between them.</summary>
    public static class LonglineGeometry
    {
        public static bool TryValidate(LonglineDef def, int tubs, Vector2 endA, Vector2 endB,
                                       out int hookCount, out LonglineRefusal refusal)
        {
            hookCount = 0;
            refusal = LonglineRefusal.InvalidDefinition;
            if (def == null || !def.HasValidTuning) return false;
            refusal = LonglineRefusal.InvalidTubCount;
            if (tubs < 1 || tubs > def.MaxTubsPerSet) return false;
            refusal = LonglineRefusal.InvalidEnds;
            if (!Finite(endA.x) || !Finite(endA.y) || !Finite(endB.x) || !Finite(endB.y)) return false;
            double dx = (double)endB.x - endA.x, dy = (double)endB.y - endA.y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 0d) return false;
            double paidOut = (double)tubs * def.TubLengthMeters;
            refusal = LonglineRefusal.LineTooLong;
            if (lengthSquared > paidOut * paidOut) return false;
            hookCount = tubs * def.HooksPerTub;
            refusal = LonglineRefusal.None;
            return true;
        }

        public static Vector2 HookPosition(Vector2 endA, Vector2 endB, int hookCount, int hookIndex)
        {
            if (hookCount < 1 || hookIndex < 0 || hookIndex >= hookCount)
                throw new ArgumentOutOfRangeException(nameof(hookIndex));
            float along = (float)((hookIndex + 1d) / (hookCount + 1d));
            return Vector2.Lerp(endA, endB, along);
        }

        public static bool TryBuild(LonglineDef def, int tubs, Vector2 endA, Vector2 endB,
                                    out Vector2[] hooks, out LonglineRefusal refusal)
        {
            hooks = Array.Empty<Vector2>();
            if (!TryValidate(def, tubs, endA, endB, out int count, out refusal)) return false;
            hooks = new Vector2[count];
            for (int i = 0; i < count; i++) hooks[i] = HookPosition(endA, endB, count, i);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
