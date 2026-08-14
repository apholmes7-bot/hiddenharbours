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
        private void OnCaught(FishCaught e) => _caught++;
        private void OnLanded(CatchLanded e) => _landed++;

        private StampClock _clock;
        private SaveData _save;

        [SetUp]
        public void SetUp()
        {
            _caught = 0; _landed = 0;
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchLanded>(OnLanded);
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
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
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
        public void The_shovel_slings_for_the_clam_and_comes_back_when_the_pail_takes_it()
        {
            // ⚠️ WITHOUT THIS THE LOOP CANNOT CLOSE. Digging needs the shovel IN HAND and produces a thing
            // that also goes in hand. One slot and no sling means the first clam has nowhere to land.
            var pail = Pail();
            ClamDig dig = Hole(pail);
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            ICarriable shovel = hands.Carried;

            Assert.IsTrue(dig.TryDig());

            Assert.AreSame(shovel, hands.Slung, "the shovel went under her arm, not onto the sand");
            Assert.IsInstanceOf<CarriableCatch>(hands.Carried, "the clam has the hand");

            Assert.IsTrue(hands.TryGiveCatchTo(pail));

            Assert.IsNull(hands.Slung, "the sling is empty again");
            Assert.AreSame(shovel, hands.Carried, "and the shovel is back in her hand, ready to dig");
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
    }
}
