namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which storey of a building the player is on</b>: 0 the ground floor and everywhere outdoors, 1 the
    /// floor above. The building he is inside reports it (World's <c>BuildingInterior</c>, on every change);
    /// a reader in another module (a thing set down on a floor, which must not be seen or picked up from the
    /// floor below) asks here and never names the World class (rule 4). The same number as
    /// <see cref="SaveData.RestLevel"/>. Not saved: it follows from where he stands.
    /// </summary>
    public static class PlayerStorey
    {
        private static object _building;

        /// <summary>The storey he is on now.</summary>
        public static int Level { get; private set; }

        /// <summary>A building says whether he is inside it and on which storey. Stepping out of the
        /// building that last reported him inside puts him back on the ground; a building he was never
        /// reported inside changes nothing by saying he is outside it.</summary>
        public static void Report(object building, bool inside, int level)
        {
            if (building == null) return;
            if (inside)
            {
                _building = building;
                Level = level > 0 ? level : 0;
            }
            else if (ReferenceEquals(_building, building))
            {
                _building = null;
                Level = 0;
            }
        }

        /// <summary>Back to the ground (the services were torn down, a test).</summary>
        public static void Clear()
        {
            _building = null;
            Level = 0;
        }
    }
}