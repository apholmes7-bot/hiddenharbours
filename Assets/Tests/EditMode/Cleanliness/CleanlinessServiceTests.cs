using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Cleanliness slice 1, the service and the seams: getting dirty through the service, washing at a
    /// station wherever it stands, stinky joining hunger and tiredness in the walk/price factor and the reply
    /// tone, the stage signal, the bus responder, and a clean player changing nothing. Every number is the
    /// test's own; one game hour is <see cref="Hour"/> clock seconds.
    /// </summary>
    public class CleanlinessServiceTests
    {
        private const double Hour = 100d;
        private const float Eps = 1e-5f;

        private readonly List<Object> _made = new List<Object>();
        private readonly List<NeedStageChanged> _stages = new List<NeedStageChanged>();
        private readonly List<PlayerWashChanged> _washes = new List<PlayerWashChanged>();
        private TraitFixtures.MemorySave _save;
        private Clock _clock;
        private CleanlinessSettings _settings;
        private WashStationDef _sea, _sink, _shower, _toilet;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<NeedStageChanged>();
            EventBus.Clear<PlayerWashChanged>();
            EventBus.Subscribe<NeedStageChanged>(OnStage);
            EventBus.Subscribe<PlayerWashChanged>(OnWash);
            _save = new TraitFixtures.MemorySave();
            _clock = new Clock();
            _settings = CleanlinessMathTests.Settings();
            _sea = CleanlinessMathTests.Station(_made, "washstation.sea", WashStationKind.Sea, 1f, 0.5f, bothWays: true);
            _sink = CleanlinessMathTests.Station(_made, "washstation.sink", WashStationKind.Sink, 1f, 0.8f);
            _shower = CleanlinessMathTests.Station(_made, "washstation.shower", WashStationKind.Shower, 4f, 1f);
            _toilet = CleanlinessMathTests.Station(_made, "washstation.toilet", WashStationKind.Toilet, 0f, 0f, sitOnly: true);
            _stages.Clear();
            _washes.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CleanlinessResponder.Uninstall();
            EventBus.Unsubscribe<NeedStageChanged>(OnStage);
            EventBus.Unsubscribe<PlayerWashChanged>(OnWash);
            EventBus.Clear<NeedStageChanged>();
            EventBus.Clear<PlayerWashChanged>();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void OnStage(NeedStageChanged e) => _stages.Add(e);
        private void OnWash(PlayerWashChanged e) => _washes.Add(e);

        private CleanlinessService Service()
            => new CleanlinessService(() => _save, () => _clock, () => _settings,
                                      () => new[] { _sea, _sink, _shower, _toilet }, () => Hour);

        /// <summary>Hunger and tiredness with no rise (so only dirt moves), and a dirt curve with real walk
        /// and sell penalties so the seam is observable.</summary>
        private static NeedsSettings Needs() => new NeedsSettings
        {
            Hunger = new NeedCurve { MildAt = 0.4f, StrongAt = 0.8f, MildSellPenalty = 0.02f, StrongSellPenalty = 0.05f,
                                     MildWalkPenalty = 0.05f, StrongWalkPenalty = 0.12f },
            Tiredness = new NeedCurve { MildAt = 0.5f, StrongAt = 0.9f },
            WalkFloor = 0.8f, SellFloor = 0.9f, TickGameMinutes = 5f,
        };

        private CleanlinessSettings WithPenalties()
        {
            CleanlinessSettings s = _settings;
            s.Curve.MildSellPenalty = 0.02f;
            s.Curve.StrongSellPenalty = 0.05f;
            s.Curve.MildWalkPenalty = 0.05f;
            s.Curve.StrongWalkPenalty = 0.12f;
            return s;
        }

        private NeedsService NeedsOver(ICleanlinessService clean)
            => new NeedsService(() => _save, () => _clock, Needs,
                                () => new TraitService(() => _save, () => new TraitDef[0]), () => Hour,
                                cleanliness: () => clean, cleanlinessSettings: () => _settings);

        private void SetDirt(float dirt) => _save.Current.PlayerCleanliness = new PlayerCleanlinessDto
        {
            Dirt = dirt, SinceGameSeconds = _clock.T,
        };

        // ---- getting dirty through the service -----------------------------------------------------

        [Test]
        public void Activity_RaisesDirtAtItsRate_AndEnds()
        {
            var svc = Service();
            SetDirt(0f);
            svc.BeginActivity(DirtActivity.HardWork);
            _clock.T += Hour;
            Assert.AreEqual(0.1f, svc.Dirt, Eps);
            svc.EndActivity(DirtActivity.HardWork);
            _clock.T += Hour;
            Assert.AreEqual(0.12f, svc.Dirt, Eps, "passive only after it ends");
            Assert.AreEqual(DirtActivity.None, svc.Activity);
        }

        [Test]
        public void ReportAction_AddsItsBumpAtOnce()
        {
            var svc = Service();
            SetDirt(0.1f);
            svc.ReportAction(DirtActivity.FishHandling);
            Assert.AreEqual(0.15f, svc.Dirt, Eps);
            svc.ReportAction(DirtActivity.HardWork);   // bump 0
            Assert.AreEqual(0.15f, svc.Dirt, Eps);
        }

        // ---- washing, wherever the station stands --------------------------------------------------

        [Test]
        public void Shower_CleansFully_Sink_StopsAtItsCeiling()
        {
            var svc = Service();
            SetDirt(0.9f);
            svc.BeginWash(_sink);
            // Stand at the sink for its WHOLE counted wash (the guard window), far longer than the 0.7 h its
            // rate needs to bring 0.9 down to the ceiling's 0.2. Past the window the wash is no longer counted
            // and passive dirt runs again (StretchPastItsWindow_... pins that), so reading later would measure
            // the passive rate, not the ceiling.
            double window = _settings.MaxStretchGameMinutes / 60d * Hour;
            Assert.Greater(window, (0.9d - 0.2d) / _sink.CleanRatePerGameHour * Hour, "the window outlasts the scrub");
            _clock.T += window;
            Assert.AreEqual(0.2f, svc.Dirt, Eps, "the sink cannot get you fully clean");
            svc.BeginWash(_shower);
            _clock.T += 0.5d * Hour;
            Assert.AreEqual(0f, svc.Dirt, Eps, "the shower can");
            svc.EndWash();
            Assert.IsNull(svc.WashingAt);
            Assert.AreEqual(4, _washes.Count, "sink on, sink off, shower on, shower off");
        }

        [Test]
        public void Washing_PublishesOnAndOff()
        {
            var svc = Service();
            SetDirt(0.5f);
            svc.BeginWash(_shower);
            svc.EndWash();
            Assert.AreEqual(2, _washes.Count);
            Assert.IsTrue(_washes[0].Washing);
            Assert.AreEqual(WashStationKind.Shower, _washes[0].Kind);
            Assert.IsFalse(_washes[1].Washing);
            Assert.AreEqual("washstation.shower", _washes[1].StationId);
        }

        [Test]
        public void Toilet_IsSitOnly_ChangesNothing()
        {
            var svc = Service();
            SetDirt(0.6f);
            svc.BeginWash(_toilet);
            _clock.T += 3d * Hour;
            Assert.IsNull(svc.WashingAt);
            Assert.AreEqual(0.66f, svc.Dirt, Eps, "passive only: the toilet has no function");
            Assert.AreEqual(0, _washes.Count);
        }

        [Test]
        public void StretchPastItsWindow_IsClosedByTheTick_WithoutChangingTheLevel()
        {
            var svc = Service();
            SetDirt(0.9f);
            svc.BeginWash(_sink);
            _clock.T += 10d * Hour;
            float before = svc.Dirt;
            svc.Tick();
            Assert.IsNull(svc.WashingAt);
            Assert.AreEqual(before, svc.Dirt, Eps);
        }

        // ---- stinky through the needs seam ---------------------------------------------------------

        [Test]
        public void Clean_ChangesNothing_BitForBit()
        {
            var svc = Service();
            SetDirt(0f);
            _settings = WithPenalties();
            var needs = NeedsOver(svc);
            Assert.AreEqual(System.BitConverter.GetBytes(TraitMath.Identity),
                            System.BitConverter.GetBytes(needs.PlayerMultiplier(TraitStat.SellPrice)));
            Assert.AreEqual(System.BitConverter.GetBytes(TraitMath.Identity),
                            System.BitConverter.GetBytes(needs.PlayerMultiplier(TraitStat.WalkSpeed)));
            Assert.AreEqual(ReplyTone.Warm, NeedsNotice.ToneOf(NeedsNotice.Of(needs)));
        }

        [Test]
        public void Stinky_DrivesThePriceAndReplySeam_ExactlyLikeHunger()
        {
            _settings = WithPenalties();
            var svc = Service();

            // Hunger strong alone.
            _save.Current.PlayerNeeds = new PlayerNeedsDto { Hunger = new NeedStateDto(0.85f, 0d), Tiredness = new NeedStateDto(0f, 0d) };
            SetDirt(0f);
            var needs = NeedsOver(svc);
            float hungryPrice = needs.PlayerMultiplier(TraitStat.SellPrice);
            float hungryWalk = needs.PlayerMultiplier(TraitStat.WalkSpeed);

            // Dirt strong alone, same penalties.
            _save.Current.PlayerNeeds = new PlayerNeedsDto { Hunger = new NeedStateDto(0f, 0d), Tiredness = new NeedStateDto(0f, 0d) };
            SetDirt(0.8f);
            Assert.AreEqual(NeedStage.Strong, needs.Stage(NeedKind.Dirtiness));
            Assert.AreEqual(hungryPrice, needs.PlayerMultiplier(TraitStat.SellPrice), Eps);
            Assert.AreEqual(hungryWalk, needs.PlayerMultiplier(TraitStat.WalkSpeed), Eps);
            Assert.AreEqual(0.95f, needs.PlayerMultiplier(TraitStat.SellPrice), Eps);
            Assert.AreEqual(NeedSigns.Stinky, NeedsNotice.Of(needs));
            Assert.AreEqual(ReplyTone.PutOff, NeedsNotice.ToneOf(NeedsNotice.Of(needs)));

            // Just under the line: grubby, not stinky.
            SetDirt(0.74f);
            Assert.AreEqual(NeedStage.Mild, needs.Stage(NeedKind.Dirtiness));
            Assert.AreEqual(ReplyTone.Warm, NeedsNotice.ToneOf(NeedsNotice.Of(needs)));
        }

        [Test]
        public void ShippedDefaults_StinkyCostsNothingAtTheTill()
        {
            _settings = CleanlinessSettings.Default;
            var svc = Service();
            SetDirt(1f);
            var needs = NeedsOver(svc);
            Assert.AreEqual(NeedStage.Strong, needs.Stage(NeedKind.Dirtiness));
            Assert.AreEqual(TraitMath.Identity, needs.PlayerMultiplier(TraitStat.SellPrice),
                "owner 2026-11-14: needs never cut a sale price; stink shows in the replies");
            Assert.AreEqual(TraitMath.Identity, needs.PlayerMultiplier(TraitStat.WalkSpeed));
        }

        [Test]
        public void TheSlowTick_AnnouncesBecomingStinky()
        {
            var svc = Service();
            SetDirt(0.7f);
            var needs = NeedsOver(svc);
            needs.Tick();   // baseline
            _clock.T += 3d * Hour;   // +0.06 passive -> 0.76
            needs.Tick();
            Assert.That(_stages.Exists(e => e.Need == NeedKind.Dirtiness && e.To == NeedStage.Strong));
        }

        [Test]
        public void CrankyTone_ComesFromHungerOrTiredness()
        {
            Assert.AreEqual(ReplyTone.Cranky, NeedsNotice.ToneOf(NeedsNotice.FromStages(NeedStage.Strong, NeedStage.None, NeedStage.None)));
            Assert.AreEqual(ReplyTone.Cranky, NeedsNotice.ToneOf(NeedsNotice.FromStages(NeedStage.None, NeedStage.Strong, NeedStage.Mild)));
            Assert.AreEqual(ReplyTone.PutOff, NeedsNotice.ToneOf(NeedsNotice.FromStages(NeedStage.Strong, NeedStage.Strong, NeedStage.Strong)));
        }

        // ---- the bus responder ---------------------------------------------------------------------

        [Test]
        public void Responder_FishAndSea()
        {
            var svc = Service();
            GameServices.Cleanliness = svc;
            SetDirt(0.9f);
            CleanlinessResponder.Install();

            EventBus.Publish(new FishCaught(default(CatchItem)));
            Assert.AreEqual(0.95f, svc.Dirt, Eps, "one fish handled: the test's bump of 0.05");

            EventBus.Publish(new OnFootWaterStateChanged(OnFootWaterState.Swim, OnFootWaterState.Wade, true, 2f));
            Assert.AreSame(_sea, svc.WashingAt);
            _clock.T += 1d * Hour;
            Assert.AreEqual(0.5f, svc.Dirt, Eps, "the sea rinses to its base, no further");
            EventBus.Publish(new OnFootWaterStateChanged(OnFootWaterState.Wade, OnFootWaterState.Swim, false, 1f));
            Assert.IsNull(svc.WashingAt);
        }

        [Test]
        public void NoSave_DoesNothing()
        {
            var svc = new CleanlinessService(() => null, () => _clock, () => _settings, () => new[] { _shower }, () => Hour);
            svc.BeginWash(_shower);
            svc.BeginActivity(DirtActivity.Gym);
            svc.ReportAction(DirtActivity.FishHandling);
            svc.Tick();
            Assert.AreEqual(0f, svc.Dirt);
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
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }
    }
}
