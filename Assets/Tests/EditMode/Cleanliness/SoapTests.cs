using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Soap (owner ask 2026-11-17: "add soap as a new inventory item to purchase and to be needed to reach
    /// 100% cleanliness"). Without soap a shower or a bath stops at the unsoaped cap; with a bar it reaches
    /// fully clean and uses one wash of the bar; the sink keeps its own lower cap and the sea is unchanged; the
    /// wash event says whether soap went in or was missing, and the missing case makes the player think the
    /// no-soap line; the general store sells the bar. Every number is the test's own; one game hour is
    /// <see cref="Hour"/> clock seconds.
    /// </summary>
    public class SoapTests
    {
        private const double Hour = 100d;
        private const float Eps = 1e-5f;
        private const string Soap = "supply.soap_bar";
        private const string LeBlancs = "seller.leblancs";

        private readonly List<Object> _made = new List<Object>();
        private readonly List<PlayerWashChanged> _washes = new List<PlayerWashChanged>();
        private readonly List<AmbientSpeechRequested> _spoken = new List<AmbientSpeechRequested>();
        private TraitFixtures.MemorySave _save;
        private Clock _clock;
        private CleanlinessSettings _settings;
        private WashStationDef _sea, _sink, _shower, _bath;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<PlayerWashChanged>();
            EventBus.Subscribe<PlayerWashChanged>(OnWash);
            EventBus.Subscribe<AmbientSpeechRequested>(OnSpoken);
            _save = new TraitFixtures.MemorySave();
            _clock = new Clock();
            _settings = SoapSettings();
            _sea = CleanlinessMathTests.Station(_made, "washstation.sea", WashStationKind.Sea, 1f, 0.5f, bothWays: true);
            _sink = CleanlinessMathTests.Station(_made, "washstation.sink", WashStationKind.Sink, 1f, 0.8f);
            _shower = CleanlinessMathTests.Station(_made, "washstation.shower", WashStationKind.Shower, 4f, 1f);
            _bath = CleanlinessMathTests.Station(_made, "washstation.bath", WashStationKind.Bath, 4f, 1f);
            _washes.Clear();
            _spoken.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CleanlinessResponder.Uninstall();
            EventBus.Unsubscribe<PlayerWashChanged>(OnWash);
            EventBus.Unsubscribe<AmbientSpeechRequested>(OnSpoken);
            EventBus.Clear<PlayerWashChanged>();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void OnWash(PlayerWashChanged e) => _washes.Add(e);
        private void OnSpoken(AmbientSpeechRequested e) => _spoken.Add(e);

        /// <summary>The service tests' settings with the soap rule on: 8 washes a bar, 0.85 without soap.</summary>
        private static CleanlinessSettings SoapSettings()
        {
            CleanlinessSettings s = CleanlinessMathTests.Settings();
            s.SoapSupplyId = Soap;
            s.WashesPerSoapBar = 8;
            s.UnsoapedCeiling = 0.85f;
            s.NoSoapLine = "No soap.";
            return s;
        }

        private CleanlinessService Service()
            => new CleanlinessService(() => _save, () => _clock, () => _settings,
                                      () => new[] { _sea, _sink, _shower, _bath }, () => Hour);

        private SaveData Save => _save.Current;

        private void SetDirt(float dirt) => Save.PlayerCleanliness = new PlayerCleanlinessDto
        {
            Dirt = dirt, SinceGameSeconds = _clock.T,
        };

        private void GiveBars(int n) => SupplyLocker.Add(Save, Soap, n);

        /// <summary>One wash at <paramref name="station"/> from 0.9 dirt, one game hour: long enough to reach
        /// any ceiling (the slowest station here, the sink at 1/h, needs 0.7 h to come down from 0.9 to its 0.2)
        /// and well inside the 120-minute counted window.</summary>
        private float WashFrom09(CleanlinessService svc, WashStationDef station)
        {
            SetDirt(0.9f);
            svc.BeginWash(station);
            _clock.T += 1d * Hour;
            float dirt = svc.Dirt;
            svc.EndWash();
            _clock.T += 0.01d * Hour;
            return dirt;
        }

        // ---- the cap without soap, fully clean with it -------------------------------------------------

        [Test]
        public void WithoutSoap_AShowerOrABath_StopsAtTheUnsoapedCap()
        {
            var svc = Service();
            Assert.AreEqual(0.15f, WashFrom09(svc, _shower), Eps, "1 - 0.85: the shower cannot finish the job");
            Assert.AreEqual(0.15f, WashFrom09(svc, _bath), Eps, "nor can the bath");
            Assert.AreEqual(0.85f, svc.CeilingFor(_shower), Eps);
            Assert.IsFalse(svc.HasSoap);
        }

        [Test]
        public void WithSoap_AShowerOrABath_ReachesFullyClean()
        {
            var svc = Service();
            GiveBars(1);
            Assert.AreEqual(1f, svc.CeilingFor(_shower), Eps, "a bar on hand lifts the cap before the wash");
            Assert.AreEqual(0f, WashFrom09(svc, _shower), Eps, "soaped, the shower gets him fully clean");
            Assert.AreEqual(0f, WashFrom09(svc, _bath), Eps, "so does the bath");
        }

        [Test]
        public void TheSink_KeepsItsOwnCap_AndUsesNoSoap()
        {
            var svc = Service();
            Assert.AreEqual(0.2f, WashFrom09(svc, _sink), Eps, "without soap, the sink's own 0.8");
            GiveBars(1);
            Assert.AreEqual(0.2f, WashFrom09(svc, _sink), Eps, "with soap, still the sink's own 0.8");
            Assert.AreEqual(1, SoapStock.Bars(Save, _settings), "the sink never opens a bar");
            Assert.AreEqual(0, SoapStock.OpenBarWashes(Save, _settings));
            Assert.IsTrue(_washes.All(e => !e.Soaped && !e.SoapMissing), "the sink never asks for soap");
            Assert.IsFalse(svc.SoapMatters(_sink));
        }

        [Test]
        public void TheSea_IsUnchanged_BothWays_AndNeverAsksForSoap()
        {
            var svc = Service();
            Assert.AreEqual(0.5f, WashFrom09(svc, _sea), Eps, "the sea rinses down to its base");
            SetDirt(0.1f);
            svc.BeginWash(_sea);
            _clock.T += 1d * Hour;
            Assert.AreEqual(0.5f, svc.Dirt, Eps, "and pulls a clean swimmer up to it");
            svc.EndWash();
            Assert.IsFalse(svc.SoapMatters(_sea));
            Assert.IsTrue(_washes.All(e => !e.Soaped && !e.SoapMissing));
        }

        [Test]
        public void WithTheRuleOff_TheShowerStillCleansFully_AndSpendsNothing()
        {
            _settings.SoapSupplyId = "";
            var svc = Service();
            GiveBars(1);
            Assert.AreEqual(0f, WashFrom09(svc, _shower), Eps, "no soap id configured: the old behaviour");
            Assert.AreEqual(1, SupplyLocker.Count(Save, Soap), "and the bar is left alone");
            Assert.IsTrue(_washes.All(e => !e.Soaped && !e.SoapMissing));
        }

        // ---- consumption --------------------------------------------------------------------------------

        [Test]
        public void OneBar_IsEightSoapedWashes_ThenTheCapReturns()
        {
            var svc = Service();
            GiveBars(1);
            Assert.AreEqual(8, SoapStock.WashesLeft(Save, _settings));

            Assert.AreEqual(0f, WashFrom09(svc, _shower), Eps, "wash 1 is soaped");
            Assert.AreEqual(0, SoapStock.Bars(Save, _settings), "the first wash opens the bar");
            Assert.AreEqual(7, SoapStock.OpenBarWashes(Save, _settings), "and seven washes are left on it");

            for (int i = 2; i <= 8; i++)
                Assert.AreEqual(0f, WashFrom09(svc, _shower), Eps, $"wash {i} is soaped");
            Assert.AreEqual(0, SoapStock.WashesLeft(Save, _settings), "eight washes use up the bar");
            Assert.IsFalse(SoapStock.Has(Save, _settings));
            Assert.AreEqual(0, SupplyLocker.Count(Save, SoapStock.OpenBarId(_settings)), "no empty entry is carried");

            Assert.AreEqual(0.15f, WashFrom09(svc, _shower), Eps, "wash 9 has no soap");
        }

        [Test]
        public void StayingInTheSameShower_UsesOneWash_NotOnePerCall()
        {
            var svc = Service();
            GiveBars(1);
            SetDirt(0.9f);
            svc.BeginWash(_shower);
            svc.BeginWash(_shower);   // the same station again is a no-op
            Assert.AreEqual(7, SoapStock.WashesLeft(Save, _settings));
            Assert.IsTrue(svc.WashSoaped);
        }

        [Test]
        public void AnOpenBar_IsSpentBeforeANewOneIsOpened()
        {
            var svc = Service();
            GiveBars(2);
            WashFrom09(svc, _shower);
            Assert.AreEqual(1, SoapStock.Bars(Save, _settings), "one bar opened, one still wrapped");
            Assert.AreEqual(7, SoapStock.OpenBarWashes(Save, _settings));
            Assert.AreEqual(15, SoapStock.WashesLeft(Save, _settings));
        }

        [Test]
        public void TryUseWash_RefusesWithNoSaveOrNoSoap_AndChangesNothing()
        {
            Assert.IsFalse(SoapStock.TryUseWash(null, _settings));
            Assert.IsFalse(SoapStock.TryUseWash(Save, _settings));
            Assert.AreEqual(0, SoapStock.WashesLeft(Save, _settings));
        }

        // ---- the wash event and the no-soap line ----------------------------------------------------------

        [Test]
        public void TheWashEvent_SaysSoapedOrSoapMissing()
        {
            var svc = Service();
            SetDirt(0.9f);
            svc.BeginWash(_shower);
            Assert.IsTrue(_washes[0].Washing);
            Assert.IsFalse(_washes[0].Soaped);
            Assert.IsTrue(_washes[0].SoapMissing, "a shower without soap says so");
            svc.EndWash();
            Assert.IsFalse(_washes[1].Washing);
            Assert.IsFalse(_washes[1].SoapMissing, "the end of a wash is never a missing-soap moment");

            GiveBars(1);
            svc.BeginWash(_shower);
            Assert.IsTrue(_washes[2].Soaped, "with a bar, the wash is soaped (the visuals lane's hook)");
            Assert.IsFalse(_washes[2].SoapMissing);
            svc.EndWash();
            Assert.IsTrue(_washes[3].Soaped, "the end says which wash ended");
        }

        [Test]
        public void TheOldConstructor_IsNeitherSoapedNorMissing()
        {
            var e = new PlayerWashChanged(true, WashStationKind.Shower, "washstation.shower");
            Assert.IsFalse(e.Soaped);
            Assert.IsFalse(e.SoapMissing);
            Assert.IsFalse(new PlayerWashChanged(true, WashStationKind.Shower, "x", true, true).SoapMissing,
                           "a soaped wash is never also missing soap");
        }

        [Test]
        public void WashingWithoutSoap_MakesThePlayerThinkTheNoSoapLine_Once()
        {
            var svc = Service();
            GameServices.Cleanliness = svc;
            CleanlinessResponder.Install();
            SetDirt(0.9f);
            svc.BeginWash(_shower);
            Assert.AreEqual(1, _spoken.Count, "one thought as the unsoaped wash begins");
            Assert.AreEqual(AmbientSpeechKind.InnerVoice, _spoken[0].Kind, "his own voice, the inner-voice bubble");
            Assert.AreEqual(GameServices.CleanlinessSettings.NoSoapLine, _spoken[0].Line);
            svc.EndWash();

            GiveBars(1);
            svc.BeginWash(_shower);
            svc.EndWash();
            svc.BeginWash(_sink);
            Assert.AreEqual(1, _spoken.Count, "a soaped shower and a sink think nothing");
        }

        // ---- determinism ----------------------------------------------------------------------------------

        [Test]
        public void TheSoapMath_IsPure_TheSameInputsGiveTheSameDirt()
        {
            float a = CleanlinessMath.AfterWash(0.9f, _shower, _settings, false, 1d);
            float b = CleanlinessMath.AfterWash(0.9f, _shower, _settings, false, 1d);
            Assert.AreEqual(a, b);
            Assert.AreEqual(0.15f, a, Eps);
            Assert.AreEqual(0f, CleanlinessMath.AfterWash(0.9f, _shower, _settings, true, 1d), Eps);
            Assert.AreEqual(0.2f, CleanlinessMath.AfterWash(0.9f, _sink, _settings, true, 1d), Eps);
            Assert.AreEqual(0.2f, CleanlinessMath.AfterWash(0.9f, _sink, _settings, false, 1d), Eps);
        }

        // ---- the purchase offer ---------------------------------------------------------------------------

        [Test]
        public void TheGeneralStore_SellsTheBar_AndABuyPutsItInTheLocker()
        {
            List<SupplyDef> offers = CatalogSource.For<SupplyDef>(LeBlancs);
            SupplyDef bar = offers.SingleOrDefault(o => o.Id == Soap);
            Assert.IsNotNull(bar, "Marguerite's book lists the bar of soap");
            Assert.Greater(bar.Price, 0);
            Assert.AreEqual(CleanlinessSettings.Default.SoapSupplyId, bar.Id, "the config's soap is the one the store sells");

            var go = new GameObject("SoapShop");
            _made.Add(go);
            var shop = go.AddComponent<SupplyShop>();
            var save = new SaveData();
            var wallet = new FakeWallet(bar.Price);
            Assert.IsTrue(shop.TryBuy(bar, wallet, save));
            Assert.AreEqual(0, wallet.Money);
            Assert.AreEqual(1, SoapStock.Bars(save, CleanlinessSettings.Default));
            Assert.AreEqual(CleanlinessSettings.Default.WashesPerSoapBar, SoapStock.WashesLeft(save, CleanlinessSettings.Default));
        }

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class Clock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsPaused { get; set; }
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public float TimeScale { get; set; } = 1f;
        }
    }
}
