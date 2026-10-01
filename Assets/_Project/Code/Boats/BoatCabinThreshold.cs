using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>THE DOORWAY, AS A PLACE YOU WALK THROUGH</b> — the pure geometry of a cabin threshold, so that
    /// "she has stepped through the door" is one measurement asked in one frame rather than two walkers'
    /// private opinions about where a door is.
    ///
    /// <para><b>Why a band and not a line.</b> The owner's 2026-08-28 ruling is that an open door "a
    /// player can walk through freely", and a walker crossing a mathematical LINE is a walker who has to
    /// hit a doorway to the millimetre on a deck that is rolling under her — the least cozy thing a boat
    /// can do (P5). The band is the doorway's own <see cref="BoatInteriorDoor.ClearWidthMeters"/>, which
    /// is the hole the kit actually measured: half of it either side of the threshold point is exactly
    /// "standing in the doorway", and it is DATA rather than a feel knob (rule 6). A door whose sidecar
    /// omits the width has no band at all, which is honest — you cannot walk through an opening nobody
    /// measured — and <see cref="BoatCabinDoor"/> keeps the press-through for her.</para>
    ///
    /// <para><b>⭐ …and the WALL LINE, because the band is a disc and a wall is not.</b> The threshold
    /// point is the sill's outer lip: on every measured hull it stands 0.07 to 0.26 m outside the edge
    /// of the room it opens (<see cref="TryWallLine"/>). The sole walk cannot leave its room and the
    /// deck walk may not enter it, so the one place both of them can stand is that edge — and a
    /// crossing taken anywhere else in the band would have the other walker's clamp drag her across the
    /// difference in one frame. So she crosses ON the line: going out, once she has reached it from
    /// inside; going in, once she has reached it from out on deck (<see cref="HasReachedTheWallLine"/>).
    /// The line's outward normal is also the doorway's AXIS — which way "through" is — measured off the
    /// room's own outline rather than trusted to <see cref="BoatInteriorDoor.Side"/>'s label.</para>
    ///
    /// <para><b>⭐ The key decides, owner ruling D1 (a), 2026-09-30.</b> The old hysteresis (a whole
    /// clear width of daylight before the doorway would take her again) is gone: a player holding one
    /// key met a doorway that would not have her until she had walked away from it and back. In the band,
    /// on the line, with the key pointing through (<see cref="IsHeldThrough"/>), she goes; within one
    /// clear width, a key held within <see cref="BoatInteriorDoor.PullConeDegrees"/> of the axis is
    /// carried onto the opening (<see cref="Steer"/>). What keeps a rocked key from strobing the cabin
    /// is now the door's settle (<see cref="BoatInteriorDoor.CrossingSettleSeconds"/>), which a player
    /// walking back through on purpose never meets.</para>
    ///
    /// <para><b>⭐ Hull-local, and therefore frame-free.</b> Every quantity here is in the rig's own
    /// metres (+x starboard, +y bow) — the frame <see cref="HullLocalAnchor"/>'s own tooltip names as the
    /// one "every authored deck polygon, fitting pivot and door threshold already speaks". So the test is
    /// the same on the sole and on the deck, at every heading, with no projection, no bake elevation and
    /// no handedness in it. That matters more than it looks: the cabin walk folds the hull's measured
    /// handedness into its projection and the deck walk assumes the counter-clockwise convention, so a
    /// threshold test done in WORLD offsets would have to pick one of them and would mirror the doorway
    /// end for end on the hulls that disagree. The held key is asked in the same frame, turned there by
    /// each walker's own projection.</para>
    ///
    /// <para>Pure, static, allocation-free and deterministic — <see cref="BoatCabinWalkMath"/>'s
    /// discipline, for its reason: the rule about where a player may walk is worth asserting without a
    /// scene.</para>
    /// </summary>
    public static class BoatCabinThreshold
    {
        /// <summary>
        /// <b>How near the wall line counts as standing ON it</b>, metres. Not a feel number: the two
        /// walkers stand her within <see cref="BoatCabinWalkMath.ClearEpsilonMetres"/> of an edge, each on
        /// its own side of it, and a centimetre admits that and a float's error on the fleet's longest
        /// hull (fifty metres from her origin) with nothing a player could see.
        /// </summary>
        public const float OnTheWallLineMetres = 0.01f;

        /// <summary>A held key shorter than this is no key at all — the walkers' own dead zone
        /// (<see cref="BoatCabinWalkMath.Step"/> steps nobody under a 1e-4 input).</summary>
        private const float HeldDeadZoneSqr = 1e-8f;

        /// <summary>Where the doorway is, in the hull's own metres — the threshold point with its sill
        /// HEIGHT dropped, because a band is a place on a floor and the height is what says which floor
        /// (<see cref="BoatInterior.LevelIndexAtHeight"/> asks that question, and it is the only thing
        /// that should).</summary>
        public static Vector2 PointOf(BoatInteriorDoor door)
            => door == null ? Vector2.zero : new Vector2(door.ThresholdPoint.x, door.ThresholdPoint.y);

        /// <summary>
        /// <b>How near the threshold is "in the doorway"</b>, metres — half the clear width the kit
        /// measured. Zero for a door with no measured opening, and every caller reads that as "this
        /// threshold cannot be walked" rather than as a small band.
        /// </summary>
        public static float BandRadiusMetres(BoatInteriorDoor door)
            => door == null ? 0f : Mathf.Max(0f, door.ClearWidthMeters) * 0.5f;

        /// <summary>True when this door has an opening wide enough to walk through at all. False is DATA:
        /// a sidecar that never measured the leaf has not described a doorway, and a zero-width band
        /// would silently make the threshold unwalkable rather than saying so.</summary>
        public static bool HasBand(BoatInteriorDoor door) => BandRadiusMetres(door) > 0f;

        /// <summary>Is <paramref name="hullLocal"/> standing in this doorway?</summary>
        public static bool IsInBand(BoatInteriorDoor door, Vector2 hullLocal)
        {
            float radius = BandRadiusMetres(door);
            if (radius <= 0f) return false;
            return (hullLocal - PointOf(door)).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// <b>Is the floor she is standing on this doorway's floor?</b> Her floor height against the sill's
        /// (<see cref="BoatInteriorDoor.ThresholdPoint"/>'s z), within <paramref name="toleranceMetres"/>
        /// — the def's <see cref="BoatInteriorDef.FloorTolerance"/>, the same bar the importer placed her
        /// routes by. The band is a place in PLAN and this is the other half of "standing in the
        /// doorway": on the sport fishers a deck 2.9 m above the sill and a cockpit 0.5 m below it both
        /// lie inside the band (see <see cref="ICabinThresholdAtHeight"/>). False for no door.
        /// </summary>
        public static bool IsOnTheSill(BoatInteriorDoor door, float floorZMetres, float toleranceMetres)
            => door != null && Mathf.Abs(floorZMetres - door.ThresholdPoint.z) <= Mathf.Max(0f, toleranceMetres);

        // ---- the wall the doorway stands in -----------------------------------------------------------

        /// <summary>
        /// <b>The wall this doorway is cut in</b>: of every level at the sill's height (within the def's
        /// <see cref="BoatInteriorDef.FloorTolerance"/>), the outline edge nearest the threshold point.
        /// <paramref name="levelIndex"/> is that level — the ROOM the door opens; <paramref name="onLine"/>
        /// the edge's nearest point to the threshold; <paramref name="outward"/> its unit normal pointing
        /// out of the room, which is the doorway's axis.
        ///
        /// <para><b>Nearest EDGE, not first level.</b> Five hulls list a main deck at the sill's height
        /// before the house sole (the coastal packet, the side dragger, both stern trawlers, the tanker);
        /// their thresholds stand three to five metres inside that deck's outline and 0.07 m outside the
        /// house's. The edge a threshold is nearest is the wall it is in.</para>
        ///
        /// <para>False when no usable level stands at the sill's height: then there is no wall to walk
        /// through, and <see cref="BoatCabinDoor"/> keeps the press-through for her, as it does for a
        /// door with no measured width.</para>
        /// </summary>
        public static bool TryWallLine(BoatInteriorDef def, BoatInteriorDoor door, out int levelIndex,
                                       out Vector2 onLine, out Vector2 outward)
        {
            levelIndex = -1;
            onLine = Vector2.zero;
            outward = Vector2.zero;
            if (def == null || door == null || def.Levels == null) return false;

            Vector2 threshold = PointOf(door);
            float tolerance = def.FloorTolerance;
            float best = float.PositiveInfinity;
            for (int i = 0; i < def.Levels.Length; i++)
            {
                BoatInteriorLevel level = def.Levels[i];
                if (level == null || !level.IsUsable()) continue;
                if (Mathf.Abs(level.SoleZMeters - door.ThresholdPoint.z) > tolerance) continue;

                Vector2[] outline = level.Outline;
                float winding = SignedArea(outline);
                if (Mathf.Abs(winding) <= 1e-12f) continue;
                for (int e = 0; e < outline.Length; e++)
                {
                    Vector2 a = outline[e];
                    Vector2 ab = outline[(e + 1) % outline.Length] - a;
                    float length = ab.sqrMagnitude;
                    if (length <= 1e-12f) continue;
                    Vector2 q = a + ab * Mathf.Clamp01(Vector2.Dot(threshold - a, ab) / length);
                    float sqr = (threshold - q).sqrMagnitude;
                    if (sqr >= best) continue;

                    best = sqr;
                    levelIndex = i;
                    onLine = q;
                    // An anticlockwise outline has its room on the LEFT of each edge, so its right-hand
                    // normal points out of it; a clockwise one the other way round.
                    Vector2 right = new Vector2(ab.y, -ab.x).normalized;
                    outward = winding > 0f ? right : -right;
                }
            }
            return levelIndex >= 0;
        }

        /// <summary>
        /// <b>Has she reached the wall line from her side of it?</b> Going out (she is in the room) she
        /// must stand on the line or past it; going in (she is on deck), on it or past it the other way —
        /// each within <see cref="OnTheWallLineMetres"/>. See the class remarks: the line is the one place
        /// both walkers can stand, so a crossing there moves her by no more than their own clamps do.
        /// </summary>
        public static bool HasReachedTheWallLine(Vector2 onLine, Vector2 outward, bool goingIn,
                                                 Vector2 hullLocal)
        {
            float pastTheLine = PastTheWallLine(onLine, outward, hullLocal);
            return goingIn ? pastTheLine <= OnTheWallLineMetres : pastTheLine >= -OnTheWallLineMetres;
        }

        /// <summary>How far out of the room <paramref name="hullLocal"/> stands, metres along the axis:
        /// negative inside it, zero on the wall line.</summary>
        public static float PastTheWallLine(Vector2 onLine, Vector2 outward, Vector2 hullLocal)
            => Vector2.Dot(hullLocal - onLine, outward);

        /// <summary>
        /// <b>How little a tick's progress toward the wall line counts as none</b>, metres. Not a feel
        /// number: a walker stopped by her floor's edge is stood at the same point tick after tick, to a
        /// float's error, and the slowest walk a gamepad's dead zone lets through still covers ten times
        /// this in a 240 Hz tick.
        /// </summary>
        public const float PressedShortMetres = 1e-4f;

        /// <summary>
        /// <b>Is she pressed against her floor's edge short of the wall line?</b> — her last tick took
        /// her no nearer it (<paramref name="pastBefore"/> and <paramref name="pastNow"/> are
        /// <see cref="PastTheWallLine"/> then and now). On five of the fleet's hulls the deck ends at the
        /// sill's outer lip, 0.07 m short of the room (the coastal packet, the lobster boat, the side
        /// dragger, the tanker's poop deck and a stern trawler's): she can stand in the doorway with the
        /// key pointing through and never reach the line. Standing as near it as her floor lets her is
        /// reaching it, and the room's own clamp takes her the rest of the sill. NaN before is no record.
        /// </summary>
        public static bool IsPressedShort(float pastBefore, float pastNow, bool goingIn)
        {
            if (float.IsNaN(pastBefore)) return false;
            return goingIn ? pastNow >= pastBefore - PressedShortMetres
                           : pastNow <= pastBefore + PressedShortMetres;
        }

        /// <summary>
        /// <b>Is her key pointing through the doorway?</b> Out along the axis when she is in the room,
        /// in along it when she is on deck, within the door's
        /// <see cref="BoatInteriorDoor.CrossingConeDegrees"/>. No key, no crossing: a player standing in a
        /// doorway is not walking through it.
        /// </summary>
        public static bool IsHeldThrough(BoatInteriorDoor door, Vector2 outward, bool goingIn,
                                         Vector2 heldHullLocal)
        {
            if (door == null || heldHullLocal.sqrMagnitude <= HeldDeadZoneSqr) return false;
            if (outward.sqrMagnitude <= 1e-12f) return false;
            return Vector2.Angle(heldHullLocal, goingIn ? -outward : outward) <= door.CrossingCone;
        }

        // ---- D1 (a): the pull ------------------------------------------------------------------------------

        /// <summary>How near the threshold the pull reaches, metres: the door's
        /// <see cref="BoatInteriorDoor.PullReachClearWidths"/> of its own clear width.</summary>
        public static float PullReachMetres(BoatInteriorDoor door)
            => door == null ? 0f : Mathf.Max(0f, door.ClearWidthMeters) * door.PullReach;

        /// <summary>
        /// <b>Is this key, here, one the doorway carries?</b> Within the pull's reach of the threshold,
        /// and within <see cref="BoatInteriorDoor.PullConeDegrees"/> of the way through (out of the room
        /// when <paramref name="goingIn"/> is false, into it when true).
        /// </summary>
        public static bool IsInThePull(BoatInteriorDoor door, Vector2 outward, bool goingIn,
                                       Vector2 hullLocal, Vector2 heldHullLocal)
        {
            if (door == null || heldHullLocal.sqrMagnitude <= HeldDeadZoneSqr) return false;
            if (outward.sqrMagnitude <= 1e-12f) return false;
            float reach = PullReachMetres(door);
            if (reach <= 0f || (hullLocal - PointOf(door)).sqrMagnitude > reach * reach) return false;
            return Vector2.Angle(heldHullLocal, goingIn ? -outward : outward) <= door.PullCone;
        }

        /// <summary>Is this key within <see cref="BoatInteriorDoor.PullConeDegrees"/> of the doorway's axis,
        /// either way along it — wherever she stands?</summary>
        public static bool IsLeaningThrough(BoatInteriorDoor door, Vector2 outward, Vector2 heldHullLocal)
        {
            if (door == null || heldHullLocal.sqrMagnitude <= HeldDeadZoneSqr) return false;
            if (outward.sqrMagnitude <= 1e-12f) return false;
            float off = Vector2.Angle(heldHullLocal, outward);
            return Mathf.Min(off, 180f - off) <= door.PullCone;
        }

        /// <summary>How near a piece of furniture counts as standing against it, metres. Not a feel number:
        /// the walkers stop her <see cref="BoatCabinWalkMath.ClearEpsilonMetres"/> off a face, and a
        /// centimetre admits that with room for a float's error.</summary>
        public const float AgainstFurnitureMetres = 0.01f;

        /// <summary>
        /// <b>D1 (a)'s <i>"round the helm seat"</i>, to its far corner.</b> Is she standing against a piece
        /// of furniture the pull reaches — a blocking footprint on <paramref name="room"/> that comes within
        /// <see cref="PullReachMetres"/> of the threshold? The reach is where the doorway starts carrying
        /// her; the furniture just inside it is carried round whole. On the Newfoundland inshore hardtop one
        /// clear width (0.6 m) ends 7 mm short of the helm seat's corner, and a carry that stopped at the
        /// reach left her pressed square on the seat's face.
        /// </summary>
        public static bool IsAgainstFurnitureJustInside(BoatInteriorDoor door, BoatInteriorLevel room,
                                                        Vector2 hullLocal)
        {
            float reach = PullReachMetres(door);
            if (reach <= 0f || room == null || room.Obstructions == null) return false;
            Vector2 threshold = PointOf(door);
            float against = AgainstFurnitureMetres * AgainstFurnitureMetres;
            for (int i = 0; i < room.Obstructions.Length; i++)
            {
                BoatInteriorObstruction furniture = room.Obstructions[i];
                if (!BoatCabinWalkMath.Blocks(furniture)) continue;
                DeckAreaMath.ClosestPointOnOutline(furniture.Footprint, hullLocal, out float sqr);
                if (sqr > against) continue;
                DeckAreaMath.ClosestPointOnOutline(furniture.Footprint, threshold, out float fromDoor);
                if (fromDoor <= reach * reach) return true;
            }
            return false;
        }

        /// <summary>
        /// <b>D1 (a), owner 2026-09-30: <i>"within one clear width, a key held within 30° of the
        /// doorway's axis carries her through the opening."</i></b> The direction a walker should step
        /// her in: for a key <see cref="IsInThePull"/>, toward the opening's centre on its wall line
        /// (<paramref name="onLine"/>, <see cref="TryWallLine"/>) — the one point both walkers can stand on,
        /// and inside the band from wherever the pull reaches — at the key's own length; for every other
        /// key, the key untouched (<i>"farther off, nothing is bent"</i>). Aimed any farther through, the
        /// line from a lane beside the cape's helm seat met the wall 0.49 m off the opening's centre, outside
        /// the band, and she crawled along the bulkhead to it.
        /// </summary>
        public static Vector2 Steer(BoatInteriorDoor door, Vector2 onLine, Vector2 outward, bool goingIn,
                                    Vector2 hullLocal, Vector2 heldHullLocal)
        {
            if (!IsInThePull(door, outward, goingIn, hullLocal, heldHullLocal)) return heldHullLocal;
            Vector2 toward = onLine - hullLocal;
            return toward.sqrMagnitude <= 1e-12f ? heldHullLocal : toward.normalized * heldHullLocal.magnitude;
        }

        /// <summary>Twice the signed area of <paramref name="polygon"/>: positive anticlockwise.</summary>
        private static float SignedArea(Vector2[] polygon)
        {
            float sum = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                sum += a.x * b.y - b.x * a.y;
            }
            return sum;
        }
    }
}
