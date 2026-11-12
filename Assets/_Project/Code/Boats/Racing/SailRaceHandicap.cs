using System;

namespace HiddenHarbours.Boats
{
    [Serializable]
    public struct SailRaceHandicap
    {
        public string HullId;
        public float TimeMultiplier;
        public SailRaceHandicap(string hullId, float timeMultiplier)
        {
            RaceGeometry.RequireId(hullId, "boat.");
            RaceGeometry.RequirePositive(timeMultiplier, nameof(timeMultiplier));
            HullId = hullId; TimeMultiplier = timeMultiplier;
        }
    }
}
