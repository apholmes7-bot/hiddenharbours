using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Tracks per-category supply at a buyer's market and turns it into live prices. Selling raises a
    /// category's supply (depressing its price); a daily settle on day-rollover clears a fraction of it
    /// (the price recovers). Each market reads its own <b>demand</b> D from <see cref="GameConfig"/> by
    /// <see cref="MarketId"/>, so Port Greywick prices a glut differently from the cove — a reason to
    /// choose where to sell (VS-16). A fuller sim (NPC fleet landings, contracts, per-category demand)
    /// is M2 — see design/economy-and-business.md §1.2–§1.4.
    /// </summary>
    public class Market : MonoBehaviour
    {
        /// <summary>Daily recovery used when no <see cref="GameConfig"/> is assigned (half the glut clears).</summary>
        public const float DefaultDailyRecovery = 0.5f;

        [SerializeField] private GameConfig _config;
        [Tooltip("Which market this is — sets its demand D (read from GameConfig). Default Cove needs no wiring.")]
        [SerializeField] private MarketId _marketId = MarketId.Cove;
        [Tooltip("How much one fish sold adds to its category's supply (the glut per sale).")]
        [Min(0f)] [SerializeField] private float _supplyPerSale = 1f;

        private readonly Dictionary<FishCategory, float> _supply = new();
        private bool _subscribed;

        /// <summary>This market's demand D (from GameConfig by <see cref="MarketId"/>; 1 if unconfigured).</summary>
        public float DemandFactor
        {
            get
            {
                if (_config == null) return 1f;
                return _marketId == MarketId.Greywick ? _config.MarketDemandGreywick : _config.MarketDemandCove;
            }
        }

        /// <summary>How much one sale adds to supply (the sell screen projects marginal prices with this).</summary>
        public float SupplyPerSale => _supplyPerSale;

        public MarketId Id => _marketId;

        public float SupplyOf(FishCategory category)
            => _supply.TryGetValue(category, out float s) ? s : 0f;

        /// <summary>Live price multiplier for a category at this market's current supply AND demand.</summary>
        public float PriceMultiplier(FishCategory category, float elasticity)
            => MarketMath.PriceMultiplier(SupplyOf(category), elasticity, DemandFactor);

        /// <summary>
        /// The ₲ price the NEXT unit of this category would fetch right now (current supply + demand).
        /// The sell screen calls this per slider step, projecting supply up by <see cref="SupplyPerSale"/>.
        /// </summary>
        public int NextUnitPrice(FishCategory category, int baseValue, float elasticity)
            => MarketMath.MarginalPrice(baseValue, SupplyOf(category), elasticity, DemandFactor);

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
    }
}
