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

        /// <summary>
        /// ⭐ <b>WHICH WAY ROUND THE BED IS</b> — the bed's own declaration of the end its pillow is on,
        /// plus the frame that makes it a direction. <see cref="PillowAnchor.None"/> when the bed never
        /// said, under which a presenter keeps whatever heading it already had.
        ///
        /// <para><b>Why this rides the beat rather than being worked out by whoever draws.</b> A pose is
        /// the only consumer that can be WRONG about it, and the failure draws perfectly: a sleeper laid
        /// out along a bed the other way up is a picture, not an exception, and the first person to notice
        /// is the owner looking at their own bedroom. The bed knows the answer for nothing — content
        /// declared it — so it is carried, exactly as the door anchor is carried rather than measured off
        /// a wall.</para>
        ///
        /// <para><b>Still presentation only.</b> Nothing about the SAVE turns on this: the anchor written
        /// by <see cref="RestSaveResponder"/> is a region, a storey and the player's own feet, and none of
        /// the three moves because a pillow is at one end or the other. A pillow is CONTENT — recomputed
        /// from the bed's declaration every time the room is stood — so it is not save state and there is
        /// no schema step here (ADR 0037, CLAUDE.md rule 5).</para>
        /// </summary>
        public readonly PillowAnchor Pillow;

        public SleepBeatRequested(Vector2 bedPosition, int level, string place)
            : this(bedPosition, level, place, PillowAnchor.None)
        {
        }

        public SleepBeatRequested(Vector2 bedPosition, int level, string place, PillowAnchor pillow)
        {
            BedPosition = bedPosition;
            Level = level;
            Place = place ?? string.Empty;
            Pillow = pillow;
        }

        /// <summary>
        /// Where the sleeper's HEAD lands, world units — on the pillow for a declared bed, and in the
        /// middle of the mattress for one that never said. One definition, read by the pose and by the
        /// content test alike, so "the head is on the pillow side" cannot mean two things.
        /// </summary>
        public Vector2 HeadPosition => Pillow.HeadWorld(BedPosition);
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
