using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE NAV LOCKER — the one place the player's chart knowledge (waypoints, the planned route, the
    /// track breadcrumb) is read and written.
    ///
    /// <para><b>Why here, and why per SAVE.</b> Same reason as <see cref="InstrumentLocker"/>: state
    /// three lanes touch turns into three hand-rolled find-the-row loops, which is how an off-by-one
    /// gets into a save file. But unlike an instrument, this is not equipment — a waypoint is the
    /// skipper's knowledge of the water. Buying a bigger boat does not make you forget where the wreck
    /// is, so these rows are keyed by REGION, not by hull. The glass that displays them is still owned
    /// per hull (<see cref="InstrumentLocker"/>); the knowledge is not.</para>
    ///
    /// <para><b>Bounded by construction.</b> Every writer here enforces the caps in
    /// <see cref="ChartplotterSettings"/>, and the two limits behave DIFFERENTLY on purpose: marking a
    /// waypoint at the cap is REFUSED (a waypoint is a deliberate act — losing one silently would be
    /// worse than being told no), while the track is a true ring buffer that drops its oldest crumb
    /// (a breadcrumb is a record, and the oldest is the right thing to lose).</para>
    ///
    /// <para>Static and null-safe throughout: a null save reads as "no navigation" rather than
    /// throwing — the established greybox posture.</para>
    /// </summary>
    public static class NavLocker
    {
        // ---- waypoints ------------------------------------------------------------------------------

        /// <summary>
        /// Fill <paramref name="into"/> (cleared first) with every VALID waypoint in
        /// <paramref name="regionId"/> and return it. Caller-provided list so the chart's per-repaint
        /// read allocates nothing (rule 7).
        /// </summary>
        public static List<NavWaypoint> WaypointsIn(SaveData save, string regionId, List<NavWaypoint> into)
        {
            into ??= new List<NavWaypoint>();
            into.Clear();
            if (save?.NavWaypoints == null || string.IsNullOrEmpty(regionId)) return into;
            for (int i = 0; i < save.NavWaypoints.Count; i++)
            {
                NavWaypoint w = save.NavWaypoints[i].ToWaypoint();
                if (w.IsValid && w.RegionId == regionId) into.Add(w);
            }
            return into;
        }

        /// <summary>How many waypoints exist across the whole save (what the cap is measured against).</summary>
        public static int WaypointCount(SaveData save) => save?.NavWaypoints?.Count ?? 0;

        /// <summary>
        /// Mark a waypoint. Returns false — and writes nothing — when the save is null, the row is
        /// unusable (no region, or a NaN/infinite position), or the save is already at
        /// <see cref="ChartplotterSettings.MaxWaypoints"/>. A refusal is the honest answer at the cap;
        /// see the class remarks for why this does not silently evict.
        /// </summary>
        public static bool AddWaypoint(SaveData save, in NavWaypoint w, in ChartplotterSettings cfg)
        {
            if (save == null || !w.IsValid) return false;
            save.NavWaypoints ??= new List<NavWaypointDto>();
            int cap = cfg.MaxWaypoints > 0 ? cfg.MaxWaypoints : ChartplotterSettings.Default.MaxWaypoints;
            if (save.NavWaypoints.Count >= cap) return false;
            save.NavWaypoints.Add(new NavWaypointDto(in w));
            return true;
        }

        /// <summary>
        /// Remove the waypoint nearest <paramref name="pos"/> within <paramref name="radiusMetres"/> in
        /// this region. Returns true iff a row was removed. Nearest-within-radius rather than exact
        /// match because the caller is a fingertip on a chart, not an index.
        /// </summary>
        public static bool RemoveWaypointNear(SaveData save, string regionId, Vector2 pos,
                                              float radiusMetres)
        {
            if (save?.NavWaypoints == null || string.IsNullOrEmpty(regionId)) return false;
            if (!NavMath.IsFinite(pos) || radiusMetres <= 0f) return false;
            int best = -1;
            float bestSq = radiusMetres * radiusMetres;
            for (int i = 0; i < save.NavWaypoints.Count; i++)
            {
                NavWaypoint w = save.NavWaypoints[i].ToWaypoint();
                if (!w.IsValid || w.RegionId != regionId) continue;
                float sq = (w.Pos - pos).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = i;
            }
            if (best < 0) return false;
            save.NavWaypoints.RemoveAt(best);
            return true;
        }

        // ---- the planned route ----------------------------------------------------------------------

        /// <summary>Fill <paramref name="into"/> (cleared first) with the route's points in this region,
        /// in order. A route that belongs to another region reads as empty rather than as a line drawn
        /// across the wrong water.</summary>
        public static List<Vector2> RouteIn(SaveData save, string regionId, List<Vector2> into)
        {
            into ??= new List<Vector2>();
            into.Clear();
            if (save?.NavRoute == null || string.IsNullOrEmpty(regionId)) return into;
            for (int i = 0; i < save.NavRoute.Count; i++)
            {
                NavRoutePointDto p = save.NavRoute[i];
                if (p.RegionId != regionId) continue;
                Vector2 v = p.Pos;
                if (NavMath.IsFinite(v)) into.Add(v);
            }
            return into;
        }

        /// <summary>
        /// Append a point to the planned route. Returns false at the leg cap or for an unusable point.
        /// A route point in a DIFFERENT region than the existing route replaces it — you cannot plan one
        /// passage across two frames, and silently mixing them would draw a leg through nothing.
        /// </summary>
        public static bool AddRoutePoint(SaveData save, string regionId, Vector2 pos,
                                         in ChartplotterSettings cfg)
        {
            if (save == null || string.IsNullOrEmpty(regionId) || !NavMath.IsFinite(pos)) return false;
            save.NavRoute ??= new List<NavRoutePointDto>();
            if (save.NavRoute.Count > 0 && save.NavRoute[0].RegionId != regionId) save.NavRoute.Clear();
            int maxLegs = cfg.MaxRouteLegs > 0 ? cfg.MaxRouteLegs : ChartplotterSettings.Default.MaxRouteLegs;
            if (save.NavRoute.Count >= maxLegs + 1) return false;   // N legs = N+1 points
            save.NavRoute.Add(new NavRoutePointDto(regionId, pos));
            return true;
        }

        /// <summary>Clear the planned route. Returns true iff anything was there.</summary>
        public static bool ClearRoute(SaveData save)
        {
            if (save?.NavRoute == null || save.NavRoute.Count == 0) return false;
            save.NavRoute.Clear();
            return true;
        }

        // ---- the track breadcrumb -------------------------------------------------------------------

        /// <summary>Fill <paramref name="into"/> (cleared first) with this region's track crumbs, oldest
        /// first.</summary>
        public static List<Vector2> TrackIn(SaveData save, string regionId, List<Vector2> into)
        {
            into ??= new List<Vector2>();
            into.Clear();
            if (save?.NavTrack == null || string.IsNullOrEmpty(regionId)) return into;
            for (int i = 0; i < save.NavTrack.Count; i++)
            {
                NavTrackPoint p = save.NavTrack[i].ToPoint();
                if (p.IsValid && p.RegionId == regionId) into.Add(p.Pos);
            }
            return into;
        }

        /// <summary>
        /// Record where the boat is, if it has moved far enough since the last crumb IN THIS REGION.
        /// Returns true iff a crumb was written.
        ///
        /// <para>This is the whole track rule, and it is a pure function of the save, the position and
        /// the settings — no time, no frame rate, no instrument. That is what lets the track accrue
        /// identically whether or not a plotter is fitted or drawn (the honesty invariant), and what
        /// makes it testable with no scene at all.</para>
        ///
        /// <para>The ring: at <see cref="ChartplotterSettings.MaxTrackPoints"/> the OLDEST crumb is
        /// dropped. The spacing gate is measured against the last crumb of the SAME region, so crossing
        /// a region boundary always lays a fresh first crumb rather than comparing against a position
        /// in another frame.</para>
        /// </summary>
        public static bool RecordTrack(SaveData save, string regionId, Vector2 pos,
                                       in ChartplotterSettings cfg)
        {
            if (save == null || string.IsNullOrEmpty(regionId) || !NavMath.IsFinite(pos)) return false;
            save.NavTrack ??= new List<NavTrackPointDto>();

            float spacing = cfg.TrackMinSpacingMetres > 0f
                ? cfg.TrackMinSpacingMetres
                : ChartplotterSettings.Default.TrackMinSpacingMetres;

            // The last crumb in THIS region — walking back rather than taking the list's tail, so a
            // trip to another region and home again still gates against where we actually left off.
            for (int i = save.NavTrack.Count - 1; i >= 0; i--)
            {
                NavTrackPointDto d = save.NavTrack[i];
                if (d.RegionId != regionId) continue;
                Vector2 last = new Vector2(d.X, d.Y);
                if (NavMath.IsFinite(last) && (pos - last).sqrMagnitude < spacing * spacing) return false;
                break;
            }

            int cap = cfg.MaxTrackPoints > 1 ? cfg.MaxTrackPoints : ChartplotterSettings.Default.MaxTrackPoints;
            save.NavTrack.Add(new NavTrackPointDto(regionId, pos));
            while (save.NavTrack.Count > cap) save.NavTrack.RemoveAt(0);
            return true;
        }

        /// <summary>Erase the whole track (the plotter's TRACK-clear). Returns true iff anything went.</summary>
        public static bool ClearTrack(SaveData save)
        {
            if (save?.NavTrack == null || save.NavTrack.Count == 0) return false;
            save.NavTrack.Clear();
            return true;
        }
    }
}
