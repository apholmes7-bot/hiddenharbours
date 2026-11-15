namespace HiddenHarbours.Core
{
    /// <summary>
    /// The station ids the shipped cooking recipes name. A station is an <b>open set of stable string ids</b>
    /// (<c>station.snake_case</c>), not an enum, so a later facility (<c>station.salt_house</c>,
    /// <c>station.smokehouse</c>) is a new id in a new recipe asset with no code change. These two exist
    /// because the owner's two-step cooking ruling (2026-11-14: "Food needs to be prepped at a prep station
    /// first, cut/prepared then cooked on a cooktop") is checked by the content tests.
    /// </summary>
    public static class RecipeStations
    {
        /// <summary>The cutting board: raw catch in, a prepared cut out. Never feeds the player.</summary>
        public const string Prep = "station.prep";

        /// <summary>The kitchenette's cooktop: a prepared cut in, a cooked dish out.</summary>
        public const string Cooktop = "station.cooktop";
    }
}
