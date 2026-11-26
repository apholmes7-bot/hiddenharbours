using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A clam can be in hand, but it needs a container to stack</b> — the owner's third ruling of
    /// 2026-08-13, pinned.
    ///
    /// <para>The claims, in the order they would break:
    /// <list type="number">
    ///   <item><b>THE LANDING MOVED.</b> A dug clam goes into her HAND, not into the pail. The pail stays
    ///   empty until she puts it there.</item>
    ///   <item><b>THE TWO EVENTS MEAN DIFFERENT THINGS.</b> <c>FishCaught</c> has always meant "a catch
    ///   entered a hold" — fill renderers re-read the container on it, onboarding counts it, deck
    ///   presenters re-stack. It must NOT fire over a clam that is merely in a fist. <c>CatchLanded</c>
    ///   carries that moment instead.</item>
    ///   <item><b>THE SLING.</b> Digging needs the shovel IN HAND and produces a thing that also goes in
    ///   hand — so without somewhere for the shovel to go, the first clam has nowhere to land and the loop
    ///   cannot close. The tool tucks under her arm and comes back the moment the hands are free.</item>
    ///   <item><b>FRESHNESS SURVIVES THE WALK.</b> The item is moved, never re-stamped. Re-stamping on the
    ///   stack would silently reset the spoil clock every time she filled a bucket, and nothing else in
    ///   the game would notice for a long time.</item>
    ///   <item><b>THE BOAT IS UNTOUCHED.</b> At sea a catch still goes straight into the hold with
    ///   <c>FishCaught</c>, because a boat HAS a hold and the deck ladder is its own arc.</item>
    /// </list></para>
    /// </summary>
    public class CatchInHandTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 20;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 1.0f;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 0.5f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class StampClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private readonly List<Object> _spawned = new();
        private int _caught;      // FishCaught — "a catch entered a hold"
        private int _landed;      // CatchLanded — "a catch is in her hands"
        private int _taken;       // CatchTakenFromHand — "the loose catch left her hands"
        private CatchItem _takenItem;
        private void OnCaught(FishCaught e) => _caught++;
        private void OnLanded(CatchLanded e) => _landed++;
        private void OnTaken(CatchTakenFromHand e) { _taken++; _takenItem = e.Item; }

        private StampClock _clock;
        private SaveData _save;

        [SetUp]
        public void SetUp()
        {
            _caught = 0; _landed = 0; _taken = 0; _takenItem = default;
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<CatchTakenFromHand>();
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchLanded>(OnLanded);
            EventBus.Subscribe<CatchTakenFromHand>(OnTaken);
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();

            _clock = new StampClock { TotalSeconds = 1000d };
            _save = SaveMigration.NewGame();
            _save.OwnedGear.Add("gear.shovel");
            _save.OwnedGear.Add("gear.bucket");

            GameServices.Clock = _clock;
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new FlatEnv();
            GameServices.Save = new FakeSaveService(_save);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<CatchLanded>(OnLanded);
            EventBus.Unsubscribe<CatchTakenFromHand>(OnTaken);
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<CatchTakenFromHand>();
            Interactables.Clear();
            InteractVerb.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures --------------------------------------------------------------------------------

        private FishSpeciesDef Clam()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.soft_shell_clam"; f.DisplayName = "Soft-shell Clam";
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2;
            f.SupplyElasticity = 0.45f; f.SpoilPerDay = 0.5f;
            _spawned.Add(f);
            return f;
        }

        private ClamDig Hole(IHold bucket)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            var d = go.AddComponent<ClamDig>();
            d.Configure(Clam(), bucket, go.transform, "gear.shovel", seed: 7, reachRadius: 1.25f);
            d.ConfigureInteract("fixture.clam_hole.test", ToolDef.ShovelId);
            return d;
        }

        private ClamBucket Pail()
        {
            var go = new GameObject("Pail");
            _spawned.Add(go);
            var b = go.AddComponent<ClamBucket>();
            b.Configure(20, requireOwnedBucket: false);
            return b;
        }

        // ---- 1. THE LANDING MOVED --------------------------------------------------------------------

        [Test]
        public void PailOwnership_ControlsCapacityAndNeverDeletesItsExistingCatch()
        {
            var pail = Pail();
            pail.Configure(20, requireOwnedBucket: true);
            var clam = new CatchItem("fish.soft_shell_clam", "Clam", FishCategory.Shellfish, 0.1f, 2, 0.45f);
            Assert.AreEqual(20, pail.CapacityUnits);
            Assert.IsTrue(pail.TryAdd(clam));
            _save.OwnedGear.Remove("gear.bucket");
            Assert.AreEqual(0, pail.CapacityUnits);
            Assert.IsFalse(pail.TryAdd(clam));
            Assert.AreEqual(1, pail.UsedUnits);
            Assert.AreEqual(1, _save.BucketCatch.Count);
            _save.OwnedGear.Add("gear.bucket");
            Assert.AreEqual(20, pail.CapacityUnits);
            Assert.IsTrue(pail.TryAdd(clam));
            Assert.AreEqual(2, pail.UsedUnits);
        }

        [Test]
        public void RealPail_CapsAtTwenty_AndRemovalPreservesTheOtherCatches()
        {
            var pail = Pail();
            for (int i = 0; i < 20; i++)
                Assert.IsTrue(pail.TryAdd(new CatchItem("fish.soft_shell_clam", "Clam", FishCategory.Shellfish,
                    0.05f + i * 0.005f, 2, 0.45f, 0.5f, Freshness.Landed(1000d + i))));
            Assert.AreEqual(20, pail.UsedUnits);
            Assert.AreEqual(20, _save.BucketCatch.Count);
            Assert.IsFalse(pail.TryAdd(pail.Items[0]));
            Assert.IsFalse(pail.TryRemoveAt(-1));
            Assert.IsFalse(pail.TryRemoveAt(20));
            var survivor = pail.Items[1];
            Assert.IsTrue(pail.TryRemoveAt(0));
            Assert.AreEqual(19, pail.UsedUnits);
            Assert.AreEqual(19, _save.BucketCatch.Count);
            Assert.AreEqual(survivor.WeightKg, pail.Items[0].WeightKg);
            Assert.AreEqual(survivor.Freshness.LastSettleGameSeconds, pail.Items[0].Freshness.LastSettleGameSeconds);
            Assert.AreEqual(0, _caught);
            Assert.AreEqual(0, _landed);
            pail.Clear();
            Assert.AreEqual(0, pail.UsedUnits);
            Assert.IsEmpty(_save.BucketCatch);
        }

        [Test]
        public void A_dug_clam_goes_into_her_HAND_and_the_pail_stays_empty()
        {
            var pail = Pail();
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsTrue(dig.TryDig());

            Assert.IsInstanceOf<CarriableCatch>(hands.Carried, "the clam is in her hand");
            Assert.AreEqual("fish.soft_shell_clam", hands.Carried.DefId);
            Assert.AreEqual(0, pail.UsedUnits, "…and NOT in the pail — that is the whole ruling");
        }

        [Test]
        public void FishCaught_stays_at_the_STACK_and_CatchLanded_carries_the_hand()
        {
            // ⚠️ The event claim. FishCaught means "a catch entered a hold" — the fill renderers, the
            // onboarding director and the deck presenters all act on it. Firing it over a clam in a fist
            // would lie to every one of them, and the lie would be invisible for a long time.
            var pail = Pail();
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsTrue(dig.TryDig());
            Assert.AreEqual(0, _caught, "nothing entered a hold — she is holding it");
            Assert.AreEqual(1, _landed, "the hand moment has its own event");

            Assert.IsTrue(hands.TryGiveCatchTo(pail));
            Assert.AreEqual(1, _caught, "NOW a catch entered a hold");
            Assert.AreEqual(1, _landed, "and the hand moment did not happen twice");
        }

        [Test]
        public void Putting_it_in_the_pail_stacks_it_and_frees_her_hands()
        {
            var pail = Pail();
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            Assert.IsTrue(dig.TryDig());

            Assert.IsTrue(hands.TryGiveCatchTo(pail));

            Assert.AreEqual(1, pail.UsedUnits, "one clam in the pail");
            Assert.AreEqual("fish.soft_shell_clam", pail.Items[0].SpeciesId);
            Assert.IsFalse(hands.Carried is CarriableCatch, "the clam is out of her hands");
        }

        // ---- 2. THE SLING ----------------------------------------------------------------------------

        [Test]
        public void The_shovel_STAYS_IN_HAND_for_the_clam_now_that_she_has_two()
        {
            // ⭐ THE CONTINUITY LAW, and the visible change the second slot bought. This used to sling:
            // one slot meant the clam could only land by displacing the shovel. Two slots mean the clam
            // takes the free hand and NOTHING already held moves — which is the owner's ruling in its
            // smallest form ("a fish in one hand and the rod in the other"), and it is what removes a
            // tuck-and-untuck animation beat from every single dug clam.
            var pail = Pail();
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            ICarriable shovel = hands.Carried;
            CarryHandSide shovelHand = hands.Slots.SideOf(shovel);

            Assert.IsTrue(dig.TryDig());

            Assert.IsNull(hands.Slung, "a hand was free, so nothing needed tucking away");
            Assert.AreEqual(shovelHand, hands.Slots.SideOf(shovel), "the shovel did not even change hands");
            Assert.IsInstanceOf<CarriableCatch>(hands.Slots.FirstOf(HandLoad.Catch),
                                                "and the clam has the other one");
            Assert.AreEqual(2, hands.Slots.UsedHands);

            Assert.IsTrue(hands.TryGiveCatchTo(pail));

            Assert.AreEqual(1, hands.Slots.UsedHands, "the clam is stowed and the hand is free again");
            Assert.AreEqual(shovelHand, hands.Slots.SideOf(shovel), "the shovel never moved at all");
        }

        [Test]
        public void The_sling_still_catches_the_case_two_hands_cannot_the_pail_AND_the_shovel()
        {
            // The sling did not go away — it narrowed to the one case where the alternative really is
            // dropping the catch on the floor: BOTH hands committed. Carry the pail as well as the
            // shovel and there is no free hand, so the tool tucks away exactly as it always did, and
            // comes straight back when the pail takes the clam.
            var pail = Pail();
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            ICarriable shovel = hands.Carried;

            var pailGo = new GameObject("CarriedPail");
            _spawned.Add(pailGo);
            var carried = pailGo.AddComponent<CarriableBucket>();
            carried.Configure(20, pailGo.AddComponent<SpriteRenderer>(), "container.test.carried");
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(carried), "a pail shares with a shovel");
            Assert.AreEqual(2, hands.Slots.UsedHands, "both hands are now committed");

            Assert.IsTrue(dig.TryDig());

            Assert.AreSame(shovel, hands.Slung, "the shovel went under her arm, not onto the sand");
            Assert.IsInstanceOf<CarriableCatch>(hands.Slots.FirstOf(HandLoad.Catch), "the clam has a hand");

            Assert.IsTrue(hands.TryGiveCatchTo(pail));

            Assert.IsNull(hands.Slung, "the sling is empty again");
            Assert.AreNotEqual(CarryHandSide.None, hands.Slots.SideOf(shovel),
                               "and the shovel is back in a hand, ready to dig");
            Assert.AreEqual(2, hands.Slots.UsedHands, "the pail and the shovel, as she started");
        }

        [Test]
        public void A_second_dig_refuses_while_a_clam_is_still_in_her_hand()
        {
            // The over-encumbrance rule in its cheapest honest form (§4.2): full is full — no weight
            // meter, no slow crawl, you just deal with the clam you have.
            var pail = Pail();
            ClamDig first = Hole(pail);
            ClamDig second = Hole(pail);
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsTrue(first.TryDig());
            Assert.IsFalse(second.TryDig(), "one pair of hands, one clam");
            Assert.IsFalse(second.Consumed, "and the refused hole is NOT spent — it keeps its clam");
            Assert.AreEqual(0, pail.UsedUnits);
        }

        // ---- 3. FRESHNESS ----------------------------------------------------------------------------

        [Test]
        public void The_clam_is_MOVED_not_re_stamped_so_time_in_her_hand_still_counts()
        {
            // ⚠️ The silent one. If the stack re-stamped Freshness.Landed, every clam would come out of
            // the hand box-fresh and the whole spoil clock would quietly stop mattering — with no test
            // anywhere failing, because every OTHER property of the item would be identical.
            var pail = Pail();
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            _clock.TotalSeconds = 1000d;
            Assert.IsTrue(dig.TryDig());
            var inHand = (CarriableCatch)hands.Carried;
            double stampedAt = inHand.Item.Freshness.LastSettleGameSeconds;
            Assert.AreEqual(1000d, stampedAt, 1e-9, "stamped when it left the sand");

            _clock.TotalSeconds = 9999d;               // she walks the bar for a while
            Assert.IsTrue(hands.TryGiveCatchTo(pail));

            Assert.AreEqual(1000d, pail.Items[0].Freshness.LastSettleGameSeconds, 1e-9,
                            "the stack must not restart the spoil clock");
        }

        // ---- 4. THE PAIL AS A CANDIDATE --------------------------------------------------------------

        [Test]
        public void The_pail_is_only_a_candidate_when_a_catch_is_in_her_hands()
        {
            var pail = Pail();
            Interactables.Register(pail);
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsFalse(pail.IsAvailable, "holding a shovel, the pail has nothing to offer");

            Assert.IsTrue(dig.TryDig());
            Assert.IsTrue(pail.IsAvailable, "holding a clam, it does");

            var actor = InteractActor.For(hands.transform.position, Vector2.up, ControlMode.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolveNow(actor, InteractResolver.DefaultFacingArcDegrees,
                                                         out IInteractable best));
            Assert.AreSame(pail, best, "with a clam in hand the press belongs to the container");

            best.Interact(actor);
            Assert.AreEqual(1, pail.UsedUnits, "and the press stacked it");
        }

        [Test]
        public void A_full_pail_refuses_and_the_clam_STAYS_in_her_hand()
        {
            // A full container is still AVAILABLE — press it and the game says so (P5). What must never
            // happen is the catch evaporating between the hand and the refusal.
            var pail = Pail();
            pail.Configure(1, requireOwnedBucket: false);
            pail.TryAdd(new CatchItem("fish.filler", "Filler", FishCategory.Shellfish, 0.1f, 1, 0.1f, 0f,
                                      Freshness.Landed(0d)));

            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            Assert.IsTrue(dig.TryDig());

            Assert.IsFalse(hands.TryGiveCatchTo(pail), "no room");
            Assert.IsInstanceOf<CarriableCatch>(hands.Carried, "the clam is still in her hand, not gone");
            Assert.AreEqual(1, pail.UsedUnits, "and nothing was overwritten");
        }

        // ---- 5. COMPATIBILITY: no hands, and the boat --------------------------------------------------

        [Test]
        public void With_no_hands_published_the_dig_falls_back_to_the_pail_exactly_as_before()
        {
            // EditMode, a bare art scene, a region played with no persistent core. "Nobody published a
            // pair of hands" must mean "as before" — never a dropped clam.
            var pail = Pail();
            ClamDig dig = Hole(pail);
            dig.ConfigureInteract("fixture.clam_hole.test", shovelToolId: null);   // no hands ⇒ no in-hand gate

            Assert.IsNull(GameServices.CatchHands, "precondition: nothing published");
            Assert.IsTrue(dig.TryDig());

            Assert.AreEqual(1, pail.UsedUnits, "straight into the pail, the pre-ruling path");
            Assert.AreEqual(1, _caught, "and FishCaught fired at the hold, as it always did");
            Assert.AreEqual(0, _landed, "no hand moment — there were no hands");
        }

        [Test]
        public void At_sea_the_rod_still_lands_into_the_hold_with_FishCaught()
        {
            // The boat path is deliberately untouched: a boat HAS a hold, and the deck ladder (tote, tray,
            // ice box) is its own arc. This is the guard that keeps both paths coexisting.
            var hold = new FakeHold();
            var go = new GameObject("Dory");
            _spawned.Add(go);
            var fishing = go.AddComponent<FishingController>();
            var cod = ScriptableObject.CreateInstance<FishSpeciesDef>();
            cod.Id = "fish.cod"; cod.DisplayName = "Cod"; cod.Category = FishCategory.InshoreGroundfish;
            cod.MinWeightKg = 1f; cod.MaxWeightKg = 4f; cod.BaseValue = 12; cod.SupplyElasticity = 0.2f;
            _spawned.Add(cod);
            fishing.Configure(hold, new[] { cod }, "region.st_peters", Gear.Handline, seed: 7);

            CarryHands hands = ToolInHand.Empty(_spawned);
            fishing.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            var item = new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 2f, 12, 0.2f, 0.4f,
                                     Freshness.Landed(_clock.TotalSeconds));
            // Drive the destination decision the way the land path does, with the mode set to OnDeck.
            Assert.IsTrue(hold.TryAdd(item));
            EventBus.Publish(new FishCaught(item));

            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(1, _caught);
            Assert.IsFalse(hands.IsCarrying, "nothing went into her hands on deck");
        }

        // ---- 6. THE HANDS THEMSELVES -----------------------------------------------------------------

        [Test]
        public void TryPutInHand_refuses_a_second_catch_and_keeps_the_first()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            var a = new CatchItem("fish.a", "A", FishCategory.Shellfish, 0.1f, 1, 0.1f, 0f, Freshness.Landed(0d));
            var b = new CatchItem("fish.b", "B", FishCategory.Shellfish, 0.2f, 2, 0.1f, 0f, Freshness.Landed(0d));

            Assert.IsTrue(hands.TryPutInHand(a));
            Assert.IsFalse(hands.TryPutInHand(b), "one catch at a time");
            Assert.AreEqual("fish.a", hands.Carried.DefId, "and the refusal did not swap what she holds");
        }

        [Test]
        public void TryGiveCatchTo_is_a_no_op_when_there_is_nothing_to_give()
        {
            var pail = Pail();
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsFalse(hands.TryGiveCatchTo(pail), "a shovel is not a catch");
            Assert.AreEqual(0, pail.UsedUnits);
            Assert.IsTrue(hands.IsCarrying, "and she still has her shovel");
        }

        // ---- 7. A STATION CAN TAKE THE LOOSE CATCH THROUGH CORE ---------------------------------------

        private static CatchItem PreparedCatch()
            => new CatchItem("fish.atlantic_cod", "Atlantic cod", FishCategory.InshoreGroundfish,
                             1.75f, 17, 0.23f, 0.42f,
                             new FreshnessState(0.19f, 716.5d, StorageMode.Ambient));

        private CarriableBucket CarryPail(CarryHands hands)
        {
            var go = new GameObject("CarriedPail");
            _spawned.Add(go);
            var pail = go.AddComponent<CarriableBucket>();
            pail.Configure(20, go.AddComponent<SpriteRenderer>(), "container.test.carried");
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(pail));
            return pail;
        }

        [Test]
        public void Core_take_returns_the_whole_catch_frees_the_slot_and_removes_the_carried_object()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            ICatchHands contract = GameServices.CatchHands;
            CatchItem original = PreparedCatch();
            Assert.IsTrue(contract.TryPutInHand(original));
            var carried = (CarriableCatch)hands.Slots.FirstOf(HandLoad.Catch);
            _clock.TotalSeconds += 1234d;

            Assert.IsTrue(contract.TryTakeFromHand(out CatchItem taken));

            Assert.AreEqual(original, taken, "every field, including the freshness stamp, survives");
            Assert.IsTrue(carried == null, "the carried object is destroyed in EditMode");
            Assert.AreEqual(0, hands.GetComponentsInChildren<CarriableCatch>(true).Length);
            Assert.IsTrue(hands.Slots.IsEmpty);
            Assert.IsFalse(hands.IsCarrying);
            Assert.IsNull(hands.Carried);
            Assert.AreEqual(1, _taken);
            Assert.AreEqual(original, _takenItem);
            Assert.AreEqual(0, _caught, "taking is not a hold landing");
            Assert.AreEqual(1, _landed, "taking must not publish another landing");
            Assert.IsFalse(contract.TryTakeFromHand(out CatchItem again));
            Assert.AreEqual(default(CatchItem), again);
            Assert.AreEqual(1, _taken, "a second take cannot repeat the signal");
            Assert.IsTrue(contract.TryPutInHand(original), "the freed slot accepts the next catch");
        }

        [Test]
        public void Core_take_and_peek_on_empty_hands_are_silent_and_change_nothing()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            ICatchHands contract = GameServices.CatchHands;

            Assert.IsFalse(contract.TryPeekHand(out CatchItem peeked));
            Assert.AreEqual(default(CatchItem), peeked);
            Assert.IsFalse(contract.TryTakeFromHand(out CatchItem taken));
            Assert.AreEqual(default(CatchItem), taken);
            Assert.IsTrue(hands.Slots.IsEmpty);
            Assert.IsNull(hands.Carried);
            Assert.IsNull(hands.Slung);
            Assert.AreEqual(0, hands.transform.childCount);
            Assert.AreEqual(0, _taken);
            Assert.AreEqual(0, _caught);
            Assert.AreEqual(0, _landed);
        }

        [Test]
        public void Core_peek_keeps_the_same_item_object_and_slot_until_it_is_taken()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            ICatchHands contract = GameServices.CatchHands;
            CatchItem original = PreparedCatch();
            Assert.IsTrue(contract.TryPutInHand(original));
            ICarriable carried = hands.Carried;
            CarryHandSide side = hands.Slots.SideOf(carried);

            Assert.IsTrue(contract.TryPeekHand(out CatchItem first));
            Assert.IsTrue(contract.TryPeekHand(out CatchItem second));

            Assert.AreEqual(original, first);
            Assert.AreEqual(original, second);
            Assert.AreSame(carried, hands.Carried);
            Assert.AreEqual(side, hands.Slots.SideOf(carried));
            Assert.AreEqual(1, hands.Slots.UsedHands);
            Assert.AreEqual(0, _taken);
            Assert.AreEqual(0, _caught);
            Assert.AreEqual(1, _landed);
            Assert.IsTrue(contract.TryTakeFromHand(out CatchItem taken));
            Assert.AreEqual(first, taken);
        }

        [Test]
        public void A_carried_pail_is_not_a_hand_catch_and_its_contents_are_untouched()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            CarriableBucket pail = CarryPail(hands);
            CatchItem original = PreparedCatch();
            Assert.IsTrue(pail.TryAdd(original));
            CarryHandSide side = hands.Slots.SideOf(pail);
            ICatchHands contract = GameServices.CatchHands;

            Assert.IsFalse(contract.TryPeekHand(out CatchItem peeked));
            Assert.AreEqual(default(CatchItem), peeked);
            Assert.IsFalse(contract.TryTakeFromHand(out CatchItem taken));
            Assert.AreEqual(default(CatchItem), taken);
            Assert.AreSame(pail, hands.Carried);
            Assert.AreEqual(side, hands.Slots.SideOf(pail));
            Assert.AreEqual(hands.transform, pail.transform.parent);
            Assert.AreEqual(1, hands.Slots.UsedHands);
            Assert.AreEqual(1, pail.UsedUnits);
            Assert.AreEqual(original, pail.Items[0]);
            Assert.AreEqual(0, _taken);
            Assert.AreEqual(0, _caught);
            Assert.AreEqual(0, _landed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Both_handoff_paths_signal_once_after_cleanup_and_restore_the_slung_tool(bool toHold)
        {
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            ICarriable tool = hands.Carried;
            CarriableBucket pail = CarryPail(hands);
            CatchItem original = PreparedCatch();
            Assert.IsTrue(GameServices.CatchHands.TryPutInHand(original));
            Assert.AreSame(tool, hands.Slung);
            bool observedCleanHands = false;
            EventBus.Subscribe<CatchTakenFromHand>(_ =>
            {
                observedCleanHands = !hands.Slots.Holds(HandLoad.Catch)
                                     && hands.GetComponentsInChildren<CarriableCatch>(true).Length == 0
                                     && hands.Slung == null && CarriedItem.IsHeld(hands, tool);
            });

            if (toHold)
                Assert.IsTrue(hands.TryGiveCatchTo(pail));
            else
            {
                Assert.IsTrue(GameServices.CatchHands.TryTakeFromHand(out CatchItem taken));
                Assert.AreEqual(original, taken);
            }

            Assert.IsTrue(observedCleanHands, "the signal observes completed cleanup and sling recovery");
            Assert.AreEqual(1, _taken);
            Assert.AreEqual(original, _takenItem);
            Assert.AreEqual(toHold ? 1 : 0, _caught, "only a hold handoff publishes FishCaught");
            Assert.AreEqual(1, _landed);
            Assert.AreEqual(toHold ? 1 : 0, pail.UsedUnits);
            if (toHold) Assert.AreEqual(original, pail.Items[0]);
            Assert.AreEqual(2, hands.Slots.UsedHands, "the pail and the restored tool");
            Assert.IsFalse(GameServices.CatchHands.TryTakeFromHand(out _));
            Assert.AreEqual(1, _taken);
        }

        [Test]
        public void A_refused_hold_handoff_keeps_the_catch_and_emits_no_taken_signal()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            CatchItem original = PreparedCatch();
            Assert.IsTrue(GameServices.CatchHands.TryPutInHand(original));
            ICarriable carried = hands.Carried;
            var full = new FakeHold { Capacity = 0 };

            Assert.IsFalse(hands.TryGiveCatchTo(null));
            Assert.IsFalse(hands.TryGiveCatchTo(full));
            Assert.AreSame(carried, hands.Carried);
            Assert.IsTrue(GameServices.CatchHands.TryPeekHand(out CatchItem peeked));
            Assert.AreEqual(original, peeked);
            Assert.AreEqual(0, _taken);
            Assert.AreEqual(0, _caught);
        }

        [Test]
        public void A_hold_landing_observer_cannot_take_the_same_catch_again()
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            CatchItem original = PreparedCatch();
            Assert.IsTrue(GameServices.CatchHands.TryPutInHand(original));
            var hold = new FakeHold();
            bool sawEmptyHand = false;
            bool tookAgain = true;
            EventBus.Subscribe<FishCaught>(e =>
            {
                sawEmptyHand = !GameServices.CatchHands.TryPeekHand(out _);
                tookAgain = GameServices.CatchHands.TryTakeFromHand(out _);
            });

            Assert.IsTrue(hands.TryGiveCatchTo(hold));

            Assert.IsTrue(sawEmptyHand, "hold listeners see the committed handoff");
            Assert.IsFalse(tookAgain, "the same catch cannot be taken while its hold landing is announced");
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(original, hold.Items[0]);
            Assert.AreEqual(1, _caught);
            Assert.AreEqual(1, _taken);
        }
    }
}
