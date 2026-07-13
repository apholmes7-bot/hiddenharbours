using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Build-7 <see cref="DeckPot"/> state machine, engine-light: pot aboard → pick (nip risk) →
    /// sort (keeper / short / berried) → band (lands EXACTLY the resolved catch item — the unchanged
    /// economy path) → bait-ready — plus the cozy AUTO-RESOLVE (the ADR 0020 greybox compromise: no
    /// dupes, no silent loss). Determinism is pinned end-to-end: the same placement facts build the same
    /// animals and the same grab sequence resolves the same fates, run after run (rule 5).
    /// </summary>
    public class DeckPotStateTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 6;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private const int Seed = 4242;
        private const string Instance = "trap.lobster#5000.1";
        private const double Placed = 5000.0;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- builders ---------------------------------------------------------------------------

        private TrapDef MakeTrap(DeckWorkDef deckWork)
        {
            var t = ScriptableObject.CreateInstance<TrapDef>();
            t.Id = "trap.lobster"; t.DisplayName = "Lobster Pot";
            t.AllowedCatchFishIds = new[] { "fish.lobster" };
            t.RequiredBaitId = "bait.herring";
            t.DeckWork = deckWork;
            _spawned.Add(t);
            return t;
        }

        /// <summary>A deck-work ruleset with ONE lobster rule whose knobs the test dials: force keepers
        /// (min keep at 0), force shorts (min keep above the window), force/forbid berried and nips.</summary>
        private DeckWorkDef MakeDef(float minKeepMm, float berriedChance, float nipRushed, float nipCareful)
        {
            var d = ScriptableObject.CreateInstance<DeckWorkDef>();
            d.Id = "deckwork.test"; d.DisplayName = "Test deck work";
            d.QuickGrabSeconds = 0.15f;
            d.FullGrabSeconds = 0.9f;
            d.NipChanceRushed01 = nipRushed;
            d.NipChanceCareful01 = nipCareful;
            d.SpeciesRules = new[]
            {
                new SpeciesDeckRule
                {
                    SpeciesId = "fish.lobster",
                    MinKeepSizeMm = minKeepMm,
                    SizeMinMm = 62f,
                    SizeMaxMm = 140f,
                    CanBeBerried = berriedChance > 0f,
                    BerriedChance01 = berriedChance,
                    Shape = DeckAnimalShape.Lobster,
                },
            };
            _spawned.Add(d);
            return d;
        }

        private static CatchItem Lobster(float kg = 0.9f)
            => new CatchItem("fish.lobster", "American Lobster", FishCategory.Shellfish, kg, 28, 0.35f);

        private DeckPot MakePot(DeckWorkDef def, params CatchItem[] items)
            => new DeckPot(MakeTrap(def), def, Instance, Placed, Seed, items);

        // ---- deterministic derivation --------------------------------------------------------------

        [Test]
        public void SameFacts_BuildIdenticalAnimals_EveryTime()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 83f, berriedChance: 0.15f, nipRushed: 0.55f, nipCareful: 0.06f);
            var items = new[] { Lobster(0.7f), Lobster(1.1f), Lobster(2.3f) };

            DeckPot a = MakePot(def, items);
            DeckPot b = MakePot(def, items);

            Assert.AreEqual(a.Animals.Count, b.Animals.Count);
            for (int i = 0; i < a.Animals.Count; i++)
            {
                Assert.AreEqual(a.Animals[i].SizeMm, b.Animals[i].SizeMm, 0f,
                    $"animal {i}: same placement facts ⇒ bit-identical size (rule 5)");
                Assert.AreEqual(a.Animals[i].Berried, b.Animals[i].Berried, $"animal {i}: same berried flag");
                Assert.AreEqual(a.Animals[i].Keeper, b.Animals[i].Keeper, $"animal {i}: same sort verdict");
            }
        }

        [Test]
        public void SameGrabSequence_ResolvesTheSameFates_EveryTime()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 83f, berriedChance: 0.15f, nipRushed: 0.5f, nipCareful: 0.5f);
            var items = new[] { Lobster(0.7f), Lobster(1.1f), Lobster(2.3f) };

            List<GrabOutcome> Run()
            {
                DeckPot pot = MakePot(def, items);
                var outcomes = new List<GrabOutcome>();
                // Grab with the same mid-care hold until the pot is empty (nips retry the same animal).
                for (int guard = 0; guard < 64 && pot.InPotCount > 0; guard++)
                    outcomes.Add(pot.TryGrabNext(0.5f, out _));
                return outcomes;
            }

            CollectionAssert.AreEqual(Run(), Run(),
                "an identical grab sequence over the same facts resolves identically — nips and all (rule 5)");
        }

        [Test]
        public void TheBandedItem_IsExactlyTheResolvedCatchItem()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 0f, nipCareful: 0f);
            CatchItem resolved = Lobster(1.234f);
            DeckPot pot = MakePot(def, resolved);

            Assert.AreEqual(GrabOutcome.Keeper, pot.TryGrabNext(1f, out _));
            var hold = new FakeHold();
            Assert.AreEqual(BandOutcome.Banded, pot.TryBandNext(hold, out _));

            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(resolved.SpeciesId, hold.Items[0].SpeciesId, "the deck never changes WHAT was caught");
            Assert.AreEqual(resolved.WeightKg, hold.Items[0].WeightKg, 0f, "same weight — the Build-3 roll untouched");
            Assert.AreEqual(resolved.BaseValue, hold.Items[0].BaseValue, "same value — zero economy change");
        }

        // ---- the pick + nip ------------------------------------------------------------------------

        [Test]
        public void ANip_LeavesTheAnimalInThePot_AndTheRetryIsAFreshDeterministicRoll()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 1f, nipCareful: 1f);
            DeckPot pot = MakePot(def, Lobster());

            Assert.AreEqual(GrabOutcome.Nipped, pot.TryGrabNext(0.5f, out DeckPot.Animal a),
                "chance 1 ⇒ the grab is nipped");
            Assert.AreEqual(DeckAnimalFate.InPot, a.Fate, "a nip costs time only — the animal stays in the pot");
            Assert.AreEqual(1, a.GrabAttempts, "the attempt was counted (the retry draws the NEXT stream value)");

            Assert.AreEqual(GrabOutcome.Nipped, pot.TryGrabNext(0.5f, out _), "still chance 1 — nipped again");
            Assert.AreEqual(2, a.GrabAttempts);
            Assert.AreEqual(1, pot.InPotCount, "no animal ever lost to a nip");
        }

        [Test]
        public void AFullCarefulHold_GrabsClean_WhenTheCarefulChanceIsZero()
        {
            // Rushed always nips, careful never — the care read is the difference.
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 1f, nipCareful: 0f);
            DeckPot pot = MakePot(def, Lobster());

            Assert.AreEqual(GrabOutcome.Nipped, pot.TryGrabNext(0.15f, out _),
                "a snatch at the quick mark risks the full rushed chance (1) — nipped");
            Assert.AreEqual(GrabOutcome.Keeper, pot.TryGrabNext(0.9f, out _),
                "a full hold eases the chance to the careful floor (0) — clean grab");
        }

        // ---- the sort ------------------------------------------------------------------------------

        [Test]
        public void Shorts_GoBackOverTheSide_ValueZero()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 1000f, berriedChance: 0f, nipRushed: 0f, nipCareful: 0f);
            DeckPot pot = MakePot(def, Lobster());

            Assert.AreEqual(GrabOutcome.ReturnedShort, pot.TryGrabNext(1f, out DeckPot.Animal a),
                "under the gauge (the window tops out below the min keep) → a short");
            Assert.AreEqual(DeckAnimalFate.Returned, a.Fate);
            Assert.AreEqual(0, pot.OnDeckCount, "a short never waits for banding — it went straight back");
            var hold = new FakeHold();
            Assert.AreEqual(BandOutcome.None, pot.TryBandNext(hold, out _), "nothing to band");
            Assert.AreEqual(0, hold.UsedUnits, "value zero — nothing landed");
        }

        [Test]
        public void BerriedHens_GoBack_RegardlessOfSize()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 1f, nipRushed: 0f, nipCareful: 0f);
            DeckPot pot = MakePot(def, Lobster());

            Assert.AreEqual(GrabOutcome.ReturnedBerried, pot.TryGrabNext(1f, out DeckPot.Animal a),
                "berried chance 1 ⇒ a hen, and she goes back even at legal size");
            Assert.AreEqual(DeckAnimalFate.Returned, a.Fate);
        }

        [Test]
        public void ASpeciesWithoutARule_IsAlwaysAKeeper()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 1000f, berriedChance: 1f, nipRushed: 0f, nipCareful: 0f);
            var odd = new CatchItem("fish.unruled", "Odd Catch", FishCategory.Shellfish, 1f, 5, 0.3f);
            DeckPot pot = MakePot(def, odd);

            Assert.AreEqual(GrabOutcome.Keeper, pot.TryGrabNext(1f, out DeckPot.Animal a),
                "no rule ⇒ nothing gates it back (the documented always-keeper convention)");
            Assert.IsFalse(a.HasRule);
        }

        // ---- band + bait + ready -------------------------------------------------------------------

        [Test]
        public void TheFullCycle_PickBandBait_ReachesReadyToSet()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 0f, nipCareful: 0f);
            DeckPot pot = MakePot(def, Lobster());
            var hold = new FakeHold();

            Assert.IsFalse(pot.NeedsBait, "a full pot isn't ready for bait");
            Assert.AreEqual(GrabOutcome.Keeper, pot.TryGrabNext(1f, out _));
            Assert.IsFalse(pot.NeedsBait, "an unbanded keeper still blocks the bait (band first)");
            Assert.AreEqual(BandOutcome.Banded, pot.TryBandNext(hold, out _));
            Assert.IsTrue(pot.NeedsBait, "picked empty + banded ⇒ she wants bait");
            Assert.IsFalse(pot.ReadyToSet);

            pot.MarkBaited(null);
            Assert.IsTrue(pot.ReadyToSet, "baited and squared away ⇒ T can set her");
        }

        [Test]
        public void AFullHold_LeavesTheKeeperOnDeck_NoPenalty()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 0f, nipCareful: 0f);
            DeckPot pot = MakePot(def, Lobster());
            pot.TryGrabNext(1f, out _);

            var full = new FakeHold { Capacity = 0 };
            Assert.AreEqual(BandOutcome.NoRoom, pot.TryBandNext(full, out DeckPot.Animal a));
            Assert.AreEqual(DeckAnimalFate.OnDeck, a.Fate, "the keeper waits — sell and come back, nothing lost");
        }

        // ---- the cozy auto-resolve (the ADR 0020 greybox compromise) --------------------------------

        [Test]
        public void AutoResolve_LandsKeepersAndReturnsTheRest_PerTheDeterministicSort()
        {
            // Three animals: forced keeper facts (min keep 0), no nips involved — auto-resolve straight
            // off the pot: every keeper lands, nothing else exists.
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 1f, nipCareful: 1f);
            DeckPot pot = MakePot(def, Lobster(0.7f), Lobster(1.1f), Lobster(2.3f));
            var hold = new FakeHold();
            var landed = new List<CatchItem>();

            pot.AutoResolve(hold, landed, out int kept, out int returned, out int noRoom);

            Assert.AreEqual(3, kept, "everything aboard landed as if picked + banded (nip risk never gates the resolve)");
            Assert.AreEqual(0, returned);
            Assert.AreEqual(0, noRoom);
            Assert.AreEqual(3, hold.UsedUnits);
            Assert.AreEqual(3, landed.Count, "each landed item is reported so the caller can publish FishCaught");
        }

        [Test]
        public void AutoResolve_NeverDupes_WhatWasAlreadyBandedOrReturned()
        {
            DeckWorkDef def = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 0f, nipCareful: 0f);
            DeckPot pot = MakePot(def, Lobster(0.7f), Lobster(1.1f));
            var hold = new FakeHold();

            // Work the first animal fully by hand: picked + banded → 1 in the hold.
            pot.TryGrabNext(1f, out _);
            pot.TryBandNext(hold, out _);
            Assert.AreEqual(1, hold.UsedUnits);

            // Auto-resolve the rest: only the second animal lands — the banded one is NOT re-added.
            var landed = new List<CatchItem>();
            pot.AutoResolve(hold, landed, out int kept, out _, out _);
            Assert.AreEqual(1, kept, "only what was still aboard resolved");
            Assert.AreEqual(2, hold.UsedUnits, "no dupes — the hand-banded keeper stayed a single item");
        }

        [Test]
        public void AutoResolve_ReturnsShorts_AndCountsNoRoomHonestly()
        {
            // Everything is a short → returned, nothing landed.
            DeckWorkDef shortDef = MakeDef(minKeepMm: 1000f, berriedChance: 0f, nipRushed: 0f, nipCareful: 0f);
            DeckPot shortPot = MakePot(shortDef, Lobster());
            var hold = new FakeHold();
            shortPot.AutoResolve(hold, null, out int kept, out int returned, out int noRoom);
            Assert.AreEqual(0, kept);
            Assert.AreEqual(1, returned, "the short went back — exactly what the hand sort would have done");
            Assert.AreEqual(0, hold.UsedUnits);

            // A full hold refuses the keeper — counted, never silent.
            DeckWorkDef keepDef = MakeDef(minKeepMm: 0f, berriedChance: 0f, nipRushed: 0f, nipCareful: 0f);
            DeckPot keepPot = MakePot(keepDef, Lobster());
            var full = new FakeHold { Capacity = 0 };
            keepPot.AutoResolve(full, null, out kept, out returned, out noRoom);
            Assert.AreEqual(0, kept);
            Assert.AreEqual(1, noRoom, "a refused keeper is counted so the one toast can say so");
        }
    }
}
