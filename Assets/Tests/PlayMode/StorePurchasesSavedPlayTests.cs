using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A shelf he bought empty is still empty after the game is closed and started again</b> (the store
    /// purchases over save v26's rows), with the store the running game builds: the shipped shelves, built by
    /// <see cref="IslandShelvesHost"/> from the clock and the water, reached only through the Core directory.
    /// Every item taken is written into the game as it is taken and a write is asked for; after the save has
    /// been through its real JSON and the game is loaded, the host builds the store again, the shelf is still
    /// sold out, the world is told so at once (what the sold-out cards listen to), and the shelf fills again
    /// when the uninterrupted game's shelf does. Headless-safe: nothing renders.
    /// </summary>
    public class StorePurchasesSavedPlayTests
    {
        private sealed class DrivenClock : IGameClock
        {
            public double Seconds;
            public float DayLength = GameConfig.DefaultSecondsPerDay;
            public double TotalSeconds => Seconds;
            public GameTime Now => new GameTime(Seconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => (int)(Seconds / DayLength);
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => (float)(Seconds / DayLength % 1.0) * 24f;
            public float DayFraction => (float)(Seconds / DayLength % 1.0);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        // The bar is dry all day: the supply run can always come.
        private sealed class LowWater : IEnvironmentService
        {
            public int WorldSeed => 11;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => -50f;
            public float WaterLevelAt(double totalSeconds) => -50f;
        }

        private sealed class FakeSave : ISaveService
        {
            public FakeSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; }
        }

        private readonly List<IslandShelvesChanged> _changes = new();
        private System.Action<IslandShelvesChanged> _onChange;
        private GameConfig _cfg;
        private DrivenClock _clock;
        private GameObject _ownHost;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _cfg = ScriptableObject.CreateInstance<GameConfig>();
            _clock = new DrivenClock { DayLength = _cfg.SecondsPerDay };
            GameServices.Config = _cfg;
            GameServices.Clock = _clock;
            GameServices.Environment = new LowWater();
            _changes.Clear();
            _onChange = c => _changes.Add(c);
            EventBus.Subscribe(_onChange);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onChange);
            GameServices.Reset();
            EventBus.Publish(new GameLoaded());   // the host lets go of the stores built for this test
            if (_ownHost != null) Object.Destroy(_ownHost);
            if (_cfg != null) Object.Destroy(_cfg);
        }

        private void EnsureHost()
        {
            IslandShelvesHost host = Object.FindFirstObjectByType<IslandShelvesHost>();
            if (host == null)
            {
                _ownHost = new GameObject("[IslandShelvesHost test]");
                _ownHost.AddComponent<IslandShelvesHost>();
                return;
            }
            // The fixture's service reset drops every listener: the running host listens again.
            host.enabled = false;
            host.enabled = true;
        }

        // A loaded game: the host tears its stores down and builds them again on its next frame.
        private IEnumerator LoadGame(SaveData save)
        {
            GameServices.Save = new FakeSave(save);
            EventBus.Publish(new GameLoaded());
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeBuysAShelfEmpty_ClosesTheGame_AndItIsStillSoldOut_UntilTheRunComes()
        {
            IslandStoreShelvesDef[] defs = Resources.LoadAll<IslandStoreShelvesDef>(IslandShelvesHost.ShelvesResourcesFolder);
            Assert.That(defs.Length, Is.GreaterThan(0), "a store's shelves ship");
            IslandStoreShelvesDef def = defs[0];
            double day = _cfg.SecondsPerDay;

            EnsureHost();
            SaveData save = SaveMigration.NewGame();
            _clock.Seconds = 10.0 * day;
            yield return LoadGame(save);
            Assert.IsTrue(IslandShelvesDirectory.TryGet(def.ShopId, out IIslandShelves shelves), "the game built the store");

            // A morning with something on a shelf.
            string line = null;
            double now = 0.0;
            for (int d = 10; d < 40 && line == null; d++)
                foreach (IslandShelfLine l in def.Lines)
                {
                    double t = (d + 10.0 / 24.0) * day;
                    if (shelves.CountAt(l.Id, t) < 1) continue;
                    line = l.Id;
                    now = t;
                    break;
                }
            Assert.IsNotNull(line, "some shelf holds something on some morning");
            _clock.Seconds = now;
            yield return null;

            var fake = (FakeSave)GameServices.Save;
            int savesBefore = fake.Saves;
            int before = shelves.CountAt(line, now);
            int bought = 0;
            while (shelves.TryTakeOne(line, now)) bought++;
            Assert.That(bought, Is.GreaterThan(0));
            Assert.That(shelves.CountAt(line, now), Is.EqualTo(0), "bought empty");
            Assert.That(fake.Saves - savesBefore, Is.EqualTo(bought), "each purchase asks for a write");
            Assert.That(StorePurchaseRows.CountFor(save.StorePurchases, def.ShopId), Is.GreaterThan(0), "and is in the game");
            double units = 0.0;
            foreach (StorePurchaseRowDto row in save.StorePurchases)
            {
                Assert.That(row.ShopId, Is.EqualTo(def.ShopId));
                Assert.That(row.ShelfLineId, Is.EqualTo(line));
                units += row.Units;
            }
            Assert.That(units, Is.GreaterThan(0.0));
            Assert.That(save.SpecialOrders.Count, Is.EqualTo(0), "special orders are not touched");

            // The game is closed and started again: only the file survives.
            string onDisk = SaveSerialization.ToJson(save);
            SaveData loaded = SaveSerialization.FromJson(onDisk);
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(onDisk), "the load repair mends nothing in what the store wrote");
            _changes.Clear();
            yield return LoadGame(loaded);

            Assert.IsTrue(IslandShelvesDirectory.TryGet(def.ShopId, out IIslandShelves rebuilt));
            Assert.That(rebuilt, Is.Not.SameAs(shelves), "a new store, built for the loaded game");
            Assert.That(rebuilt.CountAt(line, now), Is.EqualTo(0), "the shelf is still sold out");
            Assert.IsFalse(rebuilt.TryTakeOne(line, now));
            bool told = false;
            foreach (IslandShelvesChanged c in _changes)
                if (c.ShopId == def.ShopId && c.LineId == line && c.Count == 0) told = true;
            Assert.IsTrue(told, "and the world is told at once, so the sold-out card is back");

            // It fills again when the shelf of the game that was never closed fills again.
            double restock = -1.0;
            for (int d = 0; d <= def.Ledger.LookbackDays + 2 && restock < 0.0; d++)
                foreach (double hour in new[] { 10.0, 14.0, 18.0 })
                {
                    double t = now - (10.0 / 24.0) * day + (d + hour / 24.0) * day;
                    if (t <= now) continue;
                    Assert.That(rebuilt.CountAt(line, t), Is.EqualTo(shelves.CountAt(line, t)), "day +" + d + " " + hour + "h");
                    if (shelves.CountAt(line, t) > 0 && restock < 0.0) restock = t;
                }
            Assert.That(restock, Is.GreaterThan(now), "the run comes and the shelf fills again");
            Assert.That(rebuilt.CountAt(line, restock), Is.GreaterThan(0));

            // A new game has none of it.
            yield return LoadGame(SaveMigration.NewGame());
            Assert.IsTrue(IslandShelvesDirectory.TryGet(def.ShopId, out IIslandShelves fresh));
            Assert.That(fresh.CountAt(line, now), Is.EqualTo(before), "a new game's shelf is the calendar's");
        }
    }
}
