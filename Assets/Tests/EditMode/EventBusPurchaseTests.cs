using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.EditMode
{
    public class EventBusPurchaseTests
    {
        private readonly List<Object> _objects = new();
        private sealed class Wallet : IWallet
        {
            public int Money { get; private set; } = 2000;
            public int SpendCalls;
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                SpendCalls++;
                if (Money < amount) return false;
                Money -= amount;
                return true;
            }
        }

        [SetUp]
        public void SetUp() { GameServices.Reset(); EventBus.Clear<BoatPurchased>(); }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            EventBus.Clear<BoatPurchased>(); GameServices.Reset();
        }

        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

        [Test]
        public void Purchase_WhenSaveHandlerThrows_StillReachesFleetAndSuccessContinuation()
        {
            var dory = Keep(ScriptableObject.CreateInstance<BoatHullDef>());
            dory.Id = "boat.dory"; dory.HoldUnits = 6;
            var punt = Keep(ScriptableObject.CreateInstance<BoatHullDef>());
            punt.Id = "boat.punt"; punt.HoldUnits = 14;
            var go = Keep(new GameObject("Fleet"));
            var boat = go.AddComponent<BoatController>();
            var hold = go.AddComponent<ShipHold>();
            var renderer = go.AddComponent<SpriteRenderer>();
            var fleet = go.AddComponent<OwnedFleet>();
            boat.SetHull(dory); hold.SetHull(dory);
            fleet.Configure(new[] { dory, punt }, boat, hold, renderer);
            EventBus.Clear<BoatPurchased>();
            EventBus.Subscribe<BoatPurchased>(_ => throw new IOException("purchase-save"));
            EventBus.Subscribe<BoatPurchased>(fleet.OnBoatPurchased);
            var offer = Keep(ScriptableObject.CreateInstance<ShipwrightOffer>());
            offer.BoatId = "boat.punt"; offer.Price = 1800;
            var shop = Keep(new GameObject("Shipwright")).AddComponent<Shipwright>();
            var wallet = new Wallet();

            LogAssert.Expect(LogType.Exception, new Regex("^IOException: purchase-save(?:\\r?\\n|$)"));
            bool returned = shop.TryBuy(offer, wallet);

            Assert.That(returned, Is.True);
            Assert.That(shop.LastPurchaseSucceeded, Is.True, "the real post-publish success continuation ran");
            Assert.That(wallet.Money, Is.EqualTo(200));
            Assert.That(wallet.SpendCalls, Is.EqualTo(1));
            Assert.That(boat.Hull, Is.SameAs(punt));
            Assert.That(hold.CapacityUnits, Is.EqualTo(14));
        }
    }
}
