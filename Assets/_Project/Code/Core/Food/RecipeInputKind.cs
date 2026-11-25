namespace HiddenHarbours.Core
{
    /// <summary>
    /// What a <see cref="RecipeDef"/> takes in. <b>Append-only</b>: assets store the number, so a value is
    /// never renumbered or reused.
    /// </summary>
    public enum RecipeInputKind
    {
        /// <summary>One fish species, by its <c>fish.*</c> id (<see cref="CatchItem.SpeciesId"/>). The most
        /// specific match for a raw catch.</summary>
        Species = 0,

        /// <summary>Any catch of one <see cref="FishCategory"/>. The fallback for a raw catch no species
        /// recipe names.</summary>
        Category = 1,

        /// <summary>The output id of another recipe (a fillet from the prep station going to the cooktop).
        /// </summary>
        Intermediate = 2,

        /// <summary>Any stock item by its id (a <see cref="FoodDef.ItemId"/> or a supply id such as
        /// <c>supply.tinned_beans</c>), not another recipe's output: what a microwave heats. Not part of the
        /// raw-catch chain, so <see cref="RecipeBook.IsReachableFromCatch"/> never follows it.</summary>
        Item = 3,
    }
}
