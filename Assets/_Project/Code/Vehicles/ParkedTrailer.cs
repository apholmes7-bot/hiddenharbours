using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>A towed body standing in the world, that skins herself when the scene runs</b> — the
    /// trailer's <see cref="ParkedVehicle"/>, and it exists for exactly the reason that one does.
    ///
    /// <para><b>Why a second component rather than a flag on the first.</b> <c>ParkedVehicle</c>
    /// carries a <see cref="VehicleDef"/> and hands it a <see cref="VehicleController"/> and a
    /// <see cref="VehicleDoor"/> when she is drivable. A towed body has no def — PR 2 left every
    /// field of one off her deliberately, because they are all a driven machine's — so a
    /// <c>ParkedVehicle</c> with a null def is not a trailer, it is a refusal. What she needs is a
    /// mesh, her legs down, and her heading; that is this.</para>
    ///
    /// <para><b>Places, does NOT draw</b> — the moorage law, the same one the truck park and the
    /// Otter's landing obey. The mesh path is runtime-owned, so she skins herself at play; a builder
    /// that skinned her here would serialise the unskinned state into the committed scene (memory
    /// <c>mesh-hulls-must-skin-at-runtime</c>).</para>
    ///
    /// <para>⚠️ <b>Her heading is the transform's, and it has to be.</b> She is authored into a scene
    /// by rotating this object; <see cref="TowedBody"/> reads that back on enable. Nothing here
    /// serialises a heading of its own — see <see cref="TowedBody.HeadingDegrees"/> for why a second
    /// copy is the bug rather than the belt and braces.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParkedTrailer : MonoBehaviour
    {
        [Tooltip("Which towed body stands here. Serialized by the builder; skinned at runtime.")]
        [SerializeField] private VehicleMeshDef _body;

        private VehicleSkinner.Rig _rig;

        /// <summary>Which towed body stands here.</summary>
        public VehicleMeshDef Body => _body;

        /// <summary>True once she has actually taken the mesh path.</summary>
        public bool IsSkinned => _rig.Skinned;

        /// <summary>Her <see cref="TowedBody"/> — what a tractor's hitch finds and couples to.</summary>
        public TowedBody Trailer { get; private set; }

        /// <summary>
        /// Set her up from code — the builder's and the tests' path.
        ///
        /// <para>⚠️ Skins immediately when the object is already live, for the reason
        /// <see cref="ParkedVehicle.Configure"/> documents at length: <c>AddComponent</c> on an ACTIVE
        /// GameObject runs <c>OnEnable</c> before the caller has said what this is, and the
        /// <c>SetActive(true)</c> that usually follows is a no-op. <see cref="Skin"/> is idempotent,
        /// so a caller who does both skins once.</para>
        /// </summary>
        public void Configure(VehicleMeshDef body)
        {
            _body = body;
            if (isActiveAndEnabled) Skin();
        }

        private void OnEnable() => Skin();

        /// <summary>
        /// Install her picture and her towed-body behaviour. Idempotent, and safe to call when no
        /// presentation service is registered — she simply stays unskinned, which is the correct
        /// EditMode answer rather than an error.
        ///
        /// <para><b>Order is load-bearing.</b> The skin is what installs her
        /// <see cref="VehicleDoors"/> and snaps them shut — and shut is <c>0</c>, which on a trailer
        /// is her landing gear DOWN, the pose she bakes parked at. <see cref="TowedBody"/> then finds
        /// those doors rather than growing a second set, so <see cref="TowedBody.LegsAreDown"/> reads
        /// the same crank the player turns.</para>
        /// </summary>
        public void Skin()
        {
            if (_body == null) return;

            _rig = VehicleSkinner.ApplyTowed(gameObject, _body);

            if (Trailer == null)
            {
                Trailer = GetComponent<TowedBody>();
                if (Trailer == null) Trailer = gameObject.AddComponent<TowedBody>();
            }
            Trailer.Configure(_body);
        }

        private void OnDisable()
        {
            VehicleSkinner.Remove(gameObject);
            _rig = default;
        }
    }
}
