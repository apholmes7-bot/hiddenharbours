using System;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public partial class InteractVerbTests
    {
        private const string GuestKey = "player.guest";

        [Test]
        public void ActorIdentity_DefaultAndExistingConstructionNameTheSoloPlayer()
        {
            Assert.That(default(InteractActor).PlayerKey, Is.EqualTo(InteractActor.LocalPlayerKey));
            Assert.That(new InteractActor(Vector2.one, Vector2.right, InteractContext.OnFoot).PlayerKey,
                Is.EqualTo(InteractActor.LocalPlayerKey));
            Assert.That(InteractActor.For(Vector2.one, Vector2.up, ControlMode.OnDeck).PlayerKey,
                Is.EqualTo(InteractActor.LocalPlayerKey));
            Assert.That(new InteractActor(Vector2.zero, Vector2.zero, InteractContext.None),
                Is.EqualTo(default(InteractActor)), "the existing zero-value actor keeps value equality");
            Assert.That(new InteractActor(Vector2.zero, Vector2.zero, InteractContext.None,
                InteractActor.LocalPlayerKey), Is.EqualTo(default(InteractActor)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        public void ActorIdentity_ExplicitMissingKeyCannotBecomeTheSoloPlayer(string key)
        {
            Assert.Throws<ArgumentException>(() =>
                new InteractActor(Vector2.zero, Vector2.up, InteractContext.OnFoot, key));
            Assert.Throws<ArgumentException>(() =>
                InteractActor.For(Vector2.zero, Vector2.up, ControlMode.OnFoot, key));
            var lookup = new ActorServicesByPlayer(new ActorPurse());
            Assert.Throws<ArgumentException>(() => lookup.Bind(key, new ActorHands(), new ActorBag()));
        }

        [Test]
        public void ActorIdentity_MovementAndControlModeDoNotChangeWhoPerformsTheAction()
        {
            var target = At("fixture", 0, 0);
            foreach (ControlMode mode in new[]
                { ControlMode.OnFoot, ControlMode.OnDeck, ControlMode.Aboard, ControlMode.Driving })
            {
                var position = new Vector2((int)mode, 2f);
                var actor = InteractActor.For(position, Vector2.left, mode, GuestKey);
                InteractVerb.Perform(target, -1, actor);
                Assert.That(target.LastActor.PlayerKey, Is.EqualTo(GuestKey));
                Assert.That(target.LastActor.Position, Is.EqualTo(position));
                Assert.That(target.LastActor.Facing, Is.EqualTo(Vector2.left));
                Assert.That(target.LastActor.Context, Is.EqualTo(InteractActor.ContextFor(mode)));
            }
            Assert.That(target.Calls, Is.EqualTo(4));
            Assert.That(_performed.Count, Is.EqualTo(4));
        }

        [Test]
        public void ActorServices_SoloReadsTodaysGlobalsOnEveryLookup()
        {
            ICarrier previousHands = GameServices.Hands;
            IShoppingBagHands previousBag = GameServices.BagHands;
            IWallet previousPurse = GameServices.Wallet;
            try
            {
                IActorServices lookup = new SinglePlayerActorServices();
                var firstHands = new ActorHands();
                var firstBag = new ActorBag();
                var firstPurse = new ActorPurse();
                GameServices.Hands = firstHands;
                GameServices.BagHands = firstBag;
                GameServices.Wallet = firstPurse;
                Assert.That(lookup.TryResolve(default, out ActorServices first), Is.True);
                AssertServices(first, firstHands, firstBag, firstPurse);

                var nextHands = new ActorHands();
                var nextBag = new ActorBag();
                var nextPurse = new ActorPurse();
                GameServices.Hands = nextHands;
                GameServices.BagHands = nextBag;
                GameServices.Wallet = nextPurse;
                var actor = InteractActor.For(Vector2.one, Vector2.up, ControlMode.OnFoot);
                Assert.That(lookup.TryResolve(actor, out ActorServices next), Is.True);
                AssertServices(next, nextHands, nextBag, nextPurse);

                GameServices.Hands = null;
                GameServices.BagHands = null;
                GameServices.Wallet = null;
                Assert.That(lookup.TryResolve(default, out ActorServices empty), Is.True);
                AssertServices(empty, null, null, null);
            }
            finally
            {
                GameServices.Hands = previousHands;
                GameServices.BagHands = previousBag;
                GameServices.Wallet = previousPurse;
            }
        }

        [Test]
        public void ActorServices_SoloRefusesAnExplicitGuest()
        {
            IActorServices lookup = new SinglePlayerActorServices();
            Assert.That(lookup.TryResolve(ActorFor(GuestKey), out ActorServices services), Is.False);
            AssertServices(services, null, null, null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActorServices_TwoActorsKeepTheirOwnHandsAndBagWithOnePurse(bool guestFirst)
        {
            var purse = new ActorPurse();
            purse.Add(60);
            var lookup = new ActorServicesByPlayer(purse);
            var hostHands = new ActorHands();
            var guestHands = new ActorHands();
            var hostBag = new ActorBag();
            var guestBag = new ActorBag();
            if (guestFirst) lookup.Bind(GuestKey, guestHands, guestBag);
            lookup.Bind(InteractActor.LocalPlayerKey, hostHands, hostBag);
            if (!guestFirst) lookup.Bind(GuestKey, guestHands, guestBag);

            Assert.That(lookup.TryResolve(default, out ActorServices host), Is.True);
            Assert.That(lookup.TryResolve(ActorFor(GuestKey), out ActorServices guest), Is.True);
            AssertServices(host, hostHands, hostBag, purse);
            AssertServices(guest, guestHands, guestBag, purse);
            Assert.That(host.Hands, Is.Not.SameAs(guest.Hands));
            Assert.That(host.BagHands, Is.Not.SameAs(guest.BagHands));

            Assert.That(guest.Purse.TrySpend(15), Is.True);
            Assert.That(guest.BagHands.TryBag(null, "supply.test_bread"), Is.EqualTo(ShoppingBagRefusal.None));
            Assert.That(hostBag.Goods, Is.EqualTo(0));
            Assert.That(guestBag.Goods, Is.EqualTo(1));
            Assert.That(host.Purse.Money, Is.EqualTo(45));
            Assert.That(guest.Purse.Money, Is.EqualTo(45));
        }

        [Test]
        public void ActorServices_UnknownKeysNeverUseAnotherPlayerOrTheSoloFallback()
        {
            var lookup = new ActorServicesByPlayer(new ActorPurse());
            lookup.Bind(InteractActor.LocalPlayerKey, new ActorHands(), new ActorBag());
            Assert.That(lookup.TryResolve(ActorFor(GuestKey), out ActorServices unknown), Is.False);
            AssertServices(unknown, null, null, null);
            lookup.Clear();
            Assert.That(lookup.TryResolve(default, out ActorServices empty), Is.False);
            AssertServices(empty, null, null, null);
        }

        [Test]
        public void ActorServices_KeysUseOrdinalIdentityAndIgnoreLocation()
        {
            var purse = new ActorPurse();
            var lookup = new ActorServicesByPlayer(purse);
            var lower = new ActorHands();
            var upper = new ActorHands();
            lookup.Bind(GuestKey, lower, null);
            lookup.Bind("PLAYER.GUEST", upper, null);
            var moved = InteractActor.For(new Vector2(80, 10), Vector2.down, ControlMode.OnDeck, GuestKey);
            Assert.That(lookup.TryResolve(moved, out ActorServices lowerResult), Is.True);
            Assert.That(lookup.TryResolve(ActorFor("PLAYER.GUEST"), out ActorServices upperResult), Is.True);
            AssertServices(lowerResult, lower, null, purse);
            AssertServices(upperResult, upper, null, purse);
        }

        [Test]
        public void ActorServices_RebindAndDisconnectDoNotChangeTheOtherPlayersServicesOrHeldState()
        {
            var purse = new ActorPurse();
            var lookup = new ActorServicesByPlayer(purse);
            var hostHands = new ActorHands();
            var hostBag = new ActorBag();
            var guestBag = new ActorBag();
            guestBag.TryBag(null, "supply.test_bread");
            lookup.Bind(InteractActor.LocalPlayerKey, hostHands, hostBag);
            lookup.Bind(GuestKey, new ActorHands(), guestBag);
            var replacement = new ActorHands();
            lookup.Bind(GuestKey, replacement, guestBag);
            Assert.That(lookup.TryResolve(ActorFor(GuestKey), out ActorServices rebound), Is.True);
            AssertServices(rebound, replacement, guestBag, purse);
            Assert.That(lookup.Unbind(GuestKey), Is.True);
            Assert.That(lookup.Unbind(GuestKey), Is.False);
            Assert.That(lookup.TryResolve(ActorFor(GuestKey), out ActorServices absent), Is.False);
            AssertServices(absent, null, null, null);
            Assert.That(lookup.TryResolve(default, out ActorServices host), Is.True);
            AssertServices(host, hostHands, hostBag, purse);
            Assert.That(guestBag.Goods, Is.EqualTo(1));
            lookup.Bind(GuestKey, replacement, guestBag);
            Assert.That(lookup.TryResolve(ActorFor(GuestKey), out ActorServices returned), Is.True);
            AssertServices(returned, replacement, guestBag, purse);
            Assert.That(guestBag.Goods, Is.EqualTo(1));
        }

        [Test]
        public void ActorServices_SeparateSessionsDoNotShareBindings()
        {
            var purseA = new ActorPurse();
            var purseB = new ActorPurse();
            var sessionA = new ActorServicesByPlayer(purseA);
            var sessionB = new ActorServicesByPlayer(purseB);
            var handsA = new ActorHands();
            var handsB = new ActorHands();
            sessionA.Bind(GuestKey, handsA, null);
            sessionB.Bind(GuestKey, handsB, null);
            Assert.That(sessionA.TryResolve(ActorFor(GuestKey), out ActorServices a), Is.True);
            Assert.That(sessionB.TryResolve(ActorFor(GuestKey), out ActorServices b), Is.True);
            AssertServices(a, handsA, null, purseA);
            AssertServices(b, handsB, null, purseB);
            sessionA.Clear();
            Assert.That(sessionA.TryResolve(ActorFor(GuestKey), out _), Is.False);
            Assert.That(sessionB.TryResolve(ActorFor(GuestKey), out ActorServices stillB), Is.True);
            AssertServices(stillB, handsB, null, purseB);
        }

        [Test]
        public void ActorServices_SessionRequiresASharedPurse()
        {
            Assert.Throws<ArgumentNullException>(() => new ActorServicesByPlayer(null));
        }

        private static InteractActor ActorFor(string key)
            => InteractActor.For(Vector2.zero, Vector2.right, ControlMode.OnFoot, key);

        private static void AssertServices(ActorServices services, ICarrier hands,
            IShoppingBagHands bag, IWallet purse)
        {
            Assert.That(services.Hands, Is.SameAs(hands));
            Assert.That(services.BagHands, Is.SameAs(bag));
            Assert.That(services.Purse, Is.SameAs(purse));
        }

        private sealed class ActorHands : ICarrier
        {
            public ICarriable Carried => null;
            public bool IsCarrying => false;
        }

        private sealed class ActorBag : IShoppingBagHands
        {
            public int Goods { get; private set; }
            public bool HoldsBag => Goods != 0;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => ShoppingBagRefusal.None;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId)
            {
                Goods++;
                return ShoppingBagRefusal.None;
            }
            public bool TryPutAway(string storageId) => false;
        }

        private sealed class ActorPurse : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (Money < amount) return false;
                Money -= amount;
                return true;
            }
        }
    }
}
