using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A tie-off point on the land</b> — a wharf bollard, a quay cleat, a ring on a piling — published to
    /// the rope gameplay as a Core <see cref="IMooringCleat"/>. Drop it on a fitting, give it the deck
    /// height it is bolted to, and it registers itself on enable: the same self-installing, scene-scoped
    /// pattern as <see cref="StandablePlatform"/> / <see cref="TidalTerrain"/>, so gameplay reads it through
    /// Core and never references this class (rule 4).
    ///
    /// <para><b>Why it lives beside <see cref="StandablePlatform"/>.</b> Shore furniture is authored world
    /// content — the design doc's own split is explicit that boats carry cleats as RIG DATA while
    /// "shore furniture (wharves, floats) has counterpart cleat/bollard points (world-content authors those
    /// in-scene)". This is that counterpart, and like the platform it is data (rule 6): id and elevation are
    /// serialized, with a <see cref="Configure"/> for the region builders.</para>
    ///
    /// <para><b>Its elevation does NOT move with the tide, and that is the entire mechanic.</b> A wharf
    /// bollard is bolted to planks at a fixed height above chart datum; a boat's cleat floats. The growing
    /// gap between them as the water falls is what eats a short line's reach across the water — see
    /// <see cref="MooringLineMath.HorizontalReach"/>. So author this from the deck the fitting stands on
    /// (the wharf builders measure it off the terrain, never a literal) and the tide does the rest.</para>
    ///
    /// <para><b>Placement is expected to move; consumers are not.</b> These markers are placed today by the
    /// wharf builders from the same fittings table that positions the bollard sprites. When the wharf rig
    /// grows real mount symbols (the art-director's export, the boats' own <c>CLEATS</c> pattern), only the
    /// PLACEMENT changes: this component and everything reading <see cref="IMooringCleat"/> stay exactly as
    /// they are.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShoreCleat : MonoBehaviour, IMooringCleat
    {
        [Header("Identity")]
        [Tooltip("Stable id of this fitting (e.g. 'wharf.st_peters.bollard_2') — diagnostics, and what a " +
                 "saved made-fast line names. Never a lookup key: the registry is walked, not hashed.")]
        [SerializeField] private string _id = "cleat.unnamed";

        [Header("Height")]
        [Tooltip("The fitting's height in METRES ABOVE CHART DATUM — the same frame the tide model, the " +
                 "terrain and StandablePlatform all speak. Normally the deck elevation of the structure " +
                 "it is bolted to. MEASURE it off that structure rather than picking a number: this is " +
                 "the fixed end of every mooring line, and a guessed one quietly mistunes how much tide a " +
                 "given scope can absorb.")]
        [SerializeField] private float _elevationMeters;

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public CleatSide Side => CleatSide.Shore;

        /// <summary>Where the fitting stands, in world metres — this transform's own position. Shore
        /// furniture does not move, so there is nothing to project and nothing to recompute.</summary>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ElevationMeters => _elevationMeters;

        /// <summary>Author the cleat in one call (region builders; the direct-configure convention).</summary>
        public void Configure(string id, float elevationMeters)
        {
            _id = id;
            _elevationMeters = elevationMeters;
        }

        private void OnEnable() => MooringCleats.Register(this);

        private void OnDisable() => MooringCleats.Unregister(this);
    }
}
