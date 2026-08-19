using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A tie-off point on a float</b> — the cleat run along a floating dock, published to the rope
    /// gameplay as a Core <see cref="IMooringCleat"/>. The variant of <see cref="ShoreCleat"/> for
    /// furniture that is bolted to something which MOVES: a quay bollard is at one height above chart
    /// datum forever, and a float's cleat rides the tide with the planks it is through-bolted to.
    ///
    /// <para><b>It reads the float rather than carrying its own height.</b> One number, on the
    /// <see cref="FloatingPlatform"/>, feeds both the deck the player stands on and the fitting they make
    /// a line fast to — so the rope can never be tied two metres above or below the planks beside it. A
    /// duplicated freeboard here would be a second copy of the same fact, and the two copies would
    /// eventually disagree.</para>
    ///
    /// <para><b>⭐ AND THIS IS WHY A FLOAT IS THE KIND BERTH.</b> A boat's own cleat sits at
    /// <c>water + her freeboard</c> and this one sits at <c>water + the float's freeboard</c>, so the drop
    /// between them is the DIFFERENCE OF TWO FREEBOARDS — a constant, whatever the tide does. A line made
    /// fast at a float never comes tight on the ebb and never has to be tended, which is exactly the
    /// mechanic the wall berths do not have (there the drop grows by the whole range and a short scope
    /// slips). Nothing implements that; it falls out of the arithmetic.</para>
    ///
    /// <para><b>Still SHORE-side.</b> <see cref="CleatSide"/> answers which END of a rope this is — you
    /// tie your boat TO the float — and a line is only ever made fast between opposing sides. It is also
    /// still a FIXED anchor in the sense <c>CleatAnchor.IsFixed</c> means: the float is held in plan by
    /// its guide piles, so it holds station horizontally and only its height moves.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatCleat : MonoBehaviour, IMooringCleat
    {
        [Header("Identity")]
        [Tooltip("Stable id of this fitting (e.g. 'wharf.nine_mile_creek.float_cleat_2') — diagnostics, " +
                 "and what a saved made-fast line names. Never a lookup key.")]
        [SerializeField] private string _id = "cleat.unnamed";

        [Header("What it is bolted to")]
        [Tooltip("The float whose deck this cleat rides. Read LIVE — that is the entire difference from " +
                 "a ShoreCleat, which carries one authored elevation because a quay does not move.")]
        [SerializeField] private FloatingPlatform _float;

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public CleatSide Side => CleatSide.Shore;

        /// <summary>Where the fitting is, in world metres — this transform's own position. A float is
        /// held in plan by its guide piles, so there is nothing horizontal to recompute.</summary>
        public Vector2 WorldPosition => transform.position;

        /// <summary>The float this cleat is bolted to (for tests / tooling).</summary>
        public FloatingPlatform Float => _float;

        /// <summary>
        /// The fitting's height above chart datum: the float's deck, right now. Bolted to nothing it
        /// reads <b>0</b> (chart datum) — a cleat with no float is not a tie-off, and reporting datum is
        /// the degenerate reading that cannot silently claim a height it has no basis for.
        /// </summary>
        public float ElevationMeters => _float != null ? _float.DeckElevationNow() : 0f;

        /// <summary>Author the cleat in one call (region builders; the direct-configure convention).</summary>
        public void Configure(string id, FloatingPlatform floatingPlatform)
        {
            _id = id;
            _float = floatingPlatform;
        }

        private void OnEnable() => MooringCleats.Register(this);

        private void OnDisable() => MooringCleats.Unregister(this);
    }
}
