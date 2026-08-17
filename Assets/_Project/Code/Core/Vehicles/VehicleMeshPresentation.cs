using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Installs / removes the Art-side facet renderer for a ROAD VEHICLE (ADR 0035).</b> The
    /// sibling of <see cref="IHullMeshPresentationService"/>, and a separate interface on purpose.
    ///
    /// <para><b>Why not just another method on the hull service.</b> Two reasons, and the second is
    /// the real one. It would break every test double that implements the hull interface, for a
    /// method none of them care about — but more importantly, installing a HULL does three things a
    /// truck must not do: it bolts on a <c>FoamInjector</c> so she churns the wake-foam buffer, it
    /// makes her a <c>ReflectiveObject</c> so she appears in the water, and it hands the renderer a
    /// waterline clamp. A road vehicle that reflected in the harbour and laid a foam ribbon down
    /// Wharf Road would be a defect that no test asks about, because no test would think to. A
    /// separate entry point cannot make that mistake.</para>
    ///
    /// <para><b>The POSE seam is shared, though</b> — <see cref="IHullMeshRenderer"/> is handed back
    /// here. That interface is misnamed rather than boat-specific: its four channels are heading in
    /// rig dir units, roll, pitch and heave, which is exactly what a truck needs (heading from the
    /// controller, roll and pitch from her suspension, heave from her ride). Duplicating it as
    /// <c>IVehicleMeshRenderer</c> would fork the Art renderer to gain nothing. See ADR 0035.</para>
    /// </summary>
    public interface IVehicleMeshPresentationService
    {
        /// <summary>
        /// Install (or re-configure in place) a vehicle-mesh renderer on <paramref name="host"/>.
        /// Returns null — with a logged reason — when the def is unusable, so the caller can refuse
        /// to place her rather than field an invisible truck.
        /// </summary>
        IHullMeshRenderer Install(GameObject host, VehicleMeshDef def);

        /// <summary>
        /// Bolt one articulated fitting — a wheel, a steering knuckle — onto a vehicle already
        /// installed on <paramref name="host"/>. <paramref name="slot"/> names the instance
        /// ("WheelFL"), so a re-install reconfigures in place rather than accumulating wheels.
        /// </summary>
        IHullPropRenderer AttachWheel(GameObject host, HullPropMeshDef def, string slot);

        /// <summary>Remove a previously installed vehicle renderer (and its wheels) from
        /// <paramref name="host"/>. Safe when none is present.</summary>
        void Remove(GameObject host);
    }

    /// <summary>
    /// The service locator for <see cref="IVehicleMeshPresentationService"/>. Deliberately NOT a
    /// <c>GameServices</c> member, for the same reason <see cref="HullMeshPresentation"/> is not:
    /// <c>GameServices.Reset()</c> clears game-STATE services between tests and scenes, and this is
    /// stateless presentation wiring that must survive those resets.
    ///
    /// <para>⚠️ Art self-registers at runtime load and edit-time scene BUILDERS deliberately do not,
    /// so a built scene serialises no renderer and the mesh path is chosen live, per run. A builder
    /// that installed its own would bake the fallback into the committed scene, where it draws a
    /// perfectly good truck in the right place and nothing warns
    /// (memory: <c>mesh-hulls-must-skin-at-runtime</c>). <see cref="ParkedVehicle"/> is the shape
    /// that works: the builder places a GameObject carrying a Def, and it skins itself on enable.</para>
    /// </summary>
    public static class VehicleMeshPresentation
    {
        public static IVehicleMeshPresentationService Service { get; set; }
    }
}
