using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>Puts the island stores' shelves into the running game</b> (store sold-out slice). Self-installing,
    /// like <see cref="LicenseService"/>: after the first scene loads it builds an <see cref="IslandStoreShelves"/>
    /// for every <see cref="IslandStoreShelvesDef"/> under <c>Resources/IslandSupply/Shelves</c>, registers
    /// each in the Core <see cref="IslandShelvesDirectory"/> (so the World module's sold-out cards read them
    /// without naming this class) and binds the counter's listings in <see cref="IslandCounterShelves"/>.
    ///
    /// <para><b>Nothing is saved.</b> The shelves are recomputed from the clock and the tide; the player's
    /// purchases are held for the session and dropped when a game is loaded (saving them is the island
    /// economy's slice 8). The tide is the environment's water level, which in this archipelago is the one
    /// bar's tide in every region.</para>
    ///
    /// <para><b>Cheap.</b> It re-reads the shelves every <see cref="IslandStoreShelvesDef.RecheckGameMinutes"/>
    /// game minutes, not per frame, and publishes <see cref="IslandShelvesChanged"/> only when a line's whole
    /// item count moves. The steady state allocates nothing.</para>
    /// </summary>
    public sealed class IslandShelvesHost : MonoBehaviour
    {
        /// <summary>Where the shelves Defs are loaded from (under a Resources folder).</summary>
        public const string ShelvesResourcesFolder = "IslandSupply/Shelves";

        /// <summary>Where the carrier Defs (the runner's kit) are loaded from.</summary>
        public const string CarriersResourcesFolder = "IslandSupply/Carriers";

        /// <summary>Blocks of run days the calendar keeps: the replay reaches back one lookback, so the
        /// current block and the one before always cover it, and a third avoids churn at a boundary.</summary>
        private const int RunDayBlocksKept = 3;

        private const double SecondsPerMinute = 60.0;

        private sealed class Store
        {
            public IslandStoreShelvesDef Def;
            public IslandStoreShelves Shelves;
            public IslandShelfTill Till;
            public string[] Lines;
            public int[] Last;
            public double RecheckSeconds;
        }

        private static IslandShelvesHost _instance;
        private readonly List<Store> _stores = new List<Store>();
        private bool _built;
        private double _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[IslandShelvesHost]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<IslandShelvesHost>();
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            EventBus.Subscribe<IslandShelvesChanged>(OnChanged);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<IslandShelvesChanged>(OnChanged);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            TearDown();
            if (_instance == this) _instance = null;
        }

        private void OnGameLoaded(GameLoaded _)
        {
            // A loaded game is the whole truth: this session's purchases belong to the game that was left.
            TearDown();
        }

        private void Update()
        {
            IGameClock clock = GameServices.Clock;
            if (clock == null) return;
            if (!_built)
            {
                if (!TryBuild()) return;
                _nextCheck = double.NegativeInfinity;
            }
            double now = clock.TotalSeconds;
            if (now < _nextCheck && now >= _nextCheck - MinRecheck()) return;
            Recheck(now);
        }

        private double MinRecheck()
        {
            double min = double.PositiveInfinity;
            for (int i = 0; i < _stores.Count; i++) min = Math.Min(min, _stores[i].RecheckSeconds);
            return double.IsInfinity(min) ? 0.0 : min;
        }

        /// <summary>Re-reads every line at <paramref name="now"/> and publishes the ones whose count moved.
        /// Public so a test or a probe can drive it without the frame loop.</summary>
        public void Recheck(double now)
        {
            for (int s = 0; s < _stores.Count; s++)
            {
                Store store = _stores[s];
                store.Shelves.SlowTick(now);
                for (int i = 0; i < store.Lines.Length; i++)
                {
                    int count = store.Shelves.CountAt(store.Lines[i], now);
                    if (count == store.Last[i]) continue;
                    store.Last[i] = count;
                    EventBus.Publish(new IslandShelvesChanged(store.Shelves.ShopId, store.Lines[i], count));
                }
            }
            _nextCheck = now + MinRecheck();
        }

        private void OnChanged(IslandShelvesChanged change)
        {
            // A purchase at the counter publishes its own change; keep the last count so it is not re-sent.
            for (int s = 0; s < _stores.Count; s++)
            {
                Store store = _stores[s];
                if (!string.Equals(store.Shelves.ShopId, change.ShopId, StringComparison.Ordinal)) continue;
                for (int i = 0; i < store.Lines.Length; i++)
                    if (string.Equals(store.Lines[i], change.LineId, StringComparison.Ordinal)) store.Last[i] = change.Count;
            }
        }

        private bool TryBuild()
        {
            IEnvironmentService env = GameServices.Environment;
            GameConfig cfg = GameServices.Config;
            if (env == null || cfg == null) return false;

            IslandSupplyConfig route = Resources.Load<IslandSupplyConfig>(IslandSupplyConfig.ResourcesPath);
            IslandStoreShelvesDef[] defs = Resources.LoadAll<IslandStoreShelvesDef>(ShelvesResourcesFolder);
            var carriers = new Dictionary<string, CarrierDef>(StringComparer.Ordinal);
            foreach (CarrierDef c in Resources.LoadAll<CarrierDef>(CarriersResourcesFolder))
                if (c != null && !string.IsNullOrEmpty(c.Id)) carriers[c.Id] = c;

            _built = true;   // one attempt per game: a content mistake is logged once, not every frame
            if (route == null || defs == null) return true;

            Func<double, float> waterAt = t => env.WaterLevelAt(t);
            foreach (IslandStoreShelvesDef def in defs)
            {
                try
                {
                    IslandStoreShelves shelves = Build(def, route, carriers, cfg, waterAt);
                    var store = new Store
                    {
                        Def = def,
                        Shelves = shelves,
                        Lines = new string[def.Lines.Count],
                        Last = new int[def.Lines.Count],
                        RecheckSeconds = def.RecheckGameMinutes * SecondsPerMinute,
                    };
                    for (int i = 0; i < store.Lines.Length; i++) { store.Lines[i] = def.Lines[i].Id; store.Last[i] = -1; }
                    _stores.Add(store);
                    IslandShelvesDirectory.Register(shelves);
                    IslandCounterShelves.Bind(def, shelves);
                    store.Till = new IslandShelfTill(def.ShopId, def.SellerId);
                    ShelfTills.Register(store.Till);   // buying at the shelf: the World's shelf goods ask this
                }
                catch (Exception e)
                {
                    Debug.LogError("[IslandShelvesHost] " + (def != null ? def.Id : "null") + " not built: " + e.Message);
                }
            }
            return true;
        }

        /// <summary>
        /// The shelves one Def describes, over its ledger, with the run calendar read from
        /// <paramref name="waterAt"/> for any day of an open-ended game. The composition the game runs, public
        /// so a test builds the same thing from the shipped assets.
        /// </summary>
        public static IslandStoreShelves Build(IslandStoreShelvesDef def, IslandSupplyConfig route,
                                               IReadOnlyDictionary<string, CarrierDef> carriers, GameConfig cfg,
                                               Func<double, float> waterAt)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (def.Ledger == null) throw new ArgumentException("Shelves have no back room: " + def.Id);
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var days = new IslandRunDays(waterAt, def.Ledger.Runner, route, cfg,
                                         Math.Max(1, def.Ledger.LookbackDays), RunDayBlocksKept);
            return IslandStoreShelves.FromDef(def, route, carriers, days.Possible, cfg.SecondsPerDay);
        }

        private void TearDown()
        {
            for (int s = 0; s < _stores.Count; s++)
            {
                IslandShelvesDirectory.Unregister(_stores[s].Shelves);
                IslandCounterShelves.Unbind(_stores[s].Shelves);
                ShelfTills.Unregister(_stores[s].Till);
            }
            _stores.Clear();
            _built = false;
        }
    }
}
