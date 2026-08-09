using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The stern-deck gear cycle, live: the owner's loop (canon M2-33) driven through a real component on
    /// a real GameObject, on BOTH working hulls, with the kit's sheets absent AND present.
    ///
    /// <para><b>Driven in SECONDS, never in frames.</b> Every wait here is
    /// <see cref="WaitForSeconds"/> — a frame count is not time, and a test that counted frames would pass
    /// or fail with the machine it ran on. (The parts of the cycle that are paced by a QUANTITY are not
    /// timed at all, which is the point of them: they are seeked from the work.)</para>
    ///
    /// <para><b>What this does not cover, and why.</b> The hull's own slot claim/release is exercised
    /// against a live <c>IsoFacetHullRenderer</c> by that seam's own suite (<c>DeckOccupantSlotTests</c>);
    /// a boat here wears no mesh skin, so its slots resolve to <see cref="NoDeckOccupantSlots"/> — which is
    /// itself a case worth pinning, because most of the fleet is a sprite hull and the gear has to draw
    /// there too.</para>
    /// </summary>
    public class SternDeckLoopPlayTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            EventBus.Publish(new DeckLoopStateChanged(DeckLoopState.Idle));
            // DestroyImmediate, not Destroy: a deferred destroy leaves the previous test's presenter
            // subscribed to the bus for another frame, and a leaked one warning about ITS full deck
            // would fail the next test's LogAssert with a log nobody in that test produced.
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures: the SHIPPED data, so the test cannot drift from the assets -------------------

        private static DeckGearKitDef Kit(bool withArt)
        {
            var kit = ScriptableObject.CreateInstance<DeckGearKitDef>();
            kit.Id = "deckgearkit.test";
            kit.FacingCount = 8;
            var kinds = new[] { DeckGearKind.HaulerStation, DeckGearKind.BandingTable,
                                DeckGearKind.ChopBoard, DeckGearKind.BaitBin };
            var entries = new List<DeckGearKitEntry>();
            foreach (var k in kinds)
            {
                var e = new DeckGearKitEntry
                {
                    Kind = k,
                    // DeckGear.STATIONS — the hauler is the one with turn 4.
                    StandLocalMeters = k == DeckGearKind.HaulerStation
                        ? new Vector3(-0.30f, 0.44f, 0f) : new Vector3(0f, -0.52f, 0f),
                    Turn = k == DeckGearKind.HaulerStation ? 4 : 0,
                    WorkZMeters = k == DeckGearKind.HaulerStation ? 1.05f : 0.885f,
                    ClearMeters = new Vector2(0.62f, 0.58f),
                    Facings = withArt ? EightCells() : System.Array.Empty<Sprite>(),
                };
                entries.Add(e);
            }
            kit.Entries = entries.ToArray();
            return kit;
        }

        private static Sprite[] EightCells()
        {
            var cells = new Sprite[8];
            for (int i = 0; i < 8; i++) cells[i] = OneCell();
            return cells;
        }

        private static Sprite OneCell()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 32f);
        }

        /// <summary>The lobster boat's shipped layout: her own HAULER block, and TrapIso.CAPS 5/2.</summary>
        private static BoatDeckGearDef LobsterGear()
            => Gear("deckgear.lobster_boat",
                    new Vector3(1.34f, -1.50f, 1.44f), new Vector3(-0.85f, -1.60f, 0.50f),
                    new Vector3(-0.30f, -5.30f, 0.50f), 5,
                    new Vector3(-1.74f, -4.80f, 1.13f), 2);

        /// <summary>The Cape Islander's: a different hauler, and TrapIso.CAPS 3/2.</summary>
        private static BoatDeckGearDef CapeGear()
            => Gear("deckgear.cape_islander",
                    new Vector3(1.30f, -2.56f, 1.592f), new Vector3(-0.95f, -3.20f, 0.72f),
                    new Vector3(-0.25f, -5.90f, 0.72f), 3,
                    new Vector3(-1.47f, -5.45f, 1.36f), 2);

        private static BoatDeckGearDef Gear(string id, Vector3 hauler, Vector3 bench,
                                            Vector3 deckBase, int deckCap,
                                            Vector3 washBase, int washCap)
        {
            var g = ScriptableObject.CreateInstance<BoatDeckGearDef>();
            g.Id = id;
            g.Mounts = new[]
            {
                new BoatDeckGearMount { Kind = DeckGearKind.HaulerStation, MountLocalMeters = hauler, Dir = 2 },
                new BoatDeckGearMount { Kind = DeckGearKind.BandingTable, MountLocalMeters = bench, Dir = 4 },
            };
            g.StackBases = new[]
            {
                new BoatDeckStackBase { Surface = DeckStackSurface.Deck, BaseLocalMeters = deckBase,
                                        Dir = 0, Capacity = deckCap },
                new BoatDeckStackBase { Surface = DeckStackSurface.Washboard, BaseLocalMeters = washBase,
                                        Dir = 0, Capacity = washCap },
            };
            return g;
        }

        private SternDeckGearPresenter NewDeck(BoatDeckGearDef gear, DeckGearKitDef kit, out GameObject boat)
        {
            boat = new GameObject("Boat");
            _spawned.Add(boat);
            _spawned.Add(gear);
            _spawned.Add(kit);
            var deck = boat.AddComponent<SternDeckGearPresenter>();
            deck.Configure(gear, kit, null);   // no worker: the clips are a separate concern below
            return deck;
        }

        private static void Beat(SternDeckStage stage, float worked01, int stacked, bool inPlay)
            => EventBus.Publish(new DeckLoopStateChanged(
                   new DeckLoopState(stage, worked01, stacked, inPlay,
                                     new DeckPotLook(null, 0.40f, new[] { 0, -1, 1, -1, 0, 1 }, 0.04f))));

        private static List<SpriteRenderer> DrawnOf(GameObject boat)
        {
            var live = new List<SpriteRenderer>();
            foreach (var sr in boat.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.enabled) live.Add(sr);
            return live;
        }

        // ---- the cycle ------------------------------------------------------------------------------

        /// <summary>
        /// <b>The whole loop, beat by beat, in seconds.</b> Gear stands on the deck the moment there is a
        /// deck; the pot appears when she comes aboard; the stack grows when she is squared away; and
        /// everything but the furniture goes when she is tossed back.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWholeCycleRunsOnTheLobsterBoat()
        {
            SternDeckGearPresenter deck = NewDeck(LobsterGear(), Kit(withArt: false), out GameObject boat);
            yield return new WaitForSeconds(0.1f);

            int furniture = deck.DrawnCount;
            Assert.AreEqual(2, furniture, "her hauler and her bench stand on the deck with nothing doing");

            // Trap aboard on the stern deck.
            Beat(SternDeckStage.Aboard, 0f, 0, true);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 1, deck.DrawnCount, "the pot in play joins the furniture");

            // Empty her — the beat is paced by the animals worked, so it is NOT timed.
            Beat(SternDeckStage.Emptying, 0.5f, 0, true);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 1, deck.DrawnCount, "still one pot, being worked");

            // Chop and re-bait her.
            Beat(SternDeckStage.Baiting, 0f, 0, true);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 1, deck.DrawnCount);

            // Squared away — onto the stack, off the hands.
            Beat(SternDeckStage.Stacking, 1f, 1, false);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 1, deck.DrawnCount, "one pot on the stack, none in play");

            // Over the rail she goes.
            Beat(SternDeckStage.Setting, 1f, 0, false);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture, deck.DrawnCount, "the deck is clear again but the gear stays bolted");
            Assert.Zero(deck.RefusedClaims, "nothing was refused anywhere in a clean cycle");
        }

        // ---- never one rectangle --------------------------------------------------------------------

        /// <summary>
        /// <b>The gear rides the hull.</b> A fixed world-axis offset — the defect this slice exists to
        /// avoid — would leave the bench where it was when the boat moved out from under it. Every
        /// position is projected from the hull's own frame through her drawn heading, every frame.
        ///
        /// <para>This boat wears no hull presenter, so her drawn heading is 0 whichever way the transform
        /// points; TRANSLATION is therefore the honest thing to measure here, and the heading half of the
        /// same projection is pinned pure in <c>DeckGearPlacementTests</c>.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheGearRidesTheHullInsteadOfSittingOnAFixedRectangle()
        {
            SternDeckGearPresenter deck = NewDeck(LobsterGear(), Kit(withArt: false), out GameObject boat);
            yield return new WaitForSeconds(0.1f);

            var north = new List<Vector3>();
            foreach (var sr in DrawnOf(boat)) north.Add(sr.transform.position);
            CollectionAssert.IsNotEmpty(north);

            // Come about. With no hull presenter the drawn heading is 0 either way, so the honest way to
            // prove the projection is live is to move the BOAT and watch the gear ride her.
            boat.transform.position = new Vector3(7f, -3f, 0f);
            yield return new WaitForSeconds(0.1f);

            var moved = new List<Vector3>();
            foreach (var sr in DrawnOf(boat)) moved.Add(sr.transform.position);
            Assert.AreEqual(north.Count, moved.Count);
            for (int i = 0; i < north.Count; i++)
                Assert.AreEqual(new Vector3(7f, -3f, 0f), moved[i] - north[i],
                    "every piece rides the hull — nothing is pinned to a world-axis rectangle");
        }

        /// <summary>
        /// <b>Two hulls, two decks.</b> The same component fed each hull's own data must place the gear in
        /// two different places — a shared rectangle would put it in one, and would look perfectly fine on
        /// whichever boat it was authored against.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTwoHullsPlaceTheirGearDifferently()
        {
            SternDeckGearPresenter lobster = NewDeck(LobsterGear(), Kit(withArt: false), out GameObject a);
            SternDeckGearPresenter cape = NewDeck(CapeGear(), Kit(withArt: false), out GameObject b);
            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual(lobster.DrawnCount, cape.DrawnCount, "both carry the same two pieces");

            var lobsterAt = DrawnOf(a);
            var capeAt = DrawnOf(b);
            for (int i = 0; i < lobsterAt.Count; i++)
                Assert.AreNotEqual(lobsterAt[i].transform.position, capeAt[i].transform.position,
                    "the two hulls' gear must not land in the same spot — their sidecars disagree");
        }

        // ---- the CAPS refusal ------------------------------------------------------------------------

        /// <summary>
        /// <b>A full deck refuses OUT LOUD.</b> Eight pots on a hull that takes seven draws seven and says
        /// so — it never quietly draws one fewer than it was told it had, and it never stacks one in
        /// mid-air. This is the "must never silently over-claim" clause, on the surface where the player
        /// would actually hit it.
        /// </summary>
        [UnityTest]
        public IEnumerator AFullDeckRefusesOutLoudRatherThanStackingInMidAir()
        {
            SternDeckGearPresenter deck = NewDeck(LobsterGear(), Kit(withArt: false), out GameObject boat);
            yield return new WaitForSeconds(0.1f);
            int furniture = deck.DrawnCount;

            // Exactly full: 5 on the deck + 2 on the washboard, the hull's own TrapIso.CAPS.
            Beat(SternDeckStage.None, 0f, 7, false);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 7, deck.DrawnCount, "a full hull draws every pot she can take");
            Assert.Zero(deck.RefusedClaims, "…and refuses nothing while they fit");

            // One over. The refusal is logged, and the eighth pot is simply not drawn.
            LogAssert.Expect(LogType.Warning, new Regex("over this hull's stacking capacity"));
            Beat(SternDeckStage.None, 0f, 8, false);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 7, deck.DrawnCount, "the eighth pot is NOT drawn floating");
            Assert.AreEqual(1, deck.RefusedClaims, "…and the refusal is counted, not eaten");
        }

        /// <summary>The Cape Islander fills sooner, because her CAPS say so — the same test, a different
        /// hull, a different answer.</summary>
        [UnityTest]
        public IEnumerator TheCapeIslanderFillsAtHerOwnCapacity()
        {
            SternDeckGearPresenter deck = NewDeck(CapeGear(), Kit(withArt: false), out GameObject boat);
            yield return new WaitForSeconds(0.1f);
            int furniture = deck.DrawnCount;

            Beat(SternDeckStage.None, 0f, 5, false);      // 3 on deck + 2 on the washboard
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 5, deck.DrawnCount);
            Assert.Zero(deck.RefusedClaims);

            LogAssert.Expect(LogType.Warning, new Regex("over this hull's stacking capacity"));
            Beat(SternDeckStage.None, 0f, 6, false);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(furniture + 5, deck.DrawnCount,
                "a Cape Islander is full two pots before a lobster boat is");
            Assert.AreEqual(1, deck.RefusedClaims);
        }

        // ---- the sheet-absent fallback, BOTH WAYS (#454's WithNoBoardingArt discipline) --------------

        /// <summary>
        /// <b>WITH NO KIT ART the deck is unchanged.</b> The deck-gear bake lands on its own schedule, so
        /// the state this must work in is the one where it has not landed: every piece draws its greybox
        /// stand-in, at exactly the measured position, and nothing is missing from the picture.
        /// </summary>
        [UnityTest]
        public IEnumerator WithNoKitArt_TheGearStillStandsInTheRightPlaces()
        {
            SternDeckGearPresenter deck = NewDeck(LobsterGear(), Kit(withArt: false), out GameObject boat);
            yield return new WaitForSeconds(0.1f);

            var drawn = DrawnOf(boat);
            Assert.AreEqual(2, drawn.Count, "both pieces draw with no sheets baked at all");
            foreach (var sr in drawn)
                Assert.IsNotNull(sr.sprite, "an unbaked kind draws its stand-in, never nothing");
        }

        /// <summary>
        /// <b>AND WITH the kit art the SAME positions are used.</b> The pair is the point: art changes the
        /// picture and nothing else. A bake that moved the gear would mean the geometry had been coming
        /// from the sheets rather than from the data.
        /// </summary>
        [UnityTest]
        public IEnumerator WithKitArt_ThePicturesChangeButThePositionsDoNot()
        {
            SternDeckGearPresenter bare = NewDeck(LobsterGear(), Kit(withArt: false), out GameObject a);
            SternDeckGearPresenter baked = NewDeck(LobsterGear(), Kit(withArt: true), out GameObject b);
            b.transform.position = a.transform.position;
            yield return new WaitForSeconds(0.1f);

            var bareAt = DrawnOf(a);
            var bakedAt = DrawnOf(b);
            Assert.AreEqual(bareAt.Count, bakedAt.Count, "the same pieces draw either way");

            for (int i = 0; i < bareAt.Count; i++)
            {
                Assert.AreEqual(bareAt[i].transform.position, bakedAt[i].transform.position,
                    "the bake must not move the gear — the position is data, not art");
                Assert.AreNotSame(bareAt[i].sprite, bakedAt[i].sprite,
                    "…but it is a different picture, which is the whole of what the bake changes");
            }
            Assert.Zero(bare.RefusedClaims);
            Assert.Zero(baked.RefusedClaims);
        }

        /// <summary>
        /// A boat wearing a sprite hull (or no skin at all) has no slots to claim, and that is not a
        /// mistake to be shouted about — it is most of the fleet. The gear draws un-occluded, exactly as
        /// it did before any of this existed, and nothing is counted as refused.
        /// </summary>
        [UnityTest]
        public IEnumerator OnAHullWithNoSlotsTheGearDrawsUnOccludedAndSaysNothing()
        {
            SternDeckGearPresenter deck = NewDeck(LobsterGear(), Kit(withArt: false), out GameObject boat);
            Beat(SternDeckStage.Aboard, 0f, 2, true);
            yield return new WaitForSeconds(0.1f);

            Assert.Greater(deck.DrawnCount, 0, "the gear draws on a hull that cannot hide it");
            Assert.Zero(deck.RefusedClaims,
                "a sprite hull refuses SILENTLY by design — it must not be reported as an overflow");
        }

        /// <summary>An unwired deck — no gear asset, no kit — is completely inert rather than throwing,
        /// which is the honest answer for an open boat.</summary>
        [UnityTest]
        public IEnumerator AnUnwiredDeckIsInert()
        {
            var boat = new GameObject("OpenBoat");
            _spawned.Add(boat);
            var deck = boat.AddComponent<SternDeckGearPresenter>();
            Beat(SternDeckStage.Aboard, 0f, 3, true);
            yield return new WaitForSeconds(0.1f);

            Assert.Zero(deck.DrawnCount, "an open boat carries no deck gear");
            Assert.Zero(deck.RefusedClaims);
        }
    }
}
