using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>The ONE place a vehicle gets her picture (ADR 0035)</b> — the sibling of
    /// <c>BoatHullSkinner</c>, and deliberately much smaller, because a truck has no sprite compass
    /// to fall back to. She is mesh or she is nothing.
    ///
    /// <para>⚠️ <b>Runtime only, and that is load-bearing.</b> The Art service registers itself at
    /// <c>RuntimeInitializeOnLoadMethod</c> and edit-time scene builders deliberately do not call
    /// <c>EnsureRegistered</c>, so this returns an unskinned rig inside a builder — by design.
    /// A builder that skinned its own vehicles would serialise a renderer whose setup is
    /// runtime-owned into the committed scene. On the boat side that failure was invisible (the
    /// sprite fallback drew a perfectly good boat in the right berth and nothing warned); here it
    /// would be an empty GameObject, which is at least loud. Either way the shape that works is the
    /// same: a builder PLACES a <see cref="ParkedVehicle"/> carrying a Def, and it skins itself on
    /// enable. See memory <c>mesh-hulls-must-skin-at-runtime</c>.</para>
    /// </summary>
    public static class VehicleSkinner
    {
        /// <summary>The child that carries the drawn image. Screen-aligned; the root carries the
        /// heading.</summary>
        public const string VisualChildName = "VehicleVisual";

        /// <summary>What a skin attempt produced.</summary>
        public readonly struct Rig
        {
            /// <summary>True when the mesh path is live and she is being drawn.</summary>
            public readonly bool Skinned;
            public readonly Transform Visual;
            public readonly VehicleMeshDriver Driver;
            public readonly IHullMeshRenderer Renderer;

            public Rig(bool skinned, Transform visual, VehicleMeshDriver driver,
                       IHullMeshRenderer renderer)
            {
                Skinned = skinned; Visual = visual; Driver = driver; Renderer = renderer;
            }
        }

        /// <summary>
        /// Dress <paramref name="root"/> as <paramref name="vehicle"/>: install the body mesh, bolt
        /// on every wheel, and wire the driver that poses them.
        ///
        /// <para>Idempotent — a re-skin reconfigures in place rather than accumulating wheels — and
        /// it REFUSES rather than half-builds: an unusable def, or no service registered, returns an
        /// unskinned rig and leaves the root alone.</para>
        /// </summary>
        public static Rig Apply(GameObject root, VehicleDef vehicle, VehicleController controller = null)
        {
            if (root == null || vehicle == null || !vehicle.IsUsable()) return default;
            return Install(root, vehicle.Mesh, vehicle.Id, controller);
        }

        /// <summary>
        /// ⭐ <b>Dress a TOWED BODY, who has no <see cref="VehicleDef"/> and never will.</b>
        ///
        /// <para>PR 2 left every <c>VehicleDef</c> field off a trailer deliberately — they are all a
        /// driven machine's — and the consequence surfaced only when something first tried to PLACE
        /// one: <see cref="Apply"/> is the sole installer of a vehicle's picture and it takes a def a
        /// trailer does not have, so a trailer stood in a scene as an invisible object. Nothing
        /// warned, because nothing had ever asked.</para>
        ///
        /// <para>The picture never needed the def. The presentation service installs from a
        /// <see cref="VehicleMeshDef"/> alone (<c>IVehicleMeshPresentationService.Install</c>), and
        /// everything below that touches the def touches only its <c>Id</c> — so this is the same
        /// installer with the mesh's own id standing in, NOT a second skinning path to keep in step.
        /// A trailer gets her body, her wheels, her landing-gear state swap, her doors and her
        /// handles from exactly the code a truck does.</para>
        ///
        /// <para>She grows no <see cref="VehicleHitch"/>: that is gated on <c>mesh.CanTow</c>, which
        /// reads the art's own plate, and a towed body publishes a kingpin rather than a fifth wheel.
        /// She is given no controller for the same reason — there is nothing to drive.</para>
        /// </summary>
        public static Rig ApplyTowed(GameObject root, VehicleMeshDef mesh)
        {
            if (root == null || mesh == null || !mesh.IsUsable()) return default;
            return Install(root, mesh, mesh.Id, null);
        }

        /// <summary>The installer both paths share — everything a picture needs, off the MESH def and
        /// an id. Private because the two public doors above are the ones that decide what may be
        /// dressed and with what.</summary>
        private static Rig Install(GameObject root, VehicleMeshDef mesh, string vehicleId,
                                   VehicleController controller)
        {
            if (mesh == null) return default;

            IVehicleMeshPresentationService service = VehicleMeshPresentation.Service;
            if (service == null) return default;

            Transform visual = root.transform.Find(VisualChildName);
            if (visual == null)
            {
                var go = new GameObject(VisualChildName);
                go.transform.SetParent(root.transform, false);
                visual = go.transform;
            }

            IHullMeshRenderer renderer = service.Install(visual.gameObject, mesh);
            if (renderer == null) return default;

            // The wheels, in the def's own order, so the driver's fitment array and its renderer
            // array are index-parallel by construction rather than by a lookup that could miss.
            var wheels = new List<IHullPropRenderer>(mesh.Wheels.Length);
            var states = new List<IHullPropRenderer[]>(mesh.Wheels.Length);

            for (int i = 0; i < mesh.Wheels.Length; i++)
            {
                VehicleFitment f = mesh.Wheels[i];

                // ⭐ A part that is neither a rotation nor a translation was baked at each end of
                // its travel instead (a trailer's telescoping legs, and so far only those). It
                // installs as ONE RENDERER PER STATE and the driver shows exactly one — there is
                // nothing to pose it by, so the alternative to a swap is a lie about its shape.
                if (f.Motion == VehicleFitmentMotion.DiscreteStates &&
                    f.StateProps != null && f.StateProps.Length > 0)
                {
                    var built = new IHullPropRenderer[f.StateProps.Length];
                    for (int k = 0; k < f.StateProps.Length; k++)
                    {
                        if (f.StateProps[k] == null) continue;
                        string name = f.StateNames != null && k < f.StateNames.Length
                            ? f.StateNames[k] : k.ToString();
                        built[k] = service.AttachWheel(
                            visual.gameObject, f.StateProps[k], f.Slot + "#" + name);
                        // Everything but the first is hidden until the driver's first pose, so a
                        // freshly skinned trailer does not flash both leg lengths at once.
                        if (built[k] != null) built[k].Visible = k == 0;
                    }
                    states.Add(built);
                    wheels.Add(built.Length > 0 ? built[0] : null);
                    continue;
                }

                states.Add(null);
                wheels.Add(f.Prop != null
                    ? service.AttachWheel(visual.gameObject, f.Prop, f.Slot)
                    : null);
            }

            // How open everything is. Configured before the driver so its first pose reads real
            // state rather than an empty array, and snapped shut so a placed machine arrives with
            // its doors closed rather than sweeping them open from nowhere.
            var doors = root.GetComponent<VehicleDoors>();
            if (doors == null) doors = root.AddComponent<VehicleDoors>();
            doors.Configure(mesh);
            doors.SnapAllShut();

            InstallHandles(root, visual, mesh, vehicleId, doors);

            // ⭐ A fifth wheel, for anything whose def publishes one — which is decided by the ART
            // (VehicleMeshDef.CanTow reads her plate), never by her kind. A machine that does not
            // tow never grows the component, so a bobtail box truck has no release handle to find.
            if (mesh.CanTow)
            {
                var hitch = root.GetComponent<VehicleHitch>();
                if (hitch == null) hitch = root.AddComponent<VehicleHitch>();
                hitch.Configure(mesh, controller != null ? controller
                                                        : root.GetComponent<VehicleController>(),
                                vehicleId);
            }

            var driver = root.GetComponent<VehicleMeshDriver>();
            if (driver == null) driver = root.AddComponent<VehicleMeshDriver>();
            driver.Configure(visual, renderer, mesh,
                             controller != null ? controller : root.GetComponent<VehicleController>(),
                             mesh.Wheels, wheels.ToArray(), states.ToArray(), doors);

            return new Rig(true, visual, driver, renderer);
        }

        /// <summary>Strip a vehicle's picture off <paramref name="root"/>. Safe when unskinned.</summary>
        public static void Remove(GameObject root)
        {
            if (root == null) return;

            Transform visual = root.transform.Find(VisualChildName);
            if (visual != null) VehicleMeshPresentation.Service?.Remove(visual.gameObject);

            var driver = root.GetComponent<VehicleMeshDriver>();
            if (driver != null) driver.Configure(null, null, null, null, null, null);
        }

        /// <summary>The child every handle lives under, so a re-skin can clear them in one go rather
        /// than hunting components off the root.</summary>
        private const string HandlesChildName = "Handles";

        /// <summary>
        /// Stand one <see cref="VehicleDoorHandle"/> at each worked opening the art published a place
        /// for.
        ///
        /// <para>⚠️ <b><c>drive</c> and <c>ride</c> are skipped deliberately.</b> They ARE the cab
        /// doors — the art hangs getting in off the leaf you open to do it — but the seat flow
        /// already owns that press (<see cref="VehicleDoor"/>, which is both the door and the seat).
        /// Registering a second interactable at the same point would put two candidates a few
        /// centimetres apart and let the resolver's tie-break decide whether pressing E opens the
        /// door or climbs in. The leaves are still POSED; what they do not get is a rival handle.</para>
        ///
        /// <para>⚠️ <b>A group with no reach point gets no handle</b>, and that is the art speaking
        /// rather than a gap: a trailer's cranks are published as per-body formulas and her
        /// <c>couple</c> point as prose, because the act belongs to the tractor. A handle at (0, 0)
        /// would sit on the machine's own centreline.</para>
        /// </summary>
        private static void InstallHandles(GameObject root, Transform visual, VehicleMeshDef mesh,
                                           string vehicleId, VehicleDoors doors)
        {
            Transform holder = root.transform.Find(HandlesChildName);
            if (holder != null) UnityEngine.Object.DestroyImmediate(holder.gameObject);
            if (mesh.DoorGroups == null || mesh.DoorGroups.Length == 0) return;

            var go = new GameObject(HandlesChildName);
            go.transform.SetParent(root.transform, false);
            holder = go.transform;

            for (int i = 0; i < mesh.DoorGroups.Length; i++)
            {
                VehicleDoorGroup group = mesh.DoorGroups[i];
                if (!group.HasReachPoint) continue;
                if (group.Id == "drive" || group.Id == "ride") continue;

                // ⚠️ Built INACTIVE and switched on after Configure. AddComponent on a live object
                // runs OnEnable — and so the registration — before the caller has said what this is;
                // the handle's own id is computed live to survive that, but registering a handle
                // that does not yet know its group would still offer a press that does nothing.
                var child = new GameObject(group.Id);
                child.SetActive(false);
                child.transform.SetParent(holder, false);

                var handle = child.AddComponent<VehicleDoorHandle>();
                handle.Configure(doors, group, vehicleId);
                child.SetActive(true);
            }
        }
    }
}
