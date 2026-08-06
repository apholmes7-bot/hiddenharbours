using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The live WALKABLE-DECK frame under the player's feet (Rod Fishing v2 §4 — fishing off the
    /// rocking, drifting deck): where the hull is, which way its drawn picture points, and the deck
    /// bounds the player walks in, and where in those bounds they stand — exactly the frame the Player
    /// lane's deck-walk is clamping to this frame. Everything a consumer needs to express "where does
    /// the angler stand ON the boat, and does their line cross the hull?" without touching the Player
    /// or Boats modules.
    ///
    /// <para><b>The bounds are the HULL's, not a constant.</b> Since M2-37's data half they are the
    /// axis-aligned box around that hull's authored walkable polygons (imported from her rig sidecar),
    /// so a dragger grades her rails as a dragger rather than as the dory every hull used to borrow;
    /// a hull with no measured deck still publishes the deck-walk's greybox rectangle. Either way it is
    /// a BOX around the walk area, deliberately — a consumer grading "how far across the hull does this
    /// line run" wants the hull's extent, and a box keeps that read closed-form.</para>
    /// </summary>
    public readonly struct DeckStanceState
    {
        /// <summary>The boat PHYSICS ROOT's world position (m) — the origin of the deck frame.</summary>
        public readonly Vector2 HullPosition;

        /// <summary>The compass heading of the hull PICTURE on screen (0 = North, 90 = East,
        /// clockwise — the project's bearing convention): snapped for a sprite compass, continuous for
        /// a mesh hull. The deck rectangle lives in this frame (deck +Y = the drawn bow).</summary>
        public readonly float DrawnHeadingDegrees;

        /// <summary>Centre of the walkable deck rectangle as a DECK-FRAME offset from the hull
        /// position (x abeam, y along the keel toward the bow).</summary>
        public readonly Vector2 DeckCenter;

        /// <summary>Half-extents (m) of the walkable deck rectangle in the deck frame:
        /// x = half the beam, y = half the length along the keel.</summary>
        public readonly Vector2 DeckHalfExtents;

        /// <summary>
        /// Where the player is STANDING, in that same deck frame (m) — published outright rather than
        /// left for a consumer to recover from their world position.
        ///
        /// <para>It has to be, now that hulls carry real polygons: a measured deck is projected onto the
        /// screen with its own artwork's iso foreshortening, so inverting a world offset back into the
        /// deck frame needs both that elevation AND the height of the deck being stood on. The publisher
        /// knows all three; a consumer knowing only the heading would silently read the angler out of
        /// place at every facing but N/S. On the greybox rectangle this is simply the rotation of the
        /// player's boat-relative offset — exactly what consumers computed themselves before.</para>
        /// </summary>
        public readonly Vector2 AnglerDeckPosition;

        /// <summary>The pre-M2-37 shape: no angler position published, so it defaults to the deck
        /// centre. Kept for callers (and tests) that only describe a deck, not somebody standing on it.</summary>
        public DeckStanceState(Vector2 hullPosition, float drawnHeadingDegrees,
                               Vector2 deckCenter, Vector2 deckHalfExtents)
            : this(hullPosition, drawnHeadingDegrees, deckCenter, deckHalfExtents, deckCenter) { }

        public DeckStanceState(Vector2 hullPosition, float drawnHeadingDegrees,
                               Vector2 deckCenter, Vector2 deckHalfExtents, Vector2 anglerDeckPosition)
        {
            HullPosition = hullPosition;
            DrawnHeadingDegrees = drawnHeadingDegrees;
            DeckCenter = deckCenter;
            DeckHalfExtents = deckHalfExtents;
            AnglerDeckPosition = anglerDeckPosition;
        }
    }

    /// <summary>
    /// The Core seam between the Player lane's deck-walk (the publisher — it owns the deck rectangle
    /// and the drawn-facing read) and the Fishing lane's deck-angle fight term (the consumer) —
    /// CLAUDE.md rule 4: the two feature modules meet HERE, never each other. The publish/clear shape
    /// mirrors <see cref="DisplacedSea"/>: the active <c>DeckWalkController</c> publishes its live
    /// frame each tick while the player stands on a deck and clears it when deck-walking ends, so an
    /// absent state simply means "not on a deck" — dock/shore fishing reads no stance and the
    /// deck-angle term is 0 by construction (the dock-parity contract).
    ///
    /// <para>Presentation/interaction state only, deliberately NOT part of the sim determinism
    /// contract: nothing here is saved, and the publisher recomputes it every tick from live
    /// transforms. The owner is a <see cref="UnityEngine.Object"/> so a publisher destroyed without a
    /// clean disable (scene teardown, EditMode test churn) self-heals — Unity's destroyed-object null
    /// makes <see cref="IsActive"/> read false the moment the owner dies.</para>
    ///
    /// <para>FLAG lead-architect: new Core contract (Rod Fishing v2 Wave 4's deck-stance seam).</para>
    /// </summary>
    public static class DeckStance
    {
        private static Object s_Owner;
        private static DeckStanceState s_State;

        /// <summary>True while a live deck-walk has published a stance — the ONE gate the deck-angle
        /// fight term sits behind (no stance ⇒ not on a deck ⇒ the term is exactly 0).</summary>
        public static bool IsActive => s_Owner != null;   // Unity null: a destroyed owner reads inactive

        /// <summary>The live stance; false (and <c>default</c>) when the player is not on a deck.</summary>
        public static bool TryGet(out DeckStanceState state)
        {
            bool active = s_Owner != null;
            state = active ? s_State : default;
            return active;
        }

        /// <summary>Publish the live deck frame (each tick — re-publish is how the drifting,
        /// weathervaning hull reaches the fight). One player, one deck: with multiple publishers the
        /// last one wins, exactly like <see cref="DisplacedSea"/>.</summary>
        public static void Publish(Object owner, in DeckStanceState state)
        {
            if (owner == null) return;
            s_Owner = owner;
            s_State = state;
        }

        /// <summary>Clear the stance — only by its current owner (a stale publisher going away must
        /// not kill a newer one's state). No stance ⇒ off-deck: the dock-parity contract.</summary>
        public static void Clear(Object owner)
        {
            if (!ReferenceEquals(s_Owner, owner)) return;
            s_Owner = null;
            s_State = default;
        }
    }
}
