using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A snapshot of the sea's "mood" at a place and time (Pillar 1). Produced by the
    /// environment service each physics tick and consumed by boat physics
    /// (docs/architecture/tech-architecture.md §4, design/time-tides-weather.md).
    /// </summary>
    public readonly struct EnvironmentSample
    {
        public readonly Vector2 WindVector;     // direction * strength, m/s
        public readonly Vector2 CurrentVector;  // tidal current "set", m/s
        public readonly float   TideHeight;     // metres relative to chart datum
        public readonly SeaState SeaState;
        public readonly float   Visibility;     // 0 (thick fog) .. 1 (clear)

        public EnvironmentSample(Vector2 windVector, Vector2 currentVector,
                                 float tideHeight, SeaState seaState, float visibility)
        {
            WindVector = windVector;
            CurrentVector = currentVector;
            TideHeight = tideHeight;
            SeaState = seaState;
            Visibility = visibility;
        }
    }

    /// <summary>
    /// Per-region tide shape. Regions carry their own profile so the same clock produces
    /// gentle water in Coddle Cove and enormous swings in the Fundy Rips. Authored as data
    /// on a RegionDef later; defaults provided here for the greybox.
    /// </summary>
    [System.Serializable]
    public struct TideProfile
    {
        [Tooltip("Mean water level (metres rel. datum).")]
        public float MeanLevel;
        [Tooltip("Half the range between high and low water at spring tide (metres).")]
        public float Amplitude;
        [Tooltip("Phase offset of high water, in hours, so regions don't all peak together.")]
        public float PhaseHours;

        public static TideProfile CoddleCove => new TideProfile { MeanLevel = 0f, Amplitude = 1.6f, PhaseHours = 0f };
        public static TideProfile FundyRips  => new TideProfile { MeanLevel = 0f, Amplitude = 7.5f, PhaseHours = 1.2f };
    }
}
