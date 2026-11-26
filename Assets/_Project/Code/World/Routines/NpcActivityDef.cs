using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What a villager DOES at a station</b>, as data (ADR 0003): the thing a routine block can name in
    /// <see cref="RoutineEntry.Doing"/> beside its presentation tag. One asset per activity, keyed by a stable,
    /// append-only <see cref="Id"/> (<c>npc_activity.snake_case</c>).
    ///
    /// <para>Abstract on purpose: each kind of doing carries its own data (an angler's rod and session, say)
    /// and is read by the system that draws it. A block that names none is exactly the block it always was.
    /// Nothing here is saved or ticked (rule 5).</para>
    /// </summary>
    public abstract class NpcActivityDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (npc_activity.snake_case, e.g. npc_activity.wharf_rod). " +
                 "Content-validated for uniqueness.")]
        public string Id = "npc_activity.example";

        /// <summary>The id prefix every activity id carries.</summary>
        public const string IdPrefix = "npc_activity.";

        /// <summary>
        /// True when this activity can be done at a station with these properties. The base answer is
        /// "anywhere"; a subclass narrows it (an angler needs open air). Content validation asks it of every
        /// block that names this activity.
        /// </summary>
        public virtual bool ValidAt(bool stationIndoors, out string why)
        {
            why = null;
            return true;
        }

        /// <summary>
        /// Where this activity puts him instead of his routine station, when it brings its own stand (an
        /// angler's plank at the deck edge). The base answer is "none": the station stands.
        /// </summary>
        public virtual bool TryGetStand(out HiddenHarbours.Core.ShoreStand stand)
        {
            stand = default;
            return false;
        }
    }
}
