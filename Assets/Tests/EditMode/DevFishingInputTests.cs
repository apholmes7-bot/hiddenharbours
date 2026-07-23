using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // BaitDef (the trap's required bait)
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The owner's blocking bug: Space was double-bound — DevFishingInput cast a handline EVERY frame with
    /// zero gating, so pressing Space during a trap haul both pulled the rope AND cast a line (the cast read
    /// as "I can't haul"). These pin the fix on the same GameObject the greybox wires (FishingController +
    /// TrapHaulController + DevFishingInput all on the Dory):
    ///
    ///   (a) while a haul is live, the handline cast STANDS DOWN — Space belongs to the pull, no line casts;
    ///   (b) handline fishing is a DECK action now — no cast at the helm or on foot (the Build-5 model);
    ///   (c) a closed gate never STRANDS an in-flight cast — the tick keeps running with held=false so it
    ///       eases to its cozy resolution instead of freezing mid-cast.
    ///
    /// Driven through the public gate (<see cref="DevFishingInput.FishingLive"/>) + the public
    /// <see cref="DevFishingInput.TickFishing"/> — the DevTrapInput/TrapHaulController convention, no
    /// play-mode lifecycle, no real key input.
    /// </summary>
    public class DevFishingInputTests
    {
        // In-test hold (the Core contract) — mirrors FishingControllerTests' FakeHold.
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 6;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item)
            {
                if (_items.Count >= Capacity) return false;
                _items.Add(item);
                return true;
            }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeClock : IGameClock
        {
            public double Seconds;
            public double TotalSeconds => Seconds;
            public GameTime Now => default;
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public void SeekTo(double totalSeconds) => Seconds = totalSeconds;
        }

        private sealed class FakeEnv : IEnvironmentService
        {
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;   // glassy calm → a broad, forgiving pull window
            public float TideHeightAt(double totalSeconds) => 2f;
            public float WaterLevelAt(double totalSeconds) => 2f;
        }

        private readonly List<Object> _spawned = new();

        private TrapDef _trap;
        private BaitDef _bait;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            FishSpeciesRegistry.Reset();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<DevNotice>();

            GameServices.Clock = new FakeClock { Seconds = 5000.0 };
            GameServices.Environment = new FakeEnv();

            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _trap.Id = "trap.lobster"; _trap.DisplayName = "Lobster Pot";
            _trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            _trap.RequiredBaitId = "bait.herring";
            _trap.SoakHours = 12f; _trap.MinSoakDepthMeters = 3f; _trap.MaxSoakDepthMeters = 40f;
            _spawned.Add(_trap);

            _bait = ScriptableObject.CreateInstance<BaitDef>();
            _bait.Id = "bait.herring"; _bait.DisplayName = "Herring";
            _bait.FavorsSpeciesIds = new[] { "fish.lobster" };
            _spawned.Add(_bait);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<DevNotice>();
            InteractionGate.Reset();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject NewGo(string name)
        {
            var g = new GameObject(name);
            _spawned.Add(g);
            return g;
        }

        private FishSpeciesDef MakeFish(string id)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        // The full greybox rig on ONE GameObject (the Dory): a handline + a haul + the dev fishing input.
        // Order matters only for the RequireComponent(FishingController); the haul is resolved lazily.
        private (DevFishingInput dev, FishingController fishing, TrapHaulController haul) BuildRig(
            PlacedTrapService svc, Transform rail, FakeHold hold)
        {
            var go = NewGo("Dory");
            var fishing = go.AddComponent<FishingController>();
            fishing.Configure(hold, new[] { MakeFish("fish.cod") }, "region.coddle_cove", Gear.Handline, seed: 7);
            var haul = go.AddComponent<TrapHaulController>();
            haul.Configure(svc, rail, hold, "region.coddle_cove", calmHaulRate: 0.6f);
            var dev = go.AddComponent<DevFishingInput>();
            return (dev, fishing, haul);
        }

        private PlacedTrapService MakeServiceWithPotAt(Vector2 pos)
        {
            var svcGo = NewGo("PlacedTrapService");
            var svc = svcGo.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, svcGo.transform);
            svc.PlaceTrap(_trap, _bait, pos, "region.st_peters");
            return svc;
        }

        // ---- (a) the fix: a live haul owns Space; the handline stands down --------------------------

        [Test]
        public void Cast_IsSuppressed_WhileAHaulIsLive()
        {
            var svc = MakeServiceWithPotAt(new Vector2(1f, 1f));
            var rail = NewGo("Rail"); rail.transform.position = new Vector2(1f, 1f);
            var hold = new FakeHold();
            var (dev, fishing, haul) = BuildRig(svc, rail.transform, hold);

            // On deck (the only place the gear works). Drive the mode through the public handler — in
            // EditMode AddComponent doesn't fire OnEnable, so the components aren't bus-subscribed (the
            // established TrapDeckGatingTests convention).
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(dev.FishingLive, "on deck with no haul → the handline is live");

            // Start the haul: Space now belongs to the PULL, so the cast must stand down.
            Assert.IsTrue(haul.TryStartHaul(), "a pot alongside starts a haul from the deck");
            Assert.IsTrue(haul.IsHauling);
            Assert.IsFalse(dev.FishingLive, "a live haul owns Space — the handline is suppressed");

            // A whole fishing GESTURE during the haul must NOT cast a line (the owner's bug): every held
            // tick is swallowed while the haul owns the key, so the flick never even winds back.
            FlickGestures.Flick(dev, fishing);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase,
                "gesturing mid-haul must never cast a handline");

            // The haul ends WITH the key still held (the hold-haul reality). The carried-over hold must NOT
            // flip into a cast — the release latch swallows it until a genuine release-then-press.
            dev.TickFishing(0.05f, rawHeld: true);   // re-held through the end of the haul → the latch arms
            haul.CancelHaul();
            Assert.IsFalse(haul.IsHauling);
            Assert.IsTrue(dev.FishingLive, "haul over → the handline gate is open again");
            dev.TickFishing(0.05f, true, new Vector2(1f, -1f), true);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase,
                "a key still held from the haul must NOT start a wind-back — even with a live pointer");

            // Release, then a fresh gesture — now it casts (nothing was permanently broken).
            dev.TickFishing(0.05f, rawHeld: false);
            FlickGestures.CastLine(dev, fishing);
            Assert.AreEqual(FishingPhase.Waiting, fishing.Phase, "a genuine release-then-flick casts again");
        }

        // ---- (a′) the resurrected bug: a HOLD-haul that ends with the key down must not auto-cast --------

        [Test]
        public void HoldingThroughTheEndOfAHaul_DoesNotAutoCast_ReleaseThenPressDoes()
        {
            var svc = MakeServiceWithPotAt(new Vector2(1f, 1f));
            var rail = NewGo("Rail"); rail.transform.position = new Vector2(1f, 1f);
            var hold = new FakeHold();
            var (dev, fishing, haul) = BuildRig(svc, rail.transform, hold);

            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            // Start the haul and HOLD Space through it — the hold takes line, the cast stays down.
            Assert.IsTrue(haul.TryStartHaul());
            for (int i = 0; i < 30 && haul.IsHauling; i++)
            {
                haul.TickHaul(0.1f, holding: true);       // the player holds with the swell
                dev.TickFishing(0.1f, rawHeld: true);     // …the SAME Space, ticked into the fishing gate
            }
            Assert.IsFalse(haul.IsHauling, "the hold surfaced the pot — the haul ended");

            // The pot's aboard but Space is STILL held. The instant the haul ended the cast gate reopened —
            // the exact frame FishingController would see a false→true rising edge and start a wind-back.
            // The latch must prevent it: a still-held key starts NOTHING, even with a live pointer.
            dev.TickFishing(0.1f, true, new Vector2(1f, -1f), true);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase,
                "holding through the end of a haul must NEVER auto-cast a handline (the resurrected bug)");
            dev.TickFishing(0.1f, true, new Vector2(1f, -1f), true);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase, "…still no cast while the key stays held");

            // Release, then a fresh flick — a genuine fresh cast.
            dev.TickFishing(0.1f, rawHeld: false);
            FlickGestures.CastLine(dev, fishing);
            Assert.AreEqual(FishingPhase.Waiting, fishing.Phase, "release-then-flick casts a fresh line");
        }

        // ---- (b) handline is a DECK action — gated off the deck ------------------------------------

        [Test]
        public void Cast_IsGated_OffTheDeck()
        {
            var svc = MakeServiceWithPotAt(new Vector2(50f, 50f));   // pot far away → no haul in reach
            var rail = NewGo("Rail");
            var hold = new FakeHold();
            var (dev, fishing, _) = BuildRig(svc, rail.transform, hold);

            // Fresh → un-decked. A full flick gesture ashore must not cast.
            Assert.IsFalse(dev.FishingLive, "fresh component is un-decked → the handline is dead");
            FlickGestures.Flick(dev, fishing);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase, "no casting off the deck");

            // At the helm you're steering — still no cast.
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            Assert.IsFalse(dev.FishingLive, "at the helm the handline is dead");
            FlickGestures.Flick(dev, fishing);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase, "no casting at the helm");

            // On deck → the handline is live and the flick casts.
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(dev.FishingLive, "ON DECK → the handline is live");
            FlickGestures.CastLine(dev, fishing);
            Assert.AreEqual(FishingPhase.Waiting, fishing.Phase, "on deck, the flick casts");
        }

        [Test]
        public void Cast_StandsDown_UnderAModalDialogue()
        {
            var svc = MakeServiceWithPotAt(new Vector2(50f, 50f));
            var rail = NewGo("Rail");
            var (dev, fishing, _) = BuildRig(svc, rail.transform, new FakeHold());

            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(dev.FishingLive);

            InteractionGate.IsBlocked = true;
            Assert.IsFalse(dev.FishingLive, "a modal dialogue owns the keys while up");
            FlickGestures.Flick(dev, fishing);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase, "no casting under a modal dialogue");

            InteractionGate.IsBlocked = false;
            Assert.IsTrue(dev.FishingLive, "released with the dialogue");
        }

        // ---- (b′) the flick-cast wrinkle: a gate closing MID-WIND-BACK aborts, never flings ---------

        [Test]
        public void GateClosingMidWindBack_AbortsTheGesture_InsteadOfFlingingIt()
        {
            var svc = MakeServiceWithPotAt(new Vector2(1f, 1f));
            var rail = NewGo("Rail"); rail.transform.position = new Vector2(1f, 1f);
            var hold = new FakeHold();
            var (dev, fishing, haul) = BuildRig(svc, rail.transform, hold);

            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            // Start winding back (a live gesture, nothing in the water yet)…
            Vector2 a = fishing.transform.position;
            dev.TickFishing(0.02f, true, a + new Vector2(0f, -0.5f), true);
            dev.TickFishing(0.02f, true, a + new Vector2(0f, -1.5f), true);
            Assert.AreEqual(FishingPhase.WindBack, fishing.Phase, "the wind-back is live");

            // …then a haul takes the key. The forced release must ABORT the wind-back (back to Idle),
            // not evaluate the half-made gesture into a flung cast.
            Assert.IsTrue(haul.TryStartHaul());
            dev.TickFishing(0.02f, true, a + new Vector2(0f, -1.5f), true);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase,
                "a gate closing mid-wind-back stands the fisher back up — nothing flies, nothing lost");
        }

        // ---- (c) no stranded cast: a closed gate eases an in-flight cast, never freezes it ----------

        [Test]
        public void InFlightCast_EasesToResolution_WhenTheGateCloses_NotStranded()
        {
            var svc = MakeServiceWithPotAt(new Vector2(1f, 1f));
            var rail = NewGo("Rail"); rail.transform.position = new Vector2(1f, 1f);
            var hold = new FakeHold();
            var (dev, fishing, haul) = BuildRig(svc, rail.transform, hold);

            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            FlickGestures.CastLine(dev, fishing);                  // flick a line out
            Assert.AreEqual(FishingPhase.Waiting, fishing.Phase, "a line is out");

            // Now start a haul mid-cast: the handline must not freeze in Waiting — the gate closes but the
            // tick keeps running with held=false, so the FSM continues to advance toward its cozy resolution.
            Assert.IsTrue(haul.TryStartHaul());
            Assert.IsFalse(dev.FishingLive);

            float t = 0f;
            while (fishing.Phase == FishingPhase.Waiting && t < 30f)
            {
                dev.TickFishing(0.05f, rawHeld: true);   // still "pressing", but the gate forces held=false
                t += 0.05f;
            }
            Assert.AreNotEqual(FishingPhase.Waiting, fishing.Phase,
                "the in-flight cast advanced (bite fired) instead of stranding in Waiting");
        }
    }
}
