namespace HiddenHarbours.Core
{
    /// <summary>
    /// A kitchen station started cooking (<see cref="RecipeRunner.TryStart"/>): one input was taken. For the
    /// station's sound and look, and the player's bubble; nothing is required to consume it.
    /// </summary>
    public readonly struct FoodCookStarted
    {
        /// <summary>The <see cref="CookingStationDef.Id"/> (<c>cooking_station.stove</c>).</summary>
        public readonly string StationId;
        /// <summary>Which placed station (its interactable id).</summary>
        public readonly string StationKey;
        /// <summary>The <see cref="RecipeDef.Id"/> running.</summary>
        public readonly string RecipeId;
        /// <summary>The item one unit of which was used up.</summary>
        public readonly string InputItemId;
        /// <summary>The game time it is done at (<see cref="IGameClock.TotalSeconds"/>).</summary>
        public readonly double ReadyAtGameSeconds;

        public FoodCookStarted(string stationId, string stationKey, string recipeId, string inputItemId, double readyAt)
        {
            StationId = stationId; StationKey = stationKey; RecipeId = recipeId; InputItemId = inputItemId;
            ReadyAtGameSeconds = readyAt;
        }
    }

    /// <summary>
    /// A kitchen station finished (<see cref="RecipeRunner.TryFinish"/>): one <see cref="OutputId"/> went
    /// into the supply locker, where the eat path finds it.
    /// </summary>
    public readonly struct FoodCooked
    {
        public readonly string StationId;
        public readonly string StationKey;
        public readonly string RecipeId;
        /// <summary>The item added to the supply locker (<c>food.pan_fried_cod</c>).</summary>
        public readonly string OutputId;

        public FoodCooked(string stationId, string stationKey, string recipeId, string outputId)
        {
            StationId = stationId; StationKey = stationKey; RecipeId = recipeId; OutputId = outputId;
        }
    }

    /// <summary>A kitchen station did not start. A reason code, never text; nothing was taken.</summary>
    public readonly struct CookRefused
    {
        /// <summary>The station kind's id, or null when there was no station data.</summary>
        public readonly string StationId;
        public readonly string StationKey;
        /// <summary>Why. Never <see cref="CookRefusal.None"/>.</summary>
        public readonly CookRefusal Reason;

        public CookRefused(string stationId, string stationKey, CookRefusal reason)
        {
            StationId = stationId; StationKey = stationKey; Reason = reason;
        }
    }
}
