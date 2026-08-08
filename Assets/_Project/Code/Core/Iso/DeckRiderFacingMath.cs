using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>WHICH WAY A RIDER ON A DECK IS LOOKING</b> — the answer that has to be composed rather than
    /// measured, because a boat is a moving frame of reference.
    ///
    /// <para><b>The defect this exists to close (owner playtest 2026-08-07):</b> <i>"the sprite doesn't
    /// follow the orientation of the boat, they slowly lose reference and spin horizontally."</i> A
    /// character's facing is normally read straight off its own motion
    /// (<see cref="IsoCharacterMath.HeadingFor"/>), and on deck that read is measured in the boat's frame
    /// — which is right for TRANSLATION (a drifting hull must not make the fisher stride on the spot) and
    /// wrong for ROTATION. A hull turning under a motionless fisher moves them in that frame with no step
    /// taken, so the measured "velocity" is an artefact of the turn: it points across the turn, its
    /// direction sweeps round as the hull comes about, and the facing chases it. The figure ends up
    /// spinning slowly on the spot, having lost any fixed relationship to the deck under their feet.</para>
    ///
    /// <para><b>The rule.</b> A rider's facing is TWO facts, not one: where they are looking <b>relative to
    /// the deck</b>, and where the deck is <b>pointing</b>. The first is a deck-frame bearing that only
    /// their own walking changes; the second is the hull's drawn heading. Compose them every frame and both
    /// desired behaviours fall out with nothing to accumulate:</para>
    /// <list type="bullet">
    ///   <item>A fisher standing still on a turning boat <b>turns with her</b> — their deck bearing is
    ///   unchanged, so the composition follows the hull exactly.</item>
    ///   <item>A fisher walking the deck faces the way they are walking, whatever the hull is doing.</item>
    /// </list>
    ///
    /// <para><b>Derived, never integrated.</b> Both terms are re-read from the authority every frame — the
    /// bearing from the deck-frame step just taken, the heading from the presenter — and the result is a
    /// pure function of them. There is no per-frame delta anywhere in it, which is what makes drift
    /// structurally impossible rather than merely small. (The same lesson as the flick-cast release
    /// position: never compute one quantity two ways, and never accumulate what you can derive.)</para>
    ///
    /// <para><b>The deck frame</b> is <c>DeckWalkController</c>'s: <c>x</c> abeam to STARBOARD, <c>y</c>
    /// along the keel toward the BOW, metres of real deck. It is heading-independent by construction — the
    /// walkable polygons live in it — which is exactly the property that makes a bearing measured in it
    /// immune to the hull's turn.</para>
    ///
    /// <para><b>Rules.</b> Pure, static, allocation-free and deterministic, like <see cref="DeckRideMath"/>
    /// beside it. Visual-only (rule 5): nothing here feeds the sim.</para>
    /// </summary>
    public static class DeckRiderFacingMath
    {
        /// <summary>
        /// The bearing a rider holds <b>relative to the deck</b> (degrees; 0 = looking at the bow, +90 =
        /// looking to starboard — the compass convention applied in the deck frame), given the deck-frame
        /// velocity they are walking at.
        ///
        /// <para>Below <paramref name="minSpeed"/> the bearing is HELD, not zeroed: a fisher who stops
        /// keeps looking where they were going. That is <see cref="IsoCharacterMath.HeadingFor"/>'s own
        /// rule, and this deliberately CALLS it rather than restating the <c>atan2</c> — the deck frame and
        /// the world frame share one bearing convention, and two copies of it would be two chances to
        /// disagree about which way north is.</para>
        /// </summary>
        /// <param name="deckVelocity">Deck-frame velocity (x abeam to starboard, y toward the bow), m/s.</param>
        /// <param name="minSpeed">Below this the step is treated as noise and <paramref name="heldBearing"/>
        /// is kept. Deliberately the caller's number: it is the same "is this a real step?" question the
        /// on-foot presenter already answers.</param>
        /// <param name="heldBearing">The bearing to keep while stopped.</param>
        public static float DeckBearing(Vector2 deckVelocity, float minSpeed, float heldBearing)
            => IsoCharacterMath.HeadingFor(deckVelocity, minSpeed, heldBearing);

        /// <summary>
        /// The COMPASS heading (degrees, 0 = North, CW — what a character sheet's row is picked by) of a
        /// rider holding <paramref name="deckBearingDegrees"/> on a hull whose picture is drawn at
        /// <paramref name="hullDrawnHeadingDegrees"/>.
        ///
        /// <para>The hull's <b>DRAWN</b> heading, never her physics heading: the rider is composited onto
        /// the picture on screen, and on a snap-directional hull that picture lags the body by up to half a
        /// facing step. Wrapped to [0, 360) so the published value stays a sane bearing however many turns
        /// the boat has made.</para>
        /// </summary>
        public static float CompassHeading(float hullDrawnHeadingDegrees, float deckBearingDegrees)
            => Wrap360(hullDrawnHeadingDegrees + deckBearingDegrees);

        /// <summary>
        /// The deck bearing a rider ALREADY facing <paramref name="compassHeadingDegrees"/> is holding, on
        /// a hull drawn at <paramref name="hullDrawnHeadingDegrees"/> — the exact inverse of
        /// <see cref="CompassHeading"/>.
        ///
        /// <para>This is what makes stepping aboard continuous. Seeding the bearing to "looking at the bow"
        /// would spin the fisher to face forward the instant their feet hit the deck; seeding it from the
        /// facing they arrived with means the composition's first frame reproduces that facing exactly, and
        /// only the boat's own turning moves them afterwards.</para>
        /// </summary>
        public static float DeckBearingFor(float compassHeadingDegrees, float hullDrawnHeadingDegrees)
            => Wrap360(compassHeadingDegrees - hullDrawnHeadingDegrees);

        /// <summary>Degrees into [0, 360). A NaN in gives a NaN out deliberately — a caller feeding one has
        /// a broken heading source and must not have it quietly turned into North.</summary>
        private static float Wrap360(float degrees)
        {
            float d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }
    }
}
