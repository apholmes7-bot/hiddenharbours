using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The pure proximity gate for stall interactions — selling at the Fish Buyer (B) and buying at the
    /// Shipwright (P): the action may fire only when the player is ON FOOT and within reach of the
    /// stall, never from anywhere and never while aboard. No Unity scene/state, so it is
    /// EditMode-testable; mirrors <c>ControlSwitcher</c>'s dock-zone test
    /// (<c>Vector2.Distance(player, point) &lt;= radius</c>).
    /// </summary>
    public static class StallGate
    {
        /// <summary>Default reach (metres) — a forgiving step-up distance, like the dock zone.</summary>
        public const float DefaultRange = 4f;

        /// <summary>True iff <paramref name="player"/> is within <paramref name="range"/> of the stall
        /// (inclusive, XY distance).</summary>
        public static bool InRange(Vector2 player, Vector2 stall, float range)
            => Vector2.Distance(player, stall) <= range;

        /// <summary>True iff the interaction may fire: the player is ON FOOT and in range of the stall.</summary>
        public static bool CanInteract(bool onFoot, Vector2 player, Vector2 stall, float range)
            => onFoot && InRange(player, stall, range);
    }
}
