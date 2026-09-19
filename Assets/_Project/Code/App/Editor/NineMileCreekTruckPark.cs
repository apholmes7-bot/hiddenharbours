#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE DUALLY, PARKED AT THE PARK</b> — closing the reachability gap #560 left. That PR
    /// shipped the whole drive mode "on the dev-picker path only", and there IS no picker: she was
    /// drivable in every test and reachable in no scene. One drivable
    /// <see cref="ParkedVehicle"/> now stands on the truck park, placed by the region builder.
    ///
    /// <para><b>Places, does NOT draw</b> — the moorage law (<c>FleetReviewMoorage</c>,
    /// <c>NineMileCreekMooredFleet</c>): the mesh path is runtime-owned, so she skins herself at
    /// play and a builder that skinned her here would bake the sprite fallback into the committed
    /// scene (memory <c>mesh-hulls-must-skin-at-runtime</c>).</para>
    ///
    /// <para>⚠️ <b>The SITE is still the owner's PROPOSAL</b> —
    /// <see cref="NineMileCreekMainland.TruckParkPos"/> says so in capitals. Placing her there does
    /// not pre-empt the walk verdict: the position derives from the one constant everything else on
    /// the park derives from, so when the verdict moves the park she moves with it in the same
    /// one-line change. #560's gate was never "no truck"; it was "never place her twice".</para>
    ///
    /// <para>⭐ <b>And the Modern 3500 beside him</b> — the owner's ruling D2 (2026-09-18): "Park her
    /// at Nine Mile Creek beside the Dually." <see cref="PlaceModern3500"/> stands her, drivable, in the
    /// park's west bay (<see cref="NineMileCreekMainland.Modern3500ParkPos"/>, derived from the same one
    /// constant) by the same recipe. Her def is the fleet bake's, never hand-written.</para>
    /// </summary>
    public static class NineMileCreekTruckPark
    {
        /// <summary>The placed object's name, so a rebuild can find and replace it.</summary>
        public const string TruckName = "DuallyAtThePark";

        /// <summary>Repo path of the def she carries — the asset #556 baked.</summary>
        public const string DuallyDefPath = "Assets/_Project/Data/Vehicles/Dually3500.asset";

        /// <summary>The Modern 3500's placed name — her own, so a rebuild or a lookup by name can
        /// never take her for him.</summary>
        public const string Modern3500Name = "Modern3500AtThePark";

        /// <summary>Repo path of the def she carries — the one her row in the fleet bake
        /// (<c>VehicleRigFleet</c>) produces.</summary>
        public const string Modern3500DefPath = "Assets/_Project/Data/Vehicles/Modern3500.asset";

        /// <summary>
        /// Stand the Dually on the park. Returns the truck, or null (with a warning) when her def
        /// is not on disk — a region built before the vehicle bake gets an empty park, stated
        /// rather than silent.
        /// </summary>
        public static GameObject Place()
        {
            var dually = AssetDatabase.LoadAssetAtPath<VehicleDef>(DuallyDefPath);
            if (dually == null)
            {
                Debug.LogWarning($"[NineMileCreekTruckPark] No def at {DuallyDefPath} — the truck " +
                                 "park builds empty. Bake the vehicle (#556) before the region.");
                return null;
            }

            var truckGo = new GameObject(TruckName, typeof(Rigidbody2D));
            truckGo.transform.position = NineMileCreekMainland.TruckParkPos;

            // Serialized state carries gravityScale 0 too, though VehicleController.Awake re-zeroes
            // it at play — a truck must not fall south through a top-down world.
            truckGo.GetComponent<Rigidbody2D>().gravityScale = 0f;

            truckGo.AddComponent<ParkedVehicle>().Configure(dually, drivable: true);
            return truckGo;
        }

        /// <summary>
        /// Stand the Modern 3500 in the bay beside him (<see cref="NineMileCreekMainland.Modern3500ParkPos"/>),
        /// drivable. Returns her, or null (with a warning) when her def is not on disk — the same
        /// stated, not silent, empty bay as <see cref="Place"/>.
        /// </summary>
        public static GameObject PlaceModern3500()
        {
            var modern = AssetDatabase.LoadAssetAtPath<VehicleDef>(Modern3500DefPath);
            if (modern == null)
            {
                Debug.LogWarning($"[NineMileCreekTruckPark] No def at {Modern3500DefPath} — her bay " +
                                 "builds empty. Run the fleet bake (her row in VehicleRigFleet) before the region.");
                return null;
            }

            var truckGo = new GameObject(Modern3500Name, typeof(Rigidbody2D));
            truckGo.transform.position = NineMileCreekMainland.Modern3500ParkPos;

            // gravityScale 0 in the serialized state, as for him: a truck must not fall south
            // through a top-down world.
            truckGo.GetComponent<Rigidbody2D>().gravityScale = 0f;

            truckGo.AddComponent<ParkedVehicle>().Configure(modern, drivable: true);
            return truckGo;
        }
    }
}
#endif
