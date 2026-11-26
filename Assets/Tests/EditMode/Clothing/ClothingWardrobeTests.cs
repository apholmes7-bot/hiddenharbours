using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Store clothing, pass 1: the per-player wardrobe (owned, worn, effects summed), the rack's stock and
    /// the one buying transaction. Every garment and number here is the test's own.
    /// </summary>
    public class ClothingWardrobeTests
    {
        private readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<PlayerClothingChanged>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<PlayerClothingChanged>();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        internal static ClothingDef Garment(List<Object> made, string id, ClothingSlot slot, int price,
                                            params ClothingEffect[] effects)
        {
            var def = ScriptableObject.CreateInstance<ClothingDef>();
            def.Id = id;
            def.DisplayName = id;
            def.Slot = slot;
            def.Price = price;
            def.Does = "test";
            def.Effects = effects;
            made.Add(def);
            return def;
        }

        internal static ClothingRackDef Rack(List<Object> made, params (ClothingDef def, int stock)[] lines)
        {
            var rack = ScriptableObject.CreateInstance<ClothingRackDef>();
            rack.Id = "clothing_rack.test";
            rack.ShopId = "shop.test";
            rack.Lines = new ClothingRackLine[lines.Length];
            for (int i = 0; i < lines.Length; i++) rack.Lines[i] = new ClothingRackLine(lines[i].def, lines[i].stock);
            made.Add(rack);
            return rack;
        }

        internal sealed class CountingWallet : IWallet
        {
            public int Money { get; set; }
            public int Spends;
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                Spends++;
                if (amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        [Test]
        public void Wardrobe_WearsOnlyWhatHeOwns_OnePerSlot()
        {
            ClothingDef cap = Garment(_made, "clothing.cap", ClothingSlot.Head, 5);
            ClothingDef hat = Garment(_made, "clothing.hat", ClothingSlot.Head, 5);
            var w = new PlayerWardrobe(ClothingPlayers.Local);

            Assert.IsFalse(w.Wear(cap), "not his yet");
            Assert.IsNull(w.WornIn(ClothingSlot.Head));
            Assert.IsTrue(w.Grant(cap));
            Assert.IsFalse(w.Grant(cap), "a garment is owned once");
            Assert.IsTrue(w.Wear(cap));
            Assert.AreSame(cap, w.WornIn(ClothingSlot.Head));

            w.Grant(hat);
            Assert.IsTrue(w.Wear(hat));
            Assert.AreSame(hat, w.WornIn(ClothingSlot.Head), "the hat replaces the cap in the one head slot");
            Assert.IsFalse(w.IsWearing(cap));
            Assert.IsTrue(w.Owns(cap), "taking a garment off never loses it");

            Assert.IsTrue(w.TakeOff(ClothingSlot.Head));
            Assert.IsFalse(w.TakeOff(ClothingSlot.Head), "already bare");
        }

        [Test]
        public void Wardrobe_NothingWorn_EveryEffectIsExactlyOne()
        {
            var w = new PlayerWardrobe(ClothingPlayers.Local);
            foreach (NeedKind need in System.Enum.GetValues(typeof(NeedKind)))
                Assert.AreEqual(1f, w.NeedRelief(need));
            foreach (DirtActivity a in System.Enum.GetValues(typeof(DirtActivity)))
                Assert.AreEqual(1f, w.DirtScale(a));
        }

        [Test]
        public void Wardrobe_EffectsAddWhenWorn_AndGoWhenTakenOff()
        {
            ClothingDef sweater = Garment(_made, "clothing.sweater", ClothingSlot.Top, 10,
                                          ClothingEffect.Relief(NeedKind.Tiredness, 0.75f));
            ClothingDef gloves = Garment(_made, "clothing.gloves", ClothingSlot.Hands, 5,
                                         ClothingEffect.Dirt(DirtActivity.Bait, 0.5f));
            ClothingDef oilskins = Garment(_made, "clothing.oilskins", ClothingSlot.Outerwear, 30,
                                           ClothingEffect.Dirt(DirtActivity.Bait, 0.8f));
            var w = new PlayerWardrobe(ClothingPlayers.Local);
            w.Grant(sweater); w.Grant(gloves); w.Grant(oilskins);

            w.Wear(sweater);
            Assert.AreEqual(0.75f, w.NeedRelief(NeedKind.Tiredness), 1e-6f);
            Assert.AreEqual(1f, w.NeedRelief(NeedKind.Hunger), "only the need it names");

            w.Wear(gloves);
            w.Wear(oilskins);
            Assert.AreEqual(0.4f, w.DirtScale(DirtActivity.Bait), 1e-6f, "two garments on one kind of work multiply");
            Assert.AreEqual(1f, w.DirtScale(DirtActivity.FishHandling));

            w.TakeOff(ClothingSlot.Hands);
            Assert.AreEqual(0.8f, w.DirtScale(DirtActivity.Bait), 1e-6f, "the gloves' part goes with the gloves");
            w.TakeOff(ClothingSlot.Top);
            Assert.AreEqual(1f, w.NeedRelief(NeedKind.Tiredness), "off is exactly 1 again");
        }

        [Test]
        public void Wardrobe_ChangeIsAnnounced_WithWhoseItIs()
        {
            var seen = new List<string>();
            System.Action<PlayerClothingChanged> on = e => seen.Add(e.PlayerKey);
            EventBus.Subscribe(on);
            try
            {
                ClothingDef cap = Garment(_made, "clothing.cap", ClothingSlot.Head, 5);
                var w = new PlayerWardrobe("player.two");
                int before = w.Revision;
                w.Grant(cap);
                w.Wear(cap);
                Assert.AreEqual(before + 2, w.Revision);
                CollectionAssert.AreEqual(new[] { "player.two", "player.two" }, seen);
            }
            finally { EventBus.Unsubscribe(on); }
        }

        [Test]
        public void TwoPlayers_WardrobesAreIndependent()
        {
            ClothingDef sweater = Garment(_made, "clothing.sweater", ClothingSlot.Top, 10,
                                          ClothingEffect.Relief(NeedKind.Tiredness, 0.5f));
            var service = new ClothingService(() => new[] { sweater });
            PlayerWardrobe a = service.For("player.a");
            PlayerWardrobe b = service.For("player.b");

            Assert.AreNotSame(a, b);
            Assert.AreSame(a, service.For("player.a"), "one wardrobe per player, kept");
            a.Grant(sweater);
            a.Wear(sweater);

            Assert.IsTrue(a.IsWearing(sweater));
            Assert.IsFalse(b.Owns(sweater), "what one player buys is his alone");
            Assert.IsNull(b.WornIn(ClothingSlot.Top));
            Assert.AreEqual(1f, b.NeedRelief(NeedKind.Tiredness), "and so are its effects");
            Assert.AreEqual(1f, service.Local.NeedRelief(NeedKind.Tiredness), "the local player is a third wardrobe");
        }

        [Test]
        public void Buy_DebitsOnce_TakesOneOfThatGarmentOnly_AndPutsItOn()
        {
            ClothingDef boots = Garment(_made, "clothing.boots", ClothingSlot.Feet, 18);
            ClothingDef cap = Garment(_made, "clothing.cap", ClothingSlot.Head, 5);
            var stock = new ClothingRackStock(Rack(_made, (boots, 2), (cap, 3)));
            var w = new PlayerWardrobe(ClothingPlayers.Local);
            var wallet = new CountingWallet { Money = 50 };

            Assert.AreEqual(ClothingBuyResult.Bought, ClothingShop.TryBuy(stock, w, wallet, boots));
            Assert.AreEqual(32, wallet.Money, "paid the price");
            Assert.AreEqual(1, wallet.Spends, "exactly one charge");
            Assert.AreEqual(1, stock.Left(boots));
            Assert.AreEqual(3, stock.Left(cap), "another garment's stock is untouched");
            Assert.IsTrue(w.Owns(boots));
            Assert.IsTrue(w.IsWearing(boots), "buying puts it on");

            Assert.AreEqual(ClothingBuyResult.AlreadyOwned, ClothingShop.TryBuy(stock, w, wallet, boots));
            Assert.AreEqual(1, wallet.Spends, "a second confirm never charges again");
            Assert.AreEqual(1, stock.Left(boots));
        }

        [Test]
        public void Buy_Refusals_ChangeNothing()
        {
            ClothingDef boots = Garment(_made, "clothing.boots", ClothingSlot.Feet, 18);
            ClothingDef coat = Garment(_made, "clothing.coat", ClothingSlot.Outerwear, 99);
            ClothingDef elsewhere = Garment(_made, "clothing.elsewhere", ClothingSlot.Top, 1);
            var stock = new ClothingRackStock(Rack(_made, (boots, 0), (coat, 1)));
            var w = new PlayerWardrobe(ClothingPlayers.Local);
            var wallet = new CountingWallet { Money = 20 };

            Assert.AreEqual(ClothingBuyResult.SoldOut, ClothingShop.TryBuy(stock, w, wallet, boots));
            Assert.AreEqual(ClothingBuyResult.TooDear, ClothingShop.TryBuy(stock, w, wallet, coat));
            Assert.AreEqual(ClothingBuyResult.NotStocked, ClothingShop.TryBuy(stock, w, wallet, elsewhere));
            Assert.AreEqual(ClothingBuyResult.NoWallet, ClothingShop.TryBuy(stock, w, null, coat));

            Assert.AreEqual(20, wallet.Money);
            Assert.AreEqual(1, stock.Left(coat));
            Assert.AreEqual(0, w.Owned.Count);
            Assert.IsNull(w.WornIn(ClothingSlot.Outerwear));
        }
    }
}
