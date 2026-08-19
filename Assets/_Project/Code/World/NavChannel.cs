using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Which side of a channel a mark stands on, seen by a vessel travelling the channel's
    /// <b>direction of buoyage</b> (seaward → harbour). Port is her left hand, starboard her right.
    ///
    /// <para><b>Namespace-level, deliberately not nested</b> — the same CS0426 lesson
    /// <see cref="CoastRunSector"/> carries.</para>
    /// </summary>
    public enum NavChannelHand
    {
        /// <summary>The traveller's LEFT hand going in. IALA Region B: GREEN.</summary>
        Port = 0,
        /// <summary>The traveller's RIGHT hand going in. IALA Region B: RED ("red right returning").</summary>
        Starboard = 1,
    }

    /// <summary>The four cardinal quadrants. A cardinal mark says "the SAFE water is on my
    /// <see cref="NavCardinalQuadrant"/> side" — so a North cardinal is passed to its north.</summary>
    public enum NavCardinalQuadrant
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>
    /// <b>One authored fairway, and the direction you are buoying it in.</b> A channel is DATA
    /// (CLAUDE.md rule 2): a route the owner proposes, a width, and the claim it makes about what can
    /// use it. Nothing here is a mark — <see cref="NavMarkPlan"/> derives those from this plus the
    /// seabed, so re-siting a route moves every mark on it and no coordinate is typed twice.
    ///
    /// <para><b>⭐ THE AUTHORED ORDER IS THE DIRECTION OF BUOYAGE, AND THAT IS THE WHOLE POINT.</b>
    /// <see cref="Waypoints"/>[0] is the SEAWARD end and the last is the harbour end, so
    /// "returning from seaward" is a property of the table rather than a note in a comment. IALA
    /// Region B (this is Canada) then decides the colours by arithmetic — red to starboard, green to
    /// port, going in — and no buoy's hand is ever hand-typed. Reverse the waypoint list and every
    /// mark on the channel swaps sides, which is exactly what reversing a direction of buoyage
    /// means.</para>
    ///
    /// <para><b>⚠ THE ROUTE IS A PROPOSAL; THE SEABED DECIDES.</b> The authored polyline says roughly
    /// where the owner wants the fairway. <see cref="NavMarkPlan"/> snaps each station to the deepest
    /// water within <see cref="SearchHalfWidthMetres"/> of it, so a channel follows the deep water
    /// that is actually painted rather than the line somebody drew. On a flat approach the snap is a
    /// no-op and the authored line stands — which is the honest outcome, not a broken snap.</para>
    ///
    /// <para><b>The claim, and why it is a tide FRACTION.</b> A channel into a drying harbour is a
    /// real channel; it is simply not a channel at every hour. So a route states the draught it
    /// carries (<see cref="DeclaredDraughtMetres"/>) and the state of tide it carries it at
    /// (<see cref="NavigableTideFraction"/>, a fraction of the region's own amplitude — never metres,
    /// the 2026-08-01 pacing lesson). The content test then measures the fairway against that claim,
    /// so a route laid over a shoal fails loudly instead of quietly marking mud as safe.</para>
    /// </summary>
    public sealed class NavChannel
    {
        /// <summary>Stable id, append-only (CLAUDE.md §5): <c>channel.snake_case</c>.</summary>
        public string Id = "channel.unnamed";

        /// <summary>What a skipper calls it — used in log lines and test failures.</summary>
        public string DisplayName = "Unnamed channel";

        /// <summary>⭐ AUTHORED SEAWARD → HARBOUR. Index 0 is the seaward end; the last is the
        /// destination. This order IS the direction of buoyage.</summary>
        public Vector2[] Waypoints = new Vector2[0];

        /// <summary>Half-width of the marked fairway (m) — how far off the centreline the marks
        /// stand. The gap between a pair is twice this.</summary>
        public float HalfWidthMetres = 18f;

        /// <summary>How far off the authored line (m) the derived centreline may wander to find
        /// deeper water. 0 pins the route to exactly what was authored.</summary>
        public float SearchHalfWidthMetres = 20f;

        /// <summary>
        /// How far apart singles stand along this channel's straights (m). <b>0 inherits the region's
        /// <see cref="NavMarkTuning.StraightSpacingMetres"/></b>, which is the normal case.
        ///
        /// <para>⚠ It exists because a bay approach and a gut through a bar do not want the same
        /// pacing, and the difference is not cosmetic: a 90 m gut whose middle DRIES will refuse a
        /// mid-straight single on every single build, forever. A permanent refusal is not a finding —
        /// it is noise that teaches the owner to skim the refusal list, which is where the real
        /// findings live.</para>
        /// </summary>
        public float StraightSpacingMetres;

        /// <summary>Which rung of the mark's size ladder this channel wears — the kit bakes
        /// s12 (1.2 m, 0–10 m of water) … s30 (3.0 m, landfall). A creek gets s12; a bay approach
        /// s18.</summary>
        public string SizeId = "s12";

        /// <summary>Does this channel's LANDFALL pair carry a light? Only the seaward-most pair is
        /// lit — a lit mark every 40 m is a runway, not a harbour.</summary>
        public bool LitLandfall;

        /// <summary>The draught (m) this route claims to carry. Ties to the boat ladder: 0.6 is the
        /// skiff tier, 1.30 the lobster boat, 1.40 the Cape Islander, 2.90 the side dragger.</summary>
        public float DeclaredDraughtMetres = 0.6f;

        /// <summary>The state of tide the claim is made at, as a FRACTION of the region's tide
        /// amplitude (−1 = spring low, 0 = mean, +1 = spring high). A drying harbour states a
        /// positive fraction and is honest about it.</summary>
        public float NavigableTideFraction;

        /// <summary>Clearance (m) demanded under the declared draught before a fairway counts as
        /// carrying it. Keel-scraping is not navigable.</summary>
        public float KeelClearanceMetres = 0.2f;

        /// <summary>Total length of the authored polyline (m).</summary>
        public float AuthoredLength => NavChannelGeometry.PolylineLength(Waypoints);
    }

    /// <summary>
    /// One authored <b>danger</b> and the cardinal that guards it. The mark's quadrant is not typed
    /// beside a position and hoped for — <see cref="NavMarkPlan"/> checks it against the seabed, so a
    /// north cardinal whose danger is not actually to the south is refused rather than shipped.
    /// </summary>
    public sealed class NavCardinal
    {
        /// <summary>Stable id, append-only: <c>mark.snake_case</c>.</summary>
        public string Id = "mark.unnamed";

        /// <summary>What it guards, in words — goes into the build log and the test failure.</summary>
        public string DisplayName = "Unnamed danger";

        /// <summary>Which quadrant holds the SAFE water. A North cardinal is passed to its north.</summary>
        public NavCardinalQuadrant Quadrant = NavCardinalQuadrant.North;

        /// <summary>Where the mark is proposed. Refused if it does not float at spring low.</summary>
        public Vector2 At;

        /// <summary>Size rung from the kit's ladder.</summary>
        public string SizeId = "s12";

        /// <summary>How far either way (m) the quadrant check probes the seabed. Big enough to clear
        /// the danger's own slope, small enough to still be about THIS danger.</summary>
        public float ProbeMetres = 24f;
    }

    /// <summary>
    /// The <b>pure geometry of a buoyed channel</b> — polyline walking, the seaward→harbour normal,
    /// and the one arithmetic step that turns a side into an IALA colour. Every method is a pure
    /// function of its arguments (no scene, no RNG, no time), so the whole scheme is EditMode-
    /// assertable without loading anything and comes out the same on every machine (rule 5).
    ///
    /// <para><b>⚠ THE HAND IS DERIVED, NEVER TYPED.</b> Walking the waypoints in their authored order
    /// the vessel's LEFT is <c>(−d.y, d.x)</c> and her RIGHT is <c>(d.y, −d.x)</c>; port is the left,
    /// starboard the right, and Region B paints starboard RED. That chain is the only place a colour
    /// is decided in this feature. Hand-typing "red" onto a buoy would let the colour and the side
    /// disagree, which is the single defect a lateral system exists to make impossible.</para>
    /// </summary>
    public static class NavChannelGeometry
    {
        /// <summary>Length of a polyline (m). 0 for fewer than two points.</summary>
        public static float PolylineLength(Vector2[] points)
        {
            if (points == null || points.Length < 2) return 0f;
            float total = 0f;
            for (int i = 1; i < points.Length; i++) total += Vector2.Distance(points[i - 1], points[i]);
            return total;
        }

        /// <summary>
        /// The point <paramref name="along"/> metres from the start of the polyline, plus the unit
        /// direction of travel there. Clamps to the ends. Direction on a vertex is the direction of
        /// the segment the station falls in, which keeps a mark on a turn square to the leg it marks.
        /// </summary>
        public static Vector2 PointAlong(Vector2[] points, float along, out Vector2 direction)
        {
            direction = Vector2.right;
            if (points == null || points.Length == 0) return Vector2.zero;
            if (points.Length == 1) return points[0];

            float remaining = Mathf.Max(0f, along);
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 a = points[i - 1];
                Vector2 b = points[i];
                float seg = Vector2.Distance(a, b);
                if (seg <= 1e-4f) continue;
                direction = (b - a) / seg;
                if (remaining <= seg) return a + direction * remaining;
                remaining -= seg;
            }
            return points[points.Length - 1];
        }

        /// <summary>The vessel's LEFT hand (port side) as a unit vector, given her direction of
        /// travel. The 90° rotation that this repo's other coasts also use.</summary>
        public static Vector2 PortNormal(Vector2 direction) => new Vector2(-direction.y, direction.x);

        /// <summary>The vessel's RIGHT hand (starboard side) as a unit vector.</summary>
        public static Vector2 StarboardNormal(Vector2 direction) => new Vector2(direction.y, -direction.x);

        /// <summary>The unit offset for a hand — the ONE place a side becomes a direction.</summary>
        public static Vector2 Normal(Vector2 direction, NavChannelHand hand) =>
            hand == NavChannelHand.Port ? PortNormal(direction) : StarboardNormal(direction);

        /// <summary>
        /// ⭐ <b>IALA REGION B, and it is one line because it has to be.</b> Returning FROM SEAWARD,
        /// <b>red to starboard, green to port</b> — "red right returning", as the Canadian Coast Guard
        /// flies it. Region A is the mirror of this and is NOT what this world uses.
        /// </summary>
        public static bool IsRed(NavChannelHand hand) => hand == NavChannelHand.Starboard;

        /// <summary>
        /// The kit type a lateral mark of this hand wears. <c>lit</c> picks the lighted variant of
        /// the same hand — never a different hand, which is why the light flag cannot change a colour.
        /// </summary>
        public static string LateralMarkType(NavChannelHand hand, bool lit) =>
            hand == NavChannelHand.Starboard
                ? (lit ? "StbdLit" : "StbdNun")
                : (lit ? "PortLit" : "PortCan");

        /// <summary>The kit type for a cardinal quadrant.</summary>
        public static string CardinalMarkType(NavCardinalQuadrant quadrant)
        {
            switch (quadrant)
            {
                case NavCardinalQuadrant.North: return "CardinalN";
                case NavCardinalQuadrant.East:  return "CardinalE";
                case NavCardinalQuadrant.South: return "CardinalS";
                default:                        return "CardinalW";
            }
        }

        /// <summary>
        /// The unit vector pointing into a cardinal's SAFE quadrant — the way you pass it. Compass
        /// frame (N = +y, E = +x), the same frame <see cref="CoastPlan"/> and
        /// <see cref="MainlandCoast"/> measure bearings in.
        /// </summary>
        public static Vector2 SafeDirection(NavCardinalQuadrant quadrant)
        {
            switch (quadrant)
            {
                case NavCardinalQuadrant.North: return Vector2.up;
                case NavCardinalQuadrant.East:  return Vector2.right;
                case NavCardinalQuadrant.South: return Vector2.down;
                default:                        return Vector2.left;
            }
        }

        /// <summary>Compass bearing (N = 0, clockwise) of a direction vector, in [0, 360).</summary>
        public static float CompassBearing(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 1e-8f) return 0f;
            float deg = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            return deg < 0f ? deg + 360f : deg;
        }

        /// <summary>
        /// ⚠️ <b>THE NAV-BUOY KIT IS CLOCKWISE, AND THIS IS THE ONE PLACE THAT MATTERS.</b> Cell
        /// order is N NE E SE S SW W NW and cell <c>i</c> depicts compass heading <c>+45°·i</c> — the
        /// OPPOSITE handedness to every boat in the fleet, whose sheets bake counter-clockwise. So
        /// the mapping is a plain divide with NO negation: applying the fleet's correction here
        /// mirrors all eight cells. The kit's SCREEN mean (−46.7525°/step) is numerically identical
        /// to the figure the counter-clockwise rigs record, so it cannot be used to check this; only
        /// the un-squashed ground bearing can, and it was measured at bake time.
        /// </summary>
        public static int FacingCell(float compassBearing)
        {
            float wrapped = Mathf.Repeat(compassBearing, 360f);
            return Mathf.RoundToInt(wrapped / 45f) % 8;
        }
    }
}
