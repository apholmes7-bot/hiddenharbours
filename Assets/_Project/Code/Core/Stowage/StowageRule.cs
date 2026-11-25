namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE storage-acceptance rule, once</b> — may this item go in this container right now? Every stow
    /// path asks this and nothing else: the catch into a hold (<c>CarryHands.TryGiveCatchTo</c>) and a rod
    /// into a rack (<c>StowageRack</c>).
    ///
    /// <para><b>Both halves are data.</b> The item's <see cref="StowageClass"/> is on its Def, the
    /// container's <see cref="StowageAccepts"/> and capacity are on its Def. Nothing here names a rod or a
    /// rack by id, so a gear locker that also takes rods is a new asset with
    /// <c>General | Rods</c>, not a code change.</para>
    ///
    /// <para><b>Order of the checks is the useful sentence first.</b> "That does not go there" beats
    /// "it is full": a full fish hold is not a reason a rod would fit in it.</para>
    ///
    /// <para>Pure and allocation-free: no state, no clock, no roll (rule 5).</para>
    /// </summary>
    public static class StowageRule
    {
        /// <summary>
        /// What every container that predates this rule takes: the holds, the pails, the coolers, the
        /// freezer. General only — which is exactly why a rod can never go in one.
        /// </summary>
        public const StowageAccepts ExistingContainer = StowageAccepts.General;

        /// <summary>The flag a container needs to take an item of this class.</summary>
        public static StowageAccepts FlagFor(StowageClass item) => item switch
        {
            StowageClass.Rod => StowageAccepts.Rods,
            _ => StowageAccepts.General,
        };

        /// <summary>Does a container that takes <paramref name="accepts"/> take this kind of item at all
        /// (capacity aside)?</summary>
        public static bool Accepts(StowageAccepts accepts, StowageClass item)
            => (accepts & FlagFor(item)) != 0;

        /// <summary>
        /// May one <paramref name="item"/> go into a container that takes <paramref name="accepts"/>,
        /// holding <paramref name="used"/> of <paramref name="capacity"/>? A capacity of zero or less is
        /// a container with no room, never an unlimited one.
        /// </summary>
        public static StowRefusal Check(StowageClass item, StowageAccepts accepts, int used, int capacity)
        {
            if (!Accepts(accepts, item)) return StowRefusal.WrongContainer;
            if (used >= capacity) return StowRefusal.Full;
            return StowRefusal.None;
        }
    }
}
