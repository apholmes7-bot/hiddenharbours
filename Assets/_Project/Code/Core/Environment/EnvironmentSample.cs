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
        public readonly SeaState SeaState;      // STEPPED canon scale — gameplay gates + the HUD readout
        public readonly float   Visibility;     // 0 (thick fog) .. 1 (clear)

        /// <summary>
        /// The CONTINUOUS sea-state axis, 0 (glass) .. 1 (storm) — the smooth counterpart of
        /// <see cref="SeaState"/>. Equals <c>(int)SeaState / 7</c> exactly at every band edge and eases
        /// linearly between them (see <c>WeatherModel.SeaState01</c>), so PRESENTATION consumers (water
        /// chop/swell, palette mood, weather dim, sea mist, wake roughness) track the weather without the
        /// 1/7 pops the stepped enum produces. Gameplay gates (e.g. <c>MaxSafeSeaState</c>) and the HUD
        /// "Sea: Light (2/7)" readout keep reading the enum — stepping is intended there.
        /// </summary>
        public readonly float SeaState01;

        public EnvironmentSample(Vector2 windVector, Vector2 currentVector,
                                 float tideHeight, SeaState seaState, float visibility,
                                 float seaState01)
        {
            WindVector = windVector;
            CurrentVector = currentVector;
            TideHeight = tideHeight;
            SeaState = seaState;
            Visibility = visibility;
            SeaState01 = Mathf.Clamp01(seaState01);
        }

        /// <summary>
        /// Convenience constructor (kept for existing callers/tests): derives the continuous axis from
        /// the enum's normalised value (<c>(int)state / 7</c> — the band-edge value), so a sample built
        /// from just the enum behaves exactly as the pre-continuous-axis code did.
        /// </summary>
        public EnvironmentSample(Vector2 windVector, Vector2 currentVector,
                                 float tideHeight, SeaState seaState, float visibility)
            : this(windVector, currentVector, tideHeight, seaState, visibility,
                   (int)seaState / (float)SeaState.Storm)
        {
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
