using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Art's side of the vehicle-mesh seam (ADR 0035)</b> — the
    /// <see cref="IVehicleMeshPresentationService"/> that installs an <see cref="IsoFacetHullRenderer"/>
    /// on a host GameObject from a committed <see cref="VehicleMeshDef"/>. Vehicles calls it through
    /// <see cref="VehicleMeshPresentation.Service"/> and never sees a URP type (rule 4).
    ///
    /// <para><b>The renderer is the hull's, unchanged, and that is the whole point of the exercise.</b>
    /// A vehicle rig is baked through the same camera (40° elevation), packs the same UV0 face
    /// constants, and needs the same four pose channels. The facet pass does not know what it is
    /// drawing, and it should not have to — so this class is a different DOOR onto the same room,
    /// not a second room.</para>
    ///
    /// <para><b>What it deliberately does NOT do, and why the separate door earns its keep.</b>
    /// <see cref="IsoFacetHullPresentationService.Install"/> also attaches a <c>FoamInjector</c> (so
    /// the hull churns the advected wake-foam buffer) and a <c>ReflectiveObject</c> (so she appears
    /// in the water). Both are right for a boat and wrong for a truck: a Dually parked on Wharf Road
    /// would lay a foam ribbon down the gravel and cast a reflection in a harbour she is nowhere
    /// near. Neither is attached here. The waterline clamp fields are likewise left at 0 — the
    /// documented "clamp off" — because a road vehicle has no watertight deck.</para>
    ///
    /// <para><b>Self-registering at runtime</b> (<see cref="RuntimeInitializeOnLoadMethod"/>, before
    /// the first scene), so a player build and PlayMode tests get the mesh path with no wiring.
    /// EditMode tests and editor tooling call <see cref="EnsureRegistered"/> explicitly; edit-time
    /// scene BUILDERS deliberately do not — see <see cref="VehicleMeshPresentation"/> for the
    /// failure that rule prevents.</para>
    /// </summary>
    public sealed class IsoFacetVehiclePresentationService : IVehicleMeshPresentationService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAtLoad() => EnsureRegistered();

        /// <summary>Idempotent registration. Never replaces a live service (a test double stays).</summary>
        public static void EnsureRegistered()
        {
            VehicleMeshPresentation.Service ??= new IsoFacetVehiclePresentationService();
        }

        /// <inheritdoc/>
        public IHullMeshRenderer Install(GameObject host, VehicleMeshDef def)
        {
            if (host == null) return null;
            if (def == null || !def.IsUsable())
            {
                Debug.LogError($"[IsoFacetVehiclePresentationService] '{host.name}': vehicle mesh def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable (missing mesh/ramps/" +
                               "bayer, bad cell geometry, or no chassis). No renderer installed — the " +
                               "caller should refuse to place her rather than field an invisible truck.");
                return null;
            }

            var renderer = host.GetComponent<IsoFacetHullRenderer>();
            if (renderer == null) renderer = host.AddComponent<IsoFacetHullRenderer>();
            renderer.Configure(ToSetup(def));
            return renderer;
        }

        /// <inheritdoc/>
        public IHullPropRenderer AttachWheel(GameObject host, HullPropMeshDef def, string slot)
        {
            if (host == null) return null;
            if (def == null || !def.IsUsable())
            {
                Debug.LogError($"[IsoFacetVehiclePresentationService] '{host.name}': fitting def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable. Not attached — she " +
                               "will drive on the body mesh alone, which has no wheels in it at all.");
                return null;
            }

            var vehicle = host.GetComponent<IsoFacetHullRenderer>();
            if (vehicle == null)
            {
                Debug.LogError($"[IsoFacetVehiclePresentationService] '{host.name}': no vehicle mesh is " +
                               $"installed, so fitting '{def.Id}' has nothing to bolt to. Install the " +
                               "body first — a wheel posed against no body would sit at the world " +
                               "origin steering on its own.");
                return null;
            }

            // ⚠️ PARENT TO THE POSED CHILD, not to the host. The host does not turn; the posed mesh
            // carries heading and the suspension pose, and a wheel must inherit both or it shears off
            // the truck the instant she moves.
            Transform posed = vehicle.PosedMesh;
            if (posed == null)
            {
                Debug.LogError($"[IsoFacetVehiclePresentationService] '{host.name}': the renderer has no " +
                               $"posed mesh child to hang '{def.Id}' from. It was never configured, or " +
                               "its child layout changed and this attach point must be re-aimed.");
                return null;
            }

            Transform existing = posed.Find(slot);
            var prop = existing != null ? existing.GetComponent<IsoFacetPropRenderer>() : null;
            if (prop == null)
            {
                var go = new GameObject(slot) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(posed, false);
                prop = go.AddComponent<IsoFacetPropRenderer>();
            }
            prop.Configure(IsoFacetHullPresentationService.ToPropSetup(def));
            return prop;
        }

        /// <inheritdoc/>
        public void Remove(GameObject host)
        {
            if (host == null) return;
            var vehicle = host.GetComponent<IsoFacetHullRenderer>();
            Transform posed = vehicle != null ? vehicle.PosedMesh : null;
            if (posed != null)
            {
                for (int i = posed.childCount - 1; i >= 0; i--)
                {
                    var prop = posed.GetChild(i).GetComponent<IsoFacetPropRenderer>();
                    if (prop != null) Destroy(prop.gameObject);
                }
            }

            if (vehicle != null) Destroy(vehicle);
            var group = host.GetComponent<SortingGroup>();
            if (group != null) Destroy(group);
        }

        /// <summary>
        /// The def, converted to the renderer's runtime setup — plain copies, no rescaling.
        ///
        /// <para>The two watertight fields are left at 0, which the renderer documents as "clamp
        /// off": a road vehicle has no waterline to hold the sea below, and 0 is the same value the
        /// whole fleet carried before that clamp existed, so the vehicle path takes the render the
        /// hull path is A/B-pinned against.</para>
        /// </summary>
        public static IsoFacetHullSetup ToSetup(VehicleMeshDef def)
        {
            var ramps = new Color32[def.Ramps.Length][];
            var offsets = new int[def.Ramps.Length];
            for (int m = 0; m < def.Ramps.Length; m++)
            {
                ramps[m] = def.Ramps[m].Colors;
                offsets[m] = def.Ramps[m].Offset;
            }

            return new IsoFacetHullSetup
            {
                Mesh = def.Mesh,
                Ramps = ramps,
                RampOffsets = offsets,
                LightN = def.LightN,
                Gain = def.Gain,
                Bias = def.Bias,
                Bayer16 = def.Bayer16,
                Keyline = def.Keyline,
                PivotPx = def.PivotPx,
                PxPerMetre = def.PxPerMetre,
                CellW = def.CellW,
                CellH = def.CellH,
                ElevationDeg = def.ElevationDeg,
                WatertightDeckHeightMeters = 0f,
                WatertightHalfBeamMeters = 0f,
            };
        }

        // Editor-safe destroy: tests and tooling call Remove outside play mode, where
        // Object.Destroy throws.
        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
