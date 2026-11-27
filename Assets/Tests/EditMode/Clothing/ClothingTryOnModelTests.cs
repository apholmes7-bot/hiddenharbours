using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Store clothing, pass 1: the try-on page as a rule. Trying on is a preview only; leaving puts back
    /// exactly what he had; buying is the one way a garment becomes his.
    /// </summary>
    public class ClothingTryOnModelTests
    {
        private readonly List<Object> _made = new List<Object>();

        private sealed class FakeAppearance : IPlayerAppearance
        {
            public CharacterRecipe Committed { get; set; } = new CharacterRecipe { Outfit = "teal", Shirt = "white" };
            public CharacterRecipe Previewed;
            public CharacterRecipe Showing => Previewed ?? Committed;
            public bool IsPreviewing => Previewed != null;
            public bool RefuseAll;
            public int Commits, Previews, Cancels;

            public bool Preview(CharacterRecipe recipe, out string refusal)
            {
                Previews++;
                if (RefuseAll) { refusal = "refused"; return false; }
                refusal = null;
                Previewed = recipe.Clone();
                return true;
            }

            public void CancelPreview() { Cancels++; Previewed = null; }

            public bool Commit(CharacterRecipe recipe, out string refusal)
            {
                Commits++;
                refusal = null;
                Committed = recipe.Clone();
                Previewed = null;
                return true;
            }
        }

        private ClothingDef _oilskins, _cap, _sweater;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<PlayerClothingChanged>();
            _oilskins = ClothingWardrobeTests.Garment(_made, "clothing.oilskins", ClothingSlot.Outerwear, 40,
                ClothingEffect.Dirt(DirtActivity.FishHandling, 0.5f));
            _oilskins.StandIn = new[] { new ClothingLookKey("outfit", "oil") };
            _cap = ClothingWardrobeTests.Garment(_made, "clothing.cap", ClothingSlot.Head, 5);
            _sweater = ClothingWardrobeTests.Garment(_made, "clothing.sweater", ClothingSlot.Top, 16,
                ClothingEffect.Relief(NeedKind.Tiredness, 0.75f));
            _sweater.StandIn = new[] { new ClothingLookKey("shirt", "cream") };
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<PlayerClothingChanged>();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private ClothingRackStock Stock() =>
            new ClothingRackStock(ClothingWardrobeTests.Rack(_made, (_oilskins, 2), (_cap, 3), (_sweater, 1)));

        private static int RowOf(ClothingTryOnModel m, ClothingDef def)
        {
            for (int r = 0; r < m.RowCount; r++) if (m.GarmentAt(r) == def) return r;
            return -1;
        }

        [Test]
        public void Rows_InSlotOrder_LeaveLast()
        {
            var m = new ClothingTryOnModel(Stock(), new PlayerWardrobe(ClothingPlayers.Local),
                                           new ClothingWardrobeTests.CountingWallet(), new FakeAppearance());
            Assert.AreEqual(4, m.RowCount);
            Assert.AreSame(_cap, m.GarmentAt(0), "head first");
            Assert.AreSame(_sweater, m.GarmentAt(1), "then top");
            Assert.AreSame(_oilskins, m.GarmentAt(2), "outerwear after top");
            Assert.AreEqual(3, m.LeaveRow);
            Assert.IsNull(m.GarmentAt(m.LeaveRow));
        }

        [Test]
        public void TryingOn_NeverChangesOwnedOrWorn_UntilHeBuys()
        {
            var wardrobe = new PlayerWardrobe(ClothingPlayers.Local);
            var wallet = new ClothingWardrobeTests.CountingWallet { Money = 100 };
            var look = new FakeAppearance();
            var m = new ClothingTryOnModel(Stock(), wardrobe, wallet, look);

            for (int r = 0; r < m.LeaveRow; r++) m.Select(r);

            Assert.AreSame(_oilskins, m.TriedIn(ClothingSlot.Outerwear));
            Assert.AreSame(_cap, m.TriedIn(ClothingSlot.Head));
            Assert.AreEqual(0, wardrobe.Owned.Count, "a try is not a purchase");
            for (int s = 0; s < ClothingSlots.Count; s++) Assert.IsNull(wardrobe.WornIn((ClothingSlot)s));
            Assert.AreEqual(100, wallet.Money);
            Assert.AreEqual(0, wallet.Spends);
            Assert.AreEqual(0, look.Commits, "the page never commits his look");
            Assert.AreEqual(1f, wardrobe.DirtScale(DirtActivity.FishHandling), "a try carries no effect");
            Assert.AreEqual(2, m.Left(_oilskins), "a try takes nothing off the rack");
        }

        [Test]
        public void Leaving_RestoresExactly()
        {
            var wardrobe = new PlayerWardrobe(ClothingPlayers.Local);
            wardrobe.Grant(_cap);
            wardrobe.Wear(_cap);
            var look = new FakeAppearance();
            CharacterRecipe before = look.Committed.Clone();
            var m = new ClothingTryOnModel(Stock(), wardrobe, new ClothingWardrobeTests.CountingWallet(), look);

            m.Select(RowOf(m, _oilskins));
            m.Select(RowOf(m, _sweater));
            Assert.IsTrue(look.IsPreviewing);
            Assert.AreEqual("oil", look.Showing.Outfit);
            Assert.AreEqual("cream", look.Showing.Shirt);

            m.Leave();

            Assert.IsFalse(m.IsOpen);
            Assert.IsFalse(look.IsPreviewing, "his own clothes are back on the figure");
            Assert.IsTrue(before.SameAs(look.Committed));
            Assert.IsTrue(before.SameAs(look.Showing));
            Assert.AreEqual(0, look.Commits);
            Assert.AreSame(_cap, wardrobe.WornIn(ClothingSlot.Head), "what he wore, he still wears");
            Assert.IsNull(wardrobe.WornIn(ClothingSlot.Outerwear));
            Assert.IsNull(m.TriedIn(ClothingSlot.Outerwear));
            Assert.AreEqual(1, wardrobe.Owned.Count);
        }

        [Test]
        public void Buying_DebitsOnce_DropsOnlyThatStock_AndPutsItOn()
        {
            var wardrobe = new PlayerWardrobe(ClothingPlayers.Local);
            var wallet = new ClothingWardrobeTests.CountingWallet { Money = 100 };
            var m = new ClothingTryOnModel(Stock(), wardrobe, wallet, new FakeAppearance());

            m.Select(RowOf(m, _oilskins));
            Assert.IsTrue(m.Activate());

            Assert.AreEqual(ClothingBuyResult.Bought, m.LastResult);
            Assert.AreEqual(60, wallet.Money);
            Assert.AreEqual(1, wallet.Spends);
            Assert.AreEqual(1, m.Left(_oilskins));
            Assert.AreEqual(3, m.Left(_cap));
            Assert.AreEqual(1, m.Left(_sweater));
            Assert.IsTrue(m.Wears(_oilskins));
            Assert.IsNull(m.TriedIn(ClothingSlot.Outerwear), "bought, so worn rather than tried");
            Assert.AreSame(_oilskins, m.ShowingIn(ClothingSlot.Outerwear));
            Assert.AreEqual(0.5f, wardrobe.DirtScale(DirtActivity.FishHandling), 1e-6f, "its effect is on him");

            Assert.IsFalse(m.Activate(), "already his and on him");
            Assert.AreEqual(1, wallet.Spends, "a second press never charges again");
            Assert.AreEqual(1, m.Left(_oilskins));
        }

        [Test]
        public void Buying_TooDear_ChangesNothing()
        {
            var wardrobe = new PlayerWardrobe(ClothingPlayers.Local);
            var wallet = new ClothingWardrobeTests.CountingWallet { Money = 10 };
            var m = new ClothingTryOnModel(Stock(), wardrobe, wallet, new FakeAppearance());

            m.Select(RowOf(m, _oilskins));
            Assert.IsFalse(m.Activate());
            Assert.AreEqual(ClothingBuyResult.TooDear, m.LastResult);
            Assert.AreEqual(10, wallet.Money);
            Assert.AreEqual(2, m.Left(_oilskins));
            Assert.IsFalse(m.Owns(_oilskins));
            Assert.AreSame(_oilskins, m.TriedIn(ClothingSlot.Outerwear), "still trying it on");
        }

        [Test]
        public void OwnedGarment_PutsOnFree()
        {
            var wardrobe = new PlayerWardrobe(ClothingPlayers.Local);
            wardrobe.Grant(_sweater);
            var wallet = new ClothingWardrobeTests.CountingWallet { Money = 100 };
            var m = new ClothingTryOnModel(Stock(), wardrobe, wallet, new FakeAppearance());

            m.Select(RowOf(m, _sweater));
            Assert.IsTrue(m.Activate());
            Assert.IsTrue(wardrobe.IsWearing(_sweater));
            Assert.AreEqual(0, wallet.Spends);
            Assert.AreEqual(1, m.Left(_sweater));
        }

        [Test]
        public void Try_ShowsTheStandIn_OrHimAsHeIs()
        {
            var look = new FakeAppearance();
            var m = new ClothingTryOnModel(Stock(), new PlayerWardrobe(ClothingPlayers.Local),
                                           new ClothingWardrobeTests.CountingWallet(), look);
            Assert.IsFalse(m.StandInShown, "nothing worn, nothing painted");

            m.Select(RowOf(m, _cap));
            Assert.IsFalse(m.StandInShown, "the cap has no stand-in yet");
            Assert.IsFalse(look.IsPreviewing);

            m.Select(RowOf(m, _oilskins));
            Assert.IsTrue(m.StandInShown);
            Assert.AreEqual("oil", look.Showing.Outfit);
            Assert.AreEqual("white", look.Showing.Shirt, "fields no garment paints stay his own");
        }

        [Test]
        public void Try_RefusedByTheBuilder_ShowsHimAsHeIs()
        {
            var look = new FakeAppearance { RefuseAll = true };
            var m = new ClothingTryOnModel(Stock(), new PlayerWardrobe(ClothingPlayers.Local),
                                           new ClothingWardrobeTests.CountingWallet(), look);
            m.Select(RowOf(m, _oilskins));
            Assert.IsFalse(m.StandInShown);
            Assert.IsFalse(look.IsPreviewing);
            Assert.AreSame(_oilskins, m.TriedIn(ClothingSlot.Outerwear), "still listed as tried, picture to come");
        }

        [Test]
        public void Input_KeyboardAndGamepad_MoveBuyAndLeave()
        {
            var wardrobe = new PlayerWardrobe(ClothingPlayers.Local);
            var wallet = new ClothingWardrobeTests.CountingWallet { Money = 100 };
            var m = new ClothingTryOnModel(Stock(), wardrobe, wallet, new FakeAppearance());

            m.Input(0f, false, false);      // release the push that opened the page
            m.Input(-1f, false, false);     // down one row
            Assert.AreEqual(1, m.FocusRow);
            m.Input(-1f, false, false);     // held: no repeat
            Assert.AreEqual(1, m.FocusRow);
            m.Input(0f, false, false);
            m.Input(1f, false, false);      // up one row
            Assert.AreEqual(0, m.FocusRow);
            m.Input(0f, false, false);
            m.Input(1f, false, false);      // up from the top wraps to Leave
            Assert.AreEqual(m.LeaveRow, m.FocusRow);

            m.Input(0f, false, false);
            m.Input(-1f, false, false);     // wraps back to the first garment (the cap)
            m.Input(0f, true, false);       // Interact buys
            Assert.IsTrue(wardrobe.IsWearing(_cap));
            Assert.AreEqual(1, wallet.Spends);

            m.Input(0f, false, true);       // Cancel leaves
            Assert.IsFalse(m.IsOpen);
            m.Input(-1f, true, false);
            Assert.AreEqual(1, wallet.Spends, "a closed page does nothing");
        }
    }
}
