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

    /// <summary>Something on a station (<see cref="RecipeRunner"/>). Session-only: never saved.</summary>
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
    /// <para><b>Session-only.</b> Jobs live in memory and are never saved (no save version change): a game
    /// saved and quit mid-cook loses what was on the station. A later save step would add one row per job
    /// (station key, recipe id, ready time) to <see cref="SaveData"/>.</para>
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
        private readonly Dictionary<string, CookJob> _jobs = new Dictionary<string, CookJob>(StringComparer.Ordinal);
        private readonly List<string> _scratch = new List<string>();
        private RecipeBook _shippedRecipes;
        private FoodBook _shippedFoods;

        public RecipeRunner(Func<RecipeBook> recipes = null, Func<FoodBook> foods = null,
                            Func<ISaveService> save = null, Func<ICatchHands> hands = null,
                            Func<double> now = null)
        {
            _recipes = recipes ?? (() => _shippedRecipes ??= RecipeBook.FromResources());
            _foods = foods ?? (() => _shippedFoods ??= FoodBook.FromResources());
            _save = save ?? (() => GameServices.Save);
            _hands = hands ?? (() => GameServices.CatchHands);
            _now = now ?? (() => GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d);
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
            return !string.IsNullOrEmpty(stationKey) && _jobs.TryGetValue(stationKey, out job);
        }

        /// <summary>How many stations are cooking.</summary>
        public int ActiveCount => _jobs.Count;

        /// <summary>What <paramref name="station"/> would cook now. Changes nothing and publishes nothing.</summary>
        public CookRefusal Check(CookingStationDef station, string stationKey, out CookPlan plan)
        {
            plan = default;
            if (station == null || string.IsNullOrEmpty(stationKey)) return CookRefusal.NoStation;
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
            List<SupplyStock> stock = _save()?.Current?.SupplyStock;
            if (stock == null || stock.Count == 0) return CookRefusal.NothingToCook;

            _scratch.Clear();
            for (int i = 0; i < stock.Count; i++)
                if (stock[i].Count > 0 && !string.IsNullOrEmpty(stock[i].SupplyId)) _scratch.Add(stock[i].SupplyId);
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

            bool taken = plan.FromHand
                ? _hands() != null && _hands().TryTakeFromHand(out _)
                : SupplyLocker.TrySpend(_save()?.Current, plan.InputItemId);
            if (!taken) return Refuse(station, stationKey, CookRefusal.NothingToCook);

            double readyAt = _now() + plan.Seconds;
            _jobs[stationKey] = new CookJob(station.Id, stationKey, plan.Recipe, readyAt);
            EventBus.Publish(new FoodCookStarted(station.Id, stationKey, plan.Recipe.Id, plan.InputItemId, readyAt));
            return CookRefusal.None;
        }

        /// <summary>
        /// If the placed station's food is done, put one output in the supply locker, publish
        /// <see cref="FoodCooked"/> and free the station. False (and nothing changes) when nothing is on it
        /// or it is not done yet.
        /// </summary>
        public bool TryFinish(string stationKey, out CookJob finished)
        {
            finished = default;
            if (string.IsNullOrEmpty(stationKey) || !_jobs.TryGetValue(stationKey, out CookJob job)) return false;
            if (_now() < job.ReadyAt) return false;

            _jobs.Remove(stationKey);
            SupplyLocker.Add(_save()?.Current, job.Recipe.OutputId, 1);
            EventBus.Publish(new FoodCooked(job.StationId, stationKey, job.Recipe.Id, job.Recipe.OutputId));
            finished = job;
            return true;
        }

        /// <summary>Forget every job (a new session; fixtures).</summary>
        public void Clear() => _jobs.Clear();

        private static CookRefusal Refuse(CookingStationDef station, string stationKey, CookRefusal reason)
        {
            EventBus.Publish(new CookRefused(station != null ? station.Id : null, stationKey, reason));
            return reason;
        }
    }
}
