using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A seat behind a wheel</b> — everything the Player lane needs in order to drive something, and
    /// nothing about what that something is (ADR 0035).
    ///
    /// <para><b>Why this interface exists at all.</b> <c>ControlSwitcher</c> owns the control-mode state
    /// machine and lives in Player; <c>VehicleController</c> owns the motion and lives in Vehicles; the two
    /// modules may not name each other's concrete types (rule 4), and Vehicles references Core only. So the
    /// wheel is handed over as a Core contract, exactly as <see cref="IBoardingLadder"/> hands over a set of
    /// rungs and <see cref="IMooringCleat"/> a place to make fast. The switcher drives an
    /// <c>IDriveSeat</c>; that it happens to be a one-tonne dually is the Vehicles lane's business.</para>
    ///
    /// <para><b>Read LIVE, never captured.</b> Every position below is a property rather than a field for
    /// the reason <see cref="IInteractable.WorldPosition"/> is one: the machine MOVES, and a door latched at
    /// the moment you pressed the key would be a door in the road behind her. The switcher re-reads them at
    /// the instant it uses them.</para>
    ///
    /// <para><b>⚠️ A seat is a <c>UnityEngine.Object</c> in practice, so it can be FAKE-null.</b> Holders of
    /// this interface must test the reference through <see cref="IsAlive"/> rather than <c>!= null</c>: the
    /// interface-typed <c>==</c> is plain reference equality and sails straight past a destroyed component,
    /// so a truck deleted under a driver would look perfectly alive right up until the next dereference
    /// threw. This is the same trap <c>GameServices.PlayerTransform</c> was built to close, and the drive
    /// mode reaches it the moment anything despawns a vehicle mid-drive.</para>
    /// </summary>
    public interface IDriveSeat
    {
        /// <summary>Stable <c>vehicle.*</c> id of the machine — carried onto
        /// <see cref="ActiveVehicleChanged"/> so the camera can frame her without naming her type.</summary>
        string VehicleId { get; }

        /// <summary>Can she actually be driven right now? False for scenery (a truck parked as dressing,
        /// with no controller), for a def with no usable mesh — the wheelbase and lock angles live there,
        /// so a missing one would leave the drive model dividing by a default — and for a machine some
        /// other rule has taken out of service. A refusal here is the cozy "not this one" (P5), not an
        /// error.</summary>
        bool IsDrivable { get; }

        /// <summary>Her physics root — the transform the driver rides while aboard, and the one the whole
        /// fleet's heading convention is written against (<c>transform.up</c> is the nose).</summary>
        Transform Root { get; }

        /// <summary>How much world height the camera shows while driving her
        /// (<c>VehicleDef.CameraWorldHeightMeters</c>).</summary>
        float CameraWorldHeightMeters { get; }

        /// <summary>
        /// <b>The driver's door, in world metres</b> — where you must stand to get in, and where you are
        /// put down when you get out.
        ///
        /// <para><b>One point serves both ends deliberately.</b> The rig's sidecar publishes a single
        /// <c>drive</c> reach point for this door, measured to sit outside the leaf's swept disc; getting
        /// in and getting out at the same spot means the two can never drift apart, and it is also what a
        /// person does. It travels with her, so you step out wherever she has been driven to, not where you
        /// got in.</para>
        /// </summary>
        Vector2 DoorWorldPosition { get; }

        /// <summary>True while the underlying object is really alive — see the fake-null warning on the
        /// interface. Implementors return the Unity-null-aware answer for themselves; holders must never
        /// hand-roll it, because the honest test is not one an interface-typed <c>==</c> can express.</summary>
        bool IsAlive { get; }

        /// <summary>Take the wheel: zero the controls and make her ready to be driven. Idempotent.</summary>
        void TakeControls();

        /// <summary>
        /// One tick of the driver's demand. <paramref name="throttle"/> is −1 (full astern) … +1 (full
        /// ahead) and <paramref name="steer"/> is −1 (full right) … +1 (full left) — the wheel POSITION is
        /// the machine's own business and moves toward this at her steering rate, so the picture and the
        /// yaw stay solved from one number (ADR 0035 §5).
        /// </summary>
        void SetDriveInput(float throttle, float steer, bool brake);

        /// <summary>Give the wheel up: drop the demand and bring her to rest, so a truck stepped out of
        /// does not coast away across the park. The mirror of <see cref="TakeControls"/>, and the twin of
        /// what leaving a helm does to a boat's throttle.</summary>
        void ReleaseControls();
    }
}
