namespace HiddenHarbours.Core
{
    /// <summary>
    /// Why a thing was not put away. <see cref="None"/> means it may be. A reason code with no text, the
    /// house pattern (<see cref="EatRefusal"/>): the caller words it, and on every refusal NOTHING has
    /// moved — the item stays in the hand, or wherever it legally was.
    /// </summary>
    public enum StowRefusal
    {
        /// <summary>It may be stowed.</summary>
        None = 0,

        /// <summary>Nothing in hand that could be put away.</summary>
        NothingToStow,

        /// <summary>No container to put it in (an unwired fixture, a destroyed object).</summary>
        NoContainer,

        /// <summary>This container does not take this kind of thing: a rod into a fish hold, a pail into
        /// a rod rack.</summary>
        WrongContainer,

        /// <summary>The container takes this kind of thing but has no room left.</summary>
        Full,
    }
}
