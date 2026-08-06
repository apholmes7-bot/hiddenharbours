using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The boat's own answer to "where can I stand?"</b> — the seam between the hull's data
    /// (<see cref="BoatVisualDef.Deck"/>) and the Player lane's deck-walk, which must not reach into
    /// Boats' asset graph to find it (rule 4: a feature module talks through a named seam, not through
    /// another module's fields).
    ///
    /// <para>Written on the boat's PHYSICS ROOT by <see cref="BoatHullSkinner"/> on every skin/re-skin,
    /// exactly where <see cref="BoatHullPresenterHost"/> is written and for the same reason: the dev
    /// hull-picker swaps a hull under the player's feet, and the walk must pick up the new deck on the
    /// very next tick rather than through a reference captured at boarding. A hull with no deck data
    /// clears the component's def rather than removing the component, so a swap back re-arms with no
    /// AddComponent churn.</para>
    ///
    /// <para>No Update, no allocation, no state of its own — a labelled pointer, deliberately.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatDeckAreas : MonoBehaviour
    {
        [Tooltip("This hull's imported walkable areas (BoatVisualDef.Deck). Null = she has no measured " +
                 "deck and the walk falls back to its greybox rectangle — absence is data, not a fault.")]
        [SerializeField] private BoatDeckDef _deck;

        /// <summary>The hull's imported deck areas; null when she has none.</summary>
        public BoatDeckDef Deck => _deck;

        /// <summary>True when this hull carries a usable walkable polygon set.</summary>
        public bool HasWalkableDeck() => _deck != null && _deck.HasWalkableDeck();

        /// <summary>Point this hull at (or away from) a deck def. Adds the component only when there is
        /// something to say — an unmeasured hull that never had one does not grow an empty marker.</summary>
        public static void Write(GameObject root, BoatDeckDef deck)
        {
            if (root == null) return;
            var host = root.GetComponent<BoatDeckAreas>();
            if (host == null)
            {
                if (deck == null) return;                  // nothing to record, nothing to add
                host = root.AddComponent<BoatDeckAreas>();
            }
            host._deck = deck;
        }

        /// <summary>The deck areas installed on <paramref name="root"/>, or null. The one read the
        /// deck-walk makes; null-safe on a boat that was never skinned.</summary>
        public static BoatDeckDef Resolve(GameObject root)
        {
            if (root == null) return null;
            var host = root.GetComponent<BoatDeckAreas>();
            return host != null ? host._deck : null;
        }

        /// <summary>Wire the deck in one call (tests / editor builders).</summary>
        public void Configure(BoatDeckDef deck) => _deck = deck;
    }
}
