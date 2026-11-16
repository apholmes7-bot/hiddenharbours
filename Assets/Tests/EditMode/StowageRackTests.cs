using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rod rack in the hands: a rod goes in and comes back out as the same object, a shovel is refused
    /// and stays in the hand, a full rack refuses and the rod stays in the hand, a refused take leaves the
    /// rod in the rack. Nothing is ever destroyed. Plus the shipped content: the rod is a Rod, the rack's
    /// Def takes rods only, and its offer is bought through the existing gear purchase seam.
    /// </summary>
    public class StowageRackTests
    {
        private const string RodToolPath = "Assets/_Project/Data/Tools/Rod.asset";
        private const string ShovelToolPath = "Assets/_Project/Data/Tools/Shovel.asset";
        private const string RackDefPath = "Assets/_Project/Data/Stowage/RodRack.asset";
        private const string RackOfferPath = "Assets/_Project/Data/Resources/Catalog/Gear/RodRack.asset";

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int money) { Money = money; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures ------------------------------------------------------------------------------------

        private CarriableTool MakeTool(string id, StowageClass stowage)
        {
            var def = ScriptableObject.CreateInstance<ToolDef>();
            def.Id = id;
            def.DisplayName = id;
            def.Stowage = stowage;
            _spawned.Add(def);

            var go = new GameObject($"Tool({id})");
            _spawned.Add(go);
            var tool = go.AddComponent<CarriableTool>();
            tool.Configure(def, go.AddComponent<SpriteRenderer>(), $"{id}#{_spawned.Count}");
            return tool;
        }

        private StowageRack MakeRack(int capacity, StowageAccepts accepts = StowageAccepts.Rods)
        {
            var def = ScriptableObject.CreateInstance<StowageDef>();
            def.Id = "stowage.test_rack";
            def.DisplayName = "rod rack";
            def.Accepts = accepts;
            def.Capacity = capacity;
            _spawned.Add(def);

            var go = new GameObject("RodRack");
            _spawned.Add(go);
            var holder = new GameObject("Slots");
            holder.transform.SetParent(go.transform, false);
            var rack = go.AddComponent<StowageRack>();
            rack.Configure(def, holder.transform, "stowage.test_rack#1");
            return rack;
        }

        private CarryHands HandsHolding(CarriableTool tool)
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(tool), "fixture: the tool is in hand");
            return hands;
        }

        // ---- accept ----------------------------------------------------------------------------------------

        [Test]
        public void A_held_rod_goes_into_the_rack()
        {
            CarriableTool rod = MakeTool(ToolDef.RodId, StowageClass.Rod);
            CarryHands hands = HandsHolding(rod);
            StowageRack rack = MakeRack(capacity: 4);

            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));

            Assert.IsFalse(hands.IsCarrying, "out of her hands");
            Assert.AreEqual(1, rack.Count);
            Assert.AreEqual("Slots", rod.transform.parent.name, "parented under the rack's holder");
            Assert.IsFalse(rod.gameObject.activeSelf, "in the rack, out of the interact registry");
            Assert.IsTrue(rod != null, "never destroyed");
        }

        [Test]
        public void A_stowed_rod_comes_back_out_as_the_same_object()
        {
            CarriableTool rod = MakeTool(ToolDef.RodId, StowageClass.Rod);
            CarryHands hands = HandsHolding(rod);
            StowageRack rack = MakeRack(capacity: 4);
            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));

            Assert.AreEqual(CarryRefusal.None, rack.TryTake(hands, out CarriableTool taken));

            Assert.AreSame(rod, taken);
            Assert.IsTrue(rod.IsCarried, "back in her hands");
            Assert.IsTrue(rod.gameObject.activeSelf);
            Assert.AreEqual(0, rack.Count);
            Assert.AreSame(hands.transform, rod.transform.parent, "carried, not left under the rack");
        }

        // ---- refuse: nothing moves ------------------------------------------------------------------------

        [Test]
        public void A_shovel_is_refused_and_stays_in_hand()
        {
            CarriableTool shovel = MakeTool(ToolDef.ShovelId, StowageClass.General);
            CarryHands hands = HandsHolding(shovel);
            StowageRack rack = MakeRack(capacity: 4);

            Assert.AreEqual(StowRefusal.WrongContainer, rack.TryStow(hands, shovel));

            Assert.IsTrue(shovel.IsCarried, "still in her hand");
            Assert.AreSame(hands.transform, shovel.transform.parent);
            Assert.IsTrue(shovel.gameObject.activeSelf);
            Assert.AreEqual(0, rack.Count);
        }

        [Test]
        public void A_rack_not_flagged_for_rods_refuses_a_rod_and_it_stays_in_hand()
        {
            CarriableTool rod = MakeTool(ToolDef.RodId, StowageClass.Rod);
            CarryHands hands = HandsHolding(rod);
            StowageRack crate = MakeRack(capacity: 10, accepts: StowageRule.ExistingContainer);

            Assert.AreEqual(StowRefusal.WrongContainer, crate.TryStow(hands, rod));
            Assert.IsTrue(rod.IsCarried);
            Assert.AreEqual(0, crate.Count);
        }

        [Test]
        public void A_full_rack_refuses_and_the_rod_stays_in_hand()
        {
            StowageRack rack = MakeRack(capacity: 1);
            CarriableTool first = MakeTool(ToolDef.RodId, StowageClass.Rod);
            CarryHands hands = HandsHolding(first);
            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, first));

            CarriableTool second = MakeTool(ToolDef.RodId, StowageClass.Rod);
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(second));

            Assert.AreEqual(StowRefusal.Full, rack.TryStow(hands, second));
            Assert.IsTrue(second.IsCarried, "the second rod stays in her hand");
            Assert.IsTrue(second.gameObject.activeSelf);
            Assert.AreEqual(1, rack.Count);
        }

        [Test]
        public void A_refused_take_leaves_the_rod_in_the_rack()
        {
            StowageRack rack = MakeRack(capacity: 4);
            CarriableTool rod = MakeTool(ToolDef.RodId, StowageClass.Rod);
            CarryHands hands = HandsHolding(rod);
            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));

            // A shovel in hand: one working tool at a time, so the rod cannot be lifted.
            CarriableTool shovel = MakeTool(ToolDef.ShovelId, StowageClass.General);
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(shovel));

            Assert.AreNotEqual(CarryRefusal.None, rack.TryTake(hands, out CarriableTool taken));
            Assert.IsNull(taken);
            Assert.AreEqual(1, rack.Count, "still in the rack");
            Assert.IsFalse(rod.gameObject.activeSelf);
            Assert.AreEqual("Slots", rod.transform.parent.name, "back under the holder");
        }

        [Test]
        public void An_empty_rack_has_nothing_to_take()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            StowageRack rack = MakeRack(capacity: 4);
            Assert.AreNotEqual(CarryRefusal.None, rack.TryTake(hands, out CarriableTool taken));
            Assert.IsNull(taken);
        }

        // ---- the press ladder -------------------------------------------------------------------------------

        [Test]
        public void The_rack_outranks_put_down_only_while_a_rod_is_held()
        {
            StowageRack rack = MakeRack(capacity: 4);

            CarryHands hands = ToolInHand.Empty(_spawned);   // publishes GameServices.Hands
            Assert.AreEqual(InteractPriority.Fixture, rack.Priority, "empty-handed: a fixture");

            CarriableTool shovel = MakeTool(ToolDef.ShovelId, StowageClass.General);
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(shovel));
            Assert.AreEqual(InteractPriority.Fixture, rack.Priority, "a shovel is not the rack's to take");

            var held = MakeTool(ToolDef.RodId, StowageClass.Rod);
            CarryHands rodHands = HandsHolding(held);         // a second pair, now the published hands
            Assert.AreEqual(InteractPriority.ToolTarget, rack.Priority, "a rod in hand: put it in the rack");
            Assert.AreSame(held, rack.HeldStowable(rodHands));
        }

        // ---- shipped content -----------------------------------------------------------------------------------

        [Test]
        public void The_shipped_rod_is_a_Rod_and_the_shovel_is_not()
        {
            var rod = AssetDatabase.LoadAssetAtPath<ToolDef>(RodToolPath);
            var shovel = AssetDatabase.LoadAssetAtPath<ToolDef>(ShovelToolPath);
            Assert.IsNotNull(rod, RodToolPath);
            Assert.IsNotNull(shovel, ShovelToolPath);
            Assert.AreEqual(StowageClass.Rod, rod.Stowage, "the rod only goes in a rack or a rod locker");
            Assert.AreEqual(StowageClass.General, shovel.Stowage);
        }

        [Test]
        public void The_shipped_rod_rack_takes_rods_only_and_has_room()
        {
            var rack = AssetDatabase.LoadAssetAtPath<StowageDef>(RackDefPath);
            Assert.IsNotNull(rack, RackDefPath);
            Assert.AreEqual("stowage.rod_rack", rack.Id);
            Assert.AreEqual(StowageAccepts.Rods, rack.Accepts, "rods and nothing else");
            Assert.Greater(rack.Capacity, 0, "a rack that holds no rods is not a rack");
            Assert.IsFalse(string.IsNullOrWhiteSpace(rack.DisplayName), "a refusal has to name it");
            Assert.AreEqual("gear.rod_rack", rack.GearId, "linked to its shop offer");
        }

        [Test]
        public void The_rod_rack_is_bought_through_the_gear_shop_seam()
        {
            var rack = AssetDatabase.LoadAssetAtPath<StowageDef>(RackDefPath);
            var offer = AssetDatabase.LoadAssetAtPath<GearOffer>(RackOfferPath);
            Assert.IsNotNull(offer, RackOfferPath);
            Assert.AreEqual(rack.GearId, offer.Id, "the offer and the Def name the same gear id");
            Assert.Greater(offer.Price, 0);

            var go = new GameObject("Counter");
            _spawned.Add(go);
            var shop = go.AddComponent<GearShop>();

            var shortWallet = new FakeWallet(offer.Price - 1);
            var shortSave = new SaveData();
            Assert.IsFalse(shop.TryBuy(offer, shortWallet, shortSave), "one coin short");
            Assert.AreEqual(offer.Price - 1, shortWallet.Money, "a refused sale takes nothing");

            var wallet = new FakeWallet(offer.Price);
            var save = new SaveData();
            Assert.IsTrue(shop.TryBuy(offer, wallet, save), "the Def's price buys it");
            Assert.AreEqual(0, wallet.Money);
            CollectionAssert.Contains(save.OwnedGear, "gear.rod_rack", "ownership recorded by stable id");
        }
    }
}
