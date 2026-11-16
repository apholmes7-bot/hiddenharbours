namespace HiddenHarbours.Core
{
    /// <summary>
    /// Why an eat did not happen: a <b>reason code</b>, never a sentence. Whoever reacts (a speech bubble,
    /// a shrug, a villager's remark) owns the words, so logic carries no player-facing text and the ruling's
    /// "diegetic only" holds. <b>Append-only</b>: a value is never renumbered or reused.
    /// </summary>
    public enum EatRefusal
    {
        /// <summary>It was eaten.</summary>
        None = 0,

        /// <summary>Nothing to eat: empty hands, or none of that item in the locker.</summary>
        NothingToEat = 1,

        /// <summary>The item is not food at all (no <see cref="FoodDef"/> describes it).</summary>
        NotFood = 2,

        /// <summary>It is food, but not as it is: it wants the prep station and the cooktop first (a raw
        /// cod).</summary>
        NotEdibleRaw = 3,
    }
}
