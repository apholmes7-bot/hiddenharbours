using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-08 load-restore — the pure mapping that re-applies a loaded <see cref="SaveData"/> to the live
    /// Core services so a save resumes exactly where it was saved. Covers the three pushed restores (clock
    /// seek, money-to-balance via the wallet API, idempotent licence grants) and the <see cref="GameLoaded"/>
    /// announcement, plus the new-game guard (null data writes nothing but still announces). All headless:
    /// <see cref="SaveRestore"/> is static + service-injected, so no scene / no GameServices globals.
    /// </summary>
    public class SaveRestoreTests
    {
        // ---- minimal Core-contract fakes (faithful to the real semantics) -----------------------

        /// <summary>A clock that records the last absolute seek (the only behaviour restore drives).</summary>
        private sealed class FakeClock : IGameClock
        {
            public double SeekedTo = double.NaN;
            public int SeekCount;
            public double TotalSeconds { get; private set; }
            public void SeekTo(double totalSeconds)
            {
                SeekedTo = totalSeconds < 0d ? 0d : totalSeconds;
                TotalSeconds = SeekedTo;
                SeekCount++;
            }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>An IWallet that accumulates — same money rule as PlayerWallet.</summary>
        private sealed class FakeWallet : IWallet
        {
            public int Money { get; private set; }
            public FakeWallet(int starting = 0) { Money = starting; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount < 0 || amount > Money) return false; Money -= amount; return true; }
        }

        /// <summary>An ILicenseService that records grants — same idempotence as the real one.</summary>
        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public readonly List<string> GrantCalls = new();
            public bool IsLicensed(string id) => string.IsNullOrEmpty(id) || _held.Contains(id);
            public void Grant(string id)
            {
                if (string.IsNullOrEmpty(id)) return;
                GrantCalls.Add(id);     // records EVERY call so we can assert idempotence at the source
                _held.Add(id);
            }
            public int Count => _held.Count;
        }

        private int _gameLoadedRaised;
        private void OnGameLoaded(GameLoaded _) => _gameLoadedRaised++;

        [SetUp]
        public void SetUp()
        {
            _gameLoadedRaised = 0;
            EventBus.Clear<GameLoaded>();
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            EventBus.Clear<GameLoaded>();
        }

        private static SaveData Saved(double t, int money, params string[] licenses) => new SaveData
        {
            SchemaVersion   = SaveMigration.CurrentVersion,
            GameTimeSeconds = t,
            Money           = money,
            OwnedLicenses   = new List<string>(licenses),
        };

        // ---- clock ------------------------------------------------------------------------------

        [Test]
        public void RestoreClock_SeeksToSavedGameTime()
        {
            var clock = new FakeClock();
            SaveRestore.RestoreClock(Saved(54321.5, 0), clock);

            Assert.AreEqual(1, clock.SeekCount, "the clock is seeked exactly once");
            Assert.AreEqual(54321.5, clock.SeekedTo, 1e-9, "the clock seeks to the saved gameTime at full precision");
        }

        [Test]
        public void RestoreClock_NullData_OrNullClock_IsNoOp()
        {
            var clock = new FakeClock();
            Assert.DoesNotThrow(() => SaveRestore.RestoreClock(null, clock));
            Assert.AreEqual(0, clock.SeekCount, "null data must not seek the clock");
            Assert.DoesNotThrow(() => SaveRestore.RestoreClock(Saved(10, 0), null));
        }

        // ---- money ------------------------------------------------------------------------------

        [Test]
        public void RestoreMoney_BringsWalletToSavedBalance_FromZero()
        {
            var wallet = new FakeWallet(0);
            SaveRestore.RestoreMoney(Saved(0, 250), wallet);
            Assert.AreEqual(250, wallet.Money, "wallet is brought up to the saved balance");
        }

        [Test]
        public void RestoreMoney_AppliesSignedDelta_WhenWalletStartsNonZero()
        {
            var wallet = new FakeWallet(1000);                 // greybox could spawn with seed money
            SaveRestore.RestoreMoney(Saved(0, 250), wallet);    // saved < current → wallet drops to saved
            Assert.AreEqual(250, wallet.Money, "the signed delta brings it DOWN to the saved balance, not up");
        }

        [Test]
        public void RestoreMoney_AlreadyAtBalance_IsNoOp()
        {
            var wallet = new FakeWallet(250);
            SaveRestore.RestoreMoney(Saved(0, 250), wallet);
            Assert.AreEqual(250, wallet.Money, "already at the saved balance → unchanged (delta 0)");
        }

        // ---- licences ---------------------------------------------------------------------------

        [Test]
        public void RestoreLicenses_GrantsEverySavedLicence_Idempotently()
        {
            var licenses = new FakeLicenses();
            // A saved wallet with a duplicate + an empty id: the empty is skipped, the dup is granted once.
            var data = Saved(0, 0, "license.cod", "license.cod", "", "license.lobster");

            SaveRestore.RestoreLicenses(data, licenses);

            Assert.IsTrue(licenses.IsLicensed("license.cod"));
            Assert.IsTrue(licenses.IsLicensed("license.lobster"));
            Assert.AreEqual(2, licenses.Count, "two distinct licences held (the duplicate collapses, empty skipped)");
        }

        [Test]
        public void RestoreLicenses_NullList_IsNoOp()
        {
            var licenses = new FakeLicenses();
            var data = new SaveData { OwnedLicenses = null };
            Assert.DoesNotThrow(() => SaveRestore.RestoreLicenses(data, licenses));
            Assert.AreEqual(0, licenses.Count);
        }

        // ---- the whole apply --------------------------------------------------------------------

        [Test]
        public void ApplyToLiveServices_RestoresAll_AndPublishesGameLoaded()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0);
            var licenses = new FakeLicenses();
            var data = Saved(12345.678, 500, "license.cod");

            SaveRestore.ApplyToLiveServices(data, clock, wallet, licenses);

            Assert.AreEqual(12345.678, clock.SeekedTo, 1e-9, "clock restored");
            Assert.AreEqual(500, wallet.Money, "money restored");
            Assert.IsTrue(licenses.IsLicensed("license.cod"), "licence restored");
            Assert.AreEqual(1, _gameLoadedRaised, "GameLoaded is published exactly once");
        }

        [Test]
        public void ApplyToLiveServices_NullData_WritesNothing_ButStillAnnounces()
        {
            // The new-game path: GameRoot passes null so the authored start hour stands. Nothing is written,
            // but GameLoaded still fires so subscribers (the owned fleet) have one code path.
            var clock = new FakeClock();
            var wallet = new FakeWallet(777);

            SaveRestore.ApplyToLiveServices(null, clock, wallet, null);

            Assert.AreEqual(0, clock.SeekCount, "null data must NOT seek the clock (keeps the new-game start hour)");
            Assert.AreEqual(777, wallet.Money, "null data must not touch the wallet");
            Assert.AreEqual(1, _gameLoadedRaised, "GameLoaded still fires for a new game");
        }

        [Test]
        public void ApplyToLiveServices_PublishLoadedFalse_DoesNotAnnounce()
        {
            var clock = new FakeClock();
            SaveRestore.ApplyToLiveServices(Saved(10, 0), clock, null, null, publishLoaded: false);
            Assert.AreEqual(0, _gameLoadedRaised, "publishLoaded:false suppresses the announcement");
            Assert.AreEqual(1, clock.SeekCount, "…but the scalar restores still run");
        }

        [Test]
        public void ApplyToLiveServices_NullServices_DoNotThrow()
        {
            // A context missing a wallet/licence service (greybox, EditMode) restores what it can, skips the rest.
            Assert.DoesNotThrow(() => SaveRestore.ApplyToLiveServices(Saved(10, 100), null, null, null));
            Assert.AreEqual(1, _gameLoadedRaised, "even with no services, the load is announced");
        }
    }
}
