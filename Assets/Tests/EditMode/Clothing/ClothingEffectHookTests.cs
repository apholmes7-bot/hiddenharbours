using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Store clothing, pass 1: the effects reach the systems they name, and only while worn. A sweater eases
    /// tiredness through the needs service's relief seam; oilskins, gloves and overalls scale the dirt a
    /// kind of work adds through the cleanliness service. With nothing worn both read exactly as before.
    /// Every number is the test's own; one game hour is <see cref="Hour"/> clock seconds.
    /// </summary>
    public class ClothingEffectHookTests
    {
        private const double Hour = 100d;
        private const float Eps = 1e-5f;

        private readonly List<Object> _made = new List<Object>();
        private TraitFixtures.MemorySave _save;
        private Clock _clock;
        private CleanlinessSettings _settings;
        private ClothingService _clothes;
        private ClothingDef _sweater, _oilskins, _overalls;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<PlayerClothingChanged>();
            _save = new TraitFixtures.MemorySave();
            _clock = new Clock();
            _settings = CleanlinessMathTests.Settings();
            _sweater = ClothingWardrobeTests.Garment(_made, "clothing.sweater", ClothingSlot.Top, 16,
                ClothingEffect.Relief(NeedKind.Tiredness, 0.75f));
            _oilskins = ClothingWardrobeTests.Garment(_made, "clothing.oilskins", ClothingSlot.Outerwear, 40,
                ClothingEffect.Dirt(DirtActivity.FishHandling, 0.5f));
            _overalls = ClothingWardrobeTests.Garment(_made, "clothing.overalls", ClothingSlot.Legs, 22,
                ClothingEffect.Dirt(DirtActivity.HardWork, 0.7f));
            _clothes = new ClothingService(() => new[] { _sweater, _oilskins, _overalls });
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<PlayerClothingChanged>();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void Wear(ClothingDef def)
        {
            _clothes.Local.Grant(def);
            _clothes.Local.Wear(def);
        }

        private CleanlinessService Clean()
            => new CleanlinessService(() => _save, () => _clock, () => _settings,
                                      () => new WashStationDef[0], () => Hour, () => _clothes);

        private static NeedsSettings Needs() => new NeedsSettings
        {
            Hunger = new NeedCurve { MildAt = 0.5f, StrongAt = 0.9f },
            Tiredness = new NeedCurve { MildAt = 0.5f, StrongAt = 0.9f, MildWalkPenalty = 0.04f, StrongWalkPenalty = 0.1f },
            WalkFloor = 0.8f, SellFloor = 0.9f, TickGameMinutes = 5f,
        };

        private NeedsService NeedsOver(ICleanlinessService clean)
            => new NeedsService(() => _save, () => _clock, Needs,
                                () => new TraitService(() => _save, () => new TraitDef[0]), () => Hour,
                                cleanliness: () => clean, cleanlinessSettings: () => _settings,
                                clothing: () => _clothes);

        private void SetDirt(float dirt) => _save.Current.PlayerCleanliness = new PlayerCleanlinessDto
        {
            Dirt = dirt, SinceGameSeconds = _clock.T,
        };

        private void SetTired(float tired) => _save.Current.PlayerNeeds = new PlayerNeedsDto
        {
            Hunger = new NeedStateDto(0f, 0d), Tiredness = new NeedStateDto(tired, 0d),
        };

        // ---- needs ---------------------------------------------------------------------------------

        [Test]
        public void Sweater_EasesTiredness_OnlyWhileWorn()
        {
            SetTired(0.6f);
            SetDirt(0f);
            NeedsService needs = NeedsOver(Clean());

            Assert.AreEqual(0.96f, needs.PlayerMultiplier(TraitStat.WalkSpeed), Eps, "tired, no sweater");
            Wear(_sweater);
            Assert.AreEqual(0.97f, needs.PlayerMultiplier(TraitStat.WalkSpeed), Eps, "a quarter of the penalty lifted");
            _clothes.Local.TakeOff(ClothingSlot.Top);
            Assert.AreEqual(0.96f, needs.PlayerMultiplier(TraitStat.WalkSpeed), Eps, "off, and it is back");
        }

        [Test]
        public void NoClothes_NeedsReadExactlyAsWithoutTheSeam()
        {
            SetTired(0.6f);
            SetDirt(0f);
            ICleanlinessService clean = Clean();
            NeedsService withSeam = NeedsOver(clean);
            var without = new NeedsService(() => _save, () => _clock, Needs,
                                           () => new TraitService(() => _save, () => new TraitDef[0]), () => Hour,
                                           cleanliness: () => clean, cleanlinessSettings: () => _settings,
                                           clothing: () => null);
            Assert.AreEqual(without.PlayerMultiplier(TraitStat.WalkSpeed), withSeam.PlayerMultiplier(TraitStat.WalkSpeed));
            Assert.AreEqual(without.PlayerMultiplier(TraitStat.SellPrice), withSeam.PlayerMultiplier(TraitStat.SellPrice));
        }

        // ---- cleanliness ---------------------------------------------------------------------------

        [Test]
        public void Oilskins_HalveTheDirtOfLandingAFish()
        {
            CleanlinessService clean = Clean();
            SetDirt(0f);
            clean.ReportAction(DirtActivity.FishHandling);
            Assert.AreEqual(0.05f, clean.Dirt, Eps, "bare");

            SetDirt(0f);
            Wear(_oilskins);
            clean.ReportAction(DirtActivity.FishHandling);
            Assert.AreEqual(0.025f, clean.Dirt, Eps, "in oilskins");

            SetDirt(0f);
            clean.ReportAction(DirtActivity.HardWork);
            Assert.AreEqual(0f, clean.Dirt, Eps, "only the work it names");
        }

        [Test]
        public void Overalls_ScaleAStretch_TakenAsItBegins()
        {
            CleanlinessService clean = Clean();
            SetDirt(0f);
            Wear(_overalls);

            clean.BeginActivity(DirtActivity.HardWork);
            _clock.T += Hour / 2;
            _clothes.Local.TakeOff(ClothingSlot.Legs);   // mid-stretch: this stretch keeps its rate
            _clock.T += Hour / 2;
            Assert.AreEqual(0.02f + 0.08f * 0.7f, clean.Dirt, Eps, "passive plus the scaled work");
            clean.EndActivity(DirtActivity.HardWork);
            Assert.AreEqual(0.076f, clean.Dirt, Eps, "ending settles the same line");

            float before = clean.Dirt;
            clean.BeginActivity(DirtActivity.HardWork);   // the next stretch, bare
            _clock.T += Hour;
            Assert.AreEqual(before + 0.1f, clean.Dirt, Eps, "unscaled");
            clean.EndActivity(DirtActivity.HardWork);
        }

        [Test]
        public void NoClothes_DirtReadsExactlyAsWithoutTheSeam()
        {
            CleanlinessService withSeam = Clean();
            var without = new CleanlinessService(() => _save, () => _clock, () => _settings,
                                                 () => new WashStationDef[0], () => Hour, () => null);
            SetDirt(0.1f);
            withSeam.BeginActivity(DirtActivity.HardWork);
            _clock.T += Hour * 0.37;
            float a = withSeam.Dirt;
            float b = without.Dirt;
            Assert.AreEqual(b, a, "bit for bit");
            withSeam.ReportAction(DirtActivity.FishHandling);
            Assert.AreEqual(without.Dirt, withSeam.Dirt);
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
