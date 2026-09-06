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
        ///
        /// <para>⚠️ On a machine you sit ASTRIDE this is the PREFERRED side, not the only one — see
        /// <see cref="AltDoorWorldPosition"/>.</para>
        /// </summary>
        Vector2 DoorWorldPosition { get; }

        /// <summary>
        /// ⭐⭐ <b>The OTHER side you can get on from, and whether she has one.</b>
        ///
        /// <para><b>A cab has one driver's door and the question does not arise</b> — every truck in the
        /// fleet and the Otter answer false here, and nothing about them changes. A machine you sit
        /// ASTRIDE has two, and the two are not equivalent: the enduro's side stand is on the STREET side,
        /// which is why her own sidecar says <c>mount.preferred: "street"</c> and a rider swings a leg
        /// over the stand; the quad's says <c>"either"</c>.</para>
        ///
        /// <para>⚠️⚠️ <b>Without this, the published curb side is UNREACHABLE — measured, not argued.</b>
        /// A door is one <see cref="IInteractable"/> at one point with one reach, and the ATV pack's two
        /// reach points are <b>1.96 m</b> apart on the enduro, <b>2.30 m</b> on the trike and
        /// <b>2.38 m</b> on the quad — every one of them past <c>VehicleDoor</c>'s 1.5 m. So a rider
        /// standing exactly where the art says she may mount was refused, silently, on the side the art
        /// prefers for two of the three machines. Widening the reach instead would have been the wrong
        /// fix twice over: it would let her mount from over the nose as well, and it would state a reach
        /// the art never published.</para>
        ///
        /// <para>Live, like everything else here. Meaningless when <see cref="HasAltDoor"/> is false.</para>
        /// </summary>
        bool HasAltDoor { get; }

        /// <inheritdoc cref="HasAltDoor"/>
        Vector2 AltDoorWorldPosition { get; }

        /// <summary>
        /// <b>Is her driver drawn?</b> An art fact, not a rule: a machine whose rig publishes a seat in
        /// the OPEN — the Otter's cockpit is "an open tub with two benches, not a room" — has somewhere
        /// a person is visibly sat, and a figure at her wheel is genuinely on screen. A hard-cab machine
        /// publishes none, and her driver stays hidden exactly as every driver was before this existed.
        ///
        /// <para>Deliberately NOT "does she have a seat": the Dually has three, under a roof panel and
        /// behind glass that is opaque at 32 px/m, and still answers false. False makes the two members
        /// below meaningless and a caller must not read them.</para>
        /// </summary>
        bool ShowsDriver { get; }

        /// <summary>
        /// <b>The GROUND point of the driver's seat, in world metres</b> — where a figure sitting in her
        /// stands on the map. Live, like <see cref="DoorWorldPosition"/> and for the same reason: she
        /// moves, and a seat latched at the moment you got in would be a seat back in the yard.
        ///
        /// <para>Meaningless when <see cref="ShowsDriver"/> is false.</para>
        /// </summary>
        Vector2 DriverSeatWorldPosition { get; }

        /// <summary>
        /// <b>How high that seat's cushion sits above her own ground plane, in metres.</b> Kept apart
        /// from the point above because it is a different KIND of quantity: the point is a place on the
        /// map and swings as she turns, while a height never does. Folding the two into one Vector3
        /// would invite precisely the mistake of rotating a height.
        ///
        /// <para>Meaningless when <see cref="ShowsDriver"/> is false.</para>
        /// </summary>
        float DriverSeatHeightMeters { get; }

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

    /// <summary>
    /// ⭐ <b>Which side you are put down on</b> — one rule, in one place, so the switcher and the tests
    /// cannot answer it differently.
    ///
    /// <para><b>The nearest one, and on a cab that is the only one.</b> A machine with no alternate side
    /// returns her single door unchanged, so every truck in the fleet and the Otter are byte-for-byte what
    /// they were. A machine you sit astride sets you down on the side you are actually over — which after
    /// a ride is the side you steered her from, and is what a person does.</para>
    ///
    /// <para>⚠️ <b>Ties go to the PREFERRED side</b>, which is the art's own <c>mount.preferred</c>: the
    /// enduro's stand is on the street side and a rider steps off over it. The comparison is
    /// <c>&lt;</c> rather than <c>&lt;=</c>, so a rider exactly between them steps off the way the art
    /// says, deterministically, rather than on whichever float happened to be smaller.</para>
    /// </summary>
    public static class DriveSeatSides
    {
        /// <summary>The door nearest <paramref name="from"/>, or the preferred one on a machine with a
        /// single way on. Reads the seat LIVE, so it is the door where she is NOW.</summary>
        public static Vector2 NearestDoor(IDriveSeat seat, Vector2 from)
        {
            if (seat == null) return from;
            Vector2 preferred = seat.DoorWorldPosition;
            if (!seat.HasAltDoor) return preferred;

            Vector2 alt = seat.AltDoorWorldPosition;
            return (alt - from).sqrMagnitude < (preferred - from).sqrMagnitude ? alt : preferred;
        }
    }
}
