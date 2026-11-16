using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What a wash station is (cleanliness slice 1). Append-only, explicit values: a
    /// <see cref="WashStationDef"/> asset stores the integer.
    /// </summary>
    public enum WashStationKind
    {
        /// <summary>The sea itself: rinses to a low base and salts a clean player back down to it.</summary>
        Sea = 0,

        /// <summary>A sink or basin: a basic wash, slower, and it cannot get you fully clean.</summary>
        Sink = 1,

        /// <summary>A shower: fast, all the way clean.</summary>
        Shower = 2,

        /// <summary>A bath (or a tub): all the way clean, a little slower than a shower.</summary>
        Bath = 3,

        /// <summary>A toilet: a SEAT with no function (owner: "can be seated on but has no function").</summary>
        Toilet = 4,
    }

    /// <summary>
    /// <b>ONE KIND OF WASH STATION, AS DATA</b> (cleanliness slice 1, owner ask 2026-11-15: showers and baths
    /// as stations in boats and buildings, sinks for a basic wash that cannot fully clean, the sea only to a
    /// base level, and a toilet you can sit on). One asset per kind under
    /// <c>Data/Resources/WashStations/</c>, id <c>washstation.snake_case</c>.
    ///
    /// <para><b>Location-agnostic.</b> A station in the world is just one of these ids plus an anchor
    /// (where to stand or sit), whatever it stands in: a boat's head, a home's bathroom, a shop's back room,
    /// an outhouse. The washing itself is <see cref="ICleanlinessService.BeginWash"/> /
    /// <see cref="ICleanlinessService.EndWash"/> and knows nothing about where.</para>
    ///
    /// <para><b>The three ceilings</b> are strictly ordered sea &lt; sink &lt; shower/bath, and a test pins
    /// it on the shipped assets (<c>WashStationContentTests</c>).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Wash Station", fileName = "WashStation")]
    public sealed class WashStationDef : ScriptableObject
    {
        /// <summary>The verb an anchor carries to be a wash spot (stand here and wash).</summary>
        public const string ActionWash = "wash";

        /// <summary>The verb an anchor carries to be a seat. Spelt exactly as the galley lane's
        /// <c>BoatInteriorDef.ActionSit</c> ("sit") so a toilet anchor and a dining seat share one verb at
        /// merge; the toilet is told apart by its station id (<see cref="WashStationKind.Toilet"/>).</summary>
        public const string ActionSit = "sit";

        [Tooltip("Stable id, append-only (washstation.snake_case, e.g. washstation.sink).")]
        public string Id = "washstation.example";

        [Tooltip("Shown name (never a number or a bar).")]
        public string DisplayName = "";

        [Tooltip("What this station is.")]
        public WashStationKind Kind = WashStationKind.Sink;

        [Tooltip("Dirt taken off per IN-GAME hour while washing (0..1 scale; 1 is filthy to clean). 0 for a " +
                 "toilet. The sea uses the same rate to salt a cleaner player down to its base.")]
        [Min(0f)] public float CleanRatePerGameHour = 1f;

        [Tooltip("How clean this station can get you, 0..1 (1 = fully clean). Washing never goes past it. " +
                 "Must be strictly ordered sea < sink < shower/bath.")]
        [Range(0f, 1f)] public float Ceiling = 1f;

        [Tooltip("ON for the sea only: a player CLEANER than the ceiling is pulled back down to it (salt), so " +
                 "the sea pulls toward its base from either side. OFF: a station only ever cleans.")]
        public bool PullsBothWays = false;

        [Tooltip("ON for a toilet: a seat with no effect at all. Washing at it does nothing.")]
        public bool SitOnly = false;

        [Tooltip("Needs running water to work (a placed station with no supply would refuse). No water " +
                 "signal exists yet, so nothing reads this; it is here for the placement lanes.")]
        public bool NeedsWater = false;

        [Tooltip("Needs power to work (an electric pump or heater). No power signal exists yet, so nothing " +
                 "reads this; it is here for the placement lanes.")]
        public bool NeedsPower = false;

        /// <summary>True when washing here changes anything.</summary>
        public bool Washes => !SitOnly && CleanRatePerGameHour > 0f;
    }
}
