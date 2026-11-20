using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Economy
{
    public class AutopilotFittingTests
    {
        const string Offered = "boat.offered";
        const string Other = "boat.other";
        const int Price = 60;

        static IAutopilotEligibility Eligibility() => new AutopilotEligibility(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, bool>(Offered, true),
            new System.Collections.Generic.KeyValuePair<string, bool>(Other, true),
            new System.Collections.Generic.KeyValuePair<string, bool>("boat.refused", false),
        });

        sealed class Wallet : IWallet
        {
            public int Money { get; private set; } = Price * 3;
            public int SpendCalls { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                SpendCalls++;
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        InMemoryAutopilotFitments _state;
        IAutopilotFittingService _service;
        Wallet _wallet;

        [SetUp]
        public void StartWithAnUnfittedFleet()
        {
            _state = new InMemoryAutopilotFitments();
            _service = new AutopilotFittingService(Eligibility(), _state);
            _wallet = new Wallet();
        }

        [Test]
        public void FittingTwice_ChargesOnce()
        {
            Assert.IsTrue(_service.TryFit(Offered, Price, _wallet));
            Assert.IsTrue(_service.TryFit(Offered, Price, _wallet));
            Assert.AreEqual(Price * 2, _wallet.Money);
            Assert.AreEqual(1, _wallet.SpendCalls);
            Assert.IsTrue(_service.IsFitted(Offered));
            Assert.IsTrue(_state.IsPurchased(Offered));
        }

        [Test]
        public void Removing_RestoresUnfittedState_AndRefittingRetainsThePaidUnit()
        {
            Assert.IsTrue(_service.TryFit(Offered, Price, _wallet));
            Assert.IsTrue(_service.Remove(Offered));
            Assert.IsFalse(_service.IsFitted(Offered));
            Assert.IsTrue(_service.CanFit(Offered));
            Assert.IsTrue(_state.IsPurchased(Offered));
            Assert.IsFalse(_service.Remove(Offered));
            Assert.AreEqual(Price * 2, _wallet.Money, "removal is no refund");
            Assert.IsTrue(_service.TryFit(Offered, Price + 10, null), "a later price does not rebill an owned unit");
            Assert.IsTrue(_service.IsFitted(Offered));
            Assert.AreEqual(1, _wallet.SpendCalls);
        }

        [Test]
        public void ARefusedHull_ChargesNothing_AndCreatesNoPurchase()
        {
            Assert.IsFalse(_service.CanFit("boat.refused"));
            Assert.IsFalse(_service.TryFit("boat.refused", Price, _wallet));
            Assert.AreEqual(Price * 3, _wallet.Money);
            Assert.AreEqual(0, _wallet.SpendCalls);
            Assert.IsFalse(_service.IsFitted("boat.refused"));
            Assert.IsFalse(_state.IsPurchased("boat.refused"));
        }

        [Test]
        public void TwoHulls_DoNotSharePurchasesOrFittedState()
        {
            Assert.IsTrue(_service.TryFit(Offered, Price, _wallet));
            Assert.IsFalse(_service.IsFitted(Other));
            Assert.IsFalse(_state.IsPurchased(Other));
            Assert.IsTrue(_service.TryFit(Other, Price, _wallet));
            Assert.AreEqual(2, _wallet.SpendCalls);
            Assert.IsTrue(_service.Remove(Offered));
            Assert.IsTrue(_service.IsFitted(Other));
            Assert.IsFalse(_service.IsFitted(Offered));
        }

        [Test]
        public void TwoOwnedBoatsOfOneHull_KeepSeparateState_ButUseTheSameEligibility()
        {
            string second = OwnedBoatId.Mint(Offered, new[] { Offered });
            Assert.IsTrue(_service.CanFit(second));
            Assert.IsTrue(_service.TryFit(Offered, Price, _wallet));
            Assert.IsFalse(_state.IsPurchased(second));
            Assert.IsTrue(_service.TryFit(second, Price, _wallet));
            Assert.AreEqual(2, _wallet.SpendCalls);
            Assert.IsTrue(_service.Remove(second));
            Assert.IsTrue(_service.IsFitted(Offered));
            Assert.IsFalse(_service.IsFitted(second));
        }

        [Test]
        public void InsufficientFunds_LeavesStateAndMoneyUnchanged()
        {
            Assert.IsFalse(_service.TryFit(Offered, _wallet.Money + 1, _wallet));
            Assert.AreEqual(Price * 3, _wallet.Money);
            Assert.IsFalse(_state.IsPurchased(Offered));
            Assert.IsFalse(_service.IsFitted(Offered));
        }

        [Test]
        public void AFirstPurchaseWithoutAWallet_IsRefused()
        {
            Assert.IsFalse(_service.TryFit(Offered, Price, null));
            Assert.IsFalse(_state.IsPurchased(Offered));
            Assert.IsFalse(_service.IsFitted(Offered));
        }

        [Test]
        public void ANegativePrice_ChargesNothing_AndCannotCreateOwnership()
        {
            Assert.IsFalse(_service.TryFit(Offered, -1, _wallet));
            Assert.AreEqual(0, _wallet.SpendCalls);
            Assert.IsFalse(_state.IsPurchased(Offered));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("boat.offered/invalid")]
        public void AnInvalidBoatId_RefusesAllOperations_WithoutCharging(string id)
        {
            Assert.IsFalse(_service.CanFit(id));
            Assert.IsFalse(_service.TryFit(id, Price, _wallet));
            Assert.IsFalse(_service.IsFitted(id));
            Assert.IsFalse(_service.Remove(id));
            Assert.AreEqual(0, _wallet.SpendCalls);
        }

        [Test]
        public void RemovingAnUnpurchasedUnit_IsANoOp()
        {
            Assert.IsFalse(_service.Remove(Offered));
            Assert.IsFalse(_state.IsPurchased(Offered));
        }

        [Test]
        public void ANewServiceUsingTheSameStore_SeesThePurchase_WithoutChargingAgain()
        {
            Assert.IsTrue(_service.TryFit(Offered, Price, _wallet));
            var otherService = new AutopilotFittingService(Eligibility(), _state);
            Assert.IsTrue(otherService.IsFitted(Offered));
            Assert.IsTrue(otherService.TryFit(Offered, Price, _wallet));
            Assert.AreEqual(1, _wallet.SpendCalls);
        }

        [Test]
        public void SeparateStores_DoNotLeakStateBetweenSessions()
        {
            Assert.IsTrue(_service.TryFit(Offered, Price, _wallet));
            var otherSession = new AutopilotFittingService(Eligibility(), new InMemoryAutopilotFitments());
            Assert.IsFalse(otherSession.IsFitted(Offered));
            Assert.IsTrue(otherSession.TryFit(Offered, Price, _wallet));
            Assert.AreEqual(2, _wallet.SpendCalls);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Eligibility_UsesOnlyTheSuppliedAuthoredFlag(bool offered)
        {
            var flags = new[] { new System.Collections.Generic.KeyValuePair<string, bool>(Offered, offered) };
            Assert.AreEqual(offered, new AutopilotEligibility(flags).CanTakeAutopilot(Offered));
        }

        [Test]
        public void Eligibility_RefusesUnknownMissingAndAmbiguousHullIds()
        {
            var flag = new System.Collections.Generic.KeyValuePair<string, bool>(Offered, true);
            var ambiguous = new AutopilotEligibility(new[] { flag, flag, flag });
            Assert.IsFalse(ambiguous.CanTakeAutopilot(Offered));
            Assert.IsFalse(ambiguous.CanTakeAutopilot(null));
            Assert.IsFalse(Eligibility().CanTakeAutopilot("boat.unknown"));
            Assert.IsFalse(new AutopilotEligibility(null).CanTakeAutopilot(Offered));
        }
    }
}
