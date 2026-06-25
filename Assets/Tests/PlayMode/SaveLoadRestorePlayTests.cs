using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// VS-08 load-restore acceptance — the round-trip integration test. A session grants a boat + a licence
    /// + a repair + gear and advances the clock, SAVES to disk, then a fresh "reloaded" session restores
    /// from that file and resumes EXACTLY where it was saved: the live clock, the live owned fleet (the
    /// active hull + its hold), money, licences, gear, and repaired-boat state all match what was saved —
    /// restored through the same Core service APIs gameplay uses (no reaching into feature internals).
    ///
    /// <para>Plus the determinism guard (CLAUDE.md rule 5): the tide at the restored time equals the tide
    /// computed fresh from <c>(seed, gameTime)</c> — the environment is RECOMPUTED from the restored clock,
    /// never restored from the save. This is the invariant the save system exists to protect.</para>
    ///
    /// <para>Real production logic throughout: the real <see cref="GameClock"/> (and its new
    /// <c>SeekTo</c>), the real <see cref="OwnedFleet"/> restore off <see cref="GameLoaded"/>, the real
    /// <see cref="LicenseService"/>, the real <see cref="RepairLedger"/> / <see cref="PlayerGear"/> save
    /// reads, and the real <see cref="SaveStore"/> disk serialization (to a temp path, never the player's
    /// save). Only the save-service is an in-memory recorder so grants land in the blob without standing up
    /// the whole Economy purchase rig (which has its own tests).</para>
    /// </summary>
    public class SaveLoadRestorePlayTests
    {
        private const string DoryId = "boat.dory";
        private const string PuntId = "boat.punt";
        private const string CodLicense = "license.cod";

        private readonly List<Object> _spawned = new();
        private string _saveDir;

        /// <summary>An in-memory ISaveService: holds the blob the grant flows mutate; persists on demand to a
        /// temp disk path so the test can do a real serialize→deserialize round-trip. Mirrors the real
        /// service's contract (Current is the source of truth; Save writes it) without the singleton/DDOL
        /// lifecycle or the player's real save file.</summary>
        private sealed class MemorySaveService : ISaveService
        {
            private readonly string _path;
            public MemorySaveService(SaveData data, string path) { Current = data; _path = path; }
            public SaveData Current { get; }
            public bool LoadedExistingSave => true;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => SaveStore.Write(Current, _path);   // real atomic disk write
        }

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<GameLoaded>();
            EventBus.Clear<MoneyChanged>();
            GameServices.Reset();
            _saveDir = Path.Combine(Path.GetTempPath(), "hh_save_restore_play");
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<GameLoaded>();
            EventBus.Clear<MoneyChanged>();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, recursive: true);
        }

        // ---- rig builders --------------------------------------------------------------------

        private GameConfig MakeConfig()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();   // sensible defaults (1200 s/day, etc.)
            _spawned.Add(cfg);
            return cfg;
        }

        private BoatHullDef MakeHull(string id, int holdUnits)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = id;
            h.HoldUnits = holdUnits;
            _spawned.Add(h);
            return h;
        }

        // PlayMode runs Awake synchronously on AddComponent for an ACTIVE GameObject — which would fire
        // GameClock/EnvironmentService's "no config" LogError before we could assign _config. So we build
        // the GO INACTIVE, wire the serialized fields, THEN activate: Awake then sees the config (the same
        // create-inactive→configure→activate trick the builders use). Returns the activated component.
        private T AddConfigured<T>(string name, System.Action<T> configure) where T : Component
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
            configure(c);
            go.SetActive(true);             // Awake runs now, with the fields populated
            return c;
        }

        private GameClock MakeClock(GameConfig cfg)
        {
            var clock = AddConfigured<GameClock>("Clock", c => SetPrivate(c, "_config", cfg));
            clock.enabled = false;          // we drive time explicitly (SeekTo), no per-frame drift
            return clock;
        }

        private EnvironmentService MakeEnv(GameConfig cfg, int seed)
            => AddConfigured<EnvironmentService>("Env", e =>
            {
                SetPrivate(e, "_config", cfg);
                SetPrivate(e, "_worldSeed", seed);
            });

        private (OwnedFleet fleet, BoatController boat, ShipHold hold) MakeFleet(BoatHullDef[] registry, BoatHullDef start)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<Rigidbody2D>();
            var boat = go.AddComponent<BoatController>();
            var hold = go.AddComponent<ShipHold>();
            var sr = go.AddComponent<SpriteRenderer>();
            var fleet = go.AddComponent<OwnedFleet>();
            boat.SetHull(start);
            hold.SetHull(start);
            fleet.Configure(registry, boat, hold, sr);
            return (fleet, boat, hold);
        }

        private PlayerWallet MakeWallet()
        {
            var go = new GameObject("Wallet");
            _spawned.Add(go);
            return go.AddComponent<PlayerWallet>();
        }

        private LicenseService MakeLicenses()
        {
            var go = new GameObject("Licenses");
            _spawned.Add(go);
            var svc = go.AddComponent<LicenseService>();
            svc.Register();                 // publishes itself to GameServices.Licenses + self-seeds from save
            return svc;
        }

        /// <summary>Destroy the GameObjects backing these components (and drop them from the spawn list) so a
        /// session's live objects + their static singletons / bus subscriptions don't leak into the next.</summary>
        private void DestroySessionObjects(params Component[] components)
        {
            foreach (var c in components)
            {
                if (c == null) continue;
                var go = c.gameObject;
                _spawned.Remove(go);
                Object.DestroyImmediate(go);
            }
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }

        // ---- the round-trip ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SaveThenReload_RestoresClockFleetMoneyLicenceGearRepair_Exactly()
        {
            const int seed = 4242;
            string path = Path.Combine(_saveDir, "session.json");

            // The state the saved session ends in (clock advanced ~2.6 days into the game, owns+aboard the
            // Punt, holds the cod licence, bought+repaired the Punt, owns the rod + shovel, 1,375 ₲).
            double savedTime = 1200.0 * 2 + 803.5;     // 2 days + 803.5 s — sub-minute precision on purpose
            const int savedMoney = 1375;

            // ---------- SESSION 1: live game, drive grants through the real APIs, SAVE ----------
            {
                var cfg = MakeConfig();
                var dory = MakeHull(DoryId, 6);
                var punt = MakeHull(PuntId, 14);

                var clock = MakeClock(cfg);
                var env = MakeEnv(cfg, seed);
                var wallet = MakeWallet();
                var (fleet, boat, _) = MakeFleet(new[] { dory, punt }, dory);

                GameServices.Clock = clock;
                GameServices.Environment = env;
                GameServices.Wallet = wallet;

                // The save the session writes into — the grant flows mutate Current, the licence service seeds from it.
                var save = new MemorySaveService(SaveMigration.NewGame(), path);
                GameServices.Save = save;

                var licenses = MakeLicenses();              // reads GameServices.Save (empty so far)
                fleet.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));  // aboard so the purchase is "real"

                // advance the clock (time passed this session)
                clock.SeekTo(savedTime);

                // grant a BOAT (the Punt) through the Core signal the Shipwright raises → fleet swaps to it…
                EventBus.Publish(new BoatPurchased(PuntId, 1800));
                Assert.AreSame(punt, boat.Hull, "sanity: session-1 purchase put us in the Punt");
                // …and record ownership in the save the way SaveService's signal handler does.
                save.Current.OwnedBoats.Add(DoryId);
                save.Current.OwnedBoats.Add(PuntId);
                save.Current.ActiveHullId = PuntId;

                // grant a LICENCE through the real service (writes to the blob)
                licenses.Grant(CodLicense);

                // mark a REPAIR through the real bookkeeping (the Shipwright's MarkRepaired)
                RepairLedger.MarkRepaired(save.Current, PuntId);

                // grant GEAR (the rod + shovel) — the owned-gear list the GearShop writes
                save.Current.OwnedGear.Add(PlayerGear.RodId);
                save.Current.OwnedGear.Add(PlayerGear.ShovelId);

                // money the session ended with
                wallet.Add(savedMoney);
                save.Current.Money = wallet.Money;

                // snapshot the live clock into the save (what SaveService.SnapshotLiveState does)
                save.Current.GameTimeSeconds = clock.TotalSeconds;
                save.Current.WorldSeed = env.WorldSeed;

                save.Save();                                 // ← write to disk (atomic)
                Assert.IsTrue(File.Exists(path), "the session must have written a save file");

                // Tear the session down so nothing leaks into the reload: destroy the live objects (their
                // OnDisable/OnDestroy unsubscribe + clear the static singletons), reset the locator, and clear
                // the buses. The reloaded session then stands up a genuinely fresh game.
                DestroySessionObjects(clock, env, wallet, boat, licenses);
                GameServices.Reset();
                EventBus.Clear<BoatPurchased>();
                EventBus.Clear<ActiveBoatChanged>();
                EventBus.Clear<ControlModeChanged>();
                EventBus.Clear<GameLoaded>();
            }

            yield return null;   // a frame between the two "launches" (lets Destroy + OnDestroy flush)

            // ---------- SESSION 2: fresh launch, READ the save, RESTORE, assert resume ----------
            {
                SaveData reloaded = SaveStore.Read(path);     // real deserialize + migrate
                Assert.IsNotNull(reloaded, "the save must read back from disk");

                var cfg = MakeConfig();
                var dory = MakeHull(DoryId, 6);
                var punt = MakeHull(PuntId, 14);

                var clock = MakeClock(cfg);
                var env = MakeEnv(cfg, reloaded.WorldSeed);
                var wallet = MakeWallet();                    // fresh wallet — starts at 0
                // fresh fleet — starts in the Dory; its Awake subscribes OnGameLoaded so the restore fires on the signal
                var (_, boat, hold) = MakeFleet(new[] { dory, punt }, dory);

                GameServices.Clock = clock;
                GameServices.Environment = env;
                GameServices.Wallet = wallet;
                GameServices.Save = new MemorySaveService(reloaded, path);

                var licenses = MakeLicenses();                // fresh licence wallet
                // The fleet subscribes to GameLoaded in its own Awake (active GO in PlayMode), so the runtime
                // path is already wired; we don't re-subscribe (that would just double the idempotent handler).

                Assert.AreSame(dory, boat.Hull, "sanity: the reloaded session starts in the default Dory…");
                Assert.AreEqual(0, wallet.Money, "…and a fresh wallet at 0");

                // THE RESTORE — exactly what GameRoot.Start() runs, through the same Core APIs.
                SaveRestore.ApplyToLiveServices(reloaded, clock, wallet, licenses);

                // ---- assert the LIVE game resumed exactly where it was saved ----
                Assert.AreEqual(savedTime, clock.TotalSeconds, 1e-6,
                    "CLOCK: the live clock is at the saved game-time (sub-minute precision).");
                Assert.AreSame(punt, boat.Hull,
                    "FLEET: the live boat is the saved active hull (the Punt), not the default Dory.");
                Assert.AreEqual(14, hold.CapacityUnits,
                    "FLEET: the restored hull's hold capacity is live (Punt = 14 HU).");
                Assert.AreEqual(savedMoney, wallet.Money,
                    "MONEY: the live wallet is at the saved balance.");
                Assert.IsTrue(licenses.IsLicensed(CodLicense),
                    "LICENCE: the live licence wallet holds the saved cod licence.");
                Assert.IsTrue(PlayerGear.Owns(reloaded, PlayerGear.RodId),
                    "GEAR: the rod is owned in the restored save (PlayerGear reads it live).");
                Assert.IsTrue(PlayerGear.Owns(reloaded, PlayerGear.ShovelId),
                    "GEAR: the shovel is owned in the restored save.");
                Assert.IsTrue(RepairLedger.IsRepaired(reloaded, PuntId),
                    "REPAIR: the Punt is marked repaired (usable) in the restored save.");

                // ---- DETERMINISM GUARD: tide is RECOMPUTED from the restored clock, never restored ----
                float tideAtRestoredClock = env.TideHeightAt(clock.TotalSeconds);
                float tideFreshFromSavedTime = env.TideHeightAt(reloaded.GameTimeSeconds);
                float tideFromModel = TideModel.Height(savedTime, TideProfile.CoddleCove, cfg);

                Assert.AreEqual(tideFreshFromSavedTime, tideAtRestoredClock, 1e-6f,
                    "the tide at the restored clock equals the tide at the saved gameTime — recomputed, not stored.");
                Assert.AreEqual(tideFromModel, tideAtRestoredClock, 1e-4f,
                    "and that equals a fresh (seed, time) computation from the pure tide model (P1 determinism).");
            }

            yield return null;
        }

        // ---- clock seek determinism: SeekTo is a pure set, no rollover replay ----------------

        [UnityTest]
        public IEnumerator SeekTo_LandsExactly_AndDoesNotReplayDayRollovers()
        {
            var cfg = MakeConfig();
            // An ENABLED clock here (unlike MakeClock's): we want a real Update to tick AFTER the seek, to
            // prove SeekTo re-baselined the rollover guard so the next frame doesn't fire a spurious morning.
            var clock = AddConfigured<GameClock>("LiveClock", c => SetPrivate(c, "_config", cfg));
            GameServices.Clock = clock;
            yield return null;   // Awake ran, _t is at the start hour, rollover guard baselined to day 0

            int dayStarts = 0;
            void OnDay(DayStarted e) => dayStarts++;
            EventBus.Subscribe<DayStarted>(OnDay);
            try
            {
                // Seek several days forward — a RESTORE, not a fast-forward: no DayStarted should fire for
                // the jumped span (the player is already on that day; we don't re-announce the mornings).
                clock.SeekTo(cfg.SecondsPerDay * 3 + 500);
                yield return null;   // a real Update ticks; the guard was re-baselined by SeekTo → no replay
                yield return null;

                Assert.GreaterOrEqual(clock.TotalSeconds, cfg.SecondsPerDay * 3 + 500,
                    "the clock is at (or just past, from the live ticks) the seeked time");
                Assert.AreEqual(0, dayStarts,
                    "seeking (a restore) must NOT replay the day-rollover events for the jumped span");
                Assert.AreEqual(3, clock.DayIndex, "and the calendar reads the restored day");
            }
            finally { EventBus.Unsubscribe<DayStarted>(OnDay); }
        }
    }
}
