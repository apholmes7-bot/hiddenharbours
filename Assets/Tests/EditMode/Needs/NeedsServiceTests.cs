using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Needs slice 1 (hunger and tiredness, owner ruling 2026-11-14). Every number here is the test's own
    /// (<see cref="Settings"/>), so the owner retuning <see cref="GameConfig.Needs"/> never breaks these.
    /// One game hour is <see cref="Hour"/> clock seconds.
    /// </summary>
    public class NeedsServiceTests
    {
        private const double Hour = 100d;

        private TraitFixtures _fx;
        private TraitFixtures.MemorySave _save;
        private NeedsClock _clock;
        private NeedsSettings _settings;
        private readonly List<NeedStageChanged> _events = new List<NeedStageChanged>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<NeedStageChanged>();
            EventBus.Subscribe<NeedStageChanged>(OnStage);
            _fx = new TraitFixtures();
            _save = new TraitFixtures.MemorySave();
            _clock = new NeedsClock();
            _settings = Settings();
            _events.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<NeedStageChanged>(OnStage);
            EventBus.Clear<NeedStageChanged>();
            GameServices.Reset();
            _fx.DestroyAll();
        }

        private void OnStage(NeedStageChanged e) => _events.Add(e);

        private static byte[] Bits(float f) => System.BitConverter.GetBytes(f);

        /// <summary>Hunger: +0.1 per game hour, peckish 0.4, hungry 0.8. Tiredness: +0.05 per hour, tired
        /// 0.5, worn out 0.9. Walk floor 0.8, sell floor 0.9.</summary>
        private static NeedsSettings Settings() => new NeedsSettings
        {
            Hunger = new NeedCurve
            {
                RisePerGameHour = 0.1f, MildAt = 0.4f, StrongAt = 0.8f,
                MildWalkPenalty = 0.05f, StrongWalkPenalty = 0.12f,
                MildSellPenalty = 0.02f, StrongSellPenalty = 0.05f,
            },
            Tiredness = new NeedCurve
            {
                RisePerGameHour = 0.05f, MildAt = 0.5f, StrongAt = 0.9f,
                MildWalkPenalty = 0.04f, StrongWalkPenalty = 0.10f,
                MildSellPenalty = 0.01f, StrongSellPenalty = 0.03f,
            },
            WalkFloor = 0.8f,
            SellFloor = 0.9f,
            TickGameMinutes = 5f,
        };

        private NeedsService Service(ITraitService traits = null)
            => new NeedsService(() => _save, () => _clock, () => _settings,
                                () => traits ?? new TraitService(() => _save, () => new TraitDef[0]), () => Hour);

        private void SetNeeds(float hunger, float tiredness, double at = 0d)
        {
            _save.Current.PlayerNeeds = new PlayerNeedsDto
            {
                Hunger = new NeedStateDto(hunger, at),
                Tiredness = new NeedStateDto(tiredness, at),
            };
        }

        // ---- rise over game time ------------------------------------------------------------------

        [Test]
        public void Level_RisesWithGameTime_Deterministically()
        {
            var state = new NeedStateDto(0f, 1000d);
            float a = NeedsMath.LevelAt(state, _settings.Hunger, 1000d + 5d * Hour, Hour);
            float b = NeedsMath.LevelAt(state, _settings.Hunger, 1000d + 5d * Hour, Hour);
            Assert.AreEqual(0.5f, a, 1e-6f, "0.1 per game hour for five hours");
            Assert.AreEqual(Bits(a), Bits(b), "same save, same instant, same bits");
            Assert.AreEqual(1f, NeedsMath.LevelAt(state, _settings.Hunger, 1000d + 50d * Hour, Hour), "clamped at 1");
        }

        [Test]
        public void Level_DoesNotDependOnHowOftenItIsRead()
        {
            SetNeeds(0f, 0f);
            NeedsService ticked = Service();
            for (int i = 0; i <= 700; i++)
            {
                _clock.T = i;   // tick every clock second for seven game hours
                ticked.Tick();
            }
            float everyTick = ticked.Level(NeedKind.Hunger);

            SetNeeds(0f, 0f);
            NeedsService untouched = Service();
            _clock.T = 700d;
            Assert.AreEqual(Bits(untouched.Level(NeedKind.Hunger)), Bits(everyTick), "no frame step, no drift");
        }

        [Test]
        public void Level_Unstamped_OrClockBehind_HoldsTheSavedLevel()
        {
            var fresh = new NeedStateDto(0.3f, NeedStateDto.Unstamped);
            Assert.AreEqual(0.3f, NeedsMath.LevelAt(fresh, _settings.Hunger, 9999d, Hour));
            var later = new NeedStateDto(0.3f, 5000d);
            Assert.AreEqual(0.3f, NeedsMath.LevelAt(later, _settings.Hunger, 10d, Hour), "time never runs back");
        }

        [Test]
        public void Tick_AnchorsAFreshGame_AtTheFirstClockItSees()
        {
            Assert.IsFalse(_save.Current.PlayerNeeds.Hunger.IsStamped, "a new game starts unanchored");
            _clock.T = 750d;   // the opening's 07:30, say
            NeedsService service = Service();
            service.Tick();
            Assert.AreEqual(750d, _save.Current.PlayerNeeds.Hunger.SinceGameSeconds);
            Assert.AreEqual(750d, _save.Current.PlayerNeeds.Tiredness.SinceGameSeconds);
            Assert.AreEqual(0f, service.Level(NeedKind.Hunger), "the hours before the opening never count");
            _clock.T = 750d + 2d * Hour;
            Assert.AreEqual(0.2f, service.Level(NeedKind.Hunger), 1e-6f);
        }

        // ---- stages --------------------------------------------------------------------------------

        [Test]
        public void Stages_CrossOnceEach_AndTheFirstLookOnlyRecords()
        {
            SetNeeds(0f, 0f);
            NeedsService service = Service();
            for (int i = 0; i <= 12 * 10; i++)
            {
                _clock.T = i * Hour / 10d;   // twelve game hours, ten ticks an hour
                service.Tick();
            }
            var hunger = _events.FindAll(e => e.Need == NeedKind.Hunger);
            Assert.AreEqual(2, hunger.Count, "fine -> peckish -> hungry, once each");
            Assert.AreEqual(NeedStage.None, hunger[0].From);
            Assert.AreEqual(NeedStage.Mild, hunger[0].To);
            Assert.AreEqual(NeedStage.Mild, hunger[1].From);
            Assert.AreEqual(NeedStage.Strong, hunger[1].To);
            var tired = _events.FindAll(e => e.Need == NeedKind.Tiredness);
            Assert.AreEqual(1, tired.Count, "0.6 by hour twelve: tired, not yet worn out");
            Assert.AreEqual(NeedStage.Mild, tired[0].To);
        }

        [Test]
        public void Stage_ReadsTheConfigThresholds()
        {
            Assert.AreEqual(NeedStage.None, NeedsMath.StageOf(0.39f, _settings.Hunger));
            Assert.AreEqual(NeedStage.Mild, NeedsMath.StageOf(0.4f, _settings.Hunger));
            Assert.AreEqual(NeedStage.Strong, NeedsMath.StageOf(0.8f, _settings.Hunger));
            Assert.AreEqual(NeedStage.Strong, NeedsMath.StageOf(1f, _settings.Hunger));
        }

        // ---- the effects -----------------------------------------------------------------------------

        [Test]
        public void FedAndRested_EveryFactorIsExactlyOne()
        {
            SetNeeds(0f, 0f);
            NeedsService service = Service();
            foreach (TraitStat stat in System.Enum.GetValues(typeof(TraitStat)))
                Assert.AreEqual(Bits(1f), Bits(service.PlayerMultiplier(stat)), stat.ToString());
            Assert.AreEqual(1f, new NeedsService(() => null).PlayerMultiplier(TraitStat.WalkSpeed), "no save at all");
        }

        [Test]
        public void Penalties_AddAcrossNeeds_AndTouchOnlyWalkAndSell()
        {
            SetNeeds(0.5f, 0.95f);   // peckish, worn out
            NeedsService service = Service();
            Assert.AreEqual(1f - 0.05f - 0.10f, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f);
            Assert.AreEqual(1f - 0.02f - 0.03f, service.PlayerMultiplier(TraitStat.SellPrice), 1e-6f);
            Assert.AreEqual(Bits(1f), Bits(service.PlayerMultiplier(TraitStat.BiteWait)),
                "the ruling: needs move only the walk and the in-person price");
        }

        [Test]
        public void WalkFactor_NeverFallsBelowTheFloor_SlowsButNeverStops()
        {
            _settings.Hunger.StrongWalkPenalty = 0.9f;
            _settings.Tiredness.StrongWalkPenalty = 0.9f;
            SetNeeds(1f, 1f);
            NeedsService service = Service();
            Assert.AreEqual(0.8f, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f, "held at the config floor");

            _settings.WalkFloor = 0f;   // a mis-set config still cannot stop the character
            Assert.AreEqual(NeedsMath.MinimumFloor, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f);
            Assert.Greater(NeedsMath.MinimumFloor, 0f);
        }

        [Test]
        public void Penalties_StackWithTraitBonuses_InOnePlace()
        {
            TraitService traits = TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker", "trait.charismatic");
            SetNeeds(0.85f, 0f);   // hungry
            GameServices.Needs = Service(traits);

            Assert.AreEqual(1.08f * 0.88f, CharacterFactors.Player(TraitStat.WalkSpeed), 1e-5f);
            Assert.AreEqual(1.05f * 0.95f, CharacterFactors.Player(TraitStat.SellPrice), 1e-5f);
            Assert.AreEqual(3f * 1.08f * 0.88f, PlayerWalkController.WithWalkTrait(3f), 1e-5f, "the shore walk seam");
            Assert.AreEqual(1.05f * 0.95f, SellService.SellerPriceLevel(null), 1e-5f, "the in-person till seam");
        }

        [Test]
        public void Seams_FedAndRested_AreBitIdenticalToTraitsAlone()
        {
            TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker");
            SetNeeds(0f, 0f);
            GameServices.Needs = Service(GameServices.Traits);
            float traitAlone = GameServices.Traits.PlayerMultiplier(TraitStat.WalkSpeed);
            Assert.AreEqual(Bits(TraitMath.Apply(3f, traitAlone)), Bits(PlayerWalkController.WithWalkTrait(3f)));
            GameServices.Reset();
            Assert.AreEqual(Bits(3f), Bits(PlayerWalkController.WithWalkTrait(3f)), "default services, no save");
            Assert.AreEqual(Bits(1f), Bits(SellService.SellerPriceLevel(null)));
        }

        [Test]
        public void ATraitRelief_CancelsOrEasesANeedsPenalty()
        {
            TraitDef hardy = _fx.Make("trait.hardy");
            hardy.NeedReliefs = new[] { new NeedRelief(NeedKind.Hunger, 0f) };
            TraitDef stoic = _fx.Make("trait.stoic");
            stoic.NeedReliefs = new[] { new NeedRelief(NeedKind.Tiredness, 0.5f) };
            var catalog = new[] { hardy, stoic };

            TraitService traits = TraitFixtures.Install(_save, catalog, "trait.hardy");
            SetNeeds(0.85f, 0.95f);   // hungry (0.12) and worn out (0.10)
            NeedsService service = Service(traits);
            Assert.AreEqual(1f - 0.10f, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f, "hunger's penalty cancelled");
            Assert.AreEqual(NeedStage.Strong, service.Stage(NeedKind.Hunger), "still hungry: only the cost is gone");

            traits.SetPlayerTraits(new[] { "trait.hardy", "trait.stoic" }, out _);
            Assert.AreEqual(1f - 0.05f, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f, "tiredness halved");
        }

        [Test]
        public void AReliefOutsideZeroToOne_CanNeverBecomeADrawback()
        {
            TraitDef odd = _fx.Make("trait.odd");
            odd.NeedReliefs = new[] { new NeedRelief(NeedKind.Hunger, 3f) };
            TraitService traits = TraitFixtures.Install(_save, new[] { odd }, "trait.odd");
            SetNeeds(0.85f, 0f);
            Assert.AreEqual(1f - 0.12f, Service(traits).PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f, "clamped to 1");
        }

        // ---- relief ----------------------------------------------------------------------------------

        [Test]
        public void Eat_LowersHungerFromNow_AndAnnouncesTheStage()
        {
            SetNeeds(0.5f, 0f, at: 0d);
            _clock.T = 3.5d * Hour;   // 0.85: hungry
            NeedsService service = Service();
            service.Tick();
            Assert.AreEqual(NeedStage.Strong, service.Stage(NeedKind.Hunger));
            _events.Clear();

            service.Eat(0.5f);
            Assert.AreEqual(0.35f, service.Level(NeedKind.Hunger), 1e-5f);
            Assert.AreEqual(3.5d * Hour, _save.Current.PlayerNeeds.Hunger.SinceGameSeconds, "re-anchored at the meal");
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(NeedStage.Strong, _events[0].From);
            Assert.AreEqual(NeedStage.None, _events[0].To);

            service.Eat(5f);
            Assert.AreEqual(0f, service.Level(NeedKind.Hunger), "never below fed");
            service.Eat(0f);
            service.Eat(-1f);
            Assert.AreEqual(0f, service.Level(NeedKind.Hunger));
            Assert.AreEqual(0, _save.Saves, "eating is not a save point");
        }

        [Test]
        public void Sleep_ClearsTiredness_AndLeavesHunger()
        {
            SetNeeds(0.2f, 0.95f, at: 0d);
            _clock.T = 2d * Hour;
            NeedsService service = Service();
            float hungerBefore = service.Level(NeedKind.Hunger);
            service.Sleep();
            Assert.AreEqual(0f, service.Level(NeedKind.Tiredness));
            Assert.AreEqual(hungerBefore, service.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(NeedKind.Tiredness, _events[0].Need);
            Assert.AreEqual(NeedStage.None, _events[0].To);
            _clock.T = 12d * Hour;
            Assert.AreEqual(0.5f, service.Level(NeedKind.Tiredness), 1e-5f, "it rises again from the waking");
        }

        [Test]
        public void NoSave_EverythingIsAQuietNoOp()
        {
            var service = new NeedsService(() => null, () => _clock, () => _settings);
            Assert.DoesNotThrow(() => { service.Tick(); service.Eat(1f); service.Sleep(); });
            Assert.AreEqual(0f, service.Level(NeedKind.Hunger));
            Assert.AreEqual(NeedStage.None, service.Stage(NeedKind.Tiredness));
            Assert.IsEmpty(_events);
        }

        // ---- save write and clock seek ---------------------------------------------------------------

        [Test]
        public void Settle_ReanchorsAtNow_WithoutChangingTheLevel()
        {
            SetNeeds(0.1f, 0.2f, at: 0d);
            var needs = _save.Current.PlayerNeeds;
            NeedsMath.Settle(needs, 4d * Hour, _settings, Hour);
            Assert.AreEqual(0.5f, needs.Hunger.Level, 1e-6f);
            Assert.AreEqual(0.4f, needs.Tiredness.Level, 1e-6f);
            Assert.AreEqual(4d * Hour, needs.Hunger.SinceGameSeconds);
            Assert.AreEqual(0.5f, NeedsMath.LevelAt(needs.Hunger, _settings.Hunger, 4d * Hour, Hour), 1e-6f);

            var fresh = new PlayerNeedsDto();
            NeedsMath.Settle(fresh, 4d * Hour, _settings, Hour);
            Assert.IsFalse(fresh.Hunger.IsStamped, "an unanchored need stays unanchored");
        }

        [Test]
        public void Rebase_ASeekNeitherStarvesNorFeeds()
        {
            SetNeeds(0.3f, 0.1f, at: 100d);
            var needs = _save.Current.PlayerNeeds;
            NeedsMath.Rebase(needs, 100d + 30d * Hour);
            Assert.AreEqual(0.3f, NeedsMath.LevelAt(needs.Hunger, _settings.Hunger, 100d + 30d * Hour, Hour), 1e-6f);
            Assert.AreEqual(0.1f, needs.Tiredness.Level);
            NeedsMath.Rebase(null, 5d);   // null-safe
        }

        // ---- the slow tick's cadence -------------------------------------------------------------------

        [Test]
        public void TheSlowTick_IsGameMinutesNotFrames()
        {
            GameConfig previous = GameServices.Config;
            GameServices.Config = null;
            try
            {
                double expected = NeedsSettings.Default.TickGameMinutes * GameConfig.DefaultSecondsPerDay / (24d * 60d);
                Assert.AreEqual(expected, GameServices.NeedsTickGameSeconds, 1e-9, "game minutes, no config wired");
            }
            finally
            {
                GameServices.Config = previous;
            }
        }

        [Test]
        public void TheShippedDefaults_SlowButNeverStop()
        {
            NeedsSettings d = NeedsSettings.Default;
            Assert.Greater(d.WalkFloor, NeedsMath.MinimumFloor - 1e-6f);
            Assert.Less(d.WalkFloor, 1f);
            foreach (NeedCurve c in new[] { d.Hunger, d.Tiredness })
            {
                Assert.Greater(c.RisePerGameHour, 0f);
                Assert.Less(c.MildAt, c.StrongAt);
                Assert.LessOrEqual(c.MildWalkPenalty, c.StrongWalkPenalty);
                Assert.LessOrEqual(c.MildSellPenalty, c.StrongSellPenalty);
            }
        }

        // ---- the shipped config: needs never cut a sale price (owner ruling 2026-11-14) ----------------
        // The mechanism stays (Penalties_AddAcrossNeeds / Penalties_StackWithTraitBonuses prove it with the
        // test's own non-zero numbers); these pin the SHIPPED numbers to no sell penalty at all.

        [Test]
        public void TheShippedDefaults_HungerAndTiredness_LeaveTheSalePriceUnchanged()
        {
            _settings = NeedsSettings.Default;
            foreach (NeedCurve c in new[] { _settings.Hunger, _settings.Tiredness })
            {
                Assert.AreEqual(0f, c.MildSellPenalty, "shipped: no first-stage sell penalty");
                Assert.AreEqual(0f, c.StrongSellPenalty, "shipped: no second-stage sell penalty");
            }

            SetNeeds(1f, 1f);   // hungry and worn right out
            NeedsService service = Service();
            Assert.AreEqual(Bits(1f), Bits(service.PlayerMultiplier(TraitStat.SellPrice)),
                "hunger and tiredness leave the sale price unchanged");
            Assert.Less(service.PlayerMultiplier(TraitStat.WalkSpeed), 1f, "the walk still slows");
        }

        [Test]
        public void TheShippedDefaults_Charismatic_StillRaisesTheSale_WhenHungryAndTired()
        {
            _settings = NeedsSettings.Default;
            TraitService traits = TraitFixtures.Install(_save, _fx.SliceOne(), "trait.charismatic");
            SetNeeds(1f, 1f);
            GameServices.Needs = Service(traits);

            Assert.AreEqual(1.05f, CharacterFactors.Player(TraitStat.SellPrice), 1e-6f);
            Assert.AreEqual(1.05f, SellService.SellerPriceLevel(null), 1e-6f, "the in-person till seam");
        }

        [Test]
        public void TheConfigAsset_ShipsNoNeedsSellPenalty()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project/Data/Config/GameConfig.asset");
            Assert.IsTrue(System.IO.File.Exists(path), "missing GameConfig.asset");
            string yaml = System.IO.File.ReadAllText(path);
            var matches = System.Text.RegularExpressions.Regex.Matches(
                yaml, @"^\s*(?:Mild|Strong)SellPenalty:\s*(\S+)\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.AreEqual(4, matches.Count, "Hunger and Tiredness each list both sell-penalty keys");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                Assert.IsTrue(float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float v),
                    $"not a number: '{m.Groups[1].Value}'");
                Assert.AreEqual(0f, v, "the shipped asset must not let needs cut a sale price");
            }
        }

        private sealed class NeedsClock : IGameClock
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
