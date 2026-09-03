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

        /// <summary>
        /// ⭐ <b>Her heading in degrees</b>, the same convention the controller and the hitch use —
        /// and <b>carried by the ROOT TRANSFORM</b>, which is the whole point.
        ///
        /// <para>The picture is posed from <c>BearingDegrees(transform.up)</c>
        /// (<see cref="VehicleMeshDriver.CurrentDirUnits"/>), and so is
        /// <see cref="VehicleHitch.HeadingDegrees"/>. So a heading kept ONLY in a field here would be
        /// a second copy of one fact, and the two would disagree the moment either moved without the
        /// other. They did: a trailer under tow off-tracked correctly in the arithmetic while her
        /// drawn picture stood frozen at the angle she was parked on, because
        /// <see cref="FollowKingpin"/> wrote the field and nothing wrote the transform. It was
        /// invisible only because nothing skinned a trailer yet.</para>
        ///
        /// <para>So the setter mirrors to the transform and the getter reads a field kept exactly in
        /// step with it. The field is the one carrying full float precision — reading the bearing back
        /// out of a quaternion every time would quantise every step of
        /// <see cref="FollowKingpin"/>'s read-modify-write — while the transform is what every reader
        /// already asks. One fact, one place, two spellings that cannot drift.</para>
        ///
        /// <para>⚠️ It is the ROOT that turns, never the visual child: the child is stomped to
        /// screen-identity every frame, because the picture turns by posing the mesh. That is the
        /// distinction the old note here was reaching for, and it argues for the root carrying the
        /// heading rather than against it.</para>
        /// </summary>
        public float HeadingDegrees
        {
            get => _headingDegrees;
            set
            {
                _headingDegrees = value;
                // z = −bearing is the exact inverse of BoatKinematics.BearingDegrees(transform.up):
                // up = (−sin z, cos z), and atan2(−sin z, cos z) = −z. Dirty-checked so a heading that
                // does not move costs no transform write.
                Quaternion want = Quaternion.Euler(0f, 0f, -value);
                if (transform.rotation != want) transform.rotation = want;
            }
        }

        private float _headingDegrees;

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
        /// well forward of her origin (3.365 m on a pup), so this is not her position.
        ///
        /// <para>⭐ Through <see cref="VehicleCouplingMath.LocalOffsetToWorld"/> — the ONE rotation, in the
        /// transform frame — so the pin is reported where the picture draws it. This used to be a
        /// private counter-clockwise turn that agreed with the drawn trailer only facing north or
        /// south; <c>VehicleCouplingTests</c> now asks at four headings that this and
        /// <c>transform.TransformPoint</c> answer the same point.</para></summary>
        public Vector2 KingpinWorld
        {
            get
            {
                VehicleKingpin pin = Kingpin;
                if (!pin.Published) return transform.position;

                Vector2 local = new Vector2(pin.CouplingPointLocal.x, pin.CouplingPointLocal.y);
                return (Vector2)transform.position
                       + VehicleCouplingMath.LocalOffsetToWorld(local, HeadingDegrees);
            }
        }

        public void Configure(VehicleMeshDef mesh)
        {
            _mesh = mesh;

            // ⚠️⚠️ DO NOT TIDY THIS CALL AWAY — it is not a duplicate of OnEnable.
            // An EDITOR builder never gets an OnEnable: AddComponent fires no callback on a plain
            // MonoBehaviour outside play (memory editmode-has-no-onenable), so a trailer a region
            // builder stands up would sit unregistered, at heading 0, while her picture drew on the
            // angle she was placed at. Measured: her pin was reported 7.175 m on the WRONG SIDE of
            // her, and the tractor parked on her plate was offered no couple at all.
            AdoptTheTransformAndRegister();

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

        /// <summary>
        /// ⭐ <b>Seed the heading from the transform</b> — what makes a trailer AUTHORED INTO A SCENE
        /// come back on the angle she was placed on.
        ///
        /// <para><see cref="HeadingDegrees"/>'s backing field is not serialized and must not be: the
        /// transform's rotation already is, and a second serialized copy is a second thing to get out
        /// of step across a save. So the transform is the authority at load and the field is filled
        /// from it here.</para>
        ///
        /// <para>Idempotent, because every setter write mirrors INTO the transform: re-enabling a
        /// trailer whose heading was set in code reads back the value that was set, not a stale one.
        /// Without this a scene-placed trailer loaded at heading 0 while her picture drew on the
        /// authored angle — her pin reported somewhere she was not, and the tractor parked nose to
        /// nose with her refused the couple.</para>
        /// </summary>
        private void OnEnable()
        {
            AdoptTheTransformAndRegister();
            if (_doors == null) _doors = GetComponent<VehicleDoors>();
        }

        /// <summary>
        /// Take the heading the transform is carrying, and make sure the registry knows her — the two
        /// things that must be true of a trailer standing in the world, done from BOTH the enable hook
        /// and <see cref="Configure"/>.
        ///
        /// <para>Both, because neither alone covers both callers. A trailer deserialized from a saved
        /// scene is never <c>Configure</c>d — her mesh is already on the component — so the hook is what
        /// seeds her. A trailer a region BUILDER stands up gets no hook at all, because editor-time
        /// <c>AddComponent</c> fires no callback on a plain MonoBehaviour. Idempotent, so doing it twice
        /// costs a float and a list scan.</para>
        ///
        /// <para>The registry is compacted first. <see cref="OnDisable"/> is the normal way out, but an
        /// editor fixture that destroys a trailer never gets one either, and a stale entry would be
        /// asked for its pin by every capture test that ran afterwards.</para>
        /// </summary>
        private void AdoptTheTransformAndRegister()
        {
            _headingDegrees = BoatKinematics.BearingDegrees(transform.up);

            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] == null) Live.RemoveAt(i);

            if (!Live.Contains(this)) Live.Add(this);
        }

        private void OnDisable()
        {
            Live.Remove(this);
            // A trailer that leaves the world must not leave a tractor thinking she is still on.
            if (CoupledTo != null) CoupledTo.ForgetTrailer(this);
        }
    }
}
