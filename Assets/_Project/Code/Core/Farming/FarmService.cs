using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b><see cref="IFarmService"/> over the shipped Defs</b> (farm slice 1). The <see cref="TraitService"/>
    /// shape: every seam is injectable, so a test drives it with its own Defs, seed and calendar. The
    /// defaults load every <see cref="FarmPlotDef"/>, <see cref="CropDef"/> and <see cref="ProduceDef"/>
    /// under <c>Resources/Farming/</c> once on first need, read the world seed from
    /// <see cref="GameServices.Environment"/> and the calendar from <see cref="GameServices.Config"/> at
    /// each call.
    /// </summary>
    public sealed class FarmService : IFarmService
    {
        /// <summary>The folder under any <c>Resources</c> root the farm Defs live in.</summary>
        public const string ResourcesFolder = "Farming";

        private readonly Func<IEnumerable<FarmPlotDef>> _plotSource;
        private readonly Func<IEnumerable<CropDef>> _cropSource;
        private readonly Func<IEnumerable<ProduceDef>> _produceSource;
        private readonly Func<int> _worldSeed;
        private readonly Func<int> _daysPerSeason;
        private readonly Func<int> _startDayOfYear;

        private Catalog<FarmPlotDef> _plots;
        private Catalog<CropDef> _crops;
        private Catalog<ProduceDef> _produce;
        private readonly Dictionary<string, FarmStage> _observed = new Dictionary<string, FarmStage>(StringComparer.Ordinal);

        public FarmService(Func<IEnumerable<FarmPlotDef>> plots = null, Func<IEnumerable<CropDef>> crops = null,
                           Func<IEnumerable<ProduceDef>> produce = null, Func<int> worldSeed = null,
                           Func<int> daysPerSeason = null, Func<int> startDayOfYear = null)
        {
            _plotSource = plots ?? (() => Resources.LoadAll<FarmPlotDef>(ResourcesFolder));
            _cropSource = crops ?? (() => Resources.LoadAll<CropDef>(ResourcesFolder));
            _produceSource = produce ?? (() => Resources.LoadAll<ProduceDef>(ResourcesFolder));
            _worldSeed = worldSeed ?? (() => GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0);
            _daysPerSeason = daysPerSeason ?? (() => GameServices.DaysPerSeason);
            // The same fallback GameCalendar.AtSeconds uses with no config: High Summer day 1.
            _startDayOfYear = startDayOfYear ?? (() => GameServices.Config != null
                ? GameServices.Config.StartDayOfYear : GameConfig.DefaultDaysPerSeason);
        }

        public IReadOnlyList<FarmPlotDef> Plots => PlotCatalog.All;
        public IReadOnlyList<CropDef> Crops => CropCatalog.All;
        public IReadOnlyList<ProduceDef> Produce => ProduceCatalog.All;

        public FarmPlotDef FindPlot(string id) => PlotCatalog.Find(id);
        public CropDef FindCrop(string id) => CropCatalog.Find(id);
        public ProduceDef FindProduce(string id) => ProduceCatalog.Find(id);

        public CropDef CropFor(FarmPlotDef plot, int year) => FindCrop(FarmMath.CropIdForYear(plot, year));

        public bool TryGetState(string plotId, long elapsedDay, out FarmPlotState state)
        {
            FarmPlotDef plot = FindPlot(plotId);
            if (plot == null)
            {
                state = default;
                return false;
            }
            state = StateOf(plot, elapsedDay);
            return true;
        }

        public int ChoresDue(string plotId, long elapsedDay, List<FarmChore> into)
        {
            FarmPlotDef plot = FindPlot(plotId);
            if (plot == null || into == null) return 0;
            FarmPlotState state = StateOf(plot, elapsedDay);
            return FarmMath.ChoresDue(state.Schedule, FindCrop(state.CropId), state.DayOfYear, into);
        }

        public int PlotsTendedBy(string npcId, List<FarmPlotDef> into)
        {
            if (string.IsNullOrEmpty(npcId) || into == null) return 0;
            int before = into.Count;
            foreach (FarmPlotDef plot in Plots)
            {
                string[] tenders = plot.TenderNpcIds;
                if (tenders == null) continue;
                foreach (string t in tenders)
                    if (string.Equals(t, npcId, StringComparison.Ordinal)) { into.Add(plot); break; }
            }
            return into.Count - before;
        }

        public void Observe(long elapsedDay, bool announce)
        {
            foreach (FarmPlotDef plot in Plots)
            {
                FarmPlotState state = StateOf(plot, elapsedDay);
                bool known = _observed.TryGetValue(plot.Id, out FarmStage last);
                _observed[plot.Id] = state.Stage;
                if (announce && known && last != state.Stage)
                    EventBus.Publish(new FarmPlotStageChanged(plot.Id, state.CropId, last, state.Stage, elapsedDay));
            }
        }

        private FarmPlotState StateOf(FarmPlotDef plot, long elapsedDay)
        {
            int length = Math.Max(1, _daysPerSeason());
            int start = _startDayOfYear();
            FarmMath.CalendarDay day = FarmMath.DayAt(elapsedDay, length, start);
            return FarmMath.StateAt(plot, CropFor(plot, day.Year), _worldSeed(), elapsedDay, length, start);
        }

        private Catalog<FarmPlotDef> PlotCatalog => _plots ??= new Catalog<FarmPlotDef>(_plotSource(), d => d.Id);
        private Catalog<CropDef> CropCatalog => _crops ??= new Catalog<CropDef>(_cropSource(), d => d.Id);
        private Catalog<ProduceDef> ProduceCatalog => _produce ??= new Catalog<ProduceDef>(_produceSource(), d => d.Id);

        /// <summary>Defs by id, first by id wins (the content validation test fails a duplicate loudly).</summary>
        private sealed class Catalog<T> where T : UnityEngine.Object
        {
            public readonly List<T> All = new List<T>();
            private readonly Dictionary<string, T> _byId = new Dictionary<string, T>(StringComparer.Ordinal);
            private readonly Func<T, string> _id;

            public Catalog(IEnumerable<T> source, Func<T, string> id)
            {
                _id = id;
                if (source != null)
                    foreach (T def in source)
                    {
                        if (def == null) continue;
                        string key = id(def);
                        if (string.IsNullOrEmpty(key) || _byId.ContainsKey(key)) continue;
                        _byId.Add(key, def);
                        All.Add(def);
                    }
                All.Sort((a, b) => string.CompareOrdinal(_id(a), _id(b)));
            }

            public T Find(string id) => !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out T def) ? def : null;
        }
    }
}
