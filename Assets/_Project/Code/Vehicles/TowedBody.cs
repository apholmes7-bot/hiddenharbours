using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>A trailer standing in the world</b> — a baked towed body, parked on her own legs until
    /// something backs under her nose.
    ///
    /// <para><b>She wears a <see cref="VehicleMeshDef"/> and NO <c>VehicleDef</c></b>, which is PR 2's
    /// deliberate omission standing. Every field on that asset is a driven machine's — top speed,
    /// acceleration, steering authority, camera height — and a towed body has none of them. What she
    /// needs instead is here: where her pin is, how long she is, and whether her legs are down.</para>
    ///
    /// <para><b>Never drivable, and not by a rule written here.</b>
    /// <see cref="VehicleKinds.IsDrivable"/> is an explicit switch over the kind table, and
    /// <c>TowedBody</c> returns false there — so nothing has to remember to special-case her, and a
    /// future kind cannot inherit "drivable" by being added.</para>
    ///
    /// <para>⚠️ <b>Her legs are the interlock.</b> A trailer whose gear is up is standing on her
    /// kingpin and nothing else; <see cref="LegsAreDown"/> is what <see cref="VehicleHitch"/> refuses
    /// an uncouple on. The state lives in <see cref="VehicleDoors"/> because the legs are worked by
    /// the same crank the player turns, rather than being a second copy of the same fact.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowedBody : MonoBehaviour
    {
        /// <summary>Every towed body currently in the world. A registry rather than a scene search:
        /// a tractor asks this every frame she is uncoupled, and <c>FindObjectsByType</c> allocates
        /// (rule 7). Registration is symmetric with enable/disable, so a despawned trailer leaves.</summary>
        private static readonly List<TowedBody> Live = new List<TowedBody>();
        public static IReadOnlyList<TowedBody> All => Live;

        [SerializeField] private VehicleMeshDef _mesh;

        private VehicleDoors _doors;

        public VehicleMeshDef Mesh => _mesh;

        /// <summary>Her pin and her follow inputs, as her own sidecar published them for THIS body.
        /// Unpublished on anything that is not a towed body — check before reading.</summary>
        public VehicleKingpin Kingpin => _mesh != null ? _mesh.Kingpin : default;

        /// <summary>The tractor she is on, or null. Set only by <see cref="VehicleHitch"/> — a
        /// trailer does not couple herself.</summary>
        public VehicleHitch CoupledTo { get; internal set; }

        public bool IsCoupled => CoupledTo != null;

        /// <summary>Her heading in degrees, the same convention the controller uses. Held here
        /// rather than read off the transform's rotation because the visual child is stomped to
        /// screen-identity every frame — the picture turns by posing the mesh, not the object.</summary>
        public float HeadingDegrees { get; set; }

        /// <summary>
        /// ⚠️ <b>Are her shoes on the ground?</b> Read from the gear crank's own state, so there is
        /// one answer rather than two that can disagree.
        ///
        /// <para>The gear group runs 0 = down (parked, the pose she bakes at) to 1 = up (towing), and
        /// <b>down means DOWN</b> — not "less than half way up". A shoe that has lifted at all is a
        /// shoe carrying nothing, so a trailer 40 % through her crank is standing on her kingpin just
        /// as surely as one fully raised. Dropping her there would put her nose in the gravel, which
        /// is precisely what <see cref="VehicleHitch.TryUncouple"/> exists to refuse.</para>
        ///
        /// <para>The comparison is exact rather than epsilon'd because
        /// <see cref="VehicleDoors.Advance"/> lands on its target exactly:
        /// <c>MoveTowards</c> clamps on arrival and <c>Approximately</c> pins it there. A crank that
        /// has run its course reads 0, and one that has not reads something strictly above it.</para>
        ///
        /// <para>A trailer with no gear at all — nothing in this pack — reads DOWN, which is the
        /// answer that refuses to drop her.</para>
        /// </summary>
        public bool LegsAreDown =>
            _doors == null || _doors.Openness("LandingGearShoes") <= 0f;

        /// <summary>Where her kingpin is in the world right now, from her live pose. The pin sits
        /// well forward of her origin (3.365 m on a pup), so this is not her position.</summary>
        public Vector2 KingpinWorld
        {
            get
            {
                VehicleKingpin pin = Kingpin;
                if (!pin.Published) return transform.position;

                float rad = HeadingDegrees * Mathf.Deg2Rad;
                float s = Mathf.Sin(rad), c = Mathf.Cos(rad);
                Vector2 local = new Vector2(pin.CouplingPointLocal.x, pin.CouplingPointLocal.y);
                Vector2 world = new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
                return (Vector2)transform.position + world;
            }
        }

        public void Configure(VehicleMeshDef mesh)
        {
            _mesh = mesh;
            _doors = GetComponent<VehicleDoors>();
            if (_doors == null && mesh != null)
            {
                _doors = gameObject.AddComponent<VehicleDoors>();
                _doors.Configure(mesh);
                _doors.SnapAllShut();
            }
        }

        /// <summary>
        /// ⭐ <b>Follow the pin one step</b> — the whole off-tracking model, and it is three lines
        /// because <see cref="VehicleCouplingMath"/> owns the arithmetic.
        ///
        /// <para>Her heading swings toward the line of travel at a rate set by how far she is folded
        /// and how long she is; then she is placed so her pin lands exactly on the tractor's. That
        /// order matters: swinging first and placing second means she pivots about the coupling
        /// rather than about her own middle, which is what a fifth wheel does.</para>
        ///
        /// <para>⚠️ The articulation is CLAMPED to the pair's cap, not refused. A jackknife held at
        /// its limit reads as a truck out of room; one that rejected the input would read as a
        /// truck that stopped steering.</para>
        /// </summary>
        public void FollowKingpin(Vector2 kingpinWorld, float tractorHeadingDegrees,
                                  float distanceMeters, float capDegrees)
        {
            VehicleKingpin pin = Kingpin;
            if (!pin.Published) return;

            float articulation = Mathf.DeltaAngle(HeadingDegrees, tractorHeadingDegrees);
            HeadingDegrees += VehicleCouplingMath.TrailerYawDeltaDegrees(
                articulation, distanceMeters, pin.KingpinToAxleCentreMeters);

            // Hold the fold at the cap by moving HER, since the tractor is the one being driven.
            float folded = Mathf.DeltaAngle(HeadingDegrees, tractorHeadingDegrees);
            float allowed = VehicleCouplingMath.ClampArticulation(folded, capDegrees);
            if (!Mathf.Approximately(folded, allowed))
                HeadingDegrees = tractorHeadingDegrees - allowed;

            Vector2 origin = VehicleCouplingMath.BodyOriginFromKingpin(
                kingpinWorld, HeadingDegrees, pin);
            transform.position = new Vector3(origin.x, origin.y, transform.position.z);
        }

        /// <summary>The fold between her and a tractor heading, degrees. Public so the hitch and the
        /// tests ask the same question of the same code.</summary>
        public float ArticulationAgainst(float tractorHeadingDegrees) =>
            Mathf.DeltaAngle(HeadingDegrees, tractorHeadingDegrees);

        private void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
            if (_doors == null) _doors = GetComponent<VehicleDoors>();
        }

        private void OnDisable()
        {
            Live.Remove(this);
            // A trailer that leaves the world must not leave a tractor thinking she is still on.
            if (CoupledTo != null) CoupledTo.ForgetTrailer(this);
        }
    }
}
