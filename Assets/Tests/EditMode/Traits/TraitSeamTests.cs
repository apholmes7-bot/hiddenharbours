using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Traits slice 1, the three seams. Each seam reads its stat through <see cref="GameServices.Traits"/>
    /// (rule 4): with no traits the number is bit-for-bit what it was, and with the trait it moves by the
    /// trait's factor. Defs are test-made, so the owner retuning a shipped asset never breaks these.
    /// </summary>
    public class TraitSeamTests
    {
        private TraitFixtures _fx;
        private TraitFixtures.MemorySave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<CatchSold>();
            _fx = new TraitFixtures();
            _save = new TraitFixtures.MemorySave();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            GameServices.Reset();
            _fx.DestroyAll();
        }

        private static byte[] Bits(float f) => System.BitConverter.GetBytes(f);

        // ---- Speedwalker: PlayerWalkController ----------------------------------------------------

        [Test]
        public void Walk_NoTraits_IsBitIdentical()
        {
            TraitFixtures.Install(_save, _fx.SliceOne());
            foreach (float v in new[] { 3f, 5.5f, 2.9999f })
                Assert.AreEqual(Bits(v), Bits(PlayerWalkController.WithWalkTrait(v)));
            GameServices.Reset();   // the default service with no save at all
            Assert.AreEqual(Bits(3f), Bits(PlayerWalkController.WithWalkTrait(3f)));
        }

        [Test]
        public void Walk_Speedwalker_ScalesWalkAndSprint()
        {
            TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker");
            Assert.AreEqual(3f * 1.08f, PlayerWalkController.WithWalkTrait(3f), 1e-5f);
            Assert.AreEqual(5.5f * 1.08f, PlayerWalkController.WithWalkTrait(5.5f), 1e-5f);
            // Fed through the unchanged SpeedFor: the sprint is still the faster of the two, and swimming
            // still refuses the sprint (the trait never buys a way out of the sea).
            float walk = PlayerWalkController.WithWalkTrait(3f), sprint = PlayerWalkController.WithWalkTrait(5.5f);
            Assert.AreEqual(sprint, PlayerWalkController.SpeedFor(true, walk, sprint, float.NegativeInfinity, 0.6f));
            Assert.AreEqual(walk, PlayerWalkController.SpeedFor(true, walk, sprint, 1f, 0.6f));
        }

        [Test]
        public void Walk_OtherTraits_DoNotMoveIt()
        {
            TraitFixtures.Install(_save, _fx.SliceOne(), "trait.charismatic", "trait.patient_angler");
            Assert.AreEqual(Bits(3f), Bits(PlayerWalkController.WithWalkTrait(3f)));
        }

        // ---- Charismatic: SellService (the till and the quote) -----------------------------------

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 999;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }

        private static FakeHold Cod(int n)
        {
            var hold = new FakeHold();
            for (int i = 0; i < n; i++)
                hold.TryAdd(new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 100, 0.4f));
            return hold;
        }

        /// <summary>The till's own sum, at a given price level, with no market (supply 0, demand 1).</summary>
        private static int Expected(int n, float priceLevel)
        {
            int total = 0;
            for (int j = 0; j < n; j++) total += SellPricing.MarginalPrice(100, 0.4f, 0f, j, 1f, 1f, priceLevel);
            return total;
        }

        [Test]
        public void Sell_NoTraits_LevelIsBitIdentical_AndPaysTheOldPrice()
        {
            TraitFixtures.Install(_save, _fx.SliceOne());
            Assert.AreEqual(Bits(1f), Bits(SellService.SellerPriceLevel(null)));
            Assert.AreEqual(Expected(3, 1f), SellService.Quote(Cod(3), null, "fish.cod", 3));
            var wallet = new FakeWallet();
            Assert.AreEqual(Expected(3, 1f), SellService.SellAll(Cod(3), wallet, null));
            Assert.AreEqual(Expected(3, 1f), wallet.Money);
        }

        [Test]
        public void Sell_Charismatic_RaisesTheQuoteAndTheCoinAlike()
        {
            TraitFixtures.Install(_save, _fx.SliceOne(), "trait.charismatic");
            float level = SellService.SellerPriceLevel(null);
            Assert.AreEqual(1.05f, level, 1e-6f);
            int expected = Expected(3, level);
            Assert.Greater(expected, Expected(3, 1f), "a base value of 100 leaves room for 5 % to show");

            Assert.AreEqual(expected, SellService.Quote(Cod(3), null, "fish.cod", 3), "the screen shows it");
            var wallet = new FakeWallet();
            Assert.AreEqual(expected, SellService.SellSpecies(Cod(3), wallet, null, "fish.cod", 3), "the till pays it");
            Assert.AreEqual(expected, wallet.Money);
        }

        [Test]
        public void Sell_OtherTraits_DoNotMoveIt()
        {
            TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker", "trait.patient_angler");
            Assert.AreEqual(Bits(1f), Bits(SellService.SellerPriceLevel(null)));
        }

        // ---- Patient Angler: FishingController ---------------------------------------------------

        [Test]
        public void Bite_NoTraits_IsBitIdentical()
        {
            TraitFixtures.Install(_save, _fx.SliceOne());
            foreach (float v in new[] { 1.5f, 4f, 2.718281f })
                Assert.AreEqual(Bits(v), Bits(FishingController.WithBiteWaitTrait(v)));
        }

        [Test]
        public void Bite_PatientAngler_ShortensTheWait()
        {
            TraitFixtures.Install(_save, _fx.SliceOne(), "trait.patient_angler");
            Assert.AreEqual(4f * 0.9f, FishingController.WithBiteWaitTrait(4f), 1e-5f);
        }

        [Test]
        public void Bite_OtherTraits_DoNotMoveIt()
        {
            TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker", "trait.charismatic");
            Assert.AreEqual(Bits(4f), Bits(FishingController.WithBiteWaitTrait(4f)));
        }
    }
}
