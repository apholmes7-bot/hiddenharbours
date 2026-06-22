using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Tracks per-category supply at a buyer's market and turns it into live prices. Selling raises a
    /// category's supply (depressing its price); a daily settle on day-rollover clears a fraction of it
    /// (the price recovers). <b>Demand D is per-FishCategory</b> (a buyer can want cod more than
    /// mackerel), so supply AND demand are isolated per category — glutting cod never moves mackerel's
    /// price. A category with no override falls back to the market's baseline demand (from
    /// <see cref="GameConfig"/> by <see cref="MarketId"/>: the cove vs Port Greywick — a reason to
    /// choose where to sell, VS-16). A fuller sim (NPC fleet landings, contracts) is M2 — see
    /// design/economy-and-business.md §1.2–§1.4.
    /// </summary>
    public class Market : MonoBehaviour
    {
        /// <summary>Daily recovery used when no <see cref="GameConfig"/> is assigned (half the glut clears).</summary>
        public const float DefaultDailyRecovery = 0.5f;

        /// <summary>A per-category demand override: this market wants this category at demand D.</summary>
        [System.Serializable]
        public struct CategoryDemand
        {
            public FishCategory Category;
            [Min(0.01f)] public float Demand;
            public CategoryDemand(FishCategory category, float demand) { Category = category; Demand = demand; }
        }

        [SerializeField] private GameConfig _config;
        [Tooltip("Which market this is — sets its BASELINE demand D (read from GameConfig). Default Cove needs no wiring.")]
        [SerializeField] private MarketId _marketId = MarketId.Cove;
        [Tooltip("How much one fish sold adds to its category's supply (the glut per sale).")]
        [Min(0f)] [SerializeField] private float _supplyPerSale = 1f;
        [Tooltip("Per-category demand overrides D in 1/(1+e·S/D). A category not listed uses the baseline " +
                 "demand below. Lets a buyer value cod over mackerel; gluts stay per-category (market depth 2).")]
        [SerializeField] private CategoryDemand[] _categoryDemand;

        private readonly Dictionary<FishCategory, float> _supply = new();
        private bool _subscribed;

        /// <summary>The market's BASELINE demand D (from GameConfig by <see cref="MarketId"/>; 1 if
        /// unconfigured). The default for any category without a per-category override.</summary>
        public float DemandFactor
        {
            get
            {
                if (_config == null) return 1f;
                return _marketId == MarketId.Greywick ? _config.MarketDemandGreywick : _config.MarketDemandCove;
            }
        }

        /// <summary>Demand D for a specific category: its per-category override if listed, else the
        /// market baseline (<see cref="DemandFactor"/>). The single demand source all pricing reads.</summary>
        public float DemandFor(FishCategory category)
        {
            if (_categoryDemand != null)
                for (int i = 0; i < _categoryDemand.Length; i++)
                    if (_categoryDemand[i].Category == category)
                        return Mathf.Max(MarketMath.MinDemand, _categoryDemand[i].Demand);
            return DemandFactor;
        }

        /// <summary>How much one sale adds to supply (the sell screen projects marginal prices with this).</summary>
        public float SupplyPerSale => _supplyPerSale;

        public MarketId Id => _marketId;

        public float SupplyOf(FishCategory category)
            => _supply.TryGetValue(category, out float s) ? s : 0f;

        /// <summary>Live price multiplier for a category at this market's current supply AND per-category demand.</summary>
        public float PriceMultiplier(FishCategory category, float elasticity)
            => MarketMath.PriceMultiplier(SupplyOf(category), elasticity, DemandFor(category));

        /// <summary>
        /// The ₲ price the NEXT unit of this category would fetch right now (current supply + per-category
        /// demand). The sell screen calls this per slider step, projecting supply up by <see cref="SupplyPerSale"/>.
        /// </summary>
        public int NextUnitPrice(FishCategory category, int baseValue, float elasticity)
            => MarketMath.MarginalPrice(baseValue, SupplyOf(category), elasticity, DemandFor(category));

        public void RegisterSale(FishCategory category, int count = 1)
            => _supply[category] = SupplyOf(category) + _supplyPerSale * count;

        // ---- daily settle (deterministic price recovery on day rollover) --------------------

        private void OnEnable()
        {
            if (_subscribed) return;
            EventBus.Subscribe<DayStarted>(OnDayStarted);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<DayStarted>(OnDayStarted);
            _subscribed = false;
        }

        /// <summary>Day-rollover handler — public so EditMode tests can drive the settle without the
        /// play-mode lifecycle (mirrors OwnedFleet.OnBoatPurchased).</summary>
        public void OnDayStarted(DayStarted e) => SettleDaily();

        /// <summary>
        /// Clear a fraction of every category's glut once (the daily settle). Deterministic — driven by
        /// the day rollover, not per-frame time, so the same sell history + day count gives the same
        /// price (rule 5). The fraction comes from GameConfig (owner-tunable), no magic number.
        /// </summary>
        public void SettleDaily()
        {
            if (_supply.Count == 0) return;
            float fraction = _config != null ? _config.MarketDailyRecovery : DefaultDailyRecovery;

            // Copy keys so we can mutate the dictionary while iterating.
            var keys = new List<FishCategory>(_supply.Keys);
            for (int i = 0; i < keys.Count; i++)
                _supply[keys[i]] = MarketMath.SettleSupplyDaily(_supply[keys[i]], fraction);
        }

        /// <summary>Wire the market in one call (tests / editor). Mirrors the Configure pattern used
        /// across the codebase so a Greywick market is a data choice, not a code branch.</summary>
        public void Configure(GameConfig config, MarketId marketId, float supplyPerSale = 1f)
        {
            _config = config;
            _marketId = marketId;
            _supplyPerSale = Mathf.Max(0f, supplyPerSale);
        }

        /// <summary>Set (or override) one category's demand D — for tests / authoring a buyer that values
        /// some catch over others. A demand ≤ 0 is clamped up to <see cref="MarketMath.MinDemand"/>.</summary>
        public void SetCategoryDemand(FishCategory category, float demand)
        {
            var list = _categoryDemand != null
                ? new List<CategoryDemand>(_categoryDemand)
                : new List<CategoryDemand>();
            for (int i = 0; i < list.Count; i++)
                if (list[i].Category == category)
                {
                    list[i] = new CategoryDemand(category, demand);
                    _categoryDemand = list.ToArray();
                    return;
                }
            list.Add(new CategoryDemand(category, demand));
            _categoryDemand = list.ToArray();
        }
    }
}
