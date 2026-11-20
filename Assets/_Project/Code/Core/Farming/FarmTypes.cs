using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Where a farm plot is in its year (farm slice 1). <b>Append-only</b>: a later presenter keys art on
    /// the number, so a value is never renumbered or reused.
    /// </summary>
    public enum FarmStage
    {
        /// <summary>Rough, untilled ground: before this year's tilling, or a rest year in the rotation.</summary>
        Fallow = 0,
        /// <summary>Turned soil in rows, nothing sown yet.</summary>
        Tilled = 1,
        /// <summary>Sown; the rows are bare to the eye.</summary>
        Planted = 2,
        /// <summary>The first green showing along the rows.</summary>
        Sprouting = 3,
        /// <summary>Leafy and filling out, not ready (or mature but waiting for its harvest window).</summary>
        Growing = 4,
        /// <summary>Ready to lift or cut, inside the crop's harvest window.</summary>
        Ready = 5,
        /// <summary>Harvested: stubble and lifted ground until next year's tilling.</summary>
        Stubble = 6,
    }

    /// <summary>
    /// One piece of farm work (farm slice 1). A plain model: an NPC routine OR a player action can be the
    /// hand that does it; nothing here knows which. <b>Append-only.</b>
    /// </summary>
    public enum FarmChore
    {
        Till = 0,
        Plant = 1,
        /// <summary>Weeding, hilling, watering: the "tend" days between planting and readiness.</summary>
        Tend = 2,
        Harvest = 3,
    }

    /// <summary>
    /// Who may work a plot besides its tenders (owner ruling 2026-11-14, relayed: "players can farm too
    /// starting with working on ginnys plot, another plot would need to be approved by an npc as a side job
    /// or larger plots can be purchased"). Slice 1 only CARRIES this; nothing reads it yet.
    /// <b>Append-only.</b>
    /// </summary>
    public enum FarmPlotAccess
    {
        /// <summary>Only the tenders work it.</summary>
        TendersOnly = 0,
        /// <summary>The player may simply pitch in (Ginny's plot).</summary>
        OpenToHelp = 1,
        /// <summary>The player may work it once <see cref="FarmPlotDef.ApproverNpcId"/> agrees, as a side job.</summary>
        SideJobByApproval = 2,
        /// <summary>The player may buy it for <see cref="FarmPlotDef.PurchasePrice"/>.</summary>
        ForPurchase = 3,
    }

    /// <summary>
    /// A day on the four-season calendar as an author writes it: a season and a 1-based day within it
    /// (<see cref="IGameClock.DayOfSeason"/>). Converted to a 0-based day of the year by
    /// <see cref="FarmMath.DayOfYear"/> with the configured season length, so the owner reads
    /// "Early Spring 15", never a bare day number.
    /// </summary>
    [Serializable]
    public struct SeasonDay
    {
        public Season Season;
        [Min(1)] public int Day;

        public SeasonDay(Season season, int day)
        {
            Season = season;
            Day = day;
        }

        public override string ToString() => Season + " " + Day;
    }
}
