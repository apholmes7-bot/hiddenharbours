using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Buying the used outboard at Nine Mile Creek</b> — the M1 ladder's closing rung
    /// (<c>m1-progression-pacing.md</c> §2, target day 13–15).
    ///
    /// <para><b>The hole this closes.</b> <c>boat.dory_outboard</c> has existed end-to-end since #366 —
    /// the hull variant, the transom mount, the oars that stow when she's under power — and NOTHING
    /// SOLD IT. Every rung of that work was unreachable in a real game. The fix is one Def asset on
    /// the existing purchase path, so what this file has to prove is not new mechanics but that the
    /// path actually joins up: coin out of the wallet at one end, an engine on the transom at the
    /// other.</para>
    ///
    /// <para><b>Reads the REAL committed assets off disk</b> (the <see cref="DoryOutboardContentTests"/>
    /// convention), because the failure mode here is a STALE ASSET rather than a broken algorithm — an
    /// offer pointed at a boat id nobody ships, a price hand-edited to something that breaks the ladder,
    /// a hull that quietly stopped being <see cref="PropulsionType.Engine"/>. Synthetic defs would pass
    /// happily through every one of those.</para>
    ///
    /// <para><b>The mount query is the one to read carefully.</b> "Is her engine drawn?" is not a flag
    /// anyone sets — <see cref="BoatHullSkinner"/> asks
    /// <c>visual.HasMotorMesh() &amp;&amp; BoatController.UsesEngineHelm(hull.Propulsion)</c> of the
    /// ACTIVE hull, every time it skins. So the assertion that the purchase makes her mountable has to
    /// run that same predicate against the hull the fleet actually swapped to, not against the offer or
    /// the asset on disk.</para>
    /// </summary>
    public class DoryOutboardPurchaseTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";
        const string DataShip  = "Assets/_Project/Data/Shipwright";

        const string OutboardHullId = "boat.dory_outboard";

        /// <summary>The Punt (<c>PuntOffer</c>, ₲1800) — the next HULL up the ladder. Named as a literal
        /// because her offer asset is builder-generated and not committed, so it cannot be read off
        /// disk in CI; the number itself is pinned by <see cref="PilotableFleetContentTests"/>.</summary>
        const int PuntPrice = 1800;

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

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<BoatPurchased> _purchases = new();
        private void OnPurchased(BoatPurchased e) => _purchases.Add(e);

        [SetUp]
        public void SetUp()
        {
            _purchases.Clear();
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Subscribe<BoatPurchased>(OnPurchased);
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<BoatPurchased>(OnPurchased);
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<ControlModeChanged>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the real content on disk --------------------------------------------------------

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, $"{path} is missing — it is a COMMITTED canonical asset, not a " +
                                "builder-generated one. Re-run Hidden Harbours ▸ Build Nine Mile Creek " +
                                "Scene only if it was deleted; otherwise this is a bad merge.");
            return a;
        }

        static ShipwrightOffer OutboardOffer() =>
            Load<ShipwrightOffer>($"{DataShip}/DoryOutboardOffer.asset");

        static ShipwrightOffer DamagedDoryOffer() =>
            Load<ShipwrightOffer>($"{DataShip}/DamagedDoryOffer.asset");

        static BoatHullDef OutboardHull() => Load<BoatHullDef>($"{DataBoats}/DoryOutboard.asset");

        static BoatHullDef RowedHull() => Load<BoatHullDef>($"{DataBoats}/Dory.asset");

        /// <summary>Hector's till. The builder wires <c>_offer</c>/<c>_walletProvider</c> into the
        /// serialized fields; here the offer and wallet ride the public <c>TryBuy(offer, wallet)</c>
        /// seam that the no-arg entrypoint delegates to — the same code, either way.</summary>
        private Shipwright MakeSeller()
        {
            var go = new GameObject("UsedOutboardSeller");
            _spawned.Add(go);
            return go.AddComponent<Shipwright>();
        }

        /// <summary>The player's boat, wired to the fleet the way the persistent core wires it: the
        /// registry is the REAL committed hulls, keyed by stable id, starting in the rowed dory.
        /// EditMode doesn't run Awake, so the fleet's bus subscription is made by hand — the
        /// <see cref="OwnedFleetTests"/> convention.</summary>
        private (OwnedFleet fleet, BoatController boat, ShipHold hold) MakeFleetInTheRowedDory()
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<Rigidbody2D>();
            var boat = go.AddComponent<BoatController>();
            var hold = go.AddComponent<ShipHold>();
            var fleet = go.AddComponent<OwnedFleet>();

            var rowed = RowedHull();
            boat.SetHull(rowed);
            hold.SetHull(rowed);
            // No SpriteRenderer: the picture is BoatHullSkinner's business and is covered by
            // BoatHullSkinTests. What is under test here is the id-keyed grant and the hull it lands on.
            fleet.Configure(new[] { rowed, OutboardHull() }, boat, hold, null);

            EventBus.Subscribe<BoatPurchased>(fleet.OnBoatPurchased);
            return (fleet, boat, hold);
        }

        // ---- the headline: coin out, engine on ------------------------------------------------

        [Test]
        public void BuyingTheOutboard_DebitsTheWallet_AndLeavesHerMountable()
        {
            var offer = OutboardOffer();
            var seller = MakeSeller();
            var wallet = new FakeWallet(offer.Price + 25);          // just enough, and 25 over
            var (_, boat, hold) = MakeFleetInTheRowedDory();

            Assert.IsFalse(BoatController.UsesEngineHelm(boat.Hull.Propulsion),
                "she starts as the ROWED dory — no engine to draw, and no engine helm.");

            bool bought = seller.TryBuy(offer, wallet);

            Assert.IsTrue(bought, "an affordable outboard can be bought");
            Assert.AreEqual(25, wallet.Money,
                "the wallet is debited EXACTLY the offer's price — the price is the offer's, not a " +
                "literal anywhere in code (rule 6).");
            Assert.AreEqual(1, _purchases.Count, "exactly one BoatPurchased crosses the bus");
            Assert.AreEqual(OutboardHullId, _purchases[0].BoatId);
            Assert.AreEqual(offer.Price, _purchases[0].PricePaid);

            // …and the far end of the path: the fleet took the grant and swapped the active hull, so the
            // predicate the skinner runs to decide whether to hang the motor now answers yes.
            Assert.AreEqual(OutboardHullId, boat.Hull.Id,
                "BoatPurchased → OwnedFleet grants the hull by stable id (D8: the outboard IS a hull " +
                "variant, so the purchase swaps her).");
            Assert.IsTrue(BoatController.UsesEngineHelm(boat.Hull.Propulsion),
                "THE MOUNT QUERY. BoatHullSkinner draws the transom motor when " +
                "`visual.HasMotorMesh() && UsesEngineHelm(hull.Propulsion)` — this is that second " +
                "clause, asked of the hull the purchase actually landed on.");
            Assert.AreEqual(RowedHull().HoldUnits, hold.CapacityUnits,
                "…and an outboard is not a bigger hold: her capacity is untouched by the upgrade.");
        }

        [Test]
        public void TheOutboard_IsBoughtWorking_SoTheBoardingGateNeverLocksHerOut()
        {
            // Shipwright.TryBuy marks a NON-damaged buy repaired on grant, and ControlSwitcher.BoardableNow
            // refuses to board an owned hull that isn't repaired. Get this wrong and the player pays ₲900
            // for a boat they can no longer board — the soft-lock the damaged dory exists to create ON
            // PURPOSE and this offer must not.
            var offer = OutboardOffer();
            var seller = MakeSeller();
            var save = SaveMigration.NewGame();
            GameServices.Save = new FakeSaveService(save);

            Assert.IsFalse(offer.StartsDamaged,
                "an outboard is bought WORKING — there is no second repair beat on this rung.");
            Assert.IsFalse(RepairLedger.IsRepaired(save, OutboardHullId), "…not before the purchase.");

            Assert.IsTrue(seller.TryBuy(offer, new FakeWallet(offer.Price)));

            Assert.IsTrue(RepairLedger.IsRepaired(save, OutboardHullId),
                "…and repaired-on-grant after it, which is what keeps the boarding gate open.");
        }

        [Test]
        public void AnUnaffordableOutboard_ChangesNothing()
        {
            var offer = OutboardOffer();
            var seller = MakeSeller();
            var wallet = new FakeWallet(offer.Price - 1);           // one coin short
            var (_, boat, _) = MakeFleetInTheRowedDory();

            Assert.IsFalse(seller.TryBuy(offer, wallet), "one coin short is short");
            Assert.AreEqual(offer.Price - 1, wallet.Money, "a refused purchase spends nothing");
            Assert.IsEmpty(_purchases, "…and publishes nothing, so no hull is granted");
            Assert.AreEqual("boat.dory", boat.Hull.Id, "she is still the rowed dory");
        }

        // ---- content validation: the offer, and where it sits on the ladder --------------------

        [Test]
        public void TheOffer_NamesAHullThatShipsAndIsActuallyPowered()
        {
            var offer = OutboardOffer();
            var hull = OutboardHull();

            Assert.AreEqual(OutboardHullId, offer.BoatId,
                "ids are append-only and stable (§5) — and an offer whose BoatId names no hull is a " +
                "purchase that takes the coin and grants nothing (OwnedFleet's registry miss is a " +
                "graceful no-op, which is exactly what makes it silent).");
            Assert.AreEqual(offer.BoatId, hull.Id, "…and the hull on disk is the one it names.");
            Assert.AreEqual(PropulsionType.Engine, hull.Propulsion,
                "the whole point of the rung: the hull this offer sells is the POWERED variant.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(offer.DisplayName),
                "the buy screen has no Flavor field for a boat row, so DisplayName carries all of it.");
        }

        [Test]
        public void ThePrice_SitsWhereTheClosingRungBelongs_AboveTheWholeDory_AndUnderTheNextHull()
        {
            // The ladder, from the SHIPPED assets rather than from memory. This is a pacing guard, not a
            // balance claim: the owner may tune the number freely inside these bounds (rule 6), and if a
            // tuning pass wants it OUTSIDE them that is a deliberate re-shaping of m1-progression-pacing
            // §2's day 13–15 rung — which should move this test, on purpose.
            var offer = OutboardOffer();
            var dory = DamagedDoryOffer();
            int wholeDory = dory.Price + dory.RepairCost;

            Assert.Greater(offer.Price, wholeDory,
                $"the closing rung must cost more than the whole dory ({dory.Price} + {dory.RepairCost} " +
                $"= ₲{wholeDory}, the day-6–9 save-up) or day 13–15 is no climb at all.");
            Assert.Less(offer.Price, PuntPrice,
                $"…and less than the Punt (₲{PuntPrice}), or hanging a kicker on the boat you own stops " +
                "being the cheaper rung than buying a bigger boat — which is the shape of the whole " +
                "ladder (P2, P4).");
            Assert.AreEqual(0, offer.RepairCost,
                "no repair beat on this rung, so the field says so rather than carrying a stale default.");
        }
    }
}
