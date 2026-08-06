using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE geometry of standing on an authored deck: point-in-polygon, nearest point on a polygon,
    /// a fitted height plane, and — the part that is easy to get wrong — the transform between the
    /// rig's honest hull metres and the SCREEN offsets the player's transform actually lives in.
    ///
    /// <para><b>Why a projection at all.</b> A deck polygon is hull-local metres: the lobster boat's
    /// cockpit sole runs from y −6.0 to y +0.55, 6.55 m of real deck. But the hull is DRAWN by the iso
    /// rigs' ¾ camera at 40° above the horizon, which squashes along-view distance on screen by
    /// sin(40°) ≈ 0.643 and leaves screen-X alone. Walk the polygon in raw metres and, with the boat
    /// pointing north, the player strolls 6.55 m up-screen while the drawn bow is only 4.2 m away —
    /// standing clean off the picture. That is the SAME defect the wake plume had
    /// (<see cref="WakeGrading.ForeshortenY"/>, "not even connected to it and way off to the stern"),
    /// and it is the same fix: project the way the ART projects, per artwork, and never rescale a
    /// constant onto a different lever arm.</para>
    ///
    /// <para><b>The decomposition that keeps it cheap.</b> The clamp runs in the HULL frame, where the
    /// polygon is heading-independent and never has to be re-projected; only the single resulting point
    /// is projected out to screen. So a per-tick clamp costs one crossing test and one projection, not a
    /// polygon transform (rule 7).</para>
    ///
    /// <para><b>The rotation half is already in the repo and unchanged.</b> The (x, y) rotation here is
    /// numerically identical to <c>DeckWalkController.DeckFrameToWorld</c> / its inverse — the deck
    /// frame's +Y is the drawn bow. All this adds on top is the artwork's sin(elev) squash on the
    /// rotated Y and the cos(elev) lift from the deck's own height, which is exactly
    /// <c>MountedRockPoseMath.Project(local, −heading, 0, 0, elev)</c> — the transform every other
    /// on-hull anchor already goes through. Pure, static, deterministic, NaN-safe.</para>
    /// </summary>
    public static class DeckAreaMath
    {
        /// <summary>The plan-view elevation: 90° = no foreshortening at all. The right answer for
        /// artwork that was never baked by a camera (the hand-drawn <c>FishingBoat_*</c> compass), and
        /// the default everywhere so an unmeasured hull keeps exactly the placement it has today.</summary>
        public const float PlanViewElevationDegrees = 90f;

        // ---- the deck-frame ↔ screen transform ----------------------------------------------------

        /// <summary>
        /// A DECK-FRAME point (x abeam toward starboard, y along the keel toward the bow, both hull
        /// metres) at height <paramref name="heightMeters"/> above the keel, as a boat-relative WORLD
        /// (screen-axis) offset for a hull DRAWN at compass heading <paramref name="drawnHeadingDeg"/>
        /// (0 = North, 90 = East, clockwise) by artwork baked at
        /// <paramref name="bakeElevationDegrees"/> above the horizon.
        ///
        /// <para>At <see cref="PlanViewElevationDegrees"/> this is the plain rotation the deck-walk has
        /// always used — the un-measured hull's behaviour is preserved bit-for-bit.</para>
        /// </summary>
        public static Vector2 DeckToWorld(Vector2 deckOffset, float heightMeters, float drawnHeadingDeg,
                                          float bakeElevationDegrees = PlanViewElevationDegrees)
        {
            float rad = Safe(drawnHeadingDeg) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float dx = Safe(deckOffset.x), dy = Safe(deckOffset.y);

            // The rotation, verbatim from DeckWalkController.DeckFrameToWorld: deck +Y → the drawn bow.
            float rx = dx * cos + dy * sin;
            float ry = -dx * sin + dy * cos;

            float s = WakeGrading.ForeshortenY(bakeElevationDegrees);
            float c = LiftZ(bakeElevationDegrees);
            return new Vector2(rx, ry * s + Safe(heightMeters) * c);
        }

        /// <summary>
        /// The exact inverse of <see cref="DeckToWorld"/>: a boat-relative WORLD offset read back as a
        /// deck-frame point, given the height of the deck it was standing on. The height must be
        /// supplied because the projection collapses two unknowns (along-hull distance and height) onto
        /// one screen axis — an offset alone cannot say whether you are aft on the sole or forward on
        /// the foredeck, and guessing is how a player teleports when they cross a step.
        /// </summary>
        public static Vector2 WorldToDeck(Vector2 worldOffset, float heightMeters, float drawnHeadingDeg,
                                          float bakeElevationDegrees = PlanViewElevationDegrees)
        {
            float s = WakeGrading.ForeshortenY(bakeElevationDegrees);
            float c = LiftZ(bakeElevationDegrees);
            float rx = Safe(worldOffset.x);
            float ry = (Safe(worldOffset.y) - Safe(heightMeters) * c) / Mathf.Max(1e-4f, s);

            float rad = Safe(drawnHeadingDeg) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(rx * cos - ry * sin, rx * sin + ry * cos);
        }

        /// <summary>
        /// A screen-axis input DIRECTION as the deck-frame direction that DRAWS along it — the walk's
        /// input transform. Because the projection is linear, the returned direction projects back
        /// exactly parallel to the input: press up-screen and you still walk up-screen, at every
        /// heading. Its LENGTH is deliberately not preserved (the caller re-normalises), because a step
        /// is a distance on the DECK, not on the screen: a metre of deck walked into the foreshortened
        /// axis covers less screen, which is what a ¾ camera means.
        /// </summary>
        public static Vector2 WorldDirectionToDeck(Vector2 worldDirection, float drawnHeadingDeg,
                                                   float bakeElevationDegrees = PlanViewElevationDegrees)
            => WorldToDeck(worldDirection, 0f, drawnHeadingDeg, bakeElevationDegrees);

        /// <summary>How far up-screen one metre of HEIGHT above the keel carries: <c>cos(elev)</c>, and
        /// exactly 0 for a plan view (looking straight down, height is invisible). Degenerate elevations
        /// collapse to 0 for the same reason <see cref="WakeGrading.ForeshortenY"/> collapses to 1: the
        /// pair of them is then the identity, so a half-authored visual degrades to today's placement
        /// rather than to a player standing somewhere new.</summary>
        public static float LiftZ(float bakeElevationDegrees)
        {
            if (float.IsNaN(bakeElevationDegrees)) return 0f;
            if (bakeElevationDegrees <= 0f || bakeElevationDegrees >= 90f) return 0f;
            return Mathf.Cos(bakeElevationDegrees * Mathf.Deg2Rad);
        }

        // ---- polygon geometry ----------------------------------------------------------------------

        /// <summary>The axis-aligned bounds (minX, minY, maxX, maxY) of a polygon — what the importer
        /// bakes into <see cref="DeckArea.Bounds"/>. An empty polygon reports a zero box.</summary>
        public static Vector4 BoundsOf(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length == 0) return Vector4.zero;
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 v = polygon[i];
                if (v.x < minX) minX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.x > maxX) maxX = v.x;
                if (v.y > maxY) maxY = v.y;
            }
            return new Vector4(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Is the deck-frame point inside the polygon? The standard even-odd crossing test, with the
        /// baked <paramref name="bounds"/> as a one-compare reject first (pass <see cref="Vector4.zero"/>
        /// to skip it — the tests do). Points exactly on an edge may read either way; that is harmless
        /// here because the caller clamps an outside read straight back onto the outline.
        /// </summary>
        public static bool Contains(Vector2[] polygon, Vector4 bounds, Vector2 point)
        {
            if (polygon == null || polygon.Length < 3) return false;
            float px = Safe(point.x), py = Safe(point.y);

            bool boundsUsable = bounds.z > bounds.x || bounds.w > bounds.y;
            if (boundsUsable && (px < bounds.x || px > bounds.z || py < bounds.y || py > bounds.w))
                return false;

            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                float xi = polygon[i].x, yi = polygon[i].y;
                float xj = polygon[j].x, yj = polygon[j].y;
                if ((yi > py) == (yj > py)) continue;
                float t = (py - yi) / (yj - yi);
                if (px < xi + t * (xj - xi)) inside = !inside;
            }
            return inside;
        }

        /// <summary>Convenience overload that computes the bounds itself — tests and one-off callers.</summary>
        public static bool Contains(Vector2[] polygon, Vector2 point)
            => Contains(polygon, Vector4.zero, point);

        /// <summary>
        /// The closest point ON the polygon's outline to <paramref name="point"/> (deck frame), with the
        /// squared distance out. Walks every edge — allocation-free, and only ever reached when the
        /// player has pressed off the deck, never while they stand inside it.
        /// </summary>
        public static Vector2 ClosestPointOnOutline(Vector2[] polygon, Vector2 point, out float sqrDistance)
        {
            sqrDistance = float.PositiveInfinity;
            if (polygon == null || polygon.Length == 0) return point;
            if (polygon.Length == 1) { sqrDistance = (polygon[0] - point).sqrMagnitude; return polygon[0]; }

            Vector2 p = new Vector2(Safe(point.x), Safe(point.y));
            Vector2 best = polygon[0];
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 c = ClosestPointOnSegment(polygon[j], polygon[i], p);
                float sqr = (c - p).sqrMagnitude;
                if (sqr >= sqrDistance) continue;
                sqrDistance = sqr;
                best = c;
            }
            return best;
        }

        /// <summary>The closest point on the segment a→b to p. Pure; a degenerate segment returns a.</summary>
        public static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            if (lenSqr <= 1e-12f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
            return a + ab * t;
        }

        /// <summary>The height (m above the keel) of a fitted plane <c>z = a·x + b·y + c</c> at a
        /// deck-frame point. Flat areas carry a = b = 0, so this is their authored z exactly.</summary>
        public static float HeightAt(Vector3 plane, Vector2 deckPoint)
            => plane.x * Safe(deckPoint.x) + plane.y * Safe(deckPoint.y) + plane.z;

        /// <summary>
        /// Least-squares fit of <c>z = a·x + b·y + c</c> through authored (x, y, z) vertices — how the
        /// importer turns a <c>polygon3d</c> into an O(1) height read. Returns (a, b, c); degenerate
        /// input (fewer than 3 points, or all points collinear in plan) falls back to the flat mean,
        /// which is the honest answer for a shape with no plan area to tilt over.
        /// </summary>
        public static Vector3 FitHeightPlane(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0) return Vector3.zero;

            double n = vertices.Length, sx = 0, sy = 0, sz = 0, sxx = 0, sxy = 0, syy = 0, sxz = 0, syz = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                double x = vertices[i].x, y = vertices[i].y, z = vertices[i].z;
                sx += x; sy += y; sz += z;
                sxx += x * x; sxy += x * y; syy += y * y;
                sxz += x * z; syz += y * z;
            }

            double meanZ = sz / n;
            if (vertices.Length < 3) return new Vector3(0f, 0f, (float)meanZ);

            // Centred normal equations — the 2×2 covariance of (x, y) against the covariance with z.
            double cxx = sxx - sx * sx / n, cxy = sxy - sx * sy / n, cyy = syy - sy * sy / n;
            double cxz = sxz - sx * sz / n, cyz = syz - sy * sz / n;
            double det = cxx * cyy - cxy * cxy;
            if (System.Math.Abs(det) < 1e-9) return new Vector3(0f, 0f, (float)meanZ);

            double a = (cxz * cyy - cyz * cxy) / det;
            double b = (cyz * cxx - cxz * cxy) / det;
            double c = (sz - a * sx - b * sy) / n;
            return new Vector3((float)a, (float)b, (float)c);
        }

        /// <summary>The worst |authored z − fitted z| over the vertices (m) — the fit's honest error bar,
        /// which the importer records on the area and reports.</summary>
        public static float HeightPlaneResidualMax(Vector3[] vertices, Vector3 plane)
        {
            if (vertices == null || vertices.Length == 0) return 0f;
            float worst = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                float fitted = HeightAt(plane, new Vector2(vertices[i].x, vertices[i].y));
                float err = Mathf.Abs(vertices[i].z - fitted);
                if (err > worst) worst = err;
            }
            return worst;
        }

        private static float Safe(float v) => float.IsNaN(v) ? 0f : v;
    }
}
