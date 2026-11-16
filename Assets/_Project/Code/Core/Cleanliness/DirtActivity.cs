using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The kinds of work that get the player dirty</b> (cleanliness slice 1, owner ask 2026-11-15: "hard
    /// work, dirty work, gym work or being in the ocean can make you dirty"). A KIND only: how dirty each
    /// makes you is the owner's number on <see cref="CleanlinessSettings.Sources"/> (rule 6). The sea is not
    /// here: it is a wash station that pulls both ways (<see cref="WashStationKind.Sea"/>).
    ///
    /// <para><b>Append-only, explicit values</b>: <see cref="DirtSourceRate.Kind"/> stores the integer in
    /// <c>GameConfig.asset</c> and <see cref="PlayerCleanlinessDto.Activity"/> stores it in the save.</para>
    /// </summary>
    public enum DirtActivity
    {
        /// <summary>Nothing in particular: only the slow passive rate applies.</summary>
        None = 0,

        /// <summary>Hard work: hauling pots, carrying, rowing. Sweat more than grime.</summary>
        HardWork = 1,

        /// <summary>Handling fish: gutting, landing, the slime and scales.</summary>
        FishHandling = 2,

        /// <summary>Bait: baiting pots and hooks, the bait barrel.</summary>
        Bait = 3,

        /// <summary>Clam digging on the flats: mud to the elbows.</summary>
        ClamDigging = 4,

        /// <summary>Farm work: the plot, the soil, the barn.</summary>
        FarmWork = 5,

        /// <summary>Engine work: grease and diesel.</summary>
        EngineWork = 6,

        /// <summary>A gym workout (the body lane's exercise).</summary>
        Gym = 7,
    }

    /// <summary>
    /// <b>How dirty one kind of work makes you</b> (cleanliness slice 1). Two ways in, both optional: a
    /// RATE while a stretch of that work is going on (<see cref="ICleanlinessService.BeginActivity"/>), on
    /// top of the passive rate, and a one-off BUMP each time a single action of it is reported
    /// (<see cref="ICleanlinessService.ReportAction"/>: a fish landed, a pot rebaited). Dirt is 0..1, 0 clean.
    /// </summary>
    [Serializable]
    public struct DirtSourceRate
    {
        [Tooltip("Which kind of work these numbers are for.")]
        public DirtActivity Kind;

        [Tooltip("Dirt added per IN-GAME hour while a stretch of this work goes on, on top of the passive " +
                 "rate (0..1 scale; the stinky line is the curve's StrongAt).")]
        [Min(0f)] public float RisePerGameHour;

        [Tooltip("Dirt added at once by one reported action of this work (one fish landed, one pot rebaited).")]
        [Min(0f)] public float PerAction;

        public DirtSourceRate(DirtActivity kind, float risePerGameHour, float perAction)
        {
            Kind = kind;
            RisePerGameHour = risePerGameHour;
            PerAction = perAction;
        }
    }
}
