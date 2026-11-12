using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    [Serializable]
    public struct SailRaceClubRules
    {
        public float TurnDegrees, MinimumTurnSeconds, MaximumHeadingStepDegrees;
        public SailRaceClubRules(float turnDegrees, float minimumTurnSeconds, float maximumHeadingStepDegrees)
        {
            RaceGeometry.RequirePositive(turnDegrees, nameof(turnDegrees));
            RaceGeometry.RequirePositive(minimumTurnSeconds, nameof(minimumTurnSeconds));
            RaceGeometry.RequirePositive(maximumHeadingStepDegrees, nameof(maximumHeadingStepDegrees));
            if (turnDegrees < 360f || maximumHeadingStepDegrees >= 180f)
                throw new ArgumentException("A turn needs a full rotation; samples must resolve its direction.");
            TurnDegrees = turnDegrees; MinimumTurnSeconds = minimumTurnSeconds;
            MaximumHeadingStepDegrees = maximumHeadingStepDegrees;
        }
        internal void Validate() => _ = new SailRaceClubRules(TurnDegrees, MinimumTurnSeconds, MaximumHeadingStepDegrees);
    }
}
