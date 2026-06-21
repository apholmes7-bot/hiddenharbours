namespace HiddenHarbours.Core
{
    /// <summary>
    /// Reports the active boat's heading and course-over-ground through Core, so UI (and anything
    /// else) can read how the boat is moving WITHOUT referencing the Boats module — the same one-way,
    /// Core-mediated handoff the camera uses via <see cref="ActiveBoatChanged"/> (project-structure
    /// §5; CLAUDE.md rule 4). Implemented in the Boats lane (ActiveBoatProbe), consumed here.
    ///
    /// <para><b>Pull, not push.</b> This is sampled on demand (like <see cref="IEnvironmentService"/>),
    /// not pushed each physics tick — the HUD already polls the environment at ~4 Hz, so the boat
    /// read joins that cadence and the sim avoids per-tick event spam (ADR 0007).</para>
    /// </summary>
    public interface IActiveBoatService
    {
        /// <summary>True when there is a controllable active boat (aboard). False on foot / when
        /// moored, so a consumer can hide a heading read rather than show a stale one.</summary>
        bool HasActiveBoat { get; }

        /// <summary>Snapshot the active boat's heading + course-over-ground this instant. When there
        /// is no active boat, returns <see cref="BoatKinematics.None"/> (HasBoat == false).</summary>
        BoatKinematics Sample();
    }
}
