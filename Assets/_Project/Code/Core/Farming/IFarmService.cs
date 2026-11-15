using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The farm plots, as a seam</b> (farm slice 1, rule 4). Read-only questions about the shipped plots,
    /// crops and produce, answered from <see cref="FarmMath"/>: "what does this plot look like on this day",
    /// "what work is due on it today", "which plots does this NPC tend". A routine, a presenter or a later
    /// player action asks here and never learns how a plot is computed.
    ///
    /// <para><b>Days are elapsed days</b> (<see cref="IGameClock.DayIndex"/>); the service turns them into the
    /// calendar with the configured season length and start day. Recomputed, never saved (rule 5).</para>
    /// </summary>
    public interface IFarmService
    {
        /// <summary>Every shipped plot, ordinal by id.</summary>
        IReadOnlyList<FarmPlotDef> Plots { get; }
        /// <summary>Every shipped crop, ordinal by id.</summary>
        IReadOnlyList<CropDef> Crops { get; }
        /// <summary>Every shipped produce item, ordinal by id.</summary>
        IReadOnlyList<ProduceDef> Produce { get; }

        FarmPlotDef FindPlot(string id);
        CropDef FindCrop(string id);
        ProduceDef FindProduce(string id);

        /// <summary>The crop the plot's rotation sows in <paramref name="year"/> (1-based); null in a rest
        /// year or for an unknown crop id.</summary>
        CropDef CropFor(FarmPlotDef plot, int year);

        /// <summary>The plot's state on <paramref name="elapsedDay"/>. False for an unknown plot.</summary>
        bool TryGetState(string plotId, long elapsedDay, out FarmPlotState state);

        /// <summary>The work due on the plot on <paramref name="elapsedDay"/>, appended to
        /// <paramref name="into"/>; returns how many (0 for an unknown plot).</summary>
        int ChoresDue(string plotId, long elapsedDay, List<FarmChore> into);

        /// <summary>The plots <paramref name="npcId"/> tends, appended to <paramref name="into"/> in id order;
        /// returns how many.</summary>
        int PlotsTendedBy(string npcId, List<FarmPlotDef> into);

        /// <summary>
        /// Note every plot's stage on <paramref name="elapsedDay"/> and, when <paramref name="announce"/> is
        /// set, publish <see cref="FarmPlotStageChanged"/> for each plot whose stage moved since the last
        /// observation. Called once a day by the day bridge (never per frame); a load re-baselines without
        /// announcing, since a restore is not time passing. The first observation only baselines.
        /// </summary>
        void Observe(long elapsedDay, bool announce);
    }

    /// <summary>A plot moved to a new stage at a day's start (published by <see cref="IFarmService.Observe"/>).</summary>
    public readonly struct FarmPlotStageChanged
    {
        public readonly string PlotId;
        /// <summary>The crop this year, or null in a rest year.</summary>
        public readonly string CropId;
        public readonly FarmStage From;
        public readonly FarmStage To;
        public readonly long ElapsedDay;

        public FarmPlotStageChanged(string plotId, string cropId, FarmStage from, FarmStage to, long elapsedDay)
        {
            PlotId = plotId;
            CropId = cropId;
            From = from;
            To = to;
            ElapsedDay = elapsedDay;
        }
    }
}
