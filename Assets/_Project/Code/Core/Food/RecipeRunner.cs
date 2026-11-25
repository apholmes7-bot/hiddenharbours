using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>What a station would cook right now (<see cref="RecipeRunner.Check"/>). Plain data.</summary>
    public readonly struct CookPlan
    {
        /// <summary>The recipe that would run.</summary>
        public readonly RecipeDef Recipe;
        /// <summary>The item one unit of which would be used up (<c>fish.atlantic_cod</c>, <c>food.cod_fillet</c>).</summary>
        public readonly string InputItemId;
        /// <summary>True when the input is the loose catch in the hands; false when it is in the supply locker.</summary>
        public readonly bool FromHand;
        /// <summary>Game seconds the work takes at this station.</summary>
        public readonly float Seconds;

        public CookPlan(RecipeDef recipe, string inputItemId, bool fromHand, float seconds)
        {
            Recipe = recipe; InputItemId = inputItemId; FromHand = fromHand; Seconds = seconds;
        }
    }

    /// <summary>Something on a station (<see cref="RecipeRunner"/>). Saved as a <see cref="CookJobDto"/> row.</summary>
    public readonly struct CookJob
    {
        /// <summary>The <see cref="CookingStationDef.Id"/> of the station kind.</summary>
        public readonly string StationId;
        /// <summary>Which placed station (its interactable id), so two stoves cook separately.</summary>
        public readonly string StationKey;
        /// <summary>The recipe running.</summary>
        public readonly RecipeDef Recipe;
        /// <summary>The game time (<see cref="IGameClock.TotalSeconds"/>) it is done at.</summary>
        public readonly double ReadyAt;

        public CookJob(string stationId, string stationKey, RecipeDef recipe, double readyAt)
        {
            StationId = stationId; StationKey = stationKey; Recipe = recipe; ReadyAt = readyAt;
        }
    }

    /// <summary>
    /// <b>The one place a recipe is run</b> (kitchens and microwaves slice): a station takes one input from
    /// the hands or the supply locker, works for the recipe's <see cref="RecipeDef.WorkSeconds"/> times the
    /// station's <see cref="CookingStationDef.CookTimeMultiplier"/> of game time, then puts the output into
    /// the supply locker (<see cref="SupplyLocker"/>), where <see cref="FoodEater.TryEatFromStock(string)"/>
    /// already eats it. A boat galley, a house kitchen and a shop all use this; only their station data
    /// differs.
    ///
    /// <para><b>What it takes.</b> If the hands hold a catch, only that is considered: what you hold is
    /// what you meant. Otherwise the supply locker is scanned in ordinal id order, and the first item one
    /// of the station's accepted recipes takes is used. A microwave (<see cref="CookingStationDef.MicrowavableOnly"/>)
    /// refuses any input whose <see cref="FoodDef.Microwavable"/> is not set, so a raw fish stays in the
    /// hand (owner 2026-11-15).</para>
    ///
    /// <para><b>A refusal changes nothing.</b> Every check runs before the input is taken. The input is taken
    /// when the work starts (it is on the stove), and the output arrives when <see cref="TryFinish"/> is
    /// asked at or after the ready time.</para>
    ///
    /// <para><b>Saved with the game (v24), bound to one save.</b> The jobs belong to the live
    /// <see cref="SaveData"/> object, the catch record's pattern (<see cref="CatchRecordLog"/>). The first
    /// read after the save object changes (a load, a new game) loads the jobs from
    /// <see cref="SaveData.CookJobs"/>; every start writes its row and every finish removes it
    /// (<see cref="CookJobSave"/>), so the next save write carries what is on each station and a reload finds
    /// it there, finishing at the same game time. A job is never carried from one save object to another,
    /// so food never duplicates across saves. While there is no save at all (pre-boot, mid-load) the runner
    /// stays bound to the last one. <see cref="GameServices.Cooking"/> subscribes its runner's
    /// <see cref="GameLoadedHandler"/> to <see cref="GameLoaded"/>, which unbinds it so the next read reloads
    /// from the rows even when the save object is reused.</para>
    ///
    /// <para><b>Load-time repair.</b> A row whose recipe id is no longer in the recipe book, or whose
    /// station id names no <see cref="CookingStationDef"/>, or that repeats a station key already loaded, is
    /// dropped from the save as it loads (the migration's heal has already dropped rows with no key, no
    /// recipe or no usable time). Nothing throws.</para>
    ///
    /// <para><b>Job keys.</b> A job is keyed by the placed station's interactable id, so every placed
    /// station needs its own id, built from its owner (scene or boat instance) plus the fixture id; two
    /// stations sharing an id would share one job.</para>
    ///
    /// <para><b>Deterministic</b> (rule 5): no roll; the time is read from the injected clock, defaulting to
    /// <see cref="IGameClock.TotalSeconds"/>. Diegetic only: it publishes <see cref="FoodCookStarted"/>,
    /// <see cref="FoodCooked"/> and <see cref="CookRefused"/>, never text.</para>
    /// </summary>
    public sealed class RecipeRunner
    {
        private readonly Func<RecipeBook> _recipes;
        private readonly Func<FoodBook> _foods;
        private readonly Func<ISaveService> _save;
        private readonly Func<ICatchHands> _hands;
        private readonly Func<double> _now;
        private readonly Func<string, CookingStationDef> _stations;
        private readonly Dictionary<string, CookJob> _jobs = new Dictionary<string, CookJob>(StringComparer.Ordinal);
        private readonly List<string> _scratch = new List<string>();
        private RecipeBook _shippedRecipes;
        private FoodBook _shippedFoods;
        private Dictionary<string, CookingStationDef> _shippedStations;

        // The save object the jobs belong to. Unbound until the first read; null is a real binding (no save).
        private static readonly object Unbound = new object();
        private object _session = Unbound;

        /// <param name="stations">Finds a station kind by its <see cref="CookingStationDef.Id"/> when jobs are
        /// loaded from a save; defaults to the shipped Defs under <c>Resources/CookingStations/</c>.</param>
        public RecipeRunner(Func<RecipeBook> recipes = null, Func<FoodBook> foods = null,
                            Func<ISaveService> save = null, Func<ICatchHands> hands = null,
                            Func<double> now = null, Func<string, CookingStationDef> stations = null)
        {
            _recipes = recipes ?? (() => _shippedRecipes ??= RecipeBook.FromResources());
            _foods = foods ?? (() => _shippedFoods ??= FoodBook.FromResources());
            _save = save ?? (() => GameServices.Save);
            _hands = hands ?? (() => GameServices.CatchHands);
            _now = now ?? (() => GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d);
            _stations = stations ?? ShippedStation;
        }

        private CookingStationDef ShippedStation(string id)
        {
            if (_shippedStations == null)
            {
                _shippedStations = new Dictionary<string, CookingStationDef>(StringComparer.Ordinal);
                foreach (CookingStationDef def in UnityEngine.Resources.LoadAll<CookingStationDef>(CookingStationDef.ResourcesFolder))
                    if (def != null && !string.IsNullOrEmpty(def.Id) && !_shippedStations.ContainsKey(def.Id))
                        _shippedStations.Add(def.Id, def);
            }
            return !string.IsNullOrEmpty(id) && _shippedStations.TryGetValue(id, out CookingStationDef found) ? found : null;
        }

        /// <summary>Bind the jobs to the live save object: when it changed since the last read, forget the
        /// old jobs and load the new save's rows. No save keeps the current binding.</summary>
        private void Bind()
        {
            SaveData live = _save()?.Current;
            if (ReferenceEquals(live, _session)) return;
            if (live == null && !ReferenceEquals(_session, Unbound)) return;
            _jobs.Clear();
            _session = live;
            CookJobSave.Load(live, _recipes(), _stations, _jobs);
        }

        /// <summary>Game seconds <paramref name="recipe"/> takes at <paramref name="station"/>.</summary>
        public static float CookSeconds(RecipeDef recipe, CookingStationDef station)
        {
            if (recipe == null) return 0f;
            float work = recipe.WorkSeconds > 0f ? recipe.WorkSeconds : 0f;
            return station != null ? work * station.CookTimeMultiplierInForce : work;
        }

        /// <summary>True when the placed station <paramref name="stationKey"/> has something on it.</summary>
        public bool IsCooking(string stationKey, out CookJob job)
        {
            job = default;
            Bind();
            return !string.IsNullOrEmpty(stationKey) && _jobs.TryGetValue(stationKey, out job);
        }

        /// <summary>How many stations are cooking.</summary>
        public int ActiveCount
        {
            get
            {
                Bind();
                return _jobs.Count;
            }
        }

        /// <summary>What <paramref name="station"/> would cook now. Changes nothing and publishes nothing.</summary>
        public CookRefusal Check(CookingStationDef station, string stationKey, out CookPlan plan)
        {
            plan = default;
            if (station == null || string.IsNullOrEmpty(stationKey)) return CookRefusal.NoStation;
            Bind();
            if (_jobs.ContainsKey(stationKey)) return CookRefusal.Busy;
            RecipeBook book = _recipes();
            if (book == null) return CookRefusal.NoStation;

            ICatchHands hands = _hands();
            if (hands != null && hands.TryPeekHand(out CatchItem held) && !string.IsNullOrEmpty(held.SpeciesId))
                return CheckHand(station, book, held, out plan);
            return CheckLocker(station, book, out plan);
        }

        private CookRefusal CheckHand(CookingStationDef station, RecipeBook book, in CatchItem held, out CookPlan plan)
        {
            plan = default;
            if (station.MicrowavableOnly && !IsMicrowavable(held.SpeciesId)) return CookRefusal.NotMicrowavable;
            string[] accepted = station.AcceptedStations;
            if (accepted != null)
                for (int i = 0; i < accepted.Length; i++)
                    if (book.TryFind(accepted[i], held, out RecipeDef recipe))
                    {
                        plan = new CookPlan(recipe, held.SpeciesId, true, CookSeconds(recipe, station));
                        return CookRefusal.None;
                    }
            if (!station.Accepts(RecipeStations.Prep) && book.TryFind(RecipeStations.Prep, held, out _))
                return CookRefusal.NeedsPrep;
            return CookRefusal.NothingToCook;
        }

        private CookRefusal CheckLocker(CookingStationDef station, RecipeBook book, out CookPlan plan)
        {
            plan = default;
            SaveData save = _save()?.Current;
            List<SupplyStock> stock = save?.SupplyStock;
            if (stock == null || stock.Count == 0) return CookRefusal.NothingToCook;

            _scratch.Clear();
            // Through the locker, not the raw count: shopping still in his bag is not cooked until put away.
            for (int i = 0; i < stock.Count; i++)
                if (!string.IsNullOrEmpty(stock[i].SupplyId) && SupplyLocker.Has(save, stock[i].SupplyId))
                    _scratch.Add(stock[i].SupplyId);
            _scratch.Sort(string.CompareOrdinal);

            bool refusedForMicrowave = false;
            string[] accepted = station.AcceptedStations;
            if (accepted == null) return CookRefusal.NothingToCook;
            for (int i = 0; i < _scratch.Count; i++)
            {
                string itemId = _scratch[i];
                RecipeDef found = null;
                for (int s = 0; s < accepted.Length && found == null; s++)
                    book.TryFind(accepted[s], itemId, out found);
                if (found == null) continue;
                if (station.MicrowavableOnly && !IsMicrowavable(itemId)) { refusedForMicrowave = true; continue; }
                plan = new CookPlan(found, itemId, false, CookSeconds(found, station));
                return CookRefusal.None;
            }
            return refusedForMicrowave ? CookRefusal.NotMicrowavable : CookRefusal.NothingToCook;
        }

        private bool IsMicrowavable(string itemId)
        {
            FoodDef food = _foods()?.FindByItem(itemId);
            return food != null && food.Microwavable;
        }

        /// <summary>
        /// Start cooking at the placed station <paramref name="stationKey"/>: check, take one input, and note
        /// when it is ready. Publishes <see cref="FoodCookStarted"/> once, or <see cref="CookRefused"/> once
        /// with nothing taken.
        /// </summary>
        public CookRefusal TryStart(CookingStationDef station, string stationKey)
        {
            CookRefusal refusal = Check(station, stationKey, out CookPlan plan);
            if (refusal != CookRefusal.None) return Refuse(station, stationKey, refusal);

            CatchItem took = default;
            bool taken = plan.FromHand
                ? _hands() != null && _hands().TryTakeFromHand(out took)
                : SupplyLocker.TrySpend(_save()?.Current, plan.InputItemId);
            if (!taken) return Refuse(station, stationKey, CookRefusal.NothingToCook);

            double readyAt = _now() + plan.Seconds;
            var job = new CookJob(station.Id, stationKey, plan.Recipe, readyAt);
            _jobs[stationKey] = job;
            CookJobSave.Write(_session as SaveData, job);
            EventBus.Publish(new FoodCookStarted(station.Id, stationKey, plan.Recipe.Id, plan.InputItemId, readyAt,
                                                 plan.FromHand ? took.WeightKg : 0f));
            return CookRefusal.None;
        }

        /// <summary>
        /// If the placed station's food is done, put one output in the supply locker, publish
        /// <see cref="FoodCooked"/> and free the station. False (and nothing changes) when nothing is on it,
        /// it is not done yet, or there is no save to put the food in. A job whose recipe names no output is
        /// dropped without a <see cref="FoodCooked"/>.
        /// </summary>
        public bool TryFinish(string stationKey, out CookJob finished)
        {
            finished = default;
            if (string.IsNullOrEmpty(stationKey)) return false;
            Bind();
            if (!_jobs.TryGetValue(stationKey, out CookJob job)) return false;
            if (_now() < job.ReadyAt) return false;

            // No save to put it in (pre-boot, mid-load): keep the job and deliver later; never announce food
            // that went nowhere.
            SaveData save = _save()?.Current;
            if (save == null) return false;

            _jobs.Remove(stationKey);
            CookJobSave.Remove(save, stationKey);
            if (job.Recipe == null || string.IsNullOrEmpty(job.Recipe.OutputId)) return false;
            SupplyLocker.Add(save, job.Recipe.OutputId, 1);
            EventBus.Publish(new FoodCooked(job.StationId, stationKey, job.Recipe.Id, job.Recipe.OutputId));
            finished = job;
            return true;
        }

        /// <summary>Forget the jobs held in memory and unbind: the next read reloads them from the live save's
        /// rows (a load, a new game, fixtures). The save's rows are untouched.</summary>
        public void Clear()
        {
            _jobs.Clear();
            _session = Unbound;
        }

        private Action<GameLoaded> _gameLoadedHandler;

        /// <summary>The handler <see cref="GameServices.Cooking"/> subscribes to <see cref="GameLoaded"/>: it
        /// unbinds (<see cref="Clear"/>), so the loaded save's own rows are what is cooking. One cached
        /// delegate, so the same instance unsubscribes cleanly.</summary>
        public Action<GameLoaded> GameLoadedHandler => _gameLoadedHandler ??= _ => Clear();

        private static CookRefusal Refuse(CookingStationDef station, string stationKey, CookRefusal reason)
        {
            EventBus.Publish(new CookRefused(station != null ? station.Id : null, stationKey, reason));
            return reason;
        }
    }

    /// <summary>
    /// <b>Cook jobs in the save</b> (v24, <see cref="SaveData.CookJobs"/>): load them into a
    /// <see cref="RecipeRunner"/> on bind (with the load-time repair), write a row when a job starts, remove
    /// it when the job finishes. Null-safe: a null save reads as no rows and a write to it does nothing.
    /// The row carries the station key, the station kind id, the recipe id and the ready game time; the
    /// output is always one of the recipe's own, into the supply locker, so nothing else is needed.
    /// </summary>
    public static class CookJobSave
    {
        /// <summary>
        /// Add the save's jobs to <paramref name="into"/>, in saved order. A row is dropped from the save
        /// (and not loaded) when it is damaged, repeats a station key, names a recipe <paramref name="recipes"/>
        /// does not hold, or names a station kind <paramref name="stations"/> cannot find. Never throws.
        /// </summary>
        public static void Load(SaveData save, RecipeBook recipes, Func<string, CookingStationDef> stations,
                                Dictionary<string, CookJob> into)
        {
            if (save?.CookJobs == null || into == null) return;
            List<CookJobDto> rows = save.CookJobs;
            for (int i = 0; i < rows.Count; i++)
            {
                CookJobDto r = rows[i];
                RecipeDef recipe = r != null && recipes != null ? recipes.Find(r.RecipeId) : null;
                CookingStationDef station = null;
                if (r != null && stations != null && !string.IsNullOrEmpty(r.StationId))
                {
                    try { station = stations(r.StationId); }
                    catch (Exception) { station = null; }
                }
                bool usable = r != null && !string.IsNullOrEmpty(r.StationKey) && recipe != null && station != null
                              && !double.IsNaN(r.ReadyAtGameSeconds) && !double.IsInfinity(r.ReadyAtGameSeconds)
                              && !into.ContainsKey(r.StationKey);
                if (!usable)
                {
                    rows.RemoveAt(i--);
                    continue;
                }
                into.Add(r.StationKey, new CookJob(station.Id, r.StationKey, recipe, r.ReadyAtGameSeconds));
            }
        }

        /// <summary>Write <paramref name="job"/> as its station's row: the existing row for that key replaced
        /// in place, or a new one appended.</summary>
        public static void Write(SaveData save, in CookJob job)
        {
            if (save == null || string.IsNullOrEmpty(job.StationKey) || job.Recipe == null) return;
            save.CookJobs ??= new List<CookJobDto>();
            CookJobDto row = null;
            for (int i = 0; i < save.CookJobs.Count; i++)
                if (save.CookJobs[i] != null && string.Equals(save.CookJobs[i].StationKey, job.StationKey, StringComparison.Ordinal))
                {
                    row = save.CookJobs[i];
                    break;
                }
            if (row == null)
            {
                row = new CookJobDto { StationKey = job.StationKey };
                save.CookJobs.Add(row);
            }
            row.StationId = job.StationId ?? "";
            row.RecipeId = job.Recipe.Id ?? "";
            row.ReadyAtGameSeconds = job.ReadyAt;
        }

        /// <summary>Remove every row for <paramref name="stationKey"/>.</summary>
        public static void Remove(SaveData save, string stationKey)
        {
            if (save?.CookJobs == null || string.IsNullOrEmpty(stationKey)) return;
            for (int i = save.CookJobs.Count - 1; i >= 0; i--)
                if (save.CookJobs[i] == null || string.Equals(save.CookJobs[i].StationKey, stationKey, StringComparison.Ordinal))
                    save.CookJobs.RemoveAt(i);
        }
    }
}