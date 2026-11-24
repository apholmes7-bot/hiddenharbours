using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Walk up to a good on the shelf and buy it</b> (buying at the shelf, slice 1), joined up in a running
    /// scene: a real <see cref="ShelfGood"/> component registers itself as an interact candidate, the player
    /// walks toward it a step a frame, the one resolver starts offering it once he is in reach with its name and
    /// price as the label, and a press through the candidate buys exactly one through the store's Core till and
    /// says so in his bubble. The till is a plain double: the money, the shelf count and the book are the
    /// Economy's and pinned in EditMode (<c>ShopAtTheShelfTests</c>); what only PlayMode shows is the wiring.
    /// Headless-safe: nothing renders.
    /// </summary>
    public class ShopAtTheShelfPlayTests
    {
        private const string ShopId = "shop.test_store";
        private const string Listing = "supply.bread";

        private sealed class Till : IShelfTill
        {
            public int OnShelf = 2;
            public int Price = 4;
            public int Money = 10;
            public int Buys;
            public string ShopId => ShopAtTheShelfPlayTests.ShopId;

            public bool TryQuote(string listingId, double totalSeconds, out ShelfQuote quote)
            {
                quote = new ShelfQuote(listingId, "Loaf of Bread", Price, OnShelf);
                return listingId == Listing;
            }

            public ShelfBuyResult TryBuyOne(string listingId, double totalSeconds)
            {
                if (listingId != Listing) return ShelfBuyResult.NotSold;
                if (OnShelf <= 0) return ShelfBuyResult.SoldOut;
                if (Money < Price) return ShelfBuyResult.TooDear;
                Money -= Price; OnShelf--; Buys++;
                return ShelfBuyResult.Bought;
            }
        }

        private readonly List<string> _cues = new();
        private System.Action<InnerVoiceCue> _onCue;
        private GameObject _go;
        private Till _till;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            _cues.Clear();
            _onCue = c => _cues.Add(c.CueId);
            EventBus.Subscribe(_onCue);
            _till = new Till();
            ShelfTills.Register(_till);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onCue);
            if (_go != null) Object.Destroy(_go);
            ShelfTills.Clear();
            Interactables.Clear();
        }

        private static string Offered(Vector2 at)
        {
            var actor = new InteractActor(at, Vector2.zero, InteractContext.OnFoot);
            return InteractResolver.TryResolveNow(actor, 120f, out IInteractable best) ? best.VerbLabel : null;
        }

        [UnityTest]
        public IEnumerator HeWalksUpToTheBread_ThePriceAppears_AndAPressBuysOne()
        {
            _go = new GameObject("Bread");
            _go.SetActive(false);
            _go.transform.position = new Vector3(5f, 2f, 0f);
            ShelfGood good = _go.AddComponent<ShelfGood>();
            good.Configure("shelfslots.test.slot.bread", ShopId, Listing, 1.5f, false, () => true);
            _go.SetActive(true);
            good.Refresh(0.0);
            yield return null;

            Vector2 at = new Vector2(0f, 2f);
            Assert.IsNull(Offered(at), "nothing is offered from across the room");
            string offered = null;
            for (int frame = 0; frame < 120 && offered == null; frame++)
            {
                at = Vector2.MoveTowards(at, _go.transform.position, 0.1f);
                yield return null;
                offered = Offered(at);
            }
            Assert.That(offered, Is.EqualTo("Buy Loaf of Bread — " + MoneyFormat.Balance(4)), "the name and price appear in reach");
            Assert.That(Vector2.Distance(at, _go.transform.position), Is.LessThanOrEqualTo(1.5f + 1e-4f));

            var actor = new InteractActor(at, Vector2.zero, InteractContext.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolveNow(actor, 120f, out IInteractable best));
            best.Interact(actor);
            yield return null;

            Assert.That(_till.Buys, Is.EqualTo(1), "one press buys one");
            Assert.That(_till.Money, Is.EqualTo(6));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBought }));

            best.Interact(actor);
            yield return null;
            Assert.That(_till.OnShelf, Is.EqualTo(0));
            Assert.That(Offered(at), Is.EqualTo("Loaf of Bread — sold out"), "the empty shelf says so before a press");
            best.Interact(actor);
            Assert.That(_till.Buys, Is.EqualTo(2), "an empty shelf sells nothing");
            Assert.That(_cues[_cues.Count - 1], Is.EqualTo(InnerVoiceCues.ShelfSoldOut));
        }

        [UnityTest]
        public IEnumerator AShortPurseIsRefused_AndNothingMoves()
        {
            _till.Money = 3;
            _go = new GameObject("Bread");
            _go.SetActive(false);
            ShelfGood good = _go.AddComponent<ShelfGood>();
            good.Configure("shelfslots.test.slot.bread", ShopId, Listing, 1.5f, false, () => true);
            _go.SetActive(true);
            good.Refresh(0.0);
            yield return null;

            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolveNow(actor, 120f, out IInteractable best));
            best.Interact(actor);
            Assert.That(_till.Buys, Is.EqualTo(0));
            Assert.That(_till.Money, Is.EqualTo(3));
            Assert.That(_till.OnShelf, Is.EqualTo(2));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfTooDear }));
        }
    }
}
