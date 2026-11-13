using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy.SupplyChain
{
    public enum FishRunPhase { Loading, Travelling, Unloading, Returning }

    /// <summary>
    /// Isolated, fixed-step NPC chain. Construction allocates; AdvanceTo and inventory work do not.
    /// Replay requires the same initial landings and time-indexed physical-work receipts as well as
    /// (seed, game time). No Unity lifecycle, global random source, scene binding or save writer.
    /// Treat the validated spec as immutable after construction.
    /// </summary>
    public sealed class FishChainModel
    {
        private readonly FishChainSpec _spec;
        private readonly uint _seed;
        private readonly IFishChainHands _hands;
        private readonly IFishChainCarrier _carrier;
        private readonly IFishChainMarket _market;
        private readonly FishLotStore[] _stores;
        private readonly string[] _species;
        private readonly long[] _landed, _consumedFish, _consumedGrams, _byproductGrams;
        private readonly FishLot[] _work;
        private readonly long[] _workRemaining, _departedAt;
        private readonly int[] _workers, _demand, _source, _cargo, _destination, _outlet, _loadCursor;
        private readonly FishRunPhase[] _phase;
        private readonly long[] _outletConsumed, _exported;
        private readonly int _intake;
        private int _linePriority;
        private long _nextStep, _lastRequested = -1, _day = -1, _acceptedToday;

        public long SteppedSeconds { get; private set; } = -1;
        public long Revenue { get; private set; }
        public long AcceptedTodayGrams => _acceptedToday;

        public FishChainModel(FishChainSpec spec, uint worldSeed, IFishChainHands hands,
            IFishChainCarrier carrier, IFishChainMarket market = null)
        {
            if (spec == null || hands == null || carrier == null) throw new ArgumentNullException();
            spec.Validate(); _spec = spec; _seed = worldSeed; _hands = hands; _carrier = carrier; _market = market;
            _stores = new FishLotStore[spec.Storages.Length];
            for (int i = 0; i < _stores.Length; i++) _stores[i] = new FishLotStore(spec.Storages[i]);
            _intake = StoreIndex(spec.IntakeStorageId);
            var species = new System.Collections.Generic.List<string>();
            for (int i = 0; i < spec.Lines.Length; i++)
                if (!species.Contains(spec.Lines[i].SpeciesId)) species.Add(spec.Lines[i].SpeciesId);
            _species = species.ToArray();
            _landed = new long[_stores.Length * _species.Length];
            _consumedFish = new long[_species.Length]; _consumedGrams = new long[_species.Length];
            _byproductGrams = new long[_species.Length];
            _work = new FishLot[spec.Lines.Length]; _workRemaining = new long[_work.Length];
            _workers = new int[_work.Length];
            _linePriority = _work.Length == 0 ? 0 : (int)(worldSeed % (uint)_work.Length);
            _demand = new int[spec.Outlets.Length]; _outletConsumed = new long[_demand.Length]; _exported = new long[_demand.Length];
            int runs = spec.Runs.Length;
            _source = new int[runs]; _cargo = new int[runs]; _destination = new int[runs]; _outlet = new int[runs];
            _phase = new FishRunPhase[runs]; _departedAt = new long[runs];
            _loadCursor = new int[runs];
            for (int i = 0; i < runs; i++)
            {
                _source[i] = StoreIndex(spec.Runs[i].SourceStorageId);
                _cargo[i] = StoreIndex(spec.Runs[i].CargoStorageId);
                _destination[i] = StoreIndex(spec.Runs[i].DestinationStorageId);
                _outlet[i] = OutletIndex(spec.Runs[i].DestinationStorageId);
            }
        }

        /// <summary>Import an already hand-delivered raw lot. Failure changes no inventory or market totals.</summary>
        public bool TryLand(string wharfId, in FishLot lot)
        {
            int store = StoreIndex(wharfId), species = SpeciesIndex(lot.SpeciesId);
            if (store < 0 || species < 0 || _stores[store].Spec.Kind != FishStoreKind.Wharf ||
                lot.Product != FishProduct.Raw || lot.IndividualRawGrams != lot.IndividualGrams ||
                lot.Count <= 0 || _stores[store].Fits(lot) != lot.Count) return false;
            _stores[store].Add(lot);
            _landed[store * _species.Length + species] += lot.Count;
            return true;
        }

        public void SetWorkers(int line, int workers)
        {
            if (line < 0 || line >= _workers.Length || workers < 0) throw new ArgumentOutOfRangeException();
            _workers[line] = workers;
        }

        /// <summary>Slow-tick entry point. Fixed steps prevent polling frequency from changing the result.</summary>
        public void AdvanceTo(long absoluteGameSeconds)
        {
            if (absoluteGameSeconds < 0 || absoluteGameSeconds < _lastRequested) throw new ArgumentOutOfRangeException();
            while (_nextStep <= absoluteGameSeconds)
            {
                Step(_nextStep); SteppedSeconds = _nextStep;
                _nextStep = checked(_nextStep + _spec.StepSeconds);
            }
            _lastRequested = absoluteGameSeconds;
        }

        private void Step(long now)
        {
            long day = now / _spec.SecondsPerDay;
            if (day != _day)
            {
                _day = day; _acceptedToday = 0;
                int season = (int)GameCalendar.AtDay(day, _spec.DaysPerSeason, _spec.StartDayOfYear).Season;
                int weekday = (int)(day % 7);
                for (int i = 0; i < _demand.Length; i++)
                    _demand[i] = (int)Math.Min(int.MaxValue, (long)_spec.Outlets[i].DemandByWeekday[weekday] *
                        _spec.Outlets[i].SeasonalDemandPpm[season] / FishChainSpec.PartsPerMillion);
            }
            int firstLine = _linePriority;
            for (int n = 0; n < _work.Length; n++)
            {
                int line = (firstLine + n) % _work.Length;
                if (ProcessLine(line, now)) _linePriority = (line + 1) % _work.Length;
            }
            // A seeded daily rotation is deterministic and prevents one delivery run always winning a glut.
            int first = _phase.Length == 0 ? 0 : (int)((_seed + (ulong)day) % (ulong)_phase.Length);
            for (int n = 0; n < _phase.Length; n++) Run((first + n) % _phase.Length, now);
            for (int i = 0; i < _demand.Length; i++) Consume(i);
            if (_market != null)
            {
                FishLotStore intake = _stores[_intake];
                long free = Math.Min(intake.Spec.MassCapacityGrams - intake.MassGrams, _spec.DailyIntakeGrams - _acceptedToday);
                bool blocked = free <= 0 || intake.VolumeMl == intake.Spec.VolumeCapacityMl;
                for (int s = 0; s < _stores.Length; s++)
                    if (_stores[s].Spec.Kind == FishStoreKind.Wharf)
                        for (int p = 0; p < _species.Length; p++)
                        {
                            var pressure = new FishChainPressure(_stores[s].Spec.Id, _species[p],
                                _landed[s * _species.Length + p], _stores[s].CountOf(_species[p]), free, blocked, now);
                            _market.Report(pressure);
                        }
            }
        }

        private bool ProcessLine(int line, long now)
        {
            FishLineSpec spec = _spec.Lines[line];
            if (_workers[line] < spec.RequiredWorkers) return false;
            FishLotStore output = _stores[StoreIndex(spec.OutputStorageId)];
            if (_work[line].Count > 0)
            {
                _workRemaining[line] = Math.Max(0, _workRemaining[line] - _spec.StepSeconds);
                if (_workRemaining[line] > 0) return false;
                FishLot product = _work[line];
                product.Product = spec.Product;
                product.IndividualGrams = (int)Math.Max(1, (long)product.IndividualRawGrams * spec.YieldPpm / FishChainSpec.PartsPerMillion);
                product.IndividualVolumeMl = (int)Math.Max(1, (long)product.IndividualVolumeMl * spec.YieldPpm / FishChainSpec.PartsPerMillion);
                int count = output.Fits(product);
                count = Confirm(spec.Id, spec.Id, output.Spec.Id, product, count, now);
                if (count == 0) return false;
                product.Count = count; output.Add(product); _work[line].Count -= count;
                _byproductGrams[SpeciesIndex(product.SpeciesId)] += (long)count * (product.IndividualRawGrams - product.IndividualGrams);
                return false;
            }
            FishLotStore intake = _stores[_intake];
            for (int i = 0; i < intake.Lots.Length; i++)
            {
                FishLot lot = intake.Lots[i];
                if (lot.Count == 0 || lot.SpeciesId != spec.SpeciesId || lot.Product != FishProduct.Raw) continue;
                int capacity = (int)Math.Min(Math.Min(lot.Count, spec.BatchFish),
                    Math.Min(spec.WorkMassCapacityGrams / lot.IndividualGrams, spec.WorkVolumeCapacityMl / lot.IndividualVolumeMl));
                int count = Confirm(spec.Id, intake.Spec.Id, spec.Id, lot, capacity, now);
                if (count == 0) return false;
                _work[line] = intake.Remove(i, count);
                _workRemaining[line] = spec.ProcessSeconds + ((long)count * spec.LabourSecondsPerFish + _workers[line] - 1) / _workers[line];
                return true;
            }
            return false;
        }

        private void Run(int run, long now)
        {
            FishTruckRunSpec spec = _spec.Runs[run];
            if (_phase[run] == FishRunPhase.Travelling || _phase[run] == FishRunPhase.Returning)
            {
                bool returning = _phase[run] == FishRunPhase.Returning;
                if (now - _departedAt[run] >= spec.TravelSeconds && _carrier.HasArrived(spec.Id, returning, _departedAt[run], now))
                    _phase[run] = returning ? FishRunPhase.Loading : FishRunPhase.Unloading;
                return;
            }
            bool loading = _phase[run] == FishRunPhase.Loading;
            if (loading && !_carrier.CanLoad(spec.Id, spec.ExistingRouteId, now)) return;
            FishLotStore source = _stores[loading ? _source[run] : _cargo[run]];
            FishLotStore target = _stores[loading ? _cargo[run] : _destination[run]];
            int budget = Math.Min(spec.HandFishPerStep, _spec.StepSeconds / spec.HandSecondsPerFish);
            int moved = 0;
            int firstLot = loading ? _loadCursor[run] : 0;
            for (int n = 0; n < source.Lots.Length && budget > 0; n++)
            {
                int i = (firstLot + n) % source.Lots.Length;
                FishLot lot = source.Lots[i];
                if (lot.Count == 0) continue;
                int count = Math.Min(budget, target.Fits(lot));
                int outlet = _outlet[run];
                if (outlet >= 0)
                {
                    FishOutletSpec customer = _spec.Outlets[outlet];
                    if (lot.SpeciesId != customer.SpeciesId || lot.Product != customer.Product) continue;
                    if (loading)
                        count = (int)Math.Min(count, Math.Max(0, _demand[outlet] -
                            _stores[_destination[run]].CountOf(lot.SpeciesId, lot.Product) - target.CountOf(lot.SpeciesId, lot.Product)));
                }
                if (!loading && _destination[run] == _intake)
                    count = (int)Math.Min(count, (_spec.DailyIntakeGrams - _acceptedToday) / lot.IndividualGrams);
                count = Confirm(spec.Id, source.Spec.Id, target.Spec.Id, lot, count, now);
                if (count == 0) continue;
                FishLot transfer = source.Remove(i, count); target.Add(transfer); budget -= count; moved += count;
                if (loading) _loadCursor[run] = (i + 1) % source.Lots.Length;
                if (!loading && _destination[run] == _intake) _acceptedToday += transfer.MassGrams;
                if (!loading && outlet >= 0)
                {
                    Revenue += (long)count * _spec.Outlets[outlet].PricePerFish;
                    if (_spec.Outlets[outlet].Kind == FishOutletKind.Export) _exported[outlet] += count;
                }
            }
            if (loading && moved > 0) { _phase[run] = FishRunPhase.Travelling; _departedAt[run] = now; }
            if (!loading && source.MassGrams == 0) { _phase[run] = FishRunPhase.Returning; _departedAt[run] = now; }
        }

        private int Confirm(string work, string source, string target, in FishLot lot, int count, long now)
        {
            if (count <= 0) return 0;
            var request = new FishChainTransfer(work, source, target, lot.SpeciesId, lot.Product,
                lot.IndividualRawGrams, lot.IndividualGrams, lot.IndividualVolumeMl, count, now);
            return Math.Max(0, Math.Min(count, _hands.ConfirmTransfer(request)));
        }

        private void Consume(int outlet)
        {
            FishOutletSpec spec = _spec.Outlets[outlet];
            FishLotStore store = _stores[StoreIndex(spec.StorageId)];
            for (int i = 0; i < store.Lots.Length && _demand[outlet] > 0; i++)
            {
                FishLot lot = store.Lots[i];
                if (lot.Count == 0 || lot.SpeciesId != spec.SpeciesId || lot.Product != spec.Product) continue;
                int count = Math.Min(lot.Count, _demand[outlet]);
                FishLot consumed = store.Remove(i, count); int species = SpeciesIndex(lot.SpeciesId);
                _consumedFish[species] += count; _consumedGrams[species] += consumed.MassGrams;
                _demand[outlet] -= count; _outletConsumed[outlet] += count;
            }
        }

        public long CountAt(string store, string species) => _stores[RequiredStore(store)].CountOf(species);
        public long MassAt(string store) => _stores[RequiredStore(store)].MassGrams;
        public long VolumeAt(string store) => _stores[RequiredStore(store)].VolumeMl;
        public FishLot LotAt(string store, int slot) => _stores[RequiredStore(store)].Lots[slot];
        public FishRunPhase Phase(int run) => _phase[run];
        public long OutletConsumed(int outlet) => _outletConsumed[outlet];
        public long Exported(int outlet) => _exported[outlet];
        public int RemainingDemand(int outlet) => _demand[outlet];
        public long Consumed(string species) => _consumedFish[RequiredSpecies(species)];
        public long ByproductGrams(string species) => _byproductGrams[RequiredSpecies(species)];

        public long TotalFish(string species)
        {
            long total = Consumed(species);
            for (int i = 0; i < _stores.Length; i++) total += _stores[i].CountOf(species);
            for (int i = 0; i < _work.Length; i++) if (_work[i].SpeciesId == species) total += _work[i].Count;
            return total;
        }

        public long TotalGrams(string species)
        {
            int p = RequiredSpecies(species);
            long grams = _consumedGrams[p] + _byproductGrams[p];
            for (int i = 0; i < _stores.Length; i++)
                for (int j = 0; j < _stores[i].Lots.Length; j++)
                    if (_stores[i].Lots[j].SpeciesId == species) grams += _stores[i].Lots[j].MassGrams;
            for (int i = 0; i < _work.Length; i++) if (_work[i].SpeciesId == species) grams += _work[i].MassGrams;
            return grams;
        }

        private int StoreIndex(string id)
        { for (int i = 0; i < _stores.Length; i++) if (_stores[i].Spec.Id == id) return i; return -1; }
        private int SpeciesIndex(string id)
        { for (int i = 0; i < _species.Length; i++) if (_species[i] == id) return i; return -1; }
        private int OutletIndex(string storage)
        { for (int i = 0; i < _spec.Outlets.Length; i++) if (_spec.Outlets[i].StorageId == storage) return i; return -1; }
        private int RequiredStore(string id)
        { int index = StoreIndex(id); return index >= 0 ? index : throw new ArgumentException("Unknown storage"); }
        private int RequiredSpecies(string id)
        { int index = SpeciesIndex(id); return index >= 0 ? index : throw new ArgumentException("Unknown species"); }
    }
}
