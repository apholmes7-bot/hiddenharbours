using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One species on the player's catch record, as saved</b> (save v24): what the Notebook's catch page
    /// shows. The species id is the key; the name is the one the catch carried, kept so the page never has
    /// to ask the Fishing module what a species is called (rule 4). Read and written only through
    /// <see cref="CatchRecordLog"/> (via <see cref="CatchRecordSave"/>). Flat and JsonUtility-friendly.
    /// </summary>
    [Serializable]
    public sealed class CaughtSpeciesDto
    {
        /// <summary>The species' stable Def id (<c>fish.*</c>).</summary>
        public string SpeciesId = "";

        /// <summary>The name the catch carried when it was counted.</summary>
        public string DisplayName = "";

        /// <summary>How many caught, released ones included (at least 1 on a kept row).</summary>
        public int Count;

        /// <summary>How many of <see cref="Count"/> were protected, documented and put back.</summary>
        public int ReleasedCount;

        /// <summary>The heaviest catch of this species, kg; 0 = unknown.</summary>
        public float RecordWeightKg;
    }

    /// <summary>
    /// <b>Gifts one person has accepted on one game day</b> (save v24, data only until the gifting lane wires
    /// it). Only the current day's rows are meant to be written, so the list never grows past the cast.
    /// </summary>
    [Serializable]
    public sealed class NpcGiftDayDto
    {
        /// <summary>The person's stable NPC id.</summary>
        public string NpcId = "";

        /// <summary>The game day the count belongs to (<c>IGameClock.DayIndex</c>).</summary>
        public int DayIndex;

        /// <summary>Gifts accepted on <see cref="DayIndex"/> (at least 1 on a kept row).</summary>
        public int Count;
    }

    /// <summary>
    /// <b>Something cooking on a placed station</b> (save v24, data only until the kitchens lane wires it):
    /// one row per running job. The input was taken when the work began; the output is the recipe's own and
    /// arrives at <see cref="ReadyAtGameSeconds"/>. Ids only, so a retuned recipe follows into every save.
    /// </summary>
    [Serializable]
    public sealed class CookJobDto
    {
        /// <summary>Which placed station (its interactable id), so two stoves cook separately.</summary>
        public string StationKey = "";

        /// <summary>The station kind's stable Def id (<c>cooking_station.*</c>).</summary>
        public string StationId = "";

        /// <summary>The running recipe's stable Def id (<c>recipe.*</c>).</summary>
        public string RecipeId = "";

        /// <summary>The game clock (<c>IGameClock.TotalSeconds</c>) the job is done at.</summary>
        public double ReadyAtGameSeconds;
    }

    /// <summary>
    /// <b>One set of Loran "numbers" a hull's receiver remembers</b> (save v24, data only until the
    /// instrument host wires it): the <see cref="LoranWaypoint"/> fields plus the hull they belong to.
    /// Kept as time differences, never a position, so a retune of the chain data never moves a ground.
    /// </summary>
    [Serializable]
    public sealed class LoranWaypointDto
    {
        /// <summary>The hull whose receiver holds it (the <see cref="InstrumentLocker"/> key).</summary>
        public string HullId = "";

        /// <summary>The slot number shown on the set, 1-based.</summary>
        public int Number;

        /// <summary>The chain's stable Def id.</summary>
        public string ChainId = "";

        /// <summary>The secondary indexes the pair was read on.</summary>
        public int SecondaryA, SecondaryB;

        /// <summary>The two time differences.</summary>
        public double TdA, TdB;

        /// <summary>This row as the runtime waypoint (hull id dropped).</summary>
        public LoranWaypoint ToWaypoint() => new LoranWaypoint(Number, ChainId, SecondaryA, SecondaryB, TdA, TdB);

        /// <summary>A row for <paramref name="hullId"/> holding <paramref name="w"/>.</summary>
        public static LoranWaypointDto From(string hullId, in LoranWaypoint w) => new LoranWaypointDto
        {
            HullId = hullId ?? "", Number = w.Number, ChainId = w.ChainId ?? "",
            SecondaryA = w.SecondaryA, SecondaryB = w.SecondaryB, TdA = w.TdA, TdB = w.TdB,
        };
    }
}
