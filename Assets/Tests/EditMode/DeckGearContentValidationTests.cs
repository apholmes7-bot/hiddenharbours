using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the stern-deck gear assets (the <c>ContentValidationTests</c> pattern, over
    /// the ACTUAL Data/ content): unique <c>type.snake_case</c> ids, capacities that match the rig's own
    /// <c>TrapIso.CAPS</c>, stations that match <c>DeckGear.STATIONS</c> — and the load-bearing one, that
    /// every mount and every stack base lands ON the hull's authored deck.
    ///
    /// <para><b>Why the rigs' numbers are pinned HERE and not in the maths.</b> A capacity written into a
    /// C# file would be the defect the whole data-driven placement exists to prevent, so the placement
    /// tests deliberately pass every number in. That leaves exactly one honest place to check that the
    /// shipped ASSETS carry the numbers the rigs measured, and this is it. If a rig is re-measured, these
    /// are the assertions that go red and say so.</para>
    /// </summary>
    public class DeckGearContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const float Tol = 1e-3f;

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static BoatDeckGearDef GearFor(string id)
        {
            foreach (var g in LoadAll<BoatDeckGearDef>()) if (g.Id == id) return g;
            return null;
        }

        /// <summary>The one shipped kit. Asserted rather than indexed, so a missing asset fails with a
        /// sentence instead of an IndexOutOfRange from the fixture.</summary>
        private static DeckGearKitDef OnlyKit()
        {
            var kits = LoadAll<DeckGearKitDef>();
            Assert.IsNotEmpty(kits, "the deck-loop kit's station table ships");
            return kits[0];
        }

        private static BoatDeckDef DeckFor(string id)
        {
            foreach (var d in LoadAll<BoatDeckDef>()) if (d.Id == id) return d;
            return null;
        }

        // ---- ids ------------------------------------------------------------------------------------

        [Test]
        public void DeckGearDefs_HaveNonEmptyUniqueSnakeCaseIds()
        {
            var seen = new Dictionary<string, string>();
            var idRule = new Regex("^[a-z][a-z0-9]*\\.[a-z0-9_]+$");

            var gear = LoadAll<BoatDeckGearDef>();
            Assert.IsNotEmpty(gear, "the slice ships deck gear for at least one hull");
            var kits = LoadAll<DeckGearKitDef>();
            Assert.IsNotEmpty(kits, "the slice ships the deck-loop kit's station table");

            foreach (var g in gear) Register(seen, g.Id, AssetDatabase.GetAssetPath(g), idRule);
            foreach (var k in kits) Register(seen, k.Id, AssetDatabase.GetAssetPath(k), idRule);
        }

        private static void Register(Dictionary<string, string> seen, string id, string path, Regex rule)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{path}: empty id");
            Assert.IsTrue(rule.IsMatch(id),
                $"{path}: id '{id}' is not type.snake_case (CLAUDE.md §5)");
            Assert.IsFalse(seen.ContainsKey(id),
                $"duplicate id '{id}' in '{path}' and '{(seen.ContainsKey(id) ? seen[id] : "?")}'");
            seen[id] = path;
        }

        [Test]
        public void EachDefIsOneEntityPerFile()
        {
            foreach (var g in LoadAll<BoatDeckGearDef>())
            {
                string path = AssetDatabase.GetAssetPath(g);
                Assert.AreEqual(1, AssetDatabase.LoadAllAssetsAtPath(path).Length,
                    $"{path}: one entity per file (CLAUDE.md rule 2)");
            }
        }

        // ---- the rigs' own numbers, as shipped -------------------------------------------------------

        /// <summary>
        /// <b>TrapIso.CAPS, as shipped.</b> A lobster boat takes 5 on the deck and 2 on the washboard; a
        /// Cape Islander takes 3 and 2. The hulls DISAGREEING is the point — one capacity for the fleet is
        /// the same defect as one deck rectangle for the fleet.
        /// </summary>
        [Test]
        public void StackCapacitiesMatchTheRigsCaps()
        {
            var lobster = GearFor("deckgear.lobster_boat");
            Assert.IsNotNull(lobster, "the lobster boat's deck gear ships");
            Assert.AreEqual(5, lobster.CapacityOf(DeckStackSurface.Deck), "TrapIso.CAPS.lobsterBoat.deck");
            Assert.AreEqual(2, lobster.CapacityOf(DeckStackSurface.Washboard),
                "TrapIso.CAPS.lobsterBoat.washboard");

            var cape = GearFor("deckgear.cape_islander");
            Assert.IsNotNull(cape, "the Cape Islander's deck gear ships");
            Assert.AreEqual(3, cape.CapacityOf(DeckStackSurface.Deck), "TrapIso.CAPS.capeIslander.deck");
            Assert.AreEqual(2, cape.CapacityOf(DeckStackSurface.Washboard),
                "TrapIso.CAPS.capeIslander.washboard");

            Assert.AreNotEqual(lobster.TotalStackCapacity(), cape.TotalStackCapacity(),
                "the two hulls must not have been given the same capacity by copy-paste");
        }

        /// <summary>
        /// <b>DeckGear.STATIONS, as shipped</b> — and in particular the one that is easy to get wrong: the
        /// hauler's operator faces OUTBOARD, turn 4. (The rig's own PROSE above the table says "turn 0";
        /// the TABLE says 4, and the table is the data. Everything downstream — DeckGearPlacement's doc,
        /// this slice — agrees with the table.)
        /// </summary>
        [Test]
        public void KitStationsMatchTheRigsStationTable()
        {
            DeckGearKitDef kit = OnlyKit();

            var hauler = kit.EntryFor(DeckGearKind.HaulerStation);
            Assert.IsNotNull(hauler, "the kit carries the hauler station");
            Assert.AreEqual(4, hauler.Turn, "the hauler's operator faces OUTBOARD — STATIONS.turn is 4");
            Assert.AreEqual(1.05f, hauler.WorkZMeters, Tol, "STATIONS.haulerstation.workZ");
            Assert.AreEqual(new Vector3(-0.30f, 0.44f, 0f), hauler.StandLocalMeters);

            var bench = kit.EntryFor(DeckGearKind.BandingTable);
            Assert.IsNotNull(bench, "the kit carries the banding bench");
            Assert.AreEqual(0, bench.Turn, "every station but the hauler's faces the gear — turn 0");
            Assert.AreEqual(0.885f, bench.WorkZMeters, Tol, "STATIONS.bandingtable.workZ");

            var chop = kit.EntryFor(DeckGearKind.ChopBoard);
            Assert.IsNotNull(chop, "the kit carries the chopping board");
            Assert.AreEqual(0.775f, chop.WorkZMeters, Tol, "STATIONS.chopboard.workZ");

            // ⚠️ The kit turns the OTHER WAY from the fleet's boats; its own contract says so and says
            // that applying the fleet correction mirrors all eight cells.
            Assert.IsFalse(kit.FacingsAreCounterClockwise,
                "the deck-loop kit is CLOCKWISE — deckLoopKit.contract.json says so in as many words");
            Assert.AreEqual(8, kit.FacingCount);
        }

        /// <summary>
        /// <b>The hauler mounts are the HULLS' own HAULER blocks</b> — measured per hull in a V8 host, and
        /// different on the two boats, which is the whole reason they are data.
        /// </summary>
        [Test]
        public void HaulerMountsAreTheHullsOwnMeasuredBlocks()
        {
            var lobster = GearFor("deckgear.lobster_boat").MountFor(DeckGearKind.HaulerStation);
            Assert.IsNotNull(lobster, "the lobster boat works her pots at a hauler");
            Assert.AreEqual(new Vector3(1.34f, -1.50f, 1.44f), lobster.MountLocalMeters,
                "lobsterBoatIsoRig.HAULER");

            var cape = GearFor("deckgear.cape_islander").MountFor(DeckGearKind.HaulerStation);
            Assert.IsNotNull(cape, "the Cape Islander works her pots at a hauler");
            Assert.AreEqual(new Vector3(1.30f, -2.56f, 1.592f), cape.MountLocalMeters,
                "capeIslanderIsoRig.HAULER");
        }

        /// <summary>TrapIso.STACK, as shipped on the pots themselves — a step of 0 would collapse a stack
        /// into one pot, so every shipped trap that opts into deck work must carry one.</summary>
        [Test]
        public void TrapsThatWorkOnDeckCarryTheirOwnStackStep()
        {
            var traps = LoadAll<TrapDef>();
            Assert.IsNotEmpty(traps);
            foreach (var t in traps)
            {
                if (t.DeckWork == null) continue;   // opt-in: a trap without deck work never stacks
                string path = AssetDatabase.GetAssetPath(t);
                Assert.Greater(t.StackStepMeters, 0f,
                    $"{path}: a deck-working pot needs its TrapIso.STACK step — 0 collapses the stack");
                Assert.Less(t.StackStepMeters, 1.5f,
                    $"{path}: a tier step over 1.5 m is a height, not a NEST (a cone nests at 0.52, not 1.18)");
            }
        }

        // ---- the load-bearing check: the gear is ON the deck -----------------------------------------

        /// <summary>
        /// <b>Every mount and every deck stack base lands on the hull's own authored deck.</b>
        ///
        /// <para>This is the assertion that makes "never one hard-coded rectangle" checkable: the gear's
        /// positions are cross-examined against the SAME polygons the deck walk clamps the player to
        /// (<see cref="BoatDeckDef"/>, imported from each hull's gameplay sidecar). A mount typed by eye —
        /// or copied from the other hull — lands off the sole and this goes red.</para>
        ///
        /// <para>Washboard bases are checked against the washboard strips instead, which is what
        /// <c>includeWashboards</c> opens.</para>
        /// </summary>
        [Test]
        public void EveryMountAndStackBaseLandsOnTheHullsOwnDeck()
        {
            AssertGearSitsOnDeck("deckgear.lobster_boat", "deck.lobster_boat_iso");
            AssertGearSitsOnDeck("deckgear.cape_islander", "deck.cape_islander_iso");
        }

        private static void AssertGearSitsOnDeck(string gearId, string deckId)
        {
            BoatDeckGearDef gear = GearFor(gearId);
            BoatDeckDef deck = DeckFor(deckId);
            Assert.IsNotNull(gear, $"{gearId} ships");
            Assert.IsNotNull(deck, $"{deckId} ships — the gear is checked against the hull's own polygons");
            Assert.IsTrue(deck.HasWalkableDeck(), $"{deckId} has authored deck areas");

            foreach (var m in gear.Mounts)
            {
                // The hauler is BOLTED TO THE WASHBOARD (it is a section of washboard, by the kit's own
                // description), so it is the one mount checked with the side decks open.
                bool washboard = m.Kind == DeckGearKind.HaulerStation;
                AssertOnDeck(deck, m.MountLocalMeters, washboard,
                             $"{gearId}: the {m.Kind} mount");
            }

            foreach (var b in gear.StackBases)
            {
                if (b.Capacity <= 0) continue;   // a surface you cannot stack on needs no base on the deck
                AssertOnDeck(deck, b.BaseLocalMeters, b.Surface == DeckStackSurface.Washboard,
                             $"{gearId}: the {b.Surface} stack base");
            }
        }

        private static void AssertOnDeck(BoatDeckDef deck, Vector3 local, bool includeWashboards, string what)
        {
            var point = new Vector2(local.x, local.y);
            int hint = -1;
            Vector2 clamped = deck.ClampToWalkable(point, ref hint, out float _, includeWashboards);
            Assert.AreEqual(point.x, clamped.x, Tol,
                $"{what} at {point} is off the hull's authored deck (clamped to {clamped})");
            Assert.AreEqual(point.y, clamped.y, Tol,
                $"{what} at {point} is off the hull's authored deck (clamped to {clamped})");
        }

        /// <summary>
        /// <b>And so does every OPERATOR — the crew contract, checked.</b> A mount can sit perfectly on the
        /// sole and still put its operator over the side, because the stand offset is in the GEAR's frame
        /// and rotates with it. This walks each station through
        /// <see cref="DeckGearPlacement.OperatorStand"/> — the same call production makes — and asserts the
        /// feet land on the hull's own authored sole.
        ///
        /// <para>The hauler is the one that would fail loudest: its stand is offset ACROSS as well as
        /// along (−0.30, +0.44), so on a rail-mounted station four of the eight bearings put the worker
        /// outboard of the hull entirely.</para>
        /// </summary>
        [Test]
        public void EveryOperatorStandsOnTheHullsOwnDeck()
        {
            AssertOperatorsStandOnDeck("deckgear.lobster_boat", "deck.lobster_boat_iso");
            AssertOperatorsStandOnDeck("deckgear.cape_islander", "deck.cape_islander_iso");
        }

        private static void AssertOperatorsStandOnDeck(string gearId, string deckId)
        {
            BoatDeckGearDef gear = GearFor(gearId);
            BoatDeckDef deck = DeckFor(deckId);
            DeckGearKitDef kit = OnlyKit();

            foreach (var m in gear.Mounts)
            {
                DeckGearKitEntry entry = kit.EntryFor(m.Kind);
                Assert.IsNotNull(entry, $"{gearId}: the kit carries no station for {m.Kind}");

                Vector3 stand = DeckGearPlacement.OperatorStand(m.MountLocalMeters, m.Dir, entry.Station());
                AssertOnDeck(deck, stand, includeWashboards: false,
                             $"{gearId}: the operator of the {m.Kind}");
            }
        }

        /// <summary>
        /// <b>The two hulls were laid out separately.</b> A copy-paste would put the same gear at the same
        /// metres on boats of different beam and different sheer, which is exactly the defect the per-hull
        /// data exists to prevent — and it would still pass every other test here.
        /// </summary>
        [Test]
        public void TheTwoHullsGearIsNotACopyOfEachOther()
        {
            BoatDeckGearDef lobster = GearFor("deckgear.lobster_boat");
            BoatDeckGearDef cape = GearFor("deckgear.cape_islander");

            int shared = 0;
            foreach (var m in lobster.Mounts)
            {
                var other = cape.MountFor(m.Kind);
                if (other != null && other.MountLocalMeters == m.MountLocalMeters) shared++;
            }
            Assert.Zero(shared,
                "no piece of gear may sit at identical hull-local metres on both hulls — they are " +
                "different boats, and their sidecars say so");
        }

        /// <summary>The whole loop must be workable on each hull: every beat the cycle plays needs a
        /// station to play it at, or the hands have nowhere to be.</summary>
        [Test]
        public void EachHullCarriesAStationForEveryWorkedBeat()
        {
            foreach (string id in new[] { "deckgear.lobster_boat", "deckgear.cape_islander" })
            {
                BoatDeckGearDef gear = GearFor(id);
                Assert.IsTrue(gear.HasHaulerStation(), $"{id}: works the hauler");
                Assert.IsNotNull(gear.MountFor(DeckGearKind.BandingTable), $"{id}: empties at a bench");
                Assert.IsNotNull(gear.MountFor(DeckGearKind.ChopBoard), $"{id}: chops bait at a board");
                Assert.IsNotNull(gear.StackBaseFor(DeckStackSurface.Deck), $"{id}: stacks on her deck");
            }
        }
    }
}
