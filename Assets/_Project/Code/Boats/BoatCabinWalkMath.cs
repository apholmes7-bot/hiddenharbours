using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>THE CABIN SOLE, AS SOMETHING YOU CAN WALK ABOUT ON</b> — the pure geometry of standing inside a
    /// measured boat interior while the hull she is inside is under way.
    ///
    /// <para><b>Why this exists beside <see cref="DeckAreaMath"/> rather than inside it.</b> A deck and a
    /// cabin sole are the same KIND of thing — a hull-local polygon you stand on, projected onto a hull
    /// the ¾ camera draws — and every line of the transform half is <see cref="DeckAreaMath"/>'s,
    /// unchanged and called rather than copied. What differs is the DATA: a deck is a
    /// <see cref="BoatDeckDef"/> of <c>DeckArea</c>s with fitted height planes, and a cabin level is a
    /// <see cref="BoatInteriorLevel"/> — one flat sole at one <see cref="BoatInteriorLevel.SoleZMeters"/>,
    /// with its furniture listed BESIDE it rather than cut out of it. Those two shapes cannot share a
    /// clamp without one of them pretending to be the other.</para>
    ///
    /// <para><b>⭐ Obstructions BLOCK — both kinds of them, and the def says so in as many words.</b>
    /// <see cref="BoatInteriorObstruction.Treatment"/> is the sidecar's own word: <c>wall</c> ("blocks and
    /// hides") and <c>waist_block</c> ("blocks, seen over"). The difference between them is what you can
    /// SEE over, which is a drawing question; to a walker they are both furniture, and neither is a hole
    /// in the floor. The def's own remark — <i>"carried verbatim — the runtime rules on it, not this"</i>
    /// — is this class ruling on it: an unrecognised treatment blocks too, because a thing the art bothered
    /// to measure and name is a thing that is there.</para>
    ///
    /// <para><b>The clamp slides rather than stopping.</b> Off the sole you are pulled to its nearest edge
    /// (so walking into her side deck-house wall carries you along it); into a locker you are pushed to
    /// the nearest point of ITS outline (so you round the helm console instead of sticking to it). A point
    /// that is still blocked after <see cref="ClearPasses"/> of that — a walker wedged between two pieces
    /// of furniture that touch — keeps the position it came in with, which is what a collider does and the
    /// only answer that cannot put somebody inside a bunk.</para>
    ///
    /// <para><b>⚠ Handedness is a PARAMETER, never an assumption.</b> <see cref="DeckAreaMath"/> bakes the
    /// counter-clockwise convention into its heading argument (it is
    /// <c>MountedRockPoseMath.Project(local, −heading, …)</c>); <see cref="HullLocalAnchor"/> — which is
    /// what puts this cabin's DOOR on screen — picks the sign from the hull's MEASURED
    /// <see cref="HullMeshDef.AzimuthCounterClockwise"/>. If those two disagreed, the player would walk in
    /// a cabin mirrored end for end against the doorway she is trying to reach. So the sign is folded in
    /// once, here, by <see cref="TurntableHeading"/>, and every entry point takes it.</para>
    ///
    /// <para>Pure, static, allocation-free and deterministic — the same discipline as
    /// <see cref="DeckAreaMath"/>, and for the same reason: the rule about where a player may stand is
    /// worth being able to assert without a scene.</para>
    /// </summary>
    public static class BoatCabinWalkMath
    {
        /// <summary>How many times a blocked point is pushed clear before the step is refused outright.
        /// Four, matching <c>DeckWalkController</c>'s own seeding passes: a walker pushed out of one
        /// locker can land in the one beside it, and two adjacent pieces of furniture is the deepest
        /// arrangement any hull in this kit measures.</summary>
        public const int ClearPasses = 4;

        /// <summary>How far past an obstruction's edge a cleared point is put, metres. Small enough to be
        /// invisible and large enough that the crossing test on the next tick cannot report the walker as
        /// still inside the thing they were just pushed out of.</summary>
        private const float ClearEpsilonMetres = 0.001f;

        // ---- the frame -------------------------------------------------------------------------------

        /// <summary>
        /// The heading to hand <see cref="DeckAreaMath"/> so that its projection is the one this hull's
        /// art is actually drawn by. <see cref="DeckAreaMath"/> assumes the counter-clockwise convention;
        /// a clockwise-baked hull is the same transform with the turntable run the other way, which is one
        /// negation and not a second projection.
        /// </summary>
        public static float TurntableHeading(float drawnHeadingDegrees, bool azimuthCounterClockwise)
            => azimuthCounterClockwise ? drawnHeadingDegrees : -drawnHeadingDegrees;

        /// <summary>A point on the sole, as a boat-relative WORLD (screen-axis) offset from the hull's
        /// pivot — the same transform, through the same helper, that puts the cabin DOOR where it is.
        /// </summary>
        public static Vector2 ToWorldOffset(Vector2 cabinLocal, float soleZMeters,
                                            float drawnHeadingDegrees, float bakeElevationDegrees,
                                            bool azimuthCounterClockwise)
            => DeckAreaMath.DeckToWorld(cabinLocal, soleZMeters,
                                        TurntableHeading(drawnHeadingDegrees, azimuthCounterClockwise),
                                        bakeElevationDegrees);

        /// <summary>The exact inverse of <see cref="ToWorldOffset"/> — a boat-relative world offset read
        /// back as a point on the sole. The sole's own height is supplied because the projection folds
        /// along-hull distance and height onto one screen axis; a cabin level is FLAT, so unlike the deck's
        /// sheer-following foredeck this inverse is exact on the first pass and needs no iteration.
        /// </summary>
        public static Vector2 FromWorldOffset(Vector2 worldOffset, float soleZMeters,
                                              float drawnHeadingDegrees, float bakeElevationDegrees,
                                              bool azimuthCounterClockwise)
            => DeckAreaMath.WorldToDeck(worldOffset, soleZMeters,
                                        TurntableHeading(drawnHeadingDegrees, azimuthCounterClockwise),
                                        bakeElevationDegrees);

        // ---- what blocks -----------------------------------------------------------------------------

        /// <summary>
        /// Does this obstruction stop a walker? Every measured one does — see the class remarks. Null, and
        /// a footprint with fewer than three points, do not: a measurement that did not finish is not a
        /// wall, and treating it as one would put invisible furniture in the middle of a room.
        /// </summary>
        public static bool Blocks(BoatInteriorObstruction obstruction)
            => obstruction != null && obstruction.Footprint != null && obstruction.Footprint.Length >= 3;

        /// <summary>True when <paramref name="cabinLocal"/> is somewhere a walker may actually stand on
        /// this level: inside the sole, and inside none of its furniture.</summary>
        public static bool IsStandable(BoatInteriorLevel level, Vector2 cabinLocal)
        {
            if (level == null || !level.IsUsable()) return false;
            if (!DeckAreaMath.Contains(level.Outline, cabinLocal)) return false;
            return IndexOfBlockingObstruction(level, cabinLocal) < 0;
        }

        /// <summary>The first obstruction on this level that contains the point, or −1. FIRST in the def's
        /// own order rather than nearest, so the answer is a function of the data and not of iteration
        /// luck (the determinism rule, one level down).</summary>
        private static int IndexOfBlockingObstruction(BoatInteriorLevel level, Vector2 cabinLocal)
        {
            BoatInteriorObstruction[] furniture = level.Obstructions;
            if (furniture == null) return -1;
            for (int i = 0; i < furniture.Length; i++)
            {
                if (!Blocks(furniture[i])) continue;
                if (DeckAreaMath.Contains(furniture[i].Footprint, cabinLocal)) return i;
            }
            return -1;
        }

        // ---- the clamp -------------------------------------------------------------------------------

        /// <summary>
        /// Put <paramref name="wanted"/> somewhere a walker may stand, sliding rather than stopping where
        /// it can. <paramref name="fallback"/> is the position to keep when it cannot — the walker's own
        /// previous point, which is standable by induction because this method is the only thing that
        /// produces one.
        /// </summary>
        public static Vector2 ClampToSole(BoatInteriorLevel level, Vector2 wanted, Vector2 fallback)
        {
            if (level == null || !level.IsUsable()) return wanted;

            Vector2 p = PullOntoTheSole(level, wanted);

            for (int pass = 0; pass < ClearPasses; pass++)
            {
                int blocking = IndexOfBlockingObstruction(level, p);
                if (blocking < 0) return p;
                p = PullOntoTheSole(level, PushClearOf(level.Obstructions[blocking].Footprint, p));
            }

            // Still wedged. Keep what we had: a walker who cannot be placed is a walker who does not move,
            // and there is no arrangement of furniture in which that is worse than standing in a locker.
            return IsStandable(level, fallback) ? fallback : p;
        }

        /// <summary>Inside the sole already, or the nearest point of its outline.</summary>
        private static Vector2 PullOntoTheSole(BoatInteriorLevel level, Vector2 p)
            => DeckAreaMath.Contains(level.Outline, p)
                   ? p
                   : DeckAreaMath.ClosestPointOnOutline(level.Outline, p, out _);

        /// <summary>Out of a footprint by the nearest edge, plus a hair — so the crossing test on the next
        /// tick cannot report the walker as still inside the thing they were just pushed out of.</summary>
        private static Vector2 PushClearOf(Vector2[] footprint, Vector2 inside)
        {
            Vector2 edge = DeckAreaMath.ClosestPointOnOutline(footprint, inside, out float sqr);
            if (sqr <= 1e-12f) return edge;   // already on the boundary: nothing to point away along
            return edge + (edge - inside).normalized * ClearEpsilonMetres;
        }

        // ---- the step --------------------------------------------------------------------------------

        /// <summary>
        /// One step about the cabin, in the sole's own metres.
        ///
        /// <para>The screen-axis <paramref name="moveInput"/> becomes the sole direction that DRAWS along
        /// it (press up-screen, walk up-screen, at every heading — <c>DeckWalkController</c>'s rule and
        /// <see cref="DeckAreaMath.WorldDirectionToDeck"/>'s reason), the step is
        /// <paramref name="speedMetresPerSecond"/> metres of SOLE per second, and the result is clamped
        /// onto somewhere she may stand.</para>
        ///
        /// <para><b>Clamps with no input too</b>, and that is not a formality: the hull yaws under her the
        /// whole passage, and a point that was on the sole is still on the sole — but a level swapped under
        /// her, or a seed taken from a world position that was never inside, would leave her outside it.
        /// </para>
        /// </summary>
        public static Vector2 Step(BoatInteriorLevel level, Vector2 cabinLocal, Vector2 moveInput,
                                   float speedMetresPerSecond, float deltaSeconds,
                                   float drawnHeadingDegrees, float bakeElevationDegrees,
                                   bool azimuthCounterClockwise)
        {
            Vector2 wanted = cabinLocal;
            float magnitude = Mathf.Min(1f, moveInput.magnitude);
            if (magnitude > 1e-4f)
            {
                Vector2 dir = DeckAreaMath.WorldDirectionToDeck(
                    moveInput, TurntableHeading(drawnHeadingDegrees, azimuthCounterClockwise),
                    bakeElevationDegrees);
                if (dir.sqrMagnitude > 1e-10f)
                    wanted += dir.normalized *
                              (magnitude * Mathf.Max(0f, speedMetresPerSecond) * Mathf.Max(0f, deltaSeconds));
            }
            return ClampToSole(level, wanted, cabinLocal);
        }

        // ---- where you come in -----------------------------------------------------------------------

        /// <summary>
        /// <b>Where a walker stands the moment she is inside</b> — just in from
        /// <paramref name="door"/>'s threshold, on <paramref name="level"/>.
        ///
        /// <para>The threshold itself is the DOORWAY, and a doorway is a hole in a wall: it is on the
        /// sole's edge by construction, and a walker placed exactly on an edge is a walker one clamp away
        /// from being somewhere else. So the start is the threshold pulled onto the sole by the ordinary
        /// clamp — the same call every subsequent tick makes, which is what makes the first frame and the
        /// second frame agree.</para>
        ///
        /// <para>A hull with no door starts at the sole's own centroid, which is the only point a polygon
        /// can nominate without being asked about a doorway.</para>
        /// </summary>
        public static Vector2 StartPointFor(BoatInteriorLevel level, BoatInteriorDoor door)
        {
            if (level == null || !level.IsUsable()) return Vector2.zero;

            Vector2 seed = door != null
                ? new Vector2(door.ThresholdPoint.x, door.ThresholdPoint.y)
                : CentroidOf(level.Outline);

            // ⚠ The fallback is the CENTROID and not the seed: ClampToSole's fallback is "the point you
            // came in with", and on this one call there is no previous point to come in with. A convex
            // sole's centroid is inside it; a concave one's may not be, and then the clamp's own pull is
            // the answer — which is why it is passed through the clamp rather than used raw.
            Vector2 centre = CentroidOf(level.Outline);
            return ClampToSole(level, seed, IsStandable(level, centre) ? centre : seed);
        }

        /// <summary>The mean of a polygon's vertices. Not the area centroid — this is a seed for a clamp,
        /// not a physical property, and the vertex mean is the one that cannot divide by a zero area.
        /// </summary>
        public static Vector2 CentroidOf(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < polygon.Length; i++) sum += polygon[i];
            return sum / polygon.Length;
        }
    }
}
