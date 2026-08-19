#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE OTTER, STAGED AT THE BOAT RAMP</b> — the amphibian's first place in the world, and the
    /// same shape the Dually's <see cref="NineMileCreekTruckPark"/> established: one drivable
    /// <see cref="ParkedVehicle"/>, placed by the region builder off a published PROPOSAL constant.
    ///
    /// <para><b>Why she needed placing at all.</b> #562 built the whole drive⇄swim model and #581 baked
    /// her, and until now she existed only in tests: usable, and standing nowhere. That is the exact
    /// gap #560 left the Dually in, found by the owner walking the world and not finding her.</para>
    ///
    /// <para><b>Places, does NOT draw</b> — the moorage law. The mesh path is runtime-owned, so she
    /// skins herself at play; a builder that skinned her here would bake the sprite fallback into the
    /// committed scene (memory <c>mesh-hulls-must-skin-at-runtime</c>).</para>
    ///
    /// <para><b>She is pointed down the ramp</b>, which is the one thing here that is presentation
    /// rather than position: an amphibian parked square to the water reads as abandoned, and one
    /// pointed at it reads as about to go. The heading derives from the ramp's own axis, so it cannot
    /// drift from the site.</para>
    ///
    /// <para>⚠️ <b>THE SITE IS A PROPOSAL AWAITING THE OWNER'S WALK</b> —
    /// <see cref="NineMileCreekMainland.OtterLandingPos"/> says so at length, including the one thing
    /// worth arguing with (the basin bares at spring low). Placing her does not pre-empt that verdict:
    /// she derives from the boat ramp's own two ends, so moving the ramp moves her, and moving her
    /// alone is a one-line change to that property.</para>
    /// </summary>
    public static class NineMileCreekOtterLanding
    {
        /// <summary>The placed object's name, so a rebuild can find and replace it.</summary>
        public const string OtterName = "OtterAtTheRamp";

        /// <summary>Repo path of the def she carries — the asset #581 baked.</summary>
        public const string OtterDefPath = "Assets/_Project/Data/Vehicles/Otter8x8.asset";

        /// <summary>
        /// Stand the Otter beside the boat ramp. Returns her, or null (with a warning) when her def is
        /// not on disk — a region built before the vehicle bake gets an empty ramp, stated rather than
        /// silent.
        /// </summary>
        public static GameObject Place()
        {
            var otter = AssetDatabase.LoadAssetAtPath<VehicleDef>(OtterDefPath);
            if (otter == null)
            {
                Debug.LogWarning($"[NineMileCreekOtterLanding] No def at {OtterDefPath} — the ramp " +
                                 "builds empty. Bake the vehicle (#581) before the region.");
                return null;
            }

            var otterGo = new GameObject(OtterName, typeof(Rigidbody2D));
            otterGo.transform.position = NineMileCreekMainland.OtterLandingPos;
            otterGo.transform.rotation =
                Quaternion.Euler(0f, 0f, NineMileCreekMainland.OtterLandingHeadingDegrees);

            // Serialized state carries gravityScale 0 too, though VehicleController.Awake re-zeroes it
            // at play — a machine must not fall south through a top-down world.
            otterGo.GetComponent<Rigidbody2D>().gravityScale = 0f;

            otterGo.AddComponent<ParkedVehicle>().Configure(otter, drivable: true);
            return otterGo;
        }
    }
}
#endif
