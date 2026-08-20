using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// <b>Fuel retail — what a pump charges, and what it pours.</b> Everything about the transaction that
    /// can be proved without a frame.
    ///
    ///   • THE ROUNDING — up, to whole money, ONCE on the final figure. Pinned against the two ways it
    ///     goes wrong: rounding to nearest (which hands out free fuel) and rounding per litre (which
    ///     charges 60 for a can that costs 41).
    ///   • THE REFUSALS — every one of them, and the ORDER they resolve in. Grade before fullness is a
    ///     deliberate choice about which sentence is useful, not an accident of the if-chain.
    ///   • THE MONEY-LIMITED FILL — short of coin the pump pours what you can pay for rather than refusing,
    ///     and it never charges a coin more than you have. That last one is the arithmetic trap: the
    ///     litres are derived FROM the money, so re-rounding them overcharges by one.
    ///   • THE PUMP SEAM — a refusal charges nothing, a fill charges exactly the quote, and the purchase
    ///     is announced on the bus.
    ///   • THE RUNG FLIP — a pump is a fixture until you are holding a can, and a ToolTarget once you are.
    ///     Without that, a held can outranks the pump forever and filling it is IMPOSSIBLE.
    ///
    /// <para>What the SHIPPED station assets say is <c>FuelStationContentValidationTests</c>'s; this suite
    /// builds its own Defs so a re-price by the owner can never turn a rule into a failure.</para>
    /// </summary>
    public class FuelPricingTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        // ---- fakes -------------------------------------------------------------------------------

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        /// <summary>A can, as the pump sees it — the same clamping contract <c>FuelLevelPresenter</c>
        /// implements, without dragging the art module into an economy test.</summary>
        private sealed class FakeCan : IFuelVessel
        {
            public string Grade { get; set; } = FuelGrades.Gas;
            public float CapacityLitres { get; set; } = 20f;
            public float Litres { get; private set; }

            public FakeCan() { }
            public FakeCan(string grade, float capacity, float litres)
            {
                Grade = grade; CapacityLitres = capacity; Litres = litres;
            }

            public void Deliver(float litres)
            {
                if (litres <= 0f) return;
                Litres = Mathf.Min(CapacityLitres, Litres + litres);
            }

            /// <summary>The POUR half of the seam. Nothing in THIS suite draws — a pump only ever fills —
            /// but the interface now carries both halves, so a can that could not be emptied would be a
            /// fiction. Same measured-answer contract the real can implements.</summary>
            public float Draw(float litres)
            {
                if (litres <= 0f) return 0f;
                float given = Mathf.Min(Litres, litres);
                Litres -= given;
                return given;
            }
        }

        /// <summary>A can as a real scene object: the pump finds a vessel by <c>GetComponent</c> off what
        /// the hands report, so the rung test needs a component, not just an interface.</summary>
        private sealed class FakeCanBehaviour : MonoBehaviour, IFuelVessel
        {
            public string Grade { get; set; } = FuelGrades.Gas;
            public float CapacityLitres { get; set; } = 20f;
            public float Litres { get; private set; }
            public void Deliver(float litres) { if (litres > 0f) Litres = Mathf.Min(CapacityLitres, Litres + litres); }

            public float Draw(float litres)
            {
                if (litres <= 0f) return 0f;
                float given = Mathf.Min(Litres, litres);
                Litres -= given;
                return given;
            }
        }

        /// <summary>Hands holding something. <see cref="ICarriable"/> is mostly art-facing; the pump reads
        /// only <see cref="ICarriable.Transform"/>, so the rest is answered honestly and ignored.</summary>
        private sealed class FakeHands : ICarrier, ICarriable
        {
            public FakeHands(Transform held) { Transform = held; }
            public ICarriable Carried => Transform != null ? this : null;
            public bool IsCarrying => Carried != null;

            public string DefId => "fuelstore.gas_jerry_s20";
            public bool IsCarriable => true;
            public Transform Transform { get; }
            public int BakedFacings => 8;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int sortingLayerId, int sortingOrder) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        // ---- fixtures ----------------------------------------------------------------------------

        private static FuelGradeOffer Offer(string grade, bool available, float perLitre)
            => new FuelGradeOffer { Grade = grade, Available = available, PricePerLitre = perLitre };

        private FuelStationDef Station(params FuelGradeOffer[] grades)
        {
            var def = ScriptableObject.CreateInstance<FuelStationDef>();
            def.Id = "station.test";
            def.DisplayName = "Test Pumps";
            def.Grades = grades;
            _spawned.Add(def);
            return def;
        }

        private FuelPump Pump(FuelStationDef station, string grade, IWallet wallet)
        {
            var go = new GameObject("pump");
            _spawned.Add(go);
            var pump = go.AddComponent<FuelPump>();
            pump.Configure(station, grade, "fixture.test.pump_" + grade);
            pump.SetWallet(wallet);
            return pump;
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Hands = null;
            EventBus.Clear<FuelPurchased>();
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- the rounding ------------------------------------------------------------------------

        [Test]
        public void FullFill_ChargesCeilingOfLitresTimesPrice()
        {
            // 8 L of room at 2.05 = 16.40 -> 17.
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Gas, 20f, 12f, money: 1000);

            Assert.AreEqual(FuelRefusal.None, q.Refusal);
            Assert.AreEqual(8f, q.Litres, 1e-4f, "it fills the room it has");
            Assert.AreEqual(17, q.Price, "16.40 rounds UP to 17 - a fill is never free");
            Assert.IsFalse(q.LimitedByMoney);
        }

        [Test]
        public void Rounding_IsUp_NotNearest()
        {
            // 1 L at 2.10 = 2.10. Rounded to nearest that is 2; rounded up it is 3.
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.10f), FuelGrades.Gas, 20f, 19f, money: 1000);

            Assert.AreEqual(1f, q.Litres, 1e-4f);
            Assert.AreEqual(3, q.Price, "2.10 rounds UP to 3; rounding to nearest would hand out a free tenth");
        }

        [Test]
        public void Rounding_HappensOnce_NotPerLitre()
        {
            // The trap: ceil the UNIT price and multiply, and a 20 L can at 2.05 costs 60 instead of 41.
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Gas, 20f, 0f, money: 1000);

            Assert.AreEqual(20f, q.Litres, 1e-4f);
            Assert.AreEqual(41, q.Price, "one rounding, on the final figure - not twenty roundings of 2.05");
        }

        // ---- the refusals ------------------------------------------------------------------------

        [Test]
        public void GradeNotSoldHere_IsRefused()
        {
            var q = FuelPricing.Quote(Offer(FuelGrades.Diesel, false, 1.85f), FuelGrades.Diesel, 20f, 0f, money: 1000);

            Assert.AreEqual(FuelRefusal.GradeNotSold, q.Refusal);
            Assert.AreEqual(0, q.Price);
            Assert.IsFalse(q.CanBuy);
        }

        [Test]
        public void WrongGradeCan_IsRefused()
        {
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Diesel, 20f, 0f, money: 1000);

            Assert.AreEqual(FuelRefusal.GradeMismatch, q.Refusal);
            Assert.AreEqual(0f, q.Litres);
        }

        [Test]
        public void WrongGrade_OutranksFullness_SoTheUsefulSentenceIsSaid()
        {
            // A brim-full diesel can at the gas pump. Both faults apply; the one worth hearing is that it
            // is the wrong can, because that is the mistake the player is about to repeat.
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Diesel, 20f, 20f, money: 1000);

            Assert.AreEqual(FuelRefusal.GradeMismatch, q.Refusal);
        }

        [Test]
        public void FullCan_IsRefused()
        {
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Gas, 20f, 20f, money: 1000);

            Assert.AreEqual(FuelRefusal.VesselFull, q.Refusal);
        }

        [Test]
        public void AVesselThatHoldsNothing_IsNotAVessel()
        {
            // A nozzle / dispenser head: capacity 0. Every later branch would divide by it.
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Gas, 0f, 0f, money: 1000);

            Assert.AreEqual(FuelRefusal.NoVessel, q.Refusal);
        }

        [Test]
        public void NullVessel_IsRefusedNotThrown()
        {
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), (IFuelVessel)null, money: 1000);

            Assert.AreEqual(FuelRefusal.NoVessel, q.Refusal);
        }

        [Test]
        public void NoMoneyAtAll_IsRefused()
        {
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Gas, 20f, 0f, money: 0);

            Assert.AreEqual(FuelRefusal.CannotAfford, q.Refusal);
        }

        // ---- the money-limited fill --------------------------------------------------------------

        [Test]
        public void ShortOfMoney_ThePumpPoursWhatYouCanPayFor()
        {
            // A full 20 L at 2.05 is 41; the purse holds 20.
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2.05f), FuelGrades.Gas, 20f, 0f, money: 20);

            Assert.AreEqual(FuelRefusal.None, q.Refusal, "a pump that gives nothing can strand the player");
            Assert.IsTrue(q.LimitedByMoney, "and it must SAY the purse decided, or a half-full can reads as a bug");
            Assert.AreEqual(20f / 2.05f, q.Litres, 1e-4f);
        }

        [Test]
        public void AMoneyLimitedFill_NeverChargesMoreThanYouHave()
        {
            // The arithmetic trap: the litres are DERIVED from the money, so re-rounding them overcharges
            // by one. Swept across purses because it only bites on the values where the product lands
            // just above an integer.
            var offer = Offer(FuelGrades.Gas, true, 2.05f);
            for (int money = 1; money <= 40; money++)
            {
                var q = FuelPricing.Quote(offer, FuelGrades.Gas, 20f, 0f, money);
                Assert.LessOrEqual(q.Price, money, $"purse of {money} was charged {q.Price}");
            }
        }

        [Test]
        public void AFreePump_PoursWithNoMoneyAtAll()
        {
            // Legal, and it must not fall into the money-limited branch, where it would divide by zero.
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 0f), FuelGrades.Gas, 20f, 0f, money: 0);

            Assert.AreEqual(FuelRefusal.None, q.Refusal);
            Assert.IsTrue(q.CanBuy);
            Assert.AreEqual(20f, q.Litres, 1e-4f);
            Assert.AreEqual(0, q.Price);
        }

        // ---- asking for an amount ----------------------------------------------------------------

        [Test]
        public void AskingForAnAmount_CapsTheFill()
        {
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2f), FuelGrades.Gas, 20f, 0f, money: 1000, maxLitres: 5f);

            Assert.AreEqual(5f, q.Litres, 1e-4f);
            Assert.AreEqual(10, q.Price);
        }

        [Test]
        public void AskingForMoreThanItHolds_ClampsToTheRoom()
        {
            var q = FuelPricing.Quote(Offer(FuelGrades.Gas, true, 2f), FuelGrades.Gas, 20f, 15f, money: 1000, maxLitres: 40f);

            Assert.AreEqual(5f, q.Litres, 1e-4f, "you cannot buy more than the can will take");
        }

        // ---- determinism -------------------------------------------------------------------------

        [Test]
        public void TheSameQuestion_AlwaysGetsTheSameQuote()
        {
            var offer = Offer(FuelGrades.Gas, true, 2.05f);
            var a = FuelPricing.Quote(offer, FuelGrades.Gas, 20f, 3.5f, money: 17);
            var b = FuelPricing.Quote(offer, FuelGrades.Gas, 20f, 3.5f, money: 17);

            Assert.AreEqual(a.Refusal, b.Refusal);
            Assert.AreEqual(a.Price, b.Price);
            Assert.IsTrue(a.Litres.Equals(b.Litres), "bit-identical: pricing has no state and no randomness");
        }

        // ---- the station Def ---------------------------------------------------------------------

        [Test]
        public void AGradeWithNoRow_ReadsTheSameAsAGradeSwitchedOff()
        {
            var station = Station(Offer(FuelGrades.Gas, true, 2.05f), Offer(FuelGrades.Oil, false, 10.5f));

            Assert.IsTrue(station.Sells(FuelGrades.Gas));
            Assert.IsFalse(station.Sells(FuelGrades.Oil), "authored but switched off");
            Assert.IsFalse(station.Sells(FuelGrades.StoveOil), "never authored - one case, not two");
            Assert.AreEqual(0f, station.PricePerLitre(FuelGrades.Oil), "an unsold grade quotes nothing");
            Assert.AreEqual(1, station.StockedGradeCount);
        }

        // ---- the pump seam -----------------------------------------------------------------------

        [Test]
        public void AFill_ChargesExactlyTheQuoteAndPoursExactlyTheLitres()
        {
            var wallet = new FakeWallet(100);
            var pump = Pump(Station(Offer(FuelGrades.Gas, true, 2.05f)), FuelGrades.Gas, wallet);
            var can = new FakeCan(FuelGrades.Gas, 20f, 0f);

            Assert.IsTrue(pump.TryFill(can, wallet));
            Assert.AreEqual(100 - 41, wallet.Money);
            Assert.AreEqual(20f, can.Litres, 1e-4f);
            Assert.AreEqual(20f, pump.LastLitresDelivered, 1e-4f);
            Assert.IsTrue(pump.LastFillSucceeded);
        }

        [Test]
        public void ARefusal_ChargesNothing()
        {
            var wallet = new FakeWallet(100);
            var pump = Pump(Station(Offer(FuelGrades.Gas, true, 2.05f)), FuelGrades.Gas, wallet);
            var diesel = new FakeCan(FuelGrades.Diesel, 20f, 0f);

            Assert.IsFalse(pump.TryFill(diesel, wallet));
            Assert.AreEqual(100, wallet.Money, "a refusal must never eat the coin");
            Assert.AreEqual(0f, diesel.Litres);
            Assert.IsFalse(pump.LastFillSucceeded);
        }

        [Test]
        public void APumpWiredToNoStation_RefusesRatherThanPourFree()
        {
            var wallet = new FakeWallet(100);
            var pump = Pump(null, FuelGrades.Gas, wallet);
            var can = new FakeCan(FuelGrades.Gas, 20f, 0f);

            Assert.AreEqual(FuelRefusal.NoStation, pump.Quote(can, wallet.Money).Refusal);
            Assert.IsFalse(pump.TryFill(can, wallet));
            Assert.AreEqual(100, wallet.Money);
            Assert.AreEqual(0f, can.Litres);
        }

        [Test]
        public void AMoneyLimitedFill_SpendsThePurseAndPartlyFillsTheCan()
        {
            var wallet = new FakeWallet(20);
            var pump = Pump(Station(Offer(FuelGrades.Gas, true, 2.05f)), FuelGrades.Gas, wallet);
            var can = new FakeCan(FuelGrades.Gas, 20f, 0f);

            Assert.IsTrue(pump.TryFill(can, wallet));
            Assert.AreEqual(0, wallet.Money);
            Assert.AreEqual(20f / 2.05f, can.Litres, 1e-3f);
            Assert.Less(can.Litres, can.CapacityLitres);
        }

        [Test]
        public void APurchase_IsAnnouncedOnTheBus()
        {
            var seen = new List<FuelPurchased>();
            void OnBought(FuelPurchased p) => seen.Add(p);

            EventBus.Clear<FuelPurchased>();
            EventBus.Subscribe<FuelPurchased>(OnBought);
            try
            {
                var wallet = new FakeWallet(100);
                var station = Station(Offer(FuelGrades.Diesel, true, 1.85f));
                var pump = Pump(station, FuelGrades.Diesel, wallet);

                Assert.IsTrue(pump.TryFill(new FakeCan(FuelGrades.Diesel, 20f, 10f), wallet));

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual("station.test", seen[0].StationId);
                Assert.AreEqual(FuelGrades.Diesel, seen[0].Grade);
                Assert.AreEqual(10f, seen[0].Litres, 1e-4f);
                Assert.AreEqual(19, seen[0].PricePaid, "10 L at 1.85 = 18.50, rounded up");
            }
            finally
            {
                EventBus.Unsubscribe<FuelPurchased>(OnBought);
                EventBus.Clear<FuelPurchased>();
            }
        }

        // ---- the rung flip -----------------------------------------------------------------------

        [Test]
        public void APump_IsAFixtureUntilYouAreHoldingSomethingToFill()
        {
            var pump = Pump(Station(Offer(FuelGrades.Gas, true, 2.05f)), FuelGrades.Gas, new FakeWallet(100));

            GameServices.Hands = null;
            Assert.AreEqual(InteractPriority.Fixture, pump.Priority,
                "empty-handed, a pump is just a thing on the wharf");
        }

        [Test]
        public void HoldingACan_ThePumpClaimsToolTarget_OrFillingIsImpossible()
        {
            // THE COLLISION THIS EXISTS FOR: a carried can reports Held (20). A pump left at Fixture (0)
            // loses every press to "set the can down", so standing at the pump holding the can you could
            // never fill it. ToolTarget is the rung that fixes exactly this inversion (see InteractPriority).
            var pump = Pump(Station(Offer(FuelGrades.Gas, true, 2.05f)), FuelGrades.Gas, new FakeWallet(100));

            var canGo = new GameObject("can");
            _spawned.Add(canGo);
            canGo.AddComponent<FakeCanBehaviour>();
            GameServices.Hands = new FakeHands(canGo.transform);

            Assert.AreEqual(InteractPriority.ToolTarget, pump.Priority);
            Assert.Greater(pump.Priority, InteractPriority.Held, "it must outrank the can in your hands");
        }

        [Test]
        public void HoldingSomethingThatIsNotAVessel_LeavesTheRungAlone()
        {
            // The obligation ToolTarget puts on a claimant: claim it only when the tool is actually held,
            // or nothing could ever be put down beside a pump again.
            var pump = Pump(Station(Offer(FuelGrades.Gas, true, 2.05f)), FuelGrades.Gas, new FakeWallet(100));

            var shovelGo = new GameObject("shovel");
            _spawned.Add(shovelGo);
            GameServices.Hands = new FakeHands(shovelGo.transform);

            Assert.AreEqual(InteractPriority.Fixture, pump.Priority);
        }

        [Test]
        public void SwappingWhatIsHeld_ReRunsTheLookup()
        {
            // Priority memoizes on WHICH object is held (it is read on the resolve path). The memo must
            // invalidate when the hands change, or the rung sticks at whatever was held first.
            var pump = Pump(Station(Offer(FuelGrades.Gas, true, 2.05f)), FuelGrades.Gas, new FakeWallet(100));

            var shovelGo = new GameObject("shovel");
            var canGo = new GameObject("can");
            _spawned.Add(shovelGo);
            _spawned.Add(canGo);
            canGo.AddComponent<FakeCanBehaviour>();

            GameServices.Hands = new FakeHands(shovelGo.transform);
            Assert.AreEqual(InteractPriority.Fixture, pump.Priority);

            GameServices.Hands = new FakeHands(canGo.transform);
            Assert.AreEqual(InteractPriority.ToolTarget, pump.Priority);

            GameServices.Hands = null;
            Assert.AreEqual(InteractPriority.Fixture, pump.Priority);
        }
    }
}
