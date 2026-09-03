using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐ <b>Which way is OUTBOARD?</b> The pure geometry behind the owner's two-press exit (2026-09-02):
    /// <i>"one button to get on the washboard and then it depends which way you face when you place the
    /// next button, either in the boat or in the water if facing each."</i>
    ///
    /// <para><b>Everything here is in the HULL's own frame</b> — x abeam to starboard, y along the keel
    /// toward the bow — and that is the point. "Outboard" is a fact about the boat, not about the
    /// screen: the rail is where it is whichever way she is pointing. The caller turns the rider's
    /// COMPASS facing into a deck bearing with <see cref="DeckRiderFacingMath.DeckBearingFor"/> — the
    /// seam that already exists and already agrees about which way north is — and everything below is
    /// then a dot product between two directions in one frame. No projection, no ¾ squash: applying the
    /// artwork's foreshortening to a direction would change the ANGLE between the facing and the rail,
    /// and that angle is the whole decision (ADR 0042 — the squash is for drawing).</para>
    ///
    /// <para><b>⚠ The tie rule is a safety rule, not a rounding convention.</b> A rider facing exactly
    /// ALONG the rail is not trying to leave the boat, and the sea is 4 m deep under this berth. Ties —
    /// and anything within <see cref="TieEpsilon"/> of one — resolve INBOARD. Nobody goes over the side
    /// by accident; going in is a thing you have to mean.</para>
    /// </summary>
    public static class OverTheSideMath
    {
        /// <summary>
        /// How square-on to the rail a facing must be before it counts as leaving the boat. Not a float
        /// fudge: at exactly 90° to the outward normal the rider is walking the rail, and the dot product
        /// there is 0 in exact arithmetic and ±1e-8 in practice. A hair of dead band each side turns
        /// "the arithmetic wobbled" into "she was walking along the gunwale", which is the honest reading.
        /// </summary>
        public const float TieEpsilon = 1e-3f;

        /// <summary>
        /// The rider's facing as a DECK-FRAME unit vector, from the deck bearing
        /// <see cref="DeckRiderFacingMath.DeckBearingFor"/> hands back: 0° looks at the bow, +90° to
        /// starboard — the compass convention applied in the deck frame, so bow is +y and starboard +x.
        /// </summary>
        public static Vector2 FacingFromDeckBearing(float deckBearingDegrees)
        {
            if (float.IsNaN(deckBearingDegrees) || float.IsInfinity(deckBearingDegrees))
                return new Vector2(0f, 1f);                       // a broken bearing looks at the bow
            float rad = deckBearingDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        /// <summary>
        /// ⭐ <b>Is this facing taking her over the side?</b> The one decision the second press makes.
        /// Ties and near-ties are INBOARD — see the class note.
        /// </summary>
        public static bool GoesOverTheSide(Vector2 facingDeckFrame, Vector2 outwardNormal)
            => Vector2.Dot(Safe(facingDeckFrame), Safe(outwardNormal)) > TieEpsilon;

        // ---- the outward normal, for the two shapes a deck comes in ----------------------------------

        /// <summary>
        /// The outward normal of the walkable BOX at the point nearest <paramref name="deckPoint"/> — the
        /// answer for a hull with no authored deck polygons, whose walkable area is the fallback
        /// rectangle.
        ///
        /// <para><b>Corners sum.</b> Standing on the quarter, both the side and the transom are "the
        /// rail", and the honest outward direction is the diagonal between them — not whichever edge won
        /// a floating-point coin toss. Any edge within <see cref="TieEpsilon"/> of the nearest is
        /// included and the normals are summed.</para>
        /// </summary>
        public static Vector2 OutwardNormalOnBox(Vector2 center, Vector2 halfExtents, Vector2 deckPoint)
        {
            Vector2 c = Safe(center), p = Safe(deckPoint);
            float hx = Mathf.Max(0f, Safe(halfExtents).x), hy = Mathf.Max(0f, Safe(halfExtents).y);
            Vector2 d = p - c;

            // Distance from the point to each of the four edges, measured outward-positive.
            float toStarboard = hx - d.x, toPort = hx + d.x;      // +x and −x faces
            float toBow = hy - d.y, toStern = hy + d.y;           // +y and −y faces
            float nearest = Mathf.Min(Mathf.Min(toStarboard, toPort), Mathf.Min(toBow, toStern));

            Vector2 n = Vector2.zero;
            if (toStarboard <= nearest + TieEpsilon) n += new Vector2(1f, 0f);
            if (toPort      <= nearest + TieEpsilon) n += new Vector2(-1f, 0f);
            if (toBow       <= nearest + TieEpsilon) n += new Vector2(0f, 1f);
            if (toStern     <= nearest + TieEpsilon) n += new Vector2(0f, -1f);

            // A degenerate box (zero extent both ways) makes every face "nearest" and the sum cancels;
            // there is no outboard on a hull with no width, so say so rather than returning a NaN.
            return n.sqrMagnitude > 1e-8f ? n.normalized : Vector2.zero;
        }


        private static float Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        private static Vector2 Safe(Vector2 v) => new Vector2(Safe(v.x), Safe(v.y));
    }
}
