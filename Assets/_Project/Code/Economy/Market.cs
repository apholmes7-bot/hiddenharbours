using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Tracks per-category supply at Port Greywick and turns it into live prices. Selling raises a
    /// category's supply (depressing its price); supply decays over time (price recovers). A fuller
    /// sim (NPC fleet landings, multiple buyers, contracts) comes in M2 — see design/economy-and-business.md.
    /// </summary>
    public class Market : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [Tooltip("How much one fish sold adds to its category's supply.")]
        [SerializeField] private float _supplyPerSale = 1f;
        [Tooltip("How fast supply decays back toward zero, per in-game hour.")]
        [SerializeField] private float _supplyDecayPerHour = 2f;

        private readonly Dictionary<FishCategory, float> _supply = new();

        public float SupplyOf(FishCategory category)
            => _supply.TryGetValue(category, out float s) ? s : 0f;

        public float PriceMultiplier(FishCategory category, float elasticity)
            => MarketMath.PriceMultiplier(SupplyOf(category), elasticity);

        public void RegisterSale(FishCategory category, int count = 1)
            => _supply[category] = SupplyOf(category) + _supplyPerSale * count;

        private void Update()
        {
            if (_supply.Count == 0) return;
            float perSecond = _config != null
                ? _supplyDecayPerHour / _config.SecondsPerHour
                : _supplyDecayPerHour / 3600f;

            // Copy keys so we can mutate the dictionary while iterating.
            var keys = new List<FishCategory>(_supply.Keys);
            for (int i = 0; i < keys.Count; i++)
                _supply[keys[i]] = MarketMath.DecaySupply(_supply[keys[i]], perSecond, Time.deltaTime);
        }
    }
}
