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

            IVehicleMeshPresentationService service = VehicleMeshPresentation.Service;
            if (service == null) return default;

            VehicleMeshDef mesh = vehicle.Mesh;

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
            for (int i = 0; i < mesh.Wheels.Length; i++)
            {
                VehicleFitment f = mesh.Wheels[i];
                wheels.Add(f.Prop != null
                    ? service.AttachWheel(visual.gameObject, f.Prop, f.Slot)
                    : null);
            }

            var driver = root.GetComponent<VehicleMeshDriver>();
            if (driver == null) driver = root.AddComponent<VehicleMeshDriver>();
            driver.Configure(visual, renderer, mesh,
                             controller != null ? controller : root.GetComponent<VehicleController>(),
                             mesh.Wheels, wheels.ToArray());

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
    }
}
