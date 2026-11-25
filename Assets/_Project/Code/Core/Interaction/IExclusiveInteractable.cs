namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A thing one body uses at a time</b>: a bed, a bunk (owner ask 2026-11-15: "a bunk occupied is
    /// unable to be selected at that time, or a nearby bunk will automatically be selected if the player
    /// is in the selectable area").
    ///
    /// <para><b>Optional, beside <see cref="IInteractable"/>.</b> A registrant that implements this is
    /// asked two things by <see cref="InteractChoiceList"/>: is someone in it, and which family of
    /// interchangeable things it belongs to. An occupied one is never chosen; when a free member of the
    /// same <see cref="ExclusiveGroup"/> is also in reach, the free one is offered in its place, so the
    /// player standing between two bunks is simply given the empty one. With N members in reach the rule
    /// is the same: the best free member by <see cref="InteractResolver"/>'s ladder, else the best
    /// occupied one, shown and refused.</para>
    ///
    /// <para><b>Occupancy is not stored here.</b> For a bed or bunk the answer is
    /// <see cref="RestQuorum.IsOccupied"/> keyed on the bed id the rest request already carries; this
    /// interface is only the question, so the chooser can ask it without naming Boats or World
    /// (rule 4).</para>
    /// </summary>
    public interface IExclusiveInteractable
    {
        /// <summary>Which family of interchangeable things this is (<see cref="InteractExclusiveGroups"/>).
        /// Null or empty means "interchangeable with nothing": occupancy still disables it, but no other
        /// candidate is offered in its place.</summary>
        string ExclusiveGroup { get; }

        /// <summary>True while somebody else is using it. Read live, at the moment the list is built and
        /// again at the moment a choice is confirmed.</summary>
        bool IsOccupied { get; }
    }

    /// <summary>The stable group keys (<c>group.snake_case</c>). Append-only.</summary>
    public static class InteractExclusiveGroups
    {
        /// <summary>Somewhere to sleep: a bed ashore, a bunk aboard. Any free one will do.</summary>
        public const string Rest = "group.rest";
    }
}
