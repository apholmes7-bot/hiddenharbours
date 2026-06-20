using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Player-facing fishing: on a cast it builds the <see cref="CatchContext"/> from the live clock
    /// and environment, resolves a catch, and drops it in the boat's hold (an <see cref="IHold"/>),
    /// raising <see cref="FishCaught"/>. Input is fed by DevFishingInput now, the InputService later.
    /// </summary>
    public class FishingController : MonoBehaviour
    {
        [SerializeField] private string _regionId = "region.coddle_cove";
        [SerializeField] private Gear _gear = Gear.Handline;
        [Tooltip("The fish that can be caught in this region (region scenes provide these).")]
        [SerializeField] private FishSpeciesDef[] _regionFish;
        [Tooltip("A GameObject carrying an IHold (the boat's ShipHold).")]
        [SerializeField] private GameObject _holdProvider;
        [Tooltip("0 = time-seeded RNG; set non-zero for reproducible catches in testing.")]
        [SerializeField] private int _rngSeed = 0;

        private IHold _hold;
        private System.Random _rng;

        public Gear Gear { get => _gear; set => _gear = value; }

        private void Awake()
        {
            _rng = _rngSeed == 0 ? new System.Random() : new System.Random(_rngSeed);
            if (_holdProvider != null) _hold = _holdProvider.GetComponent<IHold>();
            if (_hold == null)
                Debug.LogWarning("[FishingController] No IHold found on the hold provider.", this);
        }

        /// <summary>Attempt one cast. Returns true if a fish was landed.</summary>
        public bool TryCast()
        {
            if (_hold == null) return false;
            if (_hold.UsedUnits >= _hold.CapacityUnits)
            {
                Debug.Log("[Fishing] Hold is full — head in and sell.");
                return false;
            }

            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            float tide = env != null ? env.Sample().TideHeight : 0f;
            float hour = clock != null ? clock.HourOfDay : 12f;
            Season season = clock != null ? clock.Season : Season.HighSummer;

            var ctx = new CatchContext(_regionId, tide, hour, season, _gear);
            FishSpeciesDef fish = CatchResolver.Resolve(_regionFish, in ctx, _rng);
            if (fish == null)
            {
                Debug.Log("[Fishing] Nothing biting here, now.");
                return false;
            }

            float weight = CatchResolver.RollWeight(fish, _rng);
            var item = new CatchItem(fish.Id, fish.DisplayName, fish.Category,
                                     weight, fish.BaseValue, fish.SupplyElasticity);

            if (_hold.TryAdd(item))
            {
                EventBus.Publish(new FishCaught(item));
                Debug.Log($"[Fishing] Caught {item}!");
                return true;
            }
            return false;
        }
    }
}
