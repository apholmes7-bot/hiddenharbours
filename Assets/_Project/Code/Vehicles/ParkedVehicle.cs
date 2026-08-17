using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>A vehicle standing in the world, that skins herself when the scene runs (ADR 0035)</b> —
    /// the vehicle lane's <c>MooredBoat</c>, and it exists for exactly the reason that one does.
    ///
    /// <para>⚠️ <b>A scene builder must place THIS, not a skinned truck.</b> The mesh path is
    /// runtime-owned: the Art service registers at <c>RuntimeInitializeOnLoadMethod</c> and
    /// deliberately does not register inside an editor builder, so anything a builder skins is
    /// serialised in the fallback state — permanently, invisibly, and in exactly the right place, so
    /// nothing ever warns. A GameObject carrying a Def and skinning itself in <c>OnEnable</c> chooses
    /// the mesh path live, per run. See memory <c>mesh-hulls-must-skin-at-runtime</c>, where the boat
    /// fleet shipped that bug.</para>
    ///
    /// <para><b>The Def is serialized, so the builder's job is one field and a position.</b> Content
    /// is data (rule 2): where a truck stands is scene authoring, what she IS is an asset.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ParkedVehicle : MonoBehaviour
    {
        [Tooltip("Which vehicle stands here. Serialized by the builder; skinned at runtime.")]
        [SerializeField] private VehicleDef _vehicle;

        [Tooltip("Give her a controller so she can be driven. False leaves her scenery — parked, " +
                 "drawn, and not simulated, which is what most trucks in a village should be.")]
        [SerializeField] private bool _drivable;

        private VehicleSkinner.Rig _rig;

        /// <summary>Which vehicle stands here.</summary>
        public VehicleDef Vehicle => _vehicle;

        /// <summary>True once she has actually taken the mesh path.</summary>
        public bool IsSkinned => _rig.Skinned;

        /// <summary>Her controller, when she is drivable; null when she is scenery.</summary>
        public VehicleController Controller { get; private set; }

        /// <summary>Set her up from code — the builder's and the tests' path. Call before enable.</summary>
        public void Configure(VehicleDef vehicle, bool drivable)
        {
            _vehicle = vehicle;
            _drivable = drivable;
        }

        private void OnEnable() => Skin();

        /// <summary>
        /// Install her picture (and her controller, if she is drivable). Idempotent, and safe to
        /// call when no presentation service is registered — she simply stays unskinned, which is
        /// the correct EditMode answer rather than an error.
        /// </summary>
        public void Skin()
        {
            if (_vehicle == null) return;

            if (_drivable && Controller == null)
            {
                Controller = GetComponent<VehicleController>();
                if (Controller == null) Controller = gameObject.AddComponent<VehicleController>();
                Controller.SetVehicle(_vehicle);
            }

            _rig = VehicleSkinner.Apply(gameObject, _vehicle, Controller);
        }

        private void OnDisable()
        {
            VehicleSkinner.Remove(gameObject);
            _rig = default;
        }
    }
}
