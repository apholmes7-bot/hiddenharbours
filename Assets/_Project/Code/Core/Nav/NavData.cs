using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>The symbol a marked waypoint draws with (navRig.js:32 <c>wpt</c> palette keys). Values are
    /// PERSISTED — append only, never renumber.</summary>
    public enum NavWaypointKind
    {
        Mark = 0,
        Anchor = 1,
        Fuel = 2,
        Hazard = 3,
        Dest = 4,
    }

    /// <summary>
    /// One marked position on the chart, in WORLD METRES, stamped with the region it belongs to.
    ///
    /// <para><b>Why region-stamped rather than region-partitioned.</b> A world position only means
    /// anything inside its own region's frame — the same (x, y) is a different patch of sea in
    /// St Peters and in Coddle Cove. Carrying the id on the row keeps ONE flat, absolutely-bounded
    /// list (see <see cref="ChartplotterSettings.MaxWaypoints"/>) instead of a per-region map whose
    /// total size grows with the number of places the player has visited.</para>
    /// </summary>
    public readonly struct NavWaypoint
    {
        public readonly string RegionId;
        public readonly string Name;
        public readonly Vector2 Pos;          // world metres, the frame ITidalTerrain samples in
        public readonly NavWaypointKind Kind;

        public NavWaypoint(string regionId, string name, Vector2 pos, NavWaypointKind kind)
        {
            RegionId = regionId ?? "";
            Name = name ?? "";
            Pos = pos;
            Kind = kind;
        }

        /// <summary>Is this row usable? A NaN/infinite position is unrenderable and a region-less row
        /// can never be matched to a chart, so both are dropped rather than drawn somewhere wrong.</summary>
        public bool IsValid => !string.IsNullOrEmpty(RegionId) && NavMath.IsFinite(Pos);
    }

    /// <summary>One breadcrumb of where the boat has actually been (world metres, region-stamped).</summary>
    public readonly struct NavTrackPoint
    {
        public readonly string RegionId;
        public readonly Vector2 Pos;

        public NavTrackPoint(string regionId, Vector2 pos)
        {
            RegionId = regionId ?? "";
            Pos = pos;
        }

        public bool IsValid => !string.IsNullOrEmpty(RegionId) && NavMath.IsFinite(Pos);
    }

    /// <summary>
    /// The chart's pure navigation maths — bearings, distances, route lengths and the unit the whole
    /// instrument reads in.
    ///
    /// <para><b>One bearing convention, not two.</b> Every bearing here forwards to
    /// <see cref="BoatKinematics.BearingDegrees"/>, the compass convention the boat, the HUD and the
    /// binnacle compass already share (0 = N = +Y, 90 = E = +X, clockwise). The rig computes its own
    /// <c>atan2(dx, -dy)</c> because a canvas measures Y DOWNWARD and the game world does not; porting
    /// that formula literally would have put a second, silently-flipped bearing in the codebase.</para>
    ///
    /// <para><b>Metres in, nautical miles out.</b> The world is authored in metres
    /// (<c>RegionDef.WorldSizeMeters</c>); the instrument reads in NM because that is what the rig
    /// draws and what a plotter shows. The conversion lives here once.</para>
    /// </summary>
    public static class NavMath
    {
        /// <summary>Metres in one nautical mile (the international definition, exact).</summary>
        public const double MetresPerNauticalMile = 1852.0;

        public static bool IsFinite(Vector2 v)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

        /// <summary>Compass bearing FROM one world point TO another, 0 = N, clockwise, [0, 360).
        /// Coincident points have no bearing and resolve to 0 (North), the shared fallback.</summary>
        public static float BearingDegrees(Vector2 from, Vector2 to)
            => BoatKinematics.BearingDegrees(to - from);

        public static float DistanceMetres(Vector2 a, Vector2 b) => (b - a).magnitude;

        /// <summary>
        /// The unit vector a compass bearing points along — the exact inverse of
        /// <see cref="BoatKinematics.BearingDegrees"/> (0 = N = +Y, 90 = E = +X).
        ///
        /// <para>Here rather than at the call site for this class's founding reason: the rig computes
        /// its own <c>dir(a) = {sin a, −cos a}</c> (navRig.js:121) because a canvas measures Y
        /// DOWNWARD, and porting that literally would put a second, silently-flipped direction beside
        /// the one bearing convention the boat, the HUD and the compass already share. The depth-ahead
        /// profile is the first consumer; the set arrow and any future "run this bearing out" read the
        /// same one.</para>
        /// FLAG lead-architect: additive helper on the ADR 0025 S6 nav-maths seam.
        /// </summary>
        public static Vector2 DirectionFromBearing(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        public static float MetresToNM(float metres) => (float)(metres / MetresPerNauticalMile);

        public static float NMToMetres(float nm) => (float)(nm * MetresPerNauticalMile);

        /// <summary>
        /// Speed over ground in metres per second → KNOTS, which is what the plotter's SOG field prints.
        /// FLAG lead-architect: additive helper on the ADR 0025 S6 nav-maths seam.
        ///
        /// <para>Here rather than at the call site for the reason this class exists: the sim measures
        /// speed in m/s (<see cref="BoatKinematics.SpeedOverGround"/>) and every instrument that shows
        /// it wants knots, so the factor lives once beside the distance conversion it is derived
        /// from.</para>
        /// </summary>
        public static float MetresPerSecondToKnots(float mps)
            => (float)(mps * 3600.0 / MetresPerNauticalMile);

        /// <summary>Distance between two world points in nautical miles.</summary>
        public static float DistanceNM(Vector2 a, Vector2 b) => MetresToNM(DistanceMetres(a, b));

        /// <summary>
        /// Total length of a route in metres — the sum of its legs (navRig.js:553 <c>routeLenNM</c>).
        /// A route of fewer than two points has no length. Null-safe.
        /// </summary>
        public static float RouteLengthMetres(System.Collections.Generic.IReadOnlyList<Vector2> route)
        {
            if (route == null || route.Count < 2) return 0f;
            float sum = 0f;
            for (int i = 1; i < route.Count; i++) sum += DistanceMetres(route[i - 1], route[i]);
            return sum;
        }

        /// <summary>Time to go, in MINUTES, covering <paramref name="metres"/> at
        /// <paramref name="knots"/> (navRig.js:408 <c>tot/spd*60</c>). A standstill has no ETA —
        /// returns <see cref="float.PositiveInfinity"/>, which the formatter renders as "--:--"
        /// rather than an invented number.</summary>
        public static float MinutesToGo(float metres, float knots)
        {
            if (knots <= 0f) return float.PositiveInfinity;
            return MetresToNM(metres) / knots * 60f;
        }

        /// <summary>Wrap to [0, 360) — the rig's <c>norm360</c>.</summary>
        public static float Norm360(float deg)
        {
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return deg;
        }
    }
}
