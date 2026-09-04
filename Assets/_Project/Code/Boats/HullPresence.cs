using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>SAYS THAT A BOAT IS LYING HERE</b> — the one component that puts a hull into
    /// <see cref="HullPresences"/>, so the water knows there is something to swim up to.
    ///
    /// <para><b>Why one component and not an interface on each boat class.</b> The player's hull
    /// (<see cref="BoatController"/>) and a berthed one (<see cref="MooredBoat"/>) have almost nothing in
    /// common except that they are boats floating in world metres, and the outline is the ONLY thing the
    /// registry wants. Implementing it twice would mean deriving a hull's beam twice, and the second
    /// derivation is where the two quietly stop agreeing. So the derivation lives here, once, and both
    /// call <see cref="Install"/>.</para>
    ///
    /// <para><b>⚠ A hull with no honest size does not register.</b> <see cref="Configure"/> refuses a
    /// non-positive length, and an unconfigured component stays out of the registry — because a
    /// zero-length footprint is a POINT at the object's origin, and a point in a registry that opens the
    /// boat-only wall around it would open a hole in the sea around nothing. Absence is the safe reading:
    /// no hull here, wall stands.</para>
    ///
    /// <para><b>Her beam, honestly.</b> <c>BoatHullDef</c> carries a length and a draught and <b>no
    /// beam</b> (the gap <c>docs/design/npc-pilotage.md</c> §3 already names). Where the hull's mesh
    /// authors a <c>WatertightHalfBeamMeters</c> that is the measured half-beam and it is used; where it
    /// does not, <see cref="BeamFraction"/> stands in — the same 0.37 the arrival uses to place a cape
    /// islander's mooring cleat, which lands within a centimetre of St Peters' independently measured
    /// 2.40 m. Either way this is a REACH of several metres, so the residual is noise; what would not be
    /// noise is measuring to her root, which is why <see cref="HullFootprint"/> exists.</para>
    ///
    /// <para><b>Runtime only, never baked.</b> Installed from the components that bring a hull to life,
    /// so a scene a builder wrote carries no serialized registration for a boat that has not been skinned
    /// yet — the same discipline the hull renderer follows for the same reason.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HullPresence : MonoBehaviour, IHullPresence
    {
        /// <summary>Beam as a fraction of length, for a hull whose mesh does not author a half-beam.
        /// The arrival's own ratio (<c>ArrivalOpening.HullBeamFraction</c>), which checks out against the
        /// cape islander's measured beam to a centimetre.</summary>
        public const float BeamFraction = 0.37f;

        [Tooltip("Her length overall in metres (BoatHullDef.LengthMeters). Zero means 'not configured' " +
                 "and keeps her OUT of the registry — see the class note.")]
        [SerializeField, Min(0f)] private float _lengthMetres;

        [Tooltip("Half her beam in metres — the hull mesh's authored WatertightHalfBeamMeters where there " +
                 "is one, else length x BeamFraction / 2.")]
        [SerializeField, Min(0f)] private float _halfBeamMetres;

        /// <summary>
        /// Her outline where she lies THIS INSTANT: the root's position, her bow along
        /// <c>transform.up</c> (the convention every hull in this game is rotated in — the root carries
        /// the heading and the visual child counter-rotates), un-projected world metres.
        /// </summary>
        public HullFootprint Footprint =>
            HullFootprint.FromBowDirection(transform.position, transform.up,
                                           _lengthMetres, _halfBeamMetres);

        /// <summary>Her length overall (m) as configured; 0 = not configured, i.e. not registered.</summary>
        public float LengthMetres => _lengthMetres;

        /// <summary>Half her beam (m) as configured.</summary>
        public float HalfBeamMetres => _halfBeamMetres;

        /// <summary>
        /// Give her a size, and register her if she now has an honest one. Safe to call again (a hull
        /// swap): the registry ignores a double registration, and the footprint is read live, so nothing
        /// caches the old dimensions.
        /// </summary>
        public void Configure(float lengthMetres, float halfBeamMetres)
        {
            _lengthMetres = Mathf.Max(0f, lengthMetres);
            _halfBeamMetres = Mathf.Max(0f, halfBeamMetres);
            if (isActiveAndEnabled && _lengthMetres > 0f) HullPresences.Register(this);
        }

        /// <summary>
        /// Put one on this hull and size it from her def. Hands back the component, or <c>null</c> when
        /// the def cannot say how big she is — the caller does not have to guess, and a hull nobody can
        /// measure simply is not in the registry.
        /// </summary>
        /// <param name="host">The hull ROOT (the transform that carries her heading).</param>
        /// <param name="hull">Her def — the length comes from here.</param>
        /// <param name="visual">Her visual, for the mesh's authored half-beam. Null falls back to the
        /// def's own visual, and then to <see cref="BeamFraction"/>.</param>
        public static HullPresence Install(GameObject host, BoatHullDef hull, BoatVisualDef visual = null)
        {
            if (host == null || hull == null || hull.LengthMeters <= 0f) return null;

            var presence = host.GetComponent<HullPresence>();
            if (presence == null) presence = host.AddComponent<HullPresence>();
            presence.Configure(hull.LengthMeters, HalfBeamOf(hull, visual));
            return presence;
        }

        /// <summary>Half her beam in metres — the authored mesh value where there is one, else the
        /// <see cref="BeamFraction"/> stand-in. The ONE derivation (see the class note).</summary>
        public static float HalfBeamOf(BoatHullDef hull, BoatVisualDef visual = null)
        {
            if (hull == null) return 0f;
            BoatVisualDef skin = visual != null ? visual : hull.Visual;
            float authored = skin != null && skin.HullMesh != null
                ? skin.HullMesh.WatertightHalfBeamMeters
                : 0f;
            return authored > 0f ? authored : Mathf.Max(0f, hull.LengthMeters) * BeamFraction * 0.5f;
        }

        private void OnEnable()
        {
            // Only a hull somebody has sized: an unconfigured one is a point, and a point must not open
            // the boat-only wall around itself. Configure() registers her the moment she has a size.
            if (_lengthMetres > 0f) HullPresences.Register(this);
        }

        private void OnDisable() => HullPresences.Unregister(this);
    }
}
