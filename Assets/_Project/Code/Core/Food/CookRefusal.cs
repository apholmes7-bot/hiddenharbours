namespace HiddenHarbours.Core
{
    /// <summary>
    /// Why a cooking station did not start (<see cref="RecipeRunner"/>). A reason code, never text: the
    /// station's own lines (<see cref="CookingStationDef"/>) say it in the player's bubble. <b>Append-only.</b>
    /// </summary>
    public enum CookRefusal
    {
        /// <summary>It started.</summary>
        None = 0,
        /// <summary>No station data, or no recipes to run.</summary>
        NoStation = 1,
        /// <summary>Nothing in the hand or the locker that this station has a recipe for.</summary>
        NothingToCook = 2,
        /// <summary>This station is already cooking something.</summary>
        Busy = 3,
        /// <summary>A microwave was offered raw or unmicrowavable food (owner 2026-11-15: "you cant microwave
        /// a raw fish or raw food"). Nothing is taken.</summary>
        NotMicrowavable = 4,
        /// <summary>The catch in the hand must go to a prep station first (owner ruling 2026-11-14).</summary>
        NeedsPrep = 5,
    }
}
