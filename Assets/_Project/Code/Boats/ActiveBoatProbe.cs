using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The Boats-lane producer for the Core <see cref="IActiveBoatService"/> heading seam (ADR 0007).
    /// It reads the active <see cref="BoatController"/>'s bow facing and rigidbody velocity and exposes
    /// them as a Core <see cref="BoatKinematics"/> snapshot, so the HUD (HiddenHarbours.UI, which only
    /// references Core) can build the VS-19 compass, set-&amp;-drift predictor, and apparent-wind read
    /// without ever referencing this module.
    ///
    /// <para><b>Scene-scoped, self-registering.</b> Unlike the clock/environment (persistent boot
    /// singletons wired on GameRoot), the active boat is a per-scene, per-hull runtime thing. So this
    /// probe self-registers into <see cref="GameServices.ActiveBoat"/> on enable and clears it on
    /// disable — the same Core-mediated, one-way handoff the Boats/Player lane already uses to publish
    /// <see cref="ActiveBoatChanged"/>. It only ever touches its own module + Core, so it does not
    /// usurp GameRoot's role as the cross-module composition root (project-structure §5).</para>
    ///
    /// <para><b>Heading semantics (the rowed dory).</b> Heading is the bearing of the bow
    /// (<c>transform.up</c>) — the hull's facing — which is well-defined no matter whether the
    /// differential oars (PR #26) or a rudder turned it; velocity is the rigidbody's course-over-ground
    /// (wind- and current-set included). Both are already <see cref="BoatController"/>'s public surface
    /// (<see cref="BoatController.Velocity"/> + the documented bow = <c>transform.up</c>), so this seam
    /// adds no new physics meaning.</para>
    /// </summary>
    public sealed class ActiveBoatProbe : MonoBehaviour, IActiveBoatService
    {
        [Tooltip("The boat whose heading + velocity to report. In the greybox this is the one persistent " +
                 "dory whose hull OwnedFleet swaps on a purchase; a later multi-boat setup re-points this.")]
        [SerializeField] private BoatController _boat;

        /// <summary>True only when a boat is present AND under control (its controller is enabled) —
        /// the ControlSwitcher enables the controller on boarding and disables it when moored / on
        /// foot, so this naturally reads false ashore.</summary>
        public bool HasActiveBoat => _boat != null && _boat.isActiveAndEnabled;

        /// <inheritdoc/>
        public BoatKinematics Sample()
            => HasActiveBoat ? BoatKinematics.FromBow(_boat.transform.up, _boat.Velocity)
                             : BoatKinematics.None;

        /// <summary>Wire the probe in one call (tests / editor builder), mirroring the Configure
        /// pattern used by OwnedFleet / ControlSwitcher.</summary>
        public void Configure(BoatController boat) => _boat = boat;

        // Register/clear the Core slot with this component's enable lifetime (scene-scoped service).
        private void OnEnable() => GameServices.ActiveBoat = this;

        private void OnDisable()
        {
            if (ReferenceEquals(GameServices.ActiveBoat, this))
                GameServices.ActiveBoat = null;
        }
    }
}
