#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE OTTER, PARKED SAFE FOR NOW</b> — the amphibian's place in the world, and the same shape the
    /// Dually's <see cref="NineMileCreekTruckPark"/> established: one drivable
    /// <see cref="ParkedVehicle"/>, placed by the region builder off a published constant.
    ///
    /// <para><b>Why she needed placing at all.</b> #562 built the whole drive⇄swim model and #581 baked
    /// her, and until now she existed only in tests: usable, and standing nowhere. That is the exact
    /// gap #560 left the Dually in, found by the owner walking the world and not finding her.</para>
    ///
    /// <para><b>Places, does NOT draw</b> — the moorage law. The mesh path is runtime-owned, so she
    /// skins herself at play; a builder that skinned her here would bake the sprite fallback into the
    /// committed scene (memory <c>mesh-hulls-must-skin-at-runtime</c>).</para>
    ///
    /// <para><b>Where she stands since NMC H2.</b> She was staged beside the boat ramp
    /// (<see cref="NineMileCreekMainland.OtterLandingPos"/>), pointed down it. G3 has no boat ramp,
    /// and the owner ruled (2026-10-15): "Just place the otter somewhere safe for now. Finishing the
    /// scene is more important". So she keeps her spot and faces inland
    /// (<see cref="NineMileCreekGroundPlan.OtterParkPos"/>,
    /// <see cref="NineMileCreekGroundPlan.OtterParkHeadingDegrees"/>), and N1's step 08 turns the
    /// scene's Otter the same way. Her launch is a later wave's; her name stays, so a rebuild still
    /// finds her.</para>
    /// </summary>
    public static class NineMileCreekOtterLanding
    {
        /// <summary>The placed object's name, so a rebuild can find and replace it.</summary>
        public const string OtterName = "OtterAtTheRamp";

        /// <summary>Repo path of the def she carries — the asset #581 baked.</summary>
        public const string OtterDefPath = "Assets/_Project/Data/Vehicles/Otter8x8.asset";

        /// <summary>
        /// Stand the Otter at her park. Returns her, or null (with a warning) when her def is not on
        /// disk — a region built before the vehicle bake has no Otter, stated rather than silent.
        /// </summary>
        public static GameObject Place()
        {
            var otter = AssetDatabase.LoadAssetAtPath<VehicleDef>(OtterDefPath);
            if (otter == null)
            {
                Debug.LogWarning($"[NineMileCreekOtterLanding] No def at {OtterDefPath} — she " +
                                 "stands nowhere. Bake the vehicle (#581) before the region.");
                return null;
            }

            var otterGo = new GameObject(OtterName, typeof(Rigidbody2D));
            otterGo.transform.position = NineMileCreekGroundPlan.OtterParkPos;
            otterGo.transform.rotation = NineMileCreekGroundPlan.OtterParkRotation;

            // Serialized state carries gravityScale 0 too, though VehicleController.Awake re-zeroes it
            // at play — a machine must not fall south through a top-down world.
            otterGo.GetComponent<Rigidbody2D>().gravityScale = 0f;

            otterGo.AddComponent<ParkedVehicle>().Configure(otter, drivable: true);
            return otterGo;
        }
    }
}
#endif
