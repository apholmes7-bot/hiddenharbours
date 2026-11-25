using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Store sections (owner ask 2026-11-25: several foods on one shelf, the player picks which with the
    /// quick chooser, freeing room for the fishing goods). A section gathers several slots' goods into ONE
    /// place to reach; its press opens the existing chooser with one row per good (name, price, how many are
    /// left); a row buys exactly as pressing that good alone does, through the same till and into the same
    /// bag; each good still runs out on its own, and a sold-out good's row says so and cannot be picked.
    /// The till is a plain double here: the money and the shelf counts behind it are the Economy's, pinned
    /// in <c>ShopAtTheShelfTests</c>.
    /// </summary>
    public class StoreSectionsTests
    {
        private const string Shop = "shop.test_store";
        private const float Arc = 120f;

        private readonly List<Object> _spawned = new List<Object>();
        private readonly List<string> _cues = new List<string>();
        private int _frame;

        // ---- fakes -------------------------------------------------------------------------------------------

        private sealed class Till : IShelfTill
        {
            public readonly Dictionary<string, (string Name, int Price)> Goods =
                new Dictionary<string, (string, int)>();
            public readonly Dictionary<string, int> OnShelf = new Dictionary<string, int>();
            public readonly List<string> Bought = new List<string>();
            public int Money = 100;
            public string ShopId => Shop;

            public void Stock(string listing, string name, int price, int count)
            {
                Goods[listing] = (name, price);
                OnShelf[listing] = count;
            }

            public bool TryQuote(string listingId, double totalSeconds, out ShelfQuote quote)
            {
                quote = default;
                if (!Goods.TryGetValue(listingId, out var g)) return false;
                quote = new ShelfQuote(listingId, g.Name, g.Price, OnShelf[listingId]);
                return true;
            }

            public ShelfBuyResult TryBuyOne(string listingId, double totalSeconds)
            {
                if (!Goods.TryGetValue(listingId, out var g)) return ShelfBuyResult.NotSold;
                if (OnShelf[listingId] <= 0) return ShelfBuyResult.SoldOut;
                if (Money < g.Price) return ShelfBuyResult.TooDear;
                Money -= g.Price;
                OnShelf[listingId]--;
                Bought.Add(listingId);
                return ShelfBuyResult.Bought;
            }
        }

        private sealed class FakeBagHands : IShoppingBagHands
        {
            public readonly List<string> Bagged = new List<string>();
            public bool HoldsBag => Bagged.Count > 0;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => ShoppingBagRefusal.None;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId)
            {
                Bagged.Add(supplyId);
                return ShoppingBagRefusal.None;
            }
            public bool TryPutAway(string storageId) { Bagged.Clear(); return true; }
        }

        private Till _till;
        private FakeBagHands _bag;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            InteractActorProbe.Reset();
            InteractChooser.Reset();
            _frame = 100;
            InteractChooser.FrameClock = () => _frame;
            ShelfTills.Clear();
            _till = new Till();
            _till.Stock("supply.bread", "Loaf of Bread", 4, 3);
            _till.Stock("supply.oatcakes", "Oatcakes", 2, 2);
            _till.Stock("supply.tinned_beans", "Tin of Beans", 3, 1);
            _till.Stock("bait.capelin", "Capelin", 2, 5);
            ShelfTills.Register(_till);
            _bag = new FakeBagHands();
            GameServices.BagHands = _bag;
            _cues.Clear();
            EventBus.Subscribe<InnerVoiceCue>(OnCue);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<InnerVoiceCue>(OnCue);
            InteractChooser.Reset();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            InteractActorProbe.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            Interactables.Clear();
            ShelfTills.Clear();
            GameServices.Reset();
        }

        private void OnCue(InnerVoiceCue c) => _cues.Add(c.CueId);

        // A store with three foods in one section, and the capelin on its own beside them.
        private SoldOutSigns MakeStore(bool withSection = true)
        {
            var def = ScriptableObject.CreateInstance<ShopShelfSlotsDef>();
            _spawned.Add(def);
            def.Id = "shelfslots.test_store";
            def.ShopId = Shop;
            def.ReachMeters = 1.5f;
            def.Bag = ScriptableObject.CreateInstance<ShoppingBagDef>();
            _spawned.Add(def.Bag);
            def.Slots.Add(new ShopShelfSlot { Id = "slot.bread", LineId = "line.bread", ListingId = "supply.bread", Offset = new Vector2(-0.5f, 0.3f), IntoBag = true });
            def.Slots.Add(new ShopShelfSlot { Id = "slot.oatcakes", LineId = "line.oatcakes", ListingId = "supply.oatcakes", Offset = new Vector2(0.5f, 0.3f), IntoBag = true });
            def.Slots.Add(new ShopShelfSlot { Id = "slot.beans", LineId = "line.tinned_beans", ListingId = "supply.tinned_beans", Offset = new Vector2(0f, -0.2f), IntoBag = true });
            def.Slots.Add(new ShopShelfSlot { Id = "slot.bait", LineId = "line.bait_box", ListingId = "bait.capelin", Offset = new Vector2(6f, 0f) });
            if (withSection)
                def.Sections.Add(new ShopShelfSection
                {
                    Id = "section.food",
                    VerbLabel = "Look at the groceries",
                    Offset = Vector2.zero,
                    SlotIds = new List<string> { "slot.bread", "slot.oatcakes", "slot.beans" },
                });

            var go = new GameObject("TestStore");
            _spawned.Add(go);
            var signs = go.AddComponent<SoldOutSigns>();
            signs.SetInsideTest(() => true);
            signs.Configure(def, null);
            signs.Refresh(0.0, true);
            // ⚠️ EditMode never fires OnEnable: register what OnEnable registers at runtime (a section, and a
            // good in no section). PlayMode (StoreSectionsPlayTests) proves the self-registration.
            foreach (ShelfSection s in signs.Sections) Interactables.Register(s);
            foreach (ShelfGood g in signs.ShelfGoods) if (g != null && !g.InSection) Interactables.Register(g);
            return signs;
        }

        private static InteractActor ActorAt(Vector2 at) => new InteractActor(at, Vector2.up, InteractContext.OnFoot);

        private static string Row(string name, int price, int left)
            => name + " — " + MoneyFormat.Balance(price) + " · " + left + " left";

        // ---- the section stands in for its goods ----------------------------------------------------------

        [Test]
        public void TheSectionIsTheOneThingToReach_ItsGoodsAreNotOfferedOnTheirOwn()
        {
            SoldOutSigns signs = MakeStore();
            Assert.That(signs.Sections.Length, Is.EqualTo(1));
            ShelfSection section = signs.Sections[0];
            Assert.That(section.Id, Is.EqualTo("shelfslots.test_store.section.food"));
            Assert.That(section.Goods, Is.EqualTo(new[] { signs.ShelfGoods[0], signs.ShelfGoods[1], signs.ShelfGoods[2] }));

            Assert.IsFalse(signs.ShelfGoods[3].InSection, "a good in no section still sells on its own");
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(signs.ShelfGoods[i].InSection, signs.ShelfGoods[i].ListingId + " sells only through its section");

            // A good handed to a section leaves the registry even if it had registered first.
            ShelfGood bread = signs.ShelfGoods[0];
            Interactables.Register(bread);
            bread.PutInSection();
            Assert.That(new List<IInteractable>(Interactables.Active), Has.No.Member(bread));
            Assert.IsTrue(section.IsAvailable);
            Assert.That(section.VerbLabel, Is.EqualTo("Look at the groceries"));
        }

        [Test]
        public void WithoutASection_TheGoodsSellOnTheirOwnAsBefore()
        {
            SoldOutSigns signs = MakeStore(withSection: false);
            Assert.That(signs.Sections, Is.Empty);
            for (int i = 0; i < 4; i++) Assert.IsFalse(signs.ShelfGoods[i].InSection);
            Assert.That(signs.ShelfGoods[0].ChoiceLabel, Is.Empty, "no section row words are built for a good alone");
            Assert.That(signs.ShelfGoods[0].VerbLabel, Is.EqualTo("Buy Loaf of Bread — " + MoneyFormat.Balance(4)));
        }

        [Test]
        public void EachRowSaysTheGoodItsPriceAndHowManyAreLeft()
        {
            ShelfSection section = MakeStore().Sections[0];
            Assert.That(section.ActionCount, Is.EqualTo(3));
            Assert.That(section.ActionLabel(0), Is.EqualTo(Row("Loaf of Bread", 4, 3)));
            Assert.That(section.ActionLabel(1), Is.EqualTo(Row("Oatcakes", 2, 2)));
            Assert.That(section.ActionLabel(2), Is.EqualTo(Row("Tin of Beans", 3, 1)));
            for (int i = 0; i < 3; i++) Assert.IsTrue(section.ActionAvailable(i), "row " + i);
            Assert.IsNull(section.ActionLabel(3), "no row past the end");
            Assert.IsFalse(section.ActionAvailable(-1));
        }

        // ---- buying through a row ----------------------------------------------------------------------

        [Test]
        public void BuyingTheSecondRow_TakesOnlyThatGood_PaysItsPrice_AndBagsIt()
        {
            SoldOutSigns signs = MakeStore();
            ShelfSection section = signs.Sections[0];

            Assert.That(section.Buy(1, 0.0), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(_till.Money, Is.EqualTo(100 - 2));
            Assert.That(_till.Bought, Is.EqualTo(new[] { "supply.oatcakes" }));
            Assert.That(_till.OnShelf["supply.oatcakes"], Is.EqualTo(1));
            Assert.That(_till.OnShelf["supply.bread"], Is.EqualTo(3), "the bread is its own stock");
            Assert.That(_till.OnShelf["supply.tinned_beans"], Is.EqualTo(1), "the beans are their own stock");
            Assert.That(_bag.Bagged, Is.EqualTo(new[] { "supply.oatcakes" }), "into the paper bag, as at its own shelf");
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBagged }));
            Assert.That(section.ActionLabel(1), Is.EqualTo(Row("Oatcakes", 2, 1)), "the row counts down at once");
        }

        [Test]
        public void ARowBuysExactlyWhatPressingThatGoodAloneBuys()
        {
            SoldOutSigns signs = MakeStore();
            ShelfSection section = signs.Sections[0];
            ShelfBuyResult viaRow = section.Buy(0, 0.0);
            int moneyAfterRow = _till.Money;
            ShelfBuyResult alone = signs.ShelfGoods[0].Press(0.0);
            Assert.That(viaRow, Is.EqualTo(alone));
            Assert.That(100 - moneyAfterRow, Is.EqualTo(moneyAfterRow - _till.Money), "the same price both ways");
            Assert.That(_till.Bought, Is.EqualTo(new[] { "supply.bread", "supply.bread" }));
            Assert.That(_bag.Bagged, Is.EqualTo(new[] { "supply.bread", "supply.bread" }));
        }

        [Test]
        public void ASoldOutGood_SaysSoInItsRow_CannotBePicked_AndTheOthersStillSell()
        {
            SoldOutSigns signs = MakeStore();
            ShelfSection section = signs.Sections[0];
            Assert.That(section.Buy(2, 0.0), Is.EqualTo(ShelfBuyResult.Bought), "the last tin");
            Assert.That(section.ActionLabel(2), Is.EqualTo("Tin of Beans — sold out"));
            Assert.IsFalse(section.ActionAvailable(2), "a sold-out row is greyed");
            Assert.IsTrue(section.ActionAvailable(0));
            Assert.IsTrue(section.ActionAvailable(1));
            Assert.That(section.Buy(2, 0.0), Is.EqualTo(ShelfBuyResult.SoldOut), "and buys nothing if forced");
            Assert.That(_till.Bought, Is.EqualTo(new[] { "supply.tinned_beans" }));
            Assert.That(section.Buy(0, 0.0), Is.EqualTo(ShelfBuyResult.Bought), "the bread is still there");
        }

        [Test]
        public void APressWithAllButOneSoldOut_BuysTheOneLeft()
        {
            ShelfSection section = MakeStore().Sections[0];
            _till.OnShelf["supply.bread"] = 0;
            _till.OnShelf["supply.tinned_beans"] = 0;
            foreach (ShelfGood g in section.Goods) g.Refresh(0.0);
            section.Interact(ActorAt(Vector2.zero));
            Assert.That(_till.Bought, Is.EqualTo(new[] { "supply.oatcakes" }));
        }

        [Test]
        public void APressWithEverythingSoldOut_SaysTheShelfIsEmpty_AndMovesNothing()
        {
            ShelfSection section = MakeStore().Sections[0];
            foreach (string l in new[] { "supply.bread", "supply.oatcakes", "supply.tinned_beans" }) _till.OnShelf[l] = 0;
            foreach (ShelfGood g in section.Goods) g.Refresh(0.0);
            Assert.IsTrue(section.IsAvailable, "still offered: the rows say why");
            section.Interact(ActorAt(Vector2.zero));
            Assert.That(_till.Bought, Is.Empty);
            Assert.That(_till.Money, Is.EqualTo(100));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfSoldOut }));
        }

        [Test]
        public void ShortOfMoney_TheRowRefuses_AndNothingMoves()
        {
            ShelfSection section = MakeStore().Sections[0];
            _till.Money = 3;
            Assert.That(section.Buy(0, 0.0), Is.EqualTo(ShelfBuyResult.TooDear));
            Assert.That(_till.Money, Is.EqualTo(3));
            Assert.That(_till.OnShelf["supply.bread"], Is.EqualTo(3));
            Assert.That(_bag.Bagged, Is.Empty);
        }

        // ---- through the chooser, as a key press does ----------------------------------------------------

        [Test]
        public void ThePressOpensTheChooser_OneRowPerGood_AndPickingTheSecondBuysIt()
        {
            MakeStore();
            InteractActor actor = ActorAt(new Vector2(0f, -0.8f));
            Assert.IsTrue(InteractVerb.TryPress(actor, Arc), "the press is taken");
            Assert.IsTrue(InteractChooser.IsOpen, "three goods to choose from: the chooser opens");
            InteractChoiceList rows = InteractChooser.Rows;
            Assert.That(rows.Count, Is.EqualTo(3), "the capelin is out of reach");
            Assert.That(rows[0].Label, Is.EqualTo(Row("Loaf of Bread", 4, 3)));
            Assert.That(rows[1].Label, Is.EqualTo(Row("Oatcakes", 2, 2)));
            Assert.That(rows[2].Label, Is.EqualTo(Row("Tin of Beans", 3, 1)));
            Assert.That(_till.Bought, Is.Empty, "opening the list buys nothing");

            Assert.IsTrue(InteractChooser.Pick(1));
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.That(_till.Bought, Is.EqualTo(new[] { "supply.oatcakes" }));
            Assert.That(_till.Money, Is.EqualTo(98));
            Assert.That(_bag.Bagged, Is.EqualTo(new[] { "supply.oatcakes" }));
        }

        [Test]
        public void TheChooser_GreysASoldOutGood_AndStepsOverIt()
        {
            SoldOutSigns signs = MakeStore();
            _till.OnShelf["supply.oatcakes"] = 0;
            signs.Refresh(0.0, true);
            InteractActor actor = ActorAt(new Vector2(0f, -0.8f));
            Assert.IsTrue(InteractVerb.TryPress(actor, Arc));
            Assert.IsTrue(InteractChooser.IsOpen);
            InteractChoiceList rows = InteractChooser.Rows;
            Assert.That(rows[1].Label, Is.EqualTo("Oatcakes — sold out"));
            Assert.IsFalse(rows[1].Enabled, "greyed");
            Assert.That(rows.Selected, Is.EqualTo(0));
            Assert.IsTrue(InteractChooser.Step(1));
            Assert.That(rows.Selected, Is.EqualTo(2), "the selection steps over the sold-out row");
            Assert.IsFalse(InteractChooser.Pick(1), "a click on it does nothing");
            Assert.IsTrue(InteractChooser.IsOpen, "and the list stays up");
            Assert.That(_till.Bought, Is.Empty);
        }

        [Test]
        public void TheRowsAreRebuiltOnlyWhenAQuoteMoves()
        {
            ShelfSection section = MakeStore().Sections[0];
            string before = section.ActionLabel(0);
            section.Goods[0].Refresh(0.0);
            Assert.That(section.ActionLabel(0), Is.SameAs(before), "an unchanged quote keeps the cached words");
            _till.OnShelf["supply.bread"] = 2;
            section.Goods[0].Refresh(0.0);
            Assert.That(section.ActionLabel(0), Is.EqualTo(Row("Loaf of Bread", 4, 2)));
        }
    }
}
