using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>All the cleanliness numbers in one block</b> on <see cref="GameConfig"/> (cleanliness slice 1,
    /// owner ask 2026-11-15), read through <see cref="GameServices.CleanlinessSettings"/> so an unwired
    /// config falls back to <see cref="Default"/>. The wash rates and ceilings are NOT here: they live on
    /// each <see cref="WashStationDef"/> asset (one per kind), because a station is content.
    ///
    /// <para>The need is held as DIRT (0 clean .. 1 filthy) so it rises like hunger and tiredness and shares
    /// their <see cref="NeedCurve"/>: Mild is "grubby", Strong is "stinky" (villagers notice).</para>
    /// </summary>
    [Serializable]
    public struct CleanlinessSettings
    {
        [Tooltip("The dirt need's curve. RisePerGameHour is the slow PASSIVE rate (just living); MildAt is " +
                 "grubby; StrongAt is STINKY, the line villagers react to. Walk and sell penalties shipped at " +
                 "0 (owner 2026-11-14: needs never cut a sale price; stink is told by the villagers, not a " +
                 "slower walk). The knobs stay.")]
        public NeedCurve Curve;

        [Tooltip("How dirty each kind of work makes you, per game hour while it lasts and per single action. " +
                 "A kind with no row adds nothing.")]
        public DirtSourceRate[] Sources;

        [Tooltip("The longest one announced stretch of work or one wash is counted for, in IN-GAME minutes: a " +
                 "guard so a missed 'stopped' signal cannot dirty (or clean) the player for a whole day.")]
        [Min(1f)] public float MaxStretchGameMinutes;

        [Tooltip("Does WADING in the sea count as being in it (rinse and salt)? Off: only swimming does.")]
        public bool WadeCountsAsSea;

        [Tooltip("The WashStationDef id the sea uses (its rate and base level live on that asset).")]
        public string SeaStationId;

        /// <summary>The id of the shipped sea station asset.</summary>
        public const string DefaultSeaStationId = "washstation.sea";

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel). Read with
        /// <see cref="GameConfig.SecondsPerDay"/> 1800: one game hour is 75 real seconds.</summary>
        public static CleanlinessSettings Default => new CleanlinessSettings
        {
            Curve = new NeedCurve
            {
                RisePerGameHour = 1f / 48f,   // idle: grubby after 24 game hours, stinky after 36
                MildAt = 0.5f,                // grubby
                StrongAt = 0.75f,             // stinky: villagers notice
                MildWalkPenalty = 0f,
                StrongWalkPenalty = 0f,
                MildSellPenalty = 0f,         // owner 2026-11-14: needs never cut a sale price
                StrongSellPenalty = 0f,
            },
            Sources = new[]
            {
                new DirtSourceRate(DirtActivity.HardWork, 0.08f, 0f),        // stinky from clean in ~7.4 h
                new DirtSourceRate(DirtActivity.FishHandling, 0.10f, 0.015f),
                new DirtSourceRate(DirtActivity.Bait, 0.12f, 0.01f),
                new DirtSourceRate(DirtActivity.ClamDigging, 0.15f, 0.01f),
                new DirtSourceRate(DirtActivity.FarmWork, 0.12f, 0f),
                new DirtSourceRate(DirtActivity.EngineWork, 0.20f, 0f),      // stinky from clean in ~3.4 h
                new DirtSourceRate(DirtActivity.Gym, 0.15f, 0f),             // stinky from clean in ~4.4 h
            },
            MaxStretchGameMinutes = 240f,
            WadeCountsAsSea = false,
            SeaStationId = DefaultSeaStationId,
        };
    }
}
