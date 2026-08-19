using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised alongside <see cref="RestSaveRequested"/> when the player turns in — the PRESENTATION half
    /// of the beat, carrying the one thing the save half has no use for: <b>where the bed is</b>.
    ///
    /// <para><b>Why a second signal instead of a field on the first.</b> <see cref="RestSaveRequested"/>
    /// carries the WAKE position, which is deliberately the player's own feet and not the bed's
    /// transform (a bed's transform is the middle of its footprint, so waking there is waking in the
    /// mattress). The sleeping picture wants the opposite — the mattress itself. Those are two different
    /// quantities wanted by two different consumers, and bolting the second onto the save signal would
    /// invite exactly the confusion its own comment warns about. #580's signal, its constructor and its
    /// save semantics are untouched by this arc.</para>
    ///
    /// <para><b>Presentation only.</b> Nothing that writes a save, moves the clock, or decides whether a
    /// rest is allowed may read this. A consumer that does nothing at all (no sleep art baked, no
    /// presenter in the scene) is a legal outcome and leaves the beat exactly as it shipped in #580.</para>
    /// </summary>
    public readonly struct SleepBeatRequested
    {
        /// <summary>The mattress, world metres — the bed's own transform. The sleep sheet is baked lying
        /// on a 0.30 m mattress with its pivot at the bed's floor contact, so this is the point the pose
        /// is drawn at, NOT the player's standing spot.</summary>
        public readonly Vector2 BedPosition;

        /// <summary>Which interior storey the bed is on, mirrored from the rest request so a presenter
        /// can sort or gate on it without reaching for the bed.</summary>
        public readonly int Level;

        /// <summary>The bed's place name, for a notice or a debug line. May be empty.</summary>
        public readonly string Place;

        public SleepBeatRequested(Vector2 bedPosition, int level, string place)
        {
            BedPosition = bedPosition;
            Level = level;
            Place = place ?? string.Empty;
        }
    }

    /// <summary>
    /// Raised when the sleeping beat ends and the player has the renderer (and their own position) back.
    /// The bookend to <see cref="SleepBeatRequested"/>, so a consumer that dimmed, muted or hid
    /// something for the beat can restore it without running its own timer against the same duration —
    /// two timers on one beat is the same quantity computed twice.
    /// </summary>
    public readonly struct SleepBeatEnded
    {
        /// <summary>True when the beat ran to completion; false when it was cut short (the presenter was
        /// disabled, the region unloaded, another clip claimed the renderer).</summary>
        public readonly bool Completed;

        public SleepBeatEnded(bool completed) => Completed = completed;
    }
}
