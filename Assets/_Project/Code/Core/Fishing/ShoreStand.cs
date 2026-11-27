using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The edge a shore angler fishes over</b>: a line on the ground (a point on it and the outward
    /// normal, pointing at open water) and the margin a fish keeps from it. Everything a villager angler
    /// throws or draws in (the cast aim, the fish in the fight, the splashes) is clamped to the water side,
    /// so the fight never crosses the planks he is standing on. Pure value, no allocation.
    ///
    /// <para>An unset edge (<see cref="IsSet"/> false, the default) clamps nothing, so a spot without one
    /// behaves exactly as before.</para>
    ///
    /// FLAG lead-architect: new Core value (the shore stand seam).
    /// </summary>
    public readonly struct ShoreEdge
    {
        /// <summary>A point on the edge line (world metres, or relative to the angler after <see cref="RelativeTo"/>).</summary>
        public readonly Vector2 Point;
        /// <summary>Unit normal of the line, pointing out over the water.</summary>
        public readonly Vector2 Outward;
        /// <summary>How far out from the line a clamped position is kept (m, never negative).</summary>
        public readonly float MarginM;
        public readonly bool IsSet;

        public ShoreEdge(Vector2 point, Vector2 outward, float marginM)
        {
            Point = point;
            float m = outward.magnitude;
            Outward = m > 0f ? outward / m : Vector2.zero;
            MarginM = Mathf.Max(0f, marginM);
            IsSet = m > 0f;
        }

        /// <summary>An edge from a compass bearing for the outward normal (0 = North, clockwise).</summary>
        public static ShoreEdge FromCompass(Vector2 point, float outwardDegrees, float marginM)
        {
            float r = outwardDegrees * Mathf.Deg2Rad;
            return new ShoreEdge(point, new Vector2(Mathf.Sin(r), Mathf.Cos(r)), marginM);
        }

        /// <summary>How far <paramref name="p"/> lies out over the water from the line (negative = on the land side).</summary>
        public float OutwardDistance(Vector2 p) => IsSet ? Vector2.Dot(p - Point, Outward) : float.PositiveInfinity;

        /// <summary><paramref name="p"/>, pushed straight out to the margin when it is short of it. Unset: <paramref name="p"/>.</summary>
        public Vector2 Clamp(Vector2 p)
        {
            if (!IsSet) return p;
            float d = Vector2.Dot(p - Point, Outward);
            return d < MarginM ? p + Outward * (MarginM - d) : p;
        }

        /// <summary>The same line, measured from <paramref name="origin"/> (the angler's stand), for offsets.</summary>
        public ShoreEdge RelativeTo(Vector2 origin) => IsSet ? new ShoreEdge(Point - origin, Outward, MarginM) : default;
    }

    /// <summary>
    /// <b>Where a shore angler stands to fish a spot</b>, as data on the spot: the plank he stands on, the
    /// way he faces, and the edge he fishes over. Read through <see cref="IShoreCatchFacts.TryGetStand"/>.
    /// </summary>
    public readonly struct ShoreStand
    {
        public readonly Vector2 Position;
        /// <summary>Compass heading he faces (0 = North, clockwise).</summary>
        public readonly float HeadingDegrees;
        public readonly ShoreEdge Edge;

        public ShoreStand(Vector2 position, float headingDegrees, in ShoreEdge edge)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            Edge = edge;
        }
    }
}
